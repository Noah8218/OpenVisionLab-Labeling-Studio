using MahApps.Metro.IconPacks;
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

    public static class WpfCandidateReviewPresenter
    {
        private const double HighOverlapIou = 0.5D;

        public static WpfCandidateReviewListItem BuildListItem(
            YoloWorkerSmokeCandidate candidate,
            int displayIndex,
            Rectangle bounds,
            WpfCandidateOverlapInfo overlap,
            float minimumConfidence)
        {
            return new WpfCandidateReviewListItem(
                $"{displayIndex}. {GetClassName(candidate)}  {FormatConfidence(candidate, "P1")}",
                BuildSecondaryText(candidate, bounds, overlap, minimumConfidence),
                BuildDetail(candidate, bounds, overlap, minimumConfidence),
                candidate,
                GetIconKind(candidate, bounds, overlap, minimumConfidence),
                GetStateBrush(candidate, bounds, overlap, minimumConfidence));
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

            return bounds.IsEmpty
                ? $"{GetClassName(candidate)}  {FormatConfidence(candidate, "P1")}  \uC774\uBBF8\uC9C0 \uBC16"
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
                return "\uC120\uD0DD\uB41C AI \uD6C4\uBCF4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.";
            }

            string confidence = FormatConfidence(candidate, "P2");
            string threshold = minimumConfidence.ToString("P0", CultureInfo.CurrentCulture);
            string boundsText = bounds.IsEmpty
                ? "\uC774\uBBF8\uC9C0 \uBC16"
                : FormatBoundsCompact(bounds);
            string status = IsConfirmable(candidate, bounds, overlap, minimumConfidence)
                ? "\uD655\uC815 \uAC00\uB2A5"
                : IsHighOverlap(overlap)
                    ? "\uC911\uBCF5 \uAC00\uB2A5 - \uD655\uC815 \uC81C\uC678"
                    : "\uAC80\uD1A0 \uD544\uC694";

            string scoreText = IsSmartMask(candidate)
                ? $"\uC0DD\uC131 \uBC29\uC2DD {confidence} / \uC2E0\uB8B0\uB3C4 \uC810\uC218 \uC5C6\uC74C"
                : $"\uC2E0\uB8B0\uB3C4 {confidence} / \uAE30\uC900 {threshold}";
            return $"{GetClassName(candidate)} / {scoreText}\n\uC88C\uD45C: {boundsText}\n\uC0C1\uD0DC: {status}\n{BuildCurrentObjectComparison(bounds, overlap)}";
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
                    "\uD6C4\uBCF4 \uC5C6\uC74C: \uAC80\uD1A0\uD560 AI \uD6C4\uBCF4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.",
                    false,
                    "\uC120\uD0DD: AI \uD6C4\uBCF4 \uC5C6\uC74C");
            }

            string confidence = FormatConfidence(candidate, "P1");
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
                return "\uC120\uD0DD: AI \uD6C4\uBCF4 \uC5C6\uC74C";
            }

            string candidateText = BuildCandidateSummaryName(candidate);
            if (bounds.IsEmpty)
            {
                return $"\uC120\uD0DD: {candidateText} / \uC774\uBBF8\uC9C0 \uBC16 / \uC870\uCE58: \uC2A4\uD0B5";
            }

            if (string.IsNullOrWhiteSpace(overlap.Label))
            {
                return $"\uC120\uD0DD: {candidateText} / \uD604\uC7AC \uB77C\uBCA8: \uACB9\uCE68 \uC5C6\uC74C / \uC870\uCE58: \uB9DE\uC73C\uBA74 \uD655\uC815";
            }

            string overlapText = overlap.Iou.ToString("P0", CultureInfo.CurrentCulture);
            return highOverlap
                ? $"\uC120\uD0DD: {candidateText} / \uD604\uC7AC \uB77C\uBCA8: {overlap.Label} \uACB9\uCE68 {overlapText} / \uC870\uCE58: \uAE30\uC874 \uB77C\uBCA8 \uD655\uC778 \uD6C4 \uAC19\uC73C\uBA74 \uC2A4\uD0B5"
                : $"\uC120\uD0DD: {candidateText} / \uD604\uC7AC \uB77C\uBCA8: {overlap.Label} \uACB9\uCE68 {overlapText} / \uC870\uCE58: \uC0C8 \uAC1D\uCCB4\uBA74 \uD655\uC815";
        }

        private static string BuildComparisonDecision(Rectangle bounds, WpfCandidateOverlapInfo overlap, bool highOverlap)
        {
            if (bounds.IsEmpty)
            {
                return "\uC774\uBBF8\uC9C0 \uBC16 \uD6C4\uBCF4: \uD655\uC815\uD558\uC9C0 \uB9D0\uACE0 \uC2A4\uD0B5\uD558\uC138\uC694.";
            }

            if (highOverlap)
            {
                return "\uC911\uBCF5 \uAC00\uB2A5: \uAE30\uC874 \uB77C\uBCA8 \uBC84\uD2BC\uC73C\uB85C \uACB9\uCE58\uB294 \uB77C\uBCA8\uC744 \uD655\uC778\uD558\uACE0, \uAC19\uC740 \uAC1D\uCCB4\uBA74 \uC2A4\uD0B5\uD558\uC138\uC694.";
            }

            if (!string.IsNullOrWhiteSpace(overlap.Label))
            {
                return "\uBD80\uBD84 \uACB9\uCE68: AI \uD6C4\uBCF4\uC640 \uAE30\uC874 \uB77C\uBCA8\uC744 \uBE44\uAD50\uD55C \uB4A4, \uC0C8 \uAC1D\uCCB4\uBA74 \uD655\uC815\uD558\uC138\uC694.";
            }

            return "\uC0C8 \uD6C4\uBCF4: \uACB9\uCE58\uB294 \uAE30\uC874 \uB77C\uBCA8\uC774 \uC5C6\uC2B5\uB2C8\uB2E4. \uB9DE\uC73C\uBA74 \uD655\uC815, \uC544\uB2C8\uBA74 \uC2A4\uD0B5\uD558\uC138\uC694.";
        }

        public static string BuildSecondaryText(
            YoloWorkerSmokeCandidate candidate,
            Rectangle bounds,
            WpfCandidateOverlapInfo overlap,
            float minimumConfidence)
        {
            if (candidate == null)
            {
                return "\uAC80\uD1A0 \uD544\uC694";
            }

            string status = IsConfirmable(candidate, bounds, overlap, minimumConfidence)
                ? "\uD655\uC815 \uAC00\uB2A5"
                : IsHighOverlap(overlap)
                    ? "\uC911\uBCF5 \uAC00\uB2A5"
                    : "\uAC80\uD1A0 \uD544\uC694";

            return bounds.IsEmpty
                ? $"\uC774\uBBF8\uC9C0 \uBC16 / {status}"
                : $"{FormatBoundsCompact(bounds)} / {status}";
        }

        public static string BuildCurrentObjectComparison(Rectangle bounds, WpfCandidateOverlapInfo overlap)
        {
            if (bounds.IsEmpty)
            {
                return "\uD604\uC7AC \uB77C\uBCA8: \uBE44\uAD50 \uBD88\uAC00";
            }

            return string.IsNullOrWhiteSpace(overlap.Label)
                ? "\uD604\uC7AC \uB77C\uBCA8: \uACB9\uCE68 \uC5C6\uC74C"
                : $"\uD604\uC7AC \uB77C\uBCA8: {overlap.Label} / \uACB9\uCE68 {overlap.Iou.ToString("P0", CultureInfo.CurrentCulture)}";
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

            if (bounds.IsEmpty)
            {
                return "\uC774\uBBF8\uC9C0 \uC601\uC5ED \uBC16 \uD6C4\uBCF4\uB294 \uD655\uC815\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.";
            }

            if (IsHighOverlap(overlap))
            {
                return "\uD604\uC7AC \uB77C\uBCA8\uACFC 50% \uC774\uC0C1 \uACB9\uCCD0 \uC911\uBCF5 \uD655\uC815\uC5D0\uC11C \uC81C\uC678\uD569\uB2C8\uB2E4. \uAE30\uC874 \uB77C\uBCA8\uC744 \uD655\uC778\uD558\uAC70\uB098 \uD6C4\uBCF4\uB97C \uC2A4\uD0B5\uD558\uC138\uC694.";
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
                ? "\uBC15\uC2A4 \uD504\uB86C\uD504\uD2B8"
                : (candidate?.Confidence ?? 0D).ToString(format, CultureInfo.CurrentCulture);

        private static bool IsSmartMask(YoloWorkerSmokeCandidate candidate)
            => string.Equals(candidate?.CandidateType, "smart-mask", StringComparison.OrdinalIgnoreCase);

        public static string FormatBoundsCompact(Rectangle bounds)
            => bounds.IsEmpty ? "-" : $"\uD06C\uAE30 {bounds.Width}x{bounds.Height} / \uC704\uCE58 x={bounds.X}, y={bounds.Y}";

        private static string BuildCandidateSummaryName(YoloWorkerSmokeCandidate candidate)
        {
            string indexText = candidate?.Index > 0 ? $"{candidate.Index}. " : string.Empty;
            return $"{indexText}{GetClassName(candidate)} {FormatConfidence(candidate, "P1")}";
        }
    }
}
