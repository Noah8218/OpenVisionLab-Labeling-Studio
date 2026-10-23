using MvcVisionSystem._1._Core;
using OpenVisionLab;
using OpenVisionLab.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Windows.Data;

namespace MvcVisionSystem
{
    public sealed class WpfImageQueuePanelViewModel : WpfObservableViewModel, IDisposable, IComparer
    {
        private static readonly Action NoOpCommand = () => { };
        private static readonly Action<object> NoOpSelectionCommand = _ => { };
        private static readonly Action<string> NoOpTextCommand = _ => { };
        private static readonly Action NoOpMouseCommand = () => { };
        private static readonly Action<WpfImageQueueItem> NoOpQueueItemCommand = _ => { };
        private static readonly Action<WpfImageQueueFilter> NoOpFilterCommand = _ => { };
        private static readonly WpfImageQueueFilter[] LabelingFilterOrder = { WpfImageQueueFilter.All, WpfImageQueueFilter.Unlabeled, WpfImageQueueFilter.ReviewPending, WpfImageQueueFilter.Reviewed, WpfImageQueueFilter.NeedsFix, WpfImageQueueFilter.Candidate, WpfImageQueueFilter.LabelSuspicion };
        private ImageQueueSummary labelingFilterSummary = new ImageQueueSummary();
        private int labelingCandidateCount;
        private int labelingSuspicionCount;
        private bool isInferencePresentation;
        private int inferenceFilterIndex;
        private int inferenceCompletedCount;
        private int inferenceImageCount;
        private string inferenceModelCaption = string.Empty;
        private Func<string, InferenceResultReviewEntry> inferenceResultProvider;
        private Func<InferenceResultReviewEntry, ModelDecisionResult> inferenceDecisionProvider;
        private Action refreshInferenceView;
        private bool isOpenSelectedImageEnabled;
        private Action<ImageQueueSort> candidateSortChanged;
        private Func<bool> canGenerateCandidates;
        private ImageQueueSortOption selectedCandidateSort;
        private IReadOnlyList<ImageQueueSortOption> candidateSortOptions = Array.Empty<ImageQueueSortOption>();
        private bool candidateWorkflowVisible;
        private string candidateSortUnavailableText = string.Empty;
        private ICommand generateCandidatesCommand = new RelayCommand(NoOpCommand, () => false);
        private bool isCandidateBatchRunning;
        private CandidateBatchSummary candidateBatchSummary;
        private IReadOnlyList<WpfImageQueueFilterOption> queueFilterOptions = WpfImageQueueFilterOption.CreateDefaults()
            .Where(option => option.Filter != WpfImageQueueFilter.Candidate && option.Filter != WpfImageQueueFilter.LabelSuspicion).ToArray();
        private AnomalyImageReviewState candidateAnomalyPredictionState;
        private double? candidateAnomalyScore;
        private double? candidateAnomalyThreshold;
        private Action<AnomalyImageReviewState> applyCandidateAnomaly;
        private Func<bool> canApplyCandidateAnomaly;
        private ICommand applyCandidateAnomalyCommand = new RelayCommand(NoOpCommand, () => false);

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
        private string batchStatusText = T("WpfImageQueue.Batch.Waiting");
        private int batchProgressMaximum = 1;
        private int batchProgressValue;
        private WpfImageQueueFilter selectedFilter = WpfImageQueueFilter.All;
        private string searchText = string.Empty;
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
        private WpfImageQueueItem activeQueueItem;
        private bool isSelectedImagePathVisible;
        private string currentImageTaskTitleText = T("WpfImageQueue.Current.Waiting.Title");
        private string currentImageTaskDetailText = T("WpfImageQueue.Current.Waiting.StandardDetail");
        private string currentImageTaskBadgeText = T("WpfImageQueue.Current.Waiting.Badge");
        private string currentImageTaskKey = "Waiting";
        private string currentImageTaskToolTip = T("WpfImageQueue.Current.Waiting.ToolTip");
        private Action<WpfImageQueueItem> selectedQueueItemChanged = NoOpQueueItemCommand;
        private Action<WpfImageQueueFilter> filterChanged = NoOpFilterCommand;
        private Action<string> searchTextChanged = NoOpTextCommand;
        private readonly RelayCommand clearSearchCommand;
        private readonly RelayCommand resetQueueFiltersCommand;
        private string currentImageFolderPath = string.Empty;
        private string currentImageFolderDisplayText = T("WpfImageQueue.Folder.Empty");
        private int datasetImageCount;
        private int datasetLabeledImageCount;
        private int datasetReviewedImageCount;
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
        private ICommand queueExplicitOpenCommand = new RelayCommand(NoOpCommand);
        private ICommand queueMouseDoubleClickCommand = new RelayCommand(NoOpMouseCommand);
        private ICommand applyAnomalyFolderStateSuggestionCommand = new RelayCommand(NoOpCommand);
        private ICommand dismissAnomalyFolderStateSuggestionCommand = new RelayCommand(NoOpCommand);
        private ICommand markAnomalyNormalCommand = new RelayCommand(NoOpCommand);
        private ICommand markAnomalyAbnormalCommand = new RelayCommand(NoOpCommand);
        private ICommand clearAnomalyReviewCommand = new RelayCommand(NoOpCommand);
        private readonly System.Windows.Threading.DispatcherTimer anomalyNoticeTimer;
        private System.Collections.IEnumerable anomalyGridItems = Array.Empty<WpfImageQueueItem>();
        private IReadOnlyList<WpfImageQueueItem> anomalyGridSelection = Array.Empty<WpfImageQueueItem>();
        private bool isAnomalyGridActive;
        private bool isAnomalyDecisionAvailable;
        private bool isAnomalySingleDecisionEnabled;
        private bool isUndoAnomalyDecisionEnabled;
        private bool isAnomalyDecisionNoticeVisible;
        private bool anomalyNoticeOffersUndo;
        private string anomalyNoticeKind = string.Empty;
        private string anomalyNoticeFileName = string.Empty;
        private AnomalyImageReviewState anomalyNoticeState;
        private int anomalyNoticeCount;
        private ICommand toggleAnomalyGridCommand = new RelayCommand(NoOpCommand);
        private ICommand markAnomalySelectionNormalCommand = new RelayCommand(NoOpCommand);
        private ICommand markAnomalySelectionAbnormalCommand = new RelayCommand(NoOpCommand);
        private ICommand undoAnomalyDecisionCommand = new RelayCommand(NoOpCommand);
        private ICommand openAnomalyGridImageCommand = new RelayCommand(NoOpCommand);
        private readonly RelayCommand<object> anomalyGridSelectionChangedCommand;
        private AnomalyImageReviewSummary anomalyDecisionSummary;
        private string anomalyPredictionClassName = string.Empty;
        private double anomalyPredictionConfidence;
        private AnomalyImageReviewState anomalyPredictionReviewState;
        private bool isAnomalyAutoAdvanceAfterDecision = true;
        private bool isRestoringAnomalyAutoAdvance;
        private Action<bool> anomalyAutoAdvanceChanged = _ => { };

        public WpfImageQueuePanelViewModel()
        {
            clearSearchCommand = new RelayCommand(() => ExecuteSearchTextChanged(string.Empty), () => HasSearchText);
            resetQueueFiltersCommand = new RelayCommand(ExecuteResetQueueFilters, () => HasQueueFilter);
            anomalyGridSelectionChangedCommand = new RelayCommand<object>(SetAnomalyGridSelection);
            anomalyNoticeTimer = new System.Windows.Threading.DispatcherTimer();
            anomalyNoticeTimer.Tick += OnAnomalyNoticeElapsed;
        }

