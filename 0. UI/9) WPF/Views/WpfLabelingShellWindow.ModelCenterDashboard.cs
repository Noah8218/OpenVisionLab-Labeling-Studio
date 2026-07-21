using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
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
            WpfModelCenterDashboardState dashboardState = WpfModelCenterDashboardPresentationService.Build(
                settings,
                comparison,
                global.Data.ProjectSettings.TrainingGuide,
                global.Data.ProjectSettings.ModelRegistry,
                configuredWeightsPath,
                hasPendingModelSelection,
                isModelPromotionHeld);
            ShellViewModel?.SetModelCenterModelState(
                dashboardState.CurrentModelText,
                dashboardState.CandidateModelText,
                dashboardState.AdoptionText,
                dashboardState.NextActionText,
                dashboardState.ConfirmModelButtonText,
                dashboardState.ConfirmModelButtonToolTip,
                dashboardState.CanConfirmModel,
                dashboardState.DecisionSummaryText,
                dashboardState.DecisionEvidenceText,
                dashboardState.DecisionActionText,
                dashboardState.RuntimeActionText);
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

        private async void ExecuteRunAnomalyEvaluationCommand()
        {
            if (isAnomalyEvaluationRunning)
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
            WpfAnomalyClassificationEvaluationRunRequest request = anomalyClassificationEvaluationRunService.BuildRequest(global.Data);
            IReadOnlyList<string> validationErrors = anomalyClassificationEvaluationRunService.ValidateRequest(request);
            if (validationErrors.Count > 0)
            {
                string message = "\uC774\uC0C1 \uBD84\uB958 \uD3C9\uAC00 \uC2E4\uD589 \uBD88\uAC00: " + string.Join(" / ", validationErrors.Take(3));
                SetYoloCommandStatus(message, isBusy: false);
                AppendLog(message);
                return;
            }

            isAnomalyEvaluationRunning = true;
            UpdateYoloCommandButtons();
            SetYoloCommandStatus("\uC774\uC0C1 \uBD84\uB958 \uD3C9\uAC00 \uC2E4\uD589 \uC911...", isBusy: true);
            AppendLog($"Anomaly classification evaluation started: weights={Path.GetFileName(request.WeightsPath)}, dataset={request.DatasetRootPath}");

            try
            {
                WpfAnomalyClassificationEvaluationRunResult result = await anomalyClassificationEvaluationRunService
                    .RunAsync(request)
                    .ConfigureAwait(true);

                if (!result.Succeeded || string.IsNullOrWhiteSpace(result.SummaryPath))
                {
                    string errorText = BuildAnomalyEvaluationFailureText(result);
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
                string errorText = $"\uC774\uC0C1 \uBD84\uB958 \uD3C9\uAC00 \uC2E4\uD328: {ex.Message}";
                SetYoloCommandStatus(errorText, isBusy: false);
                AppendLog(errorText);
            }
            finally
            {
                isAnomalyEvaluationRunning = false;
                UpdateYoloCommandButtons();
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

        private static string BuildAnomalyEvaluationFailureText(WpfAnomalyClassificationEvaluationRunResult result)
        {
            string detail = result?.Error ?? string.Empty;
            if (string.IsNullOrWhiteSpace(detail))
            {
                detail = result?.Output ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(detail))
            {
                return "\uC774\uC0C1 \uBD84\uB958 \uD3C9\uAC00 \uC2E4\uD328: \uC2E4\uD589 \uACB0\uACFC\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.";
            }

            string firstLine = detail
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault() ?? detail.Trim();
            return $"\uC774\uC0C1 \uBD84\uB958 \uD3C9\uAC00 \uC2E4\uD328: {firstLine}";
        }

        private string ResolveModelCenterAnomalyEvaluationSummaryPath(string outputRootPath)
        {
            if (!string.IsNullOrWhiteSpace(manualModelCenterAnomalyEvaluationSummaryPath))
            {
                string manualPath = manualModelCenterAnomalyEvaluationSummaryPath.Trim();
                if (File.Exists(manualPath))
                {
                    return manualPath;
                }

                manualModelCenterAnomalyEvaluationSummaryPath = string.Empty;
            }

            return FindModelCenterAnomalyEvaluationSummaryPath(outputRootPath);
        }

        private bool TryApplyModelCenterAnomalyEvaluationSummary(string summaryPath)
        {
            if (string.IsNullOrWhiteSpace(summaryPath) || !File.Exists(summaryPath))
            {
                return false;
            }

            try
            {
                AnomalyClassificationEvaluationReport report = AnomalyClassificationEvaluationService.ReadSummaryFile(summaryPath);
                AnomalyClassificationEvaluationOptions options = ReadModelCenterAnomalyEvaluationOptions(summaryPath);
                ShellViewModel?.SetModelCenterAnomalyEvaluationState(
                    WpfAnomalyClassificationEvaluationPresentationService.Build(report, options));
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string FindModelCenterAnomalyEvaluationSummaryPath(string outputRootPath)
        {
            if (string.IsNullOrWhiteSpace(outputRootPath))
            {
                return string.Empty;
            }

            string root = outputRootPath.Trim();
            string directPath = Path.Combine(root, "classification-evaluation-summary.json");
            if (File.Exists(directPath))
            {
                return directPath;
            }

            string evaluationPath = Path.Combine(root, "classification-evaluation", "classification-evaluation-summary.json");
            if (File.Exists(evaluationPath))
            {
                return evaluationPath;
            }

            try
            {
                if (!Directory.Exists(root))
                {
                    return string.Empty;
                }

                return Directory
                    .EnumerateDirectories(root, "classification-evaluation-*", SearchOption.TopDirectoryOnly)
                    .Select(directory => new FileInfo(Path.Combine(directory, "classification-evaluation-summary.json")))
                    .Where(summary => summary.Exists)
                    .OrderByDescending(summary => summary.LastWriteTimeUtc)
                    .Select(summary => summary.FullName)
                    .FirstOrDefault() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static AnomalyClassificationEvaluationOptions ReadModelCenterAnomalyEvaluationOptions(string summaryPath)
        {
            var options = new AnomalyClassificationEvaluationOptions();
            try
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(summaryPath));
                if (!document.RootElement.TryGetProperty("thresholds", out JsonElement thresholds)
                    || thresholds.ValueKind != JsonValueKind.Object)
                {
                    return options;
                }

                options.MinimumTotalImageCount = ReadModelCenterThresholdInt(
                    thresholds,
                    "minimumTotalImageCount",
                    options.MinimumTotalImageCount);
                options.MinimumPerClassImageCount = ReadModelCenterThresholdInt(
                    thresholds,
                    "minimumPerClassImageCount",
                    options.MinimumPerClassImageCount);
                options.MinimumAccuracy = ReadModelCenterThresholdDouble(
                    thresholds,
                    "minimumAccuracy",
                    options.MinimumAccuracy);
                options.MinimumPerClassAccuracy = ReadModelCenterThresholdDouble(
                    thresholds,
                    "minimumPerClassAccuracy",
                    options.MinimumPerClassAccuracy);
                options.MinimumConfidence = ReadModelCenterThresholdDouble(
                    thresholds,
                    "minimumConfidence",
                    options.MinimumConfidence);
            }
            catch
            {
                return options;
            }

            return options;
        }

        private static int ReadModelCenterThresholdInt(JsonElement thresholds, string propertyName, int fallback)
        {
            return thresholds.TryGetProperty(propertyName, out JsonElement value)
                && value.ValueKind == JsonValueKind.Number
                && value.TryGetInt32(out int result)
                ? result
                : fallback;
        }

        private static double ReadModelCenterThresholdDouble(JsonElement thresholds, string propertyName, double fallback)
        {
            return thresholds.TryGetProperty(propertyName, out JsonElement value)
                && value.ValueKind == JsonValueKind.Number
                && value.TryGetDouble(out double result)
                ? Math.Clamp(result, 0D, 1D)
                : fallback;
        }
    }
}
