using System;
using System.Collections.Generic;

namespace MvcVisionSystem
{
    /// <summary>
    /// Applies dataset-purpose visibility rules while the Shell owns live annotation collections.
    /// Deferred status work is scheduled through explicit UI callbacks and stops after close approval.
    /// </summary>
    internal sealed class AnnotationVisibilityAdapter
    {
        private readonly AnnotationVisibilityAdapterContext context;
        private string pendingStatusText = string.Empty;

        internal AnnotationVisibilityAdapter(AnnotationVisibilityAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        internal bool IsSegmentationDatasetPurposeActive()
            => GetCurrentDatasetPurpose() == LabelingDatasetPurpose.Segmentation;

        internal int GetVisibleManualSegmentCount()
            => IsSegmentationDatasetPurposeActive() ? context.ManualSegments?.Count ?? 0 : 0;

        internal IReadOnlyList<LabelingSegmentationObject> GetVisibleManualSegments()
        {
            if (!IsSegmentationDatasetPurposeActive() || context.ManualSegments == null)
            {
                return Array.Empty<LabelingSegmentationObject>();
            }

            return context.ManualSegments;
        }

        internal void RefreshAnnotationVisibilityForDatasetPurpose(bool notifyOperator = false)
        {
            LabelingDatasetPurpose currentPurpose = GetCurrentDatasetPurpose();
            int segmentCount = context.ManualSegments?.Count ?? 0;
            if (currentPurpose != LabelingDatasetPurpose.Segmentation)
            {
                // Purpose switches hide segmentation artifacts without deleting the canonical data.
                context.ResetPolygonAnnotation?.Invoke();
                context.ClearBrushCursorPreview?.Invoke();
                context.ClearMaskStrokePreview?.Invoke();
            }

            context.RefreshPolygonOverlays?.Invoke();
            context.RefreshObjectList?.Invoke();
            if (notifyOperator)
            {
                ReportAnnotationVisibilityForDatasetPurpose(currentPurpose, segmentCount);
            }
        }

        internal void ReportAnnotationVisibilityForDatasetPurpose(LabelingDatasetPurpose purpose, int segmentCount)
        {
            if (context.IsApplicationCloseApproved?.Invoke() == true)
            {
                return;
            }

            string text = DatasetContextPresentationService.BuildAnnotationVisibilityStatusText(purpose, segmentCount);
            ApplyAnnotationVisibilityStatus(text);
            ScheduleAnnotationVisibilityStatusRefresh(text);
        }

        internal void ApplyAnnotationVisibilityStatus(string text)
        {
            context.SetModelStatus?.Invoke(text);
            context.AppendLog?.Invoke(text);
            pendingStatusText = text ?? string.Empty;
        }

        internal void ScheduleAnnotationVisibilityStatusRefresh(string text)
        {
            context.ScheduleAtApplicationIdle?.Invoke(() => ApplyScheduledAnnotationVisibilityStatus(text));
            context.StopAnnotationVisibilityRefreshTimer?.Invoke();
            context.StartAnnotationVisibilityRefreshTimer?.Invoke();
        }

        internal void ApplyScheduledAnnotationVisibilityStatus(string text)
        {
            if (context.IsApplicationCloseApproved?.Invoke() == true)
            {
                return;
            }

            context.SetModelStatus?.Invoke(text);
        }

        internal void HandleAnnotationVisibilityRefreshTimerTick()
        {
            context.StopAnnotationVisibilityRefreshTimer?.Invoke();
            if (context.IsApplicationCloseApproved?.Invoke() == true)
            {
                return;
            }

            context.SetModelStatus?.Invoke(pendingStatusText);
        }

        internal void EnsureSegmentationDatasetPurposeForSegmentationTool()
        {
            if (IsSegmentationDatasetPurposeActive())
            {
                return;
            }

            context.ApplyDatasetPurposeToCurrentProject?.Invoke(LabelingDatasetPurpose.Segmentation);
            context.RefreshCanvasAnnotationToolScope?.Invoke();
        }

        private LabelingDatasetPurpose GetCurrentDatasetPurpose()
            => context.CurrentDatasetPurposeProvider?.Invoke() ?? LabelingDatasetPurpose.ObjectDetection;
    }

    internal sealed class AnnotationVisibilityAdapterContext
    {
        internal List<LabelingSegmentationObject> ManualSegments { get; init; }
        internal Func<LabelingDatasetPurpose> CurrentDatasetPurposeProvider { get; init; }
        internal Action ResetPolygonAnnotation { get; init; }
        internal Action ClearBrushCursorPreview { get; init; }
        internal Action ClearMaskStrokePreview { get; init; }
        internal Action RefreshPolygonOverlays { get; init; }
        internal Action RefreshObjectList { get; init; }
        internal Func<bool> IsApplicationCloseApproved { get; init; }
        internal Action<string> SetModelStatus { get; init; }
        internal Action<string> AppendLog { get; init; }
        internal Action<Action> ScheduleAtApplicationIdle { get; init; }
        internal Action StopAnnotationVisibilityRefreshTimer { get; init; }
        internal Action StartAnnotationVisibilityRefreshTimer { get; init; }
        internal Action<LabelingDatasetPurpose> ApplyDatasetPurposeToCurrentProject { get; init; }
        internal Action RefreshCanvasAnnotationToolScope { get; init; }
    }
}
