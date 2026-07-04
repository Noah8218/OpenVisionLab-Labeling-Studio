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

namespace MvcVisionSystem.Yolo
{
    public static class CvatImageTaskArchiveExportService
    {
        private static readonly string[] DatasetModes =
        {
            YoloDatasetSplitService.TrainMode,
            YoloDatasetSplitService.ValidMode,
            YoloDatasetSplitService.TestMode
        };

        private static readonly string[] ImageExtensions = { ".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff" };

        public static CvatImageTaskArchiveExportResult ExportDataset(
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
                throw new ArgumentException("CVAT image task archive output path is required.", nameof(outputPath));
            }

            data.NormalizeOutputPaths();
            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            CvatImageTaskArchiveExportResult result = BuildAnnotationDocument(data, splits, out XDocument document, out List<CvatImageArchiveImage> images);

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

                foreach (CvatImageArchiveImage image in images)
                {
                    archive.CreateEntryFromFile(image.SourcePath, $"images/{image.ArchiveName}", CompressionLevel.Optimal);
                    result.ArchiveEntryNames.Add($"images/{image.ArchiveName}");
                }
            }

            result.OutputPath = outputPath;
            result.ArchiveEntryNames.Insert(0, "annotations.xml");
            return result;
        }

        private static CvatImageTaskArchiveExportResult BuildAnnotationDocument(
            CData data,
            IEnumerable<string> splits,
            out XDocument document,
            out List<CvatImageArchiveImage> images)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            data.NormalizeOutputPaths();
            images = new List<CvatImageArchiveImage>();
            var result = new CvatImageTaskArchiveExportResult
            {
                CategoryCount = CountExportableClasses(data)
            };

            IReadOnlyList<string> normalizedSplits = NormalizeSplits(splits);
            result.Splits.AddRange(normalizedSplits);

            foreach (string split in normalizedSplits)
            {
                string imageDirectory = Path.Combine(data.OutputRootPath, "data", split, "images");
                string labelDirectory = Path.Combine(data.OutputRootPath, "data", split, "labels");
                foreach (string imagePath in EnumerateImageFiles(imageDirectory))
                {
                    using Image image = Image.FromFile(imagePath);
                    var archiveImage = new CvatImageArchiveImage
                    {
                        Id = images.Count,
                        SourcePath = imagePath,
                        ArchiveName = $"{split}/{Path.GetFileName(imagePath)}",
                        Width = image.Width,
                        Height = image.Height
                    };

                    string labelPath = Path.Combine(labelDirectory, $"{Path.GetFileNameWithoutExtension(imagePath)}.txt");
                    if (File.Exists(labelPath))
                    {
                        archiveImage.Boxes.AddRange(LoadBoxes(data, labelPath, image.Size, result));
                    }

                    images.Add(archiveImage);
                    result.ImageCount++;
                    result.BoxCount += archiveImage.Boxes.Count;
                }
            }

            document = BuildCvatDocument(data, images);
            return result;
        }

        private static XDocument BuildCvatDocument(CData data, IReadOnlyList<CvatImageArchiveImage> images)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            var annotations = new XElement("annotations",
                new XElement("version", "1.1"),
                new XElement("meta",
                    new XElement("task",
                        new XElement("id", 0),
                        new XElement("name", "OpenVisionLab Labeling Studio export"),
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

            foreach (CvatImageArchiveImage image in images)
            {
                var imageElement = new XElement("image",
                    new XAttribute("id", image.Id),
                    new XAttribute("name", image.ArchiveName),
                    new XAttribute("width", image.Width),
                    new XAttribute("height", image.Height));

                foreach (CvatImageBox box in image.Boxes)
                {
                    imageElement.Add(new XElement("box",
                        new XAttribute("label", box.Label),
                        new XAttribute("xtl", FormatCoordinate(box.Xtl)),
                        new XAttribute("ytl", FormatCoordinate(box.Ytl)),
                        new XAttribute("xbr", FormatCoordinate(box.Xbr)),
                        new XAttribute("ybr", FormatCoordinate(box.Ybr)),
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
                    new XElement("type", "bbox"),
                    new XElement("attributes")));
            }

            return labels;
        }

        private static IReadOnlyList<CvatImageBox> LoadBoxes(
            CData data,
            string labelPath,
            Size imageSize,
            CvatImageTaskArchiveExportResult result)
        {
            var boxes = new List<CvatImageBox>();
            foreach (string line in File.ReadLines(labelPath))
            {
                if (!YoloAnnotationService.TryParseYoloLine(line, imageSize, out int classIndex, out Rectangle bounds)
                    || classIndex < 0
                    || classIndex >= (data.ClassNamedList?.Count ?? 0)
                    || string.IsNullOrWhiteSpace(data.ClassNamedList[classIndex]?.Text))
                {
                    result.SkippedAnnotationCount++;
                    continue;
                }

                boxes.Add(new CvatImageBox
                {
                    Label = data.ClassNamedList[classIndex].Text.Trim(),
                    Xtl = bounds.Left,
                    Ytl = bounds.Top,
                    Xbr = bounds.Right,
                    Ybr = bounds.Bottom
                });
            }

            return boxes;
        }

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

    public sealed class CvatImageTaskArchiveExportResult
    {
        public string OutputPath { get; set; } = string.Empty;

        public int ImageCount { get; set; }

        public int BoxCount { get; set; }

        public int CategoryCount { get; set; }

        public int SkippedAnnotationCount { get; set; }

        public List<string> Splits { get; } = new List<string>();

        public List<string> ArchiveEntryNames { get; } = new List<string>();
    }

    internal sealed class CvatImageArchiveImage
    {
        public int Id { get; set; }

        public string SourcePath { get; set; } = string.Empty;

        public string ArchiveName { get; set; } = string.Empty;

        public int Width { get; set; }

        public int Height { get; set; }

        public List<CvatImageBox> Boxes { get; } = new List<CvatImageBox>();
    }

    internal sealed class CvatImageBox
    {
        public string Label { get; set; } = string.Empty;

        public int Xtl { get; set; }

        public int Ytl { get; set; }

        public int Xbr { get; set; }

        public int Ybr { get; set; }
    }
}
