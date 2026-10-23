using MvcVisionSystem._1._Core;
using System;

namespace MvcVisionSystem
{
    public class ModelRuntimeUnavailablePresentation
    {
        public string CommandStatusText { get; set; } = string.Empty;

        public string LogText { get; set; } = string.Empty;

        public string RecoveryTitle { get; set; } = string.Empty;

        public string RecoveryDetail { get; set; } = string.Empty;

        public string RecoveryAction { get; set; } = string.Empty;

        public string ReadinessText { get; set; } = string.Empty;

        public string InspectionStatusText { get; set; } = string.Empty;

        public string InspectionStatusToolTip { get; set; } = string.Empty;

        public string ModelStatusText { get; set; } = string.Empty;

        public string CurrentModelText { get; set; } = string.Empty;

        public string CandidateModelText { get; set; } = string.Empty;

        public string AdoptionText { get; set; } = string.Empty;

        public string NextActionText { get; set; } = string.Empty;

        public string NoCandidateText { get; set; } = string.Empty;

        public string CandidateReviewDetailText { get; set; } = string.Empty;

        public string DecisionTitleText { get; set; } = string.Empty;

        public string DecisionEvidenceText { get; set; } = string.Empty;

        public string ProfileText { get; set; } = string.Empty;

        public string TrainingRunText { get; set; } = string.Empty;

        public string SummaryPrimaryText { get; set; } = string.Empty;

        public string SummarySecondaryText { get; set; } = string.Empty;
    }

    public static class ModelRuntimeUnavailablePresentationService
    {
        public static ModelCenterDashboardState BuildDashboardState(ModelRuntimeUnavailablePresentation presentation)
        {
            if (presentation == null)
            {
                return null;
            }

            ModelCenterDashboardState state = ModelCenterDashboardPresentationService.BuildModelCenterState(
                presentation.CurrentModelText,
                presentation.CandidateModelText,
                presentation.AdoptionText,
                presentation.NextActionText,
                presentation.NoCandidateText,
                presentation.CandidateReviewDetailText,
                false,
                presentation.DecisionTitleText,
                presentation.DecisionEvidenceText,
                presentation.NextActionText);
            state.ReviewCandidateButtonText = presentation.NoCandidateText;
            state.ReviewCandidateButtonToolTip = presentation.CandidateReviewDetailText;
            state.CanReviewCandidate = false;
            state.RegistryPresentation = new WpfModelRegistryPresentation
            {
                ProfileText = presentation.ProfileText,
                TrainingRunText = presentation.TrainingRunText,
                CandidateModelText = presentation.CandidateModelText,
                InspectionModelText = presentation.CurrentModelText,
                ActionText = presentation.NextActionText,
                SummaryPrimaryText = presentation.SummaryPrimaryText,
                SummarySecondaryText = presentation.SummarySecondaryText,
                HistoryItems = new WpfModelRegistryHistoryItem[0]
            };
            return state;
        }

