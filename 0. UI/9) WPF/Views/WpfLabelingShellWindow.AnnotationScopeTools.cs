using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem
{
    // Responsibility group: annotation purpose scope and tool selection.
    // These members remain WPF Window adapters; independent policy belongs in services.
    public partial class WpfLabelingShellWindow
    {
        #region AnnotationScope
        private LabelingDatasetPurpose GetCurrentDatasetPurpose()
        {
            EnsureProjectSettings();
            return LearningWorkflowViewModel?.GetSelectedDatasetPurpose()
                ?? global.Data.ProjectSettings.DatasetPurpose;
        }

        private bool IsSegmentationDatasetPurposeActive()
            => GetCurrentDatasetPurpose() == LabelingDatasetPurpose.Segmentation;

        private int GetVisibleManualSegmentCount()
            => IsSegmentationDatasetPurposeActive() ? manualSegments.Count : 0;

        private IReadOnlyList<LabelingSegmentationObject> GetVisibleManualSegments()
            => IsSegmentationDatasetPurposeActive()
                ? manualSegments
                : Array.Empty<LabelingSegmentationObject>();

        private void RefreshAnnotationVisibilityForDatasetPurpose(bool notifyOperator = false)
        {
            LabelingDatasetPurpose currentPurpose = GetCurrentDatasetPurpose();
            int segmentCount = manualSegments.Count;
            bool isSegmentationPurpose = currentPurpose == LabelingDatasetPurpose.Segmentation;
            if (!isSegmentationPurpose)
            {
                // Dataset purpose switches should hide segmentation artifacts without deleting them.
                // This prevents stale masks/polygons from leaking into box labeling while preserving undo/data if the operator switches back.
                polygonAnnotationService.Reset();
                MainCanvasViewModel?.ClearBrushCursorPreview();
                MainCanvasViewModel?.ClearMaskStrokePreview(refresh: false);
            }

            RefreshPolygonOverlays();
            RefreshObjectList();
            if (notifyOperator)
            {
                ReportAnnotationVisibilityForDatasetPurpose(currentPurpose, segmentCount);
            }
        }

        private void ReportAnnotationVisibilityForDatasetPurpose(LabelingDatasetPurpose purpose, int segmentCount)
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            string text = DatasetContextPresentationService.BuildAnnotationVisibilityStatusText(purpose, segmentCount);
            ApplyAnnotationVisibilityStatus(text);
            ScheduleAnnotationVisibilityStatusRefresh(text);
        }

        private void ApplyAnnotationVisibilityStatus(string text)
        {
            SetModelStatus(text);
            AppendLog(text);
            pendingAnnotationVisibilityStatusText = text;
        }

        private void ScheduleAnnotationVisibilityStatusRefresh(string text)
        {
            // Tool selection can fire once more after the dataset-purpose list click.
            // Re-apply this short guidance at idle so the operator sees why masks vanished or returned.
            Dispatcher?.BeginInvoke(
                new Action(() => ApplyScheduledAnnotationVisibilityStatus(text)),
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            shellTimers.AnnotationVisibilityRefresh.Stop();
            shellTimers.AnnotationVisibilityRefresh.Start();
        }

        private void ApplyScheduledAnnotationVisibilityStatus(string text)
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            SetModelStatus(text);
        }

        private void AnnotationVisibilityRefreshTimer_Tick(object sender, EventArgs e)
        {
            shellTimers.AnnotationVisibilityRefresh.Stop();
            if (isApplicationCloseApproved)
            {
                return;
            }

            SetModelStatus(pendingAnnotationVisibilityStatusText);
        }

        private void EnsureSegmentationDatasetPurposeForSegmentationTool()
        {
            if (IsSegmentationDatasetPurposeActive())
            {
                return;
            }

            ApplyDatasetPurposeToCurrentProject(LabelingDatasetPurpose.Segmentation);
            RefreshCanvasAnnotationToolScope();
        }
        #endregion

        #region AnnotationToolSelectionCommands
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

            CancelPendingAnnotationToolEdits(selectedToolItem.Tool);

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
                AnnotationToolWorkflowAction action = AnnotationWorkflowService.ResolveToolAction(selectedToolItem.Tool);
                ApplyResolvedAnnotationTool(action);
            }
            finally
            {
                applyingAnnotationToolSelection = false;
            }
        }

        private void CancelPendingAnnotationToolEdits(WpfAnnotationTool selectedTool)
        {
            if (selectedTool != WpfAnnotationTool.Rectangle)
            {
                CancelFourPointBoxDraft(updateStatus: false);
            }

            if (pendingSegmentationRemoveUnderlyingPlan != null)
            {
                CancelPendingSegmentationRemoveUnderlying(updateStatus: false);
            }

            if (pendingPolygonVertexEditMode.HasValue)
            {
                CancelPendingPolygonVertexEdit(updateStatus: false);
            }

            if (pendingIntelligentScissorsSource != null)
            {
                CancelPendingIntelligentScissors(updateStatus: false);
            }
        }

        private void ApplyResolvedAnnotationTool(AnnotationToolWorkflowAction action)
        {
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

        private void ApplyAnnotationToolWorkflowAction(AnnotationToolWorkflowAction action)
        {
            switch (action.Kind)
            {
                case WpfAnnotationToolWorkflowActionKind.DrawRoi:
                    SetWorkflowMode(WorkflowMode.Labeling);
                    FocusLabelingSidePanelForTool(action.Tool);
                    MainCanvasViewModel.DrawingShapeKind = action.ShapeKind;
                    if (action.Tool == WpfAnnotationTool.Rectangle)
                    {
                        ApplyRectangleDrawingInputMode();
                    }
                    else
                    {
                        MainCanvasViewModel.IsImagePointInputMode = false;
                        MainCanvasViewModel.IsTeachingMode = true;
                    }
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

        private void SetPendingAnnotationToolStatus(AnnotationToolCapability capability)
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

            if (pendingSegmentationSplitOrientation.HasValue)
            {
                CancelPendingSegmentationSplit(updateStatus: false);
            }

            if (pendingSegmentationHoleEditMode.HasValue)
            {
                CancelPendingSegmentationHoleEdit(updateStatus: false);
            }

            if (pendingPolygonVertexEditMode.HasValue)
            {
                CancelPendingPolygonVertexEdit(updateStatus: false);
            }

            if (pendingSegmentationRemoveUnderlyingPlan != null)
            {
                CancelPendingSegmentationRemoveUnderlying(updateStatus: false);
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

            string purposeName = DatasetContextPresentationService.FormatPurposeName(GetCurrentDatasetPurpose());
            string toolName = LearningWorkflowViewModel?.AnnotationTools.FirstOrDefault(item => item.Tool == tool)?.Text
                ?? tool.ToString();
            SetModelStatus($"\uD604\uC7AC \uB370\uC774\uD130\uC14B \uBAA9\uC801({purposeName})\uC5D0\uC11C\uB294 {toolName} \uB3C4\uAD6C\uB97C \uC0AC\uC6A9\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.");
            AppendLog($"\uB3C4\uAD6C \uC120\uD0DD \uCC28\uB2E8: purpose={purposeName}, tool={toolName}");
            RefreshCanvasWorkflowContext();
        }

        private bool TryDuplicateSelectedAnnotation()
        {
            CompleteMaskAnnotationStroke();
            FlushQueuedMaskStrokeCommits();
            if (!TryGetSelectedObjectReviewItem(out WpfObjectReviewItemRef selectedItem))
            {
                SetYoloCommandStatus("복제할 저장 라벨을 먼저 선택하세요.", isBusy: false);
                return false;
            }

            switch (selectedItem.Source)
            {
                case WpfObjectReviewSource.ManualRoi:
                    return TryDuplicateManualRoi(selectedItem.Index);

                case WpfObjectReviewSource.ManualSegment:
                    return TryDuplicateManualSegment(selectedItem.Index);

                default:
                    SetYoloCommandStatus("수동 박스, 폴리곤, 브러시 마스크만 복제할 수 있습니다.", isBusy: false);
                    return false;
            }
        }

        private bool TryDuplicateManualRoi(int sourceIndex)
        {
            if (sourceIndex < 0 || sourceIndex >= manualRois.Count)
            {
                return false;
            }

            if (!CanMutateSelectedObject(
                WpfObjectReviewItemRef.Manual(sourceIndex),
                requireVisible: true,
                out string stateError))
            {
                SetYoloCommandStatus(stateError, isBusy: false);
                return false;
            }

            if (ObjectReviewPresentationService.GetManualRoiShapeKind(manualRoiShapeKinds, sourceIndex)
                != OpenVisionLab.ImageCanvas.CanvasShapes.CanvasRoiShapeKind.Rectangle)
            {
                SetYoloCommandStatus("현재 P0-A 복제는 박스, 폴리곤, 브러시 마스크만 지원합니다.", isBusy: false);
                return false;
            }

            RegisterAnnotationHistoryBeforeChange("라벨 복제");
            System.Drawing.Rectangle duplicate = AnnotationProductivityService.CreateOffsetRectangle(
                manualRois[sourceIndex],
                activeImageSize);
            string className = GetManualRoiClassName(sourceIndex);
            manualRois.Add(duplicate);
            manualRoiClassNames.Add(className);
            manualRoiShapeKinds.Add(ObjectReviewPresentationService.GetManualRoiShapeKind(manualRoiShapeKinds, sourceIndex));
            manualRoiOverlayIds.Add(string.Empty);

            int duplicateIndex = manualRois.Count - 1;
            RedrawReviewRois();
            RefreshObjectListWithSelection(WpfObjectReviewItemRef.Manual(duplicateIndex));
            ShowSavedLabelsWorkflowView();
            QueueActiveImageQueueStatusRefresh(hasActiveCandidates: pendingDetectionCandidates.Count > 0);
            SetYoloCommandStatus(
                $"라벨 복제: {className} / x={duplicate.X}, y={duplicate.Y}, w={duplicate.Width}, h={duplicate.Height}",
                isBusy: false);
            AppendLog($"Duplicated manual ROI: source={sourceIndex + 1}, target={duplicateIndex + 1}, class={className}");
            return true;
        }

        private bool TryDuplicateManualSegment(int sourceIndex)
        {
            if (sourceIndex < 0 || sourceIndex >= manualSegments.Count)
            {
                return false;
            }

            if (!CanMutateSelectedObject(
                WpfObjectReviewItemRef.ManualSegment(sourceIndex),
                requireVisible: true,
                out string stateError))
            {
                SetYoloCommandStatus(stateError, isBusy: false);
                return false;
            }

            LabelingSegmentationObject duplicate = AnnotationProductivityService.CreateOffsetSegment(
                manualSegments[sourceIndex],
                activeImageSize,
                maskAnnotationService);
            if (duplicate == null)
            {
                return false;
            }

            RegisterAnnotationHistoryBeforeChange("라벨 복제");
            duplicate.ZOrder = SegmentationZOrderService.GetNextZOrder(manualSegments);
            manualSegments.Add(duplicate);
            int duplicateIndex = manualSegments.Count - 1;
            RefreshObjectListWithSelection(WpfObjectReviewItemRef.ManualSegment(duplicateIndex));
            RefreshPolygonOverlays();
            ShowSavedLabelsWorkflowView();
            QueueActiveImageQueueStatusRefresh(hasActiveCandidates: pendingDetectionCandidates.Count > 0);

            string shapeName = duplicate.IsRasterMask ? "마스크" : "폴리곤";
            string className = FirstNonEmpty(duplicate.ClassName, duplicate.ClassItem?.Text, "Defect");
            SetYoloCommandStatus($"라벨 복제: {shapeName} / {className}", isBusy: false);
            AppendLog($"Duplicated manual segment: source={sourceIndex + 1}, target={duplicateIndex + 1}, shape={shapeName}, class={className}");
            return true;
        }
        #endregion

    }
}
