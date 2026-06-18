using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace MvcVisionSystem.Yolo
{
    public enum YoloImageReviewState
    {
        Unreviewed,
        Requested,
        Candidate,
        NoCandidate,
        Confirmed,
        Skipped,
        Failed
    }

    public sealed class YoloImageReviewStatus
    {
        internal YoloImageReviewStatus(string imagePath)
        {
            ImagePath = imagePath ?? string.Empty;
            ImageName = Path.GetFileNameWithoutExtension(ImagePath);
            LabelStatus = new YoloImageLabelStatus(string.Empty, 0, 0);
            DetectionStatusOverride = string.Empty;
            LastDetectionMessage = string.Empty;
            ReviewState = YoloImageReviewState.Unreviewed;
        }

        public string ImagePath { get; }

        public string ImageName { get; }

        public YoloImageLabelStatus LabelStatus { get; internal set; }

        public int DetectionCandidateCount { get; internal set; }

        public int DetectionAttemptCount { get; internal set; }

        public string LastDetectionMessage { get; internal set; }

        public DateTime LastUpdatedUtc { get; internal set; }

        public YoloImageReviewState ReviewState { get; internal set; }

        internal string DetectionStatusOverride { get; set; }

        public bool IsLabeled => LabelStatus?.HasObjects == true;

        public string LabelText => LabelStatus?.Text ?? "No Label";

        public string DetectionText
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(DetectionStatusOverride))
                {
                    return DetectionStatusOverride;
                }

                return ReviewState switch
                {
                    YoloImageReviewState.Requested => "Requested",
                    YoloImageReviewState.Candidate => DetectionCandidateCount > 0 ? $"Candidate {DetectionCandidateCount}" : "Candidate",
                    YoloImageReviewState.NoCandidate => "No Candidate",
                    YoloImageReviewState.Confirmed => "Confirmed",
                    YoloImageReviewState.Skipped => "Skipped",
                    YoloImageReviewState.Failed => "Failed",
                    _ => DetectionCandidateCount > 0 ? $"Candidate {DetectionCandidateCount}" : string.Empty
                };
            }
        }

        public string DetectionDetailText
        {
            get
            {
                List<string> details = new List<string>();
                if (!string.IsNullOrWhiteSpace(DetectionText))
                {
                    details.Add(DetectionText);
                }

                if (DetectionAttemptCount > 0)
                {
                    details.Add($"Attempt {DetectionAttemptCount}");
                }

                if (!string.IsNullOrWhiteSpace(LastDetectionMessage))
                {
                    details.Add(LastDetectionMessage);
                }

                return details.Count > 0 ? string.Join(" / ", details) : string.Empty;
            }
        }
    }

    public sealed class YoloImageReviewStatusService
    {
        private readonly Dictionary<string, YoloImageReviewStatus> statuses = new Dictionary<string, YoloImageReviewStatus>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<YoloImageReviewStatus> GetItems()
        {
            return statuses.Values.ToList();
        }

        public void SetImages(IEnumerable<string> imagePaths)
        {
            HashSet<string> activeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
                    statuses[key] = new YoloImageReviewStatus(imagePath);
                }
            }

            foreach (string key in statuses.Keys.Where(key => !activeKeys.Contains(key)).ToList())
            {
                statuses.Remove(key);
            }
        }

        public static string ResolveReviewStatusFilePath(CData data)
        {
            string outputRootPath = data?.OutputRootPath;
            if (string.IsNullOrWhiteSpace(outputRootPath))
            {
                return string.Empty;
            }

            return Path.Combine(outputRootPath, "review-status.json");
        }

        public void LoadReviewStatus(CData data, IEnumerable<string> imagePaths)
        {
            SetImages(imagePaths);
            string filePath = ResolveReviewStatusFilePath(data);
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return;
            }

            try
            {
                List<PersistedReviewStatus> persistedItems = JsonConvert.DeserializeObject<List<PersistedReviewStatus>>(
                    File.ReadAllText(filePath)) ?? new List<PersistedReviewStatus>();

                foreach (PersistedReviewStatus persisted in persistedItems)
                {
                    YoloImageReviewStatus status = FindByIdentity(persisted.ImagePath, persisted.ImageName);
                    if (status == null)
                    {
                        continue;
                    }

                    if (!TryResolveReviewState(persisted, out YoloImageReviewState reviewState))
                    {
                        continue;
                    }

                    status.DetectionCandidateCount = Math.Max(0, persisted.DetectionCandidateCount);
                    status.DetectionAttemptCount = Math.Max(0, persisted.DetectionAttemptCount);
                    status.LastDetectionMessage = persisted.LastDetectionMessage ?? string.Empty;
                    status.ReviewState = reviewState;
                    status.DetectionStatusOverride = string.Empty;
                    status.LastUpdatedUtc = persisted.LastUpdatedUtc;
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

            try
            {
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                List<PersistedReviewStatus> persistedItems = statuses.Values
                    .Where(status => status.ReviewState != YoloImageReviewState.Unreviewed)
                    .Select(status => new PersistedReviewStatus
                    {
                        ImagePath = status.ImagePath,
                        ImageName = status.ImageName,
                        DetectionCandidateCount = status.DetectionCandidateCount,
                        DetectionAttemptCount = status.DetectionAttemptCount,
                        LastDetectionMessage = status.LastDetectionMessage,
                        ReviewState = status.ReviewState,
                        ReviewStateName = status.ReviewState.ToString(),
                        LastUpdatedUtc = status.LastUpdatedUtc
                    })
                    .OrderBy(item => item.ImagePath, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                string json = JsonConvert.SerializeObject(persistedItems, Formatting.Indented);
                if (File.Exists(filePath)
                    && string.Equals(File.ReadAllText(filePath), json, StringComparison.Ordinal))
                {
                    return;
                }

                File.WriteAllText(filePath, json);
            }
            catch
            {
                // Do not interrupt labeling if the optional review-state cache cannot be written.
            }
        }

        public YoloImageReviewStatus GetOrCreate(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return null;
            }

            string key = NormalizeKey(imagePath);
            if (!statuses.TryGetValue(key, out YoloImageReviewStatus status))
            {
                status = new YoloImageReviewStatus(imagePath);
                statuses[key] = status;
            }

            return status;
        }

        public YoloImageReviewStatus RefreshLabelStatus(string imagePath, Size imageSize, CData data)
        {
            YoloImageReviewStatus status = GetOrCreate(imagePath);
            if (status == null)
            {
                return null;
            }

            status.LabelStatus = YoloImageLabelStatusService.Build(imagePath, imageSize, data);
            if (status.LabelStatus?.HasObjects == true && status.ReviewState == YoloImageReviewState.Unreviewed)
            {
                status.ReviewState = YoloImageReviewState.Confirmed;
            }
            else if (status.LabelStatus?.HasObjects != true && status.ReviewState == YoloImageReviewState.Confirmed)
            {
                status.ReviewState = YoloImageReviewState.Unreviewed;
            }

            status.LastUpdatedUtc = DateTime.UtcNow;
            return status;
        }

        public YoloImageReviewStatus RefreshLabelStatusAndReviewState(
            string imagePath,
            Size imageSize,
            CData data,
            bool hasActiveCandidates)
        {
            YoloImageReviewStatus status = RefreshLabelStatus(imagePath, imageSize, data);
            if (status == null)
            {
                return null;
            }

            if (hasActiveCandidates)
            {
                return status;
            }

            if (status.IsLabeled)
            {
                return MarkConfirmed(status.ImagePath, status.ImageName);
            }

            return ClearDetectionStatus(status.ImagePath, status.ImageName);
        }

        public YoloImageReviewStatus SetDetectionRequested(string imagePath)
        {
            return SetDetectionStatus(imagePath, string.Empty, "Requested", "Detection requested.", countDetectionAttempt: true);
        }

        public YoloImageReviewStatus SetDetectionRequested(string imagePath, string imageName)
        {
            return SetDetectionStatus(imagePath, imageName, "Requested", "Detection requested.", countDetectionAttempt: true);
        }

        public YoloImageReviewStatus SetDetectionFailed(string imagePath)
        {
            return SetDetectionFailed(imagePath, string.Empty, "Detection failed.");
        }

        public YoloImageReviewStatus SetDetectionFailed(string imagePath, string imageName, string message)
        {
            return SetDetectionStatus(imagePath, imageName, "Failed", message);
        }

        public YoloImageReviewStatus SetDetectionNoCandidates(string imagePath, string imageName)
        {
            return SetDetectionStatus(imagePath, imageName, "No Candidate", "No candidates found.");
        }

        public YoloImageReviewStatus MarkConfirmed(string imagePath, string imageName = "")
        {
            return SetReviewState(imagePath, imageName, YoloImageReviewState.Confirmed, 0, "Candidates confirmed.");
        }

        public YoloImageReviewStatus MarkSkipped(string imagePath, string imageName = "")
        {
            return SetReviewState(imagePath, imageName, YoloImageReviewState.Skipped, 0, "Candidate skipped.");
        }

        public YoloImageReviewStatus ClearDetectionStatus(string imagePath)
        {
            return SetDetectionCandidates(imagePath, string.Empty, 0);
        }

        public YoloImageReviewStatus ClearDetectionStatus(string imagePath, string imageName)
        {
            return SetDetectionCandidates(imagePath, imageName, 0);
        }

        public YoloImageReviewStatus SetDetectionCandidates(string imagePath, string imageName, int candidateCount)
        {
            YoloImageReviewStatus status = FindByIdentity(imagePath, imageName);
            if (status == null && !string.IsNullOrWhiteSpace(imagePath))
            {
                status = GetOrCreate(imagePath);
            }

            if (status == null)
            {
                return null;
            }

            status.DetectionCandidateCount = Math.Max(0, candidateCount);
            status.DetectionStatusOverride = string.Empty;
            status.ReviewState = status.DetectionCandidateCount > 0
                ? YoloImageReviewState.Candidate
                : YoloImageReviewState.Unreviewed;
            status.LastDetectionMessage = status.DetectionCandidateCount > 0
                ? $"Candidates found: {status.DetectionCandidateCount}"
                : string.Empty;
            status.LastUpdatedUtc = DateTime.UtcNow;
            return status;
        }

        public int GetLabeledCount()
        {
            return statuses.Values.Count(status => status.IsLabeled);
        }

        public bool TryFindNextUnlabeled(IReadOnlyList<string> orderedImagePaths, string currentImagePath, out string nextImagePath)
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

            for (int offset = 1; offset <= orderedImagePaths.Count; offset++)
            {
                int index = (startIndex + offset) % orderedImagePaths.Count;
                string imagePath = orderedImagePaths[index];
                YoloImageReviewStatus status = GetOrCreate(imagePath);
                if (status != null && !status.IsLabeled)
                {
                    nextImagePath = imagePath;
                    return true;
                }
            }

            return false;
        }

        private YoloImageReviewStatus SetDetectionStatus(string imagePath, string imageName, string text, string message, bool countDetectionAttempt = false)
        {
            YoloImageReviewState state = text switch
            {
                "Requested" => YoloImageReviewState.Requested,
                "Failed" => YoloImageReviewState.Failed,
                "No Candidate" => YoloImageReviewState.NoCandidate,
                _ => YoloImageReviewState.Unreviewed
            };

            return SetReviewState(imagePath, imageName, state, 0, message, countDetectionAttempt);
        }

        private YoloImageReviewStatus SetReviewState(
            string imagePath,
            string imageName,
            YoloImageReviewState state,
            int candidateCount,
            string message = "",
            bool countDetectionAttempt = false)
        {
            YoloImageReviewStatus status = FindByIdentity(imagePath, imageName);
            if (status == null && !string.IsNullOrWhiteSpace(imagePath))
            {
                status = GetOrCreate(imagePath);
            }

            if (status == null)
            {
                return null;
            }

            status.DetectionCandidateCount = Math.Max(0, candidateCount);
            status.DetectionStatusOverride = string.Empty;
            if (countDetectionAttempt && status.ReviewState != YoloImageReviewState.Requested)
            {
                status.DetectionAttemptCount++;
            }

            status.ReviewState = state;
            status.LastDetectionMessage = message ?? string.Empty;
            status.LastUpdatedUtc = DateTime.UtcNow;
            return status;
        }

        private YoloImageReviewStatus FindByIdentity(string imagePath, string imageName)
        {
            if (!string.IsNullOrWhiteSpace(imagePath))
            {
                string key = NormalizeKey(imagePath);
                if (statuses.TryGetValue(key, out YoloImageReviewStatus byPath))
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

        private static bool TryResolveReviewState(PersistedReviewStatus persisted, out YoloImageReviewState reviewState)
        {
            reviewState = YoloImageReviewState.Unreviewed;
            if (persisted == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(persisted.ReviewStateName)
                && Enum.TryParse(persisted.ReviewStateName, ignoreCase: true, out reviewState)
                && Enum.IsDefined(typeof(YoloImageReviewState), reviewState))
            {
                return true;
            }

            if (!Enum.IsDefined(typeof(YoloImageReviewState), persisted.ReviewState))
            {
                return false;
            }

            reviewState = persisted.ReviewState;
            return true;
        }

        private sealed class PersistedReviewStatus
        {
            public string ImagePath { get; set; } = string.Empty;

            public string ImageName { get; set; } = string.Empty;

            public int DetectionCandidateCount { get; set; }

            public int DetectionAttemptCount { get; set; }

            public string LastDetectionMessage { get; set; } = string.Empty;

            public YoloImageReviewState ReviewState { get; set; }

            public string ReviewStateName { get; set; } = string.Empty;

            public DateTime LastUpdatedUtc { get; set; }
        }
    }
}
