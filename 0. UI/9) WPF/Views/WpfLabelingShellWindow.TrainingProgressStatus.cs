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
            bool hasCurrentStatus = hasStatus && IsTrainingStatusCurrent(status);
            bool isLiveTraining = hasCurrentStatus && IsLiveTrainingStatus(status);
            if (hasCurrentStatus)
            {
                isTrainingWorkflowRunning = isLiveTraining;
            }

            if (hasCurrentStatus && status.LastTrainingProgressPercent.HasValue)
            {
                SetTrainingProgressValue(Math.Clamp(status.LastTrainingProgressPercent.Value, 0, 100));
            }
            else if (!isTrainingCommandRunning && !isTrainingWorkflowRunning)
            {
                SetTrainingProgressValue(0);
            }

            if (hasCurrentStatus)
            {
                SetTrainingProgressStatus(
                    BuildTrainingProgressSummary(status),
                    BuildTrainingEpochSummary(status),
                    TrainingSettingsViewModel?.TrainingProgressValue ?? TrainingProgressBar?.Value ?? 0D,
                    isIndeterminate: isLiveTraining && !status.LastTrainingProgressPercent.HasValue);
                UpdateYoloTrainingGuideTrainingHistory(status);
                if (WpfTrainingWeightsService.IsCompletedTrainingState(status.LastTrainingState))
                {
                    TryApplyLatestTrainingWeightsFromProject(logIfUnchanged: false);
                }
            }
            else if (isTrainingWorkflowRunning)
            {
                SetTrainingProgressStatus(
                    "\uD559\uC2B5 \uBA85\uB839 \uC218\uB77D\uB428 / \uC6CC\uCEE4 \uC0C1\uD0DC \uB300\uAE30",
                    "\uC5D0\uD3ED \uC2DC\uC791 \uC804",
                    TrainingSettingsViewModel?.TrainingProgressValue ?? TrainingProgressBar?.Value ?? 0D,
                    isIndeterminate: true);
            }
            else if (!isTrainingCommandRunning)
            {
                SetTrainingProgressStatus("\uD559\uC2B5 \uB300\uAE30", string.Empty, 0D, isIndeterminate: false);
            }

            UpdateTrainingStatusVisual(status, lastYoloTrainingReadinessReport);
            UpdateYoloTrainingRecoveryStatus(status);
            RefreshYoloTrainingStepCompletion();
            UpdateYoloCommandButtons();
            if (hasCurrentStatus && IsTerminalTrainingState(status.LastTrainingState))
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

        private bool IsTrainingStatusCurrent(PythonCommunicationStatus status)
        {
            if (!HasTrainingStatus(status))
            {
                return false;
            }

            if (!isTrainingWorkflowRunning
                || trainingStatusPollStartedUtc == DateTime.MinValue
                || !status.LastTrainingStatusAtUtc.HasValue)
            {
                return true;
            }

            return status.LastTrainingStatusAtUtc.Value >= trainingStatusPollStartedUtc.AddSeconds(-1);
        }

        private static bool IsLiveTrainingStatus(PythonCommunicationStatus status)
        {
            if (!HasTrainingStatus(status))
            {
                return false;
            }

            string state = status.LastTrainingState?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(state))
            {
                return !IsTerminalTrainingState(state);
            }

            return status.LastTrainingProgressPercent.HasValue
                && status.LastTrainingProgressPercent.Value > 0
                && status.LastTrainingProgressPercent.Value < 100;
        }

        private void TrainingStatusPollTimer_Tick(object sender, EventArgs e)
        {
            PythonCommunicationStatus status = global.GetPythonCommunicationStatusSnapshot();
            UpdateTrainingProgressFromWorker();
            bool hasCurrentStatus = HasTrainingStatus(status) && IsTrainingStatusCurrent(status);
            if (hasCurrentStatus && IsTerminalTrainingState(status.LastTrainingState))
            {
                StopTrainingStatusPolling();
                return;
            }

            if (!hasCurrentStatus
                && trainingStatusPollStartedUtc != DateTime.MinValue
                && DateTime.UtcNow - trainingStatusPollStartedUtc > TimeSpan.FromSeconds(TrainingStatusPollTimeoutSeconds))
            {
                string timeoutText = "학습 상태 응답 없음: 명령 전송 후 워커의 에폭/상태 로그가 도착하지 않았습니다.";
                timeoutText = "\uD559\uC2B5 \uC0C1\uD0DC \uC751\uB2F5 \uC5C6\uC74C: \uBA85\uB839 \uC804\uC1A1 \uD6C4 \uC6CC\uCEE4\uC758 \uC5D0\uD3ED/\uC0C1\uD0DC \uB85C\uADF8\uAC00 \uB3C4\uCC29\uD558\uC9C0 \uC54A\uC558\uC2B5\uB2C8\uB2E4.";
                SetTrainingProgressStatus(timeoutText, string.Empty, 0D, isIndeterminate: false);
                SetYoloRecoveryStatus(
                    "학습 상태 응답 없음",
                    timeoutText,
                    "\uB2E4\uC74C: \uC0C1\uC138 \uB85C\uADF8\uC5D0\uC11C Python worker \uCD9C\uB825\uC744 \uD655\uC778\uD558\uACE0, \uD544\uC694\uD558\uBA74 \uBAA8\uB378 \uD14C\uC2A4\uD2B8 \uB610\uB294 \uC7AC\uC2DC\uC791 \uD6C4 \uD559\uC2B5\uC744 \uB2E4\uC2DC \uC2DC\uC791\uD558\uC138\uC694.");
                SetYoloRecoveryStatus(
                    "\uD559\uC2B5 \uC0C1\uD0DC \uC751\uB2F5 \uC5C6\uC74C",
                    timeoutText,
                    "\uB2E4\uC74C: \uC0C1\uC138 \uB85C\uADF8\uC5D0\uC11C Python worker \uCD9C\uB825\uC744 \uD655\uC778\uD558\uACE0, \uD544\uC694\uD558\uBA74 \uBAA8\uB378 \uD14C\uC2A4\uD2B8 \uB610\uB294 \uC7AC\uC2DC\uC791 \uD6C4 \uD559\uC2B5\uC744 \uB2E4\uC2DC \uC2DC\uC791\uD558\uC138\uC694.");
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

        private void UpdateYoloTrainingRecoveryStatus(PythonCommunicationStatus status)
        {
            if (!HasTrainingStatus(status))
            {
                return;
            }

            string state = status.LastTrainingState?.Trim() ?? string.Empty;
            if (string.Equals(state, "failed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "error", StringComparison.OrdinalIgnoreCase))
            {
                SetYoloRecoveryStatus(
                    "\uD559\uC2B5 \uC2E4\uD328 - \uC870\uCE58 \uD544\uC694",
                    BuildTrainingRecoveryDetail(status),
                    "\uB2E4\uC74C: \uC0C1\uC138 \uB85C\uADF8\uC758 \uB9C8\uC9C0\uB9C9 \uC624\uB958 \uD655\uC778 -> \uB370\uC774\uD130\uC14B \uC810\uAC80 -> \uD544\uC694 \uC2DC \uC774\uBBF8\uC9C0 320/\uBC30\uCE58 4\uB85C \uB0AE\uCD98 \uB4A4 \uB2E4\uC2DC \uD559\uC2B5\uD558\uC138\uC694.");
                return;
            }

            if (WpfTrainingWeightsService.IsCompletedTrainingState(state)
                || IsLiveTrainingStatus(status)
                || string.Equals(state, "started", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "running", StringComparison.OrdinalIgnoreCase))
            {
                ClearYoloRecoveryStatus();
            }
        }

        private static string BuildTrainingRecoveryDetail(PythonCommunicationStatus status)
        {
            string message = FormatTrainingMessage(status?.LastTrainingMessage);
            if (string.IsNullOrWhiteSpace(message))
            {
                message = "Python worker\uAC00 \uC2E4\uD328 \uC0C1\uD0DC\uB97C \uBCF4\uB0C8\uC9C0\uB9CC \uC0C1\uC138 \uBA54\uC2DC\uC9C0\uB97C \uC81C\uACF5\uD558\uC9C0 \uC54A\uC558\uC2B5\uB2C8\uB2E4.";
            }

            return "\uC6D0\uC778: " + message;
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
                return "\uD559\uC2B5 \uB300\uAE30";
            }

            List<string> parts = new List<string>
            {
                $"\uD559\uC2B5 {FormatTrainingState(status.LastTrainingState)}"
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
                "" => "\uC0C1\uD0DC \uBBF8\uD655\uC778",
                "idle" => "\uB300\uAE30",
                "starting" => "\uC2DC\uC791 \uC911",
                "started" => "\uBA85\uB839 \uC218\uB77D\uB428",
                "running" => "\uC9C4\uD589 \uC911",
                "completed" => "\uC644\uB8CC",
                "stopped" => "\uC911\uC9C0\uB428",
                "failed" => "\uC2E4\uD328",
                "error" => "\uC624\uB958",
                _ => normalized
            };
        }

        private static string FormatTrainingMessage(string message)
        {
            string normalized = message?.Trim() ?? string.Empty;
            if (normalized.StartsWith("Training failed: process exited at the first training batch", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("Training failed: process exited near the first training batch", StringComparison.OrdinalIgnoreCase))
            {
                return "학습 실패: CPU에서 현재 설정이 너무 큽니다. yolov5s / 이미지 320 / 배치 4 권장";
            }

            return normalized.ToLowerInvariant() switch
            {
                "epoch" => "\uC5D0\uD3ED",
                "epoch update" => "\uC5D0\uD3ED \uAC31\uC2E0",
                "yolo training accepted by worker." => "\uC6CC\uCEE4 \uC218\uB77D, \uC5D0\uD3ED \uC2DC\uC791 \uC804",
                "yolov5 training started." => "\uC6CC\uCEE4 \uC2DC\uC791",
                "yolov5 training completed." => "\uD559\uC2B5 \uC644\uB8CC",
                "yolov5 training failed." => "\uD559\uC2B5 \uC2E4\uD328",
                "yolov5 training stopped." => "\uD559\uC2B5 \uC911\uC9C0\uB428",
                _ => normalized
            };
        }

        private static string BuildTrainingEpochSummary(PythonCommunicationStatus status)
        {
            if (IsLiveTrainingStatus(status) && status?.LastTrainingEpoch.HasValue != true)
            {
                return "\uC5D0\uD3ED \uC2DC\uC791 \uC804";
            }

            if (status?.LastTrainingEpoch.HasValue == true && status.LastTrainingTotalEpochs.HasValue)
            {
                return $"\uC5D0\uD3ED {status.LastTrainingEpoch.Value}/{status.LastTrainingTotalEpochs.Value}";
            }

            if (status?.LastTrainingEpoch.HasValue == true)
            {
                return $"\uC5D0\uD3ED {status.LastTrainingEpoch.Value}";
            }

            return string.Empty;
        }
    }
}
