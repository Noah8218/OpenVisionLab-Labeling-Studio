using OpenVisionLab.Mvvm;
using System;
using System.Windows.Input;

namespace MvcVisionSystem
{
    public sealed class WpfLabelingShellViewModel : WpfObservableViewModel
    {
        private static readonly Action NoOpCommand = () => { };
        private static readonly Action<KeyInputCommandArgs> NoOpKeyCommand = _ => { };

        private bool isCurrentImageDetectionEnabled;
        private bool isLabelingModeActive = true;
        private bool isInferenceModeActive;
        private bool isLabelingModeButtonEnabled = true;
        private bool isInferenceModeButtonEnabled = true;
        private ICommand loadedCommand = new RelayCommand(NoOpCommand);
        private ICommand closedCommand = new RelayCommand(NoOpCommand);
        private ICommand previewKeyDownCommand = new RelayCommand<KeyInputCommandArgs>(NoOpKeyCommand);
        private ICommand toggleThemeCommand = new RelayCommand(NoOpCommand);
        private ICommand loadSampleCommand = new RelayCommand(NoOpCommand);
        private ICommand addSampleRoiCommand = new RelayCommand(NoOpCommand);
        private ICommand saveAnnotationsCommand = new RelayCommand(NoOpCommand);
        private ICommand labelingModeCommand = new RelayCommand(NoOpCommand);
        private ICommand inferenceModeCommand = new RelayCommand(NoOpCommand);
        private ICommand checkYoloCommand = new RelayCommand(NoOpCommand);
        private ICommand detectCurrentImageCommand = new RelayCommand(NoOpCommand);

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

        public ICommand LoadedCommand
        {
            get => loadedCommand;
            private set => SetProperty(ref loadedCommand, value);
        }

        public ICommand ClosedCommand
        {
            get => closedCommand;
            private set => SetProperty(ref closedCommand, value);
        }

        public ICommand PreviewKeyDownCommand
        {
            get => previewKeyDownCommand;
            private set => SetProperty(ref previewKeyDownCommand, value);
        }

        public ICommand ToggleThemeCommand
        {
            get => toggleThemeCommand;
            private set => SetProperty(ref toggleThemeCommand, value);
        }

        public ICommand LoadSampleCommand
        {
            get => loadSampleCommand;
            private set => SetProperty(ref loadSampleCommand, value);
        }

        public ICommand AddSampleRoiCommand
        {
            get => addSampleRoiCommand;
            private set => SetProperty(ref addSampleRoiCommand, value);
        }

        public ICommand SaveAnnotationsCommand
        {
            get => saveAnnotationsCommand;
            private set => SetProperty(ref saveAnnotationsCommand, value);
        }

        public ICommand LabelingModeCommand
        {
            get => labelingModeCommand;
            private set => SetProperty(ref labelingModeCommand, value);
        }

        public ICommand InferenceModeCommand
        {
            get => inferenceModeCommand;
            private set => SetProperty(ref inferenceModeCommand, value);
        }

        public ICommand CheckYoloCommand
        {
            get => checkYoloCommand;
            private set => SetProperty(ref checkYoloCommand, value);
        }

        public ICommand DetectCurrentImageCommand
        {
            get => detectCurrentImageCommand;
            private set => SetProperty(ref detectCurrentImageCommand, value);
        }

        public void ConfigureCommands(
            Action toggleTheme,
            Action loadSample,
            Action addSampleRoi,
            Action saveAnnotations,
            Action labelingMode,
            Action inferenceMode,
            Action checkYolo,
            Action detectCurrentImage,
            Action loaded,
            Action closed,
            Action<KeyInputCommandArgs> previewKeyDown)
        {
            // Shell lifecycle and toolbar commands are injected; key commands use a DTO so this ViewModel avoids WPF EventArgs.
            ToggleThemeCommand = new RelayCommand(toggleTheme ?? NoOpCommand);
            LoadSampleCommand = new RelayCommand(loadSample ?? NoOpCommand);
            AddSampleRoiCommand = new RelayCommand(addSampleRoi ?? NoOpCommand);
            SaveAnnotationsCommand = new RelayCommand(saveAnnotations ?? NoOpCommand);
            LabelingModeCommand = new RelayCommand(labelingMode ?? NoOpCommand);
            InferenceModeCommand = new RelayCommand(inferenceMode ?? NoOpCommand);
            CheckYoloCommand = new RelayCommand(checkYolo ?? NoOpCommand);
            DetectCurrentImageCommand = new RelayCommand(detectCurrentImage ?? NoOpCommand);
            LoadedCommand = new RelayCommand(loaded ?? NoOpCommand);
            ClosedCommand = new RelayCommand(closed ?? NoOpCommand);
            PreviewKeyDownCommand = new RelayCommand<KeyInputCommandArgs>(previewKeyDown ?? NoOpKeyCommand);
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
