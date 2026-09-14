using MvcVisionSystem._1._Core;
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
    public class ModelComparisonRunService
    {
        private readonly string repositoryRoot;
        private readonly ExternalProcessRunner processRunner;
        private readonly ModelComparisonDatasetPreflightService datasetPreflightService;

        public ModelComparisonRunService(
            string repositoryRoot = "",
            ExternalProcessRunner processRunner = null,
            ModelComparisonDatasetPreflightService datasetPreflightService = null)
        {
            this.repositoryRoot = string.IsNullOrWhiteSpace(repositoryRoot)
                ? FindRepositoryRoot()
                : repositoryRoot;
            this.processRunner = processRunner ?? new ExternalProcessRunner();
            this.datasetPreflightService = datasetPreflightService ?? new ModelComparisonDatasetPreflightService();
        }

        public ModelComparisonRunRequest BuildRequest(
            LabelingProjectData data,
            TrainingWeightsService trainingWeightsService,
            string task = "test",
            string baselineWeightsOverride = "")
        {
            data?.NormalizeOutputPaths();
            data?.NormalizeTrainingSettings();
            PythonModelSettings settings = data?.ProjectSettings?.PythonModel ?? new PythonModelSettings();
            PythonModelRuntimePathResolver.ApplyPathDefaults(settings);
            TrainingSettings training = data?.GetTrainingSettings() ?? new TrainingSettings();
            string projectRoot = settings.ProjectRootPath?.Trim() ?? string.Empty;
            string candidateWeights = string.Empty;
            trainingWeightsService?.TryFindLatestTrainingWeights(projectRoot, data?.OutputRootPath ?? string.Empty, out candidateWeights);
            string baselineWeights = ResolveBaselineWeightsPath(settings, baselineWeightsOverride);

            return new ModelComparisonRunRequest
            {
                ScriptPath = Path.Combine(repositoryRoot, "scripts", "compare-yolo-models.ps1"),
                PythonExecutablePath = PythonModelSettingsValidator.ResolvePythonExecutable(settings),
                YoloProjectRootPath = projectRoot,
                YoloSourceRootPath = ResolveYoloSourceRoot(projectRoot),
                DataYamlPath = data?.DataYamlFilePath ?? string.Empty,
                BaselineWeightsPath = baselineWeights,
                CandidateWeightsPath = candidateWeights,
                ImageSize = Math.Max(1, training.ImageSize),
                BatchSize = Math.Max(1, training.Batch),
                Task = string.Equals(task, "val", StringComparison.OrdinalIgnoreCase) ? "val" : "test",
                ModelTask = ResolveModelTask(data),
                SegmentationPositiveClassName = ResolveSegmentationPositiveClassName(data),
                UiConfidence = settings.MinimumDetectionConfidence,
                OutputDirectory = Path.Combine(repositoryRoot, "artifacts", "yolo-model-comparison")
            };
        }

        public ModelComparisonRunRequest BuildYoloV5YoloV8DetectionRequest(
            LabelingProjectData data,
            string task = "")
            => BuildYoloDetectionEngineRequest(
                data,
                PythonModelSettings.EngineYoloV5,
                PythonModelSettings.EngineYoloV8,
                task);

        public ModelComparisonRunRequest BuildYoloV8Yolo11DetectionRequest(
            LabelingProjectData data,
            string task = "")
            => BuildYoloDetectionEngineRequest(
                data,
                PythonModelSettings.EngineYoloV8,
                PythonModelSettings.EngineYolo11,
                task);

        public ModelComparisonRunRequest BuildYoloDetectionEngineRequest(
            LabelingProjectData data,
            string baselineEngine,
            string candidateEngine,
            string task = "")
        {
            data?.NormalizeOutputPaths();
            data?.NormalizeTrainingSettings();
            PythonModelSettings settings = data?.ProjectSettings?.PythonModel ?? new PythonModelSettings();
            PythonModelRuntimePathResolver.ApplyPathDefaults(settings);
            TrainingSettings training = data?.GetTrainingSettings() ?? new TrainingSettings();
            ModelRegistrySettings registry = data?.ProjectSettings?.ModelRegistry;
            string normalizedBaselineEngine = PythonModelSettings.NormalizeModelEngine(baselineEngine);
            string normalizedCandidateEngine = PythonModelSettings.NormalizeModelEngine(candidateEngine);
            EngineModelRuntime baseline = ResolveEngineModelRuntime(
                settings,
                registry,
                normalizedBaselineEngine);
            EngineModelRuntime candidate = ResolveEngineModelRuntime(
                settings,
                registry,
                normalizedCandidateEngine);

            return new ModelComparisonRunRequest
            {
                ScriptPath = Path.Combine(repositoryRoot, "scripts", "compare-yolo-models.ps1"),
                PythonExecutablePath = baseline.PythonExecutablePath,
                YoloProjectRootPath = baseline.ProjectRootPath,
                YoloSourceRootPath = baseline.SourceRootPath,
                DataYamlPath = data?.DataYamlFilePath ?? string.Empty,
                BaselineWeightsPath = baseline.WeightsPath,
                CandidateWeightsPath = candidate.WeightsPath,
                BaselineModelEngine = normalizedBaselineEngine,
                BaselinePythonExecutablePath = baseline.PythonExecutablePath,
                BaselineYoloSourceRootPath = baseline.SourceRootPath,
                CandidateModelEngine = normalizedCandidateEngine,
                CandidatePythonExecutablePath = candidate.PythonExecutablePath,
                CandidateYoloSourceRootPath = candidate.SourceRootPath,
                ImageSize = Math.Max(1, training.ImageSize),
                BatchSize = 1,
                BenchmarkRepeatCount = 5,
                Task = datasetPreflightService.ResolveEngineComparisonTask(data?.DataYamlFilePath, task),
                ModelTask = "detect",
                UiConfidence = settings.MinimumDetectionConfidence,
                OutputDirectory = Path.Combine(repositoryRoot, "artifacts", "yolo-model-comparison"),
                IsEngineComparison = true
            };
        }

        public IReadOnlyList<string> ValidateRequest(ModelComparisonRunRequest request)
        {
            var errors = new List<string>();
            if (request == null)
            {
                errors.Add("\uBAA8\uB378 \uBE44\uAD50 \uC694\uCCAD \uC815\uBCF4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.");
                return errors;
            }

            ValidateFile(request.ScriptPath, "\uBAA8\uB378 \uBE44\uAD50 \uC2E4\uD589 \uC2A4\uD06C\uB9BD\uD2B8", errors);
            if (request.IsEngineComparison)
            {
                ValidateModelRuntime(
                    request.BaselinePythonExecutablePath,
                    request.BaselineYoloSourceRootPath,
                    request.BaselineModelEngine,
                    errors);
                ValidateModelRuntime(
                    request.CandidatePythonExecutablePath,
                    request.CandidateYoloSourceRootPath,
                    request.CandidateModelEngine,
                    errors);
            }
            else
            {
                ValidateFile(request.PythonExecutablePath, "\uCD94\uB860 \uC2E4\uD589 \uD30C\uC77C", errors);
                ValidateDirectory(request.YoloSourceRootPath, "\uBAA8\uB378 \uD504\uB85C\uC81D\uD2B8 \uD3F4\uB354", errors);
                ValidateYoloValidationRuntime(request.YoloSourceRootPath, errors);
            }
            ValidateFile(request.DataYamlPath, "\uD559\uC2B5 \uC124\uC815 \uD30C\uC77C", errors);
            ValidateFile(
                request.BaselineWeightsPath,
                request.IsEngineComparison ? PythonModelSettings.FormatModelEngineName(request.BaselineModelEngine) + " \uAC1D\uCCB4\uD0D0\uC9C0 \uBAA8\uB378" : "\uAE30\uC874 \uBAA8\uB378 \uD30C\uC77C",
                errors);
            ValidateFile(
                request.CandidateWeightsPath,
                request.IsEngineComparison ? PythonModelSettings.FormatModelEngineName(request.CandidateModelEngine) + " \uAC1D\uCCB4\uD0D0\uC9C0 \uBAA8\uB378" : "\uC0C8 \uBAA8\uB378 \uD30C\uC77C",
                errors);
            if (request.IsEngineComparison && !string.Equals(request.ModelTask, "detect", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("YOLO engine comparison can run only on object-detection datasets.");
            }
            ValidateDifferentWeights(request, errors);
            errors.AddRange(datasetPreflightService.ValidateDataYamlSplitImages(request));
            if (request.ImageSize <= 0)
            {
                errors.Add("\uBE44\uAD50 \uC774\uBBF8\uC9C0 \uD06C\uAE30\uB294 0\uBCF4\uB2E4 \uCEE4\uC57C \uD569\uB2C8\uB2E4.");
            }

            if (request.BatchSize <= 0)
            {
                errors.Add("\uBE44\uAD50 \uBC30\uCE58 \uD06C\uAE30\uB294 0\uBCF4\uB2E4 \uCEE4\uC57C \uD569\uB2C8\uB2E4.");
            }

            if (request.BenchmarkRepeatCount < 1 || request.BenchmarkRepeatCount > 10)
            {
                errors.Add("\uBE44\uAD50 \uBC18\uBCF5 \uD69F\uC218\uB294 1~10\uD68C\uC5EC\uC57C \uD569\uB2C8\uB2E4.");
            }

            return errors;
        }

        public async Task<ModelComparisonRunResult> RunAsync(ModelComparisonRunRequest request, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<string> errors = ValidateRequest(request);
            if (errors.Count > 0)
            {
                return ModelComparisonRunResult.Failed(string.Join(Environment.NewLine, errors), string.Empty, string.Empty);
            }

            ExternalProcessRunResult processResult = await processRunner
                .RunAsync(CreateStartInfo(request), Timeout.InfiniteTimeSpan, cancellationToken)
                .ConfigureAwait(false);
            string stdout = processResult.Output;
            string stderr = processResult.Error;
            if (processResult.Canceled)
            {
                return ModelComparisonRunResult.Failed(
                    "Model comparison was canceled.",
                    stdout,
                    stderr);
            }

            if (processResult.TimedOut)
            {
                return ModelComparisonRunResult.Failed(
                    "Model comparison timed out.",
                    stdout,
                    stderr);
            }

            string summaryPath = TryFindLatestSummaryPath(request.OutputDirectory);
            return processResult.ExitCode == 0
                ? ModelComparisonRunResult.Success(summaryPath, stdout, stderr)
                : ModelComparisonRunResult.Failed(BuildFailureMessage(processResult.ExitCode, stderr, stdout), stdout, stderr);
        }

        public ProcessStartInfo CreateStartInfo(ModelComparisonRunRequest request)
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

        public IReadOnlyList<string> BuildPowerShellArguments(ModelComparisonRunRequest request)
        {
            var arguments = new List<string>
            {
                "-NoProfile",
                "-ExecutionPolicy",
                "Bypass",
                "-File",
                request.ScriptPath,
                "-PythonExe",
                request.PythonExecutablePath,
                "-YoloProjectRoot",
                request.YoloProjectRootPath,
                "-YoloSourceRoot",
                request.YoloSourceRootPath,
                "-DataYaml",
                request.DataYamlPath,
                "-BaselineWeights",
                request.BaselineWeightsPath,
                "-CandidateWeights",
                request.CandidateWeightsPath,
                "-ImageSize",
                request.ImageSize.ToString(CultureInfo.InvariantCulture),
                "-BatchSize",
                request.BatchSize.ToString(CultureInfo.InvariantCulture),
                "-BenchmarkRepeatCount",
                request.BenchmarkRepeatCount.ToString(CultureInfo.InvariantCulture),
                "-Task",
                request.Task,
                "-ModelTask",
                request.ModelTask,
                "-UiConfidence",
                request.UiConfidence.ToString(CultureInfo.InvariantCulture),
                "-OutputDirectory",
                request.OutputDirectory
            };

            if (!string.IsNullOrWhiteSpace(request.SegmentationPositiveClassName))
            {
                arguments.Add("-SegmentationPositiveClassName");
                arguments.Add(request.SegmentationPositiveClassName);
            }

            AddOptionalArgument(arguments, "-BaselinePythonExe", request.BaselinePythonExecutablePath);
            AddOptionalArgument(arguments, "-BaselineYoloSourceRoot", request.BaselineYoloSourceRootPath);
            AddOptionalArgument(arguments, "-BaselineEngine", request.BaselineModelEngine);
            AddOptionalArgument(arguments, "-CandidatePythonExe", request.CandidatePythonExecutablePath);
            AddOptionalArgument(arguments, "-CandidateYoloSourceRoot", request.CandidateYoloSourceRootPath);
            AddOptionalArgument(arguments, "-CandidateEngine", request.CandidateModelEngine);

            return arguments;
        }

        private static void AddOptionalArgument(List<string> arguments, string name, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            arguments.Add(name);
            arguments.Add(value);
        }

        private static string ResolveBaselineWeightsPath(PythonModelSettings settings, string baselineWeightsOverride = "")
        {
            string overridePath = baselineWeightsOverride?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(overridePath))
            {
                return overridePath;
            }

            string configured = settings?.WeightsPath?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured;
            }

            string projectRoot = settings?.ProjectRootPath?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(projectRoot) ? string.Empty : Path.Combine(projectRoot, "best.pt");
        }

        private static string ResolveYoloSourceRoot(string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                return string.Empty;
            }

            string directValPy = Path.Combine(projectRoot, "val.py");
            if (File.Exists(directValPy))
            {
                return projectRoot;
            }

            string nestedYoloV5 = Path.Combine(projectRoot, "yolov5Master");
            if (File.Exists(Path.Combine(nestedYoloV5, "val.py")))
            {
                return nestedYoloV5;
            }

            string nestedUltralytics = Path.Combine(projectRoot, "ultralyticsMaster");
            if (Directory.Exists(Path.Combine(nestedUltralytics, "ultralytics")))
            {
                return nestedUltralytics;
            }

            if (Directory.Exists(Path.Combine(projectRoot, "ultralytics")))
            {
                return projectRoot;
            }

            return nestedYoloV5;
        }

        private static EngineModelRuntime ResolveEngineModelRuntime(
            PythonModelSettings currentSettings,
            ModelRegistrySettings registry,
            string engine)
        {
            registry?.EnsureDefaults();
            string normalizedEngine = PythonModelSettings.NormalizeModelEngine(engine);
            Dictionary<string, ModelProfile> profiles = registry?.Profiles?
                .Where(profile => profile != null
                    && string.Equals(
                        PythonModelSettings.NormalizeModelEngine(profile.ModelEngine),
                        normalizedEngine,
                        StringComparison.Ordinal)
                    && string.Equals(
                        profile.DatasetPurpose,
                        LabelingDatasetPurpose.ObjectDetection.ToString(),
                        StringComparison.Ordinal))
                .GroupBy(profile => profile.ProfileId ?? string.Empty, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal)
                ?? new Dictionary<string, ModelProfile>(StringComparer.Ordinal);
            ModelCandidate candidate = registry?.Candidates?
                .Where(item => item != null
                    && profiles.ContainsKey(item.ProfileId ?? string.Empty)
                    && !string.Equals(item.Decision, ModelRegistryService.CandidateDecisionRejected, StringComparison.Ordinal)
                    && File.Exists(item.WeightsPath ?? string.Empty))
                .OrderByDescending(item => ParseUtc(item.LastSeenUtc))
                .FirstOrDefault();

            if (candidate != null && profiles.TryGetValue(candidate.ProfileId ?? string.Empty, out ModelProfile profile))
            {
                return BuildEngineModelRuntime(normalizedEngine, profile.ProjectRootPath, candidate.WeightsPath, currentSettings);
            }

            if (currentSettings != null
                && string.Equals(
                    PythonModelSettings.NormalizeModelEngine(currentSettings.ModelEngine),
                    normalizedEngine,
                    StringComparison.Ordinal))
            {
                return BuildEngineModelRuntime(
                    normalizedEngine,
                    currentSettings.ProjectRootPath,
                    currentSettings.WeightsPath,
                    currentSettings);
            }

            ModelProfile latestProfile = profiles.Values
                .OrderByDescending(profile => ParseUtc(profile.LastUsedUtc))
                .FirstOrDefault();
            return BuildEngineModelRuntime(normalizedEngine, latestProfile?.ProjectRootPath, string.Empty, currentSettings);
        }

        private static EngineModelRuntime BuildEngineModelRuntime(
            string engine,
            string projectRoot,
            string weightsPath,
            PythonModelSettings currentSettings)
        {
            string normalizedRoot = projectRoot?.Trim() ?? string.Empty;
            bool useCurrentPython = currentSettings != null
                && string.Equals(
                    PythonModelSettings.NormalizeModelEngine(currentSettings.ModelEngine),
                    engine,
                    StringComparison.Ordinal)
                && string.Equals(
                    currentSettings.ProjectRootPath?.Trim(),
                    normalizedRoot,
                    StringComparison.OrdinalIgnoreCase);
            string pythonPath = useCurrentPython
                ? PythonModelSettingsValidator.ResolvePythonExecutable(currentSettings)
                : string.IsNullOrWhiteSpace(normalizedRoot)
                    ? string.Empty
                    : Path.Combine(normalizedRoot, ".venv", "Scripts", "python.exe");
            return new EngineModelRuntime
            {
                ProjectRootPath = normalizedRoot,
                SourceRootPath = ResolveYoloSourceRoot(normalizedRoot),
                PythonExecutablePath = pythonPath,
                WeightsPath = weightsPath?.Trim() ?? string.Empty
            };
        }

        private static DateTime ParseUtc(string value)
        {
            return DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTime parsed)
                ? parsed
                : DateTime.MinValue;
        }

        private static string ResolveModelTask(LabelingProjectData data)
        {
            return data?.ProjectSettings?.DatasetPurpose == LabelingDatasetPurpose.Segmentation
                ? "segment"
                : "detect";
        }

        private static string ResolveSegmentationPositiveClassName(LabelingProjectData data)
        {
            if (data?.ProjectSettings?.DatasetPurpose != LabelingDatasetPurpose.Segmentation)
            {
                return string.Empty;
            }

            List<string> names = data.ClassNamedList?
                .Select(item => item?.Text?.Trim() ?? string.Empty)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToList() ?? new List<string>();
            string preferred = names.FirstOrDefault(name => string.Equals(name, "NG", StringComparison.OrdinalIgnoreCase))
                ?? names.FirstOrDefault(name => string.Equals(name, "Defect", StringComparison.OrdinalIgnoreCase));
            return preferred ?? (names.Count == 1 ? names[0] : string.Empty);
        }

        private static void ValidateYoloValidationRuntime(string sourceRootPath, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(sourceRootPath))
            {
                errors.Add("Model validation runtime not found: ");
                return;
            }

            if (File.Exists(Path.Combine(sourceRootPath, "val.py"))
                || Directory.Exists(Path.Combine(sourceRootPath, "ultralytics")))
            {
                return;
            }

            errors.Add($"Model validation runtime not found: {sourceRootPath}");
        }

        private static void ValidateModelRuntime(
            string pythonExecutablePath,
            string sourceRootPath,
            string engine,
            List<string> errors)
        {
            string label = string.IsNullOrWhiteSpace(engine) ? "YOLO" : engine.Trim();
            ValidateFile(pythonExecutablePath, $"{label} Python", errors);
            ValidateDirectory(sourceRootPath, $"{label} \uB85C\uCEEC \uC18C\uC2A4", errors);
            int errorCount = errors.Count;
            ValidateYoloValidationRuntime(sourceRootPath, errors);
            if (errors.Count > errorCount)
            {
                errors[errors.Count - 1] = $"{label} validation runtime not found: {sourceRootPath}";
            }
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

        private static void ValidateDifferentWeights(ModelComparisonRunRequest request, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(request?.BaselineWeightsPath)
                || string.IsNullOrWhiteSpace(request.CandidateWeightsPath))
            {
                return;
            }

            string baseline = Path.GetFullPath(request.BaselineWeightsPath);
            string candidate = Path.GetFullPath(request.CandidateWeightsPath);
            if (string.Equals(baseline, candidate, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("\uAE30\uC874 \uBAA8\uB378\uACFC \uC0C8 \uBAA8\uB378 \uD30C\uC77C\uC774 \uAC19\uC2B5\uB2C8\uB2E4. \uBE44\uAD50 \uC804 \uC0C8\uB85C \uD559\uC2B5\uD558\uAC70\uB098 \uB2E4\uB978 \uBAA8\uB378\uC744 \uC120\uD0DD\uD558\uC138\uC694.");
            }
        }

        private static string TryFindLatestSummaryPath(string outputDirectory)
        {
            if (string.IsNullOrWhiteSpace(outputDirectory) || !Directory.Exists(outputDirectory))
            {
                return string.Empty;
            }

            return Directory
                .EnumerateFiles(outputDirectory, "comparison-summary.json", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault() ?? string.Empty;
        }

        private static string BuildFailureMessage(int exitCode, string stderr, string stdout)
        {
            string detail = string.IsNullOrWhiteSpace(stderr) ? stdout : stderr;
            if (string.IsNullOrWhiteSpace(detail))
            {
                detail = "No process output.";
            }

            return $"Model comparison failed. ExitCode={exitCode}. {detail.Trim()}";
        }

        private static string FindRepositoryRoot()
            => RepositoryRootResolver.FindRepositoryRoot();

        private sealed class EngineModelRuntime
        {
            public string ProjectRootPath { get; set; } = string.Empty;

            public string SourceRootPath { get; set; } = string.Empty;

            public string PythonExecutablePath { get; set; } = string.Empty;

            public string WeightsPath { get; set; } = string.Empty;
        }
    }

    public class ModelComparisonRunRequest
    {
        public string ScriptPath { get; set; } = string.Empty;

        public string PythonExecutablePath { get; set; } = string.Empty;

        public string YoloProjectRootPath { get; set; } = string.Empty;

        public string YoloSourceRootPath { get; set; } = string.Empty;

        public string DataYamlPath { get; set; } = string.Empty;

        public string BaselineWeightsPath { get; set; } = string.Empty;

        public string CandidateWeightsPath { get; set; } = string.Empty;

        public int ImageSize { get; set; } = 320;

        public int BatchSize { get; set; } = 16;

        public int BenchmarkRepeatCount { get; set; } = 1;

        public string Task { get; set; } = "test";

        public string ModelTask { get; set; } = "detect";

        public string SegmentationPositiveClassName { get; set; } = string.Empty;

        public string BaselineModelEngine { get; set; } = string.Empty;

        public string BaselinePythonExecutablePath { get; set; } = string.Empty;

        public string BaselineYoloSourceRootPath { get; set; } = string.Empty;

        public string CandidateModelEngine { get; set; } = string.Empty;

        public string CandidatePythonExecutablePath { get; set; } = string.Empty;

        public string CandidateYoloSourceRootPath { get; set; } = string.Empty;

        public bool IsEngineComparison { get; set; }

        public double UiConfidence { get; set; } = 0.25D;

        public string OutputDirectory { get; set; } = string.Empty;
    }

    public class ModelComparisonRunResult
    {
        protected ModelComparisonRunResult(bool succeeded, string summaryPath, string output, string error)
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

        public static ModelComparisonRunResult Success(string summaryPath, string output, string error)
            => new ModelComparisonRunResult(true, summaryPath, output, error);

        public static ModelComparisonRunResult Failed(string error, string output, string stderr)
            => new ModelComparisonRunResult(false, string.Empty, output, string.IsNullOrWhiteSpace(error) ? stderr : error);
    }

    [Obsolete("Use ModelComparisonRunRequest.", false)]
    public sealed class WpfModelComparisonRunRequest : ModelComparisonRunRequest
    {
        internal static WpfModelComparisonRunRequest FromCanonical(ModelComparisonRunRequest source)
        {
            if (source == null)
            {
                return null;
            }

            return new WpfModelComparisonRunRequest
            {
                ScriptPath = source.ScriptPath,
                PythonExecutablePath = source.PythonExecutablePath,
                YoloProjectRootPath = source.YoloProjectRootPath,
                YoloSourceRootPath = source.YoloSourceRootPath,
                DataYamlPath = source.DataYamlPath,
                BaselineWeightsPath = source.BaselineWeightsPath,
                CandidateWeightsPath = source.CandidateWeightsPath,
                ImageSize = source.ImageSize,
                BatchSize = source.BatchSize,
                BenchmarkRepeatCount = source.BenchmarkRepeatCount,
                Task = source.Task,
                ModelTask = source.ModelTask,
                SegmentationPositiveClassName = source.SegmentationPositiveClassName,
                BaselineModelEngine = source.BaselineModelEngine,
                BaselinePythonExecutablePath = source.BaselinePythonExecutablePath,
                BaselineYoloSourceRootPath = source.BaselineYoloSourceRootPath,
                CandidateModelEngine = source.CandidateModelEngine,
                CandidatePythonExecutablePath = source.CandidatePythonExecutablePath,
                CandidateYoloSourceRootPath = source.CandidateYoloSourceRootPath,
                IsEngineComparison = source.IsEngineComparison,
                UiConfidence = source.UiConfidence,
                OutputDirectory = source.OutputDirectory
            };
        }
    }

    [Obsolete("Use ModelComparisonRunResult.", false)]
    public sealed class WpfModelComparisonRunResult : ModelComparisonRunResult
    {
        private WpfModelComparisonRunResult(bool succeeded, string summaryPath, string output, string error)
            : base(succeeded, summaryPath, output, error)
        {
        }

        internal static WpfModelComparisonRunResult FromCanonical(ModelComparisonRunResult source)
        {
            return source == null
                ? null
                : new WpfModelComparisonRunResult(source.Succeeded, source.SummaryPath, source.Output, source.Error);
        }

        public static new WpfModelComparisonRunResult Success(string summaryPath, string output, string error)
            => new WpfModelComparisonRunResult(true, summaryPath, output, error);

        public static new WpfModelComparisonRunResult Failed(string error, string output, string stderr)
            => new WpfModelComparisonRunResult(false, string.Empty, output, string.IsNullOrWhiteSpace(error) ? stderr : error);
    }

    [Obsolete("Use ModelComparisonRunService.", false)]
    public sealed class WpfModelComparisonRunService : ModelComparisonRunService
    {
        public WpfModelComparisonRunService(
            string repositoryRoot = "",
            ExternalProcessRunner processRunner = null)
            : base(repositoryRoot, processRunner)
        {
        }

        public new WpfModelComparisonRunRequest BuildRequest(
            LabelingProjectData data,
            TrainingWeightsService trainingWeightsService,
            string task = "test",
            string baselineWeightsOverride = "")
            => WpfModelComparisonRunRequest.FromCanonical(base.BuildRequest(data, trainingWeightsService, task, baselineWeightsOverride));

        public new WpfModelComparisonRunRequest BuildYoloV5YoloV8DetectionRequest(LabelingProjectData data, string task = "")
            => WpfModelComparisonRunRequest.FromCanonical(base.BuildYoloV5YoloV8DetectionRequest(data, task));

        public new WpfModelComparisonRunRequest BuildYoloV8Yolo11DetectionRequest(LabelingProjectData data, string task = "")
            => WpfModelComparisonRunRequest.FromCanonical(base.BuildYoloV8Yolo11DetectionRequest(data, task));

        public new WpfModelComparisonRunRequest BuildYoloDetectionEngineRequest(
            LabelingProjectData data,
            string baselineEngine,
            string candidateEngine,
            string task = "")
            => WpfModelComparisonRunRequest.FromCanonical(base.BuildYoloDetectionEngineRequest(data, baselineEngine, candidateEngine, task));

        public IReadOnlyList<string> ValidateRequest(WpfModelComparisonRunRequest request)
            => base.ValidateRequest(request);

        public async Task<WpfModelComparisonRunResult> RunAsync(
            WpfModelComparisonRunRequest request,
            CancellationToken cancellationToken = default)
            => WpfModelComparisonRunResult.FromCanonical(await base.RunAsync(request, cancellationToken).ConfigureAwait(false));

        public ProcessStartInfo CreateStartInfo(WpfModelComparisonRunRequest request)
            => base.CreateStartInfo(request);

        public IReadOnlyList<string> BuildPowerShellArguments(WpfModelComparisonRunRequest request)
            => base.BuildPowerShellArguments(request);
    }
}
