using OpenVisionLab;
using System;
using System.Linq;
using MvcVisionSystem._1._Core;
using System.IO;
using MvcVisionSystem.Yolo;

namespace MvcVisionSystem
{
    public static class ModelCenterDashboardPresentationService
    {
        private static string ModelCenterActionRouteText => OpenVisionLanguageService.T("WpfModelCenterDashboard.Action.Route");

        public static ModelCenterDashboardState Build(
            PythonModelSettings settings,
            WpfTrainingWeightsComparison comparison,
            YoloTrainingGuideHistory trainingGuide,
            ModelRegistrySettings modelRegistry,
            string configuredWeightsPath,
            bool hasPendingModelSelection,
            bool isModelPromotionHeld)
            => Build(settings, comparison, trainingGuide, modelRegistry, configuredWeightsPath, hasPendingModelSelection, isModelPromotionHeld, null);

        public static ModelCenterDashboardState Build(
            PythonModelSettings settings,
            WpfTrainingWeightsComparison comparison,
            YoloTrainingGuideHistory trainingGuide,
            ModelRegistrySettings modelRegistry,
            string configuredWeightsPath,
            bool hasPendingModelSelection,
            bool isModelPromotionHeld,
            PythonModelRuntimeState runtimeState)
        {
            string confirmModelButtonToolTip = BuildConfirmModelButtonToolTip(
                comparison,
                configuredWeightsPath,
                hasPendingModelSelection);
            if (isModelPromotionHeld)
            {
                confirmModelButtonToolTip = ModelCandidateDecisionPresentationService.BuildHeldCandidateSaveBlockedStatus();
            }

            string currentModelText = BuildCurrentModelText(settings, configuredWeightsPath, hasPendingModelSelection, runtimeState);
            string candidateModelText = BuildCandidateModelText(comparison, configuredWeightsPath, hasPendingModelSelection);
            string adoptionText = BuildAdoptionText(comparison, configuredWeightsPath, hasPendingModelSelection);
            string nextActionText = BuildNextActionText(comparison, configuredWeightsPath, hasPendingModelSelection);
            ModelCenterDashboardState state = BuildModelCenterState(
                currentModelText,
                candidateModelText,
                adoptionText,
                nextActionText,
                BuildConfirmModelButtonText(comparison, configuredWeightsPath, hasPendingModelSelection),
                confirmModelButtonToolTip,
                hasPendingModelSelection && !isModelPromotionHeld,
                BuildDecisionSummaryText(comparison, configuredWeightsPath, hasPendingModelSelection),
                BuildDecisionEvidenceText(comparison, configuredWeightsPath, hasPendingModelSelection),
                BuildDecisionActionText(comparison, configuredWeightsPath, hasPendingModelSelection),
                ModelRegistryPresentationService.BuildSelectedRuntimeSummaryText(settings, runtimeState));
            state.RegistryPresentation = ModelRegistryPresentationService.Build(
                settings,
                comparison,
                trainingGuide,
                modelRegistry,
                hasPendingModelSelection,
                runtimeState);
            state.ReviewCandidateButtonText = BuildReviewCandidateButtonText(comparison, configuredWeightsPath, hasPendingModelSelection);
            state.ReviewCandidateButtonToolTip = BuildReviewCandidateButtonToolTip(comparison, configuredWeightsPath, hasPendingModelSelection);
            state.CanReviewCandidate = CanReviewCandidate(comparison, configuredWeightsPath, hasPendingModelSelection);
            return state;
        }

