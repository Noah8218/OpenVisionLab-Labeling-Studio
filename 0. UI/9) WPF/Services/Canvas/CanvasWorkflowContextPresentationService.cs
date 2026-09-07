using OpenVisionLab;

namespace MvcVisionSystem
{
    /// <summary>
    /// Builds the small workflow strip shown above the canvas from an immutable
    /// snapshot. The Window still owns controls and applies the result; this
    /// service owns only step/tool/action selection and wording.
    /// </summary>
    public static class CanvasWorkflowContextPresentationService
    {
        public static CanvasWorkflowContext Build(
            bool isInferenceMode,
            bool isAnomalyDatasetPurpose,
            bool hasActiveImage,
            bool hasUnsavedAnnotations,
            bool hasCanvasLabelObjects,
            int pendingCandidateCount,
            WpfLearningStep? selectedStep,
            string selectedStepText,
            WpfAnnotationTool? selectedTool,
            string selectedToolText,
            WpfAnnotationTool activeAnnotationTool,
            LabelingBoxDrawingMethod? selectedBoxDrawingMethod)
        {
            if (isAnomalyDatasetPurpose)
            {
                return new CanvasWorkflowContext(
                    WpfLearningStep.Label,
                    "이미지 판정",
                    "OK/NG",
                    "이미지 전체를 확인한 뒤 오른쪽에서 정상(OK) 또는 이상(NG)을 선택하세요.");
            }

            WpfLearningStep effectiveStep = ResolveEffectiveStep(
                isInferenceMode,
                hasActiveImage,
                hasUnsavedAnnotations,
                hasCanvasLabelObjects,
                pendingCandidateCount,
                selectedStep,
                selectedTool,
                activeAnnotationTool);
            string stepText = BuildStepText(effectiveStep, selectedStep, selectedStepText);
            string toolText = BuildToolText(effectiveStep, selectedToolText);
            string actionText = BuildActionText(
                effectiveStep,
                hasActiveImage,
                hasUnsavedAnnotations,
                hasCanvasLabelObjects,
                selectedTool,
                selectedBoxDrawingMethod);
            return new CanvasWorkflowContext(effectiveStep, stepText, toolText, actionText);
        }

        public static string FormatDisplayMode(WpfCanvasDisplayMode mode)
        {
            return mode switch
            {
                WpfCanvasDisplayMode.InferenceOnly => "AI 후보",
                WpfCanvasDisplayMode.Both => "비교",
                _ => "라벨만"
            };
        }

        private static WpfLearningStep ResolveEffectiveStep(
            bool isInferenceMode,
            bool hasActiveImage,
            bool hasUnsavedAnnotations,
            bool hasCanvasLabelObjects,
            int pendingCandidateCount,
            WpfLearningStep? selectedStep,
            WpfAnnotationTool? selectedTool,
            WpfAnnotationTool activeAnnotationTool)
        {
            if (isInferenceMode)
            {
                return pendingCandidateCount > 0
                    ? WpfLearningStep.Review
                    : WpfLearningStep.Infer;
            }

            if (!hasActiveImage)
            {
                return WpfLearningStep.Sample;
            }

            if (IsCanvasLabelingTool(selectedTool) || IsCanvasLabelingTool(activeAnnotationTool))
            {
                return WpfLearningStep.Label;
            }

            if (hasUnsavedAnnotations || hasCanvasLabelObjects)
            {
                return WpfLearningStep.Save;
            }

            return selectedStep == WpfLearningStep.Sample
                ? WpfLearningStep.Label
                : selectedStep ?? WpfLearningStep.Label;
        }

        private static string BuildStepText(
            WpfLearningStep effectiveStep,
            WpfLearningStep? selectedStep,
            string selectedStepText)
        {
            if (selectedStep == effectiveStep && !string.IsNullOrWhiteSpace(selectedStepText))
            {
                return selectedStepText;
            }

            return effectiveStep switch
            {
                WpfLearningStep.Label => "라벨",
                WpfLearningStep.Infer => "추론",
                WpfLearningStep.Review => "검토",
                WpfLearningStep.Save => "저장",
                _ => "샘플"
            };
        }

        private static string BuildToolText(WpfLearningStep effectiveStep, string selectedToolText)
        {
            return effectiveStep switch
            {
                WpfLearningStep.Sample => "이미지 큐",
                WpfLearningStep.Infer => "현재 검사",
                WpfLearningStep.Review => "AI 후보 검토",
                _ => selectedToolText
            };
        }

