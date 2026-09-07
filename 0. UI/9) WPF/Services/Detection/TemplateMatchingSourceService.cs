using MvcVisionSystem._1._Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace MvcVisionSystem
{
    // Resolves a template source from a read-only snapshot; visual interaction state stays outside this policy owner.
    public class TemplateMatchingSourceService
    {
        public bool TryResolveTemplateMatchingSource(
            TemplateMatchingSourceSnapshot snapshot,
            out Rectangle templateBounds,
            out string className)
        {
            templateBounds = Rectangle.Empty;
            className = string.Empty;
            if (snapshot == null)
            {
                return false;
            }

            WpfObjectReviewItemRef selected = snapshot.SelectedObject;
            if (selected?.Source == WpfObjectReviewSource.ManualRoi
                && IsValidIndex(snapshot.ManualRois, selected.Index))
            {
                templateBounds = snapshot.ManualRois[selected.Index];
                className = ObjectReviewPresentationService.GetManualRoiClassName(
                    snapshot.ManualRoiClassNames,
                    selected.Index);
                return !templateBounds.IsEmpty;
            }

            if (selected?.Source == WpfObjectReviewSource.ManualSegment
                && TryResolveManualSegmentTemplateSource(
                    snapshot.ManualSegments,
                    selected.Index,
                    out templateBounds,
                    out className))
            {
                return true;
            }

            if (snapshot.ManualRois.Count == 1)
            {
                templateBounds = snapshot.ManualRois[0];
                className = ObjectReviewPresentationService.GetManualRoiClassName(
                    snapshot.ManualRoiClassNames,
                    0);
                return !templateBounds.IsEmpty;
            }

            return snapshot.ManualRois.Count == 0
                && snapshot.ManualSegments.Count == 1
                && TryResolveManualSegmentTemplateSource(
                    snapshot.ManualSegments,
                    0,
                    out templateBounds,
                    out className);
        }

        public bool TryResolveTemplateMatchingSourceSegment(
            TemplateMatchingSourceSnapshot snapshot,
            out IReadOnlyList<Point> points,
            out IReadOnlyList<IReadOnlyList<Point>> cutouts)
        {
            points = Array.Empty<Point>();
            cutouts = Array.Empty<IReadOnlyList<Point>>();
            int index = ResolveManualSegmentIndex(snapshot);
            return index >= 0
                && TryResolveManualSegmentTemplateShape(snapshot.ManualSegments, index, out points, out cutouts);
        }

        public bool TryResolveTemplateMatchingSourceMask(
            TemplateMatchingSourceSnapshot snapshot,
            out byte[] maskData,
            out Size maskSize,
            out Rectangle maskBounds)
        {
            maskData = Array.Empty<byte>();
            maskSize = Size.Empty;
            maskBounds = Rectangle.Empty;
            int index = ResolveManualSegmentIndex(snapshot);
            return index >= 0
                && TryResolveManualSegmentTemplateMask(
                    snapshot.ManualSegments,
                    index,
                    out maskData,
                    out maskSize,
                    out maskBounds);
        }

        public string GetManualSegmentClassName(LabelingSegmentationObject segment)
        {
            if (!string.IsNullOrWhiteSpace(segment?.ClassName))
            {
                return segment.ClassName;
            }

            if (!string.IsNullOrWhiteSpace(segment?.ClassItem?.Text))
            {
                return segment.ClassItem.Text;
            }

            return "Defect";
        }

        private bool TryResolveManualSegmentTemplateSource(
            IReadOnlyList<LabelingSegmentationObject> manualSegments,
            int index,
            out Rectangle templateBounds,
            out string className)
        {
            templateBounds = Rectangle.Empty;
            className = string.Empty;
            if (!IsValidIndex(manualSegments, index))
            {
                return false;
            }

            LabelingSegmentationObject segment = manualSegments[index];
            if (segment == null || segment.Bounds.IsEmpty)
            {
                return false;
            }

            templateBounds = segment.Bounds;
            className = GetManualSegmentClassName(segment);
            return true;
        }

        private static bool TryResolveManualSegmentTemplateShape(
            IReadOnlyList<LabelingSegmentationObject> manualSegments,
            int index,
            out IReadOnlyList<Point> points,
            out IReadOnlyList<IReadOnlyList<Point>> cutouts)
        {
            points = Array.Empty<Point>();
            cutouts = Array.Empty<IReadOnlyList<Point>>();
            if (!IsValidIndex(manualSegments, index))
            {
                return false;
            }

            LabelingSegmentationObject segment = manualSegments[index];
            if (segment?.Points == null || segment.Points.Count < 3)
            {
                return false;
            }

            points = segment.Points.ToList();
            cutouts = (segment.CutoutPolygons ?? new List<List<Point>>())
                .Where(cutout => cutout?.Count >= 3)
                .Select(cutout => (IReadOnlyList<Point>)cutout.ToList())
                .ToList();
            return true;
        }

        private static bool TryResolveManualSegmentTemplateMask(
            IReadOnlyList<LabelingSegmentationObject> manualSegments,
            int index,
            out byte[] maskData,
            out Size maskSize,
            out Rectangle maskBounds)
        {
            maskData = Array.Empty<byte>();
            maskSize = Size.Empty;
            maskBounds = Rectangle.Empty;
            if (!IsValidIndex(manualSegments, index))
            {
                return false;
            }

            LabelingSegmentationObject segment = manualSegments[index];
            if (segment?.IsRasterMask != true || segment.MaskData == null || segment.MaskSize.IsEmpty)
            {
                return false;
            }

            maskData = segment.MaskData.ToArray();
            maskSize = segment.MaskSize;
            maskBounds = segment.Bounds;
            return !maskBounds.IsEmpty;
        }

        private static int ResolveManualSegmentIndex(TemplateMatchingSourceSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return -1;
            }

            if (snapshot.SelectedObject?.Source == WpfObjectReviewSource.ManualSegment
                && IsValidIndex(snapshot.ManualSegments, snapshot.SelectedObject.Index))
            {
                return snapshot.SelectedObject.Index;
            }

            return snapshot.ManualRois.Count == 0 && snapshot.ManualSegments.Count == 1 ? 0 : -1;
        }

        private static bool IsValidIndex<T>(IReadOnlyList<T> items, int index)
            => items != null && index >= 0 && index < items.Count;
    }

    [Obsolete("Use TemplateMatchingSourceService.", false)]
    public sealed class WpfTemplateMatchingSourceService : TemplateMatchingSourceService
    {
    }

    public class TemplateMatchingSourceSnapshot
    {
        public TemplateMatchingSourceSnapshot(
            WpfObjectReviewItemRef selectedObject,
            IReadOnlyList<Rectangle> manualRois,
            IReadOnlyList<string> manualRoiClassNames,
            IReadOnlyList<LabelingSegmentationObject> manualSegments)
        {
            SelectedObject = selectedObject;
            ManualRois = manualRois ?? Array.Empty<Rectangle>();
            ManualRoiClassNames = manualRoiClassNames ?? Array.Empty<string>();
            ManualSegments = manualSegments ?? Array.Empty<LabelingSegmentationObject>();
        }

        public WpfObjectReviewItemRef SelectedObject { get; }

        public IReadOnlyList<Rectangle> ManualRois { get; }

        public IReadOnlyList<string> ManualRoiClassNames { get; }

        public IReadOnlyList<LabelingSegmentationObject> ManualSegments { get; }
    }

    [Obsolete("Use TemplateMatchingSourceSnapshot.", false)]
    public sealed class WpfTemplateMatchingSourceSnapshot : TemplateMatchingSourceSnapshot
    {
        public WpfTemplateMatchingSourceSnapshot(
            WpfObjectReviewItemRef selectedObject,
            IReadOnlyList<Rectangle> manualRois,
            IReadOnlyList<string> manualRoiClassNames,
            IReadOnlyList<LabelingSegmentationObject> manualSegments)
            : base(selectedObject, manualRois, manualRoiClassNames, manualSegments)
        {
        }
    }
}
