using MvcVisionSystem;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace MvcVisionSystem.Yolo
{
    public static class LabelStudioSegmentationImportService
    {
        private static readonly string[] DatasetModes =
        {
            YoloDatasetSplitService.TrainMode,
            YoloDatasetSplitService.ValidMode,
            YoloDatasetSplitService.TestMode
        };

        public static LabelStudioSegmentationImportResult ImportTasks(
            CData data,
            string taskJsonPath,
            string imageRoot,
            string targetSplit = YoloDatasetSplitService.TrainMode)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (string.IsNullOrWhiteSpace(taskJsonPath))
            {
                throw new ArgumentException("Label Studio segmentation task JSON path is required.", nameof(taskJsonPath));
            }

            if (!File.Exists(taskJsonPath))
            {
                throw new FileNotFoundException("Label Studio segmentation task JSON was not found.", taskJsonPath);
            }

            string split = NormalizeSplit(targetSplit);
            if (string.IsNullOrWhiteSpace(split))
            {
                throw new ArgumentException("Target split must be train, valid, or test.", nameof(targetSplit));
            }

            List<LabelStudioSegmentationTask> tasks = JsonConvert.DeserializeObject<List<LabelStudioSegmentationTask>>(File.ReadAllText(taskJsonPath))
                ?? new List<LabelStudioSegmentationTask>();

            data.NormalizeOutputPaths();
            data.EnsureYoloOutputDirectories();

            var result = new LabelStudioSegmentationImportResult
            {
                TaskJsonPath = taskJsonPath,
                ImageRoot = ResolveImageRoot(taskJsonPath, imageRoot),
                TargetSplit = split
            };

            string imageDirectory = Path.Combine(data.OutputRootPath, "data", split, "images");
            Directory.CreateDirectory(imageDirectory);

            foreach (LabelStudioSegmentationTask task in tasks)
            {
                if (!TryImportTask(data, task, imageDirectory, result))
                {
                    result.SkippedTaskCount++;
                }
            }

            result.CategoryCount = data.ClassNamedList?.Count(item => !string.IsNullOrWhiteSpace(item?.Text)) ?? 0;
            data.SaveYoloDataYaml();
            return result;
        }

        private static bool TryImportTask(
            CData data,
            LabelStudioSegmentationTask task,
            string imageDirectory,
            LabelStudioSegmentationImportResult result)
        {
            string imageValue = task?.Data?.Image ?? string.Empty;
            if (string.IsNullOrWhiteSpace(imageValue))
            {
                return false;
            }

            string sourcePath = ResolveSourceImagePath(result.ImageRoot, imageValue);
            if (!File.Exists(sourcePath))
            {
                return false;
            }

            string fileName = Path.GetFileName(imageValue.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            string targetImagePath = Path.Combine(imageDirectory, fileName);
            File.Copy(sourcePath, targetImagePath, overwrite: true);

            using Image sourceImage = Image.FromFile(sourcePath);
            Dictionary<string, List<LabelingSegmentationObject>> segmentsByClass = BuildSegments(
                data,
                task,
                sourceImage.Size,
                result);

            if (segmentsByClass.Count > 0)
            {
                SaveSegmentationArtifacts(data, result.TargetSplit, fileName, sourceImage, segmentsByClass);
                result.ImportedSegmentFileCount++;
                result.ImportedResultCount += segmentsByClass.Values.Sum(list => list.Count);
            }

            result.ImportedTaskCount++;
            result.ImportedImagePaths.Add(targetImagePath);
            return true;
        }

        private static Dictionary<string, List<LabelingSegmentationObject>> BuildSegments(
            CData data,
            LabelStudioSegmentationTask task,
            Size fallbackImageSize,
            LabelStudioSegmentationImportResult result)
        {
            var segmentsByClass = new Dictionary<string, List<LabelingSegmentationObject>>(StringComparer.OrdinalIgnoreCase);
            IEnumerable<LabelStudioSegmentationResult> results = (task?.Annotations ?? new List<LabelStudioSegmentationAnnotation>())
                .SelectMany(annotation => annotation?.Result ?? new List<LabelStudioSegmentationResult>());

            foreach (LabelStudioSegmentationResult item in results)
            {
                if (!TryBuildSegment(data, item, fallbackImageSize, out string className, out LabelingSegmentationObject segment))
                {
                    result.SkippedResultCount++;
                    continue;
                }

                if (!segmentsByClass.TryGetValue(className, out List<LabelingSegmentationObject> segments))
                {
                    segments = new List<LabelingSegmentationObject>();
                    segmentsByClass.Add(className, segments);
                }

                segments.Add(segment);
            }

            return segmentsByClass;
        }

        private static bool TryBuildSegment(
            CData data,
            LabelStudioSegmentationResult item,
            Size fallbackImageSize,
            out string className,
            out LabelingSegmentationObject segment)
        {
            className = string.Empty;
            segment = null;
            if (item == null
                || !string.Equals(item.Type, "polygonlabels", StringComparison.OrdinalIgnoreCase)
                || item.Value?.PolygonLabels == null
                || item.Value.PolygonLabels.Length == 0)
            {
                return false;
            }

            className = ClassCatalogService.NormalizeClassName(item.Value.PolygonLabels[0]);
            int classIndex = FindOrAddClass(data, className);
            Size imageSize = item.OriginalWidth > 0 && item.OriginalHeight > 0
                ? new Size(item.OriginalWidth, item.OriginalHeight)
                : fallbackImageSize;
            if (classIndex < 0 || imageSize.Width <= 0 || imageSize.Height <= 0)
            {
                return false;
            }

            List<Point> points = BuildPolygonPoints(item.Value.Points, imageSize);
            if (points.Count < 3)
            {
                return false;
            }

            segment = new LabelingSegmentationObject(points, data.ClassNamedList[classIndex]);
            return true;
        }

        private static List<Point> BuildPolygonPoints(IEnumerable<double[]> pointValues, Size imageSize)
        {
            var points = new List<Point>();
            foreach (double[] pointValue in pointValues ?? Enumerable.Empty<double[]>())
            {
                if (pointValue == null || pointValue.Length < 2)
                {
                    continue;
                }

                points.Add(new Point(
                    (int)Math.Round(pointValue[0] / 100D * imageSize.Width),
                    (int)Math.Round(pointValue[1] / 100D * imageSize.Height)));
            }

            return SegmentationGeometry.NormalizePolygon(points, imageSize, minimumDistance: 1);
        }

        private static void SaveSegmentationArtifacts(
            CData data,
            string targetSplit,
            string fileName,
            Image image,
            IReadOnlyDictionary<string, List<LabelingSegmentationObject>> segmentsByClass)
        {
            int originalValidationPercent = data.ProjectSettings.YoloDataset.ValidationPercent;
            int originalTestPercent = data.ProjectSettings.YoloDataset.TestPercent;
            try
            {
                if (string.Equals(targetSplit, YoloDatasetSplitService.ValidMode, StringComparison.OrdinalIgnoreCase))
                {
                    data.ProjectSettings.YoloDataset.ValidationPercent = 100;
                    data.ProjectSettings.YoloDataset.TestPercent = 0;
                }
                else if (string.Equals(targetSplit, YoloDatasetSplitService.TestMode, StringComparison.OrdinalIgnoreCase))
                {
                    data.ProjectSettings.YoloDataset.ValidationPercent = 0;
                    data.ProjectSettings.YoloDataset.TestPercent = 100;
                }
                else
                {
                    data.ProjectSettings.YoloDataset.ValidationPercent = 0;
                    data.ProjectSettings.YoloDataset.TestPercent = 0;
                }

                YoloSegmentationAnnotationService.SaveSegmentationAnnotations(
                    fileName,
                    image,
                    segmentsByClass,
                    data.ClassNamedList,
                    data);
            }
            finally
            {
                data.ProjectSettings.YoloDataset.ValidationPercent = originalValidationPercent;
                data.ProjectSettings.YoloDataset.TestPercent = originalTestPercent;
            }
        }

        private static int FindOrAddClass(CData data, string className)
        {
            if (string.IsNullOrWhiteSpace(className))
            {
                return -1;
            }

            int classIndex = FindClassIndex(data, className);
            if (classIndex >= 0)
            {
                return classIndex;
            }

            ClassCatalogService.TryAddClass(data, className, out _);
            return FindClassIndex(data, className);
        }

        private static int FindClassIndex(CData data, string className)
        {
            for (int index = 0; index < (data.ClassNamedList?.Count ?? 0); index++)
            {
                if (string.Equals(data.ClassNamedList[index]?.Text, className, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return -1;
        }

        private static string ResolveImageRoot(string taskJsonPath, string imageRoot)
            => string.IsNullOrWhiteSpace(imageRoot)
                ? Path.GetDirectoryName(Path.GetFullPath(taskJsonPath)) ?? string.Empty
                : Path.GetFullPath(imageRoot);

        private static string ResolveSourceImagePath(string imageRoot, string imageValue)
        {
            string normalized = imageValue.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            return Path.IsPathRooted(normalized)
                ? normalized
                : Path.Combine(imageRoot ?? string.Empty, normalized);
        }

        private static string NormalizeSplit(string split)
        {
            foreach (string mode in DatasetModes)
            {
                if (string.Equals(split, mode, StringComparison.OrdinalIgnoreCase))
                {
                    return mode;
                }
            }

            return string.Empty;
        }
    }

    public sealed class LabelStudioSegmentationImportResult
    {
        public string TaskJsonPath { get; set; } = string.Empty;

        public string ImageRoot { get; set; } = string.Empty;

        public string TargetSplit { get; set; } = string.Empty;

        public int ImportedTaskCount { get; set; }

        public int ImportedResultCount { get; set; }

        public int ImportedSegmentFileCount { get; set; }

        public int CategoryCount { get; set; }

        public int SkippedTaskCount { get; set; }

        public int SkippedResultCount { get; set; }

        public List<string> ImportedImagePaths { get; } = new List<string>();
    }
}
