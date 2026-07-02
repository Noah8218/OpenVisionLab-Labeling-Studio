using Lib.Common;
using MahApps.Metro.IconPacks;
using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using DrawingSize = System.Drawing.Size;
using MediaBrush = System.Windows.Media.Brush;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Queue detail scanning is intentionally separate from selection so mouse/ROI interactions never wait on file IO.
        private async Task StartImageQueueDetailRefreshAsync(IReadOnlyList<string> imagePaths, CancellationToken token)
        {
            if (imagePaths == null || imagePaths.Count == 0)
            {
                return;
            }

            int loadedCount = 0;
            foreach (string imagePath in imagePaths)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                WpfImageQueueItem item = FindImageQueueItem(imagePath);
                if (item == null)
                {
                    continue;
                }

                try
                {
                    WpfImageQueueDetail detail = await Task.Run(() => BuildImageQueueDetail(imagePath), token).ConfigureAwait(true);
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    ApplyImageQueueDetail(item, detail);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    item.LabelStatus = "확인 실패";
                    item.DetectStatus = "대기";
                    AppendLog($"Image status failed: {Path.GetFileName(imagePath)}  {ex.Message}");
                }

                loadedCount++;
                if (loadedCount == 1 || loadedCount % 20 == 0 || loadedCount == imagePaths.Count)
                {
                    imageQueueView?.Refresh();
                    UpdateImageQueueStatusText(loadedCount, imagePaths.Count);
                }
            }
        }

        private WpfImageQueueDetail BuildImageQueueDetail(string imagePath)
        {
            return WpfImageQueueDetailLoader.Build(imagePath, imageReviewStatus, global.Data);
        }

        private void ApplyImageQueueDetail(WpfImageQueueItem item, WpfImageQueueDetail detail)
        {
            if (item == null || detail == null)
            {
                return;
            }

            item.Dimensions = WpfImageQueueDetailLoader.FormatImageSize(detail.ImageSize);
            ApplyReviewStatusToItem(item, detail.ReviewStatus);
        }

        private void ApplyReviewStatusToItem(WpfImageQueueItem item, YoloImageReviewStatus status)
        {
            if (item == null || status == null)
            {
                return;
            }

            item.LabelStatus = FormatLabelStatusForQueue(status.LabelText);
            item.DetectStatus = FormatDetectionStatusForQueue(status);
            item.IsLabeled = status.IsLabeled;
            item.IsSaveRequired = false;
            item.ReviewState = status.ReviewState;
            item.QueueIconKind = GetQueueIconKind(status);
            item.QueueIconBrush = GetQueueIconBrush(status);
            item.QueueBadgeBackgroundBrush = GetQueueBadgeBackgroundBrush(status);
            item.QueueRowAccentBrush = GetQueueRowAccentBrush(status);
            item.QueueBadgeText = BuildQueueBadgeText(status);
            item.QueueStatusSummary = BuildQueueStatusSummary(status);
            item.Detail = BuildReviewDetailText(status);
            if (IsActiveImageQueueSaveRequired(item))
            {
                ApplySaveRequiredStatusToQueueItem(item, annotationDirtyReason);
            }

            RefreshYoloTrainingStepCompletion();
        }
    }
}
