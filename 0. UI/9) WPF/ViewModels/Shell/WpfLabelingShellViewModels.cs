using OpenVisionLab.ImageCanvas.ViewModels;
using System;

namespace MvcVisionSystem
{
    public sealed class WpfLabelingShellViewModels : System.IDisposable
    {
        private bool disposed;

        public WpfLocalizationViewModel LanguageViewModel { get; } = WpfLocalizationViewModel.Instance;

        public WpfLabelingShellViewModel ShellViewModel { get; } = new WpfLabelingShellViewModel();

        public WpfLearningWorkflowPanelViewModel LearningWorkflowViewModel { get; } = new WpfLearningWorkflowPanelViewModel();

        public WpfImageQueuePanelViewModel ImageQueueViewModel { get; } = new WpfImageQueuePanelViewModel();

        public WpfTemplateMatchingAutoLabelViewModel TemplateMatchingAutoLabelViewModel { get; } = new WpfTemplateMatchingAutoLabelViewModel();

        public WpfCanvasPanelViewModel CanvasPanelViewModel { get; } = new WpfCanvasPanelViewModel();

        public WpfObjectReviewPanelViewModel ObjectReviewViewModel { get; } = new WpfObjectReviewPanelViewModel();

        private readonly Lazy<WpfModelWorkflowViewModels> modelWorkflowViewModels =
            new Lazy<WpfModelWorkflowViewModels>(() => new WpfModelWorkflowViewModels());

        public WpfModelWorkflowViewModels ModelWorkflowViewModels => modelWorkflowViewModels.Value;

        public bool IsModelWorkflowCreated => modelWorkflowViewModels.IsValueCreated;

        // Binding-safe accessors do not create model workflow state. Explicit shell
        // workflow entry points use the window accessors, which compose the model
        // panels before returning the concrete ViewModel.
        public WpfCandidateReviewPanelViewModel CandidateReviewViewModel =>
            modelWorkflowViewModels.IsValueCreated
                ? modelWorkflowViewModels.Value.ExistingCandidateReviewViewModel
                : null;

        public WpfCandidateReviewPanelViewModel ExistingCandidateReviewViewModel => CandidateReviewViewModel;

        public WpfClassCatalogPanelViewModel ClassCatalogViewModel { get; } = new WpfClassCatalogPanelViewModel();

        public WpfYoloStatusPanelViewModel YoloStatusViewModel =>
            modelWorkflowViewModels.IsValueCreated
                ? modelWorkflowViewModels.Value.ExistingYoloStatusViewModel
                : null;

        public WpfYoloStatusPanelViewModel ExistingYoloStatusViewModel => YoloStatusViewModel;

        public WpfProjectConfigPanelViewModel ProjectConfigViewModel { get; } = new WpfProjectConfigPanelViewModel();

        public WpfYoloModelSettingsPanelViewModel YoloModelSettingsViewModel =>
            modelWorkflowViewModels.IsValueCreated
                ? modelWorkflowViewModels.Value.ExistingYoloModelSettingsViewModel
                : null;

        public WpfYoloModelSettingsPanelViewModel ExistingYoloModelSettingsViewModel => YoloModelSettingsViewModel;

        public WpfTrainingSettingsPanelViewModel TrainingSettingsViewModel =>
            modelWorkflowViewModels.IsValueCreated
                ? modelWorkflowViewModels.Value.ExistingTrainingSettingsViewModel
                : null;

        public WpfTrainingSettingsPanelViewModel ExistingTrainingSettingsViewModel => TrainingSettingsViewModel;

        public WpfStatusBarPanelViewModel StatusBarViewModel { get; } = new WpfStatusBarPanelViewModel();

        public WpfShellLogPanelViewModel ShellLogViewModel { get; } = new WpfShellLogPanelViewModel();

        public WpfRuntimeDiagnosticsViewModel RuntimeDiagnosticsViewModel { get; } = new WpfRuntimeDiagnosticsViewModel();

        public RoiImageCanvasViewModel MainCanvasViewModel { get; } = new RoiImageCanvasViewModel("Main");

        public void ApplyWorkflowCommandState(WorkflowCommandState state, ModelComparisonCommandState comparisonState)
        {
            ShellViewModel.ApplyWorkflowCommandState(state);
            ImageQueueViewModel.ApplyWorkflowCommandState(state);
            ProjectConfigViewModel.ApplyWorkflowCommandState(state);
            LearningWorkflowViewModel.SetModelComparisonRunState(
                comparisonState?.IsEnabled == true,
                comparisonState?.ActionText,
                comparisonState?.ToolTipText,
                comparisonState?.BasisText);

            if (!IsModelWorkflowCreated)
            {
                return;
            }

            ModelWorkflowViewModels.YoloStatusViewModel.ApplyWorkflowCommandState(state);
            ModelWorkflowViewModels.YoloModelSettingsViewModel.ApplyWorkflowCommandState(state);
            ModelWorkflowViewModels.TrainingSettingsViewModel.ApplyWorkflowCommandState(state);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            TemplateMatchingAutoLabelViewModel.Dispose();
            MainCanvasViewModel.Dispose();
            CanvasPanelViewModel.Dispose();
            LearningWorkflowViewModel.Dispose();
            StatusBarViewModel.Dispose();
            ShellLogViewModel.Dispose();
        }
    }
}
