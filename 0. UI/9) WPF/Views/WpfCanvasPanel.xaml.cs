using OpenVisionLab.ImageCanvas.Views;
using System.Windows;
using System.Windows.Controls;
using WpfUiButton = Wpf.Ui.Controls.Button;

namespace MvcVisionSystem
{
    public partial class WpfCanvasPanel : UserControl
    {
        public WpfCanvasPanel()
        {
            InitializeComponent();
            DataContext = ViewModel;
        }

        public WpfCanvasPanelViewModel ViewModel { get; } = new WpfCanvasPanelViewModel();

        public event RoutedEventHandler FitRequested;
        public event RoutedEventHandler ActualSizeRequested;
        public event RoutedEventHandler PanRequested;
        public event RoutedEventHandler FocusCandidateRequested;
        public event RoutedEventHandler ResetAiOverlayRequested;

        public RoiImageCanvasView MainCanvas => MainCanvasView;
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

        private void FitCanvasButton_Click(object sender, RoutedEventArgs e) => FitRequested?.Invoke(sender, e);
        private void ActualSizeCanvasButton_Click(object sender, RoutedEventArgs e) => ActualSizeRequested?.Invoke(sender, e);
        private void PanCanvasButton_Click(object sender, RoutedEventArgs e) => PanRequested?.Invoke(sender, e);
        private void FocusCandidateCanvasButton_Click(object sender, RoutedEventArgs e) => FocusCandidateRequested?.Invoke(sender, e);
        private void ResetAiOverlayCanvasButton_Click(object sender, RoutedEventArgs e) => ResetAiOverlayRequested?.Invoke(sender, e);
    }
}
