using System.Windows.Forms;

namespace OpenVisionLab.ImageCanvas.Canvas
{
	public sealed class CanvasMouseEventArgs : MouseEventArgs
	{
		public CanvasMouseEventArgs(MouseButtons button, int clicks, int x, int y, int delta)
			: base(button, clicks, x, y, delta)
		{
			CanvasButton = CanvasPointerButtonMapper.FromWinForms(button);
		}

		public CanvasPointerButton CanvasButton { get; }
	}
}
