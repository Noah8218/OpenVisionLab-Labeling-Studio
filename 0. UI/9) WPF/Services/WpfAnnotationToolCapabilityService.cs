using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem
{
    public static class WpfAnnotationToolCapabilityService
    {
        private static readonly Dictionary<WpfAnnotationTool, WpfAnnotationToolCapability> Capabilities =
            new Dictionary<WpfAnnotationTool, WpfAnnotationToolCapability>
            {
                [WpfAnnotationTool.Select] = Connected(
                    WpfAnnotationTool.Select,
                    "\uC120\uD0DD",
                    "WPF \uAC1D\uCCB4 \uC120\uD0DD/\uD3B8\uC9D1 \uBAA8\uB4DC\uB85C \uC804\uD658\uB429\uB2C8\uB2E4."),
                [WpfAnnotationTool.Rectangle] = Connected(
                    WpfAnnotationTool.Rectangle,
                    "\uBC15\uC2A4",
                    "WPF OpenGL ROI \uB4DC\uB85C\uC789 \uACBD\uB85C\uC5D0 \uC5F0\uACB0\uB418\uC5B4 \uC788\uC2B5\uB2C8\uB2E4."),
                [WpfAnnotationTool.PanZoom] = Connected(
                    WpfAnnotationTool.PanZoom,
                    "\uC774\uB3D9",
                    "WPF OpenGL \uBDF0\uC5B4 Pan \uACBD\uB85C\uC5D0 \uC5F0\uACB0\uB418\uC5B4 \uC788\uC2B5\uB2C8\uB2E4."),
                [WpfAnnotationTool.Delete] = Connected(
                    WpfAnnotationTool.Delete,
                    "\uC0AD\uC81C",
                    "\uD604\uC7AC \uAC1D\uCCB4 \uAC80\uD1A0 \uC120\uD0DD \uD56D\uBAA9 \uC0AD\uC81C \uACBD\uB85C\uC5D0 \uC5F0\uACB0\uB418\uC5B4 \uC788\uC2B5\uB2C8\uB2E4."),
                [WpfAnnotationTool.Ellipse] = Connected(
                    WpfAnnotationTool.Ellipse,
                    "\uC6D0/\uD0C0\uC6D0",
                    "\uC774\uBBF8\uC9C0 \uD53D\uC140 \uAE30\uC900 bounding box\uB85C \uC6D0/\uD0C0\uC6D0\uC744 \uADF8\uB9AC\uACE0 YOLO\uB294 \uD574\uB2F9 box\uB85C \uC800\uC7A5\uD569\uB2C8\uB2E4."),
                [WpfAnnotationTool.Polygon] = Connected(
                    WpfAnnotationTool.Polygon,
                    "\uD3F4\uB9AC\uACE4",
                    "\uC774\uBBF8\uC9C0 \uD53D\uC140 \uD074\uB9AD \uC785\uB825, OpenGL \uD504\uB9AC\uBDF0, \uC138\uADF8\uBA58\uD14C\uC774\uC158 \uC800\uC7A5 \uACBD\uB85C\uC5D0 \uC5F0\uACB0\uB418\uC5B4 \uC788\uC2B5\uB2C8\uB2E4."),
                [WpfAnnotationTool.Brush] = Connected(
                    WpfAnnotationTool.Brush,
                    "\uBE0C\uB7EC\uC2DC",
                    "\uC774\uBBF8\uC9C0 \uD53D\uC140 \uAE30\uC900 raster mask \uBE0C\uB7EC\uC2DC \uD3B8\uC9D1, OpenGL \uD504\uB9AC\uBDF0, \uC138\uADF8\uBA58\uD14C\uC774\uC158 \uC800\uC7A5 \uACBD\uB85C\uC5D0 \uC5F0\uACB0\uB418\uC5B4 \uC788\uC2B5\uB2C8\uB2E4."),
                [WpfAnnotationTool.Eraser] = Connected(
                    WpfAnnotationTool.Eraser,
                    "\uC9C0\uC6B0\uAC1C",
                    "\uC774\uBBF8\uC9C0 \uD53D\uC140 \uAE30\uC900 raster mask \uC9C0\uC6B0\uAC1C, OpenGL \uD504\uB9AC\uBDF0, \uC138\uADF8\uBA58\uD14C\uC774\uC158 \uC800\uC7A5 \uACBD\uB85C\uC5D0 \uC5F0\uACB0\uB418\uC5B4 \uC788\uC2B5\uB2C8\uB2E4."),
                [WpfAnnotationTool.Undo] = Connected(
                    WpfAnnotationTool.Undo,
                    "Undo",
                    "WPF ROI, AI \uD6C4\uBCF4, polygon, mask \uD3B8\uC9D1 \uC2A4\uB0C5\uC0F7 \uC774\uB825\uC744 \uD1B5\uD574 \uC9C1\uC804 \uBCC0\uACBD\uC744 \uB418\uB3CC\uB9BD\uB2C8\uB2E4."),
                [WpfAnnotationTool.Redo] = Connected(
                    WpfAnnotationTool.Redo,
                    "Redo",
                    "WPF \uD3B8\uC9D1 \uC2A4\uB0C5\uC0F7 \uC774\uB825\uC5D0\uC11C Undo\uB85C \uB418\uB3CC\uB9B0 \uBCC0\uACBD\uC744 \uB2E4\uC2DC \uC801\uC6A9\uD569\uB2C8\uB2E4.")
            };

        public static WpfAnnotationToolCapability Get(WpfAnnotationTool tool)
            => Capabilities.TryGetValue(tool, out WpfAnnotationToolCapability capability)
                ? capability
                : Pending(tool, tool.ToString(), "\uD604\uC7AC WPF \uACBD\uB85C \uAC80\uC99D \uC804\uC785\uB2C8\uB2E4.");

        public static IReadOnlyList<WpfAnnotationToolCapability> GetAll()
            => Capabilities.Values.ToList();

        public static bool IsConnected(WpfAnnotationTool tool)
            => Get(tool).IsConnected;

        private static WpfAnnotationToolCapability Connected(WpfAnnotationTool tool, string displayName, string statusText)
            => new WpfAnnotationToolCapability(tool, displayName, true, "\uAC00\uB2A5", statusText);

        private static WpfAnnotationToolCapability Pending(WpfAnnotationTool tool, string displayName, string statusText)
            => new WpfAnnotationToolCapability(tool, displayName, false, "\uB300\uAE30", statusText);
    }

    public sealed class WpfAnnotationToolCapability
    {
        public WpfAnnotationToolCapability(
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
}
