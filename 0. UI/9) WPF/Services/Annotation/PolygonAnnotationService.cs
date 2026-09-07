using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace MvcVisionSystem
{
    public enum WpfPolygonVertexEditMode
    {
        Insert,
        Delete
    }

    public class PolygonAnnotationService
    {
        public const int DefaultCloseDistancePixels = 8;
        public const string InsertVertexStructuralOperationName = "VertexInsert";
        public const string DeleteVertexStructuralOperationName = "VertexDelete";
        public const string IntelligentScissorsStructuralOperationName = "IntelligentScissors";

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

        public bool TryComplete(LabelClass classItem, Size imageSize, out LabelingSegmentationObject annotation, out string message)
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

        public static int ResolveImageHitTolerance(float zoomScale, int screenPixels = 8)
            => Math.Max(
                1,
                (int)Math.Ceiling(Math.Max(0.0001F, zoomScale) * Math.Max(1, screenPixels)));

        public static int FindNearestEdgeIndex(
            LabelingSegmentationObject segment,
            Point imagePoint,
            int maxDistancePixels,
            out Point nearestPoint)
        {
            nearestPoint = Point.Empty;
            if (segment?.IsRasterMask != false || segment.Points == null || segment.Points.Count < 3)
            {
                return -1;
            }

            double maximumDistanceSquared = Math.Pow(Math.Max(1, maxDistancePixels), 2D);
            int selectedIndex = -1;
            double selectedDistanceSquared = double.MaxValue;
            Point selectedPoint = Point.Empty;
            for (int i = 0; i < segment.Points.Count; i++)
            {
                Point first = segment.Points[i];
                Point second = segment.Points[(i + 1) % segment.Points.Count];
                Point projected = ProjectPointToSegment(imagePoint, first, second, out double distanceSquared);
                if (distanceSquared <= maximumDistanceSquared && distanceSquared < selectedDistanceSquared)
                {
                    selectedIndex = i;
                    selectedDistanceSquared = distanceSquared;
                    selectedPoint = projected;
                }
            }

            nearestPoint = selectedPoint;
            return selectedIndex;
        }

        public static bool TryInsertPoint(
            LabelingSegmentationObject segment,
            Point imagePoint,
            Size imageSize,
            int maxDistancePixels,
            out int insertedPointIndex,
            out Rectangle changedBounds,
            out string error)
        {
            insertedPointIndex = -1;
            changedBounds = Rectangle.Empty;
            error = string.Empty;
            if (!CanEditVertices(segment, imageSize))
            {
                error = "\uC218\uB3D9 \uD3F4\uB9AC\uACE4\uB9CC \uC815\uC810\uC744 \uCD94\uAC00\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.";
                return false;
            }

            int edgeIndex = FindNearestEdgeIndex(segment, imagePoint, maxDistancePixels, out Point projected);
            if (edgeIndex < 0)
            {
                error = "\uC120\uD0DD\uD55C \uD3F4\uB9AC\uACE4 \uBAA8\uC11C\uB9AC \uADFC\uCC98\uB97C \uD074\uB9AD\uD558\uC138\uC694.";
                return false;
            }

            Point first = segment.Points[edgeIndex];
            Point second = segment.Points[(edgeIndex + 1) % segment.Points.Count];
            if (projected == first || projected == second)
            {
                error = "\uAE30\uC874 \uC815\uC810\uACFC \uB108\uBB34 \uAC00\uAE5D\uC2B5\uB2C8\uB2E4. \uBAA8\uC11C\uB9AC \uC911\uAC04\uC744 \uD074\uB9AD\uD558\uC138\uC694.";
                return false;
            }

            var candidate = new List<Point>(segment.Points);
            insertedPointIndex = edgeIndex + 1;
            candidate.Insert(insertedPointIndex, projected);
            if (!IsValidSimplePolygon(candidate, imageSize))
            {
                insertedPointIndex = -1;
                error = "\uC815\uC810\uC744 \uCD94\uAC00\uD558\uBA74 \uC720\uD6A8\uD558\uC9C0 \uC54A\uC740 \uD3F4\uB9AC\uACE4\uC774 \uB429\uB2C8\uB2E4.";
                return false;
            }

            ApplyPointMutation(segment, candidate, InsertVertexStructuralOperationName, out changedBounds);
            return true;
        }

        public static bool TryDeletePoint(
            LabelingSegmentationObject segment,
            Point imagePoint,
            Size imageSize,
            int maxDistancePixels,
            out int deletedPointIndex,
            out Rectangle changedBounds,
            out string error)
        {
            deletedPointIndex = FindNearestPointIndex(segment, imagePoint, maxDistancePixels);
            if (deletedPointIndex < 0)
            {
                changedBounds = Rectangle.Empty;
                error = "\uC0AD\uC81C\uD560 \uC815\uC810 \uADFC\uCC98\uB97C \uD074\uB9AD\uD558\uC138\uC694.";
                return false;
            }

            return TryDeletePoint(
                segment,
                deletedPointIndex,
                imageSize,
                out changedBounds,
                out error);
        }

        public static bool TryDeletePoint(
            LabelingSegmentationObject segment,
            int pointIndex,
            Size imageSize,
            out Rectangle changedBounds,
            out string error)
        {
            changedBounds = Rectangle.Empty;
            error = string.Empty;
            if (!CanEditVertices(segment, imageSize)
                || pointIndex < 0
                || pointIndex >= segment.Points.Count)
            {
                error = "\uC218\uB3D9 \uD3F4\uB9AC\uACE4\uC758 \uC720\uD6A8\uD55C \uC815\uC810\uC744 \uC120\uD0DD\uD558\uC138\uC694.";
                return false;
            }

            if (segment.Points.Count <= 3)
            {
                error = "\uD3F4\uB9AC\uACE4\uC740 \uCD5C\uC18C 3\uAC1C\uC758 \uC815\uC810\uC774 \uD544\uC694\uD569\uB2C8\uB2E4.";
                return false;
            }

            var candidate = new List<Point>(segment.Points);
            candidate.RemoveAt(pointIndex);
            if (!IsValidSimplePolygon(candidate, imageSize))
            {
                error = "\uC815\uC810\uC744 \uC0AD\uC81C\uD558\uBA74 \uAD50\uCC28\uD558\uAC70\uB098 \uBA74\uC801\uC774 \uC5C6\uB294 \uD3F4\uB9AC\uACE4\uC774 \uB429\uB2C8\uB2E4.";
                return false;
            }

            ApplyPointMutation(segment, candidate, DeleteVertexStructuralOperationName, out changedBounds);
            return true;
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

        public static bool TryApplyReplacementPoints(
            LabelingSegmentationObject segment,
            IReadOnlyList<Point> replacementPoints,
            Size imageSize,
            string operation,
            out Rectangle changedBounds,
            out string error)
        {
            changedBounds = Rectangle.Empty;
            error = string.Empty;
            if (!CanEditVertices(segment, imageSize)
                || replacementPoints == null
                || !IsValidSimplePolygon(replacementPoints, imageSize))
            {
                error = "\uACBD\uACC4 \uACBD\uB85C\uB97C \uC801\uC6A9\uD558\uBA74 \uAD50\uCC28\uD558\uAC70\uB098 \uBA74\uC801\uC774 \uC5C6\uB294 \uD3F4\uB9AC\uACE4\uC774 \uB429\uB2C8\uB2E4.";
                return false;
            }

            var candidate = replacementPoints.ToList();
            if (candidate.SequenceEqual(segment.Points))
            {
                error = "\uD604\uC7AC \uD3F4\uB9AC\uACE4\uACFC \uB2E4\uB978 \uACBD\uACC4 \uACBD\uB85C\uB97C \uCC3E\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.";
                return false;
            }

            ApplyPointMutation(
                segment,
                candidate,
                string.IsNullOrWhiteSpace(operation)
                    ? IntelligentScissorsStructuralOperationName
                    : operation.Trim(),
                out changedBounds);
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
            LabelClass classItem,
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

        private static bool CanEditVertices(LabelingSegmentationObject segment, Size imageSize)
            => segment?.IsRasterMask == false
                && segment.Points != null
                && segment.Points.Count >= 3
                && imageSize.Width > 0
                && imageSize.Height > 0;

        private static void ApplyPointMutation(
            LabelingSegmentationObject segment,
            List<Point> points,
            string operation,
            out Rectangle changedBounds)
        {
            Rectangle oldBounds = SegmentationGeometry.GetBounds(segment.Points);
            segment.Points = points;
            Rectangle newBounds = SegmentationGeometry.GetBounds(segment.Points);
            changedBounds = oldBounds.IsEmpty ? newBounds : Rectangle.Union(oldBounds, newBounds);
            segment.LastStructuralOperation = operation;
            segment.RenderVersion++;
            segment.RenderDirtyBounds = segment.RenderDirtyBounds.IsEmpty
                ? changedBounds
                : Rectangle.Union(segment.RenderDirtyBounds, changedBounds);
        }

        public static bool IsValidSimplePolygon(IReadOnlyList<Point> points, Size imageSize)
        {
            if (points == null
                || points.Count < 3
                || points.Distinct().Count() != points.Count
                || SegmentationGeometry.NormalizePolygon(
                    points,
                    imageSize,
                    minimumDistance: 1,
                    simplificationTolerance: 0D).Count != points.Count
                || CalculateTwiceSignedArea(points) == 0L)
            {
                return false;
            }

            for (int firstEdge = 0; firstEdge < points.Count; firstEdge++)
            {
                int firstNext = (firstEdge + 1) % points.Count;
                for (int secondEdge = firstEdge + 1; secondEdge < points.Count; secondEdge++)
                {
                    int secondNext = (secondEdge + 1) % points.Count;
                    bool adjacent = firstEdge == secondEdge
                        || firstNext == secondEdge
                        || secondNext == firstEdge;
                    if (adjacent)
                    {
                        continue;
                    }

                    if (SegmentsIntersect(
                        points[firstEdge],
                        points[firstNext],
                        points[secondEdge],
                        points[secondNext]))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static Point ProjectPointToSegment(
            Point point,
            Point first,
            Point second,
            out double distanceSquared)
        {
            double dx = second.X - first.X;
            double dy = second.Y - first.Y;
            double lengthSquared = (dx * dx) + (dy * dy);
            double parameter = lengthSquared <= double.Epsilon
                ? 0D
                : (((point.X - first.X) * dx) + ((point.Y - first.Y) * dy)) / lengthSquared;
            parameter = Math.Clamp(parameter, 0D, 1D);
            double projectedX = first.X + (parameter * dx);
            double projectedY = first.Y + (parameter * dy);
            double distanceX = point.X - projectedX;
            double distanceY = point.Y - projectedY;
            distanceSquared = (distanceX * distanceX) + (distanceY * distanceY);
            return new Point(
                (int)Math.Round(projectedX, MidpointRounding.AwayFromZero),
                (int)Math.Round(projectedY, MidpointRounding.AwayFromZero));
        }

        private static long CalculateTwiceSignedArea(IReadOnlyList<Point> points)
        {
            long area = 0L;
            for (int i = 0; i < points.Count; i++)
            {
                Point first = points[i];
                Point second = points[(i + 1) % points.Count];
                area += ((long)first.X * second.Y) - ((long)second.X * first.Y);
            }

            return area;
        }

        private static bool SegmentsIntersect(Point first, Point second, Point third, Point fourth)
        {
            long firstOrientation = Cross(first, second, third);
            long secondOrientation = Cross(first, second, fourth);
            long thirdOrientation = Cross(third, fourth, first);
            long fourthOrientation = Cross(third, fourth, second);
            if (((firstOrientation > 0L && secondOrientation < 0L)
                    || (firstOrientation < 0L && secondOrientation > 0L))
                && ((thirdOrientation > 0L && fourthOrientation < 0L)
                    || (thirdOrientation < 0L && fourthOrientation > 0L)))
            {
                return true;
            }

            return (firstOrientation == 0L && IsOnSegment(first, second, third))
                || (secondOrientation == 0L && IsOnSegment(first, second, fourth))
                || (thirdOrientation == 0L && IsOnSegment(third, fourth, first))
                || (fourthOrientation == 0L && IsOnSegment(third, fourth, second));
        }

        private static long Cross(Point first, Point second, Point third)
            => ((long)second.X - first.X) * ((long)third.Y - first.Y)
                - (((long)second.Y - first.Y) * ((long)third.X - first.X));

        private static bool IsOnSegment(Point first, Point second, Point point)
            => point.X >= Math.Min(first.X, second.X)
                && point.X <= Math.Max(first.X, second.X)
                && point.Y >= Math.Min(first.Y, second.Y)
                && point.Y <= Math.Max(first.Y, second.Y);
    }

    [Obsolete("Use PolygonAnnotationService.", false)]
    public sealed class WpfPolygonAnnotationService : PolygonAnnotationService
    {
    }
}
