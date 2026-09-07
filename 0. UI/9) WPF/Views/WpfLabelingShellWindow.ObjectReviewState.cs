using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using OpenVisionLab.Wpf.MessageDialogs;

namespace MvcVisionSystem
{
    // Responsibility group: object metadata and session state.
    // These members remain WPF Window adapters; independent policy belongs in services.
    public partial class WpfLabelingShellWindow
    {
        #region ObjectMetadata
        private void RestoreObjectMetadataTagsFromProject()
        {
            EnsureProjectSettings();
            ObjectReviewViewModel?.SetMetadataTagDefinitions(
                global.Data.ProjectSettings.ObjectReviewTags);
        }

        private void LoadObjectMetadataForActiveImage(string imagePath)
        {
            RestoreObjectMetadataTagsFromProject();
            WpfObjectReviewLoadResult workflowResult = objectReviewWorkflowService.Load(
                BuildObjectReviewPersistenceRequest(
                    imagePath,
                    Path.GetFileName(imagePath ?? string.Empty)));
            if (workflowResult.ShouldClearExistingState)
            {
                objectMetadataStateService.Clear();
            }

            ApplyObjectReviewMetadataChanges(workflowResult.MetadataChanges);
            WpfObjectMetadataLoadResult result = workflowResult.PersistenceResult;
            if (!string.IsNullOrWhiteSpace(result.StatusText))
            {
                AppendLog(result.IsCompatible
                    ? result.StatusText
                    : $"\uAC1D\uCCB4 \uBA54\uD0C0\uB370\uC774\uD130 \uBB34\uC2DC: {result.StatusText}");
            }
        }

        private bool TrySaveCurrentObjectMetadata(string imageName)
        {
            try
            {
                objectReviewWorkflowService.Save(
                    BuildObjectReviewPersistenceRequest(string.Empty, imageName));
                return true;
            }
            catch (Exception ex)
            {
                string status = $"\uAC1D\uCCB4 \uBA54\uD0C0\uB370\uC774\uD130 \uC800\uC7A5 \uC2E4\uD328: {ex.Message}";
                SetYoloCommandStatus(status, isBusy: false);
                AppendLog(status);
                return false;
            }
        }

        private void ExecuteTogglePersistentOccludedCommand()
        {
            if (!TryGetSelectedObjectReviewItem(out WpfObjectReviewItemRef item)
                || !TryCaptureObjectReviewSnapshot(item, out WpfObjectReviewObjectSnapshot snapshot))
            {
                SetYoloCommandStatus(
                    "\uC218\uB3D9 \uBC15\uC2A4 \uB610\uB294 \uC138\uADF8\uBA3C\uD2B8\uB97C \uC120\uD0DD\uD558\uC138\uC694.",
                    isBusy: false);
                return;
            }

            WpfObjectReviewMutationResult workflowResult =
                objectReviewWorkflowService.ToggleOccluded(snapshot);
            if (!workflowResult.IsApplicable
                || !ApplyObjectReviewMutation(workflowResult))
            {
                SetYoloCommandStatus(
                    "\uC218\uB3D9 \uBC15\uC2A4 \uB610\uB294 \uC138\uADF8\uBA3C\uD2B8\uB97C \uC120\uD0DD\uD558\uC138\uC694.",
                    isBusy: false);
                return;
            }

            WpfPersistentObjectMetadata metadata = workflowResult.Metadata;

            RefreshObjectListWithSelection(item);
            MarkAnnotationsDirty(
                metadata.IsOccluded
                    ? "\uAC1D\uCCB4 \uAC00\uB9BC \uBA54\uD0C0\uB370\uC774\uD130 \uC124\uC815"
                    : "\uAC1D\uCCB4 \uAC00\uB9BC \uBA54\uD0C0\uB370\uC774\uD130 \uD574\uC81C");
            string status = metadata.IsOccluded
                ? "\uAC00\uB9BC \uAC1D\uCCB4\uB85C \uD45C\uC2DC\uD588\uC2B5\uB2C8\uB2E4. \uB77C\uBCA8 \uC800\uC7A5 \uC2DC \uBA54\uD0C0\uB370\uC774\uD130\uC5D0 \uBC18\uC601\uB429\uB2C8\uB2E4."
                : "\uAC00\uB9BC \uD45C\uC2DC\uB97C \uD574\uC81C\uD588\uC2B5\uB2C8\uB2E4. \uB77C\uBCA8 \uC800\uC7A5 \uC2DC \uBC18\uC601\uB429\uB2C8\uB2E4.";
            SetYoloCommandStatus(status, isBusy: false);
            AppendLog(status);
        }

