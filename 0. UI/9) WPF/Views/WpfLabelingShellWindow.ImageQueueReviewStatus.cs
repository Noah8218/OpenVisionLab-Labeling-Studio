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
        // Review-state persistence runs outside the immediate delete/selection hot path and marshals only the latest result back to WPF.
        private void RefreshActiveImageQueueStatus(bool hasActiveCandidates)
        {
            if (string.IsNullOrWhiteSpace(activeImagePath) || activeImageSize.IsEmpty)
            {
                return;
            }

            WpfImageQueueItem item = FindImageQueueItem(activeImagePath);
            YoloImageReviewStatus status = RefreshActiveImageQueueStatusCore(
                activeImagePath,
                activeImageSize,
                global.Data,
                hasActiveCandidates);
            ApplyReviewStatusToItem(item, status);
            imageQueueView?.Refresh();
            UpdateImageQueueStatusText();
        }

        private void QueueActiveImageQueueStatusRefresh(bool hasActiveCandidates)
        {
            if (string.IsNullOrWhiteSpace(activeImagePath) || activeImageSize.IsEmpty)
            {
                return;
            }

            string imagePath = activeImagePath;
            DrawingSize imageSize = activeImageSize;
            CData data = global.Data;
            int refreshVersion = Interlocked.Increment(ref queuedActiveImageQueueStatusRefreshVersion);

            // Delete must feel immediate. Label-file recount and review-state JSON writes are
            // background bookkeeping; only the latest completed result returns to the UI thread.
            Task.Run(() => RefreshActiveImageQueueStatusCore(
                    imagePath,
                    imageSize,
                    data,
                    hasActiveCandidates))
                .ContinueWith(
                    task => ApplyQueuedActiveImageQueueStatusRefresh(refreshVersion, imagePath, task),
                    TaskScheduler.Default);
        }

        private YoloImageReviewStatus RefreshActiveImageQueueStatusCore(
            string imagePath,
            DrawingSize imageSize,
            CData data,
            bool hasActiveCandidates)
        {
            YoloImageReviewStatus status = imageReviewStatus.RefreshLabelStatusAndReviewState(
                imagePath,
                imageSize,
                data,
                hasActiveCandidates);
            imageReviewStatus.SaveReviewStatus(data);
            return status;
        }

        private void ApplyQueuedActiveImageQueueStatusRefresh(
            int refreshVersion,
            string imagePath,
            Task<YoloImageReviewStatus> refreshTask)
        {
            try
            {
                Dispatcher.BeginInvoke(
                    new Action(() =>
                    {
                        if (refreshVersion != Volatile.Read(ref queuedActiveImageQueueStatusRefreshVersion)
                            || !string.Equals(activeImagePath, imagePath, StringComparison.OrdinalIgnoreCase)
                            || refreshTask.IsCanceled)
                        {
                            return;
                        }

                        if (refreshTask.IsFaulted)
                        {
                            AppendLog($"Image queue status refresh failed after delete: {refreshTask.Exception?.GetBaseException().Message}");
                            return;
                        }

                        ApplyReviewStatusToItem(FindImageQueueItem(imagePath), refreshTask.Result);
                        imageQueueView?.Refresh();
                        UpdateImageQueueStatusText();
                    }),
                    DispatcherPriority.Background);
            }
            catch (InvalidOperationException)
            {
                // The shell can close while a queued delete-status refresh is finishing.
            }
            catch (TaskCanceledException)
            {
            }
        }

        private void SetActiveImageDetectionStatus(int candidateCount, bool succeeded)
        {
            if (string.IsNullOrWhiteSpace(activeImagePath))
            {
                return;
            }

            string imageName = Path.GetFileNameWithoutExtension(activeImagePath);
            YoloImageReviewStatus status = succeeded
                ? candidateCount > 0
                    ? imageReviewStatus.SetDetectionCandidates(activeImagePath, imageName, candidateCount)
                    : imageReviewStatus.SetDetectionNoCandidates(activeImagePath, imageName)
                : imageReviewStatus.SetDetectionFailed(activeImagePath, imageName, "Detection failed.");
            ApplyReviewStatusToItem(FindImageQueueItem(activeImagePath), status);
            imageReviewStatus.SaveReviewStatus(global.Data);
            imageQueueView?.Refresh();
            UpdateImageQueueStatusText();
        }

        private void MarkActiveImageConfirmed()
        {
            if (string.IsNullOrWhiteSpace(activeImagePath))
            {
                return;
            }

            YoloImageReviewStatus status = imageReviewStatus.MarkConfirmed(activeImagePath, Path.GetFileNameWithoutExtension(activeImagePath));
            if (!activeImageSize.IsEmpty)
            {
                status = imageReviewStatus.RefreshLabelStatusAndReviewState(activeImagePath, activeImageSize, global.Data, hasActiveCandidates: false) ?? status;
            }

            ApplyReviewStatusToItem(FindImageQueueItem(activeImagePath), status);
            imageReviewStatus.SaveReviewStatus(global.Data);
            imageQueueView?.Refresh();
            UpdateImageQueueStatusText();
        }

        private void MarkActiveImageSkippedOrCandidate()
        {
            if (string.IsNullOrWhiteSpace(activeImagePath))
            {
                return;
            }

            string imageName = Path.GetFileNameWithoutExtension(activeImagePath);
            YoloImageReviewStatus status = pendingDetectionCandidates.Count > 0
                ? imageReviewStatus.SetDetectionCandidates(activeImagePath, imageName, pendingDetectionCandidates.Count)
                : imageReviewStatus.MarkSkipped(activeImagePath, imageName);
            ApplyReviewStatusToItem(FindImageQueueItem(activeImagePath), status);
            imageReviewStatus.SaveReviewStatus(global.Data);
            imageQueueView?.Refresh();
            UpdateImageQueueStatusText();
        }
    }
}
