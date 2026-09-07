using System;
using System.IO;

namespace MvcVisionSystem
{
    /// <summary>
    /// Read-only inputs used to choose the candidate/adoption decision panel state.
    /// This snapshot deliberately carries no registry or recipe mutation behavior.
    /// </summary>
    public class ModelCandidateDecisionSnapshot
    {
        public bool HasPendingRecipeSave { get; set; }

        public bool IsPromotionHeld { get; set; }

        public string CandidateWeightsPath { get; set; } = string.Empty;

        public string BaselineWeightsPath { get; set; } = string.Empty;

        public bool CandidateWeightsFileExists { get; set; }

        public bool BaselineWeightsFileExists { get; set; }

        public bool HasLatestCandidate { get; set; }

        public string LatestCandidateWeightsPath { get; set; } = string.Empty;

        public string LatestCandidateDecision { get; set; } = string.Empty;

        public string LatestCandidateDecisionSummary { get; set; } = string.Empty;

        public bool LatestCandidateSavedToRecipe { get; set; }

        public bool HasLatestWeights { get; set; }
    }

    public class ModelCandidateDecisionPresentation
    {
        public bool CanSave { get; set; }

        public bool CanReject { get; set; }

        public string StatusText { get; set; } = string.Empty;

        public string DetailText { get; set; } = string.Empty;

        public string SaveToolTip { get; set; } = string.Empty;

        public string RejectToolTip { get; set; } = string.Empty;
    }

    public static class ModelCandidateDecisionPresentationService
    {
        public static ModelCandidateDecisionPresentation Build(ModelCandidateDecisionSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return BuildNoCandidate();
            }

            string candidateWeightsPath = snapshot.CandidateWeightsPath?.Trim() ?? string.Empty;
            string baselineWeightsPath = snapshot.BaselineWeightsPath?.Trim() ?? string.Empty;
            bool hasPendingCandidate = snapshot.HasPendingRecipeSave
                && !string.IsNullOrWhiteSpace(candidateWeightsPath)
                && snapshot.CandidateWeightsFileExists;
            bool canReject = hasPendingCandidate
                && !string.IsNullOrWhiteSpace(baselineWeightsPath)
                && snapshot.BaselineWeightsFileExists;

            if (hasPendingCandidate)
            {
                return snapshot.IsPromotionHeld
                    ? BuildHeldCandidate(candidateWeightsPath, canReject)
                    : BuildPendingCandidate(candidateWeightsPath, baselineWeightsPath, canReject);
            }

            if (snapshot.HasLatestCandidate)
            {
                string decision = snapshot.LatestCandidateDecision ?? string.Empty;
                if (string.Equals(decision, ModelRegistryService.CandidateDecisionRejected, StringComparison.Ordinal))
                {
                    return BuildRejectedCandidate(
                        snapshot.LatestCandidateWeightsPath,
                        snapshot.LatestCandidateDecisionSummary);
                }

                if (string.Equals(decision, ModelRegistryService.CandidateDecisionAdopted, StringComparison.Ordinal)
                    || snapshot.LatestCandidateSavedToRecipe)
                {
                    return BuildSavedCandidate(snapshot.LatestCandidateWeightsPath);
                }
            }

            return snapshot.HasLatestWeights
                ? BuildReviewAvailable()
                : BuildNoCandidate();
        }

        public static ModelCandidateDecisionPresentation BuildPendingCandidate(
            string candidateWeightsPath,
            string baselineWeightsPath,
            bool canReject)
        {
            string candidateName = FormatModelName(candidateWeightsPath, "\uD6C4\uBCF4 \uBAA8\uB378");
            string baselineName = string.IsNullOrWhiteSpace(baselineWeightsPath)
                ? "\uAE30\uC874 \uBAA8\uB378 \uD655\uC778 \uD544\uC694"
                : FormatModelName(baselineWeightsPath, "\uAE30\uC874 \uBAA8\uB378");

            return new ModelCandidateDecisionPresentation
            {
                CanSave = true,
                CanReject = canReject,
                StatusText = $"\uD6C4\uBCF4 \uACB0\uC815: \uC800\uC7A5 \uB610\uB294 \uAC70\uC808 \uD544\uC694 ({candidateName})",
                DetailText = $"\uC800\uC7A5\uD558\uBA74 \uB2E4\uC74C \uCD94\uB860\uBD80\uD130 \uC774 \uD6C4\uBCF4\uB97C \uC0AC\uC6A9\uD569\uB2C8\uB2E4. \uAC70\uC808\uD558\uBA74 \uAE30\uC874 \uAC80\uC0AC \uBAA8\uB378 {baselineName}\uC744 \uC720\uC9C0\uD569\uB2C8\uB2E4.",
                SaveToolTip = "\uD559\uC2B5 \uACB0\uACFC\uB97C recipe\uC758 \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378\uB85C \uC800\uC7A5\uD569\uB2C8\uB2E4.",
                RejectToolTip = canReject
                    ? "\uD559\uC2B5 \uACB0\uACFC\uB97C \uC4F0\uC9C0 \uC54A\uACE0 \uAE30\uC874 \uAC80\uC0AC \uBAA8\uB378\uC744 \uC720\uC9C0\uD569\uB2C8\uB2E4."
                    : "\uB418\uB3CC\uB9B4 \uAE30\uC874 \uAC80\uC0AC \uBAA8\uB378 \uACBD\uB85C\uAC00 \uC5C6\uC5B4 \uAC70\uC808\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4."
            };
        }