        public static ModelRuntimeUnavailablePresentation Build(
            PythonModelRuntimeState runtimeState,
            string statusText = null)
        {
            runtimeState ??= new PythonModelRuntimeState(
                PythonModelRuntimeStateKind.NotInstalled,
                canRunTraining: false,
                canRunInference: false,
                OpenVisionLab.OpenVisionLanguageService.T("WpfModelRuntimeUnavailable.Fallback.Summary"),
                OpenVisionLab.OpenVisionLanguageService.T("WpfModelRuntimeUnavailable.Fallback.Detail"),
                OpenVisionLab.OpenVisionLanguageService.T("WpfModelRuntimeUnavailable.NextActionFallback"));

            string nextAction = string.IsNullOrWhiteSpace(runtimeState.NextActionText)
                ? OpenVisionLab.OpenVisionLanguageService.T("WpfModelRuntimeUnavailable.NextActionFallback")
                : runtimeState.NextActionText.Trim();
            string commandStatus = string.IsNullOrWhiteSpace(statusText)
                ? runtimeState.DetailText
                : statusText.Trim();
            string readinessText = string.IsNullOrWhiteSpace(statusText)
                ? $"{runtimeState.SummaryText} / {nextAction}"
                : statusText.Trim();
            string recoveryTitle = runtimeState.State == PythonModelRuntimeStateKind.NotInstalled
                ? OpenVisionLab.OpenVisionLanguageService.T("WpfModelRuntimeUnavailable.Recovery.NotInstalled.Title")
                : OpenVisionLab.OpenVisionLanguageService.T("WpfModelRuntimeUnavailable.Recovery.Settings.Title");
            string recoveryAction = runtimeState.State == PythonModelRuntimeStateKind.NotInstalled
                ? OpenVisionLab.OpenVisionLanguageService.T("WpfModelRuntimeUnavailable.Recovery.NotInstalled.Action")
                : nextAction;
            string currentModelText = OpenVisionLab.OpenVisionLanguageService.T("WpfModelRuntimeUnavailable.CurrentModel.NotInstalled");
            string inspectionStatusText = OpenVisionLab.OpenVisionLanguageService.T("WpfModelRuntimeUnavailable.InspectionStatus.NotInstalled");
            string modelStatusText = OpenVisionLab.OpenVisionLanguageService.T("WpfModelRuntimeUnavailable.ModelStatus.NotInstalled");
            if (runtimeState.State != PythonModelRuntimeStateKind.NotInstalled)
            {
                currentModelText = OpenVisionLab.OpenVisionLanguageService.T("WpfModelRuntimeUnavailable.CurrentModel.Settings");
                inspectionStatusText = OpenVisionLab.OpenVisionLanguageService.T("WpfModelRuntimeUnavailable.InspectionStatus.Settings");
                modelStatusText = OpenVisionLab.OpenVisionLanguageService.T("WpfModelRuntimeUnavailable.ModelStatus.Settings");
            }

            string candidateModelText = OpenVisionLab.OpenVisionLanguageService.T("WpfModelRuntimeUnavailable.Candidate.Unchecked");
            string adoptionText = OpenVisionLab.OpenVisionLanguageService.T("WpfModelRuntimeUnavailable.Assignment.RuntimeNeeded");
            string nextActionText = string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelRuntimeUnavailable.NextAction.Value"), nextAction);
            string noCandidateText = OpenVisionLab.OpenVisionLanguageService.T("WpfModelRuntimeUnavailable.ReviewUnavailable.Text");

            return new ModelRuntimeUnavailablePresentation
            {
                CommandStatusText = commandStatus,
                LogText = $"{commandStatus} / {runtimeState.DetailText}",
                RecoveryTitle = recoveryTitle,
                RecoveryDetail = runtimeState.DetailText,
                RecoveryAction = recoveryAction,
                ReadinessText = readinessText,
                InspectionStatusText = inspectionStatusText,
                InspectionStatusToolTip = runtimeState.DetailText,
                ModelStatusText = modelStatusText,
                CurrentModelText = currentModelText,
                CandidateModelText = candidateModelText,
                AdoptionText = adoptionText,
                NextActionText = nextActionText,
                NoCandidateText = noCandidateText,
                CandidateReviewDetailText = nextAction,
                DecisionTitleText = OpenVisionLab.OpenVisionLanguageService.T("WpfModelRuntimeUnavailable.Decision.RuntimeNeeded"),
                DecisionEvidenceText = OpenVisionLab.OpenVisionLanguageService.T("WpfModelRuntimeUnavailable.Decision.RuntimeEvidence"),
                ProfileText = OpenVisionLab.OpenVisionLanguageService.T("WpfModelRuntimeUnavailable.Profile.RuntimeNeeded"),
                TrainingRunText = OpenVisionLab.OpenVisionLanguageService.T("WpfModelRuntimeUnavailable.TrainingRun.RuntimeNeeded"),
                SummaryPrimaryText = OpenVisionLab.OpenVisionLanguageService.T("WpfModelRuntimeUnavailable.Summary.RuntimeNeeded"),
                SummarySecondaryText = string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelRuntimeUnavailable.Summary.Secondary"), nextAction)
            };
        }
    }

    [Obsolete("Use ModelRuntimeUnavailablePresentation.", false)]
    public sealed class WpfModelRuntimeUnavailablePresentation : ModelRuntimeUnavailablePresentation
    {
        public WpfModelRuntimeUnavailablePresentation()
        {
        }

        public WpfModelRuntimeUnavailablePresentation(ModelRuntimeUnavailablePresentation source)
        {
            if (source == null)
            {
                return;
            }

            CommandStatusText = source.CommandStatusText;
            LogText = source.LogText;
            RecoveryTitle = source.RecoveryTitle;
            RecoveryDetail = source.RecoveryDetail;
            RecoveryAction = source.RecoveryAction;
            ReadinessText = source.ReadinessText;
            InspectionStatusText = source.InspectionStatusText;
            InspectionStatusToolTip = source.InspectionStatusToolTip;
            ModelStatusText = source.ModelStatusText;
            CurrentModelText = source.CurrentModelText;
            CandidateModelText = source.CandidateModelText;
            AdoptionText = source.AdoptionText;
            NextActionText = source.NextActionText;
            NoCandidateText = source.NoCandidateText;
            CandidateReviewDetailText = source.CandidateReviewDetailText;
            DecisionTitleText = source.DecisionTitleText;
            DecisionEvidenceText = source.DecisionEvidenceText;
            ProfileText = source.ProfileText;
            TrainingRunText = source.TrainingRunText;
            SummaryPrimaryText = source.SummaryPrimaryText;
            SummarySecondaryText = source.SummarySecondaryText;
        }
    }

    [Obsolete("Use ModelRuntimeUnavailablePresentationService.", false)]
    public static class WpfModelRuntimeUnavailablePresentationService
    {
        public static WpfModelCenterDashboardState BuildDashboardState(WpfModelRuntimeUnavailablePresentation presentation)
            => WpfModelCenterDashboardState.FromCanonical(ModelRuntimeUnavailablePresentationService.BuildDashboardState(presentation));

        public static WpfModelRuntimeUnavailablePresentation Build(
            PythonModelRuntimeState runtimeState,
            string statusText = null)
            => new WpfModelRuntimeUnavailablePresentation(ModelRuntimeUnavailablePresentationService.Build(runtimeState, statusText));
    }
}
