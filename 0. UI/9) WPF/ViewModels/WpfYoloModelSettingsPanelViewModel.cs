using System;
using System.Globalization;
using System.Windows.Input;
using OpenVisionLab.Mvvm;
using MvcVisionSystem._1._Core;

namespace MvcVisionSystem
{
    public sealed class WpfYoloModelSettingsPanelViewModel : WpfObservableViewModel
    {
        private string pythonExecutablePath = string.Empty;
        private string projectRootPath = string.Empty;
        private string clientScriptPath = string.Empty;
        private string weightsPath = string.Empty;
        private string imageRootPath = string.Empty;
        private string minimumConfidenceText = string.Empty;
        private string maximumCandidatesText = string.Empty;
        private string inferenceImageSizeText = string.Empty;
        private string timeoutSecondsText = string.Empty;
        private bool autoStartClient;
        private bool isBrowsePythonEnabled = true;
        private bool isBrowseProjectRootEnabled = true;
        private bool isBrowseClientScriptEnabled = true;
        private bool isBrowseWeightsEnabled = true;
        private bool isBrowseImageRootEnabled = true;
        private bool isSaveSettingsEnabled = true;
        private bool isResetSettingsEnabled = true;
        private ICommand browsePythonCommand = new RelayCommand(NoOpCommand);
        private ICommand browseProjectRootCommand = new RelayCommand(NoOpCommand);
        private ICommand browseClientScriptCommand = new RelayCommand(NoOpCommand);
        private ICommand browseWeightsCommand = new RelayCommand(NoOpCommand);
        private ICommand browseImageRootCommand = new RelayCommand(NoOpCommand);
        private ICommand saveSettingsCommand = new RelayCommand(NoOpCommand);
        private ICommand resetSettingsCommand = new RelayCommand(NoOpCommand);

        public string ViewName => nameof(WpfYoloModelSettingsPanel);

        public ICommand BrowsePythonCommand
        {
            get => browsePythonCommand;
            private set => SetProperty(ref browsePythonCommand, value);
        }

        public ICommand BrowseProjectRootCommand
        {
            get => browseProjectRootCommand;
            private set => SetProperty(ref browseProjectRootCommand, value);
        }

        public ICommand BrowseClientScriptCommand
        {
            get => browseClientScriptCommand;
            private set => SetProperty(ref browseClientScriptCommand, value);
        }

        public ICommand BrowseWeightsCommand
        {
            get => browseWeightsCommand;
            private set => SetProperty(ref browseWeightsCommand, value);
        }

        public ICommand BrowseImageRootCommand
        {
            get => browseImageRootCommand;
            private set => SetProperty(ref browseImageRootCommand, value);
        }

        public ICommand SaveSettingsCommand
        {
            get => saveSettingsCommand;
            private set => SetProperty(ref saveSettingsCommand, value);
        }

        public ICommand ResetSettingsCommand
        {
            get => resetSettingsCommand;
            private set => SetProperty(ref resetSettingsCommand, value);
        }
        public string PythonExecutablePath
        {
            get => pythonExecutablePath;
            set => SetProperty(ref pythonExecutablePath, value ?? string.Empty);
        }

        public string ProjectRootPath
        {
            get => projectRootPath;
            set => SetProperty(ref projectRootPath, value ?? string.Empty);
        }

        public string ClientScriptPath
        {
            get => clientScriptPath;
            set => SetProperty(ref clientScriptPath, value ?? string.Empty);
        }

        public string WeightsPath
        {
            get => weightsPath;
            set => SetProperty(ref weightsPath, value ?? string.Empty);
        }

        public string ImageRootPath
        {
            get => imageRootPath;
            set => SetProperty(ref imageRootPath, value ?? string.Empty);
        }

        public string MinimumConfidenceText
        {
            get => minimumConfidenceText;
            set => SetProperty(ref minimumConfidenceText, value ?? string.Empty);
        }

        public string MaximumCandidatesText
        {
            get => maximumCandidatesText;
            set => SetProperty(ref maximumCandidatesText, value ?? string.Empty);
        }

        public string InferenceImageSizeText
        {
            get => inferenceImageSizeText;
            set => SetProperty(ref inferenceImageSizeText, value ?? string.Empty);
        }

        public string TimeoutSecondsText
        {
            get => timeoutSecondsText;
            set => SetProperty(ref timeoutSecondsText, value ?? string.Empty);
        }

        public bool AutoStartClient
        {
            get => autoStartClient;
            set => SetProperty(ref autoStartClient, value);
        }

        public bool IsBrowsePythonEnabled
        {
            get => isBrowsePythonEnabled;
            private set => SetProperty(ref isBrowsePythonEnabled, value);
        }

        public bool IsBrowseProjectRootEnabled
        {
            get => isBrowseProjectRootEnabled;
            private set => SetProperty(ref isBrowseProjectRootEnabled, value);
        }

        public bool IsBrowseClientScriptEnabled
        {
            get => isBrowseClientScriptEnabled;
            private set => SetProperty(ref isBrowseClientScriptEnabled, value);
        }

