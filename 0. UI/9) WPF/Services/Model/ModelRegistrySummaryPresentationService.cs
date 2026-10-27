using MvcVisionSystem._1._Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MvcVisionSystem
{
    /// <summary>
    /// Builds the read-only Model Registry summary shown by the WPF panels.
    /// It owns profile, runtime, training-run, candidate, inspection-model and
    /// action wording while persistence and model adoption remain elsewhere.
    /// </summary>
    public static class ModelRegistrySummaryPresentationService
    {
        public static WpfModelRegistryPresentation Build(
            PythonModelSettings settings,
            WpfTrainingWeightsComparison comparison,
            YoloTrainingGuideHistory history,
            ModelRegistrySettings registry,
            bool hasPendingInspectionModelSelection)
            => Build(settings, comparison, history, registry, hasPendingInspectionModelSelection, null);

        public static WpfModelRegistryPresentation Build(
            PythonModelSettings settings,
            WpfTrainingWeightsComparison comparison,
            YoloTrainingGuideHistory history,
            ModelRegistrySettings registry,
            bool hasPendingInspectionModelSelection,
            PythonModelRuntimeState runtimeState)
        {
            settings ??= new PythonModelSettings();
            history?.EnsureDefaults();
            registry?.EnsureDefaults();

            Func<string> candidateTextSource;
            Func<string> actionTextSource;
            var presentation = new WpfModelRegistryPresentation
            {
                ProfileText = BuildProfileText(settings, registry),
                TrainingRunText = BuildTrainingRunText(history, comparison, registry),
                CandidateModelText = (candidateTextSource = BuildCandidateModelTextSource(comparison, settings.WeightsPath, registry))(),
                InspectionModelText = BuildInspectionModelText(settings, registry, hasPendingInspectionModelSelection, runtimeState),
                ActionText = (actionTextSource = BuildActionTextSource(comparison, settings.WeightsPath, registry, hasPendingInspectionModelSelection))(),
                SummaryPrimaryText = BuildSummaryPrimaryText(settings, comparison, registry, hasPendingInspectionModelSelection),
                SummarySecondaryText = BuildSummarySecondaryText(settings, history, registry, hasPendingInspectionModelSelection, runtimeState),
                HistoryItems = ModelRegistryHistoryPresentationService.IncludeConfiguredModel(ModelRegistryHistoryPresentationService.BuildHistoryItems(registry), hasPendingInspectionModelSelection ? null : settings)
            };
            string originCandidate = presentation.CandidateModelText;
            string originAction = presentation.ActionText;
            presentation.DecisionTextSource = (renderCandidate, renderAction) => (originCandidate, originAction, renderCandidate ? candidateTextSource() : string.Empty, renderAction ? actionTextSource() : string.Empty);
            return presentation;
        }

        private static string BuildProfileText(PythonModelSettings settings, ModelRegistrySettings registry)
        {
            string engine = PythonModelSettings.NormalizeModelEngine(settings?.ModelEngine);
            string profileName = engine switch
            {
                PythonModelSettings.EngineYoloV8 => OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Profile.Engine.YoloV8"),
                PythonModelSettings.EngineYolo11 => OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Profile.Engine.Yolo11"),
                PythonModelSettings.EngineOnnx => OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Profile.Engine.Onnx"),
                PythonModelSettings.EngineUnet => OpenVisionLab.OpenVisionLanguageService.T("WpfModelSettings.EngineName.Unet"),
                PythonModelSettings.EnginePatchCore => OpenVisionLab.OpenVisionLanguageService.T("WpfModelSettings.EngineName.PatchCore"),
                _ => OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Profile.Engine.YoloV5")
            };

            string protocol = settings?.GetProtocolModelName() ?? string.Empty;
            string registrySuffix = registry?.Profiles?.Count > 0
                ? string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Profile.RegisteredCount"), registry.Profiles.Count)
                : string.Empty;
            string text = string.IsNullOrWhiteSpace(protocol)
                ? string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Profile.Value"), profileName)
                : string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Profile.WithProtocol"), profileName, protocol);
            return text + registrySuffix;
        }

        private static string BuildSummaryPrimaryText(
            PythonModelSettings settings,
            WpfTrainingWeightsComparison comparison,
            ModelRegistrySettings registry,
            bool hasPendingSelection)
        {
            string currentPath = ResolveCurrentInspectionWeightsPath(settings, registry, hasPendingSelection);
            string current = ModelRegistryTextPresentationService.FormatModelPath(currentPath);
            if (string.IsNullOrWhiteSpace(current))
            {
                current = OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Summary.Current.None");
            }

            ModelCandidate latestRecord = ModelRegistryService.FindLatestCandidate(registry);
            string candidatePath = comparison?.LatestWeightsPath?.Trim() ?? latestRecord?.WeightsPath?.Trim() ?? string.Empty;
            string candidate = ModelRegistryTextPresentationService.FormatModelPath(candidatePath);
            string candidateText;
            if (string.IsNullOrWhiteSpace(candidate))
            {
                candidateText = OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Summary.TrainedModelNone");
            }
            else if (!hasPendingSelection
                && !string.IsNullOrWhiteSpace(currentPath)
                && string.Equals(candidatePath, currentPath, StringComparison.OrdinalIgnoreCase))
            {
                candidateText = OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Summary.TrainedModelSame");
            }
            else
            {
                candidateText = string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Summary.TrainedModelValue"), candidate);
            }

            return string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Summary.Primary"), current, candidateText);
        }

        private static string BuildSummarySecondaryText(
            PythonModelSettings settings,
            YoloTrainingGuideHistory history,
            ModelRegistrySettings registry,
            bool hasPendingSelection,
            PythonModelRuntimeState runtimeState)
        {
            string profile = BuildSelectedRuntimeSummaryText(settings, runtimeState);

            string trainingState = OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Summary.Training.None");
            TrainingRun latestRun = ModelRegistryService.FindLatestTrainingRun(registry);
            if (latestRun != null)
            {
                trainingState = ModelRegistryTextPresentationService.FormatTrainingState(latestRun.State);
            }
            else if (!string.IsNullOrWhiteSpace(history?.LastTrainingState))
            {
                trainingState = ModelRegistryTextPresentationService.FormatTrainingState(history.LastTrainingState);
                if (history.LastTrainingProgressPercent >= 0)
                {
                    trainingState += $" {Math.Clamp(history.LastTrainingProgressPercent, 0, 100)}%";
                }
            }

            int historyCount = registry?.Candidates?.Count ?? 0;
            ModelCandidate latestCandidate = ModelRegistryService.FindLatestCandidate(registry);
            string candidateState = hasPendingSelection
                ? OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Summary.Selection.Pending")
                : latestCandidate?.SavedToRecipe == true
                    ? OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Summary.Selection.Saved")
                    : latestCandidate != null
                        ? OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Summary.Selection.Unsaved")
                        : OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Summary.Selection.None");

            return string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Summary.Secondary"), profile, trainingState, historyCount, candidateState);
        }

        public static string BuildSelectedRuntimeSummaryText(PythonModelSettings settings)
            => BuildSelectedRuntimeSummaryText(settings, null);

        public static string BuildSelectedRuntimeSummaryText(PythonModelSettings settings, PythonModelRuntimeState runtimeState)
        {
            settings ??= new PythonModelSettings();
            PythonModelRuntimeProfile selectedProfile = PythonModelRuntimeProfileService
                .BuildProfiles(settings)
                .FirstOrDefault(item => item?.IsSelected == true);

            string runtimeName = BuildRuntimeName(selectedProfile, settings.ModelEngine);
            string readinessText = runtimeState == null
                ? BuildRuntimeReadinessText(selectedProfile)
                : BuildRuntimeReadinessText(runtimeState.CanRunInference, runtimeState.CanRunTraining, runtimeState.State != PythonModelRuntimeStateKind.NotInstalled);
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
                return OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Runtime.Unknown");
            }

            return BuildRuntimeReadinessText(selectedProfile.CanInspect, selectedProfile.CanTrain, selectedProfile.IsRuntimeConnected);
        }

        private static string BuildRuntimeReadinessText(bool canInspect, bool canTrain, bool isConnected)
        {
            if (canInspect && canTrain)
            {
                return OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Runtime.Ready");
            }

            if (canInspect)
            {
                return OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Runtime.InferenceOnly");
            }

            if (canTrain)
            {
                return OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Runtime.TrainingOnly");
            }

            return isConnected
                ? OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Runtime.Settings")
                : OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Runtime.Install");
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
            string latestModel = ModelRegistryTextPresentationService.FormatModelPath(comparison?.LatestWeightsPath);
            TrainingRun latestRun = ModelRegistryService.FindLatestTrainingRun(registry);
            if (history == null || string.IsNullOrWhiteSpace(history.LastTrainingState))
            {
                if (latestRun != null)
                {
                    string runModel = ModelRegistryTextPresentationService.FormatModelPath(latestRun.CandidateWeightsPath);
                    string metrics = string.IsNullOrWhiteSpace(latestRun.MetricsSummary)
                        ? string.Empty
                        : $" / {latestRun.MetricsSummary}";
                    return string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.TrainingRun.Value"), ModelRegistryTextPresentationService.FormatTrainingState(latestRun.State), runModel, metrics);
                }

                return string.IsNullOrWhiteSpace(latestModel)
                    ? OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.TrainingRun.None")
                    : string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.TrainingRun.Result"), latestModel);
            }

            string progress = history.LastTrainingProgressPercent >= 0
                ? $" {Math.Clamp(history.LastTrainingProgressPercent, 0, 100)}%"
                : string.Empty;
            string time = ModelRegistryTextPresentationService.FormatLocalTime(history.LastTrainingUpdateUtc);
            string suffix = string.IsNullOrWhiteSpace(latestModel)
                ? string.Empty
                : string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.TrainingRun.ResultSuffix"), latestModel);
            return string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.TrainingRun.History"), ModelRegistryTextPresentationService.FormatTrainingState(history.LastTrainingState), progress, time, suffix).TrimEnd();
        }

        private static Func<string> BuildCandidateModelTextSource(WpfTrainingWeightsComparison comparison, string currentWeightsPath, ModelRegistrySettings registry)
        {
            ModelCandidate latestRecord = ModelRegistryService.FindLatestCandidate(registry);
            bool hasLatestWeights = comparison?.HasLatestWeights == true;
            bool hasRecord = latestRecord != null;
            if (!hasLatestWeights && !hasRecord) return () => OpenVisionLab.OpenVisionLanguageService.T("WpfModelCenterDashboard.Candidate.None");
            string latest = comparison?.LatestWeightsPath?.Trim() ?? latestRecord?.WeightsPath?.Trim() ?? string.Empty;
            string current = currentWeightsPath?.Trim() ?? string.Empty;
            string display = ModelRegistryTextPresentationService.FormatModelPath(latest);
            bool isSame = !string.IsNullOrWhiteSpace(current) && string.Equals(latest, current, StringComparison.OrdinalIgnoreCase);
            string compactMetrics = BuildCompactMetricSummary(comparison?.MetricsStatusText ?? latestRecord?.MetricsSummary);
            string decision = latestRecord?.Decision ?? string.Empty;
            string reason = latestRecord?.DecisionSummary ?? string.Empty;
            bool savedToRecipe = latestRecord?.SavedToRecipe == true;
            return () =>
            {
                string sameText = OpenVisionLab.OpenVisionLanguageService.T(isSame ? "WpfModelRegistrySummary.Candidate.SameSelected" : "WpfModelRegistrySummary.Candidate.CompareBeforeSave");
                string metrics = string.IsNullOrWhiteSpace(compactMetrics) ? string.Empty : $" / {compactMetrics}";
                string decisionText = hasRecord ? FormatCandidateDecision(decision, reason, false) : string.Empty;
                string savedText = savedToRecipe ? OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Candidate.Saved")
                    : hasRecord ? OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Candidate.Pending") : string.Empty;
                return string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Candidate.Value"), display, metrics, sameText, savedText, decisionText);
            };
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
            bool hasPendingSelection,
            PythonModelRuntimeState runtimeState)
        {
            string weightsPath = settings?.WeightsPath ?? string.Empty;
            string runtimeSummaryText = BuildSelectedRuntimeSummaryText(settings, runtimeState);
            ModelCandidate currentRecord = ModelRegistryService.FindCurrentInspectionModel(registry);
            string sourcePath = !string.IsNullOrWhiteSpace(currentRecord?.WeightsPath)
                ? currentRecord.WeightsPath
                : hasPendingSelection
                    ? string.Empty
                    : weightsPath;
            string display = ModelRegistryTextPresentationService.FormatModelPath(sourcePath);
            if (string.IsNullOrWhiteSpace(display))
            {
                return hasPendingSelection
                    ? string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Inspection.NonePending"), runtimeSummaryText)
                    : string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelCenterDashboard.Current.NoneRuntime"), runtimeSummaryText);
            }

            string state = currentRecord?.SavedToRecipe == true
                    ? OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Inspection.Saved")
                    : File.Exists(sourcePath ?? string.Empty)
                    ? OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Inspection.FileExists")
                    : OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Inspection.CheckFile");
            if (hasPendingSelection)
            {
                state += OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Inspection.SelectionPending");
            }

            return string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Inspection.Value"), display, runtimeSummaryText, state);
        }

        private static Func<string> BuildActionTextSource(WpfTrainingWeightsComparison comparison, string currentWeightsPath, ModelRegistrySettings registry, bool hasPendingSelection)
        {
            if (hasPendingSelection) return () => OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Action.SavePending");
            if (comparison?.HasLatestWeights == true
                && !string.Equals(comparison.LatestWeightsPath?.Trim() ?? string.Empty, currentWeightsPath?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                return () => OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Action.Compare");
            int adoptionCount = registry?.AdoptionHistory?.Count ?? 0;
            int decisionCount = registry?.CandidateDecisions?.Count ?? 0;
            if (adoptionCount > 0 || decisionCount > 0)
            {
                ModelCandidateDecision latest = ModelRegistryService.FindLatestCandidateDecision(registry);
                string decision = latest?.Decision ?? string.Empty;
                string reason = latest?.DecisionSummary ?? string.Empty;
                return () =>
                {
                    string latestDecision = FormatCandidateDecision(decision, reason, true);
                    string decisionSuffix = decisionCount > 0
                        ? string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Action.DecisionCount"), decisionCount, latestDecision)
                        : string.Empty;
                    string adoptionSuffix = adoptionCount > 0
                        ? string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Action.AssignmentCount"), adoptionCount)
                        : string.Empty;
                    return string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Action.StructureWithHistory"), adoptionSuffix, decisionSuffix);
                };
            }
            return () => OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Action.Structure");
        }

        private static string FormatCandidateDecision(string decision, string rawSummary, bool isDecisionRecord)
        {
            string text = ModelRegistryTextPresentationService.FormatCandidateDecisionText(decision);
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            string reason = ModelRegistryTextPresentationService.FormatPendingDecisionReason(decision, rawSummary);
            string summary = string.IsNullOrWhiteSpace(rawSummary) ? string.Empty : isDecisionRecord ? $" {reason}" : $" ({reason})";
            return string.Format(OpenVisionLab.OpenVisionLanguageService.T(isDecisionRecord ? "WpfModelRegistrySummary.Decision.Latest" : "WpfModelRegistrySummary.Decision.Value"), text, summary);
        }

    }

    [Obsolete("Use ModelRegistrySummaryPresentationService.", false)]
    public static class WpfModelRegistrySummaryPresentationService
    {
        public static WpfModelRegistryPresentation Build(
            PythonModelSettings settings,
            WpfTrainingWeightsComparison comparison,
            YoloTrainingGuideHistory history,
            ModelRegistrySettings registry,
            bool hasPendingInspectionModelSelection)
            => ModelRegistrySummaryPresentationService.Build(settings, comparison, history, registry, hasPendingInspectionModelSelection);

        public static string BuildSelectedRuntimeSummaryText(PythonModelSettings settings)
            => ModelRegistrySummaryPresentationService.BuildSelectedRuntimeSummaryText(settings);

        public static string BuildCompactMetricSummary(string metricsText)
            => ModelRegistrySummaryPresentationService.BuildCompactMetricSummary(metricsText);
    }
}
