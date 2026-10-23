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
        {
            if (context.CanvasPanelViewModel?.IsConfiguringAnnotationTools == true)
            {
                return;
            }

            if (selectedItem is WpfAnnotationToolItem selectedToolItem)
            {
                ApplyAnnotationToolSelection(selectedToolItem);
            }
        }

        internal bool ApplyAnnotationToolSelection(WpfAnnotationToolItem selectedToolItem)
        {
            if (selectedToolItem == null)
            {
                return false;
            }

            if (!CanSelectAnnotationToolForCurrentPurpose(selectedToolItem))
            {
                RejectAnnotationToolOutsideCurrentPurpose(selectedToolItem.Tool);
                return false;
            }

            if (isApplyingSelection)
            {
                SynchronizeAnnotationToolSelection(selectedToolItem);
                return true;
            }

            isApplyingSelection = true;
            try
            {
                AnnotationToolWorkflowAction action = AnnotationWorkflowService.ResolveToolAction(selectedToolItem.Tool);
                if (action.Kind != WpfAnnotationToolWorkflowActionKind.Undo
                    && action.Kind != WpfAnnotationToolWorkflowActionKind.Redo
                    && context.SetLabelingWorkflowMode?.Invoke() == false)
                {
                    RestoreActiveAnnotationToolSelection();
                    return false;
                }

                context.CancelPendingSegmentationSplit?.Invoke();
                context.CancelPendingSegmentationHoleEdit?.Invoke();
                CancelPendingAnnotationToolEdits(selectedToolItem.Tool);
                SynchronizeAnnotationToolSelection(selectedToolItem);
                ApplyResolvedAnnotationTool(action);
                if (action.Kind != WpfAnnotationToolWorkflowActionKind.Pending
                    && !AnnotationWorkflowService.IsOneShotCommandTool(selectedToolItem.Tool))
                {
                    SynchronizeAnnotationToolSelection(selectedToolItem);
                }
                context.RefreshCanvasWorkflowContext?.Invoke();
                return true;
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

            WpfAnnotationToolItem selectedTool = ResolveSelectableAnnotationTool(tool);
            if (selectedTool == null)
            {
                RejectAnnotationToolOutsideCurrentPurpose(tool);
                return;
            }

            if (ApplyAnnotationToolSelection(selectedTool) && revealInGuide)
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
                    context.FocusLabelingSidePanelForTool?.Invoke(action.Tool);
                    context.ExecutePanCanvasCommand?.Invoke();
                    break;

                case WpfAnnotationToolWorkflowActionKind.Delete:
                    context.FocusLabelingSidePanelForTool?.Invoke(action.Tool);
                    context.ExecuteDeleteObjectCommand?.Invoke();
                    break;

                case WpfAnnotationToolWorkflowActionKind.Select:
                    context.MainCanvasViewModel.IsTeachingMode = false;
                    context.MainCanvasViewModel.IsImagePointInputMode = context.IsManualSegmentSelected?.Invoke() == true;
                    context.SetModelStatus?.Invoke("도구: 선택");
                    context.FocusLabelingSidePanelForTool?.Invoke(action.Tool);
                    break;

                case WpfAnnotationToolWorkflowActionKind.LabelingFallback when action.Tool == WpfAnnotationTool.SmartMask:
                    context.ActivateSmartMaskTool?.Invoke();
                    context.SetModelStatus?.Invoke("도구: 스마트 마스크");
                    context.SetYoloCommandStatus?.Invoke("스마트 마스크 버튼을 눌러 후보를 만드세요.", false);
                    context.FocusLabelingSidePanelForTool?.Invoke(action.Tool);
                    break;

                case WpfAnnotationToolWorkflowActionKind.Undo:
                    context.UndoAnnotationHistory?.Invoke();
                    break;

                case WpfAnnotationToolWorkflowActionKind.Redo:
                    context.RedoAnnotationHistory?.Invoke();
                    break;

                default:
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
            context.MainCanvasViewModel.IsTeachingMode = false;
            context.SetModelStatus?.Invoke($"도구 대기: {toolName}");
            context.SetYoloCommandStatus?.Invoke(
                capability?.StatusText ?? OpenVisionLab.OpenVisionLanguageService.T("WpfAnnotationTool.StateUnavailable"),
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

        private void RestoreActiveAnnotationToolSelection()
        {
            WpfAnnotationTool activeTool = context.ActiveAnnotationToolProvider?.Invoke() ?? WpfAnnotationTool.Select;
            WpfAnnotationToolItem activeToolItem = context.LearningWorkflowViewModel?.AnnotationTools
                .FirstOrDefault(item => item.Tool == activeTool);
            if (activeToolItem != null)
            {
                SynchronizeAnnotationToolSelection(activeToolItem);
            }
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
        internal Func<WpfAnnotationTool> ActiveAnnotationToolProvider { get; init; }
        internal Action<bool> EndPolygonAnnotationMode { get; init; }
        internal Action EndMaskAnnotationMode { get; init; }
        internal Action FocusAnnotationToolsTab { get; init; }
        internal Action<WpfAnnotationTool> SetActiveAnnotationTool { get; init; }
        internal Func<bool> SetLabelingWorkflowMode { get; init; }
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
        internal Action ActivateSmartMaskTool { get; init; }
        internal Action ShowAnnotationToolPalette { get; init; }
    }
}
