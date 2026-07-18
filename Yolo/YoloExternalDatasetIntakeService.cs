using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using YamlDotNet.Serialization;

namespace MvcVisionSystem.Yolo
{
    public sealed class YoloExternalDatasetIntakeReport
    {
        public YoloExternalDatasetIntakeReport(
            string dataYamlFilePath,
            string datasetRootPath,
            LabelingDatasetPurpose purpose,
            IEnumerable<string> classNames,
            YoloExternalDatasetSplitSummary train,
            YoloExternalDatasetSplitSummary valid,
            YoloExternalDatasetSplitSummary test,
            IReadOnlyDictionary<string, int> annotationCountByClass,
            IEnumerable<string> errors,
            string sourceFingerprintSha256,
            int sourceFileCount)
        {
            DataYamlFilePath = dataYamlFilePath ?? string.Empty;
            DatasetRootPath = datasetRootPath ?? string.Empty;
            Purpose = purpose;
            ClassNames = (classNames ?? Array.Empty<string>()).ToArray();
            Train = train ?? new YoloExternalDatasetSplitSummary("train", string.Empty, 0, 0, 0);
            Valid = valid ?? new YoloExternalDatasetSplitSummary("val", string.Empty, 0, 0, 0);
            Test = test ?? new YoloExternalDatasetSplitSummary("test", string.Empty, 0, 0, 0);
            AnnotationCountByClass = new Dictionary<string, int>(annotationCountByClass ?? new Dictionary<string, int>(), StringComparer.OrdinalIgnoreCase);
            Errors = (errors ?? Array.Empty<string>()).Where(error => !string.IsNullOrWhiteSpace(error)).ToArray();
            SourceFingerprintSha256 = sourceFingerprintSha256 ?? string.Empty;
            SourceFileCount = Math.Max(0, sourceFileCount);
        }

        public string DataYamlFilePath { get; }

        public string DatasetRootPath { get; }

        public LabelingDatasetPurpose Purpose { get; }

        public IReadOnlyList<string> ClassNames { get; }

        public YoloExternalDatasetSplitSummary Train { get; }

        public YoloExternalDatasetSplitSummary Valid { get; }

        public YoloExternalDatasetSplitSummary Test { get; }

        public IReadOnlyDictionary<string, int> AnnotationCountByClass { get; }

        public IReadOnlyList<string> Errors { get; }

        public string SourceFingerprintSha256 { get; }

        public int SourceFileCount { get; }

        public bool IsReady => Errors.Count == 0;

        public int TotalImageCount => Train.ImageCount + Valid.ImageCount + Test.ImageCount;

        public int TotalLabelFileCount => Train.LabelFileCount + Valid.LabelFileCount + Test.LabelFileCount;

        public int TotalAnnotationCount => AnnotationCountByClass.Values.Sum();

        public string Summary =>
            $"{YoloExternalDatasetIntakeService.FormatPurpose(Purpose)} / train {Train.ImageCount} / val {Valid.ImageCount} / test {Test.ImageCount} / labels {TotalLabelFileCount} / annotations {TotalAnnotationCount} / classes {ClassNames.Count} / source files {SourceFileCount}";
    }

    public sealed class YoloExternalDatasetSplitSummary
    {
        public YoloExternalDatasetSplitSummary(string name, string imageDirectoryPath, int imageCount, int labelFileCount, int emptyLabelFileCount)
        {
            Name = name ?? string.Empty;
            ImageDirectoryPath = imageDirectoryPath ?? string.Empty;
            ImageCount = Math.Max(0, imageCount);
            LabelFileCount = Math.Max(0, labelFileCount);
            EmptyLabelFileCount = Math.Max(0, emptyLabelFileCount);
        }

        public string Name { get; }

        public string ImageDirectoryPath { get; }

        public int ImageCount { get; }

        public int LabelFileCount { get; }

        public int EmptyLabelFileCount { get; }
    }

    // Validates a native Ultralytics detection/segmentation dataset without copying or modifying it.
    public static class YoloExternalDatasetIntakeService
    {
        private static readonly HashSet<string> ImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff"
        };

