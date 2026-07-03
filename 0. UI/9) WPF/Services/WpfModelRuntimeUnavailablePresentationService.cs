using MvcVisionSystem._1._Core;

namespace MvcVisionSystem
{
    public sealed class WpfModelRuntimeUnavailablePresentation
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

    public static class WpfModelRuntimeUnavailablePresentationService
    {
        public static WpfModelRuntimeUnavailablePresentation Build(
            PythonModelRuntimeState runtimeState,
            string statusText = null)
        {
            runtimeState ??= new PythonModelRuntimeState(
                PythonModelRuntimeStateKind.NotInstalled,
                canRunTraining: false,
                canRunInference: false,
                "\uB77C\uBCA8\uB9C1 \uAC00\uB2A5 / \uBAA8\uB378 \uC2E4\uD589\uAE30 \uBBF8\uC124\uCE58",
                "\uBAA8\uB378 \uC2E4\uD589\uAE30 \uC0C1\uD0DC\uB97C \uD655\uC778\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.",
                "\uBAA8\uB378 \uC2E4\uD589\uAE30 \uC124\uCE58 \uB610\uB294 \uACBD\uB85C \uC5F0\uACB0 \uD544\uC694");

            string nextAction = string.IsNullOrWhiteSpace(runtimeState.NextActionText)
                ? "\uBAA8\uB378 \uC2E4\uD589\uAE30 \uC124\uCE58 \uB610\uB294 \uACBD\uB85C \uC5F0\uACB0 \uD544\uC694"
                : runtimeState.NextActionText.Trim();
            string commandStatus = string.IsNullOrWhiteSpace(statusText)
                ? runtimeState.DetailText
                : statusText.Trim();
            string readinessText = string.IsNullOrWhiteSpace(statusText)
                ? $"{runtimeState.SummaryText} / {nextAction}"
                : statusText.Trim();
            string recoveryTitle = runtimeState.State == PythonModelRuntimeStateKind.NotInstalled
                ? "\uBAA8\uB378 \uC2E4\uD589\uAE30 \uBBF8\uC124\uCE58"
                : "\uBAA8\uB378 \uC124\uC815 \uD655\uC778 \uD544\uC694";
            string recoveryAction = runtimeState.State == PythonModelRuntimeStateKind.NotInstalled
                ? "\uB2E4\uC74C: \uBAA8\uB378 \uC2E4\uD589\uAE30\uB97C \uC124\uCE58\uD558\uAC70\uB098 \uAE30\uC874 \uBAA8\uB378 \uACBD\uB85C\uB97C \uC5F0\uACB0\uD55C \uB4A4 \uB2E4\uC2DC \uC2E4\uD589\uD558\uC138\uC694."
                : nextAction;
            string currentModelText = "\uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378: \uBAA8\uB378 \uC2E4\uD589\uAE30 \uBBF8\uC124\uCE58";
            string inspectionStatusText = "\uBAA8\uB378 \uAE30\uB2A5: \uBBF8\uC124\uCE58";
            string modelStatusText = "\uBAA8\uB378: \uBBF8\uC124\uCE58";
            if (runtimeState.State != PythonModelRuntimeStateKind.NotInstalled)
            {
                currentModelText = "\uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378: \uBAA8\uB378 \uC124\uC815 \uD655\uC778 \uD544\uC694";
                inspectionStatusText = "\uBAA8\uB378 \uAE30\uB2A5: \uC124\uC815 \uD655\uC778 \uD544\uC694";
                modelStatusText = "\uBAA8\uB378: \uC124\uC815 \uD655\uC778 \uD544\uC694";
            }

            string candidateModelText = "\uC0C8 \uD559\uC2B5 \uBAA8\uB378 \uD6C4\uBCF4: \uC5C6\uC74C";
            string adoptionText = "\uBAA8\uB378 \uC801\uC6A9: \uBAA8\uB378 \uC2E4\uD589\uAE30 \uC5F0\uACB0 \uD544\uC694";
            string nextActionText = $"\uB2E4\uC74C: {nextAction}";
            string noCandidateText = "\uD6C4\uBCF4 \uC5C6\uC74C";

            return new WpfModelRuntimeUnavailablePresentation
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
                DecisionTitleText = "\uD310\uB2E8: \uBAA8\uB378 \uC2E4\uD589\uAE30 \uC5F0\uACB0 \uD544\uC694",
                DecisionEvidenceText = "\uADFC\uAC70: \uB77C\uBCA8\uB9C1\uC740 \uACC4\uC18D\uD560 \uC218 \uC788\uC9C0\uB9CC \uD559\uC2B5/\uD604\uC7AC \uAC80\uC0AC\uB294 \uC2E4\uD589\uAE30 \uC5F0\uACB0 \uD6C4 \uC0AC\uC6A9\uD569\uB2C8\uB2E4.",
                ProfileText = "\uBAA8\uB378 \uD504\uB85C\uD544: \uC2E4\uD589\uAE30 \uC5F0\uACB0 \uD544\uC694",
                TrainingRunText = "\uD559\uC2B5 \uC2E4\uD589: \uBAA8\uB378 \uC2E4\uD589\uAE30 \uC5F0\uACB0 \uC804",
                SummaryPrimaryText = "\uD604\uC7AC \uAC80\uC0AC: \uBAA8\uB378 \uC2E4\uD589\uAE30 \uBBF8\uC124\uCE58 / \uD559\uC2B5 \uD6C4\uBCF4: \uC5C6\uC74C",
                SummarySecondaryText = $"\uB77C\uBCA8\uB9C1\uC740 \uAC00\uB2A5 / {nextAction}"
            };
        }
    }
}
