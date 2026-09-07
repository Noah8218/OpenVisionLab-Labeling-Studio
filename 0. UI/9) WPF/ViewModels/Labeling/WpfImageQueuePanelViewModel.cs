using OpenVisionLab;
using OpenVisionLab.Mvvm;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
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
        private bool isTemplateBatchEnabled;
        private bool isRetryFailedEnabled;
        private bool isStopBatchEnabled;
        private string queueFilterUnfinishedText = Format("WpfImageQueue.Filter.Unfinished", 0);
        private string queueFilterAllText = T("WpfImageQueue.Filter.All");
        private string queueFilterCandidateText = Format("WpfImageQueue.Filter.Candidate", 0);
        private string queueFilterFailedText = Format("WpfImageQueue.Filter.Failed", 0);
        private string queueFilterConfirmedText = Format("WpfImageQueue.Filter.Confirmed", 0);
        private string queueFilterSkippedText = Format("WpfImageQueue.Filter.Skipped", 0);
        private string queueFilterNoCandidateText = Format("WpfImageQueue.Filter.NoCandidate", 0);
        private WpfImageQueueFilter selectedFilterForPresentation = WpfImageQueueFilter.All;
        private int unfinishedCountForPresentation;
        private int candidateCountForPresentation;
        private int failedCountForPresentation;
        private int confirmedCountForPresentation;
        private int skippedCountForPresentation;
        private int noCandidateCountForPresentation;
        private bool isQueueFilterUnfinishedActive;
        private bool isQueueFilterAllActive = true;
        private bool isQueueFilterCandidateActive;
        private bool isQueueFilterFailedActive;
        private bool isQueueFilterConfirmedActive;
        private bool isQueueFilterSkippedActive;
        private bool isQueueFilterNoCandidateActive;
        private WpfImageQueueItem selectedQueueItem;
        private string currentImageTaskTitleText = T("WpfImageQueue.Current.Waiting.Title");
        private string currentImageTaskDetailText = T("WpfImageQueue.Current.Waiting.StandardDetail");
        private string currentImageTaskBadgeText = T("WpfImageQueue.Current.Waiting.Badge");
        private string currentImageTaskKey = "Waiting";
        private string currentImageTaskToolTip = T("WpfImageQueue.Current.Waiting.ToolTip");
        private Action<WpfImageQueueItem> selectedQueueItemChanged = NoOpQueueItemCommand;
        private string currentImageFolderPath = string.Empty;
        private string currentImageFolderDisplayText = T("WpfImageQueue.Folder.Empty");
        private bool isOpenCurrentImageFolderEnabled;
        private bool isAnomalyFolderStateSuggestionVisible;
        private string anomalyFolderStateSuggestionTitleText = T("WpfImageQueue.Anomaly.Suggestion.DefaultTitle");
        private string anomalyFolderStateSuggestionText = string.Empty;
        private string anomalyFolderStateSuggestionApplyText = T("WpfImageQueue.Anomaly.Suggestion.DefaultApply");
        private int anomalyNormalCount;
        private int anomalyAbnormalCount;
        private int anomalyTotalCount;
        private bool isAnomalyImageReviewMode;
        private System.Windows.Visibility anomalyImageReviewVisibility = System.Windows.Visibility.Collapsed;
        private System.Windows.Visibility standardQueueWorkflowVisibility = System.Windows.Visibility.Visible;
        private string queueDecisionColumnHeaderText = T("WpfImageQueue.Decision.Standard");
        private string queueSecondaryColumnHeaderText = T("WpfImageQueue.Secondary.Standard");
        private ICommand loadImageRootCommand = new RelayCommand(NoOpCommand);
        private ICommand browseImageFolderCommand = new RelayCommand(NoOpCommand);
        private ICommand openCurrentImageFolderCommand = new RelayCommand(NoOpCommand);
        private ICommand refreshImageQueueCommand = new RelayCommand(NoOpCommand);
        private ICommand nextUnlabeledCommand = new RelayCommand(NoOpCommand);
        private ICommand openSelectedQueueImageCommand = new RelayCommand(NoOpCommand);
        private ICommand detectSelectedQueueCommand = new RelayCommand(NoOpCommand);
        private ICommand batchDetectQueueCommand = new RelayCommand(NoOpCommand);
        private ICommand templateBatchQueueCommand = new RelayCommand(NoOpCommand);
        private ICommand retryFailedQueueCommand = new RelayCommand(NoOpCommand);
        private ICommand stopBatchQueueCommand = new RelayCommand(NoOpCommand);
        private ICommand queueFilterUnfinishedCommand = new RelayCommand(NoOpCommand);
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
        private ICommand applyAnomalyFolderStateSuggestionCommand = new RelayCommand(NoOpCommand);
        private ICommand dismissAnomalyFolderStateSuggestionCommand = new RelayCommand(NoOpCommand);
        private ICommand markAnomalyNormalCommand = new RelayCommand(NoOpCommand);
        private ICommand markAnomalyAbnormalCommand = new RelayCommand(NoOpCommand);
        private ICommand clearAnomalyReviewCommand = new RelayCommand(NoOpCommand);

        public string ViewName => nameof(WpfImageQueuePanel);

        public string PanelTitleText => T("WpfImageQueue.Panel.Title");

        public string BatchStatusText => T("WpfImageQueue.Batch.Waiting");

        public string LoadConfiguredImageRootNameText => T("WpfImageQueue.LoadConfigured.Name");

        public string LoadConfiguredImageRootToolTipText => T("WpfImageQueue.LoadConfigured.ToolTip");

        public string BrowseImageFolderNameText => T("WpfImageQueue.Browse.Name");

        public string BrowseImageFolderToolTipText => T("WpfImageQueue.Browse.ToolTip");

        public string RefreshImageQueueNameText => T("WpfImageQueue.Refresh.Name");

        public string RefreshImageQueueToolTipText => T("WpfImageQueue.Refresh.ToolTip");

        public string OpenSelectedQueueImageNameText => T("WpfImageQueue.OpenSelected.Name");

        public string OpenSelectedQueueImageToolTipText => T("WpfImageQueue.OpenSelected.ToolTip");

        public string CurrentImageFolderOpenNameText => T("WpfImageQueue.CurrentFolder.Open.Name");

        public string CurrentImageFolderOpenToolTipText => T("WpfImageQueue.CurrentFolder.Open.ToolTip");

        public string QueueWorklistTitleText => T("WpfImageQueue.Worklist.Title");

        public string QueueWorklistSummaryText => IsAnomalyImageReviewMode
            ? T("WpfImageQueue.Worklist.Summary.Anomaly")
            : T("WpfImageQueue.Worklist.Summary.Standard");

        public string QueueWorklistToolTipText => T("WpfImageQueue.Worklist.ToolTip");

        public string QueueWorklistAutomationName => $"{QueueWorklistTitleText} / {QueueFilterUnfinishedText}";

        public string NextUnlabeledActionText => IsAnomalyImageReviewMode
            ? T("WpfImageQueue.Next.Anomaly")
            : T("WpfImageQueue.Next.Standard");

        public string NextUnlabeledToolTip => IsAnomalyImageReviewMode
            ? T("WpfImageQueue.Next.Anomaly.ToolTip")
            : T("WpfImageQueue.Next.Standard.ToolTip");

        public string FilterToolTipText => T("WpfImageQueue.Filter.ToolTip");

        public string SearchAutomationNameText => T("WpfImageQueue.Search.Name");

        public string SearchToolTipText => T("WpfImageQueue.Search.ToolTip");

        public string DetectSelectedNameText => T("WpfImageQueue.Action.Detect.Name");

        public string DetectSelectedToolTipText => T("WpfImageQueue.Action.Detect.ToolTip");

        public string BatchDetectNameText => T("WpfImageQueue.Action.Batch.Name");

        public string BatchDetectToolTipText => T("WpfImageQueue.Action.Batch.ToolTip");

        public string TemplateBatchNameText => T("WpfImageQueue.Action.Template.Name");

        public string TemplateBatchToolTipText => T("WpfImageQueue.Action.Template.ToolTip");

        public string TemplateBatchButtonText => T("WpfImageQueue.Action.Template.Text");

        public string RetryFailedNameText => T("WpfImageQueue.Action.Retry.Name");

        public string RetryFailedToolTipText => T("WpfImageQueue.Action.Retry.ToolTip");

        public string StopBatchNameText => T("WpfImageQueue.Action.Stop.Name");

        public string StopBatchToolTipText => T("WpfImageQueue.Action.Stop.ToolTip");

        public string QueueFileColumnHeaderText => T("WpfImageQueue.Column.File");

        public string QueueDimensionsColumnHeaderText => T("WpfImageQueue.Column.Dimensions");

        public string AnomalyCardAutomationNameText => T("WpfImageQueue.Anomaly.Card.Name");

        public string AnomalyCardTitleText => T("WpfImageQueue.Anomaly.Card.Title");

        public string AnomalyCardDetailText => T("WpfImageQueue.Anomaly.Card.Detail");

        public string AnomalyNormalNameText => T("WpfImageQueue.Anomaly.Normal.Name");

        public string AnomalyNormalToolTipText => T("WpfImageQueue.Anomaly.Normal.ToolTip");

        public string AnomalyNormalButtonText => T("WpfImageQueue.Anomaly.Normal.Text");

        public string AnomalyAbnormalNameText => T("WpfImageQueue.Anomaly.Abnormal.Name");

        public string AnomalyAbnormalToolTipText => T("WpfImageQueue.Anomaly.Abnormal.ToolTip");

        public string AnomalyAbnormalButtonText => T("WpfImageQueue.Anomaly.Abnormal.Text");

        public string ClearAnomalyReviewNameText => T("WpfImageQueue.Anomaly.Clear.Name");

        public string ClearAnomalyReviewToolTipText => T("WpfImageQueue.Anomaly.Clear.ToolTip");

        public string ClearAnomalyReviewButtonText => T("WpfImageQueue.Anomaly.Clear.Text");

        public string ApplyAnomalySuggestionNameText => T("WpfImageQueue.Anomaly.Suggestion.Apply.Name");

        public string ApplyAnomalySuggestionToolTipText => T("WpfImageQueue.Anomaly.Suggestion.Apply.ToolTip");

        public string DismissAnomalySuggestionNameText => T("WpfImageQueue.Anomaly.Suggestion.Dismiss.Name");

        public string DismissAnomalySuggestionToolTipText => T("WpfImageQueue.Anomaly.Suggestion.Dismiss.ToolTip");

        public string DismissAnomalySuggestionButtonText => T("WpfImageQueue.Anomaly.Suggestion.Dismiss.Text");

        public WpfImageQueueItem SelectedQueueItem
        {
            get => selectedQueueItem;
            // Run selection from the bound property too, so headless tests and first-click UI paths do not depend on event attach timing.
            set
            {
                if (ReferenceEquals(selectedQueueItem, value))
                {
                    return;
                }

                if (selectedQueueItem != null)
                {
                    selectedQueueItem.PropertyChanged -= OnSelectedQueueItemPropertyChanged;
                }

                if (SetProperty(ref selectedQueueItem, value))
                {
                    if (selectedQueueItem != null)
                    {
                        selectedQueueItem.PropertyChanged += OnSelectedQueueItemPropertyChanged;
                    }

                    RefreshCurrentImageTaskSummary();
                    selectedQueueItemChanged(value);
                }
            }
        }

        public string CurrentImageTaskTitleText
        {
            get => currentImageTaskTitleText;
            private set => SetProperty(ref currentImageTaskTitleText, value ?? string.Empty);
        }

        public string CurrentImageTaskDetailText
        {
            get => currentImageTaskDetailText;
            private set => SetProperty(ref currentImageTaskDetailText, value ?? string.Empty);
        }

        public string CurrentImageTaskBadgeText
        {
            get => currentImageTaskBadgeText;
            private set => SetProperty(ref currentImageTaskBadgeText, value ?? string.Empty);
        }

        public string CurrentImageTaskKey
        {
            get => currentImageTaskKey;
            private set => SetProperty(ref currentImageTaskKey, value ?? "Waiting");
        }

        public string CurrentImageTaskToolTip
        {
            get => currentImageTaskToolTip;
            private set => SetProperty(ref currentImageTaskToolTip, value ?? string.Empty);
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

        public ICommand OpenCurrentImageFolderCommand
        {
            get => openCurrentImageFolderCommand;
            private set => SetProperty(ref openCurrentImageFolderCommand, value);
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

        public ICommand TemplateBatchQueueCommand
        {
            get => templateBatchQueueCommand;
            private set => SetProperty(ref templateBatchQueueCommand, value);
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

        public ICommand QueueFilterUnfinishedCommand
        {
            get => queueFilterUnfinishedCommand;
            private set => SetProperty(ref queueFilterUnfinishedCommand, value);
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

        public bool IsTemplateBatchEnabled
        {
            get => isTemplateBatchEnabled;
            private set => SetProperty(ref isTemplateBatchEnabled, value);
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

        public string CurrentImageFolderPath
        {
            get => currentImageFolderPath;
            private set => SetProperty(ref currentImageFolderPath, value ?? string.Empty);
        }

        public string CurrentImageFolderDisplayText
        {
            get => currentImageFolderDisplayText;
            private set => SetProperty(ref currentImageFolderDisplayText, value ?? string.Empty);
        }

        public bool IsOpenCurrentImageFolderEnabled
        {
            get => isOpenCurrentImageFolderEnabled;
            private set => SetProperty(ref isOpenCurrentImageFolderEnabled, value);
        }

        public bool IsAnomalyFolderStateSuggestionVisible
        {
            get => isAnomalyFolderStateSuggestionVisible;
            private set => SetProperty(ref isAnomalyFolderStateSuggestionVisible, value);
        }

        public string AnomalyFolderStateSuggestionText
        {
            get => anomalyFolderStateSuggestionText;
            private set => SetProperty(ref anomalyFolderStateSuggestionText, value ?? string.Empty);
        }

        public string AnomalyFolderStateSuggestionTitleText
        {
            get => anomalyFolderStateSuggestionTitleText;
            private set => SetProperty(ref anomalyFolderStateSuggestionTitleText, value ?? string.Empty);
        }

        public ICommand ApplyAnomalyFolderStateSuggestionCommand
        {
            get => applyAnomalyFolderStateSuggestionCommand;
            private set => SetProperty(ref applyAnomalyFolderStateSuggestionCommand, value);
        }

        public ICommand DismissAnomalyFolderStateSuggestionCommand
        {
            get => dismissAnomalyFolderStateSuggestionCommand;
            private set => SetProperty(ref dismissAnomalyFolderStateSuggestionCommand, value);
        }

        public string AnomalyFolderStateSuggestionApplyText
        {
            get => anomalyFolderStateSuggestionApplyText;
            private set => SetProperty(ref anomalyFolderStateSuggestionApplyText, value ?? string.Empty);
        }

        public bool IsAnomalyImageReviewMode
        {
            get => isAnomalyImageReviewMode;
            private set => SetProperty(ref isAnomalyImageReviewMode, value);
        }

        public System.Windows.Visibility AnomalyImageReviewVisibility
        {
            get => anomalyImageReviewVisibility;
            private set => SetProperty(ref anomalyImageReviewVisibility, value);
        }

        public System.Windows.Visibility StandardQueueWorkflowVisibility
        {
            get => standardQueueWorkflowVisibility;
            private set => SetProperty(ref standardQueueWorkflowVisibility, value);
        }

        public string QueueDecisionColumnHeaderText
        {
            get => queueDecisionColumnHeaderText;
            private set => SetProperty(ref queueDecisionColumnHeaderText, value ?? string.Empty);
        }

        public string QueueSecondaryColumnHeaderText
        {
            get => queueSecondaryColumnHeaderText;
            private set => SetProperty(ref queueSecondaryColumnHeaderText, value ?? string.Empty);
        }

        public ICommand MarkAnomalyNormalCommand
        {
            get => markAnomalyNormalCommand;
            private set => SetProperty(ref markAnomalyNormalCommand, value);
        }

        public ICommand MarkAnomalyAbnormalCommand
        {
            get => markAnomalyAbnormalCommand;
            private set => SetProperty(ref markAnomalyAbnormalCommand, value);
        }

        public ICommand ClearAnomalyReviewCommand
        {
            get => clearAnomalyReviewCommand;
            private set => SetProperty(ref clearAnomalyReviewCommand, value);
        }

        public string QueueFilterUnfinishedText
        {
            get => queueFilterUnfinishedText;
            private set
            {
                if (SetProperty(ref queueFilterUnfinishedText, value))
                {
                    OnPropertyChanged(nameof(QueueWorklistAutomationName));
                }
            }
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

        public bool IsQueueFilterUnfinishedActive
        {
            get => isQueueFilterUnfinishedActive;
            private set => SetProperty(ref isQueueFilterUnfinishedActive, value);
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
            Action openCurrentImageFolder,
            Action refreshImageQueue,
            Action nextUnlabeled,
            Action openSelectedQueueImage,
            Action detectSelectedQueue,
            Action batchDetectQueue,
            Action templateBatchQueue,
            Action retryFailedQueue,
            Action stopBatchQueue,
            Action queueFilterUnfinished,
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
            Action queueMouseDoubleClick,
            Action applyAnomalyFolderStateSuggestion,
            Action dismissAnomalyFolderStateSuggestion,
            Action markAnomalyNormal,
            Action markAnomalyAbnormal,
            Action clearAnomalyReview)
        {
            // Queue actions stay injected so the virtualized queue view does not relay UI events through code-behind.
            LoadImageRootCommand = new RelayCommand(loadImageRoot ?? NoOpCommand);
            BrowseImageFolderCommand = new RelayCommand(browseImageFolder ?? NoOpCommand);
            OpenCurrentImageFolderCommand = new RelayCommand(openCurrentImageFolder ?? NoOpCommand);
            RefreshImageQueueCommand = new RelayCommand(refreshImageQueue ?? NoOpCommand);
            NextUnlabeledCommand = new RelayCommand(nextUnlabeled ?? NoOpCommand);
            OpenSelectedQueueImageCommand = new RelayCommand(openSelectedQueueImage ?? NoOpCommand);
            DetectSelectedQueueCommand = new RelayCommand(detectSelectedQueue ?? NoOpCommand);
            BatchDetectQueueCommand = new RelayCommand(batchDetectQueue ?? NoOpCommand);
            TemplateBatchQueueCommand = new RelayCommand(templateBatchQueue ?? NoOpCommand);
            RetryFailedQueueCommand = new RelayCommand(retryFailedQueue ?? NoOpCommand);
            StopBatchQueueCommand = new RelayCommand(stopBatchQueue ?? NoOpCommand);
            QueueFilterUnfinishedCommand = new RelayCommand(queueFilterUnfinished ?? NoOpCommand);
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
            ApplyAnomalyFolderStateSuggestionCommand = new RelayCommand(applyAnomalyFolderStateSuggestion ?? NoOpCommand);
            DismissAnomalyFolderStateSuggestionCommand = new RelayCommand(dismissAnomalyFolderStateSuggestion ?? NoOpCommand);
            MarkAnomalyNormalCommand = new RelayCommand(markAnomalyNormal ?? NoOpCommand);
            MarkAnomalyAbnormalCommand = new RelayCommand(markAnomalyAbnormal ?? NoOpCommand);
            ClearAnomalyReviewCommand = new RelayCommand(clearAnomalyReview ?? NoOpCommand);
        }

        public void RefreshLocalizedPresentation(IEnumerable<WpfImageQueueItem> queueItems = null)
        {
            foreach (WpfImageQueueItem item in queueItems ?? Array.Empty<WpfImageQueueItem>())
            {
                item?.RefreshLocalizedPresentation();
            }

            QueueDecisionColumnHeaderText = IsAnomalyImageReviewMode
                ? T("WpfImageQueue.Decision.Anomaly")
                : T("WpfImageQueue.Decision.Standard");
            QueueSecondaryColumnHeaderText = IsAnomalyImageReviewMode
                ? T("WpfImageQueue.Secondary.Anomaly")
                : T("WpfImageQueue.Secondary.Standard");
            CurrentImageFolderDisplayText = FormatFolderDisplayPath(CurrentImageFolderPath);
            RefreshQuickFilterText();
            RefreshAnomalyFolderStateSuggestionText();
            OnPropertyChanged(string.Empty);
            RefreshCurrentImageTaskSummary();
        }

        public void SetAnomalyImageReviewMode(bool enabled)
        {
            IsAnomalyImageReviewMode = enabled;
            AnomalyImageReviewVisibility = enabled
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
            StandardQueueWorkflowVisibility = enabled
                ? System.Windows.Visibility.Collapsed
                : System.Windows.Visibility.Visible;
            QueueDecisionColumnHeaderText = enabled
                ? T("WpfImageQueue.Decision.Anomaly")
                : T("WpfImageQueue.Decision.Standard");
            QueueSecondaryColumnHeaderText = enabled
                ? T("WpfImageQueue.Secondary.Anomaly")
                : T("WpfImageQueue.Secondary.Standard");
            OnPropertyChanged(nameof(NextUnlabeledActionText));
            OnPropertyChanged(nameof(NextUnlabeledToolTip));
            OnPropertyChanged(nameof(QueueWorklistSummaryText));
            RefreshCurrentImageTaskSummary();
        }

        public void SetAnomalyFolderStateSuggestion(AnomalyImageReviewFolderImportResult suggestion)
        {
            int normalCount = suggestion?.NormalImageCount ?? 0;
            int abnormalCount = suggestion?.AbnormalImageCount ?? 0;
            if (normalCount <= 0 && abnormalCount <= 0)
            {
                ClearAnomalyFolderStateSuggestion();
                return;
            }

            int totalCount = normalCount
                + abnormalCount
                + (suggestion?.ExistingReviewCount ?? 0)
                + (suggestion?.UnmatchedImageCount ?? 0);
            anomalyNormalCount = normalCount;
            anomalyAbnormalCount = abnormalCount;
            anomalyTotalCount = totalCount;
            RefreshAnomalyFolderStateSuggestionText();
            IsAnomalyFolderStateSuggestionVisible = true;
        }

        public void ClearAnomalyFolderStateSuggestion()
        {
            IsAnomalyFolderStateSuggestionVisible = false;
            anomalyNormalCount = 0;
            anomalyAbnormalCount = 0;
            anomalyTotalCount = 0;
            AnomalyFolderStateSuggestionTitleText = T("WpfImageQueue.Anomaly.Suggestion.DefaultTitle");
            AnomalyFolderStateSuggestionText = string.Empty;
            AnomalyFolderStateSuggestionApplyText = T("WpfImageQueue.Anomaly.Suggestion.DefaultApply");
        }

        public void SetCurrentImageFolder(string folderPath, bool canOpenFolder)
        {
            string normalizedPath = string.IsNullOrWhiteSpace(folderPath) ? string.Empty : folderPath.Trim();
            CurrentImageFolderPath = normalizedPath;
            CurrentImageFolderDisplayText = FormatFolderDisplayPath(normalizedPath);
            IsOpenCurrentImageFolderEnabled = canOpenFolder && !string.IsNullOrWhiteSpace(normalizedPath);
        }

        public void SetSelectedImageAvailability(bool canOpenSelectedImage)
        {
            IsOpenSelectedImageEnabled = canOpenSelectedImage;
        }

        public void ApplyWorkflowCommandState(WorkflowCommandState state)
        {
            bool canRunInference = state?.CanRunInference == true;
            IsDetectSelectedEnabled = canRunInference;
            IsBatchDetectEnabled = canRunInference;
            IsTemplateBatchEnabled = state?.CanRunGeneralCommands == true;
            IsRetryFailedEnabled = canRunInference;
            IsStopBatchEnabled = state?.CanStopBatchDetection == true;
        }

        public void SetQuickFilterState(
            WpfImageQueueFilter selectedFilter,
            int candidateCount,
            int failedCount,
            int confirmedCount,
            int skippedCount,
            int noCandidateCount,
            int unfinishedCount = 0)
        {
            selectedFilterForPresentation = selectedFilter;
            unfinishedCountForPresentation = Math.Max(0, unfinishedCount);
            candidateCountForPresentation = Math.Max(0, candidateCount);
            failedCountForPresentation = Math.Max(0, failedCount);
            confirmedCountForPresentation = Math.Max(0, confirmedCount);
            skippedCountForPresentation = Math.Max(0, skippedCount);
            noCandidateCountForPresentation = Math.Max(0, noCandidateCount);
            RefreshQuickFilterText();

            IsQueueFilterUnfinishedActive = selectedFilter == WpfImageQueueFilter.Unlabeled;
            IsQueueFilterAllActive = selectedFilter == WpfImageQueueFilter.All;
            IsQueueFilterCandidateActive = selectedFilter == WpfImageQueueFilter.Candidate;
            IsQueueFilterFailedActive = selectedFilter == WpfImageQueueFilter.Failed;
            IsQueueFilterConfirmedActive = selectedFilter == WpfImageQueueFilter.Confirmed;
            IsQueueFilterSkippedActive = selectedFilter == WpfImageQueueFilter.Skipped;
            IsQueueFilterNoCandidateActive = selectedFilter == WpfImageQueueFilter.NoCandidate;
        }

        private void RefreshQuickFilterText()
        {
            QueueFilterUnfinishedText = Format("WpfImageQueue.Filter.Unfinished", unfinishedCountForPresentation);
            QueueFilterAllText = T("WpfImageQueue.Filter.All");
            QueueFilterCandidateText = Format("WpfImageQueue.Filter.Candidate", candidateCountForPresentation);
            QueueFilterFailedText = Format("WpfImageQueue.Filter.Failed", failedCountForPresentation);
            QueueFilterConfirmedText = Format("WpfImageQueue.Filter.Confirmed", confirmedCountForPresentation);
            QueueFilterSkippedText = Format("WpfImageQueue.Filter.Skipped", skippedCountForPresentation);
            QueueFilterNoCandidateText = Format("WpfImageQueue.Filter.NoCandidate", noCandidateCountForPresentation);
        }

        private void RefreshAnomalyFolderStateSuggestionText()
        {
            if (anomalyNormalCount <= 0 && anomalyAbnormalCount <= 0)
            {
                if (!IsAnomalyFolderStateSuggestionVisible)
                {
                    AnomalyFolderStateSuggestionTitleText = T("WpfImageQueue.Anomaly.Suggestion.DefaultTitle");
                    AnomalyFolderStateSuggestionText = string.Empty;
                    AnomalyFolderStateSuggestionApplyText = T("WpfImageQueue.Anomaly.Suggestion.DefaultApply");
                }

                return;
            }

            AnomalyFolderStateSuggestionTitleText = T("WpfImageQueue.Anomaly.Suggestion.FoundTitle");
            AnomalyFolderStateSuggestionText = Format(
                "WpfImageQueue.Anomaly.Suggestion.FoundText",
                anomalyNormalCount,
                anomalyAbnormalCount,
                anomalyTotalCount);
            AnomalyFolderStateSuggestionApplyText = Format(
                "WpfImageQueue.Anomaly.Suggestion.ApplyCount",
                anomalyNormalCount + anomalyAbnormalCount);
        }

        private void OnSelectedQueueItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e?.PropertyName, nameof(WpfImageQueueItem.QueueStatusSummary), StringComparison.Ordinal)
                || string.Equals(e?.PropertyName, nameof(WpfImageQueueItem.QueueBadgeText), StringComparison.Ordinal)
                || string.Equals(e?.PropertyName, nameof(WpfImageQueueItem.LabelStatus), StringComparison.Ordinal)
                || string.Equals(e?.PropertyName, nameof(WpfImageQueueItem.DetectStatus), StringComparison.Ordinal)
                || string.Equals(e?.PropertyName, nameof(WpfImageQueueItem.ReviewState), StringComparison.Ordinal)
                || string.Equals(e?.PropertyName, nameof(WpfImageQueueItem.AnomalyReviewState), StringComparison.Ordinal)
                || string.Equals(e?.PropertyName, nameof(WpfImageQueueItem.QualityReviewState), StringComparison.Ordinal)
                || string.Equals(e?.PropertyName, nameof(WpfImageQueueItem.IsLabeled), StringComparison.Ordinal)
                || string.Equals(e?.PropertyName, nameof(WpfImageQueueItem.IsSaveRequired), StringComparison.Ordinal)
                || string.Equals(e?.PropertyName, nameof(WpfImageQueueItem.FileName), StringComparison.Ordinal))
            {
                RefreshCurrentImageTaskSummary();
            }
        }

        private void RefreshCurrentImageTaskSummary()
        {
            if (OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.English)
            {
                RefreshCurrentImageTaskSummaryEnglish();
                return;
            }

            WpfImageQueueItem item = selectedQueueItem;
            if (item == null)
            {
                CurrentImageTaskTitleText = "\uC774\uBBF8\uC9C0 \uC120\uD0DD";
                CurrentImageTaskDetailText = IsAnomalyImageReviewMode
                    ? "목록에서 이미지를 선택한 뒤 이미지 전체를 정상(OK) 또는 이상(NG)으로 판정하세요."
                    : "\uC67C\uCABD \uBAA9\uB85D\uC5D0\uC11C \uC774\uBBF8\uC9C0\uB97C \uC120\uD0DD\uD558\uBA74 \uC800\uC7A5/\uAC80\uC0AC \uC0C1\uD0DC\uB97C \uBCF4\uC5EC\uC90D\uB2C8\uB2E4.";
                CurrentImageTaskBadgeText = "\uB300\uAE30";
                CurrentImageTaskKey = "Waiting";
                CurrentImageTaskToolTip = BuildCurrentImageTaskToolTip(
                    null,
                    CurrentImageTaskTitleText,
                    CurrentImageTaskDetailText,
                    "\uC774\uBBF8\uC9C0\uB97C \uC120\uD0DD\uD558\uBA74 \uD604\uC7AC \uC791\uC5C5 \uC0C1\uD0DC\uAC00 \uD45C\uC2DC\uB429\uB2C8\uB2E4.");
                return;
            }

            if (IsAnomalyImageReviewMode)
            {
                CurrentImageTaskTitleText = "현재 이미지 OK/NG 판정";
                switch (item.AnomalyReviewState)
                {
                    case AnomalyImageReviewState.Normal:
                        CurrentImageTaskDetailText = "이미지 전체를 정상(OK)으로 저장했습니다. 필요하면 NG로 다시 판정할 수 있습니다.";
                        CurrentImageTaskBadgeText = "OK";
                        CurrentImageTaskKey = "AnomalyNormal";
                        break;
                    case AnomalyImageReviewState.Abnormal:
                        CurrentImageTaskDetailText = "이미지 전체를 이상(NG)으로 저장했습니다. 필요하면 OK로 다시 판정할 수 있습니다.";
                        CurrentImageTaskBadgeText = "NG";
                        CurrentImageTaskKey = "AnomalyAbnormal";
                        break;
                    default:
                        CurrentImageTaskDetailText = "결함 위치를 그리지 않습니다. 이미지 전체가 정상인지 이상인지 판정하세요.";
                        CurrentImageTaskBadgeText = "미판정";
                        CurrentImageTaskKey = "AnomalyUnreviewed";
                        break;
                }

                CurrentImageTaskToolTip = BuildCurrentImageTaskToolTip(
                    item.FileName,
                    CurrentImageTaskTitleText,
                    CurrentImageTaskDetailText,
                    item.QueueStatusSummary);
                return;
            }

            string labelStatus = string.IsNullOrWhiteSpace(item.LabelStatus) ? "\uC5C6\uC74C" : item.LabelStatus;
            string detectStatus = string.IsNullOrWhiteSpace(item.DetectStatus) ? "\uB300\uAE30" : item.DetectStatus;
            string statusSummary = string.IsNullOrWhiteSpace(item.QueueStatusSummary)
                ? $"\uC800\uC7A5 {labelStatus} / \uAC80\uC0AC {detectStatus}"
                : item.QueueStatusSummary;

            if (item.IsSaveRequired)
            {
                CurrentImageTaskTitleText = "\uB77C\uBCA8 \uC800\uC7A5 \uD544\uC694";
                CurrentImageTaskDetailText = statusSummary;
                CurrentImageTaskBadgeText = string.IsNullOrWhiteSpace(item.QueueBadgeText)
                    ? "\uC800\uC7A5 \uD544\uC694"
                    : item.QueueBadgeText;
                CurrentImageTaskKey = "SaveRequired";
                CurrentImageTaskToolTip = BuildCurrentImageTaskToolTip(
                    item.FileName,
                    CurrentImageTaskTitleText,
                    CurrentImageTaskDetailText,
                    statusSummary);
                return;
            }

            if (item.QualityReviewState == Yolo.YoloImageQualityReviewState.NeedsFix)
            {
                CurrentImageTaskTitleText = "라벨 수정 필요";
                CurrentImageTaskDetailText = "저장 라벨을 확인하고 수정한 뒤 라벨 저장 후 검수 완료로 변경하세요.";
                CurrentImageTaskBadgeText = "수정 필요";
                CurrentImageTaskKey = "NeedsFix";
                CurrentImageTaskToolTip = BuildCurrentImageTaskToolTip(
                    item.FileName,
                    CurrentImageTaskTitleText,
                    CurrentImageTaskDetailText,
                    statusSummary);
                return;
            }

            if (item.QualityReviewState == Yolo.YoloImageQualityReviewState.Reviewed)
            {
                CurrentImageTaskTitleText = "품질 검수 완료";
                CurrentImageTaskDetailText = "현재 저장 라벨이 검수 완료된 이미지입니다.";
                CurrentImageTaskBadgeText = "검수 완료";
                CurrentImageTaskKey = "QualityReviewed";
                CurrentImageTaskToolTip = BuildCurrentImageTaskToolTip(
                    item.FileName,
                    CurrentImageTaskTitleText,
                    CurrentImageTaskDetailText,
                    statusSummary);
                return;
            }

            switch (item.ReviewState)
            {
                case Yolo.YoloImageReviewState.Requested:
                    CurrentImageTaskTitleText = "\uAC80\uC0AC \uC9C4\uD589 \uC911";
                    CurrentImageTaskDetailText = "\uAC80\uC0AC \uACB0\uACFC\uB97C \uAE30\uB2E4\uB9AC\uB294 \uC774\uBBF8\uC9C0\uC785\uB2C8\uB2E4.";
                    CurrentImageTaskBadgeText = "\uAC80\uC0AC\uC911";
                    CurrentImageTaskKey = "Requested";
                    break;
                case Yolo.YoloImageReviewState.Candidate:
                    CurrentImageTaskTitleText = "AI \uD6C4\uBCF4 \uAC80\uD1A0";
                    CurrentImageTaskDetailText = "\uD6C4\uBCF4\uB97C \uD655\uC815\uD558\uAC70\uB098 \uC228\uAE30\uC138\uC694. \uD655\uC815\uD558\uBA74 \uC800\uC7A5 \uB77C\uBCA8\uC5D0 \uC790\uB3D9 \uBC18\uC601\uB429\uB2C8\uB2E4.";
                    CurrentImageTaskBadgeText = string.IsNullOrWhiteSpace(item.QueueBadgeText) ? "AI" : item.QueueBadgeText;
                    CurrentImageTaskKey = "Candidate";
                    break;
                case Yolo.YoloImageReviewState.Failed:
                    CurrentImageTaskTitleText = "\uAC80\uC0AC \uC2E4\uD328";
                    CurrentImageTaskDetailText = statusSummary;
                    CurrentImageTaskBadgeText = "\uC2E4\uD328";
                    CurrentImageTaskKey = "Failed";
                    break;
                case Yolo.YoloImageReviewState.Confirmed:
                    CurrentImageTaskTitleText = "\uB77C\uBCA8 \uC800\uC7A5 \uC644\uB8CC";
                    CurrentImageTaskDetailText = "\uB2E4\uC74C \uBBF8\uC644\uB8CC\uB85C \uC774\uB3D9\uD558\uAC70\uB098, \uD544\uC694\uD558\uBA74 \uB2E4\uC2DC \uC5F4\uC5B4 \uC218\uC815\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.";
                    CurrentImageTaskBadgeText = "\uC800\uC7A5";
                    CurrentImageTaskKey = "Saved";
                    break;
                case Yolo.YoloImageReviewState.NoCandidate:
                    CurrentImageTaskTitleText = "\uAC1D\uCCB4 \uC5C6\uC74C \uC644\uB8CC";
                    CurrentImageTaskDetailText = "\uAC1D\uCCB4 \uC5C6\uC74C\uC73C\uB85C \uC800\uC7A5\uB428. \uB2E4\uC74C \uBBF8\uC644\uB8CC\uB85C \uC774\uB3D9\uD558\uAC70\uB098 \uB2E4\uC2DC \uC5F4\uC5B4 \uC218\uC815\uD558\uC138\uC694.";
                    CurrentImageTaskBadgeText = "\uAC1D\uCCB4\uC5C6\uC74C";
                    CurrentImageTaskKey = "Saved";
                    break;
                case Yolo.YoloImageReviewState.Skipped:
                    CurrentImageTaskTitleText = "\uD6C4\uBCF4 \uC228\uAE40";
                    CurrentImageTaskDetailText = "AI \uD6C4\uBCF4\uB97C \uC228\uAE34 \uC0C1\uD0DC\uC785\uB2C8\uB2E4. \uD544\uC694\uD558\uBA74 \uB2E4\uC2DC \uAC80\uD1A0\uD558\uC138\uC694.";
                    CurrentImageTaskBadgeText = "\uC228\uAE40";
                    CurrentImageTaskKey = "Skipped";
                    break;
                default:
                    if (item.IsLabeled)
                    {
                        CurrentImageTaskTitleText = "\uC800\uC7A5 \uB77C\uBCA8 \uC788\uC74C";
                        CurrentImageTaskDetailText = $"\uC800\uC7A5 {labelStatus} / \uAC80\uC0AC {detectStatus}. \uB2E4\uC74C \uBBF8\uC644\uB8CC\uB85C \uC774\uB3D9\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.";
                        CurrentImageTaskBadgeText = "\uC800\uC7A5";
                        CurrentImageTaskKey = "Saved";
                    }
                    else
                    {
                        CurrentImageTaskTitleText = "\uB77C\uBCA8 \uC791\uC5C5 \uD544\uC694";
                        CurrentImageTaskDetailText = "\uB77C\uBCA8\uC744 \uB9CC\uB4E0 \uB4A4 \uB77C\uBCA8 \uC800\uC7A5, \uB610\uB294 \uAC1D\uCCB4 \uC5C6\uC74C\uC744 \uC120\uD0DD\uD558\uC138\uC694.";
                        CurrentImageTaskBadgeText = "\uC791\uC5C5";
                        CurrentImageTaskKey = "NeedsLabel";
                    }

                    break;
            }

            CurrentImageTaskToolTip = BuildCurrentImageTaskToolTip(
                item.FileName,
                CurrentImageTaskTitleText,
                CurrentImageTaskDetailText,
                statusSummary);
        }

        private void RefreshCurrentImageTaskSummaryEnglish()
        {
            WpfImageQueueItem item = selectedQueueItem;
            if (item == null)
            {
                CurrentImageTaskTitleText = T("WpfImageQueue.Current.Waiting.Title");
                CurrentImageTaskDetailText = IsAnomalyImageReviewMode
                    ? T("WpfImageQueue.Current.Waiting.AnomalyDetail")
                    : T("WpfImageQueue.Current.Waiting.StandardDetail");
                CurrentImageTaskBadgeText = T("WpfImageQueue.Current.Waiting.Badge");
                CurrentImageTaskKey = "Waiting";
                CurrentImageTaskToolTip = BuildCurrentImageTaskToolTip(
                    null,
                    CurrentImageTaskTitleText,
                    CurrentImageTaskDetailText,
                    T("WpfImageQueue.Current.Waiting.ToolTip"));
                return;
            }

            if (IsAnomalyImageReviewMode)
            {
                CurrentImageTaskTitleText = T("WpfImageQueue.Current.Anomaly.Title");
                switch (item.AnomalyReviewState)
                {
                    case AnomalyImageReviewState.Normal:
                        CurrentImageTaskDetailText = T("WpfImageQueue.Current.Anomaly.NormalDetail");
                        CurrentImageTaskBadgeText = "OK";
                        CurrentImageTaskKey = "AnomalyNormal";
                        break;
                    case AnomalyImageReviewState.Abnormal:
                        CurrentImageTaskDetailText = T("WpfImageQueue.Current.Anomaly.AbnormalDetail");
                        CurrentImageTaskBadgeText = "NG";
                        CurrentImageTaskKey = "AnomalyAbnormal";
                        break;
                    default:
                        CurrentImageTaskDetailText = T("WpfImageQueue.Current.Anomaly.UnreviewedDetail");
                        CurrentImageTaskBadgeText = T("WpfImageQueue.Current.Anomaly.UnreviewedBadge");
                        CurrentImageTaskKey = "AnomalyUnreviewed";
                        break;
                }

                CurrentImageTaskToolTip = BuildCurrentImageTaskToolTip(
                    item.FileName,
                    CurrentImageTaskTitleText,
                    CurrentImageTaskDetailText,
                    item.LocalizedQueueStatusSummary);
                return;
            }

            string labelStatus = item.LocalizedLabelStatus;
            string detectStatus = item.LocalizedDetectStatus;
            string statusSummary = string.IsNullOrWhiteSpace(item.LocalizedQueueStatusSummary)
                ? string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}: {1} / {2}: {3}",
                    T("WpfImageQueue.Row.SavePrefix"),
                    labelStatus,
                    T("WpfImageQueue.Row.InspectPrefix"),
                    detectStatus)
                : item.LocalizedQueueStatusSummary;

            if (item.IsSaveRequired)
            {
                CurrentImageTaskTitleText = T("WpfImageQueue.Current.SaveRequired.Title");
                CurrentImageTaskDetailText = statusSummary;
                CurrentImageTaskBadgeText = string.IsNullOrWhiteSpace(item.LocalizedQueueBadgeText)
                    ? T("WpfImageQueue.Current.SaveRequired.Badge")
                    : item.LocalizedQueueBadgeText;
                CurrentImageTaskKey = "SaveRequired";
            }
            else if (item.QualityReviewState == Yolo.YoloImageQualityReviewState.NeedsFix)
            {
                CurrentImageTaskTitleText = T("WpfImageQueue.Current.NeedsFix.Title");
                CurrentImageTaskDetailText = T("WpfImageQueue.Current.NeedsFix.Detail");
                CurrentImageTaskBadgeText = T("WpfImageQueue.Current.NeedsFix.Badge");
                CurrentImageTaskKey = "NeedsFix";
            }
            else if (item.QualityReviewState == Yolo.YoloImageQualityReviewState.Reviewed)
            {
                CurrentImageTaskTitleText = T("WpfImageQueue.Current.QualityReviewed.Title");
                CurrentImageTaskDetailText = T("WpfImageQueue.Current.QualityReviewed.Detail");
                CurrentImageTaskBadgeText = T("WpfImageQueue.Current.QualityReviewed.Badge");
                CurrentImageTaskKey = "QualityReviewed";
            }
            else
            {
                switch (item.ReviewState)
                {
                    case Yolo.YoloImageReviewState.Requested:
                        CurrentImageTaskTitleText = T("WpfImageQueue.Current.Requested.Title");
                        CurrentImageTaskDetailText = T("WpfImageQueue.Current.Requested.Detail");
                        CurrentImageTaskBadgeText = T("WpfImageQueue.Current.Requested.Badge");
                        CurrentImageTaskKey = "Requested";
                        break;
                    case Yolo.YoloImageReviewState.Candidate:
                        CurrentImageTaskTitleText = T("WpfImageQueue.Current.Candidate.Title");
                        CurrentImageTaskDetailText = T("WpfImageQueue.Current.Candidate.Detail");
                        CurrentImageTaskBadgeText = string.IsNullOrWhiteSpace(item.LocalizedQueueBadgeText)
                            ? "AI"
                            : item.LocalizedQueueBadgeText;
                        CurrentImageTaskKey = "Candidate";
                        break;
                    case Yolo.YoloImageReviewState.Failed:
                        CurrentImageTaskTitleText = T("WpfImageQueue.Current.Failed.Title");
                        CurrentImageTaskDetailText = statusSummary;
                        CurrentImageTaskBadgeText = T("WpfImageQueue.Current.Failed.Badge");
                        CurrentImageTaskKey = "Failed";
                        break;
                    case Yolo.YoloImageReviewState.Confirmed:
                        CurrentImageTaskTitleText = T("WpfImageQueue.Current.Saved.Title");
                        CurrentImageTaskDetailText = T("WpfImageQueue.Current.Saved.Detail");
                        CurrentImageTaskBadgeText = T("WpfImageQueue.Current.Saved.Badge");
                        CurrentImageTaskKey = "Saved";
                        break;
                    case Yolo.YoloImageReviewState.NoCandidate:
                        CurrentImageTaskTitleText = T("WpfImageQueue.Current.NoCandidate.Title");
                        CurrentImageTaskDetailText = T("WpfImageQueue.Current.NoCandidate.Detail");
                        CurrentImageTaskBadgeText = T("WpfImageQueue.Current.NoCandidate.Badge");
                        CurrentImageTaskKey = "Saved";
                        break;
                    case Yolo.YoloImageReviewState.Skipped:
                        CurrentImageTaskTitleText = T("WpfImageQueue.Current.Skipped.Title");
                        CurrentImageTaskDetailText = T("WpfImageQueue.Current.Skipped.Detail");
                        CurrentImageTaskBadgeText = T("WpfImageQueue.Current.Skipped.Badge");
                        CurrentImageTaskKey = "Skipped";
                        break;
                    default:
                        if (item.IsLabeled)
                        {
                            CurrentImageTaskTitleText = T("WpfImageQueue.Current.Saved.Title");
                            CurrentImageTaskDetailText = statusSummary;
                            CurrentImageTaskBadgeText = T("WpfImageQueue.Current.Saved.Badge");
                            CurrentImageTaskKey = "Saved";
                        }
                        else
                        {
                            CurrentImageTaskTitleText = T("WpfImageQueue.Current.NeedsLabel.Title");
                            CurrentImageTaskDetailText = T("WpfImageQueue.Current.NeedsLabel.Detail");
                            CurrentImageTaskBadgeText = T("WpfImageQueue.Current.NeedsLabel.Badge");
                            CurrentImageTaskKey = "NeedsLabel";
                        }

                        break;
                }
            }

            CurrentImageTaskToolTip = BuildCurrentImageTaskToolTip(
                item.FileName,
                CurrentImageTaskTitleText,
                CurrentImageTaskDetailText,
                statusSummary);
        }

        private static string BuildCurrentImageTaskToolTip(
            string fileName,
            string title,
            string detail,
            string statusSummary)
        {
            string normalizedTitle = string.IsNullOrWhiteSpace(title) ? string.Empty : title.Trim();
            string normalizedDetail = string.IsNullOrWhiteSpace(detail) ? string.Empty : detail.Trim();
            string normalizedStatus = string.IsNullOrWhiteSpace(statusSummary) ? string.Empty : statusSummary.Trim();
            string normalizedFileName = string.IsNullOrWhiteSpace(fileName) ? string.Empty : fileName.Trim();
            string text = string.IsNullOrWhiteSpace(normalizedFileName)
                ? normalizedTitle
                : $"{normalizedFileName}{Environment.NewLine}{normalizedTitle}";

            if (!string.IsNullOrWhiteSpace(normalizedDetail)
                && !string.Equals(normalizedDetail, normalizedTitle, StringComparison.Ordinal))
            {
                text = string.IsNullOrWhiteSpace(text)
                    ? normalizedDetail
                    : $"{text}{Environment.NewLine}{normalizedDetail}";
            }

            if (!string.IsNullOrWhiteSpace(normalizedStatus)
                && !string.Equals(normalizedStatus, normalizedDetail, StringComparison.Ordinal)
                && !string.Equals(normalizedStatus, normalizedTitle, StringComparison.Ordinal))
            {
                text = string.IsNullOrWhiteSpace(text)
                    ? normalizedStatus
                    : string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}{1}{2}: {3}",
                        text,
                        Environment.NewLine,
                        T("WpfImageQueue.Current.StatusPrefix"),
                        normalizedStatus);
            }

            return text;
        }

        private static string FormatFolderDisplayPath(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return T("WpfImageQueue.Folder.Empty");
            }

            const int MaximumVisibleCharacters = 54;
            if (folderPath.Length <= MaximumVisibleCharacters)
            {
                return folderPath;
            }

            try
            {
                string root = Path.GetPathRoot(folderPath) ?? string.Empty;
                string relativePath = folderPath.Substring(root.Length);
                string[] segments = relativePath
                    .Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length <= 3)
                {
                    return folderPath;
                }

                string separator = Path.DirectorySeparatorChar.ToString();
                string tail = string.Join(separator, segments.Skip(Math.Max(0, segments.Length - 3)));
                return $"{root}...{separator}{tail}";
            }
            catch (ArgumentException)
            {
                return folderPath;
            }
        }

        private static string T(string key) => OpenVisionLanguageService.T(key);

        private static string Format(string key, params object[] arguments)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                T(key),
                arguments ?? Array.Empty<object>());
        }
    }
}
