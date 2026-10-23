using System;
using System.Collections.Generic;

namespace MvcVisionSystem
{
    /// <summary>
    /// Projects the selected Object Review row into the three selected-object
    /// cards. The ViewModel remains the WPF binding adapter; session and
    /// metadata services remain the mutable state owners.
    /// </summary>
    public sealed class ObjectReviewSelectedObjectPresentationService
    {
        public ObjectReviewSelectedObjectPresentationSnapshot Build(
            WpfObjectReviewListItem selectedObject,
            string summaryText,
            IReadOnlyList<WpfObjectReviewListItem> objects)
        {
            bool hasSelectedObject = selectedObject?.IsEnabled == true;
            bool supportsSessionState = selectedObject?.SupportsSessionState == true;
            bool supportsPersistentMetadata = selectedObject?.SupportsPersistentMetadata == true;
            bool hasAnyEnabledObject = false;
            foreach (WpfObjectReviewListItem item in objects ?? Array.Empty<WpfObjectReviewListItem>())
            {
                if (item?.IsEnabled == true)
                {
                    hasAnyEnabledObject = true;
                    break;
                }
            }

            string selectedTaskTitleText;
            string selectedTaskDetailText;
            string selectedTaskActionText;
            if (hasSelectedObject)
            {
                selectedTaskTitleText = OpenVisionLab.OpenVisionLanguageService.T("WpfObjectReview.Selected.EditTitle");
                selectedTaskDetailText = string.IsNullOrWhiteSpace(selectedObject.DisplayText)
                    ? summaryText ?? string.Empty
                    : selectedObject.DisplayText;
                selectedTaskActionText = OpenVisionLab.OpenVisionLanguageService.T("WpfObjectReview.Selected.SaveAction");
            }
            else
            {
                selectedTaskTitleText = hasAnyEnabledObject
                    ? OpenVisionLab.OpenVisionLanguageService.T("WpfObjectReview.Selected.NoSelectionTitle")
                    : OpenVisionLab.OpenVisionLanguageService.T("WpfObjectReview.Selected.EmptyTitle");
                selectedTaskDetailText = hasAnyEnabledObject
                    ? OpenVisionLab.OpenVisionLanguageService.T("WpfObjectReview.Selected.SelectDetail")
                    : summaryText ?? string.Empty;
                selectedTaskActionText = hasAnyEnabledObject
                    ? OpenVisionLab.OpenVisionLanguageService.T("WpfObjectReview.Selected.SelectAction")
                    : OpenVisionLab.OpenVisionLanguageService.T("WpfObjectReview.Selected.EmptyAction");
            }

            bool isSelectedObjectGrouped = supportsPersistentMetadata
                && !string.IsNullOrWhiteSpace(selectedObject.GroupId);
            return new ObjectReviewSelectedObjectPresentationSnapshot(
                supportsSessionState,
                supportsSessionState && selectedObject.IsHidden,
                supportsSessionState && selectedObject.IsLocked,
                supportsSessionState && selectedObject.IsPinned,
                supportsSessionState
                    ? selectedObject.ObjectSessionStateText
                    : OpenVisionLab.OpenVisionLanguageService.T("WpfObjectReview.Selected.SessionSelect"),
                supportsPersistentMetadata,
                supportsPersistentMetadata && selectedObject.IsOccluded,
                supportsPersistentMetadata && !string.IsNullOrWhiteSpace(selectedObject.MetadataTagsText)
                    ? selectedObject.MetadataTagsText
                    : OpenVisionLab.OpenVisionLanguageService.T("WpfObjectReview.Selected.NoTags"),
                isSelectedObjectGrouped,
                isSelectedObjectGrouped ? selectedObject.GroupDisplayText : OpenVisionLab.OpenVisionLanguageService.T("WpfObjectReview.Selected.NoGroup"),
                selectedTaskTitleText,
                selectedTaskDetailText,
                selectedTaskActionText);
        }
    }

    public sealed class ObjectReviewSelectedObjectPresentationSnapshot
    {
        public ObjectReviewSelectedObjectPresentationSnapshot(
            bool isObjectSessionStateEnabled,
            bool isSelectedObjectHidden,
            bool isSelectedObjectLocked,
            bool isSelectedObjectPinned,
            string objectSessionStateStatusText,
            bool isPersistentMetadataEnabled,
            bool isSelectedObjectOccluded,
            string selectedObjectTagsText,
            bool isSelectedObjectGrouped,
            string selectedObjectGroupText,
            string selectedObjectTaskTitleText,
            string selectedObjectTaskDetailText,
            string selectedObjectTaskActionText)
        {
            IsObjectSessionStateEnabled = isObjectSessionStateEnabled;
            IsSelectedObjectHidden = isSelectedObjectHidden;
            IsSelectedObjectLocked = isSelectedObjectLocked;
            IsSelectedObjectPinned = isSelectedObjectPinned;
            ObjectSessionStateStatusText = objectSessionStateStatusText ?? string.Empty;
            IsPersistentMetadataEnabled = isPersistentMetadataEnabled;
            IsSelectedObjectOccluded = isSelectedObjectOccluded;
            SelectedObjectTagsText = selectedObjectTagsText ?? string.Empty;
            IsSelectedObjectGrouped = isSelectedObjectGrouped;
            SelectedObjectGroupText = selectedObjectGroupText ?? string.Empty;
            SelectedObjectTaskTitleText = selectedObjectTaskTitleText ?? string.Empty;
            SelectedObjectTaskDetailText = selectedObjectTaskDetailText ?? string.Empty;
            SelectedObjectTaskActionText = selectedObjectTaskActionText ?? string.Empty;
        }

        public bool IsObjectSessionStateEnabled { get; }

        public bool IsSelectedObjectHidden { get; }

        public bool IsSelectedObjectLocked { get; }

        public bool IsSelectedObjectPinned { get; }

        public string ObjectSessionStateStatusText { get; }

        public bool IsPersistentMetadataEnabled { get; }

        public bool IsSelectedObjectOccluded { get; }

        public string SelectedObjectTagsText { get; }

        public bool IsSelectedObjectGrouped { get; }

        public string SelectedObjectGroupText { get; }

        public string SelectedObjectTaskTitleText { get; }

        public string SelectedObjectTaskDetailText { get; }

        public string SelectedObjectTaskActionText { get; }
    }
}
