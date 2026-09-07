using System;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using CvMat = OpenCvSharp.Mat;
using CvMatType = OpenCvSharp.MatType;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingImageLockMode = System.Drawing.Imaging.ImageLockMode;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;
using DrawingRectangle = System.Drawing.Rectangle;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the one Bitmap-to-Mat copy contract shared by image decode and display preview paths.
    /// </summary>
    public static class BitmapMatConversionService
    {
        public static CvMat CopyToMat(DrawingBitmap bitmap)
        {
            if (bitmap == null)
            {
                throw new ArgumentNullException(nameof(bitmap));
            }

            var bounds = new DrawingRectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData data = bitmap.LockBits(
                bounds,
                DrawingImageLockMode.ReadOnly,
                DrawingPixelFormat.Format24bppRgb);
            try
            {
                int rowBytes = bitmap.Width * 3;
                int stride = Math.Abs(data.Stride);
                var source = new byte[stride * bitmap.Height];
                Marshal.Copy(data.Scan0, source, 0, source.Length);

                var compact = new byte[rowBytes * bitmap.Height];
                for (int row = 0; row < bitmap.Height; row++)
                {
                    int sourceRow = data.Stride >= 0 ? row : bitmap.Height - 1 - row;
                    Buffer.BlockCopy(source, sourceRow * stride, compact, row * rowBytes, rowBytes);
                }

                var mat = new CvMat(bitmap.Height, bitmap.Width, CvMatType.CV_8UC3);
                Marshal.Copy(compact, 0, mat.Data, compact.Length);
                return mat;
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }
    }

    [Obsolete("Use BitmapMatConversionService.", false)]
    public static class WpfBitmapMatConversionService
    {
        public static CvMat CopyToMat(DrawingBitmap bitmap)
            => BitmapMatConversionService.CopyToMat(bitmap);
    }
}