        private void ExecuteTogglePersistentTagCommand(string requestedTag)
        {
            if (string.IsNullOrWhiteSpace(requestedTag?.Trim())
                || !TryGetSelectedObjectReviewItem(out WpfObjectReviewItemRef item)
                || !TryCaptureObjectReviewSnapshot(item, out WpfObjectReviewObjectSnapshot snapshot))
            {
                SetYoloCommandStatus(
                    "\uC218\uB3D9 \uBC15\uC2A4/\uC138\uADF8\uBA3C\uD2B8\uB97C \uC120\uD0DD\uD558\uACE0 \uD0DC\uADF8\uB97C \uC785\uB825\uD558\uC138\uC694.",
                    isBusy: false);
                return;
            }

            WpfObjectReviewMutationResult workflowResult = objectReviewWorkflowService.ToggleTag(
                snapshot,
                requestedTag,
                global.Data,
                GetCurrentRecipeName());
            if (!workflowResult.IsApplicable)
            {
                ReportObjectReviewWorkflowError(workflowResult);
                return;
            }

            if (!ApplyObjectReviewMutation(workflowResult))
            {
                SetYoloCommandStatus(
                    "\uC218\uB3D9 \uBC15\uC2A4 \uB610\uB294 \uC138\uADF8\uBA3C\uD2B8\uC5D0\uB9CC \uD0DC\uADF8\uB97C \uC801\uC6A9\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.",
                    isBusy: false);
                return;
            }

            string tag = workflowResult.Tag;
            WpfPersistentObjectMetadata metadata = workflowResult.Metadata;
            bool isApplied = metadata.Tags.Any(value =>
                string.Equals(value, tag, StringComparison.OrdinalIgnoreCase));
            RefreshObjectListWithSelection(item);
            MarkAnnotationsDirty(
                isApplied
                    ? $"\uAC1D\uCCB4 \uD0DC\uADF8 \uC124\uC815: {tag}"
                    : $"\uAC1D\uCCB4 \uD0DC\uADF8 \uD574\uC81C: {tag}");
            string status = isApplied
                ? $"\uD0DC\uADF8 \uC801\uC6A9: {tag} \u00B7 \uB77C\uBCA8 \uC800\uC7A5 \uD544\uC694"
                : $"\uD0DC\uADF8 \uD574\uC81C: {tag} \u00B7 \uB77C\uBCA8 \uC800\uC7A5 \uD544\uC694";
            SetYoloCommandStatus(status, isBusy: false);
            AppendLog(status);
        }

        private void ExecuteResetRecipeMetadataTagsCommand()
        {
            WpfObjectReviewMutationResult workflowResult =
                objectReviewWorkflowService.ResetRecipeMetadataTags(
                    global.Data,
                    GetCurrentRecipeName());
            if (!workflowResult.IsApplicable)
            {
                ReportObjectReviewWorkflowError(workflowResult);
                return;
            }

            ApplyObjectReviewMutation(workflowResult);
            const string status =
                "Recipe \uD0DC\uADF8 \uBAA9\uB85D\uC744 \uAE30\uBCF8\uAC12(\uBE48 \uBAA9\uB85D)\uC73C\uB85C \uB418\uB3CC\uB838\uC2B5\uB2C8\uB2E4. \uAE30\uC874 \uAC1D\uCCB4\uC5D0 \uC800\uC7A5\uB41C \uD0DC\uADF8\uB294 \uC0AD\uC81C\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.";
            SetYoloCommandStatus(status, isBusy: false);
            AppendLog(status);
        }

