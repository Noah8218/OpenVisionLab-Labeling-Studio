using OpenVisionLab.ImageCanvas.ViewModels;
using System;

namespace MvcVisionSystem
{
    public sealed class WpfLabelingShellViewModels : System.IDisposable
    {
        private bool disposed;

        public WpfLabelingShellViewModels()
        {
            MenuViewModel = new ShellMenuViewModel(this);
            ShellViewModel.PropertyChanged += OnShellWorkspaceChanged;
        }

        public ShellMenuViewModel MenuViewModel { get; }

        public WpfLocalizationViewModel LanguageViewModel { get; } = WpfLocalizationViewModel.Instance;

        public WpfLabelingShellViewModel ShellViewModel { get; } = new WpfLabelingShellViewModel();

        public WpfDatasetSelectionWindowViewModel DatasetSelectionViewModel { get; } = new WpfDatasetSelectionWindowViewModel(new DatasetSelectionPreferencesService(), selectFirstVisibleRecipe: true);

        public WpfLearningWorkflowPanelViewModel LearningWorkflowViewModel { get; } = new WpfLearningWorkflowPanelViewModel();

        public WpfImageQueuePanelViewModel ImageQueueViewModel { get; } = new WpfImageQueuePanelViewModel();

        public DatasetWorkspaceViewModel DatasetWorkspaceViewModel { get; } = new DatasetWorkspaceViewModel();
        public ModelComparisonWorkspaceViewModel ModelComparisonWorkspaceViewModel { get; } = new ModelComparisonWorkspaceViewModel();
        public TrainingWorkspaceViewModel TrainingWorkspaceViewModel { get; } = new TrainingWorkspaceViewModel();

        public WpfTemplateMatchingAutoLabelViewModel TemplateMatchingAutoLabelViewModel { get; } = new WpfTemplateMatchingAutoLabelViewModel();

        public WpfCanvasPanelViewModel CanvasPanelViewModel { get; } = new WpfCanvasPanelViewModel();

        public LabelingPreferencesViewModel LabelingPreferencesViewModel { get; } = new LabelingPreferencesViewModel();

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
            DatasetWorkspaceViewModel.ApplyWorkflowCommandState(state);
            TrainingWorkspaceViewModel.ApplyWorkflowCommandState(state);
            ProjectConfigViewModel.ApplyWorkflowCommandState(state);
            MenuViewModel.Refresh();
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

        private void OnShellWorkspaceChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(WpfLabelingShellViewModel.IsDatasetStageActive) && ShellViewModel.IsDatasetStageActive)
                DatasetWorkspaceViewModel.RequestRefresh();
            if (e.PropertyName == nameof(WpfLabelingShellViewModel.IsTrainingModelStageActive) && ShellViewModel.IsTrainingModelStageActive)
                TrainingWorkspaceViewModel.RequestRefresh();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            ShellViewModel.PropertyChanged -= OnShellWorkspaceChanged;
            ShellViewModel.ClearModelRegistryLocalizationSource();
            DatasetWorkspaceViewModel.Dispose();
            ModelComparisonWorkspaceViewModel.Dispose();
            TrainingWorkspaceViewModel.Dispose();
            ImageQueueViewModel.Dispose();
            TemplateMatchingAutoLabelViewModel.Dispose();
            DatasetSelectionViewModel.Dispose();
            MainCanvasViewModel.Dispose();
            CanvasPanelViewModel.Dispose();
            LabelingPreferencesViewModel.Dispose();
            LearningWorkflowViewModel.Dispose();
            StatusBarViewModel.Dispose();
            ShellLogViewModel.Dispose();
        }
    }
}
