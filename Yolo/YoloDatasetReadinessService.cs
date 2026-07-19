using System;
using System.Collections.Generic;
using System.Linq;
using MvcVisionSystem._1._Core;

namespace MvcVisionSystem.Yolo
{
    public sealed class YoloDatasetReadinessReport
    {
        public YoloDatasetReadinessReport(
            YoloDatasetValidationResult configuration,
            YoloDatasetValidationResult trainingFiles,
            YoloDatasetStatistics statistics,
            LabelingDatasetPurpose purpose = LabelingDatasetPurpose.ObjectDetection)
        {
            Configuration = configuration ?? new YoloDatasetValidationResult(Array.Empty<string>());
            TrainingFiles = trainingFiles ?? new YoloDatasetValidationResult(Array.Empty<string>());
            Statistics = statistics ?? new YoloDatasetStatistics();
            Purpose = purpose;
        }

        public YoloDatasetValidationResult Configuration { get; }
        public YoloDatasetValidationResult TrainingFiles { get; }
        public YoloDatasetStatistics Statistics { get; }
        public LabelingDatasetPurpose Purpose { get; }
        public bool IsReady => Configuration.IsValid && TrainingFiles.IsValid;
        public IReadOnlyList<string> Errors => Configuration.Errors.Concat(TrainingFiles.Errors).ToList();

        public IReadOnlyList<string> SummaryLines
        {
            get
            {
                if (Purpose == LabelingDatasetPurpose.AnomalyDetection)
                {
                    return new[]
                    {
                        $"Anomaly classification dataset ready. TrainImages:{Statistics.TrainImageCount}, ValidImages:{Statistics.ValidImageCount}, TestImages:{Statistics.TestImageCount}, Normal:{Statistics.AnomalyNormalImageCount}, Abnormal:{Statistics.AnomalyAbnormalImageCount}, Unreviewed:{Statistics.AnomalyUnreviewedImageCount}",
                        $"Dataset purpose summary. {BuildPurposeSummary(Purpose, Statistics)}"
                    };
                }

                var lines = new List<string>
                {
                    $"YOLO dataset ready. Purpose:{Purpose}, TrainImages:{Statistics.TrainImageCount}, ValidImages:{Statistics.ValidImageCount}, TestImages:{Statistics.TestImageCount}, TrainLabels:{Statistics.TrainLabelCount}, ValidLabels:{Statistics.ValidLabelCount}, TestLabels:{Statistics.TestLabelCount}, Objects:{Statistics.TotalObjectCount}, Segments:{Statistics.TotalSegmentationObjectCount}",
                    $"Dataset purpose summary. {BuildPurposeSummary(Purpose, Statistics)}"
                };

                foreach (KeyValuePair<string, int> item in Statistics.ObjectCountByClass.OrderBy(item => item.Key))
                {
                    lines.Add($"YOLO class objects. {item.Key}:{item.Value}");
                }

                foreach (KeyValuePair<string, int> item in Statistics.SegmentationObjectCountByClass.OrderBy(item => item.Key))
                {
                    lines.Add($"YOLO segmentation objects. {item.Key}:{item.Value}");
                }

                return lines;
            }
        }

        private static string BuildPurposeSummary(LabelingDatasetPurpose purpose, YoloDatasetStatistics statistics)
        {
            statistics ??= new YoloDatasetStatistics();
            return purpose switch
            {
                LabelingDatasetPurpose.Segmentation =>
                    $"Segmentation uses segment JSON/mask PNG annotations as primary labels. SegmentObjects:{statistics.TotalSegmentationObjectCount}, SegmentFiles:{statistics.TotalSegmentFileCount}, MaskFiles:{statistics.TotalMaskFileCount}, BoxLabelsAuxiliary:{statistics.TotalObjectCount}",
                LabelingDatasetPurpose.AnomalyDetection =>
                    $"AnomalyDetection uses reviewed normal/abnormal images for image-level classification. Normal:{statistics.AnomalyNormalImageCount}, Abnormal:{statistics.AnomalyAbnormalImageCount}, Unreviewed:{statistics.AnomalyUnreviewedImageCount}",
                _ =>
                    $"ObjectDetection uses YOLO box .txt labels. BoxLabels:{statistics.TotalObjectCount}, SegmentationArtifactsExcluded:{statistics.TotalSegmentationArtifactFileCount}"
            };
        }
    }

    public static class YoloDatasetReadinessService
    {
        public static YoloDatasetReadinessReport Build(CData data, bool refreshYaml)
        {
            LabelingDatasetPurpose purpose = ResolveDatasetPurpose(data);
            YoloDatasetValidationResult configuration = purpose == LabelingDatasetPurpose.AnomalyDetection
                ? YoloDatasetValidator.ValidateAnomalyClassificationConfiguration(data)
                : YoloDatasetValidator.ValidateConfiguration(data);
            if (!configuration.IsValid)
            {
                return new YoloDatasetReadinessReport(
                    configuration,
                    new YoloDatasetValidationResult(Array.Empty<string>()),
                    new YoloDatasetStatistics(),
                    purpose);
            }

            if (purpose == LabelingDatasetPurpose.AnomalyDetection)
            {
                AnomalyClassificationTrainingReadinessReport anomaly =
                    AnomalyClassificationTrainingReadinessService.Build(data);
                var anomalyStatistics = new YoloDatasetStatistics
                {
                    TrainImageCount = anomaly.TrainImageCount,
                    ValidImageCount = anomaly.ValidImageCount,
                    TestImageCount = anomaly.TestImageCount,
                    TrainLabelCount = anomaly.TrainImageCount,
                    ValidLabelCount = anomaly.ValidImageCount,
                    TestLabelCount = anomaly.TestImageCount,
                    AnomalyNormalImageCount = anomaly.NormalImageCount,
                    AnomalyAbnormalImageCount = anomaly.AbnormalImageCount,
                    AnomalyUnreviewedImageCount = anomaly.UnreviewedImageCount
                };
                return new YoloDatasetReadinessReport(
                    configuration,
                    new YoloDatasetValidationResult(anomaly.Errors),
                    anomalyStatistics,
                    purpose);
            }

            if (refreshYaml)
            {
                data.SaveYoloDataYaml();
            }

            YoloDatasetValidationResult files = YoloDatasetValidator.ValidateTrainingFiles(data);
            // Keep statistics even when readiness fails so the operator sees the scale of the issue
            // (for example, 125 duplicated train/valid images) instead of a vague "not ready" state.
            YoloDatasetStatistics statistics = YoloDatasetValidator.BuildStatistics(data);

            return new YoloDatasetReadinessReport(configuration, files, statistics, purpose);
        }

        private static LabelingDatasetPurpose ResolveDatasetPurpose(CData data)
        {
            data?.ProjectSettings?.EnsureDefaults();
            return data?.ProjectSettings?.DatasetPurpose ?? LabelingDatasetPurpose.ObjectDetection;
        }
    }
}
