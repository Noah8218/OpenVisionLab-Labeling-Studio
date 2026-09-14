using System;
using System.Linq;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using OpenVisionLab.ImageCanvas.ViewModels;

namespace MvcVisionSystem
{
    /// <summary>
    /// Applies the selected annotation tool to the existing Canvas and workflow owners.
    /// The Shell supplies cross-panel actions and retains its active-tool state.
    /// </summary>
    internal sealed class AnnotationToolSelectionAdapter
    {
        private readonly AnnotationToolSelectionAdapterContext context;
        private bool isApplyingSelection;

        internal AnnotationToolSelectionAdapter(AnnotationToolSelectionAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        internal void ExecuteCanvasAnnotationToolSelectionChanged(object selectedItem)
            => ApplyAnnotationToolSelection(
                (selectedItem as WpfAnnotationToolItem)
                ?? context.CanvasPanelViewModel?.SelectedAnnotationTool);

        internal void ApplyAnnotationToolSelection(WpfAnnotationToolItem selectedToolItem)
        {
            if (selectedToolItem == null)
            {
                return;
            }

            CancelPendingAnnotationToolEdits(selectedToolItem.Tool);
            if (!CanSelectAnnotationToolForCurrentPurpose(selectedToolItem))
            {
                RejectAnnotationToolOutsideCurrentPurpose(selectedToolItem.Tool);
                return;
            }

            if (isApplyingSelection)
            {
                SynchronizeAnnotationToolSelection(selectedToolItem);
                return;
            }

            isApplyingSelection = true;
            try
            {
                SynchronizeAnnotationToolSelection(selectedToolItem);
                AnnotationToolWorkflowAction action = AnnotationWorkflowService.ResolveToolAction(selectedToolItem.Tool);
                ApplyResolvedAnnotationTool(action);
            }
            finally
            {
                isApplyingSelection = false;
            }
        }

        internal void SelectAnnotationTool(WpfAnnotationTool tool, bool revealInGuide = false)
        {
            if (context.LearningWorkflowViewModel == null)
            {
                return;
            }

            context.CancelPendingSegmentationSplit?.Invoke();
            context.CancelPendingSegmentationHoleEdit?.Invoke();
            CancelPendingAnnotationToolEdits(tool);

            WpfAnnotationToolItem selectedTool = ResolveSelectableAnnotationTool(tool);
            if (selectedTool == null)
            {
                RejectAnnotationToolOutsideCurrentPurpose(tool);
                return;
            }

            ApplyAnnotationToolSelection(selectedTool);
            if (revealInGuide)
            {
                context.ShowAnnotationToolPalette?.Invoke();
            }
        }

        internal WpfAnnotationToolItem ResolveSelectableAnnotationTool(WpfAnnotationTool tool)
        {
            WpfAnnotationToolItem selectedTool = context.LearningWorkflowViewModel?.SelectableAnnotationTools
                .FirstOrDefault(item => item.Tool == tool);
            if (selectedTool != null)
            {
                return selectedTool;
            }

            return AnnotationWorkflowService.IsOneShotCommandTool(tool)
                ? context.LearningWorkflowViewModel?.AnnotationTools.FirstOrDefault(item => item.Tool == tool)
                : null;
        }

        private void CancelPendingAnnotationToolEdits(WpfAnnotationTool selectedTool)
        {
            if (selectedTool != WpfAnnotationTool.Rectangle)
            {
                context.CancelFourPointBoxDraft?.Invoke();
            }

            if (context.HasPendingSegmentationRemoveUnderlyingPlan?.Invoke() == true)
            {
                context.CancelPendingSegmentationRemoveUnderlying?.Invoke();
            }

            if (context.IsPolygonVertexEditPending?.Invoke() == true)
            {
                context.CancelPendingPolygonVertexEdit?.Invoke();
            }

            if (context.IsIntelligentScissorsPending?.Invoke() == true)
            {
                context.CancelPendingIntelligentScissors?.Invoke();
            }
        }

        private bool CanSelectAnnotationToolForCurrentPurpose(WpfAnnotationToolItem selectedToolItem)
        {
            if (selectedToolItem == null)
            {
                return false;
            }

            if (AnnotationWorkflowService.IsOneShotCommandTool(selectedToolItem.Tool))
            {
                return true;
            }

            return context.LearningWorkflowViewModel?.SelectableAnnotationTools
                .Any(item => ReferenceEquals(item, selectedToolItem) || item.Tool == selectedToolItem.Tool) == true;
        }

        private void ApplyResolvedAnnotationTool(AnnotationToolWorkflowAction action)
        {
            WpfAnnotationTool tool = action.Tool;
            if (action.Kind == WpfAnnotationToolWorkflowActionKind.Pending)
            {
                context.SetActiveAnnotationTool?.Invoke(WpfAnnotationTool.Select);
                context.EndPolygonAnnotationMode?.Invoke(true);
                context.EndMaskAnnotationMode?.Invoke();
                context.FocusAnnotationToolsTab?.Invoke();
                SetPendingAnnotationToolStatus(action.Capability);
                return;
            }

            context.SetActiveAnnotationTool?.Invoke(tool);
            if (tool != WpfAnnotationTool.Polygon)
            {
                context.EndPolygonAnnotationMode?.Invoke(true);
            }

            if (tool != WpfAnnotationTool.Brush && tool != WpfAnnotationTool.Eraser)
            {
                context.EndMaskAnnotationMode?.Invoke();
            }

            ApplyAnnotationToolWorkflowAction(action);
        }

        private void ApplyAnnotationToolWorkflowAction(AnnotationToolWorkflowAction action)
        {
            switch (action.Kind)
            {
                case WpfAnnotationToolWorkflowActionKind.DrawRoi:
                    context.SetLabelingWorkflowMode?.Invoke();
                    context.FocusLabelingSidePanelForTool?.Invoke(action.Tool);
                    context.MainCanvasViewModel.DrawingShapeKind = action.ShapeKind;
                    if (action.Tool == WpfAnnotationTool.Rectangle)
                    {
                        context.ApplyRectangleDrawingInputMode?.Invoke();
                    }
                    else
                    {
                        context.MainCanvasViewModel.IsImagePointInputMode = false;
                        context.MainCanvasViewModel.IsTeachingMode = true;
                    }

                    context.SetModelStatus?.Invoke(action.ModelStatusText);
                    context.SetYoloCommandStatus?.Invoke(action.CommandStatusText, false);
                    context.AppendLog?.Invoke(action.LogText);
                    break;

                case WpfAnnotationToolWorkflowActionKind.Polygon:
                    context.BeginPolygonAnnotationMode?.Invoke();
                    context.FocusLabelingSidePanelForTool?.Invoke(action.Tool);
                    break;

                case WpfAnnotationToolWorkflowActionKind.Brush:
                case WpfAnnotationToolWorkflowActionKind.Eraser:
                    context.BeginMaskAnnotationMode?.Invoke(action.Tool);
                    context.FocusLabelingSidePanelForTool?.Invoke(action.Tool);
                    break;

                case WpfAnnotationToolWorkflowActionKind.PanZoom:
                    context.SetLabelingWorkflowMode?.Invoke();
                    context.FocusLabelingSidePanelForTool?.Invoke(action.Tool);
                    context.ExecutePanCanvasCommand?.Invoke();
                    break;

                case WpfAnnotationToolWorkflowActionKind.Delete:
                    context.SetLabelingWorkflowMode?.Invoke();
                    context.FocusLabelingSidePanelForTool?.Invoke(action.Tool);
                    context.ExecuteDeleteObjectCommand?.Invoke();
                    break;

                case WpfAnnotationToolWorkflowActionKind.Select:
                    context.SetLabelingWorkflowMode?.Invoke();
                    context.MainCanvasViewModel.IsTeachingMode = false;
                    context.MainCanvasViewModel.IsImagePointInputMode = context.IsManualSegmentSelected?.Invoke() == true;
                    context.SetModelStatus?.Invoke("도구: 선택");
                    context.FocusLabelingSidePanelForTool?.Invoke(action.Tool);
                    break;

                case WpfAnnotationToolWorkflowActionKind.Undo:
                    context.UndoAnnotationHistory?.Invoke();
                    break;

                case WpfAnnotationToolWorkflowActionKind.Redo:
                    context.RedoAnnotationHistory?.Invoke();
                    break;

                default:
                    context.SetLabelingWorkflowMode?.Invoke();
                    break;
            }
        }

        private void SynchronizeAnnotationToolSelection(WpfAnnotationToolItem selectedToolItem)
        {
            if (selectedToolItem == null)
            {
                return;
            }

            if (!ReferenceEquals(context.LearningWorkflowViewModel?.SelectedTool, selectedToolItem))
            {
                context.LearningWorkflowViewModel.SelectedTool = selectedToolItem;
            }

            context.CanvasPanelViewModel?.SetSelectedAnnotationTool(selectedToolItem);
            context.RefreshCanvasWorkflowContext?.Invoke();
        }

        private void SetPendingAnnotationToolStatus(AnnotationToolCapability capability)
        {
            string toolName = capability?.DisplayName ?? string.Empty;
            context.SetLabelingWorkflowMode?.Invoke();
            context.MainCanvasViewModel.IsTeachingMode = false;
            context.SetModelStatus?.Invoke($"도구 대기: {toolName}");
            context.SetYoloCommandStatus?.Invoke(
                capability?.StatusText ?? "\uC2E4\uC81C \uB4DC\uB85C\uC789 \uACBD\uB85C \uAC80\uC99D \uC804\uC785\uB2C8\uB2E4.",
                false);
            context.AppendLog?.Invoke($"{toolName} 도구 대기: {capability?.StatusText}");
        }

        private void RejectAnnotationToolOutsideCurrentPurpose(WpfAnnotationTool tool)
        {
            WpfAnnotationToolItem fallback = context.LearningWorkflowViewModel?.SelectedTool;
            if (fallback != null)
            {
                SynchronizeAnnotationToolSelection(fallback);
            }

            LabelingDatasetPurpose purpose = context.CurrentDatasetPurposeProvider?.Invoke()
                ?? LabelingDatasetPurpose.ObjectDetection;
            string purposeName = DatasetContextPresentationService.FormatPurposeName(purpose);
            string toolName = context.LearningWorkflowViewModel?.AnnotationTools
                .FirstOrDefault(item => item.Tool == tool)?.Text
                ?? tool.ToString();
            context.SetModelStatus?.Invoke($"현재 데이터셋 목적({purposeName})에서는 {toolName} 도구를 사용하지 않습니다.");
            context.AppendLog?.Invoke($"도구 선택 차단: purpose={purposeName}, tool={toolName}");
            context.RefreshCanvasWorkflowContext?.Invoke();
        }
    }

