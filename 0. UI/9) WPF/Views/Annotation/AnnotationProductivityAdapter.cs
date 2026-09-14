using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the selected manual annotation duplication workflow.
    /// Geometry and cloning stay in existing services; the Shell supplies live state and presentation callbacks.
    /// </summary>
    internal sealed class AnnotationProductivityAdapter
    {
        private readonly AnnotationProductivityAdapterContext context;

        internal AnnotationProductivityAdapter(AnnotationProductivityAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        internal bool TryDuplicateSelectedAnnotation()
        {
            context.CompleteMaskAnnotationStroke?.Invoke();
            context.FlushQueuedMaskStrokeCommits?.Invoke();
            WpfObjectReviewItemRef selectedItem = context.SelectedObjectReviewItemProvider?.Invoke();
            if (selectedItem == null)
            {
                context.SetYoloCommandStatus?.Invoke("복제할 저장 라벨을 먼저 선택하세요.", false);
                return false;
            }

            return selectedItem.Source switch
            {
                WpfObjectReviewSource.ManualRoi => TryDuplicateManualRoi(selectedItem.Index),
                WpfObjectReviewSource.ManualSegment => TryDuplicateManualSegment(selectedItem.Index),
                _ => RejectUnsupportedSelection()
            };
        }

        internal bool TryDuplicateManualRoi(int sourceIndex)
        {
            if (context.ManualRois == null || sourceIndex < 0 || sourceIndex >= context.ManualRois.Count)
            {
                return false;
            }

            if (!TryValidateMutation(WpfObjectReviewItemRef.Manual(sourceIndex)))
            {
                return false;
            }

            if (ObjectReviewPresentationService.GetManualRoiShapeKind(context.ManualRoiShapeKinds, sourceIndex)
                != CanvasRoiShapeKind.Rectangle)
            {
                context.SetYoloCommandStatus?.Invoke("현재 P0-A 복제는 박스, 폴리곤, 브러시 마스크만 지원합니다.", false);
                return false;
            }

            context.RegisterAnnotationHistoryBeforeChange?.Invoke("라벨 복제");
            Rectangle duplicate = AnnotationProductivityService.CreateOffsetRectangle(
                context.ManualRois[sourceIndex],
                context.ActiveImageSizeProvider?.Invoke() ?? Size.Empty);
            string className = ObjectReviewPresentationService.GetManualRoiClassName(
                context.ManualRoiClassNames,
                sourceIndex);
            context.ManualRois.Add(duplicate);
            context.ManualRoiClassNames?.Add(className);
            context.ManualRoiShapeKinds?.Add(ObjectReviewPresentationService.GetManualRoiShapeKind(
                context.ManualRoiShapeKinds,
                sourceIndex));
            context.ManualRoiOverlayIds?.Add(string.Empty);

            int duplicateIndex = context.ManualRois.Count - 1;
            context.RedrawReviewRois?.Invoke();
            context.RefreshObjectListWithSelection?.Invoke(WpfObjectReviewItemRef.Manual(duplicateIndex));
            context.ShowSavedLabelsWorkflowView?.Invoke();
            context.QueueActiveImageQueueStatusRefresh?.Invoke(context.PendingCandidateCountProvider?.Invoke() > 0);
            context.SetYoloCommandStatus?.Invoke(
                $"라벨 복제: {className} / x={duplicate.X}, y={duplicate.Y}, w={duplicate.Width}, h={duplicate.Height}",
                false);
            context.AppendLog?.Invoke($"Duplicated manual ROI: source={sourceIndex + 1}, target={duplicateIndex + 1}, class={className}");
            return true;
        }

        internal bool TryDuplicateManualSegment(int sourceIndex)
        {
            if (context.ManualSegments == null || sourceIndex < 0 || sourceIndex >= context.ManualSegments.Count)
            {
                return false;
            }

            if (!TryValidateMutation(WpfObjectReviewItemRef.ManualSegment(sourceIndex)))
            {
                return false;
            }

            LabelingSegmentationObject duplicate = AnnotationProductivityService.CreateOffsetSegment(
                context.ManualSegments[sourceIndex],
                context.ActiveImageSizeProvider?.Invoke() ?? Size.Empty,
                context.MaskAnnotationService);
            if (duplicate == null)
            {
                return false;
            }

            context.RegisterAnnotationHistoryBeforeChange?.Invoke("라벨 복제");
            duplicate.ZOrder = SegmentationZOrderService.GetNextZOrder(context.ManualSegments);
            context.ManualSegments.Add(duplicate);
            int duplicateIndex = context.ManualSegments.Count - 1;
            context.RefreshObjectListWithSelection?.Invoke(WpfObjectReviewItemRef.ManualSegment(duplicateIndex));
            context.RefreshPolygonOverlays?.Invoke();
            context.ShowSavedLabelsWorkflowView?.Invoke();
            context.QueueActiveImageQueueStatusRefresh?.Invoke(context.PendingCandidateCountProvider?.Invoke() > 0);

            string shapeName = duplicate.IsRasterMask ? "마스크" : "폴리곤";
            string className = FirstNonEmpty(duplicate.ClassName, duplicate.ClassItem?.Text, "Defect");
            context.SetYoloCommandStatus?.Invoke($"라벨 복제: {shapeName} / {className}", false);
            context.AppendLog?.Invoke($"Duplicated manual segment: source={sourceIndex + 1}, target={duplicateIndex + 1}, shape={shapeName}, class={className}");
            return true;
        }

        private bool TryValidateMutation(WpfObjectReviewItemRef item)
        {
            string error = context.MutationErrorProvider?.Invoke(item, true) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(error))
            {
                return true;
            }

            context.SetYoloCommandStatus?.Invoke(error, false);
            return false;
        }

        private bool RejectUnsupportedSelection()
        {
            context.SetYoloCommandStatus?.Invoke("수동 박스, 폴리곤, 브러시 마스크만 복제할 수 있습니다.", false);
            return false;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }
    }

    internal sealed class AnnotationProductivityAdapterContext
    {
        internal Func<WpfObjectReviewItemRef> SelectedObjectReviewItemProvider { get; init; }
        internal Action CompleteMaskAnnotationStroke { get; init; }
        internal Action FlushQueuedMaskStrokeCommits { get; init; }
        internal List<Rectangle> ManualRois { get; init; }
        internal List<string> ManualRoiClassNames { get; init; }
        internal List<CanvasRoiShapeKind> ManualRoiShapeKinds { get; init; }
        internal List<string> ManualRoiOverlayIds { get; init; }
        internal List<LabelingSegmentationObject> ManualSegments { get; init; }
        internal Func<Size> ActiveImageSizeProvider { get; init; }
        internal MaskAnnotationService MaskAnnotationService { get; init; }
        internal Func<WpfObjectReviewItemRef, bool, string> MutationErrorProvider { get; init; }
        internal Action<string> RegisterAnnotationHistoryBeforeChange { get; init; }
        internal Action RedrawReviewRois { get; init; }
        internal Action<WpfObjectReviewItemRef> RefreshObjectListWithSelection { get; init; }
        internal Action RefreshPolygonOverlays { get; init; }
        internal Action ShowSavedLabelsWorkflowView { get; init; }
        internal Func<int> PendingCandidateCountProvider { get; init; }
        internal Action<bool> QueueActiveImageQueueStatusRefresh { get; init; }
        internal Action<string, bool> SetYoloCommandStatus { get; init; }
        internal Action<string> AppendLog { get; init; }
    }
}
