using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Cv2 = OpenCvSharp.Cv2;
using CvContourApproximationModes = OpenCvSharp.ContourApproximationModes;
using CvHierarchyIndex = OpenCvSharp.HierarchyIndex;
using CvMat = OpenCvSharp.Mat;
using CvMatType = OpenCvSharp.MatType;
using CvPoint = OpenCvSharp.Point;
using CvRetrievalModes = OpenCvSharp.RetrievalModes;
using CvScalar = OpenCvSharp.Scalar;

namespace MvcVisionSystem
{
    public static class SegmentationGeometry
    {
        public sealed class SegmentationMaskRegion
        {
            public List<Point> Points { get; set; } = new List<Point>();

            public List<List<Point>> Cutouts { get; set; } = new List<List<Point>>();
        }

        public static List<Point> NormalizePolygon(IEnumerable<Point> points, Size imageSize, int minimumDistance = 2, double simplificationTolerance = 0D)
        {
            var normalized = new List<Point>();
            if (points == null || imageSize.Width <= 0 || imageSize.Height <= 0)
            {
                return normalized;
            }

            int maxX = imageSize.Width - 1;
            int maxY = imageSize.Height - 1;
            int minDistanceSquared = Math.Max(1, minimumDistance) * Math.Max(1, minimumDistance);
            Point? last = null;

            foreach (Point point in points)
            {
                var clipped = new Point(
                    Math.Clamp(point.X, 0, maxX),
                    Math.Clamp(point.Y, 0, maxY));

                if (last.HasValue && DistanceSquared(last.Value, clipped) < minDistanceSquared)
                {
                    continue;
                }

                normalized.Add(clipped);
                last = clipped;
            }

            while (normalized.Count > 1 && normalized[0] == normalized[^1])
            {
                normalized.RemoveAt(normalized.Count - 1);
            }

            if (normalized.Distinct().Count() < 3)
            {
                normalized.Clear();
                return normalized;
            }

            Rectangle bounds = GetBounds(normalized);
            if (bounds.Width <= 1 || bounds.Height <= 1)
            {
                normalized.Clear();
            }

            if (normalized.Count > 3 && simplificationTolerance > 0D)
            {
                normalized = SimplifyPolygon(normalized, simplificationTolerance);
            }

            return normalized;
        }

        public static List<Point> SimplifyPolygon(IEnumerable<Point> points, double tolerance)
        {
            List<Point> source = points?.ToList() ?? new List<Point>();
            if (source.Count <= 3 || tolerance <= 0D)
            {
                return source;
            }

            List<Point> simplified = SimplifyPolyline(source, Math.Max(0.1D, tolerance));
            if (simplified.Distinct().Count() < 3)
            {
                return source;
            }

            return simplified;
        }

        public static Rectangle GetBounds(IEnumerable<Point> points)
        {
            if (points == null)
            {
                return Rectangle.Empty;
            }

            bool hasPoint = false;
            int left = int.MaxValue;
            int top = int.MaxValue;
            int right = int.MinValue;
            int bottom = int.MinValue;

            foreach (Point point in points)
            {
                hasPoint = true;
                left = Math.Min(left, point.X);
                top = Math.Min(top, point.Y);
                right = Math.Max(right, point.X);
                bottom = Math.Max(bottom, point.Y);
            }

            return hasPoint
                ? Rectangle.FromLTRB(left, top, right + 1, bottom + 1)
                : Rectangle.Empty;
        }

        public static List<Point> RectangleToPolygon(Rectangle rectangle, Size imageSize)
        {
            Rectangle clipped = Rectangle.Intersect(rectangle, new Rectangle(Point.Empty, imageSize));
            if (clipped.Width <= 1 || clipped.Height <= 1)
            {
                return new List<Point>();
            }

            return NormalizePolygon(
                new[]
                {
                    new Point(clipped.Left, clipped.Top),
                    new Point(clipped.Right - 1, clipped.Top),
                    new Point(clipped.Right - 1, clipped.Bottom - 1),
                    new Point(clipped.Left, clipped.Bottom - 1)
                },
                imageSize,
                minimumDistance: 1);
        }

