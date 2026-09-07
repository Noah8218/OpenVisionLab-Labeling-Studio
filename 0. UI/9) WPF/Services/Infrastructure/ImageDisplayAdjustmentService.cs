using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingRectangle = System.Drawing.Rectangle;

namespace MvcVisionSystem
{
    public class ImageDisplayAdjustmentOptions
    {
        public int Brightness { get; init; }
        public double Contrast { get; init; } = 1D;
        public double Gamma { get; init; } = 1D;
        public bool Invert { get; init; }
        public bool EqualizeHistogram { get; init; }

        public bool IsDefault
            => Brightness == 0
                && Math.Abs(Contrast - 1D) < 0.0001D
                && Math.Abs(Gamma - 1D) < 0.0001D
                && !Invert
                && !EqualizeHistogram;

        public ImageDisplayAdjustmentOptions Normalize()
            => new ImageDisplayAdjustmentOptions
            {
                Brightness = Math.Clamp(Brightness, -100, 100),
                Contrast = Math.Clamp(Contrast, 0.5D, 2D),
                Gamma = Math.Clamp(Gamma, 0.2D, 3D),
                Invert = Invert,
                EqualizeHistogram = EqualizeHistogram
            };
    }

    public class ImageDisplayAdjustmentService
    {
        public DrawingBitmap CreateAdjustedCopy(
            DrawingBitmap source,
            ImageDisplayAdjustmentOptions options)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            ImageDisplayAdjustmentOptions normalized =
                (options ?? new ImageDisplayAdjustmentOptions()).Normalize();
            var output = new DrawingBitmap(source.Width, source.Height, PixelFormat.Format24bppRgb);
            using (Graphics graphics = Graphics.FromImage(output))
            {
                graphics.DrawImageUnscaled(source, 0, 0);
            }

            if (normalized.IsDefault)
            {
                return output;
            }

            ApplyInPlace(output, normalized);
            return output;
        }

        private static void ApplyInPlace(
            DrawingBitmap bitmap,
            ImageDisplayAdjustmentOptions options)
        {
            var bounds = new DrawingRectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData data = bitmap.LockBits(bounds, ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
            try
            {
                int stride = Math.Abs(data.Stride);
                var pixels = new byte[stride * bitmap.Height];
                Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                byte[] lookup = BuildLookup(options);
                int[] histogram = options.EqualizeHistogram ? new int[256] : null;

                for (int row = 0; row < bitmap.Height; row++)
                {
                    int dataRow = data.Stride >= 0 ? row : bitmap.Height - 1 - row;
                    int rowOffset = dataRow * stride;
                    for (int column = 0; column < bitmap.Width; column++)
                    {
                        int offset = rowOffset + column * 3;
                        pixels[offset] = lookup[pixels[offset]];
                        pixels[offset + 1] = lookup[pixels[offset + 1]];
                        pixels[offset + 2] = lookup[pixels[offset + 2]];
                        if (histogram != null)
                        {
                            histogram[CalculateLuminance(
                                pixels[offset + 2],
                                pixels[offset + 1],
                                pixels[offset])]++;
                        }
                    }
                }

                if (histogram != null)
                {
                    ApplyHistogramEqualization(
                        pixels,
                        stride,
                        data.Stride >= 0,
                        bitmap.Width,
                        bitmap.Height,
                        histogram);
                }

                Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        private static byte[] BuildLookup(ImageDisplayAdjustmentOptions options)
        {
            var lookup = new byte[256];
            double inverseGamma = 1D / options.Gamma;
            for (int value = 0; value < lookup.Length; value++)
            {
                double adjusted = ((value - 127.5D) * options.Contrast)
                    + 127.5D
                    + options.Brightness;
                adjusted = Math.Clamp(adjusted, 0D, 255D);
                adjusted = 255D * Math.Pow(adjusted / 255D, inverseGamma);
                if (options.Invert)
                {
                    adjusted = 255D - adjusted;
                }

                lookup[value] = (byte)Math.Clamp((int)Math.Round(adjusted), 0, 255);
            }

            return lookup;
        }

        private static void ApplyHistogramEqualization(
            byte[] pixels,
            int stride,
            bool topDown,
            int width,
            int height,
            int[] histogram)
        {
            int total = width * height;
            int cumulative = 0;
            int firstNonZeroCumulative = 0;
            var mapping = new byte[256];
            for (int luminance = 0; luminance < histogram.Length; luminance++)
            {
                cumulative += histogram[luminance];
                if (firstNonZeroCumulative == 0 && histogram[luminance] > 0)
                {
                    firstNonZeroCumulative = cumulative;
                }

                int denominator = total - firstNonZeroCumulative;
                mapping[luminance] = denominator <= 0
                    ? (byte)luminance
                    : (byte)Math.Clamp(
                        (int)Math.Round((cumulative - firstNonZeroCumulative) * 255D / denominator),
                        0,
                        255);
            }

            for (int row = 0; row < height; row++)
            {
                int dataRow = topDown ? row : height - 1 - row;
                int rowOffset = dataRow * stride;
                for (int column = 0; column < width; column++)
                {
                    int offset = rowOffset + column * 3;
                    int luminance = CalculateLuminance(
                        pixels[offset + 2],
                        pixels[offset + 1],
                        pixels[offset]);
                    int delta = mapping[luminance] - luminance;
                    pixels[offset] = ClampToByte(pixels[offset] + delta);
                    pixels[offset + 1] = ClampToByte(pixels[offset + 1] + delta);
                    pixels[offset + 2] = ClampToByte(pixels[offset + 2] + delta);
                }
            }
        }

        private static int CalculateLuminance(int red, int green, int blue)
            => (77 * red + 150 * green + 29 * blue) >> 8;

        private static byte ClampToByte(int value)
            => (byte)Math.Clamp(value, 0, 255);
    }

    [Obsolete("Use ImageDisplayAdjustmentService.", false)]
    public sealed class WpfImageDisplayAdjustmentService : ImageDisplayAdjustmentService
    {
    }

    [Obsolete("Use ImageDisplayAdjustmentOptions.", false)]
    public sealed class WpfImageDisplayAdjustmentOptions : ImageDisplayAdjustmentOptions
    {
    }
}
