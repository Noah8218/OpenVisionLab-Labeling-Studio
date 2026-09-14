using System;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

namespace MvcVisionSystem
{
    // Owns the Dataset Purpose transition between project state and the WPF panels.
    // Recipe persistence remains with ProjectRecipeSessionService; this adapter only
    // coordinates the existing state and presentation owners at the Shell boundary.
    internal sealed class DatasetPurposeWorkflowAdapter
    {
        private readonly DatasetPurposeWorkflowAdapterContext context;

        internal DatasetPurposeWorkflowAdapter(DatasetPurposeWorkflowAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            ArgumentNullException.ThrowIfNull(context.DataProvider);
            ArgumentNullException.ThrowIfNull(context.RecipeNameProvider);
        }

        internal void ApplyProjectDatasetPurposeToWorkflow()
        {
            context.EnsureProjectSettings?.Invoke();
            LabelingProjectData data = context.DataProvider?.Invoke();
            LabelingProjectSettings settings = data.ProjectSettings;
            context.CanvasPanelViewModel?.RestoreSmartMaskAutoContourMode(settings.SmartMaskAutoContourEnabled);
            context.RestoreBoxDrawingMethodFromProject?.Invoke();
            context.LearningWorkflowViewModel?.ApplyDatasetPurpose(settings.DatasetPurpose);
            RefreshPresentation();
            context.RefreshYoloTrainingStepCompletion?.Invoke();
        }

        internal void ApplyDatasetPurposeToCurrentProject(LabelingDatasetPurpose purpose)
            => ApplyDatasetPurposeToCurrentProjectCore(purpose, persistAfterBindingsSettle: false);

        internal void ApplyPersistedDatasetPurposeToCurrentProject(LabelingDatasetPurpose purpose)
            => ApplyDatasetPurposeToCurrentProjectCore(purpose, persistAfterBindingsSettle: true);

        private void ApplyDatasetPurposeToCurrentProjectCore(
            LabelingDatasetPurpose purpose,
            bool persistAfterBindingsSettle)
        {
            SynchronizeDatasetPurposeToCurrentProject(purpose);
            string recipeName = context.RecipeNameProvider?.Invoke() ?? string.Empty;

            // The Recipe session commits Data before it publishes the selected
            // Recipe identity, but a queued WPF SelectionChanged callback can
            // still carry the previous recipe's purpose. Reconcile at idle so
            // that stale UI state cannot overwrite the new Recipe.
            context.Dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(() => ApplyDatasetPurposeAfterBindingsSettle(
                    purpose,
                    recipeName,
                    persistAfterBindingsSettle)));
        }

        private void ApplyDatasetPurposeAfterBindingsSettle(
            LabelingDatasetPurpose purpose,
            string recipeName,
            bool persistAfterBindingsSettle)
        {
            if (context.IsApplicationCloseApproved?.Invoke() == true
                || context.ProjectRecipeApplyWorkflowService?.CanContinue != true)
            {
                return;
            }

            if (context.GetCurrentDatasetPurpose?.Invoke() != purpose)
            {
                SynchronizeDatasetPurposeToCurrentProject(purpose);
            }

            if (!persistAfterBindingsSettle
                || !string.Equals(context.RecipeNameProvider?.Invoke(), recipeName, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(recipeName))
            {
                return;
            }

            RecipeConfigurationSaveResult saveResult = context.ProjectRecipeSessionService.SaveConfiguration(
                context.DataProvider?.Invoke(),
                recipeName,
                updateYoloDataYaml: false,
                refreshDatasetVersion: true);
            if (!saveResult.IsSuccess)
            {
                context.AppendLog?.Invoke($"데이터셋 용도 저장 실패: {saveResult.ErrorMessage}");
            }
        }

        private void SynchronizeDatasetPurposeToCurrentProject(LabelingDatasetPurpose purpose)
        {
            context.CancelFourPointBoxDraft?.Invoke();
            context.EnsureProjectSettings?.Invoke();
            LabelingProjectData data = context.DataProvider?.Invoke();
            data.ProjectSettings.DatasetPurpose = purpose;
            context.CanvasPanelViewModel?.RestoreSmartMaskAutoContourMode(
                data.ProjectSettings.SmartMaskAutoContourEnabled);
            context.CanvasPanelViewModel?.RestoreBoxDrawingMethod(
                data.ProjectSettings.BoxDrawingMethod);
            context.LearningWorkflowViewModel?.ApplyDatasetPurpose(purpose);

            // Recipe creation can run while the purpose ListBox still displays
            // the previous Recipe. Update the target before its SelectionChanged
            // command can write that stale value back to the new Recipe.
            if (context.DatasetPurposeListBox != null)
            {
                BindingOperations.GetBindingExpression(
                    context.DatasetPurposeListBox,
                    System.Windows.Controls.Primitives.Selector.SelectedItemProperty)?.UpdateTarget();
            }

            RefreshPresentation();
            context.RefreshShellDatasetContext?.Invoke();
        }

        private void RefreshPresentation()
        {
            context.RefreshCanvasAnnotationToolScope?.Invoke();
            context.ApplyAnnotationToolSelection?.Invoke(context.LearningWorkflowViewModel?.SelectedTool);
            context.RefreshCanvasWorkflowContext?.Invoke();
            context.RefreshAnnotationVisibilityForDatasetPurpose?.Invoke();
        }
    }

    internal sealed class DatasetPurposeWorkflowAdapterContext
    {
        internal Func<LabelingProjectData> DataProvider { get; init; }
        internal Func<string> RecipeNameProvider { get; init; }
        internal ProjectRecipeSessionService ProjectRecipeSessionService { get; init; }
        internal ProjectRecipeApplyWorkflowService ProjectRecipeApplyWorkflowService { get; init; }
        internal WpfLearningWorkflowPanelViewModel LearningWorkflowViewModel { get; init; }
        internal WpfCanvasPanelViewModel CanvasPanelViewModel { get; init; }
        internal ListBox DatasetPurposeListBox { get; init; }
        internal Dispatcher Dispatcher { get; init; }
        internal Action EnsureProjectSettings { get; init; }
        internal Action RestoreBoxDrawingMethodFromProject { get; init; }
        internal Action CancelFourPointBoxDraft { get; init; }
        internal Action RefreshCanvasAnnotationToolScope { get; init; }
        internal Action<WpfAnnotationToolItem> ApplyAnnotationToolSelection { get; init; }
        internal Action RefreshCanvasWorkflowContext { get; init; }
        internal Action RefreshAnnotationVisibilityForDatasetPurpose { get; init; }
        internal Action RefreshYoloTrainingStepCompletion { get; init; }
        internal Action RefreshShellDatasetContext { get; init; }
        internal Func<LabelingDatasetPurpose> GetCurrentDatasetPurpose { get; init; }
        internal Func<bool> IsApplicationCloseApproved { get; init; }
        internal Action<string> AppendLog { get; init; }
    }
}
