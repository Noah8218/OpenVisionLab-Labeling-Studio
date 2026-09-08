using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.Canvas;
using OpenVisionLab.ImageCanvas.OpenGLRendering;
using System;
using System.Drawing;
using OpenVisionLab.ImageCanvas.ViewModels;
using MvcVisionSystem._1._Core;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using System.Linq;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    // Responsibility group: specialized annotation tools.
    // These members remain WPF Window adapters; independent policy belongs in services.
    public partial class WpfLabelingShellWindow
    {
        #region FourPointBox
        private readonly FourPointBoxService fourPointBoxService = new FourPointBoxService();

        private bool IsFourPointBoxInputActive()
            => activeAnnotationTool == WpfAnnotationTool.Rectangle
                && CanvasPanelViewModel?.SelectedBoxDrawingMethod?.Method
                    == LabelingBoxDrawingMethod.FourPointExtreme;

        private void ExecuteSetBoxDrawingMethod(LabelingBoxDrawingMethod method)
        {
            LabelingBoxDrawingMethod normalized = Enum.IsDefined(typeof(LabelingBoxDrawingMethod), method)
                ? method
                : LabelingBoxDrawingMethod.TwoPointDrag;
            CancelFourPointBoxDraft(updateStatus: false);
            EnsureProjectSettings();
            global.Data.ProjectSettings.BoxDrawingMethod = normalized;
            CanvasPanelViewModel?.RestoreBoxDrawingMethod(normalized);

            string recipeName = GetCurrentRecipeName();
            if (!string.IsNullOrWhiteSpace(recipeName))
            {
                try
                {
                    RecipeConfigurationSaveResult saveResult = projectRecipeSessionService.SaveConfiguration(
                        global.Data,
                        recipeName,
                        updateYoloDataYaml: false,
                        refreshDatasetVersion: false);
                    if (!saveResult.IsSuccess)
                    {
                        AppendLog("\uBC15\uC2A4 \uC785\uB825 \uBC29\uC2DD \uC800\uC7A5 \uC2E4\uD328: " + saveResult.ErrorMessage);
                    }
                }
                catch (Exception error)
                {
                    AppendLog("\uBC15\uC2A4 \uC785\uB825 \uBC29\uC2DD \uC800\uC7A5 \uC2E4\uD328: " + error.Message);
                }
            }

            ApplyRectangleDrawingInputMode();
            string methodName = normalized == LabelingBoxDrawingMethod.FourPointExtreme
                ? "4\uC810 \uADF9\uC810"
                : "2\uC810 \uB4DC\uB798\uADF8";
            SetYoloCommandStatus($"\uBC15\uC2A4 \uC785\uB825: {methodName}", isBusy: false);
            AppendLog($"\uBC15\uC2A4 \uC785\uB825 \uBC29\uC2DD: {methodName}");
        }

        private void RestoreBoxDrawingMethodFromProject()
        {
            EnsureProjectSettings();
            CanvasPanelViewModel?.RestoreBoxDrawingMethod(global.Data.ProjectSettings.BoxDrawingMethod);
            CancelFourPointBoxDraft(updateStatus: false);
            ApplyRectangleDrawingInputMode();
        }

        private void ApplyRectangleDrawingInputMode()
        {
            if (MainCanvasViewModel == null || activeAnnotationTool != WpfAnnotationTool.Rectangle)
            {
                return;
            }

            if (IsFourPointBoxInputActive())
            {
                MainCanvasViewModel.IsTeachingMode = false;
                MainCanvasViewModel.IsImagePointInputMode = true;
                MainCanvasViewModel.ImageViewer.SetViewMode(CanvasInteractionMode.None);
                CanvasPanelViewModel?.SetFourPointBoxProgress(fourPointBoxService.PointCount);
                RefreshCanvasWorkflowContext();
                return;
            }

            MainCanvasViewModel.IsImagePointInputMode = false;
            MainCanvasViewModel.IsTeachingMode = true;
            CanvasPanelViewModel?.SetFourPointBoxProgress(0);
            RefreshCanvasWorkflowContext();
        }

        private bool TryHandleFourPointBoxInput(CanvasImagePointEventArgs e)
        {
            if (!IsFourPointBoxInputActive() || e == null)
            {
                return false;
            }

            if (e.Button == CanvasPointerButton.Right)
            {
                CancelFourPointBoxDraft(updateStatus: true);
                return true;
            }

            if (e.Button != CanvasPointerButton.Left)
            {
                return true;
            }

            WpfFourPointBoxInputResult result = fourPointBoxService.TryAddPoint(
                e.ImagePoint,
                activeImageSize,
                out Rectangle completedBounds,
                out string message);
            CanvasPanelViewModel?.SetFourPointBoxProgress(fourPointBoxService.PointCount);
            RefreshPolygonOverlays();
            SetYoloCommandStatus(message, isBusy: false);
            if (result != WpfFourPointBoxInputResult.Completed)
            {
                return true;
            }

            string className = FirstNonEmpty(GetSelectedClassName(), "Defect");
            if (MainCanvasViewModel.AddCompletedImageRectangle(completedBounds, className) == null)
            {
                SetYoloCommandStatus("\uBC15\uC2A4 \uC624\uBC84\uB808\uC774\uB97C \uCD94\uAC00\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.", isBusy: false);
                return true;
            }

            CanvasPanelViewModel?.SetFourPointBoxProgress(0);
            RefreshPolygonOverlays();
            return true;
        }

        private bool RemoveLastFourPointBoxPoint()
        {
            if (!IsFourPointBoxInputActive() || !fourPointBoxService.RemoveLastPoint())
            {
                return false;
            }

            CanvasPanelViewModel?.SetFourPointBoxProgress(fourPointBoxService.PointCount);
            RefreshPolygonOverlays();
            SetYoloCommandStatus(
                fourPointBoxService.PointCount == 0
                    ? "4\uC810 \uADF9\uC810 \uC785\uB825\uC744 \uB2E4\uC2DC \uC2DC\uC791\uD558\uC138\uC694."
                    : fourPointBoxService.BuildProgressText(),
                isBusy: false);
            return true;
        }

        private bool CancelFourPointBoxDraft(bool updateStatus)
        {
            bool canceled = fourPointBoxService.Reset();
            CanvasPanelViewModel?.SetFourPointBoxProgress(0);
            if (canceled)
            {
                RefreshPolygonOverlays();
                if (updateStatus)
                {
                    SetYoloCommandStatus(
                        "4\uC810 \uADF9\uC810 \uCD08\uC548\uC744 \uCDE8\uC18C\uD588\uC2B5\uB2C8\uB2E4.",
                        isBusy: false);
                }
            }

            return canceled;
        }
        #endregion

        #region IntelligentScissorsCommands
        private void ExecuteBeginIntelligentScissorsCommand()
        {
            if (activeImageBitmap == null || activeImageSize.IsEmpty)
            {
                const string imageError = "\uACBD\uACC4\uB97C \uBD84\uC11D\uD560 \uC774\uBBF8\uC9C0\uB97C \uBA3C\uC800 \uC5EC\uC138\uC694.";
                SetYoloCommandStatus(imageError, isBusy: false);
                AppendLog(imageError);
                return;
            }

            if (!TryGetSelectedObjectReviewItem(out WpfObjectReviewItemRef selected)
                || selected.Source != WpfObjectReviewSource.ManualSegment
                || selected.Index < 0
                || selected.Index >= manualSegments.Count
                || manualSegments[selected.Index]?.IsRasterMask != false)
            {
                const string selectionError = "\uACBD\uACC4\uB97C \uB2E4\uC2DC \uACC4\uC0B0\uD560 \uC218\uB3D9 \uD3F4\uB9AC\uACE4\uC744 \uC120\uD0DD\uD558\uC138\uC694.";
                SetYoloCommandStatus(selectionError, isBusy: false);
                AppendLog(selectionError);
                return;
            }

            if (!CanMutateSelectedObject(selected, requireVisible: true, out string stateError))
            {
                SetYoloCommandStatus(stateError, isBusy: false);
                AppendLog(stateError);
                return;
            }

            CancelPendingSegmentationRemoveUnderlying(updateStatus: false);
            CancelPendingPolygonVertexEdit(updateStatus: false);
            CancelPendingSegmentationSplit(updateStatus: false);
            CancelPendingSegmentationHoleEdit(updateStatus: false);
            SelectAnnotationTool(WpfAnnotationTool.Select);
            polygonBoundaryEditWorkflowService.BeginIntelligentScissors(
                selected.Index,
                manualSegments[selected.Index]);
            ObjectReviewViewModel?.SetIntelligentScissorsState(
                pending: true,
                hasPreview: false,
                statusText: "\uACBD\uACC4 \uCD94\uC885: \uB2E4\uC2DC \uACC4\uC0B0\uD560 \uD3F4\uB9AC\uACE4 \uBAA8\uC11C\uB9AC \uADFC\uCC98\uB97C \uD074\uB9AD\uD558\uC138\uC694. \uC6B0\uD074\uB9AD\uC740 \uCDE8\uC18C\uC785\uB2C8\uB2E4.");
            MainCanvasViewModel.IsImagePointInputMode = true;
            MainCanvasViewModel.ImageViewer.SetViewMode(CanvasInteractionMode.None);
            RefreshPolygonOverlays();
            const string status = "\uACBD\uACC4 \uCD94\uC885: \uD3F4\uB9AC\uACE4 \uBAA8\uC11C\uB9AC\uB97C \uD074\uB9AD\uD558\uBA74 \uC774\uBBF8\uC9C0 \uACBD\uACC4 \uACBD\uB85C\uB97C \uBBF8\uB9AC\uBCF4\uAE30\uD569\uB2C8\uB2E4.";
            SetYoloCommandStatus(status, isBusy: false);
            AppendLog(status);
        }

        private bool TryHandlePendingIntelligentScissors(CanvasImagePointEventArgs e)
        {
            if (!polygonBoundaryEditWorkflowService.IsIntelligentScissorsPending)
            {
                return false;
            }

            if (e?.Button == CanvasPointerButton.Right)
            {
                CancelPendingIntelligentScissors(updateStatus: true);
                return true;
            }

            if (e?.Button != CanvasPointerButton.Left)
            {
                return true;
            }

            if (!TryResolvePendingIntelligentScissorsSource(out int sourceIndex, out LabelingSegmentationObject source))
            {
                CancelPendingIntelligentScissors(updateStatus: false);
                const string staleSelectionError = "\uC120\uD0DD\uD55C \uAC1D\uCCB4\uAC00 \uBCC0\uACBD\uB418\uC5B4 \uACBD\uACC4 \uCD94\uC885\uC744 \uCDE8\uC18C\uD588\uC2B5\uB2C8\uB2E4.";
                SetYoloCommandStatus(staleSelectionError, isBusy: false);
                AppendLog(staleSelectionError);
                return true;
            }

            int hitTolerance = PolygonAnnotationService.ResolveImageHitTolerance(
                MainCanvasViewModel?.ImageViewer?.ZoomScale ?? 1F);
            if (!polygonBoundaryEditWorkflowService.TryPreviewIntelligentScissors(
                activeImageBitmap,
                e.ImagePoint,
                activeImageSize,
                hitTolerance,
                out WpfIntelligentScissorsPlan plan,
                out string error))
            {
                ObjectReviewViewModel?.SetIntelligentScissorsState(
                    pending: true,
                    hasPreview: false,
                    statusText: error);
                RefreshPolygonOverlays();
                SetYoloCommandStatus(error, isBusy: false);
                AppendLog($"Intelligent scissors preview skipped: {error}");
                return true;
            }

            string previewStatus = FormattableString.Invariant(
                $"\uACBD\uACC4 \uBBF8\uB9AC\uBCF4\uAE30: {plan.PathPoints.Count}\uAC1C \uACBD\uB85C\uC810 / {plan.Elapsed.TotalMilliseconds:0.0} ms. \uCE94\uBC84\uC2A4\uB97C \uD655\uC778\uD55C \uD6C4 \uBBF8\uB9AC\uBCF4\uAE30 \uC801\uC6A9\uC744 \uB204\uB974\uC138\uC694.");
            ObjectReviewViewModel?.SetIntelligentScissorsState(
                pending: true,
                hasPreview: true,
                statusText: previewStatus);
            RefreshPolygonOverlays();
            SetYoloCommandStatus(previewStatus, isBusy: false);
            AppendLog($"Intelligent scissors preview: segment {sourceIndex + 1} / edge {plan.EdgeIndex + 1} / {plan.PathPoints.Count} points / {plan.Elapsed.TotalMilliseconds:0.0} ms");
            return true;
        }

        private void ExecuteApplyIntelligentScissorsCommand()
        {
            if (!polygonBoundaryEditWorkflowService.HasIntelligentScissorsPreview
                || !TryResolvePendingIntelligentScissorsSource(out int sourceIndex, out LabelingSegmentationObject source))
            {
                const string previewError = "\uC801\uC6A9\uD560 \uACBD\uACC4 \uBBF8\uB9AC\uBCF4\uAE30\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.";
                SetYoloCommandStatus(previewError, isBusy: false);
                AppendLog(previewError);
                return;
            }

            var selected = WpfObjectReviewItemRef.ManualSegment(sourceIndex);
            if (!CanMutateSelectedObject(selected, requireVisible: true, out string stateError))
            {
                CancelPendingIntelligentScissors(updateStatus: false);
                SetYoloCommandStatus(stateError, isBusy: false);
                AppendLog(stateError);
                return;
            }

            const string actionName = "\uD3F4\uB9AC\uACE4 \uACBD\uACC4 \uCD94\uC885";
            WpfAnnotationHistorySnapshot beforeChange = CaptureAnnotationHistory(actionName);
            if (!polygonBoundaryEditWorkflowService.TryApplyIntelligentScissors(
                manualSegments,
                activeImageSize,
                out sourceIndex,
                out source,
                out Rectangle _,
                out string error))
            {
                CancelPendingIntelligentScissors(updateStatus: false);
                SetYoloCommandStatus(error, isBusy: false);
                AppendLog($"{actionName} skipped: {error}");
                return;
            }

            PushAnnotationHistorySnapshot(beforeChange);
            CancelPendingIntelligentScissors(updateStatus: false);
            RefreshPolygonOverlays();
            RefreshObjectListWithSelection(WpfObjectReviewItemRef.ManualSegment(sourceIndex));
            MarkAnnotationsDirty(actionName);
            QueueActiveImageQueueStatusRefresh(hasActiveCandidates: pendingDetectionCandidates.Count > 0);
            string status = $"{actionName}: {source.Points.Count}\uAC1C \uC815\uC810 / \uBBF8\uB9AC\uBCF4\uAE30 \uACBD\uB85C \uC801\uC6A9";
            SetYoloCommandStatus(status, isBusy: false);
            AppendLog(status);
        }

        private void ExecuteCancelIntelligentScissorsCommand()
            => CancelPendingIntelligentScissors(updateStatus: true);

        private bool TryResolvePendingIntelligentScissorsSource(
            out int sourceIndex,
            out LabelingSegmentationObject source)
        {
            return polygonBoundaryEditWorkflowService.TryResolveIntelligentScissorsSource(
                manualSegments,
                out sourceIndex,
                out source);
        }

        private void CancelPendingIntelligentScissors(bool updateStatus)
        {
            bool wasPending = polygonBoundaryEditWorkflowService.CancelIntelligentScissors();
            ObjectReviewViewModel?.SetIntelligentScissorsState(
                pending: false,
                hasPreview: false);
            if (MainCanvasViewModel != null)
            {
                MainCanvasViewModel.IsImagePointInputMode =
                    activeAnnotationTool == WpfAnnotationTool.Polygon
                    || activeAnnotationTool == WpfAnnotationTool.Brush
                    || activeAnnotationTool == WpfAnnotationTool.Eraser
                    || (activeAnnotationTool == WpfAnnotationTool.Select
                        && ObjectReviewViewModel?.IsSelectedSource(WpfObjectReviewSource.ManualSegment) == true);
            }

            RefreshPolygonOverlays();
            if (wasPending && updateStatus)
            {
                const string status = "\uACBD\uACC4 \uCD94\uC885 \uBBF8\uB9AC\uBCF4\uAE30\uB97C \uCDE8\uC18C\uD588\uC2B5\uB2C8\uB2E4.";
                SetYoloCommandStatus(status, isBusy: false);
                AppendLog(status);
            }
        }
        #endregion

        #region PolygonVertexCommands
        private void ExecuteBeginInsertPolygonVertexCommand()
            => BeginPendingPolygonVertexEdit(WpfPolygonVertexEditMode.Insert);

        private void ExecuteBeginDeletePolygonVertexCommand()
            => BeginPendingPolygonVertexEdit(WpfPolygonVertexEditMode.Delete);

        private void ExecuteCancelPolygonVertexEditCommand()
            => CancelPendingPolygonVertexEdit(updateStatus: true);

        private void BeginPendingPolygonVertexEdit(WpfPolygonVertexEditMode mode)
        {
            CancelPendingIntelligentScissors(updateStatus: false);
            CompleteMaskAnnotationStroke();
            FlushQueuedMaskStrokeCommits();
            if (smartMaskPromptSession.HasSession || smartMaskWorkflowService.IsRunning)
            {
                const string smartMaskError = "\uC2A4\uB9C8\uD2B8 \uB9C8\uC2A4\uD06C \uD6C4\uBCF4\uB97C \uD655\uC815\uD558\uAC70\uB098 \uCDE8\uC18C\uD55C \uB4A4 \uD3F4\uB9AC\uACE4 \uC815\uC810\uC744 \uD3B8\uC9D1\uD558\uC138\uC694.";
                SetYoloCommandStatus(smartMaskError, isBusy: false);
                AppendLog(smartMaskError);
                return;
            }

            if (!TryGetSelectedObjectReviewItem(out WpfObjectReviewItemRef selected)
                || selected.Source != WpfObjectReviewSource.ManualSegment
                || selected.Index < 0
                || selected.Index >= manualSegments.Count
                || manualSegments[selected.Index]?.IsRasterMask != false
                || activeImageSize.IsEmpty)
            {
                const string selectionError = "\uC815\uC810\uC744 \uD3B8\uC9D1\uD560 \uC218\uB3D9 \uD3F4\uB9AC\uACE4 \uAC1D\uCCB4\uB97C \uD558\uB098 \uC120\uD0DD\uD558\uC138\uC694.";
                SetYoloCommandStatus(selectionError, isBusy: false);
                AppendLog(selectionError);
                return;
            }

            if (!CanMutateSelectedObject(selected, requireVisible: true, out string stateError))
            {
                SetYoloCommandStatus(stateError, isBusy: false);
                AppendLog(stateError);
                return;
            }

            SelectAnnotationTool(WpfAnnotationTool.Select);
            polygonBoundaryEditWorkflowService.BeginPolygonVertexEdit(
                selected.Index,
                manualSegments[selected.Index],
                mode);
            ObjectReviewViewModel?.SetVertexEditPending(mode);
            MainCanvasViewModel.IsImagePointInputMode = true;
            MainCanvasViewModel.ImageViewer.SetViewMode(CanvasInteractionMode.None);
            RefreshPolygonOverlays();

            string status = mode == WpfPolygonVertexEditMode.Insert
                ? "\uC815\uC810 \uCD94\uAC00: \uC120\uD0DD\uD55C \uD3F4\uB9AC\uACE4 \uBAA8\uC11C\uB9AC \uADFC\uCC98\uB97C \uD074\uB9AD\uD558\uC138\uC694. \uC6B0\uD074\uB9AD\uC740 \uCDE8\uC18C\uC785\uB2C8\uB2E4."
                : "\uC815\uC810 \uC0AD\uC81C: \uC0AD\uC81C\uD560 \uC815\uC810 \uADFC\uCC98\uB97C \uD074\uB9AD\uD558\uC138\uC694. \uC6B0\uD074\uB9AD\uC740 \uCDE8\uC18C\uC785\uB2C8\uB2E4.";
            SetYoloCommandStatus(status, isBusy: false);
            AppendLog(status);
        }

        private bool TryApplyPendingPolygonVertexEdit(CanvasImagePointEventArgs e)
        {
            if (!polygonBoundaryEditWorkflowService.IsPolygonVertexEditPending)
            {
                return false;
            }

            if (e?.Button == CanvasPointerButton.Right)
            {
                CancelPendingPolygonVertexEdit(updateStatus: true);
                return true;
            }

            if (e?.Button != CanvasPointerButton.Left)
            {
                return true;
            }

            if (!TryResolvePendingPolygonVertexSource(out int sourceIndex, out LabelingSegmentationObject source))
            {
                CancelPendingPolygonVertexEdit(updateStatus: false);
                const string staleSelectionError = "\uC120\uD0DD\uD55C \uAC1D\uCCB4\uAC00 \uBCC0\uACBD\uB418\uC5B4 \uC815\uC810 \uD3B8\uC9D1\uC744 \uCDE8\uC18C\uD588\uC2B5\uB2C8\uB2E4.";
                SetYoloCommandStatus(staleSelectionError, isBusy: false);
                AppendLog(staleSelectionError);
                return true;
            }

            int hitTolerance = PolygonAnnotationService.ResolveImageHitTolerance(
                MainCanvasViewModel?.ImageViewer?.ZoomScale ?? 1F);
            WpfPolygonVertexEditMode mode = polygonBoundaryEditWorkflowService.PolygonVertexEditMode.Value;
            string actionName = mode == WpfPolygonVertexEditMode.Insert
                ? "\uD3F4\uB9AC\uACE4 \uC815\uC810 \uCD94\uAC00"
                : "\uD3F4\uB9AC\uACE4 \uC815\uC810 \uC0AD\uC81C";
            WpfAnnotationHistorySnapshot beforeChange = CaptureAnnotationHistory(actionName);
            bool changed = polygonBoundaryEditWorkflowService.TryApplyPolygonVertexEdit(
                manualSegments,
                e.ImagePoint,
                activeImageSize,
                hitTolerance,
                out sourceIndex,
                out source,
                out mode,
                out Rectangle _,
                out string error);
            if (!changed)
            {
                SetYoloCommandStatus(error, isBusy: false);
                AppendLog($"{actionName} skipped: {error}");
                return true;
            }

            PushAnnotationHistorySnapshot(beforeChange);
            CancelPendingPolygonVertexEdit(updateStatus: false);
            RefreshPolygonOverlays();
            RefreshObjectListWithSelection(WpfObjectReviewItemRef.ManualSegment(sourceIndex));
            MarkAnnotationsDirty(actionName);
            QueueActiveImageQueueStatusRefresh(hasActiveCandidates: pendingDetectionCandidates.Count > 0);
            string status = $"{actionName}: {source.Points.Count}\uAC1C \uC815\uC810";
            SetYoloCommandStatus(status, isBusy: false);
            AppendLog(status);
            return true;
        }

        private bool TryResolvePendingPolygonVertexSource(
            out int sourceIndex,
            out LabelingSegmentationObject source)
        {
            return polygonBoundaryEditWorkflowService.TryResolvePolygonVertexSource(
                manualSegments,
                out sourceIndex,
                out source);
        }

        private void CancelPendingPolygonVertexEdit(bool updateStatus)
        {
            bool wasPending = polygonBoundaryEditWorkflowService.CancelPolygonVertexEdit();
            ObjectReviewViewModel?.SetVertexEditPending(null);
            if (MainCanvasViewModel != null)
            {
                MainCanvasViewModel.IsImagePointInputMode =
                    activeAnnotationTool == WpfAnnotationTool.Polygon
                    || activeAnnotationTool == WpfAnnotationTool.Brush
                    || activeAnnotationTool == WpfAnnotationTool.Eraser
                    || (activeAnnotationTool == WpfAnnotationTool.Select
                        && ObjectReviewViewModel?.IsSelectedSource(WpfObjectReviewSource.ManualSegment) == true);
            }

            RefreshPolygonOverlays();
            if (wasPending && updateStatus)
            {
                const string status = "\uD3F4\uB9AC\uACE4 \uC815\uC810 \uD3B8\uC9D1\uC744 \uCDE8\uC18C\uD588\uC2B5\uB2C8\uB2E4.";
                SetYoloCommandStatus(status, isBusy: false);
                AppendLog(status);
            }
        }
        #endregion

        #region SmartMask
        private void ExecuteCreateSmartMaskCandidateCommand()
        {
            _ = ExecuteCreateSmartMaskCandidateCommandAsync();
        }

        private async Task ExecuteCreateSmartMaskCandidateCommandAsync()
        {
            if (isApplicationCloseApproved || smartMaskWorkflowService.IsRunning)
            {
                return;
            }

            bool isStartingSession = !smartMaskPromptSession.HasSession;
            string promptOverlayId = string.Empty;
            Rectangle promptBounds = Rectangle.Empty;
            if (isStartingSession)
            {
                int promptIndex = FindSmartMaskPromptIndex();
                if (promptIndex < 0 || activeImageSize.IsEmpty || string.IsNullOrWhiteSpace(activeImagePath))
                {
                    RefreshSmartMaskCommandState("결함 둘레에 사각형 박스를 먼저 그린 뒤 다시 누르세요.");
                    AppendLog("스마트 마스크: 결함 둘레에 사각형 박스를 먼저 그리세요.");
                    return;
                }

                promptOverlayId = ObjectReviewSelectionService.GetManualRoiOverlayId(manualRoiOverlayIds, promptIndex);
                promptBounds = manualRois[promptIndex];
                string className = GetManualRoiClassName(promptIndex);
                int classIdValue = global.Data.ClassNamedList.FindIndex(item =>
                    string.Equals(item?.Text, className, StringComparison.OrdinalIgnoreCase));
                int? classId = classIdValue >= 0 ? classIdValue : null;
                smartMaskPromptSession.Start(
                    activeImagePath,
                    GetCurrentSmartMaskRecipeName(),
                    promptBounds,
                    classId,
                    className);
            }

            WpfSmartMaskPromptSnapshot snapshot = smartMaskPromptSession.Capture();
            MobileSamBoxPromptRequest request = smartMaskWorkflowService.BuildRequest(
                global.Data.ProjectSettings?.PythonModel,
                snapshot.ImagePath,
                snapshot.PromptBounds,
                snapshot.ClassId,
                snapshot.ClassName,
                snapshot.Points,
                smartMaskPromptSession.MaximumPolygonPoints);
            if (!request.IsValid)
            {
                string error = string.Join(" ", request.Errors);
                if (isStartingSession)
                {
                    ResetSmartMaskPromptSession();
                }
                RefreshSmartMaskCommandState(error);
                AppendLog("스마트 마스크 준비 실패: " + error);
                return;
            }

            if (MainCanvasViewModel != null)
            {
                MainCanvasViewModel.IsTeachingMode = false;
            }
            RefreshSmartMaskCommandState("MobileSAM이 박스와 보정점을 사용해 후보 경계를 계산하고 있습니다.");
            SetYoloCommandStatus("스마트 마스크 후보 생성 중...", isBusy: true);
            SmartMaskWorkflowResult workflowResult = await smartMaskWorkflowService.RunAsync(
                new SmartMaskWorkflowRequest { Prompt = request });

            if (isApplicationCloseApproved)
            {
                return;
            }

            if (!workflowResult.Started)
            {
                RefreshSmartMaskCommandState("스마트 마스크 후보 생성이 이미 실행 중이거나 종료된 상태입니다.");
                return;
            }
            if (workflowResult.IsCanceled)
            {
                RefreshSmartMaskCommandState("후보 생성을 취소했습니다. 프롬프트는 유지되며 다시 실행할 수 있습니다.");
                SetYoloCommandStatus("스마트 마스크 후보 생성 취소", isBusy: false);
                AppendLog("스마트 마스크 후보 생성을 취소했습니다.");
                return;
            }
            if (workflowResult.Error != null)
            {
                string workflowError = workflowResult.Error.Message;
                RefreshSmartMaskCommandState(workflowError);
                SetYoloCommandStatus("스마트 마스크 실패: " + workflowError, isBusy: false);
                AppendLog("스마트 마스크 실패: " + workflowError);
                return;
            }

            MobileSamBoxPromptResult result = workflowResult.Result;

            if (!smartMaskPromptSession.Matches(
                    snapshot,
                    activeImagePath,
                    GetCurrentSmartMaskRecipeName()))
            {
                RefreshSmartMaskCommandState("이미지, 레시피 또는 프롬프트가 변경되어 이전 결과를 적용하지 않았습니다.");
                AppendLog("스마트 마스크 결과 무시: 실행 중 이미지, 레시피 또는 프롬프트가 변경되었습니다.");
                return;
            }
            if (!result.Succeeded || result.Candidate == null)
            {
                RefreshSmartMaskCommandState(result.Error);
                SetYoloCommandStatus("스마트 마스크 실패: " + result.Error, isBusy: false);
                AppendLog("스마트 마스크 실패: " + result.Error);
                return;
            }

            if (isStartingSession)
            {
                int currentPromptIndex = ObjectReviewSelectionService.FindManualRoiIndexByOverlayId(manualRoiOverlayIds, promptOverlayId);
                if (currentPromptIndex < 0 || manualRois[currentPromptIndex] != promptBounds)
                {
                    smartMaskPromptSession.Reset();
                    RefreshSmartMaskCommandState("프롬프트 박스가 변경되어 후보를 적용하지 않았습니다.");
                    AppendLog("스마트 마스크 결과 무시: 프롬프트 박스가 변경되었습니다.");
                    return;
                }

                RegisterAnnotationHistoryBeforeChange("박스를 스마트 마스크 프롬프트로 전환", markDirty: false);
                manualRois.RemoveAt(currentPromptIndex);
                RemoveAtIfPresent(manualRoiClassNames, currentPromptIndex);
                RemoveAtIfPresent(manualRoiShapeKinds, currentPromptIndex);
                RemoveAtIfPresent(manualRoiOverlayIds, currentPromptIndex);
            }
            else
            {
                RegisterAnnotationHistoryBeforeChange("스마트 마스크 후보 다시 생성", markDirty: false);
            }

            // Candidate Review still owns the one visible pending candidate. The Smart Mask
            // session retains only the initial/latest alternatives until the object is resolved.
            ApplyDetectionCandidatesPreservingConfirmed(new[] { result.Candidate }, succeeded: true);
            smartMaskPromptSession.RecordCandidate(result.Candidate);
            RefreshPolygonOverlays();
            SetYoloCommandStatus(result.Summary + " / 확정 전 후보", isBusy: false);
            AppendLog($"{result.Summary} / {result.RuntimeSummary} / mask area {result.MaskArea}");
            RefreshSmartMaskCommandState("자동 후보가 부족할 때 보정 옵션에서 한 점을 추가하고 다시 생성해 비교하세요. 확정 전에는 저장되지 않습니다.");
        }

        private void ExecuteSetSmartMaskPointModeCommand(WpfSmartMaskPointInputMode mode)
        {
            if (!smartMaskPromptSession.HasSession || smartMaskWorkflowService.IsRunning)
            {
                return;
            }

            WpfSmartMaskPointInputMode nextMode = smartMaskPromptSession.InputMode == mode
                ? WpfSmartMaskPointInputMode.None
                : mode;
            smartMaskPromptSession.SetInputMode(nextMode);
            if (MainCanvasViewModel != null)
            {
                MainCanvasViewModel.IsTeachingMode = false;
                MainCanvasViewModel.IsImagePointInputMode = nextMode != WpfSmartMaskPointInputMode.None;
                MainCanvasViewModel.ImageViewer.SetViewMode(
                    activeAnnotationTool == WpfAnnotationTool.PanZoom
                        ? CanvasInteractionMode.Drag
                        : CanvasInteractionMode.None);
            }

            RefreshSmartMaskCommandState(nextMode == WpfSmartMaskPointInputMode.Positive
                ? "캔버스에서 객체에 포함할 위치를 클릭하세요."
                : nextMode == WpfSmartMaskPointInputMode.Negative
                    ? "캔버스에서 후보에서 제외할 위치를 클릭하세요."
                    : "보정점 입력을 종료했습니다.");
        }

        private void ExecuteCancelSmartMaskGenerationCommand()
        {
            if (!smartMaskWorkflowService.IsRunning)
            {
                return;
            }

            smartMaskWorkflowService.Cancel();
            RefreshSmartMaskCommandState("후보 생성을 취소하는 중입니다.");
        }

        private void ExecuteUndoSmartMaskPointCommand()
        {
            if (smartMaskPromptSession.UndoPoint())
            {
                RefreshPolygonOverlays();
                RefreshSmartMaskCommandState("마지막 보정점을 취소했습니다. 후보 다시 생성을 눌러 반영하세요.");
            }
        }

        private void ExecuteClearSmartMaskPointsCommand()
        {
            if (smartMaskPromptSession.ClearPoints())
            {
                RefreshPolygonOverlays();
                RefreshSmartMaskCommandState("모든 보정점을 지웠습니다. 후보 다시 생성을 눌러 반영하세요.");
            }
        }

        private void ExecuteSetSmartMaskPolygonDetailCommand(WpfSmartMaskPolygonDetail detail)
        {
            smartMaskPromptSession.SetPolygonDetail(detail);
            RefreshSmartMaskCommandState();
        }

        private void ExecuteNextSmartMaskInstanceCommand()
        {
            if (!smartMaskPromptSession.HasSession
                || !smartMaskPromptSession.HasProducedCandidate
                || candidateReviewState.HasPendingCandidates
                || smartMaskWorkflowService.IsRunning)
            {
                RefreshSmartMaskCommandState("현재 후보를 먼저 확정하거나 스킵하세요.");
                return;
            }

            ResetSmartMaskPromptSession();
            SelectAnnotationTool(WpfAnnotationTool.Rectangle);
            string nextDetail = CanvasPanelViewModel?.IsSmartMaskAutoContourEnabled == true
                ? "다음 객체를 사각형으로 감싸면 자동 윤곽 후보가 바로 생성됩니다."
                : "다음 객체 둘레에 사각형 박스를 그린 뒤 자동 윤곽 옵션을 켜세요.";
            RefreshSmartMaskCommandState(nextDetail);
            SetYoloCommandStatus("스마트 마스크: 다음 객체 박스를 기다립니다.", isBusy: false);
        }

        private void ExecuteSetSmartMaskAutoContourMode(bool enabled)
        {
            EnsureProjectSettings();
            global.Data.ProjectSettings.SmartMaskAutoContourEnabled = enabled;
            string recipeName = GetCurrentRecipeName();
            if (!string.IsNullOrWhiteSpace(recipeName))
            {
                try
                {
                    RecipeConfigurationSaveResult saveResult = projectRecipeSessionService.SaveConfiguration(
                        global.Data,
                        recipeName,
                        updateYoloDataYaml: false,
                        refreshDatasetVersion: false);
                    if (!saveResult.IsSuccess)
                    {
                        AppendLog("\uC790\uB3D9 \uC724\uACFD \uC635\uC158 \uC800\uC7A5 \uC2E4\uD328: " + saveResult.ErrorMessage);
                    }
                }
                catch (Exception error)
                {
                    AppendLog("자동 윤곽 옵션 저장 실패: " + error.Message);
                }
            }

            if (enabled)
            {
                SelectAnnotationTool(WpfAnnotationTool.Rectangle);
                RefreshSmartMaskCommandState("자동 윤곽 켜짐 · 새 사각형을 완성하면 MobileSAM 후보를 바로 만듭니다.");
                SetYoloCommandStatus("라벨링 옵션: 자동 윤곽 켜짐", isBusy: false);
                AppendLog("자동 윤곽 켜짐: 새 사각형마다 검토용 윤곽 후보를 자동 생성합니다.");
                return;
            }

            RefreshSmartMaskCommandState("자동 윤곽 꺼짐 · 사각형은 일반 박스 라벨로 유지됩니다.");
            SetYoloCommandStatus("라벨링 옵션: 일반 박스", isBusy: false);
            AppendLog("자동 윤곽 꺼짐: 새 사각형을 일반 박스 라벨로 유지합니다.");
        }

        private void TryStartAutoSmartMaskForNewRoi(CanvasRect<float> roiRect)
        {
            if (roiRect == null
                || roiRect.ShapeKind != CanvasRoiShapeKind.Rectangle
                || CanvasPanelViewModel?.IsSmartMaskAutoContourEnabled != true
                || global.Data.ProjectSettings?.DatasetPurpose != LabelingDatasetPurpose.Segmentation
                || activeAnnotationTool != WpfAnnotationTool.Rectangle
                || smartMaskPromptSession.HasSession
                || candidateReviewState.HasPendingCandidates
                || smartMaskWorkflowService.IsRunning)
            {
                return;
            }

            AppendLog("자동 윤곽: 새 사각형을 MobileSAM 프롬프트로 사용합니다.");
            ExecuteCreateSmartMaskCandidateCommand();
        }

        private void ContinueAutoSmartMaskAfterResolvedCandidate(string resolution)
        {
            if (CanvasPanelViewModel?.IsSmartMaskAutoContourEnabled != true
                || candidateReviewState.HasPendingCandidates)
            {
                return;
            }

            ResetSmartMaskPromptSession();
            SelectAnnotationTool(WpfAnnotationTool.Rectangle);
            RefreshSmartMaskCommandState($"{resolution} 완료 · 다음 객체를 사각형으로 감싸세요.");
            SetYoloCommandStatus($"자동 윤곽: {resolution} 완료 · 다음 박스 대기", isBusy: false);
        }

        private void ExecuteSelectSmartMaskCandidateVersionCommand(WpfSmartMaskCandidateVersion version)
        {
            if (smartMaskWorkflowService.IsRunning
                || !candidateReviewState.HasPendingCandidates
                || !smartMaskPromptSession.TrySelectCandidate(version, out YoloWorkerSmokeCandidate candidate))
            {
                RefreshSmartMaskCommandState("비교할 Smart Mask 후보가 없습니다.");
                return;
            }

            candidateReviewState.LoadPendingCandidates(new[] { candidate }, clearConfirmed: false);
            ApplyCanvasDisplayMode(WpfCanvasDisplayMode.InferenceOnly, redraw: false, logChange: false);
            RefreshCandidateListWithPreferred(candidate);
            RefreshObjectList();
            RedrawReviewRois();
            RefreshCanvasWorkflowContext();
            string versionText = version == WpfSmartMaskCandidateVersion.Initial ? "이전" : "현재";
            AddCandidateReviewHistory($"Smart Mask {versionText} 후보 보기 · 확정 전");
            SetYoloCommandStatus($"Smart Mask {versionText} 후보 선택 / 확정 전", isBusy: false);
            AppendLog($"Smart Mask {versionText} 후보로 전환했습니다. 확정 전에는 저장되지 않습니다.");
            RefreshSmartMaskCommandState($"{versionText} 후보를 보고 있습니다. 확정하면 이 후보만 저장됩니다.");
        }

        private bool TryApplySmartMaskPointInput(CanvasImagePointEventArgs e)
        {
            if (e == null
                || !smartMaskPromptSession.HasSession
                || smartMaskPromptSession.InputMode == WpfSmartMaskPointInputMode.None)
            {
                return false;
            }

            if (e.Button == CanvasPointerButton.Right)
            {
                ExecuteUndoSmartMaskPointCommand();
                return true;
            }
            if (e.Button != CanvasPointerButton.Left)
            {
                return true;
            }
            if (!smartMaskPromptSession.TryAddPoint(e.ImagePoint, activeImageSize))
            {
                return true;
            }

            RefreshPolygonOverlays();
            RefreshSmartMaskCommandState("보정점을 추가했습니다. 후보 다시 생성으로 효과를 비교한 뒤 부족할 때 다음 점을 추가하세요.");
            return true;
        }

        private void ResetSmartMaskPromptSession()
        {
            smartMaskWorkflowService.Cancel();
            smartMaskPromptSession.Reset();
            if (MainCanvasViewModel != null)
            {
                MainCanvasViewModel.IsImagePointInputMode = false;
                MainCanvasViewModel.IsTeachingMode =
                    activeAnnotationTool == WpfAnnotationTool.Rectangle
                    || activeAnnotationTool == WpfAnnotationTool.Ellipse;
                MainCanvasViewModel.ImageViewer.SetViewMode(
                    activeAnnotationTool == WpfAnnotationTool.PanZoom
                        ? CanvasInteractionMode.Drag
                        : CanvasInteractionMode.None);
            }
            RefreshPolygonOverlays();
        }

        private string GetCurrentSmartMaskRecipeName()
            => global.Recipe?.Name ?? string.Empty;

        private int FindSmartMaskPromptIndex()
        {
            EnsureManualRoiMetadataCount();
            for (int index = manualRois.Count - 1; index >= 0; index--)
            {
                if (ObjectReviewPresentationService.GetManualRoiShapeKind(manualRoiShapeKinds, index) == CanvasRoiShapeKind.Rectangle
                    && !manualRois[index].IsEmpty)
                {
                    return index;
                }
            }

            return -1;
        }

        private void RefreshSmartMaskCommandState(string detail = "")
        {
            if (CanvasPanelViewModel == null)
            {
                return;
            }

            bool isVisible = global.Data.ProjectSettings?.DatasetPurpose == LabelingDatasetPurpose.Segmentation;
            if (smartMaskPromptSession.HasSession
                && !smartMaskPromptSession.MatchesContext(activeImagePath, GetCurrentSmartMaskRecipeName()))
            {
                ResetSmartMaskPromptSession();
            }
            bool hasSession = isVisible && smartMaskPromptSession.HasSession;
            int promptIndex = isVisible && !hasSession ? FindSmartMaskPromptIndex() : -1;
            string effectiveDetail = detail;
            bool isReady = false;
            if (isVisible && !smartMaskWorkflowService.IsRunning && !activeImageSize.IsEmpty)
            {
                WpfSmartMaskPromptSnapshot snapshot = hasSession
                    ? smartMaskPromptSession.Capture()
                    : promptIndex >= 0
                        ? new WpfSmartMaskPromptSnapshot
                        {
                            ImagePath = activeImagePath,
                            PromptBounds = manualRois[promptIndex],
                            ClassName = GetManualRoiClassName(promptIndex)
                        }
                        : null;
                if (snapshot != null)
                {
                    MobileSamBoxPromptRequest request = smartMaskWorkflowService.BuildRequest(
                        global.Data.ProjectSettings?.PythonModel,
                        snapshot.ImagePath,
                        snapshot.PromptBounds,
                        snapshot.ClassId,
                        snapshot.ClassName,
                        snapshot.Points,
                        hasSession ? smartMaskPromptSession.MaximumPolygonPoints : 96);
                    isReady = request.IsValid;
                    if (string.IsNullOrWhiteSpace(effectiveDetail) && !isReady)
                    {
                        effectiveDetail = string.Join(" ", request.Errors);
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(effectiveDetail))
            {
                effectiveDetail = hasSession
                    ? "포함점/제외점을 한 점씩 추가하고 다시 생성해 비교하세요. 다시 생성하면 대기 후보 하나를 교체합니다."
                    : promptIndex < 0
                        ? "결함 둘레에 사각형 박스를 그리면 MobileSAM 후보 마스크를 만들 수 있습니다."
                        : "마지막 사각형을 시작 박스로 사용합니다. 결과는 확정 전 후보로만 표시됩니다.";
            }

            CanvasPanelViewModel.SetSmartMaskState(
                isVisible,
                isReady,
                smartMaskWorkflowService.IsRunning,
                effectiveDetail,
                hasSession);
            CanvasPanelViewModel.SetSmartMaskSessionState(
                hasSession,
                smartMaskWorkflowService.IsRunning,
                smartMaskPromptSession.PositivePointCount,
                smartMaskPromptSession.NegativePointCount,
                smartMaskPromptSession.InputMode,
                smartMaskPromptSession.HasProducedCandidate,
                smartMaskPromptSession.HasProducedCandidate && !candidateReviewState.HasPendingCandidates,
                smartMaskPromptSession.HasCandidateComparison
                    && candidateReviewState.PendingCandidates.Count == 1
                    && smartMaskPromptSession.IsSelectedCandidate(candidateReviewState.PendingCandidates[0]),
                smartMaskPromptSession.SelectedCandidateVersion);
        }
        #endregion

    }
}
