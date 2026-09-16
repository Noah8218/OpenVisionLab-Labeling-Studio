using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MvcVisionSystem.Yolo;

namespace MvcVisionSystem
{
    public static class RecipeDatasetVersionService
    {
        public const int IdentitySchemaVersion = 2;
        public const string Algorithm = "sha256-relative-path-content-v2";
        public const string HistoryDirectoryName = "dataset.versions";
        internal const int MaxSnapshotAttempts = 2;
        private static readonly HashSet<string> ImageExtensions = new HashSet<string>(
            new[] { ".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff" },
            StringComparer.OrdinalIgnoreCase);
        private static readonly string[] Splits = { "train", "valid", "test" };

        [ThreadStatic]
        private static Action<string> testSnapshotFileObserver;

        private sealed class DatasetFileCandidate
        {
            public string Path { get; set; } = string.Empty;
            public string Kind { get; set; } = string.Empty;
            public string Split { get; set; } = string.Empty;
            public string RelativePath { get; set; } = string.Empty;
            public long Length { get; set; }
            public DateTime LastWriteTimeUtc { get; set; }
            public DateTime CreationTimeUtc { get; set; }
            public string Key => Kind + "\0" + Split + "\0" + Path;
        }

        private sealed class DatasetSnapshotObserverScope : IDisposable
        {
            private readonly Action<string> previous;
            private bool disposed;

            public DatasetSnapshotObserverScope(Action<string> previous)
            {
                this.previous = previous;
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                testSnapshotFileObserver = previous;
            }
        }

        private sealed class DatasetSnapshotChangedException : IOException
        {
            public DatasetSnapshotChangedException(string message)
                : base(message)
            {
            }
        }

        internal static IDisposable PushTestSnapshotFileObserver(Action<string> observer)
        {
            ArgumentNullException.ThrowIfNull(observer);
            Action<string> previous = testSnapshotFileObserver;
            testSnapshotFileObserver = observer;
            return new DatasetSnapshotObserverScope(previous);
        }

        public static RecipeDatasetVersionSnapshot CreateSnapshot(LabelingProjectData data)
        {
            data ??= new LabelingProjectData();
            data.NormalizeOutputPaths();
            data.ProjectSettings ??= new LabelingProjectSettings();
            data.ProjectSettings.EnsureDefaults();

            IOException lastFailure = null;
            for (int attempt = 1; attempt <= MaxSnapshotAttempts; attempt++)
            {
                try
                {
                    return CreateSnapshotAttempt(data);
                }
                catch (IOException error)
                {
                    lastFailure = error;
                    if (attempt == MaxSnapshotAttempts)
                    {
                        throw new IOException(
                            $"Dataset changed or could not be read consistently after {MaxSnapshotAttempts} attempts.",
                            error);
                    }
                }
            }

            throw new IOException("Dataset snapshot could not be created.", lastFailure);
        }

        private static RecipeDatasetVersionSnapshot CreateSnapshotAttempt(LabelingProjectData data)
        {
            List<DatasetFileCandidate> beforeCandidates = EnumerateDatasetFiles(data);

            var snapshot = new RecipeDatasetVersionSnapshot
            {
                CapturedUtc = DateTime.UtcNow.ToString("O"),
                DatasetPurpose = data.ProjectSettings.DatasetPurpose.ToString(),
                Classes = data.ClassNamedList?
                    .Select(item => item?.Text?.Trim())
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToList() ?? new List<string>()
            };

            foreach (DatasetFileCandidate candidate in beforeCandidates)
            {
                snapshot.Files.Add(CreateFileRecord(candidate));
            }

            EnsureDatasetFilesStable(beforeCandidates, EnumerateDatasetFiles(data));

            snapshot.Files = snapshot.Files
                .OrderBy(item => item.Kind, StringComparer.Ordinal)
                .ThenBy(item => item.Split, StringComparer.Ordinal)
                .ThenBy(item => item.RelativePath, StringComparer.Ordinal)
                .ToList();
            snapshot.FileCount = snapshot.Files.Count;
            snapshot.ImageFileCount = snapshot.Files.Count(item => string.Equals(item.Kind, "image", StringComparison.Ordinal));
            snapshot.AnnotationFileCount = snapshot.FileCount - snapshot.ImageFileCount;
            snapshot.ClassContractSha256 = ComputeClassContractSha256(snapshot.Classes);
            snapshot.SplitContractSha256 = HashingService.ComputeUtf8TextSha256(BuildFileContract(snapshot.Files), lowerCase: true);
            snapshot.ContentSha256 = HashingService.ComputeUtf8TextSha256(string.Join(
                "\n",
                "recipe-dataset-version-v2",
                snapshot.DatasetPurpose,
                snapshot.ClassContractSha256,
                snapshot.SplitContractSha256), lowerCase: true);
            snapshot.DatasetVersionId = "dsv2-" + snapshot.ContentSha256.ToLowerInvariant();
            return snapshot;
        }

