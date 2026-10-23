using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MvcVisionSystem._1._Core
{
    public static class PythonModelSettingsValidator
    {
        public static PythonModelRuntimeState GetRuntimeState(
            PythonModelSettings settings,
            IEnumerable<string> supportedModels = null,
            IEnumerable<string> trainingModels = null,
            IEnumerable<string> detectionModels = null)
        {
            settings ??= new PythonModelSettings();
            PythonModelRuntimePathResolver.ApplyPathDefaults(settings);

            string projectRootPath = settings.ProjectRootPath?.Trim() ?? string.Empty;
            string clientScriptPath = settings.ClientScriptPath?.Trim() ?? string.Empty;
            bool projectRootExists = !string.IsNullOrWhiteSpace(projectRootPath) && Directory.Exists(projectRootPath);
            bool clientScriptExists = !string.IsNullOrWhiteSpace(clientScriptPath) && File.Exists(clientScriptPath);
            if (!projectRootExists || !clientScriptExists)
            {
                string detail = "\ub77c\ubca8\ub9c1\uc740 \uacc4\uc18d\ud560 \uc218 \uc788\uc2b5\ub2c8\ub2e4. \ud559\uc2b5\u00b7\ud604\uc7ac \uc774\ubbf8\uc9c0 \ucd94\ub860\uc5d0 \uc0ac\uc6a9\ud560 \uc2e4\ud589 \ud658\uacbd\uc744 \uc124\uce58\ud558\uac70\ub098 \uae30\uc874 \uacbd\ub85c\ub97c \uc5f0\uacb0\ud558\uc138\uc694.";
                return new PythonModelRuntimeState(
                    PythonModelRuntimeStateKind.NotInstalled,
                    canRunTraining: false,
                    canRunInference: false,
                    "\ub77c\ubca8\ub9c1 \uac00\ub2a5 / \uc2e4\ud589 \ud658\uacbd \ubbf8\uc124\uce58",
                    detail,
                    "\uc2e4\ud589 \ud658\uacbd \uc124\uce58\u00b7\uacbd\ub85c \uc5f0\uacb0 \ud544\uc694");
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
                    "\ub77c\ubca8\ub9c1 \uac00\ub2a5 / \ubaa8\ub378 \uae30\ub2a5 \uc900\ube44\ub428",
                    "\ud559\uc2b5\u00b7\ud604\uc7ac \ucd94\ub860 \uac00\ub2a5",
                    "\ud559\uc2b5\u00b7\ud604\uc7ac \ucd94\ub860 \uac00\ub2a5");
            }

            if (!canRunTraining && canRunInference)
            {
                return new PythonModelRuntimeState(
                    PythonModelRuntimeStateKind.Incomplete,
                    canRunTraining: false,
                    canRunInference: true,
                    "\ub77c\ubca8\ub9c1 \uac00\ub2a5 / \ud604\uc7ac \ucd94\ub860 \uac00\ub2a5",
                    string.IsNullOrWhiteSpace(adapterSupport.DetailText)
                        ? "\ud604\uc7ac \uc774\ubbf8\uc9c0 \ucd94\ub860\uc740 \uac00\ub2a5\ud558\uba70 \ud559\uc2b5\uc740 \ubcc4\ub3c4 \uc5f0\uacb0\uc774 \ud544\uc694\ud569\ub2c8\ub2e4."
                        : adapterSupport.DetailText,
                    string.IsNullOrWhiteSpace(adapterSupport.NextActionText)
                        ? "\ud559\uc2b5\ud558\ub824\uba74 \ud559\uc2b5 \uc9c0\uc6d0 \uc2e4\ud589 \ud658\uacbd\uc744 \uc5f0\uacb0\ud558\uc138\uc694."
                        : adapterSupport.NextActionText);
            }

            string firstIssue = inferenceValidation.Errors.FirstOrDefault()
                ?? trainingValidation.Errors.FirstOrDefault()
                ?? inferenceValidation.Warnings.FirstOrDefault()
                ?? trainingValidation.Warnings.FirstOrDefault()
                ?? "\uBAA8\uB378 \uC124\uC815\uC744 \uD655\uC778\uD558\uC138\uC694";
            string action = canRunTraining && !canRunInference
                ? "학습은 가능하지만 현재 이미지 추론 준비는 완료되지 않았습니다. 모델 설정과 실행 환경의 추론 지원을 확인하세요."
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
            PythonModelRuntimePathResolver.ApplyDefaults(settings);

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
                errors.Add($"추론 응답 제한 시간은 1초부터 600초 사이여야 합니다: {configuredDetectionTimeoutSeconds}");
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
