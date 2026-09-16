using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace MvcVisionSystem
{
    public sealed class WpfPersistentObjectMetadata
    {
        public static WpfPersistentObjectMetadata Default { get; } =
            new WpfPersistentObjectMetadata(false, Array.Empty<string>(), string.Empty);

        public WpfPersistentObjectMetadata(bool isOccluded, IEnumerable<string> tags)
            : this(isOccluded, tags, string.Empty)
        {
        }

        public WpfPersistentObjectMetadata(
            bool isOccluded,
            IEnumerable<string> tags,
            string groupId)
        {
            IsOccluded = isOccluded;
            Tags = ObjectMetadataStateService.NormalizeTags(tags);
            GroupId = ObjectMetadataStateService.NormalizeGroupId(groupId);
        }

        public bool IsOccluded { get; }

        public IReadOnlyList<string> Tags { get; }

        public string GroupId { get; }

        public bool IsDefault => !IsOccluded && Tags.Count == 0 && string.IsNullOrEmpty(GroupId);

        public string BadgeText
        {
            get
            {
                var badges = new List<string>();
                if (IsOccluded)
                {
                    badges.Add("\uAC00\uB9BC");
                }

                badges.AddRange(Tags);
                return string.Join(" \u00B7 ", badges);
            }
        }
    }

    public class ObjectMetadataStateService
    {
        public const int MaximumTagCount = 16;
        public const int MaximumTagLength = 32;

        private readonly Dictionary<int, MutableMetadata> manualRoiMetadata =
            new Dictionary<int, MutableMetadata>();
        private readonly Dictionary<string, MutableMetadata> segmentIdMetadata =
            new Dictionary<string, MutableMetadata>(StringComparer.Ordinal);
        private readonly Dictionary<LabelingSegmentationObject, MutableMetadata> segmentReferenceMetadata =
            new Dictionary<LabelingSegmentationObject, MutableMetadata>(ReferenceComparer.Instance);

        public WpfPersistentObjectMetadata GetManualRoiMetadata(int index)
            => index >= 0 && manualRoiMetadata.TryGetValue(index, out MutableMetadata metadata)
                ? metadata.Snapshot()
                : WpfPersistentObjectMetadata.Default;

        public WpfPersistentObjectMetadata GetManualSegmentMetadata(LabelingSegmentationObject segment)
        {
            MutableMetadata metadata = ResolveSegmentMetadata(segment, create: false);
            return metadata?.Snapshot() ?? WpfPersistentObjectMetadata.Default;
        }

        public WpfPersistentObjectMetadata ToggleManualRoiOccluded(int index)
        {
            MutableMetadata metadata = ResolveManualRoiMetadata(index, create: true);
            if (metadata == null)
            {
                return WpfPersistentObjectMetadata.Default;
            }

            metadata.IsOccluded = !metadata.IsOccluded;
            RemoveManualRoiDefault(index, metadata);
            return metadata.Snapshot();
        }

        public WpfPersistentObjectMetadata ToggleManualSegmentOccluded(LabelingSegmentationObject segment)
        {
            MutableMetadata metadata = ResolveSegmentMetadata(segment, create: true);
            if (metadata == null)
            {
                return WpfPersistentObjectMetadata.Default;
            }

            metadata.IsOccluded = !metadata.IsOccluded;
            RemoveSegmentDefault(segment, metadata);
            return metadata.Snapshot();
        }

        public WpfPersistentObjectMetadata ToggleManualRoiTag(int index, string tag)
        {
            MutableMetadata metadata = ResolveManualRoiMetadata(index, create: true);
            if (metadata == null || !ToggleTag(metadata, tag))
            {
                return metadata?.Snapshot() ?? WpfPersistentObjectMetadata.Default;
            }

            RemoveManualRoiDefault(index, metadata);
            return metadata.Snapshot();
        }

        public WpfPersistentObjectMetadata ToggleManualSegmentTag(
            LabelingSegmentationObject segment,
            string tag)
        {
            MutableMetadata metadata = ResolveSegmentMetadata(segment, create: true);
            if (metadata == null || !ToggleTag(metadata, tag))
            {
                return metadata?.Snapshot() ?? WpfPersistentObjectMetadata.Default;
            }

            RemoveSegmentDefault(segment, metadata);
            return metadata.Snapshot();
        }

        public WpfPersistentObjectMetadata SetManualRoiOccluded(int index, bool isOccluded)
        {
            MutableMetadata metadata = ResolveManualRoiMetadata(index, create: isOccluded);
            if (metadata == null)
            {
                return WpfPersistentObjectMetadata.Default;
            }

            metadata.IsOccluded = isOccluded;
            RemoveManualRoiDefault(index, metadata);
            return metadata.Snapshot();
        }

        public WpfPersistentObjectMetadata SetManualSegmentOccluded(
            LabelingSegmentationObject segment,
            bool isOccluded)
        {
            MutableMetadata metadata = ResolveSegmentMetadata(segment, create: isOccluded);
            if (metadata == null)
            {
                return WpfPersistentObjectMetadata.Default;
            }

            metadata.IsOccluded = isOccluded;
            RemoveSegmentDefault(segment, metadata);
            return metadata.Snapshot();
        }

        public WpfPersistentObjectMetadata SetManualRoiTag(int index, string tag, bool isApplied)
        {
            MutableMetadata metadata = ResolveManualRoiMetadata(index, create: isApplied);
            if (metadata == null)
            {
                return WpfPersistentObjectMetadata.Default;
            }

            SetTag(metadata, tag, isApplied);
            RemoveManualRoiDefault(index, metadata);
            return metadata.Snapshot();
        }

        public WpfPersistentObjectMetadata SetManualSegmentTag(
            LabelingSegmentationObject segment,
            string tag,
            bool isApplied)
        {
            MutableMetadata metadata = ResolveSegmentMetadata(segment, create: isApplied);
            if (metadata == null)
            {
                return WpfPersistentObjectMetadata.Default;
            }

            SetTag(metadata, tag, isApplied);
            RemoveSegmentDefault(segment, metadata);
            return metadata.Snapshot();
        }

        public WpfPersistentObjectMetadata SetManualRoiGroupId(int index, string groupId)
        {
            string normalized = NormalizeGroupId(groupId);
            MutableMetadata metadata = ResolveManualRoiMetadata(
                index,
                create: !string.IsNullOrEmpty(normalized));
            if (metadata == null)
            {
                return WpfPersistentObjectMetadata.Default;
            }

            metadata.GroupId = normalized;
            RemoveManualRoiDefault(index, metadata);
            return metadata.Snapshot();
        }

        public WpfPersistentObjectMetadata SetManualSegmentGroupId(
            LabelingSegmentationObject segment,
            string groupId)
        {
            string normalized = NormalizeGroupId(groupId);
            MutableMetadata metadata = ResolveSegmentMetadata(
                segment,
                create: !string.IsNullOrEmpty(normalized));
            if (metadata == null)
            {
                return WpfPersistentObjectMetadata.Default;
            }

            metadata.GroupId = normalized;
            RemoveSegmentDefault(segment, metadata);
            return metadata.Snapshot();
        }

        public void SetManualRoiMetadata(int index, WpfPersistentObjectMetadata metadata)
        {
            if (index < 0)
            {
                return;
            }

            SetMetadata(manualRoiMetadata, index, metadata);
        }

        public void SetManualSegmentMetadata(
            LabelingSegmentationObject segment,
            WpfPersistentObjectMetadata metadata)
        {
            if (segment == null)
            {
                return;
            }

            MutableMetadata target = ResolveSegmentMetadata(segment, create: metadata?.IsDefault != true);
            if (target == null)
            {
                return;
            }

            target.Apply(metadata);
            RemoveSegmentDefault(segment, target);
        }

        public void ShiftRoiMetadataAfterRemoval(int removedIndex)
        {
            if (removedIndex < 0 || manualRoiMetadata.Count == 0)
            {
                return;
            }

            var shifted = new Dictionary<int, MutableMetadata>();
            foreach (KeyValuePair<int, MutableMetadata> pair in manualRoiMetadata)
            {
                if (pair.Key != removedIndex)
                {
                    shifted[pair.Key > removedIndex ? pair.Key - 1 : pair.Key] = pair.Value;
                }
            }

            manualRoiMetadata.Clear();
            foreach (KeyValuePair<int, MutableMetadata> pair in shifted)
            {
                manualRoiMetadata[pair.Key] = pair.Value;
            }
        }

        public void RemoveManualSegment(LabelingSegmentationObject segment)
        {
            if (segment == null)
            {
                return;
            }

            segmentReferenceMetadata.Remove(segment);
            string objectId = NormalizeObjectId(segment.ObjectId);
            if (!string.IsNullOrEmpty(objectId))
            {
                segmentIdMetadata.Remove(objectId);
            }
        }

        public void Clear()
        {
            manualRoiMetadata.Clear();
            segmentIdMetadata.Clear();
            segmentReferenceMetadata.Clear();
        }

        public int DissolveInvalidGroups(
            int manualRoiCount,
            IReadOnlyList<LabelingSegmentationObject> manualSegments)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < Math.Max(0, manualRoiCount); index++)
            {
                IncrementGroupCount(counts, GetManualRoiMetadata(index).GroupId);
            }

            foreach (LabelingSegmentationObject segment in manualSegments
                ?? Array.Empty<LabelingSegmentationObject>())
            {
                IncrementGroupCount(counts, GetManualSegmentMetadata(segment).GroupId);
            }

            HashSet<string> invalid = counts
                .Where(pair => pair.Value < 2)
                .Select(pair => pair.Key)
                .ToHashSet(StringComparer.Ordinal);
            if (invalid.Count == 0)
            {
                return 0;
            }

            for (int index = 0; index < Math.Max(0, manualRoiCount); index++)
            {
                WpfPersistentObjectMetadata metadata = GetManualRoiMetadata(index);
                if (invalid.Contains(metadata.GroupId))
                {
                    SetManualRoiGroupId(index, string.Empty);
                }
            }

            foreach (LabelingSegmentationObject segment in manualSegments
                ?? Array.Empty<LabelingSegmentationObject>())
            {
                WpfPersistentObjectMetadata metadata = GetManualSegmentMetadata(segment);
                if (invalid.Contains(metadata.GroupId))
                {
                    SetManualSegmentGroupId(segment, string.Empty);
                }
            }

            return invalid.Count;
        }

        public static string NormalizeTag(string tag)
        {
            string normalized = tag?.Trim() ?? string.Empty;
            return normalized.Length <= MaximumTagLength
                ? normalized
                : normalized.Substring(0, MaximumTagLength);
        }

        public static IReadOnlyList<string> NormalizeTags(IEnumerable<string> tags)
            => (tags ?? Array.Empty<string>())
                .Select(NormalizeTag)
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaximumTagCount)
                .ToList();

        public static string NormalizeGroupId(string groupId)
            => Guid.TryParse(groupId?.Trim(), out Guid parsed)
                ? parsed.ToString("N")
                : string.Empty;

        private MutableMetadata ResolveManualRoiMetadata(int index, bool create)
        {
            if (index < 0)
            {
                return null;
            }

            if (manualRoiMetadata.TryGetValue(index, out MutableMetadata metadata) || !create)
            {
                return metadata;
            }

            metadata = new MutableMetadata();
            manualRoiMetadata[index] = metadata;
            return metadata;
        }

        private MutableMetadata ResolveSegmentMetadata(LabelingSegmentationObject segment, bool create)
        {
            if (segment == null)
            {
                return null;
            }

            string objectId = NormalizeObjectId(segment.ObjectId);
            if (!string.IsNullOrEmpty(objectId))
            {
                if (segmentIdMetadata.TryGetValue(objectId, out MutableMetadata stableMetadata))
                {
                    return stableMetadata;
                }

                if (segmentReferenceMetadata.TryGetValue(segment, out MutableMetadata referenceMetadata))
                {
                    segmentReferenceMetadata.Remove(segment);
                    segmentIdMetadata[objectId] = referenceMetadata;
                    return referenceMetadata;
                }

                if (!create)
                {
                    return null;
                }

                var created = new MutableMetadata();
                segmentIdMetadata[objectId] = created;
                return created;
            }

            if (segmentReferenceMetadata.TryGetValue(segment, out MutableMetadata metadata) || !create)
            {
                return metadata;
            }

            metadata = new MutableMetadata();
            segmentReferenceMetadata[segment] = metadata;
            return metadata;
        }

        private void RemoveManualRoiDefault(int index, MutableMetadata metadata)
        {
            if (metadata?.IsDefault == true)
            {
                manualRoiMetadata.Remove(index);
            }
        }

        private void RemoveSegmentDefault(LabelingSegmentationObject segment, MutableMetadata metadata)
        {
            if (segment == null || metadata?.IsDefault != true)
            {
                return;
            }

            segmentReferenceMetadata.Remove(segment);
            string objectId = NormalizeObjectId(segment.ObjectId);
            if (!string.IsNullOrEmpty(objectId))
            {
                segmentIdMetadata.Remove(objectId);
            }
        }

        private static void SetMetadata(
            IDictionary<int, MutableMetadata> target,
            int key,
            WpfPersistentObjectMetadata metadata)
        {
            if (metadata == null || metadata.IsDefault)
            {
                target.Remove(key);
                return;
            }

            var mutable = new MutableMetadata();
            mutable.Apply(metadata);
            target[key] = mutable;
        }

        private static bool ToggleTag(MutableMetadata metadata, string tag)
        {
            string normalized = NormalizeTag(tag);
            if (metadata == null || string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            string existing = metadata.Tags.FirstOrDefault(item =>
                string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(existing))
            {
                metadata.Tags.Remove(existing);
                return true;
            }

            if (metadata.Tags.Count >= MaximumTagCount)
            {
                return false;
            }

            metadata.Tags.Add(normalized);
            metadata.Tags.Sort(StringComparer.OrdinalIgnoreCase);
            return true;
        }

        private static void SetTag(MutableMetadata metadata, string tag, bool isApplied)
        {
            string normalized = NormalizeTag(tag);
            if (metadata == null || string.IsNullOrWhiteSpace(normalized))
            {
                return;
            }

            string existing = metadata.Tags.FirstOrDefault(item =>
                string.Equals(item, normalized, StringComparison.OrdinalIgnoreCase));
            if (!isApplied)
            {
                if (!string.IsNullOrEmpty(existing))
                {
                    metadata.Tags.Remove(existing);
                }
                return;
            }

            if (string.IsNullOrEmpty(existing) && metadata.Tags.Count < MaximumTagCount)
            {
                metadata.Tags.Add(normalized);
                metadata.Tags.Sort(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static void IncrementGroupCount(IDictionary<string, int> counts, string groupId)
        {
            if (string.IsNullOrWhiteSpace(groupId))
            {
                return;
            }

            counts.TryGetValue(groupId, out int count);
            counts[groupId] = count + 1;
        }

        private static string NormalizeObjectId(string objectId)
            => string.IsNullOrWhiteSpace(objectId) ? string.Empty : objectId.Trim();

        private sealed class MutableMetadata
        {
            public bool IsOccluded { get; set; }

            public List<string> Tags { get; } = new List<string>();

            public string GroupId { get; set; } = string.Empty;

            public bool IsDefault => !IsOccluded && Tags.Count == 0 && string.IsNullOrEmpty(GroupId);

            public void Apply(WpfPersistentObjectMetadata metadata)
            {
                IsOccluded = metadata?.IsOccluded == true;
                Tags.Clear();
                Tags.AddRange(NormalizeTags(metadata?.Tags));
                GroupId = NormalizeGroupId(metadata?.GroupId);
            }

            public WpfPersistentObjectMetadata Snapshot()
                => new WpfPersistentObjectMetadata(IsOccluded, Tags, GroupId);
        }

        private sealed class ReferenceComparer : IEqualityComparer<LabelingSegmentationObject>
        {
            public static ReferenceComparer Instance { get; } = new ReferenceComparer();

            public bool Equals(LabelingSegmentationObject x, LabelingSegmentationObject y)
                => ReferenceEquals(x, y);

            public int GetHashCode(LabelingSegmentationObject obj)
                => obj == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }

    public class ObjectMetadataPersistenceService
    {
        private const int CurrentVersion = 2;
        private const string BoxKind = "Box";
        private const string SegmentKind = "Segment";

        private static readonly string[] DatasetModes =
        {
            Yolo.YoloDatasetSplitService.TrainMode,
            Yolo.YoloDatasetSplitService.ValidMode,
            Yolo.YoloDatasetSplitService.TestMode
        };

        public WpfObjectMetadataLoadResult LoadForImage(
            string imagePath,
            IReadOnlyList<Rectangle> manualRois,
            IReadOnlyList<string> manualRoiClassNames,
            IReadOnlyList<LabelingSegmentationObject> manualSegments,
            ObjectMetadataStateService stateService,
            LabelingProjectData data)
        {
            stateService?.Clear();
            if (stateService == null || data == null || string.IsNullOrWhiteSpace(imagePath))
            {
                return WpfObjectMetadataLoadResult.Empty;
            }

            string metadataPath = GetCandidateMetadataPaths(imagePath, data)
                .FirstOrDefault(File.Exists);
            if (string.IsNullOrWhiteSpace(metadataPath))
            {
                return WpfObjectMetadataLoadResult.Empty;
            }

            WpfObjectMetadataFile file;
            try
            {
                file = JsonConvert.DeserializeObject<WpfObjectMetadataFile>(
                    File.ReadAllText(metadataPath));
            }
            catch (Exception ex)
            {
                return WpfObjectMetadataLoadResult.Incompatible(
                    metadataPath,
                    $"\uAC1D\uCCB4 \uBA54\uD0C0\uB370\uC774\uD130\uB97C \uC77D\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4: {ex.Message}");
            }

            if (file == null || (file.Version != 1 && file.Version != CurrentVersion))
            {
                return WpfObjectMetadataLoadResult.Incompatible(
                    metadataPath,
                    "\uC9C0\uC6D0\uD558\uC9C0 \uC54A\uB294 \uAC1D\uCCB4 \uBA54\uD0C0\uB370\uC774\uD130 \uBC84\uC804\uC785\uB2C8\uB2E4.");
            }

            List<WpfObjectMetadataRecord> records = file.Objects ?? new List<WpfObjectMetadataRecord>();
            var boxes = records
                .Where(record => string.Equals(record.Kind, BoxKind, StringComparison.OrdinalIgnoreCase))
                .GroupBy(BuildBoxRecordKey, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            var segments = records
                .Where(record => string.Equals(record.Kind, SegmentKind, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(record.ObjectId))
                .GroupBy(record => record.ObjectId.Trim(), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

            int loadedCount = 0;
            var occurrences = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < (manualRois?.Count ?? 0); index++)
            {
                Rectangle bounds = manualRois[index];
                string className = GetClassName(manualRoiClassNames, index);
                if (bounds.IsEmpty || string.IsNullOrWhiteSpace(className))
                {
                    continue;
                }

                int occurrence = NextOccurrence(occurrences, BuildBoxIdentity(className, bounds));
                string key = BuildBoxKey(className, bounds, occurrence);
                if (boxes.TryGetValue(key, out WpfObjectMetadataRecord record))
                {
                    stateService.SetManualRoiMetadata(index, ToMetadata(record));
                    loadedCount++;
                }
            }

            foreach (LabelingSegmentationObject segment in manualSegments ?? Array.Empty<LabelingSegmentationObject>())
            {
                string objectId = segment?.ObjectId?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(objectId)
                    && segments.TryGetValue(objectId, out WpfObjectMetadataRecord record))
                {
                    stateService.SetManualSegmentMetadata(segment, ToMetadata(record));
                    loadedCount++;
                }
            }

            int dissolvedGroupCount = stateService.DissolveInvalidGroups(
                manualRois?.Count ?? 0,
                manualSegments);
            string dissolvedSuffix = dissolvedGroupCount > 0
                ? $" / \uAD6C\uC131\uC6D0 2\uAC1C \uBBF8\uB9CC \uADF8\uB8F9 {dissolvedGroupCount}\uAC1C \uD574\uC81C"
                : string.Empty;

            return new WpfObjectMetadataLoadResult(
                metadataPath,
                loadedCount,
                isCompatible: true,
                loadedCount > 0
                    ? $"\uAC1D\uCCB4 \uBA54\uD0C0\uB370\uC774\uD130 {loadedCount}\uAC1C \uBCF5\uC6D0{dissolvedSuffix}"
                    : "\uC800\uC7A5\uB41C \uAC1D\uCCB4 \uBA54\uD0C0\uB370\uC774\uD130\uC640 \uD604\uC7AC \uB77C\uBCA8\uC744 \uB9E4\uCE6D\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.");
        }

        public IReadOnlyList<string> Save(
            string imageName,
            IReadOnlyList<Rectangle> manualRois,
            IReadOnlyList<string> manualRoiClassNames,
            IReadOnlyList<LabelingSegmentationObject> manualSegments,
            ObjectMetadataStateService stateService,
            LabelingProjectData data)
        {
            if (stateService == null || data == null || string.IsNullOrWhiteSpace(imageName))
            {
                return Array.Empty<string>();
            }

            data.NormalizeOutputPaths();
            data.EnsureYoloOutputDirectories();
            string fileStem = Path.GetFileNameWithoutExtension(imageName);
            if (string.IsNullOrWhiteSpace(fileStem))
            {
                return Array.Empty<string>();
            }

            List<WpfObjectMetadataRecord> records = BuildRecords(
                manualRois,
                manualRoiClassNames,
                manualSegments,
                stateService);
            var targetModes = new HashSet<string>(
                Yolo.YoloDatasetSplitService.SelectModesForImage(
                    fileStem,
                    data.ProjectSettings?.YoloDataset),
                StringComparer.OrdinalIgnoreCase);
            var writtenPaths = new List<string>();

            foreach (string mode in DatasetModes)
            {
                string path = BuildMetadataPath(data.OutputRootPath, mode, fileStem);
                if (!targetModes.Contains(mode) || records.Count == 0)
                {
                    if (File.Exists(path))
                    {
                        AnnotationFilePersistence.Delete(path);
                    }
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var file = new WpfObjectMetadataFile
                {
                    Version = CurrentVersion,
                    ImageName = imageName,
                    Objects = records
                };
                AnnotationFilePersistence.WriteAtomically(
                    path,
                    temporaryPath => File.WriteAllText(
                        temporaryPath,
                        JsonConvert.SerializeObject(file, Formatting.Indented)));
                writtenPaths.Add(path);
            }

            return writtenPaths;
        }

        public static string BuildMetadataPath(string outputRootPath, string mode, string imageName)
        {
            string fileStem = Path.GetFileNameWithoutExtension(imageName ?? string.Empty);
            return Path.Combine(
                outputRootPath ?? string.Empty,
                "data",
                mode ?? string.Empty,
                "object-metadata",
                $"{fileStem}.json");
        }

        private static List<WpfObjectMetadataRecord> BuildRecords(
            IReadOnlyList<Rectangle> manualRois,
            IReadOnlyList<string> manualRoiClassNames,
            IReadOnlyList<LabelingSegmentationObject> manualSegments,
            ObjectMetadataStateService stateService)
        {
            var records = new List<WpfObjectMetadataRecord>();
            var occurrences = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < (manualRois?.Count ?? 0); index++)
            {
                Rectangle bounds = manualRois[index];
                string className = GetClassName(manualRoiClassNames, index);
                if (bounds.IsEmpty || string.IsNullOrWhiteSpace(className))
                {
                    continue;
                }

                int occurrence = NextOccurrence(occurrences, BuildBoxIdentity(className, bounds));
                WpfPersistentObjectMetadata metadata = stateService.GetManualRoiMetadata(index);
                if (!metadata.IsDefault)
                {
                    records.Add(new WpfObjectMetadataRecord
                    {
                        Kind = BoxKind,
                        ClassName = className,
                        X = bounds.X,
                        Y = bounds.Y,
                        Width = bounds.Width,
                        Height = bounds.Height,
                        Occurrence = occurrence,
                        IsOccluded = metadata.IsOccluded,
                        Tags = metadata.Tags.ToList(),
                        GroupId = metadata.GroupId
                    });
                }
            }

            var segmentObjectIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (LabelingSegmentationObject segment in manualSegments ?? Array.Empty<LabelingSegmentationObject>())
            {
                if (segment == null)
                {
                    continue;
                }

                WpfPersistentObjectMetadata metadata = stateService.GetManualSegmentMetadata(segment);
                string objectId = segment.ObjectId?.Trim() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(objectId) && !segmentObjectIds.Add(objectId))
                {
                    throw new InvalidDataException(
                        $"세그먼트 객체 ID가 중복되었습니다: {objectId}");
                }

                if (metadata.IsDefault)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(objectId))
                {
                    throw new InvalidDataException(
                        "객체 메타데이터를 저장하려면 세그먼트 객체 ID가 필요합니다.");
                }

                records.Add(new WpfObjectMetadataRecord
                {
                    Kind = SegmentKind,
                    ObjectId = objectId,
                    ClassName = segment.ClassName ?? string.Empty,
                    IsOccluded = metadata.IsOccluded,
                    Tags = metadata.Tags.ToList(),
                    GroupId = metadata.GroupId
                });
            }

            return records;
        }

        private static IEnumerable<string> GetCandidateMetadataPaths(string imagePath, LabelingProjectData data)
        {
            var paths = new List<string>();
            string imageDirectory = Path.GetDirectoryName(imagePath) ?? string.Empty;
            if (string.Equals(Path.GetFileName(imageDirectory), "images", StringComparison.OrdinalIgnoreCase))
            {
                string splitDirectory = Path.GetDirectoryName(imageDirectory) ?? string.Empty;
                paths.Add(Path.Combine(
                    splitDirectory,
                    "object-metadata",
                    $"{Path.GetFileNameWithoutExtension(imagePath)}.json"));
            }

            foreach (string mode in DatasetModes)
            {
                paths.Add(BuildMetadataPath(data.OutputRootPath, mode, imagePath));
            }

            return paths.Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static string GetClassName(IReadOnlyList<string> classNames, int index)
            => index >= 0 && index < (classNames?.Count ?? 0)
                ? ClassCatalogService.NormalizeClassName(classNames[index])
                : string.Empty;

        private static int NextOccurrence(IDictionary<string, int> occurrences, string identity)
        {
            occurrences.TryGetValue(identity, out int occurrence);
            occurrences[identity] = occurrence + 1;
            return occurrence;
        }

        private static string BuildBoxRecordKey(WpfObjectMetadataRecord record)
            => BuildBoxKey(
                record?.ClassName,
                new Rectangle(
                    record?.X ?? 0,
                    record?.Y ?? 0,
                    record?.Width ?? 0,
                    record?.Height ?? 0),
                Math.Max(0, record?.Occurrence ?? 0));

        private static string BuildBoxKey(string className, Rectangle bounds, int occurrence)
            => $"{BuildBoxIdentity(className, bounds)}|{Math.Max(0, occurrence)}";

        private static string BuildBoxIdentity(string className, Rectangle bounds)
            => FormattableString.Invariant(
                $"{ClassCatalogService.NormalizeClassName(className)}|{bounds.X}|{bounds.Y}|{bounds.Width}|{bounds.Height}");

        private static WpfPersistentObjectMetadata ToMetadata(WpfObjectMetadataRecord record)
            => new WpfPersistentObjectMetadata(
                record?.IsOccluded == true,
                record?.Tags,
                record?.GroupId);
    }

    public sealed class WpfObjectMetadataLoadResult
    {
        public static WpfObjectMetadataLoadResult Empty { get; } =
            new WpfObjectMetadataLoadResult(string.Empty, 0, true, string.Empty);

        public WpfObjectMetadataLoadResult(
            string path,
            int loadedCount,
            bool isCompatible,
            string statusText)
        {
            Path = path ?? string.Empty;
            LoadedCount = Math.Max(0, loadedCount);
            IsCompatible = isCompatible;
            StatusText = statusText ?? string.Empty;
        }

        public string Path { get; }
        public int LoadedCount { get; }
        public bool IsCompatible { get; }
        public string StatusText { get; }

        public static WpfObjectMetadataLoadResult Incompatible(string path, string statusText)
            => new WpfObjectMetadataLoadResult(path, 0, false, statusText);
    }

    public sealed class WpfObjectMetadataFile
    {
        public int Version { get; set; } = 1;
        public string ImageName { get; set; } = string.Empty;
        public List<WpfObjectMetadataRecord> Objects { get; set; } =
            new List<WpfObjectMetadataRecord>();
    }

    public sealed class WpfObjectMetadataRecord
    {
        public string Kind { get; set; } = string.Empty;
        public string ObjectId { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Occurrence { get; set; }
        public bool IsOccluded { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
        public string GroupId { get; set; } = string.Empty;
    }

    [Obsolete("Use ObjectMetadataStateService.", false)]
    public sealed class WpfObjectMetadataStateService : ObjectMetadataStateService
    {
    }

    [Obsolete("Use ObjectMetadataPersistenceService.", false)]
    public sealed class WpfObjectMetadataPersistenceService : ObjectMetadataPersistenceService
    {
    }
}
