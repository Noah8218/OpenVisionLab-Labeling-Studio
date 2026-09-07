using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace MvcVisionSystem
{
    // Responsibility group: training guide checklist and history presentation.
    // These members remain WPF Window adapters; independent policy belongs in services.
    public partial class WpfLabelingShellWindow
    {
        #region TrainingGuideHistoryStatus
        // Training guide history persistence is separated from live checklist calculation.
        private void UpdateYoloTrainingGuideDatasetHistory(
            YoloDatasetReadinessReport report,
            TrainingChecklistPresentation presentation,
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
            if (!TrainingProgressPresentationService.HasTrainingStatus(status))
            {
                return;
            }

            EnsureProjectSettings();
            YoloTrainingGuideHistory history = global.Data.ProjectSettings.TrainingGuide;
            trainingGuideHistoryService.UpdateTrainingHistory(
                history,
                status,
                TrainingProgressPresentationService.IsTerminalTrainingState,
                ref lastRecordedTrainingGuideRunSignature);
            UpdateYoloTrainingHistoryText();

            if (TrainingProgressPresentationService.IsTerminalTrainingState(history.LastTrainingState) && !hasPendingTrainingWeightsRecipeSave)
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
                savedToRecipe,
                global.Data.ProjectSettings.TrainingGuide.LastTrainingDatasetVersionId,
                global.Data.ProjectSettings.TrainingGuide.LastTrainingDatasetContentSha256);
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
                trainingGuideHistoryService.BuildRunHistoryItems(history, TrainingProgressPresentationService.FormatTrainingState));
            LearningWorkflowViewModel.SetTrainingHistoryText(
                trainingGuideHistoryService.BuildHistoryText(history, TrainingProgressPresentationService.FormatTrainingState));
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
                // Training progress is recipe metadata. The training start already
                // captured the exact dataset, so do not rescan every image here.
                RecipeConfigurationSaveResult saveResult = projectRecipeSessionService.SaveConfiguration(
                    global.Data,
                    recipeName,
                    updateYoloDataYaml: false,
                    refreshDatasetVersion: false);
                if (!saveResult.IsSuccess)
                {
                    AppendLog($"\uD559\uC2B5 \uAC00\uC774\uB4DC \uC774\uB825 \uC800\uC7A5 \uC2E4\uD328: {saveResult.ErrorMessage}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                AppendLog($"학습 가이드 이력 저장 실패: {ex.Message}");
                return false;
            }
        }
        #endregion

        #region TrainingGuideStatus
        // Training guide status owns dataset checklist and history persistence; live worker polling stays in TrainingStatus.
        private void UpdateYoloTrainingChecklist(YoloDatasetReadinessReport report, bool recordHistory)
        {
            if (LearningWorkflowViewModel == null || report == null)
            {
                return;
            }

            lastYoloTrainingReadinessReport = report;
            TrainingChecklistPresentation presentation = TrainingReadinessPresentationService.BuildChecklistPresentation(
                global.Data,
                report);
            LearningWorkflowViewModel.SetTrainingChecklistLocalization(presentation.Localization);
            LearningWorkflowViewModel.SetTrainingChecklistActionLocalization(presentation.ActionLocalization);
            UpdateDatasetStatusDashboard(report, presentation);
            UpdateYoloTrainingGuideDatasetHistory(report, presentation, recordHistory);
            UpdateYoloTrainingHistoryText();
            RefreshYoloTrainingStepCompletion(report);
        }

        // The Shell remains a UI adapter: it snapshots live state, applies the
        // service result to the existing workflow ViewModel, and owns no step policy.
        private void RefreshYoloTrainingStepCompletion(YoloDatasetReadinessReport report = null)
        {
            if (LearningWorkflowViewModel == null)
            {
                return;
            }

            report ??= lastYoloTrainingReadinessReport;
            string recipeName = GetCurrentRecipeName();
            bool hasDatasetSetup = (!string.IsNullOrWhiteSpace(recipeName)
                    && File.Exists(LabelingDatasetManifestService.GetManifestPath(recipeName)))
                || (!string.IsNullOrWhiteSpace(global.Data?.OutputRootPath)
                    && Directory.Exists(global.Data.OutputRootPath));
            bool hasCompletedCurrentDatasetTraining = false;
            PythonCommunicationStatus status = global.GetPythonCommunicationStatusSnapshot();
            string trainingState = status?.LastTrainingState?.Trim() ?? string.Empty;
            bool trainingCompletedFromWorker = TrainingWeightsService.IsCompletedTrainingState(trainingState);
            if (!trainingCompletedFromWorker)
            {
                hasCompletedCurrentDatasetTraining = BuildCurrentTrainingWeightsComparison()?.HasCompletedCurrentDatasetTraining == true;
            }

            IEnumerable<WpfImageQueueItem> queueSource = imageQueueItems == null
                ? Enumerable.Empty<WpfImageQueueItem>()
                : imageQueueItems;
            IReadOnlyList<TrainingStepQueueState> queueItems = queueSource
                .Where(item => item != null)
                .Select(item => new TrainingStepQueueState(
                    item.IsLabeled,
                    item.IsSaveRequired,
                    item.ReviewState,
                    item.QualityReviewState))
                .ToList();
            TrainingStepCompletionSnapshot completion = TrainingStepCompletionService.Build(
                report,
                queueItems,
                activeImagePath,
                global.Data?.ClassNamedList?.Count ?? 0,
                manualRois.Count,
                confirmedDetectionCandidates.Count,
                hasDatasetSetup,
                hasCompletedCurrentDatasetTraining,
                status,
                pendingDetectionCandidates.Count);

            foreach (TrainingStepState step in completion.Steps)
            {
                LearningWorkflowViewModel.SetYoloTrainingStepState(step.Order, step.IsCompleted, step.StateText);
            }

            LearningWorkflowViewModel.SetYoloFixActionAvailability(
                canFixClasses: true,
                canFixLabels: completion.HasImages,
                canFixDataset: true);
        }

        private void UpdateDatasetStatusDashboard(
            YoloDatasetReadinessReport report,
            TrainingChecklistPresentation presentation)
        {
            if (LearningWorkflowViewModel == null || report == null)
            {
                return;
            }

            YoloDatasetStatistics statistics = report.Statistics ?? new YoloDatasetStatistics();
            int classCount = global.Data?.ClassNamedList?.Count ?? 0;
            IReadOnlyList<string> warnings = report.IsReady
                ? YoloDatasetDiagnosticsService.BuildQualityWarnings(global.Data, statistics)
                : Array.Empty<string>();
            AnomalyImageReviewSummary anomalySummary = report.Purpose == LabelingDatasetPurpose.AnomalyDetection
                ? anomalyImageReviewWorkflowService.LoadPersistedSummary(global.Data, statistics.TotalImageCount)
                : null;
            YoloDatasetQualityAuditReport qualityAudit = YoloDatasetQualityAuditService.Build(global.Data);
            DatasetDashboardLocalizationSnapshot dashboardLocalization = DatasetDashboardLocalizationService.Build(
                report,
                statistics,
                classCount,
                warnings,
                anomalySummary,
                qualityAudit,
                TrainingReadinessPresentationService.ClassifyIssue(report.Errors));

            LearningWorkflowViewModel.SetModelReplacementLocalization(
                BuildModelReplacementLocalization(report, statistics));
            LearningWorkflowViewModel.SetDatasetDashboard(
                dashboardLocalization.StatusText,
                dashboardLocalization.SummaryText,
                presentation?.ActionText ?? string.Empty,
                DatasetDashboardPresentationService.BuildMetrics(report, statistics, classCount, anomalySummary, qualityAudit),
                dashboardLocalization.IssueItems,
                dashboardLocalization);
        }



        private static ModelReplacementLocalizationSnapshot BuildModelReplacementLocalization(
            YoloDatasetReadinessReport report,
            YoloDatasetStatistics statistics)
        {
            return ModelReplacementLocalizationService.Build(report, statistics);
        }
        #endregion

    }
}
