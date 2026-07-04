using MahApps.Metro.IconPacks;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem
{
    public static class WpfDatasetQualityAuditPresentationService
    {
        public static WpfDatasetDashboardMetricItem BuildQualityMetric(YoloDatasetQualityAuditReport report)
        {
            report ??= new YoloDatasetQualityAuditReport();
            int problemCount = report.TotalMissingLabelCount + report.TotalInvalidLabelLineCount;
            bool hasArtifacts = report.TotalImageCount > 0 || report.TotalLabelFileCount > 0;
            string value = problemCount > 0
                ? problemCount.ToString()
                : hasArtifacts ? "OK" : "-";
            string status = !hasArtifacts
                ? "\uB300\uAE30"
                : problemCount > 0
                    ? "\uC815\uB9AC \uD544\uC694"
                    : "\uD655\uC778";

            return new WpfDatasetDashboardMetricItem(
                "\uD488\uC9C8",
                value,
                BuildQualityMetricDetail(report),
                status,
                problemCount > 0 ? PackIconMaterialKind.AlertCircleOutline : PackIconMaterialKind.CheckCircleOutline,
                isProblem: problemCount > 0,
                isWarning: false,
                actionKind: WpfDatasetDashboardActionKind.CheckDataset);
        }

        public static string BuildQualityIssue(YoloDatasetQualityAuditReport report)
        {
            report ??= new YoloDatasetQualityAuditReport();
            if (report.TotalMissingLabelCount <= 0 && report.TotalInvalidLabelLineCount <= 0)
            {
                return string.Empty;
            }

            return $"\uB2E4\uC74C: \uB204\uB77D \uB77C\uBCA8 {report.TotalMissingLabelCount}\uC7A5, invalid \uB77C\uBCA8 {report.TotalInvalidLabelLineCount}\uC904\uC744 \uC815\uB9AC\uD558\uC138\uC694.";
        }

        private static string BuildQualityMetricDetail(YoloDatasetQualityAuditReport report)
        {
            string classSummary = BuildClassDistributionSummary(report?.ObjectCountByClass);
            string qualitySummary = $"\uB204\uB77D {report?.TotalMissingLabelCount ?? 0}\uC7A5, invalid {report?.TotalInvalidLabelLineCount ?? 0}\uC904, \uBE48 \uC815\uC0C1 {report?.TotalEmptyLabelCount ?? 0}\uC7A5";
            return string.IsNullOrWhiteSpace(classSummary)
                ? qualitySummary
                : qualitySummary + ", " + classSummary;
        }

        private static string BuildClassDistributionSummary(IReadOnlyDictionary<string, int> classCounts)
        {
            if (classCounts == null || classCounts.Count == 0)
            {
                return string.Empty;
            }

            return "\uD074\uB798\uC2A4 " + string.Join(
                ", ",
                classCounts
                    .OrderByDescending(item => item.Value)
                    .ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                    .Take(3)
                    .Select(item => $"{item.Key} {item.Value}"));
        }
    }
}
