using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    public class AnomalyClassificationEvaluationRunService
    {
        private readonly string repositoryRoot;
        private readonly ExternalProcessRunner processRunner;

        public AnomalyClassificationEvaluationRunService(
            string repositoryRoot = "",
            ExternalProcessRunner processRunner = null)
        {
            this.repositoryRoot = string.IsNullOrWhiteSpace(repositoryRoot)
                ? FindRepositoryRoot()
                : repositoryRoot;
            this.processRunner = processRunner ?? new ExternalProcessRunner();
        }

        public AnomalyClassificationEvaluationRunRequest BuildRequest(LabelingProjectData data)
        {
            data?.NormalizeOutputPaths();
            data?.NormalizeTrainingSettings();
            data?.ProjectSettings?.EnsureDefaults();

            PythonModelSettings settings = data?.ProjectSettings?.PythonModel ?? new PythonModelSettings();
            _1._Core.PythonModelRuntimePathResolver.ApplyDefaults(settings);
            string outputRoot = data?.OutputRootPath ?? string.Empty;
            string datasetRoot = string.Empty;
            string preparationError = string.Empty;
            if (!string.IsNullOrWhiteSpace(outputRoot))
            {
                string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                datasetRoot = Path.Combine(outputRoot, "classification-evaluation-input", timestamp);
                preparationError = TryExportEvaluationDataset(data, datasetRoot);
            }

            return new AnomalyClassificationEvaluationRunRequest
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
                MaximumCandidates = settings.MaximumDetectionCandidates,
                Device = "cpu",
                OutputDirectory = outputRoot,
                MinimumConfidence = data?.ProjectSettings?.AnomalyClassification?.MinimumConfidence ?? 0D,
                ModelName = settings.GetProtocolModelName(),
                PreparationError = preparationError
            };
        }

        public IReadOnlyList<string> ValidateRequest(AnomalyClassificationEvaluationRunRequest request)
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

            bool isSupportedModel =
                string.Equals(request.ModelName, "yolov8", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(request.ModelName, "yolo11", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(request.ModelName, "patchcore", StringComparison.OrdinalIgnoreCase);
            if (!isSupportedModel)
            {
                errors.Add("\uC774\uC0C1\uD0D0\uC9C0 \uD3C9\uAC00\uB294 YOLOv8, YOLO11 \uB610\uB294 PatchCore \uB7F0\uD0C0\uC784\uC5D0\uC11C\uB9CC \uC2E4\uD589\uD569\uB2C8\uB2E4.");
            }

            ValidateFile(request.ScriptPath, "\uC774\uC0C1\uD0D0\uC9C0 \uD3C9\uAC00 \uC2A4\uD06C\uB9BD\uD2B8", errors);
            ValidateFile(request.PythonExecutablePath, "Python \uC2E4\uD589 \uD30C\uC77C", errors);
            ValidateFile(request.WorkerScriptPath, "\uC774\uC0C1\uD0D0\uC9C0 worker", errors);
            ValidateDirectory(request.ModelRootPath, "\uBAA8\uB378 \uB7F0\uD0C0\uC784 \uD3F4\uB354", errors);
            ValidateFile(request.WeightsPath, "\uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378 \uD30C\uC77C", errors);
            ValidateDirectory(request.DatasetRootPath, "anomaly classification \uD3C9\uAC00 \uB370\uC774\uD130\uC14B", errors);
            ValidateDirectory(request.OutputDirectory, "\uD3C9\uAC00 \uACB0\uACFC \uC800\uC7A5 \uD3F4\uB354", errors);
            ValidateSplitImages(request, errors);
            ValidateDatasetContentLeakage(request, errors);

            if (request.ImageSize < 64 || request.ImageSize > 2048)
            {
                errors.Add($"\uD3C9\uAC00 \uC774\uBBF8\uC9C0 \uD06C\uAE30\uB294 64~2048 \uC0AC\uC774\uC5EC\uC57C \uD569\uB2C8\uB2E4: {request.ImageSize}");
            }

            if (request.MinimumConfidence < 0D || request.MinimumConfidence > 1D)
            {
                errors.Add($"\uD3C9\uAC00 \uC2E0\uB8B0\uB3C4 \uAE30\uC900\uC740 0~1 \uC0AC\uC774\uC5EC\uC57C \uD569\uB2C8\uB2E4: {request.MinimumConfidence}");
            }

            if (request.MaximumCandidates < 1 || request.MaximumCandidates > 200)
            {
                errors.Add($"\uC704\uCE58 \uD6C4\uBCF4 \uCD5C\uB300 \uAC1C\uC218\uB294 1~200\uC774\uC5B4\uC57C \uD569\uB2C8\uB2E4: {request.MaximumCandidates}");
            }

            return errors;
        }

        public async Task<AnomalyClassificationEvaluationRunResult> RunAsync(
            AnomalyClassificationEvaluationRunRequest request,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<string> errors = ValidateRequest(request);
            if (errors.Count > 0)
            {
                return AnomalyClassificationEvaluationRunResult.Failed(string.Join(Environment.NewLine, errors), string.Empty, string.Empty);
            }

            ExternalProcessRunResult processResult = await processRunner
                .RunAsync(CreateStartInfo(request), Timeout.InfiniteTimeSpan, cancellationToken)
                .ConfigureAwait(false);
            string stdout = processResult.Output;
            string stderr = processResult.Error;
            if (processResult.Canceled)
            {
                return AnomalyClassificationEvaluationRunResult.Failed(
                    "Anomaly classification evaluation was canceled.",
                    stdout,
                    stderr);
            }

            if (processResult.TimedOut)
            {
                return AnomalyClassificationEvaluationRunResult.Failed(
                    "Anomaly classification evaluation timed out.",
                    stdout,
                    stderr);
            }

            string summaryPath = TryFindSummaryPath(stdout);
            return processResult.ExitCode == 0 && !string.IsNullOrWhiteSpace(summaryPath)
                ? AnomalyClassificationEvaluationRunResult.Success(summaryPath, stdout, stderr)
                : AnomalyClassificationEvaluationRunResult.Failed(BuildFailureMessage(processResult.ExitCode, stderr, stdout), stdout, stderr);
        }

        public ProcessStartInfo CreateStartInfo(AnomalyClassificationEvaluationRunRequest request)
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

        public IReadOnlyList<string> BuildPowerShellArguments(AnomalyClassificationEvaluationRunRequest request)
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
                "-ModelName",
                request.ModelName,
                "-Device",
                request.Device,
                "-MaximumCandidates",
                request.MaximumCandidates.ToString(CultureInfo.InvariantCulture),
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

        private static string TryExportEvaluationDataset(LabelingProjectData data, string datasetRoot)
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

        private static void ValidateSplitImages(AnomalyClassificationEvaluationRunRequest request, List<string> errors)
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

        private static void ValidateDatasetContentLeakage(
            AnomalyClassificationEvaluationRunRequest request,
            List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(request?.DatasetRootPath) || !Directory.Exists(request.DatasetRootPath))
            {
                return;
            }

            string[] splits =
            {
                YoloDatasetSplitService.TrainMode,
                YoloDatasetSplitService.ValidMode,
                YoloDatasetSplitService.TestMode
            };

            for (int leftIndex = 0; leftIndex < splits.Length; leftIndex++)
            {
                string leftSplit = splits[leftIndex];
                string leftRoot = Path.Combine(request.DatasetRootPath, leftSplit);
                if (!HasSplitImages(leftRoot))
                {
                    continue;
                }

                for (int rightIndex = leftIndex + 1; rightIndex < splits.Length; rightIndex++)
                {
                    string rightSplit = splits[rightIndex];
                    string rightRoot = Path.Combine(request.DatasetRootPath, rightSplit);
                    if (!HasSplitImages(rightRoot))
                    {
                        continue;
                    }

                    ValidateNoContentOverlap(
                        new[] { leftRoot },
                        rightRoot,
                        $"{leftSplit}/{rightSplit} \uBD84\uD560",
                        $"\uAC19\uC740 \uCD2C\uC601 \uC774\uBBF8\uC9C0\uB97C \uD558\uB098\uC758 \uBD84\uD560\uC5D0\uB9CC \uB450\uC138\uC694: {leftRoot} | {rightRoot}",
                        errors);
                }
            }

            foreach (string split in splits)
            {
                string splitRoot = Path.Combine(request.DatasetRootPath, split);
                string normalRoot = Path.Combine(splitRoot, AnomalyClassificationDatasetExportService.NormalClassFolderName);
                string abnormalRoot = Path.Combine(splitRoot, AnomalyClassificationDatasetExportService.AbnormalClassFolderName);
                if (CountImages(normalRoot) <= 0 || CountImages(abnormalRoot) <= 0)
                {
                    continue;
                }

                ValidateNoContentOverlap(
                    new[] { normalRoot },
                    abnormalRoot,
                    $"{split} normal/abnormal",
                    $"\uAC19\uC740 \uC774\uBBF8\uC9C0\uB97C \uC815\uC0C1\uACFC \uC774\uC0C1\uC5D0 \uB3D9\uC2DC\uC5D0 \uB450\uC9C0 \uB9C8\uC138\uC694: {normalRoot} | {abnormalRoot}",
                    errors);
            }
        }

        private static bool HasSplitImages(string splitRoot)
        {
            return CountImages(Path.Combine(splitRoot, AnomalyClassificationDatasetExportService.NormalClassFolderName)) > 0
                || CountImages(Path.Combine(splitRoot, AnomalyClassificationDatasetExportService.AbnormalClassFolderName)) > 0;
        }

        private static void ValidateNoContentOverlap(
            IEnumerable<string> referenceDirectories,
            string comparisonDirectory,
            string scope,
            string guidance,
            List<string> errors)
        {
            YoloExternalEvaluationDataAuditReport report =
                YoloExternalEvaluationDataAuditService.Build(referenceDirectories, comparisonDirectory);
            foreach (string auditError in report.Errors)
            {
                AddDistinct(
                    errors,
                    $"\uD3C9\uAC00 \uB370\uC774\uD130 \uB204\uC218 \uAC80\uC0AC\uB97C \uC644\uB8CC\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4 ({scope}): {auditError} " +
                    "\uC77D\uC744 \uC218 \uC5C6\uB294 \uD30C\uC77C\uC744 \uC815\uB9AC\uD55C \uB4A4 \uB2E4\uC2DC \uC2E4\uD589\uD558\uC138\uC694.");
            }

            if (report.HasContentOverlap)
            {
                string example = string.IsNullOrWhiteSpace(report.OverlapExample)
                    ? string.Empty
                    : $" \uC608: {report.OverlapExample}.";
                AddDistinct(
                    errors,
                    $"\uD3C9\uAC00 \uB370\uC774\uD130 \uB204\uC218: {scope}\uC5D0 \uAE38\uC774+SHA-256\uC774 \uAC19\uC740 \uC774\uBBF8\uC9C0 \uCF58\uD150\uCE20 {report.ContentOverlapCount}\uAC74\uC774 \uC788\uC2B5\uB2C8\uB2E4.{example} {guidance}");
            }
        }

        private static void AddDistinct(List<string> errors, string error)
        {
            if (!errors.Contains(error, StringComparer.Ordinal))
            {
                errors.Add(error);
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
            => RepositoryRootResolver.FindRepositoryRoot();
    }

    [Obsolete("Use AnomalyClassificationEvaluationRunService.", false)]
    public sealed class WpfAnomalyClassificationEvaluationRunService : AnomalyClassificationEvaluationRunService
    {
        public WpfAnomalyClassificationEvaluationRunService(
            string repositoryRoot = "",
            ExternalProcessRunner processRunner = null)
            : base(repositoryRoot, processRunner)
        {
        }

        public new WpfAnomalyClassificationEvaluationRunRequest BuildRequest(LabelingProjectData data)
        {
            return WpfAnomalyClassificationEvaluationRunRequest.FromCanonical(base.BuildRequest(data));
        }

        public IReadOnlyList<string> ValidateRequest(WpfAnomalyClassificationEvaluationRunRequest request)
        {
            return base.ValidateRequest(request);
        }

        public async Task<WpfAnomalyClassificationEvaluationRunResult> RunAsync(
            WpfAnomalyClassificationEvaluationRunRequest request,
            CancellationToken cancellationToken = default)
        {
            AnomalyClassificationEvaluationRunResult result = await base.RunAsync(request, cancellationToken).ConfigureAwait(false);
            return WpfAnomalyClassificationEvaluationRunResult.FromCanonical(result);
        }

        public ProcessStartInfo CreateStartInfo(WpfAnomalyClassificationEvaluationRunRequest request)
        {
            return base.CreateStartInfo(request);
        }

        public IReadOnlyList<string> BuildPowerShellArguments(WpfAnomalyClassificationEvaluationRunRequest request)
        {
            return base.BuildPowerShellArguments(request);
        }
    }

    public class AnomalyClassificationEvaluationRunRequest
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

        public int MaximumCandidates { get; set; } = 20;

        public string Device { get; set; } = "cpu";

        public string OutputDirectory { get; set; } = string.Empty;

        public int MinimumTotalImageCount { get; set; } = 10;

        public int MinimumPerClassImageCount { get; set; } = 5;

        public double MinimumAccuracy { get; set; } = 0.9D;

        public double MinimumPerClassAccuracy { get; set; } = 0.8D;

        public double MinimumConfidence { get; set; }

        public string ModelName { get; set; } = string.Empty;

        public string PreparationError { get; set; } = string.Empty;
    }

    public class AnomalyClassificationEvaluationRunResult
    {
        protected AnomalyClassificationEvaluationRunResult(bool succeeded, string summaryPath, string output, string error)
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

        public static AnomalyClassificationEvaluationRunResult Success(string summaryPath, string output, string error)
            => new AnomalyClassificationEvaluationRunResult(true, summaryPath, output, error);

        public static AnomalyClassificationEvaluationRunResult Failed(string error, string output, string stderr)
            => new AnomalyClassificationEvaluationRunResult(false, string.Empty, output, string.IsNullOrWhiteSpace(error) ? stderr : error);
    }

    [Obsolete("Use AnomalyClassificationEvaluationRunRequest.", false)]
    public sealed class WpfAnomalyClassificationEvaluationRunRequest : AnomalyClassificationEvaluationRunRequest
    {
        internal static WpfAnomalyClassificationEvaluationRunRequest FromCanonical(AnomalyClassificationEvaluationRunRequest source)
        {
            if (source == null)
            {
                return null;
            }

            return new WpfAnomalyClassificationEvaluationRunRequest
            {
                ScriptPath = source.ScriptPath,
                PythonExecutablePath = source.PythonExecutablePath,
                WorkerScriptPath = source.WorkerScriptPath,
                ModelRootPath = source.ModelRootPath,
                WeightsPath = source.WeightsPath,
                DatasetRootPath = source.DatasetRootPath,
                Split = source.Split,
                ImageSize = source.ImageSize,
                Confidence = source.Confidence,
                MaximumCandidates = source.MaximumCandidates,
                Device = source.Device,
                OutputDirectory = source.OutputDirectory,
                MinimumTotalImageCount = source.MinimumTotalImageCount,
                MinimumPerClassImageCount = source.MinimumPerClassImageCount,
                MinimumAccuracy = source.MinimumAccuracy,
                MinimumPerClassAccuracy = source.MinimumPerClassAccuracy,
                MinimumConfidence = source.MinimumConfidence,
                ModelName = source.ModelName,
                PreparationError = source.PreparationError
            };
        }
    }

    [Obsolete("Use AnomalyClassificationEvaluationRunResult.", false)]
    public sealed class WpfAnomalyClassificationEvaluationRunResult : AnomalyClassificationEvaluationRunResult
    {
        private WpfAnomalyClassificationEvaluationRunResult(bool succeeded, string summaryPath, string output, string error)
            : base(succeeded, summaryPath, output, error)
        {
        }

        internal static WpfAnomalyClassificationEvaluationRunResult FromCanonical(AnomalyClassificationEvaluationRunResult source)
        {
            return source == null
                ? null
                : new WpfAnomalyClassificationEvaluationRunResult(source.Succeeded, source.SummaryPath, source.Output, source.Error);
        }

        public static new WpfAnomalyClassificationEvaluationRunResult Success(string summaryPath, string output, string error)
            => new WpfAnomalyClassificationEvaluationRunResult(true, summaryPath, output, error);

        public static new WpfAnomalyClassificationEvaluationRunResult Failed(string error, string output, string stderr)
            => new WpfAnomalyClassificationEvaluationRunResult(false, string.Empty, output, string.IsNullOrWhiteSpace(error) ? stderr : error);
    }
}
