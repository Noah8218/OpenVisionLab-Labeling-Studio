using System;
using System.Collections.Generic;
using System.Drawing;

namespace MvcVisionSystem
{
    /// <summary>
    /// Applies one explicit QA marker repair to a copy of a binary mask. The
    /// caller owns preview, confirmation, history, and canonical replacement.
    /// </summary>
    public sealed class MaskQualityRepairService
    {
        private static readonly Point[] Neighbors =
        {
            new Point(-1, 0),
            new Point(1, 0),
            new Point(0, -1),
            new Point(0, 1)
        };

        public bool TryApply(
            byte[] maskData,
            Size maskSize,
            MaskQualityMarker marker,
            out byte[] repairedMask,
            out Rectangle changedBounds)
        {
            repairedMask = Array.Empty<byte>();
            changedBounds = Rectangle.Empty;
            if (maskData == null
                || marker == null
                || maskSize.Width <= 0
                || maskSize.Height <= 0
                || maskData.Length != checked(maskSize.Width * maskSize.Height)
                || marker.Bounds.IsEmpty)
            {
                return false;
            }

            Rectangle bounds = Rectangle.Intersect(
                new Rectangle(Point.Empty, maskSize),
                marker.Bounds);
            if (bounds.IsEmpty)
            {
                return false;
            }

            bool foreground = marker.Kind == MaskQualityMarkerKind.SmallComponent;
            Point start = FindMatchingPoint(maskData, maskSize, bounds, foreground);
            if (start.X < 0)
            {
                return false;
            }

            repairedMask = (byte[])maskData.Clone();
            var queue = new Queue<Point>();
            var visited = new bool[maskData.Length];
            queue.Enqueue(start);
            visited[(start.Y * maskSize.Width) + start.X] = true;
            int minX = maskSize.Width;
            int minY = maskSize.Height;
            int maxX = -1;
            int maxY = -1;
            while (queue.Count > 0)
            {
                Point point = queue.Dequeue();
                int index = (point.Y * maskSize.Width) + point.X;
                bool matches = (maskData[index] != 0) == foreground;
                if (!matches)
                {
                    continue;
                }

                repairedMask[index] = foreground ? (byte)0 : (byte)255;
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
                foreach (Point offset in Neighbors)
                {
                    int nextX = point.X + offset.X;
                    int nextY = point.Y + offset.Y;
                    if (nextX < bounds.Left || nextY < bounds.Top
                        || nextX >= bounds.Right || nextY >= bounds.Bottom)
                    {
                        continue;
                    }

                    int nextIndex = (nextY * maskSize.Width) + nextX;
                    if (visited[nextIndex])
                    {
                        continue;
                    }

                    visited[nextIndex] = true;
                    queue.Enqueue(new Point(nextX, nextY));
                }
            }

            if (maxX < minX || maxY < minY)
            {
                repairedMask = Array.Empty<byte>();
                return false;
            }

            changedBounds = Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
            return true;
        }

        private static Point FindMatchingPoint(byte[] maskData, Size size, Rectangle bounds, bool foreground)
        {
            for (int y = bounds.Top; y < bounds.Bottom; y++)
            {
                for (int x = bounds.Left; x < bounds.Right; x++)
                {
                    if ((maskData[(y * size.Width) + x] != 0) == foreground)
                    {
                        return new Point(x, y);
                    }
                }
            }

            return new Point(-1, -1);
        }
    }
}
