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
                        $"Anomaly detection dataset ready. TrainImages:{Statistics.TrainImageCount}, ValidImages:{Statistics.ValidImageCount}, TestImages:{Statistics.TestImageCount}, Normal:{Statistics.AnomalyNormalImageCount}, Abnormal:{Statistics.AnomalyAbnormalImageCount}, Unreviewed:{Statistics.AnomalyUnreviewedImageCount}",
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
                    $"AnomalyDetection uses reviewed normal/abnormal images for image-level judgements. Normal:{statistics.AnomalyNormalImageCount}, Abnormal:{statistics.AnomalyAbnormalImageCount}, Unreviewed:{statistics.AnomalyUnreviewedImageCount}",
                _ =>
                    $"ObjectDetection uses YOLO box .txt labels. BoxLabels:{statistics.TotalObjectCount}, SegmentationArtifactsExcluded:{statistics.TotalSegmentationArtifactFileCount}"
            };
        }
    }
}
