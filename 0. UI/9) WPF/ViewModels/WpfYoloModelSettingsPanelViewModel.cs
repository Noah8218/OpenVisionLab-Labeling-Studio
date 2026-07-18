using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using OpenVisionLab.Mvvm;
using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;

namespace MvcVisionSystem
{
    public sealed class WpfYoloModelSettingsPanelViewModel : WpfObservableViewModel
    {
        private string pythonExecutablePath = string.Empty;
        private string selectedModelEngine = PythonModelSettings.EngineYoloV5;
        private string projectRootPath = string.Empty;
        private string clientScriptPath = string.Empty;
        private string weightsPath = string.Empty;
        private string imageRootPath = string.Empty;
        private string minimumConfidenceText = string.Empty;
        private string maximumCandidatesText = string.Empty;
        private string inferenceImageSizeText = string.Empty;
        private string timeoutSecondsText = string.Empty;
        private string anomalyNormalClassNamesText = string.Empty;
        private string anomalyAbnormalClassNamesText = string.Empty;
        private string anomalyMinimumConfidenceText = "0";
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
        private ICommand runtimeProfileActionCommand = new RelayCommand<string>(NoOpTextCommand);
        private Action<string> runtimeProfileAction = NoOpTextCommand;
        private bool isRuntimeProfileActionEnabled = true;
        private string runtimeProfileActionStatusText = "\uC2E4\uD589\uD560 \uBAA8\uB378\uC744 \uC120\uD0DD\uD558\uBA74 \uB2E4\uC74C \uC791\uC5C5\uC744 \uC5EC\uAE30\uC5D0 \uBCF4\uC5EC\uC90D\uB2C8\uB2E4.";
        private string runtimeSelfTestTitleText = string.Empty;
        private string runtimeSelfTestSummaryText = string.Empty;
        private string runtimeSelfTestDetailText = string.Empty;
        private string runtimeExecutionTitleText = string.Empty;
        private string runtimeExecutionSummaryText = string.Empty;
        private string runtimeExecutionWorkerText = string.Empty;
        private string runtimeExecutionTrainingText = string.Empty;
        private string runtimeExecutionInspectionText = string.Empty;
        private Visibility runtimeInstallPlanVisibility = Visibility.Collapsed;
        private string runtimeInstallPlanTitleText = string.Empty;
        private string runtimeInstallPlanSummaryText = string.Empty;
        private string runtimeInstallPlanDetailText = string.Empty;
        private string runtimeInstallPlanTargetText = string.Empty;
        private string runtimeInstallPlanCommandText = string.Empty;
        private string runtimeInstallPlanUninstallCommandText = string.Empty;
        private string runtimePackageResultSummaryText = "\uC544\uC9C1 \uC2E4\uD589 \uAE30\uB85D \uC5C6\uC74C";
        private string runtimePackageResultDetailText = "\uC124\uCE58 \uC2E4\uD589 \uB610\uB294 \uC81C\uAC70 \uD6C4 \uACB0\uACFC\uAC00 \uC5EC\uAE30\uC5D0 \uD45C\uC2DC\uB429\uB2C8\uB2E4.";
        private ICommand runtimeInstallPackageCommand = new RelayCommand(NoOpCommand);
        private ICommand runtimeUninstallPackageCommand = new RelayCommand(NoOpCommand);
        private Action runtimeInstallPackageAction = NoOpCommand;
        private Action runtimeUninstallPackageAction = NoOpCommand;
        private bool isRuntimePackageActionAllowed = true;
        private bool isRuntimeInstallPackageEnabled;
        private bool isRuntimeUninstallPackageEnabled;
        private string[] workerSupportedModels = Array.Empty<string>();
        private string[] workerTrainingModels = Array.Empty<string>();
        private string[] workerDetectionModels = Array.Empty<string>();

        public string ViewName => nameof(WpfYoloModelSettingsPanel);

        public ObservableCollection<PythonModelRuntimeProfile> RuntimeProfileItems { get; } = new ObservableCollection<PythonModelRuntimeProfile>();

        public ObservableCollection<PythonModelRuntimeSelfTestItem> RuntimeSelfTestItems { get; } = new ObservableCollection<PythonModelRuntimeSelfTestItem>();

        public ObservableCollection<ModelAdapterCatalogItem> ModelAdapterCatalogItems { get; } = new ObservableCollection<ModelAdapterCatalogItem>();