        private static string BuildActionText(
            WpfLearningStep effectiveStep,
            bool hasActiveImage,
            bool hasUnsavedAnnotations,
            bool hasCanvasLabelObjects,
            WpfAnnotationTool? selectedTool,
            LabelingBoxDrawingMethod? selectedBoxDrawingMethod)
        {
            if (!hasActiveImage)
            {
                return OpenVisionLanguageService.T("WpfCanvas.Workflow.NoImageAction");
            }

            if (effectiveStep == WpfLearningStep.Label)
            {
                if (selectedTool == WpfAnnotationTool.Rectangle
                    && selectedBoxDrawingMethod == LabelingBoxDrawingMethod.FourPointExtreme)
                {
                    return "박스 라벨링: 객체의 위 → 아래 → 왼쪽 → 오른쪽 극점을 순서대로 누르세요. Backspace는 이전 점, Esc는 초안 취소입니다.";
                }

                return selectedTool switch
                {
                    WpfAnnotationTool.Rectangle => "박스 라벨링: 캔버스에서 드래그하고 클래스가 맞는지 확인한 뒤 라벨 저장을 누르세요.",
                    WpfAnnotationTool.Ellipse => "원형 라벨링: 드래그해 영역을 만들고 클래스와 위치를 확인한 뒤 라벨 저장을 누르세요.",
                    WpfAnnotationTool.Polygon => "폴리곤 라벨링: 꼭짓점을 찍고 마지막 점에서 마무리하세요.",
                    WpfAnnotationTool.Brush => "마스크 칠하기: 드래그하고 놓은 뒤 결과를 확인한 다음 라벨 저장을 누르세요.",
                    WpfAnnotationTool.Eraser => "마스크 편집: 마스크 위를 드래그해 지울 영역을 정리하세요.",
                    WpfAnnotationTool.PanZoom => "화면 이동: 이미지를 끌어 위치를 맞춘 뒤 라벨 도구로 돌아가세요.",
                    _ when hasUnsavedAnnotations => "저장 필요: 라벨 저장 버튼을 눌러 현재 이미지의 라벨을 저장하세요.",
                    _ when hasCanvasLabelObjects => "객체 검토: 라벨 위치와 클래스를 확인한 뒤 라벨 저장을 누르세요.",
                    _ => "라벨링 시작: 빠른 도구에서 박스나 브러시를 선택하고 캔버스에 그리세요."
                };
            }

            return effectiveStep switch
            {
                WpfLearningStep.Sample => "이미지 선택: 이미지 큐에서 작업할 이미지를 여세요.",
                WpfLearningStep.Infer => "추론 실행: 현재 검사로 AI 후보를 만들고 검토 탭에서 확인하세요.",
                WpfLearningStep.Review => "AI 후보 검토: 확정, 전체 확정, 또는 스킵하세요.",
                WpfLearningStep.Save when hasUnsavedAnnotations => "저장 필요: 라벨 저장 버튼으로 현재 라벨을 파일에 반영하세요.",
                WpfLearningStep.Save => "저장 완료: 이미지 큐의 다음 버튼으로 이어서 작업하세요.",
                _ => "다음 작업을 선택하세요."
            };
        }

        private static bool IsCanvasLabelingTool(WpfAnnotationTool? tool)
        {
            return tool == WpfAnnotationTool.Rectangle
                || tool == WpfAnnotationTool.Ellipse
                || tool == WpfAnnotationTool.Polygon
                || tool == WpfAnnotationTool.Brush
                || tool == WpfAnnotationTool.Eraser;
        }
    }

    [System.Obsolete("Use CanvasWorkflowContextPresentationService.", false)]
    public static class WpfCanvasWorkflowContextPresentationService
    {
        public static WpfCanvasWorkflowContext Build(
            bool isInferenceMode,
            bool isAnomalyDatasetPurpose,
            bool hasActiveImage,
            bool hasUnsavedAnnotations,
            bool hasCanvasLabelObjects,
            int pendingCandidateCount,
            WpfLearningStep? selectedStep,
            string selectedStepText,
            WpfAnnotationTool? selectedTool,
            string selectedToolText,
            WpfAnnotationTool activeAnnotationTool,
            LabelingBoxDrawingMethod? selectedBoxDrawingMethod)
            => new WpfCanvasWorkflowContext(CanvasWorkflowContextPresentationService.Build(
                isInferenceMode,
                isAnomalyDatasetPurpose,
                hasActiveImage,
                hasUnsavedAnnotations,
                hasCanvasLabelObjects,
                pendingCandidateCount,
                selectedStep,
                selectedStepText,
                selectedTool,
                selectedToolText,
                activeAnnotationTool,
                selectedBoxDrawingMethod));

        public static string FormatDisplayMode(WpfCanvasDisplayMode mode)
            => CanvasWorkflowContextPresentationService.FormatDisplayMode(mode);
    }

    public class CanvasWorkflowContext
    {
        public CanvasWorkflowContext(
            WpfLearningStep effectiveStep,
            string stepText,
            string toolText,
            string actionText)
        {
            EffectiveStep = effectiveStep;
            StepText = stepText ?? string.Empty;
            ToolText = toolText ?? string.Empty;
            ActionText = actionText ?? string.Empty;
        }

        public WpfLearningStep EffectiveStep { get; }

        public string StepText { get; }

        public string ToolText { get; }

        public string ActionText { get; }
    }

    [System.Obsolete("Use CanvasWorkflowContext.", false)]
    public sealed class WpfCanvasWorkflowContext : CanvasWorkflowContext
    {
        public WpfCanvasWorkflowContext(
            WpfLearningStep effectiveStep,
            string stepText,
            string toolText,
            string actionText)
            : base(effectiveStep, stepText, toolText, actionText)
        {
        }

        public WpfCanvasWorkflowContext(CanvasWorkflowContext source)
            : this(source?.EffectiveStep ?? WpfLearningStep.Sample, source?.StepText, source?.ToolText, source?.ActionText)
        {
        }
    }
}
