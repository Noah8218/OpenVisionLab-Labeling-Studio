using System;
using OpenVisionLab.ImageCanvas.CanvasShapes;

namespace MvcVisionSystem
{
    public class AnnotationToolWorkflowAction
    {
        protected AnnotationToolWorkflowAction(
            WpfAnnotationTool tool,
            AnnotationToolCapability capability,
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

        public AnnotationToolCapability Capability { get; }

        public WpfAnnotationToolWorkflowActionKind Kind { get; }

        public CanvasRoiShapeKind ShapeKind { get; }

        public string ModelStatusText { get; }

        public string CommandStatusText { get; }

        public string LogText { get; }

        public static AnnotationToolWorkflowAction Pending(WpfAnnotationTool tool, AnnotationToolCapability capability)
            => new AnnotationToolWorkflowAction(tool, capability, WpfAnnotationToolWorkflowActionKind.Pending, CanvasRoiShapeKind.Rectangle, string.Empty, string.Empty, string.Empty);

        public static AnnotationToolWorkflowAction DrawRoi(
            WpfAnnotationTool tool,
            AnnotationToolCapability capability,
            CanvasRoiShapeKind shapeKind,
            string modelStatusText,
            string commandStatusText,
            string logText)
            => new AnnotationToolWorkflowAction(tool, capability, WpfAnnotationToolWorkflowActionKind.DrawRoi, shapeKind, modelStatusText, commandStatusText, logText);

        public static AnnotationToolWorkflowAction Simple(
            WpfAnnotationTool tool,
            AnnotationToolCapability capability,
            WpfAnnotationToolWorkflowActionKind kind)
            => new AnnotationToolWorkflowAction(tool, capability, kind, CanvasRoiShapeKind.Rectangle, string.Empty, string.Empty, string.Empty);
    }

    [Obsolete("Use AnnotationToolWorkflowAction.", false)]
    public sealed class WpfAnnotationToolWorkflowAction : AnnotationToolWorkflowAction
    {
        private WpfAnnotationToolWorkflowAction(
            WpfAnnotationTool tool,
            WpfAnnotationToolCapability capability,
            WpfAnnotationToolWorkflowActionKind kind,
            CanvasRoiShapeKind shapeKind,
            string modelStatusText,
            string commandStatusText,
            string logText)
            : base(tool, capability, kind, shapeKind, modelStatusText, commandStatusText, logText)
        {
        }

        public new WpfAnnotationToolCapability Capability => (WpfAnnotationToolCapability)base.Capability;

        public static WpfAnnotationToolWorkflowAction From(AnnotationToolWorkflowAction action)
        {
            if (action is WpfAnnotationToolWorkflowAction compatibilityAction)
            {
                return compatibilityAction;
            }

            return new WpfAnnotationToolWorkflowAction(
                action.Tool,
                new WpfAnnotationToolCapability(action.Capability),
                action.Kind,
                action.ShapeKind,
                action.ModelStatusText,
                action.CommandStatusText,
                action.LogText);
        }

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
