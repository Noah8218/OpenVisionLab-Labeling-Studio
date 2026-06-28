using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Threading;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        private void CompleteMaskAnnotationStroke()
        {
            Stopwatch strokeStopwatch = Stopwatch.StartNew();
            bool strokeWasActive = activeMaskStrokeInProgress;
            bool commitQueued = strokeWasActive && EnqueueMaskAnnotationStrokeCommit();
            if (commitQueued)
            {
                double strokeMilliseconds = strokeStopwatch.Elapsed.TotalMilliseconds;
                SetModelStatus(FormattableString.Invariant($"\uB9C8\uC2A4\uD06C \uD3B8\uC9D1 \uC608\uC57D: \uB300\uAE30 {pendingMaskStrokeCommitCount}\uAC1C / MouseUp {strokeMilliseconds:F1}ms"));
            }
            else
            {
                if (!HasPendingMaskStrokeCommitWork())
                {
                    MainCanvasViewModel?.ClearMaskStrokePreview(clearTexture: false, refreshAfterInput: true);
                }

                if (strokeWasActive)
                {
                    double strokeMilliseconds = strokeStopwatch.Elapsed.TotalMilliseconds;
                    SetModelStatus(FormattableString.Invariant($"\uB9C8\uC2A4\uD06C \uD3B8\uC9D1 \uBC18\uC601: \uBCC0\uACBD \uC5C6\uC74C / MouseUp {strokeMilliseconds:F1}ms"));
                }
            }

            activeMaskStrokeInProgress = false;
            activeMaskStrokeActionName = string.Empty;
            activeMaskStrokeSegmentIndices.Clear();
            ResetMaskStrokeCommitBuffer();
            activeMaskStrokeNeedsFullObjectRefresh = false;
        }

        private bool EnqueueMaskAnnotationStrokeCommit()
        {
            if (activeMaskStrokeCommitSession.Count == 0)
            {
                return false;
            }

            WpfAnnotationTool tool = activeMaskStrokeCommitSession.Tool;
            if (tool != WpfAnnotationTool.Brush && tool != WpfAnnotationTool.Eraser)
            {
                return false;
            }

            string className = FirstNonEmpty(activeMaskStrokeCommitSession.ClassName, GetSelectedClassName(), "Defect");
            CClassItem classItem = tool == WpfAnnotationTool.Brush
                ? CloneClassItemForQueuedMaskCommit(EnsureClassItem(className))
                : null;
            var command = new WpfQueuedMaskStrokeCommit(
                ++queuedMaskStrokeCommitSequence,
                activeImagePath,
                activeImageSize,
                activeMaskStrokeCommitSession.Centers.ToList(),
                activeMaskStrokeCommitSession.Radius > 0 ? activeMaskStrokeCommitSession.Radius : GetMaskBrushRadius(),
                tool,
                className,
                classItem,
                activeMaskStrokeActionName,
                pendingDetectionCandidates.Count > 0);

            queuedMaskStrokeCommits.Enqueue(command);
            pendingMaskStrokeCommitCount++;
            // The FBO preview is visible immediately, but the edit is already real
            // from the operator's point of view. Mark save state now instead of
            // waiting for deferred CPU MaskData/history materialization.
            MarkAnnotationsDirty(activeMaskStrokeActionName);
            ScheduleMaskStrokeCommitQueue();
            return true;
        }

        private void ScheduleMaskStrokeCommitQueue()
        {
            if (queuedMaskStrokeCommits.Count == 0)
            {
                return;
            }

            isMaskStrokeCommitQueueScheduled = true;
            maskStrokeCommitQueueTimer.Stop();
            maskStrokeCommitQueueTimer.Interval = TimeSpan.FromMilliseconds(WpfMaskEditStateService.CommitQueueQuietMilliseconds);
            maskStrokeCommitQueueTimer.Start();

            // The FBO preview is already the visible source after MouseUp. Let the
            // quiet idle timer process CPU MaskData/history work so repeated strokes
            // and immediate wheel/pan input are not forced to wait behind a commit.
        }

        private void MaskStrokeCommitQueueTimer_Tick(object sender, EventArgs e)
        {
            ProcessQueuedMaskStrokeCommits();
        }

        private void ProcessQueuedMaskStrokeCommits()
        {
            if (queuedMaskStrokeCommits.Count == 0)
            {
                maskStrokeCommitQueueTimer.Stop();
                isMaskStrokeCommitQueueScheduled = false;
                return;
            }

            if (!CanProcessQueuedMaskStrokeCommitNow())
            {
                // Keep queued CPU materialization out of the active painting loop.
                // It will be flushed when the user leaves the mask tool, saves, or
                // invokes undo/redo; until then the GPU/FBO preview is authoritative.
                maskStrokeCommitQueueTimer.Stop();
                isMaskStrokeCommitQueueScheduled = true;
                return;
            }

            WpfQueuedMaskStrokeCommit command = queuedMaskStrokeCommits.Dequeue();
            ApplyQueuedMaskStrokeCommit(command);
            if (queuedMaskStrokeCommits.Count == 0)
            {
                maskStrokeCommitQueueTimer.Stop();
                isMaskStrokeCommitQueueScheduled = false;
                maskStrokeCommitQueueTimer.Interval = TimeSpan.FromMilliseconds(WpfMaskEditStateService.CommitQueueQuietMilliseconds);
            }
            else
            {
                maskStrokeCommitQueueTimer.Interval = TimeSpan.FromMilliseconds(WpfMaskEditStateService.CommitQueueDrainIntervalMilliseconds);
            }
        }

        private void FlushQueuedMaskStrokeCommits()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new Action(FlushQueuedMaskStrokeCommits));
                return;
            }

            isMaskStrokeToolEndFlushScheduled = false;
            isMaskStrokeCommitQueueScheduled = false;
            maskStrokeCommitQueueTimer.Stop();
            if (queuedMaskStrokeCommits.Count == 0)
            {
                return;
            }

            Stopwatch flushStopwatch = Stopwatch.StartNew();
            BeginMaskStrokeCommitBatchFlush();
            try
            {
                while (queuedMaskStrokeCommits.Count > 0)
                {
                    ApplyQueuedMaskStrokeCommit(queuedMaskStrokeCommits.Dequeue());
                }
            }
            finally
            {
                CompleteMaskStrokeCommitBatchFlush(flushStopwatch.Elapsed.TotalMilliseconds);
            }
        }

        private bool ScheduleQueuedMaskStrokeCommitsAfterToolEnd()
        {
            if (queuedMaskStrokeCommits.Count == 0)
            {
                return false;
            }

            // Tool selection should be visually immediate. Keep the FBO preview on
            // screen and materialize MaskData/history after the input event returns.
            isMaskStrokeCommitQueueScheduled = false;
            isMaskStrokeToolEndFlushScheduled = true;
            maskStrokeToolEndFlushRequestedTicks = Stopwatch.GetTimestamp();
            maskStrokeCommitQueueTimer.Stop();
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!isMaskStrokeToolEndFlushScheduled)
                {
                    return;
                }

                bool previousSuppressSelection = suppressMaskStrokeCommitSelection;
                suppressMaskStrokeCommitSelection = true;
                try
                {
                    FlushQueuedMaskStrokeCommits();
                }
                finally
                {
                    suppressMaskStrokeCommitSelection = previousSuppressSelection;
                }
            }), DispatcherPriority.Background);
            return true;
        }

        private void ClearQueuedMaskStrokeCommits()
        {
            queuedMaskStrokeCommits.Clear();
            pendingMaskStrokeCommitCount = 0;
            isMaskStrokeCommitQueueScheduled = false;
            isMaskStrokeToolEndFlushScheduled = false;
            maskStrokeToolEndFlushRequestedTicks = 0;
            maskStrokeCommitQueueTimer.Stop();
        }

        private bool CanProcessQueuedMaskStrokeCommitNow()
            => maskEditStateService.CanProcessQueuedStrokeCommit(activeMaskStrokeInProgress, activeAnnotationTool);

        private bool HasPendingMaskStrokeCommitWork()
            => pendingMaskStrokeCommitCount > 0
                || queuedMaskStrokeCommits.Count > 0
                || isMaskStrokeCommitQueueScheduled
                || isMaskStrokeToolEndFlushScheduled;

        private void ApplyQueuedMaskStrokeCommit(WpfQueuedMaskStrokeCommit command)
        {
            Stopwatch applyStopwatch = Stopwatch.StartNew();
            try
            {
                if (command == null
                    || !string.Equals(command.ImagePath, activeImagePath, StringComparison.Ordinal)
                    || command.ImageSize != activeImageSize)
                {
                    return;
                }

                long phaseTicks = Stopwatch.GetTimestamp();
                IReadOnlyList<MaskStrokeHistoryDeltaDraft> historyDrafts = maskStrokeHistoryDraftService.BuildDrafts(
                    command,
                    manualSegments);
                double historyMilliseconds = ElapsedMilliseconds(phaseTicks);
                phaseTicks = Stopwatch.GetTimestamp();
                bool changed = ApplyQueuedMaskStrokeCommitCore(
                    command,
                    out List<int> changedSegmentIndices,
                    out bool needsFullObjectRefresh);
                double maskMilliseconds = ElapsedMilliseconds(phaseTicks);
                if (!changed)
                {
                    // Dense strokes often end with an already-painted sample. Do not let
                    // that no-op overwrite the last successful commit message or clear the
                    // FBO while later queued samples may still be visible.
                    if (queuedMaskStrokeCommits.Count == 0)
                    {
                        ScheduleMaskStrokePreviewCommitSwap();
                    }

                    if (!manualSegments.Any(segment => segment?.IsRasterMask == true))
                    {
                        SetModelStatus(FormattableString.Invariant($"\uB9C8\uC2A4\uD06C \uD3B8\uC9D1 \uBC18\uC601: \uBCC0\uACBD \uC5C6\uC74C / Queue {applyStopwatch.Elapsed.TotalMilliseconds:F1}ms"));
                    }

                    SetMaskCommitAutomationSignal(command, changed: false, applyStopwatch.Elapsed.TotalMilliseconds);
                    return;
                }

                WpfAnnotationHistorySnapshot beforeChange = maskStrokeHistoryDraftService.CreateSnapshot(
                    command,
                    historyDrafts,
                    manualSegments);
                phaseTicks = Stopwatch.GetTimestamp();
                PushAnnotationHistorySnapshot(beforeChange);
                if (isMaskStrokeCommitBatchFlushActive)
                {
                    TrackBatchedMaskStrokeCommit(
                        command,
                        changedSegmentIndices,
                        needsFullObjectRefresh,
                        command.HasActiveCandidates);
                    SetMaskCommitAutomationSignal(command, changed: true, applyStopwatch.Elapsed.TotalMilliseconds);
                    return;
                }

                bool objectRowsRefreshed = TryRefreshMaskStrokeObjectReviewRows(
                    changedSegmentIndices,
                    needsFullObjectRefresh);
                MainCanvasViewModel?.MarkNextRenderDiagnostics(FormattableString.Invariant($"mask queued commit #{command.Sequence} changed segments={changedSegmentIndices.Count}"));
                bool canvasOverlayQueued = TryRefreshMaskStrokeCanvasOverlays(
                    changedSegmentIndices,
                    needsFullObjectRefresh,
                    refreshAfterInput: true);
                double viewMilliseconds = ElapsedMilliseconds(phaseTicks);

                SetModelStatus(FormattableString.Invariant($"\uB9C8\uC2A4\uD06C \uD3B8\uC9D1 \uBC18\uC601: \uC138\uADF8\uBA3C\uD2B8 \uAC1D\uCCB4 {manualSegments.Count}\uAC1C / Queue {applyStopwatch.Elapsed.TotalMilliseconds:F1}ms (undo {historyMilliseconds:F1} / mask {maskMilliseconds:F1} / view {viewMilliseconds:F1})"));
                SetMaskCommitAutomationSignal(command, changed: true, applyStopwatch.Elapsed.TotalMilliseconds);
                QueueMaskStrokePresentationRefresh(
                    changedSegmentIndices,
                    needsFullObjectRefresh,
                    command.HasActiveCandidates,
                    objectRowsRefreshed,
                    canvasOverlayQueued);
            }
            finally
            {
                pendingMaskStrokeCommitCount = Math.Max(0, pendingMaskStrokeCommitCount - 1);
            }
        }

        private static double ElapsedMilliseconds(long startTicks)
            => (Stopwatch.GetTimestamp() - startTicks) * 1000D / Stopwatch.Frequency;

        private void BeginMaskStrokeCommitBatchFlush()
        {
            // Tool-end flush can contain many strokes. Keep undo/history per stroke,
            // but defer object-list and OpenGL overlay presentation to one final pass.
            isMaskStrokeCommitBatchFlushActive = true;
            batchedMaskStrokeSegmentIndices.Clear();
            batchedMaskStrokeNeedsFullObjectRefresh = false;
            batchedMaskStrokeHasActiveCandidates = false;
            batchedMaskStrokeChangedCommitCount = 0;
            batchedMaskStrokeMaxWaitMilliseconds = 0D;
        }

        private void CompleteMaskStrokeCommitBatchFlush(double flushMilliseconds)
        {
            bool hadChangedCommits = batchedMaskStrokeChangedCommitCount > 0;
            IReadOnlyList<int> changedSegmentIndices = batchedMaskStrokeSegmentIndices
                .OrderBy(index => index)
                .ToList();
            bool needsFullObjectRefresh = batchedMaskStrokeNeedsFullObjectRefresh;
            bool hasActiveCandidates = batchedMaskStrokeHasActiveCandidates;
            int changedCommitCount = batchedMaskStrokeChangedCommitCount;
            double maxWaitMilliseconds = batchedMaskStrokeMaxWaitMilliseconds;
            double toolEndWaitMilliseconds = GetMaskStrokeToolEndFlushWaitMilliseconds();

            isMaskStrokeCommitBatchFlushActive = false;
            maskStrokeToolEndFlushRequestedTicks = 0;
            batchedMaskStrokeSegmentIndices.Clear();
            batchedMaskStrokeNeedsFullObjectRefresh = false;
            batchedMaskStrokeHasActiveCandidates = false;
            batchedMaskStrokeChangedCommitCount = 0;
            batchedMaskStrokeMaxWaitMilliseconds = 0D;

            if (!hadChangedCommits)
            {
                ScheduleMaskStrokePreviewCommitSwap();
                return;
            }

            long phaseTicks = Stopwatch.GetTimestamp();
            bool objectRowsRefreshed = TryRefreshMaskStrokeObjectReviewRows(
                changedSegmentIndices,
                needsFullObjectRefresh);
            MainCanvasViewModel?.MarkNextRenderDiagnostics(FormattableString.Invariant($"mask batch commit changed strokes={changedCommitCount} segments={changedSegmentIndices.Count}"));
            bool canvasOverlayQueued = TryRefreshMaskStrokeCanvasOverlays(
                changedSegmentIndices,
                needsFullObjectRefresh,
                refreshAfterInput: true);
            double viewMilliseconds = ElapsedMilliseconds(phaseTicks);

            SetModelStatus(FormattableString.Invariant($"\uB9C8\uC2A4\uD06C \uD3B8\uC9D1 \uBC18\uC601: \uBC30\uCE58 {changedCommitCount}\uAC1C / Queue {flushMilliseconds:F1}ms (view {viewMilliseconds:F1})"));
            StatusBarViewModel?.SetModelStatusAutomationText(FormattableString.Invariant(
                $"mask batch commit changed=True count={changedCommitCount} waitMs={maxWaitMilliseconds:F1} toolEndWaitMs={toolEndWaitMilliseconds:F1} queueMs={flushMilliseconds:F1}"));
            QueueMaskStrokePresentationRefresh(
                changedSegmentIndices,
                needsFullObjectRefresh,
                hasActiveCandidates,
                objectRowsRefreshed,
                canvasOverlayQueued);
        }

        private void TrackBatchedMaskStrokeCommit(
            WpfQueuedMaskStrokeCommit command,
            IEnumerable<int> changedSegmentIndices,
            bool needsFullObjectRefresh,
            bool hasActiveCandidates)
        {
            batchedMaskStrokeChangedCommitCount++;
            batchedMaskStrokeNeedsFullObjectRefresh |= needsFullObjectRefresh;
            batchedMaskStrokeHasActiveCandidates |= hasActiveCandidates;
            batchedMaskStrokeMaxWaitMilliseconds = Math.Max(
                batchedMaskStrokeMaxWaitMilliseconds,
                GetQueuedMaskStrokeWaitMilliseconds(command));
            foreach (int segmentIndex in changedSegmentIndices ?? Array.Empty<int>())
            {
                batchedMaskStrokeSegmentIndices.Add(segmentIndex);
            }
        }

        private void SetMaskCommitAutomationSignal(
            WpfQueuedMaskStrokeCommit command,
            bool changed,
            double milliseconds)
        {
            StatusBarViewModel?.SetModelStatusAutomationText(FormattableString.Invariant(
                $"mask commit #{command?.Sequence ?? 0} changed={changed} waitMs={GetQueuedMaskStrokeWaitMilliseconds(command):F1} queueMs={milliseconds:F1}"));
        }

        private static double GetQueuedMaskStrokeWaitMilliseconds(WpfQueuedMaskStrokeCommit command)
        {
            if (command == null || command.CreatedTicks <= 0)
            {
                return 0D;
            }

            return (Stopwatch.GetTimestamp() - command.CreatedTicks) * 1000D / Stopwatch.Frequency;
        }

        private double GetMaskStrokeToolEndFlushWaitMilliseconds()
        {
            if (maskStrokeToolEndFlushRequestedTicks <= 0)
            {
                return 0D;
            }

            return (Stopwatch.GetTimestamp() - maskStrokeToolEndFlushRequestedTicks) * 1000D / Stopwatch.Frequency;
        }

        private bool ApplyQueuedMaskStrokeCommitCore(
            WpfQueuedMaskStrokeCommit command,
            out List<int> changedSegmentIndices,
            out bool needsFullObjectRefresh)
        {
            changedSegmentIndices = new List<int>();
            needsFullObjectRefresh = false;
            if (command.Centers.Count == 0)
            {
                return false;
            }

            if (command.Tool == WpfAnnotationTool.Brush)
            {
                bool changed = maskAnnotationService.Paint(
                    manualSegments,
                    command.Centers,
                    command.Radius,
                    command.ImageSize,
                    command.ClassItem,
                    out LabelingSegmentationObject changedSegment,
                    out _);
                TrackQueuedMaskStrokeSegment(changedSegment, changedSegmentIndices, ref needsFullObjectRefresh);
                return changed;
            }

            if (command.Tool == WpfAnnotationTool.Eraser)
            {
                int segmentCountBeforeErase = manualSegments.Count;
                bool changed = maskAnnotationService.Erase(
                    manualSegments,
                    command.Centers,
                    command.Radius,
                    command.ImageSize,
                    out _,
                    out IReadOnlyList<LabelingSegmentationObject> changedSegments);
                TrackQueuedMaskStrokeSegments(changedSegments, changedSegmentIndices, ref needsFullObjectRefresh);
                needsFullObjectRefresh |= manualSegments.Count != segmentCountBeforeErase;
                return changed;
            }

            return false;
        }

        private void QueueMaskStrokePresentationRefresh(
            IReadOnlyList<int> changedSegmentIndices,
            bool needsFullObjectRefresh,
            bool hasActiveCandidates,
            bool objectRowsRefreshed,
            bool canvasOverlayQueued)
        {
            // MouseUp owns only the CPU mask commit and the minimal canvas state swap.
            // Side-list refreshes and status persistence follow at idle priority so
            // wheel/pan input queued right after release keeps the viewport responsive.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                // Rapid mask painting must not force the right panel away from the
                // active guide/tool tab; only the existing object-review view model
                // receives incremental row data.
                if (!objectRowsRefreshed
                    && !TryRefreshMaskStrokeObjectReviewRows(changedSegmentIndices, needsFullObjectRefresh))
                {
                    RefreshObjectList();
                }

                RefreshCanvasWorkflowContext();
                QueueActiveImageQueueStatusRefresh(hasActiveCandidates);

                if (!canvasOverlayQueued)
                {
                    MainCanvasViewModel?.ClearMaskStrokePreview(refresh: false, clearTexture: false);
                    if (!TryRefreshMaskStrokeCanvasOverlays(changedSegmentIndices, needsFullObjectRefresh))
                    {
                        RefreshPolygonOverlays();
                    }
                }
                else if (!objectRowsRefreshed)
                {
                    // Full row refresh can change the selected object. Requeue the mask
                    // overlay after input so the cyan selection handles match the side list.
                    TryRefreshMaskStrokeCanvasOverlays(
                        changedSegmentIndices,
                        needsFullObjectRefresh,
                        refreshAfterInput: true);
                }

                ScheduleMaskStrokePreviewCommitSwap();
            }), DispatcherPriority.ApplicationIdle);
        }

        private void ScheduleMaskStrokePreviewCommitSwap()
        {
            if (IsMaskAnnotationToolActive())
            {
                return;
            }

            // The FBO preview already matches the committed mask. Keep it alive briefly
            // after MouseUp so immediate wheel/pan input is not forced through the first
            // committed-mask texture upload frame.
            maskStrokePreviewCommitSwapTimer.Stop();
            maskStrokePreviewCommitSwapTimer.Start();
        }

        private void CancelMaskStrokePreviewCommitSwap()
        {
            maskStrokePreviewCommitSwapTimer.Stop();
        }

        private void MaskStrokePreviewCommitSwapTimer_Tick(object sender, EventArgs e)
        {
            maskStrokePreviewCommitSwapTimer.Stop();
            if (activeMaskStrokeInProgress)
            {
                return;
            }

            if (maskEditStateService.ShouldDelayPreviewSwap(activeMaskStrokeInProgress, HasPendingMaskStrokeCommitWork(), activeAnnotationTool))
            {
                maskStrokePreviewCommitSwapTimer.Start();
                return;
            }

            MainCanvasViewModel?.ClearMaskStrokePreview(
                refresh: true,
                clearTexture: false,
                refreshAfterInput: true);
        }

        private IReadOnlyList<System.Drawing.Point> AppendMaskStrokeCommitCenters(IEnumerable<System.Drawing.Point> centers)
            => activeMaskStrokeCommitSession.Append(centers, activeImageSize);

        private void ResetMaskStrokeCommitBuffer()
        {
            activeMaskStrokeCommitSession.Reset();
            lastMaskStrokeStatusUpdateTicks = 0;
        }

        private void TrackQueuedMaskStrokeSegment(
            LabelingSegmentationObject segment,
            ICollection<int> changedSegmentIndices,
            ref bool needsFullObjectRefresh)
        {
            if (segment == null)
            {
                return;
            }

            int index = manualSegments.IndexOf(segment);
            if (index >= 0)
            {
                changedSegmentIndices?.Add(index);
                return;
            }

            needsFullObjectRefresh = true;
        }

        private void TrackQueuedMaskStrokeSegments(
            IEnumerable<LabelingSegmentationObject> segments,
            ICollection<int> changedSegmentIndices,
            ref bool needsFullObjectRefresh)
        {
            foreach (LabelingSegmentationObject segment in segments ?? Array.Empty<LabelingSegmentationObject>())
            {
                TrackQueuedMaskStrokeSegment(segment, changedSegmentIndices, ref needsFullObjectRefresh);
            }
        }

        private static CClassItem CloneClassItemForQueuedMaskCommit(CClassItem source)
            => source == null
                ? null
                : new CClassItem
                {
                    Text = source.Text ?? string.Empty,
                    DrawColor = source.DrawColor
                };
    }
}
