using MvcVisionSystem._3._Communication.TCP;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MvcVisionSystem
{
    public sealed class WpfTrainingGuideHistoryService
    {
        public const int RunHistoryLimit = 8;

        public void UpdateDatasetHistory(
            YoloTrainingGuideHistory history,
            bool isReady,
            string issueKind,
            string summary,
            bool recordHistory)
        {
            if (history == null)
            {
                return;
            }

            history.EnsureDefaults();
            history.LastDatasetCheckUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            history.LastDatasetReady = isReady;
            history.LastDatasetIssueKind = issueKind ?? string.Empty;
            history.LastDatasetSummary = summary ?? string.Empty;

            if (recordHistory)
            {
                AddRunHistoryRecord(history, "DatasetCheck");
            }
        }

        public void UpdateTrainingHistory(
            YoloTrainingGuideHistory history,
            PythonCommunicationStatus status,
            Func<string, bool> isTerminalTrainingState,
            ref string lastRecordedRunSignature)
        {
            if (history == null || status == null)
            {
                return;
            }

            history.EnsureDefaults();
            history.LastTrainingUpdateUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            history.LastTrainingState = status.LastTrainingState ?? string.Empty;
            history.LastTrainingProgressPercent = status.LastTrainingProgressPercent ?? -1;
            history.LastTrainingMessage = BuildTrainingHistoryMessage(status);

            bool isTerminal = isTerminalTrainingState?.Invoke(history.LastTrainingState) == true;
            if (!isTerminal)
            {
                return;
            }

            string signature = $"{history.LastTrainingState}|{history.LastTrainingProgressPercent}|{history.LastTrainingMessage}";
            if (string.Equals(lastRecordedRunSignature, signature, StringComparison.Ordinal))
            {
                return;
            }

            lastRecordedRunSignature = signature;
            AddRunHistoryRecord(history, "TrainingState");
        }

        public void UpdateAppliedWeightsHistory(YoloTrainingGuideHistory history, string weightsPath, bool savedToRecipe)
        {
            if (history == null)
            {
                return;
            }

            history.EnsureDefaults();
            history.AppliedWeightsPath = weightsPath ?? string.Empty;
            history.AppliedWeightsUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            history.AppliedWeightsSavedToRecipe = savedToRecipe;
            UpsertAppliedWeightsRunHistory(history, weightsPath, savedToRecipe);
        }

        public IReadOnlyList<string> BuildRunHistoryItems(YoloTrainingGuideHistory history, Func<string, string> formatTrainingState, int maxItems = 5)
        {
            return (history?.RunHistory ?? new List<YoloTrainingGuideRunRecord>())
                .Where(item => item != null)
                .OrderByDescending(item => ParseHistoryUtc(item.EventUtc))
                .Take(Math.Max(0, maxItems))
                .Select(item => FormatRunHistoryItem(item, formatTrainingState))
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList();
        }

        public string BuildHistoryText(YoloTrainingGuideHistory history, Func<string, string> formatTrainingState)
        {
            if (history == null)
            {
                return "최근 학습 이력: 아직 없습니다.";
            }

            history.EnsureDefaults();
            var parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(history.LastDatasetCheckUtc))
            {
                string readyText = history.LastDatasetReady ? "학습 가능" : $"확인 필요({history.LastDatasetIssueKind})";
                parts.Add($"점검 {FormatHistoryTime(history.LastDatasetCheckUtc)} {readyText}");
            }

            if (!string.IsNullOrWhiteSpace(history.LastTrainingState))
            {
                string progress = history.LastTrainingProgressPercent >= 0
                    ? $" {Math.Clamp(history.LastTrainingProgressPercent, 0, 100)}%"
                    : string.Empty;
                parts.Add($"학습 {FormatTrainingState(formatTrainingState, history.LastTrainingState)}{progress}");
            }

            if (IsFailedTrainingState(history.LastTrainingState)
                && !string.IsNullOrWhiteSpace(history.LastTrainingMessage))
            {
                parts.Add(history.LastTrainingMessage.Trim());
            }

            if (!string.IsNullOrWhiteSpace(history.AppliedWeightsPath))
            {
                string savedText = history.AppliedWeightsSavedToRecipe ? "recipe 저장됨" : "recipe 미저장";
                parts.Add($"weight {WpfTrainingWeightsService.FormatWeightsDisplayPath(history.AppliedWeightsPath)} / {savedText}");
            }

            return parts.Count == 0
                ? "최근 학습 이력: 아직 없습니다."
                : $"최근 이력: {string.Join(" · ", parts)}";
        }

        private void AddRunHistoryRecord(YoloTrainingGuideHistory history, string eventKind)
        {
            history.EnsureDefaults();
            history.RunHistory.Add(CreateRunHistoryRecord(history, eventKind));
            TrimRunHistory(history);
        }

        private void UpsertAppliedWeightsRunHistory(YoloTrainingGuideHistory history, string weightsPath, bool savedToRecipe)
        {
            history.EnsureDefaults();
            string normalizedPath = weightsPath ?? string.Empty;
            YoloTrainingGuideRunRecord existing = history.RunHistory
                .LastOrDefault(item =>
                    string.Equals(item.EventKind, "Weight", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(item.AppliedWeightsPath ?? string.Empty, normalizedPath, StringComparison.OrdinalIgnoreCase));

            if (existing == null)
            {
                history.RunHistory.Add(CreateRunHistoryRecord(history, "Weight"));
            }
            else
            {
                existing.EventUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                existing.AppliedWeightsSavedToRecipe = savedToRecipe;
                existing.TrainingState = history.LastTrainingState ?? string.Empty;
                existing.TrainingProgressPercent = history.LastTrainingProgressPercent;
                existing.TrainingMessage = history.LastTrainingMessage ?? string.Empty;
            }

            TrimRunHistory(history);
        }

        private static YoloTrainingGuideRunRecord CreateRunHistoryRecord(YoloTrainingGuideHistory history, string eventKind)
        {
            return new YoloTrainingGuideRunRecord
            {
                EventUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                EventKind = eventKind ?? string.Empty,
                DatasetReady = history.LastDatasetReady,
                DatasetIssueKind = history.LastDatasetIssueKind ?? string.Empty,
                DatasetSummary = history.LastDatasetSummary ?? string.Empty,
                TrainingState = history.LastTrainingState ?? string.Empty,
                TrainingProgressPercent = history.LastTrainingProgressPercent,
                TrainingMessage = history.LastTrainingMessage ?? string.Empty,
                AppliedWeightsPath = history.AppliedWeightsPath ?? string.Empty,
                AppliedWeightsSavedToRecipe = history.AppliedWeightsSavedToRecipe
            };
        }

        private static void TrimRunHistory(YoloTrainingGuideHistory history)
        {
            if (history?.RunHistory == null)
            {
                return;
            }

            while (history.RunHistory.Count > RunHistoryLimit)
            {
                history.RunHistory.RemoveAt(0);
            }
        }

        private static string FormatRunHistoryItem(YoloTrainingGuideRunRecord record, Func<string, string> formatTrainingState)
        {
            string time = FormatHistoryTime(record?.EventUtc);
            return (record?.EventKind ?? string.Empty) switch
            {
                "DatasetCheck" => $"{time} 점검: {(record.DatasetReady ? "학습 가능" : $"확인 필요 {record.DatasetIssueKind}")}",
                "TrainingState" => $"{time} 학습: {FormatTrainingState(formatTrainingState, record.TrainingState)} {FormatHistoryProgress(record.TrainingProgressPercent)}".TrimEnd(),
                "Weight" => $"{time} weight: {Path.GetFileName(record.AppliedWeightsPath)} / {(record.AppliedWeightsSavedToRecipe ? "recipe 저장됨" : "recipe 미저장")}",
                _ => $"{time} 기록: {record?.EventKind}"
            };
        }

        private static string FormatTrainingState(Func<string, string> formatTrainingState, string state)
        {
            return formatTrainingState?.Invoke(state) ?? state ?? string.Empty;
        }

        private static string BuildTrainingHistoryMessage(PythonCommunicationStatus status)
        {
            if (status == null)
            {
                return string.Empty;
            }

            string state = status.LastTrainingState?.Trim() ?? string.Empty;
            string message = IsFailedTrainingState(state) && !string.IsNullOrWhiteSpace(status.LastError)
                ? status.LastError
                : status.LastTrainingMessage ?? string.Empty;
            string displayMessage = WpfTrainingProgressPresentationService.FormatTrainingMessage(message);
            if (string.IsNullOrWhiteSpace(displayMessage))
            {
                return string.Empty;
            }

            if (message.Contains("TrainingWeightDownloadRequired", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(status.LastTrainingWeightsPath))
            {
                string weightName = Path.GetFileName(status.LastTrainingWeightsPath.Trim());
                if (!string.IsNullOrWhiteSpace(weightName))
                {
                    return $"{displayMessage} ({weightName})";
                }
            }

            return displayMessage;
        }

        private static bool IsFailedTrainingState(string state)
        {
            string normalized = state?.Trim() ?? string.Empty;
            return string.Equals(normalized, "failed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "error", StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatHistoryProgress(int progressPercent)
        {
            return progressPercent >= 0
                ? $"{Math.Clamp(progressPercent, 0, 100)}%"
                : string.Empty;
        }

        private static DateTime ParseHistoryUtc(string utcText)
        {
            return DateTime.TryParse(
                utcText,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out DateTime utc)
                ? utc
                : DateTime.MinValue;
        }

        private static string FormatHistoryTime(string utcText)
        {
            if (DateTime.TryParse(
                utcText,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out DateTime utc))
            {
                return utc.ToLocalTime().ToString("HH:mm", CultureInfo.CurrentCulture);
            }

            return "-";
        }
    }
}
