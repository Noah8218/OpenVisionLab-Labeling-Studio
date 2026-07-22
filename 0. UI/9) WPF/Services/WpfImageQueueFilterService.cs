using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem
{
    public sealed class WpfImageQueueSummary
    {
        public int TotalCount { get; internal set; }
        public int CompletedCount { get; internal set; }
        public int WorklistCount { get; internal set; }
        public int SaveRequiredCount { get; internal set; }
        public int NeedsFixCount { get; internal set; }
        public int RequestedCount { get; internal set; }
        public int CandidateCount { get; internal set; }
        public int ConfirmedCount { get; internal set; }
        public int SkippedCount { get; internal set; }
        public int NoCandidateCount { get; internal set; }
        public int FailedCount { get; internal set; }
    }

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

        public static int CountByFilter(WpfImageQueueSummary summary, WpfImageQueueFilter filter)
        {
            summary ??= new WpfImageQueueSummary();
            return filter switch
            {
                WpfImageQueueFilter.Unlabeled => summary.WorklistCount,
                WpfImageQueueFilter.NeedsFix => summary.NeedsFixCount,
                WpfImageQueueFilter.Requested => summary.RequestedCount,
                WpfImageQueueFilter.Candidate => summary.CandidateCount,
                WpfImageQueueFilter.Confirmed => summary.ConfirmedCount,
                WpfImageQueueFilter.Skipped => summary.SkippedCount,
                WpfImageQueueFilter.NoCandidate => summary.NoCandidateCount,
                WpfImageQueueFilter.Failed => summary.FailedCount,
                _ => summary.TotalCount
            };
        }

        public static WpfImageQueueSummary Summarize(IEnumerable<WpfImageQueueItem> items)
        {
            var summary = new WpfImageQueueSummary();
            foreach (WpfImageQueueItem item in items ?? Array.Empty<WpfImageQueueItem>())
            {
                if (item == null)
                {
                    continue;
                }

                summary.TotalCount++;
                if (IsCompletedQueueItem(item))
                {
                    summary.CompletedCount++;
                }
                else
                {
                    summary.WorklistCount++;
                }

                if (item.IsSaveRequired)
                {
                    summary.SaveRequiredCount++;
                }

                if (item.QualityReviewState == YoloImageQualityReviewState.NeedsFix)
                {
                    summary.NeedsFixCount++;
                }

                switch (item.ReviewState)
                {
                    case YoloImageReviewState.Requested:
                        summary.RequestedCount++;
                        break;
                    case YoloImageReviewState.Candidate:
                        summary.CandidateCount++;
                        break;
                    case YoloImageReviewState.Confirmed:
                        summary.ConfirmedCount++;
                        break;
                    case YoloImageReviewState.Skipped:
                        summary.SkippedCount++;
                        break;
                    case YoloImageReviewState.NoCandidate:
                        summary.NoCandidateCount++;
                        break;
                    case YoloImageReviewState.Failed:
                        summary.FailedCount++;
                        break;
                }
            }

            return summary;
        }

        public static WpfImageQueueItem FindSingleItem(IEnumerable<WpfImageQueueItem> items)
        {
            List<WpfImageQueueItem> matches = (items ?? Array.Empty<WpfImageQueueItem>())
                .Where(item => item != null)
                .Take(2)
                .ToList();
            return matches.Count == 1 ? matches[0] : null;
        }

        public static WpfImageQueueItem FindSingleSearchMatch(
            IEnumerable<WpfImageQueueItem> items,
            string searchText,
            WpfImageQueueFilter filter)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return null;
            }

            return FindSingleItem((items ?? Array.Empty<WpfImageQueueItem>())
                .Where(item => ShouldShow(item, searchText, filter)));
        }

        public static int CountSearchMatches(
            IEnumerable<WpfImageQueueItem> items,
            string searchText,
            WpfImageQueueFilter filter,
            int limit)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return 0;
            }

            return (items ?? Array.Empty<WpfImageQueueItem>())
                .Where(item => ShouldShow(item, searchText, filter))
                .Take(Math.Max(1, limit))
                .Count();
        }

        public static string BuildDatasetStatusText(
            IEnumerable<WpfImageQueueItem> items,
            int visibleCount,
            WpfImageQueueFilter filter,
            int loadedCount = -1,
            int totalToLoad = -1)
        {
            return BuildDatasetStatusText(Summarize(items), visibleCount, filter, loadedCount, totalToLoad);
        }

        public static string BuildDatasetStatusText(
            WpfImageQueueSummary summary,
            int visibleCount,
            WpfImageQueueFilter filter,
            int loadedCount = -1,
            int totalToLoad = -1)
        {
            summary ??= new WpfImageQueueSummary();
            string reviewCountText = WpfImageQueuePresenter.BuildReviewCountSummary(summary);
            string filterText = filter == WpfImageQueueFilter.All
                ? string.Empty
                : $" / 필터 {WpfImageQueueFilterOption.GetDisplayName(filter)}";
            string loadingText = loadedCount >= 0 && totalToLoad > 0 && loadedCount < totalToLoad
                ? $" / 로드 {loadedCount}/{totalToLoad}"
                : string.Empty;

            return $"데이터셋: {Math.Max(0, visibleCount)}/{summary.TotalCount} 이미지 / 완료 {summary.CompletedCount}{reviewCountText}{filterText}{loadingText}";
        }

        public static string BuildDatasetStatusTextWithActiveImage(
            IEnumerable<WpfImageQueueItem> items,
            int visibleCount,
            WpfImageQueueFilter filter,
            int loadedCount,
            int totalToLoad,
            string activeImagePath)
        {
            return BuildDatasetStatusTextWithActiveImage(
                Summarize(items),
                visibleCount,
                filter,
                loadedCount,
                totalToLoad,
                activeImagePath);
        }

        public static string BuildDatasetStatusTextWithActiveImage(
            WpfImageQueueSummary summary,
            int visibleCount,
            WpfImageQueueFilter filter,
            int loadedCount,
            int totalToLoad,
            string activeImagePath)
        {
            string statusText = BuildDatasetStatusText(summary, visibleCount, filter, loadedCount, totalToLoad);
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
                WpfImageQueueFilter.NeedsFix => item.QualityReviewState == YoloImageQualityReviewState.NeedsFix,
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

            if (item.QualityReviewState == YoloImageQualityReviewState.NeedsFix)
            {
                return false;
            }

            return HasCompletedLabelWork(item);
        }

        public static bool HasCompletedLabelWork(WpfImageQueueItem item)
        {
            if (item == null || item.IsSaveRequired)
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