        public static List<Point> CircleToPolygon(Point center, int radius, Size imageSize, int pointCount = 24)
        {
            if (imageSize.Width <= 0 || imageSize.Height <= 0 || radius <= 0)
            {
                return new List<Point>();
            }

            int safePointCount = Math.Max(8, pointCount);
            var points = new List<Point>(safePointCount);
            for (int i = 0; i < safePointCount; i++)
            {
                double angle = (Math.PI * 2D * i) / safePointCount;
                points.Add(new Point(
                    center.X + (int)Math.Round(Math.Cos(angle) * radius),
                    center.Y + (int)Math.Round(Math.Sin(angle) * radius)));
            }

            return NormalizePolygon(points, imageSize, minimumDistance: 1, simplificationTolerance: 0.75D);
        }

        public static List<List<Point>> BrushStrokeToPolygons(IEnumerable<Point> centers, int radius, Size imageSize)
        {
            if (imageSize.Width <= 0 || imageSize.Height <= 0 || radius <= 0)
            {
                return new List<List<Point>>();
            }

            int safeRadius = Math.Max(1, radius);
            List<Point> validCenters = centers?
                .Where(point => !point.IsEmpty)
                .Select(point => new Point(
                    Math.Clamp(point.X, 0, imageSize.Width - 1),
                    Math.Clamp(point.Y, 0, imageSize.Height - 1)))
                .Distinct()
                .ToList() ?? new List<Point>();
            if (validCenters.Count == 0)
            {
                return new List<List<Point>>();
            }

            Rectangle workBounds = BuildBrushWorkBounds(validCenters, safeRadius, imageSize);
            if (workBounds.Width <= 1 || workBounds.Height <= 1)
            {
                return new List<List<Point>>();
            }

            using var mask = new CvMat(workBounds.Height, workBounds.Width, CvMatType.CV_8UC1, CvScalar.Black);
            foreach (Point center in validCenters)
            {
                Cv2.Circle(
                    mask,
                    new CvPoint(center.X - workBounds.Left, center.Y - workBounds.Top),
                    safeRadius,
                    CvScalar.White,
                    thickness: -1);
            }

            Cv2.FindContours(
                mask,
                out CvPoint[][] contours,
                out CvHierarchyIndex[] _,
                CvRetrievalModes.External,
                CvContourApproximationModes.ApproxSimple);

            var polygons = new List<List<Point>>();
            foreach (CvPoint[] contour in contours.OrderByDescending(contour => Cv2.ContourArea(contour)))
            {
                if (contour == null || contour.Length < 3 || Cv2.ContourArea(contour) < 2D)
                {
                    continue;
                }

                List<Point> points = contour
                    .Select(point => new Point(point.X + workBounds.Left, point.Y + workBounds.Top))
                    .ToList();
                List<Point> normalized = NormalizePolygon(points, imageSize, minimumDistance: 1, simplificationTolerance: 1.0D);
                if (normalized.Count >= 3)
                {
                    polygons.Add(normalized);
                }
            }

            return polygons;
        }

