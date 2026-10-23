using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.ViewModels;
using OpenVisionLab.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingSize = System.Drawing.Size;

namespace MvcVisionSystem
{
    // Owns Object Review list projection, selection transitions, and direct object edits.
    // Metadata/session policy stays in ObjectReviewStateAdapter; canvas and Shell effects
    // are explicit callbacks so this owner can be reasoned about without a Window.
    internal sealed class ObjectReviewInteractionAdapter
    {
        private const int ObjectReviewFullRefreshDeleteLimit = 10_000;
        private readonly ObjectReviewInteractionAdapterContext context;

        internal ObjectReviewInteractionAdapter(ObjectReviewInteractionAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            if (context.DataProvider == null) throw new ArgumentNullException(nameof(context.DataProvider));
            if (context.ObjectReviewPresentationService == null) throw new ArgumentNullException(nameof(context.ObjectReviewPresentationService));
            if (context.ObjectSessionStateService == null) throw new ArgumentNullException(nameof(context.ObjectSessionStateService));
            if (context.ObjectMetadataStateService == null) throw new ArgumentNullException(nameof(context.ObjectMetadataStateService));
            if (context.SegmentationMergeService == null) throw new ArgumentNullException(nameof(context.SegmentationMergeService));
            if (context.CandidateReviewState == null) throw new ArgumentNullException(nameof(context.CandidateReviewState));
            if (context.ClassCatalogWorkflowService == null) throw new ArgumentNullException(nameof(context.ClassCatalogWorkflowService));
            if (context.ManualRois == null) throw new ArgumentNullException(nameof(context.ManualRois));
            if (context.ManualRoiClassNames == null) throw new ArgumentNullException(nameof(context.ManualRoiClassNames));
            if (context.ManualRoiShapeKinds == null) throw new ArgumentNullException(nameof(context.ManualRoiShapeKinds));
            if (context.ManualRoiOverlayIds == null) throw new ArgumentNullException(nameof(context.ManualRoiOverlayIds));
            if (context.ManualSegments == null) throw new ArgumentNullException(nameof(context.ManualSegments));
            if (context.PolygonBoundaryEditWorkflowService == null) throw new ArgumentNullException(nameof(context.PolygonBoundaryEditWorkflowService));
        }

        private LabelingProjectData projectData => context.DataProvider?.Invoke();
        private ObjectReviewPresentationService objectReviewPresentationService => context.ObjectReviewPresentationService;
        private ObjectSessionStateService objectSessionStateService => context.ObjectSessionStateService;
        private ObjectMetadataStateService objectMetadataStateService => context.ObjectMetadataStateService;
        private SegmentationMergeService segmentationMergeService => context.SegmentationMergeService;
        private CandidateReviewStateService candidateReviewState => context.CandidateReviewState;
        private ClassCatalogWorkflowService classCatalogWorkflowService => context.ClassCatalogWorkflowService;
        private List<DrawingRectangle> manualRois => context.ManualRois;
        private List<string> manualRoiClassNames => context.ManualRoiClassNames;
        private List<OpenVisionLab.ImageCanvas.CanvasShapes.CanvasRoiShapeKind> manualRoiShapeKinds => context.ManualRoiShapeKinds;
        private List<string> manualRoiOverlayIds => context.ManualRoiOverlayIds;
        private List<LabelingSegmentationObject> manualSegments => context.ManualSegments;
        private DrawingSize activeImageSize => context.ActiveImageSizeProvider?.Invoke() ?? DrawingSize.Empty;
        private IReadOnlyList<YoloWorkerSmokeCandidate> pendingDetectionCandidates
            => context.PendingDetectionCandidatesProvider?.Invoke() ?? Array.Empty<YoloWorkerSmokeCandidate>();
        private IReadOnlyList<YoloWorkerSmokeCandidate> confirmedDetectionCandidates => candidateReviewState.ConfirmedCandidates;
        private WpfObjectReviewPanelViewModel ObjectReviewViewModel => context.ObjectReviewViewModelProvider?.Invoke();
        private RoiImageCanvasViewModel MainCanvasViewModel => context.MainCanvasViewModelProvider?.Invoke();
        private WpfAnnotationTool activeAnnotationTool => context.ActiveAnnotationToolProvider?.Invoke() ?? WpfAnnotationTool.Select;
        private WpfSegmentationSplitOrientation? pendingSegmentationSplitOrientation => context.PendingSegmentationSplitOrientationProvider?.Invoke();
        private int pendingSegmentationSplitSourceIndex => context.PendingSegmentationSplitSourceIndexProvider?.Invoke() ?? -1;
        private WpfSegmentationHoleEditMode? pendingSegmentationHoleEditMode => context.PendingSegmentationHoleEditModeProvider?.Invoke();
        private int pendingSegmentationHoleSourceIndex => context.PendingSegmentationHoleSourceIndexProvider?.Invoke() ?? -1;
        private WpfSegmentationRemoveUnderlyingPlan pendingSegmentationRemoveUnderlyingPlan => context.PendingSegmentationRemoveUnderlyingPlanProvider?.Invoke();
        private PolygonBoundaryEditWorkflowService polygonBoundaryEditWorkflowService => context.PolygonBoundaryEditWorkflowService;

        private IReadOnlyList<LabelingSegmentationObject> GetVisibleManualSegments()
            => context.VisibleManualSegmentsProvider?.Invoke() ?? Array.Empty<LabelingSegmentationObject>();

        private int GetVisibleManualSegmentCount()
            => context.VisibleManualSegmentCountProvider?.Invoke() ?? GetVisibleManualSegments().Count;

        private WpfCandidateOverlapInfo GetCandidateOverlapInfo(DrawingRectangle bounds)
            => context.GetCandidateOverlapInfo?.Invoke(bounds) ?? default;

        private float GetMinimumDetectionConfidence()
            => context.MinimumDetectionConfidenceProvider?.Invoke() ?? 0F;

        internal void HandleObjectReviewWorkflowStatusChanged(string status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return;
            }

            context.SetYoloCommandStatus?.Invoke(status, false);
            context.AppendLog?.Invoke(status);
        }

        private void SetYoloCommandStatus(string message, bool isBusy)
            => context.SetYoloCommandStatus?.Invoke(message, isBusy);

        private void AppendLog(string message)
            => context.AppendLog?.Invoke(message);

        private void MarkAnnotationsDirty(string reason)
            => context.MarkAnnotationsDirty?.Invoke(reason);

