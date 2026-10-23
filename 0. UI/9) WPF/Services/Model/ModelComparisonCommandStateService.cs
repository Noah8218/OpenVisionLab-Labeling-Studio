using MvcVisionSystem.Yolo;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the model-comparison command's readiness wording and availability.
    /// The Shell supplies a snapshot and applies the result to its ViewModel.
    /// </summary>
    public static class ModelComparisonCommandStateService
    {
        public static ModelComparisonCommandState Build(
            bool isModelComparisonRunning,
            WorkflowCommandState state,
            YoloDatasetReadinessReport report,
            bool isInferenceStageActive = false,
            bool hasSelectedInferenceCandidate = false,
            bool selectedInferenceCandidateUsesCurrentRuntime = false)
        {
            if (isModelComparisonRunning)
            {
                return new ModelComparisonCommandState(
                    false,
                    OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonCommandStateService.Running"),
                    OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonCommandStateService.RunningHelp"),
                    OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonCommandStateService.RunningBasis"));
            }

            if (state?.CanRunGeneralCommands != true)
            {
                return new ModelComparisonCommandState(
                    false,
                    OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonCommandStateService.Waiting"),
                    OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonCommandStateService.BusyHelp"),
                    OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonCommandStateService.BusyBasis"));
            }

            if (isInferenceStageActive && !hasSelectedInferenceCandidate)
            {
                return new ModelComparisonCommandState(
                    false,
                    OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonCommandStateService.SelectedCompare"),
                    "추론 화면에서 비교할 학습 모델을 선택하세요.",
                    "비교 대상: 현재 검사 모델과 선택한 학습 모델");
            }

            if (isInferenceStageActive && !selectedInferenceCandidateUsesCurrentRuntime)
            {
                return new ModelComparisonCommandState(
                    false,
                    OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonCommandStateService.SelectedCompare"),
                    "선택한 학습 모델이 현재 검사 모델과 같은 실행 환경을 사용하지 않습니다.",
                    "비교하려면 같은 모델 실행 환경에서 학습한 모델을 선택하세요.");
            }

            if (report == null)
            {
                return new ModelComparisonCommandState(
                    false,
                    OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonCommandStateService.CheckNeeded"),
                    OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonCommandStateService.CheckHelp"),
                    OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonCommandStateService.CheckBasis"));
            }

            if (!report.IsReady)
            {
                return new ModelComparisonCommandState(
                    false,
                    OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonCommandStateService.Blocked"),
                    OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonCommandStateService.BlockedHelp"),
                    OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonCommandStateService.BlockedBasis"));
            }

            int testImageCount = report.Statistics?.TestImageCount ?? 0;
            int testLabelCount = report.Statistics?.TestLabelCount ?? 0;
            int finalVerificationCount = System.Math.Min(testImageCount, testLabelCount);
            if (testImageCount <= 0)
            {
                return new ModelComparisonCommandState(
                    false,
                    OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonCommandStateService.TestNeeded"),
                    OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonCommandStateService.TestHelp"),
                    OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonCommandStateService.TestEmpty"));
            }

            if (testLabelCount <= 0)
            {
                return new ModelComparisonCommandState(
                    false,
                    OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonCommandStateService.LabelsNeeded"),
                    OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonCommandStateService.LabelsHelp"),
                    string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonCommandStateService.LabelsEmpty"), testImageCount));
            }

            if (finalVerificationCount < DatasetDashboardPresentationService.RecommendedModelReplacementTestImageCount)
            {
                return new ModelComparisonCommandState(
                    true,
                    isInferenceStageActive ? OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonCommandStateService.SelectedCompare") : OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonCommandStateService.Compare"),
                    isInferenceStageActive
                        ? string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonCommandStateService.SelectedHelp"), finalVerificationCount)
                        : string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonCommandStateService.WeakHelp"), finalVerificationCount, DatasetDashboardPresentationService.RecommendedModelReplacementTestImageCount),
                    string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonCommandStateService.WeakBasis"), finalVerificationCount, DatasetDashboardPresentationService.RecommendedModelReplacementTestImageCount));
            }

            return new ModelComparisonCommandState(
                true,
                isInferenceStageActive ? OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonCommandStateService.SelectedCompare") : OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonCommandStateService.Compare"),
                isInferenceStageActive
                    ? string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonCommandStateService.SelectedHelp"), finalVerificationCount)
                    : string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonCommandStateService.ReadyHelp"), finalVerificationCount),
                string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonCommandStateService.ReadyBasis"), finalVerificationCount, DatasetDashboardPresentationService.RecommendedModelReplacementTestImageCount));
        }
    }

    [System.Obsolete("Use ModelComparisonCommandStateService.", false)]
    public static class WpfModelComparisonCommandStateService
    {
        public static WpfModelComparisonCommandState Build(
            bool isModelComparisonRunning,
            WpfWorkflowCommandState state,
            YoloDatasetReadinessReport report)
            => new WpfModelComparisonCommandState(ModelComparisonCommandStateService.Build(isModelComparisonRunning, state, report));
    }

    public class ModelComparisonCommandState
    {
        public ModelComparisonCommandState(
            bool isEnabled,
            string actionText,
            string toolTipText,
            string basisText)
        {
            IsEnabled = isEnabled;
            ActionText = actionText ?? string.Empty;
            ToolTipText = toolTipText ?? string.Empty;
            BasisText = basisText ?? string.Empty;
        }

        public bool IsEnabled { get; }

        public string ActionText { get; }

        public string ToolTipText { get; }

        public string BasisText { get; }
    }

    [System.Obsolete("Use ModelComparisonCommandState.", false)]
    public sealed class WpfModelComparisonCommandState : ModelComparisonCommandState
    {
        public WpfModelComparisonCommandState(
            bool isEnabled,
            string actionText,
            string toolTipText,
            string basisText)
            : base(isEnabled, actionText, toolTipText, basisText)
        {
        }

        public WpfModelComparisonCommandState(ModelComparisonCommandState source)
            : this(source?.IsEnabled ?? false, source?.ActionText, source?.ToolTipText, source?.BasisText)
        {
        }
    }
}
