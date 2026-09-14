using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.Canvas;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using OpenVisionLab.ImageCanvas.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using DrawingColor = System.Drawing.Color;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingSize = System.Drawing.Size;

namespace MvcVisionSystem
{
    // Owns annotation canvas synchronization and overlay projection. The Shell
    // supplies mutable state and presentation callbacks; no Window/control is
    // referenced here, so rendering policy can be tested without the visual tree.
    internal sealed class AnnotationRenderingAdapter
    {
        private readonly AnnotationRenderingAdapterContext context;

        internal AnnotationRenderingAdapter(AnnotationRenderingAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            if (context.DataProvider == null) throw new ArgumentNullException(nameof(context.DataProvider));
            if (context.ManualRois == null) throw new ArgumentNullException(nameof(context.ManualRois));
            if (context.ManualRoiClassNames == null) throw new ArgumentNullException(nameof(context.ManualRoiClassNames));
            if (context.ManualRoiShapeKinds == null) throw new ArgumentNullException(nameof(context.ManualRoiShapeKinds));
            if (context.ManualRoiOverlayIds == null) throw new ArgumentNullException(nameof(context.ManualRoiOverlayIds));
            if (context.ManualSegments == null) throw new ArgumentNullException(nameof(context.ManualSegments));
            if (context.PolygonAnnotationService == null) throw new ArgumentNullException(nameof(context.PolygonAnnotationService));
            if (context.HolePolygonAnnotationService == null) throw new ArgumentNullException(nameof(context.HolePolygonAnnotationService));
            if (context.PolygonBoundaryEditWorkflowService == null) throw new ArgumentNullException(nameof(context.PolygonBoundaryEditWorkflowService));
            if (context.SmartMaskPromptSession == null) throw new ArgumentNullException(nameof(context.SmartMaskPromptSession));
            if (context.FourPointBoxService == null) throw new ArgumentNullException(nameof(context.FourPointBoxService));
            if (context.ClassCatalogWorkflowService == null) throw new ArgumentNullException(nameof(context.ClassCatalogWorkflowService));
        }

        private DrawingSize activeImageSize => context.ActiveImageSizeProvider?.Invoke() ?? DrawingSize.Empty;
        private string activeImagePath => context.ActiveImagePathProvider?.Invoke() ?? string.Empty;
        private List<DrawingRectangle> manualRois => context.ManualRois;
        private List<string> manualRoiClassNames => context.ManualRoiClassNames;
        private List<CanvasRoiShapeKind> manualRoiShapeKinds => context.ManualRoiShapeKinds;
        private List<string> manualRoiOverlayIds => context.ManualRoiOverlayIds;
        private List<LabelingSegmentationObject> manualSegments => context.ManualSegments;
        private PolygonAnnotationService polygonAnnotationService => context.PolygonAnnotationService;
        private PolygonAnnotationService holePolygonAnnotationService => context.HolePolygonAnnotationService;
        private PolygonBoundaryEditWorkflowService polygonBoundaryEditWorkflowService => context.PolygonBoundaryEditWorkflowService;
        private SmartMaskPromptSessionService smartMaskPromptSession => context.SmartMaskPromptSession;
        private FourPointBoxService fourPointBoxService => context.FourPointBoxService;
        private ClassCatalogWorkflowService classCatalogWorkflowService => context.ClassCatalogWorkflowService;
        private WpfCanvasPanelViewModel CanvasPanelViewModel => context.CanvasPanelViewModelProvider?.Invoke();
        private RoiImageCanvasViewModel MainCanvasViewModel => context.MainCanvasViewModelProvider?.Invoke();
        private WpfLearningWorkflowPanelViewModel LearningWorkflowViewModel => context.LearningWorkflowViewModelProvider?.Invoke();
        private WpfObjectReviewPanelViewModel ObjectReviewViewModel => context.ObjectReviewViewModelProvider?.Invoke();
        private AnnotationSegmentEditAdapter annotationSegmentEditAdapter => context.AnnotationSegmentEditAdapter;
        private WpfSegmentationHoleEditMode? pendingSegmentationHoleEditMode => context.PendingSegmentationHoleEditModeProvider?.Invoke();
        private IReadOnlyList<YoloWorkerSmokeCandidate> pendingDetectionCandidates
            => context.PendingDetectionCandidatesProvider?.Invoke() ?? Array.Empty<YoloWorkerSmokeCandidate>();
        private IReadOnlyList<YoloWorkerSmokeCandidate> confirmedDetectionCandidates
            => context.ConfirmedDetectionCandidatesProvider?.Invoke() ?? Array.Empty<YoloWorkerSmokeCandidate>();
        private bool isApplicationCloseApproved => IsApplicationCloseApproved();

        private static string FirstNonEmpty(params string[] values)
            => values?.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

        private bool IsApplicationCloseApproved()
            => context.IsApplicationCloseApprovedProvider?.Invoke() == true;

        private string GetSelectedClassName()
            => context.SelectedClassNameProvider?.Invoke() ?? string.Empty;

        private string GetManualRoiClassName(int index)
            => context.ManualRoiClassNameProvider?.Invoke(index) ?? string.Empty;

        private WpfObjectSessionState GetManualSegmentSessionState(int index)
            => context.ManualSegmentSessionStateProvider?.Invoke(index) ?? WpfObjectSessionState.Default;

        private WpfObjectSessionState GetManualRoiSessionState(int index)
            => context.ManualRoiSessionStateProvider?.Invoke(index) ?? WpfObjectSessionState.Default;

        private bool IsPendingRemoveUnderlyingAffectedIndex(int sourceIndex)
            => context.IsPendingRemoveUnderlyingAffectedIndexProvider?.Invoke(sourceIndex) == true;