        public string ModelAdapterCatalogBoundaryText
            => "레시피는 기준 원본입니다. 표시된 포맷 또는 런타임은 선언된 작업·데이터·실행기·근거 계약 안에서만 사용할 수 있습니다.";

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

        public ICommand RuntimeProfileActionCommand
        {
            get => runtimeProfileActionCommand;
            private set => SetProperty(ref runtimeProfileActionCommand, value);
        }

        public ICommand RuntimeInstallPackageCommand
        {
            get => runtimeInstallPackageCommand;
            private set => SetProperty(ref runtimeInstallPackageCommand, value);
        }

        public ICommand RuntimeUninstallPackageCommand
        {
            get => runtimeUninstallPackageCommand;
            private set => SetProperty(ref runtimeUninstallPackageCommand, value);
        }

        public string PythonExecutablePath
        {
            get => pythonExecutablePath;
            set
            {
                if (SetProperty(ref pythonExecutablePath, value ?? string.Empty))
                {
                    NotifySettingsSummaryChanged();
                }
            }
        }

        public ObservableCollection<string> ModelEngineOptions { get; } = new ObservableCollection<string>(PythonModelSettings.GetSupportedModelEngines());

        public string SelectedModelEngine
        {
            get => selectedModelEngine;
            set
            {
                if (SetProperty(ref selectedModelEngine, PythonModelSettings.NormalizeModelEngine(value)))
                {
                    OnPropertyChanged(nameof(ModelEngineHintText));
                    NotifySettingsSummaryChanged();
                }
            }
        }

        public string RuntimeProfileHeaderText => "\uBAA8\uB378 \uC2E4\uD589\uAE30 \uC5F0\uACB0 \uC0C1\uD0DC";

        public string RuntimeProfileActionStatusText
        {
            get => runtimeProfileActionStatusText;
            private set => SetProperty(ref runtimeProfileActionStatusText, value ?? string.Empty);
        }

        public string RuntimeSelfTestTitleText
        {
            get => runtimeSelfTestTitleText;
            private set => SetProperty(ref runtimeSelfTestTitleText, value ?? string.Empty);
        }

        public string RuntimeSelfTestSummaryText
        {
            get => runtimeSelfTestSummaryText;
            private set => SetProperty(ref runtimeSelfTestSummaryText, value ?? string.Empty);
        }

        public string RuntimeSelfTestDetailText
        {
            get => runtimeSelfTestDetailText;
            private set => SetProperty(ref runtimeSelfTestDetailText, value ?? string.Empty);
        }

        public string RuntimeExecutionTitleText
        {
            get => runtimeExecutionTitleText;
            private set => SetProperty(ref runtimeExecutionTitleText, value ?? string.Empty);
        }

        public string RuntimeExecutionSummaryText
        {
            get => runtimeExecutionSummaryText;
            private set
            {
                if (SetProperty(ref runtimeExecutionSummaryText, value ?? string.Empty))
                {
                    OnPropertyChanged(nameof(SettingsSummaryRuntimeStatusText));
                }
            }
        }

        public string RuntimeExecutionWorkerText
        {
            get => runtimeExecutionWorkerText;
            private set => SetProperty(ref runtimeExecutionWorkerText, value ?? string.Empty);
        }

        public string RuntimeExecutionTrainingText
        {
            get => runtimeExecutionTrainingText;
            private set => SetProperty(ref runtimeExecutionTrainingText, value ?? string.Empty);
        }

        public string RuntimeExecutionInspectionText
        {
            get => runtimeExecutionInspectionText;
            private set => SetProperty(ref runtimeExecutionInspectionText, value ?? string.Empty);
        }

        public Visibility RuntimeInstallPlanVisibility
        {
            get => runtimeInstallPlanVisibility;
            private set => SetProperty(ref runtimeInstallPlanVisibility, value);
        }

        public string RuntimeInstallPlanTitleText
        {
            get => runtimeInstallPlanTitleText;
            private set => SetProperty(ref runtimeInstallPlanTitleText, value ?? string.Empty);
        }

        public string RuntimeInstallPlanSummaryText
        {
            get => runtimeInstallPlanSummaryText;
            private set => SetProperty(ref runtimeInstallPlanSummaryText, value ?? string.Empty);
        }

        public string RuntimeInstallPlanDetailText
        {
            get => runtimeInstallPlanDetailText;
            private set => SetProperty(ref runtimeInstallPlanDetailText, value ?? string.Empty);
        }

