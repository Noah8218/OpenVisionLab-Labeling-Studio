using System;
using System.IO;
using System.Linq;

namespace MvcVisionSystem._1._Core
{
    public sealed class PythonModelRuntimeInstallPlan
    {
        public PythonModelRuntimeInstallPlan(
            string engine,
            string titleText,
            string summaryText,
            string detailText,
            string targetEnvironmentText,
            string commandText,
            string installCommandText,
            string uninstallCommandText,
            bool isVisible,
            bool canPreviewCommand,
            bool canRunInstall,
            bool canRunUninstall,
            bool requiresInstallation,
            bool isAlreadyInstalled)
        {
            Engine = engine ?? string.Empty;
            TitleText = titleText ?? string.Empty;
            SummaryText = summaryText ?? string.Empty;
            DetailText = detailText ?? string.Empty;
            TargetEnvironmentText = targetEnvironmentText ?? string.Empty;
            CommandText = commandText ?? string.Empty;
            InstallCommandText = installCommandText ?? string.Empty;
            UninstallCommandText = uninstallCommandText ?? string.Empty;
            IsVisible = isVisible;
            CanPreviewCommand = canPreviewCommand;
            CanRunInstall = canRunInstall;
            CanRunUninstall = canRunUninstall;
            RequiresInstallation = requiresInstallation;
            IsAlreadyInstalled = isAlreadyInstalled;
        }

        public string Engine { get; }
        public string TitleText { get; }
        public string SummaryText { get; }
        public string DetailText { get; }
        public string TargetEnvironmentText { get; }
        public string CommandText { get; }
        public string InstallCommandText { get; }
        public string UninstallCommandText { get; }
        public bool IsVisible { get; }
        public bool CanPreviewCommand { get; }
        public bool CanRunInstall { get; }
        public bool CanRunUninstall { get; }
        public bool RequiresInstallation { get; }
        public bool IsAlreadyInstalled { get; }
    }

