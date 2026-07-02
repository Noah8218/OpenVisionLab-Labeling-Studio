using MahApps.Metro.IconPacks;
using Lib.Common;
using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        private const int RecommendedModelReplacementTestImageCount = 10;

        // Training guide status owns dataset checklist and history persistence; live worker polling stays in TrainingStatus.
        private void UpdateYoloTrainingChecklist(YoloDatasetReadinessReport report, bool recordHistory)
        {
            if (LearningWorkflowViewModel == null || report == null)
            {
                return;
            }

            lastYoloTrainingReadinessReport = report;
            YoloTrainingIssuePresentation presentation = BuildYoloTrainingIssuePresentation(report);
            LearningWorkflowViewModel.TrainingChecklistStatusText = presentation.StatusText;
            LearningWorkflowViewModel.TrainingChecklistDetailText = presentation.DetailText;
            LearningWorkflowViewModel.TrainingChecklistActionText = presentation.ActionText;
            UpdateDatasetStatusDashboard(report, presentation);
            UpdateYoloTrainingGuideDatasetHistory(report, presentation, recordHistory);
            UpdateYoloTrainingHistoryText();
            RefreshYoloTrainingStepCompletion(report);
        }

        private void UpdateDatasetStatusDashboard(
            YoloDatasetReadinessReport report,
            YoloTrainingIssuePresentation presentation)
        {
            if (LearningWorkflowViewModel == null || report == null)
            {
                return;
            }

            YoloDatasetStatistics statistics = report.Statistics ?? new YoloDatasetStatistics();
            int classCount = global.Data?.ClassNamedList?.Count ?? 0;
            IReadOnlyList<string> warnings = report.IsReady
                ? YoloDatasetDiagnosticsService.BuildQualityWarnings(global.Data, statistics)
                : Array.Empty<string>();
            string statusText = report.IsReady
                ? warnings.Count > 0
                    ? "\uB370\uC774\uD130\uC14B \uC0C1\uD0DC: \uC8FC\uC758 \uD6C4 \uD559\uC2B5 \uAC00\uB2A5"
                    : "\uB370\uC774\uD130\uC14B \uC0C1\uD0DC: \uD559\uC2B5 \uAC00\uB2A5"
                : "\uB370\uC774\uD130\uC14B \uC0C1\uD0DC: \uD559\uC2B5 \uBD88\uAC00";
            string summaryText =
                $"purpose {report.Purpose}, images {statistics.TotalImageCount}, train {statistics.TrainImageCount}, valid {statistics.ValidImageCount}, test {statistics.TestImageCount}, classes {classCount}";
            LearningWorkflowViewModel.SetModelReplacementReadiness(
                BuildModelReplacementStatusText(report, statistics),
                BuildModelReplacementDetailText(report, statistics));
            LearningWorkflowViewModel.SetDatasetDashboard(
                statusText,
                summaryText,
                presentation?.ActionText ?? string.Empty,
                BuildDatasetDashboardMetrics(report, statistics, classCount),
                BuildDatasetDashboardIssues(report, warnings, classCount));
        }

        private static IReadOnlyList<WpfDatasetDashboardMetricItem> BuildDatasetDashboardMetrics(
            YoloDatasetReadinessReport report,
            YoloDatasetStatistics statistics,
            int classCount)
        {
            statistics ??= new YoloDatasetStatistics();
            LabelingDatasetPurpose purpose = report?.Purpose ?? LabelingDatasetPurpose.ObjectDetection;
            bool hasImages = statistics.TotalImageCount > 0;
            bool hasTrain = statistics.TrainImageCount > 0;
            bool hasValid = statistics.ValidImageCount > 0;
            bool hasTest = statistics.TestImageCount > 0;
            bool hasTestLabels = statistics.TestLabelCount > 0;
            int finalVerificationCount = Math.Min(statistics.TestImageCount, statistics.TestLabelCount);
            bool hasReplacementEvidence = hasTest && hasTestLabels;
            bool hasStrongReplacementEvidence = finalVerificationCount >= RecommendedModelReplacementTestImageCount;
            bool hasSplitOverlap = statistics.TrainValidImageContentOverlapCount > 0 || statistics.SplitImageContentOverlapCount > 0;
            bool needsBoxLabels = purpose != LabelingDatasetPurpose.Segmentation;
            bool hasPrimaryLabels = needsBoxLabels
                ? statistics.TotalObjectCount > 0
                : statistics.TotalSegmentationObjectCount > 0 || statistics.TotalMaskFileCount > 0;
            int primaryLabelValue = needsBoxLabels
                ? statistics.TotalObjectCount
                : Math.Max(statistics.TotalSegmentationObjectCount, statistics.TotalMaskFileCount);
            int artifactFileCount = needsBoxLabels
                ? statistics.TotalLabelFileCount
                : statistics.TotalSegmentFileCount + statistics.TotalMaskFileCount;
            int completedImageLabelCount = needsBoxLabels
                ? statistics.TotalLabelFileCount
                : Math.Max(statistics.TotalSegmentFileCount, statistics.TotalMaskFileCount);
            int visibleCompletedImageLabelCount = statistics.TotalImageCount > 0
                ? Math.Min(completedImageLabelCount, statistics.TotalImageCount)
                : 0;
            int progressPercent = statistics.TotalImageCount > 0
                ? (int)Math.Round(visibleCompletedImageLabelCount * 100D / statistics.TotalImageCount)
                : 0;
            bool isLabelingComplete = hasImages && completedImageLabelCount >= statistics.TotalImageCount;
            bool hasAnyCompletedImageLabel = completedImageLabelCount > 0;

            return new[]
            {
                new WpfDatasetDashboardMetricItem(
                    "\uC774\uBBF8\uC9C0",
                    statistics.TotalImageCount.ToString(),
                    $"train {statistics.TrainImageCount}, valid {statistics.ValidImageCount}, test {statistics.TestImageCount}",
                    hasImages ? "\uC644\uB8CC" : "\uD544\uC694",
                    PackIconMaterialKind.FolderImage,
                    isProblem: !hasImages,
                    isWarning: false,
                    actionKind: WpfDatasetDashboardActionKind.OpenImages),
                new WpfDatasetDashboardMetricItem(
                    "\uC9C4\uD589",
                    $"{visibleCompletedImageLabelCount}/{statistics.TotalImageCount}",
                    $"\uAC80\uD1A0\uB41C \uB77C\uBCA8 \uD30C\uC77C {completedImageLabelCount}\uAC1C, \uC9C4\uD589\uB960 {progressPercent}%",
                    !hasImages ? "\uB300\uAE30" : isLabelingComplete ? "\uC644\uB8CC" : hasAnyCompletedImageLabel ? "\uC9C4\uD589" : "\uD544\uC694",
                    PackIconMaterialKind.ProgressClock,
                    isProblem: hasImages && !isLabelingComplete,
                    isWarning: false,
                    actionKind: WpfDatasetDashboardActionKind.OpenLabelingProgress),
                new WpfDatasetDashboardMetricItem(
                    "\uBD84\uD560",
                    $"{statistics.TrainImageCount}/{statistics.ValidImageCount}/{statistics.TestImageCount}",
                    "\uD559\uC2B5 / \uAC80\uC99D / \uCD5C\uC885 \uAC80\uC99D",
                    !hasTrain || !hasValid ? "\uD655\uC778" : hasTest ? "\uC644\uB8CC" : "\uD14C\uC2A4\uD2B8 \uC5C6\uC74C",
                    PackIconMaterialKind.CheckAll,
                    isProblem: !hasTrain || !hasValid,
                    isWarning: hasTrain && hasValid && !hasTest,
                    actionKind: WpfDatasetDashboardActionKind.OpenDatasetSettings),
                new WpfDatasetDashboardMetricItem(
                    "\uAD50\uCCB4",
                    report?.IsReady == true
                        ? hasReplacementEvidence ? hasStrongReplacementEvidence ? "\uAC00\uB2A5" : "\uC8FC\uC758" : "\uBCF4\uB958"
                        : "\uBD88\uAC00",
                    hasReplacementEvidence
                        ? hasStrongReplacementEvidence
                            ? $"\uCD5C\uC885 \uAC80\uC99D \uB77C\uBCA8 {finalVerificationCount}\uC7A5"
                            : $"\uCD5C\uC885 \uAC80\uC99D \uB77C\uBCA8 {finalVerificationCount}\uC7A5 / \uAD8C\uC7A5 {RecommendedModelReplacementTestImageCount}\uC7A5 \uC774\uC0C1"
                        : hasTest
                            ? "\uCD5C\uC885 \uAC80\uC99D \uB77C\uBCA8 0\uC7A5: \uC815\uB2F5 \uB77C\uBCA8 \uD544\uC694"
                            : "\uCD5C\uC885 \uAC80\uC99D 0\uC7A5: \uD559\uC2B5/\uAC80\uC99D\uB9CC \uC788\uC74C",
                    report?.IsReady == true
                        ? hasReplacementEvidence ? hasStrongReplacementEvidence ? "\uC644\uB8CC" : "\uADFC\uAC70 \uBD80\uC871" : "\uCD5C\uC885 \uB77C\uBCA8 \uD544\uC694"
                        : "\uD559\uC2B5 \uBA3C\uC800",
                    hasStrongReplacementEvidence ? PackIconMaterialKind.CheckCircleOutline : PackIconMaterialKind.AlertCircleOutline,
                    isProblem: report?.IsReady != true,
                    isWarning: report?.IsReady == true && !hasStrongReplacementEvidence,
                    actionKind: WpfDatasetDashboardActionKind.OpenDatasetSettings),
                new WpfDatasetDashboardMetricItem(
                    needsBoxLabels ? "\uBC15\uC2A4" : "\uC138\uADF8",
                    primaryLabelValue.ToString(),
                    needsBoxLabels
                        ? $"\uBC15\uC2A4 {statistics.TotalObjectCount}\uAC1C, \uB77C\uBCA8 \uD30C\uC77C {statistics.TotalLabelFileCount}\uAC1C"
                        : $"\uC138\uADF8\uBA58\uD2B8 {statistics.TotalSegmentationObjectCount}\uAC1C, \uB9C8\uC2A4\uD06C {statistics.TotalMaskFileCount}\uAC1C",
                    hasPrimaryLabels ? "\uC644\uB8CC" : "\uD544\uC694",
                    needsBoxLabels ? PackIconMaterialKind.ShapeSquareRoundedPlus : PackIconMaterialKind.ViewListOutline,
                    isProblem: !hasPrimaryLabels,
                    isWarning: false,
                    actionKind: WpfDatasetDashboardActionKind.OpenLabelingTool),
                new WpfDatasetDashboardMetricItem(
                    "\uB77C\uBCA8 \uD30C\uC77C",
                    artifactFileCount.ToString(),
                    needsBoxLabels
                        ? $"\uD559\uC2B5 {statistics.TrainLabelCount}\uAC1C, \uAC80\uC99D {statistics.ValidLabelCount}\uAC1C, \uCD5C\uC885 {statistics.TestLabelCount}\uAC1C"
                        : $"\uC138\uADF8\uBA58\uD2B8 \uD30C\uC77C {statistics.TotalSegmentFileCount}\uAC1C, \uB9C8\uC2A4\uD06C {statistics.TotalMaskFileCount}\uAC1C",
                    artifactFileCount > 0 ? "\uC788\uC74C" : "\uD544\uC694",
                    PackIconMaterialKind.FileDocumentOutline,
                    isProblem: artifactFileCount == 0,
                    isWarning: false,
                    actionKind: WpfDatasetDashboardActionKind.CheckDataset),
                new WpfDatasetDashboardMetricItem(
                    "\uD074\uB798\uC2A4",
                    classCount.ToString(),
                    "\uBAA8\uB378\uC774 \uBC30\uC6B8 \uC774\uB984",
                    classCount > 0 ? "\uC644\uB8CC" : "\uD544\uC694",
                    PackIconMaterialKind.TagMultipleOutline,
                    isProblem: classCount == 0,
                    isWarning: false,
                    actionKind: WpfDatasetDashboardActionKind.OpenClassCatalog),
                new WpfDatasetDashboardMetricItem(
                    "\uC911\uBCF5",
                    (statistics.TrainValidImageContentOverlapCount + statistics.SplitImageContentOverlapCount).ToString(),
                    FirstNonEmpty(statistics.TrainValidImageOverlapExample, statistics.SplitImageOverlapExample, "\uBD84\uD560 \uC911\uBCF5"),
                    hasSplitOverlap ? "\uBD84\uB9AC \uD544\uC694" : "\uC5C6\uC74C",
                    PackIconMaterialKind.AlertCircleOutline,
                    isProblem: hasSplitOverlap,
                    isWarning: false,
                    actionKind: WpfDatasetDashboardActionKind.OpenDatasetSettings)
            };
        }

        private static string BuildModelReplacementStatusText(YoloDatasetReadinessReport report, YoloDatasetStatistics statistics)
        {
            if (report?.IsReady != true)
            {
                return "\uBAA8\uB378 \uAD50\uCCB4: \uBD88\uAC00";
            }

            int testImageCount = statistics?.TestImageCount ?? 0;
            int testLabelCount = statistics?.TestLabelCount ?? 0;
            int finalVerificationCount = Math.Min(testImageCount, testLabelCount);
            return testImageCount > 0 && testLabelCount > 0
                ? finalVerificationCount >= RecommendedModelReplacementTestImageCount
                    ? "\uBAA8\uB378 \uAD50\uCCB4: \uAC00\uB2A5"
                    : "\uBAA8\uB378 \uAD50\uCCB4: \uADFC\uAC70 \uBD80\uC871"
                : "\uBAA8\uB378 \uAD50\uCCB4: \uBCF4\uB958";
        }

        private static string BuildModelReplacementDetailText(YoloDatasetReadinessReport report, YoloDatasetStatistics statistics)
        {
            if (report?.IsReady != true)
            {
                return "\uBA3C\uC800 \uD559\uC2B5 \uBD88\uAC00 \uD56D\uBAA9\uC744 \uD574\uACB0\uD574\uC57C \uBAA8\uB378 \uAD50\uCCB4\uB97C \uD310\uB2E8\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.";
            }

            int testCount = statistics?.TestImageCount ?? 0;
            int testLabelCount = statistics?.TestLabelCount ?? 0;
            int finalVerificationCount = Math.Min(testCount, testLabelCount);
            if (testCount > 0 && testLabelCount <= 0)
            {
                return "\uCD5C\uC885 \uAC80\uC99D \uC774\uBBF8\uC9C0\uB294 \uC788\uC9C0\uB9CC \uC815\uB2F5 \uB77C\uBCA8 \uD30C\uC77C\uC774 \uC5C6\uC2B5\uB2C8\uB2E4. \uBAA8\uB378 \uBE44\uAD50 \uC804 \uCD5C\uC885 \uAC80\uC99D \uB77C\uBCA8\uC744 \uC800\uC7A5\uD558\uC138\uC694.";
            }

            if (finalVerificationCount > 0)
            {
                if (finalVerificationCount < RecommendedModelReplacementTestImageCount)
                {
                    int neededCount = RecommendedModelReplacementTestImageCount - finalVerificationCount;
                    return $"\uCD5C\uC885 \uAC80\uC99D \uB77C\uBCA8 {finalVerificationCount}\uC7A5\uC73C\uB85C \uBE44\uAD50\uB294 \uAC00\uB2A5\uD558\uC9C0\uB9CC \uAD50\uCCB4 \uADFC\uAC70\uAC00 \uC57D\uD569\uB2C8\uB2E4. \uAD8C\uC7A5 {RecommendedModelReplacementTestImageCount}\uC7A5\uAE4C\uC9C0 {neededCount}\uC7A5\uC744 \uB354 \uD655\uBCF4\uD558\uC138\uC694.";
                }

                return $"\uCD5C\uC885 \uAC80\uC99D \uB77C\uBCA8 {finalVerificationCount}\uC7A5\uC73C\uB85C \uAE30\uC874 \uBAA8\uB378\uACFC \uBE44\uAD50 \uD6C4 \uAD50\uCCB4\uB97C \uD310\uB2E8\uD558\uC138\uC694.";
            }

            return "\uD559\uC2B5/\uAC80\uC99D \uC774\uBBF8\uC9C0\uB85C \uD559\uC2B5\uC740 \uAC00\uB2A5\uD558\uC9C0\uB9CC \uCD5C\uC885 \uAC80\uC99D \uC774\uBBF8\uC9C0\uAC00 0\uC7A5\uC774\uBBC0\uB85C \uAE30\uC874 \uBAA8\uB378 \uAD50\uCCB4\uB294 \uBCF4\uB958\uD558\uC138\uC694.";
        }

        private static IReadOnlyList<string> BuildDatasetDashboardIssues(
            YoloDatasetReadinessReport report,
            IReadOnlyList<string> warnings,
            int classCount)
        {
            var issues = new List<string>();
            string nextAction = BuildObjectDetectionNextActionIssue(report, classCount);
            if (!string.IsNullOrWhiteSpace(nextAction))
            {
                issues.Add(nextAction);
            }

            if (report?.IsReady == true)
            {
                if (warnings != null && warnings.Count > 0)
                {
                    foreach (string warning in warnings.Take(3))
                    {
                        issues.Add("\uC8FC\uC758: " + FormatDatasetQualityWarning(warning));
                    }
                }
                else
                {
                    issues.Add("\uBB38\uC81C \uC5C6\uC74C: \uD559\uC2B5 \uC124\uC815\uC73C\uB85C \uC774\uB3D9\uD574\uB3C4 \uB429\uB2C8\uB2E4.");
                }

                return issues;
            }

            IReadOnlyList<string> errors = report?.Errors ?? Array.Empty<string>();
            string issueKind = ClassifyYoloTrainingIssue(errors);
            issues.Add(BuildDatasetDashboardFriendlyIssue(issueKind));
            foreach (string error in errors.Take(2))
            {
                if (!string.IsNullOrWhiteSpace(error))
                {
                    issues.Add("\uC138\uBD80: " + error);
                }
            }

            return issues;
        }

        private static string BuildObjectDetectionNextActionIssue(
            YoloDatasetReadinessReport report,
            int classCount)
        {
            if (report == null || report.Purpose != LabelingDatasetPurpose.ObjectDetection)
            {
                return string.Empty;
            }

            YoloDatasetStatistics statistics = report.Statistics ?? new YoloDatasetStatistics();
            if (statistics.TotalImageCount <= 0)
            {
                return "다음: 이미지 폴더를 열어 객체탐지 데이터셋을 시작하세요.";
            }

            if (classCount <= 0)
            {
                return "다음: 클래스 탭에서 모델이 배울 이름을 등록하세요.";
            }

            if (statistics.TotalLabelFileCount <= 0 && statistics.TotalObjectCount <= 0)
            {
                return "다음: 박스 도구로 첫 객체를 라벨링하고 저장하세요.";
            }

            int completedImageCount = Math.Min(statistics.TotalLabelFileCount, statistics.TotalImageCount);
            if (completedImageCount < statistics.TotalImageCount)
            {
                int remainingImageCount = Math.Max(0, statistics.TotalImageCount - completedImageCount);
                return $"다음: 미완료 {remainingImageCount}장에 박스를 저장하거나 빈 정상 이미지로 완료 처리하세요.";
            }

            bool hasSplitOverlap = statistics.TrainValidImageContentOverlapCount > 0
                || statistics.SplitImageContentOverlapCount > 0;
            if (hasSplitOverlap)
            {
                return "\uB2E4\uC74C: \uD559\uC2B5/\uAC80\uC99D/\uCD5C\uC885 \uAC80\uC99D\uC5D0 \uAC19\uC740 \uC774\uBBF8\uC9C0\uAC00 \uC11E\uC774\uC9C0 \uC54A\uB3C4\uB85D \uBD84\uB9AC\uD558\uC138\uC694.";
            }

            if (statistics.ValidImageCount <= 0)
            {
                return "\uB2E4\uC74C: \uAC80\uC99D \uC774\uBBF8\uC9C0\uB97C 1\uC7A5 \uC774\uC0C1 \uD655\uBCF4\uD574 \uD559\uC2B5 \uAC80\uC99D\uC774 \uAC00\uB2A5\uD558\uAC8C \uB9CC\uB4DC\uC138\uC694.";
            }

            if (statistics.TestImageCount <= 0)
            {
                return "\uB2E4\uC74C: \uBAA8\uB378 \uAD50\uCCB4 \uD310\uB2E8 \uC804 \uCD5C\uC885 \uAC80\uC99D \uC774\uBBF8\uC9C0\uB97C 1\uC7A5 \uC774\uC0C1 \uD655\uBCF4\uD558\uC138\uC694.";
            }

            if (statistics.TestImageCount > 0 && statistics.TestLabelCount <= 0)
            {
                return "\uB2E4\uC74C: \uCD5C\uC885 \uAC80\uC99D \uC774\uBBF8\uC9C0\uC5D0 \uC815\uB2F5 \uB77C\uBCA8\uC744 \uC800\uC7A5\uD558\uC138\uC694.";
            }

            int finalVerificationCount = Math.Min(statistics.TestImageCount, statistics.TestLabelCount);
            if (finalVerificationCount < RecommendedModelReplacementTestImageCount)
            {
                int neededCount = RecommendedModelReplacementTestImageCount - finalVerificationCount;
                return $"\uB2E4\uC74C: \uBAA8\uB378 \uAD50\uCCB4 \uADFC\uAC70\uB97C \uB192\uC774\uB824\uBA74 \uCD5C\uC885 \uAC80\uC99D \uB77C\uBCA8\uC744 {neededCount}\uC7A5 \uB354 \uD655\uBCF4\uD558\uC138\uC694.";
            }

            return report.IsReady
                ? "\uC644\uB8CC: \uAC1D\uCCB4\uD0D0\uC9C0 \uB77C\uBCA8\uB9C1 MVP\uAC00 \uC900\uBE44\uB418\uC5C8\uC2B5\uB2C8\uB2E4. \uD559\uC2B5 \uD6C4 \uCD5C\uC885 \uAC80\uC99D \uBE44\uAD50\uB85C \uBAA8\uB378 \uAD50\uCCB4\uB97C \uD310\uB2E8\uD558\uC138\uC694."
                : "다음: 데이터셋 점검 결과의 세부 오류를 먼저 해결하세요.";
        }

        private static string BuildDatasetDashboardFriendlyIssue(string issueKind)
        {
            return issueKind switch
            {
                "Classes" => "\uD074\uB798\uC2A4 \uB4F1\uB85D\uC774 \uD544\uC694\uD569\uB2C8\uB2E4. \uBAA8\uB378\uC774 \uBC30\uC6B8 \uC774\uB984\uC744 \uBA3C\uC800 \uB9CC\uB4DC\uC138\uC694.",
                "Labels" => "\uB77C\uBCA8 \uC800\uC7A5\uC774 \uD544\uC694\uD569\uB2C8\uB2E4. \uBC15\uC2A4\uB97C \uADF8\uB9AC\uACE0 \uC800\uC7A5\uD55C \uB4A4 \uB2E4\uC2DC \uC810\uAC80\uD558\uC138\uC694.",
                "SegmentationPolicy" => "\uAC1D\uCCB4 \uD0D0\uC9C0\uB294 \uBC15\uC2A4 txt \uB77C\uBCA8\uC774 \uD544\uC694\uD569\uB2C8\uB2E4.",
                "SegmentationLabels" => "\uC138\uADF8\uBA58\uD14C\uC774\uC158\uC740 \uBE0C\uB7EC\uC2DC/\uD3F4\uB9AC\uACE4 \uB77C\uBCA8\uC774 \uD544\uC694\uD569\uB2C8\uB2E4.",
                "ValidImages" => "\uAC80\uC99D \uC774\uBBF8\uC9C0\uAC00 \uD544\uC694\uD569\uB2C8\uB2E4. \uBD84\uD560 \uC124\uC815\uC744 \uD655\uC778\uD558\uC138\uC694.",
                "Split" => "\uD559\uC2B5/\uAC80\uC99D/\uCD5C\uC885 \uAC80\uC99D\uC5D0 \uAC19\uC740 \uC774\uBBF8\uC9C0\uAC00 \uC11E\uC5EC \uC788\uC2B5\uB2C8\uB2E4. \uD559\uC2B5\uACFC \uAC80\uC99D\uC740 \uB2E4\uB978 \uC774\uBBF8\uC9C0\uB85C \uBD84\uB9AC\uD558\uC138\uC694.",
                "DataYaml" => "\uD559\uC2B5 \uC124\uC815\uC774 \uD604\uC7AC \uD504\uB85C\uC81D\uD2B8\uC640 \uB9DE\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4. \uB370\uC774\uD130\uC14B \uC810\uAC80\uC744 \uB2E4\uC2DC \uC2E4\uD589\uD558\uC138\uC694.",
                "LabelFormat" => "\uBC15\uC2A4 \uB77C\uBCA8 \uD30C\uC77C \uD615\uC2DD\uC774 \uC798\uBABB\uB41C \uD30C\uC77C\uC774 \uC788\uC2B5\uB2C8\uB2E4.",
                "OutputRoot" => "\uB370\uC774\uD130\uC14B \uC800\uC7A5 \uACBD\uB85C\uB97C \uD655\uC778\uD558\uC138\uC694.",
                "Images" => "\uD559\uC2B5\uD560 \uC774\uBBF8\uC9C0 \uD3F4\uB354\uB97C \uB2E4\uC2DC \uC120\uD0DD\uD558\uC138\uC694.",
                _ => "\uB370\uC774\uD130\uC14B \uC810\uAC80 \uACB0\uACFC\uB97C \uD655\uC778\uD558\uACE0 \uD544\uC694\uD55C \uD56D\uBAA9\uC744 \uC218\uC815\uD558\uC138\uC694."
            };
        }

        private YoloTrainingIssuePresentation BuildYoloTrainingIssuePresentation(YoloDatasetReadinessReport report)
        {
            if (report?.IsReady == true)
            {
                YoloDatasetStatistics statistics = report.Statistics;
                LabelingDatasetPurpose purpose = report.Purpose;
                int classCount = global.Data?.ClassNamedList?.Count ?? 0;
                IReadOnlyList<string> warnings = YoloDatasetDiagnosticsService.BuildQualityWarnings(global.Data, statistics);
                bool hasWarnings = warnings.Count > 0;
                return new YoloTrainingIssuePresentation(
                    hasWarnings ? "ReadyWithWarnings" : "Ready",
                    BuildReadyDatasetStatusText(statistics, purpose, hasWarnings),
                    BuildReadyDatasetDetail(statistics, classCount, purpose),
                    hasWarnings
                        ? BuildQualityWarningActionText(warnings)
                        : BuildReadyDatasetActionText(purpose));
            }

            string firstError = report?.Errors?.FirstOrDefault() ?? "원인 미확인";
            string issueKind = ClassifyYoloTrainingIssue(report?.Errors ?? Array.Empty<string>());
            LabelingDatasetPurpose failurePurpose = report?.Purpose ?? LabelingDatasetPurpose.ObjectDetection;
            string failureDetail = BuildDatasetFailureDetail(firstError, report?.Statistics, failurePurpose);
            return issueKind switch
            {
                "Classes" => new YoloTrainingIssuePresentation(
                    issueKind,
                    "데이터셋: 학습 불가 / 클래스 등록 필요",
                    failureDetail,
                    "클래스 등록 버튼으로 이동해 모델이 배울 이름을 먼저 등록하세요."),
                "Labels" => new YoloTrainingIssuePresentation(
                    issueKind,
                    "데이터셋: 학습 불가 / 라벨 저장 필요",
                    failureDetail,
                    "라벨링 시작 버튼으로 이동해 박스를 그리고 저장한 뒤 다시 점검하세요."),
                "SegmentationPolicy" => new YoloTrainingIssuePresentation(
                    issueKind,
                    "데이터셋: 세그먼트 저장됨 / 박스 라벨 필요",
                    failureDetail,
                    "\uD604\uC7AC \uAC1D\uCCB4\uD0D0\uC9C0 \uD559\uC2B5\uC740 \uBC15\uC2A4 \uB77C\uBCA8 \uD30C\uC77C\uC774 \uD544\uC694\uD569\uB2C8\uB2E4. \uBC15\uC2A4 \uB77C\uBCA8\uC744 \uCD94\uAC00\uD558\uAC70\uB098 segmentation \uD559\uC2B5/export \uC815\uCC45\uC744 \uC120\uD0DD\uD558\uC138\uC694."),
                "SegmentationLabels" => new YoloTrainingIssuePresentation(
                    issueKind,
                    "데이터셋: 세그멘테이션 준비 불가 / 마스크 라벨 필요",
                    failureDetail,
                    "세그멘테이션 목적에서는 브러시나 폴리곤으로 마스크를 저장한 뒤 다시 데이터셋 점검을 실행하세요."),
                "ValidImages" => new YoloTrainingIssuePresentation(
                    issueKind,
                    "데이터셋: 학습 불가 / 검증 이미지 필요",
                    failureDetail,
                    "데이터셋 점검으로 이동해 train/valid 분리와 validation 비율을 다시 확인하세요."),
                "Split" => new YoloTrainingIssuePresentation(
                    issueKind,
                    "데이터셋: 학습 불가 / train-valid-test 중복 분리 필요",
                    failureDetail,
                    "검증/테스트 이미지는 학습 이미지와 달라야 합니다. 이미지 폴더를 다시 나누거나 split 비율과 샘플 구성을 점검하세요."),
                "DataYaml" => new YoloTrainingIssuePresentation(
                    issueKind,
                    "\uB370\uC774\uD130\uC14B: \uD559\uC2B5 \uBD88\uAC00 / \uD559\uC2B5 \uC124\uC815 \uD655\uC778 \uD544\uC694",
                    failureDetail,
                    "\uB370\uC774\uD130\uC14B \uC810\uAC80 \uBC84\uD2BC\uC73C\uB85C \uD559\uC2B5 \uC124\uC815\uC744 \uB2E4\uC2DC \uC0DD\uC131\uD558\uACE0 \uC800\uC7A5 \uACBD\uB85C\uB97C \uD655\uC778\uD558\uC138\uC694."),
                "LabelFormat" => new YoloTrainingIssuePresentation(
                    issueKind,
                    "데이터셋: 학습 불가 / 라벨 형식 오류",
                    failureDetail,
                    "문제가 있는 txt 라벨을 다시 저장하거나 해당 이미지를 다시 라벨링하세요."),
                "OutputRoot" => new YoloTrainingIssuePresentation(
                    issueKind,
                    "데이터셋: 학습 불가 / 저장 경로 확인 필요",
                    failureDetail,
                    "\uB370\uC774\uD130\uC14B \uD648\uC5D0\uC11C \uC800\uC7A5 \uD3F4\uB354\uB97C \uD655\uC778\uD558\uACE0 \uC124\uC815\uC744 \uC800\uC7A5\uD558\uC138\uC694."),
                "Images" => new YoloTrainingIssuePresentation(
                    issueKind,
                    "데이터셋: 학습 불가 / 이미지 폴더 확인 필요",
                    failureDetail,
                    "1단계에서 학습 이미지 폴더를 다시 열고, 지원되는 이미지가 있는지 확인하세요."),
                _ => new YoloTrainingIssuePresentation(
                    issueKind,
                    "데이터셋: 학습 불가 / 확인 필요",
                    failureDetail,
                    "원인을 확인한 뒤 클래스, 라벨링, 데이터셋 점검 중 맞는 항목으로 이동하세요.")
            };
        }

        private static string BuildReadyDatasetStatusText(
            YoloDatasetStatistics statistics,
            LabelingDatasetPurpose purpose,
            bool hasWarnings)
        {
            statistics ??= new YoloDatasetStatistics();
            string readinessText = hasWarnings ? "주의 후 학습 가능" : "학습 가능";
            string purposeText = BuildDatasetPurposeDisplayName(purpose);
            int imageCount = statistics.TotalImageCount;

            return purpose switch
            {
                LabelingDatasetPurpose.Segmentation =>
                    $"데이터셋: {purposeText} {readinessText} / 이미지 {imageCount} / {BuildSegmentationPrimaryLabelText(statistics)} / 박스 라벨 {statistics.TotalObjectCount} 보조",
                LabelingDatasetPurpose.AnomalyDetection =>
                    $"데이터셋: {purposeText} {readinessText} / 이미지 {imageCount} / 결함 박스 {statistics.TotalObjectCount} / 세그 라벨 {statistics.TotalSegmentationObjectCount} 보조",
                _ =>
                    $"데이터셋: {purposeText} {readinessText} / 이미지 {imageCount} / 박스 라벨 {statistics.TotalObjectCount} / 세그 라벨 {statistics.TotalSegmentationObjectCount} 제외"
            };
        }

        private static string BuildReadyDatasetDetail(YoloDatasetStatistics statistics, int classCount, LabelingDatasetPurpose purpose)
        {
            if (statistics == null)
            {
                return $"purpose {purpose}, train 0, valid 0, test 0, box labels 0, segment labels 0, classes 0";
            }

            return $"purpose {purpose}, train {statistics.TrainImageCount}, valid {statistics.ValidImageCount}, test {statistics.TestImageCount}, box labels {statistics.TotalObjectCount} ({statistics.TotalLabelFileCount} files), segment labels {statistics.TotalSegmentationObjectCount} ({statistics.TotalSegmentFileCount} files), masks {statistics.TotalMaskFileCount}, classes {classCount}";
        }

        private static string BuildSegmentationPrimaryLabelText(YoloDatasetStatistics statistics)
        {
            if (statistics == null)
            {
                return "세그 라벨 0";
            }

            if (statistics.TotalSegmentationObjectCount > 0)
            {
                return $"세그 라벨 {statistics.TotalSegmentationObjectCount}";
            }

            return statistics.TotalMaskFileCount > 0
                ? $"마스크 {statistics.TotalMaskFileCount}파일"
                : "세그 라벨 0";
        }

        private static string BuildReadyDatasetActionText(LabelingDatasetPurpose purpose)
        {
            return purpose switch
            {
                LabelingDatasetPurpose.Segmentation => "이제 세그멘테이션 학습/export 설정을 확인하고 시작하세요.",
                LabelingDatasetPurpose.AnomalyDetection => "이제 이상 탐지 학습 설정을 확인하고 시작하세요.",
                _ => "\uC774\uC81C 5\uB2E8\uACC4 \uBAA8\uB378 \uD559\uC2B5\uC5D0\uC11C \uC124\uC815\uC744 \uD655\uC778\uD558\uACE0 \uC2DC\uC791\uD558\uC138\uC694."
            };
        }

        private static string BuildQualityWarningActionText(IReadOnlyList<string> warnings)
        {
            if (warnings == null || warnings.Count == 0)
            {
                return "\uC774\uC81C 5\uB2E8\uACC4 \uBAA8\uB378 \uD559\uC2B5\uC5D0\uC11C \uC124\uC815\uC744 \uD655\uC778\uD558\uACE0 \uC2DC\uC791\uD558\uC138\uC694.";
            }

            return string.Join(" / ", warnings.Take(2).Select(FormatDatasetQualityWarning));
        }

        private static string FormatDatasetQualityWarning(string warning)
        {
            if (string.IsNullOrWhiteSpace(warning))
            {
                return "\uD655\uC778\uC774 \uD544\uC694\uD55C \uB370\uC774\uD130\uC14B \uACBD\uACE0\uAC00 \uC788\uC2B5\uB2C8\uB2E4.";
            }

            string normalized = warning.Trim();
            if (normalized.Contains("Test split is empty", StringComparison.OrdinalIgnoreCase))
            {
                return "\uCD5C\uC885 \uAC80\uC99D \uC774\uBBF8\uC9C0\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4. \uBAA8\uB378 \uAD50\uCCB4 \uD310\uB2E8 \uC804 1\uC7A5 \uC774\uC0C1 \uD655\uBCF4\uD558\uC138\uC694.";
            }

            if (normalized.Contains("YOLO split guide", StringComparison.OrdinalIgnoreCase))
            {
                return "\uAC80\uC99D\uACFC \uCD5C\uC885 \uAC80\uC99D \uBD84\uD560 \uAE30\uC900\uC744 \uD655\uC778\uD558\uC138\uC694.";
            }

            return normalized
                .Replace("train/valid/test", "\uD559\uC2B5/\uAC80\uC99D/\uCD5C\uC885 \uAC80\uC99D", StringComparison.OrdinalIgnoreCase)
                .Replace("train", "\uD559\uC2B5", StringComparison.OrdinalIgnoreCase)
                .Replace("valid", "\uAC80\uC99D", StringComparison.OrdinalIgnoreCase)
                .Replace("test split", "\uCD5C\uC885 \uAC80\uC99D", StringComparison.OrdinalIgnoreCase)
                .Replace("test", "\uCD5C\uC885", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildDatasetFailureDetail(string firstError, YoloDatasetStatistics statistics, LabelingDatasetPurpose purpose)
        {
            string issue = string.IsNullOrWhiteSpace(firstError) ? "원인 미확인" : firstError;
            if (statistics == null)
            {
                return $"purpose {purpose}, {issue}";
            }

            return $"purpose {purpose}, {issue} / train {statistics.TrainImageCount}, valid {statistics.ValidImageCount}, test {statistics.TestImageCount}, box labels {statistics.TotalObjectCount}, segment labels {statistics.TotalSegmentationObjectCount}, masks {statistics.TotalMaskFileCount}";
        }

        private static string BuildDatasetPurposeDisplayName(LabelingDatasetPurpose purpose)
        {
            return purpose switch
            {
                LabelingDatasetPurpose.Segmentation => "세그멘테이션",
                LabelingDatasetPurpose.AnomalyDetection => "이상 탐지",
                _ => "객체 탐지"
            };
        }

        private static string ClassifyYoloTrainingIssue(IEnumerable<string> errors)
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

        private sealed class YoloTrainingIssuePresentation
        {
            public YoloTrainingIssuePresentation(string issueKind, string statusText, string detailText, string actionText)
            {
                IssueKind = issueKind ?? string.Empty;
                StatusText = statusText ?? string.Empty;
                DetailText = detailText ?? string.Empty;
                ActionText = actionText ?? string.Empty;
            }

            public string IssueKind { get; }

            public string StatusText { get; }

            public string DetailText { get; }

            public string ActionText { get; }
        }
    }
}
