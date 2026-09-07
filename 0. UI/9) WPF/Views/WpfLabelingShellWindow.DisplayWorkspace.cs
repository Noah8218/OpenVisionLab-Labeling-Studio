using System;
using CvMat = OpenCvSharp.Mat;
using DrawingBitmap = System.Drawing.Bitmap;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace MvcVisionSystem
{
    // Responsibility group: display adjustment and workspace layout.
    // These members remain WPF Window adapters; independent policy belongs in services.
    public partial class WpfLabelingShellWindow
    {
        #region DisplayAdjustment
        private void ScheduleDisplayAdjustmentRefresh()
        {
            shellTimers.DisplayAdjustmentRefresh.Stop();
            if (activeImageBitmap == null || activeImageSize.IsEmpty)
            {
                return;
            }

            shellTimers.DisplayAdjustmentRefresh.Start();
        }

        private void DisplayAdjustmentRefreshTimer_Tick(object sender, EventArgs e)
        {
            shellTimers.DisplayAdjustmentRefresh.Stop();
            if (isApplicationCloseApproved)
            {
                return;
            }

            ApplyDisplayAdjustmentNow();
        }

        private void ApplyDisplayAdjustmentNow()
        {
            if (isApplicationCloseApproved
                || activeImageBitmap == null
                || activeImageSize.IsEmpty)
            {
                return;
            }

            ImageDisplayAdjustmentOptions options =
                CanvasPanelViewModel.GetDisplayAdjustmentOptions();
            using DrawingBitmap adjusted =
                imageDisplayAdjustmentService.CreateAdjustedCopy(activeImageBitmap, options);
            using CvMat displayMat = BitmapMatConversionService.CopyToMat(adjusted);
            using (MainCanvasViewModel.ImageViewer.SuppressRefresh())
            {
                MainCanvasViewModel.LoadImage(
                    displayMat,
                    string.IsNullOrWhiteSpace(activeImagePath)
                        ? "display-preview"
                        : System.IO.Path.GetFileName(activeImagePath));
            }
            MainCanvasViewModel.ImageViewer.RefreshGL();
        }
        #endregion

        #region WorkspaceLayout
        private void MainCanvasView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (activeImageBitmap == null
                || activeImageSize.IsEmpty
                || (!e.WidthChanged && !e.HeightChanged))
            {
                return;
            }

            int requestVersion = ++canvasLayoutAutoFitVersion;
            Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.ContextIdle,
                new System.Action(() => ApplyScheduledCanvasAutoFit(requestVersion)));
        }

        private void ApplyScheduledCanvasAutoFit(int requestVersion)
        {
            if (isApplicationCloseApproved
                || requestVersion != canvasLayoutAutoFitVersion
                || activeImageBitmap == null
                || activeImageSize.IsEmpty
                || MainCanvasView == null
                || !MainCanvasView.IsVisible
                || MainCanvasView.ActualWidth <= 1D
                || MainCanvasView.ActualHeight <= 1D)
            {
                return;
            }

            MainCanvasViewModel.ImageViewer.ZoomToFit();
        }

        private void LeftWorkspaceSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            ShellViewModel?.SetRightWorkflowExpandedPaneWidth(RightWorkflowColumn.ActualWidth);
            BindingOperations.SetBinding(
                RightWorkflowColumn,
                ColumnDefinition.WidthProperty,
                new System.Windows.Data.Binding("ShellViewModel.RightWorkflowPaneGridLength")
                {
                    Source = this,
                    Mode = BindingMode.OneWay
                });
            SaveWorkspaceLayoutSettings();
        }

        private void RightWorkspaceSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            ShellViewModel?.SetImageQueueExpandedPaneWidth(ImageQueueColumn.ActualWidth);
            BindingOperations.SetBinding(
                ImageQueueColumn,
                ColumnDefinition.WidthProperty,
                new System.Windows.Data.Binding("ShellViewModel.ImageQueuePaneGridLength")
                {
                    Source = this,
                    Mode = BindingMode.OneWay
                });
            SaveWorkspaceLayoutSettings();
        }

        private void RestoreWorkspaceLayoutSettings()
        {
            ApplyWorkspaceLayoutSettings(workspaceLayoutSettingsService.Load());
        }

        private void ExecuteResetWorkspaceLayoutCommand()
        {
            WpfWorkspaceLayoutSettings defaults = WpfWorkspaceLayoutSettings.CreateDefault();
            ApplyWorkspaceLayoutSettings(defaults);
            PersistWorkspaceLayoutSettings(defaults);
            AppendLog("\uD328\uB110 \uB108\uBE44 \uCD08\uAE30\uD654: \uC791\uC5C5 \uD328\uB110 340px / \uC774\uBBF8\uC9C0 \uD050 320px");
        }

        private void ApplyWorkspaceLayoutSettings(WpfWorkspaceLayoutSettings settings)
        {
            WpfWorkspaceLayoutSettings normalized = WpfWorkspaceLayoutSettings.Normalize(settings);
            ShellViewModel?.SetRightWorkflowExpandedPaneWidth(normalized.WorkflowPaneWidth);
            ShellViewModel?.SetImageQueueExpandedPaneWidth(normalized.ImageQueuePaneWidth);
        }

        private void SaveWorkspaceLayoutSettings()
        {
            double queueWidth = ShellViewModel?.ImageQueueExpandedPaneWidth
                ?? (ImageQueueColumn.Width.IsAbsolute
                ? ImageQueueColumn.Width.Value
                : ImageQueueColumn.ActualWidth);
            var settings = new WpfWorkspaceLayoutSettings
            {
                WorkflowPaneWidth = ShellViewModel?.RightWorkflowExpandedPaneWidth
                    ?? WpfWorkspaceLayoutSettings.DefaultWorkflowPaneWidth,
                ImageQueuePaneWidth = queueWidth
            };
            PersistWorkspaceLayoutSettings(settings);
        }

        private void PersistWorkspaceLayoutSettings(WpfWorkspaceLayoutSettings settings)
        {
            if (!workspaceLayoutSettingsService.TrySave(settings, out string error))
            {
                AppendLog("\uD328\uB110 \uB108\uBE44 \uC800\uC7A5 \uC2E4\uD328: " + error);
            }
        }
        #endregion

    }
}