        public static ModelCandidateDecisionPresentation BuildHeldCandidate(
            string candidateWeightsPath,
            bool canReject)
        {
            string candidateName = FormatModelName(candidateWeightsPath, "\uD6C4\uBCF4 \uBAA8\uB378");
            return new ModelCandidateDecisionPresentation
            {
                CanSave = false,
                CanReject = canReject,
                StatusText = $"\uD6C4\uBCF4 \uACB0\uC815: \uAC80\uC99D \uBCF4\uB958 ({candidateName})",
                DetailText = "\uCD5C\uC885 \uAC80\uC99D \uACB0\uACFC\uAC00 \uAD50\uCCB4 \uBCF4\uB958\uC785\uB2C8\uB2E4. \uD559\uC2B5 \uB370\uC774\uD130\uB97C \uBCF4\uAC15\uD558\uAC70\uB098 \uBAA8\uB378\uC744 \uC870\uC815\uD55C \uB4A4 \uD6C4\uBCF4 \uAC80\uC99D\uC744 \uB2E4\uC2DC \uC2E4\uD589\uD558\uC138\uC694. \uAC70\uC808\uD558\uBA74 \uAE30\uC874 \uAC80\uC0AC \uBAA8\uB378\uC744 \uC720\uC9C0\uD569\uB2C8\uB2E4.",
                SaveToolTip = "\uAD50\uCCB4 \uBCF4\uB958 \uD6C4\uBCF4\uB294 \uAC80\uC0AC \uBAA8\uB378\uB85C \uC800\uC7A5\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4. \uB370\uC774\uD130\uB97C \uBCF4\uAC15\uD558\uAC70\uB098 \uBAA8\uB378\uC744 \uC870\uC815\uD55C \uB4A4 \uD6C4\uBCF4 \uAC80\uC99D\uC744 \uB2E4\uC2DC \uC2E4\uD589\uD558\uC138\uC694.",
                RejectToolTip = canReject
                    ? "\uD559\uC2B5 \uACB0\uACFC\uB97C \uC4F0\uC9C0 \uC54A\uACE0 \uAE30\uC874 \uAC80\uC0AC \uBAA8\uB378\uC744 \uC720\uC9C0\uD569\uB2C8\uB2E4."
                    : "\uB418\uB3CC\uB9B4 \uAE30\uC874 \uAC80\uC0AC \uBAA8\uB378 \uACBD\uB85C\uAC00 \uC5C6\uC5B4 \uAC70\uC808\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4."
            };
        }

        public static ModelCandidateDecisionPresentation BuildRejectedCandidate(
            string candidateWeightsPath,
            string decisionSummary)
        {
            return new ModelCandidateDecisionPresentation
            {
                CanSave = false,
                CanReject = false,
                StatusText = $"\uD6C4\uBCF4 \uACB0\uC815: \uAC70\uC808\uB428 ({FormatModelName(candidateWeightsPath, "\uD6C4\uBCF4 \uBAA8\uB378")})",
                DetailText = string.IsNullOrWhiteSpace(decisionSummary)
                    ? "\uC774 \uD6C4\uBCF4\uB294 \uD604\uC7AC recipe\uC758 \uAC80\uC0AC \uBAA8\uB378\uB85C \uCC44\uD0DD\uD558\uC9C0 \uC54A\uC558\uC2B5\uB2C8\uB2E4."
                    : decisionSummary.Trim(),
                SaveToolTip = "\uC774\uBBF8 \uAC70\uC808\uB41C \uD6C4\uBCF4\uC785\uB2C8\uB2E4. \uB2E4\uC2DC \uC4F0\uB824\uBA74 \uBAA8\uB378 \uC124\uC815\uC5D0\uC11C \uC9C1\uC811 \uC120\uD0DD\uD558\uC138\uC694.",
                RejectToolTip = "\uC774\uBBF8 \uAC70\uC808\uB41C \uD6C4\uBCF4\uC785\uB2C8\uB2E4."
            };
        }

