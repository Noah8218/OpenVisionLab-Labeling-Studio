using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MvcVisionSystem.Yolo
{
    public static class YoloDatasetValidator
    {
        public static YoloDatasetValidationResult ValidateConfiguration(LabelingProjectData data)
        {
            var errors = new List<string>();
            if (data == null)
            {
                errors.Add("Dataset configuration is missing.");
                return new YoloDatasetValidationResult(errors);
            }

            data.NormalizeOutputPaths();
            ValidateOutputPath(data, errors);
            ValidateClasses(data, errors);
            ValidateDatasetSplit(data.ProjectSettings?.YoloDataset, errors);
            ValidateTrainingSettings(data.GetTrainingSettings(), errors);
            return new YoloDatasetValidationResult(errors);
        }

        public static YoloDatasetValidationResult ValidateTrainingFiles(LabelingProjectData data)
        {
            var errors = new List<string>();
            if (data == null)
            {
                errors.Add("Dataset configuration is missing.");
                return new YoloDatasetValidationResult(errors);
            }

            data.NormalizeOutputPaths();
            LabelingDatasetPurpose purpose = ResolveDatasetPurpose(data);
            ValidateFileExists(data.DataYamlFilePath, "data.yaml", errors);
            YoloDatasetManifestValidationService.Validate(data, errors);
            YoloDatasetSourceGroupService.ValidateOptional(data.OutputRootPath, errors);
            YoloDatasetAnnotationSetValidationService.Validate(data, purpose, errors);

            YoloDatasetSplitQualityService.ValidateSeparation("train", data.TrainImagesPath, "valid", data.ValidImagesPath, errors);
            YoloDatasetSplitQualityService.ValidateSeparation("train", data.TrainImagesPath, "test", data.TestImagesPath, errors);
            YoloDatasetSplitQualityService.ValidateSeparation("valid", data.ValidImagesPath, "test", data.TestImagesPath, errors);
            YoloDatasetStatistics statistics = BuildStatistics(data);
            AppendPurposeAnnotationPolicyErrors(purpose, statistics, errors);

            return new YoloDatasetValidationResult(errors);
        }
        public static YoloDatasetValidationResult ValidateAnomalyClassificationConfiguration(LabelingProjectData data)
        {
            var errors = new List<string>();
            if (data == null)
            {
                errors.Add("Dataset configuration is missing.");
                return new YoloDatasetValidationResult(errors);
            }

            data.NormalizeOutputPaths();
            ValidateOutputPath(data, errors);
            ValidateDatasetSplit(data.ProjectSettings?.YoloDataset, errors);
            ValidateTrainingSettings(data.GetTrainingSettings(), errors);
            return new YoloDatasetValidationResult(errors);
        }

        private static void AppendPurposeAnnotationPolicyErrors(
            LabelingDatasetPurpose purpose,
            YoloDatasetStatistics statistics,
            List<string> errors)
        {
            if (statistics == null)
            {
                return;
            }

            if (purpose == LabelingDatasetPurpose.Segmentation)
            {
                if (statistics.TotalSegmentationObjectCount == 0 && statistics.TotalMaskFileCount == 0)
                {
                    errors.Add($"Segmentation dataset has no segment JSON or mask PNG annotations. Draw brush/polygon masks before segmentation training/export. Box labels:{statistics.TotalObjectCount}.");
                }

                return;
            }

            if (statistics.TotalObjectCount > 0)
            {
                return;
            }

            string purposeName = purpose == LabelingDatasetPurpose.AnomalyDetection
                ? "AnomalyDetection"
                : "ObjectDetection";
            if (statistics.TotalSegmentationObjectCount > 0 || statistics.TotalMaskFileCount > 0)
            {
                errors.Add($"{purposeName} dataset has segmentation annotations ({statistics.TotalSegmentationObjectCount}) but no YOLO box labels. This purpose trains from box .txt labels; add box labels or switch the dataset purpose to Segmentation.");
                return;
            }

            errors.Add($"{purposeName} dataset has no YOLO box labels. Draw and save at least one rectangle label before training.");
        }

        public static YoloDatasetStatistics BuildStatistics(LabelingProjectData data)
        {
            return YoloDatasetStatisticsService.Build(data);
        }

        private static void ValidateOutputPath(LabelingProjectData data, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(data.OutputRootPath))
            {
                errors.Add("YOLO output root path is empty.");
                return;
            }

            if (!YoloDatasetSettings.IsYamlFilePath(data.DataYamlFilePath))
            {
                errors.Add("YOLO data.yaml path must end with .yaml or .yml.");
            }

            try
            {
                string writableProbeDirectory = FindNearestExistingDirectory(data.OutputRootPath);
                if (string.IsNullOrWhiteSpace(writableProbeDirectory))
                {
                    errors.Add("YOLO output root path has no existing writable parent.");
                    return;
                }

                string probePath = Path.Combine(writableProbeDirectory, $".write-test-{Guid.NewGuid():N}.tmp");
                File.WriteAllText(probePath, "");
                File.Delete(probePath);
            }
            catch (Exception ex)
            {
                errors.Add($"YOLO output root path is not writable: {ex.Message}");
            }
        }

        private static string FindNearestExistingDirectory(string path)
        {
            string current = Path.GetFullPath(path);
            while (!string.IsNullOrWhiteSpace(current))
            {
                if (Directory.Exists(current))
                {
                    return current;
                }

                current = Directory.GetParent(current)?.FullName;
            }

            return string.Empty;
        }

        private static void ValidateClasses(LabelingProjectData data, List<string> errors)
        {
            if (data.ClassNamedList == null || data.ClassNamedList.Count == 0)
            {
                errors.Add("At least one class is required before training.");
                return;
            }

            List<string> classNames = data.ClassNamedList
                .Select(item => item?.Text?.Trim() ?? "")
                .ToList();

            if (classNames.Any(string.IsNullOrWhiteSpace))
            {
                errors.Add("Class names cannot be empty.");
            }

            foreach (string duplicate in classNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key))
            {
                errors.Add($"Duplicate class name: {duplicate}");
            }
        }

        private static void ValidateDatasetSplit(YoloDatasetSettings settings, List<string> errors)
        {
            if (settings == null)
            {
                return;
            }

            if (settings.ValidationPercent < 0 || settings.ValidationPercent > 100)
            {
                errors.Add("Validation split percent must be between 0 and 100.");
            }

            if (settings.TestPercent < 0 || settings.TestPercent > 100)
            {
                errors.Add("Test split percent must be between 0 and 100.");
            }

            if (settings.ValidationPercent + settings.TestPercent > 100)
            {
                errors.Add("Validation split percent and test split percent must not exceed 100 combined.");
            }
        }

        private static void ValidateTrainingSettings(TrainingSettings settings, List<string> errors)
        {
            if (settings == null)
            {
                errors.Add("Training settings are missing.");
                return;
            }

            if (settings.ImageSize <= 0)
            {
                errors.Add("Training image size must be greater than zero.");
            }

            if (settings.Batch <= 0)
            {
                errors.Add("Training batch size must be greater than zero.");
            }

            if (settings.Epoch <= 0)
            {
                errors.Add("Training epoch count must be greater than zero.");
            }
        }

        private static void ValidateFileExists(string path, string name, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                errors.Add($"{name} file does not exist: {path}");
            }
        }

        private static LabelingDatasetPurpose ResolveDatasetPurpose(LabelingProjectData data)
        {
            data?.ProjectSettings?.EnsureDefaults();
            return data?.ProjectSettings?.DatasetPurpose ?? LabelingDatasetPurpose.ObjectDetection;
        }

    }
}
