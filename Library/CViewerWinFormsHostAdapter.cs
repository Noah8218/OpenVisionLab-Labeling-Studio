using OpenVisionLab.ImageCanvas.Rendering;
using System;
using System.Windows.Forms;

namespace MvcVisionSystem
{
    public sealed class CViewerWinFormsHostAdapter : IDisposable
    {
        private readonly CViewer viewer;
        private bool disposed;

        public CViewerWinFormsHostAdapter(CViewer viewer, Control host, bool onlyDragMode = false)
        {
            this.viewer = viewer ?? throw new ArgumentNullException(nameof(viewer));
            Host = host ?? throw new ArgumentNullException(nameof(host));
            Canvas = viewer.AttachToWinFormsHost(Host, onlyDragMode);
        }

        public Control Host { get; }

        public ImageCanvasControl Canvas { get; }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            viewer.DetachWinFormsCanvas(Canvas);
        }
    }
}
