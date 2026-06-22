using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace MvcVisionSystem
{
    public sealed class WpfPolygonAnnotationService
    {
        public const int DefaultCloseDistancePixels = 8;

        private readonly List<Point> points = new List<Point>();

        public IReadOnlyList<Point> Points => points;

        public bool IsClosed { get; private set; }

        public void Reset()
        {
            points.Clear();
            IsClosed = false;
        }

        public bool TryAddPoint(Point imagePoint, Size imageSize, out bool closed)
        {
            closed = false;
            if (IsClosed || imageSize.Width <= 0 || imageSize.Height <= 0)
            {
                return false;
            }

            Point clipped = ClampToImage(imagePoint, imageSize);
            if (points.Count >= 3 && IsNearStart(clipped, DefaultCloseDistancePixels))
            {
                IsClosed = true;
                closed = true;
                return true;
            }

            if (points.Count > 0 && points[points.Count - 1] == clipped)
            {
                return false;
            }

            points.Add(clipped);
            return true;
        }

        public bool TryComplete(CClassItem classItem, Size imageSize, out LabelingSegmentationObject annotation, out string message)
        {
            annotation = null;
            message = string.Empty;

            if (!TryCreateObject(points, classItem, imageSize, out annotation))
            {
                message = "Polygon needs at least three valid image-pixel points.";
                return false;
            }

            IsClosed = true;
            return true;
        }

        public static int FindNearestPointIndex(LabelingSegmentationObject segment, Point imagePoint, int maxDistancePixels)
        {
            if (segment?.Points == null || segment.Points.Count == 0)
            {
                return -1;
            }

            int maxDistance = Math.Max(1, maxDistancePixels);
            int maxDistanceSquared = maxDistance * maxDistance;
            int selectedIndex = -1;
            int selectedDistance = int.MaxValue;
            for (int i = 0; i < segment.Points.Count; i++)
            {
                int dx = segment.Points[i].X - imagePoint.X;
                int dy = segment.Points[i].Y - imagePoint.Y;
                int distanceSquared = (dx * dx) + (dy * dy);
                if (distanceSquared <= maxDistanceSquared && distanceSquared < selectedDistance)
                {
                    selectedIndex = i;
                    selectedDistance = distanceSquared;
                }
            }

            return selectedIndex;
        }

        public static bool TryMovePoint(
            LabelingSegmentationObject segment,
            int pointIndex,
            Point imagePoint,
            Size imageSize,
            out Rectangle changedBounds)
        {
            changedBounds = Rectangle.Empty;
            if (segment?.Points == null
                || pointIndex < 0
                || pointIndex >= segment.Points.Count
                || imageSize.Width <= 0
                || imageSize.Height <= 0)
            {
                return false;
            }

            Point oldPoint = segment.Points[pointIndex];
            Point newPoint = ClampToImage(imagePoint, imageSize);
            if (oldPoint == newPoint)
            {
                return false;
            }

            Rectangle oldBounds = SegmentationGeometry.GetBounds(segment.Points);
            segment.Points[pointIndex] = newPoint;
            Rectangle newBounds = SegmentationGeometry.GetBounds(segment.Points);
            changedBounds = oldBounds.IsEmpty ? newBounds : Rectangle.Union(oldBounds, newBounds);
            return true;
        }

        public static bool TryMovePolygon(
            LabelingSegmentationObject segment,
            int deltaX,
            int deltaY,
            Size imageSize,
            out Rectangle changedBounds)
        {
            changedBounds = Rectangle.Empty;
            if (segment?.Points == null
                || segment.Points.Count == 0
                || imageSize.Width <= 0
                || imageSize.Height <= 0)
            {
                return false;
            }

            Rectangle oldBounds = SegmentationGeometry.GetBounds(segment.Points);
            if (oldBounds.IsEmpty)
            {
                return false;
            }

            int safeDeltaX = Math.Clamp(deltaX, -oldBounds.Left, imageSize.Width - oldBounds.Right);
            int safeDeltaY = Math.Clamp(deltaY, -oldBounds.Top, imageSize.Height - oldBounds.Bottom);
            if (safeDeltaX == 0 && safeDeltaY == 0)
            {
                return false;
            }

            for (int i = 0; i < segment.Points.Count; i++)
            {
                Point point = segment.Points[i];
                segment.Points[i] = new Point(point.X + safeDeltaX, point.Y + safeDeltaY);
            }

            Rectangle newBounds = SegmentationGeometry.GetBounds(segment.Points);
            changedBounds = Rectangle.Union(oldBounds, newBounds);
            return true;
        }

        public static bool IsPointInsidePolygon(LabelingSegmentationObject segment, Point imagePoint)
        {
            return segment?.Points != null
                && segment.Points.Count >= 3
                && SegmentationGeometry.ContainsPoint(segment.Points, imagePoint);
        }

        public static bool TryCreateObject(
            IEnumerable<Point> rawPoints,
            CClassItem classItem,
            Size imageSize,
            out LabelingSegmentationObject annotation)
        {
            annotation = null;
            List<Point> normalized = SegmentationGeometry.NormalizePolygon(
                rawPoints,
                imageSize,
                minimumDistance: 1,
                simplificationTolerance: 0D);

            if (normalized.Count < 3)
            {
                return false;
            }

            annotation = new LabelingSegmentationObject(normalized, classItem)
            {
                ClassName = classItem?.Text ?? string.Empty
            };
            return true;
        }

        private bool IsNearStart(Point point, int closeDistancePixels)
        {
            if (points.Count == 0)
            {
                return false;
            }

            Point start = points[0];
            int dx = point.X - start.X;
            int dy = point.Y - start.Y;
            return dx * dx + dy * dy <= closeDistancePixels * closeDistancePixels;
        }

        private static Point ClampToImage(Point point, Size imageSize)
            => new Point(
                Math.Clamp(point.X, 0, imageSize.Width - 1),
                Math.Clamp(point.Y, 0, imageSize.Height - 1));
    }
}