        public static YoloExternalDatasetIntakeReport Build(string dataYamlFilePath, LabelingDatasetPurpose purpose)
        {
            var errors = new List<string>();
            if (!IsSupportedPurpose(purpose))
            {
                errors.Add("External native YOLO intake supports ObjectDetection or Segmentation only.");
                return Empty(dataYamlFilePath, purpose, errors);
            }

            string yamlPath = NormalizeYamlPath(dataYamlFilePath, errors);
            if (errors.Count > 0)
            {
                return Empty(yamlPath, purpose, errors);
            }

            Dictionary<object, object> document = ReadYaml(yamlPath, errors);
            if (document == null)
            {
                return Empty(yamlPath, purpose, errors);
            }

            string datasetRoot = ResolveDatasetRoot(yamlPath, ReadScalar(document, "path"), errors);
            List<string> classNames = ReadClassNames(document, errors);
            ValidateClassCount(document, classNames, errors);

            string trainDirectory = ResolveSplitDirectory(yamlPath, datasetRoot, ReadScalar(document, "train"), "train", required: true, errors);
            string validDirectory = ResolveSplitDirectory(yamlPath, datasetRoot, ReadScalar(document, "val"), "val", required: true, errors);
            string testDirectory = ResolveSplitDirectory(yamlPath, datasetRoot, ReadScalar(document, "test"), "test", required: false, errors);

            var annotationCountByClass = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            SplitScan train = ScanSplit("train", trainDirectory, required: true, purpose, classNames, annotationCountByClass, errors);
            SplitScan valid = ScanSplit("val", validDirectory, required: true, purpose, classNames, annotationCountByClass, errors);
            SplitScan test = ScanSplit("test", testDirectory, required: false, purpose, classNames, annotationCountByClass, errors);
            ValidateSplitPathSeparation(train.ImagePaths, valid.ImagePaths, test.ImagePaths, errors);

            if (classNames.Count == 0)
            {
                AddError(errors, "data.yaml names must declare at least one class.");
            }

            if (train.AnnotationCount == 0)
            {
                AddError(errors, "External YOLO train split has no annotations. Select a dataset with reviewed YOLO labels before training.");
            }

            int sourceFileCount = 0;
            string sourceFingerprintSha256 = errors.Count == 0
                ? BuildSourceFingerprint(yamlPath, train, valid, test, out sourceFileCount, errors)
                : string.Empty;

            return new YoloExternalDatasetIntakeReport(
                yamlPath,
                datasetRoot,
                purpose,
                classNames,
                train.ToSummary(),
                valid.ToSummary(),
                test.ToSummary(),
                annotationCountByClass,
                errors,
                sourceFingerprintSha256,
                sourceFileCount);
        }

        public static bool IsSupportedPurpose(LabelingDatasetPurpose purpose)
            => purpose == LabelingDatasetPurpose.ObjectDetection || purpose == LabelingDatasetPurpose.Segmentation;

        public static string FormatPurpose(LabelingDatasetPurpose purpose)
            => purpose == LabelingDatasetPurpose.Segmentation ? "Segmentation" : "ObjectDetection";

        public static void ApplyValidation(
            ExternalYoloDatasetSettings settings,
            YoloExternalDatasetIntakeReport report,
            bool acceptSourceIdentity = false)
        {
            if (settings == null || report == null)
            {
                return;
            }

            settings.LastValidationSucceeded = report.IsReady;
            settings.LastValidationUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            settings.LastValidationSummary = report.IsReady
                ? report.Summary
                : report.Errors.FirstOrDefault() ?? "External YOLO data.yaml validation failed.";
            settings.TrainImageCount = report.Train.ImageCount;
            settings.ValidImageCount = report.Valid.ImageCount;
            settings.TestImageCount = report.Test.ImageCount;
            settings.LabelFileCount = report.TotalLabelFileCount;
            settings.AnnotationCount = report.TotalAnnotationCount;
            settings.ClassCount = report.ClassNames.Count;
            if (acceptSourceIdentity || string.IsNullOrWhiteSpace(settings.SourceFingerprintSha256))
            {
                settings.SourceFingerprintSha256 = report.SourceFingerprintSha256;
                settings.SourceFileCount = report.SourceFileCount;
                if (acceptSourceIdentity && report.IsReady)
                {
                    settings.RequiresExplicitReactivation = false;
                }
            }
        }

