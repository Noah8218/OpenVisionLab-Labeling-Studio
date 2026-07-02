using Lib.Common;
using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Training guide history persistence is separated from live checklist calculation.
        private void UpdateYoloTrainingGuideDatasetHistory(
            YoloDatasetReadinessReport report,
            YoloTrainingIssuePresentation presentation,
            bool recordHistory)
        {
            EnsureProjectSettings();
            YoloTrainingGuideHistory history = global.Data.ProjectSettings.TrainingGuide;
            trainingGuideHistoryService.UpdateDatasetHistory(
                history,
                report?.IsReady == true,
                presentation?.IssueKind,
                presentation?.DetailText,
                recordHistory);
            UpdateYoloTrainingHistoryText();

            if (!hasPendingTrainingWeightsRecipeSave)
            {
                TrySaveTrainingGuideHistoryQuietly();
            }
        }

        private void UpdateYoloTrainingGuideTrainingHistory(PythonCommunicationStatus status)
        {
            if (!HasTrainingStatus(status))
            {
                return;
            }

            EnsureProjectSettings();
            YoloTrainingGuideHistory history = global.Data.ProjectSettings.TrainingGuide;
            trainingGuideHistoryService.UpdateTrainingHistory(
                history,
                status,
                IsTerminalTrainingState,
                ref lastRecordedTrainingGuideRunSignature);
            UpdateYoloTrainingHistoryText();

            if (IsTerminalTrainingState(history.LastTrainingState) && !hasPendingTrainingWeightsRecipeSave)
            {
                TrySaveTrainingGuideHistoryQuietly();
            }
        }

        private void UpdateAppliedTrainingWeightsHistory(string weightsPath, bool savedToRecipe)
        {
            EnsureProjectSettings();
            WpfTrainingWeightsComparison comparison = BuildCurrentTrainingWeightsComparison();
            trainingGuideHistoryService.UpdateAppliedWeightsHistory(
                global.Data.ProjectSettings.TrainingGuide,
                weightsPath,
                savedToRecipe);
            ModelRegistryService.RecordTrainingCandidate(
                global.Data.ProjectSettings.ModelRegistry,
                global.Data.ProjectSettings.PythonModel,
                global.Data.ProjectSettings.DatasetPurpose,
                global.Data.OutputRootPath,
                weightsPath,
                pendingTrainingBaselineWeightsPath,
                comparison?.MetricsStatusText,
                global.Data.ProjectSettings.TrainingGuide.LastTrainingState,
                global.Data.ProjectSettings.TrainingGuide.LastTrainingProgressPercent,
                global.Data.ProjectSettings.TrainingGuide.LastTrainingMessage,
                savedToRecipe);
            UpdateYoloTrainingHistoryText();
        }

        private void UpdateYoloTrainingHistoryText()
        {
            if (LearningWorkflowViewModel == null)
            {
                return;
            }

            EnsureProjectSettings();
            YoloTrainingGuideHistory history = global.Data.ProjectSettings.TrainingGuide;
            history.EnsureDefaults();
            LearningWorkflowViewModel.SetTrainingRunHistoryItems(
                trainingGuideHistoryService.BuildRunHistoryItems(history, FormatTrainingState));
            LearningWorkflowViewModel.TrainingHistoryText = trainingGuideHistoryService.BuildHistoryText(history, FormatTrainingState);
            UpdateTrainingResultComparisonText();
        }

        private void UpdateTrainingResultComparisonText()
        {
            if (LearningWorkflowViewModel == null)
            {
                return;
            }

            WpfTrainingWeightsComparison comparison = BuildCurrentTrainingWeightsComparison();

            // The guide shows metrics as the reason for keeping or switching best.pt,
            // so keep this next to history updates instead of burying it in the log.
            UpdateTrainingComparisonViewModel(comparison);
        }

        private bool TrySaveTrainingGuideHistoryQuietly()
        {
            string recipeName = GetCurrentRecipeName();
            if (string.IsNullOrWhiteSpace(recipeName))
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
                AppendLog($"학습 가이드 이력 저장 실패: {ex.Message}");
                return false;
            }
        }
    }
}