        public bool IsBrowseWeightsEnabled
        {
            get => isBrowseWeightsEnabled;
            private set => SetProperty(ref isBrowseWeightsEnabled, value);
        }

        public bool IsBrowseImageRootEnabled
        {
            get => isBrowseImageRootEnabled;
            private set => SetProperty(ref isBrowseImageRootEnabled, value);
        }

        public bool IsSaveSettingsEnabled
        {
            get => isSaveSettingsEnabled;
            private set => SetProperty(ref isSaveSettingsEnabled, value);
        }

        public bool IsResetSettingsEnabled
        {
            get => isResetSettingsEnabled;
            private set => SetProperty(ref isResetSettingsEnabled, value);
        }

        public void ConfigureCommands(
            Action browsePython,
            Action browseProjectRoot,
            Action browseClientScript,
            Action browseWeights,
            Action browseImageRoot,
            Action saveSettings,
            Action resetSettings)
        {
            // Path-picker commands are injected to keep file dialogs in the shell, not in view code-behind.
            BrowsePythonCommand = new RelayCommand(browsePython ?? NoOpCommand);
            BrowseProjectRootCommand = new RelayCommand(browseProjectRoot ?? NoOpCommand);
            BrowseClientScriptCommand = new RelayCommand(browseClientScript ?? NoOpCommand);
            BrowseWeightsCommand = new RelayCommand(browseWeights ?? NoOpCommand);
            BrowseImageRootCommand = new RelayCommand(browseImageRoot ?? NoOpCommand);
            SaveSettingsCommand = new RelayCommand(saveSettings ?? NoOpCommand);
            ResetSettingsCommand = new RelayCommand(resetSettings ?? NoOpCommand);
        }
        public void LoadFrom(PythonModelSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            PythonExecutablePath = PythonModelSettingsValidator.ResolvePythonExecutable(settings);
            ProjectRootPath = settings.ProjectRootPath ?? string.Empty;
            ClientScriptPath = settings.ClientScriptPath ?? string.Empty;
            WeightsPath = settings.WeightsPath ?? string.Empty;
            ImageRootPath = settings.ImageRootPath ?? string.Empty;
            MinimumConfidenceText = settings.MinimumDetectionConfidence.ToString("0.##", CultureInfo.InvariantCulture);
            MaximumCandidatesText = settings.MaximumDetectionCandidates.ToString(CultureInfo.InvariantCulture);
            InferenceImageSizeText = settings.InferenceImageSize.ToString(CultureInfo.InvariantCulture);
            TimeoutSecondsText = settings.DetectionTimeoutSeconds.ToString(CultureInfo.InvariantCulture);
            AutoStartClient = settings.AutoStartClient;
        }

        public void ApplyTo(PythonModelSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (!float.TryParse(MinimumConfidenceText, NumberStyles.Float, CultureInfo.InvariantCulture, out float confidence))
            {
                throw new FormatException("신뢰도는 0과 1 사이 숫자여야 합니다.");
            }

            if (!int.TryParse(TimeoutSecondsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int timeoutSeconds))
            {
                throw new FormatException("시간 제한은 초 단위 정수여야 합니다.");
            }

            if (!int.TryParse(MaximumCandidatesText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int maximumCandidates))
            {
                throw new FormatException("Maximum candidates must be an integer.");
            }

            if (!int.TryParse(InferenceImageSizeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int inferenceImageSize))
            {
                throw new FormatException("Inference image size must be an integer.");
            }

            settings.PythonExecutablePath = PythonExecutablePath.Trim();
            settings.ProjectRootPath = ProjectRootPath.Trim();
            settings.ClientScriptPath = ClientScriptPath.Trim();
            settings.WeightsPath = WeightsPath.Trim();
            settings.ImageRootPath = ImageRootPath.Trim();
            settings.AutoStartClient = AutoStartClient;
            settings.MinimumDetectionConfidence = Math.Clamp(confidence, 0F, 1F);
            settings.MaximumDetectionCandidates = Math.Clamp(maximumCandidates, 1, 200);
            settings.InferenceImageSize = Math.Clamp(inferenceImageSize, 64, 2048);
            settings.DetectionTimeoutSeconds = Math.Clamp(timeoutSeconds, 1, 600);
            settings.EnsureDefaults();
        }

        public void ApplyWorkflowCommandState(WpfWorkflowCommandState state)
        {
            bool canRunGeneralCommands = state?.CanRunGeneralCommands == true;
            IsBrowsePythonEnabled = canRunGeneralCommands;
            IsBrowseProjectRootEnabled = canRunGeneralCommands;
            IsBrowseClientScriptEnabled = canRunGeneralCommands;
            IsBrowseWeightsEnabled = canRunGeneralCommands;
            IsBrowseImageRootEnabled = canRunGeneralCommands;
            IsSaveSettingsEnabled = canRunGeneralCommands;
            IsResetSettingsEnabled = canRunGeneralCommands;
        }

        private static void NoOpCommand()
        {
        }
    }
}
