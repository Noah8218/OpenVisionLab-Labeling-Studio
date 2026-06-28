using OpenVisionLab.ImageCanvas.CanvasShapes;

namespace MvcVisionSystem
{
    public static class WpfAnnotationWorkflowService
    {
        public static WpfLearningModeWorkflowAction ResolveModeAction(WpfLearningMode mode)
        {
            return mode switch
            {
                WpfLearningMode.Infer => WpfLearningModeWorkflowAction.Inference,
                WpfLearningMode.Review => WpfLearningModeWorkflowAction.Inference,
                WpfLearningMode.Train => WpfLearningModeWorkflowAction.LabelingAndFocusYoloSettings,
                _ => WpfLearningModeWorkflowAction.Labeling
            };
        }

        public static WpfLearningStepWorkflowAction ResolveStepAction(WpfLearningStep step)
        {
            return step switch
            {
                WpfLearningStep.Sample => WpfLearningStepWorkflowAction.LoadSample,
                WpfLearningStep.Label => WpfLearningStepWorkflowAction.StartBoxLabeling,
                WpfLearningStep.Infer => WpfLearningStepWorkflowAction.Inference,
                WpfLearningStep.Review => WpfLearningStepWorkflowAction.ShowCandidateReview,
                WpfLearningStep.Save => WpfLearningStepWorkflowAction.SaveAnnotations,
                _ => WpfLearningStepWorkflowAction.None
            };
        }

        public static WpfAnnotationToolWorkflowAction ResolveToolAction(WpfAnnotationTool tool)
        {
            // Keep palette decision data here so the shell only applies a resolved workflow action.
            WpfAnnotationToolCapability capability = WpfAnnotationToolCapabilityService.Get(tool);
            if (!capability.IsConnected)
            {
                return WpfAnnotationToolWorkflowAction.Pending(tool, capability);
            }

            return tool switch
            {
                WpfAnnotationTool.Rectangle => WpfAnnotationToolWorkflowAction.DrawRoi(
                    tool,
                    capability,
                    CanvasRoiShapeKind.Rectangle,
                    "\uB3C4\uAD6C: \uBC15\uC2A4 \uB77C\uBCA8\uB9C1",
                    "\uBC15\uC2A4 \uB77C\uBCA8\uB9C1 \uB3C4\uAD6C\uAC00 \uD65C\uC131\uD654\uB418\uC5C8\uC2B5\uB2C8\uB2E4. \uC774\uBBF8\uC9C0 \uC704\uC5D0\uC11C \uB4DC\uB798\uADF8\uD574 \uAC1D\uCCB4 \uC601\uC5ED\uC744 \uB9CC\uB4DC\uC138\uC694.",
                    "\uBC15\uC2A4 \uB77C\uBCA8\uB9C1 \uB3C4\uAD6C"),
                WpfAnnotationTool.Ellipse => WpfAnnotationToolWorkflowAction.DrawRoi(
                    tool,
                    capability,
                    CanvasRoiShapeKind.Ellipse,
                    "\uB3C4\uAD6C: \uC6D0/\uD0C0\uC6D0",
                    "\uC6D0/\uD0C0\uC6D0 \uB77C\uBCA8\uC744 \uADF8\uB9B4 \uC218 \uC788\uC2B5\uB2C8\uB2E4. \uC800\uC7A5 \uC2DC \uC774\uBBF8\uC9C0 \uC704\uCE58\uC640 \uD06C\uAE30\uB97C \uD568\uAED8 \uAE30\uB85D\uD569\uB2C8\uB2E4.",
                    "\uC6D0/\uD0C0\uC6D0 \uB77C\uBCA8\uB9C1 \uB3C4\uAD6C"),
                WpfAnnotationTool.Polygon => WpfAnnotationToolWorkflowAction.Simple(tool, capability, WpfAnnotationToolWorkflowActionKind.Polygon),
                WpfAnnotationTool.Brush => WpfAnnotationToolWorkflowAction.Simple(tool, capability, WpfAnnotationToolWorkflowActionKind.Brush),
                WpfAnnotationTool.Eraser => WpfAnnotationToolWorkflowAction.Simple(tool, capability, WpfAnnotationToolWorkflowActionKind.Eraser),
                WpfAnnotationTool.PanZoom => WpfAnnotationToolWorkflowAction.Simple(tool, capability, WpfAnnotationToolWorkflowActionKind.PanZoom),
                WpfAnnotationTool.Delete => WpfAnnotationToolWorkflowAction.Simple(tool, capability, WpfAnnotationToolWorkflowActionKind.Delete),
                WpfAnnotationTool.Undo => WpfAnnotationToolWorkflowAction.Simple(tool, capability, WpfAnnotationToolWorkflowActionKind.Undo),
                WpfAnnotationTool.Redo => WpfAnnotationToolWorkflowAction.Simple(tool, capability, WpfAnnotationToolWorkflowActionKind.Redo),
                WpfAnnotationTool.Select => WpfAnnotationToolWorkflowAction.Simple(tool, capability, WpfAnnotationToolWorkflowActionKind.Select),
                _ => WpfAnnotationToolWorkflowAction.Simple(tool, capability, WpfAnnotationToolWorkflowActionKind.LabelingFallback)
            };
        }
    }