        private void ExecuteBeginObjectGroupSelectionCommand()
        {
            objectReviewWorkflowService.BeginGroupSelection();
            ObjectReviewViewModel?.SetGroupSelectionMode(true);
            const string status = "\uADF8\uB8F9 \uAD6C\uC131 \uC120\uD0DD \uC2DC\uC791: \uBAA9\uB85D\uC5D0\uC11C \uBBF8\uADF8\uB8F9 \uC800\uC7A5 \uAC1D\uCCB4\uB97C 2\uAC1C \uC774\uC0C1 \uC120\uD0DD\uD558\uC138\uC694.";
            ObjectReviewViewModel?.RefreshGroupSelectionPresentation(status);
            SetYoloCommandStatus(status, isBusy: false);
        }

        private void ExecuteCancelObjectGroupSelectionCommand()
            => CancelObjectGroupSelection(updateStatus: true);

        private void CancelObjectGroupSelection(bool updateStatus)
        {
            bool wasActive = objectReviewWorkflowService.IsGroupSelectionActive;
            objectReviewWorkflowService.CancelGroupSelection();
            ObjectReviewViewModel?.SetGroupSelectionMode(false);
            if (wasActive && updateStatus)
            {
                const string status = "\uADF8\uB8F9 \uAD6C\uC131 \uC120\uD0DD\uC744 \uCDE8\uC18C\uD588\uC2B5\uB2C8\uB2E4.";
                SetYoloCommandStatus(status, isBusy: false);
                AppendLog(status);
            }
        }

        private void ExecuteObjectGroupSelectionChangedCommand(object value)
        {
            if (value is not WpfObjectReviewListItem row
                || row.Payload is not WpfObjectReviewItemRef item)
            {
                return;
            }

            if (!objectReviewWorkflowService.SetGroupSelection(
                item,
                row.IsGroupSelected,
                out string error))
            {
                row.IsGroupSelected = false;
                ObjectReviewViewModel?.RefreshGroupSelectionPresentation(error);
                SetYoloCommandStatus(error, isBusy: false);
                return;
            }

            ObjectReviewViewModel?.RefreshGroupSelectionPresentation();
        }

        private void ExecuteCreateObjectGroupCommand()
        {
            WpfObjectReviewMutationResult workflowResult = objectReviewWorkflowService.CreateGroup(
                CaptureObjectReviewSnapshots());
            if (!workflowResult.IsApplicable)
            {
                ObjectReviewViewModel?.RefreshGroupSelectionPresentation(workflowResult.ErrorMessage);
                SetYoloCommandStatus(workflowResult.ErrorMessage, isBusy: false);
                return;
            }

            if (!ApplyObjectReviewMutation(workflowResult))
            {
                const string applyError = "\uADF8\uB8F9 \uAD6C\uC131\uC6D0 \uC801\uC6A9 \uC911 \uAC1D\uCCB4\uAC00 \uBCC0\uACBD\uB418\uC5B4 \uC804\uCCB4 \uC791\uC5C5\uC744 \uB418\uB3CC\uB838\uC2B5\uB2C8\uB2E4.";
                ObjectReviewViewModel?.RefreshGroupSelectionPresentation(applyError);
                SetYoloCommandStatus(applyError, isBusy: false);
                return;
            }

            WpfObjectReviewItemRef selection = workflowResult.FocusItem;
            int memberCount = workflowResult.MetadataChanges.Count;
            CancelObjectGroupSelection(updateStatus: false);
            RefreshObjectListWithSelection(selection);
            MarkAnnotationsDirty($"\uAC80\uC218 \uADF8\uB8F9 \uC0DD\uC131: {memberCount}\uAC1C");
            string status = $"\uAC80\uC218 \uADF8\uB8F9 \uC0DD\uC131: {memberCount}\uAC1C \u00B7 \uB77C\uBCA8 \uC800\uC7A5 \uD544\uC694";
            SetYoloCommandStatus(status, isBusy: false);
            AppendLog(status);
        }

