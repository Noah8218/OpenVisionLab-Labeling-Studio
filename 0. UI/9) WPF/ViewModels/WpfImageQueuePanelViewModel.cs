using OpenVisionLab.Mvvm;
using System;
using System.Windows.Input;

namespace MvcVisionSystem
{
    public sealed class WpfImageQueuePanelViewModel : WpfObservableViewModel
    {
        private static readonly Action NoOpCommand = () => { };
        private static readonly Action<object> NoOpSelectionCommand = _ => { };
        private static readonly Action<string> NoOpTextCommand = _ => { };
        private static readonly Action NoOpMouseCommand = () => { };
        private static readonly Action<WpfImageQueueItem> NoOpQueueItemCommand = _ => { };
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
        private WpfImageQueueItem selectedQueueItem;
        private Action<WpfImageQueueItem> selectedQueueItemChanged = NoOpQueueItemCommand;
        private ICommand loadImageRootCommand = new RelayCommand(NoOpCommand);
        private ICommand browseImageFolderCommand = new RelayCommand(NoOpCommand);
        private ICommand refreshImageQueueCommand = new RelayCommand(NoOpCommand);
        private ICommand nextUnlabeledCommand = new RelayCommand(NoOpCommand);
        private ICommand openSelectedQueueImageCommand = new RelayCommand(NoOpCommand);
        private ICommand detectSelectedQueueCommand = new RelayCommand(NoOpCommand);
        private ICommand batchDetectQueueCommand = new RelayCommand(NoOpCommand);
        private ICommand retryFailedQueueCommand = new RelayCommand(NoOpCommand);
        private ICommand stopBatchQueueCommand = new RelayCommand(NoOpCommand);
        private ICommand queueFilterAllCommand = new RelayCommand(NoOpCommand);
        private ICommand queueFilterCandidateCommand = new RelayCommand(NoOpCommand);
        private ICommand queueFilterFailedCommand = new RelayCommand(NoOpCommand);
        private ICommand queueFilterConfirmedCommand = new RelayCommand(NoOpCommand);
        private ICommand queueFilterSkippedCommand = new RelayCommand(NoOpCommand);
        private ICommand queueFilterNoCandidateCommand = new RelayCommand(NoOpCommand);
        private ICommand filterSelectionChangedCommand = new RelayCommand<object>(NoOpSelectionCommand);
        private ICommand searchTextChangedCommand = new RelayCommand<string>(NoOpTextCommand);
        private ICommand queueSelectionChangedCommand = new RelayCommand<object>(NoOpSelectionCommand);
        private ICommand queueMouseDoubleClickCommand = new RelayCommand(NoOpMouseCommand);

        public string ViewName => nameof(WpfImageQueuePanel);

        public WpfImageQueueItem SelectedQueueItem
        {
            get => selectedQueueItem;
            // Run selection from the bound property too, so headless tests and first-click UI paths do not depend on event attach timing.
            set
            {
                if (SetProperty(ref selectedQueueItem, value))
                {
                    selectedQueueItemChanged(value);
                }
            }
        }

        public ICommand LoadImageRootCommand
        {
            get => loadImageRootCommand;
            private set => SetProperty(ref loadImageRootCommand, value);
        }

        public ICommand BrowseImageFolderCommand
        {
            get => browseImageFolderCommand;
            private set => SetProperty(ref browseImageFolderCommand, value);
        }

        public ICommand RefreshImageQueueCommand
        {
            get => refreshImageQueueCommand;
            private set => SetProperty(ref refreshImageQueueCommand, value);
        }

        public ICommand NextUnlabeledCommand
        {
            get => nextUnlabeledCommand;
            private set => SetProperty(ref nextUnlabeledCommand, value);
        }

        public ICommand OpenSelectedQueueImageCommand
        {
            get => openSelectedQueueImageCommand;
            private set => SetProperty(ref openSelectedQueueImageCommand, value);
        }

        public ICommand DetectSelectedQueueCommand
        {
            get => detectSelectedQueueCommand;
            private set => SetProperty(ref detectSelectedQueueCommand, value);
        }

        public ICommand BatchDetectQueueCommand
        {
            get => batchDetectQueueCommand;
            private set => SetProperty(ref batchDetectQueueCommand, value);
        }

        public ICommand RetryFailedQueueCommand
        {
            get => retryFailedQueueCommand;
            private set => SetProperty(ref retryFailedQueueCommand, value);
        }

        public ICommand StopBatchQueueCommand
        {
            get => stopBatchQueueCommand;
            private set => SetProperty(ref stopBatchQueueCommand, value);
        }

        public ICommand QueueFilterAllCommand
        {
            get => queueFilterAllCommand;
            private set => SetProperty(ref queueFilterAllCommand, value);
        }

