using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        private LabelingDatasetPurpose GetCurrentDatasetPurpose()
        {
            EnsureProjectSettings();
            return LearningWorkflowViewModel?.GetSelectedDatasetPurpose()
                ?? global.Data.ProjectSettings.DatasetPurpose;
        }

        private bool IsSegmentationDatasetPurposeActive()
            => GetCurrentDatasetPurpose() == LabelingDatasetPurpose.Segmentation;

        private int GetVisibleManualSegmentCount()
            => IsSegmentationDatasetPurposeActive() ? manualSegments.Count : 0;

        private IReadOnlyList<LabelingSegmentationObject> GetVisibleManualSegments()
            => IsSegmentationDatasetPurposeActive()
                ? manualSegments
                : Array.Empty<LabelingSegmentationObject>();

        private void RefreshAnnotationVisibilityForDatasetPurpose(bool notifyOperator = false)
        {
            LabelingDatasetPurpose currentPurpose = GetCurrentDatasetPurpose();
            int segmentCount = manualSegments.Count;
            bool isSegmentationPurpose = currentPurpose == LabelingDatasetPurpose.Segmentation;
            if (!isSegmentationPurpose)
            {
                // Dataset purpose switches should hide segmentation artifacts without deleting them.
                // This prevents stale masks/polygons from leaking into box labeling while preserving undo/data if the operator switches back.
                polygonAnnotationService.Reset();
                MainCanvasViewModel?.ClearBrushCursorPreview();
                MainCanvasViewModel?.ClearMaskStrokePreview(refresh: false);
            }

            RefreshPolygonOverlays();
            RefreshObjectList();
            if (notifyOperator)
            {
                ReportAnnotationVisibilityForDatasetPurpose(currentPurpose, segmentCount);
            }
        }

        private void ReportAnnotationVisibilityForDatasetPurpose(LabelingDatasetPurpose purpose, int segmentCount)
        {
            string text = BuildAnnotationVisibilityStatusText(purpose, segmentCount);
            SetModelStatus(text);
            AppendLog(text);
            // Tool selection can fire once more after the dataset-purpose list click.
            // Re-apply this short guidance at idle so the operator sees why masks vanished or returned.
            Dispatcher?.BeginInvoke(
                new Action(() => SetModelStatus(text)),
                System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            var timer = new System.Windows.Threading.DispatcherTimer(
                System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(220)
            };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                SetModelStatus(text);
            };
            timer.Start();
        }

        private static string BuildAnnotationVisibilityStatusText(LabelingDatasetPurpose purpose, int segmentCount)
        {
            string purposeName = FormatDatasetPurposeName(purpose);
            if (purpose == LabelingDatasetPurpose.Segmentation)
            {
                return segmentCount > 0
                    ? $"데이터셋 목적: {purposeName} / 세그 라벨 {segmentCount}개 표시"
                    : $"데이터셋 목적: {purposeName} / 세그 라벨 작성 가능";
            }

            return segmentCount > 0
                ? $"데이터셋 목적: {purposeName} / 세그 라벨 {segmentCount}개 숨김"
                : $"데이터셋 목적: {purposeName}";
        }

        private static string FormatDatasetPurposeName(LabelingDatasetPurpose purpose)
        {
            return purpose switch
            {
                LabelingDatasetPurpose.Segmentation => "세그멘테이션",
                LabelingDatasetPurpose.AnomalyDetection => "이상 탐지",
                _ => "객체 탐지"
            };
        }

        private void EnsureSegmentationDatasetPurposeForSegmentationTool()
        {
            if (IsSegmentationDatasetPurposeActive())
            {
                return;
            }

            ApplyDatasetPurposeToCurrentProject(LabelingDatasetPurpose.Segmentation);
            RefreshCanvasAnnotationToolScope();
        }
    }
}
