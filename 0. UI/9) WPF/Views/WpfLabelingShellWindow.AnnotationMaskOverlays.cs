using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.Canvas;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using OpenVisionLab.ImageCanvas.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using DrawingRectangle = System.Drawing.Rectangle;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Mask overlays update only dirty raster segments so brush/eraser rendering stays incremental.
        private bool TryRefreshMaskStrokeCanvasOverlays()
            => TryRefreshMaskStrokeCanvasOverlays(activeMaskStrokeSegmentIndices, activeMaskStrokeNeedsFullObjectRefresh);

        private bool TryRefreshMaskStrokeCanvasOverlays(
            IEnumerable<int> segmentIndices,
            bool needsFullObjectRefresh)
            => TryRefreshMaskStrokeCanvasOverlays(segmentIndices, needsFullObjectRefresh, refreshAfterInput: false);

        private bool TryRefreshMaskStrokeCanvasOverlays(
            IEnumerable<int> segmentIndices,
            bool needsFullObjectRefresh,
            bool refreshAfterInput)
        {
            IReadOnlyList<int> orderedSegmentIndices = (segmentIndices ?? Array.Empty<int>())
                .Distinct()
                .OrderBy(index => index)
                .ToList();
            if (needsFullObjectRefresh
                || orderedSegmentIndices.Count == 0
                || MainCanvasViewModel == null)
            {
                return false;
            }

            float maskOpacity = (float)(LearningWorkflowViewModel?.MaskOpacity ?? 0.66);
            WpfObjectReviewListItem selectedObject = ObjectReviewViewModel?.SelectedObject;
            string selectedSourceKey = selectedObject?.SourceKey ?? string.Empty;
            int selectedSourceIndex = selectedObject?.SourceIndex ?? -1;
            foreach (int segmentIndex in orderedSegmentIndices)
            {
                if (!TryBuildManualMaskOverlay(segmentIndex, selectedSourceKey, selectedSourceIndex, maskOpacity, out RoiImageCanvasMaskOverlay maskOverlay)
                    || !MainCanvasViewModel.TryUpsertMaskOverlay(maskOverlay, refreshAfterInput))
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryBuildManualMaskOverlay(
            int segmentIndex,
            string selectedSourceKey,
            int selectedSourceIndex,
            float maskOpacity,
            out RoiImageCanvasMaskOverlay overlay)
        {
            overlay = null;
            if (segmentIndex < 0 || segmentIndex >= manualSegments.Count)
            {
                return false;
            }

            LabelingSegmentationObject segment = manualSegments[segmentIndex];
            if (segment?.IsRasterMask != true || segment.MaskData == null || segment.MaskSize.IsEmpty || segment.Bounds.IsEmpty)
            {
                return false;
            }

            DrawingRectangle maskBounds = DrawingRectangle.Intersect(
                segment.Bounds,
                new DrawingRectangle(0, 0, segment.MaskSize.Width, segment.MaskSize.Height));
            if (maskBounds.IsEmpty)
            {
                return false;
            }

            string className = FirstNonEmpty(segment.ClassName, segment.ClassItem?.Text, "Defect");
            bool isSegmentSelected = string.Equals(
                selectedSourceKey,
                WpfObjectReviewSource.ManualSegment.ToString(),
                StringComparison.OrdinalIgnoreCase)
                && selectedSourceIndex == segmentIndex
                && ShouldSelectCommittedMaskAfterStroke();
            int displayIndex = manualRois.Count + segmentIndex + 1;
            overlay = new RoiImageCanvasMaskOverlay(
                $"{activeImagePath}|mask|{segmentIndex}",
                segment.MaskData,
                segment.MaskSize,
                maskBounds,
                segment.Color,
                maskOpacity,
                segment.RenderVersion,
                isSegmentSelected,
                $"MASK {displayIndex} {className}",
                segment.RenderDirtyBounds,
                (uploadedVersion, uploadedBounds) => ClearMaskRenderDirtyBounds(segment, uploadedVersion, uploadedBounds));
            return true;
        }
        private static void ClearMaskRenderDirtyBounds(LabelingSegmentationObject segment, int uploadedVersion, DrawingRectangle uploadedBounds)
        {
            if (segment == null || uploadedBounds.IsEmpty || segment.RenderVersion != uploadedVersion)
            {
                return;
            }

            // The OpenGL texture has consumed this exact render version. Keep newer
            // stroke dirt intact so a fast MouseMove cannot clear work not uploaded yet.
            segment.RenderDirtyBounds = DrawingRectangle.Empty;
        }

        private static string FormatSegmentBoundsCompact(LabelingSegmentationObject segment)
        {
            DrawingRectangle bounds = segment?.Bounds ?? DrawingRectangle.Empty;
            return bounds.IsEmpty
                ? "-"
                : WpfCandidateReviewPresenter.FormatBoundsCompact(bounds);
        }
    }
}
