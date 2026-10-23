using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the active anomaly review state from catalog adoption through
    /// commands, queue projection, folder suggestions and explicit persistence.
    /// Catalog loading creates a detached session; the UI thread adopts it only
    /// after its catalog request has passed the current-request check.
    /// </summary>
    public sealed class AnomalyImageReviewSession
    {
        private readonly AnomalyFolderStateSuggestionWorkflowService folderSuggestion = new AnomalyFolderStateSuggestionWorkflowService();
        private AnomalyImageReviewStatusService reviewStatus;
        private AnomalyImageReviewWorkflowService reviewWorkflow;
        private AnomalyImageReviewFolderImportResult folderStateSuggestion;
        private string reviewStatusFilePath;
        private int? activeCatalogVersion;
        private bool hasActiveState = true;
        private readonly Stack<AnomalyImageReviewChange> reviewHistory = new Stack<AnomalyImageReviewChange>();

        public AnomalyImageReviewSession()
            : this(new AnomalyImageReviewStatusService(), null, null)
        {
        }

        internal AnomalyImageReviewSession(AnomalyImageReviewWorkflowService reviewWorkflow)
            : this(reviewWorkflow?.ReviewStatus, reviewWorkflow ?? throw new ArgumentNullException(nameof(reviewWorkflow)), null)
        {
        }

        internal AnomalyImageReviewSession(
            AnomalyImageReviewStatusService reviewStatus,
            AnomalyImageReviewWorkflowService reviewWorkflow,
            AnomalyImageReviewFolderImportResult folderStateSuggestion)
        {
            this.reviewStatus = reviewStatus ?? new AnomalyImageReviewStatusService();
            this.reviewWorkflow = reviewWorkflow ?? new AnomalyImageReviewWorkflowService(this.reviewStatus);
            this.folderStateSuggestion = folderStateSuggestion;
        }

        public string CurrentImageRoot => folderSuggestion.CurrentImageRoot;

        public string DismissedImageRoot => folderSuggestion.DismissedImageRoot;

        public AnomalyImageReviewFolderImportResult FolderStateSuggestion => folderStateSuggestion;

        public bool IsCatalogLoading => activeCatalogVersion.HasValue;

        internal AnomalyImageReviewStatusService ReviewStatus => reviewStatus;

        internal AnomalyImageReviewWorkflowService ReviewWorkflow => reviewWorkflow;

        public static AnomalyImageReviewSession Load(IEnumerable<string> imagePaths, LabelingProjectData data, bool isAnomalyPurpose)
        {
            var session = new AnomalyImageReviewSession();
            session.reviewStatusFilePath = AnomalyImageReviewStatusService.ResolveReviewStatusFilePath(data);
            session.reviewWorkflow.SetImages(imagePaths);
            session.reviewWorkflow.LoadReviewStatus(data, imagePaths);
            session.folderStateSuggestion = isAnomalyPurpose
                ? session.reviewWorkflow.PreviewUnreviewedStatesFromParentFolders()
                : null;
            return session;
        }

        public void AdoptCatalog(string imageRoot, AnomalyImageReviewSession loaded)
        {
            if (loaded == null)
            {
                throw new ArgumentNullException(nameof(loaded));
            }

            TrackImageRoot(imageRoot);
            reviewStatus = loaded.reviewStatus;
            reviewWorkflow = loaded.reviewWorkflow;
            folderStateSuggestion = loaded.folderStateSuggestion;
            reviewStatusFilePath = loaded.reviewStatusFilePath;
            hasActiveState = loaded.hasActiveState;
            reviewHistory.Clear();
        }

        public void BeginCatalogLoad(int version)
        {
            reviewHistory.Clear();
            activeCatalogVersion = version;
        }

        public void CompleteCatalogLoad(int version)
        {
            if (activeCatalogVersion == version)
            {
                activeCatalogVersion = null;
            }
        }

        public void CancelCatalogLoad()
        {
            activeCatalogVersion = null;
        }

        public bool CanReview(LabelingProjectData data)
        {
            return hasActiveState && !IsCatalogLoading
                && (reviewStatusFilePath == null
                    || string.Equals(reviewStatusFilePath, AnomalyImageReviewStatusService.ResolveReviewStatusFilePath(data), StringComparison.OrdinalIgnoreCase));
        }

        public AnomalyImageReviewCommandResult Apply(
            string imagePath,
            string imageName,
            AnomalyImageReviewState state,
            bool isAnomalyPurpose,
            LabelingProjectData data,
            bool saveReviewStatus = true,
            ImageQualityReviewWorkflowService qualityReview = null)
        {
            if (!isAnomalyPurpose || string.IsNullOrWhiteSpace(imagePath) || !CanReview(data))
            {
                return AnomalyImageReviewCommandResult.NotApplicable();
            }

            if (saveReviewStatus)
            {
                ApplyMany(new[] { imagePath }, state, isAnomalyPurpose, data, qualityReview);
                return AnomalyImageReviewCommandResult.Applied(reviewWorkflow.GetOrCreate(imagePath));
            }

            reviewHistory.Clear();
            AnomalyImageReviewResult result = reviewWorkflow.ApplyReviewState(
                new AnomalyImageReviewRequest(imagePath, imageName, state, saveReviewStatus),
                data);
            return result?.IsApplicable == true
                ? AnomalyImageReviewCommandResult.Applied(result.Status)
                : AnomalyImageReviewCommandResult.NotApplicable();
        }

        public IReadOnlyList<AnomalyImageReviewStatus> ApplyMany(IEnumerable<string> imagePaths, AnomalyImageReviewState state, bool isAnomalyPurpose, LabelingProjectData data, ImageQualityReviewWorkflowService qualityReview = null)
        {
            if (!isAnomalyPurpose || !CanReview(data)) return Array.Empty<AnomalyImageReviewStatus>();
            AnomalyImageReviewChange change = reviewWorkflow.ApplyReviewStates(imagePaths, state, data, qualityReview);
            // A bulk decision saves once, while Ctrl+Z restores one image at a time as the grid hint promises.
            for (int index = 0; index < change.Before.Count; index++)
                reviewHistory.Push(new AnomalyImageReviewChange(new[] { change.Before[index] }, new[] { change.After[index] }));
            return change.After;
        }

        public bool CanUndoReview(LabelingProjectData data)
        {
            return CanReview(data) && reviewHistory.Count > 0 && reviewHistory.Peek().After.All(snapshot =>
            {
                AnomalyImageReviewStatus current = reviewWorkflow.GetOrCreate(snapshot.ImagePath);
                return current.ReviewState == snapshot.ReviewState && current.LastUpdatedUtc == snapshot.LastUpdatedUtc;
            });
        }

        public IReadOnlyList<AnomalyImageReviewStatus> UndoReview(LabelingProjectData data, ImageQualityReviewWorkflowService qualityReview = null)
        {
            if (!CanUndoReview(data)) return Array.Empty<AnomalyImageReviewStatus>();
            IReadOnlyList<AnomalyImageReviewStatus> restored = reviewWorkflow.UndoReview(reviewHistory.Peek(), data, qualityReview);
            reviewHistory.Pop();
            return restored;
        }

        public AnomalyImageReviewNextResult FindNextUnreviewed(
            IReadOnlyList<string> orderedImagePaths,
            string currentImagePath,
            bool isAnomalyPurpose)
        {
            if (!isAnomalyPurpose || !hasActiveState || IsCatalogLoading
                || !reviewWorkflow.TryFindNextUnreviewed(orderedImagePaths, currentImagePath, out string nextImagePath))
            {
                return AnomalyImageReviewNextResult.NotFound();
            }

            return AnomalyImageReviewNextResult.Found(nextImagePath);
        }

        public AnomalyClassificationResult ApplyClassification(AnomalyClassificationRequest request, LabelingProjectData data)
        {
            return CanReview(data)
                ? reviewWorkflow.ApplyClassification(request, data)
                : AnomalyClassificationResult.NotApplicable();
        }

        public void SaveReviewStatus(LabelingProjectData data)
        {
            if (CanReview(data))
            {
                reviewWorkflow.SaveReviewStatus(data);
            }
        }

        // A running batch must flush its original state before a catalog reload
        // starts reading. The callback never follows a subsequently adopted state.
        public Action CaptureReviewStatusSave(LabelingProjectData data)
        {
            if (!CanReview(data))
            {
                return () => { };
            }

            AnomalyImageReviewWorkflowService workflow = reviewWorkflow;
            string filePath = AnomalyImageReviewStatusService.ResolveReviewStatusFilePath(data);
            return () =>
            {
                if (string.Equals(filePath, AnomalyImageReviewStatusService.ResolveReviewStatusFilePath(data), StringComparison.OrdinalIgnoreCase))
                {
                    workflow.SaveReviewStatus(data);
                }
            };
        }

        public AnomalyImageReviewStatus GetStatus(string imagePath, bool isAnomalyPurpose)
        {
            return isAnomalyPurpose && !string.IsNullOrWhiteSpace(imagePath)
                ? reviewWorkflow.GetOrCreate(imagePath)
                : null;
        }

        public AnomalyImageReviewQueueProjection BuildQueue(IReadOnlyList<string> imagePaths, bool isAnomalyPurpose)
        {
            if (!isAnomalyPurpose)
            {
                return AnomalyImageReviewQueueProjection.NotApplicable();
            }

            var statuses = new Dictionary<string, AnomalyImageReviewStatus>(StringComparer.OrdinalIgnoreCase);
            foreach (string imagePath in imagePaths ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(imagePath))
                {
                    statuses[imagePath] = reviewWorkflow.GetOrCreate(imagePath);
                }
            }

            return AnomalyImageReviewQueueProjection.Applied(statuses);
        }

        public AnomalyImageReviewSummary LoadSummary(LabelingProjectData data, int totalImageCount, bool isAnomalyPurpose)
        {
            return isAnomalyPurpose
                ? reviewWorkflow.LoadPersistedSummary(data, totalImageCount)
                : null;
        }

        public void TrackImageRoot(string imageRoot)
        {
            folderSuggestion.TrackImageRoot(imageRoot);
        }

        public bool ShouldShowFolderSuggestion(bool isAnomalyPurpose)
        {
            return folderSuggestion.ShouldShow(CurrentImageRoot, isAnomalyPurpose, folderStateSuggestion);
        }

        public AnomalyFolderStateSuggestionApplyResult ApplyFolderSuggestion(bool isAnomalyPurpose, LabelingProjectData data)
        {
            if (!CanReview(data))
            {
                return AnomalyFolderStateSuggestionApplyResult.NotApplicable();
            }

            AnomalyFolderStateSuggestionApplyResult result = folderSuggestion.Apply(CurrentImageRoot, isAnomalyPurpose, reviewWorkflow);
            if (result.HasChanges)
            {
                reviewHistory.Clear();
                reviewWorkflow.SaveReviewStatus(data);
            }

            return result;
        }

        public void DismissFolderSuggestion()
        {
            folderSuggestion.Dismiss(CurrentImageRoot);
        }

        public void Reset()
        {
            reviewStatus = new AnomalyImageReviewStatusService();
            reviewWorkflow = new AnomalyImageReviewWorkflowService(reviewStatus);
            folderStateSuggestion = null;
            reviewStatusFilePath = null;
            activeCatalogVersion = null;
            hasActiveState = false;
            reviewHistory.Clear();
            folderSuggestion.Reset();
        }
    }
}
