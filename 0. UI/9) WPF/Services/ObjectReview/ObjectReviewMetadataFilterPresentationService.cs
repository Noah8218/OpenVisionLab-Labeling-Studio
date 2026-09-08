using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem
{
    /// <summary>
    /// Computes Object Review metadata catalogs and filter presentation. The
    /// ViewModel applies the snapshots to WPF collections and row properties;
    /// ObjectMetadataStateService remains the metadata mutation owner.
    /// </summary>
    public sealed class ObjectReviewMetadataFilterPresentationService
    {
        public const string AllMetadataTagsFilter = "전체 태그";
        public const string AllGroupsFilter = "전체 그룹";
        public const string UngroupedFilter = "미그룹";

        public ObjectReviewMetadataTagCatalogSnapshot BuildTagCatalog(
            IEnumerable<string> recipeMetadataTags,
            IReadOnlyList<WpfObjectReviewListItem> objects,
            string selectedTag,
            string selectedFilter)
        {
            IReadOnlyList<string> tags = ObjectMetadataStateService.NormalizeTags(
                (recipeMetadataTags ?? Array.Empty<string>()).Concat(
                    (objects ?? Array.Empty<WpfObjectReviewListItem>())
                        .SelectMany(item => item?.MetadataTags ?? Array.Empty<string>())));
            string normalizedSelectedTag = selectedTag ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedSelectedTag) && tags.Count > 0)
            {
                normalizedSelectedTag = tags[0];
            }

            string normalizedSelectedFilter = string.IsNullOrWhiteSpace(selectedFilter)
                ? AllMetadataTagsFilter
                : selectedFilter.Trim();
            if (!tags.Any(tag => string.Equals(tag, normalizedSelectedFilter, StringComparison.OrdinalIgnoreCase)))
            {
                normalizedSelectedFilter = AllMetadataTagsFilter;
            }

            return new ObjectReviewMetadataTagCatalogSnapshot(
                tags,
                new[] { AllMetadataTagsFilter }.Concat(tags).ToList(),
                normalizedSelectedTag,
                normalizedSelectedFilter);
        }

        public ObjectReviewGroupCatalogSnapshot BuildGroupCatalog(
            IReadOnlyList<WpfObjectReviewListItem> objects,
            string selectedFilter)
        {
            IReadOnlyList<WpfObjectReviewListItem> rows =
                objects ?? Array.Empty<WpfObjectReviewListItem>();
            List<ObjectReviewGroupPresentation> groups = rows
                .Where(item => item?.IsEnabled == true && !string.IsNullOrWhiteSpace(item.GroupId))
                .GroupBy(item => item.GroupId, StringComparer.Ordinal)
                .OrderBy(group => IndexOf(rows, group.First()))
                .Select((group, index) => new ObjectReviewGroupPresentation(
                    group.Key,
                    $"그룹 {index + 1} ({group.Count()}개)",
                    group.Count()))
                .ToList();
            List<string> filters = new List<string> { AllGroupsFilter, UngroupedFilter };
            filters.AddRange(groups.Select(group => group.DisplayText));
            string normalizedSelectedFilter = string.IsNullOrWhiteSpace(selectedFilter)
                ? AllGroupsFilter
                : selectedFilter.Trim();
            if (!filters.Any(value => string.Equals(value, normalizedSelectedFilter, StringComparison.OrdinalIgnoreCase)))
            {
                normalizedSelectedFilter = AllGroupsFilter;
            }

            return new ObjectReviewGroupCatalogSnapshot(groups, filters, normalizedSelectedFilter);
        }

        private static int IndexOf(
            IReadOnlyList<WpfObjectReviewListItem> rows,
            WpfObjectReviewListItem target)
        {
            for (int index = 0; index < rows.Count; index++)
            {
                if (ReferenceEquals(rows[index], target))
                {
                    return index;
                }
            }

            return int.MaxValue;
        }

        public ObjectReviewMetadataFilterSnapshot BuildFilter(
            IReadOnlyList<WpfObjectReviewListItem> objects,
            string selectedMetadataTagFilter,
            string selectedGroupFilter,
            bool isOccludedFilterActive,
            bool selectFirstMatch)
        {
            IReadOnlyList<WpfObjectReviewListItem> rows =
                objects ?? Array.Empty<WpfObjectReviewListItem>();
            string tagFilter = string.Equals(
                selectedMetadataTagFilter,
                AllMetadataTagsFilter,
                StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : selectedMetadataTagFilter?.Trim() ?? string.Empty;
            string groupFilter = string.IsNullOrWhiteSpace(selectedGroupFilter)
                ? AllGroupsFilter
                : selectedGroupFilter.Trim();
            bool[] matches = new bool[rows.Count];
            int enabledCount = 0;
            int visibleCount = 0;
            int firstMatchingIndex = -1;
            for (int index = 0; index < rows.Count; index++)
            {
                WpfObjectReviewListItem item = rows[index];
                if (item?.IsEnabled != true)
                {
                    matches[index] = true;
                    continue;
                }

                enabledCount++;
                bool matchesFilter = (!isOccludedFilterActive || item.IsOccluded)
                    && (string.IsNullOrWhiteSpace(tagFilter)
                        || item.MetadataTags.Any(tag =>
                            string.Equals(tag, tagFilter, StringComparison.OrdinalIgnoreCase)))
                    && (string.Equals(groupFilter, AllGroupsFilter, StringComparison.OrdinalIgnoreCase)
                        || (string.Equals(groupFilter, UngroupedFilter, StringComparison.OrdinalIgnoreCase)
                            && string.IsNullOrWhiteSpace(item.GroupId))
                        || string.Equals(groupFilter, item.GroupDisplayText, StringComparison.OrdinalIgnoreCase));
                matches[index] = matchesFilter;
                if (matchesFilter)
                {
                    visibleCount++;
                    if (firstMatchingIndex < 0)
                    {
                        firstMatchingIndex = index;
                    }
                }
            }

            string summaryText = !isOccludedFilterActive
                && string.IsNullOrWhiteSpace(tagFilter)
                && string.Equals(groupFilter, AllGroupsFilter, StringComparison.OrdinalIgnoreCase)
                ? $"전체 {enabledCount}개"
                : $"필터 {visibleCount}/{enabledCount}개";
            return new ObjectReviewMetadataFilterSnapshot(
                matches,
                enabledCount,
                visibleCount,
                summaryText,
                selectFirstMatch ? firstMatchingIndex : -1);
        }
    }

    public sealed class ObjectReviewMetadataTagCatalogSnapshot
    {
        public ObjectReviewMetadataTagCatalogSnapshot(
            IReadOnlyList<string> tags,
            IReadOnlyList<string> filters,
            string selectedTag,
            string selectedFilter)
        {
            Tags = tags ?? Array.Empty<string>();
            Filters = filters ?? Array.Empty<string>();
            SelectedTag = selectedTag ?? string.Empty;
            SelectedFilter = selectedFilter ?? ObjectReviewMetadataFilterPresentationService.AllMetadataTagsFilter;
        }

        public IReadOnlyList<string> Tags { get; }

        public IReadOnlyList<string> Filters { get; }

        public string SelectedTag { get; }

        public string SelectedFilter { get; }
    }

    public sealed class ObjectReviewGroupCatalogSnapshot
    {
        public ObjectReviewGroupCatalogSnapshot(
            IReadOnlyList<ObjectReviewGroupPresentation> groups,
            IReadOnlyList<string> filters,
            string selectedFilter)
        {
            Groups = groups ?? Array.Empty<ObjectReviewGroupPresentation>();
            Filters = filters ?? Array.Empty<string>();
            SelectedFilter = selectedFilter ?? ObjectReviewMetadataFilterPresentationService.AllGroupsFilter;
        }

        public IReadOnlyList<ObjectReviewGroupPresentation> Groups { get; }

        public IReadOnlyList<string> Filters { get; }

        public string SelectedFilter { get; }
    }

    public sealed class ObjectReviewGroupPresentation
    {
        public ObjectReviewGroupPresentation(string groupId, string displayText, int memberCount)
        {
            GroupId = groupId ?? string.Empty;
            DisplayText = displayText ?? string.Empty;
            MemberCount = Math.Max(0, memberCount);
        }

        public string GroupId { get; }

        public string DisplayText { get; }

        public int MemberCount { get; }
    }

    public sealed class ObjectReviewMetadataFilterSnapshot
    {
        public ObjectReviewMetadataFilterSnapshot(
            IReadOnlyList<bool> matches,
            int enabledCount,
            int visibleCount,
            string summaryText,
            int firstMatchingIndex)
        {
            Matches = matches ?? Array.Empty<bool>();
            EnabledCount = Math.Max(0, enabledCount);
            VisibleCount = Math.Max(0, visibleCount);
            SummaryText = summaryText ?? string.Empty;
            FirstMatchingIndex = firstMatchingIndex;
        }

        public IReadOnlyList<bool> Matches { get; }

        public int EnabledCount { get; }

        public int VisibleCount { get; }

        public string SummaryText { get; }

        public int FirstMatchingIndex { get; }
    }
}
