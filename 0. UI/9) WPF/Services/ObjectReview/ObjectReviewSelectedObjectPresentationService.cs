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
                selectedTaskTitleText = "\uC120\uD0DD \uB77C\uBCA8 \uC218\uC815";
                selectedTaskDetailText = string.IsNullOrWhiteSpace(selectedObject.DisplayText)
                    ? summaryText ?? string.Empty
                    : selectedObject.DisplayText;
                selectedTaskActionText = "\uD074\uB798\uC2A4\uB97C \uBC14\uAFB8\uAC70\uB098 \uC0AD\uC81C\uD558\uBA74 \uC800\uC7A5 \uD544\uC694 \uC0C1\uD0DC\uAC00 \uB429\uB2C8\uB2E4. \uB77C\uBCA8 \uC800\uC7A5\uC73C\uB85C \uD30C\uC77C\uC5D0 \uBC18\uC601\uD558\uC138\uC694.";
            }
            else
            {
                selectedTaskTitleText = hasAnyEnabledObject
                    ? "\uC120\uD0DD \uB77C\uBCA8 \uC5C6\uC74C"
                    : "\uD604\uC7AC \uC774\uBBF8\uC9C0 \uB77C\uBCA8 \uC5C6\uC74C";
                selectedTaskDetailText = hasAnyEnabledObject
                    ? "\uBAA9\uB85D\uC5D0\uC11C \uB77C\uBCA8\uC744 \uC120\uD0DD\uD558\uBA74 \uD074\uB798\uC2A4 \uBCC0\uACBD\uACFC \uC0AD\uC81C\uAC00 \uD65C\uC131\uD654\uB429\uB2C8\uB2E4."
                    : summaryText ?? string.Empty;
                selectedTaskActionText = hasAnyEnabledObject
                    ? "\uC120\uD0DD \uD6C4 \uD544\uC694\uD55C \uBCC0\uACBD\uC744 \uD558\uACE0, \uB77C\uBCA8 \uC800\uC7A5\uC73C\uB85C \uC644\uB8CC\uD558\uC138\uC694."
                    : "\uAC1D\uCCB4\uAC00 \uC5C6\uB2E4\uBA74 \uB2E4\uC74C \uC774\uBBF8\uC9C0\uB85C \uC774\uB3D9\uD558\uAC70\uB098 \uAC1D\uCCB4 \uC5C6\uC74C \uC791\uC5C5\uC73C\uB85C \uC644\uB8CC\uD558\uC138\uC694.";
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
                    : "\uC218\uB3D9 \uBC15\uC2A4 \uB610\uB294 \uC138\uADF8\uBA58\uD2B8\uB97C \uC120\uD0DD\uD558\uC138\uC694.",
                supportsPersistentMetadata,
                supportsPersistentMetadata && selectedObject.IsOccluded,
                supportsPersistentMetadata && !string.IsNullOrWhiteSpace(selectedObject.MetadataTagsText)
                    ? selectedObject.MetadataTagsText
                    : "\uD0DC\uADF8 \uC5C6\uC74C",
                isSelectedObjectGrouped,
                isSelectedObjectGrouped ? selectedObject.GroupDisplayText : "\uADF8\uB8F9 \uC5C6\uC74C",
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