        public static Rectangle GetMaskBounds(byte[] maskData, Size maskSize)
        {
            if (maskData == null || maskSize.Width <= 0 || maskSize.Height <= 0 || maskData.Length != maskSize.Width * maskSize.Height)
            {
                return Rectangle.Empty;
            }

            int left = maskSize.Width;
            int top = maskSize.Height;
            int right = -1;
            int bottom = -1;
            for (int y = 0; y < maskSize.Height; y++)
            {
                int rowOffset = y * maskSize.Width;
                for (int x = 0; x < maskSize.Width; x++)
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

            return right < left || bottom < top
                ? Rectangle.Empty
                : Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
        }

        public static List<SegmentationMaskRegion> RasterMaskToRegions(byte[] maskData, Size maskSize, Size imageSize)
        {
            if (maskData == null || maskSize.Width <= 0 || maskSize.Height <= 0 || maskData.Length != maskSize.Width * maskSize.Height)
            {
                return new List<SegmentationMaskRegion>();
            }

            Rectangle bounds = GetMaskBounds(maskData, maskSize);
            if (bounds.IsEmpty)
            {
                return new List<SegmentationMaskRegion>();
            }

            Rectangle workBounds = Rectangle.Intersect(bounds, new Rectangle(Point.Empty, imageSize));
            if (workBounds.Width <= 1 || workBounds.Height <= 1)
            {
                return new List<SegmentationMaskRegion>();
            }

            return BuildMaskRegions(imageSize, workBounds, mask =>
            {
                for (int y = workBounds.Top; y < workBounds.Bottom; y++)
                {
                    int rowOffset = y * maskSize.Width;
                    for (int x = workBounds.Left; x < workBounds.Right; x++)
                    {
                        if (maskData[rowOffset + x] != 0)
                        {
                            mask.Set(y - workBounds.Top, x - workBounds.Left, (byte)255);
                        }
                    }
                }
            });
        }

        public static List<SegmentationMaskRegion> AddBrushStrokeToMask(
            IEnumerable<IEnumerable<Point>> polygons,
            IEnumerable<IEnumerable<Point>> cutouts,
            IEnumerable<Point> centers,
            int radius,
            Size imageSize)
        {
            List<Point> validCenters = NormalizeCenters(centers, imageSize);
            List<List<Point>> normalizedPolygons = NormalizePolygonList(polygons, imageSize, simplificationTolerance: 0D);
            List<List<Point>> normalizedCutouts = NormalizePolygonList(cutouts, imageSize, simplificationTolerance: 0.75D);
            if (imageSize.Width <= 0 || imageSize.Height <= 0 || (validCenters.Count == 0 && normalizedPolygons.Count == 0))
            {
                return new List<SegmentationMaskRegion>();
            }

            int safeRadius = Math.Max(1, radius);
            Rectangle workBounds = BuildMaskWorkBounds(normalizedPolygons, normalizedCutouts, validCenters, safeRadius, imageSize);
            if (workBounds.Width <= 1 || workBounds.Height <= 1)
            {
                return new List<SegmentationMaskRegion>();
            }

            return BuildMaskRegions(imageSize, workBounds, mask =>
            {
                FillPolygons(mask, normalizedPolygons, workBounds, CvScalar.White);
                FillPolygons(mask, normalizedCutouts, workBounds, CvScalar.Black);
                FillBrushStroke(mask, validCenters, safeRadius, workBounds, CvScalar.White);
            });
        }

        public static List<SegmentationMaskRegion> EraseBrushStrokeFromPolygon(
            IEnumerable<Point> polygon,
            IEnumerable<Point> centers,
            int radius,
            Size imageSize,
            IEnumerable<IEnumerable<Point>> cutouts = null)
        {
            List<Point> source = NormalizePolygon(polygon, imageSize, minimumDistance: 1, simplificationTolerance: 0D);
            List<Point> validCenters = NormalizeCenters(centers, imageSize);
            List<List<Point>> normalizedCutouts = NormalizePolygonList(cutouts, imageSize, simplificationTolerance: 0.75D);
            var result = new List<SegmentationMaskRegion>();
            int safeRadius = Math.Max(1, radius);
            if (source.Count < 3 || validCenters.Count == 0 || imageSize.Width <= 0 || imageSize.Height <= 0)
            {
                return result;
            }

            Rectangle workBounds = BuildMaskWorkBounds(new[] { source }, normalizedCutouts, validCenters, safeRadius, imageSize);
            if (workBounds.Width <= 1 || workBounds.Height <= 1)
            {
                return result;
            }

            return BuildMaskRegions(imageSize, workBounds, mask =>
            {
                FillPolygons(mask, new[] { source }, workBounds, CvScalar.White);
                FillPolygons(mask, normalizedCutouts, workBounds, CvScalar.Black);
                FillBrushStroke(mask, validCenters, safeRadius, workBounds, CvScalar.Black);
            });
        }

        public static List<Point> ExtractDarkRegionPolygon(Bitmap image, Rectangle searchRegion, int thresholdOffset = 12)
        {
            if (image == null || image.Width <= 0 || image.Height <= 0)
            {
                return new List<Point>();
            }

            Rectangle clipped = Rectangle.Intersect(searchRegion, new Rectangle(Point.Empty, image.Size));
            if (clipped.Width <= 1 || clipped.Height <= 1)
            {
                return new List<Point>();
            }

            int[,] gray = new int[clipped.Width, clipped.Height];
            long sum = 0;
            int minGray = 255;
            int maxGray = 0;
            for (int y = 0; y < clipped.Height; y++)
            {
                for (int x = 0; x < clipped.Width; x++)
                {
                    Color color = image.GetPixel(clipped.Left + x, clipped.Top + y);
                    int value = (color.R + color.G + color.B) / 3;
                    gray[x, y] = value;
                    sum += value;
                    minGray = Math.Min(minGray, value);
                    maxGray = Math.Max(maxGray, value);
                }
            }

            int mean = (int)(sum / Math.Max(1, clipped.Width * clipped.Height));
            int contrast = maxGray - minGray;
            if (contrast < 6)
            {
                return new List<Point>();
            }

            int contrastThreshold = minGray + Math.Max(4, (int)Math.Round(contrast * 0.45D));
            int meanThreshold = mean - Math.Max(2, thresholdOffset / 2);
            int threshold = Math.Clamp(Math.Min(contrastThreshold, meanThreshold), minGray + 1, maxGray);
            bool[,] dark = new bool[clipped.Width, clipped.Height];
            int darkCount = 0;
            for (int y = 0; y < clipped.Height; y++)
            {
                for (int x = 0; x < clipped.Width; x++)
                {
                    dark[x, y] = gray[x, y] <= threshold;
                    if (dark[x, y])
                    {
                        darkCount++;
                    }
                }
            }

            if (darkCount == 0)
            {
                return new List<Point>();
            }

            List<DarkRegionComponent> components = FindDarkRegionComponents(dark, gray, clipped);
            if (components.Count == 0)
            {
                return new List<Point>();
            }

            int roiArea = Math.Max(1, clipped.Width * clipped.Height);
            int minimumPixels = Math.Max(3, (int)Math.Round(roiArea * 0.0005D));
            DarkRegionComponent selected = components
                .Where(component => !component.TouchesEdge)
                .Where(component => component.PixelCount >= minimumPixels)
                .Where(component => component.PixelCount <= roiArea * 0.45D)
                .OrderBy(component => component.MeanGray)
                .ThenByDescending(component => component.PixelCount)
                .FirstOrDefault()
                ?? components
                    .Where(component => component.PixelCount >= minimumPixels)
                    .Where(component => component.PixelCount <= roiArea * 0.55D)
                    .OrderBy(component => component.TouchesEdge)
                    .ThenBy(component => component.MeanGray)
                    .ThenByDescending(component => component.PixelCount)
                    .FirstOrDefault();

            if (selected == null)
            {
                return new List<Point>();
            }

            List<Point> hull = BuildConvexHull(selected.BoundaryPoints);
            return hull.Count >= 3
                ? NormalizePolygon(hull, image.Size, minimumDistance: 1, simplificationTolerance: 1.5D)
                : RectangleToPolygon(selected.Bounds, image.Size);
        }

        public static bool ContainsPoint(IEnumerable<Point> polygon, Point point)
        {
            if (polygon == null)
            {
                return false;
            }

            Point[] points = polygon.ToArray();
            if (points.Length < 3)
            {
                return false;
            }

            bool inside = false;
            int previous = points.Length - 1;
            for (int current = 0; current < points.Length; current++)
            {
                Point currentPoint = points[current];
                Point previousPoint = points[previous];
                bool crossesY = currentPoint.Y > point.Y != previousPoint.Y > point.Y;
                if (crossesY)
                {
                    double xIntersection = (previousPoint.X - currentPoint.X) * (point.Y - currentPoint.Y)
                        / (double)(previousPoint.Y - currentPoint.Y)
                        + currentPoint.X;
                    if (point.X < xIntersection)
                    {
                        inside = !inside;
                    }
                }

                previous = current;
            }

            return inside;
        }

        public static bool ContainsPoint(IEnumerable<Point> polygon, IEnumerable<IEnumerable<Point>> cutouts, Point point)
        {
            if (!ContainsPoint(polygon, point))
            {
                return false;
            }

            foreach (IEnumerable<Point> cutout in cutouts ?? Enumerable.Empty<IEnumerable<Point>>())
            {
                if (ContainsPoint(cutout, point))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool IntersectsCircle(IEnumerable<Point> polygon, Point center, int radius)
        {
            Point[] points = polygon?.ToArray() ?? Array.Empty<Point>();
            if (points.Length < 3 || radius <= 0)
            {
                return false;
            }

            if (ContainsPoint(points, center))
            {
                return true;
            }

            double radiusSquared = radius * radius;
            for (int i = 0; i < points.Length; i++)
            {
                Point first = points[i];
                Point second = points[(i + 1) % points.Length];
                if (DistancePointToSegmentSquared(center, first, second) <= radiusSquared)
                {
                    return true;
                }
            }

            return false;
        }

        public static List<List<Point>> EraseCircleFromPolygon(
            IEnumerable<Point> polygon,
            Point center,
            int radius,
            Size imageSize,
            IEnumerable<IEnumerable<Point>> cutouts = null)
        {
            return EraseBrushStrokeFromPolygon(polygon, new[] { center }, radius, imageSize, cutouts)
                .Select(region => region.Points)
                .Where(points => points.Count >= 3)
                .ToList();
        }

        public static List<Point> MergePolygonsToHull(IEnumerable<IEnumerable<Point>> polygons, Size imageSize)
        {
            if (polygons == null || imageSize.Width <= 0 || imageSize.Height <= 0)
            {
                return new List<Point>();
            }

            List<Point> points = polygons
                .Where(polygon => polygon != null)
                .SelectMany(polygon => polygon)
                .ToList();
            if (points.Count < 3)
            {
                return new List<Point>();
            }

            List<Point> hull = BuildConvexHull(points);
            return NormalizePolygon(hull, imageSize, minimumDistance: 1, simplificationTolerance: 1.5D);
        }

        private static Rectangle BuildBrushWorkBounds(IReadOnlyCollection<Point> centers, int radius, Size imageSize)
        {
            int left = Math.Max(0, centers.Min(point => point.X) - radius - 2);
            int top = Math.Max(0, centers.Min(point => point.Y) - radius - 2);
            int right = Math.Min(imageSize.Width, centers.Max(point => point.X) + radius + 3);
            int bottom = Math.Min(imageSize.Height, centers.Max(point => point.Y) + radius + 3);
            return Rectangle.FromLTRB(left, top, Math.Max(left + 1, right), Math.Max(top + 1, bottom));
        }

        private static List<DarkRegionComponent> FindDarkRegionComponents(bool[,] dark, int[,] gray, Rectangle offset)
        {
            int width = dark.GetLength(0);
            int height = dark.GetLength(1);
            bool[,] visited = new bool[width, height];
            var components = new List<DarkRegionComponent>();
            int[] dx = { -1, 1, 0, 0 };
            int[] dy = { 0, 0, -1, 1 };

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!dark[x, y] || visited[x, y])
                    {
                        continue;
                    }

                    var queue = new Queue<Point>();
                    var boundary = new List<Point>();
                    int left = x;
                    int top = y;
                    int right = x;
                    int bottom = y;
                    int pixelCount = 0;
                    long graySum = 0;
                    bool touchesEdge = false;

                    visited[x, y] = true;
                    queue.Enqueue(new Point(x, y));
                    while (queue.Count > 0)
                    {
                        Point point = queue.Dequeue();
                        pixelCount++;
                        graySum += gray[point.X, point.Y];
                        left = Math.Min(left, point.X);
                        top = Math.Min(top, point.Y);
                        right = Math.Max(right, point.X);
                        bottom = Math.Max(bottom, point.Y);
                        touchesEdge |= point.X == 0 || point.Y == 0 || point.X == width - 1 || point.Y == height - 1;

                        if (IsBoundaryPixel(dark, point.X, point.Y))
                        {
                            boundary.Add(new Point(offset.Left + point.X, offset.Top + point.Y));
                        }

                        for (int i = 0; i < dx.Length; i++)
                        {
                            int nextX = point.X + dx[i];
                            int nextY = point.Y + dy[i];
                            if (nextX < 0 || nextY < 0 || nextX >= width || nextY >= height)
                            {
                                continue;
                            }

                            if (!dark[nextX, nextY] || visited[nextX, nextY])
                            {
                                continue;
                            }

                            visited[nextX, nextY] = true;
                            queue.Enqueue(new Point(nextX, nextY));
                        }
                    }

                    if (pixelCount > 0 && boundary.Count > 0)
                    {
                        components.Add(new DarkRegionComponent
                        {
                            BoundaryPoints = boundary,
                            Bounds = Rectangle.FromLTRB(offset.Left + left, offset.Top + top, offset.Left + right + 1, offset.Top + bottom + 1),
                            MeanGray = graySum / (double)pixelCount,
                            PixelCount = pixelCount,
                            TouchesEdge = touchesEdge
                        });
                    }
                }
            }

            return components;
        }

