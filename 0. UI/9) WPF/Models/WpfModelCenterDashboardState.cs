namespace MvcVisionSystem
{
    /// <summary>
    /// Read-only Model Center presentation state built from the active recipe
    /// and its discovered training candidate.
    /// </summary>
    public sealed class WpfModelCenterDashboardState
    {
        public WpfModelRegistryPresentation RegistryPresentation { get; set; }

        public string CurrentModelText { get; set; } = string.Empty;

        public string CandidateModelText { get; set; } = string.Empty;

        public string AdoptionText { get; set; } = string.Empty;

        public string NextActionText { get; set; } = string.Empty;

        public string ReviewCandidateButtonText { get; set; } = string.Empty;

        public string ReviewCandidateButtonToolTip { get; set; } = string.Empty;

        public bool CanReviewCandidate { get; set; }

        public string ConfirmModelButtonText { get; set; } = string.Empty;

        public string ConfirmModelButtonToolTip { get; set; } = string.Empty;

        public bool CanConfirmModel { get; set; }

        public string DecisionSummaryText { get; set; } = string.Empty;

        public string DecisionEvidenceText { get; set; } = string.Empty;

        public string DecisionActionText { get; set; } = string.Empty;

        public string RuntimeActionText { get; set; } = string.Empty;
    }
}
