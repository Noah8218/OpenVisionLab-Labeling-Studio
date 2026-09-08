using MvcVisionSystem._1._Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    // Responsibility group: model comparison and training weight presentation.
    // These members remain WPF Window adapters; independent policy belongs in services.
    public partial class WpfLabelingShellWindow
    {
        #region ProjectModelComparison
        // PL-0038: keep live project inputs and UI adapters here; execution/state has one owner.
        private ModelComparisonWorkflowService CreateModelComparisonWorkflow()
        {
            return new ModelComparisonWorkflowService(
                () => modelComparisonRunService.BuildRequest(
                    global.Data,
                    trainingWeightsService,
                    task: "test",
                    baselineWeightsOverride: ModelCenterDashboardWorkflowService.ResolveComparisonCurrentWeightsPath(
                        global.Data.ProjectSettings.PythonModel.WeightsPath, pendingTrainingBaselineWeightsPath)),
                compareYoloV8ToYolo11 => compareYoloV8ToYolo11
                    ? modelComparisonRunService.BuildYoloV8Yolo11DetectionRequest(global.Data)
                    : modelComparisonRunService.BuildYoloV5YoloV8DetectionRequest(global.Data),
                () => segmentationAdapterComparisonRunService.BuildRequest(
                    global.Data,
                    global.Data.ProjectSettings.PythonModel,
                    TrainingSettingsViewModel.SegmentationUnetWeightsPath,
                    TrainingSettingsViewModel.SegmentationYoloWeightsPath,
                    TrainingSettingsViewModel.SelectedSegmentationYoloEngine),
                modelComparisonRunService.ValidateRequest,
                segmentationAdapterComparisonRunService.ValidateRequest,
                modelComparisonRunService.RunAsync,
                segmentationAdapterComparisonRunService.RunAsync,
                (result, request) => modelComparisonReviewService.BuildFromSummaryFile(
                    result.SummaryPath,
                    global.Data.ClassNamedList?.Select(item => item?.Text ?? string.Empty).ToList() ?? new List<string>(),
                    request.UiConfidence,
                    maxExamples: 5));
        }

        private ModelComparisonCallbacks CreateModelComparisonCallbacks()
        {
            return new ModelComparisonCallbacks
            {
                Prepare = refreshReadiness =>
                {
                    EnsureProjectSettings();
                    SaveTrainingEditorFields();
                    if (refreshReadiness) RefreshTrainingReadinessPanel(refreshYaml: true);
                },
                ModelEngine = () => global.Data.ProjectSettings.PythonModel.ModelEngine,
                DatasetPurpose = () => global.Data.ProjectSettings.DatasetPurpose,
                HasSegmentationSettings = () => TrainingSettingsViewModel != null,
                RefreshCommands = UpdateYoloCommandButtons,
                SetComparisonResult = (summary, comparison, decision) => LearningWorkflowViewModel.SetTrainingComparisonResultTexts(summary, comparison, decision),
                AdoptionDecisionText = () => LearningWorkflowViewModel.TrainingModelAdoptionDecisionText,
                RefreshCandidateComparison = () =>
                {
                    WpfTrainingWeightsComparison comparison = BuildCurrentTrainingWeightsComparison();
                    UpdateTrainingComparisonViewModel(comparison, TrainingComparisonPresentationService.BuildComparisonStatusText(comparison));
                    UpdateCandidateModelComparisonReviewPanel(comparison);
                },
                RefreshHistory = RefreshModelComparisonHistoryItems,
                SetComparisonSource = text => CandidateReviewViewModel.SetModelComparisonSourceText(text),
                ApplyReview = (report, historical) => CandidateReviewViewModel.SetModelComparisonReview(report, isHistoricalSelection: historical),
                ClearCandidateDecision = () => CandidateReviewViewModel.SetModelCandidateDecisionState(false, false, null, null, null, null),
                AddReviewHistory = text => CandidateReviewViewModel?.AddReviewHistory(text),
                ShowReview = ShowCandidateReviewWorkflowView,
                SetCommandStatus = SetYoloCommandStatus,
                AppendLog = AppendLog,
                SetSegmentationResult = (running, status, detail, action) => TrainingSettingsViewModel.SetSegmentationAdapterComparisonExecutionState(running, status, detail, action)
            };
        }

        private void ExecuteRunModelComparisonCommand()
        {
            _ = ExecuteRunModelComparisonCommandAsync();
        }

        private Task ExecuteRunModelComparisonCommandAsync()
        {
            return modelComparisonWorkflowService.RunCandidateAsync(CreateModelComparisonCallbacks());
        }

        private void ExecuteRunYoloEngineComparisonCommand()
        {
            _ = ExecuteRunYoloEngineComparisonCommandAsync();
        }

        private Task ExecuteRunYoloEngineComparisonCommandAsync()
        {
            return modelComparisonWorkflowService.RunEngineAsync(CreateModelComparisonCallbacks());
        }

        // Historical comparison loading is part of the same model-comparison
        // call path, so its review-history projection stays with this owner.
        private WpfModelComparisonHistoryItem RefreshModelComparisonHistoryItems(
            string baselineWeightsPath,
            string candidateWeightsPath,
            string preferredSummaryPath = "")
        {
            if (string.IsNullOrWhiteSpace(baselineWeightsPath)
                || string.IsNullOrWhiteSpace(candidateWeightsPath))
            {
                CandidateReviewViewModel.SetModelComparisonHistory(Array.Empty<WpfModelComparisonHistoryItem>());
                return null;
            }

            IReadOnlyList<WpfModelComparisonHistoryItem> items = modelComparisonReviewService.BuildHistory(
                baselineWeightsPath,
                candidateWeightsPath,
                maxItems: 8);
            CandidateReviewViewModel.SetModelComparisonHistory(items, preferredSummaryPath);
            return CandidateReviewViewModel.SelectedModelComparisonHistoryItem;
        }

        private WpfModelComparisonReviewReport BuildModelComparisonHistoryReport(
            WpfModelComparisonHistoryItem item)
        {
            if (item == null)
            {
                return WpfModelComparisonReviewReport.Empty;
            }

            IReadOnlyList<string> classNames = global.Data?.ClassNamedList == null
                ? Array.Empty<string>()
                : global.Data.ClassNamedList
                    .Select(classItem => classItem?.Text ?? string.Empty)
                    .ToList();
            double confidence = global.Data?.ProjectSettings?.PythonModel?.MinimumDetectionConfidence ?? 0.25D;
            return modelComparisonReviewService.BuildFromSummaryFile(
                item.SourcePath,
                classNames,
                confidence,
                maxExamples: 5);
        }

        private void ExecuteModelComparisonHistorySelectionChangedCommand(object selectedItem)
        {
            if (selectedItem is not WpfModelComparisonHistoryItem item)
            {
                return;
            }

            WpfModelComparisonReviewReport report = BuildModelComparisonHistoryReport(item);
            if (!report.HasComparison)
            {
                AppendLog($"\uBAA8\uB378 \uBE44\uAD50 \uC774\uB825 \uBD88\uB7EC\uC624\uAE30 \uC2E4\uD328: {item.SourcePath}");
                return;
            }

            CandidateReviewViewModel.SetModelComparisonSourceText(
                $"{item.DisplayText} / {item.DetailText} / {item.SourcePath}");
            CandidateReviewViewModel.SetModelComparisonReview(
                report,
                isHistoricalSelection: !item.IsLatest);
            try
            {
                RefreshModelCenterDashboard(BuildCurrentTrainingWeightsComparison());
            }
            catch (Exception ex)
            {
                AppendLog($"\uBAA8\uB378 \uBE44\uAD50 \uC774\uB825 \uD310\uB2E8 \uAC31\uC2E0 \uC2E4\uD328: {ex.Message}");
            }

            AppendLog($"\uBAA8\uB378 \uBE44\uAD50 \uC774\uB825 \uC120\uD0DD: {item.DisplayText}");
        }
        #endregion

        #region ProjectTrainingWeights
        // The workflow service stages data-side state; the Shell only presents it
        // and keeps the explicit Recipe save/decision boundary.
        private bool TryApplyLatestTrainingWeightsFromProject(bool logIfUnchanged)
        {
            EnsureProjectSettings();
            PythonModelSettings settings = global.Data.ProjectSettings.PythonModel;
            TrainingWeightsApplicationResult application = trainingWeightsApplicationWorkflowService.StageLatestCandidate(
                new TrainingWeightsApplicationRequest
                {
                    Data = global.Data,
                    CurrentWeightsPath = settings.WeightsPath
                });
            WpfTrainingWeightsComparison comparison = application.Comparison;
            string comparisonStatusText = TrainingComparisonPresentationService
                .BuildComparisonStatusText(comparison);
            if (LearningWorkflowViewModel != null)
            {
                UpdateTrainingComparisonViewModel(comparison, comparisonStatusText);
            }
            RefreshModelCenterDashboard(comparison);

            if (application.Status == TrainingWeightsApplicationStatus.NoCandidate)
            {
                if (logIfUnchanged)
                {
                    SetYoloCommandStatus($"{comparisonStatusText}. 모델 설정에서 학습 결과 모델을 직접 선택하세요.", isBusy: false);
                    AppendLog("학습 결과 모델 후보를 찾지 못했습니다.");
                }

                return false;
            }

            if (application.Status == TrainingWeightsApplicationStatus.AlreadyCurrent)
            {
                if (logIfUnchanged)
                {
                    SetYoloCommandStatus(comparisonStatusText, isBusy: false);
                    AppendLog($"현재 검사 모델 유지: {application.CandidateWeightsPath}");
                }

                return false;
            }

            if (application.Status == TrainingWeightsApplicationStatus.NotNewer)
            {
                if (logIfUnchanged)
                {
                    SetYoloCommandStatus(comparisonStatusText, isBusy: false);
                    AppendLog($"현재 검사 모델 유지: {settings.WeightsPath}");
                }

                return false;
            }

            string latestWeightsPath = application.CandidateWeightsPath;
            pendingTrainingBaselineWeightsPath = application.BaselineWeightsPath;
            YoloModelSettingsViewModel?.LoadFrom(settings);
            string latestDisplayName = TrainingWeightsService.FormatWeightsDisplayPath(latestWeightsPath);
            SetModelStatus($"모델 후보: {Path.GetFileName(latestWeightsPath)}");
            hasPendingTrainingWeightsRecipeSave = true;
            RefreshModelCenterDashboard(comparison);
            SetGlobalInferenceStatus(string.Empty, isBusy: false);
            SetModelStatus($"모델 후보: {latestDisplayName}");
            FocusYoloModelSettingsTab();
            SaveYoloSettingsButton?.Focus();
            SetProjectConfigStatus("새 학습 모델 후보를 검사 모델 설정에 올렸습니다. 모델 비교 후 저장하면 프로젝트에 반영됩니다.");
            SetYoloCommandStatus($"새 학습 모델 후보: {Path.GetFileName(latestWeightsPath)} / {comparison.MetricsStatusText} / 모델 비교 후 저장 필요", isBusy: false);

            SetYoloCommandStatus($"현재 데이터셋 학습 완료: {latestDisplayName} / {comparison.MetricsStatusText} / 모델 비교 및 저장 필요", isBusy: false);

            if (!string.Equals(lastAutoAppliedTrainingWeightsPath, latestWeightsPath, StringComparison.OrdinalIgnoreCase))
            {
                lastAutoAppliedTrainingWeightsPath = latestWeightsPath;
                AppendLog($"새 학습 모델 후보 등록: {latestWeightsPath} / baseline={pendingTrainingBaselineWeightsPath} / {comparison.MetricsStatusText} / 모델 비교 후 저장 필요");
            }

            return true;
        }

        private WpfTrainingWeightsComparison BuildCurrentTrainingWeightsComparison()
        {
            EnsureProjectSettings();
            PythonModelSettings settings = global.Data.ProjectSettings.PythonModel;
            return modelCenterDashboardWorkflowService.BuildComparison(
                global.Data,
                settings.WeightsPath,
                pendingTrainingBaselineWeightsPath);
        }

        private void UpdateTrainingComparisonViewModel(WpfTrainingWeightsComparison comparison, string comparisonStatusText = null)
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

        private void UpdateCandidateModelComparisonReviewPanel(WpfTrainingWeightsComparison comparison = null)
        {
            if (CandidateReviewViewModel == null)
            {
                return;
            }

            comparison ??= BuildCurrentTrainingWeightsComparison();
            IReadOnlyList<string> classNames = global.Data?.ClassNamedList == null
                ? Array.Empty<string>()
                : global.Data.ClassNamedList
                    .Select(item => item?.Text ?? string.Empty)
                    .ToList();
            double confidence = global.Data?.ProjectSettings?.PythonModel?.MinimumDetectionConfidence ?? 0.25D;
            CandidateReviewViewModel.SetModelComparisonSourceText(
                InferenceStatusPresentationService.BuildModelComparisonSourceText(
                    global.Data?.ProjectSettings?.PythonModel,
                    comparison?.CurrentWeightsPath,
                    comparison?.LatestWeightsPath));
            // The latest matching artifact remains authoritative; older matching runs are read-only history.
            WpfModelComparisonHistoryItem historyItem = RefreshModelComparisonHistoryItems(
                comparison?.CurrentWeightsPath,
                comparison?.LatestWeightsPath);
            WpfModelComparisonReviewReport report = historyItem == null
                ? WpfModelComparisonReviewReport.Empty
                : modelComparisonReviewService.BuildFromSummaryFile(
                    historyItem.SourcePath,
                    classNames,
                    confidence,
                    maxExamples: 5);
            CandidateReviewViewModel.SetModelComparisonReview(
                report,
                isHistoricalSelection: historyItem?.IsLatest == false);
            UpdateCandidateModelDecisionPanel(comparison);
        }
        #endregion

    }
}
