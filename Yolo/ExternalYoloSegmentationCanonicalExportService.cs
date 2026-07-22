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
    /// <summary>
    /// Converts an explicitly activated, native YOLO segmentation data.yaml into the
    /// app-owned raster-mask contract consumed by the bundled U-Net worker.  Native
    /// images, labels and data.yaml are read only; all output stays below the recipe.
    /// </summary>
    public static class ExternalYoloSegmentationCanonicalExportService
    {
        private const int ManifestVersion = 1;
        // Keep the app-owned path compact: GDI+ mask writers can fail near the legacy
        // Windows path limit when a long recipe root and a 64-character fingerprint meet.
        private const string ArtifactDirectoryName = "unet-ext";

        public static UnetSegmentationDatasetExportResult Export(CData data)
        {
            return Export(data, data?.ProjectSettings?.ExternalYoloDataset?.DataYamlFilePath);
        }

        public static UnetSegmentationDatasetExportResult Export(CData data, string dataYamlFilePath)
        {
            var result = new UnetSegmentationDatasetExportResult();
            if (data == null || data.ProjectSettings?.DatasetPurpose != LabelingDatasetPurpose.Segmentation)
            {
                result.Errors.Add("External native YOLO canonical export requires a Segmentation recipe.");
                return result;
            }

            string recipeRootPath = data.OutputRootPath?.Trim() ?? string.Empty;
            if (recipeRootPath.Length == 0)
            {
                result.Errors.Add("External native YOLO canonical export requires an app recipe output folder.");
                return result;
            }

            YoloExternalDatasetSourcePacket packet = YoloExternalDatasetIntakeService.ReadValidatedSourcePacket(
                dataYamlFilePath,
                LabelingDatasetPurpose.Segmentation);
            if (!packet.IsReady)
            {
                result.Errors.AddRange(packet.Report?.Errors ?? Array.Empty<string>());
                return result;
            }

            ExternalExportPlan plan = BuildPlan(packet, result);
            if (plan == null || result.Errors.Count > 0)
            {
                return result;
            }

            result.SourceDataTreeSha256Before = packet.Report.SourceFingerprintSha256;
            result.ClassContractSha256 = ComputeTextSha256(JsonConvert.SerializeObject(plan.Classes));
            result.DatasetFingerprint = ComputeTextSha256(
                    "external-native-yolo-segmentation-v1\n"
                    + result.SourceDataTreeSha256Before + "\n"
                    + result.ClassContractSha256)
                .ToLowerInvariant();
            result.OutputRootPath = Path.Combine(
                recipeRootPath,
                "artifacts",
                ArtifactDirectoryName,
                result.DatasetFingerprint.Substring(0, 24));

            if (TryReuseExistingArtifact(result))
            {
                SetSourceFingerprintAfter(dataYamlFilePath, result);
                return result;
            }

            string artifactParentPath = Path.GetDirectoryName(result.OutputRootPath) ?? string.Empty;
            string temporaryPath = Path.Combine(
                artifactParentPath,
                ".tmp-" + result.DatasetFingerprint.Substring(0, 16) + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            try
            {
                Directory.CreateDirectory(temporaryPath);
                MaterializeExport(temporaryPath, plan);
                WriteManifestFiles(temporaryPath, plan, result);
                Directory.Move(temporaryPath, result.OutputRootPath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is ExternalException)
            {
                result.Errors.Add("External native YOLO canonical export failed: " + ex.Message);
            }
            finally
            {
                if (Directory.Exists(temporaryPath))
                {
                    Directory.Delete(temporaryPath, recursive: true);
                }
            }

            SetSourceFingerprintAfter(dataYamlFilePath, result);
            return result;
        }

        private static ExternalExportPlan BuildPlan(
            YoloExternalDatasetSourcePacket packet,
            UnetSegmentationDatasetExportResult result)
        {
            List<UnetClassContractItem> classes = BuildClassContract(packet.Report.ClassNames, result.Errors);
            if (result.Errors.Count > 0)
            {
                return null;
            }

            var plan = new ExternalExportPlan(packet.Report, classes);
            var splitByContentHash = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (YoloExternalDatasetSourceEntry sourceEntry in packet.Entries)
            {
                string split = NormalizeSplit(sourceEntry.Split);
                if (split.Length == 0)
                {
                    result.Errors.Add("External native YOLO canonical export received an unsupported split: " + sourceEntry.Split);
                    continue;
                }

                if (!TryBuildEntry(plan, split, sourceEntry, out ExternalExportEntry entry, out string error))
                {
                    result.Errors.Add(error);
                    continue;
                }

                if (splitByContentHash.TryGetValue(entry.ImageSha256, out string existingSplit))
                {
                    result.Errors.Add($"External native YOLO canonical export rejects duplicate image content across splits: {existingSplit} and {split} share {Path.GetFileName(sourceEntry.ImagePath)}.");
                    continue;
                }

                splitByContentHash.Add(entry.ImageSha256, split);
                plan.Entries.Add(entry);
                UnetSegmentationDatasetExportSplitSummary splitSummary = GetOrCreateSummary(result, split);
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

            foreach (UnetSegmentationDatasetExportSplitSummary split in result.Splits)
            {
                if (split.ImageCount > 0 && split.PositiveMaskImageCount == 0)
                {
                    result.Errors.Add($"External native YOLO canonical export needs at least one positive mask in the {split.Split} split.");
                }
            }

            if (!plan.Entries.Any(entry => string.Equals(entry.Split, "train", StringComparison.OrdinalIgnoreCase)))
            {
                result.Errors.Add("External native YOLO canonical export needs at least one train image.");
            }
            if (!plan.Entries.Any(entry => string.Equals(entry.Split, "valid", StringComparison.OrdinalIgnoreCase)))
            {
                result.Errors.Add("External native YOLO canonical export needs at least one val image.");
            }

            return result.Errors.Count == 0 ? plan : null;
        }

        private static List<UnetClassContractItem> BuildClassContract(
            IReadOnlyList<string> classNames,
            List<string> errors)
        {
            var classes = new List<UnetClassContractItem>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string sourceName in classNames ?? Array.Empty<string>())
            {
                string name = sourceName?.Trim() ?? string.Empty;
                if (name.Length == 0)
                {
                    errors.Add("External native YOLO canonical export needs non-empty segmentation class names.");
                    continue;
                }
                if (!names.Add(name))
                {
                    errors.Add("External native YOLO canonical export needs unique segmentation class names: " + name);
                    continue;
                }

                classes.Add(new UnetClassContractItem { Index = classes.Count + 1, Name = name });
            }

            if (classes.Count == 0)
            {
                errors.Add("External native YOLO canonical export needs at least one segmentation class.");
            }
            else if (classes.Count > 254)
            {
                errors.Add("External native YOLO canonical export supports at most 254 foreground classes because mask value 0 is reserved for background.");
            }

            return classes;
        }

        private static bool TryBuildEntry(
            ExternalExportPlan plan,
            string split,
            YoloExternalDatasetSourceEntry sourceEntry,
            out ExternalExportEntry entry,
            out string error)
        {
            entry = null;
            error = string.Empty;
            Size imageSize;
            try
            {
                using var image = new Bitmap(sourceEntry.ImagePath);
                imageSize = image.Size;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is ExternalException || ex is IOException)
            {
                error = "External native YOLO canonical export could not read source image " + sourceEntry.ImagePath + ": " + ex.Message;
                return false;
            }

            if (!TryBuildMaskFromNativeYoloPolygons(
                    sourceEntry.LabelPath,
                    imageSize,
                    plan.Classes.Count,
                    out byte[] maskValues,
                    out error))
            {
                error = "External native YOLO canonical export cannot rasterize " + sourceEntry.LabelPath + ": " + error;
                return false;
            }

            string imageSha256 = ComputeFileSha256(sourceEntry.ImagePath);
            string extension = Path.GetExtension(sourceEntry.ImagePath).ToLowerInvariant();
            string artifactName = imageSha256.Substring(0, Math.Min(24, imageSha256.Length)) + extension;
            entry = new ExternalExportEntry
            {
                Split = split,
                SourceImagePath = sourceEntry.ImagePath,
                SourceRelativeImagePath = BuildSourceRelativePath(plan.Report.DatasetRootPath, sourceEntry.ImagePath),
                RelativeImagePath = artifactName,
                RelativeMaskPath = Path.ChangeExtension(artifactName, ".png") ?? string.Empty,
                ImageSha256 = imageSha256,
                ImageWidth = imageSize.Width,
                ImageHeight = imageSize.Height,
                MaskValues = maskValues,
                HasForeground = maskValues.Any(value => value != 0)
            };
            return true;
        }

        private static bool TryBuildMaskFromNativeYoloPolygons(
            string labelPath,
            Size imageSize,
            int classCount,
            out byte[] values,
            out string error)
        {
            values = new byte[imageSize.Width * imageSize.Height];
            error = string.Empty;
            if (!File.Exists(labelPath))
            {
                return true;
            }

            string[] lines;
            try
            {
                lines = File.ReadAllLines(labelPath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                error = ex.Message;
                return false;
            }

            int overlapPixelCount = 0;
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string[] tokens = (lines[lineIndex] ?? string.Empty)
                    .Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length == 0)
                {
                    continue;
                }

                if (tokens.Length < 7 || (tokens.Length - 1) % 2 != 0
                    || !int.TryParse(tokens[0], out int classIndex)
                    || classIndex < 0 || classIndex >= classCount)
                {
                    error = $"line {lineIndex + 1} is not a valid native YOLO segmentation polygon.";
                    return false;
                }

                var points = new PointF[(tokens.Length - 1) / 2];
                for (int tokenIndex = 1; tokenIndex < tokens.Length; tokenIndex += 2)
                {
                    if (!double.TryParse(tokens[tokenIndex], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double normalizedX)
                        || !double.TryParse(tokens[tokenIndex + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double normalizedY)
                        || normalizedX < 0D || normalizedX > 1D || normalizedY < 0D || normalizedY > 1D)
                    {
                        error = $"line {lineIndex + 1} contains an invalid normalized polygon coordinate.";
                        return false;
                    }

                    points[(tokenIndex - 1) / 2] = new PointF(
                        (float)(normalizedX * Math.Max(0, imageSize.Width - 1)),
                        (float)(normalizedY * Math.Max(0, imageSize.Height - 1)));
                }

                byte[] polygonValues = RasterizePolygon(points, imageSize);
                if (!polygonValues.Any(value => value != 0))
                {
                    error = $"line {lineIndex + 1} has no pixels inside the source image.";
                    return false;
                }

                byte classValue = (byte)(classIndex + 1);
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

        private static byte[] RasterizePolygon(PointF[] points, Size imageSize)
        {
            using var bitmap = new Bitmap(imageSize.Width, imageSize.Height, PixelFormat.Format24bppRgb);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (var brush = new SolidBrush(Color.White))
            {
                graphics.Clear(Color.Black);
                graphics.FillPolygon(brush, points);
            }

            return ReadBitmapValues(bitmap);
        }

        private static byte[] ReadBitmapValues(Bitmap source)
        {
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
                        values[targetOffset + x] = pixels[sourceOffset + (x * 3)];
                    }
                }
                return values;
            }
            finally
            {
                normalized.UnlockBits(bitmapData);
            }
        }

        private static void MaterializeExport(string temporaryPath, ExternalExportPlan plan)
        {
            foreach (ExternalExportEntry entry in plan.Entries)
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

        private static void WriteManifestFiles(
            string temporaryPath,
            ExternalExportPlan plan,
            UnetSegmentationDatasetExportResult result)
        {
            var manifest = new UnetSegmentationDatasetExportManifest
            {
                Version = ManifestVersion,
                DatasetFingerprint = result.DatasetFingerprint,
                SourceRecipeRootPath = plan.Report.DataYamlFilePath,
                SourceDataTreeSha256 = result.SourceDataTreeSha256Before,
                ClassContractSha256 = result.ClassContractSha256,
                Classes = plan.Classes,
                Splits = plan.Entries
                    .GroupBy(entry => entry.Split, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(group => SplitSortOrder(group.Key))
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

        private static bool TryReuseExistingArtifact(UnetSegmentationDatasetExportResult result)
        {
            if (!Directory.Exists(result.OutputRootPath))
            {
                return false;
            }

            try
            {
                string manifestPath = Path.Combine(result.OutputRootPath, "dataset-manifest.json");
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
                result.Errors.Add("Existing external native YOLO canonical artifact cannot be reused: " + ex.Message);
                return false;
            }

            result.Errors.Add("Existing external native YOLO canonical artifact has a different provenance manifest and will not be overwritten: " + result.OutputRootPath);
            return false;
        }

        private static void SetSourceFingerprintAfter(string dataYamlFilePath, UnetSegmentationDatasetExportResult result)
        {
            YoloExternalDatasetIntakeReport afterReport = YoloExternalDatasetIntakeService.Build(
                dataYamlFilePath,
                LabelingDatasetPurpose.Segmentation);
            result.SourceDataTreeSha256After = afterReport.SourceFingerprintSha256;
            if (!afterReport.IsReady)
            {
                result.Errors.Add("External native YOLO source could not be revalidated after canonical export: " + string.Join(" / ", afterReport.Errors));
            }
            else if (!string.Equals(result.SourceDataTreeSha256Before, result.SourceDataTreeSha256After, StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add("External native YOLO source changed while the canonical export was being created; the artifact is not usable.");
            }
        }

        private static UnetSegmentationDatasetExportSplitSummary GetOrCreateSummary(
            UnetSegmentationDatasetExportResult result,
            string split)
        {
            UnetSegmentationDatasetExportSplitSummary existing = result.Splits
                .FirstOrDefault(summary => string.Equals(summary.Split, split, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                return existing;
            }

            var summary = new UnetSegmentationDatasetExportSplitSummary(split);
            result.Splits.Add(summary);
            return summary;
        }

        private static string NormalizeSplit(string split)
        {
            if (string.Equals(split, "train", StringComparison.OrdinalIgnoreCase))
            {
                return "train";
            }
            if (string.Equals(split, "val", StringComparison.OrdinalIgnoreCase)
                || string.Equals(split, "valid", StringComparison.OrdinalIgnoreCase))
            {
                return "valid";
            }
            return string.Equals(split, "test", StringComparison.OrdinalIgnoreCase) ? "test" : string.Empty;
        }

        private static int SplitSortOrder(string split)
            => string.Equals(split, "train", StringComparison.OrdinalIgnoreCase) ? 0
                : string.Equals(split, "valid", StringComparison.OrdinalIgnoreCase) ? 1
                : 2;

        private static string BuildSourceRelativePath(string datasetRootPath, string imagePath)
        {
            try
            {
                return Path.GetRelativePath(datasetRootPath, imagePath).Replace('\\', '/');
            }
            catch (ArgumentException)
            {
                return imagePath.Replace('\\', '/');
            }
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

        private sealed class ExternalExportPlan
        {
            public ExternalExportPlan(YoloExternalDatasetIntakeReport report, List<UnetClassContractItem> classes)
            {
                Report = report;
                Classes = classes;
            }

            public YoloExternalDatasetIntakeReport Report { get; }

            public List<UnetClassContractItem> Classes { get; }

            public List<ExternalExportEntry> Entries { get; } = new List<ExternalExportEntry>();
        }

        private sealed class ExternalExportEntry
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
}
