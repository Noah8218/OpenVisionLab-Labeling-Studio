using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MvcVisionSystem.Yolo
{
    /// <summary>
    /// Owns the optional dataset source-group sidecar contract. It validates
    /// explicit lineage only; it never derives a group or Lot from a filename.
    /// </summary>
    public static class YoloDatasetSourceGroupService
    {
        public const int SchemaVersion = 1;

        public const string FileName = "dataset.source-groups.json";

        public const string Unknown = "Unknown";

        public static string GetSidecarPath(string datasetRootPath)
        {
            return Path.Combine(datasetRootPath ?? string.Empty, FileName);
        }

        public static YoloDatasetSourceGroupLoadResult LoadForDatasetRoot(string datasetRootPath)
            => Load(GetSidecarPath(datasetRootPath));

        public static YoloDatasetSourceGroupLoadResult Load(string sidecarPath)
        {
            string normalizedPath = sidecarPath ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedPath) || !File.Exists(normalizedPath))
            {
                return new YoloDatasetSourceGroupLoadResult(
                    normalizedPath,
                    isAvailable: false,
                    isValid: true,
                    new YoloDatasetSourceGroupSidecar(),
                    Array.Empty<string>());
            }

            try
            {
                YoloDatasetSourceGroupSidecar sidecar = JsonConvert.DeserializeObject<YoloDatasetSourceGroupSidecar>(
                    File.ReadAllText(normalizedPath));
                YoloDatasetSourceGroupValidationResult validation = Validate(sidecar);
                return new YoloDatasetSourceGroupLoadResult(
                    normalizedPath,
                    isAvailable: true,
                    isValid: validation.IsValid,
                    sidecar ?? new YoloDatasetSourceGroupSidecar(),
                    validation.Errors);
            }
            catch (Exception error) when (error is IOException
                || error is UnauthorizedAccessException
                || error is JsonException)
            {
                return new YoloDatasetSourceGroupLoadResult(
                    normalizedPath,
                    isAvailable: true,
                    isValid: false,
                    new YoloDatasetSourceGroupSidecar(),
                    new[] { $"Source-group sidecar could not be read: {error.Message}" });
            }
        }

        public static YoloDatasetSourceGroupValidationResult Validate(YoloDatasetSourceGroupSidecar sidecar)
        {
            var errors = new List<string>();
            if (sidecar == null)
            {
                errors.Add("Source-group sidecar is empty.");
                return new YoloDatasetSourceGroupValidationResult(errors);
            }

            if (sidecar.SchemaVersion != SchemaVersion)
            {
                errors.Add($"Unsupported source-group sidecar schema version: {sidecar.SchemaVersion}. Expected {SchemaVersion}.");
            }

            IReadOnlyList<YoloDatasetSourceGroupEntry> entries = sidecar.Entries ?? new List<YoloDatasetSourceGroupEntry>();
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var sourceIds = new Dictionary<string, YoloDatasetSourceGroupEntry>(StringComparer.Ordinal);

            for (int index = 0; index < entries.Count; index++)
            {
                YoloDatasetSourceGroupEntry entry = entries[index];
                if (entry == null)
                {
                    errors.Add($"Source-group sidecar entry {index + 1} is null.");
                    continue;
                }

                string relativePath = NormalizeRelativeImagePath(entry.RelativeImagePath);
                if (string.IsNullOrWhiteSpace(relativePath))
                {
                    errors.Add($"Source-group sidecar entry {index + 1} has an invalid relativeImagePath.");
                }
                else if (!paths.Add(relativePath))
                {
                    errors.Add($"Duplicate source-group relativeImagePath: {relativePath}");
                }

                string sourceId = NormalizeId(entry.SourceId);
                string parentSourceId = NormalizeId(entry.ParentSourceId);
                if (!string.IsNullOrWhiteSpace(parentSourceId) && string.IsNullOrWhiteSpace(sourceId))
                {
                    errors.Add($"Source-group entry '{relativePath}' has a parentSourceId but no sourceId.");
                }

                if (!string.IsNullOrWhiteSpace(sourceId))
                {
                    if (!sourceIds.TryAdd(sourceId, entry))
                    {
                        errors.Add($"Duplicate sourceId: {sourceId}");
                    }
                }

                if (HasControlWhitespace(entry.RelativeImagePath)
                    || HasControlWhitespace(entry.SourceId)
                    || HasControlWhitespace(entry.SourceGroupId)
                    || HasControlWhitespace(entry.ParentSourceId)
                    || HasControlWhitespace(entry.Transformation)
                    || HasControlWhitespace(entry.CaptureBundleId))
                {
                    errors.Add($"Source-group entry '{relativePath}' contains control whitespace.");
                }
            }

            foreach (KeyValuePair<string, YoloDatasetSourceGroupEntry> pair in sourceIds)
            {
                string parentSourceId = NormalizeId(pair.Value.ParentSourceId);
                if (!string.IsNullOrWhiteSpace(parentSourceId) && !sourceIds.ContainsKey(parentSourceId))
                {
                    errors.Add($"Source-group entry '{pair.Key}' references missing parentSourceId: {parentSourceId}");
                }
            }

            foreach (string sourceId in sourceIds.Keys)
            {
                DetectCycle(sourceId, sourceIds, new HashSet<string>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal), errors);
            }

            return new YoloDatasetSourceGroupValidationResult(errors);
        }

        public static void Save(string sidecarPath, YoloDatasetSourceGroupSidecar sidecar)
        {
            YoloDatasetSourceGroupValidationResult validation = Validate(sidecar);
            if (!validation.IsValid)
            {
                throw new InvalidDataException(
                    "Source-group sidecar is invalid: " + string.Join(" ", validation.Errors));
            }

            if (string.IsNullOrWhiteSpace(sidecarPath))
            {
                throw new ArgumentException("Source-group sidecar path is required.", nameof(sidecarPath));
            }

            string directory = Path.GetDirectoryName(sidecarPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            AnnotationFilePersistence.WriteAtomically(
                sidecarPath,
                temporaryPath => File.WriteAllText(
                    temporaryPath,
                    JsonConvert.SerializeObject(sidecar, Formatting.Indented)));
        }

        public static YoloDatasetSourceGroupResolution Resolve(
            YoloDatasetSourceGroupLoadResult loadResult,
            string relativeImagePath)
        {
            string normalizedPath = NormalizeRelativeImagePath(relativeImagePath);
            if (loadResult == null || !loadResult.IsValid || string.IsNullOrWhiteSpace(normalizedPath))
            {
                return UnknownResolution(normalizedPath);
            }

            YoloDatasetSourceGroupEntry entry = loadResult.Entries
                .FirstOrDefault(item => string.Equals(
                    NormalizeRelativeImagePath(item?.RelativeImagePath),
                    normalizedPath,
                    StringComparison.OrdinalIgnoreCase));
            if (entry == null)
            {
                return UnknownResolution(normalizedPath);
            }

            var bySourceId = loadResult.Entries
                .Where(item => item != null && !string.IsNullOrWhiteSpace(NormalizeId(item.SourceId)))
                .ToDictionary(item => NormalizeId(item.SourceId), item => item, StringComparer.Ordinal);
            var lineage = new List<string>();
            YoloDatasetSourceGroupEntry current = entry;
            string sourceGroupId = NormalizeId(entry.SourceGroupId);
            string originalSourceId = NormalizeId(entry.SourceId);
            string parentSourceId = NormalizeId(entry.ParentSourceId);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            string captureBundleId = NormalizeId(entry.CaptureBundleId);

            while (current != null)
            {
                string currentSourceId = NormalizeId(current.SourceId);
                if (!string.IsNullOrWhiteSpace(currentSourceId))
                {
                    if (!visited.Add(currentSourceId))
                    {
                        return UnknownResolution(normalizedPath);
                    }

                    lineage.Add(currentSourceId);
                    originalSourceId = currentSourceId;
                }

                if (string.IsNullOrWhiteSpace(sourceGroupId))
                {
                    sourceGroupId = NormalizeId(current.SourceGroupId);
                }

                if (string.IsNullOrWhiteSpace(captureBundleId))
                {
                    captureBundleId = NormalizeId(current.CaptureBundleId);
                }

                string currentParentId = NormalizeId(current.ParentSourceId);
                if (string.IsNullOrWhiteSpace(currentParentId))
                {
                    break;
                }

                if (!bySourceId.TryGetValue(currentParentId, out current))
                {
                    return UnknownResolution(normalizedPath);
                }
            }

            return new YoloDatasetSourceGroupResolution(
                normalizedPath,
                isKnown: true,
                string.IsNullOrWhiteSpace(entry.SourceId) ? Unknown : NormalizeId(entry.SourceId),
                string.IsNullOrWhiteSpace(sourceGroupId) ? Unknown : sourceGroupId,
                string.IsNullOrWhiteSpace(originalSourceId) ? Unknown : originalSourceId,
                string.IsNullOrWhiteSpace(parentSourceId) ? Unknown : parentSourceId,
                string.IsNullOrWhiteSpace(entry.Transformation) ? Unknown : entry.Transformation.Trim(),
                string.IsNullOrWhiteSpace(captureBundleId) ? Unknown : captureBundleId,
                lineage);
        }

        public static void ValidateOptional(string datasetRootPath, IList<string> errors)
        {
            if (errors == null)
            {
                return;
            }

            YoloDatasetSourceGroupLoadResult result = LoadForDatasetRoot(datasetRootPath);
            if (result.IsAvailable && !result.IsValid)
            {
                foreach (string error in result.Errors)
                {
                    errors.Add(error);
                }
            }
        }

        public static string NormalizeRelativeImagePath(string path)
        {
            string value = (path ?? string.Empty).Trim().Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(value)
                || value.StartsWith("/", StringComparison.Ordinal)
                || value.Contains(":", StringComparison.Ordinal))
            {
                return string.Empty;
            }

            string[] segments = value.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0 || segments.Any(segment => segment == "." || segment == ".."))
            {
                return string.Empty;
            }

            return string.Join("/", segments);
        }

        private static YoloDatasetSourceGroupResolution UnknownResolution(string normalizedPath)
            => new YoloDatasetSourceGroupResolution(
                normalizedPath,
                isKnown: false,
                Unknown,
                Unknown,
                Unknown,
                Unknown,
                Unknown,
                Unknown,
                Array.Empty<string>());

        private static void DetectCycle(
            string sourceId,
            IReadOnlyDictionary<string, YoloDatasetSourceGroupEntry> entries,
            ISet<string> visiting,
            ISet<string> completed,
            ICollection<string> errors)
        {
            if (completed.Contains(sourceId) || !visiting.Add(sourceId))
            {
                if (visiting.Contains(sourceId))
                {
                    errors.Add($"Source-group lineage contains a cycle at sourceId: {sourceId}");
                }

                return;
            }

            string parentSourceId = NormalizeId(entries[sourceId].ParentSourceId);
            if (!string.IsNullOrWhiteSpace(parentSourceId) && entries.ContainsKey(parentSourceId))
            {
                DetectCycle(parentSourceId, entries, visiting, completed, errors);
            }

            visiting.Remove(sourceId);
            completed.Add(sourceId);
        }

        private static string NormalizeId(string value)
            => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

        private static bool HasControlWhitespace(string value)
            => !string.IsNullOrEmpty(value) && value.Any(char.IsControl);
    }

    public sealed class YoloDatasetSourceGroupValidationResult
    {
        internal YoloDatasetSourceGroupValidationResult(IEnumerable<string> errors)
        {
            Errors = (errors ?? Enumerable.Empty<string>()).ToList();
        }

        public IReadOnlyList<string> Errors { get; }

        public bool IsValid => Errors.Count == 0;

        public string Summary => string.Join(Environment.NewLine, Errors);
    }
}
