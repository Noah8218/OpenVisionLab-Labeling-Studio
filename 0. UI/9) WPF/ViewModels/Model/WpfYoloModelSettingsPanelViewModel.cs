using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using OpenVisionLab;
using OpenVisionLab.Mvvm;
using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;

namespace MvcVisionSystem
{
    public sealed class WpfYoloModelSettingsPanelViewModel : WpfObservableViewModel
    {
        private static readonly Action<object> NoOpObjectCommand = _ => { };
        private string pythonExecutablePath = string.Empty;
        private string selectedModelEngine = PythonModelSettings.EngineYoloV5;
        private string modelEngineBeforeDropDown = PythonModelSettings.EngineYoloV5;
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
        private bool applyingModelEngineDefaults;
        private bool loadingAppliedSettings;
        private bool isSettingsDirty;
        private string appliedEditorSignature = string.Empty;
        private PythonModelSettings appliedModelSettings = new PythonModelSettings();
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
        private Func<YoloModelSettingsPathCallbacks> pathSelectionCallbacksProvider;
        private ICommand saveSettingsCommand = new RelayCommand(NoOpCommand);
        private ICommand resetSettingsCommand = new RelayCommand(NoOpCommand);
        private ICommand cancelChangesCommand = new RelayCommand(NoOpCommand);
        private ICommand runtimeProfileActionCommand = new RelayCommand<string>(NoOpTextCommand);
        private ICommand modelEngineDropDownOpenedCommand = new RelayCommand<object>(NoOpObjectCommand);
        private ICommand modelEngineDropDownClosedCommand = new RelayCommand<object>(NoOpObjectCommand);
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
            => "레시피의 작업·데이터 설정에 맞는 모델과 실행 환경을 사용하세요.";

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

        public ICommand CancelChangesCommand
        {
            get => cancelChangesCommand;
            private set => SetProperty(ref cancelChangesCommand, value);
        }

        public ICommand RuntimeProfileActionCommand
        {
            get => runtimeProfileActionCommand;
            private set => SetProperty(ref runtimeProfileActionCommand, value);
        }

        public ICommand ModelEngineDropDownOpenedCommand
        {
            get => modelEngineDropDownOpenedCommand;
            private set => SetProperty(ref modelEngineDropDownOpenedCommand, value);
        }

