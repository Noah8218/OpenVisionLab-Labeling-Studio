using System.Windows.Controls;
using ListBox = System.Windows.Controls.ListBox;
using UserControl = System.Windows.Controls.UserControl;
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
        public Grid RoleSplitPanel => CandidateReviewRoleSplitPanel;
        public Border CurrentImageRoleCard => CurrentImageCandidateRoleCard;
        public Border ModelValidationCard => ModelValidationRoleCard;
        public TextBlock CurrentImageRoleTitle => CurrentImageReviewRoleTitleText;
        public TextBlock CurrentImageRoleDetail => CurrentImageReviewRoleDetailText;
        public TextBlock CurrentImageRoleResult => CurrentImageReviewRoleResultText;
        public TextBlock ModelValidationRoleTitle => ModelValidationRoleTitleText;
        public TextBlock ModelValidationRoleDetail => ModelValidationRoleDetailText;
        public TextBlock ModelValidationRoleResult => ModelValidationRoleResultText;
        public TextBlock ConfidenceTextBlock => CandidateConfidenceText;
        public TextBlock DetailTextBlock => CandidateDetailText;
        public Border ModelComparisonReview => ModelComparisonReviewPanel;
        public TextBlock ModelComparisonSource => ModelComparisonSourceText;
        public TextBlock ModelComparisonStatus => ModelComparisonStatusText;
        public TextBlock ModelComparisonDetail => ModelComparisonDetailText;
        public TextBlock ModelComparisonAction => ModelComparisonActionText;
        public Border ModelCandidateDecision => ModelCandidateDecisionPanel;
        public TextBlock ModelCandidateDecisionStatus => ModelCandidateDecisionStatusText;
        public TextBlock ModelCandidateDecisionDetail => ModelCandidateDecisionDetailText;
        public WpfUiButton SaveModelCandidate => SaveModelCandidateButton;
        public WpfUiButton RejectModelCandidate => RejectModelCandidateButton;
        public Expander ModelComparisonExampleDisclosure => ModelComparisonExampleExpander;
        public TextBlock ModelComparisonExampleSummary => ModelComparisonExampleSummaryText;
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
        public Expander ReviewHistoryDisclosure => CandidateReviewHistoryExpander;
        public TextBlock ReviewHistorySummary => CandidateReviewHistorySummaryText;
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
