using Lib.Common;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace MvcVisionSystem._1._Core
{
    public static class CDisplayManager
    {
        public static EventHandler<EventArgs> EventUpdateParameter;
        public static EventHandler<EventArgs> EventUpdateResult;
        private static readonly DisplayDockHost DisplayHost = new DisplayDockHost();
        private static readonly DisplayLayerStore LayerStore = new DisplayLayerStore(() => Displays);
        private static Mat _imageSrc = new Mat();
        public static List<FormLayerDisplay> Displays { get; set; } = new List<FormLayerDisplay>();        
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
            get
            {
                return m_TackTime;
            }
            set
            {
                m_TackTime = value;
                if(EventUpdateResult != null)
                {
                    EventUpdateResult(null, null);
                }
            }
        }
        
        public static void SetForm(Form form) => DisplayHost.SetOwner(form);        
        public static void SetDockPanel(DockPanel dockPanel) => DisplayHost.SetDockPanel(dockPanel);         
        public static void SetDisplayPanel(Control panel) => DisplayHost.SetDisplayPanel(panel);
        public static void SetDisplayLayerList(List<FormLayerDisplay> Display) => Displays = Display ?? new List<FormLayerDisplay>();

        public static IReadOnlyList<DisplayLayerInfo> GetLayerInfos() => LayerStore.GetInfos();

        public static string GetLayerTitle(int index) => LayerStore.GetTitle(index);

        public static FormLayerDisplay GetLayerDisplayOrNull(string title) => LayerStore.GetByTitleOrFirst(title);

        public static FormLayerDisplay GetLayerDisplayOrNull(int index) => LayerStore.GetOrNull(index);

        public static FormLayerDisplay GetMainDisplayOrNull() => LayerStore.GetOrNull(DEFINE.Main);

        public static FormLayerDisplay GetSelectedDisplayOrNull() => LayerStore.GetByTitleOrFirst(SelecteItem);

        public static bool IsDisplayInvokeRequired => DisplayHost.IsInvokeRequired;

        public static void CreatePanel(Bitmap bitmap = null)
        {
            InvokeOnDisplayThread(() =>
            {
                FormVision_NewPanel formVision_NewPanel = new FormVision_NewPanel(LayerCount);
                if (formVision_NewPanel.ShowDialog() == DialogResult.OK)
                {
                    if (bitmap == null)
                    {
                        using (Bitmap placeholder = new Bitmap(10, 10))
                        {
                            CreateLayerDisplay(placeholder, formVision_NewPanel.PanelName, true);
                        }
                    }
                    else
                    {
                        CreateLayerDisplay(bitmap, formVision_NewPanel.PanelName, true);
                    }
                }
            });
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
            InvokeOnDisplayThread(() =>
            {
                LayerStore.RemoveEmpty();
            });
        }

        public static void CreateLayerDisplay(Mat ImageSource, string strTitle, bool bUseClose = true, bool activate = true)
        {
            using (Bitmap image = CImageConverter.ToBitmap(ImageSource))
            {
                CreateLayerDisplay(image, strTitle, bUseClose, null, activate);
            }
        }

        public static void CreateLayerDisplay(Bitmap ImageSource, string strTitle, bool bUseClose = true, IEnumerable<DetectionOverlayItem> detectionOverlays = null, bool activate = true)
        {
            ClearEmptyDisplay();

            InvokeOnDisplayThread(() =>
            {
                string previousSelectedTitle = SelecteItem;
                FormLayerDisplay previousDisplay = LayerStore.GetByTitleOrFirst(previousSelectedTitle);
                int existingIndex = LayerStore.FindIndex(strTitle);

                if (existingIndex < 0)
                {
                    FormLayerDisplay display = LayerStore.Create(ImageSource, bUseClose, strTitle);
                    display.SetDetectionOverlays(detectionOverlays);
                    DisplayHost.ShowDisplay(display);
                    display.ZoomToFit();
                    if (activate)
                    {
                        DisplayHost.ActivateDisplay(display);
                        FocusItem = display.Text;
                        SelecteItem = display.Text;
                    }
                    else if (previousDisplay != null)
                    {
                        DisplayHost.ActivateDisplay(previousDisplay);
                        FocusItem = previousDisplay.Text;
                        SelecteItem = previousDisplay.Text;
                    }
                    else
                    {
                        SelecteItem = previousSelectedTitle;
                    }
                    return;
                }

                FormLayerDisplay existingDisplay = LayerStore.GetOrNull(existingIndex);
                if (existingDisplay == null)
                {
                    return;
                }

                if (string.Equals(strTitle, "Main", StringComparison.OrdinalIgnoreCase))
                {
                    existingDisplay.ResetAnnotations();
                }

                existingDisplay.SetImage(ImageSource);
                existingDisplay.SetDetectionOverlays(detectionOverlays);

                if (activate && DisplayHost.ActiveDocumentTitle != strTitle)
                {
                    DisplayHost.ActivateDisplay(existingDisplay);
                    FocusItem = existingDisplay.Text;
                    SelecteItem = existingDisplay.Text;
                }
                else if (!activate && previousDisplay != null && DisplayHost.ActiveDocumentTitle != previousDisplay.Text)
                {
                    DisplayHost.ActivateDisplay(previousDisplay);
                    FocusItem = previousDisplay.Text;
                    SelecteItem = previousDisplay.Text;
                }
            });
        }

        public static void ZoomToFit(string strTitle)
        {
            FormLayerDisplay display = GetLayerDisplayOrNull(strTitle);
            display?.ZoomToFit();
        }

        public static bool SetDetectionOverlays(string title, IEnumerable<DetectionOverlayItem> detectionOverlays)
        {
            bool updated = false;
            InvokeOnDisplayThread(() =>
            {
                int index = LayerStore.FindIndex(title);
                FormLayerDisplay display = LayerStore.GetOrNull(index);
                if (display == null)
                {
                    return;
                }

                display.SetDetectionOverlays(detectionOverlays);
                updated = true;
            });

            return updated;
        }

        public static void NotifyAnnotationSelectionChanged(FormLayerDisplay display, LabelingAnnotationSelectionChangedEventArgs selection)
        {
            AnnotationSelectionChanged?.Invoke(
                null,
                new DisplayAnnotationSelectionChangedEventArgs(display, selection));
        }

        public static void ActivateLayer(string title)
        {
            InvokeOnDisplayThread(() =>
            {
                FormLayerDisplay display = GetLayerDisplayOrNull(title);
                if (display == null)
                {
                    return;
                }

                DisplayHost.ActivateDisplay(display);
                FocusItem = display.Text;
                SelecteItem = display.Text;
            });
        }

        public static void ActivateLayer(int index)
        {
            InvokeOnDisplayThread(() =>
            {
                FormLayerDisplay display = GetLayerDisplayOrNull(index);
                if (display == null)
                {
                    return;
                }

                DisplayHost.ActivateDisplay(display);
                FocusItem = display.Text;
                SelecteItem = display.Text;
            });
        }

        public static void RefreshLayer(int index)
        {
            InvokeOnDisplayThread(() => LayerStore.GetOrNull(index)?.RefreshViewer());
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
            FormLayerDisplay display = GetLayerDisplayOrNull(title);
            display?.AcceptImageChanged();
        }

        public static void InvokeOnDisplayThread(Action action)
        {
            DisplayHost.InvokeOnUiThread(action);
        }

        public static TResult InvokeOnDisplayThread<TResult>(Func<TResult> action)
        {
            return DisplayHost.InvokeOnUiThread(action);
        }
    }

    public sealed class DisplayAnnotationSelectionChangedEventArgs : EventArgs
    {
        public DisplayAnnotationSelectionChangedEventArgs(FormLayerDisplay display, LabelingAnnotationSelectionChangedEventArgs selection)
        {
            Display = display;
            Selection = selection;
        }

        public FormLayerDisplay Display { get; }

        public LabelingAnnotationSelectionChangedEventArgs Selection { get; }
    }
}
