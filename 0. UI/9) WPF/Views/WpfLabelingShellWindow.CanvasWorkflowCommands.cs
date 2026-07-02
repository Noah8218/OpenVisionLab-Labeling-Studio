using OpenVisionLab.ImageCanvas.Canvas;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Canvas workflow commands wrap view movement, candidate focus, overlay reset, display filtering, and mode switching.
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
            AppendLog("\uCEA0\uBC84\uC2A4 \uC774\uB3D9 \uBAA8\uB4DC");
        }

        private void ExecuteFocusCandidateCommand()
        {
            FocusSelectedCandidateInViewer(logIfMissing: true);
        }

        private void ExecuteCanvasDisplayModeSelectionChanged(object selectedItem)
        {
            WpfCanvasDisplayModeItem displayModeItem = selectedItem as WpfCanvasDisplayModeItem
                ?? CanvasPanelViewModel?.SelectedDisplayMode;
            if (displayModeItem == null)
            {
                return;
            }

            ApplyCanvasDisplayMode(displayModeItem.Mode, redraw: true, logChange: true);
        }

        private void ApplyCanvasDisplayMode(WpfCanvasDisplayMode mode, bool redraw, bool logChange)
        {
            bool changed = canvasDisplayMode != mode;
            canvasDisplayMode = mode;
            CanvasPanelViewModel?.SetDisplayMode(mode);
            RefreshCanvasLayerVisibilityState();

            if (redraw)
            {
                RedrawReviewRois();
                UpdateDetectionResultOverlay();
                UpdateCanvasCommandButtons();
            }

            if (logChange && changed)
            {
                string modeText = FormatCanvasDisplayMode(mode);
                SetModelStatus($"\uCEA0\uBC84\uC2A4 \uBCF4\uAE30: {modeText}");
                AppendLog($"\uCEA0\uBC84\uC2A4 \uBCF4\uAE30: {modeText}");
            }
        }

        private static string FormatCanvasDisplayMode(WpfCanvasDisplayMode mode)
        {
            switch (mode)
            {
                case WpfCanvasDisplayMode.InferenceOnly:
                    return "AI \uD6C4\uBCF4";

                case WpfCanvasDisplayMode.Both:
                    return "\uBE44\uAD50";

                default:
                    return "\uB77C\uBCA8\uB9CC";
            }
        }

        private bool ShouldShowLabelOverlays()
            => canvasDisplayMode != WpfCanvasDisplayMode.InferenceOnly;

        private bool ShouldShowInferenceOverlays()
            => canvasDisplayMode != WpfCanvasDisplayMode.LabelsOnly;

        private void RefreshCanvasLayerVisibilityState()
        {
            int labelCount = GetCanvasLabelObjectCount();
            int candidateCount = pendingDetectionCandidates?.Count ?? 0;
            CanvasPanelViewModel?.SetLayerVisibilityState(
                canvasDisplayMode,
                labelCount,
                candidateCount,
                !string.IsNullOrWhiteSpace(annotationDirtyReason));
            CanvasPanelViewModel?.SetNoObjectCompletionState(
                activeImageBitmap != null && !activeImageSize.IsEmpty,
                labelCount > 0,
                candidateCount > 0);
        }

        private void ExecuteResetAiOverlayCommand()
        {
            int removedCount = candidateReviewState.ClearPendingCandidates();
            ApplyCanvasDisplayMode(WpfCanvasDisplayMode.LabelsOnly, redraw: false, logChange: false);
            RefreshCandidateList();
            RedrawReviewRois();
            UpdateDetectionResultOverlay();
            SetPythonStatus("\uCD94\uB860: AI \uD6C4\uBCF4 \uD45C\uC2DC \uC9C0\uC6C0");
            AppendLog($"\uAC80\uCD9C \uD6C4\uBCF4 \uD45C\uC2DC \uC9C0\uC6C0: {removedCount}\uAC1C");
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

            AppendLog("\uB77C\uBCA8\uB9C1 \uBAA8\uB4DC\uB85C \uC804\uD658\uD588\uC2B5\uB2C8\uB2E4. \uCEA0\uBC84\uC2A4\uB294 \uB77C\uBCA8\uB9CC \uD45C\uC2DC\uD569\uB2C8\uB2E4.");
        }

        private void ExecuteInferenceModeCommand()
        {
            SetWorkflowMode(WorkflowMode.Inference);
            if (MainCanvasViewModel.TeachingCommand?.CanExecute(null) == true && MainCanvasViewModel.IsTeachingMode)
            {
                MainCanvasViewModel.TeachingCommand.Execute(null);
            }

            AppendLog("\uCD94\uB860 \uAC80\uD1A0 \uBAA8\uB4DC\uB85C \uC804\uD658\uD588\uC2B5\uB2C8\uB2E4. \uCEA0\uBC84\uC2A4\uB294 AI \uCD94\uB860 \uD6C4\uBCF4\uB9CC \uD45C\uC2DC\uD569\uB2C8\uB2E4.");
        }
    }
}
