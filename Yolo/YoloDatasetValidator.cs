using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;
using YamlDotNet.Serialization;

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
        private readonly Dictionary<string, int> segmentationObjectCountByClass = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public int TrainImageCount { get; internal set; }
        public int ValidImageCount { get; internal set; }
        public int TestImageCount { get; internal set; }
        public int TrainLabelCount { get; internal set; }
        public int ValidLabelCount { get; internal set; }
        public int TestLabelCount { get; internal set; }
        public int TrainEmptyLabelFileCount { get; internal set; }
        public int ValidEmptyLabelFileCount { get; internal set; }
        public int TestEmptyLabelFileCount { get; internal set; }
        public int TrainSegmentFileCount { get; internal set; }
        public int ValidSegmentFileCount { get; internal set; }
        public int TestSegmentFileCount { get; internal set; }
        public int TrainMaskFileCount { get; internal set; }
        public int ValidMaskFileCount { get; internal set; }
        public int TestMaskFileCount { get; internal set; }
        public int TrainValidImageNameOverlapCount { get; internal set; }
        public int TrainValidImageContentOverlapCount { get; internal set; }
        public string TrainValidImageOverlapExample { get; internal set; } = "";
        public int SplitImageContentOverlapCount { get; internal set; }
        public string SplitImageOverlapExample { get; internal set; } = "";
        public int TotalImageCount => TrainImageCount + ValidImageCount + TestImageCount;
        public int TotalLabelFileCount => TrainLabelCount + ValidLabelCount + TestLabelCount;
        public int TotalEmptyLabelFileCount => TrainEmptyLabelFileCount + ValidEmptyLabelFileCount + TestEmptyLabelFileCount;
        public int TotalSegmentFileCount => TrainSegmentFileCount + ValidSegmentFileCount + TestSegmentFileCount;
        public int TotalMaskFileCount => TrainMaskFileCount + ValidMaskFileCount + TestMaskFileCount;
        public int TotalSegmentationArtifactFileCount => TotalSegmentFileCount + TotalMaskFileCount;
        public int TotalObjectCount => objectCountByClass.Values.Sum();
        public int TotalSegmentationObjectCount => segmentationObjectCountByClass.Values.Sum();
        public int TotalAnnotationObjectCount => TotalObjectCount + TotalSegmentationObjectCount;
        public IReadOnlyDictionary<string, int> ObjectCountByClass => objectCountByClass;
        public IReadOnlyDictionary<string, int> SegmentationObjectCountByClass => segmentationObjectCountByClass;

        internal void AddObject(string className)
        {
            if (string.IsNullOrWhiteSpace(className))
            {
                return;
            }

            objectCountByClass.TryGetValue(className, out int count);
            objectCountByClass[className] = count + 1;
        }

        internal void AddSegmentationObject(string className)
        {
            if (string.IsNullOrWhiteSpace(className))
            {
                return;
            }

            segmentationObjectCountByClass.TryGetValue(className, out int count);
            segmentationObjectCountByClass[className] = count + 1;
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
            LabelingDatasetPurpose purpose = ResolveDatasetPurpose(data);
            ValidateFileExists(data.DataYamlFilePath, "data.yaml", errors);
            ValidateDataYamlContract(data, errors);

            if (purpose == LabelingDatasetPurpose.Segmentation)
            {
                ValidateSegmentationImageAndAnnotationSet(
                    "train",
                    data.TrainImagesPath,
                    Path.Combine(data.OutputRootPath, "data", "train", "segments"),
                    Path.Combine(data.OutputRootPath, "data", "train", "masks"),
                    Path.Combine(data.OutputRootPath, "data", "train", "labels"),
                    data.ClassNamedList,
                    errors);
                ValidateSegmentationImageAndAnnotationSet(
                    "valid",
                    data.ValidImagesPath,
                    Path.Combine(data.OutputRootPath, "data", "valid", "segments"),
                    Path.Combine(data.OutputRootPath, "data", "valid", "masks"),
                    Path.Combine(data.OutputRootPath, "data", "valid", "labels"),
                    data.ClassNamedList,
                    errors);
                ValidateOptionalSegmentationImageAndAnnotationSet(
                    "test",
                    data.TestImagesPath,
                    Path.Combine(data.OutputRootPath, "data", "test", "segments"),
                    Path.Combine(data.OutputRootPath, "data", "test", "masks"),
                    Path.Combine(data.OutputRootPath, "data", "test", "labels"),
                    data.ClassNamedList,
                    errors);
            }
            else
            {
                ValidateImageAndLabelSet("train", data.TrainImagesPath, Path.Combine(data.OutputRootPath, "data", "train", "labels"), data.ClassNamedList, errors);
                ValidateImageAndLabelSet("valid", data.ValidImagesPath, Path.Combine(data.OutputRootPath, "data", "valid", "labels"), data.ClassNamedList, errors);
                ValidateOptionalImageAndLabelSet("test", data.TestImagesPath, Path.Combine(data.OutputRootPath, "data", "test", "labels"), data.ClassNamedList, errors);
            }

            ValidateSplitSeparation("train", data.TrainImagesPath, "valid", data.ValidImagesPath, errors);
            ValidateSplitSeparation("train", data.TrainImagesPath, "test", data.TestImagesPath, errors);
            ValidateSplitSeparation("valid", data.ValidImagesPath, "test", data.TestImagesPath, errors);
            YoloDatasetStatistics statistics = BuildStatistics(data);
            AppendPurposeAnnotationPolicyErrors(purpose, statistics, errors);

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
            string trainSegmentsPath = Path.Combine(data.OutputRootPath, "data", "train", "segments");
            string validSegmentsPath = Path.Combine(data.OutputRootPath, "data", "valid", "segments");
            string testSegmentsPath = Path.Combine(data.OutputRootPath, "data", "test", "segments");
            string trainMasksPath = Path.Combine(data.OutputRootPath, "data", "train", "masks");
            string validMasksPath = Path.Combine(data.OutputRootPath, "data", "valid", "masks");
            string testMasksPath = Path.Combine(data.OutputRootPath, "data", "test", "masks");

            statistics.TrainImageCount = CountImages(data.TrainImagesPath);
            statistics.ValidImageCount = CountImages(data.ValidImagesPath);
            statistics.TestImageCount = CountImages(data.TestImagesPath);
            statistics.TrainLabelCount = CountFiles(trainLabelsPath, "*.txt");
            statistics.ValidLabelCount = CountFiles(validLabelsPath, "*.txt");
            statistics.TestLabelCount = CountFiles(testLabelsPath, "*.txt");
            statistics.TrainEmptyLabelFileCount = CountEmptyLabelFiles(trainLabelsPath);
            statistics.ValidEmptyLabelFileCount = CountEmptyLabelFiles(validLabelsPath);
            statistics.TestEmptyLabelFileCount = CountEmptyLabelFiles(testLabelsPath);
            statistics.TrainSegmentFileCount = CountFiles(trainSegmentsPath, "*.json");
            statistics.ValidSegmentFileCount = CountFiles(validSegmentsPath, "*.json");
            statistics.TestSegmentFileCount = CountFiles(testSegmentsPath, "*.json");
            statistics.TrainMaskFileCount = CountFiles(trainMasksPath, "*.png");
            statistics.ValidMaskFileCount = CountFiles(validMasksPath, "*.png");
            statistics.TestMaskFileCount = CountFiles(testMasksPath, "*.png");
            AddSplitOverlapStatistics(data.TrainImagesPath, data.ValidImagesPath, data.TestImagesPath, statistics);

            CountObjects(trainLabelsPath, data.ClassNamedList, statistics);
            CountObjects(validLabelsPath, data.ClassNamedList, statistics);
            CountObjects(testLabelsPath, data.ClassNamedList, statistics);
            CountSegmentationObjects(trainSegmentsPath, data.ClassNamedList, statistics);
            CountSegmentationObjects(validSegmentsPath, data.ClassNamedList, statistics);
            CountSegmentationObjects(testSegmentsPath, data.ClassNamedList, statistics);
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

        private static void ValidateDataYamlContract(CData data, List<string> errors)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.DataYamlFilePath) || !File.Exists(data.DataYamlFilePath))
            {
                return;
            }

            byte[] yamlBytes;
            try
            {
                yamlBytes = File.ReadAllBytes(data.DataYamlFilePath);
            }
            catch (Exception ex)
            {
                errors.Add($"data.yaml could not be read: {ex.Message}");
                return;
            }

            if (yamlBytes.Length >= 3 && yamlBytes[0] == 0xEF && yamlBytes[1] == 0xBB && yamlBytes[2] == 0xBF)
            {
                errors.Add("data.yaml must be UTF-8 without BOM. YOLOv5 can misread the first key when a BOM is present.");
            }

            YoloDataYamlContract yaml;
            try
            {
                string yamlText = Encoding.UTF8.GetString(yamlBytes);
                yaml = new DeserializerBuilder()
                    .IgnoreUnmatchedProperties()
                    .Build()
                    .Deserialize<YoloDataYamlContract>(yamlText);
            }
            catch (Exception ex)
            {
                errors.Add($"data.yaml is invalid YAML: {ex.Message}");
                return;
            }

            if (yaml == null)
            {
                errors.Add("data.yaml is empty or unreadable.");
                return;
            }

            ValidateDataYamlClasses(data, yaml, errors);
            ValidateDataYamlPath(data.DataYamlFilePath, yaml.path, "train", yaml.train, data.TrainImagesPath, required: true, errors);
            ValidateDataYamlPath(data.DataYamlFilePath, yaml.path, "val", yaml.val, data.ValidImagesPath, required: true, errors);
            ValidateDataYamlPath(data.DataYamlFilePath, yaml.path, "test", yaml.test, data.TestImagesPath, required: false, errors);
        }

        private static void ValidateDataYamlClasses(CData data, YoloDataYamlContract yaml, List<string> errors)
        {
            List<string> classNames = data.ClassNamedList?
                .Select(item => item?.Text?.Trim() ?? string.Empty)
                .ToList()
                ?? new List<string>();

            if (yaml.nc != classNames.Count)
            {
                errors.Add($"data.yaml class count does not match project classes. yaml nc:{yaml.nc}, project classes:{classNames.Count}.");
            }

            if (yaml.names == null || yaml.names.Count != classNames.Count)
            {
                errors.Add($"data.yaml class names do not match project classes. yaml names:{yaml.names?.Count ?? 0}, project classes:{classNames.Count}.");
                return;
            }

            for (int index = 0; index < classNames.Count; index++)
            {
                if (!string.Equals(yaml.names[index]?.Trim() ?? string.Empty, classNames[index], StringComparison.Ordinal))
                {
                    errors.Add($"data.yaml class name mismatch at index {index}. yaml:'{yaml.names[index]}', project:'{classNames[index]}'.");
                }
            }
        }

        private static void ValidateDataYamlPath(
            string yamlFilePath,
            string yamlRootPath,
            string key,
            string yamlPath,
            string expectedPath,
            bool required,
            List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(yamlPath))
            {
                if (required)
                {
                    errors.Add($"data.yaml '{key}' path is missing.");
                }

                return;
            }

            string resolved = ResolveDataYamlPath(yamlFilePath, yamlRootPath, yamlPath);
            string expected = NormalizeFullPath(expectedPath);
            if (!string.Equals(resolved, expected, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"data.yaml '{key}' path does not match the current project dataset. yaml:'{yamlPath}', resolved:'{resolved}', expected:'{expected}'.");
            }
        }

        private static string ResolveDataYamlPath(string yamlFilePath, string yamlRootPath, string yamlPath)
        {
            if (string.IsNullOrWhiteSpace(yamlPath))
            {
                return string.Empty;
            }

            string normalizedPath = yamlPath.Replace('/', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(normalizedPath))
            {
                return NormalizeFullPath(normalizedPath);
            }

            string root = string.IsNullOrWhiteSpace(yamlRootPath)
                ? Path.GetDirectoryName(yamlFilePath)
                : yamlRootPath.Replace('/', Path.DirectorySeparatorChar);
            if (!Path.IsPathRooted(root))
            {
                string yamlDirectory = Path.GetDirectoryName(yamlFilePath) ?? string.Empty;
                root = Path.Combine(yamlDirectory, root);
            }

            return NormalizeFullPath(Path.Combine(root ?? string.Empty, normalizedPath));
        }

        private static string NormalizeFullPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
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

        private static void ValidateSegmentationImageAndAnnotationSet(
            string mode,
            string imageDirectory,
            string segmentDirectory,
            string maskDirectory,
            string labelDirectory,
            IReadOnlyList<CClassItem> classes,
            List<string> errors)
        {
            if (!Directory.Exists(imageDirectory))
            {
                errors.Add($"{mode} image directory does not exist: {imageDirectory}");
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
                string fileStem = Path.GetFileNameWithoutExtension(imagePath);
                string segmentPath = Path.Combine(segmentDirectory, $"{fileStem}.json");
                string maskPath = Path.Combine(maskDirectory, $"{fileStem}.png");
                string labelPath = Path.Combine(labelDirectory, $"{fileStem}.txt");
                bool hasSegment = File.Exists(segmentPath);
                bool hasMask = File.Exists(maskPath);
                if (!hasSegment && !hasMask)
                {
                    if (!IsEmptyLabelFile(labelPath))
                    {
                        errors.Add($"{mode} segmentation annotation or empty background label is missing for image: {Path.GetFileName(imagePath)}");
                    }

                    continue;
                }

                if (hasSegment)
                {
                    ValidateSegmentFile(mode, segmentPath, classes, errors);
                }
            }
        }

        private static void ValidateOptionalSegmentationImageAndAnnotationSet(
            string mode,
            string imageDirectory,
            string segmentDirectory,
            string maskDirectory,
            string labelDirectory,
            IReadOnlyList<CClassItem> classes,
            List<string> errors)
        {
            bool hasImages = EnumerateSupportedImages(imageDirectory).Any();
            bool hasSegments = Directory.Exists(segmentDirectory) && Directory.EnumerateFiles(segmentDirectory, "*.json").Any();
            bool hasMasks = Directory.Exists(maskDirectory) && Directory.EnumerateFiles(maskDirectory, "*.png").Any();
            bool hasLabels = Directory.Exists(labelDirectory) && Directory.EnumerateFiles(labelDirectory, "*.txt").Any();
            if (!hasImages && !hasSegments && !hasMasks && !hasLabels)
            {
                return;
            }

            ValidateSegmentationImageAndAnnotationSet(mode, imageDirectory, segmentDirectory, maskDirectory, labelDirectory, classes, errors);
        }

        private static void ValidateSegmentFile(string mode, string segmentPath, IReadOnlyList<CClassItem> classes, List<string> errors)
        {
            SegmentationAnnotationFile annotation;
            try
            {
                annotation = JsonConvert.DeserializeObject<SegmentationAnnotationFile>(File.ReadAllText(segmentPath));
            }
            catch (Exception ex)
            {
                errors.Add($"{mode} segment JSON is invalid at {Path.GetFileName(segmentPath)}: {ex.Message}");
                return;
            }

            int polygonCount = 0;
            foreach (SegmentationPolygonRecord record in annotation?.Polygons ?? new List<SegmentationPolygonRecord>())
            {
                polygonCount++;
                if (record?.Points == null || record.Points.Count < 3)
                {
                    errors.Add($"{mode} segment polygon has fewer than three points at {Path.GetFileName(segmentPath)}:{polygonCount}");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(record.ClassName)
                    && (classes == null || record.ClassIndex < 0 || record.ClassIndex >= classes.Count))
                {
                    errors.Add($"{mode} segment polygon has invalid class index at {Path.GetFileName(segmentPath)}:{polygonCount}");
                }
            }

            if (polygonCount == 0)
            {
                errors.Add($"{mode} segment JSON has no polygons: {Path.GetFileName(segmentPath)}");
            }
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

        private static bool IsEmptyLabelFile(string labelPath)
        {
            return File.Exists(labelPath)
                && File.ReadAllText(labelPath).Trim().Length == 0;
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

        private static int CountEmptyLabelFiles(string directory)
        {
            if (!Directory.Exists(directory))
            {
                return 0;
            }

            return Directory.EnumerateFiles(directory, "*.txt")
                .Count(path => File.ReadAllText(path).Trim().Length == 0);
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

        private static void CountSegmentationObjects(string segmentDirectory, IReadOnlyList<CClassItem> classes, YoloDatasetStatistics statistics)
        {
            if (!Directory.Exists(segmentDirectory) || statistics == null)
            {
                return;
            }

            foreach (string segmentPath in Directory.EnumerateFiles(segmentDirectory, "*.json"))
            {
                SegmentationAnnotationFile annotation;
                try
                {
                    annotation = JsonConvert.DeserializeObject<SegmentationAnnotationFile>(File.ReadAllText(segmentPath));
                }
                catch
                {
                    continue;
                }

                foreach (SegmentationPolygonRecord record in annotation?.Polygons ?? new List<SegmentationPolygonRecord>())
                {
                    if (record?.Points == null || record.Points.Count < 3)
                    {
                        continue;
                    }

                    string className = ResolveSegmentationClassName(record, classes);
                    statistics.AddSegmentationObject(className);
                }
            }
        }

        private static string ResolveSegmentationClassName(SegmentationPolygonRecord record, IReadOnlyList<CClassItem> classes)
        {
            if (!string.IsNullOrWhiteSpace(record?.ClassName))
            {
                return record.ClassName;
            }

            return record != null && classes != null && record.ClassIndex >= 0 && record.ClassIndex < classes.Count
                ? classes[record.ClassIndex]?.Text ?? string.Empty
                : string.Empty;
        }

        private static LabelingDatasetPurpose ResolveDatasetPurpose(CData data)
        {
            data?.ProjectSettings?.EnsureDefaults();
            return data?.ProjectSettings?.DatasetPurpose ?? LabelingDatasetPurpose.ObjectDetection;
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

        private sealed class YoloDataYamlContract
        {
            public string path { get; set; } = "";
            public string train { get; set; } = "";
            public string val { get; set; } = "";
            public string test { get; set; } = "";
            public int nc { get; set; }
            public List<string> names { get; set; } = new List<string>();
        }
    }
}
