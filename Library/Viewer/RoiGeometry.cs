using System.Drawing;

namespace MvcVisionSystem
{
    public static class RoiGeometry
    {
        public static Rectangle CreateBoundedRectangle(Point start, Point end, Rectangle bounds)
        {
            int left = Clamp(System.Math.Min(start.X, end.X), bounds.Left, bounds.Right);
            int top = Clamp(System.Math.Min(start.Y, end.Y), bounds.Top, bounds.Bottom);
            int right = Clamp(System.Math.Max(start.X, end.X), bounds.Left, bounds.Right);
            int bottom = Clamp(System.Math.Max(start.Y, end.Y), bounds.Top, bounds.Bottom);

            if (right < left || bottom < top)
            {
                return Rectangle.Empty;
            }

            return Rectangle.FromLTRB(left, top, right, bottom);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) { return min; }
            if (value > max) { return max; }
            return value;
        }
    }
}
