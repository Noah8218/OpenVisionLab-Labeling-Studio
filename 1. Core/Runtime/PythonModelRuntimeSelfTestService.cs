using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MvcVisionSystem._1._Core
{
    public static class PythonModelRuntimeSelfTestService
    {
        private static readonly string[] YoloV5RequiredRepositoryFiles =
        {
            "hubconf.py",
            "train.py",
            "detect.py",
            Path.Combine("models", "common.py")
        };

        public static PythonModelRuntimeRepositoryCheck CheckYoloV5Repository(string projectRootPath)
        {
            string repositoryRootPath = projectRootPath?.Trim() ?? string.Empty;
            var missingRelativePaths = new List<string>();
            foreach (string relativePath in YoloV5RequiredRepositoryFiles)
            {
                if (string.IsNullOrWhiteSpace(repositoryRootPath)
                    || !File.Exists(Path.Combine(repositoryRootPath, relativePath)))
                {
                    missingRelativePaths.Add(relativePath.Replace('\\', '/'));
                }
            }

            return new PythonModelRuntimeRepositoryCheck(
                repositoryRootPath,
                YoloV5RequiredRepositoryFiles.Select(path => path.Replace('\\', '/')),
                missingRelativePaths);
        }

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
            else if (UsesUnet(settings.ModelEngine))
            {
                int pythonIndex = items.FindIndex(item => string.Equals(item.LabelText, "Python", StringComparison.Ordinal));
                items.Insert(Math.Max(0, pythonIndex + 1), BuildUnetPackageItem(settings));
            }
            else if (UsesPatchCore(settings.ModelEngine))
            {
                int pythonIndex = items.FindIndex(item => string.Equals(item.LabelText, "Python", StringComparison.Ordinal));
                items.Insert(Math.Max(0, pythonIndex + 1), BuildPatchCorePackageItem(settings));
            }

            items.Add(BuildExecutionSupportItem(settings));

            return new PythonModelRuntimeSelfTestReport(
                "선택한 실행 환경 점검",
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
                installed ? sitePackagesPath : $"ultralytics \uD328\uD0A4\uC9C0\uB97C \uCC3E\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4: {sitePackagesPath} / \uBAA8\uB378 \uC124\uC815\uC5D0\uC11C \uC124\uCE58\uD55C \uB4A4 \uB2E4\uC2DC \uC810\uAC80\uD558\uC138\uC694.",
                installed,
                isWarning: false);
        }

        private static PythonModelRuntimeSelfTestItem BuildUnetPackageItem(PythonModelSettings settings)
        {
            string pythonExecutable = PythonModelSettingsValidator.ResolvePythonExecutable(settings);
            if (!TryResolveSitePackagesPath(pythonExecutable, out string sitePackagesPath))
            {
                return new PythonModelRuntimeSelfTestItem(
                    "PyTorch U-Net",
                    "\uD655\uC778 \uD544\uC694",
                    "torch와 Pillow를 점검하려면 C:\\Git\\unet용 Python venv를 연결하세요.",
                    isPassed: false,
                    isWarning: true);
            }

            bool installed = Directory.Exists(Path.Combine(sitePackagesPath, "torch"))
                && Directory.Exists(Path.Combine(sitePackagesPath, "PIL"));
            return new PythonModelRuntimeSelfTestItem(
                "PyTorch U-Net",
                installed ? "\uD655\uC778" : "\uC124\uCE58 \uD544\uC694",
                installed ? sitePackagesPath : $"torch 또는 Pillow 패키지가 없습니다: {sitePackagesPath}",
                installed,
                isWarning: false);
        }

        private static PythonModelRuntimeSelfTestItem BuildPatchCorePackageItem(PythonModelSettings settings)
        {
            string pythonExecutable = PythonModelSettingsValidator.ResolvePythonExecutable(settings);
            if (!TryResolveSitePackagesPath(pythonExecutable, out string sitePackagesPath))
            {
                return new PythonModelRuntimeSelfTestItem(
                    "PyTorch PatchCore",
                    "확인 필요",
                    "PatchCore는 torch, torchvision, numpy, Pillow가 설치된 Python이 필요합니다.",
                    isPassed: false,
                    isWarning: true);
            }

            bool installed = Directory.Exists(Path.Combine(sitePackagesPath, "torch"))
                && Directory.Exists(Path.Combine(sitePackagesPath, "torchvision"))
                && Directory.Exists(Path.Combine(sitePackagesPath, "numpy"))
                && Directory.Exists(Path.Combine(sitePackagesPath, "PIL"));
            return new PythonModelRuntimeSelfTestItem(
                "PyTorch PatchCore",
                installed ? "확인" : "설치 필요",
                installed ? sitePackagesPath : $"torch, torchvision, numpy 또는 Pillow가 없습니다: {sitePackagesPath}",
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
                "\uD504\uB85C\uC81D\uD2B8" => $"{prefix} / \uc2e4\ud589 \ud658\uacbd \uc124\uc815\uc5d0\uc11c YOLO \ud504\ub85c\uc81d\ud2b8 \ud3f4\ub354\ub97c \uc5f0\uacb0\ud558\uc138\uc694.",
                "\uBAA8\uB378 \uB8E8\uD2B8" => $"{prefix} / YOLOv5 \uD3F4\uB354 \uC5F0\uACB0 \uB610\uB294 \uBAA8\uB378 \uB8E8\uD2B8 \uACBD\uB85C\uB97C \uD655\uC778\uD558\uC138\uC694.",
                "\uC774\uBBF8\uC9C0" => $"{prefix} / 이미지 폴더를 선택하거나 추론할 이미지를 여세요. 라벨링은 계속할 수 있습니다.",
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
                "\uC2E4\uD589 \uC2A4\uD06C\uB9BD\uD2B8" => $"{prefix}\r\n\uBAA8\uB378 \uD1B5\uC2E0 \uC2A4\uD06C\uB9BD\uD2B8\uC758 \uACBD\uB85C\uB97C \uC5F0\uACB0\uD558\uC138\uC694.",
                "\uAC80\uC0AC \uBAA8\uB378" => $"{prefix} / \ud604\uc7ac \uc774\ubbf8\uc9c0 \ucd94\ub860\uc5d0 \ubaa8\ub378 \ud30c\uc77c\uc774 \ud544\uc694\ud569\ub2c8\ub2e4. \ud559\uc2b5 \ud6c4 \uac80\uc0ac \ubaa8\ub378\ub85c \uc9c0\uc815\ud558\uac70\ub098 .pt \ud30c\uc77c\uc744 \uc120\ud0dd\ud558\uc138\uc694.",
                _ => $"{prefix} / \uD30C\uC77C \uACBD\uB85C\uB97C \uB2E4\uC2DC \uC120\uD0DD\uD558\uC138\uC694."
            };
        }

        private static string FormatMissingPythonDetail(string pythonExecutable)
        {
            return string.IsNullOrWhiteSpace(pythonExecutable)
                ? "\uACBD\uB85C \uBBF8\uC124\uC815 / Python \uC2E4\uD589 \uD30C\uC77C \uB610\uB294 venv Scripts \uD3F4\uB354\uB97C \uC5F0\uACB0\uD558\uC138\uC694."
                : $"\uCC3E\uC744 \uC218 \uC5C6\uC74C: {pythonExecutable}\r\nPython \uC2E4\uD589 \uD30C\uC77C\uC774\uB098 venv Scripts \uD3F4\uB354\uB97C \uB2E4\uC2DC \uC120\uD0DD\uD558\uC138\uC694.";
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
                string pythonDirectory = Path.GetDirectoryName(trimmed) ?? string.Empty;
                if (!string.Equals(Path.GetFileName(pythonDirectory), "Scripts", StringComparison.OrdinalIgnoreCase))
                {
                    string systemSitePackages = Path.Combine(pythonDirectory, "Lib", "site-packages");
                    if (Directory.Exists(systemSitePackages))
                    {
                        sitePackagesPath = systemSitePackages;
                        return true;
                    }
                }

                scriptsPath = pythonDirectory;
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

        private static bool UsesUnet(string engine)
            => string.Equals(
                PythonModelSettings.NormalizeModelEngine(engine),
                PythonModelSettings.EngineUnet,
                StringComparison.Ordinal);

        private static bool UsesPatchCore(string engine)
            => string.Equals(
                PythonModelSettings.NormalizeModelEngine(engine),
                PythonModelSettings.EnginePatchCore,
                StringComparison.Ordinal);

        private static string FormatDetail(PythonModelRuntimeState runtimeState)
        {
            if (runtimeState == null)
            {
                return "\uBAA8\uB378 \uC124\uC815\uC744 \uD655\uC778\uD558\uC138\uC694.";
            }

            if (runtimeState.CanRunTraining && runtimeState.CanRunInference)
            {
                return "학습과 추론 모두 실행 가능합니다.";
            }

            if (runtimeState.CanRunTraining)
            {
                return "\uD559\uC2B5\uC740 \uAC00\uB2A5\uD558\uACE0, \uAC80\uC0AC\uB294 \uBAA8\uB378 \uD30C\uC77C \uC120\uD0DD\uC774 \uD544\uC694\uD569\uB2C8\uB2E4.";
            }

            return runtimeState.NextActionText;
        }
    }
}
