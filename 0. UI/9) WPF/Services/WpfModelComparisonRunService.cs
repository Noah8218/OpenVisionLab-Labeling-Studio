using MvcVisionSystem._1._Core;
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
    public sealed class WpfModelComparisonRunService
    {
        private readonly string repositoryRoot;

        public WpfModelComparisonRunService(string repositoryRoot = "")
        {
            this.repositoryRoot = string.IsNullOrWhiteSpace(repositoryRoot)
                ? FindRepositoryRoot()
                : repositoryRoot;
        }

        public WpfModelComparisonRunRequest BuildRequest(
            CData data,
            WpfTrainingWeightsService trainingWeightsService,
            string task = "test",
            string baselineWeightsOverride = "")
        {
            data?.NormalizeOutputPaths();
            data?.NormalizeTrainingSettings();
            PythonModelSettings settings = data?.ProjectSettings?.PythonModel ?? new PythonModelSettings();
            TrainingSettings training = data?.GetTrainingSettings() ?? new TrainingSettings();
            string projectRoot = settings.ProjectRootPath?.Trim() ?? string.Empty;
            string candidateWeights = string.Empty;
            trainingWeightsService?.TryFindLatestTrainingWeights(projectRoot, data?.OutputRootPath ?? string.Empty, out candidateWeights);
            string baselineWeights = ResolveBaselineWeightsPath(settings, baselineWeightsOverride);

            return new WpfModelComparisonRunRequest
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

        public WpfModelComparisonRunRequest BuildYoloV5YoloV8DetectionRequest(
            CData data,
            string task = "test")
        {
            data?.NormalizeOutputPaths();
            data?.NormalizeTrainingSettings();
            PythonModelSettings settings = data?.ProjectSettings?.PythonModel ?? new PythonModelSettings();
            TrainingSettings training = data?.GetTrainingSettings() ?? new TrainingSettings();
            ModelRegistrySettings registry = data?.ProjectSettings?.ModelRegistry;
            EngineModelRuntime yoloV5 = ResolveEngineModelRuntime(
                settings,
                registry,
                PythonModelSettings.EngineYoloV5);
            EngineModelRuntime yoloV8 = ResolveEngineModelRuntime(
                settings,
                registry,
                PythonModelSettings.EngineYoloV8);

            return new WpfModelComparisonRunRequest
            {
                ScriptPath = Path.Combine(repositoryRoot, "scripts", "compare-yolo-models.ps1"),
                PythonExecutablePath = yoloV5.PythonExecutablePath,
                YoloProjectRootPath = yoloV5.ProjectRootPath,
                YoloSourceRootPath = yoloV5.SourceRootPath,
                DataYamlPath = data?.DataYamlFilePath ?? string.Empty,
                BaselineWeightsPath = yoloV5.WeightsPath,
                CandidateWeightsPath = yoloV8.WeightsPath,
                BaselineModelEngine = PythonModelSettings.EngineYoloV5,
                BaselinePythonExecutablePath = yoloV5.PythonExecutablePath,
                BaselineYoloSourceRootPath = yoloV5.SourceRootPath,
                CandidateModelEngine = PythonModelSettings.EngineYoloV8,
                CandidatePythonExecutablePath = yoloV8.PythonExecutablePath,
                CandidateYoloSourceRootPath = yoloV8.SourceRootPath,
                ImageSize = Math.Max(1, training.ImageSize),
                BatchSize = 1,
                Task = string.Equals(task, "val", StringComparison.OrdinalIgnoreCase) ? "val" : "test",
                ModelTask = "detect",
                UiConfidence = settings.MinimumDetectionConfidence,
                OutputDirectory = Path.Combine(repositoryRoot, "artifacts", "yolo-model-comparison"),
                IsEngineComparison = true
            };
        }

        public IReadOnlyList<string> ValidateRequest(WpfModelComparisonRunRequest request)
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
                request.IsEngineComparison ? "YOLOv5 \uAC1D\uCCB4\uD0D0\uC9C0 \uBAA8\uB378" : "\uAE30\uC874 \uBAA8\uB378 \uD30C\uC77C",
                errors);
            ValidateFile(
                request.CandidateWeightsPath,
                request.IsEngineComparison ? "YOLOv8 \uAC1D\uCCB4\uD0D0\uC9C0 \uBAA8\uB378" : "\uC0C8 \uBAA8\uB378 \uD30C\uC77C",
                errors);
            if (request.IsEngineComparison && !string.Equals(request.ModelTask, "detect", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("YOLOv5/YOLOv8 \uC5D4\uC9C4 \uBE44\uAD50\uB294 \uAC1D\uCCB4\uD0D0\uC9C0 \uB370\uC774\uD130\uC14B\uC5D0\uC11C\uB9CC \uC2E4\uD589\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.");
            }
            ValidateDifferentWeights(request, errors);
            ValidateDataYamlSplitImages(request, errors);
            if (request.ImageSize <= 0)
            {
                errors.Add("\uBE44\uAD50 \uC774\uBBF8\uC9C0 \uD06C\uAE30\uB294 0\uBCF4\uB2E4 \uCEE4\uC57C \uD569\uB2C8\uB2E4.");
            }

            if (request.BatchSize <= 0)
            {
                errors.Add("\uBE44\uAD50 \uBC30\uCE58 \uD06C\uAE30\uB294 0\uBCF4\uB2E4 \uCEE4\uC57C \uD569\uB2C8\uB2E4.");
            }

            return errors;
        }

        public async Task<WpfModelComparisonRunResult> RunAsync(WpfModelComparisonRunRequest request, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<string> errors = ValidateRequest(request);
            if (errors.Count > 0)
            {
                return WpfModelComparisonRunResult.Failed(string.Join(Environment.NewLine, errors), string.Empty, string.Empty);
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
            string summaryPath = TryFindLatestSummaryPath(request.OutputDirectory);
            return process.ExitCode == 0
                ? WpfModelComparisonRunResult.Success(summaryPath, stdout, stderr)
                : WpfModelComparisonRunResult.Failed(BuildFailureMessage(process.ExitCode, stderr, stdout), stdout, stderr);
        }

        public ProcessStartInfo CreateStartInfo(WpfModelComparisonRunRequest request)
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

        public IReadOnlyList<string> BuildPowerShellArguments(WpfModelComparisonRunRequest request)
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

        private static string ResolveModelTask(CData data)
        {
            return data?.ProjectSettings?.DatasetPurpose == LabelingDatasetPurpose.Segmentation
                ? "segment"
                : "detect";
        }

        private static string ResolveSegmentationPositiveClassName(CData data)
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

        private static void ValidateDifferentWeights(WpfModelComparisonRunRequest request, List<string> errors)
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

        private static void ValidateDataYamlSplitImages(WpfModelComparisonRunRequest request, List<string> errors)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.DataYamlPath) || !File.Exists(request.DataYamlPath))
            {
                return;
            }

            string task = string.Equals(request.Task, "val", StringComparison.OrdinalIgnoreCase) ? "val" : "test";
            Dictionary<string, string> values = ReadDataYamlScalarValues(request.DataYamlPath);
            if (!values.TryGetValue(task, out string splitPath) || string.IsNullOrWhiteSpace(splitPath))
            {
                errors.Add($"\uD559\uC2B5 \uC124\uC815\uC5D0 \uCD5C\uC885 \uAC80\uC99D \uC774\uBBF8\uC9C0 \uACBD\uB85C\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4. \uBAA8\uB378 \uBE44\uAD50 \uC804 {task} \uC774\uBBF8\uC9C0\uB97C 1\uC7A5 \uC774\uC0C1 \uD655\uBCF4\uD558\uC138\uC694.");
                return;
            }

            string yamlRootPath = values.TryGetValue("path", out string rootPath) ? rootPath : string.Empty;
            string resolved = ResolveDataYamlPath(request.DataYamlPath, yamlRootPath, splitPath);
            int imageCount = CountDataYamlImages(resolved);
            if (imageCount <= 0)
            {
                errors.Add($"\uD559\uC2B5 \uC124\uC815\uC758 {task} \uBD84\uD560\uC5D0 \uC774\uBBF8\uC9C0\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4: {resolved}");
                return;
            }

            int labelCount = CountDataYamlLabelFiles(resolved);
            if (labelCount <= 0)
            {
                errors.Add($"\uD559\uC2B5 \uC124\uC815\uC758 {task} \uBD84\uD560\uC5D0 \uC815\uB2F5 \uB77C\uBCA8 \uD30C\uC77C\uC774 \uC5C6\uC2B5\uB2C8\uB2E4: {resolved}");
                return;
            }

            if (string.Equals(request.ModelTask, "segment", StringComparison.OrdinalIgnoreCase)
                && CountDataYamlSegmentationLabelLines(resolved) <= 0)
            {
                errors.Add($"Model comparison needs at least one positive segmentation label line in the {task} split before YOLOv8 SEG validation: {resolved}");
            }
        }

        private static int CountDataYamlImages(string resolvedPath)
        {
            if (string.IsNullOrWhiteSpace(resolvedPath))
            {
                return 0;
            }

            if (Directory.Exists(resolvedPath))
            {
                return Directory
                    .EnumerateFiles(resolvedPath, "*.*", SearchOption.TopDirectoryOnly)
                    .Count(IsSupportedImagePath);
            }

            if (File.Exists(resolvedPath))
            {
                string directory = Path.GetDirectoryName(resolvedPath) ?? Directory.GetCurrentDirectory();
                return File
                    .ReadLines(resolvedPath)
                    .Select(line => RemoveYamlInlineComment(line).Trim())
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Select(line => ResolveListImagePath(directory, line))
                    .Count(path => File.Exists(path) && IsSupportedImagePath(path));
            }

            return 0;
        }

        private static int CountDataYamlLabelFiles(string resolvedPath)
        {
            if (string.IsNullOrWhiteSpace(resolvedPath))
            {
                return 0;
            }

            if (Directory.Exists(resolvedPath))
            {
                return Directory
                    .EnumerateFiles(resolvedPath, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(IsSupportedImagePath)
                    .Select(ResolveLabelPathFromImagePath)
                    .Count(File.Exists);
            }

            if (File.Exists(resolvedPath))
            {
                string directory = Path.GetDirectoryName(resolvedPath) ?? Directory.GetCurrentDirectory();
                return File
                    .ReadLines(resolvedPath)
                    .Select(line => RemoveYamlInlineComment(line).Trim())
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Select(line => ResolveListImagePath(directory, line))
                    .Select(ResolveLabelPathFromImagePath)
                    .Count(File.Exists);
            }

            return 0;
        }

        private static int CountDataYamlSegmentationLabelLines(string resolvedPath)
        {
            return EnumerateDataYamlLabelPaths(resolvedPath)
                .Where(File.Exists)
                .SelectMany(File.ReadLines)
                .Select(line => RemoveYamlInlineComment(line).Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Count(IsSegmentationLabelLine);
        }

        private static IEnumerable<string> EnumerateDataYamlLabelPaths(string resolvedPath)
        {
            if (string.IsNullOrWhiteSpace(resolvedPath))
            {
                yield break;
            }

            if (Directory.Exists(resolvedPath))
            {
                foreach (string labelPath in Directory
                    .EnumerateFiles(resolvedPath, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(IsSupportedImagePath)
                    .Select(ResolveLabelPathFromImagePath))
                {
                    yield return labelPath;
                }

                yield break;
            }

            if (!File.Exists(resolvedPath))
            {
                yield break;
            }

            string directory = Path.GetDirectoryName(resolvedPath) ?? Directory.GetCurrentDirectory();
            foreach (string labelPath in File
                .ReadLines(resolvedPath)
                .Select(line => RemoveYamlInlineComment(line).Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => ResolveLabelPathFromImagePath(ResolveListImagePath(directory, line))))
            {
                yield return labelPath;
            }
        }

        private static bool IsSegmentationLabelLine(string line)
        {
            string[] tokens = (line ?? string.Empty)
                .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 7 || tokens.Length % 2 == 0)
            {
                return false;
            }

            return tokens.All(token => double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out _));
        }

        private static string ResolveLabelsDirectoryFromImagesPath(string imagesPath)
        {
            string normalized = Path.GetFullPath(imagesPath ?? string.Empty)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string directoryName = Path.GetFileName(normalized);
            string parent = Path.GetDirectoryName(normalized) ?? string.Empty;
            if (string.Equals(directoryName, "images", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(parent))
            {
                return Path.Combine(parent, "labels");
            }

            return Path.Combine(normalized, "labels");
        }

        private static string ResolveLabelPathFromImagePath(string imagePath)
        {
            string directory = Path.GetDirectoryName(imagePath) ?? string.Empty;
            string labelsDirectory = ResolveLabelsDirectoryFromImagesPath(directory);
            return Path.Combine(labelsDirectory, Path.GetFileNameWithoutExtension(imagePath) + ".txt");
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

        private static Dictionary<string, string> ReadDataYamlScalarValues(string dataYamlPath)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string rawLine in File.ReadLines(dataYamlPath))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                int separatorIndex = line.IndexOf(':');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, separatorIndex).Trim();
                string value = StripYamlScalarValue(line.Substring(separatorIndex + 1).Trim());
                if (!string.IsNullOrWhiteSpace(key))
                {
                    values[key] = value;
                }
            }

            return values;
        }

        private static string StripYamlScalarValue(string value)
        {
            value = RemoveYamlInlineComment(value ?? string.Empty).Trim();
            if (value.Length >= 2
                && ((value[0] == '"' && value[value.Length - 1] == '"')
                    || (value[0] == '\'' && value[value.Length - 1] == '\'')))
            {
                value = value.Substring(1, value.Length - 2);
            }

            return value.Trim();
        }

        private static string RemoveYamlInlineComment(string value)
        {
            int commentIndex = value.IndexOf('#');
            return commentIndex >= 0 ? value.Substring(0, commentIndex) : value;
        }

        private static string ResolveDataYamlPath(string yamlFilePath, string yamlRootPath, string yamlPath)
        {
            string normalizedYamlPath = (yamlPath ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(normalizedYamlPath))
            {
                return Path.GetFullPath(normalizedYamlPath);
            }

            string yamlDirectory = Path.GetDirectoryName(yamlFilePath) ?? Directory.GetCurrentDirectory();
            string root = string.IsNullOrWhiteSpace(yamlRootPath)
                ? yamlDirectory
                : yamlRootPath.Replace('/', Path.DirectorySeparatorChar);
            if (!Path.IsPathRooted(root))
            {
                root = Path.Combine(yamlDirectory, root);
            }

            return Path.GetFullPath(Path.Combine(root, normalizedYamlPath));
        }

        private static string ResolveListImagePath(string listDirectory, string imagePath)
        {
            string normalized = (imagePath ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
            return Path.IsPathRooted(normalized)
                ? Path.GetFullPath(normalized)
                : Path.GetFullPath(Path.Combine(listDirectory, normalized));
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
        {
            foreach (string startPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                string current = startPath;
                while (!string.IsNullOrWhiteSpace(current))
                {
                    if (File.Exists(Path.Combine(current, "MvcVisionSystem.sln"))
                        || File.Exists(Path.Combine(current, "MvcVisionSystem.csproj")))
                    {
                        return current;
                    }

                    current = Directory.GetParent(current)?.FullName;
                }
            }

            return Directory.GetCurrentDirectory();
        }

        private sealed class EngineModelRuntime
        {
            public string ProjectRootPath { get; set; } = string.Empty;

            public string SourceRootPath { get; set; } = string.Empty;

            public string PythonExecutablePath { get; set; } = string.Empty;

            public string WeightsPath { get; set; } = string.Empty;
        }
    }

    public sealed class WpfModelComparisonRunRequest
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

    public sealed class WpfModelComparisonRunResult
    {
        private WpfModelComparisonRunResult(bool succeeded, string summaryPath, string output, string error)
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

        public static WpfModelComparisonRunResult Success(string summaryPath, string output, string error)
            => new WpfModelComparisonRunResult(true, summaryPath, output, error);

        public static WpfModelComparisonRunResult Failed(string error, string output, string stderr)
            => new WpfModelComparisonRunResult(false, string.Empty, output, string.IsNullOrWhiteSpace(error) ? stderr : error);
    }
}
