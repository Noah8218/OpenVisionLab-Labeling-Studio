using MvcVisionSystem._1._Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns Object Review live-state application and session-state policy.
    /// Persistence and mutation policy stay in ObjectReviewWorkflowService;
    /// the Shell supplies UI refresh callbacks and mutable annotation lists.
    /// </summary>
    internal sealed class ObjectReviewStateAdapter
    {
        private readonly ObjectReviewStateAdapterContext context;

        internal ObjectReviewStateAdapter(ObjectReviewStateAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            ArgumentNullException.ThrowIfNull(context.DataProvider);
        }

        private LabelingProjectData projectData => context.DataProvider?.Invoke();
        private ObjectReviewWorkflowService objectReviewWorkflowService => context.ObjectReviewWorkflowService;
        private ObjectMetadataStateService objectMetadataStateService => context.ObjectMetadataStateService;
        private ObjectSessionStateService objectSessionStateService => context.ObjectSessionStateService;
        private List<Rectangle> manualRois => context.ManualRois;
        private List<string> manualRoiClassNames => context.ManualRoiClassNames;
        private List<OpenVisionLab.ImageCanvas.CanvasShapes.CanvasRoiShapeKind> manualRoiShapeKinds => context.ManualRoiShapeKinds;
        private List<string> manualRoiOverlayIds => context.ManualRoiOverlayIds;
        private List<LabelingSegmentationObject> manualSegments => context.ManualSegments;
        private WpfObjectReviewPanelViewModel ObjectReviewViewModel => context.ObjectReviewViewModelProvider?.Invoke();

        private void EnsureProjectSettings() => context.EnsureProjectSettings?.Invoke();

        private void AppendLog(string message) => context.AppendLog?.Invoke(message);

        private void SetYoloCommandStatus(string message, bool isBusy)
            => context.SetYoloCommandStatus?.Invoke(message, isBusy);

        private void RefreshObjectListWithSelection(WpfObjectReviewItemRef preferredSelection)
            => context.RefreshObjectListWithSelection?.Invoke(preferredSelection);

        private void MarkAnnotationsDirty(string reason)
            => context.MarkAnnotationsDirty?.Invoke(reason);

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

        #region ObjectMetadata
        internal void RestoreObjectMetadataTagsFromProject()
        {
            EnsureProjectSettings();
            context.SetMetadataTagDefinitions?.Invoke(
                projectData.ProjectSettings.ObjectReviewTags);
        }

        internal void LoadObjectMetadataForActiveImage(string imagePath)
        {
            RestoreObjectMetadataTagsFromProject();
            WpfObjectReviewLoadResult workflowResult = objectReviewWorkflowService.Load(
                BuildObjectReviewPersistenceRequest(
                    imagePath,
                    Path.GetFileName(imagePath ?? string.Empty)));
            if (workflowResult.ShouldClearExistingState)
            {
                objectMetadataStateService.Clear();
            }

            ApplyObjectReviewMetadataChanges(workflowResult.MetadataChanges);
            WpfObjectMetadataLoadResult result = workflowResult.PersistenceResult;
            if (!string.IsNullOrWhiteSpace(result.StatusText))
            {
                AppendLog(result.IsCompatible
                    ? result.StatusText
                    : $"객체 메타데이터 무시: {result.StatusText}");
            }
        }

        internal bool TrySaveCurrentObjectMetadata(string imageName)
        {
            try
            {
                objectReviewWorkflowService.Save(
                    BuildObjectReviewPersistenceRequest(string.Empty, imageName));
                return true;
            }
            catch (Exception ex)
            {
                string status = $"객체 메타데이터 저장 실패: {ex.Message}";
                SetYoloCommandStatus(status, isBusy: false);
                AppendLog(status);
                return false;
            }
        }

        internal void CancelObjectGroupSelection(bool updateStatus)
        {
            bool wasActive = objectReviewWorkflowService.IsGroupSelectionActive;
            objectReviewWorkflowService.CancelGroupSelection();
            context.SetGroupSelectionMode?.Invoke(false);
            if (wasActive && updateStatus)
            {
                const string status = "검수 그룹 구성을 취소했습니다.";
                SetYoloCommandStatus(status, isBusy: false);
                AppendLog(status);
            }
        }

        internal bool ApplyCreatedObjectGroupMutation(WpfObjectReviewMutationResult workflowResult)
        {
            if (!ApplyObjectReviewMutation(workflowResult))
            {
                return false;
            }

            int memberCount = workflowResult.MetadataChanges.Count;
            RefreshObjectListWithSelection(workflowResult.FocusItem);
            MarkAnnotationsDirty($"검수 그룹 생성: {memberCount}개");
            return true;
        }

        internal bool ApplyObjectGroupMemberRemovalMutation(WpfObjectReviewMutationResult workflowResult)
        {
            if (!ApplyObjectReviewMutation(workflowResult))
            {
                return false;
            }

            RefreshObjectListWithSelection(workflowResult.FocusItem);
            MarkAnnotationsDirty("검수 그룹 구성원 제거");
            return true;
        }

        internal bool ConfirmObjectGroupDissolve(int memberCount)
            => context.ConfirmObjectGroupDissolve?.Invoke(memberCount) == true;

        internal bool ApplyObjectGroupDissolveMutation(WpfObjectReviewMutationResult workflowResult)
        {
            if (!ApplyObjectReviewMutation(workflowResult))
            {
                return false;
            }

            WpfObjectReviewItemRef selected = ObjectReviewViewModel?.SelectedObject?.Payload as WpfObjectReviewItemRef;
            int memberCount = workflowResult.MetadataChanges.Count;
            RefreshObjectListWithSelection(selected ?? workflowResult.FocusItem);
            MarkAnnotationsDirty($"검수 그룹 해제: {memberCount}개");
            return true;
        }

        internal bool ApplyObjectGroupOccludedMutation(WpfObjectReviewMutationResult workflowResult)
        {
            if (!ApplyObjectReviewMutation(workflowResult))
            {
                return false;
            }

            bool apply = workflowResult.AppliedValue;
            RefreshObjectListWithSelection(workflowResult.FocusItem);
            MarkAnnotationsDirty(apply ? "그룹 가림 적용" : "그룹 가림 해제");
            return true;
        }

        internal bool ApplyObjectGroupTagMutation(WpfObjectReviewMutationResult workflowResult)
        {
            if (!ApplyObjectReviewMutation(workflowResult))
            {
                return false;
            }

            WpfObjectReviewItemRef selected = ObjectReviewViewModel?.SelectedObject?.Payload as WpfObjectReviewItemRef;
            bool apply = workflowResult.AppliedValue;
            string tag = workflowResult.Tag;
            RefreshObjectListWithSelection(selected ?? workflowResult.FocusItem);
            MarkAnnotationsDirty(apply ? $"그룹 태그 적용: {tag}" : $"그룹 태그 해제: {tag}");
            return true;
        }

        internal bool ApplyObjectPersistentOccludedMutation(WpfObjectReviewMutationResult workflowResult)
        {
            if (!ApplyObjectReviewMutation(workflowResult))
            {
                return false;
            }

            bool apply = workflowResult.Metadata.IsOccluded;
            RefreshObjectListWithSelection(workflowResult.FocusItem);
            MarkAnnotationsDirty(
                apply
                    ? "객체 가림 메타데이터 설정"
                    : "객체 가림 메타데이터 해제");
            return true;
        }

        internal bool ApplyObjectPersistentTagMutation(WpfObjectReviewMutationResult workflowResult)
        {
            if (!ApplyObjectReviewMutation(workflowResult))
            {
                return false;
            }

            string tag = workflowResult.Tag;
            bool apply = workflowResult.Metadata.Tags.Any(value =>
                string.Equals(value, tag, StringComparison.OrdinalIgnoreCase));
            RefreshObjectListWithSelection(workflowResult.FocusItem);
            MarkAnnotationsDirty(
                apply
                    ? $"객체 태그 설정: {tag}"
                    : $"객체 태그 해제: {tag}");
            return true;
        }

        internal bool ApplyObjectRecipeMetadataResetMutation(WpfObjectReviewMutationResult workflowResult)
            => ApplyObjectReviewMutation(workflowResult);

        private WpfObjectReviewPersistenceRequest BuildObjectReviewPersistenceRequest(
            string imagePath,
            string imageName)
        {
            return new WpfObjectReviewPersistenceRequest(
                imagePath,
                imageName,
                projectData,
                CaptureObjectReviewSnapshots());
        }

        internal IReadOnlyList<WpfObjectReviewObjectSnapshot> CaptureObjectReviewSnapshots()
        {
            var snapshots = new List<WpfObjectReviewObjectSnapshot>();
            for (int index = 0; index < manualRois.Count; index++)
            {
                snapshots.Add(new WpfObjectReviewObjectSnapshot(
                    WpfObjectReviewItemRef.Manual(index),
                    manualRois[index],
                    index < manualRoiClassNames.Count ? manualRoiClassNames[index] : string.Empty,
                    string.Empty,
                    objectMetadataStateService.GetManualRoiMetadata(index)));
            }

            for (int index = 0; index < manualSegments.Count; index++)
            {
                LabelingSegmentationObject segment = manualSegments[index];
                if (segment == null)
                {
                    continue;
                }

                snapshots.Add(new WpfObjectReviewObjectSnapshot(
                    WpfObjectReviewItemRef.ManualSegment(index),
                    Rectangle.Empty,
                    segment.ClassName,
                    segment.ObjectId,
                    objectMetadataStateService.GetManualSegmentMetadata(segment)));
            }

            return snapshots;
        }

        private bool ApplyObjectReviewMutation(WpfObjectReviewMutationResult workflowResult)
        {
            if (workflowResult == null || !workflowResult.IsApplicable)
            {
                return false;
            }

            if (workflowResult.RecipeTagsChanged)
            {
                EnsureProjectSettings();
                context.SetMetadataTagDefinitions?.Invoke(
                    projectData.ProjectSettings.ObjectReviewTags);
            }

            return ApplyObjectReviewMetadataChanges(workflowResult.MetadataChanges);
        }

        private bool ApplyObjectReviewMetadataChanges(
            IEnumerable<WpfObjectReviewMetadataChange> changes)
        {
            List<WpfObjectReviewMetadataChange> pending = (changes
                ?? Enumerable.Empty<WpfObjectReviewMetadataChange>())
                .ToList();
            foreach (WpfObjectReviewMetadataChange change in pending)
            {
                if (change?.Item?.Source == WpfObjectReviewSource.ManualRoi
                    && change.Item.Index >= 0
                    && change.Item.Index < manualRois.Count)
                {
                    continue;
                }

                if (change?.Item?.Source == WpfObjectReviewSource.ManualSegment
                    && change.Item.Index >= 0
                    && change.Item.Index < manualSegments.Count
                    && manualSegments[change.Item.Index] != null)
                {
                    continue;
                }

                return false;
            }

            foreach (WpfObjectReviewMetadataChange change in pending)
            {
                if (change.Item.Source == WpfObjectReviewSource.ManualRoi)
                {
                    objectMetadataStateService.SetManualRoiMetadata(
                        change.Item.Index,
                        change.Metadata);
                }
                else
                {
                    objectMetadataStateService.SetManualSegmentMetadata(
                        manualSegments[change.Item.Index],
                        change.Metadata);
                }
            }

            return true;
        }

        internal void ReportObjectReviewWorkflowError(
            WpfObjectReviewMutationResult workflowResult)
        {
            if (workflowResult == null || string.IsNullOrWhiteSpace(workflowResult.ErrorMessage))
            {
                return;
            }

            SetYoloCommandStatus(workflowResult.ErrorMessage, isBusy: false);
            if (workflowResult.AppendErrorToLog)
            {
                AppendLog(workflowResult.ErrorMessage);
            }
        }

        internal WpfPersistentObjectMetadata GetObjectPersistentMetadata(WpfObjectReviewItemRef item)
        {
            if (item?.Source == WpfObjectReviewSource.ManualRoi)
            {
                return objectMetadataStateService.GetManualRoiMetadata(item.Index);
            }

            if (item?.Source == WpfObjectReviewSource.ManualSegment
                && item.Index >= 0
                && item.Index < manualSegments.Count)
            {
                return objectMetadataStateService.GetManualSegmentMetadata(
                    manualSegments[item.Index]);
            }

            return WpfPersistentObjectMetadata.Default;
        }

        internal void ApplyObjectPersistentMetadata(IEnumerable<WpfObjectReviewListItem> rows)
        {
            foreach (WpfObjectReviewListItem row in rows ?? Enumerable.Empty<WpfObjectReviewListItem>())
            {
                row?.ApplyPersistentMetadata(row.Payload is WpfObjectReviewItemRef item
                    ? GetObjectPersistentMetadata(item)
                    : WpfPersistentObjectMetadata.Default);
            }
        }
        #endregion

        #region ObjectSessionStateCommands
        internal WpfObjectSessionState ApplyObjectSessionStateMutation(
            WpfObjectReviewItemRef item,
            WpfObjectSessionStateKind kind)
        {
            CompleteMaskAnnotationStroke();
            FlushQueuedMaskStrokeCommits();
            CompleteSelectedSegmentEdit();
            CancelPendingSegmentationRemoveUnderlying(updateStatus: false);
            CancelPendingSegmentationSplit(updateStatus: false);
            CancelPendingSegmentationHoleEdit(updateStatus: false);
            CancelPendingPolygonVertexEdit(updateStatus: false);
            CancelPendingIntelligentScissors(updateStatus: false);
            if (!TryToggleObjectSessionState(item, kind, out WpfObjectSessionState state))
            {
                return null;
            }

            if (state.IsHidden || state.IsLocked)
            {
                ClearCanvasRoiSelection();
                SetCanvasImagePointInputMode(false);
            }

            if (item.Source == WpfObjectReviewSource.ManualRoi)
            {
                RedrawReviewRois();
            }
            else
            {
                RefreshPolygonOverlays();
            }

            RefreshObjectListWithSelection(item);
            return state;
        }

        private bool TryToggleObjectSessionState(
            WpfObjectReviewItemRef item,
            WpfObjectSessionStateKind kind,
            out WpfObjectSessionState state)
        {
            state = WpfObjectSessionState.Default;
            if (item == null)
            {
                return false;
            }

            if (item.Source == WpfObjectReviewSource.ManualRoi
                && item.Index >= 0
                && item.Index < manualRois.Count)
            {
                state = objectSessionStateService.ToggleManualRoiState(item.Index, kind);
                return true;
            }

            if (item.Source == WpfObjectReviewSource.ManualSegment
                && item.Index >= 0
                && item.Index < manualSegments.Count
                && manualSegments[item.Index] != null)
            {
                state = objectSessionStateService.ToggleManualSegmentState(manualSegments[item.Index], kind);
                return true;
            }

            return false;
        }

        internal WpfObjectSessionState GetObjectSessionState(WpfObjectReviewItemRef item)
        {
            if (item?.Source == WpfObjectReviewSource.ManualRoi)
            {
                return objectSessionStateService.GetManualRoiState(item.Index);
            }

            if (item?.Source == WpfObjectReviewSource.ManualSegment
                && item.Index >= 0
                && item.Index < manualSegments.Count)
            {
                return objectSessionStateService.GetManualSegmentState(manualSegments[item.Index]);
            }

            return WpfObjectSessionState.Default;
        }

        internal WpfObjectSessionState GetManualRoiSessionState(int index)
            => objectSessionStateService.GetManualRoiState(index);

        internal WpfObjectSessionState GetManualSegmentSessionState(int index)
            => index >= 0 && index < manualSegments.Count
                ? objectSessionStateService.GetManualSegmentState(manualSegments[index])
                : WpfObjectSessionState.Default;

        internal bool CanMutateSelectedObject(
            WpfObjectReviewItemRef item,
            bool requireVisible,
            out string error)
        {
            WpfObjectSessionState state = GetObjectSessionState(item);
            if (state.IsLocked)
            {
                error = "잠긴 객체입니다. 잠금을 해제한 후 수정하세요.";
                return false;
            }

            if (requireVisible && state.IsHidden)
            {
                error = "숨긴 객체입니다. 표시를 켠 후 구조를 수정하세요.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        internal bool CanEditManualSegment(LabelingSegmentationObject segment)
        {
            WpfObjectSessionState state = objectSessionStateService.GetManualSegmentState(segment);
            return !state.IsHidden && !state.IsLocked;
        }
        #endregion
    }

    internal sealed class ObjectReviewStateAdapterContext
    {
        internal Func<LabelingProjectData> DataProvider { get; init; }
        internal ObjectReviewWorkflowService ObjectReviewWorkflowService { get; init; }
        internal ObjectMetadataStateService ObjectMetadataStateService { get; init; }
        internal ObjectSessionStateService ObjectSessionStateService { get; init; }
        internal List<Rectangle> ManualRois { get; init; }
        internal List<string> ManualRoiClassNames { get; init; }
        internal List<OpenVisionLab.ImageCanvas.CanvasShapes.CanvasRoiShapeKind> ManualRoiShapeKinds { get; init; }
        internal List<string> ManualRoiOverlayIds { get; init; }
        internal List<LabelingSegmentationObject> ManualSegments { get; init; }
        internal Func<WpfObjectReviewPanelViewModel> ObjectReviewViewModelProvider { get; init; }
        internal Action<IEnumerable<string>> SetMetadataTagDefinitions { get; init; }
        internal Action<bool> SetGroupSelectionMode { get; init; }
        internal Action<string, bool> SetYoloCommandStatus { get; init; }
        internal Action<string> AppendLog { get; init; }
        internal Action<WpfObjectReviewItemRef> RefreshObjectListWithSelection { get; init; }
        internal Action<string> MarkAnnotationsDirty { get; init; }
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
        internal Func<int, bool> ConfirmObjectGroupDissolve { get; init; }
        internal Action EnsureProjectSettings { get; init; }
    }
}
