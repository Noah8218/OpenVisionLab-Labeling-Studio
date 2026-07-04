using MvcVisionSystem;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace MvcVisionSystem.Yolo
{
    public static class LabelStudioSegmentationExportService
    {
        private const string FromName = "polygon";
        private const string ToName = "image";
        private static readonly string[] DatasetModes =
        {
            YoloDatasetSplitService.TrainMode,
            YoloDatasetSplitService.ValidMode,
            YoloDatasetSplitService.TestMode
        };

        private static readonly string[] ImageExtensions = { ".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff" };

        public static LabelStudioSegmentationExportResult ExportDataset(
            CData data,
            string outputPath,
            IEnumerable<string> splits = null)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException("Label Studio segmentation export output path is required.", nameof(outputPath));
            }

            LabelStudioSegmentationExportResult result = BuildTasks(data, splits, out List<LabelStudioSegmentationTask> tasks);
            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(outputPath, JsonConvert.SerializeObject(tasks, Formatting.Indented), new UTF8Encoding(false));
            result.OutputPath = outputPath;
            return result;
        }

        public static LabelStudioSegmentationExportResult BuildTasks(
            CData data,
            IEnumerable<string> splits,
            out List<LabelStudioSegmentationTask> tasks)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            data.NormalizeOutputPaths();
            tasks = new List<LabelStudioSegmentationTask>();
            var result = new LabelStudioSegmentationExportResult
            {
                CategoryCount = CountExportableClasses(data)
            };

            IReadOnlyList<string> normalizedSplits = NormalizeSplits(splits);
            result.Splits.AddRange(normalizedSplits);

            int nextTaskId = 1;
            foreach (string split in normalizedSplits)
            {
                string imageDirectory = Path.Combine(data.OutputRootPath, "data", split, "images");
                string segmentDirectory = Path.Combine(data.OutputRootPath, "data", split, "segments");
                foreach (string imagePath in EnumerateImageFiles(imageDirectory))
                {
                    using Image image = Image.FromFile(imagePath);
                    string relativeImagePath = FormatRelativePath(data.OutputRootPath, imagePath);
                    string segmentPath = Path.Combine(segmentDirectory, $"{Path.GetFileNameWithoutExtension(imagePath)}.json");
                    List<LabelStudioSegmentationResult> taskResults = File.Exists(segmentPath)
                        ? LoadResults(data, segmentPath, image.Size, nextTaskId, result)
                        : new List<LabelStudioSegmentationResult>();

                    var task = new LabelStudioSegmentationTask
                    {
                        Id = nextTaskId,
                        Data = new LabelStudioSegmentationTaskData
                        {
                            Image = relativeImagePath,
                            Split = split
                        }
                    };

                    if (File.Exists(segmentPath))
                    {
                        task.Annotations.Add(new LabelStudioSegmentationAnnotation
                        {
                            Id = nextTaskId.ToString(),
                            Result = taskResults,
                            WasCancelled = false,
                            GroundTruth = false,
                            LeadTime = 0
                        });
                        result.ReviewedTaskCount++;
                    }

                    tasks.Add(task);
                    result.TaskCount++;
                    result.ResultCount += taskResults.Count;
                    nextTaskId++;
                }
            }

            return result;
        }

        private static List<LabelStudioSegmentationResult> LoadResults(
            CData data,
            string segmentPath,
            Size imageSize,
            int taskId,
            LabelStudioSegmentationExportResult result)
        {
            var results = new List<LabelStudioSegmentationResult>();
            int resultIndex = 1;
            foreach (SegmentationPolygonRecord record in LoadPolygonRecords(segmentPath, result))
            {
                if (!TryBuildResult(record, data.ClassNamedList, imageSize, taskId, resultIndex, out LabelStudioSegmentationResult item))
                {
                    result.SkippedAnnotationCount++;
                    continue;
                }

                results.Add(item);
                resultIndex++;
            }

            return results;
        }

        private static IEnumerable<SegmentationPolygonRecord> LoadPolygonRecords(
            string segmentPath,
            LabelStudioSegmentationExportResult result)
        {
            SegmentationAnnotationFile annotation;
            try
            {
                annotation = JsonConvert.DeserializeObject<SegmentationAnnotationFile>(File.ReadAllText(segmentPath));
            }
            catch
            {
                result.SkippedAnnotationCount++;
                yield break;
            }

            foreach (SegmentationPolygonRecord record in annotation?.Polygons ?? new List<SegmentationPolygonRecord>())
            {
                yield return record;
            }
        }

        private static bool TryBuildResult(
            SegmentationPolygonRecord record,
            IReadOnlyList<CClassItem> classes,
            Size imageSize,
            int taskId,
            int resultIndex,
            out LabelStudioSegmentationResult result)
        {
            result = null;
            if (record?.Points == null || imageSize.Width <= 0 || imageSize.Height <= 0)
            {
                return false;
            }

            int classIndex = ResolveClassIndex(record, classes);
            if (classIndex < 0)
            {
                return false;
            }

            List<Point> points = SegmentationGeometry.NormalizePolygon(
                record.Points.Select(point => new Point(point.X, point.Y)),
                imageSize,
                minimumDistance: 1);
            if (points.Count < 3)
            {
                return false;
            }

            result = new LabelStudioSegmentationResult
            {
                Id = $"r{taskId}_{resultIndex}",
                FromName = FromName,
                ToName = ToName,
                Source = "$image",
                Type = "polygonlabels",
                Origin = "manual",
                ImageRotation = 0,
                OriginalWidth = imageSize.Width,
                OriginalHeight = imageSize.Height,
                Value = new LabelStudioSegmentationValue
                {
                    Points = points
                        .Select(point => new[] { ToPercent(point.X, imageSize.Width), ToPercent(point.Y, imageSize.Height) })
                        .ToList(),
                    PolygonLabels = new[] { classes[classIndex].Text.Trim() }
                }
            };
            return true;
        }

        private static int ResolveClassIndex(SegmentationPolygonRecord record, IReadOnlyList<CClassItem> classes)
        {
            if (classes == null || classes.Count == 0)
            {
                return -1;
            }

            if (record.ClassIndex >= 0
                && record.ClassIndex < classes.Count
                && !string.IsNullOrWhiteSpace(classes[record.ClassIndex]?.Text))
            {
                return record.ClassIndex;
            }

            if (string.IsNullOrWhiteSpace(record.ClassName))
            {
                return -1;
            }

            for (int index = 0; index < classes.Count; index++)
            {
                if (string.Equals(classes[index]?.Text, record.ClassName, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return -1;
        }

        private static double ToPercent(int pixels, int totalPixels)
        {
            if (totalPixels <= 0)
            {
                return 0;
            }

            return Math.Round(pixels / (double)totalPixels * 100D, 6);
        }

        private static int CountExportableClasses(CData data)
            => data.ClassNamedList?
                .Count(item => !string.IsNullOrWhiteSpace(item?.Text)) ?? 0;

        private static IReadOnlyList<string> NormalizeSplits(IEnumerable<string> splits)
        {
            var result = new List<string>();
            foreach (string split in splits ?? DatasetModes)
            {
                string normalized = NormalizeSplit(split);
                if (!string.IsNullOrWhiteSpace(normalized)
                    && !result.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                {
                    result.Add(normalized);
                }
            }

            return result.Count > 0 ? result : DatasetModes.ToList();
        }

        private static string NormalizeSplit(string split)
        {
            foreach (string mode in DatasetModes)
            {
                if (string.Equals(split, mode, StringComparison.OrdinalIgnoreCase))
                {
                    return mode;
                }
            }

            return string.Empty;
        }

        private static IEnumerable<string> EnumerateImageFiles(string imageDirectory)
        {
            if (string.IsNullOrWhiteSpace(imageDirectory) || !Directory.Exists(imageDirectory))
            {
                yield break;
            }

            foreach (string imagePath in Directory.EnumerateFiles(imageDirectory)
                .Where(path => ImageExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                yield return imagePath;
            }
        }

        private static string FormatRelativePath(string root, string path)
        {
            string relative = Path.GetRelativePath(root, path);
            return relative.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
        }
    }

    public sealed class LabelStudioSegmentationExportResult
    {
        public string OutputPath { get; set; } = string.Empty;

        public int TaskCount { get; set; }

        public int ReviewedTaskCount { get; set; }

        public int ResultCount { get; set; }

        public int CategoryCount { get; set; }

        public int SkippedAnnotationCount { get; set; }

        public List<string> Splits { get; } = new List<string>();
    }

    public sealed class LabelStudioSegmentationTask
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("data")]
        public LabelStudioSegmentationTaskData Data { get; set; } = new LabelStudioSegmentationTaskData();

        [JsonProperty("annotations")]
        public List<LabelStudioSegmentationAnnotation> Annotations { get; } = new List<LabelStudioSegmentationAnnotation>();
    }

    public sealed class LabelStudioSegmentationTaskData
    {
        [JsonProperty("image")]
        public string Image { get; set; } = string.Empty;

        [JsonProperty("split")]
        public string Split { get; set; } = string.Empty;
    }

    public sealed class LabelStudioSegmentationAnnotation
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("result")]
        public List<LabelStudioSegmentationResult> Result { get; set; } = new List<LabelStudioSegmentationResult>();

        [JsonProperty("was_cancelled")]
        public bool WasCancelled { get; set; }

        [JsonProperty("ground_truth")]
        public bool GroundTruth { get; set; }

        [JsonProperty("lead_time")]
        public double LeadTime { get; set; }
    }

    public sealed class LabelStudioSegmentationResult
    {
        [JsonProperty("from_name")]
        public string FromName { get; set; } = string.Empty;

        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("source")]
        public string Source { get; set; } = string.Empty;

        [JsonProperty("to_name")]
        public string ToName { get; set; } = string.Empty;

        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        [JsonProperty("value")]
        public LabelStudioSegmentationValue Value { get; set; } = new LabelStudioSegmentationValue();

        [JsonProperty("origin")]
        public string Origin { get; set; } = string.Empty;

        [JsonProperty("image_rotation")]
        public int ImageRotation { get; set; }

        [JsonProperty("original_width")]
        public int OriginalWidth { get; set; }

        [JsonProperty("original_height")]
        public int OriginalHeight { get; set; }
    }

    public sealed class LabelStudioSegmentationValue
    {
        [JsonProperty("points")]
        public List<double[]> Points { get; set; } = new List<double[]>();

        [JsonProperty("polygonlabels")]
        public string[] PolygonLabels { get; set; } = Array.Empty<string>();
    }
}
