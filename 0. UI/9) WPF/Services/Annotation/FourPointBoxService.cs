using System;
using System.Collections.Generic;
using System.Drawing;

namespace MvcVisionSystem
{
    public enum WpfFourPointBoxInputResult
    {
        Rejected,
        PointAccepted,
        Completed
    }

    public class FourPointBoxService
    {
        private static readonly string[] RoleNames =
        {
            "\uC704",
            "\uC544\uB798",
            "\uC67C\uCABD",
            "\uC624\uB978\uCABD"
        };

        private readonly List<Point> points = new List<Point>(4);

        public IReadOnlyList<Point> Points => points;

        public bool HasDraft => points.Count > 0;

        public int PointCount => points.Count;

        public string NextRoleName => RoleNames[Math.Min(points.Count, RoleNames.Length - 1)];

        public WpfFourPointBoxInputResult TryAddPoint(
            Point point,
            Size imageSize,
            out Rectangle completedBounds,
            out string message)
        {
            completedBounds = Rectangle.Empty;
            if (!IsInsideImage(point, imageSize))
            {
                message = "\uC774\uBBF8\uC9C0 \uC548\uCABD\uC758 \uADF9\uC810\uC744 \uB204\uB974\uC138\uC694.";
                return WpfFourPointBoxInputResult.Rejected;
            }

            if (points.Count >= 4)
            {
                Reset();
            }

            points.Add(point);
            if (points.Count < 4)
            {
                message = BuildProgressText();
                return WpfFourPointBoxInputResult.PointAccepted;
            }

            if (!TryBuildBounds(points, imageSize, out completedBounds))
            {
                points.RemoveAt(points.Count - 1);
                message = "\uD06C\uAE30\uAC00 0\uC778 \uBC15\uC2A4\uB294 \uB9CC\uB4E4 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4. \uC624\uB978\uCABD \uADF9\uC810\uC744 \uB2E4\uC2DC \uB204\uB974\uC138\uC694.";
                return WpfFourPointBoxInputResult.Rejected;
            }

            Reset();
            message = "\uADF9\uC810 4\uAC1C\uB85C \uBC15\uC2A4\uB97C \uC644\uC131\uD588\uC2B5\uB2C8\uB2E4.";
            return WpfFourPointBoxInputResult.Completed;
        }

        public bool RemoveLastPoint()
        {
            if (points.Count == 0)
            {
                return false;
            }

            points.RemoveAt(points.Count - 1);
            return true;
        }

        public bool Reset()
        {
            if (points.Count == 0)
            {
                return false;
            }

            points.Clear();
            return true;
        }

        public string BuildProgressText()
            => points.Count == 0
                ? "4\uC810 \uADF9\uC810 \u00B7 \uC704 0/4"
                : points.Count >= 4
                    ? "4\uC810 \uADF9\uC810 \u00B7 \uC644\uB8CC 4/4"
                    : $"4\uC810 \uADF9\uC810 \u00B7 {NextRoleName} {points.Count}/4";

        public static bool TryBuildBounds(
            IReadOnlyList<Point> sourcePoints,
            Size imageSize,
            out Rectangle bounds)
        {
            bounds = Rectangle.Empty;
            if (sourcePoints == null
                || sourcePoints.Count != 4
                || imageSize.Width <= 0
                || imageSize.Height <= 0)
            {
                return false;
            }

            foreach (Point point in sourcePoints)
            {
                if (!IsInsideImage(point, imageSize))
                {
                    return false;
                }
            }

            int left = Math.Min(sourcePoints[2].X, sourcePoints[3].X);
            int right = Math.Max(sourcePoints[2].X, sourcePoints[3].X);
            int top = Math.Min(sourcePoints[0].Y, sourcePoints[1].Y);
            int bottom = Math.Max(sourcePoints[0].Y, sourcePoints[1].Y);
            Rectangle clipped = Rectangle.Intersect(
                Rectangle.FromLTRB(left, top, right, bottom),
                new Rectangle(Point.Empty, imageSize));
            if (clipped.Width <= 0 || clipped.Height <= 0)
            {
                return false;
            }

            bounds = clipped;
            return true;
        }

        private static bool IsInsideImage(Point point, Size imageSize)
            => imageSize.Width > 0
                && imageSize.Height > 0
                && point.X >= 0
                && point.Y >= 0
                && point.X < imageSize.Width
                && point.Y < imageSize.Height;
    }

    [Obsolete("Use FourPointBoxService.", false)]
    public sealed class WpfFourPointBoxService : FourPointBoxService
    {
    }
}
