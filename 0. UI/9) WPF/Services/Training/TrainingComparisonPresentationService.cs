using MahApps.Metro.IconPacks;
using System;
using System.Collections.Generic;
using System.IO;

namespace MvcVisionSystem
{
    public class TrainingComparisonPresentation
    {
        public TrainingComparisonPresentation(
            string statusText,
            string summaryText,
            string adoptionDecisionText,
            IReadOnlyList<WpfTrainingResultReportItem> resultReportItems)
        {
            StatusText = statusText ?? string.Empty;
            SummaryText = summaryText ?? string.Empty;
            AdoptionDecisionText = adoptionDecisionText ?? string.Empty;
            ResultReportItems = resultReportItems ?? Array.Empty<WpfTrainingResultReportItem>();
        }

        public string StatusText { get; }

        public string SummaryText { get; }

        public string AdoptionDecisionText { get; }

        public IReadOnlyList<WpfTrainingResultReportItem> ResultReportItems { get; }
    }

    public static class TrainingComparisonPresentationService
    {
        public static TrainingComparisonPresentation Build(WpfTrainingWeightsComparison comparison)
        {
            return new TrainingComparisonPresentation(
                BuildComparisonStatusText(comparison),
                BuildComparisonSummaryText(comparison),
                BuildAdoptionDecisionText(comparison),
                BuildResultReportItems(comparison));
        }

        public static string BuildComparisonStatusText(WpfTrainingWeightsComparison comparison)
        {
            if (comparison == null)
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(comparison.MetricsStatusText)
                ? comparison.StatusText
                : $"{comparison.StatusText} / {comparison.MetricsStatusText}";
        }

        public static string BuildComparisonSummaryText(WpfTrainingWeightsComparison comparison)
        {
            if (comparison == null)
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(comparison.MetricsStatusText)
                ? comparison.StatusText
                : comparison.MetricsStatusText;
        }

