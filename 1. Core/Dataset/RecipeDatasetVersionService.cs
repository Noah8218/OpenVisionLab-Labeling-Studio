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
        private static readonly HashSet<string> ImageExtensions = new HashSet<string>(
            new[] { ".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff" },
            StringComparer.OrdinalIgnoreCase);
        private static readonly string[] Splits = { "train", "valid", "test" };

        public static RecipeDatasetVersionSnapshot CreateSnapshot(LabelingProjectData data)
        {
            data ??= new LabelingProjectData();
            data.NormalizeOutputPaths();
            data.ProjectSettings ??= new LabelingProjectSettings();
            data.ProjectSettings.EnsureDefaults();

            var snapshot = new RecipeDatasetVersionSnapshot
            {
                CapturedUtc = DateTime.UtcNow.ToString("O"),
                DatasetPurpose = data.ProjectSettings.DatasetPurpose.ToString(),
                Classes = data.ClassNamedList?
                    .Select(item => item?.Text?.Trim())
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToList() ?? new List<string>()
            };

            foreach (string split in Splits)
            {
                string splitRoot = Path.Combine(data.OutputRootPath, "data", split);
                AddFiles(snapshot.Files, Path.Combine(splitRoot, "images"), "image", split, data.OutputRootPath, IsImageFile);
                AddFiles(snapshot.Files, Path.Combine(splitRoot, "labels"), "label", split, data.OutputRootPath, _ => true);
                AddFiles(snapshot.Files, Path.Combine(splitRoot, "segments"), "segment", split, data.OutputRootPath, _ => true);
                AddFiles(snapshot.Files, Path.Combine(splitRoot, "masks"), "mask", split, data.OutputRootPath, _ => true);
            }

            if (data.ProjectSettings.DatasetPurpose == LabelingDatasetPurpose.AnomalyDetection)
            {
                foreach (string split in Splits)
                {
                    AddFiles(
                        snapshot.Files,
                        Path.Combine(data.OutputRootPath, AnomalyClassificationDatasetExportService.DefaultFolderName, split),
                        "image",
                        split,
                        data.OutputRootPath,
                        IsImageFile);
                }
            }

            if (!snapshot.Files.Any(item => string.Equals(item.Kind, "image", StringComparison.Ordinal)))
            {
                string imageRootPath = data.ProjectSettings.ResolveImageRootPath();
                AddFiles(snapshot.Files, imageRootPath, "image", "source", imageRootPath, IsImageFile);
            }

            string anomalyReviewPath = Path.Combine(data.OutputRootPath, AnomalyImageReviewStatusService.FileName);
            if (File.Exists(anomalyReviewPath))
            {
                snapshot.Files.Add(CreateFileRecord(
                    anomalyReviewPath,
                    "image-level-label",
                    "source",
                    AnomalyImageReviewStatusService.FileName));
            }

            snapshot.Files = snapshot.Files
                .OrderBy(item => item.Kind, StringComparer.Ordinal)
                .ThenBy(item => item.Split, StringComparer.Ordinal)
                .ThenBy(item => item.RelativePath, StringComparer.Ordinal)
                .ToList();
            snapshot.FileCount = snapshot.Files.Count;
            snapshot.ImageFileCount = snapshot.Files.Count(item => string.Equals(item.Kind, "image", StringComparison.Ordinal));
            snapshot.AnnotationFileCount = snapshot.FileCount - snapshot.ImageFileCount;
            snapshot.ClassContractSha256 = HashingService.ComputeUtf8TextSha256(BuildClassContract(snapshot.Classes), lowerCase: true);
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

            if (snapshot == null
                || string.IsNullOrWhiteSpace(snapshot.DatasetVersionId)
                || string.IsNullOrWhiteSpace(snapshot.ContentSha256))
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
                catch (IOException)
                {
                }
                catch (JsonException)
                {
                }
                catch (UnauthorizedAccessException)
                {
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

        private static void AddFiles(
            ICollection<RecipeDatasetVersionFile> records,
            string directoryPath,
            string kind,
            string split,
            string relativeRoot,
            Func<string, bool> include)
        {
            if (records == null || !Directory.Exists(directoryPath))
            {
                return;
            }

            foreach (string path in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
            {
                if (include?.Invoke(path) == false)
                {
                    continue;
                }

                string relativePath = Path.GetRelativePath(relativeRoot, path)
                    .Replace(Path.DirectorySeparatorChar, '/')
                    .Replace(Path.AltDirectorySeparatorChar, '/');
                records.Add(CreateFileRecord(path, kind, split, relativePath));
            }
        }

        private static RecipeDatasetVersionFile CreateFileRecord(
            string path,
            string kind,
            string split,
            string relativePath)
        {
            var fileInfo = new FileInfo(path);
            return new RecipeDatasetVersionFile
            {
                Kind = kind ?? string.Empty,
                Split = split ?? string.Empty,
                RelativePath = relativePath ?? string.Empty,
                Length = fileInfo.Length,
                Sha256 = HashingService.ComputeFileSha256(path, lowerCase: true)
            };
        }

        private static bool IsImageFile(string path)
            => ImageExtensions.Contains(Path.GetExtension(path) ?? string.Empty);

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
