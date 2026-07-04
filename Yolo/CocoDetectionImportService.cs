using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace MvcVisionSystem.Yolo
{
    public static class CocoDetectionImportService
    {
        private static readonly string[] DatasetModes =
        {
            YoloDatasetSplitService.TrainMode,
            YoloDatasetSplitService.ValidMode,
            YoloDatasetSplitService.TestMode
        };

        public static CocoDetectionImportResult ImportDataset(
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
                throw new ArgumentException("COCO annotation path is required.", nameof(annotationPath));
            }

            if (!File.Exists(annotationPath))
            {
                throw new FileNotFoundException("COCO annotation file was not found.", annotationPath);
            }

            string split = NormalizeSplit(targetSplit);
            if (string.IsNullOrWhiteSpace(split))
            {
                throw new ArgumentException("Target split must be train, valid, or test.", nameof(targetSplit));
            }

            CocoDetectionDataset dataset = JsonConvert.DeserializeObject<CocoDetectionDataset>(File.ReadAllText(annotationPath));
            if (dataset == null)
            {
                throw new InvalidDataException("COCO annotation file could not be parsed.");
            }

            data.NormalizeOutputPaths();
            data.EnsureYoloOutputDirectories();

            var result = new CocoDetectionImportResult
            {
                AnnotationPath = annotationPath,
                ImageRoot = ResolveImageRoot(annotationPath, imageRoot),
                TargetSplit = split
            };

            Dictionary<int, int> categoryIdToClassIndex = BuildClassMap(data, dataset.Categories, result);
            Dictionary<int, List<CocoDetectionAnnotation>> annotationsByImageId = (dataset.Annotations ?? new List<CocoDetectionAnnotation>())
                .GroupBy(annotation => annotation.ImageId)
                .ToDictionary(group => group.Key, group => group.ToList());

            string imageDirectory = Path.Combine(data.OutputRootPath, "data", split, "images");
            string labelDirectory = Path.Combine(data.OutputRootPath, "data", split, "labels");
            Directory.CreateDirectory(imageDirectory);
            Directory.CreateDirectory(labelDirectory);

            foreach (CocoDetectionImage image in dataset.Images ?? new List<CocoDetectionImage>())
            {
                if (!TryImportImage(
                    data,
                    image,
                    annotationsByImageId,
                    categoryIdToClassIndex,
                    result,
                    imageDirectory,
                    labelDirectory))
                {
                    result.SkippedImageCount++;
                }
            }

            data.SaveYoloDataYaml();
            return result;
        }

        private static bool TryImportImage(
            CData data,
            CocoDetectionImage image,
            IReadOnlyDictionary<int, List<CocoDetectionAnnotation>> annotationsByImageId,
            IReadOnlyDictionary<int, int> categoryIdToClassIndex,
            CocoDetectionImportResult result,
            string imageDirectory,
            string labelDirectory)
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

            Size imageSize = ResolveImageSize(image, sourcePath);
            List<string> labelLines = BuildLabelLines(
                annotationsByImageId.TryGetValue(image.Id, out List<CocoDetectionAnnotation> annotations)
                    ? annotations
                    : new List<CocoDetectionAnnotation>(),
                categoryIdToClassIndex,
                imageSize,
                result);

            string labelPath = Path.Combine(labelDirectory, $"{Path.GetFileNameWithoutExtension(fileName)}.txt");
            File.WriteAllLines(labelPath, labelLines);

            result.ImportedImageCount++;
            result.LabelFileCount++;
            result.ImportedAnnotationCount += labelLines.Count;
            result.ImportedImagePaths.Add(targetImagePath);
            return true;
        }

        private static List<string> BuildLabelLines(
            IEnumerable<CocoDetectionAnnotation> annotations,
            IReadOnlyDictionary<int, int> categoryIdToClassIndex,
            Size imageSize,
            CocoDetectionImportResult result)
        {
            var lines = new List<string>();
            foreach (CocoDetectionAnnotation annotation in annotations ?? Enumerable.Empty<CocoDetectionAnnotation>())
            {
                if (annotation?.BBox == null
                    || annotation.BBox.Length < 4
                    || !categoryIdToClassIndex.TryGetValue(annotation.CategoryId, out int classIndex)
                    || !TryBuildRectangle(annotation.BBox, imageSize, out Rectangle bounds))
                {
                    result.SkippedAnnotationCount++;
                    continue;
                }

                string line = YoloAnnotationService.TryCreateYoloLine(classIndex, bounds, imageSize);
                if (string.IsNullOrWhiteSpace(line))
                {
                    result.SkippedAnnotationCount++;
                    continue;
                }

                lines.Add(line);
            }

            return lines;
        }

        private static Dictionary<int, int> BuildClassMap(
            CData data,
            IEnumerable<CocoDetectionCategory> categories,
            CocoDetectionImportResult result)
        {
            var map = new Dictionary<int, int>();
            foreach (CocoDetectionCategory category in (categories ?? Enumerable.Empty<CocoDetectionCategory>())
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

        private static bool TryBuildRectangle(double[] bbox, Size imageSize, out Rectangle bounds)
        {
            bounds = Rectangle.Empty;
            if (bbox == null || bbox.Length < 4 || imageSize.Width <= 0 || imageSize.Height <= 0)
            {
                return false;
            }

            int left = (int)Math.Round(bbox[0]);
            int top = (int)Math.Round(bbox[1]);
            int right = (int)Math.Round(bbox[0] + bbox[2]);
            int bottom = (int)Math.Round(bbox[1] + bbox[3]);
            if (right <= left || bottom <= top)
            {
                return false;
            }

            bounds = Rectangle.Intersect(
                Rectangle.FromLTRB(left, top, right, bottom),
                new Rectangle(Point.Empty, imageSize));
            return !bounds.IsEmpty;
        }

        private static Size ResolveImageSize(CocoDetectionImage image, string sourcePath)
        {
            if (image?.Width > 0 && image.Height > 0)
            {
                return new Size(image.Width, image.Height);
            }

            using Image source = Image.FromFile(sourcePath);
            return source.Size;
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

    public sealed class CocoDetectionImportResult
    {
        public string AnnotationPath { get; set; } = string.Empty;

        public string ImageRoot { get; set; } = string.Empty;

        public string TargetSplit { get; set; } = string.Empty;

        public int ImportedImageCount { get; set; }

        public int LabelFileCount { get; set; }

        public int ImportedAnnotationCount { get; set; }

        public int CategoryCount { get; set; }

        public int SkippedImageCount { get; set; }

        public int SkippedAnnotationCount { get; set; }

        public List<string> ImportedImagePaths { get; } = new List<string>();
    }
}
