using System;
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

        public static string BuildSelectedRuntimeSummaryText(PythonModelSettings settings)
            => ModelRegistrySummaryPresentationService.BuildSelectedRuntimeSummaryText(settings);

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
                    "현재 검사: 없음 / 학습 후보: 없음"),
                SummarySecondaryText = NormalizeRegistryText(
                    presentation?.SummarySecondaryText,
                    "YOLOv5 / 최근 학습 없음 / 이력 0건 / 후보 없음"),
                ProfileText = NormalizeRegistryText(
                    presentation?.ProfileText,
                    "모델 프로필: 미설정"),
                TrainingRunText = NormalizeRegistryText(
                    presentation?.TrainingRunText,
                    "최근 학습 실행: 없음"),
                CandidateModelText = NormalizeRegistryText(
                    presentation?.CandidateModelText,
                    "모델 후보: 없음"),
                InspectionModelText = NormalizeRegistryText(
                    presentation?.InspectionModelText,
                    "현재 검사 모델: 없음"),
                ActionText = NormalizeRegistryText(
                    presentation?.ActionText,
                    "구조: 모델 프로필 -> 학습 실행 -> 후보 모델 -> 현재 검사 모델로 분리해 관리합니다."),
                HistoryHeaderText = historyCount <= 0
                    ? "최근 모델 이력 0건"
                    : $"최근 모델 이력 {historyCount}건",
                HistorySummaryText = historyCount <= 0
                    ? "학습 후보를 저장하거나 거절하면 여기에 최근 이력이 표시됩니다."
                    : "학습 run, 후보 모델, 지표, 채택/거절 결정을 함께 비교합니다.",
                IsHistoryVisible = historyCount > 0,
                HistoryItems = historyItems,
                SelectedHistoryItem = selectedHistoryItem,
                SelectedHistoryPresentation = ModelRegistryHistoryPresentationService.BuildSelectedHistoryPresentation(historyItems, selectedHistoryItem)
            };
        }

        private static string NormalizeRegistryText(string value, string fallback)
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
