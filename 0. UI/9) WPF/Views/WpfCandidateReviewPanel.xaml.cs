using System.Windows.Controls;
using WpfUiButton = Wpf.Ui.Controls.Button;

namespace MvcVisionSystem
{
    public partial class WpfCandidateReviewPanel : UserControl
    {
        public WpfCandidateReviewPanel()
        {
            InitializeComponent();
        }

        public WpfCandidateReviewPanelViewModel ViewModel => DataContext as WpfCandidateReviewPanelViewModel;

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
    }
}