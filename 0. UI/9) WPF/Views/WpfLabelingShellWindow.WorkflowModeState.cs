using MvcVisionSystem._3._Communication.TCP;
using System;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        private void SetWorkflowMode(WorkflowMode mode)
        {
            currentWorkflowMode = mode;
            UpdateYoloCommandButtons();
            UpdateCandidateActionState();
            SetModelStatus(mode == WorkflowMode.Inference
                ? "모드: 추론 검토"
                : "모드: 라벨링");
            RefreshCanvasWorkflowContext();
            UpdateWorkflowProgressStatus();
        }

        private void UpdateWorkflowModeUi()
        {
            bool canSwitchMode = !isDetecting && !isBatchDetectionRunning;
            ShellViewModel?.SetWorkflowModeState(
                currentWorkflowMode == WorkflowMode.Inference,
                canSwitchMode);
        }

        private bool EnsureInferenceModeForDetection()
        {
            if (currentWorkflowMode == WorkflowMode.Inference)
            {
                return true;
            }

            SetPythonStatus("\uCD94\uB860: \uAC80\uD1A0 \uBAA8\uB4DC \uD544\uC694");
            SetGlobalInferenceStatus("추론 검토 모드 필요", isBusy: false, isWarning: true);
            AppendLog("검출 건너뜀. 먼저 추론 검토 모드로 전환하세요.");
            UpdateYoloCommandButtons();
            return false;
        }

        private static bool IsTrainingStopAvailable(PythonCommunicationStatus status)
        {
            if (status == null)
            {
                return false;
            }

            string state = status.LastTrainingState?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(state))
            {
                return status.LastTrainingProgressPercent.HasValue
                    && status.LastTrainingProgressPercent.Value > 0
                    && status.LastTrainingProgressPercent.Value < 100;
            }

            return !IsTerminalTrainingState(state);
        }

        private static bool IsTerminalTrainingState(string state)
        {
            return string.Equals(state, "idle", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "completed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "stopped", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "failed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "error", StringComparison.OrdinalIgnoreCase);
        }
    }
}
