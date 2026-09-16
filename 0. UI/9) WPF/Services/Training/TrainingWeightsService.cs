using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

        public string LatestLastWeightsPath { get; set; } = "";

        public WpfTrainingRunMetrics LatestLastMetrics { get; set; }

        public WpfTrainingRunMetrics CurrentMetrics { get; set; }

        public string MetricVerdictText { get; set; } = "";

        public string MetricsStatusText { get; set; } = "";

        public bool LatestWeightsMatchesCurrentDataset { get; set; }

        public bool HasCompletedCurrentDatasetTraining
            => HasLatestWeights && LatestWeightsMatchesCurrentDataset && LatestMetrics?.HasEvaluationMetrics == true;

        public string LatestWeightsDisplayName
            => TrainingWeightsService.FormatWeightsDisplayPath(LatestWeightsPath);

        public string CurrentWeightsDisplayName
            => TrainingWeightsService.FormatWeightsDisplayPath(CurrentWeightsPath);

        public string LatestLastWeightsDisplayName
            => TrainingWeightsService.FormatWeightsDisplayPath(LatestLastWeightsPath);
    }

    public sealed class WpfTrainingRunMetrics
    {
        public string WeightsPath { get; set; } = "";

        public string ResultsCsvPath { get; set; } = "";

        public string CheckpointRole { get; set; } = "";

        public string ArtifactId { get; set; } = "";

        public string CheckpointMetadataPath { get; set; } = "";

        public bool IsCheckpointEpochMatched { get; set; }

        public string CheckpointReferenceText { get; set; } = "";

        public int Epoch { get; set; } = -1;

        public double? Precision { get; set; }

        public double? Recall { get; set; }

        public double? Map50 { get; set; }

        public double? Map5095 { get; set; }

        public double? BoxLoss { get; set; }

        public bool HasScore => Map5095.HasValue || Map50.HasValue || Precision.HasValue || Recall.HasValue;

        public bool HasEvaluationMetrics => HasScore || BoxLoss.HasValue;

        public bool HasCheckpointIdentity
            => Epoch >= 0 || !string.IsNullOrWhiteSpace(ArtifactId) || !string.IsNullOrWhiteSpace(CheckpointRole);

        public double? PrimaryScore => Map5095 ?? Map50 ?? Precision ?? Recall;
    }

    public class TrainingWeightsService
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
            TrainingWeightCandidate latestCandidate = FindLatestTrainingWeightCandidate(projectRootPath, outputRootPath);
            string latestWeightsPath = latestCandidate?.Path ?? string.Empty;
            DateTime? latestUtc = File.Exists(latestWeightsPath)
                ? File.GetLastWriteTimeUtc(latestWeightsPath)
                : null;
            DateTime? currentUtc = File.Exists(currentWeightsPath)
                ? File.GetLastWriteTimeUtc(currentWeightsPath)
                : null;
            bool shouldApply = ShouldPreferTrainingWeights(latestWeightsPath, currentWeightsPath);
            TryReadTrainingRunMetrics(latestWeightsPath, out WpfTrainingRunMetrics latestMetrics);
            string latestLastWeightsPath = TryFindSiblingWeightsPath(latestWeightsPath, "last.pt");
            TryReadTrainingRunMetrics(latestLastWeightsPath, out WpfTrainingRunMetrics latestLastMetrics);
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
                    latestCandidate?.MatchesCurrentDataset == true && latestMetrics?.HasEvaluationMetrics == true),
                LatestMetrics = latestMetrics,
                LatestLastWeightsPath = latestLastWeightsPath,
                LatestLastMetrics = latestLastMetrics,
                CurrentMetrics = currentMetrics,
                MetricVerdictText = metricVerdictText,
                MetricsStatusText = BuildMetricsStatusText(latestMetrics, currentMetrics, latestLastMetrics),
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

            // Exported training evidence can keep best.pt directly under its run folder
            // instead of the engine's conventional weights folder.  Keep that run identity
            // visible so operators do not see several indistinguishable "best.pt" entries.
            if ((string.Equals(fileName, "best.pt", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(fileName, "last.pt", StringComparison.OrdinalIgnoreCase))
                && !string.IsNullOrWhiteSpace(directory.Name))
            {
                return $"{directory.Name}{Path.DirectorySeparatorChar}{fileName}";
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
            if (string.IsNullOrWhiteSpace(weightsPath) || !File.Exists(weightsPath))
            {
                return false;
            }

            string normalizedWeightsPath = weightsPath.Trim();
            string checkpointMetadataPath = GetCheckpointMetadataPath(normalizedWeightsPath);
            bool metadataFileExists = File.Exists(checkpointMetadataPath);
            bool metadataRead = TryReadCheckpointMetadata(checkpointMetadataPath, out TrainingCheckpointMetadata metadata);
            string artifactId = ResolveArtifactId(normalizedWeightsPath, metadata?.ArtifactId, out bool artifactIdMismatch);
            string checkpointRole = NormalizeCheckpointRole(metadata?.CheckpointRole, normalizedWeightsPath);

            string resultsCsvPath = ResolveCheckpointResultsCsvPath(normalizedWeightsPath, metadataRead ? metadata.ResultsCsvPath : string.Empty);
            if (string.IsNullOrWhiteSpace(resultsCsvPath))
            {
                TryFindResultsCsvForWeights(normalizedWeightsPath, out resultsCsvPath);
            }

            if (string.IsNullOrWhiteSpace(resultsCsvPath))
            {
                if (!metadataRead)
                {
                    return false;
                }

                metrics = BuildIdentityOnlyMetrics(
                    normalizedWeightsPath,
                    checkpointMetadataPath,
                    checkpointRole,
                    artifactId,
                    metadata,
                    metadataFileExists,
                    artifactIdMismatch,
                    "results.csv 없음");
                return true;
            }

            string[] lines;
            try
            {
                lines = File.ReadAllLines(resultsCsvPath);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }

            int headerIndex = Array.FindIndex(lines, line => !string.IsNullOrWhiteSpace(line));
            if (headerIndex < 0)
            {
                return false;
            }

            string headerLine = lines[headerIndex];
            string[] headers = SplitCsvLine(headerLine);
            if (headers.Length == 0)
            {
                return false;
            }

            var valueRows = lines
                .Skip(headerIndex + 1)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => new TrainingMetricRow(line, SplitCsvLine(line)))
                .Where(row => row.Values.Length > 0)
                .ToList();
            if (valueRows.Count == 0)
            {
                if (!metadataRead)
                {
                    return false;
                }

                metrics = BuildIdentityOnlyMetrics(
                    normalizedWeightsPath,
                    checkpointMetadataPath,
                    checkpointRole,
                    artifactId,
                    metadata,
                    metadataFileExists,
                    artifactIdMismatch,
                    "results.csv 행 없음");
                metrics.ResultsCsvPath = resultsCsvPath;
                return true;
            }

            TrainingMetricRow selectedRow = valueRows[valueRows.Count - 1];
            string referenceText = BuildCheckpointReferenceText(metadata, metadataRead, metadataFileExists, artifactIdMismatch);
            if (metadataRead && metadata.Epoch.HasValue)
            {
                TrainingMetricRow matchingRow = valueRows
                    .LastOrDefault(row => TryReadEpoch(headers, row.Values, out int epoch) && epoch == metadata.Epoch.Value);
                if (matchingRow != null)
                {
                    selectedRow = matchingRow;
                    referenceText = BuildCheckpointReferenceText(metadata, metadataRead, metadataFileExists, artifactIdMismatch, matched: true);
                }
                else
                {
                    metrics = BuildIdentityOnlyMetrics(
                        normalizedWeightsPath,
                        checkpointMetadataPath,
                        checkpointRole,
                        artifactId,
                        metadata,
                        metadataFileExists,
                        artifactIdMismatch,
                        $"checkpoint epoch {metadata.Epoch.Value} results.csv 행 없음");
                    metrics.ResultsCsvPath = resultsCsvPath;
                    return true;
                }
            }

            metrics = BuildMetrics(
                normalizedWeightsPath,
                resultsCsvPath,
                checkpointMetadataPath,
                checkpointRole,
                artifactId,
                headers,
                selectedRow.Values,
                metadataRead && metadata?.Epoch.HasValue == true,
                referenceText);

            if (!metrics.HasEvaluationMetrics && metadataRead)
            {
                metrics.CheckpointReferenceText = $"{referenceText}; 평가 지표 없음/부분 행";
                return true;
            }

            return metrics.HasEvaluationMetrics;
        }

        public static string GetCheckpointMetadataPath(string weightsPath)
            => string.IsNullOrWhiteSpace(weightsPath) ? string.Empty : $"{weightsPath.Trim()}.metadata.json";

        public static string FormatCheckpointIdentity(WpfTrainingRunMetrics metrics)
        {
            if (metrics == null)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(metrics.CheckpointRole))
            {
                parts.Add(metrics.CheckpointRole);
            }

            if (metrics.Epoch >= 0)
            {
                parts.Add($"epoch {metrics.Epoch}");
            }

            if (!string.IsNullOrWhiteSpace(metrics.ArtifactId))
            {
                parts.Add($"artifact {metrics.ArtifactId}");
            }

            if (!string.IsNullOrWhiteSpace(metrics.CheckpointReferenceText))
            {
                parts.Add(metrics.CheckpointReferenceText);
            }

            return string.Join(" / ", parts);
        }

        private static WpfTrainingRunMetrics BuildMetrics(
            string weightsPath,
            string resultsCsvPath,
            string checkpointMetadataPath,
            string checkpointRole,
            string artifactId,
            string[] headers,
            string[] values,
            bool checkpointEpochPresent,
            string referenceText)
        {
            return new WpfTrainingRunMetrics
            {
                WeightsPath = weightsPath,
                ResultsCsvPath = resultsCsvPath,
                CheckpointMetadataPath = checkpointMetadataPath,
                CheckpointRole = checkpointRole,
                ArtifactId = artifactId,
                IsCheckpointEpochMatched = checkpointEpochPresent,
                CheckpointReferenceText = referenceText,
                Epoch = TryReadEpoch(headers, values, out int epoch) ? epoch : -1,
                Precision = ReadMetric(headers, values, PrecisionMetricAliases),
                Recall = ReadMetric(headers, values, RecallMetricAliases),
                Map50 = ReadMetric(headers, values, Map50MetricAliases),
                Map5095 = ReadMetric(headers, values, Map5095MetricAliases),
                BoxLoss = ReadMetric(headers, values, LossMetricAliases)
            };
        }

        private static WpfTrainingRunMetrics BuildIdentityOnlyMetrics(
            string weightsPath,
            string checkpointMetadataPath,
            string checkpointRole,
            string artifactId,
            TrainingCheckpointMetadata metadata,
            bool metadataFileExists,
            bool artifactIdMismatch,
            string referenceText)
        {
            string checkpointReferenceText = BuildCheckpointReferenceText(
                metadata,
                metadata != null,
                metadataFileExists,
                artifactIdMismatch,
                matched: false,
                fallbackText: referenceText);
            return new WpfTrainingRunMetrics
            {
                WeightsPath = weightsPath,
                CheckpointMetadataPath = checkpointMetadataPath,
                CheckpointRole = checkpointRole,
                ArtifactId = artifactId,
                Epoch = metadata?.Epoch ?? -1,
                IsCheckpointEpochMatched = false,
                CheckpointReferenceText = checkpointReferenceText
            };
        }

        private static string BuildCheckpointReferenceText(
            TrainingCheckpointMetadata metadata,
            bool metadataRead,
            bool metadataFileExists,
            bool artifactIdMismatch,
            bool matched = false,
            string fallbackText = "")
        {
            var parts = new List<string>();
            if (metadataRead && metadata?.Epoch.HasValue == true)
            {
                parts.Add(matched
                    ? "checkpoint epoch 연결"
                    : "checkpoint epoch 참고값");
            }
            else if (metadataFileExists)
            {
                parts.Add(metadataRead
                    ? "checkpoint epoch 없음; 마지막 epoch 참고값"
                    : "checkpoint metadata 읽기 실패; 마지막 epoch 참고값");
            }
            else
            {
                parts.Add("마지막 epoch 참고값 (checkpoint metadata 없음)");
            }

            if (artifactIdMismatch)
            {
                parts.Add("artifact hash 불일치; 파일 hash 사용");
            }

            if (!string.IsNullOrWhiteSpace(fallbackText))
            {
                parts.Add(fallbackText);
            }

            return string.Join("; ", parts);
        }

        private static string ResolveArtifactId(string weightsPath, string metadataArtifactId, out bool artifactIdMismatch)
        {
            artifactIdMismatch = false;
            string computedArtifactId = TryComputeArtifactId(weightsPath);
            if (string.IsNullOrWhiteSpace(metadataArtifactId))
            {
                return computedArtifactId;
            }

            string normalizedMetadataArtifactId = metadataArtifactId.Trim();
            if (!IsValidArtifactId(normalizedMetadataArtifactId))
            {
                artifactIdMismatch = true;
                return computedArtifactId;
            }

            if (!string.IsNullOrWhiteSpace(computedArtifactId)
                && !string.Equals(normalizedMetadataArtifactId, computedArtifactId, StringComparison.OrdinalIgnoreCase))
            {
                artifactIdMismatch = true;
                return computedArtifactId;
            }

            return normalizedMetadataArtifactId.ToLowerInvariant();
        }

        private static string TryComputeArtifactId(string weightsPath)
        {
            if (string.IsNullOrWhiteSpace(weightsPath) || !File.Exists(weightsPath))
            {
                return string.Empty;
            }

            try
            {
                using FileStream stream = File.OpenRead(weightsPath);
                byte[] hash = SHA256.HashData(stream);
                return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
            }
            catch (IOException)
            {
                return string.Empty;
            }
            catch (UnauthorizedAccessException)
            {
                return string.Empty;
            }
        }

        private static bool IsValidArtifactId(string artifactId)
        {
            if (string.IsNullOrWhiteSpace(artifactId)
                || artifactId.Length != "sha256:".Length + 64
                || !artifactId.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return artifactId.Substring("sha256:".Length).All(character => Uri.IsHexDigit(character));
        }

        private static string NormalizeCheckpointRole(string role, string weightsPath)
        {
            string normalized = role?.Trim().ToLowerInvariant() ?? string.Empty;
            if (normalized is "best" or "last")
            {
                return normalized;
            }

            string fileName = Path.GetFileName(weightsPath ?? string.Empty).ToLowerInvariant();
            return fileName switch
            {
                "best.pt" => "best",
                "last.pt" => "last",
                _ => string.Empty
            };
        }

        private static string ResolveCheckpointResultsCsvPath(string weightsPath, string metadataResultsCsvPath)
        {
            if (string.IsNullOrWhiteSpace(metadataResultsCsvPath))
            {
                return string.Empty;
            }

            string candidate = metadataResultsCsvPath.Trim();
            try
            {
                if (!Path.IsPathRooted(candidate))
                {
                    string weightsDirectory = Path.GetDirectoryName(weightsPath?.Trim() ?? string.Empty) ?? string.Empty;
                    candidate = Path.GetFullPath(Path.Combine(weightsDirectory, candidate));
                }
                else
                {
                    candidate = Path.GetFullPath(candidate);
                }
            }
            catch (ArgumentException)
            {
                return string.Empty;
            }
            catch (NotSupportedException)
            {
                return string.Empty;
            }

            return File.Exists(candidate) ? candidate : string.Empty;
        }

        private static bool TryReadCheckpointMetadata(string metadataPath, out TrainingCheckpointMetadata metadata)
        {
            metadata = null;
            if (string.IsNullOrWhiteSpace(metadataPath) || !File.Exists(metadataPath))
            {
                return false;
            }

            try
            {
                JObject root = JObject.Parse(File.ReadAllText(metadataPath));
                string role = (root["checkpointRole"] ?? root["role"])?.Value<string>()?.Trim() ?? string.Empty;
                int? epoch = TryReadInteger(root["epoch"] ?? root["checkpointEpoch"]);
                string artifactId = (root["artifactId"] ?? root["artifactID"] ?? root["sha256"])?.Value<string>()?.Trim() ?? string.Empty;
                metadata = new TrainingCheckpointMetadata
                {
                    CheckpointRole = role,
                    Epoch = epoch,
                    ArtifactId = artifactId,
                    ResultsCsvPath = (root["resultsCsvPath"] ?? root["resultsCSVPath"])?.Value<string>()?.Trim() ?? string.Empty
                };
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is JsonException)
            {
                return false;
            }
        }

        private static int? TryReadInteger(JToken token)
        {
            if (token == null)
            {
                return null;
            }

            if (token.Type == JTokenType.Integer)
            {
                int integerValue = token.Value<int>();
                return integerValue >= 0 ? integerValue : null;
            }

            return int.TryParse(token.Value<string>(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) && value >= 0
                ? value
                : null;
        }

        private static bool TryReadEpoch(string[] headers, string[] values, out int epoch)
        {
            double? value = ReadMetric(headers, values, "epoch");
            if (value.HasValue && value.Value >= int.MinValue && value.Value <= int.MaxValue)
            {
                epoch = (int)value.Value;
                return true;
            }

            epoch = -1;
            return false;
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

        private static string TryFindSiblingWeightsPath(string weightsPath, string fileName)
        {
            if (string.IsNullOrWhiteSpace(weightsPath) || string.IsNullOrWhiteSpace(fileName))
            {
                return string.Empty;
            }

            string directoryPath = Path.GetDirectoryName(weightsPath.Trim());
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return string.Empty;
            }

            string siblingPath = Path.Combine(directoryPath, fileName);
            return File.Exists(siblingPath) ? siblingPath : string.Empty;
        }

        private TrainingWeightCandidate FindLatestTrainingWeightCandidate(string projectRootPath, string outputRootPath)
        {
            List<TrainingWeightCandidate> candidates = EnumerateBestWeightCandidates(projectRootPath)
                .Concat(EnumerateBestWeightCandidates(outputRootPath))
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(path => new TrainingWeightCandidate
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

        private static string BuildMetricsStatusText(
            WpfTrainingRunMetrics latestMetrics,
            WpfTrainingRunMetrics currentMetrics,
            WpfTrainingRunMetrics latestLastMetrics)
        {
            if (latestMetrics == null || !latestMetrics.HasEvaluationMetrics)
            {
                string identity = FormatCheckpointIdentity(latestMetrics);
                string status = string.IsNullOrWhiteSpace(identity)
                    ? "지표 없음: 학습 실패 아님, 후보 검증 후 저장 판단(results.csv 없음)"
                    : $"지표 없음: 학습 실패 아님, 후보 검증 후 저장 판단 / {identity}";
                return AppendLatestLastSummary(status, latestMetrics, null, latestLastMetrics);
            }

            if (currentMetrics == null || !currentMetrics.HasEvaluationMetrics)
            {
                return AppendLatestLastSummary($"새 후보 지표: {FormatMetricSnapshot(latestMetrics)}", latestMetrics, null, latestLastMetrics);
            }

            var parts = new List<string>();
            AddPercentComparison(parts, "mAP50-95", latestMetrics.Map5095, currentMetrics.Map5095);
            AddPercentComparison(parts, "mAP50", latestMetrics.Map50, currentMetrics.Map50);
            AddPercentComparison(parts, "precision", latestMetrics.Precision, currentMetrics.Precision);
            AddPercentComparison(parts, "recall", latestMetrics.Recall, currentMetrics.Recall);
            AddLossComparison(parts, "loss", latestMetrics.BoxLoss, currentMetrics.BoxLoss);

            string comparisonText = parts.Count == 0
                ? $"지표 비교(판정 보류: 공통 지표 없음): {FormatMetricSnapshot(latestMetrics)}"
                : $"지표 비교({BuildMetricVerdictText(latestMetrics, currentMetrics)}): {string.Join(", ", parts)}";
            return AppendLatestLastSummary(comparisonText, latestMetrics, currentMetrics, latestLastMetrics);
        }

        private static string AppendLatestLastSummary(
            string text,
            WpfTrainingRunMetrics latestMetrics,
            WpfTrainingRunMetrics currentMetrics,
            WpfTrainingRunMetrics latestLastMetrics)
        {
            var summaries = new List<string>();
            string latestIdentity = FormatCheckpointIdentity(latestMetrics);
            if (!string.IsNullOrWhiteSpace(latestIdentity)
                && !text.Contains(latestIdentity, StringComparison.Ordinal))
            {
                summaries.Add($"best {latestIdentity}");
            }

            string lastIdentity = FormatCheckpointIdentity(latestLastMetrics);
            if (!string.IsNullOrWhiteSpace(lastIdentity))
            {
                summaries.Add($"last {lastIdentity}");
            }

            string currentIdentity = FormatCheckpointIdentity(currentMetrics);
            if (!string.IsNullOrWhiteSpace(currentIdentity))
            {
                summaries.Add($"현재 {currentIdentity}");
            }

            return summaries.Count == 0 ? text : $"{text} / {string.Join(" / ", summaries)}";
        }

        private static string BuildMetricVerdictText(WpfTrainingRunMetrics latestMetrics, WpfTrainingRunMetrics currentMetrics)
        {
            // Compare only a metric that both snapshots actually recorded.  A
            // precision value must not be compared with a mAP value merely because
            // it is the first available score in either snapshot.
            double? latestCommonScore = null;
            double? currentCommonScore = null;
            if (latestMetrics?.Map5095.HasValue == true && currentMetrics?.Map5095.HasValue == true)
            {
                latestCommonScore = latestMetrics.Map5095;
                currentCommonScore = currentMetrics.Map5095;
            }
            else if (latestMetrics?.Map50.HasValue == true && currentMetrics?.Map50.HasValue == true)
            {
                latestCommonScore = latestMetrics.Map50;
                currentCommonScore = currentMetrics.Map50;
            }
            else if (latestMetrics?.Precision.HasValue == true && currentMetrics?.Precision.HasValue == true)
            {
                latestCommonScore = latestMetrics.Precision;
                currentCommonScore = currentMetrics.Precision;
            }
            else if (latestMetrics?.Recall.HasValue == true && currentMetrics?.Recall.HasValue == true)
            {
                latestCommonScore = latestMetrics.Recall;
                currentCommonScore = currentMetrics.Recall;
            }

            if (latestCommonScore.HasValue && currentCommonScore.HasValue)
            {
                double deltaPercent = ToPercentValue(latestCommonScore.Value) - ToPercentValue(currentCommonScore.Value);
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
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && double.IsFinite(value))
            {
                return true;
            }

            if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
                && double.IsFinite(value))
            {
                return true;
            }

            value = default;
            return false;
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

        private sealed class TrainingWeightCandidate
        {
            public string Path { get; set; } = string.Empty;

            public DateTime LastWriteUtc { get; set; }

            public bool MatchesCurrentDataset { get; set; }
        }

        private sealed class TrainingCheckpointMetadata
        {
            public string CheckpointRole { get; set; } = string.Empty;

            public int? Epoch { get; set; }

            public string ArtifactId { get; set; } = string.Empty;

            public string ResultsCsvPath { get; set; } = string.Empty;
        }

        private sealed class TrainingMetricRow
        {
            public TrainingMetricRow(string line, string[] values)
            {
                Line = line;
                Values = values ?? Array.Empty<string>();
            }

            public string Line { get; }

            public string[] Values { get; }
        }
    }

    [Obsolete("Use TrainingWeightsService.", false)]
    public sealed class WpfTrainingWeightsService : TrainingWeightsService
    {
    }
}
