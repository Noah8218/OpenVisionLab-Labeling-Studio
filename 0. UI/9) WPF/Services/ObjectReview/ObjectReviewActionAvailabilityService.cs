using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem
{
    /// <summary>
    /// Computes Object Review command admission from a selected row and the
    /// current edit state. The ViewModel owns WPF properties; the Shell and
    /// workflow services continue to own command execution and mutations.
    /// </summary>
    public sealed class ObjectReviewActionAvailabilityService
    {
        private const string DefaultZOrderStatusText = "선택한 세그먼트의 앞뒤 표시 순서를 변경합니다.";

        public ObjectReviewActionAvailabilitySnapshot Build(
            WpfObjectReviewListItem selectedObject,
            IReadOnlyList<WpfObjectReviewListItem> objects,
            string selectedClassName,
            bool isSplitPending,
            bool isHoleEditPending,
            bool isVertexEditPending,
            bool isIntelligentScissorsPending,
            bool isRemoveUnderlyingPreviewPending,
            bool refreshSegmentCollectionState)
        {
            bool hasSelectedObject = selectedObject?.IsEnabled == true;
            bool selectedLocked = selectedObject?.IsLocked == true;
            bool selectedHidden = selectedObject?.IsHidden == true;
            bool isSegmentContextVisible = selectedObject?.IsManualSegment == true;
            bool isPolygonVertexContextVisible = selectedObject?.IsManualPolygon == true;
            bool pendingEdit = isSplitPending
                || isHoleEditPending
                || isVertexEditPending
                || isIntelligentScissorsPending
                || isRemoveUnderlyingPreviewPending;

            bool canEditSelectedObject = hasSelectedObject && !selectedLocked && !selectedHidden;
            bool canApplyClass = hasSelectedObject
                && !selectedLocked
                && !string.IsNullOrWhiteSpace(selectedClassName);
            bool canEditSegment = isSegmentContextVisible && canEditSelectedObject && !pendingEdit;
            bool canEditPolygon = isPolygonVertexContextVisible && canEditSelectedObject && !pendingEdit;

            int manualSegmentCount = objects == null
                ? 0
                : objects.Count(item => item?.IsManualSegment == true);
            int selectedSegmentIndex = isSegmentContextVisible ? selectedObject.SourceIndex : -1;
            bool canChangeZOrder = selectedSegmentIndex >= 0
                && selectedSegmentIndex < manualSegmentCount
                && canEditSelectedObject
                && !pendingEdit;
            bool showSegmentCollectionState = refreshSegmentCollectionState || isSegmentContextVisible;

            bool isSendToBackEnabled = false;
            bool isSendBackwardEnabled = false;
            bool isBringForwardEnabled = false;
            bool isBringToFrontEnabled = false;
            bool isRemoveUnderlyingPreviewEnabled = false;
            string zOrderStatusText = DefaultZOrderStatusText;
            if (showSegmentCollectionState)
            {
                if (canChangeZOrder)
                {
                    isSendToBackEnabled = selectedSegmentIndex > 0;
                    isSendBackwardEnabled = selectedSegmentIndex > 0;
                    isBringForwardEnabled = selectedSegmentIndex < manualSegmentCount - 1;
                    isBringToFrontEnabled = selectedSegmentIndex < manualSegmentCount - 1;
                    zOrderStatusText = FormattableString.Invariant(
                        $"표시 순서 {selectedSegmentIndex + 1}/{manualSegmentCount} · 숫자가 클수록 앞");
                }

                isRemoveUnderlyingPreviewEnabled = selectedSegmentIndex >= 0
                    && manualSegmentCount >= 2
                    && canEditSelectedObject
                    && !pendingEdit;
            }

            int mergeSelectionCount = refreshSegmentCollectionState && objects != null
                ? objects.Count(item => item?.IsManualSegment == true
                    && item.IsMergeSelected
                    && !item.IsHidden
                    && !item.IsLocked)
                : 0;

            return new ObjectReviewActionAvailabilitySnapshot(
                isSegmentContextVisible,
                isPolygonVertexContextVisible,
                hasSelectedObject && !selectedLocked,
                canApplyClass,
                canEditSegment,
                canEditSegment,
                canEditPolygon,
                canEditPolygon,
                isSendToBackEnabled,
                isSendBackwardEnabled,
                isBringForwardEnabled,
                isBringToFrontEnabled,
                zOrderStatusText,
                isRemoveUnderlyingPreviewEnabled,
                mergeSelectionCount >= 2
                    && !isVertexEditPending
                    && !isIntelligentScissorsPending
                    && !isRemoveUnderlyingPreviewPending,
                FormattableString.Invariant($"병합 선택 {mergeSelectionCount}개 · 같은 클래스 2개 이상"));
        }
    }

    public sealed class ObjectReviewActionAvailabilitySnapshot
    {
        public ObjectReviewActionAvailabilitySnapshot(
            bool isSegmentContextVisible,
            bool isPolygonVertexContextVisible,
            bool isDeleteEnabled,
            bool isApplyClassEnabled,
            bool isSplitEnabled,
            bool isHoleEditEnabled,
            bool isVertexEditEnabled,
            bool isIntelligentScissorsEnabled,
            bool isSendToBackEnabled,
            bool isSendBackwardEnabled,
            bool isBringForwardEnabled,
            bool isBringToFrontEnabled,
            string zOrderStatusText,
            bool isRemoveUnderlyingPreviewEnabled,
            bool isMergeSelectedSegmentsEnabled,
            string mergeSelectionText)
        {
            IsSegmentContextVisible = isSegmentContextVisible;
            IsPolygonVertexContextVisible = isPolygonVertexContextVisible;
            IsDeleteEnabled = isDeleteEnabled;
            IsApplyClassEnabled = isApplyClassEnabled;
            IsSplitEnabled = isSplitEnabled;
            IsHoleEditEnabled = isHoleEditEnabled;
            IsVertexEditEnabled = isVertexEditEnabled;
            IsIntelligentScissorsEnabled = isIntelligentScissorsEnabled;
            IsSendToBackEnabled = isSendToBackEnabled;
            IsSendBackwardEnabled = isSendBackwardEnabled;
            IsBringForwardEnabled = isBringForwardEnabled;
            IsBringToFrontEnabled = isBringToFrontEnabled;
            ZOrderStatusText = zOrderStatusText ?? string.Empty;
            IsRemoveUnderlyingPreviewEnabled = isRemoveUnderlyingPreviewEnabled;
            IsMergeSelectedSegmentsEnabled = isMergeSelectedSegmentsEnabled;
            MergeSelectionText = mergeSelectionText ?? string.Empty;
        }

        public bool IsSegmentContextVisible { get; }

        public bool IsPolygonVertexContextVisible { get; }

        public bool IsDeleteEnabled { get; }

        public bool IsApplyClassEnabled { get; }

        public bool IsSplitEnabled { get; }

        public bool IsHoleEditEnabled { get; }

        public bool IsVertexEditEnabled { get; }

        public bool IsIntelligentScissorsEnabled { get; }

        public bool IsSendToBackEnabled { get; }

        public bool IsSendBackwardEnabled { get; }

        public bool IsBringForwardEnabled { get; }

        public bool IsBringToFrontEnabled { get; }

        public string ZOrderStatusText { get; }

        public bool IsRemoveUnderlyingPreviewEnabled { get; }

        public bool IsMergeSelectedSegmentsEnabled { get; }

        public string MergeSelectionText { get; }
    }
}
