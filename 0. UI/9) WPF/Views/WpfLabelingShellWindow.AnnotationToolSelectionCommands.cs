using System.Linq;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        private void AnnotationToolListBox_SelectionChanged(object sender, object selectedItem)
        {
            ApplyAnnotationToolSelection((selectedItem as WpfAnnotationToolItem) ?? LearningWorkflowViewModel?.SelectedTool);
        }

        private void ExecuteCanvasAnnotationToolSelectionChanged(object selectedItem)
        {
            ApplyAnnotationToolSelection((selectedItem as WpfAnnotationToolItem) ?? CanvasPanelViewModel?.SelectedAnnotationTool);
        }

        private void ApplyAnnotationToolSelection(WpfAnnotationToolItem selectedToolItem)
        {
            if (selectedToolItem == null)
            {
                return;
            }

            if (!CanSelectAnnotationToolForCurrentPurpose(selectedToolItem))
            {
                RejectAnnotationToolOutsideCurrentPurpose(selectedToolItem.Tool);
                return;
            }

            if (applyingAnnotationToolSelection)
            {
                SynchronizeAnnotationToolSelection(selectedToolItem);
                return;
            }

            applyingAnnotationToolSelection = true;
            try
            {
                SynchronizeAnnotationToolSelection(selectedToolItem);
                WpfAnnotationToolWorkflowAction action = WpfAnnotationWorkflowService.ResolveToolAction(selectedToolItem.Tool);
                WpfAnnotationTool tool = action.Tool;
                if (action.Kind == WpfAnnotationToolWorkflowActionKind.Pending)
                {
                    activeAnnotationTool = WpfAnnotationTool.Select;
                    EndPolygonAnnotationMode(clearDraft: true);
                    EndMaskAnnotationMode();
                    FocusAnnotationToolsTab();
                    SetPendingAnnotationToolStatus(action.Capability);
                    return;
                }

                activeAnnotationTool = tool;
                if (tool != WpfAnnotationTool.Polygon)
                {
                    EndPolygonAnnotationMode(clearDraft: true);
                }

                if (tool != WpfAnnotationTool.Brush && tool != WpfAnnotationTool.Eraser)
                {
                    EndMaskAnnotationMode();
                }

                ApplyAnnotationToolWorkflowAction(action);
            }
            finally
            {
                applyingAnnotationToolSelection = false;
            }
        }

        private void ApplyAnnotationToolWorkflowAction(WpfAnnotationToolWorkflowAction action)
        {
            switch (action.Kind)
            {
                case WpfAnnotationToolWorkflowActionKind.DrawRoi:
                    SetWorkflowMode(WorkflowMode.Labeling);
                    FocusLabelingSidePanelForTool(action.Tool);
                    MainCanvasViewModel.DrawingShapeKind = action.ShapeKind;
                    MainCanvasViewModel.IsTeachingMode = true;
                    SetModelStatus(action.ModelStatusText);
                    SetYoloCommandStatus(action.CommandStatusText, isBusy: false);
                    AppendLog(action.LogText);
                    break;

                case WpfAnnotationToolWorkflowActionKind.Polygon:
                    BeginPolygonAnnotationMode();
                    FocusLabelingSidePanelForTool(action.Tool);
                    break;

                case WpfAnnotationToolWorkflowActionKind.Brush:
                case WpfAnnotationToolWorkflowActionKind.Eraser:
                    BeginMaskAnnotationMode(action.Tool);
                    FocusLabelingSidePanelForTool(action.Tool);
                    break;

                case WpfAnnotationToolWorkflowActionKind.PanZoom:
                    SetWorkflowMode(WorkflowMode.Labeling);
                    FocusLabelingSidePanelForTool(action.Tool);
                    ExecutePanCanvasCommand();
                    break;

                case WpfAnnotationToolWorkflowActionKind.Delete:
                    SetWorkflowMode(WorkflowMode.Labeling);
                    FocusLabelingSidePanelForTool(action.Tool);
                    ExecuteDeleteObjectCommand();
                    break;

                case WpfAnnotationToolWorkflowActionKind.Select:
                    // Select is still a labeling workflow action: it should reveal object review
                    // after returning from settings/inference tabs when labels already exist.
                    SetWorkflowMode(WorkflowMode.Labeling);
                    MainCanvasViewModel.IsTeachingMode = false;
                    MainCanvasViewModel.IsImagePointInputMode = ObjectReviewViewModel?.IsSelectedSource(WpfObjectReviewSource.ManualSegment) == true;
                    SetModelStatus("\uB3C4\uAD6C: \uC120\uD0DD");
                    FocusLabelingSidePanelForTool(action.Tool);
                    break;

                case WpfAnnotationToolWorkflowActionKind.Undo:
                    UndoWpfAnnotationHistory();
                    break;

                case WpfAnnotationToolWorkflowActionKind.Redo:
                    RedoWpfAnnotationHistory();
                    break;

                default:
                    SetWorkflowMode(WorkflowMode.Labeling);
                    break;
            }
        }

        private void SynchronizeAnnotationToolSelection(WpfAnnotationToolItem selectedToolItem)
        {
            if (selectedToolItem == null)
            {
                return;
            }

            // The guide palette and canvas toolbar display the same tool source; synchronize selection without reapplying command tools.
            if (!ReferenceEquals(LearningWorkflowViewModel?.SelectedTool, selectedToolItem))
            {
                LearningWorkflowViewModel.SelectedTool = selectedToolItem;
            }

            CanvasPanelViewModel?.SetSelectedAnnotationTool(selectedToolItem);
            RefreshCanvasWorkflowContext();
        }

        private void SetPendingAnnotationToolStatus(WpfAnnotationToolCapability capability)
        {
            string toolName = capability?.DisplayName ?? string.Empty;
            SetWorkflowMode(WorkflowMode.Labeling);
            MainCanvasViewModel.IsTeachingMode = false;
            SetModelStatus($"\uB3C4\uAD6C \uB300\uAE30: {toolName}");
            SetYoloCommandStatus(capability?.StatusText ?? "\uC2E4\uC81C \uB4DC\uB85C\uC789 \uACBD\uB85C \uAC80\uC99D \uC804\uC785\uB2C8\uB2E4.", isBusy: false);
            AppendLog($"{toolName} \uB3C4\uAD6C \uB300\uAE30: {capability?.StatusText}");
        }

        private void SelectAnnotationTool(WpfAnnotationTool tool, bool revealInGuide = false)
        {
            if (LearningWorkflowViewModel == null)
            {
                return;
            }

            WpfAnnotationToolItem selectedTool = ResolveSelectableAnnotationTool(tool);
            if (selectedTool == null)
            {
                RejectAnnotationToolOutsideCurrentPurpose(tool);
                return;
            }

            ApplyAnnotationToolSelection(selectedTool);

            if (revealInGuide)
            {
                LearningWorkflowPanelControl?.ShowAnnotationToolPalette();
            }
        }

        private WpfAnnotationToolItem ResolveSelectableAnnotationTool(WpfAnnotationTool tool)
        {
            // Dataset purpose owns the available labeling tools. Programmatic shortcuts
            // must use the same purpose-filtered list as the visible palette, otherwise
            // hidden tools such as Brush can silently enter an object-detection workflow.
            WpfAnnotationToolItem selectedTool = LearningWorkflowViewModel?.SelectableAnnotationTools
                .FirstOrDefault(item => item.Tool == tool);
            if (selectedTool != null)
            {
                return selectedTool;
            }

            return IsOneShotAnnotationCommandTool(tool)
                ? LearningWorkflowViewModel?.AnnotationTools.FirstOrDefault(item => item.Tool == tool)
                : null;
        }

        private bool CanSelectAnnotationToolForCurrentPurpose(WpfAnnotationToolItem selectedToolItem)
        {
            if (selectedToolItem == null)
            {
                return false;
            }

            if (IsOneShotAnnotationCommandTool(selectedToolItem.Tool))
            {
                return true;
            }

            return LearningWorkflowViewModel?.SelectableAnnotationTools
                .Any(item => ReferenceEquals(item, selectedToolItem) || item.Tool == selectedToolItem.Tool) == true;
        }

        private static bool IsOneShotAnnotationCommandTool(WpfAnnotationTool tool)
            => tool == WpfAnnotationTool.Undo
                || tool == WpfAnnotationTool.Redo
                || tool == WpfAnnotationTool.Delete;

        private void RejectAnnotationToolOutsideCurrentPurpose(WpfAnnotationTool tool)
        {
            WpfAnnotationToolItem fallback = LearningWorkflowViewModel?.SelectedTool;
            if (fallback != null)
            {
                SynchronizeAnnotationToolSelection(fallback);
            }

            string purposeName = FormatDatasetPurposeName(GetCurrentDatasetPurpose());
            string toolName = LearningWorkflowViewModel?.AnnotationTools.FirstOrDefault(item => item.Tool == tool)?.Text
                ?? tool.ToString();
            SetModelStatus($"\uD604\uC7AC \uB370\uC774\uD130\uC14B \uBAA9\uC801({purposeName})\uC5D0\uC11C\uB294 {toolName} \uB3C4\uAD6C\uB97C \uC0AC\uC6A9\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.");
            AppendLog($"\uB3C4\uAD6C \uC120\uD0DD \uCC28\uB2E8: purpose={purposeName}, tool={toolName}");
            RefreshCanvasWorkflowContext();
        }
    }
}
