using MvcVisionSystem.Yolo;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace MvcVisionSystem
{
    public sealed class LabelingSegmentationObject
    {
        public LabelingSegmentationObject()
        {
        }

        public LabelingSegmentationObject(IEnumerable<Point> points, CClassItem classItem)
        {
            Points = points?.ToList() ?? new List<Point>();
            ClassItem = classItem;
            ClassName = classItem?.Text ?? string.Empty;
        }

        public string ClassName { get; set; } = string.Empty;

        public CClassItem ClassItem { get; set; }

        public List<Point> Points { get; set; } = new List<Point>();

        public List<List<Point>> CutoutPolygons { get; set; } = new List<List<Point>>();

        public byte[] MaskData { get; set; }

        public Size MaskSize { get; set; }

        public Rectangle MaskBounds { get; set; }

        public int RenderVersion { get; set; }

        public Rectangle RenderDirtyBounds { get; set; }

        public bool IsRasterMask => MaskData != null
            && MaskSize.Width > 0
            && MaskSize.Height > 0
            && MaskData.Length == MaskSize.Width * MaskSize.Height;

        public bool Selected { get; set; }

        public Color Color => ClassItem?.DrawColor ?? Color.LimeGreen;

        public Rectangle Bounds => IsRasterMask
            ? (!MaskBounds.IsEmpty ? MaskBounds : SegmentationGeometry.GetMaskBounds(MaskData, MaskSize))
            : SegmentationGeometry.GetBounds(Points);
    }
}
