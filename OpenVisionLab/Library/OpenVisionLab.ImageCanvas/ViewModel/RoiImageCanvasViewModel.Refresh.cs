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

		private void _dataTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
		{
			_imageViewer.Reshape();
		}
	}
}
