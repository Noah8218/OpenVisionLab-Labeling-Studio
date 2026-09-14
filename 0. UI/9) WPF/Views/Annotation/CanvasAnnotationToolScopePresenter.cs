using System;

namespace MvcVisionSystem
{
    /// <summary>
    /// Applies the dataset-purpose tool scope to the shell, canvas, and image
    /// queue ViewModels. WPF tab selection is supplied as a narrow UI callback.
    /// </summary>
    internal sealed class CanvasAnnotationToolScopePresenter
    {
        private readonly WpfLabelingShellViewModel shellViewModel;
        private readonly WpfCanvasPanelViewModel canvasPanelViewModel;
        private readonly WpfImageQueuePanelViewModel imageQueueViewModel;
        private readonly WpfLearningWorkflowPanelViewModel learningWorkflowViewModel;
        private readonly Func<bool> isAnomalyDatasetPurpose;
        private readonly Action refreshImageQueuePurposePresentation;
        private readonly Func<bool> isLabelingStageActive;
        private readonly Action selectLearningReviewTab;
        private readonly Action<object> annotationToolSelectionChanged;

        internal CanvasAnnotationToolScopePresenter(
            WpfLabelingShellViewModel shellViewModel,
            WpfCanvasPanelViewModel canvasPanelViewModel,
            WpfImageQueuePanelViewModel imageQueueViewModel,
            WpfLearningWorkflowPanelViewModel learningWorkflowViewModel,
            Func<bool> isAnomalyDatasetPurpose,
            Action refreshImageQueuePurposePresentation,
            Func<bool> isLabelingStageActive,
            Action selectLearningReviewTab,
            Action<object> annotationToolSelectionChanged)
        {
            this.shellViewModel = shellViewModel ?? throw new ArgumentNullException(nameof(shellViewModel));
            this.canvasPanelViewModel = canvasPanelViewModel ?? throw new ArgumentNullException(nameof(canvasPanelViewModel));
            this.imageQueueViewModel = imageQueueViewModel ?? throw new ArgumentNullException(nameof(imageQueueViewModel));
            this.learningWorkflowViewModel = learningWorkflowViewModel ?? throw new ArgumentNullException(nameof(learningWorkflowViewModel));
            this.isAnomalyDatasetPurpose = isAnomalyDatasetPurpose ?? throw new ArgumentNullException(nameof(isAnomalyDatasetPurpose));
            this.refreshImageQueuePurposePresentation = refreshImageQueuePurposePresentation ?? throw new ArgumentNullException(nameof(refreshImageQueuePurposePresentation));
            this.isLabelingStageActive = isLabelingStageActive ?? throw new ArgumentNullException(nameof(isLabelingStageActive));
            this.selectLearningReviewTab = selectLearningReviewTab ?? throw new ArgumentNullException(nameof(selectLearningReviewTab));
            this.annotationToolSelectionChanged = annotationToolSelectionChanged ?? throw new ArgumentNullException(nameof(annotationToolSelectionChanged));
        }

        internal void Refresh()
        {
            bool isAnomalyImageReview = isAnomalyDatasetPurpose();
            shellViewModel.SetAnomalyImageReviewMode(isAnomalyImageReview);
            canvasPanelViewModel.SetAnomalyImageReviewMode(isAnomalyImageReview);
            imageQueueViewModel.SetAnomalyImageReviewMode(isAnomalyImageReview);
            refreshImageQueuePurposePresentation();
            if (isAnomalyImageReview && isLabelingStageActive())
            {
                selectLearningReviewTab();
            }

            canvasPanelViewModel.ConfigureAnnotationTools(
                learningWorkflowViewModel.VisibleAnnotationTools,
                learningWorkflowViewModel.SelectedTool,
                annotationToolSelectionChanged);
        }
    }
}
