using System;
using System.Collections.Generic;
using System.IO;

namespace MvcVisionSystem.Yolo
{
    public enum YoloImageReviewState
    {
        Unreviewed,
        Requested,
        Candidate,
        NoCandidate,
        Confirmed,
        Skipped,
        Failed
    }

    public enum YoloImageQualityReviewState
    {
        Unreviewed,
        NeedsFix,
        Reviewed
    }

    public sealed class YoloImageReviewStatus
    {
        internal YoloImageReviewStatus(string imagePath)
        {
            ImagePath = imagePath ?? string.Empty;
            ImageName = Path.GetFileNameWithoutExtension(ImagePath);
            LabelStatus = new YoloImageLabelStatus(string.Empty, 0, 0);
            DetectionStatusOverride = string.Empty;
            LastDetectionMessage = string.Empty;
            ReviewState = YoloImageReviewState.Unreviewed;
            QualityReviewState = YoloImageQualityReviewState.Unreviewed;
            QualityReviewNote = string.Empty;
        }

        public string ImagePath { get; }

        public string ImageName { get; }

        public YoloImageLabelStatus LabelStatus { get; internal set; }

        public int DetectionCandidateCount { get; internal set; }

        public int DetectionAttemptCount { get; internal set; }

        public string LastDetectionMessage { get; internal set; }

        public DateTime LastUpdatedUtc { get; internal set; }

        public YoloImageReviewState ReviewState { get; internal set; }

        public YoloImageQualityReviewState QualityReviewState { get; internal set; }

        public string QualityReviewNote { get; internal set; }

        public string LabelSuspicionDismissalIdentity { get; internal set; } = string.Empty;

        public bool IsLabelSuspicionDismissed(string evidenceIdentity) => !string.IsNullOrWhiteSpace(evidenceIdentity)
            && string.Equals(LabelSuspicionDismissalIdentity, evidenceIdentity, StringComparison.Ordinal);

        internal string DetectionStatusOverride { get; set; }

        public bool IsLabeled => LabelStatus?.HasObjects == true;

        public string LabelText => LabelStatus?.Text ?? "No Label";

        public string DetectionText
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(DetectionStatusOverride))
                {
                    return DetectionStatusOverride;
                }

                return ReviewState switch
                {
                    YoloImageReviewState.Requested => "Requested",
                    YoloImageReviewState.Candidate =>
                        DetectionCandidateCount > 0
                            ? $"Candidate {DetectionCandidateCount}"
                            : "Candidate",
                    YoloImageReviewState.NoCandidate => "No Candidate",
                    YoloImageReviewState.Confirmed => "Confirmed",
                    YoloImageReviewState.Skipped => "Skipped",
                    YoloImageReviewState.Failed => "Failed",
                    _ => DetectionCandidateCount > 0
                        ? $"Candidate {DetectionCandidateCount}"
                        : string.Empty
                };
            }
        }

        public string DetectionDetailText
        {
            get
            {
                List<string> details = new List<string>();
                if (!string.IsNullOrWhiteSpace(DetectionText))
                {
                    details.Add(DetectionText);
                }

                if (DetectionAttemptCount > 0)
                {
                    details.Add($"Attempt {DetectionAttemptCount}");
                }

                if (!string.IsNullOrWhiteSpace(LastDetectionMessage))
                {
                    details.Add(LastDetectionMessage);
                }

                return details.Count > 0 ? string.Join(" / ", details) : string.Empty;
            }
        }
    }
}
