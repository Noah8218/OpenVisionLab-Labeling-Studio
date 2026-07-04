using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MvcVisionSystem
{
    public enum AnomalyImageReviewState
    {
        Unreviewed,
        Normal,
        Abnormal
    }

    public sealed class AnomalyImageReviewStatus
    {
        internal AnomalyImageReviewStatus(string imagePath)
        {
            ImagePath = imagePath ?? string.Empty;
            ImageName = Path.GetFileNameWithoutExtension(ImagePath);
            ReviewState = AnomalyImageReviewState.Unreviewed;
        }

        public string ImagePath { get; }

        public string ImageName { get; }

        public AnomalyImageReviewState ReviewState { get; internal set; }

        public string ReviewStateName => ReviewState.ToString();

        public DateTime LastUpdatedUtc { get; internal set; }

        public bool IsReviewed => ReviewState == AnomalyImageReviewState.Normal
            || ReviewState == AnomalyImageReviewState.Abnormal;
    }

    public sealed class AnomalyImageReviewSummary
    {
        public int TotalImageCount { get; set; }

        public int ReviewedImageCount { get; set; }

        public int NormalImageCount { get; set; }

        public int AbnormalImageCount { get; set; }

        public int UnreviewedImageCount { get; set; }
    }

    public sealed class AnomalyImageReviewStatusService
    {
        public const string FileName = "anomaly-review-status.json";

        private const int CurrentSchemaVersion = 1;
        private readonly object syncRoot = new object();
        private readonly Dictionary<string, AnomalyImageReviewStatus> statuses = new Dictionary<string, AnomalyImageReviewStatus>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<AnomalyImageReviewStatus> GetItems()
        {
            lock (syncRoot)
            {
                return statuses.Values
                    .OrderBy(status => status.ImagePath, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }

        public void SetImages(IEnumerable<string> imagePaths)
        {
            lock (syncRoot)
            {
                SetImagesCore(imagePaths);
            }
        }

        public static string ResolveReviewStatusFilePath(CData data)
        {
            string outputRootPath = data?.OutputRootPath;
            if (string.IsNullOrWhiteSpace(outputRootPath))
            {
                return string.Empty;
            }

            return Path.Combine(outputRootPath, FileName);
        }

        public void LoadReviewStatus(CData data, IEnumerable<string> imagePaths)
        {
            lock (syncRoot)
            {
                SetImagesCore(imagePaths);
            }

            string filePath = ResolveReviewStatusFilePath(data);
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return;
            }

            try
            {
                List<PersistedReviewStatus> persistedItems = DeserializePersistedItems(File.ReadAllText(filePath));
                lock (syncRoot)
                {
                    foreach (PersistedReviewStatus persisted in persistedItems)
                    {
                        AnomalyImageReviewStatus status = FindByIdentity(persisted.ImagePath, persisted.ImageName);
                        if (status == null || !TryResolveReviewState(persisted, out AnomalyImageReviewState reviewState))
                        {
                            continue;
                        }

                        status.ReviewState = reviewState;
                        status.LastUpdatedUtc = persisted.LastUpdatedUtc;
                    }
                }
            }
            catch
            {
                // Review status is a convenience cache; corrupt files should not block image loading.
            }
        }

        public void SaveReviewStatus(CData data)
        {
            string filePath = ResolveReviewStatusFilePath(data);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            List<PersistedReviewStatus> persistedItems;
            lock (syncRoot)
            {
                persistedItems = statuses.Values
                    .Where(status => status.IsReviewed)
                    .Select(status => new PersistedReviewStatus
                    {
                        ImagePath = status.ImagePath,
                        ImageName = status.ImageName,
                        ReviewState = status.ReviewState,
                        ReviewStateName = status.ReviewState.ToString(),
                        LastUpdatedUtc = status.LastUpdatedUtc
                    })
                    .OrderBy(item => item.ImagePath, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            try
            {
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var persistedFile = new PersistedReviewStatusFile
                {
                    SchemaVersion = CurrentSchemaVersion,
                    Items = persistedItems
                };
                string json = JsonConvert.SerializeObject(persistedFile, Formatting.Indented);
                if (File.Exists(filePath)
                    && string.Equals(File.ReadAllText(filePath), json, StringComparison.Ordinal))
                {
                    return;
                }

                File.WriteAllText(filePath, json);
            }
            catch
            {
                // Do not interrupt labeling if the optional anomaly review-state cache cannot be written.
            }
        }

        public AnomalyImageReviewStatus MarkNormal(string imagePath, string imageName = "")
        {
            lock (syncRoot)
            {
                return SetReviewState(imagePath, imageName, AnomalyImageReviewState.Normal);
            }
        }

        public AnomalyImageReviewStatus MarkAbnormal(string imagePath, string imageName = "")
        {
            lock (syncRoot)
            {
                return SetReviewState(imagePath, imageName, AnomalyImageReviewState.Abnormal);
            }
        }

        public AnomalyImageReviewStatus ClearReviewState(string imagePath, string imageName = "")
        {
            lock (syncRoot)
            {
                return SetReviewState(imagePath, imageName, AnomalyImageReviewState.Unreviewed);
            }
        }

        public bool TryFindNextUnreviewed(IReadOnlyList<string> orderedImagePaths, string currentImagePath, out string nextImagePath)
        {
            nextImagePath = string.Empty;
            if (orderedImagePaths == null || orderedImagePaths.Count == 0)
            {
                return false;
            }

            int startIndex = IndexOfPath(orderedImagePaths, currentImagePath);
            if (startIndex < 0)
            {
                startIndex = -1;
            }

            lock (syncRoot)
            {
                for (int offset = 1; offset <= orderedImagePaths.Count; offset++)
                {
                    int index = (startIndex + offset) % orderedImagePaths.Count;
                    string imagePath = orderedImagePaths[index];
                    AnomalyImageReviewStatus status = GetOrCreateCore(imagePath);
                    if (status?.ReviewState == AnomalyImageReviewState.Unreviewed)
                    {
                        nextImagePath = imagePath;
                        return true;
                    }
                }
            }

            return false;
        }

        public AnomalyImageReviewSummary BuildSummary()
        {
            lock (syncRoot)
            {
                return BuildSummaryCore(statuses.Values);
            }
        }

        public static AnomalyImageReviewSummary LoadPersistedSummary(CData data, int totalImageCount = 0)
        {
            string filePath = ResolveReviewStatusFilePath(data);
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return BuildSummaryFromCounts(totalImageCount, 0, 0);
            }

            try
            {
                List<PersistedReviewStatus> persistedItems = DeserializePersistedItems(File.ReadAllText(filePath));
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                int normalCount = 0;
                int abnormalCount = 0;
                foreach (PersistedReviewStatus persisted in persistedItems)
                {
                    string identity = BuildPersistedIdentity(persisted);
                    if (string.IsNullOrWhiteSpace(identity)
                        || !seen.Add(identity)
                        || !TryResolveReviewState(persisted, out AnomalyImageReviewState reviewState))
                    {
                        continue;
                    }

                    if (reviewState == AnomalyImageReviewState.Normal)
                    {
                        normalCount++;
                    }
                    else if (reviewState == AnomalyImageReviewState.Abnormal)
                    {
                        abnormalCount++;
                    }
                }

                return BuildSummaryFromCounts(totalImageCount, normalCount, abnormalCount);
            }
            catch
            {
                return BuildSummaryFromCounts(totalImageCount, 0, 0);
            }
        }

        private void SetImagesCore(IEnumerable<string> imagePaths)
        {
            var activeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string imagePath in imagePaths ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(imagePath))
                {
                    continue;
                }

                string key = NormalizeKey(imagePath);
                activeKeys.Add(key);
                if (!statuses.ContainsKey(key))
                {
                    statuses[key] = new AnomalyImageReviewStatus(imagePath);
                }
            }

            foreach (string key in statuses.Keys.Where(key => !activeKeys.Contains(key)).ToList())
            {
                statuses.Remove(key);
            }
        }

        private AnomalyImageReviewStatus SetReviewState(string imagePath, string imageName, AnomalyImageReviewState state)
        {
            AnomalyImageReviewStatus status = FindByIdentity(imagePath, imageName);
            if (status == null && !string.IsNullOrWhiteSpace(imagePath))
            {
                status = GetOrCreateCore(imagePath);
            }

            if (status == null)
            {
                return null;
            }

            status.ReviewState = state;
            status.LastUpdatedUtc = DateTime.UtcNow;
            return status;
        }

        private AnomalyImageReviewStatus GetOrCreateCore(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return null;
            }

            string key = NormalizeKey(imagePath);
            if (!statuses.TryGetValue(key, out AnomalyImageReviewStatus status))
            {
                status = new AnomalyImageReviewStatus(imagePath);
                statuses[key] = status;
            }

            return status;
        }

        private AnomalyImageReviewStatus FindByIdentity(string imagePath, string imageName)
        {
            if (!string.IsNullOrWhiteSpace(imagePath))
            {
                string key = NormalizeKey(imagePath);
                if (statuses.TryGetValue(key, out AnomalyImageReviewStatus byPath))
                {
                    return byPath;
                }
            }

            if (!string.IsNullOrWhiteSpace(imageName))
            {
                return statuses.Values.FirstOrDefault(status =>
                    string.Equals(status.ImageName, imageName, StringComparison.OrdinalIgnoreCase));
            }

            return null;
        }

        private static AnomalyImageReviewSummary BuildSummaryCore(IEnumerable<AnomalyImageReviewStatus> source)
        {
            List<AnomalyImageReviewStatus> items = source?.ToList() ?? new List<AnomalyImageReviewStatus>();
            int normalCount = items.Count(item => item.ReviewState == AnomalyImageReviewState.Normal);
            int abnormalCount = items.Count(item => item.ReviewState == AnomalyImageReviewState.Abnormal);
            return BuildSummaryFromCounts(items.Count, normalCount, abnormalCount);
        }

        private static AnomalyImageReviewSummary BuildSummaryFromCounts(int totalImageCount, int normalCount, int abnormalCount)
        {
            int reviewedCount = Math.Max(0, normalCount) + Math.Max(0, abnormalCount);
            int resolvedTotal = Math.Max(Math.Max(0, totalImageCount), reviewedCount);
            return new AnomalyImageReviewSummary
            {
                TotalImageCount = resolvedTotal,
                NormalImageCount = Math.Max(0, normalCount),
                AbnormalImageCount = Math.Max(0, abnormalCount),
                ReviewedImageCount = reviewedCount,
                UnreviewedImageCount = Math.Max(0, resolvedTotal - reviewedCount)
            };
        }

        private static int IndexOfPath(IReadOnlyList<string> imagePaths, string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return -1;
            }

            string key = NormalizeKey(imagePath);
            for (int i = 0; i < imagePaths.Count; i++)
            {
                if (string.Equals(NormalizeKey(imagePaths[i]), key, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private static List<PersistedReviewStatus> DeserializePersistedItems(string json)
        {
            string trimmed = (json ?? string.Empty).TrimStart();
            if (trimmed.StartsWith("[", StringComparison.Ordinal))
            {
                return JsonConvert.DeserializeObject<List<PersistedReviewStatus>>(json) ?? new List<PersistedReviewStatus>();
            }

            PersistedReviewStatusFile persistedFile = JsonConvert.DeserializeObject<PersistedReviewStatusFile>(json);
            return persistedFile?.Items ?? new List<PersistedReviewStatus>();
        }

        private static bool TryResolveReviewState(PersistedReviewStatus persisted, out AnomalyImageReviewState reviewState)
        {
            reviewState = AnomalyImageReviewState.Unreviewed;
            if (persisted == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(persisted.ReviewStateName)
                && Enum.TryParse(persisted.ReviewStateName, ignoreCase: true, out reviewState)
                && Enum.IsDefined(typeof(AnomalyImageReviewState), reviewState))
            {
                return true;
            }

            if (!Enum.IsDefined(typeof(AnomalyImageReviewState), persisted.ReviewState))
            {
                return false;
            }

            reviewState = persisted.ReviewState;
            return true;
        }

        private static string BuildPersistedIdentity(PersistedReviewStatus persisted)
        {
            if (persisted == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(persisted.ImagePath))
            {
                return NormalizeKey(persisted.ImagePath);
            }

            return persisted.ImageName ?? string.Empty;
        }

        private static string NormalizeKey(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(path.Trim());
            }
            catch
            {
                return path.Trim();
            }
        }

        private sealed class PersistedReviewStatusFile
        {
            [JsonProperty("schemaVersion")]
            public int SchemaVersion { get; set; } = CurrentSchemaVersion;

            [JsonProperty("items")]
            public List<PersistedReviewStatus> Items { get; set; } = new List<PersistedReviewStatus>();
        }

        private sealed class PersistedReviewStatus
        {
            [JsonProperty("imagePath")]
            public string ImagePath { get; set; } = string.Empty;

            [JsonProperty("imageName")]
            public string ImageName { get; set; } = string.Empty;

            [JsonProperty("reviewState")]
            public AnomalyImageReviewState ReviewState { get; set; }

            [JsonProperty("reviewStateName")]
            public string ReviewStateName { get; set; } = string.Empty;

            [JsonProperty("lastUpdatedUtc")]
            public DateTime LastUpdatedUtc { get; set; }
        }
    }
}
