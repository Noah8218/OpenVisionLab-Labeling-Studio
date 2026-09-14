using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.Canvas;
using OpenVisionLab.ImageCanvas.ViewModels;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace MvcVisionSystem
{
    // Owns pending segmentation edit sessions and their collection mutations.
    // Geometry remains in the existing segmentation services; WPF presentation
    // enters through explicit ViewModel/status callbacks.
    internal sealed class SegmentationEditAdapter
    {
        private readonly SegmentationEditAdapterContext context;
        private readonly SegmentationHoleService segmentationHoleService;
        private readonly SegmentationSplitService segmentationSplitService;
        private readonly SegmentationRemoveUnderlyingService segmentationRemoveUnderlyingService;
        private readonly PolygonAnnotationService holePolygonAnnotationService;
        private WpfSegmentationRemoveUnderlyingPlan pendingSegmentationRemoveUnderlyingPlan;
        private LabelingSegmentationObject pendingSegmentationSplitSource;
        private int pendingSegmentationSplitSourceIndex = -1;
        private WpfSegmentationSplitOrientation? pendingSegmentationSplitOrientation;
        private LabelingSegmentationObject pendingSegmentationHoleSource;
        private int pendingSegmentationHoleSourceIndex = -1;
        private WpfSegmentationHoleEditMode? pendingSegmentationHoleEditMode;

        internal SegmentationEditAdapter(
            SegmentationHoleService segmentationHoleService,
            SegmentationSplitService segmentationSplitService,
            SegmentationRemoveUnderlyingService segmentationRemoveUnderlyingService,
            PolygonAnnotationService holePolygonAnnotationService,
            SegmentationEditAdapterContext context)
        {
            this.segmentationHoleService = segmentationHoleService ?? throw new ArgumentNullException(nameof(segmentationHoleService));
            this.segmentationSplitService = segmentationSplitService ?? throw new ArgumentNullException(nameof(segmentationSplitService));
            this.segmentationRemoveUnderlyingService = segmentationRemoveUnderlyingService ?? throw new ArgumentNullException(nameof(segmentationRemoveUnderlyingService));
            this.holePolygonAnnotationService = holePolygonAnnotationService ?? throw new ArgumentNullException(nameof(holePolygonAnnotationService));
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            if (context.ManualSegments == null) throw new ArgumentNullException(nameof(context.ManualSegments));
            if (context.ObjectMetadataStateService == null) throw new ArgumentNullException(nameof(context.ObjectMetadataStateService));
        }

        private List<LabelingSegmentationObject> manualSegments => context.ManualSegments;
        private int manualRoiCount => context.ManualRoiCountProvider?.Invoke() ?? 0;
        private Size activeImageSize => context.ActiveImageSizeProvider?.Invoke() ?? Size.Empty;
        private WpfAnnotationTool activeAnnotationTool => context.ActiveAnnotationToolProvider?.Invoke() ?? WpfAnnotationTool.Select;
        private WpfCanvasPanelViewModel CanvasPanelViewModel => context.CanvasPanelViewModelProvider?.Invoke();
        private RoiImageCanvasViewModel MainCanvasViewModel => context.MainCanvasViewModelProvider?.Invoke();
        private WpfObjectReviewPanelViewModel ObjectReviewViewModel => context.ObjectReviewViewModelProvider?.Invoke();
        private SmartMaskPromptSessionService smartMaskPromptSession => context.SmartMaskPromptSession;
        private SmartMaskWorkflowService smartMaskWorkflowService => context.SmartMaskWorkflowService;
        private ObjectMetadataStateService objectMetadataStateService => context.ObjectMetadataStateService;
        private IReadOnlyList<YoloWorkerSmokeCandidate> pendingDetectionCandidates
            => context.PendingDetectionCandidatesProvider?.Invoke() ?? Array.Empty<YoloWorkerSmokeCandidate>();

        internal SegmentationHoleService SegmentationHoleService => segmentationHoleService;
        internal SegmentationSplitService SegmentationSplitService => segmentationSplitService;
        internal SegmentationRemoveUnderlyingService SegmentationRemoveUnderlyingService => segmentationRemoveUnderlyingService;
        internal PolygonAnnotationService HolePolygonAnnotationService => holePolygonAnnotationService;
        internal WpfSegmentationRemoveUnderlyingPlan PendingSegmentationRemoveUnderlyingPlan => pendingSegmentationRemoveUnderlyingPlan;
        internal LabelingSegmentationObject PendingSegmentationSplitSource => pendingSegmentationSplitSource;
        internal int PendingSegmentationSplitSourceIndex => pendingSegmentationSplitSourceIndex;
        internal WpfSegmentationSplitOrientation? PendingSegmentationSplitOrientation => pendingSegmentationSplitOrientation;
        internal LabelingSegmentationObject PendingSegmentationHoleSource => pendingSegmentationHoleSource;
        internal int PendingSegmentationHoleSourceIndex => pendingSegmentationHoleSourceIndex;
        internal WpfSegmentationHoleEditMode? PendingSegmentationHoleEditMode => pendingSegmentationHoleEditMode;
        internal bool HasPendingSegmentationRemoveUnderlyingPlan => pendingSegmentationRemoveUnderlyingPlan != null;
        internal bool HasPendingSegmentationSplit => pendingSegmentationSplitOrientation.HasValue;
        internal bool HasPendingSegmentationHoleEdit => pendingSegmentationHoleEditMode.HasValue;

        private static string FirstNonEmpty(params string[] values)
            => values?.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

        private bool TryGetSelectedObjectReviewItem(out WpfObjectReviewItemRef item)
        {
            item = context.SelectedObjectReviewItemProvider?.Invoke();
            return item != null;
        }

        private bool CanMutateSelectedObject(
            WpfObjectReviewItemRef item,
            bool requireVisible,
            out string error)
        {
            error = context.MutationErrorProvider?.Invoke(item, requireVisible) ?? string.Empty;
            return string.IsNullOrWhiteSpace(error);
        }

        private void CancelPendingIntelligentScissors(bool updateStatus)
            => context.CancelPendingIntelligentScissors?.Invoke(updateStatus);

        private void CompleteMaskAnnotationStroke()
            => context.CompleteMaskAnnotationStroke?.Invoke();

        private void FlushQueuedMaskStrokeCommits()
            => context.FlushQueuedMaskStrokeCommits?.Invoke();

        private void SelectAnnotationTool(WpfAnnotationTool tool)
            => context.SelectAnnotationTool?.Invoke(tool);

        private void RefreshPolygonOverlays()
            => context.RefreshPolygonOverlays?.Invoke();

        private void ClearMaskStrokePreview()
            => context.ClearMaskStrokePreview?.Invoke();

        private void RefreshObjectListWithSelection(WpfObjectReviewItemRef selection)
            => context.RefreshObjectListWithSelection?.Invoke(selection);

        private void QueueActiveImageQueueStatusRefresh(bool hasActiveCandidates)
            => context.QueueActiveImageQueueStatusRefresh?.Invoke(hasActiveCandidates);

        private void SetYoloCommandStatus(string text, bool isBusy)
            => context.SetYoloCommandStatus?.Invoke(text, isBusy);

        private void AppendLog(string message)
            => context.AppendLog?.Invoke(message);

        private WpfAnnotationHistorySnapshot CaptureAnnotationHistory(string actionName)
            => context.CaptureAnnotationHistory?.Invoke(actionName);

        private void PushAnnotationHistorySnapshot(WpfAnnotationHistorySnapshot snapshot)
            => context.PushAnnotationHistorySnapshot?.Invoke(snapshot);

        private void CancelObjectGroupSelection(bool updateStatus)
            => context.CancelObjectGroupSelection?.Invoke(updateStatus);


        #region SegmentationHoleCommands
        internal void ExecuteBeginAddSegmentationHoleCommand()
            => BeginPendingSegmentationHoleEdit(WpfSegmentationHoleEditMode.Add);

        internal void ExecuteBeginRemoveSegmentationHoleCommand()
            => BeginPendingSegmentationHoleEdit(WpfSegmentationHoleEditMode.Remove);

        internal void ExecuteCancelSegmentationHoleEditCommand()
            => CancelPendingSegmentationHoleEdit(updateStatus: true);

        internal void BeginPendingSegmentationHoleEdit(WpfSegmentationHoleEditMode mode)
        {
            CancelPendingIntelligentScissors(updateStatus: false);
            CompleteMaskAnnotationStroke();
            FlushQueuedMaskStrokeCommits();
            if (smartMaskPromptSession.HasSession || smartMaskWorkflowService.IsRunning)
            {
                const string smartMaskError = "\uC2A4\uB9C8\uD2B8 \uB9C8\uC2A4\uD06C \uD6C4\uBCF4\uB97C \uD655\uC815\uD558\uAC70\uB098 \uCDE8\uC18C\uD55C \uB4A4 \uAD6C\uBA4D\uC744 \uD3B8\uC9D1\uD558\uC138\uC694.";
                SetYoloCommandStatus(smartMaskError, isBusy: false);
                AppendLog(smartMaskError);
                return;
            }

            if (!TryGetSelectedObjectReviewItem(out WpfObjectReviewItemRef selected)
                || selected.Source != WpfObjectReviewSource.ManualSegment
                || selected.Index < 0
                || selected.Index >= manualSegments.Count
                || activeImageSize.IsEmpty)
            {
                const string selectionError = "\uAD6C\uBA4D\uC744 \uD3B8\uC9D1\uD560 \uD3F4\uB9AC\uACE4 \uB610\uB294 \uB9C8\uC2A4\uD06C \uAC1D\uCCB4\uB97C \uD558\uB098 \uC120\uD0DD\uD558\uC138\uC694.";
                SetYoloCommandStatus(selectionError, isBusy: false);
                AppendLog(selectionError);
                return;
            }

            if (!CanMutateSelectedObject(selected, requireVisible: true, out string stateError))
            {
                SetYoloCommandStatus(stateError, isBusy: false);
                AppendLog(stateError);
                return;
            }

            SelectAnnotationTool(WpfAnnotationTool.Select);
            pendingSegmentationHoleSourceIndex = selected.Index;
            pendingSegmentationHoleSource = manualSegments[selected.Index];
            pendingSegmentationHoleEditMode = mode;
            holePolygonAnnotationService.Reset();
            ObjectReviewViewModel?.SetHoleEditPending(mode);
            MainCanvasViewModel.IsImagePointInputMode = true;
            MainCanvasViewModel.ImageViewer.SetViewMode(CanvasInteractionMode.None);
            RefreshPolygonOverlays();

            string status = mode == WpfSegmentationHoleEditMode.Add
                ? "\uAD6C\uBA4D \uADF8\uB9AC\uAE30: \uAC1D\uCCB4 \uC548\uCABD\uC5D0 \uC810\uC744 \uD074\uB9AD\uD558\uACE0 \uCCAB \uC810 \uB610\uB294 \uB354\uBE14\uD074\uB9AD\uC73C\uB85C \uC644\uB8CC\uD558\uC138\uC694."
                : "\uAD6C\uBA4D \uCC44\uC6B0\uAE30: \uC678\uBD80 \uBC30\uACBD\uACFC \uC5F0\uACB0\uB418\uC9C0 \uC54A\uC740 \uB0B4\uBD80 \uAD6C\uBA4D\uC744 \uD074\uB9AD\uD558\uC138\uC694.";
            SetYoloCommandStatus(status, isBusy: false);
            AppendLog(status);
        }

        internal bool TryApplyPendingSegmentationHoleEdit(CanvasImagePointEventArgs e)
        {
            if (!pendingSegmentationHoleEditMode.HasValue)
            {
                return false;
            }

            if (e?.Button == CanvasPointerButton.Right)
            {
                CancelPendingSegmentationHoleEdit(updateStatus: true);
                return true;
            }

            if (e?.Button != CanvasPointerButton.Left)
            {
                return true;
            }

            if (!TryResolvePendingSegmentationHoleSource(out int sourceIndex, out LabelingSegmentationObject source))
            {
                CancelPendingSegmentationHoleEdit(updateStatus: false);
                const string staleSelectionError = "\uC120\uD0DD\uD55C \uAC1D\uCCB4\uAC00 \uBCC0\uACBD\uB418\uC5B4 \uAD6C\uBA4D \uD3B8\uC9D1\uC744 \uCDE8\uC18C\uD588\uC2B5\uB2C8\uB2E4.";
                SetYoloCommandStatus(staleSelectionError, isBusy: false);
                AppendLog(staleSelectionError);
                return true;
            }

            if (pendingSegmentationHoleEditMode == WpfSegmentationHoleEditMode.Remove)
            {
                if (!segmentationHoleService.TryRemoveHole(
                    source,
                    e.ImagePoint,
                    activeImageSize,
                    out LabelingSegmentationObject filled,
                    out string error))
                {
                    SetYoloCommandStatus(error, isBusy: false);
                    AppendLog($"Segment hole fill skipped: {error}");
                    return true;
                }

                ApplySegmentationHoleEdit(sourceIndex, filled, "\uB0B4\uBD80 \uAD6C\uBA4D \uCC44\uC6B0\uAE30");
                return true;
            }

            if (e.Clicks > 1 && holePolygonAnnotationService.Points.Count >= 3)
            {
                CompletePendingSegmentationHoleAddition(sourceIndex, source);
                return true;
            }

            if (!holePolygonAnnotationService.TryAddPoint(e.ImagePoint, activeImageSize, out bool closed))
            {
                return true;
            }

            RefreshPolygonOverlays();
            if (closed)
            {
                CompletePendingSegmentationHoleAddition(sourceIndex, source);
                return true;
            }

            SetYoloCommandStatus(
                $"\uAD6C\uBA4D \uB2E4\uAC01\uD615: {holePolygonAnnotationService.Points.Count}\uC810 / \uCCAB \uC810 \uB610\uB294 \uB354\uBE14\uD074\uB9AD\uC73C\uB85C \uC644\uB8CC",
                isBusy: false);
            return true;
        }

        internal void CompletePendingSegmentationHoleAddition(
            int sourceIndex,
            LabelingSegmentationObject source)
        {
            if (!segmentationHoleService.TryAddHole(
                source,
                holePolygonAnnotationService.Points,
                activeImageSize,
                out LabelingSegmentationObject edited,
                out string error))
            {
                holePolygonAnnotationService.Reset();
                RefreshPolygonOverlays();
                SetYoloCommandStatus(error, isBusy: false);
                AppendLog($"Segment hole add skipped: {error}");
                return;
            }

            ApplySegmentationHoleEdit(sourceIndex, edited, "\uB0B4\uBD80 \uAD6C\uBA4D \uCD94\uAC00");
        }

        internal void ApplySegmentationHoleEdit(
            int sourceIndex,
            LabelingSegmentationObject edited,
            string actionName)
        {
            WpfAnnotationHistorySnapshot beforeChange = CaptureAnnotationHistory(actionName);
            manualSegments[sourceIndex] = edited;
            PushAnnotationHistorySnapshot(beforeChange);
            CancelPendingSegmentationHoleEdit(updateStatus: false);
            MainCanvasViewModel?.ClearMaskStrokePreview(refresh: false, clearTexture: true);
            RefreshPolygonOverlays();
            RefreshObjectListWithSelection(WpfObjectReviewItemRef.ManualSegment(sourceIndex));
            QueueActiveImageQueueStatusRefresh(hasActiveCandidates: pendingDetectionCandidates.Count > 0);

            string status = $"{actionName}: {edited.ClassName} / {edited.LastStructuralOperation}";
            SetYoloCommandStatus(status, isBusy: false);
            AppendLog(status);
        }

        internal bool TryResolvePendingSegmentationHoleSource(
            out int sourceIndex,
            out LabelingSegmentationObject source)
        {
            sourceIndex = pendingSegmentationHoleSourceIndex;
            source = pendingSegmentationHoleSource;
            return source != null
                && sourceIndex >= 0
                && sourceIndex < manualSegments.Count
                && ReferenceEquals(manualSegments[sourceIndex], source);
        }

        internal void CancelPendingSegmentationHoleEdit(bool updateStatus)
        {
            bool wasPending = pendingSegmentationHoleEditMode.HasValue;
            pendingSegmentationHoleSource = null;
            pendingSegmentationHoleSourceIndex = -1;
            pendingSegmentationHoleEditMode = null;
            holePolygonAnnotationService.Reset();
            ObjectReviewViewModel?.SetHoleEditPending(null);
            if (MainCanvasViewModel != null)
            {
                MainCanvasViewModel.IsImagePointInputMode =
                    activeAnnotationTool == WpfAnnotationTool.Polygon
                    || activeAnnotationTool == WpfAnnotationTool.Brush
                    || activeAnnotationTool == WpfAnnotationTool.Eraser
                    || (activeAnnotationTool == WpfAnnotationTool.Select
                        && ObjectReviewViewModel?.IsSelectedSource(WpfObjectReviewSource.ManualSegment) == true);
            }

            RefreshPolygonOverlays();
            if (wasPending && updateStatus)
            {
                const string status = "\uB0B4\uBD80 \uAD6C\uBA4D \uD3B8\uC9D1\uC744 \uCDE8\uC18C\uD588\uC2B5\uB2C8\uB2E4.";
                SetYoloCommandStatus(status, isBusy: false);
                AppendLog(status);
            }
        }
        #endregion

        #region SegmentationRemoveUnderlyingCommands
        internal void ExecutePreviewSegmentationRemoveUnderlyingCommand()
        {
            CancelPendingIntelligentScissors(updateStatus: false);
            CompleteMaskAnnotationStroke();
            FlushQueuedMaskStrokeCommits();
            string error = "\uB4A4\uCABD \uAC1D\uCCB4\uC640 \uACB9\uCE68\uC744 \uBD84\uC11D\uD560 \uC138\uADF8\uBA3C\uD2B8\uB97C \uD558\uB098 \uC120\uD0DD\uD558\uC138\uC694.";
            WpfSegmentationRemoveUnderlyingPlan plan = null;
            bool hasSelection = TryGetSelectedObjectReviewItem(out WpfObjectReviewItemRef selected)
                && selected.Source == WpfObjectReviewSource.ManualSegment;
            if (!hasSelection
                || !segmentationRemoveUnderlyingService.TryAnalyze(
                    manualSegments,
                    selected.Index,
                    activeImageSize,
                    out plan,
                    out error))
            {
                CancelPendingSegmentationRemoveUnderlying(updateStatus: false);
                SetYoloCommandStatus(error, isBusy: false);
                if (!string.IsNullOrWhiteSpace(error))
                {
                    AppendLog($"Remove-underlying analysis skipped: {error}");
                }

                return;
            }

            if (!CanMutateSelectedObject(selected, requireVisible: true, out string stateError))
            {
                CancelPendingSegmentationRemoveUnderlying(updateStatus: false);
                SetYoloCommandStatus(stateError, isBusy: false);
                AppendLog(stateError);
                return;
            }

            pendingSegmentationRemoveUnderlyingPlan = plan;
            string affected = string.Join(
                ", ",
                plan.Changes
                    .OrderBy(change => change.SourceIndex)
                    .Select(change => $"#{change.SourceIndex + 1}"));
            string warning = FormattableString.Invariant(
                $"\uC601\uD5A5 {plan.Changes.Count}\uAC1C({affected}) \u00B7 \uC81C\uAC70 {plan.RemovedPixelCount:N0}px \u00B7 \uC644\uC804 \uC0AD\uC81C {plan.RemovedObjectCount}\uAC1C. '\uD655\uC778 \uD6C4 \uC81C\uAC70'\uB97C \uB204\uB974\uBA74 \uB4A4\uCABD geometry\uAC00 \uBCC0\uACBD\uB429\uB2C8\uB2E4.");
            ObjectReviewViewModel?.SetRemoveUnderlyingPreview(true, warning);
            RefreshPolygonOverlays();
            SetYoloCommandStatus(warning, isBusy: false);
            AppendLog($"Remove-underlying preview: {warning}");
        }

        internal void ExecuteApplySegmentationRemoveUnderlyingCommand()
        {
            WpfSegmentationRemoveUnderlyingPlan pending = pendingSegmentationRemoveUnderlyingPlan;
            if (pending == null)
            {
                return;
            }

            string error = string.Empty;
            WpfSegmentationRemoveUnderlyingPlan current = null;
            bool currentSelectionMatches = pending.SelectedIndex >= 0
                && pending.SelectedIndex < manualSegments.Count
                && ReferenceEquals(manualSegments[pending.SelectedIndex], pending.SelectedSource);
            bool currentAnalysisMatches = currentSelectionMatches
                && segmentationRemoveUnderlyingService.TryAnalyze(
                    manualSegments,
                    pending.SelectedIndex,
                    activeImageSize,
                    out current,
                    out error)
                && string.Equals(current.Signature, pending.Signature, StringComparison.Ordinal);
            if (!currentAnalysisMatches)
            {
                CancelPendingSegmentationRemoveUnderlying(updateStatus: false);
                const string stale = "\uB77C\uBCA8 geometry\uB098 \uD45C\uC2DC \uC21C\uC11C\uAC00 \uBCC0\uACBD\uB418\uC5B4 \uAE30\uC874 \uC601\uD5A5 \uBD84\uC11D\uC744 \uC801\uC6A9\uD558\uC9C0 \uC54A\uC558\uC2B5\uB2C8\uB2E4. \uB2E4\uC2DC \uACB9\uCE68\uC744 \uBD84\uC11D\uD558\uC138\uC694.";
                SetYoloCommandStatus(stale, isBusy: false);
                AppendLog($"Remove-underlying stale preview rejected: {FirstNonEmpty(error, "selection or geometry changed")}");
                return;
            }

            WpfAnnotationHistorySnapshot beforeChange = CaptureAnnotationHistory("\uB4A4\uCABD \uACB9\uCE68 \uC81C\uAC70");
            foreach (WpfSegmentationRemoveUnderlyingChange change in current.Changes
                .OrderByDescending(change => change.SourceIndex))
            {
                if (change.Replacement == null)
                {
                    manualSegments.RemoveAt(change.SourceIndex);
                }
                else
                {
                    manualSegments[change.SourceIndex] = change.Replacement;
                }
            }

            int selectedIndex = manualSegments.IndexOf(current.SelectedSource);
            PushAnnotationHistorySnapshot(beforeChange);
            CancelPendingSegmentationRemoveUnderlying(updateStatus: false);
            MainCanvasViewModel?.ClearMaskStrokePreview(refresh: false, clearTexture: true);
            RefreshPolygonOverlays();
            RefreshObjectListWithSelection(selectedIndex >= 0
                ? WpfObjectReviewItemRef.ManualSegment(selectedIndex)
                : null);
            QueueActiveImageQueueStatusRefresh(hasActiveCandidates: pendingDetectionCandidates.Count > 0);

            string status = FormattableString.Invariant(
                $"\uB4A4\uCABD \uACB9\uCE68 \uC81C\uAC70: \uC601\uD5A5 {current.Changes.Count}\uAC1C / {current.RemovedPixelCount:N0}px / \uC644\uC804 \uC0AD\uC81C {current.RemovedObjectCount}\uAC1C");
            SetYoloCommandStatus(status, isBusy: false);
            AppendLog(status);
        }

        internal void ExecuteCancelSegmentationRemoveUnderlyingCommand()
            => CancelPendingSegmentationRemoveUnderlying(updateStatus: true);

        internal void CancelPendingSegmentationRemoveUnderlying(bool updateStatus)
        {
            bool wasPending = pendingSegmentationRemoveUnderlyingPlan != null;
            pendingSegmentationRemoveUnderlyingPlan = null;
            ObjectReviewViewModel?.SetRemoveUnderlyingPreview(false);
            if (wasPending)
            {
                RefreshPolygonOverlays();
            }

            if (wasPending && updateStatus)
            {
                const string status = "\uB4A4\uCABD \uACB9\uCE68 \uC81C\uAC70 \uBBF8\uB9AC\uBCF4\uAE30\uB97C \uCDE8\uC18C\uD588\uC2B5\uB2C8\uB2E4.";
                SetYoloCommandStatus(status, isBusy: false);
                AppendLog(status);
            }
        }

        internal bool IsPendingRemoveUnderlyingAffectedIndex(int sourceIndex)
            => pendingSegmentationRemoveUnderlyingPlan?.Changes
                .Any(change => change.SourceIndex == sourceIndex) == true;
        #endregion

        #region SegmentationSplitCommands
        internal void ExecuteBeginVerticalSegmentationSplitCommand()
            => BeginPendingSegmentationSplit(WpfSegmentationSplitOrientation.Vertical);

        internal void ExecuteBeginHorizontalSegmentationSplitCommand()
            => BeginPendingSegmentationSplit(WpfSegmentationSplitOrientation.Horizontal);

        internal void ExecuteCancelSegmentationSplitCommand()
            => CancelPendingSegmentationSplit(updateStatus: true);

        internal void BeginPendingSegmentationSplit(WpfSegmentationSplitOrientation orientation)
        {
            CancelPendingIntelligentScissors(updateStatus: false);
            CompleteMaskAnnotationStroke();
            FlushQueuedMaskStrokeCommits();
            if (smartMaskPromptSession.HasSession || smartMaskWorkflowService.IsRunning)
            {
                const string smartMaskError = "\uC2A4\uB9C8\uD2B8 \uB9C8\uC2A4\uD06C \uD6C4\uBCF4\uB97C \uD655\uC815\uD558\uAC70\uB098 \uCDE8\uC18C\uD55C \uB4A4 \uC808\uB2E8\uD558\uC138\uC694.";
                SetYoloCommandStatus(smartMaskError, isBusy: false);
                AppendLog(smartMaskError);
                return;
            }

            if (!TryGetSelectedObjectReviewItem(out WpfObjectReviewItemRef selected)
                || selected.Source != WpfObjectReviewSource.ManualSegment
                || selected.Index < 0
                || selected.Index >= manualSegments.Count
                || activeImageSize.IsEmpty)
            {
                const string selectionError = "\uC808\uB2E8\uD560 \uD3F4\uB9AC\uACE4 \uB610\uB294 \uB9C8\uC2A4\uD06C \uAC1D\uCCB4\uB97C \uD558\uB098 \uC120\uD0DD\uD558\uC138\uC694.";
                SetYoloCommandStatus(selectionError, isBusy: false);
                AppendLog(selectionError);
                return;
            }

            if (!CanMutateSelectedObject(selected, requireVisible: true, out string stateError))
            {
                SetYoloCommandStatus(stateError, isBusy: false);
                AppendLog(stateError);
                return;
            }

            SelectAnnotationTool(WpfAnnotationTool.Select);
            pendingSegmentationSplitSourceIndex = selected.Index;
            pendingSegmentationSplitSource = manualSegments[selected.Index];
            pendingSegmentationSplitOrientation = orientation;
            ObjectReviewViewModel?.SetSplitPending(orientation);
            MainCanvasViewModel.IsImagePointInputMode = true;
            MainCanvasViewModel.ImageViewer.SetViewMode(CanvasInteractionMode.None);

            string direction = orientation == WpfSegmentationSplitOrientation.Vertical
                ? "\uC138\uB85C"
                : "\uAC00\uB85C";
            string status = $"{direction} \uC808\uB2E8 \uC704\uCE58 \uC120\uD0DD: \uCE94\uBC84\uC2A4\uC5D0\uC11C \uAC1D\uCCB4 \uC548\uCABD\uC744 \uD074\uB9AD\uD558\uC138\uC694. \uC6B0\uD074\uB9AD\uC740 \uCDE8\uC18C\uC785\uB2C8\uB2E4.";
            SetYoloCommandStatus(status, isBusy: false);
            AppendLog(status);
        }

        internal bool TryApplyPendingSegmentationSplit(CanvasImagePointEventArgs e)
        {
            if (!pendingSegmentationSplitOrientation.HasValue)
            {
                return false;
            }

            if (e?.Button == CanvasPointerButton.Right)
            {
                CancelPendingSegmentationSplit(updateStatus: true);
                return true;
            }

            if (e?.Button != CanvasPointerButton.Left)
            {
                return true;
            }

            int sourceIndex = pendingSegmentationSplitSourceIndex;
            LabelingSegmentationObject source = pendingSegmentationSplitSource;
            if (source == null
                || sourceIndex < 0
                || sourceIndex >= manualSegments.Count
                || !ReferenceEquals(manualSegments[sourceIndex], source))
            {
                CancelPendingSegmentationSplit(updateStatus: false);
                const string staleSelectionError = "\uC120\uD0DD\uD55C \uAC1D\uCCB4\uAC00 \uBCC0\uACBD\uB418\uC5B4 \uC808\uB2E8\uC744 \uCDE8\uC18C\uD588\uC2B5\uB2C8\uB2E4. \uAC1D\uCCB4\uB97C \uB2E4\uC2DC \uC120\uD0DD\uD558\uC138\uC694.";
                SetYoloCommandStatus(staleSelectionError, isBusy: false);
                AppendLog(staleSelectionError);
                return true;
            }

            WpfSegmentationSplitOrientation orientation = pendingSegmentationSplitOrientation.Value;
            int coordinate = orientation == WpfSegmentationSplitOrientation.Vertical
                ? e.ImagePoint.X
                : e.ImagePoint.Y;
            if (!segmentationSplitService.TrySplit(
                source,
                orientation,
                coordinate,
                activeImageSize,
                out WpfSegmentationSplitResult splitResult,
                out string error))
            {
                SetYoloCommandStatus(error, isBusy: false);
                AppendLog($"Segment split skipped: {error}");
                return true;
            }

            string inheritedGroupId = objectMetadataStateService
                .GetManualSegmentMetadata(source)
                .GroupId;
            CancelObjectGroupSelection(updateStatus: false);
            WpfAnnotationHistorySnapshot beforeChange = CaptureAnnotationHistory("\uC138\uADF8\uBA3C\uD2B8 \uC808\uB2E8");
            manualSegments.RemoveAt(sourceIndex);
            manualSegments.InsertRange(sourceIndex, splitResult.Segments);
            objectMetadataStateService.RemoveManualSegment(source);
            foreach (LabelingSegmentationObject segment in splitResult.Segments)
            {
                objectMetadataStateService.SetManualSegmentGroupId(segment, inheritedGroupId);
            }
            objectMetadataStateService.DissolveInvalidGroups(manualRoiCount, manualSegments);
            PushAnnotationHistorySnapshot(beforeChange);
            CancelPendingSegmentationSplit(updateStatus: false);
            MainCanvasViewModel?.ClearMaskStrokePreview(refresh: false, clearTexture: true);
            RefreshPolygonOverlays();
            RefreshObjectListWithSelection(WpfObjectReviewItemRef.ManualSegment(sourceIndex));
            QueueActiveImageQueueStatusRefresh(hasActiveCandidates: pendingDetectionCandidates.Count > 0);

            string direction = orientation == WpfSegmentationSplitOrientation.Vertical
                ? "\uC138\uB85C"
                : "\uAC00\uB85C";
            string status = FormattableString.Invariant(
                $"\uC138\uADF8\uBA3C\uD2B8 {direction} \uC808\uB2E8: 1\uAC1C \u2192 {splitResult.Segments.Count}\uAC1C / \uC88C\uD45C {coordinate}");
            SetYoloCommandStatus(status, isBusy: false);
            AppendLog(status);
            return true;
        }

        internal void CancelPendingSegmentationSplit(bool updateStatus)
        {
            bool wasPending = pendingSegmentationSplitOrientation.HasValue;
            pendingSegmentationSplitSource = null;
            pendingSegmentationSplitSourceIndex = -1;
            pendingSegmentationSplitOrientation = null;
            ObjectReviewViewModel?.SetSplitPending(null);
            if (MainCanvasViewModel != null)
            {
                MainCanvasViewModel.IsImagePointInputMode =
                    activeAnnotationTool == WpfAnnotationTool.Polygon
                    || activeAnnotationTool == WpfAnnotationTool.Brush
                    || activeAnnotationTool == WpfAnnotationTool.Eraser
                    || (activeAnnotationTool == WpfAnnotationTool.Select
                        && ObjectReviewViewModel?.IsSelectedSource(WpfObjectReviewSource.ManualSegment) == true);
            }

            if (wasPending && updateStatus)
            {
                const string status = "\uC138\uADF8\uBA3C\uD2B8 \uC808\uB2E8 \uC704\uCE58 \uC120\uD0DD\uC744 \uCDE8\uC18C\uD588\uC2B5\uB2C8\uB2E4.";
                SetYoloCommandStatus(status, isBusy: false);
                AppendLog(status);
            }
        }
        #endregion

    }

    internal sealed class SegmentationEditAdapterContext
    {
        internal List<LabelingSegmentationObject> ManualSegments { get; init; }
        internal Func<int> ManualRoiCountProvider { get; init; }
        internal Func<Size> ActiveImageSizeProvider { get; init; }
        internal Func<WpfAnnotationTool> ActiveAnnotationToolProvider { get; init; }
        internal Func<WpfCanvasPanelViewModel> CanvasPanelViewModelProvider { get; init; }
        internal Func<RoiImageCanvasViewModel> MainCanvasViewModelProvider { get; init; }
        internal Func<WpfObjectReviewPanelViewModel> ObjectReviewViewModelProvider { get; init; }
        internal Func<WpfObjectReviewItemRef> SelectedObjectReviewItemProvider { get; init; }
        internal Func<WpfObjectReviewItemRef, bool, string> MutationErrorProvider { get; init; }
        internal SmartMaskPromptSessionService SmartMaskPromptSession { get; init; }
        internal SmartMaskWorkflowService SmartMaskWorkflowService { get; init; }
        internal ObjectMetadataStateService ObjectMetadataStateService { get; init; }
        internal Func<IReadOnlyList<YoloWorkerSmokeCandidate>> PendingDetectionCandidatesProvider { get; init; }
        internal Action<bool> CancelPendingIntelligentScissors { get; init; }
        internal Action CompleteMaskAnnotationStroke { get; init; }
        internal Action FlushQueuedMaskStrokeCommits { get; init; }
        internal Action<WpfAnnotationTool> SelectAnnotationTool { get; init; }
        internal Action RefreshPolygonOverlays { get; init; }
        internal Action ClearMaskStrokePreview { get; init; }
        internal Action<WpfObjectReviewItemRef> RefreshObjectListWithSelection { get; init; }
        internal Action<bool> QueueActiveImageQueueStatusRefresh { get; init; }
        internal Action<string, bool> SetYoloCommandStatus { get; init; }
        internal Action<string> AppendLog { get; init; }
        internal Func<string, WpfAnnotationHistorySnapshot> CaptureAnnotationHistory { get; init; }
        internal Action<WpfAnnotationHistorySnapshot> PushAnnotationHistorySnapshot { get; init; }
        internal Action<bool> CancelObjectGroupSelection { get; init; }
    }
}
