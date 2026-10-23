using MvcVisionSystem.Yolo;
using OpenVisionLab;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem
{
    public class ImageQueueSummary
    {
        public int TotalCount { get; internal set; }
        public int CompletedCount { get; internal set; }
        public int WorklistCount { get; internal set; }
        public int SaveRequiredCount { get; internal set; }
        public int NeedsFixCount { get; internal set; }
        public int ReviewPendingCount { get; internal set; }
        public int ReviewedCount { get; internal set; }
        public int RequestedCount { get; internal set; }
        public int CandidateCount { get; internal set; }
        public int ConfirmedCount { get; internal set; }
        public int SkippedCount { get; internal set; }
        public int NoCandidateCount { get; internal set; }
        public int FailedCount { get; internal set; }
        public int LabelSuspicionCount { get; internal set; }
    }

    [Obsolete("Use ImageQueueSummary.", false)]
    public sealed class WpfImageQueueSummary : ImageQueueSummary
    {
        internal static WpfImageQueueSummary FromCanonical(ImageQueueSummary source)
        {
            if (source == null)
            {
                return null;
            }

            return new WpfImageQueueSummary
            {
                TotalCount = source.TotalCount,
                CompletedCount = source.CompletedCount,
                WorklistCount = source.WorklistCount,
                SaveRequiredCount = source.SaveRequiredCount,
                NeedsFixCount = source.NeedsFixCount,
                ReviewPendingCount = source.ReviewPendingCount,
                ReviewedCount = source.ReviewedCount,
                RequestedCount = source.RequestedCount,
                CandidateCount = source.CandidateCount,
                ConfirmedCount = source.ConfirmedCount,
                SkippedCount = source.SkippedCount,
                NoCandidateCount = source.NoCandidateCount,
                FailedCount = source.FailedCount,
                LabelSuspicionCount = source.LabelSuspicionCount
            };
        }
    }

    public static class ImageQueueFilterService
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

        public static int CountByFilter(ImageQueueSummary summary, WpfImageQueueFilter filter)
        {
            summary ??= new ImageQueueSummary();
            return filter switch
            {
                WpfImageQueueFilter.Unlabeled => summary.WorklistCount,
                WpfImageQueueFilter.NeedsFix => summary.NeedsFixCount,
                WpfImageQueueFilter.ReviewPending => summary.ReviewPendingCount,
                WpfImageQueueFilter.Reviewed => summary.ReviewedCount,
                WpfImageQueueFilter.Requested => summary.RequestedCount,
                WpfImageQueueFilter.Candidate => summary.CandidateCount,
                WpfImageQueueFilter.LabelSuspicion => summary.LabelSuspicionCount,
                WpfImageQueueFilter.Confirmed => summary.ConfirmedCount,
                WpfImageQueueFilter.Skipped => summary.SkippedCount,
                WpfImageQueueFilter.NoCandidate => summary.NoCandidateCount,
                WpfImageQueueFilter.Failed => summary.FailedCount,
                _ => summary.TotalCount
            };
        }

        public static ImageQueueSummary Summarize(IEnumerable<WpfImageQueueItem> items)
        {
            var summary = new ImageQueueSummary();
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

                if (item.LabelSuspicionCount > 0) summary.LabelSuspicionCount++;
                if (item.SessionCandidateCount > 0 && item.ReviewState != YoloImageReviewState.Candidate) summary.CandidateCount++;

                if (IsReviewPendingQueueItem(item)) summary.ReviewPendingCount++;
                if (IsReviewedQueueItem(item)) summary.ReviewedCount++;
                if (HasSavedGroundTruth(item) && !item.HasSavedNoObjectLabel) summary.ConfirmedCount++;
                if (item.HasSavedNoObjectLabel) summary.NoCandidateCount++;

                switch (item.ReviewState)
                {
                    case YoloImageReviewState.Requested:
                        summary.RequestedCount++;
                        break;
                    case YoloImageReviewState.Candidate:
                        summary.CandidateCount++;
                        break;
                    case YoloImageReviewState.Skipped:
                        summary.SkippedCount++;
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
            ImageQueueSummary summary,
            int visibleCount,
            WpfImageQueueFilter filter,
            int loadedCount = -1,
            int totalToLoad = -1)
        {
            summary ??= new ImageQueueSummary();
            string reviewCountText = WpfImageQueuePresenter.BuildReviewCountSummary(summary);
            string filterText = filter == WpfImageQueueFilter.All
                ? string.Empty
                : Format("WpfShell.Status.DatasetFilter", WpfImageQueueFilterOption.GetDisplayName(filter));
            string loadingText = loadedCount >= 0 && totalToLoad > 0 && loadedCount < totalToLoad
                ? Format("WpfShell.Status.DatasetLoading", loadedCount, totalToLoad)
                : string.Empty;

            return Format(
                "WpfShell.Status.DatasetSummary",
                Math.Max(0, visibleCount),
                summary.TotalCount,
                summary.CompletedCount,
                reviewCountText,
                filterText,
                loadingText);
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
            ImageQueueSummary summary,
            int visibleCount,
            WpfImageQueueFilter filter,
            int loadedCount,
            int totalToLoad,
            string activeImagePath)
            => BuildDatasetStatusTextWithActiveImage(summary, visibleCount, filter, loadedCount, totalToLoad, activeImagePath, string.Empty, false);

        public static string BuildDatasetStatusTextWithActiveImage(
            ImageQueueSummary summary,
            int visibleCount,
            WpfImageQueueFilter filter,
            int loadedCount,
            int totalToLoad,
            string activeImagePath,
            string searchText,
            bool hasUnsavedLabels)
        {
            string statusText = BuildDatasetStatusText(summary, visibleCount, filter, loadedCount, totalToLoad);
            if (!string.IsNullOrWhiteSpace(searchText) && visibleCount == 0)
            {
                statusText = Format(hasUnsavedLabels
                    ? "WpfShell.Status.DatasetSearchNoMatch.Unsaved"
                    : "WpfShell.Status.DatasetSearchNoMatch", statusText);
            }

            if (string.IsNullOrWhiteSpace(activeImagePath))
            {
                return statusText;
            }

            // Keep the active file visible even while queue detail loading updates
            // the summary; EXE smokes and operators both need that orientation.
            return Format(
                "WpfShell.Status.DatasetActiveImage",
                statusText,
                System.IO.Path.GetFileName(activeImagePath));
        }

        private static string Format(string key, params object[] arguments)
        {
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                OpenVisionLanguageService.T(key),
                arguments ?? Array.Empty<object>());
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
                WpfImageQueueFilter.ReviewPending => IsReviewPendingQueueItem(item),
                WpfImageQueueFilter.Reviewed => IsReviewedQueueItem(item),
                WpfImageQueueFilter.Requested => item.ReviewState == YoloImageReviewState.Requested,
                WpfImageQueueFilter.Candidate => item.ReviewState == YoloImageReviewState.Candidate || item.SessionCandidateCount > 0,
                WpfImageQueueFilter.LabelSuspicion => item.LabelSuspicionCount > 0,
                WpfImageQueueFilter.Confirmed => HasSavedGroundTruth(item) && !item.HasSavedNoObjectLabel,
                WpfImageQueueFilter.Skipped => item.ReviewState == YoloImageReviewState.Skipped,
                WpfImageQueueFilter.NoCandidate => item.HasSavedNoObjectLabel,
                WpfImageQueueFilter.Failed => item.ReviewState == YoloImageReviewState.Failed,
                _ => true
            };
        }

        public static bool HasSavedGroundTruth(WpfImageQueueItem item)
            => item != null && (item.AnomalyReviewState == AnomalyImageReviewState.Normal || item.AnomalyReviewState == AnomalyImageReviewState.Abnormal || !item.IsAnomalyReviewItem && item.HasSavedLabelFile);

        public static bool IsReviewPendingQueueItem(WpfImageQueueItem item)
            => HasSavedGroundTruth(item) && !item.IsSaveRequired && item.QualityReviewState == YoloImageQualityReviewState.Unreviewed;

        public static bool IsCompletedQueueItem(WpfImageQueueItem item)
        {
            if (item == null)
            {
                return false;
            }

            if (item.QualityReviewState == YoloImageQualityReviewState.NeedsFix)
            {
                // A rejected image has a recorded outcome and remains in its own rework filter.
                // This progress state does not create saved GT or make it eligible for approval.
                return true;
            }

            return HasCompletedLabelWork(item);
        }

        public static bool IsReviewedQueueItem(WpfImageQueueItem item)
            => HasSavedGroundTruth(item) && !item.IsSaveRequired && item.QualityReviewState == YoloImageQualityReviewState.Reviewed;

        public static bool HasCompletedLabelWork(WpfImageQueueItem item)
        {
            if (item == null || item.IsSaveRequired)
            {
                return false;
            }

            // AI zero candidates is not an empty ground-truth label. Keep explicit
            // confirmation/skip compatibility while deriving saved work from its own state.
            return item.IsLabeled
                || HasSavedGroundTruth(item)
                || item.ReviewState == YoloImageReviewState.Confirmed
                || item.ReviewState == YoloImageReviewState.Skipped;
        }
    }

    [Obsolete("Use ImageQueueFilterService.", false)]
    public static class WpfImageQueueFilterService
    {
        public static bool ShouldShow(WpfImageQueueItem item, string searchText, WpfImageQueueFilter filter)
            => ImageQueueFilterService.ShouldShow(item, searchText, filter);

        public static int CountByFilter(IEnumerable<WpfImageQueueItem> items, WpfImageQueueFilter filter)
            => ImageQueueFilterService.CountByFilter(items, filter);

        public static int CountByFilter(WpfImageQueueSummary summary, WpfImageQueueFilter filter)
            => ImageQueueFilterService.CountByFilter(summary, filter);

        public static WpfImageQueueSummary Summarize(IEnumerable<WpfImageQueueItem> items)
            => WpfImageQueueSummary.FromCanonical(ImageQueueFilterService.Summarize(items));

        public static WpfImageQueueItem FindSingleItem(IEnumerable<WpfImageQueueItem> items)
            => ImageQueueFilterService.FindSingleItem(items);

        public static WpfImageQueueItem FindSingleSearchMatch(IEnumerable<WpfImageQueueItem> items, string searchText, WpfImageQueueFilter filter)
            => ImageQueueFilterService.FindSingleSearchMatch(items, searchText, filter);

        public static int CountSearchMatches(IEnumerable<WpfImageQueueItem> items, string searchText, WpfImageQueueFilter filter, int limit)
            => ImageQueueFilterService.CountSearchMatches(items, searchText, filter, limit);

        public static string BuildDatasetStatusText(IEnumerable<WpfImageQueueItem> items, int visibleCount, WpfImageQueueFilter filter, int loadedCount = -1, int totalToLoad = -1)
            => ImageQueueFilterService.BuildDatasetStatusText(items, visibleCount, filter, loadedCount, totalToLoad);

        public static string BuildDatasetStatusText(WpfImageQueueSummary summary, int visibleCount, WpfImageQueueFilter filter, int loadedCount = -1, int totalToLoad = -1)
            => ImageQueueFilterService.BuildDatasetStatusText(summary, visibleCount, filter, loadedCount, totalToLoad);

        public static string BuildDatasetStatusTextWithActiveImage(IEnumerable<WpfImageQueueItem> items, int visibleCount, WpfImageQueueFilter filter, int loadedCount, int totalToLoad, string activeImagePath)
            => ImageQueueFilterService.BuildDatasetStatusTextWithActiveImage(items, visibleCount, filter, loadedCount, totalToLoad, activeImagePath);

        public static string BuildDatasetStatusTextWithActiveImage(WpfImageQueueSummary summary, int visibleCount, WpfImageQueueFilter filter, int loadedCount, int totalToLoad, string activeImagePath)
            => ImageQueueFilterService.BuildDatasetStatusTextWithActiveImage(summary, visibleCount, filter, loadedCount, totalToLoad, activeImagePath);

        public static bool MatchesFilter(WpfImageQueueItem item, WpfImageQueueFilter filter)
            => ImageQueueFilterService.MatchesFilter(item, filter);

        public static bool IsCompletedQueueItem(WpfImageQueueItem item)
            => ImageQueueFilterService.IsCompletedQueueItem(item);

        public static bool HasCompletedLabelWork(WpfImageQueueItem item)
            => ImageQueueFilterService.HasCompletedLabelWork(item);
    }
}
