using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the command workflow for reordering selected manual segmentation objects.
    /// The existing z-order service plans the order; the Shell supplies live collections and presentation callbacks.
    /// </summary>
    internal sealed class SegmentationZOrderCommandAdapter
    {
        private readonly SegmentationZOrderService service;
        private readonly SegmentationZOrderCommandAdapterContext context;

        internal SegmentationZOrderCommandAdapter(
            SegmentationZOrderService service,
            SegmentationZOrderCommandAdapterContext context)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        internal void MoveSelectedSegmentationZOrder(WpfSegmentationZOrderMove move)
        {
            context.CompleteMaskAnnotationStroke?.Invoke();
            context.FlushQueuedMaskStrokeCommits?.Invoke();
            string error = string.Empty;
            SegmentationZOrderResult result = null;
            WpfObjectReviewItemRef selected = context.SelectedObjectReviewItemProvider?.Invoke();
            if (selected == null
                || selected.Source != WpfObjectReviewSource.ManualSegment)
            {
                error = "순서를 변경할 세그먼트를 하나 선택하세요.";
            }
            else if (context.ManualSegments == null)
            {
                error = "세그먼트 목록을 사용할 수 없습니다.";
            }
            else
            {
                service.TryPlanMove(
                    context.ManualSegments,
                    selected.Index,
                    move,
                    out result,
                    out error);
            }

            if (result == null)
            {
                context.SetYoloCommandStatus?.Invoke(error, false);
                if (!string.IsNullOrWhiteSpace(error))
                {
                    context.AppendLog?.Invoke($"Segment z-order skipped: {error}");
                }

                context.RefreshObjectReviewActionState?.Invoke();
                return;
            }

            string stateError = context.MutationErrorProvider?.Invoke(selected, true) ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(stateError))
            {
                context.SetYoloCommandStatus?.Invoke(stateError, false);
                context.AppendLog?.Invoke(stateError);
                return;
            }

            WpfAnnotationHistorySnapshot beforeChange = context.CaptureAnnotationHistory?.Invoke("세그먼트 표시 순서 변경");
            var previousZOrders = context.ManualSegments
                .Select(segment => (Segment: segment, ZOrder: segment.ZOrder))
                .ToList();
            context.ManualSegments.Clear();
            context.ManualSegments.AddRange(result.OrderedSegments);
            for (int index = 0; index < context.ManualSegments.Count; index++)
            {
                LabelingSegmentationObject segment = context.ManualSegments[index];
                if (segment == null)
                {
                    continue;
                }

                int previousZOrder = previousZOrders
                    .First(item => ReferenceEquals(item.Segment, segment))
                    .ZOrder;
                if (previousZOrder != index || index == result.SelectedIndex)
                {
                    segment.LastStructuralOperation = SegmentationZOrderService.StructuralOperationName;
                }

                segment.ZOrder = index;
            }

            context.PushAnnotationHistorySnapshot?.Invoke(beforeChange);
            context.ClearMaskStrokePreview?.Invoke();
            context.RefreshPolygonOverlays?.Invoke();
            context.RefreshObjectListWithSelection?.Invoke(WpfObjectReviewItemRef.ManualSegment(result.SelectedIndex));
            context.QueueActiveImageQueueStatusRefresh?.Invoke(context.PendingCandidateCountProvider?.Invoke() > 0);

            string action = FormatSegmentationZOrderMove(result.Move);
            string status = FormattableString.Invariant(
                $"{action}: {result.SelectedIndex + 1}/{context.ManualSegments.Count} (숫자가 클수록 앞)");
            context.SetYoloCommandStatus?.Invoke(status, false);
            context.AppendLog?.Invoke(status);
        }

        private static string FormatSegmentationZOrderMove(WpfSegmentationZOrderMove move)
            => move switch
            {
                WpfSegmentationZOrderMove.SendToBack => "맨 뒤로",
                WpfSegmentationZOrderMove.SendBackward => "한 칸 뒤로",
                WpfSegmentationZOrderMove.BringForward => "한 칸 앞으로",
                WpfSegmentationZOrderMove.BringToFront => "맨 앞으로",
                _ => "표시 순서 변경"
            };
    }

    internal sealed class SegmentationZOrderCommandAdapterContext
    {
        internal Func<WpfObjectReviewItemRef> SelectedObjectReviewItemProvider { get; init; }
        internal List<LabelingSegmentationObject> ManualSegments { get; init; }
        internal Action CompleteMaskAnnotationStroke { get; init; }
        internal Action FlushQueuedMaskStrokeCommits { get; init; }
        internal Func<WpfObjectReviewItemRef, bool, string> MutationErrorProvider { get; init; }
        internal Func<string, WpfAnnotationHistorySnapshot> CaptureAnnotationHistory { get; init; }
        internal Action<WpfAnnotationHistorySnapshot> PushAnnotationHistorySnapshot { get; init; }
        internal Action ClearMaskStrokePreview { get; init; }
        internal Action RefreshPolygonOverlays { get; init; }
        internal Action<WpfObjectReviewItemRef> RefreshObjectListWithSelection { get; init; }
        internal Func<int> PendingCandidateCountProvider { get; init; }
        internal Action<bool> QueueActiveImageQueueStatusRefresh { get; init; }
        internal Action RefreshObjectReviewActionState { get; init; }
        internal Action<string, bool> SetYoloCommandStatus { get; init; }
        internal Action<string> AppendLog { get; init; }
    }
}
