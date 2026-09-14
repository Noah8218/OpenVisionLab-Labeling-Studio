using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.Canvas;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using System;
using System.Drawing;

namespace MvcVisionSystem
{
    /// <summary>
    /// Routes image-point input and annotation mode transitions through existing
    /// annotation services while the Shell retains live collections and stroke state.
    /// </summary>
    internal sealed class AnnotationInputAdapter
    {
        private readonly AnnotationInputAdapterContext context;

        internal AnnotationInputAdapter(AnnotationInputAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        #region PointerInput
        internal void HandleImagePointClicked(CanvasImagePointEventArgs e)
        {
            if (IsCloseApproved() || e == null)
            {
                return;
            }

            if (InvokePointerHandler(context.TryHandleFourPointBoxInput, e)
                || InvokePointerHandler(context.TryApplyPendingPolygonVertexEdit, e)
                || InvokePointerHandler(context.TryHandlePendingIntelligentScissors, e)
                || InvokePointerHandler(context.TryApplyPendingSegmentationSplit, e)
                || InvokePointerHandler(context.TryApplyPendingSegmentationHoleEdit, e)
                || InvokePointerHandler(context.TryApplySmartMaskPointInput, e))
            {
                return;
            }

            WpfAnnotationTool activeTool = GetActiveAnnotationTool();
            if (activeTool == WpfAnnotationTool.Select
                && InvokePointerHandler(context.TryBeginSelectedSegmentEdit, e))
            {
                return;
            }

            if (activeTool == WpfAnnotationTool.Brush || activeTool == WpfAnnotationTool.Eraser)
            {
                context.ApplyMaskAnnotationStroke?.Invoke(e, true);
                return;
            }

            if (activeTool != WpfAnnotationTool.Polygon)
            {
                return;
            }

            if (e.Button == CanvasPointerButton.Right)
            {
                context.ResetPolygonAnnotation?.Invoke();
                context.RefreshPolygonOverlays?.Invoke();
                context.SetYoloCommandStatus?.Invoke(
                    "폴리곤 초안을 취소했습니다. 이미지를 클릭해 새 폴리곤을 시작하세요.",
                    false);
                return;
            }

            Size activeImageSize = context.ActiveImageSizeProvider?.Invoke() ?? Size.Empty;
            if (e.Button != CanvasPointerButton.Left || activeImageSize.IsEmpty)
            {
                return;
            }

            PolygonAnnotationService polygonAnnotationService = context.PolygonAnnotationService;
            if (e.Clicks > 1 && polygonAnnotationService?.Points.Count >= 3)
            {
                context.CompletePolygonAnnotation?.Invoke();
                return;
            }

            if (polygonAnnotationService == null
                || !polygonAnnotationService.TryAddPoint(e.ImagePoint, activeImageSize, out bool closed))
            {
                return;
            }

            context.RefreshPolygonOverlays?.Invoke();
            if (closed)
            {
                context.CompletePolygonAnnotation?.Invoke();
                return;
            }

            context.SetYoloCommandStatus?.Invoke(
                $"폴리곤 초안: {polygonAnnotationService.Points.Count}점. 첫 점 근처를 클릭하거나 더블클릭해 완료하세요.",
                false);
        }

        internal void HandleImagePointMoved(CanvasImagePointEventArgs e)
        {
            if (IsCloseApproved() || e == null)
            {
                return;
            }

            WpfAnnotationTool activeTool = GetActiveAnnotationTool();
            if (activeTool == WpfAnnotationTool.Select
                && InvokePointerHandler(context.TryMoveSelectedSegmentEdit, e))
            {
                return;
            }

            if (activeTool != WpfAnnotationTool.Brush && activeTool != WpfAnnotationTool.Eraser)
            {
                return;
            }

            context.ApplyMaskAnnotationStroke?.Invoke(e, false);
        }

        internal void HandleImagePointReleased()
        {
            if (IsCloseApproved())
            {
                return;
            }

            context.CompleteMaskAnnotationStroke?.Invoke();
            context.SetLastMaskStrokePoint?.Invoke(null);
            context.CompleteSelectedSegmentEdit?.Invoke();
        }

        internal void HandleImagePointHovered(CanvasImagePointEventArgs e)
        {
            WpfAnnotationTool activeTool = GetActiveAnnotationTool();
            if (e == null || (activeTool != WpfAnnotationTool.Brush && activeTool != WpfAnnotationTool.Eraser))
            {
                context.ClearBrushCursorPreview?.Invoke();
                return;
            }

            bool isEraser = activeTool == WpfAnnotationTool.Eraser;
            context.SetBrushCursorPreview?.Invoke(
                e.ImagePoint,
                context.GetMaskBrushRadius?.Invoke() ?? 0,
                context.GetMaskCursorPreviewColor?.Invoke(isEraser) ?? Color.Empty,
                isEraser);
        }
        #endregion

        #region AnnotationModes
        internal void BeginPolygonAnnotationMode()
        {
            context.EnsureSegmentationDatasetPurposeForSegmentationTool?.Invoke();
            context.SetLabelingWorkflowMode?.Invoke();
            context.SetActiveAnnotationTool?.Invoke(WpfAnnotationTool.Polygon);
            context.SetCanvasTeachingMode?.Invoke(false);
            context.SetCanvasImagePointInputMode?.Invoke(true);
            context.SetCanvasInteractionMode?.Invoke(CanvasInteractionMode.None);
            context.ResetPolygonAnnotation?.Invoke();
            context.RefreshPolygonOverlays?.Invoke();
            context.SetModelStatus?.Invoke("도구: 폴리곤 세그멘테이션");
            context.SetYoloCommandStatus?.Invoke(
                "폴리곤: 경계점을 클릭하고 첫 점 근처 또는 더블클릭으로 완료합니다. 우클릭은 초안을 취소합니다.",
                false);
            context.AppendLog?.Invoke("폴리곤 라벨링 도구를 선택했습니다.");
            context.RefreshCanvasWorkflowContext?.Invoke();
        }

        internal void EndPolygonAnnotationMode(bool clearDraft)
        {
            context.SetCanvasImagePointInputMode?.Invoke(false);
            if (clearDraft)
            {
                context.ResetPolygonAnnotation?.Invoke();
                context.RefreshPolygonOverlays?.Invoke();
            }
        }

        internal void BeginMaskAnnotationMode(WpfAnnotationTool tool)
        {
            // Do not drop a GPU-previewed brush stroke before MouseUp when tools change.
            context.CompleteMaskAnnotationStroke?.Invoke();
            context.EnsureSegmentationDatasetPurposeForSegmentationTool?.Invoke();
            context.SetLabelingWorkflowMode?.Invoke();
            context.SetActiveAnnotationTool?.Invoke(tool);
            context.SetCanvasTeachingMode?.Invoke(false);
            context.SetCanvasImagePointInputMode?.Invoke(true);
            context.SetCanvasInteractionMode?.Invoke(CanvasInteractionMode.None);
            context.ResetPolygonAnnotation?.Invoke();
            context.SetLastMaskStrokePoint?.Invoke(null);
            context.SetActiveMaskStrokeInProgress?.Invoke(false);
            context.SetActiveMaskStrokeActionName?.Invoke(string.Empty);
            context.ClearActiveMaskStrokeSegmentIndices?.Invoke();
            context.ResetMaskStrokeCommitBuffer?.Invoke();
            context.SetActiveMaskStrokeNeedsFullObjectRefresh?.Invoke(false);
            context.CancelMaskStrokePreviewCommitSwap?.Invoke();
            if (!(context.ShouldPreserveMaskPreviewDuringToolSwitch?.Invoke() ?? true))
            {
                context.ClearMaskStrokePreview?.Invoke(false);
            }

            context.RefreshPolygonOverlays?.Invoke();

            string toolName = tool == WpfAnnotationTool.Eraser ? "지우개" : "브러시";
            int radius = context.GetMaskBrushRadius?.Invoke() ?? 0;
            context.SetModelStatus?.Invoke($"도구: 마스크 {toolName}");
            context.SetYoloCommandStatus?.Invoke(
                $"마스크 {toolName}: 이미지 위를 드래그하세요. 브러시 반경 {radius}px. 우클릭하면 현재 스트로크를 초기화합니다.",
                false);
            context.AppendLog?.Invoke($"마스크 {toolName} 도구를 선택했습니다. 반경:{radius}px");
            context.RefreshCanvasWorkflowContext?.Invoke();
        }

        internal void EndMaskAnnotationMode()
        {
            context.CompleteMaskAnnotationStroke?.Invoke();
            bool materializationScheduled = context.ScheduleQueuedMaskStrokeCommitsAfterToolEnd?.Invoke() == true;
            context.SetLastMaskStrokePoint?.Invoke(null);
            context.CancelMaskStrokePreviewCommitSwap?.Invoke();
            context.ClearBrushCursorPreview?.Invoke();
            if (!materializationScheduled)
            {
                context.ClearMaskStrokePreview?.Invoke(true);
            }

            WpfAnnotationTool activeTool = GetActiveAnnotationTool();
            if (activeTool == WpfAnnotationTool.Brush || activeTool == WpfAnnotationTool.Eraser)
            {
                context.SetActiveAnnotationTool?.Invoke(WpfAnnotationTool.Select);
            }
        }
        #endregion

        private bool IsCloseApproved()
            => context.IsApplicationCloseApproved?.Invoke() == true;

        private WpfAnnotationTool GetActiveAnnotationTool()
            => context.ActiveAnnotationToolProvider?.Invoke() ?? WpfAnnotationTool.Select;

        private static bool InvokePointerHandler(
            Func<CanvasImagePointEventArgs, bool> handler,
            CanvasImagePointEventArgs e)
            => handler?.Invoke(e) == true;
    }

