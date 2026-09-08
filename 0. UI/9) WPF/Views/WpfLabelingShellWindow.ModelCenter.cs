using MvcVisionSystem._1._Core;
using System;
using System.IO;
using MvcVisionSystem.Yolo;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    // Responsibility group: candidate model decisions and model center dashboard.
    // These members remain WPF Window adapters; independent policy belongs in services.
    public partial class WpfLabelingShellWindow
    {
        #region ModelCandidateDecisionCommands
        private void ExecuteSaveModelCandidateCommand()
        {
            if (isApplicationCloseApproved)
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

        private void ExecuteRejectModelCandidateCommand()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            try
            {
                EnsureProjectSettings();
                PythonModelSettings settings = global.Data.ProjectSettings.PythonModel;
                string candidateWeightsPath = settings.WeightsPath?.Trim() ?? string.Empty;
                string baselineWeightsPath = pendingTrainingBaselineWeightsPath?.Trim() ?? string.Empty;

                if (!hasPendingTrainingWeightsRecipeSave || string.IsNullOrWhiteSpace(candidateWeightsPath))
                {
                    SetYoloCommandStatus(ModelCandidateDecisionPresentationService.BuildNoRejectCandidateStatus(), isBusy: false);
                    UpdateCandidateModelDecisionPanel();
                    return;
                }

                WpfTrainingWeightsComparison comparison = BuildCurrentTrainingWeightsComparison();
                ModelCandidateLifecycleResult result = modelCandidateLifecycleWorkflowService.Reject(
                    new ModelCandidateLifecycleRequest
                    {
                        Data = global.Data,
                        HasPendingCandidate = hasPendingTrainingWeightsRecipeSave,
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
                    hasPendingTrainingWeightsRecipeSave = false;
                    pendingTrainingBaselineWeightsPath = string.Empty;
                }

                PopulateYoloEditorFields();
                RefreshYoloStatus();
                UpdateYoloTrainingHistoryText();
                RefreshModelCenterDashboard();

                bool configSaved = result.IsCommitted;
                SetYoloCommandStatus(ModelCandidateDecisionPresentationService.BuildRejectCommandStatus(result.CandidateWeightsPath, configSaved), isBusy: false);
                SetProjectConfigStatus(ModelCandidateDecisionPresentationService.BuildRejectProjectConfigStatus(configSaved));
                if (result.Status == ModelCandidateLifecycleStatus.Failed)
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

        private void UpdateCandidateModelDecisionPanel(WpfTrainingWeightsComparison comparison = null)
        {
            if (CandidateReviewViewModel == null)
            {
                return;
            }

            EnsureProjectSettings();
            PythonModelSettings settings = global.Data.ProjectSettings.PythonModel;
            comparison ??= BuildCurrentTrainingWeightsComparison();
            string currentWeightsPath = settings.WeightsPath?.Trim() ?? string.Empty;
            string baselineWeightsPath = pendingTrainingBaselineWeightsPath?.Trim() ?? string.Empty;
            ModelCandidate latestCandidate = ModelRegistryService.FindLatestCandidate(global.Data.ProjectSettings.ModelRegistry);
            ApplyModelCandidateDecisionPresentation(
                ModelCandidateDecisionPresentationService.Build(new ModelCandidateDecisionSnapshot
                {
                    HasPendingRecipeSave = hasPendingTrainingWeightsRecipeSave,
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
        private void ExecutePromoteSelectedModelHistoryCommand()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            bool adoptionCommitted = false;
            try
            {
                EnsureProjectSettings();
                WpfModelRegistryHistoryItem selected = ShellViewModel?.SelectedModelRegistryHistoryItem;
                PythonModelSettings settings = global.Data.ProjectSettings.PythonModel;
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
                        CandidateWeightsFileExists = selected != null && File.Exists(selected.WeightsPath?.Trim() ?? string.Empty)
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

                string candidateWeightsPath = plan.CandidateWeightsPath;
                if (plan.IsAlreadyCurrent)
                {
                    SetYoloCommandStatus($"\uC774\uBBF8 \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378\uC785\uB2C8\uB2E4: {Path.GetFileName(candidateWeightsPath)}", isBusy: false);
                    RefreshModelCenterDashboard();
                    return;
                }

                ModelCandidateLifecycleResult result = modelCandidateLifecycleWorkflowService.Adopt(
                    new ModelCandidateLifecycleRequest
                    {
                        Data = global.Data,
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
                    hasPendingTrainingWeightsRecipeSave = false;
                    pendingTrainingBaselineWeightsPath = string.Empty;
                    lastAutoAppliedTrainingWeightsPath = result.CandidateWeightsPath;
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
            hasPendingTrainingWeightsRecipeSave = true;
            pendingTrainingBaselineWeightsPath = baselineWeightsPath;
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
        private void RefreshModelCenterDashboard(
            WpfTrainingWeightsComparison comparison = null,
            string configuredWeightsPathOverride = null,
            bool pendingManualWeightsSelection = false)
        {
            ModelCenterDashboardWorkflowResult workflowResult = modelCenterDashboardWorkflowService.Build(
                new ModelCenterDashboardWorkflowRequest
                {
                    Data = global.Data,
                    Comparison = comparison,
                    ConfiguredWeightsPathOverride = configuredWeightsPathOverride,
                    PendingBaselineWeightsPath = pendingTrainingBaselineWeightsPath,
                    PendingManualWeightsSelection = pendingManualWeightsSelection,
                    HasPendingTrainingWeightsRecipeSave = hasPendingTrainingWeightsRecipeSave,
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

        private void ExecuteRunAnomalyEvaluationCommand()
        {
            _ = ExecuteRunAnomalyEvaluationCommandAsync();
        }

        private async Task ExecuteRunAnomalyEvaluationCommandAsync()
        {
            if (isApplicationCloseApproved || anomalyClassificationEvaluationWorkflowService.IsRunning)
            {
                return;
            }

            EnsureProjectSettings();
            if (global.Data?.ProjectSettings?.DatasetPurpose != LabelingDatasetPurpose.AnomalyDetection)
            {
                SetYoloCommandStatus("\uC774\uC0C1 \uBD84\uB958 \uD3C9\uAC00\uB294 anomaly \uB370\uC774\uD130\uC14B\uC5D0\uC11C\uB9CC \uC2E4\uD589\uD569\uB2C8\uB2E4.", isBusy: false);
                return;
            }

            SaveYoloEditorFields();
            SaveTrainingEditorFields();
            UpdateYoloCommandButtons();
            SetYoloCommandStatus("\uC774\uC0C1 \uBD84\uB958 \uD3C9\uAC00 \uC2E4\uD589 \uC911...", isBusy: true);
            AppendLog("Anomaly classification evaluation started.");

            try
            {
                AnomalyClassificationEvaluationWorkflowRunResult workflowResult = await anomalyClassificationEvaluationWorkflowService
                    .RunAsync(global.Data)
                    .ConfigureAwait(true);
                if (isApplicationCloseApproved)
                {
                    return;
                }

                if (!workflowResult.Succeeded)
                {
                    string errorText = workflowResult.ValidationErrors.Count > 0
                        ? "\uC774\uC0C1 \uBD84\uB958 \uD3C9\uAC00 \uC2E4\uD589 \uBD88\uAC00: " + string.Join(" / ", workflowResult.ValidationErrors.Take(3))
                        : AnomalyEvaluationFailurePresentationService.Build(workflowResult.RunResult);
                    SetYoloCommandStatus(errorText, isBusy: false);
                    AppendLog(errorText);
                    return;
                }

                ShellViewModel?.SetModelCenterAnomalyEvaluationState(workflowResult.Presentation);
                string summaryName = Path.GetFileName(Path.GetDirectoryName(workflowResult.RunResult.SummaryPath) ?? workflowResult.RunResult.SummaryPath);
                string completeText = $"\uC774\uC0C1 \uBD84\uB958 \uD3C9\uAC00 \uC644\uB8CC: {summaryName}";
                RefreshModelCenterDashboard();
                SetYoloCommandStatus(completeText, isBusy: false);
                AppendLog($"{completeText}: {workflowResult.RunResult.SummaryPath}");
            }
            catch (Exception ex)
            {
                if (!isApplicationCloseApproved)
                {
                    string errorText = $"\uC774\uC0C1 \uBD84\uB958 \uD3C9\uAC00 \uC2E4\uD328: {ex.Message}";
                    SetYoloCommandStatus(errorText, isBusy: false);
                    AppendLog(errorText);
                }
            }
            finally
            {
                if (!isApplicationCloseApproved)
                {
                    UpdateYoloCommandButtons();
                }
            }
        }

        private void RefreshModelCenterAnomalyEvaluationState()
        {
            AnomalyClassificationEvaluationRefreshResult refreshResult = anomalyClassificationEvaluationWorkflowService.Refresh(global.Data);
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

        private void ExecuteLoadAnomalyEvaluationSummaryCommand()
        {
            EnsureProjectSettings();
            if (global.Data?.ProjectSettings?.DatasetPurpose != LabelingDatasetPurpose.AnomalyDetection)
            {
                SetYoloCommandStatus("\uC774\uC0C1 \uBD84\uB958 \uD3C9\uAC00\uB294 anomaly \uB370\uC774\uD130\uC14B\uC5D0\uC11C\uB9CC \uBD88\uB7EC\uC635\uB2C8\uB2E4.", isBusy: false);
                return;
            }

            AnomalyClassificationEvaluationRefreshResult refreshResult = anomalyClassificationEvaluationWorkflowService.Refresh(global.Data);
            string initialPath = !string.IsNullOrWhiteSpace(anomalyClassificationEvaluationWorkflowService.PreferredSummaryPath)
                ? anomalyClassificationEvaluationWorkflowService.PreferredSummaryPath
                : refreshResult.SummaryPath;
            if (string.IsNullOrWhiteSpace(initialPath))
            {
                initialPath = global.Data.OutputRootPath ?? string.Empty;
            }

            if (!TryPickFile(
                "\uC774\uC0C1 \uBD84\uB958 \uD3C9\uAC00 summary \uC120\uD0DD",
                "classification evaluation summary (*.json)|*.json|All files (*.*)|*.*",
                initialPath,
                out string selectedPath))
            {
                SetYoloCommandStatus("\uC774\uC0C1 \uBD84\uB958 \uD3C9\uAC00 summary \uC120\uD0DD\uC744 \uCDE8\uC18C\uD588\uC2B5\uB2C8\uB2E4.", isBusy: false);
                return;
            }

            AnomalyClassificationEvaluationSummaryLoadResult loadResult = anomalyClassificationEvaluationWorkflowService.LoadSummary(global.Data, selectedPath);
            if (loadResult.Succeeded)
            {
                ShellViewModel?.SetModelCenterAnomalyEvaluationState(loadResult.Presentation);
                SetYoloCommandStatus($"\uC774\uC0C1 \uBD84\uB958 \uD3C9\uAC00 summary \uBD88\uB7EC\uC624\uAE30 \uC644\uB8CC: {Path.GetFileName(selectedPath)}", isBusy: false);
                AppendLog($"Anomaly classification evaluation summary loaded: {selectedPath}");
                return;
            }

            ShellViewModel?.ClearModelCenterAnomalyEvaluationState();
            SetYoloCommandStatus("\uC774\uC0C1 \uBD84\uB958 \uD3C9\uAC00 summary\uB97C \uC77D\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4. JSON \uD30C\uC77C\uACFC \uD3C9\uAC00 \uACB0\uACFC\uB97C \uD655\uC778\uD558\uC138\uC694.", isBusy: false);
            AppendLog($"Anomaly classification evaluation summary load failed: {selectedPath}");
        }
        #endregion

    }
}
