using MvcVisionSystem.Yolo;
using OpenVisionLab;
using OpenVisionLab.Mvvm;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace MvcVisionSystem
{
    public sealed class DatasetWorkspaceViewModel : WpfObservableViewModel, IDisposable
    {
        private ObservableCollection<WpfImageQueueItem> source = new();
        private readonly HashSet<WpfImageQueueItem> subscribedItems = new();
        private readonly HashSet<string> selectedPaths = new(StringComparer.OrdinalIgnoreCase);
        private YoloDatasetSettings settings = new();
        private LabelingProjectData project;
        private ListCollectionView items;
        private string searchText = string.Empty;
        private string sourceRoot = string.Empty;
        private int unavailableSourceFolderCount;
        private string operationStatusKey = string.Empty;
        private object[] operationStatusArguments = Array.Empty<object>();
        private DatasetWorkspaceChoice selectedSplit;
        private DatasetWorkspaceFilterItem selectedFilter;
        private bool canEdit;
        private bool isApplyingSplit;
        private bool isAddingSourceFolder;
        private Action addSourceFolder = () => { };
        private readonly RelayCommand addSourceFolderCommand;
        private bool isRefreshing;
        private bool disposed;
        private YoloDatasetSplitOverlapSummary splitOverlap;
        private Action<string> assignSplit = _ => { };
        private Action<string> openImage = _ => { };
        private readonly RelayCommand<string> assignSplitCommand;
        private readonly RelayCommand openSelectedCommand;
        private readonly RelayCommand continueLabelingCommand;
        private bool splitSettingsOpen;
        private int splitValidationPercent;
        private int splitTestPercent;
        private int splitSeed;
        private bool stratifySplit;
        private bool freezeTestSplit;
        private YoloDatasetSplitPlan splitPreview;
        private string splitError = string.Empty;
        private Action prepareSplit = () => { };
        private Action applySplit = () => { };
        private readonly RelayCommand openSplitSettingsCommand;
        private readonly RelayCommand previewSplitCommand;
        private readonly RelayCommand applySplitSettingsCommand;
        private Action openLabelSuspicionReview = () => { };
        private Func<bool> canOpenLabelSuspicionReview = () => false;
        private readonly RelayCommand openLabelSuspicionReviewCommand;

        public DatasetWorkspaceViewModel()
        {
            foreach (string key in new[] { "all", "todo", "labeled", "reviewed", "rejected", "noobj" }) Filters.Add(new DatasetWorkspaceFilterItem(key));
            foreach (string key in new[] { "all", "train", "valid", "test", "excluded" }) SplitChoices.Add(new DatasetWorkspaceChoice(key));
            selectedFilter = Filters[0];
            selectedSplit = SplitChoices[0];
            addSourceFolderCommand = new RelayCommand(() => addSourceFolder(), () => canEdit && !isApplyingSplit && !isAddingSourceFolder && !disposed && project != null);
            assignSplitCommand = new RelayCommand<string>(split => assignSplit(split), _ => CanAssignSplit);
            openSelectedCommand = new RelayCommand(() => { if (CanOpenImage && HasSelection) openImage(SelectedItems.First().ImagePath); }, () => CanOpenImage && HasSelection);
            continueLabelingCommand = new RelayCommand(() => { if (CanOpenImage) openImage((source.FirstOrDefault(item => !HasRecordedWork(item)) ?? source.First()).ImagePath); }, () => CanOpenImage);
            openSplitSettingsCommand = new RelayCommand(OpenSplitSettings, () => CanConfigureSplit);
            previewSplitCommand = new RelayCommand(() => prepareSplit(), () => CanConfigureSplit && IsSplitSettingsOpen);
            applySplitSettingsCommand = new RelayCommand(() => applySplit(), () => CanConfigureSplit && splitPreview != null && IsSplitSettingsOpen);
            openLabelSuspicionReviewCommand = new RelayCommand(() => { if (CanOpenLabelSuspicionReview) openLabelSuspicionReview(); }, () => CanOpenLabelSuspicionReview);
            CancelSplitSettingsCommand = new RelayCommand(CancelSplitSettings, () => !IsApplyingSplit);
            UnlockTestSplitCommand = new RelayCommand(() => { FreezeTestSplit = false; }, () => IsSplitSettingsOpen && !IsApplyingSplit);
            ClearSelectionCommand = new RelayCommand(ClearSelection);
            SelectionChangedCommand = new RelayCommand<object>(UpdateSelection);
            OpenImageCommand = new RelayCommand<WpfImageQueueItem>(item => { if (CanOpenImage && item != null) openImage(item.ImagePath); }, item => CanOpenImage && item != null);
            AttachCatalog(source);
            WpfLocalizationViewModel.Instance.LanguageChanged += OnLanguageChanged;
        }

        internal event Action RefreshRequested;
        public ICollectionView Items => items;
        public ObservableCollection<DatasetWorkspaceFilterItem> Filters { get; } = new();
        public ObservableCollection<DatasetWorkspaceChoice> SplitChoices { get; } = new();
        public IEnumerable<DatasetWorkspaceChoice> AssignmentChoices => SplitChoices.Skip(1);
        public ObservableCollection<DatasetWorkspaceClassItem> Classes { get; } = new();
        public IReadOnlyList<WpfImageQueueItem> SelectedItems => source.Where(item => selectedPaths.Contains(item.ImagePath)).ToArray();
        public int SelectedCount => selectedPaths.Count;
        public bool HasSelection => SelectedCount > 0;
        internal bool CanOpenImage => canEdit && !isApplyingSplit && !isAddingSourceFolder && !disposed
            && string.IsNullOrWhiteSpace(DatasetContextPresentationService.BuildLabelingStartUnavailableHint(source.Count, false, true, Classes.Count));
        public bool CanAssignSplit => canEdit && !isApplyingSplit && !isAddingSourceFolder && HasSelection && !SelectedItems.Any(item => item.IsSaveRequired);
        public bool CanConfigureSplit => canEdit && !isApplyingSplit && !isAddingSourceFolder && !disposed && project != null && source.Count > 0 && !source.Any(item => item.IsSaveRequired);
        public bool IsSplitSettingsOpen { get => splitSettingsOpen; private set => SetProperty(ref splitSettingsOpen, value); }
        public int SplitValidationPercent { get => splitValidationPercent; set { if (SetProperty(ref splitValidationPercent, value)) InvalidateSplitPreview(); } }
        public int SplitTestPercent { get => splitTestPercent; set { if (SetProperty(ref splitTestPercent, value)) InvalidateSplitPreview(); } }
        public int SplitSeed { get => splitSeed; set { if (SetProperty(ref splitSeed, value)) InvalidateSplitPreview(); } }
        public bool StratifySplit { get => stratifySplit; set { if (SetProperty(ref stratifySplit, value)) InvalidateSplitPreview(); } }
        public bool FreezeTestSplit { get => freezeTestSplit; set { if (SetProperty(ref freezeTestSplit, value)) InvalidateSplitPreview(); } }
        public bool IsTestSplitLocked => settings.FreezeTestSplit && FreezeTestSplit;
        public int SplitTrainPercent => 100 - SplitValidationPercent - SplitTestPercent;
        public string SplitPreviewText => splitPreview == null ? splitError.Length > 0 ? LocalizationTextRuntimeService.Translate(splitError) : T("SplitSettings.PreviewRequired")
            : Format("SplitSettings.PreviewCounts", splitPreview.Counts["train"], splitPreview.Counts["valid"], splitPreview.Counts["test"])
                + Environment.NewLine + string.Join(Environment.NewLine, splitPreview.ClassCounts["train"].Keys.Select(key => SplitStratumText(key) + ": "
                    + splitPreview.ClassCounts["train"][key] + " / " + splitPreview.ClassCounts["valid"][key] + " / " + splitPreview.ClassCounts["test"][key]))
                + Environment.NewLine + T("SplitSettings.ActualDistribution");
        internal YoloDatasetSplitPlan SplitPreview => splitPreview;
        internal YoloDatasetSettings SplitDraft => new YoloDatasetSettings { ValidationPercent = SplitValidationPercent, TestPercent = SplitTestPercent, SplitSeed = SplitSeed, StratifySplit = StratifySplit, FreezeTestSplit = FreezeTestSplit };
        public bool IsApplyingSplit { get => isApplyingSplit; internal set { if (SetProperty(ref isApplyingSplit, value)) RefreshCommands(); } }
        public bool IsAnomalyPurpose => project?.ProjectSettings?.DatasetPurpose == LabelingDatasetPurpose.AnomalyDetection;
        public string SourceRoot => sourceRoot;
        public string SourceRootDisplayText => string.Join(" · ", sourceRoot.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).Select(path =>
        {
            string name = Path.GetFileName(path.TrimEnd('\\', '/'));
            return string.IsNullOrEmpty(name) ? path : name;
        }));
        public string OperationStatus => operationStatusKey == "Assigned"
            ? Format(operationStatusKey, operationStatusArguments[0], T("Split." + operationStatusArguments[1]))
            : string.IsNullOrEmpty(operationStatusKey) ? string.Empty : Format(operationStatusKey, operationStatusArguments);
        public int TotalCount => source.Count;
        public int UnworkedCount => source.Count(item => !HasRecordedWork(item));
        public int RejectedCount => source.Count(item => item.QualityReviewState == YoloImageQualityReviewState.NeedsFix);
        public int ReviewedCount => source.Count(item => HasSavedGroundTruth(item) && item.QualityReviewState == YoloImageQualityReviewState.Reviewed);
        public int TrainCount => source.Count(item => item.DatasetSplit == "train");
        public int ValidCount => source.Count(item => item.DatasetSplit == "valid");
        public int TestCount => source.Count(item => item.DatasetSplit == "test");
        public int ExcludedCount => source.Count(item => item.DatasetSplit == "excluded");
        public int TestCompletedCount => source.Count(item => item.DatasetSplit == "test" && HasSavedGroundTruth(item));
        public string TrainShare => $"{TrainCount}*";
        public string ValidShare => $"{ValidCount}*";
        public string TestShare => $"{TestCount}*";
        public string ExcludedShare => $"{ExcludedCount}*";
        public string ShownText => Format("Shown", items?.Count ?? 0);
        public string SourceSummaryText => Format("SourceSummary", TotalCount)
            + (unavailableSourceFolderCount > 0 ? Environment.NewLine + Format("AddFolder.MissingSources", unavailableSourceFolderCount) : string.Empty);
        public string ClassTitleText => Format("ClassTitle", Classes.Count);
        public string SplitSummaryText => $"{T("Split.train")} {TrainCount} · {T("Split.valid")} {ValidCount} · {T("Split.test")} {TestCount}{ExcludedSummaryText}";
        public string ExcludedSummaryText => ExcludedCount > 0 ? $" · {T("Split.excluded")} {ExcludedCount}" : string.Empty;
        public string SelectionText => Format("Selected", SelectedCount);
        public string UnworkedCheckText => Format("Check.Unworked", UnworkedCount);
        public string RejectedCheckText => Format("Check.Rejected", RejectedCount);
        public string TestCheckText => Format(IsAnomalyPurpose ? "Check.TestJudged" : "Check.Test", TestCompletedCount);
        public string ClassCheckText => Format("Check.Classes", Classes.Count(item => item.Count < 10));
        public string SplitCheckText => splitOverlap == null ? T("Check.SplitPending")
            : !string.IsNullOrEmpty(splitOverlap.EnumerationError) ? T("Check.SplitUnavailable")
            : splitOverlap.ContentOverlapCount == 0 ? T("Check.SplitClear") : Format("Check.SplitOverlap", splitOverlap.ContentOverlapCount);
        public string SplitCheckDetail => splitOverlap?.EnumerationError ?? string.Empty;
        public bool HasSplitCheckWarning => splitOverlap == null || !string.IsNullOrEmpty(splitOverlap.EnumerationError);
        public bool HasSplitOverlap => splitOverlap?.ContentOverlapCount > 0;
        public string ContinueText => UnworkedCount > 0 ? Format("Continue", UnworkedCount) : T("OpenLabeling");
        public string RemainingStageText => Format("RemainingStage", UnworkedCount);
        public bool HasUnworked => UnworkedCount > 0;
        public bool HasRejected => RejectedCount > 0;
        public bool HasSmallClasses => Classes.Any(item => item.Count < 10);
        public bool HasInsufficientTest => TestCompletedCount < 3;
        public bool IsEmpty => items?.IsEmpty != false;

        public string SearchText
        {
            get => searchText;
            set { if (SetProperty(ref searchText, value ?? string.Empty)) RefreshView(); }
        }

        public DatasetWorkspaceChoice SelectedSplit
        {
            get => selectedSplit;
            set { if (value != null && SetProperty(ref selectedSplit, value)) RefreshView(); }
        }

        public DatasetWorkspaceFilterItem SelectedFilter
        {
            get => selectedFilter;
            set { if (value != null && SetProperty(ref selectedFilter, value)) RefreshView(); }
        }

        public ICommand AssignSplitCommand => assignSplitCommand;
        public ICommand AddSourceFolderCommand => addSourceFolderCommand;
        public ICommand OpenSelectedCommand => openSelectedCommand;
        public ICommand ContinueLabelingCommand => continueLabelingCommand;
        public ICommand OpenImageCommand { get; }
        public ICommand ClearSelectionCommand { get; }
        public ICommand SelectionChangedCommand { get; }
        public ICommand OpenSplitSettingsCommand => openSplitSettingsCommand;
        public ICommand PreviewSplitCommand => previewSplitCommand;
        public ICommand ApplySplitSettingsCommand => applySplitSettingsCommand;
        public ICommand CancelSplitSettingsCommand { get; }
        public ICommand UnlockTestSplitCommand { get; }
        public ICommand OpenLabelSuspicionReviewCommand => openLabelSuspicionReviewCommand;
        public int LabelSuspicionImageCount => source.Count(item => item.LabelSuspicionCount > 0);
        public bool HasLabelSuspicionImages => LabelSuspicionImageCount > 0;
        public string LabelSuspicionCheckText => string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLanguageService.T("WpfLabelSuspicion.Dataset.Check"), LabelSuspicionImageCount);
        public string LabelSuspicionReviewText => string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLanguageService.T("WpfLabelSuspicion.Dataset.Action"), LabelSuspicionImageCount);
        private bool CanOpenLabelSuspicionReview => !disposed && !isApplyingSplit && !splitSettingsOpen && source.Count > 0 && canOpenLabelSuspicionReview();

        internal void ConfigureLabelSuspicionEntry(Action open, Func<bool> canOpen)
        {
            openLabelSuspicionReview = open ?? (() => { });
            canOpenLabelSuspicionReview = canOpen ?? (() => false);
            RefreshCommands();
        }

        internal void ConfigureSplitSettingsCommands(Action prepare, Action apply)
        { prepareSplit = prepare ?? throw new ArgumentNullException(nameof(prepare)); applySplit = apply ?? throw new ArgumentNullException(nameof(apply)); }

        private void OpenSplitSettings()
        {
            SplitValidationPercent = settings.ValidationPercent;
            SplitTestPercent = settings.TestPercent;
            SplitSeed = settings.SplitSeed;
            StratifySplit = settings.StratifySplit;
            FreezeTestSplit = settings.FreezeTestSplit;
            InvalidateSplitPreview();
            IsSplitSettingsOpen = true;
            RefreshCommands();
        }

        private void CancelSplitSettings()
        { IsSplitSettingsOpen = false; InvalidateSplitPreview(); RefreshCommands(); }

        private void InvalidateSplitPreview()
        {
            splitPreview = null;
            splitError = string.Empty;
            OnPropertyChanged(nameof(SplitPreviewText));
            OnPropertyChanged(nameof(SplitTrainPercent));
            OnPropertyChanged(nameof(IsTestSplitLocked));
            applySplitSettingsCommand?.RaiseCanExecuteChanged();
        }

        internal void SetSplitPreview(YoloDatasetSplitPlan value)
        { splitPreview = value; splitError = string.Empty; OnPropertyChanged(nameof(SplitPreviewText)); RefreshCommands(); }

        internal void ShowSplitSettingsFailure(string error)
        {
            splitPreview = null;
            splitError = string.Equals(error, OpenVisionLanguageService.GetDefaultEntries().First(entry => entry.Key == "WpfDatasetWorkspace.SplitSettings.SaveFailed").English, StringComparison.Ordinal)
                ? T("SplitSettings.SaveFailed") : error ?? string.Empty;
            OnPropertyChanged(nameof(SplitPreviewText));
            RefreshCommands();
        }

        internal void CompleteSplitSettings(int count)
        { CancelSplitSettings(); SetOperationStatus("SplitSettings.Applied", count); }

        internal void AttachCatalog(ObservableCollection<WpfImageQueueItem> catalog)
        {
            source.CollectionChanged -= OnCatalogChanged;
            foreach (WpfImageQueueItem item in subscribedItems) item.PropertyChanged -= OnItemChanged;
            subscribedItems.Clear();
            source = catalog ?? throw new ArgumentNullException(nameof(catalog));
            source.CollectionChanged += OnCatalogChanged;
            items = new ListCollectionView(source) { Filter = item => Matches((WpfImageQueueItem)item) };
            OnPropertyChanged(nameof(Items));
            RefreshRows();
        }

        internal void ConfigureCommands(Action<string> assign, Action<string> open)
        {
            assignSplit = assign ?? throw new ArgumentNullException(nameof(assign));
            openImage = open ?? throw new ArgumentNullException(nameof(open));
        }

        internal void ConfigureAddSourceFolderCommand(Action action) => addSourceFolder = action ?? (() => { });

        internal void SetAddingSourceFolder(bool value)
        {
            isAddingSourceFolder = value;
            RefreshCommands();
        }

        internal void ShowSourceFolderStatus(string key, params object[] arguments) => SetOperationStatus(key, arguments);

        internal void SetContext(LabelingProjectData data, string imageRoot, IReadOnlyDictionary<string, int> classCounts, int unavailableFolders = 0)
        {
            if (!ReferenceEquals(project, data)) { ClearSelection(); CancelSplitSettings(); SetOperationStatus(string.Empty); splitOverlap = null; }
            project = data;
            unavailableSourceFolderCount = unavailableFolders;
            if (IsAnomalyPurpose && selectedFilter?.Key == "noobj") selectedFilter = Filters[0];
            settings = data?.ProjectSettings?.YoloDataset ?? new YoloDatasetSettings();
            sourceRoot = string.IsNullOrWhiteSpace(imageRoot) ? string.Empty
                : data?.ProjectSettings?.AdditionalImageRootPaths?.Count > 0
                    ? string.Join(Environment.NewLine, data.ProjectSettings.ResolveImageRootPaths(imageRoot)) : imageRoot;
            Classes.Clear();
            if (!IsAnomalyPurpose)
            {
                for (int index = 0; index < (data?.ClassNamedList.Count ?? 0); index++)
                {
                    LabelClass label = data.ClassNamedList[index];
                    if (label == null) continue;
                    Classes.Add(new DatasetWorkspaceClassItem(new WpfClassCatalogListItem(label, index), classCounts?.GetValueOrDefault(label.Text ?? string.Empty) ?? 0));
                }
            }
            RefreshRows();
        }

        internal void ApplyWorkflowCommandState(WorkflowCommandState state)
        {
            canEdit = state?.CanSaveProjectConfig == true;
            RefreshCommands();
        }

        internal void RequestRefresh() => RefreshRequested?.Invoke();

        internal void SetSplitOverlap(YoloDatasetSplitOverlapSummary summary)
        {
            splitOverlap = summary;
            OnPropertyChanged(nameof(SplitCheckText));
            OnPropertyChanged(nameof(SplitCheckDetail));
            OnPropertyChanged(nameof(HasSplitCheckWarning));
            OnPropertyChanged(nameof(HasSplitOverlap));
        }

        internal void ShowAssignmentResult(int count, string split) => SetOperationStatus("Assigned", count, YoloDatasetSplitService.NormalizeAssignmentSplit(split));
        internal void ShowAssignmentFailure(string error) => SetOperationStatus("AssignFailed", error ?? string.Empty);

        private void SetOperationStatus(string key, params object[] arguments)
        {
            operationStatusKey = key;
            operationStatusArguments = arguments;
            OnPropertyChanged(nameof(OperationStatus));
        }

        private bool Matches(WpfImageQueueItem item)
        {
            if (item == null || selectedSplit?.Key != "all" && item.DatasetSplit != selectedSplit?.Key) return false;
            if (!string.IsNullOrWhiteSpace(searchText) && !item.FileName.Contains(searchText.Trim(), StringComparison.OrdinalIgnoreCase)) return false;
            return MatchesStatus(item, selectedFilter?.Key);
        }

        private static bool MatchesStatus(WpfImageQueueItem item, string key) => key switch
        {
            "todo" => !HasRecordedWork(item),
            "labeled" => HasSavedGroundTruth(item) && item.QualityReviewState == YoloImageQualityReviewState.Unreviewed,
            "reviewed" => HasSavedGroundTruth(item) && item.QualityReviewState == YoloImageQualityReviewState.Reviewed,
            "rejected" => item.QualityReviewState == YoloImageQualityReviewState.NeedsFix,
            "noobj" => item.HasSavedNoObjectLabel,
            _ => true
        };

        private static bool HasRecordedWork(WpfImageQueueItem item) => item.QualityReviewState == YoloImageQualityReviewState.NeedsFix || HasSavedGroundTruth(item);

        private static bool HasSavedGroundTruth(WpfImageQueueItem item) => item.IsAnomalyReviewItem
            ? item.AnomalyReviewState != AnomalyImageReviewState.Unreviewed : item.HasSavedLabelFile;

        private void OnCatalogChanged(object sender, NotifyCollectionChangedEventArgs e) => RefreshRows();

        internal void ApplyItemUpdates(Action apply)
        {
            ArgumentNullException.ThrowIfNull(apply);
            bool wasRefreshing = isRefreshing;
            isRefreshing = true;
            try { apply(); }
            finally
            {
                isRefreshing = wasRefreshing;
                if (!wasRefreshing) RefreshRows();
            }
        }

        private void RefreshRows()
        {
            if (isRefreshing || disposed) return;
            isRefreshing = true;
            try
            {
                foreach (WpfImageQueueItem old in subscribedItems.Where(item => !source.Contains(item)).ToArray())
                {
                    old.PropertyChanged -= OnItemChanged;
                    subscribedItems.Remove(old);
                }
                selectedPaths.RemoveWhere(path => !source.Any(item => string.Equals(item.ImagePath, path, StringComparison.OrdinalIgnoreCase)));
                foreach (WpfImageQueueItem item in source)
                {
                    if (subscribedItems.Add(item)) item.PropertyChanged += OnItemChanged;
                    item.DatasetSplit = YoloDatasetSplitService.SelectModesForImage(Path.GetFileNameWithoutExtension(item.ImagePath), settings).First();
                    item.IsDatasetGridSelected = selectedPaths.Contains(item.ImagePath);
                }
                foreach (DatasetWorkspaceFilterItem filter in Filters) filter.Update(source.Count(item => MatchesStatus(item, filter.Key)), IsAnomalyPurpose);
                if (IsAnomalyPurpose)
                {
                    Classes.Clear();
                    Classes.Add(new DatasetWorkspaceClassItem(T("Normal"), source.Count(item => item.AnomalyReviewState == AnomalyImageReviewState.Normal), WpfImageQueueItem.SuccessBrush, 1, isImageCount: true));
                    Classes.Add(new DatasetWorkspaceClassItem(T("Abnormal"), source.Count(item => item.AnomalyReviewState == AnomalyImageReviewState.Abnormal), WpfImageQueueItem.ErrorBrush, 2, isImageCount: true));
                }
                RefreshView();
                OnPropertyChanged(string.Empty);
                RefreshCommands();
            }
            finally { isRefreshing = false; }
        }

        private void OnItemChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(WpfImageQueueItem.LabelSuspicionCount))
            {
                OnPropertyChanged(nameof(LabelSuspicionImageCount)); OnPropertyChanged(nameof(HasLabelSuspicionImages));
                OnPropertyChanged(nameof(LabelSuspicionCheckText)); OnPropertyChanged(nameof(LabelSuspicionReviewText));
                openLabelSuspicionReviewCommand.RaiseCanExecuteChanged();
            }
            if (e.PropertyName is nameof(WpfImageQueueItem.HasSavedLabelFile) or nameof(WpfImageQueueItem.LabelStatus)
                or nameof(WpfImageQueueItem.QualityReviewState) or nameof(WpfImageQueueItem.AnomalyReviewState) or nameof(WpfImageQueueItem.IsSaveRequired))
                RefreshRows();
        }

        private void RefreshView()
        {
            if (items == null || disposed) return;
            bool wasRefreshing = isRefreshing;
            isRefreshing = true;
            try { items.Refresh(); }
            finally { isRefreshing = wasRefreshing; }
            OnPropertyChanged(nameof(ShownText));
            OnPropertyChanged(nameof(IsEmpty));
        }

        private void UpdateSelection(object value)
        {
            if (disposed || isRefreshing) return;
            var visibleSelected = new HashSet<string>((value as IEnumerable ?? Array.Empty<object>()).OfType<WpfImageQueueItem>().Select(item => item.ImagePath), StringComparer.OrdinalIgnoreCase);
            isRefreshing = true;
            try
            {
            foreach (WpfImageQueueItem item in source.Where(Matches))
            {
                if (visibleSelected.Contains(item.ImagePath)) selectedPaths.Add(item.ImagePath);
                else selectedPaths.Remove(item.ImagePath);
            }
            foreach (WpfImageQueueItem item in source) item.IsDatasetGridSelected = selectedPaths.Contains(item.ImagePath);
            }
            finally { isRefreshing = false; }
            RefreshSelection();
        }

        private void ClearSelection()
        {
            selectedPaths.Clear();
            bool wasRefreshing = isRefreshing;
            isRefreshing = true;
            try { foreach (WpfImageQueueItem item in source) item.IsDatasetGridSelected = false; }
            finally { isRefreshing = wasRefreshing; }
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(SelectedItems));
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(SelectionText));
            RefreshCommands();
        }

        private void RefreshCommands()
        {
            OnPropertyChanged(nameof(CanAssignSplit));
            assignSplitCommand.RaiseCanExecuteChanged();
            addSourceFolderCommand.RaiseCanExecuteChanged();
            openSelectedCommand.RaiseCanExecuteChanged();
            continueLabelingCommand.RaiseCanExecuteChanged();
            ((RelayCommand<WpfImageQueueItem>)OpenImageCommand).RaiseCanExecuteChanged();
            openSplitSettingsCommand.RaiseCanExecuteChanged();
            previewSplitCommand.RaiseCanExecuteChanged();
            applySplitSettingsCommand.RaiseCanExecuteChanged();
            ((RelayCommand)CancelSplitSettingsCommand).RaiseCanExecuteChanged();
            ((RelayCommand)UnlockTestSplitCommand).RaiseCanExecuteChanged();
            openLabelSuspicionReviewCommand.RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(LabelSuspicionImageCount)); OnPropertyChanged(nameof(HasLabelSuspicionImages));
            OnPropertyChanged(nameof(LabelSuspicionCheckText)); OnPropertyChanged(nameof(LabelSuspicionReviewText));
        }

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            foreach (DatasetWorkspaceChoice choice in SplitChoices) choice.RefreshText();
            foreach (DatasetWorkspaceClassItem item in Classes) item.RefreshText();
            foreach (WpfImageQueueItem item in source) item.RefreshLocalizedPresentation();
            RefreshRows();
        }

        private static string T(string key) => OpenVisionLanguageService.T("WpfDatasetWorkspace." + key);
        private static string SplitStratumText(string key) => key.StartsWith("class:", StringComparison.Ordinal) ? key.Substring(6)
            : key == "anomaly:Normal" ? T("Normal") : key == "anomaly:Abnormal" ? T("Abnormal") : T("Filter.noobj");
        private static string Format(string key, params object[] args) => string.Format(System.Globalization.CultureInfo.CurrentCulture, T(key), args);

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            WpfLocalizationViewModel.Instance.LanguageChanged -= OnLanguageChanged;
            source.CollectionChanged -= OnCatalogChanged;
            foreach (WpfImageQueueItem item in subscribedItems) item.PropertyChanged -= OnItemChanged;
            subscribedItems.Clear();
            selectedPaths.Clear();
            RefreshRequested = null;
            assignSplit = _ => { };
            openImage = _ => { };
            prepareSplit = () => { };
            applySplit = () => { };
            splitPreview = null;
            openLabelSuspicionReview = () => { };
            canOpenLabelSuspicionReview = () => false;
        }
    }

    public sealed class DatasetWorkspaceChoice : WpfObservableViewModel
    {
        private readonly string textKeyPrefix;
        public DatasetWorkspaceChoice(string key, string textKeyPrefix = "WpfDatasetWorkspace.Split.") { Key = key; this.textKeyPrefix = textKeyPrefix; }
        public string Key { get; }
        public string Text => OpenVisionLanguageService.T(textKeyPrefix + Key);
        internal void RefreshText() => OnPropertyChanged(nameof(Text));
    }

    public sealed class DatasetWorkspaceFilterItem : WpfObservableViewModel
    {
        private int count;
        private bool isAnomaly;
        public DatasetWorkspaceFilterItem(string key) { Key = key; }
        public string Key { get; }
        public string Caption => OpenVisionLanguageService.T("WpfDatasetWorkspace.Filter." + (isAnomaly && Key == "labeled" ? "judged" : Key));
        public int Count => count;
        public string Text => Caption + " " + Count;
        internal void Update(int value, bool anomaly) { count = value; isAnomaly = anomaly; OnPropertyChanged(nameof(Caption)); OnPropertyChanged(nameof(Count)); OnPropertyChanged(nameof(Text)); }
    }

    public sealed class DatasetWorkspaceClassItem : WpfObservableViewModel
    {
        public DatasetWorkspaceClassItem(WpfClassCatalogListItem item, int count) : this(item.Text, count, item.DrawBrush, item.CanonicalIndex + 1) { }
        public DatasetWorkspaceClassItem(string name, int count, Brush brush, int number, bool isImageCount = false) { Name = name; Count = count; DrawBrush = brush; Number = number; IsImageCount = isImageCount; }
        public string Name { get; }
        public int Count { get; }
        public Brush DrawBrush { get; }
        public int Number { get; }
        public bool IsImageCount { get; }
        public string CountText => string.Format(OpenVisionLanguageService.T("WpfDatasetWorkspace." + (IsImageCount ? "ImageCount" : "ObjectCount") + (Count == 1 ? "One" : string.Empty)), Count);
        internal void RefreshText() => OnPropertyChanged(nameof(CountText));
    }
}