        private sealed class DarkRegionComponent
        {
            public List<Point> BoundaryPoints { get; set; } = new List<Point>();

            public Rectangle Bounds { get; set; }

            public double MeanGray { get; set; }

            public int PixelCount { get; set; }

            public bool TouchesEdge { get; set; }
        }

        private static List<SegmentationMaskRegion> BuildMaskRegions(Size imageSize, Rectangle workBounds, Action<CvMat> paint)
        {
            using var mask = new CvMat(workBounds.Height, workBounds.Width, CvMatType.CV_8UC1, CvScalar.Black);
            paint(mask);

            Cv2.FindContours(
                mask,
                out CvPoint[][] contours,
                out CvHierarchyIndex[] hierarchy,
                CvRetrievalModes.CComp,
                CvContourApproximationModes.ApproxSimple);

            var regions = new List<SegmentationMaskRegion>();
            for (int i = 0; i < contours.Length; i++)
            {
                if (hierarchy.Length > i && hierarchy[i].Parent >= 0)
                {
                    continue;
                }

                List<Point> outer = ConvertContour(contours[i], workBounds, imageSize, simplificationTolerance: 1.0D);
                if (outer.Count < 3 || Cv2.ContourArea(contours[i]) < 2D)
                {
                    continue;
                }

                var region = new SegmentationMaskRegion { Points = outer };
                int child = hierarchy.Length > i ? hierarchy[i].Child : -1;
                while (child >= 0 && child < contours.Length)
                {
                    List<Point> cutout = ConvertContour(contours[child], workBounds, imageSize, simplificationTolerance: 0.75D);
                    if (cutout.Count >= 3 && Cv2.ContourArea(contours[child]) >= 2D)
                    {
                        region.Cutouts.Add(cutout);
                    }

                    child = hierarchy.Length > child ? hierarchy[child].Next : -1;
                }

                regions.Add(region);
            }

            return regions
                .OrderByDescending(region => GetBounds(region.Points).Width * GetBounds(region.Points).Height)
                .ToList();
        }

