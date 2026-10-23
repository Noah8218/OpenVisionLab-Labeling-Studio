using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace MvcVisionSystem.Yolo
{
    public static class DatasetSplitAssignmentService
    {
        public const string ContractFileName = "dataset.splits.json";
        private static readonly string[] ArtifactFolders = { "images", "labels", "segments", "masks", "object-metadata" };
        private static readonly string[] ImageExtensions = { ".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff" };

        public static int Apply(
            LabelingProjectData data,
            IReadOnlyList<string> catalogImagePaths,
            IReadOnlyList<string> selectedImagePaths,
            string split,
            Func<bool> persistSettings)
        {
            ArgumentNullException.ThrowIfNull(data);
            ArgumentNullException.ThrowIfNull(persistSettings);
            string targetSplit = YoloDatasetSplitService.NormalizeAssignmentSplit(split);
            if (targetSplit.Length == 0) throw new ArgumentException("A supported dataset split is required.", nameof(split));
            data.NormalizeOutputPaths();
            if (string.IsNullOrWhiteSpace(data.OutputRootPath)) throw new InvalidOperationException("Dataset output storage is required.");
            string[] selected = (selectedImagePaths ?? Array.Empty<string>()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (selected.Length == 0) return 0;
            var catalog = new HashSet<string>(catalogImagePaths ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var names = new HashSet<string>(selected.Select(Path.GetFileNameWithoutExtension), StringComparer.OrdinalIgnoreCase);
            if (catalog.Where(path => names.Contains(Path.GetFileNameWithoutExtension(path)))
                .GroupBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase).Any(group => group.Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1))
                throw new InvalidDataException("Images with the same annotation filename must be renamed before assigning a split.");

            var artifacts = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (string source in selected)
            {
                if (!catalog.Contains(source) || !File.Exists(source) || !ImageExtensions.Contains(Path.GetExtension(source), StringComparer.OrdinalIgnoreCase))
                    throw new InvalidDataException("The selected source image is no longer available in this dataset.");
                string fullSource = Path.GetFullPath(source);
                if (YoloDatasetSplitService.AnnotationModes.Any(mode => fullSource.StartsWith(
                    Path.GetFullPath(Path.Combine(data.OutputRootPath, "data", mode)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("Source images must be outside managed split folders so reassignment can keep them read-only.");
                try
                {
                    using Image image = Image.FromFile(source);
                    YoloAnnotationService.EnsureImageIdentity(Path.GetFileName(source), image, data, source);
                }
                catch (Exception error) when (error is ArgumentException or OutOfMemoryException)
                {
                    throw new InvalidDataException("The selected source image could not be decoded.", error);
                }
                string name = Path.GetFileNameWithoutExtension(source);
                artifacts.Add(name, FindArtifacts(data.OutputRootPath, name));
            }

            YoloDatasetSettings settings = data.ProjectSettings.YoloDataset;
            List<YoloImageSplitAssignment> previous = settings.ImageSplitAssignments;
            List<YoloImageSplitAssignment> assignments = GetEffectiveAssignments(settings);
            int changed = selected.Count(path => !assignments.Any(item => !item.IsAutomatic && string.Equals(item.ImageName, Path.GetFileNameWithoutExtension(path), StringComparison.OrdinalIgnoreCase) && item.Split == targetSplit));
            if (changed == 0) return 0;
            assignments.RemoveAll(item => names.Contains(item.ImageName));
            assignments.AddRange(names.Select(name => new YoloImageSplitAssignment { ImageName = name, Split = targetSplit }));
            settings.ImageSplitAssignments = assignments.OrderBy(item => item.ImageName, StringComparer.OrdinalIgnoreCase).ToList();
            try
            {
                ValidateAssignmentBoundaries(data, catalogImagePaths);
                bool committed = AnnotationFilePersistence.ExecuteTransaction(() =>
                {
                    foreach (string source in selected)
                        RestageArtifacts(data.OutputRootPath, targetSplit, artifacts[Path.GetFileNameWithoutExtension(source)]);
                    SaveContract(data);
                    return persistSettings();
                });
                if (!committed) throw new IOException("Dataset split settings were not saved; assignments and artifacts were restored.");
                return changed;
            }
            catch
            {
                settings.ImageSplitAssignments = previous;
                throw;
            }
        }

        public static YoloDatasetSplitPlan Prepare(LabelingProjectData data, IReadOnlyList<string> catalogImagePaths, YoloDatasetSettings proposed)
        {
            ArgumentNullException.ThrowIfNull(data);
            ArgumentNullException.ThrowIfNull(proposed);
            if (data.ProjectSettings.AdditionalImageRootPaths?.Count > 0)
            {
                string missing = data.ProjectSettings.ResolveImageRootPaths().FirstOrDefault(root => !Directory.Exists(root));
                if (missing != null) throw new IOException(string.Format(OpenVisionLab.OpenVisionLanguageService.T("DatasetSourceFolders.Unavailable"), missing));
            }
            YoloDatasetSettings current = data.ProjectSettings.YoloDataset;
            if (current.FreezeTestSplit && proposed.FreezeTestSplit && current.FrozenTestImages == null) throw new InvalidDataException("The saved fixed test contract is malformed. Explicitly unlock it to recover.");
            YoloDatasetSettings options = current.Copy();
            options.ValidationPercent = proposed.ValidationPercent;
            options.TestPercent = proposed.TestPercent;
            options.SplitSeed = proposed.SplitSeed;
            options.StratifySplit = proposed.StratifySplit;
            options.FreezeTestSplit = proposed.FreezeTestSplit;
            string[] paths = (catalogImagePaths ?? Array.Empty<string>()).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
            if (paths.Length == 0) throw new InvalidDataException("Add source images before preparing a dataset split.");
            if (string.IsNullOrWhiteSpace(data.OutputRootPath)) throw new InvalidOperationException("Dataset output storage is required.");
            if (paths.GroupBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
                throw new InvalidDataException("Images with the same annotation filename must be renamed before splitting.");
            var identityParts = new List<string> { JsonConvert.SerializeObject(current), JsonConvert.SerializeObject(new { options.ValidationPercent, options.TestPercent, options.SplitSeed, options.StratifySplit, options.FreezeTestSplit }), data.ProjectSettings.DatasetPurpose.ToString(), JsonConvert.SerializeObject(data.ClassNamedList.Select(item => item?.Text)) };
            var reviews = new YoloImageReviewStatusService();
            var anomaly = new AnomalyImageReviewStatusService();
            if (options.StratifySplit)
            {
                ValidateReviewFile(YoloImageReviewStatusService.ResolveReviewStatusFilePath(data), false);
                reviews.LoadReviewStatus(data, paths);
                if (data.ProjectSettings.DatasetPurpose == LabelingDatasetPurpose.AnomalyDetection)
                {
                    ValidateReviewFile(AnomalyImageReviewStatusService.ResolveReviewStatusFilePath(data), true);
                    anomaly.LoadReviewStatus(data, paths);
                }
            }
            foreach (string reviewPath in new[] { YoloImageReviewStatusService.ResolveReviewStatusFilePath(data), AnomalyImageReviewStatusService.ResolveReviewStatusFilePath(data) })
                if (File.Exists(reviewPath)) identityParts.Add(reviewPath + ":" + HashingService.ComputeFileSha256(reviewPath, lowerCase: true));
            var groupSources = data.ProjectSettings.ResolveImageRootPaths().Append(data.OutputRootPath).Where(root => !string.IsNullOrWhiteSpace(root)).Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(root => (Root: root, Loaded: YoloDatasetSourceGroupService.LoadForDatasetRoot(root))).ToArray();
            foreach (var groups in groupSources)
            {
                if (!groups.Loaded.IsValid) throw new InvalidDataException(groups.Loaded.Summary);
                if (groups.Loaded.IsAvailable) identityParts.Add(groups.Loaded.SidecarPath + ":" + HashingService.ComputeFileSha256(groups.Loaded.SidecarPath, lowerCase: true));
            }
            var images = new List<YoloDatasetSplitImage>();
            foreach (string path in paths)
            {
                if (!File.Exists(path) || !ImageExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                    throw new InvalidDataException("The source image is no longer available: " + path);
                if (YoloDatasetSplitService.AnnotationModes.Any(mode => path.StartsWith(Path.GetFullPath(Path.Combine(data.OutputRootPath, "data", mode)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("Source images must remain outside managed split folders.");
                string name = Path.GetFileNameWithoutExtension(path);
                var strata = new List<string>();
                string sourceHash = HashingService.ComputeFileSha256(path, lowerCase: true);
                identityParts.Add(path + ":" + sourceHash);
                try
                {
                    using Image image = Image.FromFile(path);
                    YoloAnnotationService.EnsureImageIdentity(Path.GetFileName(path), image, data, path);
                    IReadOnlyList<string> artifacts = FindArtifacts(data.OutputRootPath, name);
                    foreach (string artifact in artifacts) identityParts.Add(artifact + ":" + HashingService.ComputeFileSha256(artifact, lowerCase: true));
                    bool excluded = current.ImageSplitAssignments?.Any(item => item != null && !item.IsAutomatic && string.Equals(item.ImageName, name, StringComparison.OrdinalIgnoreCase)
                        && YoloDatasetSplitService.NormalizeAssignmentSplit(item.Split) == YoloDatasetSplitService.ExcludedMode) == true;
                    if (options.StratifySplit && !excluded)
                    {
                        if (reviews.GetOrCreate(path).QualityReviewState == YoloImageQualityReviewState.NeedsFix)
                            throw new InvalidDataException("Repair the rejected label before stratification: " + Path.GetFileName(path));
                        if (data.ProjectSettings.DatasetPurpose == LabelingDatasetPurpose.AnomalyDetection)
                        {
                            AnomalyImageReviewState verdict = anomaly.GetOrCreate(path).ReviewState;
                            if (verdict == AnomalyImageReviewState.Unreviewed) throw new InvalidDataException("Save a normal/abnormal judgement before stratification: " + Path.GetFileName(path));
                            strata.Add("anomaly:" + verdict);
                        }
                        else
                        {
                            YoloAnnotationLoadResult label = YoloAnnotationService.LoadAnnotationRectanglesForImageWithDiagnostics(path, data.ClassNamedList, data, image.Size);
                            if (!label.HasSourceFile || label.HasErrors) throw new InvalidDataException("Save or repair the label before stratification: " + Path.GetFileName(path) + " " + label.ErrorSummary);
                            identityParts.Add(label.LabelPath + ":" + HashingService.ComputeFileSha256(label.LabelPath, lowerCase: true));
                            foreach (string className in label.Annotations.Where(pair => pair.Value.Count > 0).Select(pair => pair.Key)) strata.Add("class:" + className);
                            if (data.ProjectSettings.DatasetPurpose == LabelingDatasetPurpose.Segmentation && strata.Count > 0)
                            {
                                var segments = YoloSegmentationAnnotationService.LoadSegmentationObjectsForImage(path, data.ClassNamedList, data, image.Size);
                                if (segments.Values.Sum(items => items.Count) == 0) throw new InvalidDataException("Saved segmentation evidence is missing: " + Path.GetFileName(path));
                                strata.Clear();
                                strata.AddRange(segments.Where(pair => pair.Value.Count > 0).Select(pair => "class:" + pair.Key));
                            }
                            if (strata.Count == 0) strata.Add("no-object");
                        }
                    }
                }
                catch (Exception error) when (error is ArgumentException or OutOfMemoryException)
                { throw new InvalidDataException("The source image could not be decoded: " + path, error); }
                var groupKeys = new List<string>();
                foreach (var groups in groupSources)
                {
                    string relative = Path.GetRelativePath(groups.Root, path);
                    YoloDatasetSourceGroupResolution resolved = YoloDatasetSourceGroupService.Resolve(groups.Loaded, relative);
                    if (!resolved.IsKnown) continue;
                    foreach (var key in new[] { ("group:", resolved.SourceGroupId), ("original:", resolved.OriginalSourceId), ("capture:", resolved.CaptureBundleId) })
                        if (key.Item2 != YoloDatasetSourceGroupService.Unknown && !string.IsNullOrWhiteSpace(key.Item2)) groupKeys.Add(key.Item1 + key.Item2);
                }
                images.Add(new YoloDatasetSplitImage { ImagePath = path, ImageName = name, SourceSha256 = sourceHash, Strata = strata, GroupKeys = groupKeys.Distinct().ToArray() });
                if (sourceHash != HashingService.ComputeFileSha256(path, lowerCase: true)) throw new InvalidDataException("A source image changed while preparing the split: " + path);
            }
            if (options.FreezeTestSplit && !current.FreezeTestSplit)
                options.FrozenTestImages = images.Where(image => YoloDatasetSplitService.SelectModesForImage(image.ImageName, current).Single() == YoloDatasetSplitService.TestMode)
                    .Select(image => new YoloImageSplitAssignment { ImageName = image.ImageName, Split = YoloDatasetSplitService.TestMode, SourceSha256 = image.SourceSha256 }).ToList();
            YoloDatasetSplitPlan plan = YoloDatasetSplitService.BuildPlan(images, options);
            plan.Fingerprint = HashingService.ComputeUtf8TextSha256(string.Join("\n", identityParts), lowerCase: true);
            return plan;
        }

        public static int ApplyPrepared(LabelingProjectData data, IReadOnlyList<string> catalogImagePaths, YoloDatasetSplitPlan prepared, Func<bool> persistSettings)
        {
            ArgumentNullException.ThrowIfNull(prepared);
            ArgumentNullException.ThrowIfNull(persistSettings);
            YoloDatasetSplitPlan fresh = Prepare(data, catalogImagePaths, prepared.Settings);
            if (fresh.Fingerprint != prepared.Fingerprint) throw new InvalidDataException("The images, labels, groups, or Recipe settings changed. Prepare the split again before applying it.");
            YoloDatasetSettings settings = data.ProjectSettings.YoloDataset;
            YoloDatasetSettings previous = settings.Copy();
            var artifacts = fresh.Images.ToDictionary(image => image.ImageName, image => FindArtifacts(data.OutputRootPath, image.ImageName), StringComparer.OrdinalIgnoreCase);
            int changed = fresh.Images.Count(image => YoloDatasetSplitService.SelectModesForImage(image.ImageName, previous).Single() != YoloDatasetSplitService.SelectModesForImage(image.ImageName, fresh.Settings).Single());
            try
            {
                settings.CopyFrom(fresh.Settings);
                bool committed = AnnotationFilePersistence.ExecuteTransaction(() =>
                {
                    foreach (YoloDatasetSplitImage image in fresh.Images)
                        RestageArtifacts(data.OutputRootPath, YoloDatasetSplitService.SelectModesForImage(image.ImageName, settings).Single(), artifacts[image.ImageName]);
                    SaveContract(data);
                    return persistSettings();
                });
                if (!committed) throw new IOException("Dataset split settings were not saved; settings, assignments, and artifacts were restored.");
                return changed;
            }
            catch { settings.CopyFrom(previous); throw; }
        }

        private static void ValidateAssignmentBoundaries(LabelingProjectData data, IReadOnlyList<string> paths)
        {
            YoloDatasetSettings settings = data.ProjectSettings.YoloDataset;
            YoloDatasetSettings options = settings.Copy();
            options.StratifySplit = false;
            // Validate whole-source groups and frozen membership without repartitioning them.
            options.ImageSplitAssignments = (paths ?? Array.Empty<string>()).Select(path => new YoloImageSplitAssignment
                { ImageName = Path.GetFileNameWithoutExtension(path), Split = YoloDatasetSplitService.SelectModesForImage(Path.GetFileNameWithoutExtension(path), settings).Single() }).ToList();
            if (!settings.FreezeTestSplit && !data.ProjectSettings.ResolveImageRootPaths().Append(data.OutputRootPath).Any(root => File.Exists(YoloDatasetSourceGroupService.GetSidecarPath(root)))) return;
            YoloDatasetSplitPlan prepared = Prepare(data, paths, options);
            YoloDatasetSplitService.BuildPlan(prepared.Images, options);
        }

        private static void ValidateReviewFile(string path, bool anomaly)
        {
            if (!File.Exists(path)) return;
            JToken token;
            try { token = JToken.Parse(File.ReadAllText(path)); }
            catch (JsonException error) { throw new InvalidDataException("Saved review state is malformed: " + path, error); }
            JArray items = token as JArray;
            if (anomaly && token is JObject file)
            {
                if ((int?)file["schemaVersion"] != 1) throw new InvalidDataException("Unsupported anomaly review schema: " + path);
                items = file["items"] as JArray;
            }
            if (items == null || items.Any(item => item is not JObject)) throw new InvalidDataException("Saved review state is malformed: " + path);
            foreach (JObject item in items)
            {
                string stateKey = anomaly ? "reviewState" : "QualityReviewState";
                string nameKey = anomaly ? "reviewStateName" : "QualityReviewStateName";
                Type type = anomaly ? typeof(AnomalyImageReviewState) : typeof(YoloImageQualityReviewState);
                if (item[nameKey]?.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string)item[nameKey]))
                {
                    if (!Enum.TryParse(type, (string)item[nameKey], true, out object parsed) || !Enum.IsDefined(type, parsed))
                        throw new InvalidDataException("Saved review judgement is invalid: " + path);
                }
                else if (item[stateKey] != null && (item[stateKey].Type != JTokenType.Integer || !Enum.IsDefined(type, (int)item[stateKey])))
                    throw new InvalidDataException("Saved review judgement is invalid: " + path);
            }
        }

        internal static void SaveContract(LabelingProjectData data)
        {
            YoloDatasetSettings settings = data?.ProjectSettings?.YoloDataset;
            if (settings == null || string.IsNullOrWhiteSpace(data.OutputRootPath)) return;
            string path = Path.Combine(data.OutputRootPath, ContractFileName);
            List<YoloImageSplitAssignment> assignments = GetEffectiveAssignments(settings);
            if (assignments.Count == 0 && !settings.StratifySplit && !settings.FreezeTestSplit && !File.Exists(path)) return;
            // Recipe XML is authoritative. This generated projection participates in content identity.
            string json = JsonConvert.SerializeObject(new
            {
                schemaVersion = 2,
                validationPercent = settings.ValidationPercent,
                testPercent = settings.TestPercent,
                splitSeed = settings.SplitSeed,
                stratifySplit = settings.StratifySplit,
                freezeTestSplit = settings.FreezeTestSplit,
                frozenTestImages = settings.FrozenTestImages?.OrderBy(item => item.ImageName, StringComparer.OrdinalIgnoreCase)
                    .Select(item => new { imageName = item.ImageName.ToLowerInvariant(), sourceSha256 = item.SourceSha256 }),
                assignments = assignments.OrderBy(item => item.ImageName, StringComparer.OrdinalIgnoreCase)
                    .Select(item => new { imageName = item.ImageName.ToLowerInvariant(), split = item.Split, isAutomatic = item.IsAutomatic, sourceSha256 = item.SourceSha256 })
            }, Formatting.Indented);
            if (!File.Exists(path) || File.ReadAllText(path) != json)
                AnnotationFilePersistence.WriteAtomically(path, temporary => File.WriteAllText(temporary, json));
        }

        private static List<YoloImageSplitAssignment> GetEffectiveAssignments(YoloDatasetSettings settings)
        {
            return (settings?.ImageSplitAssignments ?? new List<YoloImageSplitAssignment>())
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.ImageName) && YoloDatasetSplitService.NormalizeAssignmentSplit(item.Split).Length > 0)
                .GroupBy(item => item.ImageName.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group => new YoloImageSplitAssignment { ImageName = group.Key, Split = YoloDatasetSplitService.NormalizeAssignmentSplit(group.First().Split), IsAutomatic = group.First().IsAutomatic, SourceSha256 = group.First().SourceSha256 }).ToList();
        }

        private static IReadOnlyList<string> FindArtifacts(string outputRoot, string name)
        {
            var paths = new List<string>();
            foreach (string mode in YoloDatasetSplitService.AnnotationModes)
            foreach (string folder in ArtifactFolders)
            {
                string directory = Path.Combine(outputRoot, "data", mode, folder);
                if (!Directory.Exists(directory)) continue;
                paths.AddRange(Directory.EnumerateFiles(directory).Where(path => string.Equals(Path.GetFileNameWithoutExtension(path), name, StringComparison.OrdinalIgnoreCase)
                    && (folder == "images" ? ImageExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)
                        : Path.GetExtension(path).Equals(folder == "labels" ? ".txt" : folder == "masks" ? ".png" : ".json", StringComparison.OrdinalIgnoreCase))));
            }
            foreach (IGrouping<string, string> group in paths.GroupBy(path => Path.GetFileName(Path.GetDirectoryName(path)) + "/" + Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
            {
                if (group.Select(path => HashingService.ComputeFileSha256(path, lowerCase: true)).Distinct(StringComparer.Ordinal).Count() > 1)
                    throw new InvalidDataException("Conflicting saved annotations exist in multiple splits; reassignment was cancelled.");
            }
            return paths;
        }

        private static void RestageArtifacts(string outputRoot, string split, IReadOnlyList<string> paths)
        {
            foreach (IGrouping<string, string> group in paths.GroupBy(path => Path.GetFileName(Path.GetDirectoryName(path)) + "/" + Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
            {
                string source = group.First();
                string target = Path.Combine(outputRoot, "data", split, Path.GetFileName(Path.GetDirectoryName(source)), Path.GetFileName(source));
                if (!group.Contains(target, StringComparer.OrdinalIgnoreCase))
                    AnnotationFilePersistence.WriteAtomically(target, temporary => File.Copy(source, temporary));
                foreach (string oldPath in group.Where(path => !string.Equals(path, target, StringComparison.OrdinalIgnoreCase)))
                    AnnotationFilePersistence.Delete(oldPath);
            }
        }
    }
}
