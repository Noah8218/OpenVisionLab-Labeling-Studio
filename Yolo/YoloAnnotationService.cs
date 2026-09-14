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
            IReadOnlyDictionary<string, List<AnnotationRectangleObject>> roiByClass,
            IReadOnlyList<LabelClass> classes,
            LabelingProjectData data,
            string sourceImagePath = "")
        {
            if (string.IsNullOrWhiteSpace(imageName) || image == null || data == null)
            {
                return;
            }

            EnsureImageIdentity(imageName, image, data, sourceImagePath);

            AnnotationFilePersistence.ExecuteTransaction(
                () => SaveAnnotationsCore(imageName, image, roiByClass, classes, data, sourceImagePath));
        }

        private static void SaveAnnotationsCore(
            string imageName,
            Image image,
            IReadOnlyDictionary<string, List<AnnotationRectangleObject>> roiByClass,
            IReadOnlyList<LabelClass> classes,
            LabelingProjectData data,
            string sourceImagePath)
        {
            data.NormalizeOutputPaths();
            data.EnsureYoloOutputDirectories();

            string fileStem = Path.GetFileNameWithoutExtension(imageName);
            if (string.IsNullOrWhiteSpace(fileStem))
            {
                return;
            }

            List<string> lines = BuildAnnotationLines(roiByClass, classes, image.Size);
            string imageExtension = ResolveSourceImageExtension(fileStem, data, sourceImagePath);
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
                AnnotationFilePersistence.WriteAtomically(
                    labelPath,
                    temporaryPath => File.WriteAllLines(temporaryPath, lines));
            }

            data.SaveYoloDataYaml();
        }

        public static void DeleteAnnotations(string imageName, LabelingProjectData data)
        {
            if (string.IsNullOrWhiteSpace(imageName) || data == null)
            {
                return;
            }

            AnnotationFilePersistence.ExecuteTransaction(() =>
            {
                data.NormalizeOutputPaths();
                string fileStem = Path.GetFileNameWithoutExtension(imageName);
                foreach (string mode in DatasetModes)
                {
                    string imageDirectory = Path.Combine(data.OutputRootPath, "data", mode, "images");
                    string labelPath = Path.Combine(data.OutputRootPath, "data", mode, "labels", $"{fileStem}.txt");
                    DeleteDatasetFiles(imageDirectory, fileStem, labelPath);
                }
            });
        }

        public static List<string> BuildAnnotationLines(
            IReadOnlyDictionary<string, List<AnnotationRectangleObject>> roiByClass,
            IReadOnlyList<LabelClass> classes,
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
                if (string.IsNullOrWhiteSpace(className) || !roiByClass.TryGetValue(className, out List<AnnotationRectangleObject> rois))
                {
                    continue;
                }

                foreach (AnnotationRectangleObject roiObject in rois.Where(item => item != null))
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
            IReadOnlyList<LabelClass> classes,
            LabelingProjectData data,
            Size imageSize)
        {
            foreach (string labelPath in GetCandidateLabelPaths(imagePath, data))
            {
                if (File.Exists(labelPath))
                {
                    EnsureAnnotationImageIdentity(imagePath, labelPath);
                    return LoadAnnotationRectangles(labelPath, classes, imageSize);
                }
            }

            return new Dictionary<string, List<Rectangle>>();
        }

        public static IReadOnlyDictionary<string, List<Rectangle>> LoadAnnotationRectangles(
            string labelPath,
            IReadOnlyList<LabelClass> classes,
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

        public static IEnumerable<string> GetCandidateLabelPaths(string imagePath, LabelingProjectData data)
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

            var emittedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (data != null)
            {
                data.NormalizeOutputPaths();
                string outputRootPath = data.OutputRootPath;
                if (!string.IsNullOrWhiteSpace(outputRootPath))
                {
                    foreach (string mode in DatasetModes)
                    {
                        string datasetLabelPath = Path.Combine(outputRootPath, "data", mode, "labels", $"{fileStem}.txt");
                        if (emittedPaths.Add(datasetLabelPath))
                        {
                            yield return datasetLabelPath;
                        }
                    }

                    if (IsPathUnderDirectory(imagePath, outputRootPath))
                    {
                        string outputSiblingLabelPath = ResolveSiblingLabelPath(imagePath, fileStem);
                        if (!string.IsNullOrWhiteSpace(outputSiblingLabelPath) && emittedPaths.Add(outputSiblingLabelPath))
                        {
                            yield return outputSiblingLabelPath;
                        }

                        string outputSidecarLabelPath = Path.ChangeExtension(imagePath, ".txt");
                        if (!string.IsNullOrWhiteSpace(outputSidecarLabelPath) && emittedPaths.Add(outputSidecarLabelPath))
                        {
                            yield return outputSidecarLabelPath;
                        }
                    }

                    yield break;
                }
            }

            string siblingLabelPath = ResolveSiblingLabelPath(imagePath, fileStem);
            if (!string.IsNullOrWhiteSpace(siblingLabelPath) && emittedPaths.Add(siblingLabelPath))
            {
                yield return siblingLabelPath;
            }

            string sidecarLabelPath = Path.ChangeExtension(imagePath, ".txt");
            if (!string.IsNullOrWhiteSpace(sidecarLabelPath) && emittedPaths.Add(sidecarLabelPath))
            {
                yield return sidecarLabelPath;
            }
        }

        private static string ResolveSiblingLabelPath(string imagePath, string fileStem)
        {
            DirectoryInfo imageDirectory = Directory.GetParent(imagePath);
            if (imageDirectory != null && string.Equals(imageDirectory.Name, "images", StringComparison.OrdinalIgnoreCase))
            {
                string siblingLabelDirectory = Path.Combine(imageDirectory.Parent?.FullName ?? imageDirectory.FullName, "labels");
                return Path.Combine(siblingLabelDirectory, $"{fileStem}.txt");
            }

            return string.Empty;
        }

        private static bool IsPathUnderDirectory(string path, string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(rootDirectory))
            {
                return false;
            }

            try
            {
                string fullPath = Path.GetFullPath(path);
                string fullRoot = Path.GetFullPath(rootDirectory);
                if (!fullRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                    && !fullRoot.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                {
                    fullRoot += Path.DirectorySeparatorChar;
                }

                return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }

        public static IReadOnlyList<string> GetTargetLabelPaths(string imageName, LabelingProjectData data)
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

        internal static void EnsureImageIdentity(
            string imageName,
            Image image,
            LabelingProjectData data,
            string sourceImagePath = "")
        {
            if (string.IsNullOrWhiteSpace(imageName) || image == null || data == null)
            {
                return;
            }

            data.NormalizeOutputPaths();
            string fileStem = Path.GetFileNameWithoutExtension(imageName);
            if (string.IsNullOrWhiteSpace(fileStem) || string.IsNullOrWhiteSpace(data.OutputRootPath))
            {
                return;
            }

            string resolvedSourcePath = sourceImagePath ?? string.Empty;
            List<string> storedImagePaths = DatasetModes
                .SelectMany(mode => EnumerateImageFilesForStem(
                    Path.Combine(data.OutputRootPath, "data", mode, "images"),
                    fileStem))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            EnsureStoredImagesMatch(fileStem, image, resolvedSourcePath, storedImagePaths);
        }

        internal static void EnsureAnnotationImageIdentity(string sourceImagePath, string annotationPath)
        {
            if (string.IsNullOrWhiteSpace(sourceImagePath) || string.IsNullOrWhiteSpace(annotationPath))
            {
                return;
            }

            string fileStem = Path.GetFileNameWithoutExtension(annotationPath);
            DirectoryInfo annotationDirectory = Directory.GetParent(annotationPath);
            if (string.IsNullOrWhiteSpace(fileStem)
                || annotationDirectory?.Parent == null
                || (!string.Equals(annotationDirectory.Name, "labels", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(annotationDirectory.Name, "segments", StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            string imageDirectory = Path.Combine(annotationDirectory.Parent.FullName, "images");
            List<string> storedImagePaths = EnumerateImageFilesForStem(imageDirectory, fileStem).ToList();
            if (storedImagePaths.Count == 0)
            {
                // Keep legacy label-only datasets readable; app-managed saves always include an image copy.
                return;
            }

            if (storedImagePaths.All(path => PathsEqual(path, sourceImagePath)))
            {
                return;
            }

            using Image sourceImage = Image.FromFile(sourceImagePath);
            EnsureStoredImagesMatch(fileStem, sourceImage, sourceImagePath, storedImagePaths);
        }

        private static void EnsureStoredImagesMatch(
            string fileStem,
            Image sourceImage,
            string sourceImagePath,
            IReadOnlyList<string> storedImagePaths)
        {
            if (storedImagePaths == null || storedImagePaths.Count == 0)
            {
                return;
            }

            // Opening the dataset's own canonical image is an unambiguous identity match.
            // Any stale same-stem extension siblings are removed by the normal save path.
            if (storedImagePaths.Any(path => PathsEqual(path, sourceImagePath)))
            {
                return;
            }

            var encodedByExtension = new Dictionary<string, IReadOnlyList<byte[]>>(StringComparer.OrdinalIgnoreCase);
            foreach (string storedImagePath in storedImagePaths)
            {
                string extension = NormalizeImageExtension(Path.GetExtension(storedImagePath));
                if (!encodedByExtension.TryGetValue(extension, out IReadOnlyList<byte[]> encodedSources))
                {
                    encodedSources = EncodeImageCandidates(sourceImage, storedImagePath);
                    encodedByExtension[extension] = encodedSources;
                }

                if (encodedSources.Any(encodedSource => FileMatchesBytes(storedImagePath, encodedSource)))
                {
                    continue;
                }

                string sourceDescription = string.IsNullOrWhiteSpace(sourceImagePath)
                    ? "the current image"
                    : Path.GetFullPath(sourceImagePath);
                throw new YoloImageIdentityCollisionException(
                    $"Image name collision for '{fileStem}': stored dataset image '{storedImagePath}' "
                    + $"does not match '{sourceDescription}'. Rename one source image before saving; existing annotations were not changed.");
            }
        }

        private static IReadOnlyList<byte[]> EncodeImageCandidates(Image sourceImage, string imagePath)
        {
            byte[] originalFormat = EncodeImage(sourceImage, imagePath);
            using var bitmap = new Bitmap(sourceImage);
            using Bitmap wpfWorkspaceFormat = bitmap.Clone(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                PixelFormat.Format24bppRgb);
            byte[] normalizedFormat = EncodeImage(wpfWorkspaceFormat, imagePath);
            return originalFormat.SequenceEqual(normalizedFormat)
                ? new[] { originalFormat }
                : new[] { originalFormat, normalizedFormat };
        }

        private static byte[] EncodeImage(Image image, string imagePath)
        {
            using Bitmap bitmap = CreateBitmapCopy(image);
            using var stream = new MemoryStream();
            bitmap.Save(stream, ResolveImageFormat(imagePath));
            return stream.ToArray();
        }

        private static bool FileMatchesBytes(string path, byte[] expectedBytes)
        {
            if (new FileInfo(path).Length != expectedBytes.Length)
            {
                return false;
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            int offset = 0;
            while (offset < expectedBytes.Length)
            {
                int read = stream.Read(expectedBytes, offset, expectedBytes.Length - offset);
                if (read == 0)
                {
                    return false;
                }

                offset += read;
            }

            return true;
        }

        private static bool PathsEqual(string firstPath, string secondPath)
        {
            if (string.IsNullOrWhiteSpace(firstPath) || string.IsNullOrWhiteSpace(secondPath))
            {
                return false;
            }

            try
            {
                return string.Equals(
                    Path.GetFullPath(firstPath),
                    Path.GetFullPath(secondPath),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }

        private static string ResolveSourceImageExtension(string fileStem, LabelingProjectData data, string sourceImagePath)
        {
            string sourcePath = sourceImagePath ?? string.Empty;
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
                    AnnotationFilePersistence.Delete(imagePath);
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

            using Bitmap bitmap = CreateBitmapCopy(image);
            AnnotationFilePersistence.WriteAtomically(
                imagePath,
                temporaryPath =>
                {
                    using var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    bitmap.Save(stream, ResolveImageFormat(imagePath));
                });
        }

        private static Bitmap CreateBitmapCopy(Image image)
        {
            var bitmap = new Bitmap(image);
            if (image.HorizontalResolution > 0F && image.VerticalResolution > 0F)
            {
                bitmap.SetResolution(image.HorizontalResolution, image.VerticalResolution);
            }

            return bitmap;
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
                AnnotationFilePersistence.Delete(labelPath);
            }

            // YOLO labels are stem-based, so the same image stem must belong to
            // only one split and one image extension. Stale copies here create
            // duplicate train/valid samples and confusing app reopen behavior.
            foreach (string imagePath in EnumerateImageFilesForStem(imageDirectory, fileStem))
            {
                AnnotationFilePersistence.Delete(imagePath);
            }
        }
    }

    internal sealed class YoloImageIdentityCollisionException : InvalidOperationException
    {
        public YoloImageIdentityCollisionException(string message)
            : base(message)
        {
        }
    }
}
