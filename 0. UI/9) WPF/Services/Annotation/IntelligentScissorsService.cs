using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;

namespace MvcVisionSystem
{
    public sealed class WpfIntelligentScissorsOptions
    {
        public int SearchRadiusPixels { get; set; } = 24;

        public int MaximumSearchPixelCount { get; set; } = 180_000;

        public double SimplificationTolerancePixels { get; set; } = 0.75D;
    }

    public sealed class WpfIntelligentScissorsPlan
    {
        public LabelingSegmentationObject Source { get; init; }

        public int EdgeIndex { get; init; }

        public IReadOnlyList<Point> OriginalPoints { get; init; } = Array.Empty<Point>();

        public IReadOnlyList<Point> PathPoints { get; init; } = Array.Empty<Point>();

        public IReadOnlyList<Point> ReplacementPoints { get; init; } = Array.Empty<Point>();

        public Rectangle ChangedBounds { get; init; }

        public TimeSpan Elapsed { get; init; }
    }

    public class IntelligentScissorsService
    {
        private static readonly (int X, int Y, double Distance)[] NeighborOffsets =
        {
            (-1, 0, 1D),
            (0, -1, 1D),
            (1, 0, 1D),
            (0, 1, 1D),
            (-1, -1, 1.4142135623730951D),
            (1, -1, 1.4142135623730951D),
            (1, 1, 1.4142135623730951D),
            (-1, 1, 1.4142135623730951D)
        };

        private readonly WpfIntelligentScissorsOptions options;

        public IntelligentScissorsService(WpfIntelligentScissorsOptions options = null)
        {
            this.options = options ?? new WpfIntelligentScissorsOptions();
        }

        public bool TryBuildPlan(
            Bitmap image,
            LabelingSegmentationObject source,
            Point edgeHitPoint,
            Size imageSize,
            int edgeHitTolerancePixels,
            out WpfIntelligentScissorsPlan plan,
            out string error)
        {
            plan = null;
            error = string.Empty;
            if (image == null
                || source?.IsRasterMask != false
                || source.Points == null
                || source.Points.Count < 3
                || imageSize.Width <= 0
                || imageSize.Height <= 0
                || image.Width < imageSize.Width
                || image.Height < imageSize.Height)
            {
                error = "\uD604\uC7AC \uC774\uBBF8\uC9C0\uC758 \uC720\uD6A8\uD55C \uC218\uB3D9 \uD3F4\uB9AC\uACE4\uC744 \uC120\uD0DD\uD558\uC138\uC694.";
                return false;
            }

            int edgeIndex = PolygonAnnotationService.FindNearestEdgeIndex(
                source,
                edgeHitPoint,
                Math.Max(1, edgeHitTolerancePixels),
                out Point _);
            if (edgeIndex < 0)
            {
                error = "\uACBD\uACC4\uB97C \uB2E4\uC2DC \uACC4\uC0B0\uD560 \uD3F4\uB9AC\uACE4 \uBAA8\uC11C\uB9AC \uADFC\uCC98\uB97C \uD074\uB9AD\uD558\uC138\uC694.";
                return false;
            }

            Point start = source.Points[edgeIndex];
            Point end = source.Points[(edgeIndex + 1) % source.Points.Count];
            int radius = Math.Max(2, options.SearchRadiusPixels);
            Rectangle searchBounds = BuildSearchBounds(start, end, edgeHitPoint, radius, imageSize);
            if (searchBounds.Width * searchBounds.Height > Math.Max(1, options.MaximumSearchPixelCount))
            {
                error = "\uACBD\uACC4 \uD0D0\uC0C9 \uBC94\uC704\uAC00 \uB108\uBB34 \uD07D\uB2C8\uB2E4. \uB354 \uC9E7\uC740 \uBAA8\uC11C\uB9AC\uB97C \uC120\uD0DD\uD558\uC138\uC694.";
                return false;
            }

            var stopwatch = Stopwatch.StartNew();
            byte[] grayscale = ReadGrayscale(image, searchBounds);
            double[] gradient = BuildGradient(grayscale, searchBounds.Width, searchBounds.Height);
            if (!TryFindPath(
                gradient,
                searchBounds,
                start,
                end,
                radius,
                out List<Point> rawPath))
            {
                stopwatch.Stop();
                error = "\uC120\uD0DD\uD55C \uBAA8\uC11C\uB9AC \uC8FC\uBCC0\uC5D0\uC11C \uC5F0\uACB0\uB41C \uC774\uBBF8\uC9C0 \uACBD\uACC4\uB97C \uCC3E\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.";
                return false;
            }

            List<Point> path = SimplifyPath(rawPath, Math.Max(0D, options.SimplificationTolerancePixels));
            List<Point> replacement = ReplaceEdge(source.Points, edgeIndex, path);
            if (path.Count < 3
                || replacement.SequenceEqual(source.Points)
                || !PolygonAnnotationService.IsValidSimplePolygon(replacement, imageSize))
            {
                stopwatch.Stop();
                error = "\uC120\uD0DD\uD55C \uBAA8\uC11C\uB9AC\uC5D0 \uC801\uC6A9\uD560 \uC218 \uC788\uB294 \uC548\uC804\uD55C \uACBD\uACC4 \uACBD\uB85C\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.";
                return false;
            }

            stopwatch.Stop();
            Rectangle oldBounds = SegmentationGeometry.GetBounds(source.Points);
            Rectangle newBounds = SegmentationGeometry.GetBounds(replacement);
            plan = new WpfIntelligentScissorsPlan
            {
                Source = source,
                EdgeIndex = edgeIndex,
                OriginalPoints = source.Points.ToList(),
                PathPoints = path,
                ReplacementPoints = replacement,
                ChangedBounds = oldBounds.IsEmpty ? newBounds : Rectangle.Union(oldBounds, newBounds),
                Elapsed = stopwatch.Elapsed
            };
            return true;
        }

