using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MvcVisionSystem._1._Core
{
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

            ValidateDirectory(settings.ProjectRootPath, "YOLO project root", errors);
            ValidateFile(settings.ClientScriptPath, "YOLO TCP client script", errors);

            string pythonExecutable = ResolvePythonExecutable(settings);
            if (LooksLikePath(pythonExecutable) && !File.Exists(pythonExecutable))
            {
                errors.Add($"Python executable was not found: {pythonExecutable}");
            }

            if (!string.IsNullOrWhiteSpace(settings.WeightsPath) && !File.Exists(settings.WeightsPath))
            {
                string message = $"YOLO weight file was not found: {settings.WeightsPath}";
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
                warnings.Add($"Image root path was not found: {settings.ImageRootPath}");
            }

            if (float.IsNaN(configuredMinimumConfidence)
                || float.IsInfinity(configuredMinimumConfidence)
                || configuredMinimumConfidence < 0F
                || configuredMinimumConfidence > 1F)
            {
                errors.Add($"Minimum detection confidence must be between 0 and 1: {configuredMinimumConfidence}");
            }

            if (configuredDetectionTimeoutSeconds < 1 || configuredDetectionTimeoutSeconds > 600)
            {
                errors.Add($"Detection timeout seconds must be between 1 and 600: {configuredDetectionTimeoutSeconds}");
            }

            if (configuredMaximumCandidates < 1 || configuredMaximumCandidates > 200)
            {
                errors.Add($"Maximum detection candidates must be between 1 and 200: {configuredMaximumCandidates}");
            }

            if (configuredInferenceImageSize < 64 || configuredInferenceImageSize > 2048)
            {
                errors.Add($"Inference image size must be between 64 and 2048: {configuredInferenceImageSize}");
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
                errors.Add($"{name} was not found: {path}");
            }
        }

        private static void ValidateFile(string path, string name, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                errors.Add($"{name} was not found: {path}");
            }
        }
    }
}
