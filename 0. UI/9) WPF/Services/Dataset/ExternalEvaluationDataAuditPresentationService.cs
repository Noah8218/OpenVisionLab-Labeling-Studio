using MvcVisionSystem.Yolo;
using System;
using System.Linq;

namespace MvcVisionSystem
{
    /// <summary>
    /// Translates an external-evaluation content audit report into operator-facing
    /// status and detail text. Folder selection, hashing, and ViewModel updates
    /// remain outside this presentation-only owner.
    /// </summary>
    public static class ExternalEvaluationDataAuditPresentationService
    {
        public static ExternalEvaluationDataAuditPresentation Build(
            YoloExternalEvaluationDataAuditReport report)
        {
            if (report == null)
            {
                return new ExternalEvaluationDataAuditPresentation(
                    "외부 평가 대조: 확인 불가",
                    "외부 평가 대조 결과가 없습니다.");
            }

            if (report.HasErrors)
            {
                return new ExternalEvaluationDataAuditPresentation(
                    "외부 평가 대조: 확인 불가",
                    string.Join(" ", report.Errors.Take(2)));
            }

            if (!report.HasExternalImages)
            {
                return new ExternalEvaluationDataAuditPresentation(
                    "외부 평가 대조: 이미지 없음",
                    "선택한 폴더에 지원 이미지가 없습니다.");
            }

            if (report.HasContentOverlap)
            {
                return new ExternalEvaluationDataAuditPresentation(
                    "외부 평가 대조: 중복 발견",
                    $"기준 {report.ReferenceImageCount}장 / 외부 {report.ExternalImageCount}장 / 동일 콘텐츠 {report.ContentOverlapCount}장. {report.OverlapExample}");
            }

            return new ExternalEvaluationDataAuditPresentation(
                "외부 평가 대조: 중복 없음",
                $"기준 {report.ReferenceImageCount}장 / 외부 {report.ExternalImageCount}장 / 동일 콘텐츠 0장 / 파일명 중복 {report.NameOverlapCount}개. 라벨 품질과 NG 포함 여부를 다음으로 확인하세요.");
        }
    }

    [Obsolete("Use ExternalEvaluationDataAuditPresentationService.", false)]
    public static class WpfExternalEvaluationDataAuditPresentationService
    {
        public static WpfExternalEvaluationDataAuditPresentation Build(
            YoloExternalEvaluationDataAuditReport report)
        {
            ExternalEvaluationDataAuditPresentation presentation = ExternalEvaluationDataAuditPresentationService.Build(report);
            return presentation == null
                ? null
                : new WpfExternalEvaluationDataAuditPresentation(presentation.StatusText, presentation.DetailText);
        }
    }

    public class ExternalEvaluationDataAuditPresentation
    {
        public ExternalEvaluationDataAuditPresentation(string statusText, string detailText)
        {
            StatusText = statusText ?? string.Empty;
            DetailText = detailText ?? string.Empty;
        }

        public string StatusText { get; }

        public string DetailText { get; }
    }

    [Obsolete("Use ExternalEvaluationDataAuditPresentation.", false)]
    public sealed class WpfExternalEvaluationDataAuditPresentation : ExternalEvaluationDataAuditPresentation
    {
        public WpfExternalEvaluationDataAuditPresentation(string statusText, string detailText)
            : base(statusText, detailText)
        {
        }
    }
}
