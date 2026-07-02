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
            bool hasCurrentRecipeName,
            bool canRunModelTraining = true,
            bool canRunModelInference = true,
            string modelRuntimeUnavailableHint = "")
        {
            bool nonTrainingBusy = isYoloEnvironmentCommandRunning || isDetecting || isBatchDetectionRunning;
            bool trainingBusy = isTrainingCommandRunning || isTrainingStopAvailable;
            bool anyBusy = nonTrainingBusy || trainingBusy;
            bool canRunGeneralCommands = !anyBusy;
            bool canRunTraining = canRunGeneralCommands && canRunModelTraining;
            bool canRunInference = isInferenceMode && !anyBusy && canRunModelInference;
            string saveProjectConfigUnavailableHint = string.Empty;
            if (trainingBusy)
            {
                saveProjectConfigUnavailableHint = "\uD559\uC2B5/\uC911\uC9C0 \uC791\uC5C5\uC774 \uB05D\uB098\uBA74 \uAC80\uC0AC \uBAA8\uB378\uB85C \uC800\uC7A5\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.";
            }
            else if (nonTrainingBusy)
            {
                saveProjectConfigUnavailableHint = "\uD604\uC7AC \uC791\uC5C5\uC774 \uB05D\uB098\uBA74 \uAC80\uC0AC \uBAA8\uB378\uB85C \uC800\uC7A5\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.";
            }
            else if (!hasCurrentRecipeName)
            {
                saveProjectConfigUnavailableHint = "\uC800\uC7A5\uD560 recipe\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4. \uB370\uC774\uD130\uC14B \uD648\uC5D0\uC11C \uB370\uC774\uD130\uC14B\uC744 \uB9CC\uB4E4\uAC70\uB098 \uAE30\uC874 \uB370\uC774\uD130\uC14B\uC744 \uC801\uC6A9\uD558\uC138\uC694.";
            }
            string unavailableHint = anyBusy
                ? "\uD604\uC7AC \uC791\uC5C5\uC774 \uB05D\uB098\uBA74 \uC0AC\uC6A9\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4."
                : "\uCD94\uB860 \uAC80\uD1A0 \uBAA8\uB4DC\uC5D0\uC11C \uC0AC\uC6A9\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4. \uC0C1\uB2E8\uC758 \uCD94\uB860 \uAC80\uD1A0\uB97C \uBA3C\uC800 \uC120\uD0DD\uD558\uC138\uC694.";

            if (!canRunModelInference && !string.IsNullOrWhiteSpace(modelRuntimeUnavailableHint))
            {
                unavailableHint = modelRuntimeUnavailableHint;
            }

            string startTrainingUnavailableHint = !canRunModelTraining && !string.IsNullOrWhiteSpace(modelRuntimeUnavailableHint)
                ? modelRuntimeUnavailableHint
                : "\uD604\uC7AC \uB2E4\uB978 \uBA85\uB839\uC774 \uC2E4\uD589 \uC911\uC774\uBBC0\uB85C \uC644\uB8CC \uD6C4 \uD559\uC2B5\uC744 \uC2DC\uC791\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.";

            return new WpfWorkflowCommandState
            {
                NonTrainingBusy = nonTrainingBusy,
                AnyBusy = anyBusy,
                CanRunGeneralCommands = canRunGeneralCommands,
                CanSaveProjectConfig = canRunGeneralCommands && hasCurrentRecipeName,
                CanSaveProjectConfigUnavailableHint = saveProjectConfigUnavailableHint,
                CanStopTraining = !nonTrainingBusy && trainingBusy,
                CanStartTraining = canRunTraining,
                StartTrainingToolTip = canRunTraining ? "\uD559\uC2B5\uC744 \uC2DC\uC791\uD569\uB2C8\uB2E4." : startTrainingUnavailableHint,
                CanRunInference = canRunInference,
                CanStopBatchDetection = isBatchDetectionRunning,
                CurrentImageDetectionToolTip = canRunInference ? "\uD604\uC7AC \uC774\uBBF8\uC9C0\uB85C \uBAA8\uB378 \uAC80\uC0AC \uC2E4\uD589" : unavailableHint,
                SelectedQueueDetectionToolTip = canRunInference ? "\uC120\uD0DD \uC774\uBBF8\uC9C0 \uAC80\uC0AC" : unavailableHint,
                BatchDetectionToolTip = canRunInference ? "\uD45C\uC2DC\uB41C \uD589 \uC77C\uAD04 \uAC80\uC0AC" : unavailableHint,
                RetryFailedToolTip = canRunInference ? "\uC2E4\uD328 \uD589 \uC7AC\uC2DC\uB3C4" : unavailableHint,
                StopBatchToolTip = isBatchDetectionRunning ? "\uC77C\uAD04 \uAC80\uC0AC \uC911\uC9C0" : "\uC77C\uAD04 \uAC80\uC0AC \uC911\uC5D0\uB9CC \uC0AC\uC6A9\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4."
            };
        }
    }

    public sealed class WpfWorkflowCommandState
    {
        public bool NonTrainingBusy { get; set; }

        public bool AnyBusy { get; set; }

        public bool CanRunGeneralCommands { get; set; }

        public bool CanSaveProjectConfig { get; set; }

        public string CanSaveProjectConfigUnavailableHint { get; set; } = string.Empty;

        public bool CanStopTraining { get; set; }

        public bool CanStartTraining { get; set; }

        public string StartTrainingToolTip { get; set; } = string.Empty;

        public bool CanRunInference { get; set; }

        public bool CanStopBatchDetection { get; set; }

        public string CurrentImageDetectionToolTip { get; set; } = string.Empty;

        public string SelectedQueueDetectionToolTip { get; set; } = string.Empty;

        public string BatchDetectionToolTip { get; set; } = string.Empty;

        public string RetryFailedToolTip { get; set; } = string.Empty;

        public string StopBatchToolTip { get; set; } = string.Empty;
    }
}
