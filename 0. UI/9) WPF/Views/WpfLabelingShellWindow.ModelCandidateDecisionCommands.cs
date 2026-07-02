using MvcVisionSystem._1._Core;
using System;
using System.IO;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        private void ExecuteSaveModelCandidateCommand()
        {
            ExecuteSaveYoloSettingsCommand();
        }

        private void ExecuteRejectModelCandidateCommand()
        {
            try
            {
                EnsureProjectSettings();
                PythonModelSettings settings = global.Data.ProjectSettings.PythonModel;
                string candidateWeightsPath = settings.WeightsPath?.Trim() ?? string.Empty;
                string baselineWeightsPath = pendingTrainingBaselineWeightsPath?.Trim() ?? string.Empty;

                if (!hasPendingTrainingWeightsRecipeSave || string.IsNullOrWhiteSpace(candidateWeightsPath))
                {
                    SetYoloCommandStatus("거절할 학습 모델 후보가 없습니다.", isBusy: false);
                    UpdateCandidateModelDecisionPanel();
                    return;
                }

                WpfTrainingWeightsComparison comparison = BuildCurrentTrainingWeightsComparison();
                string decisionSummary = "후보 거절, 기존 검사 모델 유지";
                ModelRegistryService.RecordCandidateDecision(
                    global.Data.ProjectSettings.ModelRegistry,
                    settings,
                    global.Data.ProjectSettings.DatasetPurpose,
                    global.Data.OutputRootPath,
                    candidateWeightsPath,
                    baselineWeightsPath,
                    BuildTrainingComparisonStatusText(comparison),
                    ModelRegistryService.CandidateDecisionRejected,
                    decisionSummary,
                    savedToRecipe: false);

                if (!string.IsNullOrWhiteSpace(baselineWeightsPath) && File.Exists(baselineWeightsPath))
                {
                    settings.WeightsPath = baselineWeightsPath;
                    YoloModelSettingsViewModel?.LoadFrom(settings);
                }

                hasPendingTrainingWeightsRecipeSave = false;
                pendingTrainingBaselineWeightsPath = string.Empty;
                bool configSaved = SaveProjectConfigFromPanel();
                PopulateYoloEditorFields();
                RefreshYoloStatus();
                UpdateYoloTrainingHistoryText();
                RefreshModelCenterDashboard();
                UpdateCandidateModelDecisionPanel();

                string candidateName = Path.GetFileName(candidateWeightsPath);
                SetYoloCommandStatus(
                    configSaved
                        ? $"학습 모델 후보를 거절했습니다: {candidateName}. 현재 검사 모델을 유지합니다."
                        : $"학습 모델 후보를 거절했습니다: {candidateName}. Recipe 저장은 별도 확인이 필요합니다.",
                    isBusy: false);
                SetProjectConfigStatus(configSaved
                    ? "학습 모델 후보 거절 기록 저장 완료."
                    : "학습 모델 후보 거절 기록은 메모리에 반영됐지만 recipe 저장은 실패했습니다.");
                AppendLog($"학습 모델 후보 거절: {candidateWeightsPath} / baseline={baselineWeightsPath}");
            }
            catch (Exception ex)
            {
                SetYoloCommandStatus($"학습 모델 후보 거절 실패: {ex.Message}", isBusy: false);
                AppendLog($"학습 모델 후보 거절 실패: {ex.Message}");
            }
        }

        private void UpdateCandidateModelDecisionPanel(WpfTrainingWeightsComparison comparison = null)
        {
            if (CandidateReviewViewModel == null)
            {
                return;
            }

            EnsureProjectSettings();
            PythonModelSettings settings = global.Data.ProjectSettings.PythonModel;
            comparison ??= BuildCurrentTrainingWeightsComparison();
            string currentWeightsPath = settings.WeightsPath?.Trim() ?? string.Empty;
            string baselineWeightsPath = pendingTrainingBaselineWeightsPath?.Trim() ?? string.Empty;
            bool hasPendingCandidate = hasPendingTrainingWeightsRecipeSave
                && !string.IsNullOrWhiteSpace(currentWeightsPath)
                && File.Exists(currentWeightsPath);
            bool canReject = hasPendingCandidate
                && !string.IsNullOrWhiteSpace(baselineWeightsPath)
                && File.Exists(baselineWeightsPath);

            if (hasPendingCandidate)
            {
                string candidateName = Path.GetFileName(currentWeightsPath);
                string baselineName = string.IsNullOrWhiteSpace(baselineWeightsPath)
                    ? "기존 모델 확인 필요"
                    : Path.GetFileName(baselineWeightsPath);
                CandidateReviewViewModel.SetModelCandidateDecisionState(
                    canSave: true,
                    canReject: canReject,
                    statusText: $"후보 결정: 저장 또는 거절 필요 ({candidateName})",
                    detailText: $"저장하면 다음 추론부터 이 후보를 사용합니다. 거절하면 기존 검사 모델 {baselineName}을 유지합니다.",
                    saveToolTip: "이 학습 결과를 recipe의 현재 검사 모델로 저장합니다.",
                    rejectToolTip: canReject
                        ? "이 학습 결과를 쓰지 않고 기존 검사 모델을 유지합니다."
                        : "되돌릴 기존 검사 모델 경로가 없어 거절할 수 없습니다.");
                return;
            }

            ModelCandidate latestCandidate = ModelRegistryService.FindLatestCandidate(global.Data.ProjectSettings.ModelRegistry);
            if (latestCandidate != null)
            {
                string candidateName = Path.GetFileName(latestCandidate.WeightsPath ?? string.Empty);
                string decision = latestCandidate.Decision ?? string.Empty;
                if (string.Equals(decision, ModelRegistryService.CandidateDecisionRejected, StringComparison.Ordinal))
                {
                    CandidateReviewViewModel.SetModelCandidateDecisionState(
                        canSave: false,
                        canReject: false,
                        statusText: $"후보 결정: 거절됨 ({candidateName})",
                        detailText: string.IsNullOrWhiteSpace(latestCandidate.DecisionSummary)
                            ? "이 후보는 현재 recipe의 검사 모델로 채택하지 않았습니다."
                            : latestCandidate.DecisionSummary,
                        saveToolTip: "이미 거절된 후보입니다. 다시 쓰려면 모델 설정에서 직접 선택하세요.",
                        rejectToolTip: "이미 거절된 후보입니다.");
                    return;
                }

                if (string.Equals(decision, ModelRegistryService.CandidateDecisionAdopted, StringComparison.Ordinal)
                    || latestCandidate.SavedToRecipe)
                {
                    CandidateReviewViewModel.SetModelCandidateDecisionState(
                        canSave: false,
                        canReject: false,
                        statusText: $"후보 결정: 검사 모델로 저장됨 ({candidateName})",
                        detailText: "이 후보는 recipe의 현재 검사 모델 이력에 기록되어 있습니다.",
                        saveToolTip: "이미 검사 모델로 저장된 후보입니다.",
                        rejectToolTip: "이미 저장된 후보는 여기서 거절하지 않습니다.");
                    return;
                }
            }

            if (comparison?.HasLatestWeights == true)
            {
                CandidateReviewViewModel.SetModelCandidateDecisionState(
                    canSave: false,
                    canReject: false,
                    statusText: "후보 결정: 검토 가능",
                    detailText: "후보 검증을 실행해 기존 검사 모델과 비교한 뒤 저장 여부를 결정하세요.",
                    saveToolTip: "먼저 후보 검증으로 학습 결과를 선택하세요.",
                    rejectToolTip: "먼저 후보 검증으로 학습 결과를 선택하세요.");
                return;
            }

            CandidateReviewViewModel.SetModelCandidateDecisionState(
                canSave: false,
                canReject: false,
                statusText: "후보 결정: 후보 없음",
                detailText: "학습이 완료된 모델 후보가 생기면 이곳에서 저장 또는 거절 결정을 남길 수 있습니다.",
                saveToolTip: "저장할 학습 모델 후보가 없습니다.",
                rejectToolTip: "거절할 학습 모델 후보가 없습니다.");
        }
    }
}
