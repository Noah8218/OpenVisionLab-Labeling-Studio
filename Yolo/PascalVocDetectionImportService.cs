using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace MvcVisionSystem.Yolo
{
    public static class PascalVocDetectionImportService
    {
        private static readonly string[] DatasetModes =
        {
            YoloDatasetSplitService.TrainMode,
            YoloDatasetSplitService.ValidMode,
            YoloDatasetSplitService.TestMode
        };

        public static PascalVocDetectionImportResult ImportDirectory(
            CData data,
            string annotationDirectory,
            string imageRoot,
            string targetSplit = YoloDatasetSplitService.TrainMode)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (string.IsNullOrWhiteSpace(annotationDirectory))
            {
                throw new ArgumentException("Pascal VOC annotation directory is required.", nameof(annotationDirectory));
            }

            if (!Directory.Exists(annotationDirectory))
            {
                throw new DirectoryNotFoundException($"Pascal VOC annotation directory was not found: {annotationDirectory}");
            }

            string split = NormalizeSplit(targetSplit);
            if (string.IsNullOrWhiteSpace(split))
            {
                throw new ArgumentException("Target split must be train, valid, or test.", nameof(targetSplit));
            }

            data.NormalizeOutputPaths();
            data.EnsureYoloOutputDirectories();

            var result = new PascalVocDetectionImportResult
            {
                AnnotationDirectory = Path.GetFullPath(annotationDirectory),
                ImageRoot = string.IsNullOrWhiteSpace(imageRoot) ? Path.GetFullPath(annotationDirectory) : Path.GetFullPath(imageRoot),
                TargetSplit = split
            };

            string imageDirectory = Path.Combine(data.OutputRootPath, "data", split, "images");
            string labelDirectory = Path.Combine(data.OutputRootPath, "data", split, "labels");
            Directory.CreateDirectory(imageDirectory);
            Directory.CreateDirectory(labelDirectory);

            foreach (string xmlPath in Directory.EnumerateFiles(annotationDirectory, "*.xml", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                if (!TryImportXml(data, xmlPath, imageDirectory, labelDirectory, result))
                {
                    result.SkippedXmlCount++;
                }
            }

            result.CategoryCount = data.ClassNamedList?.Count(item => !string.IsNullOrWhiteSpace(item?.Text)) ?? 0;
            data.SaveYoloDataYaml();
            return result;
        }

        private static bool TryImportXml(
            CData data,
            string xmlPath,
            string imageDirectory,
            string labelDirectory,
            PascalVocDetectionImportResult result)
        {
            XDocument document;
            try
            {
                document = XDocument.Load(xmlPath);
            }
            catch
            {
                return false;
            }

            XElement root = document.Root;
            string fileName = root?.Element("filename")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = $"{Path.GetFileNameWithoutExtension(xmlPath)}.jpg";
            }

            string sourceImagePath = ResolveSourceImagePath(root, result.ImageRoot, fileName);
            if (!File.Exists(sourceImagePath))
            {
                return false;
            }

            Size imageSize = ResolveImageSize(root, sourceImagePath);
            if (imageSize.Width <= 0 || imageSize.Height <= 0)
            {
                return false;
            }

            string targetImagePath = Path.Combine(imageDirectory, Path.GetFileName(fileName));
            File.Copy(sourceImagePath, targetImagePath, overwrite: true);

            var lines = new List<string>();
            foreach (XElement objectElement in root?.Elements("object") ?? Enumerable.Empty<XElement>())
            {
                if (!TryBuildLabelLine(data, objectElement, imageSize, out string line))
                {
                    result.SkippedObjectCount++;
                    continue;
                }

                lines.Add(line);
            }

            string labelPath = Path.Combine(labelDirectory, $"{Path.GetFileNameWithoutExtension(fileName)}.txt");
            File.WriteAllLines(labelPath, lines);

            result.ImportedImageCount++;
            result.LabelFileCount++;
            result.ImportedObjectCount += lines.Count;
            result.ImportedImagePaths.Add(targetImagePath);
            return true;
        }

        private static bool TryBuildLabelLine(CData data, XElement objectElement, Size imageSize, out string line)
        {
            line = string.Empty;
            string className = ClassCatalogService.NormalizeClassName(objectElement?.Element("name")?.Value);
            XElement bounds = objectElement?.Element("bndbox");
            if (string.IsNullOrWhiteSpace(className)
                || bounds == null
                || !TryReadInt(bounds, "xmin", out int xMin)
                || !TryReadInt(bounds, "ymin", out int yMin)
                || !TryReadInt(bounds, "xmax", out int xMax)
                || !TryReadInt(bounds, "ymax", out int yMax))
            {
                return false;
            }

            int classIndex = FindOrAddClass(data, className);
            if (classIndex < 0)
            {
                return false;
            }

            Rectangle rectangle = Rectangle.Intersect(
                Rectangle.FromLTRB(xMin - 1, yMin - 1, xMax, yMax),
                new Rectangle(Point.Empty, imageSize));
            if (rectangle.IsEmpty)
            {
                return false;
            }

            line = YoloAnnotationService.TryCreateYoloLine(classIndex, rectangle, imageSize);
            return !string.IsNullOrWhiteSpace(line);
        }

        private static int FindOrAddClass(CData data, string className)
        {
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

        private static bool TryReadInt(XElement parent, string name, out int value)
        {
            value = 0;
            return parent != null
                && int.TryParse(parent.Element(name)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static Size ResolveImageSize(XElement root, string sourceImagePath)
        {
            XElement size = root?.Element("size");
            if (TryReadInt(size, "width", out int width)
                && TryReadInt(size, "height", out int height)
                && width > 0
                && height > 0)
            {
                return new Size(width, height);
            }

            using Image image = Image.FromFile(sourceImagePath);
            return image.Size;
        }

        private static string ResolveSourceImagePath(XElement root, string imageRoot, string fileName)
        {
            string pathValue = root?.Element("path")?.Value?.Trim();
            if (!string.IsNullOrWhiteSpace(pathValue) && Path.IsPathRooted(pathValue) && File.Exists(pathValue))
            {
                return pathValue;
            }

            return Path.Combine(imageRoot ?? string.Empty, fileName);
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

    public sealed class PascalVocDetectionImportResult
    {
        public string AnnotationDirectory { get; set; } = string.Empty;

        public string ImageRoot { get; set; } = string.Empty;

        public string TargetSplit { get; set; } = string.Empty;

        public int ImportedImageCount { get; set; }

        public int LabelFileCount { get; set; }

        public int ImportedObjectCount { get; set; }

        public int CategoryCount { get; set; }

        public int SkippedXmlCount { get; set; }

        public int SkippedObjectCount { get; set; }

        public List<string> ImportedImagePaths { get; } = new List<string>();
    }
}
