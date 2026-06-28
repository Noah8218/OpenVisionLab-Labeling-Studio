using MvcVisionSystem.Yolo;
using MvcVisionSystem._1._Core;
using System;
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
                "\uCD94\uB860 \uC2E4\uD589 \uC2A4\uD06C\uB9BD\uD2B8 \uC120\uD0DD",
                "\uC2E4\uD589 \uC2A4\uD06C\uB9BD\uD2B8 (*.py)|*.py|All files (*.*)|*.*",
                YoloModelSettingsViewModel?.ClientScriptPath ?? YoloClientScriptBox.Text,
                out string selectedPath))
            {
                if (YoloModelSettingsViewModel != null)
                {
                    YoloModelSettingsViewModel.ClientScriptPath = selectedPath;
                }

                NotifyYoloPathSelected("\uCD94\uB860 \uC2E4\uD589 \uC2A4\uD06C\uB9BD\uD2B8", selectedPath);
            }
        }

        private void ExecuteBrowseYoloWeightsCommand()
        {
            if (TryPickFile(
                "\uBAA8\uB378 \uD30C\uC77C \uC120\uD0DD",
                "\uBAA8\uB378 \uD30C\uC77C (*.pt;*.pth)|*.pt;*.pth|All files (*.*)|*.*",
                YoloModelSettingsViewModel?.WeightsPath ?? YoloWeightsPathBox.Text,
                out string selectedPath))
            {
                if (YoloModelSettingsViewModel != null)
                {
                    YoloModelSettingsViewModel.WeightsPath = selectedPath;
                }

                NotifyYoloPathSelected("\uBAA8\uB378 \uD30C\uC77C", selectedPath);
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
        private async void ExecuteSaveYoloSettingsCommand()
        {
            try
            {
                SaveYoloEditorFields();
                SaveTrainingEditorFields();
                bool pendingWeightsRecipeSave = hasPendingTrainingWeightsRecipeSave;
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
                    ? "YOLO 모델 설정 저장 완료."
                    : "YOLO 모델 설정 반영 완료. Recipe 적용 후 설정 저장이 필요합니다.");
                if (configSaved && pendingWeightsRecipeSave)
                {
                    hasPendingTrainingWeightsRecipeSave = false;
                    UpdateYoloTrainingHistoryText();
                    SetYoloCommandStatus("학습 weight가 recipe 설정에 저장되었습니다.", isBusy: false);
                    SetProjectConfigStatus("\uD559\uC2B5 \uACB0\uACFC \uBAA8\uB378 \uC801\uC6A9 \uBC0F \uC124\uC815 \uC800\uC7A5 \uC644\uB8CC.");
                }
            }
            catch (Exception ex)
            {
                SetYoloCommandStatus($"설정 저장 실패: {ex.Message}", isBusy: false);
                AppendLog($"YOLO 설정 저장 실패: {ex.Message}");
            }
        }

        private async void ExecuteResetYoloSettingsCommand()
        {
            EnsureProjectSettings();
            global.Data.ProjectSettings.PythonModel = new PythonModelSettings();
            global.Data.ProjectSettings.PythonModel.EnsureDefaults();
            PopulateYoloEditorFields();
            RefreshYoloStatus();
            await RefreshYoloSettingsPanelAsync().ConfigureAwait(true);
            AppendLog("YOLO 모델 설정을 기본값으로 되돌렸습니다.");
        }
    }
}
