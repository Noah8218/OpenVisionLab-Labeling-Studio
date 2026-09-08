using MvcVisionSystem.DrawObject;
using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using DrawingPoint = System.Drawing.Point;
using DrawingRectangle = System.Drawing.Rectangle;

namespace MvcVisionSystem
{
    // Responsibility group: annotation persistence and undo/redo history.
    // These members remain WPF Window adapters; independent policy belongs in services.
    public partial class WpfLabelingShellWindow
    {
        #region AnnotationPersistence
        // Persistence converts the current viewer labels into YOLO save models; callers should enter through SaveCurrentAnnotations.
        private bool SaveCurrentAnnotations(out int savedCount)
        {
            savedCount = 0;
            CompleteMaskAnnotationStroke();
            FlushQueuedMaskStrokeCommits();
            LabelingImageSnapshot activeImage = global.ImageWorkspace.CaptureSnapshot();
            if (activeImage.Image == null || activeImage.ImageSize.IsEmpty)
            {
                return false;
            }

            SaveTrainingEditorFields();
            Dictionary<string, List<AnnotationRectangleObject>> roisByClass = BuildAnnotationRois();
            Dictionary<string, List<LabelingSegmentationObject>> segmentsByClass = BuildAnnotationSegments();
            savedCount = CountAnnotationRois(roisByClass) + CountAnnotationSegments(segmentsByClass);
            if (savedCount == 0)
            {
                return annotationDirtyState.IsDirty
                    && SaveCurrentEmptyAnnotations();
            }

            AnnotationSaveResult saveResult = annotationSaveWorkflowService.Save(
                new AnnotationSaveRequest(
                    activeImage,
                    roisByClass,
                    segmentsByClass,
                    global.Data,
                    savedCount,
                    () => TrySaveCurrentObjectMetadata(activeImage.ImageName)));

            if (saveResult.IsSaved)
            {
                MarkAnnotationsSaved($"라벨 저장 완료: 객체 {savedCount}개");
                DiscardCrashRecoveryJournal();
                global.System?.UpdateData();
            }

            return saveResult.IsSaved;
        }

        private bool SaveCurrentEmptyAnnotations()
        {
            CompleteMaskAnnotationStroke();
            FlushQueuedMaskStrokeCommits();
            LabelingImageSnapshot activeImage = global.ImageWorkspace.CaptureSnapshot();
            if (activeImage.Image == null || activeImage.ImageSize.IsEmpty)
            {
                return false;
            }

            SaveTrainingEditorFields();
            // Object-detection datasets still need an empty label file for reviewed normal images.
            AnnotationSaveResult saveResult = annotationSaveWorkflowService.SaveEmpty(
                activeImage,
                global.Data,
                () => TrySaveCurrentObjectMetadata(activeImage.ImageName));

            if (saveResult.IsSaved)
            {
                MarkAnnotationsSaved("\uBE48 \uB77C\uBCA8 \uD30C\uC77C \uC800\uC7A5 \uC644\uB8CC");
                DiscardCrashRecoveryJournal();
                global.System?.UpdateData();
            }

            return saveResult.IsSaved;
        }

        private static int CountAnnotationRois(IReadOnlyDictionary<string, List<AnnotationRectangleObject>> roisByClass)
        {
            return roisByClass?
                .Values
                .Where(list => list != null)
                .SelectMany(list => list)
                .Count(roi => roi != null && !roi.Roi.IsEmpty) ?? 0;
        }

        private static int CountAnnotationSegments(IReadOnlyDictionary<string, List<LabelingSegmentationObject>> segmentsByClass)
        {
            return segmentsByClass?
                .Values
                .Where(list => list != null)
                .SelectMany(list => list)
                .Count(segment => segment != null && ((segment.Points != null && segment.Points.Count >= 3) || (segment.IsRasterMask && !segment.Bounds.IsEmpty))) ?? 0;
        }

        private Dictionary<string, List<AnnotationRectangleObject>> BuildAnnotationRois()
        {
            var roisByClass = new Dictionary<string, List<AnnotationRectangleObject>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < manualRois.Count; i++)
            {
                AddAnnotationRoi(roisByClass, GetManualRoiClassName(i), manualRois[i]);
            }

            foreach (YoloWorkerSmokeCandidate candidate in confirmedDetectionCandidates)
            {
                AddAnnotationRoi(
                    roisByClass,
                    candidate.ClassName,
                    CandidateReviewPresentationService.ClipCandidateBounds(candidate, activeImageSize));
            }

