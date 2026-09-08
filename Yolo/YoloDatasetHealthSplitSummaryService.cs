using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem.Yolo
{
    /// <summary>
    /// Projects existing readiness and quality reports into Dataset Health split rows.
    /// Split assignment and file scanning remain owned by their existing services.
    /// </summary>
    public static class YoloDatasetHealthSplitSummaryService
    {
        public static IReadOnlyList<YoloDatasetHealthSplitSummary> BuildDetection(
            YoloDatasetQualityAuditReport qualityAudit)
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

        public static IReadOnlyList<YoloDatasetHealthSplitSummary> BuildSegmentation(
            YoloDatasetReadinessReport readiness)
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
    }
}
