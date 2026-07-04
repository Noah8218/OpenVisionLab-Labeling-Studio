using MvcVisionSystem;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace MvcVisionSystem.Yolo
{
    public static class CocoSegmentationImportService
    {
        private static readonly string[] DatasetModes =
        {
            YoloDatasetSplitService.TrainMode,
            YoloDatasetSplitService.ValidMode,
            YoloDatasetSplitService.TestMode
        };

        public static CocoSegmentationImportResult ImportDataset(
            CData data,
            string annotationPath,
            string imageRoot,
            string targetSplit = YoloDatasetSplitService.TrainMode)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (string.IsNullOrWhiteSpace(annotationPath))
            {
                throw new ArgumentException("COCO segmentation annotation path is required.", nameof(annotationPath));
            }

            if (!File.Exists(annotationPath))
            {
                throw new FileNotFoundException("COCO segmentation annotation file was not found.", annotationPath);
            }

            string split = NormalizeSplit(targetSplit);
            if (string.IsNullOrWhiteSpace(split))
            {
                throw new ArgumentException("Target split must be train, valid, or test.", nameof(targetSplit));
            }

            CocoSegmentationDataset dataset = JsonConvert.DeserializeObject<CocoSegmentationDataset>(File.ReadAllText(annotationPath));
            if (dataset == null)
            {
                throw new InvalidDataException("COCO segmentation annotation file could not be parsed.");
            }

            data.NormalizeOutputPaths();
            data.EnsureYoloOutputDirectories();

            var result = new CocoSegmentationImportResult
            {
                AnnotationPath = annotationPath,
                ImageRoot = ResolveImageRoot(annotationPath, imageRoot),
                TargetSplit = split
            };

            Dictionary<int, int> categoryIdToClassIndex = BuildClassMap(data, dataset.Categories, result);
            Dictionary<int, List<CocoSegmentationAnnotation>> annotationsByImageId = (dataset.Annotations ?? new List<CocoSegmentationAnnotation>())
                .GroupBy(annotation => annotation.ImageId)
                .ToDictionary(group => group.Key, group => group.ToList());

            string imageDirectory = Path.Combine(data.OutputRootPath, "data", split, "images");
            Directory.CreateDirectory(imageDirectory);

            foreach (CocoSegmentationImage image in dataset.Images ?? new List<CocoSegmentationImage>())
            {
                if (!TryImportImage(data, image, annotationsByImageId, categoryIdToClassIndex, imageDirectory, result))
                {
                    result.SkippedImageCount++;
                }
            }

            data.SaveYoloDataYaml();
            return result;
        }

        private static bool TryImportImage(
            CData data,
            CocoSegmentationImage image,
            IReadOnlyDictionary<int, List<CocoSegmentationAnnotation>> annotationsByImageId,
            IReadOnlyDictionary<int, int> categoryIdToClassIndex,
            string imageDirectory,
            CocoSegmentationImportResult result)
        {
            if (image == null || string.IsNullOrWhiteSpace(image.FileName))
            {
                return false;
            }

            string sourcePath = ResolveSourceImagePath(result.ImageRoot, image.FileName);
            if (!File.Exists(sourcePath))
            {
                return false;
            }

            string fileName = Path.GetFileName(image.FileName.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            string targetImagePath = Path.Combine(imageDirectory, fileName);
            File.Copy(sourcePath, targetImagePath, overwrite: true);

            using Image sourceImage = Image.FromFile(sourcePath);
            Size imageSize = image.Width > 0 && image.Height > 0
                ? new Size(image.Width, image.Height)
                : sourceImage.Size;

            Dictionary<string, List<LabelingSegmentationObject>> segmentsByClass = BuildSegments(
                annotationsByImageId.TryGetValue(image.Id, out List<CocoSegmentationAnnotation> annotations)
                    ? annotations
                    : new List<CocoSegmentationAnnotation>(),
                categoryIdToClassIndex,
                data.ClassNamedList,
                imageSize,
                result);

            if (segmentsByClass.Count > 0)
            {
                SaveSegmentationArtifacts(data, result.TargetSplit, fileName, sourceImage, segmentsByClass);
                result.ImportedSegmentFileCount++;
                result.ImportedAnnotationCount += segmentsByClass.Values.Sum(list => list.Count);
            }

            result.ImportedImageCount++;
            result.ImportedImagePaths.Add(targetImagePath);
            return true;
        }

        private static Dictionary<string, List<LabelingSegmentationObject>> BuildSegments(
            IEnumerable<CocoSegmentationAnnotation> annotations,
            IReadOnlyDictionary<int, int> categoryIdToClassIndex,
            IReadOnlyList<CClassItem> classes,
            Size imageSize,
            CocoSegmentationImportResult result)
        {
            var segmentsByClass = new Dictionary<string, List<LabelingSegmentationObject>>(StringComparer.OrdinalIgnoreCase);
            foreach (CocoSegmentationAnnotation annotation in annotations ?? Enumerable.Empty<CocoSegmentationAnnotation>())
            {
                if (annotation == null
                    || annotation.Segmentation == null
                    || !categoryIdToClassIndex.TryGetValue(annotation.CategoryId, out int classIndex)
                    || classIndex < 0
                    || classIndex >= (classes?.Count ?? 0))
                {
                    result.SkippedAnnotationCount++;
                    continue;
                }

                foreach (double[] polygon in annotation.Segmentation)
                {
                    List<Point> points = BuildPolygonPoints(polygon, imageSize);
                    if (points.Count < 3)
                    {
                        result.SkippedAnnotationCount++;
                        continue;
                    }

                    string className = classes[classIndex].Text;
                    if (!segmentsByClass.TryGetValue(className, out List<LabelingSegmentationObject> segments))
                    {
                        segments = new List<LabelingSegmentationObject>();
                        segmentsByClass.Add(className, segments);
                    }

                    segments.Add(new LabelingSegmentationObject(points, classes[classIndex]));
                }
            }

            return segmentsByClass;
        }

        private static List<Point> BuildPolygonPoints(double[] polygon, Size imageSize)
        {
            if (polygon == null || polygon.Length < 6 || polygon.Length % 2 != 0)
            {
                return new List<Point>();
            }

            var points = new List<Point>();
            for (int i = 0; i < polygon.Length; i += 2)
            {
                points.Add(new Point(
                    (int)Math.Round(polygon[i]),
                    (int)Math.Round(polygon[i + 1])));
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

        private static Dictionary<int, int> BuildClassMap(
            CData data,
            IEnumerable<CocoSegmentationCategory> categories,
            CocoSegmentationImportResult result)
        {
            var map = new Dictionary<int, int>();
            foreach (CocoSegmentationCategory category in (categories ?? Enumerable.Empty<CocoSegmentationCategory>())
                .Where(item => item != null)
                .OrderBy(item => item.Id))
            {
                string className = ClassCatalogService.NormalizeClassName(category.Name);
                if (category.Id <= 0 || string.IsNullOrWhiteSpace(className))
                {
                    continue;
                }

                int classIndex = FindClassIndex(data, className);
                if (classIndex < 0)
                {
                    ClassCatalogService.TryAddClass(data, className, out _);
                    classIndex = FindClassIndex(data, className);
                }

                if (classIndex >= 0 && !map.ContainsKey(category.Id))
                {
                    map.Add(category.Id, classIndex);
                }
            }

            result.CategoryCount = map.Count;
            return map;
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

        private static string ResolveImageRoot(string annotationPath, string imageRoot)
            => string.IsNullOrWhiteSpace(imageRoot)
                ? Path.GetDirectoryName(Path.GetFullPath(annotationPath)) ?? string.Empty
                : Path.GetFullPath(imageRoot);

        private static string ResolveSourceImagePath(string imageRoot, string fileName)
        {
            string normalized = fileName.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
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

    public sealed class CocoSegmentationImportResult
    {
        public string AnnotationPath { get; set; } = string.Empty;

        public string ImageRoot { get; set; } = string.Empty;

        public string TargetSplit { get; set; } = string.Empty;

        public int ImportedImageCount { get; set; }

        public int ImportedAnnotationCount { get; set; }

        public int ImportedSegmentFileCount { get; set; }

        public int CategoryCount { get; set; }

        public int SkippedImageCount { get; set; }

        public int SkippedAnnotationCount { get; set; }

        public List<string> ImportedImagePaths { get; } = new List<string>();
    }
}
