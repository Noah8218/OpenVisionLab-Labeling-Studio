using System;
using System.Collections.Generic;
using System.Linq;
using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;

namespace MvcVisionSystem
{
    public static class WpfTrainingReadinessPresentationService
    {
        public static string BuildStatusText(CData data, YoloDatasetReadinessReport report)
        {
            if (data?.ProjectSettings?.DatasetPurpose == LabelingDatasetPurpose.AnomalyDetection)
            {
                return BuildAnomalyClassificationStatusText(
                    AnomalyClassificationTrainingReadinessService.Build(data));
            }

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

        public static string BuildAnomalyClassificationStatusText(AnomalyClassificationTrainingReadinessReport report)
        {
            report ??= new AnomalyClassificationTrainingReadinessReport(
                Array.Empty<string>(),
                normalImageCount: 0,
                abnormalImageCount: 0,
                unreviewedImageCount: 0,
                Array.Empty<string>());
            string countText = $"train normal {report.TrainNormalImageCount} / train abnormal {report.TrainAbnormalImageCount} / normal {report.NormalImageCount} / abnormal {report.AbnormalImageCount} / unreviewed {report.UnreviewedImageCount} / source {report.SourceImageCount}";
            if (report.IsReady)
            {
                return $"\uD559\uC2B5 \uC900\uBE44 \uC644\uB8CC: \uC774\uC0C1 \uD0D0\uC9C0 \uBD84\uB958 \uB370\uC774\uD130 / {countText}";
            }

            string issueText = BuildFriendlyIssueText(report.Errors);
            return $"\uD559\uC2B5 \uB370\uC774\uD130 \uD655\uC778 \uD544\uC694: {issueText} / {countText}";
        }

        public static string BuildFriendlyIssueSummary(string error)
            => BuildFriendlyIssueText((error ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));

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

            if (Contains(normalized, AnomalyClassificationTrainingReadinessService.NoSourceImagesError))
            {
                return "\uC774\uC0C1 \uD0D0\uC9C0 \uD559\uC2B5\uC5D0 \uC0AC\uC6A9\uD560 \uC774\uBBF8\uC9C0\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4. \uC774\uBBF8\uC9C0 \uD3F4\uB354\uB098 \uD559\uC2B5/\uAC80\uC99D \uD3F4\uB354\uB97C \uBA3C\uC800 \uC5F0\uACB0\uD558\uC138\uC694.";
            }

            if (Contains(normalized, AnomalyClassificationTrainingReadinessService.NeedsReviewedNormalAndAbnormalError))
            {
                return "\uC774\uC0C1 \uD0D0\uC9C0 \uD559\uC2B5\uC740 \uAC80\uD1A0\uB41C \uC815\uC0C1 \uC774\uBBF8\uC9C0\uC640 \uAC80\uD1A0\uB41C \uC774\uC0C1 \uC774\uBBF8\uC9C0\uAC00 \uAC01\uAC01 1\uAC1C \uC774\uC0C1 \uD544\uC694\uD569\uB2C8\uB2E4. \uC774\uBBF8\uC9C0 \uD050\uC5D0\uC11C \uC815\uC0C1/\uC774\uC0C1 \uAC80\uD1A0 \uC0C1\uD0DC\uB97C \uBA3C\uC800 \uC800\uC7A5\uD558\uC138\uC694.";
            }

            if (Contains(normalized, AnomalyClassificationTrainingReadinessService.NeedsTrainNormalAndAbnormalError))
            {
                return "\uC774\uC0C1 \uD0D0\uC9C0 \uD559\uC2B5\uC740 train \uBD84\uD560\uC5D0 \uC815\uC0C1/\uC774\uC0C1 \uC774\uBBF8\uC9C0\uAC00 \uAC01\uAC01 1\uAC1C \uC774\uC0C1 \uD544\uC694\uD569\uB2C8\uB2E4. \uAC80\uC99D/테스트 \uBE44\uC728\uC744 \uC904\uC774\uAC70\uB098 \uB354 \uB9CE\uC740 \uAC80\uD1A0 \uC774\uBBF8\uC9C0\uB97C \uCD94\uAC00\uD558\uC138\uC694.";
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