        public static bool HasCurrentSourceIdentity(
            ExternalYoloDatasetSettings settings,
            YoloExternalDatasetIntakeReport report,
            out string error)
        {
            string expected = settings?.SourceFingerprintSha256?.Trim() ?? string.Empty;
            string actual = report?.SourceFingerprintSha256?.Trim() ?? string.Empty;
            if (expected.Length == 0)
            {
                error = "External YOLO source identity baseline is missing. Revalidate and explicitly activate this data.yaml before training.";
                return false;
            }

            if (actual.Length == 0)
            {
                error = "External YOLO source identity could not be calculated. Revalidate the selected data.yaml before training.";
                return false;
            }

            if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            {
                error = "External YOLO source files changed after validation. Revalidate and explicitly activate this data.yaml before training.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public static void MarkSourceIdentityRequiresReactivation(
            ExternalYoloDatasetSettings settings,
            string error)
        {
            if (settings == null)
            {
                return;
            }

            settings.UseForTraining = false;
            settings.LastValidationSucceeded = false;
            settings.RequiresExplicitReactivation = true;
            settings.LastValidationUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            settings.LastValidationSummary = string.IsNullOrWhiteSpace(error)
                ? "External YOLO source identity changed after validation."
                : error;
        }

        public static void RecordTrainingRequest(
            ExternalYoloDatasetSettings settings,
            PythonModelSettings modelSettings,
            string model,
            string task,
            string trainingWeight,
            string runName,
            string sourceFingerprintSha256)
        {
            if (settings == null)
            {
                return;
            }

            settings.LastTrainingUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            settings.LastTrainingSourceFingerprintSha256 = sourceFingerprintSha256 ?? string.Empty;
            settings.LastTrainingDataYamlFilePath = settings.DataYamlFilePath ?? string.Empty;
            settings.LastTrainingModel = model ?? string.Empty;
            settings.LastTrainingTask = task ?? string.Empty;
            settings.LastTrainingRunName = runName ?? string.Empty;
            settings.LastTrainingWeightFile = trainingWeight ?? string.Empty;
            settings.LastTrainingResolvedWeightFile = ResolveExistingFilePath(
                trainingWeight,
                modelSettings?.ProjectRootPath);
            settings.LastTrainingWeightSha256 = TryComputeFileSha256(settings.LastTrainingResolvedWeightFile);
            settings.LastTrainingPythonExecutablePath = modelSettings?.PythonExecutablePath ?? string.Empty;
            settings.LastTrainingClientScriptPath = modelSettings?.ClientScriptPath ?? string.Empty;
            settings.LastTrainingClientScriptSha256 = TryComputeFileSha256(settings.LastTrainingClientScriptPath);
        }

        private static YoloExternalDatasetIntakeReport Empty(string dataYamlFilePath, LabelingDatasetPurpose purpose, IEnumerable<string> errors)
            => new YoloExternalDatasetIntakeReport(
                dataYamlFilePath,
                string.Empty,
                purpose,
                Array.Empty<string>(),
                new YoloExternalDatasetSplitSummary("train", string.Empty, 0, 0, 0),
                new YoloExternalDatasetSplitSummary("val", string.Empty, 0, 0, 0),
                new YoloExternalDatasetSplitSummary("test", string.Empty, 0, 0, 0),
                new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                errors,
                string.Empty,
                0);

        private static string NormalizeYamlPath(string dataYamlFilePath, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(dataYamlFilePath))
            {
                errors.Add("Select a native YOLO data.yaml file.");
                return string.Empty;
            }

            string extension = Path.GetExtension(dataYamlFilePath);
            if (!string.Equals(extension, ".yaml", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(extension, ".yml", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("External YOLO dataset selection must be a .yaml or .yml file.");
                return string.Empty;
            }

            try
            {
                string fullPath = Path.GetFullPath(dataYamlFilePath);
                if (!File.Exists(fullPath))
                {
                    errors.Add($"External YOLO data.yaml was not found: {fullPath}");
                }

                return fullPath;
            }
            catch (Exception ex)
            {
                errors.Add($"External YOLO data.yaml path is invalid: {ex.Message}");
                return string.Empty;
            }
        }

        private static Dictionary<object, object> ReadYaml(string yamlPath, List<string> errors)
        {
            try
            {
                string yaml = File.ReadAllText(yamlPath);
                Dictionary<object, object> document = new DeserializerBuilder()
                    .IgnoreUnmatchedProperties()
                    .Build()
                    .Deserialize<Dictionary<object, object>>(yaml);
                if (document == null || document.Count == 0)
                {
                    errors.Add("External YOLO data.yaml is empty.");
                    return null;
                }

                return document;
            }
            catch (Exception ex)
            {
                errors.Add($"External YOLO data.yaml is invalid YAML: {ex.Message}");
                return null;
            }
        }

        private static string ResolveDatasetRoot(string yamlPath, string configuredRoot, List<string> errors)
        {
            string yamlDirectory = Path.GetDirectoryName(yamlPath) ?? string.Empty;
            string rootText = NormalizeYamlScalar(configuredRoot);
            string candidate = string.IsNullOrWhiteSpace(rootText) || rootText == "."
                ? yamlDirectory
                : Path.IsPathRooted(rootText)
                    ? rootText
                    : Path.Combine(yamlDirectory, rootText);
            try
            {
                string fullPath = Path.GetFullPath(candidate);
                if (!Directory.Exists(fullPath))
                {
                    errors.Add($"External YOLO data.yaml path root does not exist: {fullPath}");
                }

                return fullPath;
            }
            catch (Exception ex)
            {
                errors.Add($"External YOLO data.yaml path root is invalid: {ex.Message}");
                return string.Empty;
            }
        }

        private static string ResolveSplitDirectory(
            string yamlPath,
            string datasetRoot,
            string configuredSplit,
            string key,
            bool required,
            List<string> errors)
        {
            string splitText = NormalizeYamlScalar(configuredSplit);
            if (string.IsNullOrWhiteSpace(splitText))
            {
                if (required)
                {
                    errors.Add($"External YOLO data.yaml '{key}' path is required.");
                }

                return string.Empty;
            }

            string candidate = Path.IsPathRooted(splitText)
                ? splitText
                : Path.Combine(string.IsNullOrWhiteSpace(datasetRoot) ? Path.GetDirectoryName(yamlPath) ?? string.Empty : datasetRoot, splitText);
            try
            {
                string fullPath = Path.GetFullPath(candidate);
                if (!Directory.Exists(fullPath))
                {
                    errors.Add($"External YOLO {key} image directory does not exist: {fullPath}");
                }

                return fullPath;
            }
            catch (Exception ex)
            {
                errors.Add($"External YOLO {key} path is invalid: {ex.Message}");
                return string.Empty;
            }
        }

        private static List<string> ReadClassNames(Dictionary<object, object> document, List<string> errors)
        {
            object rawNames = ReadRaw(document, "names");
            if (rawNames == null)
            {
                errors.Add("External YOLO data.yaml names are required.");
                return new List<string>();
            }

            var names = new List<string>();
            if (rawNames is IDictionary<object, object> map)
            {
                foreach (KeyValuePair<object, object> entry in map
                    .OrderBy(item => TryParseIndex(item.Key, out int index) ? index : int.MaxValue)
                    .ThenBy(item => Convert.ToString(item.Key, CultureInfo.InvariantCulture), StringComparer.Ordinal))
                {
                    if (!TryParseIndex(entry.Key, out int expectedIndex) || expectedIndex != names.Count)
                    {
                        errors.Add("External YOLO data.yaml names mapping must use consecutive indexes starting at 0.");
                        return new List<string>();
                    }

                    string name = NormalizeYamlScalar(entry.Value);
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        errors.Add($"External YOLO data.yaml class name at index {expectedIndex} is empty.");
                        return new List<string>();
                    }

                    names.Add(name);
                }
            }
            else if (rawNames is IEnumerable sequence && rawNames is not string)
            {
                foreach (object item in sequence)
                {
                    string name = NormalizeYamlScalar(item);
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        errors.Add("External YOLO data.yaml names contains an empty class name.");
                        return new List<string>();
                    }

                    names.Add(name);
                }
            }
            else
            {
                errors.Add("External YOLO data.yaml names must be a list or indexed mapping.");
            }

            if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Count)
            {
                errors.Add("External YOLO data.yaml names contains duplicate class names.");
            }

