using MvcVisionSystem.Yolo;
using MvcVisionSystem._1._Core;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
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
                    ExecuteConnectUltralyticsRuntime(normalizedEngine);
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
                CreateYoloModelSettingsSnapshot(),
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
            if (!TryPickFolder("YOLOv8 \uD3F4\uB354 \uC5F0\uACB0", initialPath, out string selectedPath))
            {
                YoloProjectRootBox?.Focus();
                SetYoloCommandStatus("YOLOv8 \uD3F4\uB354 \uC5F0\uACB0\uC744 \uCDE8\uC18C\uD588\uC2B5\uB2C8\uB2E4. \uAE30\uC874 \uACBD\uB85C\uB97C \uD655\uC778\uD558\uAC70\uB098 \uB2E4\uC2DC \uC5F0\uACB0\uD558\uC138\uC694.", isBusy: false);
                AppendLog("YOLOv8 local worker \uD3F4\uB354 \uC5F0\uACB0 \uCDE8\uC18C.");
                return;
            }

            PythonModelRuntimeConnectionResult result = PythonModelRuntimeConnectionService.BuildYoloV8FolderConnection(
                CreateYoloModelSettingsSnapshot(),
                selectedPath,
                global?.Data?.ProjectSettings?.DatasetPurpose ?? LabelingDatasetPurpose.ObjectDetection);
            YoloModelSettingsViewModel?.ApplyRuntimeConnectionResult(result);
            YoloProjectRootBox?.Focus();
            SetYoloCommandStatus($"{result.SummaryText}: {result.DetailText}", isBusy: false);
            AppendLog($"YOLOv8 \uD3F4\uB354 \uC5F0\uACB0: {selectedPath} / {result.SummaryText}");
        }

        private void ExecuteConnectYoloV5RuntimeFolder()
        {
            string initialPath = YoloModelSettingsViewModel?.ProjectRootPath ?? YoloProjectRootBox.Text;
            if (!TryPickFolder("YOLOv5 \uD3F4\uB354 \uC5F0\uACB0", initialPath, out string selectedPath))
            {
                YoloProjectRootBox?.Focus();
                SetYoloCommandStatus("YOLOv5 \uD3F4\uB354 \uC5F0\uACB0\uC744 \uCDE8\uC18C\uD588\uC2B5\uB2C8\uB2E4. \uAE30\uC874 \uACBD\uB85C\uB97C \uD655\uC778\uD558\uAC70\uB098 \uB2E4\uC2DC \uC5F0\uACB0\uD558\uC138\uC694.", isBusy: false);
                AppendLog("YOLOv5 \uC2E4\uD589\uAE30 \uD3F4\uB354 \uC5F0\uACB0 \uCDE8\uC18C.");
                return;
            }

            PythonModelRuntimeConnectionResult result = PythonModelRuntimeConnectionService.BuildYoloV5FolderConnection(
                CreateYoloModelSettingsSnapshot(),
                selectedPath);
            YoloModelSettingsViewModel?.ApplyRuntimeConnectionResult(result);
            YoloProjectRootBox?.Focus();
            SetYoloCommandStatus($"{result.SummaryText}: {result.DetailText}", isBusy: false);
            AppendLog($"YOLOv5 \uD3F4\uB354 \uC5F0\uACB0: {selectedPath} / {result.SummaryText}");
        }

        private PythonModelSettings CreateYoloModelSettingsSnapshot()
        {
            PythonModelSettings current = global?.Data?.ProjectSettings?.PythonModel ?? new PythonModelSettings();
            return new PythonModelSettings
            {
                PythonExecutablePath = YoloModelSettingsViewModel?.PythonExecutablePath ?? current.PythonExecutablePath,
                ModelEngine = YoloModelSettingsViewModel?.SelectedModelEngine ?? current.ModelEngine,
                ProjectRootPath = YoloModelSettingsViewModel?.ProjectRootPath ?? current.ProjectRootPath,
                ClientScriptPath = YoloModelSettingsViewModel?.ClientScriptPath ?? current.ClientScriptPath,
                WeightsPath = YoloModelSettingsViewModel?.WeightsPath ?? current.WeightsPath,
                ImageRootPath = YoloModelSettingsViewModel?.ImageRootPath ?? current.ImageRootPath,
                MinimumDetectionConfidence = current.MinimumDetectionConfidence,
                MaximumDetectionCandidates = current.MaximumDetectionCandidates,
                InferenceImageSize = current.InferenceImageSize,
                DetectionTimeoutSeconds = current.DetectionTimeoutSeconds,
                AutoStartClient = current.AutoStartClient
            };
        }

        private async void ExecuteSaveYoloSettingsCommand()
        {
            try
            {
                SaveYoloEditorFields();
                SaveTrainingEditorFields();
                bool pendingWeightsRecipeSave = hasPendingTrainingWeightsRecipeSave;
                if (pendingWeightsRecipeSave && CandidateReviewViewModel?.IsModelPromotionHeld == true)
                {
                    string status = WpfModelCandidateDecisionPresentationService.BuildHeldCandidateSaveBlockedStatus();
                    SetYoloCommandStatus(status, isBusy: false);
                    AppendLog(status);
                    return;
                }

                if (pendingWeightsRecipeSave)
                {
                    UpdateAppliedTrainingWeightsHistory(global.Data.ProjectSettings.PythonModel.WeightsPath, savedToRecipe: true);
                }

                bool configSaved = SaveProjectConfigFromPanel();
                if (!configSaved && pendingWeightsRecipeSave)
                {
                    UpdateAppliedTrainingWeightsHistory(global.Data.ProjectSettings.PythonModel.WeightsPath, savedToRecipe: false);
                }

                PopulateYoloEditorFields();
                RefreshYoloStatus();
                await RefreshYoloSettingsPanelAsync().ConfigureAwait(true);
                AppendLog(configSaved
                    ? "\uBAA8\uB378 \uD504\uB85C\uD544 \uC124\uC815 \uC800\uC7A5 \uC644\uB8CC."
                    : "\uBAA8\uB378 \uD504\uB85C\uD544 \uC124\uC815 \uBC18\uC601 \uC644\uB8CC. Recipe \uC801\uC6A9 \uD6C4 \uC124\uC815 \uC800\uC7A5\uC774 \uD544\uC694\uD569\uB2C8\uB2E4.");
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
                SetYoloCommandStatus($"설정 저장 실패: {ex.Message}", isBusy: false);
                AppendLog($"\uBAA8\uB378 \uD504\uB85C\uD544 \uC124\uC815 \uC800\uC7A5 \uC2E4\uD328: {ex.Message}");
            }
        }

        private async void ExecuteResetYoloSettingsCommand()
        {
            EnsureProjectSettings();
            global.Data.ProjectSettings.PythonModel = new PythonModelSettings();
            global.Data.ProjectSettings.PythonModel.EnsureDefaults();
            pendingTrainingBaselineWeightsPath = string.Empty;
            hasPendingTrainingWeightsRecipeSave = false;
            PopulateYoloEditorFields();
            RefreshYoloStatus();
            RefreshModelCenterDashboard();
            await RefreshYoloSettingsPanelAsync().ConfigureAwait(true);
            AppendLog("YOLO 모델 설정을 기본값으로 되돌렸습니다.");
        }
    }
}
