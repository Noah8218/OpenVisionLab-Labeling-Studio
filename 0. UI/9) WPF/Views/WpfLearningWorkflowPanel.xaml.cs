using System;
using System.Windows.Controls;
using System.Windows.Threading;

namespace MvcVisionSystem
{
    public partial class WpfLearningWorkflowPanel : UserControl
    {
        public WpfLearningWorkflowPanel()
        {
            InitializeComponent();
        }

        public WpfLearningWorkflowPanelViewModel ViewModel => DataContext as WpfLearningWorkflowPanelViewModel;

        public ListBox DatasetPurposeList => DatasetPurposeListBox;

        public TextBlock DatasetPurposeSummary => DatasetPurposeSummaryText;

        public TextBlock DatasetPurposeToolSummary => DatasetPurposeToolSummaryText;

        public TextBlock DatasetSetupFirstAction => DatasetSetupFirstActionText;

        public Button DatasetSetupStart => DatasetSetupStartButton;

        public TextBlock DatasetSetupStatus => DatasetSetupStatusText;

        public TextBlock CurrentWorkflowAction => CurrentWorkflowActionText;

        public ListBox ModeList => LearningModeListBox;

        public ListBox ToolList => AnnotationToolListBox;

        public ListBox StepList => LearningStepListBox;

        public ScrollViewer WorkflowScrollViewer => LearningWorkflowScrollViewer;

        public Expander LearningConcepts => LearningConceptsExpander;

        public TextBlock GroundTruthTextBlock => GroundTruthChipText;

        public TextBlock PredictionTextBlock => PredictionChipText;

        public ItemsControl YoloTrainingWorkflowList => YoloTrainingWorkflowItemsControl;

        public ItemsControl YoloCurrentTrainingProgressList => YoloCurrentTrainingProgressItemsControl;

        public TextBlock YoloTrainingWorkflowSummary => YoloTrainingWorkflowSummaryText;

        public TextBlock YoloTrainingChecklistStatus => YoloTrainingChecklistStatusText;

        public TextBlock YoloTrainingChecklistDetail => YoloTrainingChecklistDetailText;

        public TextBlock YoloTrainingChecklistAction => YoloTrainingChecklistActionText;

        public TextBlock DatasetDashboardStatus => DatasetDashboardStatusText;

        public TextBlock DatasetDashboardSummary => DatasetDashboardSummaryText;

        public TextBlock DatasetDashboardAction => DatasetDashboardActionText;

        public TextBlock ModelReplacementStatus => ModelReplacementStatusText;

        public TextBlock ModelReplacementDetail => ModelReplacementDetailText;

        public ItemsControl DatasetDashboardMetricList => DatasetDashboardMetricItemsControl;

        public ItemsControl DatasetDashboardIssueList => DatasetDashboardIssueItemsControl;

        public TextBlock YoloTrainingHistory => YoloTrainingHistoryText;

        public TextBlock YoloTrainingResultComparison => YoloTrainingResultComparisonText;

        public TextBlock YoloTrainingModelAdoptionDecision => YoloTrainingModelAdoptionDecisionText;

        public ItemsControl YoloTrainingResultReportList => YoloTrainingResultReportItemsControl;

        public TextBlock YoloModelComparisonBasis => YoloModelComparisonBasisText;

        public Button YoloRunModelComparison => YoloRunModelComparisonButton;

        public ItemsControl YoloTrainingRunHistoryList => YoloTrainingRunHistoryItemsControl;

        public Button YoloFixClasses => YoloFixClassesButton;

        public Button YoloFixLabels => YoloFixLabelsButton;

        public Button YoloFixDataset => YoloFixDatasetButton;

        public Button TutorialOpenHtmlGuide => TutorialOpenHtmlGuideButton;

        public void ShowAnnotationToolPalette()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // Labeling tools are the operator's primary controls, so keep them
                // reachable without expanding the secondary learning concepts section.
                LearningWorkflowScrollViewer.ScrollToTop();
                AnnotationToolListBox.UpdateLayout();
                if (AnnotationToolListBox.SelectedItem != null)
                {
                    AnnotationToolListBox.ScrollIntoView(AnnotationToolListBox.SelectedItem);
                }

                AnnotationToolHeaderText.BringIntoView();
            }), DispatcherPriority.Background);
        }
    }
}
