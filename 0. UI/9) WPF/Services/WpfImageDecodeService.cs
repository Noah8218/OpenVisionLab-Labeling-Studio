using MvcVisionSystem.Yolo;
using System;
using System.Runtime.InteropServices;
using CvMat = OpenCvSharp.Mat;
using CvMatType = OpenCvSharp.MatType;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingImageLockMode = System.Drawing.Imaging.ImageLockMode;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;
using DrawingRectangle = System.Drawing.Rectangle;

namespace MvcVisionSystem
{
    public sealed class WpfImageDecodeService
    {
        public WpfCachedDecodedImage DecodeForCanvas(string imagePath)
            => DecodeCore(imagePath, long.MaxValue);

        public WpfCachedDecodedImage TryDecodeForCache(string imagePath)
            => TryDecodeForCache(imagePath, WpfImageDecodeCacheService.DefaultMaxPixels);

        public WpfCachedDecodedImage TryDecodeForCache(string imagePath, long maxPixels)
        {
            try
            {
                return DecodeCore(imagePath, Math.Max(1L, maxPixels));
            }
            catch
            {
                return null;
            }
        }

        private static WpfCachedDecodedImage DecodeCore(string imagePath, long maxPixels)
        {
            DrawingBitmap workspaceBitmap = null;
            CvMat imageMat = null;
            try
            {
                using DrawingBitmap loaded = AppImageLoader.LoadBitmap(imagePath);
                if ((long)loaded.Width * loaded.Height > maxPixels)
                {
                    return null;
                }

                // Decode ownership lives here so Shell and preload paths cannot drift on clone format or Mat lifetime rules.
                workspaceBitmap = loaded.Clone(
                    new DrawingRectangle(0, 0, loaded.Width, loaded.Height),
                    DrawingPixelFormat.Format24bppRgb);
                imageMat = CopyBitmapToMat(workspaceBitmap);
                return new WpfCachedDecodedImage(imagePath, workspaceBitmap, imageMat);
            }
            catch
            {
                workspaceBitmap?.Dispose();
                imageMat?.Dispose();
                throw;
            }
        }

        private static CvMat CopyBitmapToMat(DrawingBitmap bitmap)
        {
            if (bitmap == null)
            {
                throw new ArgumentNullException(nameof(bitmap));
            }

            var bounds = new DrawingRectangle(0, 0, bitmap.Width, bitmap.Height);
            System.Drawing.Imaging.BitmapData data = bitmap.LockBits(bounds, DrawingImageLockMode.ReadOnly, DrawingPixelFormat.Format24bppRgb);
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
}