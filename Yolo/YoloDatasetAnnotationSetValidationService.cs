using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace MvcVisionSystem.Yolo
{
    /// <summary>
    /// Validates the image and annotation files that make up each YOLO dataset split.
    /// </summary>
    public static class YoloDatasetAnnotationSetValidationService
    {
        private static readonly string[] ImageExtensions = { ".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff" };

        public static void Validate(LabelingProjectData data, LabelingDatasetPurpose purpose, IList<string> errors)
        {
            if (data == null || errors == null)
            {
                return;
            }

            if (purpose == LabelingDatasetPurpose.Segmentation)
            {
                ValidateSegmentationImageAndAnnotationSet(
                    "train",
                    data.TrainImagesPath,
                    Path.Combine(data.OutputRootPath, "data", "train", "segments"),
                    Path.Combine(data.OutputRootPath, "data", "train", "masks"),
                    Path.Combine(data.OutputRootPath, "data", "train", "labels"),
                    data.ClassNamedList,
                    errors);
                ValidateSegmentationImageAndAnnotationSet(
                    "valid",
                    data.ValidImagesPath,
                    Path.Combine(data.OutputRootPath, "data", "valid", "segments"),
                    Path.Combine(data.OutputRootPath, "data", "valid", "masks"),
                    Path.Combine(data.OutputRootPath, "data", "valid", "labels"),
                    data.ClassNamedList,
                    errors);
                ValidateOptionalSegmentationImageAndAnnotationSet(
                    "test",
                    data.TestImagesPath,
                    Path.Combine(data.OutputRootPath, "data", "test", "segments"),
                    Path.Combine(data.OutputRootPath, "data", "test", "masks"),
                    Path.Combine(data.OutputRootPath, "data", "test", "labels"),
                    data.ClassNamedList,
                    errors);
                return;
            }

            ValidateImageAndLabelSet(
                "train",
                data.TrainImagesPath,
                Path.Combine(data.OutputRootPath, "data", "train", "labels"),
                data.ClassNamedList,
                errors);
            ValidateImageAndLabelSet(
                "valid",
                data.ValidImagesPath,
                Path.Combine(data.OutputRootPath, "data", "valid", "labels"),
                data.ClassNamedList,
                errors);
            ValidateOptionalImageAndLabelSet(
                "test",
                data.TestImagesPath,
                Path.Combine(data.OutputRootPath, "data", "test", "labels"),
                data.ClassNamedList,
                errors);
        }

        private static void ValidateImageAndLabelSet(
            string mode,
            string imageDirectory,
            string labelDirectory,
            IReadOnlyList<LabelClass> classes,
            IList<string> errors)
        {
            if (!Directory.Exists(imageDirectory))
            {
                errors.Add($"{mode} image directory does not exist: {imageDirectory}");
                return;
            }

            if (!Directory.Exists(labelDirectory))
            {
                errors.Add($"{mode} label directory does not exist: {labelDirectory}");
                return;
            }

            List<string> images = Directory
                .EnumerateFiles(imageDirectory)
                .Where(IsSupportedImageFile)
                .ToList();

            if (images.Count == 0)
            {
                errors.Add($"{mode} image directory has no supported images.");
                return;
            }

            ValidateImageFiles(mode, images, errors);
            ValidateArtifactOwnership(mode, images, labelDirectory, "*.txt", "label", errors);

            foreach (string imagePath in images)
            {
                string labelPath = Path.Combine(labelDirectory, $"{Path.GetFileNameWithoutExtension(imagePath)}.txt");
                if (!File.Exists(labelPath))
                {
                    errors.Add($"{mode} label file is missing for image: {Path.GetFileName(imagePath)}");
                    continue;
                }

                ValidateLabelFile(mode, labelPath, classes, errors);
            }
        }

        private static void ValidateOptionalImageAndLabelSet(
            string mode,
            string imageDirectory,
            string labelDirectory,
            IReadOnlyList<LabelClass> classes,
            IList<string> errors)
        {
            bool hasImages = EnumerateSupportedImages(imageDirectory).Any();
            bool hasLabels = Directory.Exists(labelDirectory) && Directory.EnumerateFiles(labelDirectory, "*.txt").Any();
            if (!hasImages && !hasLabels)
            {
                return;
            }

            ValidateImageAndLabelSet(mode, imageDirectory, labelDirectory, classes, errors);
        }

        private static void ValidateSegmentationImageAndAnnotationSet(
            string mode,
            string imageDirectory,
            string segmentDirectory,
            string maskDirectory,
            string labelDirectory,
            IReadOnlyList<LabelClass> classes,
            IList<string> errors)
        {
            if (!Directory.Exists(imageDirectory))
            {
                errors.Add($"{mode} image directory does not exist: {imageDirectory}");
                return;
            }

            List<string> images = Directory
                .EnumerateFiles(imageDirectory)
                .Where(IsSupportedImageFile)
                .ToList();

            if (images.Count == 0)
            {
                errors.Add($"{mode} image directory has no supported images.");
                return;
            }

            Dictionary<string, Size> imageSizes = ValidateImageFiles(mode, images, errors);
            ValidateArtifactOwnership(mode, images, segmentDirectory, "*.json", "segment", errors);
            ValidateArtifactOwnership(mode, images, maskDirectory, "*.png", "mask", errors);
            ValidateArtifactOwnership(mode, images, labelDirectory, "*.txt", "label", errors);

            foreach (string imagePath in images)
            {
                string fileStem = Path.GetFileNameWithoutExtension(imagePath);
                string segmentPath = Path.Combine(segmentDirectory, $"{fileStem}.json");
                string maskPath = Path.Combine(maskDirectory, $"{fileStem}.png");
                string labelPath = Path.Combine(labelDirectory, $"{fileStem}.txt");
                bool hasSegment = File.Exists(segmentPath);
                bool hasMask = File.Exists(maskPath);
                if (!hasSegment && !hasMask)
                {
                    if (!IsEmptyLabelFile(labelPath))
                    {
                        errors.Add($"{mode} segmentation annotation or empty background label is missing for image: {Path.GetFileName(imagePath)}");
                    }

                    continue;
                }

                if (hasSegment && imageSizes.TryGetValue(imagePath, out Size imageSize))
                {
                    ValidateSegmentFile(mode, segmentPath, maskPath, imageSize, classes, errors);
                }

                if (hasMask && imageSizes.TryGetValue(imagePath, out Size maskImageSize))
                {
                    ValidateMaskFile(mode, maskPath, maskImageSize, errors);
                }
            }
        }

        private static void ValidateOptionalSegmentationImageAndAnnotationSet(
            string mode,
            string imageDirectory,
            string segmentDirectory,
            string maskDirectory,
            string labelDirectory,
            IReadOnlyList<LabelClass> classes,
            IList<string> errors)
        {
            bool hasImages = EnumerateSupportedImages(imageDirectory).Any();
            bool hasSegments = Directory.Exists(segmentDirectory) && Directory.EnumerateFiles(segmentDirectory, "*.json").Any();
            bool hasMasks = Directory.Exists(maskDirectory) && Directory.EnumerateFiles(maskDirectory, "*.png").Any();
            bool hasLabels = Directory.Exists(labelDirectory) && Directory.EnumerateFiles(labelDirectory, "*.txt").Any();
            if (!hasImages && !hasSegments && !hasMasks && !hasLabels)
            {
                return;
            }

            ValidateSegmentationImageAndAnnotationSet(mode, imageDirectory, segmentDirectory, maskDirectory, labelDirectory, classes, errors);
        }

        private static void ValidateSegmentFile(
            string mode,
            string segmentPath,
            string maskPath,
            Size imageSize,
            IReadOnlyList<LabelClass> classes,
            IList<string> errors)
        {
            SegmentationAnnotationFile annotation;
            try
            {
                annotation = JsonConvert.DeserializeObject<SegmentationAnnotationFile>(File.ReadAllText(segmentPath));
            }
            catch (Exception ex)
            {
                errors.Add($"{mode} segment JSON is invalid at {Path.GetFileName(segmentPath)}: {ex.Message}");
                return;
            }

            if (((annotation?.ImageWidth ?? 0) > 0 && annotation.ImageWidth != imageSize.Width)
                || ((annotation?.ImageHeight ?? 0) > 0 && annotation.ImageHeight != imageSize.Height))
            {
                errors.Add($"{mode} dataset integrity: segment dimensions at '{Path.GetFileName(segmentPath)}' do not match the image {imageSize.Width}x{imageSize.Height}.");
            }

            if (annotation?.Polygons == null || annotation.Polygons.Count == 0)
            {
                errors.Add($"{mode} segment JSON has no polygons: {Path.GetFileName(segmentPath)}");
                return;
            }

            try
            {
                YoloSegmentationAnnotationService.LoadSegmentationObjects(segmentPath, maskPath, classes, imageSize);
            }
            catch (Exception ex)
            {
                errors.Add($"{mode} segment JSON is invalid at {Path.GetFileName(segmentPath)}: {ex.Message}");
            }
        }

        private static Dictionary<string, Size> ValidateImageFiles(
            string mode,
            IReadOnlyList<string> images,
            IList<string> errors)
        {
            var sizes = new Dictionary<string, Size>(StringComparer.OrdinalIgnoreCase);
            foreach (IGrouping<string, string> collision in (images ?? Array.Empty<string>())
                .GroupBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1))
            {
                errors.Add(
                    $"{mode} dataset integrity: duplicate image stem '{collision.Key}' is used by "
                    + string.Join(", ", collision.Select(Path.GetFileName).OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
                    + ". Keep exactly one image file for each stem.");
            }

            foreach (string imagePath in images ?? Array.Empty<string>())
            {
                try
                {
                    using Image image = Image.FromFile(imagePath);
                    sizes[imagePath] = image.Size;
                }
                catch (Exception ex)
                {
                    errors.Add($"{mode} dataset integrity: unreadable image '{Path.GetFileName(imagePath)}': {ex.Message}");
                }
            }

            return sizes;
        }

        private static void ValidateArtifactOwnership(
            string mode,
            IReadOnlyList<string> images,
            string artifactDirectory,
            string searchPattern,
            string artifactName,
            IList<string> errors)
        {
            if (!Directory.Exists(artifactDirectory))
            {
                return;
            }

            var imageStems = new HashSet<string>(
                (images ?? Array.Empty<string>()).Select(Path.GetFileNameWithoutExtension),
                StringComparer.OrdinalIgnoreCase);
            foreach (string artifactPath in Directory.EnumerateFiles(artifactDirectory, searchPattern)
                .Where(path => !imageStems.Contains(Path.GetFileNameWithoutExtension(path))))
            {
                errors.Add($"{mode} dataset integrity: orphan {artifactName} '{Path.GetFileName(artifactPath)}' has no image with the same stem.");
            }
        }

        private static void ValidateMaskFile(string mode, string maskPath, Size imageSize, IList<string> errors)
        {
            try
            {
                using Image mask = Image.FromFile(maskPath);
                if (mask.Size != imageSize)
                {
                    errors.Add($"{mode} dataset integrity: mask dimensions at '{Path.GetFileName(maskPath)}' do not match the image {imageSize.Width}x{imageSize.Height}.");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"{mode} dataset integrity: unreadable mask '{Path.GetFileName(maskPath)}': {ex.Message}");
            }
        }

        private static void ValidateLabelFile(string mode, string labelPath, IReadOnlyList<LabelClass> classes, IList<string> errors)
        {
            int lineNo = 0;
            foreach (string line in File.ReadLines(labelPath))
            {
                lineNo++;
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 5)
                {
                    errors.Add($"{mode} label has invalid YOLO format at {Path.GetFileName(labelPath)}:{lineNo}");
                    continue;
                }

                if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int classIndex)
                    || classIndex < 0
                    || classes == null
                    || classIndex >= classes.Count)
                {
                    errors.Add($"{mode} label has invalid class index at {Path.GetFileName(labelPath)}:{lineNo}");
                    continue;
                }

                for (int i = 1; i < parts.Length; i++)
                {
                    if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                        || double.IsNaN(value)
                        || double.IsInfinity(value)
                        || value < 0
                        || value > 1)
                    {
                        errors.Add($"{mode} label has out-of-range normalized value at {Path.GetFileName(labelPath)}:{lineNo}");
                        break;
                    }
                }

                if (double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double width) && width <= 0)
                {
                    errors.Add($"{mode} label width must be greater than zero at {Path.GetFileName(labelPath)}:{lineNo}");
                }

                if (double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out double height) && height <= 0)
                {
                    errors.Add($"{mode} label height must be greater than zero at {Path.GetFileName(labelPath)}:{lineNo}");
                }
            }
        }

        private static bool IsSupportedImageFile(string path)
        {
            string extension = Path.GetExtension(path);
            return ImageExtensions.Any(item => string.Equals(item, extension, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsEmptyLabelFile(string labelPath)
        {
            return File.Exists(labelPath) && File.ReadAllText(labelPath).Trim().Length == 0;
        }

        private static IEnumerable<string> EnumerateSupportedImages(string directory)
        {
            if (!Directory.Exists(directory))
            {
                return Enumerable.Empty<string>();
            }

            return Directory.EnumerateFiles(directory).Where(IsSupportedImageFile);
        }
    }
}
