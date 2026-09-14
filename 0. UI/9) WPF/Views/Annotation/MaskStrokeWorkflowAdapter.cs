using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.Canvas;
using OpenVisionLab.ImageCanvas.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;

namespace MvcVisionSystem
{
    internal enum MaskStrokeDispatchPriority
    {
        Background,
        ApplicationIdle
    }

    // Owns interactive mask-stroke state, deferred CPU materialization, and preview timing.
    // Canvas, Window, persistence, and collection mutation stay behind explicit callbacks.
    internal sealed class MaskStrokeWorkflowAdapter : IDisposable
    {
        private readonly MaskStrokeWorkflowAdapterContext context;
        private readonly MaskEditStateService maskEditStateService = new MaskEditStateService();
        private readonly MaskStrokeHistoryDraftService maskStrokeHistoryDraftService = new MaskStrokeHistoryDraftService();
        private readonly HashSet<int> activeMaskStrokeSegmentIndices = new HashSet<int>();
        private readonly MaskStrokeCommitSession activeMaskStrokeCommitSession = new MaskStrokeCommitSession();
        private readonly Queue<QueuedMaskStrokeCommit> queuedMaskStrokeCommits = new Queue<QueuedMaskStrokeCommit>();
        private readonly HashSet<int> batchedMaskStrokeSegmentIndices = new HashSet<int>();
        private bool disposed;
        private System.Drawing.Point? lastMaskStrokePoint;
        private long lastMaskStrokeStatusUpdateTicks;
        private bool activeMaskStrokeInProgress;
        private bool isMaskStrokeCommitQueueScheduled;
        private bool isMaskStrokeToolEndFlushScheduled;
        private long maskStrokeToolEndFlushRequestedTicks;
        private bool suppressMaskStrokeCommitSelection;
        private int pendingMaskStrokeCommitCount;
        private int queuedMaskStrokeCommitSequence;
        private string activeMaskStrokeActionName = string.Empty;
        private bool activeMaskStrokeNeedsFullObjectRefresh;
        private bool isMaskStrokeCommitBatchFlushActive;
        private bool batchedMaskStrokeNeedsFullObjectRefresh;
        private bool batchedMaskStrokeHasActiveCandidates;
        private int batchedMaskStrokeChangedCommitCount;
        private double batchedMaskStrokeMaxWaitMilliseconds;

        internal MaskStrokeWorkflowAdapter(MaskStrokeWorkflowAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            if (context.ManualSegments == null) throw new ArgumentNullException(nameof(context.ManualSegments));
            if (context.MaskAnnotationService == null) throw new ArgumentNullException(nameof(context.MaskAnnotationService));
            if (context.Timers == null) throw new ArgumentNullException(nameof(context.Timers));
            if (context.Dispatcher == null) throw new ArgumentNullException(nameof(context.Dispatcher));
        }

        private List<LabelingSegmentationObject> manualSegments => context.ManualSegments;
        private string activeImagePath => context.ActiveImagePathProvider?.Invoke() ?? string.Empty;
        private Size activeImageSize => context.ActiveImageSizeProvider?.Invoke() ?? Size.Empty;
        private WpfAnnotationTool activeAnnotationTool => context.ActiveAnnotationToolProvider?.Invoke() ?? WpfAnnotationTool.Select;
        private RoiImageCanvasViewModel MainCanvasViewModel => context.MainCanvasViewModelProvider?.Invoke();
        private WpfCanvasPanelViewModel CanvasPanelViewModel => context.CanvasPanelViewModelProvider?.Invoke();
        private WpfLearningWorkflowPanelViewModel LearningWorkflowViewModel => context.LearningWorkflowViewModelProvider?.Invoke();
        private WpfObjectReviewPanelViewModel ObjectReviewViewModel => context.ObjectReviewViewModelProvider?.Invoke();
        private WpfStatusBarPanelViewModel StatusBarViewModel => context.StatusBarViewModelProvider?.Invoke();
        private MaskAnnotationService maskAnnotationService => context.MaskAnnotationService;
        private MaskStrokeTimerSetFacade shellTimers => context.Timers;
        private MaskStrokeDispatcherFacade Dispatcher => context.Dispatcher;

