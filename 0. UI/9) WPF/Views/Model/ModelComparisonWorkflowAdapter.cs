using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MvcVisionSystem
{
    // Owns WPF composition for model-comparison callbacks and review history.
    // Execution, cancellation, and process lifetime remain in ModelComparisonWorkflowService.
    internal sealed class ModelComparisonWorkflowAdapter
    {
        private readonly ModelComparisonWorkflowAdapterContext context;

        private LabelingProjectData projectData => context.DataProvider?.Invoke();

        private WpfTrainingSettingsPanelViewModel TrainingSettingsViewModel =>
            context.TrainingSettingsViewModelProvider?.Invoke();

        private WpfLearningWorkflowPanelViewModel LearningWorkflowViewModel =>
            context.LearningWorkflowViewModelProvider?.Invoke();

        private WpfCandidateReviewPanelViewModel CandidateReviewViewModel =>
            context.CandidateReviewViewModelProvider?.Invoke();

        internal ModelComparisonWorkflowAdapter(ModelComparisonWorkflowAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            ArgumentNullException.ThrowIfNull(context.DataProvider);
        }

        internal ModelComparisonWorkflowService CreateWorkflow()
        {
            return new ModelComparisonWorkflowService(
                () => context.ModelComparisonRunService.BuildRequest(
                    projectData,
                    context.TrainingWeightsService,
                    task: "test",
                    baselineWeightsOverride: ModelCenterDashboardWorkflowService.ResolveComparisonCurrentWeightsPath(
                        projectData.ProjectSettings.PythonModel.WeightsPath,
                        context.PendingTrainingBaselineWeightsPathProvider?.Invoke())),
                compareYoloV8ToYolo11 => compareYoloV8ToYolo11
                    ? context.ModelComparisonRunService.BuildYoloV8Yolo11DetectionRequest(projectData)
                    : context.ModelComparisonRunService.BuildYoloV5YoloV8DetectionRequest(projectData),
                () => context.SegmentationAdapterComparisonRunService.BuildRequest(
                    projectData,
                    projectData.ProjectSettings.PythonModel,
                    TrainingSettingsViewModel.SegmentationUnetWeightsPath,
                    TrainingSettingsViewModel.SegmentationYoloWeightsPath,
                    TrainingSettingsViewModel.SelectedSegmentationYoloEngine),
                context.ModelComparisonRunService.ValidateRequest,
                context.SegmentationAdapterComparisonRunService.ValidateRequest,
                context.ModelComparisonRunService.RunAsync,
                context.SegmentationAdapterComparisonRunService.RunAsync,
                (result, request) => context.ModelComparisonReviewService.BuildFromSummaryFile(
                    result.SummaryPath,
                    projectData.ClassNamedList?.Select(item => item?.Text ?? string.Empty).ToList()
                        ?? new List<string>(),
                    request.UiConfidence,
                    maxExamples: 5));
        }

        internal SegmentationAdapterComparisonContext BuildSegmentationAdapterComparisonContext()
        {
            context.EnsureProjectSettings();
            LabelingProjectData data = projectData;
            return context.SegmentationAdapterComparisonRunService.BuildContext(
                data,
                data?.ProjectSettings?.ModelRegistry,
                data?.ProjectSettings?.PythonModel);
        }

        internal bool TryApplyLatestTrainingWeightsFromProject(bool logIfUnchanged)
        {
            context.EnsureProjectSettings();
            PythonModelSettings settings = projectData.ProjectSettings.PythonModel;
            TrainingWeightsApplicationResult application = context.TrainingWeightsApplicationWorkflowService.StageLatestCandidate(
                new TrainingWeightsApplicationRequest
                {
                    Data = projectData,
                    CurrentWeightsPath = settings.WeightsPath
                });
            WpfTrainingWeightsComparison comparison = application.Comparison;
            string comparisonStatusText = TrainingComparisonPresentationService.BuildComparisonStatusText(comparison);
            if (LearningWorkflowViewModel != null)
            {
                UpdateTrainingComparisonViewModel(comparison, comparisonStatusText);
            }

            context.RefreshModelCenterDashboard(comparison);
            if (application.Status == TrainingWeightsApplicationStatus.NoCandidate)
            {
                if (logIfUnchanged)
                {
                    context.SetCommandStatus($"{comparisonStatusText}. 모델 설정에서 학습 결과 모델을 직접 선택하세요.", false);
                    context.AppendLog("학습 결과 모델 후보를 찾지 못했습니다.");
                }

                return false;
            }

            if (application.Status == TrainingWeightsApplicationStatus.AlreadyCurrent)
            {
                if (logIfUnchanged)
                {
                    context.SetCommandStatus(comparisonStatusText, false);
                    context.AppendLog($"현재 검사 모델 유지: {application.CandidateWeightsPath}");
                }

                return false;
            }

            if (application.Status == TrainingWeightsApplicationStatus.NotNewer)
            {
                if (logIfUnchanged)
                {
                    context.SetCommandStatus(comparisonStatusText, false);
                    context.AppendLog($"현재 검사 모델 유지: {settings.WeightsPath}");
                }

                return false;
            }

            string latestWeightsPath = application.CandidateWeightsPath;
            context.SetPendingTrainingBaselineWeightsPath(application.BaselineWeightsPath);
            context.LoadYoloSettings(settings);
            string latestDisplayName = TrainingWeightsService.FormatWeightsDisplayPath(latestWeightsPath);
            context.SetModelStatus($"모델 후보: {Path.GetFileName(latestWeightsPath)}");
            context.SetHasPendingTrainingWeightsRecipeSave(true);
            context.RefreshModelCenterDashboard(comparison);
            context.SetGlobalInferenceStatus(string.Empty, false);
            context.SetModelStatus($"모델 후보: {latestDisplayName}");
            context.FocusYoloModelSettingsTab();
            context.FocusSaveYoloSettingsButton();
            context.SetProjectConfigStatus("새 학습 모델 후보를 검사 모델 설정에 올렸습니다. 모델 비교 후 저장하면 프로젝트에 반영됩니다.");
            context.SetCommandStatus($"새 학습 모델 후보: {Path.GetFileName(latestWeightsPath)} / {comparison.MetricsStatusText} / 모델 비교 후 저장 필요", false);
            context.SetCommandStatus($"현재 데이터셋 학습 완료: {latestDisplayName} / {comparison.MetricsStatusText} / 모델 비교 및 저장 필요", false);

            if (!string.Equals(context.LastAutoAppliedTrainingWeightsPathProvider?.Invoke(), latestWeightsPath, StringComparison.OrdinalIgnoreCase))
            {
                context.SetLastAutoAppliedTrainingWeightsPath(latestWeightsPath);
                context.AppendLog($"새 학습 모델 후보 등록: {latestWeightsPath} / baseline={application.BaselineWeightsPath} / {comparison.MetricsStatusText} / 모델 비교 후 저장 필요");
            }

            return true;
        }

        internal WpfTrainingWeightsComparison BuildCurrentTrainingWeightsComparison()
        {
            if (context.BuildCurrentTrainingWeightsComparison != null)
            {
                return context.BuildCurrentTrainingWeightsComparison();
            }

            context.EnsureProjectSettings();
            PythonModelSettings settings = projectData.ProjectSettings.PythonModel;
            return context.ModelCenterDashboardWorkflowService.BuildComparison(
                projectData,
                settings.WeightsPath,
                context.PendingTrainingBaselineWeightsPathProvider?.Invoke());
        }

        internal void UpdateTrainingComparisonViewModel(WpfTrainingWeightsComparison comparison, string comparisonStatusText = null)
        {
            if (LearningWorkflowViewModel == null)
            {
                return;
            }

            TrainingComparisonPresentation presentation = TrainingComparisonPresentationService.Build(comparison);
            comparisonStatusText ??= presentation.StatusText;
            LearningWorkflowViewModel.SetTrainingComparisonResultTexts(
                summaryText: presentation.SummaryText,
                comparisonText: comparisonStatusText,
                adoptionDecisionText: presentation.AdoptionDecisionText);
            LearningWorkflowViewModel.SetTrainingResultReportItems(presentation.ResultReportItems);
            UpdateCandidateModelComparisonReviewPanel(comparison);
        }

        internal void UpdateCandidateModelComparisonReviewPanel(WpfTrainingWeightsComparison comparison = null)
        {
            if (CandidateReviewViewModel == null)
            {
                return;
            }

            comparison ??= BuildCurrentTrainingWeightsComparison();
            IReadOnlyList<string> classNames = projectData?.ClassNamedList == null
                ? Array.Empty<string>()
                : projectData.ClassNamedList
                    .Select(item => item?.Text ?? string.Empty)
                    .ToList();
            double confidence = projectData?.ProjectSettings?.PythonModel?.MinimumDetectionConfidence ?? 0.25D;
            CandidateReviewViewModel.SetModelComparisonSourceText(
                InferenceStatusPresentationService.BuildModelComparisonSourceText(
                    projectData?.ProjectSettings?.PythonModel,
                    comparison?.CurrentWeightsPath,
                    comparison?.LatestWeightsPath));
            WpfModelComparisonHistoryItem historyItem = RefreshHistoryItems(
                comparison?.CurrentWeightsPath,
                comparison?.LatestWeightsPath);
            WpfModelComparisonReviewReport report = historyItem == null
                ? WpfModelComparisonReviewReport.Empty
                : BuildHistoryReport(historyItem);
            CandidateReviewViewModel.SetModelComparisonReview(
                report,
                isHistoricalSelection: historyItem?.IsLatest == false);
            context.UpdateCandidateModelDecisionPanel(comparison);
        }

        internal ModelComparisonCallbacks CreateCallbacks()
        {
            return new ModelComparisonCallbacks
            {
                Prepare = refreshReadiness =>
                {
                    context.EnsureProjectSettings();
                    context.SaveTrainingEditorFields();
                    if (refreshReadiness)
                    {
                        context.RefreshTrainingReadinessPanel(true);
                    }
                },
                ModelEngine = () => projectData.ProjectSettings.PythonModel.ModelEngine,
                DatasetPurpose = () => projectData.ProjectSettings.DatasetPurpose,
                HasSegmentationSettings = () => TrainingSettingsViewModel != null,
                RefreshCommands = context.RefreshCommands,
                SetComparisonResult = (summary, comparison, decision) =>
                    LearningWorkflowViewModel.SetTrainingComparisonResultTexts(summary, comparison, decision),
                AdoptionDecisionText = () => LearningWorkflowViewModel.TrainingModelAdoptionDecisionText,
                RefreshCandidateComparison = () =>
                {
                    WpfTrainingWeightsComparison comparison = BuildCurrentTrainingWeightsComparison();
                    UpdateTrainingComparisonViewModel(
                        comparison,
                        TrainingComparisonPresentationService.BuildComparisonStatusText(comparison));
                },
                RefreshHistory = RefreshHistoryItems,
                SetComparisonSource = text => CandidateReviewViewModel.SetModelComparisonSourceText(text),
                ApplyReview = (report, historical) =>
                    CandidateReviewViewModel.SetModelComparisonReview(report, isHistoricalSelection: historical),
                ClearCandidateDecision = () =>
                    CandidateReviewViewModel.SetModelCandidateDecisionState(false, false, null, null, null, null),
                AddReviewHistory = text => CandidateReviewViewModel?.AddReviewHistory(text),
                ShowReview = context.ShowCandidateReviewWorkflowView,
                SetCommandStatus = context.SetCommandStatus,
                AppendLog = context.AppendLog,
                SetSegmentationResult = (running, status, detail, action) =>
                    TrainingSettingsViewModel.SetSegmentationAdapterComparisonExecutionState(
                        running,
                        status,
                        detail,
                        action)
            };
        }

        internal WpfModelComparisonHistoryItem RefreshHistoryItems(
            string baselineWeightsPath,
            string candidateWeightsPath,
            string preferredSummaryPath = "")
        {
            if (string.IsNullOrWhiteSpace(baselineWeightsPath)
                || string.IsNullOrWhiteSpace(candidateWeightsPath))
            {
                CandidateReviewViewModel?.SetModelComparisonHistory(
                    Array.Empty<WpfModelComparisonHistoryItem>());
                return null;
            }

            IReadOnlyList<WpfModelComparisonHistoryItem> items = context.ModelComparisonReviewService.BuildHistory(
                baselineWeightsPath,
                candidateWeightsPath,
                maxItems: 8);
            CandidateReviewViewModel?.SetModelComparisonHistory(items, preferredSummaryPath);
            return CandidateReviewViewModel?.SelectedModelComparisonHistoryItem;
        }

        internal WpfModelComparisonReviewReport BuildHistoryReport(WpfModelComparisonHistoryItem item)
        {
            if (item == null)
            {
                return WpfModelComparisonReviewReport.Empty;
            }

            IReadOnlyList<string> classNames = projectData?.ClassNamedList == null
                ? Array.Empty<string>()
                : projectData.ClassNamedList
                    .Select(classItem => classItem?.Text ?? string.Empty)
                    .ToList();
            double confidence = projectData?.ProjectSettings?.PythonModel?.MinimumDetectionConfidence ?? 0.25D;
            return context.ModelComparisonReviewService.BuildFromSummaryFile(
                item.SourcePath,
                classNames,
                confidence,
                maxExamples: 5);
        }

        internal void ApplyHistorySelectionEffects(WpfModelComparisonHistoryItem item)
        {
            WpfModelComparisonReviewReport report = BuildHistoryReport(item);
            if (!report.HasComparison)
            {
                context.AppendLog($"모델 비교 이력 불러오기 실패: {item.SourcePath}");
                return;
            }

            CandidateReviewViewModel?.SetModelComparisonSourceText(
                $"{item.DisplayText} / {item.DetailText} / {item.SourcePath}");
            CandidateReviewViewModel?.SetModelComparisonReview(
                report,
                isHistoricalSelection: !item.IsLatest);
            try
            {
                context.RefreshModelCenterDashboard(BuildCurrentTrainingWeightsComparison());
            }
            catch (Exception ex)
            {
                context.AppendLog($"모델 비교 이력 판단 갱신 실패: {ex.Message}");
            }

            context.AppendLog($"모델 비교 이력 선택: {item.DisplayText}");
        }
    }

    internal sealed class ModelComparisonWorkflowAdapterContext
    {
        internal Func<LabelingProjectData> DataProvider { get; init; }
        internal ModelComparisonRunService ModelComparisonRunService { get; init; }
        internal SegmentationAdapterComparisonRunService SegmentationAdapterComparisonRunService { get; init; }
        internal ModelComparisonReviewService ModelComparisonReviewService { get; init; }
        internal ModelCenterDashboardWorkflowService ModelCenterDashboardWorkflowService { get; init; }
        internal TrainingWeightsService TrainingWeightsService { get; init; }
        internal TrainingWeightsApplicationWorkflowService TrainingWeightsApplicationWorkflowService { get; init; }
        internal Func<WpfTrainingSettingsPanelViewModel> TrainingSettingsViewModelProvider { get; init; }
        internal Func<WpfLearningWorkflowPanelViewModel> LearningWorkflowViewModelProvider { get; init; }
        internal Func<WpfCandidateReviewPanelViewModel> CandidateReviewViewModelProvider { get; init; }
        internal Func<string> PendingTrainingBaselineWeightsPathProvider { get; init; }
        internal Func<string> LastAutoAppliedTrainingWeightsPathProvider { get; init; }
        internal Func<WpfTrainingWeightsComparison> BuildCurrentTrainingWeightsComparison { get; init; }
        internal Action EnsureProjectSettings { get; init; }
        internal Action SaveTrainingEditorFields { get; init; }
        internal Action<bool> RefreshTrainingReadinessPanel { get; init; }
        internal Action RefreshCommands { get; init; }
        internal Action<WpfTrainingWeightsComparison> RefreshModelCenterDashboard { get; init; }
        internal Action<WpfTrainingWeightsComparison> UpdateCandidateModelDecisionPanel { get; init; }
        internal Action ShowCandidateReviewWorkflowView { get; init; }
        internal Action<string, bool> SetCommandStatus { get; init; }
        internal Action<string> AppendLog { get; init; }
        internal Action<PythonModelSettings> LoadYoloSettings { get; init; }
        internal Action<string> SetPendingTrainingBaselineWeightsPath { get; init; }
        internal Action<bool> SetHasPendingTrainingWeightsRecipeSave { get; init; }
        internal Action<string> SetLastAutoAppliedTrainingWeightsPath { get; init; }
        internal Action<string> SetModelStatus { get; init; }
        internal Action<string> SetProjectConfigStatus { get; init; }
        internal Action<string, bool> SetGlobalInferenceStatus { get; init; }
        internal Action FocusYoloModelSettingsTab { get; init; }
        internal Action FocusSaveYoloSettingsButton { get; init; }
    }
}
