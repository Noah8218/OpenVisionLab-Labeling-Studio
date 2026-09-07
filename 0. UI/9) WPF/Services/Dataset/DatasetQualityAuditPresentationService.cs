using MahApps.Metro.IconPacks;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem
{
    public static class DatasetQualityAuditPresentationService
    {
        public static WpfDatasetDashboardMetricItem BuildQualityMetric(YoloDatasetQualityAuditReport report)
        {
            report ??= new YoloDatasetQualityAuditReport();
            int problemCount = report.TotalMissingLabelCount + report.TotalInvalidLabelLineCount;
            bool hasArtifacts = report.TotalImageCount > 0 || report.TotalLabelFileCount > 0;
            string value = problemCount > 0
                ? problemCount.ToString()
                : hasArtifacts ? "OK" : "-";
            string statusKey = !hasArtifacts
                ? "WpfLearningWorkflow.DatasetDashboard.Metric.State.Waiting"
                : problemCount > 0
                    ? "WpfLearningWorkflow.DatasetDashboard.Metric.State.CleanupNeeded"
                    : "WpfLearningWorkflow.DatasetDashboard.Metric.State.Check";
            string classSummary = BuildClassDistributionSummary(report.ObjectCountByClass);
            string detailKey = string.IsNullOrWhiteSpace(classSummary)
                ? "WpfLearningWorkflow.DatasetDashboard.Metric.Quality.Detail.NoClasses"
                : "WpfLearningWorkflow.DatasetDashboard.Metric.Quality.Detail.Classes";

            return DatasetDashboardLocalizationService.CreateMetric(
                "WpfLearningWorkflow.DatasetDashboard.Metric.Quality.Title",
                value,
                detailKey,
                statusKey,
                problemCount > 0 ? PackIconMaterialKind.AlertCircleOutline : PackIconMaterialKind.CheckCircleOutline,
                isProblem: problemCount > 0,
                isWarning: false,
                actionKind: WpfDatasetDashboardActionKind.ExportQualityAudit,
                string.IsNullOrWhiteSpace(classSummary)
                    ? new object[] { report.TotalMissingLabelCount, report.TotalInvalidLabelLineCount, report.TotalEmptyLabelCount }
                    : new object[] { report.TotalMissingLabelCount, report.TotalInvalidLabelLineCount, report.TotalEmptyLabelCount, classSummary });
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

        private static string BuildClassDistributionSummary(IReadOnlyDictionary<string, int> classCounts)
        {
            if (classCounts == null || classCounts.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(
                ", ",
                classCounts
                    .OrderByDescending(item => item.Value)
                    .ThenBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                    .Take(3)
                    .Select(item => $"{item.Key} {item.Value}"));
        }
    }

    [Obsolete("Use DatasetQualityAuditPresentationService.", false)]
    public static class WpfDatasetQualityAuditPresentationService
    {
        public static WpfDatasetDashboardMetricItem BuildQualityMetric(YoloDatasetQualityAuditReport report)
            => DatasetQualityAuditPresentationService.BuildQualityMetric(report);

        public static string BuildQualityIssue(YoloDatasetQualityAuditReport report)
            => DatasetQualityAuditPresentationService.BuildQualityIssue(report);
    }
}
