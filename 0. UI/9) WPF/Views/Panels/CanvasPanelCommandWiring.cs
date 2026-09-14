using MvcVisionSystem.Yolo;
using OpenVisionLab;
using OpenVisionLab.Mvvm.Behaviors;
using System;
using System.Windows;
using System.Windows.Controls;

namespace MvcVisionSystem
{
    /// <summary>
    /// Composes the existing Canvas ViewModel with shell-owned WPF actions.
    /// It owns registration only; annotation, display-mode, and smart-mask policy
    /// remain in their existing ViewModel/service owners.
    /// </summary>
    internal sealed class CanvasPanelCommandWiring
    {
        private readonly CanvasPanelCommandWiringContext context;

        internal CanvasPanelCommandWiring(CanvasPanelCommandWiringContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        internal void ConfigureCanvasPanelCommands()
        {
            context.CanvasPanelViewModel.ConfigureCommands(
                context.ExecuteFitCanvasCommand,
                context.ExecuteActualSizeCanvasCommand,
                context.ExecutePanCanvasCommand,
                context.ExecuteFocusCandidateCommand,
                context.ExecuteResetAiOverlayCommand);
            context.CanvasPanelViewModel.ConfigureDisplayAdjustment(
                context.ScheduleDisplayAdjustmentRefresh);
            context.CanvasPanelViewModel.ConfigureCandidateReviewCommands(
                context.ExecutePreviousCandidateCommand,
                context.ExecuteNextCandidateCommand,
                context.ExecuteFocusCurrentLabelCommand,
                context.ExecuteConfirmSelectedCandidateCommand,
                context.ExecuteSkipSelectedCandidateCommand);
            context.CanvasPanelViewModel.ConfigureAnnotationTools(
                context.LearningWorkflowViewModel.VisibleAnnotationTools,
                context.LearningWorkflowViewModel.SelectedTool,
                context.ExecuteCanvasAnnotationToolSelectionChanged);
            context.CanvasPanelViewModel.ConfigureAnnotationCommands(
                context.ExecuteUndoAnnotationCommand,
                context.ExecuteRedoAnnotationCommand,
                context.ExecuteDeleteObjectCommand);
            context.CanvasPanelViewModel.ConfigureAnnotationSaveCommand(
                context.ExecuteSaveAnnotationsCommand);
            context.CanvasPanelViewModel.ConfigureNoObjectCompletionCommand(
                context.ExecuteCompleteNoObjectAndNextCommand);
            context.CanvasPanelViewModel.ConfigureLabelClassSelection(
                null,
                () => context.ShowClassCatalogWorkflowView(WpfShellWorkflowStage.Labeling));
            context.CanvasPanelViewModel.ConfigureLabelClassSelectionWorkflow(
                () => context.CancelFourPointBoxDraft(false),
                context.SelectClassCatalogClass,
                context.RefreshObjectClassOptions);
            context.CanvasPanelViewModel.ConfigureDisplayModeSelection(null);
            context.CanvasPanelViewModel.ConfigureDisplayModeSelectionWorkflow(
                context.ApplyCanvasDisplayMode);
            context.CanvasPanelViewModel.ConfigureBoxDrawingMethod(
                context.ExecuteSetBoxDrawingMethod);
            context.CanvasPanelViewModel.ConfigureBrushSizeCommands(
                context.ExecuteDecreaseBrushSizeCommand,
                context.ExecuteIncreaseBrushSizeCommand);
            context.CanvasPanelViewModel.ConfigureSmartMaskCommands(
                context.ExecuteCreateSmartMaskCandidateCommand,
                () => context.ExecuteSetSmartMaskPointModeCommand(WpfSmartMaskPointInputMode.Positive),
                () => context.ExecuteSetSmartMaskPointModeCommand(WpfSmartMaskPointInputMode.Negative),
                context.ExecuteUndoSmartMaskPointCommand,
                context.ExecuteClearSmartMaskPointsCommand,
                context.ExecuteCancelSmartMaskGenerationCommand,
                context.ExecuteNextSmartMaskInstanceCommand,
                () => context.ExecuteSelectSmartMaskCandidateVersionCommand(WpfSmartMaskCandidateVersion.Initial),
                () => context.ExecuteSelectSmartMaskCandidateVersionCommand(WpfSmartMaskCandidateVersion.Latest),
                context.ExecuteSetSmartMaskAutoContourMode,
                context.ExecuteSetSmartMaskPolygonDetailCommand);
            context.SyncCanvasBrushSizeFromWorkflow();
            context.RefreshCanvasAnnotationToolScope();
            context.RefreshCanvasWorkflowContext();
            context.RefreshAttachedCommandBindings(
                context.CanvasAnnotationToolListBox,
                new[] { InputCommandBehaviors.SelectedItemChangedCommandProperty });
            context.RefreshAttachedCommandBindings(
                context.CanvasLabelClassListBox,
                new[] { InputCommandBehaviors.SelectedItemChangedCommandProperty });
            context.RefreshAttachedCommandBindings(
                context.CanvasDisplayModeListBox,
                new[] { InputCommandBehaviors.SelectedItemChangedCommandProperty });
        }
    }

