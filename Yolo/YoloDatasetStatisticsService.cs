using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace MvcVisionSystem.Yolo
{
    /// <summary>
    /// Owns read-only dataset file and annotation statistics projection.
    /// Validation entry-point policy remains with YoloDatasetValidator.
    /// </summary>
    public static class YoloDatasetStatisticsService
    {
        private static readonly string[] ImageExtensions = { ".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff" };

        public static YoloDatasetStatistics Build(LabelingProjectData data)
        {
            var statistics = new YoloDatasetStatistics();
            if (data == null)
            {
                return statistics;
            }

            data.NormalizeOutputPaths();
            string trainLabelsPath = Path.Combine(data.OutputRootPath, "data", "train", "labels");
            string validLabelsPath = Path.Combine(data.OutputRootPath, "data", "valid", "labels");
            string testLabelsPath = Path.Combine(data.OutputRootPath, "data", "test", "labels");
            string trainSegmentsPath = Path.Combine(data.OutputRootPath, "data", "train", "segments");
            string validSegmentsPath = Path.Combine(data.OutputRootPath, "data", "valid", "segments");
            string testSegmentsPath = Path.Combine(data.OutputRootPath, "data", "test", "segments");
            string trainMasksPath = Path.Combine(data.OutputRootPath, "data", "train", "masks");
            string validMasksPath = Path.Combine(data.OutputRootPath, "data", "valid", "masks");
            string testMasksPath = Path.Combine(data.OutputRootPath, "data", "test", "masks");

            statistics.TrainImageCount = CountImages(data.TrainImagesPath);
            statistics.ValidImageCount = CountImages(data.ValidImagesPath);
            statistics.TestImageCount = CountImages(data.TestImagesPath);
            statistics.TrainLabelCount = CountFiles(trainLabelsPath, "*.txt");
            statistics.ValidLabelCount = CountFiles(validLabelsPath, "*.txt");
            statistics.TestLabelCount = CountFiles(testLabelsPath, "*.txt");
            statistics.TrainEmptyLabelFileCount = CountEmptyLabelFiles(trainLabelsPath);
            statistics.ValidEmptyLabelFileCount = CountEmptyLabelFiles(validLabelsPath);
            statistics.TestEmptyLabelFileCount = CountEmptyLabelFiles(testLabelsPath);
            statistics.TrainSegmentFileCount = CountFiles(trainSegmentsPath, "*.json");
            statistics.ValidSegmentFileCount = CountFiles(validSegmentsPath, "*.json");
            statistics.TestSegmentFileCount = CountFiles(testSegmentsPath, "*.json");
            statistics.TrainMaskFileCount = CountFiles(trainMasksPath, "*.png");
            statistics.ValidMaskFileCount = CountFiles(validMasksPath, "*.png");
            statistics.TestMaskFileCount = CountFiles(testMasksPath, "*.png");
            statistics.TrainSegmentationBackgroundImageCount = CountSegmentationBackgroundImages(data.TrainImagesPath, trainLabelsPath, trainSegmentsPath, trainMasksPath);
            statistics.ValidSegmentationBackgroundImageCount = CountSegmentationBackgroundImages(data.ValidImagesPath, validLabelsPath, validSegmentsPath, validMasksPath);
            statistics.TestSegmentationBackgroundImageCount = CountSegmentationBackgroundImages(data.TestImagesPath, testLabelsPath, testSegmentsPath, testMasksPath);
            YoloDatasetSplitQualityService.AddStatistics(
                data.TrainImagesPath,
                data.ValidImagesPath,
                data.TestImagesPath,
                statistics);

            CountObjects(trainLabelsPath, data.ClassNamedList, statistics);
            CountObjects(validLabelsPath, data.ClassNamedList, statistics);
            CountObjects(testLabelsPath, data.ClassNamedList, statistics);
            bool collectSegmentationSplitCounts = data.ProjectSettings?.DatasetPurpose == LabelingDatasetPurpose.Segmentation;
            CountSegmentationObjects(trainSegmentsPath, data.ClassNamedList, statistics, split: collectSegmentationSplitCounts ? YoloDatasetSplitService.TrainMode : null, imageDirectory: data.TrainImagesPath);
            CountSegmentationObjects(validSegmentsPath, data.ClassNamedList, statistics, split: collectSegmentationSplitCounts ? YoloDatasetSplitService.ValidMode : null, imageDirectory: data.ValidImagesPath);
            CountSegmentationObjects(testSegmentsPath, data.ClassNamedList, statistics, split: collectSegmentationSplitCounts ? YoloDatasetSplitService.TestMode : null, imageDirectory: data.TestImagesPath);
            return statistics;
        }

        public static IReadOnlyDictionary<string, int> BuildClassObjectCounts(LabelingProjectData data, bool includeExcluded = false, IReadOnlySet<string> imageNames = null)
        {
            var statistics = new YoloDatasetStatistics();
            if (data == null) return statistics.ObjectCountByClass;
            foreach (string mode in includeExcluded ? YoloDatasetSplitService.AnnotationModes : YoloDatasetSplitService.StandardModes)
            {
                CountObjects(Path.Combine(data.OutputRootPath, "data", mode, "labels"), data.ClassNamedList, statistics, imageNames);
                CountSegmentationObjects(Path.Combine(data.OutputRootPath, "data", mode, "segments"), data.ClassNamedList, statistics, imageNames);
            }
            return data.ClassNamedList.Where(item => item != null).GroupBy(item => item.Text, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => data.ProjectSettings?.DatasetPurpose == LabelingDatasetPurpose.Segmentation
                    ? Math.Max(statistics.ObjectCountByClass.GetValueOrDefault(group.Key), statistics.SegmentationObjectCountByClass.GetValueOrDefault(group.Key))
                    : statistics.ObjectCountByClass.GetValueOrDefault(group.Key), StringComparer.OrdinalIgnoreCase);
        }

        private static IEnumerable<string> EnumerateSupportedImages(string directory)
        {
            if (!Directory.Exists(directory))
            {
                return Enumerable.Empty<string>();
            }

            return Directory.EnumerateFiles(directory).Where(IsSupportedImageFile);
        }

        private static bool IsSupportedImageFile(string path)
        {
            string extension = Path.GetExtension(path);
            return ImageExtensions.Any(item => string.Equals(item, extension, StringComparison.OrdinalIgnoreCase));
        }

        private static int CountImages(string directory)
        {
            return EnumerateSupportedImages(directory).Count();
        }

        private static int CountFiles(string directory, string searchPattern)
        {
            if (!Directory.Exists(directory))
            {
                return 0;
            }

            return Directory.EnumerateFiles(directory, searchPattern).Count();
        }

        private static int CountEmptyLabelFiles(string directory)
        {
            if (!Directory.Exists(directory))
            {
                return 0;
            }

            return Directory.EnumerateFiles(directory, "*.txt")
                .Count(path => File.ReadAllText(path).Trim().Length == 0);
        }

        private static int CountSegmentationBackgroundImages(string imageDirectory, string labelDirectory, string segmentDirectory, string maskDirectory)
        {
            return EnumerateSupportedImages(imageDirectory).Count(imagePath =>
            {
                string fileStem = Path.GetFileNameWithoutExtension(imagePath);
                if (File.Exists(Path.Combine(segmentDirectory, fileStem + ".json"))
                    || File.Exists(Path.Combine(maskDirectory, fileStem + ".png")))
                {
                    return false;
                }

                string labelPath = Path.Combine(labelDirectory, fileStem + ".txt");
                return File.Exists(labelPath) && File.ReadAllText(labelPath).Trim().Length == 0;
            });
        }

        private static void CountObjects(string labelDirectory, IReadOnlyList<LabelClass> classes, YoloDatasetStatistics statistics, IReadOnlySet<string> imageNames = null)
        {
            if (!Directory.Exists(labelDirectory) || classes == null || statistics == null)
            {
                return;
            }

            foreach (string labelPath in Directory.EnumerateFiles(labelDirectory, "*.txt"))
            {
                if (imageNames != null && !imageNames.Contains(Path.GetFileNameWithoutExtension(labelPath))) continue;
                foreach (string line in File.ReadLines(labelPath))
                {
                    string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length != 5 || !int.TryParse(parts[0], out int classIndex))
                    {
                        continue;
                    }

                    if (classIndex < 0 || classIndex >= classes.Count)
                    {
                        continue;
                    }

                    statistics.AddObject(classes[classIndex]?.Text ?? "");
                }
            }
        }

        private static void CountSegmentationObjects(
            string segmentDirectory,
            IReadOnlyList<LabelClass> classes,
            YoloDatasetStatistics statistics,
            IReadOnlySet<string> imageNames = null,
            string split = null,
            string imageDirectory = null)
        {
            if (statistics == null)
            {
                return;
            }

            Dictionary<string, string[]> imagePaths = split == null ? null : EnumerateSupportedImages(imageDirectory)
                .GroupBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> splitCounts = split == null ? null : (classes ?? Array.Empty<LabelClass>())
                .Where(item => !string.IsNullOrWhiteSpace(item?.Text))
                .Select(item => item.Text).Distinct(StringComparer.OrdinalIgnoreCase)
                .ToDictionary(name => name, _ => 0, StringComparer.OrdinalIgnoreCase);
            var remainingImages = imagePaths == null ? null : new HashSet<string>(imagePaths.Keys, StringComparer.OrdinalIgnoreCase);
            bool hasCompleteSplit = split != null && Directory.Exists(imageDirectory)
                && splitCounts.Count > 0 && imagePaths.Values.All(paths => paths.Length == 1);

            foreach (string segmentPath in Directory.Exists(segmentDirectory)
                ? Directory.EnumerateFiles(segmentDirectory, "*.json")
                : Enumerable.Empty<string>())
            {
                string fileStem = Path.GetFileNameWithoutExtension(segmentPath);
                if (imageNames != null && !imageNames.Contains(fileStem)) continue;
                SegmentationAnnotationFile annotation;
                try
                {
                    annotation = JsonConvert.DeserializeObject<SegmentationAnnotationFile>(File.ReadAllText(segmentPath));
                }
                catch
                {
                    if (imagePaths?.ContainsKey(fileStem) == true) hasCompleteSplit = false;
                    continue;
                }

                foreach (SegmentationPolygonRecord record in annotation?.Polygons ?? new List<SegmentationPolygonRecord>())
                {
                    if (record?.Points == null || record.Points.Count < 3)
                    {
                        continue;
                    }

                    statistics.AddSegmentationObject(ResolveSegmentationClassName(record, classes));
                }

                if (imagePaths == null || !imagePaths.TryGetValue(fileStem, out string[] paths)) continue;
                remainingImages.Remove(fileStem);
                try
                {
                    using FileStream imageStream = File.OpenRead(paths[0]);
                    using Image image = Image.FromStream(imageStream);
                    if (annotation?.Polygons == null || annotation.Polygons.Count == 0
                        || annotation.ImageWidth > 0 && annotation.ImageWidth != image.Width
                        || annotation.ImageHeight > 0 && annotation.ImageHeight != image.Height)
                    {
                        hasCompleteSplit = false;
                        continue;
                    }

                    string maskPath = Path.Combine(Path.GetDirectoryName(segmentDirectory), "masks", fileStem + ".png");
                    YoloSegmentationAnnotationService.LoadSegmentationObjects(annotation, segmentPath, maskPath, classes, image.Size);
                    foreach (SegmentationPolygonRecord record in annotation.Polygons)
                    {
                        string className = ResolveSegmentationClassName(record, classes);
                        if (!splitCounts.ContainsKey(className))
                        {
                            hasCompleteSplit = false;
                            break;
                        }

                        splitCounts[className]++;
                    }
                }
                catch
                {
                    hasCompleteSplit = false;
                }
            }

            if (split == null) return;
            foreach (string fileStem in remainingImages)
            {
                string splitDirectory = Path.GetDirectoryName(segmentDirectory);
                string maskPath = Path.Combine(splitDirectory, "masks", fileStem + ".png");
                string labelPath = Path.Combine(splitDirectory, "labels", fileStem + ".txt");
                try
                {
                    using FileStream imageStream = File.OpenRead(imagePaths[fileStem][0]);
                    using Image image = Image.FromStream(imageStream);
                    if (File.Exists(maskPath) || !File.Exists(labelPath) || File.ReadAllText(labelPath).Trim().Length != 0)
                    {
                        hasCompleteSplit = false;
                    }
                }
                catch
                {
                    hasCompleteSplit = false;
                }
            }
            statistics.SetSegmentationObjectCounts(split, hasCompleteSplit ? splitCounts : null);
        }

        private static string ResolveSegmentationClassName(SegmentationPolygonRecord record, IReadOnlyList<LabelClass> classes)
        {
            if (!string.IsNullOrWhiteSpace(record?.ClassName))
            {
                return record.ClassName;
            }

            return record != null && classes != null && record.ClassIndex >= 0 && record.ClassIndex < classes.Count
                ? classes[record.ClassIndex]?.Text ?? string.Empty
                : string.Empty;
        }
    }
}
