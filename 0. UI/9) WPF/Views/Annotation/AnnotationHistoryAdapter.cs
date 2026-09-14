using MvcVisionSystem._1._Core;
using MvcVisionSystem.DrawObject;
using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the WPF annotation history call path while the history service owns
    /// bounded undo/redo state. The adapter maps snapshots to the Shell's live
    /// collections and presentation callbacks without referencing WPF controls.
    /// </summary>
    internal sealed class AnnotationHistoryAdapter
    {
        private readonly AnnotationHistoryAdapterContext context;
        private readonly AnnotationHistoryWorkflowService workflowService;
        private bool suppressAnnotationHistory;
        private string activeRoiEditHistoryOverlayId = string.Empty;

        internal AnnotationHistoryAdapter(AnnotationHistoryAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            workflowService = context.AnnotationHistoryWorkflowService ?? new AnnotationHistoryWorkflowService();
            ArgumentNullException.ThrowIfNull(context.ManualRois);
            ArgumentNullException.ThrowIfNull(context.ManualRoiClassNames);
            ArgumentNullException.ThrowIfNull(context.ManualRoiShapeKinds);
            ArgumentNullException.ThrowIfNull(context.ManualRoiOverlayIds);
            ArgumentNullException.ThrowIfNull(context.ManualSegments);
            ArgumentNullException.ThrowIfNull(context.PendingCandidates);
            ArgumentNullException.ThrowIfNull(context.ConfirmedCandidates);
            ArgumentNullException.ThrowIfNull(context.MarkAnnotationsDirty);
            ArgumentNullException.ThrowIfNull(context.ApplyToolState);
        }

        internal AnnotationHistoryWorkflowService WorkflowService => workflowService;

        internal WpfAnnotationHistorySnapshot CaptureAnnotationHistory(string actionName)
            => AnnotationHistoryService.Capture(
                actionName,
                context.ManualRois.ToList(),
                context.ManualRoiClassNames.ToList(),
                context.ManualRoiShapeKinds.ToList(),
                context.ManualSegments.ToList(),
                context.PendingCandidates.ToList(),
                context.ConfirmedCandidates.ToList());

        internal WpfAnnotationHistorySnapshot CaptureManualRoiHistory(string actionName)
            => AnnotationHistoryService.CaptureManualRoiList(
                actionName,
                context.ManualRois.ToList(),
                context.ManualRoiClassNames.ToList(),
                context.ManualRoiShapeKinds.ToList());

        internal void RegisterAnnotationHistoryBeforeChange(string actionName, bool markDirty = true)
            => PushAnnotationHistorySnapshot(CaptureAnnotationHistory(actionName), markDirty);

        internal void RegisterRoiEditHistoryBeforeChange(string overlayId, string actionName)
        {
            string normalizedOverlayId = overlayId ?? string.Empty;
            if (string.Equals(activeRoiEditHistoryOverlayId, normalizedOverlayId, StringComparison.Ordinal))
            {
                return;
            }

            RegisterAnnotationHistoryBeforeChange(actionName);
            activeRoiEditHistoryOverlayId = normalizedOverlayId;
        }

        internal void PushAnnotationHistorySnapshot(WpfAnnotationHistorySnapshot snapshot, bool markDirty = true)
        {
            if (suppressAnnotationHistory || snapshot == null)
            {
                return;
            }

            if (workflowService.Push(snapshot) && markDirty)
            {
                context.MarkAnnotationsDirty(snapshot.ActionName);
            }

            RefreshAnnotationHistoryToolState();
        }

        internal void ClearAnnotationHistory()
        {
            workflowService.Clear();
            activeRoiEditHistoryOverlayId = string.Empty;
            RefreshAnnotationHistoryToolState();
        }

        internal void ResetActiveRoiEditHistory()
            => activeRoiEditHistoryOverlayId = string.Empty;

        internal void RefreshAnnotationHistoryToolState()
        {
            bool hasPendingMaskStrokeUndo = context.HasPendingMaskStrokeUndoWork?.Invoke() == true;
            AnnotationHistoryToolState toolState = workflowService.GetToolState(
                hasPendingMaskStrokeUndo,
                context.GetPendingMaskStrokeUndoActionName?.Invoke());
            context.ApplyToolState(toolState);
        }

        internal bool UndoWpfAnnotationHistory()
        {
            context.CompleteMaskAnnotationStroke?.Invoke();
            context.FlushQueuedMaskStrokeCommits?.Invoke();
            if (!workflowService.TryUndo(
                    CaptureHistoryForOppositeStack,
                    out AnnotationHistoryTransition transition))
            {
                context.SetYoloCommandStatus?.Invoke("되돌릴 편집 이력이 없습니다.", false);
                return false;
            }

            RestoreAnnotationHistorySnapshot(transition.Target);
            string displayActionName = transition.DisplayActionName;
            context.SetYoloCommandStatus?.Invoke($"되돌리기: {displayActionName}", false);
            context.AppendLog?.Invoke($"되돌리기: {displayActionName}");
            context.MarkAnnotationsDirty($"되돌리기 {displayActionName}");
            RefreshAnnotationHistoryToolState();
            return true;
        }

        internal void ExecuteUndoAnnotationCommand()
            => UndoWpfAnnotationHistory();

        internal void ExecuteRedoAnnotationCommand()
            => RedoWpfAnnotationHistory();

        internal bool RedoWpfAnnotationHistory()
        {
            context.CompleteMaskAnnotationStroke?.Invoke();
            context.FlushQueuedMaskStrokeCommits?.Invoke();
            if (!workflowService.TryRedo(
                    CaptureHistoryForOppositeStack,
                    out AnnotationHistoryTransition transition))
            {
                context.SetYoloCommandStatus?.Invoke("다시 실행할 편집 이력이 없습니다.", false);
                return false;
            }

            RestoreAnnotationHistorySnapshot(transition.Target);
            string displayActionName = transition.DisplayActionName;
            context.SetYoloCommandStatus?.Invoke($"다시 적용: {displayActionName}", false);
            context.AppendLog?.Invoke($"다시 적용: {displayActionName}");
            context.MarkAnnotationsDirty($"다시 적용 {displayActionName}");
            RefreshAnnotationHistoryToolState();
            return true;
        }

        private WpfAnnotationHistorySnapshot CaptureHistoryForOppositeStack(
            string actionName,
            WpfAnnotationHistorySnapshot target)
        {
            return AnnotationHistoryService.CaptureMaskDeltaInverse(
                    actionName,
                    target,
                    context.ManualSegments.ToList())
                ?? CaptureAnnotationHistory(actionName);
        }

        internal void RestoreAnnotationHistorySnapshot(WpfAnnotationHistorySnapshot snapshot)
        {
            context.CancelPendingIntelligentScissors?.Invoke();
            suppressAnnotationHistory = true;
            try
            {
                AnnotationHistoryService.Restore(
                    snapshot,
                    context.ManualRois,
                    context.ManualRoiClassNames,
                    context.ManualRoiShapeKinds,
                    context.ManualRoiOverlayIds,
                    context.ManualSegments,
                    context.PendingCandidates,
                    context.ConfirmedCandidates);

                activeRoiEditHistoryOverlayId = string.Empty;
                context.ResetPolygonAnnotation?.Invoke();
                context.ResetMaskStrokeStateAfterHistoryRestore?.Invoke();
                context.ClearMaskStrokePreview?.Invoke();
                context.EnsureManualRoiMetadataCount?.Invoke();
                context.RefreshPolygonOverlays?.Invoke();
                context.RefreshObjectList?.Invoke();
                context.RefreshCandidateList?.Invoke();
                context.RedrawReviewRois?.Invoke();
                context.PopulateClassList?.Invoke();
                context.UpdateDetectionResultOverlay?.Invoke();
                context.RefreshActiveImageQueueStatus?.Invoke(context.PendingCandidates.Count > 0);
                context.SetPythonStatus?.Invoke($"추론: 대기 {context.PendingCandidates.Count} / 확정 {context.ConfirmedCandidates.Count}");
            }
            finally
            {
                suppressAnnotationHistory = false;
            }
        }
    }

    internal sealed class AnnotationHistoryAdapterContext
    {
        internal AnnotationHistoryWorkflowService AnnotationHistoryWorkflowService { get; init; }
        internal IList<Rectangle> ManualRois { get; init; }
        internal IList<string> ManualRoiClassNames { get; init; }
        internal IList<CanvasRoiShapeKind> ManualRoiShapeKinds { get; init; }
        internal IList<string> ManualRoiOverlayIds { get; init; }
        internal IList<LabelingSegmentationObject> ManualSegments { get; init; }
        internal IList<YoloWorkerSmokeCandidate> PendingCandidates { get; init; }
        internal IList<YoloWorkerSmokeCandidate> ConfirmedCandidates { get; init; }
        internal Func<bool> HasPendingMaskStrokeUndoWork { get; init; }
        internal Func<string> GetPendingMaskStrokeUndoActionName { get; init; }
        internal Action CompleteMaskAnnotationStroke { get; init; }
        internal Action FlushQueuedMaskStrokeCommits { get; init; }
        internal Action CancelPendingIntelligentScissors { get; init; }
        internal Action ResetPolygonAnnotation { get; init; }
        internal Action ResetMaskStrokeStateAfterHistoryRestore { get; init; }
        internal Action ClearMaskStrokePreview { get; init; }
        internal Action EnsureManualRoiMetadataCount { get; init; }
        internal Action RefreshPolygonOverlays { get; init; }
        internal Action RefreshObjectList { get; init; }
        internal Action RefreshCandidateList { get; init; }
        internal Action RedrawReviewRois { get; init; }
        internal Action PopulateClassList { get; init; }
        internal Action UpdateDetectionResultOverlay { get; init; }
        internal Action<bool> RefreshActiveImageQueueStatus { get; init; }
        internal Action<string> SetPythonStatus { get; init; }
        internal Action<string, bool> SetYoloCommandStatus { get; init; }
        internal Action<string> AppendLog { get; init; }
        internal Action<string> MarkAnnotationsDirty { get; init; }
        internal Action<AnnotationHistoryToolState> ApplyToolState { get; init; }
    }
}
