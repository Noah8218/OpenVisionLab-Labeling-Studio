using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfUiButton = Wpf.Ui.Controls.Button;

namespace MvcVisionSystem
{
    public partial class WpfCandidateReviewPanel : UserControl
    {
        public WpfCandidateReviewPanel()
        {
            InitializeComponent();
            DataContext = ViewModel;
        }

        public WpfCandidateReviewPanelViewModel ViewModel { get; } = new WpfCandidateReviewPanelViewModel();

        public event RoutedPropertyChangedEventHandler<double> ConfidenceChanged;
        public event RoutedEventHandler ConfirmSelectedRequested;
        public event RoutedEventHandler ConfirmAllRequested;
        public event RoutedEventHandler SkipSelectedRequested;
        public event RoutedEventHandler PreviousCandidateRequested;
        public event RoutedEventHandler NextCandidateRequested;
        public event RoutedEventHandler FocusCandidateRequested;
        public event SelectionChangedEventHandler CandidateSelectionChanged;
        public event KeyEventHandler CandidatePreviewKeyDown;

        public Slider ConfidenceSlider => CandidateConfidenceSlider;
        public TextBlock ConfidenceTextBlock => CandidateConfidenceText;
        public TextBlock DetailTextBlock => CandidateDetailText;
        public Border SelectedCandidateSummary => SelectedCandidateSummaryPanel;
        public TextBlock SelectedCandidateSummaryTextBlock => SelectedCandidateSummaryText;
        public Border ComparisonPanel => CandidateComparisonPanel;
        public TextBlock CompareCandidateText => CandidateCompareCandidateText;
        public TextBlock CompareCurrentText => CandidateCompareCurrentText;
        public TextBlock CompareOverlapText => CandidateCompareOverlapText;
        public Border ReviewHistoryPanel => CandidateReviewHistoryPanel;
        public ItemsControl ReviewHistoryItems => CandidateReviewHistoryItems;
        public WpfUiButton PreviousCandidate => PreviousCandidateButton;
        public WpfUiButton NextCandidate => NextCandidateButton;
        public WpfUiButton FocusCandidate => FocusCandidateButton;
        public WpfUiButton ConfirmSelectedButton => ConfirmSelectedCandidateButton;
        public WpfUiButton ConfirmAllButton => ConfirmAllCandidatesButton;
        public WpfUiButton SkipSelectedButton => SkipSelectedCandidateButton;
        public ListBox CandidateList => CandidateListBox;

        private void CandidateConfidenceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => ConfidenceChanged?.Invoke(sender, e);
        private void ConfirmSelectedCandidateButton_Click(object sender, RoutedEventArgs e) => ConfirmSelectedRequested?.Invoke(sender, e);
        private void ConfirmAllCandidatesButton_Click(object sender, RoutedEventArgs e) => ConfirmAllRequested?.Invoke(sender, e);
        private void SkipSelectedCandidateButton_Click(object sender, RoutedEventArgs e) => SkipSelectedRequested?.Invoke(sender, e);
        private void PreviousCandidateButton_Click(object sender, RoutedEventArgs e) => PreviousCandidateRequested?.Invoke(sender, e);
        private void NextCandidateButton_Click(object sender, RoutedEventArgs e) => NextCandidateRequested?.Invoke(sender, e);
        private void FocusCandidateButton_Click(object sender, RoutedEventArgs e) => FocusCandidateRequested?.Invoke(sender, e);
        private void CandidateListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => CandidateSelectionChanged?.Invoke(sender, e);
        private void CandidateListBox_PreviewKeyDown(object sender, KeyEventArgs e) => CandidatePreviewKeyDown?.Invoke(sender, e);
    }
}
