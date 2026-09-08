using MvcVisionSystem.Yolo;
using OpenVisionLab;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    // The service owns file/detail batch work. The coordinator owns request
    // cancellation/task lifetime; WPF row mutation and dispatcher application
    // stay with the Shell so the result boundary is independently testable.
    public class ImageQueueDetailRefreshService
    {
        public const int BatchSize = 64;
        public const int Parallelism = 4;

        public Task RefreshAsync(
            IReadOnlyList<string> imagePaths,
            ImageQualityReviewWorkflowService reviewWorkflow,
            LabelingProjectData data,
            Func<IReadOnlyList<ImageQueueDetailRefreshResult>, int, int, Task> applyBatchAsync,
            CancellationToken token)
        {
            return RefreshAsync(imagePaths, reviewWorkflow?.CaptureLabelStatusRefresh(data, isCurrent: () => !token.IsCancellationRequested), applyBatchAsync, token);
        }

        internal async Task RefreshAsync(
            IReadOnlyList<string> imagePaths,
            Func<string, System.Drawing.Size, YoloImageReviewStatus> refreshLabelStatus,
            Func<IReadOnlyList<ImageQueueDetailRefreshResult>, int, int, Task> applyBatchAsync,
            CancellationToken token)
        {
            if (imagePaths == null || imagePaths.Count == 0 || refreshLabelStatus == null)
            {
                return;
            }

            if (applyBatchAsync == null)
            {
                throw new ArgumentNullException(nameof(applyBatchAsync));
            }

            int loadedCount = 0;
            var pendingResults = new List<ImageQueueDetailRefreshResult>(BatchSize);
            try
            {
                for (int startIndex = 0; startIndex < imagePaths.Count; startIndex += Parallelism)
                {
                    token.ThrowIfCancellationRequested();
                    int endIndex = Math.Min(imagePaths.Count, startIndex + Parallelism);
                    var detailTasks = new List<Task<ImageQueueDetailRefreshResult>>(endIndex - startIndex);
                    for (int index = startIndex; index < endIndex; index++)
                    {
                        detailTasks.Add(BuildResultAsync(imagePaths[index], refreshLabelStatus, token));
                    }

                    ImageQueueDetailRefreshResult[] results = await Task.WhenAll(detailTasks).ConfigureAwait(false);
                    pendingResults.AddRange(results);
                    loadedCount = endIndex;
                    if (pendingResults.Count >= BatchSize || loadedCount == imagePaths.Count)
                    {
                        await ApplyBatchAsync(
                            pendingResults,
                            loadedCount,
                            imagePaths.Count,
                            applyBatchAsync).ConfigureAwait(false);
                    }
                }

                if (pendingResults.Count > 0)
                {
                    await ApplyBatchAsync(
                        pendingResults,
                        loadedCount,
                        imagePaths.Count,
                        applyBatchAsync).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // A newer queue root or window close owns the newer state.
            }
        }

        private static Task<ImageQueueDetailRefreshResult> BuildResultAsync(
            string imagePath,
            Func<string, System.Drawing.Size, YoloImageReviewStatus> refreshLabelStatus,
            CancellationToken token)
        {
            return Task.Run(
                () =>
                {
                    try
                    {
                        return ImageQueueDetailRefreshResult.Success(
                            imagePath,
                            ImageQueueDetailLoader.Build(imagePath, size => refreshLabelStatus(imagePath, size)));
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        return ImageQueueDetailRefreshResult.Failure(imagePath, ex);
                    }
                },
                token);
        }

        private static async Task ApplyBatchAsync(
            List<ImageQueueDetailRefreshResult> pendingResults,
            int loadedCount,
            int totalCount,
            Func<IReadOnlyList<ImageQueueDetailRefreshResult>, int, int, Task> applyBatchAsync)
        {
            if (pendingResults == null || pendingResults.Count == 0)
            {
                return;
            }

            ImageQueueDetailRefreshResult[] results = pendingResults.ToArray();
            pendingResults.Clear();
            await applyBatchAsync(results, loadedCount, totalCount).ConfigureAwait(false);
        }
    }

    [Obsolete("Use ImageQueueDetailRefreshService.", false)]
    public sealed class WpfImageQueueDetailRefreshService : ImageQueueDetailRefreshService
    {
    }

    public class ImageQueueDetailRefreshResult
    {
        protected ImageQueueDetailRefreshResult(
            string imagePath,
            WpfImageQueueDetail detail,
            Exception error)
        {
            ImagePath = imagePath ?? string.Empty;
            Detail = detail;
            Error = error;
        }

        public string ImagePath { get; }

        public WpfImageQueueDetail Detail { get; }

        public Exception Error { get; }

        public static ImageQueueDetailRefreshResult Success(
            string imagePath,
            WpfImageQueueDetail detail)
        {
            return new ImageQueueDetailRefreshResult(imagePath, detail, null);
        }

        public static ImageQueueDetailRefreshResult Failure(
            string imagePath,
            Exception error)
        {
            return new ImageQueueDetailRefreshResult(imagePath, null, error);
        }
    }

    [Obsolete("Use ImageQueueDetailRefreshResult.", false)]
    public sealed class WpfImageQueueDetailRefreshResult : ImageQueueDetailRefreshResult
    {
        private WpfImageQueueDetailRefreshResult(
            string imagePath,
            WpfImageQueueDetail detail,
            Exception error)
            : base(imagePath, detail, error)
        {
        }

        internal static WpfImageQueueDetailRefreshResult FromCanonical(ImageQueueDetailRefreshResult source)
        {
            return source == null
                ? null
                : new WpfImageQueueDetailRefreshResult(source.ImagePath, source.Detail, source.Error);
        }

        public static new WpfImageQueueDetailRefreshResult Success(
            string imagePath,
            WpfImageQueueDetail detail)
        {
            return new WpfImageQueueDetailRefreshResult(imagePath, detail, null);
        }

        public static new WpfImageQueueDetailRefreshResult Failure(
            string imagePath,
            Exception error)
        {
            return new WpfImageQueueDetailRefreshResult(imagePath, null, error);
        }
    }
}
