using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem
{
    public static class WpfImageQueueFilterService
    {
        public static bool ShouldShow(WpfImageQueueItem item, string searchText, WpfImageQueueFilter filter)
        {
            if (item == null)
            {
                return false;
            }

            string normalizedSearch = searchText?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(normalizedSearch)
                && item.FileName.IndexOf(normalizedSearch, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            return MatchesFilter(item, filter);
        }

        public static int CountByFilter(IEnumerable<WpfImageQueueItem> items, WpfImageQueueFilter filter)
        {
            return (items ?? Array.Empty<WpfImageQueueItem>())
                .Count(item => item != null && MatchesFilter(item, filter));
        }

        public static string BuildDatasetStatusText(
            IEnumerable<WpfImageQueueItem> items,
            int visibleCount,
            WpfImageQueueFilter filter,
            int loadedCount = -1,
            int totalToLoad = -1)
        {
            List<WpfImageQueueItem> queueItems = (items ?? Array.Empty<WpfImageQueueItem>())
                .Where(item => item != null)
                .ToList();
            int totalCount = queueItems.Count;
            int completedCount = queueItems.Count(IsCompletedQueueItem);
            string reviewCountText = WpfImageQueuePresenter.BuildReviewCountSummary(queueItems);
            string filterText = filter == WpfImageQueueFilter.All
                ? string.Empty
                : $" / 필터 {WpfImageQueueFilterOption.GetDisplayName(filter)}";
            string loadingText = loadedCount >= 0 && totalToLoad > 0 && loadedCount < totalToLoad
                ? $" / 로드 {loadedCount}/{totalToLoad}"
                : string.Empty;

            return $"데이터셋: {Math.Max(0, visibleCount)}/{totalCount} 이미지 / 완료 {completedCount}{reviewCountText}{filterText}{loadingText}";
        }

        public static string BuildDatasetStatusTextWithActiveImage(
            IEnumerable<WpfImageQueueItem> items,
            int visibleCount,
            WpfImageQueueFilter filter,
            int loadedCount,
            int totalToLoad,
            string activeImagePath)
        {
            string statusText = BuildDatasetStatusText(items, visibleCount, filter, loadedCount, totalToLoad);
            if (string.IsNullOrWhiteSpace(activeImagePath))
            {
                return statusText;
            }

            // Keep the active file visible even while queue detail loading updates
            // the summary; EXE smokes and operators both need that orientation.
            return $"{statusText} / \uD604\uC7AC {System.IO.Path.GetFileName(activeImagePath)}";
        }

        public static bool MatchesFilter(WpfImageQueueItem item, WpfImageQueueFilter filter)
        {
            if (item == null)
            {
                return false;
            }

            return filter switch
            {
                WpfImageQueueFilter.Unlabeled => !IsCompletedQueueItem(item),
                WpfImageQueueFilter.Requested => item.ReviewState == YoloImageReviewState.Requested,
                WpfImageQueueFilter.Candidate => item.ReviewState == YoloImageReviewState.Candidate,
                WpfImageQueueFilter.Confirmed => item.ReviewState == YoloImageReviewState.Confirmed,
                WpfImageQueueFilter.Skipped => item.ReviewState == YoloImageReviewState.Skipped,
                WpfImageQueueFilter.NoCandidate => item.ReviewState == YoloImageReviewState.NoCandidate,
                WpfImageQueueFilter.Failed => item.ReviewState == YoloImageReviewState.Failed,
                _ => true
            };
        }

        public static bool IsCompletedQueueItem(WpfImageQueueItem item)
        {
            if (item == null)
            {
                return false;
            }

            if (item.IsSaveRequired)
            {
                return false;
            }

            // A saved empty label file has no objects, but it is still a reviewed normal image.
            // Treat confirmed/skipped/no-candidate rows as complete so the work-needed filter means work remains.
            return item.IsLabeled
                || item.ReviewState == YoloImageReviewState.Confirmed
                || item.ReviewState == YoloImageReviewState.Skipped
                || item.ReviewState == YoloImageReviewState.NoCandidate;
        }
    }
}
