using OpenVisionLab.Mvvm;
using OpenVisionLab;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using MvcVisionSystem._1._Core;

namespace MvcVisionSystem
{
    public sealed class WpfLabelingShellViewModel : WpfObservableViewModel
    {
        private static readonly Action NoOpCommand = () => { };
        private static readonly Action<KeyInputCommandArgs> NoOpKeyCommand = _ => { };
        private static readonly GridLength RightWorkflowExpandedPaneGridLengthValue = new GridLength(WpfWorkspaceLayoutSettings.DefaultWorkflowPaneWidth);
        private static readonly GridLength RightWorkflowCollapsedPaneGridLengthValue = new GridLength(72D);
        private static readonly GridLength WorkspaceCanvasPaneGridLengthValue = new GridLength(1D, GridUnitType.Star);
        private static readonly GridLength WorkspaceSplitterPaneGridLengthValue = new GridLength(10D);
        private static readonly GridLength WorkspaceHiddenPaneGridLengthValue = new GridLength(0D);
        private WpfShellWorkflowStage currentWorkflowStage = WpfShellWorkflowStage.Dataset;
        private Action<WpfShellWorkflowStage> workflowStageChanged;
        private bool isAnomalyImageReviewMode;
        private bool isInferenceInspectionOnly;
        private bool isLabelingAssistPanelExpanded;
        private int inferenceTargetIndex;
        private WpfModelRegistryHistoryItem selectedInferenceModelHistoryItem;
        private string selectedInferenceModelCandidateId = string.Empty;
        private string selectedInferenceModelWeightsPath = string.Empty;
        private bool hasExplicitInferenceModelSelection;
        private bool isRefreshingModelRegistryHistory;
        private Func<bool> inferenceRunStateProvider;
        private Action inferencePurposeChanged;
        private Action inferenceModelSelectionChanged;
        private Action runCurrentInferenceTarget;
        private Action runVisibleInferenceTarget;
        private Action runUnexecutedInferenceTarget;
        private Func<bool> canRunCurrentInferenceTarget;
        private Func<bool> canRunVisibleInferenceTarget;
        private Func<bool> canRunUnexecutedInferenceTarget;
        private string inferenceRunTargetUnavailableHint = string.Empty;
        private readonly InferenceResultReviewService inferenceResultReview = new();
        private Func<InferenceResultReviewEntry, (double? Threshold, bool? SavedVerdict)> inferenceAnomalyEvidenceProvider;
        private (double? Threshold, bool? SavedVerdict) inferenceAnomalyEvidence;
        private Action<InferenceResultReviewEntry> openInferenceDecisionRule;
        private ICommand openInferenceDecisionRuleCommand = new RelayCommand(NoOpCommand, () => false);
        private InferenceResultReviewContext inferenceReviewContext = InferenceResultReviewContext.Empty;
        private Func<InferenceResultReviewContext> inferenceReviewContextProvider;
        private Func<string, bool> openInferenceResultImage;
        private Action<YoloWorkerSmokeTestResult> restoreInferenceResult;
        private Action inferenceResultRoleChanged;
        private Action resetInferenceResultFilters;
        private Action<YoloWorkerSmokeCandidate> selectInferencePrediction;
        private Func<YoloWorkerSmokeCandidate> selectedInferencePredictionProvider;
        private InferenceResultSummary inferenceSummary = InferenceResultSummary.Empty;
        private IReadOnlyList<WpfCandidateReviewListItem> inferencePredictionListItems = Array.Empty<WpfCandidateReviewListItem>();
        private Func<IReadOnlyList<WpfClassCatalogListItem>> inferenceClassCatalogProvider;
        private Func<InferenceResultReviewEntry, double, bool> sendInferenceResultToLabeling;
        private bool sendingInferenceResultToLabeling;
        private ICommand sendInferenceResultsToLabelingCommand = new RelayCommand(NoOpCommand, () => false);
        private Func<string, string> exportInferenceResults;
        private Func<Func<string>, string> exportDeferredInferenceResults;
        private bool inferenceDecisionRefreshPending;
        private bool exportingInferenceResults;
        private ICommand exportInferenceResultsCommand = new RelayCommand(NoOpCommand, () => false);
        private bool? lastInferenceResultReadOnly;
        private bool refreshingInferenceResultPresentation;
        private bool inferenceResultsWereVisible;
        private bool inferenceWorkspaceWasActive;
        private bool inferenceResultRestorePending;
        private string inferenceNavigationMessage = string.Empty;

        private bool isCurrentImageDetectionEnabled;
        private bool canRunInferenceTarget;
        private bool isLabelingModeActive = true;
        private bool isInferenceModeActive;
        private bool isDatasetStageActive = true;
        private bool isRecipeLibraryVisible = true;
        private bool isLabelingStageActive;
        private bool isInferenceStageActive;
        private bool isTrainingModelStageActive;
        private bool isModelComparisonStageActive;
        private bool isSavedLabelsViewVisible;
        private bool isCandidateReviewViewVisible;
        private bool isGuideToolsViewVisible = true;
        private bool isClassCatalogViewVisible = true;
        private bool isClassCatalogDialogOpen;
        private bool isClassAddDialogOpen;
        private int operationsTabIndex = 3;
        private bool isYoloModelCenterViewVisible;
        private bool isRightWorkflowSubNavigationVisible;
        private bool isRightWorkflowShortcutBarVisible;
        private bool isRightWorkflowDockExpanded = true;
        private bool isRightWorkflowDockRailVisible;
        private double rightWorkflowExpandedPaneWidth = RightWorkflowExpandedPaneGridLengthValue.Value;
        private GridLength rightWorkflowPaneGridLength = RightWorkflowExpandedPaneGridLengthValue;
        private double workspaceViewportWidth;
        private bool workspaceLayoutSettingsRestored;
        private double rightWorkflowPaneMinWidth = 72D;
        private double rightWorkflowPaneMaxWidth = 640D;
        private bool isModelWorkspaceActive;
        private bool isCanvasWorkspaceVisible = true;
        private bool isImageQueueWorkspaceVisible = true;
        private bool isWorkspaceSplitterVisible = true;
        private bool isAnnotationWorkflowVisible = true;
        private FlowDirection labelingWorkspaceFlowDirection = FlowDirection.RightToLeft;
        private bool isRightWorkflowDockToggleVisible = true;
        private GridLength canvasWorkspacePaneGridLength = WorkspaceCanvasPaneGridLengthValue;
        private GridLength imageQueuePaneGridLength = new GridLength(WpfWorkspaceLayoutSettings.DefaultImageQueuePaneWidth);
        private GridLength workspaceSplitterPaneGridLength = WorkspaceSplitterPaneGridLengthValue;
        private int rightWorkflowDockColumn;
        private int imageQueueColumn = 4;
        private double workflowContextHeaderHeight = 48D;
        private double workflowStageRailHeight = 48D;
        private double canvasWorkspacePaneMinWidth = 440D;
        private double imageQueuePaneMinWidth = WpfWorkspaceLayoutSettings.MinimumImageQueuePaneWidth;
        private double imageQueueExpandedPaneWidth = WpfWorkspaceLayoutSettings.DefaultImageQueuePaneWidth;
        private string rightWorkflowDockToggleText;
        private string rightWorkflowDockToggleToolTip;
        private string rightWorkflowViewTitleText = OpenVisionLanguageService.T("WpfShell.Right.Title.Dataset");
        private string rightWorkflowViewDetailText;
        private string rightWorkflowRailCurrentViewText;
        private bool isSavedLabelsShortcutActive;
        private bool isLabelingGuideShortcutActive = true;
        private bool isClassCatalogShortcutActive;
        private string workflowStageProgressText = WorkflowStagePresentationService.Build(WpfShellWorkflowStage.Dataset).ProgressText;
        private string workflowStageTitleText = WorkflowStagePresentationService.Build(WpfShellWorkflowStage.Dataset).TitleText;
        private string workflowStageDetailText = WorkflowStagePresentationService.Build(WpfShellWorkflowStage.Dataset).DetailText;
        private string workflowStageNextActionText = WorkflowStagePresentationService.Build(WpfShellWorkflowStage.Dataset).NextActionText;
        private bool isLabelingModeButtonEnabled = true;
        private bool isInferenceModeButtonEnabled = true;
        private bool isOpenDatasetFolderEnabled;
        private string currentDatasetName;
        private string currentDatasetPurposeText;
        private string currentDatasetPathText;
        private string currentDatasetStoragePathText;
        private string currentDatasetImageRootText;
        private string currentDatasetSourceText;
        private string currentDatasetToolTip;
        private string modelCenterTrainingStatusText;
        private string modelCenterTrainingDetailText;
        private string modelCenterCurrentModelText;
        private string modelCenterCandidateModelText;
        private string modelCenterAdoptionText;
        private string modelCenterNextActionText;
        private string modelCenterCurrentModelDetailText;
        private string modelCenterCandidateModelDetailText;
        private string modelCenterAdoptionDetailText;
        private string modelCenterNextActionDetailText;
        private string modelCenterDecisionSummaryText;
        private string modelCenterDecisionEvidenceText;
        private string modelCenterDecisionActionText;
        private bool isModelCenterAnomalyEvaluationVisible;
        private string modelCenterAnomalyEvaluationRecommendationText = string.Empty;
        private string modelCenterAnomalyEvaluationMetricsText = string.Empty;
        private string modelCenterAnomalyEvaluationDetailText = string.Empty;
        private string modelCenterAnomalyEvaluationActionText = string.Empty;
        private bool isModelCenterAnomalyEvaluationPickerVisible;
        private bool isModelCenterAnomalyEvaluationPickerEnabled;
        private string modelRegistrySummaryPrimaryText;
        private string modelRegistrySummarySecondaryText = OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.State.EmptySecondary");
        private string modelRegistryProfileText;
        private string modelRegistryTrainingRunText;
        private string modelRegistryCandidateModelText;
        private string modelRegistryInspectionModelText = OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.State.InspectionNone");
        private string modelRegistryActionText;
        private string modelRegistryHistoryHeaderText = OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.State.HistoryHeaderEmpty");
        private WpfModelRegistryPresentation modelRegistryLocalizationSource = new WpfModelRegistryPresentation();
        private string modelRegistryHistorySummaryText;
        private WpfModelRegistryHistoryItem selectedModelRegistryHistoryItem;
        private string selectedModelHistoryTitleText = OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.History.Selection.NoneTitle");
        private string selectedModelHistoryDetailText = OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.History.Selection.NoneDetail");
        private string selectedModelHistoryMetricText = string.Empty;
        private string selectedModelHistoryDecisionText = string.Empty;
        private string selectedModelHistoryComparisonTitleText = OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.History.Selection.ComparisonTitle");
        private string selectedModelHistoryCurrentModelText = string.Empty;
        private string selectedModelHistorySelectedModelText = string.Empty;
        private string selectedModelHistoryComparisonMetricText = string.Empty;
        private string selectedModelHistoryActionText = OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.History.Selection.CannotAssign");
        private string selectedModelHistoryActionToolTip = OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.History.Selection.SelectFirst");
        private bool isModelRegistryHistoryVisible;
        private bool isSelectedModelHistoryVisible;
        private bool isSelectedModelHistoryActionAvailable;
        private bool isSelectedModelHistoryActionEnabled;
        private bool isModelCenterRecoveryVisible;
        private string modelCenterRecoveryTitleText = string.Empty;
        private string modelCenterRecoveryDetailText = string.Empty;
        private string modelCenterRecoveryActionText = string.Empty;
        private string modelCenterConfirmModelButtonText;
        private string modelCenterConfirmModelButtonToolTip;
        private string modelCenterConfirmModelBaseToolTip;
        private string modelCenterConfirmModelUnavailableToolTip = string.Empty;
        private bool isModelCenterConfirmModelEnabled;
        private bool isModelCenterConfirmModelAvailable;
        private string modelCenterInspectCurrentImageButtonText;
        private string modelCenterInspectCurrentImageButtonToolTip;
        private bool isModelCenterInspectCurrentImageEnabled;
        private string modelCenterReviewCandidateButtonText;
        private string modelCenterReviewCandidateButtonToolTip;
        private bool isModelCenterReviewCandidateEnabled;
        private bool isModelCenterReviewCandidateAvailable;
        private string modelCenterRuntimeActionText = string.Empty;
        private string modelCenterActionStateText;
        private bool canRunModelCenterCommands = true;
        private bool canRunModelCenterReviewCommands = true;
        private ICommand loadedCommand = new RelayCommand(NoOpCommand);
        private ICommand closedCommand = new RelayCommand(NoOpCommand);
        private ICommand deactivatedCommand = new RelayCommand(NoOpCommand);
        private ICommand previewKeyDownCommand = new RelayCommand<KeyInputCommandArgs>(NoOpKeyCommand);
        private ICommand previewKeyUpCommand = new RelayCommand<KeyInputCommandArgs>(NoOpKeyCommand);
        private ICommand toggleThemeCommand = new RelayCommand(NoOpCommand);
        private ICommand loadSampleCommand = new RelayCommand(NoOpCommand);
        private ICommand addSampleRoiCommand = new RelayCommand(NoOpCommand);
        private ICommand saveAnnotationsCommand = new RelayCommand(NoOpCommand);
        private ICommand labelingModeCommand = new RelayCommand(NoOpCommand);
        private ICommand inferenceModeCommand = new RelayCommand(NoOpCommand);
        private ICommand datasetHomeCommand = new RelayCommand(NoOpCommand);
        private ICommand labelingWorkbenchCommand = new RelayCommand(NoOpCommand);
        private ICommand inferenceReviewCommand = new RelayCommand(NoOpCommand);
        private ICommand trainingModelCenterCommand = new RelayCommand(NoOpCommand);
        private ICommand modelComparisonCommand = new RelayCommand(NoOpCommand);
        private ICommand reviewCandidateModelCommand = new RelayCommand(NoOpCommand);
        private ICommand promoteSelectedModelHistoryCommand = new RelayCommand(NoOpCommand);
        private ICommand runAnomalyEvaluationCommand = new RelayCommand(NoOpCommand);
        private ICommand loadAnomalyEvaluationSummaryCommand = new RelayCommand(NoOpCommand);
        private ICommand openModelBenchmarkCommand = new RelayCommand(NoOpCommand);
        private ICommand openDatasetHealthCommand = new RelayCommand(NoOpCommand);
        private ICommand openDatasetInterchangeCommand = new RelayCommand(NoOpCommand);
        private AnomalyClassificationEvaluationWorkflowService anomalyClassificationEvaluationWorkflowService;
        private Func<AnomalyEvaluationCallbacks> anomalyEvaluationCallbacksProvider;
        private ICommand showSavedLabelsViewCommand = new RelayCommand(NoOpCommand);
        private ICommand showLabelingGuideViewCommand = new RelayCommand(NoOpCommand);
        private ICommand showClassCatalogViewCommand = new RelayCommand(NoOpCommand);
        private ICommand closeClassCatalogDialogCommand;
        private ICommand toggleRightWorkflowDockCommand;
        private ICommand resetWorkspaceLayoutCommand = new RelayCommand(NoOpCommand);
        private ICommand workflowPaneResizeCommand = new RelayCommand<double>(_ => { });
        private ICommand imageQueuePaneResizeCommand = new RelayCommand<double>(_ => { });
        private ICommand workspaceViewportSizeChangedCommand = new RelayCommand<double>(_ => { });
        private ICommand checkYoloCommand = new RelayCommand(NoOpCommand);
        private ICommand detectCurrentImageCommand = new RelayCommand(NoOpCommand);
        private ICommand runInferenceTargetCommand = new RelayCommand(NoOpCommand);
        private ICommand toggleLabelingAssistPanelCommand = new RelayCommand(NoOpCommand);
        private ICommand applySelectedInferenceModelCommand = new RelayCommand(NoOpCommand);
        private ICommand markInferenceResultSeenCommand = new RelayCommand(NoOpCommand);
        private ICommand markInferenceResultSeenAndNextCommand = new RelayCommand(NoOpCommand);
        private ICommand showInferenceResultsCommand = new RelayCommand(NoOpCommand);
        private ICommand runTemplateMatchingCommand = new RelayCommand(NoOpCommand);
        private ICommand changeDatasetCommand = new RelayCommand(NoOpCommand);
        private ICommand closeRecipeCommand = new RelayCommand(NoOpCommand);
        private ICommand openDatasetFolderCommand = new RelayCommand(NoOpCommand);
        private ICommand changeImageFolderCommand = new RelayCommand(NoOpCommand);
        private ICommand completeLabelingCommand = new RelayCommand(NoOpCommand);
        private ICommand cancelLabelingCommand = new RelayCommand(NoOpCommand);
        private readonly WorkspaceLayoutSettingsService workspaceLayoutSettingsService = new WorkspaceLayoutSettingsService();
        private string workspaceLayoutLastSaveError = string.Empty;

        public event EventHandler WorkspaceLayoutSaveFailed;
        public event EventHandler WorkspaceLayoutReset;

        public ObservableCollection<WpfModelRegistryHistoryItem> ModelRegistryHistoryItems { get; } = new ObservableCollection<WpfModelRegistryHistoryItem>();

        public string ViewName => nameof(WpfLabelingShellWindow);

        public bool IsCurrentImageDetectionEnabled
        {
            get => isCurrentImageDetectionEnabled;
            private set
            {
                if (SetProperty(ref isCurrentImageDetectionEnabled, value))
                {
                    RefreshInferenceTargetCommandState();
                }
            }
        }

        public bool IsInferenceTargetEnabled => canRunInferenceTarget && IsInferenceModelSelectionReady;
        public bool CanRunInferenceTarget => CanRunInferenceTargetCommand();
        public bool HasCompletedInferenceResult(string imagePath) => inferenceResultReview.Find(imagePath)?.Result != null;
        internal InferenceResultReviewService InferenceResultReview => inferenceResultReview;

