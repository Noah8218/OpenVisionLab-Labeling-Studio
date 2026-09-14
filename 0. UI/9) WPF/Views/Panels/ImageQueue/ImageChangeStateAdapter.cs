using MvcVisionSystem._1._Core;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using System;
using System.Collections.Generic;
using DrawingRectangle = System.Drawing.Rectangle;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns transient state cleanup while the active image changes. Image
    /// loading and annotation persistence remain with their existing owners.
    /// </summary>
    internal sealed class ImageChangeStateAdapter
    {
        private readonly ImageChangeStateAdapterContext context;

        internal ImageChangeStateAdapter(ImageChangeStateAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        internal void PrepareForImageChange()
        {
            ResetPreImageChangeState(cancelFourPointDraft: true);
        }

        internal void ResetForImageChange()
        {
            ClearAnnotationCollections();
            context.CancelPendingSegmentationSplit?.Invoke();
            context.CancelPendingSegmentationHoleEdit?.Invoke();
            context.CancelPendingPolygonVertexEdit?.Invoke();
            context.CancelPendingIntelligentScissors?.Invoke();
            context.ResetMaskStrokeStateForImageChange?.Invoke();
            context.PolygonAnnotationService.Reset();
            context.CandidateReviewState.ClearAll();
            context.SmartMaskPromptSession.Reset();
            context.ClearAnnotationHistory?.Invoke();
        }

        internal void ClearAfterQueueReset()
        {
            ResetPreImageChangeState(cancelFourPointDraft: false);
            context.ImageLoadResourceService.Clear(context.ApplicationState.ImageWorkspace.ActiveImage);
            context.ApplicationState.ImageWorkspace.SetActiveImage(string.Empty, string.Empty, null);

            ResetForImageChange();
            context.ClearCanvasImage?.Invoke();
            context.RefreshCandidateList?.Invoke();
            context.RefreshObjectList?.Invoke();
            context.SetAnnotationSaveStatusWaiting?.Invoke();
        }

        private void ResetPreImageChangeState(bool cancelFourPointDraft)
        {
            context.StopDisplayAdjustmentRefresh?.Invoke();
            if (cancelFourPointDraft)
            {
                context.CancelFourPointBoxDraft?.Invoke();
            }

            context.CancelPendingSegmentationRemoveUnderlying?.Invoke();
            context.CancelPendingPolygonVertexEdit?.Invoke();
            context.CancelPendingIntelligentScissors?.Invoke();
            context.CancelObjectGroupSelection?.Invoke();
            context.ObjectSessionStateService.Clear();
            context.ObjectMetadataStateService.Clear();
        }

        private void ClearAnnotationCollections()
        {
            context.ManualRois.Clear();
            context.ManualRoiClassNames.Clear();
            context.ManualRoiShapeKinds.Clear();
            context.ManualRoiOverlayIds.Clear();
            context.ManualSegments.Clear();
        }
    }

    internal sealed class ImageChangeStateAdapterContext
    {
        internal LabelingApplicationState ApplicationState { get; init; }
        internal ObjectSessionStateService ObjectSessionStateService { get; init; }
        internal ObjectMetadataStateService ObjectMetadataStateService { get; init; }
        internal ImageLoadResourceService ImageLoadResourceService { get; init; }
        internal CandidateReviewStateService CandidateReviewState { get; init; }
        internal SmartMaskPromptSessionService SmartMaskPromptSession { get; init; }
        internal PolygonAnnotationService PolygonAnnotationService { get; init; }
        internal IList<DrawingRectangle> ManualRois { get; init; }
        internal IList<string> ManualRoiClassNames { get; init; }
        internal IList<CanvasRoiShapeKind> ManualRoiShapeKinds { get; init; }
        internal IList<string> ManualRoiOverlayIds { get; init; }
        internal IList<LabelingSegmentationObject> ManualSegments { get; init; }
        internal Action StopDisplayAdjustmentRefresh { get; init; }
        internal Action CancelFourPointBoxDraft { get; init; }
        internal Action CancelPendingSegmentationRemoveUnderlying { get; init; }
        internal Action CancelPendingSegmentationSplit { get; init; }
        internal Action CancelPendingSegmentationHoleEdit { get; init; }
        internal Action CancelPendingPolygonVertexEdit { get; init; }
        internal Action CancelPendingIntelligentScissors { get; init; }
        internal Action CancelObjectGroupSelection { get; init; }
        internal Action ResetMaskStrokeStateForImageChange { get; init; }
        internal Action ClearAnnotationHistory { get; init; }
        internal Action ClearCanvasImage { get; init; }
        internal Action RefreshCandidateList { get; init; }
        internal Action RefreshObjectList { get; init; }
        internal Action SetAnnotationSaveStatusWaiting { get; init; }
    }
}
