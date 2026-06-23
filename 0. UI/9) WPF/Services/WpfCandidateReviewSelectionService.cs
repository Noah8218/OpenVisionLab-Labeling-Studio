using MvcVisionSystem._1._Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem
{
    public static class WpfCandidateReviewSelectionService
    {
        public static YoloWorkerSmokeCandidate GetSelectedCandidate(WpfCandidateReviewListItem selectedCandidate)
            => selectedCandidate?.Payload as YoloWorkerSmokeCandidate;

        public static WpfCandidateNavigationSelection SelectCandidateOffset(
            IEnumerable<WpfCandidateReviewListItem> rows,
            WpfCandidateReviewListItem selectedCandidate,
            int offset)
        {
            List<WpfCandidateReviewListItem> candidateRows = GetCandidateRows(rows);
            if (candidateRows.Count == 0)
            {
                return WpfCandidateNavigationSelection.NoCandidates();
            }

            if (candidateRows.Count == 1)
            {
                return WpfCandidateNavigationSelection.SingleCandidate(candidateRows[0]);
            }

            int selectedIndex = candidateRows.IndexOf(selectedCandidate);
            if (selectedIndex < 0)
            {
                selectedIndex = 0;
            }
            else
            {
                selectedIndex = (selectedIndex + offset + candidateRows.Count) % candidateRows.Count;
            }

            return WpfCandidateNavigationSelection.Selected(candidateRows[selectedIndex]);
        }

        public static YoloWorkerSmokeCandidate FindNextVisibleCandidateAfter(
            IEnumerable<YoloWorkerSmokeCandidate> visibleCandidates,
            YoloWorkerSmokeCandidate current,
            IEnumerable<YoloWorkerSmokeCandidate> removingCandidates)
        {
            List<YoloWorkerSmokeCandidate> visible = (visibleCandidates ?? Array.Empty<YoloWorkerSmokeCandidate>())
                .Where(candidate => candidate != null)
                .ToList();
            if (visible.Count == 0)
            {
                return null;
            }

            // Confirm/skip should keep the operator near the reviewed row instead of jumping to the first candidate.
            var removing = new HashSet<YoloWorkerSmokeCandidate>((removingCandidates ?? Array.Empty<YoloWorkerSmokeCandidate>())
                .Where(candidate => candidate != null));
            List<YoloWorkerSmokeCandidate> remaining = visible
                .Where(candidate => !removing.Contains(candidate))
                .ToList();
            if (remaining.Count == 0)
            {
                return null;
            }

            int currentIndex = visible.FindIndex(candidate => ReferenceEquals(candidate, current));
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            int nextIndex = Math.Min(currentIndex, remaining.Count - 1);
            return remaining[nextIndex];
        }

        private static List<WpfCandidateReviewListItem> GetCandidateRows(IEnumerable<WpfCandidateReviewListItem> rows)
        {
            return (rows ?? Array.Empty<WpfCandidateReviewListItem>())
                .Where(item => GetSelectedCandidate(item) != null)
                .ToList();
        }
    }

    public sealed class WpfCandidateNavigationSelection
    {
        private WpfCandidateNavigationSelection(
            WpfCandidateNavigationStatus status,
            WpfCandidateReviewListItem selectedItem)
        {
            Status = status;
            SelectedItem = selectedItem;
        }

        public WpfCandidateNavigationStatus Status { get; }

        public WpfCandidateReviewListItem SelectedItem { get; }

        public static WpfCandidateNavigationSelection Selected(WpfCandidateReviewListItem selectedItem)
            => new WpfCandidateNavigationSelection(WpfCandidateNavigationStatus.Selected, selectedItem);

        public static WpfCandidateNavigationSelection NoCandidates()
            => new WpfCandidateNavigationSelection(WpfCandidateNavigationStatus.NoCandidates, null);

        public static WpfCandidateNavigationSelection SingleCandidate(WpfCandidateReviewListItem selectedItem)
            => new WpfCandidateNavigationSelection(WpfCandidateNavigationStatus.SingleCandidate, selectedItem);
    }

    public enum WpfCandidateNavigationStatus
    {
        Selected,
        NoCandidates,
        SingleCandidate
    }
}