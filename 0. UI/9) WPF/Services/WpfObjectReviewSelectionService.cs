using System;
using System.Collections.Generic;

namespace MvcVisionSystem
{
    public static class WpfObjectReviewSelectionService
    {
        public static bool TryResolveSelectedItem(
            WpfObjectReviewListItem selectedObject,
            IReadOnlyList<string> manualOverlayIds,
            int manualRoiCount,
            out WpfObjectReviewItemRef item)
        {
            item = null;
            if (!(selectedObject?.Payload is WpfObjectReviewItemRef payload))
            {
                return false;
            }

            item = ResolveItem(payload, manualOverlayIds, manualRoiCount);
            return item != null;
        }

        public static WpfObjectReviewItemRef ResolveItem(
            WpfObjectReviewItemRef item,
            IReadOnlyList<string> manualOverlayIds,
            int manualRoiCount)
        {
            if (item?.Source != WpfObjectReviewSource.ManualRoi)
            {
                return item;
            }

            int manualIndex = ResolveManualRoiIndex(item, manualOverlayIds, manualRoiCount);
            return manualIndex >= 0
                ? WpfObjectReviewItemRef.Manual(manualIndex, GetManualRoiOverlayId(manualOverlayIds, manualIndex))
                : null;
        }

        public static int ResolveManualRoiIndex(
            WpfObjectReviewItemRef item,
            IReadOnlyList<string> manualOverlayIds,
            int manualRoiCount)
        {
            if (item == null)
            {
                return -1;
            }

            // SourceId is the stable canvas overlay id; row indexes can be stale after large-list incremental deletes.
            int indexByOverlay = FindManualRoiIndexByOverlayId(manualOverlayIds, item.SourceId);
            if (indexByOverlay >= 0)
            {
                return indexByOverlay;
            }

            return item.Index >= 0 && item.Index < manualRoiCount
                ? item.Index
                : -1;
        }

        public static int FindManualRoiIndexByOverlayId(IReadOnlyList<string> manualOverlayIds, string overlayId)
        {
            if (string.IsNullOrWhiteSpace(overlayId) || manualOverlayIds == null)
            {
                return -1;
            }

            for (int i = 0; i < manualOverlayIds.Count; i++)
            {
                if (string.Equals(manualOverlayIds[i], overlayId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        public static string GetManualRoiOverlayId(IReadOnlyList<string> manualOverlayIds, int index)
        {
            return manualOverlayIds != null && index >= 0 && index < manualOverlayIds.Count
                ? manualOverlayIds[index] ?? string.Empty
                : string.Empty;
        }

        public static bool IsSource(WpfObjectReviewListItem item, WpfObjectReviewSource source)
        {
            return string.Equals(item?.SourceKey, source.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        public static int GetSelectedRowIndex(IList<WpfObjectReviewListItem> objects, WpfObjectReviewListItem selected)
        {
            return selected != null && objects != null
                ? objects.IndexOf(selected)
                : -1;
        }

        public static bool CanReplaceManualRoiRow(
            IList<WpfObjectReviewListItem> objects,
            int manualRoiIndex,
            int manualRoiCount)
        {
            if (manualRoiIndex < 0
                || manualRoiIndex >= manualRoiCount
                || objects == null
                || manualRoiIndex >= objects.Count)
            {
                return false;
            }

            WpfObjectReviewListItem currentRow = objects[manualRoiIndex];
            return IsSource(currentRow, WpfObjectReviewSource.ManualRoi)
                && currentRow.SourceIndex == manualRoiIndex;
        }

        public static bool ShouldUseIncrementalDelete(
            WpfObjectReviewSource deletedSource,
            int remainingObjectCount,
            int fullRefreshDeleteLimit,
            int deletedObjectRowIndex,
            int currentObjectRowCount)
        {
            // Manual ROI delete must never publish a full Reset; even small lists can stall because WPF rebuilds selection and canvas handles together.
            return deletedSource == WpfObjectReviewSource.ManualRoi
                && remainingObjectCount >= 0
                && deletedObjectRowIndex >= 0
                && deletedObjectRowIndex < currentObjectRowCount;
        }
        public static int GetSelectionIndexAfterDelete(int deletedObjectRowIndex, int remainingObjectRowCount)
        {
            if (remainingObjectRowCount <= 0)
            {
                return -1;
            }

            return Math.Max(0, Math.Min(deletedObjectRowIndex, remainingObjectRowCount - 1));
        }
    }
}