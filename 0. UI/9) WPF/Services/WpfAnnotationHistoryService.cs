using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace MvcVisionSystem
{
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
                Height = source.Height
            };
        }

        private static CClassItem CloneClassItem(CClassItem source)
        {
            if (source == null)
            {
                return null;
            }

            return new CClassItem
            {
                Text = source.Text ?? string.Empty,
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

            foreach (WpfMaskSegmentHistoryDelta delta in deltas ?? Array.Empty<WpfMaskSegmentHistoryDelta>())
            {
                if (delta == null)
                {
                    continue;
                }

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
            segment.MaskBounds = delta.MaskBounds;
            segment.RenderVersion = Math.Max(segment.RenderVersion + 1, delta.RenderVersion + 1);
            segment.RenderDirtyBounds = delta.RestoreBounds;
            segment.Selected = delta.Selected;
        }
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
            CClassItem classItem,
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
                : new CClassItem
                {
                    Text = classItem.Text ?? string.Empty,
                    DrawColor = classItem.DrawColor
                };
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

        public CClassItem ClassItem { get; }

        public bool Selected { get; }

        public bool RemoveCreatedSegment { get; }

        public bool RestoreRemovedSegment { get; }
    }
}
