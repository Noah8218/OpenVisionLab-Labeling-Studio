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
    public static class DatasetDashboardPresentationService
    {
        public const int RecommendedModelReplacementTestImageCount = 10;

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

            string replacementValueKey = report?.IsReady == true
                ? hasReplacementEvidence
                    ? hasStrongReplacementEvidence
                        ? "WpfLearningWorkflow.DatasetDashboard.Metric.Replacement.Value.Available"
                        : "WpfLearningWorkflow.DatasetDashboard.Metric.Replacement.Value.Caution"
                    : "WpfLearningWorkflow.DatasetDashboard.Metric.Replacement.Value.Hold"
                : "WpfLearningWorkflow.DatasetDashboard.Metric.Replacement.Value.Unavailable";
            string replacementValueFallback = string.Empty;
            string replacementDetailKey = hasReplacementEvidence
                ? hasStrongReplacementEvidence
                    ? "WpfLearningWorkflow.DatasetDashboard.Metric.Replacement.Detail.Evidence"
                    : "WpfLearningWorkflow.DatasetDashboard.Metric.Replacement.Detail.Weak"
                : hasTest
                    ? "WpfLearningWorkflow.DatasetDashboard.Metric.Replacement.Detail.NoLabels"
                    : "WpfLearningWorkflow.DatasetDashboard.Metric.Replacement.Detail.NoTest";
            object[] replacementDetailArguments = hasReplacementEvidence
                ? hasStrongReplacementEvidence
                    ? new object[] { finalVerificationCount }
                    : new object[] { finalVerificationCount, RecommendedModelReplacementTestImageCount }
                : Array.Empty<object>();
            string replacementStateKey = report?.IsReady == true
                ? hasReplacementEvidence
                    ? hasStrongReplacementEvidence
                        ? "WpfLearningWorkflow.DatasetDashboard.Metric.State.Complete"
                        : "WpfLearningWorkflow.DatasetDashboard.Metric.State.EvidenceInsufficient"
                    : "WpfLearningWorkflow.DatasetDashboard.Metric.State.FinalLabelsNeeded"
                : "WpfLearningWorkflow.DatasetDashboard.Metric.State.TrainFirst";
            string duplicateOverlapExample = FirstNonEmpty(
                statistics.TrainValidImageOverlapExample,
                statistics.SplitImageOverlapExample);

            var metrics = new List<WpfDatasetDashboardMetricItem>
            {
                DatasetDashboardLocalizationService.CreateMetric(
                    "WpfLearningWorkflow.DatasetDashboard.Metric.Images.Title",
                    statistics.TotalImageCount.ToString(),
                    "WpfLearningWorkflow.DatasetDashboard.Metric.Images.Detail",
                    hasImages
                        ? "WpfLearningWorkflow.DatasetDashboard.Metric.State.Complete"
                        : "WpfLearningWorkflow.DatasetDashboard.Metric.State.Needed",
                    PackIconMaterialKind.FolderImage,
                    isProblem: !hasImages,
                    isWarning: false,
                    actionKind: WpfDatasetDashboardActionKind.OpenImages,
                    statistics.TrainImageCount,
                    statistics.ValidImageCount,
                    statistics.TestImageCount),
                DatasetDashboardLocalizationService.CreateMetric(
                    "WpfLearningWorkflow.DatasetDashboard.Metric.Progress.Title",
                    $"{visibleCompletedImageLabelCount}/{statistics.TotalImageCount}",
                    "WpfLearningWorkflow.DatasetDashboard.Metric.Progress.Detail",
                    !hasImages
                        ? "WpfLearningWorkflow.DatasetDashboard.Metric.State.Waiting"
                        : isLabelingComplete
                            ? "WpfLearningWorkflow.DatasetDashboard.Metric.State.Complete"
                            : hasAnyCompletedImageLabel
                                ? "WpfLearningWorkflow.DatasetDashboard.Metric.State.Progress"
                                : "WpfLearningWorkflow.DatasetDashboard.Metric.State.Needed",
                    PackIconMaterialKind.ProgressClock,
                    isProblem: hasImages && !isLabelingComplete,
                    isWarning: false,
                    actionKind: WpfDatasetDashboardActionKind.OpenLabelingProgress,
                    completedImageLabelCount,
                    progressPercent),
                DatasetDashboardLocalizationService.CreateMetric(
                    "WpfLearningWorkflow.DatasetDashboard.Metric.Split.Title",
                    $"{statistics.TrainImageCount}/{statistics.ValidImageCount}/{statistics.TestImageCount}",
                    "WpfLearningWorkflow.DatasetDashboard.Metric.Split.Detail",
                    !hasTrain || !hasValid
                        ? "WpfLearningWorkflow.DatasetDashboard.Metric.State.Check"
                        : hasTest
                            ? "WpfLearningWorkflow.DatasetDashboard.Metric.State.Complete"
                            : "WpfLearningWorkflow.DatasetDashboard.Metric.State.NoTest",
                    PackIconMaterialKind.CheckAll,
                    isProblem: !hasTrain || !hasValid,
                    isWarning: hasTrain && hasValid && !hasTest,
                    actionKind: WpfDatasetDashboardActionKind.OpenDatasetSettings),
                DatasetDashboardLocalizationService.CreateMetricWithValue(
                    "WpfLearningWorkflow.DatasetDashboard.Metric.Replacement.Title",
                    replacementValueKey,
                    replacementValueFallback,
                    replacementDetailKey,
                    replacementStateKey,
                    hasStrongReplacementEvidence ? PackIconMaterialKind.CheckCircleOutline : PackIconMaterialKind.AlertCircleOutline,
                    isProblem: report?.IsReady != true,
                    isWarning: report?.IsReady == true && !hasStrongReplacementEvidence,
                    actionKind: WpfDatasetDashboardActionKind.OpenDatasetSettings,
                    replacementDetailArguments),
                DatasetDashboardLocalizationService.CreateMetric(
                    isAnomaly
                        ? "WpfLearningWorkflow.DatasetDashboard.Metric.Primary.Anomaly.Title"
                        : needsBoxLabels
                            ? "WpfLearningWorkflow.DatasetDashboard.Metric.Primary.Detection.Title"
                            : "WpfLearningWorkflow.DatasetDashboard.Metric.Primary.Segmentation.Title",
                    primaryLabelValue.ToString(),
                    isAnomaly
                        ? "WpfLearningWorkflow.DatasetDashboard.Metric.Primary.Anomaly.Detail"
                        : needsBoxLabels
                            ? "WpfLearningWorkflow.DatasetDashboard.Metric.Primary.Detection.Detail"
                            : "WpfLearningWorkflow.DatasetDashboard.Metric.Primary.Segmentation.Detail",
                    hasPrimaryLabels
                        ? "WpfLearningWorkflow.DatasetDashboard.Metric.State.Complete"
                        : "WpfLearningWorkflow.DatasetDashboard.Metric.State.Needed",
                    isAnomaly ? PackIconMaterialKind.CheckCircleOutline : needsBoxLabels ? PackIconMaterialKind.ShapeSquareRoundedPlus : PackIconMaterialKind.ViewListOutline,
                    isProblem: !hasPrimaryLabels,
                    isWarning: false,
                    actionKind: WpfDatasetDashboardActionKind.OpenLabelingTool,
                    isAnomaly
                        ? new object[] { statistics.AnomalyNormalImageCount, statistics.AnomalyAbnormalImageCount }
                        : needsBoxLabels
                            ? new object[] { statistics.TotalObjectCount, statistics.TotalLabelFileCount }
                            : new object[] { statistics.TotalSegmentationObjectCount, statistics.TotalMaskFileCount }),
                DatasetDashboardLocalizationService.CreateMetric(
                    isAnomaly
                        ? "WpfLearningWorkflow.DatasetDashboard.Metric.Artifacts.Anomaly.Title"
                        : needsBoxLabels
                            ? "WpfLearningWorkflow.DatasetDashboard.Metric.Artifacts.Detection.Title"
                            : "WpfLearningWorkflow.DatasetDashboard.Metric.Artifacts.Segmentation.Title",
                    artifactFileCount.ToString(),
                    isAnomaly
                        ? "WpfLearningWorkflow.DatasetDashboard.Metric.Artifacts.Anomaly.Detail"
                        : needsBoxLabels
                            ? "WpfLearningWorkflow.DatasetDashboard.Metric.Artifacts.Detection.Detail"
                            : "WpfLearningWorkflow.DatasetDashboard.Metric.Artifacts.Segmentation.Detail",
                    artifactFileCount > 0
                        ? isAnomaly
                            ? "WpfLearningWorkflow.DatasetDashboard.Metric.State.Complete"
                            : needsBoxLabels
                                ? "WpfLearningWorkflow.DatasetDashboard.Metric.State.Exists"
                                : "WpfLearningWorkflow.DatasetDashboard.Metric.State.Review"
                        : "WpfLearningWorkflow.DatasetDashboard.Metric.State.Needed",
                    PackIconMaterialKind.FileDocumentOutline,
                    isProblem: artifactFileCount == 0,
                    isWarning: false,
                    actionKind: isAnomaly || needsBoxLabels
                        ? WpfDatasetDashboardActionKind.CheckDataset
                        : WpfDatasetDashboardActionKind.ExportHistoricalSegmentationRemediationAudit,
                    isAnomaly
                        ? new object[] { statistics.TrainImageCount, statistics.ValidImageCount, statistics.TestImageCount }
                        : needsBoxLabels
                            ? new object[] { statistics.TrainLabelCount, statistics.ValidLabelCount, statistics.TestLabelCount }
                            : new object[] { statistics.TotalSegmentFileCount, statistics.TotalMaskFileCount }),
                DatasetDashboardLocalizationService.CreateMetric(
                    "WpfLearningWorkflow.DatasetDashboard.Metric.Class.Title",
                    classCount.ToString(),
                    "WpfLearningWorkflow.DatasetDashboard.Metric.Class.Detail",
                    classCount > 0
                        ? "WpfLearningWorkflow.DatasetDashboard.Metric.State.Complete"
                        : "WpfLearningWorkflow.DatasetDashboard.Metric.State.Needed",
                    PackIconMaterialKind.TagMultipleOutline,
                    isProblem: classCount == 0,
                    isWarning: false,
                    actionKind: WpfDatasetDashboardActionKind.OpenClassCatalog),
                DatasetDashboardLocalizationService.CreateMetric(
                    "WpfLearningWorkflow.DatasetDashboard.Metric.Duplicate.Title",
                    (statistics.TrainValidImageContentOverlapCount + statistics.SplitImageContentOverlapCount).ToString(),
                    string.IsNullOrWhiteSpace(duplicateOverlapExample)
                        ? "WpfLearningWorkflow.DatasetDashboard.Metric.Duplicate.Detail.NoExample"
                        : "WpfLearningWorkflow.DatasetDashboard.Metric.Duplicate.Detail.Example",
                    hasSplitOverlap
                        ? "WpfLearningWorkflow.DatasetDashboard.Metric.State.SeparateNeeded"
                        : "WpfLearningWorkflow.DatasetDashboard.Metric.State.None",
                    PackIconMaterialKind.AlertCircleOutline,
                    isProblem: hasSplitOverlap,
                    isWarning: false,
                    actionKind: WpfDatasetDashboardActionKind.OpenDatasetSettings,
                    string.IsNullOrWhiteSpace(duplicateOverlapExample)
                        ? Array.Empty<object>()
                        : new object[] { duplicateOverlapExample })
            };

            if (purpose == LabelingDatasetPurpose.AnomalyDetection)
            {
                metrics.Insert(Math.Min(2, metrics.Count), AnomalyDashboardPresentationService.BuildReviewStateMetric(anomalySummary));
            }

            if (qualityAudit != null)
            {
                metrics.Insert(Math.Min(purpose == LabelingDatasetPurpose.AnomalyDetection ? 3 : 2, metrics.Count), DatasetQualityAuditPresentationService.BuildQualityMetric(qualityAudit));
            }

            return metrics;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values?.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }
    }

    [Obsolete("Use DatasetDashboardPresentationService.", false)]
    public static class WpfDatasetDashboardPresentationService
    {
        public const int RecommendedModelReplacementTestImageCount = DatasetDashboardPresentationService.RecommendedModelReplacementTestImageCount;

        public static IReadOnlyList<WpfDatasetDashboardMetricItem> BuildMetrics(
            YoloDatasetReadinessReport report,
            YoloDatasetStatistics statistics,
            int classCount,
            AnomalyImageReviewSummary anomalySummary = null,
            YoloDatasetQualityAuditReport qualityAudit = null)
            => DatasetDashboardPresentationService.BuildMetrics(report, statistics, classCount, anomalySummary, qualityAudit);
    }
}
