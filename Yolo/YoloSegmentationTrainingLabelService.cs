using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MvcVisionSystem.Yolo
{
    public sealed class YoloSegmentationTrainingLabelExportResult
    {
        public int ImageCount { get; set; }

        public int LabelFileCount { get; set; }

        public int PolygonCount { get; set; }

        public int TrainPolygonCount { get; set; }

        public int ValidPolygonCount { get; set; }

        public int TestPolygonCount { get; set; }

        public int BackgroundImageCount { get; set; }

        public int EmptyLabelFileCount { get; set; }

        public List<string> Errors { get; } = new List<string>();

        public bool IsReady => Errors.Count == 0 && LabelFileCount > 0 && PolygonCount > 0;
    }

    public static class YoloSegmentationTrainingLabelService
    {
        private static readonly string[] DatasetModes =
        {
            YoloDatasetSplitService.TrainMode,
            YoloDatasetSplitService.ValidMode,
            YoloDatasetSplitService.TestMode
        };

        private static readonly string[] ImageExtensions = { ".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff" };

        public static YoloSegmentationTrainingLabelExportResult Export(CData data)
        {
            var result = new YoloSegmentationTrainingLabelExportResult();
            if (data == null)
            {
                result.Errors.Add("Dataset configuration is missing.");
                return result;
            }

            data.NormalizeOutputPaths();
            data.EnsureYoloOutputDirectories();
            ImportOkBackgroundImages(data, result);

            foreach (string mode in DatasetModes)
            {
                ExportMode(data, mode, result);
            }

            if (result.PolygonCount == 0)
            {
                result.Errors.Add("YOLOv8 segmentation training needs polygon segment JSON files that can be converted to labels/*.txt.");
            }
            if (result.TrainPolygonCount == 0)
            {
                result.Errors.Add("YOLOv8 segmentation training needs at least one train split polygon segment label.");
            }
            if (result.ValidPolygonCount == 0)
            {
                result.Errors.Add("YOLOv8 segmentation training needs at least one valid split polygon segment label.");
            }

            return result;
        }

        // The historical remediation audit must compare the exact conversion used for YOLO training without writing labels.
        internal static IReadOnlyList<string> BuildReadOnlyLabelLines(
            string segmentPath,
            string maskPath,
            IReadOnlyList<CClassItem> classes,
            string imagePath,
            out IReadOnlyList<string> errors)
        {
            var result = new YoloSegmentationTrainingLabelExportResult();
            List<string> lines = BuildLabelLines(segmentPath, maskPath, classes, imagePath, "audit", result);
            errors = result.Errors.ToList();
            return lines;
        }

        private static void ExportMode(CData data, string mode, YoloSegmentationTrainingLabelExportResult result)
        {
            string imageDirectory = Path.Combine(data.OutputRootPath, "data", mode, "images");
            string segmentDirectory = Path.Combine(data.OutputRootPath, "data", mode, "segments");
            string maskDirectory = Path.Combine(data.OutputRootPath, "data", mode, "masks");
            string labelDirectory = Path.Combine(data.OutputRootPath, "data", mode, "labels");
            Directory.CreateDirectory(labelDirectory);

            if (!Directory.Exists(imageDirectory))
            {
                return;
            }

            foreach (string imagePath in Directory.EnumerateFiles(imageDirectory).Where(IsSupportedImageFile))
            {
                result.ImageCount++;
                string fileStem = Path.GetFileNameWithoutExtension(imagePath);
                string segmentPath = Path.Combine(segmentDirectory, $"{fileStem}.json");
                string maskPath = Path.Combine(maskDirectory, $"{fileStem}.png");
                string labelPath = Path.Combine(labelDirectory, $"{fileStem}.txt");
                List<string> lines = BuildLabelLines(segmentPath, maskPath, data.ClassNamedList, imagePath, mode, result);
                if (lines.Count == 0)
                {
                    if (!IsEmptyLabelFile(labelPath))
                    {
                        if (File.Exists(labelPath))
                        {
                            File.Delete(labelPath);
                        }

                        continue;
                    }

                    File.WriteAllLines(labelPath, Array.Empty<string>());
                    result.LabelFileCount++;
                    result.EmptyLabelFileCount++;
                    continue;
                }

                File.WriteAllLines(labelPath, lines);
                result.LabelFileCount++;
                result.PolygonCount += lines.Count;
                if (string.Equals(mode, YoloDatasetSplitService.TrainMode, StringComparison.OrdinalIgnoreCase))
                {
                    result.TrainPolygonCount += lines.Count;
                }
                else if (string.Equals(mode, YoloDatasetSplitService.ValidMode, StringComparison.OrdinalIgnoreCase))
                {
                    result.ValidPolygonCount += lines.Count;
                }
                else if (string.Equals(mode, YoloDatasetSplitService.TestMode, StringComparison.OrdinalIgnoreCase))
                {
                    result.TestPolygonCount += lines.Count;
                }
            }
        }

        private static void ImportOkBackgroundImages(CData data, YoloSegmentationTrainingLabelExportResult result)
        {
            string sourceRoot = data?.ProjectSettings?.PythonModel?.ImageRootPath;
            if (string.IsNullOrWhiteSpace(sourceRoot) || !Directory.Exists(sourceRoot))
            {
                return;
            }

            foreach (string sourceImagePath in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)
                .Where(IsSupportedImageFile)
                .Where(path => IsUnderNamedFolder(sourceRoot, path, "OK")))
            {
                string fileStem = Path.GetFileNameWithoutExtension(sourceImagePath);
                string extension = Path.GetExtension(sourceImagePath);
                if (string.IsNullOrWhiteSpace(fileStem) || string.IsNullOrWhiteSpace(extension))
                {
                    continue;
                }

                IReadOnlyList<string> selectedModes = YoloDatasetSplitService.SelectModesForImage(fileStem, data.ProjectSettings?.YoloDataset);
                RemoveStaleOkBackgroundCopies(data, fileStem, selectedModes);

                foreach (string mode in selectedModes)
                {
                    string imageDirectory = Path.Combine(data.OutputRootPath, "data", mode, "images");
                    string labelDirectory = Path.Combine(data.OutputRootPath, "data", mode, "labels");
                    string targetImagePath = Path.Combine(imageDirectory, $"{fileStem}{extension}");
                    string labelPath = Path.Combine(labelDirectory, $"{fileStem}.txt");
                    if (HasSegmentationArtifact(data, mode, fileStem))
                    {
                        continue;
                    }

                    Directory.CreateDirectory(imageDirectory);
                    Directory.CreateDirectory(labelDirectory);
                    if (!File.Exists(targetImagePath))
                    {
                        File.Copy(sourceImagePath, targetImagePath);
                    }

                    File.WriteAllLines(labelPath, Array.Empty<string>());
                    result.BackgroundImageCount++;
                }
            }
        }

        private static void RemoveStaleOkBackgroundCopies(CData data, string fileStem, IReadOnlyList<string> selectedModes)
        {
            foreach (string mode in DatasetModes)
            {
                if (selectedModes.Contains(mode, StringComparer.OrdinalIgnoreCase)
                    || HasSegmentationArtifact(data, mode, fileStem))
                {
                    continue;
                }

                string modeRoot = Path.Combine(data.OutputRootPath, "data", mode);
                string labelPath = Path.Combine(modeRoot, "labels", $"{fileStem}.txt");
                if (!IsEmptyLabelFile(labelPath))
                {
                    continue;
                }

                File.Delete(labelPath);
                string imageDirectory = Path.Combine(modeRoot, "images");
                if (!Directory.Exists(imageDirectory))
                {
                    continue;
                }

                foreach (string imagePath in Directory.EnumerateFiles(imageDirectory, $"{fileStem}.*").Where(IsSupportedImageFile))
                {
                    File.Delete(imagePath);
                }
            }
        }

        private static bool HasSegmentationArtifact(CData data, string mode, string fileStem)
        {
            string modeRoot = Path.Combine(data.OutputRootPath, "data", mode);
            return File.Exists(Path.Combine(modeRoot, "segments", $"{fileStem}.json"))
                || File.Exists(Path.Combine(modeRoot, "masks", $"{fileStem}.png"));
        }

        private static bool IsEmptyLabelFile(string labelPath)
        {
            return File.Exists(labelPath)
                && File.ReadAllText(labelPath).Trim().Length == 0;
        }

        private static bool IsUnderNamedFolder(string root, string path, string folderName)
        {
            string rootFullPath = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            DirectoryInfo directory = new FileInfo(path).Directory;
            while (directory != null)
            {
                if (string.Equals(directory.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), rootFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (string.Equals(directory.Name, folderName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                directory = directory.Parent;
            }

            return false;
        }

        private static List<string> BuildLabelLines(
            string segmentPath,
            string maskPath,
            IReadOnlyList<CClassItem> classes,
            string imagePath,
            string mode,
            YoloSegmentationTrainingLabelExportResult result)
        {
            var lines = new List<string>();
            if (!File.Exists(segmentPath))
            {
                return lines;
            }

            SegmentationAnnotationFile annotation;
            try
            {
                annotation = JsonConvert.DeserializeObject<SegmentationAnnotationFile>(File.ReadAllText(segmentPath));
            }
            catch (Exception ex)
            {
                result.Errors.Add($"{mode} segment JSON is invalid for YOLO segmentation labels at {Path.GetFileName(segmentPath)}: {ex.Message}");
                return lines;
            }

            Size imageSize = ResolveImageSize(annotation, imagePath);
            if (imageSize.Width <= 0 || imageSize.Height <= 0)
            {
                result.Errors.Add($"{mode} image size could not be resolved for YOLO segmentation labels: {Path.GetFileName(imagePath)}");
                return lines;
            }

            IReadOnlyDictionary<string, List<LabelingSegmentationObject>> segmentsByClass =
                YoloSegmentationAnnotationService.LoadSegmentationObjects(
                    segmentPath,
                    maskPath,
                    classes,
                    imageSize);
            foreach (KeyValuePair<string, List<LabelingSegmentationObject>> classSegments in segmentsByClass)
            {
                int classIndex = ResolveClassIndex(classSegments.Key, classes);
                if (classIndex < 0)
                {
                    continue;
                }

                foreach (LabelingSegmentationObject segment in classSegments.Value ?? new List<LabelingSegmentationObject>())
                {
                    IReadOnlyList<List<Point>> polygons;
                    if (segment?.IsRasterMask == true)
                    {
                        List<SegmentationGeometry.SegmentationMaskRegion> regions = RasterMaskPolygonService.BuildRegions(
                            segment.MaskData,
                            segment.MaskSize,
                            imageSize).ToList();
                        polygons = regions.Count > 0
                            ? regions.Select(region => region.Points).ToList()
                            : new[] { SegmentationGeometry.RectangleToPolygon(segment.Bounds, imageSize) };
                    }
                    else
                    {
                        polygons = new[] { segment?.Points ?? new List<Point>() };
                    }

                    foreach (List<Point> polygon in polygons)
                    {
                        if (TryBuildLabelLine(classIndex, polygon, imageSize, out string line))
                        {
                            lines.Add(line);
                        }
                    }
                }
            }

            return lines;
        }

        private static Size ResolveImageSize(SegmentationAnnotationFile annotation, string imagePath)
        {
            if ((annotation?.ImageWidth ?? 0) > 0 && (annotation?.ImageHeight ?? 0) > 0)
            {
                return new Size(annotation.ImageWidth, annotation.ImageHeight);
            }

            try
            {
                using Image image = Image.FromFile(imagePath);
                return image.Size;
            }
            catch
            {
                return Size.Empty;
            }
        }

        private static bool TryBuildLabelLine(
            int classIndex,
            IEnumerable<Point> sourcePoints,
            Size imageSize,
            out string line)
        {
            line = string.Empty;
            if (classIndex < 0 || sourcePoints == null)
            {
                return false;
            }

            List<Point> points = SegmentationGeometry.NormalizePolygon(
                sourcePoints,
                imageSize,
                minimumDistance: 1);
            if (points.Count < 3)
            {
                return false;
            }

            var parts = new List<string> { classIndex.ToString(CultureInfo.InvariantCulture) };
            foreach (Point point in points)
            {
                parts.Add(FormatRatio(point.X / (double)imageSize.Width));
                parts.Add(FormatRatio(point.Y / (double)imageSize.Height));
            }

            line = string.Join(" ", parts);
            return true;
        }

        private static int ResolveClassIndex(string className, IReadOnlyList<CClassItem> classes)
        {
            if (string.IsNullOrWhiteSpace(className) || classes == null)
            {
                return -1;
            }

            for (int index = 0; index < classes.Count; index++)
            {
                if (string.Equals(classes[index]?.Text, className.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return -1;
        }

        private static string FormatRatio(double value)
        {
            return Math.Clamp(value, 0, 1).ToString("0.######", CultureInfo.InvariantCulture);
        }

        private static bool IsSupportedImageFile(string path)
        {
            return ImageExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
        }
    }
}
