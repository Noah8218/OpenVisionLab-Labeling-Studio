using MvcVisionSystem.Yolo;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    // Responsibility group: external YOLO dataset intake.
    // These members remain WPF Window adapters; independent policy belongs in services.
    public partial class WpfLabelingShellWindow
    {
        #region ExternalYoloDatasetIntake
        // Dialogs remain in the view adapter; Recipe persistence uses the existing
        // project session owner and validation remains side-effect-free in
        // YoloExternalDatasetIntakeService.
        private void ExecuteSelectExternalYoloDatasetCommand()
        {
            _ = ExecuteSelectExternalYoloDatasetCommandAsync();
        }

        private async Task ExecuteSelectExternalYoloDatasetCommandAsync()
        {
            if (isApplicationCloseApproved || externalYoloDatasetIntakeWorkflowService.IsRunning)
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

            if (isApplicationCloseApproved)
            {
                return;
            }

            await ValidateAndStoreExternalYoloDatasetAsync(
                selectedPath,
                LearningWorkflowViewModel?.GetSelectedExternalYoloDatasetPurpose() ?? LabelingDatasetPurpose.ObjectDetection,
                useForNextTraining: false);
        }

        private void ExecuteActivateExternalYoloDatasetCommand()
        {
            _ = ExecuteActivateExternalYoloDatasetCommandAsync();
        }

        private async Task ExecuteActivateExternalYoloDatasetCommandAsync()
        {
            if (isApplicationCloseApproved || externalYoloDatasetIntakeWorkflowService.IsRunning)
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
            if (isApplicationCloseApproved || externalYoloDatasetIntakeWorkflowService.IsRunning)
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
            if (isApplicationCloseApproved || externalYoloDatasetIntakeWorkflowService.IsRunning)
            {
                return;
            }

            LearningWorkflowViewModel?.SetExternalYoloDatasetIntakeResult(
                purpose,
                "외부 YOLO data.yaml: 확인 중",
                "원본 이미지와 라벨은 수정하지 않고 경로, 분할, 클래스, 라벨 형식만 확인합니다.",
                dataYamlFilePath);

            ExternalYoloDatasetIntakeWorkflowResult workflowResult =
                await externalYoloDatasetIntakeWorkflowService.RunAsync(
                    new ExternalYoloDatasetIntakeWorkflowRequest
                    {
                        DataYamlFilePath = dataYamlFilePath,
                        Purpose = purpose
                    });
            if (!workflowResult.Started || workflowResult.IsCanceled)
            {
                return;
            }

            if (workflowResult.Error != null)
            {
                if (!isApplicationCloseApproved && !externalYoloDatasetIntakeWorkflowService.IsClosed)
                {
                    LearningWorkflowViewModel?.SetExternalYoloDatasetIntakeResult(
                        purpose,
                        "외부 YOLO data.yaml: 확인 불가",
                        workflowResult.Error.Message,
                        dataYamlFilePath);
                }

                return;
            }

            if (isApplicationCloseApproved || externalYoloDatasetIntakeWorkflowService.IsClosed)
            {
                return;
            }

            YoloExternalDatasetIntakeReport report = workflowResult.Report;

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

            (string statusText, string detailText) = TrainingReadinessPresentationService.BuildExternalDatasetPresentation(settings);

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
                RecipeConfigurationSaveResult saveResult = projectRecipeSessionService.SaveConfiguration(
                    global.Data,
                    recipeName,
                    updateYoloDataYaml: false,
                    refreshDatasetVersion: true);
                if (!saveResult.IsSuccess)
                {
                    throw new IOException(saveResult.ErrorMessage);
                }

                return true;
            }
            catch (Exception ex)
            {
                AppendLog($"외부 YOLO data.yaml 설정 저장 실패: {ex.Message}");
                return false;
            }
        }
        #endregion

    }
}
