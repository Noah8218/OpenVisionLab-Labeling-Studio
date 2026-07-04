using MvcVisionSystem;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Newtonsoft.Json;

namespace MvcVisionSystem.Yolo
{
    public static class CvatSegmentationArchiveExportService
    {
        private static readonly string[] DatasetModes =
        {
            YoloDatasetSplitService.TrainMode,
            YoloDatasetSplitService.ValidMode,
            YoloDatasetSplitService.TestMode
        };

        private static readonly string[] ImageExtensions = { ".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff" };

        public static CvatSegmentationArchiveExportResult ExportDataset(
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
                throw new ArgumentException("CVAT segmentation archive output path is required.", nameof(outputPath));
            }

            data.NormalizeOutputPaths();
            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            CvatSegmentationArchiveExportResult result = BuildAnnotationDocument(data, splits, out XDocument document, out List<CvatSegmentationArchiveImage> images);

            using (FileStream fileStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
            using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Create))
            {
                ZipArchiveEntry annotationEntry = archive.CreateEntry("annotations.xml", CompressionLevel.Optimal);
                using (Stream annotationStream = annotationEntry.Open())
                using (var writer = XmlWriter.Create(annotationStream, new XmlWriterSettings
                {
                    Encoding = new UTF8Encoding(false),
                    Indent = true
                }))
                {
                    document.Save(writer);
                }

                foreach (CvatSegmentationArchiveImage image in images)
                {
                    archive.CreateEntryFromFile(image.SourcePath, $"images/{image.ArchiveName}", CompressionLevel.Optimal);
                    result.ArchiveEntryNames.Add($"images/{image.ArchiveName}");
                }
            }

