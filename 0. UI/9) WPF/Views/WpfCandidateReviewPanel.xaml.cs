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
        public Border ModelComparisonReview => ModelComparisonReviewPanel;
        public TextBlock ModelComparisonStatus => ModelComparisonStatusText;
        public TextBlock ModelComparisonDetail => ModelComparisonDetailText;
        public TextBlock ModelComparisonAction => ModelComparisonActionText;
        public ScrollViewer ModelComparisonExampleScroller => ModelComparisonExampleScrollViewer;
        public ItemsControl ModelComparisonExampleList => ModelComparisonExampleItems;
        public Border SelectedCandidateSummary => SelectedCandidateSummaryPanel;
        public TextBlock SelectedCandidateSummaryTextBlock => SelectedCandidateSummaryText;
        public Border ComparisonPanel => CandidateComparisonPanel;
        public TextBlock CompareCandidateText => CandidateCompareCandidateText;
        public TextBlock CompareCurrentText => CandidateCompareCurrentText;
        public TextBlock CompareOverlapText => CandidateCompareOverlapText;
        public TextBlock CompareDecisionText => CandidateCompareDecisionText;
        public Border ReviewHistoryPanel => CandidateReviewHistoryPanel;
        public ItemsControl ReviewHistoryItems => CandidateReviewHistoryItems;
        public WpfUiButton PreviousCandidate => PreviousCandidateButton;
        public WpfUiButton NextCandidate => NextCandidateButton;
        public WpfUiButton FocusCandidate => FocusCandidateButton;
        public WpfUiButton FocusCurrentLabel => FocusCurrentLabelButton;
        public WpfUiButton ConfirmSelectedButton => ConfirmSelectedCandidateButton;
        public WpfUiButton ConfirmAllButton => ConfirmAllCandidatesButton;
        public WpfUiButton SkipSelectedButton => SkipSelectedCandidateButton;
        public WpfUiButton CompleteImageAndNext => CompleteImageAndNextButton;
        public ListBox CandidateList => CandidateListBox;
    }
}