        public InferenceResultReviewEntry DisplayedInferenceResult => inferenceReviewContext.IsComparisonEvidence ? null
            : IsLabelingStageActive && inferenceReviewContext.LabelingTransferEntry != null ? inferenceReviewContext.LabelingTransferEntry : inferenceResultReview.Find(inferenceReviewContext.ImagePath);
        public bool HasDisplayedInferenceResult => DisplayedInferenceResult?.Result != null;
        public bool IsInferenceNotRunHintVisible => !inferenceReviewContext.IsBusy && !inferenceReviewContext.IsComparisonEvidence
            && !string.IsNullOrWhiteSpace(inferenceReviewContext.ImagePath) && !HasDisplayedInferenceResult && DisplayedInferenceResult?.Failure == null;
        public string InferencePartDecisionText
        {
            get
            {
                if (!HasDisplayedInferenceResult) return string.Empty;
                ModelDecisionResult result = inferenceResultReview.GetDecisionProjection(DisplayedInferenceResult);
                string verdict = result.IsNg.HasValue ? OpenVisionLanguageService.T(result.IsNg.Value ? "WpfInference.Decision.Ng" : "WpfInference.Decision.Ok")
                    : FormatInferenceDecisionStatusLabel(result);
                return string.Format(CultureInfo.CurrentCulture, OpenVisionLanguageService.T("WpfInference.Decision.Format"), verdict);
            }
        }
        public bool IsAnomalyInference => IsInferenceStageActive && IsAnomalyImageReviewMode;
        public string InferenceDecisionTitleText => T(IsAnomalyImageReviewMode ? "WpfInference.ImageDecision" : "WpfInference.PartDecision");
        public bool? InferenceDecisionIsNg => HasDisplayedInferenceResult ? inferenceResultReview.GetDecisionProjection(DisplayedInferenceResult).IsNg : null;
        public string InferenceDecisionLabelText
        {
            get
            {
                if (!HasDisplayedInferenceResult) return string.Empty;
                ModelDecisionResult decision = inferenceResultReview.GetDecisionProjection(DisplayedInferenceResult);
                return decision.IsNg.HasValue ? InferenceDecisionValueText : FormatInferenceDecisionStatusLabel(decision);
            }
        }
        public string InferenceDecisionValueText
        {
            get
            {
                if (!HasDisplayedInferenceResult) return string.Empty;
                ModelDecisionResult decision = inferenceResultReview.GetDecisionProjection(DisplayedInferenceResult);
                return !decision.IsNg.HasValue ? InferencePartDecisionText
                    : IsAnomalyImageReviewMode ? T(decision.IsNg.Value ? "WpfInference.Decision.Ng" : "WpfInference.Decision.Ok") : decision.IsNg.Value ? "NG" : "OK";
            }
        }
        public string InferenceAnomalyThresholdText
        {
            get
            {
                double? threshold = inferenceAnomalyEvidence.Threshold ?? CapturedInferenceAnomalyThreshold;
                if (!threshold.HasValue) return T("WpfInference.Anomaly.ThresholdUnavailable");
                string text = string.Format(CultureInfo.CurrentCulture, T(inferenceAnomalyEvidence.Threshold.HasValue ? "WpfInference.Anomaly.DecisionThreshold" : "WpfInference.Anomaly.CapturedThreshold"), threshold.Value.ToString("0.####", CultureInfo.CurrentCulture));
                return inferenceAnomalyEvidence.Threshold.HasValue ? text : text + " · " + T("WpfComparisonWorkspace.RuleUnconfigured");
            }
        }
        private double? CapturedInferenceAnomalyThreshold
        {
            get
            {
                double[] thresholds = DisplayedInferenceResult?.Result?.Candidates?.Where(candidate => candidate?.AnomalyThreshold != null && double.IsFinite(candidate.AnomalyThreshold.Value))
                    .Select(candidate => candidate.AnomalyThreshold.Value).Distinct().Take(2).ToArray() ?? Array.Empty<double>();
                return thresholds.Length == 1 ? thresholds[0] : null;
            }
        }
        public string InferenceAnomalyScoreText
        {
            get
            {
                double? score = DisplayedInferenceResult?.Result?.CaptureAnomalyScore;
                score ??= DisplayedInferenceResult?.Result?.Candidates?.FirstOrDefault(candidate => candidate?.ImageLevel == true && candidate.AnomalyScore.HasValue)?.AnomalyScore;
                double? threshold = inferenceAnomalyEvidence.Threshold ?? CapturedInferenceAnomalyThreshold;
                return string.Format(CultureInfo.CurrentCulture, T(inferenceAnomalyEvidence.Threshold.HasValue ? "WpfInference.Anomaly.ScoreThreshold" : "WpfInference.Anomaly.ScoreCapturedThreshold"),
                    score.HasValue && double.IsFinite(score.Value) ? score.Value.ToString("0.####", CultureInfo.CurrentCulture) : "—",
                    threshold.HasValue ? threshold.Value.ToString("0.####", CultureInfo.CurrentCulture) : "—");
            }
        }
        public string InferenceSavedVerdictText
        {
            get
            {
                if (!inferenceAnomalyEvidence.SavedVerdict.HasValue) return T("WpfInference.Anomaly.SavedNone");
                bool? decision = inferenceResultReview.GetDecisionProjection(DisplayedInferenceResult).IsNg;
                string agreement = !decision.HasValue ? T("WpfInference.Queue.DecisionUnknown") : T(decision == inferenceAnomalyEvidence.SavedVerdict ? "WpfInference.Anomaly.Agrees" : "WpfInference.Anomaly.Disagrees");
                return string.Format(CultureInfo.CurrentCulture, T("WpfInference.Anomaly.SavedVerdict"), T(inferenceAnomalyEvidence.SavedVerdict.Value ? "WpfAnomalyGrid.AbnormalState" : "WpfAnomalyGrid.NormalState"), agreement);
            }
        }
        public string InferenceVisiblePredictionCountText => string.Format(CultureInfo.CurrentCulture, T("WpfInference.VisibleResults"), InferencePredictions.Count, inferenceReviewContext.DisplayConfidence.ToString("0.00", CultureInfo.CurrentCulture));
        public string InferencePanelTitleText => T("WpfInference.Workspace.Results");
        public bool IsInferenceResultReadOnly => IsInferenceInspectionOnly || DisplayedInferenceResult?.Result?.Source?.ResultOnly == true && !(IsLabelingStageActive && inferenceReviewContext.LabelingTransferEntry != null);
        public bool CanReviewInferenceResult => IsInferenceControlsVisible && !inferenceReviewContext.IsBusy && !inferenceReviewContext.IsClosing && !inferenceReviewContext.HasUnsavedLabels && !inferenceReviewContext.HasPendingCandidates && !inferenceReviewContext.IsComparisonEvidence && !(IsLabelingStageActive && inferenceReviewContext.LabelingTransferEntry != null);
        public bool CanMarkInferenceResultSeen => CanReviewInferenceResult && HasDisplayedInferenceResult && DisplayedInferenceResult?.Seen != true;
        public bool CanMoveToNextInferenceResult => CanReviewInferenceResult && HasDisplayedInferenceResult;
        public bool CanShowInferenceResults => CanReviewInferenceResult && inferenceResultReview.ResultPaths.Count > 0;
        public bool CanSelectInferencePrediction => CanReviewInferenceResult && HasDisplayedInferenceResult;
        public bool CanSendInferenceResultsToLabeling => sendInferenceResultToLabeling != null && !sendingInferenceResultToLabeling && IsInferenceStageActive && CanReviewInferenceResult && HasDisplayedInferenceResult && !IsAnomalyImageReviewMode
            && DisplayedInferenceResult.Result.Candidates?.Any(candidate => candidate?.ImageLevel == true) != true;
        public bool IsInferenceSummaryReady => !inferenceReviewContext.IsBusy;
        public bool CanExportInferenceResults => (exportInferenceResults != null || exportDeferredInferenceResults != null) && !exportingInferenceResults && IsInferenceStageActive && !inferenceReviewContext.IsBusy && !inferenceReviewContext.IsClosing
            && !inferenceReviewContext.IsComparisonEvidence && inferenceReviewContext.NextSettings != null
            && (inferenceReviewContext.AllImages ?? inferenceReviewContext.VisibleImages)?.Any(path => !string.IsNullOrWhiteSpace(path)) == true;
        public IReadOnlyList<InferenceClassSummary> InferenceClassSummaryItems => inferenceSummary.Classes;
        public IReadOnlyList<WpfCandidateReviewListItem> InferenceClassSummaryRows => inferenceSummary.Classes.Select(item => BuildInferenceReviewItem(item.ClassName, item.ClassId, item.Count.ToString(CultureInfo.CurrentCulture), item)).ToArray();
        public string InferenceSummaryText => string.Format(CultureInfo.CurrentCulture, T(IsAnomalyImageReviewMode ? "WpfInference.Anomaly.Summary" : "WpfInference.Decision.Summary"),
            inferenceSummary.CompletedImages, inferenceSummary.TotalImages, inferenceSummary.NgImages, inferenceSummary.OkImages, inferenceSummary.UnknownImages);
        public string InferenceResultTitleText => HasDisplayedInferenceResult
            ? string.Format(CultureInfo.CurrentCulture, OpenVisionLanguageService.T("WpfInference.Workspace.ResultCount"), DisplayedInferenceResult.Result.CandidateCount)
            : OpenVisionLanguageService.T(DisplayedInferenceResult?.Failure != null ? "WpfInference.Workspace.Failed" : "WpfInference.Workspace.NotRun");
        public IReadOnlyList<YoloWorkerSmokeCandidate> InferencePredictions => DisplayedInferenceResult?.Result?.Candidates?.Where(candidate => candidate != null && (IsAnomalyImageReviewMode || candidate.ImageLevel || candidate.Confidence >= inferenceReviewContext.DisplayConfidence)).ToArray() ?? Array.Empty<YoloWorkerSmokeCandidate>();
        public IReadOnlyList<WpfCandidateReviewListItem> InferencePredictionListItems => inferencePredictionListItems;
        public bool HasVisibleInferencePredictions => InferencePredictions.Count > 0;
        public string InferenceResultEmptyText => !HasDisplayedInferenceResult
            ? OpenVisionLanguageService.T("WpfInference.ResultOnly.Panel.Empty")
            : OpenVisionLanguageService.T(DisplayedInferenceResult.Result.CandidateCount == 0 ? "WpfInference.ResultOnly.Panel.CompletedEmpty" : "WpfInference.Workspace.FilteredEmpty");
        public YoloWorkerSmokeCandidate SelectedInferencePrediction
        {
            get
            {
                YoloWorkerSmokeCandidate selected = selectedInferencePredictionProvider?.Invoke();
                return selected != null && InferencePredictions.Contains(selected) ? selected : null;
            }
            set
            {
                if (!CanSelectInferencePrediction || ReferenceEquals(SelectedInferencePrediction, value)
                    || value != null && !InferencePredictions.Contains(value)) return;
                selectInferencePrediction?.Invoke(value);
                OnPropertyChanged();
            }
        }
        public bool IsInferenceResultOutsideFilter => !string.IsNullOrWhiteSpace(inferenceReviewContext.ImagePath) && !inferenceReviewContext.VisibleImages.Contains(inferenceReviewContext.ImagePath, StringComparer.OrdinalIgnoreCase);
        public bool IsInferenceResultSettingsChanged => DisplayedInferenceResult?.Result?.Source is { } source && !source.Matches(inferenceReviewContext.NextSettings,
            IsLabelingStageActive && inferenceReviewContext.LabelingTransferEntry != null ? source.ResultOnly : IsInferenceInspectionOnly);
        public string InferenceResultSourceText
        {
            get
            {
                InferenceResultSource source = DisplayedInferenceResult?.Result?.Source;
                if (source == null) return OpenVisionLanguageService.T(inferenceReviewContext.IsComparisonEvidence ? "WpfInference.Source.Comparison" : "WpfInference.Source.Empty");
                return string.Format(CultureInfo.CurrentCulture, OpenVisionLanguageService.T("WpfInference.Source.Format"), Path.GetFileName(source.ImagePath), source.ModelEngine, Path.GetFileName(source.WeightsPath), source.ImageSize, source.Confidence, source.CompletedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture), OpenVisionLanguageService.T(source.ResultOnly ? "WpfInference.Purpose.ResultOnly" : "WpfInference.Purpose.Labeling"));
            }
        }
        public string InferenceResultCountsText
        {
            get
            {
                if (!HasDisplayedInferenceResult) return string.Empty;
                int total = DisplayedInferenceResult.Result.CandidateCount;
                int visible = Math.Clamp(IsInferenceStageActive ? InferencePredictions.Count : inferenceReviewContext.VisiblePredictions, 0, Math.Max(0, total));
                return string.Format(CultureInfo.CurrentCulture, OpenVisionLanguageService.T("WpfInference.Source.Counts"), total, visible, Math.Max(0, total - visible));
            }
        }
        public string InferenceResultWarningText
        {
            get
            {
                string failure = DisplayedInferenceResult?.Failure?.Summary;
                if (!string.IsNullOrWhiteSpace(failure)) return string.Format(CultureInfo.CurrentCulture, OpenVisionLanguageService.T(HasDisplayedInferenceResult ? "WpfInference.Source.FailedPrevious" : "WpfInference.Source.Failed"), failure);
                return IsInferenceResultSettingsChanged ? OpenVisionLanguageService.T("WpfInference.Source.Changed") : string.Empty;
            }
        }
        public string InferenceResultReviewText => !string.IsNullOrEmpty(inferenceNavigationMessage) ? inferenceNavigationMessage : OpenVisionLanguageService.T(DisplayedInferenceResult?.Seen == true ? "WpfInference.Seen.Done" : "WpfInference.Seen.Detail");
        public string InferenceResultOutsideFilterText => IsInferenceResultOutsideFilter ? OpenVisionLanguageService.T("WpfInference.Source.OutsideFilter") : string.Empty;

        public bool IsInferenceInspectionOnly
        {
            get => isInferenceInspectionOnly;
            set
            {
                if (!CanChangeInferencePurpose || !SetProperty(ref isInferenceInspectionOnly, value))
                {
                    return;
                }

                OnPropertyChanged(nameof(IsInferenceLabelingPurposeSelected));
                inferencePurposeChanged?.Invoke();
            }
        }

        public bool IsLabelingAssistPanelExpanded
        {
            get => isLabelingAssistPanelExpanded;
            private set
            {
                if (!CanToggleLabelingAssistPanel || !SetProperty(ref isLabelingAssistPanelExpanded, value))
                {
                    return;
                }

                if (value && isInferenceInspectionOnly)
                {
                    IsInferenceInspectionOnly = false;
                }
                else
                {
                    inferencePurposeChanged?.Invoke();
                }

                OnPropertyChanged(nameof(IsInferenceControlsVisible));
                RefreshInferenceTargetCommandState();
                RefreshLabelingAssistPanelCommandState();
            }
        }

        public bool IsInferenceControlsVisible
            => IsInferenceStageActive || (IsLabelingStageActive && IsLabelingAssistPanelExpanded);

        public bool CanToggleLabelingAssistPanel => IsLabelingStageActive && CanChangeInferencePurpose;

        public bool IsInferenceLabelingPurposeSelected
        {
            get => !isInferenceInspectionOnly;
            set
            {
                if (value)
                {
                    IsInferenceInspectionOnly = false;
                }
            }
        }

        public bool CanChangeInferencePurpose => inferenceRunStateProvider?.Invoke() != true;

        public string InferenceRunTargetToolTipText => !IsInferenceModelSelectionReady
            ? OpenVisionLanguageService.T("WpfInference.Model.TrialUnavailable")
            : !canRunInferenceTarget
                ? inferenceRunTargetUnavailableHint
                : OpenVisionLanguageService.T(inferenceTargetIndex switch
                {
                    1 => canRunUnexecutedInferenceTarget?.Invoke() == true ? "WpfInference.Run.Unexecuted.ToolTip" : "WpfInference.Run.Unexecuted.Empty",
                    2 => "WpfInference.Run.All.ToolTip",
                    _ => "WpfInference.Run.Current.ToolTip"
                });

        public bool IsInferenceVisibleListTarget
        {
            get => inferenceTargetIndex == 2;
            set { if (value) InferenceTargetIndex = 2; else if (inferenceTargetIndex == 2) InferenceTargetIndex = 0; }
        }

        public string[] InferenceTargetOptions => new[]
        {
            OpenVisionLanguageService.T("WpfInference.Target.Current"),
            OpenVisionLanguageService.T("WpfInference.Target.Unexecuted"),
            OpenVisionLanguageService.T("WpfInference.Target.All")
        };

        public int InferenceTargetIndex
        {
            get => inferenceTargetIndex;
            set
            {
                if (!CanChangeInferencePurpose || value < 0 || value > 2 || !SetProperty(ref inferenceTargetIndex, value)) return;
                OnPropertyChanged(nameof(IsInferenceCurrentImageTarget));
                OnPropertyChanged(nameof(IsInferenceVisibleListTarget));
                RefreshInferenceTargetCommandState();
            }
        }

        public bool IsInferenceCurrentImageTarget
        {
            get => inferenceTargetIndex == 0;
            set
            {
                if (value)
                {
                    InferenceTargetIndex = 0;
                }
            }
        }

        public bool IsInferenceModelSelectionReady
            => SelectedInferenceModelHistoryItem == null
                || SelectedInferenceModelHistoryItem.IsCurrentInspectionModel
                || SelectedInferenceModelHistoryItem.CanRunInferenceTrial;

        public WpfModelRegistryHistoryItem SelectedInferenceModelHistoryItem
        {
            get => selectedInferenceModelHistoryItem;
            set
            {
                if (!CanChangeInferencePurpose
                    || (isRefreshingModelRegistryHistory && value == null))
                {
                    return;
                }

                if (value != null)
                {
                    IReadOnlyList<WpfModelRegistryHistoryItem> history = ModelRegistryHistoryPresentationService.IncludeInferenceSelection(ModelRegistryHistoryItems, value);
                    if (!ReferenceEquals(history, ModelRegistryHistoryItems))
                    {
                        isRefreshingModelRegistryHistory = true;
                        try
                        {
                            ModelRegistryHistoryItems.Clear();
                            foreach (WpfModelRegistryHistoryItem item in history) ModelRegistryHistoryItems.Add(item);
                        }
                        finally { isRefreshingModelRegistryHistory = false; }
                    }

                    value = ModelRegistryHistoryItems.First(item => string.Equals(item.CandidateId, value.CandidateId, StringComparison.Ordinal)
                        && string.Equals(item.WeightsPath, value.WeightsPath, StringComparison.OrdinalIgnoreCase));
                }

                if (!SetProperty(ref selectedInferenceModelHistoryItem, value)) return;
                hasExplicitInferenceModelSelection = value != null;
                selectedInferenceModelCandidateId = value?.CandidateId ?? string.Empty;
                selectedInferenceModelWeightsPath = value?.WeightsPath ?? string.Empty;
                OnPropertyChanged(nameof(IsInferenceModelSelectionReady));
                OnPropertyChanged(nameof(IsInferenceTargetEnabled));
                OnPropertyChanged(nameof(CanApplySelectedInferenceModel));
                RefreshInferenceTargetCommandState();
                RefreshInferenceModelCommandState();
                inferenceModelSelectionChanged?.Invoke();
                OnPropertyChanged(nameof(InferenceRunTargetToolTipText));
            }
        }

        public bool CanApplySelectedInferenceModel
            => CanChangeInferencePurpose
                && SelectedInferenceModelHistoryItem?.CanPromoteToInspectionModel == true;

        public bool IsLabelingModeActive
        {
            get => isLabelingModeActive;
            private set => SetProperty(ref isLabelingModeActive, value);
        }

        public bool IsInferenceModeActive
        {
            get => isInferenceModeActive;
            private set => SetProperty(ref isInferenceModeActive, value);
        }

        public bool IsDatasetStageActive
        {
            get => isDatasetStageActive;
            private set => SetProperty(ref isDatasetStageActive, value);
        }

