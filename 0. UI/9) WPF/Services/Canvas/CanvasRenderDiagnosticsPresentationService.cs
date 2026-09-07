using OpenVisionLab.ImageCanvas.ViewModels;
using System;

namespace MvcVisionSystem
{
    /// <summary>
    /// Formats canvas render diagnostics without owning the canvas event lifetime.
    /// </summary>
    public static class CanvasRenderDiagnosticsPresentationService
    {
        public static string BuildLogMessage(RoiImageCanvasRenderDiagnosticsEventArgs diagnostics)
        {
            if (diagnostics == null)
            {
                return string.Empty;
            }

            return FormattableString.Invariant(
                $"GL frame after {diagnostics.Reason}: wait {diagnostics.WaitMilliseconds:F1}ms / draw {diagnostics.DrawMilliseconds:F1}ms / content {diagnostics.ContentMilliseconds:F1}ms / mask {diagnostics.MaskMilliseconds:F1}ms / detection {diagnostics.DetectionMilliseconds:F1}ms / polygon {diagnostics.PolygonMilliseconds:F1}ms / misc {diagnostics.MiscMilliseconds:F1}ms / preview={diagnostics.UsedMaskPreview} / masks={diagnostics.MaskOverlayCount} / pendingPreview={diagnostics.PendingPreviewCommandCount}");
        }
    }

    [Obsolete("Use CanvasRenderDiagnosticsPresentationService.", false)]
    public static class WpfCanvasRenderDiagnosticsPresentationService
    {
        public static string BuildLogMessage(RoiImageCanvasRenderDiagnosticsEventArgs diagnostics)
            => CanvasRenderDiagnosticsPresentationService.BuildLogMessage(diagnostics);
    }
}
