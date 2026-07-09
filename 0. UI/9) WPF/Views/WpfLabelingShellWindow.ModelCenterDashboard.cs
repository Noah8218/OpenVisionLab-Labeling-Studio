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
            WpfModelRegistryPresentation registryPresentation = WpfModelRegistryPresentationService.Build(
                settings,
                comparison,
                global.Data.ProjectSettings.TrainingGuide,
                global.Data.ProjectSettings.ModelRegistry,
                hasPendingModelSelection);
            string currentModelText = BuildModelCenterCurrentModelText(settings, configuredWeightsPath, hasPendingModelSelection);
            string candidateModelText = BuildModelCenterCandidateModelText(comparison, configuredWeightsPath, hasPendingModelSelection);
            string adoptionText = BuildModelCenterAdoptionText(comparison, configuredWeightsPath, hasPendingModelSelection);
            string nextActionText = BuildModelCenterNextActionText(comparison, configuredWeightsPath, hasPendingModelSelection);
            string reviewCandidateButtonText = BuildModelCenterReviewCandidateButtonText(comparison, configuredWeightsPath, hasPendingModelSelection);
            string reviewCandidateButtonToolTip = BuildModelCenterReviewCandidateButtonToolTip(comparison, configuredWeightsPath, hasPendingModelSelection);
            bool canReviewCandidate = CanReviewModelCandidate(comparison, configuredWeightsPath, hasPendingModelSelection);
            string confirmModelButtonText = BuildModelCenterConfirmModelButtonText(comparison, configuredWeightsPath, hasPendingModelSelection);
            string confirmModelButtonToolTip = BuildModelCenterConfirmModelButtonToolTip(comparison, configuredWeightsPath, hasPendingModelSelection);
            string runtimeActionText = WpfModelRegistryPresentationService.BuildSelectedRuntimeSummaryText(settings);
            ShellViewModel?.SetModelCenterModelState(
                currentModelText,
                candidateModelText,
                adoptionText,
                nextActionText,
                confirmModelButtonText,
                confirmModelButtonToolTip,
                hasPendingModelSelection,
                BuildModelCenterDecisionSummaryText(comparison, configuredWeightsPath, hasPendingModelSelection),
                BuildModelCenterDecisionEvidenceText(comparison, configuredWeightsPath, hasPendingModelSelection),
                BuildModelCenterDecisionActionText(comparison, configuredWeightsPath, hasPendingModelSelection),
                runtimeActionText);
            LearningWorkflowViewModel?.SetTrainingModelLifecycleState(
                currentModelText,
                candidateModelText,
                adoptionText,
                nextActionText);
            TrainingSettingsViewModel?.SetPostTrainingModelActionState(
                currentModelText,
                candidateModelText,
                adoptionText,
                nextActionText,
                reviewCandidateButtonText,
                reviewCandidateButtonToolTip,
                canReviewCandidate,
                confirmModelButtonText,
                confirmModelButtonToolTip,
                hasPendingModelSelection);
            ShellViewModel?.SetModelCenterCandidateReviewState(
                reviewCandidateButtonText,
                reviewCandidateButtonToolTip,
                canReviewCandidate);
            ShellViewModel?.SetModelRegistryState(registryPresentation);
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

        private static string BuildModelCenterCurrentModelText(
            PythonModelSettings settings,
            string configuredWeightsPath,
            bool hasPendingModelSelection)
        {
            string trimmedPath = configuredWeightsPath?.Trim() ?? string.Empty;
            string runtimeSummaryText = WpfModelRegistryPresentationService.BuildSelectedRuntimeSummaryText(settings);
            if (string.IsNullOrWhiteSpace(trimmedPath))
            {
                return $"\uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378: \uC5C6\uC74C / \uC2E4\uD589\uAE30 {runtimeSummaryText}";
            }

            string displayPath = FormatModelCenterPath(trimmedPath);
            if (!File.Exists(trimmedPath))
            {
                return $"\uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378: {displayPath} / \uC2E4\uD589\uAE30 {runtimeSummaryText} / \uD30C\uC77C \uC5C6\uC74C";
            }

            return hasPendingModelSelection
                ? $"\uAC80\uC0AC \uBAA8\uB378 \uD6C4\uBCF4: {displayPath} / \uC2E4\uD589\uAE30 {runtimeSummaryText} / \uC124\uC815 \uC800\uC7A5 \uD544\uC694"
                : $"\uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378: {displayPath} / \uC2E4\uD589\uAE30 {runtimeSummaryText}";
        }

        private static string BuildModelCenterCandidateModelText(
            WpfTrainingWeightsComparison comparison,
            string configuredWeightsPath,
            bool hasPendingModelSelection)
        {
            if (comparison?.HasLatestWeights != true)
            {
                return "\uC0C8 \uD559\uC2B5 \uBAA8\uB378 \uD6C4\uBCF4: \uC5C6\uC74C";
            }

            string latestWeightsPath = comparison.LatestWeightsPath?.Trim() ?? string.Empty;
            string currentWeightsPath = configuredWeightsPath?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(currentWeightsPath)
                && string.Equals(latestWeightsPath, currentWeightsPath, StringComparison.OrdinalIgnoreCase)
                && !hasPendingModelSelection)
            {
                return "\uC0C8 \uD559\uC2B5 \uBAA8\uB378 \uD6C4\uBCF4: \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378\uACFC \uAC19\uC74C";
            }

            string compactMetricsText = WpfModelRegistryPresentationService.BuildCompactMetricSummary(comparison.MetricsStatusText);
            string suffix = string.IsNullOrWhiteSpace(compactMetricsText)
                ? string.Empty
                : $" / {compactMetricsText}";
            return $"\uC0C8 \uD559\uC2B5 \uBAA8\uB378 \uD6C4\uBCF4: {FormatModelCenterPath(latestWeightsPath)}{suffix}";
        }

        private static string BuildModelCenterAdoptionText(
            WpfTrainingWeightsComparison comparison,
            string configuredWeightsPath,
            bool hasPendingModelSelection)
        {
            if (hasPendingModelSelection)
            {
                return "\uBAA8\uB378 \uC801\uC6A9: \uD6C4\uBCF4 \uC120\uD0DD\uB428 - \uC124\uC815 \uC800\uC7A5 \uD544\uC694";
            }

            if (comparison?.HasLatestWeights != true)
            {
                return "\uBAA8\uB378 \uC801\uC6A9: \uD559\uC2B5 \uACB0\uACFC \uC5C6\uC74C";
            }

            string latestWeightsPath = comparison.LatestWeightsPath?.Trim() ?? string.Empty;
            string currentWeightsPath = configuredWeightsPath?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(currentWeightsPath)
                && string.Equals(latestWeightsPath, currentWeightsPath, StringComparison.OrdinalIgnoreCase))
            {
                return "\uBAA8\uB378 \uC801\uC6A9: \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378\uB85C \uC0AC\uC6A9 \uC911";
            }

            return comparison.ShouldApplyLatest
                ? "\uBAA8\uB378 \uC801\uC6A9: \uC0C8 \uD6C4\uBCF4 \uAC80\uD1A0 \uD544\uC694"
                : "\uBAA8\uB378 \uC801\uC6A9: \uD604\uC7AC \uBAA8\uB378 \uC720\uC9C0 \uAD8C\uC7A5";
        }

        private static string BuildModelCenterNextActionText(
            WpfTrainingWeightsComparison comparison,
            string configuredWeightsPath,
            bool hasPendingModelSelection)
        {
            if (hasPendingModelSelection)
            {
                return "\uB2E4\uC74C: \uAC80\uC0AC \uBAA8\uB378\uB85C \uC800\uC7A5\uC744 \uB20C\uB7EC recipe\uC5D0 \uC800\uC7A5\uD558\uC138\uC694. \uB2E4\uC74C \uCD94\uB860\uBD80\uD130 \uC774 \uBAA8\uB378\uC744 \uC0AC\uC6A9\uD569\uB2C8\uB2E4.";
            }

            string currentWeightsPath = configuredWeightsPath?.Trim() ?? string.Empty;
            if (comparison?.HasLatestWeights != true)
            {
                return string.IsNullOrWhiteSpace(currentWeightsPath)
                    ? "\uB2E4\uC74C: \uB370\uC774\uD130\uC14B \uC810\uAC80 \uD6C4 \uD559\uC2B5\uC744 \uC2DC\uC791\uD558\uC138\uC694."
                    : "\uB2E4\uC74C: \uD544\uC694\uD558\uBA74 \uC0C8 \uD559\uC2B5\uC744 \uC2DC\uC791\uD558\uAC70\uB098 \uD604\uC7AC \uBAA8\uB378\uB85C \uAC80\uC0AC\uD558\uC138\uC694.";
            }

            string latestWeightsPath = comparison.LatestWeightsPath?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(currentWeightsPath)
                && string.Equals(latestWeightsPath, currentWeightsPath, StringComparison.OrdinalIgnoreCase))
            {
                return "\uB2E4\uC74C: \uD604\uC7AC \uAC80\uC0AC \uBC84\uD2BC\uC73C\uB85C \uCD94\uB860 \uAC80\uD1A0\uB97C \uC9C4\uD589\uD558\uC138\uC694.";
            }

            return "\uB2E4\uC74C: \uD6C4\uBCF4 \uBAA8\uB378\uC758 \uCD5C\uC885 \uAC80\uC99D \uACB0\uACFC\uB97C \uBE44\uAD50\uD55C \uB4A4 \uC800\uC7A5\uD558\uC138\uC694.";
        }

        private static string BuildModelCenterConfirmModelButtonText(
            WpfTrainingWeightsComparison comparison,
            string configuredWeightsPath,
            bool hasPendingModelSelection)
        {
            if (hasPendingModelSelection)
            {
                return "\uAC80\uC0AC \uBAA8\uB378\uB85C \uC800\uC7A5";
            }

            if (IsLatestWeightsCurrent(comparison, configuredWeightsPath))
            {
                return "\uC801\uC6A9 \uC644\uB8CC";
            }

            if (comparison?.HasLatestWeights == true)
            {
                return "\uD6C4\uBCF4 \uAC80\uD1A0 \uD544\uC694";
            }

            return "\uD6C4\uBCF4 \uC5C6\uC74C";
        }

        private static string BuildModelCenterDecisionSummaryText(
            WpfTrainingWeightsComparison comparison,
            string configuredWeightsPath,
            bool hasPendingModelSelection)
        {
            if (hasPendingModelSelection)
            {
                return "\uD310\uB2E8: \uAC80\uC0AC \uBAA8\uB378 \uD6C4\uBCF4\uAC00 \uC120\uD0DD\uB428";
            }

            if (comparison?.HasLatestWeights != true)
            {
                return "\uD310\uB2E8: \uC801\uC6A9\uD560 \uD559\uC2B5 \uACB0\uACFC\uAC00 \uC5C6\uC74C";
            }

            if (IsLatestWeightsCurrent(comparison, configuredWeightsPath))
            {
                return "\uD310\uB2E8: \uC774\uBBF8 \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378";
            }

            if (comparison.ShouldApplyLatest)
            {
                return "\uD310\uB2E8: \uC0C8 \uBAA8\uB378 \uD6C4\uBCF4 \uAC80\uC99D \uD6C4 \uC801\uC6A9 \uAC00\uB2A5";
            }

            return "\uD310\uB2E8: \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378 \uC720\uC9C0 \uAD8C\uC7A5";
        }

        private static string BuildModelCenterDecisionEvidenceText(
            WpfTrainingWeightsComparison comparison,
            string configuredWeightsPath,
            bool hasPendingModelSelection)
        {
            string currentModel = FormatModelCenterPath(configuredWeightsPath);
            if (string.IsNullOrWhiteSpace(currentModel))
            {
                currentModel = "\uC5C6\uC74C";
            }

            if (comparison?.HasLatestWeights != true)
            {
                return $"\uADFC\uAC70: \uAC80\uC0AC \uBAA8\uB378 {currentModel} / \uD559\uC2B5 \uACB0\uACFC \uC5C6\uC74C";
            }

            string candidateModel = FormatModelCenterPath(comparison.LatestWeightsPath);
            string metrics = string.IsNullOrWhiteSpace(comparison.MetricsStatusText)
                ? comparison.StatusText
                : comparison.MetricsStatusText;
            if (string.IsNullOrWhiteSpace(metrics))
            {
                metrics = "\uCD5C\uC885 \uAC80\uC99D \uBE44\uAD50 \uD544\uC694";
            }

            string metricsEvidence = EnsureModelCenterNonFailureEvidence(metrics);
            if (hasPendingModelSelection)
            {
                return $"\uADFC\uAC70: \uC120\uD0DD \uD6C4\uBCF4 {currentModel} / \uD559\uC2B5 \uD6C4\uBCF4 {candidateModel} / {metricsEvidence}";
            }

            return $"\uADFC\uAC70: \uAC80\uC0AC \uBAA8\uB378 {currentModel} / \uD559\uC2B5 \uD6C4\uBCF4 {candidateModel} / {metricsEvidence}";
        }

        private static string EnsureModelCenterNonFailureEvidence(string metricsText)
        {
            string text = metricsText?.Trim() ?? string.Empty;
            if (text.Contains("\uC2E4\uD328 \uC544\uB2D8", StringComparison.Ordinal))
            {
                return text;
            }

            return string.IsNullOrWhiteSpace(text)
                ? "\uD559\uC2B5 \uC2E4\uD328 \uC544\uB2D8"
                : $"{text} / \uD559\uC2B5 \uC2E4\uD328 \uC544\uB2D8";
        }

        private static string BuildModelCenterDecisionActionText(
            WpfTrainingWeightsComparison comparison,
            string configuredWeightsPath,
            bool hasPendingModelSelection)
        {
            if (hasPendingModelSelection)
            {
                return "\uC800\uC7A5: \uAC80\uC0AC \uBAA8\uB378\uB85C \uC800\uC7A5 \uBC84\uD2BC\uC73C\uB85C recipe\uC5D0 \uC800\uC7A5, \uB2E4\uC74C \uCD94\uB860\uBD80\uD130 \uC0AC\uC6A9";
            }

            if (comparison?.HasLatestWeights != true)
            {
                return "\uB2E4\uC74C: \uB370\uC774\uD130\uC14B \uC810\uAC80 \uD6C4 \uD559\uC2B5 \uC2DC\uC791";
            }

            if (IsLatestWeightsCurrent(comparison, configuredWeightsPath))
            {
                return "\uB2E4\uC74C: \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378\uB85C \uCD94\uB860 \uAC80\uD1A0";
            }

            return comparison.ShouldApplyLatest
                ? "\uC800\uC7A5: \uD6C4\uBCF4 \uAC80\uC99D\uC73C\uB85C \uC608\uC2DC \uD655\uC778 \uD6C4 \uAC80\uC0AC \uBAA8\uB378\uB85C \uC800\uC7A5"
                : "\uD310\uB2E8: \uD604\uC7AC \uBAA8\uB378 \uC720\uC9C0, \uD544\uC694 \uC2DC \uD6C4\uBCF4 \uAC80\uC99D\uB9CC \uD655\uC778";
        }

        private static string BuildModelCenterConfirmModelButtonToolTip(
            WpfTrainingWeightsComparison comparison,
            string configuredWeightsPath,
            bool hasPendingModelSelection)
        {
            if (hasPendingModelSelection)
            {
                return "\uC120\uD0DD\uD55C \uBAA8\uB378\uC744 recipe\uC5D0 \uC800\uC7A5\uD558\uACE0 \uB2E4\uC74C \uCD94\uB860\uBD80\uD130 \uAC80\uC0AC \uBAA8\uB378\uB85C \uC0AC\uC6A9\uD569\uB2C8\uB2E4.";
            }

            if (IsLatestWeightsCurrent(comparison, configuredWeightsPath))
            {
                return "\uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378\uACFC \uD559\uC2B5 \uACB0\uACFC \uBAA8\uB378\uC774 \uAC19\uC2B5\uB2C8\uB2E4.";
            }

            if (comparison?.HasLatestWeights == true)
            {
                return "\uBA3C\uC800 \uD6C4\uBCF4 \uBAA8\uB378\uC744 \uAC80\uD1A0\uD558\uAC70\uB098 \uC120\uD0DD\uD558\uC138\uC694.";
            }

            return "\uD655\uC815\uD560 \uD559\uC2B5 \uACB0\uACFC \uBAA8\uB378\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.";
        }

        private static string BuildModelCenterReviewCandidateButtonText(
            WpfTrainingWeightsComparison comparison,
            string configuredWeightsPath,
            bool hasPendingModelSelection)
        {
            if (CanReviewModelCandidate(comparison, configuredWeightsPath, hasPendingModelSelection))
            {
                return "\uD6C4\uBCF4 \uAC80\uC99D";
            }

            if (IsLatestWeightsCurrent(comparison, configuredWeightsPath))
            {
                return "\uC801\uC6A9 \uC644\uB8CC";
            }

            return "\uD6C4\uBCF4 \uC5C6\uC74C";
        }

        private static string BuildModelCenterReviewCandidateButtonToolTip(
            WpfTrainingWeightsComparison comparison,
            string configuredWeightsPath,
            bool hasPendingModelSelection)
        {
            if (CanReviewModelCandidate(comparison, configuredWeightsPath, hasPendingModelSelection))
            {
                return "\uD559\uC2B5 \uD6C4\uBCF4 \uBAA8\uB378\uC744 \uD6C4\uBCF4 \uAC80\uD1A0 \uD0ED\uC5D0\uC11C \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378\uACFC \uBE44\uAD50\uD569\uB2C8\uB2E4. \uAC80\uCD9C \uC2E4\uD589\uC740 \uD604\uC7AC \uAC80\uC0AC \uBC84\uD2BC\uC5D0\uC11C \uD604\uC7AC \uC774\uBBF8\uC9C0\uB85C \uC9C4\uD589\uD569\uB2C8\uB2E4.";
            }

            if (IsLatestWeightsCurrent(comparison, configuredWeightsPath))
            {
                return "\uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378\uACFC \uD559\uC2B5 \uACB0\uACFC \uBAA8\uB378\uC774 \uAC19\uC544 \uAC80\uC99D\uD560 \uD6C4\uBCF4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.";
            }

            return "\uAC80\uD1A0\uD560 \uD559\uC2B5 \uACB0\uACFC \uBAA8\uB378\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.";
        }

        private static bool CanReviewModelCandidate(
            WpfTrainingWeightsComparison comparison,
            string configuredWeightsPath,
            bool hasPendingModelSelection)
        {
            if (comparison?.HasLatestWeights != true)
            {
                return false;
            }

            return hasPendingModelSelection || !IsLatestWeightsCurrent(comparison, configuredWeightsPath);
        }

        private static bool IsLatestWeightsCurrent(WpfTrainingWeightsComparison comparison, string configuredWeightsPath)
        {
            string latestWeightsPath = comparison?.LatestWeightsPath?.Trim() ?? string.Empty;
            string currentWeightsPath = configuredWeightsPath?.Trim() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(latestWeightsPath)
                && !string.IsNullOrWhiteSpace(currentWeightsPath)
                && string.Equals(latestWeightsPath, currentWeightsPath, StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatModelCenterPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            string displayPath = WpfTrainingWeightsService.FormatWeightsDisplayPath(path);
            return string.IsNullOrWhiteSpace(displayPath)
                ? Path.GetFileName(path.Trim())
                : displayPath;
        }
    }
}
