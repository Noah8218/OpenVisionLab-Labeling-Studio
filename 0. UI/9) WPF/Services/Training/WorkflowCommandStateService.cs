namespace MvcVisionSystem
{
    public static class WorkflowCommandStateService
    {
        public static bool HasActiveCommand(
            bool isYoloEnvironmentCommandRunning,
            bool isDetecting,
            bool isBatchDetectionRunning,
            bool isTrainingCommandRunning)
        {
            return isYoloEnvironmentCommandRunning
                || isDetecting
                || isBatchDetectionRunning
                || isTrainingCommandRunning;
        }

        public static WorkflowCommandState Build(
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
                ? OpenVisionLab.OpenVisionLanguageService.T("WpfShell.Command.Unavailable.Busy")
                : OpenVisionLab.OpenVisionLanguageService.T("WpfShell.Command.Unavailable.InferenceMode");

            if (isInferenceMode
                && !canRunModelInference
                && !string.IsNullOrWhiteSpace(modelRuntimeUnavailableHint))
            {
                unavailableHint = OpenVisionLab.OpenVisionLanguageService.CurrentLanguage == OpenVisionLab.OpenVisionLanguage.English
                    ? OpenVisionLab.OpenVisionLanguageService.T("WpfShell.Command.Unavailable.ModelRuntime")
                    : modelRuntimeUnavailableHint;
            }

            string startTrainingUnavailableHint = !canRunModelTraining && !string.IsNullOrWhiteSpace(modelRuntimeUnavailableHint)
                ? modelRuntimeUnavailableHint
                : "\uD604\uC7AC \uB2E4\uB978 \uBA85\uB839\uC774 \uC2E4\uD589 \uC911\uC774\uBBC0\uB85C \uC644\uB8CC \uD6C4 \uD559\uC2B5\uC744 \uC2DC\uC791\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.";

            return new WorkflowCommandState
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
                CurrentImageDetectionToolTip = canRunInference
                    ? OpenVisionLab.OpenVisionLanguageService.T("WpfShell.Header.Inspect.ToolTip")
                    : unavailableHint,
                SelectedQueueDetectionToolTip = canRunInference ? "\uC120\uD0DD \uC774\uBBF8\uC9C0 \uAC80\uC0AC" : unavailableHint,
                BatchDetectionToolTip = canRunInference ? "\uD45C\uC2DC\uB41C \uD589 \uC77C\uAD04 \uAC80\uC0AC" : unavailableHint,
                RetryFailedToolTip = canRunInference ? "\uC2E4\uD328 \uD589 \uC7AC\uC2DC\uB3C4" : unavailableHint,
                StopBatchToolTip = isBatchDetectionRunning ? "\uC77C\uAD04 \uAC80\uC0AC \uC911\uC9C0" : "\uC77C\uAD04 \uAC80\uC0AC \uC911\uC5D0\uB9CC \uC0AC\uC6A9\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4."
            };
        }
    }

    [System.Obsolete("Use WorkflowCommandStateService.", false)]
    public static class WpfWorkflowCommandStateService
    {
        public static bool HasActiveCommand(bool isYoloEnvironmentCommandRunning, bool isDetecting, bool isBatchDetectionRunning, bool isTrainingCommandRunning)
            => WorkflowCommandStateService.HasActiveCommand(isYoloEnvironmentCommandRunning, isDetecting, isBatchDetectionRunning, isTrainingCommandRunning);

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
            => new WpfWorkflowCommandState(WorkflowCommandStateService.Build(
                isInferenceMode,
                isYoloEnvironmentCommandRunning,
                isDetecting,
                isBatchDetectionRunning,
                isTrainingCommandRunning,
                isTrainingStopAvailable,
                hasCurrentRecipeName,
                canRunModelTraining,
                canRunModelInference,
                modelRuntimeUnavailableHint));
    }

    public class WorkflowCommandState
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

    [System.Obsolete("Use WorkflowCommandState.", false)]
    public sealed class WpfWorkflowCommandState : WorkflowCommandState
    {
        public WpfWorkflowCommandState()
        {
        }

        public WpfWorkflowCommandState(WorkflowCommandState source)
        {
            if (source == null)
            {
                return;
            }

            NonTrainingBusy = source.NonTrainingBusy;
            AnyBusy = source.AnyBusy;
            CanRunGeneralCommands = source.CanRunGeneralCommands;
            CanSaveProjectConfig = source.CanSaveProjectConfig;
            CanSaveProjectConfigUnavailableHint = source.CanSaveProjectConfigUnavailableHint;
            CanStopTraining = source.CanStopTraining;
            CanStartTraining = source.CanStartTraining;
            StartTrainingToolTip = source.StartTrainingToolTip;
            CanRunInference = source.CanRunInference;
            CanStopBatchDetection = source.CanStopBatchDetection;
            CurrentImageDetectionToolTip = source.CurrentImageDetectionToolTip;
            SelectedQueueDetectionToolTip = source.SelectedQueueDetectionToolTip;
            BatchDetectionToolTip = source.BatchDetectionToolTip;
            RetryFailedToolTip = source.RetryFailedToolTip;
            StopBatchToolTip = source.StopBatchToolTip;
        }
    }
}