        public string RuntimeInstallPlanTargetText
        {
            get => runtimeInstallPlanTargetText;
            private set => SetProperty(ref runtimeInstallPlanTargetText, value ?? string.Empty);
        }

        public string RuntimeInstallPlanCommandText
        {
            get => runtimeInstallPlanCommandText;
            private set => SetProperty(ref runtimeInstallPlanCommandText, value ?? string.Empty);
        }

        public string RuntimeInstallPlanUninstallCommandText
        {
            get => runtimeInstallPlanUninstallCommandText;
            private set => SetProperty(ref runtimeInstallPlanUninstallCommandText, value ?? string.Empty);
        }

        public string RuntimePackageResultTitleText => "\uCD5C\uADFC \uC2E4\uD589 \uACB0\uACFC";

        public string RuntimePackageResultSummaryText
        {
            get => runtimePackageResultSummaryText;
            private set => SetProperty(ref runtimePackageResultSummaryText, value ?? string.Empty);
        }

        public string RuntimePackageResultDetailText
        {
            get => runtimePackageResultDetailText;
            private set => SetProperty(ref runtimePackageResultDetailText, value ?? string.Empty);
        }

        public bool IsRuntimeInstallPackageEnabled
        {
            get => isRuntimeInstallPackageEnabled;
            private set => SetProperty(ref isRuntimeInstallPackageEnabled, value);
        }

        public bool IsRuntimeUninstallPackageEnabled
        {
            get => isRuntimeUninstallPackageEnabled;
            private set => SetProperty(ref isRuntimeUninstallPackageEnabled, value);
        }

        public string SettingsSummaryTitleText => "\uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378 \uD504\uB85C\uD544";

        public string SettingsSummaryModelText
            => string.Format(
                CultureInfo.CurrentCulture,
                "\uBAA8\uB378 \uD504\uB85C\uD544: {0} / \uAC80\uC0AC \uBAA8\uB378: {1}",
                FormatModelProfileName(SelectedModelEngine),
                FormatPathLeaf(WeightsPath, "\uBBF8\uC124\uC815"));

        public string SettingsSummaryRuntimeText
            => string.Format(
                CultureInfo.CurrentCulture,
                "\uC2E0\uB8B0\uB3C4 {0} / \uC774\uBBF8\uC9C0 {1} / \uCD5C\uB300 \uD6C4\uBCF4 {2} / \uC2DC\uAC04 {3}\uCD08",
                string.IsNullOrWhiteSpace(MinimumConfidenceText) ? "-" : MinimumConfidenceText,
                string.IsNullOrWhiteSpace(InferenceImageSizeText) ? "-" : InferenceImageSizeText,
                string.IsNullOrWhiteSpace(MaximumCandidatesText) ? "-" : MaximumCandidatesText,
                string.IsNullOrWhiteSpace(TimeoutSecondsText) ? "-" : TimeoutSecondsText);

        public string SettingsSummaryRuntimeStatusText
            => string.IsNullOrWhiteSpace(RuntimeExecutionSummaryText)
                ? "\uC2E4\uD589\uAE30 \uC0C1\uD0DC: \uD655\uC778 \uD544\uC694"
                : RuntimeExecutionSummaryText;

        public string SettingsSummaryPathText
            => string.Format(
                CultureInfo.CurrentCulture,
                "\uC774\uBBF8\uC9C0: {0} / Python: {1}",
                FormatPathLeaf(ImageRootPath, "\uBBF8\uC124\uC815"),
                FormatPathLeaf(PythonExecutablePath, "\uBBF8\uC124\uC815"));

        public string SettingsSummaryActionText
            => "\uC774 \uD654\uBA74\uC740 \uAC80\uC0AC\uC5D0 \uC4F8 \uBAA8\uB378 \uD504\uB85C\uD544\uACFC \uBAA8\uB378 \uD30C\uC77C\uC744 \uC800\uC7A5\uD569\uB2C8\uB2E4. \uC5EC\uB7EC \uBAA8\uB378\uC744 \uBE44\uAD50\uD560 \uB54C\uB294 \uD559\uC2B5 \uACB0\uACFC \uD6C4\uBCF4\uB97C \uAC80\uC99D\uD55C \uB4A4 \uAC80\uC0AC \uBAA8\uB378\uB85C \uC800\uC7A5\uD558\uC138\uC694.";

        public string AnomalyMappingHeaderText => "\uC774\uC0C1 \uD0D0\uC9C0 \uD310\uC815 \uB9E4\uD551";