        public bool TryApplyPlan(
            LabelingSegmentationObject source,
            WpfIntelligentScissorsPlan plan,
            Size imageSize,
            out Rectangle changedBounds,
            out string error)
        {
            changedBounds = Rectangle.Empty;
            error = string.Empty;
            if (source == null
                || plan == null
                || !ReferenceEquals(source, plan.Source)
                || !source.Points.SequenceEqual(plan.OriginalPoints))
            {
                error = "\uD3F4\uB9AC\uACE4\uC774 \uBBF8\uB9AC\uBCF4\uAE30 \uD6C4 \uBCC0\uACBD\uB418\uC5B4 \uACBD\uACC4 \uC801\uC6A9\uC744 \uCDE8\uC18C\uD588\uC2B5\uB2C8\uB2E4.";
                return false;
            }

            return PolygonAnnotationService.TryApplyReplacementPoints(
                source,
                plan.ReplacementPoints,
                imageSize,
                PolygonAnnotationService.IntelligentScissorsStructuralOperationName,
                out changedBounds,
                out error);
        }

        private static Rectangle BuildSearchBounds(
            Point start,
            Point end,
            Point hit,
            int radius,
            Size imageSize)
        {
            int left = Math.Max(0, Math.Min(start.X, Math.Min(end.X, hit.X)) - radius);
            int top = Math.Max(0, Math.Min(start.Y, Math.Min(end.Y, hit.Y)) - radius);
            int right = Math.Min(imageSize.Width - 1, Math.Max(start.X, Math.Max(end.X, hit.X)) + radius);
            int bottom = Math.Min(imageSize.Height - 1, Math.Max(start.Y, Math.Max(end.Y, hit.Y)) + radius);
            return Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
        }

