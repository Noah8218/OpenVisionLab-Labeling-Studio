using System;
using System.Collections.Generic;
using System.Linq;
using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;

namespace MvcVisionSystem
{
    public static class TrainingReadinessPresentationService
    {
        public static string BuildStatusText(LabelingProjectData data, YoloDatasetReadinessReport report)
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

            string issueText = BuildFriendlyIssueText(data, report);
            return $"학습 데이터 확인 필요: {issueText} / {countText}";
        }

        public static TrainingChecklistPresentation BuildChecklistPresentation(
            LabelingProjectData data,
            YoloDatasetReadinessReport report)
        {
            if (report?.IsReady == true)
            {
                YoloDatasetStatistics statistics = report.Statistics;
                LabelingDatasetPurpose purpose = report.Purpose;
                int classCount = data?.ClassNamedList?.Count ?? 0;
                IReadOnlyList<string> warnings = YoloDatasetDiagnosticsService.BuildQualityWarnings(data, statistics);
                bool hasWarnings = warnings.Count > 0;
                TrainingChecklistLocalizationSnapshot localization = TrainingChecklistLocalizationService.BuildReady(
                    statistics,
                    classCount,
                    purpose,
                    hasWarnings);
                return new TrainingChecklistPresentation(
                    hasWarnings ? "ReadyWithWarnings" : "Ready",
                    localization,
                    hasWarnings
                        ? TrainingChecklistLocalizationService.BuildQualityWarningAction(warnings)
                        : TrainingChecklistLocalizationService.BuildReadyAction(purpose));
            }

            string firstError = report?.Errors?.FirstOrDefault() ?? "원인 미확인";
            string issueKind = ClassifyIssue(report?.Errors ?? Array.Empty<string>());
            LabelingDatasetPurpose failurePurpose = report?.Purpose ?? LabelingDatasetPurpose.ObjectDetection;
            TrainingChecklistLocalizationSnapshot failureLocalization = TrainingChecklistLocalizationService.BuildFailure(
                issueKind,
                firstError,
                report?.Statistics,
                failurePurpose);
            return new TrainingChecklistPresentation(
                issueKind,
                failureLocalization,
                TrainingChecklistLocalizationService.BuildFailureAction(issueKind));
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

        public static string BuildReadyDatasetStatusText(
            YoloDatasetStatistics statistics,
            LabelingDatasetPurpose purpose,
            bool hasWarnings)
        {
            return TrainingChecklistLocalizationService
                .BuildReady(statistics, classCount: 0, purpose: purpose, hasWarnings: hasWarnings)
                .StatusText;
        }

        public static (string StatusText, string DetailText) BuildExternalDatasetPresentation(ExternalYoloDatasetSettings settings)
        {
            if (settings == null)
            {
                return (string.Empty, string.Empty);
            }

            string statusText;
            string detailText;
            if (!settings.HasSelection)
            {
                statusText = "외부 YOLO data.yaml: 선택 안 함";
                detailText = "선택 후 검증해도 원본 이미지와 라벨은 수정하지 않습니다.";
            }
            else if (settings.RequiresExplicitReactivation)
            {
                statusText = "외부 YOLO data.yaml: 재활성화 필요";
                detailText = string.IsNullOrWhiteSpace(settings.LastValidationSummary)
                    ? "원본이 변경되었을 수 있습니다. data.yaml을 다시 검증한 뒤 '다음 학습 사용'으로 명시적으로 활성화하세요."
                    : settings.LastValidationSummary;
            }
            else if (settings.UseForTraining && settings.LastValidationSucceeded)
            {
                statusText = "외부 YOLO data.yaml: 다음 학습에 사용";
                detailText = settings.LastValidationSummary + " 내부 레시피 데이터는 바꾸지 않으며 자동 학습이나 모델 채택은 하지 않습니다.";
            }
            else if (settings.LastValidationSucceeded)
            {
                statusText = "외부 YOLO data.yaml: 검증됨";
                detailText = settings.LastValidationSummary + " '다음 학습 사용'을 눌러야 학습 소스가 바뀝니다.";
            }
            else
            {
                statusText = settings.UseForTraining
                    ? "외부 YOLO data.yaml: 재검증 필요"
                    : "외부 YOLO data.yaml: 확인 필요";
                detailText = string.IsNullOrWhiteSpace(settings.LastValidationSummary)
                    ? "data.yaml을 다시 선택해 경로와 라벨 형식을 확인하세요."
                    : settings.LastValidationSummary;
            }

            if (settings.LastValidationSucceeded
                && !string.IsNullOrWhiteSpace(settings.LastValidationClassNames))
            {
                detailText += Environment.NewLine + "외부 학습 클래스: " + settings.LastValidationClassNames
                    + " (레시피 클래스는 자동으로 바꾸지 않음)";
            }

            return (statusText, detailText);
        }

        public static (string ReadinessText, string ChecklistStatusText, string ChecklistDetailText, string ChecklistActionText) BuildExternalTrainingReadinessPresentation(
            ExternalYoloDatasetSettings settings,
            YoloExternalDatasetIntakeReport externalReport)
        {
            bool isReady = settings?.LastValidationSucceeded == true;
            LabelingDatasetPurpose purpose = externalReport?.Purpose ?? settings?.DatasetPurpose ?? LabelingDatasetPurpose.ObjectDetection;
            int trainCount = externalReport?.Train.ImageCount ?? settings?.TrainImageCount ?? 0;
            int validCount = externalReport?.Valid.ImageCount ?? settings?.ValidImageCount ?? 0;
            int testCount = externalReport?.Test.ImageCount ?? settings?.TestImageCount ?? 0;
            int annotationCount = externalReport?.TotalAnnotationCount ?? settings?.AnnotationCount ?? 0;
            int classCount = externalReport?.ClassNames.Count ?? settings?.ClassCount ?? 0;
            string validationDetail = isReady && externalReport?.IsReady == true
                ? externalReport.Summary
                : externalReport != null && !externalReport.IsReady
                    ? string.Join(" ", externalReport.Errors.Take(2))
                    : settings?.LastValidationSummary ?? string.Empty;
            string statusPrefix = externalReport == null ? "외부 YOLO data.yaml 마지막 검증" : "외부 YOLO data.yaml 준비";
            string readinessText = isReady
                ? $"{statusPrefix} 완료: {YoloExternalDatasetIntakeService.FormatPurpose(purpose)} / 학습 {trainCount} / 검증 {validCount} / 테스트 {testCount} / 객체 {annotationCount} / 클래스 {classCount}"
                : "외부 YOLO data.yaml 확인 필요: " + (string.IsNullOrWhiteSpace(validationDetail) ? "원인을 확인하세요." : validationDetail);
            string checklistStatusText = isReady
                ? externalReport == null
                    ? "데이터셋: 외부 YOLO data.yaml 마지막 검증 통과"
                    : "데이터셋: 외부 YOLO data.yaml 준비 완료"
                : "데이터셋: 외부 YOLO data.yaml 확인 필요";
            string checklistActionText = isReady
                ? externalReport == null
                    ? "다음: 학습 시작 시 원본 외부 data.yaml을 다시 검증합니다."
                    : "다음: 학습/모델 탭에서 시작합니다. 원본 외부 데이터는 변경하지 않습니다."
                : "다음: data.yaml 경로, train/val 분할, names와 라벨 형식을 수정한 뒤 다시 확인합니다.";

            return (readinessText, checklistStatusText, validationDetail, checklistActionText);
        }

        public static string ClassifyIssue(IEnumerable<string> errors)
        {
            List<string> normalized = (errors ?? Array.Empty<string>())
                .Select(error => error?.Trim() ?? string.Empty)
                .Where(error => error.Length > 0)
                .Select(error => error.ToLowerInvariant())
                .ToList();

            if (normalized.Any(error => error.Contains("invalid yolo format", StringComparison.Ordinal)
                || error.Contains("invalid class index", StringComparison.Ordinal)
                || error.Contains("out-of-range normalized value", StringComparison.Ordinal)
                || error.Contains("label width must", StringComparison.Ordinal)
                || error.Contains("label height must", StringComparison.Ordinal)))
            {
                return "LabelFormat";
            }

            if (normalized.Any(error => error.Contains("at least one class", StringComparison.Ordinal)
                || error.Contains("class names", StringComparison.Ordinal)
                || error.Contains("duplicate class", StringComparison.Ordinal)))
            {
                return "Classes";
            }

            if (normalized.Any(error => error.Contains("label file is missing", StringComparison.Ordinal)
                || error.Contains("label directory", StringComparison.Ordinal)))
            {
                return "Labels";
            }

            if (normalized.Any(error => error.Contains("segmentation annotations", StringComparison.Ordinal)
                && error.Contains("no yolo box labels", StringComparison.Ordinal)))
            {
                return "SegmentationPolicy";
            }

            if (normalized.Any(error => error.Contains("segmentation dataset", StringComparison.Ordinal)
                || error.Contains("segmentation annotation is missing", StringComparison.Ordinal)
                || error.Contains("segment json", StringComparison.Ordinal)
                || error.Contains("mask png", StringComparison.Ordinal)))
            {
                return "SegmentationLabels";
            }

            if (normalized.Any(error => error.Contains("valid image directory", StringComparison.Ordinal)))
            {
                return "ValidImages";
            }

            if (normalized.Any(error => error.Contains("train/valid image split", StringComparison.Ordinal)
                || error.Contains("train/test image split", StringComparison.Ordinal)
                || error.Contains("valid/test image split", StringComparison.Ordinal)
                || error.Contains("duplicate image content", StringComparison.Ordinal)
                || error.Contains("different validation images", StringComparison.Ordinal)))
            {
                return "Split";
            }

            if (normalized.Any(error => error.Contains("data.yaml", StringComparison.Ordinal)))
            {
                return "DataYaml";
            }

            if (normalized.Any(error => error.Contains("output root", StringComparison.Ordinal)))
            {
                return "OutputRoot";
            }

            if (normalized.Any(error => error.Contains("image directory", StringComparison.Ordinal)
                || error.Contains("supported images", StringComparison.Ordinal)))
            {
                return "Images";
            }

            return "Unknown";
        }

        private static string BuildFriendlyIssueText(LabelingProjectData data, YoloDatasetReadinessReport report)
        {
            if (report != null
                && (report.Purpose == LabelingDatasetPurpose.Segmentation
                    || data?.ProjectSettings?.DatasetPurpose == LabelingDatasetPurpose.Segmentation))
            {
                string segmentationIssueText = BuildSegmentationIssueText(report);
                if (!string.IsNullOrWhiteSpace(segmentationIssueText))
                {
                    return segmentationIssueText;
                }
            }

            return BuildFriendlyIssueText(report?.Errors ?? Array.Empty<string>());
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

            if (Contains(normalized, "YOLOv8 segmentation training needs polygon segment JSON"))
            {
                return "\uC138\uADF8\uBA58\uD14C\uC774\uC158 \uD559\uC2B5\uC740 polygon segment JSON\uC774 \uD544\uC694\uD569\uB2C8\uB2E4. \uBE0C\uB7EC\uC2DC/\uD3F4\uB9AC\uACE4 \uB77C\uBCA8\uC744 \uC800\uC7A5\uD55C \uB4A4 \uB2E4\uC2DC \uC2DC\uC791\uD558\uC138\uC694.";
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

            if (Contains(normalized, YoloTrainingWorkflowService.AnomalyClassificationRuntimeError))
            {
                return "이상탐지 분류 학습은 YOLOv8 또는 YOLO11 실행기가 필요합니다. 모델 실행기에서 지원 실행기를 연결한 뒤 다시 시작하세요.";
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

        private static string BuildSegmentationIssueText(YoloDatasetReadinessReport report)
        {
            IReadOnlyList<string> errors = report?.Errors ?? Array.Empty<string>();
            YoloDatasetStatistics statistics = report?.Statistics ?? new YoloDatasetStatistics();
            bool hasSplitError = Contains(errors, "train image directory")
                || Contains(errors, "valid image directory");
            bool hasSegmentationArtifact = statistics.TotalSegmentationArtifactFileCount > 0
                || statistics.TotalSegmentationObjectCount > 0;
            if (hasSplitError
                || (hasSegmentationArtifact && (statistics.TrainImageCount == 0 || statistics.ValidImageCount == 0)))
            {
                return "\uC138\uADF8\uBA58\uD14C\uC774\uC158 \uD559\uC2B5\uC740 train\uACFC valid \uBD84\uD560\uC5D0 \uAC01\uAC01 \uC800\uC7A5\uB41C \uB9C8\uC2A4\uD06C \uB77C\uBCA8\uC774 \uD544\uC694\uD569\uB2C8\uB2E4. \uB354 \uB9CE\uC740 SEG \uC774\uBBF8\uC9C0\uB97C \uB77C\uBCA8 \uC800\uC7A5\uD558\uACE0 \uB2E4\uC2DC \uC810\uAC80\uD558\uC138\uC694.";
            }

            if (statistics.TotalSegmentationArtifactFileCount == 0
                || Contains(errors, "Segmentation dataset has no segment JSON or mask PNG annotations")
                || Contains(errors, "segmentation annotation is missing"))
            {
                return "\uC138\uADF8\uBA58\uD14C\uC774\uC158 \uBAA9\uC801\uC5D0\uC11C\uB294 \uBE0C\uB7EC\uC2DC\uB098 \uD3F4\uB9AC\uACE4\uC73C\uB85C \uB9C8\uC2A4\uD06C\uB97C \uC800\uC7A5\uD55C \uB4A4 \uB2E4\uC2DC \uB370\uC774\uD130\uC14B \uC810\uAC80\uC744 \uC2E4\uD589\uD558\uC138\uC694.";
            }

            return string.Empty;
        }
    }

    [Obsolete("Use TrainingReadinessPresentationService.", false)]
    public static class WpfTrainingReadinessPresentationService
    {
        public static string BuildStatusText(LabelingProjectData data, YoloDatasetReadinessReport report)
            => TrainingReadinessPresentationService.BuildStatusText(data, report);

        public static WpfTrainingChecklistPresentation BuildChecklistPresentation(LabelingProjectData data, YoloDatasetReadinessReport report)
            => WpfTrainingChecklistPresentation.FromCanonical(TrainingReadinessPresentationService.BuildChecklistPresentation(data, report));

        public static string BuildAnomalyClassificationStatusText(AnomalyClassificationTrainingReadinessReport report)
            => TrainingReadinessPresentationService.BuildAnomalyClassificationStatusText(report);

        public static string BuildFriendlyIssueSummary(string error)
            => TrainingReadinessPresentationService.BuildFriendlyIssueSummary(error);

        public static string BuildReadyDatasetStatusText(YoloDatasetStatistics statistics, LabelingDatasetPurpose purpose, bool hasWarnings)
            => TrainingReadinessPresentationService.BuildReadyDatasetStatusText(statistics, purpose, hasWarnings);

        public static (string StatusText, string DetailText) BuildExternalDatasetPresentation(ExternalYoloDatasetSettings settings)
            => TrainingReadinessPresentationService.BuildExternalDatasetPresentation(settings);

        public static (string ReadinessText, string ChecklistStatusText, string ChecklistDetailText, string ChecklistActionText) BuildExternalTrainingReadinessPresentation(
            ExternalYoloDatasetSettings settings,
            YoloExternalDatasetIntakeReport externalReport)
            => TrainingReadinessPresentationService.BuildExternalTrainingReadinessPresentation(settings, externalReport);

        public static string ClassifyIssue(IEnumerable<string> errors)
            => TrainingReadinessPresentationService.ClassifyIssue(errors);
    }

    public sealed class TrainingChecklistPresentation
    {
        public TrainingChecklistPresentation(
            string issueKind,
            TrainingChecklistLocalizationSnapshot localization,
            TrainingChecklistActionLocalizationSnapshot actionLocalization)
        {
            IssueKind = issueKind ?? string.Empty;
            Localization = localization ?? throw new ArgumentNullException(nameof(localization));
            ActionLocalization = actionLocalization ?? throw new ArgumentNullException(nameof(actionLocalization));
        }

        public string IssueKind { get; }

        public TrainingChecklistLocalizationSnapshot Localization { get; }

        public TrainingChecklistActionLocalizationSnapshot ActionLocalization { get; }

        public string StatusText => Localization.StatusText;

        public string DetailText => Localization.DetailText;

        public string ActionText => ActionLocalization.ActionText;
    }

    [Obsolete("Use TrainingChecklistPresentation.", false)]
    public sealed class WpfTrainingChecklistPresentation
    {
        private readonly TrainingChecklistPresentation inner;
        private readonly WpfTrainingChecklistLocalizationSnapshot localization;
        private readonly WpfTrainingChecklistActionLocalizationSnapshot actionLocalization;

        public WpfTrainingChecklistPresentation(
            string issueKind,
            WpfTrainingChecklistLocalizationSnapshot localization,
            WpfTrainingChecklistActionLocalizationSnapshot actionLocalization)
        {
            inner = new TrainingChecklistPresentation(
                issueKind,
                localization?.ToCanonical(),
                actionLocalization?.ToCanonical());
            this.localization = localization;
            this.actionLocalization = actionLocalization;
        }

        private WpfTrainingChecklistPresentation(TrainingChecklistPresentation source)
        {
            inner = source ?? throw new ArgumentNullException(nameof(source));
            localization = WpfTrainingChecklistLocalizationSnapshot.FromCanonical(source.Localization);
            actionLocalization = WpfTrainingChecklistActionLocalizationSnapshot.FromCanonical(source.ActionLocalization);
        }

        internal static WpfTrainingChecklistPresentation FromCanonical(TrainingChecklistPresentation source)
            => source == null ? null : new WpfTrainingChecklistPresentation(source);

        public string IssueKind => inner.IssueKind;

        public WpfTrainingChecklistLocalizationSnapshot Localization => localization;

        public WpfTrainingChecklistActionLocalizationSnapshot ActionLocalization => actionLocalization;

        public string StatusText => inner.StatusText;

        public string DetailText => inner.DetailText;

        public string ActionText => inner.ActionText;
    }
}