        private static List<Point> ConvertContour(CvPoint[] contour, Rectangle workBounds, Size imageSize, double simplificationTolerance)
        {
            if (contour == null || contour.Length < 3)
            {
                return new List<Point>();
            }

            List<Point> points = contour
                .Select(point => new Point(point.X + workBounds.Left, point.Y + workBounds.Top))
                .ToList();
            return NormalizePolygon(points, imageSize, minimumDistance: 1, simplificationTolerance: simplificationTolerance);
        }

        private static void FillPolygons(CvMat mask, IEnumerable<IEnumerable<Point>> polygons, Rectangle workBounds, CvScalar color)
        {
            foreach (IEnumerable<Point> polygon in polygons ?? Enumerable.Empty<IEnumerable<Point>>())
            {
                CvPoint[] localPolygon = polygon
                    .Select(point => new CvPoint(point.X - workBounds.Left, point.Y - workBounds.Top))
                    .ToArray();
                if (localPolygon.Length >= 3)
                {
                    Cv2.FillPoly(mask, new[] { localPolygon }, color);
                }
            }
        }

        private static void FillBrushStroke(CvMat mask, IEnumerable<Point> centers, int radius, Rectangle workBounds, CvScalar color)
        {
            foreach (Point center in centers ?? Enumerable.Empty<Point>())
            {
                Cv2.Circle(
                    mask,
                    new CvPoint(center.X - workBounds.Left, center.Y - workBounds.Top),
                    radius,
                    color,
                    thickness: -1);
            }
        }

