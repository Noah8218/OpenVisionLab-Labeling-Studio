using MvcVisionSystem._1._Core;
using System;
using System.Collections.Generic;

namespace MvcVisionSystem
{
    /// <summary>
    /// Compatibility adapter for callers that supply a fixed review workflow.
    /// The application uses AnomalyImageReviewSession across catalog reloads.
    /// </summary>
    public sealed class AnomalyImageReviewPresentationWorkflowService
    {
        private readonly AnomalyImageReviewSession session;

        public AnomalyImageReviewPresentationWorkflowService(
            AnomalyImageReviewWorkflowService reviewWorkflow)
        {
            session = new AnomalyImageReviewSession(reviewWorkflow);
        }

        public AnomalyImageReviewStatus GetStatus(string imagePath, bool isAnomalyPurpose)
        {
            return session.GetStatus(imagePath, isAnomalyPurpose);
        }

        public AnomalyImageReviewQueueProjection BuildQueue(
            IReadOnlyList<string> imagePaths,
            bool isAnomalyPurpose)
        {
            return session.BuildQueue(imagePaths, isAnomalyPurpose);
        }

        public AnomalyImageReviewSummary LoadSummary(
            LabelingProjectData data,
            int totalImageCount,
            bool isAnomalyPurpose)
        {
            return session.LoadSummary(data, totalImageCount, isAnomalyPurpose);
        }
    }

    public sealed class AnomalyImageReviewQueueProjection
    {
        private readonly IReadOnlyDictionary<string, AnomalyImageReviewStatus> statuses;

        private AnomalyImageReviewQueueProjection(
            bool isApplicable,
            IReadOnlyDictionary<string, AnomalyImageReviewStatus> statuses)
        {
            IsApplicable = isApplicable;
            this.statuses = statuses ?? new Dictionary<string, AnomalyImageReviewStatus>(StringComparer.OrdinalIgnoreCase);
        }

        public bool IsApplicable { get; }

        public AnomalyImageReviewStatus GetStatus(string imagePath)
        {
            return !string.IsNullOrWhiteSpace(imagePath)
                && statuses.TryGetValue(imagePath, out AnomalyImageReviewStatus status)
                ? status
                : null;
        }

        public static AnomalyImageReviewQueueProjection NotApplicable()
            => new AnomalyImageReviewQueueProjection(false, null);

        public static AnomalyImageReviewQueueProjection Applied(
            IReadOnlyDictionary<string, AnomalyImageReviewStatus> statuses)
            => new AnomalyImageReviewQueueProjection(true, statuses);
    }
}
