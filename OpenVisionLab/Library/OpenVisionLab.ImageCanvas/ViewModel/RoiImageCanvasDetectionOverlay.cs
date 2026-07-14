using System;
using System.Collections.Generic;
using System.Drawing;

namespace OpenVisionLab.ImageCanvas.ViewModels
{
    public sealed class RoiImageCanvasDetectionOverlay
    {
        public int Index { get; set; } = -1;

        public Rectangle Bounds { get; set; }

        public IReadOnlyList<PointF> ContourPoints { get; set; } = Array.Empty<PointF>();

        public bool IsContourOnly { get; set; }

        public string Label { get; set; } = string.Empty;

        public Color Color { get; set; } = Color.DeepSkyBlue;

        public bool IsSelected { get; set; }
    }
}
