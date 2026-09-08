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
        private readonly CancellationTokenSource refreshCancellation = new CancellationTokenSource();
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
            CancellationToken cancellationToken;
            lock (syncRoot)
            {
                if (Volatile.Read(ref disposed) != 0)
                {
                    return null;
                }

                requestVersion = ++refreshVersion;
                cancellationToken = refreshCancellation.Token;
            }

            Func<string, Size, YoloImageReviewStatus> refresh = reviewWorkflow.CaptureLabelStatusRefresh(
                data, hasActiveCandidates, saveReviewStatus: true,
                isCurrent: () => IsCurrent(requestVersion) && !cancellationToken.IsCancellationRequested);
            Task<YoloImageReviewStatus> completion = Task.Run(() => refresh(imagePath, imageSize), CancellationToken.None);
            return new ImageQueueReviewStatusRefreshOperation(requestVersion, imagePath, completion);
        }

        public bool IsCurrent(int requestVersion)
        {
            return Volatile.Read(ref disposed) == 0
                && requestVersion == Volatile.Read(ref refreshVersion);
        }

        public void Dispose()
        {
            lock (syncRoot)
            {
                if (Interlocked.Exchange(ref disposed, 1) != 0)
                {
                    return;
                }

                ++refreshVersion;
                refreshCancellation.Cancel();
                refreshCancellation.Dispose();
            }
        }

        public void Cancel()
        {
            lock (syncRoot) ++refreshVersion;
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