        private void ApplyObjectPersistentMetadata(IEnumerable<WpfObjectReviewListItem> rows)
            => context.ApplyObjectPersistentMetadata?.Invoke(rows);

        private void CompleteMaskAnnotationStroke()
            => context.CompleteMaskAnnotationStroke?.Invoke();

        private void FlushQueuedMaskStrokeCommits()
            => context.FlushQueuedMaskStrokeCommits?.Invoke();

        private void CompleteSelectedSegmentEdit()
            => context.CompleteSelectedSegmentEdit?.Invoke();

        private void CancelPendingSegmentationRemoveUnderlying(bool updateStatus)
            => context.CancelPendingSegmentationRemoveUnderlying?.Invoke(updateStatus);

        private void CancelPendingSegmentationSplit(bool updateStatus)
            => context.CancelPendingSegmentationSplit?.Invoke(updateStatus);

        private void CancelPendingSegmentationHoleEdit(bool updateStatus)
            => context.CancelPendingSegmentationHoleEdit?.Invoke(updateStatus);

        private void CancelPendingPolygonVertexEdit(bool updateStatus)
            => context.CancelPendingPolygonVertexEdit?.Invoke(updateStatus);

        private void CancelPendingIntelligentScissors(bool updateStatus)
            => context.CancelPendingIntelligentScissors?.Invoke(updateStatus);

        private void ClearCanvasRoiSelection()
            => context.ClearCanvasRoiSelection?.Invoke();

        private void SetCanvasImagePointInputMode(bool value)
            => context.SetCanvasImagePointInputMode?.Invoke(value);

        private void RedrawReviewRois()
            => context.RedrawReviewRois?.Invoke();

        private void RefreshPolygonOverlays()
            => context.RefreshPolygonOverlays?.Invoke();

        private void CancelObjectGroupSelection(bool updateStatus)
            => context.CancelObjectGroupSelection?.Invoke(updateStatus);

        private WpfAnnotationHistorySnapshot CaptureAnnotationHistory(string actionName)
            => context.CaptureAnnotationHistory?.Invoke(actionName);

        private void PushAnnotationHistorySnapshot(WpfAnnotationHistorySnapshot snapshot)
            => context.PushAnnotationHistorySnapshot?.Invoke(snapshot);

        private bool CanMutateSelectedObject(WpfObjectReviewItemRef item, bool requireVisible, out string error)
        {
            if (context.CanMutateSelectedObject == null)
            {
                error = "객체 검수 상태를 초기화할 수 없습니다.";
                return false;
            }

            return context.CanMutateSelectedObject(item, requireVisible, out error);
        }

        private WpfObjectSessionState GetObjectSessionState(WpfObjectReviewItemRef item)
            => context.GetObjectSessionState?.Invoke(item) ?? WpfObjectSessionState.Default;

        private WpfObjectSessionState GetManualRoiSessionState(int index)
            => context.GetManualRoiSessionState?.Invoke(index) ?? WpfObjectSessionState.Default;

        private WpfObjectSessionState GetManualSegmentSessionState(int index)
            => context.GetManualSegmentSessionState?.Invoke(index) ?? WpfObjectSessionState.Default;

        private bool IsSegmentationDatasetPurposeActive()
            => context.IsSegmentationDatasetPurposeActive?.Invoke() == true;

        private void ApplyManualRoiOverlayColor(int index, bool refreshImmediately)
            => context.ApplyManualRoiOverlayColor?.Invoke(index, refreshImmediately);

        private void ClearMaskStrokePreview()
            => context.ClearMaskStrokePreview?.Invoke(false, true);

        private WpfAnnotationHistorySnapshot CaptureManualRoiHistory(string actionName)
            => context.CaptureManualRoiHistory?.Invoke(actionName);

        private void QueueActiveImageQueueStatusRefresh(bool hasActiveCandidates)
            => context.QueueActiveImageQueueStatusRefresh?.Invoke(hasActiveCandidates);

        private static void RemoveAtIfPresent<T>(IList<T> items, int index)
        {
            if (items != null && index >= 0 && index < items.Count)
            {
                items.RemoveAt(index);
            }
        }

        #region ObjectReview
        internal void RefreshObjectList()
        {
            RefreshObjectListViewModel(null);
        }

        internal void RefreshObjectListWithSelection(WpfObjectReviewItemRef preferredSelection)
        {
            RefreshObjectListViewModel(preferredSelection);
        }

        private void RefreshObjectListViewModel(WpfObjectReviewItemRef preferredSelection)
        {
            WpfObjectReviewItemRef previousSelection = null;
            TryGetSelectedObjectReviewItem(out previousSelection);

            WpfObjectReviewListPresentation presentation = objectReviewPresentationService.BuildListPresentation(
                manualRois,
                manualRoiClassNames,
                manualRoiShapeKinds,
                manualRoiOverlayIds,
                GetVisibleManualSegments(),
                confirmedDetectionCandidates,
                preferredSelection,
                previousSelection,
                candidate => CandidateReviewPresentationService.ClipCandidateBounds(candidate, activeImageSize),
                candidate =>
                {
                    DrawingRectangle bounds = CandidateReviewPresentationService.ClipCandidateBounds(candidate, activeImageSize);
                    return CandidateReviewPresenter.BuildDetail(
                        candidate,
                        bounds,
                        GetCandidateOverlapInfo(bounds),
                        GetMinimumDetectionConfidence());
                });

            ApplyObjectSessionStates(presentation.Rows);
            ApplyObjectPersistentMetadata(presentation.Rows);
            SetObjectReviewObjects(presentation.Rows, presentation.Summary, presentation.SelectedItem);
            UpdateObjectReviewActionState();
        }

        internal WpfObjectReviewListItem BuildManualRoiObjectReviewItem(int index)
        {
            WpfObjectReviewListItem row = objectReviewPresentationService.BuildManualRoiItem(
                manualRois,
                manualRoiClassNames,
                manualRoiShapeKinds,
                manualRoiOverlayIds,
                index);
            row?.ApplySessionState(objectSessionStateService.GetManualRoiState(index));
            row?.ApplyPersistentMetadata(objectMetadataStateService.GetManualRoiMetadata(index));
            ApplyObjectClassBrush(row);
            return row;
        }

