using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.Canvas;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the transient state and policy for moving a selected manual mask or polygon.
    /// The Shell supplies the live annotation collections and presentation callbacks.
    /// </summary>
    internal sealed class AnnotationSegmentEditAdapter
    {
        private readonly AnnotationSegmentEditAdapterContext context;
        private int activeSegmentDragIndex = -1;
        private int activePolygonPointDragIndex = -1;
        private Point? lastSegmentDragPoint;
        private WpfAnnotationHistorySnapshot activeSegmentDragSnapshot;
        private bool activeSegmentDragChanged;

        internal AnnotationSegmentEditAdapter(AnnotationSegmentEditAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        internal int ActiveSegmentDragIndex => activeSegmentDragIndex;

        internal int ActivePolygonPointDragIndex => activePolygonPointDragIndex;

        internal bool TryBeginSelectedSegmentEdit(CanvasImagePointEventArgs e)
        {
            Size activeImageSize = context.ActiveImageSizeProvider?.Invoke() ?? Size.Empty;
            if (e == null || e.Button != CanvasPointerButton.Left || activeImageSize.IsEmpty)
            {
                return false;
            }

            WpfObjectReviewItemRef item = context.SelectedObjectReviewItemProvider?.Invoke();
            if (item == null
                || item.Source != WpfObjectReviewSource.ManualSegment
                || item.Index < 0
                || item.Index >= context.ManualSegments.Count)
            {
                return false;
            }

            LabelingSegmentationObject segment = context.ManualSegments[item.Index];
            if (segment == null || context.CanEditManualSegment?.Invoke(segment) != true)
            {
                return false;
            }

            int pointIndex = -1;
            if (segment.IsRasterMask)
            {
                if (context.MaskAnnotationService?.IsPixelHit(segment, e.ImagePoint) != true)
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

            WpfObjectSessionState sessionState = context.ObjectSessionStateService?.GetManualSegmentState(segment);
            if (sessionState?.IsPinned == true && (segment.IsRasterMask || pointIndex < 0))
            {
                context.SetYoloCommandStatus?.Invoke(
                    "이동 고정된 객체입니다. 고정을 해제하면 전체 위치를 옮길 수 있습니다.",
                    false);
                return false;
            }

            activeSegmentDragIndex = item.Index;
            activePolygonPointDragIndex = pointIndex;
            lastSegmentDragPoint = e.ImagePoint;
            activeSegmentDragChanged = false;
            activeSegmentDragSnapshot = context.CaptureAnnotationHistory?.Invoke(segment.IsRasterMask
                ? "Move mask"
                : pointIndex >= 0 ? "Move polygon point" : "Move polygon");
            context.RefreshPolygonOverlays?.Invoke();
            context.SetYoloCommandStatus?.Invoke(
                segment.IsRasterMask
                    ? "Mask selected: drag to move it."
                    : pointIndex >= 0
                        ? $"Polygon point {pointIndex + 1} selected: drag to move it."
                        : "Polygon selected: drag inside to move it.",
                false);
            return true;
        }

        internal bool TryMoveSelectedSegmentEdit(CanvasImagePointEventArgs e)
        {
            Size activeImageSize = context.ActiveImageSizeProvider?.Invoke() ?? Size.Empty;
            if (e == null
                || e.Button != CanvasPointerButton.Left
                || activeSegmentDragIndex < 0
                || activeSegmentDragIndex >= context.ManualSegments.Count
                || !lastSegmentDragPoint.HasValue)
            {
                return false;
            }

            LabelingSegmentationObject segment = context.ManualSegments[activeSegmentDragIndex];
            if (segment == null)
            {
                return false;
            }

            bool changed;
            Point previous = lastSegmentDragPoint.Value;
            if (segment.IsRasterMask)
            {
                changed = context.MaskAnnotationService?.TryMoveRasterMask(
                    segment,
                    e.ImagePoint.X - previous.X,
                    e.ImagePoint.Y - previous.Y,
                    activeImageSize,
                    out _) == true;
            }
            else
            {
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
            context.RefreshPolygonOverlays?.Invoke();
            return true;
        }

        internal void CompleteSelectedSegmentEdit()
        {
            bool changed = activeSegmentDragChanged;
            bool movedPoint = activePolygonPointDragIndex >= 0;
            if (activeSegmentDragSnapshot != null && activeSegmentDragChanged)
            {
                context.PushAnnotationHistorySnapshot?.Invoke(activeSegmentDragSnapshot, true);
                context.AppendLog?.Invoke(movedPoint
                    ? "Polygon point moved."
                    : "Mask or polygon moved.");
            }

            activeSegmentDragIndex = -1;
            activePolygonPointDragIndex = -1;
            lastSegmentDragPoint = null;
            activeSegmentDragSnapshot = null;
            activeSegmentDragChanged = false;
            context.RefreshPolygonOverlays?.Invoke();
            if (changed)
            {
                context.MarkAnnotationsDirty?.Invoke(movedPoint
                    ? "Move polygon point"
                    : "Move polygon");
                context.RefreshObjectList?.Invoke();
                context.RefreshActiveImageQueueStatus?.Invoke(context.PendingCandidateCountProvider?.Invoke() > 0);
            }
        }
    }

    internal sealed class AnnotationSegmentEditAdapterContext
    {
        internal Func<Size> ActiveImageSizeProvider { get; init; }
        internal Func<WpfObjectReviewItemRef> SelectedObjectReviewItemProvider { get; init; }
        internal IList<LabelingSegmentationObject> ManualSegments { get; init; }
        internal Func<LabelingSegmentationObject, bool> CanEditManualSegment { get; init; }
        internal MaskAnnotationService MaskAnnotationService { get; init; }
        internal ObjectSessionStateService ObjectSessionStateService { get; init; }
        internal Func<string, WpfAnnotationHistorySnapshot> CaptureAnnotationHistory { get; init; }
        internal Action<WpfAnnotationHistorySnapshot, bool> PushAnnotationHistorySnapshot { get; init; }
        internal Action RefreshPolygonOverlays { get; init; }
        internal Action<string, bool> SetYoloCommandStatus { get; init; }
        internal Action<string> AppendLog { get; init; }
        internal Action<string> MarkAnnotationsDirty { get; init; }
        internal Action RefreshObjectList { get; init; }
        internal Func<int> PendingCandidateCountProvider { get; init; }
        internal Action<bool> RefreshActiveImageQueueStatus { get; init; }
    }
}
