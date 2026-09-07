using System;
using System.IO;
using MvcVisionSystem.Yolo;

namespace MvcVisionSystem
{
    public static class ModelCenterDashboardPresentationService
    {
        private const string ModelCenterActionRouteText = "실행: 후보 검증=학습 후보 탭 열기, 현재 검사=검사 모델+현재 이미지 -> AI 후보/캔버스";

        public static ModelCenterDashboardState Build(
            PythonModelSettings settings,
            WpfTrainingWeightsComparison comparison,
            YoloTrainingGuideHistory trainingGuide,
            ModelRegistrySettings modelRegistry,
            string configuredWeightsPath,
            bool hasPendingModelSelection,
            bool isModelPromotionHeld)
        {
            string confirmModelButtonToolTip = BuildConfirmModelButtonToolTip(
                comparison,
                configuredWeightsPath,
                hasPendingModelSelection);
            if (isModelPromotionHeld)
            {
                confirmModelButtonToolTip = ModelCandidateDecisionPresentationService.BuildHeldCandidateSaveBlockedStatus();
            }

            string currentModelText = BuildCurrentModelText(settings, configuredWeightsPath, hasPendingModelSelection);
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
                ModelRegistryPresentationService.BuildSelectedRuntimeSummaryText(settings));
            state.RegistryPresentation = ModelRegistryPresentationService.Build(
                settings,
                comparison,
                trainingGuide,
                modelRegistry,
                hasPendingModelSelection);
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
            string normalizedCurrentModelText = string.IsNullOrWhiteSpace(currentModelText)
                ? "\uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378: \uC5C6\uC74C"
                : currentModelText.Trim();
            string normalizedCandidateModelText = string.IsNullOrWhiteSpace(candidateModelText)
                ? "\uC0C8 \uD559\uC2B5 \uBAA8\uB378 \uD6C4\uBCF4: \uC5C6\uC74C"
                : candidateModelText.Trim();
            string normalizedAdoptionText = string.IsNullOrWhiteSpace(adoptionText)
                ? "\uBAA8\uB378 \uC801\uC6A9: \uB300\uAE30"
                : adoptionText.Trim();
            string normalizedNextActionText = string.IsNullOrWhiteSpace(nextActionText)
                ? "\uB2E4\uC74C: \uB370\uC774\uD130\uC14B \uC810\uAC80 \uD6C4 \uD559\uC2B5\uC744 \uC2DC\uC791\uD558\uC138\uC694."
                : nextActionText.Trim();
            string currentModelDetailText = StripModelCenterPrefix(
                normalizedCurrentModelText,
                "\uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378:",
                "\uAC80\uC0AC \uBAA8\uB378 \uD6C4\uBCF4:");
            string candidateModelDetailText = StripModelCenterPrefix(
                normalizedCandidateModelText,
                "\uC0C8 \uD559\uC2B5 \uBAA8\uB378 \uD6C4\uBCF4:");
            string adoptionDetailText = StripModelCenterPrefix(
                normalizedAdoptionText,
                "\uBAA8\uB378 \uC801\uC6A9:");
            string nextActionDetailText = StripModelCenterPrefix(
                normalizedNextActionText,
                "\uB2E4\uC74C:");

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
                    ? "\uD6C4\uBCF4 \uC5C6\uC74C"
                    : confirmModelButtonText.Trim(),
                ConfirmModelButtonToolTip = string.IsNullOrWhiteSpace(confirmModelButtonToolTip)
                    ? "\uD655\uC815\uD560 \uD559\uC2B5 \uACB0\uACFC \uBAA8\uB378\uC774 \uC5C6\uC2B5\uB2C8\uB2E4."
                    : confirmModelButtonToolTip.Trim(),
                CanConfirmModel = canConfirmModel,
                DecisionSummaryText = string.IsNullOrWhiteSpace(decisionSummaryText)
                    ? "\uD310\uB2E8: " + adoptionDetailText
                    : decisionSummaryText.Trim(),
                DecisionEvidenceText = string.IsNullOrWhiteSpace(decisionEvidenceText)
                    ? "\uADFC\uAC70: \uAC80\uC0AC \uBAA8\uB378 " + currentModelDetailText + " / \uD559\uC2B5 \uACB0\uACFC " + candidateModelDetailText
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
                ? "\uB300\uC0C1 \uC5C6\uC74C"
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
                : "실행기: " + runtimeActionText + " / ";
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
                return "검사 모델을 저장한 뒤 현재 검사를 실행할 수 있습니다.";
            }

