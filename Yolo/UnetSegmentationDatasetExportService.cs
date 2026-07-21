using MvcVisionSystem;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace MvcVisionSystem.Yolo
{
    public sealed class UnetSegmentationDatasetExportResult
    {
        public string OutputRootPath { get; internal set; } = string.Empty;

        public string DatasetFingerprint { get; internal set; } = string.Empty;

        public string SourceDataTreeSha256Before { get; internal set; } = string.Empty;

        public string SourceDataTreeSha256After { get; internal set; } = string.Empty;

        public string ClassContractSha256 { get; internal set; } = string.Empty;

        public bool ReusedExistingArtifact { get; internal set; }

        public int ImageCount { get; internal set; }

        public int PositiveMaskImageCount { get; internal set; }

        public List<UnetSegmentationDatasetExportSplitSummary> Splits { get; } = new List<UnetSegmentationDatasetExportSplitSummary>();

        public List<string> Errors { get; } = new List<string>();

        public bool IsReady => Errors.Count == 0
            && ImageCount > 0
            && !string.IsNullOrWhiteSpace(OutputRootPath)
            && !string.IsNullOrWhiteSpace(DatasetFingerprint)
            && string.Equals(SourceDataTreeSha256Before, SourceDataTreeSha256After, StringComparison.OrdinalIgnoreCase);
    }

    public sealed class UnetSegmentationDatasetExportSplitSummary
    {
        public UnetSegmentationDatasetExportSplitSummary(string split)
        {
            Split = split ?? string.Empty;
        }

        public string Split { get; }

        public int ImageCount { get; internal set; }

        public int PositiveMaskImageCount { get; internal set; }

        public int BackgroundMaskImageCount { get; internal set; }
    }

    /// <summary>
    /// Creates an app-owned U-Net semantic-segmentation export from the recipe's saved split images and masks.
    /// The recipe data tree is read-only; output is kept below artifacts/unet-dataset.
    /// </summary>
    public static class UnetSegmentationDatasetExportService
    {
        private const int ManifestVersion = 1;

        private static readonly string[] DatasetModes =
        {
            YoloDatasetSplitService.TrainMode,
            YoloDatasetSplitService.ValidMode,
            YoloDatasetSplitService.TestMode
        };

        private static readonly HashSet<string> ImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff"
        };

        public static UnetSegmentationDatasetExportResult Export(CData data)
        {
            var result = new UnetSegmentationDatasetExportResult();
            ExportPlan plan = BuildPlan(data, result);
            if (plan == null || result.Errors.Count > 0)
            {
                return result;
            }

            result.SourceDataTreeSha256Before = ComputeDirectorySha256(plan.SourceDataRootPath);
            result.ClassContractSha256 = ComputeTextSha256(JsonConvert.SerializeObject(plan.Classes));
            result.DatasetFingerprint = ComputeTextSha256(result.SourceDataTreeSha256Before + "\n" + result.ClassContractSha256)
                .ToLowerInvariant();
            result.OutputRootPath = Path.Combine(
                plan.RecipeRootPath,
                "artifacts",
                "unet-dataset",
                result.DatasetFingerprint);

            if (TryReuseExistingArtifact(result, plan))
            {
                result.SourceDataTreeSha256After = ComputeDirectorySha256(plan.SourceDataRootPath);
                return result;
            }

            string artifactParentPath = Path.GetDirectoryName(result.OutputRootPath) ?? string.Empty;
            string temporaryPath = Path.Combine(artifactParentPath, "." + result.DatasetFingerprint + ".tmp-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(temporaryPath);
                MaterializeExport(temporaryPath, plan, result);
                WriteManifestFiles(temporaryPath, plan, result);
                Directory.Move(temporaryPath, result.OutputRootPath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is ExternalException)
            {
                result.Errors.Add("U-Net segmentation export failed: " + ex.Message);
            }
            finally
            {
                if (Directory.Exists(temporaryPath))
                {
                    Directory.Delete(temporaryPath, recursive: true);
                }
            }

            result.SourceDataTreeSha256After = ComputeDirectorySha256(plan.SourceDataRootPath);
            if (!string.Equals(result.SourceDataTreeSha256Before, result.SourceDataTreeSha256After, StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add("Recipe data changed while the U-Net export was being created; the artifact is not usable.");
            }

            return result;
        }

        public static string ComputeSourceDataTreeSha256(CData data)
        {
            string recipeRootPath = data?.OutputRootPath ?? string.Empty;
            string sourceDataRootPath = Path.Combine(recipeRootPath, "data");
            return Directory.Exists(sourceDataRootPath) ? ComputeDirectorySha256(sourceDataRootPath) : string.Empty;
        }

        private static ExportPlan BuildPlan(CData data, UnetSegmentationDatasetExportResult result)
        {
            if (data == null)
            {
                result.Errors.Add("U-Net export needs a recipe dataset configuration.");
                return null;
            }

            if (data.ProjectSettings?.DatasetPurpose != LabelingDatasetPurpose.Segmentation)
            {
                result.Errors.Add("U-Net export is available only for Segmentation recipes. Bounding-box-only recipes need segmentation masks.");
                return null;
            }

            string recipeRootPath = data.OutputRootPath ?? string.Empty;
            string sourceDataRootPath = Path.Combine(recipeRootPath, "data");
            if (string.IsNullOrWhiteSpace(recipeRootPath) || !Directory.Exists(sourceDataRootPath))
            {
                result.Errors.Add("U-Net export needs the recipe data directory: " + sourceDataRootPath);
                return null;
            }

            List<UnetClassContractItem> classes = BuildClassContract(data.ClassNamedList, result.Errors);
            if (result.Errors.Count > 0)
            {
                return null;
            }

            var plan = new ExportPlan(recipeRootPath, sourceDataRootPath, classes);
            var contentSplitByHash = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string split in DatasetModes)
            {
                var splitSummary = new UnetSegmentationDatasetExportSplitSummary(split);
                result.Splits.Add(splitSummary);
                string imageDirectory = Path.Combine(sourceDataRootPath, split, "images");
                if (!Directory.Exists(imageDirectory))
                {
                    result.Errors.Add($"U-Net export needs a {split} image split: {imageDirectory}");
                    continue;
                }

                List<string> imagePaths = Directory.EnumerateFiles(imageDirectory, "*", SearchOption.AllDirectories)
                    .Where(path => ImageExtensions.Contains(Path.GetExtension(path)))
                    .OrderBy(path => Path.GetRelativePath(imageDirectory, path), StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (imagePaths.Count == 0)
                {
                    result.Errors.Add($"U-Net export needs at least one image in the {split} split.");
                    continue;
                }

                var relativeStems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string imagePath in imagePaths)
                {
                    string relativeImagePath = Path.GetRelativePath(imageDirectory, imagePath);
                    string relativeStem = Path.ChangeExtension(relativeImagePath, null) ?? string.Empty;
                    if (!relativeStems.Add(relativeStem))
                    {
                        result.Errors.Add($"U-Net export found duplicate image stems in {split}: {relativeStem}");
                        continue;
                    }

                    if (!TryBuildEntry(
                        sourceDataRootPath,
                        split,
                        imagePath,
                        relativeImagePath,
                        classes,
                        out ExportEntry entry,
                        out string error))
                    {
                        result.Errors.Add(error);
                        continue;
                    }

                    if (contentSplitByHash.TryGetValue(entry.ImageSha256, out string existingSplit))
                    {
                        result.Errors.Add($"U-Net export rejects duplicate image content across splits: {existingSplit} and {split} share {relativeImagePath}.");
                        continue;
                    }

                    contentSplitByHash.Add(entry.ImageSha256, split);
                    plan.Entries.Add(entry);
                    splitSummary.ImageCount++;
                    if (entry.HasForeground)
                    {
                        splitSummary.PositiveMaskImageCount++;
                        result.PositiveMaskImageCount++;
                    }
                    else
                    {
                        splitSummary.BackgroundMaskImageCount++;
                    }
                    result.ImageCount++;
                }
            }

            foreach (UnetSegmentationDatasetExportSplitSummary split in result.Splits)
            {
                if (split.ImageCount == 0)
                {
                    continue;
                }

                if (split.PositiveMaskImageCount == 0)
                {
                    result.Errors.Add($"U-Net export needs at least one positive mask in the {split} split.");
                }
            }

            return result.Errors.Count == 0 ? plan : null;
        }

        private static List<UnetClassContractItem> BuildClassContract(IReadOnlyList<CClassItem> sourceClasses, List<string> errors)
        {
            var classes = new List<UnetClassContractItem>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (CClassItem sourceClass in sourceClasses ?? Array.Empty<CClassItem>())
            {
                string name = sourceClass?.Text?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name))
                {
                    errors.Add("U-Net export needs a non-empty class name for every segmentation class.");
                    continue;
                }
                if (!names.Add(name))
                {
                    errors.Add("U-Net export needs unique segmentation class names: " + name);
                    continue;
                }

                classes.Add(new UnetClassContractItem { Index = classes.Count + 1, Name = name });
            }

            if (classes.Count == 0)
            {
                errors.Add("U-Net export needs at least one segmentation class.");
            }
            else if (classes.Count > 254)
            {
                errors.Add("U-Net export supports at most 254 foreground classes because mask value 0 is reserved for background.");
            }

            return classes;
        }

        private static bool TryBuildEntry(
            string sourceDataRootPath,
            string split,
            string imagePath,
            string relativeImagePath,
            IReadOnlyList<UnetClassContractItem> classes,
            out ExportEntry entry,
            out string error)
        {
            entry = null;
            error = string.Empty;
            Size imageSize;
            try
            {
                using var image = new Bitmap(imagePath);
                imageSize = image.Size;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is ExternalException || ex is IOException)
            {
                error = "U-Net export could not read source image " + imagePath + ": " + ex.Message;
                return false;
            }

            string relativeStem = Path.ChangeExtension(relativeImagePath, null) ?? string.Empty;
            string splitRootPath = Path.Combine(sourceDataRootPath, split);
            string maskPath = ResolveAnnotationPath(splitRootPath, "masks", relativeStem, ".png");
            string segmentPath = ResolveAnnotationPath(splitRootPath, "segments", relativeStem, ".json");
            string labelPath = ResolveAnnotationPath(splitRootPath, "labels", relativeStem, ".txt");
            byte[] maskValues = null;

            if (File.Exists(maskPath)
                && !TryReadMaskValues(maskPath, imageSize, classes.Count, out maskValues, out error))
            {
                error = "U-Net export cannot use mask " + maskPath + ": " + error;
                return false;
            }

            byte[] segmentValues = null;
            if (File.Exists(segmentPath))
            {
                if (!TryBuildMaskFromSegmentJson(segmentPath, imageSize, classes, out segmentValues, out error))
                {
                    error = "U-Net export cannot use segment annotation " + segmentPath + ": " + error;
                    return false;
                }

                if (maskValues == null)
                {
                    maskValues = segmentValues;
                }
            }

            if (maskValues == null)
            {
                if (!File.Exists(labelPath) || !IsEmptyTextFile(labelPath))
                {
                    error = "U-Net export needs a segmentation mask/polygon or an explicit empty background label for " + imagePath;
                    return false;
                }

                maskValues = new byte[imageSize.Width * imageSize.Height];
            }

            bool hasForeground = maskValues.Any(value => value != 0);
            entry = new ExportEntry
            {
                Split = split,
                SourceImagePath = imagePath,
                SourceRelativeImagePath = Path.Combine(split, "images", relativeImagePath).Replace('\\', '/'),
                RelativeImagePath = relativeImagePath,
                RelativeMaskPath = Path.ChangeExtension(relativeImagePath, ".png") ?? string.Empty,
                ImageSha256 = ComputeFileSha256(imagePath),
                ImageWidth = imageSize.Width,
                ImageHeight = imageSize.Height,
                MaskValues = maskValues,
                HasForeground = hasForeground
            };
            return true;
        }

        private static string ResolveAnnotationPath(string splitRootPath, string annotationDirectory, string relativeStem, string extension)
        {
            string nestedPath = Path.Combine(splitRootPath, annotationDirectory, relativeStem + extension);
            if (File.Exists(nestedPath))
            {
                return nestedPath;
            }

            return Path.Combine(splitRootPath, annotationDirectory, Path.GetFileName(relativeStem) + extension);
        }

        private static bool TryReadMaskValues(string maskPath, Size imageSize, int classCount, out byte[] values, out string error)
        {
            values = null;
            error = string.Empty;
            try
            {
                using var source = new Bitmap(maskPath);
                if (source.Size != imageSize)
                {
                    error = $"mask size {source.Width}x{source.Height} does not match image size {imageSize.Width}x{imageSize.Height}";
                    return false;
                }

                values = ReadBitmapValues(source, requireGrayscale: true, out error);
                if (values == null)
                {
                    return false;
                }

                int invalidValue = values.FirstOrDefault(value => value > classCount);
                if (invalidValue > 0)
                {
                    error = $"mask class value {invalidValue} is outside background 0 and configured classes 1..{classCount}";
                    return false;
                }

                return true;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is ExternalException || ex is IOException)
            {
                error = ex.Message;
                return false;
            }
        }

        private static bool TryBuildMaskFromSegmentJson(
            string segmentPath,
            Size imageSize,
            IReadOnlyList<UnetClassContractItem> classes,
            out byte[] values,
            out string error)
        {
            values = new byte[imageSize.Width * imageSize.Height];
            error = string.Empty;
            SegmentationAnnotationFile annotation;
            try
            {
                annotation = JsonConvert.DeserializeObject<SegmentationAnnotationFile>(File.ReadAllText(segmentPath));
            }
            catch (Exception ex) when (ex is JsonException || ex is IOException || ex is UnauthorizedAccessException)
            {
                error = "segment JSON is invalid: " + ex.Message;
                return false;
            }

            if (annotation == null || annotation.Polygons == null || annotation.Polygons.Count == 0)
            {
                error = "segment JSON has no polygons";
                return false;
            }
            if ((annotation.ImageWidth > 0 && annotation.ImageWidth != imageSize.Width)
                || (annotation.ImageHeight > 0 && annotation.ImageHeight != imageSize.Height))
            {
                error = "segment JSON image dimensions do not match the source image";
                return false;
            }

            int overlapPixelCount = 0;
            foreach (SegmentationPolygonRecord record in annotation.Polygons)
            {
                if (!TryResolveClassValue(record, classes, out byte classValue, out error))
                {
                    return false;
                }
                if (record?.Points == null || record.Points.Count < 3)
                {
                    error = "segment polygon has fewer than three points";
                    return false;
                }

                byte[] polygonValues = RasterizePolygonRecord(record, imageSize);
                if (!polygonValues.Any(value => value != 0))
                {
                    error = "segment polygon has no pixels inside the source image";
                    return false;
                }

                for (int index = 0; index < values.Length; index++)
                {
                    if (polygonValues[index] == 0)
                    {
                        continue;
                    }
                    if (values[index] != 0 && values[index] != classValue)
                    {
                        overlapPixelCount++;
                    }
                    else
                    {
                        values[index] = classValue;
                    }
                }
            }

            if (overlapPixelCount > 0)
            {
                error = $"different classes overlap in {overlapPixelCount} mask pixels";
                return false;
            }

            return true;
        }

        private static bool TryResolveClassValue(
            SegmentationPolygonRecord record,
            IReadOnlyList<UnetClassContractItem> classes,
            out byte classValue,
            out string error)
        {
            classValue = 0;
            error = string.Empty;
            int classIndex = record?.ClassIndex ?? -1;
            if (classIndex < 0 || classIndex >= classes.Count)
            {
                error = "segment polygon has an invalid class index";
                return false;
            }

            string expectedName = classes[classIndex].Name;
            if (!string.IsNullOrWhiteSpace(record.ClassName)
                && !string.Equals(record.ClassName.Trim(), expectedName, StringComparison.OrdinalIgnoreCase))
            {
                error = $"segment polygon class name '{record.ClassName}' does not match configured class '{expectedName}'";
                return false;
            }

            classValue = (byte)(classIndex + 1);
            return true;
        }

        private static byte[] RasterizePolygonRecord(SegmentationPolygonRecord record, Size imageSize)
        {
            using var bitmap = new Bitmap(imageSize.Width, imageSize.Height, PixelFormat.Format24bppRgb);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (var fillBrush = new SolidBrush(Color.White))
            using (var eraseBrush = new SolidBrush(Color.Black))
            {
                graphics.Clear(Color.Black);
                graphics.FillPolygon(fillBrush, record.Points.Select(point => new Point(point.X, point.Y)).ToArray());
                foreach (List<SegmentationPointRecord> cutout in record.Cutouts ?? new List<List<SegmentationPointRecord>>())
                {
                    if (cutout?.Count >= 3)
                    {
                        graphics.FillPolygon(eraseBrush, cutout.Select(point => new Point(point.X, point.Y)).ToArray());
                    }
                }
            }

            return ReadBitmapValues(bitmap, requireGrayscale: false, out _);
        }

        private static byte[] ReadBitmapValues(Bitmap source, bool requireGrayscale, out string error)
        {
            error = string.Empty;
            Rectangle bounds = new Rectangle(Point.Empty, source.Size);
            using Bitmap normalized = source.Clone(bounds, PixelFormat.Format24bppRgb);
            BitmapData bitmapData = normalized.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            try
            {
                int stride = Math.Abs(bitmapData.Stride);
                var pixels = new byte[stride * normalized.Height];
                Marshal.Copy(bitmapData.Scan0, pixels, 0, pixels.Length);
                var values = new byte[normalized.Width * normalized.Height];
                for (int y = 0; y < normalized.Height; y++)
                {
                    int sourceRow = bitmapData.Stride >= 0 ? y : normalized.Height - 1 - y;
                    int sourceOffset = sourceRow * stride;
                    int targetOffset = y * normalized.Width;
                    for (int x = 0; x < normalized.Width; x++)
                    {
                        int pixelOffset = sourceOffset + (x * 3);
                        byte blue = pixels[pixelOffset];
                        byte green = pixels[pixelOffset + 1];
                        byte red = pixels[pixelOffset + 2];
                        if (requireGrayscale && (blue != green || blue != red))
                        {
                            error = "mask pixels must use identical red, green, and blue class-index values";
                            return null;
                        }
                        values[targetOffset + x] = blue;
                    }
                }

                return values;
            }
            finally
            {
                normalized.UnlockBits(bitmapData);
            }
        }

        private static bool IsEmptyTextFile(string path)
        {
            try
            {
                return File.ReadAllLines(path).All(line => string.IsNullOrWhiteSpace(line));
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static bool TryReuseExistingArtifact(UnetSegmentationDatasetExportResult result, ExportPlan plan)
        {
            if (!Directory.Exists(result.OutputRootPath))
            {
                return false;
            }

            string manifestPath = Path.Combine(result.OutputRootPath, "dataset-manifest.json");
            try
            {
                UnetSegmentationDatasetExportManifest manifest = JsonConvert.DeserializeObject<UnetSegmentationDatasetExportManifest>(File.ReadAllText(manifestPath));
                if (manifest != null
                    && manifest.Version == ManifestVersion
                    && string.Equals(manifest.DatasetFingerprint, result.DatasetFingerprint, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(manifest.SourceDataTreeSha256, result.SourceDataTreeSha256Before, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(manifest.ClassContractSha256, result.ClassContractSha256, StringComparison.OrdinalIgnoreCase))
                {
                    result.ReusedExistingArtifact = true;
                    return true;
                }
            }
            catch (Exception ex) when (ex is IOException || ex is JsonException || ex is UnauthorizedAccessException)
            {
                result.Errors.Add("Existing U-Net artifact cannot be reused: " + ex.Message);
                return false;
            }

            result.Errors.Add("Existing U-Net artifact has a different provenance manifest and will not be overwritten: " + result.OutputRootPath);
            return false;
        }

        private static void MaterializeExport(string temporaryPath, ExportPlan plan, UnetSegmentationDatasetExportResult result)
        {
            foreach (ExportEntry entry in plan.Entries)
            {
                string imagePath = Path.Combine(temporaryPath, "images", entry.Split, entry.RelativeImagePath);
                string maskPath = Path.Combine(temporaryPath, "masks", entry.Split, entry.RelativeMaskPath);
                Directory.CreateDirectory(Path.GetDirectoryName(imagePath) ?? temporaryPath);
                Directory.CreateDirectory(Path.GetDirectoryName(maskPath) ?? temporaryPath);
                File.Copy(entry.SourceImagePath, imagePath, overwrite: false);
                WriteMask(maskPath, entry.ImageWidth, entry.ImageHeight, entry.MaskValues);
                entry.ExportImageSha256 = ComputeFileSha256(imagePath);
                entry.ExportMaskSha256 = ComputeFileSha256(maskPath);
            }
        }

        private static void WriteManifestFiles(string temporaryPath, ExportPlan plan, UnetSegmentationDatasetExportResult result)
        {
            var manifest = new UnetSegmentationDatasetExportManifest
            {
                Version = ManifestVersion,
                DatasetFingerprint = result.DatasetFingerprint,
                SourceRecipeRootPath = plan.RecipeRootPath,
                SourceDataTreeSha256 = result.SourceDataTreeSha256Before,
                ClassContractSha256 = result.ClassContractSha256,
                Classes = plan.Classes,
                Splits = plan.Entries
                    .GroupBy(entry => entry.Split, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(group => Array.IndexOf(DatasetModes, group.Key))
                    .Select(group => new UnetSegmentationDatasetExportManifestSplit
                    {
                        Split = group.Key,
                        Images = group.Select(entry => new UnetSegmentationDatasetExportManifestImage
                        {
                            SourceRelativeImagePath = entry.SourceRelativeImagePath,
                            ImageSha256 = entry.ImageSha256,
                            ImageWidth = entry.ImageWidth,
                            ImageHeight = entry.ImageHeight,
                            ExportImageRelativePath = Path.Combine("images", entry.Split, entry.RelativeImagePath).Replace('\\', '/'),
                            ExportImageSha256 = entry.ExportImageSha256,
                            ExportMaskRelativePath = Path.Combine("masks", entry.Split, entry.RelativeMaskPath).Replace('\\', '/'),
                            ExportMaskSha256 = entry.ExportMaskSha256,
                            HasForeground = entry.HasForeground
                        }).ToList()
                    }).ToList()
            };

            File.WriteAllText(
                Path.Combine(temporaryPath, "classes.json"),
                JsonConvert.SerializeObject(plan.Classes, Formatting.Indented),
                Encoding.UTF8);
            File.WriteAllText(
                Path.Combine(temporaryPath, "dataset-manifest.json"),
                JsonConvert.SerializeObject(manifest, Formatting.Indented),
                Encoding.UTF8);
        }

        private static void WriteMask(string path, int width, int height, byte[] values)
        {
            using var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            Rectangle bounds = new Rectangle(0, 0, width, height);
            BitmapData bitmapData = bitmap.LockBits(bounds, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            try
            {
                int stride = Math.Abs(bitmapData.Stride);
                var pixels = new byte[stride * height];
                for (int y = 0; y < height; y++)
                {
                    int targetRow = bitmapData.Stride >= 0 ? y : height - 1 - y;
                    int targetOffset = targetRow * stride;
                    int sourceOffset = y * width;
                    for (int x = 0; x < width; x++)
                    {
                        byte value = values[sourceOffset + x];
                        int pixelOffset = targetOffset + (x * 3);
                        pixels[pixelOffset] = value;
                        pixels[pixelOffset + 1] = value;
                        pixels[pixelOffset + 2] = value;
                    }
                }
                Marshal.Copy(pixels, 0, bitmapData.Scan0, pixels.Length);
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }
            bitmap.Save(path, ImageFormat.Png);
        }

        private static string ComputeDirectorySha256(string rootPath)
        {
            using SHA256 hash = SHA256.Create();
            foreach (string path in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories)
                .OrderBy(path => Path.GetRelativePath(rootPath, path), StringComparer.OrdinalIgnoreCase))
            {
                string relativePath = Path.GetRelativePath(rootPath, path).Replace('\\', '/');
                byte[] line = Encoding.UTF8.GetBytes(relativePath + "\0" + ComputeFileSha256(path) + "\n");
                hash.TransformBlock(line, 0, line.Length, null, 0);
            }
            hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return Convert.ToHexString(hash.Hash);
        }

        private static string ComputeFileSha256(string path)
        {
            using SHA256 hash = SHA256.Create();
            using FileStream stream = File.OpenRead(path);
            return Convert.ToHexString(hash.ComputeHash(stream));
        }

        private static string ComputeTextSha256(string text)
        {
            using SHA256 hash = SHA256.Create();
            return Convert.ToHexString(hash.ComputeHash(Encoding.UTF8.GetBytes(text ?? string.Empty)));
        }

        private sealed class ExportPlan
        {
            public ExportPlan(string recipeRootPath, string sourceDataRootPath, List<UnetClassContractItem> classes)
            {
                RecipeRootPath = recipeRootPath;
                SourceDataRootPath = sourceDataRootPath;
                Classes = classes;
            }

            public string RecipeRootPath { get; }

            public string SourceDataRootPath { get; }

            public List<UnetClassContractItem> Classes { get; }

            public List<ExportEntry> Entries { get; } = new List<ExportEntry>();
        }

        private sealed class ExportEntry
        {
            public string Split { get; set; } = string.Empty;

            public string SourceImagePath { get; set; } = string.Empty;

            public string SourceRelativeImagePath { get; set; } = string.Empty;

            public string RelativeImagePath { get; set; } = string.Empty;

            public string RelativeMaskPath { get; set; } = string.Empty;

            public string ImageSha256 { get; set; } = string.Empty;

            public string ExportImageSha256 { get; set; } = string.Empty;

            public string ExportMaskSha256 { get; set; } = string.Empty;

            public int ImageWidth { get; set; }

            public int ImageHeight { get; set; }

            public byte[] MaskValues { get; set; } = Array.Empty<byte>();

            public bool HasForeground { get; set; }
        }
    }

    public sealed class UnetClassContractItem
    {
        public int Index { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    public sealed class UnetSegmentationDatasetExportManifest
    {
        public int Version { get; set; }

        public string DatasetFingerprint { get; set; } = string.Empty;

        public string SourceRecipeRootPath { get; set; } = string.Empty;

        public string SourceDataTreeSha256 { get; set; } = string.Empty;

        public string ClassContractSha256 { get; set; } = string.Empty;

        public List<UnetClassContractItem> Classes { get; set; } = new List<UnetClassContractItem>();

        public List<UnetSegmentationDatasetExportManifestSplit> Splits { get; set; } = new List<UnetSegmentationDatasetExportManifestSplit>();
    }

    public sealed class UnetSegmentationDatasetExportManifestSplit
    {
        public string Split { get; set; } = string.Empty;

        public List<UnetSegmentationDatasetExportManifestImage> Images { get; set; } = new List<UnetSegmentationDatasetExportManifestImage>();
    }

    public sealed class UnetSegmentationDatasetExportManifestImage
    {
        public string SourceRelativeImagePath { get; set; } = string.Empty;

        public string ImageSha256 { get; set; } = string.Empty;

        public int ImageWidth { get; set; }

        public int ImageHeight { get; set; }

        public string ExportImageRelativePath { get; set; } = string.Empty;

        public string ExportImageSha256 { get; set; } = string.Empty;

        public string ExportMaskRelativePath { get; set; } = string.Empty;

        public string ExportMaskSha256 { get; set; } = string.Empty;

        public bool HasForeground { get; set; }
    }
}
