using OpenCvSharp;
using OpenVisionLab.ImageCanvas.ViewModels;
using OpenVisionLab.ImageCanvas.Views;
using System;
using System.Drawing;
using System.Windows;
using System.Windows.Threading;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingSize = System.Drawing.Size;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the WPF-only display preview and settled canvas auto-fit callbacks.
    /// The Shell remains the owner of the active image and timer lifetime; this
    /// adapter receives those values through explicit providers.
    /// </summary>
    internal sealed class CanvasDisplayWorkspaceAdapter
    {
        private readonly Dispatcher dispatcher;
        private readonly Func<DispatcherTimer> displayAdjustmentTimerProvider;
        private readonly Func<bool> isApplicationCloseApproved;
        private readonly Func<DrawingBitmap> activeImageBitmapProvider;
        private readonly Func<DrawingSize> activeImageSizeProvider;
        private readonly Func<string> activeImagePathProvider;
        private readonly WpfCanvasPanelViewModel canvasPanelViewModel;
        private readonly RoiImageCanvasViewModel mainCanvasViewModel;
        private readonly RoiImageCanvasView mainCanvasView;
        private readonly ImageDisplayAdjustmentService imageDisplayAdjustmentService;
        private int canvasLayoutAutoFitVersion;

        internal CanvasDisplayWorkspaceAdapter(
            Dispatcher dispatcher,
            Func<DispatcherTimer> displayAdjustmentTimerProvider,
            Func<bool> isApplicationCloseApproved,
            Func<DrawingBitmap> activeImageBitmapProvider,
            Func<DrawingSize> activeImageSizeProvider,
            Func<string> activeImagePathProvider,
            WpfCanvasPanelViewModel canvasPanelViewModel,
            RoiImageCanvasViewModel mainCanvasViewModel,
            RoiImageCanvasView mainCanvasView,
            ImageDisplayAdjustmentService imageDisplayAdjustmentService)
        {
            this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            this.displayAdjustmentTimerProvider = displayAdjustmentTimerProvider
                ?? throw new ArgumentNullException(nameof(displayAdjustmentTimerProvider));
            this.isApplicationCloseApproved = isApplicationCloseApproved
                ?? throw new ArgumentNullException(nameof(isApplicationCloseApproved));
            this.activeImageBitmapProvider = activeImageBitmapProvider
                ?? throw new ArgumentNullException(nameof(activeImageBitmapProvider));
            this.activeImageSizeProvider = activeImageSizeProvider
                ?? throw new ArgumentNullException(nameof(activeImageSizeProvider));
            this.activeImagePathProvider = activeImagePathProvider
                ?? throw new ArgumentNullException(nameof(activeImagePathProvider));
            this.canvasPanelViewModel = canvasPanelViewModel
                ?? throw new ArgumentNullException(nameof(canvasPanelViewModel));
            this.mainCanvasViewModel = mainCanvasViewModel
                ?? throw new ArgumentNullException(nameof(mainCanvasViewModel));
            this.mainCanvasView = mainCanvasView
                ?? throw new ArgumentNullException(nameof(mainCanvasView));
            this.imageDisplayAdjustmentService = imageDisplayAdjustmentService
                ?? throw new ArgumentNullException(nameof(imageDisplayAdjustmentService));
        }

        #region DisplayAdjustment
        internal void ScheduleDisplayAdjustmentRefresh()
        {
            DispatcherTimer timer = displayAdjustmentTimerProvider();
            timer.Stop();
            if (!HasActiveImage())
            {
                return;
            }

            timer.Start();
        }

        internal void HandleDisplayAdjustmentRefreshTimerTick(object sender, EventArgs e)
        {
            DispatcherTimer timer = displayAdjustmentTimerProvider();
            timer.Stop();
            if (isApplicationCloseApproved())
            {
                return;
            }

            ApplyDisplayAdjustmentNow();
        }

        internal void ApplyDisplayAdjustmentNow()
        {
            if (isApplicationCloseApproved() || !HasActiveImage())
            {
                return;
            }

            DrawingBitmap activeImageBitmap = activeImageBitmapProvider();
            ImageDisplayAdjustmentOptions options = canvasPanelViewModel.GetDisplayAdjustmentOptions();
            using DrawingBitmap adjusted = imageDisplayAdjustmentService.CreateAdjustedCopy(activeImageBitmap, options);
            using Mat displayMat = BitmapMatConversionService.CopyToMat(adjusted);
            using (mainCanvasViewModel.ImageViewer.SuppressRefresh())
            {
                mainCanvasViewModel.LoadImage(
                    displayMat,
                    string.IsNullOrWhiteSpace(activeImagePathProvider())
                        ? "display-preview"
                        : System.IO.Path.GetFileName(activeImagePathProvider()));
            }
            mainCanvasViewModel.ImageViewer.RefreshGL();
        }
        #endregion

        #region CanvasLayout
        internal void HandleCanvasViewSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!HasActiveImage() || (!e.WidthChanged && !e.HeightChanged))
            {
                return;
            }

            int requestVersion = ++canvasLayoutAutoFitVersion;
            dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(() => ApplyScheduledCanvasAutoFit(requestVersion)));
        }

        private void ApplyScheduledCanvasAutoFit(int requestVersion)
        {
            if (isApplicationCloseApproved()
                || requestVersion != canvasLayoutAutoFitVersion
                || !HasActiveImage()
                || !mainCanvasView.IsVisible
                || mainCanvasView.ActualWidth <= 1D
                || mainCanvasView.ActualHeight <= 1D)
            {
                return;
            }

            mainCanvasViewModel.ImageViewer.ZoomToFit();
        }
        #endregion

        private bool HasActiveImage()
        {
            return activeImageBitmapProvider() != null && !activeImageSizeProvider().IsEmpty;
        }
    }
}
