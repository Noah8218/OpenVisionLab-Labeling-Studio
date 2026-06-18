using Lib.Common;
using MvcVisionSystem._1._Core;
using OpenVisionLab.ImageCanvas;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using CvMat = OpenCvSharp.Mat;

namespace MvcVisionSystem
{
    public partial class CViewer
    {
        public void SetDisplayImage(Bitmap image, string imageName = "", string imagePath = "", bool resetAnnotations = false, bool zoomToFit = true)
        {
            if (resetAnnotations)
            {
                ResetAnnotations();
            }

            SetCurrentImage(image, zoomToFit);

            if (!string.IsNullOrWhiteSpace(imageName) || !string.IsNullOrWhiteSpace(imagePath))
            {
                CGlobal.Inst.ImageWorkspace.SetActiveImage(imageName ?? string.Empty, imagePath ?? string.Empty, _currentImage);
            }
        }

        public void LoadMainImage(Bitmap image, string imageName, string imagePath = "")
        {
            ResetAnnotations();
            CGlobal.Inst.Data.LastSelectImageName = imageName ?? string.Empty;
            CGlobal.Inst.Data.LastSelectImagePath = imagePath ?? string.Empty;
            SetCurrentImage(image, true);
            CGlobal.Inst.ImageWorkspace.SetActiveImage(imageName ?? string.Empty, imagePath ?? string.Empty, _currentImage);
            CDisplayManager.ImageSrc = BitmapImageConverter.ToMat(_currentImage);
        }

        private void SetCurrentImage(Bitmap image, bool zoomToFit)
        {
            ClearRasterMaskTextureCache();
            Image oldImage = _currentImage;
            _currentImage = CreateCanvasBitmap(image);
            _ImageChanged = true;

            if (oldImage != null && !ReferenceEquals(oldImage, _currentImage))
            {
                oldImage.Dispose();
            }

            if (_currentImage == null)
            {
                _imageSize = Size.Empty;
                _pendingImageLoad = false;
                Canvas?.ClearTexture();
                Canvas?.RefreshGL();
                return;
            }

            if (!CanLoadTexture())
            {
                _pendingImageLoad = true;
                _pendingZoomToFit = zoomToFit;
                return;
            }

            LoadCurrentImage(zoomToFit);
        }

        private bool CanLoadTexture()
        {
            return Canvas != null && Canvas.IsHandleCreated && !Canvas.IsDisposed;
        }

        private void LoadPendingImage()
        {
            if (!_pendingImageLoad || _currentImage == null || !CanLoadTexture())
            {
                return;
            }

            LoadCurrentImage(_pendingZoomToFit);
        }

        private void LoadCurrentImage(bool zoomToFit)
        {
            if (_currentImage == null || Canvas == null)
            {
                return;
            }

            try
            {
                _pendingImageLoad = false;

                using CvMat mat = BitmapImageConverter.ToMat(_currentImage);
                CanvasImageLoader.UploadMatAsTexture(Canvas, mat, TextureName, ref _imageSize, zoomToFit);
                if (!zoomToFit)
                {
                    Canvas.RefreshGL();
                }
            }
            catch (Exception)
            {
                _pendingImageLoad = true;
                _pendingZoomToFit = zoomToFit;
            }
        }

        private Bitmap CreateCanvasBitmap(Bitmap source)
        {
            if (source == null)
            {
                return null;
            }

            return source.Clone(new Rectangle(0, 0, source.Width, source.Height), PixelFormat.Format24bppRgb);
        }
    }
}