        public bool IsInferencePresentation => isInferencePresentation;
        public string InferenceFilterAllText => T("WpfImageQueue.Filter.All");
        public string InferenceFilterUnexecutedText => T("WpfInference.Workspace.NotRun");
        public string InferenceFilterNgText => IsAnomalyImageReviewMode ? T("WpfAnomalyGrid.AbnormalState") : "NG";
        public string InferenceFilterOkText => IsAnomalyImageReviewMode ? T("WpfAnomalyGrid.NormalState") : "OK";
        public int InferenceFilterIndex
        {
            get => inferenceFilterIndex;
            set { if (SetProperty(ref inferenceFilterIndex, Math.Clamp(value, 0, 3))) refreshInferenceView?.Invoke(); }
        }
        public string InferenceQueueSummaryText => Format("WpfInference.Queue.Progress", inferenceCompletedCount, inferenceImageCount, inferenceModelCaption);

        public void ConfigureInferencePresentation(Func<string, InferenceResultReviewEntry> resultProvider, Func<InferenceResultReviewEntry, ModelDecisionResult> decisionProvider, Action refreshView)
        {
            inferenceResultProvider = resultProvider;
            inferenceDecisionProvider = decisionProvider;
            refreshInferenceView = refreshView;
        }

        public void RefreshInferencePresentation(bool active, IEnumerable<WpfImageQueueItem> items, string modelCaption)
        {
            bool changed = SetProperty(ref isInferencePresentation, active, nameof(IsInferencePresentation));
            WpfImageQueueItem[] rows = (items ?? Array.Empty<WpfImageQueueItem>()).Where(item => item != null).ToArray();
            int completed = 0;
            foreach (WpfImageQueueItem row in rows)
            {
                InferenceResultReviewEntry entry = inferenceResultProvider?.Invoke(row.ImagePath);
                bool hasResult = entry?.Result != null;
                bool? isNg = hasResult ? inferenceDecisionProvider?.Invoke(entry)?.IsNg : null;
                string key = !hasResult ? entry?.Failure != null ? "WpfInference.Workspace.Failed" : "WpfInference.Workspace.NotRun"
                    : !isNg.HasValue ? "WpfInference.Queue.DecisionUnknown"
                    : IsAnomalyImageReviewMode ? isNg.Value ? "WpfAnomalyGrid.AbnormalState" : "WpfAnomalyGrid.NormalState"
                    : isNg.Value ? "WpfInference.Queue.Ng" : "WpfInference.Queue.Ok";
                changed |= row.SetInferencePresentation(hasResult, isNg, key);
                if (hasResult) completed++;
            }
            changed |= inferenceCompletedCount != completed || inferenceImageCount != rows.Length || inferenceModelCaption != modelCaption;
            inferenceCompletedCount = completed;
            inferenceImageCount = rows.Length;
            inferenceModelCaption = modelCaption ?? string.Empty;
            OnPropertyChanged(nameof(InferenceQueueSummaryText));
            OnPropertyChanged(nameof(InferenceFilterNgText));
            OnPropertyChanged(nameof(InferenceFilterOkText));
            if (changed) refreshInferenceView?.Invoke();
        }

        public IReadOnlyList<ImageQueueSortOption> CandidateSortOptions => candidateSortOptions;
        public IReadOnlyList<WpfImageQueueFilterOption> QueueFilterOptions => queueFilterOptions;
        public IReadOnlyList<WpfImageQueueFilterOption> LabelingFilterOptions => LabelingFilterOrder
            .Select(filter => queueFilterOptions.FirstOrDefault(option => option.Filter == filter)).Where(option => option != null).ToArray();

        public void SetLabelingFilterCounts(ImageQueueSummary summary)
        {
            labelingFilterSummary = summary ?? new ImageQueueSummary();
            SetCandidateFilterCounts(labelingFilterSummary.CandidateCount, labelingFilterSummary.LabelSuspicionCount);
            foreach (WpfImageQueueFilterOption option in queueFilterOptions)
            {
                option.IsAnomalyLabeling = IsAnomalyImageReviewMode;
                option.Count = ImageQueueFilterService.CountByFilter(labelingFilterSummary, option.Filter);
            }
            OnPropertyChanged(nameof(LabelingCompletionProgressText));
            OnPropertyChanged(nameof(LabelingRemainingText));
            OnPropertyChanged(nameof(LabelingReviewProgressText));
            OnPropertyChanged(nameof(LabelingReviewedProgressWidth));
            OnPropertyChanged(nameof(LabelingCompletedProgressWidth));
            OnPropertyChanged(nameof(LabelingRemainingProgressWidth));
        }
        public string LabelingRemainingText => Format("WpfLabeling.Footer.Remaining", labelingFilterSummary.WorklistCount);
        public string LabelingCompletionProgressText => $"{T(IsAnomalyImageReviewMode ? "WpfAnomalyLabeling.Mode" : "WpfShell.Right.Rail.Labels")} {labelingFilterSummary.CompletedCount}/{labelingFilterSummary.TotalCount}";
        public string LabelingReviewProgressText => $"{T("WpfClassCatalog.ColorPreset.Review")} {labelingFilterSummary.ReviewedCount}/{labelingFilterSummary.TotalCount}";
        public System.Windows.GridLength LabelingReviewedProgressWidth => new System.Windows.GridLength(labelingFilterSummary.ReviewedCount, System.Windows.GridUnitType.Star);
        public System.Windows.GridLength LabelingCompletedProgressWidth => new System.Windows.GridLength(Math.Max(0, labelingFilterSummary.CompletedCount - labelingFilterSummary.ReviewedCount), System.Windows.GridUnitType.Star);
        public System.Windows.GridLength LabelingRemainingProgressWidth => new System.Windows.GridLength(Math.Max(0, labelingFilterSummary.TotalCount - labelingFilterSummary.CompletedCount), System.Windows.GridUnitType.Star);

        public WpfImageQueueFilterOption SelectedLabelingFilterOption
        {
            get => LabelingFilterOrder.Contains(SelectedFilter) ? SelectedFilterOption : null;
            set { if (value != null) SetSelectedFilter(value.Filter); }
        }

        public WpfImageQueueFilterOption SelectedFilterOption
        {
            get => queueFilterOptions.FirstOrDefault(option => option.Filter == SelectedFilter);
            set { if (value != null) SetSelectedFilter(value.Filter); }
        }

