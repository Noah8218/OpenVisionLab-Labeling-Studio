using System;
using System.Windows.Controls;
using System.Windows.Threading;
using Button = System.Windows.Controls.Button;
using ListBox = System.Windows.Controls.ListBox;
using UserControl = System.Windows.Controls.UserControl;

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

        public Border GuideToolsRole => GuideToolsRolePanel;

        public TextBlock GuideToolsRoleTitle => GuideToolsRoleTitleText;

        public TextBlock GuideToolsPrimaryTask => GuideToolsPrimaryTaskText;

        public TextBlock GuideToolsHelperTask => GuideToolsHelperTaskText;

        public Border CurrentWorkflowStepPanelControl => CurrentWorkflowStepPanel;

        public Border FirstRunSamplePathPanelControl => FirstRunSamplePathPanel;

        public TextBlock FirstRunSamplePathTitle => FirstRunSamplePathTitleText;

        public TextBlock FirstRunSamplePathSummary => FirstRunSamplePathSummaryText;

        public TextBlock FirstRunSamplePathPrimaryAction => FirstRunSamplePathPrimaryActionText;

        public ItemsControl FirstRunSamplePathList => FirstRunSamplePathItemsControl;

        public TextBlock DatasetSetupFirstAction => DatasetSetupFirstActionText;

        public ItemsControl FirstRunChecklistList => FirstRunChecklistItemsControl;

        public Button DatasetSetupStart => DatasetSetupStartButton;

        public Button DatasetOpenExisting => DatasetOpenExistingButton;

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

        public TextBlock TrainingModelLifecycleCurrent => TrainingModelLifecycleCurrentText;

        public TextBlock TrainingModelLifecycleCandidate => TrainingModelLifecycleCandidateText;

        public TextBlock TrainingModelLifecycleDecision => TrainingModelLifecycleDecisionText;

        public TextBlock TrainingModelLifecycleNextAction => TrainingModelLifecycleNextActionText;

        public ItemsControl DatasetDashboardMetricList => DatasetDashboardMetricItemsControl;

        public ItemsControl DatasetDashboardIssueList => DatasetDashboardIssueItemsControl;

        public TextBlock YoloTrainingHistory => YoloTrainingHistoryText;

        public TextBlock YoloTrainingResultComparison => YoloTrainingResultComparisonText;

        public TextBlock YoloTrainingModelAdoptionDecision => YoloTrainingModelAdoptionDecisionText;

        public ItemsControl YoloTrainingResultReportList => YoloTrainingResultReportItemsControl;

        public TextBlock YoloModelComparisonBasis => YoloModelComparisonBasisText;

        public Button YoloRunModelComparison => YoloRunModelComparisonButton;

        public ItemsControl YoloTrainingRunHistoryList => YoloTrainingRunHistoryItemsControl;

        public Border TemplateWorkflow => TemplateWorkflowPanel;

        public TextBlock TemplateWorkflowTitle => TemplateWorkflowTitleText;

        public TextBlock TemplateWorkflowSummary => TemplateWorkflowSummaryText;

        public Border TemplateWorkflowRole => TemplateWorkflowRolePanel;

        public TextBlock TemplateWorkflowRoleDetail => TemplateWorkflowRoleText;

        public ItemsControl TemplateWorkflowStepList => TemplateWorkflowItemsControl;

        public Button TemplateCurrentImage => TemplateCurrentImageGuideButton;

        public Button TemplateBatch => TemplateBatchGuideButton;

        public Button YoloFixClasses => YoloFixClassesButton;

        public Button YoloFixLabels => YoloFixLabelsButton;

        public Button YoloFixDataset => YoloFixDatasetButton;

        public Button TutorialOpenHtmlGuide => TutorialOpenHtmlGuideButton;

        public void ShowAnnotationToolPalette()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ViewModel?.ShowLabelingTask();
                LearningWorkflowScrollViewer.ScrollToTop();
                CurrentWorkflowStepPanel.BringIntoView();
            }), DispatcherPriority.Background);
        }

        public void ShowDatasetSetupStart()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                ViewModel?.ShowDatasetOnboarding();
                LearningWorkflowScrollViewer.ScrollToTop();
                DatasetSetupActionPanel.BringIntoView();
                DatasetSetupStartButton.Focus();
            }), DispatcherPriority.Background);
        }
    }
}
