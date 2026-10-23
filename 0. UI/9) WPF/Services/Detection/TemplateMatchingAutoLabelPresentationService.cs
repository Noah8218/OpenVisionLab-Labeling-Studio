using System;
using System.Drawing;

namespace MvcVisionSystem
{
    public class TemplateMatchingAutoLabelPresentationService
    {
        public string BuildTemplateRegisteredStatus(string className, Rectangle templateBounds)
        {
            string safeClassName = string.IsNullOrWhiteSpace(className) ? "Defect" : className.Trim();
            return $"템플릿 등록: {safeClassName} {Math.Max(0, templateBounds.Width)}x{Math.Max(0, templateBounds.Height)} / 다음: 다른 이미지에서 라벨 초안 생성";
        }

        public string BuildApplyFailureStatus(string message)
        {
            return $"템플릿 적용 실패: {NormalizeMessage(message, "원인을 확인할 수 없습니다.")}";
        }

        public string BuildApplyResultStatus(int addedCount, int candidateCount)
        {
            if (addedCount > 0)
            {
                return $"템플릿 라벨 초안 {addedCount}개 생성 - 위치 확인 후 라벨 저장";
            }

            return candidateCount > 0
                ? "템플릿 위치는 찾았지만 기존 라벨과 겹쳐 초안 추가 없음"
                : "템플릿 위치를 찾지 못했습니다";
        }

        public string BuildBatchStartCommandStatus(int totalCount)
        {
            return $"전체 이미지 템플릿 자동 저장 시작: {Math.Max(0, totalCount)}장";
        }

        public string BuildBatchStartGlobalStatus(int totalCount)
        {
            return $"전체 이미지 템플릿 자동 저장 중: 0/{Math.Max(0, totalCount)}";
        }

        public string BuildBatchItemGlobalStatus(int itemNumber, int totalCount, string fileName)
        {
            return $"전체 이미지 템플릿 자동 저장 {Math.Max(0, itemNumber)}/{Math.Max(0, totalCount)}: {NormalizeMessage(fileName, "이미지")}";
        }

        public string BuildBatchCompletionCommandStatus(
            bool canceled,
            int savedImageCount,
            int savedObjectCount,
            int noCandidateCount,
            int failedCount)
        {
            return $"전체 이미지 템플릿 자동 저장 {(canceled ? "취소" : "완료")}: 저장 {Math.Max(0, savedImageCount)}장, 라벨 {Math.Max(0, savedObjectCount)}개, 위치 없음 {Math.Max(0, noCandidateCount)}장, 실패 {Math.Max(0, failedCount)}장";
        }

        public string BuildBatchFailureStatus(string message)
        {
            return $"전체 이미지 템플릿 자동 저장 실패: {NormalizeMessage(message, "원인을 확인할 수 없습니다.")}";
        }

        public string BuildBatchCompletionGlobalStatus(
            bool canceled,
            int completedCount,
            int totalCount,
            TimeSpan elapsed)
        {
            return $"전체 이미지 템플릿 자동 저장 {(canceled ? "취소" : "완료")}: {Math.Max(0, completedCount)}/{Math.Max(0, totalCount)} / {Math.Max(0D, elapsed.TotalSeconds):0.0}s";
        }

        public string BuildGuideBody(string message)
        {
            string safeMessage = NormalizeMessage(message, OpenVisionLab.OpenVisionLanguageService.T("WpfTemplate.Guide.Prepare"));
            return string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfTemplate.Guide.Body"), safeMessage, Environment.NewLine, Environment.NewLine, Environment.NewLine, Environment.NewLine, Environment.NewLine, Environment.NewLine, Environment.NewLine, Environment.NewLine);
        }

        private static string NormalizeMessage(string message, string fallback)
        {
            return string.IsNullOrWhiteSpace(message) ? fallback : message.Trim();
        }
    }

    [Obsolete("Use TemplateMatchingAutoLabelPresentationService.", false)]
    public sealed class WpfTemplateMatchingAutoLabelPresentationService : TemplateMatchingAutoLabelPresentationService
    {
    }
}
