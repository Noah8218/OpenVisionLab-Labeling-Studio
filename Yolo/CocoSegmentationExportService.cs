using MvcVisionSystem;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace MvcVisionSystem.Yolo
{
    public static class CocoSegmentationExportService
    {
        private static readonly string[] DatasetModes =
        {
            YoloDatasetSplitService.TrainMode,
            YoloDatasetSplitService.ValidMode,
            YoloDatasetSplitService.TestMode
        };

        private static readonly string[] ImageExtensions = { ".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff" };

        public static CocoSegmentationExportResult ExportDataset(
            CData data,
            string outputPath,
            IEnumerable<string> splits = null)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException("COCO segmentation export output path is required.", nameof(outputPath));
            }

            CocoSegmentationExportResult result = BuildDataset(data, splits, out CocoSegmentationDataset dataset);
            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(outputPath, JsonConvert.SerializeObject(dataset, Formatting.Indented), new UTF8Encoding(false));
            result.OutputPath = outputPath;
            return result;
        }

        public static CocoSegmentationExportResult BuildDataset(
            CData data,
            IEnumerable<string> splits,
            out CocoSegmentationDataset dataset)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            data.NormalizeOutputPaths();
            var result = new CocoSegmentationExportResult();
            dataset = new CocoSegmentationDataset();

            IReadOnlyList<string> normalizedSplits = NormalizeSplits(splits);
            result.Splits.AddRange(normalizedSplits);

            for (int classIndex = 0; classIndex < (data.ClassNamedList?.Count ?? 0); classIndex++)
            {
                string className = data.ClassNamedList[classIndex]?.Text ?? string.Empty;
                if (string.IsNullOrWhiteSpace(className))
                {
                    continue;
                }

                dataset.Categories.Add(new CocoSegmentationCategory
                {
                    Id = classIndex + 1,
                    Name = className.Trim(),
                    SuperCategory = "object"
                });
            }

            int nextImageId = 1;
            int nextAnnotationId = 1;
            foreach (string split in normalizedSplits)
            {
                string imageDirectory = Path.Combine(data.OutputRootPath, "data", split, "images");
                string segmentDirectory = Path.Combine(data.OutputRootPath, "data", split, "segments");
                foreach (string imagePath in EnumerateImageFiles(imageDirectory))
                {
                    using Image image = Image.FromFile(imagePath);
                    int imageId = nextImageId++;
                    dataset.Images.Add(new CocoSegmentationImage
                    {
                        Id = imageId,
                        FileName = FormatRelativePath(data.OutputRootPath, imagePath),
                        Width = image.Width,
                        Height = image.Height
                    });

                    string segmentPath = Path.Combine(segmentDirectory, $"{Path.GetFileNameWithoutExtension(imagePath)}.json");
                    if (!File.Exists(segmentPath))
                    {
                        continue;
                    }

                    foreach (SegmentationPolygonRecord record in LoadPolygonRecords(segmentPath, result))
                    {
                        if (!TryBuildAnnotation(
                            record,
                            data.ClassNamedList,
                            image.Size,
                            imageId,
                            nextAnnotationId,
                            out CocoSegmentationAnnotation annotation))
                        {
                            result.SkippedAnnotationCount++;
                            continue;
                        }

                        dataset.Annotations.Add(annotation);
                        nextAnnotationId++;
                    }
                }
            }

            result.ImageCount = dataset.Images.Count;
            result.AnnotationCount = dataset.Annotations.Count;
            result.CategoryCount = dataset.Categories.Count;
            return result;
        }

        private static IEnumerable<SegmentationPolygonRecord> LoadPolygonRecords(
            string segmentPath,
            CocoSegmentationExportResult result)
        {
            SegmentationAnnotationFile annotation;
            try
            {
                annotation = JsonConvert.DeserializeObject<SegmentationAnnotationFile>(File.ReadAllText(segmentPath));
            }
            catch
            {
                result.SkippedAnnotationCount++;
                yield break;
            }

            foreach (SegmentationPolygonRecord record in annotation?.Polygons ?? new List<SegmentationPolygonRecord>())
            {
                yield return record;
            }
        }

        private static bool TryBuildAnnotation(
            SegmentationPolygonRecord record,
            IReadOnlyList<CClassItem> classes,
            Size imageSize,
            int imageId,
            int annotationId,
            out CocoSegmentationAnnotation annotation)
        {
            annotation = null;
            if (record?.Points == null || imageSize.Width <= 0 || imageSize.Height <= 0)
            {
                return false;
            }

            int classIndex = ResolveClassIndex(record, classes);
            if (classIndex < 0)
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

            double[] segmentation = BuildSegmentation(points);
            if (segmentation.Length < 6 || !TryBuildBoundingBox(points, out double[] boundingBox))
            {
                return false;
            }

            double area = Math.Abs(CalculatePolygonArea(points));
            if (area <= 0D)
            {
                return false;
            }

            annotation = new CocoSegmentationAnnotation
            {
                Id = annotationId,
                ImageId = imageId,
                CategoryId = classIndex + 1,
                Segmentation = new List<double[]> { segmentation },
                BBox = boundingBox,
                Area = area,
                IsCrowd = 0
            };
            return true;
        }

        private static int ResolveClassIndex(SegmentationPolygonRecord record, IReadOnlyList<CClassItem> classes)
        {
            if (classes == null || classes.Count == 0)
            {
                return -1;
            }

            if (record.ClassIndex >= 0
                && record.ClassIndex < classes.Count
                && !string.IsNullOrWhiteSpace(classes[record.ClassIndex]?.Text))
            {
                return record.ClassIndex;
            }

            if (string.IsNullOrWhiteSpace(record.ClassName))
            {
                return -1;
            }

            for (int index = 0; index < classes.Count; index++)
            {
                if (string.Equals(classes[index]?.Text, record.ClassName, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return -1;
        }

        private static double[] BuildSegmentation(IReadOnlyList<Point> points)
        {
            var values = new double[points.Count * 2];
            for (int i = 0; i < points.Count; i++)
            {
                values[i * 2] = points[i].X;
                values[(i * 2) + 1] = points[i].Y;
            }

            return values;
        }

        private static bool TryBuildBoundingBox(IReadOnlyList<Point> points, out double[] boundingBox)
        {
            boundingBox = Array.Empty<double>();
            if (points == null || points.Count == 0)
            {
                return false;
            }

            int minX = points.Min(point => point.X);
            int minY = points.Min(point => point.Y);
            int maxX = points.Max(point => point.X);
            int maxY = points.Max(point => point.Y);
            double width = maxX - minX;
            double height = maxY - minY;
            if (width <= 0D || height <= 0D)
            {
                return false;
            }

            boundingBox = new[] { (double)minX, minY, width, height };
            return true;
        }

        private static double CalculatePolygonArea(IReadOnlyList<Point> points)
        {
            double area = 0D;
            for (int i = 0; i < points.Count; i++)
            {
                Point first = points[i];
                Point second = points[(i + 1) % points.Count];
                area += (first.X * second.Y) - (second.X * first.Y);
            }

            return area / 2D;
        }

        private static IReadOnlyList<string> NormalizeSplits(IEnumerable<string> splits)
        {
            var result = new List<string>();
            foreach (string split in splits ?? DatasetModes)
            {
                string normalized = NormalizeSplit(split);
                if (!string.IsNullOrWhiteSpace(normalized)
                    && !result.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                {
                    result.Add(normalized);
                }
            }

            return result.Count > 0 ? result : DatasetModes.ToList();
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

        private static IEnumerable<string> EnumerateImageFiles(string imageDirectory)
        {
            if (string.IsNullOrWhiteSpace(imageDirectory) || !Directory.Exists(imageDirectory))
            {
                yield break;
            }

            foreach (string imagePath in Directory.EnumerateFiles(imageDirectory)
                .Where(path => ImageExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                yield return imagePath;
            }
        }

        private static string FormatRelativePath(string root, string path)
        {
            string relative = Path.GetRelativePath(root, path);
            return relative.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
        }
    }

    public sealed class CocoSegmentationExportResult
    {
        public string OutputPath { get; set; } = string.Empty;

        public int ImageCount { get; set; }

        public int AnnotationCount { get; set; }

        public int CategoryCount { get; set; }

        public int SkippedAnnotationCount { get; set; }

        public List<string> Splits { get; } = new List<string>();
    }

    public sealed class CocoSegmentationDataset
    {
        [JsonProperty("info")]
        public CocoSegmentationInfo Info { get; set; } = new CocoSegmentationInfo();

        [JsonProperty("licenses")]
        public List<object> Licenses { get; } = new List<object>();

        [JsonProperty("images")]
        public List<CocoSegmentationImage> Images { get; } = new List<CocoSegmentationImage>();

        [JsonProperty("annotations")]
        public List<CocoSegmentationAnnotation> Annotations { get; } = new List<CocoSegmentationAnnotation>();

        [JsonProperty("categories")]
        public List<CocoSegmentationCategory> Categories { get; } = new List<CocoSegmentationCategory>();
    }

    public sealed class CocoSegmentationInfo
    {
        [JsonProperty("description")]
        public string Description { get; set; } = "OpenVisionLab Labeling Studio COCO segmentation export";

        [JsonProperty("version")]
        public string Version { get; set; } = "1.0";
    }

    public sealed class CocoSegmentationImage
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("file_name")]
        public string FileName { get; set; } = string.Empty;

        [JsonProperty("width")]
        public int Width { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }
    }

    public sealed class CocoSegmentationAnnotation
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("image_id")]
        public int ImageId { get; set; }

        [JsonProperty("category_id")]
        public int CategoryId { get; set; }

        [JsonProperty("segmentation")]
        public List<double[]> Segmentation { get; set; } = new List<double[]>();

        [JsonProperty("bbox")]
        public double[] BBox { get; set; } = Array.Empty<double>();

        [JsonProperty("area")]
        public double Area { get; set; }

        [JsonProperty("iscrowd")]
        public int IsCrowd { get; set; }
    }

    public sealed class CocoSegmentationCategory
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("supercategory")]
        public string SuperCategory { get; set; } = string.Empty;
    }
}
