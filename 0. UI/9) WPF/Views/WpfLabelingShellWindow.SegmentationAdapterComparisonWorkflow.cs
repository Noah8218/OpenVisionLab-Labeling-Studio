using System;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    // Responsibility group: segmentation adapter comparison.
    // These members remain WPF Window adapters; independent policy belongs in services.
    public partial class WpfLabelingShellWindow
    {
        #region SegmentationAdapterComparison
        private void RefreshSegmentationAdapterComparisonState()
        {
            EnsureProjectSettings();
            if (TrainingSettingsViewModel == null)
            {
                return;
            }

            SegmentationAdapterComparisonContext context = segmentationAdapterComparisonRunService.BuildContext(
                global.Data,
                global.Data.ProjectSettings.ModelRegistry,
                global.Data.ProjectSettings.PythonModel);
            TrainingSettingsViewModel.SetSegmentationAdapterComparisonContext(context);
        }

        private void ExecuteBrowseSegmentationUnetCheckpointCommand()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            string currentPath = TrainingSettingsViewModel?.SegmentationUnetWeightsPath ?? string.Empty;
            if (!TryPickFile(
                    "U-Net segmentation checkpoint 선택",
                    "PyTorch checkpoint (*.pt;*.pth)|*.pt;*.pth|All files (*.*)|*.*",
                    currentPath,
                    out string selectedPath))
            {
                return;
            }

            if (isApplicationCloseApproved)
            {
                return;
            }

            TrainingSettingsViewModel.SegmentationUnetWeightsPath = selectedPath;
            RefreshSegmentationAdapterComparisonState();
        }

        private void ExecuteBrowseSegmentationYoloCheckpointCommand()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            string currentPath = TrainingSettingsViewModel?.SegmentationYoloWeightsPath ?? string.Empty;
            if (!TryPickFile(
                    "YOLO segmentation checkpoint 선택",
                    "Ultralytics checkpoint (*.pt;*.pth)|*.pt;*.pth|All files (*.*)|*.*",
                    currentPath,
                    out string selectedPath))
            {
                return;
            }

            if (isApplicationCloseApproved)
            {
                return;
            }

            TrainingSettingsViewModel.SegmentationYoloWeightsPath = selectedPath;
            RefreshSegmentationAdapterComparisonState();
        }

        private void ExecuteRunSegmentationAdapterComparisonCommand()
        {
            _ = ExecuteRunSegmentationAdapterComparisonCommandAsync();
        }

        private Task ExecuteRunSegmentationAdapterComparisonCommandAsync()
        {
            return modelComparisonWorkflowService.RunSegmentationAsync(CreateModelComparisonCallbacks());
        }

        #endregion

    }
}