        public static ModelCandidateDecisionPresentation BuildSavedCandidate(string candidateWeightsPath)
        {
            return new ModelCandidateDecisionPresentation
            {
                CanSave = false,
                CanReject = false,
                StatusText = $"\uD6C4\uBCF4 \uACB0\uC815: \uAC80\uC0AC \uBAA8\uB378\uB85C \uC800\uC7A5\uB428 ({FormatModelName(candidateWeightsPath, "\uD6C4\uBCF4 \uBAA8\uB378")})",
                DetailText = "\uC774 \uD6C4\uBCF4\uB294 recipe\uC758 \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378 \uC774\uB825\uC5D0 \uAE30\uB85D\uB418\uC5B4 \uC788\uC2B5\uB2C8\uB2E4.",
                SaveToolTip = "\uC774\uBBF8 \uAC80\uC0AC \uBAA8\uB378\uB85C \uC800\uC7A5\uB41C \uD6C4\uBCF4\uC785\uB2C8\uB2E4.",
                RejectToolTip = "\uC774\uBBF8 \uC800\uC7A5\uB41C \uD6C4\uBCF4\uB294 \uC5EC\uAE30\uC11C \uAC70\uC808\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4."
            };
        }

        public static ModelCandidateDecisionPresentation BuildReviewAvailable()
        {
            return new ModelCandidateDecisionPresentation
            {
                CanSave = false,
                CanReject = false,
                StatusText = "\uD6C4\uBCF4 \uACB0\uC815: \uAC80\uD1A0 \uAC00\uB2A5",
                DetailText = "\uD6C4\uBCF4 \uAC80\uC99D\uC744 \uC2E4\uD589\uD574 \uAE30\uC874 \uAC80\uC0AC \uBAA8\uB378\uACFC \uBE44\uAD50\uD55C \uB4A4 \uC800\uC7A5 \uC5EC\uBD80\uB97C \uACB0\uC815\uD558\uC138\uC694.",
                SaveToolTip = "\uBA3C\uC800 \uD6C4\uBCF4 \uAC80\uC99D\uC73C\uB85C \uD559\uC2B5 \uACB0\uACFC\uB97C \uD655\uC778\uD558\uC138\uC694.",
                RejectToolTip = "\uBA3C\uC800 \uD6C4\uBCF4 \uAC80\uC99D\uC73C\uB85C \uD559\uC2B5 \uACB0\uACFC\uB97C \uD655\uC778\uD558\uC138\uC694."
            };
        }

        public static ModelCandidateDecisionPresentation BuildNoCandidate()
        {
            return new ModelCandidateDecisionPresentation
            {
                CanSave = false,
                CanReject = false,
                StatusText = "\uD6C4\uBCF4 \uACB0\uC815: \uD6C4\uBCF4 \uC5C6\uC74C",
                DetailText = "\uD559\uC2B5\uC774 \uC644\uB8CC\uB418\uC5B4 \uBAA8\uB378 \uD6C4\uBCF4\uAC00 \uC0DD\uAE30\uBA74 \uC5EC\uAE30\uC5D0\uC11C \uC800\uC7A5 \uB610\uB294 \uAC70\uC808 \uACB0\uC815\uC744 \uB0A8\uAE38 \uC218 \uC788\uC2B5\uB2C8\uB2E4.",
                SaveToolTip = "\uC800\uC7A5\uD560 \uD559\uC2B5 \uBAA8\uB378 \uD6C4\uBCF4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.",
                RejectToolTip = "\uAC70\uC808\uD560 \uD559\uC2B5 \uBAA8\uB378 \uD6C4\uBCF4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4."
            };
        }

        public static string BuildNoRejectCandidateStatus()
        {
            return "\uAC70\uC808\uD560 \uD559\uC2B5 \uBAA8\uB378 \uD6C4\uBCF4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.";
        }

