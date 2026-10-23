using MvcVisionSystem._1._Core;
using System;
using System.Drawing;
using System.Text.RegularExpressions;
using OpenVisionLab;

namespace MvcVisionSystem
{
    public static class ObjectReviewPresenter
    {
        public static string BuildSummary(int objectCount)
            => string.Format(OpenVisionLanguageService.T("WpfObjectReview.Summary.Count"), objectCount);

        public static string FormatMaskQualityThresholds(int smallArea, int holeArea)
            => string.Format(OpenVisionLanguageService.T("WpfObjectReview.MaskQuality.Thresholds"), smallArea, holeArea);

        public static string LocalizeIntelligentScissorsStatus(string text)
        {
            Match match = Regex.Match(text ?? string.Empty, @"^경계 미리보기: ([0-9]+)개 경로점 / ([0-9]+\.[0-9]) ms\. 캔버스를 확인한 후 미리보기 적용을 누르세요\.$");
            if (match.Success)
            {
                return string.Format(OpenVisionLanguageService.T("WpfObjectReview.Segmentation.Scissors.PreviewSummary"), match.Groups[1].Value, match.Groups[2].Value);
            }

            return text == "폴리곤 모서리를 선택해 이미지 경계 경로를 미리보기합니다."
                ? LocalizationTextRuntimeService.Translate(text)
                : text ?? string.Empty;
        }

        public static string LocalizeRemoveUnderlyingStatus(string text)
        {
            const string legacyShapeName = "geometry";
            Match match = Regex.Match(text ?? string.Empty, @"^영향 ([0-9]+)개\((#[0-9]+(?:, #[0-9]+)*)\) · 제거 ([0-9]+(?:,[0-9]{3})*)px · 완전 삭제 ([0-9]+)개\. '확인 후 제거'를 누르면 뒤쪽 (?:" + legacyShapeName + @"가|형상이) 변경됩니다\.$");
            if (match.Success)
            {
                return string.Format(OpenVisionLanguageService.T("WpfObjectReview.Segmentation.Overlap.PreviewSummary"), match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value, match.Groups[4].Value);
            }

            return text == $"위쪽 객체와 겹치는 뒤쪽 {legacyShapeName}를 분석한 후 확인해 제거합니다."
                || text == "위쪽 객체와 겹치는 뒤쪽 형상을 분석한 후 확인해 제거합니다."
                ? OpenVisionLanguageService.T("WpfObjectReview.Segmentation.Overlap.Hint")
                : text ?? string.Empty;
        }

        public static string FormatMergeSelection(int count)
            => string.Format(OpenVisionLanguageService.T("WpfObjectReview.Segmentation.Merge.Summary"), count);

        public static string FormatSegmentOrder(int index, int count, bool canChangeOrder)
            => canChangeOrder
                ? string.Format(OpenVisionLanguageService.T("WpfObjectReview.Segmentation.Order.Summary"), index + 1, count)
                : OpenVisionLanguageService.T("WpfObjectReview.Segmentation.Order.Hint");

        public static string EmptyText
            => OpenVisionLanguageService.T("WpfObjectReview.EmptyHint");

        public static WpfObjectReviewListItem BuildManualItem(
            int displayIndex,
            string className,
            Rectangle roi,
            string shapeName,
            string sourceKey,
            int sourceIndex,
            object payload,
            bool isManualPolygon = false)
        {
            return new WpfObjectReviewListItem(
                FormatManualSummary(displayIndex, className, roi, shapeName),
                FormatManualDetail(className, roi, shapeName),
                sourceKey,
                sourceIndex,
                payload,
                isManualPolygon: isManualPolygon,
                className: FirstNonEmpty(className, "Defect"),
                objectKindText: FirstNonEmpty(shapeName, "박스"),
                displayIndex: displayIndex,
                manualBounds: roi);
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
            string confirmedDetail = string.IsNullOrWhiteSpace(detail)
                ? "\uCD9C\uCC98: AI \uD6C4\uBCF4 \uD655\uC815 / \uC800\uC7A5 \uB77C\uBCA8\uB85C \uBC18\uC601\uB428"
                : $"\uCD9C\uCC98: AI \uD6C4\uBCF4 \uD655\uC815 / \uC800\uC7A5 \uB77C\uBCA8\uB85C \uBC18\uC601\uB428\n{detail}";
            return new WpfObjectReviewListItem(
                FormatConfirmedSummary(candidate, displayIndex, bounds),
                confirmedDetail,
                sourceKey,
                sourceIndex,
                payload,
                className: FirstNonEmpty(candidate?.ClassName, "Defect"),
                objectKindText: CandidateReviewPresenter.GetBoundaryPointCount(candidate) >= 3 ? "폴리곤" : "박스",
                displayIndex: displayIndex);
        }