    /// <summary>
    /// Explicit dependency list for Canvas command composition. Keeping the
    /// shell callbacks here makes the caller and mutable-state owners searchable
    /// without sharing Window private state through a partial type.
    /// </summary>
    internal sealed class CanvasPanelCommandWiringContext
    {
        internal WpfCanvasPanelViewModel CanvasPanelViewModel { get; init; }
        internal WpfLearningWorkflowPanelViewModel LearningWorkflowViewModel { get; init; }
        internal ListBox CanvasAnnotationToolListBox { get; init; }
        internal ListBox CanvasLabelClassListBox { get; init; }
        internal ListBox CanvasDisplayModeListBox { get; init; }
        internal Action<DependencyObject, DependencyProperty[]> RefreshAttachedCommandBindings { get; init; }
        internal Action ExecuteFitCanvasCommand { get; init; }
        internal Action ExecuteActualSizeCanvasCommand { get; init; }
        internal Action ExecutePanCanvasCommand { get; init; }
        internal Action ExecuteFocusCandidateCommand { get; init; }
        internal Action ExecuteResetAiOverlayCommand { get; init; }
        internal Action ScheduleDisplayAdjustmentRefresh { get; init; }
        internal Action ExecutePreviousCandidateCommand { get; init; }
        internal Action ExecuteNextCandidateCommand { get; init; }
        internal Action ExecuteFocusCurrentLabelCommand { get; init; }
        internal Action ExecuteConfirmSelectedCandidateCommand { get; init; }
        internal Action ExecuteSkipSelectedCandidateCommand { get; init; }
        internal Action<object> ExecuteCanvasAnnotationToolSelectionChanged { get; init; }
        internal Action ExecuteUndoAnnotationCommand { get; init; }
        internal Action ExecuteRedoAnnotationCommand { get; init; }
        internal Action ExecuteDeleteObjectCommand { get; init; }
        internal Action ExecuteSaveAnnotationsCommand { get; init; }
        internal Action ExecuteCompleteNoObjectAndNextCommand { get; init; }
        internal Action<WpfShellWorkflowStage> ShowClassCatalogWorkflowView { get; init; }
        internal Action<bool> CancelFourPointBoxDraft { get; init; }
        internal Action<string> SelectClassCatalogClass { get; init; }
        internal Action<string> RefreshObjectClassOptions { get; init; }
        internal Action<WpfCanvasDisplayMode> ApplyCanvasDisplayMode { get; init; }
        internal Action<LabelingBoxDrawingMethod> ExecuteSetBoxDrawingMethod { get; init; }
        internal Action ExecuteDecreaseBrushSizeCommand { get; init; }
        internal Action ExecuteIncreaseBrushSizeCommand { get; init; }
        internal Action ExecuteCreateSmartMaskCandidateCommand { get; init; }
        internal Action<WpfSmartMaskPointInputMode> ExecuteSetSmartMaskPointModeCommand { get; init; }
        internal Action ExecuteUndoSmartMaskPointCommand { get; init; }
        internal Action ExecuteClearSmartMaskPointsCommand { get; init; }
        internal Action ExecuteCancelSmartMaskGenerationCommand { get; init; }
        internal Action ExecuteNextSmartMaskInstanceCommand { get; init; }
        internal Action<WpfSmartMaskCandidateVersion> ExecuteSelectSmartMaskCandidateVersionCommand { get; init; }
        internal Action<bool> ExecuteSetSmartMaskAutoContourMode { get; init; }
        internal Action<WpfSmartMaskPolygonDetail> ExecuteSetSmartMaskPolygonDetailCommand { get; init; }
        internal Action SyncCanvasBrushSizeFromWorkflow { get; init; }
        internal Action RefreshCanvasAnnotationToolScope { get; init; }
        internal Action RefreshCanvasWorkflowContext { get; init; }
    }
}
