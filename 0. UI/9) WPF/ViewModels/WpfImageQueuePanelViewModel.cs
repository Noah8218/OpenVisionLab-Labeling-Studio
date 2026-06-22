namespace MvcVisionSystem
{
    public sealed class WpfImageQueuePanelViewModel : WpfObservableViewModel
    {
        private bool isOpenSelectedImageEnabled;
        private bool isDetectSelectedEnabled;
        private bool isBatchDetectEnabled;
        private bool isRetryFailedEnabled;
        private bool isStopBatchEnabled;
        private string queueFilterAllText = "\uC804\uCCB4";
        private string queueFilterCandidateText = WpfImageQueuePresenter.FormatQuickFilterText("\uD6C4\uBCF4", 0);
        private string queueFilterFailedText = WpfImageQueuePresenter.FormatQuickFilterText("\uC2E4\uD328", 0);
        private string queueFilterConfirmedText = WpfImageQueuePresenter.FormatQuickFilterText("\uD655\uC815", 0);
        private string queueFilterSkippedText = WpfImageQueuePresenter.FormatQuickFilterText("\uC2A4\uD0B5", 0);
        private string queueFilterNoCandidateText = WpfImageQueuePresenter.FormatQuickFilterText("\uC5C6\uC74C", 0);
        private bool isQueueFilterAllActive = true;
        private bool isQueueFilterCandidateActive;
        private bool isQueueFilterFailedActive;
        private bool isQueueFilterConfirmedActive;
        private bool isQueueFilterSkippedActive;
        private bool isQueueFilterNoCandidateActive;

        public string ViewName => nameof(WpfImageQueuePanel);

        public bool IsOpenSelectedImageEnabled
        {
            get => isOpenSelectedImageEnabled;
            private set => SetProperty(ref isOpenSelectedImageEnabled, value);
        }

        public bool IsDetectSelectedEnabled
        {
            get => isDetectSelectedEnabled;
            private set => SetProperty(ref isDetectSelectedEnabled, value);
        }

        public bool IsBatchDetectEnabled
        {
            get => isBatchDetectEnabled;
            private set => SetProperty(ref isBatchDetectEnabled, value);
        }

        public bool IsRetryFailedEnabled
        {
            get => isRetryFailedEnabled;
            private set => SetProperty(ref isRetryFailedEnabled, value);
        }

        public bool IsStopBatchEnabled
        {
            get => isStopBatchEnabled;
            private set => SetProperty(ref isStopBatchEnabled, value);
        }

        public string QueueFilterAllText
        {
            get => queueFilterAllText;
            private set => SetProperty(ref queueFilterAllText, value);
        }

        public string QueueFilterCandidateText
        {
            get => queueFilterCandidateText;
            private set => SetProperty(ref queueFilterCandidateText, value);
        }

        public string QueueFilterFailedText
        {
            get => queueFilterFailedText;
            private set => SetProperty(ref queueFilterFailedText, value);
        }

        public string QueueFilterConfirmedText
        {
            get => queueFilterConfirmedText;
            private set => SetProperty(ref queueFilterConfirmedText, value);
        }

        public string QueueFilterSkippedText
        {
            get => queueFilterSkippedText;
            private set => SetProperty(ref queueFilterSkippedText, value);
        }

        public string QueueFilterNoCandidateText
        {
            get => queueFilterNoCandidateText;
            private set => SetProperty(ref queueFilterNoCandidateText, value);
        }

        public bool IsQueueFilterAllActive
        {
            get => isQueueFilterAllActive;
            private set => SetProperty(ref isQueueFilterAllActive, value);
        }

        public bool IsQueueFilterCandidateActive
        {
            get => isQueueFilterCandidateActive;
            private set => SetProperty(ref isQueueFilterCandidateActive, value);
        }

        public bool IsQueueFilterFailedActive
        {
            get => isQueueFilterFailedActive;
            private set => SetProperty(ref isQueueFilterFailedActive, value);
        }

        public bool IsQueueFilterConfirmedActive
        {
            get => isQueueFilterConfirmedActive;
            private set => SetProperty(ref isQueueFilterConfirmedActive, value);
        }

        public bool IsQueueFilterSkippedActive
        {
            get => isQueueFilterSkippedActive;
            private set => SetProperty(ref isQueueFilterSkippedActive, value);
        }

        public bool IsQueueFilterNoCandidateActive
        {
            get => isQueueFilterNoCandidateActive;
            private set => SetProperty(ref isQueueFilterNoCandidateActive, value);
        }

        public void SetSelectedImageAvailability(bool canOpenSelectedImage)
        {
            IsOpenSelectedImageEnabled = canOpenSelectedImage;
        }

        public void ApplyWorkflowCommandState(WpfWorkflowCommandState state)
        {
            bool canRunInference = state?.CanRunInference == true;
            IsDetectSelectedEnabled = canRunInference;
            IsBatchDetectEnabled = canRunInference;
            IsRetryFailedEnabled = canRunInference;
            IsStopBatchEnabled = state?.CanStopBatchDetection == true;
        }

        public void SetQuickFilterState(
            WpfImageQueueFilter selectedFilter,
            int candidateCount,
            int failedCount,
            int confirmedCount,
            int skippedCount,
            int noCandidateCount)
        {
            QueueFilterAllText = "\uC804\uCCB4";
            QueueFilterCandidateText = WpfImageQueuePresenter.FormatQuickFilterText("\uD6C4\uBCF4", candidateCount);
            QueueFilterFailedText = WpfImageQueuePresenter.FormatQuickFilterText("\uC2E4\uD328", failedCount);
            QueueFilterConfirmedText = WpfImageQueuePresenter.FormatQuickFilterText("\uD655\uC815", confirmedCount);
            QueueFilterSkippedText = WpfImageQueuePresenter.FormatQuickFilterText("\uC2A4\uD0B5", skippedCount);
            QueueFilterNoCandidateText = WpfImageQueuePresenter.FormatQuickFilterText("\uC5C6\uC74C", noCandidateCount);

            IsQueueFilterAllActive = selectedFilter == WpfImageQueueFilter.All;
            IsQueueFilterCandidateActive = selectedFilter == WpfImageQueueFilter.Candidate;
            IsQueueFilterFailedActive = selectedFilter == WpfImageQueueFilter.Failed;
            IsQueueFilterConfirmedActive = selectedFilter == WpfImageQueueFilter.Confirmed;
            IsQueueFilterSkippedActive = selectedFilter == WpfImageQueueFilter.Skipped;
            IsQueueFilterNoCandidateActive = selectedFilter == WpfImageQueueFilter.NoCandidate;
        }
    }
}
