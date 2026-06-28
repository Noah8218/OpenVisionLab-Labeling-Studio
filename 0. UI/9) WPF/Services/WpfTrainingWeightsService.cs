using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MvcVisionSystem
{
    public sealed class WpfTrainingWeightsComparison
    {
        public string LatestWeightsPath { get; set; } = "";

        public string CurrentWeightsPath { get; set; } = "";

        public DateTime? LatestWeightsUtc { get; set; }

        public DateTime? CurrentWeightsUtc { get; set; }

        public bool HasLatestWeights => !string.IsNullOrWhiteSpace(LatestWeightsPath);

        public bool ShouldApplyLatest { get; set; }

        public string StatusText { get; set; } = "";

        public WpfTrainingRunMetrics LatestMetrics { get; set; }

        public WpfTrainingRunMetrics CurrentMetrics { get; set; }

        public string MetricVerdictText { get; set; } = "";

        public string MetricsStatusText { get; set; } = "";
    }

    public sealed class WpfTrainingRunMetrics
    {
        public string ResultsCsvPath { get; set; } = "";

        public int Epoch { get; set; } = -1;

        public double? Precision { get; set; }

        public double? Recall { get; set; }

        public double? Map50 { get; set; }

        public double? Map5095 { get; set; }

        public double? BoxLoss { get; set; }

        public bool HasScore => Map5095.HasValue || Map50.HasValue || Precision.HasValue || Recall.HasValue;

        public double? PrimaryScore => Map5095 ?? Map50 ?? Precision ?? Recall;
    }

    public sealed class WpfTrainingWeightsService
    {
        // YOLOv5, YOLOv8 detection, and segmentation exports use slightly different
        // results.csv metric headers; keep those aliases here so the guide UI stays simple.
        private static readonly string[] PrecisionMetricAliases =
        {
            "metrics/precision",
            "metrics/precision(b)",
            "metrics/precision(m)",
            "precision",
            "precision(b)",
            "precision(m)",
            "p",
            "p(b)",
            "p(m)"
        };

        private static readonly string[] RecallMetricAliases =
        {
            "metrics/recall",
            "metrics/recall(b)",
            "metrics/recall(m)",
            "recall",
            "recall(b)",
            "recall(m)",
            "r",
            "r(b)",
            "r(m)"
        };

        private static readonly string[] Map50MetricAliases =
        {
            "metrics/map_0.5",
            "metrics/mAP_0.5",
            "metrics/map50",
            "metrics/mAP50",
            "metrics/map50(b)",
            "metrics/mAP50(B)",
            "metrics/map50(m)",
            "metrics/mAP50(M)",
            "map_0.5",
            "mAP_0.5",
            "map50",
            "mAP50",
            "map50(b)",
            "mAP50(B)",
            "map50(m)",
            "mAP50(M)",
            "map@50",
            "mAP@0.5"
        };

        private static readonly string[] Map5095MetricAliases =
        {
            "metrics/map_0.5:0.95",
            "metrics/mAP_0.5:0.95",
            "metrics/map50-95",
            "metrics/mAP50-95",
            "metrics/map50-95(b)",
            "metrics/mAP50-95(B)",
            "metrics/map50-95(m)",
            "metrics/mAP50-95(M)",
            "map_0.5:0.95",
            "mAP_0.5:0.95",
            "map50-95",
            "mAP50-95",
            "map50-95(b)",
            "mAP50-95(B)",
            "map50-95(m)",
            "mAP50-95(M)",
            "map@50-95",
            "mAP@0.5:0.95"
        };

        private static readonly string[] BoxLossMetricAliases =
        {
            "val/box_loss",
            "val/box_loss(b)",
            "val/box_loss(m)",
            "train/box_loss",
            "train/box_loss(b)",
            "train/box_loss(m)",
            "box_loss",
            "box_loss(b)",
            "box_loss(m)"
        };

        public bool TryFindLatestTrainingWeights(string projectRootPath, string outputRootPath, out string latestWeightsPath)
        {
            latestWeightsPath = EnumerateBestWeightCandidates(projectRootPath)
                .Concat(EnumerateBestWeightCandidates(outputRootPath))
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault() ?? string.Empty;

            return !string.IsNullOrWhiteSpace(latestWeightsPath);
        }

        public IReadOnlyList<string> EnumerateBestWeightCandidates(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            {
                return Array.Empty<string>();
            }

            var candidates = new List<string>
            {
                Path.Combine(rootPath, "best.pt")
            };

            string trainRunsRoot = Path.Combine(rootPath, "runs", "train");
            if (!Directory.Exists(trainRunsRoot))
            {
                return candidates;
            }

            candidates.Add(Path.Combine(trainRunsRoot, "weights", "best.pt"));
            foreach (string runDirectory in Directory.EnumerateDirectories(trainRunsRoot))
            {
                candidates.Add(Path.Combine(runDirectory, "weights", "best.pt"));
            }

            return candidates;
        }

        public WpfTrainingWeightsComparison BuildComparison(string projectRootPath, string outputRootPath, string currentWeightsPath)
        {
            currentWeightsPath = currentWeightsPath?.Trim() ?? string.Empty;
            TryFindLatestTrainingWeights(projectRootPath, outputRootPath, out string latestWeightsPath);
            DateTime? latestUtc = File.Exists(latestWeightsPath)
                ? File.GetLastWriteTimeUtc(latestWeightsPath)
                : null;
            DateTime? currentUtc = File.Exists(currentWeightsPath)
                ? File.GetLastWriteTimeUtc(currentWeightsPath)
                : null;
            bool shouldApply = ShouldPreferTrainingWeights(latestWeightsPath, currentWeightsPath);
            TryReadTrainingRunMetrics(latestWeightsPath, out WpfTrainingRunMetrics latestMetrics);
            TryReadTrainingRunMetrics(currentWeightsPath, out WpfTrainingRunMetrics currentMetrics);
            string metricVerdictText = BuildMetricVerdictText(latestMetrics, currentMetrics);

            return new WpfTrainingWeightsComparison
            {
                LatestWeightsPath = latestWeightsPath,
                CurrentWeightsPath = currentWeightsPath,
                LatestWeightsUtc = latestUtc,
                CurrentWeightsUtc = currentUtc,
                ShouldApplyLatest = shouldApply,
                StatusText = BuildComparisonStatusText(latestWeightsPath, currentWeightsPath, latestUtc, currentUtc, shouldApply),
                LatestMetrics = latestMetrics,
                CurrentMetrics = currentMetrics,
                MetricVerdictText = metricVerdictText,
                MetricsStatusText = BuildMetricsStatusText(latestMetrics, currentMetrics)
            };
        }

        public static bool ShouldPreferTrainingWeights(string latestWeightsPath, string currentWeightsPath)
        {
            if (string.IsNullOrWhiteSpace(latestWeightsPath) || !File.Exists(latestWeightsPath))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(currentWeightsPath) || !File.Exists(currentWeightsPath))
            {
                return true;
            }

            // Do not re-apply the same best.pt on completion. The guide should only move the
            // operator when training produced a genuinely newer artifact.
            if (PathsEqual(latestWeightsPath, currentWeightsPath))
            {
                return false;
            }

            return File.GetLastWriteTimeUtc(latestWeightsPath) > File.GetLastWriteTimeUtc(currentWeightsPath);
        }

        public static bool IsCompletedTrainingState(string state)
            => string.Equals(state?.Trim(), "completed", StringComparison.OrdinalIgnoreCase);

        private static string BuildComparisonStatusText(
            string latestWeightsPath,
            string currentWeightsPath,
            DateTime? latestUtc,
            DateTime? currentUtc,
            bool shouldApply)
        {
            if (string.IsNullOrWhiteSpace(latestWeightsPath))
            {
                return "\uD559\uC2B5 \uACB0\uACFC \uBAA8\uB378 \uD6C4\uBCF4 \uC5C6\uC74C";
            }

            string latestName = Path.GetFileName(latestWeightsPath);
            if (PathsEqual(latestWeightsPath, currentWeightsPath))
            {
                return $"현재 학습 결과 사용 중: {latestName}";
            }

            if (shouldApply)
            {
                string currentText = currentUtc.HasValue ? $" / 기존 {currentUtc.Value:yyyy-MM-dd HH:mm}Z" : string.Empty;
                string latestText = latestUtc.HasValue ? $" ({latestUtc.Value:yyyy-MM-dd HH:mm}Z)" : string.Empty;
                return $"새 학습 결과 적용 가능: {latestName}{latestText}{currentText}";
            }

            return string.IsNullOrWhiteSpace(currentWeightsPath)
                ? $"학습 결과 적용 불가: {latestName}"
                : $"기존 weight 유지: {Path.GetFileName(currentWeightsPath)}";
        }

        public static bool TryReadTrainingRunMetrics(string weightsPath, out WpfTrainingRunMetrics metrics)
        {
            metrics = null;
            if (!TryFindResultsCsvForWeights(weightsPath, out string resultsCsvPath))
            {
                return false;
            }

            string[] lines = File.ReadAllLines(resultsCsvPath);
            if (lines.Length < 2)
            {
                return false;
            }

            string headerLine = lines.FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));
            string valueLine = lines.Reverse().FirstOrDefault(line => !string.IsNullOrWhiteSpace(line) && !string.Equals(line, headerLine, StringComparison.Ordinal));
            if (string.IsNullOrWhiteSpace(headerLine) || string.IsNullOrWhiteSpace(valueLine))
            {
                return false;
            }

            string[] headers = SplitCsvLine(headerLine);
            string[] values = SplitCsvLine(valueLine);
            if (headers.Length == 0 || values.Length == 0)
            {
                return false;
            }

            metrics = new WpfTrainingRunMetrics
            {
                ResultsCsvPath = resultsCsvPath,
                Epoch = (int)(ReadMetric(headers, values, "epoch") ?? -1D),
                Precision = ReadMetric(headers, values, PrecisionMetricAliases),
                Recall = ReadMetric(headers, values, RecallMetricAliases),
                Map50 = ReadMetric(headers, values, Map50MetricAliases),
                Map5095 = ReadMetric(headers, values, Map5095MetricAliases),
                BoxLoss = ReadMetric(headers, values, BoxLossMetricAliases)
            };

            return metrics.HasScore || metrics.BoxLoss.HasValue;
        }

        private static bool TryFindResultsCsvForWeights(string weightsPath, out string resultsCsvPath)
        {
            resultsCsvPath = string.Empty;
            if (string.IsNullOrWhiteSpace(weightsPath))
            {
                return false;
            }

            string weightsDirectory = Path.GetDirectoryName(weightsPath.Trim());
            if (string.IsNullOrWhiteSpace(weightsDirectory))
            {
                return false;
            }

            var candidates = new List<string>
            {
                Path.Combine(weightsDirectory, "results.csv")
            };

            DirectoryInfo weightsDirectoryInfo = new DirectoryInfo(weightsDirectory);
            if (string.Equals(weightsDirectoryInfo.Name, "weights", StringComparison.OrdinalIgnoreCase)
                && weightsDirectoryInfo.Parent != null)
            {
                candidates.Add(Path.Combine(weightsDirectoryInfo.Parent.FullName, "results.csv"));
            }

            if (weightsDirectoryInfo.Parent != null)
            {
                candidates.Add(Path.Combine(weightsDirectoryInfo.Parent.FullName, "results.csv"));
            }

            resultsCsvPath = candidates
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault() ?? string.Empty;

            return !string.IsNullOrWhiteSpace(resultsCsvPath);
        }

        private static string BuildMetricsStatusText(WpfTrainingRunMetrics latestMetrics, WpfTrainingRunMetrics currentMetrics)
        {
            if (latestMetrics == null || !latestMetrics.HasScore)
            {
                return "학습 지표: results.csv 없음";
            }

            if (currentMetrics == null || !currentMetrics.HasScore)
            {
                return $"학습 지표: 최신 {FormatMetricSnapshot(latestMetrics)}";
            }

            var parts = new List<string>();
            AddPercentComparison(parts, "mAP50-95", latestMetrics.Map5095, currentMetrics.Map5095);
            AddPercentComparison(parts, "mAP50", latestMetrics.Map50, currentMetrics.Map50);
            AddPercentComparison(parts, "precision", latestMetrics.Precision, currentMetrics.Precision);
            AddPercentComparison(parts, "recall", latestMetrics.Recall, currentMetrics.Recall);
            AddLossComparison(parts, "box loss", latestMetrics.BoxLoss, currentMetrics.BoxLoss);

            return parts.Count == 0
                ? $"학습 지표: 최신 {FormatMetricSnapshot(latestMetrics)}"
                : $"학습 지표 비교({BuildMetricVerdictText(latestMetrics, currentMetrics)}): {string.Join(", ", parts)}";
        }

        private static string BuildMetricVerdictText(WpfTrainingRunMetrics latestMetrics, WpfTrainingRunMetrics currentMetrics)
        {
            // Keep the learner-facing verdict simple: prefer mAP-style scores when
            // present, and use loss only when no score metric was recorded.
            if (latestMetrics?.PrimaryScore.HasValue == true && currentMetrics?.PrimaryScore.HasValue == true)
            {
                double deltaPercent = ToPercentValue(latestMetrics.PrimaryScore.Value) - ToPercentValue(currentMetrics.PrimaryScore.Value);
                if (deltaPercent > 0.1D)
                {
                    return "최신 우세";
                }

                if (deltaPercent < -0.1D)
                {
                    return "기존 우세";
                }

                return "동률";
            }

            if (latestMetrics?.BoxLoss.HasValue == true && currentMetrics?.BoxLoss.HasValue == true)
            {
                double lossDelta = latestMetrics.BoxLoss.Value - currentMetrics.BoxLoss.Value;
                if (lossDelta < -0.0001D)
                {
                    return "최신 우세";
                }

                if (lossDelta > 0.0001D)
                {
                    return "기존 우세";
                }

                return "동률";
            }

            return "판정 보류";
        }

        private static string FormatMetricSnapshot(WpfTrainingRunMetrics metrics)
        {
            var parts = new List<string>();
            AddPercentSnapshot(parts, "mAP50-95", metrics?.Map5095);
            AddPercentSnapshot(parts, "mAP50", metrics?.Map50);
            AddPercentSnapshot(parts, "precision", metrics?.Precision);
            AddPercentSnapshot(parts, "recall", metrics?.Recall);
            if (metrics?.BoxLoss != null)
            {
                parts.Add($"box loss {metrics.BoxLoss.Value:0.###}");
            }

            return parts.Count == 0 ? "지표 없음" : string.Join(", ", parts);
        }

        private static void AddPercentSnapshot(List<string> parts, string name, double? value)
        {
            if (value.HasValue)
            {
                parts.Add($"{name} {FormatPercent(value.Value)}");
            }
        }

        private static void AddPercentComparison(List<string> parts, string name, double? latest, double? current)
        {
            if (!latest.HasValue || !current.HasValue)
            {
                return;
            }

            double latestPercent = ToPercentValue(latest.Value);
            double currentPercent = ToPercentValue(current.Value);
            double delta = latestPercent - currentPercent;
            parts.Add($"{name} {latestPercent:0.0}% ({delta:+0.0;-0.0;0.0}%p)");
        }

        private static void AddLossComparison(List<string> parts, string name, double? latest, double? current)
        {
            if (!latest.HasValue || !current.HasValue)
            {
                return;
            }

            double delta = latest.Value - current.Value;
            parts.Add($"{name} {latest.Value:0.###} ({delta:+0.###;-0.###;0})");
        }

        private static string FormatPercent(double value)
            => $"{ToPercentValue(value):0.0}%";

        private static double ToPercentValue(double value)
            => Math.Abs(value) <= 1.5D ? value * 100D : value;

        private static double? ReadMetric(string[] headers, string[] values, params string[] aliases)
        {
            if (headers == null || values == null || aliases == null)
            {
                return null;
            }

            string[] normalizedAliases = aliases
                .Select(NormalizeMetricHeader)
                .Where(alias => !string.IsNullOrWhiteSpace(alias))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach (string alias in normalizedAliases)
            {
                for (int index = 0; index < headers.Length && index < values.Length; index++)
                {
                    if (!string.Equals(alias, NormalizeMetricHeader(headers[index]), StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (TryParseMetricValue(values[index], out double value))
                    {
                        return value;
                    }
                }
            }

            return null;
        }

        private static string NormalizeMetricHeader(string text)
        {
            string trimmed = (text ?? string.Empty)
                .Trim()
                .Trim('"', '\'')
                .TrimStart('\ufeff');

            return new string(trimmed
                .Where(character => !char.IsWhiteSpace(character) && character != '_')
                .ToArray())
                .ToLowerInvariant();
        }

        private static bool TryParseMetricValue(string text, out double value)
        {
            text = (text ?? string.Empty).Trim();
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                || double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }

        private static string[] SplitCsvLine(string line)
            => (line ?? string.Empty).Split(',').Select(part => part.Trim()).ToArray();

        private static bool PathsEqual(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return false;
            }

            try
            {
                left = Path.GetFullPath(left.Trim());
                right = Path.GetFullPath(right.Trim());
            }
            catch (ArgumentException)
            {
                left = left.Trim();
                right = right.Trim();
            }
            catch (NotSupportedException)
            {
                left = left.Trim();
                right = right.Trim();
            }

            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }
}
