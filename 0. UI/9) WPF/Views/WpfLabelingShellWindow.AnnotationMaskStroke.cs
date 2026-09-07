using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Threading;
using OpenVisionLab.ImageCanvas.Canvas;
using System.ComponentModel;

namespace MvcVisionSystem
{
    // Responsibility group: mask stroke input, preview, and deferred commit.
    // These members remain WPF Window adapters; independent policy belongs in services.
    public partial class WpfLabelingShellWindow
    {
        #region AnnotationMaskStrokeCommit
        private void CompleteMaskAnnotationStroke()
        {
            Stopwatch strokeStopwatch = Stopwatch.StartNew();
            bool strokeWasActive = activeMaskStrokeInProgress;
            bool commitQueued = strokeWasActive && EnqueueMaskAnnotationStrokeCommit();
            if (!commitQueued)
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
            LabelClass classItem = tool == WpfAnnotationTool.Brush
                ? CloneClassItemForQueuedMaskCommit(
                    classCatalogWorkflowService.EnsureClassItem(global.Data, className))
                : null;
            var command = new QueuedMaskStrokeCommit(
                ++queuedMaskStrokeCommitSequence,
                activeImagePath,
                activeImageSize,
                activeMaskStrokeCommitSession.DetachCenters(),
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
            MarkMaskStrokeAnnotationsDirty(activeMaskStrokeActionName);
            RefreshAnnotationHistoryToolState();
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
            shellTimers.MaskStrokeCommitQueue.Stop();
            shellTimers.MaskStrokeCommitQueue.Interval = TimeSpan.FromMilliseconds(MaskEditStateService.CommitQueueQuietMilliseconds);
            shellTimers.MaskStrokeCommitQueue.Start();

            // The FBO preview is already the visible source after MouseUp. Let the
            // quiet idle timer process CPU MaskData/history work so repeated strokes
            // and immediate wheel/pan input are not forced to wait behind a commit.
        }

        private void MaskStrokeCommitQueueTimer_Tick(object sender, EventArgs e)
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            ProcessQueuedMaskStrokeCommits();
        }

        private void ProcessQueuedMaskStrokeCommits()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            if (queuedMaskStrokeCommits.Count == 0)
            {
                shellTimers.MaskStrokeCommitQueue.Stop();
                isMaskStrokeCommitQueueScheduled = false;
                return;
            }

            if (!CanProcessQueuedMaskStrokeCommitNow())
            {
                // Keep queued CPU materialization out of the active painting loop.
                // MaskData, history, object-review rows, and overlay texture work
                // wait for save/tool-end flush so MouseUp leaves the UI thread clear.
                shellTimers.MaskStrokeCommitQueue.Stop();
                isMaskStrokeCommitQueueScheduled = true;
                return;
            }

            QueuedMaskStrokeCommit command = queuedMaskStrokeCommits.Dequeue();
            ApplyQueuedMaskStrokeCommit(command);
            if (queuedMaskStrokeCommits.Count == 0)
            {
                shellTimers.MaskStrokeCommitQueue.Stop();
                isMaskStrokeCommitQueueScheduled = false;
                shellTimers.MaskStrokeCommitQueue.Interval = TimeSpan.FromMilliseconds(MaskEditStateService.CommitQueueQuietMilliseconds);
            }
            else
            {
                shellTimers.MaskStrokeCommitQueue.Interval = TimeSpan.FromMilliseconds(MaskEditStateService.CommitQueueDrainIntervalMilliseconds);
            }
        }

