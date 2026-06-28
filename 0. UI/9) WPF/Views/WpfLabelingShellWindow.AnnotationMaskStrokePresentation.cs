using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        private bool TryRefreshMaskStrokeObjectReviewRows()
            => TryRefreshMaskStrokeObjectReviewRows(activeMaskStrokeSegmentIndices, activeMaskStrokeNeedsFullObjectRefresh);

        private bool TryRefreshMaskStrokeObjectReviewRows(
            IEnumerable<int> segmentIndices,
            bool needsFullObjectRefresh)
        {
            IReadOnlyList<int> orderedSegmentIndices = (segmentIndices ?? Array.Empty<int>())
                .Distinct()
                .OrderBy(index => index)
                .ToList();
            if (needsFullObjectRefresh
                || orderedSegmentIndices.Count == 0
                || ObjectReviewViewModel == null)
            {
                return false;
            }

            string summary = WpfObjectReviewPresenter.BuildSummary(
                manualRois.Count + manualSegments.Count + confirmedDetectionCandidates.Count);
            bool selectChangedMask = orderedSegmentIndices.Count == 1
                && ShouldSelectCommittedMaskAfterStroke();
            foreach (int segmentIndex in orderedSegmentIndices)
            {
                if (!TryRefreshManualSegmentObjectReviewRow(segmentIndex, summary, selectChangedMask))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsMaskAnnotationToolActive()
            => maskEditStateService.IsMaskPaintTool(activeAnnotationTool);

        private bool ShouldSelectCommittedMaskAfterStroke()
            => !suppressMaskStrokeCommitSelection
                && maskEditStateService.ShouldSelectCommittedMask(activeAnnotationTool);

        private int GetMaskBrushRadius()
        {
            int brushSize = LearningWorkflowViewModel?.BrushSize ?? WpfMaskAnnotationService.DefaultBrushRadius * 2;
            return Math.Clamp((int)Math.Round(brushSize / 2D), 1, 128);
        }

        private System.Drawing.Color GetMaskCursorPreviewColor(bool isEraser)
        {
            if (isEraser)
            {
                return System.Drawing.Color.FromArgb(245, 158, 11);
            }

            string className = FirstNonEmpty(GetSelectedClassName(), "Defect");
            CClassItem existing = global.Data.ClassNamedList?
                .FirstOrDefault(item => string.Equals(item?.Text, className, StringComparison.OrdinalIgnoreCase));
            return existing?.DrawColor ?? System.Drawing.Color.FromArgb(44, 210, 110);
        }
    }
}
