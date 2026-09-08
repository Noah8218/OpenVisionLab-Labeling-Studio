using System;

namespace MvcVisionSystem
{
    /// <summary>
    /// Coordinates the optional folder-name-to-anomaly-state suggestion shown
    /// by the Image Queue. WPF owns only the card projection and status text.
    /// </summary>
    public sealed class AnomalyFolderStateSuggestionWorkflowService
    {
        private readonly ImageQueueSelectionService selectionService;
        private string currentImageRoot = string.Empty;
        private string dismissedImageRoot = string.Empty;

        public AnomalyFolderStateSuggestionWorkflowService(
            ImageQueueSelectionService selectionService = null)
        {
            this.selectionService = selectionService ?? new ImageQueueSelectionService();
        }

        public string CurrentImageRoot => currentImageRoot;

        public string DismissedImageRoot => dismissedImageRoot;

        public void TrackImageRoot(string imageRoot)
        {
            string normalizedRoot = imageRoot?.Trim() ?? string.Empty;
            if (!selectionService.IsSameRoot(currentImageRoot, normalizedRoot))
            {
                dismissedImageRoot = string.Empty;
            }

            currentImageRoot = normalizedRoot;
        }

        public bool ShouldShow(
            string imageRoot,
            bool isAnomalyPurpose,
            AnomalyImageReviewFolderImportResult suggestion)
        {
            TrackImageRoot(imageRoot);
            return isAnomalyPurpose
                && suggestion?.HasChanges == true
                && !selectionService.IsSameRoot(currentImageRoot, dismissedImageRoot);
        }

        public AnomalyFolderStateSuggestionApplyResult Apply(
            string imageRoot,
            bool isAnomalyPurpose,
            AnomalyImageReviewWorkflowService reviewWorkflow)
        {
            TrackImageRoot(imageRoot);
            if (!isAnomalyPurpose || reviewWorkflow == null)
            {
                return AnomalyFolderStateSuggestionApplyResult.NotApplicable();
            }

            dismissedImageRoot = currentImageRoot;
            return AnomalyFolderStateSuggestionApplyResult.Applied(
                reviewWorkflow.ImportUnreviewedStatesFromParentFolders());
        }

        public void Dismiss(string imageRoot)
        {
            TrackImageRoot(imageRoot);
            dismissedImageRoot = currentImageRoot;
        }

        public void Reset()
        {
            currentImageRoot = string.Empty;
            dismissedImageRoot = string.Empty;
        }
    }

    public sealed class AnomalyFolderStateSuggestionApplyResult
    {
        private AnomalyFolderStateSuggestionApplyResult(
            bool isApplicable,
            AnomalyImageReviewFolderImportResult importResult)
        {
            IsApplicable = isApplicable;
            ImportResult = importResult;
        }

        public bool IsApplicable { get; }

        public AnomalyImageReviewFolderImportResult ImportResult { get; }

        public bool HasChanges => ImportResult?.HasChanges == true;

        public static AnomalyFolderStateSuggestionApplyResult NotApplicable()
            => new AnomalyFolderStateSuggestionApplyResult(false, null);

        public static AnomalyFolderStateSuggestionApplyResult Applied(
            AnomalyImageReviewFolderImportResult importResult)
            => new AnomalyFolderStateSuggestionApplyResult(true, importResult);
    }
}
