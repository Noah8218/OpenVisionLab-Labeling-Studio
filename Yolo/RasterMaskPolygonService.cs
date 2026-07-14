using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace MvcVisionSystem.Yolo
{
    internal static class RasterMaskPolygonService
    {
        public static IReadOnlyList<SegmentationGeometry.SegmentationMaskRegion> BuildRegions(
            byte[] maskData,
            Size maskSize,
            Size imageSize)
        {
            if (maskData == null
                || maskSize.Width <= 0
                || maskSize.Height <= 0
                || maskData.Length != maskSize.Width * maskSize.Height
                || maskSize != imageSize)
            {
                return Array.Empty<SegmentationGeometry.SegmentationMaskRegion>();
            }

            List<BoundaryEdge> edges = BuildBoundaryEdges(maskData, maskSize);
            if (edges.Count == 0)
            {
                return Array.Empty<SegmentationGeometry.SegmentationMaskRegion>();
            }

            var outgoing = edges
                .GroupBy(edge => edge.Start)
                .ToDictionary(group => group.Key, group => group.ToList());
            var used = new HashSet<BoundaryEdge>();
            var loops = new List<BoundaryLoop>();
            foreach (BoundaryEdge edge in edges)
            {
                if (used.Contains(edge))
                {
                    continue;
                }

                List<Point> gridLoop = TraceLoop(edge, outgoing, used, edges.Count);
                List<Point> polygon = ConvertGridLoopToPolygon(gridLoop, imageSize);
                if (polygon.Count < 3)
                {
                    continue;
                }

                long signedArea = GetSignedArea(polygon);
                if (signedArea != 0)
                {
                    loops.Add(new BoundaryLoop(polygon, signedArea));
                }
            }

            List<BoundaryLoop> outerLoops = loops
                .Where(loop => loop.SignedArea > 0)
                .OrderByDescending(loop => loop.AbsoluteArea)
                .ToList();
            BoundaryLoop promotedOuter = null;
            if (outerLoops.Count == 0 && loops.Count > 0)
            {
                promotedOuter = loops.OrderByDescending(loop => loop.AbsoluteArea).First();
                List<Point> reversed = promotedOuter.Points.AsEnumerable().Reverse().ToList();
                outerLoops.Add(new BoundaryLoop(reversed, promotedOuter.AbsoluteArea));
            }

            var entries = outerLoops
                .Select(loop => new RegionEntry(
                    loop,
                    new SegmentationGeometry.SegmentationMaskRegion { Points = loop.Points }))
                .ToList();
            foreach (BoundaryLoop cutout in loops.Where(loop => loop.SignedArea < 0 && !ReferenceEquals(loop, promotedOuter)))
            {
                Point probe = new Point(
                    (int)Math.Round(cutout.Points.Average(point => point.X)),
                    (int)Math.Round(cutout.Points.Average(point => point.Y)));
                RegionEntry owner = entries
                    .Where(entry => SegmentationGeometry.ContainsPoint(entry.Loop.Points, probe))
                    .OrderBy(entry => entry.Loop.AbsoluteArea)
                    .FirstOrDefault();
                owner?.Region.Cutouts.Add(cutout.Points);
            }

            return entries
                .OrderByDescending(entry => entry.Loop.AbsoluteArea)
                .Select(entry => entry.Region)
                .ToList();
        }

        private static List<BoundaryEdge> BuildBoundaryEdges(byte[] maskData, Size size)
        {
            Rectangle bounds = SegmentationGeometry.GetMaskBounds(maskData, size);
            var edges = new List<BoundaryEdge>();
            for (int y = bounds.Top; y < bounds.Bottom; y++)
            {
                int rowOffset = y * size.Width;
                for (int x = bounds.Left; x < bounds.Right; x++)
                {
                    if (maskData[rowOffset + x] == 0)
                    {
                        continue;
                    }

                    if (y == 0 || maskData[((y - 1) * size.Width) + x] == 0)
                    {
                        edges.Add(new BoundaryEdge(new Point(x, y), new Point(x + 1, y)));
                    }
                    if (x == size.Width - 1 || maskData[rowOffset + x + 1] == 0)
                    {
                        edges.Add(new BoundaryEdge(new Point(x + 1, y), new Point(x + 1, y + 1)));
                    }
                    if (y == size.Height - 1 || maskData[((y + 1) * size.Width) + x] == 0)
                    {
                        edges.Add(new BoundaryEdge(new Point(x + 1, y + 1), new Point(x, y + 1)));
                    }
                    if (x == 0 || maskData[rowOffset + x - 1] == 0)
                    {
                        edges.Add(new BoundaryEdge(new Point(x, y + 1), new Point(x, y)));
                    }
                }
            }

            return edges;
        }

        private static List<Point> TraceLoop(
            BoundaryEdge first,
            IReadOnlyDictionary<Point, List<BoundaryEdge>> outgoing,
            ISet<BoundaryEdge> used,
            int edgeCount)
        {
            var points = new List<Point> { first.Start };
            BoundaryEdge current = first;
            for (int guard = 0; guard <= edgeCount; guard++)
            {
                if (!used.Add(current))
                {
                    return new List<Point>();
                }

                if (current.End == first.Start)
                {
                    return points;
                }

                points.Add(current.End);
                if (!TrySelectNextEdge(current, outgoing, used, out BoundaryEdge next))
                {
                    return new List<Point>();
                }

                current = next;
            }

            return new List<Point>();
        }

        private static bool TrySelectNextEdge(
            BoundaryEdge current,
            IReadOnlyDictionary<Point, List<BoundaryEdge>> outgoing,
            ISet<BoundaryEdge> used,
            out BoundaryEdge next)
        {
            next = default;
            if (!outgoing.TryGetValue(current.End, out List<BoundaryEdge> candidates))
            {
                return false;
            }

            int direction = GetDirection(current);
            int[] priorities = { (direction + 1) % 4, direction, (direction + 3) % 4, (direction + 2) % 4 };
            foreach (int preferredDirection in priorities)
            {
                foreach (BoundaryEdge candidate in candidates)
                {
                    if (!used.Contains(candidate) && GetDirection(candidate) == preferredDirection)
                    {
                        next = candidate;
                        return true;
                    }
                }
            }

            return false;
        }

        private static int GetDirection(BoundaryEdge edge)
        {
            if (edge.End.X > edge.Start.X)
            {
                return 0;
            }
            if (edge.End.Y > edge.Start.Y)
            {
                return 1;
            }
            if (edge.End.X < edge.Start.X)
            {
                return 2;
            }

            return 3;
        }

        private static List<Point> ConvertGridLoopToPolygon(IReadOnlyList<Point> gridLoop, Size imageSize)
        {
            if (gridLoop == null || gridLoop.Count < 4)
            {
                return new List<Point>();
            }

            int maxX = gridLoop.Max(point => point.X);
            int maxY = gridLoop.Max(point => point.Y);
            var gridCorners = new List<Point>();
            for (int index = 0; index < gridLoop.Count; index++)
            {
                Point previous = gridLoop[(index + gridLoop.Count - 1) % gridLoop.Count];
                Point current = gridLoop[index];
                Point following = gridLoop[(index + 1) % gridLoop.Count];
                int cross = ((current.X - previous.X) * (following.Y - current.Y))
                    - ((current.Y - previous.Y) * (following.X - current.X));
                if (cross != 0)
                {
                    gridCorners.Add(current);
                }
            }

            List<Point> mapped = gridCorners
                .Select(point => new Point(
                    Math.Clamp(point.X == maxX ? point.X - 1 : point.X, 0, imageSize.Width - 1),
                    Math.Clamp(point.Y == maxY ? point.Y - 1 : point.Y, 0, imageSize.Height - 1)))
                .ToList();

            return SegmentationGeometry.NormalizePolygon(
                mapped,
                imageSize,
                minimumDistance: 1,
                simplificationTolerance: 0D);
        }

        private static long GetSignedArea(IReadOnlyList<Point> points)
        {
            long area = 0;
            for (int index = 0; index < points.Count; index++)
            {
                Point current = points[index];
                Point next = points[(index + 1) % points.Count];
                area += ((long)current.X * next.Y) - ((long)next.X * current.Y);
            }

            return area;
        }

        private readonly struct BoundaryEdge : IEquatable<BoundaryEdge>
        {
            public BoundaryEdge(Point start, Point end)
            {
                Start = start;
                End = end;
            }

            public Point Start { get; }

            public Point End { get; }

            public bool Equals(BoundaryEdge other) => Start == other.Start && End == other.End;

            public override bool Equals(object obj) => obj is BoundaryEdge other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(Start, End);
        }

        private sealed class BoundaryLoop
        {
            public BoundaryLoop(List<Point> points, long signedArea)
            {
                Points = points;
                SignedArea = signedArea;
            }

            public List<Point> Points { get; }

            public long SignedArea { get; }

            public long AbsoluteArea => Math.Abs(SignedArea);
        }

        private sealed class RegionEntry
        {
            public RegionEntry(BoundaryLoop loop, SegmentationGeometry.SegmentationMaskRegion region)
            {
                Loop = loop;
                Region = region;
            }

            public BoundaryLoop Loop { get; }

            public SegmentationGeometry.SegmentationMaskRegion Region { get; }
        }
    }
}
