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

            foreach (string mode in DatasetModes)
            {
                ExportMode(data, mode, result);
            }

            if (result.PolygonCount == 0)
            {
                result.Errors.Add("YOLOv8 segmentation training needs polygon segment JSON files that can be converted to labels/*.txt.");
            }

            return result;
        }

        private static void ExportMode(CData data, string mode, YoloSegmentationTrainingLabelExportResult result)
        {
            string imageDirectory = Path.Combine(data.OutputRootPath, "data", mode, "images");
            string segmentDirectory = Path.Combine(data.OutputRootPath, "data", mode, "segments");
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
                string labelPath = Path.Combine(labelDirectory, $"{fileStem}.txt");
                List<string> lines = BuildLabelLines(segmentPath, data.ClassNamedList, imagePath, mode, result);
                if (lines.Count == 0)
                {
                    if (File.Exists(labelPath))
                    {
                        File.Delete(labelPath);
                    }

                    continue;
                }

                File.WriteAllLines(labelPath, lines);
                result.LabelFileCount++;
                result.PolygonCount += lines.Count;
            }
        }

        private static List<string> BuildLabelLines(
            string segmentPath,
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

            foreach (SegmentationPolygonRecord record in annotation?.Polygons ?? new List<SegmentationPolygonRecord>())
            {
                if (!TryBuildLabelLine(record, classes, imageSize, out string line))
                {
                    continue;
                }

                lines.Add(line);
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
            SegmentationPolygonRecord record,
            IReadOnlyList<CClassItem> classes,
            Size imageSize,
            out string line)
        {
            line = string.Empty;
            int classIndex = ResolveClassIndex(record, classes);
            if (classIndex < 0 || record?.Points == null)
            {
                return false;
            }

            List<Point> points = SegmentationGeometry.NormalizePolygon(
                record.Points.Select(point => new Point(point.X, point.Y)),
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

        private static int ResolveClassIndex(SegmentationPolygonRecord record, IReadOnlyList<CClassItem> classes)
        {
            if (record == null)
            {
                return -1;
            }

            if (!string.IsNullOrWhiteSpace(record.ClassName) && classes != null)
            {
                for (int index = 0; index < classes.Count; index++)
                {
                    if (string.Equals(classes[index]?.Text, record.ClassName.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        return index;
                    }
                }
            }

            return classes != null && record.ClassIndex >= 0 && record.ClassIndex < classes.Count
                ? record.ClassIndex
                : -1;
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
