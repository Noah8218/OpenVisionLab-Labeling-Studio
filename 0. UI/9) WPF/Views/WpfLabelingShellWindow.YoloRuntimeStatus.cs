using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using System.IO;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        private void NotifyYoloPathSelected(string label, string selectedPath)
        {
            SetYoloCommandStatus($"{label} 선택됨. 저장을 눌러 설정에 반영하세요.", isBusy: false);
            AppendLog($"{label} 선택: {selectedPath}");
        }

        private void RefreshYoloStatus()
        {
            global.Data.ProjectSettings ??= new LabelingProjectSettings();
            global.Data.ProjectSettings.PythonModel ??= new PythonModelSettings();
            PythonModelSettings settings = global.Data.ProjectSettings.PythonModel;
            RefreshModelCenterDashboard();
            PythonModelRuntimeState runtimeState = GetPythonModelRuntimeState();
            if (runtimeState.State == PythonModelRuntimeStateKind.NotInstalled)
            {
                WpfModelRuntimeUnavailablePresentation presentation =
                    WpfModelRuntimeUnavailablePresentationService.Build(runtimeState);
                SetGlobalInferenceStatus(string.Empty, isBusy: false, isWarning: true);
                SetPythonStatus(runtimeState.SummaryText);
                SetInspectionModelStatus(presentation.InspectionStatusText, presentation.InspectionStatusToolTip);
                SetModelStatus(presentation.ModelStatusText);
                SetYoloCommandStatus(presentation.CommandStatusText, isBusy: false);
                ApplyModelRuntimeUnavailablePresentation(presentation);
                return;
            }

            PythonModelValidationResult result = PythonModelSettingsValidator.Validate(settings, requireWeights: true);
            SetGlobalInferenceStatus(string.Empty, isBusy: false, isWarning: !result.IsValid);
            SetPythonStatus(WpfInferenceStatusPresentationService.BuildRuntimePythonStatus(result, runtimeState));

            string weightsPath = settings.WeightsPath;
            if (!File.Exists(weightsPath))
            {
                string inspectionModelStatus = WpfInferenceStatusPresentationService.BuildInspectionModelStatusText(settings, hasPendingTrainingWeightsRecipeSave);
                SetInspectionModelStatus(
                    inspectionModelStatus,
                    WpfInferenceStatusPresentationService.BuildInspectionModelToolTip(settings, hasPendingTrainingWeightsRecipeSave));
                SetModelStatus(inspectionModelStatus);
                return;
            }

            SetInspectionModelStatus(
                WpfInferenceStatusPresentationService.BuildInspectionModelStatusText(settings, hasPendingTrainingWeightsRecipeSave),
                WpfInferenceStatusPresentationService.BuildInspectionModelToolTip(settings, hasPendingTrainingWeightsRecipeSave));
            SetModelStatus(WpfInferenceStatusPresentationService.BuildInspectionModelStatusText(settings, hasPendingTrainingWeightsRecipeSave));
        }

        private PythonModelRuntimeState GetPythonModelRuntimeState()
        {
            global.Data.ProjectSettings ??= new LabelingProjectSettings();
            global.Data.ProjectSettings.PythonModel ??= new PythonModelSettings();
            PythonCommunicationStatus status = global.GetPythonCommunicationStatusSnapshot();
            return PythonModelSettingsValidator.GetRuntimeState(
                global.Data.ProjectSettings.PythonModel,
                status?.WorkerSupportedModels,
                status?.WorkerTrainingModels,
                status?.WorkerDetectionModels);
        }

        private bool EnsureModelRuntimeForTraining()
        {
            PythonModelRuntimeState runtimeState = GetPythonModelRuntimeState();
            if (runtimeState.CanRunTraining)
            {
                return true;
            }

            ShowModelRuntimeUnavailable(
                "\uD559\uC2B5 \uC2DC\uC791 \uB300\uAE30: \uBAA8\uB378 \uC2E4\uD589\uAE30 \uC124\uCE58 \uB610\uB294 \uACBD\uB85C \uC5F0\uACB0 \uD544\uC694",
                runtimeState);
            return false;
        }

        private bool EnsureModelRuntimeForInference()
        {
            PythonModelRuntimeState runtimeState = GetPythonModelRuntimeState();
            if (runtimeState.CanRunInference)
            {
                return true;
            }

            ShowModelRuntimeUnavailable(
                "\uD604\uC7AC \uAC80\uC0AC \uB300\uAE30: \uAC80\uC0AC \uBAA8\uB378 \uD30C\uC77C \uD544\uC694",
                runtimeState);
            return false;
        }

        private void ShowModelRuntimeUnavailable(string statusText, PythonModelRuntimeState runtimeState)
        {
            runtimeState ??= GetPythonModelRuntimeState();
            WpfModelRuntimeUnavailablePresentation presentation =
                WpfModelRuntimeUnavailablePresentationService.Build(runtimeState, statusText);

            SetYoloCommandStatus(presentation.CommandStatusText, isBusy: false);
            SetYoloRecoveryStatus(presentation.RecoveryTitle, presentation.RecoveryDetail, presentation.RecoveryAction);
            SetTrainingReadinessStatus(presentation.ReadinessText);
            SetPythonStatus(runtimeState.SummaryText);
            ApplyModelRuntimeUnavailablePresentation(presentation);
            AppendLog(presentation.LogText);
        }

        private void ApplyModelRuntimeUnavailablePresentation(PythonModelRuntimeState runtimeState, string statusText = null)
        {
            runtimeState ??= GetPythonModelRuntimeState();
            WpfModelRuntimeUnavailablePresentation presentation =
                WpfModelRuntimeUnavailablePresentationService.Build(runtimeState, statusText);
            ApplyModelRuntimeUnavailablePresentation(presentation);
        }

        private void ApplyModelRuntimeUnavailablePresentation(WpfModelRuntimeUnavailablePresentation presentation)
        {
            if (presentation == null)
            {
                return;
            }

            SetTrainingReadinessStatus(presentation.ReadinessText);
            SetTrainingProgressStatus(WpfTrainingProgressPresentationService.BuildIdleProgressText(), string.Empty, 0D, isIndeterminate: false);
            LearningWorkflowViewModel?.SetTrainingModelLifecycleState(
                presentation.CurrentModelText,
                presentation.CandidateModelText,
                presentation.AdoptionText,
                presentation.NextActionText);
            ShellViewModel?.SetModelCenterModelState(
                presentation.CurrentModelText,
                presentation.CandidateModelText,
                presentation.AdoptionText,
                presentation.NextActionText,
                presentation.NoCandidateText,
                presentation.CandidateReviewDetailText,
                canConfirmModel: false,
                presentation.DecisionTitleText,
                presentation.DecisionEvidenceText,
                presentation.NextActionText);
            ShellViewModel?.SetModelCenterCandidateReviewState(
                presentation.NoCandidateText,
                presentation.CandidateReviewDetailText,
                canReviewCandidate: false);
            ShellViewModel?.SetModelRegistryState(new WpfModelRegistryPresentation
            {
                ProfileText = presentation.ProfileText,
                TrainingRunText = presentation.TrainingRunText,
                CandidateModelText = presentation.CandidateModelText,
                InspectionModelText = presentation.CurrentModelText,
                ActionText = presentation.NextActionText,
                SummaryPrimaryText = presentation.SummaryPrimaryText,
                SummarySecondaryText = presentation.SummarySecondaryText,
                HistoryItems = new WpfModelRegistryHistoryItem[0]
            });
            ShellViewModel?.SetModelCenterRecoveryState(
                presentation.RecoveryTitle,
                presentation.RecoveryDetail,
                presentation.NextActionText);
            TrainingSettingsViewModel?.SetPostTrainingModelActionState(
                presentation.CurrentModelText,
                presentation.CandidateModelText,
                presentation.AdoptionText,
                presentation.NextActionText,
                presentation.NoCandidateText,
                presentation.CandidateReviewDetailText,
                canReview: false,
                presentation.NoCandidateText,
                presentation.CandidateReviewDetailText,
                canConfirm: false);
        }

        private void SaveYoloEditorFields()
        {
            EnsureProjectSettings();
            PythonModelSettings settings = global.Data.ProjectSettings.PythonModel;
            YoloModelSettingsViewModel?.ApplyTo(settings);
            YoloModelSettingsViewModel?.ApplyTo(global.Data.ProjectSettings.AnomalyClassification);
            YoloModelSettingsViewModel?.LoadFrom(settings, global.Data.ProjectSettings.AnomalyClassification);
            CandidateConfidenceSlider.Value = System.Math.Clamp(settings.MinimumDetectionConfidence, 0F, 1F);
        }

        private void SaveTrainingEditorFields()
        {
            EnsureProjectSettings();
            TrainingSettings training = global.Data.ProjectSettings.Training;
            TrainingSettingsViewModel?.ApplyTo(training, global.Data.ProjectSettings.YoloDataset, global.Data.TrainingParam);
            PopulateTrainingEditorFields();
        }
    }
}
