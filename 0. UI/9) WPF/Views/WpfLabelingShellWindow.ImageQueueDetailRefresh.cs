using Lib.Common;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using DrawingSize = System.Drawing.Size;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        private const int ImageQueueDetailBatchSize = 64;
        private const int ImageQueueDetailParallelism = 4;

        // Queue detail scanning stays off the dispatcher. Only a bounded set of changed rows is applied at background priority.
        private async Task StartImageQueueDetailRefreshAsync(
            IReadOnlyList<string> imagePaths,
            IReadOnlyDictionary<string, WpfImageQueueItem> itemLookup,
            YoloImageReviewStatusService reviewStatus,
            CData data,
            CancellationToken token)
        {
            if (imagePaths == null || imagePaths.Count == 0 || itemLookup == null || reviewStatus == null)
            {
                return;
            }

            int loadedCount = 0;
            var pendingResults = new List<ImageQueueDetailLoadResult>(ImageQueueDetailBatchSize);
            try
            {
                for (int startIndex = 0; startIndex < imagePaths.Count; startIndex += ImageQueueDetailParallelism)
                {
                    token.ThrowIfCancellationRequested();
                    int endIndex = Math.Min(imagePaths.Count, startIndex + ImageQueueDetailParallelism);
                    var detailTasks = new List<Task<ImageQueueDetailLoadResult>>(endIndex - startIndex);
                    for (int index = startIndex; index < endIndex; index++)
                    {
                        string imagePath = imagePaths[index];
                        if (!itemLookup.TryGetValue(imagePath, out WpfImageQueueItem item) || item == null)
                        {
                            continue;
                        }

                        detailTasks.Add(BuildImageQueueDetailResultAsync(imagePath, item, reviewStatus, data, token));
                    }

                    if (detailTasks.Count > 0)
                    {
                        ImageQueueDetailLoadResult[] results = await Task.WhenAll(detailTasks).ConfigureAwait(false);
                        pendingResults.AddRange(results);
                    }

                    loadedCount = endIndex;
                    if (pendingResults.Count >= ImageQueueDetailBatchSize || loadedCount == imagePaths.Count)
                    {
                        await ApplyImageQueueDetailBatchAsync(
                            pendingResults,
                            loadedCount,
                            imagePaths.Count,
                            token).ConfigureAwait(false);
                    }
                }

                if (pendingResults.Count > 0)
                {
                    await ApplyImageQueueDetailBatchAsync(
                        pendingResults,
                        loadedCount,
                        imagePaths.Count,
                        token).ConfigureAwait(false);
                }

                token.ThrowIfCancellationRequested();
                await Dispatcher.InvokeAsync(
                    () => CompleteImageQueueDetailRefresh(token),
                    DispatcherPriority.Background,
                    token).Task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // A new root or window close owns the newer queue state.
            }
        }

        private Task<ImageQueueDetailLoadResult> BuildImageQueueDetailResultAsync(
            string imagePath,
            WpfImageQueueItem item,
            YoloImageReviewStatusService reviewStatus,
            CData data,
            CancellationToken token)
        {
            return Task.Run(() =>
            {
                try
                {
                    return ImageQueueDetailLoadResult.Success(item, BuildImageQueueDetail(imagePath, reviewStatus, data));
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    return ImageQueueDetailLoadResult.Failure(item, ex);
                }
            }, token);
        }

        private async Task ApplyImageQueueDetailBatchAsync(
            List<ImageQueueDetailLoadResult> pendingResults,
            int loadedCount,
            int totalCount,
            CancellationToken token)
        {
            if (pendingResults == null || pendingResults.Count == 0)
            {
                return;
            }

            ImageQueueDetailLoadResult[] results = pendingResults.ToArray();
            pendingResults.Clear();
            await Dispatcher.InvokeAsync(
                () => ApplyImageQueueDetailBatch(results, loadedCount, totalCount, token),
                DispatcherPriority.Background,
                token).Task.ConfigureAwait(false);
        }

        private void ApplyImageQueueDetailBatch(
            IReadOnlyList<ImageQueueDetailLoadResult> results,
            int loadedCount,
            int totalCount,
            CancellationToken token)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            foreach (ImageQueueDetailLoadResult result in results ?? Array.Empty<ImageQueueDetailLoadResult>())
            {
                if (result?.Item == null)
                {
                    continue;
                }

                if (result.Error != null)
                {
                    result.Item.LabelStatus = "\uC0C1\uD0DC \uD655\uC778 \uC2E4\uD328";
                    result.Item.DetectStatus = "\uB300\uAE30";
                    AppendLog($"Image status failed: {Path.GetFileName(result.Item.ImagePath)}  {result.Error.Message}");
                    continue;
                }

                ApplyImageQueueDetail(result.Item, result.Detail);
            }

            UpdateImageQueueDetailProgress(loadedCount, totalCount);
        }

        private void CompleteImageQueueDetailRefresh(CancellationToken token)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            // One final full view refresh makes the active filter exact without re-evaluating all rows for every detail batch.
            imageQueueView?.Refresh();
            UpdateImageQueueStatusText();
            RefreshYoloTrainingStepCompletion();
        }

        private void UpdateImageQueueDetailProgress(int loadedCount, int totalCount)
        {
            int total = Math.Max(0, totalCount);
            int loaded = Math.Min(Math.Max(0, loadedCount), total);
            string activeText = string.IsNullOrWhiteSpace(activeImagePath)
                ? string.Empty
                : $" / \uD604\uC7AC {Path.GetFileName(activeImagePath)}";
            SetDatasetStatus($"\uB370\uC774\uD130\uC14B: {imageQueueItems.Count}/{total} \uC774\uBBF8\uC9C0 / \uC0C1\uD0DC \uD655\uC778 {loaded}/{total}{activeText}");
        }

        private WpfImageQueueDetail BuildImageQueueDetail(
            string imagePath,
            YoloImageReviewStatusService reviewStatus,
            CData data)
        {
            return WpfImageQueueDetailLoader.Build(imagePath, reviewStatus, data);
        }

        private void ApplyImageQueueDetail(WpfImageQueueItem item, WpfImageQueueDetail detail)
        {
            if (item == null || detail == null)
            {
                return;
            }

            item.Dimensions = WpfImageQueueDetailLoader.FormatImageSize(detail.ImageSize);
            if (IsAnomalyDatasetPurpose())
            {
                ApplyAnomalyReviewStatusToItem(item, anomalyImageReviewStatus.GetOrCreate(item.ImagePath));
                return;
            }
            ApplyReviewStatusToItemCore(item, detail.ReviewStatus, refreshTrainingStepCompletion: false);
        }

        private void ApplyReviewStatusToItem(WpfImageQueueItem item, YoloImageReviewStatus status)
        {
            ApplyReviewStatusToItemCore(item, status, refreshTrainingStepCompletion: true);
        }

        private void ApplyReviewStatusToItemCore(
            WpfImageQueueItem item,
            YoloImageReviewStatus status,
            bool refreshTrainingStepCompletion)
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
            item.QualityReviewState = status.QualityReviewState;
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

            RefreshActiveImageQualityReviewPresentation(item, status);

            if (refreshTrainingStepCompletion)
            {
                RefreshYoloTrainingStepCompletion();
            }
        }

        private sealed class ImageQueueDetailLoadResult
        {
            private ImageQueueDetailLoadResult(WpfImageQueueItem item, WpfImageQueueDetail detail, Exception error)
            {
                Item = item;
                Detail = detail;
                Error = error;
            }

            public WpfImageQueueItem Item { get; }

            public WpfImageQueueDetail Detail { get; }

            public Exception Error { get; }

            public static ImageQueueDetailLoadResult Success(WpfImageQueueItem item, WpfImageQueueDetail detail)
            {
                return new ImageQueueDetailLoadResult(item, detail, null);
            }

            public static ImageQueueDetailLoadResult Failure(WpfImageQueueItem item, Exception error)
            {
                return new ImageQueueDetailLoadResult(item, null, error);
            }
        }
    }
}
