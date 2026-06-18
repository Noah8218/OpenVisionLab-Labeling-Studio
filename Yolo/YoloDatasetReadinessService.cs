using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem.Yolo
{
    public sealed class YoloDatasetReadinessReport
    {
        public YoloDatasetReadinessReport(
            YoloDatasetValidationResult configuration,
            YoloDatasetValidationResult trainingFiles,
            YoloDatasetStatistics statistics)
        {
            Configuration = configuration ?? new YoloDatasetValidationResult(Array.Empty<string>());
            TrainingFiles = trainingFiles ?? new YoloDatasetValidationResult(Array.Empty<string>());
            Statistics = statistics ?? new YoloDatasetStatistics();
        }

        public YoloDatasetValidationResult Configuration { get; }
        public YoloDatasetValidationResult TrainingFiles { get; }
        public YoloDatasetStatistics Statistics { get; }
        public bool IsReady => Configuration.IsValid && TrainingFiles.IsValid;
        public IReadOnlyList<string> Errors => Configuration.Errors.Concat(TrainingFiles.Errors).ToList();

        public IReadOnlyList<string> SummaryLines
        {
            get
            {
                var lines = new List<string>
                {
                    $"YOLO dataset ready. TrainImages:{Statistics.TrainImageCount}, ValidImages:{Statistics.ValidImageCount}, TrainLabels:{Statistics.TrainLabelCount}, ValidLabels:{Statistics.ValidLabelCount}, Objects:{Statistics.TotalObjectCount}"
                };

                foreach (KeyValuePair<string, int> item in Statistics.ObjectCountByClass.OrderBy(item => item.Key))
                {
                    lines.Add($"YOLO class objects. {item.Key}:{item.Value}");
                }

                return lines;
            }
        }
    }

    public static class YoloDatasetReadinessService
    {
        public static YoloDatasetReadinessReport Build(CData data, bool refreshYaml)
        {
            YoloDatasetValidationResult configuration = YoloDatasetValidator.ValidateConfiguration(data);
            if (!configuration.IsValid)
            {
                return new YoloDatasetReadinessReport(
                    configuration,
                    new YoloDatasetValidationResult(Array.Empty<string>()),
                    new YoloDatasetStatistics());
            }

            if (refreshYaml)
            {
                data.SaveYoloDataYaml();
            }

            YoloDatasetValidationResult files = YoloDatasetValidator.ValidateTrainingFiles(data);
            YoloDatasetStatistics statistics = files.IsValid
                ? YoloDatasetValidator.BuildStatistics(data)
                : new YoloDatasetStatistics();

            return new YoloDatasetReadinessReport(configuration, files, statistics);
        }
    }
}
