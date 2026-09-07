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

            PythonModelSettings settings = null;
            string candidateWeightsPath = string.Empty;
            string baselineWeightsPath = string.Empty;
            bool candidateDecisionCommitted = false;
            try
            {
                EnsureProjectSettings();
                settings = global.Data.ProjectSettings.PythonModel;
                candidateWeightsPath = settings.WeightsPath?.Trim() ?? string.Empty;
                baselineWeightsPath = pendingTrainingBaselineWeightsPath?.Trim() ?? string.Empty;

                if (!hasPendingTrainingWeightsRecipeSave || string.IsNullOrWhiteSpace(candidateWeightsPath))
                {
                    SetYoloCommandStatus(ModelCandidateDecisionPresentationService.BuildNoRejectCandidateStatus(), isBusy: false);
                    UpdateCandidateModelDecisionPanel();
                    return;
                }

                using RecipeSettingsStateTransaction recipeSettingsTransaction = new RecipeSettingsStateTransaction(global.Data);
                using ModelRegistryStateTransaction registryTransaction = new ModelRegistryStateTransaction(global.Data.ProjectSettings.ModelRegistry);
                WpfTrainingWeightsComparison comparison = BuildCurrentTrainingWeightsComparison();
                string decisionSummary = ModelCandidateDecisionPresentationService.BuildRejectDecisionSummary();
                ModelRegistryService.RecordCandidateDecision(
                    global.Data.ProjectSettings.ModelRegistry,
                    settings,
                    global.Data.ProjectSettings.DatasetPurpose,
                    global.Data.OutputRootPath,
                    candidateWeightsPath,
                    baselineWeightsPath,
                    TrainingComparisonPresentationService.BuildComparisonStatusText(comparison),
                    ModelRegistryService.CandidateDecisionRejected,
                    decisionSummary,
                    savedToRecipe: false,
                    datasetVersionId: global.Data.ProjectSettings.TrainingGuide.LastTrainingDatasetVersionId,
                    datasetContentSha256: global.Data.ProjectSettings.TrainingGuide.LastTrainingDatasetContentSha256);

                if (!string.IsNullOrWhiteSpace(baselineWeightsPath) && File.Exists(baselineWeightsPath))
                {
                    settings.WeightsPath = baselineWeightsPath;
                    YoloModelSettingsViewModel?.LoadFrom(settings);
                }

                bool configSaved = SaveModelMetadataConfigFromPanel();
                if (configSaved)
                {
                    recipeSettingsTransaction.Commit();
                    registryTransaction.Commit();
                    hasPendingTrainingWeightsRecipeSave = false;
                    pendingTrainingBaselineWeightsPath = string.Empty;
                    candidateDecisionCommitted = true;
                }
                else
                {
                    recipeSettingsTransaction.Rollback();
                    registryTransaction.Rollback();
                    RestorePendingCandidateModelState(settings, candidateWeightsPath, baselineWeightsPath);
                }

                PopulateYoloEditorFields();
                RefreshYoloStatus();
                UpdateYoloTrainingHistoryText();
                RefreshModelCenterDashboard();

                SetYoloCommandStatus(ModelCandidateDecisionPresentationService.BuildRejectCommandStatus(candidateWeightsPath, configSaved), isBusy: false);
                SetProjectConfigStatus(ModelCandidateDecisionPresentationService.BuildRejectProjectConfigStatus(configSaved));
                AppendLog(ModelCandidateDecisionPresentationService.BuildRejectLog(candidateWeightsPath, baselineWeightsPath));
            }
            catch (Exception ex)
            {
                if (!candidateDecisionCommitted)
                {
                    RestorePendingCandidateModelState(settings, candidateWeightsPath, baselineWeightsPath);
                }

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

            PythonModelSettings settings = null;
            string candidateWeightsPath = string.Empty;
            string baselineWeightsPath = string.Empty;
            bool adoptionCommitted = false;
            try
            {
                EnsureProjectSettings();
                WpfModelRegistryHistoryItem selected = ShellViewModel?.SelectedModelRegistryHistoryItem;
                settings = global.Data.ProjectSettings.PythonModel;
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

                candidateWeightsPath = plan.CandidateWeightsPath;
                if (plan.IsAlreadyCurrent)
                {
                    SetYoloCommandStatus($"\uC774\uBBF8 \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378\uC785\uB2C8\uB2E4: {Path.GetFileName(candidateWeightsPath)}", isBusy: false);
                    RefreshModelCenterDashboard();
                    return;
                }

                baselineWeightsPath = plan.BaselineWeightsPath;
                string decisionSummary = plan.DecisionSummary;
                string metricsSummary = plan.MetricsSummary;
                using RecipeSettingsStateTransaction recipeSettingsTransaction = new RecipeSettingsStateTransaction(global.Data);
                using ModelRegistryStateTransaction registryTransaction = new ModelRegistryStateTransaction(global.Data.ProjectSettings.ModelRegistry);

                settings.WeightsPath = candidateWeightsPath;
                YoloModelSettingsViewModel?.LoadFrom(settings);
                ModelRegistryService.RecordCandidateDecision(
                    global.Data.ProjectSettings.ModelRegistry,
                    settings,
                    global.Data.ProjectSettings.DatasetPurpose,
                    global.Data.OutputRootPath,
                    candidateWeightsPath,
                    baselineWeightsPath,
                    metricsSummary,
                    ModelRegistryService.CandidateDecisionAdopted,
                    decisionSummary,
                    savedToRecipe: true,
                    datasetVersionId: global.Data.ProjectSettings.TrainingGuide.LastTrainingDatasetVersionId,
                    datasetContentSha256: global.Data.ProjectSettings.TrainingGuide.LastTrainingDatasetContentSha256);

                // Model adoption changes Recipe model metadata, not dataset content.
                // Keep the existing dataset version manifest untouched for this save.
                bool configSaved = SaveModelMetadataConfigFromPanel();
                if (configSaved)
                {
                    recipeSettingsTransaction.Commit();
                    registryTransaction.Commit();
                    adoptionCommitted = true;
                    hasPendingTrainingWeightsRecipeSave = false;
                    pendingTrainingBaselineWeightsPath = string.Empty;
                    lastAutoAppliedTrainingWeightsPath = candidateWeightsPath;
                }
                else
                {
                    recipeSettingsTransaction.Rollback();
                    registryTransaction.Rollback();
                    RestorePendingCandidateModelState(settings, candidateWeightsPath, baselineWeightsPath);
                }

                PopulateYoloEditorFields();
                RefreshYoloStatus();
                UpdateYoloTrainingHistoryText();
                RefreshModelCenterDashboard();

                string modelName = Path.GetFileName(candidateWeightsPath);
                if (configSaved)
                {
                    ShellViewModel?.ClearModelCenterRecoveryState();
                    SetModelStatus($"\uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378: {modelName}");
                    SetYoloCommandStatus($"\uBAA8\uB378 \uC774\uB825 \uC801\uC6A9 \uC644\uB8CC: {modelName}. \uB2E4\uC74C \uAC80\uC0AC\uBD80\uD130 \uC774 \uBAA8\uB378\uC744 \uC0AC\uC6A9\uD569\uB2C8\uB2E4.", isBusy: false);
                    AppendLog($"Model history adopted as inspection model: {candidateWeightsPath} / previous={baselineWeightsPath}");
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

                RestorePendingCandidateModelState(settings, candidateWeightsPath, baselineWeightsPath);

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
        private string manualModelCenterAnomalyEvaluationSummaryPath = string.Empty;

        private void RefreshModelCenterDashboard(
            WpfTrainingWeightsComparison comparison = null,
            string configuredWeightsPathOverride = null,
            bool pendingManualWeightsSelection = false)
        {
            EnsureProjectSettings();
            PythonModelSettings settings = global.Data.ProjectSettings.PythonModel;
            string configuredWeightsPath = configuredWeightsPathOverride ?? settings.WeightsPath ?? string.Empty;
            comparison ??= trainingWeightsService.BuildComparison(
                settings.ProjectRootPath,
                global.Data.OutputRootPath,
                GetTrainingComparisonCurrentWeightsPath(configuredWeightsPath));

            bool hasPendingModelSelection = pendingManualWeightsSelection || hasPendingTrainingWeightsRecipeSave;
            bool isModelPromotionHeld = CandidateReviewViewModel?.IsModelPromotionHeld == true;
                ModelCenterDashboardState dashboardState = ModelCenterDashboardPresentationService.Build(
                settings,
                comparison,
                global.Data.ProjectSettings.TrainingGuide,
                global.Data.ProjectSettings.ModelRegistry,
                configuredWeightsPath,
                hasPendingModelSelection,
                isModelPromotionHeld);
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
            if (isApplicationCloseApproved || isAnomalyEvaluationRunning)
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
            AnomalyClassificationEvaluationRunRequest request = anomalyClassificationEvaluationRunService.BuildRequest(global.Data);
            IReadOnlyList<string> validationErrors = anomalyClassificationEvaluationRunService.ValidateRequest(request);
            if (validationErrors.Count > 0)
            {
                string message = "\uC774\uC0C1 \uBD84\uB958 \uD3C9\uAC00 \uC2E4\uD589 \uBD88\uAC00: " + string.Join(" / ", validationErrors.Take(3));
                SetYoloCommandStatus(message, isBusy: false);
                AppendLog(message);
                return;
            }

            isAnomalyEvaluationRunning = true;
            anomalyEvaluationCts?.Cancel();
            anomalyEvaluationCts?.Dispose();
            anomalyEvaluationCts = new CancellationTokenSource();
            CancellationToken anomalyEvaluationToken = anomalyEvaluationCts.Token;
            UpdateYoloCommandButtons();
            SetYoloCommandStatus("\uC774\uC0C1 \uBD84\uB958 \uD3C9\uAC00 \uC2E4\uD589 \uC911...", isBusy: true);
            AppendLog($"Anomaly classification evaluation started: weights={Path.GetFileName(request.WeightsPath)}, dataset={request.DatasetRootPath}");

            try
            {
                AnomalyClassificationEvaluationRunResult result = await anomalyClassificationEvaluationRunService
                    .RunAsync(request, anomalyEvaluationToken)
                    .ConfigureAwait(true);
                if (isApplicationCloseApproved)
                {
                    return;
                }

                if (!result.Succeeded || string.IsNullOrWhiteSpace(result.SummaryPath))
                {
                    string errorText = AnomalyEvaluationFailurePresentationService.Build(result);
                    SetYoloCommandStatus(errorText, isBusy: false);
                    AppendLog(errorText);
                    return;
                }

                if (!TryApplyModelCenterAnomalyEvaluationSummary(result.SummaryPath))
                {
                    string errorText = "\uC774\uC0C1 \uBD84\uB958 \uD3C9\uAC00 summary\uB97C \uC77D\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4. \uC0DD\uC131\uB41C JSON\uC744 \uD655\uC778\uD558\uC138\uC694.";
                    SetYoloCommandStatus(errorText, isBusy: false);
                    AppendLog($"{errorText} {result.SummaryPath}");
                    return;
                }

                manualModelCenterAnomalyEvaluationSummaryPath = result.SummaryPath;
                string summaryName = Path.GetFileName(Path.GetDirectoryName(result.SummaryPath) ?? result.SummaryPath);
                string completeText = $"\uC774\uC0C1 \uBD84\uB958 \uD3C9\uAC00 \uC644\uB8CC: {summaryName}";
                RefreshModelCenterDashboard();
                SetYoloCommandStatus(completeText, isBusy: false);
                AppendLog($"{completeText}: {result.SummaryPath}");
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
                anomalyEvaluationCts?.Dispose();
                anomalyEvaluationCts = null;
                isAnomalyEvaluationRunning = false;
                if (!isApplicationCloseApproved)
                {
                    UpdateYoloCommandButtons();
                }
            }
        }

        private void RefreshModelCenterAnomalyEvaluationState()
        {
            if (global.Data?.ProjectSettings?.DatasetPurpose != LabelingDatasetPurpose.AnomalyDetection)
            {
                manualModelCenterAnomalyEvaluationSummaryPath = string.Empty;
                ShellViewModel?.SetModelCenterAnomalyEvaluationPickerVisible(false);
                ShellViewModel?.ClearModelCenterAnomalyEvaluationState();
                return;
            }

            ShellViewModel?.SetModelCenterAnomalyEvaluationPickerVisible(true);
            string summaryPath = ResolveModelCenterAnomalyEvaluationSummaryPath(global.Data.OutputRootPath);
            if (string.IsNullOrWhiteSpace(summaryPath))
            {
                ShellViewModel?.ClearModelCenterAnomalyEvaluationState();
                return;
            }

            if (!TryApplyModelCenterAnomalyEvaluationSummary(summaryPath))
            {
                manualModelCenterAnomalyEvaluationSummaryPath = string.Empty;
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

            string initialPath = !string.IsNullOrWhiteSpace(manualModelCenterAnomalyEvaluationSummaryPath)
                ? manualModelCenterAnomalyEvaluationSummaryPath
                : ResolveModelCenterAnomalyEvaluationSummaryPath(global.Data.OutputRootPath);
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

            if (TryApplyModelCenterAnomalyEvaluationSummary(selectedPath))
            {
                manualModelCenterAnomalyEvaluationSummaryPath = selectedPath;
                SetYoloCommandStatus($"\uC774\uC0C1 \uBD84\uB958 \uD3C9\uAC00 summary \uBD88\uB7EC\uC624\uAE30 \uC644\uB8CC: {Path.GetFileName(selectedPath)}", isBusy: false);
                AppendLog($"Anomaly classification evaluation summary loaded: {selectedPath}");
                return;
            }

            manualModelCenterAnomalyEvaluationSummaryPath = string.Empty;
            ShellViewModel?.ClearModelCenterAnomalyEvaluationState();
            SetYoloCommandStatus("\uC774\uC0C1 \uBD84\uB958 \uD3C9\uAC00 summary\uB97C \uC77D\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4. JSON \uD30C\uC77C\uACFC \uD3C9\uAC00 \uACB0\uACFC\uB97C \uD655\uC778\uD558\uC138\uC694.", isBusy: false);
            AppendLog($"Anomaly classification evaluation summary load failed: {selectedPath}");
        }

        private string ResolveModelCenterAnomalyEvaluationSummaryPath(string outputRootPath)
        {
            string preferredPath = manualModelCenterAnomalyEvaluationSummaryPath;
            string resolvedPath = anomalyClassificationEvaluationSummaryService.ResolveSummaryPath(
                outputRootPath,
                preferredPath);
            if (!string.IsNullOrWhiteSpace(preferredPath)
                && !string.Equals(resolvedPath, preferredPath.Trim(), StringComparison.Ordinal))
            {
                manualModelCenterAnomalyEvaluationSummaryPath = string.Empty;
            }

            return resolvedPath;
        }

        private bool TryApplyModelCenterAnomalyEvaluationSummary(string summaryPath)
        {
            if (!anomalyClassificationEvaluationSummaryService.TryReadSummary(
                    summaryPath,
                    out AnomalyClassificationEvaluationSummary summary))
            {
                return false;
            }

            ShellViewModel?.SetModelCenterAnomalyEvaluationState(
                AnomalyClassificationEvaluationPresentationService.Build(
                    summary.Report,
                    summary.Options));
            return true;
        }
        #endregion

    }
}
