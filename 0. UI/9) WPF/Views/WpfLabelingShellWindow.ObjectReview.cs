using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using OpenVisionLab.Mvvm;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        private void RefreshObjectList()
        {
            RefreshObjectListViewModel(null);
        }

        private void RefreshObjectListWithSelection(WpfObjectReviewItemRef preferredSelection)
        {
            RefreshObjectListViewModel(preferredSelection);
        }

        private void RefreshObjectListViewModel(WpfObjectReviewItemRef preferredSelection)
        {
            WpfObjectReviewItemRef previousSelection = null;
            TryGetSelectedObjectReviewItem(out previousSelection);

            WpfObjectReviewListPresentation presentation = objectReviewPresentationService.BuildListPresentation(
                manualRois,
                manualRoiClassNames,
                manualRoiShapeKinds,
                manualRoiOverlayIds,
                GetVisibleManualSegments(),
                confirmedDetectionCandidates,
                preferredSelection,
                previousSelection,
                GetClippedCandidateBounds,
                FormatCandidateDetail);

            SetObjectReviewObjects(presentation.Rows, presentation.Summary, presentation.SelectedItem);
            UpdateObjectReviewActionState();
        }

        private WpfObjectReviewListItem BuildManualRoiObjectReviewItem(int index)
            => objectReviewPresentationService.BuildManualRoiItem(
                manualRois,
                manualRoiClassNames,
                manualRoiShapeKinds,
                manualRoiOverlayIds,
                index);

        private bool TryRefreshManualRoiObjectReviewRow(int manualRoiIndex, bool select)
        {
            WpfObjectReviewListItem row = BuildManualRoiObjectReviewItem(manualRoiIndex);
            if (row == null
                || !WpfObjectReviewSelectionService.CanReplaceManualRoiRow(
                    ObjectReviewViewModel?.Objects,
                    manualRoiIndex,
                    manualRois.Count))
            {
                return false;
            }

            bool replaced;
            using (ObjectReviewViewModel.SuppressSelectionNotifications())
            {
                replaced = ObjectReviewViewModel.TryReplaceObject(
                    manualRoiIndex,
                    row,
                    select);
            }

            SyncObjectClassEditorToSelection();
            UpdateObjectReviewActionState();
            return replaced;
        }

        private void SetObjectReviewObjects(
            IEnumerable<WpfObjectReviewListItem> rows,
            string summary,
            WpfObjectReviewItemRef selectedItem)
        {
            // Rebuilding the side list temporarily clears WPF SelectedItem. During ROI click/drag
            // that transient null must not clear the active canvas ROI handles.
            using (ObjectReviewViewModel.SuppressSelectionNotifications())
            {
                ObjectReviewViewModel.SetObjects(
                    rows,
                    summary,
                    selectedItem?.Source.ToString() ?? string.Empty,
                    selectedItem?.Index ?? -1);
            }

            SyncObjectClassEditorToSelection();
        }

        private string GetManualRoiClassName(int index)
            => WpfObjectReviewPresentationService.GetManualRoiClassName(manualRoiClassNames, index);

        private WpfObjectReviewListItem BuildManualSegmentObjectReviewItem(int manualSegmentIndex)
        {
            if (!IsSegmentationDatasetPurposeActive())
            {
                return null;
            }

            return BuildManualSegmentObjectReviewItemCore(manualSegmentIndex);
        }

        private WpfObjectReviewListItem BuildManualSegmentObjectReviewItemCore(int manualSegmentIndex)
            => objectReviewPresentationService.BuildManualSegmentItem(
                manualRois.Count,
                manualSegments,
                manualSegmentIndex);

        private bool TryRefreshManualSegmentObjectReviewRow(int manualSegmentIndex, string summary, bool select)
        {
            WpfObjectReviewListItem row = BuildManualSegmentObjectReviewItem(manualSegmentIndex);
            int objectRowIndex = manualRois.Count + manualSegmentIndex;
            if (row == null || ObjectReviewViewModel == null || objectRowIndex < 0)
            {
                return false;
            }

            bool updated;
            using (ObjectReviewViewModel.SuppressSelectionNotifications())
            {
                updated = ObjectReviewViewModel.TryUpsertObject(
                    objectRowIndex,
                    row,
                    summary,
                    select);
            }

            SyncObjectClassEditorToSelection();
            UpdateObjectReviewActionState();
            return updated;
        }


    }
}
