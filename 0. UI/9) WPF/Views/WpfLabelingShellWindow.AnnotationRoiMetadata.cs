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
        // ROI metadata helpers keep canvas overlay IDs aligned with review rows and history snapshots.
        private void UpdateManualRoiFromCanvasRect(CanvasRect<float> rect)
        {
            int index = FindManualRoiIndexByOverlayId(rect?.UniqueId);
            if (index < 0)
            {
                return;
            }

            DrawingRectangle bounds = ConvertCanvasRectToImageBounds(rect);
            if (bounds.IsEmpty)
            {
                return;
            }

            if (manualRois[index] != bounds || GetManualRoiShapeKind(index) != rect.ShapeKind)
            {
                RegisterRoiEditHistoryBeforeChange(rect.UniqueId, "박스 수정");
            }

            manualRois[index] = bounds;
            manualRoiShapeKinds[index] = rect.ShapeKind;
        }

        private WpfObjectReviewItemRef CreateManualRoiSelection(CanvasRect<float> rect)
        {
            int index = FindManualRoiIndexByOverlayId(rect?.UniqueId);
            return index >= 0 ? WpfObjectReviewItemRef.Manual(index, rect?.UniqueId) : null;
        }

        private DrawingRectangle ConvertCanvasRectToImageBounds(CanvasRect<float> rect)
        {
            if (rect == null || rect.IsEmpty() || activeImageSize.IsEmpty)
            {
                return DrawingRectangle.Empty;
            }

            var raw = new DrawingRectangle(
                (int)Math.Round(rect.Left),
                (int)Math.Round(activeImageSize.Height - rect.Top),
                (int)Math.Round(rect.Width),
                (int)Math.Round(rect.Height));

            return DrawingRectangle.Intersect(
                raw,
                new DrawingRectangle(0, 0, activeImageSize.Width, activeImageSize.Height));
        }

        private int FindManualRoiIndexByOverlayId(string overlayId)
            => WpfObjectReviewSelectionService.FindManualRoiIndexByOverlayId(manualRoiOverlayIds, overlayId);

        private CanvasRoiShapeKind GetManualRoiShapeKind(int index)
            => WpfObjectReviewPresentationService.GetManualRoiShapeKind(manualRoiShapeKinds, index);

        private string GetManualRoiOverlayId(int index)
            => WpfObjectReviewSelectionService.GetManualRoiOverlayId(manualRoiOverlayIds, index);

        private void EnsureManualRoiMetadataCount()
        {
            while (manualRoiShapeKinds.Count < manualRois.Count)
            {
                manualRoiShapeKinds.Add(CanvasRoiShapeKind.Rectangle);
            }

            while (manualRoiOverlayIds.Count < manualRois.Count)
            {
                manualRoiOverlayIds.Add(string.Empty);
            }
        }

        private static void RemoveAtIfPresent<T>(IList<T> items, int index)
        {
            if (items != null && index >= 0 && index < items.Count)
            {
                items.RemoveAt(index);
            }
        }

        private static string FormatManualRoiShapeName(CanvasRoiShapeKind shapeKind)
            => WpfObjectReviewPresentationService.FormatManualRoiShapeName(shapeKind);
    }
}
