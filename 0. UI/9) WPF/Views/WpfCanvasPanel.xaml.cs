using OpenVisionLab;
using OpenVisionLab.ImageCanvas.Views;
using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using ListBox = System.Windows.Controls.ListBox;
using UserControl = System.Windows.Controls.UserControl;
using WpfUiButton = Wpf.Ui.Controls.Button;

namespace MvcVisionSystem
{
    public partial class WpfCanvasPanel : UserControl
    {
        public WpfCanvasPanel()
        {
            InitializeComponent();
            LocalizationTextRuntimeService.RegisterRoot(this);
            LocalizationTextRuntimeService.RegisterRoot(MainCanvasView);
            Loaded += WpfCanvasPanel_Loaded;
            DetectionOverlayTitleText.SetBinding(
                TextBlock.TextProperty,
                new System.Windows.Data.Binding(nameof(WpfCanvasPanelViewModel.DetectionOverlayTitleText)));
        }

        public WpfCanvasPanelViewModel ViewModel => DataContext as WpfCanvasPanelViewModel;

        public RoiImageCanvasView MainCanvas => MainCanvasView;
        public ListBox AnnotationToolList => CanvasAnnotationToolListBox;
        public ListBox LabelClassList => CanvasLabelClassListBox;
        public ListBox DisplayModeList => CanvasDisplayModeListBox;
        public Border WorkflowContextStrip => CanvasWorkflowContextStrip;
        public TextBlock CurrentStepText => CanvasCurrentStepText;
        public TextBlock CurrentToolText => CanvasCurrentToolText;
        public TextBlock NextActionText => CanvasNextActionText;
        public Border LayerVisibilityStrip => CanvasLayerVisibilityStrip;
        public TextBlock LayerModeTitleText => CanvasLayerModeTitleText;
        public TextBlock LayerModeDetailText => CanvasLayerModeDetailText;
        public TextBlock LabelLayerText => CanvasLabelLayerText;
        public TextBlock InferenceLayerText => CanvasInferenceLayerText;
        public Border AnnotationSaveStateCard => CanvasAnnotationSaveStateCard;
        public TextBlock AnnotationSaveStatusTitleTextBlock => CanvasAnnotationSaveStatusTitleText;
        public TextBlock AnnotationSaveStatusDetailTextBlock => CanvasAnnotationSaveStatusDetailText;
        public Border ActiveLabelClassCard => CanvasActiveLabelClassCard;
        public TextBlock ActiveLabelClassTitleTextBlock => CanvasActiveLabelClassTitleText;
        public TextBlock ActiveLabelClassDetailTextBlock => CanvasActiveLabelClassDetailText;
        public WpfUiButton OpenClassCatalogButton => CanvasOpenClassCatalogButton;
        public WpfUiButton ShortcutHelpButton => CanvasShortcutHelpButton;
        public Border ShortcutHelpCard => CanvasShortcutHelpCard;
        public TextBlock ShortcutHelpTextBlock => CanvasShortcutHelpText;
        public Border ResultOverlay => DetectionResultOverlay;
        public TextBlock OverlayTitleText => DetectionOverlayTitleText;
        public TextBlock OverlaySummaryText => DetectionOverlaySummaryText;
        public Border OverlaySelectedBorder => DetectionOverlaySelectedBorder;
        public TextBlock OverlaySelectedText => DetectionOverlaySelectedText;
        public TextBlock OverlayDetailText => DetectionOverlayDetailText;
        public WpfUiButton SaveAnnotationButton => CanvasSaveAnnotationButton;
        public WpfUiButton CreateSmartMaskButton => CanvasCreateSmartMaskButton;
        public WpfUiButton CompleteNoObjectButton => CanvasCompleteNoObjectButton;
        public WpfUiButton FitButton => FitCanvasButton;
        public WpfUiButton ActualSizeButton => ActualSizeCanvasButton;
        public WpfUiButton PanButton => PanCanvasButton;
        public WpfUiButton DisplayAdjustmentButton => DisplayAdjustmentCanvasButton;
        public Popup DisplayAdjustmentFlyout => DisplayAdjustmentPopup;
        public WpfUiButton FocusCandidateButton => FocusCandidateCanvasButton;
        public WpfUiButton ResetAiOverlayButton => ResetAiOverlayCanvasButton;

        public void RefreshLocalizedViewerStatus()
        {
            foreach (Label label in EnumerateVisualChildren<Label>(MainCanvasView))
            {
                string key = GetViewerStatusFormatKey(label.ContentStringFormat);
                if (!string.IsNullOrWhiteSpace(key))
                {
                    label.SetCurrentValue(
                        ContentControl.ContentStringFormatProperty,
                        OpenVisionLanguageService.T(key));
                }
            }
        }

        private void WpfCanvasPanel_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            RefreshLocalizedViewerStatus();
        }

        private static string GetViewerStatusFormatKey(string format)
        {
            if (string.IsNullOrWhiteSpace(format))
            {
                return string.Empty;
            }

            if (format.IndexOf("좌표(좌하)", StringComparison.Ordinal) >= 0
                || format.IndexOf("Coordinates (bottom-left)", StringComparison.Ordinal) >= 0)
            {
                return "WpfCanvas.ViewerStatus.RobotPosition";
            }

            if (format.IndexOf("이미지좌표(좌상)", StringComparison.Ordinal) >= 0
                || format.IndexOf("Image coordinates (top-left)", StringComparison.Ordinal) >= 0)
            {
                return "WpfCanvas.ViewerStatus.ImagePosition";
            }

            if (format.IndexOf("색상", StringComparison.Ordinal) >= 0
                || format.IndexOf("Color:", StringComparison.Ordinal) >= 0)
            {
                return "WpfCanvas.ViewerStatus.PixelColor";
            }

            return string.Empty;
        }

        private static IEnumerable<T> EnumerateVisualChildren<T>(System.Windows.DependencyObject root)
            where T : System.Windows.DependencyObject
        {
            if (root == null)
            {
                yield break;
            }

            int childCount = VisualTreeHelper.GetChildrenCount(root);
            for (int index = 0; index < childCount; index++)
            {
                System.Windows.DependencyObject child = VisualTreeHelper.GetChild(root, index);
                if (child is T typedChild)
                {
                    yield return typedChild;
                }

                foreach (T descendant in EnumerateVisualChildren<T>(child))
                {
                    yield return descendant;
                }
            }
        }
    }
}
