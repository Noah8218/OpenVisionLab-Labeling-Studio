using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace MvcVisionSystem
{
    public class SegmentationMergeService
    {
        public const string StructuralOperationName = "Merge";

        public bool TryMerge(
            IReadOnlyList<LabelingSegmentationObject> segments,
            IEnumerable<int> selectedIndices,
            Size imageSize,
            out WpfSegmentationMergeResult result,
            out string error)
        {
            result = null;
            error = string.Empty;
            if (segments == null || imageSize.Width <= 0 || imageSize.Height <= 0)
            {
                error = "활성 이미지와 세그먼트가 필요합니다.";
                return false;
            }

            List<int> indices = (selectedIndices ?? Enumerable.Empty<int>())
                .Distinct()
                .OrderBy(index => index)
                .ToList();
            if (indices.Count < 2)
            {
                error = "같은 클래스 세그먼트를 2개 이상 선택하세요.";
                return false;
            }

            if (indices.Any(index => index < 0 || index >= segments.Count || segments[index] == null))
            {
                error = "병합할 세그먼트 선택이 현재 목록과 일치하지 않습니다.";
                return false;
            }

            List<LabelingSegmentationObject> sources = indices.Select(index => segments[index]).ToList();
            string className = ResolveClassName(sources[0]);
            if (sources.Any(source => !string.Equals(
                ResolveClassName(source),
                className,
                StringComparison.OrdinalIgnoreCase)))
            {
                error = "같은 클래스 세그먼트만 병합할 수 있습니다.";
                return false;
            }

            var unionMask = new byte[imageSize.Width * imageSize.Height];
            foreach (LabelingSegmentationObject source in sources)
            {
                if (SegmentationMaskGeometryService.TryRasterize(
                    source,
                    imageSize,
                    out byte[] sourceMask,
                    out Rectangle sourceBounds))
                {
                    UnionMask(sourceMask, sourceBounds, unionMask, imageSize);
                }
            }

            Rectangle maskBounds = SegmentationGeometry.GetMaskBounds(unionMask, imageSize);
            if (maskBounds.IsEmpty)
            {
                error = "선택한 세그먼트에서 병합 가능한 픽셀을 찾지 못했습니다.";
                return false;
            }

            var merged = new LabelingSegmentationObject
            {
                ClassName = className,
                ClassItem = sources[0].ClassItem,
                ObjectId = Guid.NewGuid().ToString("N"),
                ComponentIndex = -1,
                ZOrder = sources.Max(source => source.ZOrder),
                LastStructuralOperation = StructuralOperationName,
                MaskData = unionMask,
                MaskSize = imageSize,
                MaskBounds = maskBounds,
                RenderVersion = 1,
                RenderDirtyBounds = maskBounds,
                Selected = true
            };
            result = new WpfSegmentationMergeResult(indices, indices[0], merged);
            return true;
        }

        private static string ResolveClassName(LabelingSegmentationObject segment)
        {
            string className = segment?.ClassName;
            if (string.IsNullOrWhiteSpace(className))
            {
                className = segment?.ClassItem?.Text;
            }

            return string.IsNullOrWhiteSpace(className) ? "Defect" : className.Trim();
        }

        private static void UnionMask(
            byte[] sourceMask,
            Rectangle sourceBounds,
            byte[] unionMask,
            Size imageSize)
        {
            Rectangle clipped = Rectangle.Intersect(
                sourceBounds,
                new Rectangle(Point.Empty, imageSize));
            if (clipped.IsEmpty)
            {
                return;
            }

            for (int y = clipped.Top; y < clipped.Bottom; y++)
            {
                int sourceOffset = (y * imageSize.Width) + clipped.Left;
                int targetOffset = (y * imageSize.Width) + clipped.Left;
                for (int x = 0; x < clipped.Width; x++)
                {
                    if (sourceMask[sourceOffset + x] != 0)
                    {
                        unionMask[targetOffset + x] = 255;
                    }
                }
            }
        }
    }

    [Obsolete("Use SegmentationMergeService.", false)]
    public sealed class WpfSegmentationMergeService : SegmentationMergeService
    {
    }

    public sealed class WpfSegmentationMergeResult
    {
        public WpfSegmentationMergeResult(
            IReadOnlyList<int> sourceIndices,
            int insertIndex,
            LabelingSegmentationObject mergedSegment)
        {
            SourceIndices = sourceIndices ?? Array.Empty<int>();
            InsertIndex = insertIndex;
            MergedSegment = mergedSegment;
        }

        public IReadOnlyList<int> SourceIndices { get; }

        public int InsertIndex { get; }

        public LabelingSegmentationObject MergedSegment { get; }
    }
}
