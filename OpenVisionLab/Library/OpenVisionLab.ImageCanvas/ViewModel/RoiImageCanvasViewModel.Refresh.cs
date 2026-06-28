using System;

namespace OpenVisionLab.ImageCanvas.ViewModels
{
	public partial class RoiImageCanvasViewModel
	{
		public void StartDrawingTimer()
		{
			if (_refreshTimer == null) { return; }
			_refreshTimer.Stop();
			_refreshTimer.Start();
		}

		public void StartReshapeTimer()
		{
			if (_reshapeTimer == null) { return; }
			_reshapeTimer.Stop();
			_reshapeTimer.Start();
		}

		private void _refreshTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
		{
			// Brush hover and FBO stroke preview need only a frame refresh. Calling
			// Reshape here invalidates the visible overlay cache and can re-walk huge ROI sets.
			_imageViewer.RefreshTransientOverlayGL();
		}

		private void _reshapeTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
		{
			_imageViewer.Reshape();
		}
	}
}
