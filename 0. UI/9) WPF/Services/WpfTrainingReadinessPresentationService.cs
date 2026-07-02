using System;
using System.Collections.Generic;
using System.Linq;
using MvcVisionSystem.Yolo;

namespace MvcVisionSystem
{
    public static class WpfTrainingReadinessPresentationService
    {
        public static string BuildStatusText(CData data, YoloDatasetReadinessReport report)
        {
            YoloDatasetStatistics statistics = report?.Statistics ?? new YoloDatasetStatistics();
            int classCount = data?.ClassNamedList?.Count ?? 0;
            string countText = BuildCountText(statistics, classCount);

            if (report?.IsReady == true)
            {
                IReadOnlyList<string> warnings = YoloDatasetDiagnosticsService.BuildQualityWarnings(data, statistics);
                string readyText = warnings.Count > 0 ? "학습 준비 완료(주의)" : "학습 준비 완료";
                return $"{readyText}: {countText}";
            }

            string issueText = BuildFriendlyIssueText(report?.Errors ?? Array.Empty<string>());
            return $"학습 데이터 확인 필요: {issueText} / {countText}";
        }

        private static string BuildCountText(YoloDatasetStatistics statistics, int classCount)
        {
            statistics ??= new YoloDatasetStatistics();
            string segmentText = statistics.TotalSegmentationObjectCount > 0
                ? $" / 세그먼트 {statistics.TotalSegmentationObjectCount}"
                : string.Empty;
            return $"학습 {statistics.TrainImageCount} / 검증 {statistics.ValidImageCount} / 테스트 {statistics.TestImageCount} / 객체 {statistics.TotalObjectCount}{segmentText} / 클래스 {classCount}";
        }

        private static string BuildFriendlyIssueText(IEnumerable<string> errors)
        {
            List<string> normalized = (errors ?? Array.Empty<string>())
                .Select(error => (error ?? string.Empty).Trim())
                .Where(error => error.Length > 0)
                .ToList();

            if (normalized.Count == 0)
            {
                return "데이터셋 점검 결과를 확인하세요.";
            }

            if (Contains(normalized, "data.yaml")
                && (Contains(normalized, "class count")
                    || Contains(normalized, "class names")
                    || Contains(normalized, "class name mismatch")))
            {
                return "클래스 목록과 data.yaml이 맞지 않습니다. 데이터셋 점검을 다시 실행하고 클래스 탭의 목록을 확인하세요.";
            }

            if (Contains(normalized, "data.yaml"))
            {
                return "data.yaml이 현재 데이터셋 경로와 맞지 않습니다. 저장 폴더와 이미지 폴더를 확인한 뒤 데이터셋 점검을 다시 실행하세요.";
            }

            if (Contains(normalized, "no yolo box labels")
                || Contains(normalized, "no YOLO box labels")
                || Contains(normalized, "Draw and save at least one rectangle label"))
            {
                return "저장된 박스 라벨이 없습니다. 박스를 그리고 라벨 저장 후 다시 점검하세요.";
            }

            if (Contains(normalized, "label file is missing")
                || Contains(normalized, "label directory"))
            {
                return "이미지는 있지만 저장된 라벨 파일이 부족합니다. 라벨 저장 상태와 저장 폴더를 확인하세요.";
            }

            if (Contains(normalized, "valid image directory")
                || Contains(normalized, "different validation images"))
            {
                return "검증 이미지가 부족합니다. 학습/검증 분할 설정을 확인하고 다시 점검하세요.";
            }

            if (Contains(normalized, "split")
                || Contains(normalized, "duplicate image content"))
            {
                return "학습/검증/최종 검증에 같은 이미지가 섞여 있습니다. 분할 설정이나 이미지 폴더 구성을 다시 확인하세요.";
            }

            if (Contains(normalized, "at least one class")
                || Contains(normalized, "duplicate class"))
            {
                return "클래스 등록이 필요합니다. 클래스 탭에서 모델이 배울 이름을 먼저 추가하세요.";
            }

            if (Contains(normalized, "image directory")
                || Contains(normalized, "supported images"))
            {
                return "학습 이미지 폴더를 확인하세요. 지원되는 이미지가 있는 폴더를 다시 선택해야 합니다.";
            }

            if (Contains(normalized, "output root"))
            {
                return "데이터셋 저장 폴더를 확인하세요. 라벨과 학습 파일을 쓸 수 있어야 합니다.";
            }

            return "데이터셋 점검 결과를 확인하고 필요한 항목을 수정하세요. 자세한 원문은 하단 로그 상세에서 볼 수 있습니다.";
        }

        private static bool Contains(IEnumerable<string> values, string text)
        {
            return values.Any(value => value.Contains(text, StringComparison.OrdinalIgnoreCase));
        }
    }
}