    internal sealed class AnnotationToolSelectionAdapterContext
    {
        internal WpfLearningWorkflowPanelViewModel LearningWorkflowViewModel { get; init; }
        internal WpfCanvasPanelViewModel CanvasPanelViewModel { get; init; }
        internal RoiImageCanvasViewModel MainCanvasViewModel { get; init; }
        internal Func<LabelingDatasetPurpose> CurrentDatasetPurposeProvider { get; init; }
        internal Func<bool> HasPendingSegmentationRemoveUnderlyingPlan { get; init; }
        internal Func<bool> IsPolygonVertexEditPending { get; init; }
        internal Func<bool> IsIntelligentScissorsPending { get; init; }
        internal Func<bool> IsManualSegmentSelected { get; init; }
        internal Action CancelFourPointBoxDraft { get; init; }
        internal Action CancelPendingSegmentationRemoveUnderlying { get; init; }
        internal Action CancelPendingSegmentationSplit { get; init; }
        internal Action CancelPendingSegmentationHoleEdit { get; init; }
        internal Action CancelPendingPolygonVertexEdit { get; init; }
        internal Action CancelPendingIntelligentScissors { get; init; }
        internal Action<bool> EndPolygonAnnotationMode { get; init; }
        internal Action EndMaskAnnotationMode { get; init; }
        internal Action FocusAnnotationToolsTab { get; init; }
        internal Action<WpfAnnotationTool> SetActiveAnnotationTool { get; init; }
        internal Action SetLabelingWorkflowMode { get; init; }
        internal Action<WpfAnnotationTool> FocusLabelingSidePanelForTool { get; init; }
        internal Action ApplyRectangleDrawingInputMode { get; init; }
        internal Action BeginPolygonAnnotationMode { get; init; }
        internal Action<WpfAnnotationTool> BeginMaskAnnotationMode { get; init; }
        internal Action ExecutePanCanvasCommand { get; init; }
        internal Action ExecuteDeleteObjectCommand { get; init; }
        internal Action UndoAnnotationHistory { get; init; }
        internal Action RedoAnnotationHistory { get; init; }
        internal Action RefreshCanvasWorkflowContext { get; init; }
        internal Action<string> SetModelStatus { get; init; }
        internal Action<string, bool> SetYoloCommandStatus { get; init; }
        internal Action<string> AppendLog { get; init; }
        internal Action ShowAnnotationToolPalette { get; init; }
    }
}
