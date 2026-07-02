using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MvcVisionSystem._1._Core
{
    public sealed class PythonModelRuntimeSelfTestItem
    {
        public PythonModelRuntimeSelfTestItem(string labelText, string statusText, string detailText, bool isPassed, bool isWarning)
        {
            LabelText = labelText ?? string.Empty;
            StatusText = statusText ?? string.Empty;
            DetailText = detailText ?? string.Empty;
            IsPassed = isPassed;
            IsWarning = isWarning;
        }

        public string LabelText { get; }
        public string StatusText { get; }
        public string DetailText { get; }
        public bool IsPassed { get; }
        public bool IsWarning { get; }
    }

    public sealed class PythonModelRuntimeSelfTestReport
    {
        public PythonModelRuntimeSelfTestReport(
            string titleText,
            string summaryText,
            string detailText,
            IEnumerable<PythonModelRuntimeSelfTestItem> items,
            bool canTrain,
            bool canInspect)
        {
            TitleText = titleText ?? string.Empty;
            SummaryText = summaryText ?? string.Empty;
            DetailText = detailText ?? string.Empty;
            Items = (items ?? Enumerable.Empty<PythonModelRuntimeSelfTestItem>()).ToList();
            CanTrain = canTrain;
            CanInspect = canInspect;
        }

        public string TitleText { get; }
        public string SummaryText { get; }
        public string DetailText { get; }
        public IReadOnlyList<PythonModelRuntimeSelfTestItem> Items { get; }
        public bool CanTrain { get; }
        public bool CanInspect { get; }
    }

    public static class PythonModelRuntimeSelfTestService
    {
        public static PythonModelRuntimeSelfTestReport BuildReport(PythonModelSettings settings)
        {
            settings ??= new PythonModelSettings();
            PythonModelRuntimeState runtimeState = PythonModelSettingsValidator.GetRuntimeState(settings);
            var items = new List<PythonModelRuntimeSelfTestItem>
            {
                BuildDirectoryItem("\uD504\uB85C\uC81D\uD2B8", settings.ProjectRootPath),
                BuildPythonItem(settings),
                BuildFileItem("\uC2E4\uD589 \uC2A4\uD06C\uB9BD\uD2B8", settings.ClientScriptPath, isWarningWhenMissing: false),
                BuildFileItem("\uAC80\uC0AC \uBAA8\uB378", settings.WeightsPath, isWarningWhenMissing: true),
                BuildDirectoryItem("\uC774\uBBF8\uC9C0", settings.ImageRootPath, isWarningWhenMissing: true)
            };

            string modelRootPath = settings.GetModelRootPath();
            if (!string.IsNullOrWhiteSpace(modelRootPath)
                && !string.Equals(modelRootPath, settings.ProjectRootPath?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                items.Insert(1, BuildDirectoryItem("\uBAA8\uB378 \uB8E8\uD2B8", modelRootPath, isWarningWhenMissing: true));
            }

            if (UsesUltralytics(settings.ModelEngine))
            {
                int pythonIndex = items.FindIndex(item => string.Equals(item.LabelText, "Python", StringComparison.Ordinal));
                items.Insert(Math.Max(0, pythonIndex + 1), BuildUltralyticsPackageItem(settings));
            }

            items.Add(BuildExecutionSupportItem(settings));

            return new PythonModelRuntimeSelfTestReport(
                "\uC120\uD0DD \uC2E4\uD589\uAE30 \uC810\uAC80",
                runtimeState.SummaryText,
                FormatDetail(runtimeState),
                items,
                runtimeState.CanRunTraining,
                runtimeState.CanRunInference);
        }

        private static PythonModelRuntimeSelfTestItem BuildDirectoryItem(
            string labelText,
            string path,
            bool isWarningWhenMissing = false)
        {
            string trimmed = path?.Trim() ?? string.Empty;
            bool exists = !string.IsNullOrWhiteSpace(trimmed) && Directory.Exists(trimmed);
            return new PythonModelRuntimeSelfTestItem(
                labelText,
                exists ? "\uD655\uC778" : isWarningWhenMissing ? "\uD655\uC778 \uD544\uC694" : "\uD544\uC694",
                exists ? trimmed : FormatMissingDirectoryDetail(labelText, trimmed),
                exists,
                !exists && isWarningWhenMissing);
        }

        private static PythonModelRuntimeSelfTestItem BuildFileItem(
            string labelText,
            string path,
            bool isWarningWhenMissing)
        {
            string trimmed = path?.Trim() ?? string.Empty;
            bool exists = !string.IsNullOrWhiteSpace(trimmed) && File.Exists(trimmed);
            return new PythonModelRuntimeSelfTestItem(
                labelText,
                exists ? "\uD655\uC778" : isWarningWhenMissing ? "\uD655\uC778 \uD544\uC694" : "\uD544\uC694",
                exists ? trimmed : FormatMissingFileDetail(labelText, trimmed),
                exists,
                !exists && isWarningWhenMissing);
        }

        private static PythonModelRuntimeSelfTestItem BuildPythonItem(PythonModelSettings settings)
        {
            string pythonExecutable = PythonModelSettingsValidator.ResolvePythonExecutable(settings);
            bool looksLikePath = PythonModelSettingsValidator.LooksLikePath(pythonExecutable);
            bool exists = looksLikePath && File.Exists(pythonExecutable);
            if (!looksLikePath && !string.IsNullOrWhiteSpace(pythonExecutable))
            {
                return new PythonModelRuntimeSelfTestItem(
                    "Python",
                    "\uBA85\uB839 \uD655\uC778 \uD544\uC694",
                    pythonExecutable,
                    isPassed: false,
                    isWarning: true);
            }

            return new PythonModelRuntimeSelfTestItem(
                "Python",
                exists ? "\uD655\uC778" : "\uD544\uC694",
                exists ? pythonExecutable : FormatMissingPythonDetail(pythonExecutable),
                exists,
                isWarning: false);
        }

        private static PythonModelRuntimeSelfTestItem BuildExecutionSupportItem(PythonModelSettings settings)
        {
            PythonModelRuntimeAdapterSupport support = PythonModelRuntimeAdapterSupportService.Build(settings);
            return new PythonModelRuntimeSelfTestItem(
                "\uC2E4\uD589 \uC5F0\uACB0",
                support.IsExecutionSupported ? "\uD655\uC778" : "\uC5F0\uACB0 \uD544\uC694",
                support.DetailText,
                support.IsExecutionSupported,
                isWarning: false);
        }

        private static PythonModelRuntimeSelfTestItem BuildUltralyticsPackageItem(PythonModelSettings settings)
        {
            string pythonExecutable = PythonModelSettingsValidator.ResolvePythonExecutable(settings);
            if (!TryResolveSitePackagesPath(pythonExecutable, out string sitePackagesPath))
            {
                return new PythonModelRuntimeSelfTestItem(
                    "Ultralytics",
                    "\uD655\uC778 \uD544\uC694",
                    "venv Python \uACBD\uB85C\uB97C \uC5F0\uACB0\uD558\uBA74 ultralytics \uD328\uD0A4\uC9C0\uB97C \uD655\uC778\uD569\uB2C8\uB2E4.",
                    isPassed: false,
                    isWarning: true);
            }

            bool installed = Directory.Exists(Path.Combine(sitePackagesPath, "ultralytics"))
                || EnumerateDirectoriesSafe(sitePackagesPath, "ultralytics-*.dist-info").Any();
            return new PythonModelRuntimeSelfTestItem(
                "Ultralytics",
                installed ? "\uD655\uC778" : "\uC124\uCE58 \uD544\uC694",
                installed ? sitePackagesPath : $"ultralytics \uD328\uD0A4\uC9C0 \uC5C6\uC74C: {sitePackagesPath} / \uC124\uCE58 \uC2E4\uD589 \uBC84\uD2BC\uC73C\uB85C \uC124\uCE58\uD55C \uB4A4 \uB2E4\uC2DC \uC810\uAC80\uD558\uC138\uC694.",
                installed,
                isWarning: false);
        }

        private static string FormatMissingDirectoryDetail(string labelText, string path)
        {
            string prefix = string.IsNullOrWhiteSpace(path)
                ? "\uACBD\uB85C \uBBF8\uC124\uC815"
                : $"\uCC3E\uC744 \uC218 \uC5C6\uC74C: {path}";
            return labelText switch
            {
                "\uD504\uB85C\uC81D\uD2B8" => $"{prefix} / \uBAA8\uB378 \uC2E4\uD589\uAE30 \uD504\uB85C\uD544\uC5D0\uC11C YOLO \uD504\uB85C\uC81D\uD2B8 \uD3F4\uB354\uB97C \uC5F0\uACB0\uD558\uC138\uC694.",
                "\uBAA8\uB378 \uB8E8\uD2B8" => $"{prefix} / YOLOv5 \uD3F4\uB354 \uC5F0\uACB0 \uB610\uB294 \uBAA8\uB378 \uB8E8\uD2B8 \uACBD\uB85C\uB97C \uD655\uC778\uD558\uC138\uC694.",
                "\uC774\uBBF8\uC9C0" => $"{prefix} / \uAC80\uC0AC\uD560 \uC774\uBBF8\uC9C0 \uD3F4\uB354\uB97C \uC120\uD0DD\uD558\uC138\uC694. \uB77C\uBCA8\uB9C1\uC740 \uACC4\uC18D\uD560 \uC218 \uC788\uC9C0\uB9CC \uD604\uC7AC \uAC80\uC0AC\uC5D0\uB294 \uC774\uBBF8\uC9C0 \uD3F4\uB354\uAC00 \uD544\uC694\uD569\uB2C8\uB2E4.",
                _ => $"{prefix} / \uACBD\uB85C\uB97C \uB2E4\uC2DC \uC120\uD0DD\uD558\uC138\uC694."
            };
        }

        private static string FormatMissingFileDetail(string labelText, string path)
        {
            string prefix = string.IsNullOrWhiteSpace(path)
                ? "\uACBD\uB85C \uBBF8\uC124\uC815"
                : $"\uCC3E\uC744 \uC218 \uC5C6\uC74C: {path}";
            return labelText switch
            {
                "\uC2E4\uD589 \uC2A4\uD06C\uB9BD\uD2B8" => $"{prefix} / \uBAA8\uB378 worker\uB97C \uC2E4\uD589\uD560 \uC2A4\uD06C\uB9BD\uD2B8 \uACBD\uB85C\uB97C \uC5F0\uACB0\uD558\uC138\uC694.",
                "\uAC80\uC0AC \uBAA8\uB378" => $"{prefix} / \uD604\uC7AC \uAC80\uC0AC\uB294 \uBAA8\uB378 \uD30C\uC77C\uC774 \uD544\uC694\uD569\uB2C8\uB2E4. \uD559\uC2B5 \uC644\uB8CC \uD6C4 \uAC80\uC0AC \uBAA8\uB378\uB85C \uC800\uC7A5\uD558\uAC70\uB098 .pt \uD30C\uC77C\uC744 \uC120\uD0DD\uD558\uC138\uC694.",
                _ => $"{prefix} / \uD30C\uC77C \uACBD\uB85C\uB97C \uB2E4\uC2DC \uC120\uD0DD\uD558\uC138\uC694."
            };
        }

        private static string FormatMissingPythonDetail(string pythonExecutable)
        {
            return string.IsNullOrWhiteSpace(pythonExecutable)
                ? "\uACBD\uB85C \uBBF8\uC124\uC815 / Python \uC2E4\uD589 \uD30C\uC77C \uB610\uB294 venv Scripts \uD3F4\uB354\uB97C \uC5F0\uACB0\uD558\uC138\uC694."
                : $"\uCC3E\uC744 \uC218 \uC5C6\uC74C: {pythonExecutable} / Python \uC2E4\uD589 \uD30C\uC77C \uB610\uB294 venv Scripts \uD3F4\uB354\uB97C \uB2E4\uC2DC \uC120\uD0DD\uD558\uC138\uC694.";
        }

        private static bool TryResolveSitePackagesPath(string pythonExecutablePath, out string sitePackagesPath)
        {
            sitePackagesPath = string.Empty;
            string trimmed = pythonExecutablePath?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return false;
            }

            string scriptsPath = string.Empty;
            if (File.Exists(trimmed))
            {
                scriptsPath = Path.GetDirectoryName(trimmed) ?? string.Empty;
            }
            else if (Directory.Exists(trimmed))
            {
                scriptsPath = string.Equals(Path.GetFileName(trimmed.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), "Scripts", StringComparison.OrdinalIgnoreCase)
                    ? trimmed
                    : Path.Combine(trimmed, "Scripts");
            }

            if (string.IsNullOrWhiteSpace(scriptsPath))
            {
                return false;
            }

            string venvRootPath = Directory.GetParent(scriptsPath)?.FullName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(venvRootPath))
            {
                return false;
            }

            string candidate = Path.Combine(venvRootPath, "Lib", "site-packages");
            if (!Directory.Exists(candidate))
            {
                return false;
            }

            sitePackagesPath = candidate;
            return true;
        }

        private static IEnumerable<string> EnumerateDirectoriesSafe(string path, string pattern)
        {
            try
            {
                return Directory.Exists(path)
                    ? Directory.EnumerateDirectories(path, pattern, SearchOption.TopDirectoryOnly).ToList()
                    : Enumerable.Empty<string>();
            }
            catch
            {
                return Enumerable.Empty<string>();
            }
        }

        private static bool UsesUltralytics(string engine)
        {
            string normalized = PythonModelSettings.NormalizeModelEngine(engine);
            return string.Equals(normalized, PythonModelSettings.EngineYoloV8, StringComparison.Ordinal)
                || string.Equals(normalized, PythonModelSettings.EngineYolo11, StringComparison.Ordinal);
        }

        private static string FormatDetail(PythonModelRuntimeState runtimeState)
        {
            if (runtimeState == null)
            {
                return "\uBAA8\uB378 \uC124\uC815\uC744 \uD655\uC778\uD558\uC138\uC694.";
            }

            if (runtimeState.CanRunTraining && runtimeState.CanRunInference)
            {
                return "\uD559\uC2B5\uACFC \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB450 \uC2E4\uD589 \uAC00\uB2A5\uD569\uB2C8\uB2E4.";
            }

            if (runtimeState.CanRunTraining)
            {
                return "\uD559\uC2B5\uC740 \uAC00\uB2A5\uD558\uACE0, \uAC80\uC0AC\uB294 \uBAA8\uB378 \uD30C\uC77C \uC120\uD0DD\uC774 \uD544\uC694\uD569\uB2C8\uB2E4.";
            }

            return runtimeState.NextActionText;
        }
    }
}
