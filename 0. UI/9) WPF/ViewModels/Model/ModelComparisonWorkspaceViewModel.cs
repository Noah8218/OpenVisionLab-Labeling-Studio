using OpenVisionLab;
using OpenVisionLab.Mvvm;
using MvcVisionSystem._1._Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace MvcVisionSystem
{
    // Owns comparison selection and presentation. The existing workflow adapter
    // supplies read-only evidence and explicit execution/adoption callbacks.
    public sealed class ModelComparisonWorkspaceViewModel : WpfObservableViewModel, IDisposable
    {
        private IReadOnlyList<WpfModelRegistryHistoryItem> history = Array.Empty<WpfModelRegistryHistoryItem>();
        private IReadOnlyList<WpfModelBenchmarkRun> reports = Array.Empty<WpfModelBenchmarkRun>();
        private IReadOnlyDictionary<string, string> weightsHashes = new Dictionary<string, string>();
        private IReadOnlyDictionary<string, long> weightsSizes = new Dictionary<string, long>();
        private IReadOnlyDictionary<string, double> displayModelSizesMegabytes;
        private LabelingDatasetPurpose purpose;
        private IReadOnlyDictionary<string, RecipeDatasetVersionPresentation> versionLabels = new Dictionary<string, RecipeDatasetVersionPresentation>();
        private string currentVersion = string.Empty;
        private string currentContentHash = string.Empty;
        private string selectedVersion = string.Empty;
        private string runtimeEngine = string.Empty;
        private string runtimeRoot = string.Empty;
        private string projectIdentity = string.Empty;
        private string sortKey = "map50";
        private double confidence;
        private bool canRun;
        private bool isReadOnlyDisplayContext;
        private bool isBusy;
        private bool errorsOnly;
        private string agreementFilter = string.Empty;
        private bool showGroundTruth = true;
        private bool refreshing;
        private bool disposed;
        private bool isRuleTab;
        private IReadOnlyList<TrainingRun> ruleRuns = Array.Empty<TrainingRun>();
        private IReadOnlyList<string> ruleClassNames = Array.Empty<string>();
        private string ruleReviewHash = string.Empty;
        private ModelComparisonWorkspaceRow selectedRuleModel;
        private ModelDecisionRule savedRule;
        private ModelDecisionRule ruleDraftIdentity;
        private ModelDecisionEvaluationCache decisionCache;
        private string ruleConfidence = "0.25", ruleMinimumSize = "0", ruleMinimumCount = "1", ruleAnomalyThreshold = string.Empty;
        private bool ruleDirty;
        private string ruleOperationError = string.Empty;
        private Action<TrainingRun, ModelDecisionRule> saveDecisionRule;
        private Action<TrainingRun> evaluateDecisionRule;
        private Action<TrainingRun> undoDecisionRule;


        private ModelComparisonWorkspaceRow selectedA;
        private ModelComparisonWorkspaceRow selectedB;
        private ModelComparisonImagePair selectedImage;
        private ImageSource baselinePreview;
        private ImageSource candidatePreview;
        private IReadOnlyList<Color> classColors = Array.Empty<Color>();
        private string previewError = string.Empty;
        private string status = string.Empty;
        private string statusKey = string.Empty;
        private object[] statusArguments = Array.Empty<object>();
        private bool isAdoptionConfirmationOpen;
        private bool isAdopting;
        private string adoptionReason = string.Empty;
        private ModelComparisonWorkspaceRow adoptionRow;
        private string undoAdoptionId = string.Empty;
        private string undoNoticeModelName = string.Empty;
        private bool isAdoptionUndoNoticeVisible;
        private bool isUndoing;
        private Action runComparison = () => { };
        private Action<ModelComparisonWorkspaceRow, string> adoptModel = (_, _) => { };
        private Action<ModelComparisonImagePair, bool> loadPreview = (_, _) => { };
        private Action<string> reviewLabels = _ => { };
        private Action<string> undoAdoption = _ => { };
        private readonly RelayCommand runCommand;
        private readonly RelayCommand adoptCommand;
        private readonly RelayCommand reviewLabelsCommand;
        private readonly RelayCommand confirmAdoptionCommand;
        private readonly RelayCommand undoAdoptionCommand;

        public ModelComparisonWorkspaceViewModel()
        {
            ImageView = new ListCollectionView(Images) { Filter = item => (!ErrorsOnly || ((ModelComparisonImagePair)item).ErrorCount > 0)
                && (agreementFilter.Length == 0 || ((ModelComparisonImagePair)item).Agreement == agreementFilter) };
            SelectAgreementCommand = new RelayCommand<string>(key => AgreementFilter = key == "all" || key == agreementFilter ? string.Empty : key ?? string.Empty, _ => HasImageReview);
            SelectACommand = new RelayCommand<ModelComparisonWorkspaceRow>(row => SelectedA = row, row => CanChooseModels && row?.IsComparable == true);
            SelectBCommand = new RelayCommand<ModelComparisonWorkspaceRow>(row => SelectedB = row, row => CanChooseModels && row?.IsComparable == true);
            SortCommand = new RelayCommand<string>(key => { sortKey = key; RefreshRows(); }, key => CanChooseModels && MetricKeys.Contains(key));
            runCommand = new RelayCommand(() => { if (!isReadOnlyDisplayContext) runComparison(); }, () => CanRunComparison);
            adoptCommand = new RelayCommand(BeginAdoption, () => CanAdoptModel);
            confirmAdoptionCommand = new RelayCommand(ConfirmAdoption, () => !isReadOnlyDisplayContext && isAdoptionConfirmationOpen && !isBusy && !isAdopting && !string.IsNullOrWhiteSpace(AdoptionReason));
            undoAdoptionCommand = new RelayCommand(UndoAdoption, () => CanUndoAdoption);
            CancelAdoptionCommand = new RelayCommand(CancelAdoption, () => isAdoptionConfirmationOpen && !isBusy && !isAdopting);
            reviewLabelsCommand = new RelayCommand(() => { if (!isReadOnlyDisplayContext) reviewLabels(SelectedImage.ImagePath); }, () => !isReadOnlyDisplayContext && CanChooseModels && SelectedImage != null);
            ReevaluateCommand = new RelayCommand<ModelComparisonWorkspaceRow>(Reevaluate, row => !isReadOnlyDisplayContext && CanChooseModels && canRun && SelectedVersion == currentVersion && Models.Any(other => other != row && CanEvaluatePair(other, row)));
            ShowRuleCommand = new RelayCommand<ModelComparisonWorkspaceRow>(row => { if (row != null) SelectedRuleModel = row; IsRuleTab = true; }, _ => !disposed && HasModels && CanChooseModels);
            ShowPerformanceCommand = new RelayCommand(() => IsRuleTab = false);
            ShowCandidateRuleCommand = new RelayCommand(() => { SelectedRuleModel = SelectedB; IsRuleTab = true; }, () => CanChooseModels && SelectedB != null);
            SaveDecisionRuleCommand = new RelayCommand(SaveDecisionRule, () => CanSaveDecisionRule);
            EvaluateDecisionRuleCommand = new RelayCommand(() => { if (evaluateDecisionRule != null) evaluateDecisionRule(FindRuleRun()); else { ruleOperationError = "RuleNeedsEvaluation"; RefreshRulePresentation(); } }, () => CanEditDecisionRule && FindRuleRun() != null && evaluateDecisionRule != null);
            RestoreDecisionRuleCommand = new RelayCommand(RestoreDecisionRule, () => CanEditDecisionRule);
            UndoDecisionRuleSaveCommand = new RelayCommand(() => undoDecisionRule?.Invoke(FindRuleRun()), () => CanEditDecisionRule && undoDecisionRule != null && FindRuleRun()?.DecisionRuleUndoAvailable == true);
            OpenDecisionErrorImageCommand = new RelayCommand<ModelDecisionErrorImage>(image => reviewLabels(image.ImagePath), image => CanChooseModels && !isReadOnlyDisplayContext && image != null);
            DefaultDecisionRuleCommand = new RelayCommand(() => { SetRuleDraft(CreateDefaultRule()); ruleDirty = true; RefreshRulePresentation(); }, () => CanEditDecisionRule);
            ZeroMissDecisionRuleCommand = new RelayCommand(() =>
            {
                double? threshold = ModelComparisonReviewService.FindZeroMissDecisionThreshold(BuildRuleDraft(), decisionCache);
                if (threshold.HasValue) { if (IsAnomalyDecisionRule) RuleAnomalyThreshold = threshold.Value.ToString("R", CultureInfo.CurrentCulture); else RuleConfidence = threshold.Value.ToString("R", CultureInfo.CurrentCulture); }
            }, () => CanEditDecisionRule && ModelComparisonReviewService.FindZeroMissDecisionThreshold(BuildRuleDraft(), decisionCache).HasValue);
            WpfLocalizationViewModel.Instance.LanguageChanged += OnLanguageChanged;
        }

        internal event Action RefreshRequested;
        public ObservableCollection<ModelComparisonWorkspaceRow> Models { get; } = new();
        public ObservableCollection<string> Versions { get; } = new();
        public ObservableCollection<KeyValuePair<string, string>> VersionChoices { get; } = new();
        public ObservableCollection<ModelComparisonImagePair> Images { get; } = new();
        public ObservableCollection<ModelComparisonClassMetricRow> ClassMetrics { get; } = new();
        // A/B analysis row (prototype analysisRow): differences, AP per class, confidence sweep, per-image agreement.
        public ObservableCollection<ModelComparisonDifferenceRow> Differences { get; } = new();
        public ObservableCollection<ModelComparisonAgreementGroup> AgreementGroups { get; } = new();
        public ICommand SelectAgreementCommand { get; }
        public bool HasAnalysis => HasComparablePair && IsLocalizationTask;
        public string AnalysisNote => HasComparablePair ? Format("AnalysisNote", SelectedA.Run.EvidenceCount) : string.Empty;
        public IReadOnlyList<WpfModelBenchmarkThresholdReview> SweepA => HasComparablePair ? SelectedA.Run.GroundTruthReview?.ThresholdSweep : null;
        public IReadOnlyList<WpfModelBenchmarkThresholdReview> SweepB => HasComparablePair ? SelectedB.Run.GroundTruthReview?.ThresholdSweep : null;
        public double SweepConfidence => confidence;
        public string SweepCurrentText => Format("SweepCurrent", confidence.ToString("0.00", CultureInfo.CurrentCulture));
        public string SweepBestText
        {
            get
            {
                WpfModelBenchmarkThresholdReview a = ConfidenceSweepChart.Usable(SweepA).OrderByDescending(point => point.F1.Value).FirstOrDefault();
                WpfModelBenchmarkThresholdReview b = ConfidenceSweepChart.Usable(SweepB).OrderByDescending(point => point.F1.Value).FirstOrDefault();
                return a == null || b == null ? string.Empty : Format("SweepBest", a.Confidence.ToString("0.00", CultureInfo.CurrentCulture), a.F1.Value.ToString("0.000", CultureInfo.CurrentCulture),
                    b.Confidence.ToString("0.00", CultureInfo.CurrentCulture), b.F1.Value.ToString("0.000", CultureInfo.CurrentCulture));
            }
        }
        public bool HasAgreementFilter => agreementFilter.Length > 0;
        public string AgreementFilter
        {
            get => agreementFilter;
            set
            {
                if (!SetProperty(ref agreementFilter, value ?? string.Empty)) return;
                foreach (ModelComparisonAgreementGroup group in AgreementGroups) group.IsSelected = group.Key == agreementFilter;
                ImageView.Refresh(); SelectFirstShownImage();
                OnPropertyChanged(nameof(HasAgreementFilter)); OnPropertyChanged(nameof(ImagePositionText));
            }
        }
        public System.ComponentModel.ICollectionView ImageView { get; }
        public ICommand SelectACommand { get; }
        public ICommand SelectBCommand { get; }
        public ICommand SortCommand { get; }
        public WpfLocalizationViewModel Language => WpfLocalizationViewModel.Instance;
        public ICommand RunComparisonCommand => runCommand;
        public ICommand AdoptModelCommand => adoptCommand;
        public ICommand ConfirmAdoptionCommand => confirmAdoptionCommand;
        public ICommand CancelAdoptionCommand { get; }
        public ICommand UndoAdoptionCommand => undoAdoptionCommand;
        public ICommand ReviewLabelsCommand => reviewLabelsCommand;
        public ICommand ReevaluateCommand { get; }
        public ICommand ShowRuleCommand { get; }
        public ICommand ShowPerformanceCommand { get; }
        public ICommand ShowCandidateRuleCommand { get; }
        public ICommand UndoDecisionRuleSaveCommand { get; }
        public ICommand OpenDecisionErrorImageCommand { get; }
        public ICommand SaveDecisionRuleCommand { get; }
        public ICommand EvaluateDecisionRuleCommand { get; }
        public ICommand RestoreDecisionRuleCommand { get; }
        public ICommand DefaultDecisionRuleCommand { get; }
        public ICommand ZeroMissDecisionRuleCommand { get; }
        public ObservableCollection<ModelDecisionClassEditor> DecisionClasses { get; } = new();
        public bool IsRuleTab { get => isRuleTab; set { if (SetProperty(ref isRuleTab, value)) { OnPropertyChanged(nameof(IsPerformanceTab)); if (value && selectedRuleModel == null) SelectedRuleModel = Models.FirstOrDefault(row => row.History.IsCurrentInspectionModel) ?? Models.FirstOrDefault(); } } }
        public bool IsPerformanceTab => !IsRuleTab;
        public ModelComparisonWorkspaceRow SelectedRuleModel { get => selectedRuleModel; set { if (!refreshing && CanChooseModels && (value == null || Models.Contains(value)) && SetProperty(ref selectedRuleModel, value)) { decisionCache = null; RestoreDecisionRule(); } } }
        public bool IsAnomalyDecisionRule => purpose == LabelingDatasetPurpose.AnomalyDetection;
        public bool IsLocalizationDecisionRule => !IsAnomalyDecisionRule;
        public bool CanEditDecisionRule => !disposed && !isReadOnlyDisplayContext && !isBusy && CanChooseModels && FindRuleRun() != null;
        public bool CanSaveDecisionRule => CanEditDecisionRule && saveDecisionRule != null && RuleValidationError.Length == 0 && ValidationDecision.Status == "ready" && TestDecision.Status == "ready";
        public string RuleConfidence { get => ruleConfidence; set { if (SetProperty(ref ruleConfidence, value ?? string.Empty)) RuleDraftChanged(); } }
        public double RuleConfidenceSlider { get { double value = ParseRuleNumber(ruleConfidence); return double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0.25; } set => RuleConfidence = value.ToString("0.##", CultureInfo.CurrentCulture); }
        public string RuleMinimumSize { get => ruleMinimumSize; set { if (SetProperty(ref ruleMinimumSize, value ?? string.Empty)) RuleDraftChanged(); } }
        public string RuleMinimumCount { get => ruleMinimumCount; set { if (SetProperty(ref ruleMinimumCount, value ?? string.Empty)) RuleDraftChanged(); } }
        public string RuleAnomalyThreshold { get => ruleAnomalyThreshold; set { if (SetProperty(ref ruleAnomalyThreshold, value ?? string.Empty)) RuleDraftChanged(); } }
        internal ModelDecisionEvaluationCache DecisionEvaluationCache => decisionCache;
        public bool IsDecisionRuleUndoAvailable => FindRuleRun()?.DecisionRuleUndoAvailable == true;
        public string DecisionRuleSaveNotice => T("RuleSavedNotice");
        public double RuleAnomalySliderMinimum => AnomalyScoreRange().min;
        public double RuleAnomalySliderMaximum => AnomalyScoreRange().max;
        public double RuleAnomalySlider { get { double value = ParseRuleNumber(ruleAnomalyThreshold); return double.IsFinite(value) ? value : RuleAnomalySliderMinimum; } set => RuleAnomalyThreshold = value.ToString("R", CultureInfo.CurrentCulture); }
        public string ZeroMissThresholdText => ModelComparisonReviewService.FindZeroMissDecisionThreshold(BuildRuleDraft(), decisionCache) is double value ? value.ToString("0.###", CultureInfo.CurrentCulture)
            : decisionCache == null ? T("RuleNeedsEvaluation") : T("RuleZeroMissUnavailable");
        public PointCollection ValidationMissPoints => ThresholdGraphPoints(true);
        public PointCollection ValidationExtraPoints => ThresholdGraphPoints(false);
        public string ValidationMissCurveText => ValidationCurveText(true);
        public string ValidationExtraCurveText => ValidationCurveText(false);
        public double DecisionThresholdGraphX => ThresholdGraphX(IsAnomalyDecisionRule ? ParseRuleNumber(ruleAnomalyThreshold) : ParseRuleNumber(ruleConfidence));
        public double ZeroMissThresholdGraphX => ThresholdGraphX(ModelComparisonReviewService.FindZeroMissDecisionThreshold(BuildRuleDraft(), decisionCache) ?? double.NaN);
        public bool HasZeroMissThreshold => ModelComparisonReviewService.FindZeroMissDecisionThreshold(BuildRuleDraft(), decisionCache).HasValue;
        public string ThresholdGraphMinimumText => (IsAnomalyDecisionRule ? RuleAnomalySliderMinimum : .05).ToString("0.###", CultureInfo.CurrentCulture);
        public string ThresholdGraphMaximumText => (IsAnomalyDecisionRule ? RuleAnomalySliderMaximum : .95).ToString("0.###", CultureInfo.CurrentCulture);
        public IReadOnlyList<ModelDecisionErrorImage> DecisionErrorImages => new[] { ValidationDecision, TestDecision }.Where(evaluation => evaluation.Status == "ready")
            .SelectMany(evaluation => evaluation.MissImagePaths.Select(path => new ModelDecisionErrorImage(path, Format(evaluation.Split == "val" ? "RuleValidationMissImage" : "RuleTestMissImage", System.IO.Path.GetFileName(path))))
                .Concat(evaluation.ExtraImagePaths.Select(path => new ModelDecisionErrorImage(path, Format(evaluation.Split == "val" ? "RuleValidationExtraImage" : "RuleTestExtraImage", System.IO.Path.GetFileName(path)))))).ToArray();
        public string ThresholdGraphDataText => string.Join(Environment.NewLine, ModelComparisonReviewService.ScanDecisionThresholds(BuildRuleDraft(), decisionCache)
            .Where(point => point.Evaluation.Status == "ready").Select(point => Format("RuleGraphPoint", point.Threshold,
                FormatDecisionRate(point.Evaluation.Misses, point.Evaluation.NgCount),
                FormatDecisionRate(point.Evaluation.Extras, point.Evaluation.OkCount))));
        public string BaselinePartTestText => DescribeFixedTest(SelectedA);
        public string CandidatePartTestText => DescribeFixedTest(SelectedB);
        public string AdoptionPartTestText => DescribeFixedTest(adoptionRow, includeRuleStatus: false);
        public string RuleStateKey => RuleValidationError.Length > 0 && RuleValidationError == "RuleStale" ? "RuleStale" : ruleDirty ? "RuleUnsaved" : savedRule == null ? "RuleUnconfigured" : "RuleSaved";
        public string RuleStateText => T(RuleStateKey);
        public string RuleValidationError => ValidateRuleDraft(BuildRuleDraft());
        public string RuleStatusText => !string.IsNullOrEmpty(ruleOperationError) ? T(ruleOperationError) : RuleValidationError.Length > 0 ? T(RuleValidationError)
            : decisionCache == null ? T("RuleNeedsEvaluation") : ValidationDecision.Status != "ready" ? T(ValidationDecision.Status) : TestDecision.Status != "ready" ? T(TestDecision.Status) : string.Empty;
        public ModelDecisionEvaluation ValidationDecision => ModelComparisonReviewService.EvaluateDecisionRule(BuildRuleDraft(), decisionCache, "val");
        public ModelDecisionEvaluation TestDecision => ModelComparisonReviewService.EvaluateDecisionRule(BuildRuleDraft(), decisionCache, "test");
        public string RuleValidationText => FormatDecisionEvaluation(ValidationDecision);
        public string RuleTestText => FormatDecisionEvaluation(TestDecision);
        public string RuleValidationSampleText => DecisionSampleText(ValidationDecision, "RuleValidationSamples");
        public string RuleTestSampleText => DecisionSampleText(TestDecision, "RuleTestSamples");
        public string RuleValidationMissRateText => DecisionRateText(ValidationDecision, true);
        public string RuleValidationExtraRateText => DecisionRateText(ValidationDecision, false);
        public string RuleTestMissRateText => DecisionRateText(TestDecision, true);
        public string RuleTestExtraRateText => DecisionRateText(TestDecision, false);
        public string RuleValidationAccuracyText => DecisionAccuracyText(ValidationDecision);
        public string RuleTestAccuracyText => DecisionAccuracyText(TestDecision);
        public bool AreDecisionResultsReady => ValidationDecision.Status == "ready" && TestDecision.Status == "ready";
        public bool IsDecisionErrorEmptyState => AreDecisionResultsReady && DecisionErrorImages.Count == 0;
        public string DecisionErrorImagesTitleText => Format("RuleErrorImagesTitle", AreDecisionResultsReady ? DecisionErrorImages.Count.ToString(CultureInfo.CurrentCulture) : "—");
        public string RuleThresholdGraphTitle => T(IsAnomalyDecisionRule ? "RuleAnomalyGraphTitle" : "RuleConfidenceGraphTitle");
        public bool HasDecisionThresholdValue => double.IsFinite(IsAnomalyDecisionRule ? ParseRuleNumber(ruleAnomalyThreshold) : ParseRuleNumber(ruleConfidence));
        public double DecisionThresholdGraphLabelX => DecisionThresholdGraphX + (DecisionThresholdGraphX > 794 ? -226 : 6);
        public double ZeroMissThresholdGraphLabelX => ZeroMissThresholdGraphX + (ZeroMissThresholdGraphX > 794 ? -226 : 6);
        public System.Windows.TextAlignment DecisionThresholdGraphLabelAlignment => DecisionThresholdGraphX > 794 ? System.Windows.TextAlignment.Right : System.Windows.TextAlignment.Left;
        public System.Windows.TextAlignment ZeroMissThresholdGraphLabelAlignment => ZeroMissThresholdGraphX > 794 ? System.Windows.TextAlignment.Right : System.Windows.TextAlignment.Left;
        public string DecisionThresholdGraphLabelText => HasDecisionThresholdValue ? Format("RuleCurrentThresholdValue", IsAnomalyDecisionRule ? ParseRuleNumber(ruleAnomalyThreshold) : ParseRuleNumber(ruleConfidence)) : T("RuleUnconfigured");
        public string ZeroMissThresholdGraphLabelText => ModelComparisonReviewService.FindZeroMissDecisionThreshold(BuildRuleDraft(), decisionCache) is double value ? Format("RuleZeroMissThresholdValue", value) : string.Empty;
        public IReadOnlyList<KeyValuePair<double, string>> ThresholdGraphTicks
        {
            get
            {
                double min = IsAnomalyDecisionRule ? RuleAnomalySliderMinimum : .05, max = IsAnomalyDecisionRule ? RuleAnomalySliderMaximum : .95;
                return Enumerable.Range(1, 9).Select(index =>
                {
                    double value = IsAnomalyDecisionRule ? min + (max - min) * index / 10D : index / 10D;
                    return new KeyValuePair<double, string>(ThresholdGraphX(value), value.ToString("0.###", CultureInfo.CurrentCulture));
                }).ToArray();
            }
        }

        public string RuleSummaryText => BuildRuleDraft() is { } draft && ModelRegistryService.ValidateDecisionRule(draft).Length == 0
            ? IsAnomalyDecisionRule ? Format("RuleAnomalySummary", draft.AnomalyScoreThreshold) : Format("RuleDetectionSummary", draft.Confidence, draft.MinimumShortSidePixels, draft.MinimumDefectCount) : T("RuleUnconfigured");
        public string AdoptionDecisionRuleText => DescribeSavedDecisionRule(adoptionRow);
        public string AdoptionEngineChangeText => adoptionRow != null && !UsesCurrentRuntime(adoptionRow) && CanAdoptOnCurrentRuntime(adoptionRow)
            ? string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfComparisonWorkspace.AdoptionEngineChange"),
                PythonModelSettings.FormatModelEngineName(runtimeEngine), PythonModelSettings.FormatModelEngineName(adoptionRow.History.ModelEngine))
            : string.Empty;

        public bool HasModels => Models.Count > 0;
        public IReadOnlyList<string> MetricKeys => purpose switch
        {
            LabelingDatasetPurpose.Segmentation => new[] { "map50", "miou", "dice", "precision", "recall", "takt", "size" },
            LabelingDatasetPurpose.AnomalyDetection => new[] { "auroc", "accuracy", "fnr", "fpr", "threshold", "takt", "size" },
            _ => new[] { "map50", "map5095", "precision", "recall", "f1", "takt", "size", "trainingMinutes" }
        };
        public IReadOnlyList<KeyValuePair<string, string>> MetricHeaders => MetricKeys.Select(key => new KeyValuePair<string, string>(key, MetricCaption(key))).ToArray();
        public string PrimaryMetricCaption => MetricCaption(MetricKeys[0]);
        // Speed/accuracy chart: rows evaluated on the selected dataset version with both a processing
        // time and the primary metric. A new array per refresh so the chart re-renders on A/B changes.
        public IReadOnlyList<ModelComparisonWorkspaceRow> SpeedAccuracyRows => Models.Where(row => row.IsComparable && row.PrimaryMetric.HasValue && row.TaktValue.HasValue).ToArray();
        public bool HasSpeedAccuracyPoints => SpeedAccuracyRows.Count >= 2;
        public string SpeedCaption => MetricCaption("takt");

        // Same rule as the v2 prototype's recommend(): among the models evaluated on the selected dataset version,
        // the highest primary metric; when several are within 0.005 of it, the fastest of those. Needs two models.
        public ModelComparisonWorkspaceRow RecommendedRow { get; private set; }
        public bool HasRecommendation => RecommendedRow != null;
        public string RecommendationText => RecommendedRow == null ? string.Empty
            : string.Format(CultureInfo.CurrentCulture, T("Recommendation"), RecommendedRow.ModelName, recommendationReason);
        public string RecommendationReason => RecommendedRow == null ? string.Empty : "· " + recommendationReason;
        private string recommendationReason = string.Empty;

        private ModelComparisonWorkspaceRow FindRecommendedRow(out string reason)
        {
            reason = string.Empty;
            string key = MetricKeys[0];
            if (key is "takt" or "size" or "trainingMinutes" or "fnr" or "fpr") return null;
            ModelComparisonWorkspaceRow[] evaluated = Models.Where(row => row.IsComparable && row.PrimaryMetric.HasValue).ToArray();
            if (evaluated.Length < 2) return null;
            double top = evaluated.Max(row => row.PrimaryMetric.Value);
            ModelComparisonWorkspaceRow[] near = evaluated.Where(row => top - row.PrimaryMetric.Value <= 0.005).ToArray();
            ModelComparisonWorkspaceRow pick = near.Length > 1 ? near.OrderBy(row => row.TaktValue ?? double.PositiveInfinity).First() : near[0];
            reason = near.Length > 1 && pick.TaktValue.HasValue
                ? string.Format(CultureInfo.CurrentCulture, T("RecommendationFaster"), PrimaryMetricCaption, pick.TaktText)
                : string.Format(CultureInfo.CurrentCulture, T("RecommendationBest"), PrimaryMetricCaption, pick.PrimaryMetricText);
            return pick;
        }
        public bool HasClassMetrics => ClassMetrics.Count > 0;
        public bool IsLocalizationTask => purpose != LabelingDatasetPurpose.AnomalyDetection;
        public string ClassMetricCaption => T(purpose == LabelingDatasetPurpose.Segmentation ? "ClassIoU" : "ClassMetric");
        public double MetricTableMinimumWidth => 110 + 48 + 200 + 150 + MetricKeys.Count * 70 + 200;
        public bool HasImageReview => Images.Count > 0;
        public bool IsBusy { get => isBusy; internal set { if (SetProperty(ref isBusy, value)) RefreshCommands(); } }
        public bool CanRunComparison => !isReadOnlyDisplayContext && !isBusy && !isAdoptionConfirmationOpen && canRun && SelectedVersion == currentVersion && SelectedA != null && SelectedB != null && SelectedA != SelectedB && CanEvaluatePair(SelectedA, SelectedB);
        // A pair outside the configured runtime (for example YOLOv8 next to a configured YOLO11) runs as an
        // engine comparison: each model is evaluated with its own engine on the same test split.
        public bool UsesEngineComparison => SelectedA != null && SelectedB != null && !(UsesCurrentRuntime(SelectedA) && UsesCurrentRuntime(SelectedB)) && CanEvaluatePair(SelectedA, SelectedB);
        // Shown next to the run button: WPF does not show the tooltip of a disabled button, so the reason
        // the run is unavailable was invisible before (2026-11-20 circular-disk capture).
        // Only an actual block (busy, historical version, runtime mismatch, unavailable) is shown. The neutral "evaluate A
        // and B" hint while a pair is incomplete or the adoption dialog is open added a line that pushed the table down
        // on every A/B change (README capture r11, 2026-11-21).
        public string RunBlockedReason => CanRunComparison || isReadOnlyDisplayContext || isAdoptionConfirmationOpen || RunHint == T("RunHint") ? string.Empty : RunHint;
        public bool HasRunBlockedReason => !string.IsNullOrEmpty(RunBlockedReason);
        public bool CanAdoptModel => !isReadOnlyDisplayContext && CanChooseModels && SelectedVersion == currentVersion && HasComparablePair && HasImageReview && CanAdoptOnCurrentRuntime(SelectedB)
            && SelectedB?.History.CanPromoteToInspectionModel == true && !SelectedB.History.IsCurrentInspectionModel
            && SelectedB.Run.SourceSha256.Length == 64
            && SelectedB.History.WeightsSha256.Length == 64 && string.Equals(SelectedB.History.WeightsSha256, SelectedB.Run.WeightsSha256, StringComparison.OrdinalIgnoreCase);
        public bool CanChooseModels => !isBusy && !isAdopting && !isAdoptionConfirmationOpen;
        public bool IsAdoptionConfirmationOpen => isAdoptionConfirmationOpen;
        public bool IsAdoptionUndoNoticeVisible => isAdoptionUndoNoticeVisible;
        public bool HasUndoAdoptionTarget => !string.IsNullOrEmpty(undoAdoptionId);
        public bool CanUndoAdoption => !isReadOnlyDisplayContext && !disposed && CanChooseModels && !isUndoing && isAdoptionUndoNoticeVisible && HasUndoAdoptionTarget;
        public string AdoptionUndoNoticeText => Format("AdoptionNotice", undoNoticeModelName);
        public string AdoptionReason { get => adoptionReason; set { if (SetProperty(ref adoptionReason, value ?? string.Empty)) confirmAdoptionCommand.RaiseCanExecuteChanged(); } }
        public string AdoptionModelText => adoptionRow?.ModelName ?? string.Empty;
        public bool HasComparablePair => SelectedA?.IsComparable == true && SelectedB?.IsComparable == true && SelectedA != SelectedB && SelectedA.Run.EvaluationConditionKey == SelectedB.Run.EvaluationConditionKey;
        public bool ErrorsOnly { get => errorsOnly; set { if (SetProperty(ref errorsOnly, value)) { ImageView.Refresh(); SelectFirstShownImage(); OnPropertyChanged(nameof(ImagePositionText)); } } }
        public bool ShowGroundTruth { get => showGroundTruth; set { if (SetProperty(ref showGroundTruth, value)) RequestPreview(); } }
        public string SelectedVersionLabel => versionLabels.GetValueOrDefault(SelectedVersion)?.DisplayText ?? RecipeDatasetVersionPresentationService.MissingLabel;
        public string SelectionIdentityText => SelectedVersion + Environment.NewLine + (SelectedA?.History.TrainingRunId ?? string.Empty) + Environment.NewLine + (SelectedB?.History.TrainingRunId ?? string.Empty);
        public string SelectedVersion { get => selectedVersion; set { if (!refreshing && CanChooseModels && SetProperty(ref selectedVersion, value ?? string.Empty)) RefreshRows(); } }
        public KeyValuePair<string, string>? SelectedVersionChoice
        {
            get
            {
                foreach (KeyValuePair<string, string> choice in VersionChoices) if (choice.Key == SelectedVersion) return choice;
                return null;
            }
            set
            {
                // Refreshing labels removes the old item; the stored ID remains authoritative.
                if (!refreshing && value.HasValue) SelectedVersion = value.Value.Key;
                OnPropertyChanged(nameof(SelectedVersionChoice));
            }
        }
        public ModelComparisonWorkspaceRow SelectedA { get => selectedA; set { if (CanChooseModels && Models.Contains(value) && SetProperty(ref selectedA, value)) RefreshPair(); } }
        public ModelComparisonWorkspaceRow SelectedB { get => selectedB; set { if (CanChooseModels && Models.Contains(value) && SetProperty(ref selectedB, value)) RefreshPair(); } }
        public ModelComparisonImagePair SelectedImage { get => selectedImage; set { if (value == null || Images.Contains(value)) { if (SetProperty(ref selectedImage, value)) RequestPreview(); } } }
        public ImageSource BaselinePreview => baselinePreview;
        public ImageSource CandidatePreview => candidatePreview;
        public IReadOnlyList<Color> ClassColors => classColors;
        public string PreviewError => previewError;
        public string OperationStatus => status;
        public string BaselineTitle => "A · " + (SelectedA?.ModelName ?? T("None"));
        public string CandidateTitle => "B · " + (SelectedB?.ModelName ?? T("None"));
        public string BaselineImageErrors => !IsLocalizationTask || SelectedImage == null ? string.Empty : Format("ImageErrorSummary", SelectedImage.Baseline.FalsePositives.Count, SelectedImage.Baseline.FalseNegatives.Count);
        public string CandidateImageErrors => !IsLocalizationTask || SelectedImage == null ? string.Empty : Format("ImageErrorSummary", SelectedImage.Candidate.FalsePositives.Count, SelectedImage.Candidate.FalseNegatives.Count);
        public string ImageTitle => SelectedImage?.ImageName ?? T("ImagesUnavailable");
        public string ImagePositionText => Format("ImagePosition", SelectedImage == null ? 0 : ImageView.Cast<object>().ToList().IndexOf(SelectedImage) + 1, ImageView.Cast<object>().Count());
        public string EvidenceText => HasComparablePair ? purpose == LabelingDatasetPurpose.AnomalyDetection ? Format("AnomalyEvidence", SelectedA.Run.EvidenceCount)
            : Format("Evidence", SelectedA.Run.EvidenceCount, confidence, SelectedA.Run.GroundTruthReview.IouThreshold) : T("PairUnavailable");
        public string RunHint => IsBusy ? T("Busy") : SelectedVersion != currentVersion ? T("HistoricalReadOnly") : SelectedA == null || SelectedB == null || SelectedA == SelectedB ? T("RunHint") : !CanEvaluatePair(SelectedA, SelectedB) ? T("RuntimeMismatch") : !canRun ? T("RunUnavailable") : T("RunHint");
        public string BaselineMisses => FormatCount(HasComparablePair ? SelectedA.Run.GroundTruthReview?.FalseNegativeCount : null);
        public string CandidateMisses => FormatCount(HasComparablePair ? SelectedB.Run.GroundTruthReview?.FalseNegativeCount : null);
        public string BaselineExtras => FormatCount(HasComparablePair ? SelectedA.Run.GroundTruthReview?.FalsePositiveCount : null);
        public string CandidateExtras => FormatCount(HasComparablePair ? SelectedB.Run.GroundTruthReview?.FalsePositiveCount : null);
        public bool HasComparableTiming => HasComparablePair && SelectedA.Run.TimingComparisonKey == SelectedB.Run.TimingComparisonKey;
        public string BaselineTakt => HasComparableTiming ? SelectedA.TaktText : "—";
        public string CandidateTakt => HasComparableTiming ? SelectedB.TaktText : "—";

        internal void ConfigureDecisionRules(Action<TrainingRun, ModelDecisionRule> save, Action<TrainingRun> evaluate = null, Action<TrainingRun> undo = null)
        { saveDecisionRule = save; evaluateDecisionRule = evaluate; undoDecisionRule = undo; RefreshRulePresentation(); }

        internal bool OpenDecisionRuleForRunId(string runId)
        {
            if (!CanChooseModels || disposed) return false;
            ModelComparisonWorkspaceRow row = Models.FirstOrDefault(item => item.History.TrainingRunId == runId);
            if (row == null) return false;
            SelectedRuleModel = row; IsRuleTab = true; return true;
        }

        private (double min, double max) AnomalyScoreRange()
        {
            double[] values = decisionCache?.Samples.Where(sample => sample.Split == "val" && sample.IsGroundTruthConfirmed && !sample.IsRejected)
                .SelectMany(sample => sample.RawCandidates).Where(candidate => candidate?.AnomalyScore.HasValue == true && double.IsFinite(candidate.AnomalyScore.Value))
                .Select(candidate => candidate.AnomalyScore.Value).ToArray() ?? Array.Empty<double>();
            double threshold = ParseRuleNumber(ruleAnomalyThreshold);
            if (double.IsFinite(threshold)) values = values.Append(threshold).ToArray();
            if (values.Length == 0) return (0, 1);
            double min = values.Min(), max = values.Max();
            return min == max ? (min, max + Math.Max(1, Math.Abs(max) * .1)) : (min, max);
        }
        private double ThresholdGraphX(double value)
        {
            if (!double.IsFinite(value)) return 0;
            double min = IsAnomalyDecisionRule ? RuleAnomalySliderMinimum : .05, max = IsAnomalyDecisionRule ? RuleAnomalySliderMaximum : .95;
            return 50 + Math.Clamp((value - min) / (max - min), 0, 1) * 930;
        }
        private string ValidationCurveText(bool misses)
        {
            ModelDecisionEvaluation evaluation = ValidationDecision;
            string key = misses ? "RuleMissCurve" : "RuleExtraCurve";
            if (evaluation.Status == "ready" && (misses ? evaluation.NgCount : evaluation.OkCount) == 0) key += "Unavailable";
            return T(key);
        }

        private static string DecisionSampleText(ModelDecisionEvaluation evaluation, string key)
            => evaluation.Status == "ready" ? Format(key, evaluation.ImageCount, evaluation.NgCount, evaluation.OkCount)
                + (evaluation.ExcludedCount > 0 ? " · " + Format("RuleExcludedSamples", evaluation.ExcludedCount) : string.Empty) : string.Empty;

        private static string DecisionRateText(ModelDecisionEvaluation evaluation, bool misses)
            => evaluation.Status == "ready" ? FormatDecisionRate(misses ? evaluation.Misses : evaluation.Extras, misses ? evaluation.NgCount : evaluation.OkCount) : T("RuleRateUnavailable");

        private static string DecisionAccuracyText(ModelDecisionEvaluation evaluation)
            => evaluation.Accuracy.HasValue ? FormatDecisionRate(evaluation.ImageCount - evaluation.Misses - evaluation.Extras, evaluation.ImageCount) : T("RuleRateUnavailable");

        private static string FormatDecisionRate(int numerator, int denominator)
        {
            if (denominator == 0) return T("RuleRateUnavailable");
            double rate = numerator / (double)denominator;
            return rate.ToString(rate > 0 && rate < .1 ? "P1" : "P0", CultureInfo.CurrentCulture);
        }

        private PointCollection ThresholdGraphPoints(bool misses)
        {
            var points = new PointCollection();
            foreach (ModelDecisionThresholdPoint point in ModelComparisonReviewService.ScanDecisionThresholds(BuildRuleDraft(), decisionCache).Where(point => point.Evaluation.Status == "ready"))
            {
                int denominator = misses ? point.Evaluation.NgCount : point.Evaluation.OkCount;
                if (denominator == 0) continue;
                double rate = (misses ? point.Evaluation.Misses : point.Evaluation.Extras) / (double)denominator;
                points.Add(new System.Windows.Point(ThresholdGraphX(point.Threshold), 16 + (1 - rate) * 170));
            }
            return points;
        }
        private string GetSavedRuleStatus(ModelComparisonWorkspaceRow row, out ModelDecisionRule rule)
        {
            rule = row == null ? null : ruleRuns.FirstOrDefault(run => run?.TrainingRunId == row.History.TrainingRunId)?.DecisionRule;
            if (rule == null) return "RuleUnconfigured";
            string error = ModelRegistryService.ValidateDecisionRule(rule);
            if (error.Length > 0) return error;
            return ModelRegistryService.DecisionRuleMatches(rule, row.History.TrainingRunId, weightsHashes.GetValueOrDefault(row.History.WeightsPath), currentVersion, currentContentHash, ruleReviewHash) ? string.Empty : "RuleStale";
        }

        private string DescribeFixedTest(ModelComparisonWorkspaceRow row, bool includeRuleStatus = true)
        {
            string status = GetSavedRuleStatus(row, out ModelDecisionRule rule);
            if (status.Length > 0) return includeRuleStatus ? T(status) : string.Empty;
            ModelDecisionTestSummary summary = rule.FixedTestSummary;
            return summary == null ? T("RuleNeedsEvaluation") : Format("RuleFixedTestSummary", summary.Misses, summary.NgCount, summary.Extras, summary.OkCount);
        }

        internal void SetDecisionRuleContext(IReadOnlyList<TrainingRun> runs, IReadOnlyList<string> classNames, string reviewHash)
        {
            bool changed = ruleReviewHash != (reviewHash ?? string.Empty);
            ruleRuns = runs ?? Array.Empty<TrainingRun>(); ruleClassNames = classNames ?? Array.Empty<string>(); ruleReviewHash = reviewHash ?? string.Empty;
            string selectedId = selectedRuleModel?.History.TrainingRunId;
            selectedRuleModel = Models.FirstOrDefault(row => row.History.TrainingRunId == selectedId)
                ?? Models.FirstOrDefault(row => row.History.IsCurrentInspectionModel) ?? Models.FirstOrDefault();
            TrainingRun run = FindRuleRun();
            ModelDecisionRule draft = BuildRuleDraft();
            if (changed || draft == null || !ModelRegistryService.DecisionRuleMatches(draft, run?.TrainingRunId, CurrentRuleHash, currentVersion, currentContentHash, ruleReviewHash))
            { decisionCache = null; RestoreDecisionRule(); }
            else { savedRule = run?.DecisionRule?.Copy(); RefreshRulePresentation(); }
        }

        internal void SetDecisionEvaluationCache(ModelDecisionEvaluationCache cache)
        {
            if (disposed || isReadOnlyDisplayContext) return;
            TrainingRun run = FindRuleRun();
            if (cache == null || !ModelRegistryService.DecisionRuleMatches(BuildRuleDraft(), run?.TrainingRunId, cache.WeightsSha256, cache.DatasetVersionId, cache.DatasetContentSha256, cache.ReviewEvidenceSha256)
                || cache.TrainingRunId != run?.TrainingRunId) { decisionCache = null; ruleOperationError = "RuleStale"; }
            else { decisionCache = cache; ruleOperationError = string.Empty; }
            RefreshRulePresentation();
        }

        internal void CompleteDecisionRuleSave(bool success, string errorKey = "RuleSaveFailed")
        {
            if (success) { savedRule = FindRuleRun()?.DecisionRule?.Copy(); ruleDirty = false; ruleOperationError = string.Empty; }
            else ruleOperationError = errorKey;
            RefreshRulePresentation();
        }

        internal void CompleteDecisionRuleUndo() { decisionCache = null; RestoreDecisionRule(); }

        private string CurrentRuleHash => weightsHashes.GetValueOrDefault(selectedRuleModel?.History.WeightsPath ?? string.Empty) ?? string.Empty;
        private TrainingRun FindRuleRun()
        {
            TrainingRun[] matches = ruleRuns.Where(run => run != null && run.TrainingRunId == selectedRuleModel?.History.TrainingRunId).ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }
        private ModelDecisionRule CreateDefaultRule() => new()
        {
            TrainingRunId = FindRuleRun()?.TrainingRunId ?? string.Empty, WeightsSha256 = CurrentRuleHash, DatasetVersionId = currentVersion,
            DatasetContentSha256 = currentContentHash, ReviewEvidenceSha256 = ruleReviewHash, DatasetPurpose = purpose,
            Classes = ruleClassNames.Select((name, id) => new ModelDecisionClassRule { ClassId = id, ClassName = name }).ToList()
        };
        private ModelDecisionRule BuildRuleDraft()
        {
            ModelDecisionRule draft = ruleDraftIdentity?.Copy() ?? CreateDefaultRule();
            draft.Confidence = ParseRuleNumber(ruleConfidence); draft.MinimumShortSidePixels = ParseRuleNumber(ruleMinimumSize);
            draft.MinimumDefectCount = int.TryParse(ruleMinimumCount, NumberStyles.Integer, CultureInfo.CurrentCulture, out int count) ? count : 0;
            draft.AnomalyScoreThreshold = string.IsNullOrWhiteSpace(ruleAnomalyThreshold) ? null : ParseRuleNumber(ruleAnomalyThreshold);
            draft.Classes = DecisionClasses.Select(item => item.ToRule()).ToList();
            return draft;
        }
        private static double ParseRuleNumber(string text) => double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out double value) ? value : double.NaN;
        private string ValidateRuleDraft(ModelDecisionRule draft)
        {
            string error = ModelRegistryService.ValidateDecisionRule(draft);
            if (error.Length > 0) return error;
            if (!ModelRegistryService.DecisionRuleMatches(draft, FindRuleRun()?.TrainingRunId, CurrentRuleHash, currentVersion, currentContentHash, ruleReviewHash)) return "RuleStale";
            if (!IsAnomalyDecisionRule && (draft.Classes.Count != ruleClassNames.Count || draft.Classes.Any(item => item.ClassId >= ruleClassNames.Count || item.ClassName != ruleClassNames[item.ClassId]))) return "RuleClassesInvalid";
            return string.Empty;
        }
        private void RestoreDecisionRule()
        {
            savedRule = FindRuleRun()?.DecisionRule?.Copy(); ruleOperationError = string.Empty;
            SetRuleDraft(savedRule ?? CreateDefaultRule()); ruleDirty = false; RefreshRulePresentation();
        }
        private void SetRuleDraft(ModelDecisionRule draft)
        {
            ruleDraftIdentity = draft.Copy();
            ruleConfidence = draft.Confidence.ToString("R", CultureInfo.CurrentCulture); ruleMinimumSize = draft.MinimumShortSidePixels.ToString("R", CultureInfo.CurrentCulture);
            ruleMinimumCount = draft.MinimumDefectCount.ToString(CultureInfo.CurrentCulture); ruleAnomalyThreshold = draft.AnomalyScoreThreshold?.ToString("R", CultureInfo.CurrentCulture) ?? string.Empty;
            DecisionClasses.Clear(); foreach (ModelDecisionClassRule item in draft.Classes ?? new()) if (item != null) DecisionClasses.Add(new ModelDecisionClassEditor(item, RuleDraftChanged));
            RefreshRulePresentation();
        }
        private void RuleDraftChanged() { ruleDirty = true; ruleOperationError = string.Empty; RefreshRulePresentation(); }
        private void RefreshRulePresentation()
        {
            OnPropertyChanged(string.Empty);
            ((RelayCommand)SaveDecisionRuleCommand).RaiseCanExecuteChanged(); ((RelayCommand)EvaluateDecisionRuleCommand).RaiseCanExecuteChanged();
            ((RelayCommand)RestoreDecisionRuleCommand).RaiseCanExecuteChanged(); ((RelayCommand)DefaultDecisionRuleCommand).RaiseCanExecuteChanged(); ((RelayCommand)ZeroMissDecisionRuleCommand).RaiseCanExecuteChanged();
            ((RelayCommand)UndoDecisionRuleSaveCommand).RaiseCanExecuteChanged(); ((RelayCommand)ShowCandidateRuleCommand).RaiseCanExecuteChanged();
            foreach (ModelComparisonWorkspaceRow row in Models) row.DecisionTestSummaryText = DescribeFixedTest(row);
        }
        private void SaveDecisionRule()
        {
            if (!CanSaveDecisionRule) return;
            ModelDecisionRule draft = BuildRuleDraft(); draft.SavedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            saveDecisionRule(FindRuleRun(), draft);
        }
        private static string FormatDecisionEvaluation(ModelDecisionEvaluation evaluation) => evaluation.Status != "ready" ? T(evaluation.Status)
            : Format("RuleEvaluationSummary", evaluation.Misses, evaluation.NgCount, evaluation.Extras, evaluation.OkCount, evaluation.Accuracy, evaluation.ExcludedCount);
        private string DescribeSavedDecisionRule(ModelComparisonWorkspaceRow row)
        {
            string status = GetSavedRuleStatus(row, out ModelDecisionRule rule);
            if (status.Length > 0) return T(status);
            return rule.DatasetPurpose == LabelingDatasetPurpose.AnomalyDetection ? Format("RuleAnomalySummary", rule.AnomalyScoreThreshold) : Format("RuleDetectionSummary", rule.Confidence, rule.MinimumShortSidePixels, rule.MinimumDefectCount);
        }

        internal void Configure(Action run, Action<ModelComparisonWorkspaceRow, string> adopt, Action<ModelComparisonImagePair, bool> preview, Action<string> review, Action<string> undo = null)
        {
            runComparison = run ?? (() => { });
            adoptModel = adopt ?? ((_, _) => { });
            loadPreview = preview ?? ((_, _) => { });
            reviewLabels = review ?? (_ => { });
            undoAdoption = undo ?? (_ => { });
        }

        internal void SetContext(IReadOnlyList<WpfModelRegistryHistoryItem> models, IReadOnlyList<WpfModelBenchmarkRun> runs, IReadOnlyDictionary<string, string> hashes, string version, string contentHash, string engine, string root, double threshold, bool runAvailable, bool running, string recipeIdentity = "", LabelingDatasetPurpose datasetPurpose = LabelingDatasetPurpose.ObjectDetection, IReadOnlyDictionary<string, long> sizes = null, IReadOnlyDictionary<string, RecipeDatasetVersionPresentation> datasetVersionLabels = null)
        {
            versionLabels = datasetVersionLabels ?? new Dictionary<string, RecipeDatasetVersionPresentation>();
            bool leavingDisplayContext = isReadOnlyDisplayContext;
            isReadOnlyDisplayContext = false;
            displayModelSizesMegabytes = null;
            if (leavingDisplayContext) { CancelAdoption(); HideAdoptionUndoNotice(); selectedA = null; selectedB = null; }
            SetContextData(models, runs, hashes, version, contentHash, engine, root, threshold, runAvailable, running, recipeIdentity, datasetPurpose, sizes);
        }

        // Explicit display-only catalog entry: no weights verification or executable workflow is inferred.
        internal void SetReadOnlyDisplayContext(IReadOnlyList<WpfModelRegistryHistoryItem> models, IReadOnlyList<WpfModelBenchmarkRun> runs, string version, string contentHash, double threshold, string recipeIdentity = "", LabelingDatasetPurpose datasetPurpose = LabelingDatasetPurpose.ObjectDetection, IReadOnlyDictionary<string, double> modelSizesMegabytes = null)
        {
            versionLabels = new Dictionary<string, RecipeDatasetVersionPresentation>();
            isReadOnlyDisplayContext = true;
            displayModelSizesMegabytes = modelSizesMegabytes;
            CancelAdoption();
            HideAdoptionUndoNotice();
            SetContextData(models?.Where(item => item != null).ToArray(), runs?.Where(run => run != null).ToArray(), null, version, contentHash, string.Empty, string.Empty, threshold, false, false, recipeIdentity, datasetPurpose, null);
        }

        private void SetContextData(IReadOnlyList<WpfModelRegistryHistoryItem> models, IReadOnlyList<WpfModelBenchmarkRun> runs, IReadOnlyDictionary<string, string> hashes, string version, string contentHash, string engine, string root, double threshold, bool runAvailable, bool running, string recipeIdentity, LabelingDatasetPurpose datasetPurpose, IReadOnlyDictionary<string, long> sizes)
        {
            history = models ?? Array.Empty<WpfModelRegistryHistoryItem>();
            reports = runs ?? Array.Empty<WpfModelBenchmarkRun>();
            weightsHashes = hashes ?? new Dictionary<string, string>();
            weightsSizes = sizes ?? new Dictionary<string, long>();
            purpose = datasetPurpose;
            if (!MetricKeys.Contains(sortKey)) sortKey = MetricKeys[0];
            bool projectChanged = projectIdentity != (recipeIdentity ?? string.Empty) || runtimeRoot != (root ?? string.Empty);
            projectIdentity = recipeIdentity ?? string.Empty;
            if (projectChanged) { CancelAdoption(); HideAdoptionUndoNotice(); selectedA = null; selectedB = null; SetStatus(string.Empty); }
            runtimeEngine = engine ?? string.Empty;
            runtimeRoot = root ?? string.Empty;
            currentVersion = version ?? string.Empty;
            currentContentHash = contentHash ?? string.Empty;
            confidence = threshold;
            canRun = runAvailable;
            isBusy = running;
            refreshing = true;
            string[] versions = new[] { currentVersion }.Where(id => !string.IsNullOrWhiteSpace(id))
                .Concat(reports.Select(run => run.DatasetVersionId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().OrderBy(id => id)).Distinct().ToArray();
            for (int index = Versions.Count - 1; index >= 0; index--) if (!versions.Contains(Versions[index])) Versions.RemoveAt(index);
            for (int index = 0; index < versions.Length; index++)
            {
                int existingIndex = Versions.IndexOf(versions[index]);
                if (existingIndex < 0) Versions.Insert(index, versions[index]);
                else if (existingIndex != index) Versions.Move(existingIndex, index);
            }
            RefreshVersionChoices();
            if (projectChanged || !Versions.Contains(selectedVersion)) selectedVersion = currentVersion;
            refreshing = false;
            RefreshRows();
            TryRunPendingAutoEvaluation();
        }

        internal void SetCommandAvailability(bool available, bool running)
        {
            canRun = available;
            IsBusy = running;
            RefreshCommands();
            TryRunPendingAutoEvaluation();
        }

        // Prototype parity: a finished training run shows test metrics in the comparison without a manual
        // re-evaluation. The training queue names each run it just recorded; that model is evaluated through the
        // existing re-evaluate path (one pair per run) and is attempted once. A recipe without a saved dataset
        // version records runs without a version, so the run ID, not the version, selects the model.
        // The request waits while comparison commands are unavailable, which includes the time training still runs.
        internal void RequestAutoEvaluation(string trainingRunId)
        {
            string runId = trainingRunId?.Trim() ?? string.Empty;
            if (runId.Length > 0 && !autoEvaluationAttempted.Contains(runId)) pendingAutoEvaluationRuns.Add(runId);
            TryRunPendingAutoEvaluation();
        }

        internal bool IsAutoEvaluationPending => pendingAutoEvaluationRuns.Count > 0;

        private void TryRunPendingAutoEvaluation()
        {
            if (pendingAutoEvaluationRuns.Count == 0 || startingAutoEvaluation || disposed || isReadOnlyDisplayContext || !canRun || IsBusy || !CanChooseModels
                || SelectedVersion != currentVersion) return;
            // A run already evaluated on this version needs nothing; a run not listed yet waits for the next refresh.
            pendingAutoEvaluationRuns.RemoveWhere(runId => Models.Any(row => row.History.TrainingRunId == runId && row.IsComparable));
            ModelComparisonWorkspaceRow next = Models.FirstOrDefault(row => pendingAutoEvaluationRuns.Contains(row.History.TrainingRunId)
                && Models.Any(other => other != row && CanEvaluatePair(other, row)));
            if (next == null) return;

            pendingAutoEvaluationRuns.Remove(next.History.TrainingRunId);
            autoEvaluationAttempted.Add(next.History.TrainingRunId);
            startingAutoEvaluation = true;
            try { Reevaluate(next); }
            finally { startingAutoEvaluation = false; }
        }

        private bool startingAutoEvaluation;
        private readonly HashSet<string> pendingAutoEvaluationRuns = new(StringComparer.Ordinal);
        private readonly HashSet<string> autoEvaluationAttempted = new(StringComparer.Ordinal);

        internal void SetPreview(ModelComparisonPreviewPresentation preview)
        {
            baselinePreview = preview?.Baseline;
            candidatePreview = preview?.Candidate;
            classColors = preview?.ClassColors ?? Array.Empty<Color>();
            previewError = preview?.Error ?? string.Empty;
            RefreshClassMetrics();
            OnPropertyChanged(nameof(BaselinePreview));
            OnPropertyChanged(nameof(CandidatePreview));
            OnPropertyChanged(nameof(ClassColors));
            OnPropertyChanged(nameof(PreviewError));
        }

        internal void SetStatus(string text) { statusKey = string.Empty; statusArguments = Array.Empty<object>(); status = text ?? string.Empty; OnPropertyChanged(nameof(OperationStatus)); }
        internal void SetLocalizedStatus(string key, params object[] values) { statusKey = key; statusArguments = values ?? Array.Empty<object>(); status = Format(key, statusArguments); OnPropertyChanged(nameof(OperationStatus)); }
        internal void CompleteAdoption(bool committed, string message)
        {
            if (committed) CancelAdoption();
            SetStatus(message);
        }
        internal void RequestRefresh() { if (!disposed) RefreshRequested?.Invoke(); }

        internal void ShowAdoptionUndoNotice(string adoptionId, string modelName)
        {
            if (disposed || isReadOnlyDisplayContext) return;
            undoAdoptionId = adoptionId ?? string.Empty;
            undoNoticeModelName = modelName ?? string.Empty;
            isAdoptionUndoNoticeVisible = true;
            OnPropertyChanged(nameof(IsAdoptionUndoNoticeVisible));
            OnPropertyChanged(nameof(HasUndoAdoptionTarget));
            OnPropertyChanged(nameof(AdoptionUndoNoticeText));
            RefreshCommands();
        }

        internal void HideAdoptionUndoNotice()
        {
            undoAdoptionId = string.Empty;
            isAdoptionUndoNoticeVisible = false;
            OnPropertyChanged(nameof(IsAdoptionUndoNoticeVisible));
            OnPropertyChanged(nameof(HasUndoAdoptionTarget));
            RefreshCommands();
        }

        internal void ValidateAdoptionUndoNotice(string currentAdoptionId)
        {
            if (HasUndoAdoptionTarget && undoAdoptionId != currentAdoptionId) HideAdoptionUndoNotice();
        }

        private void RefreshRows()
        {
            string aPath = isReadOnlyDisplayContext ? SelectedA?.History.TrainingRunId : SelectedA?.History.WeightsPath;
            string bPath = isReadOnlyDisplayContext ? SelectedB?.History.TrainingRunId : SelectedB?.History.WeightsPath;
            string ruleRunId = selectedRuleModel?.History.TrainingRunId;
            string rulePath = selectedRuleModel?.History.WeightsPath;
            bool previousRefreshing = refreshing;
            refreshing = true;
            try
            {
                Models.Clear();
                string content = SelectedVersion == currentVersion ? currentContentHash : reports.FirstOrDefault(run => run.DatasetVersionId == SelectedVersion)?.DatasetContentSha256;
                var rows = history.Select(item =>
                {
                    WpfModelBenchmarkRun report = reports.Where(run => MatchesTask(run) && (isReadOnlyDisplayContext ? MatchesDisplayRun(item, run) : MatchesWeights(item, run)))
                        .OrderByDescending(run => run.DatasetVersionId == SelectedVersion).ThenByDescending(run => run.CreatedAt).FirstOrDefault();
                    return new ModelComparisonWorkspaceRow(item, report, SelectedVersion, content, confidence, MetricKeys,
                        !isReadOnlyDisplayContext && weightsSizes.TryGetValue(item.WeightsPath, out long bytes) ? bytes : null,
                        isReadOnlyDisplayContext && displayModelSizesMegabytes != null && !string.IsNullOrWhiteSpace(item.TrainingRunId) && displayModelSizesMegabytes.TryGetValue(item.TrainingRunId, out double megabytes) ? megabytes : null,
                        versionLabels.GetValueOrDefault(report?.DatasetVersionId ?? string.Empty));
                }).ToArray();
                WpfModelBenchmarkRun reference = rows.FirstOrDefault(row => row.History.IsCurrentInspectionModel && row.IsComparable)?.Run ?? rows.FirstOrDefault(row => row.IsComparable)?.Run;
                foreach (ModelComparisonWorkspaceRow row in rows) if (row.IsComparable && reference != null && row.Run.EvaluationConditionKey != reference.EvaluationConditionKey) row.IsComparable = false;
                foreach (string key in MetricKeys.Where(key => key != "threshold"))
                {
                    ModelComparisonWorkspaceRow[] eligible = rows.Where(row => row.IsComparable && double.IsFinite(row.SortValue(key))
                        && (key != "takt" || row.Run.TimingComparisonKey == reference?.TimingComparisonKey)).ToArray();
                    if (eligible.Length > 0)
                    {
                        double best = eligible.Min(row => row.SortValue(key));
                        foreach (ModelComparisonWorkspaceRow row in eligible.Where(row => Math.Abs(row.SortValue(key) - best) < 0.000001)) row.BestMetrics.Add(key);
                    }
                }
                int rank = 0;
                foreach (ModelComparisonWorkspaceRow row in rows.OrderByDescending(row => row.IsComparable).ThenBy(row => row.SortValue(sortKey)))
                {
                    if (row.IsComparable && row.PrimaryMetric.HasValue) row.Rank = ++rank;
                    Models.Add(row);
                }
                selectedA = Models.FirstOrDefault(row => (isReadOnlyDisplayContext ? row.History.TrainingRunId : row.History.WeightsPath) == aPath && row.IsComparable)
                    ?? Models.FirstOrDefault(row => row.History.IsCurrentInspectionModel && row.IsComparable)
                    ?? Models.FirstOrDefault(row => row.IsComparable)
                    ?? Models.FirstOrDefault(row => row.History.IsCurrentInspectionModel) ?? Models.FirstOrDefault();
                selectedB = Models.FirstOrDefault(row => (isReadOnlyDisplayContext ? row.History.TrainingRunId : row.History.WeightsPath) == bPath && row.IsComparable && row != selectedA)
                    ?? Models.FirstOrDefault(row => row.IsComparable && row != selectedA)
                    ?? Models.FirstOrDefault(row => (isReadOnlyDisplayContext ? row.History.TrainingRunId : row.History.WeightsPath) == bPath && row != selectedA) ?? Models.FirstOrDefault(row => row != selectedA);
                selectedRuleModel = Models.FirstOrDefault(row => !string.IsNullOrEmpty(ruleRunId)
                    ? row.History.TrainingRunId == ruleRunId : row.History.WeightsPath == rulePath);
            }
            finally { refreshing = previousRefreshing; }
            OnPropertyChanged(nameof(SelectedRuleModel));
            RefreshPair();
            OnPropertyChanged(nameof(HasModels));
            OnPropertyChanged(nameof(MetricHeaders)); OnPropertyChanged(nameof(PrimaryMetricCaption)); OnPropertyChanged(nameof(MetricTableMinimumWidth)); OnPropertyChanged(nameof(IsLocalizationTask));
            OnPropertyChanged(nameof(SelectedVersion));
            OnPropertyChanged(nameof(SelectedVersionChoice));
            OnPropertyChanged(nameof(SelectedVersionLabel));
            OnPropertyChanged(nameof(SelectionIdentityText));
            OnPropertyChanged(nameof(IsBusy));
        }

        private bool MatchesWeights(WpfModelRegistryHistoryItem item, WpfModelBenchmarkRun run)
        {
            string hash = weightsHashes.GetValueOrDefault(item.WeightsPath ?? string.Empty);
            return !string.IsNullOrWhiteSpace(hash) && string.Equals(hash, run.WeightsSha256, StringComparison.OrdinalIgnoreCase)
                && (SamePath(item.WeightsPath, run.WeightsPath) || SamePath(item.ArtifactPath, run.WeightsPath));
        }

        private bool MatchesDisplayRun(WpfModelRegistryHistoryItem item, WpfModelBenchmarkRun run)
            => !string.IsNullOrWhiteSpace(item.TrainingRunId) && item.TrainingRunId == run.Id && run.SourceSha256.Length == 64
                && history.Count(model => model.TrainingRunId == item.TrainingRunId) == 1 && reports.Count(report => report.Id == run.Id) == 1;

        private void RefreshPair()
        {
            ModelComparisonWorkspaceRow recommended = FindRecommendedRow(out recommendationReason);
            foreach (ModelComparisonWorkspaceRow row in Models) { row.IsA = row == SelectedA; row.IsB = row == SelectedB; row.IsRecommended = row == recommended; row.RefreshText(); }
            RecommendedRow = recommended;
            OnPropertyChanged(nameof(RecommendedRow)); OnPropertyChanged(nameof(HasRecommendation)); OnPropertyChanged(nameof(RecommendationText)); OnPropertyChanged(nameof(RecommendationReason));
            OnPropertyChanged(nameof(SpeedAccuracyRows)); OnPropertyChanged(nameof(HasSpeedAccuracyPoints));
            var previousImages = Images.ToDictionary(image => image.ImagePath, StringComparer.OrdinalIgnoreCase);
            Images.Clear();
            if (HasComparablePair)
            {
                IReadOnlyList<ModelComparisonImageReview> left = SelectedA.Run.GroundTruthReview?.ImageReviews ?? Array.Empty<ModelComparisonImageReview>();
                IReadOnlyList<ModelComparisonImageReview> right = SelectedB.Run.GroundTruthReview?.ImageReviews ?? Array.Empty<ModelComparisonImageReview>();
                var rightByPath = right.GroupBy(item => item.ImagePath, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
                if (HasCompleteReview(SelectedA.Run, left) && HasCompleteReview(SelectedB.Run, right) && left.Select(item => item.ImagePath).Distinct(StringComparer.OrdinalIgnoreCase).Count() == left.Count && rightByPath.Count == right.Count)
                {
                    foreach (ModelComparisonImageReview image in left)
                        if (rightByPath.TryGetValue(image.ImagePath, out ModelComparisonImageReview other) && image.ImageSha256.Length == 64 && image.ImageSha256 == other.ImageSha256 && SameGroundTruth(image.GroundTruth, other.GroundTruth))
                        {
                            var pair = new ModelComparisonImagePair(image, other);
                            if (previousImages.TryGetValue(image.ImagePath, out ModelComparisonImagePair previous) && previous.Baseline.ImageSha256 == image.ImageSha256) pair.ThumbnailSource = previous.ThumbnailSource;
                            Images.Add(pair);
                        }
                    if (Images.Count != left.Count) Images.Clear();
                }

            }
            RefreshClassMetrics();
            RefreshAnalysis();
            ImageView.Refresh();
            SelectFirstShownImage();
            foreach (string property in new[] { nameof(SelectionIdentityText), nameof(SelectedA), nameof(SelectedB), nameof(BaselineTitle), nameof(CandidateTitle), nameof(EvidenceText), nameof(HasComparablePair), nameof(HasImageReview), nameof(HasClassMetrics), nameof(ClassMetricCaption), nameof(BaselineMisses), nameof(CandidateMisses), nameof(BaselineExtras), nameof(CandidateExtras), nameof(BaselineTakt), nameof(CandidateTakt) }) OnPropertyChanged(property);
            RefreshCommands();
        }

        private void RefreshClassMetrics()
        {
            ClassMetrics.Clear();
            if (HasComparablePair)
            {
                foreach (int id in SelectedA.Run.ClassMetrics.Select(item => item.ClassId).Union(SelectedB.Run.ClassMetrics.Select(item => item.ClassId)))
                {
                    Color? color = id >= 0 && id < classColors.Count ? classColors[id] : null;
                    var row = new ModelComparisonClassMetricRow(SelectedA.Run.ClassMetrics.FirstOrDefault(item => item.ClassId == id), SelectedB.Run.ClassMetrics.FirstOrDefault(item => item.ClassId == id), purpose == LabelingDatasetPurpose.Segmentation, color);
                    if (row.HasValues) ClassMetrics.Add(row);
                }
            }
            OnPropertyChanged(nameof(HasClassMetrics));
        }

        // Differences decide the choice at a glance; agreement groups classify each test image by which model erred.
        // A new pair clears the agreement filter.
        private void RefreshAnalysis()
        {
            Differences.Clear(); AgreementGroups.Clear();
            agreementFilter = string.Empty;
            if (HasComparablePair)
            {
                Differences.Add(ModelComparisonDifferenceRow.Create(PrimaryMetricCaption, SelectedA.PrimaryMetric, SelectedB.PrimaryMetric, higherIsBetter: true, "0.000"));
                if (IsLocalizationTask)
                {
                    Differences.Add(ModelComparisonDifferenceRow.Create(T("Misses"), SelectedA.Run.GroundTruthReview?.FalseNegativeCount, SelectedB.Run.GroundTruthReview?.FalseNegativeCount, higherIsBetter: false, "0"));
                    Differences.Add(ModelComparisonDifferenceRow.Create(T("Extras"), SelectedA.Run.GroundTruthReview?.FalsePositiveCount, SelectedB.Run.GroundTruthReview?.FalsePositiveCount, higherIsBetter: false, "0"));
                }
                if (HasComparableTiming) Differences.Add(ModelComparisonDifferenceRow.Create(T("Takt"), SelectedA.TaktValue, SelectedB.TaktValue, higherIsBetter: false, "0.0"));
                if (Images.Count > 0)
                    foreach ((string key, byte r, byte g, byte b) in new[] { ("ok", (byte)0x3C, (byte)0xCF, (byte)0x8E), ("a", (byte)0x60, (byte)0xA5, (byte)0xFA), ("b", (byte)0xF5, (byte)0xA5, (byte)0x24), ("ab", (byte)0xF0, (byte)0x6A, (byte)0x6A) })
                        AgreementGroups.Add(new ModelComparisonAgreementGroup(key, Images.Count(image => image.Agreement == key), Images.Count, Color.FromRgb(r, g, b)));
            }
            foreach (string property in new[] { nameof(HasAnalysis), nameof(AnalysisNote), nameof(SweepA), nameof(SweepB), nameof(SweepConfidence), nameof(SweepCurrentText), nameof(SweepBestText), nameof(AgreementFilter), nameof(HasAgreementFilter) }) OnPropertyChanged(property);
        }

        private void SelectFirstShownImage() { SelectedImage = ImageView.Cast<ModelComparisonImagePair>().FirstOrDefault(); RequestPreview(); }
        private static bool HasCompleteReview(WpfModelBenchmarkRun run, IReadOnlyList<ModelComparisonImageReview> images)
            => images.Count == run.EvidenceCount && run.GroundTruthReview.ImageCount == run.EvidenceCount
                && images.Sum(image => image.TruePositives.Count) == run.GroundTruthReview.TruePositiveCount
                && images.Sum(image => image.FalsePositives.Count) == run.GroundTruthReview.FalsePositiveCount
                && images.Sum(image => image.FalseNegatives.Count) == run.GroundTruthReview.FalseNegativeCount;
        private static bool SameGroundTruth(IReadOnlyList<WpfModelBenchmarkNormalizedBox> left, IReadOnlyList<WpfModelBenchmarkNormalizedBox> right)
            => left.Count == right.Count && left.Zip(right).All(pair => pair.First.ClassId == pair.Second.ClassId && pair.First.XMin == pair.Second.XMin && pair.First.YMin == pair.Second.YMin && pair.First.XMax == pair.Second.XMax && pair.First.YMax == pair.Second.YMax);
        private void RequestPreview()
        {
            SetPreview(null);
            OnPropertyChanged(nameof(BaselineImageErrors));
            OnPropertyChanged(nameof(CandidateImageErrors));
            if (!disposed && SelectedImage != null) loadPreview(SelectedImage, ShowGroundTruth);
            OnPropertyChanged(nameof(ImageTitle));
            OnPropertyChanged(nameof(ImagePositionText));
            reviewLabelsCommand.RaiseCanExecuteChanged();
        }

        private void Reevaluate(ModelComparisonWorkspaceRow row)
        {
            if (isReadOnlyDisplayContext) return;
            selectedB = row;
            // The baseline is a model this one can be evaluated with, including a YOLOv8/YOLO11 pair on the Ultralytics
            // runtime. Weights trained in this recipe share its classes; configured weights without a training run (for
            // example a COCO-pretrained yolo11n.pt as the inspection model) may not, and the evaluation then stops on the
            // class mismatch. So the inspection model leads only when it was trained here.
            ModelComparisonWorkspaceRow[] partners = Models.Where(item => item != row && CanEvaluatePair(item, row)).ToArray();
            static bool TrainedHere(ModelComparisonWorkspaceRow item) => !string.IsNullOrWhiteSpace(item.History.TrainingRunId);
            selectedA = partners.FirstOrDefault(item => item.History.IsCurrentInspectionModel && TrainedHere(item))
                ?? partners.FirstOrDefault(TrainedHere)
                ?? partners.FirstOrDefault(item => item.History.IsCurrentInspectionModel)
                ?? partners.FirstOrDefault();
            RefreshPair();
            if (CanRunComparison) runComparison();
        }

        private void BeginAdoption()
        {
            if (!CanAdoptModel) return;
            adoptionRow = SelectedB;
            AdoptionReason = string.Empty;
            isAdoptionConfirmationOpen = true;
            OnPropertyChanged(nameof(AdoptionModelText));
            OnPropertyChanged(nameof(AdoptionDecisionRuleText));
            OnPropertyChanged(nameof(AdoptionPartTestText));
            OnPropertyChanged(nameof(AdoptionEngineChangeText));
            OnPropertyChanged(nameof(IsAdoptionConfirmationOpen));
            RefreshCommands();
        }

        private void ConfirmAdoption()
        {
            if (!confirmAdoptionCommand.CanExecute(null)) return;
            isAdopting = true;
            IsBusy = true;
            try { adoptModel(adoptionRow, AdoptionReason.Trim()); }
            finally { isAdopting = false; IsBusy = false; RefreshCommands(); }
        }

        private void CancelAdoption()
        {
            isAdoptionConfirmationOpen = false;
            adoptionRow = null;
            OnPropertyChanged(nameof(IsAdoptionConfirmationOpen));
            RefreshCommands();
        }

        private void UndoAdoption()
        {
            if (!CanUndoAdoption) return;
            isUndoing = true;
            IsBusy = true;
            try { undoAdoption(undoAdoptionId); }
            finally { isUndoing = false; IsBusy = false; RefreshCommands(); }
        }

        private bool UsesCurrentRuntime(ModelComparisonWorkspaceRow row)
            => row != null && PythonModelSettings.NormalizeModelEngine(row.History.ModelEngine) == PythonModelSettings.NormalizeModelEngine(runtimeEngine) && SamePath(row.History.ProjectRootPath, runtimeRoot);

        // A YOLOv8/YOLO11 detection model can become the inspection model on the other Ultralytics engine; adoption
        // switches the recipe engine in the same transaction (ModelCandidateLifecycleRequest.CandidateModelEngine).
        private bool CanAdoptOnCurrentRuntime(ModelComparisonWorkspaceRow row)
            => UsesCurrentRuntime(row) || (row != null && purpose == LabelingDatasetPurpose.ObjectDetection
                && ModelComparisonRunService.IsUltralyticsDetectionEngine(row.History.ModelEngine)
                && ModelComparisonRunService.IsUltralyticsDetectionEngine(runtimeEngine));

        // Same runtime, or two Ultralytics detection models (YOLOv8 / YOLO11) with weights files, which the
        // engine-comparison path of compare-yolo-models.ps1 evaluates side by side.
        private bool CanEvaluatePair(ModelComparisonWorkspaceRow a, ModelComparisonWorkspaceRow b)
            => a != null && b != null && ((UsesCurrentRuntime(a) && UsesCurrentRuntime(b))
                || (purpose == LabelingDatasetPurpose.ObjectDetection
                    && ModelComparisonRunService.IsUltralyticsDetectionEngine(a.History.ModelEngine)
                    && ModelComparisonRunService.IsUltralyticsDetectionEngine(b.History.ModelEngine)
                    && !string.IsNullOrWhiteSpace(a.History.WeightsPath) && !string.IsNullOrWhiteSpace(b.History.WeightsPath)));

        private bool MatchesTask(WpfModelBenchmarkRun run) => run.TaskKey == (purpose switch
        {
            LabelingDatasetPurpose.Segmentation => "segmentation",
            LabelingDatasetPurpose.AnomalyDetection => "anomaly-classification",
            _ => "object-detection"
        });

        private string MetricCaption(string key) => FormatMetricCaption(key, purpose);
        internal static string FormatMetricCaption(string key, LabelingDatasetPurpose purpose) => T(key switch
        {
            "map50" => purpose == LabelingDatasetPurpose.Segmentation ? "MaskMap50" : "mAP50",
            "map5095" => "mAP50-95", "precision" => "Precision", "recall" => "Recall", "f1" => "F1", "takt" => "Takt",
            "size" => "SizeMB", "trainingMinutes" => "TrainingMinutes", "miou" => "MeanIoU", "dice" => "Dice",
            "auroc" => "AUROC", "accuracy" => "Accuracy", "fnr" => "FalseNegativeRate", "fpr" => "FalsePositiveRate", "threshold" => "Threshold", _ => key
        });

        private void RefreshCommands()
        {
            runCommand.RaiseCanExecuteChanged(); adoptCommand.RaiseCanExecuteChanged(); reviewLabelsCommand.RaiseCanExecuteChanged();
            confirmAdoptionCommand.RaiseCanExecuteChanged();
            undoAdoptionCommand.RaiseCanExecuteChanged();
            ((RelayCommand)CancelAdoptionCommand).RaiseCanExecuteChanged();
            ((RelayCommand<ModelComparisonWorkspaceRow>)SelectACommand).RaiseCanExecuteChanged();
            ((RelayCommand<ModelComparisonWorkspaceRow>)SelectBCommand).RaiseCanExecuteChanged();
            ((RelayCommand<ModelComparisonWorkspaceRow>)ReevaluateCommand).RaiseCanExecuteChanged();
            ((RelayCommand<string>)SortCommand).RaiseCanExecuteChanged();
            OnPropertyChanged(nameof(CanRunComparison)); OnPropertyChanged(nameof(CanAdoptModel)); OnPropertyChanged(nameof(CanChooseModels)); OnPropertyChanged(nameof(CanUndoAdoption)); OnPropertyChanged(nameof(RunHint));
            OnPropertyChanged(nameof(UsesEngineComparison)); OnPropertyChanged(nameof(RunBlockedReason)); OnPropertyChanged(nameof(HasRunBlockedReason));
            if (SaveDecisionRuleCommand != null) RefreshRulePresentation();
        }

        internal static bool SamePath(string left, string right) => !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) && string.Equals(left.Trim().TrimEnd('\\', '/'), right.Trim().TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
        private static string FormatCount(int? value) => value?.ToString(CultureInfo.CurrentCulture) ?? "—";
        internal static string T(string key) => OpenVisionLanguageService.T("WpfComparisonWorkspace." + key);
        private static string Format(string key, params object[] values) => string.Format(CultureInfo.CurrentCulture, T(key), values);
        private void RefreshVersionChoices()
        {
            bool previousRefreshing = refreshing;
            refreshing = true;
            try
            {
                VersionChoices.Clear();
                foreach (string id in Versions) VersionChoices.Add(new KeyValuePair<string, string>(id, versionLabels.GetValueOrDefault(id)?.DisplayText ?? RecipeDatasetVersionPresentationService.MissingLabel));
            }
            finally { refreshing = previousRefreshing; }
            OnPropertyChanged(nameof(SelectedVersion));
            OnPropertyChanged(nameof(SelectedVersionChoice));
        }

        private void OnLanguageChanged(object sender, EventArgs e) { RefreshVersionChoices(); foreach (ModelComparisonWorkspaceRow row in Models) { row.DecisionTestSummaryText = DescribeFixedTest(row); row.RefreshText(); } foreach (ModelComparisonImagePair image in Images) image.RefreshText(); foreach (ModelComparisonAgreementGroup group in AgreementGroups) group.RefreshText(); if (!string.IsNullOrEmpty(statusKey)) status = Format(statusKey, statusArguments); RequestPreview(); OnPropertyChanged(string.Empty); }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            WpfLocalizationViewModel.Instance.LanguageChanged -= OnLanguageChanged;
            RefreshRequested = null;
            HideAdoptionUndoNotice();
            runComparison = () => { }; adoptModel = (_, _) => { }; loadPreview = (_, _) => { }; reviewLabels = _ => { }; undoAdoption = _ => { };
            SetPreview(null);
            Images.Clear(); ClassMetrics.Clear(); Models.Clear(); DecisionClasses.Clear(); decisionCache = null; saveDecisionRule = null; evaluateDecisionRule = null; undoDecisionRule = null;
        }
    }

    public sealed record ModelDecisionErrorImage(string ImagePath, string Caption)
    {
        public ImageSource ThumbnailSource => WpfImageQueueItem.CreateThumbnailSource(ImagePath, 96);
    }

    public sealed class ModelDecisionClassEditor : WpfObservableViewModel
    {
        private bool enabled;
        private string confidence;
        private readonly Action changed;
        public ModelDecisionClassEditor(ModelDecisionClassRule rule, Action changed)
        { ClassId = rule.ClassId; ClassName = rule.ClassName; enabled = rule.Enabled; confidence = rule.Confidence?.ToString("R", CultureInfo.CurrentCulture) ?? string.Empty; this.changed = changed; }
        public int ClassId { get; }
        public string ClassName { get; }
        public bool Enabled { get => enabled; set { if (SetProperty(ref enabled, value)) changed(); } }
        public string Confidence { get => confidence; set { if (SetProperty(ref confidence, value ?? string.Empty)) changed(); } }
        internal ModelDecisionClassRule ToRule() => new() { ClassId = ClassId, ClassName = ClassName, Enabled = Enabled,
            Confidence = string.IsNullOrWhiteSpace(Confidence) ? null : double.TryParse(Confidence, NumberStyles.Float, CultureInfo.CurrentCulture, out double value) ? value : double.NaN };
    }

    public sealed class ModelComparisonWorkspaceRow : WpfObservableViewModel
    {
        private string decisionTestSummaryText = string.Empty;
        public string DecisionTestSummaryText { get => decisionTestSummaryText; internal set => SetProperty(ref decisionTestSummaryText, value); }
        private readonly IReadOnlyList<string> metricKeys;
        private readonly long? weightsSize;
        private readonly double? displaySizeMegabytes;
        private readonly string referenceVersion;
        private readonly RecipeDatasetVersionPresentation versionLabel;

        public ModelComparisonWorkspaceRow(WpfModelRegistryHistoryItem history, WpfModelBenchmarkRun run, string version, string contentHash, double confidence, IReadOnlyList<string> columns = null, long? weightsSizeBytes = null, double? displaySizeMegabytes = null, RecipeDatasetVersionPresentation datasetVersionLabel = null)
        {
            versionLabel = datasetVersionLabel;
            History = history;
            Run = run;
            metricKeys = columns ?? new[] { "map50", "map5095", "precision", "recall", "f1", "takt", "size", "trainingMinutes" };
            weightsSize = weightsSizeBytes;
            this.displaySizeMegabytes = displaySizeMegabytes;
            referenceVersion = version ?? string.Empty;
            IsComparable = run != null && !string.IsNullOrWhiteSpace(version) && run.DatasetVersionId == version && run.DatasetContentSha256.Length == 64 && run.DatasetContentSha256 == contentHash
                && run.Split.Equals("test", StringComparison.OrdinalIgnoreCase) && run.EvidenceCount > 0 && run.EvidenceFingerprintSha256.Length == 64 && run.HasEvaluationConditions
                && (run.TaskKey == "anomaly-classification" || run.Confidence.HasValue && Math.Abs(run.Confidence.Value - confidence) < 0.000001);
        }
        public WpfModelRegistryHistoryItem History { get; }
        public WpfModelBenchmarkRun Run { get; }
        public bool IsComparable { get; internal set; }
        public bool IsA { get; internal set; }
        public bool IsB { get; internal set; }
        public bool IsRecommended { get; internal set; }
        public bool IsSelected => IsA || IsB;
        public int Rank { get; internal set; }
        internal HashSet<string> BestMetrics { get; } = new();
        public bool IsBestMap50 => BestMetrics.Contains("map50");
        public bool IsBestMap5095 => BestMetrics.Contains("map5095");
        public bool IsBestPrecision => BestMetrics.Contains("precision");
        public bool IsBestRecall => BestMetrics.Contains("recall");
        public bool IsBestF1 => BestMetrics.Contains("f1");
        public bool IsBestTakt => BestMetrics.Contains("takt");
        public string RankText => Rank == 0 ? "—" : Rank.ToString(CultureInfo.CurrentCulture);
        public string ModelName => History.InferenceTitleText;
        public string ModelRole => History.IsConfiguredModelFallback
            ? OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.History.Configured.Model")
            : History.IsCurrentInspectionModel ? ModelComparisonWorkspaceViewModel.T("CurrentModel") : ModelComparisonWorkspaceViewModel.T("TrainedModel");
        public string VersionText => string.IsNullOrEmpty(Run?.DatasetVersionId) ? ModelComparisonWorkspaceViewModel.T("VersionMissing") : versionLabel?.DisplayText ?? RecipeDatasetVersionPresentationService.MissingLabel;
        public string VersionDetail => VersionText + Environment.NewLine + (Run?.DatasetVersionId ?? string.Empty);
        public string ComparisonState => IsComparable ? ModelComparisonWorkspaceViewModel.T("SameVersion") : ModelComparisonWorkspaceViewModel.T("Reevaluate");
        public string VersionStateText => IsComparable ? string.Empty : Run != null && !string.IsNullOrWhiteSpace(Run.DatasetVersionId) && Run.DatasetVersionId != referenceVersion
            ? ModelComparisonWorkspaceViewModel.T("DifferentVersion") : ModelComparisonWorkspaceViewModel.T("Reevaluate");
        public double? PrimaryMetric => Metric(metricKeys[0]);
        public string PrimaryMetricText => MetricText(metricKeys[0]);
        public IReadOnlyList<ModelComparisonMetricCell> MetricCells => metricKeys.Select(key => new ModelComparisonMetricCell(key, MetricText(key), BestMetrics.Contains(key))).ToArray();
        public string Map50Text => Percent(Metric("map50"));
        public string Map5095Text => Percent(Metric("map5095"));
        public string PrecisionText => Percent(Metric("precision"));
        public string RecallText => Percent(Metric("recall"));
        public string F1Text => Percent(F1);
        public double? F1 => Metric("precision") is double p && Metric("recall") is double r ? p + r > 0 ? 2 * p * r / (p + r) : 0 : null;
        internal double SortValue(string key) => MetricValue(key) is double value ? key is "takt" or "size" or "trainingMinutes" or "fnr" or "fpr" ? value : -value : double.PositiveInfinity;
        public string TaktText => Run?.TaktMs is double value && double.IsFinite(value) && value >= 0 ? value.ToString("0.00", CultureInfo.CurrentCulture) : "—";
        public double? TaktValue => MetricValue("takt");
        private double? MetricValue(string key)
        {
            double? value = key switch
            {
                "f1" => F1, "takt" => Run?.TaktMs, "size" => displaySizeMegabytes ?? (weightsSize.HasValue ? weightsSize / 1_000_000D : null),
                "trainingMinutes" => Run?.Metrics.FirstOrDefault(item => item.Key == key)?.Value, _ => Metric(key)
            };
            return value.HasValue && double.IsFinite(value.Value) && value >= 0 ? value : null;
        }
        private string MetricText(string key)
        {
            double? value = MetricValue(key);
            if (!value.HasValue) return "—";
            return key is "fnr" or "fpr" ? (value.Value * 100).ToString("0.0", CultureInfo.CurrentCulture)
                : value.Value.ToString(key == "takt" ? "0.00" : key is "size" or "trainingMinutes" ? "0.0" : "0.000", CultureInfo.CurrentCulture);
        }
        private double? Metric(string key) => Run?.Metrics.FirstOrDefault(item => item.Key == key)?.Value is double value && double.IsFinite(value) && value >= 0 && value <= 1 ? value : null;
        private static string Percent(double? value) => value?.ToString("0.000", CultureInfo.CurrentCulture) ?? "—";
        internal void RefreshText() => OnPropertyChanged(string.Empty);
    }

    public sealed class ModelComparisonMetricCell
    {
        private readonly LabelingDatasetPurpose purpose;
        public ModelComparisonMetricCell(string key, string text, bool isBest, LabelingDatasetPurpose task = LabelingDatasetPurpose.ObjectDetection) { Key = key; Text = text; IsBest = isBest; purpose = task; }
        public string Key { get; }
        public string Text { get; }
        public bool IsBest { get; }
        public string Caption => ModelComparisonWorkspaceViewModel.FormatMetricCaption(Key, purpose);
    }

    public sealed class ModelComparisonImagePair : WpfObservableViewModel
    {
        private ImageSource thumbnailSource;
        public ModelComparisonImagePair(ModelComparisonImageReview baseline, ModelComparisonImageReview candidate) { Baseline = baseline; Candidate = candidate; }
        public ModelComparisonImageReview Baseline { get; }
        public ModelComparisonImageReview Candidate { get; }
        public string ImagePath => Baseline.ImagePath;
        public string ImageName => Baseline.ImageName;
        public int ErrorCount => Baseline.ErrorCount + Candidate.ErrorCount;
        public bool HasErrors => ErrorCount > 0;
        // ok: neither model erred · a: only A · b: only B · ab: both.
        public string Agreement => Baseline.ErrorCount > 0 && Candidate.ErrorCount > 0 ? "ab" : Baseline.ErrorCount > 0 ? "a" : Candidate.ErrorCount > 0 ? "b" : "ok";
        public ImageSource ThumbnailSource { get => thumbnailSource; internal set => SetProperty(ref thumbnailSource, value); }
        public string ErrorText => string.Format(ModelComparisonWorkspaceViewModel.T("ImageErrors"), ErrorCount);
        internal void RefreshText() => OnPropertyChanged(nameof(ErrorText));
    }

    public sealed class ModelComparisonClassMetricRow
    {
        private readonly bool usesIoU;
        public ModelComparisonClassMetricRow(WpfModelBenchmarkClassMetric baseline, WpfModelBenchmarkClassMetric candidate, bool useIoU = false, Color? classColor = null) { Baseline = baseline; Candidate = candidate; usesIoU = useIoU; ClassColor = classColor.GetValueOrDefault(); HasClassColor = classColor.HasValue; }
        public WpfModelBenchmarkClassMetric Baseline { get; }
        public WpfModelBenchmarkClassMetric Candidate { get; }
        public string Name => Baseline?.ClassName ?? Candidate?.ClassName;
        public Color ClassColor { get; }
        public bool HasClassColor { get; }
        private double? BaselineValue => Valid(usesIoU ? Baseline?.IoU : Baseline?.Map50);
        private double? CandidateValue => Valid(usesIoU ? Candidate?.IoU : Candidate?.Map50);
        public bool HasValues => BaselineValue.HasValue || CandidateValue.HasValue;
        public string ValuesText => "A " + (BaselineValue?.ToString("0.00", CultureInfo.CurrentCulture) ?? "—") + " · B " + (CandidateValue?.ToString("0.00", CultureInfo.CurrentCulture) ?? "—");
        public double BaselineShare => BaselineValue ?? 0;
        public double CandidateShare => CandidateValue ?? 0;
        // B against A for this class, shown when the difference is at least 0.03.
        public bool IsImproved => BaselineValue.HasValue && CandidateValue.HasValue && CandidateValue - BaselineValue >= 0.03;
        public bool IsWorse => BaselineValue.HasValue && CandidateValue.HasValue && BaselineValue - CandidateValue >= 0.03;
        public string TrendText => IsImproved ? "▲" : IsWorse ? "▼" : string.Empty;
        private static double? Valid(double? value) => value.HasValue && double.IsFinite(value.Value) && value >= 0 && value <= 1 ? value : null;
    }

    public sealed class ModelComparisonDifferenceRow
    {
        private ModelComparisonDifferenceRow(string name, string aText, string bText, string deltaText, int trend) { Name = name; AText = aText; BText = bText; DeltaText = deltaText; Trend = trend; }
        public string Name { get; }
        public string AText { get; }
        public string BText { get; }
        public string DeltaText { get; }
        // 1: B is better, -1: B is worse, 0: same or unknown.
        public int Trend { get; }
        public bool IsBetter => Trend > 0;
        public bool IsWorse => Trend < 0;

        internal static ModelComparisonDifferenceRow Create(string name, double? a, double? b, bool higherIsBetter, string format)
        {
            static bool Usable(double? value) => value.HasValue && double.IsFinite(value.Value);
            string Text(double? value) => Usable(value) ? value.Value.ToString(format, CultureInfo.CurrentCulture) : "—";
            if (!Usable(a) || !Usable(b)) return new ModelComparisonDifferenceRow(name, Text(a), Text(b), "—", 0);
            double delta = b.Value - a.Value, rounded = Math.Round(delta, format.Length > 2 ? format.Length - 2 : 0);
            int trend = rounded == 0 ? 0 : (rounded > 0) == higherIsBetter ? 1 : -1;
            return new ModelComparisonDifferenceRow(name, Text(a), Text(b), (rounded > 0 ? "+" : string.Empty) + rounded.ToString(format, CultureInfo.CurrentCulture), trend);
        }
    }

    public sealed class ModelComparisonAgreementGroup : WpfObservableViewModel
    {
        private bool isSelected;
        internal ModelComparisonAgreementGroup(string key, int count, int total, Color color) { Key = key; Count = count; Share = total > 0 ? (double)count / total : 0; Color = color; }
        public string Key { get; }
        public int Count { get; }
        public double Share { get; }
        public Color Color { get; }
        public string Name => ModelComparisonWorkspaceViewModel.T(Key switch { "a" => "AgreementOnlyA", "b" => "AgreementOnlyB", "ab" => "AgreementBoth", _ => "AgreementNone" });
        public string CountText => string.Format(CultureInfo.CurrentCulture, ModelComparisonWorkspaceViewModel.T("AgreementCount"), Count);
        public bool IsSelected { get => isSelected; internal set => SetProperty(ref isSelected, value); }
        internal void RefreshText() { OnPropertyChanged(nameof(Name)); OnPropertyChanged(nameof(CountText)); }
    }
}
