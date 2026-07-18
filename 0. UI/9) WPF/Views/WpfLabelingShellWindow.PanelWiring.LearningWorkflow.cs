using OpenVisionLab.Mvvm.Behaviors;
using System.Windows;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Learning workflow panel wiring is isolated so guide-step commands do not mix with other panel registrations.
        private void ConfigureLearningWorkflowPanelCommands()
        {
            LearningWorkflowViewModel.ConfigureCommands(
                selected => DatasetPurposeListBox_SelectionChanged(DatasetPurposeListBox, selected),
                selected => ExecuteStartDatasetSetupCommand(selected),
                selected => LearningWorkflowModeListBox_SelectionChanged(LearningModeListBox, selected),
                selected => AnnotationToolListBox_SelectionChanged(AnnotationToolListBox, selected),
                selected => LearningStepListBox_SelectionChanged(LearningStepListBox, selected),
                step => ExecuteYoloTrainingWorkflowStep(step?.Order ?? 0, LearningWorkflowPanelControl),
                ExecuteOpenTutorialHtmlGuideCommand,
                ExecuteFixYoloClassesCommand,
                ExecuteFixYoloLabelsCommand,
                ExecuteFixYoloDatasetCommand,
                ExecuteDatasetDashboardMetricCommand,
                ExecuteRunModelComparisonCommand,
                ExecuteChangeDatasetCommand,
                ExecuteFirstRunSamplePathCommand,
                TemplateMatchingAutoLabelViewModel.RunCurrentImage,
                TemplateMatchingAutoLabelViewModel.RunBatch,
                ExecuteExternalEvaluationDataAuditCommand,
                ExecuteSelectExternalYoloDatasetCommand,
                ExecuteActivateExternalYoloDatasetCommand,
                ExecuteClearExternalYoloDatasetCommand);
            RefreshAttachedCommandBindings(DatasetPurposeListBox, InputCommandBehaviors.SelectedItemChangedCommandProperty);
            RefreshAttachedCommandBindings(LearningModeListBox, InputCommandBehaviors.SelectedItemChangedCommandProperty);
            RefreshAttachedCommandBindings(AnnotationToolListBox, InputCommandBehaviors.SelectedItemChangedCommandProperty);
            RefreshAttachedCommandBindings(LearningStepListBox, InputCommandBehaviors.SelectedItemChangedCommandProperty);
        }

        private void RegisterLearningWorkflowPanelNames()
        {
            ConfigureLearningWorkflowPanelCommands();
            RegisterLearningWorkflowName(nameof(DatasetPurposeListBox), DatasetPurposeListBox);
            RegisterLearningWorkflowName(nameof(DatasetPurposeSummaryText), DatasetPurposeSummaryText);
            RegisterLearningWorkflowName(nameof(DatasetPurposeToolSummaryText), DatasetPurposeToolSummaryText);
            RegisterLearningWorkflowName(nameof(FirstRunSamplePathPanel), FirstRunSamplePathPanel);
            RegisterLearningWorkflowName(nameof(FirstRunSamplePathTitleText), FirstRunSamplePathTitleText);
            RegisterLearningWorkflowName(nameof(FirstRunSamplePathSummaryText), FirstRunSamplePathSummaryText);
            RegisterLearningWorkflowName(nameof(FirstRunSamplePathPrimaryActionText), FirstRunSamplePathPrimaryActionText);
            RegisterLearningWorkflowName(nameof(FirstRunSamplePathItemsControl), FirstRunSamplePathItemsControl);
            RegisterLearningWorkflowName(nameof(DatasetSetupStartButton), DatasetSetupStartButton);
            RegisterLearningWorkflowName(nameof(DatasetOpenExistingButton), DatasetOpenExistingButton);
            RegisterLearningWorkflowName(nameof(DatasetSetupStatusText), DatasetSetupStatusText);
            RegisterLearningWorkflowName(nameof(CurrentWorkflowActionText), CurrentWorkflowActionText);
            RegisterLearningWorkflowName(nameof(LearningModeListBox), LearningModeListBox);
            RegisterLearningWorkflowName(nameof(AnnotationToolListBox), AnnotationToolListBox);
            RegisterLearningWorkflowName(nameof(LearningStepListBox), LearningStepListBox);
            RegisterLearningWorkflowName(nameof(LearningConceptsExpander), LearningConceptsExpander);
            RegisterLearningWorkflowName(nameof(GroundTruthChipText), GroundTruthChipText);
            RegisterLearningWorkflowName(nameof(PredictionChipText), PredictionChipText);
            RegisterLearningWorkflowName(nameof(YoloTrainingWorkflowItemsControl), YoloTrainingWorkflowItemsControl);
            RegisterLearningWorkflowName(nameof(YoloCurrentTrainingProgressItemsControl), YoloCurrentTrainingProgressItemsControl);
            RegisterLearningWorkflowName(nameof(YoloTrainingWorkflowSummaryText), YoloTrainingWorkflowSummaryText);
            RegisterLearningWorkflowName(nameof(YoloTrainingChecklistStatusText), YoloTrainingChecklistStatusText);
            RegisterLearningWorkflowName(nameof(YoloTrainingChecklistDetailText), YoloTrainingChecklistDetailText);
            RegisterLearningWorkflowName(nameof(YoloTrainingChecklistActionText), YoloTrainingChecklistActionText);
            RegisterLearningWorkflowName(nameof(DatasetDashboardStatusText), DatasetDashboardStatusText);
            RegisterLearningWorkflowName(nameof(DatasetDashboardSummaryText), DatasetDashboardSummaryText);
            RegisterLearningWorkflowName(nameof(DatasetDashboardActionText), DatasetDashboardActionText);
            RegisterLearningWorkflowName(nameof(DatasetDashboardMetricItemsControl), DatasetDashboardMetricItemsControl);
            RegisterLearningWorkflowName(nameof(DatasetDashboardIssueItemsControl), DatasetDashboardIssueItemsControl);
            RegisterLearningWorkflowName(nameof(YoloTrainingHistoryText), YoloTrainingHistoryText);
            RegisterLearningWorkflowName(nameof(YoloTrainingRunHistoryItemsControl), YoloTrainingRunHistoryItemsControl);
            RegisterLearningWorkflowName(nameof(YoloRunModelComparisonButton), YoloRunModelComparisonButton);
            RegisterLearningWorkflowName(nameof(TemplateWorkflowPanel), TemplateWorkflowPanel);
            RegisterLearningWorkflowName(nameof(TemplateWorkflowTitleText), TemplateWorkflowTitleText);
            RegisterLearningWorkflowName(nameof(TemplateWorkflowSummaryText), TemplateWorkflowSummaryText);
            RegisterLearningWorkflowName(nameof(TemplateWorkflowItemsControl), TemplateWorkflowItemsControl);
            RegisterLearningWorkflowName(nameof(TemplateCurrentImageGuideButton), TemplateCurrentImageGuideButton);
            RegisterLearningWorkflowName(nameof(TemplateBatchGuideButton), TemplateBatchGuideButton);
            RegisterLearningWorkflowName(nameof(TutorialOpenHtmlGuideButton), TutorialOpenHtmlGuideButton);
            RegisterLearningWorkflowName(nameof(YoloFixClassesButton), YoloFixClassesButton);
            RegisterLearningWorkflowName(nameof(YoloFixLabelsButton), YoloFixLabelsButton);
            RegisterLearningWorkflowName(nameof(YoloFixDatasetButton), YoloFixDatasetButton);
            RegisterLearningWorkflowName(nameof(YoloExternalEvaluationAuditButton), YoloExternalEvaluationAuditButton);
            RegisterLearningWorkflowName(nameof(YoloExternalEvaluationAuditStatusText), YoloExternalEvaluationAuditStatusText);
            RegisterLearningWorkflowName(nameof(YoloExternalEvaluationAuditDetailText), YoloExternalEvaluationAuditDetailText);
            RegisterLearningWorkflowName(nameof(YoloExternalYoloDatasetSelectButton), YoloExternalYoloDatasetSelectButton);
            RegisterLearningWorkflowName(nameof(YoloExternalYoloDatasetActivateButton), YoloExternalYoloDatasetActivateButton);
            RegisterLearningWorkflowName(nameof(YoloExternalYoloDatasetClearButton), YoloExternalYoloDatasetClearButton);
            RegisterLearningWorkflowName(nameof(YoloExternalYoloDatasetStatusText), YoloExternalYoloDatasetStatusText);
            RegisterLearningWorkflowName(nameof(YoloExternalYoloDatasetDetailText), YoloExternalYoloDatasetDetailText);
        }

        private void RegisterLearningWorkflowName(string name, FrameworkElement element)
        {
            if (!string.IsNullOrWhiteSpace(name) && element != null)
            {
                RegisterName(name, element);
            }
        }

    }
}
