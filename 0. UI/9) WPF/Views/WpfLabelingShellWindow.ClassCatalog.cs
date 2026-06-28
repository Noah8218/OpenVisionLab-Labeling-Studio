using Lib.Common;
using MvcVisionSystem.Yolo;
using OpenVisionLab.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        public void FocusClassCatalogTab()
        {
            ClassesReviewTab.IsSelected = true;
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
                SetClassEditStatus("클래스 이름을 입력하세요.");
                return;
            }

            if (!ClassCatalogService.TryAddClass(global.Data, className, out CClassItem addedClass))
            {
                SetClassEditStatus($"이미 있거나 추가할 수 없는 클래스입니다: {className}");
                return;
            }

            SaveClassCatalog();
            PopulateClassList(addedClass.Text);
            ClassCatalogViewModel?.ClearClassName();

            ClassNameBox?.Focus();

            SetClassEditStatus($"클래스 추가됨: {addedClass.Text}");
        }

        private void ExecuteRemoveClassCommand()
        {
            string className = GetSelectedClassName();
            if (string.IsNullOrWhiteSpace(className))
            {
                SetClassEditStatus("삭제할 클래스를 선택하세요.");
                return;
            }

            if (string.Equals(className, "Defect", StringComparison.OrdinalIgnoreCase))
            {
                SetClassEditStatus("기본 Defect 클래스는 삭제하지 않습니다.");
                return;
            }

            if (!ClassCatalogService.RemoveClass(global.Data, className))
            {
                SetClassEditStatus($"삭제할 클래스를 찾지 못했습니다: {className}");
                return;
            }

            SaveClassCatalog();
            PopulateClassList();
            ClassCatalogViewModel?.ClearClassName();
            SetClassEditStatus($"클래스 삭제됨: {className}");
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
        }

        private void ExecuteBrowseOutputRootCommand()
        {
            if (TryPickFolder("YOLO 데이터셋 출력 폴더 선택", ClassCatalogViewModel?.OutputRootPath, out string selectedPath))
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
                DrawColor = System.Drawing.Color.Green
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

            ClassCatalogViewModel?.SetClasses(classItems, selectedName);

            RefreshObjectClassOptions(selectedName);
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
                SetClassEditStatus("저장 경로를 입력하거나 선택하세요.");
                return;
            }

            try
            {
                global.Data.ConfigureOutputRoot(outputRootPath);
                SaveClassCatalog();
                RefreshTrainingReadinessPanel(refreshYaml: false);
                SetDatasetStatus($"데이터셋: 출력 경로 {global.Data.OutputRootPath}");
                SetClassEditStatus($"저장 경로 적용됨: {global.Data.OutputRootPath}");
                AppendLog($"YOLO 데이터셋 출력 경로 저장: {global.Data.OutputRootPath}");
            }
            catch (Exception ex)
            {
                SetClassEditStatus($"저장 경로 적용 실패: {ex.Message}");
                AppendLog($"YOLO 데이터셋 출력 경로 저장 실패: {ex.Message}");
            }
        }
    }
}
