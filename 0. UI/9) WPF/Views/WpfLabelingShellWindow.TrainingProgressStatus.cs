using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Worker progress polling is isolated from readiness binding so live YOLO state updates can be changed without touching setup UI.
        private void UpdateTrainingProgressFromWorker()
        {
            PythonCommunicationStatus status = global.GetPythonCommunicationStatusSnapshot();
            bool hasStatus = HasTrainingStatus(status);
            if (status.LastTrainingProgressPercent.HasValue)
            {
                SetTrainingProgressValue(Math.Clamp(status.LastTrainingProgressPercent.Value, 0, 100));
            }
            else if (!isTrainingCommandRunning)
            {
                SetTrainingProgressValue(0);
            }

            if (hasStatus)
            {
                SetTrainingProgressStatus(
                    BuildTrainingProgressSummary(status),
                    BuildTrainingEpochSummary(status),
                    TrainingSettingsViewModel?.TrainingProgressValue ?? TrainingProgressBar?.Value ?? 0D,
                    isIndeterminate: false);
                UpdateYoloTrainingGuideTrainingHistory(status);
                if (WpfTrainingWeightsService.IsCompletedTrainingState(status.LastTrainingState))
                {
                    TryApplyLatestTrainingWeightsFromProject(logIfUnchanged: false);
                }
            }
            else if (!isTrainingCommandRunning)
            {
                SetTrainingProgressStatus("\uD559\uC2B5 \uB300\uAE30", string.Empty, 0D, isIndeterminate: false);
            }

            UpdateTrainingStatusVisual(status, lastYoloTrainingReadinessReport);
            RefreshYoloTrainingStepCompletion();
            if (hasStatus && IsTerminalTrainingState(status.LastTrainingState))
            {
                StopTrainingStatusPolling();
            }
        }

        private void StartTrainingStatusPolling()
        {
            trainingStatusPollStartedUtc = DateTime.UtcNow;
            if (!trainingStatusPollTimer.IsEnabled)
            {
                trainingStatusPollTimer.Start();
            }
        }

        private void StopTrainingStatusPolling()
        {
            if (trainingStatusPollTimer.IsEnabled)
            {
                trainingStatusPollTimer.Stop();
            }
        }

        private void TrainingStatusPollTimer_Tick(object sender, EventArgs e)
        {
            PythonCommunicationStatus status = global.GetPythonCommunicationStatusSnapshot();
            UpdateTrainingProgressFromWorker();
            if (HasTrainingStatus(status) && IsTerminalTrainingState(status.LastTrainingState))
            {
                StopTrainingStatusPolling();
                return;
            }

            if (!HasTrainingStatus(status)
                && trainingStatusPollStartedUtc != DateTime.MinValue
                && DateTime.UtcNow - trainingStatusPollStartedUtc > TimeSpan.FromSeconds(TrainingStatusPollTimeoutSeconds))
            {
                StopTrainingStatusPolling();
            }
        }

        private void UpdateTrainingStatusVisual(PythonCommunicationStatus status, YoloDatasetReadinessReport report = null)
        {
            MediaBrush readinessBrush = report == null
                ? ResolveBrushResource("SecondaryTextBrush", MediaBrushes.Gray)
                : report.IsReady
                    ? MediaBrushes.LimeGreen
                    : MediaBrushes.DarkOrange;
            MediaBrush stateBrush = ResolveTrainingStateBrush(status);
            SetTrainingStatusBrushes(readinessBrush, stateBrush);
        }

        private MediaBrush ResolveTrainingStateBrush(PythonCommunicationStatus status)
        {
            if (!HasTrainingStatus(status))
            {
                return ResolveBrushResource("SecondaryTextBrush", MediaBrushes.Gray);
            }

            string state = status.LastTrainingState?.Trim() ?? string.Empty;
            if (WpfTrainingWeightsService.IsCompletedTrainingState(state))
            {
                return MediaBrushes.LimeGreen;
            }

            if (string.Equals(state, "failed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "error", StringComparison.OrdinalIgnoreCase))
            {
                return MediaBrushes.IndianRed;
            }

            if (string.Equals(state, "stopped", StringComparison.OrdinalIgnoreCase))
            {
                return MediaBrushes.DarkOrange;
            }

            if (!IsTerminalTrainingState(state) || status.LastTrainingProgressPercent.HasValue)
            {
                return MediaBrushes.DodgerBlue;
            }

            return ResolveBrushResource("SecondaryTextBrush", MediaBrushes.Gray);
        }

        private MediaBrush ResolveBrushResource(string key, MediaBrush fallback)
        {
            return TryFindResource(key) as MediaBrush ?? fallback;
        }

        private static bool HasTrainingStatus(PythonCommunicationStatus status)
        {
            return status != null
                && (!string.IsNullOrWhiteSpace(status.LastTrainingState)
                    || !string.IsNullOrWhiteSpace(status.LastTrainingMessage)
                    || status.LastTrainingProgressPercent.HasValue
                    || status.LastTrainingEpoch.HasValue
                    || status.LastTrainingTotalEpochs.HasValue);
        }

        private static string BuildTrainingProgressSummary(PythonCommunicationStatus status)
        {
            if (!HasTrainingStatus(status))
            {
                return "학습 대기";
            }

            List<string> parts = new List<string>
            {
                $"학습 {FormatTrainingState(status.LastTrainingState)}"
            };
            if (status.LastTrainingProgressPercent.HasValue)
            {
                parts.Add($"{Math.Clamp(status.LastTrainingProgressPercent.Value, 0, 100)}%");
            }

            if (!string.IsNullOrWhiteSpace(status.LastTrainingMessage))
            {
                parts.Add(FormatTrainingMessage(status.LastTrainingMessage));
            }

            return string.Join(" / ", parts);
        }

        private static string FormatTrainingState(string state)
        {
            string normalized = state?.Trim() ?? string.Empty;
            return normalized.ToLowerInvariant() switch
            {
                "" => "상태 미확인",
                "idle" => "대기",
                "running" => "진행 중",
                "completed" => "완료",
                "stopped" => "중지됨",
                "failed" => "실패",
                "error" => "오류",
                _ => normalized
            };
        }

        private static string FormatTrainingMessage(string message)
        {
            string normalized = message?.Trim() ?? string.Empty;
            return normalized.ToLowerInvariant() switch
            {
                "epoch" => "에폭",
                "epoch update" => "에폭 갱신",
                _ => normalized
            };
        }

        private static string BuildTrainingEpochSummary(PythonCommunicationStatus status)
        {
            if (status?.LastTrainingEpoch.HasValue == true && status.LastTrainingTotalEpochs.HasValue)
            {
                return $"에폭 {status.LastTrainingEpoch.Value}/{status.LastTrainingTotalEpochs.Value}";
            }

            if (status?.LastTrainingEpoch.HasValue == true)
            {
                return $"에폭 {status.LastTrainingEpoch.Value}";
            }

            return string.Empty;
        }
    }
}
