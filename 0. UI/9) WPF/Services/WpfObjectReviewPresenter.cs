using MvcVisionSystem._1._Core;
using System;
using System.Drawing;

namespace MvcVisionSystem
{
    public static class WpfObjectReviewPresenter
    {
        public static string BuildSummary(int objectCount)
            => objectCount == 0
                ? "\uD604\uC7AC \uC774\uBBF8\uC9C0 \uAC1D\uCCB4 \uC5C6\uC74C"
                : $"{objectCount}\uAC1C \uAC1D\uCCB4";

        public static string EmptyText
            => "\uB77C\uBCA8 \uB610\uB294 \uD655\uC815\uB41C AI \uD6C4\uBCF4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.";

        public static WpfObjectReviewListItem BuildManualItem(
            int displayIndex,
            string className,
            Rectangle roi,
            string shapeName,
            string sourceKey,
            int sourceIndex,
            object payload)
        {
            return new WpfObjectReviewListItem(
                FormatManualSummary(displayIndex, className, roi, shapeName),
                FormatManualDetail(className, roi, shapeName),
                sourceKey,
                sourceIndex,
                payload);
        }

        public static WpfObjectReviewListItem BuildConfirmedItem(
            YoloWorkerSmokeCandidate candidate,
            int displayIndex,
            Rectangle bounds,
            string detail,
            string sourceKey,
            int sourceIndex,
            object payload)
        {
            return new WpfObjectReviewListItem(
                FormatConfirmedSummary(candidate, displayIndex, bounds),
                detail,
                sourceKey,
                sourceIndex,
                payload);
        }

        public static string FormatManualSummary(int displayIndex, string className, Rectangle roi, string shapeName = "박스")
        {
            return $"{displayIndex}. {FirstNonEmpty(className, "Defect")} / {FirstNonEmpty(shapeName, "박스")} / {FormatBoundsCompact(roi)}";
        }

        public static string FormatManualDetail(string className, Rectangle roi, string shapeName = "박스")
        {
            return $"\uCD9C\uCC98: \uC218\uB3D9 {FirstNonEmpty(shapeName, "박스")} / \uD074\uB798\uC2A4: {FirstNonEmpty(className, "Defect")} / \uC704\uCE58: x={roi.X}, y={roi.Y} / \uD06C\uAE30: w={roi.Width}, h={roi.Height}";
        }

        public static string FormatConfirmedSummary(YoloWorkerSmokeCandidate candidate, int displayIndex, Rectangle bounds)
        {
            return $"AI {displayIndex}. {WpfCandidateReviewPresenter.FormatCandidate(candidate, bounds)}";
        }

        public static string FormatBoundsCompact(Rectangle bounds)
        {
            return bounds.IsEmpty
                ? "-"
                : $"\uD06C\uAE30 {bounds.Width}x{bounds.Height} / \uC704\uCE58 x={bounds.X}, y={bounds.Y}";
        }

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
}
