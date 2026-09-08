using MvcVisionSystem.Yolo;
using System;
using System.IO;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingSize = System.Drawing.Size;

namespace MvcVisionSystem
{
    public static class ImageQueueDetailLoader
    {
        public static WpfImageQueueDetail Build(
            string imagePath,
            ImageQualityReviewWorkflowService reviewWorkflow,
            LabelingProjectData data)
        {
            if (reviewWorkflow == null)
            {
                throw new ArgumentNullException(nameof(reviewWorkflow));
            }

            Func<string, DrawingSize, YoloImageReviewStatus> refresh = reviewWorkflow.CaptureLabelStatusRefresh(data);
            return Build(imagePath, size => refresh(imagePath, size));
        }

        internal static WpfImageQueueDetail Build(string imagePath, Func<DrawingSize, YoloImageReviewStatus> refreshLabelStatus)
        {
            using DrawingBitmap image = AppImageLoader.LoadBitmap(imagePath);
            return new WpfImageQueueDetail
            {
                ImageSize = image.Size,
                ReviewStatus = refreshLabelStatus(image.Size)
            };
        }

        public static WpfImageQueueDetail Build(
            string imagePath,
            YoloImageReviewStatusService reviewStatus,
            LabelingProjectData data)
        {
            return Build(
                imagePath,
                new ImageQualityReviewWorkflowService(reviewStatus),
                data);
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

    [Obsolete("Use ImageQueueDetailLoader.", false)]
    public static class WpfImageQueueDetailLoader
    {
        public static WpfImageQueueDetail Build(
            string imagePath,
            ImageQualityReviewWorkflowService reviewWorkflow,
            LabelingProjectData data)
            => ImageQueueDetailLoader.Build(imagePath, reviewWorkflow, data);

        public static WpfImageQueueDetail Build(
            string imagePath,
            YoloImageReviewStatusService reviewStatus,
            LabelingProjectData data)
            => ImageQueueDetailLoader.Build(imagePath, reviewStatus, data);

        public static bool TryReadImageSize(string imagePath, out DrawingSize imageSize, out string error)
            => ImageQueueDetailLoader.TryReadImageSize(imagePath, out imageSize, out error);

        public static string FormatImageSize(DrawingSize imageSize)
            => ImageQueueDetailLoader.FormatImageSize(imageSize);
    }
}
