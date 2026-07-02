using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MvcVisionSystem._1._Core
{
    public sealed class PythonModelRuntimeConnectionResult
    {
        public PythonModelRuntimeConnectionResult(
            PythonModelSettings settings,
            PythonModelRuntimeSelfTestReport selfTestReport,
            string summaryText,
            string detailText)
        {
            Settings = settings ?? new PythonModelSettings();
            SelfTestReport = selfTestReport ?? PythonModelRuntimeSelfTestService.BuildReport(Settings);
            SummaryText = summaryText ?? string.Empty;
            DetailText = detailText ?? string.Empty;
        }

        public PythonModelSettings Settings { get; }
        public PythonModelRuntimeSelfTestReport SelfTestReport { get; }
        public string SummaryText { get; }
        public string DetailText { get; }
    }

    public static class PythonModelRuntimeConnectionService
    {
        public static PythonModelRuntimeConnectionResult BuildYoloV5FolderConnection(
            PythonModelSettings currentSettings,
            string selectedFolderPath)
        {
            string projectRootPath = ResolveYoloV5ProjectRoot(selectedFolderPath);
            PythonModelSettings settings = Clone(currentSettings);
            settings.ModelEngine = PythonModelSettings.EngineYoloV5;
            settings.ProjectRootPath = projectRootPath;
            settings.ClientScriptPath = ResolveClientScriptPath(settings.ClientScriptPath, projectRootPath);
            settings.PythonExecutablePath = ResolvePythonExecutablePath(settings.PythonExecutablePath, projectRootPath);
            settings.WeightsPath = ResolveWeightsPath(settings.WeightsPath, projectRootPath);
            settings.ImageRootPath = ResolveImageRootPath(settings.ImageRootPath, projectRootPath);

            PythonModelRuntimeSelfTestReport report = PythonModelRuntimeSelfTestService.BuildReport(settings);
            string summary = report.CanTrain
                ? "\uD559\uC2B5 \uC2E4\uD589\uAE30 \uC5F0\uACB0 \uD655\uC778"
                : "YOLOv5 \uC2E4\uD589\uAE30 \uC5F0\uACB0 \uD655\uC778 \uD544\uC694";
            string detail = report.CanInspect
                ? "\uD559\uC2B5\uACFC \uD604\uC7AC \uAC80\uC0AC\uAC00 \uAC00\uB2A5\uD569\uB2C8\uB2E4. \uC800\uC7A5\uC744 \uB20C\uB7EC recipe\uC5D0 \uBC18\uC601\uD558\uC138\uC694."
                : report.CanTrain
                    ? "\uD559\uC2B5\uC740 \uAC00\uB2A5\uD558\uACE0 \uAC80\uC0AC \uBAA8\uB378\uC740 \uC120\uD0DD\uC774 \uD544\uC694\uD569\uB2C8\uB2E4. \uC800\uC7A5\uC744 \uB20C\uB7EC recipe\uC5D0 \uBC18\uC601\uD558\uC138\uC694."
                    : "\uC810\uAC80 \uBAA9\uB85D\uC758 \uD544\uC694 \uD56D\uBAA9\uC744 \uD655\uC778\uD558\uACE0 \uACBD\uB85C\uB97C \uB2E4\uC2DC \uC120\uD0DD\uD558\uC138\uC694.";

            return new PythonModelRuntimeConnectionResult(settings, report, summary, detail);
        }

        public static PythonModelRuntimeConnectionResult BuildUltralyticsPythonConnection(
            PythonModelSettings currentSettings,
            string engine,
            string selectedPythonOrVenvPath)
        {
            PythonModelSettings settings = Clone(currentSettings);
            settings.ModelEngine = PythonModelSettings.NormalizeModelEngine(engine);
            if (settings.ModelEngine != PythonModelSettings.EngineYoloV8
                && settings.ModelEngine != PythonModelSettings.EngineYolo11)
            {
                settings.ModelEngine = PythonModelSettings.EngineYolo11;
            }

            settings.PythonExecutablePath = ResolvePythonOrVenvPath(selectedPythonOrVenvPath);
            settings.ProjectRootPath = PythonModelRuntimeBundledWorkerService.ResolveUltralyticsWorkerRootPath();
            settings.ClientScriptPath = PythonModelRuntimeBundledWorkerService.ResolveUltralyticsWorkerScriptPath();

            PythonModelRuntimeSelfTestReport report = PythonModelRuntimeSelfTestService.BuildReport(settings);
            bool hasUltralytics = report.Items.Any(item => string.Equals(item.LabelText, "Ultralytics", StringComparison.Ordinal) && item.IsPassed);
            string summary = hasUltralytics
                ? "Ultralytics \uC2E4\uD589\uAE30 \uC5F0\uACB0 \uD655\uC778"
                : "Ultralytics \uC124\uCE58 \uD544\uC694";
            string detail = hasUltralytics
                ? "\uC120\uD0DD\uD55C Python\uC5D0 ultralytics \uD328\uD0A4\uC9C0\uAC00 \uD655\uC778\uB410\uC2B5\uB2C8\uB2E4. \uC800\uC7A5\uC744 \uB20C\uB7EC recipe\uC5D0 \uBC18\uC601\uD558\uC138\uC694."
                : "\uC120\uD0DD\uD55C Python\uC5D0 ultralytics \uD328\uD0A4\uC9C0\uAC00 \uBCF4\uC774\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4. \uB2E4\uC74C \uB2E8\uACC4\uC5D0\uC11C \uC124\uCE58 \uB610\uB294 \uB2E4\uB978 venv \uC5F0\uACB0\uC774 \uD544\uC694\uD569\uB2C8\uB2E4.";

            return new PythonModelRuntimeConnectionResult(settings, report, summary, detail);
        }

        private static PythonModelSettings Clone(PythonModelSettings source)
        {
            source ??= new PythonModelSettings();
            return new PythonModelSettings
            {
                PythonExecutablePath = source.PythonExecutablePath ?? string.Empty,
                ModelEngine = PythonModelSettings.NormalizeModelEngine(source.ModelEngine),
                ProjectRootPath = source.ProjectRootPath ?? string.Empty,
                ClientScriptPath = source.ClientScriptPath ?? string.Empty,
                WeightsPath = source.WeightsPath ?? string.Empty,
                ImageRootPath = source.ImageRootPath ?? string.Empty,
                MinimumDetectionConfidence = source.MinimumDetectionConfidence,
                MaximumDetectionCandidates = source.MaximumDetectionCandidates,
                InferenceImageSize = source.InferenceImageSize,
                DetectionTimeoutSeconds = source.DetectionTimeoutSeconds,
                AutoStartClient = source.AutoStartClient
            };
        }

        private static string ResolveYoloV5ProjectRoot(string selectedFolderPath)
        {
            string selected = selectedFolderPath?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(selected))
            {
                return selected;
            }

            string leaf = Path.GetFileName(selected.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string parent = Directory.GetParent(selected)?.FullName ?? string.Empty;
            if (string.Equals(leaf, "yolov5Master", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(parent))
            {
                return parent;
            }

            return selected;
        }

        private static string ResolveClientScriptPath(string currentPath, string projectRootPath)
        {
            string preferred = Path.Combine(projectRootPath ?? string.Empty, "labelling_tcp_client.py");
            if (File.Exists(preferred))
            {
                return preferred;
            }

            return string.IsNullOrWhiteSpace(currentPath) ? preferred : currentPath.Trim();
        }

        private static string ResolvePythonExecutablePath(string currentPath, string projectRootPath)
        {
            string venvPython = Path.Combine(projectRootPath ?? string.Empty, ".venv", "Scripts", "python.exe");
            if (File.Exists(venvPython))
            {
                return venvPython;
            }

            return currentPath?.Trim() ?? string.Empty;
        }

        private static string ResolvePythonOrVenvPath(string selectedPath)
        {
            string trimmed = selectedPath?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return trimmed;
            }

            if (Directory.Exists(trimmed))
            {
                string scriptsPython = string.Equals(Path.GetFileName(trimmed.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), "Scripts", StringComparison.OrdinalIgnoreCase)
                    ? Path.Combine(trimmed, "python.exe")
                    : Path.Combine(trimmed, "Scripts", "python.exe");
                if (File.Exists(scriptsPython))
                {
                    return scriptsPython;
                }
            }

            return trimmed;
        }

        private static string ResolveWeightsPath(string currentPath, string projectRootPath)
        {
            foreach (string candidate in EnumerateWeightCandidates(projectRootPath))
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            string preferred = Path.Combine(projectRootPath ?? string.Empty, "best.pt");
            return string.IsNullOrWhiteSpace(currentPath) ? preferred : currentPath.Trim();
        }

        private static IEnumerable<string> EnumerateWeightCandidates(string projectRootPath)
        {
            string rootBest = Path.Combine(projectRootPath ?? string.Empty, "best.pt");
            yield return rootBest;

            string runsRoot = Path.Combine(projectRootPath ?? string.Empty, "runs", "train");
            if (!Directory.Exists(runsRoot))
            {
                yield break;
            }

            foreach (string candidate in Directory.EnumerateFiles(runsRoot, "best.pt", SearchOption.AllDirectories)
                .OrderByDescending(path => File.GetLastWriteTimeUtc(path)))
            {
                yield return candidate;
            }
        }

        private static string ResolveImageRootPath(string currentPath, string projectRootPath)
        {
            string[] candidates =
            {
                Path.Combine(projectRootPath ?? string.Empty, "data", "train", "images"),
                Path.Combine(projectRootPath ?? string.Empty, "data", "valid", "images"),
                Path.Combine(projectRootPath ?? string.Empty, "data", "images")
            };

            foreach (string candidate in candidates)
            {
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            return currentPath?.Trim() ?? string.Empty;
        }
    }
}