        internal bool TryRefreshManualRoiObjectReviewRow(int manualRoiIndex, bool select)
        {
            WpfObjectReviewListItem row = BuildManualRoiObjectReviewItem(manualRoiIndex);
            if (row == null
                || !ObjectReviewSelectionService.CanReplaceManualRoiRow(
                    ObjectReviewViewModel?.Objects,
                    manualRoiIndex,
                    manualRois.Count))
            {
                return false;
            }

            bool replaced;
            bool classChanged = !string.Equals(ObjectReviewViewModel.Objects[manualRoiIndex].ClassName, row.ClassName, StringComparison.OrdinalIgnoreCase);
            using (ObjectReviewViewModel.SuppressSelectionNotifications())
            {
                replaced = ObjectReviewViewModel.TryReplaceObject(
                    manualRoiIndex,
                    row,
                    select);
            }

            if (replaced && classChanged) RefreshClassObjectCounts();
            SyncObjectClassEditorToSelection();
            UpdateObjectReviewActionState();
            return replaced;
        }

        private void SetObjectReviewObjects(
            IEnumerable<WpfObjectReviewListItem> rows,
            string summary,
            WpfObjectReviewItemRef selectedItem)
        {
            // Rebuilding the side list temporarily clears WPF SelectedItem. During ROI click/drag
            // that transient null must not clear the active canvas ROI handles.
            using (ObjectReviewViewModel.SuppressSelectionNotifications())
            {
                ObjectReviewViewModel.SetObjects(
                    rows,
                    summary,
                    selectedItem?.Source.ToString() ?? string.Empty,
                    selectedItem?.Index ?? -1);
            }

            RefreshClassObjectCounts();
            SyncObjectClassEditorToSelection();
        }

        internal string GetManualRoiClassName(int index)
            => ObjectReviewPresentationService.GetManualRoiClassName(manualRoiClassNames, index);

        internal WpfObjectReviewListItem BuildManualSegmentObjectReviewItem(int manualSegmentIndex)
        {
            if (!IsSegmentationDatasetPurposeActive())
            {
                return null;
            }

            return BuildManualSegmentObjectReviewItemCore(manualSegmentIndex);
        }

        private WpfObjectReviewListItem BuildManualSegmentObjectReviewItemCore(int manualSegmentIndex)
        {
            WpfObjectReviewListItem row = objectReviewPresentationService.BuildManualSegmentItem(
                manualRois.Count,
                manualSegments,
                manualSegmentIndex);
            row?.ApplySessionState(GetManualSegmentSessionState(manualSegmentIndex));
            if (manualSegmentIndex >= 0 && manualSegmentIndex < manualSegments.Count)
            {
                row?.ApplyPersistentMetadata(
                    objectMetadataStateService.GetManualSegmentMetadata(manualSegments[manualSegmentIndex]));
            }
            ApplyObjectClassBrush(row);
            return row;
        }

        private void ApplyObjectClassBrush(WpfObjectReviewListItem row)
        {
            if (row == null) return;
            row.ApplyClassBrush(context.LabelClassesProvider?.Invoke()?.FirstOrDefault(item => string.Equals(item.Text, row.ClassName, StringComparison.OrdinalIgnoreCase))?.DrawBrush);
        }

