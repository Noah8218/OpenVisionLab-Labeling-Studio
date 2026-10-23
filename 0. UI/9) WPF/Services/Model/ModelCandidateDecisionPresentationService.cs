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
            string candidateName = FormatModelName(candidateWeightsPath, OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.ModelFallback"));
            string baselineName = string.IsNullOrWhiteSpace(baselineWeightsPath)
                ? OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.Baseline.Unknown")
                : FormatModelName(baselineWeightsPath, OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.Baseline.Label"));

            return new ModelCandidateDecisionPresentation
            {
                CanSave = true,
                CanReject = canReject,
                StatusText = string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.Pending.Status"), candidateName),
                DetailText = string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.Pending.Detail"), baselineName),
                SaveToolTip = OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.Pending.SaveToolTip"),
                RejectToolTip = canReject
                    ? OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.Reject.KeepCurrentToolTip")
                    : OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.Reject.NoBaselineToolTip")
            };
        }

        public static ModelCandidateDecisionPresentation BuildHeldCandidate(
            string candidateWeightsPath,
            bool canReject)
        {
            string candidateName = FormatModelName(candidateWeightsPath, OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.ModelFallback"));
            return new ModelCandidateDecisionPresentation
            {
                CanSave = false,
                CanReject = canReject,
                StatusText = string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.Held.Status"), candidateName),
                DetailText = OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.Held.Detail"),
                SaveToolTip = OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.Held.SaveToolTip"),
                RejectToolTip = canReject
                    ? OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.Reject.KeepCurrentToolTip")
                    : OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.Reject.NoBaselineToolTip")
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
                StatusText = string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.Rejected.Status"), FormatModelName(candidateWeightsPath, OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.ModelFallback"))),
                DetailText = string.IsNullOrWhiteSpace(decisionSummary)
                    ? OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.Rejected.Detail")
                    : decisionSummary.Trim(),
                SaveToolTip = OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.Rejected.SaveToolTip"),
                RejectToolTip = OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.Rejected.RejectToolTip")
            };
        }

        public static ModelCandidateDecisionPresentation BuildSavedCandidate(string candidateWeightsPath)
        {
            return new ModelCandidateDecisionPresentation
            {
                CanSave = false,
                CanReject = false,
                StatusText = string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.Saved.Status"), FormatModelName(candidateWeightsPath, OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.ModelFallback"))),
                DetailText = OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.Saved.Detail"),
                SaveToolTip = OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.Saved.SaveToolTip"),
                RejectToolTip = OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.Saved.RejectToolTip")
            };
        }

        public static ModelCandidateDecisionPresentation BuildReviewAvailable()
        {
            return new ModelCandidateDecisionPresentation
            {
                CanSave = false,
                CanReject = false,
                StatusText = OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.ReviewAvailable.Status"),
                DetailText = OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.ReviewAvailable.Detail"),
                SaveToolTip = OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.ReviewAvailable.SaveToolTip"),
                RejectToolTip = OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.ReviewAvailable.RejectToolTip")
            };
        }

        public static ModelCandidateDecisionPresentation BuildNoCandidate()
        {
            return new ModelCandidateDecisionPresentation
            {
                CanSave = false,
                CanReject = false,
                StatusText = OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.None.Status"),
                DetailText = OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.None.Detail"),
                SaveToolTip = OpenVisionLab.OpenVisionLanguageService.T("WpfTraining.Text.WpfCandidateReviewPanelViewModel.TrainingSaveEmpty"),
                RejectToolTip = OpenVisionLab.OpenVisionLanguageService.T("WpfTraining.Text.WpfCandidateReviewPanelViewModel.TrainingRejectEmpty")
            };
        }

        public static string BuildNoRejectCandidateStatus()
        {
            return OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.Reject.NoModelStatus");
        }

        public static string BuildHeldCandidateSaveBlockedStatus()
        {
            return OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.Held.SaveBlockedStatus");
        }

        public static string BuildRejectDecisionSummary()
        {
            return OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.Reject.PlainStatus");
        }

        public static string BuildRejectCommandStatus(string candidateWeightsPath, bool configSaved)
        {
            string candidateName = FormatModelName(candidateWeightsPath, OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.ModelFallback"));
            return configSaved
                ? string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.Reject.SavedStatus"), candidateName)
                : string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.Reject.UnsavedStatus"), candidateName);
        }

        public static string BuildRejectProjectConfigStatus(bool configSaved)
        {
            return configSaved
                ? OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.Reject.ProjectSaved")
                : OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.Reject.ProjectUnsaved");
        }

        public static string BuildRejectLog(string candidateWeightsPath, string baselineWeightsPath)
        {
            return string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.Reject.Log"), candidateWeightsPath, baselineWeightsPath);
        }

        public static string BuildRejectFailureStatus(string message)
        {
            return string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.Reject.Failure"), NormalizeMessage(message));
        }

        public static string BuildAdoptionRefreshFailureStatus(string message)
        {
            return string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.Refresh.Failure"), NormalizeMessage(message));
        }

        private static string FormatModelName(string path, string fallback)
        {
            string name = string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetFileName(path.Trim());
            return string.IsNullOrWhiteSpace(name) ? fallback : name;
        }

        private static string NormalizeMessage(string message)
        {
            return string.IsNullOrWhiteSpace(message)
                ? OpenVisionLab.OpenVisionLanguageService.T("WpfModelCandidateDecision.Failure.Unknown")
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
