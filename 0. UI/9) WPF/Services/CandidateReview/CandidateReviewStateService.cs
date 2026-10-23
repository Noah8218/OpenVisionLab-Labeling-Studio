using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem
{
    public class CandidateReviewStateService
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

        public InferenceResultReviewEntry LabelingTransferEntry { get; private set; }

        public YoloWorkerSmokeCandidate GetPendingCandidateAt(int index)
            => index >= 0 && index < pendingCandidates.Count ? pendingCandidates[index] : null;

        public int IndexOfPendingCandidate(YoloWorkerSmokeCandidate candidate)
            => candidate == null ? -1 : pendingCandidates.IndexOf(candidate);

        public int LoadPendingCandidates(IEnumerable<YoloWorkerSmokeCandidate> candidates, bool clearConfirmed)
        {
            LabelingTransferEntry = null;
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

        public static IReadOnlyList<YoloWorkerSmokeCandidate> BuildCandidatePresentation(YoloWorkerSmokeTestResult result, IReadOnlyList<LabelClass> supportedClasses = null, InferenceResultSource sourceOverride = null)
        {
            InferenceResultSource source = sourceOverride ?? result?.Source;
            if (result?.Succeeded != true || source == null || !float.IsFinite(source.Confidence) || source.Confidence < 0 || source.Confidence > 1 || source.MaximumCandidates < 1)
                return Array.Empty<YoloWorkerSmokeCandidate>();
            return (result.Candidates ?? Array.Empty<YoloWorkerSmokeCandidate>()).Where(candidate => candidate != null && !candidate.ImageLevel
                && double.IsFinite(candidate.Confidence) && candidate.Confidence >= source.Confidence && candidate.Confidence <= 1
                && (supportedClasses == null || supportedClasses.Any(label => ClassCatalogService.IsActiveClass(label)
                    && string.Equals(label.Text, candidate.ClassName?.Trim(), StringComparison.OrdinalIgnoreCase))))
                .Take(source.MaximumCandidates).ToArray();
        }

        public bool AttachInferenceResultForLabeling(InferenceResultReviewEntry entry)
        {
            if (entry?.Result?.Succeeded != true || string.IsNullOrWhiteSpace(entry.Result.Source?.ImagePath)
                || entry.Result.Candidates?.Any(candidate => candidate?.ImageLevel == true) == true) return false;
            LabelingTransferEntry = entry;
            return true;
        }

        public bool LoadInferenceResultForLabeling(InferenceResultReviewEntry entry, double displayConfidence, IReadOnlyList<LabelClass> supportedClasses = null)
        {
            YoloWorkerSmokeTestResult result = entry?.Result;
            if (result?.Succeeded != true || string.IsNullOrWhiteSpace(result.Source?.ImagePath)
                || !double.IsFinite(displayConfidence) || displayConfidence < 0 || displayConfidence > 1
                || result.Candidates?.Any(candidate => candidate?.ImageLevel == true) == true) return false;
            var candidates = BuildCandidatePresentation(result, supportedClasses)
                .Select(AnnotationHistoryService.CloneCandidate).ToArray();
            LoadPendingCandidates(candidates, clearConfirmed: false);
            LabelingTransferEntry = entry;
            return true;
        }

        public bool RestoreCandidateDraft(InferenceResultReviewEntry entry, bool restoreConfirmed = true)
        {
            if (entry?.Result?.Succeeded != true || entry.PendingDraft == null || entry.Result.Candidates?.Any(candidate => candidate?.ImageLevel == true) == true) return false;
            LoadPendingCandidates(entry.PendingDraft.Select(AnnotationHistoryService.CloneCandidate), clearConfirmed: true);
            foreach (YoloWorkerSmokeCandidate candidate in restoreConfirmed ? entry.ConfirmedDraft ?? Array.Empty<YoloWorkerSmokeCandidate>() : Array.Empty<YoloWorkerSmokeCandidate>())
                if (candidate != null) confirmedCandidates.Add(AnnotationHistoryService.CloneCandidate(candidate));
            LabelingTransferEntry = entry;
            return true;
        }

        public void ClearAll()
        {
            LabelingTransferEntry = null;
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

        public void RevertConfirmation(
            IEnumerable<YoloWorkerSmokeCandidate> confirmableCandidates,
            IReadOnlyList<YoloWorkerSmokeCandidate> pendingOrder)
        {
            List<YoloWorkerSmokeCandidate> targets = (confirmableCandidates ?? Array.Empty<YoloWorkerSmokeCandidate>())
                .Where(candidate => candidate != null)
                .ToList();
            if (targets.Count == 0)
            {
                return;
            }

            foreach (YoloWorkerSmokeCandidate candidate in targets)
            {
                confirmedCandidates.Remove(candidate);
            }

            List<YoloWorkerSmokeCandidate> currentPending = pendingCandidates.ToList();
            var restoredPending = new List<YoloWorkerSmokeCandidate>();
            foreach (YoloWorkerSmokeCandidate candidate in pendingOrder ?? Array.Empty<YoloWorkerSmokeCandidate>())
            {
                if (candidate != null && !restoredPending.Contains(candidate))
                {
                    restoredPending.Add(candidate);
                }
            }

            foreach (YoloWorkerSmokeCandidate candidate in currentPending)
            {
                if (candidate != null && !restoredPending.Contains(candidate))
                {
                    restoredPending.Add(candidate);
                }
            }

            pendingCandidates.Clear();
            pendingCandidates.AddRange(restoredPending);
        }
    }

    [Obsolete("Use CandidateReviewStateService.", false)]
    public sealed class WpfCandidateReviewStateService : CandidateReviewStateService
    {
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
