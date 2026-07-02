namespace MvcVisionSystem
{
    public sealed class WpfWorkflowStagePresentation
    {
        public WpfWorkflowStagePresentation(
            WpfShellWorkflowStage stage,
            string progressText,
            string titleText,
            string detailText,
            string nextActionText)
        {
            Stage = stage;
            ProgressText = progressText ?? string.Empty;
            TitleText = titleText ?? string.Empty;
            DetailText = detailText ?? string.Empty;
            NextActionText = nextActionText ?? string.Empty;
        }

        public WpfShellWorkflowStage Stage { get; }

        public string ProgressText { get; }

        public string TitleText { get; }

        public string DetailText { get; }

        public string NextActionText { get; }
    }

    public static class WpfWorkflowStagePresentationService
    {
        public static WpfWorkflowStagePresentation Build(WpfShellWorkflowStage stage)
        {
            return stage switch
            {
                WpfShellWorkflowStage.Labeling => new WpfWorkflowStagePresentation(
                    stage,
                    "2/4 라벨링",
                    "라벨링 워크벤치",
                    "저장 라벨만 보며 박스를 그리고 클래스 확인 후 라벨 저장.",
                    "다음: 현재 검사 또는 추론 검토에서 AI 후보 확인"),

                WpfShellWorkflowStage.Inference => new WpfWorkflowStagePresentation(
                    stage,
                    "3/4 추론",
                    "추론 검토",
                    "AI 후보만 보며 저장 라벨로 확정하거나 후보를 숨깁니다.",
                    "다음: AI 후보 확정/숨김 후 다음 미완료, 완료되면 학습/모델 센터"),

                WpfShellWorkflowStage.TrainingModel => new WpfWorkflowStagePresentation(
                    stage,
                    "4/4 학습/모델",
                    "학습/모델 센터",
                    "\uB370\uC774\uD130\uC14B \uC810\uAC80, \uD559\uC2B5 \uC9C4\uD589, best.pt \uD6C4\uBCF4\uC640 \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378\uC744 \uAD6C\uBD84\uD574 \uD655\uC778\uD569\uB2C8\uB2E4.",
                    "다음: 검증 후 모델 저장 또는 현재 검사로 확인"),

                _ => new WpfWorkflowStagePresentation(
                    WpfShellWorkflowStage.Dataset,
                    "1/4 데이터셋",
                    "데이터셋 홈",
                    "새 데이터셋을 만들거나 기존 데이터셋을 열고 저장 폴더와 이미지 폴더를 확인합니다.",
                    "다음: 라벨링 워크벤치에서 정답 라벨 저장")
            };
        }
    }
}
