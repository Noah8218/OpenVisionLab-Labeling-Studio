using OpenVisionLab.ImageCanvas.Rendering;
using System;
using System.Windows.Forms;

namespace OpenVisionLab.ImageCanvas.Canvas
{
	internal sealed class RoiImageCanvasInputAdapter
	{
		private readonly ImageCanvasControl imageViewer;

		public RoiImageCanvasInputAdapter(ImageCanvasControl imageViewer)
		{
			this.imageViewer = imageViewer ?? throw new ArgumentNullException(nameof(imageViewer));
			this.imageViewer.Load += OnLoad;
			this.imageViewer.Resized += OnResized;
			this.imageViewer.MouseDoubleClicked += OnMouseDoubleClicked;
			this.imageViewer.KeyDown += OnKeyDown;
			this.imageViewer.KeyUp += OnKeyUp;
			this.imageViewer.MouseClicked += OnMouseClicked;
			this.imageViewer.MouseDown += OnMouseDown;
			this.imageViewer.MouseMove += OnMouseMove;
			this.imageViewer.MouseUp += OnMouseUp;
			this.imageViewer.MouseLeave += OnMouseLeave;
			this.imageViewer.MouseWheel += OnMouseWheel;
		}

		public event EventHandler Load = delegate { };
		public event EventHandler Resized = delegate { };
		public event EventHandler MouseDoubleClicked = delegate { };
		public event EventHandler MouseClicked = delegate { };
		public event EventHandler MouseLeave = delegate { };
		public event EventHandler<CanvasKeyboardEventArgs> KeyDown = delegate { };
		public event EventHandler<CanvasKeyboardEventArgs> KeyUp = delegate { };
		public event EventHandler<CanvasMouseEventArgs> MouseDown = delegate { };
		public event EventHandler<CanvasMouseEventArgs> MouseMove = delegate { };
		public event EventHandler<CanvasMouseEventArgs> MouseUp = delegate { };
		public event EventHandler<CanvasMouseEventArgs> MouseWheel = delegate { };

		private void OnLoad(object sender, EventArgs e) => Load(sender, e);
		private void OnResized(object sender, EventArgs e) => Resized(sender, e);
		private void OnMouseDoubleClicked(object sender, EventArgs e) => MouseDoubleClicked(sender, e);
		private void OnMouseClicked(object sender, EventArgs e) => MouseClicked(sender, e);
		private void OnMouseLeave(object sender, EventArgs e) => MouseLeave(sender, e);
		private void OnMouseDown(object sender, CanvasMouseEventArgs e) => MouseDown(sender, e);
		private void OnMouseMove(object sender, CanvasMouseEventArgs e) => MouseMove(sender, e);
		private void OnMouseUp(object sender, CanvasMouseEventArgs e) => MouseUp(sender, e);
		private void OnMouseWheel(object sender, CanvasMouseEventArgs e) => MouseWheel(sender, e);

		private void OnKeyDown(object sender, KeyEventArgs e)
		{
			CanvasKeyboardEventArgs canvasArgs = CanvasKeyboardEventArgs.FromWinForms(e);
			KeyDown(sender, canvasArgs);
			e.Handled = canvasArgs.Handled;
		}

		private void OnKeyUp(object sender, KeyEventArgs e)
		{
			CanvasKeyboardEventArgs canvasArgs = CanvasKeyboardEventArgs.FromWinForms(e);
			KeyUp(sender, canvasArgs);
			e.Handled = canvasArgs.Handled;
		}
	}
}
