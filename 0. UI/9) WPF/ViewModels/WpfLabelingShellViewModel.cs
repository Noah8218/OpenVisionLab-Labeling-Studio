namespace MvcVisionSystem
{
    public sealed class WpfLabelingShellViewModel : WpfObservableViewModel
    {
        private bool isCurrentImageDetectionEnabled;
        private bool isLabelingModeActive = true;
        private bool isInferenceModeActive;
        private bool isLabelingModeButtonEnabled = true;
        private bool isInferenceModeButtonEnabled = true;

        public string ViewName => nameof(WpfLabelingShellWindow);

        public bool IsCurrentImageDetectionEnabled
        {
            get => isCurrentImageDetectionEnabled;
            private set => SetProperty(ref isCurrentImageDetectionEnabled, value);
        }

        public bool IsLabelingModeActive
        {
            get => isLabelingModeActive;
            private set => SetProperty(ref isLabelingModeActive, value);
        }

        public bool IsInferenceModeActive
        {
            get => isInferenceModeActive;
            private set => SetProperty(ref isInferenceModeActive, value);
        }

        public bool IsLabelingModeButtonEnabled
        {
            get => isLabelingModeButtonEnabled;
            private set => SetProperty(ref isLabelingModeButtonEnabled, value);
        }

        public bool IsInferenceModeButtonEnabled
        {
            get => isInferenceModeButtonEnabled;
            private set => SetProperty(ref isInferenceModeButtonEnabled, value);
        }

        public void ApplyWorkflowCommandState(WpfWorkflowCommandState state)
        {
            IsCurrentImageDetectionEnabled = state?.CanRunInference == true;
        }

        public void SetWorkflowModeState(bool isInferenceMode, bool canSwitchMode)
        {
            IsLabelingModeActive = !isInferenceMode;
            IsInferenceModeActive = isInferenceMode;
            IsLabelingModeButtonEnabled = IsLabelingModeActive || canSwitchMode;
            IsInferenceModeButtonEnabled = IsInferenceModeActive || canSwitchMode;
        }
    }
}
