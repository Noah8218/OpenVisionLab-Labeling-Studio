using MvcVisionSystem.Yolo;
using MvcVisionSystem._1._Core;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    // Owns the YOLO environment command composition that used to live in the Shell.
    // Runtime policy and process lifetime remain in the existing concrete services.
    internal sealed class YoloEnvironmentWorkflowAdapter
    {
        #region Fields
        private readonly YoloEnvironmentWorkflowAdapterContext context;
        private readonly YoloEnvironmentWorkflowService yoloEnvironmentWorkflowService;

        private WpfYoloModelSettingsPanelViewModel YoloModelSettingsViewModel => context.YoloModelSettingsViewModelProvider?.Invoke();
        private WpfYoloStatusPanelViewModel YoloStatusViewModel => context.YoloStatusViewModelProvider?.Invoke();
        private WpfTrainingSettingsPanelViewModel TrainingSettingsViewModel => context.TrainingSettingsViewModelProvider?.Invoke();
        private WpfCandidateReviewPanelViewModel CandidateReviewViewModel => context.CandidateReviewViewModelProvider?.Invoke();
        private WpfLabelingShellViewModel ShellViewModel => context.ShellViewModelProvider?.Invoke();
        private TrainingWeightsApplicationWorkflowService trainingWeightsApplicationWorkflowService => context.TrainingWeightsApplicationWorkflowService;
        private LabelingApplicationState applicationState => context.ApplicationState;
        private LabelingProjectData projectData => context.DataProvider?.Invoke();
        private bool isApplicationCloseApproved => context.IsApplicationCloseApproved?.Invoke() == true;
        private string pendingTrainingBaselineWeightsPath
        {
            get => context.PendingTrainingBaselineWeightsPathProvider?.Invoke() ?? string.Empty;
            set => context.SetPendingTrainingBaselineWeightsPath?.Invoke(value ?? string.Empty);
        }
        private bool hasPendingTrainingWeightsRecipeSave
        {
            get => context.HasPendingTrainingWeightsRecipeSaveProvider?.Invoke() == true;
            set => context.SetHasPendingTrainingWeightsRecipeSave?.Invoke(value);
        }
        private bool IsInferenceWorkflowActive => context.IsInferenceWorkflowActive?.Invoke() == true;
        private bool IsDetecting => context.IsDetecting?.Invoke() == true;
        private bool IsBatchDetectionRunning => context.IsBatchDetectionRunning?.Invoke() == true;
        private bool IsTrainingCommandRunning => context.IsTrainingCommandRunning?.Invoke() == true;
        #endregion

        #region Constructors
        internal YoloEnvironmentWorkflowAdapter(YoloEnvironmentWorkflowAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            ArgumentNullException.ThrowIfNull(context.ApplicationState);
            ArgumentNullException.ThrowIfNull(context.DataProvider);
            yoloEnvironmentWorkflowService = CreateYoloEnvironmentWorkflow();
        }

        internal YoloEnvironmentWorkflowService WorkflowService => yoloEnvironmentWorkflowService;
        #endregion

        #region ShellCallbacks
        private void EnsureProjectSettings() => context.EnsureProjectSettings?.Invoke();
        private void NotifyYoloPathSelected(string label, string selectedPath) => context.NotifyYoloPathSelected?.Invoke(label, selectedPath);
        private void RefreshYoloStatus() => context.RefreshYoloStatus?.Invoke();
        private Task RefreshYoloSettingsPanelAsync(PythonModelValidationResult validation = null)
            => context.RefreshYoloSettingsPanelAsync?.Invoke(validation) ?? Task.CompletedTask;
        private void SaveYoloEditorFields() => context.SaveYoloEditorFields?.Invoke();
        private void SaveTrainingEditorFields() => context.SaveTrainingEditorFields?.Invoke();
        private void RefreshCandidateConfidenceFilterFromAppliedSettings()
            => context.RefreshCandidateConfidenceFilterFromAppliedSettings?.Invoke();
        private void PopulateYoloEditorFields() => context.PopulateYoloEditorFields?.Invoke();
        private void PopulateTrainingEditorFields() => context.PopulateTrainingEditorFields?.Invoke();
        private void UpdateYoloTrainingHistoryText() => context.UpdateYoloTrainingHistoryText?.Invoke();
        private void SetGlobalInferenceStatus(string text, bool isBusy, bool isWarning = false)
            => context.SetGlobalInferenceStatus?.Invoke(text, isBusy, isWarning);
        private void SetProjectConfigStatus(string text) => context.SetProjectConfigStatus?.Invoke(text);
        private void SetYoloCommandStatus(string text, bool isBusy) => context.SetYoloCommandStatus?.Invoke(text, isBusy);
        private void SetYoloRecoveryStatus(string titleText, string detailText, string actionText)
            => context.SetYoloRecoveryStatus?.Invoke(titleText, detailText, actionText);
        private void ClearYoloRecoveryStatus() => context.ClearYoloRecoveryStatus?.Invoke();
        private void UpdateYoloCommandButtons() => context.UpdateYoloCommandButtons?.Invoke();
        private void ShowYoloModelCenterWorkflowView() => context.ShowYoloModelCenterWorkflowView?.Invoke();
        private void ShowModelRuntimeUnavailable(string statusText, PythonModelRuntimeState runtimeState)
            => context.ShowModelRuntimeUnavailable?.Invoke(statusText, runtimeState);
        private PythonModelRuntimeState GetPythonModelRuntimeState()
            => context.GetPythonModelRuntimeState?.Invoke();
        private bool EnsureModelRuntimeForInference()
            => context.EnsureModelRuntimeForInference?.Invoke() == true;
        private bool EnsureInferenceModeForDetection()
            => context.EnsureInferenceModeForDetection?.Invoke() == true;
        private Task RunInteractiveDetectionAsync(bool allowSmokeFallback)
            => context.RunInteractiveDetectionAsync?.Invoke(allowSmokeFallback) ?? Task.CompletedTask;
        private void SetWorkflowMode(bool isInferenceMode) => context.SetWorkflowMode?.Invoke(isInferenceMode);
        private void FocusYoloPythonPath() => context.FocusYoloPythonPath?.Invoke();
        private void FocusYoloProjectRoot() => context.FocusYoloProjectRoot?.Invoke();
        private void FocusYoloWeightsPath() => context.FocusYoloWeightsPath?.Invoke();
        private void ExpandYoloAdvancedSettings() => context.ExpandYoloAdvancedSettings?.Invoke();
        private string YoloPythonPathText => context.YoloPythonPathTextProvider?.Invoke() ?? string.Empty;
        private string YoloProjectRootText => context.YoloProjectRootTextProvider?.Invoke() ?? string.Empty;
        private bool TryPickFile(string title, string filter, string initialPath, out string selectedPath)
        {
            selectedPath = context.SelectFile?.Invoke(title, filter, initialPath) ?? string.Empty;
            return !string.IsNullOrWhiteSpace(selectedPath);
        }
        private bool TryPickFolder(string title, string initialPath, out string selectedPath)
        {
            selectedPath = context.SelectFolder?.Invoke(title, initialPath) ?? string.Empty;
            return !string.IsNullOrWhiteSpace(selectedPath);
        }
        private bool SaveProjectConfigFromPanel() => context.SaveProjectConfigFromPanel?.Invoke() == true;
        private void RefreshModelCenterDashboard(string configuredWeightsPathOverride, bool pendingManualWeightsSelection)
            => context.RefreshModelCenterDashboard?.Invoke(configuredWeightsPathOverride, pendingManualWeightsSelection);
        private void AppendLog(string text) => context.AppendLog?.Invoke(text);
        private void SetUltralyticsPackageOperationResult(string summaryText, string detailText)
            => context.SetUltralyticsPackageOperationResult?.Invoke(summaryText, detailText);
        private bool ConfirmUltralyticsPackageOperation(bool uninstall, PythonModelRuntimeInstallPlan plan)
            => context.ConfirmUltralyticsPackageOperation?.Invoke(uninstall, plan) == true;
        private void ApplyYoloWorkerCommandPresentation(YoloWorkerCommandPresentation presentation)
        {
            SetYoloCommandStatus(presentation.StatusText, isBusy: false);
            if (presentation.Recovery != null)
            {
                SetYoloRecoveryStatus(
                    presentation.Recovery.Title,
                    presentation.Recovery.Detail,
                    presentation.Recovery.Action);
            }

            AppendLog(presentation.LogText);
        }
        #endregion

        #region YoloEnvironmentBrowseCommands
        // The ViewModel owns path command entry; this adapter supplies WPF dialogs and presentation side effects.
        internal YoloModelSettingsPathCallbacks CreateYoloModelSettingsPathCallbacks()
        {
            return new YoloModelSettingsPathCallbacks
            {
                SelectFile = (title, filter, initialPath) => TryPickFile(title, filter, initialPath, out string selectedPath)
                    ? selectedPath
                    : string.Empty,
                SelectFolder = (title, initialPath) => TryPickFolder(title, initialPath, out string selectedPath)
                    ? selectedPath
                    : string.Empty,
                IsApplicationCloseApproved = () => isApplicationCloseApproved,
                PathSelected = NotifyYoloPathSelected,
                WeightsSelected = selectedPath =>
                {
                    pendingTrainingBaselineWeightsPath = string.Empty;
                    hasPendingTrainingWeightsRecipeSave = false;
                    RefreshModelCenterDashboard(configuredWeightsPathOverride: selectedPath, pendingManualWeightsSelection: true);
                }
            };
        }

        internal void ExecuteRuntimeProfileActionCommand(string engine)
        {
            string normalizedEngine = PythonModelSettings.NormalizeModelEngine(engine);
            if (YoloModelSettingsViewModel != null)
            {
                YoloModelSettingsViewModel.SelectedModelEngine = normalizedEngine;
            }

            if (context.ExpandYoloAdvancedSettings != null)
            {
                ExpandYoloAdvancedSettings();
            }

            switch (normalizedEngine)
            {
                case PythonModelSettings.EngineYoloV8:
                    ExecuteConnectYoloV8RuntimeFolder();
                    break;
                case PythonModelSettings.EngineYolo11:
                    ExecuteConnectYolo11RuntimeFolder();
                    break;
                case PythonModelSettings.EngineUnet:
                    ExecuteConnectUnetRuntime();
                    break;
                case PythonModelSettings.EnginePatchCore:
                    ExecuteConnectPatchCoreRuntime();
                    break;
                case PythonModelSettings.EngineOnnx:
                    FocusYoloWeightsPath();
                    SetYoloCommandStatus("ONNX \uC120\uD0DD: \uAC80\uC0AC\uC5D0 \uC4F8 .onnx \uBAA8\uB378 \uD30C\uC77C\uC744 \uC120\uD0DD\uD558\uACE0 \uC800\uC7A5\uD558\uC138\uC694.", isBusy: false);
                    AppendLog("ONNX \uCD94\uB860 \uC5F0\uACB0 \uC900\uBE44: \uAC80\uC0AC \uBAA8\uB378 \uD30C\uC77C \uC785\uB825\uB780\uC73C\uB85C \uC774\uB3D9.");
                    break;
                default:
                    ExecuteConnectYoloV5RuntimeFolder();
                    break;
            }
        }

        private void ExecuteConnectUltralyticsRuntime(string engine)
        {
            string displayName = string.Equals(engine, PythonModelSettings.EngineYoloV8, StringComparison.Ordinal)
                ? "YOLOv8"
                : "YOLO11";
            string initialPath = YoloModelSettingsViewModel?.PythonExecutablePath ?? YoloPythonPathText;
            if (!TryPickFile(
                $"{displayName} Ultralytics Python \uC5F0\uACB0",
                "Python executable (python*.exe)|python*.exe|Executable files (*.exe)|*.exe|All files (*.*)|*.*",
                initialPath,
                out string selectedPath))
            {
                FocusYoloPythonPath();
                SetYoloCommandStatus($"{displayName} Ultralytics Python \uC5F0\uACB0\uC744 \uCDE8\uC18C\uD588\uC2B5\uB2C8\uB2E4. \uAE30\uC874 Python \uACBD\uB85C\uB97C \uD655\uC778\uD558\uAC70\uB098 \uB2E4\uC2DC \uC5F0\uACB0\uD558\uC138\uC694.", isBusy: false);
                AppendLog($"{displayName} Ultralytics Python \uC5F0\uACB0 \uCDE8\uC18C.");
                return;
            }

            PythonModelRuntimeConnectionResult result = PythonModelRuntimeConnectionService.BuildUltralyticsPythonConnection(
                YoloModelSettingsViewModel.CreateSettingsSnapshot(projectData?.ProjectSettings?.PythonModel),
                engine,
                selectedPath);
            YoloModelSettingsViewModel?.ApplyRuntimeConnectionResult(result);
            FocusYoloPythonPath();
            SetYoloCommandStatus($"{result.SummaryText}: {result.DetailText}", isBusy: false);
            AppendLog($"{displayName} Ultralytics Python \uC5F0\uACB0: {selectedPath} / {result.SummaryText}");
        }

        private void ExecuteConnectYoloV8RuntimeFolder()
        {
            string initialPath = YoloModelSettingsViewModel?.ProjectRootPath ?? YoloProjectRootText;
            string selectedPath = PythonModelRuntimeConnectionService.ResolveKnownLocalRuntimeFolder(initialPath, "yolov8");
            if (string.IsNullOrWhiteSpace(selectedPath)
                && !TryPickFolder("YOLOv8 \uD3F4\uB354 \uC5F0\uACB0", initialPath, out selectedPath))
            {
                FocusYoloProjectRoot();
                SetYoloCommandStatus("YOLOv8 \uD3F4\uB354 \uC5F0\uACB0\uC744 \uCDE8\uC18C\uD588\uC2B5\uB2C8\uB2E4. \uAE30\uC874 \uACBD\uB85C\uB97C \uD655\uC778\uD558\uAC70\uB098 \uB2E4\uC2DC \uC5F0\uACB0\uD558\uC138\uC694.", isBusy: false);
                AppendLog("YOLOv8 local worker \uD3F4\uB354 \uC5F0\uACB0 \uCDE8\uC18C.");
                return;
            }

            PythonModelRuntimeConnectionResult result = PythonModelRuntimeConnectionService.BuildYoloV8FolderConnection(
                YoloModelSettingsViewModel.CreateSettingsSnapshot(projectData?.ProjectSettings?.PythonModel),
                selectedPath,
                projectData?.ProjectSettings?.DatasetPurpose ?? LabelingDatasetPurpose.ObjectDetection);
            YoloModelSettingsViewModel?.ApplyRuntimeConnectionResult(result);
            TrainingSettingsViewModel?.ApplyModelEngineSelection(result.Settings.ModelEngine);
            FocusYoloProjectRoot();
            SetYoloCommandStatus($"{result.SummaryText}: {result.DetailText}", isBusy: false);
            AppendLog($"YOLOv8 \uD3F4\uB354 \uC5F0\uACB0: {selectedPath} / {result.SummaryText}");
        }

        private void ExecuteConnectYolo11RuntimeFolder()
        {
            string initialPath = YoloModelSettingsViewModel?.ProjectRootPath ?? YoloProjectRootText;
            string selectedPath = PythonModelRuntimeConnectionService.ResolveKnownLocalRuntimeFolder(initialPath, "yolov8");
            if (string.IsNullOrWhiteSpace(selectedPath)
                && !TryPickFolder("YOLO11 Ultralytics 폴더 연결", initialPath, out selectedPath))
            {
                FocusYoloProjectRoot();
                SetYoloCommandStatus("YOLO11 실행 폴더 연결을 취소했습니다. 기존 Ultralytics 폴더를 확인하거나 다시 연결하세요.", isBusy: false);
                AppendLog("YOLO11 Ultralytics 폴더 연결 취소.");
                return;
            }

            PythonModelRuntimeConnectionResult result = PythonModelRuntimeConnectionService.BuildYolo11FolderConnection(
                YoloModelSettingsViewModel.CreateSettingsSnapshot(projectData?.ProjectSettings?.PythonModel),
                selectedPath,
                projectData?.ProjectSettings?.DatasetPurpose ?? LabelingDatasetPurpose.ObjectDetection);
            YoloModelSettingsViewModel?.ApplyRuntimeConnectionResult(result);
            TrainingSettingsViewModel?.ApplyModelEngineSelection(result.Settings.ModelEngine);
            FocusYoloProjectRoot();
            SetYoloCommandStatus($"{result.SummaryText}: {result.DetailText}", isBusy: false);
            AppendLog($"YOLO11 Ultralytics 폴더 연결: {selectedPath} / {result.SummaryText}");
        }

        private void ExecuteConnectYoloV5RuntimeFolder()
        {
            string initialPath = YoloModelSettingsViewModel?.ProjectRootPath ?? YoloProjectRootText;
            string selectedPath = PythonModelRuntimeConnectionService.ResolveKnownLocalRuntimeFolder(initialPath, "yolov5");
            if (string.IsNullOrWhiteSpace(selectedPath)
                && !TryPickFolder("YOLOv5 \uD3F4\uB354 \uC5F0\uACB0", initialPath, out selectedPath))
            {
                FocusYoloProjectRoot();
                SetYoloCommandStatus("YOLOv5 \uD3F4\uB354 \uC5F0\uACB0\uC744 \uCDE8\uC18C\uD588\uC2B5\uB2C8\uB2E4. \uAE30\uC874 \uACBD\uB85C\uB97C \uD655\uC778\uD558\uAC70\uB098 \uB2E4\uC2DC \uC5F0\uACB0\uD558\uC138\uC694.", isBusy: false);
                AppendLog("YOLOv5 \uC2E4\uD589\uAE30 \uD3F4\uB354 \uC5F0\uACB0 \uCDE8\uC18C.");
                return;
            }

            PythonModelRuntimeConnectionResult result = PythonModelRuntimeConnectionService.BuildYoloV5FolderConnection(
                YoloModelSettingsViewModel.CreateSettingsSnapshot(projectData?.ProjectSettings?.PythonModel),
                selectedPath);
            YoloModelSettingsViewModel?.ApplyRuntimeConnectionResult(result);
            TrainingSettingsViewModel?.ApplyModelEngineSelection(result.Settings.ModelEngine);
            FocusYoloProjectRoot();
            SetYoloCommandStatus($"{result.SummaryText}: {result.DetailText}", isBusy: false);
            AppendLog($"YOLOv5 \uD3F4\uB354 \uC5F0\uACB0: {selectedPath} / {result.SummaryText}");
        }

        private void ExecuteConnectUnetRuntime()
        {
            string projectRootPath = PythonModelRuntimePathResolver.GetDefaultUnetProjectRootPath();
            PythonModelRuntimeConnectionResult result = PythonModelRuntimeConnectionService.BuildUnetFolderConnection(
                YoloModelSettingsViewModel.CreateSettingsSnapshot(projectData?.ProjectSettings?.PythonModel),
                projectRootPath);
            YoloModelSettingsViewModel?.ApplyRuntimeConnectionResult(result);
            TrainingSettingsViewModel?.ApplyModelEngineSelection(result.Settings.ModelEngine);
            FocusYoloProjectRoot();
            SetYoloCommandStatus($"{result.SummaryText}: {result.DetailText}", isBusy: false);
            AppendLog($"U-Net runtime profile applied: {projectRootPath} / {result.SummaryText}");
        }

        private void ExecuteConnectPatchCoreRuntime()
        {
            string projectRootPath = PythonModelRuntimePathResolver.GetDefaultPatchCoreProjectRootPath();
            Directory.CreateDirectory(projectRootPath);
            PythonModelRuntimeConnectionResult result = PythonModelRuntimeConnectionService.BuildPatchCoreConnection(
                YoloModelSettingsViewModel.CreateSettingsSnapshot(projectData?.ProjectSettings?.PythonModel),
                YoloModelSettingsViewModel?.PythonExecutablePath ?? string.Empty);
            YoloModelSettingsViewModel?.ApplyRuntimeConnectionResult(result);
            TrainingSettingsViewModel?.ApplyModelEngineSelection(result.Settings.ModelEngine);
            FocusYoloProjectRoot();
            SetYoloCommandStatus($"{result.SummaryText}: {result.DetailText}", isBusy: false);
            AppendLog($"PatchCore runtime profile applied: {result.Settings.ProjectRootPath} / {result.SummaryText}");
        }

        internal void ExecuteSaveYoloSettingsCommand()
        {
            _ = ExecuteSaveYoloSettingsCommandAsync();
        }

        private async Task ExecuteSaveYoloSettingsCommandAsync()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            try
            {
                EnsureProjectSettings();
                bool pendingWeightsRecipeSave = hasPendingTrainingWeightsRecipeSave;
                if (pendingWeightsRecipeSave && CandidateReviewViewModel?.IsModelPromotionHeld == true)
                {
                    string status = ModelCandidateDecisionPresentationService.BuildHeldCandidateSaveBlockedStatus();
                    SetYoloCommandStatus(status, isBusy: false);
                    AppendLog(status);
                    return;
                }

                using RecipeSettingsStateTransaction recipeSettingsTransaction = new RecipeSettingsStateTransaction(projectData);
                PythonModelSettings previousModelSettings = new PythonModelSettings
                {
                    ModelEngine = projectData.ProjectSettings.PythonModel.ModelEngine,
                    ProjectRootPath = projectData.ProjectSettings.PythonModel.ProjectRootPath,
                    WeightsPath = projectData.ProjectSettings.PythonModel.WeightsPath
                };

                SaveYoloEditorFields();
                SaveTrainingEditorFields();
                using ModelRegistryStateTransaction registryTransaction = new ModelRegistryStateTransaction(projectData.ProjectSettings.ModelRegistry);
                ModelRegistryService.RecordConfiguredInspectionModel(
                    projectData.ProjectSettings.ModelRegistry,
                    previousModelSettings,
                    projectData.ProjectSettings.DatasetPurpose);
                if (pendingWeightsRecipeSave)
                {
                    trainingWeightsApplicationWorkflowService.RecordAppliedWeights(
                        new TrainingWeightsApplicationRecordRequest
                        {
                            Data = projectData,
                            WeightsPath = projectData.ProjectSettings.PythonModel.WeightsPath,
                            BaselineWeightsPath = pendingTrainingBaselineWeightsPath,
                            SavedToRecipe = true
                        });
                }
                else
                {
                    ModelRegistryService.RecordConfiguredInspectionModel(
                        projectData.ProjectSettings.ModelRegistry,
                        projectData.ProjectSettings.PythonModel,
                        projectData.ProjectSettings.DatasetPurpose,
                        previousModelSettings.WeightsPath);
                }

                bool configSaved = SaveProjectConfigFromPanel();

                if (configSaved)
                {
                    recipeSettingsTransaction.Commit();
                    registryTransaction.Commit();
                }
                else
                {
                    recipeSettingsTransaction.Rollback();
                    registryTransaction.Rollback();
                }

                if (configSaved)
                {
                    PopulateYoloEditorFields();
                    PopulateTrainingEditorFields();
                }
                else
                {
                    RefreshCandidateConfidenceFilterFromAppliedSettings();
                }

                RefreshYoloStatus();
                UpdateYoloTrainingHistoryText();
                await RefreshYoloSettingsPanelAsync().ConfigureAwait(true);
                if (isApplicationCloseApproved)
                {
                    return;
                }
                AppendLog(configSaved
                    ? "\uBAA8\uB378 \uD504\uB85C\uD544 \uC124\uC815 \uC800\uC7A5 \uC644\uB8CC."
                    : "\uBAA8\uB378 \uD504\uB85C\uD544 \uC124\uC815 \uC800\uC7A5 \uC2E4\uD328. \uD604\uC7AC \uC801\uC6A9 \uAC12\uC740 \uC720\uC9C0\uD558\uACE0 \uD3B8\uC9D1 \uB0B4\uC6A9\uC740 \uC7AC\uC2DC\uB3C4\uD560 \uC218 \uC788\uB3C4\uB85D \uB0A8\uACA8\uB450\uC5C8\uC2B5\uB2C8\uB2E4.");
                if (configSaved && pendingWeightsRecipeSave)
                {
                    hasPendingTrainingWeightsRecipeSave = false;
                    pendingTrainingBaselineWeightsPath = string.Empty;
                    UpdateYoloTrainingHistoryText();
                    RefreshYoloStatus();
                    SetGlobalInferenceStatus(string.Empty, isBusy: false);
                    string appliedModelName = Path.GetFileName(projectData.ProjectSettings.PythonModel.WeightsPath ?? string.Empty);
                    SetYoloCommandStatus($"\uAC80\uC0AC \uBAA8\uB378 \uC801\uC6A9 \uC644\uB8CC: {appliedModelName}. \uB2E4\uC74C \uD604\uC7AC \uAC80\uC0AC\uBD80\uD130 \uC774 \uBAA8\uB378\uC744 \uC0AC\uC6A9\uD569\uB2C8\uB2E4.", isBusy: false);
                    SetProjectConfigStatus("\uD559\uC2B5 \uACB0\uACFC \uBAA8\uB378\uC744 \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378\uB85C \uC800\uC7A5\uD588\uC2B5\uB2C8\uB2E4.");
                }
            }
            catch (Exception ex)
            {
                if (!isApplicationCloseApproved)
                {
                    RefreshCandidateConfidenceFilterFromAppliedSettings();
                    SetYoloCommandStatus($"설정 저장 실패: {ex.Message}", isBusy: false);
                    AppendLog($"\uBAA8\uB378 \uD504\uB85C\uD544 \uC124\uC815 \uC800\uC7A5 \uC2E4\uD328: {ex.Message}");
                }
            }
        }

        internal void ExecuteResetYoloSettingsCommand()
        {
            _ = ExecuteResetYoloSettingsCommandAsync();
        }

        private async Task ExecuteResetYoloSettingsCommandAsync()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            YoloModelSettingsViewModel?.LoadDraftDefaults();
            await RefreshYoloSettingsPanelAsync().ConfigureAwait(true);
            if (isApplicationCloseApproved)
            {
                return;
            }
            SetYoloCommandStatus("기본값을 편집란에 불러왔습니다. Recipe에 저장 및 적용하기 전에는 현재 검사 모델이 바뀌지 않습니다.", isBusy: false);
            AppendLog("YOLO 모델 기본값을 편집란에 불러왔습니다. 현재 적용 설정은 유지됩니다.");
        }
        #endregion

        #region YoloEnvironmentRuntimeCommands
        // Runtime environment commands manage Python/worker state and should not mix with settings field browsing.
        // PL-0037: lazy composition and visual adapters only; execution/state live in one owner.
        private YoloEnvironmentWorkflowService CreateYoloEnvironmentWorkflow()
        {
            return new YoloEnvironmentWorkflowService(
                () =>
                {
                    projectData.ProjectSettings ??= new LabelingProjectSettings();
                    projectData.ProjectSettings.PythonModel ??= new PythonModelSettings();
                    return projectData.ProjectSettings.PythonModel;
                },
                () => YoloModelSettingsViewModel.CreateSettingsSnapshot(projectData?.ProjectSettings?.PythonModel),
                GetPythonModelRuntimeState,
                (timeout, token) => applicationState.RestartPythonModelClientConnectionAsync(timeout, token),
                token => applicationState.StopPythonModelClientConnectionAsync(token),
                requestId =>
                {
                    applicationState.ModelRuntime.DeepLearning.SendHealthCheck(requestId);
                    applicationState.ModelRuntime.DeepLearning.SendModelStatus(requestId, ensureLoaded: false);
                },
                () => YoloRuntimePresentationService.BuildPythonWorkerFailureText(
                    applicationState.GetPythonCommunicationStatusSnapshot(), applicationState.ModelRuntime.PythonClientProcess?.LastError));
        }

        internal YoloEnvironmentCallbacks CreateYoloEnvironmentCallbacks()
        {
            return new YoloEnvironmentCallbacks
            {
                HasConflictingCommand = () => WorkflowCommandStateService.HasActiveCommand(
                    false, IsDetecting, IsBatchDetectionRunning, IsTrainingCommandRunning),
                ClearRecovery = ClearYoloRecoveryStatus,
                SetCommandStatus = SetYoloCommandStatus,
                SetCommandBusy = value => YoloStatusViewModel.SetCommandBusy(value),
                RefreshCommands = UpdateYoloCommandButtons,
                RefreshStatus = RefreshYoloStatus,
                RefreshSettingsAsync = result => RefreshYoloSettingsPanelAsync(result),
                ShowModelCenter = ShowYoloModelCenterWorkflowView,
                ShowRuntimeUnavailable = ShowModelRuntimeUnavailable,
                AppendLog = AppendLog,
                ConfirmPackage = ConfirmUltralyticsPackageOperation,
                SetRuntimeActionStatus = text => YoloModelSettingsViewModel?.SetRuntimeProfileActionStatus(text),
                SetPackageResult = SetUltralyticsPackageOperationResult,
                LoadSettings = settings => YoloModelSettingsViewModel?.LoadFrom(settings),
                RunDiagnosticAsync = () => RunInteractiveDetectionAsync(allowSmokeFallback: true),
                SetRecovery = recovery => SetYoloRecoveryStatus(recovery.Title, recovery.Detail, recovery.Action),
                ApplyWorkerPresentation = ApplyYoloWorkerCommandPresentation
            };
        }

        internal void ExecuteCheckYoloCommand()
            => YoloStatusViewModel?.CheckCommand?.Execute(null);

        internal void ExecuteDetectCurrentImageCommand()
        {
            _ = ExecuteDetectCurrentImageCommandAsync();
        }

        private async Task ExecuteDetectCurrentImageCommandAsync()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            if (!EnsureModelRuntimeForInference())
            {
                return;
            }

            if (!EnsureInferenceModeForDetection())
            {
                return;
            }

            await RunInteractiveDetectionAsync(allowSmokeFallback: false).ConfigureAwait(true);
        }

        internal void ExecuteInstallUltralyticsPackageCommand()
        {
            _ = ExecuteUltralyticsPackageCommandAsync(uninstall: false);
        }

        internal void ExecuteUninstallUltralyticsPackageCommand()
        {
            _ = ExecuteUltralyticsPackageCommandAsync(uninstall: true);
        }

        private Task ExecuteUltralyticsPackageCommandAsync(bool uninstall)
        {
            return yoloEnvironmentWorkflowService.RunPackageAsync(uninstall, CreateYoloEnvironmentCallbacks());
        }

        internal void ExecuteRunYoloSmokeCommand()
        {
            _ = ExecuteRunYoloSmokeCommandAsync();
        }

        private async Task ExecuteRunYoloSmokeCommandAsync()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            if (!IsInferenceWorkflowActive)
            {
                SetWorkflowMode(isInferenceMode: true);
                AppendLog(YoloEnvironmentCommandPresentationService.BuildModelTestModeSwitchLog());
            }

            if (!EnsureModelRuntimeForInference())
            {
                return;
            }

            await yoloEnvironmentWorkflowService.RunModelTestAsync(CreateYoloEnvironmentCallbacks()).ConfigureAwait(true);
        }

        #endregion

    }

    internal sealed class YoloEnvironmentWorkflowAdapterContext
    {
        internal LabelingApplicationState ApplicationState { get; init; }
        internal Func<LabelingProjectData> DataProvider { get; init; }
        internal Func<bool> IsApplicationCloseApproved { get; init; }
        internal Func<WpfYoloModelSettingsPanelViewModel> YoloModelSettingsViewModelProvider { get; init; }
        internal Func<WpfYoloStatusPanelViewModel> YoloStatusViewModelProvider { get; init; }
        internal Func<WpfTrainingSettingsPanelViewModel> TrainingSettingsViewModelProvider { get; init; }
        internal Func<WpfCandidateReviewPanelViewModel> CandidateReviewViewModelProvider { get; init; }
        internal Func<WpfLabelingShellViewModel> ShellViewModelProvider { get; init; }
        internal Func<PythonModelRuntimeState> GetPythonModelRuntimeState { get; init; }
        internal Func<bool> IsDetecting { get; init; }
        internal Func<bool> IsBatchDetectionRunning { get; init; }
        internal Func<bool> IsTrainingCommandRunning { get; init; }
        internal Func<bool> IsInferenceWorkflowActive { get; init; }
        internal Action<bool> SetWorkflowMode { get; init; }
        internal Func<bool> EnsureModelRuntimeForInference { get; init; }
        internal Func<bool> EnsureInferenceModeForDetection { get; init; }
        internal Func<bool, Task> RunInteractiveDetectionAsync { get; init; }
        internal Action EnsureProjectSettings { get; init; }
        internal Action<string, string> NotifyYoloPathSelected { get; init; }
        internal Action RefreshYoloStatus { get; init; }
        internal Func<PythonModelValidationResult, Task> RefreshYoloSettingsPanelAsync { get; init; }
        internal Action SaveYoloEditorFields { get; init; }
        internal Action SaveTrainingEditorFields { get; init; }
        internal Action RefreshCandidateConfidenceFilterFromAppliedSettings { get; init; }
        internal Action PopulateYoloEditorFields { get; init; }
        internal Action PopulateTrainingEditorFields { get; init; }
        internal Action UpdateYoloTrainingHistoryText { get; init; }
        internal Action<string, bool, bool> SetGlobalInferenceStatus { get; init; }
        internal Action<string> SetProjectConfigStatus { get; init; }
        internal Action<string, bool> SetYoloCommandStatus { get; init; }
        internal Action<string, string, string> SetYoloRecoveryStatus { get; init; }
        internal Action ClearYoloRecoveryStatus { get; init; }
        internal Action UpdateYoloCommandButtons { get; init; }
        internal Action ShowYoloModelCenterWorkflowView { get; init; }
        internal Action<string, PythonModelRuntimeState> ShowModelRuntimeUnavailable { get; init; }
        internal Action FocusYoloPythonPath { get; init; }
        internal Action FocusYoloProjectRoot { get; init; }
        internal Action FocusYoloWeightsPath { get; init; }
        internal Action ExpandYoloAdvancedSettings { get; init; }
        internal Func<string> YoloPythonPathTextProvider { get; init; }
        internal Func<string> YoloProjectRootTextProvider { get; init; }
        internal Func<string, string, string, string> SelectFile { get; init; }
        internal Func<string, string, string> SelectFolder { get; init; }
        internal Func<bool> SaveProjectConfigFromPanel { get; init; }
        internal Action<string, bool> RefreshModelCenterDashboard { get; init; }
        internal Action<string> AppendLog { get; init; }
        internal Action<string, string> SetUltralyticsPackageOperationResult { get; init; }
        internal Func<bool, PythonModelRuntimeInstallPlan, bool> ConfirmUltralyticsPackageOperation { get; init; }
        internal Func<string> PendingTrainingBaselineWeightsPathProvider { get; init; }
        internal Action<string> SetPendingTrainingBaselineWeightsPath { get; init; }
        internal Func<bool> HasPendingTrainingWeightsRecipeSaveProvider { get; init; }
        internal Action<bool> SetHasPendingTrainingWeightsRecipeSave { get; init; }
        internal TrainingWeightsApplicationWorkflowService TrainingWeightsApplicationWorkflowService { get; init; }
    }
}
