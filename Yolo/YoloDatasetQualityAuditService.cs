using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace MvcVisionSystem.Yolo
{
    public sealed class YoloDatasetQualityAuditReport
    {
        private readonly Dictionary<string, int> objectCountByClass = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public List<YoloDatasetQualityAuditSplitSummary> Splits { get; } = new List<YoloDatasetQualityAuditSplitSummary>();

        public int TotalImageCount => Splits.Sum(item => item.ImageCount);

        public int TotalLabelFileCount => Splits.Sum(item => item.LabelFileCount);

        public int TotalMissingLabelCount => Splits.Sum(item => item.MissingLabelCount);

        public int TotalEmptyLabelCount => Splits.Sum(item => item.EmptyLabelCount);

        public int TotalInvalidLabelLineCount => Splits.Sum(item => item.InvalidLabelLineCount);

        public int TotalObjectCount => Splits.Sum(item => item.ObjectCount);

        public IReadOnlyDictionary<string, int> ObjectCountByClass => objectCountByClass;

        public IReadOnlyList<string> SummaryLines
        {
            get
            {
                var lines = Splits
                    .Select(split => $"Dataset quality audit. Split:{split.Split}, Images:{split.ImageCount}, Labels:{split.LabelFileCount}, MissingLabels:{split.MissingLabelCount}, EmptyLabels:{split.EmptyLabelCount}, InvalidLabels:{split.InvalidLabelLineCount}, Objects:{split.ObjectCount}")
                    .ToList();

                foreach (KeyValuePair<string, int> item in objectCountByClass.OrderBy(item => item.Key))
                {
                    lines.Add($"Dataset quality class distribution. {item.Key}:{item.Value}");
                }

                return lines;
            }
        }

        internal void AddClassObject(string className)
        {
            if (string.IsNullOrWhiteSpace(className))
            {
                return;
            }

            objectCountByClass.TryGetValue(className, out int count);
            objectCountByClass[className] = count + 1;
        }
    }

    public sealed class YoloDatasetQualityAuditSplitSummary
    {
        private readonly Dictionary<string, int> objectCountByClass = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public string Split { get; set; } = string.Empty;

        public int ImageCount { get; internal set; }

        public int LabelFileCount { get; internal set; }

        public int MissingLabelCount { get; internal set; }

        public int EmptyLabelCount { get; internal set; }

        public int InvalidLabelLineCount { get; internal set; }

        public int ObjectCount { get; internal set; }

        public IReadOnlyDictionary<string, int> ObjectCountByClass => objectCountByClass;

        internal void AddClassObject(string className)
        {
            if (string.IsNullOrWhiteSpace(className))
            {
                return;
            }

            objectCountByClass.TryGetValue(className, out int count);
            objectCountByClass[className] = count + 1;
            ObjectCount++;
        }
    }

    public static class YoloDatasetQualityAuditService
    {
        private static readonly string[] DatasetModes =
        {
            YoloDatasetSplitService.TrainMode,
            YoloDatasetSplitService.ValidMode,
            YoloDatasetSplitService.TestMode
        };

        private static readonly string[] ImageExtensions = { ".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff" };

        public static YoloDatasetQualityAuditReport Build(CData data)
        {
            var report = new YoloDatasetQualityAuditReport();
            if (data == null)
            {
                return report;
            }

            data.NormalizeOutputPaths();
            foreach (string split in DatasetModes)
            {
                YoloDatasetQualityAuditSplitSummary splitSummary = BuildSplit(data, split, report);
                report.Splits.Add(splitSummary);
            }

            return report;
        }

        private static YoloDatasetQualityAuditSplitSummary BuildSplit(
            CData data,
            string split,
            YoloDatasetQualityAuditReport report)
        {
            var splitSummary = new YoloDatasetQualityAuditSplitSummary
            {
                Split = split
            };

            string imageDirectory = Path.Combine(data.OutputRootPath, "data", split, "images");
            string labelDirectory = Path.Combine(data.OutputRootPath, "data", split, "labels");
            foreach (string imagePath in EnumerateImageFiles(imageDirectory))
            {
                splitSummary.ImageCount++;
                string labelPath = Path.Combine(labelDirectory, $"{Path.GetFileNameWithoutExtension(imagePath)}.txt");
                if (!File.Exists(labelPath))
                {
                    splitSummary.MissingLabelCount++;
                    continue;
                }

                splitSummary.LabelFileCount++;
                using Image image = Image.FromFile(imagePath);
                CountLabelFile(data, labelPath, image.Size, splitSummary, report);
            }

            return splitSummary;
        }

        private static void CountLabelFile(
            CData data,
            string labelPath,
            Size imageSize,
            YoloDatasetQualityAuditSplitSummary splitSummary,
            YoloDatasetQualityAuditReport report)
        {
            bool hasContentLine = false;
            foreach (string line in File.ReadLines(labelPath))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                hasContentLine = true;
                if (!YoloAnnotationService.TryParseYoloLine(line, imageSize, out int classIndex, out _)
                    || classIndex < 0
                    || classIndex >= (data.ClassNamedList?.Count ?? 0)
                    || string.IsNullOrWhiteSpace(data.ClassNamedList[classIndex]?.Text))
                {
                    splitSummary.InvalidLabelLineCount++;
                    continue;
                }

                string className = data.ClassNamedList[classIndex].Text.Trim();
                splitSummary.AddClassObject(className);
                report.AddClassObject(className);
            }

            if (!hasContentLine)
            {
                splitSummary.EmptyLabelCount++;
            }
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
}
