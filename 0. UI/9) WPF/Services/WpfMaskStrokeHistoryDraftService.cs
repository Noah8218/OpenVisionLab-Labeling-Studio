using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem
{
    internal sealed class WpfMaskStrokeHistoryDraftService
    {
        public WpfAnnotationHistorySnapshot CreateSnapshot(
            WpfQueuedMaskStrokeCommit command,
            IReadOnlyList<MaskStrokeHistoryDeltaDraft> drafts,
            ICollection<LabelingSegmentationObject> currentSegments)
        {
            IReadOnlyList<WpfMaskSegmentHistoryDelta> deltas = (drafts ?? Array.Empty<MaskStrokeHistoryDeltaDraft>())
                .Select(draft => CreateDelta(draft, currentSegments))
                .Where(delta => delta != null)
                .ToList();
            return new WpfAnnotationHistorySnapshot(
                command.ActionName,
                Array.Empty<System.Drawing.Rectangle>(),
                Array.Empty<string>(),
                Array.Empty<CanvasRoiShapeKind>(),
                Array.Empty<LabelingSegmentationObject>(),
                Array.Empty<YoloWorkerSmokeCandidate>(),
                Array.Empty<YoloWorkerSmokeCandidate>(),
                deltas,
                restoreManualRois: false,
                restoreManualSegments: false,
                restorePendingCandidates: false,
                restoreConfirmedCandidates: false);
        }

        public IReadOnlyList<MaskStrokeHistoryDeltaDraft> BuildDrafts(
            WpfQueuedMaskStrokeCommit command,
            IReadOnlyList<LabelingSegmentationObject> manualSegments)
        {
            var drafts = new List<MaskStrokeHistoryDeltaDraft>();
            if (command == null || command.Centers.Count == 0)
            {
                return drafts;
            }

            System.Drawing.Rectangle strokeBounds = GetStrokeBounds(command.Centers, command.Radius, command.ImageSize);
            if (strokeBounds.IsEmpty)
            {
                return drafts;
            }

            if (command.Tool == WpfAnnotationTool.Brush)
            {
                int segmentIndex = FindBrushTargetSegmentIndex(command.ClassName, manualSegments);
                if (segmentIndex < 0)
                {
                    drafts.Add(MaskStrokeHistoryDeltaDraft.CreatedSegment(
                        manualSegments?.Count ?? 0,
                        command.ImageSize,
                        command.ClassName,
                        command.ClassItem));
                    return drafts;
                }

                AddPatchDraft(drafts, segmentIndex, manualSegments[segmentIndex], strokeBounds, restoreIfRemoved: false);
                return drafts;
            }

            if (command.Tool == WpfAnnotationTool.Eraser)
            {
                for (int index = 0; index < (manualSegments?.Count ?? 0); index++)
                {
                    LabelingSegmentationObject segment = manualSegments[index];
                    if (segment?.IsRasterMask != true)
                    {
                        continue;
                    }

                    System.Drawing.Rectangle currentBounds = segment.MaskBounds.IsEmpty ? segment.Bounds : segment.MaskBounds;
                    if (currentBounds.IsEmpty || !currentBounds.IntersectsWith(strokeBounds))
                    {
                        continue;
                    }

                    AddPatchDraft(drafts, index, segment, System.Drawing.Rectangle.Intersect(currentBounds, strokeBounds), restoreIfRemoved: true);
                }
            }

            return drafts;
        }

        private static WpfMaskSegmentHistoryDelta CreateDelta(
            MaskStrokeHistoryDeltaDraft draft,
            ICollection<LabelingSegmentationObject> currentSegments)
        {
            if (draft == null)
            {
                return null;
            }

            bool restoreRemovedSegment = draft.RestoreIfRemoved
                && draft.Segment != null
                && currentSegments?.Contains(draft.Segment) != true;
            return new WpfMaskSegmentHistoryDelta(
                draft.SegmentIndex,
                draft.RestoreBounds,
                draft.Pixels,
                draft.MaskSize,
                draft.MaskBounds,
                draft.RenderVersion,
                draft.RenderDirtyBounds,
                draft.ClassName,
                draft.ClassItem,
                draft.Selected,
                removeCreatedSegment: draft.RemoveCreatedSegment,
                restoreRemovedSegment: restoreRemovedSegment);
        }

        private static void AddPatchDraft(
            ICollection<MaskStrokeHistoryDeltaDraft> drafts,
            int segmentIndex,
            LabelingSegmentationObject segment,
            System.Drawing.Rectangle restoreBounds,
            bool restoreIfRemoved)
        {
            if (drafts == null
                || segment?.IsRasterMask != true
                || restoreBounds.IsEmpty)
            {
                return;
            }

            System.Drawing.Rectangle imageBounds = new System.Drawing.Rectangle(System.Drawing.Point.Empty, segment.MaskSize);
            System.Drawing.Rectangle clippedBounds = System.Drawing.Rectangle.Intersect(restoreBounds, imageBounds);
            if (clippedBounds.IsEmpty)
            {
                return;
            }

            drafts.Add(MaskStrokeHistoryDeltaDraft.Patch(
                segmentIndex,
                segment,
                clippedBounds,
                CopyMaskRegion(segment.MaskData, segment.MaskSize, clippedBounds),
                restoreIfRemoved));
        }

        private static int FindBrushTargetSegmentIndex(
            string className,
            IReadOnlyList<LabelingSegmentationObject> manualSegments)
        {
            string normalizedClassName = string.IsNullOrWhiteSpace(className) ? "Defect" : className;
            for (int index = 0; index < (manualSegments?.Count ?? 0); index++)
            {
                LabelingSegmentationObject segment = manualSegments[index];
                if (segment?.IsRasterMask == true
                    && string.Equals(segment.ClassName, normalizedClassName, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return -1;
        }

        private static byte[] CopyMaskRegion(
            byte[] maskData,
            System.Drawing.Size maskSize,
            System.Drawing.Rectangle bounds)
        {
            if (maskData == null
                || maskSize.Width <= 0
                || maskSize.Height <= 0
                || bounds.IsEmpty)
            {
                return Array.Empty<byte>();
            }

            byte[] pixels = new byte[bounds.Width * bounds.Height];
            for (int y = 0; y < bounds.Height; y++)
            {
                int sourceOffset = ((bounds.Top + y) * maskSize.Width) + bounds.Left;
                int targetOffset = y * bounds.Width;
                Buffer.BlockCopy(maskData, sourceOffset, pixels, targetOffset, bounds.Width);
            }

            return pixels;
        }

        private static System.Drawing.Rectangle GetStrokeBounds(
            IReadOnlyList<System.Drawing.Point> centers,
            int radius,
            System.Drawing.Size imageSize)
        {
            if (centers == null || centers.Count == 0 || imageSize.Width <= 0 || imageSize.Height <= 0)
            {
                return System.Drawing.Rectangle.Empty;
            }

            int safeRadius = Math.Max(1, radius) + 1;
            int left = centers.Min(point => point.X) - safeRadius;
            int top = centers.Min(point => point.Y) - safeRadius;
            int right = centers.Max(point => point.X) + safeRadius + 1;
            int bottom = centers.Max(point => point.Y) + safeRadius + 1;
            return System.Drawing.Rectangle.Intersect(
                System.Drawing.Rectangle.FromLTRB(left, top, right, bottom),
                new System.Drawing.Rectangle(System.Drawing.Point.Empty, imageSize));
        }
    }
}
