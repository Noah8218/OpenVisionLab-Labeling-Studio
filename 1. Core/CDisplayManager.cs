using Lib.Common;
using OpenCvSharp;
using OpenVisionLab.ImageCanvas;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace MvcVisionSystem._1._Core
{
    public static class CDisplayManager
    {
        public static EventHandler<EventArgs> EventUpdateParameter;
        public static EventHandler<EventArgs> EventUpdateResult;

        private static readonly DisplayLayerStore LayerStore = new DisplayLayerStore(() => Displays);
        private static Mat _imageSrc = new Mat();

        public static List<DisplayLayerDocument> Displays { get; set; } = new List<DisplayLayerDocument>();

        public static event EventHandler<DisplayAnnotationSelectionChangedEventArgs> AnnotationSelectionChanged;

        public static Mat ImageSrc
        {
            get => _imageSrc;
            set
            {
                if (ReferenceEquals(_imageSrc, value))
                {
                    return;
                }

                Mat previous = _imageSrc;
                _imageSrc = value ?? new Mat();
                previous?.Dispose();
            }
        }

        public static string SelecteItem { get; set; } = "Main";

        public static string FocusItem { get; set; } = "";

        public static int LayerCount => LayerStore.Count;

        private static string m_TackTime;

        public static string TackTime
        {
            get => m_TackTime;
            set
            {
                m_TackTime = value;
                EventUpdateResult?.Invoke(null, EventArgs.Empty);
            }
        }

        public static void SetForm(object form)
        {
        }

        public static void SetDockPanel(object dockPanel)
        {
        }

        public static void SetDisplayPanel(object panel)
        {
        }

        public static void SetDisplayLayerList(List<DisplayLayerDocument> display)
        {
            Displays = display ?? new List<DisplayLayerDocument>();
        }

        public static IReadOnlyList<DisplayLayerInfo> GetLayerInfos() => LayerStore.GetInfos();

        public static string GetLayerTitle(int index) => LayerStore.GetTitle(index);

        public static DisplayLayerDocument GetLayerDisplayOrNull(string title) => LayerStore.GetByTitleOrFirst(title);

        public static DisplayLayerDocument GetLayerDisplayOrNull(int index) => LayerStore.GetOrNull(index);

        public static DisplayLayerDocument GetMainDisplayOrNull() => LayerStore.GetOrNull(DEFINE.Main);

        public static DisplayLayerDocument GetSelectedDisplayOrNull() => LayerStore.GetByTitleOrFirst(SelecteItem);

        public static bool IsDisplayInvokeRequired => false;

        public static void CreatePanel(Bitmap bitmap = null)
        {
            string title = $"Layer {LayerCount + 1}";
            if (bitmap == null)
            {
                using Bitmap placeholder = new Bitmap(10, 10);
                CreateLayerDisplay(placeholder, title, true);
                return;
            }

            CreateLayerDisplay(bitmap, title, true);
        }

        public static int FindIndex(string strTitle)
        {
            int index = LayerStore.FindIndex(strTitle);
            return index >= 0 ? index : 0;
        }

        public static int FindIndex()
        {
            int index = LayerStore.FindIndex(SelecteItem);
            return index >= 0 ? index : 0;
        }

        private static void ClearEmptyDisplay()
        {
            LayerStore.RemoveEmpty();
        }

        public static void CreateLayerDisplay(Mat imageSource, string strTitle, bool bUseClose = true, bool activate = true)
        {
            using Bitmap image = CImageConverter.ToBitmap(imageSource);
            CreateLayerDisplay(image, strTitle, bUseClose, null, activate);
        }

        public static void CreateLayerDisplay(
            Bitmap imageSource,
            string strTitle,
            bool bUseClose = true,
            IEnumerable<DetectionOverlayItem> detectionOverlays = null,
            bool activate = true)
        {
            ClearEmptyDisplay();

            string previousSelectedTitle = SelecteItem;
            DisplayLayerDocument previousDisplay = LayerStore.GetByTitleOrFirst(previousSelectedTitle);
            int existingIndex = LayerStore.FindIndex(strTitle);

            if (existingIndex < 0)
            {
                DisplayLayerDocument display = LayerStore.Create(imageSource, bUseClose, strTitle);
                display.SetDetectionOverlays(detectionOverlays);
                display.ZoomToFit();
                SetActiveImageSourceIfMain(strTitle, imageSource);

                if (activate)
                {
                    FocusItem = display.Text;
                    SelecteItem = display.Text;
                }
                else if (previousDisplay != null)
                {
                    FocusItem = previousDisplay.Text;
                    SelecteItem = previousDisplay.Text;
                }
                else
                {
                    SelecteItem = previousSelectedTitle;
                }

                return;
            }

            DisplayLayerDocument existingDisplay = LayerStore.GetOrNull(existingIndex);
            if (existingDisplay == null)
            {
                return;
            }

            if (string.Equals(strTitle, "Main", StringComparison.OrdinalIgnoreCase))
            {
                existingDisplay.ResetAnnotations();
            }

            existingDisplay.SetImage(imageSource);
            existingDisplay.SetDetectionOverlays(detectionOverlays);
            SetActiveImageSourceIfMain(strTitle, imageSource);

            if (activate)
            {
                FocusItem = existingDisplay.Text;
                SelecteItem = existingDisplay.Text;
            }
            else if (previousDisplay != null)
            {
                FocusItem = previousDisplay.Text;
                SelecteItem = previousDisplay.Text;
            }
        }

        public static void ZoomToFit(string strTitle)
        {
            GetLayerDisplayOrNull(strTitle)?.ZoomToFit();
        }

        public static bool SetDetectionOverlays(string title, IEnumerable<DetectionOverlayItem> detectionOverlays)
        {
            int index = LayerStore.FindIndex(title);
            DisplayLayerDocument display = LayerStore.GetOrNull(index);
            if (display == null)
            {
                return false;
            }

            display.SetDetectionOverlays(detectionOverlays);
            return true;
        }

        public static void NotifyAnnotationSelectionChanged(DisplayLayerDocument display, LabelingAnnotationSelectionChangedEventArgs selection)
        {
            AnnotationSelectionChanged?.Invoke(
                null,
                new DisplayAnnotationSelectionChangedEventArgs(display, selection));
        }

        public static void ActivateLayer(string title)
        {
            DisplayLayerDocument display = GetLayerDisplayOrNull(title);
            if (display == null)
            {
                return;
            }

            FocusItem = display.Text;
            SelecteItem = display.Text;
        }

        public static void ActivateLayer(int index)
        {
            DisplayLayerDocument display = GetLayerDisplayOrNull(index);
            if (display == null)
            {
                return;
            }

            FocusItem = display.Text;
            SelecteItem = display.Text;
        }

        public static void RefreshLayer(int index)
        {
            LayerStore.GetOrNull(index)?.RefreshViewer();
        }

        public static Bitmap GetLayerImage(string title)
        {
            return GetLayerDisplayOrNull(title)?.GetCurrentImage();
        }

        public static Bitmap GetLayerImage(int index)
        {
            return GetLayerDisplayOrNull(index)?.GetCurrentImage();
        }

        public static bool IsLayerImageChanged(string title)
        {
            return GetLayerDisplayOrNull(title)?.ImageChanged == true;
        }

        public static void AcceptLayerImageChanged(string title)
        {
            GetLayerDisplayOrNull(title)?.AcceptImageChanged();
        }

        public static void InvokeOnDisplayThread(Action action)
        {
            action?.Invoke();
        }

        public static TResult InvokeOnDisplayThread<TResult>(Func<TResult> action)
        {
            return action == null ? default : action();
        }

        private static void SetActiveImageSourceIfMain(string title, Bitmap image)
        {
            if (image == null || !string.Equals(title, "Main", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            ImageSrc = BitmapImageConverter.ToMat(image);
        }
    }

    public sealed class DisplayAnnotationSelectionChangedEventArgs : EventArgs
    {
        public DisplayAnnotationSelectionChangedEventArgs(DisplayLayerDocument display, LabelingAnnotationSelectionChangedEventArgs selection)
        {
            Display = display;
            Selection = selection;
        }

        public DisplayLayerDocument Display { get; }

        public LabelingAnnotationSelectionChangedEventArgs Selection { get; }
    }
}
