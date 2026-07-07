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

    public static class WpfAnomalyClassificationEvaluationPresentationService
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
                    ? "\uC774\uC0C1 \uBD84\uB958 \uD3C9\uAC00: \uCC44\uD0DD \uAC00\uB2A5"
                    : "\uC774\uC0C1 \uBD84\uB958 \uD3C9\uAC00: \uBCF4\uB958",
                MetricsText = BuildMetricsText(report),
                DetailText = canAdopt
                    ? BuildAdoptDetailText(options)
                    : BuildHoldDetailText(report, options),
                ActionText = canAdopt
                    ? "\uC608\uC2DC \uC774\uBBF8\uC9C0\uB97C \uAC80\uD1A0\uD55C \uB4A4 \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378\uB85C \uC800\uC7A5\uD558\uC138\uC694."
                    : "\uC815\uC0C1/\uC774\uC0C1 held-out \uC774\uBBF8\uC9C0\uB97C \uB354 \uBAA8\uC73C\uAC70\uB098 \uC7AC\uD559\uC2B5\uD55C \uB4A4 \uB2E4\uC2DC \uD3C9\uAC00\uD558\uC138\uC694."
            };
        }

        private static string BuildMetricsText(AnomalyClassificationEvaluationReport report)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "\uD3C9\uAC00 {0}\uC7A5 / \uC815\uC0C1 {1}/{2}, \uC774\uC0C1 {3}/{4} / \uC804\uCCB4 {5} / \uB0AE\uC740 \uC2E0\uB8B0\uB3C4 \uC815\uB2F5 {6}\uAC74",
                report.TotalImageCount,
                report.NormalCorrectCount,
                report.NormalImageCount,
                report.AbnormalCorrectCount,
                report.AbnormalImageCount,
                FormatPercent(report.Accuracy),
                report.LowConfidenceClassMatchCount);
        }

        private static string BuildAdoptDetailText(AnomalyClassificationEvaluationOptions options)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "\uAE30\uC900 \uD1B5\uACFC: \uCD5C\uC18C {0}\uC7A5, \uD074\uB798\uC2A4\uBCC4 {1}\uC7A5, \uC804\uCCB4 \uC815\uD655\uB3C4 {2}, \uD074\uB798\uC2A4\uBCC4 \uC815\uD655\uB3C4 {3}, \uCD5C\uC18C \uC2E0\uB8B0\uB3C4 {4} \uAE30\uC900\uC744 \uCDA9\uC871\uD588\uC2B5\uB2C8\uB2E4.",
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
                blockers.Add($"\uAC80\uC99D \uC774\uBBF8\uC9C0 {report.TotalImageCount}/{Math.Max(1, options.MinimumTotalImageCount)}\uC7A5");
            }

            if (report.NormalImageCount < Math.Max(1, options.MinimumPerClassImageCount))
            {
                blockers.Add($"\uC815\uC0C1 {report.NormalImageCount}/{Math.Max(1, options.MinimumPerClassImageCount)}\uC7A5");
            }

            if (report.AbnormalImageCount < Math.Max(1, options.MinimumPerClassImageCount))
            {
                blockers.Add($"\uC774\uC0C1 {report.AbnormalImageCount}/{Math.Max(1, options.MinimumPerClassImageCount)}\uC7A5");
            }

            if (report.Accuracy < Clamp01(options.MinimumAccuracy))
            {
                blockers.Add($"\uC804\uCCB4 \uC815\uD655\uB3C4 {FormatPercent(report.Accuracy)} / \uAE30\uC900 {FormatPercent(options.MinimumAccuracy)}");
            }

            if (report.NormalAccuracy < Clamp01(options.MinimumPerClassAccuracy))
            {
                blockers.Add($"\uC815\uC0C1 \uC815\uD655\uB3C4 {FormatPercent(report.NormalAccuracy)} / \uAE30\uC900 {FormatPercent(options.MinimumPerClassAccuracy)}");
            }

            if (report.AbnormalAccuracy < Clamp01(options.MinimumPerClassAccuracy))
            {
                blockers.Add($"\uC774\uC0C1 \uC815\uD655\uB3C4 {FormatPercent(report.AbnormalAccuracy)} / \uAE30\uC900 {FormatPercent(options.MinimumPerClassAccuracy)}");
            }

            if (report.LowConfidenceClassMatchCount > 0)
            {
                blockers.Add($"\uC2E0\uB8B0\uB3C4 \uBBF8\uB2EC \uC815\uB2F5 {report.LowConfidenceClassMatchCount}\uAC74 / \uAE30\uC900 {FormatPercent(options.MinimumConfidence)}");
            }

            return blockers.Count == 0
                ? "\uBCF4\uB958 \uC0AC\uC720\uB97C \uC0C1\uC138 \uD3C9\uAC00 \uB9AC\uD3EC\uD2B8\uC5D0\uC11C \uD655\uC778\uD558\uC138\uC694."
                : "\uBCF4\uB958 \uC0AC\uC720: " + string.Join(" / ", blockers);
        }

        private static string FormatPercent(double value)
        {
            double percent = Clamp01(value) * 100D;
            return percent.ToString("0.#", CultureInfo.InvariantCulture) + "%";
        }

        private static double Clamp01(double value)
            => double.IsNaN(value) ? 0D : Math.Clamp(value, 0D, 1D);
    }
}
