using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MvcVisionSystem.Yolo
{
    public static class YoloDatasetQualityAuditExportService
    {
        public static YoloDatasetQualityAuditExportResult ExportMarkdown(
            YoloDatasetQualityAuditReport report,
            string outputPath)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException("Dataset quality audit output path is required.", nameof(outputPath));
            }

            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string markdown = BuildMarkdown(report);
            File.WriteAllText(outputPath, markdown, new UTF8Encoding(false));
            return new YoloDatasetQualityAuditExportResult
            {
                OutputPath = outputPath,
                LineCount = markdown.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).Length,
                MissingLabelCount = report.TotalMissingLabelCount,
                InvalidLabelLineCount = report.TotalInvalidLabelLineCount
            };
        }

        public static string BuildMarkdown(YoloDatasetQualityAuditReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var builder = new StringBuilder();
            builder.AppendLine("# Dataset Quality Audit");
            builder.AppendLine();
            builder.AppendLine($"- Images: {report.TotalImageCount}");
            builder.AppendLine($"- Label files: {report.TotalLabelFileCount}");
            builder.AppendLine($"- Missing labels: {report.TotalMissingLabelCount}");
            builder.AppendLine($"- Empty labels: {report.TotalEmptyLabelCount}");
            builder.AppendLine($"- Invalid label lines: {report.TotalInvalidLabelLineCount}");
            builder.AppendLine($"- Objects: {report.TotalObjectCount}");
            builder.AppendLine();
            builder.AppendLine("| Split | Images | Label files | Missing labels | Empty labels | Invalid lines | Objects |");
            builder.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: |");
            foreach (YoloDatasetQualityAuditSplitSummary split in report.Splits)
            {
                builder.AppendLine($"| {EscapeCell(split.Split)} | {split.ImageCount} | {split.LabelFileCount} | {split.MissingLabelCount} | {split.EmptyLabelCount} | {split.InvalidLabelLineCount} | {split.ObjectCount} |");
            }

            builder.AppendLine();
            builder.AppendLine("| Class | Objects |");
            builder.AppendLine("| --- | ---: |");
            foreach (KeyValuePair<string, int> item in report.ObjectCountByClass.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
            {
                builder.AppendLine($"| {EscapeCell(item.Key)} | {item.Value} |");
            }

            return builder.ToString();
        }

        private static string EscapeCell(string value)
            => (value ?? string.Empty).Replace("|", "\\|");
    }

    public sealed class YoloDatasetQualityAuditExportResult
    {
        public string OutputPath { get; set; } = string.Empty;

        public int LineCount { get; set; }

        public int MissingLabelCount { get; set; }

        public int InvalidLabelLineCount { get; set; }
    }
}
