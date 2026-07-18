using Lib.Common;
using MvcVisionSystem.Yolo;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        private bool isExternalYoloDatasetIntakeRunning;

        // Dialogs and recipe persistence belong to the view adapter; validation remains side-effect-free in YoloExternalDatasetIntakeService.
        private async void ExecuteSelectExternalYoloDatasetCommand()
        {
            if (isExternalYoloDatasetIntakeRunning)
            {
                return;
            }

            ExternalYoloDatasetSettings settings = GetExternalYoloDatasetSettings();
            if (settings == null)
            {
                return;
            }

            if (!TryPickFile(
                    "외부 YOLO data.yaml 선택",
                    "YOLO data.yaml (*.yaml;*.yml)|*.yaml;*.yml|All files (*.*)|*.*",
                    settings.DataYamlFilePath,
                    out string selectedPath))
            {
                LearningWorkflowViewModel?.SetExternalYoloDatasetIntakeResult(
                    settings.DatasetPurpose,
                    "외부 YOLO data.yaml: 선택 취소",
                    "파일을 선택하면 읽기 전용 검증 후 다음 학습에 사용할지 별도로 결정합니다.",
                    settings.DataYamlFilePath);
                return;
            }

            await ValidateAndStoreExternalYoloDatasetAsync(
                selectedPath,
                LearningWorkflowViewModel?.GetSelectedExternalYoloDatasetPurpose() ?? LabelingDatasetPurpose.ObjectDetection,
                useForNextTraining: false);
        }

        private async void ExecuteActivateExternalYoloDatasetCommand()
        {
            if (isExternalYoloDatasetIntakeRunning)
            {
                return;
            }

            ExternalYoloDatasetSettings settings = GetExternalYoloDatasetSettings();
            if (settings == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(settings.DataYamlFilePath))
            {
                LearningWorkflowViewModel?.SetExternalYoloDatasetIntakeResult(
                    settings.DatasetPurpose,
                    "외부 YOLO data.yaml: 선택 필요",
                    "먼저 data.yaml을 선택해 검증하세요.",
                    string.Empty);
                return;
            }

            await ValidateAndStoreExternalYoloDatasetAsync(
                settings.DataYamlFilePath,
                LearningWorkflowViewModel?.GetSelectedExternalYoloDatasetPurpose() ?? settings.DatasetPurpose,
                useForNextTraining: true);
        }

        private void ExecuteClearExternalYoloDatasetCommand()
        {
            if (isExternalYoloDatasetIntakeRunning)
            {
                return;
            }

            ExternalYoloDatasetSettings settings = GetExternalYoloDatasetSettings();
            if (settings == null)
            {
                return;
            }

            settings.Clear();
            TrySaveExternalYoloDatasetSettings();
            RefreshExternalYoloDatasetIntakePresentation();
            PopulateTrainingEditorFields();
            RefreshTrainingReadinessPanel(refreshYaml: false);
            AppendLog("외부 YOLO data.yaml 선택과 다음 학습 사용을 해제했습니다.");
        }

        private async Task ValidateAndStoreExternalYoloDatasetAsync(
            string dataYamlFilePath,
            LabelingDatasetPurpose purpose,
            bool useForNextTraining)
        {
            isExternalYoloDatasetIntakeRunning = true;
            LearningWorkflowViewModel?.SetExternalYoloDatasetIntakeResult(
                purpose,
                "외부 YOLO data.yaml: 확인 중",
                "원본 이미지와 라벨은 수정하지 않고 경로, 분할, 클래스, 라벨 형식만 확인합니다.",
                dataYamlFilePath);

            YoloExternalDatasetIntakeReport report;
            try
            {
                report = await Task.Run(() => YoloExternalDatasetIntakeService.Build(dataYamlFilePath, purpose));
            }
            catch (Exception ex)
            {
                LearningWorkflowViewModel?.SetExternalYoloDatasetIntakeResult(
                    purpose,
                    "외부 YOLO data.yaml: 확인 불가",
                    ex.Message,
                    dataYamlFilePath);
                return;
            }
            finally
            {
                isExternalYoloDatasetIntakeRunning = false;
            }

            ExternalYoloDatasetSettings settings = GetExternalYoloDatasetSettings();
            if (settings == null)
            {
                return;
            }

            settings.DataYamlFilePath = string.IsNullOrWhiteSpace(report.DataYamlFilePath)
                ? dataYamlFilePath ?? string.Empty
                : report.DataYamlFilePath;
            settings.DatasetPurpose = purpose;
            settings.UseForTraining = useForNextTraining && report.IsReady;
            YoloExternalDatasetIntakeService.ApplyValidation(settings, report, acceptSourceIdentity: useForNextTraining);
            TrySaveExternalYoloDatasetSettings();
            PopulateTrainingEditorFields();
            if (settings.UseForTraining)
            {
                RefreshExternalTrainingReadinessPanel(report);
            }
            else
            {
                RefreshExternalYoloDatasetIntakePresentation();
                RefreshTrainingReadinessPanel(refreshYaml: false);
            }

            if (report.IsReady)
            {
                AppendLog($"외부 YOLO data.yaml 검증 완료: {Path.GetFileName(report.DataYamlFilePath)} / {report.Summary} / 다음 학습 사용:{settings.UseForTraining}");
            }
            else
            {
                AppendLog($"외부 YOLO data.yaml 검증 실패: {string.Join(" ", report.Errors.Take(2))}");
            }
        }

        private ExternalYoloDatasetSettings GetExternalYoloDatasetSettings()
        {
            if (global?.Data == null)
            {
                return null;
            }

            EnsureProjectSettings();
            return global.Data.ProjectSettings.ExternalYoloDataset;
        }

        private void RefreshExternalYoloDatasetIntakePresentation()
        {
            ExternalYoloDatasetSettings settings = GetExternalYoloDatasetSettings();
            if (settings == null)
            {
                return;
            }

            string statusText;
            string detailText;
            if (!settings.HasSelection)
            {
                statusText = "외부 YOLO data.yaml: 선택 안 함";
                detailText = "선택 후 검증해도 원본 이미지와 라벨은 수정하지 않습니다.";
            }
            else if (settings.RequiresExplicitReactivation)
            {
                statusText = "외부 YOLO data.yaml: 재활성화 필요";
                detailText = string.IsNullOrWhiteSpace(settings.LastValidationSummary)
                    ? "원본이 변경되었을 수 있습니다. data.yaml을 다시 검증한 뒤 '다음 학습 사용'으로 명시적으로 활성화하세요."
                    : settings.LastValidationSummary;
            }
            else if (settings.UseForTraining && settings.LastValidationSucceeded)
            {
                statusText = "외부 YOLO data.yaml: 다음 학습에 사용";
                detailText = settings.LastValidationSummary + " 내부 레시피 데이터는 바꾸지 않으며 자동 학습이나 모델 채택은 하지 않습니다.";
            }
            else if (settings.LastValidationSucceeded)
            {
                statusText = "외부 YOLO data.yaml: 검증됨";
                detailText = settings.LastValidationSummary + " '다음 학습 사용'을 눌러야 학습 소스가 바뀝니다.";
            }
            else
            {
                statusText = settings.UseForTraining
                    ? "외부 YOLO data.yaml: 재검증 필요"
                    : "외부 YOLO data.yaml: 확인 필요";
                detailText = string.IsNullOrWhiteSpace(settings.LastValidationSummary)
                    ? "data.yaml을 다시 선택해 경로와 라벨 형식을 확인하세요."
                    : settings.LastValidationSummary;
            }

            LearningWorkflowViewModel?.SetExternalYoloDatasetIntakeResult(
                settings.DatasetPurpose,
                statusText,
                detailText,
                settings.DataYamlFilePath);
        }

        private bool TrySaveExternalYoloDatasetSettings()
        {
            string recipeName = GetCurrentRecipeName();
            if (global?.Data == null || string.IsNullOrWhiteSpace(recipeName))
            {
                return false;
            }

            try
            {
                CRecipe.InitDirectory(recipeName);
                global.Data.SaveConfig(recipeName);
                return true;
            }
            catch (Exception ex)
            {
                AppendLog($"외부 YOLO data.yaml 설정 저장 실패: {ex.Message}");
                return false;
            }
        }
    }
}