        private static byte[] ReadGrayscale(Bitmap image, Rectangle bounds)
        {
            using var cropped = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
            using (Graphics graphics = Graphics.FromImage(cropped))
            {
                graphics.DrawImage(
                    image,
                    new Rectangle(0, 0, bounds.Width, bounds.Height),
                    bounds,
                    GraphicsUnit.Pixel);
            }

            var result = new byte[bounds.Width * bounds.Height];
            BitmapData data = cropped.LockBits(
                new Rectangle(0, 0, cropped.Width, cropped.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format24bppRgb);
            try
            {
                unsafe
                {
                    byte* first = (byte*)data.Scan0;
                    for (int y = 0; y < bounds.Height; y++)
                    {
                        byte* row = first + (y * data.Stride);
                        for (int x = 0; x < bounds.Width; x++)
                        {
                            int pixel = x * 3;
                            result[(y * bounds.Width) + x] = (byte)Math.Clamp(
                                ((row[pixel + 2] * 77) + (row[pixel + 1] * 150) + (row[pixel] * 29)) >> 8,
                                0,
                                255);
                        }
                    }
                }
            }
            finally
            {
                cropped.UnlockBits(data);
            }

            return result;
        }

        private static double[] BuildGradient(byte[] grayscale, int width, int height)
        {
            var gradient = new double[width * height];
            for (int y = 1; y < height - 1; y++)
            {
                for (int x = 1; x < width - 1; x++)
                {
                    int topLeft = grayscale[((y - 1) * width) + x - 1];
                    int top = grayscale[((y - 1) * width) + x];
                    int topRight = grayscale[((y - 1) * width) + x + 1];
                    int left = grayscale[(y * width) + x - 1];
                    int right = grayscale[(y * width) + x + 1];
                    int bottomLeft = grayscale[((y + 1) * width) + x - 1];
                    int bottom = grayscale[((y + 1) * width) + x];
                    int bottomRight = grayscale[((y + 1) * width) + x + 1];
                    int gx = -topLeft + topRight - (2 * left) + (2 * right) - bottomLeft + bottomRight;
                    int gy = -topLeft - (2 * top) - topRight + bottomLeft + (2 * bottom) + bottomRight;
                    gradient[(y * width) + x] = Math.Min(1D, Math.Sqrt((gx * gx) + (gy * gy)) / 1020D);
                }
            }

            return gradient;
        }

        private static bool TryFindPath(
            double[] gradient,
            Rectangle bounds,
            Point start,
            Point end,
            int radius,
            out List<Point> path)
        {
            int width = bounds.Width;
            int height = bounds.Height;
            int pixelCount = width * height;
            int startIndex = ((start.Y - bounds.Top) * width) + start.X - bounds.Left;
            int endIndex = ((end.Y - bounds.Top) * width) + end.X - bounds.Left;
            var distances = Enumerable.Repeat(double.PositiveInfinity, pixelCount).ToArray();
            var previous = Enumerable.Repeat(-1, pixelCount).ToArray();
            var closed = new bool[pixelCount];
            var queue = new PriorityQueue<int, (double Score, int Index)>();
            distances[startIndex] = 0D;
            queue.Enqueue(startIndex, (Distance(start, end), startIndex));
            double radiusSquared = radius * radius;

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (closed[current])
                {
                    continue;
                }

                closed[current] = true;
                if (current == endIndex)
                {
                    break;
                }

                int currentX = current % width;
                int currentY = current / width;
                foreach ((int offsetX, int offsetY, double stepDistance) in NeighborOffsets)
                {
                    int nextX = currentX + offsetX;
                    int nextY = currentY + offsetY;
                    if (nextX < 0 || nextY < 0 || nextX >= width || nextY >= height)
                    {
                        continue;
                    }

                    int next = (nextY * width) + nextX;
                    if (closed[next])
                    {
                        continue;
                    }

                    Point imagePoint = new Point(bounds.Left + nextX, bounds.Top + nextY);
                    double lineDistanceSquared = DistanceToSegmentSquared(imagePoint, start, end);
                    if (next != endIndex && lineDistanceSquared > radiusSquared)
                    {
                        continue;
                    }

                    double edgeCost = 1D + ((1D - gradient[next]) * 8D);
                    double corridorCost = 0.4D * Math.Sqrt(lineDistanceSquared) / radius;
                    double candidate = distances[current] + (stepDistance * edgeCost) + corridorCost;
                    if (candidate + 1E-9 < distances[next]
                        || (Math.Abs(candidate - distances[next]) <= 1E-9
                            && (previous[next] < 0 || current < previous[next])))
                    {
                        distances[next] = candidate;
                        previous[next] = current;
                        double heuristic = Distance(imagePoint, end);
                        queue.Enqueue(next, (candidate + heuristic, next));
                    }
                }
            }

            if (double.IsPositiveInfinity(distances[endIndex]))
            {
                path = null;
                return false;
            }

            var reversed = new List<Point>();
            int cursor = endIndex;
            while (cursor >= 0)
            {
                reversed.Add(new Point(bounds.Left + (cursor % width), bounds.Top + (cursor / width)));
                if (cursor == startIndex)
                {
                    break;
                }

                cursor = previous[cursor];
            }

            if (reversed.Count == 0 || reversed[reversed.Count - 1] != start)
            {
                path = null;
                return false;
            }

            reversed.Reverse();
            path = reversed;
            return true;
        }

