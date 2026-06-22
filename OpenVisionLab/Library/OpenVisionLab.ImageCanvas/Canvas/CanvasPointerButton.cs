using System.Windows.Forms;

namespace OpenVisionLab.ImageCanvas.Canvas
{
	public enum CanvasPointerButton
	{
		None,
		Left,
		Middle,
		Right
	}

	internal static class CanvasPointerButtonMapper
	{
		public static CanvasPointerButton FromWinForms(MouseButtons button)
		{
			if ((button & MouseButtons.Left) == MouseButtons.Left)
			{
				return CanvasPointerButton.Left;
			}

			if ((button & MouseButtons.Right) == MouseButtons.Right)
			{
				return CanvasPointerButton.Right;
			}

			if ((button & MouseButtons.Middle) == MouseButtons.Middle)
			{
				return CanvasPointerButton.Middle;
			}

			return CanvasPointerButton.None;
		}
	}
}
