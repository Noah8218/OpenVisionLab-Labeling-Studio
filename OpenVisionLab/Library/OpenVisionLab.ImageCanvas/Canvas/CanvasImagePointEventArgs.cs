using System.Drawing;

namespace OpenVisionLab.ImageCanvas.Canvas
{
	public sealed class CanvasImagePointEventArgs
	{
		public CanvasImagePointEventArgs(
			CanvasPointerButton button,
			int clicks,
			int screenX,
			int screenY,
			Point imagePoint,
			PointF canvasPoint)
		{
			Button = button;
			Clicks = clicks;
			ScreenX = screenX;
			ScreenY = screenY;
			ImagePoint = imagePoint;
			CanvasPoint = canvasPoint;
		}

		public CanvasPointerButton Button { get; }

		public int Clicks { get; }

		public int ScreenX { get; }

		public int ScreenY { get; }

		public Point ImagePoint { get; }

		public PointF CanvasPoint { get; }
	}
}