        public string AnomalyMappingSummaryText
            => string.Format(
                CultureInfo.CurrentCulture,
                "\uC815\uC0C1 {0}\uAC1C / \uC774\uC0C1 {1}\uAC1C / \uCD5C\uC18C \uC2E0\uB8B0\uB3C4 {2}",
                CountClassNames(AnomalyNormalClassNamesText),
                CountClassNames(AnomalyAbnormalClassNamesText),
                FormatAnomalyConfidence(AnomalyMinimumConfidenceText));

        public string AdvancedSettingsHeaderText
            => "\uBAA8\uB378 \uC2E4\uD589 \uD658\uACBD \uC0C1\uC138";

        public string ModelEngineHintText
        {
            get
            {
                return SelectedModelEngine switch
                {
                    PythonModelSettings.EngineYoloV8 => "\uBAA8\uB378 \uD504\uB85C\uD544: YOLOv8 \uAC1D\uCCB4\uD0D0\uC9C0. \uBC15\uC2A4 \uB77C\uBCA8 \uB370\uC774\uD130\uC14B\uC744 \uACF5\uC720\uD558\uACE0, \uC2E4\uD589 \uC5B4\uB311\uD130\uB97C \uAD50\uCCB4\uD560 \uC218 \uC788\uAC8C \uC900\uBE44\uD569\uB2C8\uB2E4.",
                    PythonModelSettings.EngineYolo11 => "\uBAA8\uB378 \uD504\uB85C\uD544: YOLO11 \uAC1D\uCCB4\uD0D0\uC9C0. Ultralytics \uAE30\uBC18 \uC2E4\uD589 \uC5B4\uB311\uD130\uB97C \uC5F0\uACB0\uD558\uBA74 \uAC19\uC740 \uBC15\uC2A4 \uB77C\uBCA8 \uB370\uC774\uD130\uC14B\uC73C\uB85C \uD559\uC2B5/\uCD94\uB860\uC744 \uBE44\uAD50\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.",
                    PythonModelSettings.EngineOnnx => "\uBAA8\uB378 \uD504\uB85C\uD544: ONNX \uCD94\uB860 \uBAA8\uB378. \uD559\uC2B5 \uC644\uB8CC \uBAA8\uB378\uC744 \uBE60\uB978 \uAC80\uC0AC \uC2E4\uD589\uAE30\uB85C \uC5F0\uACB0\uD558\uB294 \uC6A9\uB3C4\uC785\uB2C8\uB2E4.",
                    _ => "\uBAA8\uB378 \uD504\uB85C\uD544: YOLOv5 \uAC1D\uCCB4\uD0D0\uC9C0. \uD604\uC7AC \uAE30\uBCF8 \uC2E4\uD589 \uC5B4\uB311\uD130\uC774\uBA70, \uBC15\uC2A4 \uB77C\uBCA8 \uB370\uC774\uD130\uC14B\uC744 \uD559\uC2B5/\uCD94\uB860\uC5D0 \uC0AC\uC6A9\uD569\uB2C8\uB2E4."
                };
            }
        }

        public string ProjectRootPath
        {
            get => projectRootPath;
            set
            {
                if (SetProperty(ref projectRootPath, value ?? string.Empty))
                {
                    NotifySettingsSummaryChanged();
                }
            }
        }

        public string ClientScriptPath
        {
            get => clientScriptPath;
            set
            {
                if (SetProperty(ref clientScriptPath, value ?? string.Empty))
                {
                    NotifySettingsSummaryChanged();
                }
            }
        }

        public string WeightsPath
        {
            get => weightsPath;
            set
            {
                if (SetProperty(ref weightsPath, value ?? string.Empty))
                {
                    NotifySettingsSummaryChanged();
                }
            }
        }

        public string ImageRootPath
        {
            get => imageRootPath;
            set
            {
                if (SetProperty(ref imageRootPath, value ?? string.Empty))
                {
                    NotifySettingsSummaryChanged();
                }
            }
        }

        public string MinimumConfidenceText
        {
            get => minimumConfidenceText;
            set
            {
                if (SetProperty(ref minimumConfidenceText, value ?? string.Empty))
                {
                    NotifySettingsSummaryChanged();
                }
            }
        }

        public string MaximumCandidatesText
        {
            get => maximumCandidatesText;
            set
            {
                if (SetProperty(ref maximumCandidatesText, value ?? string.Empty))
                {
                    NotifySettingsSummaryChanged();
                }
            }
        }