        private void ExecuteRemoveSelectedObjectFromGroupCommand()
        {
            if (!TryGetSelectedObjectReviewItem(out WpfObjectReviewItemRef item))
            {
                return;
            }

            WpfObjectReviewMutationResult workflowResult = objectReviewWorkflowService.RemoveSelectedFromGroup(
                CaptureObjectReviewSnapshots(),
                item);
            if (!workflowResult.IsApplicable)
            {
                SetYoloCommandStatus("\uC120\uD0DD\uD55C \uADF8\uB8F9 \uAD6C\uC131\uC6D0\uC744 \uD655\uC778\uD558\uC138\uC694.", isBusy: false);
                return;
            }

            if (!ApplyObjectReviewMutation(workflowResult))
            {
                SetYoloCommandStatus("\uC120\uD0DD\uD55C \uADF8\uB8F9 \uAD6C\uC131\uC6D0\uC744 \uD655\uC778\uD558\uC138\uC694.", isBusy: false);
                return;
            }

            int dissolved = workflowResult.DissolvedGroupCount;
            RefreshObjectListWithSelection(item);
            MarkAnnotationsDirty("\uAC80\uC218 \uADF8\uB8F9 \uAD6C\uC131\uC6D0 \uC81C\uAC70");
            string status = dissolved > 0
                ? "\uADF8\uB8F9\uC5D0\uC11C \uC81C\uAC70\uD588\uACE0 1\uAC1C\uB9CC \uB0A8\uC740 \uADF8\uB8F9\uC740 \uC790\uB3D9 \uD574\uC81C\uD588\uC2B5\uB2C8\uB2E4. \uB77C\uBCA8 \uC800\uC7A5 \uD544\uC694"
                : "\uC120\uD0DD \uAC1D\uCCB4\uB97C \uADF8\uB8F9\uC5D0\uC11C \uC81C\uAC70\uD588\uC2B5\uB2C8\uB2E4. \uB77C\uBCA8 \uC800\uC7A5 \uD544\uC694";
            SetYoloCommandStatus(status, isBusy: false);
            AppendLog(status);
        }

        private void ExecuteDissolveSelectedObjectGroupCommand()
        {
            if (!TryGetSelectedObjectReviewItem(out WpfObjectReviewItemRef selected))
            {
                return;
            }

            IReadOnlyList<WpfObjectReviewObjectSnapshot> members = objectReviewWorkflowService.GetGroupMembers(
                CaptureObjectReviewSnapshots(),
                selected);
            if (members.Count < 2)
            {
                SetYoloCommandStatus("\uD574\uC81C\uD560 \uAC80\uC218 \uADF8\uB8F9\uC744 \uC120\uD0DD\uD558\uC138\uC694.", isBusy: false);
                return;
            }

            WpfMessageDialogResult result = WpfMessageDialog.Confirm(
                this,
                "\uAC80\uC218 \uADF8\uB8F9 \uD574\uC81C",
                $"\uAD6C\uC131\uC6D0 {members.Count}\uAC1C\uC758 \uADF8\uB8F9 \uAD00\uACC4\uB97C \uD574\uC81C\uD569\uB2C8\uB2E4. \uAC1D\uCCB4\uC640 \uB77C\uBCA8\uC740 \uC0AD\uC81C\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.",
                "\uADF8\uB8F9 \uD574\uC81C",
                "\uCDE8\uC18C");
            if (result != WpfMessageDialogResult.Yes)
            {
                return;
            }

            WpfObjectReviewMutationResult workflowResult = objectReviewWorkflowService.ClearGroup(members);
            if (!workflowResult.IsApplicable || !ApplyObjectReviewMutation(workflowResult))
            {
                SetYoloCommandStatus("\uC120\uD0DD\uD55C \uAC80\uC218 \uADF8\uB8F9\uC744 \uD655\uC778\uD558\uC138\uC694.", isBusy: false);
                return;
            }
            RefreshObjectListWithSelection(selected);
            MarkAnnotationsDirty($"\uAC80\uC218 \uADF8\uB8F9 \uD574\uC81C: {members.Count}\uAC1C");
            string status = $"\uAC80\uC218 \uADF8\uB8F9 \uD574\uC81C: {members.Count}\uAC1C \u00B7 \uB77C\uBCA8 \uC800\uC7A5 \uD544\uC694";
            SetYoloCommandStatus(status, isBusy: false);
            AppendLog(status);
        }

