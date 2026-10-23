using System;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using OpenVisionLab.Mvvm;

namespace MvcVisionSystem
{
    // Owns shell-level keyboard policy while the Window supplies annotation,
    // queue, and presentation callbacks at the composition boundary.
    internal sealed class ShellKeyboardShortcutAdapter
    {
        private readonly ShellKeyboardShortcutAdapterContext context;
        private bool displayPopupEscapeHandled;

        internal ShellKeyboardShortcutAdapter(ShellKeyboardShortcutAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        internal void Execute(KeyInputCommandArgs e)
        {
            if (e?.Key == Key.Escape && !e.IsRepeat) displayPopupEscapeHandled = false;
            if (e != null && context.IsShellDialogOpen?.Invoke() == true)
            {
                if (e.Key == Key.Escape && e.Modifiers == ModifierKeys.None && !e.IsRepeat) context.CloseShellDialog?.Execute(null);
                if (e.Key == Key.Escape || e.Modifiers != ModifierKeys.None && e.Modifiers != ModifierKeys.Shift) e.Handled = true;
                return;
            }
            if (e != null && context.IsCommandPaletteOpen?.Invoke() == true)
            {
                ICommand paletteCommand = e.Modifiers == ModifierKeys.None ? e.Key switch
                {
                    Key.Escape => context.CloseCommandPalette,
                    Key.Down => context.NextPaletteCommand,
                    Key.Up => context.PreviousPaletteCommand,
                    Key.Enter => context.ExecutePaletteCommand,
                    _ => null
                } : null;
                if (paletteCommand != null)
                {
                    if (!e.IsRepeat && paletteCommand.CanExecute(null)) paletteCommand.Execute(null);
                    e.Handled = true;
                }
                else if (e.Modifiers == ModifierKeys.Control && e.Key is Key.S or Key.I or Key.K) e.Handled = true;
                return;
            }
            if (context.CanvasPanelViewModel?.IsDisplayAdjustmentOpen == true
                || e?.Key == Key.Escape && e.Modifiers == ModifierKeys.None && e.IsRepeat && displayPopupEscapeHandled)
            {
                if (e?.Key == Key.Escape && e.Modifiers == ModifierKeys.None)
                {
                    ICommand closeDisplayAdjustment = context.CanvasPanelViewModel?.ToggleDisplayAdjustmentCommand;
                    if (!e.IsRepeat && closeDisplayAdjustment?.CanExecute(null) == true)
                    {
                        closeDisplayAdjustment.Execute(null);
                        displayPopupEscapeHandled = true;
                    }
                    e.Handled = true;
                }
                return;
            }
            if (e?.Modifiers == ModifierKeys.Control && e.Key == Key.K && context.OpenCommandPalette != null)
            {
                if (!e.IsRepeat && context.OpenCommandPalette.CanExecute(null)) context.OpenCommandPalette.Execute(null);
                e.Handled = true;
                return;
            }
            if (e?.Modifiers == ModifierKeys.Control && e.Key == Key.F && context.IsRecipeLibraryVisible?.Invoke() == true && context.FocusRecipeSearch != null)
            {
                e.Handled = e.IsRepeat || context.FocusRecipeSearch();
                return;
            }
            if (e?.Key == Key.OemQuestion && e.Modifiers == ModifierKeys.Shift && !IsInteractiveControl(e.OriginalSource, allowActionControls: false))
            {
                if (!e.IsRepeat) context.ExecuteMenuShortcut?.Invoke("Shortcuts");
                e.Handled = true;
                return;
            }
            if (e?.Modifiers == ModifierKeys.Alt && e.Key is Key.D1 or Key.D2 or Key.D3 or Key.D4 or Key.D5 && context.ExecuteMenuShortcut != null)
            {
                string id = e.Key switch { Key.D1 => "StageDataset", Key.D2 => "StageLabeling", Key.D3 => "StageTraining", Key.D4 => "StageComparison", _ => "StageInference" };
                if (!e.IsRepeat) context.ExecuteMenuShortcut(id);
                e.Handled = true;
                return;
            }
            if (e != null && context.IsComparisonStageActive?.Invoke() == true)
            {
                if (e.Modifiers == ModifierKeys.None && e.Key == Key.Escape && context.CancelComparisonConfirmation?.CanExecute(null) == true)
                {
                    if (!e.IsRepeat) context.CancelComparisonConfirmation.Execute(null);
                    e.Handled = true;
                }
                return;
            }
            if (e != null && context.IsLabelingStageActive?.Invoke() == true && context.ImageQueueViewModel?.IsAnomalyImageReviewMode == true
                && !IsInteractiveControl(e.OriginalSource, allowActionControls: true))
            {
                ICommand anomalyCommand = e.Modifiers == ModifierKeys.None ? e.Key switch
                {
                    Key.G => context.ImageQueueViewModel.ToggleAnomalyGridCommand,
                    Key.D1 or Key.NumPad1 => context.ImageQueueViewModel.MarkAnomalySelectionNormalCommand,
                    Key.D2 or Key.NumPad2 => context.ImageQueueViewModel.MarkAnomalySelectionAbnormalCommand,
                    _ => null
                } : e.Modifiers == ModifierKeys.Control ? e.Key switch
                {
                    Key.Z when context.CanvasPanelViewModel?.IsAnnotationSaveEnabled != true => context.ImageQueueViewModel.UndoAnomalyDecisionCommand,
                    Key.Enter => context.ObjectReviewViewModel?.IsReviewModeActive == true
                        ? context.ObjectReviewViewModel.ApproveAndNextCommand
                        : context.CanvasPanelViewModel?.IsAnnotationSaveEnabled == true
                            ? context.CanvasPanelViewModel.SaveAndNextAnnotationCommand : context.ImageQueueViewModel.NextAnomalyUnreviewedCommand,
                    _ => null
                } : null;
                if (e.Modifiers == ModifierKeys.None && e.Key == Key.Enter)
                {
                    e.Handled = e.IsRepeat || context.AcceptAnomalyPrediction?.Invoke() == true;
                    return;
                }
                if (anomalyCommand != null)
                {
                    if (!e.IsRepeat && anomalyCommand.CanExecute(null)) anomalyCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
            }
            if (e != null && context.IsLabelingStageActive?.Invoke() == true && !IsInteractiveControl(e.OriginalSource, allowActionControls: true))
            {
                ICommand action = e.Modifiers == ModifierKeys.None ? e.Key switch
                {
                    Key.F => context.CanvasPanelViewModel?.FitCommand,
                    Key.OemOpenBrackets => context.CanvasPanelViewModel?.DecreaseBrushSizeCommand,
                    Key.OemCloseBrackets => context.CanvasPanelViewModel?.IncreaseBrushSizeCommand,
                    _ => null
                } : e.Modifiers == ModifierKeys.Control && e.Key == Key.I && context.ImageQueueViewModel?.IsDetectSelectedEnabled == true
                    ? context.ImageQueueViewModel.DetectSelectedQueueCommand : null;
                if (action != null)
                {
                    if (!e.IsRepeat && action.CanExecute(null)) action.Execute(null);
                    e.Handled = true;
                    return;
                }
                if (e.Modifiers == (ModifierKeys.Shift) && e.Key == Key.Enter)
                {
                    e.Handled = e.IsRepeat || context.ConfirmAllCandidates?.Invoke() == true;
                    return;
                }
                if (e.Modifiers == ModifierKeys.Control && e.Key is Key.C or Key.V)
                {
                    if (!e.IsRepeat)
                    {
                        if (e.Key == Key.C) context.CopySelectedAnnotation?.Invoke();
                        else context.PasteAnnotation?.Invoke();
                    }
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.Tab && e.Modifiers is ModifierKeys.None or ModifierKeys.Shift && context.ObjectReviewViewModel != null)
                {
                    if (!e.IsRepeat) context.ObjectReviewViewModel.MoveObjectSelection(e.Modifiers == ModifierKeys.Shift ? -1 : 1);
                    e.Handled = true;
                    return;
                }
            }
            if (e != null && AnnotationProductivityService.ResolveShortcut(e.Key, e.Modifiers).Kind == WpfAnnotationShortcutKind.ImportPreviousLabels
                && context.IsLabelingStageActive?.Invoke() == true
                && !IsInteractiveControl(e.OriginalSource, allowActionControls: true))
            {
                ICommand command = context.CanvasPanelViewModel?.ImportPreviousLabelsCommand;
                if (!e.IsRepeat && command?.CanExecute(null) == true) command.Execute(null);
                e.Handled = true;
                return;
            }

            if (e?.Modifiers == ModifierKeys.Control && e.Key == Key.S
                && context.IsLabelingStageActive?.Invoke() == true
                && !IsInteractiveControl(e.OriginalSource, allowActionControls: true)
                && context.ExecuteMenuShortcut != null)
            {
                if (!e.IsRepeat) context.ExecuteMenuShortcut("Save");
                e.Handled = true;
                return;
            }

            if (e?.Modifiers == ModifierKeys.None && e.Key == Key.X
                && context.IsLabelingStageActive?.Invoke() == true
                && !IsInteractiveControl(e.OriginalSource, allowActionControls: true))
            {
                ICommand command = context.CanvasPanelViewModel?.ToggleNoObjectSelectionCommand;
                if (!e.IsRepeat && command?.CanExecute(null) == true) command.Execute(null);
                e.Handled = true;
                return;
            }

            if (e?.Modifiers == ModifierKeys.Control && e.Key == Key.Enter
                && context.IsLabelingStageActive?.Invoke() == true
                && !IsInteractiveControl(e.OriginalSource, allowActionControls: true))
            {
                ICommand command = context.ObjectReviewViewModel?.IsReviewModeActive == true
                    ? context.ObjectReviewViewModel.ApproveAndNextCommand : context.CanvasPanelViewModel?.SaveAndNextAnnotationCommand;
                if (!e.IsRepeat && command?.CanExecute(null) == true) command.Execute(null);
                e.Handled = true;
                return;
            }

            bool allowAnnotationEditControls = e != null && context.IsLabelingStageActive?.Invoke() == true && (
                e.Modifiers == ModifierKeys.None && AnnotationProductivityService.ResolveShortcut(e.Key, e.Modifiers).Kind == WpfAnnotationShortcutKind.SelectClass
                || e.Modifiers == ModifierKeys.Control && (e.Key == Key.Z || e.Key == Key.Y)
                || e.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.Z);
            if (e == null || IsInteractiveControl(e.OriginalSource, allowAnnotationEditControls))
            {
                return;
            }

            string menuShortcut = e.Modifiers == ModifierKeys.Control ? e.Key switch
            {
                Key.S => "Save", Key.Z => "Undo", Key.Y => "Redo", Key.D => "Duplicate", _ => null
            } : e.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.Z ? "Redo"
                : e.Modifiers == ModifierKeys.None && e.Key == Key.Delete ? "Delete"
                : e.Modifiers == ModifierKeys.None && e.Key == Key.F1 ? "Guide" : null;
            bool candidateHistory = menuShortcut == "Undo" && context.CanvasPanelViewModel?.IsUndoSmartMaskCandidateEditEnabled == true
                || menuShortcut == "Redo" && context.CanvasPanelViewModel?.IsRedoSmartMaskCandidateEditEnabled == true;
            if (menuShortcut != null && !candidateHistory && context.ExecuteMenuShortcut != null)
            {
                if (!e.IsRepeat) context.ExecuteMenuShortcut(menuShortcut);
                e.Handled = true;
                return;
            }

            if (e.Modifiers == ModifierKeys.None && e.Key == Key.Escape)
            {
                e.Handled = context.CancelFourPointBoxDraft?.Invoke() == true;
                if (e.Handled)
                {
                    return;
                }

                e.Handled = context.CancelPolygonDraft?.Invoke() == true;
                if (e.Handled)
                {
                    return;
                }

                e.Handled = context.CancelSmartMaskGeneration?.Invoke() == true;
                return;
            }

            if (e.Modifiers == ModifierKeys.None && e.Key == Key.Back)
            {
                e.Handled = context.RemoveLastFourPointBoxPoint?.Invoke() == true
                    || context.RemoveLastPolygonDraftPoint?.Invoke() == true;
                if (e.Handled)
                {
                    return;
                }
            }

            if ((e.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (e.Key == Key.Z)
                {
                    if (e.IsRepeat) { e.Handled = true; return; }
                    bool isRedoShortcut = (e.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
                    e.Handled = isRedoShortcut
                        ? context.RedoSmartMaskCandidateEdit?.Invoke() == true
                            || context.RedoAnnotationHistory?.Invoke() == true
                        : context.UndoSmartMaskCandidateEdit?.Invoke() == true
                            || context.UndoAnnotationHistory?.Invoke() == true;
                    return;
                }

                if (e.Key == Key.Y)
                {
                    if (e.IsRepeat) { e.Handled = true; return; }
                    e.Handled = context.RedoSmartMaskCandidateEdit?.Invoke() == true
                        || context.RedoAnnotationHistory?.Invoke() == true;
                    return;
                }

                AnnotationShortcut controlShortcut = AnnotationProductivityService.ResolveShortcut(e.Key, e.Modifiers);
                if (controlShortcut.Kind == WpfAnnotationShortcutKind.DuplicateSelected)
                {
                    e.Handled = context.TryDuplicateSelectedAnnotation?.Invoke() == true;
                }

                return;
            }

            if (e.Modifiers != ModifierKeys.None)
            {
                return;
            }

            if (e.Key == Key.O)
            {
                e.Handled = e.IsRepeat
                    ? context.CanvasPanelViewModel?.IsTemporaryOriginalView == true
                    : context.CanvasPanelViewModel?.BeginTemporaryOriginalView() == true;
                return;
            }

            if (e.Key == Key.Space)
            {
                e.Handled = e.IsRepeat
                    ? context.IsTemporaryPanActive?.Invoke() == true
                    : context.BeginTemporaryPan?.Invoke() == true;
                return;
            }

            if (e.Key == Key.Enter)
            {
                if (e.IsRepeat)
                {
                    e.Handled = true;
                    return;
                }

                e.Handled = context.CompletePolygonDraft?.Invoke() == true;
                if (!e.Handled)
                {
                    e.Handled = context.ApplySmartMaskCandidate?.Invoke() == true;
                }

                if (!e.Handled)
                {
                    e.Handled = context.ConfirmSelectedCandidate?.Invoke() == true;
                }

                return;
            }

            AnnotationShortcut shortcut = AnnotationProductivityService.ResolveShortcut(e.Key, e.Modifiers);
            if (shortcut.Kind == WpfAnnotationShortcutKind.SelectClass && e.IsRepeat)
            {
                e.Handled = true;
                return;
            }
            if (TryExecuteAnnotationProductivityShortcut(shortcut))
            {
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Down || e.Key == Key.Right || e.Key == Key.D && context.IsLabelingStageActive?.Invoke() == true)
            {
                e.Handled = context.TryOpenAdjacentQueueImage?.Invoke(1) == true;
                return;
            }

            if (e.Key == Key.Up || e.Key == Key.Left || e.Key == Key.A && context.IsLabelingStageActive?.Invoke() == true)
            {
                e.Handled = context.TryOpenAdjacentQueueImage?.Invoke(-1) == true;
            }
        }

        internal void ExecuteKeyUp(KeyInputCommandArgs e)
        {
            if (e?.Key == Key.Escape)
            {
                if (displayPopupEscapeHandled) e.Handled = true;
                displayPopupEscapeHandled = false;
                return;
            }

            if (e?.Key == Key.Space)
            {
                e.Handled = context.EndTemporaryPan?.Invoke() == true;
                return;
            }

            if (e?.Key != Key.O)
            {
                return;
            }

            e.Handled = context.CanvasPanelViewModel?.EndTemporaryOriginalView() == true;
        }

        private bool TryExecuteAnnotationProductivityShortcut(AnnotationShortcut shortcut)
        {
            if (shortcut == null || shortcut.Kind == WpfAnnotationShortcutKind.None)
            {
                return false;
            }

            switch (shortcut.Kind)
            {
                case WpfAnnotationShortcutKind.SelectTool:
                    WpfAnnotationToolItem selectedTool = context.ResolveSelectableAnnotationTool?.Invoke(shortcut.Tool);
                    if (selectedTool == null)
                    {
                        return false;
                    }

                    context.ApplyAnnotationToolSelection?.Invoke(selectedTool);
                    return true;

                case WpfAnnotationShortcutKind.SelectClass:
                    WpfObjectReviewListItem selectedObject = context.ObjectReviewViewModel?.SelectedObject;
                    if (context.CanvasPanelViewModel?.TrySelectLabelClassByShortcut(shortcut.ClassIndex) != true)
                    {
                        return false;
                    }

                    context.CanvasPanelViewModel.ApplyLabelClassSelection(
                        context.CanvasPanelViewModel.SelectedLabelClass);
                    if (context.IsLabelingStageActive?.Invoke() == true && selectedObject?.IsInlineEditEnabled == true)
                        selectedObject.SelectedClassName = context.CanvasPanelViewModel.SelectedLabelClass.Text;
                    context.SetModelStatus?.Invoke(
                        $"클래스 단축키: {context.CanvasPanelViewModel.SelectedLabelClass.Text}");
                    return true;

                case WpfAnnotationShortcutKind.OpenClassCatalog:
                    context.ShowClassCatalogWorkflowView?.Invoke(WpfShellWorkflowStage.Labeling);
                    return true;

                case WpfAnnotationShortcutKind.RepeatLast:
                    return TryRepeatLastAnnotationToolAndClass();

                case WpfAnnotationShortcutKind.ToggleShortcutHelp:
                    context.CanvasPanelViewModel?.ToggleShortcutHelp();
                    return true;

                default:
                    return false;
            }
        }

        private bool TryRepeatLastAnnotationToolAndClass()
        {
            if (context.CanvasPanelViewModel?.TryGetRepeatSelection(out WpfAnnotationTool tool, out string className) != true)
            {
                return false;
            }

            WpfAnnotationToolItem selectedTool = context.ResolveSelectableAnnotationTool?.Invoke(tool);
            if (selectedTool == null)
            {
                return false;
            }

            context.CanvasPanelViewModel.SelectLabelClass(className);
            context.CanvasPanelViewModel.ApplyLabelClassSelection(
                context.CanvasPanelViewModel.SelectedLabelClass);
            context.ApplyAnnotationToolSelection?.Invoke(selectedTool);
            context.SetModelStatus?.Invoke($"마지막 라벨링 반복: {selectedTool.Text} / {className}");
            return true;
        }

        internal static bool IsInteractiveControl(object source, bool allowActionControls = false)
        {
            for (DependencyObject current = source as DependencyObject; current != null;)
            {
                if (current is MenuBase || current is MenuItem || current is ComboBox || current is ComboBoxItem
                    || current is TextBoxBase || current is PasswordBox || current is RangeBase
                    || !allowActionControls && (current is ButtonBase || current is TabItem || current is ListBox || current is ListBoxItem))
                    return true;
                DependencyObject visualParent = current is Visual || current is System.Windows.Media.Media3D.Visual3D
                    ? VisualTreeHelper.GetParent(current) : null;
                current = visualParent ?? LogicalTreeHelper.GetParent(current);
            }
            return false;
        }
    }

    internal sealed class ShellKeyboardShortcutAdapterContext
    {
        internal Func<bool> IsShellDialogOpen { get; init; }
        internal ICommand CloseShellDialog { get; init; }
        internal WpfCanvasPanelViewModel CanvasPanelViewModel { get; init; }
        internal WpfObjectReviewPanelViewModel ObjectReviewViewModel { get; init; }
        internal WpfImageQueuePanelViewModel ImageQueueViewModel { get; init; }
        internal Func<bool> IsRecipeLibraryVisible { get; init; }
        internal Func<bool> FocusRecipeSearch { get; init; }
        internal Func<bool> IsLabelingStageActive { get; init; }
        internal Func<bool> IsComparisonStageActive { get; init; }
        internal ICommand CancelComparisonConfirmation { get; init; }
        internal Action<string> ExecuteMenuShortcut { get; init; }
        internal Func<bool> IsCommandPaletteOpen { get; init; }
        internal ICommand OpenCommandPalette { get; init; }
        internal ICommand CloseCommandPalette { get; init; }
        internal ICommand NextPaletteCommand { get; init; }
        internal ICommand PreviousPaletteCommand { get; init; }
        internal ICommand ExecutePaletteCommand { get; init; }
        internal Func<bool> CancelFourPointBoxDraft { get; init; }
        internal Func<bool> CancelPolygonDraft { get; init; }
        internal Func<bool> CancelSmartMaskGeneration { get; init; }
        internal Func<bool> CompletePolygonDraft { get; init; }
        internal Func<bool> ApplySmartMaskCandidate { get; init; }
        internal Func<bool> ConfirmSelectedCandidate { get; init; }
        internal Func<bool> ConfirmAllCandidates { get; init; }
        internal Func<bool> AcceptAnomalyPrediction { get; init; }
        internal Func<bool> CopySelectedAnnotation { get; init; }
        internal Func<bool> PasteAnnotation { get; init; }
        internal Func<bool> BeginTemporaryPan { get; init; }
        internal Func<bool> EndTemporaryPan { get; init; }
        internal Func<bool> IsTemporaryPanActive { get; init; }
        internal Func<bool> RemoveLastFourPointBoxPoint { get; init; }
        internal Func<bool> RemoveLastPolygonDraftPoint { get; init; }
        internal Func<bool> UndoAnnotationHistory { get; init; }
        internal Func<bool> RedoAnnotationHistory { get; init; }
        internal Func<bool> UndoSmartMaskCandidateEdit { get; init; }
        internal Func<bool> RedoSmartMaskCandidateEdit { get; init; }
        internal Func<bool> TryDuplicateSelectedAnnotation { get; init; }
        internal Func<WpfAnnotationTool, WpfAnnotationToolItem> ResolveSelectableAnnotationTool { get; init; }
        internal Action<WpfAnnotationToolItem> ApplyAnnotationToolSelection { get; init; }
        internal Action<WpfShellWorkflowStage> ShowClassCatalogWorkflowView { get; init; }
        internal Action<string> SetModelStatus { get; init; }
        internal Func<int, bool> TryOpenAdjacentQueueImage { get; init; }
    }
}