        public static RecipeDatasetVersionSnapshot RecordSnapshot(string recipeDirectory, RecipeDatasetVersionSnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(recipeDirectory))
            {
                throw new ArgumentException("Recipe directory is required.", nameof(recipeDirectory));
            }

            if (!HasCanonicalIdentity(snapshot))
            {
                throw new ArgumentException("A complete dataset version snapshot is required.", nameof(snapshot));
            }

            string historyDirectory = Path.Combine(recipeDirectory, HistoryDirectoryName);
            Directory.CreateDirectory(historyDirectory);
            string snapshotPath = Path.Combine(historyDirectory, snapshot.DatasetVersionId + ".json");
            if (File.Exists(snapshotPath))
            {
                return LoadAndValidateSnapshot(snapshotPath, snapshot);
            }

            string temporaryPath = snapshotPath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temporaryPath, JsonConvert.SerializeObject(snapshot, Formatting.Indented));
                using (var stream = new FileStream(temporaryPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    stream.Flush(flushToDisk: true);
                }
                try
                {
                    File.Move(temporaryPath, snapshotPath);
                    return snapshot;
                }
                catch (IOException) when (File.Exists(snapshotPath))
                {
                    return LoadAndValidateSnapshot(snapshotPath, snapshot);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        public static IReadOnlyList<RecipeDatasetVersionSnapshot> LoadHistory(string recipeDirectory)
        {
            string historyDirectory = string.IsNullOrWhiteSpace(recipeDirectory)
                ? string.Empty
                : Path.Combine(recipeDirectory, HistoryDirectoryName);
            if (!Directory.Exists(historyDirectory))
            {
                return Array.Empty<RecipeDatasetVersionSnapshot>();
            }

            var snapshots = new List<RecipeDatasetVersionSnapshot>();
            foreach (string path in Directory.EnumerateFiles(historyDirectory, "dsv2-*.json", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    RecipeDatasetVersionSnapshot snapshot =
                        JsonConvert.DeserializeObject<RecipeDatasetVersionSnapshot>(File.ReadAllText(path));
                    if (snapshot != null
                        && snapshot.IdentitySchemaVersion == IdentitySchemaVersion
                        && string.Equals(snapshot.Algorithm, Algorithm, StringComparison.Ordinal)
                        && !string.IsNullOrWhiteSpace(snapshot.DatasetVersionId)
                        && !string.IsNullOrWhiteSpace(snapshot.ContentSha256))
                    {
                        string fileName = Path.GetFileNameWithoutExtension(path);
                        if (string.Equals(fileName, snapshot.DatasetVersionId, StringComparison.Ordinal)
                            && HasCanonicalIdentity(snapshot))
                        {
                            snapshots.Add(snapshot);
                        }
                    }
                }
                catch (IOException error)
                {
                    AppLog.ABNORMAL($"Dataset version history could not be read: {path} / {error.Message}");
                }
                catch (JsonException error)
                {
                    AppLog.ABNORMAL($"Dataset version history is corrupt: {path} / {error.Message}");
                }
                catch (UnauthorizedAccessException error)
                {
                    AppLog.ABNORMAL($"Dataset version history access denied: {path} / {error.Message}");
                }
            }

            return snapshots
                .OrderByDescending(item => item.CapturedUtc, StringComparer.Ordinal)
                .ToList();
        }

        public static string BuildExternalDatasetVersionId(string sourceFingerprintSha256)
        {
            string fingerprint = sourceFingerprintSha256?.Trim().ToLowerInvariant() ?? string.Empty;
            return string.IsNullOrWhiteSpace(fingerprint)
                ? string.Empty
                : "dsv2-external-yolo-" + fingerprint;
        }

        private static RecipeDatasetVersionSnapshot LoadAndValidateSnapshot(
            string snapshotPath,
            RecipeDatasetVersionSnapshot expected)
        {
            RecipeDatasetVersionSnapshot stored =
                JsonConvert.DeserializeObject<RecipeDatasetVersionSnapshot>(File.ReadAllText(snapshotPath));
            if (!HasCanonicalIdentity(stored)
                || !HasCanonicalIdentity(expected)
                || !string.Equals(Path.GetFileNameWithoutExtension(snapshotPath), stored?.DatasetVersionId, StringComparison.Ordinal)
                || !string.Equals(stored.DatasetVersionId, expected.DatasetVersionId, StringComparison.Ordinal)
                || !string.Equals(stored.ContentSha256, expected.ContentSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Dataset version history contains a conflicting immutable snapshot: " + snapshotPath);
            }

            return stored;
        }

        private static bool HasCanonicalIdentity(RecipeDatasetVersionSnapshot snapshot)
        {
            if (snapshot == null
                || snapshot.IdentitySchemaVersion != IdentitySchemaVersion
                || !string.Equals(snapshot.Algorithm, Algorithm, StringComparison.Ordinal)
                || snapshot.Classes == null
                || snapshot.Files == null
                || snapshot.FileCount != snapshot.Files.Count
                || snapshot.ImageFileCount != snapshot.Files.Count(file => file?.Kind == "image")
                || snapshot.AnnotationFileCount != snapshot.FileCount - snapshot.ImageFileCount
                || snapshot.Classes.Any(string.IsNullOrWhiteSpace)
                || snapshot.Files.Any(file => file == null
                    || string.IsNullOrWhiteSpace(file.Kind)
                    || string.IsNullOrWhiteSpace(file.Split)
                    || string.IsNullOrWhiteSpace(file.RelativePath)
                    || string.IsNullOrWhiteSpace(file.Sha256)
                    || file.Length < 0))
            {
                return false;
            }

            string classContractSha256 = HashingService.ComputeUtf8TextSha256(
                BuildClassContract(snapshot.Classes),
                lowerCase: true);
            string splitContractSha256 = HashingService.ComputeUtf8TextSha256(
                BuildFileContract(snapshot.Files),
                lowerCase: true);
            string contentSha256 = HashingService.ComputeUtf8TextSha256(
                string.Join(
                    "\n",
                    "recipe-dataset-version-v2",
                    snapshot.DatasetPurpose,
                    classContractSha256,
                    splitContractSha256),
                lowerCase: true);
            string datasetVersionId = "dsv2-" + contentSha256.ToLowerInvariant();
            return string.Equals(snapshot.ClassContractSha256, classContractSha256, StringComparison.OrdinalIgnoreCase)
                && string.Equals(snapshot.SplitContractSha256, splitContractSha256, StringComparison.OrdinalIgnoreCase)
                && string.Equals(snapshot.ContentSha256, contentSha256, StringComparison.OrdinalIgnoreCase)
                && string.Equals(snapshot.DatasetVersionId, datasetVersionId, StringComparison.Ordinal);
        }

        private static List<DatasetFileCandidate> EnumerateDatasetFiles(LabelingProjectData data)
        {
            var candidates = new List<DatasetFileCandidate>();
            foreach (string split in Splits)
            {
                string splitRoot = Path.Combine(data.OutputRootPath, "data", split);
                AddFileCandidates(candidates, Path.Combine(splitRoot, "images"), "image", split, data.OutputRootPath, IsImageFile);
                AddFileCandidates(candidates, Path.Combine(splitRoot, "labels"), "label", split, data.OutputRootPath, _ => true);
                AddFileCandidates(candidates, Path.Combine(splitRoot, "segments"), "segment", split, data.OutputRootPath, _ => true);
                AddFileCandidates(candidates, Path.Combine(splitRoot, "masks"), "mask", split, data.OutputRootPath, _ => true);
            }

            if (data.ProjectSettings.DatasetPurpose == LabelingDatasetPurpose.AnomalyDetection)
            {
                foreach (string split in Splits)
                {
                    AddFileCandidates(
                        candidates,
                        Path.Combine(data.OutputRootPath, AnomalyClassificationDatasetExportService.DefaultFolderName, split),
                        "image",
                        split,
                        data.OutputRootPath,
                        IsImageFile);
                }
            }

            if (!candidates.Any(item => string.Equals(item.Kind, "image", StringComparison.Ordinal)))
            {
                string imageRootPath = data.ProjectSettings.ResolveImageRootPath();
                AddFileCandidates(candidates, imageRootPath, "image", "source", imageRootPath, IsImageFile);
            }

            string anomalyReviewPath = Path.Combine(data.OutputRootPath, AnomalyImageReviewStatusService.FileName);
            if (File.Exists(anomalyReviewPath))
            {
                candidates.Add(CreateFileCandidate(
                    anomalyReviewPath,
                    "image-level-label",
                    "source",
                    AnomalyImageReviewStatusService.FileName));
            }

            return candidates
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .ToList();
        }

        private static void AddFileCandidates(
            ICollection<DatasetFileCandidate> candidates,
            string directoryPath,
            string kind,
            string split,
            string relativeRoot,
            Func<string, bool> include)
        {
            if (candidates == null || !Directory.Exists(directoryPath))
            {
                return;
            }

            foreach (string path in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
            {
                if (IsSaveScratchFile(path) || include?.Invoke(path) == false)
                {
                    continue;
                }

                string relativePath = Path.GetRelativePath(relativeRoot, path)
                    .Replace(Path.DirectorySeparatorChar, '/')
                    .Replace(Path.AltDirectorySeparatorChar, '/');
                candidates.Add(CreateFileCandidate(path, kind, split, relativePath));
            }
        }

        private static DatasetFileCandidate CreateFileCandidate(
            string path,
            string kind,
            string split,
            string relativePath)
        {
            string normalizedPath = Path.GetFullPath(path);
            FileInfo fileInfo = ReadFileInfo(normalizedPath);
            return new DatasetFileCandidate
            {
                Path = normalizedPath,
                Kind = kind ?? string.Empty,
                Split = split ?? string.Empty,
                RelativePath = relativePath ?? string.Empty,
                Length = fileInfo.Length,
                LastWriteTimeUtc = fileInfo.LastWriteTimeUtc,
                CreationTimeUtc = fileInfo.CreationTimeUtc
            };
        }

        private static RecipeDatasetVersionFile CreateFileRecord(DatasetFileCandidate candidate)
        {
            FileInfo beforeHash = ReadFileInfo(candidate.Path);
            EnsureFileStateMatches(candidate, beforeHash);
            testSnapshotFileObserver?.Invoke(candidate.Path);
            string sha256 = HashingService.ComputeFileSha256(candidate.Path, lowerCase: true);
            FileInfo afterHash = ReadFileInfo(candidate.Path);
            EnsureFileStateMatches(candidate, afterHash);
            return new RecipeDatasetVersionFile
            {
                Kind = candidate.Kind,
                Split = candidate.Split,
                RelativePath = candidate.RelativePath,
                Length = beforeHash.Length,
                Sha256 = sha256
            };
        }

        private static FileInfo ReadFileInfo(string path)
        {
            var fileInfo = new FileInfo(path);
            fileInfo.Refresh();
            if (!fileInfo.Exists)
            {
                throw new DatasetSnapshotChangedException("Dataset file disappeared while its fingerprint was being captured: " + path);
            }

            return fileInfo;
        }

        private static void EnsureFileStateMatches(DatasetFileCandidate expected, FileInfo actual)
        {
            if (expected.Length != actual.Length
                || expected.LastWriteTimeUtc != actual.LastWriteTimeUtc
                || expected.CreationTimeUtc != actual.CreationTimeUtc)
            {
                throw new DatasetSnapshotChangedException("Dataset file changed while its fingerprint was being captured: " + expected.Path);
            }
        }

        private static void EnsureDatasetFilesStable(
            IReadOnlyList<DatasetFileCandidate> before,
            IReadOnlyList<DatasetFileCandidate> after)
        {
            if (before == null || after == null || before.Count != after.Count)
            {
                throw new DatasetSnapshotChangedException("Dataset file membership changed while its fingerprint was being captured.");
            }

            var afterByKey = new Dictionary<string, DatasetFileCandidate>(StringComparer.Ordinal);
            foreach (DatasetFileCandidate candidate in after)
            {
                if (!afterByKey.TryAdd(candidate.Key, candidate))
                {
                    throw new DatasetSnapshotChangedException("Dataset file identity was duplicated while its fingerprint was being captured.");
                }
            }
            foreach (DatasetFileCandidate candidate in before)
            {
                if (!afterByKey.TryGetValue(candidate.Key, out DatasetFileCandidate afterCandidate)
                    || candidate.Length != afterCandidate.Length
                    || candidate.LastWriteTimeUtc != afterCandidate.LastWriteTimeUtc
                    || candidate.CreationTimeUtc != afterCandidate.CreationTimeUtc)
                {
                    throw new DatasetSnapshotChangedException("Dataset file membership or metadata changed while its fingerprint was being captured.");
                }
            }
        }

        private static bool IsImageFile(string path)
            => ImageExtensions.Contains(Path.GetExtension(path) ?? string.Empty);

        public static string ComputeClassContractSha256(IReadOnlyList<string> classes)
            => HashingService.ComputeUtf8TextSha256(BuildClassContract(classes), lowerCase: true);

        private static bool IsSaveScratchFile(string path)
        {
            string name = Path.GetFileName(path);
            int marker = name.LastIndexOf(".tmp-", StringComparison.Ordinal);
            int markerLength = 5;
            if (marker < 0)
            {
                marker = name.LastIndexOf(".rollback-", StringComparison.Ordinal);
                markerLength = 10;
            }

            return name.StartsWith(".", StringComparison.Ordinal) && marker > 0
                && Guid.TryParseExact(name.Substring(marker + markerLength), "N", out _);
        }

        private static string BuildClassContract(IReadOnlyList<string> classes)
        {
            var builder = new StringBuilder("classes-v2\n");
            for (int index = 0; index < (classes?.Count ?? 0); index++)
            {
                string value = classes[index] ?? string.Empty;
                builder.Append(index).Append(':').Append(value.Length).Append(':').Append(value).Append('\n');
            }

            return builder.ToString();
        }

        private static string BuildFileContract(IEnumerable<RecipeDatasetVersionFile> files)
        {
            var builder = new StringBuilder("splits-v2\ntrain\nvalid\ntest\nsource\n");
            foreach (RecipeDatasetVersionFile file in files ?? Array.Empty<RecipeDatasetVersionFile>())
            {
                builder
                    .Append(file.Kind?.Length ?? 0).Append(':').Append(file.Kind)
                    .Append('|').Append(file.Split?.Length ?? 0).Append(':').Append(file.Split)
                    .Append('|').Append(file.RelativePath?.Length ?? 0).Append(':').Append(file.RelativePath)
                    .Append('|').Append(file.Length)
                    .Append('|').Append(file.Sha256)
                    .Append('\n');
            }

            return builder.ToString();
        }

    }
}