        private void ExecuteToggleObjectGroupOccludedCommand()
        {
            if (!TryResolveSelectedGroupMembers(
                out WpfObjectReviewItemRef selected,
                out IReadOnlyList<WpfObjectReviewObjectSnapshot> members))
            {
                return;
            }

            WpfObjectReviewMutationResult workflowResult =
                objectReviewWorkflowService.ToggleGroupOccluded(members);
            if (!workflowResult.IsApplicable || !ApplyObjectReviewMutation(workflowResult))
            {
                SetYoloCommandStatus("\uC120\uD0DD\uD55C \uADF8\uB8F9 \uAD6C\uC131\uC6D0\uC744 \uD655\uC778\uD558\uC138\uC694.", isBusy: false);
                return;
            }
            bool apply = workflowResult.AppliedValue;
            RefreshObjectListWithSelection(selected);
            MarkAnnotationsDirty(apply ? "\uADF8\uB8F9 \uAC00\uB9BC \uC801\uC6A9" : "\uADF8\uB8F9 \uAC00\uB9BC \uD574\uC81C");
            string status = $"\uADF8\uB8F9 {members.Count}\uAC1C \uAC1D\uCCB4 \uAC00\uB9BC {(apply ? "\uC801\uC6A9" : "\uD574\uC81C")} \u00B7 \uB77C\uBCA8 \uC800\uC7A5 \uD544\uC694";
            SetYoloCommandStatus(status, isBusy: false);
            AppendLog(status);
        }

        private void ExecuteToggleObjectGroupTagCommand(string requestedTag)
        {
            if (string.IsNullOrWhiteSpace(requestedTag?.Trim())
                || !TryResolveSelectedGroupMembers(
                    out WpfObjectReviewItemRef selected,
                    out IReadOnlyList<WpfObjectReviewObjectSnapshot> members))
            {
                SetYoloCommandStatus("\uADF8\uB8F9\uACFC Recipe \uD0DC\uADF8\uB97C \uC120\uD0DD\uD558\uC138\uC694.", isBusy: false);
                return;
            }

            WpfObjectReviewMutationResult workflowResult = objectReviewWorkflowService.ToggleGroupTag(
                members,
                requestedTag,
                global.Data,
                GetCurrentRecipeName());
            if (!workflowResult.IsApplicable)
            {
                ReportObjectReviewWorkflowError(workflowResult);
                return;
            }

            if (!ApplyObjectReviewMutation(workflowResult))
            {
                SetYoloCommandStatus("\uC120\uD0DD\uD55C \uADF8\uB8F9 \uAD6C\uC131\uC6D0\uC744 \uD655\uC778\uD558\uC138\uC694.", isBusy: false);
                return;
            }
            string tag = workflowResult.Tag;
            bool apply = workflowResult.AppliedValue;
            RefreshObjectListWithSelection(selected);
            MarkAnnotationsDirty(apply ? $"\uADF8\uB8F9 \uD0DC\uADF8 \uC801\uC6A9: {tag}" : $"\uADF8\uB8F9 \uD0DC\uADF8 \uD574\uC81C: {tag}");
            string status = $"\uADF8\uB8F9 {members.Count}\uAC1C \uAC1D\uCCB4 \uD0DC\uADF8 {(apply ? "\uC801\uC6A9" : "\uD574\uC81C")}: {tag} \u00B7 \uB77C\uBCA8 \uC800\uC7A5 \uD544\uC694";
            SetYoloCommandStatus(status, isBusy: false);
            AppendLog(status);
        }

        private bool TryResolveSelectedGroupMembers(
            out WpfObjectReviewItemRef selected,
            out IReadOnlyList<WpfObjectReviewObjectSnapshot> members)
        {
            members = Array.Empty<WpfObjectReviewObjectSnapshot>();
            if (!TryGetSelectedObjectReviewItem(out selected))
            {
                return false;
            }

            members = objectReviewWorkflowService.GetGroupMembers(
                CaptureObjectReviewSnapshots(),
                selected);
            if (members.Count >= 2)
            {
                return true;
            }

            SetYoloCommandStatus("\uADF8\uB8F9 \uAD6C\uC131\uC6D0\uC744 \uC120\uD0DD\uD558\uC138\uC694.", isBusy: false);
            return false;
        }

