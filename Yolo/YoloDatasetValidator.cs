using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

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
        public int TestImageCount { get; internal set; }
        public int TrainLabelCount { get; internal set; }
        public int ValidLabelCount { get; internal set; }
        public int TestLabelCount { get; internal set; }
        public int TrainValidImageNameOverlapCount { get; internal set; }
        public int TrainValidImageContentOverlapCount { get; internal set; }
        public string TrainValidImageOverlapExample { get; internal set; } = "";
        public int SplitImageContentOverlapCount { get; internal set; }
        public string SplitImageOverlapExample { get; internal set; } = "";
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
            ValidateOptionalImageAndLabelSet("test", data.TestImagesPath, Path.Combine(data.OutputRootPath, "data", "test", "labels"), data.ClassNamedList, errors);
            ValidateSplitSeparation("train", data.TrainImagesPath, "valid", data.ValidImagesPath, errors);
            ValidateSplitSeparation("train", data.TrainImagesPath, "test", data.TestImagesPath, errors);
            ValidateSplitSeparation("valid", data.ValidImagesPath, "test", data.TestImagesPath, errors);
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
            string testLabelsPath = Path.Combine(data.OutputRootPath, "data", "test", "labels");

            statistics.TrainImageCount = CountImages(data.TrainImagesPath);
            statistics.ValidImageCount = CountImages(data.ValidImagesPath);
            statistics.TestImageCount = CountImages(data.TestImagesPath);
            statistics.TrainLabelCount = CountFiles(trainLabelsPath, "*.txt");
            statistics.ValidLabelCount = CountFiles(validLabelsPath, "*.txt");
            statistics.TestLabelCount = CountFiles(testLabelsPath, "*.txt");
            AddSplitOverlapStatistics(data.TrainImagesPath, data.ValidImagesPath, data.TestImagesPath, statistics);

            CountObjects(trainLabelsPath, data.ClassNamedList, statistics);
            CountObjects(validLabelsPath, data.ClassNamedList, statistics);
            CountObjects(testLabelsPath, data.ClassNamedList, statistics);
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

        private static void ValidateOptionalImageAndLabelSet(string mode, string imageDirectory, string labelDirectory, IReadOnlyList<CClassItem> classes, List<string> errors)
        {
            bool hasImages = EnumerateSupportedImages(imageDirectory).Any();
            bool hasLabels = Directory.Exists(labelDirectory) && Directory.EnumerateFiles(labelDirectory, "*.txt").Any();
            if (!hasImages && !hasLabels)
            {
                return;
            }

            ValidateImageAndLabelSet(mode, imageDirectory, labelDirectory, classes, errors);
        }

        private static void ValidateSplitSeparation(string leftMode, string leftImageDirectory, string rightMode, string rightImageDirectory, List<string> errors)
        {
            YoloDatasetOverlapSummary overlap = BuildSplitOverlapSummary(leftImageDirectory, rightImageDirectory);
            if (overlap.ContentOverlapCount <= 0)
            {
                return;
            }

            string example = string.IsNullOrWhiteSpace(overlap.Example)
                ? ""
                : $" Example: {overlap.Example}.";
            errors.Add($"{leftMode}/{rightMode} image split has duplicate image content: {overlap.ContentOverlapCount} overlapping image(s). Use different split images before training.{example}");
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

        private static IEnumerable<string> EnumerateSupportedImages(string directory)
        {
            if (!Directory.Exists(directory))
            {
                return Enumerable.Empty<string>();
            }

            return Directory.EnumerateFiles(directory).Where(IsSupportedImageFile);
        }

        private static int CountImages(string directory)
        {
            return EnumerateSupportedImages(directory).Count();
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

        private static void AddSplitOverlapStatistics(string trainImageDirectory, string validImageDirectory, string testImageDirectory, YoloDatasetStatistics statistics)
        {
            if (statistics == null)
            {
                return;
            }

            YoloDatasetOverlapSummary trainValid = BuildSplitOverlapSummary(trainImageDirectory, validImageDirectory);
            YoloDatasetOverlapSummary trainTest = BuildSplitOverlapSummary(trainImageDirectory, testImageDirectory);
            YoloDatasetOverlapSummary validTest = BuildSplitOverlapSummary(validImageDirectory, testImageDirectory);
            statistics.TrainValidImageNameOverlapCount = trainValid.NameOverlapCount;
            statistics.TrainValidImageContentOverlapCount = trainValid.ContentOverlapCount;
            statistics.TrainValidImageOverlapExample = trainValid.Example;
            statistics.SplitImageContentOverlapCount = trainValid.ContentOverlapCount + trainTest.ContentOverlapCount + validTest.ContentOverlapCount;
            statistics.SplitImageOverlapExample = new[] { trainValid.Example, trainTest.Example, validTest.Example }
                .FirstOrDefault(example => !string.IsNullOrWhiteSpace(example)) ?? "";
        }

        private static YoloDatasetOverlapSummary BuildSplitOverlapSummary(string leftImageDirectory, string rightImageDirectory)
        {
            List<string> leftImages = EnumerateSupportedImages(leftImageDirectory).ToList();
            List<string> rightImages = EnumerateSupportedImages(rightImageDirectory).ToList();
            if (leftImages.Count == 0 || rightImages.Count == 0)
            {
                return new YoloDatasetOverlapSummary();
            }

            var rightNames = new HashSet<string>(
                rightImages.Select(Path.GetFileName).Where(name => !string.IsNullOrWhiteSpace(name)),
                StringComparer.OrdinalIgnoreCase);
            int nameOverlap = leftImages.Count(path => rightNames.Contains(Path.GetFileName(path)));

            Dictionary<string, string> rightContent = BuildContentMap(rightImages);
            int contentOverlap = 0;
            string example = "";
            foreach (string leftImage in leftImages)
            {
                string hash = BuildFileContentKey(leftImage);
                if (!rightContent.TryGetValue(hash, out string rightImage))
                {
                    continue;
                }

                contentOverlap++;
                if (string.IsNullOrWhiteSpace(example))
                {
                    example = $"{Path.GetFileName(leftImage)} == {Path.GetFileName(rightImage)}";
                }
            }

            return new YoloDatasetOverlapSummary
            {
                NameOverlapCount = nameOverlap,
                ContentOverlapCount = contentOverlap,
                Example = example
            };
        }

        private static Dictionary<string, string> BuildContentMap(IEnumerable<string> imagePaths)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string imagePath in imagePaths ?? Enumerable.Empty<string>())
            {
                string hash = BuildFileContentKey(imagePath);
                if (!map.ContainsKey(hash))
                {
                    map[hash] = imagePath;
                }
            }

            return map;
        }

        private static string BuildFileContentKey(string path)
        {
            var info = new FileInfo(path);
            using FileStream stream = File.OpenRead(path);
            using SHA256 sha = SHA256.Create();
            string hash = Convert.ToBase64String(sha.ComputeHash(stream));
            return $"{info.Length}:{hash}";
        }

        private sealed class YoloDatasetOverlapSummary
        {
            public int NameOverlapCount { get; set; }
            public int ContentOverlapCount { get; set; }
            public string Example { get; set; } = "";
        }
    }
}
