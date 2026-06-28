using MvcVisionSystem._1._Core;
using OpenVisionLab.ImageCanvas.Canvas;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
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
            if (segment == null)
            {
                return false;
            }

            int pointIndex = -1;
            if (segment.IsRasterMask)
            {
                if (!IsMaskPixelHit(segment, e.ImagePoint))
                {
                    return false;
                }
            }
            else
            {
                pointIndex = WpfPolygonAnnotationService.FindNearestPointIndex(segment, e.ImagePoint, maxDistancePixels: 8);
                if (pointIndex < 0 && !WpfPolygonAnnotationService.IsPointInsidePolygon(segment, e.ImagePoint))
                {
                    return false;
                }
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
                    ? WpfPolygonAnnotationService.TryMovePoint(
                        segment,
                        activePolygonPointDragIndex,
                        e.ImagePoint,
                        activeImageSize,
                        out _)
                    : WpfPolygonAnnotationService.TryMovePolygon(
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
            RefreshObjectList();
            RefreshActiveImageQueueStatus(hasActiveCandidates: pendingDetectionCandidates.Count > 0);
            return true;
        }

        private void CompleteSelectedSegmentEdit()
        {
            if (activeSegmentDragSnapshot != null && activeSegmentDragChanged)
            {
                PushAnnotationHistorySnapshot(activeSegmentDragSnapshot);
                AppendLog(activePolygonPointDragIndex >= 0
                    ? "Polygon point moved."
                    : "Mask or polygon moved.");
            }

            activeSegmentDragIndex = -1;
            activePolygonPointDragIndex = -1;
            lastSegmentDragPoint = null;
            activeSegmentDragSnapshot = null;
            activeSegmentDragChanged = false;
            RefreshPolygonOverlays();
        }

        private static bool IsMaskPixelHit(LabelingSegmentationObject segment, System.Drawing.Point imagePoint)
        {
            if (segment?.IsRasterMask != true || segment.Bounds.IsEmpty || !segment.Bounds.Contains(imagePoint))
            {
                return false;
            }

            int index = (imagePoint.Y * segment.MaskSize.Width) + imagePoint.X;
            return index >= 0 && index < segment.MaskData.Length && segment.MaskData[index] != 0;
        }

    }
}
