using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MvcVisionSystem.Yolo
{
    // Imports a YOLO detection dataset (data.yaml + per-image label .txt files) into the recipe's YOLO layout.
    // Class indexes are remapped by name through the recipe class catalog, so a dataset whose names are in a
    // different order keeps its meaning. Runs inside DatasetInterchangePreflightService, which stages the output
    // and verifies that the source stays unchanged.
    public static class YoloDetectionImportService
    {
        private static readonly HashSet<string> ImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff"
        };

        public static YoloDetectionImportResult ImportDataset(
            LabelingProjectData data,
            string dataYamlPath,
            string imageRoot,
            string targetSplit = YoloDatasetSplitService.TrainMode)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (string.IsNullOrWhiteSpace(dataYamlPath) || !File.Exists(dataYamlPath))
                throw new FileNotFoundException("YOLO data.yaml was not found.", dataYamlPath);
            string split = YoloDatasetSplitService.NormalizeStandardSplit(targetSplit);
            if (string.IsNullOrWhiteSpace(split))
                throw new ArgumentException("Target split must be train, valid, or test.", nameof(targetSplit));
            if (string.IsNullOrWhiteSpace(imageRoot) || !Directory.Exists(imageRoot))
                throw new DirectoryNotFoundException("The source image folder was not found.");

            var errors = new List<string>();
            List<string> names = YoloExternalDatasetIntakeService.ReadDataYamlClassNames(dataYamlPath, errors);
            if (errors.Count > 0 || names.Count == 0)
                throw new InvalidDataException(errors.FirstOrDefault() ?? "YOLO data.yaml has no class names.");

            data.NormalizeOutputPaths();
            data.EnsureYoloOutputDirectories();
            var result = new YoloDetectionImportResult { DataYamlPath = dataYamlPath, ImageRoot = imageRoot, TargetSplit = split };
            int[] classMap = names.Select(name => ClassCatalogService.FindOrAddClass(data, ClassCatalogService.NormalizeClassName(name))).ToArray();
            result.CategoryCount = classMap.Count(index => index >= 0);

            // Label files are found by image file name under any "labels" folder next to data.yaml
            // (labels/train, train/labels, ...). A name that appears twice is ambiguous and skipped.
            string datasetRoot = Path.GetDirectoryName(Path.GetFullPath(dataYamlPath)) ?? string.Empty;
            var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var ambiguous = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string labelPath in Directory.EnumerateFiles(datasetRoot, "*.txt", SearchOption.AllDirectories)
                .Where(path => path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Any(part => string.Equals(part, "labels", StringComparison.OrdinalIgnoreCase))))
            {
                string stem = Path.GetFileNameWithoutExtension(labelPath);
                if (!labels.TryAdd(stem, labelPath)) ambiguous.Add(stem);
            }

            string imageDirectory = Path.Combine(data.OutputRootPath, "data", split, "images");
            string labelDirectory = Path.Combine(data.OutputRootPath, "data", split, "labels");
            Directory.CreateDirectory(imageDirectory);
            Directory.CreateDirectory(labelDirectory);
            var seenImages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string sourcePath in Directory.EnumerateFiles(imageRoot, "*.*", SearchOption.AllDirectories)
                .Where(path => ImageExtensions.Contains(Path.GetExtension(path)))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                string stem = Path.GetFileNameWithoutExtension(sourcePath);
                if (!seenImages.Add(stem) || ambiguous.Contains(stem))
                {
                    result.SkippedImageCount++;
                    continue;
                }

                // An image without a label file is a background (no-object) image in YOLO.
                List<string> lines = labels.TryGetValue(stem, out string labelPath)
                    ? BuildLabelLines(File.ReadAllLines(labelPath), classMap, ReadImageSize(sourcePath), result)
                    : new List<string>();
                if (labelPath == null) result.BackgroundImageCount++;

                string targetImagePath = Path.Combine(imageDirectory, Path.GetFileName(sourcePath));
                File.Copy(sourcePath, targetImagePath, overwrite: true);
                File.WriteAllLines(Path.Combine(labelDirectory, stem + ".txt"), lines);
                result.ImportedImageCount++;
                result.ImportedAnnotationCount += lines.Count;
                result.ImportedImagePaths.Add(targetImagePath);
            }

            int unmatched = labels.Keys.Count(stem => !seenImages.Contains(stem));
            if (unmatched > 0)
                result.Warnings.Add(string.Format(CultureInfo.InvariantCulture,
                    "YoloLabelsWithoutImage: Skipped - 원본 이미지 폴더에 없는 라벨 파일은 가져오지 않았습니다. (영향 {0}건)", unmatched));
            data.SaveYoloDataYaml();
            return result;
        }

        private static List<string> BuildLabelLines(IEnumerable<string> sourceLines, IReadOnlyList<int> classMap, Size imageSize, YoloDetectionImportResult result)
        {
            var lines = new List<string>();
            foreach (string raw in sourceLines)
            {
                string[] parts = (raw ?? string.Empty).Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) continue;
                // Exactly class cx cy w h; polygon (segmentation) rows and malformed rows are skipped and counted.
                if (parts.Length != 5
                    || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int sourceClass)
                    || sourceClass < 0 || sourceClass >= classMap.Count || classMap[sourceClass] < 0
                    || !TryParseUnit(parts[1], out double cx) || !TryParseUnit(parts[2], out double cy)
                    || !TryParseUnit(parts[3], out double w) || !TryParseUnit(parts[4], out double h)
                    || w <= 0 || h <= 0)
                {
                    result.SkippedAnnotationCount++;
                    continue;
                }

                Rectangle bounds = Rectangle.Intersect(
                    Rectangle.FromLTRB(
                        (int)Math.Round((cx - w / 2) * imageSize.Width), (int)Math.Round((cy - h / 2) * imageSize.Height),
                        (int)Math.Round((cx + w / 2) * imageSize.Width), (int)Math.Round((cy + h / 2) * imageSize.Height)),
                    new Rectangle(Point.Empty, imageSize));
                string line = bounds.IsEmpty ? null : YoloAnnotationService.TryCreateYoloLine(classMap[sourceClass], bounds, imageSize);
                if (string.IsNullOrWhiteSpace(line))
                {
                    result.SkippedAnnotationCount++;
                    continue;
                }

                lines.Add(line);
            }

            return lines;
        }

        private static bool TryParseUnit(string text, out double value)
            => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value >= 0 && value <= 1;

        private static Size ReadImageSize(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using Image image = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);
            return image.Size;
        }
    }

    public sealed class YoloDetectionImportResult
    {
        public string DataYamlPath { get; set; } = string.Empty;
        public string ImageRoot { get; set; } = string.Empty;
        public string TargetSplit { get; set; } = string.Empty;
        public int ImportedImageCount { get; set; }
        public int ImportedAnnotationCount { get; set; }
        public int BackgroundImageCount { get; set; }
        public int CategoryCount { get; set; }
        public int SkippedImageCount { get; set; }
        public int SkippedAnnotationCount { get; set; }
        public List<string> ImportedImagePaths { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
    }
}