        public string InferenceImageSizeText
        {
            get => inferenceImageSizeText;
            set
            {
                if (SetProperty(ref inferenceImageSizeText, value ?? string.Empty))
                {
                    NotifySettingsSummaryChanged();
                }
            }
        }

        public string TimeoutSecondsText
        {
            get => timeoutSecondsText;
            set
            {
                if (SetProperty(ref timeoutSecondsText, value ?? string.Empty))
                {
                    NotifySettingsSummaryChanged();
                }
            }
        }

        public bool AutoStartClient
        {
            get => autoStartClient;
            set => SetProperty(ref autoStartClient, value);
        }

        public string AnomalyNormalClassNamesText
        {
            get => anomalyNormalClassNamesText;
            set
            {
                if (SetProperty(ref anomalyNormalClassNamesText, value ?? string.Empty))
                {
                    NotifyAnomalyMappingChanged();
                }
            }
        }

        public string AnomalyAbnormalClassNamesText
        {
            get => anomalyAbnormalClassNamesText;
            set
            {
                if (SetProperty(ref anomalyAbnormalClassNamesText, value ?? string.Empty))
                {
                    NotifyAnomalyMappingChanged();
                }
            }
        }

        public string AnomalyMinimumConfidenceText
        {
            get => anomalyMinimumConfidenceText;
            set
            {
                if (SetProperty(ref anomalyMinimumConfidenceText, value ?? string.Empty))
                {
                    NotifyAnomalyMappingChanged();
                }
            }
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

        public bool IsRuntimeProfileActionEnabled
        {
            get => isRuntimeProfileActionEnabled;
            private set => SetProperty(ref isRuntimeProfileActionEnabled, value);
        }

        public void ConfigureCommands(
            Action browsePython,
            Action browseProjectRoot,
            Action browseClientScript,
            Action browseWeights,
            Action browseImageRoot,
            Action saveSettings,
            Action resetSettings,
            Action<string> runtimeProfileAction = null,
            Action runtimeInstallPackageAction = null,
            Action runtimeUninstallPackageAction = null)
        {
            // Path-picker commands are injected to keep file dialogs in the shell, not in view code-behind.
            BrowsePythonCommand = new RelayCommand(browsePython ?? NoOpCommand);
            BrowseProjectRootCommand = new RelayCommand(browseProjectRoot ?? NoOpCommand);
            BrowseClientScriptCommand = new RelayCommand(browseClientScript ?? NoOpCommand);
            BrowseWeightsCommand = new RelayCommand(browseWeights ?? NoOpCommand);
            BrowseImageRootCommand = new RelayCommand(browseImageRoot ?? NoOpCommand);
            SaveSettingsCommand = new RelayCommand(saveSettings ?? NoOpCommand);
            ResetSettingsCommand = new RelayCommand(resetSettings ?? NoOpCommand);
            this.runtimeProfileAction = runtimeProfileAction ?? NoOpTextCommand;
            RuntimeProfileActionCommand = new RelayCommand<string>(ExecuteRuntimeProfileAction);
            this.runtimeInstallPackageAction = runtimeInstallPackageAction ?? NoOpCommand;
            this.runtimeUninstallPackageAction = runtimeUninstallPackageAction ?? NoOpCommand;
            RuntimeInstallPackageCommand = new RelayCommand(ExecuteRuntimeInstallPackage);
            RuntimeUninstallPackageCommand = new RelayCommand(ExecuteRuntimeUninstallPackage);
        }

        public void LoadFrom(PythonModelSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            PythonExecutablePath = PythonModelSettingsValidator.ResolvePythonExecutable(settings);
            SelectedModelEngine = settings.ModelEngine;
            ProjectRootPath = settings.ProjectRootPath ?? string.Empty;
            ClientScriptPath = settings.ClientScriptPath ?? string.Empty;
            WeightsPath = settings.WeightsPath ?? string.Empty;
            ImageRootPath = settings.ImageRootPath ?? string.Empty;
            MinimumConfidenceText = settings.MinimumDetectionConfidence.ToString("0.##", CultureInfo.InvariantCulture);
            MaximumCandidatesText = settings.MaximumDetectionCandidates.ToString(CultureInfo.InvariantCulture);
            InferenceImageSizeText = settings.InferenceImageSize.ToString(CultureInfo.InvariantCulture);
            TimeoutSecondsText = settings.DetectionTimeoutSeconds.ToString(CultureInfo.InvariantCulture);
            AutoStartClient = settings.AutoStartClient;
            RefreshRuntimeProfiles();
        }

        public void LoadFrom(PythonModelSettings settings, AnomalyClassificationSettings anomalySettings)
        {
            LoadFrom(settings);
            LoadAnomalyClassificationFrom(anomalySettings);
        }

        public void LoadAnomalyClassificationFrom(AnomalyClassificationSettings settings)
        {
            settings ??= new AnomalyClassificationSettings();
            settings.EnsureDefaults();
            AnomalyNormalClassNamesText = FormatClassNames(settings.NormalClassNames);
            AnomalyAbnormalClassNamesText = FormatClassNames(settings.AbnormalClassNames);
            AnomalyMinimumConfidenceText = settings.MinimumConfidence.ToString("0.##", CultureInfo.InvariantCulture);
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
                throw new FormatException("최대 후보 수는 정수여야 합니다.");
            }

            if (!int.TryParse(InferenceImageSizeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int inferenceImageSize))
            {
                throw new FormatException("추론 이미지 크기는 정수여야 합니다.");
            }

            settings.PythonExecutablePath = PythonExecutablePath.Trim();
            settings.ModelEngine = PythonModelSettings.NormalizeModelEngine(SelectedModelEngine);
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

        public void ApplyTo(AnomalyClassificationSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (!double.TryParse(AnomalyMinimumConfidenceText, NumberStyles.Float, CultureInfo.InvariantCulture, out double confidence))
            {
                throw new FormatException("\uC774\uC0C1 \uD0D0\uC9C0 \uC2E0\uB8B0\uB3C4\uB294 0\uACFC 1 \uC0AC\uC774 \uC22B\uC790\uC5EC\uC57C \uD569\uB2C8\uB2E4.");
            }

            settings.NormalClassNames = ParseClassNames(AnomalyNormalClassNamesText);
            settings.AbnormalClassNames = ParseClassNames(AnomalyAbnormalClassNamesText);
            settings.MinimumConfidence = Math.Clamp(confidence, 0D, 1D);
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
            IsRuntimeProfileActionEnabled = canRunGeneralCommands;
            isRuntimePackageActionAllowed = canRunGeneralCommands;
            RefreshRuntimeInstallPlan(CreateCurrentSettingsSnapshot());
        }

        public void ApplyRuntimeConnectionResult(PythonModelRuntimeConnectionResult result)
        {
            if (result?.Settings == null)
            {
                return;
            }

            PythonModelSettings settings = result.Settings;
            PythonExecutablePath = settings.PythonExecutablePath ?? string.Empty;
            SelectedModelEngine = settings.ModelEngine;
            ProjectRootPath = settings.ProjectRootPath ?? string.Empty;
            ClientScriptPath = settings.ClientScriptPath ?? string.Empty;
            WeightsPath = settings.WeightsPath ?? string.Empty;
            ImageRootPath = settings.ImageRootPath ?? string.Empty;
            RuntimeProfileActionStatusText = string.IsNullOrWhiteSpace(result.DetailText)
                ? result.SummaryText
                : string.Format(CultureInfo.CurrentCulture, "{0}. {1}", result.SummaryText, result.DetailText);
            RefreshRuntimeProfiles();
        }

        public void ApplyRuntimeCapabilities(
            IEnumerable<string> supportedModels,
            IEnumerable<string> trainingModels,
            IEnumerable<string> detectionModels)
        {
            workerSupportedModels = NormalizeCapabilities(supportedModels);
            workerTrainingModels = NormalizeCapabilities(trainingModels);
            workerDetectionModels = NormalizeCapabilities(detectionModels);
            RefreshRuntimeProfiles();
        }

        public void SetRuntimeProfileActionStatus(string statusText)
        {
            RuntimeProfileActionStatusText = statusText;
        }

        public void SetRuntimePackageOperationResult(string summaryText, string detailText)
        {
            RuntimePackageResultSummaryText = summaryText;
            RuntimePackageResultDetailText = detailText;
        }

        private static void NoOpCommand()
        {
        }

        private static void NoOpTextCommand(string value)
        {
        }

        private void ExecuteRuntimeProfileAction(string engine)
        {
            string normalizedEngine = PythonModelSettings.NormalizeModelEngine(engine);
            SelectedModelEngine = normalizedEngine;
            RuntimeProfileActionStatusText = FormatRuntimeProfileActionStatusText(normalizedEngine);
            runtimeProfileAction(normalizedEngine);
            RefreshRuntimeProfiles();
        }

        private void ExecuteRuntimeInstallPackage()
        {
            RuntimeProfileActionStatusText = "Ultralytics \uC124\uCE58\uB97C \uC2DC\uC791\uD569\uB2C8\uB2E4. \uC9C4\uD589 \uC0C1\uD0DC\uB294 \uBAA8\uB378 \uC2E4\uD589\uAE30 \uC0C1\uD0DC\uC640 \uB85C\uADF8\uC5D0 \uD45C\uC2DC\uD569\uB2C8\uB2E4.";
            runtimeInstallPackageAction();
        }

        private void ExecuteRuntimeUninstallPackage()
        {
            RuntimeProfileActionStatusText = "Ultralytics \uC81C\uAC70\uB97C \uC2DC\uC791\uD569\uB2C8\uB2E4. \uBC18\uBCF5 \uD14C\uC2A4\uD2B8\uC6A9 \uC791\uC5C5\uC774\uBA70, \uC81C\uAC70 \uD6C4 self-test\uB97C \uB2E4\uC2DC \uD655\uC778\uD569\uB2C8\uB2E4.";
            runtimeUninstallPackageAction();
        }

        private void NotifySettingsSummaryChanged()
        {
            OnPropertyChanged(nameof(SettingsSummaryModelText));
            OnPropertyChanged(nameof(SettingsSummaryRuntimeText));
            OnPropertyChanged(nameof(SettingsSummaryRuntimeStatusText));
            OnPropertyChanged(nameof(SettingsSummaryPathText));
            RefreshRuntimeProfiles();
        }

        private void NotifyAnomalyMappingChanged()
        {
            OnPropertyChanged(nameof(AnomalyMappingSummaryText));
        }

        private void RefreshRuntimeProfiles()
        {
            PythonModelSettings settings = CreateCurrentSettingsSnapshot();

            RuntimeProfileItems.Clear();
            foreach (PythonModelRuntimeProfile profile in PythonModelRuntimeProfileService.BuildProfiles(
                settings,
                workerSupportedModels,
                workerTrainingModels,
                workerDetectionModels))
            {
                RuntimeProfileItems.Add(profile);
            }

            RefreshModelAdapterCatalog();
            RefreshRuntimeSelfTest(settings);
        }

        private void RefreshModelAdapterCatalog()
        {
            ModelAdapterCatalogItems.Clear();
            foreach (ModelAdapterCatalogItem item in ModelAdapterCatalogService.BuildCatalog())
            {
                ModelAdapterCatalogItems.Add(item);
            }
        }

        private void RefreshRuntimeSelfTest(PythonModelSettings settings)
        {
            PythonModelRuntimeSelfTestReport report = PythonModelRuntimeSelfTestService.BuildReport(settings);
            RuntimeSelfTestTitleText = report.TitleText;
            RuntimeSelfTestSummaryText = report.SummaryText;
            RuntimeSelfTestDetailText = report.DetailText;
            RefreshRuntimeExecutionSummary(settings);

            RuntimeSelfTestItems.Clear();
            foreach (PythonModelRuntimeSelfTestItem item in report.Items)
            {
                RuntimeSelfTestItems.Add(item);
            }

            RefreshRuntimeInstallPlan(settings);
        }

        private void RefreshRuntimeExecutionSummary(PythonModelSettings settings)
        {
            PythonModelRuntimeExecutionSummary summary = PythonModelRuntimeExecutionSummaryService.Build(
                settings,
                workerSupportedModels,
                workerTrainingModels,
                workerDetectionModels);
            RuntimeExecutionTitleText = summary.TitleText;
            RuntimeExecutionSummaryText = summary.SummaryText;
            RuntimeExecutionWorkerText = summary.WorkerText;
            RuntimeExecutionTrainingText = summary.TrainingText;
            RuntimeExecutionInspectionText = summary.InspectionText;
        }

        private static string[] NormalizeCapabilities(IEnumerable<string> values)
            => values?
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
                ?? Array.Empty<string>();

        private void RefreshRuntimeInstallPlan(PythonModelSettings settings)
        {
            PythonModelRuntimeInstallPlan plan = PythonModelRuntimeInstallPlanService.BuildPlan(settings);
            RuntimeInstallPlanVisibility = plan.IsVisible ? Visibility.Visible : Visibility.Collapsed;
            RuntimeInstallPlanTitleText = plan.TitleText;
            RuntimeInstallPlanSummaryText = plan.SummaryText;
            RuntimeInstallPlanDetailText = plan.DetailText;
            RuntimeInstallPlanTargetText = plan.TargetEnvironmentText;
            RuntimeInstallPlanCommandText = plan.CommandText;
            RuntimeInstallPlanUninstallCommandText = plan.UninstallCommandText;
            IsRuntimeInstallPackageEnabled = isRuntimePackageActionAllowed && plan.CanRunInstall;
            IsRuntimeUninstallPackageEnabled = isRuntimePackageActionAllowed && plan.CanRunUninstall;
        }

        private PythonModelSettings CreateCurrentSettingsSnapshot()
        {
            return new PythonModelSettings
            {
                PythonExecutablePath = PythonExecutablePath,
                ModelEngine = SelectedModelEngine,
                ProjectRootPath = ProjectRootPath,
                ClientScriptPath = ClientScriptPath,
                WeightsPath = WeightsPath,
                ImageRootPath = ImageRootPath
            };
        }

        private static string FormatPathLeaf(string path, string fallback)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return fallback;
            }