        public void SetCandidateFilterCounts(int candidateCount, int suspicionCount)
        {
            labelingCandidateCount = Math.Max(0, candidateCount);
            labelingSuspicionCount = Math.Max(0, suspicionCount);
            bool candidates = labelingCandidateCount > 0 || candidateWorkflowVisible && SelectedFilter == WpfImageQueueFilter.Candidate;
            bool suspicions = labelingSuspicionCount > 0 || candidateWorkflowVisible && SelectedFilter == WpfImageQueueFilter.LabelSuspicion;
            if (queueFilterOptions.Any(option => option.Filter == WpfImageQueueFilter.Candidate) == candidates
                && queueFilterOptions.Any(option => option.Filter == WpfImageQueueFilter.LabelSuspicion) == suspicions) return;
            queueFilterOptions = WpfImageQueueFilterOption.CreateDefaults().Where(option =>
                (option.Filter != WpfImageQueueFilter.Candidate || candidates) && (option.Filter != WpfImageQueueFilter.LabelSuspicion || suspicions)).ToArray();
            if (!queueFilterOptions.Any(option => option.Filter == SelectedFilter)) SetSelectedFilter(WpfImageQueueFilter.All);
            foreach (WpfImageQueueFilterOption option in queueFilterOptions)
            {
                option.IsAnomalyLabeling = IsAnomalyImageReviewMode;
                option.Count = ImageQueueFilterService.CountByFilter(labelingFilterSummary, option.Filter);
            }
            OnPropertyChanged(nameof(QueueFilterOptions));
            OnPropertyChanged(nameof(LabelingFilterOptions));
            OnPropertyChanged(nameof(SelectedFilterOption));
            OnPropertyChanged(nameof(SelectedLabelingFilterOption));
        }
        public ImageQueueSortOption SelectedCandidateSort
        {
            get => selectedCandidateSort;
            set
            {
                if (value?.IsEnabled != true || !SetProperty(ref selectedCandidateSort, value)) return;
                candidateSortChanged?.Invoke(value.Sort);
            }
        }
        public System.Windows.Visibility CandidateWorkflowVisibility => candidateWorkflowVisible ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        public ICommand GenerateCandidatesCommand => generateCandidatesCommand;
        public string GenerateCandidatesText => T("WpfCandidate.Batch.Action");
        public string CandidateSortText => T("WpfCandidate.Batch.Sort");
        public string CandidateSortUnavailableText => candidateSortUnavailableText;
        public ICommand ApplyCandidateAnomalyCommand => applyCandidateAnomalyCommand;
        public ICommand SelectNormalAnomalyCandidatesCommand { get; private set; } = new RelayCommand(NoOpCommand, () => false);
        public ICommand SelectAbnormalAnomalyCandidatesCommand { get; private set; } = new RelayCommand(NoOpCommand, () => false);
        public bool HasCandidateAnomalyPrediction => candidateAnomalyPredictionState != AnomalyImageReviewState.Unreviewed;
        public System.Windows.Visibility CandidateAnomalyPredictionVisibility => HasCandidateAnomalyPrediction ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        public string CandidateAnomalyPredictionText => HasCandidateAnomalyPrediction ? Format("WpfCandidate.Batch.AnomalyPrediction",
            T(candidateAnomalyPredictionState == AnomalyImageReviewState.Normal ? "WpfAnomalyGrid.NormalState" : "WpfAnomalyGrid.AbnormalState"),
            candidateAnomalyScore?.ToString("0.####", CultureInfo.CurrentCulture), candidateAnomalyThreshold?.ToString("0.####", CultureInfo.CurrentCulture)) : string.Empty;
        public string ApplyCandidateAnomalyText => Format("WpfCandidate.Batch.AnomalyApply", T(candidateAnomalyPredictionState == AnomalyImageReviewState.Normal ? "WpfAnomalyGrid.NormalState" : "WpfAnomalyGrid.AbnormalState"));
        public string SelectNormalAnomalyCandidatesText => Format("WpfCandidate.Batch.SelectNormal",
            anomalyGridItems.OfType<WpfImageQueueItem>().Count(row => row.SessionCandidateAnomalyState == AnomalyImageReviewState.Normal && !row.HasAnomalyVerdict));
        public string SelectAbnormalAnomalyCandidatesText => Format("WpfCandidate.Batch.SelectAbnormal",
            anomalyGridItems.OfType<WpfImageQueueItem>().Count(row => row.SessionCandidateAnomalyState == AnomalyImageReviewState.Abnormal && !row.HasAnomalyVerdict));

        public void ConfigureCandidateAnomalyAction(Action<AnomalyImageReviewState> apply, Func<bool> canApply)
        {
            applyCandidateAnomaly = apply;
            canApplyCandidateAnomaly = canApply;
            applyCandidateAnomalyCommand = new RelayCommand(() => applyCandidateAnomaly?.Invoke(candidateAnomalyPredictionState),
                () => HasCandidateAnomalyPrediction && !IsAnomalyGridActive && canApplyCandidateAnomaly?.Invoke() == true);
            SelectNormalAnomalyCandidatesCommand = new RelayCommand(() => SelectAnomalyCandidates(AnomalyImageReviewState.Normal), () => IsAnomalyGridActive && isAnomalyDecisionAvailable);
            SelectAbnormalAnomalyCandidatesCommand = new RelayCommand(() => SelectAnomalyCandidates(AnomalyImageReviewState.Abnormal), () => IsAnomalyGridActive && isAnomalyDecisionAvailable);
            OnPropertyChanged(nameof(ApplyCandidateAnomalyCommand));
            OnPropertyChanged(nameof(SelectNormalAnomalyCandidatesCommand));
            OnPropertyChanged(nameof(SelectAbnormalAnomalyCandidatesCommand));
        }

        public void SetCandidateAnomalyPrediction(YoloWorkerSmokeTestResult result, double? savedThreshold)
        {
            double? score = result?.Succeeded == true ? result.CaptureAnomalyScore ?? result.Candidates?.FirstOrDefault(candidate => candidate?.ImageLevel == true)?.AnomalyScore : null;
            bool valid = score.HasValue && savedThreshold.HasValue && double.IsFinite(score.Value) && double.IsFinite(savedThreshold.Value);
            candidateAnomalyScore = valid ? score : null;
            candidateAnomalyThreshold = valid ? savedThreshold : null;
            candidateAnomalyPredictionState = valid ? score.Value >= savedThreshold.Value ? AnomalyImageReviewState.Abnormal : AnomalyImageReviewState.Normal : AnomalyImageReviewState.Unreviewed;
            OnPropertyChanged(nameof(HasCandidateAnomalyPrediction));
            OnPropertyChanged(nameof(CandidateAnomalyPredictionVisibility));
            OnPropertyChanged(nameof(CandidateAnomalyPredictionText));
            OnPropertyChanged(nameof(ApplyCandidateAnomalyText));
            CommandManager.InvalidateRequerySuggested();
        }

        private void SelectAnomalyCandidates(AnomalyImageReviewState state)
        {
            WpfImageQueueItem[] rows = anomalyGridItems?.OfType<WpfImageQueueItem>().ToArray() ?? Array.Empty<WpfImageQueueItem>();
            foreach (WpfImageQueueItem row in rows) row.IsAnomalyGridSelected = row.SessionCandidateAnomalyState == state && !row.HasAnomalyVerdict;
            SetAnomalyGridSelection(rows.Where(row => row.IsAnomalyGridSelected).ToArray());
        }
        public System.Windows.Visibility CandidateBatchRunningVisibility => isCandidateBatchRunning ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        public string CandidateBatchSummaryText => candidateBatchSummary == null ? string.Empty : Format("WpfCandidate.Batch.Summary",
            candidateBatchSummary.ProcessedImages, candidateBatchSummary.ImagesWithCandidates, candidateBatchSummary.EmptyImages,
            candidateBatchSummary.FailedImages, candidateBatchSummary.Canceled ? T("WpfCandidate.Batch.Canceled")
                : string.IsNullOrWhiteSpace(candidateBatchSummary.FailureSummary) ? T("WpfCandidate.Batch.Completed") : T("WpfCandidate.Batch.Stopped"))
                + (string.IsNullOrWhiteSpace(candidateBatchSummary.FailureSummary) ? string.Empty : " · " + candidateBatchSummary.FailureSummary);

        public void SetCandidateWorkflowVisible(bool visible)
        {
            if (candidateWorkflowVisible == visible) return;
            candidateWorkflowVisible = visible;
            SetCandidateFilterCounts(labelingCandidateCount, labelingSuspicionCount);
            OnPropertyChanged(nameof(CandidateWorkflowVisibility));
        }

        public void SetCandidateBatchRunning(bool running)
        {
            isCandidateBatchRunning = running;
            if (running) candidateBatchSummary = null;
            OnPropertyChanged(nameof(CandidateBatchRunningVisibility));
            OnPropertyChanged(nameof(CandidateBatchSummaryText));
            if (generateCandidatesCommand is RelayCommand command) command.RaiseCanExecuteChanged();
        }

        public void SetCandidateBatchSummary(CandidateBatchSummary summary)
        {
            candidateBatchSummary = summary;
            OnPropertyChanged(nameof(CandidateBatchSummaryText));
        }