        private bool IsSegmentationDatasetPurposeActive()
            => context.IsSegmentationDatasetPurposeActiveProvider?.Invoke() == true;

        private IReadOnlyCollection<int> GetActiveMaskStrokeSegmentIndices()
            => context.ActiveMaskStrokeSegmentIndicesProvider?.Invoke() ?? Array.Empty<int>();

        private bool HasActiveMaskStrokeFullObjectRefresh()
            => context.HasActiveMaskStrokeFullObjectRefreshProvider?.Invoke() == true;

        private bool ShouldSelectCommittedMaskAfterStroke()
            => context.ShouldSelectCommittedMaskAfterStrokeProvider?.Invoke() == true;

        private YoloWorkerSmokeCandidate GetSelectedCandidate()
            => context.SelectedCandidateProvider?.Invoke();

        private void RegisterAnnotationHistoryBeforeChange(string actionName, bool markDirty = true)
            => context.RegisterAnnotationHistoryBeforeChange?.Invoke(actionName, markDirty);

        private void RegisterRoiEditHistoryBeforeChange(string overlayId, string actionName)
            => context.RegisterRoiEditHistoryBeforeChange?.Invoke(overlayId, actionName);

        private WpfAnnotationHistorySnapshot CaptureManualRoiHistory(string actionName)
            => context.CaptureManualRoiHistory?.Invoke(actionName);

        private void PushAnnotationHistorySnapshot(WpfAnnotationHistorySnapshot snapshot, bool markDirty = true)
            => context.PushAnnotationHistorySnapshot?.Invoke(snapshot, markDirty);

        private void ResetActiveRoiEditHistory()
            => context.ResetActiveRoiEditHistory?.Invoke();

        private bool TryRefreshManualRoiObjectReviewRow(int manualRoiIndex, bool select)
            => context.TryRefreshManualRoiObjectReviewRow?.Invoke(manualRoiIndex, select) == true;

        private void RefreshObjectListWithSelection(WpfObjectReviewItemRef preferredSelection)
            => context.RefreshObjectListWithSelection?.Invoke(preferredSelection);

        private void RefreshObjectList()
            => context.RefreshObjectList?.Invoke();

        private void RefreshObjectReviewAfterDelete(WpfObjectReviewSource deletedSource, int deletedObjectRowIndex)
            => context.RefreshObjectReviewAfterDelete?.Invoke(deletedSource, deletedObjectRowIndex);

        private void QueueActiveImageQueueStatusRefresh(bool hasActiveCandidates)
            => context.QueueActiveImageQueueStatusRefresh?.Invoke(hasActiveCandidates);

        private void RefreshActiveImageQueueStatus(bool hasActiveCandidates)
            => context.RefreshActiveImageQueueStatus?.Invoke(hasActiveCandidates);

        private void ShowSavedLabelsWorkflowView()
            => context.ShowSavedLabelsWorkflowView?.Invoke();

        private void SetModelStatus(string text)
            => context.SetModelStatus?.Invoke(text);

        private void SetYoloCommandStatus(string text, bool isBusy)
            => context.SetYoloCommandStatus?.Invoke(text, isBusy);

        private void AppendLog(string message)
            => context.AppendLog?.Invoke(message);

        private void RefreshSmartMaskCommandState()
            => context.RefreshSmartMaskCommandState?.Invoke();

        private void TryStartAutoSmartMaskForNewRoi(CanvasRect<float> roiRect)
            => context.TryStartAutoSmartMaskForNewRoi?.Invoke(roiRect);


        #region AnnotationCanvas
        // Class-aware drawing policy stays in the shell because the canvas library should not know labeling class names.
        internal bool ShouldDrawOverExistingRoiForCurrentClass(CanvasRect<float> roiRect)
        {
            if (roiRect == null || string.IsNullOrWhiteSpace(roiRect.UniqueId))
            {
                return false;
            }

            string currentClass = ClassCatalogService.NormalizeClassName(GetSelectedClassName());
            if (string.IsNullOrWhiteSpace(currentClass))
            {
                return false;
            }

            int index = ObjectReviewSelectionService.FindManualRoiIndexByOverlayId(manualRoiOverlayIds, roiRect.UniqueId);
            if (index < 0 || index >= manualRoiClassNames.Count)
            {
                return false;
            }

            string existingClass = ClassCatalogService.NormalizeClassName(manualRoiClassNames[index]);
            return !string.IsNullOrWhiteSpace(existingClass)
                && !string.Equals(currentClass, existingClass, StringComparison.OrdinalIgnoreCase);
        }

        internal DrawingColor GetClassDrawColor(string className)
        {
            LabelingProjectData data = context.DataProvider?.Invoke();
            LabelClass classItem = classCatalogWorkflowService.EnsureClassItem(data, FirstNonEmpty(className, "Defect"));
            return classItem?.DrawColor ?? DrawingColor.FromArgb(34, 197, 94);
        }

        internal DrawingColor GetManualRoiDrawColor(int index)
            => GetClassDrawColor(GetManualRoiClassName(index));

        internal string ResolveNewManualRoiClassName(CanvasRect<float> roiRect)
        {
            string copiedClassName = ClassCatalogService.NormalizeClassName(roiRect?.UserTag);
            return FirstNonEmpty(copiedClassName, GetSelectedClassName(), "Defect");
        }

