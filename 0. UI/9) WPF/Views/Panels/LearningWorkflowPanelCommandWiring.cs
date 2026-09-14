using OpenVisionLab;
using OpenVisionLab.Mvvm.Behaviors;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace MvcVisionSystem
{
    // Composition-only adapter for the Learning Workflow panel. Guide commands
    // remain owned by the existing ViewModel; dataset, audit, model-comparison,
    // and external-intake policy stays in their existing owners.
    internal sealed class LearningWorkflowPanelCommandWiring
    {
        private readonly LearningWorkflowPanelCommandWiringContext context;

        internal LearningWorkflowPanelCommandWiring(LearningWorkflowPanelCommandWiringContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        internal void ConfigureLearningWorkflowPanelCommands()
        {
            context.LearningWorkflowViewModel.ConfigureCommands(
                null,
                context.ExecuteStartDatasetSetupCommand,
                null,
                null,
                null,
                context.ExecuteYoloTrainingWorkflowStep,
                context.ExecuteOpenTutorialHtmlGuideCommand,
                context.ExecuteFixYoloClassesCommand,
                context.ExecuteFixYoloLabelsCommand,
                context.ExecuteFixYoloDatasetCommand,
                datasetDashboardMetricSelected: context.ExecuteDatasetDashboardMetricCommand,
                runModelComparison: null,
                datasetOpenExisting: context.ExecuteChangeDatasetCommand,
                firstRunSamplePathSelected: context.ExecuteFirstRunSamplePathCommand,
                runTemplateCurrentImage: context.ExecuteTemplateCurrentImageCommand,
                runTemplateBatch: context.ExecuteTemplateBatchCommand,
                runExternalEvaluationDataAudit: null,
                selectExternalYoloDataset: null,
                activateExternalYoloDataset: null,
                clearExternalYoloDataset: null);
            context.LearningWorkflowViewModel.ConfigureLearningModeSelectionWorkflow(context.ApplyLearningModeWorkflowAction);
            context.LearningWorkflowViewModel.ConfigureLearningStepSelectionWorkflow(context.ApplyLearningStepWorkflowAction);
            context.LearningWorkflowViewModel.ConfigureAnnotationToolSelectionWorkflow(context.ApplyAnnotationToolSelection);
            context.LearningWorkflowViewModel.ConfigureDatasetPurposeSelectionWorkflow(
                context.DatasetPurposeSelectionGuard,
                context.ApplyWorkflowDatasetPurposeSelection);
            context.LearningWorkflowViewModel.ConfigureExternalEvaluationDataAuditWorkflow(
                context.ExternalAuditWorkflowService,
                context.ExternalEvaluationDataAuditInitialDirectoryProvider,
                context.ExternalEvaluationDataAuditDirectorySelector,
                context.ExternalEvaluationDataAuditReferenceDirectoriesProvider,
                context.ExternalEvaluationDataAuditCloseApproval,
                context.ExternalEvaluationDataAuditStatusSink);
            context.LearningWorkflowViewModel.ConfigureHistoricalSegmentationRemediationAuditWorkflow(
                context.ExternalAuditWorkflowService,
                context.HistoricalSegmentationRemediationDataProvider,
                context.HistoricalSegmentationRemediationSourceImagePathProvider,
                context.HistoricalSegmentationRemediationCloseApproval,
                context.HistoricalSegmentationRemediationStatusSink);
            context.LearningWorkflowViewModel.ConfigureModelComparisonWorkflow(
                context.ModelComparisonWorkflowService,
                context.CreateModelComparisonCallbacks);
            context.LearningWorkflowViewModel.ConfigureExternalYoloDatasetIntakeWorkflow(
                context.ExternalYoloDatasetIntakeWorkflowService,
                context.CreateExternalYoloDatasetIntakeCallbacks);
            context.RefreshAttachedCommandBindings(
                context.DatasetPurposeListBox,
                new[] { InputCommandBehaviors.SelectedItemChangedCommandProperty });
            context.RefreshAttachedCommandBindings(
                context.LearningModeListBox,
                new[] { InputCommandBehaviors.SelectedItemChangedCommandProperty });
            context.RefreshAttachedCommandBindings(
                context.AnnotationToolListBox,
                new[] { InputCommandBehaviors.SelectedItemChangedCommandProperty });
            context.RefreshAttachedCommandBindings(
                context.LearningStepListBox,
                new[] { InputCommandBehaviors.SelectedItemChangedCommandProperty });
        }
    }

    internal sealed class LearningWorkflowPanelCommandWiringContext
    {
        internal WpfLearningWorkflowPanelViewModel LearningWorkflowViewModel { get; init; }
        internal Action<object> ExecuteStartDatasetSetupCommand { get; init; }
        internal Action<WpfYoloTrainingWorkflowStepItem> ExecuteYoloTrainingWorkflowStep { get; init; }
        internal Action ExecuteOpenTutorialHtmlGuideCommand { get; init; }
        internal Action ExecuteFixYoloClassesCommand { get; init; }
        internal Action ExecuteFixYoloLabelsCommand { get; init; }
        internal Action ExecuteFixYoloDatasetCommand { get; init; }
        internal Action<WpfDatasetDashboardMetricItem> ExecuteDatasetDashboardMetricCommand { get; init; }
        internal Action ExecuteChangeDatasetCommand { get; init; }
        internal Action<WpfFirstRunChecklistItem> ExecuteFirstRunSamplePathCommand { get; init; }
        internal Action ExecuteTemplateCurrentImageCommand { get; init; }
        internal Action ExecuteTemplateBatchCommand { get; init; }
        internal Action<WpfLearningModeWorkflowAction> ApplyLearningModeWorkflowAction { get; init; }
        internal Action<WpfLearningStepWorkflowAction> ApplyLearningStepWorkflowAction { get; init; }
        internal Action<WpfAnnotationToolItem> ApplyAnnotationToolSelection { get; init; }
        internal Func<WpfLearningModeItem, bool> DatasetPurposeSelectionGuard { get; init; }
        internal Action<LabelingDatasetPurpose> ApplyWorkflowDatasetPurposeSelection { get; init; }
        internal ExternalAuditWorkflowService ExternalAuditWorkflowService { get; init; }
        internal Func<string> ExternalEvaluationDataAuditInitialDirectoryProvider { get; init; }
        internal Func<string, string> ExternalEvaluationDataAuditDirectorySelector { get; init; }
        internal Func<IReadOnlyList<string>> ExternalEvaluationDataAuditReferenceDirectoriesProvider { get; init; }
        internal Func<bool> ExternalEvaluationDataAuditCloseApproval { get; init; }
        internal Action<string> ExternalEvaluationDataAuditStatusSink { get; init; }
        internal Func<LabelingProjectData> HistoricalSegmentationRemediationDataProvider { get; init; }
        internal Func<string> HistoricalSegmentationRemediationSourceImagePathProvider { get; init; }
        internal Func<bool> HistoricalSegmentationRemediationCloseApproval { get; init; }
        internal Action<string, string> HistoricalSegmentationRemediationStatusSink { get; init; }
        internal ModelComparisonWorkflowService ModelComparisonWorkflowService { get; init; }
        internal Func<ModelComparisonCallbacks> CreateModelComparisonCallbacks { get; init; }
        internal ExternalYoloDatasetIntakeWorkflowService ExternalYoloDatasetIntakeWorkflowService { get; init; }
        internal Func<ExternalYoloDatasetIntakeCallbacks> CreateExternalYoloDatasetIntakeCallbacks { get; init; }
        internal ListBox DatasetPurposeListBox { get; init; }
        internal ListBox LearningModeListBox { get; init; }
        internal ListBox AnnotationToolListBox { get; init; }
        internal ListBox LearningStepListBox { get; init; }
        internal Action<DependencyObject, DependencyProperty[]> RefreshAttachedCommandBindings { get; init; }
    }
}