        private void FlushQueuedMaskStrokeCommits()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new Action(FlushQueuedMaskStrokeCommits));
                return;
            }

            isMaskStrokeToolEndFlushScheduled = false;
            isMaskStrokeCommitQueueScheduled = false;
            shellTimers.MaskStrokeCommitQueue.Stop();
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
            shellTimers.MaskStrokeCommitQueue.Stop();
            Dispatcher.BeginInvoke(new Action(ApplyScheduledMaskStrokeToolEndFlush), DispatcherPriority.Background);
            return true;
        }

        private void ApplyScheduledMaskStrokeToolEndFlush()
        {
            if (isApplicationCloseApproved || !isMaskStrokeToolEndFlushScheduled)
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
        }

        private void ClearQueuedMaskStrokeCommits()
        {
            queuedMaskStrokeCommits.Clear();
            pendingMaskStrokeCommitCount = 0;
            isMaskStrokeCommitQueueScheduled = false;
            isMaskStrokeToolEndFlushScheduled = false;
            maskStrokeToolEndFlushRequestedTicks = 0;
            shellTimers.MaskStrokeCommitQueue.Stop();
            RefreshAnnotationHistoryToolState();
        }

        private bool CanProcessQueuedMaskStrokeCommitNow()
            => maskEditStateService.CanProcessQueuedStrokeCommit(activeMaskStrokeInProgress, activeAnnotationTool);

        private bool HasPendingMaskStrokeCommitWork()
            => pendingMaskStrokeCommitCount > 0
                || queuedMaskStrokeCommits.Count > 0
                || isMaskStrokeCommitQueueScheduled
                || isMaskStrokeToolEndFlushScheduled;

        private void ApplyQueuedMaskStrokeCommit(QueuedMaskStrokeCommit command)
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
                RefreshAnnotationHistoryToolState();
                if (!HasPendingMaskStrokeCommitWork()
                    && annotationDirtyState.IsDirty)
                {
                    ScheduleCrashRecoveryJournalWrite();
                }
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
            RefreshDeferredMaskStrokeDirtyPresentation();
            QueueMaskStrokePresentationRefresh(
                changedSegmentIndices,
                needsFullObjectRefresh,
                hasActiveCandidates,
                objectRowsRefreshed,
                canvasOverlayQueued);
        }

        private void TrackBatchedMaskStrokeCommit(
            QueuedMaskStrokeCommit command,
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
            QueuedMaskStrokeCommit command,
            bool changed,
            double milliseconds)
        {
            StatusBarViewModel?.SetModelStatusAutomationText(FormattableString.Invariant(
                $"mask commit #{command?.Sequence ?? 0} changed={changed} waitMs={GetQueuedMaskStrokeWaitMilliseconds(command):F1} queueMs={milliseconds:F1}"));
        }

        private static double GetQueuedMaskStrokeWaitMilliseconds(QueuedMaskStrokeCommit command)
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
            QueuedMaskStrokeCommit command,
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
                    out _,
                    CanEditManualSegment);
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
                    out IReadOnlyList<LabelingSegmentationObject> changedSegments,
                    CanEditManualSegment);
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
            Dispatcher.BeginInvoke(
                new Action(() => ApplyScheduledMaskStrokePresentationRefresh(
                    changedSegmentIndices,
                    needsFullObjectRefresh,
                    hasActiveCandidates,
                    objectRowsRefreshed,
                    canvasOverlayQueued)),
                DispatcherPriority.ApplicationIdle);
        }

        private void ApplyScheduledMaskStrokePresentationRefresh(
            IReadOnlyList<int> changedSegmentIndices,
            bool needsFullObjectRefresh,
            bool hasActiveCandidates,
            bool objectRowsRefreshed,
            bool canvasOverlayQueued)
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

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
            shellTimers.MaskStrokePreviewCommitSwap.Stop();
            shellTimers.MaskStrokePreviewCommitSwap.Start();
        }

        private void CancelMaskStrokePreviewCommitSwap()
        {
            shellTimers.MaskStrokePreviewCommitSwap.Stop();
        }

        private void MaskStrokePreviewCommitSwapTimer_Tick(object sender, EventArgs e)
        {
            shellTimers.MaskStrokePreviewCommitSwap.Stop();
            if (isApplicationCloseApproved)
            {
                return;
            }

            if (activeMaskStrokeInProgress)
            {
                return;
            }

            if (maskEditStateService.ShouldDelayPreviewSwap(activeMaskStrokeInProgress, HasPendingMaskStrokeCommitWork(), activeAnnotationTool))
            {
                shellTimers.MaskStrokePreviewCommitSwap.Start();
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

        private static LabelClass CloneClassItemForQueuedMaskCommit(LabelClass source)
            => source == null
                ? null
                : new LabelClass
                {
                    Text = source.Text ?? string.Empty,
                    DrawColor = source.DrawColor
                };
        #endregion

        #region AnnotationMaskStrokeInput
        // Stroke geometry is previewed by the GPU/FBO; status text is throttled so WPF
        // bindings do not compete with high-frequency brush MouseMove input.
        private static readonly long MaskStrokeStatusUpdateIntervalTicks = Math.Max(1L, Stopwatch.Frequency / 8);

        private void ApplyMaskAnnotationStroke(CanvasImagePointEventArgs e, bool resetStroke)
        {
            if (e == null || activeImageSize.IsEmpty)
            {
                return;
            }

            if (e.Button == CanvasPointerButton.Right)
            {
                CompleteMaskAnnotationStroke();
                CancelMaskStrokePreviewCommitSwap();
                lastMaskStrokePoint = null;
                MainCanvasViewModel?.ClearMaskStrokePreview();
                SetYoloCommandStatus("마스크 스트로크를 초기화했습니다. 다시 드래그해 편집을 이어가세요.", isBusy: false);
                return;
            }

            if (e.Button != CanvasPointerButton.Left)
            {
                return;
            }

            int radius = GetMaskBrushRadius();
            IReadOnlyList<System.Drawing.Point> centers = maskAnnotationService.BuildStrokeCenters(
                resetStroke ? null : lastMaskStrokePoint,
                e.ImagePoint,
                radius);
            lastMaskStrokePoint = e.ImagePoint;

            string actionName = activeAnnotationTool == WpfAnnotationTool.Brush ? "마스크 칠하기" : "마스크 지우기";
            if (resetStroke && activeMaskStrokeInProgress)
            {
                CompleteMaskAnnotationStroke();
            }

            if (!activeMaskStrokeInProgress)
            {
                CancelMaskStrokePreviewCommitSwap();
                // Match the old Viewer2D brush flow: MouseMove only feeds the GPU/FBO
                // edit preview, while MouseUp enqueues CPU MaskData/history work between strokes.
                activeMaskStrokeInProgress = true;
                activeMaskStrokeActionName = actionName;
                activeMaskStrokeSegmentIndices.Clear();
                activeMaskStrokeNeedsFullObjectRefresh = false;
                activeMaskStrokeCommitSession.Begin(
                    radius,
                    activeAnnotationTool,
                    FirstNonEmpty(GetSelectedClassName(), "Defect"));
                MainCanvasViewModel?.BeginMaskStrokePreview(
                    activeImageSize,
                    GetMaskStrokePreviewColor(activeAnnotationTool == WpfAnnotationTool.Eraser),
                    activeAnnotationTool == WpfAnnotationTool.Eraser);
            }

            IReadOnlyList<System.Drawing.Point> previewCenters = AppendMaskStrokeCommitCenters(centers);
            if (previewCenters.Count == 0)
            {
                return;
            }

            MainCanvasViewModel?.AddMaskStrokePreview(
                previewCenters,
                radius,
                GetMaskStrokePreviewColor(activeAnnotationTool == WpfAnnotationTool.Eraser),
                activeAnnotationTool == WpfAnnotationTool.Eraser);
            TryUpdateMaskStrokePreviewStatus(force: false);
        }

        private void TryUpdateMaskStrokePreviewStatus(bool force)
        {
            long now = Stopwatch.GetTimestamp();
            if (!force
                && lastMaskStrokeStatusUpdateTicks != 0
                && now - lastMaskStrokeStatusUpdateTicks < MaskStrokeStatusUpdateIntervalTicks)
            {
                return;
            }

            lastMaskStrokeStatusUpdateTicks = now;
            string action = activeAnnotationTool == WpfAnnotationTool.Brush ? "마스크 칠하기 미리보기" : "마스크 지우기 미리보기";
            SetModelStatus($"{action}: 스트로크 {activeMaskStrokeCommitSession.Count}점");
        }

        // Stroke preview colors, tool predicates, and object-row refresh are
        // part of the same interactive mask-input state as the pointer handler.
        private bool TryRefreshMaskStrokeObjectReviewRows()
            => TryRefreshMaskStrokeObjectReviewRows(activeMaskStrokeSegmentIndices, activeMaskStrokeNeedsFullObjectRefresh);

        private bool TryRefreshMaskStrokeObjectReviewRows(
            IEnumerable<int> segmentIndices,
            bool needsFullObjectRefresh)
        {
            IReadOnlyList<int> orderedSegmentIndices = (segmentIndices ?? Array.Empty<int>())
                .Distinct()
                .OrderBy(index => index)
                .ToList();
            if (needsFullObjectRefresh
                || orderedSegmentIndices.Count == 0
                || ObjectReviewViewModel == null)
            {
                return false;
            }

            string summary = ObjectReviewPresenter.BuildSummary(
                manualRois.Count + manualSegments.Count + confirmedDetectionCandidates.Count);
            bool selectChangedMask = orderedSegmentIndices.Count == 1
                && !activeMaskStrokeInProgress;
            foreach (int segmentIndex in orderedSegmentIndices)
            {
                if (!TryRefreshManualSegmentObjectReviewRow(segmentIndex, summary, selectChangedMask))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsMaskAnnotationToolActive()
            => maskEditStateService.IsMaskPaintTool(activeAnnotationTool);

        private bool ShouldSelectCommittedMaskAfterStroke()
            => !suppressMaskStrokeCommitSelection
                && maskEditStateService.ShouldSelectCommittedMask(activeAnnotationTool);

        private int GetMaskBrushRadius()
        {
            int brushSize = LearningWorkflowViewModel?.BrushSize ?? MaskAnnotationService.DefaultBrushRadius * 2;
            return Math.Clamp((int)Math.Round(brushSize / 2D), 1, 128);
        }

        private System.Drawing.Color GetMaskCursorPreviewColor(bool isEraser)
        {
            if (isEraser)
            {
                return System.Drawing.Color.FromArgb(245, 158, 11);
            }

            string className = FirstNonEmpty(GetSelectedClassName(), "Defect");
            LabelClass existing = global.Data.ClassNamedList?
                .FirstOrDefault(item => string.Equals(item?.Text, className, StringComparison.OrdinalIgnoreCase));
            return existing?.DrawColor ?? System.Drawing.Color.FromArgb(44, 210, 110);
        }

        private System.Drawing.Color GetMaskStrokePreviewColor(bool isEraser)
        {
            System.Drawing.Color color = GetMaskCursorPreviewColor(isEraser);
            if (isEraser)
            {
                return color;
            }

            int alpha = (int)Math.Round(Math.Clamp(LearningWorkflowViewModel?.MaskOpacity ?? 0.66D, 0.1D, 1.0D) * 255D);
            return System.Drawing.Color.FromArgb(alpha, color.R, color.G, color.B);
        }

        // Brush-size commands update the same shared mask-input radius and
        // Canvas toolbar projection, so they stay with this interactive owner.
        private const int CanvasBrushSizeStep = 2;

        private void ExecuteDecreaseBrushSizeCommand()
            => AdjustBrushSize(-CanvasBrushSizeStep);

        private void ExecuteIncreaseBrushSizeCommand()
            => AdjustBrushSize(CanvasBrushSizeStep);

        private void AdjustBrushSize(int delta)
        {
            if (LearningWorkflowViewModel == null)
            {
                return;
            }

            LearningWorkflowViewModel.BrushSize = Math.Clamp(
                LearningWorkflowViewModel.BrushSize + delta,
                2,
                64);
            SyncCanvasBrushSizeFromWorkflow();
        }

        private void SyncCanvasBrushSizeFromWorkflow()
        {
            CanvasPanelViewModel?.SetBrushSize(LearningWorkflowViewModel?.BrushSize ?? 12);
        }

        private void LearningWorkflowViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e?.PropertyName, nameof(WpfLearningWorkflowPanelViewModel.BrushSize), StringComparison.Ordinal))
            {
                SyncCanvasBrushSizeFromWorkflow();
            }
        }
        #endregion

    }
}
