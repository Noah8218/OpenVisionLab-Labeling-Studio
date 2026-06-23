using OpenVisionLab.ImageCanvas.Views;
using System.Windows.Controls;
using WpfUiButton = Wpf.Ui.Controls.Button;

namespace MvcVisionSystem
{
    public partial class WpfCanvasPanel : UserControl
    {
        public WpfCanvasPanel()
        {
            InitializeComponent();
        }

        public WpfCanvasPanelViewModel ViewModel => DataContext as WpfCanvasPanelViewModel;

        public RoiImageCanvasView MainCanvas => MainCanvasView;
        public ListBox AnnotationToolList => CanvasAnnotationToolListBox;
        public Border WorkflowContextStrip => CanvasWorkflowContextStrip;
        public TextBlock CurrentStepText => CanvasCurrentStepText;
        public TextBlock CurrentToolText => CanvasCurrentToolText;
        public TextBlock NextActionText => CanvasNextActionText;
        public Border ResultOverlay => DetectionResultOverlay;
        public TextBlock OverlayTitleText => DetectionOverlayTitleText;
        public TextBlock OverlaySummaryText => DetectionOverlaySummaryText;
        public Border OverlaySelectedBorder => DetectionOverlaySelectedBorder;
        public TextBlock OverlaySelectedText => DetectionOverlaySelectedText;
        public TextBlock OverlayDetailText => DetectionOverlayDetailText;
        public WpfUiButton FitButton => FitCanvasButton;
        public WpfUiButton ActualSizeButton => ActualSizeCanvasButton;
        public WpfUiButton PanButton => PanCanvasButton;
        public WpfUiButton FocusCandidateButton => FocusCandidateCanvasButton;
        public WpfUiButton ResetAiOverlayButton => ResetAiOverlayCanvasButton;
    }
}