        private static List<List<Point>> NormalizePolygonList(IEnumerable<IEnumerable<Point>> polygons, Size imageSize, double simplificationTolerance)
        {
            return polygons?
                .Select(polygon => NormalizePolygon(polygon, imageSize, minimumDistance: 1, simplificationTolerance: simplificationTolerance))
                .Where(points => points.Count >= 3)
                .ToList() ?? new List<List<Point>>();
        }

        private static List<Point> NormalizeCenters(IEnumerable<Point> centers, Size imageSize)
        {
            if (imageSize.Width <= 0 || imageSize.Height <= 0)
            {
                return new List<Point>();
            }

            return centers?
                .Where(point => !point.IsEmpty)
                .Select(point => new Point(
                    Math.Clamp(point.X, 0, imageSize.Width - 1),
                    Math.Clamp(point.Y, 0, imageSize.Height - 1)))
                .Distinct()
                .ToList() ?? new List<Point>();
        }

        private static Rectangle BuildMaskWorkBounds(
            IEnumerable<IEnumerable<Point>> polygons,
            IEnumerable<IEnumerable<Point>> cutouts,
            IEnumerable<Point> centers,
            int radius,
            Size imageSize)
        {
            var bounds = new List<Rectangle>();
            bounds.AddRange((polygons ?? Enumerable.Empty<IEnumerable<Point>>())
                .Select(GetBounds)
                .Where(rectangle => !rectangle.IsEmpty));
            bounds.AddRange((cutouts ?? Enumerable.Empty<IEnumerable<Point>>())
                .Select(GetBounds)
                .Where(rectangle => !rectangle.IsEmpty));

            foreach (Point center in centers ?? Enumerable.Empty<Point>())
            {
                bounds.Add(Rectangle.FromLTRB(
                    center.X - radius - 2,
                    center.Y - radius - 2,
                    center.X + radius + 3,
                    center.Y + radius + 3));
            }

            if (bounds.Count == 0)
            {
                return Rectangle.Empty;
            }

            Rectangle union = bounds[0];
            for (int i = 1; i < bounds.Count; i++)
            {
                union = Rectangle.Union(union, bounds[i]);
            }

            return Rectangle.Intersect(union, new Rectangle(Point.Empty, imageSize));
        }