            return roisByClass;
        }

        private Dictionary<string, List<LabelingSegmentationObject>> BuildAnnotationSegments()
        {
            var segmentsByClass = new Dictionary<string, List<LabelingSegmentationObject>>(StringComparer.OrdinalIgnoreCase);
            if (!IsSegmentationDatasetPurposeActive())
            {
                return segmentsByClass;
            }

            foreach (LabelingSegmentationObject segment in manualSegments)
            {
                if (segment == null)
                {
                    continue;
                }

                bool hasRasterMask = segment.IsRasterMask && !segment.Bounds.IsEmpty;
                bool hasPolygon = segment.Points != null && segment.Points.Count >= 3;
                if (!hasRasterMask && !hasPolygon)
                {
                    continue;
                }

                LabelClass classItem = classCatalogWorkflowService.EnsureClassItem(
                    global.Data,
                    FirstNonEmpty(segment.ClassName, segment.ClassItem?.Text, "Defect"));
                segment.ClassItem = classItem;
                segment.ClassName = classItem?.Text ?? "Defect";
                if (!segmentsByClass.TryGetValue(segment.ClassName, out List<LabelingSegmentationObject> segments))
                {
                    segments = new List<LabelingSegmentationObject>();
                    segmentsByClass[segment.ClassName] = segments;
                }

                segments.Add(segment);
            }

            foreach (YoloWorkerSmokeCandidate candidate in confirmedDetectionCandidates)
            {
                AddConfirmedCandidateSegment(segmentsByClass, candidate);
            }

            return segmentsByClass;
        }

        private void AddConfirmedCandidateSegment(
            Dictionary<string, List<LabelingSegmentationObject>> segmentsByClass,
            YoloWorkerSmokeCandidate candidate)
        {
            if (segmentsByClass == null || candidate?.PolygonPoints == null || candidate.PolygonPoints.Count < 3 || activeImageSize.IsEmpty)
            {
                return;
            }

            LabelClass classItem = classCatalogWorkflowService.EnsureClassItem(global.Data, FirstNonEmpty(candidate.ClassName, "Defect"));
            List<DrawingPoint> points = SegmentationGeometry.NormalizePolygon(
                candidate.PolygonPoints.Select(point => new DrawingPoint(
                    Math.Clamp((int)Math.Round(point.X), 0, activeImageSize.Width - 1),
                    Math.Clamp((int)Math.Round(point.Y), 0, activeImageSize.Height - 1))),
                activeImageSize,
                minimumDistance: 1,
                simplificationTolerance: 0D);
            if (points.Count < 3)
            {
                return;
            }

            var segment = new LabelingSegmentationObject(points, classItem)
            {
                ClassName = classItem?.Text ?? "Defect"
            };
            if (!segmentsByClass.TryGetValue(segment.ClassName, out List<LabelingSegmentationObject> segments))
            {
                segments = new List<LabelingSegmentationObject>();
                segmentsByClass[segment.ClassName] = segments;
            }

            segments.Add(segment);
        }

        private void AddAnnotationRoi(
            Dictionary<string, List<AnnotationRectangleObject>> roisByClass,
            string className,
            DrawingRectangle bounds)
        {
            if (roisByClass == null || bounds.IsEmpty)
            {
                return;
            }

            LabelClass classItem = classCatalogWorkflowService.EnsureClassItem(global.Data, className);
            var roiObject = new AnnotationRectangleObject
            {
                Roi = bounds,
                cClassItem = classItem
            };

            string normalizedName = classItem?.Text ?? "Defect";
            if (!roisByClass.TryGetValue(normalizedName, out List<AnnotationRectangleObject> rois))
            {
                rois = new List<AnnotationRectangleObject>();
                roisByClass[normalizedName] = rois;
            }

            rois.Add(roiObject);
        }

        private void MarkAnnotationsDirty(string reason)
        {
            annotationDirtyState.MarkDirty(reason, "Edit");
            ApplyAnnotationDirtyPresentation();
            ScheduleCrashRecoveryJournalWrite();
        }

