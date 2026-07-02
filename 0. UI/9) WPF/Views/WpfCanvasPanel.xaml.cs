using OpenVisionLab.ImageCanvas.Views;
using System.Windows.Controls;
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
        public Border ResultOverlay => DetectionResultOverlay;
        public TextBlock OverlayTitleText => DetectionOverlayTitleText;
        public TextBlock OverlaySummaryText => DetectionOverlaySummaryText;
        public Border OverlaySelectedBorder => DetectionOverlaySelectedBorder;
        public TextBlock OverlaySelectedText => DetectionOverlaySelectedText;
        public TextBlock OverlayDetailText => DetectionOverlayDetailText;
        public WpfUiButton SaveAnnotationButton => CanvasSaveAnnotationButton;
        public WpfUiButton CompleteNoObjectButton => CanvasCompleteNoObjectButton;
        public WpfUiButton FitButton => FitCanvasButton;
        public WpfUiButton ActualSizeButton => ActualSizeCanvasButton;
        public WpfUiButton PanButton => PanCanvasButton;
        public WpfUiButton FocusCandidateButton => FocusCandidateCanvasButton;
        public WpfUiButton ResetAiOverlayButton => ResetAiOverlayCanvasButton;
    }
}
