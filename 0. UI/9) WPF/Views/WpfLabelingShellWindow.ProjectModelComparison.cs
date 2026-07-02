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
