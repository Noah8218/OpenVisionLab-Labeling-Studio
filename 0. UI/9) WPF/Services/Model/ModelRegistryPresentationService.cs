using System;
using MvcVisionSystem._1._Core;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem
{
    /// <summary>
    /// Compatibility entry point for Model Registry presentation and state.
    /// Summary/runtime formatting lives in ModelRegistrySummaryPresentationService;
    /// history formatting lives in ModelRegistryHistoryPresentationService.
    /// </summary>
    public static class ModelRegistryPresentationService
    {
        public static WpfModelRegistryHistoryItem FindHistorySelection(
            IEnumerable<WpfModelRegistryHistoryItem> items,
            string candidateId,
            string weightsPath)
            => ModelRegistryHistoryPresentationService.FindHistorySelection(items, candidateId, weightsPath);

        public static WpfModelRegistryHistorySelectionPresentation BuildSelectedHistoryPresentation(
            IEnumerable<WpfModelRegistryHistoryItem> items,
            WpfModelRegistryHistoryItem selected)
            => ModelRegistryHistoryPresentationService.BuildSelectedHistoryPresentation(items, selected);

        public static WpfModelRegistryPresentation Build(
            PythonModelSettings settings,
            WpfTrainingWeightsComparison comparison,
            YoloTrainingGuideHistory history,
            bool hasPendingInspectionModelSelection)
            => ModelRegistrySummaryPresentationService.Build(
                settings,
                comparison,
                history,
                null,
                hasPendingInspectionModelSelection);

        public static WpfModelRegistryPresentation Build(
            PythonModelSettings settings,
            WpfTrainingWeightsComparison comparison,
            YoloTrainingGuideHistory history,
            ModelRegistrySettings registry,
            bool hasPendingInspectionModelSelection)
            => ModelRegistrySummaryPresentationService.Build(
                settings,
                comparison,
                history,
                registry,
                hasPendingInspectionModelSelection);

        public static WpfModelRegistryPresentation Build(
            PythonModelSettings settings,
            WpfTrainingWeightsComparison comparison,
            YoloTrainingGuideHistory history,
            ModelRegistrySettings registry,
            bool hasPendingInspectionModelSelection,
            PythonModelRuntimeState runtimeState)
            => ModelRegistrySummaryPresentationService.Build(settings, comparison, history, registry, hasPendingInspectionModelSelection, runtimeState);

        public static string BuildSelectedRuntimeSummaryText(PythonModelSettings settings)
            => ModelRegistrySummaryPresentationService.BuildSelectedRuntimeSummaryText(settings);

        public static string BuildSelectedRuntimeSummaryText(PythonModelSettings settings, PythonModelRuntimeState runtimeState)
            => ModelRegistrySummaryPresentationService.BuildSelectedRuntimeSummaryText(settings, runtimeState);

        public static string BuildCompactMetricSummary(string metricsText)
            => ModelRegistrySummaryPresentationService.BuildCompactMetricSummary(metricsText);

        public static ModelRegistryState BuildState(
            WpfModelRegistryPresentation presentation,
            string selectedCandidateId,
            string selectedWeightsPath)
        {
            IReadOnlyList<WpfModelRegistryHistoryItem> historyItems =
                (presentation?.HistoryItems ?? Array.Empty<WpfModelRegistryHistoryItem>())
                .Where(item => item != null)
                .ToArray();
            WpfModelRegistryHistoryItem selectedHistoryItem = ModelRegistryHistoryPresentationService.FindHistorySelection(
                historyItems,
                selectedCandidateId,
                selectedWeightsPath)
                ?? historyItems.FirstOrDefault();
            int historyCount = historyItems.Count;

            return new ModelRegistryState
            {
                SummaryPrimaryText = NormalizeRegistryText(
                    presentation?.SummaryPrimaryText,
                    OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Summary.Empty")),
                SummarySecondaryText = NormalizeRegistryText(
                    presentation?.SummarySecondaryText,
                    OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.State.EmptySecondary")),
                ProfileText = NormalizeRegistryText(
                    presentation?.ProfileText,
                    OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Profile.Unset")),
                TrainingRunText = NormalizeRegistryText(
                    presentation?.TrainingRunText,
                    OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.TrainingRun.None")),
                CandidateModelText = NormalizeRegistryText(
                    presentation?.CandidateModelText,
                    OpenVisionLab.OpenVisionLanguageService.T("WpfModelCenterDashboard.Candidate.None")),
                InspectionModelText = NormalizeRegistryText(
                    presentation?.InspectionModelText,
                    OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.State.InspectionNone")),
                ActionText = NormalizeRegistryText(
                    presentation?.ActionText,
                    OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistrySummary.Action.Structure")),
                HistoryHeaderText = historyCount <= 0
                    ? OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.State.HistoryHeaderEmpty")
                    : string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.State.HistoryHeaderCount"), historyCount),
                HistorySummaryText = historyCount <= 0
                    ? OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.History.EmptySummary")
                    : OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.History.Summary"),
                IsHistoryVisible = historyCount > 0,
                HistoryItems = historyItems,
                SelectedHistoryItem = selectedHistoryItem,
                SelectedHistoryPresentation = ModelRegistryHistoryPresentationService.BuildSelectedHistoryPresentation(historyItems, selectedHistoryItem)
            };
        }

        internal static string NormalizeRegistryText(string value, string fallback)
            => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    [Obsolete("Use ModelRegistryPresentationService.", false)]
    public static class WpfModelRegistryPresentationService
    {
        public static WpfModelRegistryHistoryItem FindHistorySelection(
            IEnumerable<WpfModelRegistryHistoryItem> items,
            string candidateId,
            string weightsPath)
            => ModelRegistryPresentationService.FindHistorySelection(items, candidateId, weightsPath);

        public static WpfModelRegistryHistorySelectionPresentation BuildSelectedHistoryPresentation(
            IEnumerable<WpfModelRegistryHistoryItem> items,
            WpfModelRegistryHistoryItem selected)
            => ModelRegistryPresentationService.BuildSelectedHistoryPresentation(items, selected);

        public static WpfModelRegistryPresentation Build(
            PythonModelSettings settings,
            WpfTrainingWeightsComparison comparison,
            YoloTrainingGuideHistory history,
            bool hasPendingInspectionModelSelection)
            => ModelRegistryPresentationService.Build(settings, comparison, history, hasPendingInspectionModelSelection);

        public static WpfModelRegistryPresentation Build(
            PythonModelSettings settings,
            WpfTrainingWeightsComparison comparison,
            YoloTrainingGuideHistory history,
            ModelRegistrySettings registry,
            bool hasPendingInspectionModelSelection)
            => ModelRegistryPresentationService.Build(settings, comparison, history, registry, hasPendingInspectionModelSelection);

        public static string BuildSelectedRuntimeSummaryText(PythonModelSettings settings)
            => ModelRegistryPresentationService.BuildSelectedRuntimeSummaryText(settings);

        public static string BuildCompactMetricSummary(string metricsText)
            => ModelRegistryPresentationService.BuildCompactMetricSummary(metricsText);

        public static WpfModelRegistryState BuildState(
            WpfModelRegistryPresentation presentation,
            string selectedCandidateId,
            string selectedWeightsPath)
            => WpfModelRegistryState.FromCanonical(ModelRegistryPresentationService.BuildState(presentation, selectedCandidateId, selectedWeightsPath));
    }
}
