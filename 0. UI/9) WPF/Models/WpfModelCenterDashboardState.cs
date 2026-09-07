using System;

namespace MvcVisionSystem
{
    [Obsolete("Use ModelCenterDashboardState.", false)]
    public sealed class WpfModelCenterDashboardState : ModelCenterDashboardState
    {
        internal static WpfModelCenterDashboardState FromCanonical(ModelCenterDashboardState source)
        {
            if (source == null)
            {
                return null;
            }

            return new WpfModelCenterDashboardState
            {
                RegistryPresentation = source.RegistryPresentation,
                CurrentModelText = source.CurrentModelText,
                CurrentModelDetailText = source.CurrentModelDetailText,
                CandidateModelText = source.CandidateModelText,
                CandidateModelDetailText = source.CandidateModelDetailText,
                AdoptionText = source.AdoptionText,
                AdoptionDetailText = source.AdoptionDetailText,
                NextActionText = source.NextActionText,
                NextActionDetailText = source.NextActionDetailText,
                ReviewCandidateButtonText = source.ReviewCandidateButtonText,
                ReviewCandidateButtonToolTip = source.ReviewCandidateButtonToolTip,
                CanReviewCandidate = source.CanReviewCandidate,
                ConfirmModelButtonText = source.ConfirmModelButtonText,
                ConfirmModelButtonToolTip = source.ConfirmModelButtonToolTip,
                CanConfirmModel = source.CanConfirmModel,
                DecisionSummaryText = source.DecisionSummaryText,
                DecisionEvidenceText = source.DecisionEvidenceText,
                DecisionActionText = source.DecisionActionText,
                RuntimeActionText = source.RuntimeActionText
            };
        }
    }
}
