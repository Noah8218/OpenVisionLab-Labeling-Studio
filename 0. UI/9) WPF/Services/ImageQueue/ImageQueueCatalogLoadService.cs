using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Threading;

namespace MvcVisionSystem
{
    // The service owns the file-backed catalog calculation. The coordinator
    // owns request cancellation/version state; the Shell owns UI application,
    // observable rows, and current-image transitions.
    public class ImageQueueCatalogLoadService
    {
        private readonly ImageQueueSelectionService selectionService;

        public ImageQueueCatalogLoadService(ImageQueueSelectionService selectionService)
        {
            this.selectionService = selectionService ?? throw new ArgumentNullException(nameof(selectionService));
        }

        public ImageQueueCatalogLoadResult Build(
            string imageRoot,
            LabelingProjectData data,
            bool isAnomalyPurpose,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            List<string> imagePaths = selectionService.EnumerateImageFiles(imageRoot, cancellationToken);
            if (isAnomalyPurpose)
            {
                imagePaths = selectionService.InterleaveTopLevelFolderImages(
                    imageRoot,
                    imagePaths,
                    cancellationToken);
            }

            IReadOnlyList<WpfImageQueueCatalogEntry> catalogEntries = selectionService.CreateCatalogEntries(
                imagePaths,
                cancellationToken);

            var reviewStatus = new YoloImageReviewStatusService();
            var reviewWorkflow = new ImageQualityReviewWorkflowService(reviewStatus);
            reviewWorkflow.SetImages(imagePaths);
            reviewWorkflow.LoadReviewStatus(data, imagePaths);

            var anomalyReviewStatus = new AnomalyImageReviewStatusService();
            var anomalyReviewWorkflow = new AnomalyImageReviewWorkflowService(anomalyReviewStatus);
            anomalyReviewWorkflow.SetImages(imagePaths);
            anomalyReviewWorkflow.LoadReviewStatus(data, imagePaths);
            AnomalyImageReviewFolderImportResult anomalyFolderStateSuggestion = isAnomalyPurpose
                ? anomalyReviewWorkflow.PreviewUnreviewedStatesFromParentFolders()
                : null;
            cancellationToken.ThrowIfCancellationRequested();

            return new ImageQueueCatalogLoadResult(
                imagePaths,
                catalogEntries,
                reviewStatus,
                anomalyReviewStatus,
                reviewWorkflow,
                anomalyReviewWorkflow,
                anomalyFolderStateSuggestion);
        }
    }

    [Obsolete("Use ImageQueueCatalogLoadService.", false)]
    public sealed class WpfImageQueueCatalogLoadService : ImageQueueCatalogLoadService
    {
        public WpfImageQueueCatalogLoadService(WpfImageQueueSelectionService selectionService)
            : base(selectionService)
        {
        }
    }

    public class ImageQueueCatalogLoadResult
    {
        public ImageQueueCatalogLoadResult(
            IReadOnlyList<string> imagePaths,
            IReadOnlyList<WpfImageQueueCatalogEntry> catalogEntries,
            YoloImageReviewStatusService reviewStatus,
            AnomalyImageReviewStatusService anomalyReviewStatus,
            ImageQualityReviewWorkflowService reviewWorkflow,
            AnomalyImageReviewWorkflowService anomalyReviewWorkflow,
            AnomalyImageReviewFolderImportResult anomalyFolderStateSuggestion)
        {
            ImagePaths = imagePaths ?? Array.Empty<string>();
            CatalogEntries = catalogEntries ?? Array.Empty<WpfImageQueueCatalogEntry>();
            ReviewStatus = reviewStatus ?? new YoloImageReviewStatusService();
            AnomalyReviewStatus = anomalyReviewStatus ?? new AnomalyImageReviewStatusService();
            ReviewWorkflow = reviewWorkflow ?? new ImageQualityReviewWorkflowService(ReviewStatus);
            AnomalyReviewWorkflow = anomalyReviewWorkflow ?? new AnomalyImageReviewWorkflowService(AnomalyReviewStatus);
            AnomalyFolderStateSuggestion = anomalyFolderStateSuggestion;
        }

        public IReadOnlyList<string> ImagePaths { get; }

        public IReadOnlyList<WpfImageQueueCatalogEntry> CatalogEntries { get; }

        public YoloImageReviewStatusService ReviewStatus { get; }

        public AnomalyImageReviewStatusService AnomalyReviewStatus { get; }

        public ImageQualityReviewWorkflowService ReviewWorkflow { get; }

        public AnomalyImageReviewWorkflowService AnomalyReviewWorkflow { get; }

        public AnomalyImageReviewFolderImportResult AnomalyFolderStateSuggestion { get; }
    }

    [Obsolete("Use ImageQueueCatalogLoadResult.", false)]
    public sealed class WpfImageQueueCatalogLoadResult : ImageQueueCatalogLoadResult
    {
        public WpfImageQueueCatalogLoadResult(
            IReadOnlyList<string> imagePaths,
            IReadOnlyList<WpfImageQueueCatalogEntry> catalogEntries,
            YoloImageReviewStatusService reviewStatus,
            AnomalyImageReviewStatusService anomalyReviewStatus,
            ImageQualityReviewWorkflowService reviewWorkflow,
            AnomalyImageReviewWorkflowService anomalyReviewWorkflow,
            AnomalyImageReviewFolderImportResult anomalyFolderStateSuggestion)
            : base(
                imagePaths,
                catalogEntries,
                reviewStatus,
                anomalyReviewStatus,
                reviewWorkflow,
                anomalyReviewWorkflow,
                anomalyFolderStateSuggestion)
        {
        }
    }
}
