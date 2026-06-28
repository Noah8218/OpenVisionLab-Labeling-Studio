using MvcVisionSystem._1._Core;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    public sealed class WpfDetectionWorkerCompletionWaiter : IDisposable
    {
        private readonly DetectionResultApplicationService detectionResults;
        private readonly string imagePath;
        private readonly CancellationToken cancellationToken;
        private readonly TaskCompletionSource<DetectionCandidatesUpdatedEventArgs> completion =
            new TaskCompletionSource<DetectionCandidatesUpdatedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationTokenRegistration cancelRegistration;
        private bool disposed;

        public WpfDetectionWorkerCompletionWaiter(
            DetectionResultApplicationService detectionResults,
            string imagePath,
            CancellationToken cancellationToken)
        {
            this.detectionResults = detectionResults ?? throw new ArgumentNullException(nameof(detectionResults));
            this.imagePath = imagePath ?? string.Empty;
            this.cancellationToken = cancellationToken;
            this.detectionResults.DetectionCandidatesUpdated += OnDetectionCandidatesUpdated;
            cancelRegistration = cancellationToken.Register(CancelPendingDetection);
        }

        public Task<DetectionCandidatesUpdatedEventArgs> Completion => completion.Task;

        public static bool IsCompletionForImage(DetectionCandidatesUpdatedEventArgs e, string imagePath)
        {
            if (e == null)
            {
                return false;
            }

            if (e.Reason != DetectionCandidateUpdateReason.ResultCompleted
                && e.Reason != DetectionCandidateUpdateReason.RequestTimedOut)
            {
                return false;
            }

            string normalizedPath = imagePath ?? string.Empty;
            string imageName = Path.GetFileNameWithoutExtension(normalizedPath);
            return string.Equals(e.ImagePath, normalizedPath, StringComparison.OrdinalIgnoreCase)
                || string.Equals(e.ImageName, imageName, StringComparison.OrdinalIgnoreCase);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            detectionResults.DetectionCandidatesUpdated -= OnDetectionCandidatesUpdated;
            cancelRegistration.Dispose();
        }

        private void OnDetectionCandidatesUpdated(object sender, DetectionCandidatesUpdatedEventArgs e)
        {
            if (IsCompletionForImage(e, imagePath))
            {
                completion.TrySetResult(e);
            }
        }

        private void CancelPendingDetection()
        {
            detectionResults.CancelPendingDetection();
            completion.TrySetCanceled(cancellationToken);
        }
    }
}
