using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace MvcVisionSystem.Yolo
{
    public static class YoloImageQualityReviewReportExportService
    {
        public const string DefaultFileName = "label-quality-review.md";

        public static string ResolveDefaultOutputPath(CData data)
        {
            string outputRootPath = data?.OutputRootPath;
            return string.IsNullOrWhiteSpace(outputRootPath)
                ? string.Empty
                : Path.Combine(outputRootPath, DefaultFileName);
        }

        public static YoloImageQualityReviewReportExportResult ExportMarkdown(
            IEnumerable<YoloImageReviewStatus> statuses,
            string outputPath,
            DateTime? generatedAtUtc = null)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                throw new ArgumentException("Label quality review output path is required.", nameof(outputPath));
            }

            List<YoloImageReviewStatus> items = (statuses ?? Enumerable.Empty<YoloImageReviewStatus>())
                .Where(status => status != null)
                .OrderBy(status => status.ImageName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(status => status.ImagePath, StringComparer.OrdinalIgnoreCase)
                .ToList();
            DateTime reportTimestampUtc = (generatedAtUtc ?? DateTime.UtcNow).ToUniversalTime();
            string markdown = BuildMarkdown(items, reportTimestampUtc);
            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(outputPath, markdown, new UTF8Encoding(false));
            return new YoloImageQualityReviewReportExportResult
            {
                OutputPath = outputPath,
                TotalImageCount = items.Count,
                UnreviewedCount = items.Count(item => item.QualityReviewState == YoloImageQualityReviewState.Unreviewed),
                NeedsFixCount = items.Count(item => item.QualityReviewState == YoloImageQualityReviewState.NeedsFix),
                ReviewedCount = items.Count(item => item.QualityReviewState == YoloImageQualityReviewState.Reviewed)
            };
        }

        public static string BuildMarkdown(
            IEnumerable<YoloImageReviewStatus> statuses,
            DateTime generatedAtUtc)
        {
            List<YoloImageReviewStatus> items = (statuses ?? Enumerable.Empty<YoloImageReviewStatus>())
                .Where(status => status != null)
                .ToList();
            List<YoloImageReviewStatus> needsFixItems = items
                .Where(status => status.QualityReviewState == YoloImageQualityReviewState.NeedsFix)
                .OrderBy(status => status.ImageName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(status => status.ImagePath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var builder = new StringBuilder();
            builder.AppendLine("# 라벨 품질 검수 보고서");
            builder.AppendLine();
            builder.AppendLine($"- 생성 시각 (UTC): {generatedAtUtc.ToUniversalTime():yyyy-MM-dd HH:mm:ss}");
            builder.AppendLine($"- 전체 이미지: {items.Count}");
            builder.AppendLine($"- 미검토: {items.Count(item => item.QualityReviewState == YoloImageQualityReviewState.Unreviewed)}");
            builder.AppendLine($"- 수정 필요: {needsFixItems.Count}");
            builder.AppendLine($"- 검수 완료: {items.Count(item => item.QualityReviewState == YoloImageQualityReviewState.Reviewed)}");
            builder.AppendLine();
            builder.AppendLine("## 수정 필요 이미지");
            builder.AppendLine();
            if (needsFixItems.Count == 0)
            {
                builder.AppendLine("수정 필요 이미지가 없습니다.");
                return builder.ToString();
            }

            builder.AppendLine("| 이미지 | 수정 사유 | 최근 변경 (UTC) |");
            builder.AppendLine("| --- | --- | --- |");
            foreach (YoloImageReviewStatus status in needsFixItems)
            {
                string imageName = Path.GetFileName(status.ImagePath);
                if (string.IsNullOrWhiteSpace(imageName))
                {
                    imageName = status.ImageName;
                }

                string note = string.IsNullOrWhiteSpace(status.QualityReviewNote)
                    ? "사유 미입력"
                    : status.QualityReviewNote;
                string updatedAt = status.LastUpdatedUtc == default
                    ? string.Empty
                    : status.LastUpdatedUtc.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss");
                builder.AppendLine($"| {EscapeCell(imageName)} | {EscapeCell(note)} | {updatedAt} |");
            }

            return builder.ToString();
        }

        private static string EscapeCell(string value)
            => (value ?? string.Empty)
                .Replace("|", "\\|")
                .Replace("\r", " ")
                .Replace("\n", " ");
    }

    public sealed class YoloImageQualityReviewReportExportResult
    {
        public string OutputPath { get; set; } = string.Empty;

        public int TotalImageCount { get; set; }

        public int UnreviewedCount { get; set; }

        public int NeedsFixCount { get; set; }

        public int ReviewedCount { get; set; }
    }
}
