using System;
using System.Drawing;

namespace MvcVisionSystem
{
    public sealed class WpfTemplateMatchingAutoLabelPresentationService
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
            string safeMessage = NormalizeMessage(message, "템플릿 작업을 계속하려면 기준 이미지와 기준 라벨을 먼저 준비하세요.");
            return $"{safeMessage}{Environment.NewLine}{Environment.NewLine}사용 순서:{Environment.NewLine}1. 기준 이미지에서 찾고 싶은 모양을 라벨 박스로 저장합니다.{Environment.NewLine}2. 객체 검토 목록에서 그 라벨 박스 1개를 선택합니다.{Environment.NewLine}3. 도구 > 현재 이미지 라벨 초안 생성으로 기준 템플릿을 등록합니다.{Environment.NewLine}4. 다른 이미지를 열고 현재 이미지 라벨 초안 생성 또는 전체 이미지 자동 저장을 실행합니다.{Environment.NewLine}5. 현재 이미지에 생성된 라벨 초안은 위치를 확인한 뒤 라벨 저장을 누릅니다.{Environment.NewLine}6. 전체 이미지 자동 저장은 라벨 없는 이미지에 바로 저장됩니다.";
        }

        private static string NormalizeMessage(string message, string fallback)
        {
            return string.IsNullOrWhiteSpace(message) ? fallback : message.Trim();
        }
    }
}