        public ICommand ModelEngineDropDownClosedCommand
        {
            get => modelEngineDropDownClosedCommand;
            private set => SetProperty(ref modelEngineDropDownClosedCommand, value);
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
                    if (!loadingAppliedSettings
                        && !applyingModelEngineDefaults
                        && string.Equals(selectedModelEngine, PythonModelSettings.EngineUnet, StringComparison.Ordinal))
                    {
                        ApplyUnetRuntimeDefaults();
                    }
                    else if (!loadingAppliedSettings
                        && !applyingModelEngineDefaults
                        && string.Equals(selectedModelEngine, PythonModelSettings.EnginePatchCore, StringComparison.Ordinal))
                    {
                        ApplyPatchCoreRuntimeDefaults();
                    }

                    OnPropertyChanged(nameof(ModelEngineHintText));
                    NotifySettingsSummaryChanged();
                }
            }
        }

        public string RuntimeProfileHeaderText => OpenVisionLanguageService.T("WpfModelSettings.RuntimeConnectionTitle");

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

        public string SettingsSummaryTitleText => OpenVisionLanguageService.T("WpfModelSettings.SettingsSummaryTitleText");

        public string SettingsSummaryModelText
            => string.Format(
                CultureInfo.CurrentCulture,
                OpenVisionLanguageService.T("WpfModelSettings.SummaryModel"),
                FormatModelProfileName(appliedModelSettings.ModelEngine),
                FormatPathLeaf(appliedModelSettings.WeightsPath, OpenVisionLanguageService.T("WpfTrainingWorkspace.NoModel")));

        public string SettingsSummaryRuntimeText
            => string.Format(
                CultureInfo.CurrentCulture,
                OpenVisionLanguageService.T("WpfModelSettings.SummaryRuntime"),
                appliedModelSettings.MinimumDetectionConfidence.ToString("R", CultureInfo.InvariantCulture),
                appliedModelSettings.InferenceImageSize.ToString(CultureInfo.InvariantCulture),
                appliedModelSettings.MaximumDetectionCandidates.ToString(CultureInfo.InvariantCulture),
                appliedModelSettings.DetectionTimeoutSeconds.ToString(CultureInfo.InvariantCulture));

        public string SettingsSummaryRuntimeStatusText
            => LocalizeRuntimeSummary(PythonModelRuntimeExecutionSummaryService.Build(
                appliedModelSettings,
                workerSupportedModels,
                workerTrainingModels,
                workerDetectionModels).SummaryText);

        public string SettingsSummaryPathText
            => string.Format(
                CultureInfo.CurrentCulture,
                OpenVisionLanguageService.T("WpfModelSettings.SummaryPath"),
                FormatPathLeaf(appliedModelSettings.ImageRootPath, OpenVisionLanguageService.T("WpfTrainingWorkspace.NoModel")),
                FormatPathLeaf(appliedModelSettings.PythonExecutablePath, OpenVisionLanguageService.T("WpfTrainingWorkspace.NoModel")));

        public string SettingsSummaryActionText
            => OpenVisionLanguageService.T("WpfModelSettings.SettingsSummaryActionText");

        public string EditingSummaryTitleText => OpenVisionLanguageService.T("WpfModelSettings.EditingSummaryTitleText");

        public string EditingSummaryText
            => string.Format(
                CultureInfo.CurrentCulture,
                "{0} / {1}",
                FormatModelProfileName(SelectedModelEngine),
                FormatPathLeaf(WeightsPath, OpenVisionLanguageService.T("WpfModelSettings.ModelFileUnconfigured")));

        public string EditStateText
            => IsSettingsDirty
                ? OpenVisionLanguageService.T("WpfModelSettings.Dirty")
                : OpenVisionLanguageService.T("WpfModelSettings.Clean");

        public string AppliedWeightsPath => appliedModelSettings.WeightsPath ?? string.Empty;

        public string SettingsSaveScopeText
            => string.Format(CultureInfo.CurrentCulture, OpenVisionLanguageService.T("WpfTrainingSettings.SaveScope"),
                OpenVisionLanguageService.T(IsSettingsDirty ? "WpfTrainingSettings.Unsaved" : "WpfTrainingSettings.Saved"));

        public bool IsSettingsDirty
        {
            get => isSettingsDirty;
            private set
            {
                if (SetProperty(ref isSettingsDirty, value))
                {
                    OnPropertyChanged(nameof(EditStateText));
                    OnPropertyChanged(nameof(SettingsSaveScopeText));
                }
            }
        }

        public string AnomalyMappingHeaderText => OpenVisionLanguageService.T("WpfAnomalyMapping.Title");

        public string AnomalyMappingSummaryText
            => string.Format(
                CultureInfo.CurrentCulture,
                OpenVisionLanguageService.T("WpfAnomalyMapping.Summary"),
                CountClassNames(AnomalyNormalClassNamesText),
                CountClassNames(AnomalyAbnormalClassNamesText),
                FormatAnomalyConfidence(AnomalyMinimumConfidenceText));

        public string AdvancedSettingsHeaderText
            => OpenVisionLanguageService.T("WpfModelSettings.AdvancedTitle");

        public string ModelEngineHintText
        {
            get
            {
                return SelectedModelEngine switch
                {
                    PythonModelSettings.EngineYoloV8 => OpenVisionLanguageService.T("WpfModelSettings.Hint0"),
                    PythonModelSettings.EngineYolo11 => OpenVisionLanguageService.T("WpfModelSettings.Hint1"),
                    PythonModelSettings.EngineUnet => OpenVisionLanguageService.T("WpfModelSettings.Hint2"),
                    PythonModelSettings.EnginePatchCore => OpenVisionLanguageService.T("WpfModelSettings.Hint3"),
                    PythonModelSettings.EngineOnnx => OpenVisionLanguageService.T("WpfModelSettings.Hint4"),
                    _ => OpenVisionLanguageService.T("WpfModelSettings.Hint5")
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
            set
            {
                if (SetProperty(ref autoStartClient, value))
                {
                    NotifySettingsSummaryChanged();
                }
            }
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
            Action runtimeUninstallPackageAction = null,
            Action cancelChanges = null)
        {
            // Path-picker commands are injected to keep file dialogs in the shell, not in view code-behind.
            BrowsePythonCommand = new RelayCommand(browsePython ?? NoOpCommand);
            BrowseProjectRootCommand = new RelayCommand(browseProjectRoot ?? NoOpCommand);
            BrowseClientScriptCommand = new RelayCommand(browseClientScript ?? NoOpCommand);
            BrowseWeightsCommand = new RelayCommand(browseWeights ?? NoOpCommand);
            BrowseImageRootCommand = new RelayCommand(browseImageRoot ?? NoOpCommand);
            SaveSettingsCommand = new RelayCommand(saveSettings ?? NoOpCommand);
            ResetSettingsCommand = new RelayCommand(resetSettings ?? NoOpCommand);
            CancelChangesCommand = new RelayCommand(cancelChanges ?? NoOpCommand);
            this.runtimeProfileAction = runtimeProfileAction ?? NoOpTextCommand;
            RuntimeProfileActionCommand = new RelayCommand<string>(ExecuteRuntimeProfileAction);
            ModelEngineDropDownOpenedCommand = new RelayCommand<object>(ExecuteModelEngineDropDownOpened);
            ModelEngineDropDownClosedCommand = new RelayCommand<object>(ExecuteModelEngineDropDownClosed, _ => IsRuntimeProfileActionEnabled);
            this.runtimeInstallPackageAction = runtimeInstallPackageAction ?? NoOpCommand;
            this.runtimeUninstallPackageAction = runtimeUninstallPackageAction ?? NoOpCommand;
            RuntimeInstallPackageCommand = new RelayCommand(ExecuteRuntimeInstallPackage);
            RuntimeUninstallPackageCommand = new RelayCommand(ExecuteRuntimeUninstallPackage);
        }

        public void ConfigurePathSelectionWorkflow(Func<YoloModelSettingsPathCallbacks> callbacksProvider)
        {
            pathSelectionCallbacksProvider = callbacksProvider
                ?? throw new ArgumentNullException(nameof(callbacksProvider));
            BrowsePythonCommand = new RelayCommand(ExecuteBrowsePythonCommand);
            BrowseProjectRootCommand = new RelayCommand(ExecuteBrowseProjectRootCommand);
            BrowseClientScriptCommand = new RelayCommand(ExecuteBrowseClientScriptCommand);
            BrowseWeightsCommand = new RelayCommand(ExecuteBrowseWeightsCommand);
            BrowseImageRootCommand = new RelayCommand(ExecuteBrowseImageRootCommand);
        }

        private void ExecuteBrowsePythonCommand()
        {
            YoloModelSettingsPathCallbacks callbacks = pathSelectionCallbacksProvider?.Invoke();
            if (callbacks?.IsApplicationCloseApproved?.Invoke() == true)
            {
                return;
            }

            string selectedPath = callbacks?.SelectFile?.Invoke(
                "Select Python executable",
                "Python executable (python*.exe)|python*.exe|Executable files (*.exe)|*.exe|All files (*.*)|*.*",
                PythonExecutablePath);
            if (string.IsNullOrWhiteSpace(selectedPath)
                || callbacks?.IsApplicationCloseApproved?.Invoke() == true)
            {
                return;
            }

            PythonExecutablePath = selectedPath;
            callbacks.PathSelected?.Invoke("Python 실행 파일", selectedPath);
        }

        private void ExecuteBrowseProjectRootCommand()
        {
            YoloModelSettingsPathCallbacks callbacks = pathSelectionCallbacksProvider?.Invoke();
            if (callbacks?.IsApplicationCloseApproved?.Invoke() == true)
            {
                return;
            }

            string selectedPath = callbacks?.SelectFolder?.Invoke("YOLO 프로젝트 폴더 선택", ProjectRootPath);
            if (string.IsNullOrWhiteSpace(selectedPath)
                || callbacks?.IsApplicationCloseApproved?.Invoke() == true)
            {
                return;
            }

            ProjectRootPath = selectedPath;
            callbacks.PathSelected?.Invoke("YOLO 프로젝트 폴더", selectedPath);
        }

        private void ExecuteBrowseClientScriptCommand()
        {
            YoloModelSettingsPathCallbacks callbacks = pathSelectionCallbacksProvider?.Invoke();
            if (callbacks?.IsApplicationCloseApproved?.Invoke() == true)
            {
                return;
            }

            string selectedPath = callbacks?.SelectFile?.Invoke(
                "추론 실행 스크립트 선택",
                "실행 스크립트 (*.py)|*.py|All files (*.*)|*.*",
                ClientScriptPath);
            if (string.IsNullOrWhiteSpace(selectedPath)
                || callbacks?.IsApplicationCloseApproved?.Invoke() == true)
            {
                return;
            }

            ClientScriptPath = selectedPath;
            callbacks.PathSelected?.Invoke("추론 실행 스크립트", selectedPath);
        }

        private void ExecuteBrowseWeightsCommand()
        {
            YoloModelSettingsPathCallbacks callbacks = pathSelectionCallbacksProvider?.Invoke();
            if (callbacks?.IsApplicationCloseApproved?.Invoke() == true)
            {
                return;
            }

            string selectedPath = callbacks?.SelectFile?.Invoke(
                "검사용 모델 파일 선택",
                "모델 파일 (*.pt;*.pth)|*.pt;*.pth|All files (*.*)|*.*",
                WeightsPath);
            if (string.IsNullOrWhiteSpace(selectedPath)
                || callbacks?.IsApplicationCloseApproved?.Invoke() == true)
            {
                return;
            }

            WeightsPath = selectedPath;
            callbacks.WeightsSelected?.Invoke(selectedPath);
            callbacks.PathSelected?.Invoke("검사용 모델 파일", selectedPath);
        }

        private void ExecuteBrowseImageRootCommand()
        {
            YoloModelSettingsPathCallbacks callbacks = pathSelectionCallbacksProvider?.Invoke();
            if (callbacks?.IsApplicationCloseApproved?.Invoke() == true)
            {
                return;
            }

            string selectedPath = callbacks?.SelectFolder?.Invoke(OpenVisionLab.OpenVisionLanguageService.T("WpfDataset.Picker.ImageFolder"), ImageRootPath);
            if (string.IsNullOrWhiteSpace(selectedPath)
                || callbacks?.IsApplicationCloseApproved?.Invoke() == true)
            {
                return;
            }

            ImageRootPath = selectedPath;
            callbacks.PathSelected?.Invoke("이미지 루트 폴더", selectedPath);
        }

        public void LoadFrom(PythonModelSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            PythonModelRuntimePathResolver.ApplyDefaults(settings);

            loadingAppliedSettings = true;
            try
            {
                LoadModelEditorFields(settings);
                appliedModelSettings = CloneSettings(settings);
                appliedEditorSignature = BuildEditorSignature();
            }
            finally
            {
                loadingAppliedSettings = false;
            }

            modelEngineBeforeDropDown = selectedModelEngine;

            NotifyAppliedSettingsChanged();
            RefreshDirtyState();
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
            AnomalyMinimumConfidenceText = settings.MinimumConfidence.ToString("R", CultureInfo.InvariantCulture);
            appliedEditorSignature = BuildEditorSignature();
            RefreshDirtyState();
        }

        public void LoadDraftDefaults()
        {
            var defaults = new PythonModelSettings();
            PythonModelRuntimePathResolver.ApplyDefaults(defaults);
            loadingAppliedSettings = true;
            try
            {
                LoadModelEditorFields(defaults);
            }
            finally
            {
                loadingAppliedSettings = false;
            }

            modelEngineBeforeDropDown = selectedModelEngine;

            RefreshRuntimeProfiles();
            RefreshDirtyState();
        }

        /// <summary>
        /// Builds the runtime-settings snapshot used by connection and package
        /// commands. The editor owns the values currently being edited; the
        /// applied settings provide the remaining runtime values without being
        /// mutated.
        /// </summary>
        public PythonModelSettings CreateSettingsSnapshot(PythonModelSettings fallbackSettings = null)
        {
            PythonModelSettings fallback = fallbackSettings ?? new PythonModelSettings();
            return new PythonModelSettings
            {
                PythonExecutablePath = PythonExecutablePath ?? fallback.PythonExecutablePath,
                ModelEngine = SelectedModelEngine ?? fallback.ModelEngine,
                ProjectRootPath = ProjectRootPath ?? fallback.ProjectRootPath,
                ClientScriptPath = ClientScriptPath ?? fallback.ClientScriptPath,
                WeightsPath = WeightsPath ?? fallback.WeightsPath,
                ImageRootPath = ImageRootPath ?? fallback.ImageRootPath,
                MinimumDetectionConfidence = fallback.MinimumDetectionConfidence,
                MaximumDetectionCandidates = fallback.MaximumDetectionCandidates,
                InferenceImageSize = fallback.InferenceImageSize,
                DetectionTimeoutSeconds = fallback.DetectionTimeoutSeconds,
                AutoStartClient = fallback.AutoStartClient
            };
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
            PythonModelRuntimePathResolver.ApplyDefaults(settings);
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

        public void ApplyWorkflowCommandState(WorkflowCommandState state)
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

        private void ExecuteModelEngineDropDownOpened(object value)
        {
            modelEngineBeforeDropDown = SelectedModelEngine;
        }

        private void ExecuteModelEngineDropDownClosed(object value)
        {
            if (loadingAppliedSettings || applyingModelEngineDefaults)
            {
                return;
            }

            string normalizedEngine = PythonModelSettings.NormalizeModelEngine(SelectedModelEngine);
            if (string.Equals(modelEngineBeforeDropDown, normalizedEngine, StringComparison.Ordinal))
            {
                return;
            }

            ExecuteRuntimeProfileAction(normalizedEngine);
        }

        private void ExecuteRuntimeProfileAction(string engine)
        {
            string normalizedEngine = PythonModelSettings.NormalizeModelEngine(engine);
            modelEngineBeforeDropDown = normalizedEngine;
            SelectedModelEngine = normalizedEngine;
            RuntimeProfileActionStatusText = FormatRuntimeProfileActionStatusText(normalizedEngine);
            runtimeProfileAction(normalizedEngine);
            RefreshRuntimeProfiles();
        }

        private void ApplyUnetRuntimeDefaults()
        {
            applyingModelEngineDefaults = true;
            try
            {
                PythonModelRuntimeConnectionResult result = PythonModelRuntimeConnectionService.BuildUnetFolderConnection(
                    CreateCurrentSettingsSnapshot(),
                    PythonModelRuntimePathResolver.GetDefaultUnetProjectRootPath());
                PythonExecutablePath = result.Settings.PythonExecutablePath ?? string.Empty;
                ProjectRootPath = result.Settings.ProjectRootPath ?? string.Empty;
                ClientScriptPath = result.Settings.ClientScriptPath ?? string.Empty;
                WeightsPath = result.Settings.WeightsPath ?? string.Empty;
                RuntimeProfileActionStatusText = result.SummaryText;
            }
            finally
            {
                applyingModelEngineDefaults = false;
            }
        }

        private void ApplyPatchCoreRuntimeDefaults()
        {
            applyingModelEngineDefaults = true;
            try
            {
                PythonModelRuntimeConnectionResult result = PythonModelRuntimeConnectionService.BuildPatchCoreConnection(
                    CreateCurrentSettingsSnapshot(),
                    PythonExecutablePath);
                PythonExecutablePath = result.Settings.PythonExecutablePath ?? string.Empty;
                ProjectRootPath = result.Settings.ProjectRootPath ?? string.Empty;
                ClientScriptPath = result.Settings.ClientScriptPath ?? string.Empty;
                WeightsPath = result.Settings.WeightsPath ?? string.Empty;
                if (CountClassNames(AnomalyNormalClassNamesText) == 0)
                {
                    AnomalyNormalClassNamesText = "normal";
                }

                if (CountClassNames(AnomalyAbnormalClassNamesText) == 0)
                {
                    AnomalyAbnormalClassNamesText = "abnormal";
                }
                RuntimeProfileActionStatusText = result.SummaryText;
            }
            finally
            {
                applyingModelEngineDefaults = false;
            }
        }

        private void ExecuteRuntimeInstallPackage()
        {
            RuntimeProfileActionStatusText = "\uc120\ud0dd\ud55c \uc2e4\ud589 \ud658\uacbd\uc758 \ud328\ud0a4\uc9c0 \uc124\uce58\ub97c \uc2dc\uc791\ud569\ub2c8\ub2e4. \uc9c4\ud589 \uc0c1\ud0dc\ub294 \uc2e4\ud589 \ud658\uacbd \uc0c1\ud0dc\uc640 \ub85c\uadf8\uc5d0 \ud45c\uc2dc\ud569\ub2c8\ub2e4.";
            runtimeInstallPackageAction();
        }

        private void ExecuteRuntimeUninstallPackage()
        {
            RuntimeProfileActionStatusText = "\uc120\ud0dd\ud55c \uc2e4\ud589 \ud658\uacbd\uc758 \ud328\ud0a4\uc9c0 \uc81c\uac70\ub97c \uc2dc\uc791\ud569\ub2c8\ub2e4. \uc81c\uac70 \ud6c4 \uc2e4\ud589 \ud658\uacbd\uc744 \ub2e4\uc2dc \uc810\uac80\ud569\ub2c8\ub2e4.";
            runtimeUninstallPackageAction();
        }

        private static string LocalizeRuntimeSummary(string summary)
        {
            foreach (string key in new[] { "WpfModelSettings.StatusUnknown", "WpfModelSettings.StatusReady", "WpfModelSettings.StatusInspectOnly", "WpfModelSettings.StatusTrainOnly", "WpfModelSettings.StatusIncomplete", "WpfModelSettings.StatusMissing" })
            {
                IReadOnlyList<string> previous = OpenVisionLanguageService.GetPreviousKoreanDefaults(key);
                foreach (string source in previous)
                {
                    if (summary.EndsWith(source, StringComparison.Ordinal)) return summary.Substring(0, summary.Length - source.Length) + OpenVisionLanguageService.T(key);
                }
                if (LocalizationTextRuntimeService.TryGetCachedTextPair(key, out string korean, out string english))
                {
                    foreach (string source in new[] { korean, english })
                    {
                        if (source.Length > 0 && summary.EndsWith(source, StringComparison.Ordinal)) return summary.Substring(0, summary.Length - source.Length) + OpenVisionLanguageService.T(key);
                    }
                }
            }
            return summary;
        }

        internal void RefreshLocalizedPresentation()
        {
            foreach (string property in new[] { nameof(SettingsSummaryTitleText), nameof(SettingsSummaryActionText), nameof(EditingSummaryTitleText), nameof(EditStateText), nameof(SettingsSaveScopeText), nameof(ModelEngineHintText), nameof(AnomalyMappingHeaderText), nameof(AnomalyMappingSummaryText), nameof(AdvancedSettingsHeaderText), nameof(RuntimeProfileHeaderText) }) OnPropertyChanged(property);
            NotifyAppliedSettingsChanged();
        }

        private void NotifySettingsSummaryChanged()
        {
            OnPropertyChanged(nameof(SettingsSummaryModelText));
            OnPropertyChanged(nameof(SettingsSummaryRuntimeText));
            OnPropertyChanged(nameof(SettingsSummaryRuntimeStatusText));
            OnPropertyChanged(nameof(SettingsSummaryPathText));
            RefreshRuntimeProfiles();
            RefreshDirtyState();
        }

        private void NotifyAnomalyMappingChanged()
        {
            OnPropertyChanged(nameof(AnomalyMappingSummaryText));
            RefreshDirtyState();
        }

        private void LoadModelEditorFields(PythonModelSettings settings)
        {
            PythonExecutablePath = PythonModelSettingsValidator.ResolvePythonExecutable(settings);
            SelectedModelEngine = settings.ModelEngine;
            ProjectRootPath = settings.ProjectRootPath ?? string.Empty;
            ClientScriptPath = settings.ClientScriptPath ?? string.Empty;
            WeightsPath = settings.WeightsPath ?? string.Empty;
            ImageRootPath = settings.ImageRootPath ?? string.Empty;
            MinimumConfidenceText = settings.MinimumDetectionConfidence.ToString("R", CultureInfo.InvariantCulture);
            MaximumCandidatesText = settings.MaximumDetectionCandidates.ToString(CultureInfo.InvariantCulture);
            InferenceImageSizeText = settings.InferenceImageSize.ToString(CultureInfo.InvariantCulture);
            TimeoutSecondsText = settings.DetectionTimeoutSeconds.ToString(CultureInfo.InvariantCulture);
            AutoStartClient = settings.AutoStartClient;
            RefreshRuntimeProfiles();
        }

        private void RefreshDirtyState()
        {
            if (!loadingAppliedSettings)
            {
                IsSettingsDirty = !string.Equals(appliedEditorSignature, BuildEditorSignature(), StringComparison.Ordinal);
                OnPropertyChanged(nameof(EditingSummaryText));
            }
        }

        private string BuildEditorSignature()
            => string.Join("\u001f", new[]
            {
                (PythonExecutablePath ?? string.Empty).Trim(),
                PythonModelSettings.NormalizeModelEngine(SelectedModelEngine),
                (ProjectRootPath ?? string.Empty).Trim(),
                (ClientScriptPath ?? string.Empty).Trim(),
                (WeightsPath ?? string.Empty).Trim(),
                (ImageRootPath ?? string.Empty).Trim(),
                (MinimumConfidenceText ?? string.Empty).Trim(),
                (MaximumCandidatesText ?? string.Empty).Trim(),
                (InferenceImageSizeText ?? string.Empty).Trim(),
                (TimeoutSecondsText ?? string.Empty).Trim(),
                AutoStartClient ? "1" : "0",
                (AnomalyNormalClassNamesText ?? string.Empty).Trim(),
                (AnomalyAbnormalClassNamesText ?? string.Empty).Trim(),
                (AnomalyMinimumConfidenceText ?? string.Empty).Trim()
            });

        private void NotifyAppliedSettingsChanged()
        {
            OnPropertyChanged(nameof(SettingsSummaryModelText));
            OnPropertyChanged(nameof(SettingsSummaryRuntimeText));
            OnPropertyChanged(nameof(SettingsSummaryRuntimeStatusText));
            OnPropertyChanged(nameof(SettingsSummaryPathText));
            OnPropertyChanged(nameof(AppliedWeightsPath));
        }

        private static PythonModelSettings CloneSettings(PythonModelSettings source)
        {
            var clone = new PythonModelSettings
            {
                PythonExecutablePath = source.PythonExecutablePath ?? string.Empty,
                ModelEngine = source.ModelEngine,
                ProjectRootPath = source.ProjectRootPath ?? string.Empty,
                ClientScriptPath = source.ClientScriptPath ?? string.Empty,
                WeightsPath = source.WeightsPath ?? string.Empty,
                ImageRootPath = source.ImageRootPath ?? string.Empty,
                MinimumDetectionConfidence = source.MinimumDetectionConfidence,
                MaximumDetectionCandidates = source.MaximumDetectionCandidates,
                InferenceImageSize = source.InferenceImageSize,
                DetectionTimeoutSeconds = source.DetectionTimeoutSeconds,
                AutoStartClient = source.AutoStartClient
            };
            PythonModelRuntimePathResolver.ApplyDefaults(clone);
            return clone;
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
            => CreateSettingsSnapshot();

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
                ? Math.Clamp(confidence, 0D, 1D).ToString("R", CultureInfo.InvariantCulture)
                : "-";
        }

        private static string FormatModelProfileName(string engine)
        {
            string normalized = PythonModelSettings.NormalizeModelEngine(engine);
            return normalized switch
            {
                PythonModelSettings.EngineYoloV8 => OpenVisionLanguageService.T("WpfModelSettings.EngineName.YoloV8"),
                PythonModelSettings.EngineYolo11 => OpenVisionLanguageService.T("WpfModelSettings.EngineName.Yolo11"),
                PythonModelSettings.EngineUnet => OpenVisionLanguageService.T("WpfModelSettings.EngineName.Unet"),
                PythonModelSettings.EnginePatchCore => OpenVisionLanguageService.T("WpfModelSettings.EngineName.PatchCore"),
                PythonModelSettings.EngineOnnx => OpenVisionLanguageService.T("WpfModelSettings.EngineName.Onnx"),
                _ => OpenVisionLanguageService.T("WpfModelSettings.EngineName.YoloV5")
            };
        }

        private static string FormatRuntimeProfileActionStatusText(string engine)
            => PythonModelSettings.NormalizeModelEngine(engine) switch
            {
                PythonModelSettings.EngineYoloV8 => "YOLOv8 \uc120\ud0dd\ub428. Ultralytics \uc2e4\ud589 \ud658\uacbd\uc744 \uc124\uce58\ud558\uac70\ub098 Python \uacbd\ub85c\ub97c \uc5f0\uacb0\ud558\uc138\uc694.",
                PythonModelSettings.EngineYolo11 => "YOLO11 \uc120\ud0dd\ub428. Ultralytics \uc2e4\ud589 \ud658\uacbd\uc744 \uc124\uce58\ud558\uac70\ub098 Python \uacbd\ub85c\ub97c \uc5f0\uacb0\ud558\uc138\uc694.",
                PythonModelSettings.EngineUnet => "U-Net \uc120\ud0dd\ub428. C:\\Git\\unet\uc758 PyTorch \uc2e4\ud589 \ud658\uacbd\uacfc \ubc88\ub4e4 \ud504\ub85c\uadf8\ub7a8\uc774 \uc790\ub3d9 \uc801\uc6a9\ub429\ub2c8\ub2e4.",
                PythonModelSettings.EnginePatchCore => "PatchCore 선택됨. 정상 이미지 학습용 PyTorch 환경과 번들 프로그램이 적용됩니다.",
                PythonModelSettings.EngineOnnx => "ONNX \uc120\ud0dd\ub428. \ucd94\ub860\uc5d0 \uc4f8 .onnx \ubaa8\ub378 \ud30c\uc77c\uc744 \uc120\ud0dd\ud558\uace0 \uc800\uc7a5\ud558\uc138\uc694.",
                _ => "YOLOv5 \uc120\ud0dd\ub428. \uae30\uc874 YOLOv5 \ud3f4\ub354\ub97c \uc5f0\uacb0\ud558\uace0 \uc800\uc7a5\ud558\uba74 \uc774 \ud658\uacbd\uc5d0\uc11c \ud559\uc2b5\u00b7\ucd94\ub860\uc744 \uc2e4\ud589\ud569\ub2c8\ub2e4."
            };
    }

    public sealed class YoloModelSettingsPathCallbacks
    {
        public Func<string, string, string, string> SelectFile { get; init; }
        public Func<string, string, string> SelectFolder { get; init; }
        public Func<bool> IsApplicationCloseApproved { get; init; }
        public Action<string, string> PathSelected { get; init; }
        public Action<string> WeightsSelected { get; init; }
    }
}
