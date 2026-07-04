using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace MvcVisionSystem.Yolo
{
    public static class LabelStudioDetectionImportService
    {
        private static readonly string[] DatasetModes =
        {
            YoloDatasetSplitService.TrainMode,
            YoloDatasetSplitService.ValidMode,
            YoloDatasetSplitService.TestMode
        };

        public static LabelStudioDetectionImportResult ImportTasks(
            CData data,
            string taskJsonPath,
            string imageRoot,
            string targetSplit = YoloDatasetSplitService.TrainMode)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (string.IsNullOrWhiteSpace(taskJsonPath))
            {
                throw new ArgumentException("Label Studio task JSON path is required.", nameof(taskJsonPath));
            }

            if (!File.Exists(taskJsonPath))
            {
                throw new FileNotFoundException("Label Studio task JSON was not found.", taskJsonPath);
            }

            string split = NormalizeSplit(targetSplit);
            if (string.IsNullOrWhiteSpace(split))
            {
                throw new ArgumentException("Target split must be train, valid, or test.", nameof(targetSplit));
            }

            List<LabelStudioDetectionTask> tasks = JsonConvert.DeserializeObject<List<LabelStudioDetectionTask>>(File.ReadAllText(taskJsonPath))
                ?? new List<LabelStudioDetectionTask>();

            data.NormalizeOutputPaths();
            data.EnsureYoloOutputDirectories();

            var result = new LabelStudioDetectionImportResult
            {
                TaskJsonPath = taskJsonPath,
                ImageRoot = ResolveImageRoot(taskJsonPath, imageRoot),
                TargetSplit = split
            };

            string imageDirectory = Path.Combine(data.OutputRootPath, "data", split, "images");
            string labelDirectory = Path.Combine(data.OutputRootPath, "data", split, "labels");
            Directory.CreateDirectory(imageDirectory);
            Directory.CreateDirectory(labelDirectory);

            foreach (LabelStudioDetectionTask task in tasks)
            {
                if (!TryImportTask(data, task, imageDirectory, labelDirectory, result))
                {
                    result.SkippedTaskCount++;
                }
            }

            result.CategoryCount = data.ClassNamedList?.Count(item => !string.IsNullOrWhiteSpace(item?.Text)) ?? 0;
            data.SaveYoloDataYaml();
            return result;
        }

        private static bool TryImportTask(
            CData data,
            LabelStudioDetectionTask task,
            string imageDirectory,
            string labelDirectory,
            LabelStudioDetectionImportResult result)
        {
            string imageValue = task?.Data?.Image ?? string.Empty;
            if (string.IsNullOrWhiteSpace(imageValue))
            {
                return false;
            }

            string sourcePath = ResolveSourceImagePath(result.ImageRoot, imageValue);
            if (!File.Exists(sourcePath))
            {
                return false;
            }

            string fileName = Path.GetFileName(imageValue.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return false;
            }

            string targetImagePath = Path.Combine(imageDirectory, fileName);
            File.Copy(sourcePath, targetImagePath, overwrite: true);

            Size fallbackImageSize = ResolveImageSize(sourcePath);
            List<string> labelLines = BuildLabelLines(data, task, fallbackImageSize, result);
            string labelPath = Path.Combine(labelDirectory, $"{Path.GetFileNameWithoutExtension(fileName)}.txt");
            File.WriteAllLines(labelPath, labelLines);

            result.ImportedTaskCount++;
            result.LabelFileCount++;
            result.ImportedResultCount += labelLines.Count;
            result.ImportedImagePaths.Add(targetImagePath);
            return true;
        }

        private static List<string> BuildLabelLines(
            CData data,
            LabelStudioDetectionTask task,
            Size fallbackImageSize,
            LabelStudioDetectionImportResult result)
        {
            var lines = new List<string>();
            IEnumerable<LabelStudioDetectionResult> results = (task?.Annotations ?? new List<LabelStudioDetectionAnnotation>())
                .SelectMany(annotation => annotation?.Result ?? new List<LabelStudioDetectionResult>());

            foreach (LabelStudioDetectionResult item in results)
            {
                if (!TryBuildLabelLine(data, item, fallbackImageSize, out string line))
                {
                    result.SkippedResultCount++;
                    continue;
                }

                lines.Add(line);
            }

            return lines;
        }

        private static bool TryBuildLabelLine(
            CData data,
            LabelStudioDetectionResult item,
            Size fallbackImageSize,
            out string line)
        {
            line = string.Empty;
            if (item == null
                || !string.Equals(item.Type, "rectanglelabels", StringComparison.OrdinalIgnoreCase)
                || item.Value?.RectangleLabels == null
                || item.Value.RectangleLabels.Length == 0)
            {
                return false;
            }

            string className = ClassCatalogService.NormalizeClassName(item.Value.RectangleLabels[0]);
            int classIndex = FindOrAddClass(data, className);
            Size imageSize = item.OriginalWidth > 0 && item.OriginalHeight > 0
                ? new Size(item.OriginalWidth, item.OriginalHeight)
                : fallbackImageSize;
            if (classIndex < 0 || imageSize.Width <= 0 || imageSize.Height <= 0)
            {
                return false;
            }

            int left = (int)Math.Round(item.Value.X / 100D * imageSize.Width);
            int top = (int)Math.Round(item.Value.Y / 100D * imageSize.Height);
            int width = (int)Math.Round(item.Value.Width / 100D * imageSize.Width);
            int height = (int)Math.Round(item.Value.Height / 100D * imageSize.Height);
            Rectangle rectangle = Rectangle.Intersect(
                new Rectangle(left, top, width, height),
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
            if (string.IsNullOrWhiteSpace(className))
            {
                return -1;
            }

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

        private static Size ResolveImageSize(string sourcePath)
        {
            using Image image = Image.FromFile(sourcePath);
            return image.Size;
        }

        private static string ResolveImageRoot(string taskJsonPath, string imageRoot)
            => string.IsNullOrWhiteSpace(imageRoot)
                ? Path.GetDirectoryName(Path.GetFullPath(taskJsonPath)) ?? string.Empty
                : Path.GetFullPath(imageRoot);

        private static string ResolveSourceImagePath(string imageRoot, string imageValue)
        {
            string normalized = imageValue.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
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

    public sealed class LabelStudioDetectionImportResult
    {
        public string TaskJsonPath { get; set; } = string.Empty;

        public string ImageRoot { get; set; } = string.Empty;

        public string TargetSplit { get; set; } = string.Empty;

        public int ImportedTaskCount { get; set; }

        public int LabelFileCount { get; set; }

        public int ImportedResultCount { get; set; }

        public int CategoryCount { get; set; }

        public int SkippedTaskCount { get; set; }

        public int SkippedResultCount { get; set; }

        public List<string> ImportedImagePaths { get; } = new List<string>();
    }
}
