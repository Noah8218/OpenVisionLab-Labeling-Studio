using MvcVisionSystem.Yolo;
using OpenVisionLab.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using DrawingColor = System.Drawing.Color;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
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
            string className = ClassCatalogService.NormalizeClassName(ClassCatalogViewModel?.ClassName);
            if (string.IsNullOrWhiteSpace(className))
            {
                SetClassEditStatus("\uC0C8 \uD074\uB798\uC2A4 \uC774\uB984\uC744 \uC785\uB825\uD558\uC138\uC694.");
                return;
            }

            if (!ClassCatalogService.TryAddClass(global.Data, className, out CClassItem addedClass))
            {
                SetClassEditStatus(string.Format("\uC774\uBBF8 \uC874\uC7AC\uD558\uAC70\uB098 \uC0AC\uC6A9\uD560 \uC218 \uC5C6\uB294 \uD074\uB798\uC2A4 \uC774\uB984\uC785\uB2C8\uB2E4: {0}", className));
                return;
            }

            SaveClassCatalog();
            PopulateClassList(addedClass.Text);
            ClassCatalogViewModel?.ClearClassName();
            ClassNameBox?.Focus();
            SetClassEditStatus(string.Format("\uD074\uB798\uC2A4 \uCD94\uAC00: {0}", addedClass.Text));
        }

        private void ExecuteRenameClassCommand()
        {
            string currentName = GetSelectedClassName();
            if (string.IsNullOrWhiteSpace(currentName))
            {
                SetClassEditStatus("\uD074\uB798\uC2A4\uB97C \uC120\uD0DD\uD558\uC138\uC694.");
                return;
            }

            string newName = ClassCatalogService.NormalizeClassName(ClassCatalogViewModel?.ClassName);
            if (string.IsNullOrWhiteSpace(newName))
            {
                SetClassEditStatus("\uC0C8 \uD074\uB798\uC2A4 \uC774\uB984\uC744 \uC785\uB825\uD558\uC138\uC694.");
                return;
            }

            if (!ClassCatalogService.TryRenameClass(global.Data, currentName, newName, out CClassItem renamedClass))
            {
                SetClassEditStatus(string.Format("\uC774\uBBF8 \uC874\uC7AC\uD558\uAC70\uB098 \uC0AC\uC6A9\uD560 \uC218 \uC5C6\uB294 \uD074\uB798\uC2A4 \uC774\uB984\uC785\uB2C8\uB2E4: {0}", newName));
                return;
            }

            RenameActiveAnnotationClasses(currentName, renamedClass.Text, renamedClass);
            SaveClassCatalog();
            PopulateClassList(renamedClass.Text);
            RefreshObjectList();
            RedrawReviewRois();
            SetClassEditStatus(string.Format("\uD074\uB798\uC2A4 \uC774\uB984 \uBCC0\uACBD: {0} -> {1}", currentName, renamedClass.Text));
        }

        private void ExecuteRemoveClassCommand()
        {
            string className = GetSelectedClassName();
            if (string.IsNullOrWhiteSpace(className))
            {
                SetClassEditStatus("\uC0AD\uC81C\uD560 \uD074\uB798\uC2A4\uB97C \uC120\uD0DD\uD558\uC138\uC694.");
                return;
            }

            int editableClassCount = global.Data?.ClassNamedList?
                .Count(item => item != null && !string.IsNullOrWhiteSpace(item.Text)) ?? 0;
            if (editableClassCount <= 1)
            {
                SetClassEditStatus("\uCD5C\uC18C 1\uAC1C \uD074\uB798\uC2A4\uB294 \uD544\uC694\uD569\uB2C8\uB2E4. \uBA3C\uC800 \uC0C8 \uC774\uB984\uC73C\uB85C \uBCC0\uACBD\uD558\uAC70\uB098 \uB2E4\uB978 \uD074\uB798\uC2A4\uB97C \uCD94\uAC00\uD558\uC138\uC694.");
                return;
            }

            if (!ClassCatalogService.RemoveClass(global.Data, className))
            {
                SetClassEditStatus(string.Format("\uC0AD\uC81C\uD560 \uD074\uB798\uC2A4\uB97C \uCC3E\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4: {0}", className));
                return;
            }

            SaveClassCatalog();
            PopulateClassList();
            ClassCatalogViewModel?.ClearClassName();
            SetClassEditStatus(string.Format("\uD074\uB798\uC2A4 \uC0AD\uC81C: {0}", className));
        }

        private void ExecuteApplyClassColorCommand()
        {
            string className = GetSelectedClassName();
            if (string.IsNullOrWhiteSpace(className))
            {
                SetClassEditStatus("\uD074\uB798\uC2A4\uB97C \uC120\uD0DD\uD558\uC138\uC694.");
                return;
            }

            DrawingColor color = ClassCatalogViewModel?.SelectedColorPreset?.Color ?? DrawingColor.LimeGreen;
            if (!ClassCatalogService.TrySetClassColor(global.Data, className, color, out CClassItem classItem))
            {
                SetClassEditStatus(string.Format("\uC0AD\uC81C\uD560 \uD074\uB798\uC2A4\uB97C \uCC3E\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4: {0}", className));
                return;
            }

            RenameActiveAnnotationClasses(classItem.Text, classItem.Text, classItem);
            SaveClassCatalog();
            PopulateClassList(classItem.Text);
            RefreshObjectList();
            RedrawReviewRois();
            SetClassEditStatus(string.Format("\uD074\uB798\uC2A4 \uC0C9\uC0C1 \uBCC0\uACBD: {0}", classItem.Text));
        }

        private void ClassListBox_SelectionChanged(object sender, object selectedItem)
        {
            string className = (selectedItem as WpfClassCatalogListItem)?.Text ?? GetSelectedClassName();
            if (string.IsNullOrWhiteSpace(className))
            {
                return;
            }

            if (ClassCatalogViewModel != null)
            {
                ClassCatalogViewModel.ClassName = className;
            }

            CanvasPanelViewModel?.SelectLabelClass(className);
            RefreshObjectClassOptions(className);
        }

        private void CanvasLabelClass_SelectionChanged(object sender, object selectedItem)
        {
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
            if (TryPickFolder("\uB370\uC774\uD130\uC14B \uCD9C\uB825 \uD3F4\uB354 \uC120\uD0DD", ClassCatalogViewModel?.OutputRootPath, out string selectedPath))
            {
                if (ClassCatalogViewModel != null)
                {
                    ClassCatalogViewModel.OutputRootPath = selectedPath;
                }

                SaveOutputRootFromEditor();
            }
        }

        private void ExecuteSaveOutputRootCommand()
        {
            SaveOutputRootFromEditor();
        }

        private CClassItem EnsureClassItem(string className)
        {
            global.Data.ClassNamedList ??= new List<CClassItem>();
            string normalizedName = ClassCatalogService.NormalizeClassName(className);
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                normalizedName = "Defect";
            }

            CClassItem existing = global.Data.ClassNamedList
                .FirstOrDefault(item => string.Equals(item.Text, normalizedName, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                return existing;
            }

            if (ClassCatalogService.TryAddClass(global.Data, normalizedName, out CClassItem added))
            {
                return added;
            }

            return new CClassItem
            {
                Text = normalizedName,
                DrawColor = DrawingColor.FromArgb(34, 197, 94)
            };
        }

        private void PopulateClassList(string selectedName = "")
        {
            PopulateClassCatalogFields();
            if (global.Data.ClassNamedList == null
                || !global.Data.ClassNamedList.Any(item => item != null && !string.IsNullOrWhiteSpace(item.Text)))
            {
                EnsureClassItem("Defect");
            }

            List<CClassItem> classItems = global.Data.ClassNamedList
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Text))
                .OrderBy(item => item.Text, StringComparer.OrdinalIgnoreCase)
                .ToList();

            string effectiveSelectedName = ClassCatalogService.NormalizeClassName(selectedName);
            if (string.IsNullOrWhiteSpace(effectiveSelectedName))
            {
                effectiveSelectedName = GetSelectedClassName();
            }

            if (string.IsNullOrWhiteSpace(effectiveSelectedName))
            {
                effectiveSelectedName = classItems.FirstOrDefault()?.Text ?? string.Empty;
            }
            else if (!classItems.Any(item => string.Equals(item.Text, effectiveSelectedName, StringComparison.OrdinalIgnoreCase)))
            {
                effectiveSelectedName = classItems.FirstOrDefault()?.Text ?? string.Empty;
            }

            ClassCatalogViewModel?.SetClasses(classItems, effectiveSelectedName);
            CanvasPanelViewModel?.SetLabelClasses(classItems, effectiveSelectedName);

            RefreshObjectClassOptions(effectiveSelectedName);
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

        private void RenameActiveAnnotationClasses(string oldName, string newName, CClassItem classItem)
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

        private void SaveClassCatalog()
        {
            global.Data.SaveYoloDataYaml();
            global.Data.SaveConfig(global.Recipe.Name);
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

            try
            {
                if (!string.IsNullOrWhiteSpace(annotationDirtyReason))
                {
                    SetClassEditStatus("\uC800\uC7A5\uD558\uC9C0 \uC54A\uC740 \uB77C\uBCA8\uC774 \uC788\uC2B5\uB2C8\uB2E4. \uBA3C\uC800 \uB77C\uBCA8\uC744 \uC800\uC7A5\uD55C \uB4A4 \uC800\uC7A5 \uACBD\uB85C\uB97C \uBC14\uAFB8\uC138\uC694.");
                    return;
                }

                string previousOutputRootPath = global.Data?.OutputRootPath ?? string.Empty;
                global.Data.ConfigureOutputRoot(outputRootPath);
                SaveClassCatalog();
                ReloadActiveImageAnnotationsAfterOutputRootChange(previousOutputRootPath, global.Data.OutputRootPath);
                RefreshTrainingReadinessPanel(refreshYaml: false);
                SetDatasetStatus(string.Format("\uB370\uC774\uD130\uC14B: \uCD9C\uB825 \uACBD\uB85C {0}", global.Data.OutputRootPath));
                SetClassEditStatus(string.Format("\uC800\uC7A5 \uACBD\uB85C \uC801\uC6A9: {0} / \uD074\uB798\uC2A4\uB294 \uB808\uC2DC\uD53C\uC5D0 \uC720\uC9C0\uB418\uACE0, \uD604\uC7AC \uC774\uBBF8\uC9C0\uB294 \uC0C8 \uACBD\uB85C\uC758 \uB77C\uBCA8 \uAE30\uC900\uC73C\uB85C \uB2E4\uC2DC \uD655\uC778\uD588\uC2B5\uB2C8\uB2E4.", global.Data.OutputRootPath));
                AppendLog(string.Format("\uB370\uC774\uD130\uC14B \uCD9C\uB825 \uACBD\uB85C \uC800\uC7A5: {0}", global.Data.OutputRootPath));
            }
            catch (Exception ex)
            {
                SetClassEditStatus(string.Format("\uC800\uC7A5 \uACBD\uB85C \uC801\uC6A9 \uC2E4\uD328: {0}", ex.Message));
                AppendLog(string.Format("\uB370\uC774\uD130\uC14B \uCD9C\uB825 \uACBD\uB85C \uC800\uC7A5 \uC2E4\uD328: {0}", ex.Message));
            }
        }

        private void ReloadActiveImageAnnotationsAfterOutputRootChange(string previousOutputRootPath, string currentOutputRootPath)
        {
            if (string.IsNullOrWhiteSpace(activeImagePath)
                || activeImageBitmap == null
                || activeImageSize.IsEmpty
                || PathsEqual(previousOutputRootPath, currentOutputRootPath))
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
    }
}