            string trimmed = path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return fallback;
            }

            string leaf = Path.GetFileName(trimmed);
            return string.IsNullOrWhiteSpace(leaf) ? trimmed : leaf;
        }

        private static string FormatClassNames(System.Collections.Generic.IEnumerable<string> classNames)
        {
            return string.Join(", ", classNames ?? Array.Empty<string>());
        }

        private static System.Collections.Generic.List<string> ParseClassNames(string text)
        {
            return (text ?? string.Empty)
                .Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static int CountClassNames(string text)
        {
            return ParseClassNames(text).Count;
        }

        private static string FormatAnomalyConfidence(string text)
        {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double confidence)
                ? Math.Clamp(confidence, 0D, 1D).ToString("0.##", CultureInfo.InvariantCulture)
                : "-";
        }

        private static string FormatModelProfileName(string engine)
        {
            string normalized = PythonModelSettings.NormalizeModelEngine(engine);
            return normalized switch
            {
                PythonModelSettings.EngineYoloV8 => "YOLOv8 \uAC1D\uCCB4\uD0D0\uC9C0",
                PythonModelSettings.EngineYolo11 => "YOLO11 \uAC1D\uCCB4\uD0D0\uC9C0",
                PythonModelSettings.EngineOnnx => "ONNX \uCD94\uB860",
                _ => "YOLOv5 \uAC1D\uCCB4\uD0D0\uC9C0"
            };
        }

        private static string FormatRuntimeProfileActionStatusText(string engine)
            => PythonModelSettings.NormalizeModelEngine(engine) switch
            {
                PythonModelSettings.EngineYoloV8 => "YOLOv8 \uC120\uD0DD\uB428. \uB2E4\uC74C \uC791\uC5C5\uC740 Ultralytics \uC2E4\uD589\uAE30 \uC124\uCE58 \uB610\uB294 Python \uACBD\uB85C \uC5F0\uACB0\uC785\uB2C8\uB2E4.",
                PythonModelSettings.EngineYolo11 => "YOLO11 \uC120\uD0DD\uB428. \uB2E4\uC74C \uC791\uC5C5\uC740 Ultralytics \uC2E4\uD589\uAE30 \uC124\uCE58 \uB610\uB294 Python \uACBD\uB85C \uC5F0\uACB0\uC785\uB2C8\uB2E4.",
                PythonModelSettings.EngineOnnx => "ONNX \uC120\uD0DD\uB428. \uAC80\uC0AC\uC5D0 \uC4F8 .onnx \uBAA8\uB378 \uD30C\uC77C\uC744 \uC120\uD0DD\uD558\uACE0 \uC800\uC7A5\uD558\uC138\uC694.",
                _ => "YOLOv5 \uC120\uD0DD\uB428. \uAE30\uC874 YOLOv5 \uD3F4\uB354\uB97C \uC5F0\uACB0\uD55C \uB4A4 \uC800\uC7A5\uD558\uBA74 \uC774 \uD504\uB85C\uD544\uB85C \uD559\uC2B5/\uAC80\uC0AC\uB97C \uC2E4\uD589\uD569\uB2C8\uB2E4."
            };
    }
}
