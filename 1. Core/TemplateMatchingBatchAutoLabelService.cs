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
            if (data?.ProjectSettings?.DatasetPurpose == LabelingDatasetPurpose.Segmentation)
            {
                return YoloSegmentationAnnotationService
                    .GetCandidateSegmentPaths(imagePath, data)
                    .Any(File.Exists);
            }

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
            CancellationToken token,
            Rectangle? sourceSegmentBounds = null,
            IReadOnlyList<Point> sourceSegmentPoints = null,
            IReadOnlyList<IReadOnlyList<Point>> sourceSegmentCutouts = null,
            byte[] sourceMaskData = null,
            Size sourceMaskSize = default,
            Rectangle sourceMaskBounds = default)
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

                string imageName = Path.GetFileName(imagePath);
                if (data.ProjectSettings?.DatasetPurpose == LabelingDatasetPurpose.Segmentation)
                {
                    YoloAnnotationService.SaveAnnotations(
                        imageName,
                        sourceImage,
                        new Dictionary<string, List<CRectangleObject>>(StringComparer.OrdinalIgnoreCase),
                        data.ClassNamedList,
                        data,
                        sourceImagePath: imagePath);
                    YoloSegmentationAnnotationService.SaveSegmentationAnnotations(
                        imageName,
                        sourceImage,
                        BuildSegmentsByClass(
                            classItem,
                            className,
                            result.Candidates,
                            sourceImage.Size,
                            sourceSegmentBounds,
                            sourceSegmentPoints,
                            sourceSegmentCutouts,
                            sourceMaskData,
                            sourceMaskSize,
                            sourceMaskBounds),
                        data.ClassNamedList,
                        data);
                }
                else
                {
                    YoloAnnotationService.SaveAnnotations(
                        imageName,
                        sourceImage,
                        roisByClass,
                        data.ClassNamedList,
                        data,
                        sourceImagePath: imagePath);
                }

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

        internal static IReadOnlyDictionary<string, List<LabelingSegmentationObject>> BuildSegmentsByClass(
            CClassItem classItem,
            string className,
            IReadOnlyList<YoloWorkerSmokeCandidate> candidates,
            Size imageSize,
            Rectangle? sourceSegmentBounds = null,
            IReadOnlyList<Point> sourceSegmentPoints = null,
            IReadOnlyList<IReadOnlyList<Point>> sourceSegmentCutouts = null,
            byte[] sourceMaskData = null,
            Size sourceMaskSize = default,
            Rectangle sourceMaskBounds = default)
        {
            string normalizedClassName = classItem?.Text;
            if (string.IsNullOrWhiteSpace(normalizedClassName))
            {
                normalizedClassName = string.IsNullOrWhiteSpace(className) ? "Defect" : className.Trim();
            }

            var segments = new List<LabelingSegmentationObject>();
            foreach (YoloWorkerSmokeCandidate candidate in candidates ?? Array.Empty<YoloWorkerSmokeCandidate>())
            {
                Rectangle bounds = candidate.ToRectangle();
                if (bounds.IsEmpty)
                {
                    continue;
                }

                IReadOnlyList<LabelingSegmentationObject> maskSegments = BuildTranslatedSourceMaskSegments(
                    classItem,
                    normalizedClassName,
                    bounds,
                    imageSize,
                    sourceMaskData,
                    sourceMaskSize,
                    sourceMaskBounds);
                if (maskSegments.Count > 0)
                {
                    segments.AddRange(maskSegments);
                    continue;
                }

                LabelingSegmentationObject segment = BuildTranslatedSourceSegment(
                    classItem,
                    normalizedClassName,
                    bounds,
                    imageSize,
                    sourceSegmentBounds,
                    sourceSegmentPoints,
                    sourceSegmentCutouts);
                if (segment == null)
                {
                    List<Point> points = SegmentationGeometry.RectangleToPolygon(bounds, imageSize);
                    if (points.Count < 3)
                    {
                        continue;
                    }

                    segment = new LabelingSegmentationObject(points, classItem ?? new CClassItem { Text = normalizedClassName })
                    {
                        ClassName = normalizedClassName
                    };
                }

                segments.Add(segment);
            }

            return new Dictionary<string, List<LabelingSegmentationObject>>(StringComparer.OrdinalIgnoreCase)
            {
                [normalizedClassName] = segments
            };
        }

        // Shared by the approved historical migration so it uses the same contour transfer as new template labeling.
        internal static IReadOnlyList<LabelingSegmentationObject> BuildTranslatedSourceMaskSegments(
            CClassItem classItem,
            string className,
            Rectangle targetBounds,
            Size imageSize,
            byte[] sourceMaskData,
            Size sourceMaskSize,
            Rectangle sourceMaskBounds)
        {
            if (sourceMaskData == null
                || sourceMaskSize.Width <= 0
                || sourceMaskSize.Height <= 0
                || sourceMaskData.Length != sourceMaskSize.Width * sourceMaskSize.Height
                || sourceMaskBounds.IsEmpty
                || targetBounds.IsEmpty
                || imageSize.Width <= 0
                || imageSize.Height <= 0)
            {
                return Array.Empty<LabelingSegmentationObject>();
            }

            Rectangle clippedTargetBounds = Rectangle.Intersect(targetBounds, new Rectangle(Point.Empty, imageSize));
            if (clippedTargetBounds.Width <= 1 || clippedTargetBounds.Height <= 1)
            {
                return Array.Empty<LabelingSegmentationObject>();
            }

            byte[] targetMask = new byte[imageSize.Width * imageSize.Height];
            double sourceSpanX = Math.Max(1, sourceMaskBounds.Width - 1);
            double sourceSpanY = Math.Max(1, sourceMaskBounds.Height - 1);
            double targetSpanX = Math.Max(1, targetBounds.Width - 1);
            double targetSpanY = Math.Max(1, targetBounds.Height - 1);
            int paintedPixelCount = 0;

            for (int y = clippedTargetBounds.Top; y < clippedTargetBounds.Bottom; y++)
            {
                int sourceY = sourceMaskBounds.Top + (int)Math.Round(((y - targetBounds.Top) / targetSpanY) * sourceSpanY);
                if (sourceY < 0 || sourceY >= sourceMaskSize.Height)
                {
                    continue;
                }

                for (int x = clippedTargetBounds.Left; x < clippedTargetBounds.Right; x++)
                {
                    int sourceX = sourceMaskBounds.Left + (int)Math.Round(((x - targetBounds.Left) / targetSpanX) * sourceSpanX);
                    if (sourceX < 0 || sourceX >= sourceMaskSize.Width)
                    {
                        continue;
                    }

                    if (sourceMaskData[(sourceY * sourceMaskSize.Width) + sourceX] == 0)
                    {
                        continue;
                    }

                    targetMask[(y * imageSize.Width) + x] = 1;
                    paintedPixelCount++;
                }
            }

            if (paintedPixelCount == 0)
            {
                return Array.Empty<LabelingSegmentationObject>();
            }

            Rectangle targetMaskBounds = SegmentationGeometry.GetMaskBounds(targetMask, imageSize);
            return targetMaskBounds.IsEmpty
                ? Array.Empty<LabelingSegmentationObject>()
                : new[]
                {
                    new LabelingSegmentationObject(Array.Empty<Point>(), classItem ?? new CClassItem { Text = className })
                    {
                        ClassName = className,
                        MaskData = targetMask,
                        MaskSize = imageSize,
                        MaskBounds = targetMaskBounds
                    }
                };
        }

        private static LabelingSegmentationObject BuildTranslatedSourceSegment(
            CClassItem classItem,
            string className,
            Rectangle targetBounds,
            Size imageSize,
            Rectangle? sourceSegmentBounds,
            IReadOnlyList<Point> sourceSegmentPoints,
            IReadOnlyList<IReadOnlyList<Point>> sourceSegmentCutouts)
        {
            if (!sourceSegmentBounds.HasValue
                || sourceSegmentBounds.Value.IsEmpty
                || sourceSegmentPoints == null
                || sourceSegmentPoints.Count < 3)
            {
                return null;
            }

            List<Point> points = TransformPolygon(sourceSegmentPoints, sourceSegmentBounds.Value, targetBounds, imageSize, simplificationTolerance: 0D);
            if (points.Count < 3)
            {
                return null;
            }

            var segment = new LabelingSegmentationObject(points, classItem ?? new CClassItem { Text = className })
            {
                ClassName = className
            };

            foreach (IReadOnlyList<Point> cutout in sourceSegmentCutouts ?? Array.Empty<IReadOnlyList<Point>>())
            {
                List<Point> transformedCutout = TransformPolygon(cutout, sourceSegmentBounds.Value, targetBounds, imageSize, simplificationTolerance: 0.75D);
                if (transformedCutout.Count >= 3)
                {
                    segment.CutoutPolygons.Add(transformedCutout);
                }
            }

            return segment;
        }

        private static List<Point> TransformPolygon(
            IEnumerable<Point> points,
            Rectangle sourceBounds,
            Rectangle targetBounds,
            Size imageSize,
            double simplificationTolerance)
        {
            if (points == null || sourceBounds.IsEmpty || targetBounds.IsEmpty)
            {
                return new List<Point>();
            }

            double sourceSpanX = Math.Max(1, sourceBounds.Width - 1);
            double sourceSpanY = Math.Max(1, sourceBounds.Height - 1);
            double targetSpanX = Math.Max(1, targetBounds.Width - 1);
            double targetSpanY = Math.Max(1, targetBounds.Height - 1);

            return SegmentationGeometry.NormalizePolygon(
                points.Select(point => new Point(
                    targetBounds.Left + (int)Math.Round(((point.X - sourceBounds.Left) / sourceSpanX) * targetSpanX),
                    targetBounds.Top + (int)Math.Round(((point.Y - sourceBounds.Top) / sourceSpanY) * targetSpanY))),
                imageSize,
                minimumDistance: 1,
                simplificationTolerance: simplificationTolerance);
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
