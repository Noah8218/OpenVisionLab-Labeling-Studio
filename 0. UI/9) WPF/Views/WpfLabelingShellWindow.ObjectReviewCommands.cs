using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using MvcVisionSystem.Yolo;
using OpenVisionLab.Mvvm;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Object commands mutate one selected object at a time; incremental delete avoids forcing a full canvas/list rebuild.
        private void ExecuteObjectSelectionChangedCommand(object selectedItem)
        {
            if (ObjectReviewViewModel?.IsSelectionNotificationSuppressed == true)
            {
                UpdateObjectReviewActionState();
                return;
            }

            SyncObjectClassEditorToSelection();
            UpdateObjectReviewActionState();
            bool isManualSegmentSelected = ObjectReviewViewModel?.IsSelectedSource(WpfObjectReviewSource.ManualSegment) == true;
            if (activeAnnotationTool == WpfAnnotationTool.Select)
            {
                MainCanvasViewModel.IsImagePointInputMode = isManualSegmentSelected;
            }

            if (ObjectReviewViewModel?.IsSelectedSource(WpfObjectReviewSource.ManualRoi) != true)
            {
                MainCanvasViewModel.ClearRoiSelection();
            }

            RefreshPolygonOverlays();
        }

        private void ExecuteApplyObjectClassCommand()
        {
            if (!TryGetSelectedObjectReviewItem(out WpfObjectReviewItemRef item))
            {
                return;
            }

            string className = WpfObjectReviewEditService.NormalizeClassName(ObjectReviewViewModel?.SelectedClassName);
            CClassItem classItem = EnsureClassItem(className);
            WpfAnnotationHistorySnapshot beforeChange = CaptureAnnotationHistory("Change object class");
            if (!WpfObjectReviewEditService.TryApplyClass(
                item,
                manualRois,
                manualRoiClassNames,
                manualSegments,
                candidateReviewState.MutableConfirmedCandidates,
                className,
                out string appliedClassName,
                classItem))
            {
                return;
            }

            PushAnnotationHistorySnapshot(beforeChange);
            if (item.Source == WpfObjectReviewSource.ManualRoi)
            {
                ApplyManualRoiOverlayColor(item.Index, refreshImmediately: true);
            }
            else if (item.Source == WpfObjectReviewSource.ConfirmedAi)
            {
                RedrawReviewRois();
            }
            else
            {
                MainCanvasViewModel?.ClearMaskStrokePreview(refresh: false, clearTexture: true);
                RefreshPolygonOverlays();
            }

            RefreshObjectList();
            MarkAnnotationsDirty($"\uAC1D\uCCB4 \uD074\uB798\uC2A4 \uBCC0\uACBD: {appliedClassName}");

            AppendLog($"Changed object class: {appliedClassName}");
        }

        private void ExecuteDeleteObjectCommand()
        {
            DeleteSelectedObject();
        }

        private void ExecuteObjectPreviewKeyDownCommand(KeyInputCommandArgs e)
        {
            if (e == null || (e.Key != Key.Delete && e.Key != Key.Back))
            {
                return;
            }

            e.Handled = DeleteSelectedObject();
        }

        private bool DeleteSelectedObject()
        {
            if (!TryGetSelectedObjectReviewItem(out WpfObjectReviewItemRef item))
            {
                return false;
            }

            int selectedObjectRowIndex = GetSelectedObjectReviewRowIndex();
            string manualOverlayId = item.Source == WpfObjectReviewSource.ManualRoi
                ? GetManualRoiOverlayId(item.Index)
                : string.Empty;
            string removedText = ObjectReviewViewModel?.SelectedObject?.DisplayText
                ?? "object";
            WpfAnnotationHistorySnapshot beforeChange = item.Source == WpfObjectReviewSource.ManualRoi
                ? CaptureManualRoiHistory("\uB77C\uBCA8 \uC0AD\uC81C")
                : CaptureAnnotationHistory("\uB77C\uBCA8 \uC0AD\uC81C");
            if (!WpfObjectReviewEditService.TryDelete(
                item,
                manualRois,
                manualRoiClassNames,
                manualSegments,
                candidateReviewState.MutableConfirmedCandidates))
            {
                UpdateObjectReviewActionState();
                return false;
            }

            PushAnnotationHistorySnapshot(beforeChange);
            if (item.Source == WpfObjectReviewSource.ManualRoi)
            {
                RemoveAtIfPresent(manualRoiShapeKinds, item.Index);
                RemoveAtIfPresent(manualRoiOverlayIds, item.Index);
                if (!RemoveCanvasRoiOverlayById(manualOverlayId))
                {
                    RedrawReviewRois();
                }

                ClearCanvasRoiSelectionAfterDelete(manualOverlayId);
            }
            else if (item.Source == WpfObjectReviewSource.ManualSegment)
            {
                RefreshPolygonOverlays();
            }
            else
            {
                RedrawReviewRois();
            }

            RefreshObjectReviewAfterDelete(item.Source, selectedObjectRowIndex);
            MarkAnnotationsDirty($"\uB77C\uBCA8 \uC0AD\uC81C: {removedText}");
            QueueActiveImageQueueStatusRefresh(hasActiveCandidates: pendingDetectionCandidates.Count > 0);
            AppendLog($"Removed object from review: {removedText}");
            return true;
        }

        private bool TryGetSelectedObjectReviewItem(out WpfObjectReviewItemRef item)
        {
            if (ObjectReviewViewModel == null)
            {
                item = null;
                return false;
            }

            return ObjectReviewViewModel.TryResolveSelectedItem(
                manualRoiOverlayIds,
                manualRois.Count,
                out item);
        }

        private int GetSelectedObjectReviewRowIndex()
            => ObjectReviewViewModel?.GetSelectedRowIndex() ?? -1;

        private bool RemoveCanvasRoiOverlayById(string overlayId)
        {
            if (string.IsNullOrWhiteSpace(overlayId) || MainCanvasViewModel?.ImageViewer == null)
            {
                return false;
            }

            var overlayItem = MainCanvasViewModel.ImageViewer.GetCanvasOverlayManager().GetOverlayByUniqueId(overlayId);
            string groupName = overlayItem?.Parent?.GroupType
                ?? overlayItem?.Shape?.GroupType
                ?? MainCanvasViewModel.ImageViewer.GetCanvasOverlayManager().LastGroupType
                ?? string.Empty;
            OpenVisionLab.ImageCanvas.OpenGLRendering.OpenGlOverlayExtensions.DeleteOverlay(
                MainCanvasViewModel.ImageViewer,
                overlayId,
                groupName,
                refreshImmediately: false);
            return overlayItem != null;
        }

        private void ClearCanvasRoiSelectionAfterDelete(string overlayId)
        {
            if (MainCanvasViewModel == null)
            {
                return;
            }

            if (!MainCanvasViewModel.ClearDeletedRoiSelection(overlayId, refreshImmediately: false))
            {
                MainCanvasViewModel.ClearRoiSelection(refreshImmediately: false);
            }
        }

        private void RefreshObjectReviewAfterDelete(WpfObjectReviewSource deletedSource, int deletedObjectRowIndex)
        {
            int objectCount = manualRois.Count + GetVisibleManualSegmentCount() + confirmedDetectionCandidates.Count;
            WpfObjectReviewDeleteRefreshPlan plan = objectReviewPresentationService.BuildDeleteRefreshPlan(
                deletedSource,
                objectCount,
                ObjectReviewFullRefreshDeleteLimit,
                deletedObjectRowIndex,
                ObjectReviewViewModel?.Objects?.Count ?? 0);
            if (!plan.UseIncremental)
            {
                RefreshObjectList();
                return;
            }

            using (ObjectReviewViewModel.SuppressSelectionNotifications())
            {
                if (!ObjectReviewViewModel.TryRemoveObject(
                    deletedObjectRowIndex,
                    plan.Summary,
                    plan.SelectedRowIndex))
                {
                    RefreshObjectList();
                    return;
                }
            }

            SyncObjectClassEditorToSelection();
            UpdateObjectReviewActionState();
        }
    }
}
