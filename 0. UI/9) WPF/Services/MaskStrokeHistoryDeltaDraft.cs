using MvcVisionSystem.Yolo;
using System;

namespace MvcVisionSystem
{
    internal sealed class MaskStrokeHistoryDeltaDraft
    {
        private MaskStrokeHistoryDeltaDraft()
        {
        }

        public static MaskStrokeHistoryDeltaDraft CreatedSegment(
            int segmentIndex,
            System.Drawing.Size maskSize,
            string className,
            CClassItem classItem)
            => new MaskStrokeHistoryDeltaDraft
            {
                SegmentIndex = segmentIndex,
                RestoreBounds = System.Drawing.Rectangle.Empty,
                Pixels = Array.Empty<byte>(),
                MaskSize = maskSize,
                MaskBounds = System.Drawing.Rectangle.Empty,
                RenderVersion = 0,
                RenderDirtyBounds = System.Drawing.Rectangle.Empty,
                ClassName = className ?? string.Empty,
                ClassItem = CloneClassItem(classItem),
                Selected = false,
                RemoveCreatedSegment = true,
                RestoreIfRemoved = false
            };

        public static MaskStrokeHistoryDeltaDraft Patch(
            int segmentIndex,
            LabelingSegmentationObject segment,
            System.Drawing.Rectangle restoreBounds,
            byte[] pixels,
            bool restoreIfRemoved)
            => new MaskStrokeHistoryDeltaDraft
            {
                SegmentIndex = segmentIndex,
                Segment = segment,
                RestoreBounds = restoreBounds,
                Pixels = pixels ?? Array.Empty<byte>(),
                MaskSize = segment?.MaskSize ?? System.Drawing.Size.Empty,
                MaskBounds = segment?.MaskBounds ?? System.Drawing.Rectangle.Empty,
                RenderVersion = segment?.RenderVersion ?? 0,
                RenderDirtyBounds = segment?.RenderDirtyBounds ?? System.Drawing.Rectangle.Empty,
                ClassName = segment?.ClassName ?? string.Empty,
                ClassItem = CloneClassItem(segment?.ClassItem),
                Selected = segment?.Selected == true,
                RestoreIfRemoved = restoreIfRemoved
            };

        public int SegmentIndex { get; private set; }

        public LabelingSegmentationObject Segment { get; private set; }

        public System.Drawing.Rectangle RestoreBounds { get; private set; }

        public byte[] Pixels { get; private set; }

        public System.Drawing.Size MaskSize { get; private set; }

        public System.Drawing.Rectangle MaskBounds { get; private set; }

        public int RenderVersion { get; private set; }

        public System.Drawing.Rectangle RenderDirtyBounds { get; private set; }

        public string ClassName { get; private set; }

        public CClassItem ClassItem { get; private set; }

        public bool Selected { get; private set; }

        public bool RemoveCreatedSegment { get; private set; }

        public bool RestoreIfRemoved { get; private set; }

        private static CClassItem CloneClassItem(CClassItem source)
            => source == null
                ? null
                : new CClassItem
                {
                    Text = source.Text ?? string.Empty,
                    DrawColor = source.DrawColor
                };
    }
}
