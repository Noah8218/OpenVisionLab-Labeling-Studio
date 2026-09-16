using OpenVisionLab.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MvcVisionSystem
{
    public class RuntimeDiagnosticsService
    {
        public const string ApplicationDataRootEnvironmentVariable = "OPENVISIONLAB_LABELING_APP_DATA_ROOT";
        public const string LocalizationConfigRootEnvironmentVariable = "OPENVISIONLAB_CONFIG_ROOT";
        public const int RetentionDays = 30;
        public const int MaximumDiagnosticFiles = 50;
        public const int MaximumSupportBundleFiles = 20;
        public const long MaximumSupportBundleBytes = 250L * 1024L * 1024L;
        public const long MaximumIncludedLogBytes = 5L * 1024L * 1024L;
        public const string ViewerGraphicsCheckName = "viewerGraphics";

        private const int MaximumIncludedLogFiles = 20;
        private const int MaximumLogCharactersPerFile = 500000;
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        private static readonly Regex SecretAssignmentRegex = new Regex(
            @"(?i)([""']?\b(password|passwd|token|api[_-]?key|secret|authorization)\b[""']?\s*[:=]\s*)([""']?)([^\s,;}""']+)",
            RegexOptions.Compiled);
        private static readonly Regex WindowsPathRegex = new Regex(
            @"(?i)(?<![A-Za-z0-9_])(?:[A-Z]:\\|\\\\)[^\r\n""<>|]+",
            RegexOptions.Compiled);

        private readonly RuntimeDiagnosticsPaths paths;
        private Func<RuntimeSelfTestCheck> graphicsCapabilityProvider;

        public RuntimeDiagnosticsService()
            : this(RuntimeDiagnosticsPaths.Resolve())
        {
        }

        public RuntimeDiagnosticsService(string applicationRoot, string applicationDataRoot)
            : this(RuntimeDiagnosticsPaths.Resolve(applicationRoot, applicationDataRoot))
        {
        }

        internal RuntimeDiagnosticsService(RuntimeDiagnosticsPaths paths)
        {
            this.paths = paths ?? throw new ArgumentNullException(nameof(paths));
        }

        internal RuntimeDiagnosticsService(
            RuntimeDiagnosticsPaths paths,
            Func<RuntimeSelfTestCheck> graphicsCapabilityProvider)
            : this(paths)
        {
            SetGraphicsCapabilityProvider(graphicsCapabilityProvider);
        }

        public RuntimeDiagnosticsPaths Paths => paths;

        public void SetGraphicsCapabilityProvider(Func<RuntimeSelfTestCheck> provider)
        {
            graphicsCapabilityProvider = provider;
        }

        internal RuntimeSelfTestCheck RunGraphicsCapabilityCheck()
        {
            if (graphicsCapabilityProvider == null)
            {
                return new RuntimeSelfTestCheck(
                    ViewerGraphicsCheckName,
                    "warning",
                    "이미지 뷰어 그래픽 점검이 현재 실행 표면에 연결되지 않았습니다.");
            }

            try
            {
                RuntimeSelfTestCheck check = graphicsCapabilityProvider();
                return check ?? new RuntimeSelfTestCheck(
                    ViewerGraphicsCheckName,
                    "fail",
                    "이미지 뷰어 그래픽 점검 결과가 없습니다.");
            }
            catch (Exception ex)
            {
                return new RuntimeSelfTestCheck(
                    ViewerGraphicsCheckName,
                    "fail",
                    "이미지 뷰어 그래픽 점검 실패 · " + ex.GetType().Name);
            }
        }

        public static RuntimeStartupResult ConfigureApplicationStartup()
        {
            RuntimeDiagnosticsPaths resolvedPaths = RuntimeDiagnosticsPaths.Resolve();
            try
            {
                string startupPath = ConfigureApplicationStartup(resolvedPaths);
                return new RuntimeStartupResult(true, startupPath, string.Empty);
            }
            catch (Exception ex)
            {
                string primaryError = ex.GetType().Name + ": " + ex.Message;
                try
                {
                    RuntimeDiagnosticsPaths fallbackPaths = RuntimeDiagnosticsPaths.Resolve(
                        AppContext.BaseDirectory,
                        Path.Combine(Path.GetTempPath(), "OpenVisionLab", "LabelingStudio"));
                    string startupPath = ConfigureApplicationStartup(fallbackPaths);
                    return new RuntimeStartupResult(true, startupPath, "사용자 경로 실패, 임시 경로 사용: " + primaryError);
                }
                catch (Exception fallbackException)
                {
                    return new RuntimeStartupResult(
                        false,
                        string.Empty,
                        primaryError + " / fallback " + fallbackException.GetType().Name + ": " + fallbackException.Message);
                }
            }
        }

        public RuntimeSelfTestResult RunSelfTest(bool persistResult = true)
        {
            return RunSelfTestCore(persistResult, probeWritableDirectories: true);
        }

        public RuntimeSelfTestResult RunReadOnlySelfTest()
        {
            return RunSelfTestCore(persistResult: false, probeWritableDirectories: false);
        }

        private RuntimeSelfTestResult RunSelfTestCore(
            bool persistResult,
            bool probeWritableDirectories)
        {
            if (probeWritableDirectories)
            {
                paths.EnsureDirectories();
            }

            var checks = new List<RuntimeSelfTestCheck>();
            Assembly productAssembly = typeof(RuntimeDiagnosticsService).Assembly;
            string productVersion = GetProductVersion(productAssembly);
            string productDllPath = productAssembly.Location;
            string executablePath = Path.Combine(paths.ApplicationRoot, "OpenVisionLab.LabelingStudio.exe");
            string manifestPath = Path.Combine(paths.ApplicationRoot, "release-manifest.json");

            checks.Add(new RuntimeSelfTestCheck(
                "productIdentity",
                "pass",
                $"OpenVisionLab Labeling Studio {productVersion} / {RuntimeInformation.ProcessArchitecture}"));

            checks.Add(new RuntimeSelfTestCheck(
                "productBinary",
                File.Exists(productDllPath) ? "pass" : "fail",
                File.Exists(productDllPath) ? "제품 DLL 확인" : "제품 DLL 없음"));

            bool hasExecutable = File.Exists(executablePath);
            checks.Add(new RuntimeSelfTestCheck(
                "applicationExecutable",
                hasExecutable ? "pass" : "warning",
                hasExecutable ? "배포 실행 파일 확인" : "개발/테스트 호스트: 배포 EXE 없음"));

            if (File.Exists(manifestPath))
            {
                checks.Add(ValidateReleaseManifest(manifestPath, productVersion));
            }
            else
            {
                checks.Add(new RuntimeSelfTestCheck(
                    "releaseManifest",
                    "warning",
                    "개발/테스트 실행: release-manifest.json 없음"));
            }

            checks.Add(probeWritableDirectories
                ? CheckWritableDirectory("diagnosticsPath", paths.DiagnosticsDirectory)
                : CheckExistingDirectoryReadOnly("diagnosticsPath", paths.DiagnosticsDirectory));
            checks.Add(probeWritableDirectories
                ? CheckWritableDirectory("supportBundlePath", paths.SupportBundlesDirectory)
                : CheckExistingDirectoryReadOnly("supportBundlePath", paths.SupportBundlesDirectory));

            bool logOutsideApplication = !IsSameOrChildPath(paths.LogDirectory, paths.ApplicationRoot);
            checks.Add(new RuntimeSelfTestCheck(
                "logIsolation",
                logOutsideApplication ? "pass" : "fail",
                logOutsideApplication
                    ? "로그가 사용자 쓰기 경로에 분리됨"
                    : "로그 경로가 설치/실행 폴더 내부임"));
            checks.Add(RunGraphicsCapabilityCheck());

            var result = new RuntimeSelfTestResult(
                DateTimeOffset.Now,
                "OpenVisionLab Labeling Studio",
                productVersion,
                RuntimeInformation.OSDescription,
                RuntimeInformation.FrameworkDescription,
                RuntimeInformation.ProcessArchitecture.ToString(),
                checks);

            if (persistResult)
            {
                WriteJsonAtomically(paths.LatestSelfTestPath, result);
            }

            return result;
        }

        public SupportBundleResult CreateSupportBundle()
        {
            paths.EnsureDirectories();
            ApplyRetention();

            RuntimeSelfTestResult selfTest = RunSelfTest(persistResult: true);
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string archivePath = Path.Combine(
                paths.SupportBundlesDirectory,
                $"OpenVisionLab-Support-{stamp}-{Environment.ProcessId}.zip");
            string temporaryPath = archivePath + ".tmp";
            var includedEntries = new List<string>();
            var skippedLogs = new List<string>();

            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }

            try
            {
                using (var archive = ZipFile.Open(temporaryPath, ZipArchiveMode.Create))
                {
                    AddJsonEntry(archive, "self-test.json", selfTest, includedEntries);
                    AddJsonEntry(archive, "config-summary.json", BuildSafeConfigSummary(), includedEntries);

                    string releaseManifestPath = Path.Combine(paths.ApplicationRoot, "release-manifest.json");
                    if (File.Exists(releaseManifestPath))
                    {
                        AddTextEntry(
                            archive,
                            "release-manifest.json",
                            File.ReadAllText(releaseManifestPath, Encoding.UTF8),
                            includedEntries);
                    }

                    string latestStartup = FindLatestFile(paths.DiagnosticsDirectory, "startup-*.json");
                    if (!string.IsNullOrWhiteSpace(latestStartup))
                    {
                        AddTextEntry(
                            archive,
                            "startup/latest.json",
                            RedactText(File.ReadAllText(latestStartup, Encoding.UTF8)),
                            includedEntries);
                    }

                    AddSanitizedLogs(archive, includedEntries, skippedLogs);

                    string[] manifestEntries = includedEntries
                        .Append("support-manifest.json")
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .ToArray();
                    var manifest = new
                    {
                        schemaVersion = 1,
                        createdAt = DateTimeOffset.Now,
                        product = "OpenVisionLab Labeling Studio",
                        productVersion = selfTest.ProductVersion,
                        privacyPolicy = new
                        {
                            mode = "allow-list",
                            included = new[]
                            {
                                "generated self-test",
                                "generated non-secret configuration summary",
                                "release manifest when present",
                                "latest redacted startup diagnostics",
                                "recent bounded redacted application logs"
                            },
                            excludedByDefault = new[]
                            {
                                "dataset images",
                                "labels and annotations",
                                "model weights",
                                "recipes and projects",
                                "credentials and raw runtime configuration",
                                "memory dumps"
                            }
                        },
                        includedEntries = manifestEntries,
                        skippedLogs = skippedLogs.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                        selfTestStatus = selfTest.OverallStatus
                    };
                    AddJsonEntry(archive, "support-manifest.json", manifest, includedEntries: null);
                }

                File.Move(temporaryPath, archivePath);
                ApplyRetention();
                return new SupportBundleResult(
                    archivePath,
                    selfTest,
                    includedEntries.Append("support-manifest.json").OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                    skippedLogs);
            }
            catch
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }

                throw;
            }
        }

        internal void ApplyRetention()
        {
            paths.EnsureDirectories();
            PruneDirectory(
                paths.DiagnosticsDirectory,
                "*.json",
                RetentionDays,
                MaximumDiagnosticFiles,
                maximumBytes: 20L * 1024L * 1024L);
            PruneDirectory(
                paths.SupportBundlesDirectory,
                "*.zip",
                RetentionDays,
                MaximumSupportBundleFiles,
                MaximumSupportBundleBytes);
        }

        private static string ConfigureApplicationStartup(RuntimeDiagnosticsPaths resolvedPaths)
        {
            resolvedPaths.EnsureDirectories();
            Environment.SetEnvironmentVariable(
                LocalizationConfigRootEnvironmentVariable,
                resolvedPaths.ConfigDirectory);
            OVLog.ApplyFilePolicy(resolvedPaths.LogDirectory, maxBackupFileCount: 10, maximumFileSizeInMB: 20);
            OVLog.ApplyRetentionPolicy(resolvedPaths.LogDirectory, RetentionDays);

            var service = new RuntimeDiagnosticsService(resolvedPaths);
            service.ApplyRetention();
            return service.WriteStartupDiagnostics();
        }

        private string WriteStartupDiagnostics()
        {
            Assembly productAssembly = typeof(RuntimeDiagnosticsService).Assembly;
            string path = Path.Combine(
                paths.DiagnosticsDirectory,
                $"startup-{DateTime.Now:yyyyMMdd-HHmmssfff}-{Environment.ProcessId}.json");
            var record = new
            {
                schemaVersion = 1,
                eventName = "application-startup",
                recordedAt = DateTimeOffset.Now,
                product = "OpenVisionLab Labeling Studio",
                productVersion = GetProductVersion(productAssembly),
                assemblyVersion = productAssembly.GetName().Version?.ToString() ?? string.Empty,
                framework = RuntimeInformation.FrameworkDescription,
                operatingSystem = RuntimeInformation.OSDescription,
                processArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                processId = Environment.ProcessId,
                package = new
                {
                    applicationRoot = paths.ApplicationRoot,
                    releaseManifestPresent = File.Exists(Path.Combine(paths.ApplicationRoot, "release-manifest.json"))
                },
                writablePaths = new
                {
                    applicationDataRoot = paths.ApplicationDataRoot,
                    logDirectory = paths.LogDirectory,
                    diagnosticsDirectory = paths.DiagnosticsDirectory,
                    supportBundlesDirectory = paths.SupportBundlesDirectory,
                    configDirectory = paths.ConfigDirectory
                },
                policies = BuildSafeConfigSummary()
            };
            WriteJsonAtomically(path, record);
            return path;
        }

        private object BuildSafeConfigSummary()
        {
            return new
            {
                schemaVersion = 1,
                applicationDataScope = "current-user",
                logRetentionDays = RetentionDays,
                maximumLogFileMegabytes = 20,
                maximumLogBackups = 10,
                diagnosticRetentionDays = RetentionDays,
                maximumDiagnosticFiles = MaximumDiagnosticFiles,
                supportBundleRetentionDays = RetentionDays,
                maximumSupportBundles = MaximumSupportBundleFiles,
                supportBundleMaximumStoredMegabytes = MaximumSupportBundleBytes / (1024L * 1024L),
                supportBundleCollection = "explicit-action-only",
                telemetry = "disabled",
                supportBundlePrivacy = "allow-list-and-redaction",
                taskExecutionDuringSelfTest = "none"
            };
        }

        private static RuntimeSelfTestCheck ValidateReleaseManifest(string manifestPath, string productVersion)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath, Encoding.UTF8));
                JsonElement root = document.RootElement;
                string manifestProduct = root.TryGetProperty("productName", out JsonElement productName)
                    ? productName.GetString()
                    : string.Empty;
                string manifestVersion = root.TryGetProperty("productVersion", out JsonElement version)
                    ? version.GetString()
                    : string.Empty;
                bool valid = string.Equals(manifestProduct, "OpenVisionLab Labeling Studio", StringComparison.Ordinal)
                    && string.Equals(manifestVersion, productVersion, StringComparison.Ordinal);
                return new RuntimeSelfTestCheck(
                    "releaseManifest",
                    valid ? "pass" : "fail",
                    valid
                        ? $"배포 manifest 제품/버전 일치 ({manifestVersion})"
                        : $"배포 manifest 불일치 (product={manifestProduct}, version={manifestVersion})");
            }
            catch (Exception ex)
            {
                return new RuntimeSelfTestCheck(
                    "releaseManifest",
                    "fail",
                    "배포 manifest 읽기 실패: " + ex.GetType().Name);
            }
        }

        private static RuntimeSelfTestCheck CheckWritableDirectory(string name, string directory)
        {
            string probePath = Path.Combine(directory, $".write-probe-{Environment.ProcessId}-{Guid.NewGuid():N}");
            try
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(probePath, "probe", Encoding.UTF8);
                File.Delete(probePath);
                return new RuntimeSelfTestCheck(name, "pass", "쓰기/삭제 확인");
            }
            catch (Exception ex)
            {
                return new RuntimeSelfTestCheck(name, "fail", "쓰기 실패: " + ex.GetType().Name);
            }
            finally
            {
                try
                {
                    if (File.Exists(probePath))
                    {
                        File.Delete(probePath);
                    }
                }
                catch
                {
                }
            }
        }

        private static RuntimeSelfTestCheck CheckExistingDirectoryReadOnly(string name, string directory)
        {
            bool exists = Directory.Exists(directory);
            return new RuntimeSelfTestCheck(
                name,
                exists ? "pass" : "warning",
                exists
                    ? "경로 존재 확인 · 읽기 전용 CLI에서는 쓰기 프로브 생략"
                    : "경로 없음 · 읽기 전용 CLI에서는 생성하지 않음");
        }

        private void AddSanitizedLogs(
            ZipArchive archive,
            IList<string> includedEntries,
            IList<string> skippedLogs)
        {
            if (!Directory.Exists(paths.LogDirectory))
            {
                return;
            }

            long includedBytes = 0;
            int includedCount = 0;
            int inspectedCount = 0;
            foreach (string logPath in Directory
                .EnumerateFiles(paths.LogDirectory, "*.log", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc))
            {
                inspectedCount++;
                string safeLogLabel = $"log-{inspectedCount:D2}.log";
                if (includedCount >= MaximumIncludedLogFiles || includedBytes >= MaximumIncludedLogBytes)
                {
                    skippedLogs.Add(safeLogLabel + ": bundle log limit");
                    continue;
                }

                try
                {
                    string sanitized = RedactText(ReadLogTail(logPath));
                    byte[] content = Encoding.UTF8.GetBytes(sanitized);
                    if (includedBytes + content.Length > MaximumIncludedLogBytes)
                    {
                        skippedLogs.Add(safeLogLabel + ": bundle byte limit");
                        continue;
                    }

                    string entryName = $"logs/{includedCount + 1:D2}.log";
                    AddBytesEntry(archive, entryName, content, includedEntries);
                    includedBytes += content.Length;
                    includedCount++;
                }
                catch (Exception ex)
                {
                    skippedLogs.Add(safeLogLabel + ": " + ex.GetType().Name);
                }
            }
        }

        private static string ReadLogTail(string path)
        {
            var lines = new Queue<string>();
            int totalCharacters = 0;
            foreach (string line in File.ReadLines(path, Encoding.UTF8))
            {
                lines.Enqueue(line);
                totalCharacters += line.Length + Environment.NewLine.Length;
                while (totalCharacters > MaximumLogCharactersPerFile && lines.Count > 1)
                {
                    string removed = lines.Dequeue();
                    totalCharacters -= removed.Length + Environment.NewLine.Length;
                }
            }

            return string.Join(Environment.NewLine, lines);
        }

        internal static string RedactText(string value)
        {
            string text = value ?? string.Empty;
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(userProfile))
            {
                text = text.Replace(userProfile, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
            }

            text = SecretAssignmentRegex.Replace(
                text,
                match => match.Groups[1].Value
                    + match.Groups[3].Value
                    + "<redacted>"
                    + match.Groups[3].Value);
            text = WindowsPathRegex.Replace(text, "<redacted-path>");
            return text;
        }

        private static void PruneDirectory(
            string directory,
            string pattern,
            int retentionDays,
            int maximumFiles,
            long maximumBytes)
        {
            if (!Directory.Exists(directory))
            {
                return;
            }

            DateTime cutoff = DateTime.UtcNow.AddDays(-retentionDays);
            var files = Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly)
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .ToList();

            foreach (FileInfo file in files.Where(file => file.LastWriteTimeUtc < cutoff).ToArray())
            {
                TryDelete(file);
                files.Remove(file);
            }

            long totalBytes = files.Sum(file => file.Exists ? file.Length : 0L);
            for (int index = files.Count - 1;
                 index >= 0 && (files.Count > maximumFiles || totalBytes > maximumBytes);
                 index--)
            {
                FileInfo file = files[index];
                long length = file.Exists ? file.Length : 0L;
                if (TryDelete(file))
                {
                    totalBytes -= length;
                    files.RemoveAt(index);
                }
            }
        }

        private static bool TryDelete(FileInfo file)
        {
            try
            {
                file.Delete();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void AddJsonEntry(
            ZipArchive archive,
            string entryName,
            object value,
            IList<string> includedEntries)
        {
            AddTextEntry(archive, entryName, JsonSerializer.Serialize(value, JsonOptions), includedEntries);
        }

        private static void AddTextEntry(
            ZipArchive archive,
            string entryName,
            string value,
            IList<string> includedEntries)
        {
            AddBytesEntry(archive, entryName, Encoding.UTF8.GetBytes(value ?? string.Empty), includedEntries);
        }

        private static void AddBytesEntry(
            ZipArchive archive,
            string entryName,
            byte[] content,
            IList<string> includedEntries)
        {
            ZipArchiveEntry entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using Stream stream = entry.Open();
            stream.Write(content, 0, content.Length);
            includedEntries?.Add(entryName);
        }

        private static string FindLatestFile(string directory, string pattern)
        {
            return Directory.Exists(directory)
                ? Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault()
                : null;
        }

        private static string GetProductVersion(Assembly productAssembly)
        {
            return productAssembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion?
                .Split('+')[0]
                ?? productAssembly.GetName().Version?.ToString(3)
                ?? "unknown";
        }

        private static bool IsSameOrChildPath(string candidate, string parent)
        {
            string normalizedCandidate = Path.GetFullPath(candidate)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedParent = Path.GetFullPath(parent)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(normalizedCandidate, normalizedParent, StringComparison.OrdinalIgnoreCase)
                || normalizedCandidate.StartsWith(
                    normalizedParent + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase);
        }

        private static void WriteJsonAtomically(string path, object value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporaryPath = path + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(value, JsonOptions), new UTF8Encoding(false));
            File.Move(temporaryPath, path, overwrite: true);
        }
    }

    public sealed class RuntimeDiagnosticsPaths
    {
        internal RuntimeDiagnosticsPaths(string applicationRoot, string applicationDataRoot)
        {
            ApplicationRoot = Path.GetFullPath(applicationRoot);
            ApplicationDataRoot = Path.GetFullPath(applicationDataRoot);
            LogDirectory = Path.Combine(ApplicationDataRoot, "Logs");
            DiagnosticsDirectory = Path.Combine(ApplicationDataRoot, "Diagnostics");
            SupportBundlesDirectory = Path.Combine(ApplicationDataRoot, "SupportBundles");
            ConfigDirectory = Path.Combine(ApplicationDataRoot, "Config");
            LatestSelfTestPath = Path.Combine(DiagnosticsDirectory, "self-test-latest.json");
        }

        public string ApplicationRoot { get; }
        public string ApplicationDataRoot { get; }
        public string LogDirectory { get; }
        public string DiagnosticsDirectory { get; }
        public string SupportBundlesDirectory { get; }
        public string ConfigDirectory { get; }
        public string LatestSelfTestPath { get; }

        public static RuntimeDiagnosticsPaths Resolve(string applicationRoot = null, string applicationDataRoot = null)
        {
            string resolvedApplicationRoot = string.IsNullOrWhiteSpace(applicationRoot)
                ? AppContext.BaseDirectory
                : applicationRoot;
            string configuredRoot = string.IsNullOrWhiteSpace(applicationDataRoot)
                ? Environment.GetEnvironmentVariable(RuntimeDiagnosticsService.ApplicationDataRootEnvironmentVariable)
                : applicationDataRoot;
            string localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string resolvedDataRoot = !string.IsNullOrWhiteSpace(configuredRoot)
                ? configuredRoot
                : !string.IsNullOrWhiteSpace(localApplicationData)
                    ? Path.Combine(localApplicationData, "OpenVisionLab", "LabelingStudio")
                    : Path.Combine(Path.GetTempPath(), "OpenVisionLab", "LabelingStudio");
            return new RuntimeDiagnosticsPaths(resolvedApplicationRoot, resolvedDataRoot);
        }

        public void EnsureDirectories()
        {
            Directory.CreateDirectory(ApplicationDataRoot);
            Directory.CreateDirectory(LogDirectory);
            Directory.CreateDirectory(DiagnosticsDirectory);
            Directory.CreateDirectory(SupportBundlesDirectory);
            Directory.CreateDirectory(ConfigDirectory);
        }
    }

    public sealed class RuntimeStartupResult
    {
        public RuntimeStartupResult(bool succeeded, string diagnosticsPath, string error)
        {
            Succeeded = succeeded;
            DiagnosticsPath = diagnosticsPath ?? string.Empty;
            Error = error ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string DiagnosticsPath { get; }
        public string Error { get; }
    }

    public sealed class RuntimeSelfTestCheck
    {
        public RuntimeSelfTestCheck(string name, string status, string detail)
        {
            Name = name ?? string.Empty;
            Status = status ?? "fail";
            Detail = detail ?? string.Empty;
        }

        public string Name { get; }
        public string Status { get; }
        public string Detail { get; }
    }

    public sealed class RuntimeSelfTestResult
    {
        public RuntimeSelfTestResult(
            DateTimeOffset checkedAt,
            string product,
            string productVersion,
            string operatingSystem,
            string framework,
            string processArchitecture,
            IReadOnlyList<RuntimeSelfTestCheck> checks)
        {
            CheckedAt = checkedAt;
            Product = product ?? string.Empty;
            ProductVersion = productVersion ?? string.Empty;
            OperatingSystem = operatingSystem ?? string.Empty;
            Framework = framework ?? string.Empty;
            ProcessArchitecture = processArchitecture ?? string.Empty;
            Checks = checks ?? Array.Empty<RuntimeSelfTestCheck>();
        }

        public DateTimeOffset CheckedAt { get; }
        public string Product { get; }
        public string ProductVersion { get; }
        public string OperatingSystem { get; }
        public string Framework { get; }
        public string ProcessArchitecture { get; }
        public IReadOnlyList<RuntimeSelfTestCheck> Checks { get; }
        public int PassedCount => Checks.Count(check => string.Equals(check.Status, "pass", StringComparison.Ordinal));
        public int WarningCount => Checks.Count(check => string.Equals(check.Status, "warning", StringComparison.Ordinal));
        public int FailedCount => Checks.Count(check => string.Equals(check.Status, "fail", StringComparison.Ordinal));
        public string OverallStatus => FailedCount > 0 ? "fail" : WarningCount > 0 ? "warning" : "pass";
    }

    public sealed class SupportBundleResult
    {
        public SupportBundleResult(
            string archivePath,
            RuntimeSelfTestResult selfTest,
            IReadOnlyList<string> includedEntries,
            IReadOnlyList<string> skippedLogs)
        {
            ArchivePath = archivePath ?? string.Empty;
            SelfTest = selfTest;
            IncludedEntries = includedEntries ?? Array.Empty<string>();
            SkippedLogs = skippedLogs ?? Array.Empty<string>();
        }

        public string ArchivePath { get; }
        public RuntimeSelfTestResult SelfTest { get; }
        public IReadOnlyList<string> IncludedEntries { get; }
        public IReadOnlyList<string> SkippedLogs { get; }
    }

    [Obsolete("Use RuntimeDiagnosticsService.", false)]
    public sealed class WpfRuntimeDiagnosticsService : RuntimeDiagnosticsService
    {
        public WpfRuntimeDiagnosticsService()
            : base()
        {
        }

        public WpfRuntimeDiagnosticsService(string applicationRoot, string applicationDataRoot)
            : base(applicationRoot, applicationDataRoot)
        {
        }

        internal WpfRuntimeDiagnosticsService(WpfRuntimeDiagnosticsPaths paths)
            : base(paths?.ToCanonical())
        {
        }

        internal WpfRuntimeDiagnosticsService(
            WpfRuntimeDiagnosticsPaths paths,
            Func<WpfRuntimeSelfTestCheck> graphicsCapabilityProvider)
            : base(paths?.ToCanonical(), () => WpfRuntimeSelfTestCheck.ToCanonical(graphicsCapabilityProvider?.Invoke()))
        {
        }

        public new WpfRuntimeDiagnosticsPaths Paths => WpfRuntimeDiagnosticsPaths.FromCanonical(base.Paths);

        public static new WpfRuntimeStartupResult ConfigureApplicationStartup()
        {
            return WpfRuntimeStartupResult.FromCanonical(RuntimeDiagnosticsService.ConfigureApplicationStartup());
        }

        public new WpfRuntimeSelfTestResult RunSelfTest(bool persistResult = true)
        {
            return WpfRuntimeSelfTestResult.FromCanonical(base.RunSelfTest(persistResult));
        }

        public new WpfRuntimeSelfTestResult RunReadOnlySelfTest()
        {
            return WpfRuntimeSelfTestResult.FromCanonical(base.RunReadOnlySelfTest());
        }

        public new WpfSupportBundleResult CreateSupportBundle()
        {
            return WpfSupportBundleResult.FromCanonical(base.CreateSupportBundle());
        }

        public void SetGraphicsCapabilityProvider(Func<WpfRuntimeSelfTestCheck> provider)
        {
            if (provider == null)
            {
                base.SetGraphicsCapabilityProvider(null);
                return;
            }

            base.SetGraphicsCapabilityProvider(() => WpfRuntimeSelfTestCheck.ToCanonical(provider()));
        }
    }

    [Obsolete("Use RuntimeDiagnosticsPaths.", false)]
    public sealed class WpfRuntimeDiagnosticsPaths
    {
        private readonly RuntimeDiagnosticsPaths inner;

        private WpfRuntimeDiagnosticsPaths(RuntimeDiagnosticsPaths inner)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public string ApplicationRoot => inner.ApplicationRoot;
        public string ApplicationDataRoot => inner.ApplicationDataRoot;
        public string LogDirectory => inner.LogDirectory;
        public string DiagnosticsDirectory => inner.DiagnosticsDirectory;
        public string SupportBundlesDirectory => inner.SupportBundlesDirectory;
        public string ConfigDirectory => inner.ConfigDirectory;
        public string LatestSelfTestPath => inner.LatestSelfTestPath;

        public static WpfRuntimeDiagnosticsPaths Resolve(string applicationRoot = null, string applicationDataRoot = null)
        {
            return new WpfRuntimeDiagnosticsPaths(RuntimeDiagnosticsPaths.Resolve(applicationRoot, applicationDataRoot));
        }

        public void EnsureDirectories()
        {
            inner.EnsureDirectories();
        }

        internal RuntimeDiagnosticsPaths ToCanonical()
        {
            return inner;
        }

        internal static WpfRuntimeDiagnosticsPaths FromCanonical(RuntimeDiagnosticsPaths value)
        {
            return new WpfRuntimeDiagnosticsPaths(value);
        }
    }

    [Obsolete("Use RuntimeStartupResult.", false)]
    public sealed class WpfRuntimeStartupResult
    {
        public WpfRuntimeStartupResult(bool succeeded, string diagnosticsPath, string error)
        {
            Succeeded = succeeded;
            DiagnosticsPath = diagnosticsPath ?? string.Empty;
            Error = error ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string DiagnosticsPath { get; }
        public string Error { get; }

        internal static WpfRuntimeStartupResult FromCanonical(RuntimeStartupResult value)
        {
            return value == null ? null : new WpfRuntimeStartupResult(value.Succeeded, value.DiagnosticsPath, value.Error);
        }
    }

    [Obsolete("Use RuntimeSelfTestCheck.", false)]
    public sealed class WpfRuntimeSelfTestCheck
    {
        public WpfRuntimeSelfTestCheck(string name, string status, string detail)
        {
            Name = name ?? string.Empty;
            Status = status ?? "fail";
            Detail = detail ?? string.Empty;
        }

        public string Name { get; }
        public string Status { get; }
        public string Detail { get; }

        internal RuntimeSelfTestCheck ToCanonical()
        {
            return new RuntimeSelfTestCheck(Name, Status, Detail);
        }

        internal static RuntimeSelfTestCheck ToCanonical(WpfRuntimeSelfTestCheck value)
        {
            return value?.ToCanonical();
        }

        internal static WpfRuntimeSelfTestCheck FromCanonical(RuntimeSelfTestCheck value)
        {
            return value == null ? null : new WpfRuntimeSelfTestCheck(value.Name, value.Status, value.Detail);
        }
    }

    [Obsolete("Use RuntimeSelfTestResult.", false)]
    public sealed class WpfRuntimeSelfTestResult
    {
        public WpfRuntimeSelfTestResult(
            DateTimeOffset checkedAt,
            string product,
            string productVersion,
            string operatingSystem,
            string framework,
            string processArchitecture,
            IReadOnlyList<WpfRuntimeSelfTestCheck> checks)
        {
            CheckedAt = checkedAt;
            Product = product ?? string.Empty;
            ProductVersion = productVersion ?? string.Empty;
            OperatingSystem = operatingSystem ?? string.Empty;
            Framework = framework ?? string.Empty;
            ProcessArchitecture = processArchitecture ?? string.Empty;
            Checks = checks ?? Array.Empty<WpfRuntimeSelfTestCheck>();
        }

        public DateTimeOffset CheckedAt { get; }
        public string Product { get; }
        public string ProductVersion { get; }
        public string OperatingSystem { get; }
        public string Framework { get; }
        public string ProcessArchitecture { get; }
        public IReadOnlyList<WpfRuntimeSelfTestCheck> Checks { get; }
        public int PassedCount => Checks.Count(check => string.Equals(check.Status, "pass", StringComparison.Ordinal));
        public int WarningCount => Checks.Count(check => string.Equals(check.Status, "warning", StringComparison.Ordinal));
        public int FailedCount => Checks.Count(check => string.Equals(check.Status, "fail", StringComparison.Ordinal));
        public string OverallStatus => FailedCount > 0 ? "fail" : WarningCount > 0 ? "warning" : "pass";

        internal RuntimeSelfTestResult ToCanonical()
        {
            return new RuntimeSelfTestResult(
                CheckedAt,
                Product,
                ProductVersion,
                OperatingSystem,
                Framework,
                ProcessArchitecture,
                Checks.Select(WpfRuntimeSelfTestCheck.ToCanonical).ToArray());
        }

        internal static WpfRuntimeSelfTestResult FromCanonical(RuntimeSelfTestResult value)
        {
            return value == null
                ? null
                : new WpfRuntimeSelfTestResult(
                    value.CheckedAt,
                    value.Product,
                    value.ProductVersion,
                    value.OperatingSystem,
                    value.Framework,
                    value.ProcessArchitecture,
                    value.Checks.Select(WpfRuntimeSelfTestCheck.FromCanonical).ToArray());
        }
    }

    [Obsolete("Use SupportBundleResult.", false)]
    public sealed class WpfSupportBundleResult
    {
        public WpfSupportBundleResult(
            string archivePath,
            WpfRuntimeSelfTestResult selfTest,
            IReadOnlyList<string> includedEntries,
            IReadOnlyList<string> skippedLogs)
        {
            ArchivePath = archivePath ?? string.Empty;
            SelfTest = selfTest;
            IncludedEntries = includedEntries ?? Array.Empty<string>();
            SkippedLogs = skippedLogs ?? Array.Empty<string>();
        }

        public string ArchivePath { get; }
        public WpfRuntimeSelfTestResult SelfTest { get; }
        public IReadOnlyList<string> IncludedEntries { get; }
        public IReadOnlyList<string> SkippedLogs { get; }

        internal static WpfSupportBundleResult FromCanonical(SupportBundleResult value)
        {
            return value == null
                ? null
                : new WpfSupportBundleResult(
                    value.ArchivePath,
                    WpfRuntimeSelfTestResult.FromCanonical(value.SelfTest),
                    value.IncludedEntries,
                    value.SkippedLogs);
        }
    }
}