        public static string BuildHeldCandidateSaveBlockedStatus()
        {
            return "\uD6C4\uBCF4 \uAC80\uC99D \uACB0\uACFC\uAC00 \uAD50\uCCB4 \uBCF4\uB958\uC5EC\uC11C \uAC80\uC0AC \uBAA8\uB378\uB85C \uC800\uC7A5\uD558\uC9C0 \uC54A\uC558\uC2B5\uB2C8\uB2E4. \uB370\uC774\uD130\uB97C \uBCF4\uAC15\uD558\uAC70\uB098 \uBAA8\uB378\uC744 \uC870\uC815\uD55C \uB4A4 \uD6C4\uBCF4 \uAC80\uC99D\uC744 \uB2E4\uC2DC \uC2E4\uD589\uD558\uC138\uC694.";
        }

        public static string BuildRejectDecisionSummary()
        {
            return "\uD6C4\uBCF4 \uAC70\uC808, \uAE30\uC874 \uAC80\uC0AC \uBAA8\uB378 \uC720\uC9C0";
        }

        public static string BuildRejectCommandStatus(string candidateWeightsPath, bool configSaved)
        {
            string candidateName = FormatModelName(candidateWeightsPath, "\uD6C4\uBCF4 \uBAA8\uB378");
            return configSaved
                ? $"\uD559\uC2B5 \uBAA8\uB378 \uD6C4\uBCF4\uB97C \uAC70\uC808\uD588\uC2B5\uB2C8\uB2E4: {candidateName}. \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378\uC744 \uC720\uC9C0\uD569\uB2C8\uB2E4."
                : $"\uD559\uC2B5 \uBAA8\uB378 \uD6C4\uBCF4 \uAC70\uC808\uC744 \uC800\uC7A5\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4: {candidateName}. \uD6C4\uBCF4\uB97C \uBCF4\uB958 \uC0C1\uD0DC\uB85C \uC720\uC9C0\uD569\uB2C8\uB2E4.";
        }

        public static string BuildRejectProjectConfigStatus(bool configSaved)
        {
            return configSaved
                ? "\uD559\uC2B5 \uBAA8\uB378 \uD6C4\uBCF4 \uAC70\uC808 \uAE30\uB85D \uC800\uC7A5 \uC644\uB8CC."
                : "\uD559\uC2B5 \uBAA8\uB378 \uD6C4\uBCF4 \uAC70\uC808\uC740 \uC800\uC7A5\uB418\uC9C0 \uC54A\uC558\uC2B5\uB2C8\uB2E4. Recipe \uC800\uC7A5 \uD6C4 \uB2E4\uC2DC \uAC70\uC808\uD558\uC138\uC694.";
        }

        public static string BuildRejectLog(string candidateWeightsPath, string baselineWeightsPath)
        {
            return $"\uD559\uC2B5 \uBAA8\uB378 \uD6C4\uBCF4 \uAC70\uC808: {candidateWeightsPath} / baseline={baselineWeightsPath}";
        }

        public static string BuildRejectFailureStatus(string message)
        {
            return $"\uD559\uC2B5 \uBAA8\uB378 \uD6C4\uBCF4 \uAC70\uC808 \uC2E4\uD328. \uD6C4\uBCF4\uB294 \uBCF4\uB958 \uC0C1\uD0DC\uB85C \uC720\uC9C0\uB418\uBA70 \uC7AC\uC2DC\uB3C4\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4: {NormalizeMessage(message)}";
        }

        public static string BuildAdoptionRefreshFailureStatus(string message)
        {
            return $"\uBAA8\uB378 \uC774\uB825 \uC801\uC6A9 \uC800\uC7A5\uC740 \uC644\uB8CC\uB418\uC5C8\uC9C0\uB9CC \uD654\uBA74 \uAC31\uC2E0\uC5D0 \uC2E4\uD328\uD588\uC2B5\uB2C8\uB2E4. \uC800\uC7A5\uB41C \uAC80\uC0AC \uBAA8\uB378\uC740 \uC720\uC9C0\uB429\uB2C8\uB2E4: {NormalizeMessage(message)}";
        }

        private static string FormatModelName(string path, string fallback)
        {
            string name = string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetFileName(path.Trim());
            return string.IsNullOrWhiteSpace(name) ? fallback : name;
        }

