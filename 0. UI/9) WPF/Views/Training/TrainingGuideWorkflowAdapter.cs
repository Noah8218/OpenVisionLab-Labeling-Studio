using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the WPF callback boundary for the training guide. Existing
    /// readiness, history, comparison, and step-completion services keep
    /// their policy; this adapter only snapshots Shell state and projects the
    /// results to the learning workflow ViewModel.
    /// </summary>
    internal sealed class TrainingGuideWorkflowAdapter
    {
        private readonly TrainingGuideWorkflowAdapterContext context;

        internal TrainingGuideWorkflowAdapter(TrainingGuideWorkflowAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            if (context.DataProvider == null)
            {
                throw new ArgumentNullException(nameof(context.DataProvider));
            }

            if (context.LearningWorkflowViewModel == null)
            {
                throw new ArgumentNullException(nameof(context.LearningWorkflowViewModel));
            }

            if (context.TrainingGuideHistoryService == null)
            {
                throw new ArgumentNullException(nameof(context.TrainingGuideHistoryService));
            }

            if (context.TrainingGuideHistoryWorkflowService == null)
            {
                throw new ArgumentNullException(nameof(context.TrainingGuideHistoryWorkflowService));
            }

            if (context.ProjectRecipeSessionService == null)
            {
                throw new ArgumentNullException(nameof(context.ProjectRecipeSessionService));
            }

            if (context.AnomalyImageReviewSession == null)
            {
                throw new ArgumentNullException(nameof(context.AnomalyImageReviewSession));
            }

            if (context.CurrentRecipeNameProvider == null)
            {
                throw new ArgumentNullException(nameof(context.CurrentRecipeNameProvider));
            }

            if (context.CommunicationStatusProvider == null)
            {
                throw new ArgumentNullException(nameof(context.CommunicationStatusProvider));
            }

            if (context.HasPendingTrainingWeightsRecipeSaveProvider == null)
            {
                throw new ArgumentNullException(nameof(context.HasPendingTrainingWeightsRecipeSaveProvider));
            }

            if (context.LastTrainingReadinessReportProvider == null)
            {
                throw new ArgumentNullException(nameof(context.LastTrainingReadinessReportProvider));
            }

            if (context.SetLastTrainingReadinessReport == null)
            {
                throw new ArgumentNullException(nameof(context.SetLastTrainingReadinessReport));
            }

            if (context.BuildCurrentTrainingWeightsComparison == null)
            {
                throw new ArgumentNullException(nameof(context.BuildCurrentTrainingWeightsComparison));
            }

            if (context.UpdateTrainingComparisonViewModel == null)
            {
                throw new ArgumentNullException(nameof(context.UpdateTrainingComparisonViewModel));
            }

            if (context.EnsureProjectSettings == null)
            {
                throw new ArgumentNullException(nameof(context.EnsureProjectSettings));
            }

            if (context.AppendLog == null)
            {
                throw new ArgumentNullException(nameof(context.AppendLog));
            }

            if (context.ActiveImagePathProvider == null)
            {
                throw new ArgumentNullException(nameof(context.ActiveImagePathProvider));
            }

            if (context.ImageQueueItemsProvider == null)
            {
                throw new ArgumentNullException(nameof(context.ImageQueueItemsProvider));
            }

            if (context.ManualRoiCountProvider == null)
            {
                throw new ArgumentNullException(nameof(context.ManualRoiCountProvider));
            }

            if (context.ConfirmedCandidateCountProvider == null)
            {
                throw new ArgumentNullException(nameof(context.ConfirmedCandidateCountProvider));
            }

            if (context.PendingCandidateCountProvider == null)
            {
                throw new ArgumentNullException(nameof(context.PendingCandidateCountProvider));
            }
        }

        #region TrainingGuide
        internal void UpdateYoloTrainingGuideDatasetHistory(
            YoloDatasetReadinessReport report,
            TrainingChecklistPresentation presentation,
            bool recordHistory)
        {
            LabelingProjectData data = context.DataProvider();
            context.TrainingGuideHistoryWorkflowService.RecordDatasetHistory(
                new TrainingGuideDatasetHistoryWorkflowRequest
                {
                    Data = data,
                    IsReady = report?.IsReady == true,
                    IssueKind = presentation?.IssueKind,
                    Summary = presentation?.DetailText,
                    RecordHistory = recordHistory,
                    HasPendingTrainingWeightsRecipeSave = context.HasPendingTrainingWeightsRecipeSaveProvider(),
                    SaveHistoryQuietly = TrySaveTrainingGuideHistoryQuietly
                });
            UpdateYoloTrainingHistoryText();
        }

        internal void UpdateYoloTrainingGuideTrainingHistory(PythonCommunicationStatus status)
        {
            if (!TrainingProgressPresentationService.HasTrainingStatus(status))
            {
                return;
            }

            LabelingProjectData data = context.DataProvider();
            context.TrainingGuideHistoryWorkflowService.RecordTrainingHistory(
                new TrainingGuideTrainingHistoryWorkflowRequest
                {
                    Data = data,
                    Status = status,
                    HasPendingTrainingWeightsRecipeSave = context.HasPendingTrainingWeightsRecipeSaveProvider(),
                    SaveHistoryQuietly = TrySaveTrainingGuideHistoryQuietly
                });
            UpdateYoloTrainingHistoryText();
        }

        internal void UpdateYoloTrainingHistoryText()
        {
            context.EnsureProjectSettings();
            YoloTrainingGuideHistory history = context.DataProvider().ProjectSettings.TrainingGuide;
            history.EnsureDefaults();
            context.LearningWorkflowViewModel.SetTrainingRunHistoryItems(
                context.TrainingGuideHistoryService.BuildRunHistoryItems(history, TrainingProgressPresentationService.FormatTrainingState));
            context.LearningWorkflowViewModel.SetTrainingHistoryText(
                context.TrainingGuideHistoryService.BuildHistoryText(history, TrainingProgressPresentationService.FormatTrainingState));
            UpdateTrainingResultComparisonText();
        }

        internal void UpdateTrainingResultComparisonText()
        {
            WpfTrainingWeightsComparison comparison = context.BuildCurrentTrainingWeightsComparison();

            // The guide shows metrics as the reason for keeping or switching best.pt,
            // so keep this next to history updates instead of burying it in the log.
            context.UpdateTrainingComparisonViewModel(comparison);
        }

        internal bool TrySaveTrainingGuideHistoryQuietly()
        {
            string recipeName = context.CurrentRecipeNameProvider();
            if (string.IsNullOrWhiteSpace(recipeName))
            {
                return false;
            }

            try
            {
                // Training progress is recipe metadata. The training start already
                // captured the exact dataset, so do not rescan every image here.
                RecipeConfigurationSaveResult saveResult = context.ProjectRecipeSessionService.SaveConfiguration(
                    context.DataProvider(),
                    recipeName,
                    updateYoloDataYaml: false,
                    refreshDatasetVersion: false);
                if (!saveResult.IsSuccess)
                {
                    context.AppendLog($"학습 가이드 이력 저장 실패: {saveResult.ErrorMessage}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                context.AppendLog($"학습 가이드 이력 저장 실패: {ex.Message}");
                return false;
            }
        }

        internal void UpdateYoloTrainingChecklist(YoloDatasetReadinessReport report, bool recordHistory)
        {
            if (report == null)
            {
                return;
            }

            context.SetLastTrainingReadinessReport(report);
            LabelingProjectData data = context.DataProvider();
            TrainingChecklistPresentation presentation = TrainingReadinessPresentationService.BuildChecklistPresentation(
                data,
                report);
            context.LearningWorkflowViewModel.SetTrainingChecklistLocalization(presentation.Localization);
            context.LearningWorkflowViewModel.SetTrainingChecklistActionLocalization(presentation.ActionLocalization);
            UpdateDatasetStatusDashboard(report, presentation);
            UpdateYoloTrainingGuideDatasetHistory(report, presentation, recordHistory);
            UpdateYoloTrainingHistoryText();
            RefreshYoloTrainingStepCompletion(report);
        }

        // The Shell remains a UI adapter: it snapshots live state, applies the
        // service result to the existing workflow ViewModel, and owns no step policy.
        internal void RefreshYoloTrainingStepCompletion(YoloDatasetReadinessReport report = null)
        {
            report ??= context.LastTrainingReadinessReportProvider();
            string recipeName = context.CurrentRecipeNameProvider();
            LabelingProjectData data = context.DataProvider();
            bool hasDatasetSetup = (!string.IsNullOrWhiteSpace(recipeName)
                    && File.Exists(LabelingDatasetManifestService.GetManifestPath(recipeName)))
                || (!string.IsNullOrWhiteSpace(data?.OutputRootPath)
                    && Directory.Exists(data.OutputRootPath));
            bool hasCompletedCurrentDatasetTraining = false;
            PythonCommunicationStatus status = context.CommunicationStatusProvider();
            string trainingState = status?.LastTrainingState?.Trim() ?? string.Empty;
            bool trainingCompletedFromWorker = TrainingWeightsService.IsCompletedTrainingState(trainingState);
            if (!trainingCompletedFromWorker)
            {
                hasCompletedCurrentDatasetTraining = context.BuildCurrentTrainingWeightsComparison()?.HasCompletedCurrentDatasetTraining == true;
            }

            IEnumerable<WpfImageQueueItem> queueSource = context.ImageQueueItemsProvider()
                ?? Enumerable.Empty<WpfImageQueueItem>();
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
                context.ActiveImagePathProvider(),
                data?.ClassNamedList?.Count ?? 0,
                context.ManualRoiCountProvider(),
                context.ConfirmedCandidateCountProvider(),
                hasDatasetSetup,
                hasCompletedCurrentDatasetTraining,
                status,
                context.PendingCandidateCountProvider());

            foreach (TrainingStepState step in completion.Steps)
            {
                context.LearningWorkflowViewModel.SetYoloTrainingStepState(step.Order, step.IsCompleted, step.StateText);
            }

            context.LearningWorkflowViewModel.SetYoloFixActionAvailability(
                canFixClasses: true,
                canFixLabels: completion.HasImages,
                canFixDataset: true);
        }

        internal void FinishQueueCompletionAndGuideDatasetCheck()
        {
            // The queue completion transition belongs to the training guide workflow:
            // the Shell only supplies the existing presentation callbacks.
            context.RefreshTrainingReadinessPanel?.Invoke(true);
            WpfLearningStepItem saveStep = context.LearningWorkflowViewModel.LearningSteps
                .FirstOrDefault(step => step.Step == WpfLearningStep.Save);
            if (saveStep != null)
            {
                context.LearningWorkflowViewModel.SelectedStep = saveStep;
            }

            context.SetModelStatus?.Invoke("이미지 완료: 데이터셋 점검 결과를 확인하세요.");
            context.AppendLog("모든 이미지 완료: 데이터셋 점검을 실행했습니다. 다음 단계로 이동할 수 있습니다.");
            context.RefreshCanvasWorkflowContext?.Invoke();
        }

        internal void UpdateDatasetStatusDashboard(
            YoloDatasetReadinessReport report,
            TrainingChecklistPresentation presentation)
        {
            if (report == null)
            {
                return;
            }

            YoloDatasetStatistics statistics = report.Statistics ?? new YoloDatasetStatistics();
            LabelingProjectData data = context.DataProvider();
            int classCount = data?.ClassNamedList?.Count ?? 0;
            IReadOnlyList<string> warnings = report.IsReady
                ? YoloDatasetDiagnosticsService.BuildQualityWarnings(data, statistics)
                : Array.Empty<string>();
            AnomalyImageReviewSummary anomalySummary = context.AnomalyImageReviewSession.LoadSummary(
                data,
                statistics.TotalImageCount,
                isAnomalyPurpose: report.Purpose == LabelingDatasetPurpose.AnomalyDetection);
            YoloDatasetQualityAuditReport qualityAudit = YoloDatasetQualityAuditService.Build(data);
            DatasetDashboardLocalizationSnapshot dashboardLocalization = DatasetDashboardLocalizationService.Build(
                report,
                statistics,
                classCount,
                warnings,
                anomalySummary,
                qualityAudit,
                TrainingReadinessPresentationService.ClassifyIssue(report.Errors));

            context.LearningWorkflowViewModel.SetModelReplacementLocalization(
                BuildModelReplacementLocalization(report, statistics));
            context.LearningWorkflowViewModel.SetDatasetDashboard(
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

    internal sealed class TrainingGuideWorkflowAdapterContext
    {
        internal Func<LabelingProjectData> DataProvider { get; init; }
        internal WpfLearningWorkflowPanelViewModel LearningWorkflowViewModel { get; init; }
        internal TrainingGuideHistoryService TrainingGuideHistoryService { get; init; }
        internal TrainingGuideHistoryWorkflowService TrainingGuideHistoryWorkflowService { get; init; }
        internal ProjectRecipeSessionService ProjectRecipeSessionService { get; init; }
        internal AnomalyImageReviewSession AnomalyImageReviewSession { get; init; }
        internal Func<string> CurrentRecipeNameProvider { get; init; }
        internal Func<PythonCommunicationStatus> CommunicationStatusProvider { get; init; }
        internal Func<bool> HasPendingTrainingWeightsRecipeSaveProvider { get; init; }
        internal Func<YoloDatasetReadinessReport> LastTrainingReadinessReportProvider { get; init; }
        internal Action<YoloDatasetReadinessReport> SetLastTrainingReadinessReport { get; init; }
        internal Func<WpfTrainingWeightsComparison> BuildCurrentTrainingWeightsComparison { get; init; }
        internal Action<WpfTrainingWeightsComparison> UpdateTrainingComparisonViewModel { get; init; }
        internal Action EnsureProjectSettings { get; init; }
        internal Action<string> AppendLog { get; init; }
        internal Func<string> ActiveImagePathProvider { get; init; }
        internal Func<IEnumerable<WpfImageQueueItem>> ImageQueueItemsProvider { get; init; }
        internal Func<int> ManualRoiCountProvider { get; init; }
        internal Func<int> ConfirmedCandidateCountProvider { get; init; }
        internal Func<int> PendingCandidateCountProvider { get; init; }
        internal Action<bool> RefreshTrainingReadinessPanel { get; init; }
        internal Action RefreshCanvasWorkflowContext { get; init; }
        internal Action<string> SetModelStatus { get; init; }
    }
}