        public bool IsRecipeLibraryVisible
        {
            get => isRecipeLibraryVisible;
            private set
            {
                if (SetProperty(ref isRecipeLibraryVisible, value))
                {
                    OnPropertyChanged(nameof(MainWorkspaceVisibility));
                    OnPropertyChanged(nameof(RecipeLibraryVisibility));
                    OnPropertyChanged(nameof(IsStackedWorkflowHeader));
                }
            }
        }

        public Visibility MainWorkspaceVisibility => IsRecipeLibraryVisible ? Visibility.Collapsed : Visibility.Visible;

        public Visibility RecipeLibraryVisibility => IsRecipeLibraryVisible ? Visibility.Visible : Visibility.Collapsed;

        internal void SetRecipeLibraryVisible(bool visible)
        {
            if (visible) IsClassCatalogDialogOpen = false;
            IsRecipeLibraryVisible = visible;
        }

        public bool IsLabelingStageActive
        {
            get => isLabelingStageActive;
            private set => SetProperty(ref isLabelingStageActive, value);
        }

        public bool IsInferenceStageActive
        {
            get => isInferenceStageActive;
            private set
            {
                if (SetProperty(ref isInferenceStageActive, value))
                {
                    RefreshInferenceTargetCommandState();
                    inferencePurposeChanged?.Invoke();
                }
            }
        }

        public bool IsTrainingModelStageActive
        {
            get => isTrainingModelStageActive;
            private set => SetProperty(ref isTrainingModelStageActive, value);
        }

        public bool IsModelComparisonStageActive
        {
            get => isModelComparisonStageActive;
            private set => SetProperty(ref isModelComparisonStageActive, value);
        }

        public bool IsSavedLabelsViewVisible
        {
            get => isSavedLabelsViewVisible;
            private set => SetProperty(ref isSavedLabelsViewVisible, value);
        }

        public bool IsAnomalyImageReviewMode
        {
            get => isAnomalyImageReviewMode;
            private set => SetProperty(ref isAnomalyImageReviewMode, value);
        }

        public bool IsAnnotationWorkflowVisible
        {
            get => isAnnotationWorkflowVisible;
            private set => SetProperty(ref isAnnotationWorkflowVisible, value);
        }

        public bool IsCandidateReviewViewVisible
        {
            get => isCandidateReviewViewVisible;
            private set => SetProperty(ref isCandidateReviewViewVisible, value);
        }

        public bool IsGuideToolsViewVisible
        {
            get => isGuideToolsViewVisible;
            private set => SetProperty(ref isGuideToolsViewVisible, value);
        }

        public bool IsClassCatalogViewVisible
        {
            get => isClassCatalogViewVisible;
            private set => SetProperty(ref isClassCatalogViewVisible, value);
        }

        public bool IsClassCatalogDialogOpen
        {
            get => isClassCatalogDialogOpen;
            private set
            {
                SetProperty(ref isClassCatalogDialogOpen, value);
                if (!value) IsClassAddDialogOpen = false;
            }
        }

        public bool IsClassAddDialogOpen
        {
            get => isClassAddDialogOpen;
            private set => SetProperty(ref isClassAddDialogOpen, value);
        }

        public int OperationsTabIndex
        {
            get => operationsTabIndex;
            set => SetProperty(ref operationsTabIndex, Math.Clamp(value, 0, 3));
        }

        public bool IsYoloModelCenterViewVisible
        {
            get => isYoloModelCenterViewVisible;
            private set => SetProperty(ref isYoloModelCenterViewVisible, value);
        }

        public bool IsRightWorkflowSubNavigationVisible
        {
            get => isRightWorkflowSubNavigationVisible;
            private set => SetProperty(ref isRightWorkflowSubNavigationVisible, value);
        }

        public bool IsRightWorkflowShortcutBarVisible
        {
            get => isRightWorkflowShortcutBarVisible;
            private set => SetProperty(ref isRightWorkflowShortcutBarVisible, value);
        }

        public bool IsRightWorkflowDockExpanded
        {
            get => isRightWorkflowDockExpanded;
            private set => SetProperty(ref isRightWorkflowDockExpanded, value);
        }

        public bool IsRightWorkflowDockRailVisible
        {
            get => isRightWorkflowDockRailVisible;
            private set => SetProperty(ref isRightWorkflowDockRailVisible, value);
        }

        public bool IsStackedWorkflowHeader => !IsRecipeLibraryVisible && workspaceViewportWidth <= 1600D;
        public bool IsCompactDatasetLayout => workspaceViewportWidth < 1400D;
        public bool IsCompactInferenceLayout => workspaceViewportWidth <= 1500D;

        public GridLength RightWorkflowPaneGridLength
        {
            get => rightWorkflowPaneGridLength;
            private set => SetProperty(ref rightWorkflowPaneGridLength, value);
        }

        public double RightWorkflowPaneMinWidth
        {
            get => rightWorkflowPaneMinWidth;
            private set => SetProperty(ref rightWorkflowPaneMinWidth, value);
        }

        public double RightWorkflowPaneMaxWidth
        {
            get => rightWorkflowPaneMaxWidth;
            private set => SetProperty(ref rightWorkflowPaneMaxWidth, value);
        }

        public bool IsModelWorkspaceActive
        {
            get => isModelWorkspaceActive;
            private set => SetProperty(ref isModelWorkspaceActive, value);
        }

        public bool IsCanvasWorkspaceVisible
        {
            get => isCanvasWorkspaceVisible;
            private set => SetProperty(ref isCanvasWorkspaceVisible, value);
        }

        public bool IsImageQueueWorkspaceVisible
        {
            get => isImageQueueWorkspaceVisible;
            private set => SetProperty(ref isImageQueueWorkspaceVisible, value);
        }

        public bool IsWorkspaceSplitterVisible
        {
            get => isWorkspaceSplitterVisible;
            private set => SetProperty(ref isWorkspaceSplitterVisible, value);
        }

        public bool IsRightWorkflowDockToggleVisible
        {
            get => isRightWorkflowDockToggleVisible;
            private set => SetProperty(ref isRightWorkflowDockToggleVisible, value);
        }

        public GridLength CanvasWorkspacePaneGridLength
        {
            get => canvasWorkspacePaneGridLength;
            private set => SetProperty(ref canvasWorkspacePaneGridLength, value);
        }

        public GridLength ImageQueuePaneGridLength
        {
            get => imageQueuePaneGridLength;
            private set => SetProperty(ref imageQueuePaneGridLength, value);
        }

        public GridLength WorkspaceSplitterPaneGridLength
        {
            get => workspaceSplitterPaneGridLength;
            private set => SetProperty(ref workspaceSplitterPaneGridLength, value);
        }

        public double CanvasWorkspacePaneMinWidth
        {
            get => canvasWorkspacePaneMinWidth;
            private set => SetProperty(ref canvasWorkspacePaneMinWidth, value);
        }

        public double ImageQueuePaneMinWidth
        {
            get => imageQueuePaneMinWidth;
            private set => SetProperty(ref imageQueuePaneMinWidth, value);
        }

        public double ImageQueueExpandedPaneWidth => imageQueueExpandedPaneWidth;

        public double RightWorkflowExpandedPaneWidth => rightWorkflowExpandedPaneWidth;

        public string RightWorkflowDockToggleText
        {
            get => rightWorkflowDockToggleText ?? (OpenVisionLab.OpenVisionLanguageService.T("WpfShell.Right.Dock.Collapse"));
            private set => SetProperty(ref rightWorkflowDockToggleText, value ?? string.Empty);
        }

        public string RightWorkflowDockToggleToolTip
        {
            get => rightWorkflowDockToggleToolTip ?? (OpenVisionLab.OpenVisionLanguageService.T("WpfShell.Right.Dock.Collapse.ToolTip"));
            private set => SetProperty(ref rightWorkflowDockToggleToolTip, value ?? string.Empty);
        }

        public bool IsSavedLabelsShortcutActive
        {
            get => isSavedLabelsShortcutActive;
            private set => SetProperty(ref isSavedLabelsShortcutActive, value);
        }

        public bool IsLabelingGuideShortcutActive
        {
            get => isLabelingGuideShortcutActive;
            private set => SetProperty(ref isLabelingGuideShortcutActive, value);
        }

        public bool IsClassCatalogShortcutActive
        {
            get => isClassCatalogShortcutActive;
            private set => SetProperty(ref isClassCatalogShortcutActive, value);
        }

        public string WorkflowStageProgressText
        {
            get => workflowStageProgressText;
            private set => SetProperty(ref workflowStageProgressText, value ?? string.Empty);
        }

        public string WorkflowStageTitleText
        {
            get => workflowStageTitleText;
            private set => SetProperty(ref workflowStageTitleText, value ?? string.Empty);
        }

        public string RightWorkflowViewTitleText
        {
            get => rightWorkflowViewTitleText;
            private set => SetProperty(ref rightWorkflowViewTitleText, value ?? string.Empty);
        }

        public string RightWorkflowViewDetailText
        {
            get => rightWorkflowViewDetailText ?? (OpenVisionLab.OpenVisionLanguageService.T("WpfShell.Right.Detail.Dataset"));
            private set => SetProperty(ref rightWorkflowViewDetailText, value ?? string.Empty);
        }

        public string RightWorkflowRailCurrentViewText
        {
            get => rightWorkflowRailCurrentViewText ?? (OpenVisionLab.OpenVisionLanguageService.T("WpfShell.Right.Rail.Home"));
            private set => SetProperty(ref rightWorkflowRailCurrentViewText, value ?? string.Empty);
        }

        public string WorkflowStageDetailText
        {
            get => workflowStageDetailText;
            private set => SetProperty(ref workflowStageDetailText, value ?? string.Empty);
        }

        public string WorkflowStageNextActionText
        {
            get => workflowStageNextActionText;
            private set => SetProperty(ref workflowStageNextActionText, value ?? string.Empty);
        }

        public bool IsLabelingModeButtonEnabled
        {
            get => isLabelingModeButtonEnabled;
            private set => SetProperty(ref isLabelingModeButtonEnabled, value);
        }

        public bool IsInferenceModeButtonEnabled
        {
            get => isInferenceModeButtonEnabled;
            private set => SetProperty(ref isInferenceModeButtonEnabled, value);
        }

        public bool IsOpenDatasetFolderEnabled
        {
            get => isOpenDatasetFolderEnabled;
            private set => SetProperty(ref isOpenDatasetFolderEnabled, value);
        }

        public string CurrentDatasetName
        {
            get => currentDatasetName ?? (OpenVisionLab.OpenVisionLanguageService.T("WpfShell.Dataset.Unselected"));
            private set => SetProperty(ref currentDatasetName, value ?? string.Empty);
        }

        public string CurrentDatasetPurposeText
        {
            get => currentDatasetPurposeText ?? (OpenVisionLab.OpenVisionLanguageService.T("WpfShell.Dataset.Purpose.Unselected"));
            private set => SetProperty(ref currentDatasetPurposeText, value ?? string.Empty);
        }

        public string CurrentDatasetPathText
        {
            get => currentDatasetPathText ?? (CurrentDatasetStoragePathText + "  /  " + CurrentDatasetImageRootText + "  /  " + CurrentDatasetSourceText);
            private set => SetProperty(ref currentDatasetPathText, value ?? string.Empty);
        }

