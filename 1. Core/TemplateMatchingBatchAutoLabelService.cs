using MvcVisionSystem.DrawObject;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;

namespace MvcVisionSystem._1._Core
{
    public sealed class TemplateMatchingBatchAutoLabelItemResult
    {
        public string ImagePath { get; set; } = string.Empty;
        public bool Saved { get; set; }
        public bool NoCandidate { get; set; }
        public string Message { get; set; } = string.Empty;
        public int CandidateCount { get; set; }
        public Size ImageSize { get; set; } = Size.Empty;
        public TimeSpan Elapsed { get; set; }

        public static TemplateMatchingBatchAutoLabelItemResult CreateSaved(string imagePath, int candidateCount, TimeSpan elapsed, Size imageSize)
        {
            return new TemplateMatchingBatchAutoLabelItemResult
            {
                ImagePath = imagePath ?? string.Empty,
                Saved = true,
                CandidateCount = Math.Max(0, candidateCount),
                ImageSize = imageSize,
                Elapsed = elapsed
            };
        }

        public static TemplateMatchingBatchAutoLabelItemResult NoCandidates(string imagePath, TimeSpan elapsed, Size imageSize)
        {
            return new TemplateMatchingBatchAutoLabelItemResult
            {
                ImagePath = imagePath ?? string.Empty,
                NoCandidate = true,
                Message = "no candidate",
                ImageSize = imageSize,
                Elapsed = elapsed
            };
        }

        public static TemplateMatchingBatchAutoLabelItemResult Failed(string imagePath, string message, TimeSpan elapsed, Size imageSize = default)
        {
            return new TemplateMatchingBatchAutoLabelItemResult
            {
                ImagePath = imagePath ?? string.Empty,
                Message = string.IsNullOrWhiteSpace(message) ? "failed" : message,
                ImageSize = imageSize,
                Elapsed = elapsed
            };
        }

        public static TemplateMatchingBatchAutoLabelItemResult Canceled(string imagePath, TimeSpan elapsed)
        {
            return Failed(imagePath, "canceled", elapsed);
        }
    }

    public sealed class TemplateMatchingBatchAutoLabelService
    {
        private readonly TemplateMatchingAutoLabelService templateMatchingAutoLabelService;

        public TemplateMatchingBatchAutoLabelService()
            : this(new TemplateMatchingAutoLabelService())
        {
        }

        public TemplateMatchingBatchAutoLabelService(TemplateMatchingAutoLabelService templateMatchingAutoLabelService)
        {
            this.templateMatchingAutoLabelService = templateMatchingAutoLabelService ?? new TemplateMatchingAutoLabelService();
        }

        public IReadOnlyList<string> BuildUnlabeledImagePathQueue(
            IEnumerable<string> imagePaths,
            CData data,
            string activeImagePath)
        {
            return (imagePaths ?? Array.Empty<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                .GroupBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Where(path => !PathsEqual(path, activeImagePath))
                .Where(path => !HasExistingLabelFile(path, data))
                .ToList();
        }

        public bool HasExistingLabelFile(string imagePath, CData data)
        {
            return YoloAnnotationService
                .GetCandidateLabelPaths(imagePath, data)
                .Any(File.Exists);
        }

        public TemplateMatchingBatchAutoLabelItemResult MatchAndSaveImage(
            string imagePath,
            Bitmap templateImage,
            CClassItem classItem,
            string className,
            CData data,
            TemplateMatchingAutoLabelOptions options,
            CancellationToken token)
        {
            var stopwatch = Stopwatch.StartNew();
            if (token.IsCancellationRequested)
            {
                return TemplateMatchingBatchAutoLabelItemResult.Canceled(imagePath, stopwatch.Elapsed);
            }

            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                return TemplateMatchingBatchAutoLabelItemResult.Failed(imagePath, "image file not found", stopwatch.Elapsed);
            }

            if (data == null)
            {
                return TemplateMatchingBatchAutoLabelItemResult.Failed(imagePath, "dataset is not ready", stopwatch.Elapsed);
            }

            if (HasExistingLabelFile(imagePath, data))
            {
                return TemplateMatchingBatchAutoLabelItemResult.Failed(imagePath, "label file already exists", stopwatch.Elapsed);
            }

            try
            {
                using Bitmap sourceImage = new Bitmap(imagePath);
                TemplateMatchingAutoLabelResult result = templateMatchingAutoLabelService.MatchImageWithTemplate(
                    sourceImage,
                    templateImage,
                    className,
                    options ?? new TemplateMatchingAutoLabelOptions { ExcludeSourceRegion = false });

                if (!result.Succeeded)
                {
                    return TemplateMatchingBatchAutoLabelItemResult.Failed(imagePath, result.Message, stopwatch.Elapsed, sourceImage.Size);
                }

                IReadOnlyDictionary<string, List<CRectangleObject>> roisByClass = BuildRoisByClass(
                    classItem,
                    className,
                    result.Candidates);
                int objectCount = roisByClass.Values.Sum(list => list?.Count ?? 0);
                if (objectCount == 0)
                {
                    return TemplateMatchingBatchAutoLabelItemResult.NoCandidates(imagePath, stopwatch.Elapsed, sourceImage.Size);
                }

                YoloAnnotationService.SaveAnnotations(
                    Path.GetFileName(imagePath),
                    sourceImage,
                    roisByClass,
                    data.ClassNamedList,
                    data,
                    sourceImagePath: imagePath);
                return TemplateMatchingBatchAutoLabelItemResult.CreateSaved(imagePath, objectCount, stopwatch.Elapsed, sourceImage.Size);
            }
            catch (Exception ex)
            {
                return TemplateMatchingBatchAutoLabelItemResult.Failed(imagePath, ex.Message, stopwatch.Elapsed);
            }
        }

        private static IReadOnlyDictionary<string, List<CRectangleObject>> BuildRoisByClass(
            CClassItem classItem,
            string className,
            IReadOnlyList<YoloWorkerSmokeCandidate> candidates)
        {
            string normalizedClassName = classItem?.Text;
            if (string.IsNullOrWhiteSpace(normalizedClassName))
            {
                normalizedClassName = string.IsNullOrWhiteSpace(className) ? "Defect" : className.Trim();
            }

            var rois = new List<CRectangleObject>();
            foreach (YoloWorkerSmokeCandidate candidate in candidates ?? Array.Empty<YoloWorkerSmokeCandidate>())
            {
                Rectangle bounds = candidate.ToRectangle();
                if (bounds.IsEmpty)
                {
                    continue;
                }

                rois.Add(new CRectangleObject
                {
                    Roi = bounds,
                    cClassItem = classItem ?? new CClassItem { Text = normalizedClassName }
                });
            }

            return new Dictionary<string, List<CRectangleObject>>(StringComparer.OrdinalIgnoreCase)
            {
                [normalizedClassName] = rois
            };
        }

        private static bool PathsEqual(string left, string right)
        {
            return !string.IsNullOrWhiteSpace(left)
                && !string.IsNullOrWhiteSpace(right)
                && string.Equals(
                    Path.GetFullPath(left),
                    Path.GetFullPath(right),
                    StringComparison.OrdinalIgnoreCase);
        }
    }
}
