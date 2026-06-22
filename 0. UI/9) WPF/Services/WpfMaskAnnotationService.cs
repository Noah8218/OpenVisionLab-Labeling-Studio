using MvcVisionSystem.Yolo;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace MvcVisionSystem
{
    public sealed class WpfMaskAnnotationService
    {
        public const int DefaultBrushRadius = 6;

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
        {
            changedBounds = Rectangle.Empty;
            if (segments == null || segments.Count == 0 || imageSize.Width <= 0 || imageSize.Height <= 0)
            {
                return false;
            }

            List<Point> strokeCenters = NormalizeCenters(centers, imageSize);
            if (strokeCenters.Count == 0)
            {
                return false;
            }

            bool changed = false;
            foreach (LabelingSegmentationObject segment in segments.Where(item => item?.IsRasterMask == true).ToList())
            {
                if (!ApplyBrushToMask(segment.MaskData, segment.MaskSize, strokeCenters, radius, paint: false, out Rectangle segmentChangedBounds))
                {
                    continue;
                }

                segment.MaskBounds = ShouldRecalculateMaskBoundsAfterErase(segment.MaskBounds, segmentChangedBounds)
                    ? SegmentationGeometry.GetMaskBounds(segment.MaskData, segment.MaskSize)
                    : segment.MaskBounds;
                MarkRenderDirty(segment, segmentChangedBounds);
                changedBounds = changedBounds.IsEmpty ? segmentChangedBounds : Rectangle.Union(changedBounds, segmentChangedBounds);
                changed = true;
                if (segment.MaskBounds.IsEmpty)
                {
                    segments.Remove(segment);
                }
            }

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
            out Rectangle changedBounds)
        {
            changedBounds = Rectangle.Empty;
            if (mask == null || maskSize.Width <= 0 || maskSize.Height <= 0 || mask.Length != maskSize.Width * maskSize.Height)
            {
                return false;
            }

            int safeRadius = Math.Max(1, radius);
            double radiusSquared = safeRadius * safeRadius;
            byte target = paint ? (byte)1 : (byte)0;
            bool changed = false;
            int leftChanged = int.MaxValue;
            int topChanged = int.MaxValue;
            int rightChanged = int.MinValue;
            int bottomChanged = int.MinValue;

            foreach (Point center in centers ?? Enumerable.Empty<Point>())
            {
                int left = Math.Max(0, center.X - safeRadius - 1);
                int top = Math.Max(0, center.Y - safeRadius - 1);
                int right = Math.Min(maskSize.Width - 1, center.X + safeRadius + 1);
                int bottom = Math.Min(maskSize.Height - 1, center.Y + safeRadius + 1);
                for (int y = top; y <= bottom; y++)
                {
                    int rowOffset = y * maskSize.Width;
                    for (int x = left; x <= right; x++)
                    {
                        if (!DoesPixelCellIntersectCircle(x, y, center, radiusSquared))
                        {
                            continue;
                        }

                        int index = rowOffset + x;
                        if (mask[index] == target)
                        {
                            continue;
                        }

                        mask[index] = target;
                        changed = true;
                        leftChanged = Math.Min(leftChanged, x);
                        topChanged = Math.Min(topChanged, y);
                        rightChanged = Math.Max(rightChanged, x);
                        bottomChanged = Math.Max(bottomChanged, y);
                    }
                }
            }

            changedBounds = changed
                ? Rectangle.FromLTRB(leftChanged, topChanged, rightChanged + 1, bottomChanged + 1)
                : Rectangle.Empty;
            return changed;
        }

        private static bool DoesPixelCellIntersectCircle(int x, int y, Point center, double radiusSquared)
        {
            double nearestX = Math.Clamp(center.X, x - 0.5D, x + 0.5D);
            double nearestY = Math.Clamp(center.Y, y - 0.5D, y + 0.5D);
            double dx = nearestX - center.X;
            double dy = nearestY - center.Y;
            return (dx * dx) + (dy * dy) <= radiusSquared;
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
