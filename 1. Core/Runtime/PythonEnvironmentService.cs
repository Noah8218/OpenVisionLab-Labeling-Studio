using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem._1._Core
{
    public static class PythonEnvironmentService
    {
        private const int CheckTimeoutMilliseconds = 30000;
        private const int InstallTimeoutMilliseconds = 10 * 60 * 1000;
        private static readonly ExternalProcessRunner ProcessRunner = new ExternalProcessRunner();

        public static async Task<PythonEnvironmentCheckResult> CheckRequirementsAsync(
            PythonModelSettings settings,
            CancellationToken cancellationToken = default)
        {
            settings ??= new PythonModelSettings();
            PythonModelRuntimePathResolver.ApplyDefaults(settings);

            var errors = new List<string>();
            var warnings = new List<string>();
            PythonModelValidationResult validation = PythonModelSettingsValidator.Validate(settings, requireWeights: false);
            errors.AddRange(validation.Errors);
            warnings.AddRange(validation.Warnings);

            string pythonExecutablePath = PythonModelSettingsValidator.ResolvePythonExecutable(settings);
            string requirementsPath = settings.GetRequirementsPath();
            IReadOnlyList<string> requiredPackages = PythonRequirementsParser.ReadPackageNames(requirementsPath, warnings, errors);

            if (errors.Count > 0)
            {
                return BuildCheckResult(pythonExecutablePath, requirementsPath, requiredPackages, Array.Empty<string>(), errors, warnings);
            }

            if (requiredPackages.Count == 0)
            {
                warnings.Add($"requirements.txt에서 설치할 패키지 이름을 찾지 못했습니다: {requirementsPath}");
                return BuildCheckResult(pythonExecutablePath, requirementsPath, requiredPackages, Array.Empty<string>(), errors, warnings);
            }

            ProcessExecutionResult pipList = await RunPythonAsync(
                pythonExecutablePath,
                settings.ProjectRootPath,
                new[] { "-m", "pip", "list", "--format=json" },
                CheckTimeoutMilliseconds,
                cancellationToken).ConfigureAwait(false);

            if (pipList.ExitCode != 0)
            {
                errors.Add(FirstNonEmpty(pipList.Error, pipList.Output, "설치된 Python 패키지 목록을 확인하지 못했습니다."));
                return BuildCheckResult(pythonExecutablePath, requirementsPath, requiredPackages, Array.Empty<string>(), errors, warnings);
            }

            IReadOnlyCollection<string> installedPackages = PythonRequirementsParser.ParseInstalledPackageNames(pipList.Output);
            if (installedPackages.Count == 0)
            {
                errors.Add("Python 패키지 목록이 비어 있거나 읽을 수 없습니다.");
                return BuildCheckResult(pythonExecutablePath, requirementsPath, requiredPackages, Array.Empty<string>(), errors, warnings);
            }

            List<string> missing = requiredPackages
                .Where(packageName => !installedPackages.Contains(PythonRequirementsParser.NormalizePackageName(packageName)))
                .OrderBy(packageName => packageName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return BuildCheckResult(pythonExecutablePath, requirementsPath, requiredPackages, missing, errors, warnings);
        }

        public static async Task<PythonPackageInstallResult> InstallRequirementsAsync(
            PythonModelSettings settings,
            CancellationToken cancellationToken = default)
        {
            settings ??= new PythonModelSettings();
            PythonModelRuntimePathResolver.ApplyDefaults(settings);
            string pythonExecutablePath = PythonModelSettingsValidator.ResolvePythonExecutable(settings);
            string requirementsPath = settings.GetRequirementsPath();
            string commandLine = $"{pythonExecutablePath} -m pip install -r \"{requirementsPath}\"";

            if (string.IsNullOrWhiteSpace(requirementsPath) || !File.Exists(requirementsPath))
            {
                return new PythonPackageInstallResult
                {
                    Succeeded = false,
                    ExitCode = -1,
                    CommandLine = commandLine,
                    Error = $"requirements.txt 파일을 찾을 수 없습니다: {requirementsPath}"
                };
            }

            ProcessExecutionResult result = await RunPythonAsync(
                pythonExecutablePath,
                settings.ProjectRootPath,
                new[] { "-m", "pip", "install", "-r", requirementsPath },
                InstallTimeoutMilliseconds,
                cancellationToken).ConfigureAwait(false);

            return new PythonPackageInstallResult
            {
                Succeeded = result.ExitCode == 0,
                ExitCode = result.ExitCode,
                CommandLine = commandLine,
                Output = result.Output,
                Error = result.Error
            };
        }

        public static Task<PythonPackageInstallResult> InstallPackageAsync(
            PythonModelSettings settings,
            string packageName,
            CancellationToken cancellationToken = default)
            => RunPackageCommandAsync(
                settings,
                packageName,
                new[] { "-m", "pip", "install", "--upgrade", packageName?.Trim() ?? string.Empty },
                "install",
                cancellationToken);

        public static Task<PythonPackageInstallResult> UninstallPackageAsync(
            PythonModelSettings settings,
            string packageName,
            CancellationToken cancellationToken = default)
            => RunPackageCommandAsync(
                settings,
                packageName,
                new[] { "-m", "pip", "uninstall", "-y", packageName?.Trim() ?? string.Empty },
                "uninstall",
                cancellationToken);

        public static IReadOnlyList<string> ReadRequirementPackageNames(string requirementsPath)
        {
            var warnings = new List<string>();
            var errors = new List<string>();
            return PythonRequirementsParser.ReadPackageNames(requirementsPath, warnings, errors);
        }

        private static async Task<PythonPackageInstallResult> RunPackageCommandAsync(
            PythonModelSettings settings,
            string packageName,
            IReadOnlyList<string> arguments,
            string operationName,
            CancellationToken cancellationToken)
        {
            settings ??= new PythonModelSettings();
            PythonModelRuntimePathResolver.ApplyDefaults(settings);
            string trimmedPackage = packageName?.Trim() ?? string.Empty;
            string pythonExecutablePath = PythonModelSettingsValidator.ResolvePythonExecutable(settings);
            string commandLine = $"{pythonExecutablePath} {string.Join(" ", arguments ?? Array.Empty<string>())}";
            if (!PythonRequirementsParser.IsSafePackageName(trimmedPackage))
            {
                return new PythonPackageInstallResult
                {
                    Succeeded = false,
                    ExitCode = -1,
                    CommandLine = commandLine,
                    Error = $"Python 패키지 이름이 올바르지 않습니다({operationName}): {trimmedPackage}"
                };
            }

            ProcessExecutionResult result = await RunPythonAsync(
                pythonExecutablePath,
                settings.ProjectRootPath,
                arguments,
                InstallTimeoutMilliseconds,
                cancellationToken).ConfigureAwait(false);

            return new PythonPackageInstallResult
            {
                Succeeded = result.ExitCode == 0,
                ExitCode = result.ExitCode,
                CommandLine = commandLine,
                Output = result.Output,
                Error = result.Error
            };
        }

        private static PythonEnvironmentCheckResult BuildCheckResult(
            string pythonExecutablePath,
            string requirementsPath,
            IReadOnlyList<string> requiredPackages,
            IReadOnlyList<string> missingPackages,
            IReadOnlyList<string> errors,
            IReadOnlyList<string> warnings)
        {
            return new PythonEnvironmentCheckResult
            {
                PythonExecutablePath = pythonExecutablePath ?? string.Empty,
                RequirementsPath = requirementsPath ?? string.Empty,
                RequiredPackages = requiredPackages ?? Array.Empty<string>(),
                MissingPackages = missingPackages ?? Array.Empty<string>(),
                Errors = errors?.ToList() ?? new List<string>(),
                Warnings = warnings?.ToList() ?? new List<string>()
            };
        }

        private static async Task<ProcessExecutionResult> RunPythonAsync(
            string pythonExecutablePath,
            string workingDirectory,
            IEnumerable<string> arguments,
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = pythonExecutablePath,
                WorkingDirectory = Directory.Exists(workingDirectory) ? workingDirectory : AppContext.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            foreach (string argument in arguments ?? Enumerable.Empty<string>())
            {
                startInfo.ArgumentList.Add(argument);
            }

            ExternalProcessRunResult result = await ProcessRunner
                .RunAsync(
                    startInfo,
                    TimeSpan.FromMilliseconds(timeoutMilliseconds),
                    cancellationToken)
                .ConfigureAwait(false);
            if (result.TimedOut)
            {
                return new ProcessExecutionResult(-1, result.Output, "Python 명령 시간이 초과되었습니다.");
            }

            if (result.Canceled)
            {
                return new ProcessExecutionResult(-1, result.Output, "Python 명령이 취소되었습니다.");
            }

            if (!result.Started && string.IsNullOrWhiteSpace(result.Error))
            {
                return new ProcessExecutionResult(-1, result.Output, "Python 프로세스를 시작하지 못했습니다.");
            }

            return new ProcessExecutionResult(result.ExitCode, result.Output, result.Error);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values?.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }

        private sealed class ProcessExecutionResult
        {
            public ProcessExecutionResult(int exitCode, string output, string error)
            {
                ExitCode = exitCode;
                Output = output ?? string.Empty;
                Error = error ?? string.Empty;
            }

            public int ExitCode { get; }

            public string Output { get; }

            public string Error { get; }
        }
    }
}
