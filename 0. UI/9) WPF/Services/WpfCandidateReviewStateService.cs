using MvcVisionSystem._1._Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem
{
    public sealed class WpfCandidateReviewStateService
    {
        private readonly List<YoloWorkerSmokeCandidate> pendingCandidates = new List<YoloWorkerSmokeCandidate>();
        private readonly List<YoloWorkerSmokeCandidate> confirmedCandidates = new List<YoloWorkerSmokeCandidate>();

        public IReadOnlyList<YoloWorkerSmokeCandidate> PendingCandidates => pendingCandidates;

        public IReadOnlyList<YoloWorkerSmokeCandidate> ConfirmedCandidates => confirmedCandidates;

        public IList<YoloWorkerSmokeCandidate> MutablePendingCandidates => pendingCandidates;

        public IList<YoloWorkerSmokeCandidate> MutableConfirmedCandidates => confirmedCandidates;

        public int PendingCount => pendingCandidates.Count;

        public int ConfirmedCount => confirmedCandidates.Count;

        public bool HasPendingCandidates => pendingCandidates.Count > 0;

        public YoloWorkerSmokeCandidate GetPendingCandidateAt(int index)
            => index >= 0 && index < pendingCandidates.Count ? pendingCandidates[index] : null;

        public int IndexOfPendingCandidate(YoloWorkerSmokeCandidate candidate)
            => candidate == null ? -1 : pendingCandidates.IndexOf(candidate);

        public int LoadPendingCandidates(IEnumerable<YoloWorkerSmokeCandidate> candidates, bool clearConfirmed)
        {
            pendingCandidates.Clear();
            if (clearConfirmed)
            {
                confirmedCandidates.Clear();
            }

            foreach (YoloWorkerSmokeCandidate candidate in candidates ?? Array.Empty<YoloWorkerSmokeCandidate>())
            {
                if (candidate != null)
                {
                    pendingCandidates.Add(candidate);
                }
            }

            return pendingCandidates.Count;
        }

        public void ClearAll()
        {
            pendingCandidates.Clear();
            confirmedCandidates.Clear();
        }

        public int ClearPendingCandidates()
        {
            int removedCount = pendingCandidates.Count;
            pendingCandidates.Clear();
            return removedCount;
        }

        public bool SkipCandidate(YoloWorkerSmokeCandidate candidate)
        {
            return candidate != null && pendingCandidates.Remove(candidate);
        }

        public IReadOnlyList<YoloWorkerSmokeCandidate> GetVisibleCandidates(double minimumConfidence)
        {
            return pendingCandidates
                .Where(candidate => candidate != null && candidate.Confidence >= minimumConfidence)
                .ToList();
        }

        public WpfCandidateConfirmationPlan BuildConfirmationPlan(
            IEnumerable<YoloWorkerSmokeCandidate> requestedCandidates,
            Predicate<YoloWorkerSmokeCandidate> isConfirmable,
            Predicate<YoloWorkerSmokeCandidate> isDuplicate)
        {
            var requested = (requestedCandidates ?? Array.Empty<YoloWorkerSmokeCandidate>())
                .Where(candidate => candidate != null && pendingCandidates.Contains(candidate))
                .ToList();
            var confirmable = requested
                .Where(candidate => isConfirmable?.Invoke(candidate) == true)
                .ToList();

            // Duplicate counts stay in the state service so confirm UX does not reimplement pending-list rules in the shell.
            int duplicatePendingCount = requested.Count(candidate => isDuplicate?.Invoke(candidate) == true);
            int skippedDuplicateCount = requested.Count(candidate => isDuplicate?.Invoke(candidate) == true);
            return new WpfCandidateConfirmationPlan(confirmable, duplicatePendingCount, skippedDuplicateCount);
        }

        public void ApplyConfirmation(IEnumerable<YoloWorkerSmokeCandidate> confirmableCandidates)
        {
            foreach (YoloWorkerSmokeCandidate candidate in confirmableCandidates ?? Array.Empty<YoloWorkerSmokeCandidate>())
            {
                if (candidate == null)
                {
                    continue;
                }

                pendingCandidates.Remove(candidate);
                if (!confirmedCandidates.Contains(candidate))
                {
                    confirmedCandidates.Add(candidate);
                }
            }
        }
    }

    public sealed class WpfCandidateConfirmationPlan
    {
        public WpfCandidateConfirmationPlan(
            IReadOnlyList<YoloWorkerSmokeCandidate> confirmableCandidates,
            int duplicatePendingCount,
            int skippedDuplicateCount)
        {
            ConfirmableCandidates = confirmableCandidates ?? Array.Empty<YoloWorkerSmokeCandidate>();
            DuplicatePendingCount = duplicatePendingCount;
            SkippedDuplicateCount = skippedDuplicateCount;
        }

        public IReadOnlyList<YoloWorkerSmokeCandidate> ConfirmableCandidates { get; }

        public int DuplicatePendingCount { get; }

        public int SkippedDuplicateCount { get; }

        public bool HasConfirmableCandidates => ConfirmableCandidates.Count > 0;
    }
}