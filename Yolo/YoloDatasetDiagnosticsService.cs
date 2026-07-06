using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem.Yolo
{
    public static class YoloDatasetDiagnosticsService
    {
        private const int RecommendedMinimumObjectsPerClass = 5;
        private const int RecommendedMinimumSegmentationPositiveImagesPerSplit = 5;
        private const double ClassBalanceWarningRatio = 5D;
        private const double SegmentationBackgroundWarningRatio = 3D;

        public static IReadOnlyList<string> BuildOperatorReport(CData data, bool refreshYaml)
        {
            var lines = new List<string>();
            YoloDatasetReadinessReport report = YoloDatasetReadinessService.Build(data, refreshYaml);

            lines.Add(report.IsReady
                ? $"YOLO dataset diagnosis: READY / Purpose:{report.Purpose}"
                : $"YOLO dataset diagnosis: NOT READY / Purpose:{report.Purpose}");
            if (data != null)
            {
                lines.Add($"YOLO output root: {data.OutputRootPath}");
                lines.Add($"YOLO data.yaml: {data.DataYamlFilePath}");
            }

            if (report.IsReady)
            {
                lines.AddRange(report.SummaryLines);
                AppendQualityHints(lines, data, report.Statistics);
                return lines;
            }

            foreach (string error in report.Errors)
            {
                lines.Add($"YOLO dataset issue: {error}");
            }

            return lines;
        }

        private static void AppendQualityHints(List<string> lines, CData data, YoloDatasetStatistics statistics)
        {
            if (statistics == null)
            {
                return;
            }

            lines.Add($"YOLO split detail. Train:{statistics.TrainImageCount}, Valid:{statistics.ValidImageCount}, Test:{statistics.TestImageCount}");
            lines.Add("YOLO split guide: Validation checks training while it runs. Test is reserved for final model comparison.");

            string classBalanceLine = BuildClassBalanceLine(data, statistics);
            if (!string.IsNullOrWhiteSpace(classBalanceLine))
            {
                lines.Add(classBalanceLine);
            }

            lines.AddRange(BuildQualityWarnings(data, statistics));
        }

        public static IReadOnlyList<string> BuildQualityWarnings(CData data, YoloDatasetStatistics statistics)
        {
            var lines = new List<string>();
            if (statistics == null)
            {
                return lines;
            }

            LabelingDatasetPurpose purpose = ResolveDatasetPurpose(data);
            AppendPurposeWarnings(lines, purpose, statistics);

            if (statistics.SplitImageContentOverlapCount > 0)
            {
                string example = string.IsNullOrWhiteSpace(statistics.SplitImageOverlapExample)
                    ? string.Empty
                    : $" Example: {statistics.SplitImageOverlapExample}.";
                lines.Add($"YOLO dataset warning: train/valid/test split contains duplicate image content ({statistics.SplitImageContentOverlapCount}).{example}");
            }

            if (statistics.TrainValidImageNameOverlapCount > 0)
            {
                lines.Add($"YOLO dataset warning: train/valid split contains duplicate file names ({statistics.TrainValidImageNameOverlapCount}). Check that validation images are independent.");
            }

            if (statistics.TestImageCount == 0)
            {
                lines.Add("YOLO dataset warning: Test split is empty. Set Test % before final model comparison.");
            }

            List<KeyValuePair<string, int>> classCounts = BuildClassCounts(data, statistics, purpose);
            if (purpose == LabelingDatasetPurpose.Segmentation
                && statistics.TotalSegmentationObjectCount == 0
                && statistics.TotalMaskFileCount > 0)
            {
                // Mask PNG files are valid segmentation artifacts, but they do not preserve
                // polygon/object instances. Avoid reporting every class as 0 objects.
                return lines;
            }

            if (purpose == LabelingDatasetPurpose.Segmentation)
            {
                AppendSegmentationCoverageWarnings(lines, statistics);
            }

            foreach (KeyValuePair<string, int> item in classCounts.Where(item => item.Value < RecommendedMinimumObjectsPerClass))
            {
                lines.Add($"YOLO dataset warning: class '{item.Key}' has only {item.Value} object(s). Add more labeled examples before trusting training.");
            }

            List<KeyValuePair<string, int>> positiveClassCounts = classCounts
                .Where(item => item.Value > 0)
                .OrderBy(item => item.Value)
                .ToList();

            if (positiveClassCounts.Count < 2)
            {
                return lines;
            }

            KeyValuePair<string, int> smallest = positiveClassCounts[0];
            KeyValuePair<string, int> largest = positiveClassCounts[positiveClassCounts.Count - 1];
            if (largest.Value >= smallest.Value * ClassBalanceWarningRatio)
            {
                lines.Add($"YOLO dataset warning: class balance is skewed. Smallest '{smallest.Key}'={smallest.Value}, largest '{largest.Key}'={largest.Value}.");
            }

            return lines;
        }

        private static void AppendSegmentationCoverageWarnings(List<string> lines, YoloDatasetStatistics statistics)
        {
            AppendSegmentationSplitWarnings(lines, "train", Math.Max(statistics.TrainSegmentFileCount, statistics.TrainMaskFileCount), statistics.TrainEmptyLabelFileCount);
            AppendSegmentationSplitWarnings(lines, "valid", Math.Max(statistics.ValidSegmentFileCount, statistics.ValidMaskFileCount), statistics.ValidEmptyLabelFileCount);
            AppendSegmentationTestSplitWarnings(lines, Math.Max(statistics.TestSegmentFileCount, statistics.TestMaskFileCount), statistics.TestEmptyLabelFileCount, statistics.TestImageCount);
        }

        private static void AppendSegmentationSplitWarnings(List<string> lines, string split, int positiveImageCount, int emptyBackgroundCount)
        {
            if (positiveImageCount > 0 && positiveImageCount < RecommendedMinimumSegmentationPositiveImagesPerSplit)
            {
                lines.Add($"YOLO dataset warning: segmentation {split} split has only {positiveImageCount} positive mask image(s). Add more NG mask examples before trusting YOLOv8 SEG training.");
            }

            if (positiveImageCount > 0 && emptyBackgroundCount >= positiveImageCount * SegmentationBackgroundWarningRatio)
            {
                lines.Add($"YOLO dataset warning: segmentation {split} split has {emptyBackgroundCount} OK/background image(s) but only {positiveImageCount} positive mask image(s). Check OK/NG balance before training.");
            }
        }

        private static void AppendSegmentationTestSplitWarnings(List<string> lines, int positiveImageCount, int emptyBackgroundCount, int imageCount)
        {
            if (imageCount <= 0)
            {
                return;
            }

            if (positiveImageCount == 0 && emptyBackgroundCount > 0)
            {
                lines.Add("YOLO dataset warning: segmentation test split has OK/background image(s) but no positive mask image. Add held-out NG mask examples before final YOLOv8 SEG model comparison.");
                return;
            }

            if (positiveImageCount > 0 && positiveImageCount < RecommendedMinimumSegmentationPositiveImagesPerSplit)
            {
                lines.Add($"YOLO dataset warning: segmentation test split has only {positiveImageCount} positive mask image(s). Add more held-out NG mask examples before final YOLOv8 SEG model comparison.");
            }
        }

        private static string BuildClassBalanceLine(CData data, YoloDatasetStatistics statistics)
        {
            List<KeyValuePair<string, int>> classCounts = BuildClassCounts(data, statistics, ResolveDatasetPurpose(data));
            return classCounts.Count == 0
                ? string.Empty
                : $"YOLO class balance. {string.Join(", ", classCounts.Select(item => $"{item.Key}:{item.Value}"))}";
        }

        private static void AppendPurposeWarnings(List<string> lines, LabelingDatasetPurpose purpose, YoloDatasetStatistics statistics)
        {
            if (lines == null || statistics == null)
            {
                return;
            }

            if (purpose == LabelingDatasetPurpose.ObjectDetection && statistics.TotalSegmentationArtifactFileCount > 0)
            {
                lines.Add($"YOLO dataset warning: ObjectDetection ignores segmentation artifacts. Excluded segment objects:{statistics.TotalSegmentationObjectCount}, segment files:{statistics.TotalSegmentFileCount}, mask files:{statistics.TotalMaskFileCount}.");
                return;
            }

            if (purpose == LabelingDatasetPurpose.Segmentation && statistics.TotalObjectCount > 0)
            {
                lines.Add($"YOLO dataset warning: Segmentation uses segment/mask annotations as primary labels. Box labels are auxiliary:{statistics.TotalObjectCount}.");
            }

            if (purpose == LabelingDatasetPurpose.Segmentation
                && statistics.TotalSegmentationObjectCount == 0
                && statistics.TotalMaskFileCount > 0)
            {
                lines.Add($"YOLO dataset warning: Segmentation has mask PNG files ({statistics.TotalMaskFileCount}) without segment JSON polygons. Readiness is based on masks; class/object balance requires segment JSON.");
            }
        }

        private static List<KeyValuePair<string, int>> BuildClassCounts(CData data, YoloDatasetStatistics statistics, LabelingDatasetPurpose purpose)
        {
            if (statistics == null)
            {
                return new List<KeyValuePair<string, int>>();
            }

            List<string> classNames = data?.ClassNamedList?
                .Select(item => item?.Text?.Trim() ?? string.Empty)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(System.StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            if (classNames.Count == 0)
            {
                classNames = purpose == LabelingDatasetPurpose.Segmentation
                    ? statistics.SegmentationObjectCountByClass.Keys.OrderBy(name => name).ToList()
                    : statistics.ObjectCountByClass.Keys.OrderBy(name => name).ToList();
            }

            var classCounts = new List<KeyValuePair<string, int>>();
            foreach (string className in classNames)
            {
                int count;
                if (purpose == LabelingDatasetPurpose.Segmentation)
                {
                    statistics.SegmentationObjectCountByClass.TryGetValue(className, out count);
                }
                else
                {
                    statistics.ObjectCountByClass.TryGetValue(className, out count);
                }

                classCounts.Add(new KeyValuePair<string, int>(className, count));
            }

            return classCounts;
        }

        private static LabelingDatasetPurpose ResolveDatasetPurpose(CData data)
        {
            data?.ProjectSettings?.EnsureDefaults();
            return data?.ProjectSettings?.DatasetPurpose ?? LabelingDatasetPurpose.ObjectDetection;
        }
    }
}
