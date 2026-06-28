using OpenVisionLab.ImageCanvas.Canvas;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Canvas workflow commands wrap view movement, candidate focus, overlay reset, and mode switching.
        private void ExecuteFitCanvasCommand()
        {
            MainCanvasViewModel.ImageViewer.ZoomToFit();
        }

        private void ExecuteActualSizeCanvasCommand()
        {
            MainCanvasViewModel.ImageViewer.ZoomToActualSize();
        }

        private void ExecutePanCanvasCommand()
        {
            MainCanvasViewModel.IsTeachingMode = false;
            MainCanvasViewModel.ImageViewer.SetViewMode(CanvasInteractionMode.Drag);
            AppendLog("캔버스 이동 모드");
        }

        private void ExecuteFocusCandidateCommand()
        {
            FocusSelectedCandidateInViewer(logIfMissing: true);
        }

        private void ExecuteResetAiOverlayCommand()
        {
            int removedCount = candidateReviewState.ClearPendingCandidates();
            RefreshCandidateList();
            RedrawReviewRois();
            UpdateDetectionResultOverlay();
            SetPythonStatus("\uCD94\uB860: AI \uD6C4\uBCF4 \uD45C\uC2DC \uC9C0\uC6C0");
            AppendLog($"검출 후보 표시 지움: {removedCount}개");
        }

        private void ExecuteLabelingModeCommand()
        {
            SetWorkflowMode(WorkflowMode.Labeling);
            FocusAnnotationToolsTab();
            if (MainCanvasViewModel.TeachingCommand?.CanExecute(null) == true)
            {
                if (!MainCanvasViewModel.IsTeachingMode)
                {
                    MainCanvasViewModel.TeachingCommand.Execute(null);
                }
            }

            AppendLog("라벨링 모드로 전환했습니다. 이미지 선택만으로 추론하지 않습니다.");
        }

        private void ExecuteInferenceModeCommand()
        {
            SetWorkflowMode(WorkflowMode.Inference);
            if (MainCanvasViewModel.TeachingCommand?.CanExecute(null) == true && MainCanvasViewModel.IsTeachingMode)
            {
                MainCanvasViewModel.TeachingCommand.Execute(null);
            }

            AppendLog("추론 검토 모드로 전환했습니다. 현재 추론 또는 큐 검사 버튼으로 YOLO를 실행하세요.");
        }
    }

}
