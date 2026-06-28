using System;

namespace OpenVisionLab.ImageCanvas.ViewModels
{
	public sealed class RoiImageCanvasRenderDiagnosticsEventArgs : EventArgs
	{
		public RoiImageCanvasRenderDiagnosticsEventArgs(
			string reason,
			double waitMilliseconds,
			double drawMilliseconds,
			double contentMilliseconds,
			double maskMilliseconds,
			double detectionMilliseconds,
			double polygonMilliseconds,
			double miscMilliseconds,
			bool usedMaskPreview,
			int maskOverlayCount,
			int pendingPreviewCommandCount)
		{
			Reason = string.IsNullOrWhiteSpace(reason) ? "render" : reason;
			WaitMilliseconds = waitMilliseconds;
			DrawMilliseconds = drawMilliseconds;
			ContentMilliseconds = contentMilliseconds;
			MaskMilliseconds = maskMilliseconds;
			DetectionMilliseconds = detectionMilliseconds;
			PolygonMilliseconds = polygonMilliseconds;
			MiscMilliseconds = miscMilliseconds;
			UsedMaskPreview = usedMaskPreview;
			MaskOverlayCount = maskOverlayCount;
			PendingPreviewCommandCount = pendingPreviewCommandCount;
		}

		public string Reason { get; }

		public double WaitMilliseconds { get; }

		public double DrawMilliseconds { get; }

		public double ContentMilliseconds { get; }

		public double MaskMilliseconds { get; }

		public double DetectionMilliseconds { get; }

		public double PolygonMilliseconds { get; }

		public double MiscMilliseconds { get; }

		public bool UsedMaskPreview { get; }

		public int MaskOverlayCount { get; }

		public int PendingPreviewCommandCount { get; }
	}
}
