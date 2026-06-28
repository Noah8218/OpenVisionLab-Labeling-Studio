using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.Canvas;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using OpenVisionLab.ImageCanvas.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        private void MainCanvasViewModel_ImagePointClicked(object sender, CanvasImagePointEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            if (activeAnnotationTool == WpfAnnotationTool.Select && TryBeginSelectedSegmentEdit(e))
            {
                return;
            }

            if (activeAnnotationTool == WpfAnnotationTool.Brush || activeAnnotationTool == WpfAnnotationTool.Eraser)
            {
                ApplyMaskAnnotationStroke(e, resetStroke: true);
                return;
            }

            if (activeAnnotationTool != WpfAnnotationTool.Polygon)
            {
                return;
            }

            if (e.Button == CanvasPointerButton.Right)
            {
                polygonAnnotationService.Reset();
                RefreshPolygonOverlays();
                SetYoloCommandStatus("폴리곤 초안을 취소했습니다. 이미지를 클릭해 새 폴리곤을 시작하세요.", isBusy: false);
                return;
            }

            if (e.Button != CanvasPointerButton.Left || activeImageSize.IsEmpty)
            {
                return;
            }

            if (e.Clicks > 1 && polygonAnnotationService.Points.Count >= 3)
            {
                CompletePolygonAnnotation();
                return;
            }

            if (!polygonAnnotationService.TryAddPoint(e.ImagePoint, activeImageSize, out bool closed))
            {
                return;
            }

            RefreshPolygonOverlays();
            if (closed)
            {
                CompletePolygonAnnotation();
                return;
            }

            SetYoloCommandStatus($"폴리곤 초안: {polygonAnnotationService.Points.Count}점. 첫 점 근처를 클릭하거나 더블클릭해 완료하세요.", isBusy: false);
        }

        private void MainCanvasViewModel_ImagePointMoved(object sender, CanvasImagePointEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            if (activeAnnotationTool == WpfAnnotationTool.Select && TryMoveSelectedSegmentEdit(e))
            {
                return;
            }

            if (activeAnnotationTool != WpfAnnotationTool.Brush && activeAnnotationTool != WpfAnnotationTool.Eraser)
            {
                return;
            }

            ApplyMaskAnnotationStroke(e, resetStroke: false);
        }

        private void MainCanvasViewModel_ImagePointReleased(object sender, CanvasImagePointEventArgs e)
        {
            CompleteMaskAnnotationStroke();
            lastMaskStrokePoint = null;
            CompleteSelectedSegmentEdit();
        }

        private void MainCanvasViewModel_ImagePointHovered(object sender, CanvasImagePointEventArgs e)
        {
            if (e == null || (activeAnnotationTool != WpfAnnotationTool.Brush && activeAnnotationTool != WpfAnnotationTool.Eraser))
            {
                MainCanvasViewModel.ClearBrushCursorPreview();
                return;
            }

            MainCanvasViewModel.SetBrushCursorPreview(
                e.ImagePoint,
                GetMaskBrushRadius(),
                GetMaskCursorPreviewColor(activeAnnotationTool == WpfAnnotationTool.Eraser),
                activeAnnotationTool == WpfAnnotationTool.Eraser);
        }

        private void BeginPolygonAnnotationMode()
        {
            EnsureSegmentationDatasetPurposeForSegmentationTool();
            SetWorkflowMode(WorkflowMode.Labeling);
            activeAnnotationTool = WpfAnnotationTool.Polygon;
            MainCanvasViewModel.IsTeachingMode = false;
            MainCanvasViewModel.IsImagePointInputMode = true;
            MainCanvasViewModel.ImageViewer.SetViewMode(CanvasInteractionMode.None);
            polygonAnnotationService.Reset();
            RefreshPolygonOverlays();
            SetModelStatus("도구: 폴리곤 세그멘테이션");
            SetYoloCommandStatus("폴리곤: 경계점을 클릭하고 첫 점 근처 또는 더블클릭으로 완료합니다. 우클릭은 초안을 취소합니다.", isBusy: false);
            AppendLog("폴리곤 라벨링 도구를 선택했습니다.");
            RefreshCanvasWorkflowContext();
        }

        private void EndPolygonAnnotationMode(bool clearDraft)
        {
            if (MainCanvasViewModel != null)
            {
                MainCanvasViewModel.IsImagePointInputMode = false;
            }

            if (clearDraft)
            {
                polygonAnnotationService.Reset();
                RefreshPolygonOverlays();
            }
        }

        private void BeginMaskAnnotationMode(WpfAnnotationTool tool)
        {
            // Tool changes should not drop a GPU-previewed brush stroke before MouseUp.
            CompleteMaskAnnotationStroke();
            EnsureSegmentationDatasetPurposeForSegmentationTool();
            SetWorkflowMode(WorkflowMode.Labeling);
            activeAnnotationTool = tool;
            MainCanvasViewModel.IsTeachingMode = false;
            MainCanvasViewModel.IsImagePointInputMode = true;
            MainCanvasViewModel.ImageViewer.SetViewMode(CanvasInteractionMode.None);
            polygonAnnotationService.Reset();
            lastMaskStrokePoint = null;
            activeMaskStrokeInProgress = false;
            activeMaskStrokeActionName = string.Empty;
            activeMaskStrokeSegmentIndices.Clear();
            ResetMaskStrokeCommitBuffer();
            activeMaskStrokeNeedsFullObjectRefresh = false;
            CancelMaskStrokePreviewCommitSwap();
            // The FBO preview is the visible source until queued CPU commits catch up.
            // Tool switches must preserve it so brush -> eraser does not flash stale pixels.
            if (!maskEditStateService.ShouldPreservePreviewDuringToolSwitch(HasPendingMaskStrokeCommitWork()))
            {
                MainCanvasViewModel?.ClearMaskStrokePreview(refresh: false);
            }

            RefreshPolygonOverlays();

            string toolName = tool == WpfAnnotationTool.Eraser ? "지우개" : "브러시";
            int radius = GetMaskBrushRadius();
            SetModelStatus($"도구: 마스크 {toolName}");
            SetYoloCommandStatus($"마스크 {toolName}: 이미지 위를 드래그하세요. 브러시 반경 {radius}px. 우클릭하면 현재 스트로크를 초기화합니다.", isBusy: false);
            AppendLog($"마스크 {toolName} 도구를 선택했습니다. 반경:{radius}px");
            RefreshCanvasWorkflowContext();
        }

        private void EndMaskAnnotationMode()
        {
            CompleteMaskAnnotationStroke();
            bool materializationScheduled = ScheduleQueuedMaskStrokeCommitsAfterToolEnd();
            lastMaskStrokePoint = null;
            CancelMaskStrokePreviewCommitSwap();
            MainCanvasViewModel?.ClearBrushCursorPreview();
            // Keep the Viewer2D-style GPU preview visible until the queued CPU
            // materialization publishes the committed mask overlay.
            if (!materializationScheduled)
            {
                MainCanvasViewModel?.ClearMaskStrokePreview();
            }

            if (activeAnnotationTool == WpfAnnotationTool.Brush || activeAnnotationTool == WpfAnnotationTool.Eraser)
            {
                activeAnnotationTool = WpfAnnotationTool.Select;
            }
        }
    }
}
