namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        private void DatasetPurposeListBox_SelectionChanged(object sender, object selectedItem)
        {
            WpfLearningModeItem selectedPurposeItem = selectedItem as WpfLearningModeItem;
            if (selectedPurposeItem != null && !ReferenceEquals(LearningWorkflowViewModel?.SelectedDatasetPurposeMode, selectedPurposeItem))
            {
                LearningWorkflowViewModel.SelectedDatasetPurposeMode = selectedPurposeItem;
            }

            ApplyWorkflowDatasetPurposeToProjectSettings();
            RefreshCanvasAnnotationToolScope();
            ApplyAnnotationToolSelection(LearningWorkflowViewModel?.SelectedTool);
            RefreshCanvasWorkflowContext();
            RefreshAnnotationVisibilityForDatasetPurpose(notifyOperator: true);
        }

        private void LearningWorkflowModeListBox_SelectionChanged(object sender, object selectedItem)
        {
            WpfLearningModeItem selectedModeItem = selectedItem as WpfLearningModeItem;
            if (selectedModeItem != null && !ReferenceEquals(LearningWorkflowViewModel?.SelectedMode, selectedModeItem))
            {
                LearningWorkflowViewModel.SelectedMode = selectedModeItem;
            }

            WpfLearningMode? mode = selectedModeItem?.Mode ?? LearningWorkflowViewModel?.SelectedMode?.Mode;
            if (!mode.HasValue)
            {
                return;
            }

            WpfLearningModeWorkflowAction action = WpfAnnotationWorkflowService.ResolveModeAction(mode.Value);

            switch (action)
            {
                case WpfLearningModeWorkflowAction.Inference:
                    SetWorkflowMode(WorkflowMode.Inference);
                    break;

                case WpfLearningModeWorkflowAction.LabelingAndFocusYoloSettings:
                    SetWorkflowMode(WorkflowMode.Labeling);
                    FocusYoloSettingsTab();
                    break;

                default:
                    SetWorkflowMode(WorkflowMode.Labeling);
                    ApplyAnnotationToolSelection(LearningWorkflowViewModel?.SelectedTool);
                    break;
            }
        }
    }
}