        private static int DistanceSquared(Point first, Point second)
        {
            int dx = first.X - second.X;
            int dy = first.Y - second.Y;
            return dx * dx + dy * dy;
        }

        private static bool IsBoundaryPixel(bool[,] mask, int x, int y)
        {
            int width = mask.GetLength(0);
            int height = mask.GetLength(1);
            for (int yy = y - 1; yy <= y + 1; yy++)
            {
                for (int xx = x - 1; xx <= x + 1; xx++)
                {
                    if (xx < 0 || yy < 0 || xx >= width || yy >= height || !mask[xx, yy])
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static List<Point> BuildConvexHull(IEnumerable<Point> points)
        {
            List<Point> sorted = points?
                .Distinct()
                .OrderBy(point => point.X)
                .ThenBy(point => point.Y)
                .ToList() ?? new List<Point>();
            if (sorted.Count <= 3)
            {
                return sorted;
            }

            var lower = new List<Point>();
            foreach (Point point in sorted)
            {
                while (lower.Count >= 2 && Cross(lower[^2], lower[^1], point) <= 0)
                {
                    lower.RemoveAt(lower.Count - 1);
                }

                lower.Add(point);
            }

            var upper = new List<Point>();
            for (int i = sorted.Count - 1; i >= 0; i--)
            {
                Point point = sorted[i];
                while (upper.Count >= 2 && Cross(upper[^2], upper[^1], point) <= 0)
                {
                    upper.RemoveAt(upper.Count - 1);
                }

                upper.Add(point);
            }

            lower.RemoveAt(lower.Count - 1);
            upper.RemoveAt(upper.Count - 1);
            lower.AddRange(upper);
            return lower;
        }

        private static int Cross(Point origin, Point first, Point second)
        {
            return ((first.X - origin.X) * (second.Y - origin.Y)) - ((first.Y - origin.Y) * (second.X - origin.X));
        }

        private static double DistancePointToSegmentSquared(Point point, Point first, Point second)
        {
            double dx = second.X - first.X;
            double dy = second.Y - first.Y;
            if (Math.Abs(dx) < double.Epsilon && Math.Abs(dy) < double.Epsilon)
            {
                return DistanceSquared(point, first);
            }

            double t = (((point.X - first.X) * dx) + ((point.Y - first.Y) * dy)) / ((dx * dx) + (dy * dy));
            t = Math.Clamp(t, 0D, 1D);
            double nearestX = first.X + (t * dx);
            double nearestY = first.Y + (t * dy);
            double distanceX = point.X - nearestX;
            double distanceY = point.Y - nearestY;
            return (distanceX * distanceX) + (distanceY * distanceY);
        }

        private static List<Point> SimplifyPolyline(IReadOnlyList<Point> points, double tolerance)
        {
            if (points == null || points.Count <= 2)
            {
                return points?.ToList() ?? new List<Point>();
            }

            bool[] keep = new bool[points.Count];
            keep[0] = true;
            keep[^1] = true;
            SimplifyPolyline(points, 0, points.Count - 1, tolerance * tolerance, keep);

            var simplified = new List<Point>();
            for (int i = 0; i < points.Count; i++)
            {
                if (keep[i])
                {
                    simplified.Add(points[i]);
                }
            }

            return simplified;
        }

        private static void SimplifyPolyline(IReadOnlyList<Point> points, int first, int last, double toleranceSquared, bool[] keep)
        {
            if (last <= first + 1)
            {
                return;
            }

            double maxDistanceSquared = 0D;
            int maxIndex = first;
            for (int i = first + 1; i < last; i++)
            {
                double distanceSquared = PerpendicularDistanceSquared(points[i], points[first], points[last]);
                if (distanceSquared > maxDistanceSquared)
                {
                    maxDistanceSquared = distanceSquared;
                    maxIndex = i;
                }
            }

            if (maxDistanceSquared <= toleranceSquared)
            {
                return;
            }

            keep[maxIndex] = true;
            SimplifyPolyline(points, first, maxIndex, toleranceSquared, keep);
            SimplifyPolyline(points, maxIndex, last, toleranceSquared, keep);
        }

        private static double PerpendicularDistanceSquared(Point point, Point lineStart, Point lineEnd)
        {
            double dx = lineEnd.X - lineStart.X;
            double dy = lineEnd.Y - lineStart.Y;
            if (Math.Abs(dx) < double.Epsilon && Math.Abs(dy) < double.Epsilon)
            {
                return DistanceSquared(point, lineStart);
            }

            double numerator = (dy * point.X) - (dx * point.Y) + (lineEnd.X * lineStart.Y) - (lineEnd.Y * lineStart.X);
            return (numerator * numerator) / ((dy * dy) + (dx * dx));
        }
    }
}
