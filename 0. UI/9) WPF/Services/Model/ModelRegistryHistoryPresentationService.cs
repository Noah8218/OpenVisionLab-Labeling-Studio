using MvcVisionSystem._1._Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MvcVisionSystem
{
    public static class ModelRegistryHistoryPresentationService
    {
        public static IReadOnlyList<WpfModelRegistryHistoryItem> BuildHistoryItems(ModelRegistrySettings registry, int maximumItems = 6)
        {
            registry?.EnsureDefaults();
            if (registry == null || registry.Candidates == null || registry.Candidates.Count == 0)
            {
                return Array.Empty<WpfModelRegistryHistoryItem>();
            }

            Dictionary<string, ModelProfile> profiles = registry.Profiles
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.ProfileId))
                .GroupBy(item => item.ProfileId)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            Dictionary<string, TrainingRun> runs = registry.TrainingRuns
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.TrainingRunId))
                .GroupBy(item => item.TrainingRunId)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            Dictionary<string, ModelCandidateDecision> decisions = registry.CandidateDecisions
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.CandidateId))
                .GroupBy(item => item.CandidateId)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderByDescending(item => ModelRegistryTextPresentationService.ParseUtc(item.DecidedUtc))
                        .First(),
                    StringComparer.Ordinal);

            return registry.Candidates
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.WeightsPath))
                .OrderByDescending(item => item.IsCurrentInspectionModel)
                .ThenByDescending(item => ModelRegistryTextPresentationService.ParseUtc(item.LastSeenUtc))
                .Take(Math.Max(1, maximumItems))
                .Select(candidate => BuildHistoryItem(candidate, profiles, runs, decisions))
                .ToList();
        }

        public static IReadOnlyList<WpfModelRegistryHistoryItem> IncludeConfiguredModel(IReadOnlyList<WpfModelRegistryHistoryItem> items, PythonModelSettings settings, int maximumItems = 6)
        {
            items ??= Array.Empty<WpfModelRegistryHistoryItem>();
            string weightsPath = settings?.WeightsPath?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(weightsPath) || items.Any(item => item?.IsCurrentInspectionModel == true))
            {
                return items;
            }

            var current = new WpfModelRegistryHistoryItem
            {
                WeightsPath = weightsPath,
                ModelEngine = settings.ModelEngine,
                ProjectRootPath = settings.ProjectRootPath,
                IsCurrentInspectionModel = true,
                IsConfiguredModelFallback = true,
                KindText = OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.History.Configured.Model"),
                TitleText = $"{OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.History.Configured.Model")}: {ModelRegistryTextPresentationService.FormatModelPath(weightsPath)}",
                DetailText = OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.History.Configured.Detail"),
                DecisionText = OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.History.Configured.Detail"),
                ActionText = OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.History.Configured.Model"),
                ActionToolTip = OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.History.Configured.Detail")
            };
            return new[] { current }.Concat(items.Where(item => item != null && !string.Equals(item.WeightsPath, weightsPath, StringComparison.OrdinalIgnoreCase))).Take(Math.Max(1, maximumItems)).ToArray();
        }

        public static IReadOnlyList<WpfModelRegistryHistoryItem> IncludeInferenceSelection(IReadOnlyList<WpfModelRegistryHistoryItem> items, WpfModelRegistryHistoryItem selection, int maximumItems = 6)
        {
            items ??= Array.Empty<WpfModelRegistryHistoryItem>();
            if (selection == null || items.Any(item => item != null
                && string.Equals(item.CandidateId, selection.CandidateId, StringComparison.Ordinal)
                && string.Equals(item.WeightsPath, selection.WeightsPath, StringComparison.OrdinalIgnoreCase)))
            {
                return items;
            }

            return items.Where(item => item?.IsCurrentInspectionModel == true)
                .Concat(new[] { selection })
                .Concat(items.Where(item => item != null && !item.IsCurrentInspectionModel))
                .Take(Math.Max(1, maximumItems))
                .ToArray();
        }

        public static WpfModelRegistryHistoryItem FindHistorySelection(
            IEnumerable<WpfModelRegistryHistoryItem> items,
            string candidateId,
            string weightsPath)
        {
            IReadOnlyList<WpfModelRegistryHistoryItem> historyItems =
                (items ?? Array.Empty<WpfModelRegistryHistoryItem>())
                .Where(item => item != null)
                .ToList();
            if (!string.IsNullOrWhiteSpace(candidateId))
            {
                WpfModelRegistryHistoryItem byCandidate = historyItems.FirstOrDefault(item =>
                    string.Equals(item.CandidateId, candidateId, StringComparison.Ordinal));
                if (byCandidate != null)
                {
                    return byCandidate;
                }
            }

            if (!string.IsNullOrWhiteSpace(weightsPath))
            {
                return historyItems.FirstOrDefault(item =>
                    string.Equals(item.WeightsPath, weightsPath, StringComparison.OrdinalIgnoreCase));
            }

            return null;
        }

        public static WpfModelRegistryHistorySelectionPresentation BuildSelectedHistoryPresentation(
            IEnumerable<WpfModelRegistryHistoryItem> items,
            WpfModelRegistryHistoryItem selected)
        {
            string comparisonTitle = OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.History.Selection.ComparisonTitle");
            if (selected == null)
            {
                return new WpfModelRegistryHistorySelectionPresentation
                {
                    IsVisible = false,
                    TitleText = OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.History.Selection.NoneTitle"),
                    DetailText = OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.History.Selection.NoneDetail"),
                    ComparisonTitleText = comparisonTitle,
                    ActionText = OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.History.Selection.CannotAssign"),
                    ActionToolTip = OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.History.Selection.SelectFirst"),
                    CanPromoteToInspectionModel = false
                };
            }

            WpfModelRegistryHistoryItem current = (items ?? Array.Empty<WpfModelRegistryHistoryItem>())
                .FirstOrDefault(item => item?.IsCurrentInspectionModel == true);
            string title = string.IsNullOrWhiteSpace(selected.TitleText)
                ? OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.History.Selection.TitleFallback")
                : selected.TitleText.Trim();
            string detail = string.IsNullOrWhiteSpace(selected.DetailText)
                ? selected.WeightsPath ?? string.Empty
                : selected.DetailText.Trim();
            string actionText = string.IsNullOrWhiteSpace(selected.ActionText)
                ? OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.History.Selection.CannotAssign")
                : selected.ActionText.Trim();
            string actionToolTip = string.IsNullOrWhiteSpace(selected.ActionToolTip)
                ? OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.History.Selection.AssignToolTip")
                : selected.ActionToolTip.Trim();

            return new WpfModelRegistryHistorySelectionPresentation
            {
                IsVisible = true,
                TitleText = title,
                DetailText = detail,
                MetricText = selected.MetricText ?? string.Empty,
                DecisionText = selected.DecisionText ?? string.Empty,
                ComparisonTitleText = comparisonTitle,
                CurrentModelText = current == null
                    ? OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.History.Current.Empty")
                    : current.IsConfiguredModelFallback ? current.TitleText : BuildHistoryComparisonRowText(OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.History.Current.Label"), current),
                SelectedModelText = selected.IsConfiguredModelFallback ? selected.TitleText : selected.IsCurrentInspectionModel
                    ? "\uC120\uD0DD \uC774\uB825: \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378\uACFC \uAC19\uC74C"
                    : BuildHistoryComparisonRowText("\uC120\uD0DD \uC774\uB825", selected),
                ComparisonMetricText = BuildHistoryComparisonMetricText(current, selected),
                ActionText = actionText,
                ActionToolTip = actionToolTip,
                CanPromoteToInspectionModel = selected.CanPromoteToInspectionModel
            };
        }

        private static WpfModelRegistryHistoryItem BuildHistoryItem(
            ModelCandidate candidate,
            IReadOnlyDictionary<string, ModelProfile> profiles,
            IReadOnlyDictionary<string, TrainingRun> runs,
            IReadOnlyDictionary<string, ModelCandidateDecision> decisions)
        {
            profiles.TryGetValue(candidate.ProfileId ?? string.Empty, out ModelProfile profile);
            runs.TryGetValue(candidate.TrainingRunId ?? string.Empty, out TrainingRun run);
            decisions.TryGetValue(candidate.CandidateId ?? string.Empty, out ModelCandidateDecision decision);

            string titlePrefix = candidate.IsCurrentInspectionModel
                ? OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.History.Current.Kind")
                : OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.History.Trained.Label");
            string modelPath = ModelRegistryTextPresentationService.FormatModelPath(candidate.WeightsPath);
            string profileText = string.IsNullOrWhiteSpace(profile?.DisplayName)
                ? OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.History.Profile.Unknown")
                : profile.DisplayName;
            string runText = run == null
                ? OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.History.Run.Unknown")
                : string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.History.Run.Value"), ModelRegistryTextPresentationService.FormatTrainingState(run.State), ModelRegistryTextPresentationService.FormatLocalTime(run.EventUtc)).TrimEnd();
            string baseline = ModelRegistryTextPresentationService.FormatModelPath(candidate.BaselineWeightsPath);
            string baselineText = string.IsNullOrWhiteSpace(baseline)
                ? string.Empty
                : $" / baseline {baseline}";
            string datasetVersionText = string.IsNullOrWhiteSpace(run?.DatasetVersionId)
                ? string.Empty
                : $" / Dataset {ShortenDatasetVersion(run.DatasetVersionId)}";
            string metrics = !string.IsNullOrWhiteSpace(candidate.MetricsSummary)
                ? candidate.MetricsSummary
                : !string.IsNullOrWhiteSpace(run?.MetricsSummary)
                    ? run.MetricsSummary
                    : "\uC9C0\uD45C \uC5C6\uC74C";
            string decisionCode = !string.IsNullOrWhiteSpace(decision?.Decision)
                ? decision.Decision
                : candidate.Decision;
            string decisionText = ModelRegistryTextPresentationService.FormatCandidateDecisionText(decisionCode);
            if (string.IsNullOrWhiteSpace(decisionText))
            {
                decisionText = candidate.SavedToRecipe ? OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.Decision.Assigned") : OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.Decision.Pending");
            }

            bool weightsExists = File.Exists(candidate.WeightsPath ?? string.Empty)
                || File.Exists(candidate.ArtifactPath ?? string.Empty);
            bool artifactVerified = string.IsNullOrWhiteSpace(candidate.WeightsSha256)
                || ModelArtifactStoreService.Verify(candidate.ArtifactPath, candidate.WeightsSha256);
            bool isRejected = string.Equals(decisionCode, ModelRegistryService.CandidateDecisionRejected, StringComparison.Ordinal);
            bool canPromote = !candidate.IsCurrentInspectionModel && weightsExists && artifactVerified && !isRejected;
            bool canRunInferenceTrial = !candidate.IsCurrentInspectionModel
                && !isRejected
                && File.Exists(candidate.WeightsPath ?? string.Empty)
                && artifactVerified
                && profile != null
                && string.Equals(profile.DatasetPurpose, LabelingDatasetPurpose.ObjectDetection.ToString(), StringComparison.Ordinal)
                && PythonModelSettings.GetSupportedModelEngines().Contains(
                    PythonModelSettings.NormalizeModelEngine(profile.ModelEngine),
                    StringComparer.Ordinal);
            return new WpfModelRegistryHistoryItem
            {
                CandidateId = candidate.CandidateId ?? string.Empty,
                ProfileId = candidate.ProfileId ?? string.Empty,
                TrainingRunId = candidate.TrainingRunId ?? string.Empty,
                TrainingRunName = run?.RunName ?? string.Empty,
                DatasetVersionId = run?.DatasetVersionId ?? string.Empty,
                DatasetContentSha256 = run?.DatasetContentSha256 ?? string.Empty,
                WeightsPath = candidate.WeightsPath ?? string.Empty,
                ModelEngine = profile?.ModelEngine ?? string.Empty,
                ProjectRootPath = profile?.ProjectRootPath ?? string.Empty,
                CanRunInferenceTrial = canRunInferenceTrial,
                WeightsSha256 = candidate.WeightsSha256 ?? string.Empty,
                ArtifactPath = candidate.ArtifactPath ?? string.Empty,
                ArtifactStatus = candidate.ArtifactStatus ?? string.Empty,
                BaselineWeightsPath = candidate.BaselineWeightsPath ?? string.Empty,
                KindText = titlePrefix,
                TitleText = $"{titlePrefix}: {modelPath}",
                DetailText = $"{profileText} / {runText}{datasetVersionText}{baselineText}",
                MetricText = metrics,
                DecisionText = candidate.IsCurrentInspectionModel
                    ? $"\uD604\uC7AC \uC0AC\uC6A9 / {decisionText}"
                    : $"\uACB0\uC815 {decisionText}",
                IsCurrentInspectionModel = candidate.IsCurrentInspectionModel,
                CanPromoteToInspectionModel = canPromote,
                ActionText = candidate.IsCurrentInspectionModel
                    ? "\uD604\uC7AC \uC0AC\uC6A9 \uC911"
                    : isRejected
                        ? "\uAC70\uC808\uB41C \uD6C4\uBCF4"
                    : weightsExists
                        ? "\uAC80\uC0AC \uBAA8\uB378\uB85C \uC801\uC6A9"
                        : "\uD30C\uC77C \uC5C6\uC74C",
                ActionToolTip = candidate.IsCurrentInspectionModel
                    ? "\uC774\uBBF8 \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378\uB85C \uB4F1\uB85D\uB41C \uC774\uB825\uC785\uB2C8\uB2E4."
                    : isRejected
                        ? "\uAC70\uC808\uB41C \uD6C4\uBCF4\uB294 \uBC14\uB85C \uAC80\uC0AC \uBAA8\uB378\uB85C \uC801\uC6A9\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4. \uD544\uC694\uD558\uBA74 \uB2E4\uC2DC \uD559\uC2B5/\uBE44\uAD50\uD558\uC138\uC694."
                    : weightsExists
                        ? "\uC120\uD0DD\uD55C \uC774\uB825\uC758 \uAC00\uC911\uCE58\uB97C recipe\uC758 \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378\uB85C \uC800\uC7A5\uD569\uB2C8\uB2E4."
                        : "\uC774\uB825\uC758 \uAC00\uC911\uCE58 \uD30C\uC77C\uC774 \uC5C6\uC5B4 \uC801\uC6A9\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4."
            };
        }

        private static string ShortenDatasetVersion(string datasetVersionId)
        {
            string value = datasetVersionId?.Trim() ?? string.Empty;
            return value.Length <= 22 ? value : value.Substring(0, 22) + "…";
        }

        private static string BuildHistoryComparisonRowText(
            string titleText,
            WpfModelRegistryHistoryItem item)
        {
            if (item == null)
            {
                return $"{titleText}: \uC5C6\uC74C";
            }

            string title = string.IsNullOrWhiteSpace(item.TitleText)
                ? item.WeightsPath ?? string.Empty
                : item.TitleText.Trim();
            string decision = string.IsNullOrWhiteSpace(item.DecisionText)
                ? "\uACB0\uC815 \uBBF8\uD655\uC778"
                : item.DecisionText.Trim();
            return $"{titleText}: {title} / {decision}";
        }

        private static string BuildHistoryComparisonMetricText(
            WpfModelRegistryHistoryItem current,
            WpfModelRegistryHistoryItem selected)
        {
            if (selected == null)
            {
                return string.Empty;
            }

            if (current == null)
            {
                return "\uC9C0\uD45C \uBE44\uAD50: \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378 \uC774\uB825\uC774 \uC5C6\uC5B4 \uC120\uD0DD \uC774\uB825\uB9CC \uD655\uC778\uD569\uB2C8\uB2E4.";
            }

            bool isSameCandidate = !string.IsNullOrWhiteSpace(current.CandidateId)
                && string.Equals(current.CandidateId, selected.CandidateId, StringComparison.Ordinal);
            bool isSameWeights = !string.IsNullOrWhiteSpace(current.WeightsPath)
                && string.Equals(current.WeightsPath, selected.WeightsPath, StringComparison.OrdinalIgnoreCase);
            if (selected.IsCurrentInspectionModel || isSameCandidate || isSameWeights)
            {
                return "\uC9C0\uD45C \uBE44\uAD50: \uC120\uD0DD \uC774\uB825\uC774 \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378\uC785\uB2C8\uB2E4.";
            }

            string currentMetric = string.IsNullOrWhiteSpace(current.MetricText)
                ? "\uC9C0\uD45C \uC5C6\uC74C"
                : current.MetricText.Trim();
            string selectedMetric = string.IsNullOrWhiteSpace(selected.MetricText)
                ? "\uC9C0\uD45C \uC5C6\uC74C"
                : selected.MetricText.Trim();
            return $"\uC9C0\uD45C \uBE44\uAD50: \uD604\uC7AC {currentMetric} / \uC120\uD0DD \uC774\uB825 {selectedMetric}";
        }
    }

    [Obsolete("Use ModelRegistryHistoryPresentationService.", false)]
    public static class WpfModelRegistryHistoryPresentationService
    {
        public static IReadOnlyList<WpfModelRegistryHistoryItem> BuildHistoryItems(ModelRegistrySettings registry)
            => ModelRegistryHistoryPresentationService.BuildHistoryItems(registry);

        public static WpfModelRegistryHistoryItem FindHistorySelection(
            IEnumerable<WpfModelRegistryHistoryItem> items,
            string candidateId,
            string weightsPath)
            => ModelRegistryHistoryPresentationService.FindHistorySelection(items, candidateId, weightsPath);

        public static WpfModelRegistryHistorySelectionPresentation BuildSelectedHistoryPresentation(
            IEnumerable<WpfModelRegistryHistoryItem> items,
            WpfModelRegistryHistoryItem selected)
            => ModelRegistryHistoryPresentationService.BuildSelectedHistoryPresentation(items, selected);
    }
}