        private WpfObjectReviewPersistenceRequest BuildObjectReviewPersistenceRequest(
            string imagePath,
            string imageName)
        {
            return new WpfObjectReviewPersistenceRequest(
                imagePath,
                imageName,
                global.Data,
                CaptureObjectReviewSnapshots());
        }

        private IReadOnlyList<WpfObjectReviewObjectSnapshot> CaptureObjectReviewSnapshots()
        {
            var snapshots = new List<WpfObjectReviewObjectSnapshot>();
            for (int index = 0; index < manualRois.Count; index++)
            {
                snapshots.Add(new WpfObjectReviewObjectSnapshot(
                    WpfObjectReviewItemRef.Manual(index),
                    manualRois[index],
                    index < manualRoiClassNames.Count ? manualRoiClassNames[index] : string.Empty,
                    string.Empty,
                    objectMetadataStateService.GetManualRoiMetadata(index)));
            }

            for (int index = 0; index < manualSegments.Count; index++)
            {
                LabelingSegmentationObject segment = manualSegments[index];
                if (segment == null)
                {
                    continue;
                }

                snapshots.Add(new WpfObjectReviewObjectSnapshot(
                    WpfObjectReviewItemRef.ManualSegment(index),
                    Rectangle.Empty,
                    segment.ClassName,
                    segment.ObjectId,
                    objectMetadataStateService.GetManualSegmentMetadata(segment)));
            }

            return snapshots;
        }

        private bool TryCaptureObjectReviewSnapshot(
            WpfObjectReviewItemRef item,
            out WpfObjectReviewObjectSnapshot snapshot)
        {
            snapshot = null;
            if (item?.Source == WpfObjectReviewSource.ManualRoi
                && item.Index >= 0
                && item.Index < manualRois.Count)
            {
                snapshot = new WpfObjectReviewObjectSnapshot(
                    item,
                    manualRois[item.Index],
                    item.Index < manualRoiClassNames.Count ? manualRoiClassNames[item.Index] : string.Empty,
                    string.Empty,
                    objectMetadataStateService.GetManualRoiMetadata(item.Index));
                return true;
            }

            if (item?.Source == WpfObjectReviewSource.ManualSegment
                && item.Index >= 0
                && item.Index < manualSegments.Count
                && manualSegments[item.Index] != null)
            {
                LabelingSegmentationObject segment = manualSegments[item.Index];
                snapshot = new WpfObjectReviewObjectSnapshot(
                    item,
                    Rectangle.Empty,
                    segment.ClassName,
                    segment.ObjectId,
                    objectMetadataStateService.GetManualSegmentMetadata(segment));
                return true;
            }

            return false;
        }

        private bool ApplyObjectReviewMutation(WpfObjectReviewMutationResult workflowResult)
        {
            if (workflowResult == null || !workflowResult.IsApplicable)
            {
                return false;
            }

            if (workflowResult.RecipeTagsChanged)
            {
                EnsureProjectSettings();
                ObjectReviewViewModel?.SetMetadataTagDefinitions(
                    global.Data.ProjectSettings.ObjectReviewTags);
            }

            return ApplyObjectReviewMetadataChanges(workflowResult.MetadataChanges);
        }

        private bool ApplyObjectReviewMetadataChanges(
            IEnumerable<WpfObjectReviewMetadataChange> changes)
        {
            List<WpfObjectReviewMetadataChange> pending = (changes
                ?? Enumerable.Empty<WpfObjectReviewMetadataChange>())
                .ToList();
            foreach (WpfObjectReviewMetadataChange change in pending)
            {
                if (change?.Item?.Source == WpfObjectReviewSource.ManualRoi
                    && change.Item.Index >= 0
                    && change.Item.Index < manualRois.Count)
                {
                    continue;
                }

                if (change?.Item?.Source == WpfObjectReviewSource.ManualSegment
                    && change.Item.Index >= 0
                    && change.Item.Index < manualSegments.Count
                    && manualSegments[change.Item.Index] != null)
                {
                    continue;
                }

                return false;
            }

            foreach (WpfObjectReviewMetadataChange change in pending)
            {
                if (change.Item.Source == WpfObjectReviewSource.ManualRoi)
                {
                    objectMetadataStateService.SetManualRoiMetadata(
                        change.Item.Index,
                        change.Metadata);
                }
                else
                {
                    objectMetadataStateService.SetManualSegmentMetadata(
                        manualSegments[change.Item.Index],
                        change.Metadata);
                }
            }

            return true;
        }

