using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using OpenVisionLab.ImageCanvas.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DrawingPointF = System.Drawing.PointF;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingRectangleF = System.Drawing.RectangleF;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Candidate presentation owns overlay, focus, and formatting helpers; command handlers stay focused on review mutations.
        private IReadOnlyList<RoiImageCanvasDetectionOverlay> BuildDetectionOverlays(IEnumerable<YoloWorkerSmokeCandidate> candidates)
        {
            YoloWorkerSmokeCandidate selectedCandidate = GetSelectedCandidate();
            return (candidates ?? Array.Empty<YoloWorkerSmokeCandidate>())
                .Where(candidate => candidate != null)
                .Select((candidate, index) =>
                {
                    IReadOnlyList<DrawingPointF> contourPoints = GetCandidateContourPoints(candidate);
                    bool isContourOnly = contourPoints.Count >= 3
                        || string.Equals(candidate.SegmentationType, "polygon", StringComparison.OrdinalIgnoreCase);
                    return new RoiImageCanvasDetectionOverlay
                    {
                        Index = index,
                        Bounds = GetClippedCandidateBounds(candidate),
                        ContourPoints = contourPoints,
                        IsContourOnly = isContourOnly,
                        Label = BuildDetectionOverlayLabel(candidate, index + 1),
                        IsSelected = ReferenceEquals(candidate, selectedCandidate),
                        Color = ReferenceEquals(candidate, selectedCandidate)
                            ? System.Drawing.Color.FromArgb(80, 180, 255)
                            : IsCandidateConfirmable(candidate)
                            ? System.Drawing.Color.FromArgb(36, 211, 102)
                            : System.Drawing.Color.FromArgb(255, 193, 7)
                    };
                })
                .Where(overlay => !overlay.Bounds.IsEmpty)
                .ToList();
        }

        private YoloWorkerSmokeCandidate GetSelectedCandidate()
            => WpfCandidateReviewSelectionService.GetSelectedCandidate(CandidateReviewViewModel?.SelectedCandidate);


        private void UpdateDetectionResultOverlay()
        {
            if (CanvasPanelViewModel == null)
            {
                return;
            }

            if (!ShouldShowInferenceOverlays())
            {
                CanvasPanelViewModel.ClearDetectionOverlay();
                return;
            }

            WpfDetectionOverlayPresentation presentation = candidateReviewPresentationService.BuildOverlayPresentation(
                activeImagePath,
                pendingDetectionCandidates,
                GetSelectedCandidate(),
                GetCandidateConfidenceFilter(),
                IsCandidateHighOverlap,
                IsCandidateConfirmable,
                BuildCandidateSecondaryText);
            if (presentation.IsEmpty)
            {
                CanvasPanelViewModel.ClearDetectionOverlay();
                return;
            }

            CanvasPanelViewModel.SetDetectionOverlay(
                presentation.Title,
                presentation.Summary,
                presentation.SelectedText,
                presentation.Detail,
                presentation.Status);
        }

        private string BuildDetectionOverlayLabel(YoloWorkerSmokeCandidate candidate, int fallbackIndex)
        {
            return WpfCandidateReviewPresenter.BuildDetectionOverlayLabel(candidate, fallbackIndex);
        }

        private DrawingRectangle GetClippedCandidateBounds(YoloWorkerSmokeCandidate candidate)
        {
            if (candidate == null)
            {
                return DrawingRectangle.Empty;
            }

            DrawingRectangle bounds = candidate.ToRectangle();
            if (bounds.IsEmpty || activeImageSize.IsEmpty)
            {
                return bounds;
            }

            return DrawingRectangle.Intersect(
                bounds,
                new DrawingRectangle(0, 0, activeImageSize.Width, activeImageSize.Height));
        }

        private IReadOnlyList<DrawingPointF> GetCandidateContourPoints(YoloWorkerSmokeCandidate candidate)
        {
            if (candidate == null || activeImageSize.IsEmpty)
            {
                return Array.Empty<DrawingPointF>();
            }

            IReadOnlyList<DetectionPolygonPoint> sourcePoints = candidate.PolygonPoints;
            bool normalized = sourcePoints == null || sourcePoints.Count < 3;
            if (normalized)
            {
                sourcePoints = candidate.NormalizedPolygonPoints;
            }

            if (sourcePoints == null || sourcePoints.Count < 3)
            {
                return Array.Empty<DrawingPointF>();
            }

            List<DrawingPointF> points = sourcePoints
                .Where(point => point != null && float.IsFinite(point.X) && float.IsFinite(point.Y))
                .Select(point => new DrawingPointF(
                    Math.Clamp(normalized ? point.X * activeImageSize.Width : point.X, 0F, activeImageSize.Width - 1F),
                    Math.Clamp(normalized ? point.Y * activeImageSize.Height : point.Y, 0F, activeImageSize.Height - 1F)))
                .ToList();
            return points.Count >= 3 ? points : Array.Empty<DrawingPointF>();
        }


    }
}
