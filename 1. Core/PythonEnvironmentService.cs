using Lib.Common;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem._1._Core
{
    public sealed class PythonEnvironmentCheckResult
    {
        public string PythonExecutablePath { get; set; } = string.Empty;

        public string RequirementsPath { get; set; } = string.Empty;

        public IReadOnlyList<string> RequiredPackages { get; set; } = Array.Empty<string>();

        public IReadOnlyList<string> MissingPackages { get; set; } = Array.Empty<string>();

        public IReadOnlyList<string> Errors { get; set; } = Array.Empty<string>();

        public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();

        public bool IsReady => Errors.Count == 0 && MissingPackages.Count == 0;

        public string Summary
        {
            get
            {
                if (Errors.Count > 0)
                {
                    return Errors[0];
                }

                if (MissingPackages.Count > 0)
                {
                    return $"Missing Python packages: {string.Join(", ", MissingPackages.Take(6))}";
                }

                return Warnings.Count > 0 ? Warnings[0] : "Python environment is ready.";
            }
        }
    }

    public sealed class PythonPackageInstallResult
    {
        public bool Succeeded { get; set; }

        public int ExitCode { get; set; }

        public string CommandLine { get; set; } = string.Empty;

        public string Output { get; set; } = string.Empty;

        public string Error { get; set; } = string.Empty;

        public string Summary => Succeeded
            ? "Python requirements installed successfully."
            : !string.IsNullOrWhiteSpace(Error) ? Error : Output;
    }

    public static class PythonEnvironmentService
    {
        private const int CheckTimeoutMilliseconds = 30000;
        private const int InstallTimeoutMilliseconds = 10 * 60 * 1000;

        public static async Task<PythonEnvironmentCheckResult> CheckRequirementsAsync(
            PythonModelSettings settings,
            CancellationToken cancellationToken = default)
        {
            settings ??= new PythonModelSettings();
            settings.EnsureDefaults();

            var errors = new List<string>();
            var warnings = new List<string>();
            PythonModelValidationResult validation = PythonModelSettingsValidator.Validate(settings, requireWeights: false);
            errors.AddRange(validation.Errors);
            warnings.AddRange(validation.Warnings);

            string pythonExecutablePath = PythonModelSettingsValidator.ResolvePythonExecutable(settings);
            string requirementsPath = settings.GetRequirementsPath();
            IReadOnlyList<string> requiredPackages = ReadRequirementPackageNames(requirementsPath, warnings, errors);

            if (errors.Count > 0)
            {
                return BuildCheckResult(pythonExecutablePath, requirementsPath, requiredPackages, Array.Empty<string>(), errors, warnings);
            }

            if (requiredPackages.Count == 0)
            {
                warnings.Add($"No package names were found in requirements.txt: {requirementsPath}");
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
                errors.Add(FirstNonEmpty(pipList.Error, pipList.Output, "Could not inspect installed Python packages."));
                return BuildCheckResult(pythonExecutablePath, requirementsPath, requiredPackages, Array.Empty<string>(), errors, warnings);
            }

            IReadOnlyCollection<string> installedPackages = ParseInstalledPackageNames(pipList.Output);
            if (installedPackages.Count == 0)
            {
                errors.Add("Python package list was empty or unreadable.");
                return BuildCheckResult(pythonExecutablePath, requirementsPath, requiredPackages, Array.Empty<string>(), errors, warnings);
            }

            List<string> missing = requiredPackages
                .Where(packageName => !installedPackages.Contains(NormalizePackageName(packageName)))
                .OrderBy(packageName => packageName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return BuildCheckResult(pythonExecutablePath, requirementsPath, requiredPackages, missing, errors, warnings);
        }

        public static async Task<PythonPackageInstallResult> InstallRequirementsAsync(
            PythonModelSettings settings,
            CancellationToken cancellationToken = default)
        {
            settings ??= new PythonModelSettings();
            settings.EnsureDefaults();
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
                    Error = $"requirements.txt was not found: {requirementsPath}"
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

        public static IReadOnlyList<string> ReadRequirementPackageNames(string requirementsPath)
        {
            var warnings = new List<string>();
            var errors = new List<string>();
            return ReadRequirementPackageNames(requirementsPath, warnings, errors);
        }

        private static IReadOnlyList<string> ReadRequirementPackageNames(
            string requirementsPath,
            List<string> warnings,
            List<string> errors)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var packages = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            ReadRequirementPackageNames(requirementsPath, packages, visited, warnings, errors);
            return packages.ToList();
        }

        private static void ReadRequirementPackageNames(
            string requirementsPath,
            ISet<string> packages,
            ISet<string> visited,
            List<string> warnings,
            List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(requirementsPath) || !File.Exists(requirementsPath))
            {
                errors.Add($"requirements.txt was not found: {requirementsPath}");
                return;
            }

            string fullPath = Path.GetFullPath(requirementsPath);
            if (!visited.Add(fullPath))
            {
                return;
            }

            string baseDirectory = Path.GetDirectoryName(fullPath) ?? string.Empty;
            foreach (string rawLine in File.ReadLines(fullPath))
            {
                string line = StripComment(rawLine).Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (line.StartsWith("-r ", StringComparison.OrdinalIgnoreCase)
                    || line.StartsWith("--requirement ", StringComparison.OrdinalIgnoreCase))
                {
                    string includePath = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
                    string nestedPath = Path.IsPathRooted(includePath) ? includePath : Path.Combine(baseDirectory, includePath);
                    ReadRequirementPackageNames(nestedPath, packages, visited, warnings, errors);
                    continue;
                }

                if (line.StartsWith("-", StringComparison.Ordinal)
                    || line.StartsWith("--", StringComparison.Ordinal))
                {
                    continue;
                }

                string packageName = TryExtractPackageName(line);
                if (!string.IsNullOrWhiteSpace(packageName))
                {
                    packages.Add(packageName);
                }
            }
        }

        private static string StripComment(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return string.Empty;
            }

            int index = line.IndexOf(" #", StringComparison.Ordinal);
            return index >= 0 ? line.Substring(0, index) : line;
        }

        private static string TryExtractPackageName(string requirementLine)
        {
            string line = requirementLine.Split(';')[0].Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                return string.Empty;
            }

            Match eggMatch = Regex.Match(line, @"[#&]egg=([A-Za-z0-9_.-]+)");
            if (eggMatch.Success)
            {
                return eggMatch.Groups[1].Value;
            }

            Match packageMatch = Regex.Match(line, @"^([A-Za-z0-9_.-]+)(?:\[[^\]]+\])?\s*(?:[<>=!~]=?.*)?$");
            return packageMatch.Success ? packageMatch.Groups[1].Value : string.Empty;
        }

        private static IReadOnlyCollection<string> ParseInstalledPackageNames(string json)
        {
            try
            {
                List<PipPackageInfo> packages = JsonConvert.DeserializeObject<List<PipPackageInfo>>(json) ?? new List<PipPackageInfo>();
                return packages
                    .Select(package => NormalizePackageName(package.Name))
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private static string NormalizePackageName(string packageName)
        {
            return (packageName ?? string.Empty).Trim().Replace('_', '-').ToLowerInvariant();
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
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = pythonExecutablePath,
                    WorkingDirectory = Directory.Exists(workingDirectory) ? workingDirectory : AppContext.BaseDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            foreach (string argument in arguments ?? Enumerable.Empty<string>())
            {
                process.StartInfo.ArgumentList.Add(argument);
            }

            try
            {
                if (!process.Start())
                {
                    return new ProcessExecutionResult(-1, string.Empty, "Python process did not start.");
                }

                Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
                Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
                Task waitTask = process.WaitForExitAsync(cancellationToken);
                Task completed = await Task.WhenAny(waitTask, Task.Delay(timeoutMilliseconds, cancellationToken)).ConfigureAwait(false);
                if (completed != waitTask)
                {
                    TryKill(process);
                    return new ProcessExecutionResult(-1, await SafeRead(outputTask).ConfigureAwait(false), "Python command timed out.");
                }

                string output = await SafeRead(outputTask).ConfigureAwait(false);
                string error = await SafeRead(errorTask).ConfigureAwait(false);
                return new ProcessExecutionResult(process.ExitCode, output, error);
            }
            catch (Exception ex)
            {
                AppLog.ABNORMAL($"Python environment command failed: {ex.Message}");
                return new ProcessExecutionResult(-1, string.Empty, ex.Message);
            }
        }

        private static async Task<string> SafeRead(Task<string> task)
        {
            try
            {
                return await task.ConfigureAwait(false);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (process != null && !process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
            }
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values?.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }

        private sealed class PipPackageInfo
        {
            public string Name { get; set; } = string.Empty;
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
