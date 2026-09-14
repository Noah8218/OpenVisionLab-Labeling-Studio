using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using OpenVisionLab.ImageCanvas.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace MvcVisionSystem
{
    // WPF adapter for class catalog navigation, projection, and output-root UI.
    // Class mutation policy and persistence remain owned by the existing
    // ClassCatalogWorkflowService and WpfClassCatalogPanelViewModel.
    internal sealed class ClassCatalogWorkflowAdapter
    {
        private readonly ClassCatalogWorkflowAdapterContext context;

        internal ClassCatalogWorkflowAdapter(ClassCatalogWorkflowAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        #region ClassCatalog
        internal void FocusClassCatalogTab()
        {
            context.ShowClassCatalogWorkflowView?.Invoke(context.IsDatasetStageActive?.Invoke() == true
                ? WpfShellWorkflowStage.Dataset
                : WpfShellWorkflowStage.Labeling);
            PopulateClassList(GetSelectedClassName());
            context.UpdateLayout?.Invoke();
        }

        internal void HandleClassCatalogMutationCompleted(
            WpfClassCatalogMutationKind kind,
            WpfClassCatalogMutationResult result)
        {
            if (result == null)
            {
                return;
            }

            if (!result.IsSuccess)
            {
                if (result.Failure == WpfClassCatalogOperationFailure.Persistence)
                {
                    PopulateClassList(result.ClassName);
                    if (kind == WpfClassCatalogMutationKind.Rename
                        || kind == WpfClassCatalogMutationKind.SetColor)
                    {
                        context.RefreshObjectList?.Invoke();
                        context.RedrawReviewRois?.Invoke();
                    }
                }

                return;
            }

            switch (kind)
            {
                case WpfClassCatalogMutationKind.Add:
                    RefreshClassCatalogPersistencePresentation();
                    PopulateClassList(result.ClassItem?.Text);
                    context.ClassCatalogViewModel?.ClearClassName();
                    context.ClassNameBox?.Focus();
                    break;
                case WpfClassCatalogMutationKind.Rename:
                    RenameActiveAnnotationClasses(result.PreviousClassName, result.ClassItem?.Text, result.ClassItem);
                    RefreshClassCatalogPersistencePresentation();
                    PopulateClassList(result.ClassItem?.Text);
                    context.RefreshObjectList?.Invoke();
                    context.RedrawReviewRois?.Invoke();
                    break;
                case WpfClassCatalogMutationKind.ToggleArchive:
                    RefreshClassCatalogPersistencePresentation();
                    PopulateClassList(result.WasArchived ? result.ClassName : string.Empty);
                    if (!result.WasArchived)
                    {
                        context.ClassCatalogViewModel?.ClearClassName();
                    }

                    break;
                case WpfClassCatalogMutationKind.SetColor:
                    RenameActiveAnnotationClasses(result.ClassItem?.Text, result.ClassItem?.Text, result.ClassItem);
                    RefreshClassCatalogPersistencePresentation();
                    PopulateClassList(result.ClassItem?.Text);
                    context.RefreshObjectList?.Invoke();
                    context.RedrawReviewRois?.Invoke();
                    break;
            }
        }

        internal void ExecuteBrowseOutputRootCommand()
        {
            if (context.IsApplicationCloseApproved?.Invoke() == true)
            {
                return;
            }

            string selectedPath = context.SelectOutputRootFolder?.Invoke(
                "데이터셋 출력 폴더 선택",
                context.ClassCatalogViewModel?.OutputRootPath);
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return;
            }

            if (context.IsApplicationCloseApproved?.Invoke() == true)
            {
                return;
            }

            if (context.ClassCatalogViewModel != null)
            {
                context.ClassCatalogViewModel.OutputRootPath = selectedPath;
            }

            SaveOutputRootFromEditor();
        }

        internal void ExecuteSaveOutputRootCommand()
        {
            if (context.IsApplicationCloseApproved?.Invoke() == true)
            {
                return;
            }

            SaveOutputRootFromEditor();
        }

        internal void PopulateClassList(string selectedName = "")
        {
            PopulateClassCatalogFields();
            LabelingProjectData data = context.DataProvider?.Invoke();
            if (data?.ClassNamedList == null
                || !data.ClassNamedList.Any(ClassCatalogService.IsActiveClass))
            {
                context.ClassCatalogWorkflowService?.EnsureClassItem(data, "Defect");
            }

            List<LabelClass> classItems = data?.ClassNamedList?
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Text))
                .ToList()
                ?? new List<LabelClass>();
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
            context.ClassCatalogViewModel?.SetClasses(classItems, effectiveSelectedName);
            context.CanvasPanelViewModel?.SetLabelClasses(classItems, drawingSelectedName);

            context.RefreshObjectClassOptions?.Invoke(drawingSelectedName);
            context.RefreshTrainingStepCompletion?.Invoke();
        }

        internal string GetSelectedClassName()
        {
            return context.ClassCatalogViewModel?.SelectedClass?.Text ?? string.Empty;
        }

        private void PopulateClassCatalogFields()
        {
            LabelingProjectData data = context.DataProvider?.Invoke();
            if (data == null)
            {
                return;
            }

            context.EnsureProjectSettings?.Invoke();
            data.NormalizeOutputPaths();
            context.ClassCatalogViewModel?.LoadOutputRoot(data.OutputRootPath);
        }

        private void RenameActiveAnnotationClasses(string oldName, string newName, LabelClass classItem)
        {
            context.AnnotationClassRenameService?.Rename(
                oldName,
                newName,
                classItem,
                context.ManualRoiClassNames,
                context.ManualSegments,
                context.ConfirmedDetectionCandidates);
        }

        private void SetClassEditStatus(string message)
        {
            if (context.ClassCatalogViewModel != null)
            {
                context.ClassCatalogViewModel.StatusText = message;
            }
        }

        private void RefreshClassCatalogPersistencePresentation()
        {
            PopulateClassCatalogFields();
            context.PopulateProjectConfigPanelFields?.Invoke();
        }

        private void SaveOutputRootFromEditor()
        {
            string outputRootPath = (context.ClassCatalogViewModel?.OutputRootPath ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(outputRootPath))
            {
                SetClassEditStatus("저장 경로를 입력하거나 선택하세요.");
                return;
            }

            if (context.AnnotationDirtyState?.IsDirty == true)
            {
                SetClassEditStatus("저장하지 않은 라벨이 있습니다. 먼저 라벨을 저장한 뒤 저장 경로를 바꾸세요.");
                return;
            }

            WpfClassCatalogOutputRootResult result = context.ClassCatalogWorkflowService?.SaveOutputRoot(
                context.DataProvider?.Invoke(),
                context.RecipeNameProvider?.Invoke(),
                outputRootPath);
            if (result == null || !result.IsSuccess)
            {
                PopulateClassCatalogFields();
                SetClassEditStatus(string.Format("저장 경로 적용 실패: {0}", result?.ErrorMessage));
                context.AppendLog?.Invoke(string.Format("데이터셋 출력 경로 저장 실패: {0}", result?.ErrorMessage));
                return;
            }

            RefreshClassCatalogPersistencePresentation();
            if (context.ShouldReloadActiveImage?.Invoke(result.PreviousOutputRootPath, result.OutputRootPath) == true)
            {
                context.ReloadActiveImageAnnotations?.Invoke();
            }

            context.RefreshTrainingReadiness?.Invoke();
            context.SetDatasetStatus?.Invoke(string.Format("데이터셋: 출력 경로 {0}", result.OutputRootPath));
            SetClassEditStatus(string.Format("저장 경로 적용: {0} / 클래스는 레시피에 유지되고, 현재 이미지는 새 경로의 라벨 기준으로 다시 확인했습니다.", result.OutputRootPath));
            context.AppendLog?.Invoke(string.Format("데이터셋 출력 경로 저장: {0}", result.OutputRootPath));
        }
        #endregion
    }

    internal sealed class ClassCatalogWorkflowAdapterContext
    {
        internal Func<LabelingProjectData> DataProvider { get; init; }
        internal WpfClassCatalogPanelViewModel ClassCatalogViewModel { get; init; }
        internal WpfCanvasPanelViewModel CanvasPanelViewModel { get; init; }
        internal TextBox ClassNameBox { get; init; }
        internal ClassCatalogWorkflowService ClassCatalogWorkflowService { get; init; }
        internal AnnotationClassRenameService AnnotationClassRenameService { get; init; }
        internal IList<string> ManualRoiClassNames { get; init; }
        internal IList<LabelingSegmentationObject> ManualSegments { get; init; }
        internal IEnumerable<YoloWorkerSmokeCandidate> ConfirmedDetectionCandidates { get; init; }
        internal AnnotationDirtyState AnnotationDirtyState { get; init; }
        internal Func<bool> IsApplicationCloseApproved { get; init; }
        internal Func<bool> IsDatasetStageActive { get; init; }
        internal Action<WpfShellWorkflowStage> ShowClassCatalogWorkflowView { get; init; }
        internal Action UpdateLayout { get; init; }
        internal Func<string, string, string> SelectOutputRootFolder { get; init; }
        internal Action EnsureProjectSettings { get; init; }
        internal Action<string> RefreshObjectClassOptions { get; init; }
        internal Action RefreshObjectList { get; init; }
        internal Action RedrawReviewRois { get; init; }
        internal Action RefreshTrainingReadiness { get; init; }
        internal Action PopulateProjectConfigPanelFields { get; init; }
        internal Func<string, string, bool> ShouldReloadActiveImage { get; init; }
        internal Action ReloadActiveImageAnnotations { get; init; }
        internal Action<string> SetDatasetStatus { get; init; }
        internal Action<string> AppendLog { get; init; }
        internal Func<string> RecipeNameProvider { get; init; }
        internal Action RefreshTrainingStepCompletion { get; init; }
    }
}
