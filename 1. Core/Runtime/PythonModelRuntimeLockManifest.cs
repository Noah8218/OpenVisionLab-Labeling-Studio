using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace MvcVisionSystem._1._Core
{
    public sealed class PythonModelRuntimeLockManifest
    {
        public int SchemaVersion { get; set; }
        public string ProfileId { get; set; } = string.Empty;
        public string Engine { get; set; } = string.Empty;
        public PythonModelRuntimeLockPython Python { get; set; }
        public PythonModelRuntimeLockCuda Cuda { get; set; }
        public List<PythonModelRuntimeLockPackage> Packages { get; set; } = new List<PythonModelRuntimeLockPackage>();
        public PythonModelRuntimeLockArtifact Weights { get; set; }
        public PythonModelRuntimeLockArtifact Worker { get; set; }
    }

    public sealed class PythonModelRuntimeLockPython
    {
        public string Version { get; set; } = string.Empty;
        public string Architecture { get; set; } = string.Empty;
        public string Sha256 { get; set; } = string.Empty;
    }

    public sealed class PythonModelRuntimeLockCuda
    {
        public string Mode { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string DriverVersion { get; set; } = string.Empty;
    }

    public sealed class PythonModelRuntimeLockPackage
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string WheelFile { get; set; } = string.Empty;
        public string WheelSha256 { get; set; } = string.Empty;
    }

    public sealed class PythonModelRuntimeLockArtifact
    {
        public string Path { get; set; } = string.Empty;
        public string Sha256 { get; set; } = string.Empty;
    }

    public sealed class PythonModelRuntimeLockPreflightResult
    {
        public PythonModelRuntimeLockPreflightResult(
            bool isConfigured,
            bool isValid,
            string manifestPath,
            string profileId,
            string engine,
            IEnumerable<string> errors,
            IEnumerable<string> warnings)
        {
            IsConfigured = isConfigured;
            IsValid = isValid;
            ManifestPath = manifestPath ?? string.Empty;
            ProfileId = profileId ?? string.Empty;
            Engine = engine ?? string.Empty;
            Errors = (errors ?? Enumerable.Empty<string>()).ToArray();
            Warnings = (warnings ?? Enumerable.Empty<string>()).ToArray();
            DownloadAttempted = false;
            InstallAttempted = false;
            Summary = Errors.Count > 0
                ? string.Join(Environment.NewLine, Errors)
                : IsConfigured
                    ? $"Runtime lock verified: {ProfileId}"
                    : "Runtime lock manifest is not configured.";
        }

        public bool IsConfigured { get; }
        public bool IsValid { get; }
        public string ManifestPath { get; }
        public string ProfileId { get; }
        public string Engine { get; }
        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<string> Warnings { get; }
        public bool DownloadAttempted { get; }
        public bool InstallAttempted { get; }
        public string Summary { get; }
    }

    public static class PythonModelRuntimeLockManifestService
    {
        public const string ManifestPathEnvironmentVariable = "OPENVISIONLAB_RUNTIME_LOCK_MANIFEST";
        public const string DefaultManifestFileName = "openvisionlab-runtime-lock.json";
        public const int SupportedSchemaVersion = 1;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Disallow,
            AllowTrailingCommas = false
        };

        public static PythonModelRuntimeLockPreflightResult ValidateIfConfigured(PythonModelSettings settings)
        {
            string manifestPath = ResolveManifestPath(settings);
            if (string.IsNullOrWhiteSpace(manifestPath))
            {
                return new PythonModelRuntimeLockPreflightResult(
                    isConfigured: false,
                    isValid: true,
                    manifestPath: string.Empty,
                    profileId: string.Empty,
                    engine: string.Empty,
                    errors: Array.Empty<string>(),
                    warnings: Array.Empty<string>());
            }

            return Validate(settings, manifestPath);
        }

        public static string ResolveManifestPath(PythonModelSettings settings)
        {
            string configuredPath = Environment.GetEnvironmentVariable(ManifestPathEnvironmentVariable)?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                return GetFullPathOrOriginal(configuredPath);
            }

            string projectRootPath = settings?.ProjectRootPath?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(projectRootPath))
            {
                return string.Empty;
            }

            string conventionalPath = Path.Combine(projectRootPath, DefaultManifestFileName);
            return File.Exists(conventionalPath) ? conventionalPath : string.Empty;
        }

        public static PythonModelRuntimeLockPreflightResult Validate(PythonModelSettings settings, string manifestPath)
        {
            string normalizedManifestPath = GetFullPathOrOriginal(manifestPath?.Trim() ?? string.Empty);
            if (string.IsNullOrWhiteSpace(normalizedManifestPath))
            {
                return CreateInvalidResult(
                    isConfigured: false,
                    normalizedManifestPath,
                    Array.Empty<string>(),
                    "Runtime lock manifest path is empty.");
            }

            var errors = new List<string>();
            var warnings = new List<string>();
            PythonModelRuntimeLockManifest manifest = null;
            if (!File.Exists(normalizedManifestPath))
            {
                errors.Add($"Runtime lock manifest is missing: {normalizedManifestPath}");
                return CreateResult(normalizedManifestPath, manifest, errors, warnings);
            }

            try
            {
                manifest = JsonSerializer.Deserialize<PythonModelRuntimeLockManifest>(
                    File.ReadAllText(normalizedManifestPath),
                    JsonOptions);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
            {
                errors.Add($"Runtime lock manifest cannot be read: {normalizedManifestPath} ({ex.Message})");
                return CreateResult(normalizedManifestPath, manifest, errors, warnings);
            }

            if (manifest == null)
            {
                errors.Add($"Runtime lock manifest is empty: {normalizedManifestPath}");
                return CreateResult(normalizedManifestPath, manifest, errors, warnings);
            }

            ValidateManifestIdentity(manifest, settings, errors);
            ValidatePython(manifest.Python, settings, errors);
            ValidateCuda(manifest.Cuda, errors);

            string manifestDirectory = Path.GetDirectoryName(normalizedManifestPath) ?? string.Empty;
            Dictionary<string, string> installedPackages = ResolveInstalledPackageVersions(settings, errors);
            ValidatePackages(manifest, installedPackages, manifestDirectory, errors);
            ValidateArtifact(
                manifest.Weights,
                "weights",
                manifestDirectory,
                settings?.WeightsPath,
                errors);
            ValidateArtifact(
                manifest.Worker,
                "worker",
                manifestDirectory,
                settings?.ClientScriptPath,
                errors);

            return CreateResult(normalizedManifestPath, manifest, errors, warnings);
        }

        private static void ValidateManifestIdentity(
            PythonModelRuntimeLockManifest manifest,
            PythonModelSettings settings,
            List<string> errors)
        {
            if (manifest.SchemaVersion != SupportedSchemaVersion)
            {
                errors.Add($"Runtime lock schemaVersion must be {SupportedSchemaVersion}: {manifest.SchemaVersion}");
            }

            if (string.IsNullOrWhiteSpace(manifest.ProfileId))
            {
                errors.Add("Runtime lock profileId is required.");
            }

            string declaredEngine = manifest.Engine?.Trim() ?? string.Empty;
            if (!IsSupportedCanonicalEngine(declaredEngine))
            {
                errors.Add($"Runtime lock engine is unsupported or not canonical: {declaredEngine}");
                return;
            }

            string configuredEngine = PythonModelSettings.NormalizeModelEngine(settings?.ModelEngine);
            if (!string.Equals(configuredEngine, declaredEngine, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Runtime lock engine mismatch: manifest={declaredEngine}, settings={configuredEngine}");
            }
        }

        private static void ValidatePython(
            PythonModelRuntimeLockPython python,
            PythonModelSettings settings,
            List<string> errors)
        {
            if (python == null)
            {
                errors.Add("Runtime lock python section is required.");
                return;
            }

            if (string.IsNullOrWhiteSpace(python.Version))
            {
                errors.Add("Runtime lock python.version is required.");
            }

            string expectedArchitecture = python.Architecture?.Trim() ?? string.Empty;
            string observedArchitecture = Environment.Is64BitProcess ? "x64" : "x86";
            if (!string.Equals(expectedArchitecture, observedArchitecture, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Runtime lock Python architecture mismatch: manifest={expectedArchitecture}, process={observedArchitecture}");
            }

            string pythonExecutable = PythonModelSettingsValidator.ResolvePythonExecutable(settings);
            if (string.IsNullOrWhiteSpace(pythonExecutable) || !File.Exists(pythonExecutable))
            {
                errors.Add($"Runtime lock Python executable is missing: {pythonExecutable}");
                return;
            }

            ValidateFileHash(pythonExecutable, python.Sha256, "python executable", errors);
            string observedVersion = GetFileVersion(pythonExecutable);
            if (!VersionsEqual(python.Version, observedVersion))
            {
                errors.Add($"Runtime lock Python version mismatch: manifest={python.Version}, actual={observedVersion}");
            }

            string executableArchitecture = GetExecutableArchitecture(pythonExecutable);
            if (!string.Equals(executableArchitecture, expectedArchitecture, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Runtime lock Python executable architecture mismatch: manifest={expectedArchitecture}, actual={executableArchitecture}");
            }
        }

        private static void ValidateCuda(PythonModelRuntimeLockCuda cuda, List<string> errors)
        {
            string mode = cuda?.Mode?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(mode))
            {
                errors.Add("Runtime lock cuda.mode is required.");
                return;
            }

            if (!string.Equals(mode, "cpu", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Runtime lock CUDA mode is unsupported; only CPU is supported by this offline profile: {mode}");
            }
        }

        private static Dictionary<string, string> ResolveInstalledPackageVersions(
            PythonModelSettings settings,
            List<string> errors)
        {
            var packages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string pythonExecutable = PythonModelSettingsValidator.ResolvePythonExecutable(settings);
            if (!TryResolveSitePackagesPath(pythonExecutable, out string sitePackagesPath))
            {
                errors.Add($"Runtime lock Python site-packages directory is missing: {pythonExecutable}");
                return packages;
            }

            IEnumerable<string> metadataDirectories;
            try
            {
                metadataDirectories = Directory.EnumerateDirectories(sitePackagesPath, "*.dist-info", SearchOption.TopDirectoryOnly)
                    .Concat(Directory.EnumerateDirectories(sitePackagesPath, "*.egg-info", SearchOption.TopDirectoryOnly))
                    .ToArray();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                errors.Add($"Runtime lock package metadata cannot be enumerated: {sitePackagesPath} ({ex.Message})");
                return packages;
            }

            foreach (string metadataDirectory in metadataDirectories)
            {
                string metadataPath = Path.Combine(metadataDirectory, "METADATA");
                if (!File.Exists(metadataPath))
                {
                    metadataPath = Path.Combine(metadataDirectory, "PKG-INFO");
                }

                if (!TryReadPackageMetadata(metadataPath, out string name, out string version))
                {
                    continue;
                }

                packages[NormalizePackageName(name)] = version;
            }

            return packages;
        }

        private static void ValidatePackages(
            PythonModelRuntimeLockManifest manifest,
            IReadOnlyDictionary<string, string> installedPackages,
            string manifestDirectory,
            List<string> errors)
        {
            IReadOnlyList<PythonModelRuntimeLockPackage> packages = manifest.Packages ?? new List<PythonModelRuntimeLockPackage>();
            if (packages.Count == 0)
            {
                errors.Add("Runtime lock packages must contain at least one package.");
                return;
            }

            var declaredNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (PythonModelRuntimeLockPackage package in packages)
            {
                string name = package?.Name?.Trim() ?? string.Empty;
                string normalizedName = NormalizePackageName(name);
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(normalizedName))
                {
                    errors.Add("Runtime lock package name is required.");
                    continue;
                }

                if (!declaredNames.Add(normalizedName))
                {
                    errors.Add($"Runtime lock package is declared more than once: {name}");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(package.Version))
                {
                    errors.Add($"Runtime lock package version is required: {name}");
                }
                else if (!installedPackages.TryGetValue(normalizedName, out string installedVersion))
                {
                    errors.Add($"Runtime lock package is missing from Python: {name}");
                }
                else if (!VersionsEqual(package.Version, installedVersion))
                {
                    errors.Add($"Runtime lock package version mismatch: {name}, manifest={package.Version}, actual={installedVersion}");
                }

                ValidateWheel(package, name, manifestDirectory, errors);
            }

            foreach (string requiredPackage in GetRequiredPackageNames(manifest.Engine))
            {
                if (!declaredNames.Contains(NormalizePackageName(requiredPackage)))
                {
                    errors.Add($"Runtime lock package is required for {manifest.Engine}: {requiredPackage}");
                }
            }
        }

        private static void ValidateWheel(
            PythonModelRuntimeLockPackage package,
            string packageName,
            string manifestDirectory,
            List<string> errors)
        {
            if (package == null)
            {
                errors.Add("Runtime lock package entry is null.");
                return;
            }

            string wheelPathText = package.WheelFile?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(wheelPathText))
            {
                errors.Add($"Runtime lock wheelFile is required: {packageName}");
                return;
            }

            string wheelPath = ResolveArtifactPath(manifestDirectory, wheelPathText);
            if (!File.Exists(wheelPath))
            {
                errors.Add($"Runtime lock wheel is missing: {packageName} ({wheelPath})");
                return;
            }

            ValidateFileHash(wheelPath, package.WheelSha256, $"wheel {packageName}", errors);
        }

        private static void ValidateArtifact(
            PythonModelRuntimeLockArtifact artifact,
            string label,
            string manifestDirectory,
            string configuredPath,
            List<string> errors)
        {
            if (artifact == null)
            {
                errors.Add($"Runtime lock {label} artifact is required.");
                return;
            }

            string artifactPathText = artifact.Path?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(artifactPathText))
            {
                errors.Add($"Runtime lock {label}.path is required.");
                return;
            }

            string artifactPath = ResolveArtifactPath(manifestDirectory, artifactPathText);
            if (!File.Exists(artifactPath))
            {
                errors.Add($"Runtime lock {label} is missing: {artifactPath}");
                return;
            }

            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                errors.Add($"Runtime lock {label} is not selected in the active model settings: {label}.path={artifactPath}");
            }
            else if (!PathsEqual(configuredPath, artifactPath))
            {
                errors.Add($"Runtime lock {label} path mismatch: manifest={artifactPath}, settings={configuredPath}");
            }

            ValidateFileHash(artifactPath, artifact.Sha256, label, errors);
        }

        private static void ValidateFileHash(string path, string expectedHash, string label, List<string> errors)
        {
            string normalizedExpectedHash = expectedHash?.Trim() ?? string.Empty;
            if (!IsSha256(normalizedExpectedHash))
            {
                errors.Add($"Runtime lock {label} sha256 must be 64 hexadecimal characters: {normalizedExpectedHash}");
                return;
            }

            try
            {
                string actualHash;
                using (FileStream stream = File.OpenRead(path))
                using (SHA256 sha256 = SHA256.Create())
                {
                    actualHash = Convert.ToHexString(sha256.ComputeHash(stream));
                }

                if (!string.Equals(normalizedExpectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"Runtime lock {label} sha256 mismatch: expected={normalizedExpectedHash}, actual={actualHash}, path={path}");
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                errors.Add($"Runtime lock {label} cannot be hashed: {path} ({ex.Message})");
            }
        }

        private static PythonModelRuntimeLockPreflightResult CreateInvalidResult(
            bool isConfigured,
            string manifestPath,
            IEnumerable<string> warnings,
            string error)
        {
            return new PythonModelRuntimeLockPreflightResult(
                isConfigured,
                isValid: false,
                manifestPath,
                profileId: string.Empty,
                engine: string.Empty,
                errors: new[] { error },
                warnings);
        }

        private static PythonModelRuntimeLockPreflightResult CreateResult(
            string manifestPath,
            PythonModelRuntimeLockManifest manifest,
            IReadOnlyList<string> errors,
            IReadOnlyList<string> warnings)
        {
            return new PythonModelRuntimeLockPreflightResult(
                isConfigured: true,
                isValid: errors.Count == 0,
                manifestPath,
                manifest?.ProfileId,
                manifest?.Engine,
                errors,
                warnings);
        }

        private static bool TryResolveSitePackagesPath(string pythonExecutablePath, out string sitePackagesPath)
        {
            sitePackagesPath = string.Empty;
            string trimmedPath = pythonExecutablePath?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmedPath))
            {
                return false;
            }

            string pythonDirectory = File.Exists(trimmedPath)
                ? Path.GetDirectoryName(trimmedPath) ?? string.Empty
                : trimmedPath;
            if (string.IsNullOrWhiteSpace(pythonDirectory))
            {
                return false;
            }

            string directSitePackages = Path.Combine(pythonDirectory, "Lib", "site-packages");
            if (Directory.Exists(directSitePackages))
            {
                sitePackagesPath = directSitePackages;
                return true;
            }

            if (!string.Equals(Path.GetFileName(pythonDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), "Scripts", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string virtualEnvironmentRoot = Directory.GetParent(pythonDirectory)?.FullName ?? string.Empty;
            string virtualEnvironmentSitePackages = Path.Combine(virtualEnvironmentRoot, "Lib", "site-packages");
            if (!Directory.Exists(virtualEnvironmentSitePackages))
            {
                return false;
            }

            sitePackagesPath = virtualEnvironmentSitePackages;
            return true;
        }

        private static bool TryReadPackageMetadata(string metadataPath, out string name, out string version)
        {
            name = string.Empty;
            version = string.Empty;
            if (!File.Exists(metadataPath))
            {
                return false;
            }

            try
            {
                foreach (string line in File.ReadLines(metadataPath))
                {
                    if (line.StartsWith("Name:", StringComparison.OrdinalIgnoreCase))
                    {
                        name = line.Substring("Name:".Length).Trim();
                    }
                    else if (line.StartsWith("Version:", StringComparison.OrdinalIgnoreCase))
                    {
                        version = line.Substring("Version:".Length).Trim();
                    }

                    if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(version))
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return false;
            }

            return false;
        }

        private static string GetFileVersion(string path)
        {
            try
            {
                FileVersionInfo info = FileVersionInfo.GetVersionInfo(path);
                return (info.FileVersion ?? info.ProductVersion ?? string.Empty).Trim();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return string.Empty;
            }
        }

        private static string GetExecutableArchitecture(string path)
        {
            try
            {
                using FileStream stream = File.OpenRead(path);
                using var reader = new BinaryReader(stream);
                if (reader.ReadUInt16() != 0x5A4D || stream.Length < 0x40)
                {
                    return string.Empty;
                }

                stream.Position = 0x3C;
                int peHeaderOffset = reader.ReadInt32();
                if (peHeaderOffset < 0 || peHeaderOffset + 6 > stream.Length)
                {
                    return string.Empty;
                }

                stream.Position = peHeaderOffset;
                if (reader.ReadUInt32() != 0x00004550)
                {
                    return string.Empty;
                }

                return reader.ReadUInt16() switch
                {
                    0x014C => "x86",
                    0x8664 => "x64",
                    0xAA64 => "arm64",
                    _ => string.Empty
                };
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is EndOfStreamException)
            {
                return string.Empty;
            }
        }

        private static bool VersionsEqual(string expected, string actual)
        {
            string expectedText = expected?.Trim() ?? string.Empty;
            string actualText = actual?.Trim() ?? string.Empty;
            if (string.Equals(expectedText, actualText, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return Version.TryParse(expectedText, out Version expectedVersion)
                && Version.TryParse(actualText, out Version actualVersion)
                && expectedVersion == actualVersion;
        }

        private static bool IsSupportedCanonicalEngine(string value)
            => PythonModelSettings.GetSupportedModelEngines()
                .Any(engine => string.Equals(engine, value, StringComparison.OrdinalIgnoreCase));

        private static IReadOnlyList<string> GetRequiredPackageNames(string engine)
        {
            string normalizedEngine = PythonModelSettings.NormalizeModelEngine(engine);
            if (string.Equals(normalizedEngine, PythonModelSettings.EngineYoloV8, StringComparison.Ordinal)
                || string.Equals(normalizedEngine, PythonModelSettings.EngineYolo11, StringComparison.Ordinal))
            {
                return new[] { "torch", "torchvision", "ultralytics", "numpy", "pillow" };
            }

            if (string.Equals(normalizedEngine, PythonModelSettings.EnginePatchCore, StringComparison.Ordinal))
            {
                return new[] { "torch", "torchvision", "numpy", "pillow" };
            }

            if (string.Equals(normalizedEngine, PythonModelSettings.EngineUnet, StringComparison.Ordinal))
            {
                return new[] { "torch", "pillow" };
            }

            return new[] { "torch" };
        }

        private static string NormalizePackageName(string value)
        {
            return new string((value ?? string.Empty)
                .Where(char.IsLetterOrDigit)
                .ToArray())
                .ToLowerInvariant();
        }

        private static bool IsSha256(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 64)
            {
                return false;
            }

            return value.All(character => character is >= '0' and <= '9'
                or >= 'a' and <= 'f'
                or >= 'A' and <= 'F');
        }

        private static string ResolveArtifactPath(string manifestDirectory, string path)
        {
            try
            {
                return Path.GetFullPath(Path.IsPathRooted(path)
                    ? path
                    : Path.Combine(manifestDirectory ?? string.Empty, path));
            }
            catch
            {
                return path ?? string.Empty;
            }
        }

        private static bool PathsEqual(string first, string second)
        {
            try
            {
                return string.Equals(
                    Path.GetFullPath(first.Trim()),
                    Path.GetFullPath(second.Trim()),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return string.Equals(first?.Trim(), second?.Trim(), StringComparison.OrdinalIgnoreCase);
            }
        }

        private static string GetFullPathOrOriginal(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(value);
            }
            catch
            {
                return value;
            }
        }
    }
}
