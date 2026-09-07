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
    // Responsibility group: annotation pointer input and selected-segment editing.
    // These members remain WPF Window adapters; independent policy belongs in services.
    public partial class WpfLabelingShellWindow
    {
        #region Annotation
        private void MainCanvasViewModel_ImagePointClicked(object sender, CanvasImagePointEventArgs e)
        {
            if (isApplicationCloseApproved || e == null)
            {
                return;
            }

            if (TryHandleFourPointBoxInput(e))
            {
                return;
            }

            if (TryApplyPendingPolygonVertexEdit(e))
            {
                return;
            }

            if (TryHandlePendingIntelligentScissors(e))
            {
                return;
            }

            if (TryApplyPendingSegmentationSplit(e))
            {
                return;
            }

            if (TryApplyPendingSegmentationHoleEdit(e))
            {
                return;
            }

            if (TryApplySmartMaskPointInput(e))
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
            if (isApplicationCloseApproved || e == null)
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
            if (isApplicationCloseApproved)
            {
                return;
            }

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
        #endregion

        #region AnnotationSegmentEdit
        // Selected segment editing is isolated from brush stroke commits because it mutates existing objects directly.
        private bool TryBeginSelectedSegmentEdit(CanvasImagePointEventArgs e)
        {
            if (e == null || e.Button != CanvasPointerButton.Left || activeImageSize.IsEmpty)
            {
                return false;
            }

            if (!TryGetSelectedObjectReviewItem(out WpfObjectReviewItemRef item)
                || item.Source != WpfObjectReviewSource.ManualSegment
                || item.Index < 0
                || item.Index >= manualSegments.Count)
            {
                return false;
            }

            LabelingSegmentationObject segment = manualSegments[item.Index];
            if (segment == null || !CanEditManualSegment(segment))
            {
                return false;
            }

            int pointIndex = -1;
            if (segment.IsRasterMask)
            {
                if (!maskAnnotationService.IsPixelHit(segment, e.ImagePoint))
                {
                    return false;
                }
            }
            else
            {
                pointIndex = PolygonAnnotationService.FindNearestPointIndex(segment, e.ImagePoint, maxDistancePixels: 8);
                if (pointIndex < 0 && !PolygonAnnotationService.IsPointInsidePolygon(segment, e.ImagePoint))
                {
                    return false;
                }
            }

            WpfObjectSessionState sessionState = objectSessionStateService.GetManualSegmentState(segment);
            if (sessionState.IsPinned && (segment.IsRasterMask || pointIndex < 0))
            {
                SetYoloCommandStatus(
                    "\uC774\uB3D9 \uACE0\uC815\uB41C \uAC1D\uCCB4\uC785\uB2C8\uB2E4. \uACE0\uC815\uC744 \uD574\uC81C\uD558\uBA74 \uC804\uCCB4 \uC704\uCE58\uB97C \uC62E\uAE38 \uC218 \uC788\uC2B5\uB2C8\uB2E4.",
                    isBusy: false);
                return false;
            }

            activeSegmentDragIndex = item.Index;
            activePolygonPointDragIndex = pointIndex;
            lastSegmentDragPoint = e.ImagePoint;
            activeSegmentDragChanged = false;
            activeSegmentDragSnapshot = CaptureAnnotationHistory(segment.IsRasterMask
                ? "Move mask"
                : pointIndex >= 0 ? "Move polygon point" : "Move polygon");
            RefreshPolygonOverlays();
            SetYoloCommandStatus(segment.IsRasterMask
                ? "Mask selected: drag to move it."
                : pointIndex >= 0
                    ? $"Polygon point {pointIndex + 1} selected: drag to move it."
                    : "Polygon selected: drag inside to move it.",
                isBusy: false);
            return true;
        }

        private bool TryMoveSelectedSegmentEdit(CanvasImagePointEventArgs e)
        {
            if (e == null
                || e.Button != CanvasPointerButton.Left
                || activeSegmentDragIndex < 0
                || activeSegmentDragIndex >= manualSegments.Count
                || !lastSegmentDragPoint.HasValue)
            {
                return false;
            }

            LabelingSegmentationObject segment = manualSegments[activeSegmentDragIndex];
            if (segment == null)
            {
                return false;
            }

            bool changed;
            if (segment.IsRasterMask)
            {
                System.Drawing.Point previous = lastSegmentDragPoint.Value;
                changed = maskAnnotationService.TryMoveRasterMask(
                    segment,
                    e.ImagePoint.X - previous.X,
                    e.ImagePoint.Y - previous.Y,
                    activeImageSize,
                    out _);
            }
            else
            {
                System.Drawing.Point previous = lastSegmentDragPoint.Value;
                changed = activePolygonPointDragIndex >= 0
                    ? PolygonAnnotationService.TryMovePoint(
                        segment,
                        activePolygonPointDragIndex,
                        e.ImagePoint,
                        activeImageSize,
                        out _)
                    : PolygonAnnotationService.TryMovePolygon(
                        segment,
                        e.ImagePoint.X - previous.X,
                        e.ImagePoint.Y - previous.Y,
                        activeImageSize,
                        out _);
            }

            if (!changed)
            {
                return true;
            }

            lastSegmentDragPoint = e.ImagePoint;
            activeSegmentDragChanged = true;
            RefreshPolygonOverlays();
            return true;
        }

        private void CompleteSelectedSegmentEdit()
        {
            bool changed = activeSegmentDragChanged;
            bool movedPoint = activePolygonPointDragIndex >= 0;
            if (activeSegmentDragSnapshot != null && activeSegmentDragChanged)
            {
                PushAnnotationHistorySnapshot(activeSegmentDragSnapshot);
                AppendLog(movedPoint
                    ? "Polygon point moved."
                    : "Mask or polygon moved.");
            }

            activeSegmentDragIndex = -1;
            activePolygonPointDragIndex = -1;
            lastSegmentDragPoint = null;
            activeSegmentDragSnapshot = null;
            activeSegmentDragChanged = false;
            RefreshPolygonOverlays();
            if (changed)
            {
                MarkAnnotationsDirty(movedPoint
                    ? "Move polygon point"
                    : "Move polygon");
                RefreshObjectList();
                RefreshActiveImageQueueStatus(hasActiveCandidates: pendingDetectionCandidates.Count > 0);
            }
        }

        #endregion

    }
}
