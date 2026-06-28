using MvcVisionSystem.DrawObject;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MvcVisionSystem.Yolo
{
    public static class YoloAnnotationService
    {
        private static readonly string[] DatasetModes =
        {
            YoloDatasetSplitService.TrainMode,
            YoloDatasetSplitService.ValidMode,
            YoloDatasetSplitService.TestMode
        };

        private static readonly string[] ImageExtensions = { ".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff" };

        public static void SaveAnnotations(
            string imageName,
            Image image,
            IReadOnlyDictionary<string, List<CRectangleObject>> roiByClass,
            IReadOnlyList<CClassItem> classes,
            CData data)
        {
            if (string.IsNullOrWhiteSpace(imageName) || image == null || data == null)
            {
                return;
            }

            data.NormalizeOutputPaths();
            data.EnsureYoloOutputDirectories();

            string fileStem = Path.GetFileNameWithoutExtension(imageName);
            if (string.IsNullOrWhiteSpace(fileStem))
            {
                return;
            }

            List<string> lines = BuildAnnotationLines(roiByClass, classes, image.Size);
            string imageExtension = ResolveSourceImageExtension(fileStem, data);
            var targetModes = new HashSet<string>(
                YoloDatasetSplitService.SelectModesForImage(fileStem, data.ProjectSettings?.YoloDataset),
                StringComparer.OrdinalIgnoreCase);

            foreach (string mode in DatasetModes)
            {
                string imageDirectory = Path.Combine(data.OutputRootPath, "data", mode, "images");
                string labelDirectory = Path.Combine(data.OutputRootPath, "data", mode, "labels");
                Directory.CreateDirectory(imageDirectory);
                Directory.CreateDirectory(labelDirectory);

                string imagePath = Path.Combine(imageDirectory, $"{fileStem}{imageExtension}");
                string labelPath = Path.Combine(labelDirectory, $"{fileStem}.txt");
                if (!targetModes.Contains(mode))
                {
                    DeleteDatasetFiles(imageDirectory, fileStem, labelPath);
                    continue;
                }

                DeleteSiblingImageCopies(imageDirectory, fileStem, imageExtension);
                SaveImageCopy(image, imagePath);
                File.WriteAllLines(labelPath, lines);
            }

            data.SaveYoloDataYaml();
        }

        public static void DeleteAnnotations(string imageName, CData data)
        {
            if (string.IsNullOrWhiteSpace(imageName) || data == null)
            {
                return;
            }

            data.NormalizeOutputPaths();
            string fileStem = Path.GetFileNameWithoutExtension(imageName);
            foreach (string mode in DatasetModes)
            {
                string imageDirectory = Path.Combine(data.OutputRootPath, "data", mode, "images");
                string labelPath = Path.Combine(data.OutputRootPath, "data", mode, "labels", $"{fileStem}.txt");
                DeleteDatasetFiles(imageDirectory, fileStem, labelPath);
            }
        }

        public static List<string> BuildAnnotationLines(
            IReadOnlyDictionary<string, List<CRectangleObject>> roiByClass,
            IReadOnlyList<CClassItem> classes,
            Size imageSize)
        {
            var lines = new List<string>();
            if (roiByClass == null || classes == null || imageSize.Width <= 0 || imageSize.Height <= 0)
            {
                return lines;
            }

            for (int classIndex = 0; classIndex < classes.Count; classIndex++)
            {
                string className = classes[classIndex]?.Text ?? "";
                if (string.IsNullOrWhiteSpace(className) || !roiByClass.TryGetValue(className, out List<CRectangleObject> rois))
                {
                    continue;
                }

                foreach (CRectangleObject roiObject in rois.Where(item => item != null))
                {
                    string line = TryCreateYoloLine(classIndex, roiObject.Roi, imageSize);
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        lines.Add(line);
                    }
                }
            }

            return lines;
        }

        public static IReadOnlyDictionary<string, List<Rectangle>> LoadAnnotationRectanglesForImage(
            string imagePath,
            IReadOnlyList<CClassItem> classes,
            CData data,
            Size imageSize)
        {
            foreach (string labelPath in GetCandidateLabelPaths(imagePath, data))
            {
                if (File.Exists(labelPath))
                {
                    return LoadAnnotationRectangles(labelPath, classes, imageSize);
                }
            }

            return new Dictionary<string, List<Rectangle>>();
        }

        public static IReadOnlyDictionary<string, List<Rectangle>> LoadAnnotationRectangles(
            string labelPath,
            IReadOnlyList<CClassItem> classes,
            Size imageSize)
        {
            var result = new Dictionary<string, List<Rectangle>>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(labelPath) || !File.Exists(labelPath) || classes == null)
            {
                return result;
            }

            foreach (string line in File.ReadLines(labelPath))
            {
                if (!TryParseYoloLine(line, imageSize, out int classIndex, out Rectangle rectangle))
                {
                    continue;
                }

                if (classIndex < 0 || classIndex >= classes.Count)
                {
                    continue;
                }

                string className = classes[classIndex]?.Text ?? "";
                if (string.IsNullOrWhiteSpace(className))
                {
                    continue;
                }

                if (!result.TryGetValue(className, out List<Rectangle> rectangles))
                {
                    rectangles = new List<Rectangle>();
                    result.Add(className, rectangles);
                }

                rectangles.Add(rectangle);
            }

            return result;
        }

        public static string TryCreateYoloLine(int classIndex, Rectangle roi, Size imageSize)
        {
            Rectangle clipped = Rectangle.Intersect(roi, new Rectangle(Point.Empty, imageSize));
            if (clipped.Width <= 0 || clipped.Height <= 0)
            {
                return "";
            }

            double centerX = (clipped.Left + clipped.Width / 2.0) / imageSize.Width;
            double centerY = (clipped.Top + clipped.Height / 2.0) / imageSize.Height;
            double width = clipped.Width / (double)imageSize.Width;
            double height = clipped.Height / (double)imageSize.Height;

            return string.Join(" ", new[]
            {
                classIndex.ToString(CultureInfo.InvariantCulture),
                FormatRatio(centerX),
                FormatRatio(centerY),
                FormatRatio(width),
                FormatRatio(height)
            });
        }

        public static bool TryParseYoloLine(string line, Size imageSize, out int classIndex, out Rectangle rectangle)
        {
            classIndex = -1;
            rectangle = Rectangle.Empty;
            if (string.IsNullOrWhiteSpace(line) || imageSize.Width <= 0 || imageSize.Height <= 0)
            {
                return false;
            }

            string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 5 || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out classIndex))
            {
                return false;
            }

            if (!TryParseRatio(parts[1], out double centerX) ||
                !TryParseRatio(parts[2], out double centerY) ||
                !TryParseRatio(parts[3], out double width) ||
                !TryParseRatio(parts[4], out double height) ||
                width <= 0 ||
                height <= 0)
            {
                return false;
            }

            int left = (int)Math.Round((centerX - width / 2D) * imageSize.Width);
            int top = (int)Math.Round((centerY - height / 2D) * imageSize.Height);
            int right = (int)Math.Round((centerX + width / 2D) * imageSize.Width);
            int bottom = (int)Math.Round((centerY + height / 2D) * imageSize.Height);

            rectangle = Rectangle.Intersect(
                Rectangle.FromLTRB(left, top, right, bottom),
                new Rectangle(Point.Empty, imageSize));

            return !rectangle.IsEmpty;
        }

        private static string FormatRatio(double value)
        {
            return Math.Clamp(value, 0, 1).ToString("0.######", CultureInfo.InvariantCulture);
        }

        private static bool TryParseRatio(string value, out double ratio)
        {
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out ratio))
            {
                return false;
            }

            return !double.IsNaN(ratio)
                && !double.IsInfinity(ratio)
                && ratio >= 0
                && ratio <= 1;
        }

        public static IEnumerable<string> GetCandidateLabelPaths(string imagePath, CData data)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                yield break;
            }

            string fileStem = Path.GetFileNameWithoutExtension(imagePath);
            if (string.IsNullOrWhiteSpace(fileStem))
            {
                yield break;
            }

            DirectoryInfo imageDirectory = Directory.GetParent(imagePath);
            if (imageDirectory != null && string.Equals(imageDirectory.Name, "images", StringComparison.OrdinalIgnoreCase))
            {
                string siblingLabelDirectory = Path.Combine(imageDirectory.Parent?.FullName ?? imageDirectory.FullName, "labels");
                yield return Path.Combine(siblingLabelDirectory, $"{fileStem}.txt");
            }

            if (data != null)
            {
                data.NormalizeOutputPaths();
                yield return Path.Combine(data.OutputRootPath, "data", "train", "labels", $"{fileStem}.txt");
                yield return Path.Combine(data.OutputRootPath, "data", "valid", "labels", $"{fileStem}.txt");
                yield return Path.Combine(data.OutputRootPath, "data", "test", "labels", $"{fileStem}.txt");
            }

            yield return Path.ChangeExtension(imagePath, ".txt");
        }

        public static IReadOnlyList<string> GetTargetLabelPaths(string imageName, CData data)
        {
            if (string.IsNullOrWhiteSpace(imageName) || data == null)
            {
                return Array.Empty<string>();
            }

            data.NormalizeOutputPaths();
            string fileStem = Path.GetFileNameWithoutExtension(imageName);
            if (string.IsNullOrWhiteSpace(fileStem))
            {
                return Array.Empty<string>();
            }

            return YoloDatasetSplitService
                .SelectModesForImage(fileStem, data.ProjectSettings?.YoloDataset)
                .Select(mode => Path.Combine(data.OutputRootPath, "data", mode, "labels", $"{fileStem}.txt"))
                .ToList();
        }

        private static string ResolveSourceImageExtension(string fileStem, CData data)
        {
            string sourcePath = data?.LastSelectImagePath;
            if (!string.IsNullOrWhiteSpace(sourcePath)
                && string.Equals(Path.GetFileNameWithoutExtension(sourcePath), fileStem, StringComparison.OrdinalIgnoreCase)
                && IsSupportedImageExtension(Path.GetExtension(sourcePath)))
            {
                return NormalizeImageExtension(Path.GetExtension(sourcePath));
            }

            return ".jpeg";
        }

        private static bool IsSupportedImageExtension(string extension)
            => ImageExtensions.Contains(NormalizeImageExtension(extension), StringComparer.OrdinalIgnoreCase);

        private static string NormalizeImageExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                return string.Empty;
            }

            string normalized = extension.Trim();
            if (!normalized.StartsWith(".", StringComparison.Ordinal))
            {
                normalized = "." + normalized;
            }

            return normalized.ToLowerInvariant();
        }

        private static void DeleteSiblingImageCopies(string imageDirectory, string fileStem, string keepExtension)
        {
            string normalizedKeepExtension = NormalizeImageExtension(keepExtension);
            foreach (string imagePath in EnumerateImageFilesForStem(imageDirectory, fileStem))
            {
                if (!string.Equals(Path.GetExtension(imagePath), normalizedKeepExtension, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(imagePath);
                }
            }
        }

        private static IEnumerable<string> EnumerateImageFilesForStem(string imageDirectory, string fileStem)
        {
            if (string.IsNullOrWhiteSpace(imageDirectory) || string.IsNullOrWhiteSpace(fileStem) || !Directory.Exists(imageDirectory))
            {
                yield break;
            }

            foreach (string extension in ImageExtensions)
            {
                string path = Path.Combine(imageDirectory, $"{fileStem}{extension}");
                if (File.Exists(path))
                {
                    yield return path;
                }
            }
        }

        private static void SaveImageCopy(Image image, string imagePath)
        {
            if (File.Exists(imagePath) && new FileInfo(imagePath).Length > 0)
            {
                return;
            }

            using (Bitmap bitmap = new Bitmap(image))
            {
                bitmap.Save(imagePath, ResolveImageFormat(imagePath));
            }
        }

        private static ImageFormat ResolveImageFormat(string imagePath)
        {
            string extension = NormalizeImageExtension(Path.GetExtension(imagePath));
            if (string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase))
            {
                return ImageFormat.Png;
            }

            if (string.Equals(extension, ".bmp", StringComparison.OrdinalIgnoreCase))
            {
                return ImageFormat.Bmp;
            }

            if (string.Equals(extension, ".tif", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".tiff", StringComparison.OrdinalIgnoreCase))
            {
                return ImageFormat.Tiff;
            }

            return ImageFormat.Jpeg;
        }

        private static void DeleteDatasetFiles(string imageDirectory, string fileStem, string labelPath)
        {
            if (File.Exists(labelPath))
            {
                File.Delete(labelPath);
            }

            // YOLO labels are stem-based, so the same image stem must belong to
            // only one split and one image extension. Stale copies here create
            // duplicate train/valid samples and confusing app reopen behavior.
            foreach (string imagePath in EnumerateImageFilesForStem(imageDirectory, fileStem))
            {
                File.Delete(imagePath);
            }
        }
    }
}
