using System;
using System.Windows.Forms;

namespace OpenVisionLab.ImageCanvas.Canvas
{
	public enum CanvasKeyboardKey
	{
		None,
		Shift,
		Control,
		Enter,
		Delete,
		C,
		V
	}

	public sealed class CanvasKeyboardEventArgs : EventArgs
	{
		private CanvasKeyboardEventArgs(CanvasKeyboardKey key, bool isControlPressed)
		{
			Key = key;
			IsControlPressed = isControlPressed;
		}

		public CanvasKeyboardKey Key { get; }

		public bool IsControlPressed { get; }

		public bool Handled { get; set; }

		internal static CanvasKeyboardEventArgs FromWinForms(KeyEventArgs args)
		{
			return new CanvasKeyboardEventArgs(MapKey(args?.KeyCode ?? Keys.None), args?.Modifiers == Keys.Control);
		}

		private static CanvasKeyboardKey MapKey(Keys key)
		{
			return key switch
			{
				Keys.ShiftKey => CanvasKeyboardKey.Shift,
				Keys.ControlKey => CanvasKeyboardKey.Control,
				Keys.Enter => CanvasKeyboardKey.Enter,
				Keys.Delete => CanvasKeyboardKey.Delete,
				Keys.C => CanvasKeyboardKey.C,
				Keys.V => CanvasKeyboardKey.V,
				_ => CanvasKeyboardKey.None
			};
		}
	}
}
