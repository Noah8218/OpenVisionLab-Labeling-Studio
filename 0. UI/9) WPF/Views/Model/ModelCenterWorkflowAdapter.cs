using MvcVisionSystem._1._Core;
using System;
using System.IO;
using MvcVisionSystem.Yolo;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem
{
    // Owns WPF composition for candidate decisions and the model-center dashboard.
    // Lifecycle and policy remain in the concrete workflow services supplied by the context.
    internal sealed class ModelCenterWorkflowAdapter
    {
        #region Fields
        private readonly ModelCenterWorkflowAdapterContext context;

        private LabelingProjectData projectData => context.DataProvider?.Invoke();

        private bool IsApplicationCloseApproved => context.IsApplicationCloseApprovedProvider?.Invoke() == true;

        private WpfCandidateReviewPanelViewModel CandidateReviewViewModel =>
            context.CandidateReviewViewModelProvider?.Invoke();

        private WpfLabelingShellViewModel ShellViewModel => context.ShellViewModelProvider?.Invoke();

        private WpfLearningWorkflowPanelViewModel LearningWorkflowViewModel =>
            context.LearningWorkflowViewModelProvider?.Invoke();

        private WpfTrainingSettingsPanelViewModel TrainingSettingsViewModel =>
            context.TrainingSettingsViewModelProvider?.Invoke();

        private WpfYoloModelSettingsPanelViewModel YoloModelSettingsViewModel =>
            context.YoloModelSettingsViewModelProvider?.Invoke();

        private ModelCandidateLifecycleWorkflowService ModelCandidateLifecycleWorkflowService =>
            context.ModelCandidateLifecycleWorkflowService;

        private ModelCenterDashboardWorkflowService ModelCenterDashboardWorkflowService =>
            context.ModelCenterDashboardWorkflowService;

        private AnomalyClassificationEvaluationWorkflowService AnomalyClassificationEvaluationWorkflowService =>
            context.AnomalyClassificationEvaluationWorkflowService;

        private string PendingTrainingBaselineWeightsPath
        {
            get => context.PendingTrainingBaselineWeightsPathProvider?.Invoke() ?? string.Empty;
            set => context.SetPendingTrainingBaselineWeightsPath?.Invoke(value ?? string.Empty);
        }

        private bool HasPendingTrainingWeightsRecipeSave
        {
            get => context.HasPendingTrainingWeightsRecipeSaveProvider?.Invoke() == true;
            set => context.SetHasPendingTrainingWeightsRecipeSave?.Invoke(value);
        }

        private string LastAutoAppliedTrainingWeightsPath
        {
            get => context.LastAutoAppliedTrainingWeightsPathProvider?.Invoke() ?? string.Empty;
            set => context.SetLastAutoAppliedTrainingWeightsPath?.Invoke(value ?? string.Empty);
        }
        #endregion

        #region Constructors
        internal ModelCenterWorkflowAdapter(ModelCenterWorkflowAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            ArgumentNullException.ThrowIfNull(context.DataProvider);
        }
        #endregion

        #region ShellCallbacks
        private void EnsureProjectSettings() => context.EnsureProjectSettings?.Invoke();

        private void ExecuteSaveYoloSettingsCommand() => context.ExecuteSaveYoloSettingsCommand?.Invoke();

        private void PopulateYoloEditorFields() => context.PopulateYoloEditorFields?.Invoke();

        private void RefreshYoloStatus() => context.RefreshYoloStatus?.Invoke();

        private void UpdateYoloTrainingHistoryText() => context.UpdateYoloTrainingHistoryText?.Invoke();

        private bool SaveModelMetadataConfigFromPanel()
            => context.SaveModelMetadataConfigFromPanel?.Invoke() == true;

        private void SetYoloCommandStatus(string text, bool isBusy)
            => context.SetCommandStatus?.Invoke(text, isBusy);

        private void AppendLog(string text) => context.AppendLog?.Invoke(text);

        private void SetProjectConfigStatus(string text) => context.SetProjectConfigStatus?.Invoke(text);

        private void SetModelStatus(string text) => context.SetModelStatus?.Invoke(text);

        private void UpdateYoloCommandButtons() => context.UpdateCommandState?.Invoke();

        private void SaveYoloEditorFields() => context.SaveYoloEditorFields?.Invoke();

        private void SaveTrainingEditorFields() => context.SaveTrainingEditorFields?.Invoke();

        private string SelectFile(string title, string filter, string initialPath)
            => context.SelectFile?.Invoke(title, filter, initialPath) ?? string.Empty;
        #endregion

        #region TrainingComparison
        internal WpfTrainingWeightsComparison BuildCurrentTrainingWeightsComparison()
        {
            EnsureProjectSettings();
            PythonModelSettings settings = projectData.ProjectSettings.PythonModel;
            return ModelCenterDashboardWorkflowService.BuildComparison(
                projectData,
                settings.WeightsPath,
                PendingTrainingBaselineWeightsPath);
        }
        #endregion

        #region ModelCandidateDecisionCommands
        internal void ExecuteSaveModelCandidateCommand()
        {
            if (IsApplicationCloseApproved)
            {
                return;
            }

            if (CandidateReviewViewModel?.IsModelPromotionHeld == true)
            {
                string status = ModelCandidateDecisionPresentationService.BuildHeldCandidateSaveBlockedStatus();
                SetYoloCommandStatus(status, isBusy: false);
                AppendLog(status);
                return;
            }

            ExecuteSaveYoloSettingsCommand();
        }

        internal void ExecuteRejectModelCandidateCommand()
        {
            if (IsApplicationCloseApproved)
            {
                return;
            }

            try
            {
                EnsureProjectSettings();
                PythonModelSettings settings = projectData.ProjectSettings.PythonModel;
                string candidateWeightsPath = settings.WeightsPath?.Trim() ?? string.Empty;
                string baselineWeightsPath = PendingTrainingBaselineWeightsPath?.Trim() ?? string.Empty;

                if (!HasPendingTrainingWeightsRecipeSave || string.IsNullOrWhiteSpace(candidateWeightsPath))
                {
                    SetYoloCommandStatus(ModelCandidateDecisionPresentationService.BuildNoRejectCandidateStatus(), isBusy: false);
                    UpdateCandidateModelDecisionPanel();
                    return;
                }

                WpfTrainingWeightsComparison comparison = BuildCurrentTrainingWeightsComparison();
                ModelCandidateLifecycleResult result = ModelCandidateLifecycleWorkflowService.Reject(
                    new ModelCandidateLifecycleRequest
                    {
                        Data = projectData,
                        HasPendingCandidate = HasPendingTrainingWeightsRecipeSave,
                        CandidateWeightsPath = candidateWeightsPath,
                        BaselineWeightsPath = baselineWeightsPath,
                        MetricsSummary = TrainingComparisonPresentationService.BuildComparisonStatusText(comparison),
                        DecisionSummary = ModelCandidateDecisionPresentationService.BuildRejectDecisionSummary(),
                        SaveModelMetadata = SaveModelMetadataConfigFromPanel
                    });

                if (result.ShouldRetainPendingCandidate)
                {
                    RestorePendingCandidateModelState(settings, result.CandidateWeightsPath, result.BaselineWeightsPath);
                }
                else if (result.IsCommitted)
                {
                    HasPendingTrainingWeightsRecipeSave = false;
                    PendingTrainingBaselineWeightsPath = string.Empty;
                }

                PopulateYoloEditorFields();
                RefreshYoloStatus();
                UpdateYoloTrainingHistoryText();
                RefreshModelCenterDashboard();

                bool configSaved = result.IsCommitted;
                SetYoloCommandStatus(ModelCandidateDecisionPresentationService.BuildRejectCommandStatus(result.CandidateWeightsPath, configSaved), isBusy: false);
                SetProjectConfigStatus(ModelCandidateDecisionPresentationService.BuildRejectProjectConfigStatus(configSaved));
                if (result.Status == ModelCandidateLifecycleStatus.Failed
                    || result.Status == ModelCandidateLifecycleStatus.BaselineUnavailable)
                {
                    string failureStatus = ModelCandidateDecisionPresentationService.BuildRejectFailureStatus(result.Error?.Message);
                    SetYoloCommandStatus(failureStatus, isBusy: false);
                    AppendLog(failureStatus);
                    return;
                }

                AppendLog(ModelCandidateDecisionPresentationService.BuildRejectLog(result.CandidateWeightsPath, result.BaselineWeightsPath));
            }
            catch (Exception ex)
            {
                string failureStatus = ModelCandidateDecisionPresentationService.BuildRejectFailureStatus(ex.Message);
                SetYoloCommandStatus(failureStatus, isBusy: false);
                AppendLog(failureStatus);
            }
        }

        internal void UpdateCandidateModelDecisionPanel(WpfTrainingWeightsComparison comparison = null)
        {
            if (CandidateReviewViewModel == null)
            {
                return;
            }

            EnsureProjectSettings();
            PythonModelSettings settings = projectData.ProjectSettings.PythonModel;
            comparison ??= BuildCurrentTrainingWeightsComparison();
            string currentWeightsPath = settings.WeightsPath?.Trim() ?? string.Empty;
            string baselineWeightsPath = PendingTrainingBaselineWeightsPath?.Trim() ?? string.Empty;
            ModelCandidate latestCandidate = ModelRegistryService.FindLatestCandidate(projectData.ProjectSettings.ModelRegistry);
            ApplyModelCandidateDecisionPresentation(
                ModelCandidateDecisionPresentationService.Build(new ModelCandidateDecisionSnapshot
                {
                    HasPendingRecipeSave = HasPendingTrainingWeightsRecipeSave,
                    IsPromotionHeld = CandidateReviewViewModel.IsModelPromotionHeld,
                    CandidateWeightsPath = currentWeightsPath,
                    BaselineWeightsPath = baselineWeightsPath,
                    CandidateWeightsFileExists = File.Exists(currentWeightsPath),
                    BaselineWeightsFileExists = File.Exists(baselineWeightsPath),
                    HasLatestCandidate = latestCandidate != null,
                    LatestCandidateWeightsPath = latestCandidate?.WeightsPath,
                    LatestCandidateDecision = latestCandidate?.Decision,
                    LatestCandidateDecisionSummary = latestCandidate?.DecisionSummary,
                    LatestCandidateSavedToRecipe = latestCandidate?.SavedToRecipe == true,
                    HasLatestWeights = comparison?.HasLatestWeights == true
                }));
        }

        private void ApplyModelCandidateDecisionPresentation(ModelCandidateDecisionPresentation presentation)
        {
            if (CandidateReviewViewModel == null || presentation == null)
            {
                return;
            }

            CandidateReviewViewModel.SetModelCandidateDecisionState(
                presentation.CanSave,
                presentation.CanReject,
                presentation.StatusText,
                presentation.DetailText,
                presentation.SaveToolTip,
                presentation.RejectToolTip);
        }

        // Promoting a registry history item is the other candidate-decision
        // path, so adoption and rejection share one lifecycle owner.
        internal void ExecutePromoteSelectedModelHistoryCommand()
        {
            if (IsApplicationCloseApproved)
            {
                return;
            }

            bool adoptionCommitted = false;
            try
            {
                EnsureProjectSettings();
                WpfModelRegistryHistoryItem selected = ShellViewModel?.SelectedModelRegistryHistoryItem;
                PythonModelSettings settings = projectData.ProjectSettings.PythonModel;
                string previousWeightsPath = settings.WeightsPath?.Trim() ?? string.Empty;
                ModelHistoryAdoptionPlan plan = ModelHistoryAdoptionPlanningService.Build(
                    new ModelHistoryAdoptionRequest
                    {
                        HasSelection = selected != null,
                        CandidateWeightsPath = selected?.WeightsPath,
                        CurrentWeightsPath = previousWeightsPath,
                        FallbackBaselineWeightsPath = selected?.BaselineWeightsPath,
                        MetricText = selected?.MetricText,
                        DecisionText = selected?.DecisionText,
                        DatasetVersionId = selected?.DatasetVersionId,
                        DatasetContentSha256 = selected?.DatasetContentSha256,
                        CandidateWeightsSha256 = selected?.WeightsSha256,
                        CandidateArtifactPath = selected?.ArtifactPath,
                        CandidateWeightsFileExists = selected != null && File.Exists(selected.WeightsPath?.Trim() ?? string.Empty),
                        CandidateArtifactExists = selected != null && File.Exists(selected.ArtifactPath?.Trim() ?? string.Empty),
                        CandidateArtifactHashMatches = selected != null
                            && (string.IsNullOrWhiteSpace(selected.WeightsSha256)
                                || ModelArtifactStoreService.Verify(selected.ArtifactPath, selected.WeightsSha256))
                    });

                if (plan.Status == ModelHistoryAdoptionPlanStatus.MissingSelection)
                {
                    SetYoloCommandStatus("\uBAA8\uB378 \uC774\uB825\uC744 \uC120\uD0DD\uD558\uC138\uC694.", isBusy: false);
                    return;
                }

                if (plan.Status == ModelHistoryAdoptionPlanStatus.MissingWeightsPath)
                {
                    SetModelCenterHistoryApplyFailure(
                        "\uBAA8\uB378 \uC774\uB825 \uC801\uC6A9 \uBD88\uAC00",
                        "\uC120\uD0DD\uD55C \uBAA8\uB378 \uC774\uB825\uC5D0 \uAC00\uC911\uCE58 \uD30C\uC77C \uACBD\uB85C\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.");
                    return;
                }

                if (plan.Status == ModelHistoryAdoptionPlanStatus.CandidateWeightsFileMissing)
                {
                    SetModelCenterHistoryApplyFailure(
                        "\uBAA8\uB378 \uC774\uB825 \uC801\uC6A9 \uBD88\uAC00",
                        $"\uC120\uD0DD\uD55C \uBAA8\uB378 \uD30C\uC77C\uC774 \uC5C6\uC2B5\uB2C8\uB2E4: {plan.CandidateWeightsPath}");
                    return;
                }

                if (plan.Status == ModelHistoryAdoptionPlanStatus.CandidateArtifactMissing
                    || plan.Status == ModelHistoryAdoptionPlanStatus.CandidateArtifactMismatch)
                {
                    SetModelCenterHistoryApplyFailure(
                        "\uBAA8\uB378 \uC774\uB825 \uC801\uC6A9 \uBD88\uAC00",
                        plan.ArtifactVerificationText);
                    return;
                }

                string candidateWeightsPath = plan.CandidateWeightsPath;
                if (plan.IsAlreadyCurrent)
                {
                    SetYoloCommandStatus($"\uC774\uBBF8 \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378\uC785\uB2C8\uB2E4: {Path.GetFileName(candidateWeightsPath)}", isBusy: false);
                    RefreshModelCenterDashboard();
                    return;
                }

                ModelCandidateLifecycleResult result = ModelCandidateLifecycleWorkflowService.Adopt(
                    new ModelCandidateLifecycleRequest
                    {
                        Data = projectData,
                        CandidateWeightsPath = plan.CandidateWeightsPath,
                        BaselineWeightsPath = plan.BaselineWeightsPath,
                        MetricsSummary = plan.MetricsSummary,
                        DecisionSummary = plan.DecisionSummary,
                        AdoptionPlan = plan,
                        SaveModelMetadata = SaveModelMetadataConfigFromPanel
                    });

                if (result.ShouldRetainPendingCandidate)
                {
                    RestorePendingCandidateModelState(settings, result.CandidateWeightsPath, result.BaselineWeightsPath);
                }
                else if (result.IsCommitted)
                {
                    adoptionCommitted = true;
                    HasPendingTrainingWeightsRecipeSave = false;
                    PendingTrainingBaselineWeightsPath = string.Empty;
                    LastAutoAppliedTrainingWeightsPath = result.CandidateWeightsPath;
                }

                PopulateYoloEditorFields();
                RefreshYoloStatus();
                UpdateYoloTrainingHistoryText();
                RefreshModelCenterDashboard();

                string modelName = Path.GetFileName(result.CandidateWeightsPath);
                if (result.IsCommitted)
                {
                    ShellViewModel?.ClearModelCenterRecoveryState();
                    SetModelStatus($"\uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378: {modelName}");
                    SetYoloCommandStatus($"\uBAA8\uB378 \uC774\uB825 \uC801\uC6A9 \uC644\uB8CC: {modelName}. \uB2E4\uC74C \uAC80\uC0AC\uBD80\uD130 \uC774 \uBAA8\uB378\uC744 \uC0AC\uC6A9\uD569\uB2C8\uB2E4.", isBusy: false);
                    AppendLog($"Model history adopted as inspection model: {result.CandidateWeightsPath} / previous={result.BaselineWeightsPath}");
                    return;
                }

                if (result.Status == ModelCandidateLifecycleStatus.Failed)
                {
                    SetModelCenterHistoryApplyFailure(
                        "\uBAA8\uB378 \uC774\uB825 \uC801\uC6A9 \uC2E4\uD328",
                        result.Error?.Message ?? "\uBAA8\uB378 \uBA54\uBAA8\uB9AC \uC0C1\uD0DC\uB97C \uC800\uC7A5\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.");
                    return;
                }

                SetModelCenterHistoryApplyFailure(
                    "\uBAA8\uB378 \uC774\uB825 \uC801\uC6A9\uC740 \uBA54\uBAA8\uB9AC\uC5D0\uB9CC \uBC18\uC601\uB428",
                    "\uC120\uD0DD\uD55C \uBAA8\uB378\uC744 recipe\uC5D0 \uC800\uC7A5\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4. \uC800\uC7A5 \uACBD\uB85C\uC640 recipe \uC774\uB984\uC744 \uD655\uC778\uD55C \uB4A4 \uBAA8\uB378 \uC124\uC815\uC744 \uC800\uC7A5\uD558\uC138\uC694.");
            }
            catch (Exception ex)
            {
                if (adoptionCommitted)
                {
                    string refreshFailureStatus = ModelCandidateDecisionPresentationService.BuildAdoptionRefreshFailureStatus(ex.Message);
                    SetYoloCommandStatus(refreshFailureStatus, isBusy: false);
                    AppendLog(refreshFailureStatus);
                    return;
                }

                SetModelCenterHistoryApplyFailure(
                    "\uBAA8\uB378 \uC774\uB825 \uC801\uC6A9 \uC2E4\uD328",
                    ex.Message);
            }
        }

        private void RestorePendingCandidateModelState(PythonModelSettings settings, string candidateWeightsPath, string baselineWeightsPath)
        {
            if (settings == null || string.IsNullOrWhiteSpace(candidateWeightsPath))
            {
                return;
            }

            settings.WeightsPath = candidateWeightsPath;
            YoloModelSettingsViewModel?.LoadFrom(settings);
            HasPendingTrainingWeightsRecipeSave = true;
            PendingTrainingBaselineWeightsPath = baselineWeightsPath;
        }

        private void SetModelCenterHistoryApplyFailure(string titleText, string detailText)
        {
            ShellViewModel?.SetModelCenterRecoveryState(
                titleText,
                detailText,
                "\uBAA8\uB378 \uC774\uB825\uC758 \uD30C\uC77C \uACBD\uB85C\uC640 recipe \uC800\uC7A5 \uC0C1\uD0DC\uB97C \uD655\uC778\uD558\uC138\uC694.");
            SetYoloCommandStatus($"{titleText}: {detailText}", isBusy: false);
            AppendLog($"{titleText}: {detailText}");
            RefreshModelCenterDashboard();
        }
        #endregion

        #region ModelCenterDashboard
        internal void RefreshModelCenterDashboard(
            WpfTrainingWeightsComparison comparison = null,
            string configuredWeightsPathOverride = null,
            bool pendingManualWeightsSelection = false)
        {
            ModelCenterDashboardWorkflowResult workflowResult = ModelCenterDashboardWorkflowService.Build(
                new ModelCenterDashboardWorkflowRequest
                {
                    Data = projectData,
                    Comparison = comparison,
                    ConfiguredWeightsPathOverride = configuredWeightsPathOverride,
                    PendingBaselineWeightsPath = PendingTrainingBaselineWeightsPath,
                    PendingManualWeightsSelection = pendingManualWeightsSelection,
                    HasPendingTrainingWeightsRecipeSave = HasPendingTrainingWeightsRecipeSave,
                    IsModelPromotionHeld = CandidateReviewViewModel?.IsModelPromotionHeld == true
                });
            comparison = workflowResult.Comparison;
            ModelCenterDashboardState dashboardState = workflowResult.DashboardState;
            ShellViewModel?.ApplyModelCenterModelState(dashboardState);
            LearningWorkflowViewModel?.SetTrainingModelLifecycleState(
                dashboardState.CurrentModelText,
                dashboardState.CandidateModelText,
                dashboardState.AdoptionText,
                dashboardState.NextActionText);
            TrainingSettingsViewModel?.SetPostTrainingModelActionState(
                dashboardState.CurrentModelText,
                dashboardState.CandidateModelText,
                dashboardState.AdoptionText,
                dashboardState.NextActionText,
                dashboardState.ReviewCandidateButtonText,
                dashboardState.ReviewCandidateButtonToolTip,
                dashboardState.CanReviewCandidate,
                dashboardState.ConfirmModelButtonText,
                dashboardState.ConfirmModelButtonToolTip,
                dashboardState.CanConfirmModel);
            ShellViewModel?.SetModelCenterCandidateReviewState(
                dashboardState.ReviewCandidateButtonText,
                dashboardState.ReviewCandidateButtonToolTip,
                dashboardState.CanReviewCandidate);
            ShellViewModel?.SetModelRegistryState(dashboardState.RegistryPresentation);
            RefreshModelCenterAnomalyEvaluationState();
            UpdateCandidateModelDecisionPanel(comparison);
        }

        internal AnomalyEvaluationCallbacks CreateAnomalyEvaluationCallbacks()
        {
            return new AnomalyEvaluationCallbacks
            {
                DataProvider = () => projectData,
                IsApplicationCloseApproved = () => IsApplicationCloseApproved,
                PrepareRun = () =>
                {
                    SaveYoloEditorFields();
                    SaveTrainingEditorFields();
                },
                SetCommandStatus = (text, isBusy) => SetYoloCommandStatus(text, isBusy),
                AppendLog = AppendLog,
                RefreshDashboard = () => RefreshModelCenterDashboard(),
                UpdateCommandState = UpdateYoloCommandButtons,
                SelectSummaryPath = SelectAnomalyEvaluationSummaryPath
            };
        }

        private string SelectAnomalyEvaluationSummaryPath(string initialPath)
        {
            return SelectFile(
                "\uC774\uC0C1 \uBD84\uB958 \uD3C9\uAC00 summary \uC120\uD0DD",
                "classification evaluation summary (*.json)|*.json|All files (*.*)|*.*",
                initialPath);
        }

        internal void RefreshModelCenterAnomalyEvaluationState()
        {
            AnomalyClassificationEvaluationRefreshResult refreshResult = AnomalyClassificationEvaluationWorkflowService.Refresh(projectData);
            ShellViewModel?.SetModelCenterAnomalyEvaluationPickerVisible(refreshResult.IsVisible);
            if (refreshResult.HasSummary)
            {
                ShellViewModel?.SetModelCenterAnomalyEvaluationState(refreshResult.Presentation);
            }
            else
            {
                ShellViewModel?.ClearModelCenterAnomalyEvaluationState();
            }
        }

        internal string ResolveModelComparisonSummaryPath()
        {
            if (projectData?.ProjectSettings?.DatasetPurpose == LabelingDatasetPurpose.AnomalyDetection)
            {
                AnomalyClassificationEvaluationRefreshResult refreshResult =
                    AnomalyClassificationEvaluationWorkflowService.Refresh(projectData);
                if (refreshResult.HasSummary)
                {
                    return refreshResult.SummaryPath;
                }
            }

            return CandidateReviewViewModel?.SelectedModelComparisonHistoryItem?.SourcePath ?? string.Empty;
        }

        #endregion

    }

    internal sealed class ModelCenterWorkflowAdapterContext
    {
        internal Func<LabelingProjectData> DataProvider { get; init; }
        internal ModelCandidateLifecycleWorkflowService ModelCandidateLifecycleWorkflowService { get; init; }
        internal ModelCenterDashboardWorkflowService ModelCenterDashboardWorkflowService { get; init; }
        internal AnomalyClassificationEvaluationWorkflowService AnomalyClassificationEvaluationWorkflowService { get; init; }
        internal Func<bool> IsApplicationCloseApprovedProvider { get; init; }
        internal Func<WpfCandidateReviewPanelViewModel> CandidateReviewViewModelProvider { get; init; }
        internal Func<WpfLabelingShellViewModel> ShellViewModelProvider { get; init; }
        internal Func<WpfLearningWorkflowPanelViewModel> LearningWorkflowViewModelProvider { get; init; }
        internal Func<WpfTrainingSettingsPanelViewModel> TrainingSettingsViewModelProvider { get; init; }
        internal Func<WpfYoloModelSettingsPanelViewModel> YoloModelSettingsViewModelProvider { get; init; }
        internal Func<string> PendingTrainingBaselineWeightsPathProvider { get; init; }
        internal Action<string> SetPendingTrainingBaselineWeightsPath { get; init; }
        internal Func<bool> HasPendingTrainingWeightsRecipeSaveProvider { get; init; }
        internal Action<bool> SetHasPendingTrainingWeightsRecipeSave { get; init; }
        internal Func<string> LastAutoAppliedTrainingWeightsPathProvider { get; init; }
        internal Action<string> SetLastAutoAppliedTrainingWeightsPath { get; init; }
        internal Action EnsureProjectSettings { get; init; }
        internal Action ExecuteSaveYoloSettingsCommand { get; init; }
        internal Action PopulateYoloEditorFields { get; init; }
        internal Action RefreshYoloStatus { get; init; }
        internal Action UpdateYoloTrainingHistoryText { get; init; }
        internal Func<bool> SaveModelMetadataConfigFromPanel { get; init; }
        internal Action<string, bool> SetCommandStatus { get; init; }
        internal Action<string> AppendLog { get; init; }
        internal Action<string> SetProjectConfigStatus { get; init; }
        internal Action<string> SetModelStatus { get; init; }
        internal Action UpdateCommandState { get; init; }
        internal Action SaveYoloEditorFields { get; init; }
        internal Action SaveTrainingEditorFields { get; init; }
        internal Func<string, string, string, string> SelectFile { get; init; }
    }

}
