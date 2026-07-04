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

        public bool LatestWeightsMatchesCurrentDataset { get; set; }

        public bool HasCompletedCurrentDatasetTraining
            => HasLatestWeights && LatestWeightsMatchesCurrentDataset && LatestMetrics != null;

        public string LatestWeightsDisplayName
            => WpfTrainingWeightsService.FormatWeightsDisplayPath(LatestWeightsPath);

        public string CurrentWeightsDisplayName
            => WpfTrainingWeightsService.FormatWeightsDisplayPath(CurrentWeightsPath);
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
            "metrics/precision(m)",
            "metrics/precision(b)",
            "precision",
            "precision(m)",
            "precision(b)",
            "p",
            "p(m)",
            "p(b)"
        };

        private static readonly string[] RecallMetricAliases =
        {
            "metrics/recall",
            "metrics/recall(m)",
            "metrics/recall(b)",
            "recall",
            "recall(m)",
            "recall(b)",
            "r",
            "r(m)",
            "r(b)"
        };

        private static readonly string[] Map50MetricAliases =
        {
            "metrics/map_0.5",
            "metrics/mAP_0.5",
            "metrics/map50",
            "metrics/mAP50",
            "metrics/map50(m)",
            "metrics/mAP50(M)",
            "metrics/map50(b)",
            "metrics/mAP50(B)",
            "map_0.5",
            "mAP_0.5",
            "map50",
            "mAP50",
            "map50(m)",
            "mAP50(M)",
            "map50(b)",
            "mAP50(B)",
            "map@50",
            "mAP@0.5"
        };

        private static readonly string[] Map5095MetricAliases =
        {
            "metrics/map_0.5:0.95",
            "metrics/mAP_0.5:0.95",
            "metrics/map50-95",
            "metrics/mAP50-95",
            "metrics/map50-95(m)",
            "metrics/mAP50-95(M)",
            "metrics/map50-95(b)",
            "metrics/mAP50-95(B)",
            "map_0.5:0.95",
            "mAP_0.5:0.95",
            "map50-95",
            "mAP50-95",
            "map50-95(m)",
            "mAP50-95(M)",
            "map50-95(b)",
            "mAP50-95(B)",
            "map@50-95",
            "mAP@0.5:0.95"
        };

        private static readonly string[] LossMetricAliases =
        {
            "val/seg_loss",
            "val/box_loss",
            "val/box_loss(m)",
            "val/box_loss(b)",
            "train/seg_loss",
            "train/box_loss",
            "train/box_loss(m)",
            "train/box_loss(b)",
            "seg_loss",
            "box_loss",
            "box_loss(m)",
            "box_loss(b)"
        };

        public bool TryFindLatestTrainingWeights(string projectRootPath, string outputRootPath, out string latestWeightsPath)
        {
            latestWeightsPath = FindLatestTrainingWeightCandidate(projectRootPath, outputRootPath)?.Path ?? string.Empty;

            return !string.IsNullOrWhiteSpace(latestWeightsPath);
        }

        public IReadOnlyList<string> EnumerateBestWeightCandidates(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            {
                return Array.Empty<string>();
            }

            var candidates = new List<string>();
            foreach (string candidateRootPath in EnumerateTrainingWeightRoots(rootPath))
            {
                candidates.Add(Path.Combine(candidateRootPath, "best.pt"));

                foreach (string runsRoot in EnumerateTrainingRunRoots(candidateRootPath))
                {
                    candidates.Add(Path.Combine(runsRoot, "weights", "best.pt"));
                    foreach (string runDirectory in Directory.EnumerateDirectories(runsRoot))
                    {
                        candidates.Add(Path.Combine(runDirectory, "weights", "best.pt"));
                    }
                }
            }

            return candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        public WpfTrainingWeightsComparison BuildComparison(string projectRootPath, string outputRootPath, string currentWeightsPath)
        {
            currentWeightsPath = currentWeightsPath?.Trim() ?? string.Empty;
            WpfTrainingWeightCandidate latestCandidate = FindLatestTrainingWeightCandidate(projectRootPath, outputRootPath);
            string latestWeightsPath = latestCandidate?.Path ?? string.Empty;
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
                StatusText = BuildComparisonStatusText(
                    latestWeightsPath,
                    currentWeightsPath,
                    latestUtc,
                    currentUtc,
                    shouldApply,
                    latestCandidate?.MatchesCurrentDataset == true && latestMetrics != null),
                LatestMetrics = latestMetrics,
                CurrentMetrics = currentMetrics,
                MetricVerdictText = metricVerdictText,
                MetricsStatusText = BuildMetricsStatusText(latestMetrics, currentMetrics),
                LatestWeightsMatchesCurrentDataset = latestCandidate?.MatchesCurrentDataset == true
            };
        }

        public static string FormatWeightsDisplayPath(string weightsPath)
        {
            if (string.IsNullOrWhiteSpace(weightsPath))
            {
                return string.Empty;
            }

            string trimmed = weightsPath.Trim();
            string fileName = Path.GetFileName(trimmed);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return trimmed;
            }

            string directoryPath = Path.GetDirectoryName(trimmed);
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return fileName;
            }

            var directory = new DirectoryInfo(directoryPath);
            if (string.Equals(directory.Name, "weights", StringComparison.OrdinalIgnoreCase)
                && directory.Parent != null
                && !string.IsNullOrWhiteSpace(directory.Parent.Name))
            {
                return $"{directory.Parent.Name}{Path.DirectorySeparatorChar}{fileName}";
            }

            return fileName;
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
                BoxLoss = ReadMetric(headers, values, LossMetricAliases)
            };

            return metrics.HasScore || metrics.BoxLoss.HasValue;
        }

        private static string BuildComparisonStatusText(
            string latestWeightsPath,
            string currentWeightsPath,
            DateTime? latestUtc,
            DateTime? currentUtc,
            bool shouldApply,
            bool latestMatchesCurrentDataset)
        {
            if (string.IsNullOrWhiteSpace(latestWeightsPath))
            {
                return "학습 결과 모델 후보 없음";
            }

            string latestName = FormatWeightsDisplayPath(latestWeightsPath);
            string currentName = FormatWeightsDisplayPath(currentWeightsPath);
            if (PathsEqual(latestWeightsPath, currentWeightsPath))
            {
                return latestMatchesCurrentDataset
                    ? $"현재 데이터셋 학습 완료: {latestName} (현재 검사 모델)"
                    : $"현재 검사 모델: {latestName}";
            }

            if (shouldApply)
            {
                string currentText = currentUtc.HasValue ? $" / 현재 {currentName} {currentUtc.Value:yyyy-MM-dd HH:mm}Z" : string.Empty;
                string latestText = latestUtc.HasValue ? $" ({latestUtc.Value:yyyy-MM-dd HH:mm}Z)" : string.Empty;
                string prefix = latestMatchesCurrentDataset ? "현재 데이터셋 학습 완료" : "새 학습 모델 후보";
                return $"{prefix}: {latestName}{latestText}{currentText}";
            }

            return string.IsNullOrWhiteSpace(currentWeightsPath)
                ? $"학습 모델 후보 사용 불가: {latestName}"
                : latestMatchesCurrentDataset
                    ? $"현재 데이터셋 학습 완료: {latestName} / 현재 검사 모델 유지: {currentName}"
                    : $"현재 검사 모델 유지: {currentName}";
        }

        private static string BuildComparisonStatusText(
            string latestWeightsPath,
            string currentWeightsPath,
            DateTime? latestUtc,
            DateTime? currentUtc,
            bool shouldApply)
        {
            if (string.IsNullOrWhiteSpace(latestWeightsPath))
            {
                return "학습 결과 모델 후보 없음";
            }

            string latestName = Path.GetFileName(latestWeightsPath);
            if (PathsEqual(latestWeightsPath, currentWeightsPath))
            {
                return $"현재 검사 모델: {latestName}";
            }

            if (shouldApply)
            {
                string currentText = currentUtc.HasValue ? $" / 현재 {currentUtc.Value:yyyy-MM-dd HH:mm}Z" : string.Empty;
                string latestText = latestUtc.HasValue ? $" ({latestUtc.Value:yyyy-MM-dd HH:mm}Z)" : string.Empty;
                return $"새 학습 모델 후보: {latestName}{latestText}{currentText}";
            }

            return string.IsNullOrWhiteSpace(currentWeightsPath)
                ? $"학습 모델 후보 사용 불가: {latestName}"
                : $"현재 검사 모델 유지: {Path.GetFileName(currentWeightsPath)}";
        }

        private WpfTrainingWeightCandidate FindLatestTrainingWeightCandidate(string projectRootPath, string outputRootPath)
        {
            List<WpfTrainingWeightCandidate> candidates = EnumerateBestWeightCandidates(projectRootPath)
                .Concat(EnumerateBestWeightCandidates(outputRootPath))
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(path => new WpfTrainingWeightCandidate
                {
                    Path = path,
                    LastWriteUtc = File.GetLastWriteTimeUtc(path),
                    MatchesCurrentDataset = IsTrainingWeightsForOutputRoot(path, outputRootPath)
                })
                .ToList();

            if (candidates.Count == 0)
            {
                return null;
            }

            return candidates
                .Where(candidate => candidate.MatchesCurrentDataset)
                .OrderByDescending(candidate => candidate.LastWriteUtc)
                .FirstOrDefault()
                ?? candidates
                    .OrderByDescending(candidate => candidate.LastWriteUtc)
                    .FirstOrDefault();
        }

        private static IReadOnlyList<string> EnumerateTrainingWeightRoots(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            {
                return Array.Empty<string>();
            }

            var roots = new List<string> { rootPath };
            string nestedYoloV5Root = Path.Combine(rootPath, "yolov5Master");
            if (Directory.Exists(nestedYoloV5Root)
                && !roots.Any(root => PathsEqual(root, nestedYoloV5Root)))
            {
                roots.Add(nestedYoloV5Root);
            }

            return roots;
        }

        private static IEnumerable<string> EnumerateTrainingRunRoots(string candidateRootPath)
        {
            foreach (string runKind in new[] { "train", "segment" })
            {
                string runsRoot = Path.Combine(candidateRootPath ?? string.Empty, "runs", runKind);
                if (Directory.Exists(runsRoot))
                {
                    yield return runsRoot;
                }
            }
        }

        private static bool IsTrainingWeightsForOutputRoot(string weightsPath, string outputRootPath)
        {
            string expectedDataYamlPath = ResolveOutputDataYamlPath(outputRootPath);
            if (string.IsNullOrWhiteSpace(weightsPath) || string.IsNullOrWhiteSpace(expectedDataYamlPath))
            {
                return false;
            }

            if (IsPathUnderDirectory(weightsPath, outputRootPath))
            {
                return true;
            }

            if (!TryFindTrainingRunDirectory(weightsPath, out string runDirectoryPath))
            {
                return false;
            }

            if (!TryReadTrainingOptDataPath(runDirectoryPath, out string optDataPath))
            {
                return false;
            }

            return PathsEqual(optDataPath, expectedDataYamlPath)
                || PathsEqual(optDataPath, outputRootPath);
        }

        private static string ResolveOutputDataYamlPath(string outputRootPath)
        {
            if (string.IsNullOrWhiteSpace(outputRootPath))
            {
                return string.Empty;
            }

            string trimmed = outputRootPath.Trim();
            string extension = Path.GetExtension(trimmed);
            if (string.Equals(extension, ".yaml", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".yml", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }

            return Path.Combine(trimmed, "data.yaml");
        }

        private static bool TryFindTrainingRunDirectory(string weightsPath, out string runDirectoryPath)
        {
            runDirectoryPath = string.Empty;
            string weightsDirectoryPath = Path.GetDirectoryName(weightsPath?.Trim() ?? string.Empty);
            if (string.IsNullOrWhiteSpace(weightsDirectoryPath) || !Directory.Exists(weightsDirectoryPath))
            {
                return false;
            }

            var weightsDirectory = new DirectoryInfo(weightsDirectoryPath);
            runDirectoryPath = string.Equals(weightsDirectory.Name, "weights", StringComparison.OrdinalIgnoreCase)
                && weightsDirectory.Parent != null
                    ? weightsDirectory.Parent.FullName
                    : weightsDirectory.FullName;

            return Directory.Exists(runDirectoryPath);
        }

        private static bool TryReadTrainingOptDataPath(string runDirectoryPath, out string dataPath)
        {
            dataPath = string.Empty;
            foreach (string metadataPath in EnumerateTrainingMetadataPaths(runDirectoryPath))
            {
                foreach (string line in File.ReadLines(metadataPath))
                {
                    string trimmed = line?.Trim() ?? string.Empty;
                    if (!trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string value = trimmed.Substring("data:".Length).Trim().Trim('"', '\'');
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return false;
                    }

                    dataPath = Path.IsPathRooted(value)
                        ? value
                        : Path.GetFullPath(Path.Combine(runDirectoryPath, value));
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<string> EnumerateTrainingMetadataPaths(string runDirectoryPath)
        {
            if (string.IsNullOrWhiteSpace(runDirectoryPath))
            {
                yield break;
            }

            foreach (string fileName in new[] { "opt.yaml", "args.yaml" })
            {
                string path = Path.Combine(runDirectoryPath, fileName);
                if (File.Exists(path))
                {
                    yield return path;
                }
            }
        }

        private static bool IsPathUnderDirectory(string path, string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
            {
                return false;
            }

            try
            {
                string fullPath = Path.GetFullPath(path);
                string fullDirectoryPath = Path.GetFullPath(directoryPath);
                string relativePath = Path.GetRelativePath(fullDirectoryPath, fullPath);
                return !relativePath.StartsWith("..", StringComparison.Ordinal)
                    && !Path.IsPathRooted(relativePath);
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
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
                return "지표 없음: 학습 실패 아님, 후보 검증 후 저장 판단(results.csv 없음)";
            }

            if (currentMetrics == null || !currentMetrics.HasScore)
            {
                return $"새 후보 지표: {FormatMetricSnapshot(latestMetrics)}";
            }

            var parts = new List<string>();
            AddPercentComparison(parts, "mAP50-95", latestMetrics.Map5095, currentMetrics.Map5095);
            AddPercentComparison(parts, "mAP50", latestMetrics.Map50, currentMetrics.Map50);
            AddPercentComparison(parts, "precision", latestMetrics.Precision, currentMetrics.Precision);
            AddPercentComparison(parts, "recall", latestMetrics.Recall, currentMetrics.Recall);
            AddLossComparison(parts, "loss", latestMetrics.BoxLoss, currentMetrics.BoxLoss);

            return parts.Count == 0
                ? $"새 후보 지표: {FormatMetricSnapshot(latestMetrics)}"
                : $"지표 비교({BuildMetricVerdictText(latestMetrics, currentMetrics)}): {string.Join(", ", parts)}";
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
                    return "새 모델 우세";
                }

                if (deltaPercent < -0.1D)
                {
                    return "현재 모델 우세";
                }

                return "동률";
            }

            if (latestMetrics?.BoxLoss.HasValue == true && currentMetrics?.BoxLoss.HasValue == true)
            {
                double lossDelta = latestMetrics.BoxLoss.Value - currentMetrics.BoxLoss.Value;
                if (lossDelta < -0.0001D)
                {
                    return "새 모델 우세";
                }

                if (lossDelta > 0.0001D)
                {
                    return "현재 모델 우세";
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
                parts.Add($"loss {metrics.BoxLoss.Value:0.###}");
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

        private sealed class WpfTrainingWeightCandidate
        {
            public string Path { get; set; } = string.Empty;

            public DateTime LastWriteUtc { get; set; }

            public bool MatchesCurrentDataset { get; set; }
        }
    }
}
