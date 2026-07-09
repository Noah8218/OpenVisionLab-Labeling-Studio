using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    public sealed class WpfAnomalyClassificationEvaluationRunService
    {
        private readonly string repositoryRoot;

        public WpfAnomalyClassificationEvaluationRunService(string repositoryRoot = "")
        {
            this.repositoryRoot = string.IsNullOrWhiteSpace(repositoryRoot)
                ? FindRepositoryRoot()
                : repositoryRoot;
        }

        public WpfAnomalyClassificationEvaluationRunRequest BuildRequest(CData data)
        {
            data?.NormalizeOutputPaths();
            data?.NormalizeTrainingSettings();
            data?.ProjectSettings?.EnsureDefaults();

            PythonModelSettings settings = data?.ProjectSettings?.PythonModel ?? new PythonModelSettings();
            settings.EnsureDefaults();
            string outputRoot = data?.OutputRootPath ?? string.Empty;
            string datasetRoot = string.Empty;
            string preparationError = string.Empty;
            if (!string.IsNullOrWhiteSpace(outputRoot))
            {
                string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                datasetRoot = Path.Combine(outputRoot, "classification-evaluation-input", timestamp);
                preparationError = TryExportEvaluationDataset(data, datasetRoot);
            }

            return new WpfAnomalyClassificationEvaluationRunRequest
            {
                ScriptPath = Path.Combine(repositoryRoot, "scripts", "evaluate-yolo-classification.ps1"),
                PythonExecutablePath = PythonModelSettingsValidator.ResolvePythonExecutable(settings),
                WorkerScriptPath = settings.ClientScriptPath?.Trim() ?? string.Empty,
                ModelRootPath = settings.GetModelRootPath(),
                WeightsPath = settings.WeightsPath?.Trim() ?? string.Empty,
                DatasetRootPath = datasetRoot,
                Split = "test",
                ImageSize = Math.Max(64, settings.InferenceImageSize),
                Confidence = 0D,
                OutputDirectory = outputRoot,
                MinimumConfidence = data?.ProjectSettings?.AnomalyClassification?.MinimumConfidence ?? 0D,
                ModelName = settings.GetProtocolModelName(),
                PreparationError = preparationError
            };
        }

        public IReadOnlyList<string> ValidateRequest(WpfAnomalyClassificationEvaluationRunRequest request)
        {
            var errors = new List<string>();
            if (request == null)
            {
                errors.Add("\uC774\uC0C1 \uBD84\uB958 \uD3C9\uAC00 \uC694\uCCAD \uC815\uBCF4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.");
                return errors;
            }

            if (!string.IsNullOrWhiteSpace(request.PreparationError))
            {
                errors.Add(request.PreparationError);
            }

            if (!string.Equals(request.ModelName, "yolov8", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("YOLOv8 anomaly classification \uD3C9\uAC00\uB294 YOLOv8 \uB7F0\uD0C0\uC784\uC5D0\uC11C\uB9CC \uC2E4\uD589\uD569\uB2C8\uB2E4.");
            }

            ValidateFile(request.ScriptPath, "\uC774\uC0C1 \uBD84\uB958 \uD3C9\uAC00 \uC2A4\uD06C\uB9BD\uD2B8", errors);
            ValidateFile(request.PythonExecutablePath, "Python \uC2E4\uD589 \uD30C\uC77C", errors);
            ValidateFile(request.WorkerScriptPath, "YOLOv8 TCP adapter", errors);
            ValidateDirectory(request.ModelRootPath, "YOLOv8 \uB85C\uCEEC \uC18C\uC2A4 \uD3F4\uB354", errors);
            ValidateFile(request.WeightsPath, "\uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378 \uD30C\uC77C", errors);
            ValidateDirectory(request.DatasetRootPath, "anomaly classification \uD3C9\uAC00 \uB370\uC774\uD130\uC14B", errors);
            ValidateDirectory(request.OutputDirectory, "\uD3C9\uAC00 \uACB0\uACFC \uC800\uC7A5 \uD3F4\uB354", errors);
            ValidateSplitImages(request, errors);

            if (request.ImageSize < 64 || request.ImageSize > 2048)
            {
                errors.Add($"\uD3C9\uAC00 \uC774\uBBF8\uC9C0 \uD06C\uAE30\uB294 64~2048 \uC0AC\uC774\uC5EC\uC57C \uD569\uB2C8\uB2E4: {request.ImageSize}");
            }

            if (request.MinimumConfidence < 0D || request.MinimumConfidence > 1D)
            {
                errors.Add($"\uD3C9\uAC00 \uC2E0\uB8B0\uB3C4 \uAE30\uC900\uC740 0~1 \uC0AC\uC774\uC5EC\uC57C \uD569\uB2C8\uB2E4: {request.MinimumConfidence}");
            }

            return errors;
        }

        public async Task<WpfAnomalyClassificationEvaluationRunResult> RunAsync(
            WpfAnomalyClassificationEvaluationRunRequest request,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<string> errors = ValidateRequest(request);
            if (errors.Count > 0)
            {
                return WpfAnomalyClassificationEvaluationRunResult.Failed(string.Join(Environment.NewLine, errors), string.Empty, string.Empty);
            }

            using Process process = new Process
            {
                StartInfo = CreateStartInfo(request),
                EnableRaisingEvents = false
            };

            var output = new StringBuilder();
            var error = new StringBuilder();
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    output.AppendLine(e.Data);
                }
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    error.AppendLine(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            string stdout = output.ToString();
            string stderr = error.ToString();
            string summaryPath = TryFindSummaryPath(stdout);
            return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(summaryPath)
                ? WpfAnomalyClassificationEvaluationRunResult.Success(summaryPath, stdout, stderr)
                : WpfAnomalyClassificationEvaluationRunResult.Failed(BuildFailureMessage(process.ExitCode, stderr, stdout), stdout, stderr);
        }

        public ProcessStartInfo CreateStartInfo(WpfAnomalyClassificationEvaluationRunRequest request)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = repositoryRoot
            };

            foreach (string argument in BuildPowerShellArguments(request))
            {
                startInfo.ArgumentList.Add(argument);
            }

            return startInfo;
        }

        public IReadOnlyList<string> BuildPowerShellArguments(WpfAnomalyClassificationEvaluationRunRequest request)
        {
            return new[]
            {
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                request.ScriptPath,
                "-PythonExe",
                request.PythonExecutablePath,
                "-WorkerScript",
                request.WorkerScriptPath,
                "-ModelRoot",
                request.ModelRootPath,
                "-Weights",
                request.WeightsPath,
                "-DatasetRoot",
                request.DatasetRootPath,
                "-Split",
                request.Split,
                "-ImageSize",
                request.ImageSize.ToString(CultureInfo.InvariantCulture),
                "-Confidence",
                request.Confidence.ToString(CultureInfo.InvariantCulture),
                "-OutputDirectory",
                request.OutputDirectory,
                "-MinimumTotalImageCount",
                request.MinimumTotalImageCount.ToString(CultureInfo.InvariantCulture),
                "-MinimumPerClassImageCount",
                request.MinimumPerClassImageCount.ToString(CultureInfo.InvariantCulture),
                "-MinimumAccuracy",
                request.MinimumAccuracy.ToString(CultureInfo.InvariantCulture),
                "-MinimumPerClassAccuracy",
                request.MinimumPerClassAccuracy.ToString(CultureInfo.InvariantCulture),
                "-MinimumConfidence",
                request.MinimumConfidence.ToString(CultureInfo.InvariantCulture)
            };
        }

        private static string TryExportEvaluationDataset(CData data, string datasetRoot)
        {
            try
            {
                AnomalyClassificationTrainingReadinessReport readiness =
                    AnomalyClassificationTrainingReadinessService.Build(data);
                if (readiness.SourceImagePaths.Count == 0)
                {
                    return AnomalyClassificationTrainingReadinessService.NoSourceImagesError;
                }

                var exportService = new AnomalyClassificationDatasetExportService();
                exportService.Export(data, readiness.SourceImagePaths, datasetRoot);
                return string.Empty;
            }
            catch (Exception ex)
            {
                return $"classification evaluation dataset export failed. {ex.Message}";
            }
        }

        private static void ValidateSplitImages(WpfAnomalyClassificationEvaluationRunRequest request, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(request?.DatasetRootPath) || !Directory.Exists(request.DatasetRootPath))
            {
                return;
            }

            string split = string.IsNullOrWhiteSpace(request.Split) ? "test" : request.Split.Trim();
            int normalCount = CountImages(Path.Combine(request.DatasetRootPath, split, AnomalyClassificationDatasetExportService.NormalClassFolderName));
            int abnormalCount = CountImages(Path.Combine(request.DatasetRootPath, split, AnomalyClassificationDatasetExportService.AbnormalClassFolderName));
            if (normalCount <= 0 || abnormalCount <= 0)
            {
                errors.Add($"\uD3C9\uAC00\uC5D0\uB294 {split} normal/abnormal \uC774\uBBF8\uC9C0\uAC00 \uAC01\uAC01 1\uC7A5 \uC774\uC0C1 \uD544\uC694\uD569\uB2C8\uB2E4. normal:{normalCount}, abnormal:{abnormalCount}");
            }
        }

        private static int CountImages(string directory)
        {
            return string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)
                ? 0
                : Directory.EnumerateFiles(directory, "*.*", SearchOption.TopDirectoryOnly).Count(IsSupportedImagePath);
        }

        private static bool IsSupportedImagePath(string path)
        {
            string extension = Path.GetExtension(path);
            return string.Equals(extension, ".bmp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".tif", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".tiff", StringComparison.OrdinalIgnoreCase);
        }

        private static void ValidateFile(string path, string name, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                errors.Add($"{name} not found: {path}");
            }
        }

        private static void ValidateDirectory(string path, string name, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                errors.Add($"{name} not found: {path}");
            }
        }

        private static string TryFindSummaryPath(string stdout)
        {
            foreach (string line in (stdout ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Reverse())
            {
                string trimmed = line.Trim();
                if (trimmed.EndsWith("classification-evaluation-summary.json", StringComparison.OrdinalIgnoreCase)
                    && File.Exists(trimmed))
                {
                    return Path.GetFullPath(trimmed);
                }
            }

            return string.Empty;
        }

        private static string BuildFailureMessage(int exitCode, string stderr, string stdout)
        {
            string detail = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            if (string.IsNullOrWhiteSpace(detail))
            {
                detail = "No process output.";
            }

            return $"Anomaly classification evaluation failed. ExitCode={exitCode}. {detail.Trim()}";
        }

        private static string FindRepositoryRoot()
        {
            foreach (string startPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                string current = startPath;
                while (!string.IsNullOrWhiteSpace(current))
                {
                    if (File.Exists(Path.Combine(current, "OpenVisionLab.LabelingStudio.sln"))
                        || File.Exists(Path.Combine(current, "OpenVisionLab.LabelingStudio.csproj")))
                    {
                        return current;
                    }

                    current = Directory.GetParent(current)?.FullName;
                }
            }

            return Directory.GetCurrentDirectory();
        }
    }

    public sealed class WpfAnomalyClassificationEvaluationRunRequest
    {
        public string ScriptPath { get; set; } = string.Empty;

        public string PythonExecutablePath { get; set; } = string.Empty;

        public string WorkerScriptPath { get; set; } = string.Empty;

        public string ModelRootPath { get; set; } = string.Empty;

        public string WeightsPath { get; set; } = string.Empty;

        public string DatasetRootPath { get; set; } = string.Empty;

        public string Split { get; set; } = "test";

        public int ImageSize { get; set; } = 64;

        public double Confidence { get; set; }

        public string OutputDirectory { get; set; } = string.Empty;

        public int MinimumTotalImageCount { get; set; } = 10;

        public int MinimumPerClassImageCount { get; set; } = 5;

        public double MinimumAccuracy { get; set; } = 0.9D;

        public double MinimumPerClassAccuracy { get; set; } = 0.8D;

        public double MinimumConfidence { get; set; }

        public string ModelName { get; set; } = string.Empty;

        public string PreparationError { get; set; } = string.Empty;
    }

    public sealed class WpfAnomalyClassificationEvaluationRunResult
    {
        private WpfAnomalyClassificationEvaluationRunResult(bool succeeded, string summaryPath, string output, string error)
        {
            Succeeded = succeeded;
            SummaryPath = summaryPath ?? string.Empty;
            Output = output ?? string.Empty;
            Error = error ?? string.Empty;
        }

        public bool Succeeded { get; }

        public string SummaryPath { get; }

        public string Output { get; }

        public string Error { get; }

        public static WpfAnomalyClassificationEvaluationRunResult Success(string summaryPath, string output, string error)
            => new WpfAnomalyClassificationEvaluationRunResult(true, summaryPath, output, error);

        public static WpfAnomalyClassificationEvaluationRunResult Failed(string error, string output, string stderr)
            => new WpfAnomalyClassificationEvaluationRunResult(false, string.Empty, output, string.IsNullOrWhiteSpace(error) ? stderr : error);
    }
}
