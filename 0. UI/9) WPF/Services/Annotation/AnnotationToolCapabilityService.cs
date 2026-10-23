using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem
{
    public static class AnnotationToolCapabilityService
    {
        private static readonly Dictionary<WpfAnnotationTool, AnnotationToolCapability> Capabilities =
            new Dictionary<WpfAnnotationTool, AnnotationToolCapability>
            {
                [WpfAnnotationTool.Select] = Connected(
                    WpfAnnotationTool.Select,
                    "\uC120\uD0DD",
                    OpenVisionLab.OpenVisionLanguageService.T("WpfLearningWorkflow.ToolTip.Select.Capability")),
                [WpfAnnotationTool.Rectangle] = Connected(
                    WpfAnnotationTool.Rectangle,
                    "\uBC15\uC2A4",
                    OpenVisionLab.OpenVisionLanguageService.T("WpfLearningWorkflow.ToolTip.Rectangle.Capability")),
                [WpfAnnotationTool.PanZoom] = Connected(
                    WpfAnnotationTool.PanZoom,
                    "화면 이동",
                    OpenVisionLab.OpenVisionLanguageService.T("WpfLearningWorkflow.ToolTip.PanZoom.Capability")),
                [WpfAnnotationTool.Delete] = Connected(
                    WpfAnnotationTool.Delete,
                    "\uC0AD\uC81C",
                    OpenVisionLab.OpenVisionLanguageService.T("WpfLearningWorkflow.ToolTip.Delete.Capability")),
                [WpfAnnotationTool.Ellipse] = Connected(
                    WpfAnnotationTool.Ellipse,
                    "\uC6D0/\uD0C0\uC6D0",
                    OpenVisionLab.OpenVisionLanguageService.T("WpfLearningWorkflow.ToolTip.Ellipse.Capability")),
                [WpfAnnotationTool.Polygon] = Connected(
                    WpfAnnotationTool.Polygon,
                    "\uD3F4\uB9AC\uACE4",
                    "세그멘테이션 경계를 점으로 찍어 만들고 저장할 수 있습니다."),
                [WpfAnnotationTool.Brush] = Connected(
                    WpfAnnotationTool.Brush,
                    "\uBE0C\uB7EC\uC2DC",
                    OpenVisionLab.OpenVisionLanguageService.T("WpfLearningWorkflow.ToolTip.Brush.Capability")),
                [WpfAnnotationTool.SmartMask] = Connected(
                    WpfAnnotationTool.SmartMask,
                    "스마트 마스크",
                    "스마트 마스크 후보를 만들고 경계를 보정할 수 있습니다."),
                [WpfAnnotationTool.Eraser] = Connected(
                    WpfAnnotationTool.Eraser,
                    "\uC9C0\uC6B0\uAC1C",
                    OpenVisionLab.OpenVisionLanguageService.T("WpfLearningWorkflow.ToolTip.Eraser.Capability")),
                [WpfAnnotationTool.Undo] = Connected(
                    WpfAnnotationTool.Undo,
                    "실행 취소",
                    OpenVisionLab.OpenVisionLanguageService.T("WpfLearningWorkflow.ToolTip.Undo.Capability")),
                [WpfAnnotationTool.Redo] = Connected(
                    WpfAnnotationTool.Redo,
                    "다시 실행",
                    "실행 취소한 편집을 다시 실행합니다.")
            };

        public static AnnotationToolCapability Get(WpfAnnotationTool tool)
            => Capabilities.TryGetValue(tool, out AnnotationToolCapability capability)
                ? capability
                : Pending(tool, tool.ToString(), "아직 사용할 수 없습니다.");

        public static IReadOnlyList<AnnotationToolCapability> GetAll()
            => Capabilities.Values.ToList();

        public static bool IsConnected(WpfAnnotationTool tool)
            => Get(tool).IsConnected;

        private static AnnotationToolCapability Connected(WpfAnnotationTool tool, string displayName, string statusText)
            => new AnnotationToolCapability(tool, displayName, true, "\uAC00\uB2A5", statusText);

        private static AnnotationToolCapability Pending(WpfAnnotationTool tool, string displayName, string statusText)
            => new AnnotationToolCapability(tool, displayName, false, "\uB300\uAE30", statusText);
    }

    public class AnnotationToolCapability
    {
        public AnnotationToolCapability(
            WpfAnnotationTool tool,
            string displayName,
            bool isConnected,
            string stateText,
            string statusText)
        {
            Tool = tool;
            DisplayName = displayName ?? string.Empty;
            IsConnected = isConnected;
            StateText = stateText ?? string.Empty;
            StatusText = statusText ?? string.Empty;
        }

        public WpfAnnotationTool Tool { get; }

        public string DisplayName { get; }

        public bool IsConnected { get; }

        public string StateText { get; }

        public string StatusText { get; }
    }

    [Obsolete("Use AnnotationToolCapabilityService.", false)]
    public static class WpfAnnotationToolCapabilityService
    {
        public static WpfAnnotationToolCapability Get(WpfAnnotationTool tool)
            => new WpfAnnotationToolCapability(AnnotationToolCapabilityService.Get(tool));

        public static IReadOnlyList<WpfAnnotationToolCapability> GetAll()
            => AnnotationToolCapabilityService.GetAll()
                .Select(capability => new WpfAnnotationToolCapability(capability))
                .ToList();

        public static bool IsConnected(WpfAnnotationTool tool)
            => AnnotationToolCapabilityService.IsConnected(tool);
    }

    [Obsolete("Use AnnotationToolCapability.", false)]
    public sealed class WpfAnnotationToolCapability : AnnotationToolCapability
    {
        public WpfAnnotationToolCapability(
            WpfAnnotationTool tool,
            string displayName,
            bool isConnected,
            string stateText,
            string statusText)
            : base(tool, displayName, isConnected, stateText, statusText)
        {
        }

        public WpfAnnotationToolCapability(AnnotationToolCapability capability)
            : this(
                capability?.Tool ?? default,
                capability?.DisplayName,
                capability?.IsConnected == true,
                capability?.StateText,
                capability?.StatusText)
        {
        }
    }
}
