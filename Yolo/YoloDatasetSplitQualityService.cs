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
            : this(nameOverlapCount, contentOverlapCount, example, string.Empty)
        {
        }

        public YoloDatasetSplitOverlapSummary(
            int nameOverlapCount,
            int contentOverlapCount,
            string example,
            string enumerationError)
        {
            NameOverlapCount = Math.Max(0, nameOverlapCount);
            ContentOverlapCount = Math.Max(0, contentOverlapCount);
            Example = example ?? string.Empty;
            EnumerationError = enumerationError ?? string.Empty;
        }

        public int NameOverlapCount { get; }

        public int ContentOverlapCount { get; }

        public string Example { get; }

        public string EnumerationError { get; }
    }

    /// <summary>
    /// Owns read-only split-overlap QA and its statistics projection.
    /// Local recipe YOLO split roots are intentionally flat because the local exporters and annotation pairing are flat;
    /// native external YOLO intake owns its separate recursive images/labels contract.
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
            if (!TryEnumerateSupportedImages(leftImageDirectory, out List<string> leftImages, out string leftError))
            {
                return new YoloDatasetSplitOverlapSummary(0, 0, string.Empty, leftError);
            }

            if (!TryEnumerateSupportedImages(rightImageDirectory, out List<string> rightImages, out string rightError))
            {
                return new YoloDatasetSplitOverlapSummary(0, 0, string.Empty, rightError);
            }

            if (leftImages.Count == 0 || rightImages.Count == 0)
            {
                return new YoloDatasetSplitOverlapSummary(0, 0, string.Empty);
            }

            try
            {
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
            catch (IOException ex)
            {
                return new YoloDatasetSplitOverlapSummary(0, 0, string.Empty, BuildEnumerationError($"{leftImageDirectory} and {rightImageDirectory}", ex));
            }
            catch (UnauthorizedAccessException ex)
            {
                return new YoloDatasetSplitOverlapSummary(0, 0, string.Empty, BuildEnumerationError($"{leftImageDirectory} and {rightImageDirectory}", ex));
            }
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
            if (!string.IsNullOrWhiteSpace(overlap.EnumerationError))
            {
                if (!errors.Contains(overlap.EnumerationError, StringComparer.Ordinal))
                {
                    errors.Add(overlap.EnumerationError);
                }

                return;
            }

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

        private static bool TryEnumerateSupportedImages(
            string directory,
            out List<string> imagePaths,
            out string error)
        {
            imagePaths = new List<string>();
            error = string.Empty;
            if (!Directory.Exists(directory))
            {
                return true;
            }

            try
            {
                if (IsReparsePoint(directory))
                {
                    error = BuildUnsupportedStructureError(directory, directory);
                    return false;
                }

                string nestedDirectory = Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(nestedDirectory))
                {
                    error = BuildUnsupportedStructureError(directory, nestedDirectory);
                    return false;
                }

                foreach (string path in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                {
                    if (IsReparsePoint(path))
                    {
                        error = BuildUnsupportedStructureError(directory, path);
                        return false;
                    }

                    if (IsSupportedImageFile(path))
                    {
                        imagePaths.Add(path);
                    }
                }

                return true;
            }
            catch (IOException ex)
            {
                error = BuildEnumerationError(directory, ex);
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                error = BuildEnumerationError(directory, ex);
                return false;
            }
        }

        private static bool IsReparsePoint(string path)
            => (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

        private static string BuildUnsupportedStructureError(string datasetDirectory, string unsupportedPath)
            => $"YOLO dataset split directory must be flat; nested or linked path is unsupported before training: {unsupportedPath} (root: {datasetDirectory}). Remove the child directory/link or export the split again.";

        private static string BuildEnumerationError(string directory, Exception exception)
            => $"YOLO dataset split image enumeration failed before training: {directory}. {exception.Message}";

        private static bool IsSupportedImageFile(string path)
        {
            string extension = Path.GetExtension(path);
            return ImageExtensions.Any(item => string.Equals(item, extension, StringComparison.OrdinalIgnoreCase));
        }
    }
}