        public static string FormatManualSummary(int displayIndex, string className, Rectangle roi, string shapeName = "박스")
        {
            return $"{displayIndex}. {FirstNonEmpty(className, "Defect")} / {FormatShapeName(FirstNonEmpty(shapeName, "박스"))} / {FormatBoundsCompact(roi)}";
        }

        public static string FormatManualDetail(string className, Rectangle roi, string shapeName = "박스")
        {
            return string.Format(OpenVisionLanguageService.T("WpfObjectReview.Manual.DetailFormat"),
                FormatShapeName(FirstNonEmpty(shapeName, "박스")), FirstNonEmpty(className, "Defect"), roi.X, roi.Y, roi.Width, roi.Height);
        }

        public static string FormatConfirmedSummary(YoloWorkerSmokeCandidate candidate, int displayIndex, Rectangle bounds)
        {
            return $"\uD655\uC815 \uB77C\uBCA8 {displayIndex}. {CandidateReviewPresenter.FormatCandidate(candidate, bounds)}";
        }

        public static string FormatBoundsCompact(Rectangle bounds)
        {
            return bounds.IsEmpty ? "-" : string.Format(OpenVisionLanguageService.T("WpfObjectReview.Bounds.CompactFormat"), bounds.Width, bounds.Height, bounds.X, bounds.Y);
        }

        public static string FormatShapeName(string shapeName) => shapeName switch
        {
            "박스" => OpenVisionLanguageService.T("WpfAnnotationTool.Rectangle"),
            "타원" => OpenVisionLanguageService.T("WpfObjectReview.Row.Ellipse"),
            "폴리곤" => OpenVisionLanguageService.T("WpfAnnotationTool.Polygon"),
            "마스크" => OpenVisionLanguageService.T("WpfObjectReview.Row.Mask"),
            _ => shapeName ?? string.Empty
        };

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

    [Obsolete("Use ObjectReviewPresenter.", false)]
    public static class WpfObjectReviewPresenter
    {
        public static string BuildSummary(int objectCount)
            => ObjectReviewPresenter.BuildSummary(objectCount);

        public static string EmptyText => ObjectReviewPresenter.EmptyText;

        public static WpfObjectReviewListItem BuildManualItem(
            int displayIndex,
            string className,
            Rectangle roi,
            string shapeName,
            string sourceKey,
            int sourceIndex,
            object payload,
            bool isManualPolygon = false)
            => ObjectReviewPresenter.BuildManualItem(displayIndex, className, roi, shapeName, sourceKey, sourceIndex, payload, isManualPolygon);

        public static WpfObjectReviewListItem BuildConfirmedItem(
            YoloWorkerSmokeCandidate candidate,
            int displayIndex,
            Rectangle bounds,
            string detail,
            string sourceKey,
            int sourceIndex,
            object payload)
            => ObjectReviewPresenter.BuildConfirmedItem(candidate, displayIndex, bounds, detail, sourceKey, sourceIndex, payload);

        public static string FormatManualSummary(int displayIndex, string className, Rectangle roi, string shapeName = "박스")
            => ObjectReviewPresenter.FormatManualSummary(displayIndex, className, roi, shapeName);

        public static string FormatManualDetail(string className, Rectangle roi, string shapeName = "박스")
            => ObjectReviewPresenter.FormatManualDetail(className, roi, shapeName);

        public static string FormatConfirmedSummary(YoloWorkerSmokeCandidate candidate, int displayIndex, Rectangle bounds)
            => ObjectReviewPresenter.FormatConfirmedSummary(candidate, displayIndex, bounds);

        public static string FormatBoundsCompact(Rectangle bounds)
            => ObjectReviewPresenter.FormatBoundsCompact(bounds);
    }
}
