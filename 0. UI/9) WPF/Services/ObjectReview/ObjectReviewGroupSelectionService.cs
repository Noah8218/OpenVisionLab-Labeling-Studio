using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem
{
    public class ObjectReviewGroupSelectionService
    {
        private readonly Dictionary<string, WpfObjectReviewItemRef> selected =
            new Dictionary<string, WpfObjectReviewItemRef>(StringComparer.Ordinal);

        public bool IsActive { get; private set; }

        public int SelectedCount => selected.Count;

        public IReadOnlyList<WpfObjectReviewItemRef> SelectedItems
            => selected.Values
                .OrderBy(item => item.Source)
                .ThenBy(item => item.Index)
                .ToList();

        public void Begin()
        {
            selected.Clear();
            IsActive = true;
        }

        public void Cancel()
        {
            selected.Clear();
            IsActive = false;
        }

        public bool SetSelected(
            WpfObjectReviewItemRef item,
            bool isSelected,
            out string error)
        {
            error = string.Empty;
            if (!IsActive)
            {
                error = "\uADF8\uB8F9 \uAD6C\uC131\uC744 \uBA3C\uC800 \uC2DC\uC791\uD558\uC138\uC694.";
                return false;
            }

            if (!IsEligible(item))
            {
                error = "\uC800\uC7A5\uB41C \uC218\uB3D9 \uBC15\uC2A4\uC640 \uC138\uADF8\uBA3C\uD2B8\uB9CC \uADF8\uB8F9\uC73C\uB85C \uBB36\uC744 \uC218 \uC788\uC2B5\uB2C8\uB2E4.";
                return false;
            }

            string key = BuildKey(item);
            if (isSelected)
            {
                selected[key] = item;
            }
            else
            {
                selected.Remove(key);
            }

            return true;
        }

        public bool TryCreatePlan(
            Func<WpfObjectReviewItemRef, WpfPersistentObjectMetadata> metadataResolver,
            out WpfObjectReviewGroupCreatePlan plan,
            out string error)
        {
            plan = null;
            error = string.Empty;
            IReadOnlyList<WpfObjectReviewItemRef> items = SelectedItems;
            if (!IsActive || items.Count < 2)
            {
                error = "\uADF8\uB8F9\uC73C\uB85C \uBB36\uC744 \uC800\uC7A5 \uAC1D\uCCB4\uB97C 2\uAC1C \uC774\uC0C1 \uC120\uD0DD\uD558\uC138\uC694.";
                return false;
            }

            if (items.Any(item =>
                !string.IsNullOrWhiteSpace(metadataResolver?.Invoke(item)?.GroupId)))
            {
                error = "\uC774\uBBF8 \uADF8\uB8F9\uC5D0 \uC18D\uD55C \uAC1D\uCCB4\uAC00 \uC788\uC2B5\uB2C8\uB2E4. \uBA3C\uC800 \uADF8\uB8F9\uC5D0\uC11C \uC81C\uAC70\uD558\uC138\uC694.";
                return false;
            }

            plan = new WpfObjectReviewGroupCreatePlan(
                Guid.NewGuid().ToString("N"),
                items);
            return true;
        }

        public static bool IsEligible(WpfObjectReviewItemRef item)
            => item != null
                && item.Index >= 0
                && (item.Source == WpfObjectReviewSource.ManualRoi
                    || item.Source == WpfObjectReviewSource.ManualSegment);

        private static string BuildKey(WpfObjectReviewItemRef item)
            => $"{item.Source}:{item.Index}";
    }

    [Obsolete("Use ObjectReviewGroupSelectionService.", false)]
    public sealed class WpfObjectReviewGroupSelectionService : ObjectReviewGroupSelectionService
    {
    }

    public sealed class WpfObjectReviewGroupCreatePlan
    {
        public WpfObjectReviewGroupCreatePlan(
            string groupId,
            IReadOnlyList<WpfObjectReviewItemRef> members)
        {
            GroupId = ObjectMetadataStateService.NormalizeGroupId(groupId);
            Members = members ?? Array.Empty<WpfObjectReviewItemRef>();
        }

        public string GroupId { get; }

        public IReadOnlyList<WpfObjectReviewItemRef> Members { get; }
    }
}
