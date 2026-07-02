using MvcVisionSystem._1._Core;
using System;
using System.IO;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        private void ExecutePromoteSelectedModelHistoryCommand()
        {
            try
            {
                EnsureProjectSettings();
                WpfModelRegistryHistoryItem selected = ShellViewModel?.SelectedModelRegistryHistoryItem;
                if (selected == null)
                {
                    SetYoloCommandStatus("\uBAA8\uB378 \uC774\uB825\uC744 \uC120\uD0DD\uD558\uC138\uC694.", isBusy: false);
                    return;
                }

                string candidateWeightsPath = selected.WeightsPath?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(candidateWeightsPath))
                {
                    SetModelCenterHistoryApplyFailure(
                        "\uBAA8\uB378 \uC774\uB825 \uC801\uC6A9 \uBD88\uAC00",
                        "\uC120\uD0DD\uD55C \uBAA8\uB378 \uC774\uB825\uC5D0 \uAC00\uC911\uCE58 \uD30C\uC77C \uACBD\uB85C\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.");
                    return;
                }

                if (!File.Exists(candidateWeightsPath))
                {
                    SetModelCenterHistoryApplyFailure(
                        "\uBAA8\uB378 \uC774\uB825 \uC801\uC6A9 \uBD88\uAC00",
                        $"\uC120\uD0DD\uD55C \uBAA8\uB378 \uD30C\uC77C\uC774 \uC5C6\uC2B5\uB2C8\uB2E4: {candidateWeightsPath}");
                    return;
                }

                PythonModelSettings settings = global.Data.ProjectSettings.PythonModel;
                string previousWeightsPath = settings.WeightsPath?.Trim() ?? string.Empty;
                if (string.Equals(previousWeightsPath, candidateWeightsPath, StringComparison.OrdinalIgnoreCase))
                {
                    SetYoloCommandStatus($"\uC774\uBBF8 \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378\uC785\uB2C8\uB2E4: {Path.GetFileName(candidateWeightsPath)}", isBusy: false);
                    RefreshModelCenterDashboard();
                    return;
                }

                string baselineWeightsPath = !string.IsNullOrWhiteSpace(previousWeightsPath)
                    ? previousWeightsPath
                    : selected.BaselineWeightsPath?.Trim() ?? string.Empty;
                string decisionSummary = "\uBAA8\uB378 \uC774\uB825\uC5D0\uC11C \uAC80\uC0AC \uBAA8\uB378\uB85C \uC801\uC6A9";
                string metricsSummary = !string.IsNullOrWhiteSpace(selected.MetricText)
                    ? selected.MetricText
                    : selected.DecisionText ?? string.Empty;

                settings.WeightsPath = candidateWeightsPath;
                YoloModelSettingsViewModel?.LoadFrom(settings);
                ModelRegistryService.RecordCandidateDecision(
                    global.Data.ProjectSettings.ModelRegistry,
                    settings,
                    global.Data.ProjectSettings.DatasetPurpose,
                    global.Data.OutputRootPath,
                    candidateWeightsPath,
                    baselineWeightsPath,
                    metricsSummary,
                    ModelRegistryService.CandidateDecisionAdopted,
                    decisionSummary,
                    savedToRecipe: true);

                hasPendingTrainingWeightsRecipeSave = false;
                pendingTrainingBaselineWeightsPath = string.Empty;
                lastAutoAppliedTrainingWeightsPath = candidateWeightsPath;
                bool configSaved = SaveProjectConfigFromPanel();

                PopulateYoloEditorFields();
                RefreshYoloStatus();
                UpdateYoloTrainingHistoryText();
                RefreshModelCenterDashboard();
                UpdateCandidateModelDecisionPanel();

                string modelName = Path.GetFileName(candidateWeightsPath);
                if (configSaved)
                {
                    ShellViewModel?.ClearModelCenterRecoveryState();
                    SetModelStatus($"\uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378: {modelName}");
                    SetYoloCommandStatus($"\uBAA8\uB378 \uC774\uB825 \uC801\uC6A9 \uC644\uB8CC: {modelName}. \uB2E4\uC74C \uAC80\uC0AC\uBD80\uD130 \uC774 \uBAA8\uB378\uC744 \uC0AC\uC6A9\uD569\uB2C8\uB2E4.", isBusy: false);
                    AppendLog($"Model history adopted as inspection model: {candidateWeightsPath} / previous={baselineWeightsPath}");
                    return;
                }

                hasPendingTrainingWeightsRecipeSave = true;
                pendingTrainingBaselineWeightsPath = baselineWeightsPath;
                SetModelCenterHistoryApplyFailure(
                    "\uBAA8\uB378 \uC774\uB825 \uC801\uC6A9\uC740 \uBA54\uBAA8\uB9AC\uC5D0\uB9CC \uBC18\uC601\uB428",
                    "\uC120\uD0DD\uD55C \uBAA8\uB378\uC744 recipe\uC5D0 \uC800\uC7A5\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4. \uC800\uC7A5 \uACBD\uB85C\uC640 recipe \uC774\uB984\uC744 \uD655\uC778\uD55C \uB4A4 \uBAA8\uB378 \uC124\uC815\uC744 \uC800\uC7A5\uD558\uC138\uC694.");
            }
            catch (Exception ex)
            {
                SetModelCenterHistoryApplyFailure(
                    "\uBAA8\uB378 \uC774\uB825 \uC801\uC6A9 \uC2E4\uD328",
                    ex.Message);
            }
        }

        private void SetModelCenterHistoryApplyFailure(string titleText, string detailText)
        {
            ShellViewModel?.SetModelCenterRecoveryState(
                titleText,
                detailText,
                "\uBAA8\uB378 \uC774\uB825\uC758 \uD30C\uC77C \uACBD\uB85C\uC640 recipe \uC800\uC7A5 \uC0C1\uD0DC\uB97C \uD655\uC778\uD558\uC138\uC694.");
            SetYoloCommandStatus($"{titleText}: {detailText}", isBusy: false);
            AppendLog($"{titleText}: {detailText}");
            RefreshModelCenterDashboard();
        }
    }
}