        private void MarkMaskStrokeAnnotationsDirty(string reason)
        {
            annotationDirtyState.MarkDirty(reason, "Mask edit");
            AnnotationSaveStatePresentation dirtyPresentation = AnnotationSaveStatePresentationService.BuildDirty(annotationDirtyState.Reason);
            StatusBarViewModel?.SetAnnotationSaveStatus(
                dirtyPresentation.IsDirty,
                dirtyPresentation.StatusBarText,
                dirtyPresentation.StatusBarToolTip);
            if (!string.Equals(annotationDirtyState.Reason, "\0", StringComparison.Ordinal))
            {
                ScheduleCrashRecoveryJournalWrite();
                return;
            }
            StatusBarViewModel?.SetAnnotationSaveStatus(
                isDirty: true,
                text: "?쇰꺼 ????꾩슂",
                toolTip: $"?꾩쭅 ?뚯씪????λ릺吏 ?딆? ?몄쭛: {annotationDirtyState.Reason}");
            ScheduleCrashRecoveryJournalWrite();
        }

        private void RefreshDeferredMaskStrokeDirtyPresentation()
        {
            if (annotationDirtyState.IsDirty)
            {
                ApplyAnnotationDirtyPresentation();
            }
        }

        private void ApplyAnnotationDirtyPresentation()
        {
            InvalidateActiveImageQualityReviewAfterEdit();
            ApplyAnnotationSaveStatePresentation(
                AnnotationSaveStatePresentationService.BuildDirty(annotationDirtyState.Reason));
            ApplyActiveImageQueueSaveRequiredStatus(annotationDirtyState.Reason);
            RefreshActiveImageQualityReviewPresentation();
            RefreshCanvasLayerVisibilityState();
            RefreshCanvasWorkflowContext();
            UpdateWorkflowProgressStatus();
        }

        private void MarkAnnotationsSaved(string reason)
        {
            annotationDirtyState.Clear();
            ApplyAnnotationSaveStatePresentation(
                AnnotationSaveStatePresentationService.BuildSaved(reason));
            RefreshCanvasLayerVisibilityState();
            RefreshCanvasWorkflowContext();
            UpdateWorkflowProgressStatus();
        }

        private void SetAnnotationSaveStatusWaiting()
        {
            annotationDirtyState.Clear();
            ApplyAnnotationSaveStatePresentation(
                AnnotationSaveStatePresentationService.BuildWaiting());
            ObjectReviewViewModel?.SetQualityReviewState(
                YoloImageQualityReviewState.Unreviewed,
                hasActiveImage: false,
                canMarkReviewed: false);
            RefreshCanvasLayerVisibilityState();
            RefreshCanvasWorkflowContext();
            UpdateWorkflowProgressStatus();
        }

        private void ApplyAnnotationSaveStatePresentation(AnnotationSaveStatePresentation presentation)
        {
            if (presentation == null)
            {
                return;
            }

            StatusBarViewModel?.SetAnnotationSaveStatus(
                presentation.IsDirty,
                presentation.StatusBarText,
                presentation.StatusBarToolTip);
            CanvasPanelViewModel?.ApplyAnnotationSaveStatePresentation(presentation);
            ObjectReviewViewModel?.SetLabelSaveState(
                presentation.ObjectReviewStateKey,
                presentation.ObjectReviewBadgeText,
                presentation.ObjectReviewDetailText);
        }

        private int LoadSavedBoxAnnotationsForActiveImage(string imagePath)
        {
            if (activeImageSize.IsEmpty || GetCurrentDatasetPurpose() == LabelingDatasetPurpose.Segmentation)
            {
                return 0;
            }

            IReadOnlyDictionary<string, List<Rectangle>> savedBoxes = YoloAnnotationService.LoadAnnotationRectanglesForImage(
                imagePath,
                global.Data.ClassNamedList,
                global.Data,
                activeImageSize);
            if (savedBoxes == null || savedBoxes.Count == 0)
            {
                return 0;
            }

            int loadedCount = 0;
            foreach (KeyValuePair<string, List<Rectangle>> classBoxes in savedBoxes)
            {
                string className = ClassCatalogService.NormalizeClassName(classBoxes.Key);
                if (string.IsNullOrWhiteSpace(className) || classBoxes.Value == null)
                {
                    continue;
                }

                foreach (Rectangle box in classBoxes.Value.Where(item => !item.IsEmpty))
                {
                    manualRois.Add(box);
                    manualRoiClassNames.Add(className);
                    manualRoiShapeKinds.Add(CanvasRoiShapeKind.Rectangle);
                    manualRoiOverlayIds.Add(string.Empty);
                    loadedCount++;
                }
            }

            if (loadedCount > 0)
            {
                // Sample/opened datasets must show their saved YOLO boxes immediately;
                // otherwise a prepared object-detection project looks empty to a first-time user.
                RedrawReviewRois();
            }

            return loadedCount;
        }