        public static ModelCenterDashboardState BuildModelCenterState(
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
            var entries = OpenVisionLanguageService.GetEntries();
            string normalizedCurrentModelText = string.IsNullOrWhiteSpace(currentModelText)
                ? OpenVisionLanguageService.T("WpfModelCenterDashboard.Current.None")
                : currentModelText.Trim();
            string normalizedCandidateModelText = string.IsNullOrWhiteSpace(candidateModelText)
                ? OpenVisionLanguageService.T("WpfModelCenterDashboard.Candidate.None")
                : candidateModelText.Trim();
            string normalizedAdoptionText = string.IsNullOrWhiteSpace(adoptionText)
                ? OpenVisionLanguageService.T("WpfModelCenterDashboard.Adoption.Wait")
                : adoptionText.Trim();
            string normalizedNextActionText = string.IsNullOrWhiteSpace(nextActionText)
                ? string.Format("{0}: {1}", OpenVisionLanguageService.T("WpfLearningWorkflow.TrainingModelLifecycle.Label.NextAction"), OpenVisionLanguageService.T("WpfLearningWorkflow.TrainingModelLifecycle.Next.Initial"))
                : nextActionText.Trim();
            string currentModelDetailText = StripModelCenterPrefix(
                normalizedCurrentModelText,
                "\uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378:",
                OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingModelLifecycle.Term.InspectionModelPending")[0] + ":",
                entries.First(entry => entry.Key == "WpfLearningWorkflow.TrainingModelLifecycle.Term.InspectionModelPending").Korean + ":",
                entries.First(entry => entry.Key == "WpfLearningWorkflow.TrainingModelLifecycle.Term.InspectionModelPending").English + ":",
                entries.First(entry => entry.Key == "WpfModelCenterDashboard.Current.Value").Korean.Split(new[] { "{0}" }, StringSplitOptions.None)[0].TrimEnd(),
                entries.First(entry => entry.Key == "WpfModelCenterDashboard.Current.Value").English.Split(new[] { "{0}" }, StringSplitOptions.None)[0].TrimEnd());
            string candidateModelDetailText = StripModelCenterPrefix(
                normalizedCandidateModelText,
                OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingModelLifecycle.Term.NewTrainedModel")[0] + ":",
                entries.First(entry => entry.Key == "WpfLearningWorkflow.TrainingModelLifecycle.Term.NewTrainedModel").Korean + ":",
                entries.First(entry => entry.Key == "WpfLearningWorkflow.TrainingModelLifecycle.Term.NewTrainedModel").English + ":");
            string adoptionDetailText = StripModelCenterPrefix(
                normalizedAdoptionText,
                OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingModelLifecycle.Term.InspectionAssignment")[0] + ":",
                entries.First(entry => entry.Key == "WpfLearningWorkflow.TrainingModelLifecycle.Term.InspectionAssignment").Korean + ":",
                entries.First(entry => entry.Key == "WpfLearningWorkflow.TrainingModelLifecycle.Term.InspectionAssignment").English + ":");
            string nextActionDetailText = StripModelCenterPrefix(
                normalizedNextActionText,
                "\uB2E4\uC74C:",
                entries.First(entry => entry.Key == "WpfLearningWorkflow.TrainingModelLifecycle.Label.NextAction").Korean + ":",
                entries.First(entry => entry.Key == "WpfLearningWorkflow.TrainingModelLifecycle.Label.NextAction").English + ":");

            return new ModelCenterDashboardState
            {
                CurrentModelText = normalizedCurrentModelText,
                CurrentModelDetailText = currentModelDetailText,
                CandidateModelText = normalizedCandidateModelText,
                CandidateModelDetailText = candidateModelDetailText,
                AdoptionText = normalizedAdoptionText,
                AdoptionDetailText = adoptionDetailText,
                NextActionText = normalizedNextActionText,
                NextActionDetailText = nextActionDetailText,
                ConfirmModelButtonText = string.IsNullOrWhiteSpace(confirmModelButtonText)
                    ? OpenVisionLanguageService.T("WpfModelCenterDashboard.Confirm.NoModel")
                    : confirmModelButtonText.Trim(),
                ConfirmModelButtonToolTip = string.IsNullOrWhiteSpace(confirmModelButtonToolTip)
                    ? OpenVisionLanguageService.T("WpfModelCenterDashboard.Confirm.NoResultTooltip")
                    : confirmModelButtonToolTip.Trim(),
                CanConfirmModel = canConfirmModel,
                DecisionSummaryText = string.IsNullOrWhiteSpace(decisionSummaryText)
                    ? "\uD310\uB2E8: " + adoptionDetailText
                    : decisionSummaryText.Trim(),
                DecisionEvidenceText = string.IsNullOrWhiteSpace(decisionEvidenceText)
                    ? OpenVisionLanguageService.T("WpfModelCenterDashboard.Evidence.ModelPrefix") + " " + currentModelDetailText + OpenVisionLanguageService.T("WpfModelCenterDashboard.Evidence.TrainingResultPrefix") + " " + candidateModelDetailText
                    : decisionEvidenceText.Trim(),
                DecisionActionText = string.IsNullOrWhiteSpace(decisionActionText)
                    ? "\uD655\uC815: " + nextActionDetailText
                    : decisionActionText.Trim(),
                RuntimeActionText = (runtimeActionText ?? string.Empty).Trim()
            };
        }