        private static List<Point> ReplaceEdge(IReadOnlyList<Point> points, int edgeIndex, IReadOnlyList<Point> path)
        {
            var replacement = new List<Point>(points.Count + Math.Max(0, path.Count - 2));
            for (int index = 0; index < points.Count; index++)
            {
                replacement.Add(points[index]);
                if (index == edgeIndex)
                {
                    for (int pathIndex = 1; pathIndex < path.Count - 1; pathIndex++)
                    {
                        if (replacement[replacement.Count - 1] != path[pathIndex])
                        {
                            replacement.Add(path[pathIndex]);
                        }
                    }
                }
            }

            return replacement;
        }

        private static List<Point> SimplifyPath(IReadOnlyList<Point> points, double tolerance)
        {
            if (points == null || points.Count <= 2 || tolerance <= 0D)
            {
                return points?.ToList() ?? new List<Point>();
            }

            var keep = new bool[points.Count];
            keep[0] = true;
            keep[points.Count - 1] = true;
            SimplifyRange(points, 0, points.Count - 1, tolerance * tolerance, keep);
            return points.Where((point, index) => keep[index]).ToList();
        }

        private static void SimplifyRange(
            IReadOnlyList<Point> points,
            int first,
            int last,
            double toleranceSquared,
            bool[] keep)
        {
            if (last <= first + 1)
            {
                return;
            }

            int farthestIndex = -1;
            double farthestDistance = toleranceSquared;
            for (int index = first + 1; index < last; index++)
            {
                double distance = DistanceToSegmentSquared(points[index], points[first], points[last]);
                if (distance > farthestDistance)
                {
                    farthestDistance = distance;
                    farthestIndex = index;
                }
            }

            if (farthestIndex < 0)
            {
                return;
            }

            keep[farthestIndex] = true;
            SimplifyRange(points, first, farthestIndex, toleranceSquared, keep);
            SimplifyRange(points, farthestIndex, last, toleranceSquared, keep);
        }

        private static double Distance(Point first, Point second)
        {
            double dx = first.X - second.X;
            double dy = first.Y - second.Y;
            return Math.Sqrt((dx * dx) + (dy * dy));
        }

        private static double DistanceToSegmentSquared(Point point, Point first, Point second)
        {
            double dx = second.X - first.X;
            double dy = second.Y - first.Y;
            double lengthSquared = (dx * dx) + (dy * dy);
            if (lengthSquared <= double.Epsilon)
            {
                dx = point.X - first.X;
                dy = point.Y - first.Y;
                return (dx * dx) + (dy * dy);
            }

            double projection = ((point.X - first.X) * dx + (point.Y - first.Y) * dy) / lengthSquared;
            projection = Math.Clamp(projection, 0D, 1D);
            double projectedX = first.X + (projection * dx);
            double projectedY = first.Y + (projection * dy);
            dx = point.X - projectedX;
            dy = point.Y - projectedY;
            return (dx * dx) + (dy * dy);
        }
    }

    [Obsolete("Use IntelligentScissorsService.", false)]
    public sealed class WpfIntelligentScissorsService : IntelligentScissorsService
    {
        public WpfIntelligentScissorsService(WpfIntelligentScissorsOptions options = null) : base(options)
        {
        }
    }
}
