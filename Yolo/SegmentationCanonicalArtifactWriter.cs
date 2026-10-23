using MvcVisionSystem;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace MvcVisionSystem.Yolo
{
    internal static class SegmentationCanonicalArtifactWriter
    {
        internal const int ManifestVersion = 1;

        private static readonly string[] CanonicalSplits =
        {
            YoloDatasetSplitService.TrainMode,
            YoloDatasetSplitService.ValidMode,
            YoloDatasetSplitService.TestMode
        };

        internal static void Write(
            string temporaryPath,
            string sourceProvenancePath,
            IReadOnlyList<UnetClassContractItem> classes,
            IReadOnlyList<SegmentationCanonicalArtifactItem> items,
            UnetSegmentationDatasetExportResult result)
        {
            foreach (SegmentationCanonicalArtifactItem item in items)
            {
                string imagePath = Path.Combine(temporaryPath, "images", item.Split, item.RelativeImagePath);
                string maskPath = Path.Combine(temporaryPath, "masks", item.Split, item.RelativeMaskPath);
                Directory.CreateDirectory(Path.GetDirectoryName(imagePath) ?? temporaryPath);
                Directory.CreateDirectory(Path.GetDirectoryName(maskPath) ?? temporaryPath);
                File.Copy(item.SourceImagePath, imagePath, overwrite: false);
                WriteMask(maskPath, item.ImageWidth, item.ImageHeight, item.MaskValues);
                item.ExportImageSha256 = HashingService.ComputeFileSha256(imagePath);
                item.ExportMaskSha256 = HashingService.ComputeFileSha256(maskPath);
            }

            var manifest = new UnetSegmentationDatasetExportManifest
            {
                Version = ManifestVersion,
                DatasetFingerprint = result.DatasetFingerprint,
                SourceRecipeRootPath = sourceProvenancePath,
                SourceDataTreeSha256 = result.SourceDataTreeSha256Before,
                ClassContractSha256 = result.ClassContractSha256,
                Classes = classes.ToList(),
                Splits = items
                    .GroupBy(item => item.Split, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(group => Array.IndexOf(CanonicalSplits, group.Key))
                    .Select(group => new UnetSegmentationDatasetExportManifestSplit
                    {
                        Split = group.Key,
                        Images = group.Select(item => new UnetSegmentationDatasetExportManifestImage
                        {
                            SourceRelativeImagePath = item.SourceRelativeImagePath,
                            ImageSha256 = item.ImageSha256,
                            ImageWidth = item.ImageWidth,
                            ImageHeight = item.ImageHeight,
                            ExportImageRelativePath = Path.Combine("images", item.Split, item.RelativeImagePath).Replace('\\', '/'),
                            ExportImageSha256 = item.ExportImageSha256,
                            ExportMaskRelativePath = Path.Combine("masks", item.Split, item.RelativeMaskPath).Replace('\\', '/'),
                            ExportMaskSha256 = item.ExportMaskSha256,
                            HasForeground = item.HasForeground
                        }).ToList()
                    }).ToList()
            };

            File.WriteAllText(
                Path.Combine(temporaryPath, "classes.json"),
                JsonConvert.SerializeObject(classes, Formatting.Indented),
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
            using var output = new MemoryStream();
            bitmap.Save(output, ImageFormat.Png);
            File.WriteAllBytes(path, output.ToArray());
        }
    }

    internal sealed class SegmentationCanonicalArtifactItem
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