        private void ReportObjectReviewWorkflowError(
            WpfObjectReviewMutationResult workflowResult)
        {
            if (workflowResult == null || string.IsNullOrWhiteSpace(workflowResult.ErrorMessage))
            {
                return;
            }

            SetYoloCommandStatus(workflowResult.ErrorMessage, isBusy: false);
            if (workflowResult.AppendErrorToLog)
            {
                AppendLog(workflowResult.ErrorMessage);
            }
        }

        private WpfPersistentObjectMetadata GetObjectPersistentMetadata(WpfObjectReviewItemRef item)
        {
            if (item?.Source == WpfObjectReviewSource.ManualRoi)
            {
                return objectMetadataStateService.GetManualRoiMetadata(item.Index);
            }

            if (item?.Source == WpfObjectReviewSource.ManualSegment
                && item.Index >= 0
                && item.Index < manualSegments.Count)
            {
                return objectMetadataStateService.GetManualSegmentMetadata(
                    manualSegments[item.Index]);
            }

            return WpfPersistentObjectMetadata.Default;
        }

        private void ApplyObjectPersistentMetadata(IEnumerable<WpfObjectReviewListItem> rows)
        {
            foreach (WpfObjectReviewListItem row in rows ?? Enumerable.Empty<WpfObjectReviewListItem>())
            {
                row?.ApplyPersistentMetadata(row.Payload is WpfObjectReviewItemRef item
                    ? GetObjectPersistentMetadata(item)
                    : WpfPersistentObjectMetadata.Default);
            }
        }
        #endregion

        #region ObjectSessionStateCommands
        private void ExecuteToggleObjectHiddenCommand()
            => ToggleSelectedObjectSessionState(WpfObjectSessionStateKind.Hidden);

        private void ExecuteToggleObjectLockedCommand()
            => ToggleSelectedObjectSessionState(WpfObjectSessionStateKind.Locked);

        private void ExecuteToggleObjectPinnedCommand()
            => ToggleSelectedObjectSessionState(WpfObjectSessionStateKind.Pinned);

        private void ToggleSelectedObjectSessionState(WpfObjectSessionStateKind kind)
        {
            if (!TryGetSelectedObjectReviewItem(out WpfObjectReviewItemRef item))
            {
                const string unsupported = "\uC218\uB3D9 \uBC15\uC2A4 \uB610\uB294 \uC138\uADF8\uBA3C\uD2B8\uB97C \uC120\uD0DD\uD558\uC138\uC694.";
                SetYoloCommandStatus(unsupported, isBusy: false);
                return;
            }

            CompleteMaskAnnotationStroke();
            FlushQueuedMaskStrokeCommits();
            CompleteSelectedSegmentEdit();
            CancelPendingSegmentationRemoveUnderlying(updateStatus: false);
            CancelPendingSegmentationSplit(updateStatus: false);
            CancelPendingSegmentationHoleEdit(updateStatus: false);
            CancelPendingPolygonVertexEdit(updateStatus: false);
            CancelPendingIntelligentScissors(updateStatus: false);
            if (!TryToggleObjectSessionState(item, kind, out WpfObjectSessionState state))
            {
                const string unsupported = "\uC218\uB3D9 \uBC15\uC2A4 \uB610\uB294 \uC138\uADF8\uBA3C\uD2B8\uB97C \uC120\uD0DD\uD558\uC138\uC694.";
                SetYoloCommandStatus(unsupported, isBusy: false);
                return;
            }

            if (state.IsHidden || state.IsLocked)
            {
                MainCanvasViewModel?.ClearRoiSelection();
                MainCanvasViewModel.IsImagePointInputMode = false;
            }

            if (item.Source == WpfObjectReviewSource.ManualRoi)
            {
                RedrawReviewRois();
            }
            else
            {
                RefreshPolygonOverlays();
            }

            RefreshObjectListWithSelection(item);
            string action = FormatObjectSessionStateKind(kind);
            string stateText = kind switch
            {
                WpfObjectSessionStateKind.Hidden => state.IsHidden ? "\uCF1C\uC9D0" : "\uAEBC\uC9D0",
                WpfObjectSessionStateKind.Locked => state.IsLocked ? "\uCF1C\uC9D0" : "\uAEBC\uC9D0",
                WpfObjectSessionStateKind.Pinned => state.IsPinned ? "\uCF1C\uC9D0" : "\uAEBC\uC9D0",
                _ => string.Empty
            };
            string status = $"{action}: {stateText} \u00B7 \uD604\uC7AC \uC774\uBBF8\uC9C0 \uC138\uC158\uB9CC";
            SetYoloCommandStatus(status, isBusy: false);
            AppendLog(status);
        }

