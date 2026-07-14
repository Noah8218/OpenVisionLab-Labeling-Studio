using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using OpenVisionLab.ImageCanvas.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingRectangleF = System.Drawing.RectangleF;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Formatting and overlap helpers are pure presentation utilities used by review rows and overlays.
        private static YoloWorkerSmokeCandidate ToSmokeCandidate(DefectInfo defect, int index)
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
                SegmentationType = defect?.SegmentationType ?? string.Empty,
                PolygonPoints = defect?.PolygonPoints?.ToArray() ?? Array.Empty<DetectionPolygonPoint>(),
                NormalizedPolygonPoints = defect?.NormalizedPolygonPoints?.ToArray() ?? Array.Empty<DetectionPolygonPoint>()
            };
        }

        private string FormatCandidate(YoloWorkerSmokeCandidate candidate)
        {
            return WpfCandidateReviewPresenter.FormatCandidate(candidate, GetClippedCandidateBounds(candidate));
        }

        private string FormatCandidateDetail(YoloWorkerSmokeCandidate candidate)
        {
            DrawingRectangle bounds = GetClippedCandidateBounds(candidate);
            return WpfCandidateReviewPresenter.BuildDetail(
                candidate,
                bounds,
                GetCandidateOverlapInfo(bounds),
                GetMinimumDetectionConfidence());
        }

        private string BuildCandidateSecondaryText(YoloWorkerSmokeCandidate candidate)
        {
            DrawingRectangle bounds = GetClippedCandidateBounds(candidate);
            return WpfCandidateReviewPresenter.BuildSecondaryText(
                candidate,
                bounds,
                GetCandidateOverlapInfo(bounds),
                GetMinimumDetectionConfidence());
        }

        private string BuildCandidateCurrentObjectComparison(YoloWorkerSmokeCandidate candidate)
        {
            DrawingRectangle bounds = GetClippedCandidateBounds(candidate);
            return WpfCandidateReviewPresenter.BuildCurrentObjectComparison(
                bounds,
                GetCandidateOverlapInfo(bounds));
        }

        private (string label, DrawingRectangle bounds, double iou, WpfObjectReviewItemRef currentObjectRef) FindBestCurrentObjectOverlapInfo(DrawingRectangle candidateBounds)
        {
            string bestLabel = string.Empty;
            DrawingRectangle bestBounds = DrawingRectangle.Empty;
            double bestIou = 0D;
            WpfObjectReviewItemRef bestRef = null;

            for (int i = 0; i < manualRois.Count; i++)
            {
                double iou = CalculateIntersectionOverUnion(candidateBounds, manualRois[i]);
                if (iou > bestIou)
                {
                    bestIou = iou;
                    bestLabel = $"수동 {GetManualRoiClassName(i)}";
                    bestBounds = manualRois[i];
                    bestRef = WpfObjectReviewItemRef.Manual(i, GetManualRoiOverlayId(i));
                }
            }

            for (int i = 0; i < confirmedDetectionCandidates.Count; i++)
            {
                DrawingRectangle confirmedBounds = GetClippedCandidateBounds(confirmedDetectionCandidates[i]);
                double iou = CalculateIntersectionOverUnion(candidateBounds, confirmedBounds);
                if (iou > bestIou)
                {
                    bestIou = iou;
                    bestLabel = $"AI {GetCandidateClassName(confirmedDetectionCandidates[i])}";
                    bestBounds = confirmedBounds;
                    bestRef = WpfObjectReviewItemRef.ConfirmedAi(i);
                }
            }

            return bestIou <= 0D ? (string.Empty, DrawingRectangle.Empty, 0D, null) : (bestLabel, bestBounds, bestIou, bestRef);
        }

        private static string FormatBoundsCompact(DrawingRectangle bounds)
        {
            return WpfCandidateReviewPresenter.FormatBoundsCompact(bounds);
        }

        private static double CalculateIntersectionOverUnion(DrawingRectangle first, DrawingRectangle second)
        {
            if (first.IsEmpty || second.IsEmpty)
            {
                return 0D;
            }

            DrawingRectangle intersection = DrawingRectangle.Intersect(first, second);
            if (intersection.IsEmpty)
            {
                return 0D;
            }

            double intersectionArea = intersection.Width * intersection.Height;
            double unionArea = (first.Width * first.Height) + (second.Width * second.Height) - intersectionArea;
            return unionArea <= 0D ? 0D : intersectionArea / unionArea;
        }

        private static string GetCandidateClassName(YoloWorkerSmokeCandidate candidate)
        {
            return WpfCandidateReviewPresenter.GetClassName(candidate);
        }

        private static string FormatCandidateConfidence(YoloWorkerSmokeCandidate candidate, string format)
        {
            return WpfCandidateReviewPresenter.FormatConfidence(candidate, format);
        }
    }
}
