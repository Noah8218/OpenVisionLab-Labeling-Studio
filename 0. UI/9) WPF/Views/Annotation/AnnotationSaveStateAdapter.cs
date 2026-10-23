using MvcVisionSystem.Yolo;
using System;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns annotation dirty-state transitions and sends the immutable save-state
    /// presentation to the existing Shell surfaces. ViewModels remain the owners
    /// of their individual binding properties.
    /// </summary>
    internal sealed class AnnotationSaveStateAdapter
    {
        private readonly AnnotationSaveStateAdapterContext context;

        internal AnnotationSaveStateAdapter(AnnotationSaveStateAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            ArgumentNullException.ThrowIfNull(context.DirtyState);
            ArgumentNullException.ThrowIfNull(context.InvalidateActiveImageQualityReviewAfterEdit);
            ArgumentNullException.ThrowIfNull(context.ApplyActiveImageQueueSaveRequiredStatus);
            ArgumentNullException.ThrowIfNull(context.RefreshActiveImageQualityReviewPresentation);
            ArgumentNullException.ThrowIfNull(context.RefreshCanvasLayerVisibilityState);
            ArgumentNullException.ThrowIfNull(context.RefreshCanvasWorkflowContext);
            ArgumentNullException.ThrowIfNull(context.UpdateWorkflowProgressStatus);
            ArgumentNullException.ThrowIfNull(context.ScheduleCrashRecoveryJournalWrite);
            ArgumentNullException.ThrowIfNull(context.SetQualityReviewState);
            ArgumentNullException.ThrowIfNull(context.SetStatusBarSaveStatus);
        }

        internal void MarkAnnotationsDirty(string reason)
        {
            context.DirtyState.MarkDirty(reason, "Edit");
            if (IsAnnotationSaveBlocked())
            {
                ApplyAnnotationLoadBlockedPresentation();
                context.ScheduleCrashRecoveryJournalWrite();
                return;
            }

            ApplyAnnotationDirtyPresentation();
            context.ScheduleCrashRecoveryJournalWrite();
        }

        internal void MarkMaskStrokeAnnotationsDirty(string reason)
        {
            context.DirtyState.MarkDirty(reason, "Mask edit");
            if (IsAnnotationSaveBlocked())
            {
                ApplyAnnotationLoadBlockedPresentation();
                context.ScheduleCrashRecoveryJournalWrite();
                return;
            }

            AnnotationSaveStatePresentation dirtyPresentation =
                AnnotationSaveStatePresentationService.BuildDirty(context.DirtyState.Reason);
            context.SetStatusBarSaveStatus(
                dirtyPresentation.IsDirty,
                dirtyPresentation.StatusBarText,
                dirtyPresentation.StatusBarToolTip,
                dirtyPresentation.CanvasStatusKey);
            if (!string.Equals(context.DirtyState.Reason, "\0", StringComparison.Ordinal))
            {
                context.ScheduleCrashRecoveryJournalWrite();
                return;
            }

            context.SetStatusBarSaveStatus(
                true,
                dirtyPresentation.StatusBarText,
                dirtyPresentation.StatusBarToolTip,
                "Dirty");
            context.ScheduleCrashRecoveryJournalWrite();
        }

        internal void RefreshDeferredMaskStrokeDirtyPresentation()
        {
            if (context.DirtyState.IsDirty)
            {
                ApplyAnnotationDirtyPresentation();
            }
        }

        internal void ApplyAnnotationDirtyPresentation()
        {
            if (IsAnnotationSaveBlocked())
            {
                ApplyAnnotationLoadBlockedPresentation();
                return;
            }

            context.InvalidateActiveImageQualityReviewAfterEdit();
            ApplyAnnotationSaveStatePresentation(
                AnnotationSaveStatePresentationService.BuildDirty(context.DirtyState.Reason));
            context.ApplyActiveImageQueueSaveRequiredStatus(context.DirtyState.Reason);
            context.RefreshActiveImageQualityReviewPresentation();
            context.RefreshCanvasLayerVisibilityState();
            context.RefreshCanvasWorkflowContext();
            context.UpdateWorkflowProgressStatus();
        }

        internal void MarkAnnotationsSaved(string reason)
        {
            if (IsAnnotationSaveBlocked())
            {
                ApplyAnnotationLoadBlockedPresentation();
                return;
            }

            context.DirtyState.Clear();
            ApplyAnnotationSaveStatePresentation(
                AnnotationSaveStatePresentationService.BuildSaved(reason));
            context.RefreshCanvasLayerVisibilityState();
            context.RefreshCanvasWorkflowContext();
            context.UpdateWorkflowProgressStatus();
        }

        internal void MarkAnnotationsUnlabeled()
        {
            if (IsAnnotationSaveBlocked())
            {
                ApplyAnnotationLoadBlockedPresentation();
                return;
            }

            context.DirtyState.Clear();
            ApplyAnnotationSaveStatePresentation(
                AnnotationSaveStatePresentationService.BuildUnlabeled());
            context.RefreshCanvasLayerVisibilityState();
            context.RefreshCanvasWorkflowContext();
            context.UpdateWorkflowProgressStatus();
        }

        internal void MarkAnnotationsNoObjectSaved(string reason)
        {
            if (IsAnnotationSaveBlocked())
            {
                ApplyAnnotationLoadBlockedPresentation();
                return;
            }

            context.DirtyState.Clear();
            ApplyAnnotationSaveStatePresentation(
                AnnotationSaveStatePresentationService.BuildNoObjectSaved(reason));
            context.RefreshCanvasLayerVisibilityState();
            context.RefreshCanvasWorkflowContext();
            context.UpdateWorkflowProgressStatus();
        }

        internal void MarkAnnotationSaveFailed(string reason)
        {
            if (IsAnnotationSaveBlocked())
            {
                ApplyAnnotationLoadBlockedPresentation();
                return;
            }

            if (!context.DirtyState.IsDirty)
            {
                context.DirtyState.MarkDirty(reason, "Save failed");
                context.ApplyActiveImageQueueSaveRequiredStatus(reason);
                context.ScheduleCrashRecoveryJournalWrite();
            }

            ApplyAnnotationSaveStatePresentation(
                AnnotationSaveStatePresentationService.BuildSaveFailed(reason));
            context.RefreshCanvasWorkflowContext();
            context.UpdateWorkflowProgressStatus();
        }

        internal void SetAnnotationSaveStatusWaiting()
        {
            context.DirtyState.Clear();
            ApplyAnnotationSaveStatePresentation(
                AnnotationSaveStatePresentationService.BuildWaiting());
            context.SetQualityReviewState(
                YoloImageQualityReviewState.Unreviewed,
                false,
                false);
            context.RefreshCanvasLayerVisibilityState();
            context.RefreshCanvasWorkflowContext();
            context.UpdateWorkflowProgressStatus();
        }

        internal void ApplyAnnotationSaveStatePresentation(AnnotationSaveStatePresentation presentation)
        {
            if (presentation == null)
            {
                return;
            }

            context.SetStatusBarSaveStatus(
                presentation.IsDirty,
                presentation.StatusBarText,
                presentation.StatusBarToolTip,
                presentation.CanvasStatusKey);
            context.ApplyCanvasSaveState(presentation);
            context.ApplyObjectReviewSaveState(presentation);
        }

        private bool IsAnnotationSaveBlocked()
            => context.IsAnnotationSaveBlocked?.Invoke() == true;

        private void ApplyAnnotationLoadBlockedPresentation()
            => ApplyAnnotationSaveStatePresentation(
                AnnotationSaveStatePresentationService.BuildLoadBlocked(
                    context.AnnotationSaveBlockReasonProvider?.Invoke()));
    }

    internal sealed class AnnotationSaveStateAdapterContext
    {
        internal AnnotationDirtyState DirtyState { get; init; }
        internal Action InvalidateActiveImageQualityReviewAfterEdit { get; init; }
        internal Action<string> ApplyActiveImageQueueSaveRequiredStatus { get; init; }
        internal Action RefreshActiveImageQualityReviewPresentation { get; init; }
        internal Action RefreshCanvasLayerVisibilityState { get; init; }
        internal Action RefreshCanvasWorkflowContext { get; init; }
        internal Action UpdateWorkflowProgressStatus { get; init; }
        internal Action ScheduleCrashRecoveryJournalWrite { get; init; }
        internal Action<YoloImageQualityReviewState, bool, bool> SetQualityReviewState { get; init; }
        internal Action<bool, string, string, string> SetStatusBarSaveStatus { get; init; }
        internal Action<AnnotationSaveStatePresentation> ApplyCanvasSaveState { get; init; }
        internal Action<AnnotationSaveStatePresentation> ApplyObjectReviewSaveState { get; init; }
        internal Func<bool> IsAnnotationSaveBlocked { get; init; }
        internal Func<string> AnnotationSaveBlockReasonProvider { get; init; }
    }
}