    public static class PythonModelRuntimeInstallPlanService
    {
        public static PythonModelRuntimeInstallPlan BuildPlan(PythonModelSettings settings)
        {
            settings ??= new PythonModelSettings();
            string engine = PythonModelSettings.NormalizeModelEngine(settings.ModelEngine);
            if (!UsesUltralytics(engine))
            {
                return new PythonModelRuntimeInstallPlan(
                    engine,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    isVisible: false,
                    canPreviewCommand: false,
                    canRunInstall: false,
                    canRunUninstall: false,
                    requiresInstallation: false,
                    isAlreadyInstalled: false);
            }

            string displayName = string.Equals(engine, PythonModelSettings.EngineYoloV8, StringComparison.Ordinal)
                ? "YOLOv8"
                : "YOLO11";
            string pythonExecutable = PythonModelSettingsValidator.ResolvePythonExecutable(settings);
            string title = $"{displayName} Ultralytics \uC124\uCE58 \uC804 \uD655\uC778";
            if (!PythonModelSettingsValidator.LooksLikePath(pythonExecutable) || !File.Exists(pythonExecutable))
            {
                return new PythonModelRuntimeInstallPlan(
                    engine,
                    title,
                    "Python/venv \uC5F0\uACB0 \uD544\uC694",
                    "\uC5F0\uACB0 \uBC84\uD2BC\uC73C\uB85C \uC0AC\uC6A9\uD560 venv\uC758 Scripts\\python.exe\uB97C \uBA3C\uC800 \uC120\uD0DD\uD558\uC138\uC694.",
                    string.IsNullOrWhiteSpace(pythonExecutable) ? "\uBBF8\uC124\uC815" : pythonExecutable,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    isVisible: true,
                    canPreviewCommand: false,
                    canRunInstall: false,
                    canRunUninstall: false,
                    requiresInstallation: false,
                    isAlreadyInstalled: false);
            }

            if (!TryResolveVenvRootPath(pythonExecutable, out string venvRootPath))
            {
                return new PythonModelRuntimeInstallPlan(
                    engine,
                    title,
                    "venv \uD655\uC778 \uD544\uC694",
                    "\uD328\uD0A4\uC9C0 \uC124\uCE58 \uC704\uCE58\uB97C \uACE0\uC815\uD558\uB824\uBA74 venv\uC758 Scripts\\python.exe\uB97C \uC5F0\uACB0\uD574\uC57C \uD569\uB2C8\uB2E4.",
                    pythonExecutable,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    isVisible: true,
                    canPreviewCommand: false,
                    canRunInstall: false,
                    canRunUninstall: false,
                    requiresInstallation: false,
                    isAlreadyInstalled: false);
            }

            string sitePackagesPath = Path.Combine(venvRootPath, "Lib", "site-packages");
            bool installed = Directory.Exists(Path.Combine(sitePackagesPath, "ultralytics"))
                || EnumerateDirectoriesSafe(sitePackagesPath, "ultralytics-*.dist-info").Any();
            string installCommand = $"{Quote(pythonExecutable)} -m pip install --upgrade ultralytics";
            string uninstallCommand = $"{Quote(pythonExecutable)} -m pip uninstall -y ultralytics";
            string command = installed
                ? $"{Quote(pythonExecutable)} -m pip show ultralytics"
                : installCommand;
            return new PythonModelRuntimeInstallPlan(
                engine,
                title,
                installed ? "Ultralytics \uC124\uCE58 \uD655\uC778" : "Ultralytics \uC124\uCE58 \uD544\uC694",
                installed
                    ? "\uC774 venv\uC5D0 ultralytics \uD328\uD0A4\uC9C0\uAC00 \uC774\uBBF8 \uBCF4\uC785\uB2C8\uB2E4. \uD544\uC694\uD558\uBA74 \uB2E4\uC74C \uB2E8\uACC4\uC5D0\uC11C \uBC84\uC804 \uD655\uC778\uB9CC \uC218\uD589\uD558\uBA74 \uB429\uB2C8\uB2E4."
                    : "\uC544\uC9C1 \uC124\uCE58\uB294 \uC2E4\uD589\uD558\uC9C0 \uC54A\uC558\uC2B5\uB2C8\uB2E4. \uB2E4\uC74C \uB2E8\uACC4\uC5D0\uC11C \uC774 \uBA85\uB839\uACFC \uB300\uC0C1 venv\uB97C \uD655\uC778\uD55C \uB4A4 \uC2E4\uD589\uD558\uB294 action\uC73C\uB85C \uC5F0\uACB0\uD569\uB2C8\uB2E4.",
                venvRootPath,
                command,
                installCommand,
                uninstallCommand,
                isVisible: true,
                canPreviewCommand: true,
                canRunInstall: true,
                canRunUninstall: true,
                requiresInstallation: !installed,
                isAlreadyInstalled: installed);
        }

        private static bool TryResolveVenvRootPath(string pythonExecutablePath, out string venvRootPath)
        {
            venvRootPath = string.Empty;
            string trimmed = pythonExecutablePath?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmed) || !File.Exists(trimmed))
            {
                return false;
            }

            string scriptsPath = Path.GetDirectoryName(trimmed) ?? string.Empty;
            if (!string.Equals(
                    Path.GetFileName(scriptsPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                    "Scripts",
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string root = Directory.GetParent(scriptsPath)?.FullName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                return false;
            }

            venvRootPath = root;
            return true;
        }

        private static string Quote(string value)
        {
            string trimmed = value?.Trim() ?? string.Empty;
            return trimmed.Contains(' ') ? $"\"{trimmed}\"" : trimmed;
        }

        private static bool UsesUltralytics(string engine)
            => string.Equals(engine, PythonModelSettings.EngineYoloV8, StringComparison.Ordinal)
                || string.Equals(engine, PythonModelSettings.EngineYolo11, StringComparison.Ordinal);

        private static System.Collections.Generic.IEnumerable<string> EnumerateDirectoriesSafe(string path, string pattern)
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
    }
}
