namespace MvcVisionSystem
{
    public static class WpfWorkflowCommandStateService
    {
        public static WpfWorkflowCommandState Build(
            bool isInferenceMode,
            bool isYoloEnvironmentCommandRunning,
            bool isDetecting,
            bool isBatchDetectionRunning,
            bool isTrainingCommandRunning,
            bool isTrainingStopAvailable,
            bool hasCurrentRecipeName)
        {
            bool nonTrainingBusy = isYoloEnvironmentCommandRunning || isDetecting || isBatchDetectionRunning;
            bool anyBusy = nonTrainingBusy || isTrainingCommandRunning;
            bool canRunGeneralCommands = !anyBusy;
            bool canRunInference = isInferenceMode && !anyBusy;
            string unavailableHint = anyBusy
                ? "현재 작업이 끝나면 사용할 수 있습니다."
                : "추론 검사 모드에서 사용할 수 있습니다. 상단의 추론 검사를 먼저 선택하세요.";

            return new WpfWorkflowCommandState
            {
                NonTrainingBusy = nonTrainingBusy,
                AnyBusy = anyBusy,
                CanRunGeneralCommands = canRunGeneralCommands,
                CanSaveProjectConfig = canRunGeneralCommands && hasCurrentRecipeName,
                CanStopTraining = !nonTrainingBusy && (isTrainingCommandRunning || isTrainingStopAvailable),
                CanRunInference = canRunInference,
                CanStopBatchDetection = isBatchDetectionRunning,
                CurrentImageDetectionToolTip = canRunInference ? "현재 이미지로 YOLO 검출 실행" : unavailableHint,
                SelectedQueueDetectionToolTip = canRunInference ? "선택 이미지 검사" : unavailableHint,
                BatchDetectionToolTip = canRunInference ? "표시된 행 일괄 검사" : unavailableHint,
                RetryFailedToolTip = canRunInference ? "실패 행 재시도" : unavailableHint,
                StopBatchToolTip = isBatchDetectionRunning ? "일괄 검사 중지" : "일괄 검사 중에만 사용할 수 있습니다."
            };
        }
    }

    public sealed class WpfWorkflowCommandState
    {
        public bool NonTrainingBusy { get; set; }

        public bool AnyBusy { get; set; }

        public bool CanRunGeneralCommands { get; set; }

        public bool CanSaveProjectConfig { get; set; }

        public bool CanStopTraining { get; set; }

        public bool CanRunInference { get; set; }

        public bool CanStopBatchDetection { get; set; }

        public string CurrentImageDetectionToolTip { get; set; } = string.Empty;

        public string SelectedQueueDetectionToolTip { get; set; } = string.Empty;

        public string BatchDetectionToolTip { get; set; } = string.Empty;

        public string RetryFailedToolTip { get; set; } = string.Empty;

        public string StopBatchToolTip { get; set; } = string.Empty;
    }
}
