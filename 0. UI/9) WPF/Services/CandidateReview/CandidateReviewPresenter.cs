using MahApps.Metro.IconPacks;
using OpenVisionLab;
using MvcVisionSystem._1._Core;
using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;
using MediaSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace MvcVisionSystem
{
    public readonly struct WpfCandidateOverlapInfo
    {
        public WpfCandidateOverlapInfo(string label, Rectangle bounds, double iou, WpfObjectReviewItemRef currentObjectRef = null)
        {
            Label = label ?? string.Empty;
            Bounds = bounds;
            Iou = iou;
            CurrentObjectRef = currentObjectRef;
        }

        public string Label { get; }

        public Rectangle Bounds { get; }

        public double Iou { get; }

        public WpfObjectReviewItemRef CurrentObjectRef { get; }

        public bool HasCurrentObject => CurrentObjectRef != null;
    }

    public readonly struct WpfCandidateComparisonPresentation
    {
        public WpfCandidateComparisonPresentation(
            string candidateText,
            string currentText,
            string overlapText,
            bool isHighOverlap)
            : this(candidateText, currentText, overlapText, string.Empty, isHighOverlap)
        {
        }

        public WpfCandidateComparisonPresentation(
            string candidateText,
            string currentText,
            string overlapText,
            string decisionText,
            bool isHighOverlap)
            : this(candidateText, currentText, overlapText, decisionText, isHighOverlap, string.Empty)
        {
        }

        public WpfCandidateComparisonPresentation(
            string candidateText,
            string currentText,
            string overlapText,
            string decisionText,
            bool isHighOverlap,
            string selectionSummaryText)
        {
            CandidateText = candidateText ?? string.Empty;
            CurrentText = currentText ?? string.Empty;
            OverlapText = overlapText ?? string.Empty;
            DecisionText = decisionText ?? string.Empty;
            IsHighOverlap = isHighOverlap;
            SelectionSummaryText = selectionSummaryText ?? string.Empty;
        }

        public string CandidateText { get; }

        public string CurrentText { get; }

        public string OverlapText { get; }

        public string DecisionText { get; }

        public bool IsHighOverlap { get; }

        public string SelectionSummaryText { get; }
    }

    public static class CandidateReviewPresenter
    {
        private const double HighOverlapIou = 0.5D;

        public static WpfCandidateReviewListItem BuildListItem(
            YoloWorkerSmokeCandidate candidate,
            int displayIndex,
            Rectangle bounds,
            WpfCandidateOverlapInfo overlap,
            float minimumConfidence,
            bool resultOnly = false)
        {
            if (resultOnly)
            {
                string detail = FormatCandidate(candidate, bounds) + Environment.NewLine + OpenVisionLanguageService.T("WpfInference.ResultOnly.Panel.Detail");
                return new WpfCandidateReviewListItem($"{displayIndex}. {GetClassName(candidate)}  {FormatConfidence(candidate, "P1")}", BuildSecondaryText(candidate, bounds, overlap, minimumConfidence, true), detail, candidate, PackIconMaterialKind.EyeOutline, MediaBrushes.DeepSkyBlue);
            }
            return new WpfCandidateReviewListItem(
                $"{displayIndex}. {GetClassName(candidate)}  {FormatConfidence(candidate, "P1")}",
                BuildSecondaryText(candidate, bounds, overlap, minimumConfidence),
                BuildDetail(candidate, bounds, overlap, minimumConfidence),
                candidate,
                GetIconKind(candidate, bounds, overlap, minimumConfidence),
                GetStateBrush(candidate, bounds, overlap, minimumConfidence));
        }

        public static string BuildSelectedCandidateStatus(YoloWorkerSmokeCandidate candidate, Rectangle bounds)
        {
            string confidence = FormatConfidence(candidate, "P1");
            return candidate.ImageLevel
                ? string.Format(CultureInfo.CurrentCulture, OpenVisionLanguageService.T("WpfCandidate.StatusBar.ImageLevel"), candidate.ClassName, confidence)
                : bounds.IsEmpty
                ? string.Format(CultureInfo.CurrentCulture, OpenVisionLanguageService.T("WpfCandidate.StatusBar.OutsideImage"), candidate.ClassName, confidence)
                : string.Format(CultureInfo.CurrentCulture, OpenVisionLanguageService.T("WpfCandidate.StatusBar.Bounds"), candidate.ClassName, confidence, FormatBoundsCompact(bounds));
        }

        public static string BuildDetectionOverlayLabel(YoloWorkerSmokeCandidate candidate, int fallbackIndex)
        {
            int index = candidate?.Index > 0 ? candidate.Index : fallbackIndex;
            string className = ToCanvasSafeText(GetClassName(candidate));
            return $"AI {index} {className} {FormatConfidence(candidate, "P1")}";
        }

        public static string FormatCandidate(YoloWorkerSmokeCandidate candidate, Rectangle bounds)
        {
            if (candidate == null)
            {
                return "-";
            }

            if (IsImageLevel(candidate))
            {
                if (IsPatchCore(candidate) && !bounds.IsEmpty)
                {
                    return string.Format(CultureInfo.CurrentCulture, OpenVisionLanguageService.T("WpfInference.Text.CandidateReviewPresenter.LocationHint"), GetClassName(candidate), FormatConfidence(candidate, "P1"), FormatBoundsCompact(bounds));
                }

                return string.Format(CultureInfo.CurrentCulture, OpenVisionLanguageService.T("WpfInference.Text.CandidateReviewPresenter.WholeImageDecision"), GetClassName(candidate), FormatConfidence(candidate, "P1"));
            }

            return bounds.IsEmpty
                ? string.Format(CultureInfo.CurrentCulture, OpenVisionLanguageService.T("WpfInference.Text.CandidateReviewPresenter.OutsideImage"), GetClassName(candidate), FormatConfidence(candidate, "P1"))
                : $"{GetClassName(candidate)}  {FormatConfidence(candidate, "P1")}  {FormatBoundsCompact(bounds)}";
        }

        public static string BuildDetail(
            YoloWorkerSmokeCandidate candidate,
            Rectangle bounds,
            WpfCandidateOverlapInfo overlap,
            float minimumConfidence)
        {
            if (candidate == null)
            {
                return OpenVisionLanguageService.T("WpfCandidate.Detail.NoSelection");
            }

            string confidence = FormatConfidence(candidate, "P2");
            string threshold = minimumConfidence.ToString("P0", CultureInfo.CurrentCulture);
            if (IsImageLevel(candidate))
            {
                if (IsPatchCore(candidate))
                {
                    string score = candidate.AnomalyScore?.ToString("0.####", CultureInfo.CurrentCulture) ?? "-";
                    string anomalyThreshold = candidate.AnomalyThreshold?.ToString("0.####", CultureInfo.CurrentCulture) ?? "-";
                    string location = bounds.IsEmpty ? OpenVisionLanguageService.T("WpfCandidate.Detail.NoLocation") : FormatBoundsCompact(bounds);
                    string heatmap = string.IsNullOrWhiteSpace(candidate.HeatmapPath) ? OpenVisionLanguageService.T("WpfCandidate.Detail.NoHeatmap") : candidate.HeatmapPath;
                    return string.Format(CultureInfo.CurrentCulture, OpenVisionLanguageService.T("WpfCandidate.Detail.PatchCore"), GetClassName(candidate), confidence, score, anomalyThreshold, location, heatmap);
                }

                return string.Format(CultureInfo.CurrentCulture, OpenVisionLanguageService.T("WpfCandidate.Detail.ImageLevel"), GetClassName(candidate), confidence, threshold);
            }

            string boundsText = bounds.IsEmpty
                ? OpenVisionLanguageService.T("WpfCandidate.Detail.OutsideImage")
                : FormatBoundsCompact(bounds);
            string status = IsConfirmable(candidate, bounds, overlap, minimumConfidence)
                ? OpenVisionLanguageService.T("WpfCandidate.Status.ReadyToAccept")
                : IsHighOverlap(overlap)
                    ? OpenVisionLanguageService.T("WpfCandidate.Status.DuplicateExcluded")
                    : OpenVisionLanguageService.T("WpfCandidate.Status.NeedsCheck");

            string scoreText = IsSmartMask(candidate)
                ? string.Format(CultureInfo.CurrentCulture, OpenVisionLanguageService.T("WpfCandidate.Detail.SmartMaskMethod"), confidence)
                : string.Format(CultureInfo.CurrentCulture, OpenVisionLanguageService.T("WpfCandidate.Detail.ConfidenceThreshold"), confidence, threshold);
            string boundaryText = IsSmartMask(candidate)
                ? string.Format(CultureInfo.CurrentCulture, OpenVisionLanguageService.T("WpfCandidate.Detail.BoundaryPoints"), GetBoundaryPointCount(candidate))
                : string.Empty;
            return string.Format(CultureInfo.CurrentCulture, OpenVisionLanguageService.T("WpfCandidate.Detail.Object"), GetClassName(candidate), scoreText, boundsText, boundaryText, status, BuildCurrentObjectComparison(bounds, overlap));
        }

        public static WpfCandidateComparisonPresentation BuildComparison(
            YoloWorkerSmokeCandidate candidate,
            Rectangle bounds,
            WpfCandidateOverlapInfo overlap)
        {
            if (candidate == null)
            {
                return new WpfCandidateComparisonPresentation(
                    "-",
                    "-",
                    "\uACB9\uCE68\n0%",
                    OpenVisionLanguageService.T("WpfCandidate.Comparison.NoCandidate"),
                    false,
                    BuildSelectionSummary(null, bounds, overlap, false));
            }

            string confidence = FormatConfidence(candidate, "P1");
            if (IsImageLevel(candidate))
            {
                return new WpfCandidateComparisonPresentation(
                    $"{GetClassName(candidate)} {confidence}\n이미지 전체 판정",
                    "현재 이미지\nOK/NG 판정",
                    "위치·겹침\n해당 없음",
                    "이미지 전체 분류 후보입니다. 오른쪽 OK/NG 판정과 신뢰도를 확인하세요.",
                    false,
                    BuildSelectionSummary(candidate, bounds, overlap, false));
            }

            string candidateText = bounds.IsEmpty
                ? $"{GetClassName(candidate)} {confidence}\n\uC774\uBBF8\uC9C0 \uBC16"
                : $"{GetClassName(candidate)} {confidence}\n{FormatBoundsCompact(bounds)}";
            string currentText = string.IsNullOrWhiteSpace(overlap.Label)
                ? "\uACB9\uCE68 \uC5C6\uC74C\n-"
                : $"{overlap.Label}\n{FormatBoundsCompact(overlap.Bounds)}";
            bool highOverlap = IsHighOverlap(overlap);
            string overlapText = highOverlap
                ? $"\uC911\uBCF5\n{overlap.Iou.ToString("P0", CultureInfo.CurrentCulture)}"
                : $"\uACB9\uCE68\n{overlap.Iou.ToString("P0", CultureInfo.CurrentCulture)}";
            string decisionText = BuildComparisonDecision(bounds, overlap, highOverlap);
            string selectionSummaryText = BuildSelectionSummary(candidate, bounds, overlap, highOverlap);

            return new WpfCandidateComparisonPresentation(candidateText, currentText, overlapText, decisionText, highOverlap, selectionSummaryText);
        }

        public static string BuildSelectionSummary(
            YoloWorkerSmokeCandidate candidate,
            Rectangle bounds,
            WpfCandidateOverlapInfo overlap,
            bool highOverlap)
        {
            if (candidate == null)
            {
                return OpenVisionLanguageService.T("WpfCandidate.Selection.None");
            }

            string candidateText = BuildCandidateSummaryName(candidate);
            if (IsImageLevel(candidate))
            {
                return string.Format(CultureInfo.CurrentCulture, OpenVisionLanguageService.T("WpfCandidate.Selection.ImageLevel"), candidateText);
            }

            if (bounds.IsEmpty)
            {
                return string.Format(CultureInfo.CurrentCulture, OpenVisionLanguageService.T("WpfCandidate.Selection.OutsideImage"), candidateText);
            }

            if (string.IsNullOrWhiteSpace(overlap.Label))
            {
                return string.Format(CultureInfo.CurrentCulture, OpenVisionLanguageService.T("WpfCandidate.Selection.NoOverlap"), candidateText);
            }

            string overlapText = overlap.Iou.ToString("P0", CultureInfo.CurrentCulture);
            return string.Format(CultureInfo.CurrentCulture,
                OpenVisionLanguageService.T(highOverlap ? "WpfCandidate.Selection.Duplicate" : "WpfCandidate.Selection.NewObject"),
                candidateText, overlap.Label, overlapText);
        }

        private static string BuildComparisonDecision(Rectangle bounds, WpfCandidateOverlapInfo overlap, bool highOverlap)
        {
            if (bounds.IsEmpty)
            {
                return "\uC774\uBBF8\uC9C0 \uBC16 \uD6C4\uBCF4: \uD655\uC815\uD558\uC9C0 \uB9D0\uACE0 \uAC74\uB108\uB6F0\uC138\uC694.";
            }

            if (highOverlap)
            {
                return "\uC911\uBCF5 \uAC00\uB2A5: \uAE30\uC874 \uB77C\uBCA8 \uBC84\uD2BC\uC73C\uB85C \uACB9\uCE58\uB294 \uB77C\uBCA8\uC744 \uD655\uC778\uD558\uACE0, \uAC19\uC740 \uAC1D\uCCB4\uBA74 \uAC74\uB108\uB6F0\uC138\uC694.";
            }

            if (!string.IsNullOrWhiteSpace(overlap.Label))
            {
                return "\uBD80\uBD84 \uACB9\uCE68: AI \uD6C4\uBCF4\uC640 \uAE30\uC874 \uB77C\uBCA8\uC744 \uBE44\uAD50\uD55C \uB4A4, \uC0C8 \uAC1D\uCCB4\uBA74 \uD655\uC815\uD558\uC138\uC694.";
            }

            return "\uC0C8 \uD6C4\uBCF4: \uACB9\uCE58\uB294 \uAE30\uC874 \uB77C\uBCA8\uC774 \uC5C6\uC2B5\uB2C8\uB2E4. \uB9DE\uC73C\uBA74 \uD655\uC815, \uC544\uB2C8\uBA74 \uAC74\uB108\uB6F0\uC138\uC694.";
        }

        public static string BuildSecondaryText(
            YoloWorkerSmokeCandidate candidate,
            Rectangle bounds,
            WpfCandidateOverlapInfo overlap,
            float minimumConfidence,
            bool resultOnly = false)
        {
            if (resultOnly) return FormatCandidate(candidate, bounds) + " / " + OpenVisionLanguageService.T("WpfInference.Purpose.ResultOnly");
            if (candidate == null)
            {
                return OpenVisionLanguageService.T("WpfCandidate.Status.NeedsCheck");
            }

            if (IsImageLevel(candidate))
            {
                if (IsPatchCore(candidate) && !bounds.IsEmpty)
                {
                    return string.Format(CultureInfo.CurrentCulture, OpenVisionLanguageService.T("WpfCandidate.Secondary.PatchCoreLocation"), FormatBoundsCompact(bounds));
                }

                return OpenVisionLanguageService.T("WpfCandidate.Secondary.ImageLevel");
            }

            string status = IsConfirmable(candidate, bounds, overlap, minimumConfidence)
                ? OpenVisionLanguageService.T("WpfCandidate.Status.ReadyToAccept")
                : IsHighOverlap(overlap)
                    ? OpenVisionLanguageService.T("WpfCandidate.Status.PossibleDuplicate")
                    : OpenVisionLanguageService.T("WpfCandidate.Status.NeedsCheck");

            string geometry = IsSmartMask(candidate)
                ? string.Format(CultureInfo.CurrentCulture, OpenVisionLanguageService.T("WpfCandidate.Secondary.BoundaryPoints"), GetBoundaryPointCount(candidate))
                : FormatBoundsCompact(bounds);
            return bounds.IsEmpty
                ? string.Format(CultureInfo.CurrentCulture, OpenVisionLanguageService.T("WpfCandidate.Secondary.OutsideImage"), status)
                : $"{geometry} / {status}";
        }

        internal static int GetBoundaryPointCount(YoloWorkerSmokeCandidate candidate)
            => candidate?.PolygonPoints?.Count >= 3
                ? candidate.PolygonPoints.Count
                : candidate?.NormalizedPolygonPoints?.Count ?? 0;

        public static string BuildCurrentObjectComparison(Rectangle bounds, WpfCandidateOverlapInfo overlap)
        {
            if (bounds.IsEmpty)
            {
                return OpenVisionLanguageService.T("WpfCandidate.Detail.CurrentLabelUnavailable");
            }

            return string.IsNullOrWhiteSpace(overlap.Label)
                ? OpenVisionLanguageService.T("WpfCandidate.Detail.CurrentLabelNoOverlap")
                : string.Format(CultureInfo.CurrentCulture, OpenVisionLanguageService.T("WpfCandidate.Detail.CurrentLabelOverlap"), overlap.Label, overlap.Iou.ToString("P0", CultureInfo.CurrentCulture));
        }

        public static string BuildConfirmDisabledHint(
            YoloWorkerSmokeCandidate candidate,
            Rectangle bounds,
            WpfCandidateOverlapInfo overlap)
        {
            if (candidate == null)
            {
                return "\uD655\uC815\uD560 AI \uD6C4\uBCF4\uB97C \uC120\uD0DD\uD558\uC138\uC694.";
            }

            if (IsImageLevel(candidate))
            {
                return "이미지 전체 분류 후보는 박스 라벨로 확정하지 않습니다. 오른쪽 OK/NG 판정을 확인하세요.";
            }

            if (bounds.IsEmpty)
            {
                return "\uC774\uBBF8\uC9C0 \uC601\uC5ED \uBC16 \uD6C4\uBCF4\uB294 \uD655\uC815\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
            }

            if (IsHighOverlap(overlap))
            {
                return "\uD604\uC7AC \uB77C\uBCA8\uACFC 50% \uC774\uC0C1 \uACB9\uCCD0 \uC911\uBCF5 \uD655\uC815\uC5D0\uC11C \uC81C\uC678\uD569\uB2C8\uB2E4. \uAE30\uC874 \uB77C\uBCA8\uC744 \uD655\uC778\uD558\uAC70\uB098 \uD6C4\uBCF4\uB97C \uAC74\uB108\uB6F0\uC138\uC694.";
            }

            return "\uC2E0\uB8B0\uB3C4 \uAE30\uC900\uBCF4\uB2E4 \uB0AE\uC544 \uD655\uC815\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
        }

        public static PackIconMaterialKind GetIconKind(
            YoloWorkerSmokeCandidate candidate,
            Rectangle bounds,
            WpfCandidateOverlapInfo overlap,
            float minimumConfidence)
        {
            if (candidate == null || bounds.IsEmpty || IsHighOverlap(overlap))
            {
                if (IsImageLevel(candidate))
                {
                    return PackIconMaterialKind.ImageCheckOutline;
                }

                return PackIconMaterialKind.AlertCircleOutline;
            }

            return IsConfirmable(candidate, bounds, overlap, minimumConfidence)
                ? PackIconMaterialKind.CheckCircleOutline
                : PackIconMaterialKind.EyeOutline;
        }

        public static MediaBrush GetStateBrush(
            YoloWorkerSmokeCandidate candidate,
            Rectangle bounds,
            WpfCandidateOverlapInfo overlap,
            float minimumConfidence)
        {
            if (candidate == null || bounds.IsEmpty)
            {
                if (IsImageLevel(candidate))
                {
                    return new MediaSolidColorBrush(MediaColor.FromRgb(56, 189, 248));
                }

                return new MediaSolidColorBrush(MediaColor.FromRgb(239, 68, 68));
            }

            if (IsHighOverlap(overlap))
            {
                return new MediaSolidColorBrush(MediaColor.FromRgb(245, 197, 66));
            }

            return IsConfirmable(candidate, bounds, overlap, minimumConfidence)
                ? new MediaSolidColorBrush(MediaColor.FromRgb(34, 197, 94))
                : new MediaSolidColorBrush(MediaColor.FromRgb(245, 158, 11));
        }

        public static bool IsConfirmable(
            YoloWorkerSmokeCandidate candidate,
            Rectangle bounds,
            WpfCandidateOverlapInfo overlap,
            float minimumConfidence)
        {
            return candidate != null
                && candidate.Confidence >= minimumConfidence
                && !bounds.IsEmpty
                && !IsHighOverlap(overlap);
        }

        public static bool IsHighOverlap(WpfCandidateOverlapInfo overlap)
            => overlap.Iou >= HighOverlapIou;

        public static string GetClassName(YoloWorkerSmokeCandidate candidate)
            => string.IsNullOrWhiteSpace(candidate?.ClassName) ? "Defect" : candidate.ClassName;

        private static string ToCanvasSafeText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "Defect";
            }

            string trimmed = text.Trim();
            return trimmed.All(ch => ch >= 0x20 && ch <= 0x7E) ? trimmed : "Class";
        }

        public static string FormatConfidence(YoloWorkerSmokeCandidate candidate, string format)
            => IsSmartMask(candidate)
                ? OpenVisionLanguageService.T("WpfCandidate.Confidence.SmartMaskLocation")
                : (candidate?.Confidence ?? 0D).ToString(format, CultureInfo.CurrentCulture);

        internal static bool IsSmartMask(YoloWorkerSmokeCandidate candidate)
            => string.Equals(candidate?.CandidateType, "smart-mask", StringComparison.OrdinalIgnoreCase);

        private static bool IsImageLevel(YoloWorkerSmokeCandidate candidate)
            => candidate?.ImageLevel == true;

        private static bool IsPatchCore(YoloWorkerSmokeCandidate candidate)
            => string.Equals(candidate?.PredictionType, "patchcore", StringComparison.OrdinalIgnoreCase);

        public static string FormatBoundsCompact(Rectangle bounds)
            => bounds.IsEmpty ? "-" : string.Format(CultureInfo.CurrentCulture, OpenVisionLanguageService.T("WpfObjectReview.Bounds.CompactFormat"), bounds.Width, bounds.Height, bounds.X, bounds.Y);

        public static double CalculateIntersectionOverUnion(Rectangle first, Rectangle second)
        {
            if (first.IsEmpty || second.IsEmpty)
            {
                return 0D;
            }

            Rectangle intersection = Rectangle.Intersect(first, second);
            if (intersection.IsEmpty)
            {
                return 0D;
            }

            double intersectionArea = intersection.Width * intersection.Height;
            double unionArea = (first.Width * first.Height) + (second.Width * second.Height) - intersectionArea;
            return unionArea <= 0D ? 0D : intersectionArea / unionArea;
        }

        private static string BuildCandidateSummaryName(YoloWorkerSmokeCandidate candidate)
        {
            string indexText = candidate?.Index > 0 ? $"{candidate.Index}. " : string.Empty;
            return $"{indexText}{GetClassName(candidate)} {FormatConfidence(candidate, "P1")}";
        }
    }

    [Obsolete("Use CandidateReviewPresenter.", false)]
    public static class WpfCandidateReviewPresenter
    {
        public static WpfCandidateReviewListItem BuildListItem(
            YoloWorkerSmokeCandidate candidate,
            int displayIndex,
            Rectangle bounds,
            WpfCandidateOverlapInfo overlap,
            float minimumConfidence)
            => CandidateReviewPresenter.BuildListItem(candidate, displayIndex, bounds, overlap, minimumConfidence);

        public static string BuildDetectionOverlayLabel(YoloWorkerSmokeCandidate candidate, int fallbackIndex)
            => CandidateReviewPresenter.BuildDetectionOverlayLabel(candidate, fallbackIndex);

        public static string FormatCandidate(YoloWorkerSmokeCandidate candidate, Rectangle bounds)
            => CandidateReviewPresenter.FormatCandidate(candidate, bounds);

        public static string BuildDetail(
            YoloWorkerSmokeCandidate candidate,
            Rectangle bounds,
            WpfCandidateOverlapInfo overlap,
            float minimumConfidence)
            => CandidateReviewPresenter.BuildDetail(candidate, bounds, overlap, minimumConfidence);

        public static WpfCandidateComparisonPresentation BuildComparison(
            YoloWorkerSmokeCandidate candidate,
            Rectangle bounds,
            WpfCandidateOverlapInfo overlap)
            => CandidateReviewPresenter.BuildComparison(candidate, bounds, overlap);

        public static string BuildSelectionSummary(
            YoloWorkerSmokeCandidate candidate,
            Rectangle bounds,
            WpfCandidateOverlapInfo overlap,
            bool highOverlap)
            => CandidateReviewPresenter.BuildSelectionSummary(candidate, bounds, overlap, highOverlap);

        public static string BuildSecondaryText(
            YoloWorkerSmokeCandidate candidate,
            Rectangle bounds,
            WpfCandidateOverlapInfo overlap,
            float minimumConfidence)
            => CandidateReviewPresenter.BuildSecondaryText(candidate, bounds, overlap, minimumConfidence);

        public static string BuildCurrentObjectComparison(Rectangle bounds, WpfCandidateOverlapInfo overlap)
            => CandidateReviewPresenter.BuildCurrentObjectComparison(bounds, overlap);

        public static string BuildConfirmDisabledHint(
            YoloWorkerSmokeCandidate candidate,
            Rectangle bounds,
            WpfCandidateOverlapInfo overlap)
            => CandidateReviewPresenter.BuildConfirmDisabledHint(candidate, bounds, overlap);

        public static PackIconMaterialKind GetIconKind(
            YoloWorkerSmokeCandidate candidate,
            Rectangle bounds,
            WpfCandidateOverlapInfo overlap,
            float minimumConfidence)
            => CandidateReviewPresenter.GetIconKind(candidate, bounds, overlap, minimumConfidence);

        public static MediaBrush GetStateBrush(
            YoloWorkerSmokeCandidate candidate,
            Rectangle bounds,
            WpfCandidateOverlapInfo overlap,
            float minimumConfidence)
            => CandidateReviewPresenter.GetStateBrush(candidate, bounds, overlap, minimumConfidence);

        public static bool IsConfirmable(
            YoloWorkerSmokeCandidate candidate,
            Rectangle bounds,
            WpfCandidateOverlapInfo overlap,
            float minimumConfidence)
            => CandidateReviewPresenter.IsConfirmable(candidate, bounds, overlap, minimumConfidence);

        public static bool IsHighOverlap(WpfCandidateOverlapInfo overlap)
            => CandidateReviewPresenter.IsHighOverlap(overlap);

        public static string GetClassName(YoloWorkerSmokeCandidate candidate)
            => CandidateReviewPresenter.GetClassName(candidate);

        public static string FormatConfidence(YoloWorkerSmokeCandidate candidate, string format)
            => CandidateReviewPresenter.FormatConfidence(candidate, format);

        public static string FormatBoundsCompact(Rectangle bounds)
            => CandidateReviewPresenter.FormatBoundsCompact(bounds);

        public static double CalculateIntersectionOverUnion(Rectangle first, Rectangle second)
            => CandidateReviewPresenter.CalculateIntersectionOverUnion(first, second);
    }
}