    internal sealed class AnnotationInputAdapterContext
    {
        internal Func<bool> IsApplicationCloseApproved { get; init; }
        internal Func<CanvasImagePointEventArgs, bool> TryHandleFourPointBoxInput { get; init; }
        internal Func<CanvasImagePointEventArgs, bool> TryApplyPendingPolygonVertexEdit { get; init; }
        internal Func<CanvasImagePointEventArgs, bool> TryHandlePendingIntelligentScissors { get; init; }
        internal Func<CanvasImagePointEventArgs, bool> TryApplyPendingSegmentationSplit { get; init; }
        internal Func<CanvasImagePointEventArgs, bool> TryApplyPendingSegmentationHoleEdit { get; init; }
        internal Func<CanvasImagePointEventArgs, bool> TryApplySmartMaskPointInput { get; init; }
        internal Func<WpfAnnotationTool> ActiveAnnotationToolProvider { get; init; }
        internal Func<CanvasImagePointEventArgs, bool> TryBeginSelectedSegmentEdit { get; init; }
        internal Func<CanvasImagePointEventArgs, bool> TryMoveSelectedSegmentEdit { get; init; }
        internal Action<CanvasImagePointEventArgs, bool> ApplyMaskAnnotationStroke { get; init; }
        internal Action CompleteMaskAnnotationStroke { get; init; }
        internal Action CompleteSelectedSegmentEdit { get; init; }
        internal Action ResetPolygonAnnotation { get; init; }
        internal Action CompletePolygonAnnotation { get; init; }
        internal PolygonAnnotationService PolygonAnnotationService { get; init; }
        internal Func<Size> ActiveImageSizeProvider { get; init; }
        internal Action RefreshPolygonOverlays { get; init; }
        internal Action<string, bool> SetYoloCommandStatus { get; init; }
        internal Action EnsureSegmentationDatasetPurposeForSegmentationTool { get; init; }
        internal Action SetLabelingWorkflowMode { get; init; }
        internal Action<WpfAnnotationTool> SetActiveAnnotationTool { get; init; }
        internal Action<bool> SetCanvasTeachingMode { get; init; }
        internal Action<bool> SetCanvasImagePointInputMode { get; init; }
        internal Action<CanvasInteractionMode> SetCanvasInteractionMode { get; init; }
        internal Action<string> SetModelStatus { get; init; }
        internal Action<string> AppendLog { get; init; }
        internal Action RefreshCanvasWorkflowContext { get; init; }
        internal Action<Point?> SetLastMaskStrokePoint { get; init; }
        internal Action<bool> SetActiveMaskStrokeInProgress { get; init; }
        internal Action<string> SetActiveMaskStrokeActionName { get; init; }
        internal Action ClearActiveMaskStrokeSegmentIndices { get; init; }
        internal Action ResetMaskStrokeCommitBuffer { get; init; }
        internal Action<bool> SetActiveMaskStrokeNeedsFullObjectRefresh { get; init; }
        internal Action CancelMaskStrokePreviewCommitSwap { get; init; }
        internal Func<bool> ShouldPreserveMaskPreviewDuringToolSwitch { get; init; }
        internal Func<bool> ScheduleQueuedMaskStrokeCommitsAfterToolEnd { get; init; }
        internal Action ClearBrushCursorPreview { get; init; }
        internal Action<bool> ClearMaskStrokePreview { get; init; }
        internal Func<int> GetMaskBrushRadius { get; init; }
        internal Func<bool, Color> GetMaskCursorPreviewColor { get; init; }
        internal Action<Point, int, Color, bool> SetBrushCursorPreview { get; init; }
    }
}
