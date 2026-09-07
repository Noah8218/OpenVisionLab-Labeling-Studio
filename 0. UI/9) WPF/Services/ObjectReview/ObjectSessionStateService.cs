using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MvcVisionSystem
{
    public enum WpfObjectSessionStateKind
    {
        Hidden,
        Locked,
        Pinned
    }

    public class ObjectSessionStateService
    {
        private readonly Dictionary<int, MutableState> manualRoiStates = new Dictionary<int, MutableState>();
        private readonly Dictionary<string, MutableState> segmentIdStates =
            new Dictionary<string, MutableState>(StringComparer.Ordinal);
        private readonly Dictionary<LabelingSegmentationObject, MutableState> segmentReferenceStates =
            new Dictionary<LabelingSegmentationObject, MutableState>(ReferenceComparer.Instance);

        public WpfObjectSessionState GetManualRoiState(int index)
            => index >= 0 && manualRoiStates.TryGetValue(index, out MutableState state)
                ? state.Snapshot()
                : WpfObjectSessionState.Default;

        public WpfObjectSessionState GetManualSegmentState(LabelingSegmentationObject segment)
        {
            if (segment == null)
            {
                return WpfObjectSessionState.Default;
            }

            MutableState state = ResolveSegmentState(segment, create: false);
            return state?.Snapshot() ?? WpfObjectSessionState.Default;
        }

        public WpfObjectSessionState ToggleManualRoiState(int index, WpfObjectSessionStateKind kind)
        {
            if (index < 0)
            {
                return WpfObjectSessionState.Default;
            }

            if (!manualRoiStates.TryGetValue(index, out MutableState state))
            {
                state = new MutableState();
                manualRoiStates[index] = state;
            }

            Toggle(state, kind);
            RemoveDefaultManualRoiState(index, state);
            return state.Snapshot();
        }

        public WpfObjectSessionState ToggleManualSegmentState(
            LabelingSegmentationObject segment,
            WpfObjectSessionStateKind kind)
        {
            MutableState state = ResolveSegmentState(segment, create: true);
            if (state == null)
            {
                return WpfObjectSessionState.Default;
            }

            Toggle(state, kind);
            RemoveDefaultSegmentState(segment, state);
            return state.Snapshot();
        }

        public void ShiftRoiStatesAfterRemoval(int removedIndex)
        {
            if (removedIndex < 0 || manualRoiStates.Count == 0)
            {
                return;
            }

            var shifted = new Dictionary<int, MutableState>();
            foreach (KeyValuePair<int, MutableState> pair in manualRoiStates)
            {
                if (pair.Key == removedIndex)
                {
                    continue;
                }

                shifted[pair.Key > removedIndex ? pair.Key - 1 : pair.Key] = pair.Value;
            }

            manualRoiStates.Clear();
            foreach (KeyValuePair<int, MutableState> pair in shifted)
            {
                manualRoiStates[pair.Key] = pair.Value;
            }
        }

        public void RemoveManualSegment(LabelingSegmentationObject segment)
        {
            if (segment == null)
            {
                return;
            }

            segmentReferenceStates.Remove(segment);
            string objectId = NormalizeObjectId(segment.ObjectId);
            if (!string.IsNullOrEmpty(objectId))
            {
                segmentIdStates.Remove(objectId);
            }
        }

        public void Clear()
        {
            manualRoiStates.Clear();
            segmentIdStates.Clear();
            segmentReferenceStates.Clear();
        }

        private MutableState ResolveSegmentState(LabelingSegmentationObject segment, bool create)
        {
            if (segment == null)
            {
                return null;
            }

            string objectId = NormalizeObjectId(segment.ObjectId);
            if (!string.IsNullOrEmpty(objectId))
            {
                if (segmentIdStates.TryGetValue(objectId, out MutableState stableState))
                {
                    return stableState;
                }

                if (segmentReferenceStates.TryGetValue(segment, out MutableState referenceState))
                {
                    segmentReferenceStates.Remove(segment);
                    segmentIdStates[objectId] = referenceState;
                    return referenceState;
                }

                if (!create)
                {
                    return null;
                }

                var createdStableState = new MutableState();
                segmentIdStates[objectId] = createdStableState;
                return createdStableState;
            }

            if (segmentReferenceStates.TryGetValue(segment, out MutableState state))
            {
                return state;
            }

            if (!create)
            {
                return null;
            }

            var createdReferenceState = new MutableState();
            segmentReferenceStates[segment] = createdReferenceState;
            return createdReferenceState;
        }

        private void RemoveDefaultManualRoiState(int index, MutableState state)
        {
            if (state?.IsDefault == true)
            {
                manualRoiStates.Remove(index);
            }
        }

        private void RemoveDefaultSegmentState(LabelingSegmentationObject segment, MutableState state)
        {
            if (segment == null || state?.IsDefault != true)
            {
                return;
            }

            segmentReferenceStates.Remove(segment);
            string objectId = NormalizeObjectId(segment.ObjectId);
            if (!string.IsNullOrEmpty(objectId))
            {
                segmentIdStates.Remove(objectId);
            }
        }

        private static void Toggle(MutableState state, WpfObjectSessionStateKind kind)
        {
            switch (kind)
            {
                case WpfObjectSessionStateKind.Hidden:
                    state.IsHidden = !state.IsHidden;
                    break;
                case WpfObjectSessionStateKind.Locked:
                    state.IsLocked = !state.IsLocked;
                    break;
                case WpfObjectSessionStateKind.Pinned:
                    state.IsPinned = !state.IsPinned;
                    break;
            }
        }

        private static string NormalizeObjectId(string objectId)
            => string.IsNullOrWhiteSpace(objectId) ? string.Empty : objectId.Trim();

        private sealed class MutableState
        {
            public bool IsHidden { get; set; }

            public bool IsLocked { get; set; }

            public bool IsPinned { get; set; }

            public bool IsDefault => !IsHidden && !IsLocked && !IsPinned;

            public WpfObjectSessionState Snapshot()
                => new WpfObjectSessionState(IsHidden, IsLocked, IsPinned);
        }

        private sealed class ReferenceComparer : IEqualityComparer<LabelingSegmentationObject>
        {
            public static ReferenceComparer Instance { get; } = new ReferenceComparer();

            public bool Equals(LabelingSegmentationObject x, LabelingSegmentationObject y)
                => ReferenceEquals(x, y);

            public int GetHashCode(LabelingSegmentationObject obj)
                => obj == null ? 0 : RuntimeHelpers.GetHashCode(obj);
        }
    }

    public sealed class WpfObjectSessionState
    {
        public static WpfObjectSessionState Default { get; } =
            new WpfObjectSessionState(isHidden: false, isLocked: false, isPinned: false);

        public WpfObjectSessionState(bool isHidden, bool isLocked, bool isPinned)
        {
            IsHidden = isHidden;
            IsLocked = isLocked;
            IsPinned = isPinned;
        }

        public bool IsHidden { get; }

        public bool IsLocked { get; }

        public bool IsPinned { get; }

        public bool IsDefault => !IsHidden && !IsLocked && !IsPinned;

        public string BadgeText
        {
            get
            {
                var badges = new List<string>(3);
                if (IsHidden)
                {
                    badges.Add("\uC228\uAE40");
                }

                if (IsLocked)
                {
                    badges.Add("\uC7A0\uAE08");
                }

                if (IsPinned)
                {
                    badges.Add("\uC774\uB3D9 \uACE0\uC815");
                }

                return string.Join(" \u00B7 ", badges);
            }
        }
    }

    [Obsolete("Use ObjectSessionStateService.", false)]
    public sealed class WpfObjectSessionStateService : ObjectSessionStateService
    {
    }
}
