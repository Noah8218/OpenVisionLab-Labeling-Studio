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
                    SetYoloCommandStatus(WpfModelCandidateDecisionPresentationService.BuildNoRejectCandidateStatus(), isBusy: false);
                    UpdateCandidateModelDecisionPanel();
                    return;
                }

                WpfTrainingWeightsComparison comparison = BuildCurrentTrainingWeightsComparison();
                string decisionSummary = WpfModelCandidateDecisionPresentationService.BuildRejectDecisionSummary();
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

                SetYoloCommandStatus(WpfModelCandidateDecisionPresentationService.BuildRejectCommandStatus(candidateWeightsPath, configSaved), isBusy: false);
                SetProjectConfigStatus(WpfModelCandidateDecisionPresentationService.BuildRejectProjectConfigStatus(configSaved));
                AppendLog(WpfModelCandidateDecisionPresentationService.BuildRejectLog(candidateWeightsPath, baselineWeightsPath));
            }
            catch (Exception ex)
            {
                string failureStatus = WpfModelCandidateDecisionPresentationService.BuildRejectFailureStatus(ex.Message);
                SetYoloCommandStatus(failureStatus, isBusy: false);
                AppendLog(failureStatus);
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
                ApplyModelCandidateDecisionPresentation(
                    WpfModelCandidateDecisionPresentationService.BuildPendingCandidate(
                        currentWeightsPath,
                        baselineWeightsPath,
                        canReject));
                return;
            }

            ModelCandidate latestCandidate = ModelRegistryService.FindLatestCandidate(global.Data.ProjectSettings.ModelRegistry);
            if (latestCandidate != null)
            {
                string decision = latestCandidate.Decision ?? string.Empty;
                if (string.Equals(decision, ModelRegistryService.CandidateDecisionRejected, StringComparison.Ordinal))
                {
                    ApplyModelCandidateDecisionPresentation(
                        WpfModelCandidateDecisionPresentationService.BuildRejectedCandidate(
                            latestCandidate.WeightsPath,
                            latestCandidate.DecisionSummary));
                    return;
                }

                if (string.Equals(decision, ModelRegistryService.CandidateDecisionAdopted, StringComparison.Ordinal)
                    || latestCandidate.SavedToRecipe)
                {
                    ApplyModelCandidateDecisionPresentation(
                        WpfModelCandidateDecisionPresentationService.BuildSavedCandidate(latestCandidate.WeightsPath));
                    return;
                }
            }

            if (comparison?.HasLatestWeights == true)
            {
                ApplyModelCandidateDecisionPresentation(WpfModelCandidateDecisionPresentationService.BuildReviewAvailable());
                return;
            }

            ApplyModelCandidateDecisionPresentation(WpfModelCandidateDecisionPresentationService.BuildNoCandidate());
        }

        private void ApplyModelCandidateDecisionPresentation(WpfModelCandidateDecisionPresentation presentation)
        {
            if (CandidateReviewViewModel == null || presentation == null)
            {
                return;
            }

            CandidateReviewViewModel.SetModelCandidateDecisionState(
                presentation.CanSave,
                presentation.CanReject,
                presentation.StatusText,
                presentation.DetailText,
                presentation.SaveToolTip,
                presentation.RejectToolTip);
        }
    }
}
