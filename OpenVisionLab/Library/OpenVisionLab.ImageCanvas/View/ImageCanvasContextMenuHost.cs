using OpenVisionLab.ImageCanvas.Infrastructure;
using System;
using System.Windows.Controls;

namespace OpenVisionLab.ImageCanvas.Views
{
	internal sealed class ImageCanvasContextMenuHost : IImageCanvasContextMenuHost
	{
		private readonly ContextMenu _contextMenu;

		public ImageCanvasContextMenuHost(ContextMenu contextMenu)
		{
			_contextMenu = contextMenu ?? throw new ArgumentNullException(nameof(contextMenu));
		}

		public void Show()
		{
			_contextMenu.IsOpen = true;
		}
	}
}