        public static string BuildActionStatePart(
            string buttonText,
            bool isEnabled,
            bool isAvailable,
            bool canRun,
            string toolTip)
        {
            string label = CompactModelCenterActionLabel(buttonText);
            if (isEnabled)
            {
                return $"{label} \uAC00\uB2A5";
            }

            string reason = !isAvailable
                ? OpenVisionLanguageService.T("WpfModelCenterDashboard.Action.NoItem")
                : !canRun
                    ? BuildModelCenterUnavailableReason(toolTip, "\uC791\uC5C5 \uC870\uAC74 \uD655\uC778")
                    : BuildModelCenterUnavailableReason(toolTip, "\uC900\uBE44 \uD544\uC694");
            return $"{label} \uB300\uAE30: {reason}";
        }

        public static string BuildActionStateText(
            string runtimeActionText,
            string reviewCandidateButtonText,
            bool isReviewCandidateEnabled,
            bool isReviewCandidateAvailable,
            bool canRunReviewCommands,
            string reviewCandidateToolTip,
            string confirmModelButtonText,
            bool isConfirmModelEnabled,
            bool isConfirmModelAvailable,
            bool canRunModelCommands,
            string confirmModelToolTip,
            string inspectCurrentImageButtonText,
            bool isInspectCurrentImageEnabled,
            bool canRunInspectCurrentImage,
            string inspectCurrentImageToolTip)
        {
            string runtimeStateText = string.IsNullOrWhiteSpace(runtimeActionText)
                ? string.Empty
                : OpenVisionLanguageService.T("WpfModelCenterDashboard.Action.RuntimePrefix") + " " + runtimeActionText + " / ";
            return runtimeStateText
                + ModelCenterActionRouteText
                + " / 버튼 상태: "
                + BuildActionStatePart(
                    reviewCandidateButtonText,
                    isReviewCandidateEnabled,
                    isReviewCandidateAvailable,
                    canRunReviewCommands,
                    reviewCandidateToolTip)
                + " / "
                + BuildActionStatePart(
                    confirmModelButtonText,
                    isConfirmModelEnabled,
                    isConfirmModelAvailable,
                    canRunModelCommands,
                    confirmModelToolTip)
                + " / "
                + BuildActionStatePart(
                    inspectCurrentImageButtonText,
                    isInspectCurrentImageEnabled,
                    true,
                    canRunInspectCurrentImage,
                    inspectCurrentImageToolTip);
        }

        public static string StripModelCenterPrefix(string text, params string[] prefixes)
        {
            string normalized = (text ?? string.Empty).Trim();
            foreach (string prefix in prefixes ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(prefix)
                    && normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return normalized.Substring(prefix.Length).Trim();
                }
            }

            return normalized;
        }

        public static string BuildInspectCurrentImageToolTip(string currentModelDetailText, string runtimeActionText)
        {
            string currentModel = currentModelDetailText?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(currentModel)
                || string.Equals(currentModel, "없음", StringComparison.OrdinalIgnoreCase)
                || currentModel.Contains("파일 없음", StringComparison.OrdinalIgnoreCase))
            {
                return OpenVisionLanguageService.T("WpfModelCenterDashboard.Inspect.SaveFirstTooltip");
            }

