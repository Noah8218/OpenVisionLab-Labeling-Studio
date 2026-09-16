using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace MvcVisionSystem
{
    public static class AnnotationHistoryService
    {
        public static WpfAnnotationHistorySnapshot Capture(
            string actionName,
            IReadOnlyList<Rectangle> manualRois,
            IReadOnlyList<string> manualRoiClassNames,
            IReadOnlyList<CanvasRoiShapeKind> manualRoiShapeKinds,
            IReadOnlyList<LabelingSegmentationObject> manualSegments,
            IReadOnlyList<YoloWorkerSmokeCandidate> pendingCandidates,
            IReadOnlyList<YoloWorkerSmokeCandidate> confirmedCandidates)
        {
            return new WpfAnnotationHistorySnapshot(
                actionName,
                manualRois?.ToList() ?? new List<Rectangle>(),
                manualRoiClassNames?.ToList() ?? new List<string>(),
                manualRoiShapeKinds?.ToList() ?? new List<CanvasRoiShapeKind>(),
                (manualSegments ?? Array.Empty<LabelingSegmentationObject>())
                    .Select(CloneSegment)
                    .Where(item => item != null)
                    .ToList(),
                (pendingCandidates ?? Array.Empty<YoloWorkerSmokeCandidate>())
                    .Select(CloneCandidate)
                    .Where(item => item != null)
                    .ToList(),
                (confirmedCandidates ?? Array.Empty<YoloWorkerSmokeCandidate>())
                    .Select(CloneCandidate)
                    .Where(item => item != null)
                    .ToList());
        }

        public static WpfAnnotationHistorySnapshot CaptureManualRoiList(
            string actionName,
            IReadOnlyList<Rectangle> manualRois,
            IReadOnlyList<string> manualRoiClassNames,
            IReadOnlyList<CanvasRoiShapeKind> manualRoiShapeKinds)
        {
            // ROI delete undo should not clone unrelated mask buffers or AI candidates.
            return new WpfAnnotationHistorySnapshot(
                actionName,
                manualRois?.ToList() ?? new List<Rectangle>(),
                manualRoiClassNames?.ToList() ?? new List<string>(),
                manualRoiShapeKinds?.ToList() ?? new List<CanvasRoiShapeKind>(),
                Array.Empty<LabelingSegmentationObject>(),
                Array.Empty<YoloWorkerSmokeCandidate>(),
                Array.Empty<YoloWorkerSmokeCandidate>(),
                restoreManualRois: true,
                restoreManualSegments: false,
                restorePendingCandidates: false,
                restoreConfirmedCandidates: false);
        }

        public static WpfAnnotationHistorySnapshot CaptureMaskDeltaInverse(
            string actionName,
            WpfAnnotationHistorySnapshot snapshot,
            IReadOnlyList<LabelingSegmentationObject> manualSegments)
        {
            if (snapshot == null
                || snapshot.MaskSegmentDeltas.Count == 0
                || snapshot.RestoreManualRois
                || snapshot.RestoreManualSegments
                || snapshot.RestorePendingCandidates
                || snapshot.RestoreConfirmedCandidates)
            {
                return null;
            }

            var inverseDeltas = new List<WpfMaskSegmentHistoryDelta>(snapshot.MaskSegmentDeltas.Count);
            foreach (WpfMaskSegmentHistoryDelta delta in snapshot.MaskSegmentDeltas)
            {
                WpfMaskSegmentHistoryDelta inverse = CaptureMaskDeltaInverse(
                    delta,
                    snapshot.MaskSegmentDeltas,
                    manualSegments);
                if (inverse == null)
                {
                    return null;
                }

                inverseDeltas.Add(inverse);
            }

            return new WpfAnnotationHistorySnapshot(
                actionName,
                Array.Empty<Rectangle>(),
                Array.Empty<string>(),
                Array.Empty<CanvasRoiShapeKind>(),
                Array.Empty<LabelingSegmentationObject>(),
                Array.Empty<YoloWorkerSmokeCandidate>(),
                Array.Empty<YoloWorkerSmokeCandidate>(),
                inverseDeltas,
                restoreManualRois: false,
                restoreManualSegments: false,
                restorePendingCandidates: false,
                restoreConfirmedCandidates: false);
        }

        public static void Restore(
            WpfAnnotationHistorySnapshot snapshot,
            IList<Rectangle> manualRois,
            IList<string> manualRoiClassNames,
            IList<CanvasRoiShapeKind> manualRoiShapeKinds,
            IList<string> manualRoiOverlayIds,
            IList<LabelingSegmentationObject> manualSegments,
            IList<YoloWorkerSmokeCandidate> pendingCandidates,
            IList<YoloWorkerSmokeCandidate> confirmedCandidates)
        {
            if (snapshot == null)
            {
                return;
            }

            if (snapshot.RestoreManualRois)
            {
                Replace(manualRois, snapshot.ManualRois);
                Replace(manualRoiClassNames, snapshot.ManualRoiClassNames);
                Replace(manualRoiShapeKinds, snapshot.ManualRoiShapeKinds);
                manualRoiOverlayIds?.Clear();
            }

            if (snapshot.RestoreManualSegments)
            {
                Replace(manualSegments, snapshot.ManualSegments.Select(CloneSegment));
            }

            if (snapshot.MaskSegmentDeltas.Count > 0)
            {
                ApplyMaskSegmentDeltas(manualSegments, snapshot.MaskSegmentDeltas);
            }

            if (snapshot.RestorePendingCandidates)
            {
                Replace(pendingCandidates, snapshot.PendingCandidates.Select(CloneCandidate));
            }

            if (snapshot.RestoreConfirmedCandidates)
            {
                Replace(confirmedCandidates, snapshot.ConfirmedCandidates.Select(CloneCandidate));
            }
        }

        public static LabelingSegmentationObject CloneSegment(LabelingSegmentationObject source)
        {
            if (source == null)
            {
                return null;
            }

            return new LabelingSegmentationObject
            {
                ClassName = source.ClassName ?? string.Empty,
                ClassItem = CloneClassItem(source.ClassItem),
                ObjectId = source.ObjectId ?? string.Empty,
                ComponentIndex = source.ComponentIndex,
                ZOrder = source.ZOrder,
                LastStructuralOperation = source.LastStructuralOperation ?? string.Empty,
                Points = source.Points?.ToList() ?? new List<Point>(),
                CutoutPolygons = source.CutoutPolygons?
                    .Select(cutout => cutout?.ToList() ?? new List<Point>())
                    .ToList() ?? new List<List<Point>>(),
                MaskData = source.MaskData?.ToArray(),
                MaskSize = source.MaskSize,
                MaskBounds = source.MaskBounds,
                RenderVersion = source.RenderVersion,
                RenderDirtyBounds = source.RenderDirtyBounds,
                Selected = source.Selected
            };
        }

        public static YoloWorkerSmokeCandidate CloneCandidate(YoloWorkerSmokeCandidate source)
        {
            if (source == null)
            {
                return null;
            }

            return new YoloWorkerSmokeCandidate
            {
                Index = source.Index,
                ClassId = source.ClassId,
                ClassName = source.ClassName ?? string.Empty,
                Confidence = source.Confidence,
                X = source.X,
                Y = source.Y,
                Width = source.Width,
                Height = source.Height,
                CandidateType = source.CandidateType ?? string.Empty,
                PredictionType = source.PredictionType ?? string.Empty,
                ImageLevel = source.ImageLevel,
                AnomalyScore = source.AnomalyScore,
                AnomalyThreshold = source.AnomalyThreshold,
                HeatmapPath = source.HeatmapPath ?? string.Empty,
                SegmentationType = source.SegmentationType ?? string.Empty,
                PolygonPoints = ClonePolygonPoints(source.PolygonPoints),
                NormalizedPolygonPoints = ClonePolygonPoints(source.NormalizedPolygonPoints)
            };
        }

        private static IReadOnlyList<DetectionPolygonPoint> ClonePolygonPoints(
            IReadOnlyList<DetectionPolygonPoint> source)
        {
            return (source ?? Array.Empty<DetectionPolygonPoint>())
                .Where(point => point != null)
                .Select(point => new DetectionPolygonPoint { X = point.X, Y = point.Y })
                .ToList();
        }

        private static WpfMaskSegmentHistoryDelta CaptureMaskDeltaInverse(
            WpfMaskSegmentHistoryDelta delta,
            IReadOnlyList<WpfMaskSegmentHistoryDelta> siblingDeltas,
            IReadOnlyList<LabelingSegmentationObject> manualSegments)
        {
            if (delta == null)
            {
                return null;
            }

            if (delta.RestoreRemovedSegment)
            {
                return new WpfMaskSegmentHistoryDelta(
                    delta.SegmentIndex,
                    Rectangle.Empty,
                    Array.Empty<byte>(),
                    delta.MaskSize,
                    delta.MaskBounds,
                    delta.RenderVersion,
                    delta.RenderDirtyBounds,
                    delta.ClassName,
                    delta.ClassItem,
                    delta.ObjectId,
                    delta.ComponentIndex,
                    delta.ZOrder,
                    delta.LastStructuralOperation,
                    delta.Selected,
                    removeCreatedSegment: true);
            }

            LabelingSegmentationObject segment = ResolveMaskDeltaSource(manualSegments, delta, siblingDeltas);
            if (segment?.IsRasterMask != true)
            {
                return null;
            }

            Rectangle restoreBounds = delta.RemoveCreatedSegment
                ? segment.Bounds
                : Rectangle.Intersect(delta.RestoreBounds, new Rectangle(Point.Empty, segment.MaskSize));
            byte[] pixels = CopyMaskRegion(segment.MaskData, segment.MaskSize, restoreBounds);
            if (restoreBounds.IsEmpty || pixels.Length != restoreBounds.Width * restoreBounds.Height)
            {
                return null;
            }

            return new WpfMaskSegmentHistoryDelta(
                delta.SegmentIndex,
                restoreBounds,
                pixels,
                segment.MaskSize,
                segment.MaskBounds,
                segment.RenderVersion,
                segment.RenderDirtyBounds,
                segment.ClassName,
                segment.ClassItem,
                segment.ObjectId,
                segment.ComponentIndex,
                segment.ZOrder,
                segment.LastStructuralOperation,
                segment.Selected,
                restoreRemovedSegment: delta.RemoveCreatedSegment);
        }

        private static LabelingSegmentationObject ResolveMaskDeltaSource(
            IReadOnlyList<LabelingSegmentationObject> manualSegments,
            WpfMaskSegmentHistoryDelta delta,
            IReadOnlyList<WpfMaskSegmentHistoryDelta> siblingDeltas)
        {
            if (manualSegments == null || delta == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(delta.ObjectId))
            {
                LabelingSegmentationObject byId = manualSegments.FirstOrDefault(segment =>
                    string.Equals(segment?.ObjectId, delta.ObjectId, StringComparison.Ordinal));
                if (byId != null)
                {
                    return byId;
                }
            }

            int sourceIndex = delta.SegmentIndex - (siblingDeltas ?? Array.Empty<WpfMaskSegmentHistoryDelta>())
                .Count(item => item?.RestoreRemovedSegment == true && item.SegmentIndex < delta.SegmentIndex);
            return sourceIndex >= 0 && sourceIndex < manualSegments.Count
                ? manualSegments[sourceIndex]
                : null;
        }

        private static byte[] CopyMaskRegion(byte[] maskData, Size maskSize, Rectangle bounds)
        {
            if (maskData == null
                || maskSize.Width <= 0
                || maskSize.Height <= 0
                || maskData.Length != maskSize.Width * maskSize.Height
                || bounds.IsEmpty)
            {
                return Array.Empty<byte>();
            }

            var pixels = new byte[bounds.Width * bounds.Height];
            for (int y = 0; y < bounds.Height; y++)
            {
                int sourceOffset = ((bounds.Top + y) * maskSize.Width) + bounds.Left;
                Buffer.BlockCopy(maskData, sourceOffset, pixels, y * bounds.Width, bounds.Width);
            }

            return pixels;
        }

        private static LabelClass CloneClassItem(LabelClass source)
        {
            if (source == null)
            {
                return null;
            }

            return new LabelClass
            {
                Text = source.Text ?? string.Empty,
                IsArchived = source.IsArchived,
                DrawColor = source.DrawColor
            };
        }

        private static void Replace<T>(IList<T> target, IEnumerable<T> source)
        {
            if (target == null)
            {
                return;
            }

            target.Clear();
            foreach (T item in source ?? Enumerable.Empty<T>())
            {
                target.Add(item);
            }
        }

        private static void ApplyMaskSegmentDeltas(
            IList<LabelingSegmentationObject> manualSegments,
            IReadOnlyList<WpfMaskSegmentHistoryDelta> deltas)
        {
            if (manualSegments == null)
            {
                return;
            }

            IReadOnlyList<WpfMaskSegmentHistoryDelta> availableDeltas = (deltas ?? Array.Empty<WpfMaskSegmentHistoryDelta>())
                .Where(delta => delta != null)
                .ToList();
            IEnumerable<WpfMaskSegmentHistoryDelta> orderedDeltas = availableDeltas.Any(delta => delta.RemoveCreatedSegment)
                ? availableDeltas
                    .Where(delta => !delta.RemoveCreatedSegment)
                    .Concat(availableDeltas
                        .Where(delta => delta.RemoveCreatedSegment)
                        .OrderByDescending(delta => delta.SegmentIndex))
                : availableDeltas;
            foreach (WpfMaskSegmentHistoryDelta delta in orderedDeltas)
            {
                if (delta.RemoveCreatedSegment)
                {
                    if (delta.SegmentIndex >= 0 && delta.SegmentIndex < manualSegments.Count)
                    {
                        manualSegments.RemoveAt(delta.SegmentIndex);
                    }

                    continue;
                }

                LabelingSegmentationObject segment = ResolveMaskDeltaTarget(manualSegments, delta);
                if (segment == null)
                {
                    continue;
                }

                RestoreMaskDeltaPixels(segment, delta);
            }
        }

        private static LabelingSegmentationObject ResolveMaskDeltaTarget(
            IList<LabelingSegmentationObject> manualSegments,
            WpfMaskSegmentHistoryDelta delta)
        {
            if (delta.RestoreRemovedSegment)
            {
                var restored = new LabelingSegmentationObject(Array.Empty<Point>(), CloneClassItem(delta.ClassItem))
                {
                    ClassName = delta.ClassName ?? string.Empty,
                    ObjectId = delta.ObjectId ?? string.Empty,
                    ComponentIndex = delta.ComponentIndex,
                    ZOrder = delta.ZOrder,
                    LastStructuralOperation = delta.LastStructuralOperation ?? string.Empty,
                    MaskData = new byte[Math.Max(0, delta.MaskSize.Width * delta.MaskSize.Height)],
                    MaskSize = delta.MaskSize,
                    MaskBounds = delta.MaskBounds,
                    RenderVersion = delta.RenderVersion,
                    RenderDirtyBounds = delta.RestoreBounds,
                    Selected = delta.Selected
                };
                int insertIndex = Math.Max(0, Math.Min(delta.SegmentIndex, manualSegments.Count));
                manualSegments.Insert(insertIndex, restored);
                return restored;
            }

            if (!string.IsNullOrWhiteSpace(delta.ObjectId))
            {
                LabelingSegmentationObject byId = manualSegments.FirstOrDefault(segment =>
                    string.Equals(segment?.ObjectId, delta.ObjectId, StringComparison.Ordinal));
                if (byId != null)
                {
                    return byId;
                }
            }

            if (delta.SegmentIndex < 0 || delta.SegmentIndex >= manualSegments.Count)
            {
                return null;
            }

            return manualSegments[delta.SegmentIndex];
        }

        private static void RestoreMaskDeltaPixels(
            LabelingSegmentationObject segment,
            WpfMaskSegmentHistoryDelta delta)
        {
            if (segment?.MaskData == null
                || delta.Pixels == null
                || delta.RestoreBounds.IsEmpty
                || segment.MaskSize.Width != delta.MaskSize.Width
                || segment.MaskSize.Height != delta.MaskSize.Height)
            {
                return;
            }

            int width = delta.RestoreBounds.Width;
            int height = delta.RestoreBounds.Height;
            if (delta.Pixels.Length != width * height)
            {
                return;
            }

            for (int y = 0; y < height; y++)
            {
                int sourceOffset = y * width;
                int targetOffset = ((delta.RestoreBounds.Top + y) * segment.MaskSize.Width) + delta.RestoreBounds.Left;
                Buffer.BlockCopy(delta.Pixels, sourceOffset, segment.MaskData, targetOffset, width);
            }

            segment.ClassName = delta.ClassName ?? segment.ClassName;
            segment.ClassItem = CloneClassItem(delta.ClassItem) ?? segment.ClassItem;
            segment.ObjectId = delta.ObjectId ?? segment.ObjectId;
            segment.ComponentIndex = delta.ComponentIndex;
            segment.ZOrder = delta.ZOrder;
            segment.LastStructuralOperation = delta.LastStructuralOperation ?? segment.LastStructuralOperation;
            segment.MaskBounds = delta.MaskBounds;
            segment.RenderVersion = Math.Max(segment.RenderVersion + 1, delta.RenderVersion + 1);
            segment.RenderDirtyBounds = delta.RestoreBounds;
            segment.Selected = delta.Selected;
        }
    }

    [Obsolete("Use AnnotationHistoryService.", false)]
    public static class WpfAnnotationHistoryService
    {
        public static WpfAnnotationHistorySnapshot Capture(
            string actionName,
            IReadOnlyList<Rectangle> manualRois,
            IReadOnlyList<string> manualRoiClassNames,
            IReadOnlyList<CanvasRoiShapeKind> manualRoiShapeKinds,
            IReadOnlyList<LabelingSegmentationObject> manualSegments,
            IReadOnlyList<YoloWorkerSmokeCandidate> pendingCandidates,
            IReadOnlyList<YoloWorkerSmokeCandidate> confirmedCandidates)
            => AnnotationHistoryService.Capture(
                actionName,
                manualRois,
                manualRoiClassNames,
                manualRoiShapeKinds,
                manualSegments,
                pendingCandidates,
                confirmedCandidates);

        public static WpfAnnotationHistorySnapshot CaptureManualRoiList(
            string actionName,
            IReadOnlyList<Rectangle> manualRois,
            IReadOnlyList<string> manualRoiClassNames,
            IReadOnlyList<CanvasRoiShapeKind> manualRoiShapeKinds)
            => AnnotationHistoryService.CaptureManualRoiList(
                actionName,
                manualRois,
                manualRoiClassNames,
                manualRoiShapeKinds);

        public static WpfAnnotationHistorySnapshot CaptureMaskDeltaInverse(
            string actionName,
            WpfAnnotationHistorySnapshot snapshot,
            IReadOnlyList<LabelingSegmentationObject> manualSegments)
            => AnnotationHistoryService.CaptureMaskDeltaInverse(actionName, snapshot, manualSegments);

        public static void Restore(
            WpfAnnotationHistorySnapshot snapshot,
            IList<Rectangle> manualRois,
            IList<string> manualRoiClassNames,
            IList<CanvasRoiShapeKind> manualRoiShapeKinds,
            IList<string> manualRoiOverlayIds,
            IList<LabelingSegmentationObject> manualSegments,
            IList<YoloWorkerSmokeCandidate> pendingCandidates,
            IList<YoloWorkerSmokeCandidate> confirmedCandidates)
            => AnnotationHistoryService.Restore(
                snapshot,
                manualRois,
                manualRoiClassNames,
                manualRoiShapeKinds,
                manualRoiOverlayIds,
                manualSegments,
                pendingCandidates,
                confirmedCandidates);

        public static LabelingSegmentationObject CloneSegment(LabelingSegmentationObject source)
            => AnnotationHistoryService.CloneSegment(source);

        public static YoloWorkerSmokeCandidate CloneCandidate(YoloWorkerSmokeCandidate source)
            => AnnotationHistoryService.CloneCandidate(source);
    }

    public sealed class WpfAnnotationHistorySnapshot
    {
        public WpfAnnotationHistorySnapshot(
            string actionName,
            IReadOnlyList<Rectangle> manualRois,
            IReadOnlyList<string> manualRoiClassNames,
            IReadOnlyList<CanvasRoiShapeKind> manualRoiShapeKinds,
            IReadOnlyList<LabelingSegmentationObject> manualSegments,
            IReadOnlyList<YoloWorkerSmokeCandidate> pendingCandidates,
            IReadOnlyList<YoloWorkerSmokeCandidate> confirmedCandidates,
            IReadOnlyList<WpfMaskSegmentHistoryDelta> maskSegmentDeltas = null,
            bool restoreManualRois = true,
            bool restoreManualSegments = true,
            bool restorePendingCandidates = true,
            bool restoreConfirmedCandidates = true)
        {
            ActionName = string.IsNullOrWhiteSpace(actionName) ? "Edit" : actionName;
            ManualRois = manualRois ?? Array.Empty<Rectangle>();
            ManualRoiClassNames = manualRoiClassNames ?? Array.Empty<string>();
            ManualRoiShapeKinds = manualRoiShapeKinds ?? Array.Empty<CanvasRoiShapeKind>();
            ManualSegments = manualSegments ?? Array.Empty<LabelingSegmentationObject>();
            PendingCandidates = pendingCandidates ?? Array.Empty<YoloWorkerSmokeCandidate>();
            ConfirmedCandidates = confirmedCandidates ?? Array.Empty<YoloWorkerSmokeCandidate>();
            MaskSegmentDeltas = maskSegmentDeltas ?? Array.Empty<WpfMaskSegmentHistoryDelta>();
            RestoreManualRois = restoreManualRois;
            RestoreManualSegments = restoreManualSegments;
            RestorePendingCandidates = restorePendingCandidates;
            RestoreConfirmedCandidates = restoreConfirmedCandidates;
        }

        public string ActionName { get; }

        public IReadOnlyList<Rectangle> ManualRois { get; }

        public IReadOnlyList<string> ManualRoiClassNames { get; }

        public IReadOnlyList<CanvasRoiShapeKind> ManualRoiShapeKinds { get; }

        public IReadOnlyList<LabelingSegmentationObject> ManualSegments { get; }

        public IReadOnlyList<YoloWorkerSmokeCandidate> PendingCandidates { get; }

        public IReadOnlyList<YoloWorkerSmokeCandidate> ConfirmedCandidates { get; }

        public IReadOnlyList<WpfMaskSegmentHistoryDelta> MaskSegmentDeltas { get; }

        public bool RestoreManualRois { get; }

        public bool RestoreManualSegments { get; }

        public bool RestorePendingCandidates { get; }

        public bool RestoreConfirmedCandidates { get; }
    }

    public sealed class WpfMaskSegmentHistoryDelta
    {
        public WpfMaskSegmentHistoryDelta(
            int segmentIndex,
            Rectangle restoreBounds,
            byte[] pixels,
            Size maskSize,
            Rectangle maskBounds,
            int renderVersion,
            Rectangle renderDirtyBounds,
            string className,
            LabelClass classItem,
            string objectId,
            int componentIndex,
            int zOrder,
            string lastStructuralOperation,
            bool selected,
            bool removeCreatedSegment = false,
            bool restoreRemovedSegment = false)
        {
            SegmentIndex = segmentIndex;
            RestoreBounds = restoreBounds;
            Pixels = pixels ?? Array.Empty<byte>();
            MaskSize = maskSize;
            MaskBounds = maskBounds;
            RenderVersion = renderVersion;
            RenderDirtyBounds = renderDirtyBounds;
            ClassName = className ?? string.Empty;
            ClassItem = classItem == null
                ? null
                : new LabelClass
                {
                    Text = classItem.Text ?? string.Empty,
                    IsArchived = classItem.IsArchived,
                    DrawColor = classItem.DrawColor
                };
            ObjectId = objectId ?? string.Empty;
            ComponentIndex = componentIndex;
            ZOrder = zOrder;
            LastStructuralOperation = lastStructuralOperation ?? string.Empty;
            Selected = selected;
            RemoveCreatedSegment = removeCreatedSegment;
            RestoreRemovedSegment = restoreRemovedSegment;
        }

        public int SegmentIndex { get; }

        public Rectangle RestoreBounds { get; }

        public byte[] Pixels { get; }

        public Size MaskSize { get; }

        public Rectangle MaskBounds { get; }

        public int RenderVersion { get; }

        public Rectangle RenderDirtyBounds { get; }

        public string ClassName { get; }

        public LabelClass ClassItem { get; }

        public string ObjectId { get; }

        public int ComponentIndex { get; }

        public int ZOrder { get; }

        public string LastStructuralOperation { get; }

        public bool Selected { get; }

        public bool RemoveCreatedSegment { get; }

        public bool RestoreRemovedSegment { get; }
    }
}
