using System;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Undo/redo touches ROI, segmentation, and AI candidate state together, so it stays in one history partial instead of the event flow.
        private WpfAnnotationHistorySnapshot CaptureAnnotationHistory(string actionName)
        {
            return WpfAnnotationHistoryService.Capture(
                actionName,
                manualRois,
                manualRoiClassNames,
                manualRoiShapeKinds,
                manualSegments,
                pendingDetectionCandidates,
                confirmedDetectionCandidates);
        }

        private WpfAnnotationHistorySnapshot CaptureManualRoiHistory(string actionName)
        {
            return WpfAnnotationHistoryService.CaptureManualRoiList(
                actionName,
                manualRois,
                manualRoiClassNames,
                manualRoiShapeKinds);
        }

        private void RegisterAnnotationHistoryBeforeChange(string actionName, bool markDirty = true)
        {
            PushAnnotationHistorySnapshot(CaptureAnnotationHistory(actionName), markDirty);
        }

        private void RegisterRoiEditHistoryBeforeChange(string overlayId, string actionName)
        {
            string normalizedOverlayId = overlayId ?? string.Empty;
            if (string.Equals(activeRoiEditHistoryOverlayId, normalizedOverlayId, StringComparison.Ordinal))
            {
                return;
            }

            RegisterAnnotationHistoryBeforeChange(actionName);
            activeRoiEditHistoryOverlayId = normalizedOverlayId;
        }

        private void PushAnnotationHistorySnapshot(WpfAnnotationHistorySnapshot snapshot, bool markDirty = true)
        {
            if (suppressAnnotationHistory || snapshot == null)
            {
                return;
            }

            undoAnnotationHistory.Add(snapshot);
            if (undoAnnotationHistory.Count > AnnotationHistoryLimit)
            {
                undoAnnotationHistory.RemoveAt(0);
            }

            redoAnnotationHistory.Clear();
            if (markDirty)
            {
                MarkAnnotationsDirty(snapshot.ActionName);
            }
            RefreshAnnotationHistoryToolState();
        }

        private void ClearAnnotationHistory()
        {
            undoAnnotationHistory.Clear();
            redoAnnotationHistory.Clear();
            activeRoiEditHistoryOverlayId = string.Empty;
            RefreshAnnotationHistoryToolState();
        }

        private void RefreshAnnotationHistoryToolState()
        {
            bool hasPendingMaskStrokeUndo = HasPendingMaskStrokeUndoWork();
            bool canUndo = hasPendingMaskStrokeUndo || undoAnnotationHistory.Count > 0;
            bool canRedo = !hasPendingMaskStrokeUndo && redoAnnotationHistory.Count > 0;
            string undoActionName = hasPendingMaskStrokeUndo
                ? GetPendingMaskStrokeUndoActionName()
                : undoAnnotationHistory.Count > 0
                    ? NormalizeHistoryActionName(undoAnnotationHistory[undoAnnotationHistory.Count - 1].ActionName)
                    : string.Empty;
            string redoActionName = canRedo
                ? NormalizeHistoryActionName(redoAnnotationHistory[redoAnnotationHistory.Count - 1].ActionName)
                : string.Empty;
            LearningWorkflowViewModel?.SetAnnotationHistoryState(
                canUndo,
                canRedo,
                undoActionName,
                redoActionName);
        }

        private bool HasPendingMaskStrokeUndoWork()
            => pendingMaskStrokeCommitCount > 0 || queuedMaskStrokeCommits.Count > 0;

        private string GetPendingMaskStrokeUndoActionName()
            => queuedMaskStrokeCommits.Count > 0
                ? NormalizeHistoryActionName(queuedMaskStrokeCommits.Peek().ActionName)
                : string.Empty;

        private static string NormalizeHistoryActionName(string actionName)
        {
            string normalized = actionName ?? string.Empty;
            if (normalized.StartsWith("Undo ", StringComparison.OrdinalIgnoreCase))
            {
                return normalized.Substring(5);
            }

            if (normalized.StartsWith("Redo ", StringComparison.OrdinalIgnoreCase))
            {
                return normalized.Substring(5);
            }

            return normalized;
        }

        private static string FormatHistoryActionForDisplay(string actionName)
        {
            string normalized = NormalizeHistoryActionName(actionName);
            return string.IsNullOrWhiteSpace(normalized) ? "\uD3B8\uC9D1" : normalized;
        }

        private bool UndoWpfAnnotationHistory()
        {
            CompleteMaskAnnotationStroke();
            FlushQueuedMaskStrokeCommits();
            if (undoAnnotationHistory.Count == 0)
            {
                SetYoloCommandStatus("되돌릴 편집 이력이 없습니다.", isBusy: false);
                return false;
            }

            WpfAnnotationHistorySnapshot target = undoAnnotationHistory[undoAnnotationHistory.Count - 1];
            undoAnnotationHistory.RemoveAt(undoAnnotationHistory.Count - 1);
            redoAnnotationHistory.Add(CaptureAnnotationHistory($"Redo {target.ActionName}"));
            RestoreAnnotationHistorySnapshot(target);
            string displayActionName = FormatHistoryActionForDisplay(target.ActionName);
            SetYoloCommandStatus($"\uB418\uB3CC\uB9AC\uAE30: {displayActionName}", isBusy: false);
            AppendLog($"\uB418\uB3CC\uB9AC\uAE30: {displayActionName}");
            MarkAnnotationsDirty($"\uB418\uB3CC\uB9AC\uAE30 {displayActionName}");
            RefreshAnnotationHistoryToolState();
            return true;
        }

        private void ExecuteUndoAnnotationCommand()
        {
            UndoWpfAnnotationHistory();
        }

        private void ExecuteRedoAnnotationCommand()
        {
            RedoWpfAnnotationHistory();
        }

        private bool RedoWpfAnnotationHistory()
        {
            CompleteMaskAnnotationStroke();
            FlushQueuedMaskStrokeCommits();
            if (redoAnnotationHistory.Count == 0)
            {
                SetYoloCommandStatus("다시 실행할 편집 이력이 없습니다.", isBusy: false);
                return false;
            }

            WpfAnnotationHistorySnapshot target = redoAnnotationHistory[redoAnnotationHistory.Count - 1];
            redoAnnotationHistory.RemoveAt(redoAnnotationHistory.Count - 1);
            undoAnnotationHistory.Add(CaptureAnnotationHistory($"Undo {target.ActionName}"));
            if (undoAnnotationHistory.Count > AnnotationHistoryLimit)
            {
                undoAnnotationHistory.RemoveAt(0);
            }

            RestoreAnnotationHistorySnapshot(target);
            string displayActionName = FormatHistoryActionForDisplay(target.ActionName);
            SetYoloCommandStatus($"\uB2E4\uC2DC \uC801\uC6A9: {displayActionName}", isBusy: false);
            AppendLog($"\uB2E4\uC2DC \uC801\uC6A9: {displayActionName}");
            MarkAnnotationsDirty($"\uB2E4\uC2DC \uC801\uC6A9 {displayActionName}");
            RefreshAnnotationHistoryToolState();
            return true;
        }

        private void RestoreAnnotationHistorySnapshot(WpfAnnotationHistorySnapshot snapshot)
        {
            suppressAnnotationHistory = true;
            try
            {
                WpfAnnotationHistoryService.Restore(
                    snapshot,
                    manualRois,
                    manualRoiClassNames,
                    manualRoiShapeKinds,
                    manualRoiOverlayIds,
                    manualSegments,
                    candidateReviewState.MutablePendingCandidates,
                    candidateReviewState.MutableConfirmedCandidates);

                activeRoiEditHistoryOverlayId = string.Empty;
                polygonAnnotationService.Reset();
                lastMaskStrokePoint = null;
                activeMaskStrokeInProgress = false;
                activeMaskStrokeActionName = string.Empty;
                CancelMaskStrokePreviewCommitSwap();
                MainCanvasViewModel?.ClearMaskStrokePreview(refresh: false);
                EnsureManualRoiMetadataCount();
                RefreshPolygonOverlays();
                RefreshObjectList();
                RefreshCandidateList();
                RedrawReviewRois();
                PopulateClassList();
                UpdateDetectionResultOverlay();
                RefreshActiveImageQueueStatus(hasActiveCandidates: pendingDetectionCandidates.Count > 0);
                SetPythonStatus($"\uCD94\uB860: \uB300\uAE30 {pendingDetectionCandidates.Count} / \uD655\uC815 {confirmedDetectionCandidates.Count}");
            }
            finally
            {
                suppressAnnotationHistory = false;
            }
        }
    }
}
