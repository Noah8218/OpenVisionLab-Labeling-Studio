using MvcVisionSystem.Yolo;
using OpenVisionLab.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using DrawingColor = System.Drawing.Color;

namespace MvcVisionSystem
{
    // Responsibility group: class catalog editing and persistence.
    // These members remain WPF Window adapters; independent policy belongs in services.
    public partial class WpfLabelingShellWindow
    {
        #region ClassCatalog
        public void FocusClassCatalogTab()
        {
            ShowClassCatalogWorkflowView(ShellViewModel?.IsDatasetStageActive == true
                ? WpfShellWorkflowStage.Dataset
                : WpfShellWorkflowStage.Labeling);
            PopulateClassList(GetSelectedClassName());
            UpdateLayout();
        }

        private void ClassNameBox_KeyDown(object sender, KeyInputCommandArgs e)
        {
            if (e?.Key == Key.Enter)
            {
                ExecuteAddClassCommand();
                e.Handled = true;
            }
        }

        private void ExecuteAddClassCommand()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            WpfClassCatalogMutationResult result = classCatalogWorkflowService.Add(
                global.Data,
                global.Recipe.Name,
                ClassCatalogViewModel?.ClassName);
            if (!result.IsSuccess)
            {
                if (result.Failure == WpfClassCatalogOperationFailure.Persistence)
                {
                    PopulateClassList(GetSelectedClassName());
                }

                SetClassEditStatus(result.Failure switch
                {
                    WpfClassCatalogOperationFailure.InvalidClassName => "\uC0C8 \uD074\uB798\uC2A4 \uC774\uB984\uC744 \uC785\uD558\uC138\uC694.",
                    WpfClassCatalogOperationFailure.Persistence => string.Format("\uD074\uB798\uC2A4 \uC800\uC7A5 \uC2E4\uD328: {0}", result.ErrorMessage),
                    _ => string.Format("\uC774\uBBF8 \uC874\uC7AC\uD558\uAC70\uB098 \uC0AC\uC6A9\uD560 \uC218 \uC5C6\uB294 \uD074\uB798\uC2A4 \uC774\uB984\uC785\uB2C8\uB2E4: {0}", result.ClassName)
                });
                return;
            }

            RefreshClassCatalogPersistencePresentation();
            PopulateClassList(result.ClassItem.Text);
            ClassCatalogViewModel?.ClearClassName();
            ClassNameBox?.Focus();
            SetClassEditStatus(string.Format("\uD074\uB798\uC2A4 \uCD94\uAC00: {0}", result.ClassItem.Text));
        }

        private void ExecuteRenameClassCommand()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            string currentName = GetSelectedClassName();
            if (string.IsNullOrWhiteSpace(currentName))
            {
                SetClassEditStatus("\uD074\uB798\uC2A4\uB97C \uC120\uD0DD\uD558\uC138\uC694.");
                return;
            }

            WpfClassCatalogMutationResult result = classCatalogWorkflowService.Rename(
                global.Data,
                global.Recipe.Name,
                currentName,
                ClassCatalogViewModel?.ClassName);
            if (!result.IsSuccess)
            {
                if (result.Failure == WpfClassCatalogOperationFailure.Persistence)
                {
                    PopulateClassList(currentName);
                    RefreshObjectList();
                    RedrawReviewRois();
                }

                SetClassEditStatus(result.Failure switch
                {
                    WpfClassCatalogOperationFailure.InvalidClassName => "\uC0C8 \uD074\uB798\uC2A4 \uC774\uB984\uC744 \uC785\uD558\uC138\uC694.",
                    WpfClassCatalogOperationFailure.ArchivedClass => "\uBCF4\uAD00\uB41C \uD074\uB798\uC2A4\uB294 \uBA3C\uC800 \uBCF5\uC6D0\uD55C \uB4A4 \uC774\uB984\uC744 \uBC14\uAFC0 \uC218 \uC788\uC2B5\uB2C8\uB2E4.",
                    WpfClassCatalogOperationFailure.Persistence => string.Format("\uD074\uB798\uC2A4 \uC800\uC7A5 \uC2E4\uD328: {0}", result.ErrorMessage),
                    _ => string.Format("\uC774\uBBF8 \uC874\uC7AC\uD558\uAC70\uB098 \uC0AC\uC6A9\uD560 \uC218 \uC5C6\uB294 \uD074\uB798\uC2A4 \uC774\uB984\uC785\uB2C8\uB2E4: {0}", result.ClassName)
                });
                return;
            }

