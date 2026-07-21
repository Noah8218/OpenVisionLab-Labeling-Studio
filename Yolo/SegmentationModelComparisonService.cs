using MvcVisionSystem._1._Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace MvcVisionSystem.Yolo
{
    /// <summary>
    /// Exports a selected segmentation adapter's raw raster predictions without touching the recipe or its U-Net export.
    /// </summary>
    public sealed class SegmentationPredictionExportRequest
    {
        public string AdapterKey { get; set; } = string.Empty;

        public string Engine { get; set; } = string.Empty;

        public string PythonExecutablePath { get; set; } = string.Empty;

        public string ScriptPath { get; set; } = string.Empty;

        public string WeightsPath { get; set; } = string.Empty;

        public string DatasetExportRootPath { get; set; } = string.Empty;

        public string Split { get; set; } = "test";

        public string OutputRootPath { get; set; } = string.Empty;

        public int ImageSize { get; set; } = 320;

        public double Confidence { get; set; } = 0.25D;

        public string Device { get; set; } = "cpu";
    }

    public sealed class SegmentationPredictionExportResult
    {
        public bool Succeeded { get; internal set; }

        public string PredictionManifestPath { get; internal set; } = string.Empty;

        public string Output { get; internal set; } = string.Empty;

        public string Error { get; internal set; } = string.Empty;
    }

    public static class SegmentationPredictionExportService
    {
        public const string AdapterUnet = "unet";
        public const string AdapterUltralytics = "ultralytics";

        public static SegmentationPredictionExportRequest BuildRequest(
            string adapterKey,
            PythonModelSettings settings,
            string datasetExportRootPath,
            string outputRootPath,
            string split = "test")
        {
            settings ??= new PythonModelSettings();
            string normalizedAdapter = NormalizeAdapterKey(adapterKey);
            return new SegmentationPredictionExportRequest
            {
                AdapterKey = normalizedAdapter,
                Engine = PythonModelSettings.NormalizeModelEngine(settings.ModelEngine),
                PythonExecutablePath = PythonModelSettingsValidator.ResolvePythonExecutable(settings),
                ScriptPath = PythonModelRuntimeBundledWorkerService.ResolveSegmentationPredictionExporterScriptPath(),
                WeightsPath = settings.WeightsPath?.Trim() ?? string.Empty,
                DatasetExportRootPath = datasetExportRootPath?.Trim() ?? string.Empty,
                OutputRootPath = outputRootPath?.Trim() ?? string.Empty,
                Split = NormalizeSplit(split),
                ImageSize = Math.Max(1, settings.InferenceImageSize),
                Confidence = Math.Clamp(settings.MinimumDetectionConfidence, 0.0F, 1.0F),
                Device = "cpu"
            };
        }

        public static IReadOnlyList<string> ValidateRequest(SegmentationPredictionExportRequest request)
        {
            var errors = new List<string>();
            if (request == null)
            {
                errors.Add("Segmentation prediction export request is missing.");
                return errors;
            }

            string adapter = NormalizeAdapterKey(request.AdapterKey);
            if (string.IsNullOrWhiteSpace(adapter))
            {
                errors.Add("Segmentation prediction export supports only U-Net or Ultralytics segmentation adapters.");
            }
            ValidateFile(request.PythonExecutablePath, "Segmentation adapter Python", errors);
            ValidateFile(request.ScriptPath, "Segmentation prediction exporter", errors);
            ValidateFile(request.WeightsPath, "Segmentation checkpoint", errors);
            ValidateDirectory(request.DatasetExportRootPath, "Canonical segmentation export", errors);
            if (!File.Exists(Path.Combine(request.DatasetExportRootPath ?? string.Empty, "dataset-manifest.json")))
            {
                errors.Add("Canonical segmentation export is missing dataset-manifest.json.");
            }
            if (!File.Exists(Path.Combine(request.DatasetExportRootPath ?? string.Empty, "classes.json")))
            {
                errors.Add("Canonical segmentation export is missing classes.json.");
            }
            if (string.IsNullOrWhiteSpace(request.OutputRootPath))
            {
                errors.Add("Segmentation prediction output directory is missing.");
            }
            else if (Directory.Exists(request.OutputRootPath)
                && Directory.EnumerateFileSystemEntries(request.OutputRootPath).Any())
            {
                errors.Add("Segmentation prediction output directory must be new or empty.");
            }
            if (request.ImageSize <= 0)
            {
                errors.Add("Segmentation prediction image size must be greater than zero.");
            }
            if (request.Confidence < 0.0D || request.Confidence > 1.0D)
            {
                errors.Add("Segmentation prediction confidence must be between zero and one.");
            }
            return errors;
        }

        public static ProcessStartInfo CreateStartInfo(SegmentationPredictionExportRequest request)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = request?.PythonExecutablePath ?? string.Empty,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(request?.ScriptPath ?? string.Empty) ?? Environment.CurrentDirectory
            };
            startInfo.ArgumentList.Add(request?.ScriptPath ?? string.Empty);
            startInfo.ArgumentList.Add("--adapter");
            startInfo.ArgumentList.Add(NormalizeAdapterKey(request?.AdapterKey));
            AddArgument(startInfo, "--engine", request?.Engine);
            AddArgument(startInfo, "--data-root", request?.DatasetExportRootPath);
            AddArgument(startInfo, "--weights", request?.WeightsPath);
            AddArgument(startInfo, "--split", NormalizeSplit(request?.Split));
            AddArgument(startInfo, "--output-root", request?.OutputRootPath);
            AddArgument(startInfo, "--image-size", Math.Max(1, request?.ImageSize ?? 1).ToString(CultureInfo.InvariantCulture));
            AddArgument(startInfo, "--confidence", Math.Clamp(request?.Confidence ?? 0.25D, 0.0D, 1.0D).ToString(CultureInfo.InvariantCulture));
            AddArgument(startInfo, "--device", request?.Device);
            return startInfo;
        }

        public static SegmentationPredictionExportResult Run(SegmentationPredictionExportRequest request)
        {
            IReadOnlyList<string> errors = ValidateRequest(request);
            if (errors.Count > 0)
            {
                return new SegmentationPredictionExportResult { Error = string.Join(Environment.NewLine, errors) };
            }

            using var process = new Process { StartInfo = CreateStartInfo(request) };
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            string manifestPath = ParseManifestPath(output);
            bool succeeded = process.ExitCode == 0 && File.Exists(manifestPath);
            return new SegmentationPredictionExportResult
            {
                Succeeded = succeeded,
                PredictionManifestPath = succeeded ? manifestPath : string.Empty,
                Output = output,
                Error = succeeded
                    ? error
                    : string.IsNullOrWhiteSpace(error)
                        ? string.IsNullOrWhiteSpace(output) ? "Segmentation prediction export failed without process output." : output.Trim()
                        : error.Trim()
            };
        }

        public static string NormalizeAdapterKey(string value)
        {
            string normalized = (value ?? string.Empty).Trim().ToLowerInvariant().Replace("-", string.Empty);
            return normalized switch
            {
                "unet" => AdapterUnet,
                "ultralytics" or "yolov8" or "yolo11" => AdapterUltralytics,
                _ => string.Empty
            };
        }

        private static string NormalizeSplit(string value)
        {
            return string.Equals(value, "train", StringComparison.OrdinalIgnoreCase)
                ? "train"
                : string.Equals(value, "valid", StringComparison.OrdinalIgnoreCase)
                    ? "valid"
                    : "test";
        }

        private static void AddArgument(ProcessStartInfo startInfo, string name, string value)
        {
            startInfo.ArgumentList.Add(name);
            startInfo.ArgumentList.Add(value ?? string.Empty);
        }

        private static string ParseManifestPath(string output)
        {
            const string prefix = "OPENVISIONLAB_SEGMENTATION_PREDICTION_MANIFEST=";
            string line = (output ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault(value => value.StartsWith(prefix, StringComparison.Ordinal));
            return string.IsNullOrWhiteSpace(line) ? string.Empty : line.Substring(prefix.Length).Trim();
        }

        private static void ValidateFile(string path, string label, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                errors.Add(label + " not found: " + (path ?? string.Empty));
            }
        }

        private static void ValidateDirectory(string path, string label, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                errors.Add(label + " not found: " + (path ?? string.Empty));
            }
        }
    }

    public sealed class SegmentationMaskComparisonRequest
    {
        public string DatasetExportRootPath { get; set; } = string.Empty;

        public string BaselinePredictionManifestPath { get; set; } = string.Empty;

        public string CandidatePredictionManifestPath { get; set; } = string.Empty;

        public string Split { get; set; } = "test";

        public string OutputRootPath { get; set; } = string.Empty;

        public double ComponentIouThreshold { get; set; } = 0.5D;
    }

    public sealed class SegmentationMaskComparisonResult
    {
        public List<string> Errors { get; } = new List<string>();

        public string ReportPath { get; internal set; } = string.Empty;

        public string DatasetFingerprint { get; internal set; } = string.Empty;

        public string SourceDataTreeSha256 { get; internal set; } = string.Empty;

        public string ClassContractSha256 { get; internal set; } = string.Empty;

        public string Split { get; internal set; } = string.Empty;

        public SegmentationPredictionRunSummary Baseline { get; internal set; } = new SegmentationPredictionRunSummary();

        public SegmentationPredictionRunSummary Candidate { get; internal set; } = new SegmentationPredictionRunSummary();

        public List<SegmentationMaskComparisonClassResult> Classes { get; } = new List<SegmentationMaskComparisonClassResult>();

        public bool IsReady => Errors.Count == 0 && Classes.Count > 0;
    }

    public sealed class SegmentationPredictionRunSummary
    {
        public string AdapterKey { get; set; } = string.Empty;

        public string Engine { get; set; } = string.Empty;

        public string CheckpointPath { get; set; } = string.Empty;

        public string CheckpointSha256 { get; set; } = string.Empty;

        public int ImageCount { get; set; }

        public double MeanDice { get; set; }

        public double MeanIoU { get; set; }
    }

    public sealed class SegmentationMaskComparisonClassResult
    {
        public int ClassIndex { get; set; }

        public string ClassName { get; set; } = string.Empty;

        public SegmentationMaskMetrics Baseline { get; set; } = new SegmentationMaskMetrics();

        public SegmentationMaskMetrics Candidate { get; set; } = new SegmentationMaskMetrics();
    }

    public sealed class SegmentationMaskMetrics
    {
        public long TruePositivePixels { get; set; }

        public long FalsePositivePixels { get; set; }

        public long FalseNegativePixels { get; set; }

        public int TruePositiveComponents { get; set; }

        public int FalsePositiveComponents { get; set; }

        public int FalseNegativeComponents { get; set; }

        public double Dice { get; set; }

        public double IoU { get; set; }
    }

    public sealed class SegmentationPredictionManifestRecord
    {
        public int Version { get; set; }

        public string AdapterKey { get; set; } = string.Empty;

        public string Engine { get; set; } = string.Empty;

        public string DatasetFingerprint { get; set; } = string.Empty;

        public string SourceDataTreeSha256 { get; set; } = string.Empty;

        public string ClassContractSha256 { get; set; } = string.Empty;

        public string Split { get; set; } = string.Empty;

        public string CheckpointSha256 { get; set; } = string.Empty;

        public string CheckpointPath { get; set; } = string.Empty;

        public string ImageSha256 { get; set; } = string.Empty;

        public int ImageWidth { get; set; }

        public int ImageHeight { get; set; }

        public string PredictionMaskRelativePath { get; set; } = string.Empty;

        public string PredictionMaskSha256 { get; set; } = string.Empty;
    }

    /// <summary>
    /// Scores only compatible adapter artifacts against the canonical raster masks.
    /// A YOLO mAP report and a U-Net mask report are deliberately not mixed here.
    /// </summary>
    public static class SegmentationMaskComparisonService
    {
        public static SegmentationMaskComparisonResult Evaluate(SegmentationMaskComparisonRequest request)
        {
            var result = new SegmentationMaskComparisonResult();
            if (request == null)
            {
                result.Errors.Add("Segmentation mask comparison request is missing.");
                return result;
            }
            if (request.ComponentIouThreshold <= 0.0D || request.ComponentIouThreshold > 1.0D)
            {
                result.Errors.Add("Segmentation component IoU threshold must be greater than zero and at most one.");
                return result;
            }

            string datasetRoot = ResolveExistingDirectory(request.DatasetExportRootPath, "Canonical segmentation export", result.Errors);
            if (string.IsNullOrWhiteSpace(datasetRoot))
            {
                return result;
            }
            string datasetManifestPath = Path.Combine(datasetRoot, "dataset-manifest.json");
            if (!File.Exists(datasetManifestPath))
            {
                result.Errors.Add("Canonical segmentation export is missing dataset-manifest.json.");
                return result;
            }

            UnetSegmentationDatasetExportManifest datasetManifest;
            try
            {
                datasetManifest = JsonConvert.DeserializeObject<UnetSegmentationDatasetExportManifest>(File.ReadAllText(datasetManifestPath));
            }
            catch (Exception ex) when (ex is IOException || ex is JsonException || ex is UnauthorizedAccessException)
            {
                result.Errors.Add("Canonical segmentation manifest cannot be read: " + ex.Message);
                return result;
            }
            if (!TryValidateDatasetManifest(datasetManifest, request.Split, out List<UnetSegmentationDatasetExportManifestImage> expectedImages, out string split, result.Errors))
            {
                return result;
            }

            result.DatasetFingerprint = datasetManifest.DatasetFingerprint;
            result.SourceDataTreeSha256 = datasetManifest.SourceDataTreeSha256;
            result.ClassContractSha256 = datasetManifest.ClassContractSha256;
            result.Split = split;
            var expectedByImageSha = expectedImages.ToDictionary(item => item.ImageSha256, StringComparer.OrdinalIgnoreCase);
            ValidatedPredictionRun baseline = LoadPredictionRun(
                request.BaselinePredictionManifestPath,
                "Baseline",
                datasetManifest,
                split,
                expectedByImageSha,
                result.Errors);
            ValidatedPredictionRun candidate = LoadPredictionRun(
                request.CandidatePredictionManifestPath,
                "Candidate",
                datasetManifest,
                split,
                expectedByImageSha,
                result.Errors);
            if (result.Errors.Count > 0)
            {
                return result;
            }

            result.Baseline = baseline.Summary;
            result.Candidate = candidate.Summary;
            var baselineMetrics = datasetManifest.Classes.Select((item, index) => new MetricAccumulator(index + 1)).ToArray();
            var candidateMetrics = datasetManifest.Classes.Select((item, index) => new MetricAccumulator(index + 1)).ToArray();
            foreach (UnetSegmentationDatasetExportManifestImage expected in expectedImages.OrderBy(item => item.SourceRelativeImagePath, StringComparer.OrdinalIgnoreCase))
            {
                string groundTruthPath = ResolveContainedPath(datasetRoot, expected.ExportMaskRelativePath, "ground-truth mask", result.Errors);
                if (result.Errors.Count > 0)
                {
                    return result;
                }
                if (!TryReadMaskValues(groundTruthPath, expected.ImageWidth, expected.ImageHeight, datasetManifest.Classes.Count, out byte[] groundTruth, out string groundTruthError))
                {
                    result.Errors.Add("Canonical ground-truth mask is invalid: " + groundTruthError);
                    return result;
                }
                EvaluatePredictionMask(
                    groundTruth,
                    ReadPredictionMask(baseline, expected, datasetManifest.Classes.Count, result.Errors),
                    expected.ImageWidth,
                    expected.ImageHeight,
                    baselineMetrics,
                    request.ComponentIouThreshold);
                EvaluatePredictionMask(
                    groundTruth,
                    ReadPredictionMask(candidate, expected, datasetManifest.Classes.Count, result.Errors),
                    expected.ImageWidth,
                    expected.ImageHeight,
                    candidateMetrics,
                    request.ComponentIouThreshold);
                if (result.Errors.Count > 0)
                {
                    return result;
                }
            }

            for (int index = 0; index < datasetManifest.Classes.Count; index++)
            {
                result.Classes.Add(new SegmentationMaskComparisonClassResult
                {
                    ClassIndex = index + 1,
                    ClassName = datasetManifest.Classes[index].Name,
                    Baseline = baselineMetrics[index].ToMetrics(),
                    Candidate = candidateMetrics[index].ToMetrics()
                });
            }
            result.Baseline.MeanDice = result.Classes.Average(item => item.Baseline.Dice);
            result.Baseline.MeanIoU = result.Classes.Average(item => item.Baseline.IoU);
            result.Candidate.MeanDice = result.Classes.Average(item => item.Candidate.Dice);
            result.Candidate.MeanIoU = result.Classes.Average(item => item.Candidate.IoU);
            WriteReport(request.OutputRootPath, result);
            return result;
        }

        private static bool TryValidateDatasetManifest(
            UnetSegmentationDatasetExportManifest manifest,
            string requestedSplit,
            out List<UnetSegmentationDatasetExportManifestImage> expectedImages,
            out string split,
            List<string> errors)
        {
            expectedImages = new List<UnetSegmentationDatasetExportManifestImage>();
            split = NormalizeSplit(requestedSplit);
            string selectedSplit = split;
            if (manifest == null || manifest.Version != 1)
            {
                errors.Add("Canonical segmentation manifest version is unsupported.");
                return false;
            }
            if (string.IsNullOrWhiteSpace(manifest.DatasetFingerprint)
                || string.IsNullOrWhiteSpace(manifest.SourceDataTreeSha256)
                || string.IsNullOrWhiteSpace(manifest.ClassContractSha256)
                || manifest.Classes == null
                || manifest.Classes.Count == 0)
            {
                errors.Add("Canonical segmentation manifest has an incomplete provenance/class contract.");
                return false;
            }
            for (int index = 0; index < manifest.Classes.Count; index++)
            {
                if (manifest.Classes[index] == null
                    || manifest.Classes[index].Index != index + 1
                    || string.IsNullOrWhiteSpace(manifest.Classes[index].Name))
                {
                    errors.Add("Canonical segmentation manifest class contract is invalid.");
                    return false;
                }
            }
            UnetSegmentationDatasetExportManifestSplit selected = manifest.Splits?
                .Where(item => item != null && string.Equals(item.Split, selectedSplit, StringComparison.OrdinalIgnoreCase))
                .SingleOrDefault();
            if (selected?.Images == null || selected.Images.Count == 0)
            {
                errors.Add("Canonical segmentation manifest has no test images.");
                return false;
            }
            expectedImages = selected.Images;
            if (expectedImages.Any(item => item == null
                || string.IsNullOrWhiteSpace(item.ImageSha256)
                || item.ImageWidth <= 0
                || item.ImageHeight <= 0
                || string.IsNullOrWhiteSpace(item.ExportMaskRelativePath)))
            {
                errors.Add("Canonical segmentation test image contract is incomplete.");
                return false;
            }
            if (expectedImages.GroupBy(item => item.ImageSha256, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
            {
                errors.Add("Canonical segmentation test split has duplicate image SHA-256 entries.");
                return false;
            }
            return true;
        }

        private static ValidatedPredictionRun LoadPredictionRun(
            string manifestPath,
            string label,
            UnetSegmentationDatasetExportManifest dataset,
            string split,
            IReadOnlyDictionary<string, UnetSegmentationDatasetExportManifestImage> expectedByImageSha,
            List<string> errors)
        {
            string fullManifestPath = ResolveExistingFile(manifestPath, label + " prediction manifest", errors);
            if (string.IsNullOrWhiteSpace(fullManifestPath))
            {
                return ValidatedPredictionRun.Empty;
            }
            string root = Path.GetDirectoryName(fullManifestPath) ?? string.Empty;
            var records = new Dictionary<string, SegmentationPredictionManifestRecord>(StringComparer.OrdinalIgnoreCase);
            try
            {
                int lineNumber = 0;
                foreach (string rawLine in File.ReadLines(fullManifestPath))
                {
                    lineNumber++;
                    if (string.IsNullOrWhiteSpace(rawLine))
                    {
                        continue;
                    }
                    SegmentationPredictionManifestRecord record = JsonConvert.DeserializeObject<SegmentationPredictionManifestRecord>(rawLine);
                    if (!ValidatePredictionRecord(record, label, lineNumber, dataset, split, expectedByImageSha, root, errors))
                    {
                        continue;
                    }
                    if (!records.TryAdd(record.ImageSha256, record))
                    {
                        errors.Add(label + " prediction manifest has duplicate image SHA-256: " + record.ImageSha256);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException || ex is JsonException || ex is UnauthorizedAccessException)
            {
                errors.Add(label + " prediction manifest cannot be read: " + ex.Message);
            }
            if (records.Count != expectedByImageSha.Count)
            {
                errors.Add(label + " prediction manifest image set does not equal the canonical test split.");
            }
            if (records.Keys.Except(expectedByImageSha.Keys, StringComparer.OrdinalIgnoreCase).Any())
            {
                errors.Add(label + " prediction manifest contains an image outside the canonical test split.");
            }
            SegmentationPredictionManifestRecord first = records.Values.FirstOrDefault();
            return new ValidatedPredictionRun(root, records, new SegmentationPredictionRunSummary
            {
                AdapterKey = first?.AdapterKey ?? string.Empty,
                Engine = first?.Engine ?? string.Empty,
                CheckpointPath = first?.CheckpointPath ?? string.Empty,
                CheckpointSha256 = first?.CheckpointSha256 ?? string.Empty,
                ImageCount = records.Count
            });
        }

        private static bool ValidatePredictionRecord(
            SegmentationPredictionManifestRecord record,
            string label,
            int lineNumber,
            UnetSegmentationDatasetExportManifest dataset,
            string split,
            IReadOnlyDictionary<string, UnetSegmentationDatasetExportManifestImage> expectedByImageSha,
            string root,
            List<string> errors)
        {
            string prefix = label + " prediction manifest line " + lineNumber.ToString(CultureInfo.InvariantCulture) + ": ";
            if (record == null || record.Version != 1)
            {
                errors.Add(prefix + "unsupported record version.");
                return false;
            }
            if (string.IsNullOrWhiteSpace(record.AdapterKey)
                || string.IsNullOrWhiteSpace(record.CheckpointPath)
                || string.IsNullOrWhiteSpace(record.CheckpointSha256))
            {
                errors.Add(prefix + "adapter/checkpoint provenance is missing.");
                return false;
            }
            if (!string.Equals(record.DatasetFingerprint, dataset.DatasetFingerprint, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(record.SourceDataTreeSha256, dataset.SourceDataTreeSha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(record.ClassContractSha256, dataset.ClassContractSha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(record.Split, split, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(prefix + "dataset, source, class, or split provenance does not match the canonical export.");
                return false;
            }
            if (!expectedByImageSha.TryGetValue(record.ImageSha256 ?? string.Empty, out UnetSegmentationDatasetExportManifestImage expected))
            {
                errors.Add(prefix + "image SHA-256 is not part of the canonical test split.");
                return false;
            }
            if (record.ImageWidth != expected.ImageWidth || record.ImageHeight != expected.ImageHeight)
            {
                errors.Add(prefix + "image dimensions do not match the canonical test image.");
                return false;
            }
            string maskPath = ResolveContainedPath(root, record.PredictionMaskRelativePath, label + " prediction mask", errors);
            if (string.IsNullOrWhiteSpace(maskPath))
            {
                return false;
            }
            if (!File.Exists(maskPath))
            {
                errors.Add(prefix + "prediction mask was not found: " + maskPath);
                return false;
            }
            if (!string.Equals(ComputeFileSha256(maskPath), record.PredictionMaskSha256, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(prefix + "prediction mask SHA-256 does not match its manifest.");
                return false;
            }
            return true;
        }

        private static byte[] ReadPredictionMask(
            ValidatedPredictionRun run,
            UnetSegmentationDatasetExportManifestImage expected,
            int classCount,
            List<string> errors)
        {
            if (!run.Records.TryGetValue(expected.ImageSha256, out SegmentationPredictionManifestRecord record))
            {
                errors.Add("Prediction manifest did not include canonical image: " + expected.SourceRelativeImagePath);
                return Array.Empty<byte>();
            }
            string path = ResolveContainedPath(run.RootPath, record.PredictionMaskRelativePath, "prediction mask", errors);
            if (string.IsNullOrWhiteSpace(path))
            {
                return Array.Empty<byte>();
            }
            if (!TryReadMaskValues(path, expected.ImageWidth, expected.ImageHeight, classCount, out byte[] values, out string error))
            {
                errors.Add("Prediction mask is invalid for " + expected.SourceRelativeImagePath + ": " + error);
                return Array.Empty<byte>();
            }
            return values;
        }

        private static void EvaluatePredictionMask(
            byte[] groundTruth,
            byte[] prediction,
            int width,
            int height,
            IReadOnlyList<MetricAccumulator> metrics,
            double componentIouThreshold)
        {
            for (int classIndex = 1; classIndex <= metrics.Count; classIndex++)
            {
                MetricAccumulator metric = metrics[classIndex - 1];
                for (int pixel = 0; pixel < groundTruth.Length; pixel++)
                {
                    bool actual = groundTruth[pixel] == classIndex;
                    bool predicted = prediction[pixel] == classIndex;
                    if (actual && predicted)
                    {
                        metric.TruePositivePixels++;
                    }
                    else if (predicted)
                    {
                        metric.FalsePositivePixels++;
                    }
                    else if (actual)
                    {
                        metric.FalseNegativePixels++;
                    }
                }
                AddComponentMetrics(groundTruth, prediction, width, height, (byte)classIndex, componentIouThreshold, metric);
            }
        }

        private static void AddComponentMetrics(
            byte[] groundTruth,
            byte[] prediction,
            int width,
            int height,
            byte classIndex,
            double threshold,
            MetricAccumulator metric)
        {
            ComponentMap actual = BuildComponentMap(groundTruth, width, height, classIndex);
            ComponentMap predicted = BuildComponentMap(prediction, width, height, classIndex);
            var intersections = new Dictionary<long, int>();
            for (int pixel = 0; pixel < actual.Labels.Length; pixel++)
            {
                int actualId = actual.Labels[pixel];
                int predictedId = predicted.Labels[pixel];
                if (actualId > 0 && predictedId > 0)
                {
                    long key = ((long)actualId << 32) | (uint)predictedId;
                    intersections[key] = intersections.TryGetValue(key, out int count) ? count + 1 : 1;
                }
            }
            var candidates = new List<ComponentMatch>();
            foreach (KeyValuePair<long, int> pair in intersections)
            {
                int actualId = (int)(pair.Key >> 32);
                int predictedId = (int)(pair.Key & uint.MaxValue);
                double iou = pair.Value / (double)(actual.Areas[actualId] + predicted.Areas[predictedId] - pair.Value);
                if (iou >= threshold)
                {
                    candidates.Add(new ComponentMatch(actualId, predictedId, iou));
                }
            }
            var usedActual = new HashSet<int>();
            var usedPredicted = new HashSet<int>();
            foreach (ComponentMatch match in candidates
                .OrderByDescending(item => item.IoU)
                .ThenBy(item => item.ActualId)
                .ThenBy(item => item.PredictedId))
            {
                if (usedActual.Add(match.ActualId) && usedPredicted.Add(match.PredictedId))
                {
                    metric.TruePositiveComponents++;
                }
            }
            metric.FalseNegativeComponents += actual.Areas.Count - 1 - usedActual.Count;
            metric.FalsePositiveComponents += predicted.Areas.Count - 1 - usedPredicted.Count;
        }

        private static ComponentMap BuildComponentMap(byte[] values, int width, int height, byte classIndex)
        {
            var labels = new int[values.Length];
            var areas = new List<int> { 0 };
            var queue = new Queue<int>();
            int next = 0;
            for (int start = 0; start < values.Length; start++)
            {
                if (values[start] != classIndex || labels[start] != 0)
                {
                    continue;
                }
                int label = ++next;
                int area = 0;
                labels[start] = label;
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    area++;
                    int x = current % width;
                    int y = current / width;
                    TryEnqueue(current - 1, x > 0, values, labels, classIndex, label, queue);
                    TryEnqueue(current + 1, x + 1 < width, values, labels, classIndex, label, queue);
                    TryEnqueue(current - width, y > 0, values, labels, classIndex, label, queue);
                    TryEnqueue(current + width, y + 1 < height, values, labels, classIndex, label, queue);
                }
                areas.Add(area);
            }
            return new ComponentMap(labels, areas);
        }

        private static void TryEnqueue(int index, bool inside, byte[] values, int[] labels, byte classIndex, int label, Queue<int> queue)
        {
            if (inside && values[index] == classIndex && labels[index] == 0)
            {
                labels[index] = label;
                queue.Enqueue(index);
            }
        }

        private static bool TryReadMaskValues(string path, int width, int height, int classCount, out byte[] values, out string error)
        {
            values = Array.Empty<byte>();
            error = string.Empty;
            try
            {
                using var source = new Bitmap(path);
                if (source.Width != width || source.Height != height)
                {
                    error = $"mask size {source.Width}x{source.Height} does not match expected {width}x{height}";
                    return false;
                }
                Rectangle bounds = new Rectangle(Point.Empty, source.Size);
                using Bitmap normalized = source.Clone(bounds, PixelFormat.Format24bppRgb);
                BitmapData bitmapData = normalized.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                try
                {
                    int stride = Math.Abs(bitmapData.Stride);
                    var pixels = new byte[stride * height];
                    Marshal.Copy(bitmapData.Scan0, pixels, 0, pixels.Length);
                    values = new byte[width * height];
                    for (int y = 0; y < height; y++)
                    {
                        int row = bitmapData.Stride >= 0 ? y : height - 1 - y;
                        for (int x = 0; x < width; x++)
                        {
                            int pixel = row * stride + x * 3;
                            byte value = pixels[pixel];
                            if (pixels[pixel + 1] != value || pixels[pixel + 2] != value)
                            {
                                error = "mask pixels must use identical red, green, and blue class-index values";
                                return false;
                            }
                            if (value > classCount)
                            {
                                error = $"mask class value {value} is outside background 0 and configured classes 1..{classCount}";
                                return false;
                            }
                            values[y * width + x] = value;
                        }
                    }
                    return true;
                }
                finally
                {
                    normalized.UnlockBits(bitmapData);
                }
            }
            catch (Exception ex) when (ex is ArgumentException || ex is ExternalException || ex is IOException)
            {
                error = ex.Message;
                return false;
            }
        }

        private static void WriteReport(string outputRootPath, SegmentationMaskComparisonResult result)
        {
            if (string.IsNullOrWhiteSpace(outputRootPath) || !result.IsReady)
            {
                return;
            }
            string root = Path.GetFullPath(outputRootPath);
            if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
            {
                result.Errors.Add("Segmentation comparison output directory must be new or empty.");
                return;
            }
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "comparison-summary.json");
            File.WriteAllText(path, JsonConvert.SerializeObject(result, Formatting.Indented));
            result.ReportPath = path;
        }

        private static string ResolveExistingDirectory(string path, string label, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                errors.Add(label + " not found: " + (path ?? string.Empty));
                return string.Empty;
            }
            return Path.GetFullPath(path);
        }

        private static string ResolveExistingFile(string path, string label, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                errors.Add(label + " not found: " + (path ?? string.Empty));
                return string.Empty;
            }
            return Path.GetFullPath(path);
        }

        private static string ResolveContainedPath(string root, string relativePath, string label, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(relativePath))
            {
                errors.Add(label + " path is missing.");
                return string.Empty;
            }
            try
            {
                string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string fullPath = Path.GetFullPath(Path.Combine(fullRoot, relativePath));
                string relation = Path.GetRelativePath(fullRoot, fullPath);
                if (relation.Equals("..", StringComparison.Ordinal) || relation.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                {
                    errors.Add(label + " path escapes its app-owned artifact root.");
                    return string.Empty;
                }
                return fullPath;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException)
            {
                errors.Add(label + " path is invalid: " + ex.Message);
                return string.Empty;
            }
        }

        private static string NormalizeSplit(string value)
        {
            return string.Equals(value, "train", StringComparison.OrdinalIgnoreCase)
                ? "train"
                : string.Equals(value, "valid", StringComparison.OrdinalIgnoreCase)
                    ? "valid"
                    : "test";
        }

        private static string ComputeFileSha256(string path)
        {
            using SHA256 hash = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            return Convert.ToHexString(hash.ComputeHash(stream));
        }

        private sealed class ValidatedPredictionRun
        {
            public static readonly ValidatedPredictionRun Empty = new ValidatedPredictionRun(string.Empty, new Dictionary<string, SegmentationPredictionManifestRecord>(), new SegmentationPredictionRunSummary());

            public ValidatedPredictionRun(string rootPath, Dictionary<string, SegmentationPredictionManifestRecord> records, SegmentationPredictionRunSummary summary)
            {
                RootPath = rootPath;
                Records = records;
                Summary = summary;
            }

            public string RootPath { get; }

            public IReadOnlyDictionary<string, SegmentationPredictionManifestRecord> Records { get; }

            public SegmentationPredictionRunSummary Summary { get; }
        }

        private sealed class MetricAccumulator
        {
            public MetricAccumulator(int classIndex)
            {
                ClassIndex = classIndex;
            }

            public int ClassIndex { get; }

            public long TruePositivePixels { get; set; }

            public long FalsePositivePixels { get; set; }

            public long FalseNegativePixels { get; set; }

            public int TruePositiveComponents { get; set; }

            public int FalsePositiveComponents { get; set; }

            public int FalseNegativeComponents { get; set; }

            public SegmentationMaskMetrics ToMetrics()
            {
                long diceDenominator = (2 * TruePositivePixels) + FalsePositivePixels + FalseNegativePixels;
                long iouDenominator = TruePositivePixels + FalsePositivePixels + FalseNegativePixels;
                return new SegmentationMaskMetrics
                {
                    TruePositivePixels = TruePositivePixels,
                    FalsePositivePixels = FalsePositivePixels,
                    FalseNegativePixels = FalseNegativePixels,
                    TruePositiveComponents = TruePositiveComponents,
                    FalsePositiveComponents = FalsePositiveComponents,
                    FalseNegativeComponents = FalseNegativeComponents,
                    Dice = diceDenominator == 0 ? 1.0D : (2.0D * TruePositivePixels) / diceDenominator,
                    IoU = iouDenominator == 0 ? 1.0D : TruePositivePixels / (double)iouDenominator
                };
            }
        }

        private sealed class ComponentMap
        {
            public ComponentMap(int[] labels, List<int> areas)
            {
                Labels = labels;
                Areas = areas;
            }

            public int[] Labels { get; }

            public List<int> Areas { get; }
        }

        private readonly struct ComponentMatch
        {
            public ComponentMatch(int actualId, int predictedId, double iou)
            {
                ActualId = actualId;
                PredictedId = predictedId;
                IoU = iou;
            }

            public int ActualId { get; }

            public int PredictedId { get; }

            public double IoU { get; }
        }
    }
}