        internal void ApplyManualRoiOverlayColor(int index, bool refreshImmediately = false)
        {
            if (index < 0 || index >= manualRois.Count)
            {
                return;
            }

            string className = GetManualRoiClassName(index);
            MainCanvasViewModel?.SetRoiOverlayUserTag(
                ObjectReviewSelectionService.GetManualRoiOverlayId(manualRoiOverlayIds, index),
                className);
            MainCanvasViewModel?.SetRoiOverlayColor(
                ObjectReviewSelectionService.GetManualRoiOverlayId(manualRoiOverlayIds, index),
                GetClassDrawColor(className),
                refreshImmediately);
        }

        // Canvas annotation synchronization stays separate from tool input handling so ROI/overlay model mutations are easy to audit.
        internal void MainCanvasViewModel_RoiAdded(object sender, OpenVisionLab.ImageCanvas.Model.RoiChangedEventArgs e)
        {
            if (isApplicationCloseApproved || e?.RoiRect == null || activeImageSize.IsEmpty)
            {
                return;
            }

            DrawingRectangle bounds = ConvertCanvasRectToImageBounds(e.RoiRect);
            if (bounds.IsEmpty)
            {
                return;
            }

            string overlayId = e.RoiRect.UniqueId ?? string.Empty;
            int existingIndex = ObjectReviewSelectionService.FindManualRoiIndexByOverlayId(manualRoiOverlayIds, overlayId);
            bool addedNewManualRoi = existingIndex < 0;
            if (existingIndex >= 0)
            {
                if (manualRois[existingIndex] != bounds
                    || ObjectReviewPresentationService.GetManualRoiShapeKind(manualRoiShapeKinds, existingIndex) != e.RoiRect.ShapeKind)
                {
                    RegisterAnnotationHistoryBeforeChange("박스 수정");
                }

                manualRois[existingIndex] = bounds;
                manualRoiShapeKinds[existingIndex] = e.RoiRect.ShapeKind;
                e.RoiRect.UserTag = GetManualRoiClassName(existingIndex);
                ApplyManualRoiOverlayColor(existingIndex);
            }
            else
            {
                RegisterAnnotationHistoryBeforeChange("박스 추가");
                string className = ResolveNewManualRoiClassName(e.RoiRect);
                e.RoiRect.UserTag = className;
                manualRois.Add(bounds);
                manualRoiClassNames.Add(className);
                manualRoiShapeKinds.Add(e.RoiRect.ShapeKind);
                manualRoiOverlayIds.Add(overlayId);
                ApplyManualRoiOverlayColor(manualRois.Count - 1);
            }

            RefreshObjectListWithSelection(CreateManualRoiSelection(e.RoiRect));
            ShowSavedLabelsWorkflowView();
            string shapeName = ObjectReviewPresentationService.FormatManualRoiShapeName(e.RoiRect.ShapeKind);
            SetModelStatus($"라벨 추가: {shapeName} {CandidateReviewPresenter.FormatBoundsCompact(bounds)}");
            AppendLog($"라벨 추가({shapeName}): {bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}");
            RefreshSmartMaskCommandState();
            if (addedNewManualRoi)
            {
                TryStartAutoSmartMaskForNewRoi(e.RoiRect);
            }
        }

        internal void MainCanvasViewModel_RoiEditingCompleted(object sender, OpenVisionLab.ImageCanvas.Model.RoiChangedEventArgs e)
        {
            if (isApplicationCloseApproved || e?.RoiRect == null || activeImageSize.IsEmpty)
            {
                return;
            }

            UpdateManualRoiFromCanvasRect(e.RoiRect);
        }

        internal void MainCanvasViewModel_RoiMouseUp(object sender, OpenVisionLab.ImageCanvas.Model.RoiChangedEventArgs e)
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            WpfObjectReviewItemRef selectedManualRoi = null;
            bool updatedSingleObjectRow = false;
            if (e?.RoiRect != null)
            {
                UpdateManualRoiFromCanvasRect(e.RoiRect);
                selectedManualRoi = CreateManualRoiSelection(e.RoiRect);
                updatedSingleObjectRow = selectedManualRoi != null
                    && TryRefreshManualRoiObjectReviewRow(selectedManualRoi.Index, select: true);
            }

            ResetActiveRoiEditHistory();
            if (!updatedSingleObjectRow)
            {
                RefreshObjectListWithSelection(selectedManualRoi);
            }
        }


        internal void MainCanvasViewModel_RemoveRoiRequested(object sender, CanvasRect<float> rect)
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            int index = ObjectReviewSelectionService.FindManualRoiIndexByOverlayId(manualRoiOverlayIds, rect?.UniqueId);
            if (index < 0)
            {
                return;
            }

