using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace MvcVisionSystem.Yolo
{
    /// <summary>
    /// Reports image-name and byte-content overlap between two dataset splits.
    /// </summary>
    public sealed class YoloDatasetSplitOverlapSummary
    {
        public YoloDatasetSplitOverlapSummary(int nameOverlapCount, int contentOverlapCount, string example)
        {
            NameOverlapCount = Math.Max(0, nameOverlapCount);
            ContentOverlapCount = Math.Max(0, contentOverlapCount);
            Example = example ?? string.Empty;
        }

        public int NameOverlapCount { get; }

        public int ContentOverlapCount { get; }

        public string Example { get; }
    }

    /// <summary>
    /// Owns read-only split-overlap QA and its statistics projection.
    /// Split assignment and file validation remain with their existing owners.
    /// </summary>
    public static class YoloDatasetSplitQualityService
    {
        private static readonly string[] ImageExtensions =
        {
            ".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff"
        };

        public static YoloDatasetSplitOverlapSummary BuildOverlapSummary(
            string leftImageDirectory,
            string rightImageDirectory)
        {
            List<string> leftImages = EnumerateSupportedImages(leftImageDirectory).ToList();
            List<string> rightImages = EnumerateSupportedImages(rightImageDirectory).ToList();
            if (leftImages.Count == 0 || rightImages.Count == 0)
            {
                return new YoloDatasetSplitOverlapSummary(0, 0, string.Empty);
            }

            var rightNames = new HashSet<string>(
                rightImages.Select(Path.GetFileName).Where(name => !string.IsNullOrWhiteSpace(name)),
                StringComparer.OrdinalIgnoreCase);
            int nameOverlap = leftImages.Count(path => rightNames.Contains(Path.GetFileName(path)));

            Dictionary<string, string> rightContent = BuildContentMap(rightImages);
            int contentOverlap = 0;
            string example = string.Empty;
            foreach (string leftImage in leftImages)
            {
                string hash = BuildFileContentKey(leftImage);
                if (!rightContent.TryGetValue(hash, out string rightImage))
                {
                    continue;
                }

                contentOverlap++;
                if (string.IsNullOrWhiteSpace(example))
                {
                    example = $"{Path.GetFileName(leftImage)} == {Path.GetFileName(rightImage)}";
                }
            }

            return new YoloDatasetSplitOverlapSummary(nameOverlap, contentOverlap, example);
        }

        public static void ValidateSeparation(
            string leftMode,
            string leftImageDirectory,
            string rightMode,
            string rightImageDirectory,
            IList<string> errors)
        {
            if (errors == null)
            {
                return;
            }

            YoloDatasetSplitOverlapSummary overlap = BuildOverlapSummary(leftImageDirectory, rightImageDirectory);
            if (overlap.ContentOverlapCount <= 0)
            {
                return;
            }

            string example = string.IsNullOrWhiteSpace(overlap.Example)
                ? string.Empty
                : $" Example: {overlap.Example}.";
            errors.Add($"{leftMode}/{rightMode} image split has duplicate image content: {overlap.ContentOverlapCount} overlapping image(s). Use different split images before training.{example}");
        }

        public static void AddStatistics(
            string trainImageDirectory,
            string validImageDirectory,
            string testImageDirectory,
            YoloDatasetStatistics statistics)
        {
            if (statistics == null)
            {
                return;
            }

            YoloDatasetSplitOverlapSummary trainValid = BuildOverlapSummary(trainImageDirectory, validImageDirectory);
            YoloDatasetSplitOverlapSummary trainTest = BuildOverlapSummary(trainImageDirectory, testImageDirectory);
            YoloDatasetSplitOverlapSummary validTest = BuildOverlapSummary(validImageDirectory, testImageDirectory);
            statistics.TrainValidImageNameOverlapCount = trainValid.NameOverlapCount;
            statistics.TrainValidImageContentOverlapCount = trainValid.ContentOverlapCount;
            statistics.TrainValidImageOverlapExample = trainValid.Example;
            statistics.SplitImageContentOverlapCount = trainValid.ContentOverlapCount + trainTest.ContentOverlapCount + validTest.ContentOverlapCount;
            statistics.SplitImageOverlapExample = new[] { trainValid.Example, trainTest.Example, validTest.Example }
                .FirstOrDefault(example => !string.IsNullOrWhiteSpace(example)) ?? string.Empty;
        }

        private static Dictionary<string, string> BuildContentMap(IEnumerable<string> imagePaths)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string imagePath in imagePaths ?? Enumerable.Empty<string>())
            {
                string hash = BuildFileContentKey(imagePath);
                if (!map.ContainsKey(hash))
                {
                    map[hash] = imagePath;
                }
            }

            return map;
        }

        private static string BuildFileContentKey(string path)
        {
            var info = new FileInfo(path);
            using FileStream stream = File.OpenRead(path);
            using SHA256 sha = SHA256.Create();
            string hash = Convert.ToBase64String(sha.ComputeHash(stream));
            return $"{info.Length}:{hash}";
        }

        private static IEnumerable<string> EnumerateSupportedImages(string directory)
        {
            if (!Directory.Exists(directory))
            {
                return Enumerable.Empty<string>();
            }

            return Directory.EnumerateFiles(directory).Where(IsSupportedImageFile);
        }

        private static bool IsSupportedImageFile(string path)
        {
            string extension = Path.GetExtension(path);
            return ImageExtensions.Any(item => string.Equals(item, extension, StringComparison.OrdinalIgnoreCase));
        }
    }
}