            string runtimeText = string.IsNullOrWhiteSpace(runtimeActionText)
                || currentModel.IndexOf(runtimeActionText, StringComparison.OrdinalIgnoreCase) >= 0
                ? string.Empty
                : OpenVisionLanguageService.T("WpfModelCenterDashboard.Inspect.RuntimeSuffix") + " " + runtimeActionText + ".";
            return string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelCenterDashboard.Inspect.CurrentImage.ToolTip"), currentModel, runtimeText);
        }

        public static string BuildConfirmModelAvailabilityToolTip(
            bool isAvailable,
            bool isEnabled,
            string unavailableToolTip,
            string baseToolTip)
        {
            return isAvailable
                && !isEnabled
                && !string.IsNullOrWhiteSpace(unavailableToolTip)
                ? unavailableToolTip
                : baseToolTip;
        }

        private static string CompactModelCenterActionLabel(string buttonText)
        {
            var entries = OpenVisionLanguageService.GetEntries();
            string text = (buttonText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return "\uBC84\uD2BC";
            }

            if (string.Equals(text, entries.First(entry => entry.Key == "WpfModelCenterDashboard.Review.Button").Korean, StringComparison.Ordinal)
                || string.Equals(text, entries.First(entry => entry.Key == "WpfModelCenterDashboard.Review.Button").English, StringComparison.Ordinal))
            {
                return OpenVisionLanguageService.T("WpfModelCenterDashboard.Review.Button");
            }

            if (string.Equals(text, entries.First(entry => entry.Key == "WpfModelCenterDashboard.Confirm.SaveSettings").Korean, StringComparison.Ordinal)
                || string.Equals(text, entries.First(entry => entry.Key == "WpfModelCenterDashboard.Confirm.SaveSettings").English, StringComparison.Ordinal))
            {
                return OpenVisionLanguageService.T("WpfModelCenterDashboard.Confirm.SaveSettings");
            }

            if (text.Contains("\uD6C4\uBCF4", StringComparison.Ordinal))
            {
                return OpenVisionLanguageService.T("WpfModelCenterDashboard.Review.Button");
            }

            if (text.Contains("\uC800\uC7A5", StringComparison.Ordinal))
            {
                return OpenVisionLanguageService.T("WpfModelCenterDashboard.Confirm.SaveSettings");
            }

            if (text.Contains("\uAC80\uC0AC", StringComparison.Ordinal))
            {
                return OpenVisionLanguageService.T("WpfModelCenterDashboard.Action.CurrentInference");
            }

            return text.Length > 16
                ? text.Substring(0, 16) + "..."
                : text;
        }

        private static string BuildModelCenterUnavailableReason(string toolTip, string fallback)
        {
            var entries = OpenVisionLanguageService.GetEntries();
            string text = (toolTip ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return fallback;
            }

            if ((text.Contains(OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfModelCenterDashboard.Review.NoResultTooltip")[0].Split(' ')[0], StringComparison.Ordinal)
                && text.Contains("\uC5C6\uC2B5\uB2C8\uB2E4", StringComparison.Ordinal))
                || string.Equals(text, OpenVisionLanguageService.T("WpfModelCenterDashboard.Review.NoResultTooltip"), StringComparison.Ordinal)
                || string.Equals(text, entries.First(entry => entry.Key == "WpfModelCenterDashboard.Review.NoResultTooltip").Korean, StringComparison.Ordinal)
                || string.Equals(text, entries.First(entry => entry.Key == "WpfModelCenterDashboard.Review.NoResultTooltip").English, StringComparison.Ordinal))
            {
                return OpenVisionLanguageService.T("WpfModelCenterDashboard.Action.NoTrainingResult");
            }

            if (text.Contains("\uD655\uC815\uD560", StringComparison.Ordinal)
                && text.Contains("\uC5C6\uC2B5\uB2C8\uB2E4", StringComparison.Ordinal))
            {
                return "\uC800\uC7A5\uD560 \uD6C4\uBCF4 \uC5C6\uC74C";
            }

            if (text.Contains("recipe", StringComparison.OrdinalIgnoreCase))
            {
                return "recipe \uC800\uC7A5 \uC870\uAC74 \uD655\uC778";
            }

            if (text.Contains("\uAC80\uC0AC \uBAA8\uB378\uC744 \uC800\uC7A5", StringComparison.Ordinal))
            {
                return OpenVisionLanguageService.T("WpfModelCenterDashboard.Action.InspectionModelUnsaved");
            }

            if (text.Contains("\uD604\uC7AC \uC774\uBBF8\uC9C0", StringComparison.Ordinal))
            {
                return "\uD604\uC7AC \uC774\uBBF8\uC9C0/\uBAA8\uB378 \uD655\uC778";
            }

            return text.Length > 30
                ? text.Substring(0, 30) + "..."
                : text;
        }

        private static string BuildCurrentModelText(
            PythonModelSettings settings,
            string configuredWeightsPath,
            bool hasPendingModelSelection,
            PythonModelRuntimeState runtimeState)
        {
            string trimmedPath = configuredWeightsPath?.Trim() ?? string.Empty;
            string runtimeSummaryText = ModelRegistryPresentationService.BuildSelectedRuntimeSummaryText(settings, runtimeState);
            if (string.IsNullOrWhiteSpace(trimmedPath))
            {
                return string.Format(OpenVisionLanguageService.T("WpfModelCenterDashboard.Current.NoneRuntime"), runtimeSummaryText);
            }

            string displayPath = FormatPath(trimmedPath);
            if (!File.Exists(trimmedPath))
            {
                return string.Format(OpenVisionLanguageService.T("WpfModelCenterDashboard.Current.MissingFile"), displayPath, runtimeSummaryText);
            }

            return hasPendingModelSelection
                ? string.Format(OpenVisionLanguageService.T("WpfModelCenterDashboard.Current.PendingSave"), displayPath, runtimeSummaryText)
                : string.Format(OpenVisionLanguageService.T("WpfModelCenterDashboard.Current.Value"), displayPath, runtimeSummaryText);
        }

        private static string BuildCandidateModelText(
            WpfTrainingWeightsComparison comparison,
            string configuredWeightsPath,
            bool hasPendingModelSelection)
        {
            if (comparison?.HasLatestWeights != true)
            {
                return OpenVisionLanguageService.T("WpfModelCenterDashboard.Candidate.None");
            }

            string latestWeightsPath = comparison.LatestWeightsPath?.Trim() ?? string.Empty;
            string currentWeightsPath = configuredWeightsPath?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(currentWeightsPath)
                && string.Equals(latestWeightsPath, currentWeightsPath, StringComparison.OrdinalIgnoreCase)
                && !hasPendingModelSelection)
            {
                return OpenVisionLanguageService.T("WpfModelCenterDashboard.Candidate.Same");
            }

            string compactMetricsText = ModelRegistryPresentationService.BuildCompactMetricSummary(comparison.MetricsStatusText);
            string suffix = string.IsNullOrWhiteSpace(compactMetricsText)
                ? string.Empty
                : $" / {compactMetricsText}";
            return string.Format(OpenVisionLanguageService.T("WpfModelCenterDashboard.Candidate.Value"), FormatPath(latestWeightsPath), suffix);
        }

        private static string BuildAdoptionText(
            WpfTrainingWeightsComparison comparison,
            string configuredWeightsPath,
            bool hasPendingModelSelection)
        {
            if (hasPendingModelSelection)
            {
                return string.Format("{0}: {1}", OpenVisionLanguageService.T("WpfLearningWorkflow.TrainingModelLifecycle.Term.InspectionAssignment"), OpenVisionLanguageService.T("WpfLearningWorkflow.TrainingModelLifecycle.Decision.Pending"));
            }

            if (comparison?.HasLatestWeights != true)
            {
                return string.Format("{0}: {1}", OpenVisionLanguageService.T("WpfLearningWorkflow.TrainingModelLifecycle.Term.InspectionAssignment"), OpenVisionLanguageService.T("WpfLearningWorkflow.TrainingModelLifecycle.Decision.NoResult"));
            }

            string latestWeightsPath = comparison.LatestWeightsPath?.Trim() ?? string.Empty;
            string currentWeightsPath = configuredWeightsPath?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(currentWeightsPath)
                && string.Equals(latestWeightsPath, currentWeightsPath, StringComparison.OrdinalIgnoreCase))
            {
                return string.Format("{0}: {1}", OpenVisionLanguageService.T("WpfLearningWorkflow.TrainingModelLifecycle.Term.InspectionAssignment"), OpenVisionLanguageService.T("WpfLearningWorkflow.TrainingModelLifecycle.Decision.Current"));
            }

            return comparison.ShouldApplyLatest
                ? string.Format("{0}: {1}", OpenVisionLanguageService.T("WpfLearningWorkflow.TrainingModelLifecycle.Term.InspectionAssignment"), OpenVisionLanguageService.T("WpfLearningWorkflow.TrainingModelLifecycle.Decision.Review"))
                : string.Format("{0}: {1}", OpenVisionLanguageService.T("WpfLearningWorkflow.TrainingModelLifecycle.Term.InspectionAssignment"), OpenVisionLanguageService.T("WpfLearningWorkflow.TrainingModelLifecycle.Decision.Keep"));
        }

        private static string BuildNextActionText(
            WpfTrainingWeightsComparison comparison,
            string configuredWeightsPath,
            bool hasPendingModelSelection)
        {
            if (hasPendingModelSelection)
            {
                return string.Format("{0}: {1}", OpenVisionLanguageService.T("WpfLearningWorkflow.TrainingModelLifecycle.Label.NextAction"), OpenVisionLanguageService.T("WpfLearningWorkflow.TrainingModelLifecycle.Next.Pending"));
            }

            string currentWeightsPath = configuredWeightsPath?.Trim() ?? string.Empty;
            if (comparison?.HasLatestWeights != true)
            {
                return string.IsNullOrWhiteSpace(currentWeightsPath)
                    ? string.Format("{0}: {1}", OpenVisionLanguageService.T("WpfLearningWorkflow.TrainingModelLifecycle.Label.NextAction"), OpenVisionLanguageService.T("WpfLearningWorkflow.TrainingModelLifecycle.Next.Initial"))
                    : string.Format("{0}: {1}", OpenVisionLanguageService.T("WpfLearningWorkflow.TrainingModelLifecycle.Label.NextAction"), OpenVisionLanguageService.T("WpfLearningWorkflow.TrainingModelLifecycle.Next.CurrentOnly"));
            }

            string latestWeightsPath = comparison.LatestWeightsPath?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(currentWeightsPath)
                && string.Equals(latestWeightsPath, currentWeightsPath, StringComparison.OrdinalIgnoreCase))
            {
                return string.Format("{0}: {1}", OpenVisionLanguageService.T("WpfLearningWorkflow.TrainingModelLifecycle.Label.NextAction"), OpenVisionLanguageService.T("WpfLearningWorkflow.TrainingModelLifecycle.Next.Same"));
            }

            return string.Format("{0}: {1}", OpenVisionLanguageService.T("WpfLearningWorkflow.TrainingModelLifecycle.Label.NextAction"), OpenVisionLanguageService.T("WpfLearningWorkflow.TrainingModelLifecycle.Next.Review"));
        }

        private static string BuildConfirmModelButtonText(
            WpfTrainingWeightsComparison comparison,
            string configuredWeightsPath,
            bool hasPendingModelSelection)
        {
            if (hasPendingModelSelection)
            {
                return OpenVisionLanguageService.T("WpfModelCenterDashboard.Confirm.SaveSettings");
            }

            if (IsLatestWeightsCurrent(comparison, configuredWeightsPath))
            {
                return OpenVisionLanguageService.T("WpfModelCenterDashboard.Confirm.Assigned");
            }

            if (comparison?.HasLatestWeights == true)
            {
                return OpenVisionLanguageService.T("WpfModelCenterDashboard.Confirm.CheckTrainedModel");
            }

            return OpenVisionLanguageService.T("WpfModelCenterDashboard.Confirm.NoModel");
        }

        private static string BuildDecisionSummaryText(
            WpfTrainingWeightsComparison comparison,
            string configuredWeightsPath,
            bool hasPendingModelSelection)
        {
            if (hasPendingModelSelection)
            {
                return OpenVisionLanguageService.T("WpfModelCenterDashboard.Decision.PendingAssignment");
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
                return OpenVisionLanguageService.T("WpfModelCenterDashboard.Decision.ApplyAfterValidation");
            }

            return "\uD310\uB2E8: \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378 \uC720\uC9C0 \uAD8C\uC7A5";
        }

        private static string BuildDecisionEvidenceText(
            WpfTrainingWeightsComparison comparison,
            string configuredWeightsPath,
            bool hasPendingModelSelection)
        {
            string currentModel = FormatPath(configuredWeightsPath);
            if (string.IsNullOrWhiteSpace(currentModel))
            {
                currentModel = "\uC5C6\uC74C";
            }

            if (comparison?.HasLatestWeights != true)
            {
                return string.Format(OpenVisionLanguageService.T("WpfModelCenterDashboard.Evidence.NoTrainingResult"), currentModel);
            }

            string candidateModel = FormatPath(comparison.LatestWeightsPath);
            string metrics = string.IsNullOrWhiteSpace(comparison.MetricsStatusText)
                ? comparison.StatusText
                : comparison.MetricsStatusText;
            if (string.IsNullOrWhiteSpace(metrics))
            {
                metrics = OpenVisionLanguageService.T("WpfModelCenterDashboard.Evidence.TestComparisonNeeded");
            }

            string metricsEvidence = EnsureNonFailureEvidence(metrics);
            return hasPendingModelSelection
                ? string.Format(OpenVisionLanguageService.T("WpfModelCenterDashboard.Evidence.SelectedModel"), currentModel, candidateModel, metricsEvidence)
                : string.Format(OpenVisionLanguageService.T("WpfModelCenterDashboard.Evidence.CurrentModel"), currentModel, candidateModel, metricsEvidence);
        }

        private static string EnsureNonFailureEvidence(string metricsText)
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

        private static string BuildDecisionActionText(
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
                return OpenVisionLanguageService.T("WpfModelCenterDashboard.DecisionAction.CurrentInference");
            }

            return comparison.ShouldApplyLatest
                ? "\uC800\uC7A5: \uD6C4\uBCF4 \uAC80\uC99D\uC73C\uB85C \uC608\uC2DC \uD655\uC778 \uD6C4 \uAC80\uC0AC \uBAA8\uB378\uB85C \uC800\uC7A5"
                : OpenVisionLanguageService.T("WpfModelCenterDashboard.DecisionAction.KeepCurrent");
        }

        private static string BuildConfirmModelButtonToolTip(
            WpfTrainingWeightsComparison comparison,
            string configuredWeightsPath,
            bool hasPendingModelSelection)
        {
            if (hasPendingModelSelection)
            {
                return OpenVisionLanguageService.T("WpfModelCenterDashboard.Confirm.SaveSettingsTooltip");
            }

            if (IsLatestWeightsCurrent(comparison, configuredWeightsPath))
            {
                return OpenVisionLanguageService.T("WpfModelCenterDashboard.Confirm.SameTooltip");
            }

            if (comparison?.HasLatestWeights == true)
            {
                return OpenVisionLanguageService.T("WpfModelCenterDashboard.Confirm.CheckOrSelectTooltip");
            }

            return OpenVisionLanguageService.T("WpfModelCenterDashboard.Confirm.NoResultTooltip");
        }

        private static string BuildReviewCandidateButtonText(
            WpfTrainingWeightsComparison comparison,
            string configuredWeightsPath,
            bool hasPendingModelSelection)
        {
            if (CanReviewCandidate(comparison, configuredWeightsPath, hasPendingModelSelection))
            {
                return OpenVisionLanguageService.T("WpfModelCenterDashboard.Review.Button");
            }

            if (IsLatestWeightsCurrent(comparison, configuredWeightsPath))
            {
                return OpenVisionLanguageService.T("WpfModelCenterDashboard.Confirm.Assigned");
            }

            return OpenVisionLanguageService.T("WpfModelCenterDashboard.Confirm.NoModel");
        }

        private static string BuildReviewCandidateButtonToolTip(
            WpfTrainingWeightsComparison comparison,
            string configuredWeightsPath,
            bool hasPendingModelSelection)
        {
            if (CanReviewCandidate(comparison, configuredWeightsPath, hasPendingModelSelection))
            {
                return OpenVisionLanguageService.T("WpfModelCenterDashboard.Review.ComparisonTooltip");
            }

            if (IsLatestWeightsCurrent(comparison, configuredWeightsPath))
            {
                return OpenVisionLanguageService.T("WpfModelCenterDashboard.Review.SameTooltip");
            }

            return OpenVisionLanguageService.T("WpfModelCenterDashboard.Review.NoResultTooltip");
        }

        private static bool CanReviewCandidate(
            WpfTrainingWeightsComparison comparison,
            string configuredWeightsPath,
            bool hasPendingModelSelection)
        {
            return comparison?.HasLatestWeights == true
                && (hasPendingModelSelection || !IsLatestWeightsCurrent(comparison, configuredWeightsPath));
        }

        private static bool IsLatestWeightsCurrent(WpfTrainingWeightsComparison comparison, string configuredWeightsPath)
        {
            string latestWeightsPath = comparison?.LatestWeightsPath?.Trim() ?? string.Empty;
            string currentWeightsPath = configuredWeightsPath?.Trim() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(latestWeightsPath)
                && !string.IsNullOrWhiteSpace(currentWeightsPath)
                && string.Equals(latestWeightsPath, currentWeightsPath, StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            string displayPath = TrainingWeightsService.FormatWeightsDisplayPath(path);
            return string.IsNullOrWhiteSpace(displayPath)
                ? Path.GetFileName(path.Trim())
                : displayPath;
        }
    }

    [Obsolete("Use ModelCenterDashboardPresentationService.", false)]
    public static class WpfModelCenterDashboardPresentationService
    {
        public static WpfModelCenterDashboardState Build(
            PythonModelSettings settings,
            WpfTrainingWeightsComparison comparison,
            YoloTrainingGuideHistory trainingGuide,
            ModelRegistrySettings modelRegistry,
            string configuredWeightsPath,
            bool hasPendingModelSelection,
            bool isModelPromotionHeld)
            => WpfModelCenterDashboardState.FromCanonical(ModelCenterDashboardPresentationService.Build(settings, comparison, trainingGuide, modelRegistry, configuredWeightsPath, hasPendingModelSelection, isModelPromotionHeld));

        public static WpfModelCenterDashboardState BuildModelCenterState(
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
            => WpfModelCenterDashboardState.FromCanonical(ModelCenterDashboardPresentationService.BuildModelCenterState(
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

        public static string BuildActionStatePart(
            string buttonText,
            bool isEnabled,
            bool isAvailable,
            bool canRun,
            string toolTip)
            => ModelCenterDashboardPresentationService.BuildActionStatePart(buttonText, isEnabled, isAvailable, canRun, toolTip);

        public static string BuildActionStateText(
            string runtimeActionText,
            string reviewCandidateButtonText,
            bool isReviewCandidateEnabled,
            bool isReviewCandidateAvailable,
            bool canRunReviewCommands,
            string reviewCandidateToolTip,
            string confirmModelButtonText,
            bool isConfirmModelEnabled,
            bool isConfirmModelAvailable,
            bool canRunModelCommands,
            string confirmModelToolTip,
            string inspectCurrentImageButtonText,
            bool isInspectCurrentImageEnabled,
            bool canRunInspectCurrentImage,
            string inspectCurrentImageToolTip)
            => ModelCenterDashboardPresentationService.BuildActionStateText(
                runtimeActionText,
                reviewCandidateButtonText,
                isReviewCandidateEnabled,
                isReviewCandidateAvailable,
                canRunReviewCommands,
                reviewCandidateToolTip,
                confirmModelButtonText,
                isConfirmModelEnabled,
                isConfirmModelAvailable,
                canRunModelCommands,
                confirmModelToolTip,
                inspectCurrentImageButtonText,
                isInspectCurrentImageEnabled,
                canRunInspectCurrentImage,
                inspectCurrentImageToolTip);

        public static string StripModelCenterPrefix(string text, params string[] prefixes)
            => ModelCenterDashboardPresentationService.StripModelCenterPrefix(text, prefixes);

        public static string BuildInspectCurrentImageToolTip(string currentModelDetailText, string runtimeActionText)
            => ModelCenterDashboardPresentationService.BuildInspectCurrentImageToolTip(currentModelDetailText, runtimeActionText);

        public static string BuildConfirmModelAvailabilityToolTip(
            bool isAvailable,
            bool isEnabled,
            string unavailableToolTip,
            string baseToolTip)
            => ModelCenterDashboardPresentationService.BuildConfirmModelAvailabilityToolTip(isAvailable, isEnabled, unavailableToolTip, baseToolTip);
    }
}
