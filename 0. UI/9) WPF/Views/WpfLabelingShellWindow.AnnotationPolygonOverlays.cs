using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.Canvas;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using OpenVisionLab.ImageCanvas.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using DrawingRectangle = System.Drawing.Rectangle;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Polygon completion and overlay refresh are grouped away from ROI rectangle synchronization.
        private void CompletePolygonAnnotation()
        {
            CClassItem classItem = EnsureClassItem(FirstNonEmpty(GetSelectedClassName(), "Defect"));
            if (!polygonAnnotationService.TryComplete(classItem, activeImageSize, out LabelingSegmentationObject annotation, out string message))
            {
                SetYoloCommandStatus(message, isBusy: false);
                return;
            }

            RegisterAnnotationHistoryBeforeChange("Add polygon");
            manualSegments.Add(annotation);
            polygonAnnotationService.Reset();
            RefreshPolygonOverlays();
            RefreshObjectList();
            ObjectsReviewTab.IsSelected = true;
            SetModelStatus($"Polygon added: {annotation.ClassName} / {annotation.Points.Count} points");
            AppendLog($"Polygon added: {annotation.ClassName} / {annotation.Points.Count} points / {FormatSegmentBoundsCompact(annotation)}");
            RefreshActiveImageQueueStatus(hasActiveCandidates: pendingDetectionCandidates.Count > 0);
        }

        private void RefreshPolygonOverlays()
        {
            if (MainCanvasViewModel == null)
            {
                return;
            }

            if (!IsSegmentationDatasetPurposeActive())
            {
                MainCanvasViewModel.SetSegmentationOverlays(
                    Array.Empty<RoiImageCanvasPolygonOverlay>(),
                    Array.Empty<RoiImageCanvasMaskOverlay>());
                return;
            }

            var overlays = new List<RoiImageCanvasPolygonOverlay>();
            var maskOverlays = new List<RoiImageCanvasMaskOverlay>();
            float maskOpacity = (float)(LearningWorkflowViewModel?.MaskOpacity ?? 0.66);
            WpfObjectReviewListItem selectedObject = ObjectReviewViewModel?.SelectedObject;
            string selectedSourceKey = selectedObject?.SourceKey ?? string.Empty;
            int selectedSourceIndex = selectedObject?.SourceIndex ?? -1;
            for (int i = 0; i < manualSegments.Count; i++)
            {
                LabelingSegmentationObject segment = manualSegments[i];
                if (segment == null)
                {
                    continue;
                }

                string className = FirstNonEmpty(segment.ClassName, segment.ClassItem?.Text, "Defect");
                bool isSegmentSelected = string.Equals(
                    selectedSourceKey,
                    WpfObjectReviewSource.ManualSegment.ToString(),
                    StringComparison.OrdinalIgnoreCase)
                    && selectedSourceIndex == i;
                if (segment.IsRasterMask)
                {
                    if (TryBuildManualMaskOverlay(i, selectedSourceKey, selectedSourceIndex, maskOpacity, out RoiImageCanvasMaskOverlay maskOverlay))
                    {
                        maskOverlays.Add(maskOverlay);
                    }

                    continue;
                }

                if (segment.Points == null || segment.Points.Count == 0)
                {
                    continue;
                }

                overlays.Add(new RoiImageCanvasPolygonOverlay(
                    segment.Points,
                    $"SEG {i + 1} {className}",
                    segment.Color,
                    isClosed: true,
                    isDraft: false,
                    isSelected: isSegmentSelected,
                    selectedPointIndex: activeSegmentDragIndex == i ? activePolygonPointDragIndex : -1));
            }

            if (polygonAnnotationService.Points.Count > 0)
            {
                overlays.Add(new RoiImageCanvasPolygonOverlay(
                    polygonAnnotationService.Points,
                    $"Draft {polygonAnnotationService.Points.Count}",
                    System.Drawing.Color.FromArgb(80, 180, 255),
                    polygonAnnotationService.IsClosed,
                    isDraft: true));
            }

            MainCanvasViewModel.SetSegmentationOverlays(overlays, maskOverlays);
        }
    }
}
