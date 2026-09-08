using System;
using System.Collections.Generic;
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
            YoloDatasetSplitQualityService.AddStatistics(
                data.TrainImagesPath,
                data.ValidImagesPath,
                data.TestImagesPath,
                statistics);

            CountObjects(trainLabelsPath, data.ClassNamedList, statistics);
            CountObjects(validLabelsPath, data.ClassNamedList, statistics);
            CountObjects(testLabelsPath, data.ClassNamedList, statistics);
            CountSegmentationObjects(trainSegmentsPath, data.ClassNamedList, statistics);
            CountSegmentationObjects(validSegmentsPath, data.ClassNamedList, statistics);
            CountSegmentationObjects(testSegmentsPath, data.ClassNamedList, statistics);
            return statistics;
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

        private static void CountObjects(string labelDirectory, IReadOnlyList<LabelClass> classes, YoloDatasetStatistics statistics)
        {
            if (!Directory.Exists(labelDirectory) || classes == null || statistics == null)
            {
                return;
            }

            foreach (string labelPath in Directory.EnumerateFiles(labelDirectory, "*.txt"))
            {
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

        private static void CountSegmentationObjects(string segmentDirectory, IReadOnlyList<LabelClass> classes, YoloDatasetStatistics statistics)
        {
            if (!Directory.Exists(segmentDirectory) || statistics == null)
            {
                return;
            }

            foreach (string segmentPath in Directory.EnumerateFiles(segmentDirectory, "*.json"))
            {
                SegmentationAnnotationFile annotation;
                try
                {
                    annotation = JsonConvert.DeserializeObject<SegmentationAnnotationFile>(File.ReadAllText(segmentPath));
                }
                catch
                {
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
            }
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
