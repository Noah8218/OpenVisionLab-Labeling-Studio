using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace MvcVisionSystem.Yolo
{
    public static class PascalVocDetectionExportService
    {
        private static readonly string[] DatasetModes =
        {
            YoloDatasetSplitService.TrainMode,
            YoloDatasetSplitService.ValidMode,
            YoloDatasetSplitService.TestMode
        };

        private static readonly string[] ImageExtensions = { ".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff" };

        public static PascalVocDetectionExportResult ExportDataset(
            CData data,
            string outputDirectory,
            IEnumerable<string> splits = null)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new ArgumentException("Pascal VOC export output directory is required.", nameof(outputDirectory));
            }

            data.NormalizeOutputPaths();
            Directory.CreateDirectory(outputDirectory);

            var result = new PascalVocDetectionExportResult
            {
                OutputDirectory = outputDirectory,
                CategoryCount = CountExportableClasses(data)
            };

            IReadOnlyList<string> normalizedSplits = NormalizeSplits(splits);
            result.Splits.AddRange(normalizedSplits);

            foreach (string split in normalizedSplits)
            {
                string imageDirectory = Path.Combine(data.OutputRootPath, "data", split, "images");
                string labelDirectory = Path.Combine(data.OutputRootPath, "data", split, "labels");
                string splitOutputDirectory = Path.Combine(outputDirectory, split);
                bool splitDirectoryCreated = false;

                foreach (string imagePath in EnumerateImageFiles(imageDirectory))
                {
                    using Image image = Image.FromFile(imagePath);
                    IReadOnlyList<PascalVocDetectionObject> objects = LoadObjects(
                        data,
                        labelDirectory,
                        imagePath,
                        image.Size,
                        result);

                    if (!splitDirectoryCreated)
                    {
                        Directory.CreateDirectory(splitOutputDirectory);
                        splitDirectoryCreated = true;
                    }

                    string outputPath = Path.Combine(splitOutputDirectory, $"{Path.GetFileNameWithoutExtension(imagePath)}.xml");
                    XDocument document = BuildAnnotationDocument(data.OutputRootPath, split, imagePath, image.Size, objects);
                    File.WriteAllText(outputPath, document.ToString(), new UTF8Encoding(false));

                    result.ImageCount++;
                    result.XmlFileCount++;
                    result.ObjectCount += objects.Count;
                    result.OutputPaths.Add(outputPath);
                }
            }

            return result;
        }

        private static XDocument BuildAnnotationDocument(
            string root,
            string split,
            string imagePath,
            Size imageSize,
            IReadOnlyList<PascalVocDetectionObject> objects)
        {
            var annotation = new XElement("annotation",
                new XElement("folder", split),
                new XElement("filename", Path.GetFileName(imagePath)),
                new XElement("path", FormatRelativePath(root, imagePath)),
                new XElement("source",
                    new XElement("database", "OpenVisionLab Labeling Studio")),
                new XElement("size",
                    new XElement("width", imageSize.Width),
                    new XElement("height", imageSize.Height),
                    new XElement("depth", 3)),
                new XElement("segmented", 0));

            foreach (PascalVocDetectionObject item in objects)
            {
                annotation.Add(new XElement("object",
                    new XElement("name", item.ClassName),
                    new XElement("pose", "Unspecified"),
                    new XElement("truncated", 0),
                    new XElement("difficult", 0),
                    new XElement("bndbox",
                        new XElement("xmin", item.XMin),
                        new XElement("ymin", item.YMin),
                        new XElement("xmax", item.XMax),
                        new XElement("ymax", item.YMax))));
            }

            return new XDocument(annotation);
        }

        private static IReadOnlyList<PascalVocDetectionObject> LoadObjects(
            CData data,
            string labelDirectory,
            string imagePath,
            Size imageSize,
            PascalVocDetectionExportResult result)
        {
            var objects = new List<PascalVocDetectionObject>();
            string labelPath = Path.Combine(labelDirectory, $"{Path.GetFileNameWithoutExtension(imagePath)}.txt");
            if (!File.Exists(labelPath))
            {
                return objects;
            }

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

                objects.Add(new PascalVocDetectionObject
                {
                    ClassName = data.ClassNamedList[classIndex].Text.Trim(),
                    XMin = Math.Max(1, bounds.Left + 1),
                    YMin = Math.Max(1, bounds.Top + 1),
                    XMax = Math.Min(imageSize.Width, bounds.Right),
                    YMax = Math.Min(imageSize.Height, bounds.Bottom)
                });
            }

            return objects;
        }

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

        private static string FormatRelativePath(string root, string path)
        {
            string relative = Path.GetRelativePath(root, path);
            return relative.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
        }
    }

    public sealed class PascalVocDetectionExportResult
    {
        public string OutputDirectory { get; set; } = string.Empty;

        public int ImageCount { get; set; }

        public int XmlFileCount { get; set; }

        public int ObjectCount { get; set; }

        public int CategoryCount { get; set; }

        public int SkippedAnnotationCount { get; set; }

        public List<string> Splits { get; } = new List<string>();

        public List<string> OutputPaths { get; } = new List<string>();
    }

    internal sealed class PascalVocDetectionObject
    {
        public string ClassName { get; set; } = string.Empty;

        public int XMin { get; set; }

        public int YMin { get; set; }

        public int XMax { get; set; }

        public int YMax { get; set; }
    }
}