            return names;
        }

        private static void ValidateClassCount(Dictionary<object, object> document, IReadOnlyList<string> classNames, List<string> errors)
        {
            string rawNc = ReadScalar(document, "nc");
            if (string.IsNullOrWhiteSpace(rawNc))
            {
                return;
            }

            if (!int.TryParse(rawNc, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count)
                || count != classNames.Count)
            {
                errors.Add($"External YOLO data.yaml nc does not match names. nc:{rawNc}, names:{classNames.Count}.");
            }
        }

        private static SplitScan ScanSplit(
            string name,
            string imageDirectory,
            bool required,
            LabelingDatasetPurpose purpose,
            IReadOnlyList<string> classNames,
            Dictionary<string, int> annotationCountByClass,
            List<string> errors)
        {
            var scan = new SplitScan(name, imageDirectory);
            if (string.IsNullOrWhiteSpace(imageDirectory) || !Directory.Exists(imageDirectory))
            {
                return scan;
            }

            string labelsDirectory = ResolveLabelsDirectory(imageDirectory);
            if (string.IsNullOrWhiteSpace(labelsDirectory))
            {
                AddError(errors, $"External YOLO {name} image directory must be under an images folder so labels can resolve: {imageDirectory}");
                return scan;
            }

            string[] imagePaths;
            try
            {
                imagePaths = Directory.EnumerateFiles(imageDirectory, "*", SearchOption.AllDirectories)
                    .Where(path => ImageExtensions.Contains(Path.GetExtension(path)))
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch (Exception ex)
            {
                AddError(errors, $"External YOLO {name} image directory could not be read: {ex.Message}");
                return scan;
            }

            scan.ImagePaths.AddRange(imagePaths);
            if (required && imagePaths.Length == 0)
            {
                AddError(errors, $"External YOLO {name} image directory contains no supported images: {imageDirectory}");
            }

            foreach (string imagePath in imagePaths)
            {
                scan.SourceFilePaths.Add(imagePath);
                string relativePath = Path.GetRelativePath(imageDirectory, imagePath);
                string labelPath = Path.ChangeExtension(Path.Combine(labelsDirectory, relativePath), ".txt");
                if (!File.Exists(labelPath))
                {
                    continue;
                }

                scan.SourceFilePaths.Add(labelPath);
                scan.LabelFileCount++;
                string[] lines;
                try
                {
                    lines = File.ReadAllLines(labelPath);
                }
                catch (Exception ex)
                {
                    AddError(errors, $"External YOLO {name} label file could not be read: {labelPath}. {ex.Message}");
                    continue;
                }

                if (lines.All(string.IsNullOrWhiteSpace))
                {
                    scan.EmptyLabelFileCount++;
                    continue;
                }

                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    if (TryValidateLabelLine(
                        lines[lineIndex],
                        purpose,
                        classNames,
                        out string className,
                        out string error))
                    {
                        scan.AnnotationCount++;
                        annotationCountByClass.TryGetValue(className, out int existing);
                        annotationCountByClass[className] = existing + 1;
                    }
                    else if (!string.IsNullOrWhiteSpace(error))
                    {
                        AddError(errors, $"External YOLO {name} label {Path.GetFileName(labelPath)} line {lineIndex + 1}: {error}");
                    }
                }
            }

            return scan;
        }

        private static bool TryValidateLabelLine(
            string line,
            LabelingDatasetPurpose purpose,
            IReadOnlyList<string> classNames,
            out string className,
            out string error)
        {
            className = string.Empty;
            error = string.Empty;
            string[] tokens = (line ?? string.Empty)
                .Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
            {
                return true;
            }

            bool expectsSegment = purpose == LabelingDatasetPurpose.Segmentation;
            bool validTokenCount = expectsSegment
                ? tokens.Length >= 7 && ((tokens.Length - 1) % 2 == 0)
                : tokens.Length == 5;
            if (!validTokenCount)
            {
                error = expectsSegment
                    ? "Segmentation labels require class plus at least three normalized polygon points."
                    : "ObjectDetection labels require class plus x, y, width, height.";
                return false;
            }

            if (!int.TryParse(tokens[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int classIndex)
                || classIndex < 0
                || classIndex >= classNames.Count)
            {
                error = $"Class index '{tokens[0]}' is outside data.yaml names.";
                return false;
            }

            for (int index = 1; index < tokens.Length; index++)
            {
                if (!double.TryParse(tokens[index], NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                    || double.IsNaN(value)
                    || double.IsInfinity(value)
                    || value < 0D
                    || value > 1D)
                {
                    error = $"Coordinate '{tokens[index]}' must be a normalized value between 0 and 1.";
                    return false;
                }
            }

            if (!expectsSegment && (ParseDouble(tokens[3]) <= 0D || ParseDouble(tokens[4]) <= 0D))
            {
                error = "ObjectDetection width and height must be greater than zero.";
                return false;
            }

            className = classNames[classIndex];
            return true;
        }

        private static double ParseDouble(string value)
            => double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);

        private static void ValidateSplitPathSeparation(
            IReadOnlyCollection<string> trainPaths,
            IReadOnlyCollection<string> validPaths,
            IReadOnlyCollection<string> testPaths,
            List<string> errors)
        {
            var train = new HashSet<string>(trainPaths ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var valid = new HashSet<string>(validPaths ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var test = new HashSet<string>(testPaths ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            if (train.Overlaps(valid) || train.Overlaps(test) || valid.Overlaps(test))
            {
                AddError(errors, "External YOLO train, val, and test image paths must not overlap.");
            }
        }

        private static string ResolveLabelsDirectory(string imageDirectory)
        {
            string normalized = imageDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string separator = Path.DirectorySeparatorChar.ToString();
            string marker = separator + "images" + separator;
            int markerIndex = normalized.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex >= 0)
            {
                return normalized.Substring(0, markerIndex) + separator + "labels" + normalized.Substring(markerIndex + marker.Length - 1);
            }

            if (string.Equals(Path.GetFileName(normalized), "images", StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(Path.GetDirectoryName(normalized) ?? string.Empty, "labels");
            }

            return string.Empty;
        }

        private static object ReadRaw(Dictionary<object, object> document, string key)
            => document.FirstOrDefault(entry => string.Equals(Convert.ToString(entry.Key, CultureInfo.InvariantCulture), key, StringComparison.OrdinalIgnoreCase)).Value;

        private static string ReadScalar(Dictionary<object, object> document, string key)
            => NormalizeYamlScalar(ReadRaw(document, key));

        private static string NormalizeYamlScalar(object value)
            => Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim().Trim('"', '\'') ?? string.Empty;

        private static bool TryParseIndex(object value, out int index)
            => int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out index);

        private static void AddError(List<string> errors, string error)
        {
            if (errors.Count >= 20 || string.IsNullOrWhiteSpace(error) || errors.Contains(error, StringComparer.Ordinal))
            {
                return;
            }

            errors.Add(error);
        }

        private static string BuildSourceFingerprint(
            string yamlPath,
            SplitScan train,
            SplitScan valid,
            SplitScan test,
            out int sourceFileCount,
            List<string> errors)
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(yamlPath))
            {
                paths.Add(yamlPath);
            }

            foreach (SplitScan scan in new[] { train, valid, test })
            {
                foreach (string path in scan?.SourceFilePaths ?? Enumerable.Empty<string>())
                {
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        paths.Add(path);
                    }
                }
            }

            sourceFileCount = paths.Count;
            try
            {
                using SHA256 aggregate = SHA256.Create();
                foreach (string path in paths.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
                {
                    string fileHash = ComputeFileSha256(path);
                    byte[] entry = Encoding.UTF8.GetBytes(path.Replace('\\', '/') + "\n" + fileHash + "\n");
                    aggregate.TransformBlock(entry, 0, entry.Length, entry, 0);
                }

                aggregate.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                return ToHex(aggregate.Hash);
            }
            catch (Exception ex)
            {
                sourceFileCount = 0;
                AddError(errors, $"External YOLO source identity could not be calculated: {ex.Message}");
                return string.Empty;
            }
        }

        private static string TryComputeFileSha256(string path)
        {
            try
            {
                return File.Exists(path) ? ComputeFileSha256(path) : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string ResolveExistingFilePath(string path, string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            try
            {
                if (File.Exists(path))
                {
                    return Path.GetFullPath(path);
                }

                if (!string.IsNullOrWhiteSpace(rootDirectory))
                {
                    string candidate = Path.Combine(rootDirectory, path);
                    if (File.Exists(candidate))
                    {
                        return Path.GetFullPath(candidate);
                    }
                }
            }
            catch
            {
                // Provenance recording must not prevent a training request from being sent.
            }

            return string.Empty;
        }

        private static string ComputeFileSha256(string path)
        {
            using FileStream stream = File.OpenRead(path);
            using SHA256 sha256 = SHA256.Create();
            return ToHex(sha256.ComputeHash(stream));
        }

        private static string ToHex(byte[] bytes)
            => BitConverter.ToString(bytes ?? Array.Empty<byte>()).Replace("-", string.Empty);

        private sealed class SplitScan
        {
            public SplitScan(string name, string imageDirectoryPath)
            {
                Name = name;
                ImageDirectoryPath = imageDirectoryPath ?? string.Empty;
            }

            public string Name { get; }

            public string ImageDirectoryPath { get; }

            public List<string> ImagePaths { get; } = new List<string>();

            public List<string> SourceFilePaths { get; } = new List<string>();

            public int LabelFileCount { get; set; }

            public int EmptyLabelFileCount { get; set; }

            public int AnnotationCount { get; set; }

            public YoloExternalDatasetSplitSummary ToSummary()
                => new YoloExternalDatasetSplitSummary(Name, ImageDirectoryPath, ImagePaths.Count, LabelFileCount, EmptyLabelFileCount);
        }
    }
}
