using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using OpenVisionLab;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the WPF side of Image Queue detail refresh.
    /// File enumeration and detail calculation remain in ImageQueueDetailRefreshService;
    /// this adapter owns row projection, dispatcher affinity, cancellation, and close wait.
    /// </summary>
    internal sealed class ImageQueueDetailRefreshAdapter : IDisposable
    {
        private readonly ImageQueueDetailRefreshAdapterContext context;
        private readonly ImageQueueDetailRefreshCoordinator coordinator;

        internal ImageQueueDetailRefreshAdapter(ImageQueueDetailRefreshAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            coordinator = new ImageQueueDetailRefreshCoordinator(
                context.RefreshService ?? throw new ArgumentNullException(nameof(context.RefreshService)));
        }

        internal Task CurrentTask => coordinator.CurrentTask;

        internal ImageQueueDetailRefreshOperation Begin(
            IReadOnlyList<string> imagePaths,
            IReadOnlyDictionary<string, WpfImageQueueItem> itemLookup,
            LabelingProjectData data)
        {
            if (context.IsApplicationCloseApproved()
                || imagePaths == null
                || imagePaths.Count == 0
                || data == null)
            {
                return null;
            }

            IReadOnlyDictionary<string, WpfImageQueueItem> lookup = itemLookup
                ?? new Dictionary<string, WpfImageQueueItem>(StringComparer.OrdinalIgnoreCase);
            return coordinator.Begin(
                imagePaths,
                context.ReviewWorkflow,
                data,
                (results, loadedCount, totalCount, token) => ApplyImageQueueDetailBatchAsync(
                    results,
                    lookup,
                    loadedCount,
                    totalCount,
                    token),
                CompleteImageQueueDetailRefreshAsync);
        }

        internal Task Cancel(bool waitForCompletion)
        {
            Task detailTask = coordinator.Cancel();
            if (waitForCompletion)
            {
                WaitForCompletion(detailTask);
            }

            return detailTask;
        }

        public void Dispose() => coordinator.Dispose();

        private Task CompleteImageQueueDetailRefreshAsync(CancellationToken token)
        {
            if (context.IsApplicationCloseApproved() || token.IsCancellationRequested)
            {
                return Task.CompletedTask;
            }

            return context.Dispatcher.InvokeAsync(
                () => CompleteImageQueueDetailRefresh(token),
                DispatcherPriority.Background,
                token).Task;
        }

        private async Task ApplyImageQueueDetailBatchAsync(
            IReadOnlyList<ImageQueueDetailRefreshResult> results,
            IReadOnlyDictionary<string, WpfImageQueueItem> itemLookup,
            int loadedCount,
            int totalCount,
            CancellationToken token)
        {
            if (results == null || results.Count == 0 || context.IsApplicationCloseApproved())
            {
                return;
            }

            await context.Dispatcher.InvokeAsync(
                () => ApplyImageQueueDetailBatch(results, itemLookup, loadedCount, totalCount, token),
                DispatcherPriority.Background,
                token).Task.ConfigureAwait(false);
        }

        private void ApplyImageQueueDetailBatch(
            IReadOnlyList<ImageQueueDetailRefreshResult> results,
            IReadOnlyDictionary<string, WpfImageQueueItem> itemLookup,
            int loadedCount,
            int totalCount,
            CancellationToken token)
        {
            if (context.IsApplicationCloseApproved() || token.IsCancellationRequested)
            {
                return;
            }

            foreach (ImageQueueDetailRefreshResult result in results ?? Array.Empty<ImageQueueDetailRefreshResult>())
            {
                if (result == null
                    || itemLookup == null
                    || !itemLookup.TryGetValue(result.ImagePath, out WpfImageQueueItem item)
                    || item == null)
                {
                    continue;
                }

                if (result.Error != null)
                {
                    item.LabelStatus = "상태 확인 실패";
                    item.DetectStatus = "대기";
                    context.AppendLog?.Invoke($"Image status failed: {Path.GetFileName(item.ImagePath)}  {result.Error.Message}");
                    continue;
                }

                ApplyImageQueueDetail(item, result.Detail);
            }

            UpdateImageQueueDetailProgress(loadedCount, totalCount);
        }

        private void CompleteImageQueueDetailRefresh(CancellationToken token)
        {
            if (context.IsApplicationCloseApproved() || token.IsCancellationRequested)
            {
                return;
            }

            // One final full view refresh makes the active filter exact without re-evaluating all rows for every detail batch.
            context.ImageQueuePanelControl?.RefreshQueueView();
            context.UpdateImageQueueStatusText?.Invoke();
            context.RefreshYoloTrainingStepCompletion?.Invoke();
        }

        private void UpdateImageQueueDetailProgress(int loadedCount, int totalCount)
        {
            int total = Math.Max(0, totalCount);
            int loaded = Math.Min(Math.Max(0, loadedCount), total);
            string activeImagePath = context.ActiveImagePathProvider?.Invoke() ?? string.Empty;
            string activeText = string.IsNullOrWhiteSpace(activeImagePath)
                ? string.Empty
                : string.Format(
                    CultureInfo.InvariantCulture,
                    OpenVisionLanguageService.T("WpfShell.Status.DatasetDetailActiveImage"),
                    Path.GetFileName(activeImagePath));
            context.SetDatasetStatus?.Invoke(string.Format(
                CultureInfo.InvariantCulture,
                OpenVisionLanguageService.T("WpfShell.Status.DatasetDetailProgress"),
                context.QueueItemCountProvider?.Invoke() ?? 0,
                total,
                loaded,
                total,
                activeText));
        }

        private void ApplyImageQueueDetail(WpfImageQueueItem item, WpfImageQueueDetail detail)
        {
            if (item == null || detail == null)
            {
                return;
            }

            item.Dimensions = ImageQueueDetailLoader.FormatImageSize(detail.ImageSize);
            if (context.IsAnomalyDatasetPurpose())
            {
                WpfImageQueuePresenter.ApplyAnomalyReviewStatusToItem(
                    item,
                    context.AnomalyImageReviewSession.GetStatus(item.ImagePath, isAnomalyPurpose: true));
                return;
            }

            context.ApplyStandardReviewStatus?.Invoke(item, detail.ReviewStatus);
        }

        private void WaitForCompletion(Task detailTask)
        {
            if (detailTask == null || detailTask.IsCompleted)
            {
                return;
            }

            if (!context.Dispatcher.CheckAccess())
            {
                try
                {
                    detailTask.Wait(TimeSpan.FromSeconds(2));
                }
                catch (AggregateException exception)
                {
                    context.AppendLog?.Invoke("Image Queue detail refresh close wait failed: " + exception.GetBaseException().Message);
                }

                return;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            while (!detailTask.IsCompleted && stopwatch.Elapsed < TimeSpan.FromSeconds(2))
            {
                // Detail refresh resumes on the UI dispatcher; pump briefly so close can release image file handles.
                var frame = new DispatcherFrame();
                context.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);
            }

            if (detailTask.IsFaulted)
            {
                context.AppendLog?.Invoke("Image Queue detail refresh failed during close: " + detailTask.Exception.GetBaseException().Message);
            }
        }
    }

    internal sealed class ImageQueueDetailRefreshAdapterContext
    {
        internal ImageQueueDetailRefreshService RefreshService { get; init; }
        internal ImageQualityReviewWorkflowService ReviewWorkflow { get; init; }
        internal AnomalyImageReviewSession AnomalyImageReviewSession { get; init; }
        internal Func<bool> IsApplicationCloseApproved { get; init; }
        internal Func<bool> IsAnomalyDatasetPurpose { get; init; }
        internal Func<string> ActiveImagePathProvider { get; init; }
        internal Func<int> QueueItemCountProvider { get; init; }
        internal Action<WpfImageQueueItem, YoloImageReviewStatus> ApplyStandardReviewStatus { get; init; }
        internal Action<string> AppendLog { get; init; }
        internal Action<string> SetDatasetStatus { get; init; }
        internal Action UpdateImageQueueStatusText { get; init; }
        internal Action RefreshYoloTrainingStepCompletion { get; init; }
        internal WpfImageQueuePanel ImageQueuePanelControl { get; init; }
        internal Dispatcher Dispatcher { get; init; }
    }
}
