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
            int sourceFileCount,
            bool requiresRuntimeMaterialization = false)
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
            RequiresRuntimeMaterialization = requiresRuntimeMaterialization;
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

        // A split-list packet with labels outside the native images/labels layout is read-only at intake.
        // Training receives an app-owned standard YOLO copy so the selected source remains untouched.
        public bool RequiresRuntimeMaterialization { get; }

        public bool IsReady => Errors.Count == 0;

        public int TotalImageCount => Train.ImageCount + Valid.ImageCount + Test.ImageCount;

        public int TotalLabelFileCount => Train.LabelFileCount + Valid.LabelFileCount + Test.LabelFileCount;

        public int TotalAnnotationCount => AnnotationCountByClass.Values.Sum();

        public string Summary =>
            $"{YoloExternalDatasetIntakeService.FormatPurpose(Purpose)} / train {Train.ImageCount} / val {Valid.ImageCount} / test {Test.ImageCount} / labels {TotalLabelFileCount} / annotations {TotalAnnotationCount} / classes {ClassNames.Count} / source files {SourceFileCount}"
            + (RequiresRuntimeMaterialization ? " / app runtime copy required" : string.Empty);
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

    /// <summary>
    /// One validated native YOLO source image and its optional label file.  This remains
    /// read-only provenance; consumers must write any runtime derivative below an
    /// app-owned artifact directory.
    /// </summary>
    public sealed class YoloExternalDatasetSourceEntry
    {
        public YoloExternalDatasetSourceEntry(string split, string imagePath, string labelPath)
        {
            Split = split ?? string.Empty;
            ImagePath = imagePath ?? string.Empty;
            LabelPath = labelPath ?? string.Empty;
        }

        public string Split { get; }

        public string ImagePath { get; }

        public string LabelPath { get; }
    }

    public sealed class YoloExternalDatasetSourcePacket
    {
        public YoloExternalDatasetSourcePacket(
            YoloExternalDatasetIntakeReport report,
            IEnumerable<YoloExternalDatasetSourceEntry> entries)
        {
            Report = report;
            Entries = (entries ?? Array.Empty<YoloExternalDatasetSourceEntry>()).ToArray();
        }

        public YoloExternalDatasetIntakeReport Report { get; }

        public IReadOnlyList<YoloExternalDatasetSourceEntry> Entries { get; }

        public bool IsReady => Report?.IsReady == true;
    }

    public sealed class YoloExternalRuntimeDatasetResult
    {
        public YoloExternalRuntimeDatasetResult(
            YoloExternalDatasetIntakeReport sourceReport,
            string runtimeDataYamlFilePath,
            string runtimeRootPath,
            bool materialized,
            IEnumerable<string> errors)
        {
            SourceReport = sourceReport;
            RuntimeDataYamlFilePath = runtimeDataYamlFilePath ?? string.Empty;
            RuntimeRootPath = runtimeRootPath ?? string.Empty;
            Materialized = materialized;
            Errors = (errors ?? Array.Empty<string>())
                .Where(error => !string.IsNullOrWhiteSpace(error))
                .ToArray();
        }

        public YoloExternalDatasetIntakeReport SourceReport { get; }

        public string RuntimeDataYamlFilePath { get; }

        public string RuntimeRootPath { get; }

        public bool Materialized { get; }

        public IReadOnlyList<string> Errors { get; }

        public bool IsReady => SourceReport?.IsReady == true
            && !string.IsNullOrWhiteSpace(RuntimeDataYamlFilePath)
            && Errors.Count == 0;
    }

    // Validates a native Ultralytics detection/segmentation dataset without copying or modifying it.
    public static class YoloExternalDatasetIntakeService
    {
        private static readonly HashSet<string> ImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff"
        };

        public static YoloExternalDatasetIntakeReport Build(string dataYamlFilePath, LabelingDatasetPurpose purpose)
            => BuildPackage(dataYamlFilePath, purpose).Report;

        /// <summary>
        /// Returns the paths already validated by native YOLO intake.  The call never
        /// copies, edits, or otherwise materializes the selected data.yaml package.
        /// </summary>
        public static YoloExternalDatasetSourcePacket ReadValidatedSourcePacket(
            string dataYamlFilePath,
            LabelingDatasetPurpose purpose)
        {
            ExternalYoloDatasetIntakePackage package = BuildPackage(dataYamlFilePath, purpose);
            return new YoloExternalDatasetSourcePacket(
                package.Report,
                EnumerateSourceEntries(package));
        }

        public static YoloExternalRuntimeDatasetResult PrepareRuntimeDataset(
            string dataYamlFilePath,
            LabelingDatasetPurpose purpose,
            string runtimeParentPath)
        {
            ExternalYoloDatasetIntakePackage package = BuildPackage(dataYamlFilePath, purpose);
            YoloExternalDatasetIntakeReport report = package.Report;
            if (!report.IsReady)
            {
                return new YoloExternalRuntimeDatasetResult(
                    report,
                    string.Empty,
                    string.Empty,
                    materialized: false,
                    report.Errors);
            }

            // Even a conventional images/labels source must not be passed directly to
            // a training runtime: Ultralytics can create cache files beside labels.
            // All external training therefore receives an app-owned copy.
            return MaterializeRuntimeDataset(package, runtimeParentPath);
        }

        private static ExternalYoloDatasetIntakePackage BuildPackage(string dataYamlFilePath, LabelingDatasetPurpose purpose)
        {
            var errors = new List<string>();
            if (!IsSupportedPurpose(purpose))
            {
                errors.Add("External native YOLO intake supports ObjectDetection or Segmentation only.");
                return EmptyPackage(dataYamlFilePath, purpose, errors);
            }

            string yamlPath = NormalizeYamlPath(dataYamlFilePath, errors);
            if (errors.Count > 0)
            {
                return EmptyPackage(yamlPath, purpose, errors);
            }

            Dictionary<object, object> document = ReadYaml(yamlPath, errors);
            if (document == null)
            {
                return EmptyPackage(yamlPath, purpose, errors);
            }

            string datasetRoot = ResolveDatasetRoot(yamlPath, ReadScalar(document, "path"), errors);
            List<string> classNames = ReadClassNames(document, errors);
            ValidateClassCount(document, classNames, errors);

            SplitInput trainInput = ResolveSplitInput(yamlPath, datasetRoot, ReadScalar(document, "train"), "train", required: true, errors);
            SplitInput validInput = ResolveSplitInput(yamlPath, datasetRoot, ReadScalar(document, "val"), "val", required: true, errors);
            SplitInput testInput = ResolveSplitInput(yamlPath, datasetRoot, ReadScalar(document, "test"), "test", required: false, errors);
            bool requiresRuntimeMaterialization = trainInput.IsImageList || validInput.IsImageList || testInput.IsImageList;
            string listLabelsRoot = requiresRuntimeMaterialization
                ? ResolveListLabelsRoot(yamlPath, datasetRoot, purpose, errors)
                : string.Empty;

            var annotationCountByClass = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            SplitScan train = ScanSplit("train", trainInput, required: true, purpose, classNames, listLabelsRoot, annotationCountByClass, errors);
            SplitScan valid = ScanSplit("val", validInput, required: true, purpose, classNames, listLabelsRoot, annotationCountByClass, errors);
            SplitScan test = ScanSplit("test", testInput, required: false, purpose, classNames, listLabelsRoot, annotationCountByClass, errors);
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

            var report = new YoloExternalDatasetIntakeReport(
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
                sourceFileCount,
                requiresRuntimeMaterialization);
            return new ExternalYoloDatasetIntakePackage(report, train, valid, test);
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
            settings.LastValidationClassNames = report.IsReady
                ? string.Join(", ", report.ClassNames)
                : string.Empty;
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
            string sourceFingerprintSha256,
            string runtimeDataYamlFilePath = "")
        {
            if (settings == null)
            {
                return;
            }

            settings.LastTrainingUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            settings.LastTrainingSourceFingerprintSha256 = sourceFingerprintSha256 ?? string.Empty;
            settings.LastTrainingDataYamlFilePath = settings.DataYamlFilePath ?? string.Empty;
            settings.LastTrainingRuntimeDataYamlFilePath = runtimeDataYamlFilePath ?? string.Empty;
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

        private static ExternalYoloDatasetIntakePackage EmptyPackage(string dataYamlFilePath, LabelingDatasetPurpose purpose, IEnumerable<string> errors)
        {
            var report = new YoloExternalDatasetIntakeReport(
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
            return new ExternalYoloDatasetIntakePackage(report, new SplitScan("train", string.Empty), new SplitScan("val", string.Empty), new SplitScan("test", string.Empty));
        }

        private static IEnumerable<YoloExternalDatasetSourceEntry> EnumerateSourceEntries(ExternalYoloDatasetIntakePackage package)
        {
            foreach ((SplitScan scan, string split) in new[]
            {
                (package?.Train, "train"),
                (package?.Valid, "val"),
                (package?.Test, "test")
            })
            {
                foreach (SplitItem item in scan?.Items ?? Enumerable.Empty<SplitItem>())
                {
                    yield return new YoloExternalDatasetSourceEntry(split, item.ImagePath, item.LabelPath);
                }
            }
        }

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

        private static SplitInput ResolveSplitInput(
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

                return new SplitInput(key, string.Empty, isImageList: false, datasetRoot);
            }

            string candidate = Path.IsPathRooted(splitText)
                ? splitText
                : Path.Combine(string.IsNullOrWhiteSpace(datasetRoot) ? Path.GetDirectoryName(yamlPath) ?? string.Empty : datasetRoot, splitText);
            try
            {
                string fullPath = Path.GetFullPath(candidate);
                if (Directory.Exists(fullPath))
                {
                    return new SplitInput(key, fullPath, isImageList: false, datasetRoot);
                }

                if (File.Exists(fullPath)
                    && string.Equals(Path.GetExtension(fullPath), ".txt", StringComparison.OrdinalIgnoreCase))
                {
                    return new SplitInput(key, fullPath, isImageList: true, datasetRoot);
                }

                errors.Add($"External YOLO {key} path must be an image directory or .txt image list: {fullPath}");
                return new SplitInput(key, fullPath, isImageList: false, datasetRoot);
            }
            catch (Exception ex)
            {
                errors.Add($"External YOLO {key} path is invalid: {ex.Message}");
                return new SplitInput(key, string.Empty, isImageList: false, datasetRoot);
            }
        }

        private static string ResolveListLabelsRoot(
            string yamlPath,
            string datasetRoot,
            LabelingDatasetPurpose purpose,
            List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(datasetRoot))
            {
                return string.Empty;
            }

            string labelsRoot = Path.Combine(datasetRoot, "labels");
            if (!Directory.Exists(labelsRoot))
            {
                AddError(errors, $"External YOLO split-list packet requires a labels directory: {labelsRoot}");
                return string.Empty;
            }

            string semanticToken = Path.GetFileNameWithoutExtension(yamlPath)
                .Split(new[] { '-', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(token => !string.Equals(token, "dataset", StringComparison.OrdinalIgnoreCase))
                ?? string.Empty;
            string taskToken = purpose == LabelingDatasetPurpose.Segmentation ? "segmentation" : "detection";
            string[] candidates = Directory.EnumerateDirectories(labelsRoot, "*", SearchOption.TopDirectoryOnly)
                .Where(path => !string.IsNullOrWhiteSpace(semanticToken)
                    && Path.GetFileName(path).Contains(semanticToken, StringComparison.OrdinalIgnoreCase)
                    && Path.GetFileName(path).Contains(taskToken, StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (candidates.Length == 1)
            {
                return candidates[0];
            }

            AddError(
                errors,
                candidates.Length == 0
                    ? $"External YOLO split-list packet could not match '{Path.GetFileName(yamlPath)}' to one labels subfolder under {labelsRoot}."
                    : $"External YOLO split-list packet has ambiguous label roots for '{Path.GetFileName(yamlPath)}': {string.Join(", ", candidates.Select(Path.GetFileName))}.");
            return string.Empty;
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
            SplitInput input,
            bool required,
            LabelingDatasetPurpose purpose,
            IReadOnlyList<string> classNames,
            string listLabelsRoot,
            Dictionary<string, int> annotationCountByClass,
            List<string> errors)
        {
            string displayPath = input?.Path ?? string.Empty;
            var scan = new SplitScan(name, displayPath);
            if (input == null || string.IsNullOrWhiteSpace(input.Path))
            {
                return scan;
            }

            string labelsDirectory = input.IsImageList
                ? listLabelsRoot
                : ResolveLabelsDirectory(input.Path);
            if (string.IsNullOrWhiteSpace(labelsDirectory))
            {
                AddError(errors, input.IsImageList
                    ? $"External YOLO {name} image list requires one unambiguous labels subfolder."
                    : $"External YOLO {name} image directory must be under an images folder so labels can resolve: {input.Path}");
                return scan;
            }

            string[] imagePaths;
            try
            {
                imagePaths = input.IsImageList
                    ? ReadImageList(input.Path, input.DatasetRootPath, name, errors)
                    : Directory.EnumerateFiles(input.Path, "*", SearchOption.AllDirectories)
                        .Where(path => ImageExtensions.Contains(Path.GetExtension(path)))
                        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
            }
            catch (Exception ex)
            {
                AddError(errors, $"External YOLO {name} image {(input.IsImageList ? "list" : "directory")} could not be read: {ex.Message}");
                return scan;
            }

            scan.ImagePaths.AddRange(imagePaths);
            if (input.IsImageList)
            {
                scan.SourceFilePaths.Add(input.Path);
            }
            if (required && imagePaths.Length == 0)
            {
                AddError(errors, $"External YOLO {name} image {(input.IsImageList ? "list" : "directory")} contains no supported images: {input.Path}");
            }

            foreach (string imagePath in imagePaths)
            {
                scan.SourceFilePaths.Add(imagePath);
                string labelPath = input.IsImageList
                    ? ResolveListLabelPath(imagePath, labelsDirectory)
                    : Path.ChangeExtension(Path.Combine(labelsDirectory, Path.GetRelativePath(input.Path, imagePath)), ".txt");
                scan.Items.Add(new SplitItem(imagePath, labelPath));
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

        private static string[] ReadImageList(string listPath, string datasetRoot, string splitName, List<string> errors)
        {
            var imagePaths = new List<string>();
            foreach (string line in File.ReadAllLines(listPath))
            {
                string imageText = (line ?? string.Empty).Trim().Trim('"');
                if (imageText.Length == 0 || imageText.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                string candidate = Path.IsPathRooted(imageText)
                    ? imageText
                    : Path.Combine(datasetRoot, imageText);
                string imagePath;
                try
                {
                    imagePath = Path.GetFullPath(candidate);
                }
                catch (Exception ex)
                {
                    AddError(errors, $"External YOLO {splitName} image list path is invalid: {imageText}. {ex.Message}");
                    continue;
                }

                if (!ImageExtensions.Contains(Path.GetExtension(imagePath)))
                {
                    AddError(errors, $"External YOLO {splitName} image list contains an unsupported image extension: {imageText}");
                    continue;
                }

                if (!File.Exists(imagePath))
                {
                    AddError(errors, $"External YOLO {splitName} image list image was not found: {imagePath}");
                    continue;
                }

                imagePaths.Add(imagePath);
            }

            return imagePaths
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string ResolveListLabelPath(string imagePath, string labelsRoot)
        {
            string imagesRoot = FindImagesRoot(imagePath);
            if (string.IsNullOrWhiteSpace(imagesRoot))
            {
                return string.Empty;
            }

            string relativePath = Path.GetRelativePath(imagesRoot, imagePath);
            return Path.ChangeExtension(Path.Combine(labelsRoot, relativePath), ".txt");
        }

        private static string FindImagesRoot(string imagePath)
        {
            DirectoryInfo current = new FileInfo(imagePath).Directory;
            while (current != null)
            {
                if (string.Equals(current.Name, "images", StringComparison.OrdinalIgnoreCase))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            return string.Empty;
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

        private static YoloExternalRuntimeDatasetResult MaterializeRuntimeDataset(
            ExternalYoloDatasetIntakePackage package,
            string runtimeParentPath)
        {
            YoloExternalDatasetIntakeReport report = package.Report;
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(runtimeParentPath))
            {
                errors.Add("External YOLO runtime copy requires a recipe output folder.");
                return new YoloExternalRuntimeDatasetResult(report, string.Empty, string.Empty, materialized: false, errors);
            }

            string runtimeRoot;
            string temporaryRoot = string.Empty;
            try
            {
                string parent = Path.GetFullPath(runtimeParentPath);
                string fingerprint = report.SourceFingerprintSha256.Length >= 16
                    ? report.SourceFingerprintSha256.Substring(0, 16)
                    : report.SourceFingerprintSha256;
                runtimeRoot = Path.Combine(parent, "external-yolo-runtime-" + fingerprint);
                string runtimeYamlPath = Path.Combine(runtimeRoot, "data.yaml");
                if (File.Exists(runtimeYamlPath))
                {
                    YoloExternalDatasetIntakeReport existing = Build(runtimeYamlPath, report.Purpose);
                    if (existing.IsReady)
                    {
                        return new YoloExternalRuntimeDatasetResult(report, runtimeYamlPath, runtimeRoot, materialized: true, Array.Empty<string>());
                    }

                    errors.Add($"Existing app-owned external YOLO runtime copy is invalid: {runtimeRoot}. Remove that app-owned folder and start training again.");
                    return new YoloExternalRuntimeDatasetResult(report, string.Empty, runtimeRoot, materialized: false, errors);
                }

                Directory.CreateDirectory(parent);
                temporaryRoot = runtimeRoot + ".partial-" + Guid.NewGuid().ToString("N");
                Directory.CreateDirectory(temporaryRoot);
                CopyRuntimeSplit(package.Train, "train", temporaryRoot);
                CopyRuntimeSplit(package.Valid, "val", temporaryRoot);
                CopyRuntimeSplit(package.Test, "test", temporaryRoot);
                File.WriteAllLines(Path.Combine(temporaryRoot, "data.yaml"), BuildRuntimeDataYaml(report));

                YoloExternalDatasetIntakeReport runtimeReport = Build(Path.Combine(temporaryRoot, "data.yaml"), report.Purpose);
                if (!runtimeReport.IsReady)
                {
                    errors.AddRange(runtimeReport.Errors);
                    return new YoloExternalRuntimeDatasetResult(report, string.Empty, temporaryRoot, materialized: false, errors);
                }

                Directory.Move(temporaryRoot, runtimeRoot);
                temporaryRoot = string.Empty;
                return new YoloExternalRuntimeDatasetResult(report, runtimeYamlPath, runtimeRoot, materialized: true, Array.Empty<string>());
            }
            catch (Exception ex)
            {
                errors.Add($"External YOLO runtime copy could not be prepared: {ex.Message}");
                return new YoloExternalRuntimeDatasetResult(report, string.Empty, string.Empty, materialized: false, errors);
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(temporaryRoot) && Directory.Exists(temporaryRoot))
                {
                    Directory.Delete(temporaryRoot, recursive: true);
                }
            }
        }

        private static void CopyRuntimeSplit(SplitScan scan, string splitName, string runtimeRoot)
        {
            foreach (SplitItem item in scan?.Items ?? Enumerable.Empty<SplitItem>())
            {
                string imagesRoot = FindImagesRoot(item.ImagePath);
                if (string.IsNullOrWhiteSpace(imagesRoot))
                {
                    throw new InvalidOperationException($"External YOLO runtime copy requires images paths under an images folder: {item.ImagePath}");
                }

                string relativeImagePath = Path.GetRelativePath(imagesRoot, item.ImagePath);
                string runtimeImagePath = Path.Combine(runtimeRoot, "images", splitName, relativeImagePath);
                string runtimeLabelPath = Path.ChangeExtension(Path.Combine(runtimeRoot, "labels", splitName, relativeImagePath), ".txt");
                Directory.CreateDirectory(Path.GetDirectoryName(runtimeImagePath));
                Directory.CreateDirectory(Path.GetDirectoryName(runtimeLabelPath));
                File.Copy(item.ImagePath, runtimeImagePath, overwrite: true);
                if (File.Exists(item.LabelPath))
                {
                    File.Copy(item.LabelPath, runtimeLabelPath, overwrite: true);
                }
                else
                {
                    File.WriteAllText(runtimeLabelPath, string.Empty);
                }
            }
        }

        private static IReadOnlyList<string> BuildRuntimeDataYaml(YoloExternalDatasetIntakeReport report)
        {
            var lines = new List<string>
            {
                "path: .",
                "train: images/train",
                "val: images/val"
            };
            if (report.Test.ImageCount > 0)
            {
                lines.Add("test: images/test");
            }

            lines.Add("nc: " + report.ClassNames.Count.ToString(CultureInfo.InvariantCulture));
            lines.Add("names:");
            for (int index = 0; index < report.ClassNames.Count; index++)
            {
                string name = report.ClassNames[index]
                    .Replace("\\", "\\\\")
                    .Replace("\"", "\\\"");
                lines.Add($"  {index}: \"{name}\"");
            }

            return lines;
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

            public List<SplitItem> Items { get; } = new List<SplitItem>();

            public int LabelFileCount { get; set; }

            public int EmptyLabelFileCount { get; set; }

            public int AnnotationCount { get; set; }

            public YoloExternalDatasetSplitSummary ToSummary()
                => new YoloExternalDatasetSplitSummary(Name, ImageDirectoryPath, ImagePaths.Count, LabelFileCount, EmptyLabelFileCount);
        }

        private sealed class SplitInput
        {
            public SplitInput(string name, string path, bool isImageList, string datasetRootPath)
            {
                Name = name ?? string.Empty;
                Path = path ?? string.Empty;
                IsImageList = isImageList;
                DatasetRootPath = datasetRootPath ?? string.Empty;
            }

            public string Name { get; }

            public string Path { get; }

            public bool IsImageList { get; }

            public string DatasetRootPath { get; }
        }

        private sealed class SplitItem
        {
            public SplitItem(string imagePath, string labelPath)
            {
                ImagePath = imagePath ?? string.Empty;
                LabelPath = labelPath ?? string.Empty;
            }

            public string ImagePath { get; }

            public string LabelPath { get; }
        }

        private sealed class ExternalYoloDatasetIntakePackage
        {
            public ExternalYoloDatasetIntakePackage(
                YoloExternalDatasetIntakeReport report,
                SplitScan train,
                SplitScan valid,
                SplitScan test)
            {
                Report = report;
                Train = train;
                Valid = valid;
                Test = test;
            }

            public YoloExternalDatasetIntakeReport Report { get; }

            public SplitScan Train { get; }

            public SplitScan Valid { get; }

            public SplitScan Test { get; }
        }
    }
}
