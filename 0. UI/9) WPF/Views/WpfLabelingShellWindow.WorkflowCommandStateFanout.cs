using MvcVisionSystem.Yolo;
using System.Windows;
using System.Windows.Controls;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        private void UpdateYoloCommandButtons()
        {
            WpfWorkflowCommandState state = WpfWorkflowCommandStateService.Build(
                isInferenceMode: currentWorkflowMode == WorkflowMode.Inference,
                isYoloEnvironmentCommandRunning: isYoloEnvironmentCommandRunning || isModelComparisonRunning,
                isDetecting: isDetecting,
                isBatchDetectionRunning: isBatchDetectionRunning,
                isTrainingCommandRunning: isTrainingCommandRunning,
                isTrainingStopAvailable: IsTrainingStopAvailable(global.GetPythonCommunicationStatusSnapshot()),
                hasCurrentRecipeName: !string.IsNullOrWhiteSpace(GetCurrentRecipeName()));

            // Keep the transitional fallback controls beside the shared state fan-out so command gating stays consistent while panels move to view models.
            ApplyYoloStatusCommandState(state);
            ApplyProjectConfigCommandState(state);
            ApplyYoloModelSettingsCommandState(state);
            ApplyTrainingSettingsCommandState(state);
            ApplyLearningWorkflowCommandState(state);
            ApplyShellCommandState(state);
            ApplyImageQueueCommandState(state);
            UpdateDetectionCommandHints(state);
            UpdateWorkflowModeUi();
        }

        private void ApplyLearningWorkflowCommandState(WpfWorkflowCommandState state)
        {
            (bool enabled, string actionText, string toolTipText, string basisText) = BuildModelComparisonRunState(state);
            LearningWorkflowViewModel?.SetModelComparisonRunState(enabled, actionText, toolTipText, basisText);
        }

        private (bool Enabled, string ActionText, string ToolTipText, string BasisText) BuildModelComparisonRunState(WpfWorkflowCommandState state)
        {
            if (isModelComparisonRunning)
            {
                return (
                    false,
                    "\uBE44\uAD50 \uC2E4\uD589 \uC911",
                    "\uCD5C\uC885 \uAC80\uC99D \uC774\uBBF8\uC9C0\uB85C \uAE30\uC874 \uBAA8\uB378\uACFC \uC0C8 \uD559\uC2B5 \uBAA8\uB378\uC744 \uBE44\uAD50\uD558\uB294 \uC911\uC785\uB2C8\uB2E4.",
                    "\uBE44\uAD50 \uAE30\uC900: \uCD5C\uC885 \uAC80\uC99D \uC774\uBBF8\uC9C0\uC5D0\uC11C \uAE30\uC874 \uBAA8\uB378\uACFC \uC0C8 \uBAA8\uB378\uC758 \uCC28\uC774\uB97C \uACC4\uC0B0 \uC911\uC785\uB2C8\uB2E4.");
            }

            if (state?.CanRunGeneralCommands != true)
            {
                return (
                    false,
                    "\uB300\uAE30",
                    "\uD604\uC7AC \uB2E4\uB978 \uBA85\uB839\uC774 \uC2E4\uD589 \uC911\uC774\uBBC0\uB85C \uC644\uB8CC \uD6C4 \uBE44\uAD50\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.",
                    "\uBE44\uAD50 \uAE30\uC900: \uC9C4\uD589 \uC911\uC778 \uBA85\uB839\uC774 \uC644\uB8CC\uB41C \uB4A4 \uCD5C\uC885 \uAC80\uC99D \uC0C1\uD0DC\uB97C \uB2E4\uC2DC \uD655\uC778\uD569\uB2C8\uB2E4.");
            }

            YoloDatasetReadinessReport report = lastYoloTrainingReadinessReport;
            if (report == null)
            {
                return (
                    false,
                    "\uC810\uAC80 \uD544\uC694",
                    "\uB370\uC774\uD130\uC14B \uC810\uAC80\uC73C\uB85C \uD559\uC2B5/\uAC80\uC99D/\uCD5C\uC885 \uAC80\uC99D \uC0C1\uD0DC\uB97C \uBA3C\uC800 \uD655\uC778\uD558\uC138\uC694.",
                    "\uBE44\uAD50 \uAE30\uC900: \uB370\uC774\uD130\uC14B \uC810\uAC80 \uD6C4 \uCD5C\uC885 \uAC80\uC99D \uC774\uBBF8\uC9C0\uC640 \uC815\uB2F5 \uB77C\uBCA8 \uC218\uB97C \uD45C\uC2DC\uD569\uB2C8\uB2E4.");
            }

            if (!report.IsReady)
            {
                return (
                    false,
                    "\uD559\uC2B5 \uBD88\uAC00",
                    "\uBAA8\uB378 \uBE44\uAD50 \uC804\uC5D0 \uB370\uC774\uD130\uC14B \uD559\uC2B5 \uBD88\uAC00 \uD56D\uBAA9\uC744 \uBA3C\uC800 \uD574\uACB0\uD558\uC138\uC694.",
                    "\uBE44\uAD50 \uAE30\uC900: \uD559\uC2B5 \uAC00\uB2A5 \uC0C1\uD0DC\uAC00 \uB41C \uD6C4 \uCD5C\uC885 \uAC80\uC99D \uB77C\uBCA8\uB85C \uAD50\uCCB4 \uD310\uB2E8\uC744 \uD569\uB2C8\uB2E4.");
            }

            int testImageCount = report.Statistics?.TestImageCount ?? 0;
            int testLabelCount = report.Statistics?.TestLabelCount ?? 0;
            int finalVerificationCount = System.Math.Min(testImageCount, testLabelCount);
            if (testImageCount <= 0)
            {
                return (
                    false,
                    "\uCD5C\uC885 \uAC80\uC99D \uD544\uC694",
                    "\uBAA8\uB378 \uAD50\uCCB4 \uD310\uB2E8 \uC804\uC5D0 \uCD5C\uC885 \uAC80\uC99D \uC774\uBBF8\uC9C0\uB97C 1\uC7A5 \uC774\uC0C1 \uD655\uBCF4\uD558\uC138\uC694.",
                    "\uBE44\uAD50 \uAE30\uC900: \uCD5C\uC885 \uAC80\uC99D \uC774\uBBF8\uC9C0 0\uC7A5 / \uAD8C\uC7A5 10\uC7A5 \uC774\uC0C1");
            }

            if (testLabelCount <= 0)
            {
                return (
                    false,
                    "\uCD5C\uC885 \uB77C\uBCA8 \uD544\uC694",
                    "\uCD5C\uC885 \uAC80\uC99D \uC774\uBBF8\uC9C0\uB294 \uC788\uC9C0\uB9CC \uC815\uB2F5 \uB77C\uBCA8 \uD30C\uC77C\uC774 \uC5C6\uC2B5\uB2C8\uB2E4. \uBE44\uAD50 \uC804 \uCD5C\uC885 \uAC80\uC99D \uB77C\uBCA8\uC744 \uC800\uC7A5\uD558\uC138\uC694.",
                    $"\uBE44\uAD50 \uAE30\uC900: \uCD5C\uC885 \uAC80\uC99D \uC774\uBBF8\uC9C0 {testImageCount}\uC7A5 / \uC815\uB2F5 \uB77C\uBCA8 0\uC7A5");
            }

            if (finalVerificationCount < RecommendedModelReplacementTestImageCount)
            {
                return (
                    true,
                    "\uBAA8\uB378 \uBE44\uAD50",
                    $"\uCD5C\uC885 \uAC80\uC99D \uB77C\uBCA8 {finalVerificationCount}\uC7A5\uC73C\uB85C \uBE44\uAD50\uB294 \uAC00\uB2A5\uD558\uC9C0\uB9CC \uAD50\uCCB4 \uADFC\uAC70\uAC00 \uC57D\uD569\uB2C8\uB2E4. \uAD8C\uC7A5 {RecommendedModelReplacementTestImageCount}\uC7A5 \uC774\uC0C1\uC744 \uD655\uBCF4\uD55C \uB4A4 \uC801\uC6A9\uD558\uC138\uC694.",
                    $"\uBE44\uAD50 \uAE30\uC900: \uCD5C\uC885 \uAC80\uC99D \uB77C\uBCA8 {finalVerificationCount}\uC7A5 / \uAD8C\uC7A5 {RecommendedModelReplacementTestImageCount}\uC7A5 \uC774\uC0C1 - \uBE44\uAD50 \uAC00\uB2A5, \uAD50\uCCB4 \uADFC\uAC70\uB294 \uC57D\uD568");
            }

            return (
                true,
                "\uBAA8\uB378 \uBE44\uAD50",
                $"\uCD5C\uC885 \uAC80\uC99D \uB77C\uBCA8 {finalVerificationCount}\uC7A5\uC73C\uB85C \uAE30\uC874 \uBAA8\uB378\uACFC \uC0C8 \uD559\uC2B5 \uBAA8\uB378\uC744 \uBE44\uAD50\uD569\uB2C8\uB2E4.",
                $"\uBE44\uAD50 \uAE30\uC900: \uCD5C\uC885 \uAC80\uC99D \uB77C\uBCA8 {finalVerificationCount}\uC7A5 / \uAD8C\uC7A5 {RecommendedModelReplacementTestImageCount}\uC7A5 \uC774\uC0C1 - \uBE44\uAD50 \uD6C4 \uAD50\uCCB4 \uD310\uB2E8 \uAC00\uB2A5");
        }

        private void ApplyShellCommandState(WpfWorkflowCommandState state)
        {
            if (ShellViewModel != null)
            {
                ShellViewModel.ApplyWorkflowCommandState(state);
                return;
            }

            SetControlEnabled(DetectButton, state?.CanRunInference == true);
        }

        private void ApplyImageQueueCommandState(WpfWorkflowCommandState state)
        {
            if (ImageQueueViewModel != null)
            {
                ImageQueueViewModel.ApplyWorkflowCommandState(state);
                return;
            }

            bool canRunInference = state?.CanRunInference == true;
            SetControlEnabled(DetectSelectedQueueButton, canRunInference);
            SetControlEnabled(BatchDetectQueueButton, canRunInference);
            SetControlEnabled(RetryFailedQueueButton, canRunInference);
            SetControlEnabled(StopBatchQueueButton, state?.CanStopBatchDetection == true);
        }

        private void ApplyTrainingSettingsCommandState(WpfWorkflowCommandState state)
        {
            if (TrainingSettingsViewModel != null)
            {
                TrainingSettingsViewModel.ApplyWorkflowCommandState(state);
                return;
            }

            bool canRunGeneralCommands = state?.CanRunGeneralCommands == true;
            SetControlEnabled(RefreshTrainingReadinessButton, canRunGeneralCommands);
            SetControlEnabled(StartTrainingButton, canRunGeneralCommands);
            SetControlEnabled(StopTrainingButton, state?.CanStopTraining == true);
        }

        private void ApplyYoloModelSettingsCommandState(WpfWorkflowCommandState state)
        {
            if (YoloModelSettingsViewModel != null)
            {
                YoloModelSettingsViewModel.ApplyWorkflowCommandState(state);
                return;
            }

            bool canRunGeneralCommands = state?.CanRunGeneralCommands == true;
            SetControlEnabled(BrowseYoloPythonButton, canRunGeneralCommands);
            SetControlEnabled(BrowseYoloProjectRootButton, canRunGeneralCommands);
            SetControlEnabled(BrowseYoloClientScriptButton, canRunGeneralCommands);
            SetControlEnabled(BrowseYoloWeightsButton, canRunGeneralCommands);
            SetControlEnabled(BrowseYoloImageRootButton, canRunGeneralCommands);
            SetControlEnabled(SaveYoloSettingsButton, canRunGeneralCommands);
            SetControlEnabled(ResetYoloSettingsButton, canRunGeneralCommands);
        }

        private void ApplyProjectConfigCommandState(WpfWorkflowCommandState state)
        {
            if (ProjectConfigViewModel != null)
            {
                ProjectConfigViewModel.ApplyWorkflowCommandState(state);
                return;
            }

            bool canRunGeneralCommands = state?.CanRunGeneralCommands == true;
            SetControlEnabled(ApplyProjectRecipeButton, canRunGeneralCommands);
            SetControlEnabled(RefreshProjectRecipeListButton, canRunGeneralCommands);
            SetControlEnabled(SaveProjectConfigButton, state?.CanSaveProjectConfig == true);
            SetControlEnabled(OpenProjectConfigFolderButton, canRunGeneralCommands);
        }

        private void ApplyYoloStatusCommandState(WpfWorkflowCommandState state)
        {
            if (YoloStatusViewModel != null)
            {
                YoloStatusViewModel.ApplyWorkflowCommandState(state);
                return;
            }

            bool canRunGeneralCommands = state?.CanRunGeneralCommands == true;
            SetControlEnabled(FirstCheckYoloButton, canRunGeneralCommands);
            SetControlEnabled(InstallRequirementsButton, canRunGeneralCommands);
            SetControlEnabled(RunYoloSmokeButton, canRunGeneralCommands);
            SetControlEnabled(RestartPythonWorkerButton, canRunGeneralCommands);
            SetControlEnabled(StopPythonWorkerButton, canRunGeneralCommands);
        }

        private static void SetControlEnabled(Control control, bool isEnabled)
        {
            if (control != null)
            {
                control.IsEnabled = isEnabled;
            }
        }

        private void UpdateDetectionCommandHints(WpfWorkflowCommandState state)
        {
            SetControlToolTip(DetectButton, state?.CurrentImageDetectionToolTip);
            SetControlToolTip(DetectSelectedQueueButton, state?.SelectedQueueDetectionToolTip);
            SetControlToolTip(BatchDetectQueueButton, state?.BatchDetectionToolTip);
            SetControlToolTip(RetryFailedQueueButton, state?.RetryFailedToolTip);
            SetControlToolTip(StopBatchQueueButton, state?.StopBatchToolTip);
        }

        private static void SetControlToolTip(FrameworkElement element, string text)
        {
            if (element != null)
            {
                element.ToolTip = text;
            }
        }
    }
}
