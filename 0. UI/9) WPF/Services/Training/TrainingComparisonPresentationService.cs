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
                return OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.TrainingComparisonPresentationService.Initial");
            }

            if (!comparison.HasLatestWeights)
            {
                return OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.TrainingComparisonPresentationService.NoResult");
            }

            if (string.Equals(comparison.LatestWeightsPath?.Trim(), comparison.CurrentWeightsPath?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.TrainingComparisonPresentationService.Current");
            }

            if (comparison.LatestMetrics?.HasScore != true)
            {
                return OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.TrainingComparisonPresentationService.NoMetrics");
            }

            if (comparison.CurrentMetrics?.HasScore != true)
            {
                return OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.TrainingComparisonPresentationService.CurrentMetrics");
            }

            return comparison.MetricVerdictText switch
            {
                "새 모델 우세" => comparison.ShouldApplyLatest
                    ? OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.TrainingComparisonPresentationService.NewStronger")
                    : OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.TrainingComparisonPresentationService.FilesNeeded"),
                "현재 모델 우세" => OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.TrainingComparisonPresentationService.KeepCurrent"),
                "동률" => OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.TrainingComparisonPresentationService.Tie"),
                _ => OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.TrainingComparisonPresentationService.TestNeeded")
            };
        }

        public static IReadOnlyList<WpfTrainingResultReportItem> BuildResultReportItems(WpfTrainingWeightsComparison comparison)
        {
            if (comparison == null)
            {
                return Array.Empty<WpfTrainingResultReportItem>();
            }

            string verdict = string.IsNullOrWhiteSpace(comparison.MetricVerdictText)
                ? OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.TrainingComparisonPresentationService.Waiting")
                : comparison.MetricVerdictText;
            string decision = comparison.ShouldApplyLatest
                ? OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.TrainingComparisonPresentationService.NewModel")
                : comparison.HasLatestWeights
                    ? OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.TrainingComparisonPresentationService.Keep")
                    : OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.TrainingComparisonPresentationService.ResultNone");
            bool hasMetrics = comparison.LatestMetrics?.HasScore == true;

            return new List<WpfTrainingResultReportItem>
            {
                new WpfTrainingResultReportItem(
                    OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.TrainingComparisonPresentationService.Decision"),
                    verdict,
                    decision,
                    hasMetrics ? PackIconMaterialKind.CheckCircleOutline : PackIconMaterialKind.AlertCircleOutline,
                    isWarning: !hasMetrics),
                new WpfTrainingResultReportItem(
                    OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.TrainingComparisonPresentationService.Metrics"),
                    FormatMetricValue(comparison),
                    hasMetrics ? OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.TrainingComparisonPresentationService.MetricHelp") : OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.TrainingComparisonPresentationService.MissingCsv"),
                    PackIconMaterialKind.ProgressClock,
                    isWarning: !hasMetrics),
                new WpfTrainingResultReportItem(
                    OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.TrainingComparisonPresentationService.Candidate"),
                    FormatPath(comparison.LatestWeightsPath),
                    FormatCandidateMetricSource(comparison),
                    PackIconMaterialKind.FileDocumentOutline,
                    isWarning: !comparison.HasLatestWeights),
                new WpfTrainingResultReportItem(
                    OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.TrainingComparisonPresentationService.Baseline"),
                    FormatPath(comparison.CurrentWeightsPath),
                    FormatMetricSource(comparison.CurrentMetrics),
                    PackIconMaterialKind.RobotIndustrial)
            };
        }

        private static string FormatMetricValue(WpfTrainingWeightsComparison comparison)
        {
            if (comparison?.LatestMetrics?.HasScore != true)
            {
                return OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.TrainingComparisonPresentationService.MetricsNone");
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
                : OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.TrainingComparisonPresentationService.MetricsAvailable");
        }

        private static string FormatMetricSource(WpfTrainingRunMetrics metrics)
        {
            string source = string.IsNullOrWhiteSpace(metrics?.ResultsCsvPath)
                ? OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.TrainingComparisonPresentationService.CsvNone")
                : $"results.csv: {Path.GetFileName(Path.GetDirectoryName(metrics.ResultsCsvPath) ?? metrics.ResultsCsvPath)}";
            string checkpoint = TrainingWeightsService.FormatCheckpointIdentity(metrics);
            return string.IsNullOrWhiteSpace(checkpoint) ? source : $"{source} / {checkpoint}";
        }

        private static string FormatCandidateMetricSource(WpfTrainingWeightsComparison comparison)
        {
            string source = FormatMetricSource(comparison?.LatestMetrics);
            if (comparison?.LatestLastMetrics == null)
            {
                return source;
            }

            return $"{source} / last.pt {FormatCheckpointIdentity(comparison.LatestLastMetrics)}";
        }

        private static string FormatCheckpointIdentity(WpfTrainingRunMetrics metrics)
            => TrainingWeightsService.FormatCheckpointIdentity(metrics);

        private static string FormatPath(string path)
            => string.IsNullOrWhiteSpace(path) ? OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.TrainingComparisonPresentationService.None") : Path.GetFileName(path);

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
