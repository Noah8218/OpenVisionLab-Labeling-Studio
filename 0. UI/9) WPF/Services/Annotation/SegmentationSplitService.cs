using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace MvcVisionSystem
{
    public enum WpfSegmentationSplitOrientation
    {
        Vertical,
        Horizontal
    }

    public class SegmentationSplitService
    {
        private static readonly Point[] FourConnectedOffsets =
        {
            new Point(-1, 0),
            new Point(1, 0),
            new Point(0, -1),
            new Point(0, 1)
        };

        public const string StructuralOperationName = "Split";

        public bool TrySplit(
            LabelingSegmentationObject source,
            WpfSegmentationSplitOrientation orientation,
            int coordinate,
            Size imageSize,
            out WpfSegmentationSplitResult result,
            out string error)
        {
            result = null;
            error = string.Empty;
            if (!SegmentationMaskGeometryService.TryRasterize(
                source,
                imageSize,
                out byte[] sourceMask,
                out Rectangle sourceBounds))
            {
                error = "\uBD84\uD560\uD560 \uC138\uADF8\uBA3C\uD2B8 \uD615\uC0C1\uC744 \uCC3E\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.";
                return false;
            }

            if (!IsCoordinateInsideBounds(orientation, coordinate, sourceBounds))
            {
                error = "\uC808\uB2E8 \uC704\uCE58\uB97C \uC120\uD0DD\uD55C \uAC1D\uCCB4 \uC548\uCABD\uC5D0 \uC9C0\uC815\uD558\uC138\uC694.";
                return false;
            }

            int originalComponentCount = FindComponents(sourceMask, imageSize, sourceBounds).Count;
            int removedPixelCount = ClearCutLine(
                sourceMask,
                imageSize,
                sourceBounds,
                orientation,
                coordinate);
            List<List<int>> components = FindComponents(sourceMask, imageSize, sourceBounds)
                .Where(component => component.Count > 0)
                .OrderBy(component => component.Min(index => index / imageSize.Width))
                .ThenBy(component => component.Min(index => index % imageSize.Width))
                .ThenByDescending(component => component.Count)
                .ToList();
            if (removedPixelCount == 0 || components.Count < 2 || components.Count <= originalComponentCount)
            {
                error = "\uC808\uB2E8\uC120\uC774 \uAC1D\uCCB4\uB97C \uB458 \uC774\uC0C1\uC73C\uB85C \uBD84\uB9AC\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.";
                return false;
            }

            string className = ResolveClassName(source);
            var segments = new List<LabelingSegmentationObject>(components.Count);
            foreach (List<int> component in components)
            {
                var componentMask = new byte[sourceMask.Length];
                foreach (int index in component)
                {
                    componentMask[index] = 255;
                }

                Rectangle componentBounds = SegmentationGeometry.GetMaskBounds(componentMask, imageSize);
                segments.Add(new LabelingSegmentationObject
                {
                    ClassName = className,
                    ClassItem = source.ClassItem,
                    ObjectId = Guid.NewGuid().ToString("N"),
                    ComponentIndex = -1,
                    ZOrder = source.ZOrder,
                    LastStructuralOperation = StructuralOperationName,
                    MaskData = componentMask,
                    MaskSize = imageSize,
                    MaskBounds = componentBounds,
                    RenderVersion = 1,
                    RenderDirtyBounds = componentBounds,
                    Selected = false
                });
            }

            segments[0].Selected = true;
            result = new WpfSegmentationSplitResult(orientation, coordinate, segments);
            return true;
        }

        private static bool IsCoordinateInsideBounds(
            WpfSegmentationSplitOrientation orientation,
            int coordinate,
            Rectangle bounds)
            => orientation == WpfSegmentationSplitOrientation.Vertical
                ? coordinate > bounds.Left && coordinate < bounds.Right - 1
                : coordinate > bounds.Top && coordinate < bounds.Bottom - 1;

        private static int ClearCutLine(
            byte[] mask,
            Size imageSize,
            Rectangle bounds,
            WpfSegmentationSplitOrientation orientation,
            int coordinate)
        {
            int removedPixelCount = 0;
            if (orientation == WpfSegmentationSplitOrientation.Vertical)
            {
                for (int y = bounds.Top; y < bounds.Bottom; y++)
                {
                    int index = (y * imageSize.Width) + coordinate;
                    if (mask[index] != 0)
                    {
                        mask[index] = 0;
                        removedPixelCount++;
                    }
                }

                return removedPixelCount;
            }

            int rowOffset = coordinate * imageSize.Width;
            for (int x = bounds.Left; x < bounds.Right; x++)
            {
                int index = rowOffset + x;
                if (mask[index] != 0)
                {
                    mask[index] = 0;
                    removedPixelCount++;
                }
            }

            return removedPixelCount;
        }

        private static List<List<int>> FindComponents(
            byte[] mask,
            Size imageSize,
            Rectangle bounds)
        {
            var components = new List<List<int>>();
            var visited = new bool[mask.Length];
            var queue = new Queue<int>();
            for (int y = bounds.Top; y < bounds.Bottom; y++)
            {
                for (int x = bounds.Left; x < bounds.Right; x++)
                {
                    int seed = (y * imageSize.Width) + x;
                    if (mask[seed] == 0 || visited[seed])
                    {
                        continue;
                    }

                    var component = new List<int>();
                    visited[seed] = true;
                    queue.Enqueue(seed);
                    while (queue.Count > 0)
                    {
                        int current = queue.Dequeue();
                        component.Add(current);
                        int currentX = current % imageSize.Width;
                        int currentY = current / imageSize.Width;
                        foreach (Point offset in FourConnectedOffsets)
                        {
                            int nextX = currentX + offset.X;
                            int nextY = currentY + offset.Y;
                            if (nextX < bounds.Left
                                || nextX >= bounds.Right
                                || nextY < bounds.Top
                                || nextY >= bounds.Bottom)
                            {
                                continue;
                            }

                            int next = (nextY * imageSize.Width) + nextX;
                            if (mask[next] == 0 || visited[next])
                            {
                                continue;
                            }

                            visited[next] = true;
                            queue.Enqueue(next);
                        }
                    }

                    components.Add(component);
                }
            }

            return components;
        }

        private static string ResolveClassName(LabelingSegmentationObject segment)
        {
            string className = segment?.ClassName;
            if (string.IsNullOrWhiteSpace(className))
            {
                className = segment?.ClassItem?.Text;
            }

            return string.IsNullOrWhiteSpace(className) ? "Defect" : className.Trim();
        }
    }

    [Obsolete("Use SegmentationSplitService.", false)]
    public sealed class WpfSegmentationSplitService : SegmentationSplitService
    {
    }

    public sealed class WpfSegmentationSplitResult
    {
        public WpfSegmentationSplitResult(
            WpfSegmentationSplitOrientation orientation,
            int coordinate,
            IReadOnlyList<LabelingSegmentationObject> segments)
        {
            Orientation = orientation;
            Coordinate = coordinate;
            Segments = segments ?? Array.Empty<LabelingSegmentationObject>();
        }

        public WpfSegmentationSplitOrientation Orientation { get; }

        public int Coordinate { get; }

        public IReadOnlyList<LabelingSegmentationObject> Segments { get; }
    }
}
