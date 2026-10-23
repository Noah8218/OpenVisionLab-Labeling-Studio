using System;
using System.Collections.Generic;
using System.Globalization;
using MvcVisionSystem.Yolo;

namespace MvcVisionSystem
{
    public sealed class WpfAnomalyClassificationEvaluationPresentation
    {
        public string RecommendationText { get; set; } = string.Empty;

        public string MetricsText { get; set; } = string.Empty;

        public string DetailText { get; set; } = string.Empty;

        public string ActionText { get; set; } = string.Empty;
    }

    public static class AnomalyClassificationEvaluationPresentationService
    {
        public static WpfAnomalyClassificationEvaluationPresentation Build(
            AnomalyClassificationEvaluationReport report,
            AnomalyClassificationEvaluationOptions options = null)
        {
            report ??= new AnomalyClassificationEvaluationReport();
            options ??= new AnomalyClassificationEvaluationOptions();

            bool canAdopt = report.IsAdoptionCandidate;
            return new WpfAnomalyClassificationEvaluationPresentation
            {
                RecommendationText = canAdopt
                    ? OpenVisionLab.OpenVisionLanguageService.T("WpfAnomaly.Evaluation.Recommendation.Ready")
                    : OpenVisionLab.OpenVisionLanguageService.T("WpfAnomaly.Evaluation.Recommendation.Held"),
                MetricsText = BuildMetricsText(report),
                DetailText = canAdopt
                    ? BuildAdoptDetailText(options)
                    : BuildHoldDetailText(report, options),
                ActionText = canAdopt
                    ? OpenVisionLab.OpenVisionLanguageService.T("WpfAnomaly.Evaluation.Action.Assign")
                    : OpenVisionLab.OpenVisionLanguageService.T("WpfAnomaly.Evaluation.Action.Hold")
            };
        }

        private static string BuildMetricsText(AnomalyClassificationEvaluationReport report)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                OpenVisionLab.OpenVisionLanguageService.T("WpfAnomaly.Evaluation.Metrics"),
                FormatModelName(report.ModelName),
                report.TotalImageCount,
                report.NormalCorrectCount,
                report.NormalImageCount,
                report.AbnormalCorrectCount,
                report.AbnormalImageCount,
                FormatPercent(report.BalancedAccuracy),
                report.FalsePositiveCount,
                report.FalseNegativeCount,
                report.LowConfidenceClassMatchCount);
        }

        private static string FormatModelName(string modelName)
        {
            return (modelName ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "yolov8" => "YOLOv8",
                "yolo11" => "YOLO11",
                "patchcore" => "PatchCore",
                _ => OpenVisionLab.OpenVisionLanguageService.T("WpfAnomaly.Evaluation.Model.Unrecorded")
            };
        }

        private static string BuildAdoptDetailText(AnomalyClassificationEvaluationOptions options)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                OpenVisionLab.OpenVisionLanguageService.T("WpfAnomaly.Evaluation.Detail.Passed"),
                Math.Max(1, options.MinimumTotalImageCount),
                Math.Max(1, options.MinimumPerClassImageCount),
                FormatPercent(options.MinimumAccuracy),
                FormatPercent(options.MinimumPerClassAccuracy),
                FormatPercent(options.MinimumConfidence));
        }

        private static string BuildHoldDetailText(
            AnomalyClassificationEvaluationReport report,
            AnomalyClassificationEvaluationOptions options)
        {
            var blockers = new List<string>();
            if (report.TotalImageCount < Math.Max(1, options.MinimumTotalImageCount))
            {
                blockers.Add(string.Format(CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfAnomaly.Evaluation.Detail.ImageCount"), report.TotalImageCount, Math.Max(1, options.MinimumTotalImageCount)));
            }

            if (report.NormalImageCount < Math.Max(1, options.MinimumPerClassImageCount))
            {
                blockers.Add(string.Format(CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfAnomaly.Evaluation.Detail.NormalCount"), report.NormalImageCount, Math.Max(1, options.MinimumPerClassImageCount)));
            }

            if (report.AbnormalImageCount < Math.Max(1, options.MinimumPerClassImageCount))
            {
                blockers.Add(string.Format(CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfAnomaly.Evaluation.Detail.AbnormalCount"), report.AbnormalImageCount, Math.Max(1, options.MinimumPerClassImageCount)));
            }

            if (report.Accuracy < Clamp01(options.MinimumAccuracy))
            {
                blockers.Add(string.Format(CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfAnomaly.Evaluation.Detail.Accuracy"), FormatPercent(report.Accuracy), FormatPercent(options.MinimumAccuracy)));
            }

            if (report.NormalAccuracy < Clamp01(options.MinimumPerClassAccuracy))
            {
                blockers.Add(string.Format(CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfAnomaly.Evaluation.Detail.NormalAccuracy"), FormatPercent(report.NormalAccuracy), FormatPercent(options.MinimumPerClassAccuracy)));
            }

            if (report.AbnormalAccuracy < Clamp01(options.MinimumPerClassAccuracy))
            {
                blockers.Add(string.Format(CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfAnomaly.Evaluation.Detail.AbnormalAccuracy"), FormatPercent(report.AbnormalAccuracy), FormatPercent(options.MinimumPerClassAccuracy)));
            }

            if (report.LowConfidenceClassMatchCount > 0)
            {
                blockers.Add(string.Format(CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfAnomaly.Evaluation.Detail.Confidence"), report.LowConfidenceClassMatchCount, FormatPercent(options.MinimumConfidence)));
            }

            return blockers.Count == 0
                ? OpenVisionLab.OpenVisionLanguageService.T("WpfAnomaly.Evaluation.Detail.HoldReport")
                : OpenVisionLab.OpenVisionLanguageService.T("WpfAnomaly.Evaluation.Detail.HoldPrefix") + " " + string.Join(" / ", blockers);
        }

        private static string FormatPercent(double value)
        {
            double percent = Clamp01(value) * 100D;
            return percent.ToString("0.#", CultureInfo.InvariantCulture) + "%";
        }

        private static double Clamp01(double value)
            => double.IsNaN(value) ? 0D : Math.Clamp(value, 0D, 1D);
    }

    [Obsolete("Use AnomalyClassificationEvaluationPresentationService.", false)]
    public static class WpfAnomalyClassificationEvaluationPresentationService
    {
        public static WpfAnomalyClassificationEvaluationPresentation Build(
            AnomalyClassificationEvaluationReport report,
            AnomalyClassificationEvaluationOptions options = null)
            => AnomalyClassificationEvaluationPresentationService.Build(report, options);
    }
}
