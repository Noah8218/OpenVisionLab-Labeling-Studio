using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace MvcVisionSystem.Yolo
{
    public sealed class YoloSegmentationHistoricalRemediationAuditReport
    {
        public string OutputRootPath { get; internal set; } = string.Empty;

        public string ExcludedSourceImagePath { get; internal set; } = string.Empty;

        public string ProposedBackupRootPath { get; internal set; } = string.Empty;

        public int ExcludedSourceImageCount { get; internal set; }

        public List<YoloSegmentationHistoricalRemediationAuditImage> Images { get; } = new List<YoloSegmentationHistoricalRemediationAuditImage>();

        public List<string> Errors { get; } = new List<string>();

        public int CandidateImageCount => Images.Count;

        public int CandidateRecordCount => Images.Sum(item => item.Records.Count);

        public int ChangedYoloLabelImageCount => Images.Count(item => string.Equals(item.YoloLabelDiffKind, "Changed", StringComparison.Ordinal));

        public int UnresolvedRecordCount => Images.Sum(item => item.Records.Count(record => !record.CanProposeGeometry));

        public bool HasErrors => Errors.Count > 0 || Images.Any(item => item.Errors.Count > 0);
    }

    public sealed class YoloSegmentationHistoricalRemediationAuditImage
    {
        public string Split { get; internal set; } = string.Empty;

        public string ImageName { get; internal set; } = string.Empty;

        public string ImagePath { get; internal set; } = string.Empty;

        public string SegmentPath { get; internal set; } = string.Empty;

        public string MaskPath { get; internal set; } = string.Empty;

        public string LabelPath { get; internal set; } = string.Empty;

        public string ProposedBackupDirectory { get; internal set; } = string.Empty;

        public List<YoloSegmentationHistoricalRemediationAuditRecord> Records { get; } = new List<YoloSegmentationHistoricalRemediationAuditRecord>();

        public IReadOnlyList<string> ExistingYoloLabelLines { get; internal set; } = Array.Empty<string>();

        public IReadOnlyList<string> ProposedYoloLabelLines { get; internal set; } = Array.Empty<string>();

        public string YoloLabelDiffKind { get; internal set; } = "NotCompared";

        public string YoloLabelDiff { get; internal set; } = string.Empty;

        public List<string> Errors { get; } = new List<string>();
    }

    public sealed class YoloSegmentationHistoricalRemediationAuditRecord
    {
        public int ClassIndex { get; internal set; }

        public string ClassName { get; internal set; } = string.Empty;

        public string OldGeometryType { get; internal set; } = "LegacyUntypedRectangle";

        public int OldPointCount { get; internal set; }

        public string ProposedGeometryType { get; internal set; } = "Unavailable";

        public int ProposedPolygonCount { get; internal set; }

        public int ProposedPointCount { get; internal set; }

        public int MaskPixelCount { get; internal set; }

        public string Error { get; internal set; } = string.Empty;

        public bool CanProposeGeometry => string.IsNullOrWhiteSpace(Error) && ProposedPolygonCount > 0;
    }

    public sealed class YoloSegmentationHistoricalRemediationAuditExportResult
    {
        public string OutputPath { get; internal set; } = string.Empty;

        public int CandidateImageCount { get; internal set; }

        public int CandidateRecordCount { get; internal set; }
    }

    public static class YoloSegmentationHistoricalRemediationAuditService
    {
        public const string DefaultFileName = "segmentation-remediation-dry-run.md";

        private static readonly string[] DatasetModes =
        {
            YoloDatasetSplitService.TrainMode,
            YoloDatasetSplitService.ValidMode,
            YoloDatasetSplitService.TestMode
        };

        private static readonly string[] ImageExtensions = { ".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff" };

        public static YoloSegmentationHistoricalRemediationAuditReport Build(CData data, string sourceImagePath = "")
        {
            var report = new YoloSegmentationHistoricalRemediationAuditReport
            {
                ExcludedSourceImagePath = sourceImagePath ?? string.Empty
            };
            if (data == null)
            {
                report.Errors.Add("Dataset configuration is missing.");
                return report;
            }

            string outputRootPath = data.OutputRootPath;
            if (string.IsNullOrWhiteSpace(outputRootPath))
            {
                report.Errors.Add("Dataset output root is missing.");
                return report;
            }

            outputRootPath = Path.GetFullPath(outputRootPath);
            report.OutputRootPath = outputRootPath;
            report.ProposedBackupRootPath = Path.Combine(outputRootPath, "backups", "segmentation-remediation");
            if (!Directory.Exists(outputRootPath))
            {
                report.Errors.Add("Dataset output root does not exist.");
                return report;
            }

            string sourceStem = Path.GetFileNameWithoutExtension(sourceImagePath ?? string.Empty);
            foreach (string split in DatasetModes)
            {
                BuildSplit(report, data.ClassNamedList, outputRootPath, split, sourceStem);
            }

            if (!string.IsNullOrWhiteSpace(sourceStem) && report.ExcludedSourceImageCount == 0)
            {
                report.Errors.Add("The selected template source image was not found in the segmentation artifacts.");
            }

            return report;
        }

        public static string ResolveDefaultOutputPath(CData data)
        {
            string outputRootPath = data?.OutputRootPath;
            return string.IsNullOrWhiteSpace(outputRootPath)
                ? string.Empty
                : Path.Combine(outputRootPath, "reports", DefaultFileName);
        }

        public static YoloSegmentationHistoricalRemediationAuditExportResult ExportMarkdown(
            YoloSegmentationHistoricalRemediationAuditReport report,
            string outputPath)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException("Report output path is required.", nameof(outputPath));
            }

            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(outputPath, BuildMarkdown(report), new UTF8Encoding(false));
            return new YoloSegmentationHistoricalRemediationAuditExportResult
            {
                OutputPath = outputPath,
                CandidateImageCount = report.CandidateImageCount,
                CandidateRecordCount = report.CandidateRecordCount
            };
        }

        public static string BuildMarkdown(YoloSegmentationHistoricalRemediationAuditReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var builder = new StringBuilder();
            builder.AppendLine("# Historical SEG Remediation (Read-only Dry Run)");
            builder.AppendLine();
            builder.AppendLine("- Dataset annotation, mask, YOLO label, recipe, and model files modified: no");
            builder.AppendLine($"- Candidate images: {report.CandidateImageCount}");
            builder.AppendLine($"- Candidate records: {report.CandidateRecordCount}");
            builder.AppendLine($"- Source images excluded: {report.ExcludedSourceImageCount}");
            builder.AppendLine($"- YOLO label files with a proposed difference: {report.ChangedYoloLabelImageCount}");
            builder.AppendLine($"- Records without a usable mask-derived proposal: {report.UnresolvedRecordCount}");
            builder.AppendLine($"- Proposed backup root (not created): `{report.ProposedBackupRootPath}`");
            builder.AppendLine(string.IsNullOrWhiteSpace(report.ExcludedSourceImagePath)
                ? "- Source exclusion: none; every legacy candidate is shown."
                : $"- Source exclusion reference: `{report.ExcludedSourceImagePath}`");

            AppendErrors(builder, "Report errors", report.Errors);
            foreach (YoloSegmentationHistoricalRemediationAuditImage image in report.Images)
            {
                builder.AppendLine();
                builder.AppendLine($"## {Escape(image.Split)}/{Escape(image.ImageName)}");
                builder.AppendLine();
                builder.AppendLine($"- Segment JSON: `{image.SegmentPath}`");
                builder.AppendLine($"- Mask PNG: `{image.MaskPath}`");
                builder.AppendLine($"- Current YOLO label: `{image.LabelPath}`");
                builder.AppendLine($"- Proposed backup destination (not created): `{image.ProposedBackupDirectory}`");
                builder.AppendLine();
                builder.AppendLine("| Class | Old geometry | Old points | Proposed geometry | Proposed polygons / points | Mask pixels | State |");
                builder.AppendLine("| --- | --- | ---: | --- | ---: | ---: | --- |");
                foreach (YoloSegmentationHistoricalRemediationAuditRecord record in image.Records)
                {
                    string state = record.CanProposeGeometry ? "Ready for approved migration" : Escape(record.Error);
                    builder.AppendLine($"| {Escape(record.ClassName)} ({record.ClassIndex}) | {Escape(record.OldGeometryType)} | {record.OldPointCount} | {Escape(record.ProposedGeometryType)} | {record.ProposedPolygonCount} / {record.ProposedPointCount} | {record.MaskPixelCount} | {state} |");
                }

                AppendErrors(builder, "Image errors", image.Errors);
                builder.AppendLine();
                builder.AppendLine($"### YOLO label diff ({image.YoloLabelDiffKind})");
                builder.AppendLine();
                builder.AppendLine("```diff");
                builder.AppendLine(image.YoloLabelDiff);
                builder.AppendLine("```");
            }

            return builder.ToString();
        }

        private static void BuildSplit(
            YoloSegmentationHistoricalRemediationAuditReport report,
            IReadOnlyList<CClassItem> classes,
            string outputRootPath,
            string split,
            string sourceStem)
        {
            string splitRoot = Path.Combine(outputRootPath, "data", split);
            string segmentDirectory = Path.Combine(splitRoot, "segments");
            if (!Directory.Exists(segmentDirectory))
            {
                return;
            }

            string imageDirectory = Path.Combine(splitRoot, "images");
            string maskDirectory = Path.Combine(splitRoot, "masks");
            string labelDirectory = Path.Combine(splitRoot, "labels");
            foreach (string segmentPath in Directory.EnumerateFiles(segmentDirectory, "*.json", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                string fileStem = Path.GetFileNameWithoutExtension(segmentPath);
                if (string.IsNullOrWhiteSpace(fileStem))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(sourceStem)
                    && string.Equals(fileStem, sourceStem, StringComparison.OrdinalIgnoreCase))
                {
                    report.ExcludedSourceImageCount++;
                    continue;
                }

                string imagePath = FindImagePath(imageDirectory, fileStem);
                string maskPath = Path.Combine(maskDirectory, $"{fileStem}.png");
                string labelPath = Path.Combine(labelDirectory, $"{fileStem}.txt");
                if (!TryReadAnnotation(segmentPath, out SegmentationAnnotationFile annotation, out string annotationError))
                {
                    report.Errors.Add($"{split}/{fileStem}: {annotationError}");
                    continue;
                }

                Size imageSize = ResolveImageSize(annotation, imagePath);
                if (imageSize.Width <= 0 || imageSize.Height <= 0)
                {
                    report.Errors.Add($"{split}/{fileStem}: image size could not be resolved.");
                    continue;
                }

                var item = new YoloSegmentationHistoricalRemediationAuditImage
                {
                    Split = split,
                    ImageName = string.IsNullOrWhiteSpace(annotation.ImageName) ? fileStem : annotation.ImageName,
                    ImagePath = imagePath,
                    SegmentPath = segmentPath,
                    MaskPath = maskPath,
                    LabelPath = labelPath,
                    ProposedBackupDirectory = Path.Combine(report.ProposedBackupRootPath, split, fileStem)
                };

                foreach (SegmentationPolygonRecord record in annotation.Polygons ?? new List<SegmentationPolygonRecord>())
                {
                    if (!YoloSegmentationAnnotationService.IsLegacyRasterMaskCandidate(record, imageSize))
                    {
                        continue;
                    }

                    var recordItem = new YoloSegmentationHistoricalRemediationAuditRecord
                    {
                        ClassIndex = record.ClassIndex,
                        ClassName = ResolveClassName(record, classes),
                        OldPointCount = record.Points?.Count ?? 0
                    };
                    if (YoloSegmentationAnnotationService.TryBuildLegacyRasterMaskSegment(
                        record,
                        maskPath,
                        classes,
                        imageSize,
                        out LabelingSegmentationObject maskSegment))
                    {
                        IReadOnlyList<SegmentationPolygonRecord> proposedRecords = BuildProposedRecords(maskSegment, classes, imageSize);
                        if (proposedRecords.Count > 0)
                        {
                            recordItem.ProposedGeometryType = "RasterMaskContour";
                            recordItem.ProposedPolygonCount = proposedRecords.Count;
                            recordItem.ProposedPointCount = proposedRecords.Sum(item => item.Points?.Count ?? 0);
                            recordItem.MaskPixelCount = maskSegment.MaskData?.Count(value => value != 0) ?? 0;
                        }
                        else
                        {
                            recordItem.Error = "The class does not resolve to the current dataset catalog.";
                        }
                    }
                    else
                    {
                        recordItem.Error = File.Exists(maskPath)
                            ? "The matching class-index mask cannot produce a raster geometry proposal."
                            : "The matching class-index mask PNG is missing.";
                    }

                    item.Records.Add(recordItem);
                }

                if (item.Records.Count == 0)
                {
                    continue;
                }

                item.ExistingYoloLabelLines = ReadLabelLines(labelPath, item.Errors);
                item.ProposedYoloLabelLines = YoloSegmentationTrainingLabelService.BuildReadOnlyLabelLines(
                    segmentPath,
                    maskPath,
                    classes,
                    imagePath,
                    out IReadOnlyList<string> labelErrors);
                foreach (string error in labelErrors)
                {
                    item.Errors.Add(error);
                }

                SetYoloLabelDiff(item, File.Exists(labelPath));
                report.Images.Add(item);
            }
        }

        private static IReadOnlyList<SegmentationPolygonRecord> BuildProposedRecords(
            LabelingSegmentationObject segment,
            IReadOnlyList<CClassItem> classes,
            Size imageSize)
        {
            string className = segment?.ClassName ?? segment?.ClassItem?.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(className))
            {
                return Array.Empty<SegmentationPolygonRecord>();
            }

            var byClass = new Dictionary<string, List<LabelingSegmentationObject>>(StringComparer.OrdinalIgnoreCase)
            {
                [className] = new List<LabelingSegmentationObject> { segment }
            };
            return YoloSegmentationAnnotationService.BuildPolygonRecords(byClass, classes, imageSize);
        }

        private static bool TryReadAnnotation(string segmentPath, out SegmentationAnnotationFile annotation, out string error)
        {
            annotation = null;
            error = string.Empty;
            try
            {
                annotation = JsonConvert.DeserializeObject<SegmentationAnnotationFile>(File.ReadAllText(segmentPath));
                if (annotation == null)
                {
                    error = "Segment JSON is empty.";
                    return false;
                }

                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is JsonException)
            {
                error = "Segment JSON cannot be read: " + ex.Message;
                return false;
            }
        }

        private static Size ResolveImageSize(SegmentationAnnotationFile annotation, string imagePath)
        {
            if ((annotation?.ImageWidth ?? 0) > 0 && (annotation?.ImageHeight ?? 0) > 0)
            {
                return new Size(annotation.ImageWidth, annotation.ImageHeight);
            }

            try
            {
                using Image image = Image.FromFile(imagePath);
                return image.Size;
            }
            catch (ArgumentException)
            {
                return Size.Empty;
            }
            catch (IOException)
            {
                return Size.Empty;
            }
            catch (UnauthorizedAccessException)
            {
                return Size.Empty;
            }
        }

        private static string FindImagePath(string imageDirectory, string fileStem)
        {
            if (string.IsNullOrWhiteSpace(imageDirectory) || !Directory.Exists(imageDirectory))
            {
                return string.Empty;
            }

            return Directory.EnumerateFiles(imageDirectory, "*.*", SearchOption.TopDirectoryOnly)
                .Where(path => ImageExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                .Where(path => string.Equals(Path.GetFileNameWithoutExtension(path), fileStem, StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault() ?? string.Empty;
        }

        private static string ResolveClassName(SegmentationPolygonRecord record, IReadOnlyList<CClassItem> classes)
        {
            if (!string.IsNullOrWhiteSpace(record?.ClassName))
            {
                return record.ClassName.Trim();
            }

            return record != null
                && record.ClassIndex >= 0
                && record.ClassIndex < (classes?.Count ?? 0)
                ? classes[record.ClassIndex]?.Text ?? string.Empty
                : string.Empty;
        }

        private static IReadOnlyList<string> ReadLabelLines(string labelPath, ICollection<string> errors)
        {
            if (!File.Exists(labelPath))
            {
                return Array.Empty<string>();
            }

            try
            {
                return File.ReadAllLines(labelPath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                errors?.Add("Current YOLO label cannot be read: " + ex.Message);
                return Array.Empty<string>();
            }
        }

        private static void SetYoloLabelDiff(YoloSegmentationHistoricalRemediationAuditImage item, bool labelFileExists)
        {
            IReadOnlyList<string> existing = item.ExistingYoloLabelLines ?? Array.Empty<string>();
            IReadOnlyList<string> proposed = item.ProposedYoloLabelLines ?? Array.Empty<string>();
            if (!labelFileExists)
            {
                item.YoloLabelDiffKind = "Missing";
            }
            else if (existing.SequenceEqual(proposed, StringComparer.Ordinal))
            {
                item.YoloLabelDiffKind = "Unchanged";
            }
            else
            {
                item.YoloLabelDiffKind = "Changed";
            }

            var builder = new StringBuilder();
            builder.AppendLine($"existing lines: {existing.Count}; proposed lines: {proposed.Count}");
            int lineCount = Math.Max(existing.Count, proposed.Count);
            for (int index = 0; index < lineCount; index++)
            {
                string oldLine = index < existing.Count ? existing[index] : null;
                string newLine = index < proposed.Count ? proposed[index] : null;
                if (string.Equals(oldLine, newLine, StringComparison.Ordinal))
                {
                    continue;
                }

                if (oldLine != null)
                {
                    builder.AppendLine("- " + oldLine);
                }
                if (newLine != null)
                {
                    builder.AppendLine("+ " + newLine);
                }
            }

            if (lineCount == 0)
            {
                builder.AppendLine("(no label lines)");
            }

            item.YoloLabelDiff = builder.ToString().TrimEnd();
        }

        private static void AppendErrors(StringBuilder builder, string title, IEnumerable<string> errors)
        {
            List<string> values = (errors ?? Enumerable.Empty<string>())
                .Where(error => !string.IsNullOrWhiteSpace(error))
                .ToList();
            if (values.Count == 0)
            {
                return;
            }

            builder.AppendLine();
            builder.AppendLine("## " + title);
            foreach (string error in values)
            {
                builder.AppendLine("- " + Escape(error));
            }
        }

        private static string Escape(string value)
            => (value ?? string.Empty).Replace("|", "\\|");
    }
}