        private int LoadSavedSegmentationAnnotationsForActiveImage(string imagePath)
        {
            if (activeImageSize.IsEmpty || !IsSegmentationDatasetPurposeActive())
            {
                return 0;
            }

            IReadOnlyDictionary<string, List<LabelingSegmentationObject>> savedSegments =
                YoloSegmentationAnnotationService.LoadSegmentationObjectsForImage(
                    imagePath,
                    global.Data.ClassNamedList,
                    global.Data,
                    activeImageSize);
            if (savedSegments == null || savedSegments.Count == 0)
            {
                return 0;
            }

            var loadedSegments = new List<LabelingSegmentationObject>();
            foreach (KeyValuePair<string, List<LabelingSegmentationObject>> classSegments in savedSegments)
            {
                LabelClass classItem = classCatalogWorkflowService.EnsureClassItem(global.Data, classSegments.Key);
                foreach (LabelingSegmentationObject segment in classSegments.Value ?? Enumerable.Empty<LabelingSegmentationObject>())
                {
                    if (segment == null)
                    {
                        continue;
                    }

                    segment.ClassName = classItem?.Text ?? "Defect";
                    segment.ClassItem = classItem;
                    if (segment.IsRasterMask && segment.RenderVersion <= 0)
                    {
                        segment.RenderVersion = 1;
                        segment.RenderDirtyBounds = segment.Bounds;
                    }

                    loadedSegments.Add(segment);
                }
            }

            manualSegments.AddRange(loadedSegments
                .Select((segment, index) => new { Segment = segment, Index = index })
                .OrderBy(item => item.Segment.ZOrder)
                .ThenBy(item => item.Index)
                .Select(item => item.Segment));
            int loadedCount = loadedSegments.Count;
            if (loadedCount > 0)
            {
                RefreshPolygonOverlays();
            }

            return loadedCount;
        }
        #endregion

        #region AnnotationHistory
        // Undo/redo touches ROI, segmentation, and AI candidate state together, so it stays in one history partial instead of the event flow.
        private WpfAnnotationHistorySnapshot CaptureAnnotationHistory(string actionName)
        {
            return AnnotationHistoryService.Capture(
                actionName,
                manualRois,
                manualRoiClassNames,
                manualRoiShapeKinds,
                manualSegments,
                pendingDetectionCandidates,
                confirmedDetectionCandidates);
        }

        private WpfAnnotationHistorySnapshot CaptureManualRoiHistory(string actionName)
        {
            return AnnotationHistoryService.CaptureManualRoiList(
                actionName,
                manualRois,
                manualRoiClassNames,
                manualRoiShapeKinds);
        }

        private void RegisterAnnotationHistoryBeforeChange(string actionName, bool markDirty = true)
        {
            PushAnnotationHistorySnapshot(CaptureAnnotationHistory(actionName), markDirty);
        }

        private void RegisterRoiEditHistoryBeforeChange(string overlayId, string actionName)
        {
            string normalizedOverlayId = overlayId ?? string.Empty;
            if (string.Equals(activeRoiEditHistoryOverlayId, normalizedOverlayId, StringComparison.Ordinal))
            {
                return;
            }

            RegisterAnnotationHistoryBeforeChange(actionName);
            activeRoiEditHistoryOverlayId = normalizedOverlayId;
        }

        private void PushAnnotationHistorySnapshot(WpfAnnotationHistorySnapshot snapshot, bool markDirty = true)
        {
            if (suppressAnnotationHistory || snapshot == null)
            {
                return;
            }

            if (annotationHistoryWorkflowService.Push(snapshot) && markDirty)
            {
                MarkAnnotationsDirty(snapshot.ActionName);
            }
            RefreshAnnotationHistoryToolState();
        }

        private void ClearAnnotationHistory()
        {
            annotationHistoryWorkflowService.Clear();
            activeRoiEditHistoryOverlayId = string.Empty;
            RefreshAnnotationHistoryToolState();
        }

        private void RefreshAnnotationHistoryToolState()
        {
            bool hasPendingMaskStrokeUndo = HasPendingMaskStrokeUndoWork();
            AnnotationHistoryToolState toolState = annotationHistoryWorkflowService.GetToolState(
                hasPendingMaskStrokeUndo,
                GetPendingMaskStrokeUndoActionName());
            LearningWorkflowViewModel?.SetAnnotationHistoryState(
                toolState.CanUndo,
                toolState.CanRedo,
                toolState.UndoActionName,
                toolState.RedoActionName);
        }