        private static string NormalizeMessage(string message)
        {
            return string.IsNullOrWhiteSpace(message)
                ? "\uC0C1\uC138 \uC6D0\uC778\uC744 \uD655\uC778\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4."
                : message.Trim();
        }
    }

    [Obsolete("Use ModelCandidateDecisionSnapshot.", false)]
    public sealed class WpfModelCandidateDecisionSnapshot : ModelCandidateDecisionSnapshot
    {
    }

    [Obsolete("Use ModelCandidateDecisionPresentation.", false)]
    public sealed class WpfModelCandidateDecisionPresentation : ModelCandidateDecisionPresentation
    {
        public WpfModelCandidateDecisionPresentation()
        {
        }

        public WpfModelCandidateDecisionPresentation(ModelCandidateDecisionPresentation source)
        {
            if (source == null)
            {
                return;
            }

            CanSave = source.CanSave;
            CanReject = source.CanReject;
            StatusText = source.StatusText;
            DetailText = source.DetailText;
            SaveToolTip = source.SaveToolTip;
            RejectToolTip = source.RejectToolTip;
        }
    }

    [Obsolete("Use ModelCandidateDecisionPresentationService.", false)]
    public static class WpfModelCandidateDecisionPresentationService
    {
        public static WpfModelCandidateDecisionPresentation Build(WpfModelCandidateDecisionSnapshot snapshot)
            => ToLegacy(ModelCandidateDecisionPresentationService.Build(snapshot));

        public static WpfModelCandidateDecisionPresentation BuildPendingCandidate(
            string candidateWeightsPath,
            string baselineWeightsPath,
            bool canReject)
            => ToLegacy(ModelCandidateDecisionPresentationService.BuildPendingCandidate(candidateWeightsPath, baselineWeightsPath, canReject));

        public static WpfModelCandidateDecisionPresentation BuildHeldCandidate(string candidateWeightsPath, bool canReject)
            => ToLegacy(ModelCandidateDecisionPresentationService.BuildHeldCandidate(candidateWeightsPath, canReject));

        public static WpfModelCandidateDecisionPresentation BuildRejectedCandidate(string candidateWeightsPath, string decisionSummary)
            => ToLegacy(ModelCandidateDecisionPresentationService.BuildRejectedCandidate(candidateWeightsPath, decisionSummary));

        public static WpfModelCandidateDecisionPresentation BuildSavedCandidate(string candidateWeightsPath)
            => ToLegacy(ModelCandidateDecisionPresentationService.BuildSavedCandidate(candidateWeightsPath));

        public static WpfModelCandidateDecisionPresentation BuildReviewAvailable()
            => ToLegacy(ModelCandidateDecisionPresentationService.BuildReviewAvailable());

        public static WpfModelCandidateDecisionPresentation BuildNoCandidate()
            => ToLegacy(ModelCandidateDecisionPresentationService.BuildNoCandidate());

        public static string BuildNoRejectCandidateStatus()
            => ModelCandidateDecisionPresentationService.BuildNoRejectCandidateStatus();

        public static string BuildHeldCandidateSaveBlockedStatus()
            => ModelCandidateDecisionPresentationService.BuildHeldCandidateSaveBlockedStatus();

        public static string BuildRejectDecisionSummary()
            => ModelCandidateDecisionPresentationService.BuildRejectDecisionSummary();

        public static string BuildRejectCommandStatus(string candidateWeightsPath, bool configSaved)
            => ModelCandidateDecisionPresentationService.BuildRejectCommandStatus(candidateWeightsPath, configSaved);

        public static string BuildRejectProjectConfigStatus(bool configSaved)
            => ModelCandidateDecisionPresentationService.BuildRejectProjectConfigStatus(configSaved);

        public static string BuildRejectLog(string candidateWeightsPath, string baselineWeightsPath)
            => ModelCandidateDecisionPresentationService.BuildRejectLog(candidateWeightsPath, baselineWeightsPath);

        public static string BuildRejectFailureStatus(string message)
            => ModelCandidateDecisionPresentationService.BuildRejectFailureStatus(message);

        public static string BuildAdoptionRefreshFailureStatus(string message)
            => ModelCandidateDecisionPresentationService.BuildAdoptionRefreshFailureStatus(message);

        private static WpfModelCandidateDecisionPresentation ToLegacy(ModelCandidateDecisionPresentation presentation)
            => presentation == null ? null : new WpfModelCandidateDecisionPresentation(presentation);
    }
}
