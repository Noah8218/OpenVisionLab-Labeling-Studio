using MvcVisionSystem._1._Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns Object Review metadata policy and persistence while the Shell keeps
    /// live-state application, presentation refresh, and operator interaction.
    /// </summary>
    public class ObjectReviewWorkflowService
    {
        private readonly ObjectMetadataPersistenceService metadataPersistenceService;
        private readonly ProjectRecipeSessionService projectRecipeSessionService;
        private readonly ObjectReviewGroupSelectionService groupSelectionService =
            new ObjectReviewGroupSelectionService();

        public ObjectReviewWorkflowService(
            ObjectMetadataPersistenceService metadataPersistenceService,
            ProjectRecipeSessionService projectRecipeSessionService)
        {
            this.metadataPersistenceService = metadataPersistenceService
                ?? throw new ArgumentNullException(nameof(metadataPersistenceService));
            this.projectRecipeSessionService = projectRecipeSessionService
                ?? throw new ArgumentNullException(nameof(projectRecipeSessionService));
        }

        public bool IsGroupSelectionActive => groupSelectionService.IsActive;

        public void BeginGroupSelection() => groupSelectionService.Begin();

        public void CancelGroupSelection() => groupSelectionService.Cancel();

        public bool SetGroupSelection(
            WpfObjectReviewItemRef item,
            bool isSelected,
            out string error)
            => groupSelectionService.SetSelected(item, isSelected, out error);

        public WpfObjectReviewLoadResult Load(WpfObjectReviewPersistenceRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            MaterializedObjectSet materialized = Materialize(request.Objects);
            var temporaryState = new ObjectMetadataStateService();
            WpfObjectMetadataLoadResult persistenceResult = metadataPersistenceService.LoadForImage(
                request.ImagePath,
                materialized.ManualRois,
                materialized.ManualRoiClassNames,
                materialized.ManualSegments,
                temporaryState,
                request.Data);
            return new WpfObjectReviewLoadResult(
                persistenceResult,
                shouldClearExistingState: true,
                CaptureMetadataChanges(
                    request.Objects,
                    materialized,
                    temporaryState,
                    includeUnchanged: true));
        }

        public IReadOnlyList<string> Save(WpfObjectReviewPersistenceRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            MaterializedObjectSet materialized = Materialize(request.Objects);
            ObjectMetadataStateService temporaryState = BuildState(request.Objects, materialized);
            return metadataPersistenceService.Save(
                request.ImageName,
                materialized.ManualRois,
                materialized.ManualRoiClassNames,
                materialized.ManualSegments,
                temporaryState,
                request.Data);
        }

        public WpfObjectReviewMutationResult ToggleOccluded(
            WpfObjectReviewObjectSnapshot snapshot)
        {
            if (!TryBuildWorkingState(snapshot, out MaterializedObjectSet materialized, out ObjectMetadataStateService state))
            {
                return WpfObjectReviewMutationResult.Failure(string.Empty);
            }

            WpfPersistentObjectMetadata metadata = GetOrDefault(snapshot);
            if (snapshot.Item.Source == WpfObjectReviewSource.ManualRoi)
            {
                metadata = state.ToggleManualRoiOccluded(snapshot.Item.Index);
            }
            else if (snapshot.Item.Source == WpfObjectReviewSource.ManualSegment
                && materialized.SegmentsByKey.TryGetValue(BuildKey(snapshot.Item), out LabelingSegmentationObject segment))
            {
                metadata = state.ToggleManualSegmentOccluded(segment);
            }
            else
            {
                return WpfObjectReviewMutationResult.Failure(string.Empty);
            }

            return WpfObjectReviewMutationResult.Success(
                snapshot.Item,
                metadata,
                new[] { new WpfObjectReviewMetadataChange(snapshot.Item, metadata) });
        }

        public WpfObjectReviewMutationResult ToggleTag(
            WpfObjectReviewObjectSnapshot snapshot,
            string requestedTag,
            LabelingProjectData data,
            string recipeName)
        {
            string tag = ObjectMetadataStateService.NormalizeTag(requestedTag);
            if (!ObjectReviewGroupSelectionService.IsEligible(snapshot?.Item)
                || string.IsNullOrWhiteSpace(tag))
            {
                return WpfObjectReviewMutationResult.Failure(tag);
            }

            WpfPersistentObjectMetadata current = GetOrDefault(snapshot);
            bool alreadyApplied = current.Tags.Any(value =>
                string.Equals(value, tag, StringComparison.OrdinalIgnoreCase));
            bool recipeTagsChanged = false;
            if (!alreadyApplied
                && !TryEnsureRecipeMetadataTag(
                    data,
                    recipeName,
                    tag,
                    out recipeTagsChanged,
                    out string error,
                    out bool appendErrorToLog))
            {
                return WpfObjectReviewMutationResult.Failure(
                    tag,
                    error,
                    appendErrorToLog,
                    recipeTagsChanged);
            }

            if (!TryBuildWorkingState(snapshot, out MaterializedObjectSet materialized, out ObjectMetadataStateService state))
            {
                return WpfObjectReviewMutationResult.Failure(tag, recipeTagsChanged: recipeTagsChanged);
            }

            WpfPersistentObjectMetadata metadata;
            if (snapshot.Item.Source == WpfObjectReviewSource.ManualRoi)
            {
                metadata = state.ToggleManualRoiTag(snapshot.Item.Index, tag);
            }
            else if (materialized.SegmentsByKey.TryGetValue(BuildKey(snapshot.Item), out LabelingSegmentationObject segment))
            {
                metadata = state.ToggleManualSegmentTag(segment, tag);
            }
            else
            {
                return WpfObjectReviewMutationResult.Failure(tag, recipeTagsChanged: recipeTagsChanged);
            }

            return WpfObjectReviewMutationResult.Success(
                snapshot.Item,
                metadata,
                new[] { new WpfObjectReviewMetadataChange(snapshot.Item, metadata) },
                tag,
                recipeTagsChanged);
        }

        public WpfObjectReviewMutationResult ResetRecipeMetadataTags(
            LabelingProjectData data,
            string recipeName)
        {
            EnsureProjectSettings(data);
            if (data?.ProjectSettings?.ObjectReviewTags?.Count > 0)
            {
                List<string> previousTags = data.ProjectSettings.ObjectReviewTags.ToList();
                data.ProjectSettings.ObjectReviewTags.Clear();
                if (!TryPersistRecipeMetadataTagDefinitions(
                    data,
                    recipeName,
                    out string error,
                    out bool appendErrorToLog))
                {
                    data.ProjectSettings.ObjectReviewTags = previousTags;
                    return WpfObjectReviewMutationResult.Failure(
                        string.Empty,
                        error,
                        appendErrorToLog);
                }

                return WpfObjectReviewMutationResult.Success(
                    focusItem: null,
                    metadata: WpfPersistentObjectMetadata.Default,
                    metadataChanges: Array.Empty<WpfObjectReviewMetadataChange>(),
                    recipeTagsChanged: true);
            }

            return WpfObjectReviewMutationResult.Failure(
                string.Empty,
                "현재 Recipe 태그 목록이 이미 비어 있습니다.");
        }

        public WpfObjectReviewMutationResult CreateGroup(
            IReadOnlyList<WpfObjectReviewObjectSnapshot> objects)
        {
            Dictionary<string, WpfObjectReviewObjectSnapshot> byKey = BuildSnapshotMap(objects);
            if (!groupSelectionService.TryCreatePlan(
                item => byKey.TryGetValue(BuildKey(item), out WpfObjectReviewObjectSnapshot snapshot)
                    ? snapshot.Metadata
                    : WpfPersistentObjectMetadata.Default,
                out WpfObjectReviewGroupCreatePlan plan,
                out string error))
            {
                return WpfObjectReviewMutationResult.Failure(string.Empty, error);
            }

            var changes = new List<WpfObjectReviewMetadataChange>();
            foreach (WpfObjectReviewItemRef member in plan.Members)
            {
                WpfPersistentObjectMetadata current = byKey.TryGetValue(
                    BuildKey(member),
                    out WpfObjectReviewObjectSnapshot snapshot)
                    ? snapshot.Metadata
                    : WpfPersistentObjectMetadata.Default;
                changes.Add(new WpfObjectReviewMetadataChange(
                    member,
                    new WpfPersistentObjectMetadata(
                        current.IsOccluded,
                        current.Tags,
                        plan.GroupId)));
            }

            return WpfObjectReviewMutationResult.Success(
                plan.Members.FirstOrDefault(),
                WpfPersistentObjectMetadata.Default,
                changes);
        }

        public IReadOnlyList<WpfObjectReviewObjectSnapshot> GetGroupMembers(
            IReadOnlyList<WpfObjectReviewObjectSnapshot> objects,
            WpfObjectReviewItemRef selected)
        {
            WpfObjectReviewObjectSnapshot selectedSnapshot = (objects
                ?? Array.Empty<WpfObjectReviewObjectSnapshot>())
                .FirstOrDefault(snapshot =>
                    snapshot?.Item != null
                    && string.Equals(BuildKey(snapshot.Item), BuildKey(selected), StringComparison.Ordinal));
            string groupId = ObjectMetadataStateService.NormalizeGroupId(
                selectedSnapshot?.Metadata?.GroupId);
            if (string.IsNullOrEmpty(groupId))
            {
                return Array.Empty<WpfObjectReviewObjectSnapshot>();
            }

            return (objects ?? Array.Empty<WpfObjectReviewObjectSnapshot>())
                .Where(snapshot =>
                    ObjectReviewGroupSelectionService.IsEligible(snapshot?.Item)
                    && string.Equals(snapshot.Metadata.GroupId, groupId, StringComparison.Ordinal))
                .ToList();
        }

        public WpfObjectReviewMutationResult RemoveSelectedFromGroup(
            IReadOnlyList<WpfObjectReviewObjectSnapshot> objects,
            WpfObjectReviewItemRef selected)
        {
            WpfObjectReviewObjectSnapshot selectedSnapshot = FindSnapshot(objects, selected);
            if (selectedSnapshot == null || string.IsNullOrEmpty(selectedSnapshot.Metadata.GroupId))
            {
                return WpfObjectReviewMutationResult.Failure(string.Empty);
            }

            MaterializedObjectSet materialized = Materialize(objects);
            ObjectMetadataStateService state = BuildState(objects, materialized);
            if (!TrySetGroupId(selectedSnapshot, materialized, state, string.Empty))
            {
                return WpfObjectReviewMutationResult.Failure(string.Empty);
            }

            int dissolvedGroupCount = state.DissolveInvalidGroups(
                materialized.ManualRois.Count,
                materialized.ManualSegments);
            return WpfObjectReviewMutationResult.Success(
                selected,
                WpfPersistentObjectMetadata.Default,
                CaptureMetadataChanges(objects, materialized, state, includeUnchanged: false),
                dissolvedGroupCount: dissolvedGroupCount);
        }

        public WpfObjectReviewMutationResult ClearGroup(
            IReadOnlyList<WpfObjectReviewObjectSnapshot> members)
        {
            MaterializedObjectSet materialized = Materialize(members);
            ObjectMetadataStateService state = BuildState(members, materialized);
            foreach (WpfObjectReviewObjectSnapshot member in members
                ?? Array.Empty<WpfObjectReviewObjectSnapshot>())
            {
                TrySetGroupId(member, materialized, state, string.Empty);
            }

            return WpfObjectReviewMutationResult.Success(
                members?.FirstOrDefault()?.Item,
                WpfPersistentObjectMetadata.Default,
                CaptureMetadataChanges(members, materialized, state, includeUnchanged: false));
        }

        public WpfObjectReviewMutationResult ToggleGroupOccluded(
            IReadOnlyList<WpfObjectReviewObjectSnapshot> members)
        {
            if (members == null || members.Count == 0)
            {
                return WpfObjectReviewMutationResult.Failure(string.Empty);
            }

            bool apply = members.Any(member => !GetOrDefault(member).IsOccluded);
            MaterializedObjectSet materialized = Materialize(members);
            ObjectMetadataStateService state = BuildState(members, materialized);
            foreach (WpfObjectReviewObjectSnapshot member in members)
            {
                TrySetOccluded(member, materialized, state, apply);
            }

            return WpfObjectReviewMutationResult.Success(
                members[0].Item,
                WpfPersistentObjectMetadata.Default,
                CaptureMetadataChanges(members, materialized, state, includeUnchanged: false),
                hasAppliedValue: true,
                appliedValue: apply);
        }

        public WpfObjectReviewMutationResult ToggleGroupTag(
            IReadOnlyList<WpfObjectReviewObjectSnapshot> members,
            string requestedTag,
            LabelingProjectData data,
            string recipeName)
        {
            string tag = ObjectMetadataStateService.NormalizeTag(requestedTag);
            if (members == null
                || members.Count == 0
                || string.IsNullOrWhiteSpace(tag))
            {
                return WpfObjectReviewMutationResult.Failure(tag);
            }

            bool apply = members.Any(member => !GetOrDefault(member).Tags.Any(value =>
                string.Equals(value, tag, StringComparison.OrdinalIgnoreCase)));
            bool recipeTagsChanged = false;
            if (apply
                && !TryEnsureRecipeMetadataTag(
                    data,
                    recipeName,
                    tag,
                    out recipeTagsChanged,
                    out string error,
                    out bool appendErrorToLog))
            {
                return WpfObjectReviewMutationResult.Failure(
                    tag,
                    error,
                    appendErrorToLog,
                    recipeTagsChanged);
            }

            MaterializedObjectSet materialized = Materialize(members);
            ObjectMetadataStateService state = BuildState(members, materialized);
            foreach (WpfObjectReviewObjectSnapshot member in members)
            {
                TrySetTag(member, materialized, state, tag, apply);
            }

            return WpfObjectReviewMutationResult.Success(
                members[0].Item,
                WpfPersistentObjectMetadata.Default,
                CaptureMetadataChanges(members, materialized, state, includeUnchanged: false),
                tag,
                recipeTagsChanged,
                hasAppliedValue: true,
                appliedValue: apply);
        }

        private bool TryEnsureRecipeMetadataTag(
            LabelingProjectData data,
            string recipeName,
            string tag,
            out bool changed,
            out string error,
            out bool appendErrorToLog)
        {
            changed = false;
            error = string.Empty;
            appendErrorToLog = false;
            EnsureProjectSettings(data);
            if (data == null)
            {
                error = "Recipe를 적용한 후 객체 태그 목록을 저장하세요.";
                return false;
            }

            List<string> tags = data.ProjectSettings.ObjectReviewTags;
            if (tags.Any(value => string.Equals(value, tag, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (tags.Count >= ObjectMetadataStateService.MaximumTagCount)
            {
                error = $"현재 Recipe에는 태그를 {ObjectMetadataStateService.MaximumTagCount}개까지 정의할 수 있습니다.";
                return false;
            }

            tags.Add(tag);
            data.ProjectSettings.EnsureDefaults();
            if (!TryPersistRecipeMetadataTagDefinitions(
                data,
                recipeName,
                out error,
                out appendErrorToLog))
            {
                data.ProjectSettings.ObjectReviewTags.RemoveAll(value =>
                    string.Equals(value, tag, StringComparison.OrdinalIgnoreCase));
                return false;
            }

            changed = true;
            return true;
        }

        private bool TryPersistRecipeMetadataTagDefinitions(
            LabelingProjectData data,
            string recipeName,
            out string error,
            out bool appendErrorToLog)
        {
            error = string.Empty;
            appendErrorToLog = false;
            if (string.IsNullOrWhiteSpace(recipeName))
            {
                error = "Recipe를 적용한 후 객체 태그 목록을 저장하세요.";
                return false;
            }

            try
            {
                projectRecipeSessionService.Save(data, recipeName);
                return true;
            }
            catch (Exception ex)
            {
                error = $"Recipe 태그 목록 저장 실패: {ex.Message}";
                appendErrorToLog = true;
                return false;
            }
        }

        private static void EnsureProjectSettings(LabelingProjectData data)
        {
            if (data == null)
            {
                return;
            }

            data.ProjectSettings ??= new LabelingProjectSettings();
            PythonModelRuntimePathResolver.ApplyDefaults(data.ProjectSettings);
            data.ProjectSettings.ObjectReviewTags ??= new List<string>();
        }

        private static bool TryBuildWorkingState(
            WpfObjectReviewObjectSnapshot snapshot,
            out MaterializedObjectSet materialized,
            out ObjectMetadataStateService state)
        {
            materialized = Materialize(snapshot == null
                ? Array.Empty<WpfObjectReviewObjectSnapshot>()
                : new[] { snapshot });
            state = BuildState(
                snapshot == null
                    ? Array.Empty<WpfObjectReviewObjectSnapshot>()
                    : new[] { snapshot },
                materialized);
            return ObjectReviewGroupSelectionService.IsEligible(snapshot?.Item);
        }

        private static bool TrySetGroupId(
            WpfObjectReviewObjectSnapshot snapshot,
            MaterializedObjectSet materialized,
            ObjectMetadataStateService state,
            string groupId)
        {
            if (snapshot?.Item == null)
            {
                return false;
            }

            if (snapshot.Item.Source == WpfObjectReviewSource.ManualRoi
                && snapshot.Item.Index >= 0
                && snapshot.Item.Index < materialized.ManualRois.Count)
            {
                state.SetManualRoiGroupId(snapshot.Item.Index, groupId);
                return true;
            }

            if (snapshot.Item.Source == WpfObjectReviewSource.ManualSegment
                && materialized.SegmentsByKey.TryGetValue(
                    BuildKey(snapshot.Item),
                    out LabelingSegmentationObject segment))
            {
                state.SetManualSegmentGroupId(segment, groupId);
                return true;
            }

            return false;
        }

        private static bool TrySetOccluded(
            WpfObjectReviewObjectSnapshot snapshot,
            MaterializedObjectSet materialized,
            ObjectMetadataStateService state,
            bool isOccluded)
        {
            if (snapshot?.Item?.Source == WpfObjectReviewSource.ManualRoi
                && snapshot.Item.Index >= 0
                && snapshot.Item.Index < materialized.ManualRois.Count)
            {
                state.SetManualRoiOccluded(snapshot.Item.Index, isOccluded);
                return true;
            }

            if (snapshot?.Item?.Source == WpfObjectReviewSource.ManualSegment
                && materialized.SegmentsByKey.TryGetValue(
                    BuildKey(snapshot.Item),
                    out LabelingSegmentationObject segment))
            {
                state.SetManualSegmentOccluded(segment, isOccluded);
                return true;
            }

            return false;
        }

        private static bool TrySetTag(
            WpfObjectReviewObjectSnapshot snapshot,
            MaterializedObjectSet materialized,
            ObjectMetadataStateService state,
            string tag,
            bool isApplied)
        {
            if (snapshot?.Item?.Source == WpfObjectReviewSource.ManualRoi
                && snapshot.Item.Index >= 0
                && snapshot.Item.Index < materialized.ManualRois.Count)
            {
                state.SetManualRoiTag(snapshot.Item.Index, tag, isApplied);
                return true;
            }

            if (snapshot?.Item?.Source == WpfObjectReviewSource.ManualSegment
                && materialized.SegmentsByKey.TryGetValue(
                    BuildKey(snapshot.Item),
                    out LabelingSegmentationObject segment))
            {
                state.SetManualSegmentTag(segment, tag, isApplied);
                return true;
            }

            return false;
        }

        private static ObjectMetadataStateService BuildState(
            IReadOnlyList<WpfObjectReviewObjectSnapshot> objects,
            MaterializedObjectSet materialized)
        {
            var state = new ObjectMetadataStateService();
            foreach (WpfObjectReviewObjectSnapshot snapshot in objects
                ?? Array.Empty<WpfObjectReviewObjectSnapshot>())
            {
                if (snapshot?.Item == null)
                {
                    continue;
                }

                if (snapshot.Item.Source == WpfObjectReviewSource.ManualRoi)
                {
                    state.SetManualRoiMetadata(snapshot.Item.Index, snapshot.Metadata);
                }
                else if (snapshot.Item.Source == WpfObjectReviewSource.ManualSegment
                    && materialized.SegmentsByKey.TryGetValue(
                        BuildKey(snapshot.Item),
                        out LabelingSegmentationObject segment))
                {
                    state.SetManualSegmentMetadata(segment, snapshot.Metadata);
                }
            }

            return state;
        }

        private static IReadOnlyList<WpfObjectReviewMetadataChange> CaptureMetadataChanges(
            IReadOnlyList<WpfObjectReviewObjectSnapshot> objects,
            MaterializedObjectSet materialized,
            ObjectMetadataStateService state,
            bool includeUnchanged)
        {
            var changes = new List<WpfObjectReviewMetadataChange>();
            foreach (WpfObjectReviewObjectSnapshot snapshot in objects
                ?? Array.Empty<WpfObjectReviewObjectSnapshot>())
            {
                if (!TryGetMetadata(snapshot, materialized, state, out WpfPersistentObjectMetadata metadata))
                {
                    continue;
                }

                if (includeUnchanged || !AreEqual(snapshot.Metadata, metadata))
                {
                    changes.Add(new WpfObjectReviewMetadataChange(snapshot.Item, metadata));
                }
            }

            return changes;
        }

        private static bool TryGetMetadata(
            WpfObjectReviewObjectSnapshot snapshot,
            MaterializedObjectSet materialized,
            ObjectMetadataStateService state,
            out WpfPersistentObjectMetadata metadata)
        {
            metadata = WpfPersistentObjectMetadata.Default;
            if (snapshot?.Item == null)
            {
                return false;
            }

            if (snapshot.Item.Source == WpfObjectReviewSource.ManualRoi
                && snapshot.Item.Index >= 0
                && snapshot.Item.Index < materialized.ManualRois.Count)
            {
                metadata = state.GetManualRoiMetadata(snapshot.Item.Index);
                return true;
            }

            if (snapshot.Item.Source == WpfObjectReviewSource.ManualSegment
                && materialized.SegmentsByKey.TryGetValue(
                    BuildKey(snapshot.Item),
                    out LabelingSegmentationObject segment))
            {
                metadata = state.GetManualSegmentMetadata(segment);
                return true;
            }

            return false;
        }

        private static MaterializedObjectSet Materialize(
            IReadOnlyList<WpfObjectReviewObjectSnapshot> objects)
        {
            IReadOnlyList<WpfObjectReviewObjectSnapshot> safeObjects =
                objects ?? Array.Empty<WpfObjectReviewObjectSnapshot>();
            int roiCount = safeObjects
                .Where(snapshot => snapshot?.Item?.Source == WpfObjectReviewSource.ManualRoi)
                .Select(snapshot => snapshot.Item.Index + 1)
                .DefaultIfEmpty(0)
                .Max();
            var rois = Enumerable.Repeat(Rectangle.Empty, Math.Max(0, roiCount)).ToList();
            var classNames = Enumerable.Repeat(string.Empty, Math.Max(0, roiCount)).ToList();
            var segments = new List<LabelingSegmentationObject>();
            var segmentsByKey = new Dictionary<string, LabelingSegmentationObject>(StringComparer.Ordinal);

            foreach (WpfObjectReviewObjectSnapshot snapshot in safeObjects)
            {
                if (!ObjectReviewGroupSelectionService.IsEligible(snapshot?.Item))
                {
                    continue;
                }

                if (snapshot.Item.Source == WpfObjectReviewSource.ManualRoi
                    && snapshot.Item.Index < rois.Count)
                {
                    rois[snapshot.Item.Index] = snapshot.Bounds;
                    classNames[snapshot.Item.Index] = snapshot.ClassName;
                    continue;
                }

                if (snapshot.Item.Source == WpfObjectReviewSource.ManualSegment)
                {
                    var segment = new LabelingSegmentationObject
                    {
                        ObjectId = snapshot.ObjectId,
                        ClassName = snapshot.ClassName
                    };
                    segments.Add(segment);
                    segmentsByKey[BuildKey(snapshot.Item)] = segment;
                }
            }

            return new MaterializedObjectSet(rois, classNames, segments, segmentsByKey);
        }

        private static Dictionary<string, WpfObjectReviewObjectSnapshot> BuildSnapshotMap(
            IReadOnlyList<WpfObjectReviewObjectSnapshot> objects)
            => (objects ?? Array.Empty<WpfObjectReviewObjectSnapshot>())
                .Where(snapshot => snapshot?.Item != null)
                .GroupBy(snapshot => BuildKey(snapshot.Item), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        private static WpfObjectReviewObjectSnapshot FindSnapshot(
            IReadOnlyList<WpfObjectReviewObjectSnapshot> objects,
            WpfObjectReviewItemRef item)
            => (objects ?? Array.Empty<WpfObjectReviewObjectSnapshot>())
                .FirstOrDefault(snapshot =>
                    snapshot?.Item != null
                    && string.Equals(BuildKey(snapshot.Item), BuildKey(item), StringComparison.Ordinal));

        private static WpfPersistentObjectMetadata GetOrDefault(
            WpfObjectReviewObjectSnapshot snapshot)
            => snapshot?.Metadata ?? WpfPersistentObjectMetadata.Default;

        private static bool AreEqual(
            WpfPersistentObjectMetadata left,
            WpfPersistentObjectMetadata right)
            => left?.IsOccluded == right?.IsOccluded
                && string.Equals(left?.GroupId, right?.GroupId, StringComparison.Ordinal)
                && (left?.Tags ?? Array.Empty<string>())
                    .SequenceEqual(right?.Tags ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

        private static string BuildKey(WpfObjectReviewItemRef item)
            => item == null ? string.Empty : $"{item.Source}:{item.Index}";

        private sealed class MaterializedObjectSet
        {
            internal MaterializedObjectSet(
                List<Rectangle> manualRois,
                List<string> manualRoiClassNames,
                List<LabelingSegmentationObject> manualSegments,
                Dictionary<string, LabelingSegmentationObject> segmentsByKey)
            {
                ManualRois = manualRois;
                ManualRoiClassNames = manualRoiClassNames;
                ManualSegments = manualSegments;
                SegmentsByKey = segmentsByKey;
            }

            internal List<Rectangle> ManualRois { get; }
            internal List<string> ManualRoiClassNames { get; }
            internal List<LabelingSegmentationObject> ManualSegments { get; }
            internal Dictionary<string, LabelingSegmentationObject> SegmentsByKey { get; }
        }
    }

    [Obsolete("Use ObjectReviewWorkflowService.", false)]
    public sealed class WpfObjectReviewWorkflowService : ObjectReviewWorkflowService
    {
        public WpfObjectReviewWorkflowService(
            ObjectMetadataPersistenceService metadataPersistenceService,
            ProjectRecipeSessionService projectRecipeSessionService)
            : base(metadataPersistenceService, projectRecipeSessionService)
        {
        }
    }

    public sealed class WpfObjectReviewPersistenceRequest
    {
        public WpfObjectReviewPersistenceRequest(
            string imagePath,
            string imageName,
            LabelingProjectData data,
            IReadOnlyList<WpfObjectReviewObjectSnapshot> objects)
        {
            ImagePath = imagePath ?? string.Empty;
            ImageName = imageName ?? string.Empty;
            Data = data;
            Objects = (objects ?? Array.Empty<WpfObjectReviewObjectSnapshot>()).ToList();
        }

        public string ImagePath { get; }
        public string ImageName { get; }
        public LabelingProjectData Data { get; }
        public IReadOnlyList<WpfObjectReviewObjectSnapshot> Objects { get; }
    }

    public sealed class WpfObjectReviewObjectSnapshot
    {
        public WpfObjectReviewObjectSnapshot(
            WpfObjectReviewItemRef item,
            Rectangle bounds,
            string className,
            string objectId,
            WpfPersistentObjectMetadata metadata)
        {
            Item = item;
            Bounds = bounds;
            ClassName = className ?? string.Empty;
            ObjectId = objectId ?? string.Empty;
            Metadata = metadata ?? WpfPersistentObjectMetadata.Default;
        }

        public WpfObjectReviewItemRef Item { get; }
        public Rectangle Bounds { get; }
        public string ClassName { get; }
        public string ObjectId { get; }
        public WpfPersistentObjectMetadata Metadata { get; }
    }

    public sealed class WpfObjectReviewMetadataChange
    {
        public WpfObjectReviewMetadataChange(
            WpfObjectReviewItemRef item,
            WpfPersistentObjectMetadata metadata)
        {
            Item = item;
            Metadata = metadata ?? WpfPersistentObjectMetadata.Default;
        }

        public WpfObjectReviewItemRef Item { get; }
        public WpfPersistentObjectMetadata Metadata { get; }
    }

    public sealed class WpfObjectReviewLoadResult
    {
        public WpfObjectReviewLoadResult(
            WpfObjectMetadataLoadResult persistenceResult,
            bool shouldClearExistingState,
            IReadOnlyList<WpfObjectReviewMetadataChange> metadataChanges)
        {
            PersistenceResult = persistenceResult ?? WpfObjectMetadataLoadResult.Empty;
            ShouldClearExistingState = shouldClearExistingState;
            MetadataChanges = metadataChanges ?? Array.Empty<WpfObjectReviewMetadataChange>();
        }

        public WpfObjectMetadataLoadResult PersistenceResult { get; }
        public bool ShouldClearExistingState { get; }
        public IReadOnlyList<WpfObjectReviewMetadataChange> MetadataChanges { get; }
    }

    public sealed class WpfObjectReviewMutationResult
    {
        public WpfObjectReviewMutationResult(
            bool isApplicable,
            string errorMessage,
            bool appendErrorToLog,
            WpfObjectReviewItemRef focusItem,
            WpfPersistentObjectMetadata metadata,
            IReadOnlyList<WpfObjectReviewMetadataChange> metadataChanges,
            string tag,
            bool recipeTagsChanged,
            int dissolvedGroupCount,
            bool hasAppliedValue,
            bool appliedValue)
        {
            IsApplicable = isApplicable;
            ErrorMessage = errorMessage ?? string.Empty;
            AppendErrorToLog = appendErrorToLog;
            FocusItem = focusItem;
            Metadata = metadata ?? WpfPersistentObjectMetadata.Default;
            MetadataChanges = metadataChanges ?? Array.Empty<WpfObjectReviewMetadataChange>();
            Tag = tag ?? string.Empty;
            RecipeTagsChanged = recipeTagsChanged;
            DissolvedGroupCount = Math.Max(0, dissolvedGroupCount);
            HasAppliedValue = hasAppliedValue;
            AppliedValue = appliedValue;
        }

        public bool IsApplicable { get; }
        public string ErrorMessage { get; }
        public bool AppendErrorToLog { get; }
        public WpfObjectReviewItemRef FocusItem { get; }
        public WpfPersistentObjectMetadata Metadata { get; }
        public IReadOnlyList<WpfObjectReviewMetadataChange> MetadataChanges { get; }
        public string Tag { get; }
        public bool RecipeTagsChanged { get; }
        public int DissolvedGroupCount { get; }
        public bool HasAppliedValue { get; }
        public bool AppliedValue { get; }

        internal static WpfObjectReviewMutationResult Success(
            WpfObjectReviewItemRef focusItem,
            WpfPersistentObjectMetadata metadata,
            IReadOnlyList<WpfObjectReviewMetadataChange> metadataChanges,
            string tag = "",
            bool recipeTagsChanged = false,
            int dissolvedGroupCount = 0,
            bool hasAppliedValue = false,
            bool appliedValue = false)
            => new WpfObjectReviewMutationResult(
                true,
                string.Empty,
                false,
                focusItem,
                metadata,
                metadataChanges,
                tag,
                recipeTagsChanged,
                dissolvedGroupCount,
                hasAppliedValue,
                appliedValue);

        internal static WpfObjectReviewMutationResult Failure(
            string tag,
            string errorMessage = "",
            bool appendErrorToLog = false,
            bool recipeTagsChanged = false)
            => new WpfObjectReviewMutationResult(
                false,
                errorMessage,
                appendErrorToLog,
                null,
                WpfPersistentObjectMetadata.Default,
                Array.Empty<WpfObjectReviewMetadataChange>(),
                tag,
                recipeTagsChanged,
                0,
                false,
                false);
    }
}
