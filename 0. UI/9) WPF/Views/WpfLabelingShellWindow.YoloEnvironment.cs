using MvcVisionSystem.Yolo;
using MvcVisionSystem._1._Core;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenVisionLab.Wpf.MessageDialogs;

namespace MvcVisionSystem
{
    // Responsibility group: YOLO environment settings and worker commands.
    // These members remain WPF Window adapters; independent policy belongs in services.
    public partial class WpfLabelingShellWindow
    {
        #region YoloEnvironmentBrowseCommands
        // Browse/save/reset commands only edit settings view models and persist project configuration.
        private void ExecuteBrowseYoloPythonCommand()
        {
            if (TryPickFile(
                "Select Python executable",
                "Python executable (python*.exe)|python*.exe|Executable files (*.exe)|*.exe|All files (*.*)|*.*",
                YoloModelSettingsViewModel?.PythonExecutablePath ?? YoloPythonPathBox.Text,
                out string selectedPath))
            {
                if (YoloModelSettingsViewModel != null)
                {
                    YoloModelSettingsViewModel.PythonExecutablePath = selectedPath;
                }

                NotifyYoloPathSelected("Python 실행 파일", selectedPath);
            }
        }

        private void ExecuteBrowseYoloProjectRootCommand()
        {
            if (TryPickFolder("YOLO 프로젝트 폴더 선택", YoloModelSettingsViewModel?.ProjectRootPath ?? YoloProjectRootBox.Text, out string selectedPath))
            {
                if (YoloModelSettingsViewModel != null)
                {
                    YoloModelSettingsViewModel.ProjectRootPath = selectedPath;
                }

                NotifyYoloPathSelected("YOLO 프로젝트 폴더", selectedPath);
            }
        }

        private void ExecuteBrowseYoloClientScriptCommand()
        {
            if (TryPickFile(
                "추론 실행 스크립트 선택",
                "실행 스크립트 (*.py)|*.py|All files (*.*)|*.*",
                YoloModelSettingsViewModel?.ClientScriptPath ?? YoloClientScriptBox.Text,
                out string selectedPath))
            {
                if (YoloModelSettingsViewModel != null)
                {
                    YoloModelSettingsViewModel.ClientScriptPath = selectedPath;
                }

                NotifyYoloPathSelected("추론 실행 스크립트", selectedPath);
            }
        }

        private void ExecuteBrowseYoloWeightsCommand()
        {
            if (TryPickFile(
                "검사용 모델 파일 선택",
                "모델 파일 (*.pt;*.pth)|*.pt;*.pth|All files (*.*)|*.*",
                YoloModelSettingsViewModel?.WeightsPath ?? YoloWeightsPathBox.Text,
                out string selectedPath))
            {
                if (YoloModelSettingsViewModel != null)
                {
                    YoloModelSettingsViewModel.WeightsPath = selectedPath;
                }

                pendingTrainingBaselineWeightsPath = string.Empty;
                hasPendingTrainingWeightsRecipeSave = false;
                RefreshModelCenterDashboard(configuredWeightsPathOverride: selectedPath, pendingManualWeightsSelection: true);
                NotifyYoloPathSelected("검사용 모델 파일", selectedPath);
            }
        }

        private void ExecuteBrowseYoloImageRootCommand()
        {
            if (TryPickFolder("이미지 루트 폴더 선택", YoloModelSettingsViewModel?.ImageRootPath ?? YoloImageRootBox.Text, out string selectedPath))
            {
                if (YoloModelSettingsViewModel != null)
                {
                    YoloModelSettingsViewModel.ImageRootPath = selectedPath;
                }

                NotifyYoloPathSelected("이미지 루트 폴더", selectedPath);
            }
        }

