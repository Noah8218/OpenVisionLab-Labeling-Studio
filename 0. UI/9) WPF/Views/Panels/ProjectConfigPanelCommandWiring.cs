using OpenVisionLab;
using OpenVisionLab.Mvvm.Behaviors;
using System;
using System.Windows;
using System.Windows.Controls;

namespace MvcVisionSystem
{
    // Composition-only adapter for project configuration commands. Recipe
    // persistence, apply serialization, archive policy, and selection policy
    // remain owned by the existing ViewModel and workflow services.
    internal sealed class ProjectConfigPanelCommandWiring
    {
        private readonly ProjectConfigPanelCommandWiringContext context;

        internal ProjectConfigPanelCommandWiring(ProjectConfigPanelCommandWiringContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        internal void ConfigureProjectConfigPanelCommands()
        {
            context.ProjectConfigViewModel.ConfigureCommands(
                applyRecipe: null,
                refreshRecipeList: null,
                saveProjectConfig: null,
                openProjectConfigFolder: null,
                exportProjectArchive: null,
                importProjectArchive: null,
                recipeSelectionChanged: null);
            context.ProjectConfigViewModel.ConfigurePersistenceWorkflow(
                context.ProjectRecipeSessionService,
                context.ProjectRecipeApplyWorkflowService,
                context.ApplicationStateProvider,
                context.CloseApprovedProvider);
            context.ProjectConfigViewModel.ConfigureNavigationWorkflow(context.CreateNavigationCallbacks);
            context.ProjectConfigViewModel.ConfigureArchiveWorkflow(
                context.ProjectArchiveWorkflowService,
                context.CreateArchiveCallbacks);
            context.ProjectConfigViewModel.ConfigureRecipeSelectionWorkflow(context.RecipeSelectionGuard);
            context.ProjectConfigViewModel.ProjectConfigSaved -= context.ProjectConfigSaved;
            context.ProjectConfigViewModel.ProjectConfigSaved += context.ProjectConfigSaved;
            context.ProjectConfigViewModel.ProjectRecipeApplied -= context.ProjectRecipeApplied;
            context.ProjectConfigViewModel.ProjectRecipeApplied += context.ProjectRecipeApplied;
            context.ProjectConfigViewModel.WorkflowError -= context.WorkflowError;
            context.ProjectConfigViewModel.WorkflowError += context.WorkflowError;
            context.RefreshAttachedCommandBindings(
                context.RecipeListBox,
                new[] { InputCommandBehaviors.SelectedItemChangedCommandProperty });
        }
    }

    internal sealed class ProjectConfigPanelCommandWiringContext
    {
        internal WpfProjectConfigPanelViewModel ProjectConfigViewModel { get; init; }
        internal ProjectRecipeSessionService ProjectRecipeSessionService { get; init; }
        internal ProjectRecipeApplyWorkflowService ProjectRecipeApplyWorkflowService { get; init; }
        internal Func<LabelingApplicationState> ApplicationStateProvider { get; init; }
        internal Func<bool> CloseApprovedProvider { get; init; }
        internal Func<ProjectConfigNavigationCallbacks> CreateNavigationCallbacks { get; init; }
        internal ProjectArchiveWorkflowService ProjectArchiveWorkflowService { get; init; }
        internal Func<ProjectConfigArchiveCallbacks> CreateArchiveCallbacks { get; init; }
        internal Func<string, bool> RecipeSelectionGuard { get; init; }
        internal Action<string> ProjectConfigSaved { get; init; }
        internal Action<string, ProjectRecipeApplyResult> ProjectRecipeApplied { get; init; }
        internal Action<string> WorkflowError { get; init; }
        internal ComboBox RecipeListBox { get; init; }
        internal Action<DependencyObject, DependencyProperty[]> RefreshAttachedCommandBindings { get; init; }
    }
}