        private bool HasPendingMaskStrokeUndoWork()
            => pendingMaskStrokeCommitCount > 0 || queuedMaskStrokeCommits.Count > 0;

        private string GetPendingMaskStrokeUndoActionName()
            => queuedMaskStrokeCommits.Count > 0
                ? queuedMaskStrokeCommits.Peek().ActionName
                : string.Empty;

        private bool UndoWpfAnnotationHistory()
        {
            CompleteMaskAnnotationStroke();
            FlushQueuedMaskStrokeCommits();
            if (!annotationHistoryWorkflowService.TryUndo(
                    CaptureHistoryForOppositeStack,
                    out AnnotationHistoryTransition transition))
            {
                SetYoloCommandStatus("되돌릴 편집 이력이 없습니다.", isBusy: false);
                return false;
            }

            RestoreAnnotationHistorySnapshot(transition.Target);
            string displayActionName = transition.DisplayActionName;
            SetYoloCommandStatus($"\uB418\uB3CC\uB9AC\uAE30: {displayActionName}", isBusy: false);
            AppendLog($"\uB418\uB3CC\uB9AC\uAE30: {displayActionName}");
            MarkAnnotationsDirty($"\uB418\uB3CC\uB9AC\uAE30 {displayActionName}");
            RefreshAnnotationHistoryToolState();
            return true;
        }

        private void ExecuteUndoAnnotationCommand()
        {
            UndoWpfAnnotationHistory();
        }

        private void ExecuteRedoAnnotationCommand()
        {
            RedoWpfAnnotationHistory();
        }

        private bool RedoWpfAnnotationHistory()
        {
            CompleteMaskAnnotationStroke();
            FlushQueuedMaskStrokeCommits();
            if (!annotationHistoryWorkflowService.TryRedo(
                    CaptureHistoryForOppositeStack,
                    out AnnotationHistoryTransition transition))
            {
                SetYoloCommandStatus("다시 실행할 편집 이력이 없습니다.", isBusy: false);
                return false;
            }

            RestoreAnnotationHistorySnapshot(transition.Target);
            string displayActionName = transition.DisplayActionName;
            SetYoloCommandStatus($"\uB2E4\uC2DC \uC801\uC6A9: {displayActionName}", isBusy: false);
            AppendLog($"\uB2E4\uC2DC \uC801\uC6A9: {displayActionName}");
            MarkAnnotationsDirty($"\uB2E4\uC2DC \uC801\uC6A9 {displayActionName}");
            RefreshAnnotationHistoryToolState();
            return true;
        }

        private WpfAnnotationHistorySnapshot CaptureHistoryForOppositeStack(
            string actionName,
            WpfAnnotationHistorySnapshot target)
        {
            return AnnotationHistoryService.CaptureMaskDeltaInverse(
                    actionName,
                    target,
                    manualSegments)
                ?? CaptureAnnotationHistory(actionName);
        }

        private void RestoreAnnotationHistorySnapshot(WpfAnnotationHistorySnapshot snapshot)
        {
            CancelPendingIntelligentScissors(updateStatus: false);
            suppressAnnotationHistory = true;
            try
            {
                AnnotationHistoryService.Restore(
                    snapshot,
                    manualRois,
                    manualRoiClassNames,
                    manualRoiShapeKinds,
                    manualRoiOverlayIds,
                    manualSegments,
                    candidateReviewState.MutablePendingCandidates,
                    candidateReviewState.MutableConfirmedCandidates);

                activeRoiEditHistoryOverlayId = string.Empty;
                polygonAnnotationService.Reset();
                lastMaskStrokePoint = null;
                activeMaskStrokeInProgress = false;
                activeMaskStrokeActionName = string.Empty;
                CancelMaskStrokePreviewCommitSwap();
                MainCanvasViewModel?.ClearMaskStrokePreview(refresh: false);
                EnsureManualRoiMetadataCount();
                RefreshPolygonOverlays();
                RefreshObjectList();
                RefreshCandidateList();
                RedrawReviewRois();
                PopulateClassList();
                UpdateDetectionResultOverlay();
                RefreshActiveImageQueueStatus(hasActiveCandidates: pendingDetectionCandidates.Count > 0);
                SetPythonStatus($"\uCD94\uB860: \uB300\uAE30 {pendingDetectionCandidates.Count} / \uD655\uC815 {confirmedDetectionCandidates.Count}");
            }
            finally
            {
                suppressAnnotationHistory = false;
            }
        }
        #endregion

    }
}