            result.OutputPath = outputPath;
            result.ArchiveEntryNames.Insert(0, "annotations.xml");
            return result;
        }

        private static CvatSegmentationArchiveExportResult BuildAnnotationDocument(
            CData data,
            IEnumerable<string> splits,
            out XDocument document,
            out List<CvatSegmentationArchiveImage> images)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            data.NormalizeOutputPaths();
            images = new List<CvatSegmentationArchiveImage>();
            var result = new CvatSegmentationArchiveExportResult
            {
                CategoryCount = CountExportableClasses(data)
            };

            IReadOnlyList<string> normalizedSplits = NormalizeSplits(splits);
            result.Splits.AddRange(normalizedSplits);

            foreach (string split in normalizedSplits)
            {
                string imageDirectory = Path.Combine(data.OutputRootPath, "data", split, "images");
                string segmentDirectory = Path.Combine(data.OutputRootPath, "data", split, "segments");
                foreach (string imagePath in EnumerateImageFiles(imageDirectory))
                {
                    using Image image = Image.FromFile(imagePath);
                    var archiveImage = new CvatSegmentationArchiveImage
                    {
                        Id = images.Count,
                        SourcePath = imagePath,
                        ArchiveName = $"{split}/{Path.GetFileName(imagePath)}",
                        Width = image.Width,
                        Height = image.Height
                    };

                    string segmentPath = Path.Combine(segmentDirectory, $"{Path.GetFileNameWithoutExtension(imagePath)}.json");
                    if (File.Exists(segmentPath))
                    {
                        archiveImage.Polygons.AddRange(LoadPolygons(data, segmentPath, image.Size, result));
                    }

                    images.Add(archiveImage);
                    result.ImageCount++;
                    result.PolygonCount += archiveImage.Polygons.Count;
                }
            }

            document = BuildCvatDocument(data, images);
            return result;
        }

        private static XDocument BuildCvatDocument(CData data, IReadOnlyList<CvatSegmentationArchiveImage> images)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            var annotations = new XElement("annotations",
                new XElement("version", "1.1"),
                new XElement("meta",
                    new XElement("task",
                        new XElement("id", 0),
                        new XElement("name", "OpenVisionLab Labeling Studio segmentation export"),
                        new XElement("size", images.Count),
                        new XElement("mode", "annotation"),
                        new XElement("overlap", 0),
                        new XElement("bugtracker", string.Empty),
                        new XElement("flipped", "False"),
                        new XElement("created", FormatTimestamp(now)),
                        new XElement("updated", FormatTimestamp(now)),
                        BuildLabelsElement(data),
                        new XElement("segments",
                            new XElement("segment",
                                new XElement("id", 0),
                                new XElement("start", 0),
                                new XElement("stop", Math.Max(0, images.Count - 1)),
                                new XElement("url", string.Empty))),
                        new XElement("owner",
                            new XElement("username", string.Empty),
                            new XElement("email", string.Empty))),
                    new XElement("dumped", FormatTimestamp(now))));

            foreach (CvatSegmentationArchiveImage image in images)
            {
                var imageElement = new XElement("image",
                    new XAttribute("id", image.Id),
                    new XAttribute("name", image.ArchiveName),
                    new XAttribute("width", image.Width),
                    new XAttribute("height", image.Height));

                foreach (CvatSegmentationPolygon polygon in image.Polygons)
                {
                    imageElement.Add(new XElement("polygon",
                        new XAttribute("label", polygon.Label),
                        new XAttribute("points", FormatPoints(polygon.Points)),
                        new XAttribute("occluded", 0),
                        new XAttribute("z_order", 0)));
                }

                annotations.Add(imageElement);
            }

            return new XDocument(new XDeclaration("1.0", "utf-8", null), annotations);
        }

        private static XElement BuildLabelsElement(CData data)
        {
            var labels = new XElement("labels");
            for (int classIndex = 0; classIndex < (data.ClassNamedList?.Count ?? 0); classIndex++)
            {
                string className = data.ClassNamedList[classIndex]?.Text ?? string.Empty;
                if (string.IsNullOrWhiteSpace(className))
                {
                    continue;
                }

                labels.Add(new XElement("label",
                    new XElement("name", className.Trim()),
                    new XElement("type", "polygon"),
                    new XElement("attributes")));
            }

            return labels;
        }

        private static IReadOnlyList<CvatSegmentationPolygon> LoadPolygons(
            CData data,
            string segmentPath,
            Size imageSize,
            CvatSegmentationArchiveExportResult result)
        {
            var polygons = new List<CvatSegmentationPolygon>();
            foreach (SegmentationPolygonRecord record in LoadPolygonRecords(segmentPath, result))
            {
                if (!TryBuildPolygon(record, data.ClassNamedList, imageSize, out CvatSegmentationPolygon polygon))
                {
                    result.SkippedAnnotationCount++;
                    continue;
                }

                polygons.Add(polygon);
            }

            return polygons;
        }

        private static IEnumerable<SegmentationPolygonRecord> LoadPolygonRecords(
            string segmentPath,
            CvatSegmentationArchiveExportResult result)
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

        private static bool TryBuildPolygon(
            SegmentationPolygonRecord record,
            IReadOnlyList<CClassItem> classes,
            Size imageSize,
            out CvatSegmentationPolygon polygon)
        {
            polygon = null;
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

            polygon = new CvatSegmentationPolygon
            {
                Label = classes[classIndex].Text.Trim(),
                Points = points
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

        private static string FormatPoints(IEnumerable<Point> points)
            => string.Join(";", (points ?? Enumerable.Empty<Point>())
                .Select(point => $"{FormatCoordinate(point.X)},{FormatCoordinate(point.Y)}"));

        private static string FormatCoordinate(int coordinate)
            => coordinate.ToString("0.00", CultureInfo.InvariantCulture);

        private static string FormatTimestamp(DateTimeOffset value)
            => value.ToString("yyyy-MM-dd HH:mm:ss.ffffffK", CultureInfo.InvariantCulture);

        private static int CountExportableClasses(CData data)
            => data.ClassNamedList?
                .Count(item => !string.IsNullOrWhiteSpace(item?.Text)) ?? 0;

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
    }

    public sealed class CvatSegmentationArchiveExportResult
    {
        public string OutputPath { get; set; } = string.Empty;

        public int ImageCount { get; set; }

        public int PolygonCount { get; set; }

        public int CategoryCount { get; set; }

        public int SkippedAnnotationCount { get; set; }

        public List<string> Splits { get; } = new List<string>();

        public List<string> ArchiveEntryNames { get; } = new List<string>();
    }

    internal sealed class CvatSegmentationArchiveImage
    {
        public int Id { get; set; }

        public string SourcePath { get; set; } = string.Empty;

        public string ArchiveName { get; set; } = string.Empty;

        public int Width { get; set; }

        public int Height { get; set; }

        public List<CvatSegmentationPolygon> Polygons { get; } = new List<CvatSegmentationPolygon>();
    }

    internal sealed class CvatSegmentationPolygon
    {
        public string Label { get; set; } = string.Empty;

        public List<Point> Points { get; set; } = new List<Point>();
    }
}
