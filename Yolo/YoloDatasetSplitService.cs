using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem.Yolo
{
    public static class YoloDatasetSplitService
    {
        public const string TrainMode = "train";
        public const string ValidMode = "valid";
        public const string TestMode = "test";
        public const string ExcludedMode = "excluded";

        private static readonly string[] StandardModeValues =
        {
            TrainMode,
            ValidMode,
            TestMode
        };

        private static readonly IReadOnlyList<string> ReadOnlyStandardModes = Array.AsReadOnly(StandardModeValues);

        public static IReadOnlyList<string> StandardModes => ReadOnlyStandardModes;

        public static IReadOnlyList<string> AnnotationModes { get; } = Array.AsReadOnly(new[] { TrainMode, ValidMode, TestMode, ExcludedMode });

        public static string NormalizeAssignmentSplit(string split) => string.Equals(split, ExcludedMode, StringComparison.OrdinalIgnoreCase)
            ? ExcludedMode : NormalizeStandardSplit(split);

        public static string NormalizeStandardSplit(string split)
        {
            foreach (string mode in StandardModeValues)
            {
                if (string.Equals(split, mode, StringComparison.OrdinalIgnoreCase))
                {
                    return mode;
                }
            }

            return string.Empty;
        }

        public static IReadOnlyList<string> SelectModesForImage(string imageName, YoloDatasetSettings settings)
        {
            YoloImageSplitAssignment assignment = settings?.ImageSplitAssignments?.FirstOrDefault(item => item != null
                && string.Equals(item.ImageName?.Trim(), imageName?.Trim(), StringComparison.OrdinalIgnoreCase)
                && NormalizeAssignmentSplit(item.Split).Length > 0);
            if (assignment != null) return new[] { NormalizeAssignmentSplit(assignment.Split) };
            if (settings?.FreezeTestSplit == true && settings.FrozenTestImages?.Any(item => item != null
                && string.Equals(item.ImageName, imageName?.Trim(), StringComparison.OrdinalIgnoreCase)) == true) return new[] { TestMode };
            int validationPercent = Math.Clamp(settings?.ValidationPercent ?? 20, 0, 100);
            int testPercent = settings?.FreezeTestSplit == true ? 0 : Math.Clamp(settings?.TestPercent ?? 0, 0, 100);
            if (settings?.FreezeTestSplit == true && settings.TestPercent < 100)
                validationPercent = Math.Clamp((int)Math.Round(validationPercent * 100D / (100 - settings.TestPercent)), 0, 100);
            int reservedPercent = Math.Clamp(validationPercent + testPercent, 0, 100);
            if (reservedPercent <= 0)
            {
                return new[] { TrainMode };
            }

            if (testPercent >= 100)
            {
                return new[] { TestMode };
            }

            if (validationPercent >= 100)
            {
                return new[] { ValidMode };
            }

            uint bucket = StableBucket(imageName, settings?.SplitSeed ?? 17);
            if (bucket < testPercent)
            {
                return new[] { TestMode };
            }

            return bucket < reservedPercent
                ? new[] { ValidMode }
                : new[] { TrainMode };
        }

        public static YoloDatasetSplitPlan BuildPlan(IReadOnlyList<YoloDatasetSplitImage> images, YoloDatasetSettings options)
        {
            ArgumentNullException.ThrowIfNull(images);
            ArgumentNullException.ThrowIfNull(options);
            if (options.ValidationPercent < 0 || options.ValidationPercent > 100 || options.TestPercent < 0 || options.TestPercent > 100 || options.ValidationPercent + options.TestPercent > 100)
                throw new ArgumentException("Validation and test percentages must be between 0 and 100 and sum to at most 100.");
            var result = options.Copy();
            var names = new HashSet<string>(images.Select(image => image.ImageName), StringComparer.OrdinalIgnoreCase);
            if (names.Count != images.Count) throw new System.IO.InvalidDataException("Duplicate annotation image identities must be renamed before splitting.");
            var manual = (options.ImageSplitAssignments ?? new()).Where(item => item != null && !item.IsAutomatic).ToArray();
            if ((options.ImageSplitAssignments ?? new()).Where(item => item != null).GroupBy(item => item.ImageName, StringComparer.OrdinalIgnoreCase)
                .Any(group => group.Select(item => NormalizeAssignmentSplit(item.Split)).Distinct().Count() > 1))
                throw new System.IO.InvalidDataException("Saved split assignments conflict for one image identity.");
            if (manual.Any(item => string.IsNullOrWhiteSpace(item.ImageName) || NormalizeAssignmentSplit(item.Split).Length == 0)
                || manual.GroupBy(item => item.ImageName, StringComparer.OrdinalIgnoreCase).Any(group => group.Select(item => NormalizeAssignmentSplit(item.Split)).Distinct().Count() > 1))
                throw new System.IO.InvalidDataException("Saved manual split assignments conflict or are malformed.");
            var frozen = new HashSet<string>((options.FrozenTestImages ?? new()).Select(item => item?.ImageName ?? string.Empty), StringComparer.OrdinalIgnoreCase);
            if (options.FreezeTestSplit)
            {
                if (options.FrozenTestImages == null || frozen.Count != options.FrozenTestImages.Count || options.FrozenTestImages.Any(item => item == null
                    || item.Split != TestMode || item.SourceSha256?.Length != 64 || item.SourceSha256.Any(ch => !Uri.IsHexDigit(ch))
                    || !images.Any(image => string.Equals(image.ImageName, item.ImageName, StringComparison.OrdinalIgnoreCase) && image.SourceSha256 == item.SourceSha256)))
                    throw new System.IO.InvalidDataException("The fixed test images are missing, replaced, or malformed. Restore the original images or explicitly unlock the test split.");
                if (manual.Any(item => names.Contains(item.ImageName) && (NormalizeAssignmentSplit(item.Split) == TestMode) != frozen.Contains(item.ImageName)))
                    throw new System.IO.InvalidDataException("A manual assignment conflicts with the fixed test split. Explicitly unlock it before changing test membership.");
            }
            if (options.StratifySplit && images.Any(image => (image.Strata == null || image.Strata.Count == 0)
                && !manual.Any(item => string.Equals(item.ImageName, image.ImageName, StringComparison.OrdinalIgnoreCase) && NormalizeAssignmentSplit(item.Split) == ExcludedMode)))
                throw new System.IO.InvalidDataException("Stratification needs valid saved labels or a saved anomaly judgement for every included image. Save or repair the labels first.");

            var groupByImage = images.ToDictionary(image => image, image => new List<YoloDatasetSplitImage> { image });
            var groups = new HashSet<List<YoloDatasetSplitImage>>(groupByImage.Values);
            // Known lineage keys form connected groups, including shared capture bundles and parent sources.
            var owners = new Dictionary<string, List<YoloDatasetSplitImage>>(StringComparer.Ordinal);
            foreach (YoloDatasetSplitImage image in images)
            {
                List<YoloDatasetSplitImage> group = groupByImage[image];
                foreach (string key in image.GroupKeys ?? Array.Empty<string>())
                {
                    if (owners.TryGetValue(key, out var other) && !ReferenceEquals(group, other))
                    {
                        if (group.Count < other.Count) (group, other) = (other, group);
                        group.AddRange(other);
                        groups.Remove(other);
                        foreach (YoloDatasetSplitImage moved in other)
                        {
                            groupByImage[moved] = group;
                            foreach (string oldKey in moved.GroupKeys ?? Array.Empty<string>()) owners[oldKey] = group;
                        }
                    }
                    owners[key] = group;
                }
            }
            var assignments = manual.Where(item => !names.Contains(item.ImageName)).Select(item => new YoloImageSplitAssignment
                { ImageName = item.ImageName, Split = item.Split, IsAutomatic = false, SourceSha256 = item.SourceSha256 }).ToList();
            int included = images.Count(image => !manual.Any(item => string.Equals(item.ImageName, image.ImageName, StringComparison.OrdinalIgnoreCase) && NormalizeAssignmentSplit(item.Split) == ExcludedMode));
            double[] proportions = { (100 - options.ValidationPercent - options.TestPercent) / 100D, options.ValidationPercent / 100D, options.TestPercent / 100D };
            if (options.FreezeTestSplit)
            {
                double remainder = proportions[0] + proportions[1];
                proportions = new[] { remainder > 0 ? proportions[0] / remainder : 1D, remainder > 0 ? proportions[1] / remainder : 0D, 0D };
            }
            int available = options.FreezeTestSplit ? included - frozen.Count : included;
            string[] strata = images.SelectMany(image => image.Strata ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            var totals = strata.ToDictionary(key => key, key => images.Count(image => image.Strata?.Contains(key) == true && (!options.FreezeTestSplit || !frozen.Contains(image.ImageName))
                && !manual.Any(item => string.Equals(item.ImageName, image.ImageName, StringComparison.OrdinalIgnoreCase) && NormalizeAssignmentSplit(item.Split) == ExcludedMode)), StringComparer.Ordinal);
            var counts = StandardModes.ToDictionary(mode => mode, _ => 0);
            var classCounts = StandardModes.ToDictionary(mode => mode, _ => strata.ToDictionary(key => key, _ => 0));
            var pending = new List<List<YoloDatasetSplitImage>>();
            void Assign(List<YoloDatasetSplitImage> group, string split)
            {
                foreach (YoloDatasetSplitImage image in group)
                {
                    YoloImageSplitAssignment explicitAssignment = manual.FirstOrDefault(item => string.Equals(item.ImageName, image.ImageName, StringComparison.OrdinalIgnoreCase));
                    assignments.Add(new YoloImageSplitAssignment { ImageName = image.ImageName, Split = split, IsAutomatic = explicitAssignment == null, SourceSha256 = image.SourceSha256 });
                    if (split == ExcludedMode) continue;
                    counts[split]++;
                    foreach (string key in image.Strata ?? Array.Empty<string>()) classCounts[split][key]++;
                }
            }
            foreach (var group in groups)
            {
                string[] anchors = group.SelectMany(image => manual.Where(item => string.Equals(item.ImageName, image.ImageName, StringComparison.OrdinalIgnoreCase)).Select(item => NormalizeAssignmentSplit(item.Split)))
                    .Concat(options.FreezeTestSplit && group.Any(image => frozen.Contains(image.ImageName)) ? new[] { TestMode } : Array.Empty<string>()).Distinct().ToArray();
                if (anchors.Length > 1) throw new System.IO.InvalidDataException("A known source group has conflicting manual or fixed-test assignments.");
                if (options.FreezeTestSplit && group.Any(image => frozen.Contains(image.ImageName)) && group.Any(image => !frozen.Contains(image.ImageName)))
                    throw new System.IO.InvalidDataException("A new image shares a source group with the fixed test set. Unlock the test split before repartitioning this group.");
                if (anchors.Length == 1) Assign(group, anchors[0]); else pending.Add(group);
            }
            foreach (var group in pending.OrderByDescending(group => options.StratifySplit ? group.SelectMany(image => image.Strata).Sum(key => 1D / Math.Max(1, totals[key])) : 0D)
                .ThenBy(group => StableBucket(group[0].ImageName, options.SplitSeed)).ThenBy(group => group[0].ImageName, StringComparer.OrdinalIgnoreCase))
            {
                string split;
                if (!options.StratifySplit && !options.FreezeTestSplit)
                    split = SelectModesForImage(group[0].ImageName, new YoloDatasetSettings { ValidationPercent = options.ValidationPercent, TestPercent = options.TestPercent, SplitSeed = options.SplitSeed }).Single();
                else
                {
                    var groupClasses = strata.ToDictionary(key => key, key => group.Count(image => image.Strata?.Contains(key) == true));
                    double Cost(int candidate)
                    {
                        double score = 0;
                        for (int mode = 0; mode < 3; mode++)
                        {
                            if (options.FreezeTestSplit && mode == 2) continue;
                            double actual = counts[StandardModeValues[mode]] + (candidate == mode ? group.Count : 0);
                            score += Math.Pow(actual - available * proportions[mode], 2) / Math.Max(1, available);
                            if (options.StratifySplit)
                                foreach (string key in strata)
                                    score += Math.Pow(classCounts[StandardModeValues[mode]][key] + (candidate == mode ? groupClasses[key] : 0) - totals[key] * proportions[mode], 2) / Math.Max(1, totals[key]);
                        }
                        return score;
                    }
                    split = Enumerable.Range(0, 3).Where(index => proportions[index] > 0).OrderBy(Cost).ThenBy(index => index).Select(index => StandardModeValues[index]).First();
                }
                Assign(group, split);
            }
            result.ImageSplitAssignments = assignments.OrderBy(item => item.ImageName, StringComparer.OrdinalIgnoreCase).ToList();
            if (!result.FreezeTestSplit) result.FrozenTestImages.Clear();
            return new YoloDatasetSplitPlan(result, images, counts, classCounts);
        }

        private static uint StableBucket(string value, int seed)
        {
            unchecked
            {
                uint hash = 2166136261;
                string key = (value ?? string.Empty).Trim().ToLowerInvariant();
                foreach (char ch in key)
                {
                    hash ^= ch;
                    hash *= 16777619;
                }

                hash ^= (uint)seed;
                hash *= 16777619;
                return hash % 100;
            }
        }
    }

    public sealed class YoloDatasetSplitImage
    {
        public string ImagePath { get; init; }
        public string ImageName { get; init; }
        public string SourceSha256 { get; init; }
        public IReadOnlyList<string> Strata { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> GroupKeys { get; init; } = Array.Empty<string>();
    }

    public sealed class YoloDatasetSplitPlan
    {
        internal YoloDatasetSplitPlan(YoloDatasetSettings settings, IReadOnlyList<YoloDatasetSplitImage> images, IReadOnlyDictionary<string, int> counts,
            IReadOnlyDictionary<string, Dictionary<string, int>> classCounts)
        { Settings = settings; Images = images; Counts = counts; ClassCounts = classCounts; }
        internal YoloDatasetSettings Settings { get; }
        internal IReadOnlyList<YoloDatasetSplitImage> Images { get; }
        public IReadOnlyDictionary<string, int> Counts { get; }
        public IReadOnlyDictionary<string, Dictionary<string, int>> ClassCounts { get; }
        internal string Fingerprint { get; set; }
    }
}
