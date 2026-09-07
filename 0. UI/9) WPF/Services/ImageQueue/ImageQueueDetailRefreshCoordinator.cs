using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns one active Image Queue detail refresh and its cancellation lifetime.
    /// The Shell keeps the UI callback, row lookup, and result application.
    /// </summary>
    public class ImageQueueDetailRefreshCoordinator : IDisposable
    {
        private readonly object syncRoot = new object();
        private readonly ImageQueueDetailRefreshService refreshService;
        private CancellationTokenSource activeCancellation;
        private Task activeTask = Task.CompletedTask;
        private int refreshVersion;
        private bool disposed;

        public ImageQueueDetailRefreshCoordinator(ImageQueueDetailRefreshService refreshService)
        {
            this.refreshService = refreshService ?? throw new ArgumentNullException(nameof(refreshService));
        }

        public int CurrentVersion
        {
            get
            {
                lock (syncRoot)
                {
                    return refreshVersion;
                }
            }
        }

        public Task CurrentTask
        {
            get
            {
                lock (syncRoot)
                {
                    return activeTask;
                }
            }
        }

        public ImageQueueDetailRefreshOperation Begin(
            IReadOnlyList<string> imagePaths,
            ImageQualityReviewWorkflowService reviewWorkflow,
            LabelingProjectData data,
            Func<IReadOnlyList<ImageQueueDetailRefreshResult>, int, int, CancellationToken, Task> applyBatchAsync,
            Func<CancellationToken, Task> completeAsync)
        {
            if (imagePaths == null || imagePaths.Count == 0 || reviewWorkflow == null || data == null)
            {
                return null;
            }

            if (applyBatchAsync == null)
            {
                throw new ArgumentNullException(nameof(applyBatchAsync));
            }

            Cancel();
            lock (syncRoot)
            {
                if (disposed)
                {
                    return null;
                }

                var cancellation = new CancellationTokenSource();
                var operation = new ImageQueueDetailRefreshOperation(
                    ++refreshVersion,
                    imagePaths,
                    reviewWorkflow,
                    data,
                    applyBatchAsync,
                    completeAsync,
                    cancellation);
                activeCancellation = cancellation;
                activeTask = Task.Run(() => ExecuteAsync(operation), cancellation.Token);
                return operation;
            }
        }

        public bool IsCurrent(ImageQueueDetailRefreshOperation operation)
        {
            if (operation == null)
            {
                return false;
            }

            lock (syncRoot)
            {
                return !disposed
                    && operation.Version == refreshVersion
                    && ReferenceEquals(operation.Cancellation, activeCancellation)
                    && !operation.CancellationToken.IsCancellationRequested;
            }
        }

        public void Complete(ImageQueueDetailRefreshOperation operation)
        {
            if (operation == null)
            {
                return;
            }

            CancellationTokenSource cancellation = null;
            lock (syncRoot)
            {
                if (ReferenceEquals(operation.Cancellation, activeCancellation))
                {
                    activeCancellation = null;
                    activeTask = Task.CompletedTask;
                    cancellation = operation.Cancellation;
                }
            }

            cancellation?.Dispose();
        }

        public Task Cancel()
        {
            CancellationTokenSource cancellation;
            Task task;
            lock (syncRoot)
            {
                cancellation = activeCancellation;
                task = activeTask;
                if (cancellation == null)
                {
                    return Task.CompletedTask;
                }

                ++refreshVersion;
                activeCancellation = null;
                activeTask = Task.CompletedTask;
            }

            cancellation.Cancel();
            DisposeWhenCompleted(cancellation, task);
            return task ?? Task.CompletedTask;
        }

        public void Dispose()
        {
            CancellationTokenSource cancellation;
            Task task;
            lock (syncRoot)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                cancellation = activeCancellation;
                task = activeTask;
                activeCancellation = null;
                activeTask = Task.CompletedTask;
                if (cancellation != null)
                {
                    ++refreshVersion;
                }
            }

            if (cancellation == null)
            {
                return;
            }

            cancellation.Cancel();
            DisposeWhenCompleted(cancellation, task);
        }

        private async Task ExecuteAsync(ImageQueueDetailRefreshOperation operation)
        {
            try
            {
                await refreshService.RefreshAsync(
                    operation.ImagePaths,
                    operation.ReviewWorkflow,
                    operation.Data,
                    (results, loadedCount, totalCount) => operation.ApplyBatchAsync(
                        results,
                        loadedCount,
                        totalCount,
                        operation.CancellationToken),
                    operation.CancellationToken).ConfigureAwait(false);

                operation.CancellationToken.ThrowIfCancellationRequested();
                if (IsCurrent(operation) && operation.CompleteAsync != null)
                {
                    await operation.CompleteAsync(operation.CancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // A newer queue root or window close owns the newer state.
            }
            finally
            {
                Complete(operation);
            }
        }

        private static void DisposeWhenCompleted(CancellationTokenSource cancellation, Task task)
        {
            if (task == null || task.IsCompleted)
            {
                cancellation.Dispose();
                return;
            }

            task.ContinueWith(
                _ => cancellation.Dispose(),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    [Obsolete("Use ImageQueueDetailRefreshCoordinator.", false)]
    public sealed class WpfImageQueueDetailRefreshCoordinator : ImageQueueDetailRefreshCoordinator
    {
        public WpfImageQueueDetailRefreshCoordinator(ImageQueueDetailRefreshService refreshService)
            : base(refreshService)
        {
        }
    }

    public class ImageQueueDetailRefreshOperation
    {
        internal ImageQueueDetailRefreshOperation(
            int version,
            IReadOnlyList<string> imagePaths,
            ImageQualityReviewWorkflowService reviewWorkflow,
            LabelingProjectData data,
            Func<IReadOnlyList<ImageQueueDetailRefreshResult>, int, int, CancellationToken, Task> applyBatchAsync,
            Func<CancellationToken, Task> completeAsync,
            CancellationTokenSource cancellation)
        {
            Version = version;
            ImagePaths = imagePaths ?? throw new ArgumentNullException(nameof(imagePaths));
            ReviewWorkflow = reviewWorkflow ?? throw new ArgumentNullException(nameof(reviewWorkflow));
            Data = data ?? throw new ArgumentNullException(nameof(data));
            ApplyBatchAsync = applyBatchAsync ?? throw new ArgumentNullException(nameof(applyBatchAsync));
            CompleteAsync = completeAsync;
            Cancellation = cancellation ?? throw new ArgumentNullException(nameof(cancellation));
        }

        public int Version { get; }

        public CancellationToken CancellationToken => Cancellation.Token;

        internal IReadOnlyList<string> ImagePaths { get; }

        internal ImageQualityReviewWorkflowService ReviewWorkflow { get; }

        internal LabelingProjectData Data { get; }

        internal Func<IReadOnlyList<ImageQueueDetailRefreshResult>, int, int, CancellationToken, Task> ApplyBatchAsync { get; }

        internal Func<CancellationToken, Task> CompleteAsync { get; }

        internal CancellationTokenSource Cancellation { get; }
    }

    [Obsolete("Use ImageQueueDetailRefreshOperation.", false)]
    public sealed class WpfImageQueueDetailRefreshOperation : ImageQueueDetailRefreshOperation
    {
        internal WpfImageQueueDetailRefreshOperation(ImageQueueDetailRefreshOperation source)
            : base(
                source?.Version ?? throw new ArgumentNullException(nameof(source)),
                source.ImagePaths,
                source.ReviewWorkflow,
                source.Data,
                source.ApplyBatchAsync,
                source.CompleteAsync,
                source.Cancellation)
        {
        }
    }
}
