using MvcVisionSystem._1._Core;
using System;
using System.Collections.Generic;

namespace MvcVisionSystem
{
    /// <summary>
    /// Compatibility adapter for callers that supply a fixed review workflow.
    /// The application uses AnomalyImageReviewSession across catalog reloads.
    /// </summary>
    public sealed class AnomalyImageReviewCommandWorkflowService
    {
        private readonly AnomalyImageReviewSession session;

        public AnomalyImageReviewCommandWorkflowService(
            AnomalyImageReviewWorkflowService reviewWorkflow)
        {
            session = new AnomalyImageReviewSession(reviewWorkflow);
        }

        public AnomalyImageReviewCommandResult Apply(
            string imagePath,
            string imageName,
            AnomalyImageReviewState state,
            bool isAnomalyPurpose,
            LabelingProjectData data,
            bool saveReviewStatus = true)
        {
            return session.Apply(imagePath, imageName, state, isAnomalyPurpose, data, saveReviewStatus);
        }

        public AnomalyImageReviewNextResult FindNextUnreviewed(
            IReadOnlyList<string> orderedImagePaths,
            string currentImagePath,
            bool isAnomalyPurpose)
        {
            return session.FindNextUnreviewed(orderedImagePaths, currentImagePath, isAnomalyPurpose);
        }

        public void SaveReviewStatus(LabelingProjectData data)
        {
            session.SaveReviewStatus(data);
        }
    }

    public sealed class AnomalyImageReviewCommandResult
    {
        private AnomalyImageReviewCommandResult(
            bool isApplicable,
            AnomalyImageReviewStatus status)
        {
            IsApplicable = isApplicable;
            Status = status;
        }

        public bool IsApplicable { get; }

        public AnomalyImageReviewStatus Status { get; }

        public static AnomalyImageReviewCommandResult NotApplicable()
            => new AnomalyImageReviewCommandResult(false, null);

        public static AnomalyImageReviewCommandResult Applied(AnomalyImageReviewStatus status)
            => new AnomalyImageReviewCommandResult(true, status);
    }

    public sealed class AnomalyImageReviewNextResult
    {
        private AnomalyImageReviewNextResult(bool hasNextImage, string nextImagePath)
        {
            HasNextImage = hasNextImage;
            NextImagePath = nextImagePath ?? string.Empty;
        }

        public bool HasNextImage { get; }

        public string NextImagePath { get; }

        public static AnomalyImageReviewNextResult NotFound()
            => new AnomalyImageReviewNextResult(false, string.Empty);

        public static AnomalyImageReviewNextResult Found(string nextImagePath)
            => new AnomalyImageReviewNextResult(true, nextImagePath);
    }
}
