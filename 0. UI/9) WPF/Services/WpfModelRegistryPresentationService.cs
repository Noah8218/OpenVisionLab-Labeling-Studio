using MvcVisionSystem._1._Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MvcVisionSystem
{
    public sealed class WpfModelRegistryHistoryItem
    {
        public string CandidateId { get; set; } = string.Empty;

        public string ProfileId { get; set; } = string.Empty;

        public string TrainingRunId { get; set; } = string.Empty;

        public string WeightsPath { get; set; } = string.Empty;

        public string BaselineWeightsPath { get; set; } = string.Empty;

        public string KindText { get; set; } = string.Empty;

        public string TitleText { get; set; } = string.Empty;

        public string DetailText { get; set; } = string.Empty;

        public string MetricText { get; set; } = string.Empty;

        public string DecisionText { get; set; } = string.Empty;

        public bool IsCurrentInspectionModel { get; set; }

        public bool CanPromoteToInspectionModel { get; set; }

        public string ActionText { get; set; } = string.Empty;

        public string ActionToolTip { get; set; } = string.Empty;
    }

    public sealed class WpfModelRegistryPresentation
    {
        public string ProfileText { get; set; } = string.Empty;

        public string TrainingRunText { get; set; } = string.Empty;

        public string CandidateModelText { get; set; } = string.Empty;

        public string InspectionModelText { get; set; } = string.Empty;

        public string ActionText { get; set; } = string.Empty;

        public string SummaryPrimaryText { get; set; } = string.Empty;

        public string SummarySecondaryText { get; set; } = string.Empty;

        public IReadOnlyList<WpfModelRegistryHistoryItem> HistoryItems { get; set; } = Array.Empty<WpfModelRegistryHistoryItem>();
    }

    public static class WpfModelRegistryPresentationService
    {
        public static WpfModelRegistryPresentation Build(
            PythonModelSettings settings,
            WpfTrainingWeightsComparison comparison,
            YoloTrainingGuideHistory history,
            bool hasPendingInspectionModelSelection)
            => Build(settings, comparison, history, null, hasPendingInspectionModelSelection);

        public static WpfModelRegistryPresentation Build(
            PythonModelSettings settings,
            WpfTrainingWeightsComparison comparison,
            YoloTrainingGuideHistory history,
            ModelRegistrySettings registry,
            bool hasPendingInspectionModelSelection)
        {
            settings ??= new PythonModelSettings();
            history?.EnsureDefaults();
            registry?.EnsureDefaults();

            return new WpfModelRegistryPresentation
            {
                ProfileText = BuildProfileText(settings, registry),
                TrainingRunText = BuildTrainingRunText(history, comparison, registry),
                CandidateModelText = BuildCandidateModelText(comparison, settings.WeightsPath, registry),
                InspectionModelText = BuildInspectionModelText(settings, registry, hasPendingInspectionModelSelection),
                ActionText = BuildActionText(comparison, settings.WeightsPath, registry, hasPendingInspectionModelSelection),
                SummaryPrimaryText = BuildSummaryPrimaryText(settings, comparison, registry, hasPendingInspectionModelSelection),
                SummarySecondaryText = BuildSummarySecondaryText(settings, history, registry, hasPendingInspectionModelSelection),
                HistoryItems = BuildHistoryItems(registry)
            };
        }

        private static IReadOnlyList<WpfModelRegistryHistoryItem> BuildHistoryItems(ModelRegistrySettings registry)
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
                        .OrderByDescending(item => ParseUtc(item.DecidedUtc))
                        .First(),
                    StringComparer.Ordinal);

            return registry.Candidates
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.WeightsPath))
                .OrderByDescending(item => item.IsCurrentInspectionModel)
                .ThenByDescending(item => ParseUtc(item.LastSeenUtc))
                .Take(6)
                .Select(candidate => BuildHistoryItem(candidate, profiles, runs, decisions))
                .ToList();
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
                ? "\uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378"
                : "\uD559\uC2B5 \uD6C4\uBCF4";
            string modelPath = FormatModelPath(candidate.WeightsPath);
            string profileText = string.IsNullOrWhiteSpace(profile?.DisplayName)
                ? "\uD504\uB85C\uD544 \uBBF8\uD655\uC778"
                : profile.DisplayName;
            string runText = run == null
                ? "\uC2E4\uD589 \uC774\uB825 \uBBF8\uD655\uC778"
                : $"\uC2E4\uD589 {FormatTrainingState(run.State)} {FormatLocalTime(run.EventUtc)}".TrimEnd();
            string baseline = FormatModelPath(candidate.BaselineWeightsPath);
            string baselineText = string.IsNullOrWhiteSpace(baseline)
                ? string.Empty
                : $" / baseline {baseline}";
            string metrics = !string.IsNullOrWhiteSpace(candidate.MetricsSummary)
                ? candidate.MetricsSummary
                : !string.IsNullOrWhiteSpace(run?.MetricsSummary)
                    ? run.MetricsSummary
                    : "\uC9C0\uD45C \uC5C6\uC74C";
            string decisionCode = !string.IsNullOrWhiteSpace(decision?.Decision)
                ? decision.Decision
                : candidate.Decision;
            string decisionText = FormatCandidateDecisionText(decisionCode);
            if (string.IsNullOrWhiteSpace(decisionText))
            {
                decisionText = candidate.SavedToRecipe ? "\uCC44\uD0DD" : "\uB300\uAE30";
            }

            bool weightsExists = File.Exists(candidate.WeightsPath ?? string.Empty);
            bool canPromote = !candidate.IsCurrentInspectionModel && weightsExists;
            return new WpfModelRegistryHistoryItem
            {
                CandidateId = candidate.CandidateId ?? string.Empty,
                ProfileId = candidate.ProfileId ?? string.Empty,
                TrainingRunId = candidate.TrainingRunId ?? string.Empty,
                WeightsPath = candidate.WeightsPath ?? string.Empty,
                BaselineWeightsPath = candidate.BaselineWeightsPath ?? string.Empty,
                KindText = titlePrefix,
                TitleText = $"{titlePrefix}: {modelPath}",
                DetailText = $"{profileText} / {runText}{baselineText}",
                MetricText = metrics,
                DecisionText = candidate.IsCurrentInspectionModel
                    ? $"\uD604\uC7AC \uC0AC\uC6A9 / {decisionText}"
                    : $"\uACB0\uC815 {decisionText}",
                IsCurrentInspectionModel = candidate.IsCurrentInspectionModel,
                CanPromoteToInspectionModel = canPromote,
                ActionText = candidate.IsCurrentInspectionModel
                    ? "\uD604\uC7AC \uC0AC\uC6A9 \uC911"
                    : weightsExists
                        ? "\uAC80\uC0AC \uBAA8\uB378\uB85C \uC801\uC6A9"
                        : "\uD30C\uC77C \uC5C6\uC74C",
                ActionToolTip = candidate.IsCurrentInspectionModel
                    ? "\uC774\uBBF8 \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378\uB85C \uB4F1\uB85D\uB41C \uC774\uB825\uC785\uB2C8\uB2E4."
                    : weightsExists
                        ? "\uC120\uD0DD\uD55C \uC774\uB825\uC758 \uAC00\uC911\uCE58\uB97C recipe\uC758 \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378\uB85C \uC800\uC7A5\uD569\uB2C8\uB2E4."
                        : "\uC774\uB825\uC758 \uAC00\uC911\uCE58 \uD30C\uC77C\uC774 \uC5C6\uC5B4 \uC801\uC6A9\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4."
            };
        }

        private static string BuildProfileText(PythonModelSettings settings, ModelRegistrySettings registry)
        {
            string engine = PythonModelSettings.NormalizeModelEngine(settings?.ModelEngine);
            string profileName = engine switch
            {
                PythonModelSettings.EngineYoloV8 => "YOLOv8 \uAC1D\uCCB4\uD0D0\uC9C0",
                PythonModelSettings.EngineYolo11 => "YOLO11 \uAC1D\uCCB4\uD0D0\uC9C0",
                PythonModelSettings.EngineOnnx => "ONNX \uCD94\uB860",
                _ => "YOLOv5 \uAC1D\uCCB4\uD0D0\uC9C0"
            };

            string protocol = settings?.GetProtocolModelName() ?? string.Empty;
            string registrySuffix = registry?.Profiles?.Count > 0
                ? $" / \uB4F1\uB85D \uD504\uB85C\uD544 {registry.Profiles.Count}"
                : string.Empty;
            string text = string.IsNullOrWhiteSpace(protocol)
                ? $"\uBAA8\uB378 \uD504\uB85C\uD544: {profileName}"
                : $"\uBAA8\uB378 \uD504\uB85C\uD544: {profileName} / \uC2E4\uD589 \uC5B4\uB311\uD130 {protocol}";
            return text + registrySuffix;
        }

        private static string BuildSummaryPrimaryText(
            PythonModelSettings settings,
            WpfTrainingWeightsComparison comparison,
            ModelRegistrySettings registry,
            bool hasPendingSelection)
        {
            string currentPath = ResolveCurrentInspectionWeightsPath(settings, registry, hasPendingSelection);
            string current = FormatModelPath(currentPath);
            if (string.IsNullOrWhiteSpace(current))
            {
                current = "\uC5C6\uC74C";
            }

            ModelCandidate latestRecord = ModelRegistryService.FindLatestCandidate(registry);
            string candidatePath = comparison?.LatestWeightsPath?.Trim() ?? latestRecord?.WeightsPath?.Trim() ?? string.Empty;
            string candidate = FormatModelPath(candidatePath);
            string candidateText;
            if (string.IsNullOrWhiteSpace(candidate))
            {
                candidateText = "\uD559\uC2B5 \uD6C4\uBCF4: \uC5C6\uC74C";
            }
            else if (!hasPendingSelection
                && !string.IsNullOrWhiteSpace(currentPath)
                && string.Equals(candidatePath, currentPath, StringComparison.OrdinalIgnoreCase))
            {
                candidateText = "\uD559\uC2B5 \uD6C4\uBCF4: \uD604\uC7AC \uBAA8\uB378\uACFC \uAC19\uC74C";
            }
            else
            {
                candidateText = $"\uD559\uC2B5 \uD6C4\uBCF4: {candidate}";
            }

            return $"\uD604\uC7AC \uAC80\uC0AC: {current} / {candidateText}";
        }

        private static string BuildSummarySecondaryText(
            PythonModelSettings settings,
            YoloTrainingGuideHistory history,
            ModelRegistrySettings registry,
            bool hasPendingSelection)
        {
            string profile = BuildSelectedRuntimeSummaryText(settings);

            string trainingState = "\uC5C6\uC74C";
            TrainingRun latestRun = ModelRegistryService.FindLatestTrainingRun(registry);
            if (latestRun != null)
            {
                trainingState = FormatTrainingState(latestRun.State);
            }
            else if (!string.IsNullOrWhiteSpace(history?.LastTrainingState))
            {
                trainingState = FormatTrainingState(history.LastTrainingState);
                if (history.LastTrainingProgressPercent >= 0)
                {
                    trainingState += $" {Math.Clamp(history.LastTrainingProgressPercent, 0, 100)}%";
                }
            }

            int historyCount = registry?.Candidates?.Count ?? 0;
            ModelCandidate latestCandidate = ModelRegistryService.FindLatestCandidate(registry);
            string candidateState = hasPendingSelection
                ? "\uC120\uD0DD \uD6C4\uBCF4 recipe \uC800\uC7A5 \uC804"
                : latestCandidate?.SavedToRecipe == true
                    ? "\uD6C4\uBCF4 recipe \uC800\uC7A5\uB428"
                    : latestCandidate != null
                        ? "\uD6C4\uBCF4 \uC800\uC7A5 \uB300\uAE30"
                        : "\uD6C4\uBCF4 \uC5C6\uC74C";

            return $"{profile} / \uCD5C\uADFC \uD559\uC2B5 {trainingState} / \uC774\uB825 {historyCount}\uAC74 / {candidateState}";
        }

        public static string BuildSelectedRuntimeSummaryText(PythonModelSettings settings)
        {
            settings ??= new PythonModelSettings();
            PythonModelRuntimeProfile selectedProfile = PythonModelRuntimeProfileService
                .BuildProfiles(settings)
                .FirstOrDefault(item => item?.IsSelected == true);

            string runtimeName = BuildRuntimeName(selectedProfile, settings.ModelEngine);
            string readinessText = BuildRuntimeReadinessText(selectedProfile);
            return string.IsNullOrWhiteSpace(runtimeName)
                ? readinessText
                : $"{runtimeName} / {readinessText}";
        }

        private static string BuildRuntimeName(PythonModelRuntimeProfile selectedProfile, string fallbackEngine)
        {
            string engine = selectedProfile?.DisplayName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(engine))
            {
                engine = PythonModelSettings.NormalizeModelEngine(fallbackEngine) switch
                {
                    PythonModelSettings.EngineYoloV8 => "YOLOv8",
                    PythonModelSettings.EngineYolo11 => "YOLO11",
                    PythonModelSettings.EngineOnnx => "ONNX",
                    _ => "YOLOv5"
                };
            }

            string family = selectedProfile?.RuntimeFamilyText?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(family))
            {
                return engine;
            }

            return family.IndexOf(engine, StringComparison.OrdinalIgnoreCase) >= 0
                ? family
                : $"{engine} {family}";
        }

        private static string BuildRuntimeReadinessText(PythonModelRuntimeProfile selectedProfile)
        {
            if (selectedProfile == null)
            {
                return "\uC0C1\uD0DC \uD655\uC778 \uD544\uC694";
            }

            if (selectedProfile.CanInspect && selectedProfile.CanTrain)
            {
                return "\uAC80\uC0AC/\uD559\uC2B5 \uAC00\uB2A5";
            }

            if (selectedProfile.CanInspect)
            {
                return "\uD604\uC7AC \uAC80\uC0AC \uAC00\uB2A5 / \uD559\uC2B5 \uBBF8\uC9C0\uC6D0";
            }

            if (selectedProfile.CanTrain)
            {
                return "\uD559\uC2B5 \uAC00\uB2A5 / \uAC80\uC0AC \uBAA8\uB378 \uD544\uC694";
            }

            return selectedProfile.IsRuntimeConnected
                ? "\uC124\uC815 \uD655\uC778 \uD544\uC694"
                : "\uC124\uCE58/\uC5F0\uACB0 \uD544\uC694";
        }

        private static string ResolveCurrentInspectionWeightsPath(
            PythonModelSettings settings,
            ModelRegistrySettings registry,
            bool hasPendingSelection)
        {
            ModelCandidate currentRecord = ModelRegistryService.FindCurrentInspectionModel(registry);
            if (!string.IsNullOrWhiteSpace(currentRecord?.WeightsPath))
            {
                return currentRecord.WeightsPath.Trim();
            }

            return hasPendingSelection
                ? string.Empty
                : settings?.WeightsPath?.Trim() ?? string.Empty;
        }

        private static string BuildTrainingRunText(
            YoloTrainingGuideHistory history,
            WpfTrainingWeightsComparison comparison,
            ModelRegistrySettings registry)
        {
            string latestModel = FormatModelPath(comparison?.LatestWeightsPath);
            TrainingRun latestRun = ModelRegistryService.FindLatestTrainingRun(registry);
            if (history == null || string.IsNullOrWhiteSpace(history.LastTrainingState))
            {
                if (latestRun != null)
                {
                    string runModel = FormatModelPath(latestRun.CandidateWeightsPath);
                    string metrics = string.IsNullOrWhiteSpace(latestRun.MetricsSummary)
                        ? string.Empty
                        : $" / {latestRun.MetricsSummary}";
                    return $"\uCD5C\uADFC \uD559\uC2B5 \uC2E4\uD589: {FormatTrainingState(latestRun.State)} / \uACB0\uACFC {runModel}{metrics}";
                }

                return string.IsNullOrWhiteSpace(latestModel)
                    ? "\uCD5C\uADFC \uD559\uC2B5 \uC2E4\uD589: \uC5C6\uC74C"
                    : $"\uCD5C\uADFC \uD559\uC2B5 \uC2E4\uD589: \uACB0\uACFC \uBAA8\uB378 {latestModel}";
            }

            string progress = history.LastTrainingProgressPercent >= 0
                ? $" {Math.Clamp(history.LastTrainingProgressPercent, 0, 100)}%"
                : string.Empty;
            string time = FormatLocalTime(history.LastTrainingUpdateUtc);
            string suffix = string.IsNullOrWhiteSpace(latestModel)
                ? string.Empty
                : $" / \uACB0\uACFC {latestModel}";
            return $"\uCD5C\uADFC \uD559\uC2B5 \uC2E4\uD589: {FormatTrainingState(history.LastTrainingState)}{progress} {time}{suffix}".TrimEnd();
        }

        private static string BuildCandidateModelText(
            WpfTrainingWeightsComparison comparison,
            string currentWeightsPath,
            ModelRegistrySettings registry)
        {
            ModelCandidate latestRecord = ModelRegistryService.FindLatestCandidate(registry);
            if (comparison?.HasLatestWeights != true && latestRecord == null)
            {
                return "\uBAA8\uB378 \uD6C4\uBCF4: \uC5C6\uC74C";
            }

            string latest = comparison?.LatestWeightsPath?.Trim() ?? latestRecord?.WeightsPath?.Trim() ?? string.Empty;
            string current = currentWeightsPath?.Trim() ?? string.Empty;
            string display = FormatModelPath(latest);
            string sameText = !string.IsNullOrWhiteSpace(current)
                && string.Equals(latest, current, StringComparison.OrdinalIgnoreCase)
                    ? " / \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378\uACFC \uAC19\uC74C"
                    : " / \uAC80\uC99D \uD6C4 \uC800\uC7A5 \uD310\uB2E8";
            string metricsText = comparison?.MetricsStatusText ?? latestRecord?.MetricsSummary;
            string compactMetricsText = BuildCompactMetricSummary(metricsText);
            string metrics = string.IsNullOrWhiteSpace(compactMetricsText)
                ? string.Empty
                : $" / {compactMetricsText}";
            string decisionText = FormatCandidateDecision(latestRecord);
            string savedText = latestRecord?.SavedToRecipe == true
                ? " / recipe \uC800\uC7A5\uB428"
                : latestRecord != null
                    ? " / recipe \uC800\uC7A5 \uB300\uAE30"
                    : string.Empty;
            return $"\uBAA8\uB378 \uD6C4\uBCF4: {display}{metrics}{sameText}{savedText}{decisionText}";
        }

        public static string BuildCompactMetricSummary(string metricsText)
        {
            string text = (metricsText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string[] parts = text
                .Split(new[] { " / ", "," }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Trim())
                .Where(part => part.Length > 0)
                .ToArray();
            if (parts.Length == 0)
            {
                return text;
            }

            var compactParts = new List<string>();
            string map5095 = FindMetricPart(parts, "mAP50-95");
            string map50 = parts.FirstOrDefault(part =>
                part.IndexOf("mAP50", StringComparison.OrdinalIgnoreCase) >= 0
                && part.IndexOf("mAP50-95", StringComparison.OrdinalIgnoreCase) < 0);
            string precision = FindMetricPart(parts, "precision");
            string recall = FindMetricPart(parts, "recall");

            AddCompactMetricPart(compactParts, map5095, "mAP50-95", "mAP50-95");
            AddCompactMetricPart(compactParts, map50, "mAP50", "mAP50");
            if (!string.IsNullOrWhiteSpace(precision) && !string.IsNullOrWhiteSpace(recall))
            {
                string precisionValue = StripMetricLabel(precision, "precision");
                string recallValue = StripMetricLabel(recall, "recall");
                compactParts.Add($"P/R {precisionValue}/{recallValue}");
            }
            else
            {
                AddCompactMetricPart(compactParts, precision, "precision", "precision");
                AddCompactMetricPart(compactParts, recall, "recall", "recall");
            }

            if (compactParts.Count > 0)
            {
                return string.Join(" / ", compactParts);
            }

            return parts.Length <= 2
                ? text
                : string.Join(" / ", parts.Take(2));
        }

        private static string FindMetricPart(IEnumerable<string> parts, string label)
            => parts.FirstOrDefault(part => part.IndexOf(label, StringComparison.OrdinalIgnoreCase) >= 0);

        private static void AddCompactMetricPart(ICollection<string> compactParts, string metricPart, string label, string displayLabel)
        {
            if (string.IsNullOrWhiteSpace(metricPart))
            {
                return;
            }

            string value = StripMetricLabel(metricPart, label);
            compactParts.Add(string.IsNullOrWhiteSpace(value)
                ? displayLabel
                : $"{displayLabel} {value}");
        }

        private static string StripMetricLabel(string metricPart, string label)
        {
            string text = (metricPart ?? string.Empty).Trim();
            int index = text.IndexOf(label, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return text;
            }

            return text
                .Substring(index + label.Length)
                .Trim()
                .TrimStart(':', '=')
                .Trim();
        }

        private static string BuildInspectionModelText(
            PythonModelSettings settings,
            ModelRegistrySettings registry,
            bool hasPendingSelection)
        {
            string weightsPath = settings?.WeightsPath ?? string.Empty;
            string runtimeSummaryText = BuildSelectedRuntimeSummaryText(settings);
            ModelCandidate currentRecord = ModelRegistryService.FindCurrentInspectionModel(registry);
            string sourcePath = !string.IsNullOrWhiteSpace(currentRecord?.WeightsPath)
                ? currentRecord.WeightsPath
                : hasPendingSelection
                    ? string.Empty
                    : weightsPath;
            string display = FormatModelPath(sourcePath);
            if (string.IsNullOrWhiteSpace(display))
            {
                return hasPendingSelection
                    ? $"\uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378: \uC5C6\uC74C / \uC2E4\uD589\uAE30 {runtimeSummaryText} / \uC120\uD0DD \uD6C4\uBCF4 recipe \uC800\uC7A5 \uC804"
                    : $"\uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378: \uC5C6\uC74C / \uC2E4\uD589\uAE30 {runtimeSummaryText}";
            }

            string state = currentRecord?.SavedToRecipe == true
                    ? "recipe \uC800\uC7A5 \uC774\uB825"
                    : File.Exists(sourcePath ?? string.Empty)
                    ? "recipe \uC800\uC7A5\uB41C \uACBD\uB85C"
                    : "\uD30C\uC77C \uD655\uC778 \uD544\uC694";
            if (hasPendingSelection)
            {
                state += " / \uC120\uD0DD \uD6C4\uBCF4\uB294 recipe \uC800\uC7A5 \uC804";
            }

            return $"\uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378: {display} / \uC2E4\uD589\uAE30 {runtimeSummaryText} / {state}";
        }

        private static string BuildActionText(
            WpfTrainingWeightsComparison comparison,
            string currentWeightsPath,
            ModelRegistrySettings registry,
            bool hasPendingSelection)
        {
            if (hasPendingSelection)
            {
                return "\uB2E4\uC74C: \uAC80\uC0AC \uBAA8\uB378\uB85C \uC800\uC7A5\uD574 recipe\uC5D0 \uD604\uC7AC \uD6C4\uBCF4\uB97C \uB4F1\uB85D\uD558\uC138\uC694.";
            }

            if (comparison?.HasLatestWeights == true
                && !string.Equals(comparison.LatestWeightsPath?.Trim() ?? string.Empty, currentWeightsPath?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                return "\uB2E4\uC74C: \uD6C4\uBCF4 \uAC80\uC99D\uC73C\uB85C \uD604\uC7AC \uBAA8\uB378\uACFC \uD559\uC2B5 \uACB0\uACFC\uB97C \uBE44\uAD50\uD55C \uB4A4 \uC800\uC7A5\uD558\uC138\uC694.";
            }

            int adoptionCount = registry?.AdoptionHistory?.Count ?? 0;
            int decisionCount = registry?.CandidateDecisions?.Count ?? 0;
            if (adoptionCount > 0 || decisionCount > 0)
            {
                string latestDecision = FormatCandidateDecision(ModelRegistryService.FindLatestCandidateDecision(registry));
                string decisionSuffix = decisionCount > 0
                    ? $" / \uACB0\uC815 \uC774\uB825 {decisionCount}\uAC74{latestDecision}"
                    : string.Empty;
                string adoptionSuffix = adoptionCount > 0
                    ? $" / \uCC44\uD0DD \uC774\uB825 {adoptionCount}\uAC74"
                    : string.Empty;
                return $"\uAD6C\uC870: \uBAA8\uB378 \uD504\uB85C\uD544 -> \uD559\uC2B5 \uC2E4\uD589 -> \uD6C4\uBCF4 \uBAA8\uB378 -> \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378{adoptionSuffix}{decisionSuffix}";
            }

            return "\uAD6C\uC870: \uBAA8\uB378 \uD504\uB85C\uD544 -> \uD559\uC2B5 \uC2E4\uD589 -> \uD6C4\uBCF4 \uBAA8\uB378 -> \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378\uB85C \uBD84\uB9AC\uD574 \uAD00\uB9AC\uD569\uB2C8\uB2E4.";
        }

        private static string FormatCandidateDecision(ModelCandidate candidate)
        {
            if (candidate == null)
            {
                return string.Empty;
            }

            string text = FormatCandidateDecisionText(candidate.Decision);
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string summary = string.IsNullOrWhiteSpace(candidate.DecisionSummary)
                ? string.Empty
                : $" ({candidate.DecisionSummary})";
            return $" / \uACB0\uC815 {text}{summary}";
        }

        private static string FormatCandidateDecision(ModelCandidateDecision decision)
        {
            string text = FormatCandidateDecisionText(decision?.Decision);
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string summary = string.IsNullOrWhiteSpace(decision?.DecisionSummary)
                ? string.Empty
                : $" {decision.DecisionSummary}";
            return $" / \uCD5C\uADFC {text}{summary}";
        }

        private static string FormatCandidateDecisionText(string decision)
        {
            return (decision ?? string.Empty).Trim() switch
            {
                ModelRegistryService.CandidateDecisionAdopted => "\uCC44\uD0DD",
                ModelRegistryService.CandidateDecisionRejected => "\uAC70\uC808",
                ModelRegistryService.CandidateDecisionPending => "\uB300\uAE30",
                _ => string.Empty
            };
        }

        private static string FormatModelPath(string path)
            => WpfTrainingWeightsService.FormatWeightsDisplayPath(path);

        private static string FormatTrainingState(string state)
        {
            return (state ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "completed" or "complete" or "done" => "\uC644\uB8CC",
                "failed" or "error" => "\uC2E4\uD328",
                "running" or "training" => "\uC9C4\uD589 \uC911",
                "started" => "\uC2DC\uC791\uB428",
                "" => "\uBBF8\uD655\uC778",
                _ => state.Trim()
            };
        }

        private static string FormatLocalTime(string utcText)
        {
            if (DateTime.TryParse(
                utcText,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out DateTime utc))
            {
                return utc.ToLocalTime().ToString("HH:mm", CultureInfo.CurrentCulture);
            }

            return string.Empty;
        }

        private static DateTime ParseUtc(string utcText)
        {
            if (DateTime.TryParse(
                utcText,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out DateTime utc))
            {
                return utc.ToUniversalTime();
            }

            return DateTime.MinValue;
        }
    }
}
