using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MvcVisionSystem.Yolo
{
    public sealed class AnomalyClassificationDatasetExportResult
    {
        public string DatasetRootPath { get; set; } = string.Empty;

        public int NormalImageCount { get; set; }

        public int AbnormalImageCount { get; set; }

        public int SkippedImageCount { get; set; }

        public int TotalExportedImageCount => NormalImageCount + AbnormalImageCount;
    }

    public sealed class AnomalyClassificationDatasetExportService
    {
        public const string DefaultFolderName = "classification";
        public const string NormalClassFolderName = "normal";
        public const string AbnormalClassFolderName = "abnormal";

        public AnomalyClassificationDatasetExportResult Export(
            CData data,
            IEnumerable<string> imagePaths,
            string datasetRootPath = "")
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            data.ProjectSettings?.EnsureDefaults();
            string rootPath = ResolveDatasetRootPath(data, datasetRootPath);
            Directory.CreateDirectory(rootPath);

            string[] orderedImages = (imagePaths ?? Enumerable.Empty<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var reviewStatus = new AnomalyImageReviewStatusService();
            reviewStatus.LoadReviewStatus(data, orderedImages);
            reviewStatus.ImportUnreviewedStatesFromParentFolders();

            var result = new AnomalyClassificationDatasetExportResult
            {
                DatasetRootPath = rootPath
            };

            foreach (AnomalyImageReviewStatus item in reviewStatus.GetItems())
            {
                if (!item.IsReviewed || !File.Exists(item.ImagePath))
                {
                    result.SkippedImageCount++;
                    continue;
                }

                string classFolderName = item.ReviewState == AnomalyImageReviewState.Normal
                    ? NormalClassFolderName
                    : AbnormalClassFolderName;
                IReadOnlyList<string> splits = YoloDatasetSplitService.SelectModesForImage(
                    item.ImageName,
                    data.ProjectSettings?.YoloDataset);

                foreach (string split in splits)
                {
                    string targetDirectory = Path.Combine(rootPath, NormalizeSplit(split), classFolderName);
                    Directory.CreateDirectory(targetDirectory);
                    string targetPath = ResolveUniqueTargetPath(targetDirectory, item.ImagePath);
                    File.Copy(item.ImagePath, targetPath, overwrite: false);
                    if (item.ReviewState == AnomalyImageReviewState.Normal)
                    {
                        result.NormalImageCount++;
                    }
                    else
                    {
                        result.AbnormalImageCount++;
                    }
                }
            }

            return result;
        }

        private static string ResolveDatasetRootPath(CData data, string datasetRootPath)
        {
            if (!string.IsNullOrWhiteSpace(datasetRootPath))
            {
                return Path.GetFullPath(datasetRootPath);
            }

            string outputRootPath = data.OutputRootPath;
            if (string.IsNullOrWhiteSpace(outputRootPath))
            {
                throw new ArgumentException("Output root path is required for anomaly classification export.", nameof(data));
            }

            return Path.Combine(outputRootPath, DefaultFolderName);
        }

        private static string NormalizeSplit(string split)
        {
            return string.IsNullOrWhiteSpace(split)
                ? YoloDatasetSplitService.TrainMode
                : split.Trim();
        }

        private static string ResolveUniqueTargetPath(string targetDirectory, string sourcePath)
        {
            string fileName = Path.GetFileName(sourcePath);
            string candidate = Path.Combine(targetDirectory, fileName);
            if (!File.Exists(candidate))
            {
                return candidate;
            }

            string stem = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);
            for (int index = 2; ; index++)
            {
                candidate = Path.Combine(targetDirectory, $"{stem}-{index}{extension}");
                if (!File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }
    }
}
