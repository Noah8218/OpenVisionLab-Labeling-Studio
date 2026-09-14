using System;

namespace MvcVisionSystem
{
    // Composition-only adapter for training settings commands. The existing
    // ViewModel owns command entry and training/comparison policy; the Shell
    // supplies runtime, picker, and presentation callbacks at this boundary.
    internal sealed class TrainingSettingsPanelCommandWiring
    {
        private readonly TrainingSettingsPanelCommandWiringContext context;

        internal TrainingSettingsPanelCommandWiring(TrainingSettingsPanelCommandWiringContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        internal void ConfigureTrainingSettingsPanelCommands()
        {
            context.TrainingSettingsViewModel.ConfigureCommands(
                refreshReadiness: null,
                startTraining: null,
                stopTraining: null,
                reviewTrainedModel: context.ExecuteReviewTrainedModelCommand,
                confirmTrainedModel: context.ExecuteConfirmTrainedModelCommand,
                runYoloEngineComparison: null,
                browseSegmentationUnetCheckpoint: null,
                browseSegmentationYoloCheckpoint: null,
                runSegmentationAdapterComparison: null);
            context.TrainingSettingsViewModel.ConfigureRuntimeWorkflow(
                context.TrainingRuntimeWorkflowService,
                context.TrainingCommandLifecycleService,
                context.CreateTrainingRuntimeCallbacks);
            context.TrainingSettingsViewModel.ConfigureModelComparisonWorkflow(
                context.ModelComparisonWorkflowService,
                context.CreateModelComparisonCallbacks);
            context.TrainingSettingsViewModel.ConfigureSegmentationAdapterComparisonWorkflow(
                context.CreateSegmentationAdapterComparisonContext);
            context.TrainingSettingsViewModel.ConfigureSegmentationCheckpointSelectionWorkflow(
                context.CreateSegmentationCheckpointPathCallbacks);
        }
    }

    internal sealed class TrainingSettingsPanelCommandWiringContext
    {
        internal WpfTrainingSettingsPanelViewModel TrainingSettingsViewModel { get; init; }
        internal Action ExecuteReviewTrainedModelCommand { get; init; }
        internal Action ExecuteConfirmTrainedModelCommand { get; init; }
        internal TrainingRuntimeWorkflowService TrainingRuntimeWorkflowService { get; init; }
        internal TrainingCommandLifecycleService TrainingCommandLifecycleService { get; init; }
        internal Func<TrainingRuntimeCallbacks> CreateTrainingRuntimeCallbacks { get; init; }
        internal ModelComparisonWorkflowService ModelComparisonWorkflowService { get; init; }
        internal Func<ModelComparisonCallbacks> CreateModelComparisonCallbacks { get; init; }
        internal Func<SegmentationAdapterComparisonContext> CreateSegmentationAdapterComparisonContext { get; init; }
        internal Func<SegmentationCheckpointPathCallbacks> CreateSegmentationCheckpointPathCallbacks { get; init; }
    }
}