        internal IReadOnlyCollection<int> ActiveMaskStrokeSegmentIndices => activeMaskStrokeSegmentIndices;
        internal bool ActiveMaskStrokeNeedsFullObjectRefresh => activeMaskStrokeNeedsFullObjectRefresh;
        internal bool IsActiveMaskStroke => activeMaskStrokeInProgress;

        internal void SetLastMaskStrokePoint(Point? point)
            => lastMaskStrokePoint = point;

        internal void SetActiveMaskStrokeInProgress(bool value)
            => activeMaskStrokeInProgress = value;

        internal void SetActiveMaskStrokeActionName(string value)
            => activeMaskStrokeActionName = value ?? string.Empty;

        internal void ClearActiveMaskStrokeSegmentIndices()
            => activeMaskStrokeSegmentIndices.Clear();

        internal void SetActiveMaskStrokeNeedsFullObjectRefresh(bool value)
            => activeMaskStrokeNeedsFullObjectRefresh = value;

        internal bool ShouldPreserveMaskPreviewDuringToolSwitch()
            => maskEditStateService.ShouldPreservePreviewDuringToolSwitch(HasPendingMaskStrokeCommitWork());

        internal void ResetForImageChange()
        {
            ClearQueuedMaskStrokeCommits();
            CancelMaskStrokePreviewCommitSwap();
            lastMaskStrokePoint = null;
            activeMaskStrokeInProgress = false;
            activeMaskStrokeActionName = string.Empty;
            activeMaskStrokeSegmentIndices.Clear();
            ResetMaskStrokeCommitBuffer();
            activeMaskStrokeNeedsFullObjectRefresh = false;
        }

        internal void ResetAfterHistoryRestore()
        {
            lastMaskStrokePoint = null;
            activeMaskStrokeInProgress = false;
            activeMaskStrokeActionName = string.Empty;
            activeMaskStrokeSegmentIndices.Clear();
            activeMaskStrokeNeedsFullObjectRefresh = false;
            ResetMaskStrokeCommitBuffer();
            CancelMaskStrokePreviewCommitSwap();
        }

        internal bool HasPendingMaskStrokeUndoWork()
            => pendingMaskStrokeCommitCount > 0 || queuedMaskStrokeCommits.Count > 0;

        internal string GetPendingMaskStrokeUndoActionName()
            => queuedMaskStrokeCommits.Count > 0 ? queuedMaskStrokeCommits.Peek().ActionName : string.Empty;

        private bool IsApplicationCloseApproved()
            => context.IsApplicationCloseApproved?.Invoke() == true;

        private bool IsAnnotationDirty()
            => context.IsAnnotationDirty?.Invoke() == true;

        private bool HasPendingDetectionCandidates()
            => (context.PendingCandidateCountProvider?.Invoke() ?? 0) > 0;

        private int BuildObjectReviewSummary()
            => context.ObjectReviewSummaryProvider?.Invoke() ?? 0;

        private string GetSelectedClassName()
            => context.SelectedClassNameProvider?.Invoke() ?? string.Empty;

        private LabelClass EnsureClassItem(string className)
            => context.EnsureClassItem?.Invoke(className);

        private bool CanEditManualSegment(LabelingSegmentationObject segment)
            => context.CanEditManualSegment?.Invoke(segment) ?? true;

        private void MarkMaskStrokeAnnotationsDirty(string reason)
            => context.MarkMaskStrokeAnnotationsDirty?.Invoke(reason);

        private void RefreshAnnotationHistoryToolState()
            => context.RefreshAnnotationHistoryToolState?.Invoke();

        private void RefreshDeferredMaskStrokeDirtyPresentation()
            => context.RefreshDeferredMaskStrokeDirtyPresentation?.Invoke();

        private void PushAnnotationHistorySnapshot(WpfAnnotationHistorySnapshot snapshot)
            => context.PushAnnotationHistorySnapshot?.Invoke(snapshot);

