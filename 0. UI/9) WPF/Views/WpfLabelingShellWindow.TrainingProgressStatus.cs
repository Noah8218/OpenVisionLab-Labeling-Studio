using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.Yolo;
using System;
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
                    WpfTrainingProgressPresentationService.BuildAcceptedWorkerWaitProgressText(),
                    WpfTrainingProgressPresentationService.BuildBeforeEpochText(),
                    TrainingSettingsViewModel?.TrainingProgressValue ?? TrainingProgressBar?.Value ?? 0D,
                    isIndeterminate: true);
            }
            else if (!isTrainingCommandRunning)
            {
                SetTrainingProgressStatus(WpfTrainingProgressPresentationService.BuildIdleProgressText(), string.Empty, 0D, isIndeterminate: false);
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
            RequestTrainingStatusSnapshotFromWorker();
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
            RequestTrainingStatusSnapshotFromWorker();
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
                string timeoutText = WpfTrainingProgressPresentationService.BuildStatusNoResponseText();
                WpfTrainingRecoveryStatus recovery = WpfTrainingProgressPresentationService.BuildStatusNoResponseRecovery(timeoutText);
                SetTrainingProgressStatus(timeoutText, string.Empty, 0D, isIndeterminate: false);
                SetYoloRecoveryStatus(recovery.Title, recovery.Detail, recovery.Action);
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

        private void RequestTrainingStatusSnapshotFromWorker()
        {
            if (!isTrainingWorkflowRunning)
            {
                return;
            }

            global.DeepLearning?.SendModelStatus(CreateRequestId(), ensureLoaded: false);
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
                WpfTrainingRecoveryStatus recovery = WpfTrainingProgressPresentationService.BuildFailedRecovery(BuildTrainingRecoveryDetail(status));
                SetYoloRecoveryStatus(recovery.Title, recovery.Detail, recovery.Action);
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
            return WpfTrainingProgressPresentationService.BuildFailureDetail(status);
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
            return WpfTrainingProgressPresentationService.BuildProgressSummary(status);
        }

        private static string FormatTrainingState(string state)
        {
            return WpfTrainingProgressPresentationService.FormatTrainingState(state);
        }

        private static string FormatTrainingMessage(string message)
        {
            return WpfTrainingProgressPresentationService.FormatTrainingMessage(message);
        }

        private static string BuildTrainingEpochSummary(PythonCommunicationStatus status)
        {
            return WpfTrainingProgressPresentationService.BuildEpochSummary(status, IsLiveTrainingStatus(status));
        }
    }
}
