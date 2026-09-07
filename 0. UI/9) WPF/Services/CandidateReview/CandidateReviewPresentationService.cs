using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using OpenVisionLab.ImageCanvas.ViewModels;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MvcVisionSystem
{
    public class CandidateReviewPresentationService
    {
        public static YoloWorkerSmokeCandidate FromDefect(DefectInfo defect, int index)
        {
            return new YoloWorkerSmokeCandidate
            {
                Index = index,
                ClassName = string.IsNullOrWhiteSpace(defect?.ClassName) ? "Defect" : defect.ClassName,
                Confidence = defect?.Confidence ?? 0D,
                X = defect?.X ?? 0D,
                Y = defect?.Y ?? 0D,
                Width = defect?.Width ?? 0D,
                Height = defect?.Height ?? 0D,
                CandidateType = defect?.CandidateType ?? string.Empty,
                PredictionType = defect?.PredictionType ?? string.Empty,
                ImageLevel = defect?.ImageLevel == true,
                AnomalyScore = defect?.AnomalyScore,
                AnomalyThreshold = defect?.AnomalyThreshold,
                HeatmapPath = defect?.HeatmapPath ?? string.Empty,
                SegmentationType = defect?.SegmentationType ?? string.Empty,
                PolygonPoints = defect?.PolygonPoints?.ToArray() ?? Array.Empty<DetectionPolygonPoint>(),
                NormalizedPolygonPoints = defect?.NormalizedPolygonPoints?.ToArray() ?? Array.Empty<DetectionPolygonPoint>()
            };
        }

        public static Rectangle ClipCandidateBounds(YoloWorkerSmokeCandidate candidate, Size imageSize)
        {
            if (candidate == null)
            {
                return Rectangle.Empty;
            }

            Rectangle bounds = candidate.ToRectangle();
            if (bounds.IsEmpty || imageSize.IsEmpty)
            {
                return bounds;
            }

            return Rectangle.Intersect(
                bounds,
                new Rectangle(0, 0, imageSize.Width, imageSize.Height));
        }

        public static IReadOnlyList<PointF> GetCandidateContourPoints(
            YoloWorkerSmokeCandidate candidate,
            Size imageSize)
        {
            if (candidate == null || imageSize.IsEmpty)
            {
                return Array.Empty<PointF>();
            }

            IReadOnlyList<DetectionPolygonPoint> sourcePoints = candidate.PolygonPoints;
            bool normalized = sourcePoints == null || sourcePoints.Count < 3;
            if (normalized)
            {
                sourcePoints = candidate.NormalizedPolygonPoints;
            }

            if (sourcePoints == null || sourcePoints.Count < 3)
            {
                return Array.Empty<PointF>();
            }

            List<PointF> points = sourcePoints
                .Where(point => point != null && float.IsFinite(point.X) && float.IsFinite(point.Y))
                .Select(point => new PointF(
                    Math.Clamp(normalized ? point.X * imageSize.Width : point.X, 0F, imageSize.Width - 1F),
                    Math.Clamp(normalized ? point.Y * imageSize.Height : point.Y, 0F, imageSize.Height - 1F)))
                .ToList();
            return points.Count >= 3 ? points : Array.Empty<PointF>();
        }

        public static IReadOnlyList<RoiImageCanvasDetectionOverlay> BuildDetectionOverlays(
            IEnumerable<YoloWorkerSmokeCandidate> candidates,
            YoloWorkerSmokeCandidate selectedCandidate,
            Size imageSize,
            Func<YoloWorkerSmokeCandidate, bool> isConfirmable)
        {
            return (candidates ?? Array.Empty<YoloWorkerSmokeCandidate>())
                .Where(candidate => candidate != null)
                .Select((candidate, index) =>
                {
                    IReadOnlyList<PointF> contourPoints = GetCandidateContourPoints(candidate, imageSize);
                    bool isContourOnly = contourPoints.Count >= 3
                        || string.Equals(candidate.SegmentationType, "polygon", StringComparison.OrdinalIgnoreCase);
                    bool isSelected = ReferenceEquals(candidate, selectedCandidate);
                    bool confirmable = isConfirmable != null && isConfirmable(candidate);
                    return new RoiImageCanvasDetectionOverlay
                    {
                        Index = index,
                        Bounds = ClipCandidateBounds(candidate, imageSize),
                        ContourPoints = contourPoints,
                        IsContourOnly = isContourOnly,
                        Label = CandidateReviewPresenter.BuildDetectionOverlayLabel(candidate, index + 1),
                        IsSelected = isSelected,
                        Color = isSelected
                            ? Color.FromArgb(80, 180, 255)
                            : confirmable
                            ? Color.FromArgb(36, 211, 102)
                            : Color.FromArgb(255, 193, 7)
                    };
                })
                .Where(overlay => !overlay.Bounds.IsEmpty)
                .ToList();
        }

        public static WpfCandidateOverlapInfo FindBestOverlap(
            Rectangle candidateBounds,
            IEnumerable<WpfCandidateOverlapSource> sources)
        {
            WpfCandidateOverlapSource best = default;
            double bestIou = 0D;
            foreach (WpfCandidateOverlapSource source in sources ?? Array.Empty<WpfCandidateOverlapSource>())
            {
                double iou = CandidateReviewPresenter.CalculateIntersectionOverUnion(candidateBounds, source.Bounds);
                if (iou > bestIou)
                {
                    bestIou = iou;
                    best = source;
                }
            }

            return bestIou <= 0D
                ? new WpfCandidateOverlapInfo(string.Empty, Rectangle.Empty, 0D)
                : new WpfCandidateOverlapInfo(best.Label, best.Bounds, bestIou, best.CurrentObjectRef);
        }

        public WpfCandidateReviewListPresentation BuildListPresentation(
            IReadOnlyList<YoloWorkerSmokeCandidate> pendingCandidates,
            IReadOnlyList<YoloWorkerSmokeCandidate> visibleCandidates,
            YoloWorkerSmokeCandidate preferredCandidate,
            double confidenceFilter,
            float minimumConfidence,
            Func<YoloWorkerSmokeCandidate, Rectangle> getBounds,
            Func<Rectangle, WpfCandidateOverlapInfo> getOverlap)
        {
            var rows = new List<WpfCandidateReviewListItem>();
            IReadOnlyList<YoloWorkerSmokeCandidate> pending = pendingCandidates ?? Array.Empty<YoloWorkerSmokeCandidate>();
            IReadOnlyList<YoloWorkerSmokeCandidate> visible = visibleCandidates ?? Array.Empty<YoloWorkerSmokeCandidate>();

            if (pending.Count == 0)
            {
                rows.Add(WpfCandidateReviewListItem.Empty(
                    "AI \uD6C4\uBCF4 \uC5C6\uC74C",
                    "\uD604\uC7AC \uAC80\uC0AC \uACB0\uACFC AI \uD6C4\uBCF4\uAC00 \uC5C6\uAC70\uB098 \uC544\uC9C1 \uAC80\uC0AC\uD558\uC9C0 \uC54A\uC558\uC2B5\uB2C8\uB2E4."));
                return new WpfCandidateReviewListPresentation(
                    rows,
                    "AI \uD6C4\uBCF4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.",
                    null);
            }

            if (visible.Count == 0)
            {
                rows.Add(WpfCandidateReviewListItem.Empty(
                    "\uD544\uD130 \uD1B5\uACFC AI \uD6C4\uBCF4 \uC5C6\uC74C",
                    "\uC2E0\uB8B0\uB3C4 \uAE30\uC900\uC744 \uB0AE\uCD94\uBA74 \uC228\uACA8\uC9C4 AI \uD6C4\uBCF4\uB97C \uB2E4\uC2DC \uBCFC \uC218 \uC788\uC2B5\uB2C8\uB2E4."));
                return new WpfCandidateReviewListPresentation(
                    rows,
                    $"{confidenceFilter.ToString("P0", CultureInfo.CurrentCulture)} \uC774\uC0C1 AI \uD6C4\uBCF4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.",
                    null);
            }

            // Keep row text construction outside the shell so view code does not reimplement review presentation rules.
            for (int i = 0; i < visible.Count; i++)
            {
                YoloWorkerSmokeCandidate candidate = visible[i];
                if (candidate == null)
                {
                    continue;
                }

                int displayIndex = candidate.Index > 0 ? candidate.Index : i + 1;
                Rectangle bounds = ResolveBounds(candidate, getBounds);
                rows.Add(CandidateReviewPresenter.BuildListItem(
                    candidate,
                    displayIndex,
                    bounds,
                    ResolveOverlap(bounds, getOverlap),
                    minimumConfidence));
            }

            return new WpfCandidateReviewListPresentation(rows, string.Empty, preferredCandidate);
        }

        public WpfDetectionOverlayPresentation BuildOverlayPresentation(
            string activeImagePath,
            IReadOnlyList<YoloWorkerSmokeCandidate> pendingCandidates,
            YoloWorkerSmokeCandidate selectedCandidate,
            double confidenceFilter,
            Func<YoloWorkerSmokeCandidate, bool> isDuplicate,
            Func<YoloWorkerSmokeCandidate, bool> isConfirmable,
            Func<YoloWorkerSmokeCandidate, string> buildSecondaryText)
        {
            IReadOnlyList<YoloWorkerSmokeCandidate> pending = pendingCandidates ?? Array.Empty<YoloWorkerSmokeCandidate>();
            if (pending.Count == 0)
            {
                return WpfDetectionOverlayPresentation.Empty;
            }

            string imageName = string.IsNullOrWhiteSpace(activeImagePath)
                ? "-"
                : Path.GetFileName(activeImagePath);
            string summary =
                $"{imageName} / AI \uD6C4\uBCF4 {pending.Count}\uAC1C / \uC800\uC7A5 \uC804 / \uAE30\uC900 {confidenceFilter.ToString("P0", CultureInfo.CurrentCulture)}";

            YoloWorkerSmokeCandidate selected = selectedCandidate ?? pending.FirstOrDefault(candidate => candidate != null);
            bool selectedDuplicate = selected != null && SafeInvoke(isDuplicate, selected);
            bool selectedConfirmable = selected != null && SafeInvoke(isConfirmable, selected);
            int selectedIndex = IndexOfCandidate(pending, selected);
            int fallbackIndex = selectedIndex >= 0 ? selectedIndex + 1 : 1;
            int displayIndex = selected?.Index > 0 ? selected.Index : fallbackIndex;
            string selectedText = selected == null
                ? "\uC120\uD0DD AI \uD6C4\uBCF4 \uC5C6\uC74C"
                : $"\uC120\uD0DD: AI \uD6C4\uBCF4 {displayIndex} {CandidateReviewPresenter.GetClassName(selected)} {CandidateReviewPresenter.FormatConfidence(selected, "P1")} / {SafeSecondaryText(buildSecondaryText, selected)}";
            string detail = string.Join(
                "\n",
                pending
                    .Where(candidate => candidate != null)
                    .Take(4)
                    .Select((candidate, index) => $"{index + 1}. AI \uD6C4\uBCF4 {CandidateReviewPresenter.GetClassName(candidate)} {CandidateReviewPresenter.FormatConfidence(candidate, "P1")}  {SafeSecondaryText(buildSecondaryText, candidate)}"));
            WpfDetectionOverlayStatus status = selectedDuplicate
                ? WpfDetectionOverlayStatus.Duplicate
                : selectedConfirmable
                    ? WpfDetectionOverlayStatus.Confirmable
                    : WpfDetectionOverlayStatus.Review;

            return new WpfDetectionOverlayPresentation(
                isEmpty: false,
                title: "AI \uD6C4\uBCF4(\uC800\uC7A5 \uC804)",
                summary: summary,
                selectedText: selectedText,
                detail: detail,
                status: status);
        }

        private static Rectangle ResolveBounds(
            YoloWorkerSmokeCandidate candidate,
            Func<YoloWorkerSmokeCandidate, Rectangle> getBounds)
        {
            return getBounds == null ? Rectangle.Empty : getBounds(candidate);
        }

        private static WpfCandidateOverlapInfo ResolveOverlap(
            Rectangle bounds,
            Func<Rectangle, WpfCandidateOverlapInfo> getOverlap)
        {
            return getOverlap == null
                ? new WpfCandidateOverlapInfo(string.Empty, Rectangle.Empty, 0D)
                : getOverlap(bounds);
        }

        private static bool SafeInvoke(Func<YoloWorkerSmokeCandidate, bool> predicate, YoloWorkerSmokeCandidate candidate)
            => predicate != null && predicate(candidate);

        private static string SafeSecondaryText(
            Func<YoloWorkerSmokeCandidate, string> buildSecondaryText,
            YoloWorkerSmokeCandidate candidate)
        {
            return buildSecondaryText == null
                ? string.Empty
                : buildSecondaryText(candidate) ?? string.Empty;
        }

        private static int IndexOfCandidate(
            IReadOnlyList<YoloWorkerSmokeCandidate> candidates,
            YoloWorkerSmokeCandidate selected)
        {
            if (selected == null || candidates == null)
            {
                return -1;
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                if (ReferenceEquals(candidates[i], selected))
                {
                    return i;
                }
            }

            return -1;
        }
    }

    [Obsolete("Use CandidateReviewPresentationService.", false)]
    public sealed class WpfCandidateReviewPresentationService : CandidateReviewPresentationService
    {
    }

    public readonly struct WpfCandidateOverlapSource
    {
        public WpfCandidateOverlapSource(
            string label,
            Rectangle bounds,
            WpfObjectReviewItemRef currentObjectRef)
        {
            Label = label ?? string.Empty;
            Bounds = bounds;
            CurrentObjectRef = currentObjectRef;
        }

        public string Label { get; }

        public Rectangle Bounds { get; }

        public WpfObjectReviewItemRef CurrentObjectRef { get; }
    }

    public sealed class WpfCandidateReviewListPresentation
    {
        public WpfCandidateReviewListPresentation(
            IReadOnlyList<WpfCandidateReviewListItem> rows,
            string detail,
            YoloWorkerSmokeCandidate preferredCandidate)
        {
            Rows = rows ?? Array.Empty<WpfCandidateReviewListItem>();
            Detail = detail ?? string.Empty;
            PreferredCandidate = preferredCandidate;
        }

        public IReadOnlyList<WpfCandidateReviewListItem> Rows { get; }

        public string Detail { get; }

        public YoloWorkerSmokeCandidate PreferredCandidate { get; }
    }

    public sealed class WpfDetectionOverlayPresentation
    {
        public static readonly WpfDetectionOverlayPresentation Empty = new WpfDetectionOverlayPresentation(
            isEmpty: true,
            title: string.Empty,
            summary: string.Empty,
            selectedText: string.Empty,
            detail: string.Empty,
            status: WpfDetectionOverlayStatus.Review);

        public WpfDetectionOverlayPresentation(
            bool isEmpty,
            string title,
            string summary,
            string selectedText,
            string detail,
            WpfDetectionOverlayStatus status)
        {
            IsEmpty = isEmpty;
            Title = title ?? string.Empty;
            Summary = summary ?? string.Empty;
            SelectedText = selectedText ?? string.Empty;
            Detail = detail ?? string.Empty;
            Status = status;
        }

        public bool IsEmpty { get; }

        public string Title { get; }

        public string Summary { get; }

        public string SelectedText { get; }

        public string Detail { get; }

        public WpfDetectionOverlayStatus Status { get; }
    }
}
