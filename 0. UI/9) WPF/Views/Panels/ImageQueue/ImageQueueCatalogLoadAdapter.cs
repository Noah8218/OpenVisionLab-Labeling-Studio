using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the Shell-facing Image Queue catalog request workflow.
    /// File enumeration remains in ImageQueueCatalogLoadService and request
    /// cancellation remains in ImageQueueCatalogLoadCoordinator; this adapter
    /// coordinates the existing owners and hands current snapshots to projection.
    /// </summary>
    internal sealed class ImageQueueCatalogLoadAdapter : IDisposable
    {
        private readonly ImageQueueCatalogLoadAdapterContext context;

        internal ImageQueueCatalogLoadAdapter(ImageQueueCatalogLoadAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            ArgumentNullException.ThrowIfNull(context.Coordinator);
            ArgumentNullException.ThrowIfNull(context.Projection);
            ArgumentNullException.ThrowIfNull(context.DataProvider);
            ArgumentNullException.ThrowIfNull(context.ImageQualityReviewWorkflowService);
            ArgumentNullException.ThrowIfNull(context.AnomalyImageReviewSession);
        }

        internal int Load(
            string imageRoot,
            string selectedImagePath,
            bool loadFirstImage,
            bool refreshDetails)
        {
            if (!TryBegin(imageRoot, selectedImagePath, loadFirstImage, refreshDetails, out ImageQueueCatalogLoadRequest request))
            {
                return 0;
            }

            try
            {
                ImageQueueCatalogLoadResult snapshot = context.Coordinator.Load(request);
                return IsCurrent(request)
                    ? Apply(request, snapshot)
                    : 0;
            }
            catch (OperationCanceledException)
            {
                return 0;
            }
            catch (Exception exception)
            {
                ReportFailure(request, exception);
                return 0;
            }
            finally
            {
                Complete(request);
            }
        }

        internal Task<int> LoadAsync(
            string imageRoot,
            string selectedImagePath,
            bool loadFirstImage,
            bool refreshDetails)
        {
            if (!TryBegin(imageRoot, selectedImagePath, loadFirstImage, refreshDetails, out ImageQueueCatalogLoadRequest request))
            {
                return Task.FromResult(0);
            }

            return LoadAsyncCore(request);
        }

        internal Task Cancel()
        {
            Task catalogTask = context.Coordinator.Cancel();
            context.ImageQualityReviewWorkflowService.CancelCatalogLoad();
            context.AnomalyImageReviewSession.CancelCatalogLoad();
            return catalogTask;
        }

        public void Dispose() => context.Coordinator.Dispose();

        internal bool TryBegin(
            string imageRoot,
            string selectedImagePath,
            bool loadFirstImage,
            bool refreshDetails,
            out ImageQueueCatalogLoadRequest request)
        {
            request = null;
            if (context.IsApplicationCloseApproved?.Invoke() == true)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(imageRoot) || !Directory.Exists(imageRoot))
            {
                context.SetDatasetStatus?.Invoke("데이터셋: 이미지 루트 없음");
                context.AppendLog?.Invoke($"Image root does not exist: {imageRoot}");
                return false;
            }

            context.Coordinator.Cancel();
            context.ImageQualityReviewWorkflowService.CancelCatalogLoad();
            context.AnomalyImageReviewSession.CancelCatalogLoad();
            context.CancelImageQueueDetailRefresh?.Invoke();
            context.BatchDetectionWorkflowService?.Cancel();

            context.SetCurrentImageRoot?.Invoke(imageRoot);
            context.AnomalyImageReviewSession.TrackImageRoot(imageRoot);
            context.ImageQueueViewModel?.SetCurrentImageFolder(imageRoot, canOpenFolder: true);
            context.ImageQueueViewModel?.SetAnomalyImageReviewMode(context.IsAnomalyDatasetPurpose?.Invoke() == true);

            request = context.Coordinator.Begin(
                imageRoot,
                selectedImagePath,
                loadFirstImage,
                refreshDetails,
                context.DataProvider(),
                context.IsAnomalyDatasetPurpose?.Invoke() == true);
            if (request == null)
            {
                return false;
            }

            context.ImageQualityReviewWorkflowService.BeginCatalogLoad(request.Version);
            context.AnomalyImageReviewSession.BeginCatalogLoad(request.Version);
            context.SetDatasetStatus?.Invoke("데이터셋: 파일 목록 준비 중...");
            return true;
        }

        internal int Apply(ImageQueueCatalogLoadRequest request, ImageQueueCatalogLoadResult snapshot)
            => context.Projection.Apply(request, snapshot);

        internal bool IsCurrent(ImageQueueCatalogLoadRequest request)
            => request != null
                && context.Coordinator.IsCurrent(request)
                && request.MatchesData(context.DataProvider());

        internal void Complete(ImageQueueCatalogLoadRequest request)
        {
            if (request != null)
            {
                context.ImageQualityReviewWorkflowService.CompleteCatalogLoad(request.Version);
                context.AnomalyImageReviewSession.CompleteCatalogLoad(request.Version);
            }

            context.Coordinator.Complete(request);
        }

        internal void ReportFailure(ImageQueueCatalogLoadRequest request, Exception exception)
        {
            if (!IsCurrent(request))
            {
                return;
            }

            context.SetDatasetStatus?.Invoke("데이터셋: 파일 목록 준비 실패");
            context.AppendLog?.Invoke($"Image queue catalog load failed: {exception.Message}");
        }

        private async Task<int> LoadAsyncCore(ImageQueueCatalogLoadRequest request)
        {
            try
            {
                ImageQueueCatalogLoadResult snapshot = await context.Coordinator.LoadAsync(request).ConfigureAwait(true);
                return IsCurrent(request)
                    ? Apply(request, snapshot)
                    : 0;
            }
            catch (OperationCanceledException)
            {
                return 0;
            }
            catch (Exception exception)
            {
                ReportFailure(request, exception);
                return 0;
            }
            finally
            {
                Complete(request);
            }
        }
    }

    internal sealed class ImageQueueCatalogLoadAdapterContext
    {
        internal ImageQueueCatalogLoadCoordinator Coordinator { get; init; }
        internal ImageQueueCatalogProjectionAdapter Projection { get; init; }
        internal Func<LabelingProjectData> DataProvider { get; init; }
        internal ImageQualityReviewWorkflowService ImageQualityReviewWorkflowService { get; init; }
        internal AnomalyImageReviewSession AnomalyImageReviewSession { get; init; }
        internal BatchDetectionWorkflowService BatchDetectionWorkflowService { get; init; }
        internal WpfImageQueuePanelViewModel ImageQueueViewModel { get; init; }
        internal Func<bool> IsApplicationCloseApproved { get; init; }
        internal Func<bool> IsAnomalyDatasetPurpose { get; init; }
        internal Action<string> SetCurrentImageRoot { get; init; }
        internal Action CancelImageQueueDetailRefresh { get; init; }
        internal Action<string> SetDatasetStatus { get; init; }
        internal Action<string> AppendLog { get; init; }
    }
}
