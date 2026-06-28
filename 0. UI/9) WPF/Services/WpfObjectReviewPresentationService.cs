using MvcVisionSystem._1._Core;
using OpenVisionLab.ImageCanvas.Canvas;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace MvcVisionSystem
{
    public sealed class WpfObjectReviewPresentationService
    {
        public WpfObjectReviewListPresentation BuildListPresentation(
            IReadOnlyList<Rectangle> manualRois,
            IReadOnlyList<string> manualClassNames,
            IReadOnlyList<CanvasRoiShapeKind> manualShapeKinds,
            IReadOnlyList<string> manualOverlayIds,
            IReadOnlyList<LabelingSegmentationObject> manualSegments,
            IReadOnlyList<YoloWorkerSmokeCandidate> confirmedCandidates,
            WpfObjectReviewItemRef preferredSelection,
            WpfObjectReviewItemRef previousSelection,
            Func<YoloWorkerSmokeCandidate, Rectangle> getCandidateBounds,
            Func<YoloWorkerSmokeCandidate, string> buildCandidateDetail)
        {
            int manualRoiCount = manualRois?.Count ?? 0;
            int manualSegmentCount = manualSegments?.Count ?? 0;
            int confirmedCount = confirmedCandidates?.Count ?? 0;
            int objectCount = manualRoiCount + manualSegmentCount + confirmedCount;
            string summary = WpfObjectReviewPresenter.BuildSummary(objectCount);
            var rows = new List<WpfObjectReviewListItem>();

            if (objectCount == 0)
            {
                rows.Add(WpfObjectReviewListItem.Empty(WpfObjectReviewPresenter.EmptyText));
                return new WpfObjectReviewListPresentation(rows, summary, preferredSelection ?? previousSelection);
            }

            // Row construction lives here so shell delete/drag code does not duplicate side-panel display rules.
            for (int i = 0; i < manualRoiCount; i++)
            {
                rows.Add(BuildManualRoiItem(manualRois, manualClassNames, manualShapeKinds, manualOverlayIds, i));
            }

            for (int i = 0; i < manualSegmentCount; i++)
            {
                LabelingSegmentationObject segment = manualSegments[i];
                if (segment == null)
                {
                    continue;
                }

                string className = FirstNonEmpty(segment.ClassName, segment.ClassItem?.Text, "Defect");
                string shapeName = segment.IsRasterMask ? "\uB9C8\uC2A4\uD06C" : "\uD3F4\uB9AC\uACE4";
                WpfObjectReviewItemRef payload = WpfObjectReviewItemRef.ManualSegment(i);
                rows.Add(WpfObjectReviewPresenter.BuildManualItem(
                    manualRoiCount + i + 1,
                    className,
                    segment.Bounds,
                    shapeName,
                    payload.Source.ToString(),
                    payload.Index,
                    payload));
            }

            for (int i = 0; i < confirmedCount; i++)
            {
                YoloWorkerSmokeCandidate candidate = confirmedCandidates[i];
                WpfObjectReviewItemRef payload = WpfObjectReviewItemRef.ConfirmedAi(i);
                int displayIndex = candidate?.Index > 0 ? candidate.Index : i + 1;
                Rectangle bounds = getCandidateBounds == null ? Rectangle.Empty : getCandidateBounds(candidate);
                rows.Add(WpfObjectReviewPresenter.BuildConfirmedItem(
                    candidate,
                    displayIndex,
                    bounds,
                    buildCandidateDetail == null ? string.Empty : buildCandidateDetail(candidate) ?? string.Empty,
                    payload.Source.ToString(),
                    payload.Index,
                    payload));
            }

            return new WpfObjectReviewListPresentation(rows, summary, preferredSelection ?? previousSelection);
        }

        public WpfObjectReviewListItem BuildManualRoiItem(
            IReadOnlyList<Rectangle> manualRois,
            IReadOnlyList<string> manualClassNames,
            IReadOnlyList<CanvasRoiShapeKind> manualShapeKinds,
            IReadOnlyList<string> manualOverlayIds,
            int index)
        {
            if (manualRois == null || index < 0 || index >= manualRois.Count)
            {
                return null;
            }

            WpfObjectReviewItemRef payload = WpfObjectReviewItemRef.Manual(
                index,
                WpfObjectReviewSelectionService.GetManualRoiOverlayId(manualOverlayIds, index));
            return WpfObjectReviewPresenter.BuildManualItem(
                index + 1,
                GetManualRoiClassName(manualClassNames, index),
                manualRois[index],
                FormatManualRoiShapeName(GetManualRoiShapeKind(manualShapeKinds, index)),
                payload.Source.ToString(),
                payload.Index,
                payload);
        }

        public WpfObjectReviewListItem BuildManualSegmentItem(
            int manualRoiCount,
            IReadOnlyList<LabelingSegmentationObject> manualSegments,
            int index)
        {
            if (manualSegments == null || index < 0 || index >= manualSegments.Count || manualSegments[index] == null)
            {
                return null;
            }

            LabelingSegmentationObject segment = manualSegments[index];
            string className = FirstNonEmpty(segment.ClassName, segment.ClassItem?.Text, "Defect");
            string shapeName = segment.IsRasterMask ? "\uB9C8\uC2A4\uD06C" : "\uD3F4\uB9AC\uACE4";
            WpfObjectReviewItemRef payload = WpfObjectReviewItemRef.ManualSegment(index);
            return WpfObjectReviewPresenter.BuildManualItem(
                manualRoiCount + index + 1,
                className,
                segment.Bounds,
                shapeName,
                payload.Source.ToString(),
                payload.Index,
                payload);
        }
        public WpfObjectReviewDeleteRefreshPlan BuildDeleteRefreshPlan(
            WpfObjectReviewSource deletedSource,
            int remainingObjectCount,
            int fullRefreshDeleteLimit,
            int deletedObjectRowIndex,
            int currentObjectRowCount)
        {
            bool useIncremental = WpfObjectReviewSelectionService.ShouldUseIncrementalDelete(
                deletedSource,
                remainingObjectCount,
                fullRefreshDeleteLimit,
                deletedObjectRowIndex,
                currentObjectRowCount);
            int selectedRowIndex = useIncremental
                ? WpfObjectReviewSelectionService.GetSelectionIndexAfterDelete(deletedObjectRowIndex, remainingObjectCount)
                : -1;

            // Manual ROI delete must publish one collection Remove, not a full Reset of every review row.
            return new WpfObjectReviewDeleteRefreshPlan(
                useIncremental,
                WpfObjectReviewPresenter.BuildSummary(remainingObjectCount),
                selectedRowIndex);
        }

        public static string GetManualRoiClassName(IReadOnlyList<string> manualClassNames, int index)
        {
            return manualClassNames != null
                && index >= 0
                && index < manualClassNames.Count
                && !string.IsNullOrWhiteSpace(manualClassNames[index])
                ? manualClassNames[index]
                : "Defect";
        }

        public static CanvasRoiShapeKind GetManualRoiShapeKind(IReadOnlyList<CanvasRoiShapeKind> manualShapeKinds, int index)
        {
            return manualShapeKinds != null && index >= 0 && index < manualShapeKinds.Count
                ? manualShapeKinds[index]
                : CanvasRoiShapeKind.Rectangle;
        }

        public static string FormatManualRoiShapeName(CanvasRoiShapeKind shapeKind)
            => shapeKind == CanvasRoiShapeKind.Ellipse ? "\uD0C0\uC6D0" : "\uBC15\uC2A4";

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }
    }

    public sealed class WpfObjectReviewListPresentation
    {
        public WpfObjectReviewListPresentation(
            IReadOnlyList<WpfObjectReviewListItem> rows,
            string summary,
            WpfObjectReviewItemRef selectedItem)
        {
            Rows = rows ?? Array.Empty<WpfObjectReviewListItem>();
            Summary = summary ?? string.Empty;
            SelectedItem = selectedItem;
        }

        public IReadOnlyList<WpfObjectReviewListItem> Rows { get; }

        public string Summary { get; }

        public WpfObjectReviewItemRef SelectedItem { get; }
    }

    public sealed class WpfObjectReviewDeleteRefreshPlan
    {
        public WpfObjectReviewDeleteRefreshPlan(bool useIncremental, string summary, int selectedRowIndex)
        {
            UseIncremental = useIncremental;
            Summary = summary ?? string.Empty;
            SelectedRowIndex = selectedRowIndex;
        }

        public bool UseIncremental { get; }

        public string Summary { get; }

        public int SelectedRowIndex { get; }
    }
}
