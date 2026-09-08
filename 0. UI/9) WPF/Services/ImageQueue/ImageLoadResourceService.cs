using System;
using CvMat = OpenCvSharp.Mat;
using DrawingBitmap = System.Drawing.Bitmap;
using MvcVisionSystem._1._Core;

namespace MvcVisionSystem
{
    public sealed class ImageLoadResourceService : IDisposable
    {
        private DrawingBitmap activeBitmap;

        public bool HasActiveBitmap => activeBitmap != null;

        public DrawingBitmap ReplaceActiveBitmap(DrawingBitmap legacyActiveBitmap, DrawingBitmap nextBitmap)
        {
            DisposeDistinct(activeBitmap, legacyActiveBitmap, nextBitmap);
            activeBitmap = nextBitmap;
            return activeBitmap;
        }

        public void SetDisplayImage(CvMat imageMat)
        {
            DisplayManager.ImageSrc = imageMat;
        }

        public void Clear(DrawingBitmap legacyActiveBitmap = null)
        {
            DisposeDistinct(activeBitmap, legacyActiveBitmap, keep: null);
            activeBitmap = null;
            DisplayManager.ImageSrc = null;
        }

        public void Dispose()
        {
            Clear();
        }

        private static void DisposeDistinct(DrawingBitmap first, DrawingBitmap second, DrawingBitmap keep)
        {
            if (first != null && !ReferenceEquals(first, keep))
            {
                first.Dispose();
            }

            if (second != null
                && !ReferenceEquals(second, keep)
                && !ReferenceEquals(second, first))
            {
                second.Dispose();
            }
        }
    }
}
