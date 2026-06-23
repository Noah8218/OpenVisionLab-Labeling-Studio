using OpenVisionLab.ImageCanvas.ViewModels;

namespace MvcVisionSystem
{
    public sealed class WpfLabelingShellViewModels
    {
        public WpfLabelingShellViewModel ShellViewModel { get; } = new WpfLabelingShellViewModel();

        public WpfLearningWorkflowPanelViewModel LearningWorkflowViewModel { get; } = new WpfLearningWorkflowPanelViewModel();

        public WpfImageQueuePanelViewModel ImageQueueViewModel { get; } = new WpfImageQueuePanelViewModel();

        public WpfCanvasPanelViewModel CanvasPanelViewModel { get; } = new WpfCanvasPanelViewModel();

        public WpfObjectReviewPanelViewModel ObjectReviewViewModel { get; } = new WpfObjectReviewPanelViewModel();

        public WpfCandidateReviewPanelViewModel CandidateReviewViewModel { get; } = new WpfCandidateReviewPanelViewModel();

        public WpfClassCatalogPanelViewModel ClassCatalogViewModel { get; } = new WpfClassCatalogPanelViewModel();

        public WpfYoloStatusPanelViewModel YoloStatusViewModel { get; } = new WpfYoloStatusPanelViewModel();

        public WpfProjectConfigPanelViewModel ProjectConfigViewModel { get; } = new WpfProjectConfigPanelViewModel();

        public WpfYoloModelSettingsPanelViewModel YoloModelSettingsViewModel { get; } = new WpfYoloModelSettingsPanelViewModel();

        public WpfTrainingSettingsPanelViewModel TrainingSettingsViewModel { get; } = new WpfTrainingSettingsPanelViewModel();

        public WpfStatusBarPanelViewModel StatusBarViewModel { get; } = new WpfStatusBarPanelViewModel();

        public WpfShellLogPanelViewModel ShellLogViewModel { get; } = new WpfShellLogPanelViewModel();

        public RoiImageCanvasViewModel MainCanvasViewModel { get; } = new RoiImageCanvasViewModel("Main");
    }
}