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

        private bool IsActiveImageQueueSaveRequired(WpfImageQueueItem item)
        {
            return item != null
                && !string.IsNullOrWhiteSpace(annotationDirtyReason)
                && !string.IsNullOrWhiteSpace(activeImagePath)
                && string.Equals(item.ImagePath, activeImagePath, StringComparison.OrdinalIgnoreCase);
        }

        private void ApplyActiveImageQueueSaveRequiredStatus(string reason)
        {
            ApplySaveRequiredStatusToQueueItem(FindImageQueueItem(activeImagePath), reason);
            imageQueueView?.Refresh();
            UpdateImageQueueStatusText();
        }

        private static void ApplySaveRequiredStatusToQueueItem(WpfImageQueueItem item, string reason)
        {
            if (item == null)
            {
                return;
            }

            string displayReason = string.IsNullOrWhiteSpace(reason) ? "\uB77C\uBCA8 \uD3B8\uC9D1" : reason.Trim();
            item.IsSaveRequired = true;
            item.LabelStatus = "\uC800\uC7A5 \uD544\uC694";
            item.QueueIconKind = PackIconMaterialKind.AlertCircleOutline;
            item.QueueIconBrush = WpfImageQueueItem.WarningBrush;
            item.QueueBadgeBackgroundBrush = WpfImageQueueItem.WarningBadgeBrush;
            item.QueueRowAccentBrush = WpfImageQueueItem.WarningBrush;
            item.QueueBadgeText = "\uC800\uC7A5 \uD544\uC694";
            item.QueueStatusSummary = $"\uB77C\uBCA8 \uC800\uC7A5 \uD544\uC694: {displayReason}";
            item.Detail = $"{item.FileName}{Environment.NewLine}\uD30C\uC77C\uC5D0 \uBC18\uC601\uD558\uB824\uBA74 \uB77C\uBCA8 \uC800\uC7A5\uC744 \uB20C\uB7EC\uC57C \uD569\uB2C8\uB2E4.{Environment.NewLine}{displayReason}";
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

        private bool ApplyActiveAnomalyClassification(IReadOnlyList<YoloWorkerSmokeCandidate> candidates)
        {
            return ApplyAnomalyClassificationToImage(
                activeImagePath,
                Path.GetFileNameWithoutExtension(activeImagePath),
                candidates,
                saveReviewStatus: true);
        }

        private bool ApplyAnomalyClassificationToImage(
            string imagePath,
            string imageName,
            IReadOnlyList<YoloWorkerSmokeCandidate> candidates,
            bool saveReviewStatus)
        {
            if (!IsAnomalyDatasetPurpose() || string.IsNullOrWhiteSpace(imagePath))
            {
                return false;
            }

            AnomalyClassificationDecision decision = AnomalyClassificationDecisionService.Build(
                candidates,
                global.Data.ProjectSettings.AnomalyClassification.ToDecisionOptions());
            if (!decision.IsMapped)
            {
                return false;
            }

            MarkAnomalyImageReviewState(imagePath, imageName, decision.ReviewState, saveReviewStatus);
            return true;
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
            MarkActiveAnomalyImageAbnormal();
            imageQueueView?.Refresh();
            UpdateImageQueueStatusText();
        }

        private void MarkActiveImageNoCandidate()
        {
            if (string.IsNullOrWhiteSpace(activeImagePath))
            {
                return;
            }

            string imageName = Path.GetFileNameWithoutExtension(activeImagePath);
            YoloImageReviewStatus status = imageReviewStatus.SetDetectionNoCandidates(activeImagePath, imageName);
            if (!activeImageSize.IsEmpty)
            {
                status = imageReviewStatus.RefreshLabelStatusAndReviewState(activeImagePath, activeImageSize, global.Data, hasActiveCandidates: false) ?? status;
            }

            ApplyReviewStatusToItem(FindImageQueueItem(activeImagePath), status);
            imageReviewStatus.SaveReviewStatus(global.Data);
            MarkActiveAnomalyImageNormal();
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

        private bool IsAnomalyDatasetPurpose()
        {
            EnsureProjectSettings();
            return global.Data?.ProjectSettings?.DatasetPurpose == LabelingDatasetPurpose.AnomalyDetection;
        }

        private void MarkActiveAnomalyImageNormal()
        {
            MarkActiveAnomalyImageReviewState(AnomalyImageReviewState.Normal);
        }

        private void MarkActiveAnomalyImageAbnormal()
        {
            MarkActiveAnomalyImageReviewState(AnomalyImageReviewState.Abnormal);
        }

        private void MarkActiveAnomalyImageReviewState(AnomalyImageReviewState state)
        {
            if (!IsAnomalyDatasetPurpose() || string.IsNullOrWhiteSpace(activeImagePath))
            {
                return;
            }

            string imageName = Path.GetFileNameWithoutExtension(activeImagePath);
            MarkAnomalyImageReviewState(activeImagePath, imageName, state, saveReviewStatus: true);
        }

        private void MarkAnomalyImageReviewState(string imagePath, string imageName, AnomalyImageReviewState state, bool saveReviewStatus)
        {
            if (!IsAnomalyDatasetPurpose() || string.IsNullOrWhiteSpace(imagePath))
            {
                return;
            }

            if (state == AnomalyImageReviewState.Normal)
            {
                anomalyImageReviewStatus.MarkNormal(imagePath, imageName);
            }
            else if (state == AnomalyImageReviewState.Abnormal)
            {
                anomalyImageReviewStatus.MarkAbnormal(imagePath, imageName);
            }
            else
            {
                anomalyImageReviewStatus.ClearReviewState(imagePath, imageName);
            }

            if (saveReviewStatus)
            {
                SaveAnomalyImageReviewStatus();
            }
        }

        private void SaveAnomalyImageReviewStatus()
        {
            anomalyImageReviewStatus.SaveReviewStatus(global.Data);
            string recipeName = GetCurrentRecipeName();
            if (!string.IsNullOrWhiteSpace(recipeName))
            {
                LabelingDatasetManifestService.Save(global.Data, recipeName);
            }
        }
    }
}
