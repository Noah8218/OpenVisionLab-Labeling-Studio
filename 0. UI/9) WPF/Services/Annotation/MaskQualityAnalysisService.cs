using System;
using System.Collections.Generic;
using System.Drawing;

namespace MvcVisionSystem
{
    public enum MaskQualityMarkerKind
    {
        SmallComponent,
        Hole
    }

    public sealed class MaskQualityMarker
    {
        public MaskQualityMarker(MaskQualityMarkerKind kind, int area, Rectangle bounds)
        {
            Kind = kind;
            Area = area;
            Bounds = bounds;
        }

        public MaskQualityMarkerKind Kind { get; }

        public int Area { get; }

        public Rectangle Bounds { get; }
    }

    public sealed class MaskQualityAnalysisResult
    {
        public MaskQualityAnalysisResult(IReadOnlyList<MaskQualityMarker> markers)
        {
            Markers = markers ?? Array.Empty<MaskQualityMarker>();
        }

        public IReadOnlyList<MaskQualityMarker> Markers { get; }
    }

    /// <summary>
    /// Performs read-only binary-mask measurements. Product-owned thresholds are
    /// supplied by the caller; this service never repairs or rewrites the mask.
    /// </summary>
    public sealed class MaskQualityAnalysisService
    {
        private static readonly Point[] Neighbors =
        {
            new Point(-1, 0),
            new Point(1, 0),
            new Point(0, -1),
            new Point(0, 1)
        };

        public MaskQualityAnalysisResult Analyze(
            byte[] maskData,
            Size maskSize,
            int smallComponentMaximumArea,
            int holeMaximumArea)
        {
            if (maskSize.Width <= 0 || maskSize.Height <= 0 || maskData == null)
            {
                return new MaskQualityAnalysisResult(Array.Empty<MaskQualityMarker>());
            }

            long pixelCount = (long)maskSize.Width * maskSize.Height;
            if (pixelCount > int.MaxValue || maskData.Length != pixelCount)
            {
                return new MaskQualityAnalysisResult(Array.Empty<MaskQualityMarker>());
            }

            var markers = new List<MaskQualityMarker>();
            if (smallComponentMaximumArea > 0)
            {
                CollectRegions(maskData, maskSize, foreground: true, smallComponentMaximumArea, includeBoundaryRegions: true,
                    MaskQualityMarkerKind.SmallComponent, markers);
            }

            if (holeMaximumArea > 0)
            {
                CollectRegions(maskData, maskSize, foreground: false, holeMaximumArea, includeBoundaryRegions: false,
                    MaskQualityMarkerKind.Hole, markers);
            }

            return new MaskQualityAnalysisResult(markers);
        }

        private static void CollectRegions(
            byte[] maskData,
            Size size,
            bool foreground,
            int maximumArea,
            bool includeBoundaryRegions,
            MaskQualityMarkerKind kind,
            ICollection<MaskQualityMarker> markers)
        {
            var visited = new bool[maskData.Length];
            var queue = new Queue<Point>();
            for (int y = 0; y < size.Height; y++)
            {
                for (int x = 0; x < size.Width; x++)
                {
                    int startIndex = (y * size.Width) + x;
                    if (visited[startIndex] || (maskData[startIndex] != 0) != foreground)
                    {
                        continue;
                    }

                    visited[startIndex] = true;
                    queue.Enqueue(new Point(x, y));
                    int area = 0;
                    int left = x;
                    int top = y;
                    int right = x;
                    int bottom = y;
                    bool touchesBoundary = false;
                    while (queue.Count > 0)
                    {
                        Point point = queue.Dequeue();
                        area++;
                        left = Math.Min(left, point.X);
                        top = Math.Min(top, point.Y);
                        right = Math.Max(right, point.X);
                        bottom = Math.Max(bottom, point.Y);
                        touchesBoundary |= point.X == 0 || point.Y == 0 || point.X == size.Width - 1 || point.Y == size.Height - 1;

                        foreach (Point offset in Neighbors)
                        {
                            int nextX = point.X + offset.X;
                            int nextY = point.Y + offset.Y;
                            if (nextX < 0 || nextY < 0 || nextX >= size.Width || nextY >= size.Height)
                            {
                                continue;
                            }

                            int nextIndex = (nextY * size.Width) + nextX;
                            if (visited[nextIndex] || (maskData[nextIndex] != 0) != foreground)
                            {
                                continue;
                            }

                            visited[nextIndex] = true;
                            queue.Enqueue(new Point(nextX, nextY));
                        }
                    }

                    if (area <= maximumArea && (includeBoundaryRegions || !touchesBoundary))
                    {
                        markers.Add(new MaskQualityMarker(kind, area, Rectangle.FromLTRB(left, top, right + 1, bottom + 1)));
                    }
                }
            }
        }
    }
}