        public string CurrentDatasetStoragePathText
        {
            get => currentDatasetStoragePathText ?? (string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfShell.Dataset.Storage.Summary"), OpenVisionLab.OpenVisionLanguageService.T("WpfShell.Dataset.Storage.Empty")));
            private set => SetProperty(ref currentDatasetStoragePathText, value ?? string.Empty);
        }

        public string CurrentDatasetImageRootText
        {
            get => currentDatasetImageRootText ?? (string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfShell.Dataset.Images.Summary"), OpenVisionLab.OpenVisionLanguageService.T("WpfShell.Dataset.Images.Empty")));
            private set => SetProperty(ref currentDatasetImageRootText, value ?? string.Empty);
        }

        public string CurrentDatasetSourceText
        {
            get => currentDatasetSourceText ?? (OpenVisionLab.OpenVisionLanguageService.T("WpfShell.Dataset.Source.ClassesMissing") + " / " + OpenVisionLab.OpenVisionLanguageService.T("WpfShell.Dataset.Source.LabelsMissing"));
            private set => SetProperty(ref currentDatasetSourceText, value ?? string.Empty);
        }

        public string CurrentDatasetToolTip
        {
            get => currentDatasetToolTip ?? (OpenVisionLab.OpenVisionLanguageService.T("WpfShell.Dataset.Initial.ToolTip"));
            private set => SetProperty(ref currentDatasetToolTip, value ?? string.Empty);
        }

        public string ModelCenterTrainingStatusText
        {
            get => modelCenterTrainingStatusText ?? (OpenVisionLab.OpenVisionLanguageService.T("WpfShell.ModelCenter.Training.InitialStatus"));
            private set => SetProperty(ref modelCenterTrainingStatusText, value ?? string.Empty);
        }

        public string ModelCenterTrainingDetailText
        {
            get => modelCenterTrainingDetailText ?? (OpenVisionLab.OpenVisionLanguageService.T("WpfShell.ModelCenter.Training.InitialDetail"));
            private set => SetProperty(ref modelCenterTrainingDetailText, value ?? string.Empty);
        }

        public string ModelCenterCurrentModelText
        {
            get => modelCenterCurrentModelText ?? (OpenVisionLab.OpenVisionLanguageService.T("WpfModelCenterDashboard.Current.None"));
            private set => SetProperty(ref modelCenterCurrentModelText, value ?? string.Empty);
        }

        public string ModelCenterCurrentModelTitleText => "\uAC80\uC0AC \uBAA8\uB378";

        public string ModelCenterCurrentModelDetailText
        {
            get => modelCenterCurrentModelDetailText ?? (OpenVisionLab.OpenVisionLanguageService.T("WpfShell.ModelCenter.Current.InitialDetail"));
            private set => SetProperty(ref modelCenterCurrentModelDetailText, value ?? string.Empty);
        }

        public string ModelCenterCandidateModelText
        {
            get => modelCenterCandidateModelText ?? (OpenVisionLab.OpenVisionLanguageService.T("WpfModelCenterDashboard.Candidate.None"));
            private set => SetProperty(ref modelCenterCandidateModelText, value ?? string.Empty);
        }

        public string ModelCenterCandidateModelTitleText => "\uD559\uC2B5 \uACB0\uACFC \uBAA8\uB378";

        public string ModelCenterCandidateModelDetailText
        {
            get => modelCenterCandidateModelDetailText ?? (OpenVisionLab.OpenVisionLanguageService.T("WpfShell.ModelCenter.Trained.InitialDetail"));
            private set => SetProperty(ref modelCenterCandidateModelDetailText, value ?? string.Empty);
        }

        public string ModelCenterAdoptionText
        {
            get => modelCenterAdoptionText ?? (OpenVisionLab.OpenVisionLanguageService.T("WpfModelCenterDashboard.Adoption.Wait"));
            private set => SetProperty(ref modelCenterAdoptionText, value ?? string.Empty);
        }

        public string ModelCenterAdoptionTitleText => "\uC801\uC6A9 \uC0C1\uD0DC";

        public string ModelCenterAdoptionDetailText
        {
            get => modelCenterAdoptionDetailText ?? (OpenVisionLab.OpenVisionLanguageService.T("WpfShell.ModelCenter.Assignment.InitialDetail"));
            private set => SetProperty(ref modelCenterAdoptionDetailText, value ?? string.Empty);
        }

        public string ModelCenterNextActionText
        {
            get => modelCenterNextActionText ?? (string.Format("{0}: {1}", OpenVisionLab.OpenVisionLanguageService.T("WpfLearningWorkflow.TrainingModelLifecycle.Label.NextAction"), OpenVisionLab.OpenVisionLanguageService.T("WpfLearningWorkflow.TrainingModelLifecycle.Next.Initial")));
            private set => SetProperty(ref modelCenterNextActionText, value ?? string.Empty);
        }

        public string ModelCenterNextActionTitleText => "\uB2E4\uC74C \uC791\uC5C5";

        public string ModelCenterNextActionDetailText
        {
            get => modelCenterNextActionDetailText ?? (OpenVisionLab.OpenVisionLanguageService.T("WpfLearningWorkflow.TrainingModelLifecycle.Next.Initial"));
            private set => SetProperty(ref modelCenterNextActionDetailText, value ?? string.Empty);
        }

        public string ModelCenterDecisionTitleText => "\uBAA8\uB378 \uC801\uC6A9 \uD310\uB2E8";

        public string ModelCenterDecisionSummaryText
        {
            get => modelCenterDecisionSummaryText ?? (OpenVisionLab.OpenVisionLanguageService.T("WpfShell.ModelCenter.Decision.InitialSummary"));
            private set => SetProperty(ref modelCenterDecisionSummaryText, value ?? string.Empty);
        }

        public string ModelCenterDecisionEvidenceText
        {
            get => modelCenterDecisionEvidenceText ?? (OpenVisionLab.OpenVisionLanguageService.T("WpfShell.ModelCenter.Decision.InitialEvidence"));
            private set => SetProperty(ref modelCenterDecisionEvidenceText, value ?? string.Empty);
        }

        public string ModelCenterDecisionActionText
        {
            get => modelCenterDecisionActionText ?? (OpenVisionLab.OpenVisionLanguageService.T("WpfShell.ModelCenter.Decision.InitialAction"));
            private set => SetProperty(ref modelCenterDecisionActionText, value ?? string.Empty);
        }

        public string ModelCenterAnomalyEvaluationTitleText => "\uC774\uC0C1\uD0D0\uC9C0 \uD3C9\uAC00";

        public bool IsModelCenterAnomalyEvaluationVisible
        {
            get => isModelCenterAnomalyEvaluationVisible;
            private set => SetProperty(ref isModelCenterAnomalyEvaluationVisible, value);
        }

        public string ModelCenterAnomalyEvaluationRecommendationText
        {
            get => modelCenterAnomalyEvaluationRecommendationText;
            private set => SetProperty(ref modelCenterAnomalyEvaluationRecommendationText, value ?? string.Empty);
        }

        public string ModelCenterAnomalyEvaluationMetricsText
        {
            get => modelCenterAnomalyEvaluationMetricsText;
            private set => SetProperty(ref modelCenterAnomalyEvaluationMetricsText, value ?? string.Empty);
        }

        public string ModelCenterAnomalyEvaluationDetailText
        {
            get => modelCenterAnomalyEvaluationDetailText;
            private set => SetProperty(ref modelCenterAnomalyEvaluationDetailText, value ?? string.Empty);
        }

        public string ModelCenterAnomalyEvaluationActionText
        {
            get => modelCenterAnomalyEvaluationActionText;
            private set => SetProperty(ref modelCenterAnomalyEvaluationActionText, value ?? string.Empty);
        }

        public bool IsModelCenterAnomalyEvaluationPickerVisible
        {
            get => isModelCenterAnomalyEvaluationPickerVisible;
            private set => SetProperty(ref isModelCenterAnomalyEvaluationPickerVisible, value);
        }

        public bool IsModelCenterAnomalyEvaluationPickerEnabled
        {
            get => isModelCenterAnomalyEvaluationPickerEnabled;
            private set => SetProperty(ref isModelCenterAnomalyEvaluationPickerEnabled, value);
        }

        public string ModelRegistryTitleText => "\uBAA8\uB378 \uB808\uC9C0\uC2A4\uD2B8\uB9AC";

        public string ModelRegistrySummaryPrimaryText
        {
            get => modelRegistrySummaryPrimaryText ?? (OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Summary.Empty"));
            private set => SetProperty(ref modelRegistrySummaryPrimaryText, value ?? string.Empty);
        }

        public string ModelRegistrySummarySecondaryText
        {
            get => modelRegistrySummarySecondaryText;
            private set => SetProperty(ref modelRegistrySummarySecondaryText, value ?? string.Empty);
        }

        public string ModelRegistryProfileText
        {
            get => modelRegistryProfileText ?? (OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Profile.Unset"));
            private set => SetProperty(ref modelRegistryProfileText, value ?? string.Empty);
        }

        public string ModelRegistryTrainingRunText
        {
            get => modelRegistryTrainingRunText ?? (OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.TrainingRun.None"));
            private set => SetProperty(ref modelRegistryTrainingRunText, value ?? string.Empty);
        }

        public string ModelRegistryCandidateModelText
        {
            get => modelRegistryCandidateModelText ?? (OpenVisionLab.OpenVisionLanguageService.T("WpfModelCenterDashboard.Candidate.None"));
            private set => SetProperty(ref modelRegistryCandidateModelText, value ?? string.Empty);
        }

        public string ModelRegistryInspectionModelText
        {
            get => modelRegistryInspectionModelText;
            private set => SetProperty(ref modelRegistryInspectionModelText, value ?? string.Empty);
        }

        public string ModelRegistryActionText
        {
            get => modelRegistryActionText ?? (OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Action.Structure"));
            private set => SetProperty(ref modelRegistryActionText, value ?? string.Empty);
        }

        public string ModelRegistryHistoryHeaderText
        {
            get => modelRegistryHistoryHeaderText;
            private set => SetProperty(ref modelRegistryHistoryHeaderText, value ?? string.Empty);
        }

        public string ModelRegistryHistorySummaryText
        {
            get => modelRegistryHistorySummaryText ?? (OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.History.EmptySummary"));
            private set => SetProperty(ref modelRegistryHistorySummaryText, value ?? string.Empty);
        }

        public bool IsModelRegistryHistoryVisible
        {
            get => isModelRegistryHistoryVisible;
            private set => SetProperty(ref isModelRegistryHistoryVisible, value);
        }

        public WpfModelRegistryHistoryItem SelectedModelRegistryHistoryItem
        {
            get => selectedModelRegistryHistoryItem;
            set
            {
                if (SetProperty(ref selectedModelRegistryHistoryItem, value))
                {
                    RefreshSelectedModelHistoryState();
                }
            }
        }

        public bool IsSelectedModelHistoryVisible
        {
            get => isSelectedModelHistoryVisible;
            private set => SetProperty(ref isSelectedModelHistoryVisible, value);
        }

        public string SelectedModelHistoryTitleText
        {
            get => selectedModelHistoryTitleText;
            private set => SetProperty(ref selectedModelHistoryTitleText, value ?? string.Empty);
        }

        public string SelectedModelHistoryDetailText
        {
            get => selectedModelHistoryDetailText;
            private set => SetProperty(ref selectedModelHistoryDetailText, value ?? string.Empty);
        }

        public string SelectedModelHistoryMetricText
        {
            get => selectedModelHistoryMetricText;
            private set => SetProperty(ref selectedModelHistoryMetricText, value ?? string.Empty);
        }

        public string SelectedModelHistoryDecisionText
        {
            get => selectedModelHistoryDecisionText;
            private set => SetProperty(ref selectedModelHistoryDecisionText, value ?? string.Empty);
        }

        public string SelectedModelHistoryComparisonTitleText
        {
            get => selectedModelHistoryComparisonTitleText;
            private set => SetProperty(ref selectedModelHistoryComparisonTitleText, value ?? string.Empty);
        }

        public string SelectedModelHistoryCurrentModelText
        {
            get => selectedModelHistoryCurrentModelText;
            private set => SetProperty(ref selectedModelHistoryCurrentModelText, value ?? string.Empty);
        }

        public string SelectedModelHistorySelectedModelText
        {
            get => selectedModelHistorySelectedModelText;
            private set => SetProperty(ref selectedModelHistorySelectedModelText, value ?? string.Empty);
        }

        public string SelectedModelHistoryComparisonMetricText
        {
            get => selectedModelHistoryComparisonMetricText;
            private set => SetProperty(ref selectedModelHistoryComparisonMetricText, value ?? string.Empty);
        }

        public string SelectedModelHistoryActionText
        {
            get => selectedModelHistoryActionText;
            private set => SetProperty(ref selectedModelHistoryActionText, value ?? string.Empty);
        }

        public string SelectedModelHistoryActionToolTip
        {
            get => selectedModelHistoryActionToolTip;
            private set => SetProperty(ref selectedModelHistoryActionToolTip, value ?? string.Empty);
        }

        public bool IsSelectedModelHistoryActionEnabled
        {
            get => isSelectedModelHistoryActionEnabled;
            private set => SetProperty(ref isSelectedModelHistoryActionEnabled, value);
        }

        public bool IsModelCenterRecoveryVisible
        {
            get => isModelCenterRecoveryVisible;
            private set => SetProperty(ref isModelCenterRecoveryVisible, value);
        }

        public string ModelCenterRecoveryTitleText
        {
            get => modelCenterRecoveryTitleText;
            private set => SetProperty(ref modelCenterRecoveryTitleText, value ?? string.Empty);
        }

        public string ModelCenterRecoveryDetailText
        {
            get => modelCenterRecoveryDetailText;
            private set => SetProperty(ref modelCenterRecoveryDetailText, value ?? string.Empty);
        }

        public string ModelCenterRecoveryActionText
        {
            get => modelCenterRecoveryActionText;
            private set => SetProperty(ref modelCenterRecoveryActionText, value ?? string.Empty);
        }

        public string ModelCenterConfirmModelButtonText
        {
            get => modelCenterConfirmModelButtonText ?? (OpenVisionLab.OpenVisionLanguageService.T("WpfModelCenterDashboard.Confirm.NoModel"));
            private set => SetProperty(ref modelCenterConfirmModelButtonText, value ?? string.Empty);
        }

        public string ModelCenterConfirmModelButtonToolTip
        {
            get => modelCenterConfirmModelButtonToolTip ?? (OpenVisionLab.OpenVisionLanguageService.T("WpfModelCenterDashboard.Confirm.NoResultTooltip"));
            private set => SetProperty(ref modelCenterConfirmModelButtonToolTip, value ?? string.Empty);
        }

        public bool IsModelCenterConfirmModelEnabled
        {
            get => isModelCenterConfirmModelEnabled;
            private set => SetProperty(ref isModelCenterConfirmModelEnabled, value);
        }

        public string ModelCenterInspectCurrentImageButtonText
        {
            get => modelCenterInspectCurrentImageButtonText ?? (OpenVisionLab.OpenVisionLanguageService.T("WpfShell.ModelCenter.InferCurrentImage.Text"));
            private set => SetProperty(ref modelCenterInspectCurrentImageButtonText, value ?? string.Empty);
        }

        public string ModelCenterInspectCurrentImageButtonToolTip
        {
            get => modelCenterInspectCurrentImageButtonToolTip ?? (OpenVisionLab.OpenVisionLanguageService.T("WpfShell.ModelCenter.InferCurrentImage.ToolTip"));
            private set => SetProperty(ref modelCenterInspectCurrentImageButtonToolTip, value ?? string.Empty);
        }

        public bool IsModelCenterInspectCurrentImageEnabled
        {
            get => isModelCenterInspectCurrentImageEnabled;
            private set => SetProperty(ref isModelCenterInspectCurrentImageEnabled, value);
        }

        public string ModelCenterReviewCandidateButtonText
        {
            get => modelCenterReviewCandidateButtonText ?? (OpenVisionLab.OpenVisionLanguageService.T("WpfShell.ModelCenter.CheckTrainedModel.Text"));
            private set => SetProperty(ref modelCenterReviewCandidateButtonText, value ?? string.Empty);
        }

        public string ModelCenterReviewCandidateButtonToolTip
        {
            get => modelCenterReviewCandidateButtonToolTip ?? (OpenVisionLab.OpenVisionLanguageService.T("WpfModelCenterDashboard.Review.NoResultTooltip"));
            private set => SetProperty(ref modelCenterReviewCandidateButtonToolTip, value ?? string.Empty);
        }

        public bool IsModelCenterReviewCandidateEnabled
        {
            get => isModelCenterReviewCandidateEnabled;
            private set => SetProperty(ref isModelCenterReviewCandidateEnabled, value);
        }

        public string ModelCenterRuntimeActionText
        {
            get => modelCenterRuntimeActionText;
            private set => SetProperty(ref modelCenterRuntimeActionText, value ?? string.Empty);
        }

        public string ModelCenterActionStateText
        {
            get => modelCenterActionStateText ?? (OpenVisionLab.OpenVisionLanguageService.T("WpfShell.ModelCenter.InitialActionState"));
            private set => SetProperty(ref modelCenterActionStateText, value ?? string.Empty);
        }

        public ICommand LoadedCommand
        {
            get => loadedCommand;
            private set => SetProperty(ref loadedCommand, value);
        }

        public ICommand ClosedCommand
        {
            get => closedCommand;
            private set => SetProperty(ref closedCommand, value);
        }

        public int RightWorkflowDockColumn
        {
            get => rightWorkflowDockColumn;
            private set => SetProperty(ref rightWorkflowDockColumn, value);
        }

        public int ImageQueueColumn
        {
            get => imageQueueColumn;
            private set => SetProperty(ref imageQueueColumn, value);
        }

        public double WorkflowContextHeaderHeight
        {
            get => workflowContextHeaderHeight;
            private set => SetProperty(ref workflowContextHeaderHeight, value);
        }

        public double WorkflowStageRailHeight
        {
            get => workflowStageRailHeight;
            private set => SetProperty(ref workflowStageRailHeight, value);
        }

        /// <summary>
        /// Mirrors the workspace columns so the image queue stays left of the canvas
        /// and the workflow dock stays right, while panel content remains left-to-right.
        /// </summary>
        public FlowDirection LabelingWorkspaceFlowDirection
        {
            get => labelingWorkspaceFlowDirection;
            private set => SetProperty(ref labelingWorkspaceFlowDirection, value);
        }

        public ICommand DeactivatedCommand
        {
            get => deactivatedCommand;
            private set => SetProperty(ref deactivatedCommand, value);
        }

        public ICommand PreviewKeyDownCommand
        {
            get => previewKeyDownCommand;
            private set => SetProperty(ref previewKeyDownCommand, value);
        }

        public ICommand PreviewKeyUpCommand
        {
            get => previewKeyUpCommand;
            private set => SetProperty(ref previewKeyUpCommand, value);
        }

        public ICommand ToggleThemeCommand
        {
            get => toggleThemeCommand;
            private set => SetProperty(ref toggleThemeCommand, value);
        }

        public ICommand LoadSampleCommand
        {
            get => loadSampleCommand;
            private set => SetProperty(ref loadSampleCommand, value);
        }

        public ICommand AddSampleRoiCommand
        {
            get => addSampleRoiCommand;
            private set => SetProperty(ref addSampleRoiCommand, value);
        }

        public ICommand SaveAnnotationsCommand
        {
            get => saveAnnotationsCommand;
            private set => SetProperty(ref saveAnnotationsCommand, value);
        }

        public ICommand LabelingModeCommand
        {
            get => labelingModeCommand;
            private set => SetProperty(ref labelingModeCommand, value);
        }

        public ICommand InferenceModeCommand
        {
            get => inferenceModeCommand;
            private set => SetProperty(ref inferenceModeCommand, value);
        }

        public ICommand DatasetHomeCommand
        {
            get => datasetHomeCommand;
            private set => SetProperty(ref datasetHomeCommand, value);
        }

        public ICommand LabelingWorkbenchCommand
        {
            get => labelingWorkbenchCommand;
            private set => SetProperty(ref labelingWorkbenchCommand, value);
        }

        public ICommand InferenceReviewCommand
        {
            get => inferenceReviewCommand;
            private set => SetProperty(ref inferenceReviewCommand, value);
        }

        public ICommand TrainingModelCenterCommand
        {
            get => trainingModelCenterCommand;
            private set => SetProperty(ref trainingModelCenterCommand, value);
        }

        public ICommand ModelComparisonCommand
        {
            get => modelComparisonCommand;
            private set => SetProperty(ref modelComparisonCommand, value);
        }

        public ICommand ReviewCandidateModelCommand
        {
            get => reviewCandidateModelCommand;
            private set => SetProperty(ref reviewCandidateModelCommand, value);
        }

        public ICommand PromoteSelectedModelHistoryCommand
        {
            get => promoteSelectedModelHistoryCommand;
            private set => SetProperty(ref promoteSelectedModelHistoryCommand, value);
        }

        public ICommand RunAnomalyEvaluationCommand
        {
            get => runAnomalyEvaluationCommand;
            private set => SetProperty(ref runAnomalyEvaluationCommand, value);
        }

        public ICommand LoadAnomalyEvaluationSummaryCommand
        {
            get => loadAnomalyEvaluationSummaryCommand;
            private set => SetProperty(ref loadAnomalyEvaluationSummaryCommand, value);
        }

        public ICommand OpenModelBenchmarkCommand
        {
            get => openModelBenchmarkCommand;
            private set => SetProperty(ref openModelBenchmarkCommand, value);
        }

        public ICommand OpenDatasetHealthCommand
        {
            get => openDatasetHealthCommand;
            private set => SetProperty(ref openDatasetHealthCommand, value);
        }

        public ICommand OpenDatasetInterchangeCommand
        {
            get => openDatasetInterchangeCommand;
            private set => SetProperty(ref openDatasetInterchangeCommand, value);
        }

        public ICommand ShowSavedLabelsViewCommand
        {
            get => showSavedLabelsViewCommand;
            private set => SetProperty(ref showSavedLabelsViewCommand, value);
        }

        public ICommand ShowLabelingGuideViewCommand
        {
            get => showLabelingGuideViewCommand;
            private set => SetProperty(ref showLabelingGuideViewCommand, value);
        }

        public ICommand ShowClassCatalogViewCommand
        {
            get => showClassCatalogViewCommand;
            private set => SetProperty(ref showClassCatalogViewCommand, value);
        }

        public ICommand CloseClassCatalogDialogCommand => closeClassCatalogDialogCommand ??= new RelayCommand(() => IsClassCatalogDialogOpen = false);

        public ICommand ToggleRightWorkflowDockCommand
        {
            get
            {
                if (toggleRightWorkflowDockCommand == null)
                {
                    toggleRightWorkflowDockCommand = new RelayCommand(ToggleRightWorkflowDock);
                }

                return toggleRightWorkflowDockCommand;
            }
        }

        public ICommand ResetWorkspaceLayoutCommand
        {
            get => resetWorkspaceLayoutCommand;
            private set => SetProperty(ref resetWorkspaceLayoutCommand, value);
        }

        public ICommand WorkflowPaneResizeCommand
        {
            get => workflowPaneResizeCommand;
            private set => SetProperty(ref workflowPaneResizeCommand, value);
        }

        public ICommand ImageQueuePaneResizeCommand
        {
            get => imageQueuePaneResizeCommand;
            private set => SetProperty(ref imageQueuePaneResizeCommand, value);
        }

        public ICommand WorkspaceViewportSizeChangedCommand
        {
            get => workspaceViewportSizeChangedCommand;
            private set => SetProperty(ref workspaceViewportSizeChangedCommand, value);
        }

        public string WorkspaceLayoutLastSaveError
        {
            get => workspaceLayoutLastSaveError;
            private set => SetProperty(ref workspaceLayoutLastSaveError, value);
        }

        public ICommand CheckYoloCommand
        {
            get => checkYoloCommand;
            private set => SetProperty(ref checkYoloCommand, value);
        }

        public ICommand DetectCurrentImageCommand
        {
            get => detectCurrentImageCommand;
            private set => SetProperty(ref detectCurrentImageCommand, value);
        }

        public ICommand RunInferenceTargetCommand
        {
            get => runInferenceTargetCommand;
            private set => SetProperty(ref runInferenceTargetCommand, value);
        }

        public ICommand MarkInferenceResultSeenCommand { get => markInferenceResultSeenCommand; private set => SetProperty(ref markInferenceResultSeenCommand, value); }
        public ICommand SendInferenceResultsToLabelingCommand { get => sendInferenceResultsToLabelingCommand; private set => SetProperty(ref sendInferenceResultsToLabelingCommand, value); }
        public ICommand ExportInferenceResultsCommand { get => exportInferenceResultsCommand; private set => SetProperty(ref exportInferenceResultsCommand, value); }
        public ICommand OpenInferenceDecisionRuleCommand { get => openInferenceDecisionRuleCommand; private set => SetProperty(ref openInferenceDecisionRuleCommand, value); }
        public ICommand MarkInferenceResultSeenAndNextCommand { get => markInferenceResultSeenAndNextCommand; private set => SetProperty(ref markInferenceResultSeenAndNextCommand, value); }
        public ICommand ShowInferenceResultsCommand { get => showInferenceResultsCommand; private set => SetProperty(ref showInferenceResultsCommand, value); }

        public ICommand ToggleLabelingAssistPanelCommand
        {
            get => toggleLabelingAssistPanelCommand;
            private set => SetProperty(ref toggleLabelingAssistPanelCommand, value);
        }

        public ICommand ApplySelectedInferenceModelCommand
        {
            get => applySelectedInferenceModelCommand;
            private set => SetProperty(ref applySelectedInferenceModelCommand, value);
        }

        public ICommand RunTemplateMatchingCommand
        {
            get => runTemplateMatchingCommand;
            private set => SetProperty(ref runTemplateMatchingCommand, value);
        }

        public ICommand ChangeDatasetCommand
        {
            get => changeDatasetCommand;
            private set => SetProperty(ref changeDatasetCommand, value);
        }

        public ICommand CloseRecipeCommand
        {
            get => closeRecipeCommand;
            private set => SetProperty(ref closeRecipeCommand, value);
        }

        public ICommand OpenDatasetFolderCommand
        {
            get => openDatasetFolderCommand;
            private set => SetProperty(ref openDatasetFolderCommand, value);
        }

        public ICommand ChangeImageFolderCommand
        {
            get => changeImageFolderCommand;
            private set => SetProperty(ref changeImageFolderCommand, value);
        }

        public ICommand CompleteLabelingCommand
        {
            get => completeLabelingCommand;
            private set => SetProperty(ref completeLabelingCommand, value);
        }

        public ICommand CancelLabelingCommand
        {
            get => cancelLabelingCommand;
            private set => SetProperty(ref cancelLabelingCommand, value);
        }

        public void ConfigureCommands(
            Action toggleTheme,
            Action loadSample,
            Action addSampleRoi,
            Action saveAnnotations,
            Action labelingMode,
            Action inferenceMode,
            Action checkYolo,
            Action detectCurrentImage,
            Action runTemplateMatching,
            Action changeDataset,
            Action openDatasetFolder,
            Action changeImageFolder,
            Action loaded,
            Action closed,
            Action<KeyInputCommandArgs> previewKeyDown,
            Action datasetHome = null,
            Action labelingWorkbench = null,
            Action inferenceReview = null,
            Action trainingModelCenter = null,
            Action reviewCandidateModel = null,
            Action showSavedLabelsView = null,
            Action showLabelingGuideView = null,
            Action showClassCatalogView = null,
            Action promoteSelectedModelHistory = null,
            Action runAnomalyEvaluation = null,
            Action loadAnomalyEvaluationSummary = null,
            Action resetWorkspaceLayout = null,
            Action openModelBenchmark = null,
            Action openDatasetHealth = null,
            Action openDatasetInterchange = null,
            Action completeLabeling = null,
            Action cancelLabeling = null,
            Action closeRecipe = null,
            Func<bool> canStartLabeling = null,
            Action modelComparison = null,
            Action<string> openDatasetInterchangeDirection = null)
        {
            // Shell lifecycle and toolbar commands are injected; key commands use a DTO so this ViewModel avoids WPF EventArgs.
            ToggleThemeCommand = new RelayCommand(toggleTheme ?? NoOpCommand);
            LoadSampleCommand = new RelayCommand(loadSample ?? NoOpCommand);
            AddSampleRoiCommand = new RelayCommand(addSampleRoi ?? NoOpCommand);
            SaveAnnotationsCommand = new RelayCommand(saveAnnotations ?? NoOpCommand);
            LabelingModeCommand = new RelayCommand(labelingMode ?? NoOpCommand);
            InferenceModeCommand = new RelayCommand(inferenceMode ?? NoOpCommand);
            DatasetHomeCommand = new RelayCommand(datasetHome ?? NoOpCommand);
            LabelingWorkbenchCommand = new RelayCommand(labelingWorkbench ?? labelingMode ?? NoOpCommand, canStartLabeling);
            InferenceReviewCommand = new RelayCommand(inferenceReview ?? inferenceMode ?? NoOpCommand);
            TrainingModelCenterCommand = new RelayCommand(trainingModelCenter ?? NoOpCommand);
            ModelComparisonCommand = new RelayCommand(modelComparison ?? NoOpCommand);
            ReviewCandidateModelCommand = new RelayCommand(reviewCandidateModel ?? inferenceReview ?? inferenceMode ?? NoOpCommand);
            PromoteSelectedModelHistoryCommand = new RelayCommand(promoteSelectedModelHistory ?? NoOpCommand);
            RunAnomalyEvaluationCommand = new RelayCommand(runAnomalyEvaluation ?? NoOpCommand);
            LoadAnomalyEvaluationSummaryCommand = new RelayCommand(loadAnomalyEvaluationSummary ?? NoOpCommand);
            OpenModelBenchmarkCommand = new RelayCommand(openModelBenchmark ?? NoOpCommand);
            OpenDatasetHealthCommand = new RelayCommand(openDatasetHealth ?? NoOpCommand);
            OpenDatasetInterchangeCommand = openDatasetInterchangeDirection == null
                ? new RelayCommand(openDatasetInterchange ?? NoOpCommand)
                : new RelayCommand<string>(openDatasetInterchangeDirection);
            ShowSavedLabelsViewCommand = new RelayCommand(showSavedLabelsView ?? labelingMode ?? NoOpCommand);
            ShowLabelingGuideViewCommand = new RelayCommand(showLabelingGuideView ?? labelingMode ?? NoOpCommand);
            ShowClassCatalogViewCommand = new RelayCommand<object>(presentation =>
            {
                bool quickAdd = string.Equals(presentation as string, "Add", StringComparison.Ordinal);
                if (quickAdd && (IsRecipeLibraryVisible || IsClassCatalogDialogOpen && IsClassAddDialogOpen)) return;
                IsClassAddDialogOpen = quickAdd;
                (showClassCatalogView ?? labelingMode ?? NoOpCommand)();
                if (!IsClassCatalogDialogOpen) IsClassAddDialogOpen = false;
            });
            CheckYoloCommand = new RelayCommand(checkYolo ?? NoOpCommand);
            DetectCurrentImageCommand = new RelayCommand(detectCurrentImage ?? NoOpCommand);
            RunTemplateMatchingCommand = new RelayCommand(runTemplateMatching ?? NoOpCommand);
            ChangeDatasetCommand = new RelayCommand(changeDataset ?? NoOpCommand);
            CloseRecipeCommand = new RelayCommand(closeRecipe ?? NoOpCommand);
            OpenDatasetFolderCommand = new RelayCommand(openDatasetFolder ?? NoOpCommand);
            ChangeImageFolderCommand = new RelayCommand(changeImageFolder ?? NoOpCommand);
            CompleteLabelingCommand = new RelayCommand(completeLabeling ?? NoOpCommand);
            CancelLabelingCommand = new RelayCommand(cancelLabeling ?? NoOpCommand);
            ResetWorkspaceLayoutCommand = new RelayCommand(resetWorkspaceLayout ?? ExecuteResetWorkspaceLayoutCommand);
            WorkflowPaneResizeCommand = new RelayCommand<double>(ExecuteWorkflowPaneResizeCommand);
            ImageQueuePaneResizeCommand = new RelayCommand<double>(ExecuteImageQueuePaneResizeCommand);
            WorkspaceViewportSizeChangedCommand = new RelayCommand<double>(ExecuteWorkspaceViewportSizeChangedCommand);
            LoadedCommand = new RelayCommand(loaded ?? NoOpCommand);
            ClosedCommand = new RelayCommand(closed ?? NoOpCommand);
            PreviewKeyDownCommand = new RelayCommand<KeyInputCommandArgs>(previewKeyDown ?? NoOpKeyCommand);
        }

        internal void ConfigureInferenceReviewActions(
            Action runCurrentTarget,
            Action runVisibleTarget,
            Func<bool> canRunCurrentTarget,
            Func<bool> canRunVisibleTarget,
            Func<bool> isInferenceRunning,
            Action onPurposeChanged,
            Action onModelSelectionChanged = null,
            Action runUnexecutedTarget = null,
            Func<bool> canRunUnexecutedTarget = null)
        {
            runCurrentInferenceTarget = runCurrentTarget;
            runVisibleInferenceTarget = runVisibleTarget;
            canRunCurrentInferenceTarget = canRunCurrentTarget;
            canRunVisibleInferenceTarget = canRunVisibleTarget;
            runUnexecutedInferenceTarget = runUnexecutedTarget;
            canRunUnexecutedInferenceTarget = canRunUnexecutedTarget;
            inferenceRunStateProvider = isInferenceRunning;
            inferencePurposeChanged = onPurposeChanged;
            inferenceModelSelectionChanged = onModelSelectionChanged;
            ToggleLabelingAssistPanelCommand = new RelayCommand(
                ExecuteToggleLabelingAssistPanelCommand,
                () => CanToggleLabelingAssistPanel);
            RunInferenceTargetCommand = new RelayCommand(
                ExecuteRunInferenceTargetCommand,
                CanRunInferenceTargetCommand);
            ApplySelectedInferenceModelCommand = new RelayCommand(
                ExecuteApplySelectedInferenceModelCommand,
                () => CanApplySelectedInferenceModel);
            RefreshInferenceReviewState();
        }

        internal void ConfigureInferenceResultReview(
            Func<InferenceResultReviewContext> contextProvider,
            Func<string, bool> openImage,
            Action<YoloWorkerSmokeTestResult> restoreResult,
            Action onResultRoleChanged,
            Action resetResultFilters = null,
            Action<YoloWorkerSmokeCandidate> selectPrediction = null,
            Func<YoloWorkerSmokeCandidate> selectedPredictionProvider = null,
            Func<IReadOnlyList<WpfClassCatalogListItem>> classCatalogProvider = null,
            Func<InferenceResultReviewEntry, double, bool> sendResultToLabeling = null,
            Func<string, string> exportResults = null,
            Func<Func<string>, string> exportDeferredResults = null)
        {
            inferenceClassCatalogProvider = classCatalogProvider;
            sendInferenceResultToLabeling = sendResultToLabeling;
            SendInferenceResultsToLabelingCommand = new RelayCommand(ExecuteSendInferenceResultsToLabeling, () => CanSendInferenceResultsToLabeling);
            exportInferenceResults = exportResults;
            exportDeferredInferenceResults = exportDeferredResults;
            ExportInferenceResultsCommand = new RelayCommand(ExecuteExportInferenceResults, () => CanExportInferenceResults);
            selectInferencePrediction = selectPrediction;
            selectedInferencePredictionProvider = selectedPredictionProvider;
            resetInferenceResultFilters = resetResultFilters;
            inferenceReviewContextProvider = contextProvider;
            openInferenceResultImage = openImage;
            restoreInferenceResult = restoreResult;
            inferenceResultRoleChanged = onResultRoleChanged;
            MarkInferenceResultSeenCommand = new RelayCommand(ExecuteMarkInferenceResultSeen, () => CanMarkInferenceResultSeen);
            MarkInferenceResultSeenAndNextCommand = new RelayCommand(ExecuteMarkInferenceResultSeenAndNext, () => CanMoveToNextInferenceResult);
            ShowInferenceResultsCommand = new RelayCommand(ExecuteShowInferenceResults, () => CanShowInferenceResults);
            RefreshInferenceResultPresentation();
        }

        internal void ConfigureInferenceDecisionReview(Func<InferenceResultReviewEntry, ModelDecisionResult> evaluator, Action<InferenceResultReviewEntry> openRule, Func<(Func<InferenceResultReviewEntry, ModelDecisionResult> Evaluate, Func<bool> Validate)> factory = null, Func<InferenceResultReviewEntry, (double? Threshold, bool? SavedVerdict)> anomalyEvidence = null)
        {
            inferenceResultReview.ConfigureDecisionEvaluation(evaluator, factory);
            inferenceAnomalyEvidenceProvider = anomalyEvidence;
            inferenceDecisionRefreshPending = true;
            openInferenceDecisionRule = openRule;
            OpenInferenceDecisionRuleCommand = new RelayCommand(() => openInferenceDecisionRule?.Invoke(DisplayedInferenceResult), () => openInferenceDecisionRule != null && HasDisplayedInferenceResult && !inferenceReviewContext.IsBusy && !inferenceReviewContext.IsClosing);
            OnPropertyChanged(nameof(InferencePartDecisionText));
        }

        public void RecordInferenceResult(YoloWorkerSmokeTestResult result)
        {
            InferenceResultReviewContext context = inferenceReviewContextProvider?.Invoke() ?? inferenceReviewContext;
            inferenceResultReview.SetScope(context.RecipeIdentity);
            inferenceResultReview.Record(result);
            inferenceDecisionRefreshPending = true;
            inferenceNavigationMessage = string.Empty;
            RefreshInferenceResultPresentation();
        }

        internal void InvalidateInferenceDecisionProjections()
        {
            inferenceResultReview.InvalidateDecisionProjections();
            inferenceDecisionRefreshPending = true;
            RefreshInferenceResultPresentation();
        }

        internal void RefreshInferenceResultPresentation()
        {
            if (refreshingInferenceResultPresentation) return;
            refreshingInferenceResultPresentation = true;
            try
            {
                InferenceResultReviewContext next = inferenceReviewContextProvider?.Invoke() ?? inferenceReviewContext;
                bool imageChanged = !string.Equals(next.ImagePath, inferenceReviewContext.ImagePath, StringComparison.OrdinalIgnoreCase);
                bool enteredResults = IsInferenceControlsVisible && !inferenceResultsWereVisible;
                bool enteredWorkspace = IsInferenceStageActive && !inferenceWorkspaceWasActive;
                inferenceResultReview.SetScope(next.RecipeIdentity);
                bool modelChanged = inferenceResultReview.SelectModel(next.NextSettings);
                inferenceReviewContext = next;
                inferenceResultsWereVisible = IsInferenceControlsVisible;
                inferenceWorkspaceWasActive = IsInferenceStageActive;
                if (!next.IsBusy && !next.IsClosing && (inferenceDecisionRefreshPending || imageChanged || enteredResults || enteredWorkspace || modelChanged))
                {
                    inferenceResultReview.RefreshDecisionProjections(inferenceDecisionRefreshPending ? null : next.ImagePath);
                    inferenceAnomalyEvidence = IsAnomalyImageReviewMode ? inferenceAnomalyEvidenceProvider?.Invoke(DisplayedInferenceResult) ?? default : default;
                    inferenceDecisionRefreshPending = false;
                }
                if (imageChanged || enteredResults || enteredWorkspace || modelChanged) inferenceResultRestorePending = true;
                if (IsLabelingStageActive && next.LabelingTransferEntry != null) inferenceResultRestorePending = false;
                if (imageChanged || modelChanged) inferenceNavigationMessage = string.Empty;
                if (inferenceResultRestorePending && IsInferenceControlsVisible && !next.IsBusy && !next.IsClosing && !next.IsComparisonEvidence && !next.HasUnsavedLabels && !next.HasPendingCandidates && !(IsLabelingStageActive && next.LabelingTransferEntry != null))
                {
                    YoloWorkerSmokeTestResult result = DisplayedInferenceResult?.Result;
                    inferenceResultRestorePending = false;
                    restoreInferenceResult?.Invoke(result);
                    inferenceReviewContext = inferenceReviewContextProvider?.Invoke() ?? next;
                }
                if (!next.IsBusy) inferenceSummary = inferenceResultReview.BuildSummary(next.AllImages ?? next.VisibleImages, next.DisplayConfidence);
                OnPropertyChanged(nameof(InferencePartDecisionText));
                foreach (string property in new[] { nameof(IsAnomalyInference), nameof(InferenceDecisionTitleText), nameof(InferenceDecisionValueText), nameof(InferenceDecisionIsNg), nameof(InferenceDecisionLabelText), nameof(InferenceAnomalyThresholdText), nameof(InferenceAnomalyScoreText), nameof(InferenceSavedVerdictText), nameof(InferenceVisiblePredictionCountText), nameof(InferencePanelTitleText) }) OnPropertyChanged(property);
                if (OpenInferenceDecisionRuleCommand is RelayCommand decisionCommand) decisionCommand.RaiseCanExecuteChanged();
                ModelDecisionResult displayedDecision = inferenceResultReview.GetDecisionProjection(DisplayedInferenceResult);
                var rows = InferencePredictions.Select(candidate =>
                {
                    WpfCandidateReviewListItem row = BuildInferenceReviewItem(candidate.ClassName, candidate.ClassId,
                        candidate.ImageLevel ? candidate.AnomalyScore?.ToString("0.00", CultureInfo.CurrentCulture) ?? "—" : candidate.Confidence.ToString("0.00", CultureInfo.CurrentCulture), candidate);
                    row = inferencePredictionListItems.FirstOrDefault(previous => ReferenceEquals(previous.Payload, candidate) && previous.Title == row.Title
                        && previous.SecondaryText == row.SecondaryText && ReferenceEquals(previous.StateBrush, row.StateBrush)) ?? row;
                    row.IsInferenceDecisionEvidence = !candidate.ImageLevel && displayedDecision.IsNg.HasValue && displayedDecision.EvidenceIndices.Contains(candidate.Index);
                    return row;
                }).ToArray();
                if (!rows.SequenceEqual(inferencePredictionListItems)) inferencePredictionListItems = rows;
                foreach (string property in new[] { nameof(CanSelectInferencePrediction), nameof(CanSendInferenceResultsToLabeling), nameof(IsInferenceSummaryReady), nameof(InferenceClassSummaryItems), nameof(InferenceClassSummaryRows), nameof(InferenceSummaryText), nameof(InferenceResultTitleText), nameof(InferencePredictions), nameof(InferencePredictionListItems), nameof(HasVisibleInferencePredictions), nameof(InferenceResultEmptyText), nameof(SelectedInferencePrediction) }) OnPropertyChanged(property);
                foreach (string property in new[] { nameof(DisplayedInferenceResult), nameof(HasDisplayedInferenceResult), nameof(IsInferenceNotRunHintVisible), nameof(IsInferenceResultReadOnly), nameof(CanReviewInferenceResult), nameof(CanMarkInferenceResultSeen), nameof(CanMoveToNextInferenceResult), nameof(CanShowInferenceResults), nameof(IsInferenceResultOutsideFilter), nameof(IsInferenceResultSettingsChanged), nameof(InferenceResultSourceText), nameof(InferenceResultCountsText), nameof(InferenceResultWarningText), nameof(InferenceResultReviewText), nameof(InferenceResultOutsideFilterText) }) OnPropertyChanged(property);
                OnPropertyChanged(nameof(CanExportInferenceResults));
                foreach (ICommand command in new[] { MarkInferenceResultSeenCommand, MarkInferenceResultSeenAndNextCommand, ShowInferenceResultsCommand, SendInferenceResultsToLabelingCommand, ExportInferenceResultsCommand }) if (command is RelayCommand relay) relay.RaiseCanExecuteChanged();
                bool readOnly = IsInferenceResultReadOnly;
                if (lastInferenceResultReadOnly != readOnly)
                {
                    lastInferenceResultReadOnly = readOnly;
                    inferenceResultRoleChanged?.Invoke();
                }
                RefreshInferenceTargetCommandState();
            }
            finally { refreshingInferenceResultPresentation = false; }
        }

        private WpfCandidateReviewListItem BuildInferenceReviewItem(string className, int? classId, string value, object payload)
        {
            IReadOnlyList<WpfClassCatalogListItem> classes = inferenceClassCatalogProvider?.Invoke() ?? Array.Empty<WpfClassCatalogListItem>();
            WpfClassCatalogListItem item = classId.HasValue ? classes.FirstOrDefault(candidate => candidate.CanonicalIndex == classId.Value) : null;
            item ??= classes.FirstOrDefault(candidate => string.Equals(candidate.Text, className, StringComparison.OrdinalIgnoreCase));
            return new WpfCandidateReviewListItem(string.IsNullOrWhiteSpace(className) ? "Defect" : className, value, string.Empty, payload, MahApps.Metro.IconPacks.PackIconMaterialKind.CrosshairsGps, item?.DrawBrush);
        }

        private void ExecuteExportInferenceResults()
        {
            RefreshInferenceResultPresentation();
            if (!CanExportInferenceResults) return;
            exportingInferenceResults = true;
            OnPropertyChanged(nameof(CanExportInferenceResults));
            if (ExportInferenceResultsCommand is RelayCommand command) command.RaiseCanExecuteChanged();
            try
            {
                InferenceResultReviewContext captured = inferenceReviewContext;
                string[] paths = (captured.AllImages ?? captured.VisibleImages).ToArray();
                var model = (captured.NextSettings?.ModelEngine, captured.NextSettings?.WeightsPath, captured.NextSettings?.ProjectRootPath);
                string BuildCsv()
                {
                    InferenceResultReviewContext current = inferenceReviewContextProvider?.Invoke() ?? inferenceReviewContext;
                    if (current.IsClosing || current.IsBusy || !string.Equals(current.RecipeIdentity, captured.RecipeIdentity, StringComparison.OrdinalIgnoreCase)
                        || !string.Equals(inferenceResultReview.ScopeIdentity, captured.RecipeIdentity, StringComparison.OrdinalIgnoreCase)
                        || model != (current.NextSettings?.ModelEngine, current.NextSettings?.WeightsPath, current.NextSettings?.ProjectRootPath))
                        throw new InvalidDataException(OpenVisionLanguageService.T("WpfComparisonWorkspace.RuleStale"));
                    return inferenceResultReview.BuildCsv(paths, captured.DisplayConfidence);
                }
                string path = exportDeferredInferenceResults != null ? exportDeferredInferenceResults(BuildCsv) : exportInferenceResults(BuildCsv());
                inferenceNavigationMessage = string.IsNullOrWhiteSpace(path) ? OpenVisionLanguageService.T("WpfInference.Export.Canceled")
                    : string.Format(CultureInfo.CurrentCulture, OpenVisionLanguageService.T("WpfInference.Export.Done"), path);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                inferenceNavigationMessage = string.Format(CultureInfo.CurrentCulture, OpenVisionLanguageService.T("WpfInference.Export.Failed"), ex.Message);
            }
            finally { exportingInferenceResults = false; RefreshInferenceResultPresentation(); }
        }

        private void ExecuteSendInferenceResultsToLabeling()
        {
            RefreshInferenceResultPresentation();
            if (!CanSendInferenceResultsToLabeling) return;
            InferenceResultReviewEntry entry = DisplayedInferenceResult;
            double confidence = inferenceReviewContext.DisplayConfidence;
            int count = InferencePredictions.Count;
            sendingInferenceResultToLabeling = true;
            try
            {
                inferenceNavigationMessage = sendInferenceResultToLabeling?.Invoke(entry, confidence) == true
                    ? string.Format(CultureInfo.CurrentCulture, OpenVisionLanguageService.T("WpfInference.Transfer.Done"), count)
                    : OpenVisionLanguageService.T("WpfInference.Transfer.Blocked");
            }
            finally { sendingInferenceResultToLabeling = false; RefreshInferenceResultPresentation(); }
        }

        private void ExecuteMarkInferenceResultSeen()
        {
            RefreshInferenceResultPresentation();
            if (!CanMarkInferenceResultSeen) return;
            inferenceResultReview.MarkSeen(inferenceReviewContext.ImagePath);
            inferenceNavigationMessage = string.Empty;
            RefreshInferenceResultPresentation();
        }

        private void ExecuteMarkInferenceResultSeenAndNext()
        {
            RefreshInferenceResultPresentation();
            if (!CanMoveToNextInferenceResult) return;
            inferenceResultReview.MarkSeen(inferenceReviewContext.ImagePath);
            string next = inferenceResultReview.FindNextUnseen(inferenceReviewContext.VisibleImages, inferenceReviewContext.ImagePath);
            if (!string.IsNullOrEmpty(next))
            {
                if (openInferenceResultImage?.Invoke(next) != true) inferenceNavigationMessage = OpenVisionLanguageService.T("WpfInference.Seen.OpenFailed");
            }
            else inferenceNavigationMessage = OpenVisionLanguageService.T("WpfInference.Seen.NoNext");
            RefreshInferenceResultPresentation();
        }

        private void ExecuteShowInferenceResults()
        {
            RefreshInferenceResultPresentation();
            if (!CanShowInferenceResults) return;
            resetInferenceResultFilters?.Invoke();
            string path = inferenceResultReview.FindFirstResultNeedingReview();
            if (!string.IsNullOrEmpty(path) && openInferenceResultImage?.Invoke(path) != true) inferenceNavigationMessage = OpenVisionLanguageService.T("WpfInference.Seen.OpenFailed");
            RefreshInferenceResultPresentation();
        }

        internal void RefreshInferenceReviewState()
        {
            RefreshInferenceResultPresentation();
            OnPropertyChanged(nameof(CanChangeInferencePurpose));
            OnPropertyChanged(nameof(CanToggleLabelingAssistPanel));
            RefreshInferenceTargetCommandState();
            RefreshLabelingAssistPanelCommandState();
        }

        private void ExecuteToggleLabelingAssistPanelCommand()
        {
            if (CanToggleLabelingAssistPanel)
            {
                IsLabelingAssistPanelExpanded = !IsLabelingAssistPanelExpanded;
            }
        }

        private void RefreshLabelingAssistPanelCommandState()
        {
            if (ToggleLabelingAssistPanelCommand is RelayCommand command)
            {
                command.RaiseCanExecuteChanged();
            }
        }

        private void ExecuteRunInferenceTargetCommand()
        {
            if (!CanRunInferenceTargetCommand())
            {
                return;
            }

            (inferenceTargetIndex == 1 ? runUnexecutedInferenceTarget : inferenceTargetIndex == 2 ? runVisibleInferenceTarget : runCurrentInferenceTarget)?.Invoke();
        }

        private void ExecuteApplySelectedInferenceModelCommand()
        {
            if (!CanApplySelectedInferenceModel)
            {
                return;
            }

            SelectedModelRegistryHistoryItem = SelectedInferenceModelHistoryItem;
            PromoteSelectedModelHistoryCommand.Execute(null);
        }

        private bool CanRunInferenceTargetCommand()
        {
            if (!IsInferenceControlsVisible || !IsInferenceModelSelectionReady)
            {
                return false;
            }

            return inferenceTargetIndex switch
            {
                1 => canRunUnexecutedInferenceTarget?.Invoke() == true,
                2 => canRunVisibleInferenceTarget?.Invoke() == true,
                _ => canRunCurrentInferenceTarget?.Invoke() == true
            };
        }

        private void RefreshInferenceTargetCommandState()
        {
            OnPropertyChanged(nameof(CanRunInferenceTarget));
            OnPropertyChanged(nameof(InferenceRunTargetToolTipText));
            if (RunInferenceTargetCommand is RelayCommand command)
            {
                command.RaiseCanExecuteChanged();
            }

            RefreshInferenceModelCommandState();
        }

        private void RefreshInferenceModelCommandState()
        {
            OnPropertyChanged(nameof(CanApplySelectedInferenceModel));
            if (ApplySelectedInferenceModelCommand is RelayCommand command)
            {
                command.RaiseCanExecuteChanged();
            }
        }

        public void ConfigureTemporaryOriginalViewLifecycle(
            Action<KeyInputCommandArgs> previewKeyUp,
            Action deactivated)
        {
            PreviewKeyUpCommand = new RelayCommand<KeyInputCommandArgs>(previewKeyUp ?? NoOpKeyCommand);
            DeactivatedCommand = new RelayCommand(deactivated ?? NoOpCommand);
        }

        public void RestoreWorkspaceLayoutSettings()
        {
            ApplyWorkspaceLayoutSettings(workspaceLayoutSettingsService.Load());
            workspaceLayoutSettingsRestored = true;
            ApplyResponsiveWorkspaceDefaults();
        }

        public void SaveWorkspaceLayoutSettings()
        {
            PersistWorkspaceLayoutSettings(new WpfWorkspaceLayoutSettings
            {
                WorkflowPaneWidth = rightWorkflowExpandedPaneWidth,
                ImageQueuePaneWidth = imageQueueExpandedPaneWidth
            });
        }

        public void ConfigureAnomalyEvaluationWorkflow(
            AnomalyClassificationEvaluationWorkflowService workflowService,
            Func<AnomalyEvaluationCallbacks> callbacksProvider)
        {
            anomalyClassificationEvaluationWorkflowService = workflowService
                ?? throw new ArgumentNullException(nameof(workflowService));
            anomalyEvaluationCallbacksProvider = callbacksProvider
                ?? throw new ArgumentNullException(nameof(callbacksProvider));
            RunAnomalyEvaluationCommand = new RelayCommand(() => _ = ExecuteRunAnomalyEvaluationCommandAsync());
            LoadAnomalyEvaluationSummaryCommand = new RelayCommand(ExecuteLoadAnomalyEvaluationSummaryCommand);
        }

        public void ApplyWorkflowCommandState(WorkflowCommandState state)
        {
            IsCurrentImageDetectionEnabled = state?.CanRunInference == true;
            canRunInferenceTarget = state?.CanRunInferenceTarget == true;
            inferenceRunTargetUnavailableHint = state?.BatchDetectionToolTip ?? string.Empty;
            OnPropertyChanged(nameof(IsInferenceTargetEnabled));
            canRunModelCenterCommands = state?.CanSaveProjectConfig == true;
            canRunModelCenterReviewCommands = state?.CanRunGeneralCommands == true;
            modelCenterConfirmModelUnavailableToolTip = state?.CanSaveProjectConfigUnavailableHint ?? string.Empty;
            RefreshModelCenterConfirmModelEnabled();
            RefreshModelCenterReviewCandidateEnabled();
            RefreshModelCenterInspectCurrentImageEnabled();
            RefreshSelectedModelHistoryActionEnabled();
            RefreshModelCenterAnomalyEvaluationPickerEnabled();
            RefreshInferenceReviewState();
        }

        private async Task ExecuteRunAnomalyEvaluationCommandAsync()
        {
            AnomalyEvaluationCallbacks callbacks = anomalyEvaluationCallbacksProvider?.Invoke();
            if (anomalyClassificationEvaluationWorkflowService == null
                || callbacks == null
                || callbacks.IsApplicationCloseApproved?.Invoke() == true
                || anomalyClassificationEvaluationWorkflowService.IsRunning)
            {
                return;
            }

            LabelingProjectData data = callbacks.DataProvider?.Invoke();
            if (data?.ProjectSettings?.DatasetPurpose != LabelingDatasetPurpose.AnomalyDetection)
            {
                callbacks.SetCommandStatus?.Invoke("\uC774\uC0C1 \uBD84\uB958 \uD3C9\uAC00\ub294 anomaly \uB370\uC774\uD130\uC14B\uC5D0\uC11C\uB9CC \uC2E4\uD589\uD569\uB2C8\uB2E4.", false);
                return;
            }

            callbacks.PrepareRun?.Invoke();
            callbacks.SetCommandStatus?.Invoke("\uC774\uC0C1 \uBD84\uB958 \uD3C9\uAC00 \uC2E4\uD589 \uC911...", true);
            callbacks.AppendLog?.Invoke("Anomaly classification evaluation started.");

            try
            {
                AnomalyClassificationEvaluationWorkflowRunResult workflowResult = await anomalyClassificationEvaluationWorkflowService
                    .RunAsync(data)
                    .ConfigureAwait(true);
                if (callbacks.IsApplicationCloseApproved?.Invoke() == true)
                {
                    return;
                }

                if (!workflowResult.Succeeded)
                {
                    string errorText = workflowResult.ValidationErrors.Count > 0
                        ? "\uC774\uC0C1 \uBD84\uB958 \uD3C9\uAC00 \uC2E4\uD589 \uBD88\uAC00: " + string.Join(" / ", workflowResult.ValidationErrors.Take(3))
                        : AnomalyEvaluationFailurePresentationService.Build(workflowResult.RunResult);
                    callbacks.SetCommandStatus?.Invoke(errorText, false);
                    callbacks.AppendLog?.Invoke(errorText);
                    return;
                }

                SetModelCenterAnomalyEvaluationState(workflowResult.Presentation);
                string summaryName = Path.GetFileName(Path.GetDirectoryName(workflowResult.RunResult.SummaryPath) ?? workflowResult.RunResult.SummaryPath);
                string completeText = $"\uC774\uC0C1 \uBD84\uB958 \uD3C9\uAC00 \uC644\uB8CC: {summaryName}";
                callbacks.RefreshDashboard?.Invoke();
                callbacks.SetCommandStatus?.Invoke(completeText, false);
                callbacks.AppendLog?.Invoke($"{completeText}: {workflowResult.RunResult.SummaryPath}");
            }
            catch (Exception ex)
            {
                if (callbacks.IsApplicationCloseApproved?.Invoke() != true)
                {
                    string errorText = $"\uC774\uC0C1 \uBD84\uB958 \uD3C9\uAC00 \uC2E4\uD328: {ex.Message}";
                    callbacks.SetCommandStatus?.Invoke(errorText, false);
                    callbacks.AppendLog?.Invoke(errorText);
                }
            }
            finally
            {
                if (callbacks.IsApplicationCloseApproved?.Invoke() != true)
                {
                    callbacks.UpdateCommandState?.Invoke();
                }
            }
        }

        private void ExecuteLoadAnomalyEvaluationSummaryCommand()
        {
            AnomalyEvaluationCallbacks callbacks = anomalyEvaluationCallbacksProvider?.Invoke();
            if (anomalyClassificationEvaluationWorkflowService == null
                || callbacks == null
                || callbacks.IsApplicationCloseApproved?.Invoke() == true)
            {
                return;
            }

            LabelingProjectData data = callbacks.DataProvider?.Invoke();
            if (data?.ProjectSettings?.DatasetPurpose != LabelingDatasetPurpose.AnomalyDetection)
            {
                callbacks.SetCommandStatus?.Invoke("\uC774\uC0C1 \uBD84\uB958 \uD3C9\uAC00\ub294 anomaly \uB370\uC774\uD130\uC14B\uC5D0\uC11C\uB9CC \uBD88\uB7EC\uC635\uB2C8\uB2E4.", false);
                return;
            }

            AnomalyClassificationEvaluationRefreshResult refreshResult = anomalyClassificationEvaluationWorkflowService.Refresh(data);
            string initialPath = !string.IsNullOrWhiteSpace(anomalyClassificationEvaluationWorkflowService.PreferredSummaryPath)
                ? anomalyClassificationEvaluationWorkflowService.PreferredSummaryPath
                : refreshResult.SummaryPath;
            if (string.IsNullOrWhiteSpace(initialPath))
            {
                initialPath = data.OutputRootPath ?? string.Empty;
            }

            string selectedPath = callbacks.SelectSummaryPath?.Invoke(initialPath);
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                callbacks.SetCommandStatus?.Invoke("\uC774\uC0C1 \uBD84\uB958 \uD3C9\uAC00 summary \uC120\uD0DD\uC744 \uCDE8\uC18C\uD588\uC2B5\uB2C8\uB2E4.", false);
                return;
            }

            AnomalyClassificationEvaluationSummaryLoadResult loadResult = anomalyClassificationEvaluationWorkflowService.LoadSummary(data, selectedPath);
            if (loadResult.Succeeded)
            {
                SetModelCenterAnomalyEvaluationState(loadResult.Presentation);
                callbacks.SetCommandStatus?.Invoke($"\uC774\uC0C1 \uBD84\uB958 \uD3C9\uAC00 summary \uBD88\uB7EC\uC624\uAE30 \uC644\uB8CC: {Path.GetFileName(selectedPath)}", false);
                callbacks.AppendLog?.Invoke($"Anomaly classification evaluation summary loaded: {selectedPath}");
                return;
            }

            ClearModelCenterAnomalyEvaluationState();
            callbacks.SetCommandStatus?.Invoke("\uC774\uC0C1 \uBD84\uB958 \uD3C9\uAC00 summary\uB97C \uC77D\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4. JSON \uD30C\uC77C\uACFC \uD3C9\uAC00 \uACB0\uACFC\uB97C \uD655\uC778\uD558\uC138\uC694.", false);
            callbacks.AppendLog?.Invoke($"Anomaly classification evaluation summary load failed: {selectedPath}");
        }

        public void SetWorkflowModeState(bool isInferenceMode, bool canSwitchMode)
        {
            IsLabelingModeActive = !isInferenceMode;
            IsInferenceModeActive = isInferenceMode;
            IsLabelingModeButtonEnabled = IsLabelingModeActive || canSwitchMode;
            IsInferenceModeButtonEnabled = IsInferenceModeActive || canSwitchMode;
        }

        public void ShowSavedLabelsWorkflow()
        {
            SetWorkflowStage(WpfShellWorkflowStage.Labeling);
            SetRightWorkflowShortcut(WpfRightWorkflowShortcut.SavedLabels);
            SetRightWorkflowDockExpanded(true);
        }

        public void ShowCandidateReviewWorkflow(bool keepLabelingStage = false)
        {
            SetWorkflowStage(keepLabelingStage ? WpfShellWorkflowStage.Labeling : WpfShellWorkflowStage.Inference);
            if (keepLabelingStage) SetRightWorkflowShortcut(WpfRightWorkflowShortcut.SavedLabels);
            SetRightWorkflowDockExpanded(true);
        }

        public void ShowGuideToolsWorkflow(WpfShellWorkflowStage stage)
        {
            SetWorkflowStage(stage);
            SetRightWorkflowShortcut(stage == WpfShellWorkflowStage.Labeling
                || stage == WpfShellWorkflowStage.Dataset
                ? WpfRightWorkflowShortcut.LabelingGuide
                : WpfRightWorkflowShortcut.None);
            SetRightWorkflowDockExpanded(true);
        }

        public void ShowClassCatalogWorkflow(WpfShellWorkflowStage stage)
        {
            SetWorkflowStage(stage);
            SetRightWorkflowShortcut(stage == WpfShellWorkflowStage.Labeling
                || stage == WpfShellWorkflowStage.Dataset
                ? WpfRightWorkflowShortcut.ClassCatalog
                : WpfRightWorkflowShortcut.None);
            SetRightWorkflowDockExpanded(true);
        }

        public void ShowModelCenterWorkflow()
        {
            SetWorkflowStage(WpfShellWorkflowStage.TrainingModel);
            SetRightWorkflowDockExpanded(true);
        }

        internal void ConfigureWorkflowStageChanged(Action<WpfShellWorkflowStage> callback) => workflowStageChanged = callback;

        public void SetWorkflowStage(WpfShellWorkflowStage stage)
        {
            bool changed = currentWorkflowStage != stage;
            if (stage != WpfShellWorkflowStage.Labeling && isLabelingAssistPanelExpanded)
            {
                isLabelingAssistPanelExpanded = false;
                OnPropertyChanged(nameof(IsLabelingAssistPanelExpanded));
                OnPropertyChanged(nameof(IsInferenceControlsVisible));
            }

            currentWorkflowStage = stage;
            IsDatasetStageActive = stage == WpfShellWorkflowStage.Dataset;
            IsLabelingStageActive = stage == WpfShellWorkflowStage.Labeling;
            IsInferenceStageActive = stage == WpfShellWorkflowStage.Inference;
            IsTrainingModelStageActive = stage == WpfShellWorkflowStage.TrainingModel;
            IsModelComparisonStageActive = stage == WpfShellWorkflowStage.ModelComparison;
            OnPropertyChanged(nameof(IsInferenceControlsVisible));
            OnPropertyChanged(nameof(CanToggleLabelingAssistPanel));
            RefreshInferenceTargetCommandState();
            RefreshLabelingAssistPanelCommandState();
            ApplyWorkspaceStageLayout(stage);
            ApplyWorkflowStageViewVisibility(stage);
            ApplyWorkflowStageShortcutState(stage);
            ApplyRightWorkflowDockPreset(stage);

            RefreshLocalizedPresentation();
            if (changed) workflowStageChanged?.Invoke(stage);
        }

        public void RefreshLocalizedPresentation()
        {
            OnPropertyChanged(nameof(InferenceTargetOptions));
            OnPropertyChanged(nameof(InferenceTargetIndex));
            WorkflowStagePresentation presentation = WorkflowStagePresentationService.Build(currentWorkflowStage, IsAnomalyImageReviewMode);
            WorkflowStageProgressText = presentation.ProgressText;
            WorkflowStageTitleText = presentation.TitleText;
            WorkflowStageDetailText = presentation.DetailText;
            WorkflowStageNextActionText = presentation.NextActionText;
            ApplyRightWorkflowPresentation();
            RefreshRightWorkflowDockPresentation();
            RefreshInferenceResultPresentation();
            foreach (string property in new[]
            {
                nameof(RightWorkflowDockToggleText), nameof(RightWorkflowDockToggleToolTip), nameof(RightWorkflowViewDetailText),
                nameof(RightWorkflowRailCurrentViewText), nameof(CurrentDatasetName), nameof(CurrentDatasetPurposeText),
                nameof(CurrentDatasetPathText), nameof(CurrentDatasetStoragePathText), nameof(CurrentDatasetImageRootText),
                nameof(CurrentDatasetSourceText), nameof(CurrentDatasetToolTip), nameof(ModelCenterTrainingStatusText),
                nameof(ModelCenterTrainingDetailText), nameof(ModelCenterCurrentModelText), nameof(ModelCenterCandidateModelText),
                nameof(ModelCenterAdoptionText), nameof(ModelCenterNextActionText), nameof(ModelCenterCurrentModelDetailText),
                nameof(ModelCenterCandidateModelDetailText), nameof(ModelCenterAdoptionDetailText), nameof(ModelCenterNextActionDetailText),
                nameof(ModelCenterDecisionSummaryText), nameof(ModelCenterDecisionEvidenceText), nameof(ModelCenterDecisionActionText),
                nameof(ModelRegistrySummaryPrimaryText), nameof(ModelRegistryProfileText), nameof(ModelRegistryTrainingRunText),
                nameof(ModelRegistryCandidateModelText), nameof(ModelRegistryActionText), nameof(ModelRegistryHistorySummaryText),
                nameof(ModelCenterConfirmModelButtonText), nameof(ModelCenterConfirmModelButtonToolTip), nameof(ModelCenterInspectCurrentImageButtonText),
                nameof(ModelCenterInspectCurrentImageButtonToolTip), nameof(ModelCenterReviewCandidateButtonText), nameof(ModelCenterReviewCandidateButtonToolTip),
                nameof(ModelCenterActionStateText)
            })
            {
                OnPropertyChanged(property);
            }
            RefreshModelRegistryLocalizedText();
        }

        public void SetRightWorkflowShortcut(WpfRightWorkflowShortcut shortcut)
        {
            if (currentWorkflowStage == WpfShellWorkflowStage.Dataset && shortcut == WpfRightWorkflowShortcut.SavedLabels)
            {
                shortcut = WpfRightWorkflowShortcut.LabelingGuide;
            }

            if (currentWorkflowStage != WpfShellWorkflowStage.Dataset
                && currentWorkflowStage != WpfShellWorkflowStage.Labeling)
            {
                shortcut = WpfRightWorkflowShortcut.None;
            }

            IsSavedLabelsShortcutActive = shortcut == WpfRightWorkflowShortcut.SavedLabels;
            IsLabelingGuideShortcutActive = shortcut == WpfRightWorkflowShortcut.LabelingGuide;
            IsClassCatalogShortcutActive = shortcut == WpfRightWorkflowShortcut.ClassCatalog;
            IsRightWorkflowShortcutBarVisible = !IsInferenceStageActive && (currentWorkflowStage != WpfShellWorkflowStage.Dataset || IsClassCatalogShortcutActive);
            ApplyRightWorkflowPresentation(shortcut);
        }

        private WpfRightWorkflowShortcut GetActiveRightWorkflowShortcut()
        {
            if (IsSavedLabelsShortcutActive)
            {
                return WpfRightWorkflowShortcut.SavedLabels;
            }

            if (IsLabelingGuideShortcutActive)
            {
                return WpfRightWorkflowShortcut.LabelingGuide;
            }

            return IsClassCatalogShortcutActive
                ? WpfRightWorkflowShortcut.ClassCatalog
                : WpfRightWorkflowShortcut.None;
        }

        private void ApplyRightWorkflowPresentation()
            => ApplyRightWorkflowPresentation(GetActiveRightWorkflowShortcut());

        private void ApplyRightWorkflowPresentation(WpfRightWorkflowShortcut shortcut)
        {
            RightWorkflowPresentation presentation = RightWorkflowPresentationService.Build(
                currentWorkflowStage,
                shortcut);
            RightWorkflowViewTitleText = presentation.TitleText;
            RightWorkflowViewDetailText = currentWorkflowStage == WpfShellWorkflowStage.Dataset && shortcut != WpfRightWorkflowShortcut.ClassCatalog
                ? CurrentDatasetPurposeText
                : presentation.DetailText;
            RightWorkflowRailCurrentViewText = presentation.RailCurrentViewText;
        }

        private void ApplyWorkflowStageShortcutState(WpfShellWorkflowStage stage)
        {
            if (stage == WpfShellWorkflowStage.Dataset)
            {
                SetRightWorkflowShortcut(IsClassCatalogShortcutActive
                    ? WpfRightWorkflowShortcut.ClassCatalog
                    : WpfRightWorkflowShortcut.LabelingGuide);
                return;
            }

            if (stage != WpfShellWorkflowStage.Labeling)
            {
                SetRightWorkflowShortcut(WpfRightWorkflowShortcut.None);
                return;
            }

            if (IsAnomalyImageReviewMode)
            {
                SetRightWorkflowShortcut(WpfRightWorkflowShortcut.LabelingGuide);
                return;
            }

            if (!IsSavedLabelsShortcutActive
                && !IsClassCatalogShortcutActive)
            {
                SetRightWorkflowShortcut(WpfRightWorkflowShortcut.SavedLabels);
            }
        }

        private void ApplyWorkflowStageViewVisibility(WpfShellWorkflowStage stage)
        {
            IsSavedLabelsViewVisible = stage == WpfShellWorkflowStage.Labeling && !IsAnomalyImageReviewMode;
            IsCandidateReviewViewVisible = stage == WpfShellWorkflowStage.Inference;
            IsGuideToolsViewVisible = stage == WpfShellWorkflowStage.Dataset
                || stage == WpfShellWorkflowStage.Labeling;
            IsClassCatalogViewVisible = !IsAnomalyImageReviewMode
                && (stage == WpfShellWorkflowStage.Dataset
                    || stage == WpfShellWorkflowStage.Labeling);
            IsYoloModelCenterViewVisible = stage == WpfShellWorkflowStage.TrainingModel;
            int visibleViewCount = 0;
            visibleViewCount += IsSavedLabelsViewVisible ? 1 : 0;
            visibleViewCount += IsCandidateReviewViewVisible ? 1 : 0;
            visibleViewCount += IsGuideToolsViewVisible ? 1 : 0;
            visibleViewCount += IsClassCatalogViewVisible ? 1 : 0;
            visibleViewCount += IsYoloModelCenterViewVisible ? 1 : 0;
            IsRightWorkflowShortcutBarVisible = !IsInferenceStageActive;
            IsRightWorkflowSubNavigationVisible = !IsInferenceStageActive && visibleViewCount > 1 && !IsRightWorkflowShortcutBarVisible;
        }

        public void SetAnomalyImageReviewMode(bool enabled)
        {
            IsAnomalyImageReviewMode = enabled;
            IsAnnotationWorkflowVisible = !enabled;
            ApplyWorkflowStageViewVisibility(currentWorkflowStage);
            ApplyWorkflowStageShortcutState(currentWorkflowStage);
            RefreshLocalizedPresentation();
        }

        public void OpenClassCatalogDialog()
        {
            if (!IsRecipeLibraryVisible)
            {
                OperationsTabIndex = 3;
                IsClassCatalogDialogOpen = true;
            }
        }

        public void SetRightWorkflowDockExpanded(bool isExpanded)
        {
            if (IsModelWorkspaceActive)
            {
                IsRightWorkflowDockExpanded = true;
                IsRightWorkflowDockRailVisible = false;
                RightWorkflowPaneGridLength = WorkspaceCanvasPaneGridLengthValue;
                RefreshRightWorkflowDockPresentation();
                return;
            }

            IsRightWorkflowDockExpanded = isExpanded;
            IsRightWorkflowDockRailVisible = !isExpanded;
            ApplyVisibleWorkspacePaneWidths();
            RefreshRightWorkflowDockPresentation();
        }

        private void RefreshRightWorkflowDockPresentation()
        {
            if (IsModelWorkspaceActive)
            {
                RightWorkflowDockToggleText = T("WpfShell.Right.Dock.Collapse");
                RightWorkflowDockToggleToolTip = T("WpfShell.Right.Dock.ModelWorkspace");
                return;
            }

            RightWorkflowDockToggleText = IsRightWorkflowDockExpanded
                ? T("WpfShell.Right.Dock.Collapse")
                : T("WpfShell.Right.Dock.Expand");
            RightWorkflowDockToggleToolTip = IsRightWorkflowDockExpanded
                ? T("WpfShell.Right.Dock.Collapse.ToolTip")
                : T("WpfShell.Right.Dock.Expand.ToolTip");
        }

        public void SetRightWorkflowExpandedPaneWidth(double width)
        {
            if (double.IsNaN(width) || double.IsInfinity(width))
            {
                return;
            }

            rightWorkflowExpandedPaneWidth = Math.Clamp(width, 280D, 640D);
            ApplyVisibleWorkspacePaneWidths();
        }

        public void SetImageQueueExpandedPaneWidth(double width)
        {
            if (double.IsNaN(width) || double.IsInfinity(width))
            {
                return;
            }

            imageQueueExpandedPaneWidth = Math.Clamp(
                width,
                WpfWorkspaceLayoutSettings.MinimumImageQueuePaneWidth,
                WpfWorkspaceLayoutSettings.MaximumPaneWidth);
            ApplyVisibleWorkspacePaneWidths();
        }

        private void ApplyVisibleWorkspacePaneWidths()
        {
            if (IsModelWorkspaceActive) return;
            double workflowWidth = IsRightWorkflowDockExpanded ? rightWorkflowExpandedPaneWidth : RightWorkflowCollapsedPaneGridLengthValue.Value;
            double queueWidth = imageQueueExpandedPaneWidth;
            WorkspaceSplitterPaneGridLength = IsInferenceStageActive || IsLabelingStageActive ? new GridLength(0D) : IsDatasetStageActive ? new GridLength(IsCompactDatasetLayout ? 12D : 22D) : WorkspaceSplitterPaneGridLengthValue;
            if (workspaceViewportWidth > 0D)
            {
                double datasetPadding = IsDatasetStageActive ? (IsCompactDatasetLayout ? 28D : 56D) : 0D;
                double paneBudget = workspaceViewportWidth - datasetPadding - CanvasWorkspacePaneMinWidth - 2D * WorkspaceSplitterPaneGridLength.Value;
                double workflowMinimum = IsRightWorkflowDockExpanded ? WpfWorkspaceLayoutSettings.MinimumWorkflowPaneWidth : RightWorkflowCollapsedPaneGridLengthValue.Value;
                workflowWidth = Math.Min(workflowWidth, Math.Max(workflowMinimum, paneBudget - WpfWorkspaceLayoutSettings.MinimumImageQueuePaneWidth));
                queueWidth = Math.Min(queueWidth, Math.Max(WpfWorkspaceLayoutSettings.MinimumImageQueuePaneWidth, paneBudget - workflowWidth));
            }

            RightWorkflowPaneGridLength = new GridLength(workflowWidth);
            ImageQueuePaneGridLength = new GridLength(queueWidth);
        }

        private void ExecuteWorkflowPaneResizeCommand(double width)
        {
            SetRightWorkflowExpandedPaneWidth(width);
            SaveWorkspaceLayoutSettings();
        }

        private void ExecuteImageQueuePaneResizeCommand(double width)
        {
            SetImageQueueExpandedPaneWidth(width);
            SaveWorkspaceLayoutSettings();
        }

        private void ExecuteWorkspaceViewportSizeChangedCommand(double width)
        {
            if (!double.IsFinite(width) || width <= 0D)
            {
                return;
            }

            workspaceViewportWidth = width;
            OnPropertyChanged(nameof(IsStackedWorkflowHeader));
            OnPropertyChanged(nameof(IsCompactDatasetLayout));
            OnPropertyChanged(nameof(IsCompactInferenceLayout));
            ApplyResponsiveWorkspaceDefaults();
            ApplyVisibleWorkspacePaneWidths();
        }

        private void ApplyResponsiveWorkspaceDefaults()
        {
            if (!workspaceLayoutSettingsRestored || workspaceLayoutSettingsService.HasValidSavedSettings)
            {
                return;
            }

            WpfWorkspaceLayoutSettings defaults = WpfWorkspaceLayoutSettings.CreateDefault(workspaceViewportWidth);
            if (IsDatasetStageActive)
            {
                defaults.WorkflowPaneWidth = IsCompactDatasetLayout ? 344D : 410D;
                defaults.ImageQueuePaneWidth = IsCompactDatasetLayout ? 224D : 296D;
            }
            else if (IsInferenceStageActive)
            {
                defaults.WorkflowPaneWidth = IsCompactInferenceLayout ? 300D : 330D;
                defaults.ImageQueuePaneWidth = IsCompactInferenceLayout ? 240D : 270D;
            }
            ApplyWorkspaceLayoutSettings(defaults);
        }

        private void ExecuteResetWorkspaceLayoutCommand()
        {
            WpfWorkspaceLayoutSettings defaults = WpfWorkspaceLayoutSettings.CreateDefault(workspaceViewportWidth);
            ApplyWorkspaceLayoutSettings(defaults);
            if (PersistWorkspaceLayoutSettings(defaults))
            {
                WorkspaceLayoutReset?.Invoke(this, EventArgs.Empty);
            }
        }

        private void ApplyWorkspaceLayoutSettings(WpfWorkspaceLayoutSettings settings)
        {
            WpfWorkspaceLayoutSettings normalized = WpfWorkspaceLayoutSettings.Normalize(settings);
            SetRightWorkflowExpandedPaneWidth(normalized.WorkflowPaneWidth);
            SetImageQueueExpandedPaneWidth(normalized.ImageQueuePaneWidth);
        }

        private bool PersistWorkspaceLayoutSettings(WpfWorkspaceLayoutSettings settings)
        {
            if (workspaceLayoutSettingsService.TrySave(settings, out string error))
            {
                WorkspaceLayoutLastSaveError = string.Empty;
                return true;
            }

            WorkspaceLayoutLastSaveError = error;
            WorkspaceLayoutSaveFailed?.Invoke(this, EventArgs.Empty);
            return false;
        }

        private void ToggleRightWorkflowDock()
        {
            SetRightWorkflowDockExpanded(!IsRightWorkflowDockExpanded);
        }

        private void ApplyRightWorkflowDockPreset(WpfShellWorkflowStage stage)
        {
            // The labeling workbench keeps the object/class/history context visible
            // beside the canvas. Only the model-center stage needs the full-width
            // workspace without the annotation dock.
            SetRightWorkflowDockExpanded(stage != WpfShellWorkflowStage.TrainingModel);
        }

        private void ApplyWorkspaceStageLayout(WpfShellWorkflowStage stage)
        {
            bool isModelWorkspace = stage == WpfShellWorkflowStage.TrainingModel;
            bool isFocusedLabelingWorkspace = stage == WpfShellWorkflowStage.Labeling;
            // Dataset, labeling, and inference share queue-image-support order;
            // training uses its own history-and-work-area layout.
            LabelingWorkspaceFlowDirection = FlowDirection.RightToLeft;
            RightWorkflowDockColumn = 0;
            ImageQueueColumn = 4;
            WorkflowContextHeaderHeight = 48D;
            WorkflowStageRailHeight = 48D;
            IsModelWorkspaceActive = isModelWorkspace;
            IsCanvasWorkspaceVisible = !isModelWorkspace;
            IsImageQueueWorkspaceVisible = !isModelWorkspace;
            IsWorkspaceSplitterVisible = !isModelWorkspace && !IsInferenceStageActive;
            IsRightWorkflowDockToggleVisible = !isModelWorkspace;
            RightWorkflowPaneMinWidth = isModelWorkspace ? 420D : 72D;
            RightWorkflowPaneMaxWidth = isModelWorkspace ? double.PositiveInfinity : 640D;
            CanvasWorkspacePaneMinWidth = isModelWorkspace ? 0D : 440D;
            ImageQueuePaneMinWidth = isModelWorkspace ? 0D : WpfWorkspaceLayoutSettings.MinimumImageQueuePaneWidth;
            CanvasWorkspacePaneGridLength = isModelWorkspace
                ? WorkspaceHiddenPaneGridLengthValue
                : WorkspaceCanvasPaneGridLengthValue;
            ImageQueuePaneGridLength = isModelWorkspace
                ? WorkspaceHiddenPaneGridLengthValue
                : new GridLength(imageQueueExpandedPaneWidth);
            WorkspaceSplitterPaneGridLength = isModelWorkspace
                ? WorkspaceHiddenPaneGridLengthValue
                : WorkspaceSplitterPaneGridLengthValue;

            if (isModelWorkspace)
            {
                RightWorkflowPaneGridLength = WorkspaceCanvasPaneGridLengthValue;
            }
            else
            {
                ApplyResponsiveWorkspaceDefaults();
                ApplyVisibleWorkspacePaneWidths();
            }
        }

        public void SetModelCenterTrainingState(string statusText, string detailText)
        {
            ModelCenterTrainingStatusText = string.IsNullOrWhiteSpace(statusText)
                ? "\uD559\uC2B5 \uC0C1\uD0DC \uBBF8\uD655\uC778"
                : statusText.Trim();
            ModelCenterTrainingDetailText = string.IsNullOrWhiteSpace(detailText)
                ? "\uB370\uC774\uD130\uC14B \uC810\uAC80 \uD6C4 \uD559\uC2B5\uC744 \uC2DC\uC791\uD558\uC138\uC694."
                : detailText.Trim();
        }

        public void SetModelCenterRecoveryState(string titleText, string detailText, string actionText)
        {
            ModelCenterRecoveryTitleText = (titleText ?? string.Empty).Trim();
            ModelCenterRecoveryDetailText = (detailText ?? string.Empty).Trim();
            ModelCenterRecoveryActionText = (actionText ?? string.Empty).Trim();
            IsModelCenterRecoveryVisible = !string.IsNullOrWhiteSpace(ModelCenterRecoveryTitleText)
                || !string.IsNullOrWhiteSpace(ModelCenterRecoveryDetailText)
                || !string.IsNullOrWhiteSpace(ModelCenterRecoveryActionText);
        }

        public void ClearModelCenterRecoveryState()
        {
            SetModelCenterRecoveryState(string.Empty, string.Empty, string.Empty);
        }

        public void SetModelCenterAnomalyEvaluationState(WpfAnomalyClassificationEvaluationPresentation presentation)
        {
            if (presentation == null)
            {
                ClearModelCenterAnomalyEvaluationState();
                return;
            }

            ModelCenterAnomalyEvaluationRecommendationText = (presentation.RecommendationText ?? string.Empty).Trim();
            ModelCenterAnomalyEvaluationMetricsText = (presentation.MetricsText ?? string.Empty).Trim();
            ModelCenterAnomalyEvaluationDetailText = (presentation.DetailText ?? string.Empty).Trim();
            ModelCenterAnomalyEvaluationActionText = (presentation.ActionText ?? string.Empty).Trim();
            IsModelCenterAnomalyEvaluationVisible =
                !string.IsNullOrWhiteSpace(ModelCenterAnomalyEvaluationRecommendationText)
                || !string.IsNullOrWhiteSpace(ModelCenterAnomalyEvaluationMetricsText)
                || !string.IsNullOrWhiteSpace(ModelCenterAnomalyEvaluationDetailText)
                || !string.IsNullOrWhiteSpace(ModelCenterAnomalyEvaluationActionText);
        }

        public void ClearModelCenterAnomalyEvaluationState()
        {
            ModelCenterAnomalyEvaluationRecommendationText = string.Empty;
            ModelCenterAnomalyEvaluationMetricsText = string.Empty;
            ModelCenterAnomalyEvaluationDetailText = string.Empty;
            ModelCenterAnomalyEvaluationActionText = string.Empty;
            IsModelCenterAnomalyEvaluationVisible = false;
        }

        public void SetModelCenterAnomalyEvaluationPickerVisible(bool isVisible)
        {
            IsModelCenterAnomalyEvaluationPickerVisible = isVisible;
            RefreshModelCenterAnomalyEvaluationPickerEnabled();
        }

        private void RefreshModelCenterAnomalyEvaluationPickerEnabled()
        {
            IsModelCenterAnomalyEvaluationPickerEnabled =
                IsModelCenterAnomalyEvaluationPickerVisible && canRunModelCenterReviewCommands;
        }

        internal void ApplyModelCenterModelState(ModelCenterDashboardState state)
        {
            if (state == null)
            {
                return;
            }

            ModelCenterCurrentModelText = state.CurrentModelText ?? string.Empty;
            ModelCenterCurrentModelDetailText = state.CurrentModelDetailText ?? string.Empty;
            ModelCenterCandidateModelText = state.CandidateModelText ?? string.Empty;
            ModelCenterCandidateModelDetailText = state.CandidateModelDetailText ?? string.Empty;
            ModelCenterAdoptionText = state.AdoptionText ?? string.Empty;
            ModelCenterAdoptionDetailText = state.AdoptionDetailText ?? string.Empty;
            ModelCenterNextActionText = state.NextActionText ?? string.Empty;
            ModelCenterNextActionDetailText = state.NextActionDetailText ?? string.Empty;
            ModelCenterDecisionSummaryText = state.DecisionSummaryText ?? string.Empty;
            ModelCenterDecisionEvidenceText = state.DecisionEvidenceText ?? string.Empty;
            ModelCenterDecisionActionText = state.DecisionActionText ?? string.Empty;
            ModelCenterRuntimeActionText = state.RuntimeActionText ?? string.Empty;
            RefreshModelCenterInspectCurrentImageState();
            ModelCenterConfirmModelButtonText = state.ConfirmModelButtonText ?? string.Empty;
            modelCenterConfirmModelBaseToolTip = state.ConfirmModelButtonToolTip ?? string.Empty;
            isModelCenterConfirmModelAvailable = state.CanConfirmModel;
            RefreshModelCenterConfirmModelEnabled();
        }

        public void SetModelCenterModelState(
            string currentModelText,
            string candidateModelText,
            string adoptionText,
            string nextActionText,
            string confirmModelButtonText = null,
            string confirmModelButtonToolTip = null,
            bool canConfirmModel = false,
            string decisionSummaryText = null,
            string decisionEvidenceText = null,
            string decisionActionText = null,
            string runtimeActionText = null)
        {
            ApplyModelCenterModelState(
                ModelCenterDashboardPresentationService.BuildModelCenterState(
                    currentModelText,
                    candidateModelText,
                    adoptionText,
                    nextActionText,
                    confirmModelButtonText,
                    confirmModelButtonToolTip,
                    canConfirmModel,
                    decisionSummaryText,
                    decisionEvidenceText,
                    decisionActionText,
                    runtimeActionText));
        }

        public void SetModelCenterCandidateReviewState(
            string reviewCandidateButtonText,
            string reviewCandidateButtonToolTip,
            bool canReviewCandidate)
        {
            ModelCenterReviewCandidateButtonText = string.IsNullOrWhiteSpace(reviewCandidateButtonText)
                ? "\uD6C4\uBCF4 \uC5C6\uC74C"
                : reviewCandidateButtonText.Trim();
            ModelCenterReviewCandidateButtonToolTip = string.IsNullOrWhiteSpace(reviewCandidateButtonToolTip)
                ? OpenVisionLab.OpenVisionLanguageService.T("WpfModelCenterDashboard.Review.NoResultTooltip")
                : reviewCandidateButtonToolTip.Trim();
            isModelCenterReviewCandidateAvailable = canReviewCandidate;
            RefreshModelCenterReviewCandidateEnabled();
        }

        public void SetModelRegistryState(WpfModelRegistryPresentation presentation)
        {
            ModelRegistryState state = ModelRegistryPresentationService.BuildState(presentation, SelectedModelRegistryHistoryItem?.CandidateId, SelectedModelRegistryHistoryItem?.WeightsPath);
            var source = new WpfModelRegistryPresentation
            {
                SummaryPrimaryText = presentation?.SummaryPrimaryText,
                SummarySecondaryText = presentation?.SummarySecondaryText,
                ProfileText = presentation?.ProfileText,
                TrainingRunText = presentation?.TrainingRunText,
                CandidateModelText = presentation?.CandidateModelText,
                InspectionModelText = presentation?.InspectionModelText,
                ActionText = presentation?.ActionText,
                HistoryItems = state.HistoryItems,
                DecisionTextSource = presentation?.DecisionTextSource
            };
            ApplyModelRegistryState(state);
            modelRegistryLocalizationSource = source;
        }

        internal void ApplyModelRegistryState(ModelRegistryState state)
        {
            modelRegistryLocalizationSource = null;
            if (state == null)
            {
                return;
            }

            ModelRegistrySummaryPrimaryText = state.SummaryPrimaryText ?? string.Empty;
            ModelRegistrySummarySecondaryText = state.SummarySecondaryText ?? string.Empty;
            ModelRegistryProfileText = state.ProfileText ?? string.Empty;
            ModelRegistryTrainingRunText = state.TrainingRunText ?? string.Empty;
            ModelRegistryCandidateModelText = state.CandidateModelText ?? string.Empty;
            ModelRegistryInspectionModelText = state.InspectionModelText ?? string.Empty;
            ModelRegistryActionText = state.ActionText ?? string.Empty;
            isRefreshingModelRegistryHistory = true;
            try
            {
                ModelRegistryHistoryItems.Clear();
                foreach (WpfModelRegistryHistoryItem item in state.HistoryItems ?? Array.Empty<WpfModelRegistryHistoryItem>())
                {
                    if (item != null)
                    {
                        ModelRegistryHistoryItems.Add(item);
                    }
                }
            }
            finally
            {
                isRefreshingModelRegistryHistory = false;
            }

            IsModelRegistryHistoryVisible = state.IsHistoryVisible;
            ModelRegistryHistoryHeaderText = state.HistoryHeaderText ?? string.Empty;
            ModelRegistryHistorySummaryText = state.HistorySummaryText ?? string.Empty;
            RefreshSelectedInferenceModelHistoryItem();
            SetProperty(
                ref selectedModelRegistryHistoryItem,
                state.SelectedHistoryItem,
                nameof(SelectedModelRegistryHistoryItem));
            ApplySelectedModelHistoryPresentation(state.SelectedHistoryPresentation);
            RefreshInferenceTargetCommandState();
        }

        private void RefreshSelectedInferenceModelHistoryItem()
        {
            WpfModelRegistryHistoryItem selection = hasExplicitInferenceModelSelection
                ? ModelRegistryHistoryItems.FirstOrDefault(item =>
                    string.Equals(item.CandidateId, selectedInferenceModelCandidateId, StringComparison.Ordinal)
                    && string.Equals(item.WeightsPath, selectedInferenceModelWeightsPath, StringComparison.OrdinalIgnoreCase))
                : null;
            if (hasExplicitInferenceModelSelection && selection == null)
            {
                hasExplicitInferenceModelSelection = false;
                selectedInferenceModelCandidateId = string.Empty;
                selectedInferenceModelWeightsPath = string.Empty;
            }

            selection ??= ModelRegistryHistoryItems.FirstOrDefault(item => item.IsCurrentInspectionModel);

            SetProperty(
                ref selectedInferenceModelHistoryItem,
                selection,
                nameof(SelectedInferenceModelHistoryItem));
            OnPropertyChanged(nameof(IsInferenceModelSelectionReady));
            RefreshInferenceModelCommandState();
            inferenceModelSelectionChanged?.Invoke();
        }

        private void RefreshModelRegistryLocalizedText()
        {
            WpfModelRegistryPresentation source = modelRegistryLocalizationSource;
            if (source == null) return;
            foreach (WpfModelRegistryHistoryItem item in ModelRegistryHistoryItems) item.RefreshLocalizedPresentation();
            ModelRegistryState state = ModelRegistryPresentationService.BuildState(source, SelectedModelRegistryHistoryItem?.CandidateId, SelectedModelRegistryHistoryItem?.WeightsPath);
            ModelRegistrySummarySecondaryText = state.SummarySecondaryText;
            ModelRegistryInspectionModelText = state.InspectionModelText;
            ModelRegistryHistoryHeaderText = state.HistoryHeaderText;
            RefreshSelectedModelHistoryState(localizedTextOnly: true);
            if (source.DecisionTextSource == null) return;
            var origin = source.DecisionTextSource(false, false);
            bool renderCandidate = string.Equals(source.CandidateModelText, origin.OriginCandidate, StringComparison.Ordinal);
            bool renderAction = string.Equals(source.ActionText, origin.OriginAction, StringComparison.Ordinal);
            if (!renderCandidate && !renderAction) return;
            var text = source.DecisionTextSource(renderCandidate, renderAction);
            if (renderCandidate) ModelRegistryCandidateModelText = ModelRegistryPresentationService.NormalizeRegistryText(text.CandidateText, OpenVisionLab.OpenVisionLanguageService.T("WpfModelCenterDashboard.Candidate.None"));
            if (renderAction) ModelRegistryActionText = ModelRegistryPresentationService.NormalizeRegistryText(text.ActionText, OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Action.Structure"));
        }

        internal void ClearModelRegistryLocalizationSource()
            => modelRegistryLocalizationSource = null;

        private void RefreshSelectedModelHistoryState(bool localizedTextOnly = false)
        {
            WpfModelRegistryHistorySelectionPresentation presentation = ModelRegistryPresentationService.BuildSelectedHistoryPresentation(ModelRegistryHistoryItems, SelectedModelRegistryHistoryItem);
            if (!localizedTextOnly)
            {
                ApplySelectedModelHistoryPresentation(presentation);
                return;
            }
            SelectedModelHistoryTitleText = presentation.TitleText;
            SelectedModelHistoryDetailText = presentation.DetailText;
            SelectedModelHistoryDecisionText = presentation.DecisionText;
            SelectedModelHistoryComparisonTitleText = presentation.ComparisonTitleText;
            SelectedModelHistoryCurrentModelText = presentation.CurrentModelText;
            SelectedModelHistorySelectedModelText = presentation.SelectedModelText;
            SelectedModelHistoryActionText = presentation.ActionText;
            SelectedModelHistoryActionToolTip = presentation.ActionToolTip;
            RefreshSelectedModelHistoryActionEnabled(updateEnabled: false);
        }

        private void ApplySelectedModelHistoryPresentation(WpfModelRegistryHistorySelectionPresentation presentation)
        {
            if (presentation == null)
            {
                return;
            }

            IsSelectedModelHistoryVisible = presentation.IsVisible;
            SelectedModelHistoryTitleText = presentation.TitleText;
            SelectedModelHistoryDetailText = presentation.DetailText;
            SelectedModelHistoryMetricText = presentation.MetricText;
            SelectedModelHistoryDecisionText = presentation.DecisionText;
            SelectedModelHistoryComparisonTitleText = presentation.ComparisonTitleText;
            SelectedModelHistoryCurrentModelText = presentation.CurrentModelText;
            SelectedModelHistorySelectedModelText = presentation.SelectedModelText;
            SelectedModelHistoryComparisonMetricText = presentation.ComparisonMetricText;
            SelectedModelHistoryActionText = presentation.ActionText;
            SelectedModelHistoryActionToolTip = presentation.ActionToolTip;
            isSelectedModelHistoryActionAvailable = presentation.CanPromoteToInspectionModel;
            RefreshSelectedModelHistoryActionEnabled();
        }

        private void RefreshSelectedModelHistoryActionEnabled(bool updateEnabled = true)
        {
            if (updateEnabled) IsSelectedModelHistoryActionEnabled = isSelectedModelHistoryActionAvailable && canRunModelCenterCommands;
            if (SelectedModelRegistryHistoryItem != null
                && (IsSelectedModelHistoryActionEnabled || string.IsNullOrWhiteSpace(modelCenterConfirmModelUnavailableToolTip)))
            {
                SelectedModelHistoryActionToolTip = string.IsNullOrWhiteSpace(SelectedModelRegistryHistoryItem.ActionToolTip)
                    ? OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.History.Selection.AssignToolTip")
                    : SelectedModelRegistryHistoryItem.ActionToolTip.Trim();
            }

            if (isSelectedModelHistoryActionAvailable
                && !IsSelectedModelHistoryActionEnabled
                && !string.IsNullOrWhiteSpace(modelCenterConfirmModelUnavailableToolTip))
            {
                SelectedModelHistoryActionToolTip = modelCenterConfirmModelUnavailableToolTip;
            }
        }

        public void SetDatasetContext(
            string datasetName,
            string purposeText,
            string outputRootPath,
            string imageRootPath,
            bool canOpenDatasetFolder,
            int classCount = 0)
        {
            DatasetContextPresentation presentation = DatasetContextPresentationService.Build(
                datasetName,
                purposeText,
                outputRootPath,
                imageRootPath,
                classCount);

            CurrentDatasetName = presentation.DatasetName;
            CurrentDatasetPurposeText = presentation.PurposeText;
            CurrentDatasetPathText = presentation.CombinedPathText;
            CurrentDatasetStoragePathText = presentation.StoragePathText;
            CurrentDatasetImageRootText = presentation.ImageRootText;
            CurrentDatasetSourceText = presentation.SourceText;
            CurrentDatasetToolTip = presentation.Tooltip;
            IsOpenDatasetFolderEnabled = canOpenDatasetFolder;
            ApplyRightWorkflowPresentation();
        }

        private void RefreshModelCenterConfirmModelEnabled()
        {
            IsModelCenterConfirmModelEnabled = isModelCenterConfirmModelAvailable && canRunModelCenterCommands;
            RefreshModelCenterConfirmModelToolTip();
            RefreshModelCenterActionStateText();
        }

        private void RefreshModelCenterReviewCandidateEnabled()
        {
            IsModelCenterReviewCandidateEnabled = isModelCenterReviewCandidateAvailable && canRunModelCenterReviewCommands;
            RefreshModelCenterActionStateText();
        }

        private void RefreshModelCenterInspectCurrentImageState()
        {
            ModelCenterInspectCurrentImageButtonText = OpenVisionLab.OpenVisionLanguageService.T("WpfShell.ModelCenter.InferCurrentImage.Text");
            ModelCenterInspectCurrentImageButtonToolTip = ModelCenterDashboardPresentationService.BuildInspectCurrentImageToolTip(
                ModelCenterCurrentModelDetailText,
                ModelCenterRuntimeActionText);
            RefreshModelCenterInspectCurrentImageEnabled();
        }

        private void RefreshModelCenterInspectCurrentImageEnabled()
        {
            IsModelCenterInspectCurrentImageEnabled = IsCurrentImageDetectionEnabled && canRunModelCenterReviewCommands;
            RefreshModelCenterActionStateText();
        }

        private void RefreshModelCenterConfirmModelToolTip()
        {
            ModelCenterConfirmModelButtonToolTip = ModelCenterDashboardPresentationService.BuildConfirmModelAvailabilityToolTip(
                isModelCenterConfirmModelAvailable,
                IsModelCenterConfirmModelEnabled,
                modelCenterConfirmModelUnavailableToolTip,
                modelCenterConfirmModelBaseToolTip ?? OpenVisionLab.OpenVisionLanguageService.T("WpfModelCenterDashboard.Confirm.NoResultTooltip"));
        }

        private void RefreshModelCenterActionStateText()
        {
            ModelCenterActionStateText = ModelCenterDashboardPresentationService.BuildActionStateText(
                ModelCenterRuntimeActionText,
                ModelCenterReviewCandidateButtonText,
                IsModelCenterReviewCandidateEnabled,
                isModelCenterReviewCandidateAvailable,
                canRunModelCenterReviewCommands,
                ModelCenterReviewCandidateButtonToolTip,
                ModelCenterConfirmModelButtonText,
                IsModelCenterConfirmModelEnabled,
                isModelCenterConfirmModelAvailable,
                canRunModelCenterCommands,
                ModelCenterConfirmModelButtonToolTip,
                ModelCenterInspectCurrentImageButtonText,
                IsModelCenterInspectCurrentImageEnabled,
                canRunModelCenterReviewCommands && IsCurrentImageDetectionEnabled,
                ModelCenterInspectCurrentImageButtonToolTip);
        }

        private static string FormatInferenceDecisionStatusLabel(ModelDecisionResult decision)
            => OpenVisionLanguageService.T(decision.Status == "not_configured" ? "WpfComparisonWorkspace.RuleUnconfigured" : "WpfComparisonWorkspace." + decision.Status);

        private static string T(string key) => OpenVisionLanguageService.T(key);
    }

    public sealed class AnomalyEvaluationCallbacks
    {
        public Func<LabelingProjectData> DataProvider { get; init; }
        public Func<bool> IsApplicationCloseApproved { get; init; }
        public Action PrepareRun { get; init; }
        public Action<string, bool> SetCommandStatus { get; init; }
        public Action<string> AppendLog { get; init; }
        public Action RefreshDashboard { get; init; }
        public Action UpdateCommandState { get; init; }
        public Func<string, string> SelectSummaryPath { get; init; }
    }
}
