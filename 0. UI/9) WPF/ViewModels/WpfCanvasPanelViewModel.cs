namespace MvcVisionSystem
{
    public enum WpfDetectionOverlayStatus
    {
        Confirmable,
        Duplicate,
        Review
    }

    public sealed class WpfCanvasPanelViewModel : WpfObservableViewModel
    {
        private bool isFitEnabled;
        private bool isActualSizeEnabled;
        private bool isPanEnabled;
        private bool isFocusCandidateEnabled;
        private bool isResetAiOverlayEnabled;
        private System.Windows.Visibility detectionOverlayVisibility = System.Windows.Visibility.Collapsed;
        private string detectionOverlayTitleText = "AI \uAC80\uCD9C \uACB0\uACFC";
        private string detectionOverlaySummaryText = string.Empty;
        private string detectionOverlaySelectedText = string.Empty;
        private string detectionOverlayDetailText = string.Empty;
        private string detectionOverlayStatusKey = WpfDetectionOverlayStatus.Confirmable.ToString();

        public string ViewName => nameof(WpfCanvasPanel);

        public bool IsFitEnabled
        {
            get => isFitEnabled;
            private set => SetProperty(ref isFitEnabled, value);
        }

        public bool IsActualSizeEnabled
        {
            get => isActualSizeEnabled;
            private set => SetProperty(ref isActualSizeEnabled, value);
        }

        public bool IsPanEnabled
        {
            get => isPanEnabled;
            private set => SetProperty(ref isPanEnabled, value);
        }

        public bool IsFocusCandidateEnabled
        {
            get => isFocusCandidateEnabled;
            private set => SetProperty(ref isFocusCandidateEnabled, value);
        }

        public bool IsResetAiOverlayEnabled
        {
            get => isResetAiOverlayEnabled;
            private set => SetProperty(ref isResetAiOverlayEnabled, value);
        }

        public System.Windows.Visibility DetectionOverlayVisibility
        {
            get => detectionOverlayVisibility;
            private set => SetProperty(ref detectionOverlayVisibility, value);
        }

        public string DetectionOverlayTitleText
        {
            get => detectionOverlayTitleText;
            private set => SetProperty(ref detectionOverlayTitleText, value ?? string.Empty);
        }

        public string DetectionOverlaySummaryText
        {
            get => detectionOverlaySummaryText;
            private set => SetProperty(ref detectionOverlaySummaryText, value ?? string.Empty);
        }

        public string DetectionOverlaySelectedText
        {
            get => detectionOverlaySelectedText;
            private set => SetProperty(ref detectionOverlaySelectedText, value ?? string.Empty);
        }

        public string DetectionOverlayDetailText
        {
            get => detectionOverlayDetailText;
            private set => SetProperty(ref detectionOverlayDetailText, value ?? string.Empty);
        }

        public string DetectionOverlayStatusKey
        {
            get => detectionOverlayStatusKey;
            private set => SetProperty(ref detectionOverlayStatusKey, value ?? WpfDetectionOverlayStatus.Confirmable.ToString());
        }

        public void SetCommandAvailability(bool hasImage, bool hasSelectedCandidate, bool hasPendingCandidates)
        {
            IsFitEnabled = hasImage;
            IsActualSizeEnabled = hasImage;
            IsPanEnabled = hasImage;
            IsFocusCandidateEnabled = hasImage && hasSelectedCandidate;
            IsResetAiOverlayEnabled = hasImage && hasPendingCandidates;
        }

        public void ClearDetectionOverlay()
        {
            DetectionOverlayVisibility = System.Windows.Visibility.Collapsed;
            DetectionOverlaySummaryText = string.Empty;
            DetectionOverlaySelectedText = string.Empty;
            DetectionOverlayDetailText = string.Empty;
            DetectionOverlayStatusKey = WpfDetectionOverlayStatus.Confirmable.ToString();
        }

        public void SetDetectionOverlay(
            string title,
            string summary,
            string selected,
            string detail,
            WpfDetectionOverlayStatus status)
        {
            DetectionOverlayVisibility = System.Windows.Visibility.Visible;
            DetectionOverlayTitleText = string.IsNullOrWhiteSpace(title) ? "AI \uAC80\uCD9C \uACB0\uACFC" : title;
            DetectionOverlaySummaryText = summary;
            DetectionOverlaySelectedText = selected;
            DetectionOverlayDetailText = detail;
            DetectionOverlayStatusKey = status.ToString();
        }
    }
}
