using System;
using System.Collections.Generic;
using System.Drawing;

namespace MvcVisionSystem
{
    public static class OpenGlImageGeometry
    {
        public static Point RobotToImage(Point robotPoint, Size imageSize)
        {
            if (imageSize.Width <= 0 || imageSize.Height <= 0)
            {
                return Point.Empty;
            }

            int x = Clamp(robotPoint.X, 0, imageSize.Width - 1);
            int y = Clamp(imageSize.Height - robotPoint.Y, 0, imageSize.Height - 1);
            return new Point(x, y);
        }

        public static RectangleF ToOpenGlRectangle(Rectangle imageRectangle, int imageHeight)
        {
            return new RectangleF(
                imageRectangle.Left,
                imageHeight - imageRectangle.Bottom,
                imageRectangle.Width,
                imageRectangle.Height);
        }

        public static PointF ToOpenGlPoint(Point imagePoint, int imageHeight)
        {
            return new PointF(imagePoint.X, imageHeight - imagePoint.Y);
        }

        public static IEnumerable<Point> GetHandlePoints(Rectangle roi)
        {
            yield return new Point(roi.Left, roi.Top);
            yield return new Point(roi.Left + roi.Width / 2, roi.Top);
            yield return new Point(roi.Right, roi.Top);
            yield return new Point(roi.Right, roi.Top + roi.Height / 2);
            yield return new Point(roi.Right, roi.Bottom);
            yield return new Point(roi.Left + roi.Width / 2, roi.Bottom);
            yield return new Point(roi.Left, roi.Bottom);
            yield return new Point(roi.Left, roi.Top + roi.Height / 2);
        }

        public static int GetHandleSize(float zoomScale, int baseSize = 12, int minSize = 2)
        {
            return Math.Max(minSize, (int)Math.Round(baseSize * zoomScale));
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) { return min; }
            if (value > max) { return max; }
            return value;
        }
    }
}
