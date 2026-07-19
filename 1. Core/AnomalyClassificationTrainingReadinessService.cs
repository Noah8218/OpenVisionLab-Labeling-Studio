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
            int trainAbnormalImageCount = 0,
            int validNormalImageCount = 0,
            int validAbnormalImageCount = 0,
            int testNormalImageCount = 0,
            int testAbnormalImageCount = 0)
        {
            SourceImagePaths = sourceImagePaths ?? Array.Empty<string>();
            SourceImageCount = SourceImagePaths.Count;
            NormalImageCount = Math.Max(0, normalImageCount);
            AbnormalImageCount = Math.Max(0, abnormalImageCount);
            UnreviewedImageCount = Math.Max(0, unreviewedImageCount);
            TrainNormalImageCount = Math.Max(0, trainNormalImageCount);
            TrainAbnormalImageCount = Math.Max(0, trainAbnormalImageCount);
            ValidNormalImageCount = Math.Max(0, validNormalImageCount);
            ValidAbnormalImageCount = Math.Max(0, validAbnormalImageCount);
            TestNormalImageCount = Math.Max(0, testNormalImageCount);
            TestAbnormalImageCount = Math.Max(0, testAbnormalImageCount);
            Errors = errors ?? Array.Empty<string>();
        }

        public IReadOnlyList<string> SourceImagePaths { get; }

        public int SourceImageCount { get; }

        public int NormalImageCount { get; }

        public int AbnormalImageCount { get; }

        public int UnreviewedImageCount { get; }

        public int TrainNormalImageCount { get; }

        public int TrainAbnormalImageCount { get; }

        public int ValidNormalImageCount { get; }

        public int ValidAbnormalImageCount { get; }

        public int TestNormalImageCount { get; }

        public int TestAbnormalImageCount { get; }

        public int TrainImageCount => TrainNormalImageCount + TrainAbnormalImageCount;

        public int ValidImageCount => ValidNormalImageCount + ValidAbnormalImageCount;

        public int TestImageCount => TestNormalImageCount + TestAbnormalImageCount;

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
            int validNormalCount = 0;
            int validAbnormalCount = 0;
            int testNormalCount = 0;
            int testAbnormalCount = 0;
            foreach (AnomalyImageReviewStatus item in reviewStatus.GetItems())
            {
                if (!item.IsReviewed)
                {
                    continue;
                }

                IReadOnlyList<string> splits = YoloDatasetSplitService.SelectModesForImage(
                    item.ImageName,
                    data?.ProjectSettings?.YoloDataset);
                string split = splits.FirstOrDefault() ?? YoloDatasetSplitService.TrainMode;
                bool isNormal = item.ReviewState == AnomalyImageReviewState.Normal;
                bool isAbnormal = item.ReviewState == AnomalyImageReviewState.Abnormal;
                if (string.Equals(split, YoloDatasetSplitService.TrainMode, StringComparison.OrdinalIgnoreCase))
                {
                    trainNormalCount += isNormal ? 1 : 0;
                    trainAbnormalCount += isAbnormal ? 1 : 0;
                }
                else if (string.Equals(split, YoloDatasetSplitService.ValidMode, StringComparison.OrdinalIgnoreCase))
                {
                    validNormalCount += isNormal ? 1 : 0;
                    validAbnormalCount += isAbnormal ? 1 : 0;
                }
                else if (string.Equals(split, YoloDatasetSplitService.TestMode, StringComparison.OrdinalIgnoreCase))
                {
                    testNormalCount += isNormal ? 1 : 0;
                    testAbnormalCount += isAbnormal ? 1 : 0;
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
                trainAbnormalCount,
                validNormalCount,
                validAbnormalCount,
                testNormalCount,
                testAbnormalCount);
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
                .SelectMany(path => Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
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
