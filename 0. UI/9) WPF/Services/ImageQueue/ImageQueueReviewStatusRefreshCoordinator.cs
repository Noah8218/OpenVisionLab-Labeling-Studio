using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns background label-recount/review-cache refresh work for one active queue image.
    /// The Shell keeps the Dispatcher and row mutation boundary.
    /// </summary>
    public class ImageQueueReviewStatusRefreshCoordinator : IDisposable
    {
        private readonly object syncRoot = new object();
        private CancellationTokenSource activeCancellation;
        private Task activeTask = Task.CompletedTask;
        private int refreshVersion;
        private int disposed;

        public ImageQueueReviewStatusRefreshOperation Queue(
            string imagePath,
            Size imageSize,
            ImageQualityReviewWorkflowService reviewWorkflow,
            LabelingProjectData data,
            bool hasActiveCandidates)
        {
            if (string.IsNullOrWhiteSpace(imagePath)
                || imageSize.IsEmpty
                || reviewWorkflow == null
                || data == null)
            {
                return null;
            }

            int requestVersion;
            CancellationTokenSource previousCancellation;
            CancellationTokenSource cancellation;
            Task<YoloImageReviewStatus> completion;
            lock (syncRoot)
            {
                if (Volatile.Read(ref disposed) != 0)
                {
                    return null;
                }

                previousCancellation = activeCancellation;
                previousCancellation?.Cancel();
                requestVersion = ++refreshVersion;
                cancellation = new CancellationTokenSource();
                activeCancellation = cancellation;
            }

            CancellationToken cancellationToken = cancellation.Token;
            Func<string, Size, YoloImageReviewStatus> refresh = reviewWorkflow.CaptureLabelStatusRefresh(
                data, hasActiveCandidates, saveReviewStatus: true,
                isCurrent: () => IsCurrent(requestVersion) && !cancellationToken.IsCancellationRequested);

            lock (syncRoot)
            {
                completion = Task.Run(() => refresh(imagePath, imageSize), CancellationToken.None);
                if (ReferenceEquals(activeCancellation, cancellation))
                {
                    activeTask = completion;
                }

                completion.ContinueWith(
                    _ => Complete(cancellation),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }

            return new ImageQueueReviewStatusRefreshOperation(requestVersion, imagePath, completion);
        }

        public bool IsCurrent(int requestVersion)
        {
            return Volatile.Read(ref disposed) == 0
                && requestVersion == Volatile.Read(ref refreshVersion);
        }

        public void Dispose()
        {
            CancellationTokenSource cancellation;
            lock (syncRoot)
            {
                if (Interlocked.Exchange(ref disposed, 1) != 0)
                {
                    return;
                }

                ++refreshVersion;
                cancellation = activeCancellation;
                cancellation?.Cancel();
            }
        }

        public void Cancel()
        {
            CancellationTokenSource cancellation;
            lock (syncRoot)
            {
                ++refreshVersion;
                cancellation = activeCancellation;
                cancellation?.Cancel();
            }
        }

        private void Complete(CancellationTokenSource cancellation)
        {
            lock (syncRoot)
            {
                if (ReferenceEquals(activeCancellation, cancellation))
                {
                    activeCancellation = null;
                    activeTask = Task.CompletedTask;
                }
            }

            cancellation.Dispose();
        }
    }

    [Obsolete("Use ImageQueueReviewStatusRefreshCoordinator.", false)]
    public sealed class WpfImageQueueReviewStatusRefreshCoordinator : ImageQueueReviewStatusRefreshCoordinator
    {
    }

    public class ImageQueueReviewStatusRefreshOperation
    {
        internal ImageQueueReviewStatusRefreshOperation(
            int version,
            string imagePath,
            Task<YoloImageReviewStatus> completion)
        {
            Version = version;
            ImagePath = imagePath ?? string.Empty;
            Completion = completion ?? throw new ArgumentNullException(nameof(completion));
        }

        public int Version { get; }

        public string ImagePath { get; }

        public Task<YoloImageReviewStatus> Completion { get; }
    }

    [Obsolete("Use ImageQueueReviewStatusRefreshOperation.", false)]
    public sealed class WpfImageQueueReviewStatusRefreshOperation : ImageQueueReviewStatusRefreshOperation
    {
        internal WpfImageQueueReviewStatusRefreshOperation(ImageQueueReviewStatusRefreshOperation source)
            : base(
                source?.Version ?? throw new ArgumentNullException(nameof(source)),
                source.ImagePath,
                source.Completion)
        {
        }
    }
}
