using MahApps.Metro.IconPacks;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem
{
    /// <summary>
    /// Builds the read-only metric cards shown by the dataset training dashboard.
    /// </summary>
    public static class WpfDatasetDashboardPresentationService
    {
        private const int RecommendedModelReplacementTestImageCount = 10;

        public static IReadOnlyList<WpfDatasetDashboardMetricItem> BuildMetrics(
            YoloDatasetReadinessReport report,
            YoloDatasetStatistics statistics,
            int classCount,
            AnomalyImageReviewSummary anomalySummary = null,
            YoloDatasetQualityAuditReport qualityAudit = null)
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
            bool isAnomaly = purpose == LabelingDatasetPurpose.AnomalyDetection;
            bool needsBoxLabels = purpose == LabelingDatasetPurpose.ObjectDetection;
            int anomalyReviewedCount = statistics.AnomalyNormalImageCount + statistics.AnomalyAbnormalImageCount;
            bool hasPrimaryLabels = isAnomaly
                ? statistics.AnomalyNormalImageCount > 0 && statistics.AnomalyAbnormalImageCount > 0
                : needsBoxLabels
                    ? statistics.TotalObjectCount > 0
                    : statistics.TotalSegmentationObjectCount > 0 || statistics.TotalMaskFileCount > 0;
            int primaryLabelValue = isAnomaly
                ? anomalyReviewedCount
                : needsBoxLabels
                    ? statistics.TotalObjectCount
                    : Math.Max(statistics.TotalSegmentationObjectCount, statistics.TotalMaskFileCount);
            int artifactFileCount = isAnomaly
                ? anomalyReviewedCount
                : needsBoxLabels
                    ? statistics.TotalLabelFileCount
                    : statistics.TotalSegmentFileCount + statistics.TotalMaskFileCount;
            int completedImageLabelCount = artifactFileCount;
            int visibleCompletedImageLabelCount = statistics.TotalImageCount > 0
                ? Math.Min(completedImageLabelCount, statistics.TotalImageCount)
                : 0;
            int progressPercent = statistics.TotalImageCount > 0
                ? (int)Math.Round(visibleCompletedImageLabelCount * 100D / statistics.TotalImageCount)
                : 0;
            bool isLabelingComplete = hasImages && completedImageLabelCount >= statistics.TotalImageCount;
            bool hasAnyCompletedImageLabel = completedImageLabelCount > 0;

            var metrics = new List<WpfDatasetDashboardMetricItem>
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
                    isAnomaly ? "판정" : needsBoxLabels ? "\uBC15\uC2A4" : "\uC138\uADF8",
                    primaryLabelValue.ToString(),
                    isAnomaly
                        ? $"정상 {statistics.AnomalyNormalImageCount}장, 이상 {statistics.AnomalyAbnormalImageCount}장"
                        : needsBoxLabels
                        ? $"\uBC15\uC2A4 {statistics.TotalObjectCount}\uAC1C, \uB77C\uBCA8 \uD30C\uC77C {statistics.TotalLabelFileCount}\uAC1C"
                        : $"\uC138\uADF8\uBA58\uD2B8 {statistics.TotalSegmentationObjectCount}\uAC1C, \uB9C8\uC2A4\uD06C {statistics.TotalMaskFileCount}\uAC1C",
                    hasPrimaryLabels ? "\uC644\uB8CC" : "\uD544\uC694",
                    isAnomaly ? PackIconMaterialKind.CheckCircleOutline : needsBoxLabels ? PackIconMaterialKind.ShapeSquareRoundedPlus : PackIconMaterialKind.ViewListOutline,
                    isProblem: !hasPrimaryLabels,
                    isWarning: false,
                    actionKind: WpfDatasetDashboardActionKind.OpenLabelingTool),
                new WpfDatasetDashboardMetricItem(
                    isAnomaly ? "판정 이미지" : needsBoxLabels ? "\uB77C\uBCA8 \uD30C\uC77C" : "SEG \uAC80\uD1A0",
                    artifactFileCount.ToString(),
                    isAnomaly
                        ? $"학습 {statistics.TrainImageCount}장, 검증 {statistics.ValidImageCount}장, 최종 {statistics.TestImageCount}장"
                        : needsBoxLabels
                        ? $"\uD559\uC2B5 {statistics.TrainLabelCount}\uAC1C, \uAC80\uC99D {statistics.ValidLabelCount}\uAC1C, \uCD5C\uC885 {statistics.TestLabelCount}\uAC1C"
                        : $"\uC138\uADF8\uBA58\uD2B8 {statistics.TotalSegmentFileCount}\uAC1C, \uB9C8\uC2A4\uD06C {statistics.TotalMaskFileCount}\uAC1C / \uD074\uB9AD: \uAE30\uC874 \uB9C8\uC2A4\uD06C \uBCF4\uC815 \uB4DC\uB77C\uC774\uB7F0 \uBCF4\uACE0\uC11C",
                    artifactFileCount > 0 ? isAnomaly ? "완료" : needsBoxLabels ? "\uC788\uC74C" : "\uAC80\uD1A0" : "\uD544\uC694",
                    PackIconMaterialKind.FileDocumentOutline,
                    isProblem: artifactFileCount == 0,
                    isWarning: false,
                    actionKind: isAnomaly || needsBoxLabels
                        ? WpfDatasetDashboardActionKind.CheckDataset
                        : WpfDatasetDashboardActionKind.ExportHistoricalSegmentationRemediationAudit),
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

            if (purpose == LabelingDatasetPurpose.AnomalyDetection)
            {
                metrics.Insert(Math.Min(2, metrics.Count), WpfAnomalyDashboardPresentationService.BuildReviewStateMetric(anomalySummary));
            }

            if (qualityAudit != null)
            {
                metrics.Insert(Math.Min(purpose == LabelingDatasetPurpose.AnomalyDetection ? 3 : 2, metrics.Count), WpfDatasetQualityAuditPresentationService.BuildQualityMetric(qualityAudit));
            }

            return metrics;
        }
        private static string FirstNonEmpty(params string[] values)
        {
            return values?.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }
    }
}
