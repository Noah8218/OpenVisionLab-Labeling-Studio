using MvcVisionSystem.Yolo;
using System;
using System.Collections.Concurrent;
using System.Buffers;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace MvcVisionSystem
{
    public sealed class WpfMaskAnnotationService
    {
        public const int DefaultBrushRadius = 6;
        private static readonly ConcurrentDictionary<int, BrushStamp> BrushStampCache = new ConcurrentDictionary<int, BrushStamp>();

        public IReadOnlyList<Point> BuildStrokeCenters(Point? previous, Point current, int radius)
        {
            if (!previous.HasValue)
            {
                return new[] { current };
            }

            Point from = previous.Value;
            int dx = current.X - from.X;
            int dy = current.Y - from.Y;
            double distance = Math.Sqrt((dx * dx) + (dy * dy));
            int step = Math.Max(1, Math.Max(1, radius) / 2);
            int count = Math.Max(1, (int)Math.Ceiling(distance / step));
            var points = new List<Point> { from };
            Point last = from;
            for (int i = 1; i <= count; i++)
            {
                double ratio = i / (double)count;
                var point = new Point(
                    (int)Math.Round(from.X + (dx * ratio)),
                    (int)Math.Round(from.Y + (dy * ratio)));
                if (point == last)
                {
                    continue;
                }

                points.Add(point);
                last = point;
            }

            return points;
        }

        public bool Paint(
            IList<LabelingSegmentationObject> segments,
            IEnumerable<Point> centers,
            int radius,
            Size imageSize,
            CClassItem classItem,
            out LabelingSegmentationObject changedSegment,
            out Rectangle changedBounds)
        {
            changedSegment = null;
            changedBounds = Rectangle.Empty;
            if (segments == null || imageSize.Width <= 0 || imageSize.Height <= 0)
            {
                return false;
            }

            List<Point> strokeCenters = NormalizeCenters(centers, imageSize);
            if (strokeCenters.Count == 0)
            {
                return false;
            }

            CClassItem targetClass = ResolveClass(classItem);
            LabelingSegmentationObject segment = GetOrCreateRasterSegment(segments, imageSize, targetClass);
            if (!ApplyBrushToMask(segment.MaskData, segment.MaskSize, strokeCenters, radius, paint: true, out changedBounds))
            {
                return false;
            }

            segment.MaskBounds = segment.MaskBounds.IsEmpty
                ? changedBounds
                : Rectangle.Union(segment.MaskBounds, changedBounds);
            MarkRenderDirty(segment, changedBounds);
            changedSegment = segment;
            return true;
        }

        public bool Erase(
            IList<LabelingSegmentationObject> segments,
            IEnumerable<Point> centers,
            int radius,
            Size imageSize,
            out Rectangle changedBounds)
            => Erase(segments, centers, radius, imageSize, out changedBounds, out _);

        public bool Erase(
            IList<LabelingSegmentationObject> segments,
            IEnumerable<Point> centers,
            int radius,
            Size imageSize,
            out Rectangle changedBounds,
            out IReadOnlyList<LabelingSegmentationObject> changedSegments)
        {
            changedBounds = Rectangle.Empty;
            changedSegments = Array.Empty<LabelingSegmentationObject>();
            if (segments == null || segments.Count == 0 || imageSize.Width <= 0 || imageSize.Height <= 0)
            {
                return false;
            }

            List<Point> strokeCenters = NormalizeCenters(centers, imageSize);
            if (strokeCenters.Count == 0)
            {
                return false;
            }

            var changedSegmentList = new List<LabelingSegmentationObject>();
            Rectangle strokeBounds = GetStrokeBounds(strokeCenters, radius, imageSize);
            bool changed = false;
            foreach (LabelingSegmentationObject segment in segments.Where(item => item?.IsRasterMask == true).ToList())
            {
                Rectangle currentBounds = segment.MaskBounds.IsEmpty ? segment.Bounds : segment.MaskBounds;
                if (currentBounds.IsEmpty || !currentBounds.IntersectsWith(strokeBounds))
                {
                    continue;
                }

                if (!ApplyBrushToMask(segment.MaskData, segment.MaskSize, strokeCenters, radius, paint: false, out Rectangle segmentChangedBounds, currentBounds))
                {
                    continue;
                }

                // Eraser can touch large image-sized buffers. If it reaches an
                // active edge, shrink only that edge instead of rescanning the full
                // active rectangle; this keeps MouseUp/tool-end materialization bounded.
                segment.MaskBounds = ShouldRecalculateMaskBoundsAfterErase(currentBounds, segmentChangedBounds)
                    ? GetMaskBoundsAfterErase(segment.MaskData, segment.MaskSize, currentBounds, segmentChangedBounds)
                    : segment.MaskBounds;
                MarkRenderDirty(segment, segmentChangedBounds);
                changedSegmentList.Add(segment);
                changedBounds = changedBounds.IsEmpty ? segmentChangedBounds : Rectangle.Union(changedBounds, segmentChangedBounds);
                changed = true;
                if (segment.MaskBounds.IsEmpty)
                {
                    segments.Remove(segment);
                }
            }

            changedSegments = changedSegmentList;
            return changed;
        }

        public bool TryMoveRasterMask(
            LabelingSegmentationObject segment,
            int deltaX,
            int deltaY,
            Size imageSize,
            out Rectangle changedBounds)
        {
            changedBounds = Rectangle.Empty;
            if (segment?.IsRasterMask != true || imageSize.Width <= 0 || imageSize.Height <= 0)
            {
                return false;
            }

            Rectangle currentBounds = segment.Bounds;
            if (currentBounds.IsEmpty)
            {
                return false;
            }

            int safeDeltaX = Math.Clamp(deltaX, -currentBounds.Left, imageSize.Width - currentBounds.Right);
            int safeDeltaY = Math.Clamp(deltaY, -currentBounds.Top, imageSize.Height - currentBounds.Bottom);
            if (safeDeltaX == 0 && safeDeltaY == 0)
            {
                return false;
            }

            int regionWidth = currentBounds.Width;
            int regionHeight = currentBounds.Height;
            int regionByteCount = regionWidth * regionHeight;
            byte[] sourceRegion = ArrayPool<byte>.Shared.Rent(regionByteCount);
            try
            {
                for (int y = 0; y < regionHeight; y++)
                {
                    int sourceOffset = ((currentBounds.Top + y) * segment.MaskSize.Width) + currentBounds.Left;
                    Buffer.BlockCopy(segment.MaskData, sourceOffset, sourceRegion, y * regionWidth, regionWidth);
                    Array.Clear(segment.MaskData, sourceOffset, regionWidth);
                }

                // Mask move happens during MouseMove. Keep the full image-sized mask buffer
                // stable and rent the active-bounds scratch region so large masks do not
                // allocate a new multi-MB byte[] for every pointer event.
                int targetLeft = currentBounds.Left + safeDeltaX;
                int targetTop = currentBounds.Top + safeDeltaY;
                for (int y = 0; y < regionHeight; y++)
                {
                    int sourceOffset = y * regionWidth;
                    int targetOffset = ((targetTop + y) * segment.MaskSize.Width) + targetLeft;
                    // Copy whole rows. Per-pixel non-zero checks made large mask drags
                    // CPU-bound on every MouseMove even after the scratch buffer was pooled.
                    Buffer.BlockCopy(sourceRegion, sourceOffset, segment.MaskData, targetOffset, regionWidth);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(sourceRegion);
            }

            segment.MaskBounds = new Rectangle(
                currentBounds.Left + safeDeltaX,
                currentBounds.Top + safeDeltaY,
                currentBounds.Width,
                currentBounds.Height);
            changedBounds = Rectangle.Union(currentBounds, segment.MaskBounds);
            MarkRenderDirty(segment, changedBounds);
            return true;
        }

        private static LabelingSegmentationObject GetOrCreateRasterSegment(
            IList<LabelingSegmentationObject> segments,
            Size imageSize,
            CClassItem classItem)
        {
            string className = classItem?.Text ?? "Defect";
            LabelingSegmentationObject existing = segments.FirstOrDefault(segment =>
                segment?.IsRasterMask == true
                && string.Equals(segment.ClassName, className, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.ClassName = className;
                existing.ClassItem = classItem;
                return existing;
            }

            var segment = new LabelingSegmentationObject(Array.Empty<Point>(), classItem)
            {
                ClassName = className,
                ClassItem = classItem,
                MaskData = new byte[Math.Max(0, imageSize.Width * imageSize.Height)],
                MaskSize = imageSize,
                MaskBounds = Rectangle.Empty,
                RenderVersion = 1,
                RenderDirtyBounds = new Rectangle(Point.Empty, imageSize)
            };
            segments.Add(segment);
            return segment;
        }

        private static bool ApplyBrushToMask(
            byte[] mask,
            Size maskSize,
            IEnumerable<Point> centers,
            int radius,
            bool paint,
            out Rectangle changedBounds,
            Rectangle clipBounds = default)
        {
            changedBounds = Rectangle.Empty;
            if (mask == null || maskSize.Width <= 0 || maskSize.Height <= 0 || mask.Length != maskSize.Width * maskSize.Height)
            {
                return false;
            }

            Rectangle safeClipBounds = clipBounds.IsEmpty
                ? new Rectangle(Point.Empty, maskSize)
                : Rectangle.Intersect(clipBounds, new Rectangle(Point.Empty, maskSize));
            if (safeClipBounds.IsEmpty)
            {
                return false;
            }

            BrushStamp stamp = BrushStampCache.GetOrAdd(Math.Max(1, radius), CreateBrushStamp);
            byte target = paint ? (byte)1 : (byte)0;
            bool changed = false;
            int leftChanged = int.MaxValue;
            int topChanged = int.MaxValue;
            int rightChanged = int.MinValue;
            int bottomChanged = int.MinValue;

            // MouseUp commit replays the same geometry the FBO preview showed during drag.
            // Merge repeated brush circles into row spans first, otherwise dense strokes
            // revisit the same pixels hundreds of times and produce a visible release hitch.
            Dictionary<int, List<BrushStrokeSpan>> spansByRow = BuildBrushStrokeSpans(stamp, centers, safeClipBounds);
            foreach (KeyValuePair<int, List<BrushStrokeSpan>> rowSpans in spansByRow)
            {
                int y = rowSpans.Key;
                foreach (BrushStrokeSpan span in rowSpans.Value)
                {
                    changed |= ApplyBrushStrokeSpan(mask, maskSize.Width, y, span.Left, span.Right, target, ref leftChanged, ref topChanged, ref rightChanged, ref bottomChanged);
                }
            }

            changedBounds = changed
                ? Rectangle.FromLTRB(leftChanged, topChanged, rightChanged + 1, bottomChanged + 1)
                : Rectangle.Empty;
            return changed;
        }

        private static Dictionary<int, List<BrushStrokeSpan>> BuildBrushStrokeSpans(
            BrushStamp stamp,
            IEnumerable<Point> centers,
            Rectangle safeClipBounds)
        {
            var spansByRow = new Dictionary<int, List<BrushStrokeSpan>>();
            foreach (Point center in centers ?? Enumerable.Empty<Point>())
            {
                BrushStampRow[] rows = stamp.Rows;
                for (int rowIndex = 0; rowIndex < rows.Length; rowIndex++)
                {
                    BrushStampRow row = rows[rowIndex];
                    int y = center.Y + row.DeltaY;
                    if (y < safeClipBounds.Top || y >= safeClipBounds.Bottom)
                    {
                        continue;
                    }

                    int left = Math.Max(safeClipBounds.Left, center.X + row.LeftDeltaX);
                    int right = Math.Min(safeClipBounds.Right - 1, center.X + row.RightDeltaX);
                    if (left > right)
                    {
                        continue;
                    }

                    if (!spansByRow.TryGetValue(y, out List<BrushStrokeSpan> spans))
                    {
                        spans = new List<BrushStrokeSpan>();
                        spansByRow[y] = spans;
                    }

                    AddMergedBrushStrokeSpan(spans, left, right);
                }
            }

            return spansByRow;
        }

        private static void AddMergedBrushStrokeSpan(List<BrushStrokeSpan> spans, int left, int right)
        {
            if (spans == null || left > right)
            {
                return;
            }

            if (spans.Count == 0)
            {
                spans.Add(new BrushStrokeSpan(left, right));
                return;
            }

            BrushStrokeSpan last = spans[spans.Count - 1];
            if (left > last.Right + 1)
            {
                spans.Add(new BrushStrokeSpan(left, right));
                return;
            }

            if (right >= last.Left - 1)
            {
                spans[spans.Count - 1] = new BrushStrokeSpan(
                    Math.Min(last.Left, left),
                    Math.Max(last.Right, right));
                return;
            }

            int insertIndex = 0;
            while (insertIndex < spans.Count && spans[insertIndex].Right + 1 < left)
            {
                insertIndex++;
            }

            int mergedLeft = left;
            int mergedRight = right;
            int removeEnd = insertIndex;
            while (removeEnd < spans.Count && spans[removeEnd].Left <= mergedRight + 1)
            {
                BrushStrokeSpan current = spans[removeEnd];
                mergedLeft = Math.Min(mergedLeft, current.Left);
                mergedRight = Math.Max(mergedRight, current.Right);
                removeEnd++;
            }

            if (removeEnd > insertIndex)
            {
                spans.RemoveRange(insertIndex, removeEnd - insertIndex);
            }

            spans.Insert(insertIndex, new BrushStrokeSpan(mergedLeft, mergedRight));
        }

        private static bool ApplyBrushStrokeSpan(
            byte[] mask,
            int maskWidth,
            int y,
            int left,
            int right,
            byte target,
            ref int leftChanged,
            ref int topChanged,
            ref int rightChanged,
            ref int bottomChanged)
        {
            int rowOffset = y * maskWidth;
            int startIndex = rowOffset + left;
            int length = right - left + 1;
            bool changed = false;
            for (int x = left; x <= right; x++)
            {
                if (mask[rowOffset + x] == target)
                {
                    continue;
                }

                changed = true;
                break;
            }

            if (!changed)
            {
                return false;
            }

            // Spans were already merged to the exact brush footprint for this row.
            // Fill the row range in one operation; per-pixel min/max/write work was
            // the visible MouseUp cost on long brush and eraser strokes.
            Array.Fill(mask, target, startIndex, length);
            leftChanged = Math.Min(leftChanged, left);
            topChanged = Math.Min(topChanged, y);
            rightChanged = Math.Max(rightChanged, right);
            bottomChanged = Math.Max(bottomChanged, y);
            return true;
        }

        private static BrushStamp CreateBrushStamp(int radius)
        {
            int safeRadius = Math.Max(1, radius);
            int maxOffset = safeRadius + 1;
            double radiusSquared = safeRadius * safeRadius;
            var rows = new List<BrushStampRow>((maxOffset * 2) + 1);
            for (int dy = -maxOffset; dy <= maxOffset; dy++)
            {
                double yDistance = GetPixelCellDistanceFromBrushCenter(dy);
                double remaining = radiusSquared - (yDistance * yDistance);
                if (remaining < 0D)
                {
                    continue;
                }

                int maxDx = Math.Min(maxOffset, (int)Math.Floor(Math.Sqrt(remaining) + 0.5D));
                rows.Add(new BrushStampRow(dy, -maxDx, maxDx));
            }

            return new BrushStamp(rows.ToArray());
        }

        private static double GetPixelCellDistanceFromBrushCenter(int offset)
            => Math.Max(0D, Math.Abs(offset) - 0.5D);


        private sealed class BrushStamp
        {
            public BrushStamp(BrushStampRow[] rows)
            {
                Rows = rows ?? Array.Empty<BrushStampRow>();
            }

            public BrushStampRow[] Rows { get; }
        }

        private readonly struct BrushStampRow
        {
            public BrushStampRow(int deltaY, int leftDeltaX, int rightDeltaX)
            {
                DeltaY = deltaY;
                LeftDeltaX = leftDeltaX;
                RightDeltaX = rightDeltaX;
            }

            public int DeltaY { get; }

            public int LeftDeltaX { get; }

            public int RightDeltaX { get; }
        }

        private readonly struct BrushStrokeSpan
        {
            public BrushStrokeSpan(int left, int right)
            {
                Left = left;
                Right = right;
            }

            public int Left { get; }

            public int Right { get; }
        }
        private static Rectangle GetStrokeBounds(IReadOnlyList<Point> centers, int radius, Size imageSize)
        {
            if (centers == null || centers.Count == 0 || imageSize.Width <= 0 || imageSize.Height <= 0)
            {
                return Rectangle.Empty;
            }

            int safeRadius = Math.Max(1, radius) + 1;
            int left = centers.Min(point => point.X) - safeRadius;
            int top = centers.Min(point => point.Y) - safeRadius;
            int right = centers.Max(point => point.X) + safeRadius + 1;
            int bottom = centers.Max(point => point.Y) + safeRadius + 1;
            return Rectangle.Intersect(
                Rectangle.FromLTRB(left, top, right, bottom),
                new Rectangle(Point.Empty, imageSize));
        }

        private static Rectangle GetMaskBoundsWithin(byte[] maskData, Size maskSize, Rectangle searchBounds)
        {
            if (maskData == null || maskSize.Width <= 0 || maskSize.Height <= 0 || maskData.Length != maskSize.Width * maskSize.Height)
            {
                return Rectangle.Empty;
            }

            Rectangle safeBounds = Rectangle.Intersect(searchBounds, new Rectangle(Point.Empty, maskSize));
            if (safeBounds.IsEmpty)
            {
                return Rectangle.Empty;
            }

            int left = int.MaxValue;
            int top = int.MaxValue;
            int right = int.MinValue;
            int bottom = int.MinValue;
            for (int y = safeBounds.Top; y < safeBounds.Bottom; y++)
            {
                int rowOffset = y * maskSize.Width;
                for (int x = safeBounds.Left; x < safeBounds.Right; x++)
                {
                    if (maskData[rowOffset + x] == 0)
                    {
                        continue;
                    }

                    left = Math.Min(left, x);
                    top = Math.Min(top, y);
                    right = Math.Max(right, x);
                    bottom = Math.Max(bottom, y);
                }
            }

            return left == int.MaxValue
                ? Rectangle.Empty
                : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
        }

        private static Rectangle GetMaskBoundsAfterErase(
            byte[] maskData,
            Size maskSize,
            Rectangle currentBounds,
            Rectangle changedBounds)
        {
            if (maskData == null || maskSize.Width <= 0 || maskSize.Height <= 0 || maskData.Length != maskSize.Width * maskSize.Height)
            {
                return Rectangle.Empty;
            }

            Rectangle imageBounds = new Rectangle(Point.Empty, maskSize);
            Rectangle safeBounds = Rectangle.Intersect(currentBounds, imageBounds);
            if (safeBounds.IsEmpty)
            {
                return Rectangle.Empty;
            }

            Rectangle safeChangedBounds = Rectangle.Intersect(changedBounds, safeBounds);
            if (safeChangedBounds.IsEmpty)
            {
                return safeBounds;
            }

            int left = safeBounds.Left;
            int top = safeBounds.Top;
            int right = safeBounds.Right - 1;
            int bottom = safeBounds.Bottom - 1;

            if (safeChangedBounds.Left <= safeBounds.Left)
            {
                left = FindFirstNonEmptyColumn(maskData, maskSize.Width, safeBounds.Left, safeBounds.Right, safeBounds.Top, safeBounds.Bottom);
                if (left < 0)
                {
                    return Rectangle.Empty;
                }
            }

            if (safeChangedBounds.Right >= safeBounds.Right)
            {
                right = FindLastNonEmptyColumn(maskData, maskSize.Width, left, safeBounds.Right, safeBounds.Top, safeBounds.Bottom);
                if (right < left)
                {
                    return Rectangle.Empty;
                }
            }

            if (safeChangedBounds.Top <= safeBounds.Top)
            {
                top = FindFirstNonEmptyRow(maskData, maskSize.Width, left, right + 1, safeBounds.Top, safeBounds.Bottom);
                if (top < 0)
                {
                    return Rectangle.Empty;
                }
            }

            if (safeChangedBounds.Bottom >= safeBounds.Bottom)
            {
                bottom = FindLastNonEmptyRow(maskData, maskSize.Width, left, right + 1, top, safeBounds.Bottom);
                if (bottom < top)
                {
                    return Rectangle.Empty;
                }
            }

            return Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
        }

        private static int FindFirstNonEmptyColumn(byte[] maskData, int maskWidth, int left, int right, int top, int bottom)
        {
            for (int x = left; x < right; x++)
            {
                for (int y = top; y < bottom; y++)
                {
                    if (maskData[(y * maskWidth) + x] != 0)
                    {
                        return x;
                    }
                }
            }

            return -1;
        }

        private static int FindLastNonEmptyColumn(byte[] maskData, int maskWidth, int left, int right, int top, int bottom)
        {
            for (int x = right - 1; x >= left; x--)
            {
                for (int y = top; y < bottom; y++)
                {
                    if (maskData[(y * maskWidth) + x] != 0)
                    {
                        return x;
                    }
                }
            }

            return left - 1;
        }

        private static int FindFirstNonEmptyRow(byte[] maskData, int maskWidth, int left, int right, int top, int bottom)
        {
            for (int y = top; y < bottom; y++)
            {
                int rowOffset = y * maskWidth;
                for (int x = left; x < right; x++)
                {
                    if (maskData[rowOffset + x] != 0)
                    {
                        return y;
                    }
                }
            }

            return -1;
        }

        private static int FindLastNonEmptyRow(byte[] maskData, int maskWidth, int left, int right, int top, int bottom)
        {
            for (int y = bottom - 1; y >= top; y--)
            {
                int rowOffset = y * maskWidth;
                for (int x = left; x < right; x++)
                {
                    if (maskData[rowOffset + x] != 0)
                    {
                        return y;
                    }
                }
            }

            return top - 1;
        }

        private static bool ShouldRecalculateMaskBoundsAfterErase(Rectangle currentBounds, Rectangle changedBounds)
        {
            if (currentBounds.IsEmpty || changedBounds.IsEmpty)
            {
                return true;
            }

            return changedBounds.Left <= currentBounds.Left
                || changedBounds.Top <= currentBounds.Top
                || changedBounds.Right >= currentBounds.Right
                || changedBounds.Bottom >= currentBounds.Bottom;
        }

        private static void MarkRenderDirty(LabelingSegmentationObject segment, Rectangle changedBounds)
        {
            if (segment == null || changedBounds.IsEmpty)
            {
                return;
            }

            segment.RenderDirtyBounds = segment.RenderDirtyBounds.IsEmpty
                ? changedBounds
                : Rectangle.Union(segment.RenderDirtyBounds, changedBounds);
            segment.RenderVersion++;
        }

        private static CClassItem ResolveClass(CClassItem classItem)
        {
            if (classItem != null && !string.IsNullOrWhiteSpace(classItem.Text))
            {
                return classItem;
            }

            return new CClassItem
            {
                Text = "Defect",
                DrawColor = Color.DeepSkyBlue
            };
        }

        private static List<Point> NormalizeCenters(IEnumerable<Point> centers, Size imageSize)
        {
            if (imageSize.Width <= 0 || imageSize.Height <= 0)
            {
                return new List<Point>();
            }

            return (centers ?? Enumerable.Empty<Point>())
                .Select(point => new Point(
                    Math.Clamp(point.X, 0, imageSize.Width - 1),
                    Math.Clamp(point.Y, 0, imageSize.Height - 1)))
                .Distinct()
                .ToList();
        }
    }
}
