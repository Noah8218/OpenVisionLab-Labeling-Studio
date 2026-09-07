using MvcVisionSystem._1._Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace MvcVisionSystem.Yolo
{
    public static class SegmentationPredictionExportService
    {
        public const string AdapterUnet = "unet";
        public const string AdapterUltralytics = "ultralytics";
        private static readonly ExternalProcessRunner ProcessRunner = new ExternalProcessRunner();

        public static SegmentationPredictionExportRequest BuildRequest(
            string adapterKey,
            PythonModelSettings settings,
            string datasetExportRootPath,
            string outputRootPath,
            string split = "test")
        {
            settings ??= new PythonModelSettings();
            PythonModelRuntimePathResolver.ApplyPathDefaults(settings);
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
            => Run(request, CancellationToken.None);

        public static SegmentationPredictionExportResult Run(
            SegmentationPredictionExportRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<string> errors = ValidateRequest(request);
            if (errors.Count > 0)
            {
                return new SegmentationPredictionExportResult { Error = string.Join(Environment.NewLine, errors) };
            }

            ExternalProcessRunResult processResult = ProcessRunner.Run(
                CreateStartInfo(request),
                cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            string output = processResult.Output;
            string error = processResult.Error;
            string manifestPath = ParseManifestPath(output);
            bool succeeded = processResult.ExitCode == 0 && File.Exists(manifestPath);
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
}
