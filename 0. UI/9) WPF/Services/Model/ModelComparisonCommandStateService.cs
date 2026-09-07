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
            YoloDatasetReadinessReport report)
        {
            if (isModelComparisonRunning)
            {
                return new ModelComparisonCommandState(
                    false,
                    "비교 실행 중",
                    "최종 검증 이미지로 기존 모델과 새 학습 모델을 비교하는 중입니다.",
                    "비교 기준: 최종 검증 이미지에서 기존 모델과 새 모델의 차이를 계산 중입니다.");
            }

            if (state?.CanRunGeneralCommands != true)
            {
                return new ModelComparisonCommandState(
                    false,
                    "대기",
                    "현재 다른 명령이 실행 중이므로 완료 후 비교할 수 있습니다.",
                    "비교 기준: 진행 중인 명령이 완료된 뒤 최종 검증 상태를 다시 확인합니다.");
            }

            if (report == null)
            {
                return new ModelComparisonCommandState(
                    false,
                    "점검 필요",
                    "데이터셋 점검으로 학습/검증/최종 검증 상태를 먼저 확인하세요.",
                    "비교 기준: 데이터셋 점검 후 최종 검증 이미지와 정답 라벨 수를 표시합니다.");
            }

            if (!report.IsReady)
            {
                return new ModelComparisonCommandState(
                    false,
                    "학습 불가",
                    "모델 비교 전에 데이터셋 학습 불가 항목을 먼저 해결하세요.",
                    "비교 기준: 학습 가능 상태가 된 후 최종 검증 라벨로 교체 판단을 합니다.");
            }

            int testImageCount = report.Statistics?.TestImageCount ?? 0;
            int testLabelCount = report.Statistics?.TestLabelCount ?? 0;
            int finalVerificationCount = System.Math.Min(testImageCount, testLabelCount);
            if (testImageCount <= 0)
            {
                return new ModelComparisonCommandState(
                    false,
                    "최종 검증 필요",
                    "모델 교체 판단 전에 최종 검증 이미지를 1장 이상 확보하세요.",
                    "비교 기준: 최종 검증 이미지 0장 / 권장 10장 이상");
            }

            if (testLabelCount <= 0)
            {
                return new ModelComparisonCommandState(
                    false,
                    "최종 라벨 필요",
                    "최종 검증 이미지는 있지만 정답 라벨 파일이 없습니다. 비교 전 최종 검증 라벨을 저장하세요.",
                    $"비교 기준: 최종 검증 이미지 {testImageCount}장 / 정답 라벨 0장");
            }

            if (finalVerificationCount < DatasetDashboardPresentationService.RecommendedModelReplacementTestImageCount)
            {
                return new ModelComparisonCommandState(
                    true,
                    "모델 비교",
                    $"최종 검증 라벨 {finalVerificationCount}장으로 비교는 가능하지만 교체 근거가 약합니다. 권장 {DatasetDashboardPresentationService.RecommendedModelReplacementTestImageCount}장 이상을 확보한 뒤 적용하세요.",
                    $"비교 기준: 최종 검증 라벨 {finalVerificationCount}장 / 권장 {DatasetDashboardPresentationService.RecommendedModelReplacementTestImageCount}장 이상 - 비교 가능, 교체 근거는 약함");
            }

            return new ModelComparisonCommandState(
                true,
                "모델 비교",
                $"최종 검증 라벨 {finalVerificationCount}장으로 기존 모델과 새 학습 모델을 비교합니다.",
                $"비교 기준: 최종 검증 라벨 {finalVerificationCount}장 / 권장 {DatasetDashboardPresentationService.RecommendedModelReplacementTestImageCount}장 이상 - 비교 후 교체 판단 가능");
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