    public enum WpfLearningModeWorkflowAction
    {
        None,
        Labeling,
        Inference,
        LabelingAndFocusYoloSettings
    }

    public enum WpfLearningStepWorkflowAction
    {
        None,
        LoadSample,
        StartBoxLabeling,
        Inference,
        ShowCandidateReview,
        SaveAnnotations
    }

    public enum WpfAnnotationToolWorkflowActionKind
    {
        Pending,
        Select,
        DrawRoi,
        Polygon,
        Brush,
        Eraser,
        PanZoom,
        Delete,
        Undo,
        Redo,
        LabelingFallback
    }

    public sealed class WpfAnnotationToolWorkflowAction
    {
        private WpfAnnotationToolWorkflowAction(
            WpfAnnotationTool tool,
            WpfAnnotationToolCapability capability,
            WpfAnnotationToolWorkflowActionKind kind,
            CanvasRoiShapeKind shapeKind,
            string modelStatusText,
            string commandStatusText,
            string logText)
        {
            Tool = tool;
            Capability = capability;
            Kind = kind;
            ShapeKind = shapeKind;
            ModelStatusText = modelStatusText ?? string.Empty;
            CommandStatusText = commandStatusText ?? string.Empty;
            LogText = logText ?? string.Empty;
        }

        public WpfAnnotationTool Tool { get; }

        public WpfAnnotationToolCapability Capability { get; }

        public WpfAnnotationToolWorkflowActionKind Kind { get; }

        public CanvasRoiShapeKind ShapeKind { get; }

        public string ModelStatusText { get; }

        public string CommandStatusText { get; }

        public string LogText { get; }

        public static WpfAnnotationToolWorkflowAction Pending(WpfAnnotationTool tool, WpfAnnotationToolCapability capability)
            => new WpfAnnotationToolWorkflowAction(tool, capability, WpfAnnotationToolWorkflowActionKind.Pending, CanvasRoiShapeKind.Rectangle, string.Empty, string.Empty, string.Empty);

        public static WpfAnnotationToolWorkflowAction DrawRoi(
            WpfAnnotationTool tool,
            WpfAnnotationToolCapability capability,
            CanvasRoiShapeKind shapeKind,
            string modelStatusText,
            string commandStatusText,
            string logText)
            => new WpfAnnotationToolWorkflowAction(tool, capability, WpfAnnotationToolWorkflowActionKind.DrawRoi, shapeKind, modelStatusText, commandStatusText, logText);

        public static WpfAnnotationToolWorkflowAction Simple(
            WpfAnnotationTool tool,
            WpfAnnotationToolCapability capability,
            WpfAnnotationToolWorkflowActionKind kind)
            => new WpfAnnotationToolWorkflowAction(tool, capability, kind, CanvasRoiShapeKind.Rectangle, string.Empty, string.Empty, string.Empty);
    }
}