        private bool TryToggleObjectSessionState(
            WpfObjectReviewItemRef item,
            WpfObjectSessionStateKind kind,
            out WpfObjectSessionState state)
        {
            state = WpfObjectSessionState.Default;
            if (item == null)
            {
                return false;
            }

            if (item.Source == WpfObjectReviewSource.ManualRoi
                && item.Index >= 0
                && item.Index < manualRois.Count)
            {
                state = objectSessionStateService.ToggleManualRoiState(item.Index, kind);
                return true;
            }

            if (item.Source == WpfObjectReviewSource.ManualSegment
                && item.Index >= 0
                && item.Index < manualSegments.Count
                && manualSegments[item.Index] != null)
            {
                state = objectSessionStateService.ToggleManualSegmentState(manualSegments[item.Index], kind);
                return true;
            }

            return false;
        }

        private WpfObjectSessionState GetObjectSessionState(WpfObjectReviewItemRef item)
        {
            if (item?.Source == WpfObjectReviewSource.ManualRoi)
            {
                return objectSessionStateService.GetManualRoiState(item.Index);
            }

            if (item?.Source == WpfObjectReviewSource.ManualSegment
                && item.Index >= 0
                && item.Index < manualSegments.Count)
            {
                return objectSessionStateService.GetManualSegmentState(manualSegments[item.Index]);
            }

            return WpfObjectSessionState.Default;
        }

        private WpfObjectSessionState GetManualRoiSessionState(int index)
            => objectSessionStateService.GetManualRoiState(index);

        private WpfObjectSessionState GetManualSegmentSessionState(int index)
            => index >= 0 && index < manualSegments.Count
                ? objectSessionStateService.GetManualSegmentState(manualSegments[index])
                : WpfObjectSessionState.Default;

        private bool CanMutateSelectedObject(
            WpfObjectReviewItemRef item,
            bool requireVisible,
            out string error)
        {
            WpfObjectSessionState state = GetObjectSessionState(item);
            if (state.IsLocked)
            {
                error = "\uC7A0\uAE34 \uAC1D\uCCB4\uC785\uB2C8\uB2E4. \uC7A0\uAE08\uC744 \uD574\uC81C\uD55C \uD6C4 \uC218\uC815\uD558\uC138\uC694.";
                return false;
            }

            if (requireVisible && state.IsHidden)
            {
                error = "\uC228\uAE34 \uAC1D\uCCB4\uC785\uB2C8\uB2E4. \uD45C\uC2DC\uB97C \uCF20 \uD6C4 \uAD6C\uC870\uB97C \uC218\uC815\uD558\uC138\uC694.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private bool CanEditManualSegment(LabelingSegmentationObject segment)
        {
            WpfObjectSessionState state = objectSessionStateService.GetManualSegmentState(segment);
            return !state.IsHidden && !state.IsLocked;
        }

        private static string FormatObjectSessionStateKind(WpfObjectSessionStateKind kind)
            => kind switch
            {
                WpfObjectSessionStateKind.Hidden => "\uC228\uAE40",
                WpfObjectSessionStateKind.Locked => "\uC7A0\uAE08",
                WpfObjectSessionStateKind.Pinned => "\uC774\uB3D9 \uACE0\uC815",
                _ => "\uAC1D\uCCB4 \uC138\uC158 \uC0C1\uD0DC"
            };
        #endregion

    }
}
