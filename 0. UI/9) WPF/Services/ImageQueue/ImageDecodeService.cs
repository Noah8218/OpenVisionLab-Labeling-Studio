using MvcVisionSystem.Yolo;
using System;
using CvMat = OpenCvSharp.Mat;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingPixelFormat = System.Drawing.Imaging.PixelFormat;
using DrawingRectangle = System.Drawing.Rectangle;

namespace MvcVisionSystem
{
    public class ImageDecodeService
    {
        public CachedDecodedImage DecodeForCanvas(string imagePath)
            => DecodeCore(imagePath, long.MaxValue);

        public CachedDecodedImage TryDecodeForCache(string imagePath)
            => TryDecodeForCache(imagePath, ImageDecodeCacheService.DefaultMaxPixels);

        public CachedDecodedImage TryDecodeForCache(string imagePath, long maxPixels)
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

        private static CachedDecodedImage DecodeCore(string imagePath, long maxPixels)
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
                imageMat = BitmapMatConversionService.CopyToMat(workspaceBitmap);
                return new CachedDecodedImage(imagePath, workspaceBitmap, imageMat);
            }
            catch
            {
                workspaceBitmap?.Dispose();
                imageMat?.Dispose();
                throw;
            }
    }

    [Obsolete("Use ImageDecodeService.", false)]
    public sealed class WpfImageDecodeService : ImageDecodeService
    {
    }

}
}
