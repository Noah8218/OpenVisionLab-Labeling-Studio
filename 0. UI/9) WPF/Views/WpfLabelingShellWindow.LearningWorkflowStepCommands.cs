namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        private void LearningStepListBox_SelectionChanged(object sender, object selectedItem)
        {
            WpfLearningStep? step = (selectedItem as WpfLearningStepItem)?.Step ?? LearningWorkflowViewModel?.SelectedStep?.Step;
            if (!step.HasValue)
            {
                return;
            }

            switch (WpfAnnotationWorkflowService.ResolveStepAction(step.Value))
            {
                case WpfLearningStepWorkflowAction.LoadSample:
                    TryLoadStartupSampleImage();
                    break;

                case WpfLearningStepWorkflowAction.StartBoxLabeling:
                    SetWorkflowMode(WorkflowMode.Labeling);
                    SelectAnnotationTool(WpfAnnotationTool.Rectangle, revealInGuide: true);
                    ExecuteAddSampleRoiCommand();
                    break;

                case WpfLearningStepWorkflowAction.Inference:
                    SetWorkflowMode(WorkflowMode.Inference);
                    break;

                case WpfLearningStepWorkflowAction.ShowCandidateReview:
                    ShowCandidateReviewWorkflowView();
                    break;

                case WpfLearningStepWorkflowAction.SaveAnnotations:
                    ExecuteSaveAnnotationsCommand();
                    break;
            }

            RefreshCanvasWorkflowContext();
        }
    }
}
