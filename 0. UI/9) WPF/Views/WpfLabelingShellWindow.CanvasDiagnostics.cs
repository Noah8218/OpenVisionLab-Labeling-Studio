using OpenVisionLab.ImageCanvas.ViewModels;
using System;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        private void MainCanvasViewModel_RenderDiagnosticsCaptured(object sender, RoiImageCanvasRenderDiagnosticsEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            AppendLog(FormattableString.Invariant(
                $"GL frame after {e.Reason}: wait {e.WaitMilliseconds:F1}ms / draw {e.DrawMilliseconds:F1}ms / content {e.ContentMilliseconds:F1}ms / mask {e.MaskMilliseconds:F1}ms / detection {e.DetectionMilliseconds:F1}ms / polygon {e.PolygonMilliseconds:F1}ms / misc {e.MiscMilliseconds:F1}ms / preview={e.UsedMaskPreview} / masks={e.MaskOverlayCount} / pendingPreview={e.PendingPreviewCommandCount}"));
        }
    }
}
