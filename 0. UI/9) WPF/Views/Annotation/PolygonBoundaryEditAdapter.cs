using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.Canvas;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using OpenVisionLab.ImageCanvas.ViewModels;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the command and input policy for polygon boundary editing.
    /// Geometry and pending edit state remain in PolygonBoundaryEditWorkflowService;
    /// the Shell supplies mutable collections and presentation callbacks.
    /// </summary>
    internal sealed class PolygonBoundaryEditAdapter
    {
        private readonly PolygonBoundaryEditWorkflowService workflowService;
        private readonly PolygonBoundaryEditAdapterContext context;

        private Bitmap ActiveImageBitmap => context.ActiveImageBitmapProvider?.Invoke();
        private Size ActiveImageSize => context.ActiveImageSizeProvider?.Invoke() ?? Size.Empty;
        private WpfCanvasPanelViewModel CanvasPanelViewModel => context.CanvasPanelViewModel;
        private RoiImageCanvasViewModel MainCanvasViewModel => context.MainCanvasViewModel;

        internal PolygonBoundaryEditAdapter(
            PolygonBoundaryEditWorkflowService workflowService,
            PolygonBoundaryEditAdapterContext context)
        {
            this.workflowService = workflowService ?? throw new ArgumentNullException(nameof(workflowService));
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        internal void ExecuteBeginIntelligentScissorsCommand()
        {
            if (ActiveImageBitmap == null || ActiveImageSize.IsEmpty)
            {
                const string imageError = "경계를 분석할 이미지를 먼저 여세요.";
                SetYoloCommandStatus(imageError, isBusy: false);
                AppendLog(imageError);
                return;
            }

            if (!TryGetSelectedObjectReviewItem(out WpfObjectReviewItemRef selected)
                || selected.Source != WpfObjectReviewSource.ManualSegment
                || selected.Index < 0
                || selected.Index >= context.ManualSegments.Count
                || context.ManualSegments[selected.Index]?.IsRasterMask != false)
            {
                const string selectionError = "경계를 다시 계산할 수동 폴리곤을 선택하세요.";
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

            CancelPendingSegmentationRemoveUnderlying(updateStatus: false);
            CancelPendingPolygonVertexEdit(updateStatus: false);
            CancelPendingSegmentationSplit(updateStatus: false);
            CancelPendingSegmentationHoleEdit(updateStatus: false);
            SelectAnnotationTool(WpfAnnotationTool.Select);
            workflowService.BeginIntelligentScissors(
                selected.Index,
                context.ManualSegments[selected.Index]);
            SetIntelligentScissorsState(
                pending: true,
                hasPreview: false,
                statusText: "경계 추종: 다시 계산할 폴리곤 모서리 근처를 클릭하세요. 우클릭은 취소입니다.");
            MainCanvasViewModel.IsImagePointInputMode = true;
            MainCanvasViewModel.ImageViewer.SetViewMode(CanvasInteractionMode.None);
            RefreshPolygonOverlays();
            const string status = "경계 추종: 폴리곤 모서리를 클릭하면 이미지 경계 경로를 미리봅니다.";
            SetYoloCommandStatus(status, isBusy: false);
            AppendLog(status);
        }

        internal bool TryHandlePendingIntelligentScissors(CanvasImagePointEventArgs e)
        {
            if (!workflowService.IsIntelligentScissorsPending)
            {
                return false;
            }

            if (e?.Button == CanvasPointerButton.Right)
            {
                CancelPendingIntelligentScissors(updateStatus: true);
                return true;
            }

            if (e?.Button != CanvasPointerButton.Left)
            {
                return true;
            }

            if (!TryResolvePendingIntelligentScissorsSource(out int sourceIndex, out LabelingSegmentationObject source))
            {
                CancelPendingIntelligentScissors(updateStatus: false);
                const string staleSelectionError = "선택한 객체가 변경되어 경계 추종을 취소했습니다.";
                SetYoloCommandStatus(staleSelectionError, isBusy: false);
                AppendLog(staleSelectionError);
                return true;
            }

            int hitTolerance = PolygonAnnotationService.ResolveImageHitTolerance(
                MainCanvasViewModel?.ImageViewer?.ZoomScale ?? 1F);
            if (!workflowService.TryPreviewIntelligentScissors(
                ActiveImageBitmap,
                e.ImagePoint,
                ActiveImageSize,
                hitTolerance,
                out WpfIntelligentScissorsPlan plan,
                out string error))
            {
                SetIntelligentScissorsState(
                    pending: true,
                    hasPreview: false,
                    statusText: error);
                RefreshPolygonOverlays();
                SetYoloCommandStatus(error, isBusy: false);
                AppendLog($"Intelligent scissors preview skipped: {error}");
                return true;
            }

            string previewStatus = FormattableString.Invariant(
                $"경계 미리보기: {plan.PathPoints.Count}개 경로점 / {plan.Elapsed.TotalMilliseconds:0.0} ms. 캔버스를 확인한 후 미리보기 적용을 누르세요.");
            SetIntelligentScissorsState(
                pending: true,
                hasPreview: true,
                statusText: previewStatus);
            RefreshPolygonOverlays();
            SetYoloCommandStatus(previewStatus, isBusy: false);
            AppendLog($"Intelligent scissors preview: segment {sourceIndex + 1} / edge {plan.EdgeIndex + 1} / {plan.PathPoints.Count} points / {plan.Elapsed.TotalMilliseconds:0.0} ms");
            return true;
        }

        internal void ExecuteApplyIntelligentScissorsCommand()
        {
            if (!workflowService.HasIntelligentScissorsPreview
                || !TryResolvePendingIntelligentScissorsSource(out int sourceIndex, out LabelingSegmentationObject source))
            {
                const string previewError = "적용할 경계 미리보기가 없습니다.";
                SetYoloCommandStatus(previewError, isBusy: false);
                AppendLog(previewError);
                return;
            }

            var selected = WpfObjectReviewItemRef.ManualSegment(sourceIndex);
            if (!CanMutateSelectedObject(selected, requireVisible: true, out string stateError))
            {
                CancelPendingIntelligentScissors(updateStatus: false);
                SetYoloCommandStatus(stateError, isBusy: false);
                AppendLog(stateError);
                return;
            }

            const string actionName = "폴리곤 경계 추종";
            WpfAnnotationHistorySnapshot beforeChange = CaptureAnnotationHistory(actionName);
            if (!workflowService.TryApplyIntelligentScissors(
                context.ManualSegments,
                ActiveImageSize,
                out sourceIndex,
                out source,
                out Rectangle _,
                out string error))
            {
                CancelPendingIntelligentScissors(updateStatus: false);
                SetYoloCommandStatus(error, isBusy: false);
                AppendLog($"{actionName} skipped: {error}");
                return;
            }

            PushAnnotationHistorySnapshot(beforeChange);
            CancelPendingIntelligentScissors(updateStatus: false);
            RefreshPolygonOverlays();
            RefreshObjectListWithSelection(WpfObjectReviewItemRef.ManualSegment(sourceIndex));
            MarkAnnotationsDirty(actionName);
            QueueActiveImageQueueStatusRefresh(hasActiveCandidates: context.PendingCandidateCountProvider?.Invoke() > 0);
            string status = $"{actionName}: {source.Points.Count}개 정점 / 미리보기 경로 적용";
            SetYoloCommandStatus(status, isBusy: false);
            AppendLog(status);
        }

        internal void ExecuteCancelIntelligentScissorsCommand()
            => CancelPendingIntelligentScissors(updateStatus: true);

        internal bool TryResolvePendingIntelligentScissorsSource(
            out int sourceIndex,
            out LabelingSegmentationObject source)
            => workflowService.TryResolveIntelligentScissorsSource(
                context.ManualSegments,
                out sourceIndex,
                out source);

        internal void CancelPendingIntelligentScissors(bool updateStatus)
        {
            bool wasPending = workflowService.CancelIntelligentScissors();
            SetIntelligentScissorsState(pending: false, hasPreview: false);
            RestoreImagePointInputMode();
            RefreshPolygonOverlays();
            if (wasPending && updateStatus)
            {
                const string status = "경계 추종 미리보기를 취소했습니다.";
                SetYoloCommandStatus(status, isBusy: false);
                AppendLog(status);
            }
        }

        internal void ExecuteBeginInsertPolygonVertexCommand()
            => BeginPendingPolygonVertexEdit(WpfPolygonVertexEditMode.Insert);

        internal void ExecuteBeginDeletePolygonVertexCommand()
            => BeginPendingPolygonVertexEdit(WpfPolygonVertexEditMode.Delete);

        internal void ExecuteCancelPolygonVertexEditCommand()
            => CancelPendingPolygonVertexEdit(updateStatus: true);

        internal void BeginPendingPolygonVertexEdit(WpfPolygonVertexEditMode mode)
        {
            CancelPendingIntelligentScissors(updateStatus: false);
            CompleteMaskAnnotationStroke();
            FlushQueuedMaskStrokeCommits();
            if (context.HasSmartMaskSession?.Invoke() == true || context.IsSmartMaskRunning?.Invoke() == true)
            {
                const string smartMaskError = "스마트 마스크 후보를 확정하거나 취소한 뒤 폴리곤 정점을 편집하세요.";
                SetYoloCommandStatus(smartMaskError, isBusy: false);
                AppendLog(smartMaskError);
                return;
            }

            if (!TryGetSelectedObjectReviewItem(out WpfObjectReviewItemRef selected)
                || selected.Source != WpfObjectReviewSource.ManualSegment
                || selected.Index < 0
                || selected.Index >= context.ManualSegments.Count
                || context.ManualSegments[selected.Index]?.IsRasterMask != false
                || ActiveImageSize.IsEmpty)
            {
                const string selectionError = "정점을 편집할 수동 폴리곤 객체를 하나 선택하세요.";
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
            workflowService.BeginPolygonVertexEdit(
                selected.Index,
                context.ManualSegments[selected.Index],
                mode);
            SetVertexEditPending(mode);
            MainCanvasViewModel.IsImagePointInputMode = true;
            MainCanvasViewModel.ImageViewer.SetViewMode(CanvasInteractionMode.None);
            RefreshPolygonOverlays();

            string status = mode == WpfPolygonVertexEditMode.Insert
                ? "정점 추가: 선택한 폴리곤 모서리 근처를 클릭하세요. 우클릭은 취소입니다."
                : "정점 삭제: 삭제할 정점 근처를 클릭하세요. 우클릭은 취소입니다.";
            SetYoloCommandStatus(status, isBusy: false);
            AppendLog(status);
        }

        internal bool TryApplyPendingPolygonVertexEdit(CanvasImagePointEventArgs e)
        {
            if (!workflowService.IsPolygonVertexEditPending)
            {
                return false;
            }

            if (e?.Button == CanvasPointerButton.Right)
            {
                CancelPendingPolygonVertexEdit(updateStatus: true);
                return true;
            }

            if (e?.Button != CanvasPointerButton.Left)
            {
                return true;
            }

            if (!TryResolvePendingPolygonVertexSource(out int sourceIndex, out LabelingSegmentationObject source))
            {
                CancelPendingPolygonVertexEdit(updateStatus: false);
                const string staleSelectionError = "선택한 객체가 변경되어 정점 편집을 취소했습니다.";
                SetYoloCommandStatus(staleSelectionError, isBusy: false);
                AppendLog(staleSelectionError);
                return true;
            }

            int hitTolerance = PolygonAnnotationService.ResolveImageHitTolerance(
                MainCanvasViewModel?.ImageViewer?.ZoomScale ?? 1F);
            WpfPolygonVertexEditMode mode = workflowService.PolygonVertexEditMode.Value;
            string actionName = mode == WpfPolygonVertexEditMode.Insert
                ? "폴리곤 정점 추가"
                : "폴리곤 정점 삭제";
            WpfAnnotationHistorySnapshot beforeChange = CaptureAnnotationHistory(actionName);
            bool changed = workflowService.TryApplyPolygonVertexEdit(
                context.ManualSegments,
                e.ImagePoint,
                ActiveImageSize,
                hitTolerance,
                out sourceIndex,
                out source,
                out mode,
                out Rectangle _,
                out string error);
            if (!changed)
            {
                SetYoloCommandStatus(error, isBusy: false);
                AppendLog($"{actionName} skipped: {error}");
                return true;
            }

            PushAnnotationHistorySnapshot(beforeChange);
            CancelPendingPolygonVertexEdit(updateStatus: false);
            RefreshPolygonOverlays();
            RefreshObjectListWithSelection(WpfObjectReviewItemRef.ManualSegment(sourceIndex));
            MarkAnnotationsDirty(actionName);
            QueueActiveImageQueueStatusRefresh(hasActiveCandidates: context.PendingCandidateCountProvider?.Invoke() > 0);
            string status = $"{actionName}: {source.Points.Count}개 정점";
            SetYoloCommandStatus(status, isBusy: false);
            AppendLog(status);
            return true;
        }

        internal bool TryResolvePendingPolygonVertexSource(
            out int sourceIndex,
            out LabelingSegmentationObject source)
            => workflowService.TryResolvePolygonVertexSource(
                context.ManualSegments,
                out sourceIndex,
                out source);

        internal void CancelPendingPolygonVertexEdit(bool updateStatus)
        {
            bool wasPending = workflowService.CancelPolygonVertexEdit();
            SetVertexEditPending(null);
            RestoreImagePointInputMode();
            RefreshPolygonOverlays();
            if (wasPending && updateStatus)
            {
                const string status = "폴리곤 정점 편집을 취소했습니다.";
                SetYoloCommandStatus(status, isBusy: false);
                AppendLog(status);
            }
        }

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

        private void RestoreImagePointInputMode()
        {
            if (MainCanvasViewModel == null)
            {
                return;
            }

            MainCanvasViewModel.IsImagePointInputMode =
                context.ActiveAnnotationToolProvider?.Invoke() == WpfAnnotationTool.Polygon
                || context.ActiveAnnotationToolProvider?.Invoke() == WpfAnnotationTool.Brush
                || context.ActiveAnnotationToolProvider?.Invoke() == WpfAnnotationTool.Eraser
                || (context.ActiveAnnotationToolProvider?.Invoke() == WpfAnnotationTool.Select
                    && context.IsSelectedManualSegment?.Invoke() == true);
        }

        private void SetIntelligentScissorsState(bool pending, bool hasPreview, string statusText = "")
            => context.SetIntelligentScissorsState?.Invoke(pending, hasPreview, statusText);

        private void SetVertexEditPending(WpfPolygonVertexEditMode? mode)
            => context.SetVertexEditPending?.Invoke(mode);

        private void CancelPendingSegmentationRemoveUnderlying(bool updateStatus)
            => context.CancelPendingSegmentationRemoveUnderlying?.Invoke(updateStatus);

        private void CancelPendingSegmentationSplit(bool updateStatus)
            => context.CancelPendingSegmentationSplit?.Invoke(updateStatus);

        private void CancelPendingSegmentationHoleEdit(bool updateStatus)
            => context.CancelPendingSegmentationHoleEdit?.Invoke(updateStatus);

        private void CompleteMaskAnnotationStroke()
            => context.CompleteMaskAnnotationStroke?.Invoke();

        private void FlushQueuedMaskStrokeCommits()
            => context.FlushQueuedMaskStrokeCommits?.Invoke();

        private WpfAnnotationHistorySnapshot CaptureAnnotationHistory(string actionName)
            => context.CaptureAnnotationHistory?.Invoke(actionName);

        private void PushAnnotationHistorySnapshot(WpfAnnotationHistorySnapshot snapshot)
            => context.PushAnnotationHistorySnapshot?.Invoke(snapshot, true);

        private void MarkAnnotationsDirty(string actionName)
            => context.MarkAnnotationsDirty?.Invoke(actionName);

        private void RefreshObjectListWithSelection(WpfObjectReviewItemRef item)
            => context.RefreshObjectListWithSelection?.Invoke(item);

        private void QueueActiveImageQueueStatusRefresh(bool hasActiveCandidates)
            => context.QueueActiveImageQueueStatusRefresh?.Invoke(hasActiveCandidates);

        private void SelectAnnotationTool(WpfAnnotationTool tool)
            => context.SelectAnnotationTool?.Invoke(tool);

        private void RefreshPolygonOverlays()
            => context.RefreshPolygonOverlays?.Invoke();

        private void SetYoloCommandStatus(string text, bool isBusy)
            => context.SetYoloCommandStatus?.Invoke(text, isBusy);

        private void AppendLog(string message)
            => context.AppendLog?.Invoke(message);
    }

    internal sealed class PolygonBoundaryEditAdapterContext
    {
        internal List<LabelingSegmentationObject> ManualSegments { get; init; }
        internal Func<Bitmap> ActiveImageBitmapProvider { get; init; }
        internal Func<Size> ActiveImageSizeProvider { get; init; }
        internal Func<WpfAnnotationTool> ActiveAnnotationToolProvider { get; init; }
        internal Func<WpfObjectReviewItemRef> SelectedObjectReviewItemProvider { get; init; }
        internal Func<WpfObjectReviewItemRef, bool, string> MutationErrorProvider { get; init; }
        internal Func<bool> IsSelectedManualSegment { get; init; }
        internal Func<bool> HasSmartMaskSession { get; init; }
        internal Func<bool> IsSmartMaskRunning { get; init; }
        internal Func<int> PendingCandidateCountProvider { get; init; }
        internal WpfCanvasPanelViewModel CanvasPanelViewModel { get; init; }
        internal RoiImageCanvasViewModel MainCanvasViewModel { get; init; }
        internal Action<bool> CancelPendingSegmentationRemoveUnderlying { get; init; }
        internal Action<bool> CancelPendingSegmentationSplit { get; init; }
        internal Action<bool> CancelPendingSegmentationHoleEdit { get; init; }
        internal Action CompleteMaskAnnotationStroke { get; init; }
        internal Action FlushQueuedMaskStrokeCommits { get; init; }
        internal Action<WpfObjectReviewItemRef> RefreshObjectListWithSelection { get; init; }
        internal Action<bool> QueueActiveImageQueueStatusRefresh { get; init; }
        internal Action RefreshPolygonOverlays { get; init; }
        internal Func<string, WpfAnnotationHistorySnapshot> CaptureAnnotationHistory { get; init; }
        internal Action<WpfAnnotationHistorySnapshot, bool> PushAnnotationHistorySnapshot { get; init; }
        internal Action<string> MarkAnnotationsDirty { get; init; }
        internal Action<WpfAnnotationTool> SelectAnnotationTool { get; init; }
        internal Action<bool, bool, string> SetIntelligentScissorsState { get; init; }
        internal Action<WpfPolygonVertexEditMode?> SetVertexEditPending { get; init; }
        internal Action<string, bool> SetYoloCommandStatus { get; init; }
        internal Action<string> AppendLog { get; init; }
    }
}
