using OpenVisionLab;
using OpenVisionLab.Mvvm.Behaviors;
using System;
using System.Windows;
using System.Windows.Controls;

namespace MvcVisionSystem
{
    // Composition-only adapter for Class Catalog commands. Mutation and
    // selection policy stay owned by the existing panel ViewModel/service.
    internal sealed class ClassCatalogPanelCommandWiring
    {
        private readonly ClassCatalogPanelCommandWiringContext context;

        internal ClassCatalogPanelCommandWiring(ClassCatalogPanelCommandWiringContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        internal void ConfigureClassCatalogPanelCommands()
        {
            context.ClassCatalogViewModel.ConfigureCommands(
                classNamePreviewKeyDown: null,
                addClass: null,
                renameClass: null,
                archiveClass: null,
                applyClassColor: null,
                classSelectionChanged: null);
            context.ClassCatalogViewModel.ConfigureMutationWorkflow(
                context.ClassCatalogWorkflowService,
                context.DataProvider,
                context.RecipeNameProvider,
                context.CloseApprovedProvider);
            context.ClassCatalogViewModel.ConfigureClassNamePreviewKeyWorkflow();
            context.ClassCatalogViewModel.ConfigureSelectionWorkflow(
                context.CancelPendingFourPointBoxDraft,
                context.SelectCanvasClass,
                context.RefreshObjectClassOptions);
            context.ClassCatalogViewModel.MutationCompleted -= context.MutationCompleted;
            context.ClassCatalogViewModel.MutationCompleted += context.MutationCompleted;
            context.RefreshAttachedCommandBindings(
                context.ClassNameBox,
                new[] { InputCommandBehaviors.PreviewKeyInputCommandProperty });
            context.RefreshAttachedCommandBindings(
                context.ClassListBox,
                new[] { InputCommandBehaviors.SelectedItemChangedCommandProperty });
        }
    }

    internal sealed class ClassCatalogPanelCommandWiringContext
    {
        internal WpfClassCatalogPanelViewModel ClassCatalogViewModel { get; init; }
        internal ClassCatalogWorkflowService ClassCatalogWorkflowService { get; init; }
        internal Func<LabelingProjectData> DataProvider { get; init; }
        internal Func<string> RecipeNameProvider { get; init; }
        internal Func<bool> CloseApprovedProvider { get; init; }
        internal Action CancelPendingFourPointBoxDraft { get; init; }
        internal Action<string> SelectCanvasClass { get; init; }
        internal Action<string> RefreshObjectClassOptions { get; init; }
        internal Action<WpfClassCatalogMutationKind, WpfClassCatalogMutationResult> MutationCompleted { get; init; }
        internal TextBox ClassNameBox { get; init; }
        internal ListBox ClassListBox { get; init; }
        internal Action<DependencyObject, DependencyProperty[]> RefreshAttachedCommandBindings { get; init; }
    }
}
