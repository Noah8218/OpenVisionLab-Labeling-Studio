using MvcVisionSystem._1._Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem.Yolo
{
    public enum YoloDatasetHealthQualityStatus
    {
        NotEvaluated,
        Healthy,
        ProblemsFound
    }

    // Read-only aggregation for the on-demand Dataset Health view. It deliberately
    // reuses the existing readiness/audit services instead of changing labeling paths.
    public sealed class YoloDatasetHealthReport
    {
        public YoloDatasetHealthReport(
            LabelingDatasetPurpose purpose,
            YoloDatasetReadinessReport yoloReadiness,
            AnomalyClassificationTrainingReadinessReport anomalyReadiness,
            YoloDatasetQualityAuditReport qualityAudit,
            IReadOnlyList<YoloDatasetHealthSplitSummary> splits,
            IReadOnlyList<YoloDatasetHealthClassSummary> classes,
            IReadOnlyList<string> issues)
        {
            Purpose = purpose;
            YoloReadiness = yoloReadiness;
            AnomalyReadiness = anomalyReadiness;
            QualityAudit = qualityAudit;
            Splits = splits ?? Array.Empty<YoloDatasetHealthSplitSummary>();
            Classes = classes ?? Array.Empty<YoloDatasetHealthClassSummary>();
            Issues = issues ?? Array.Empty<string>();
        }

        public LabelingDatasetPurpose Purpose { get; }

        public YoloDatasetReadinessReport YoloReadiness { get; }

        public AnomalyClassificationTrainingReadinessReport AnomalyReadiness { get; }

        public YoloDatasetQualityAuditReport QualityAudit { get; }

        public IReadOnlyList<YoloDatasetHealthSplitSummary> Splits { get; }

        public IReadOnlyList<YoloDatasetHealthClassSummary> Classes { get; }

        public IReadOnlyList<string> Issues { get; }

        public bool IsReady => Purpose == LabelingDatasetPurpose.AnomalyDetection
            ? AnomalyReadiness?.IsReady == true
            : YoloReadiness?.IsReady == true;

        public int TotalImageCount => Purpose == LabelingDatasetPurpose.AnomalyDetection
            ? AnomalyReadiness?.SourceImageCount ?? 0
            : YoloReadiness?.Statistics?.TotalImageCount ?? 0;

        public int PrimaryLabelCount => Purpose switch
        {
            LabelingDatasetPurpose.Segmentation => YoloReadiness?.Statistics?.TotalSegmentationObjectCount ?? 0,
            LabelingDatasetPurpose.AnomalyDetection => (AnomalyReadiness?.NormalImageCount ?? 0) + (AnomalyReadiness?.AbnormalImageCount ?? 0),
            _ => YoloReadiness?.Statistics?.TotalObjectCount ?? 0
        };

        public int SplitContentOverlapCount => YoloReadiness?.Statistics?.SplitImageContentOverlapCount ?? 0;

        public int QualityProblemCount => Purpose == LabelingDatasetPurpose.AnomalyDetection
            ? AnomalyReadiness?.UnreviewedImageCount ?? 0
            : Purpose == LabelingDatasetPurpose.Segmentation
                ? (YoloReadiness?.TrainingFiles?.Errors ?? Array.Empty<string>()).Count(IsSegmentationQualityIssue)
            : (QualityAudit?.TotalMissingLabelCount ?? 0) + (QualityAudit?.TotalInvalidLabelLineCount ?? 0);

        public YoloDatasetHealthQualityStatus QualityStatus => Purpose switch
        {
            LabelingDatasetPurpose.AnomalyDetection => AnomalyReadiness == null || AnomalyReadiness.SourceImageCount == 0
                ? YoloDatasetHealthQualityStatus.NotEvaluated
                : QualityProblemCount > 0
                    ? YoloDatasetHealthQualityStatus.ProblemsFound
                    : YoloDatasetHealthQualityStatus.Healthy,
            LabelingDatasetPurpose.Segmentation => YoloReadiness?.Configuration?.IsValid != true
                || YoloReadiness?.Statistics?.TotalImageCount <= 0
                ? YoloDatasetHealthQualityStatus.NotEvaluated
                : QualityProblemCount > 0
                    ? YoloDatasetHealthQualityStatus.ProblemsFound
                    : YoloDatasetHealthQualityStatus.Healthy,
            _ => QualityAudit == null || QualityAudit.TotalImageCount == 0
                ? YoloDatasetHealthQualityStatus.NotEvaluated
                : QualityProblemCount > 0
                    ? YoloDatasetHealthQualityStatus.ProblemsFound
                    : YoloDatasetHealthQualityStatus.Healthy
        };

        internal static bool IsSegmentationQualityIssue(string issue)
        {
            string normalized = issue ?? string.Empty;
            return IsSegmentationMissingAnnotationIssue(normalized)
                || normalized.Contains("segment JSON is invalid", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("segment polygon has fewer than three points", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("segment polygon has invalid class index", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("segment JSON has no polygons", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("Segmentation dataset has no segment JSON or mask PNG annotations", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsSegmentationMissingAnnotationIssue(string issue)
        {
            return (issue ?? string.Empty).Contains(
                "segmentation annotation or empty background label is missing",
                StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class YoloDatasetHealthSplitSummary
    {
        public YoloDatasetHealthSplitSummary(
            string split,
            int imageCount,
            int primaryAnnotationCount,
            int labelFileCount,
            int missingLabelCount,
            int emptyLabelCount,
            int invalidLabelLineCount,
            int segmentFileCount,
            int maskFileCount,
            int auxiliaryBoxObjectCount)
        {
            Split = split ?? string.Empty;
            ImageCount = Math.Max(0, imageCount);
            PrimaryAnnotationCount = Math.Max(0, primaryAnnotationCount);
            LabelFileCount = Math.Max(0, labelFileCount);
            MissingLabelCount = Math.Max(0, missingLabelCount);
            EmptyLabelCount = Math.Max(0, emptyLabelCount);
            InvalidLabelLineCount = Math.Max(0, invalidLabelLineCount);
            SegmentFileCount = Math.Max(0, segmentFileCount);
            MaskFileCount = Math.Max(0, maskFileCount);
            AuxiliaryBoxObjectCount = Math.Max(0, auxiliaryBoxObjectCount);
        }

        public string Split { get; }

        public int ImageCount { get; }

        public int PrimaryAnnotationCount { get; }

        public int LabelFileCount { get; }

        public int MissingLabelCount { get; }

        public int EmptyLabelCount { get; }

        public int InvalidLabelLineCount { get; }

        public int SegmentFileCount { get; }

        public int MaskFileCount { get; }

        public int AuxiliaryBoxObjectCount { get; }
    }

    public sealed class YoloDatasetHealthClassSummary
    {
        public YoloDatasetHealthClassSummary(string className, int count)
        {
            ClassName = className ?? string.Empty;
            Count = Math.Max(0, count);
        }

        public string ClassName { get; }

        public int Count { get; }
    }

    public static class YoloDatasetHealthService
    {
        public static YoloDatasetHealthReport Build(CData data)
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
                ? BuildSegmentationSplits(readiness)
                : BuildDetectionSplits(qualityAudit);
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

        private static YoloDatasetHealthReport BuildAnomalyReport(CData data)
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

        private static IReadOnlyList<YoloDatasetHealthSplitSummary> BuildDetectionSplits(YoloDatasetQualityAuditReport qualityAudit)
        {
            qualityAudit ??= new YoloDatasetQualityAuditReport();
            return qualityAudit.Splits
                .Select(split => new YoloDatasetHealthSplitSummary(
                    split.Split,
                    split.ImageCount,
                    split.ObjectCount,
                    split.LabelFileCount,
                    split.MissingLabelCount,
                    split.EmptyLabelCount,
                    split.InvalidLabelLineCount,
                    segmentFileCount: 0,
                    maskFileCount: 0,
                    auxiliaryBoxObjectCount: 0))
                .ToArray();
        }

        private static IReadOnlyList<YoloDatasetHealthSplitSummary> BuildSegmentationSplits(YoloDatasetReadinessReport readiness)
        {
            YoloDatasetStatistics statistics = readiness?.Statistics;
            statistics ??= new YoloDatasetStatistics();
            IReadOnlyList<string> qualityErrors = readiness?.TrainingFiles?.Errors ?? Array.Empty<string>();
            return new[]
            {
                BuildSegmentationSplit(
                    YoloDatasetSplitService.TrainMode,
                    statistics.TrainImageCount,
                    statistics.TrainSegmentFileCount,
                    statistics.TrainMaskFileCount,
                    statistics.TrainLabelCount,
                    statistics.TrainEmptyLabelFileCount,
                    qualityErrors),
                BuildSegmentationSplit(
                    YoloDatasetSplitService.ValidMode,
                    statistics.ValidImageCount,
                    statistics.ValidSegmentFileCount,
                    statistics.ValidMaskFileCount,
                    statistics.ValidLabelCount,
                    statistics.ValidEmptyLabelFileCount,
                    qualityErrors),
                BuildSegmentationSplit(
                    YoloDatasetSplitService.TestMode,
                    statistics.TestImageCount,
                    statistics.TestSegmentFileCount,
                    statistics.TestMaskFileCount,
                    statistics.TestLabelCount,
                    statistics.TestEmptyLabelFileCount,
                    qualityErrors)
            };
        }

        private static YoloDatasetHealthSplitSummary BuildSegmentationSplit(
            string split,
            int imageCount,
            int segmentFileCount,
            int maskFileCount,
            int labelFileCount,
            int emptyLabelCount,
            IReadOnlyList<string> qualityErrors)
        {
            string splitPrefix = (split ?? string.Empty) + " ";
            IEnumerable<string> splitErrors = (qualityErrors ?? Array.Empty<string>())
                .Where(error => (error ?? string.Empty).StartsWith(splitPrefix, StringComparison.OrdinalIgnoreCase));
            int missingCount = splitErrors.Count(YoloDatasetHealthReport.IsSegmentationMissingAnnotationIssue);
            int invalidCount = splitErrors.Count(YoloDatasetHealthReport.IsSegmentationQualityIssue) - missingCount;
            return new YoloDatasetHealthSplitSummary(
                split,
                imageCount,
                Math.Max(segmentFileCount, maskFileCount),
                labelFileCount,
                missingCount,
                emptyLabelCount,
                Math.Max(0, invalidCount),
                segmentFileCount,
                maskFileCount,
                auxiliaryBoxObjectCount: 0);
        }

        private static IReadOnlyList<YoloDatasetHealthClassSummary> BuildClassSummaries(
            CData data,
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
