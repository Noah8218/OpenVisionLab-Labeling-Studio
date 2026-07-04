using MahApps.Metro.IconPacks;
using System;

namespace MvcVisionSystem
{
    public static class WpfAnomalyDashboardPresentationService
    {
        public static WpfDatasetDashboardMetricItem BuildReviewStateMetric(AnomalyImageReviewSummary summary)
        {
            summary ??= new AnomalyImageReviewSummary();
            bool hasImages = summary.TotalImageCount > 0;
            bool hasUnreviewed = summary.UnreviewedImageCount > 0;
            string status = !hasImages
                ? "\uB300\uAE30"
                : hasUnreviewed
                    ? "\uAC80\uD1A0 \uD544\uC694"
                    : summary.AbnormalImageCount > 0
                        ? "\uBD84\uB958 \uC644\uB8CC"
                        : "\uC815\uC0C1 \uC644\uB8CC";

            return new WpfDatasetDashboardMetricItem(
                "\uC815\uC0C1/\uC774\uC0C1",
                $"{summary.NormalImageCount}/{summary.AbnormalImageCount}/{summary.UnreviewedImageCount}",
                $"\uC815\uC0C1 {summary.NormalImageCount}\uC7A5, \uC774\uC0C1 {summary.AbnormalImageCount}\uC7A5, \uBBF8\uAC80\uD1A0 {summary.UnreviewedImageCount}\uC7A5",
                status,
                hasUnreviewed ? PackIconMaterialKind.AlertCircleOutline : PackIconMaterialKind.CheckCircleOutline,
                isProblem: hasImages && hasUnreviewed,
                isWarning: false,
                actionKind: WpfDatasetDashboardActionKind.OpenLabelingProgress);
        }

        public static string BuildReviewStateIssue(AnomalyImageReviewSummary summary)
        {
            summary ??= new AnomalyImageReviewSummary();
            if (summary.TotalImageCount <= 0)
            {
                return string.Empty;
            }

            if (summary.UnreviewedImageCount > 0)
            {
                return $"\uB2E4\uC74C: anomaly \uBBF8\uAC80\uD1A0 {summary.UnreviewedImageCount}\uC7A5\uC744 \uC815\uC0C1/\uC774\uC0C1\uC73C\uB85C \uBD84\uB958\uD558\uC138\uC694.";
            }

            return "\uBB38\uC81C \uC5C6\uC74C: anomaly \uC815\uC0C1/\uC774\uC0C1 \uBD84\uB958\uAC00 \uC644\uB8CC\uB410\uC2B5\uB2C8\uB2E4.";
        }
    }
}
