using System;
using System.Drawing;

namespace MvcVisionSystem._1._Core
{
    public enum DetectionCandidateUpdateReason
    {
        CandidatesChanged,
        RequestStarted,
        ResultCompleted,
        StaleResultIgnored,
        SelectionChanged,
        CandidatesCleared,
        CandidatesConfirmed,
        CandidateSkipped,
        RequestTimedOut
    }

    public sealed class DetectionCandidatesUpdatedEventArgs : EventArgs
    {
        public DetectionCandidatesUpdatedEventArgs(
            string imageName,
            string imagePath,
            int candidateCount,
            DetectionCandidateUpdateReason reason = DetectionCandidateUpdateReason.CandidatesChanged)
        {
            ImageName = imageName ?? string.Empty;
            ImagePath = imagePath ?? string.Empty;
            CandidateCount = candidateCount;
            Reason = reason;
        }

        public string ImageName { get; }

        public string ImagePath { get; }

        public int CandidateCount { get; }

        public DetectionCandidateUpdateReason Reason { get; }
    }

    public sealed class DetectionCandidateReviewItem
    {
        public DetectionCandidateReviewItem(
            int index,
            string className,
            float confidence,
            Rectangle rawBounds,
            Rectangle clippedBounds,
            bool isConfidenceAccepted,
            bool isInImageBounds,
            bool isSelected)
        {
            Index = index;
            ClassName = className ?? string.Empty;
            Confidence = confidence;
            RawBounds = rawBounds;
            ClippedBounds = clippedBounds;
            IsConfidenceAccepted = isConfidenceAccepted;
            IsInImageBounds = isInImageBounds;
            IsSelected = isSelected;
        }

        public int Index { get; }

        public string ClassName { get; }

        public float Confidence { get; }

        public Rectangle RawBounds { get; }

        public Rectangle ClippedBounds { get; }

        public bool IsConfidenceAccepted { get; }

        public bool IsInImageBounds { get; }

        public bool IsSelected { get; }

        public bool IsConfirmable => IsConfidenceAccepted && IsInImageBounds;
    }
}