        public static string BuildAdoptionDecisionText(WpfTrainingWeightsComparison comparison)
        {
            if (comparison == null)
            {
                return "교체 판단: 학습 결과 비교 전";
            }

            if (!comparison.HasLatestWeights)
            {
                return "교체 판단: 학습 결과 없음";
            }

            if (string.Equals(comparison.LatestWeightsPath?.Trim(), comparison.CurrentWeightsPath?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return "교체 판단: 이미 현재 검사 모델로 사용 중";
            }

            if (comparison.LatestMetrics?.HasScore != true)
            {
                return "교체 판단: 보류 - 학습 지표가 없어 채택 판단 불가";
            }

            if (comparison.CurrentMetrics?.HasScore != true)
            {
                return "교체 판단: 비교 필요 - 현재 모델 지표가 부족합니다";
            }

            return comparison.MetricVerdictText switch
            {
                "새 모델 우세" => comparison.ShouldApplyLatest
                    ? "교체 판단: 새 모델 후보 우세 - 최종 검증 예시 확인 후 저장"
                    : "교체 판단: 새 모델 지표 우세 - 파일 상태 확인 필요",
                "현재 모델 우세" => "교체 판단: 현재 모델 유지",
                "동률" => "교체 판단: 보류 - 차이가 작아 예시 확인 필요",
                _ => "교체 판단: 보류 - 최종 검증 비교 필요"
            };
        }

        public static IReadOnlyList<WpfTrainingResultReportItem> BuildResultReportItems(WpfTrainingWeightsComparison comparison)
        {
            if (comparison == null)
            {
                return Array.Empty<WpfTrainingResultReportItem>();
            }

            string verdict = string.IsNullOrWhiteSpace(comparison.MetricVerdictText)
                ? "비교 대기"
                : comparison.MetricVerdictText;
            string decision = comparison.ShouldApplyLatest
                ? "새 모델 후보"
                : comparison.HasLatestWeights
                    ? "현재 모델 유지"
                    : "학습 결과 없음";
            bool hasMetrics = comparison.LatestMetrics?.HasScore == true;

            return new List<WpfTrainingResultReportItem>
            {
                new WpfTrainingResultReportItem(
                    "판정",
                    verdict,
                    decision,
                    hasMetrics ? PackIconMaterialKind.CheckCircleOutline : PackIconMaterialKind.AlertCircleOutline,
                    isWarning: !hasMetrics),
                new WpfTrainingResultReportItem(
                    "지표",
                    FormatMetricValue(comparison),
                    hasMetrics ? "mAP50-95를 우선 보고 precision/recall을 함께 확인합니다." : "results.csv가 없으면 모델 교체 판단을 보류합니다.",
                    PackIconMaterialKind.ProgressClock,
                    isWarning: !hasMetrics),
                new WpfTrainingResultReportItem(
                    "새 후보",
                    FormatPath(comparison.LatestWeightsPath),
                    FormatMetricSource(comparison.LatestMetrics),
                    PackIconMaterialKind.FileDocumentOutline,
                    isWarning: !comparison.HasLatestWeights),
                new WpfTrainingResultReportItem(
                    "현재",
                    FormatPath(comparison.CurrentWeightsPath),
                    FormatMetricSource(comparison.CurrentMetrics),
                    PackIconMaterialKind.RobotIndustrial)
            };
        }

        private static string FormatMetricValue(WpfTrainingWeightsComparison comparison)
        {
            if (comparison?.LatestMetrics?.HasScore != true)
            {
                return "지표 없음";
            }

            WpfTrainingRunMetrics metrics = comparison.LatestMetrics;
            if (metrics.Map5095.HasValue)
            {
                return $"mAP50-95 {FormatPercent(metrics.Map5095.Value)}";
            }

            if (metrics.Map50.HasValue)
            {
                return $"mAP50 {FormatPercent(metrics.Map50.Value)}";
            }

            if (metrics.Precision.HasValue)
            {
                return $"precision {FormatPercent(metrics.Precision.Value)}";
            }

            return metrics.Recall.HasValue
                ? $"recall {FormatPercent(metrics.Recall.Value)}"
                : "지표 있음";
        }

        private static string FormatMetricSource(WpfTrainingRunMetrics metrics)
            => string.IsNullOrWhiteSpace(metrics?.ResultsCsvPath)
                ? "results.csv 없음"
                : $"results.csv: {Path.GetFileName(Path.GetDirectoryName(metrics.ResultsCsvPath) ?? metrics.ResultsCsvPath)}";

        private static string FormatPath(string path)
            => string.IsNullOrWhiteSpace(path) ? "없음" : Path.GetFileName(path);

        private static string FormatPercent(double value)
            => $"{(Math.Abs(value) <= 1.5D ? value * 100D : value):0.0}%";
    }

    [Obsolete("Use TrainingComparisonPresentation.", false)]
    public sealed class WpfTrainingComparisonPresentation : TrainingComparisonPresentation
    {
        public WpfTrainingComparisonPresentation(
            string statusText,
            string summaryText,
            string adoptionDecisionText,
            IReadOnlyList<WpfTrainingResultReportItem> resultReportItems)
            : base(statusText, summaryText, adoptionDecisionText, resultReportItems)
        {
        }
    }

    [Obsolete("Use TrainingComparisonPresentationService.", false)]
    public static class WpfTrainingComparisonPresentationService
    {
        public static WpfTrainingComparisonPresentation Build(WpfTrainingWeightsComparison comparison)
        {
            TrainingComparisonPresentation presentation = TrainingComparisonPresentationService.Build(comparison);
            return new WpfTrainingComparisonPresentation(
                presentation.StatusText,
                presentation.SummaryText,
                presentation.AdoptionDecisionText,
                presentation.ResultReportItems);
        }

        public static string BuildComparisonStatusText(WpfTrainingWeightsComparison comparison)
            => TrainingComparisonPresentationService.BuildComparisonStatusText(comparison);

        public static string BuildComparisonSummaryText(WpfTrainingWeightsComparison comparison)
            => TrainingComparisonPresentationService.BuildComparisonSummaryText(comparison);

        public static string BuildAdoptionDecisionText(WpfTrainingWeightsComparison comparison)
            => TrainingComparisonPresentationService.BuildAdoptionDecisionText(comparison);

        public static IReadOnlyList<WpfTrainingResultReportItem> BuildResultReportItems(WpfTrainingWeightsComparison comparison)
            => TrainingComparisonPresentationService.BuildResultReportItems(comparison);
    }
}
