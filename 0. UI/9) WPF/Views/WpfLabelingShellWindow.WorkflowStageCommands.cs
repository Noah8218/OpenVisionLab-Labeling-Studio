namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        private void ExecuteDatasetHomeCommand()
        {
            ShellViewModel?.SetWorkflowStage(WpfShellWorkflowStage.Dataset);
            FocusDatasetOnboardingTab();
            SetModelStatus("\uC791\uC5C5 \uB2E8\uACC4: \uB370\uC774\uD130\uC14B \uD648");
            AppendLog("\uC791\uC5C5 \uB2E8\uACC4 \uC774\uB3D9: \uB370\uC774\uD130\uC14B \uD648");
        }

        private void ExecuteLabelingWorkbenchCommand()
        {
            EnterLabelingWorkbenchStartView();
            SetModelStatus("\uC791\uC5C5 \uB2E8\uACC4: \uB77C\uBCA8\uB9C1 \uC6CC\uD06C\uBCA4\uCE58");
            AppendLog("\uC791\uC5C5 \uB2E8\uACC4 \uC774\uB3D9: \uB77C\uBCA8\uB9C1 \uC6CC\uD06C\uBCA4\uCE58");
        }

        private void EnterLabelingWorkbenchStartView()
        {
            LearningWorkflowViewModel?.ShowLabelingTask();
            EnterLabelingMode(openGuidePanel: false);
            ShellViewModel?.SetRightWorkflowShortcut(WpfRightWorkflowShortcut.SavedLabels);
            ShellViewModel?.SetRightWorkflowDockExpanded(false);
            SelectRightWorkflowView(ObjectsReviewTab);
        }

        private void ExecuteInferenceReviewCommand()
        {
            ExecuteInferenceModeCommand();
            ShowCandidateReviewWorkflowView();
            SetModelStatus("\uC791\uC5C5 \uB2E8\uACC4: \uCD94\uB860 \uAC80\uD1A0");
            AppendLog("\uC791\uC5C5 \uB2E8\uACC4 \uC774\uB3D9: \uCD94\uB860 \uAC80\uD1A0");
        }

        private void ExecuteTrainingModelCenterCommand()
        {
            ShellViewModel?.SetWorkflowStage(WpfShellWorkflowStage.TrainingModel);
            FocusYoloSettingsTab();
            SetModelStatus("\uC791\uC5C5 \uB2E8\uACC4: \uD559\uC2B5/\uBAA8\uB378 \uC13C\uD130");
            AppendLog("\uC791\uC5C5 \uB2E8\uACC4 \uC774\uB3D9: \uD559\uC2B5/\uBAA8\uB378 \uC13C\uD130");
        }

        private void ExecuteReviewCandidateModelCommand()
        {
            ExecuteInferenceModeCommand();
            UpdateTrainingComparisonViewModel(BuildCurrentTrainingWeightsComparison());
            ShowCandidateReviewWorkflowView();
            SetModelStatus("\uD6C4\uBCF4 \uBAA8\uB378 \uAC80\uC99D: \uD559\uC2B5 \uD6C4\uBCF4 \uAC80\uD1A0 \uD0ED");
            SetYoloCommandStatus("\uD559\uC2B5 \uD6C4\uBCF4 \uAC80\uD1A0 \uD0ED\uC73C\uB85C \uC774\uB3D9\uD588\uC2B5\uB2C8\uB2E4. \uD604\uC7AC \uC774\uBBF8\uC9C0 \uAC80\uC0AC\uB294 \uD604\uC7AC \uAC80\uC0AC \uBC84\uD2BC\uC5D0\uC11C \uC2E4\uD589\uD569\uB2C8\uB2E4.", isBusy: false);
            AppendLog("\uC791\uC5C5 \uB2E8\uACC4 \uC774\uB3D9: \uD559\uC2B5 \uD6C4\uBCF4 \uAC80\uD1A0");
        }
    }
}