        public void SelectCandidateSort(ImageQueueSort sort)
            => SelectedCandidateSort = candidateSortOptions.FirstOrDefault(option => option.Sort == sort && option.IsEnabled) ?? candidateSortOptions.FirstOrDefault();

        public void RefreshCandidateCommands()
        {
            if (generateCandidatesCommand is RelayCommand command) command.RaiseCanExecuteChanged();
        }

        public void ApplyCandidateSort(ICollectionView collectionView, bool refresh = false)
        {
            if (collectionView is not ListCollectionView listView) return;
            if (ReferenceEquals(listView.CustomSort, this) && listView.SortDescriptions.Count == 0)
            {
                if (refresh) listView.Refresh();
                return;
            }
            using (listView.DeferRefresh())
            {
                listView.SortDescriptions.Clear();
                listView.CustomSort = this;
            }
        }

        int IComparer.Compare(object left, object right)
        {
            if (left is not WpfImageQueueItem first || right is not WpfImageQueueItem second) return 0;
            ImageQueueSort sort = selectedCandidateSort?.Sort ?? ImageQueueSort.File;
            double? firstScore = sort == ImageQueueSort.Uncertain ? first.CandidateUncertainty : sort == ImageQueueSort.Disagreement ? first.ModelDisagreement : null;
            double? secondScore = sort == ImageQueueSort.Uncertain ? second.CandidateUncertainty : sort == ImageQueueSort.Disagreement ? second.ModelDisagreement : null;
            int compared = Nullable.Compare(secondScore, firstScore);
            return compared != 0 ? compared : StringComparer.OrdinalIgnoreCase.Compare(first.FileName, second.FileName);
        }

        public void ConfigureCandidateWorkflow(Action generate, Func<bool> canGenerate, Action<ImageQueueSort> sortChanged)
        {
            canGenerateCandidates = canGenerate;
            candidateSortChanged = sortChanged;
            generateCandidatesCommand = new RelayCommand(generate ?? NoOpCommand, () => canGenerateCandidates?.Invoke() == true);
            candidateWorkflowVisible = generate != null;
            OnPropertyChanged(nameof(GenerateCandidatesCommand));
            OnPropertyChanged(nameof(CandidateWorkflowVisibility));
        }

        public void SetCandidateSortAvailability(bool hasInspectionResult, bool hasMultipleModelResults, string unavailableReason)
        {
            OnPropertyChanged(nameof(SelectNormalAnomalyCandidatesText));
            OnPropertyChanged(nameof(SelectAbnormalAnomalyCandidatesText));
            SetProperty(ref candidateSortUnavailableText, hasInspectionResult && hasMultipleModelResults ? string.Empty : unavailableReason ?? string.Empty, nameof(CandidateSortUnavailableText));
            ImageQueueSort sort = selectedCandidateSort?.Sort ?? ImageQueueSort.File;
            ImageQueueSortOption[] options = new[] {
                new ImageQueueSortOption(ImageQueueSort.File, T("WpfCandidate.Batch.SortFile"), true, string.Empty),
                new ImageQueueSortOption(ImageQueueSort.Uncertain, T("WpfCandidate.Batch.SortUncertain"), hasInspectionResult, hasInspectionResult ? string.Empty : unavailableReason),
                new ImageQueueSortOption(ImageQueueSort.Disagreement, T("WpfCandidate.Batch.SortDisagreement"), hasMultipleModelResults, hasMultipleModelResults ? string.Empty : unavailableReason)
            };
            if (candidateSortOptions.SequenceEqual(options)) { RefreshCandidateCommands(); return; }
            candidateSortOptions = options;
            OnPropertyChanged(nameof(CandidateSortOptions));
            SelectedCandidateSort = candidateSortOptions.FirstOrDefault(option => option.Sort == sort && option.IsEnabled) ?? candidateSortOptions[0];
            if (generateCandidatesCommand is RelayCommand command) command.RaiseCanExecuteChanged();
        }

        public IEnumerable<WpfImageQueueItem> OrderCandidateQueue(IEnumerable<WpfImageQueueItem> items)
        {
            IEnumerable<WpfImageQueueItem> rows = items ?? Array.Empty<WpfImageQueueItem>();
            return (selectedCandidateSort?.Sort ?? ImageQueueSort.File) switch
            {
                ImageQueueSort.Uncertain => rows.OrderByDescending(item => item.CandidateUncertainty.HasValue).ThenByDescending(item => item.CandidateUncertainty).ThenBy(item => item.FileName, StringComparer.OrdinalIgnoreCase),
                ImageQueueSort.Disagreement => rows.OrderByDescending(item => item.ModelDisagreement.HasValue).ThenByDescending(item => item.ModelDisagreement).ThenBy(item => item.FileName, StringComparer.OrdinalIgnoreCase),
                _ => rows.OrderBy(item => item.FileName, StringComparer.OrdinalIgnoreCase)
            };
        }
        public string ViewName => nameof(WpfImageQueuePanel);

        public string PanelTitleText => T("WpfImageQueue.Panel.Title");

        public System.Collections.IEnumerable AnomalyGridItems => anomalyGridItems;
        public IReadOnlyList<WpfImageQueueItem> AnomalyGridSelection => anomalyGridSelection;
        public bool IsAnomalyGridActive { get => isAnomalyGridActive; private set => SetProperty(ref isAnomalyGridActive, value); }
        public bool IsAnomalyDecisionAvailable => isAnomalyDecisionAvailable;
        public bool IsUndoAnomalyDecisionEnabled => isUndoAnomalyDecisionEnabled;
        public bool IsAnomalyDecisionNoticeVisible => isAnomalyDecisionNoticeVisible;
        public bool IsAnomalyUndoNoticeVisible => isAnomalyDecisionNoticeVisible && anomalyNoticeOffersUndo && isUndoAnomalyDecisionEnabled;
        public string AnomalyGridToggleText => T(IsAnomalyGridActive ? "WpfAnomalyGrid.SingleView" : "WpfAnomalyGrid.GridView");
        public string AnomalyGridHintText => T("WpfAnomalyGrid.Hint");
        public string AnomalyGridTitleText => T("WpfAnomalyGrid.Title");
        public string AnomalyDecisionTitleText => T(IsAnomalyGridActive ? "WpfAnomalyGrid.Title" : "WpfAnomalyGrid.SingleTitle");
        public bool HasAnomalyPrediction => anomalyPredictionReviewState != AnomalyImageReviewState.Unreviewed;
        public string AnomalyPredictionSummaryText => HasAnomalyPrediction
            ? Format("WpfAnomalyGrid.Prediction.Summary", T(anomalyPredictionReviewState == AnomalyImageReviewState.Normal ? "WpfAnomalyGrid.NormalState" : "WpfAnomalyGrid.AbnormalState"), anomalyPredictionClassName, anomalyPredictionConfidence.ToString("0.###", CultureInfo.InvariantCulture))
            : T("WpfAnomalyGrid.Prediction.None");
        public string AnomalyNormalShortcutText => IsAnomalyGridActive ? T("WpfAnomalyGrid.SelectionShortcut") + " · 1" : "OK · 1";
        public string AnomalyAbnormalShortcutText => IsAnomalyGridActive ? T("WpfAnomalyGrid.SelectionShortcut") + " · 2" : "NG · 2";
        public string AnomalyDecisionProgressText => Format("WpfAnomalyGrid.Progress", anomalyDecisionSummary?.NormalImageCount ?? 0, anomalyDecisionSummary?.AbnormalImageCount ?? 0, anomalyDecisionSummary?.UnreviewedImageCount ?? 0);
        public string AnomalyNextUnreviewedText => T("WpfAnomalyGrid.Next");
        public string AnomalyAutoAdvanceText => T("WpfAnomalyGrid.AutoAdvance");
        public bool IsAnomalyAutoAdvanceAfterDecision
        {
            get => isAnomalyAutoAdvanceAfterDecision;
            set { if (SetProperty(ref isAnomalyAutoAdvanceAfterDecision, value) && !isRestoringAnomalyAutoAdvance) anomalyAutoAdvanceChanged(value); }
        }
        public string AnomalyNormalStateText => T("WpfAnomalyGrid.NormalState");
        public string AnomalyAbnormalStateText => T("WpfAnomalyGrid.AbnormalState");
        public bool IsAnomalyNormalDecisionActive => IsAnomalyGridActive ? anomalyGridSelection.Count > 0 && anomalyGridSelection.All(item => item.AnomalyReviewState == AnomalyImageReviewState.Normal) : ActiveQueueItem?.AnomalyReviewState == AnomalyImageReviewState.Normal;
        public bool IsAnomalyAbnormalDecisionActive => IsAnomalyGridActive ? anomalyGridSelection.Count > 0 && anomalyGridSelection.All(item => item.AnomalyReviewState == AnomalyImageReviewState.Abnormal) : ActiveQueueItem?.AnomalyReviewState == AnomalyImageReviewState.Abnormal;
        public string AnomalyGridSelectionText => anomalyGridSelection.Count == 0 ? T("WpfAnomalyGrid.SelectPrompt") : Format("WpfAnomalyGrid.Selected", anomalyGridSelection.Count);
        public string AnomalyGridSaveHintText => T("WpfAnomalyGrid.SaveHint");
        public string AnomalyGridNormalText => T("WpfAnomalyGrid.Normal");
        public string AnomalyGridAbnormalText => T("WpfAnomalyGrid.Abnormal");
        public string AnomalyUndoText => T("WpfAnomalyGrid.Undo");
        public string AnomalyDecisionNoticeText => anomalyNoticeKind switch
        {
            "Single" => Format("WpfAnomalyGrid.Notice.Single", anomalyNoticeFileName, T(anomalyNoticeState == AnomalyImageReviewState.Normal ? "WpfAnomalyGrid.NormalState" : "WpfAnomalyGrid.AbnormalState")),
            "Bulk" => Format("WpfAnomalyGrid.Notice.Bulk", anomalyNoticeCount, T(anomalyNoticeState == AnomalyImageReviewState.Normal ? "WpfAnomalyGrid.NormalState" : "WpfAnomalyGrid.AbnormalState")),
            "Undone" => T("WpfAnomalyGrid.Notice.Undone"),
            "UndoneImageUnavailable" => T("WpfAnomalyGrid.Notice.UndoneImageUnavailable"),
            "Failed" => T("WpfAnomalyGrid.Notice.Failed"),
            "PreferenceFailed" => T("WpfAnomalyGrid.Notice.PreferenceFailed"),
            _ => string.Empty
        };