        private void RefreshClassObjectCounts()
        {
            IReadOnlyList<WpfCanvasLabelClassItem> classes = context.LabelClassesProvider?.Invoke();
            if (classes == null || ObjectReviewViewModel == null) return;
            Dictionary<string, int> counts = ObjectReviewViewModel.Objects.Where(row => row.IsEnabled && !string.IsNullOrWhiteSpace(row.ClassName))
                .GroupBy(row => row.ClassName, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
            foreach (WpfCanvasLabelClassItem item in classes) item.SetObjectCount(counts.TryGetValue(item.Text, out int count) ? count : 0);
        }

        private void ApplyObjectSessionStates(IEnumerable<WpfObjectReviewListItem> rows)
        {
            foreach (WpfObjectReviewListItem row in rows ?? Enumerable.Empty<WpfObjectReviewListItem>())
            {
                ApplyObjectClassBrush(row);
                row?.ApplySessionState(row.Payload is WpfObjectReviewItemRef item
                    ? GetObjectSessionState(item)
                    : WpfObjectSessionState.Default);
            }
        }

        internal bool TryRefreshManualSegmentObjectReviewRow(int manualSegmentIndex, string summary, bool select)
        {
            WpfObjectReviewListItem row = BuildManualSegmentObjectReviewItem(manualSegmentIndex);
            int objectRowIndex = manualRois.Count + manualSegmentIndex;
            if (row == null || ObjectReviewViewModel == null || objectRowIndex < 0)
            {
                return false;
            }

            bool updated;
            bool classChanged = objectRowIndex >= ObjectReviewViewModel.Objects.Count
                || !string.Equals(ObjectReviewViewModel.Objects[objectRowIndex].ClassName, row.ClassName, StringComparison.OrdinalIgnoreCase);
            using (ObjectReviewViewModel.SuppressSelectionNotifications())
            {
                updated = ObjectReviewViewModel.TryUpsertObject(
                    objectRowIndex,
                    row,
                    summary,
                    select);
            }

            if (updated && classChanged) RefreshClassObjectCounts();
            SyncObjectClassEditorToSelection();
            UpdateObjectReviewActionState();
            return updated;
        }

        // Class-editor synchronization stays beside object-list construction because both
        // paths operate on the selected object-review row and its class catalog.
        private void UpdateObjectReviewActionState()
        {
            ObjectReviewViewModel?.RefreshActionState();
        }

        internal void SyncObjectClassEditorToSelection()
        {
            if (ObjectReviewViewModel == null)
            {
                return;
            }

            if (!TryGetSelectedObjectReviewItem(out WpfObjectReviewItemRef item))
            {
                ObjectReviewViewModel.SelectedClassName = string.Empty;
                return;
            }

            string className = ObjectReviewEditService.GetClassName(
                item,
                manualRoiClassNames,
                manualSegments,
                confirmedDetectionCandidates);
            ObjectReviewViewModel.SetSelectedObjectClass(GetClassNames(), className);
        }

        internal void RefreshObjectClassOptions(string selectedName = "")
        {
            if (ObjectReviewViewModel == null)
            {
                return;
            }

            string viewModelSelection = string.IsNullOrWhiteSpace(selectedName)
                ? ObjectReviewViewModel.SelectedClassName
                : selectedName;
            ObjectReviewViewModel.SetClassNames(GetClassNames(), viewModelSelection);
            RefreshClassObjectCounts();
            foreach (WpfObjectReviewListItem row in ObjectReviewViewModel.Objects) ApplyObjectClassBrush(row);
        }

        private IReadOnlyList<string> GetClassNames()
        {
            if (projectData.ClassNamedList == null
                || !projectData.ClassNamedList.Any(item => item != null && !string.IsNullOrWhiteSpace(item.Text)))
            {
                classCatalogWorkflowService.EnsureClassItem(projectData, "Defect");
            }

            return projectData.ClassNamedList
                .Where(ClassCatalogService.IsActiveClass)
                .Select(item => item.Text)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        #endregion

        #region ObjectReviewCommands
        // Object commands mutate one selected object at a time; incremental delete avoids forcing a full canvas/list rebuild.
        internal IReadOnlyList<LabelSuspicionAnnotation> CaptureLabelSuspicionPreview()
        {
            var labels = new List<LabelSuspicionAnnotation>();
            var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int index = 0; index < manualRois.Count; index++)
            {
                string name = index < manualRoiClassNames.Count ? manualRoiClassNames[index] : string.Empty;
                int occurrence = occurrences.TryGetValue(name, out int count) ? count : 0;
                occurrences[name] = occurrence + 1;
                DrawingRectangle bounds = manualRois[index];
                labels.Add(new("box:" + name + ":" + occurrence, name, bounds.X, bounds.Y, bounds.Width, bounds.Height, occurrence));
            }
            occurrences.Clear();
            foreach (LabelingSegmentationObject segment in manualSegments)
            {
                string name = segment.ClassName;
                int occurrence = occurrences.TryGetValue(name, out int count) ? count : 0;
                occurrences[name] = occurrence + 1;
                DrawingRectangle bounds = segment.Bounds;
                labels.Add(new("segment:" + name + ":" + occurrence, name, bounds.X, bounds.Y, bounds.Width, bounds.Height, occurrence, segment.ObjectId));
            }
            for (int index = 0; index < confirmedDetectionCandidates.Count; index++)
            {
                YoloWorkerSmokeCandidate candidate = confirmedDetectionCandidates[index];
                if (candidate?.ImageLevel != false) continue;
                labels.Add(new("confirmed:" + index, candidate.ClassName, candidate.X, candidate.Y, candidate.Width, candidate.Height, index));
            }
            return labels;
        }

        internal bool ApplyLabelSuspicionFix(LabelSuspicionItem suspicion)
        {
            if (suspicion == null || context.CanEditLabelSuspicion?.Invoke() != true) return false;
            if (suspicion.Kind == LabelSuspicionKind.AnomalyVerdict)
                return context.PreviewAnomalySuspicionVerdict?.Invoke(suspicion.Prediction) == true;

            WpfObjectReviewItemRef selected = ResolveSuspicionLabel(suspicion.Label);
            if (suspicion.Kind != LabelSuspicionKind.Missing && selected == null) return false;
            if (suspicion.Kind == LabelSuspicionKind.Unnecessary)
            {
                RefreshObjectListWithSelection(selected);
                if (MainCanvasViewModel != null) ExecuteObjectSelectionChangedCommand(ObjectReviewViewModel?.SelectedObject);
                return true;
            }
            if (selected != null && !CanMutateSelectedObject(selected, requireVisible: false, out string error))
            {
                SetYoloCommandStatus(error, false);
                return false;
            }

            YoloWorkerSmokeCandidate candidate = suspicion.Prediction;
            LabelClass targetClass = projectData?.ClassNamedList?.FirstOrDefault(item => ClassCatalogService.IsActiveClass(item)
                && string.Equals(item.Text, candidate?.ClassName, StringComparison.Ordinal));
            if (candidate == null || targetClass == null) return false;
            DrawingRectangle bounds = CandidateReviewPresentationService.ClipCandidateBounds(candidate, activeImageSize);
            bool segmentation = projectData.ProjectSettings?.DatasetPurpose == LabelingDatasetPurpose.Segmentation;
            List<System.Drawing.Point> points = segmentation && suspicion.Kind != LabelSuspicionKind.Class
                ? CandidateReviewPresentationService.GetCandidateContourPoints(candidate, activeImageSize)
                    .Select(point => new System.Drawing.Point((int)Math.Round(point.X), (int)Math.Round(point.Y))).Distinct().ToList() : null;
            if (suspicion.Kind != LabelSuspicionKind.Class && (bounds.IsEmpty || segmentation && (points.Count < 3 || SegmentationGeometry.GetBounds(points).IsEmpty))) return false;
            WpfAnnotationHistorySnapshot before = CaptureAnnotationHistory("Fix label suspicion");
            if (before == null) return false;
            if (suspicion.Kind == LabelSuspicionKind.Class)
            {
                if (!ObjectReviewEditService.TryApplyClass(selected, manualRois, manualRoiClassNames, manualSegments,
                    candidateReviewState.MutableConfirmedCandidates, targetClass.Text, out _, targetClass)) return false;
            }
            else if (suspicion.Kind == LabelSuspicionKind.Missing)
            {
                if (segmentation)
                {
                    manualSegments.Add(new LabelingSegmentationObject(points, targetClass) { ObjectId = Guid.NewGuid().ToString("N"), ZOrder = manualSegments.Count });
                    selected = WpfObjectReviewItemRef.ManualSegment(manualSegments.Count - 1);
                }
                else
                {
                    manualRois.Add(bounds);
                    manualRoiClassNames.Add(targetClass.Text);
                    manualRoiShapeKinds.Add(OpenVisionLab.ImageCanvas.CanvasShapes.CanvasRoiShapeKind.Rectangle);
                    manualRoiOverlayIds.Add(string.Empty);
                    selected = WpfObjectReviewItemRef.Manual(manualRois.Count - 1);
                }
            }
            else if (selected.Source == WpfObjectReviewSource.ManualRoi)
                manualRois[selected.Index] = bounds;
            else if (selected.Source == WpfObjectReviewSource.ConfirmedAi)
            {
                YoloWorkerSmokeCandidate current = candidateReviewState.MutableConfirmedCandidates[selected.Index];
                current.X = bounds.X; current.Y = bounds.Y; current.Width = bounds.Width; current.Height = bounds.Height;
                YoloWorkerSmokeCandidate geometry = AnnotationHistoryService.CloneCandidate(candidate);
                current.PolygonPoints = geometry.PolygonPoints;
                current.NormalizedPolygonPoints = geometry.NormalizedPolygonPoints;
                current.SegmentationType = candidate.SegmentationType;
            }
            else
            {
                LabelingSegmentationObject segment = manualSegments[selected.Index];
                segment.Points = points;
                segment.MaskData = null;
                segment.MaskSize = DrawingSize.Empty;
                segment.MaskBounds = DrawingRectangle.Empty;
                segment.CutoutPolygons.Clear();
                segment.RenderVersion++;
            }
            PushAnnotationHistorySnapshot(before);
            RedrawReviewRois();
            RefreshPolygonOverlays();
            RefreshObjectListWithSelection(selected);
            if (MainCanvasViewModel != null) ExecuteObjectSelectionChangedCommand(ObjectReviewViewModel?.SelectedObject);
            MarkAnnotationsDirty(OpenVisionLab.OpenVisionLanguageService.T("WpfLabelSuspicion.Edited"));
            context.RefreshLabelSuspicionReview?.Invoke();
            return true;
        }

        private WpfObjectReviewItemRef ResolveSuspicionLabel(LabelSuspicionAnnotation label)
        {
            if (label == null) return null;
            bool MatchesBounds(DrawingRectangle bounds) => bounds.X == label.X && bounds.Y == label.Y && bounds.Width == label.Width && bounds.Height == label.Height;
            if (label.Identity.StartsWith("confirmed:", StringComparison.Ordinal))
            {
                int index = label.OccurrenceIndex;
                YoloWorkerSmokeCandidate candidate = index >= 0 && index < confirmedDetectionCandidates.Count ? confirmedDetectionCandidates[index] : null;
                return candidate != null && candidate.ClassName == label.ClassName && candidate.X == label.X && candidate.Y == label.Y
                    && candidate.Width == label.Width && candidate.Height == label.Height ? WpfObjectReviewItemRef.ConfirmedAi(index) : null;
            }
            if (label.Identity.StartsWith("segment:", StringComparison.Ordinal))
            {
                int index = !string.IsNullOrEmpty(label.SegmentObjectId)
                    ? manualSegments.FindIndex(segment => string.Equals(segment?.ObjectId, label.SegmentObjectId, StringComparison.Ordinal))
                    : manualSegments.Select((segment, i) => (segment, i)).Where(item => item.segment?.ClassName == label.ClassName)
                        .Skip(label.OccurrenceIndex).Select(item => item.i).DefaultIfEmpty(-1).First();
                return index >= 0 && manualSegments[index]?.ClassName == label.ClassName && MatchesBounds(manualSegments[index].Bounds)
                    ? WpfObjectReviewItemRef.ManualSegment(index) : null;
            }
            int roi = manualRois.Select((bounds, i) => (bounds, i)).Where(item => item.i < manualRoiClassNames.Count && manualRoiClassNames[item.i] == label.ClassName)
                .Skip(label.OccurrenceIndex).Select(item => item.i).DefaultIfEmpty(-1).First();
            return roi >= 0 && MatchesBounds(manualRois[roi]) ? WpfObjectReviewItemRef.Manual(roi) : null;
        }

        internal void ExecuteObjectSelectionChangedCommand(object selectedItem)
        {
            if (ObjectReviewViewModel?.IsSelectionNotificationSuppressed == true)
            {
                UpdateObjectReviewActionState();
                return;
            }

            if (pendingSegmentationSplitOrientation.HasValue
                && (selectedItem is not WpfObjectReviewListItem selectedRow
                    || !selectedRow.IsManualSegment
                    || selectedRow.SourceIndex != pendingSegmentationSplitSourceIndex))
            {
                CancelPendingSegmentationSplit(updateStatus: false);
            }

            if (pendingSegmentationHoleEditMode.HasValue
                && (selectedItem is not WpfObjectReviewListItem selectedHoleRow
                    || !selectedHoleRow.IsManualSegment
                    || selectedHoleRow.SourceIndex != pendingSegmentationHoleSourceIndex))
            {
                CancelPendingSegmentationHoleEdit(updateStatus: false);
            }

            if (polygonBoundaryEditWorkflowService.IsPolygonVertexEditPending
                && (selectedItem is not WpfObjectReviewListItem selectedVertexRow
                    || !selectedVertexRow.IsManualPolygon
                    || selectedVertexRow.SourceIndex != polygonBoundaryEditWorkflowService.PolygonVertexSourceIndex))
            {
                CancelPendingPolygonVertexEdit(updateStatus: false);
            }

            if (polygonBoundaryEditWorkflowService.IsIntelligentScissorsPending
                && (selectedItem is not WpfObjectReviewListItem selectedScissorsRow
                    || !selectedScissorsRow.IsManualPolygon
                    || selectedScissorsRow.SourceIndex != polygonBoundaryEditWorkflowService.IntelligentScissorsSourceIndex))
            {
                CancelPendingIntelligentScissors(updateStatus: false);
            }

            if (pendingSegmentationRemoveUnderlyingPlan != null
                && (selectedItem is not WpfObjectReviewListItem selectedRemoveRow
                    || !selectedRemoveRow.IsManualSegment
                    || selectedRemoveRow.SourceIndex != pendingSegmentationRemoveUnderlyingPlan.SelectedIndex))
            {
                CancelPendingSegmentationRemoveUnderlying(updateStatus: false);
            }

            SyncObjectClassEditorToSelection();
            UpdateObjectReviewActionState();
            bool isManualSegmentSelected = ObjectReviewViewModel?.IsSelectedSource(WpfObjectReviewSource.ManualSegment) == true;
            bool canEditSelectedSegment = isManualSegmentSelected
                && selectedItem is WpfObjectReviewListItem selectedStateRow
                && !selectedStateRow.IsHidden
                && !selectedStateRow.IsLocked;
            if (activeAnnotationTool == WpfAnnotationTool.Select)
            {
                MainCanvasViewModel.IsImagePointInputMode = canEditSelectedSegment;
            }

            if (selectedItem is WpfObjectReviewListItem roiRow
                && roiRow.Payload is WpfObjectReviewItemRef roiRef
                && roiRef.Source == WpfObjectReviewSource.ManualRoi
                && !roiRow.IsHidden && !roiRow.IsLocked)
            {
                int roiIndex = ObjectReviewSelectionService.ResolveManualRoiIndex(roiRef, manualRoiOverlayIds, manualRois.Count);
                MainCanvasViewModel?.SelectRoiOverlayById(ObjectReviewSelectionService.GetManualRoiOverlayId(manualRoiOverlayIds, roiIndex));
            }
            else
            {
                MainCanvasViewModel.ClearRoiSelection();
            }

            RefreshPolygonOverlays();
            context.RefreshSmartMaskCommandState?.Invoke();
        }

        internal void ExecuteApplyObjectClassCommand()
        {
            if (!TryGetSelectedObjectReviewItem(out WpfObjectReviewItemRef item))
            {
                return;
            }

            if (!CanMutateSelectedObject(item, requireVisible: false, out string stateError))
            {
                SetYoloCommandStatus(stateError, isBusy: false);
                AppendLog(stateError);
                return;
            }

            string className = ObjectReviewEditService.NormalizeClassName(ObjectReviewViewModel?.SelectedClassName);
            LabelClass classItem = classCatalogWorkflowService.EnsureClassItem(projectData, className);
            WpfAnnotationHistorySnapshot beforeChange = CaptureAnnotationHistory("Change object class");
            if (!ObjectReviewEditService.TryApplyClass(
                item,
                manualRois,
                manualRoiClassNames,
                manualSegments,
                candidateReviewState.MutableConfirmedCandidates,
                className,
                out string appliedClassName,
                classItem))
            {
                return;
            }

            PushAnnotationHistorySnapshot(beforeChange);
            if (item.Source == WpfObjectReviewSource.ManualRoi)
            {
                ApplyManualRoiOverlayColor(item.Index, refreshImmediately: true);
            }
            else if (item.Source == WpfObjectReviewSource.ConfirmedAi)
            {
                RedrawReviewRois();
            }
            else
            {
                ClearMaskStrokePreview();
                RefreshPolygonOverlays();
            }

            RefreshObjectList();
            MarkAnnotationsDirty($"\uAC1D\uCCB4 \uD074\uB798\uC2A4 \uBCC0\uACBD: {appliedClassName}");

            AppendLog($"Changed object class: {appliedClassName}");
        }

        internal void ExecuteApplySelectedObjectsClassCommand()
        {
            IReadOnlyList<int> selectedIndices = ObjectReviewViewModel?.GetMergeSelectedManualSegmentIndices()
                ?? Array.Empty<int>();
            if (selectedIndices.Count < 2)
            {
                const string message = "클래스를 일괄 적용하려면 편집 가능한 세그먼트를 2개 이상 선택하세요.";
                SetYoloCommandStatus(message, false);
                AppendLog(message);
                return;
            }

            var validatedIndices = new List<int>(selectedIndices.Count);
            foreach (int index in selectedIndices)
            {
                if (index < 0 || index >= manualSegments.Count)
                {
                    const string message = "일괄 적용 대상 객체가 현재 이미지 상태와 일치하지 않습니다. 목록을 새로 고친 뒤 다시 시도하세요.";
                    SetYoloCommandStatus(message, false);
                    AppendLog(message);
                    return;
                }

                WpfObjectReviewItemRef item = WpfObjectReviewItemRef.ManualSegment(index);
                if (!CanMutateSelectedObject(item, requireVisible: true, out string stateError))
                {
                    SetYoloCommandStatus(stateError, false);
                    AppendLog(stateError);
                    return;
                }

                validatedIndices.Add(index);
            }

            string className = ObjectReviewEditService.NormalizeClassName(ObjectReviewViewModel?.SelectedClassName);
            LabelClass classItem = classCatalogWorkflowService.EnsureClassItem(projectData, className);
            WpfAnnotationHistorySnapshot beforeChange = CaptureAnnotationHistory("Batch object class change");
            int changedCount = 0;
            foreach (int index in validatedIndices)
            {
                WpfObjectReviewItemRef item = WpfObjectReviewItemRef.ManualSegment(index);

                if (ObjectReviewEditService.TryApplyClass(
                    item,
                    manualRois,
                    manualRoiClassNames,
                    manualSegments,
                    candidateReviewState.MutableConfirmedCandidates,
                    className,
                    out _,
                    classItem))
                {
                    changedCount++;
                }
            }

            if (changedCount == 0)
            {
                const string message = "선택 객체의 클래스가 변경되지 않았습니다.";
                SetYoloCommandStatus(message, false);
                return;
            }

            PushAnnotationHistorySnapshot(beforeChange);
            ClearMaskStrokePreview();
            RefreshPolygonOverlays();
            RefreshObjectList();
            MarkAnnotationsDirty($"{changedCount}개 객체 클래스 일괄 변경: {className}");
            SetYoloCommandStatus($"{changedCount}개 객체에 클래스 {className}을 일괄 적용했습니다. 한 번에 실행 취소할 수 있습니다.", false);
            AppendLog($"Batch class change: {changedCount} objects / {className}");
        }

        internal void ExecuteDeleteObjectCommand()
        {
            DeleteSelectedObject();
        }

        internal void ExecuteMergeSelectedSegmentsCommand()
        {
            IReadOnlyList<int> selectedIndices = ObjectReviewViewModel?
                .GetMergeSelectedManualSegmentIndices()
                ?? Array.Empty<int>();
            if (!segmentationMergeService.TryMerge(
                manualSegments,
                selectedIndices,
                activeImageSize,
                out WpfSegmentationMergeResult mergeResult,
                out string error))
            {
                SetYoloCommandStatus(error, isBusy: false);
                AppendLog($"Segment merge skipped: {error}");
                ObjectReviewViewModel?.RefreshActionState();
                return;
            }

            CancelObjectGroupSelection(updateStatus: false);
            List<WpfPersistentObjectMetadata> sourceMetadata = mergeResult.SourceIndices
                .Select(index => objectMetadataStateService.GetManualSegmentMetadata(manualSegments[index]))
                .ToList();
            string inheritedGroupId = sourceMetadata.Count > 0
                && sourceMetadata.All(metadata =>
                    !string.IsNullOrWhiteSpace(metadata.GroupId)
                    && string.Equals(metadata.GroupId, sourceMetadata[0].GroupId, StringComparison.Ordinal))
                ? sourceMetadata[0].GroupId
                : string.Empty;
            WpfAnnotationHistorySnapshot beforeChange = CaptureAnnotationHistory("\uC138\uADF8\uBA3C\uD2B8 \uBCD1\uD569");
            foreach (int index in mergeResult.SourceIndices.OrderByDescending(index => index))
            {
                manualSegments.RemoveAt(index);
            }

            int insertIndex = Math.Max(0, Math.Min(mergeResult.InsertIndex, manualSegments.Count));
            manualSegments.Insert(insertIndex, mergeResult.MergedSegment);
            objectMetadataStateService.SetManualSegmentGroupId(
                mergeResult.MergedSegment,
                inheritedGroupId);
            objectMetadataStateService.DissolveInvalidGroups(manualRois.Count, manualSegments);
            PushAnnotationHistorySnapshot(beforeChange);
            ClearMaskStrokePreview();
            RefreshPolygonOverlays();
            RefreshObjectListWithSelection(WpfObjectReviewItemRef.ManualSegment(insertIndex));
            QueueActiveImageQueueStatusRefresh(hasActiveCandidates: pendingDetectionCandidates.Count > 0);

            string status = FormattableString.Invariant(
                $"\uC138\uADF8\uBA3C\uD2B8 \uBCD1\uD569: {mergeResult.SourceIndices.Count}\uAC1C \u2192 1\uAC1C / {mergeResult.MergedSegment.ClassName}");
            SetYoloCommandStatus(status, isBusy: false);
            AppendLog(status);
        }

        internal bool DeleteSelectedObject()
        {
            if (!TryGetSelectedObjectReviewItem(out WpfObjectReviewItemRef item))
            {
                return false;
            }

            if (!CanMutateSelectedObject(item, requireVisible: false, out string stateError))
            {
                SetYoloCommandStatus(stateError, isBusy: false);
                AppendLog(stateError);
                return false;
            }

            int selectedObjectRowIndex = GetSelectedObjectReviewRowIndex();
            string manualOverlayId = item.Source == WpfObjectReviewSource.ManualRoi
                ? ObjectReviewSelectionService.GetManualRoiOverlayId(manualRoiOverlayIds, item.Index)
                : string.Empty;
            string removedText = ObjectReviewViewModel?.SelectedObject?.DisplayText
                ?? "object";
            LabelingSegmentationObject deletedSegment = item.Source == WpfObjectReviewSource.ManualSegment
                && item.Index >= 0
                && item.Index < manualSegments.Count
                ? manualSegments[item.Index]
                : null;
            WpfAnnotationHistorySnapshot beforeChange = item.Source == WpfObjectReviewSource.ManualRoi
                ? CaptureManualRoiHistory("\uB77C\uBCA8 \uC0AD\uC81C")
                : CaptureAnnotationHistory("\uB77C\uBCA8 \uC0AD\uC81C");
            CancelObjectGroupSelection(updateStatus: false);
            if (!ObjectReviewEditService.TryDelete(
                item,
                manualRois,
                manualRoiClassNames,
                manualSegments,
                candidateReviewState.MutableConfirmedCandidates))
            {
                UpdateObjectReviewActionState();
                return false;
            }

            if (item.Source == WpfObjectReviewSource.ManualRoi)
            {
                objectSessionStateService.ShiftRoiStatesAfterRemoval(item.Index);
                objectMetadataStateService.ShiftRoiMetadataAfterRemoval(item.Index);
            }
            else if (item.Source == WpfObjectReviewSource.ManualSegment)
            {
                objectSessionStateService.RemoveManualSegment(deletedSegment);
                objectMetadataStateService.RemoveManualSegment(deletedSegment);
            }
            objectMetadataStateService.DissolveInvalidGroups(manualRois.Count, manualSegments);

            PushAnnotationHistorySnapshot(beforeChange);
            if (item.Source == WpfObjectReviewSource.ManualRoi)
            {
                RemoveAtIfPresent(manualRoiShapeKinds, item.Index);
                RemoveAtIfPresent(manualRoiOverlayIds, item.Index);
                if (!RemoveCanvasRoiOverlayById(manualOverlayId))
                {
                    RedrawReviewRois();
                }

                ClearCanvasRoiSelectionAfterDelete(manualOverlayId);
            }
            else if (item.Source == WpfObjectReviewSource.ManualSegment)
            {
                RefreshPolygonOverlays();
            }
            else
            {
                RedrawReviewRois();
            }

            RefreshObjectReviewAfterDelete(item.Source, selectedObjectRowIndex);
            MarkAnnotationsDirty($"\uB77C\uBCA8 \uC0AD\uC81C: {removedText}");
            QueueActiveImageQueueStatusRefresh(hasActiveCandidates: pendingDetectionCandidates.Count > 0);
            AppendLog($"Removed object from review: {removedText}");
            return true;
        }

        internal bool TryGetSelectedObjectReviewItem(out WpfObjectReviewItemRef item)
        {
            if (ObjectReviewViewModel == null)
            {
                item = null;
                return false;
            }

            return ObjectReviewViewModel.TryResolveSelectedItem(
                manualRoiOverlayIds,
                manualRois.Count,
                out item);
        }

        private int GetSelectedObjectReviewRowIndex()
            => ObjectReviewViewModel?.GetSelectedRowIndex() ?? -1;

        private bool RemoveCanvasRoiOverlayById(string overlayId)
        {
            if (string.IsNullOrWhiteSpace(overlayId) || MainCanvasViewModel?.ImageViewer == null)
            {
                return false;
            }

            var overlayItem = MainCanvasViewModel.ImageViewer.GetCanvasOverlayManager().GetOverlayByUniqueId(overlayId);
            string groupName = overlayItem?.Parent?.GroupType
                ?? overlayItem?.Shape?.GroupType
                ?? MainCanvasViewModel.ImageViewer.GetCanvasOverlayManager().LastGroupType
                ?? string.Empty;
            OpenVisionLab.ImageCanvas.OpenGLRendering.OpenGlOverlayExtensions.DeleteOverlay(
                MainCanvasViewModel.ImageViewer,
                overlayId,
                groupName,
                refreshImmediately: false);
            return overlayItem != null;
        }

        private void ClearCanvasRoiSelectionAfterDelete(string overlayId)
        {
            if (MainCanvasViewModel == null)
            {
                return;
            }

            if (!MainCanvasViewModel.ClearDeletedRoiSelection(overlayId, refreshImmediately: false))
            {
                MainCanvasViewModel.ClearRoiSelection(refreshImmediately: false);
            }
        }

        internal void RefreshObjectReviewAfterDelete(WpfObjectReviewSource deletedSource, int deletedObjectRowIndex)
        {
            int objectCount = manualRois.Count + GetVisibleManualSegmentCount() + confirmedDetectionCandidates.Count;
            WpfObjectReviewDeleteRefreshPlan plan = objectReviewPresentationService.BuildDeleteRefreshPlan(
                deletedSource,
                objectCount,
                ObjectReviewFullRefreshDeleteLimit,
                deletedObjectRowIndex,
                ObjectReviewViewModel?.Objects?.Count ?? 0);
            if (!plan.UseIncremental)
            {
                RefreshObjectList();
                return;
            }

            using (ObjectReviewViewModel.SuppressSelectionNotifications())
            {
                if (!ObjectReviewViewModel.TryRemoveObject(
                    deletedObjectRowIndex,
                    plan.Summary,
                    plan.SelectedRowIndex))
                {
                    RefreshObjectList();
                    return;
                }
            }

            RefreshClassObjectCounts();
            SyncObjectClassEditorToSelection();
            UpdateObjectReviewActionState();
        }
        #endregion
    }