            string runtimeText = string.IsNullOrWhiteSpace(runtimeActionText)
                || currentModel.IndexOf(runtimeActionText, StringComparison.OrdinalIgnoreCase) >= 0
                ? string.Empty
                : " 실행기: " + runtimeActionText + ".";
            return "현재 이미지에 " + currentModel + " 모델로 검사를 실행하고 결과를 AI 후보/캔버스에 표시합니다." + runtimeText;
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
            string text = (buttonText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return "\uBC84\uD2BC";
            }

            if (text.Contains("\uD6C4\uBCF4", StringComparison.Ordinal))
            {
                return "\uD6C4\uBCF4 \uAC80\uC99D";
            }

            if (text.Contains("\uC800\uC7A5", StringComparison.Ordinal))
            {
                return "\uAC80\uC0AC \uBAA8\uB378 \uC800\uC7A5";
            }

            if (text.Contains("\uAC80\uC0AC", StringComparison.Ordinal))
            {
                return "\uD604\uC7AC \uAC80\uC0AC";
            }

            return text.Length > 16
                ? text.Substring(0, 16) + "..."
                : text;
        }

        private static string BuildModelCenterUnavailableReason(string toolTip, string fallback)
        {
            string text = (toolTip ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return fallback;
            }

            if (text.Contains("\uAC80\uD1A0\uD560", StringComparison.Ordinal)
                && text.Contains("\uC5C6\uC2B5\uB2C8\uB2E4", StringComparison.Ordinal))
            {
                return "\uD559\uC2B5 \uD6C4\uBCF4 \uC5C6\uC74C";
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
                return "\uAC80\uC0AC \uBAA8\uB378 \uC800\uC7A5 \uD544\uC694";
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
            bool hasPendingModelSelection)
        {
            string trimmedPath = configuredWeightsPath?.Trim() ?? string.Empty;
            string runtimeSummaryText = ModelRegistryPresentationService.BuildSelectedRuntimeSummaryText(settings);
            if (string.IsNullOrWhiteSpace(trimmedPath))
            {
                return $"\uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378: \uC5C6\uC74C / \uC2E4\uD589\uAE30 {runtimeSummaryText}";
            }

            string displayPath = FormatPath(trimmedPath);
            if (!File.Exists(trimmedPath))
            {
                return $"\uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378: {displayPath} / \uC2E4\uD589\uAE30 {runtimeSummaryText} / \uD30C\uC77C \uC5C6\uC74C";
            }

            return hasPendingModelSelection
                ? $"\uAC80\uC0AC \uBAA8\uB378 \uD6C4\uBCF4: {displayPath} / \uC2E4\uD589\uAE30 {runtimeSummaryText} / \uC124\uC815 \uC800\uC7A5 \uD544\uC694"
                : $"\uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378: {displayPath} / \uC2E4\uD589\uAE30 {runtimeSummaryText}";
        }

        private static string BuildCandidateModelText(
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

            string compactMetricsText = ModelRegistryPresentationService.BuildCompactMetricSummary(comparison.MetricsStatusText);
            string suffix = string.IsNullOrWhiteSpace(compactMetricsText)
                ? string.Empty
                : $" / {compactMetricsText}";
            return $"\uC0C8 \uD559\uC2B5 \uBAA8\uB378 \uD6C4\uBCF4: {FormatPath(latestWeightsPath)}{suffix}";
        }

        private static string BuildAdoptionText(
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

        private static string BuildNextActionText(
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

        private static string BuildConfirmModelButtonText(
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

        private static string BuildDecisionSummaryText(
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
                return $"\uADFC\uAC70: \uAC80\uC0AC \uBAA8\uB378 {currentModel} / \uD559\uC2B5 \uACB0\uACFC \uC5C6\uC74C";
            }

            string candidateModel = FormatPath(comparison.LatestWeightsPath);
            string metrics = string.IsNullOrWhiteSpace(comparison.MetricsStatusText)
                ? comparison.StatusText
                : comparison.MetricsStatusText;
            if (string.IsNullOrWhiteSpace(metrics))
            {
                metrics = "\uCD5C\uC885 \uAC80\uC99D \uBE44\uAD50 \uD544\uC694";
            }

            string metricsEvidence = EnsureNonFailureEvidence(metrics);
            return hasPendingModelSelection
                ? $"\uADFC\uAC70: \uC120\uD0DD \uD6C4\uBCF4 {currentModel} / \uD559\uC2B5 \uD6C4\uBCF4 {candidateModel} / {metricsEvidence}"
                : $"\uADFC\uAC70: \uAC80\uC0AC \uBAA8\uB378 {currentModel} / \uD559\uC2B5 \uD6C4\uBCF4 {candidateModel} / {metricsEvidence}";
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
                return "\uB2E4\uC74C: \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378\uB85C \uCD94\uB860 \uAC80\uD1A0";
            }

            return comparison.ShouldApplyLatest
                ? "\uC800\uC7A5: \uD6C4\uBCF4 \uAC80\uC99D\uC73C\uB85C \uC608\uC2DC \uD655\uC778 \uD6C4 \uAC80\uC0AC \uBAA8\uB378\uB85C \uC800\uC7A5"
                : "\uD310\uB2E8: \uD604\uC7AC \uBAA8\uB378 \uC720\uC9C0, \uD544\uC694 \uC2DC \uD6C4\uBCF4 \uAC80\uD1A0\uB9CC \uD655\uC778";
        }

        private static string BuildConfirmModelButtonToolTip(
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

        private static string BuildReviewCandidateButtonText(
            WpfTrainingWeightsComparison comparison,
            string configuredWeightsPath,
            bool hasPendingModelSelection)
        {
            if (CanReviewCandidate(comparison, configuredWeightsPath, hasPendingModelSelection))
            {
                return "\uD6C4\uBCF4 \uAC80\uC99D";
            }

            if (IsLatestWeightsCurrent(comparison, configuredWeightsPath))
            {
                return "\uC801\uC6A9 \uC644\uB8CC";
            }

            return "\uD6C4\uBCF4 \uC5C6\uC74C";
        }

        private static string BuildReviewCandidateButtonToolTip(
            WpfTrainingWeightsComparison comparison,
            string configuredWeightsPath,
            bool hasPendingModelSelection)
        {
            if (CanReviewCandidate(comparison, configuredWeightsPath, hasPendingModelSelection))
            {
                return "\uD559\uC2B5 \uD6C4\uBCF4 \uBAA8\uB378\uC744 \uD6C4\uBCF4 \uAC80\uD1A0 \uD0ED\uC5D0\uC11C \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378\uACFC \uBE44\uAD50\uD569\uB2C8\uB2E4. \uAC80\uCD9C \uC2E4\uD589\uC740 \uD604\uC7AC \uAC80\uC0AC \uBC84\uD2BC\uC5D0\uC11C \uD604\uC7AC \uC774\uBBF8\uC9C0\uB85C \uC9C4\uD589\uD569\uB2C8\uB2E4.";
            }

            if (IsLatestWeightsCurrent(comparison, configuredWeightsPath))
            {
                return "\uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378\uACFC \uD559\uC2B5 \uACB0\uACFC \uBAA8\uB378\uC774 \uAC19\uC544 \uAC80\uC99D\uD560 \uD6C4\uBCF4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.";
            }

            return "\uAC80\uD1A0\uD560 \uD559\uC2B5 \uACB0\uACFC \uBAA8\uB378\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.";
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
