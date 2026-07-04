using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace MvcVisionSystem.Yolo
{
    public static class LabelStudioDetectionExportService
    {
        private const string FromName = "bbox";
        private const string ToName = "image";
        private static readonly string[] DatasetModes =
        {
            YoloDatasetSplitService.TrainMode,
            YoloDatasetSplitService.ValidMode,
            YoloDatasetSplitService.TestMode
        };

        private static readonly string[] ImageExtensions = { ".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff" };

        public static LabelStudioDetectionExportResult ExportDataset(
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
                throw new ArgumentException("Label Studio export output path is required.", nameof(outputPath));
            }

            LabelStudioDetectionExportResult result = BuildTasks(data, splits, out List<LabelStudioDetectionTask> tasks);
            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(outputPath, JsonConvert.SerializeObject(tasks, Formatting.Indented), new UTF8Encoding(false));
            result.OutputPath = outputPath;
            return result;
        }

        public static LabelStudioDetectionExportResult BuildTasks(
            CData data,
            IEnumerable<string> splits,
            out List<LabelStudioDetectionTask> tasks)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            data.NormalizeOutputPaths();
            tasks = new List<LabelStudioDetectionTask>();
            var result = new LabelStudioDetectionExportResult
            {
                CategoryCount = CountExportableClasses(data)
            };

            IReadOnlyList<string> normalizedSplits = NormalizeSplits(splits);
            result.Splits.AddRange(normalizedSplits);

            int nextTaskId = 1;
            foreach (string split in normalizedSplits)
            {
                string imageDirectory = Path.Combine(data.OutputRootPath, "data", split, "images");
                string labelDirectory = Path.Combine(data.OutputRootPath, "data", split, "labels");
                foreach (string imagePath in EnumerateImageFiles(imageDirectory))
                {
                    using Image image = Image.FromFile(imagePath);
                    string relativeImagePath = FormatRelativePath(data.OutputRootPath, imagePath);
                    string labelPath = Path.Combine(labelDirectory, $"{Path.GetFileNameWithoutExtension(imagePath)}.txt");
                    List<LabelStudioDetectionResult> taskResults = File.Exists(labelPath)
                        ? LoadResults(data, labelPath, image.Size, nextTaskId, result)
                        : new List<LabelStudioDetectionResult>();

                    var task = new LabelStudioDetectionTask
                    {
                        Id = nextTaskId,
                        Data = new LabelStudioDetectionTaskData
                        {
                            Image = relativeImagePath,
                            Split = split
                        }
                    };

                    if (File.Exists(labelPath))
                    {
                        task.Annotations.Add(new LabelStudioDetectionAnnotation
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

        private static List<LabelStudioDetectionResult> LoadResults(
            CData data,
            string labelPath,
            Size imageSize,
            int taskId,
            LabelStudioDetectionExportResult result)
        {
            var results = new List<LabelStudioDetectionResult>();
            int resultIndex = 1;
            foreach (string line in File.ReadLines(labelPath))
            {
                if (!YoloAnnotationService.TryParseYoloLine(line, imageSize, out int classIndex, out Rectangle bounds)
                    || classIndex < 0
                    || classIndex >= (data.ClassNamedList?.Count ?? 0)
                    || string.IsNullOrWhiteSpace(data.ClassNamedList[classIndex]?.Text))
                {
                    result.SkippedAnnotationCount++;
                    continue;
                }

                results.Add(new LabelStudioDetectionResult
                {
                    Id = $"r{taskId}_{resultIndex++}",
                    FromName = FromName,
                    ToName = ToName,
                    Source = "$image",
                    Type = "rectanglelabels",
                    Origin = "manual",
                    ImageRotation = 0,
                    OriginalWidth = imageSize.Width,
                    OriginalHeight = imageSize.Height,
                    Value = new LabelStudioDetectionValue
                    {
                        X = ToPercent(bounds.Left, imageSize.Width),
                        Y = ToPercent(bounds.Top, imageSize.Height),
                        Width = ToPercent(bounds.Width, imageSize.Width),
                        Height = ToPercent(bounds.Height, imageSize.Height),
                        Rotation = 0,
                        RectangleLabels = new[] { data.ClassNamedList[classIndex].Text.Trim() }
                    }
                });
            }

            return results;
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

    public sealed class LabelStudioDetectionExportResult
    {
        public string OutputPath { get; set; } = string.Empty;

        public int TaskCount { get; set; }

        public int ReviewedTaskCount { get; set; }

        public int ResultCount { get; set; }

        public int CategoryCount { get; set; }

        public int SkippedAnnotationCount { get; set; }

        public List<string> Splits { get; } = new List<string>();
    }

    public sealed class LabelStudioDetectionTask
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("data")]
        public LabelStudioDetectionTaskData Data { get; set; } = new LabelStudioDetectionTaskData();

        [JsonProperty("annotations")]
        public List<LabelStudioDetectionAnnotation> Annotations { get; } = new List<LabelStudioDetectionAnnotation>();
    }

    public sealed class LabelStudioDetectionTaskData
    {
        [JsonProperty("image")]
        public string Image { get; set; } = string.Empty;

        [JsonProperty("split")]
        public string Split { get; set; } = string.Empty;
    }

    public sealed class LabelStudioDetectionAnnotation
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("result")]
        public List<LabelStudioDetectionResult> Result { get; set; } = new List<LabelStudioDetectionResult>();

        [JsonProperty("was_cancelled")]
        public bool WasCancelled { get; set; }

        [JsonProperty("ground_truth")]
        public bool GroundTruth { get; set; }

        [JsonProperty("lead_time")]
        public double LeadTime { get; set; }
    }

    public sealed class LabelStudioDetectionResult
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
        public LabelStudioDetectionValue Value { get; set; } = new LabelStudioDetectionValue();

        [JsonProperty("origin")]
        public string Origin { get; set; } = string.Empty;

        [JsonProperty("image_rotation")]
        public int ImageRotation { get; set; }

        [JsonProperty("original_width")]
        public int OriginalWidth { get; set; }

        [JsonProperty("original_height")]
        public int OriginalHeight { get; set; }
    }

    public sealed class LabelStudioDetectionValue
    {
        [JsonProperty("x")]
        public double X { get; set; }

        [JsonProperty("y")]
        public double Y { get; set; }

        [JsonProperty("width")]
        public double Width { get; set; }

        [JsonProperty("height")]
        public double Height { get; set; }

        [JsonProperty("rotation")]
        public int Rotation { get; set; }

        [JsonProperty("rectanglelabels")]
        public string[] RectangleLabels { get; set; } = Array.Empty<string>();
    }
}
