using System;
using System.ComponentModel;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        private const int CanvasBrushSizeStep = 2;

        private void ExecuteDecreaseBrushSizeCommand()
            => AdjustBrushSize(-CanvasBrushSizeStep);

        private void ExecuteIncreaseBrushSizeCommand()
            => AdjustBrushSize(CanvasBrushSizeStep);

        private void AdjustBrushSize(int delta)
        {
            if (LearningWorkflowViewModel == null)
            {
                return;
            }

            LearningWorkflowViewModel.BrushSize = Math.Clamp(
                LearningWorkflowViewModel.BrushSize + delta,
                2,
                64);
            SyncCanvasBrushSizeFromWorkflow();
        }

        private void SyncCanvasBrushSizeFromWorkflow()
        {
            CanvasPanelViewModel?.SetBrushSize(LearningWorkflowViewModel?.BrushSize ?? 12);
        }

        private void LearningWorkflowViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e?.PropertyName, nameof(WpfLearningWorkflowPanelViewModel.BrushSize), StringComparison.Ordinal))
            {
                SyncCanvasBrushSizeFromWorkflow();
            }
        }
    }
}
