using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using OpenVisionLab;
using OpenVisionLab.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MvcVisionSystem
{
    public sealed class TrainingWorkspaceViewModel : WpfObservableViewModel, IDisposable
    {
        private TrainingRuntimeWorkflowService runtime;
        private TrainingWorkspaceContext context;
        private Func<IReadOnlyList<TrainingDatasetImageSelection>> captureImages = () => Array.Empty<TrainingDatasetImageSelection>();
        private Func<int> completedRunCount = () => 0;
        private Action refresh = () => { };
        private Action openDataset = () => { };
        private Action openEnvironment = () => { };
        private Action<WpfModelRegistryHistoryItem> openComparison = _ => { };
        private Action<WpfModelRegistryHistoryItem> openInference = _ => { };
        private bool canStart, nonTrainingBusy, disposed, onlyReviewed, showLive;
        private string epochsText = "100", imageSizeText = "640", batchSizeText = "16", status = string.Empty;
        private string statusKey = string.Empty;
        private object[] statusValues = Array.Empty<object>();
        private int selectedSettingsTabIndex;
        private TrainingHistoryItem selectedRun;
        private readonly RelayCommand startCommand, stopCommand, cancelCommand, repeatCommand, retrySaveCommand, keepFileCommand;

        public TrainingWorkspaceViewModel()
        {
            startCommand = new RelayCommand(() => { if (CanStart) ActiveExecution = StartAsync(); }, () => CanStart);
            stopCommand = new RelayCommand(() => { if (CanStop) runtime.StopQueueCurrent(); }, () => CanStop);
            cancelCommand = new RelayCommand(() => { if (IsBusy) runtime.CancelQueue(); }, () => IsBusy);
            repeatCommand = new RelayCommand(Repeat, () => CanRepeat);
            retrySaveCommand = new RelayCommand(RetrySave, () => CanRetrySave);
            keepFileCommand = new RelayCommand(KeepFile, () => CanRetrySave);
            NewCommand = new RelayCommand(() => { if (IsBusy) { showLive = true; selectedRun = null; } else { showLive = false; selectedRun = null; } RefreshPresentation(); });
            SelectRunCommand = new RelayCommand<TrainingHistoryItem>(item => { if (item == null || disposed) return; selectedRun = item; showLive = false; RefreshPresentation(); });
            SelectLiveCommand = new RelayCommand(() => { if (QueueRows.Count > 0) { showLive = true; selectedRun = null; RefreshPresentation(); } });
            ToggleModelCommand = new RelayCommand<TrainingModelChoice>(item => { if (!CanEdit || item?.IsAvailable != true) return; item.IsSelected = !item.IsSelected; RefreshPresentation(); }, item => CanEdit && item?.IsAvailable == true);
            OpenDatasetCommand = new RelayCommand(() => { if (CanEdit) openDataset(); }, () => CanEdit);
            OpenEnvironmentCommand = new RelayCommand(() => { if (CanEdit) openEnvironment(); }, () => CanEdit);
            OpenComparisonCommand = new RelayCommand(() => { if (CanOpenResult) openComparison(selectedRun.History); }, () => CanOpenResult);
            OpenInferenceCommand = new RelayCommand(() => { if (CanOpenResult && selectedRun.History.CanRunInferenceTrial) openInference(selectedRun.History); }, () => CanOpenResult && selectedRun.History.CanRunInferenceTrial);
            RefreshCommand = new RelayCommand(() => { if (CanEdit) refresh(); }, () => CanEdit);
            WpfLocalizationViewModel.Instance.LanguageChanged += OnLanguageChanged;
        }

        public WpfLocalizationViewModel Language => WpfLocalizationViewModel.Instance;
        public WpfYoloStatusPanelViewModel RuntimeStatus { get; private set; }
        public ObservableCollection<TrainingModelChoice> Models { get; } = new();
        public ObservableCollection<TrainingHistoryItem> History { get; } = new();
        public ObservableCollection<TrainingQueueRow> QueueRows { get; } = new();
        public ObservableCollection<string> Warnings { get; } = new();
        public ICommand StartCommand => startCommand;
        public ICommand StopCommand => stopCommand;
        public ICommand CancelCommand => cancelCommand;
        public ICommand RepeatCommand => repeatCommand;
        public ICommand RetrySaveCommand => retrySaveCommand;
        public ICommand KeepFileCommand => keepFileCommand;
        public ICommand NewCommand { get; }
        public ICommand SelectRunCommand { get; }
        public ICommand SelectLiveCommand { get; }
        public ICommand ToggleModelCommand { get; }
        public ICommand OpenDatasetCommand { get; }
        public ICommand OpenEnvironmentCommand { get; }
        public ICommand OpenComparisonCommand { get; }
        public ICommand OpenInferenceCommand { get; }
        public ICommand RefreshCommand { get; }
        internal event Action RefreshRequested;
        internal void RequestRefresh() => RefreshRequested?.Invoke();
        internal Task ActiveExecution { get; private set; } = Task.CompletedTask;
        public bool IsBusy => runtime?.IsQueueBusy == true;
        public bool IsPreparing => runtime?.IsQueuePreparing == true;
        public bool CanEdit => !disposed && !IsBusy && runtime?.HasPendingQueueCandidate != true && !nonTrainingBusy;
        public bool CanStart => CanEdit && canStart && context?.DatasetContentSha256?.Length == 64 && Models.Any(model => model.IsSelected && model.IsAvailable)
            && ParseSettings(out _, out _, out _) && HasUsableInput;
        public bool CanStop => IsBusy && !IsPreparing && runtime.QueueSnapshot.Current != null;
        public bool CanRetrySave => !disposed && !IsBusy && !nonTrainingBusy && runtime?.HasPendingQueueCandidate == true;
        public bool CanRepeat => CanEdit && selectedRun?.Parameters != null && Models.Any(model => model.ModelId == selectedRun.Parameters.ModelId && model.IsAvailable);
        public bool CanOpenResult => CanEdit && selectedRun?.History != null;
        public bool IsFormVisible => !showLive && selectedRun == null;
        public bool IsLiveVisible => showLive;
        public bool IsDetailVisible => selectedRun != null && !showLive;
        public int CompletedRunCount => completedRunCount();
        public bool HasCompletedRuns => CompletedRunCount > 0;
        public bool HasHistory => History.Count > 0;
        public bool HasQueue => IsBusy || showLive || runtime?.HasPendingQueueCandidate == true;
        public bool HasStatus => !string.IsNullOrWhiteSpace(status);
        public bool HasWarnings => Warnings.Count > 0;
        public bool HasValidationError => !ParseSettings(out _, out _, out _);
        public string ValidationError => HasValidationError ? T("SettingsRange") : string.Empty;
        public string Status => status;
        public string PendingWeightsPath => runtime?.PendingQueueCandidateWeightsPath ?? string.Empty;
        public string VersionText => context?.DatasetVersionId ?? "—";
        public string VersionDisplayText => context == null ? "—" : context.VersionLabels?.GetValueOrDefault(VersionText)?.DisplayText ?? RecipeDatasetVersionPresentationService.MissingLabel;
        public string RecipeName => context?.RecipeName ?? "—";
        public string InspectionModelName => context?.InspectionModelName ?? T("NoModel");
        public string InspectionModelBadgeText => context?.InspectionModelCaptionKey == "WpfTrainingWorkspace.InspectionModel" && !string.IsNullOrWhiteSpace(context.InspectionModelName)
            ? context.InspectionModelName : OpenVisionLanguageService.T("WpfTrainingSettings.NoInspectionModel");
        public string InspectionModelCaption => OpenVisionLanguageService.T(context?.InspectionModelCaptionKey ?? "WpfTrainingWorkspace.InspectionModel");
        public string EnvironmentText => context?.EnvironmentText ?? T("EnvironmentUnknown");
        public string HistoryCountText => F("HistoryCount", History.Count);
        public string InputCountText => F("InputCounts", EligibleImages.Count(image => image.Split == "train"), EligibleImages.Count(image => image.Split == "valid"), EligibleImages.Count(image => image.Split == "test"));
        public string ExcludedText => F("ExcludedCounts", context?.Images.Count(image => !image.HasSavedGroundTruth || image.IsSaveRequired) ?? 0,
            context?.Images.Count(image => image.HasSavedGroundTruth && image.QualityReviewState == YoloImageQualityReviewState.NeedsFix) ?? 0);
        public string SavedInputText => F("SavedInput", EligibleImages.Count, context?.ClassNames.Count ?? 0);
        public string SelectedCountText => Models.Any(model => model.IsSelected) ? F("SelectedModels", Models.Count(model => model.IsSelected)) : T("PickOne");
        public string StartText => F("StartModels", Models.Count(model => model.IsSelected));
        public string LiveCountText => F("RemainingModels", runtime?.QueueSnapshot.Items.Count(item => item.State is TrainingQueueItemState.Waiting or TrainingQueueItemState.Running) ?? 0);
        public string LiveModelText => runtime?.QueueSnapshot.Current is TrainingQueueItemSnapshot item ? ModelName(item.Model.ModelId) + " · " + F("EpochProgress", item.Epoch?.ToString() ?? "—", item.TotalEpochs?.ToString() ?? "—") : T("QueueFinished");
        public string LiveLogText => IsPreparing ? T("CapturingInput") : string.Join(Environment.NewLine, QueueRows.Select(row => row.ModelName + " · " + row.StateText + (row.Message.Length == 0 ? "" : " · " + row.Message)));
        public IReadOnlyList<TrainingEpochMetric> LiveMetrics => runtime?.QueueSnapshot.Current?.Metrics
            ?? (runtime?.HasPendingQueueCandidate == true ? runtime.QueueSnapshot.Items.LastOrDefault(item => item.State == TrainingQueueItemState.Failed)?.Metrics : null) ?? Array.Empty<TrainingEpochMetric>();
        public int LiveMetricEpochs => runtime?.QueueSnapshot.Plan?.Epochs ?? 1;
        public string LiveValidationCaption
        {
            get
            {
                string key = LiveMetrics.LastOrDefault(metric => metric.ValidationMetricValue.HasValue)?.ValidationMetricKey;
                return string.IsNullOrEmpty(key) ? T("ValidationCaption") : F("ValidationMetricCaption", T("Metric." + key));
            }
        }
        public int SelectedSettingsTabIndex
        {
            get => selectedSettingsTabIndex;
            set
            {
                if (disposed || value < 0 || value > 2 || !SetProperty(ref selectedSettingsTabIndex, value)) return;
                OnPropertyChanged(nameof(IsTrainingTabVisible));
                OnPropertyChanged(nameof(IsSettingsTabVisible));
            }
        }
        public bool IsTrainingTabVisible => selectedSettingsTabIndex == 0;
        public bool IsSettingsTabVisible => selectedSettingsTabIndex != 0;
        public TrainingHistoryItem SelectedRun => selectedRun;
        public bool OnlyReviewed { get => onlyReviewed; set { if (!CanEdit || !SetProperty(ref onlyReviewed, value)) return; RefreshPresentation(); } }
        public string EpochsText { get => epochsText; set { if (CanEdit && SetProperty(ref epochsText, value ?? string.Empty)) RefreshPresentation(); } }
        public string ImageSizeText { get => imageSizeText; set { if (CanEdit && SetProperty(ref imageSizeText, value ?? string.Empty)) RefreshPresentation(); } }
        public string BatchSizeText { get => batchSizeText; set { if (CanEdit && SetProperty(ref batchSizeText, value ?? string.Empty)) RefreshPresentation(); } }
        private IReadOnlyList<TrainingDatasetImageSelection> EligibleImages => context?.Images.Where(image => image.IsEligible(onlyReviewed)).ToArray() ?? Array.Empty<TrainingDatasetImageSelection>();
        private bool IsPatchCoreSelection => Models.Any(model => model.IsSelected) && Models.Where(model => model.IsSelected).All(model => model.ModelEngine == PythonModelSettings.EnginePatchCore);
        private bool HasUsableInput => IsPatchCoreSelection
            ? EligibleImages.Count(image => image.Split == "train" && image.AnomalyReviewState == AnomalyImageReviewState.Normal) >= 2
            : EligibleImages.Any(image => image.Split == "train") && EligibleImages.Any(image => image.Split == "valid");

        internal void Configure(TrainingRuntimeWorkflowService service, Func<IReadOnlyList<TrainingDatasetImageSelection>> images, Action refreshContext, Action dataset, Action environment,
            Action<WpfModelRegistryHistoryItem> comparison, Action<WpfModelRegistryHistoryItem> inference, Func<int> completedRuns = null, WpfYoloStatusPanelViewModel runtimeStatus = null)
        {
            if (runtime != null) runtime.QueueChanged -= OnQueueChanged;
            runtime = service ?? throw new ArgumentNullException(nameof(service)); captureImages = images ?? throw new ArgumentNullException(nameof(images));
            refresh = refreshContext ?? (() => { }); openDataset = dataset ?? (() => { }); openEnvironment = environment ?? (() => { });
            openComparison = comparison ?? (_ => { }); openInference = inference ?? (_ => { });
            completedRunCount = completedRuns ?? (() => 0);
            if (RuntimeStatus != null) RuntimeStatus.PropertyChanged -= OnRuntimeStatusChanged;
            RuntimeStatus = runtimeStatus;
            if (RuntimeStatus != null) RuntimeStatus.PropertyChanged += OnRuntimeStatusChanged;
            OnPropertyChanged(nameof(RuntimeStatus));
            runtime.QueueChanged += OnQueueChanged;
            OnQueueChanged(runtime.QueueSnapshot);
        }

        internal void SetContext(TrainingWorkspaceContext value)
        {
            if (disposed) return;
            bool changedRecipe = context?.RecipeIdentity != value?.RecipeIdentity;
            string selectedId = selectedRun?.History.TrainingRunId;
            var picked = Models.Where(model => model.IsSelected).Select(model => model.ModelId).ToHashSet(StringComparer.Ordinal);
            context = value;
            Models.Clear(); History.Clear();
            if (context != null)
            {
                foreach (TrainingModelChoice item in context.Models) { item.IsSelected = item.IsAvailable && !changedRecipe && picked.Contains(item.ModelId); Models.Add(item); }
                foreach (TrainingHistoryItem item in context.History) History.Add(item);
            }
            if (changedRecipe)
            {
                SelectedSettingsTabIndex = 0;
                selectedRun = null; showLive = IsBusy; status = string.Empty; statusKey = string.Empty;
                TrainingRunParameters saved = History.FirstOrDefault(item => item.Parameters != null)?.Parameters;
                onlyReviewed = saved?.OnlyReviewed ?? false;
                epochsText = saved?.Epochs.ToString(CultureInfo.InvariantCulture) ?? "100"; imageSizeText = saved?.ImageSize.ToString(CultureInfo.InvariantCulture) ?? "640"; batchSizeText = saved?.BatchSize.ToString(CultureInfo.InvariantCulture) ?? "16";
                foreach (TrainingModelChoice item in Models.Where(model => model.IsAvailable && !History.Any(run => run.Parameters?.ModelId == model.ModelId && run.VersionText == VersionText)).Take(2)) item.IsSelected = true;
            }
            else selectedRun = History.FirstOrDefault(item => item.History.TrainingRunId == selectedId);
            RefreshPresentation();
        }

        internal void ApplyWorkflowCommandState(WorkflowCommandState state)
        {
            canStart = state?.CanStartTraining == true; nonTrainingBusy = state?.NonTrainingBusy == true;
            RefreshPresentation();
        }

        private async Task StartAsync()
        {
            if (!CanStart || !ParseSettings(out int epochs, out int size, out int batch)) return;
            var models = Models.Where(model => model.IsSelected && model.IsAvailable).Select(model => new TrainingQueueModel(model.ModelId, model.ModelEngine, model.WeightsName, "uiq-" + Guid.NewGuid().ToString("N"))).ToArray();
            var plan = new TrainingQueuePlan(context.RecipeName, context.DatasetVersionId, context.DatasetContentSha256, models, epochs, size, batch, onlyReviewed);
            showLive = true; selectedRun = null; status = string.Empty; statusKey = string.Empty;
            try
            {
                bool accepted = await runtime.RunQueueAsync(plan, captureImages());
                if (!disposed && !accepted) SetStatus("StartUnavailable");
            }
            catch (OperationCanceledException) { if (!disposed) SetStatus("Canceled"); }
            catch (Exception error) { if (!disposed) SetStatus("Failed", error.Message); }
            finally
            {
                if (!disposed)
                {
                    refresh();
                    if (runtime.HasPendingQueueCandidate) showLive = true;
                    else if (runtime.QueueSnapshot.Items.LastOrDefault()?.State == TrainingQueueItemState.Completed)
                    {
                        SelectLatestCompletedResult(); SetStatus("Completed", runtime.QueueSnapshot.CompletedCount);
                    }
                    else if (runtime.QueueSnapshot.Items.Count > 0 && runtime.QueueSnapshot.Items.All(item => item.State is TrainingQueueItemState.Completed or TrainingQueueItemState.Canceled))
                    { selectedRun = null; showLive = false; SetStatus("Canceled"); }
                    RefreshPresentation();
                }
            }
        }

        private void Repeat()
        {
            if (!CanRepeat) return;
            TrainingRunParameters settings = selectedRun.Parameters;
            foreach (TrainingModelChoice model in Models) model.IsSelected = model.ModelId == settings.ModelId;
            epochsText = settings.Epochs.ToString(CultureInfo.InvariantCulture); imageSizeText = settings.ImageSize.ToString(CultureInfo.InvariantCulture); batchSizeText = settings.BatchSize.ToString(CultureInfo.InvariantCulture);
            onlyReviewed = settings.OnlyReviewed; selectedRun = null; showLive = false; status = string.Empty; statusKey = string.Empty;
            RefreshPresentation();
        }

        private void RetrySave()
        {
            if (!CanRetrySave) return;
            try { if (runtime.TrySavePendingQueueCandidate()) { refresh(); SelectLatestCompletedResult(); SetStatus("ResultSaved"); } }
            catch (Exception error) { SetStatus("Failed", error.Message); }
            RefreshPresentation();
        }

        private void SelectLatestCompletedResult()
        {
            string runId = runtime.QueueSnapshot.Items.LastOrDefault(item => item.State == TrainingQueueItemState.Completed)?.Model.RunId;
            selectedRun = History.FirstOrDefault(item => item.History.TrainingRunId == runId);
            showLive = selectedRun == null;
        }

        private void KeepFile()
        {
            if (!CanRetrySave) return;
            string path = PendingWeightsPath;
            if (runtime.KeepPendingQueueCandidateFile()) { selectedRun = null; showLive = false; SetStatus("ResultFileRetainedAt", path); }
        }

        private void OnQueueChanged(TrainingQueueSnapshot snapshot)
        {
            if (disposed) return;
            QueueRows.Clear();
            foreach (TrainingQueueItemSnapshot item in snapshot.Items) QueueRows.Add(new TrainingQueueRow(item, ModelName(item.Model.ModelId)));
            RefreshPresentation();
        }

        private void RefreshPresentation()
        {
            if (disposed) return;
            Warnings.Clear();
            if (context != null)
            {
                int testCount = EligibleImages.Count(image => image.Split == "test");
                if (testCount < 3) Warnings.Add(F("SmallTest", testCount));
                int rejected = context.Images.Count(image => image.HasSavedGroundTruth && image.QualityReviewState == YoloImageQualityReviewState.NeedsFix);
                if (rejected > 0) Warnings.Add(F("RejectedWarning", rejected));
                if (!HasUsableInput) Warnings.Add(T(IsPatchCoreSelection ? "NeedsNormalTrain" : "NeedsTrainValid"));
                if (IsPatchCoreSelection && !EligibleImages.Any(image => image.Split == "valid" && image.AnomalyReviewState == AnomalyImageReviewState.Normal)) Warnings.Add(T("CalibrationFallback"));
                if (!Models.Any(model => model.IsAvailable)) Warnings.Add(T("NeedsRuntime"));
                if (context.Purpose != LabelingDatasetPurpose.AnomalyDetection)
                {
                    IReadOnlyDictionary<string, int> counts = onlyReviewed ? context.ReviewedClassCounts : context.ClassCounts;
                    foreach (string name in context.ClassNames.Where(name => counts.GetValueOrDefault(name) < 10)) Warnings.Add(F("SmallClass", name, counts.GetValueOrDefault(name)));
                }
            }
            foreach (string property in new[]
            {
                nameof(IsBusy), nameof(IsPreparing), nameof(CanEdit), nameof(CanStart), nameof(CanStop), nameof(CanRetrySave), nameof(CanRepeat), nameof(CanOpenResult),
                nameof(CompletedRunCount), nameof(HasCompletedRuns), nameof(IsFormVisible), nameof(IsLiveVisible), nameof(IsDetailVisible), nameof(HasHistory), nameof(HasQueue), nameof(HasStatus), nameof(HasWarnings),
                nameof(HasValidationError), nameof(ValidationError), nameof(Status), nameof(PendingWeightsPath), nameof(VersionText), nameof(VersionDisplayText), nameof(RecipeName), nameof(InspectionModelName), nameof(InspectionModelBadgeText), nameof(InspectionModelCaption), nameof(EnvironmentText),
                nameof(HistoryCountText), nameof(InputCountText), nameof(ExcludedText), nameof(SavedInputText), nameof(SelectedCountText), nameof(StartText),
                nameof(LiveCountText), nameof(LiveModelText), nameof(LiveLogText), nameof(LiveMetrics), nameof(LiveMetricEpochs), nameof(LiveValidationCaption),
                nameof(SelectedRun), nameof(OnlyReviewed), nameof(EpochsText), nameof(ImageSizeText), nameof(BatchSizeText)
            }) OnPropertyChanged(property);
            foreach (TrainingHistoryItem item in History) item.IsSelected = ReferenceEquals(item, selectedRun);
            CommandManager.InvalidateRequerySuggested();
        }

        private bool ParseSettings(out int epochs, out int size, out int batch)
        {
            bool valid = int.TryParse(epochsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out epochs) & int.TryParse(imageSizeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out size) & int.TryParse(batchSizeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out batch);
            return valid && epochs is >= 1 and <= 1000 && size is >= 64 and <= 2048 && size % 32 == 0 && batch is >= 1 and <= 128;
        }
        private string ModelName(string id) => Models.FirstOrDefault(model => model.ModelId == id)?.Name ?? id;
        internal void SetStatus(string key, params object[] values) { statusKey = key; statusValues = values; status = F(key, values); RefreshPresentation(); }
        internal void SetSnapshotError(string error)
        {
            if (!string.IsNullOrEmpty(error)) SetStatus("SnapshotFailed", error);
            else if (statusKey == "SnapshotFailed") { statusKey = string.Empty; status = string.Empty; RefreshPresentation(); }
        }
        private void OnLanguageChanged(object sender, EventArgs args)
        {
            if (statusKey.Length > 0) status = F(statusKey, statusValues);
            foreach (TrainingModelChoice model in Models) model.RefreshText();
            foreach (TrainingHistoryItem item in History) item.RefreshText();
            foreach (TrainingQueueRow row in QueueRows) row.RefreshText();
            OnPropertyChanged(nameof(RuntimeStatus));
            RequestRefresh(); RefreshPresentation();
        }
        private void OnRuntimeStatusChanged(object sender, PropertyChangedEventArgs args)
        {
            if (!disposed && args.PropertyName == nameof(WpfYoloStatusPanelViewModel.CommandProgressVisibility)
                && RuntimeStatus?.CommandProgressVisibility == System.Windows.Visibility.Collapsed) refresh();
        }
        internal static string T(string key) => OpenVisionLanguageService.T("WpfTrainingWorkspace." + key);
        internal static string F(string key, params object[] values) => string.Format(CultureInfo.CurrentCulture, T(key), values);
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; WpfLocalizationViewModel.Instance.LanguageChanged -= OnLanguageChanged;
            if (runtime != null) runtime.QueueChanged -= OnQueueChanged;
            if (RuntimeStatus != null) RuntimeStatus.PropertyChanged -= OnRuntimeStatusChanged;
            RuntimeStatus = null;
            completedRunCount = () => 0; captureImages = () => Array.Empty<TrainingDatasetImageSelection>(); refresh = () => { }; openDataset = () => { }; openEnvironment = () => { }; openComparison = _ => { }; openInference = _ => { };
        }
    }

    internal sealed record TrainingWorkspaceContext(string RecipeIdentity, string RecipeName, LabelingDatasetPurpose Purpose, string DatasetVersionId, string DatasetContentSha256,
        IReadOnlyList<TrainingDatasetImageSelection> Images, IReadOnlyList<string> ClassNames, IReadOnlyDictionary<string, int> ClassCounts, IReadOnlyDictionary<string, int> ReviewedClassCounts,
        IReadOnlyList<TrainingModelChoice> Models, IReadOnlyList<TrainingHistoryItem> History, string InspectionModelName, string EnvironmentText, string InspectionModelCaptionKey = "WpfTrainingWorkspace.InspectionModel", IReadOnlyDictionary<string, RecipeDatasetVersionPresentation> VersionLabels = null);

    public sealed class TrainingModelChoice : WpfObservableViewModel
    {
        private bool isSelected;
        internal TrainingModelChoice(string id, string name, string engine, string weights, string noteKey, bool available, string unavailableKey)
        { ModelId = id; Name = name; ModelEngine = engine; WeightsName = weights; NoteKey = noteKey; IsAvailable = available; UnavailableKey = unavailableKey; }
        public string ModelId { get; }
        public string Name { get; }
        public string ModelEngine { get; }
        public string WeightsName { get; }
        private string NoteKey { get; }
        private string UnavailableKey { get; }
        public bool IsAvailable { get; }
        public bool IsSelected { get => isSelected; internal set { if (SetProperty(ref isSelected, value)) OnPropertyChanged(nameof(SelectionGlyph)); } }
        public string SelectionGlyph => IsSelected ? "✓" : string.Empty;
        public string Note => TrainingWorkspaceViewModel.T(NoteKey);
        public string AvailabilityText => IsAvailable ? string.Empty : TrainingWorkspaceViewModel.T(UnavailableKey);
        internal void RefreshText() { OnPropertyChanged(nameof(Note)); OnPropertyChanged(nameof(AvailabilityText)); }
    }

    public sealed class TrainingHistoryItem : WpfObservableViewModel
    {
        private bool isSelected;
        private readonly LabelingDatasetPurpose purpose;
        private readonly ModelComparisonWorkspaceRow evaluation;
        private readonly RecipeDatasetVersionPresentation versionLabel;
        private readonly string runName;
        private readonly string modelId;
        internal TrainingHistoryItem(WpfModelRegistryHistoryItem history, TrainingRun run, string name, LabelingDatasetPurpose task = LabelingDatasetPurpose.ObjectDetection, WpfModelBenchmarkRun report = null, RecipeDatasetVersionPresentation datasetVersionLabel = null)
        {
            History = history; Parameters = run?.Parameters?.Copy(); EventUtc = run?.EventUtc ?? string.Empty; purpose = task;
            runName = string.IsNullOrEmpty(run?.RunName) ? name : run.RunName; modelId = run?.Parameters?.ModelId; versionLabel = datasetVersionLabel;
            var row = new ModelComparisonWorkspaceRow(history, report, run?.DatasetVersionId, run?.DatasetContentSha256, report?.Confidence ?? double.NaN, MetricKeys);
            evaluation = row.IsComparable && report.SourceSha256.Length == 64 ? row : new ModelComparisonWorkspaceRow(history, null, run?.DatasetVersionId, run?.DatasetContentSha256, double.NaN, MetricKeys);
        }
        internal WpfModelRegistryHistoryItem History { get; }
        internal TrainingRunParameters Parameters { get; }
        public string Name => ModelRegistryTextPresentationService.FormatModelCaption(History.ModelEngine, runName, EventUtc, History.WeightsPath, modelId, trainingRunId: History.TrainingRunId);
        private string EventUtc { get; }
        public bool IsSelected { get => isSelected; internal set => SetProperty(ref isSelected, value); }
        public bool IsInspectionModel => History.IsCurrentInspectionModel;
        private string DateText => DateTime.TryParse(EventUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime date) ? date.ToLocalTime().ToString("MM-dd", CultureInfo.CurrentCulture) : "—";
        public string RoleText => IsInspectionModel ? TrainingWorkspaceViewModel.T("InspectionModel") : DateText;
        public string VersionText => History.DatasetVersionId.Length == 0 ? "—" : History.DatasetVersionId;
        public string VersionDisplayText => versionLabel?.DisplayText ?? RecipeDatasetVersionPresentationService.MissingLabel;
        public string IdentityText => History.DatasetVersionId + Environment.NewLine + History.TrainingRunId;
        private IReadOnlyList<string> MetricKeys => purpose switch
        {
            LabelingDatasetPurpose.Segmentation => new[] { "miou", "dice", "map50", "takt" },
            LabelingDatasetPurpose.AnomalyDetection => new[] { "auroc", "fnr", "fpr", "threshold", "takt" },
            _ => new[] { "map50", "map5095", "precision", "recall", "takt" }
        };
        public IReadOnlyList<ModelComparisonMetricCell> MetricCells => evaluation.MetricCells.Select(cell => new ModelComparisonMetricCell(cell.Key, cell.Text, false, purpose)).ToArray();
        public double MetricCardsMinimumWidth => MetricKeys.Count * (130 + 12);
        public string MetricText => evaluation.PrimaryMetricText;
        public string SummaryText => VersionDisplayText + " · " + ModelComparisonWorkspaceViewModel.FormatMetricCaption(MetricKeys[0], purpose) + " " + MetricText;
        public string SettingsText => TrainingWorkspaceViewModel.F("RunSettings", VersionDisplayText, DateText, Parameters?.Epochs.ToString() ?? "—", Parameters?.ImageSize.ToString() ?? "—", Parameters?.BatchSize.ToString() ?? "—");
        public string DurationText => Parameters?.DurationSeconds is double duration && double.IsFinite(duration) && duration >= 0 ? TrainingWorkspaceViewModel.F("DurationMinutes", duration / 60) : "—";
        public string EvaluationNote => evaluation.Run != null ? TrainingWorkspaceViewModel.F("VerifiedEvaluation", VersionDisplayText, evaluation.Run.EvidenceCount) : TrainingWorkspaceViewModel.T("EvaluationPending");
        internal void RefreshText() { foreach (string name in new[] { nameof(Name), nameof(VersionDisplayText), nameof(RoleText), nameof(MetricText), nameof(MetricCells), nameof(SummaryText), nameof(SettingsText), nameof(DurationText), nameof(EvaluationNote) }) OnPropertyChanged(name); }
    }

    public sealed class TrainingQueueRow : WpfObservableViewModel
    {
        private readonly TrainingQueueItemSnapshot item;
        internal TrainingQueueRow(TrainingQueueItemSnapshot item, string name) { this.item = item; ModelName = name; }
        public string ModelName { get; }
        public string Message => item.Message;
        public string StateText => TrainingWorkspaceViewModel.T("State." + item.State);
        public string Glyph => item.State switch { TrainingQueueItemState.Completed => "✓", TrainingQueueItemState.Running => "▶", TrainingQueueItemState.Failed => "!", TrainingQueueItemState.Canceled => "×", _ => "·" };
        public bool IsRunning => item.State == TrainingQueueItemState.Running;
        public double Progress => item.ProgressPercent ?? 0;
        public bool IsIndeterminate => IsRunning && !item.ProgressPercent.HasValue;
        public string ProgressText => IsRunning ? TrainingWorkspaceViewModel.F("EpochProgress", item.Epoch?.ToString() ?? "—", item.TotalEpochs?.ToString() ?? "—") : StateText;
        internal void RefreshText() { OnPropertyChanged(nameof(StateText)); OnPropertyChanged(nameof(ProgressText)); }
    }
}