            PushAnnotationHistorySnapshot(CaptureManualRoiHistory("박스 삭제"));
            manualRois.RemoveAt(index);
            RemoveAtIfPresent(manualRoiClassNames, index);
            RemoveAtIfPresent(manualRoiShapeKinds, index);
            RemoveAtIfPresent(manualRoiOverlayIds, index);
            // Canvas ViewModel owns the OpenGL overlay removal after this event; the shell only updates model/review state here.
            RefreshObjectReviewAfterDelete(WpfObjectReviewSource.ManualRoi, index);
            QueueActiveImageQueueStatusRefresh(hasActiveCandidates: pendingDetectionCandidates.Count > 0);
            RefreshSmartMaskCommandState();
        }

        // ROI metadata helpers stay beside the Canvas event adapter because
        // overlay IDs, shape kinds, and review-row references form one state
        // boundary for manual ROI edits.
        internal void UpdateManualRoiFromCanvasRect(CanvasRect<float> rect)
        {
            int index = ObjectReviewSelectionService.FindManualRoiIndexByOverlayId(manualRoiOverlayIds, rect?.UniqueId);
            if (index < 0)
            {
                return;
            }

            DrawingRectangle bounds = ConvertCanvasRectToImageBounds(rect);
            if (bounds.IsEmpty)
            {
                return;
            }

            if (manualRois[index] != bounds
                || ObjectReviewPresentationService.GetManualRoiShapeKind(manualRoiShapeKinds, index) != rect.ShapeKind)
            {
                RegisterRoiEditHistoryBeforeChange(rect.UniqueId, "박스 수정");
            }

            manualRois[index] = bounds;
            manualRoiShapeKinds[index] = rect.ShapeKind;
        }

        internal WpfObjectReviewItemRef CreateManualRoiSelection(CanvasRect<float> rect)
        {
            int index = ObjectReviewSelectionService.FindManualRoiIndexByOverlayId(manualRoiOverlayIds, rect?.UniqueId);
            return index >= 0 ? WpfObjectReviewItemRef.Manual(index, rect?.UniqueId) : null;
        }

        internal DrawingRectangle ConvertCanvasRectToImageBounds(CanvasRect<float> rect)
        {
            if (rect == null || rect.IsEmpty() || activeImageSize.IsEmpty)
            {
                return DrawingRectangle.Empty;
            }

            var raw = new DrawingRectangle(
                (int)Math.Round(rect.Left),
                (int)Math.Round(activeImageSize.Height - rect.Top),
                (int)Math.Round(rect.Width),
                (int)Math.Round(rect.Height));

            return DrawingRectangle.Intersect(
                raw,
                new DrawingRectangle(0, 0, activeImageSize.Width, activeImageSize.Height));
        }

        internal void EnsureManualRoiMetadataCount()
        {
            while (manualRoiShapeKinds.Count < manualRois.Count)
            {
                manualRoiShapeKinds.Add(CanvasRoiShapeKind.Rectangle);
            }

            while (manualRoiOverlayIds.Count < manualRois.Count)
            {
                manualRoiOverlayIds.Add(string.Empty);
            }
        }

        internal void RedrawReviewRois()
        {
            EnsureManualRoiMetadataCount();
            context.RefreshCanvasLayerVisibilityState?.Invoke();
            if (MainCanvasViewModel == null)
            {
                return;
            }

            bool showLabels = CanvasPanelViewModel?.IsLabelLayerVisible == true;
            bool showInference = CanvasPanelViewModel?.IsInferenceLayerVisible == true;
            using (MainCanvasViewModel.ImageViewer.SuppressRefresh())
            {
                MainCanvasViewModel.ClearRois();
                if (showLabels)
                {
                    for (int i = 0; i < manualRois.Count; i++)
                    {
                        DrawingRectangle roi = manualRois[i];
                        WpfObjectSessionState sessionState = GetManualRoiSessionState(i);
                        if (roi.IsEmpty || sessionState.IsHidden)
                        {
                            manualRoiOverlayIds[i] = string.Empty;
                            continue;
                        }

                        string className = GetManualRoiClassName(i);
                        var overlay = MainCanvasViewModel.AddInitialRoi(
                            roi,
                            ObjectReviewPresentationService.GetManualRoiShapeKind(manualRoiShapeKinds, i),
                            GetClassDrawColor(className),
                            className);
                        manualRoiOverlayIds[i] = overlay?.UniqueId ?? string.Empty;
                        var overlayItem = MainCanvasViewModel.ImageViewer
                            .GetCanvasOverlayManager()
                            .GetOverlayByUniqueId(manualRoiOverlayIds[i]);
                        if (overlayItem != null)
                        {
                            overlayItem.IsControlLock = sessionState.IsLocked;
                            overlayItem.IsMoveLock = sessionState.IsPinned;
                        }
                    }

                    foreach (YoloWorkerSmokeCandidate candidate in confirmedDetectionCandidates)
                    {
                        DrawingRectangle bounds = CandidateReviewPresentationService.ClipCandidateBounds(candidate, activeImageSize);
                        if (!bounds.IsEmpty)
                        {
                            MainCanvasViewModel.AddInitialRoi(
                                bounds,
                                CanvasRoiShapeKind.Rectangle,
                                GetClassDrawColor(candidate.ClassName),
                                candidate.ClassName);
                        }
                    }
                }

                if (showInference)
                {
                    MainCanvasViewModel.SetDetectionOverlays(
                        CandidateReviewPresentationService.BuildDetectionOverlays(
                            pendingDetectionCandidates,
                            GetSelectedCandidate(),
                            activeImageSize,
                            context.IsCandidateConfirmableProvider));
                }
                else
                {
                    MainCanvasViewModel.SetDetectionOverlays(Array.Empty<RoiImageCanvasDetectionOverlay>());
                }

                if (showLabels)
                {
                    RefreshPolygonOverlays();
                }
                else
                {
                    ClearSegmentationOverlays();
                }
            }

            MainCanvasViewModel.ImageViewer.RefreshGL();
        }

        internal static void RemoveAtIfPresent<T>(IList<T> items, int index)
        {
            if (items != null && index >= 0 && index < items.Count)
            {
                items.RemoveAt(index);
            }
        }

        #endregion

        #region AnnotationMaskOverlays
        // Mask overlays update only dirty raster segments so brush/eraser rendering stays incremental.
        internal bool TryRefreshMaskStrokeCanvasOverlays()
            => TryRefreshMaskStrokeCanvasOverlays(
                GetActiveMaskStrokeSegmentIndices(),
                HasActiveMaskStrokeFullObjectRefresh());

        internal bool TryRefreshMaskStrokeCanvasOverlays(
            IEnumerable<int> segmentIndices,
            bool needsFullObjectRefresh)
            => TryRefreshMaskStrokeCanvasOverlays(segmentIndices, needsFullObjectRefresh, refreshAfterInput: false);

        internal bool TryRefreshMaskStrokeCanvasOverlays(
            IEnumerable<int> segmentIndices,
            bool needsFullObjectRefresh,
            bool refreshAfterInput)
        {
            IReadOnlyList<int> orderedSegmentIndices = (segmentIndices ?? Array.Empty<int>())
                .Distinct()
                .OrderBy(index => index)
                .ToList();
            if (needsFullObjectRefresh
                || orderedSegmentIndices.Count == 0
                || MainCanvasViewModel == null)
            {
                return false;
            }

            float maskOpacity = (float)(LearningWorkflowViewModel?.MaskOpacity ?? 0.66);
            WpfObjectReviewListItem selectedObject = ObjectReviewViewModel?.SelectedObject;
            string selectedSourceKey = selectedObject?.SourceKey ?? string.Empty;
            int selectedSourceIndex = selectedObject?.SourceIndex ?? -1;
            foreach (int segmentIndex in orderedSegmentIndices)
            {
                if (!TryBuildManualMaskOverlay(segmentIndex, selectedSourceKey, selectedSourceIndex, maskOpacity, out RoiImageCanvasMaskOverlay maskOverlay)
                    || !MainCanvasViewModel.TryUpsertMaskOverlay(maskOverlay, refreshAfterInput))
                {
                    return false;
                }
            }

            return true;
        }

        internal bool TryBuildManualMaskOverlay(
            int segmentIndex,
            string selectedSourceKey,
            int selectedSourceIndex,
            float maskOpacity,
            out RoiImageCanvasMaskOverlay overlay)
        {
            overlay = null;
            if (segmentIndex < 0 || segmentIndex >= manualSegments.Count)
            {
                return false;
            }

            LabelingSegmentationObject segment = manualSegments[segmentIndex];
            WpfObjectSessionState sessionState = GetManualSegmentSessionState(segmentIndex);
            if (segment?.IsRasterMask != true || segment.MaskData == null || segment.MaskSize.IsEmpty || segment.Bounds.IsEmpty)
            {
                return false;
            }

            if (sessionState.IsHidden)
            {
                return false;
            }

            DrawingRectangle maskBounds = DrawingRectangle.Intersect(
                segment.Bounds,
                new DrawingRectangle(0, 0, segment.MaskSize.Width, segment.MaskSize.Height));
            if (maskBounds.IsEmpty)
            {
                return false;
            }

            string className = FirstNonEmpty(segment.ClassName, segment.ClassItem?.Text, "Defect");
            bool isSegmentSelected = string.Equals(
                selectedSourceKey,
                WpfObjectReviewSource.ManualSegment.ToString(),
                StringComparison.OrdinalIgnoreCase)
                && selectedSourceIndex == segmentIndex
                && ShouldSelectCommittedMaskAfterStroke();
            int displayIndex = manualRois.Count + segmentIndex + 1;
            bool isRemoveUnderlyingPreview = IsPendingRemoveUnderlyingAffectedIndex(segmentIndex);
            System.Drawing.Color overlayColor = isRemoveUnderlyingPreview
                ? System.Drawing.Color.Orange
                : segment.Color;
            overlay = new RoiImageCanvasMaskOverlay(
                $"{activeImagePath}|mask|{segmentIndex}",
                segment.MaskData,
                segment.MaskSize,
                maskBounds,
                overlayColor,
                maskOpacity,
                segment.RenderVersion,
                isSegmentSelected,
                isRemoveUnderlyingPreview
                    ? $"REMOVE PREVIEW {displayIndex} {className}"
                    : $"SEG {displayIndex} {className}",
                segment.RenderDirtyBounds,
                (uploadedVersion, uploadedBounds) => ClearMaskRenderDirtyBounds(segment, uploadedVersion, uploadedBounds));
            return true;
        }
        internal static void ClearMaskRenderDirtyBounds(LabelingSegmentationObject segment, int uploadedVersion, DrawingRectangle uploadedBounds)
        {
            if (segment == null || uploadedBounds.IsEmpty || segment.RenderVersion != uploadedVersion)
            {
                return;
            }

            // The OpenGL texture has consumed this exact render version. Keep newer
            // stroke dirt intact so a fast MouseMove cannot clear work not uploaded yet.
            segment.RenderDirtyBounds = DrawingRectangle.Empty;
        }

        internal static string FormatSegmentBoundsCompact(LabelingSegmentationObject segment)
        {
            DrawingRectangle bounds = segment?.Bounds ?? DrawingRectangle.Empty;
            return bounds.IsEmpty
                ? "-"
                : CandidateReviewPresenter.FormatBoundsCompact(bounds);
        }
        #endregion

        #region AnnotationPolygonOverlays
        // Polygon completion and overlay refresh are grouped away from ROI rectangle synchronization.
        internal void CompletePolygonAnnotation()
        {
            LabelingProjectData data = context.DataProvider?.Invoke();
            LabelClass classItem = classCatalogWorkflowService.EnsureClassItem(
                data,
                FirstNonEmpty(GetSelectedClassName(), "Defect"));
            if (!polygonAnnotationService.TryComplete(classItem, activeImageSize, out LabelingSegmentationObject annotation, out string message))
            {
                SetYoloCommandStatus(message, isBusy: false);
                return;
            }

            RegisterAnnotationHistoryBeforeChange("Add polygon");
            annotation.ZOrder = SegmentationZOrderService.GetNextZOrder(manualSegments);
            manualSegments.Add(annotation);
            polygonAnnotationService.Reset();
            RefreshPolygonOverlays();
            RefreshObjectList();
            ShowSavedLabelsWorkflowView();
            SetModelStatus($"Polygon added: {annotation.ClassName} / {annotation.Points.Count} points");
            AppendLog($"Polygon added: {annotation.ClassName} / {annotation.Points.Count} points / {FormatSegmentBoundsCompact(annotation)}");
            RefreshActiveImageQueueStatus(hasActiveCandidates: pendingDetectionCandidates.Count > 0);
        }

        internal void RefreshPolygonOverlays()
        {
            if (MainCanvasViewModel == null)
            {
                return;
            }

            bool showSavedLabels = CanvasPanelViewModel.IsLabelLayerVisible;
            bool showSmartMaskPrompts = smartMaskPromptSession.HasSession;
            bool showFourPointBoxDraft = fourPointBoxService.HasDraft;
            if (!IsSegmentationDatasetPurposeActive())
            {
                if (!showFourPointBoxDraft)
                {
                    ClearSegmentationOverlays();
                    return;
                }

                var fourPointOnlyOverlays = new List<RoiImageCanvasPolygonOverlay>();
                AppendFourPointBoxDraftOverlays(fourPointOnlyOverlays);
                MainCanvasViewModel.SetSegmentationOverlays(
                    fourPointOnlyOverlays,
                    Array.Empty<RoiImageCanvasMaskOverlay>());
                return;
            }

            if (!showSavedLabels && !showSmartMaskPrompts && !showFourPointBoxDraft)
            {
                ClearSegmentationOverlays();
                return;
            }

            var overlays = new List<RoiImageCanvasPolygonOverlay>();
            var maskOverlays = new List<RoiImageCanvasMaskOverlay>();
            float maskOpacity = (float)(LearningWorkflowViewModel?.MaskOpacity ?? 0.66);
            WpfObjectReviewListItem selectedObject = ObjectReviewViewModel?.SelectedObject;
            string selectedSourceKey = selectedObject?.SourceKey ?? string.Empty;
            int selectedSourceIndex = selectedObject?.SourceIndex ?? -1;
            if (showSavedLabels)
            {
                IEnumerable<int> renderIndices = manualSegments
                    .Select((segment, index) => new { Segment = segment, Index = index })
                    .Where(item => item.Segment != null)
                    .OrderBy(item => item.Segment.ZOrder)
                    .ThenBy(item => item.Index)
                    .Select(item => item.Index);
                foreach (int i in renderIndices)
                {
                    LabelingSegmentationObject segment = manualSegments[i];
                    WpfObjectSessionState sessionState = GetManualSegmentSessionState(i);
                    if (sessionState.IsHidden)
                    {
                        continue;
                    }

                    string className = FirstNonEmpty(segment.ClassName, segment.ClassItem?.Text, "Defect");
                    bool isSegmentSelected = string.Equals(
                        selectedSourceKey,
                        WpfObjectReviewSource.ManualSegment.ToString(),
                        StringComparison.OrdinalIgnoreCase)
                        && selectedSourceIndex == i;
                    if (segment.IsRasterMask)
                    {
                        if (TryBuildManualMaskOverlay(i, selectedSourceKey, selectedSourceIndex, maskOpacity, out RoiImageCanvasMaskOverlay maskOverlay))
                        {
                            maskOverlays.Add(maskOverlay);
                        }

                        continue;
                    }

                    if (segment.Points == null || segment.Points.Count == 0)
                    {
                        continue;
                    }

                    bool isRemoveUnderlyingPreview = IsPendingRemoveUnderlyingAffectedIndex(i);
                    System.Drawing.Color overlayColor = isRemoveUnderlyingPreview
                        ? System.Drawing.Color.Orange
                        : segment.Color;
                    overlays.Add(new RoiImageCanvasPolygonOverlay(
                        segment.Points,
                        isRemoveUnderlyingPreview
                            ? $"REMOVE PREVIEW {i + 1} {className}"
                            : $"SEG {i + 1} {className}",
                        overlayColor,
                        isClosed: true,
                        isDraft: false,
                        isSelected: isSegmentSelected,
                        selectedPointIndex: annotationSegmentEditAdapter.ActiveSegmentDragIndex == i
                            ? annotationSegmentEditAdapter.ActivePolygonPointDragIndex
                            : -1));
                }

                if (polygonAnnotationService.Points.Count > 0)
                {
                    overlays.Add(new RoiImageCanvasPolygonOverlay(
                        polygonAnnotationService.Points,
                        $"Draft {polygonAnnotationService.Points.Count}",
                        System.Drawing.Color.FromArgb(80, 180, 255),
                        polygonAnnotationService.IsClosed,
                        isDraft: true));
                }

                if (pendingSegmentationHoleEditMode == WpfSegmentationHoleEditMode.Add
                    && holePolygonAnnotationService.Points.Count > 0)
                {
                    overlays.Add(new RoiImageCanvasPolygonOverlay(
                        holePolygonAnnotationService.Points,
                        $"HOLE DRAFT {holePolygonAnnotationService.Points.Count}",
                        System.Drawing.Color.Orange,
                        holePolygonAnnotationService.IsClosed,
                        isDraft: true,
                        isSelected: true));
                }

                if (polygonBoundaryEditWorkflowService.IntelligentScissorsPlan?.PathPoints?.Count > 1)
                {
                    overlays.Add(new RoiImageCanvasPolygonOverlay(
                        polygonBoundaryEditWorkflowService.IntelligentScissorsPlan.PathPoints,
                        "EDGE PREVIEW",
                        System.Drawing.Color.Gold,
                        isClosed: false,
                        isDraft: true,
                        isSelected: true));
                }
            }

            AppendSmartMaskPromptOverlays(overlays);
            AppendFourPointBoxDraftOverlays(overlays);
            AppendPendingSmartMaskCandidateMask(maskOverlays, maskOpacity);
            MainCanvasViewModel.SetSegmentationOverlays(overlays, maskOverlays);
        }

        internal void AppendFourPointBoxDraftOverlays(List<RoiImageCanvasPolygonOverlay> overlays)
        {
            if (overlays == null || !fourPointBoxService.HasDraft || activeImageSize.IsEmpty)
            {
                return;
            }

            IReadOnlyList<System.Drawing.Point> points = fourPointBoxService.Points;
            string[] roles = { "\uC704", "\uC544\uB798", "\uC67C\uCABD", "\uC624\uB978\uCABD" };
            for (int index = 0; index < points.Count; index++)
            {
                System.Drawing.Point point = points[index];
                bool horizontal = index < 2;
                var guidePoints = horizontal
                    ? new[]
                    {
                        new System.Drawing.Point(0, point.Y),
                        new System.Drawing.Point(activeImageSize.Width - 1, point.Y)
                    }
                    : new[]
                    {
                        new System.Drawing.Point(point.X, 0),
                        new System.Drawing.Point(point.X, activeImageSize.Height - 1)
                    };
                overlays.Add(new RoiImageCanvasPolygonOverlay(
                    guidePoints,
                    $"4P {index + 1}/4 {roles[index]}",
                    System.Drawing.Color.DeepSkyBlue,
                    isClosed: false,
                    isDraft: true));

                const int markerRadius = 4;
                overlays.Add(new RoiImageCanvasPolygonOverlay(
                    new[]
                    {
                        new System.Drawing.Point(Math.Max(0, point.X - markerRadius), point.Y),
                        new System.Drawing.Point(point.X, Math.Max(0, point.Y - markerRadius)),
                        new System.Drawing.Point(Math.Min(activeImageSize.Width - 1, point.X + markerRadius), point.Y),
                        new System.Drawing.Point(point.X, Math.Min(activeImageSize.Height - 1, point.Y + markerRadius))
                    },
                    $"4P {index + 1}",
                    System.Drawing.Color.White,
                    isClosed: true,
                    isDraft: true));
            }
        }

        internal void AppendPendingSmartMaskCandidateMask(
            List<RoiImageCanvasMaskOverlay> maskOverlays,
            float configuredOpacity)
        {
            if (!smartMaskPromptSession.HasSession || activeImageSize.IsEmpty)
            {
                return;
            }

            YoloWorkerSmokeCandidate candidate = GetSelectedCandidate();
            IReadOnlyList<System.Drawing.PointF> contour = CandidateReviewPresentationService.GetCandidateContourPoints(
                candidate,
                activeImageSize);
            if (candidate == null || contour.Count < 3)
            {
                return;
            }

            List<System.Drawing.Point> points = SegmentationGeometry.NormalizePolygon(
                contour.Select(point => new System.Drawing.Point(
                    Math.Clamp((int)Math.Round(point.X), 0, activeImageSize.Width - 1),
                    Math.Clamp((int)Math.Round(point.Y), 0, activeImageSize.Height - 1))),
                activeImageSize,
                minimumDistance: 1,
                simplificationTolerance: 0D);
            var source = new LabelingSegmentationObject
            {
                Points = points,
                ClassName = FirstNonEmpty(candidate.ClassName, "Defect")
            };
            if (!SegmentationMaskGeometryService.TryRasterize(
                    source,
                    activeImageSize,
                    out byte[] maskData,
                    out DrawingRectangle maskBounds))
            {
                return;
            }

            int renderVersion = 17;
            foreach (System.Drawing.Point point in points)
            {
                renderVersion = unchecked((renderVersion * 31) + point.X);
                renderVersion = unchecked((renderVersion * 31) + point.Y);
            }

            float candidateOpacity = Math.Clamp(configuredOpacity * 0.62F, 0.34F, 0.46F);
            maskOverlays.Add(new RoiImageCanvasMaskOverlay(
                $"smart-mask-candidate:{candidate.Index}",
                maskData,
                activeImageSize,
                maskBounds,
                System.Drawing.Color.FromArgb(80, 180, 255),
                candidateOpacity,
                renderVersion,
                isSelected: false,
                label: string.Empty,
                showMarker: false));
        }

        internal void AppendSmartMaskPromptOverlays(List<RoiImageCanvasPolygonOverlay> overlays)
        {
            if (!smartMaskPromptSession.HasSession)
            {
                return;
            }

            System.Drawing.Rectangle bounds = smartMaskPromptSession.PromptBounds;
            overlays.Add(new RoiImageCanvasPolygonOverlay(
                new[]
                {
                    new System.Drawing.Point(bounds.Left, bounds.Top),
                    new System.Drawing.Point(bounds.Right, bounds.Top),
                    new System.Drawing.Point(bounds.Right, bounds.Bottom),
                    new System.Drawing.Point(bounds.Left, bounds.Bottom)
                },
                "SMART MASK PROMPT",
                System.Drawing.Color.FromArgb(168, 85, 247),
                isClosed: true,
                isDraft: true));

            for (int index = 0; index < smartMaskPromptSession.Points.Count; index++)
            {
                WpfSmartMaskPromptPoint point = smartMaskPromptSession.Points[index];
                int radius = 5;
                overlays.Add(new RoiImageCanvasPolygonOverlay(
                    new[]
                    {
                        new System.Drawing.Point(point.Position.X, point.Position.Y - radius),
                        new System.Drawing.Point(point.Position.X + radius, point.Position.Y),
                        new System.Drawing.Point(point.Position.X, point.Position.Y + radius),
                        new System.Drawing.Point(point.Position.X - radius, point.Position.Y)
                    },
                    point.Kind == WpfSmartMaskPointKind.Positive ? $"+{index + 1}" : $"−{index + 1}",
                    point.Kind == WpfSmartMaskPointKind.Positive
                        ? System.Drawing.Color.LimeGreen
                        : System.Drawing.Color.Red,
                    isClosed: true,
                    isDraft: false,
                    isSelected: true));
            }
        }

        internal void ClearSegmentationOverlays()
        {
            MainCanvasViewModel?.SetSegmentationOverlays(
                Array.Empty<RoiImageCanvasPolygonOverlay>(),
                Array.Empty<RoiImageCanvasMaskOverlay>());
        }
        #endregion
    }

    internal sealed class AnnotationRenderingAdapterContext
    {
        internal Func<LabelingProjectData> DataProvider { get; init; }
        internal List<DrawingRectangle> ManualRois { get; init; }
        internal List<string> ManualRoiClassNames { get; init; }
        internal List<CanvasRoiShapeKind> ManualRoiShapeKinds { get; init; }
        internal List<string> ManualRoiOverlayIds { get; init; }
        internal List<LabelingSegmentationObject> ManualSegments { get; init; }
        internal Func<DrawingSize> ActiveImageSizeProvider { get; init; }
        internal Func<string> ActiveImagePathProvider { get; init; }
        internal Func<WpfCanvasPanelViewModel> CanvasPanelViewModelProvider { get; init; }
        internal Func<RoiImageCanvasViewModel> MainCanvasViewModelProvider { get; init; }
        internal Func<WpfLearningWorkflowPanelViewModel> LearningWorkflowViewModelProvider { get; init; }
        internal Func<WpfObjectReviewPanelViewModel> ObjectReviewViewModelProvider { get; init; }
        internal PolygonAnnotationService PolygonAnnotationService { get; init; }
        internal PolygonAnnotationService HolePolygonAnnotationService { get; init; }
        internal PolygonBoundaryEditWorkflowService PolygonBoundaryEditWorkflowService { get; init; }
        internal SmartMaskPromptSessionService SmartMaskPromptSession { get; init; }
        internal FourPointBoxService FourPointBoxService { get; init; }
        internal ClassCatalogWorkflowService ClassCatalogWorkflowService { get; init; }
        internal AnnotationSegmentEditAdapter AnnotationSegmentEditAdapter { get; init; }
        internal Func<WpfSegmentationHoleEditMode?> PendingSegmentationHoleEditModeProvider { get; init; }
        internal Func<IReadOnlyList<YoloWorkerSmokeCandidate>> PendingDetectionCandidatesProvider { get; init; }
        internal Func<IReadOnlyList<YoloWorkerSmokeCandidate>> ConfirmedDetectionCandidatesProvider { get; init; }
        internal Func<YoloWorkerSmokeCandidate> SelectedCandidateProvider { get; init; }
        internal Func<YoloWorkerSmokeCandidate, bool> IsCandidateConfirmableProvider { get; init; }
        internal Func<bool> IsApplicationCloseApprovedProvider { get; init; }
        internal Func<int, WpfObjectSessionState> ManualRoiSessionStateProvider { get; init; }
        internal Func<string> SelectedClassNameProvider { get; init; }
        internal Func<int, string> ManualRoiClassNameProvider { get; init; }
        internal Func<int, WpfObjectSessionState> ManualSegmentSessionStateProvider { get; init; }
        internal Func<int, bool> IsPendingRemoveUnderlyingAffectedIndexProvider { get; init; }
        internal Func<bool> IsSegmentationDatasetPurposeActiveProvider { get; init; }
        internal Func<IReadOnlyCollection<int>> ActiveMaskStrokeSegmentIndicesProvider { get; init; }
        internal Func<bool> HasActiveMaskStrokeFullObjectRefreshProvider { get; init; }
        internal Func<bool> ShouldSelectCommittedMaskAfterStrokeProvider { get; init; }
        internal Action<string, bool> RegisterAnnotationHistoryBeforeChange { get; init; }
        internal Action<string, string> RegisterRoiEditHistoryBeforeChange { get; init; }
        internal Func<string, WpfAnnotationHistorySnapshot> CaptureManualRoiHistory { get; init; }
        internal Action<WpfAnnotationHistorySnapshot, bool> PushAnnotationHistorySnapshot { get; init; }
        internal Action ResetActiveRoiEditHistory { get; init; }
        internal Func<int, bool, bool> TryRefreshManualRoiObjectReviewRow { get; init; }
        internal Action<WpfObjectReviewItemRef> RefreshObjectListWithSelection { get; init; }
        internal Action RefreshObjectList { get; init; }
        internal Action<WpfObjectReviewSource, int> RefreshObjectReviewAfterDelete { get; init; }
        internal Action<bool> QueueActiveImageQueueStatusRefresh { get; init; }
        internal Action<bool> RefreshActiveImageQueueStatus { get; init; }
        internal Action ShowSavedLabelsWorkflowView { get; init; }
        internal Action<string> SetModelStatus { get; init; }
        internal Action<string, bool> SetYoloCommandStatus { get; init; }
        internal Action<string> AppendLog { get; init; }
        internal Action RefreshSmartMaskCommandState { get; init; }
        internal Action<CanvasRect<float>> TryStartAutoSmartMaskForNewRoi { get; init; }
        internal Action RefreshCanvasLayerVisibilityState { get; init; }
    }
}
