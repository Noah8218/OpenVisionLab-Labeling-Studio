using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace MvcVisionSystem
{
    public static class CDrawBitmap
    {
        public static Bitmap GetBitmapFormat24bppRgb(Bitmap bitmap)
        {
            return bitmap.Clone(new Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.PixelFormat.Format24bppRgb);
        }

        public static void ToHighQuality(this Graphics graphics)
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }

        public static void ToLowQuality(this Graphics graphics)
        {
            graphics.InterpolationMode = InterpolationMode.Low;
            graphics.CompositingQuality = CompositingQuality.HighSpeed;
            graphics.SmoothingMode = SmoothingMode.HighSpeed;
            graphics.TextRenderingHint = TextRenderingHint.SystemDefault;
            graphics.PixelOffsetMode = PixelOffsetMode.HighSpeed;
        }
    }
}
