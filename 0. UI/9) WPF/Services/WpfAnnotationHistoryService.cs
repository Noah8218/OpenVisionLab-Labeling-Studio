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

        public bool RestoreManualRois { get; }

        public bool RestoreManualSegments { get; }

        public bool RestorePendingCandidates { get; }

        public bool RestoreConfirmedCandidates { get; }
    }
}
