using SharpGL;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace OpenVisionLab.ImageCanvas.Rendering
{
	internal sealed class ImageCanvasOpenGlHostAdapter : IDisposable
	{
		private readonly EventHandler loadHandler;
		private readonly EventHandler resizedHandler;
		private readonly MouseEventHandler mouseDoubleClickHandler;
		private readonly RenderEventHandler drawHandler;
		private readonly KeyEventHandler keyUpHandler;
		private readonly KeyEventHandler keyDownHandler;
		private readonly MouseEventHandler mouseClickHandler;
		private readonly MouseEventHandler mouseDownHandler;
		private readonly MouseEventHandler mouseMoveHandler;
		private readonly MouseEventHandler mouseUpHandler;
		private readonly MouseEventHandler mouseWheelHandler;
		private readonly EventHandler mouseLeaveHandler;
		private bool disposed;

		public ImageCanvasOpenGlHostAdapter(
			EventHandler loadHandler,
			EventHandler resizedHandler,
			MouseEventHandler mouseDoubleClickHandler,
			RenderEventHandler drawHandler,
			KeyEventHandler keyUpHandler,
			KeyEventHandler keyDownHandler,
			MouseEventHandler mouseClickHandler,
			MouseEventHandler mouseDownHandler,
			MouseEventHandler mouseMoveHandler,
			MouseEventHandler mouseUpHandler,
			MouseEventHandler mouseWheelHandler,
			EventHandler mouseLeaveHandler)
		{
			this.loadHandler = loadHandler ?? throw new ArgumentNullException(nameof(loadHandler));
			this.resizedHandler = resizedHandler ?? throw new ArgumentNullException(nameof(resizedHandler));
			this.mouseDoubleClickHandler = mouseDoubleClickHandler ?? throw new ArgumentNullException(nameof(mouseDoubleClickHandler));
			this.drawHandler = drawHandler ?? throw new ArgumentNullException(nameof(drawHandler));
			this.keyUpHandler = keyUpHandler ?? throw new ArgumentNullException(nameof(keyUpHandler));
			this.keyDownHandler = keyDownHandler ?? throw new ArgumentNullException(nameof(keyDownHandler));
			this.mouseClickHandler = mouseClickHandler ?? throw new ArgumentNullException(nameof(mouseClickHandler));
			this.mouseDownHandler = mouseDownHandler ?? throw new ArgumentNullException(nameof(mouseDownHandler));
			this.mouseMoveHandler = mouseMoveHandler ?? throw new ArgumentNullException(nameof(mouseMoveHandler));
			this.mouseUpHandler = mouseUpHandler ?? throw new ArgumentNullException(nameof(mouseUpHandler));
			this.mouseWheelHandler = mouseWheelHandler ?? throw new ArgumentNullException(nameof(mouseWheelHandler));
			this.mouseLeaveHandler = mouseLeaveHandler ?? throw new ArgumentNullException(nameof(mouseLeaveHandler));

			Control = CreateControl();
			AttachEvents();
		}

		public OpenGLControl Control { get; }

		public void Dispose()
		{
			if (disposed)
			{
				return;
			}

			disposed = true;
			DetachEvents();
		}

		private static OpenGLControl CreateControl()
		{
			var control = new OpenGLControl();
			((ISupportInitialize)control).BeginInit();
			control.AutoSize = true;
			control.Dock = DockStyle.Fill;
			control.DrawFPS = false;
			control.FrameRate = 28;
			control.Location = new Point(0, 0);
			control.Margin = new Padding(6, 5, 6, 5);
			control.Name = "openGLControl";
			control.OpenGLVersion = SharpGL.Version.OpenGLVersion.OpenGL2_1;
			control.RenderContextType = RenderContextType.NativeWindow;
			control.RenderTrigger = RenderTrigger.Manual;
			control.Size = new Size(859, 562);
			control.TabIndex = 0;
			((ISupportInitialize)control).EndInit();
			return control;
		}

		private void AttachEvents()
		{
			Control.Load += loadHandler;
			Control.Resized += resizedHandler;
			Control.MouseDoubleClick += mouseDoubleClickHandler;
			Control.OpenGLDraw += drawHandler;
			Control.KeyUp += keyUpHandler;
			Control.KeyDown += keyDownHandler;
			Control.MouseClick += mouseClickHandler;
			Control.MouseDown += mouseDownHandler;
			Control.MouseMove += mouseMoveHandler;
			Control.MouseUp += mouseUpHandler;
			Control.MouseWheel += mouseWheelHandler;
			Control.MouseLeave += mouseLeaveHandler;
		}

		private void DetachEvents()
		{
			Control.Load -= loadHandler;
			Control.Resized -= resizedHandler;
			Control.MouseDoubleClick -= mouseDoubleClickHandler;
			Control.OpenGLDraw -= drawHandler;
			Control.KeyUp -= keyUpHandler;
			Control.KeyDown -= keyDownHandler;
			Control.MouseClick -= mouseClickHandler;
			Control.MouseDown -= mouseDownHandler;
			Control.MouseMove -= mouseMoveHandler;
			Control.MouseUp -= mouseUpHandler;
			Control.MouseWheel -= mouseWheelHandler;
			Control.MouseLeave -= mouseLeaveHandler;
		}
	}
}
