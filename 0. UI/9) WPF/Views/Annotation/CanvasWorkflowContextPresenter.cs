using System;

namespace MvcVisionSystem
{
    /// <summary>
    /// Projects the shell's current annotation state into the canvas and guide
    /// ViewModels. The shell supplies a snapshot so this presenter never owns
    /// image, annotation, or candidate collections.
    /// </summary>
    internal sealed class CanvasWorkflowContextPresenter
    {
        private readonly WpfCanvasPanelViewModel canvasPanelViewModel;
        private readonly WpfLearningWorkflowPanelViewModel learningWorkflowViewModel;
        private readonly Action refreshSmartMaskCommandState;
        private readonly Func<CanvasWorkflowContextSnapshot> snapshotProvider;

        internal CanvasWorkflowContextPresenter(
            WpfCanvasPanelViewModel canvasPanelViewModel,
            WpfLearningWorkflowPanelViewModel learningWorkflowViewModel,
            Action refreshSmartMaskCommandState,
            Func<CanvasWorkflowContextSnapshot> snapshotProvider)
        {
            this.canvasPanelViewModel = canvasPanelViewModel ?? throw new ArgumentNullException(nameof(canvasPanelViewModel));
            this.learningWorkflowViewModel = learningWorkflowViewModel ?? throw new ArgumentNullException(nameof(learningWorkflowViewModel));
            this.refreshSmartMaskCommandState = refreshSmartMaskCommandState ?? throw new ArgumentNullException(nameof(refreshSmartMaskCommandState));
            this.snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
        }

        internal void Refresh()
        {
            refreshSmartMaskCommandState();
            CanvasWorkflowContextSnapshot snapshot = snapshotProvider();
            if (snapshot == null)
            {
                return;
            }

            CanvasWorkflowContext context = CanvasWorkflowContextPresentationService.Build(
                snapshot.IsInferenceMode,
                snapshot.IsAnomalyDatasetPurpose,
                snapshot.HasActiveImage,
                snapshot.HasUnsavedAnnotations,
                snapshot.HasCanvasLabelObjects,
                snapshot.PendingCandidateCount,
                snapshot.SelectedStep,
                snapshot.SelectedStepText,
                snapshot.SelectedTool,
                snapshot.SelectedToolText,
                snapshot.ActiveAnnotationTool,
                snapshot.SelectedBoxDrawingMethod);
            canvasPanelViewModel.SetWorkflowContext(context);
            learningWorkflowViewModel.SetLiveLabelingTask(context);
        }
    }

    /// <summary>
    /// Immutable input captured by the shell at one point in the workflow.
    /// </summary>
    internal sealed class CanvasWorkflowContextSnapshot
    {
        internal CanvasWorkflowContextSnapshot(
            bool isInferenceMode,
            bool isAnomalyDatasetPurpose,
            bool hasActiveImage,
            bool hasUnsavedAnnotations,
            bool hasCanvasLabelObjects,
            int pendingCandidateCount,
            WpfLearningStep? selectedStep,
            string selectedStepText,
            WpfAnnotationTool? selectedTool,
            string selectedToolText,
            WpfAnnotationTool activeAnnotationTool,
            LabelingBoxDrawingMethod? selectedBoxDrawingMethod)
        {
            IsInferenceMode = isInferenceMode;
            IsAnomalyDatasetPurpose = isAnomalyDatasetPurpose;
            HasActiveImage = hasActiveImage;
            HasUnsavedAnnotations = hasUnsavedAnnotations;
            HasCanvasLabelObjects = hasCanvasLabelObjects;
            PendingCandidateCount = Math.Max(0, pendingCandidateCount);
            SelectedStep = selectedStep;
            SelectedStepText = selectedStepText ?? string.Empty;
            SelectedTool = selectedTool;
            SelectedToolText = selectedToolText ?? string.Empty;
            ActiveAnnotationTool = activeAnnotationTool;
            SelectedBoxDrawingMethod = selectedBoxDrawingMethod;
        }

        internal bool IsInferenceMode { get; }

        internal bool IsAnomalyDatasetPurpose { get; }

        internal bool HasActiveImage { get; }

        internal bool HasUnsavedAnnotations { get; }

        internal bool HasCanvasLabelObjects { get; }

        internal int PendingCandidateCount { get; }

        internal WpfLearningStep? SelectedStep { get; }

        internal string SelectedStepText { get; }

        internal WpfAnnotationTool? SelectedTool { get; }

        internal string SelectedToolText { get; }

        internal WpfAnnotationTool ActiveAnnotationTool { get; }

        internal LabelingBoxDrawingMethod? SelectedBoxDrawingMethod { get; }
    }
}
