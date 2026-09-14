using System;
using System.Windows.Controls;
using System.Windows.Input;
using OpenVisionLab.Mvvm;

namespace MvcVisionSystem
{
    // Owns shell-level keyboard policy while the Window supplies annotation,
    // queue, and presentation callbacks at the composition boundary.
    internal sealed class ShellKeyboardShortcutAdapter
    {
        private readonly ShellKeyboardShortcutAdapterContext context;

        internal ShellKeyboardShortcutAdapter(ShellKeyboardShortcutAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        internal void Execute(KeyInputCommandArgs e)
        {
            if (e == null || IsTextEditingElement(e.OriginalSource))
            {
                return;
            }

            if (e.Modifiers == ModifierKeys.None && e.Key == Key.Escape)
            {
                e.Handled = context.CancelFourPointBoxDraft?.Invoke() == true;
                if (e.Handled)
                {
                    return;
                }
            }

            if (e.Modifiers == ModifierKeys.None && e.Key == Key.Back)
            {
                e.Handled = context.RemoveLastFourPointBoxPoint?.Invoke() == true;
                if (e.Handled)
                {
                    return;
                }
            }

            if ((e.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (e.Key == Key.Z)
                {
                    bool isRedoShortcut = (e.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
                    e.Handled = isRedoShortcut
                        ? context.RedoAnnotationHistory?.Invoke() == true
                        : context.UndoAnnotationHistory?.Invoke() == true;
                    return;
                }

                if (e.Key == Key.Y)
                {
                    e.Handled = context.RedoAnnotationHistory?.Invoke() == true;
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

            AnnotationShortcut shortcut = AnnotationProductivityService.ResolveShortcut(e.Key, e.Modifiers);
            if (TryExecuteAnnotationProductivityShortcut(shortcut))
            {
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Down || e.Key == Key.Right)
            {
                e.Handled = context.TryOpenAdjacentQueueImage?.Invoke(1) == true;
                return;
            }

            if (e.Key == Key.Up || e.Key == Key.Left)
            {
                e.Handled = context.TryOpenAdjacentQueueImage?.Invoke(-1) == true;
            }
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
                    if (context.CanvasPanelViewModel?.TrySelectLabelClassByShortcut(shortcut.ClassIndex) != true)
                    {
                        return false;
                    }

                    context.CanvasPanelViewModel.ApplyLabelClassSelection(
                        context.CanvasPanelViewModel.SelectedLabelClass);
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

        private static bool IsTextEditingElement(object source)
        {
            return source is TextBox
                || source is ComboBox
                || source is System.Windows.Controls.Primitives.RangeBase
                || source is System.Windows.Controls.Primitives.TextBoxBase;
        }
    }

    internal sealed class ShellKeyboardShortcutAdapterContext
    {
        internal WpfCanvasPanelViewModel CanvasPanelViewModel { get; init; }
        internal Func<bool> CancelFourPointBoxDraft { get; init; }
        internal Func<bool> RemoveLastFourPointBoxPoint { get; init; }
        internal Func<bool> UndoAnnotationHistory { get; init; }
        internal Func<bool> RedoAnnotationHistory { get; init; }
        internal Func<bool> TryDuplicateSelectedAnnotation { get; init; }
        internal Func<WpfAnnotationTool, WpfAnnotationToolItem> ResolveSelectableAnnotationTool { get; init; }
        internal Action<WpfAnnotationToolItem> ApplyAnnotationToolSelection { get; init; }
        internal Action<WpfShellWorkflowStage> ShowClassCatalogWorkflowView { get; init; }
        internal Action<string> SetModelStatus { get; init; }
        internal Func<int, bool> TryOpenAdjacentQueueImage { get; init; }
    }
}
