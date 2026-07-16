using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace MvcVisionSystem.Yolo
{
    public sealed class YoloExternalEvaluationDataAuditReport
    {
        public YoloExternalEvaluationDataAuditReport(
            string externalDirectory,
            int referenceImageCount,
            int externalImageCount,
            int nameOverlapCount,
            int contentOverlapCount,
            string overlapExample,
            IEnumerable<string> errors)
        {
            ExternalDirectory = externalDirectory ?? string.Empty;
            ReferenceImageCount = referenceImageCount;
            ExternalImageCount = externalImageCount;
            NameOverlapCount = nameOverlapCount;
            ContentOverlapCount = contentOverlapCount;
            OverlapExample = overlapExample ?? string.Empty;
            Errors = (errors ?? Enumerable.Empty<string>()).ToList();
        }

        public string ExternalDirectory { get; }
        public int ReferenceImageCount { get; }
        public int ExternalImageCount { get; }
        public int NameOverlapCount { get; }
        public int ContentOverlapCount { get; }
        public string OverlapExample { get; }
        public IReadOnlyList<string> Errors { get; }
        public bool HasErrors => Errors.Count > 0;
        public bool HasExternalImages => ExternalImageCount > 0;
        public bool HasContentOverlap => ContentOverlapCount > 0;
        public bool IsIndependentByContent => !HasErrors && HasExternalImages && !HasContentOverlap;
    }

    public static class YoloExternalEvaluationDataAuditService
    {
        private static readonly HashSet<string> ImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff"
        };

        public static YoloExternalEvaluationDataAuditReport Build(
            IEnumerable<string> referenceDirectories,
            string externalDirectory)
        {
            var errors = new List<string>();
            List<string> referenceImages = EnumerateReferenceImages(referenceDirectories, errors);
            List<string> externalImages = EnumerateExternalImages(externalDirectory, errors);
            if (referenceImages.Count == 0 && errors.Count == 0)
            {
                errors.Add("The current dataset has no supported train/valid/test images to compare.");
            }

            if (externalImages.Count == 0 || referenceImages.Count == 0)
            {
                return new YoloExternalEvaluationDataAuditReport(
                    externalDirectory,
                    referenceImages.Count,
                    externalImages.Count,
                    0,
                    0,
                    string.Empty,
                    errors);
            }

            var referenceNames = new HashSet<string>(
                referenceImages.Select(Path.GetFileName).Where(name => !string.IsNullOrWhiteSpace(name)),
                StringComparer.OrdinalIgnoreCase);
            int nameOverlapCount = externalImages.Count(path => referenceNames.Contains(Path.GetFileName(path)));

            Dictionary<string, string> referenceContent = BuildContentMap(referenceImages, errors);
            int contentOverlapCount = 0;
            string overlapExample = string.Empty;
            foreach (string externalImage in externalImages)
            {
                if (!TryBuildContentKey(externalImage, out string contentKey, out string error))
                {
                    errors.Add(error);
                    continue;
                }

                if (!referenceContent.TryGetValue(contentKey, out string referenceImage))
                {
                    continue;
                }

                contentOverlapCount++;
                if (string.IsNullOrWhiteSpace(overlapExample))
                {
                    overlapExample = $"{Path.GetFileName(referenceImage)} == {Path.GetFileName(externalImage)}";
                }
            }

            return new YoloExternalEvaluationDataAuditReport(
                externalDirectory,
                referenceImages.Count,
                externalImages.Count,
                nameOverlapCount,
                contentOverlapCount,
                overlapExample,
                errors);
        }

        private static List<string> EnumerateReferenceImages(IEnumerable<string> directories, List<string> errors)
        {
            var images = new List<string>();
            foreach (string directory in (directories ?? Enumerable.Empty<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!Directory.Exists(directory))
                {
                    continue;
                }

                images.AddRange(EnumerateSupportedImages(directory, errors));
            }

            return images;
        }

        private static List<string> EnumerateExternalImages(string directory, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                errors.Add("The external evaluation folder does not exist.");
                return new List<string>();
            }

            return EnumerateSupportedImages(directory, errors);
        }

        private static List<string> EnumerateSupportedImages(string directory, List<string> errors)
        {
            var images = new List<string>();
            try
            {
                foreach (string path in Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories))
                {
                    if (ImageExtensions.Contains(Path.GetExtension(path)))
                    {
                        images.Add(path);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                errors.Add($"Cannot read image folder '{directory}': {ex.Message}");
            }

            return images;
        }

        private static Dictionary<string, string> BuildContentMap(IEnumerable<string> imagePaths, List<string> errors)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string imagePath in imagePaths ?? Enumerable.Empty<string>())
            {
                if (!TryBuildContentKey(imagePath, out string contentKey, out string error))
                {
                    errors.Add(error);
                    continue;
                }

                if (!map.ContainsKey(contentKey))
                {
                    map[contentKey] = imagePath;
                }
            }

            return map;
        }

        private static bool TryBuildContentKey(string path, out string contentKey, out string error)
        {
            contentKey = string.Empty;
            error = string.Empty;
            try
            {
                var info = new FileInfo(path);
                using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using SHA256 sha = SHA256.Create();
                contentKey = $"{info.Length}:{Convert.ToBase64String(sha.ComputeHash(stream))}";
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                error = $"Cannot read image '{Path.GetFileName(path)}': {ex.Message}";
                return false;
            }
        }
    }
}
