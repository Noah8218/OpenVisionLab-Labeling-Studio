using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace MvcVisionSystem.Yolo
{
    public static class CvatDetectionImportService
    {
        private static readonly string[] DatasetModes =
        {
            YoloDatasetSplitService.TrainMode,
            YoloDatasetSplitService.ValidMode,
            YoloDatasetSplitService.TestMode
        };

        public static CvatDetectionImportResult ImportArchive(
            CData data,
            string archivePath,
            string targetSplit = YoloDatasetSplitService.TrainMode)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (string.IsNullOrWhiteSpace(archivePath))
            {
                throw new ArgumentException("CVAT archive path is required.", nameof(archivePath));
            }

            if (!File.Exists(archivePath))
            {
                throw new FileNotFoundException("CVAT archive was not found.", archivePath);
            }

            string split = NormalizeSplit(targetSplit);
            if (string.IsNullOrWhiteSpace(split))
            {
                throw new ArgumentException("Target split must be train, valid, or test.", nameof(targetSplit));
            }

            data.NormalizeOutputPaths();
            data.EnsureYoloOutputDirectories();

            var result = new CvatDetectionImportResult
            {
                ArchivePath = archivePath,
                TargetSplit = split
            };

            string imageDirectory = Path.Combine(data.OutputRootPath, "data", split, "images");
            string labelDirectory = Path.Combine(data.OutputRootPath, "data", split, "labels");
            Directory.CreateDirectory(imageDirectory);
            Directory.CreateDirectory(labelDirectory);

            using (ZipArchive archive = ZipFile.OpenRead(archivePath))
            {
                ZipArchiveEntry annotationEntry = archive.GetEntry("annotations.xml");
                if (annotationEntry == null)
                {
                    throw new InvalidDataException("CVAT archive does not contain annotations.xml.");
                }

                XDocument document;
                using (Stream annotationStream = annotationEntry.Open())
                {
                    document = XDocument.Load(annotationStream);
                }

                foreach (XElement imageElement in document.Root?.Elements("image") ?? Enumerable.Empty<XElement>())
                {
                    if (!TryImportImage(data, archive, imageElement, imageDirectory, labelDirectory, result))
                    {
                        result.SkippedImageCount++;
                    }
                }
            }

            result.CategoryCount = data.ClassNamedList?.Count(item => !string.IsNullOrWhiteSpace(item?.Text)) ?? 0;
            data.SaveYoloDataYaml();
            return result;
        }

        private static bool TryImportImage(
            CData data,
            ZipArchive archive,
            XElement imageElement,
            string imageDirectory,
            string labelDirectory,
            CvatDetectionImportResult result)
        {
            string archiveName = imageElement?.Attribute("name")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(archiveName)
                || !TryReadInt(imageElement, "width", out int width)
                || !TryReadInt(imageElement, "height", out int height)
                || width <= 0
                || height <= 0)
            {
                return false;
            }

            ZipArchiveEntry imageEntry = archive.GetEntry($"images/{archiveName.Replace('\\', '/')}");
            if (imageEntry == null)
            {
                return false;
            }

            string fileName = Path.GetFileName(archiveName.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            string targetImagePath = Path.Combine(imageDirectory, fileName);
            imageEntry.ExtractToFile(targetImagePath, overwrite: true);

            var imageSize = new Size(width, height);
            var lines = new List<string>();
            foreach (XElement boxElement in imageElement.Elements("box"))
            {
                if (!TryBuildLabelLine(data, boxElement, imageSize, out string line))
                {
                    result.SkippedBoxCount++;
                    continue;
                }

                lines.Add(line);
            }

            string labelPath = Path.Combine(labelDirectory, $"{Path.GetFileNameWithoutExtension(fileName)}.txt");
            File.WriteAllLines(labelPath, lines);

            result.ImportedImageCount++;
            result.LabelFileCount++;
            result.ImportedBoxCount += lines.Count;
            result.ImportedImagePaths.Add(targetImagePath);
            return true;
        }

        private static bool TryBuildLabelLine(CData data, XElement boxElement, Size imageSize, out string line)
        {
            line = string.Empty;
            string className = ClassCatalogService.NormalizeClassName(boxElement?.Attribute("label")?.Value);
            if (string.IsNullOrWhiteSpace(className)
                || !TryReadDouble(boxElement, "xtl", out double xtl)
                || !TryReadDouble(boxElement, "ytl", out double ytl)
                || !TryReadDouble(boxElement, "xbr", out double xbr)
                || !TryReadDouble(boxElement, "ybr", out double ybr))
            {
                return false;
            }

            int classIndex = FindOrAddClass(data, className);
            Rectangle rectangle = Rectangle.Intersect(
                Rectangle.FromLTRB(
                    (int)Math.Round(xtl),
                    (int)Math.Round(ytl),
                    (int)Math.Round(xbr),
                    (int)Math.Round(ybr)),
                new Rectangle(Point.Empty, imageSize));
            if (classIndex < 0 || rectangle.IsEmpty)
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

        private static bool TryReadInt(XElement element, string attributeName, out int value)
        {
            value = 0;
            return int.TryParse(element?.Attribute(attributeName)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static bool TryReadDouble(XElement element, string attributeName, out double value)
        {
            value = 0;
            return double.TryParse(element?.Attribute(attributeName)?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
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

    public sealed class CvatDetectionImportResult
    {
        public string ArchivePath { get; set; } = string.Empty;

        public string TargetSplit { get; set; } = string.Empty;

        public int ImportedImageCount { get; set; }

        public int LabelFileCount { get; set; }

        public int ImportedBoxCount { get; set; }

        public int CategoryCount { get; set; }

        public int SkippedImageCount { get; set; }

        public int SkippedBoxCount { get; set; }

        public List<string> ImportedImagePaths { get; } = new List<string>();
    }
}