        private bool TryRefreshMaskStrokeCanvasOverlays(
            IEnumerable<int> segmentIndices,
            bool needsFullObjectRefresh,
            bool refreshAfterInput = false)
            => context.TryRefreshMaskStrokeCanvasOverlays?.Invoke(segmentIndices, needsFullObjectRefresh, refreshAfterInput) == true;

        private bool TryRefreshManualSegmentObjectReviewRow(int segmentIndex, string summary, bool select)
            => context.TryRefreshManualSegmentObjectReviewRow?.Invoke(segmentIndex, summary, select) == true;

        private void QueueActiveImageQueueStatusRefresh(bool hasActiveCandidates)
            => context.QueueActiveImageQueueStatusRefresh?.Invoke(hasActiveCandidates);

        private void RefreshCanvasWorkflowContext()
            => context.RefreshCanvasWorkflowContext?.Invoke();

        private void RefreshObjectList()
            => context.RefreshObjectList?.Invoke();

        private void ScheduleCrashRecoveryJournalWrite()
            => context.ScheduleCrashRecoveryJournalWrite?.Invoke();

        private void SetModelStatus(string text)
            => context.SetModelStatus?.Invoke(text);

        private void SetYoloCommandStatus(string text, bool isBusy)
            => context.SetYoloCommandStatus?.Invoke(text, isBusy);

        private void RefreshPolygonOverlays()
            => context.RefreshPolygonOverlays?.Invoke();

        private void SetModelStatusAutomationText(string text)
            => context.SetModelStatusAutomationText?.Invoke(text);

        private void ScheduleOnDispatcher(Action action, MaskStrokeDispatchPriority priority)
            => Dispatcher.BeginInvoke(action, priority);

