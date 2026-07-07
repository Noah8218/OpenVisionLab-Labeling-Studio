using MahApps.Metro.IconPacks;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MvcVisionSystem
{
    public sealed class WpfModelComparisonReviewService
    {
        private const double DefaultIouThreshold = 0.5D;
        private const double DefaultConfidenceThreshold = 0.25D;
        private static readonly string[] ImageExtensions = { ".bmp", ".jpg", ".jpeg", ".png", ".tif", ".tiff" };

        public WpfModelComparisonReviewReport BuildLatestReport(
            IReadOnlyList<string> classNames,
            double confidenceThreshold = DefaultConfidenceThreshold,
            int maxExamples = 5)
        {
            string artifactsRoot = Path.Combine(FindRepositoryRoot(), "artifacts", "yolo-model-comparison");
            if (!Directory.Exists(artifactsRoot))
            {
                return WpfModelComparisonReviewReport.Empty;
            }

            string summaryPath = Directory.EnumerateFiles(artifactsRoot, "comparison-summary.json", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            return string.IsNullOrWhiteSpace(summaryPath)
                ? WpfModelComparisonReviewReport.Empty
                : BuildFromSummaryFile(summaryPath, classNames, confidenceThreshold, maxExamples);
        }

        public WpfModelComparisonReviewReport BuildFromSummaryFile(
            string summaryJsonPath,
            IReadOnlyList<string> classNames,
            double? confidenceThreshold = null,
            int maxExamples = 5)
        {
            if (string.IsNullOrWhiteSpace(summaryJsonPath) || !File.Exists(summaryJsonPath))
            {
                return WpfModelComparisonReviewReport.Empty;
            }

            JObject summary = JObject.Parse(File.ReadAllText(summaryJsonPath));
            string baselineLabelsPath = ResolveSummaryPath(summaryJsonPath, summary.SelectToken("baseline.labelsPath")?.Value<string>());
            string candidateLabelsPath = ResolveSummaryPath(summaryJsonPath, summary.SelectToken("candidate.labelsPath")?.Value<string>());
            string dataYamlPath = ResolveSummaryPath(summaryJsonPath, summary.SelectToken("dataYaml")?.Value<string>());
            string task = summary.SelectToken("task")?.Value<string>() ?? "val";
            string promotionDecision = summary.SelectToken("promotion.recommendation")?.Value<string>() ?? string.Empty;
            string promotionReason = summary.SelectToken("promotion.reason")?.Value<string>() ?? string.Empty;
            Func<string, string> resolveImagePath = BuildImagePathResolver(dataYamlPath, task);
            double threshold = confidenceThreshold
                ?? summary.SelectToken("uiConfidence")?.Value<double?>()
                ?? DefaultConfidenceThreshold;

            return BuildFromLabelDirectories(
                baselineLabelsPath,
                candidateLabelsPath,
                classNames,
                threshold,
                DefaultIouThreshold,
                maxExamples,
                summaryJsonPath,
                resolveImagePath,
                promotionDecision,
                promotionReason);
        }

        public WpfModelComparisonReviewReport BuildFromLabelDirectories(
            string baselineLabelsPath,
            string candidateLabelsPath,
            IReadOnlyList<string> classNames,
            double confidenceThreshold = DefaultConfidenceThreshold,
            double iouThreshold = DefaultIouThreshold,
            int maxExamples = 5,
            string sourcePath = "",
            Func<string, string> resolveImagePath = null,
            string promotionDecision = "",
            string promotionReason = "")
        {
            if (!Directory.Exists(baselineLabelsPath) || !Directory.Exists(candidateLabelsPath))
            {
                return new WpfModelComparisonReviewReport(
                    hasComparison: true,
                    summaryText: "\uBAA8\uB378 \uCC28\uC774 \uC608\uC2DC: \uD45C\uC2DC\uD560 \uB77C\uBCA8 \uACB0\uACFC \uC5C6\uC74C",
                    detailText: "\uBAA8\uB378 \uBE44\uAD50 \uACB0\uACFC\uB97C \uB9CC\uB4E0 \uB4A4 \uC608\uC2DC\uB97C \uD655\uC778\uD558\uC138\uC694.",
                    sourcePath: sourcePath,
                    examples: Array.Empty<WpfModelComparisonReviewExample>());
            }

            Dictionary<string, List<YoloLabelDetection>> baseline = ReadDetections(baselineLabelsPath, classNames, confidenceThreshold);
            Dictionary<string, List<YoloLabelDetection>> candidate = ReadDetections(candidateLabelsPath, classNames, confidenceThreshold);
            List<WpfModelComparisonReviewExample> examples = BuildExamples(baseline, candidate, iouThreshold, resolveImagePath)
                .Take(Math.Max(1, maxExamples))
                .ToList();
            int differenceImageCount = BuildDifferenceImageKeys(baseline, candidate, iouThreshold).Count;
            int baselineCount = baseline.Values.Sum(items => items.Count);
            int candidateCount = candidate.Values.Sum(items => items.Count);
            string summary = examples.Count == 0
                ? "\uBAA8\uB378 \uCC28\uC774 \uC608\uC2DC: \uCC28\uC774 \uC5C6\uC74C"
                : $"\uBAA8\uB378 \uCC28\uC774 \uC608\uC2DC: \uCC28\uC774 {differenceImageCount}\uAC1C \uC774\uBBF8\uC9C0 / \uC608\uC2DC {examples.Count}\uAC1C";
            string detail =
                $"\uAE30\uC874 \uBAA8\uB378 {baselineCount}\uAC1C, \uC0C8 \uBAA8\uB378 {candidateCount}\uAC1C, \uC2E0\uB8B0\uB3C4 {confidenceThreshold.ToString("P0", CultureInfo.CurrentCulture)}, \uACB9\uCE68 {iouThreshold.ToString("P0", CultureInfo.CurrentCulture)}";
            string promotionDetail = BuildPromotionDetailText(promotionDecision, promotionReason);
            if (!string.IsNullOrWhiteSpace(promotionDetail))
            {
                detail += " / " + promotionDetail;
            }

            return new WpfModelComparisonReviewReport(
                hasComparison: true,
                summaryText: summary,
                detailText: detail,
                sourcePath: sourcePath,
                examples: examples);
        }

        private static string BuildPromotionDetailText(string decision, string reason)
        {
            if (string.IsNullOrWhiteSpace(decision))
            {
                return string.Empty;
            }

            string label = decision.Trim().ToLowerInvariant() switch
            {
                "promote" => "\uAD50\uCCB4 \uCD94\uCC9C",
                "hold" => "\uAD50\uCCB4 \uBCF4\uB958",
                "review" => "\uC608\uC2DC \uAC80\uD1A0",
                _ => "\uAD50\uCCB4 \uD310\uB2E8"
            };
            string reasonText = BuildPromotionReasonText(reason);
            return string.IsNullOrWhiteSpace(reasonText)
                ? label
                : label + ": " + reasonText;
        }

        private static string BuildPromotionReasonText(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return string.Empty;
            }

            string trimmed = reason.Trim();
            string normalized = trimmed.ToLowerInvariant();
            if (normalized.Contains("precision", StringComparison.Ordinal)
                && normalized.Contains("below", StringComparison.Ordinal)
                && normalized.Contains("minimum", StringComparison.Ordinal))
            {
                MatchCollection numbers = Regex.Matches(trimmed, @"[-+]?\d+(?:\.\d+)?");
                string current = numbers.Count > 0 ? FormatReasonPercent(numbers[0].Value) : string.Empty;
                string minimum = numbers.Count > 1 ? FormatReasonPercent(numbers[1].Value) : string.Empty;
                return string.IsNullOrWhiteSpace(current) || string.IsNullOrWhiteSpace(minimum)
                    ? "\uC815\uBC00\uB3C4\uAC00 \uCD5C\uC18C \uAE30\uC900\uBCF4\uB2E4 \uB0AE\uC2B5\uB2C8\uB2E4. \uB77C\uBCA8\uACFC \uD559\uC2B5 \uB370\uC774\uD130\uB97C \uB354 \uD655\uC778\uD55C \uB4A4 \uAD50\uCCB4\uD558\uC138\uC694."
                    : string.Format(
                        CultureInfo.CurrentCulture,
                        "\uC815\uBC00\uB3C4 {0}\uAC00 \uCD5C\uC18C \uAE30\uC900 {1}\uBCF4\uB2E4 \uB0AE\uC2B5\uB2C8\uB2E4. \uB77C\uBCA8\uACFC \uD559\uC2B5 \uB370\uC774\uD130\uB97C \uB354 \uD655\uC778\uD55C \uB4A4 \uAD50\uCCB4\uD558\uC138\uC694.",
                        current,
                        minimum);
            }

            if (normalized.Contains("held-out comparison uses", StringComparison.Ordinal)
                && normalized.Contains("labeled images", StringComparison.Ordinal)
                && normalized.Contains("collect at least", StringComparison.Ordinal))
            {
                MatchCollection numbers = Regex.Matches(trimmed, @"[-+]?\d+(?:\.\d+)?");
                string current = numbers.Count > 0 ? numbers[0].Value : string.Empty;
                string minimum = numbers.Count > 1 ? numbers[1].Value : string.Empty;
                return string.IsNullOrWhiteSpace(current) || string.IsNullOrWhiteSpace(minimum)
                    ? "\uCD5C\uC885 \uAC80\uC99D \uB77C\uBCA8 \uC218\uAC00 \uAD50\uCCB4 \uD310\uB2E8\uC5D0 \uBD80\uC871\uD569\uB2C8\uB2E4."
                    : string.Format(
                        CultureInfo.CurrentCulture,
                        "\uCD5C\uC885 \uAC80\uC99D \uB77C\uBCA8\uC774 {0}\uAC1C\uBFD0\uC785\uB2C8\uB2E4. \uBAA8\uB378 \uAD50\uCCB4 \uC804\uC5D0 \uCD5C\uC18C {1}\uAC1C\uAE4C\uC9C0 \uD655\uBCF4\uD558\uC138\uC694.",
                        current,
                        minimum);
            }

            if (normalized.Contains("ui-threshold candidates", StringComparison.Ordinal)
                && normalized.Contains("confidence", StringComparison.Ordinal))
            {
                MatchCollection numbers = Regex.Matches(trimmed, @"[-+]?\d+(?:\.\d+)?");
                string threshold = numbers.Count > 1 ? FormatReasonPercent(numbers[1].Value) : string.Empty;
                return string.IsNullOrWhiteSpace(threshold)
                    ? "\uAC80\uD1A0 \uAE30\uC900 \uC2E0\uB8B0\uB3C4\uC5D0\uC11C \uC0C8 \uBAA8\uB378 \uD6C4\uBCF4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4. \uAE30\uC900\uC744 \uB0AE\uCD94\uAC70\uB098 \uD559\uC2B5 \uB370\uC774\uD130\uB97C \uBCF4\uAC15\uD55C \uB4A4 \uAD50\uCCB4\uD558\uC138\uC694."
                    : string.Format(
                        CultureInfo.CurrentCulture,
                        "\uAC80\uD1A0 \uAE30\uC900 \uC2E0\uB8B0\uB3C4 {0}\uC5D0\uC11C \uC0C8 \uBAA8\uB378 \uD6C4\uBCF4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4. \uAE30\uC900\uC744 \uB0AE\uCD94\uAC70\uB098 \uD559\uC2B5 \uB370\uC774\uD130\uB97C \uBCF4\uAC15\uD55C \uB4A4 \uAD50\uCCB4\uD558\uC138\uC694.",
                        threshold);
            }

            return trimmed.All(c => c <= 127)
                ? "\uBAA8\uB378 \uAD50\uCCB4 \uADFC\uAC70\uB97C \uB354 \uD655\uC778\uD55C \uB4A4 \uD310\uB2E8\uD558\uC138\uC694."
                : trimmed;
        }

        private static string FormatReasonPercent(string value)
        {
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number)
                ? number.ToString("P1", CultureInfo.CurrentCulture)
                : string.Empty;
        }

        private static List<WpfModelComparisonReviewExample> BuildExamples(
            Dictionary<string, List<YoloLabelDetection>> baseline,
            Dictionary<string, List<YoloLabelDetection>> candidate,
            double iouThreshold,
            Func<string, string> resolveImagePath = null)
        {
            var examples = new List<WpfModelComparisonReviewExample>();
            foreach (string imageKey in baseline.Keys.Concat(candidate.Keys).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(key => key, StringComparer.OrdinalIgnoreCase))
            {
                string imagePath = ResolveExampleImagePath(resolveImagePath, imageKey);
                List<YoloLabelDetection> baselineItems = baseline.TryGetValue(imageKey, out List<YoloLabelDetection> baseList)
                    ? baseList
                    : new List<YoloLabelDetection>();
                List<YoloLabelDetection> candidateItems = candidate.TryGetValue(imageKey, out List<YoloLabelDetection> candidateList)
                    ? candidateList
                    : new List<YoloLabelDetection>();
                var matchedBaseline = new HashSet<int>();

                foreach (YoloLabelDetection candidateItem in candidateItems)
                {
                    (int baselineIndex, double iou) = FindBestMatch(candidateItem, baselineItems, matchedBaseline);
                    if (baselineIndex < 0 || iou < iouThreshold)
                    {
                        examples.Add(BuildCandidateOnlyExample(imageKey, candidateItem, imagePath));
                        continue;
                    }

                    matchedBaseline.Add(baselineIndex);
                    YoloLabelDetection baselineItem = baselineItems[baselineIndex];
                    if (baselineItem.ClassId != candidateItem.ClassId)
                    {
                        examples.Add(BuildClassChangedExample(imageKey, baselineItem, candidateItem, iou, imagePath));
                    }
                }

                for (int i = 0; i < baselineItems.Count; i++)
                {
                    if (!matchedBaseline.Contains(i))
                    {
                        examples.Add(BuildBaselineOnlyExample(imageKey, baselineItems[i], imagePath));
                    }
                }
            }

            return examples
                .OrderByDescending(item => item.Priority)
                .ThenBy(item => item.ImageKey, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static HashSet<string> BuildDifferenceImageKeys(
            Dictionary<string, List<YoloLabelDetection>> baseline,
            Dictionary<string, List<YoloLabelDetection>> candidate,
            double iouThreshold)
        {
            return BuildExamples(baseline, candidate, iouThreshold)
                .Select(item => item.ImageKey)
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static WpfModelComparisonReviewExample BuildCandidateOnlyExample(string imageKey, YoloLabelDetection candidate, string imagePath)
            => new WpfModelComparisonReviewExample(
                imageKey,
                "CandidateOnly",
                $"\uC0C8 \uBAA8\uB378\uB9CC \uAC80\uCD9C: {imageKey}",
                $"\uC0C8 \uBAA8\uB378 {FormatDetection(candidate)} / \uAE30\uC874 \uBAA8\uB378 -",
                PackIconMaterialKind.PlusCircleOutline,
                priority: 3,
                imagePath: imagePath,
                actionText: "\uD655\uC778: \uC2E4\uC81C \uAC1D\uCCB4\uBA74 \uC0C8 \uBAA8\uB378 \uAC1C\uC120, \uC544\uB2C8\uBA74 \uACFC\uAC80\uCD9C",
                locationText: FormatLocation(candidate),
                left: candidate.Left,
                top: candidate.Top,
                right: candidate.Right,
                bottom: candidate.Bottom);

        private static WpfModelComparisonReviewExample BuildBaselineOnlyExample(string imageKey, YoloLabelDetection baseline, string imagePath)
            => new WpfModelComparisonReviewExample(
                imageKey,
                "BaselineOnly",
                $"\uAE30\uC874 \uBAA8\uB378\uB9CC \uAC80\uCD9C: {imageKey}",
                $"\uAE30\uC874 \uBAA8\uB378 {FormatDetection(baseline)} / \uC0C8 \uBAA8\uB378 -",
                PackIconMaterialKind.MinusCircleOutline,
                priority: 2,
                imagePath: imagePath,
                actionText: "\uD655\uC778: \uC2E4\uC81C \uAC1D\uCCB4\uBA74 \uC0C8 \uBAA8\uB378 \uB204\uB77D\uC73C\uB85C \uAD50\uCCB4 \uBCF4\uB958",
                locationText: FormatLocation(baseline),
                left: baseline.Left,
                top: baseline.Top,
                right: baseline.Right,
                bottom: baseline.Bottom);

        private static WpfModelComparisonReviewExample BuildClassChangedExample(
            string imageKey,
            YoloLabelDetection baseline,
            YoloLabelDetection candidate,
            double iou,
            string imagePath)
            => new WpfModelComparisonReviewExample(
                imageKey,
                "ClassChanged",
                $"\uD074\uB798\uC2A4 \uB2E4\uB984: {imageKey}",
                $"\uAE30\uC874 \uBAA8\uB378 {FormatDetection(baseline)} / \uC0C8 \uBAA8\uB378 {FormatDetection(candidate)} / \uACB9\uCE68 {iou.ToString("P0", CultureInfo.CurrentCulture)}",
                PackIconMaterialKind.SwapHorizontal,
                priority: 4,
                imagePath: imagePath,
                actionText: "\uD655\uC778: \uB77C\uBCA8 \uAE30\uC900\uACFC \uB354 \uB9DE\uB294 \uBAA8\uB378\uC744 \uC120\uD0DD",
                locationText: FormatLocation(candidate),
                left: candidate.Left,
                top: candidate.Top,
                right: candidate.Right,
                bottom: candidate.Bottom);

        private static string ResolveExampleImagePath(Func<string, string> resolveImagePath, string imageKey)
        {
            if (resolveImagePath == null || string.IsNullOrWhiteSpace(imageKey))
            {
                return string.Empty;
            }

            try
            {
                return resolveImagePath(imageKey) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static (int Index, double Iou) FindBestMatch(
            YoloLabelDetection candidate,
            IReadOnlyList<YoloLabelDetection> baseline,
            HashSet<int> matchedBaseline)
        {
            int bestIndex = -1;
            double bestIou = 0D;
            for (int i = 0; i < baseline.Count; i++)
            {
                if (matchedBaseline.Contains(i))
                {
                    continue;
                }

                double iou = CalculateIou(candidate, baseline[i]);
                if (iou > bestIou)
                {
                    bestIou = iou;
                    bestIndex = i;
                }
            }

            return (bestIndex, bestIou);
        }

        private static Dictionary<string, List<YoloLabelDetection>> ReadDetections(
            string labelsPath,
            IReadOnlyList<string> classNames,
            double confidenceThreshold)
        {
            var result = new Dictionary<string, List<YoloLabelDetection>>(StringComparer.OrdinalIgnoreCase);
            if (!Directory.Exists(labelsPath))
            {
                return result;
            }

            foreach (string filePath in Directory.EnumerateFiles(labelsPath, "*.txt", SearchOption.TopDirectoryOnly))
            {
                string imageKey = Path.GetFileNameWithoutExtension(filePath);
                foreach (string line in File.ReadLines(filePath))
                {
                    if (TryParseDetection(line, classNames, out YoloLabelDetection detection)
                        && detection.Confidence >= confidenceThreshold)
                    {
                        if (!result.TryGetValue(imageKey, out List<YoloLabelDetection> detections))
                        {
                            detections = new List<YoloLabelDetection>();
                            result[imageKey] = detections;
                        }

                        detections.Add(detection);
                    }
                }
            }

            return result;
        }

        private static bool TryParseDetection(
            string line,
            IReadOnlyList<string> classNames,
            out YoloLabelDetection detection)
        {
            detection = default;
            string[] parts = (line ?? string.Empty)
                .Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2
                || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int classId))
            {
                return false;
            }

            if (TryParseSegmentationDetection(parts, classId, classNames, out detection))
            {
                return true;
            }

            if (parts.Length < 5
                || !TryParseDouble(parts[1], out double centerX)
                || !TryParseDouble(parts[2], out double centerY)
                || !TryParseDouble(parts[3], out double width)
                || !TryParseDouble(parts[4], out double height))
            {
                return false;
            }

            double confidence = parts.Length >= 6 && TryParseDouble(parts[5], out double parsedConfidence)
                ? parsedConfidence
                : 1D;
            detection = new YoloLabelDetection(
                classId,
                ResolveClassName(classId, classNames),
                confidence,
                Math.Max(0D, centerX - width / 2D),
                Math.Max(0D, centerY - height / 2D),
                Math.Min(1D, centerX + width / 2D),
                Math.Min(1D, centerY + height / 2D));
            return detection.Right > detection.Left && detection.Bottom > detection.Top;
        }

        private static bool TryParseSegmentationDetection(
            string[] parts,
            int classId,
            IReadOnlyList<string> classNames,
            out YoloLabelDetection detection)
        {
            detection = default;
            int coordinateTokenCount = parts.Length - 1;
            double confidence = 1D;
            if (coordinateTokenCount >= 7 && coordinateTokenCount % 2 == 1)
            {
                if (!TryParseDouble(parts[parts.Length - 1], out confidence))
                {
                    return false;
                }

                coordinateTokenCount--;
            }

            if (coordinateTokenCount < 6 || coordinateTokenCount % 2 != 0)
            {
                return false;
            }

            double left = 1D;
            double top = 1D;
            double right = 0D;
            double bottom = 0D;
            for (int i = 1; i <= coordinateTokenCount; i += 2)
            {
                if (!TryParseDouble(parts[i], out double x) || !TryParseDouble(parts[i + 1], out double y))
                {
                    return false;
                }

                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }

            detection = new YoloLabelDetection(
                classId,
                ResolveClassName(classId, classNames),
                confidence,
                Math.Max(0D, Math.Min(1D, left)),
                Math.Max(0D, Math.Min(1D, top)),
                Math.Max(0D, Math.Min(1D, right)),
                Math.Max(0D, Math.Min(1D, bottom)));
            return detection.Right > detection.Left && detection.Bottom > detection.Top;
        }

        private static bool TryParseDouble(string value, out double result)
            => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);

        private static double CalculateIou(YoloLabelDetection left, YoloLabelDetection right)
        {
            double x1 = Math.Max(left.Left, right.Left);
            double y1 = Math.Max(left.Top, right.Top);
            double x2 = Math.Min(left.Right, right.Right);
            double y2 = Math.Min(left.Bottom, right.Bottom);
            double intersection = Math.Max(0D, x2 - x1) * Math.Max(0D, y2 - y1);
            if (intersection <= 0D)
            {
                return 0D;
            }

            double union = left.Area + right.Area - intersection;
            return union <= 0D ? 0D : intersection / union;
        }

        private static string FormatDetection(YoloLabelDetection detection)
            => $"{detection.ClassName} {detection.Confidence.ToString("P1", CultureInfo.CurrentCulture)}";

        private static string FormatLocation(YoloLabelDetection detection)
        {
            double centerX = detection.Left + (detection.Right - detection.Left) / 2D;
            double centerY = detection.Top + (detection.Bottom - detection.Top) / 2D;
            double width = detection.Right - detection.Left;
            double height = detection.Bottom - detection.Top;
            return $"\uC704\uCE58: \uC911\uC2EC {centerX.ToString("P0", CultureInfo.CurrentCulture)}, {centerY.ToString("P0", CultureInfo.CurrentCulture)} / \uD06C\uAE30 {width.ToString("P0", CultureInfo.CurrentCulture)} x {height.ToString("P0", CultureInfo.CurrentCulture)}";
        }

        private static string ResolveClassName(int classId, IReadOnlyList<string> classNames)
        {
            if (classNames != null && classId >= 0 && classId < classNames.Count && !string.IsNullOrWhiteSpace(classNames[classId]))
            {
                return classNames[classId];
            }

            return $"class {classId}";
        }

        private static string ResolveSummaryPath(string summaryJsonPath, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return Path.IsPathRooted(path)
                ? Path.GetFullPath(path)
                : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(summaryJsonPath) ?? Directory.GetCurrentDirectory(), path));
        }

        private static Func<string, string> BuildImagePathResolver(string dataYamlPath, string task)
        {
            string imageDirectory = ResolveImageDirectoryFromDataYaml(dataYamlPath, task);
            if (!Directory.Exists(imageDirectory))
            {
                return _ => string.Empty;
            }

            // Build a stem index once so every Candidate Review row can open the source image without scanning the folder again.
            Dictionary<string, string> imagePathByKey = Directory
                .EnumerateFiles(imageDirectory, "*.*", SearchOption.TopDirectoryOnly)
                .Where(IsSupportedImageFile)
                .GroupBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            return imageKey => imagePathByKey.TryGetValue(imageKey ?? string.Empty, out string imagePath)
                ? imagePath
                : string.Empty;
        }

        private static string ResolveImageDirectoryFromDataYaml(string dataYamlPath, string task)
        {
            if (string.IsNullOrWhiteSpace(dataYamlPath) || !File.Exists(dataYamlPath))
            {
                return string.Empty;
            }

            Dictionary<string, string> values = ReadDataYamlScalarValues(dataYamlPath);
            string yamlKey = NormalizeComparisonTask(task);
            if (!values.TryGetValue(yamlKey, out string yamlImagePath) || string.IsNullOrWhiteSpace(yamlImagePath))
            {
                return string.Empty;
            }

            values.TryGetValue("path", out string yamlRootPath);
            return ResolveDataYamlPath(dataYamlPath, yamlRootPath, yamlImagePath);
        }

        private static Dictionary<string, string> ReadDataYamlScalarValues(string dataYamlPath)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string rawLine in File.ReadLines(dataYamlPath))
            {
                string line = rawLine?.Trim() ?? string.Empty;
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                int separatorIndex = line.IndexOf(':');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, separatorIndex).Trim();
                string value = StripYamlScalarValue(line.Substring(separatorIndex + 1).Trim());
                if (!string.IsNullOrWhiteSpace(key) && value.Length > 0)
                {
                    result[key] = value;
                }
            }

            return result;
        }

        private static string StripYamlScalarValue(string value)
        {
            value = RemoveYamlInlineComment(value ?? string.Empty).Trim();
            if (value.Length >= 2
                && ((value[0] == '"' && value[value.Length - 1] == '"')
                    || (value[0] == '\'' && value[value.Length - 1] == '\'')))
            {
                value = value.Substring(1, value.Length - 2);
            }

            return value.Trim();
        }

        private static string RemoveYamlInlineComment(string value)
        {
            bool inSingleQuote = false;
            bool inDoubleQuote = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\'' && !inDoubleQuote)
                {
                    inSingleQuote = !inSingleQuote;
                    continue;
                }

                if (c == '"' && !inSingleQuote)
                {
                    inDoubleQuote = !inDoubleQuote;
                    continue;
                }

                if (c == '#' && !inSingleQuote && !inDoubleQuote)
                {
                    return value.Substring(0, i);
                }
            }

            return value;
        }

        private static string NormalizeComparisonTask(string task)
        {
            string normalized = (task ?? string.Empty).Trim().ToLowerInvariant();
            return normalized == "test" ? "test" : "val";
        }

        private static string ResolveDataYamlPath(string yamlFilePath, string yamlRootPath, string yamlPath)
        {
            string normalizedPath = (yamlPath ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                return string.Empty;
            }

            if (Path.IsPathRooted(normalizedPath))
            {
                return Path.GetFullPath(normalizedPath);
            }

            string root = string.IsNullOrWhiteSpace(yamlRootPath)
                ? Path.GetDirectoryName(yamlFilePath) ?? string.Empty
                : yamlRootPath.Replace('/', Path.DirectorySeparatorChar);
            if (!Path.IsPathRooted(root))
            {
                root = Path.Combine(Path.GetDirectoryName(yamlFilePath) ?? string.Empty, root);
            }

            return Path.GetFullPath(Path.Combine(root, normalizedPath));
        }

        private static bool IsSupportedImageFile(string path)
        {
            string extension = Path.GetExtension(path);
            return ImageExtensions.Any(item => string.Equals(item, extension, StringComparison.OrdinalIgnoreCase));
        }

        private static string FindRepositoryRoot()
        {
            foreach (string startPath in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                string current = startPath;
                while (!string.IsNullOrWhiteSpace(current))
                {
                    if (File.Exists(Path.Combine(current, "MvcVisionSystem.sln"))
                        || File.Exists(Path.Combine(current, "MvcVisionSystem.csproj")))
                    {
                        return current;
                    }

                    current = Directory.GetParent(current)?.FullName;
                }
            }

            return Directory.GetCurrentDirectory();
        }

        private readonly struct YoloLabelDetection
        {
            public YoloLabelDetection(int classId, string className, double confidence, double left, double top, double right, double bottom)
            {
                ClassId = classId;
                ClassName = className ?? string.Empty;
                Confidence = confidence;
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }

            public int ClassId { get; }

            public string ClassName { get; }

            public double Confidence { get; }

            public double Left { get; }

            public double Top { get; }

            public double Right { get; }

            public double Bottom { get; }

            public double Area => Math.Max(0D, Right - Left) * Math.Max(0D, Bottom - Top);
        }
    }

    public sealed class WpfModelComparisonReviewReport
    {
        public static readonly WpfModelComparisonReviewReport Empty = new WpfModelComparisonReviewReport(
            hasComparison: false,
            summaryText: string.Empty,
            detailText: string.Empty,
            sourcePath: string.Empty,
            examples: Array.Empty<WpfModelComparisonReviewExample>());

        public WpfModelComparisonReviewReport(
            bool hasComparison,
            string summaryText,
            string detailText,
            string sourcePath,
            IReadOnlyList<WpfModelComparisonReviewExample> examples)
        {
            HasComparison = hasComparison;
            SummaryText = summaryText ?? string.Empty;
            DetailText = detailText ?? string.Empty;
            SourcePath = sourcePath ?? string.Empty;
            Examples = examples ?? Array.Empty<WpfModelComparisonReviewExample>();
        }

        public bool HasComparison { get; }

        public string SummaryText { get; }

        public string DetailText { get; }

        public string SourcePath { get; }

        public IReadOnlyList<WpfModelComparisonReviewExample> Examples { get; }
    }

    public sealed class WpfModelComparisonReviewExample
    {
        public WpfModelComparisonReviewExample(
            string imageKey,
            string kind,
            string title,
            string detail,
            PackIconMaterialKind iconKind,
            int priority,
            string imagePath = "",
            string actionText = "",
            string locationText = "",
            double left = 0D,
            double top = 0D,
            double right = 0D,
            double bottom = 0D)
        {
            ImageKey = imageKey ?? string.Empty;
            Kind = kind ?? string.Empty;
            Title = title ?? string.Empty;
            Detail = detail ?? string.Empty;
            IconKind = iconKind;
            Priority = priority;
            ImagePath = imagePath ?? string.Empty;
            ActionText = actionText ?? string.Empty;
            LocationText = locationText ?? string.Empty;
            Left = Clamp01(left);
            Top = Clamp01(top);
            Right = Clamp01(right);
            Bottom = Clamp01(bottom);
        }

        public string ImageKey { get; }

        public string Kind { get; }

        public string Title { get; }

        public string Detail { get; }

        public string ActionText { get; }

        public string LocationText { get; }

        public string ImagePath { get; }

        public PackIconMaterialKind IconKind { get; }

        public int Priority { get; }

        public double Left { get; }

        public double Top { get; }

        public double Right { get; }

        public double Bottom { get; }

        public bool HasFocusBox => Right > Left && Bottom > Top;

        public string ReviewText
        {
            get
            {
                string location = string.IsNullOrWhiteSpace(LocationText) ? string.Empty : Environment.NewLine + LocationText;
                string action = string.IsNullOrWhiteSpace(ActionText) ? string.Empty : Environment.NewLine + ActionText;
                return $"{Detail}{location}{action}";
            }
        }

        private static double Clamp01(double value)
            => double.IsNaN(value) || double.IsInfinity(value) ? 0D : Math.Max(0D, Math.Min(1D, value));
    }
}
