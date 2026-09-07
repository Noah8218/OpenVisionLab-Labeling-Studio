using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace MvcVisionSystem
{
    public class SegmentationRemoveUnderlyingService
    {
        public const string StructuralOperationName = "RemoveUnderlying";

        public bool TryAnalyze(
            IReadOnlyList<LabelingSegmentationObject> segments,
            int selectedIndex,
            Size imageSize,
            out WpfSegmentationRemoveUnderlyingPlan plan,
            out string error)
        {
            plan = null;
            error = string.Empty;
            if (segments == null
                || segments.Count < 2
                || selectedIndex < 0
                || selectedIndex >= segments.Count
                || segments[selectedIndex] == null
                || imageSize.Width <= 0
                || imageSize.Height <= 0)
            {
                error = "\uB4A4\uCABD \uAC1D\uCCB4\uC640 \uACB9\uCE68\uC744 \uBD84\uC11D\uD560 \uC138\uADF8\uBA3C\uD2B8\uB97C \uD558\uB098 \uC120\uD0DD\uD558\uC138\uC694.";
                return false;
            }

            var rasterized = new List<RasterizedSegment>(segments.Count);
            for (int index = 0; index < segments.Count; index++)
            {
                LabelingSegmentationObject segment = segments[index];
                if (segment == null
                    || !SegmentationMaskGeometryService.TryRasterize(
                        segment,
                        imageSize,
                        out byte[] mask,
                        out Rectangle bounds))
                {
                    error = FormattableString.Invariant(
                        $"\uC138\uADF8\uBA3C\uD2B8 {index + 1}\uC758 \uC720\uD6A8\uD55C \uD3F4\uB9AC\uACE4/\uB9C8\uC2A4\uD06C geometry\uB97C \uC77D\uC744 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.");
                    return false;
                }

                rasterized.Add(new RasterizedSegment(segment, mask, bounds));
            }

            List<int> orderedIndices = Enumerable.Range(0, segments.Count)
                .OrderBy(index => segments[index].ZOrder)
                .ThenBy(index => index)
                .ToList();
            int selectedOrderIndex = orderedIndices.IndexOf(selectedIndex);
            if (selectedOrderIndex <= 0)
            {
                error = "\uC120\uD0DD\uD55C \uC138\uADF8\uBA3C\uD2B8 \uB4A4\uC5D0 \uBD84\uC11D\uD560 \uAC1D\uCCB4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.";
                return false;
            }

            RasterizedSegment selected = rasterized[selectedIndex];
            var changes = new List<WpfSegmentationRemoveUnderlyingChange>();
            long totalRemovedPixels = 0;
            for (int orderIndex = 0; orderIndex < selectedOrderIndex; orderIndex++)
            {
                int sourceIndex = orderedIndices[orderIndex];
                RasterizedSegment underlying = rasterized[sourceIndex];
                Rectangle intersection = Rectangle.Intersect(selected.Bounds, underlying.Bounds);
                if (intersection.IsEmpty)
                {
                    continue;
                }

                int removedPixels = CountOverlap(
                    selected.Mask,
                    underlying.Mask,
                    imageSize,
                    intersection);
                if (removedPixels <= 0)
                {
                    continue;
                }

                byte[] editedMask = underlying.Mask.ToArray();
                SubtractMask(selected.Mask, editedMask, imageSize, intersection);
                Rectangle remainingBounds = SegmentationGeometry.GetMaskBounds(editedMask, imageSize);
                LabelingSegmentationObject replacement = remainingBounds.IsEmpty
                    ? null
                    : CreateReplacement(underlying.Source, editedMask, imageSize, remainingBounds);
                changes.Add(new WpfSegmentationRemoveUnderlyingChange(
                    sourceIndex,
                    underlying.Source,
                    replacement,
                    removedPixels));
                totalRemovedPixels += removedPixels;
            }

            if (changes.Count == 0)
            {
                error = "\uC120\uD0DD\uD55C \uC138\uADF8\uBA3C\uD2B8\uC640 \uACB9\uCE58\uB294 \uB4A4\uCABD \uAC1D\uCCB4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.";
                return false;
            }

            plan = new WpfSegmentationRemoveUnderlyingPlan(
                selectedIndex,
                segments[selectedIndex],
                changes,
                totalRemovedPixels,
                ComputeSignature(segments, rasterized, selectedIndex, imageSize));
            return true;
        }

        private static int CountOverlap(
            byte[] selectedMask,
            byte[] underlyingMask,
            Size imageSize,
            Rectangle bounds)
        {
            int count = 0;
            for (int y = bounds.Top; y < bounds.Bottom; y++)
            {
                int rowOffset = y * imageSize.Width;
                for (int x = bounds.Left; x < bounds.Right; x++)
                {
                    int index = rowOffset + x;
                    if (selectedMask[index] != 0 && underlyingMask[index] != 0)
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        private static void SubtractMask(
            byte[] selectedMask,
            byte[] editedMask,
            Size imageSize,
            Rectangle bounds)
        {
            for (int y = bounds.Top; y < bounds.Bottom; y++)
            {
                int rowOffset = y * imageSize.Width;
                for (int x = bounds.Left; x < bounds.Right; x++)
                {
                    int index = rowOffset + x;
                    if (selectedMask[index] != 0)
                    {
                        editedMask[index] = 0;
                    }
                }
            }
        }

        private static LabelingSegmentationObject CreateReplacement(
            LabelingSegmentationObject source,
            byte[] mask,
            Size imageSize,
            Rectangle bounds)
            => new LabelingSegmentationObject
            {
                ClassName = ResolveClassName(source),
                ClassItem = source.ClassItem,
                ObjectId = source.ObjectId ?? string.Empty,
                ComponentIndex = -1,
                ZOrder = source.ZOrder,
                LastStructuralOperation = StructuralOperationName,
                MaskData = mask,
                MaskSize = imageSize,
                MaskBounds = bounds,
                RenderVersion = Math.Max(1, source.RenderVersion + 1),
                RenderDirtyBounds = bounds,
                Selected = false
            };

        private static string ResolveClassName(LabelingSegmentationObject segment)
        {
            string className = segment?.ClassName;
            if (string.IsNullOrWhiteSpace(className))
            {
                className = segment?.ClassItem?.Text;
            }

            return string.IsNullOrWhiteSpace(className) ? "Defect" : className.Trim();
        }

        private static string ComputeSignature(
            IReadOnlyList<LabelingSegmentationObject> segments,
            IReadOnlyList<RasterizedSegment> rasterized,
            int selectedIndex,
            Size imageSize)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;

            AddInt(ref hash, imageSize.Width, prime);
            AddInt(ref hash, imageSize.Height, prime);
            AddInt(ref hash, selectedIndex, prime);
            AddInt(ref hash, segments.Count, prime);
            for (int index = 0; index < segments.Count; index++)
            {
                LabelingSegmentationObject segment = segments[index];
                AddInt(ref hash, index, prime);
                AddInt(ref hash, segment.ZOrder, prime);
                AddString(ref hash, segment.ObjectId, prime);
                AddString(ref hash, ResolveClassName(segment), prime);
                AddString(ref hash, segment.LastStructuralOperation, prime);
                byte[] mask = rasterized[index].Mask;
                for (int pixel = 0; pixel < mask.Length; pixel++)
                {
                    hash ^= mask[pixel];
                    hash *= prime;
                }
            }

            return hash.ToString("X16");
        }

        private static void AddInt(ref ulong hash, int value, ulong prime)
        {
            unchecked
            {
                for (int shift = 0; shift < 32; shift += 8)
                {
                    hash ^= (byte)(value >> shift);
                    hash *= prime;
                }
            }
        }

        private static void AddString(ref ulong hash, string value, ulong prime)
        {
            foreach (byte item in Encoding.UTF8.GetBytes(value ?? string.Empty))
            {
                hash ^= item;
                hash *= prime;
            }

            hash ^= 0xFF;
            hash *= prime;
        }

        private sealed class RasterizedSegment
        {
            public RasterizedSegment(
                LabelingSegmentationObject source,
                byte[] mask,
                Rectangle bounds)
            {
                Source = source;
                Mask = mask;
                Bounds = bounds;
            }

            public LabelingSegmentationObject Source { get; }

            public byte[] Mask { get; }

            public Rectangle Bounds { get; }
        }
    }

    [Obsolete("Use SegmentationRemoveUnderlyingService.", false)]
    public sealed class WpfSegmentationRemoveUnderlyingService : SegmentationRemoveUnderlyingService
    {
    }

    public sealed class WpfSegmentationRemoveUnderlyingPlan
    {
        public WpfSegmentationRemoveUnderlyingPlan(
            int selectedIndex,
            LabelingSegmentationObject selectedSource,
            IReadOnlyList<WpfSegmentationRemoveUnderlyingChange> changes,
            long removedPixelCount,
            string signature)
        {
            SelectedIndex = selectedIndex;
            SelectedSource = selectedSource;
            Changes = changes ?? Array.Empty<WpfSegmentationRemoveUnderlyingChange>();
            RemovedPixelCount = removedPixelCount;
            Signature = signature ?? string.Empty;
        }

        public int SelectedIndex { get; }

        public LabelingSegmentationObject SelectedSource { get; }

        public IReadOnlyList<WpfSegmentationRemoveUnderlyingChange> Changes { get; }

        public long RemovedPixelCount { get; }

        public string Signature { get; }

        public int RemovedObjectCount => Changes.Count(change => change.Replacement == null);
    }

    public sealed class WpfSegmentationRemoveUnderlyingChange
    {
        public WpfSegmentationRemoveUnderlyingChange(
            int sourceIndex,
            LabelingSegmentationObject source,
            LabelingSegmentationObject replacement,
            int removedPixelCount)
        {
            SourceIndex = sourceIndex;
            Source = source;
            Replacement = replacement;
            RemovedPixelCount = removedPixelCount;
        }

        public int SourceIndex { get; }

        public LabelingSegmentationObject Source { get; }

        public LabelingSegmentationObject Replacement { get; }

        public int RemovedPixelCount { get; }
    }
}
