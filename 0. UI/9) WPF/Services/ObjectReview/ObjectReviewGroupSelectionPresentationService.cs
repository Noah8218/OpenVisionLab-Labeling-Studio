using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem
{
    /// <summary>
    /// Builds the Object Review group-selection presentation snapshot. The
    /// ViewModel owns WPF properties and row selection mutation; the workflow
    /// service owns group creation and persistence policy.
    /// </summary>
    public sealed class ObjectReviewGroupSelectionPresentationService
    {
        public ObjectReviewGroupSelectionPresentationSnapshot Build(
            IReadOnlyList<WpfObjectReviewListItem> objects,
            bool isGroupSelectionMode,
            string statusText = "")
        {
            List<WpfObjectReviewListItem> selectedRows = (objects ?? Array.Empty<WpfObjectReviewListItem>())
                .Where(item => item?.IsGroupSelected == true && item.CanGroupSelect)
                .ToList();
            int selectedCount = selectedRows.Count;
            string selectedTypes = string.Join(
                " / ",
                selectedRows
                    .GroupBy(GetGroupPreviewObjectType, StringComparer.Ordinal)
                    .Select(group => $"{group.Key} {group.Count()}개"));
            string selectedClasses = string.Join(
                ", ",
                selectedRows
                    .Select(GetGroupPreviewClassName)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(3));
            string preview = string.Join(
                " · ",
                new[]
                {
                    $"그룹 선택 {selectedCount}개",
                    selectedTypes,
                    string.IsNullOrWhiteSpace(selectedClasses)
                        ? string.Empty
                        : $"클래스 {selectedClasses}"
                }.Where(value => !string.IsNullOrWhiteSpace(value)));
            string resolvedStatusText = !string.IsNullOrWhiteSpace(statusText)
                ? statusText.Trim()
                : isGroupSelectionMode
                    ? $"{preview} · 2개 이상 선택 후 그룹 만들기"
                    : "그룹 구성을 시작하면 저장 객체를 2개 이상 선택합니다.";

            return new ObjectReviewGroupSelectionPresentationSnapshot(
                selectedCount,
                isGroupSelectionMode && selectedCount >= 2,
                resolvedStatusText);
        }

        private static string GetGroupPreviewObjectType(WpfObjectReviewListItem item)
            => item?.IsManualSegment == true
                ? item.IsManualPolygon ? "폴리곤" : "마스크"
                : "박스";

        private static string GetGroupPreviewClassName(WpfObjectReviewListItem item)
        {
            string text = item?.DisplayText ?? string.Empty;
            int prefixEnd = text.IndexOf(". ", StringComparison.Ordinal);
            if (prefixEnd >= 0)
            {
                text = text.Substring(prefixEnd + 2);
            }

            int detailStart = text.IndexOf(" /", StringComparison.Ordinal);
            return (detailStart >= 0 ? text.Substring(0, detailStart) : text).Trim();
        }
    }

    public sealed class ObjectReviewGroupSelectionPresentationSnapshot
    {
        public ObjectReviewGroupSelectionPresentationSnapshot(
            int selectedCount,
            bool isCreateGroupEnabled,
            string statusText)
        {
            SelectedCount = Math.Max(0, selectedCount);
            IsCreateGroupEnabled = isCreateGroupEnabled;
            StatusText = statusText ?? string.Empty;
        }

        public int SelectedCount { get; }

        public bool IsCreateGroupEnabled { get; }

        public string StatusText { get; }
    }
}
