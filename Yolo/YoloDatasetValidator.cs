using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MvcVisionSystem.Yolo
{
    public sealed class YoloDatasetValidationResult
    {
        public YoloDatasetValidationResult(IEnumerable<string> errors)
        {
            Errors = (errors ?? Enumerable.Empty<string>()).ToList();
        }

        public IReadOnlyList<string> Errors { get; }
        public bool IsValid => Errors.Count == 0;
        public string Summary => string.Join(Environment.NewLine, Errors);
    }

    public sealed class YoloDatasetStatistics
    {
        private readonly Dictionary<string, int> objectCountByClass = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public int TrainImageCount { get; internal set; }
        public int ValidImageCount { get; internal set; }
        public int TrainLabelCount { get; internal set; }
        public int ValidLabelCount { get; internal set; }
        public int TotalObjectCount => objectCountByClass.Values.Sum();
        public IReadOnlyDictionary<string, int> ObjectCountByClass => objectCountByClass;

        internal void AddObject(string className)
        {
            if (string.IsNullOrWhiteSpace(className))
            {
                return;
            }

            objectCountByClass.TryGetValue(className, out int count);
            objectCountByClass[className] = count + 1;
        }
    }

    public static class YoloDatasetValidator
    {
        private static readonly string[] ImageExtensions = { ".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff" };

        public static YoloDatasetValidationResult ValidateConfiguration(CData data)
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

        public static YoloDatasetValidationResult ValidateTrainingFiles(CData data)
        {
            var errors = new List<string>();
            if (data == null)
            {
                errors.Add("Dataset configuration is missing.");
                return new YoloDatasetValidationResult(errors);
            }

            data.NormalizeOutputPaths();
            ValidateFileExists(data.DataYamlFilePath, "data.yaml", errors);
            ValidateImageAndLabelSet("train", data.TrainImagesPath, Path.Combine(data.OutputRootPath, "data", "train", "labels"), data.ClassNamedList, errors);
            ValidateImageAndLabelSet("valid", data.ValidImagesPath, Path.Combine(data.OutputRootPath, "data", "valid", "labels"), data.ClassNamedList, errors);
            return new YoloDatasetValidationResult(errors);
        }

        public static YoloDatasetStatistics BuildStatistics(CData data)
        {
            var statistics = new YoloDatasetStatistics();
            if (data == null)
            {
                return statistics;
            }

            data.NormalizeOutputPaths();
            string trainLabelsPath = Path.Combine(data.OutputRootPath, "data", "train", "labels");
            string validLabelsPath = Path.Combine(data.OutputRootPath, "data", "valid", "labels");

            statistics.TrainImageCount = CountImages(data.TrainImagesPath);
            statistics.ValidImageCount = CountImages(data.ValidImagesPath);
            statistics.TrainLabelCount = CountFiles(trainLabelsPath, "*.txt");
            statistics.ValidLabelCount = CountFiles(validLabelsPath, "*.txt");

            CountObjects(trainLabelsPath, data.ClassNamedList, statistics);
            CountObjects(validLabelsPath, data.ClassNamedList, statistics);
            return statistics;
        }

        private static void ValidateOutputPath(CData data, List<string> errors)
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
                Directory.CreateDirectory(data.OutputRootPath);
                string probePath = Path.Combine(data.OutputRootPath, $".write-test-{Guid.NewGuid():N}.tmp");
                File.WriteAllText(probePath, "");
                File.Delete(probePath);
            }
            catch (Exception ex)
            {
                errors.Add($"YOLO output root path is not writable: {ex.Message}");
            }
        }

        private static void ValidateClasses(CData data, List<string> errors)
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

        private static void ValidateImageAndLabelSet(string mode, string imageDirectory, string labelDirectory, IReadOnlyList<CClassItem> classes, List<string> errors)
        {
            if (!Directory.Exists(imageDirectory))
            {
                errors.Add($"{mode} image directory does not exist: {imageDirectory}");
                return;
            }

            if (!Directory.Exists(labelDirectory))
            {
                errors.Add($"{mode} label directory does not exist: {labelDirectory}");
                return;
            }

            List<string> images = Directory
                .EnumerateFiles(imageDirectory)
                .Where(IsSupportedImageFile)
                .ToList();

            if (images.Count == 0)
            {
                errors.Add($"{mode} image directory has no supported images.");
                return;
            }

            foreach (string imagePath in images)
            {
                string labelPath = Path.Combine(labelDirectory, $"{Path.GetFileNameWithoutExtension(imagePath)}.txt");
                if (!File.Exists(labelPath))
                {
                    errors.Add($"{mode} label file is missing for image: {Path.GetFileName(imagePath)}");
                    continue;
                }

                ValidateLabelFile(mode, labelPath, classes, errors);
            }
        }

        private static void ValidateLabelFile(string mode, string labelPath, IReadOnlyList<CClassItem> classes, List<string> errors)
        {
            int lineNo = 0;
            foreach (string line in File.ReadLines(labelPath))
            {
                lineNo++;
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 5)
                {
                    errors.Add($"{mode} label has invalid YOLO format at {Path.GetFileName(labelPath)}:{lineNo}");
                    continue;
                }

                if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int classIndex) ||
                    classIndex < 0 ||
                    classes == null ||
                    classIndex >= classes.Count)
                {
                    errors.Add($"{mode} label has invalid class index at {Path.GetFileName(labelPath)}:{lineNo}");
                    continue;
                }

                for (int i = 1; i < parts.Length; i++)
                {
                    if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ||
                        double.IsNaN(value) ||
                        double.IsInfinity(value) ||
                        value < 0 ||
                        value > 1)
                    {
                        errors.Add($"{mode} label has out-of-range normalized value at {Path.GetFileName(labelPath)}:{lineNo}");
                        break;
                    }
                }

                if (double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double width) && width <= 0)
                {
                    errors.Add($"{mode} label width must be greater than zero at {Path.GetFileName(labelPath)}:{lineNo}");
                }

                if (double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out double height) && height <= 0)
                {
                    errors.Add($"{mode} label height must be greater than zero at {Path.GetFileName(labelPath)}:{lineNo}");
                }
            }
        }

        private static void ValidateFileExists(string path, string name, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                errors.Add($"{name} file does not exist: {path}");
            }
        }

        private static bool IsSupportedImageFile(string path)
        {
            string extension = Path.GetExtension(path);
            return ImageExtensions.Any(item => string.Equals(item, extension, StringComparison.OrdinalIgnoreCase));
        }

        private static int CountImages(string directory)
        {
            if (!Directory.Exists(directory))
            {
                return 0;
            }

            return Directory.EnumerateFiles(directory).Count(IsSupportedImageFile);
        }

        private static int CountFiles(string directory, string searchPattern)
        {
            if (!Directory.Exists(directory))
            {
                return 0;
            }

            return Directory.EnumerateFiles(directory, searchPattern).Count();
        }

        private static void CountObjects(string labelDirectory, IReadOnlyList<CClassItem> classes, YoloDatasetStatistics statistics)
        {
            if (!Directory.Exists(labelDirectory) || classes == null || statistics == null)
            {
                return;
            }

            foreach (string labelPath in Directory.EnumerateFiles(labelDirectory, "*.txt"))
            {
                foreach (string line in File.ReadLines(labelPath))
                {
                    string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length != 5 || !int.TryParse(parts[0], out int classIndex))
                    {
                        continue;
                    }

                    if (classIndex < 0 || classIndex >= classes.Count)
                    {
                        continue;
                    }

                    statistics.AddObject(classes[classIndex]?.Text ?? "");
                }
            }
        }
    }
}
