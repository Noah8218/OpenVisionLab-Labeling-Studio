using MvcVisionSystem._1._Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem.Yolo
{
    public static class YoloDatasetHealthService
    {
        public static YoloDatasetHealthReport Build(LabelingProjectData data)
        {
            data?.ProjectSettings?.EnsureDefaults();
            LabelingDatasetPurpose purpose = data?.ProjectSettings?.DatasetPurpose ?? LabelingDatasetPurpose.ObjectDetection;
            if (purpose == LabelingDatasetPurpose.AnomalyDetection)
            {
                return BuildAnomalyReport(data);
            }

            YoloDatasetReadinessReport readiness = YoloDatasetReadinessService.Build(data, refreshYaml: false);
            YoloDatasetQualityAuditReport qualityAudit = purpose == LabelingDatasetPurpose.ObjectDetection && data != null
                ? YoloDatasetQualityAuditService.Build(data)
                : null;
            IReadOnlyList<YoloDatasetHealthSplitSummary> splits = purpose == LabelingDatasetPurpose.Segmentation
                ? YoloDatasetHealthSplitSummaryService.BuildSegmentation(readiness)
                : YoloDatasetHealthSplitSummaryService.BuildDetection(qualityAudit);
            IReadOnlyList<YoloDatasetHealthClassSummary> classes = BuildClassSummaries(data, readiness?.Statistics, purpose);

            var issues = new List<string>();
            issues.AddRange(readiness?.Errors ?? Array.Empty<string>());
            issues.AddRange(YoloDatasetDiagnosticsService.BuildQualityWarnings(data, readiness?.Statistics));
            if (qualityAudit != null)
            {
                if (qualityAudit.TotalMissingLabelCount > 0)
                {
                    issues.Add($"dataset quality has {qualityAudit.TotalMissingLabelCount} missing label file(s)");
                }

                if (qualityAudit.TotalInvalidLabelLineCount > 0)
                {
                    issues.Add($"dataset quality has {qualityAudit.TotalInvalidLabelLineCount} invalid label line(s)");
                }
            }

            return new YoloDatasetHealthReport(
                purpose,
                readiness,
                anomalyReadiness: null,
                qualityAudit,
                splits,
                classes,
                NormalizeIssues(issues));
        }

        private static YoloDatasetHealthReport BuildAnomalyReport(LabelingProjectData data)
        {
            AnomalyClassificationTrainingReadinessReport readiness = AnomalyClassificationTrainingReadinessService.Build(data);
            var classes = new[]
            {
                new YoloDatasetHealthClassSummary("normal", readiness.NormalImageCount),
                new YoloDatasetHealthClassSummary("abnormal", readiness.AbnormalImageCount)
            };
            var issues = new List<string>(readiness.Errors ?? Array.Empty<string>());
            if (readiness.UnreviewedImageCount > 0)
            {
                issues.Add($"anomaly dataset has {readiness.UnreviewedImageCount} unreviewed image(s)");
            }

            return new YoloDatasetHealthReport(
                LabelingDatasetPurpose.AnomalyDetection,
                yoloReadiness: null,
                readiness,
                qualityAudit: null,
                splits: Array.Empty<YoloDatasetHealthSplitSummary>(),
                classes,
                NormalizeIssues(issues));
        }

        private static IReadOnlyList<YoloDatasetHealthClassSummary> BuildClassSummaries(
            LabelingProjectData data,
            YoloDatasetStatistics statistics,
            LabelingDatasetPurpose purpose)
        {
            statistics ??= new YoloDatasetStatistics();
            IReadOnlyDictionary<string, int> source = purpose == LabelingDatasetPurpose.Segmentation
                ? statistics.SegmentationObjectCountByClass
                : statistics.ObjectCountByClass;
            List<string> classNames = data?.ClassNamedList?
                .Select(item => item?.Text?.Trim() ?? string.Empty)
                .Where(name => name.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();
            if (classNames.Count == 0)
            {
                classNames = source.Keys.ToList();
            }

            return classNames
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Select(name =>
                {
                    source.TryGetValue(name, out int count);
                    return new YoloDatasetHealthClassSummary(name, count);
                })
                .ToArray();
        }

        private static IReadOnlyList<string> NormalizeIssues(IEnumerable<string> issues)
        {
            return (issues ?? Enumerable.Empty<string>())
                .Select(item => item?.Trim() ?? string.Empty)
                .Where(item => item.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
}