        public ICommand QueueFilterCandidateCommand
        {
            get => queueFilterCandidateCommand;
            private set => SetProperty(ref queueFilterCandidateCommand, value);
        }

        public ICommand QueueFilterFailedCommand
        {
            get => queueFilterFailedCommand;
            private set => SetProperty(ref queueFilterFailedCommand, value);
        }

        public ICommand QueueFilterConfirmedCommand
        {
            get => queueFilterConfirmedCommand;
            private set => SetProperty(ref queueFilterConfirmedCommand, value);
        }

        public ICommand QueueFilterSkippedCommand
        {
            get => queueFilterSkippedCommand;
            private set => SetProperty(ref queueFilterSkippedCommand, value);
        }

        public ICommand QueueFilterNoCandidateCommand
        {
            get => queueFilterNoCandidateCommand;
            private set => SetProperty(ref queueFilterNoCandidateCommand, value);
        }

        public ICommand FilterSelectionChangedCommand
        {
            get => filterSelectionChangedCommand;
            private set => SetProperty(ref filterSelectionChangedCommand, value);
        }

        public ICommand SearchTextChangedCommand
        {
            get => searchTextChangedCommand;
            private set => SetProperty(ref searchTextChangedCommand, value);
        }

        public ICommand QueueSelectionChangedCommand
        {
            get => queueSelectionChangedCommand;
            private set => SetProperty(ref queueSelectionChangedCommand, value);
        }

        public ICommand QueueMouseDoubleClickCommand
        {
            get => queueMouseDoubleClickCommand;
            private set => SetProperty(ref queueMouseDoubleClickCommand, value);
        }

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

        public void ConfigureCommands(
            Action loadImageRoot,
            Action browseImageFolder,
            Action refreshImageQueue,
            Action nextUnlabeled,
            Action openSelectedQueueImage,
            Action detectSelectedQueue,
            Action batchDetectQueue,
            Action retryFailedQueue,
            Action stopBatchQueue,
            Action queueFilterAll,
            Action queueFilterCandidate,
            Action queueFilterFailed,
            Action queueFilterConfirmed,
            Action queueFilterSkipped,
            Action queueFilterNoCandidate,
            Action<WpfImageQueueItem> selectedQueueItemChanged,
            Action<object> filterSelectionChanged,
            Action<string> searchTextChanged,
            Action<object> queueSelectionChanged,
            Action queueMouseDoubleClick)
        {
            // Queue actions stay injected so the virtualized queue view does not relay UI events through code-behind.
            LoadImageRootCommand = new RelayCommand(loadImageRoot ?? NoOpCommand);
            BrowseImageFolderCommand = new RelayCommand(browseImageFolder ?? NoOpCommand);
            RefreshImageQueueCommand = new RelayCommand(refreshImageQueue ?? NoOpCommand);
            NextUnlabeledCommand = new RelayCommand(nextUnlabeled ?? NoOpCommand);
            OpenSelectedQueueImageCommand = new RelayCommand(openSelectedQueueImage ?? NoOpCommand);
            DetectSelectedQueueCommand = new RelayCommand(detectSelectedQueue ?? NoOpCommand);
            BatchDetectQueueCommand = new RelayCommand(batchDetectQueue ?? NoOpCommand);
            RetryFailedQueueCommand = new RelayCommand(retryFailedQueue ?? NoOpCommand);
            StopBatchQueueCommand = new RelayCommand(stopBatchQueue ?? NoOpCommand);
            QueueFilterAllCommand = new RelayCommand(queueFilterAll ?? NoOpCommand);
            QueueFilterCandidateCommand = new RelayCommand(queueFilterCandidate ?? NoOpCommand);
            QueueFilterFailedCommand = new RelayCommand(queueFilterFailed ?? NoOpCommand);
            QueueFilterConfirmedCommand = new RelayCommand(queueFilterConfirmed ?? NoOpCommand);
            QueueFilterSkippedCommand = new RelayCommand(queueFilterSkipped ?? NoOpCommand);
            QueueFilterNoCandidateCommand = new RelayCommand(queueFilterNoCandidate ?? NoOpCommand);
            this.selectedQueueItemChanged = selectedQueueItemChanged ?? NoOpQueueItemCommand;
            FilterSelectionChangedCommand = new RelayCommand<object>(filterSelectionChanged ?? NoOpSelectionCommand);
            SearchTextChangedCommand = new RelayCommand<string>(searchTextChanged ?? NoOpTextCommand);
            QueueSelectionChangedCommand = new RelayCommand<object>(queueSelectionChanged ?? NoOpSelectionCommand);
            QueueMouseDoubleClickCommand = new RelayCommand(queueMouseDoubleClick ?? NoOpMouseCommand);
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
