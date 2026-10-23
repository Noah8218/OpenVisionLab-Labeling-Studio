using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace MvcVisionSystem.Yolo
{
    public static class YoloDatasetQualityAuditService
    {
        private static readonly string[] DatasetModes =
        {
            YoloDatasetSplitService.TrainMode,
            YoloDatasetSplitService.ValidMode,
            YoloDatasetSplitService.TestMode
        };

        private static readonly string[] ImageExtensions = { ".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff" };

        public static YoloDatasetQualityAuditReport Build(LabelingProjectData data)
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
            LabelingProjectData data,
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
                Size imageSize;
                try
                {
                    using FileStream stream = File.OpenRead(imagePath);
                    using Image image = Image.FromStream(stream);
                    imageSize = image.Size;
                }
                catch
                {
                    // Dataset integrity validation reports the exact unreadable image.
                    // Keep the remaining read-only quality audit available.
                    continue;
                }

                CountLabelFile(data, labelPath, imageSize, splitSummary, report);
            }

            return splitSummary;
        }

        private static void CountLabelFile(
            LabelingProjectData data,
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
