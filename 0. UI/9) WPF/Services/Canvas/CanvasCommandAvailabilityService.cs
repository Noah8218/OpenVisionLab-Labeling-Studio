using System;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns canvas command admission from the Shell's image and candidate snapshots.
    /// The ViewModel only projects the immutable result into WPF binding properties.
    /// </summary>
    public sealed class CanvasCommandAvailabilityService
    {
        private bool hasImage;
        private bool hasSelectedCandidate;
        private bool hasPendingCandidates;
        private bool canNavigatePrevious;
        private bool canNavigateNext;
        private bool canFocusCurrentLabel;
        private bool canConfirmSelected;
        private bool canSkipSelected;

        public CanvasCommandAvailabilitySnapshot SetImageState(
            bool hasImage,
            bool hasSelectedCandidate,
            bool hasPendingCandidates)
        {
            this.hasImage = hasImage;
            this.hasSelectedCandidate = hasSelectedCandidate;
            this.hasPendingCandidates = hasPendingCandidates;
            return BuildSnapshot();
        }

        public CanvasCommandAvailabilitySnapshot SetCandidateReviewState(
            bool canNavigatePrevious,
            bool canNavigateNext,
            bool canFocusCurrentLabel,
            bool canConfirmSelected,
            bool canSkipSelected)
        {
            this.canNavigatePrevious = canNavigatePrevious;
            this.canNavigateNext = canNavigateNext;
            this.canFocusCurrentLabel = canFocusCurrentLabel;
            this.canConfirmSelected = canConfirmSelected;
            this.canSkipSelected = canSkipSelected;
            return BuildSnapshot();
        }

        public CanvasCommandAvailabilitySnapshot GetSnapshot() => BuildSnapshot();

        private CanvasCommandAvailabilitySnapshot BuildSnapshot()
        {
            return new CanvasCommandAvailabilitySnapshot(
                isFitEnabled: hasImage,
                isActualSizeEnabled: hasImage,
                isPanEnabled: hasImage,
                isFocusCandidateEnabled: hasImage && hasSelectedCandidate,
                isResetAiOverlayEnabled: hasImage && hasPendingCandidates,
                isPreviousCandidateEnabled: canNavigatePrevious,
                isNextCandidateEnabled: canNavigateNext,
                isFocusCurrentLabelEnabled: canFocusCurrentLabel,
                isConfirmSelectedEnabled: canConfirmSelected,
                isSkipSelectedEnabled: canSkipSelected);
        }
    }

    public sealed class CanvasCommandAvailabilitySnapshot
    {
        public CanvasCommandAvailabilitySnapshot(
            bool isFitEnabled,
            bool isActualSizeEnabled,
            bool isPanEnabled,
            bool isFocusCandidateEnabled,
            bool isResetAiOverlayEnabled,
            bool isPreviousCandidateEnabled,
            bool isNextCandidateEnabled,
            bool isFocusCurrentLabelEnabled,
            bool isConfirmSelectedEnabled,
            bool isSkipSelectedEnabled)
        {
            IsFitEnabled = isFitEnabled;
            IsActualSizeEnabled = isActualSizeEnabled;
            IsPanEnabled = isPanEnabled;
            IsFocusCandidateEnabled = isFocusCandidateEnabled;
            IsResetAiOverlayEnabled = isResetAiOverlayEnabled;
            IsPreviousCandidateEnabled = isPreviousCandidateEnabled;
            IsNextCandidateEnabled = isNextCandidateEnabled;
            IsFocusCurrentLabelEnabled = isFocusCurrentLabelEnabled;
            IsConfirmSelectedEnabled = isConfirmSelectedEnabled;
            IsSkipSelectedEnabled = isSkipSelectedEnabled;
        }

        public bool IsFitEnabled { get; }

        public bool IsActualSizeEnabled { get; }

        public bool IsPanEnabled { get; }

        public bool IsFocusCandidateEnabled { get; }

        public bool IsResetAiOverlayEnabled { get; }

        public bool IsPreviousCandidateEnabled { get; }

        public bool IsNextCandidateEnabled { get; }

        public bool IsFocusCurrentLabelEnabled { get; }

        public bool IsConfirmSelectedEnabled { get; }

        public bool IsSkipSelectedEnabled { get; }
    }
}
