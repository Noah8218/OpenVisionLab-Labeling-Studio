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

    public enum YoloImageQualityReviewState
    {
        Unreviewed,
        NeedsFix,
        Reviewed
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
            QualityReviewState = YoloImageQualityReviewState.Unreviewed;
            QualityReviewNote = string.Empty;
        }

        public string ImagePath { get; }

        public string ImageName { get; }

        public YoloImageLabelStatus LabelStatus { get; internal set; }

        public int DetectionCandidateCount { get; internal set; }

        public int DetectionAttemptCount { get; internal set; }

        public string LastDetectionMessage { get; internal set; }

        public DateTime LastUpdatedUtc { get; internal set; }

        public YoloImageReviewState ReviewState { get; internal set; }

        public YoloImageQualityReviewState QualityReviewState { get; internal set; }

        public string QualityReviewNote { get; internal set; }

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
        public const int QualityReviewNoteMaxLength = 200;

        // Queue detail loading and delete-status refresh can run off the UI thread, so guard the shared status map.
        private readonly object syncRoot = new object();
        private readonly Dictionary<string, YoloImageReviewStatus> statuses = new Dictionary<string, YoloImageReviewStatus>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<YoloImageReviewStatus> GetItems()
        {
            lock (syncRoot)
            {
                return statuses.Values.ToList();
            }
        }

        public void SetImages(IEnumerable<string> imagePaths)
        {
            lock (syncRoot)
            {
                SetImagesCore(imagePaths);
            }
        }

        private void SetImagesCore(IEnumerable<string> imagePaths)
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
                List<PersistedReviewStatus> persistedItems = JsonConvert.DeserializeObject<List<PersistedReviewStatus>>(
                    File.ReadAllText(filePath)) ?? new List<PersistedReviewStatus>();

                lock (syncRoot)
                {
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
                        status.QualityReviewState = TryResolveQualityReviewState(persisted, out YoloImageQualityReviewState qualityReviewState)
                            ? qualityReviewState
                            : YoloImageQualityReviewState.Unreviewed;
                        status.QualityReviewNote = status.QualityReviewState == YoloImageQualityReviewState.NeedsFix
                            ? NormalizeQualityReviewNote(persisted.QualityReviewNote)
                            : string.Empty;
                        status.DetectionStatusOverride = string.Empty;
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
                    .Where(status => status.ReviewState != YoloImageReviewState.Unreviewed
                        || status.QualityReviewState != YoloImageQualityReviewState.Unreviewed
                        || !string.IsNullOrWhiteSpace(status.QualityReviewNote))
                    .Select(status => new PersistedReviewStatus
                    {
                        ImagePath = status.ImagePath,
                        ImageName = status.ImageName,
                        DetectionCandidateCount = status.DetectionCandidateCount,
                        DetectionAttemptCount = status.DetectionAttemptCount,
                        LastDetectionMessage = status.LastDetectionMessage,
                        ReviewState = status.ReviewState,
                        ReviewStateName = status.ReviewState.ToString(),
                        QualityReviewState = status.QualityReviewState,
                        QualityReviewStateName = status.QualityReviewState.ToString(),
                        QualityReviewNote = status.QualityReviewNote,
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
            lock (syncRoot)
            {
                return GetOrCreateCore(imagePath);
            }
        }

        private YoloImageReviewStatus GetOrCreateCore(string imagePath)
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
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return null;
            }

            YoloImageLabelStatus labelStatus = YoloImageLabelStatusService.Build(imagePath, imageSize, data);
            lock (syncRoot)
            {
                return ApplyLabelStatusCore(imagePath, labelStatus);
            }
        }

        public YoloImageReviewStatus RefreshLabelStatusAndReviewState(
            string imagePath,
            Size imageSize,
            CData data,
            bool hasActiveCandidates)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return null;
            }

            YoloImageLabelStatus labelStatus = YoloImageLabelStatusService.Build(imagePath, imageSize, data);
            lock (syncRoot)
            {
                YoloImageReviewStatus status = ApplyLabelStatusCore(imagePath, labelStatus);
                if (status == null || hasActiveCandidates)
                {
                    return status;
                }

                if (status.IsLabeled)
                {
                    return SetReviewState(status.ImagePath, status.ImageName, YoloImageReviewState.Confirmed, 0, "Candidates confirmed.");
                }

                if (status.LabelStatus?.HasLabelFile == true)
                {
                    return SetReviewState(status.ImagePath, status.ImageName, YoloImageReviewState.NoCandidate, 0, "Reviewed as no object.");
                }

                return SetDetectionCandidatesCore(status.ImagePath, status.ImageName, 0);
            }
        }

        private YoloImageReviewStatus ApplyLabelStatusCore(string imagePath, YoloImageLabelStatus labelStatus)
        {
            YoloImageReviewStatus status = GetOrCreateCore(imagePath);
            if (status == null)
            {
                return null;
            }

            status.LabelStatus = labelStatus;
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

        public YoloImageReviewStatus SetDetectionRequested(string imagePath)
        {
            return SetDetectionRequested(imagePath, string.Empty);
        }

        public YoloImageReviewStatus SetDetectionRequested(string imagePath, string imageName)
        {
            lock (syncRoot)
            {
                return SetDetectionStatus(imagePath, imageName, "Requested", "Detection requested.", countDetectionAttempt: true);
            }
        }

        public YoloImageReviewStatus SetDetectionFailed(string imagePath)
        {
            return SetDetectionFailed(imagePath, string.Empty, "Detection failed.");
        }

        public YoloImageReviewStatus SetDetectionFailed(string imagePath, string imageName, string message)
        {
            lock (syncRoot)
            {
                return SetDetectionStatus(imagePath, imageName, "Failed", message);
            }
        }

        public YoloImageReviewStatus SetDetectionNoCandidates(string imagePath, string imageName)
        {
            lock (syncRoot)
            {
                return SetDetectionStatus(imagePath, imageName, "No Candidate", "No candidates found.");
            }
        }

        public YoloImageReviewStatus MarkConfirmed(string imagePath, string imageName = "")
        {
            lock (syncRoot)
            {
                return SetReviewState(imagePath, imageName, YoloImageReviewState.Confirmed, 0, "Candidates confirmed.");
            }
        }

        public YoloImageReviewStatus MarkSkipped(string imagePath, string imageName = "")
        {
            lock (syncRoot)
            {
                return SetReviewState(imagePath, imageName, YoloImageReviewState.Skipped, 0, "Candidate skipped.");
            }
        }

        public YoloImageReviewStatus MarkQualityNeedsFix(
            string imagePath,
            string imageName = "",
            string qualityReviewNote = "")
        {
            lock (syncRoot)
            {
                return SetQualityReviewState(
                    imagePath,
                    imageName,
                    YoloImageQualityReviewState.NeedsFix,
                    qualityReviewNote);
            }
        }

        public YoloImageReviewStatus MarkQualityReviewed(string imagePath, string imageName = "")
        {
            lock (syncRoot)
            {
                return SetQualityReviewState(imagePath, imageName, YoloImageQualityReviewState.Reviewed);
            }
        }

        public YoloImageReviewStatus ClearQualityReview(string imagePath, string imageName = "")
        {
            lock (syncRoot)
            {
                return SetQualityReviewState(imagePath, imageName, YoloImageQualityReviewState.Unreviewed);
            }
        }

        public YoloImageReviewStatus InvalidateQualityReviewAfterEdit(string imagePath, string imageName = "")
        {
            lock (syncRoot)
            {
                YoloImageReviewStatus status = FindByIdentity(imagePath, imageName);
                if (status?.QualityReviewState != YoloImageQualityReviewState.Reviewed)
                {
                    return status;
                }

                return SetQualityReviewState(imagePath, imageName, YoloImageQualityReviewState.Unreviewed);
            }
        }

        public YoloImageReviewStatus ClearDetectionStatus(string imagePath)
        {
            return ClearDetectionStatus(imagePath, string.Empty);
        }

        public YoloImageReviewStatus ClearDetectionStatus(string imagePath, string imageName)
        {
            lock (syncRoot)
            {
                return SetDetectionCandidatesCore(imagePath, imageName, 0);
            }
        }

        public YoloImageReviewStatus SetDetectionCandidates(string imagePath, string imageName, int candidateCount)
        {
            lock (syncRoot)
            {
                return SetDetectionCandidatesCore(imagePath, imageName, candidateCount);
            }
        }

        private YoloImageReviewStatus SetDetectionCandidatesCore(string imagePath, string imageName, int candidateCount)
        {
            YoloImageReviewStatus status = FindByIdentity(imagePath, imageName);
            if (status == null && !string.IsNullOrWhiteSpace(imagePath))
            {
                status = GetOrCreateCore(imagePath);
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
            lock (syncRoot)
            {
                return statuses.Values.Count(status => status.IsLabeled);
            }
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

            lock (syncRoot)
            {
                for (int offset = 1; offset <= orderedImagePaths.Count; offset++)
                {
                    int index = (startIndex + offset) % orderedImagePaths.Count;
                    string imagePath = orderedImagePaths[index];
                    YoloImageReviewStatus status = GetOrCreateCore(imagePath);
                    if (NeedsQueueReview(status))
                    {
                        nextImagePath = imagePath;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool NeedsQueueReview(YoloImageReviewStatus status)
        {
            if (status == null)
            {
                return false;
            }

            if (status.QualityReviewState == YoloImageQualityReviewState.NeedsFix)
            {
                return true;
            }

            if (status.IsLabeled)
            {
                return false;
            }

            // A reviewed normal image has no label objects, but it is not an operator queue item anymore.
            return status.ReviewState != YoloImageReviewState.Confirmed
                && status.ReviewState != YoloImageReviewState.Skipped
                && status.ReviewState != YoloImageReviewState.NoCandidate;
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
                status = GetOrCreateCore(imagePath);
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

        private YoloImageReviewStatus SetQualityReviewState(
            string imagePath,
            string imageName,
            YoloImageQualityReviewState state,
            string qualityReviewNote = "")
        {
            YoloImageReviewStatus status = FindByIdentity(imagePath, imageName);
            if (status == null && !string.IsNullOrWhiteSpace(imagePath))
            {
                status = GetOrCreateCore(imagePath);
            }

            if (status == null)
            {
                return null;
            }

            status.QualityReviewState = state;
            status.QualityReviewNote = state == YoloImageQualityReviewState.NeedsFix
                ? NormalizeQualityReviewNote(qualityReviewNote)
                : string.Empty;
            status.LastUpdatedUtc = DateTime.UtcNow;
            return status;
        }

        public static string NormalizeQualityReviewNote(string qualityReviewNote)
        {
            string normalized = (qualityReviewNote ?? string.Empty)
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Trim();
            return normalized.Length <= QualityReviewNoteMaxLength
                ? normalized
                : normalized.Substring(0, QualityReviewNoteMaxLength);
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

        private static bool TryResolveQualityReviewState(
            PersistedReviewStatus persisted,
            out YoloImageQualityReviewState qualityReviewState)
        {
            qualityReviewState = YoloImageQualityReviewState.Unreviewed;
            if (persisted == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(persisted.QualityReviewStateName)
                && Enum.TryParse(persisted.QualityReviewStateName, ignoreCase: true, out qualityReviewState)
                && Enum.IsDefined(typeof(YoloImageQualityReviewState), qualityReviewState))
            {
                return true;
            }

            if (!Enum.IsDefined(typeof(YoloImageQualityReviewState), persisted.QualityReviewState))
            {
                return false;
            }

            qualityReviewState = persisted.QualityReviewState;
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

            public YoloImageQualityReviewState QualityReviewState { get; set; }

            public string QualityReviewStateName { get; set; } = string.Empty;

            public string QualityReviewNote { get; set; } = string.Empty;

            public DateTime LastUpdatedUtc { get; set; }
        }
    }
}
