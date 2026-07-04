using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MvcVisionSystem.Yolo;

namespace MvcVisionSystem._1._Core
{
    public sealed class AnomalyClassificationTrainingReadinessReport
    {
        public AnomalyClassificationTrainingReadinessReport(
            IReadOnlyList<string> sourceImagePaths,
            int normalImageCount,
            int abnormalImageCount,
            int unreviewedImageCount,
            IReadOnlyList<string> errors,
            int trainNormalImageCount = 0,
            int trainAbnormalImageCount = 0)
        {
            SourceImagePaths = sourceImagePaths ?? Array.Empty<string>();
            SourceImageCount = SourceImagePaths.Count;
            NormalImageCount = Math.Max(0, normalImageCount);
            AbnormalImageCount = Math.Max(0, abnormalImageCount);
            UnreviewedImageCount = Math.Max(0, unreviewedImageCount);
            TrainNormalImageCount = Math.Max(0, trainNormalImageCount);
            TrainAbnormalImageCount = Math.Max(0, trainAbnormalImageCount);
            Errors = errors ?? Array.Empty<string>();
        }

        public IReadOnlyList<string> SourceImagePaths { get; }

        public int SourceImageCount { get; }

        public int NormalImageCount { get; }

        public int AbnormalImageCount { get; }

        public int UnreviewedImageCount { get; }

        public int TrainNormalImageCount { get; }

        public int TrainAbnormalImageCount { get; }

        public IReadOnlyList<string> Errors { get; }

        public bool IsReady => Errors.Count == 0;
    }

    public static class AnomalyClassificationTrainingReadinessService
    {
        public const string NoSourceImagesError = "anomaly classification training needs source images";
        public const string NeedsReviewedNormalAndAbnormalError = "anomaly classification training needs reviewed normal and abnormal images";
        public const string NeedsTrainNormalAndAbnormalError = "anomaly classification training needs train split normal and abnormal images";

        public static AnomalyClassificationTrainingReadinessReport Build(CData data)
        {
            string[] sourceImagePaths = EnumerateSourceImages(data).ToArray();
            var errors = new List<string>();
            if (sourceImagePaths.Length == 0)
            {
                errors.Add(NoSourceImagesError);
                return new AnomalyClassificationTrainingReadinessReport(
                    sourceImagePaths,
                    normalImageCount: 0,
                    abnormalImageCount: 0,
                    unreviewedImageCount: 0,
                    errors);
            }

            var reviewStatus = new AnomalyImageReviewStatusService();
            reviewStatus.LoadReviewStatus(data, sourceImagePaths);
            AnomalyImageReviewSummary summary = reviewStatus.BuildSummary();
            int trainNormalCount = 0;
            int trainAbnormalCount = 0;
            foreach (AnomalyImageReviewStatus item in reviewStatus.GetItems())
            {
                if (!item.IsReviewed)
                {
                    continue;
                }

                IReadOnlyList<string> splits = YoloDatasetSplitService.SelectModesForImage(
                    item.ImageName,
                    data?.ProjectSettings?.YoloDataset);
                if (!splits.Contains(YoloDatasetSplitService.TrainMode, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (item.ReviewState == AnomalyImageReviewState.Normal)
                {
                    trainNormalCount++;
                }
                else if (item.ReviewState == AnomalyImageReviewState.Abnormal)
                {
                    trainAbnormalCount++;
                }
            }

            if (summary.NormalImageCount == 0 || summary.AbnormalImageCount == 0)
            {
                errors.Add($"{NeedsReviewedNormalAndAbnormalError}. Normal:{summary.NormalImageCount}, Abnormal:{summary.AbnormalImageCount}, Unreviewed:{summary.UnreviewedImageCount}");
            }
            else if (trainNormalCount == 0 || trainAbnormalCount == 0)
            {
                errors.Add($"{NeedsTrainNormalAndAbnormalError}. TrainNormal:{trainNormalCount}, TrainAbnormal:{trainAbnormalCount}, Normal:{summary.NormalImageCount}, Abnormal:{summary.AbnormalImageCount}");
            }

            return new AnomalyClassificationTrainingReadinessReport(
                sourceImagePaths,
                summary.NormalImageCount,
                summary.AbnormalImageCount,
                summary.UnreviewedImageCount,
                errors,
                trainNormalCount,
                trainAbnormalCount);
        }

        private static IEnumerable<string> EnumerateSourceImages(CData data)
        {
            var roots = new[]
            {
                data?.ProjectSettings?.PythonModel?.ImageRootPath,
                data?.TrainImagesPath,
                data?.ValidImagesPath,
                data?.TestImagesPath
            };

            return roots
                .Where(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                .SelectMany(path => Directory.EnumerateFiles(path))
                .Where(IsImageFile)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsImageFile(string path)
        {
            string extension = Path.GetExtension(path)?.ToLowerInvariant() ?? string.Empty;
            return extension == ".bmp"
                || extension == ".jpg"
                || extension == ".jpeg"
                || extension == ".png"
                || extension == ".tif"
                || extension == ".tiff";
        }
    }
}
