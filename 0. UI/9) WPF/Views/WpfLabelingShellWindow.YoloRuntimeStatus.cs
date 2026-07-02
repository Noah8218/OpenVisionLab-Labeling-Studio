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
                SetGlobalInferenceStatus(string.Empty, isBusy: false, isWarning: true);
                SetPythonStatus(runtimeState.SummaryText);
                SetInspectionModelStatus(
                    "\uBAA8\uB378 \uAE30\uB2A5: \uBBF8\uC124\uCE58",
                    runtimeState.DetailText);
                SetModelStatus("\uBAA8\uB378: \uBBF8\uC124\uCE58");
                SetYoloCommandStatus(runtimeState.DetailText, isBusy: false);
                ApplyModelRuntimeUnavailablePresentation(runtimeState);
                return;
            }

            PythonModelValidationResult result = PythonModelSettingsValidator.Validate(settings, requireWeights: true);
            SetGlobalInferenceStatus(string.Empty, isBusy: false, isWarning: !result.IsValid);
            SetPythonStatus(result.IsValid ? "추론: 준비 완료" : runtimeState.SummaryText);

            string weightsPath = settings.WeightsPath;
            if (!File.Exists(weightsPath))
            {
                SetInspectionModelStatus(
                    "\uAC80\uC0AC \uBAA8\uB378: \uC5C6\uC74C",
                    "\uAC80\uC0AC\uC5D0 \uC0AC\uC6A9\uD560 \uBAA8\uB378 \uD30C\uC77C\uC744 YOLO \uC124\uC815\uC5D0\uC11C \uC120\uD0DD\uD558\uC138\uC694.");
                SetModelStatus("검사 모델: 없음");
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
            string title = runtimeState.State == PythonModelRuntimeStateKind.NotInstalled
                ? "\uBAA8\uB378 \uC2E4\uD589\uAE30 \uBBF8\uC124\uCE58"
                : "\uBAA8\uB378 \uC124\uC815 \uD655\uC778 \uD544\uC694";
            string action = runtimeState.State == PythonModelRuntimeStateKind.NotInstalled
                ? "\uB2E4\uC74C: \uBAA8\uB378 \uC2E4\uD589\uAE30\uB97C \uC124\uCE58\uD558\uAC70\uB098 \uAE30\uC874 \uBAA8\uB378 \uACBD\uB85C\uB97C \uC5F0\uACB0\uD55C \uB4A4 \uB2E4\uC2DC \uC2E4\uD589\uD558\uC138\uC694."
                : runtimeState.NextActionText;

            SetYoloCommandStatus(statusText, isBusy: false);
            SetYoloRecoveryStatus(title, runtimeState.DetailText, action);
            SetTrainingReadinessStatus(statusText);
            SetPythonStatus(runtimeState.SummaryText);
            ApplyModelRuntimeUnavailablePresentation(runtimeState, statusText);
            AppendLog($"{statusText} / {runtimeState.DetailText}");
        }

        private void ApplyModelRuntimeUnavailablePresentation(PythonModelRuntimeState runtimeState, string statusText = null)
        {
            runtimeState ??= GetPythonModelRuntimeState();
            string nextAction = string.IsNullOrWhiteSpace(runtimeState.NextActionText)
                ? "\uBAA8\uB378 \uC2E4\uD589\uAE30 \uC124\uCE58 \uB610\uB294 \uACBD\uB85C \uC5F0\uACB0 \uD544\uC694"
                : runtimeState.NextActionText;
            string readinessText = string.IsNullOrWhiteSpace(statusText)
                ? $"{runtimeState.SummaryText} / {nextAction}"
                : statusText;
            string currentModelText = "\uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378: \uBAA8\uB378 \uC2E4\uD589\uAE30 \uBBF8\uC124\uCE58";
            string candidateModelText = "\uC0C8 \uD559\uC2B5 \uBAA8\uB378 \uD6C4\uBCF4: \uC5C6\uC74C";
            string adoptionText = "\uBAA8\uB378 \uC801\uC6A9: \uBAA8\uB378 \uC2E4\uD589\uAE30 \uC5F0\uACB0 \uD544\uC694";
            string nextActionText = $"\uB2E4\uC74C: {nextAction}";
            string noCandidateText = "\uD6C4\uBCF4 \uC5C6\uC74C";
            string decisionEvidence = "\uADFC\uAC70: \uB77C\uBCA8\uB9C1\uC740 \uACC4\uC18D\uD560 \uC218 \uC788\uC9C0\uB9CC \uD559\uC2B5/\uD604\uC7AC \uAC80\uC0AC\uB294 \uC2E4\uD589\uAE30 \uC5F0\uACB0 \uD6C4 \uC0AC\uC6A9\uD569\uB2C8\uB2E4.";

            SetTrainingReadinessStatus(readinessText);
            SetTrainingProgressStatus("\uD559\uC2B5 \uB300\uAE30", string.Empty, 0D, isIndeterminate: false);
            LearningWorkflowViewModel?.SetTrainingModelLifecycleState(
                currentModelText,
                candidateModelText,
                adoptionText,
                nextActionText);
            ShellViewModel?.SetModelCenterModelState(
                currentModelText,
                candidateModelText,
                adoptionText,
                nextActionText,
                noCandidateText,
                nextAction,
                canConfirmModel: false,
                "\uD310\uB2E8: \uBAA8\uB378 \uC2E4\uD589\uAE30 \uC5F0\uACB0 \uD544\uC694",
                decisionEvidence,
                nextActionText);
            ShellViewModel?.SetModelCenterCandidateReviewState(noCandidateText, nextAction, canReviewCandidate: false);
            ShellViewModel?.SetModelRegistryState(new WpfModelRegistryPresentation
            {
                ProfileText = "\uBAA8\uB378 \uD504\uB85C\uD544: \uC2E4\uD589\uAE30 \uC5F0\uACB0 \uD544\uC694",
                TrainingRunText = "\uD559\uC2B5 \uC2E4\uD589: \uBAA8\uB378 \uC2E4\uD589\uAE30 \uC5F0\uACB0 \uC804",
                CandidateModelText = candidateModelText,
                InspectionModelText = currentModelText,
                ActionText = nextActionText,
                SummaryPrimaryText = "\uD604\uC7AC \uAC80\uC0AC: \uBAA8\uB378 \uC2E4\uD589\uAE30 \uBBF8\uC124\uCE58 / \uD559\uC2B5 \uD6C4\uBCF4: \uC5C6\uC74C",
                SummarySecondaryText = $"\uB77C\uBCA8\uB9C1\uC740 \uAC00\uB2A5 / {nextAction}",
                HistoryItems = new WpfModelRegistryHistoryItem[0]
            });
            ShellViewModel?.SetModelCenterRecoveryState(
                "\uBAA8\uB378 \uC2E4\uD589\uAE30 \uBBF8\uC124\uCE58",
                runtimeState.DetailText,
                nextActionText);
            TrainingSettingsViewModel?.SetPostTrainingModelActionState(
                currentModelText,
                candidateModelText,
                adoptionText,
                nextActionText,
                noCandidateText,
                nextAction,
                canReview: false,
                noCandidateText,
                nextAction,
                canConfirm: false);
        }

        private void SaveYoloEditorFields()
        {
            EnsureProjectSettings();
            PythonModelSettings settings = global.Data.ProjectSettings.PythonModel;
            YoloModelSettingsViewModel?.ApplyTo(settings);
            YoloModelSettingsViewModel?.LoadFrom(settings);
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
