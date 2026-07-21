using System;
using System.IO;
using MvcVisionSystem.Yolo;

namespace MvcVisionSystem
{
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
        {
            string confirmModelButtonToolTip = BuildConfirmModelButtonToolTip(
                comparison,
                configuredWeightsPath,
                hasPendingModelSelection);
            if (isModelPromotionHeld)
            {
                confirmModelButtonToolTip = WpfModelCandidateDecisionPresentationService.BuildHeldCandidateSaveBlockedStatus();
            }

            return new WpfModelCenterDashboardState
            {
                RegistryPresentation = WpfModelRegistryPresentationService.Build(
                    settings,
                    comparison,
                    trainingGuide,
                    modelRegistry,
                    hasPendingModelSelection),
                CurrentModelText = BuildCurrentModelText(settings, configuredWeightsPath, hasPendingModelSelection),
                CandidateModelText = BuildCandidateModelText(comparison, configuredWeightsPath, hasPendingModelSelection),
                AdoptionText = BuildAdoptionText(comparison, configuredWeightsPath, hasPendingModelSelection),
                NextActionText = BuildNextActionText(comparison, configuredWeightsPath, hasPendingModelSelection),
                ReviewCandidateButtonText = BuildReviewCandidateButtonText(comparison, configuredWeightsPath, hasPendingModelSelection),
                ReviewCandidateButtonToolTip = BuildReviewCandidateButtonToolTip(comparison, configuredWeightsPath, hasPendingModelSelection),
                CanReviewCandidate = CanReviewCandidate(comparison, configuredWeightsPath, hasPendingModelSelection),
                ConfirmModelButtonText = BuildConfirmModelButtonText(comparison, configuredWeightsPath, hasPendingModelSelection),
                ConfirmModelButtonToolTip = confirmModelButtonToolTip,
                CanConfirmModel = hasPendingModelSelection && !isModelPromotionHeld,
                DecisionSummaryText = BuildDecisionSummaryText(comparison, configuredWeightsPath, hasPendingModelSelection),
                DecisionEvidenceText = BuildDecisionEvidenceText(comparison, configuredWeightsPath, hasPendingModelSelection),
                DecisionActionText = BuildDecisionActionText(comparison, configuredWeightsPath, hasPendingModelSelection),
                RuntimeActionText = WpfModelRegistryPresentationService.BuildSelectedRuntimeSummaryText(settings)
            };
        }

        private static string BuildCurrentModelText(
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

            string compactMetricsText = WpfModelRegistryPresentationService.BuildCompactMetricSummary(comparison.MetricsStatusText);
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

            string displayPath = WpfTrainingWeightsService.FormatWeightsDisplayPath(path);
            return string.IsNullOrWhiteSpace(displayPath)
                ? Path.GetFileName(path.Trim())
                : displayPath;
        }
    }
}