        private static string FirstNonEmpty(params string[] values)
            => values?.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            shellTimers.MaskStrokeCommitQueue.Stop();
            shellTimers.MaskStrokePreviewCommitSwap.Stop();
            queuedMaskStrokeCommits.Clear();
            activeMaskStrokeCommitSession.Reset();
            activeMaskStrokeSegmentIndices.Clear();
            batchedMaskStrokeSegmentIndices.Clear();
        }
        #region AnnotationMaskStrokeCommit
        internal void CompleteMaskAnnotationStroke()
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
                    EnsureClassItem(className))
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
                HasPendingDetectionCandidates());

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

        internal void MaskStrokeCommitQueueTimer_Tick(object sender, EventArgs e)
        {
            if (IsApplicationCloseApproved())
            {
                return;
            }

            ProcessQueuedMaskStrokeCommits();
        }

        private void ProcessQueuedMaskStrokeCommits()
        {
            if (IsApplicationCloseApproved())
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

        internal void FlushQueuedMaskStrokeCommits()
        {
            if (IsApplicationCloseApproved())
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

        internal bool ScheduleQueuedMaskStrokeCommitsAfterToolEnd()
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
            Dispatcher.BeginInvoke(new Action(ApplyScheduledMaskStrokeToolEndFlush), MaskStrokeDispatchPriority.Background);
            return true;
        }

        private void ApplyScheduledMaskStrokeToolEndFlush()
        {
            if (IsApplicationCloseApproved() || !isMaskStrokeToolEndFlushScheduled)
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

        internal void ClearQueuedMaskStrokeCommits()
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

        internal bool HasPendingMaskStrokeCommitWork()
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
                    && IsAnnotationDirty())
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
                MaskStrokeDispatchPriority.ApplicationIdle);
        }

        private void ApplyScheduledMaskStrokePresentationRefresh(
            IReadOnlyList<int> changedSegmentIndices,
            bool needsFullObjectRefresh,
            bool hasActiveCandidates,
            bool objectRowsRefreshed,
            bool canvasOverlayQueued)
        {
            if (IsApplicationCloseApproved())
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

        internal void CancelMaskStrokePreviewCommitSwap()
        {
            shellTimers.MaskStrokePreviewCommitSwap.Stop();
        }

        internal void MaskStrokePreviewCommitSwapTimer_Tick(object sender, EventArgs e)
        {
            shellTimers.MaskStrokePreviewCommitSwap.Stop();
            if (IsApplicationCloseApproved())
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

        internal void ResetMaskStrokeCommitBuffer()
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

        internal void ApplyMaskAnnotationStroke(CanvasImagePointEventArgs e, bool resetStroke)
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
                BuildObjectReviewSummary());
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

        internal bool IsMaskAnnotationToolActive()
            => maskEditStateService.IsMaskPaintTool(activeAnnotationTool);

        internal bool ShouldSelectCommittedMaskAfterStroke()
            => !suppressMaskStrokeCommitSelection
                && maskEditStateService.ShouldSelectCommittedMask(activeAnnotationTool);

        internal int GetMaskBrushRadius()
        {
            int brushSize = LearningWorkflowViewModel?.BrushSize ?? CanvasBrushSizePresentationService.DefaultSize;
            return Math.Clamp((int)Math.Round(brushSize / 2D), 1, 128);
        }

        internal System.Drawing.Color GetMaskCursorPreviewColor(bool isEraser)
        {
            if (isEraser)
            {
                return System.Drawing.Color.FromArgb(245, 158, 11);
            }

            string className = FirstNonEmpty(GetSelectedClassName(), "Defect");
            LabelClass existing = context.FindClassItem?.Invoke(className);
            return existing?.DrawColor ?? System.Drawing.Color.FromArgb(44, 210, 110);
        }

        internal System.Drawing.Color GetMaskStrokePreviewColor(bool isEraser)
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
        private const int CanvasBrushSizeStep = CanvasBrushSizePresentationService.Step;

        internal void ExecuteDecreaseBrushSizeCommand()
            => AdjustBrushSize(-CanvasBrushSizeStep);

        internal void ExecuteIncreaseBrushSizeCommand()
            => AdjustBrushSize(CanvasBrushSizeStep);

        private void AdjustBrushSize(int delta)
        {
            if (LearningWorkflowViewModel == null)
            {
                return;
            }

            LearningWorkflowViewModel.BrushSize += delta;
            SyncCanvasBrushSizeFromWorkflow();
        }

        internal void SyncCanvasBrushSizeFromWorkflow()
        {
            CanvasPanelViewModel?.SetBrushSize(
                LearningWorkflowViewModel?.BrushSize ?? CanvasBrushSizePresentationService.DefaultSize);
        }

        internal void LearningWorkflowViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e?.PropertyName, nameof(WpfLearningWorkflowPanelViewModel.BrushSize), StringComparison.Ordinal))
            {
                SyncCanvasBrushSizeFromWorkflow();
            }
        }
        #endregion
    }

    internal sealed class MaskStrokeWorkflowAdapterContext
    {
        internal List<LabelingSegmentationObject> ManualSegments { get; init; }
        internal Func<string> ActiveImagePathProvider { get; init; }
        internal Func<Size> ActiveImageSizeProvider { get; init; }
        internal Func<WpfAnnotationTool> ActiveAnnotationToolProvider { get; init; }
        internal Func<string> SelectedClassNameProvider { get; init; }
        internal Func<string, LabelClass> EnsureClassItem { get; init; }
        internal Func<string, LabelClass> FindClassItem { get; init; }
        internal Func<int> ObjectReviewSummaryProvider { get; init; }
        internal Func<int> ManualRoiCountProvider { get; init; }
        internal Func<int> ConfirmedCandidateCountProvider { get; init; }
        internal Func<int> PendingCandidateCountProvider { get; init; }
        internal Func<bool> IsApplicationCloseApproved { get; init; }
        internal Func<bool> IsAnnotationDirty { get; init; }
        internal Func<LabelingSegmentationObject, bool> CanEditManualSegment { get; init; }
        internal MaskAnnotationService MaskAnnotationService { get; init; }
        internal Func<RoiImageCanvasViewModel> MainCanvasViewModelProvider { get; init; }
        internal Func<WpfCanvasPanelViewModel> CanvasPanelViewModelProvider { get; init; }
        internal Func<WpfLearningWorkflowPanelViewModel> LearningWorkflowViewModelProvider { get; init; }
        internal Func<WpfObjectReviewPanelViewModel> ObjectReviewViewModelProvider { get; init; }
        internal Func<WpfStatusBarPanelViewModel> StatusBarViewModelProvider { get; init; }
        internal MaskStrokeTimerSetFacade Timers { get; init; }
        internal MaskStrokeDispatcherFacade Dispatcher { get; init; }
        internal Action<string> SetModelStatus { get; init; }
        internal Action<string, bool> SetYoloCommandStatus { get; init; }
        internal Action RefreshPolygonOverlays { get; init; }
        internal Action<string> MarkMaskStrokeAnnotationsDirty { get; init; }
        internal Action RefreshAnnotationHistoryToolState { get; init; }
        internal Action RefreshDeferredMaskStrokeDirtyPresentation { get; init; }
        internal Action<WpfAnnotationHistorySnapshot> PushAnnotationHistorySnapshot { get; init; }
        internal Func<IEnumerable<int>, bool, bool, bool> TryRefreshMaskStrokeCanvasOverlays { get; init; }
        internal Func<int, string, bool, bool> TryRefreshManualSegmentObjectReviewRow { get; init; }
        internal Action<bool> QueueActiveImageQueueStatusRefresh { get; init; }
        internal Action RefreshCanvasWorkflowContext { get; init; }
        internal Action RefreshObjectList { get; init; }
        internal Action ScheduleCrashRecoveryJournalWrite { get; init; }
        internal Action<string> SetModelStatusAutomationText { get; init; }
        internal Action<bool> SetLearningWorkflowBrushSize { get; init; }
    }

    internal sealed class MaskStrokeDispatcherFacade
    {
        private readonly Func<bool> checkAccess;
        private readonly Action<Action> invoke;
        private readonly Action<Action, MaskStrokeDispatchPriority> beginInvoke;

        internal MaskStrokeDispatcherFacade(Func<bool> checkAccess, Action<Action> invoke, Action<Action, MaskStrokeDispatchPriority> beginInvoke)
        {
            this.checkAccess = checkAccess ?? throw new ArgumentNullException(nameof(checkAccess));
            this.invoke = invoke ?? throw new ArgumentNullException(nameof(invoke));
            this.beginInvoke = beginInvoke ?? throw new ArgumentNullException(nameof(beginInvoke));
        }

        internal bool CheckAccess() => checkAccess();
        internal void Invoke(Action action) => invoke(action);
        internal void BeginInvoke(Action action, MaskStrokeDispatchPriority priority) => beginInvoke(action, priority);
    }

    internal sealed class MaskStrokeTimerFacade
    {
        private readonly Action stop;
        private readonly Action start;
        private readonly Action<TimeSpan> setInterval;
        private TimeSpan interval;

        internal MaskStrokeTimerFacade(Action stop, Action start, Action<TimeSpan> setInterval, TimeSpan initialInterval)
        {
            this.stop = stop ?? throw new ArgumentNullException(nameof(stop));
            this.start = start ?? throw new ArgumentNullException(nameof(start));
            this.setInterval = setInterval ?? throw new ArgumentNullException(nameof(setInterval));
            interval = initialInterval;
        }

        internal TimeSpan Interval
        {
            get => interval;
            set
            {
                interval = value;
                setInterval(value);
            }
        }

        internal void Stop() => stop();
        internal void Start() => start();
    }

    internal sealed class MaskStrokeTimerSetFacade
    {
        internal MaskStrokeTimerFacade MaskStrokeCommitQueue { get; }
        internal MaskStrokeTimerFacade MaskStrokePreviewCommitSwap { get; }

        internal MaskStrokeTimerSetFacade(MaskStrokeTimerFacade maskStrokeCommitQueue, MaskStrokeTimerFacade maskStrokePreviewCommitSwap)
        {
            MaskStrokeCommitQueue = maskStrokeCommitQueue ?? throw new ArgumentNullException(nameof(maskStrokeCommitQueue));
            MaskStrokePreviewCommitSwap = maskStrokePreviewCommitSwap ?? throw new ArgumentNullException(nameof(maskStrokePreviewCommitSwap));
        }
    }
}
