using OpenVisionLab.Mvvm;
using System;
using System.ComponentModel;
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
        private string queueFilterUnfinishedText = WpfImageQueuePresenter.FormatWorklistActionText(0);
        private string queueFilterAllText = "\uC804\uCCB4";
        private string queueFilterCandidateText = WpfImageQueuePresenter.FormatQuickFilterText("AI \uD6C4\uBCF4", 0);
        private string queueFilterFailedText = WpfImageQueuePresenter.FormatQuickFilterText("\uC2E4\uD328", 0);
        private string queueFilterConfirmedText = WpfImageQueuePresenter.FormatQuickFilterText("\uC800\uC7A5\uB428", 0);
        private string queueFilterSkippedText = WpfImageQueuePresenter.FormatQuickFilterText("\uC228\uAE40", 0);
        private string queueFilterNoCandidateText = WpfImageQueuePresenter.FormatQuickFilterText("\uAC1D\uCCB4\uC5C6\uC74C", 0);
        private bool isQueueFilterUnfinishedActive;
        private bool isQueueFilterAllActive = true;
        private bool isQueueFilterCandidateActive;
        private bool isQueueFilterFailedActive;
        private bool isQueueFilterConfirmedActive;
        private bool isQueueFilterSkippedActive;
        private bool isQueueFilterNoCandidateActive;
        private WpfImageQueueItem selectedQueueItem;
        private string currentImageTaskTitleText = "\uC774\uBBF8\uC9C0 \uC120\uD0DD";
        private string currentImageTaskDetailText = "\uC67C\uCABD \uBAA9\uB85D\uC5D0\uC11C \uC774\uBBF8\uC9C0\uB97C \uC120\uD0DD\uD558\uBA74 \uC800\uC7A5/\uAC80\uC0AC \uC0C1\uD0DC\uB97C \uBCF4\uC5EC\uC90D\uB2C8\uB2E4.";
        private string currentImageTaskBadgeText = "\uB300\uAE30";
        private string currentImageTaskKey = "Waiting";
        private string currentImageTaskToolTip = "\uC774\uBBF8\uC9C0\uB97C \uC120\uD0DD\uD558\uBA74 \uD604\uC7AC \uC791\uC5C5 \uC0C1\uD0DC\uAC00 \uD45C\uC2DC\uB429\uB2C8\uB2E4.";
        private Action<WpfImageQueueItem> selectedQueueItemChanged = NoOpQueueItemCommand;
        private string currentImageFolderPath = string.Empty;
        private string currentImageFolderDisplayText = "이미지 폴더를 선택하세요.";
        private bool isOpenCurrentImageFolderEnabled;
        private bool isAnomalyFolderStateSuggestionVisible;
        private string anomalyFolderStateSuggestionTitleText = "폴더명으로 초기 판정을 제안합니다";
        private string anomalyFolderStateSuggestionText = string.Empty;
        private string anomalyFolderStateSuggestionApplyText = "일괄 판정";
        private bool isAnomalyImageReviewMode;
        private System.Windows.Visibility anomalyImageReviewVisibility = System.Windows.Visibility.Collapsed;
        private System.Windows.Visibility standardQueueWorkflowVisibility = System.Windows.Visibility.Visible;
        private string queueDecisionColumnHeaderText = "저장";
        private string queueSecondaryColumnHeaderText = "검사";
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

        public string QueueWorklistTitleText => "확인 필요 Worklist";

        public string QueueWorklistSummaryText => IsAnomalyImageReviewMode
            ? "미판정 OK/NG 이미지만 모아 순서대로 확인"
            : "미판정 · 저장 필요 · AI 후보 · 검사 실패 · 수정 필요";

        public string QueueWorklistAutomationName => $"{QueueWorklistTitleText} / {QueueFilterUnfinishedText}";

        public string NextUnlabeledActionText => IsAnomalyImageReviewMode ? "다음 미판정" : "\uB2E4\uC74C \uBBF8\uC644\uB8CC";

        public string NextUnlabeledToolTip => IsAnomalyImageReviewMode
            ? "정상(OK) 또는 이상(NG) 판정이 없는 다음 이미지를 엽니다."
            : "\uC800\uC7A5\uB428/\uAC1D\uCCB4\uC5C6\uC74C \uC774\uBBF8\uC9C0\uB294 \uAC74\uB108\uB6F0\uACE0 \uB77C\uBCA8\uC774 \uD544\uC694\uD55C \uB2E4\uC74C \uC774\uBBF8\uC9C0\uB97C \uC5FD\uB2C8\uB2E4.";

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

        public void SetAnomalyImageReviewMode(bool enabled)
        {
            IsAnomalyImageReviewMode = enabled;
            AnomalyImageReviewVisibility = enabled
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
            StandardQueueWorkflowVisibility = enabled
                ? System.Windows.Visibility.Collapsed
                : System.Windows.Visibility.Visible;
            QueueDecisionColumnHeaderText = enabled ? "판정" : "저장";
            QueueSecondaryColumnHeaderText = enabled ? "상태" : "검사";
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
            AnomalyFolderStateSuggestionTitleText = "OK/NG 폴더 구조를 발견했습니다";
            AnomalyFolderStateSuggestionText = $"OK/normal {normalCount}장 → 정상 · NG/abnormal {abnormalCount}장 → 이상. 총 {totalCount}장 중 아직 판정하지 않은 이미지만 적용합니다.";
            AnomalyFolderStateSuggestionApplyText = $"{normalCount + abnormalCount}장 일괄 판정";
            IsAnomalyFolderStateSuggestionVisible = true;
        }

        public void ClearAnomalyFolderStateSuggestion()
        {
            IsAnomalyFolderStateSuggestionVisible = false;
            AnomalyFolderStateSuggestionTitleText = "폴더명으로 초기 판정을 제안합니다";
            AnomalyFolderStateSuggestionText = string.Empty;
            AnomalyFolderStateSuggestionApplyText = "일괄 판정";
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

        public void ApplyWorkflowCommandState(WpfWorkflowCommandState state)
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
            QueueFilterUnfinishedText = WpfImageQueuePresenter.FormatWorklistActionText(unfinishedCount);
            QueueFilterAllText = "\uC804\uCCB4";
            QueueFilterCandidateText = WpfImageQueuePresenter.FormatQuickFilterText("AI \uD6C4\uBCF4", candidateCount);
            QueueFilterFailedText = WpfImageQueuePresenter.FormatQuickFilterText("\uC2E4\uD328", failedCount);
            QueueFilterConfirmedText = WpfImageQueuePresenter.FormatQuickFilterText("\uC800\uC7A5\uB428", confirmedCount);
            QueueFilterSkippedText = WpfImageQueuePresenter.FormatQuickFilterText("\uC228\uAE40", skippedCount);
            QueueFilterNoCandidateText = WpfImageQueuePresenter.FormatQuickFilterText("\uAC1D\uCCB4\uC5C6\uC74C", noCandidateCount);

            IsQueueFilterUnfinishedActive = selectedFilter == WpfImageQueueFilter.Unlabeled;
            IsQueueFilterAllActive = selectedFilter == WpfImageQueueFilter.All;
            IsQueueFilterCandidateActive = selectedFilter == WpfImageQueueFilter.Candidate;
            IsQueueFilterFailedActive = selectedFilter == WpfImageQueueFilter.Failed;
            IsQueueFilterConfirmedActive = selectedFilter == WpfImageQueueFilter.Confirmed;
            IsQueueFilterSkippedActive = selectedFilter == WpfImageQueueFilter.Skipped;
            IsQueueFilterNoCandidateActive = selectedFilter == WpfImageQueueFilter.NoCandidate;
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
                    : $"{text}{Environment.NewLine}\uC0C1\uD0DC: {normalizedStatus}";
            }

            return text;
        }

        private static string FormatFolderDisplayPath(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return "이미지 폴더를 선택하세요.";
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
    }
}