    internal delegate bool TryMutateSelectedObjectDelegate(
        WpfObjectReviewItemRef item,
        bool requireVisible,
        out string error);

    internal sealed class ObjectReviewInteractionAdapterContext
    {
        internal Func<LabelingProjectData> DataProvider { get; init; }
        internal ObjectReviewPresentationService ObjectReviewPresentationService { get; init; }
        internal ObjectSessionStateService ObjectSessionStateService { get; init; }
        internal ObjectMetadataStateService ObjectMetadataStateService { get; init; }
        internal SegmentationMergeService SegmentationMergeService { get; init; }
        internal CandidateReviewStateService CandidateReviewState { get; init; }
        internal ClassCatalogWorkflowService ClassCatalogWorkflowService { get; init; }
        internal List<DrawingRectangle> ManualRois { get; init; }
        internal List<string> ManualRoiClassNames { get; init; }
        internal List<OpenVisionLab.ImageCanvas.CanvasShapes.CanvasRoiShapeKind> ManualRoiShapeKinds { get; init; }
        internal List<string> ManualRoiOverlayIds { get; init; }
        internal List<LabelingSegmentationObject> ManualSegments { get; init; }
        internal Func<DrawingSize> ActiveImageSizeProvider { get; init; }
        internal Func<IReadOnlyList<YoloWorkerSmokeCandidate>> PendingDetectionCandidatesProvider { get; init; }
        internal Func<WpfObjectReviewPanelViewModel> ObjectReviewViewModelProvider { get; init; }
        internal Func<IReadOnlyList<WpfCanvasLabelClassItem>> LabelClassesProvider { get; init; }
        internal Func<RoiImageCanvasViewModel> MainCanvasViewModelProvider { get; init; }
        internal Func<WpfAnnotationTool> ActiveAnnotationToolProvider { get; init; }
        internal Func<WpfSegmentationSplitOrientation?> PendingSegmentationSplitOrientationProvider { get; init; }
        internal Func<int> PendingSegmentationSplitSourceIndexProvider { get; init; }
        internal Func<WpfSegmentationHoleEditMode?> PendingSegmentationHoleEditModeProvider { get; init; }
        internal Func<int> PendingSegmentationHoleSourceIndexProvider { get; init; }
        internal Func<WpfSegmentationRemoveUnderlyingPlan> PendingSegmentationRemoveUnderlyingPlanProvider { get; init; }
        internal PolygonBoundaryEditWorkflowService PolygonBoundaryEditWorkflowService { get; init; }
        internal Func<IReadOnlyList<LabelingSegmentationObject>> VisibleManualSegmentsProvider { get; init; }
        internal Func<int> VisibleManualSegmentCountProvider { get; init; }
        internal Func<DrawingRectangle, WpfCandidateOverlapInfo> GetCandidateOverlapInfo { get; init; }
        internal Func<float> MinimumDetectionConfidenceProvider { get; init; }
        internal Action<string, bool> SetYoloCommandStatus { get; init; }
        internal Action<string> AppendLog { get; init; }
        internal Action<WpfObjectReviewItemRef> RefreshObjectListWithSelection { get; init; }
        internal Action<string> MarkAnnotationsDirty { get; init; }
        internal Action<IEnumerable<WpfObjectReviewListItem>> ApplyObjectPersistentMetadata { get; init; }
        internal Action<bool> CancelObjectGroupSelection { get; init; }
        internal Action CompleteMaskAnnotationStroke { get; init; }
        internal Action FlushQueuedMaskStrokeCommits { get; init; }
        internal Action CompleteSelectedSegmentEdit { get; init; }
        internal Action<bool> CancelPendingSegmentationRemoveUnderlying { get; init; }
        internal Action<bool> CancelPendingSegmentationSplit { get; init; }
        internal Action<bool> CancelPendingSegmentationHoleEdit { get; init; }
        internal Action<bool> CancelPendingPolygonVertexEdit { get; init; }
        internal Action<bool> CancelPendingIntelligentScissors { get; init; }
        internal Action ClearCanvasRoiSelection { get; init; }
        internal Action<bool> SetCanvasImagePointInputMode { get; init; }
        internal Action RedrawReviewRois { get; init; }
        internal Action RefreshPolygonOverlays { get; init; }
        internal Action RefreshSmartMaskCommandState { get; init; }
        internal Func<string, WpfAnnotationHistorySnapshot> CaptureAnnotationHistory { get; init; }
        internal Func<string, WpfAnnotationHistorySnapshot> CaptureManualRoiHistory { get; init; }
        internal Action<WpfAnnotationHistorySnapshot> PushAnnotationHistorySnapshot { get; init; }
        internal TryMutateSelectedObjectDelegate CanMutateSelectedObject { get; init; }
        internal Func<WpfObjectReviewItemRef, WpfObjectSessionState> GetObjectSessionState { get; init; }
        internal Func<int, WpfObjectSessionState> GetManualRoiSessionState { get; init; }
        internal Func<int, WpfObjectSessionState> GetManualSegmentSessionState { get; init; }
        internal Func<bool> IsSegmentationDatasetPurposeActive { get; init; }
        internal Action<int, bool> ApplyManualRoiOverlayColor { get; init; }
        internal Action<bool, bool> ClearMaskStrokePreview { get; init; }
        internal Action<bool> QueueActiveImageQueueStatusRefresh { get; init; }
        internal Func<bool> CanEditLabelSuspicion { get; init; }
        internal Func<YoloWorkerSmokeCandidate, bool> PreviewAnomalySuspicionVerdict { get; init; }
        internal Action RefreshLabelSuspicionReview { get; init; }
    }
}