        public string BatchStatusText
        {
            get => LocalizationTextRuntimeService.Translate(batchStatusText);
            private set => SetProperty(ref batchStatusText, value ?? string.Empty);
        }

        public int BatchProgressMaximum
        {
            get => batchProgressMaximum;
            private set => SetProperty(ref batchProgressMaximum, Math.Max(1, value));
        }

        public int BatchProgressValue
        {
            get => batchProgressValue;
            private set => SetProperty(ref batchProgressValue, Math.Clamp(value, 0, BatchProgressMaximum));
        }

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

        public string ClearSearchText => T("WpfImageQueue.Search.Clear");

        public string ClearSearchToolTipText => T("WpfImageQueue.Search.Clear.ToolTip");

        public string NoMatchingImagesTitleText => T("WpfImageQueue.Search.Empty.Title");

        public string NoMatchingImagesDetailText => T("WpfImageQueue.Search.Empty.Detail");

        public string ResetQueueFiltersText => T("WpfImageQueue.Search.ResetFilters");

        public string ResetQueueFiltersToolTipText => T("WpfImageQueue.Search.ResetFilters.ToolTip");

        public bool HasSearchText => !string.IsNullOrEmpty(SearchText);

        public bool HasQueueFilter => !string.IsNullOrWhiteSpace(SearchText) || SelectedFilter != WpfImageQueueFilter.All;

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

        public WpfImageQueueItem ActiveQueueItem
        {
            get => activeQueueItem;
            internal set => SetProperty(ref activeQueueItem, value);
        }

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

