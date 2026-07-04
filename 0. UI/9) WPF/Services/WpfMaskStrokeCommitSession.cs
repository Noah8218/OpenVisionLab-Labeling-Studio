using System.Collections.Generic;
using System.Drawing;

namespace MvcVisionSystem
{
    internal sealed class WpfMaskStrokeCommitSession
    {
        private List<Point> centers = new List<Point>();
        private readonly HashSet<int> centerKeys = new HashSet<int>();

        public IReadOnlyList<Point> Centers => centers;

        public int Count => centers.Count;

        public int Radius { get; private set; }

        public WpfAnnotationTool Tool { get; private set; } = WpfAnnotationTool.Select;

        public string ClassName { get; private set; } = string.Empty;

        public void Begin(int radius, WpfAnnotationTool tool, string className)
        {
            Reset();
            Radius = radius;
            Tool = tool;
            ClassName = className ?? string.Empty;
        }

        public IReadOnlyList<Point> Append(IEnumerable<Point> strokeCenters, Size imageSize)
        {
            var addedCenters = new List<Point>();
            if (strokeCenters == null || imageSize.IsEmpty)
            {
                return addedCenters;
            }

            foreach (Point center in strokeCenters)
            {
                if (center.X < 0 || center.Y < 0 || center.X >= imageSize.Width || center.Y >= imageSize.Height)
                {
                    continue;
                }

                int key = (center.Y * imageSize.Width) + center.X;
                if (!centerKeys.Add(key))
                {
                    continue;
                }

                centers.Add(center);
                addedCenters.Add(center);
            }

            return addedCenters;
        }

        public IReadOnlyList<Point> DetachCenters()
        {
            List<Point> detached = centers;
            centers = new List<Point>();
            centerKeys.Clear();
            return detached;
        }

        public void Reset()
        {
            centers.Clear();
            centerKeys.Clear();
            Radius = 0;
            Tool = WpfAnnotationTool.Select;
            ClassName = string.Empty;
        }
    }
}