            RenameActiveAnnotationClasses(result.PreviousClassName, result.ClassItem.Text, result.ClassItem);
            RefreshClassCatalogPersistencePresentation();
            PopulateClassList(result.ClassItem.Text);
            RefreshObjectList();
            RedrawReviewRois();
            SetClassEditStatus(string.Format("\uD074\uB798\uC2A4 \uC774\uB984 \uBCC0\uACBD: {0} -> {1}", currentName, result.ClassItem.Text));
        }

        private void ExecuteArchiveClassCommand()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            string className = GetSelectedClassName();
            if (string.IsNullOrWhiteSpace(className))
            {
                SetClassEditStatus("\uD074\uB798\uC2A4\uB97C \uC120\uD0DD\uD558\uC138\uC694.");
                return;
            }

            WpfClassCatalogMutationResult result = classCatalogWorkflowService.ToggleArchive(
                global.Data,
                global.Recipe.Name,
                className);
            if (!result.IsSuccess)
            {
                if (result.Failure == WpfClassCatalogOperationFailure.Persistence)
                {
                    PopulateClassList(className);
                }

                SetClassEditStatus(result.Failure switch
                {
                    WpfClassCatalogOperationFailure.LastActiveClass => "\uCD5C\uC18C 1\uAC1C\uC758 \uD65C\uC131 \uD074\uB798\uC2A4\uB294 \uC720\uC9C0\uD574\uC57C \uD569\uB2C8\uB2E4.",
                    WpfClassCatalogOperationFailure.Persistence => string.Format("\uD074\uB798\uC2A4 \uC800\uC7A5 \uC2E4\uD328: {0}", result.ErrorMessage),
                    _ => string.Format(
                        result.Failure == WpfClassCatalogOperationFailure.ClassNotFound && global.Data?.ClassNamedList?.Any(item =>
                            string.Equals(item?.Text, className, StringComparison.OrdinalIgnoreCase) && item.IsArchived) == true
                            ? "\uBCF5\uC6D0\uD560 \uD074\uB798\uC2A4\uB97C \uCC3E\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4: {0}"
                            : "\uD074\uB798\uC2A4\uB97C \uCC3E\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4: {0}",
                        className)
                });
                return;
            }

            RefreshClassCatalogPersistencePresentation();
            PopulateClassList(result.WasArchived ? className : string.Empty);
            if (!result.WasArchived)
            {
                ClassCatalogViewModel?.ClearClassName();
            }

            SetClassEditStatus(string.Format(
                result.WasArchived ? "\uD074\uB798\uC2A4 \uBCF5\uC6D0: {0}" : "\uD074\uB798\uC2A4 \uBCF4\uAD00: {0}",
                className));
        }

        private void ExecuteApplyClassColorCommand()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            string className = GetSelectedClassName();
            if (string.IsNullOrWhiteSpace(className))
            {
                SetClassEditStatus("\uD074\uB798\uC2A4\uB97C \uC120\uD0DD\uD558\uC138\uC694.");
                return;
            }

            DrawingColor color = ClassCatalogViewModel?.SelectedColorPreset?.Color ?? DrawingColor.LimeGreen;
            WpfClassCatalogMutationResult result = classCatalogWorkflowService.SetColor(
                global.Data,
                global.Recipe.Name,
                className,
                color);
            if (!result.IsSuccess)
            {
                if (result.Failure == WpfClassCatalogOperationFailure.Persistence)
                {
                    PopulateClassList(className);
                    RefreshObjectList();
                    RedrawReviewRois();
                }

                SetClassEditStatus(result.Failure == WpfClassCatalogOperationFailure.Persistence
                    ? string.Format("\uD074\uB798\uC2A4 \uC800\uC7A5 \uC2E4\uD328: {0}", result.ErrorMessage)
                    : string.Format("\uC0AD\uC81C\uD560 \uD074\uB798\uC2A4\uB97C \uCC3E\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4: {0}", className));
                return;
            }

            RenameActiveAnnotationClasses(result.ClassItem.Text, result.ClassItem.Text, result.ClassItem);
            RefreshClassCatalogPersistencePresentation();
            PopulateClassList(result.ClassItem.Text);
            RefreshObjectList();
            RedrawReviewRois();
            SetClassEditStatus(string.Format("\uD074\uB798\uC2A4 \uC0C9\uC0C1 \uBCC0\uACBD: {0}", result.ClassItem.Text));
        }

        private void ClassListBox_SelectionChanged(object sender, object selectedItem)
        {
            CancelFourPointBoxDraft(updateStatus: false);
            WpfClassCatalogListItem selectedClass = selectedItem as WpfClassCatalogListItem;
            string className = selectedClass?.Text ?? GetSelectedClassName();
            if (string.IsNullOrWhiteSpace(className))
            {
                return;
            }

            if (ClassCatalogViewModel != null)
            {
                ClassCatalogViewModel.ClassName = className;
            }

            if (selectedClass?.IsArchived == true)
            {
                SetClassEditStatus(string.Format("\uBCF4\uAD00\uB41C \uD074\uB798\uC2A4: {0}. \uC0C8 \uB77C\uBCA8\uC5D0\uB294 \uC0AC\uC6A9\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.", className));
                return;
            }

            CanvasPanelViewModel?.SelectLabelClass(className);
            RefreshObjectClassOptions(className);
        }

        private void CanvasLabelClass_SelectionChanged(object sender, object selectedItem)
        {
            CancelFourPointBoxDraft(updateStatus: false);
            string className = (selectedItem as WpfCanvasLabelClassItem)?.Text
                ?? CanvasPanelViewModel?.SelectedLabelClass?.Text;
            if (string.IsNullOrWhiteSpace(className))
            {
                return;
            }

            // The drawing code reads the selected class from the catalog. Keep the
            // always-visible canvas chips and the project class catalog on one source of truth.
            ClassCatalogViewModel?.SelectClass(className);
            CanvasPanelViewModel?.SelectLabelClass(className);
            RefreshObjectClassOptions(className);
        }

        private void ExecuteBrowseOutputRootCommand()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            if (TryPickFolder("\uB370\uC774\uD130\uC14B \uCD9C\uB825 \uD3F4\uB354 \uC120\uD0DD", ClassCatalogViewModel?.OutputRootPath, out string selectedPath))
            {
                if (isApplicationCloseApproved)
                {
                    return;
                }

                if (ClassCatalogViewModel != null)
                {
                    ClassCatalogViewModel.OutputRootPath = selectedPath;
                }

                SaveOutputRootFromEditor();
            }
        }

        private void ExecuteSaveOutputRootCommand()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            SaveOutputRootFromEditor();
        }

        private void PopulateClassList(string selectedName = "")
        {
            PopulateClassCatalogFields();
            if (global.Data.ClassNamedList == null
                || !global.Data.ClassNamedList.Any(ClassCatalogService.IsActiveClass))
            {
                classCatalogWorkflowService.EnsureClassItem(global.Data, "Defect");
            }

            List<LabelClass> classItems = global.Data.ClassNamedList
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Text))
                .ToList();

            string effectiveSelectedName = ClassCatalogService.NormalizeClassName(selectedName);
            if (string.IsNullOrWhiteSpace(effectiveSelectedName))
            {
                effectiveSelectedName = GetSelectedClassName();
            }

            List<LabelClass> activeClassItems = classItems
                .Where(ClassCatalogService.IsActiveClass)
                .ToList();
            if (string.IsNullOrWhiteSpace(effectiveSelectedName))
            {
                effectiveSelectedName = activeClassItems.FirstOrDefault()?.Text
                    ?? classItems.FirstOrDefault()?.Text
                    ?? string.Empty;
            }
            else if (!classItems.Any(item => string.Equals(item.Text, effectiveSelectedName, StringComparison.OrdinalIgnoreCase)))
            {
                effectiveSelectedName = activeClassItems.FirstOrDefault()?.Text
                    ?? classItems.FirstOrDefault()?.Text
                    ?? string.Empty;
            }

            string drawingSelectedName = activeClassItems.Any(item =>
                string.Equals(item.Text, effectiveSelectedName, StringComparison.OrdinalIgnoreCase))
                ? effectiveSelectedName
                : activeClassItems.FirstOrDefault()?.Text ?? string.Empty;
            ClassCatalogViewModel?.SetClasses(classItems, effectiveSelectedName);
            CanvasPanelViewModel?.SetLabelClasses(classItems, drawingSelectedName);

            RefreshObjectClassOptions(drawingSelectedName);
            RefreshYoloTrainingStepCompletion();
        }

        private void PopulateClassCatalogFields()
        {
            EnsureProjectSettings();
            global.Data.NormalizeOutputPaths();
            ClassCatalogViewModel?.LoadOutputRoot(global.Data.OutputRootPath);
        }

        private string GetSelectedClassName()
        {
            if (ClassCatalogViewModel?.SelectedClass != null)
            {
                return ClassCatalogViewModel.SelectedClass.Text;
            }

            return string.Empty;
        }

        private void RenameActiveAnnotationClasses(string oldName, string newName, LabelClass classItem)
        {
            string normalizedOldName = ClassCatalogService.NormalizeClassName(oldName);
            string normalizedNewName = ClassCatalogService.NormalizeClassName(newName);
            if (string.IsNullOrWhiteSpace(normalizedOldName)
                || string.IsNullOrWhiteSpace(normalizedNewName))
            {
                return;
            }

            // Class catalog edits are project-level. Keep already drawn objects on the current image aligned
            // so a rename from Defect to NG does not leave stale labels in Object Review.
            for (int i = 0; i < manualRoiClassNames.Count; i++)
            {
                if (string.Equals(manualRoiClassNames[i], normalizedOldName, StringComparison.OrdinalIgnoreCase))
                {
                    manualRoiClassNames[i] = normalizedNewName;
                }
            }

            foreach (LabelingSegmentationObject segment in manualSegments)
            {
                if (segment == null)
                {
                    continue;
                }

                bool matches = string.Equals(segment.ClassName, normalizedOldName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(segment.ClassItem?.Text, normalizedOldName, StringComparison.OrdinalIgnoreCase);
                if (matches)
                {
                    segment.ClassName = normalizedNewName;
                    segment.ClassItem = classItem;
                }
            }

            foreach (var candidate in confirmedDetectionCandidates)
            {
                if (string.Equals(candidate?.ClassName, normalizedOldName, StringComparison.OrdinalIgnoreCase))
                {
                    candidate.ClassName = normalizedNewName;
                }
            }
        }

        private void SetClassEditStatus(string message)
        {
            if (ClassCatalogViewModel != null)
            {
                ClassCatalogViewModel.StatusText = message;
            }
        }

        private void RefreshClassCatalogPersistencePresentation()
        {
            PopulateClassCatalogFields();
            PopulateProjectConfigPanelFields();
        }

        private void SaveOutputRootFromEditor()
        {
            string outputRootPath = (ClassCatalogViewModel?.OutputRootPath ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(outputRootPath))
            {
                SetClassEditStatus("\uC800\uC7A5 \uACBD\uB85C\uB97C \uC785\uB825\uD558\uAC70\uB098 \uC120\uD0DD\uD558\uC138\uC694.");
                return;
            }

            if (annotationDirtyState.IsDirty)
            {
                SetClassEditStatus("\uC800\uC7A5\uD558\uC9C0 \uC54A\uC740 \uB77C\uBCA8\uC774 \uC788\uC2B5\uB2C8\uB2E4. \uBA3C\uC800 \uB77C\uBCA8\uC744 \uC800\uC7A5\uD55C \uB4A4 \uC800\uC7A5 \uACBD\uB85C\uB97C \uBC14\uAFB8\uC138\uC694.");
                return;
            }

            WpfClassCatalogOutputRootResult result = classCatalogWorkflowService.SaveOutputRoot(
                global.Data,
                global.Recipe.Name,
                outputRootPath);
            if (!result.IsSuccess)
            {
                PopulateClassCatalogFields();
                SetClassEditStatus(string.Format("\uC800\uC7A5 \uACBD\uB85C \uC801\uC6A9 \uC2E4\uD328: {0}", result.ErrorMessage));
                AppendLog(string.Format("\uB370\uC774\uD130\uC14B \uCD9C\uB825 \uACBD\uB85C \uC800\uC7A5 \uC2E4\uD328: {0}", result.ErrorMessage));
                return;
            }

            RefreshClassCatalogPersistencePresentation();
            ReloadActiveImageAnnotationsAfterOutputRootChange(
                result.PreviousOutputRootPath,
                result.OutputRootPath);
            RefreshTrainingReadinessPanel(refreshYaml: false);
            SetDatasetStatus(string.Format("\uB370\uC774\uD130\uC14B: \uCD9C\uB825 \uACBD\uB85C {0}", result.OutputRootPath));
            SetClassEditStatus(string.Format("\uC800\uC7A5 \uACBD\uB85C \uC801\uC6A9: {0} / \uD074\uB798\uC2A4\uB294 \uB808\uC2DC\uD53C\uC5D0 \uC720\uC9C0\uB418\uACE0, \uD604\uC7AC \uC774\uBBF8\uC9C0\uB294 \uC0C8 \uACBD\uB85C\uC758 \uB77C\uBCA8 \uAE30\uC900\uC73C\uB85C \uB2E4\uC2DC \uD655\uC778\uD588\uC2B5\uB2C8\uB2E4.", result.OutputRootPath));
            AppendLog(string.Format("\uB370\uC774\uD130\uC14B \uCD9C\uB825 \uACBD\uB85C \uC800\uC7A5: {0}", result.OutputRootPath));
        }

        private void ReloadActiveImageAnnotationsAfterOutputRootChange(string previousOutputRootPath, string currentOutputRootPath)
        {
            if (string.IsNullOrWhiteSpace(activeImagePath)
                || activeImageBitmap == null
                || activeImageSize.IsEmpty
                || DatasetSetupPathService.PathsEqual(previousOutputRootPath, currentOutputRootPath))
            {
                return;
            }

            TryLoadImage(
                activeImagePath,
                populateQueue: false,
                refreshQueueDetails: true,
                refreshActiveStatus: true,
                appendLoadLog: false);
        }
        #endregion

    }
}