                    IsSelectedImagePathVisible = false;
                    RefreshCurrentImageTaskSummary();
                    selectedQueueItemChanged(value);
                }
            }
        }

        public bool IsSelectedImagePathVisible
        {
            get => isSelectedImagePathVisible;
            set => SetProperty(ref isSelectedImagePathVisible, value && SelectedQueueItem != null);
        }

        public WpfImageQueueFilter SelectedFilter
        {
            get => selectedFilter;
            private set
            {
                if (SetProperty(ref selectedFilter, value))
                {
                    OnPropertyChanged(nameof(HasQueueFilter));
                    SetCandidateFilterCounts(labelingCandidateCount, labelingSuspicionCount);
                    OnPropertyChanged(nameof(SelectedLabelingFilterOption));
                    RefreshDatasetEmptyPresentation();
                    resetQueueFiltersCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string SearchText
        {
            get => searchText;
            private set
            {
                if (SetProperty(ref searchText, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(HasSearchText));
                    OnPropertyChanged(nameof(HasQueueFilter));
                    RefreshDatasetEmptyPresentation();
                    clearSearchCommand.RaiseCanExecuteChanged();
                    resetQueueFiltersCommand.RaiseCanExecuteChanged();
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

        public ICommand ClearSearchCommand => clearSearchCommand;

        public ICommand ResetQueueFiltersCommand => resetQueueFiltersCommand;

        public ICommand QueueSelectionChangedCommand
        {
            get => queueSelectionChangedCommand;
            private set => SetProperty(ref queueSelectionChangedCommand, value);
        }

        public ICommand QueueExplicitOpenCommand
        {
            get => queueExplicitOpenCommand;
            private set => SetProperty(ref queueExplicitOpenCommand, value);
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
            private set
            {
                if (SetProperty(ref currentImageFolderPath, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(CurrentImageFolderNameText));
                }
            }
        }

        public string CurrentImageFolderNameText
            => string.IsNullOrWhiteSpace(CurrentImageFolderPath)
                ? T("WpfImageQueue.Folder.Empty")
                : Path.GetFileName(CurrentImageFolderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) is { Length: > 0 } name
                    ? name
                    : CurrentImageFolderPath;

        public string CurrentImageFolderDisplayText
        {
            get => currentImageFolderDisplayText;
            private set => SetProperty(ref currentImageFolderDisplayText, value ?? string.Empty);
        }

        public int DatasetImageCount
        {
            get => datasetImageCount;
            private set
            {
                if (SetProperty(ref datasetImageCount, Math.Max(0, value))) RefreshDatasetEmptyPresentation();
            }
        }

        public string DatasetImageCountText => IsImageFolderUnavailable ? T("WpfDatasetHome.CountUnknown") : DatasetImageCount.ToString(CultureInfo.InvariantCulture);

        public string DatasetLabeledImageCountText => IsImageFolderUnavailable ? T("WpfDatasetHome.CountUnknown") : DatasetLabeledImageCount.ToString(CultureInfo.InvariantCulture);

        public bool IsImageFolderUnavailable => !string.IsNullOrWhiteSpace(CurrentImageFolderPath) && !IsOpenCurrentImageFolderEnabled;

        public string DatasetEmptyTitleText
        {
            get
            {
                if (DatasetImageCount > 0) return T(HasQueueFilter ? "WpfDatasetHome.Filtered.Title" : "WpfImageQueue.Current.Waiting.Title");
                if (IsImageFolderUnavailable) return T("WpfDatasetHome.MissingFolder.Title");
                return T(string.IsNullOrWhiteSpace(CurrentImageFolderPath) ? "WpfDatasetHome.Empty.Title" : "WpfDatasetHome.EmptyFolder.Title");
            }
        }

        public string DatasetEmptyDetailText
        {
            get
            {
                if (DatasetImageCount > 0) return T(HasQueueFilter ? "WpfImageQueue.Search.Empty.Detail" : "WpfImageQueue.Current.Waiting.StandardDetail");
                if (IsImageFolderUnavailable) return T("WpfDatasetHome.MissingFolder.Detail");
                return T(string.IsNullOrWhiteSpace(CurrentImageFolderPath) ? "WpfDatasetHome.Empty.Detail" : "WpfDatasetHome.EmptyFolder.Detail");
            }
        }

        public int DatasetLabeledImageCount
        {
            get => datasetLabeledImageCount;
            private set => SetProperty(ref datasetLabeledImageCount, Math.Max(0, value));
        }

        public int DatasetReviewedImageCount
        {
            get => datasetReviewedImageCount;
            private set => SetProperty(ref datasetReviewedImageCount, Math.Max(0, value));
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

        public ICommand ToggleAnomalyGridCommand => toggleAnomalyGridCommand;
        public ICommand MarkAnomalySelectionNormalCommand => markAnomalySelectionNormalCommand;
        public ICommand MarkAnomalySelectionAbnormalCommand => markAnomalySelectionAbnormalCommand;
        public ICommand UndoAnomalyDecisionCommand => undoAnomalyDecisionCommand;
        public ICommand OpenAnomalyGridImageCommand => openAnomalyGridImageCommand;
        public ICommand AnomalyGridSelectionChangedCommand => anomalyGridSelectionChangedCommand;
        public ICommand NextAnomalyUnreviewedCommand { get; private set; } = new RelayCommand(NoOpCommand);

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
            Action<object> filterSelectionChangedHandler = filterSelectionChanged ?? NoOpSelectionCommand;
            FilterSelectionChangedCommand = new RelayCommand<object>(selected =>
            {
                if (selected is WpfImageQueueFilterOption filterOption)
                {
                    SetSelectedFilter(filterOption.Filter);
                }

                filterSelectionChangedHandler(selected);
            });
            this.searchTextChanged = searchTextChanged ?? NoOpTextCommand;
            SearchTextChangedCommand = new RelayCommand<string>(ExecuteSearchTextChanged);
            QueueSelectionChangedCommand = new RelayCommand<object>(queueSelectionChanged ?? NoOpSelectionCommand);
            QueueMouseDoubleClickCommand = new RelayCommand(queueMouseDoubleClick ?? NoOpMouseCommand);
            ApplyAnomalyFolderStateSuggestionCommand = new RelayCommand(applyAnomalyFolderStateSuggestion ?? NoOpCommand);
            DismissAnomalyFolderStateSuggestionCommand = new RelayCommand(dismissAnomalyFolderStateSuggestion ?? NoOpCommand);
            MarkAnomalyNormalCommand = new RelayCommand(markAnomalyNormal ?? NoOpCommand);
            MarkAnomalyAbnormalCommand = new RelayCommand(markAnomalyAbnormal ?? NoOpCommand);
            ClearAnomalyReviewCommand = new RelayCommand(clearAnomalyReview ?? NoOpCommand);
        }

        public void ConfigureFilterWorkflow(Action<WpfImageQueueFilter> filterChanged)
        {
            this.filterChanged = filterChanged ?? NoOpFilterCommand;
            QueueFilterUnfinishedCommand = new RelayCommand(() => ExecuteFilterCommand(WpfImageQueueFilter.Unlabeled));
            QueueFilterAllCommand = new RelayCommand(() => ExecuteFilterCommand(WpfImageQueueFilter.All));
            QueueFilterCandidateCommand = new RelayCommand(() => ExecuteFilterCommand(WpfImageQueueFilter.Candidate));
            QueueFilterFailedCommand = new RelayCommand(() => ExecuteFilterCommand(WpfImageQueueFilter.Failed));
            QueueFilterConfirmedCommand = new RelayCommand(() => ExecuteFilterCommand(WpfImageQueueFilter.Confirmed));
            QueueFilterSkippedCommand = new RelayCommand(() => ExecuteFilterCommand(WpfImageQueueFilter.Skipped));
            QueueFilterNoCandidateCommand = new RelayCommand(() => ExecuteFilterCommand(WpfImageQueueFilter.NoCandidate));
        }

        public void ConfigureAnomalyReviewCommands(System.Collections.IEnumerable items, Action<AnomalyImageReviewState> mark, Action undo, Action<WpfImageQueueItem> open, Action<bool> autoAdvanceChanged = null)
        {
            anomalyAutoAdvanceChanged = autoAdvanceChanged ?? (_ => { });
            anomalyGridItems = items ?? Array.Empty<WpfImageQueueItem>();
            OnPropertyChanged(nameof(AnomalyGridItems));
            toggleAnomalyGridCommand = new RelayCommand(() => SetAnomalyGridActive(!IsAnomalyGridActive), () => isAnomalyDecisionAvailable);
            markAnomalySelectionNormalCommand = new RelayCommand(() => mark?.Invoke(AnomalyImageReviewState.Normal), CanMarkAnomalySelection);
            markAnomalySelectionAbnormalCommand = new RelayCommand(() => mark?.Invoke(AnomalyImageReviewState.Abnormal), CanMarkAnomalySelection);
            undoAnomalyDecisionCommand = new RelayCommand(() => undo?.Invoke(), () => isUndoAnomalyDecisionEnabled);
            openAnomalyGridImageCommand = new RelayCommand<WpfImageQueueItem>(item => open?.Invoke(item), item => isAnomalyDecisionAvailable && item != null);
            NextAnomalyUnreviewedCommand = new RelayCommand(() => NextUnlabeledCommand.Execute(null), () => isAnomalyDecisionAvailable && NextUnlabeledCommand.CanExecute(null));
            OnPropertyChanged(string.Empty);
        }

        public void SetAnomalyGridActive(bool active)
        {
            IsAnomalyGridActive = active && isAnomalyDecisionAvailable;
            ClearAnomalyGridSelection();
            OnPropertyChanged(nameof(AnomalyGridToggleText));
            OnPropertyChanged(nameof(AnomalyDecisionTitleText));
            OnPropertyChanged(nameof(AnomalyNormalShortcutText));
            OnPropertyChanged(nameof(AnomalyAbnormalShortcutText));
            CommandManager.InvalidateRequerySuggested();
        }

        public void SetAnomalyDecisionAvailability(bool available, bool canMarkSingle, bool canUndo, bool resetView = false)
        {
            isAnomalyDecisionAvailable = available;
            isAnomalySingleDecisionEnabled = available && canMarkSingle;
            isUndoAnomalyDecisionEnabled = available && canUndo;
            if (resetView)
            {
                SetAnomalyGridActive(false);
                HideAnomalyDecisionNotice();
            }
            OnPropertyChanged(nameof(IsAnomalyDecisionAvailable));
            OnPropertyChanged(nameof(IsUndoAnomalyDecisionEnabled));
            OnPropertyChanged(nameof(IsAnomalyUndoNoticeVisible));
            OnPropertyChanged(nameof(IsAnomalyNormalDecisionActive));
            OnPropertyChanged(nameof(IsAnomalyAbnormalDecisionActive));
            CommandManager.InvalidateRequerySuggested();
        }

        public void ShowAnomalyDecisionNotice(string kind, string fileName = "", AnomalyImageReviewState state = AnomalyImageReviewState.Unreviewed, int count = 1, bool offerUndo = false)
        {
            anomalyNoticeKind = kind;
            anomalyNoticeFileName = fileName;
            anomalyNoticeState = state;
            anomalyNoticeCount = count;
            anomalyNoticeOffersUndo = offerUndo;
            isAnomalyDecisionNoticeVisible = true;
            OnPropertyChanged(nameof(AnomalyDecisionNoticeText));
            OnPropertyChanged(nameof(IsAnomalyDecisionNoticeVisible));
            OnPropertyChanged(nameof(IsAnomalyUndoNoticeVisible));
            anomalyNoticeTimer.Stop();
            anomalyNoticeTimer.Interval = TimeSpan.FromSeconds(offerUndo ? 6 : 2.6);
            anomalyNoticeTimer.Start();
        }

        public void HideAnomalyDecisionNotice()
        {
            anomalyNoticeTimer.Stop();
            isAnomalyDecisionNoticeVisible = false;
            OnPropertyChanged(nameof(IsAnomalyDecisionNoticeVisible));
            OnPropertyChanged(nameof(IsAnomalyUndoNoticeVisible));
        }

        public void ClearAnomalyGridSelection()
        {
            foreach (WpfImageQueueItem item in anomalyGridSelection.ToArray()) item.IsAnomalyGridSelected = false;
            SetAnomalyGridSelection(null);
        }

        public void SetAnomalyDecisionSummary(AnomalyImageReviewSummary summary)
        {
            anomalyDecisionSummary = summary;
            OnPropertyChanged(nameof(AnomalyDecisionProgressText));
        }

        public void SetAnomalyPrediction(AnomalyClassificationDecision decision)
        {
            bool mapped = decision?.IsMapped == true;
            anomalyPredictionClassName = mapped ? decision.ClassName ?? string.Empty : string.Empty;
            anomalyPredictionConfidence = mapped ? decision.Confidence : 0D;
            anomalyPredictionReviewState = mapped ? decision.ReviewState : AnomalyImageReviewState.Unreviewed;
            OnPropertyChanged(nameof(HasAnomalyPrediction));
            OnPropertyChanged(nameof(AnomalyPredictionSummaryText));
        }

        public void RestoreAnomalyAutoAdvance(bool enabled)
        {
            isRestoringAnomalyAutoAdvance = true;
            try { IsAnomalyAutoAdvanceAfterDecision = enabled; }
            finally { isRestoringAnomalyAutoAdvance = false; }
        }

        private void SetAnomalyGridSelection(object selected)
        {
            anomalyGridSelection = (selected as System.Collections.IEnumerable)?.OfType<WpfImageQueueItem>().Distinct().ToArray() ?? Array.Empty<WpfImageQueueItem>();
            OnPropertyChanged(nameof(AnomalyGridSelection));
            OnPropertyChanged(nameof(AnomalyGridSelectionText));
            OnPropertyChanged(nameof(IsAnomalyNormalDecisionActive));
            OnPropertyChanged(nameof(IsAnomalyAbnormalDecisionActive));
            CommandManager.InvalidateRequerySuggested();
        }

        private bool CanMarkAnomalySelection() => isAnomalyDecisionAvailable && (IsAnomalyGridActive ? anomalyGridSelection.Count > 0 : isAnomalySingleDecisionEnabled);
        private void OnAnomalyNoticeElapsed(object sender, EventArgs e) => HideAnomalyDecisionNotice();

        public void Dispose()
        {
            SetCandidateAnomalyPrediction(null, null);
            applyCandidateAnomaly = null;
            canApplyCandidateAnomaly = null;
            candidateSortChanged = null;
            canGenerateCandidates = null;
            candidateWorkflowVisible = false;
            isCandidateBatchRunning = false;
            candidateBatchSummary = null;
            SetAnomalyPrediction(null);
            HideAnomalyDecisionNotice();
            anomalyNoticeTimer.Tick -= OnAnomalyNoticeElapsed;
            isAnomalyDecisionAvailable = false;
            isUndoAnomalyDecisionEnabled = false;
            anomalyAutoAdvanceChanged = _ => { };
            if (selectedQueueItem != null) selectedQueueItem.PropertyChanged -= OnSelectedQueueItemPropertyChanged;
            anomalyGridItems = Array.Empty<WpfImageQueueItem>();
            anomalyGridSelection = Array.Empty<WpfImageQueueItem>();
        }

        public void ConfigureSearchWorkflow(Action<string> applySearchAction)
        {
            searchTextChanged = applySearchAction ?? NoOpTextCommand;
            SearchTextChangedCommand = new RelayCommand<string>(ExecuteSearchTextChanged);
        }

        public void ConfigureQueueSelectionWorkflow()
        {
            QueueSelectionChangedCommand = new RelayCommand<object>(ExecuteQueueSelectionChanged);
            QueueExplicitOpenCommand = new RelayCommand<WpfImageQueueItem>(item => selectedQueueItemChanged(item), item => item != null && IsOpenSelectedImageEnabled);
        }

        private void ExecuteQueueSelectionChanged(object selectedItem)
        {
            WpfImageQueueItem item = selectedItem as WpfImageQueueItem;
            if (!ReferenceEquals(SelectedQueueItem, item))
            {
                SelectedQueueItem = item;
            }
        }

        private void ExecuteFilterCommand(WpfImageQueueFilter filter)
        {
            SetSelectedFilter(filter);
            filterChanged(filter);
        }

        private void ExecuteSearchTextChanged(string text)
        {
            string normalized = text ?? string.Empty;
            // The Text binding echoes a command-driven clear through TextChanged.
            // Only the originating change should refresh the queue.
            if (string.Equals(SearchText, normalized, StringComparison.Ordinal))
            {
                return;
            }

            SetSearchText(normalized);
            searchTextChanged(normalized);
        }

        private void ExecuteResetQueueFilters()
        {
            if (SelectedFilter != WpfImageQueueFilter.All)
            {
                ExecuteFilterCommand(WpfImageQueueFilter.All);
            }

            ExecuteSearchTextChanged(string.Empty);
        }

        public void RefreshLocalizedPresentation(IEnumerable<WpfImageQueueItem> queueItems = null)
        {
            SetCandidateSortAvailability(candidateSortOptions.Any(option => option.Sort == ImageQueueSort.Uncertain && option.IsEnabled),
                candidateSortOptions.Any(option => option.Sort == ImageQueueSort.Disagreement && option.IsEnabled), T("WpfCandidate.Batch.SortUnavailable"));
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
            foreach (WpfImageQueueFilterOption option in queueFilterOptions) option.RefreshLocalizedPresentation();
            RefreshAnomalyFolderStateSuggestionText();
            OnPropertyChanged(string.Empty);
            RefreshCurrentImageTaskSummary();
        }

        public void SetAnomalyImageReviewMode(bool enabled)
        {
            IsAnomalyImageReviewMode = enabled;
            foreach (WpfImageQueueFilterOption option in queueFilterOptions) option.IsAnomalyLabeling = enabled;
            OnPropertyChanged(nameof(LabelingCompletionProgressText));
            OnPropertyChanged(nameof(LabelingRemainingText));
            if (!enabled) SetAnomalyPrediction(null);
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
            OnPropertyChanged(nameof(IsImageFolderUnavailable));
            OnPropertyChanged(nameof(DatasetImageCountText));
            OnPropertyChanged(nameof(DatasetLabeledImageCountText));
            RefreshDatasetEmptyPresentation();
            CommandManager.InvalidateRequerySuggested();
        }

        public void SetBatchDetectionProgress(int progressMaximum, int progressValue, string statusText)
        {
            BatchProgressMaximum = progressMaximum;
            BatchProgressValue = progressValue;
            BatchStatusText = statusText;
        }

        public void SetSelectedImageAvailability(bool canOpenSelectedImage)
        {
            IsOpenSelectedImageEnabled = canOpenSelectedImage;
        }

        public void SetSelectedFilter(WpfImageQueueFilter filter)
        {
            SelectedFilter = filter;
            OnPropertyChanged(nameof(SelectedFilterOption));
        }

        public void SetSearchText(string text)
        {
            SearchText = text ?? string.Empty;
        }

        public bool ShouldShow(WpfImageQueueItem item)
        {
            if (IsInferencePresentation) return item != null && (InferenceFilterIndex switch
            {
                1 => !item.HasInferenceResult,
                2 => item.HasInferenceResult && item.InferenceIsNg == true,
                3 => item.HasInferenceResult && item.InferenceIsNg == false,
                _ => true
            });
            return ImageQueueFilterService.ShouldShow(item, SearchText, SelectedFilter)
                || (SelectedFilter == WpfImageQueueFilter.Unlabeled
                    && ReferenceEquals(item, SelectedQueueItem)
                    && ImageQueueFilterService.ShouldShow(item, SearchText, WpfImageQueueFilter.All));
        }

        public void ApplyWorkflowCommandState(WorkflowCommandState state)
        {
            bool canRunInference = state?.CanRunInferenceTarget == true;
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
            SelectedFilter = selectedFilter;
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

        public void SetDatasetContentCounts(int imageCount, int labeledImageCount, int reviewedImageCount)
        {
            DatasetImageCount = imageCount;
            DatasetLabeledImageCount = labeledImageCount;
            DatasetReviewedImageCount = reviewedImageCount;
            OnPropertyChanged(nameof(DatasetImageCountText));
            OnPropertyChanged(nameof(DatasetLabeledImageCountText));
            CommandManager.InvalidateRequerySuggested();
        }

        private void RefreshDatasetEmptyPresentation()
        {
            OnPropertyChanged(nameof(DatasetEmptyTitleText));
            OnPropertyChanged(nameof(DatasetEmptyDetailText));
        }

        private void RefreshQuickFilterText()
        {
            QueueFilterUnfinishedText = Format(
                "WpfImageQueue.Filter.Unfinished",
                FormatCountForPresentation(unfinishedCountForPresentation));
            QueueFilterAllText = T("WpfImageQueue.Filter.All");
            QueueFilterCandidateText = Format(
                "WpfImageQueue.Filter.Candidate",
                FormatCountForPresentation(candidateCountForPresentation));
            QueueFilterFailedText = Format(
                "WpfImageQueue.Filter.Failed",
                FormatCountForPresentation(failedCountForPresentation));
            QueueFilterConfirmedText = Format(
                "WpfImageQueue.Filter.Confirmed",
                FormatCountForPresentation(confirmedCountForPresentation));
            QueueFilterSkippedText = Format(
                "WpfImageQueue.Filter.Skipped",
                FormatCountForPresentation(skippedCountForPresentation));
            QueueFilterNoCandidateText = Format(
                "WpfImageQueue.Filter.NoCandidate",
                FormatCountForPresentation(noCandidateCountForPresentation));
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

            string labelStatus = item.LocalizedLabelStatus;
            string detectStatus = item.LocalizedDetectStatus;
            string statusSummary = string.IsNullOrWhiteSpace(item.QueueStatusSummary)
                ? string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.WpfImageQueuePanelViewModel.FallbackSummary"), labelStatus, detectStatus)
                : item.LocalizedQueueStatusSummary;

            if (item.IsSaveRequired)
            {
                CurrentImageTaskTitleText = T("WpfImageQueue.Current.SaveRequired.Title");
                CurrentImageTaskDetailText = statusSummary;
                CurrentImageTaskBadgeText = string.IsNullOrWhiteSpace(item.QueueBadgeText)
                    ? T("WpfImageQueue.Current.SaveRequired.Badge")
                    : item.LocalizedQueueBadgeText;
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
                CurrentImageTaskTitleText = T("WpfImageQueue.Current.NeedsFix.Title");
                CurrentImageTaskDetailText = T("WpfImageQueue.Current.NeedsFix.Detail");
                CurrentImageTaskBadgeText = T("WpfImageQueue.Current.NeedsFix.Badge");
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
                    CurrentImageTaskTitleText = T("WpfImageQueue.Current.Requested.Title");
                    CurrentImageTaskDetailText = T("WpfImageQueue.Current.Requested.Detail");
                    CurrentImageTaskBadgeText = T("WpfImageQueue.Current.Requested.Badge");
                    CurrentImageTaskKey = "Requested";
                    break;
                case Yolo.YoloImageReviewState.Candidate:
                    CurrentImageTaskTitleText = T("WpfImageQueue.Current.Candidate.Title");
                    CurrentImageTaskDetailText = T("WpfImageQueue.Current.Candidate.Detail");
                    CurrentImageTaskBadgeText = string.IsNullOrWhiteSpace(item.QueueBadgeText) ? "AI" : item.LocalizedQueueBadgeText;
                    CurrentImageTaskKey = "Candidate";
                    break;
                case Yolo.YoloImageReviewState.Failed:
                    CurrentImageTaskTitleText = T("WpfImageQueue.Current.Failed.Title");
                    CurrentImageTaskDetailText = statusSummary;
                    CurrentImageTaskBadgeText = "\uC2E4\uD328";
                    CurrentImageTaskKey = "Failed";
                    break;
                case Yolo.YoloImageReviewState.Confirmed:
                    CurrentImageTaskTitleText = "\uB77C\uBCA8 \uC800\uC7A5 \uC644\uB8CC";
                    CurrentImageTaskDetailText = T("WpfImageQueue.Current.Saved.Detail");
                    CurrentImageTaskBadgeText = "\uC800\uC7A5";
                    CurrentImageTaskKey = "Saved";
                    break;
                case Yolo.YoloImageReviewState.NoCandidate:
                    string resultKey = item.HasSavedNoObjectLabel ? "NoCandidate" : "AiEmpty";
                    CurrentImageTaskTitleText = T("WpfImageQueue.Current." + resultKey + ".Title");
                    CurrentImageTaskDetailText = T("WpfImageQueue.Current." + resultKey + ".Detail");
                    CurrentImageTaskBadgeText = T("WpfImageQueue.Current." + resultKey + ".Badge");
                    CurrentImageTaskKey = ImageQueueFilterService.HasSavedGroundTruth(item) ? "Saved" : "NeedsLabel";
                    break;
                case Yolo.YoloImageReviewState.Skipped:
                    CurrentImageTaskTitleText = "\uD6C4\uBCF4 \uC228\uAE40";
                    CurrentImageTaskDetailText = T("WpfImageQueue.Current.Skipped.Detail");
                    CurrentImageTaskBadgeText = "\uC228\uAE40";
                    CurrentImageTaskKey = "Skipped";
                    break;
                default:
                    if (item.IsLabeled)
                    {
                        CurrentImageTaskTitleText = "\uC800\uC7A5 \uB77C\uBCA8 \uC788\uC74C";
                        CurrentImageTaskDetailText = string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.WpfImageQueuePanelViewModel.SavedDetail"), labelStatus, detectStatus);
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
                        string resultKey = item.HasSavedNoObjectLabel ? "NoCandidate" : "AiEmpty";
                        CurrentImageTaskTitleText = T("WpfImageQueue.Current." + resultKey + ".Title");
                        CurrentImageTaskDetailText = T("WpfImageQueue.Current." + resultKey + ".Detail");
                        CurrentImageTaskBadgeText = T("WpfImageQueue.Current." + resultKey + ".Badge");
                        CurrentImageTaskKey = ImageQueueFilterService.HasSavedGroundTruth(item) ? "Saved" : "NeedsLabel";
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

        private static string FormatCountForPresentation(int count)
            => count > 99 ? "99+" : Math.Max(0, count).ToString(CultureInfo.InvariantCulture);

        private static string Format(string key, params object[] arguments)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                T(key),
                arguments ?? Array.Empty<object>());
        }
    }
}
