using MvcVisionSystem._1._Core;
using System;
using System.Collections.Generic;

namespace MvcVisionSystem
{
    public sealed class WpfCandidateConfirmationService
    {
        // Keep confirmation wording outside the shell so MVVM command rewiring does not duplicate review-history rules.
        public WpfCandidateConfirmationAttempt Prepare(
            WpfCandidateReviewStateService state,
            IEnumerable<YoloWorkerSmokeCandidate> candidates,
            Predicate<YoloWorkerSmokeCandidate> isConfirmable,
            Predicate<YoloWorkerSmokeCandidate> isDuplicate)
        {
            if (state == null)
            {
                return WpfCandidateConfirmationAttempt.Unavailable(
                    null,
                    "\uD655\uC815 \uAC00\uB2A5\uD55C AI \uD6C4\uBCF4 \uC5C6\uC74C",
                    "\uD655\uC815 \uAC00\uB2A5\uD55C AI \uD6C4\uBCF4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.");
            }

            WpfCandidateConfirmationPlan plan = state.BuildConfirmationPlan(candidates, isConfirmable, isDuplicate);
            if (plan.HasConfirmableCandidates)
            {
                return WpfCandidateConfirmationAttempt.Ready(plan);
            }

            return WpfCandidateConfirmationAttempt.Unavailable(
                plan,
                BuildUnavailableReviewHistory(plan),
                BuildUnavailableLogMessage(plan));
        }

        public void ApplyConfirmation(WpfCandidateReviewStateService state, WpfCandidateConfirmationPlan plan)
        {
            state?.ApplyConfirmation(plan?.ConfirmableCandidates);
        }

        public WpfCandidateConfirmationResult BuildConfirmedResult(
            string scope,
            WpfCandidateConfirmationPlan plan,
            bool saved,
            int savedCount,
            string labelPathSummary)
        {
            int confirmedCount = plan?.ConfirmableCandidates?.Count ?? 0;
            int skippedDuplicateCount = plan?.SkippedDuplicateCount ?? 0;
            string savedReviewText = saved
                ? $"\uC800\uC7A5 {savedCount}\uAC1C"
                : "\uC800\uC7A5 \uAC74\uB108\uB700";
            string duplicateReviewText = skippedDuplicateCount > 0
                ? $" / \uC911\uBCF5 \uC81C\uC678 {skippedDuplicateCount}\uAC1C"
                : string.Empty;
            string savedLogText = saved
                ? $" \uC800\uC7A5 \uAC1D\uCCB4 {savedCount}. {labelPathSummary ?? string.Empty}"
                : " \uC800\uC7A5 \uAC74\uB108\uB700.";

            return new WpfCandidateConfirmationResult(
                $"\uD655\uC815({scope}): {confirmedCount}\uAC1C / {savedReviewText}{duplicateReviewText}",
                $"AI \uD6C4\uBCF4 \uD655\uC815({scope}): {confirmedCount}\uAC1C;{savedLogText}",
                skippedDuplicateCount > 0 ? $"\uC911\uBCF5 \uAC00\uB2A5 \uD6C4\uBCF4 {skippedDuplicateCount}\uAC1C\uB294 \uC81C\uC678\uD588\uC2B5\uB2C8\uB2E4." : string.Empty,
                confirmedCount,
                skippedDuplicateCount,
                saved,
                savedCount);
        }

        private static string BuildUnavailableReviewHistory(WpfCandidateConfirmationPlan plan)
        {
            return (plan?.DuplicatePendingCount ?? 0) > 0
                ? $"\uC911\uBCF5 \uC81C\uC678: {plan.DuplicatePendingCount}\uAC1C / \uAE30\uC874 \uB77C\uBCA8 \uD655\uC778 \uD544\uC694"
                : "\uD655\uC815 \uAC00\uB2A5\uD55C AI \uD6C4\uBCF4 \uC5C6\uC74C";
        }

        private static string BuildUnavailableLogMessage(WpfCandidateConfirmationPlan plan)
        {
            return (plan?.DuplicatePendingCount ?? 0) > 0
                ? $"\uC911\uBCF5 \uAC00\uB2A5 \uD6C4\uBCF4 {plan.DuplicatePendingCount}\uAC1C\uB294 \uD655\uC815\uD558\uC9C0 \uC54A\uC558\uC2B5\uB2C8\uB2E4. \uD544\uC694\uD558\uBA74 \uAE30\uC874 \uB77C\uBCA8\uC744 \uD655\uC778\uD558\uAC70\uB098 \uD6C4\uBCF4\uB97C \uC2A4\uD0B5\uD558\uC138\uC694."
                : "\uD655\uC815 \uAC00\uB2A5\uD55C AI \uD6C4\uBCF4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.";
        }
    }

    public sealed class WpfCandidateConfirmationAttempt
    {
        private WpfCandidateConfirmationAttempt(
            WpfCandidateConfirmationPlan plan,
            bool canConfirm,
            string reviewHistoryMessage,
            string logMessage)
        {
            Plan = plan;
            CanConfirm = canConfirm;
            ReviewHistoryMessage = reviewHistoryMessage ?? string.Empty;
            LogMessage = logMessage ?? string.Empty;
        }

        public WpfCandidateConfirmationPlan Plan { get; }

        public bool CanConfirm { get; }

        public string ReviewHistoryMessage { get; }

        public string LogMessage { get; }

        public static WpfCandidateConfirmationAttempt Ready(WpfCandidateConfirmationPlan plan)
            => new WpfCandidateConfirmationAttempt(plan, true, string.Empty, string.Empty);

        public static WpfCandidateConfirmationAttempt Unavailable(
            WpfCandidateConfirmationPlan plan,
            string reviewHistoryMessage,
            string logMessage)
            => new WpfCandidateConfirmationAttempt(plan, false, reviewHistoryMessage, logMessage);
    }

    public sealed class WpfCandidateConfirmationResult
    {
        public WpfCandidateConfirmationResult(
            string reviewHistoryMessage,
            string logMessage,
            string duplicateLogMessage,
            int confirmedCount,
            int skippedDuplicateCount,
            bool saved,
            int savedCount)
        {
            ReviewHistoryMessage = reviewHistoryMessage ?? string.Empty;
            LogMessage = logMessage ?? string.Empty;
            DuplicateLogMessage = duplicateLogMessage ?? string.Empty;
            ConfirmedCount = confirmedCount;
            SkippedDuplicateCount = skippedDuplicateCount;
            Saved = saved;
            SavedCount = savedCount;
        }

        public string ReviewHistoryMessage { get; }

        public string LogMessage { get; }

        public string DuplicateLogMessage { get; }

        public int ConfirmedCount { get; }

        public int SkippedDuplicateCount { get; }

        public bool Saved { get; }

        public int SavedCount { get; }
    }
}
