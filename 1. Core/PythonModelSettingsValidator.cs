using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MvcVisionSystem._1._Core
{
    public enum PythonModelRuntimeStateKind
    {
        NotInstalled,
        Incomplete,
        Ready
    }

    public sealed class PythonModelRuntimeState
    {
        public PythonModelRuntimeState(
            PythonModelRuntimeStateKind state,
            bool canRunTraining,
            bool canRunInference,
            string summaryText,
            string detailText,
            string nextActionText)
        {
            State = state;
            CanRunTraining = canRunTraining;
            CanRunInference = canRunInference;
            SummaryText = summaryText ?? string.Empty;
            DetailText = detailText ?? string.Empty;
            NextActionText = nextActionText ?? string.Empty;
        }

        public PythonModelRuntimeStateKind State { get; }
        public bool IsRuntimeInstalled => State != PythonModelRuntimeStateKind.NotInstalled;
        public bool CanRunTraining { get; }
        public bool CanRunInference { get; }
        public string SummaryText { get; }
        public string DetailText { get; }
        public string NextActionText { get; }
    }

    public sealed class PythonModelValidationResult
    {
        public PythonModelValidationResult(IEnumerable<string> errors, IEnumerable<string> warnings)
        {
            Errors = (errors ?? Enumerable.Empty<string>()).ToList();
            Warnings = (warnings ?? Enumerable.Empty<string>()).ToList();
        }

        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<string> Warnings { get; }
        public bool IsValid => Errors.Count == 0;
        public string Summary => string.Join(Environment.NewLine, Errors.Concat(Warnings));
    }

    public static class PythonModelSettingsValidator
    {
        public static PythonModelRuntimeState GetRuntimeState(
            PythonModelSettings settings,
            IEnumerable<string> supportedModels = null,
            IEnumerable<string> trainingModels = null,
            IEnumerable<string> detectionModels = null)
        {
            settings ??= new PythonModelSettings();

            string projectRootPath = settings.ProjectRootPath?.Trim() ?? string.Empty;
            string clientScriptPath = settings.ClientScriptPath?.Trim() ?? string.Empty;
            bool projectRootExists = !string.IsNullOrWhiteSpace(projectRootPath) && Directory.Exists(projectRootPath);
            bool clientScriptExists = !string.IsNullOrWhiteSpace(clientScriptPath) && File.Exists(clientScriptPath);
            if (!projectRootExists || !clientScriptExists)
            {
                string detail = "\uB77C\uBCA8\uB9C1\uC740 \uACC4\uC18D\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4. \uD559\uC2B5/\uD604\uC7AC \uAC80\uC0AC\uB97C \uC0AC\uC6A9\uD558\uB824\uBA74 \uBAA8\uB378 \uC2E4\uD589\uAE30\uB97C \uC124\uCE58\uD558\uAC70\uB098 \uAE30\uC874 \uACBD\uB85C\uB97C \uC5F0\uACB0\uD558\uC138\uC694.";
                return new PythonModelRuntimeState(
                    PythonModelRuntimeStateKind.NotInstalled,
                    canRunTraining: false,
                    canRunInference: false,
                    "\uB77C\uBCA8\uB9C1 \uAC00\uB2A5 / \uBAA8\uB378 \uC2E4\uD589\uAE30 \uBBF8\uC124\uCE58",
                    detail,
                    "\uBAA8\uB378 \uC2E4\uD589\uAE30 \uC124\uCE58 \uB610\uB294 \uACBD\uB85C \uC5F0\uACB0 \uD544\uC694");
            }

            PythonModelRuntimeAdapterSupport adapterSupport = PythonModelRuntimeAdapterSupportService.Build(
                settings,
                supportedModels,
                trainingModels,
                detectionModels);
            if (!adapterSupport.CanTrain && !adapterSupport.CanInspect)
            {
                return new PythonModelRuntimeState(
                    PythonModelRuntimeStateKind.Incomplete,
                    canRunTraining: false,
                    canRunInference: false,
                    adapterSupport.SummaryText,
                    adapterSupport.DetailText,
                    adapterSupport.NextActionText);
            }

            PythonModelValidationResult trainingValidation = Validate(settings, requireWeights: false);
            PythonModelValidationResult inferenceValidation = Validate(settings, requireWeights: true);
            bool canRunTraining = trainingValidation.IsValid && adapterSupport.CanTrain;
            bool canRunInference = inferenceValidation.IsValid && adapterSupport.CanInspect;
            if (canRunTraining && canRunInference)
            {
                return new PythonModelRuntimeState(
                    PythonModelRuntimeStateKind.Ready,
                    canRunTraining: true,
                    canRunInference: true,
                    "\uB77C\uBCA8\uB9C1 \uAC00\uB2A5 / \uBAA8\uB378 \uAE30\uB2A5 \uC900\uBE44 \uC644\uB8CC",
                    "\uD559\uC2B5/\uD604\uC7AC \uAC80\uC0AC \uC0AC\uC6A9 \uAC00\uB2A5",
                    "\uD559\uC2B5/\uD604\uC7AC \uAC80\uC0AC \uC0AC\uC6A9 \uAC00\uB2A5");
            }

            if (!canRunTraining && canRunInference)
            {
                return new PythonModelRuntimeState(
                    PythonModelRuntimeStateKind.Incomplete,
                    canRunTraining: false,
                    canRunInference: true,
                    "\uB77C\uBCA8\uB9C1 \uAC00\uB2A5 / \uD604\uC7AC \uAC80\uC0AC \uAC00\uB2A5",
                    string.IsNullOrWhiteSpace(adapterSupport.DetailText)
                        ? "\uD604\uC7AC \uAC80\uC0AC\uB294 \uAC00\uB2A5\uD558\uC9C0\uB9CC \uD559\uC2B5 \uC2E4\uD589\uC740 \uB530\uB85C \uC5F0\uACB0\uC774 \uD544\uC694\uD569\uB2C8\uB2E4."
                        : adapterSupport.DetailText,
                    string.IsNullOrWhiteSpace(adapterSupport.NextActionText)
                        ? "\uD559\uC2B5\uC774 \uD544\uC694\uD558\uBA74 \uD559\uC2B5 \uC9C0\uC6D0 \uC2E4\uD589\uAE30\uB97C \uC5F0\uACB0\uD558\uC138\uC694."
                        : adapterSupport.NextActionText);
            }

            string firstIssue = inferenceValidation.Errors.FirstOrDefault()
                ?? trainingValidation.Errors.FirstOrDefault()
                ?? inferenceValidation.Warnings.FirstOrDefault()
                ?? trainingValidation.Warnings.FirstOrDefault()
                ?? "\uBAA8\uB378 \uC124\uC815\uC744 \uD655\uC778\uD558\uC138\uC694";
            string action = canRunTraining && !canRunInference
                ? "\uBAA8\uB378 \uC2E4\uD589\uAE30\uB294 \uC788\uC9C0\uB9CC \uAC80\uC0AC \uBAA8\uB378 \uD30C\uC77C\uC774 \uC5C6\uC2B5\uB2C8\uB2E4. \uD559\uC2B5\uC744 \uBA3C\uC800 \uC2E4\uD589\uD558\uAC70\uB098 \uBAA8\uB378 \uD30C\uC77C\uC744 \uC120\uD0DD\uD558\uC138\uC694."
                : "\uBAA8\uB378 \uC124\uC815\uC744 \uD655\uC778\uD558\uC138\uC694";
            return new PythonModelRuntimeState(
                PythonModelRuntimeStateKind.Incomplete,
                canRunTraining,
                canRunInference,
                "\uB77C\uBCA8\uB9C1 \uAC00\uB2A5 / \uBAA8\uB378 \uC124\uC815 \uD655\uC778 \uD544\uC694",
                firstIssue,
                action);
        }

        public static PythonModelValidationResult Validate(PythonModelSettings settings, bool requireWeights)
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            float configuredMinimumConfidence = settings?.MinimumDetectionConfidence ?? 0.25F;
            int configuredMaximumCandidates = settings?.MaximumDetectionCandidates ?? 20;
            int configuredInferenceImageSize = settings?.InferenceImageSize ?? 320;
            int configuredDetectionTimeoutSeconds = settings?.DetectionTimeoutSeconds ?? 30;

            settings ??= new PythonModelSettings();
            settings.EnsureDefaults();

            ValidateDirectory(settings.ProjectRootPath, "YOLO 프로젝트 폴더", errors);
            ValidateFile(settings.ClientScriptPath, "YOLO TCP 클라이언트 스크립트", errors);

            string pythonExecutable = ResolvePythonExecutable(settings);
            if (LooksLikePath(pythonExecutable) && !File.Exists(pythonExecutable))
            {
                errors.Add($"Python 실행 파일을 찾을 수 없습니다: {pythonExecutable}");
            }

            if (!string.IsNullOrWhiteSpace(settings.WeightsPath) && !File.Exists(settings.WeightsPath))
            {
                string message = $"YOLO 가중치 파일을 찾을 수 없습니다: {settings.WeightsPath}";
                if (requireWeights)
                {
                    errors.Add(message);
                }
                else
                {
                    warnings.Add(message);
                }
            }

            if (!string.IsNullOrWhiteSpace(settings.ImageRootPath) && !Directory.Exists(settings.ImageRootPath))
            {
                warnings.Add($"이미지 폴더를 찾을 수 없습니다: {settings.ImageRootPath}");
            }

            if (float.IsNaN(configuredMinimumConfidence)
                || float.IsInfinity(configuredMinimumConfidence)
                || configuredMinimumConfidence < 0F
                || configuredMinimumConfidence > 1F)
            {
                errors.Add($"신뢰도는 0과 1 사이여야 합니다: {configuredMinimumConfidence}");
            }

            if (configuredDetectionTimeoutSeconds < 1 || configuredDetectionTimeoutSeconds > 600)
            {
                errors.Add($"검사 시간 제한은 1초부터 600초 사이여야 합니다: {configuredDetectionTimeoutSeconds}");
            }

            if (configuredMaximumCandidates < 1 || configuredMaximumCandidates > 200)
            {
                errors.Add($"최대 후보 수는 1부터 200 사이여야 합니다: {configuredMaximumCandidates}");
            }

            if (configuredInferenceImageSize < 64 || configuredInferenceImageSize > 2048)
            {
                errors.Add($"추론 이미지 크기는 64부터 2048 사이여야 합니다: {configuredInferenceImageSize}");
            }

            return new PythonModelValidationResult(errors, warnings);
        }

        public static string ResolvePythonExecutable(PythonModelSettings settings)
        {
            if (!string.IsNullOrWhiteSpace(settings?.PythonExecutablePath))
            {
                return settings.PythonExecutablePath.Trim();
            }

            string projectRootPath = settings?.ProjectRootPath?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(projectRootPath))
            {
                string venvPythonPath = Path.Combine(projectRootPath, ".venv", "Scripts", "python.exe");
                if (File.Exists(venvPythonPath))
                {
                    return venvPythonPath;
                }
            }

            string windowsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string pyLauncher = Path.Combine(windowsDirectory, "py.exe");
            if (File.Exists(pyLauncher))
            {
                return pyLauncher;
            }

            return "python";
        }

        public static bool LooksLikePath(string value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && (value.Contains(Path.DirectorySeparatorChar.ToString())
                    || value.Contains(Path.AltDirectorySeparatorChar.ToString())
                    || Path.IsPathRooted(value));
        }

        private static void ValidateDirectory(string path, string name, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                errors.Add($"{name}를 찾을 수 없습니다: {path}");
            }
        }

        private static void ValidateFile(string path, string name, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                errors.Add($"{name}을 찾을 수 없습니다: {path}");
            }
        }
    }
}
