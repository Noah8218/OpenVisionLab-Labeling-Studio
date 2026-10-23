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
            LabelingBoxDrawingMethod? selectedBoxDrawingMethod,
            MaskEraserScope eraserScope = MaskEraserScope.VisibleEditable,
            bool hasSelectedMaskObject = false,
            bool isSmartMaskCandidateEditActive = false)
        {
            if (isAnomalyDatasetPurpose)
            {
                return new CanvasWorkflowContext(
                    WpfLearningStep.Label,
                    OpenVisionLanguageService.T("WpfAnomalyGrid.SingleTitle"),
                    "OK/NG",
                    OpenVisionLanguageService.T("WpfCanvas.Workflow.ImageDecisionAction"));
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
            string toolText = BuildToolText(
                effectiveStep,
                selectedTool,
                activeAnnotationTool,
                selectedToolText);
            string actionText = BuildActionText(
                effectiveStep,
                hasActiveImage,
                hasUnsavedAnnotations,
                hasCanvasLabelObjects,
                selectedTool,
                selectedBoxDrawingMethod,
                eraserScope,
                hasSelectedMaskObject,
                isSmartMaskCandidateEditActive);
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
                WpfLearningStep.Label => OpenVisionLanguageService.T("WpfShell.Right.Labels.Text"),
                WpfLearningStep.Infer => OpenVisionLanguageService.T("WpfShell.Stage.Inference.Text"),
                WpfLearningStep.Review => OpenVisionLanguageService.T("WpfCanvas.Workflow.Step.Review"),
                WpfLearningStep.Save => OpenVisionLanguageService.T("Localization.Save"),
                _ => OpenVisionLanguageService.T("WpfCanvas.Workflow.Step.Sample")
            };
        }

        private static string BuildToolText(
            WpfLearningStep effectiveStep,
            WpfAnnotationTool? selectedTool,
            WpfAnnotationTool activeAnnotationTool,
            string selectedToolText)
        {
            if (activeAnnotationTool != WpfAnnotationTool.Select)
            {
                return FormatAnnotationToolText(activeAnnotationTool, selectedToolText);
            }

            if (selectedTool.HasValue && selectedTool.Value != WpfAnnotationTool.Select)
            {
                return FormatAnnotationToolText(selectedTool.Value, selectedToolText);
            }

            string workflowToolText = effectiveStep switch
            {
                WpfLearningStep.Sample => OpenVisionLanguageService.T("WpfCanvas.Workflow.ImageQueue"),
                WpfLearningStep.Infer => OpenVisionLanguageService.T("WpfShell.Right.Inspect.Text"),
                WpfLearningStep.Review => OpenVisionLanguageService.T("WpfCanvas.WorkflowMode.Inference.Text"),
                _ => string.Empty
            };

            if (!string.IsNullOrWhiteSpace(workflowToolText))
            {
                return workflowToolText;
            }

            return selectedTool.HasValue
                ? FormatAnnotationToolText(selectedTool.Value, selectedToolText)
                : selectedToolText;
        }

        private static string FormatAnnotationToolText(WpfAnnotationTool tool, string fallbackText)
        {
            return tool switch
            {
                WpfAnnotationTool.Select => OpenVisionLanguageService.T("WpfAnnotationTool.Select"),
                WpfAnnotationTool.Rectangle => OpenVisionLanguageService.T("WpfAnnotationTool.Rectangle"),
                WpfAnnotationTool.Ellipse => OpenVisionLanguageService.T("WpfAnnotationTool.Ellipse"),
                WpfAnnotationTool.Polygon => OpenVisionLanguageService.T("WpfAnnotationTool.Polygon"),
                WpfAnnotationTool.SmartMask => OpenVisionLanguageService.T("WpfAnnotationTool.SmartMask"),
                WpfAnnotationTool.Brush => OpenVisionLanguageService.T("WpfAnnotationTool.Brush"),
                WpfAnnotationTool.Eraser => OpenVisionLanguageService.T("WpfAnnotationTool.Eraser"),
                WpfAnnotationTool.PanZoom => OpenVisionLanguageService.T("WpfAnnotationTool.PanZoom"),
                WpfAnnotationTool.Undo => OpenVisionLanguageService.T("WpfAnnotationTool.Undo"),
                WpfAnnotationTool.Redo => OpenVisionLanguageService.T("WpfAnnotationTool.Redo"),
                WpfAnnotationTool.Delete => OpenVisionLanguageService.T("WpfAnnotationTool.Delete"),
                _ => fallbackText
            };
        }

        private static string BuildActionText(
            WpfLearningStep effectiveStep,
            bool hasActiveImage,
            bool hasUnsavedAnnotations,
            bool hasCanvasLabelObjects,
            WpfAnnotationTool? selectedTool,
            LabelingBoxDrawingMethod? selectedBoxDrawingMethod,
            MaskEraserScope eraserScope,
            bool hasSelectedMaskObject,
            bool isSmartMaskCandidateEditActive)
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
                    return OpenVisionLanguageService.T("WpfCanvas.ToolContext.FourPointBox");
                }

                return selectedTool switch
                {
                    WpfAnnotationTool.Rectangle => OpenVisionLanguageService.T("WpfCanvas.ToolContext.Box"),
                    WpfAnnotationTool.Ellipse => OpenVisionLanguageService.T("WpfCanvas.ToolContext.Ellipse"),
                    WpfAnnotationTool.Polygon => OpenVisionLanguageService.T("WpfCanvas.ToolContext.Polygon"),
                    WpfAnnotationTool.SmartMask => OpenVisionLanguageService.T("WpfCanvas.ToolContext.SmartMask"),
                    WpfAnnotationTool.Brush => isSmartMaskCandidateEditActive
                        ? OpenVisionLanguageService.T("WpfCanvas.ToolContext.CandidateBrush")
                        : OpenVisionLanguageService.T("WpfCanvas.ToolContext.Brush"),
                    WpfAnnotationTool.Eraser => BuildEraserActionText(
                        eraserScope,
                        hasSelectedMaskObject,
                        isSmartMaskCandidateEditActive),
                    WpfAnnotationTool.PanZoom => OpenVisionLanguageService.T("WpfCanvas.ToolContext.Pan"),
                    _ when hasUnsavedAnnotations => OpenVisionLanguageService.T("WpfCanvas.Workflow.SaveRequiredAction"),
                    _ when hasCanvasLabelObjects => OpenVisionLanguageService.T("WpfCanvas.Workflow.CheckObjectsAction"),
                    _ => OpenVisionLanguageService.T("WpfCanvas.ToolContext.Select")
                };
            }

            return effectiveStep switch
            {
                WpfLearningStep.Sample => OpenVisionLanguageService.T("WpfCanvas.Workflow.SelectImageAction"),
                WpfLearningStep.Infer => OpenVisionLanguageService.T("WpfCanvas.Workflow.InferAction"),
                WpfLearningStep.Review => OpenVisionLanguageService.T("WpfCanvas.Workflow.ReviewAction"),
                WpfLearningStep.Save when hasUnsavedAnnotations => OpenVisionLanguageService.T("WpfCanvas.Workflow.SaveRequiredAction"),
                WpfLearningStep.Save => OpenVisionLanguageService.T("WpfCanvas.Workflow.SavedAction"),
                _ => OpenVisionLanguageService.T("WpfCanvas.Workflow.NextAction")
            };
        }

        private static string BuildEraserActionText(
            MaskEraserScope eraserScope,
            bool hasSelectedMaskObject,
            bool isSmartMaskCandidateEditActive)
        {
            if (isSmartMaskCandidateEditActive)
            {
                return OpenVisionLanguageService.T("WpfCanvas.ToolContext.CandidateEraser");
            }

            return eraserScope switch
            {
                MaskEraserScope.CurrentClass => OpenVisionLanguageService.T("WpfCanvas.ToolContext.EraserCurrentClass"),
                MaskEraserScope.SelectedObject => hasSelectedMaskObject
                    ? OpenVisionLanguageService.T("WpfCanvas.ToolContext.EraserSelected")
                    : OpenVisionLanguageService.T("WpfCanvas.ToolContext.EraserSelectFirst"),
                _ => OpenVisionLanguageService.T("WpfCanvas.ToolContext.EraserVisible")
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
            LabelingBoxDrawingMethod? selectedBoxDrawingMethod,
            MaskEraserScope eraserScope = MaskEraserScope.VisibleEditable,
            bool hasSelectedMaskObject = false,
            bool isSmartMaskCandidateEditActive = false)
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
                 selectedBoxDrawingMethod,
                 eraserScope,
                 hasSelectedMaskObject,
                 isSmartMaskCandidateEditActive));

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
