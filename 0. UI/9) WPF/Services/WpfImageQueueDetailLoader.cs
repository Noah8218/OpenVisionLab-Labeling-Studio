using MvcVisionSystem.Yolo;
using System;
using System.IO;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingSize = System.Drawing.Size;

namespace MvcVisionSystem
{
    public static class WpfImageQueueDetailLoader
    {
        public static WpfImageQueueDetail Build(string imagePath, YoloImageReviewStatusService reviewStatus, CData data)
        {
            if (reviewStatus == null)
            {
                throw new ArgumentNullException(nameof(reviewStatus));
            }

            using DrawingBitmap image = AppImageLoader.LoadBitmap(imagePath);
            return new WpfImageQueueDetail
            {
                ImageSize = image.Size,
                ReviewStatus = reviewStatus.RefreshLabelStatus(imagePath, image.Size, data)
            };
        }

        public static bool TryReadImageSize(string imagePath, out DrawingSize imageSize, out string error)
        {
            imageSize = DrawingSize.Empty;
            error = string.Empty;
            try
            {
                using DrawingBitmap image = AppImageLoader.LoadBitmap(imagePath);
                imageSize = image.Size;
                if (!imageSize.IsEmpty)
                {
                    return true;
                }

                error = $"이미지 크기 확인 실패: {Path.GetFileName(imagePath)}";
                return false;
            }
            catch (Exception ex)
            {
                error = $"이미지 크기 확인 실패: {Path.GetFileName(imagePath)}  {ex.Message}";
                return false;
            }
        }

        public static string FormatImageSize(DrawingSize imageSize)
        {
            return imageSize.IsEmpty ? string.Empty : $"{imageSize.Width}x{imageSize.Height}";
        }
    }
}
