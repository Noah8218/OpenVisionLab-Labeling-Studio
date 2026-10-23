using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace MvcVisionSystem
{
    public enum WpfSegmentationHoleEditMode
    {
        Add,
        Remove
    }

    public class SegmentationHoleService
    {
        private static readonly Point[] FourConnectedOffsets =
        {
            new Point(-1, 0),
            new Point(1, 0),
            new Point(0, -1),
            new Point(0, 1)
        };

        public const string AddStructuralOperationName = "HoleAdd";
        public const string RemoveStructuralOperationName = "HoleRemove";

        public bool TryAddHole(
            LabelingSegmentationObject source,
            IEnumerable<Point> holePoints,
            Size imageSize,
            out LabelingSegmentationObject result,
            out string error)
        {
            result = null;
            error = string.Empty;
            if (!SegmentationMaskGeometryService.TryRasterize(
                source,
                imageSize,
                out byte[] sourceMask,
                out Rectangle sourceBounds))
            {
                error = "구멍을 추가할 세그먼트 형상을 찾지 못했습니다.";
                return false;
            }

            var holeDraft = new LabelingSegmentationObject(holePoints, source.ClassItem)
            {
                ClassName = ResolveClassName(source)
            };
            if (!SegmentationMaskGeometryService.TryRasterize(
                holeDraft,
                imageSize,
                out byte[] holeMask,
                out Rectangle holeBounds))
            {
                error = "구멍을 만들려면 유효한 점 3개 이상이 필요합니다.";
                return false;
            }

            if (holeBounds.Left <= sourceBounds.Left
                || holeBounds.Top <= sourceBounds.Top
                || holeBounds.Right >= sourceBounds.Right
                || holeBounds.Bottom >= sourceBounds.Bottom)
            {
                error = "선택한 객체 경계 안쪽에 구멍을 그리세요.";
                return false;
            }

            int removedPixelCount = 0;
            int seedIndex = -1;
            for (int y = holeBounds.Top; y < holeBounds.Bottom; y++)
            {
                int rowOffset = y * imageSize.Width;
                for (int x = holeBounds.Left; x < holeBounds.Right; x++)
                {
                    int index = rowOffset + x;
                    if (holeMask[index] == 0)
                    {
                        continue;
                    }

                    if (sourceMask[index] == 0)
                    {
                        error = "구멍은 선택한 객체 영역 안에 완전히 포함되어야 합니다.";
                        return false;
                    }

                    sourceMask[index] = 0;
                    removedPixelCount++;
                    seedIndex = seedIndex < 0 ? index : seedIndex;
                }
            }

            if (removedPixelCount == 0
                || seedIndex < 0
                || !TryCollectEnclosedBackground(
                    sourceMask,
                    imageSize,
                    sourceBounds,
                    seedIndex % imageSize.Width,
                    seedIndex / imageSize.Width,
                    out _))
            {
                error = "외부 배경과 연결되는 영역은 내부 구멍으로 추가할 수 없습니다.";
                return false;
            }

            result = CreateEditedSegment(
                source,
                sourceMask,
                imageSize,
                AddStructuralOperationName);
            return result != null;
        }

        public bool TryRemoveHole(
            LabelingSegmentationObject source,
            Point imagePoint,
            Size imageSize,
            out LabelingSegmentationObject result,
            out string error)
        {
            result = null;
            error = string.Empty;
            if (!SegmentationMaskGeometryService.TryRasterize(
                source,
                imageSize,
                out byte[] sourceMask,
                out Rectangle sourceBounds))
            {
                error = "구멍을 채울 세그먼트 형상을 찾지 못했습니다.";
                return false;
            }

            if (!sourceBounds.Contains(imagePoint)
                || sourceMask[(imagePoint.Y * imageSize.Width) + imagePoint.X] != 0)
            {
                error = "선택한 객체의 빈 내부 구멍을 클릭하세요.";
                return false;
            }

            if (!TryCollectEnclosedBackground(
                sourceMask,
                imageSize,
                sourceBounds,
                imagePoint.X,
                imagePoint.Y,
                out List<int> holePixels))
            {
                error = "외부 배경과 연결된 영역은 내부 구멍이 아닙니다.";
                return false;
            }

            foreach (int index in holePixels)
            {
                sourceMask[index] = 255;
            }

            result = CreateEditedSegment(
                source,
                sourceMask,
                imageSize,
                RemoveStructuralOperationName);
            return result != null;
        }

        private static bool TryCollectEnclosedBackground(
            byte[] mask,
            Size imageSize,
            Rectangle bounds,
            int seedX,
            int seedY,
            out List<int> pixels)
        {
            pixels = new List<int>();
            if (seedX < bounds.Left
                || seedX >= bounds.Right
                || seedY < bounds.Top
                || seedY >= bounds.Bottom)
            {
                return false;
            }

            int seed = (seedY * imageSize.Width) + seedX;
            if (mask[seed] != 0)
            {
                return false;
            }

            bool touchesExterior = false;
            var visited = new bool[mask.Length];
            var queue = new Queue<int>();
            visited[seed] = true;
            queue.Enqueue(seed);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                pixels.Add(current);
                int currentX = current % imageSize.Width;
                int currentY = current / imageSize.Width;
                if (currentX == bounds.Left
                    || currentX == bounds.Right - 1
                    || currentY == bounds.Top
                    || currentY == bounds.Bottom - 1)
                {
                    touchesExterior = true;
                }

                foreach (Point offset in FourConnectedOffsets)
                {
                    int nextX = currentX + offset.X;
                    int nextY = currentY + offset.Y;
                    if (nextX < bounds.Left
                        || nextX >= bounds.Right
                        || nextY < bounds.Top
                        || nextY >= bounds.Bottom)
                    {
                        continue;
                    }

                    int next = (nextY * imageSize.Width) + nextX;
                    if (mask[next] != 0 || visited[next])
                    {
                        continue;
                    }

                    visited[next] = true;
                    queue.Enqueue(next);
                }
            }

            return pixels.Count > 0 && !touchesExterior;
        }

        private static LabelingSegmentationObject CreateEditedSegment(
            LabelingSegmentationObject source,
            byte[] maskData,
            Size imageSize,
            string operation)
        {
            Rectangle maskBounds = SegmentationGeometry.GetMaskBounds(maskData, imageSize);
            if (maskBounds.IsEmpty)
            {
                return null;
            }

            return new LabelingSegmentationObject
            {
                ClassName = ResolveClassName(source),
                ClassItem = source.ClassItem,
                ObjectId = string.IsNullOrWhiteSpace(source.ObjectId)
                    ? Guid.NewGuid().ToString("N")
                    : source.ObjectId,
                ComponentIndex = -1,
                ZOrder = source.ZOrder,
                LastStructuralOperation = operation,
                MaskData = maskData,
                MaskSize = imageSize,
                MaskBounds = maskBounds,
                RenderVersion = Math.Max(1, source.RenderVersion + 1),
                RenderDirtyBounds = maskBounds,
                Selected = true
            };
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
    }

    [Obsolete("Use SegmentationHoleService.", false)]
    public sealed class WpfSegmentationHoleService : SegmentationHoleService
    {
    }
}
