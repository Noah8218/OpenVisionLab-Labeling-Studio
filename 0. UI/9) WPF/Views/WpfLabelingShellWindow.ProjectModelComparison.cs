using MvcVisionSystem._1._Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        private async void ExecuteRunModelComparisonCommand()
        {
            if (isModelComparisonRunning)
            {
                return;
            }

            EnsureProjectSettings();
            SaveTrainingEditorFields();
            RefreshTrainingReadinessPanel(refreshYaml: true);

            WpfModelComparisonRunRequest request = modelComparisonRunService.BuildRequest(
                global.Data,
                trainingWeightsService,
                task: "test",
                baselineWeightsOverride: GetTrainingComparisonCurrentWeightsPath(global.Data.ProjectSettings.PythonModel.WeightsPath));
            IReadOnlyList<string> validationErrors = modelComparisonRunService.ValidateRequest(request);
            if (validationErrors.Count > 0)
            {
                string message = "\uBAA8\uB378 \uBE44\uAD50 \uC2E4\uD589 \uBD88\uAC00: " + string.Join(" / ", validationErrors.Take(3));
                LearningWorkflowViewModel.TrainingResultComparisonText = message;
                LearningWorkflowViewModel.TrainingModelAdoptionDecisionText = "\uAD50\uCCB4 \uD310\uB2E8: \uBCF4\uB958 - \uCD5C\uC885 \uAC80\uC99D \uBE44\uAD50 \uBD88\uAC00";
                SetYoloCommandStatus(message, isBusy: false);
                AppendLog(message);
                return;
            }

            isModelComparisonRunning = true;
            UpdateYoloCommandButtons();
            LearningWorkflowViewModel.TrainingResultComparisonText = "\uBAA8\uB378 \uBE44\uAD50 \uC2E4\uD589 \uC911: \uCD5C\uC885 \uAC80\uC99D \uC774\uBBF8\uC9C0\uB85C \uAE30\uC874 \uBAA8\uB378\uACFC \uC0C8 \uD559\uC2B5 \uBAA8\uB378\uC744 \uBE44\uAD50\uD569\uB2C8\uB2E4.";
            LearningWorkflowViewModel.TrainingModelAdoptionDecisionText = "\uAD50\uCCB4 \uD310\uB2E8: \uBE44\uAD50 \uC2E4\uD589 \uC911";
            SetYoloCommandStatus("\uBAA8\uB378 \uBE44\uAD50 \uC2E4\uD589 \uC911...", isBusy: true);
            AppendLog($"\uBAA8\uB378 \uBE44\uAD50 \uC2DC\uC791: \uAE30\uC874={Path.GetFileName(request.BaselineWeightsPath)}, \uC0C8 \uBAA8\uB378={Path.GetFileName(request.CandidateWeightsPath)}, \uB300\uC0C1={request.Task}");

            try
            {
                WpfModelComparisonRunResult result = await modelComparisonRunService
                    .RunAsync(request)
                    .ConfigureAwait(true);

                if (!result.Succeeded)
                {
                    string errorText = BuildModelComparisonFailureText(result);
                    LearningWorkflowViewModel.TrainingResultComparisonText = errorText;
                    LearningWorkflowViewModel.TrainingModelAdoptionDecisionText = "\uAD50\uCCB4 \uD310\uB2E8: \uBCF4\uB958 - \uBAA8\uB378 \uBE44\uAD50 \uC2E4\uD328";
                    SetYoloCommandStatus(errorText, isBusy: false);
                    AppendLog(errorText);
                    return;
                }

                WpfTrainingWeightsComparison comparison = trainingWeightsService.BuildComparison(
                    global.Data.ProjectSettings.PythonModel.ProjectRootPath,
                    global.Data.OutputRootPath,
                    GetTrainingComparisonCurrentWeightsPath(global.Data.ProjectSettings.PythonModel.WeightsPath));
                UpdateTrainingComparisonViewModel(comparison, BuildTrainingComparisonStatusText(comparison));
                UpdateCandidateModelComparisonReviewPanel(comparison);

                string summaryName = string.IsNullOrWhiteSpace(result.SummaryPath)
                    ? "comparison-summary.json"
                    : Path.GetFileName(Path.GetDirectoryName(result.SummaryPath) ?? result.SummaryPath);
                string completeText = $"\uBAA8\uB378 \uBE44\uAD50 \uC644\uB8CC: {summaryName}. Candidate Review\uC758 \uBAA8\uB378 \uCC28\uC774 \uC608\uC2DC\uB97C \uD074\uB9AD\uD574 \uC774\uBBF8\uC9C0 \uC704\uCE58\uB97C \uD655\uC778\uD558\uC138\uC694.";
                LearningWorkflowViewModel.TrainingResultComparisonText = completeText;
                if (!LearningWorkflowViewModel.TrainingModelAdoptionDecisionText.Contains("\uAD50\uCCB4 \uD310\uB2E8:", StringComparison.Ordinal))
                {
                    LearningWorkflowViewModel.TrainingModelAdoptionDecisionText = "\uAD50\uCCB4 \uD310\uB2E8: \uCC28\uC774 \uC608\uC2DC \uD655\uC778 \uD544\uC694";
                }

                CandidateReviewViewModel?.AddReviewHistory(completeText);
                ShowCandidateReviewWorkflowView();
                SetYoloCommandStatus(completeText, isBusy: false);
                AppendLog(completeText);
            }
            catch (Exception ex)
            {
                string errorText = $"\uBAA8\uB378 \uBE44\uAD50 \uC2E4\uD328: {ex.Message}";
                LearningWorkflowViewModel.TrainingResultComparisonText = errorText;
                LearningWorkflowViewModel.TrainingModelAdoptionDecisionText = "\uAD50\uCCB4 \uD310\uB2E8: \uBCF4\uB958 - \uBAA8\uB378 \uBE44\uAD50 \uC2E4\uD328";
                SetYoloCommandStatus(errorText, isBusy: false);
                AppendLog(errorText);
            }
            finally
            {
                isModelComparisonRunning = false;
                UpdateYoloCommandButtons();
            }
        }

        private async void ExecuteRunYoloEngineComparisonCommand()
        {
            if (isModelComparisonRunning)
            {
                return;
            }

            EnsureProjectSettings();
            SaveTrainingEditorFields();
            RefreshTrainingReadinessPanel(refreshYaml: true);
            if (global.Data.ProjectSettings.DatasetPurpose != LabelingDatasetPurpose.ObjectDetection)
            {
                const string purposeError = "YOLOv5/YOLOv8 \uBD84\uC11D\uC740 \uAC1D\uCCB4\uD0D0\uC9C0 \uB370\uC774\uD130\uC14B\uC5D0\uC11C\uB9CC \uC2E4\uD589\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.";
                LearningWorkflowViewModel.TrainingResultComparisonText = purposeError;
                SetYoloCommandStatus(purposeError, isBusy: false);
                AppendLog(purposeError);
                return;
            }

            WpfModelComparisonRunRequest request = modelComparisonRunService
                .BuildYoloV5YoloV8DetectionRequest(global.Data);
            string comparisonBasisText = string.Equals(request.Task, "val", StringComparison.OrdinalIgnoreCase)
                ? "\uD559\uC2B5 \uAC80\uC99D(val, \uAD50\uCCB4 \uD310\uB2E8 \uC544\uB2D8)"
                : "\uCD5C\uC885 \uAC80\uC99D(test)";
            IReadOnlyList<string> validationErrors = modelComparisonRunService.ValidateRequest(request);
            if (validationErrors.Count > 0)
            {
                string message = "YOLOv5/YOLOv8 \uBD84\uC11D \uC2E4\uD589 \uBD88\uAC00: " + string.Join(" / ", validationErrors.Take(4));
                LearningWorkflowViewModel.TrainingResultComparisonText = message;
                LearningWorkflowViewModel.TrainingModelAdoptionDecisionText = "\uC5D4\uC9C4 \uBE44\uAD50: \uC900\uBE44 \uD544\uC694";
                SetYoloCommandStatus(message, isBusy: false);
                AppendLog(message);
                return;
            }

            isModelComparisonRunning = true;
            UpdateYoloCommandButtons();
            LearningWorkflowViewModel.TrainingResultComparisonSummaryText = "YOLOv5 vs YOLOv8 \uAC1D\uCCB4\uD0D0\uC9C0 \uBD84\uC11D \uC911";
            LearningWorkflowViewModel.TrainingResultComparisonText = $"\uB3D9\uC77C\uD55C {comparisonBasisText} \uC774\uBBF8\uC9C0\uC5D0\uC11C \uC815\uD655\uB3C4\uC640 \uBAA8\uB378 Takt\uB97C \uCE21\uC815\uD558\uB294 \uC911\uC785\uB2C8\uB2E4.";
            LearningWorkflowViewModel.TrainingModelAdoptionDecisionText = "\uC5D4\uC9C4 \uBE44\uAD50: \uC2E4\uD589 \uC911";
            SetYoloCommandStatus("YOLOv5/YOLOv8 \uAC1D\uCCB4\uD0D0\uC9C0 \uBD84\uC11D \uC911...", isBusy: true);
            AppendLog($"YOLO \uC5D4\uC9C4 \uBE44\uAD50 \uC2DC\uC791: YOLOv5={Path.GetFileName(request.BaselineWeightsPath)}, YOLOv8={Path.GetFileName(request.CandidateWeightsPath)}, batch=1, task={request.Task}");

            try
            {
                WpfModelComparisonRunResult result = await modelComparisonRunService
                    .RunAsync(request)
                    .ConfigureAwait(true);
                if (!result.Succeeded)
                {
                    string errorText = BuildModelComparisonFailureText(result);
                    LearningWorkflowViewModel.TrainingResultComparisonText = errorText;
                    LearningWorkflowViewModel.TrainingModelAdoptionDecisionText = "\uC5D4\uC9C4 \uBE44\uAD50: \uC2E4\uD328";
                    SetYoloCommandStatus(errorText, isBusy: false);
                    AppendLog(errorText);
                    return;
                }

                IReadOnlyList<string> classNames = global.Data.ClassNamedList?
                    .Select(item => item?.Text ?? string.Empty)
                    .ToList() ?? new List<string>();
                WpfModelComparisonReviewReport report = modelComparisonReviewService.BuildFromSummaryFile(
                    result.SummaryPath,
                    classNames,
                    request.UiConfidence,
                    maxExamples: 5);
                if (!report.HasComparison)
                {
                    string errorText = "YOLOv5/YOLOv8 \uBD84\uC11D \uACB0\uACFC\uB97C \uC77D\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.";
                    LearningWorkflowViewModel.TrainingResultComparisonText = errorText;
                    LearningWorkflowViewModel.TrainingModelAdoptionDecisionText = "\uC5D4\uC9C4 \uBE44\uAD50: \uACB0\uACFC \uD655\uC778 \uD544\uC694";
                    SetYoloCommandStatus(errorText, isBusy: false);
                    AppendLog(errorText);
                    return;
                }

                string sourceText = $"\uBE44\uAD50 \uB300\uC0C1: YOLOv5 / {Path.GetFileName(request.BaselineWeightsPath)} \u2194 YOLOv8 / {Path.GetFileName(request.CandidateWeightsPath)} / \uAE30\uC900: {comparisonBasisText}";
                WpfModelComparisonHistoryItem historyItem = RefreshModelComparisonHistoryItems(
                    request.BaselineWeightsPath,
                    request.CandidateWeightsPath,
                    result.SummaryPath);
                CandidateReviewViewModel.SetModelComparisonSourceText(sourceText);
                CandidateReviewViewModel.SetModelComparisonReview(
                    report,
                    isHistoricalSelection: historyItem?.IsLatest == false);
                CandidateReviewViewModel.SetModelCandidateDecisionState(false, false, null, null, null, null);
                LearningWorkflowViewModel.TrainingResultComparisonSummaryText = "YOLOv5 vs YOLOv8 \uAC1D\uCCB4\uD0D0\uC9C0 \uBD84\uC11D \uC644\uB8CC";
                LearningWorkflowViewModel.TrainingResultComparisonText = string.IsNullOrWhiteSpace(report.BenchmarkText)
                    ? report.DetailText
                    : report.BenchmarkText + Environment.NewLine + report.DetailText;
                LearningWorkflowViewModel.TrainingModelAdoptionDecisionText = string.IsNullOrWhiteSpace(report.RecommendationText)
                    ? "\uC5D4\uC9C4 \uBE44\uAD50: \uC608\uC2DC \uD655\uC778 \uD544\uC694"
                    : report.RecommendationText;

                string completeText = $"YOLOv5/YOLOv8 \uAC1D\uCCB4\uD0D0\uC9C0 \uBD84\uC11D \uC644\uB8CC ({comparisonBasisText}): Candidate Review\uC5D0\uC11C \uC815\uD655\uB3C4, \uBAA8\uB378 Takt, \uC774\uBBF8\uC9C0\uBCC4 \uCC28\uC774\uB97C \uD655\uC778\uD558\uC138\uC694.";
                CandidateReviewViewModel.AddReviewHistory(completeText);
                ShowCandidateReviewWorkflowView();
                SetYoloCommandStatus(completeText, isBusy: false);
                AppendLog(completeText);
            }
            catch (Exception ex)
            {
                string errorText = $"YOLOv5/YOLOv8 \uBD84\uC11D \uC2E4\uD328: {ex.Message}";
                LearningWorkflowViewModel.TrainingResultComparisonText = errorText;
                LearningWorkflowViewModel.TrainingModelAdoptionDecisionText = "\uC5D4\uC9C4 \uBE44\uAD50: \uC2E4\uD328";
                SetYoloCommandStatus(errorText, isBusy: false);
                AppendLog(errorText);
            }
            finally
            {
                isModelComparisonRunning = false;
                UpdateYoloCommandButtons();
            }
        }

        private static string BuildModelComparisonFailureText(WpfModelComparisonRunResult result)
        {
            string detail = result?.Error ?? string.Empty;
            if (string.IsNullOrWhiteSpace(detail))
            {
                detail = result?.Output ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(detail))
            {
                return "\uBAA8\uB378 \uBE44\uAD50 \uC2E4\uD328: \uC2E4\uD589 \uACB0\uACFC\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.";
            }

            string firstLine = detail
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault() ?? detail.Trim();
            return $"\uBAA8\uB378 \uBE44\uAD50 \uC2E4\uD328: {firstLine}";
        }
    }
}