        private void ExecuteRuntimeProfileActionCommand(string engine)
        {
            string normalizedEngine = PythonModelSettings.NormalizeModelEngine(engine);
            if (YoloModelSettingsViewModel != null)
            {
                YoloModelSettingsViewModel.SelectedModelEngine = normalizedEngine;
            }

            if (YoloModelSettingsPanelControl?.AdvancedSettingsExpander != null)
            {
                YoloModelSettingsPanelControl.AdvancedSettingsExpander.IsExpanded = true;
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
                    YoloWeightsPathBox?.Focus();
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
            string initialPath = YoloModelSettingsViewModel?.PythonExecutablePath ?? YoloPythonPathBox.Text;
            if (!TryPickFile(
                $"{displayName} Ultralytics Python \uC5F0\uACB0",
                "Python executable (python*.exe)|python*.exe|Executable files (*.exe)|*.exe|All files (*.*)|*.*",
                initialPath,
                out string selectedPath))
            {
                YoloPythonPathBox?.Focus();
                SetYoloCommandStatus($"{displayName} Ultralytics Python \uC5F0\uACB0\uC744 \uCDE8\uC18C\uD588\uC2B5\uB2C8\uB2E4. \uAE30\uC874 Python \uACBD\uB85C\uB97C \uD655\uC778\uD558\uAC70\uB098 \uB2E4\uC2DC \uC5F0\uACB0\uD558\uC138\uC694.", isBusy: false);
                AppendLog($"{displayName} Ultralytics Python \uC5F0\uACB0 \uCDE8\uC18C.");
                return;
            }

            PythonModelRuntimeConnectionResult result = PythonModelRuntimeConnectionService.BuildUltralyticsPythonConnection(
                YoloModelSettingsViewModel.CreateSettingsSnapshot(global?.Data?.ProjectSettings?.PythonModel),
                engine,
                selectedPath);
            YoloModelSettingsViewModel?.ApplyRuntimeConnectionResult(result);
            YoloPythonPathBox?.Focus();
            SetYoloCommandStatus($"{result.SummaryText}: {result.DetailText}", isBusy: false);
            AppendLog($"{displayName} Ultralytics Python \uC5F0\uACB0: {selectedPath} / {result.SummaryText}");
        }

        private void ExecuteConnectYoloV8RuntimeFolder()
        {
            string initialPath = YoloModelSettingsViewModel?.ProjectRootPath ?? YoloProjectRootBox.Text;
            string selectedPath = PythonModelRuntimeConnectionService.ResolveKnownLocalRuntimeFolder(initialPath, "yolov8");
            if (string.IsNullOrWhiteSpace(selectedPath)
                && !TryPickFolder("YOLOv8 \uD3F4\uB354 \uC5F0\uACB0", initialPath, out selectedPath))
            {
                YoloProjectRootBox?.Focus();
                SetYoloCommandStatus("YOLOv8 \uD3F4\uB354 \uC5F0\uACB0\uC744 \uCDE8\uC18C\uD588\uC2B5\uB2C8\uB2E4. \uAE30\uC874 \uACBD\uB85C\uB97C \uD655\uC778\uD558\uAC70\uB098 \uB2E4\uC2DC \uC5F0\uACB0\uD558\uC138\uC694.", isBusy: false);
                AppendLog("YOLOv8 local worker \uD3F4\uB354 \uC5F0\uACB0 \uCDE8\uC18C.");
                return;
            }

            PythonModelRuntimeConnectionResult result = PythonModelRuntimeConnectionService.BuildYoloV8FolderConnection(
                YoloModelSettingsViewModel.CreateSettingsSnapshot(global?.Data?.ProjectSettings?.PythonModel),
                selectedPath,
                global?.Data?.ProjectSettings?.DatasetPurpose ?? LabelingDatasetPurpose.ObjectDetection);
            YoloModelSettingsViewModel?.ApplyRuntimeConnectionResult(result);
            TrainingSettingsViewModel?.ApplyModelEngineSelection(result.Settings.ModelEngine);
            YoloProjectRootBox?.Focus();
            SetYoloCommandStatus($"{result.SummaryText}: {result.DetailText}", isBusy: false);
            AppendLog($"YOLOv8 \uD3F4\uB354 \uC5F0\uACB0: {selectedPath} / {result.SummaryText}");
        }

        private void ExecuteConnectYolo11RuntimeFolder()
        {
            string initialPath = YoloModelSettingsViewModel?.ProjectRootPath ?? YoloProjectRootBox.Text;
            string selectedPath = PythonModelRuntimeConnectionService.ResolveKnownLocalRuntimeFolder(initialPath, "yolov8");
            if (string.IsNullOrWhiteSpace(selectedPath)
                && !TryPickFolder("YOLO11 Ultralytics 폴더 연결", initialPath, out selectedPath))
            {
                YoloProjectRootBox?.Focus();
                SetYoloCommandStatus("YOLO11 실행 폴더 연결을 취소했습니다. 기존 Ultralytics 폴더를 확인하거나 다시 연결하세요.", isBusy: false);
                AppendLog("YOLO11 Ultralytics 폴더 연결 취소.");
                return;
            }

            PythonModelRuntimeConnectionResult result = PythonModelRuntimeConnectionService.BuildYolo11FolderConnection(
                YoloModelSettingsViewModel.CreateSettingsSnapshot(global?.Data?.ProjectSettings?.PythonModel),
                selectedPath,
                global?.Data?.ProjectSettings?.DatasetPurpose ?? LabelingDatasetPurpose.ObjectDetection);
            YoloModelSettingsViewModel?.ApplyRuntimeConnectionResult(result);
            TrainingSettingsViewModel?.ApplyModelEngineSelection(result.Settings.ModelEngine);
            YoloProjectRootBox?.Focus();
            SetYoloCommandStatus($"{result.SummaryText}: {result.DetailText}", isBusy: false);
            AppendLog($"YOLO11 Ultralytics 폴더 연결: {selectedPath} / {result.SummaryText}");
        }

        private void ExecuteConnectYoloV5RuntimeFolder()
        {
            string initialPath = YoloModelSettingsViewModel?.ProjectRootPath ?? YoloProjectRootBox.Text;
            string selectedPath = PythonModelRuntimeConnectionService.ResolveKnownLocalRuntimeFolder(initialPath, "yolov5");
            if (string.IsNullOrWhiteSpace(selectedPath)
                && !TryPickFolder("YOLOv5 \uD3F4\uB354 \uC5F0\uACB0", initialPath, out selectedPath))
            {
                YoloProjectRootBox?.Focus();
                SetYoloCommandStatus("YOLOv5 \uD3F4\uB354 \uC5F0\uACB0\uC744 \uCDE8\uC18C\uD588\uC2B5\uB2C8\uB2E4. \uAE30\uC874 \uACBD\uB85C\uB97C \uD655\uC778\uD558\uAC70\uB098 \uB2E4\uC2DC \uC5F0\uACB0\uD558\uC138\uC694.", isBusy: false);
                AppendLog("YOLOv5 \uC2E4\uD589\uAE30 \uD3F4\uB354 \uC5F0\uACB0 \uCDE8\uC18C.");
                return;
            }

            PythonModelRuntimeConnectionResult result = PythonModelRuntimeConnectionService.BuildYoloV5FolderConnection(
                YoloModelSettingsViewModel.CreateSettingsSnapshot(global?.Data?.ProjectSettings?.PythonModel),
                selectedPath);
            YoloModelSettingsViewModel?.ApplyRuntimeConnectionResult(result);
            TrainingSettingsViewModel?.ApplyModelEngineSelection(result.Settings.ModelEngine);
            YoloProjectRootBox?.Focus();
            SetYoloCommandStatus($"{result.SummaryText}: {result.DetailText}", isBusy: false);
            AppendLog($"YOLOv5 \uD3F4\uB354 \uC5F0\uACB0: {selectedPath} / {result.SummaryText}");
        }

        private void ExecuteConnectUnetRuntime()
        {
            string projectRootPath = PythonModelRuntimePathResolver.GetDefaultUnetProjectRootPath();
            PythonModelRuntimeConnectionResult result = PythonModelRuntimeConnectionService.BuildUnetFolderConnection(
                YoloModelSettingsViewModel.CreateSettingsSnapshot(global?.Data?.ProjectSettings?.PythonModel),
                projectRootPath);
            YoloModelSettingsViewModel?.ApplyRuntimeConnectionResult(result);
            TrainingSettingsViewModel?.ApplyModelEngineSelection(result.Settings.ModelEngine);
            YoloProjectRootBox?.Focus();
            SetYoloCommandStatus($"{result.SummaryText}: {result.DetailText}", isBusy: false);
            AppendLog($"U-Net runtime profile applied: {projectRootPath} / {result.SummaryText}");
        }

        private void ExecuteConnectPatchCoreRuntime()
        {
            string projectRootPath = PythonModelRuntimePathResolver.GetDefaultPatchCoreProjectRootPath();
            Directory.CreateDirectory(projectRootPath);
            PythonModelRuntimeConnectionResult result = PythonModelRuntimeConnectionService.BuildPatchCoreConnection(
                YoloModelSettingsViewModel.CreateSettingsSnapshot(global?.Data?.ProjectSettings?.PythonModel),
                YoloModelSettingsViewModel?.PythonExecutablePath ?? string.Empty);
            YoloModelSettingsViewModel?.ApplyRuntimeConnectionResult(result);
            TrainingSettingsViewModel?.ApplyModelEngineSelection(result.Settings.ModelEngine);
            YoloProjectRootBox?.Focus();
            SetYoloCommandStatus($"{result.SummaryText}: {result.DetailText}", isBusy: false);
            AppendLog($"PatchCore runtime profile applied: {result.Settings.ProjectRootPath} / {result.SummaryText}");
        }

        private void ExecuteSaveYoloSettingsCommand()
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

                using RecipeSettingsStateTransaction recipeSettingsTransaction = new RecipeSettingsStateTransaction(global.Data);
                PythonModelSettings previousModelSettings = new PythonModelSettings
                {
                    ModelEngine = global.Data.ProjectSettings.PythonModel.ModelEngine,
                    ProjectRootPath = global.Data.ProjectSettings.PythonModel.ProjectRootPath,
                    WeightsPath = global.Data.ProjectSettings.PythonModel.WeightsPath
                };

                SaveYoloEditorFields();
                SaveTrainingEditorFields();
                using ModelRegistryStateTransaction registryTransaction = new ModelRegistryStateTransaction(global.Data.ProjectSettings.ModelRegistry);
                ModelRegistryService.RecordConfiguredInspectionModel(
                    global.Data.ProjectSettings.ModelRegistry,
                    previousModelSettings,
                    global.Data.ProjectSettings.DatasetPurpose);
                if (pendingWeightsRecipeSave)
                {
                    UpdateAppliedTrainingWeightsHistory(global.Data.ProjectSettings.PythonModel.WeightsPath, savedToRecipe: true);
                }
                else
                {
                    ModelRegistryService.RecordConfiguredInspectionModel(
                        global.Data.ProjectSettings.ModelRegistry,
                        global.Data.ProjectSettings.PythonModel,
                        global.Data.ProjectSettings.DatasetPurpose,
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
                    string appliedModelName = Path.GetFileName(global.Data.ProjectSettings.PythonModel.WeightsPath ?? string.Empty);
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

        private void ExecuteResetYoloSettingsCommand()
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
        private void ExecuteCheckYoloCommand()
        {
            _ = ExecuteCheckYoloCommandAsync();
        }

        private async Task ExecuteCheckYoloCommandAsync()
        {
            if (!BeginYoloEnvironmentCommand(YoloEnvironmentCommandPresentationService.BuildEnvironmentCheckStartingStatus()))
            {
                return;
            }

            try
            {
                global.Data.ProjectSettings ??= new LabelingProjectSettings();
                global.Data.ProjectSettings.PythonModel ??= new PythonModelSettings();
                PythonModelRuntimeState runtimeState = GetPythonModelRuntimeState();
                PythonModelValidationResult result = runtimeState.State == PythonModelRuntimeStateKind.NotInstalled
                    ? new PythonModelValidationResult(new[] { runtimeState.NextActionText }, Array.Empty<string>())
                    : PythonModelSettingsValidator.Validate(global.Data.ProjectSettings.PythonModel, requireWeights: true);
                RefreshYoloStatus();
                ShowYoloModelCenterWorkflowView();
                await RefreshYoloSettingsPanelAsync(result).ConfigureAwait(true);
                if (isApplicationCloseApproved)
                {
                    return;
                }

                if (result.IsValid)
                {
                    string readyStatus = YoloEnvironmentCommandPresentationService.BuildEnvironmentReadyStatus();
                    SetYoloCommandStatus(readyStatus, isBusy: false);
                    AppendLog(readyStatus);
                    return;
                }

                SetYoloCommandStatus(YoloEnvironmentCommandPresentationService.BuildEnvironmentNeedsAttentionStatus(), isBusy: false);
                AppendLog(YoloEnvironmentCommandPresentationService.BuildEnvironmentNeedsAttentionLogHeader());
                foreach (string line in result.Errors.Concat(result.Warnings))
                {
                    AppendLog($"- {line}");
                }
            }
            catch (Exception ex)
            {
                if (!isApplicationCloseApproved)
                {
                    string failureStatus = YoloEnvironmentCommandPresentationService.BuildEnvironmentCheckFailureStatus(ex.Message);
                    SetYoloCommandStatus(failureStatus, isBusy: false);
                    AppendLog(failureStatus);
                }
            }
            finally
            {
                EndYoloEnvironmentCommand();
            }
        }

        private void ExecuteDetectCurrentImageCommand()
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

        private void ExecuteInstallRequirementsCommand()
        {
            _ = ExecuteInstallRequirementsCommandAsync();
        }

        private async Task ExecuteInstallRequirementsCommandAsync()
        {
            if (!BeginYoloEnvironmentCommand(YoloEnvironmentCommandPresentationService.BuildRequirementsCheckStartingStatus()))
            {
                return;
            }

            try
            {
                global.Data.ProjectSettings ??= new LabelingProjectSettings();
                global.Data.ProjectSettings.PythonModel ??= new PythonModelSettings();
                PythonModelSettings settings = global.Data.ProjectSettings.PythonModel;
                PythonModelRuntimeState runtimeState = GetPythonModelRuntimeState();
                if (!runtimeState.IsRuntimeInstalled)
                {
                    ShowModelRuntimeUnavailable(runtimeState.NextActionText, runtimeState);
                    return;
                }

                PythonEnvironmentCheckResult check = await PythonEnvironmentService
                    .CheckRequirementsAsync(settings)
                    .ConfigureAwait(true);
                if (isApplicationCloseApproved)
                {
                    return;
                }

                RequirementsCheckPresentation checkPresentation =
                    YoloEnvironmentCommandPresentationService.BuildRequirementsCheckPresentation(check);
                SetYoloCommandStatus(checkPresentation.StatusText, checkPresentation.IsBusy);

                if (!checkPresentation.ShouldInstallRequirements)
                {
                    await RefreshYoloSettingsPanelAsync().ConfigureAwait(true);
                    if (isApplicationCloseApproved)
                    {
                        return;
                    }
                    AppendLog(checkPresentation.LogText);
                    return;
                }

                AppendLog(checkPresentation.LogText);
                PythonPackageInstallResult install = await PythonEnvironmentService
                    .InstallRequirementsAsync(settings)
                    .ConfigureAwait(true);

                await RefreshYoloSettingsPanelAsync().ConfigureAwait(true);
                if (isApplicationCloseApproved)
                {
                    return;
                }
                SetYoloCommandStatus(YoloEnvironmentCommandPresentationService.BuildRequirementsInstallResultStatus(install), isBusy: false);
                AppendLog(YoloEnvironmentCommandPresentationService.BuildRequirementsInstallResultLog(install));
            }
            catch (Exception ex)
            {
                if (!isApplicationCloseApproved)
                {
                    SetYoloCommandStatus(YoloEnvironmentCommandPresentationService.BuildRequirementsInstallFailureStatus(ex.Message), isBusy: false);
                    AppendLog(YoloEnvironmentCommandPresentationService.BuildRequirementsInstallFailureLog(ex.Message));
                }
            }
            finally
            {
                EndYoloEnvironmentCommand();
            }
        }

        private void ExecuteInstallUltralyticsPackageCommand()
        {
            _ = ExecuteUltralyticsPackageCommandAsync(uninstall: false);
        }

        private void ExecuteUninstallUltralyticsPackageCommand()
        {
            _ = ExecuteUltralyticsPackageCommandAsync(uninstall: true);
        }

        private async Task ExecuteUltralyticsPackageCommandAsync(bool uninstall)
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            string operationName = YoloEnvironmentCommandPresentationService.BuildUltralyticsOperationName(uninstall);
            PythonModelSettings settings = YoloModelSettingsViewModel.CreateSettingsSnapshot(global?.Data?.ProjectSettings?.PythonModel);
            PythonModelRuntimeInstallPlan plan = PythonModelRuntimeInstallPlanService.BuildPlan(settings);
            bool canRun = uninstall ? plan.CanRunUninstall : plan.CanRunInstall;
            if (!plan.IsVisible || !canRun)
            {
                string status = YoloEnvironmentCommandPresentationService.BuildUltralyticsUnavailableStatus(operationName, plan);
                SetYoloCommandStatus(status, isBusy: false);
                YoloModelSettingsViewModel?.SetRuntimeProfileActionStatus(status);
                AppendLog(YoloEnvironmentCommandPresentationService.BuildUltralyticsSkippedLog(operationName, status));
                return;
            }

            if (!ConfirmUltralyticsPackageOperation(uninstall, plan))
            {
                string canceledText = YoloEnvironmentCommandPresentationService.BuildUltralyticsCanceledStatus(operationName);
                SetYoloCommandStatus(canceledText, isBusy: false);
                YoloModelSettingsViewModel?.SetRuntimeProfileActionStatus(canceledText);
                SetUltralyticsPackageOperationResult(
                    YoloEnvironmentCommandPresentationService.BuildUltralyticsOperationSummary(DateTime.Now, operationName, "\uCDE8\uC18C"),
                    YoloEnvironmentCommandPresentationService.BuildUltralyticsPackageOperationDetail(plan, uninstall, null, canceledText));
                AppendLog(canceledText);
                return;
            }

            if (!BeginYoloEnvironmentCommand(YoloEnvironmentCommandPresentationService.BuildUltralyticsRunningStatus(operationName)))
            {
                return;
            }

            try
            {
                AppendLog(YoloEnvironmentCommandPresentationService.BuildUltralyticsStartLog(operationName, plan));
                PythonPackageInstallResult result = uninstall
                    ? await PythonEnvironmentService.UninstallPackageAsync(settings, "ultralytics").ConfigureAwait(true)
                    : await PythonEnvironmentService.InstallPackageAsync(settings, "ultralytics").ConfigureAwait(true);
                if (isApplicationCloseApproved)
                {
                    return;
                }

                foreach (string line in YoloEnvironmentCommandPresentationService.BuildUltralyticsPackageOperationLogLines(operationName, result))
                {
                    AppendLog(line);
                }
                YoloModelSettingsViewModel?.LoadFrom(settings);
                RefreshYoloStatus();

                string statusText = YoloEnvironmentCommandPresentationService.BuildUltralyticsResultStatus(uninstall, operationName, result);
                SetYoloCommandStatus(statusText, isBusy: false);
                YoloModelSettingsViewModel?.SetRuntimeProfileActionStatus(statusText);
                SetUltralyticsPackageOperationResult(
                    YoloEnvironmentCommandPresentationService.BuildUltralyticsOperationSummary(DateTime.Now, operationName, result.Succeeded ? "\uC131\uACF5" : "\uC2E4\uD328"),
                    YoloEnvironmentCommandPresentationService.BuildUltralyticsPackageOperationDetail(plan, uninstall, result, statusText));
                AppendLog(statusText);
            }
            catch (Exception ex)
            {
                if (!isApplicationCloseApproved)
                {
                    string statusText = YoloEnvironmentCommandPresentationService.BuildUltralyticsFailureStatus(operationName, ex.Message);
                    SetYoloCommandStatus(statusText, isBusy: false);
                    YoloModelSettingsViewModel?.SetRuntimeProfileActionStatus(statusText);
                    SetUltralyticsPackageOperationResult(
                        YoloEnvironmentCommandPresentationService.BuildUltralyticsOperationSummary(DateTime.Now, operationName, "\uC2E4\uD328"),
                        YoloEnvironmentCommandPresentationService.BuildUltralyticsPackageOperationDetail(plan, uninstall, null, statusText));
                    AppendLog(statusText);
                }
            }
            finally
            {
                EndYoloEnvironmentCommand();
            }
        }

        private bool ConfirmUltralyticsPackageOperation(bool uninstall, PythonModelRuntimeInstallPlan plan)
        {
            UltralyticsPackageConfirmationPresentation presentation =
                YoloEnvironmentCommandPresentationService.BuildUltralyticsConfirmation(uninstall, plan);
            WpfMessageDialogResult result = WpfMessageDialog.Confirm(
                this,
                presentation.Title,
                presentation.Detail,
                presentation.PrimaryButtonText,
                presentation.CancelButtonText);
            return result == WpfMessageDialogResult.Yes;
        }

        private void SetUltralyticsPackageOperationResult(string summaryText, string detailText)
        {
            YoloModelSettingsViewModel?.SetRuntimePackageOperationResult(summaryText, detailText);
        }

        private void ExecuteRunYoloSmokeCommand()
        {
            _ = ExecuteRunYoloSmokeCommandAsync();
        }

        private async Task ExecuteRunYoloSmokeCommandAsync()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            if (currentWorkflowMode != WorkflowMode.Inference)
            {
                SetWorkflowMode(WorkflowMode.Inference);
                AppendLog(YoloEnvironmentCommandPresentationService.BuildModelTestModeSwitchLog());
            }

            if (!EnsureModelRuntimeForInference())
            {
                return;
            }

            if (!BeginYoloEnvironmentCommand(YoloEnvironmentCommandPresentationService.BuildModelTestStartingStatus()))
            {
                return;
            }

            try
            {
                await RunInteractiveDetectionAsync(allowSmokeFallback: true).ConfigureAwait(true);
                if (isApplicationCloseApproved)
                {
                    return;
                }
                await RefreshYoloSettingsPanelAsync().ConfigureAwait(true);
                if (isApplicationCloseApproved)
                {
                    return;
                }
                SetYoloCommandStatus(YoloEnvironmentCommandPresentationService.BuildModelTestCompletedStatus(), isBusy: false);
            }
            catch (Exception ex)
            {
                if (!isApplicationCloseApproved)
                {
                    string errorText = YoloEnvironmentCommandPresentationService.BuildModelTestFailureStatus(ex.Message);
                    YoloEnvironmentRecoveryPresentation recovery = YoloEnvironmentCommandPresentationService.BuildModelTestFailureRecovery(errorText);
                    SetYoloCommandStatus(errorText, isBusy: false);
                    SetYoloRecoveryStatus(recovery.Title, recovery.Detail, recovery.Action);
                    AppendLog(errorText);
                }
            }
            finally
            {
                EndYoloEnvironmentCommand();
            }
        }

        private void ExecuteRestartPythonWorkerCommand()
        {
            _ = ExecuteRestartPythonWorkerCommandAsync();
        }

        private async Task ExecuteRestartPythonWorkerCommandAsync()
        {
            if (!BeginYoloEnvironmentCommand(YoloEnvironmentCommandPresentationService.BuildWorkerRestartStartingStatus()))
            {
                return;
            }

            CancellationTokenSource cancellation = new CancellationTokenSource();
            pythonWorkerOperationCts = cancellation;
            CancellationToken cancellationToken = cancellation.Token;
            try
            {
                PythonModelRuntimeState runtimeState = GetPythonModelRuntimeState();
                if (!runtimeState.IsRuntimeInstalled)
                {
                    ShowModelRuntimeUnavailable(runtimeState.NextActionText, runtimeState);
                    return;
                }

                bool connected = await global
                    .RestartPythonModelClientConnectionAsync(
                        YoloRuntimePresentationService.GetWorkerConnectTimeoutMilliseconds(
                            global.Data?.ProjectSettings?.PythonModel?.DetectionTimeoutSeconds ?? 30),
                        cancellationToken)
                    .ConfigureAwait(true);
                if (isApplicationCloseApproved || cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (connected)
                {
                    string requestId = YoloRuntimePresentationService.CreateRequestId();
                    global.ModelRuntime.DeepLearning.SendHealthCheck(requestId);
                    global.ModelRuntime.DeepLearning.SendModelStatus(requestId, ensureLoaded: false);
                }

                await RefreshYoloSettingsPanelAsync().ConfigureAwait(true);
                if (isApplicationCloseApproved)
                {
                    return;
                }
                ApplyYoloWorkerCommandPresentation(
                    YoloEnvironmentCommandPresentationService.BuildWorkerRestartResult(
                        connected,
                        YoloRuntimePresentationService.BuildPythonWorkerFailureText(
                            global.GetPythonCommunicationStatusSnapshot(),
                            global.ModelRuntime.PythonClientProcess?.LastError)));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                if (!isApplicationCloseApproved)
                {
                    ApplyYoloWorkerCommandPresentation(
                        YoloEnvironmentCommandPresentationService.BuildWorkerRestartFailure(ex.Message));
                }
            }
            finally
            {
                if (ReferenceEquals(pythonWorkerOperationCts, cancellation))
                {
                    pythonWorkerOperationCts = null;
                }

                cancellation.Dispose();
                EndYoloEnvironmentCommand();
            }
        }

        private void ExecuteStopPythonWorkerCommand()
        {
            _ = ExecuteStopPythonWorkerCommandAsync();
        }

        private async Task ExecuteStopPythonWorkerCommandAsync()
        {
            if (!BeginYoloEnvironmentCommand(YoloEnvironmentCommandPresentationService.BuildWorkerStopStartingStatus()))
            {
                return;
            }

            CancellationTokenSource cancellation = new CancellationTokenSource();
            pythonWorkerOperationCts = cancellation;
            CancellationToken cancellationToken = cancellation.Token;
            try
            {
                await global.StopPythonModelClientConnectionAsync(cancellationToken).ConfigureAwait(true);
                if (isApplicationCloseApproved || cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                await RefreshYoloSettingsPanelAsync().ConfigureAwait(true);
                if (isApplicationCloseApproved)
                {
                    return;
                }
                ApplyYoloWorkerCommandPresentation(
                    YoloEnvironmentCommandPresentationService.BuildWorkerStopCompleted());
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                if (!isApplicationCloseApproved)
                {
                    ApplyYoloWorkerCommandPresentation(
                        YoloEnvironmentCommandPresentationService.BuildWorkerStopFailure(ex.Message));
                }
            }
            finally
            {
                if (ReferenceEquals(pythonWorkerOperationCts, cancellation))
                {
                    pythonWorkerOperationCts = null;
                }

                cancellation.Dispose();
                EndYoloEnvironmentCommand();
            }
        }

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

        private bool BeginYoloEnvironmentCommand(string statusText)
        {
            if (isApplicationCloseApproved
                || WorkflowCommandStateService.HasActiveCommand(
                    isYoloEnvironmentCommandRunning,
                    isDetecting,
                    isBatchDetectionRunning,
                    isTrainingCommandRunning))
            {
                if (isApplicationCloseApproved)
                {
                    return false;
                }

                AppendLog(YoloEnvironmentCommandPresentationService.BuildBusyCommandLog());
                return false;
            }

            isYoloEnvironmentCommandRunning = true;
            ClearYoloRecoveryStatus();
            SetYoloCommandStatus(statusText, isBusy: true);
            UpdateYoloCommandButtons();
            return true;
        }

        private void EndYoloEnvironmentCommand()
        {
            isYoloEnvironmentCommandRunning = false;
            if (isApplicationCloseApproved)
            {
                return;
            }

            YoloStatusViewModel.SetCommandBusy(false);

            UpdateYoloCommandButtons();
            RefreshYoloStatus();
        }

        private void SetYoloCommandStatus(string text, bool isBusy)
        {
            YoloStatusViewModel.SetCommandStatus(text, isBusy);
        }

        private void SetYoloRecoveryStatus(string titleText, string detailText, string actionText)
        {
            ShellViewModel?.SetModelCenterRecoveryState(titleText, detailText, actionText);
            YoloStatusViewModel?.SetRecoveryState(titleText, detailText, actionText);
        }

        private void ClearYoloRecoveryStatus()
        {
            ShellViewModel?.ClearModelCenterRecoveryState();
            YoloStatusViewModel?.ClearRecoveryState();
        }
        #endregion

    }
}
