using System;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns annotation undo/redo transitions and the history toolbar projection.
    /// Canvas capture, snapshot restore, and WPF presentation remain Shell adapters.
    /// </summary>
    public sealed class AnnotationHistoryWorkflowService
    {
        private readonly AnnotationHistoryStack historyStack;

        public AnnotationHistoryWorkflowService(int historyLimit = 50)
        {
            historyStack = new AnnotationHistoryStack(historyLimit);
        }

        internal AnnotationHistoryStack HistoryStack => historyStack;

        public int UndoCount => historyStack.UndoCount;

        public int RedoCount => historyStack.RedoCount;

        public bool Push(WpfAnnotationHistorySnapshot snapshot)
        {
            if (snapshot == null)
            {
                return false;
            }

            historyStack.Push(snapshot);
            return true;
        }

        public void Clear()
        {
            historyStack.Clear();
        }

        public AnnotationHistoryToolState GetToolState(bool hasPendingMaskStrokeUndo, string pendingMaskStrokeActionName)
        {
            bool canUndo = hasPendingMaskStrokeUndo || historyStack.UndoCount > 0;
            bool canRedo = !hasPendingMaskStrokeUndo && historyStack.RedoCount > 0;
            string undoActionName = hasPendingMaskStrokeUndo
                ? NormalizeActionName(pendingMaskStrokeActionName)
                : historyStack.UndoCount > 0
                    ? NormalizeActionName(historyStack.PeekUndo().ActionName)
                    : string.Empty;
            string redoActionName = canRedo
                ? NormalizeActionName(historyStack.PeekRedo().ActionName)
                : string.Empty;
            return new AnnotationHistoryToolState(canUndo, canRedo, undoActionName, redoActionName);
        }

        public bool TryUndo(
            Func<string, WpfAnnotationHistorySnapshot, WpfAnnotationHistorySnapshot> captureOpposite,
            out AnnotationHistoryTransition transition)
        {
            WpfAnnotationHistorySnapshot candidate = historyStack.PeekUndo();
            if (candidate == null)
            {
                transition = null;
                return false;
            }

            WpfAnnotationHistorySnapshot opposite = captureOpposite(
                $"Redo {candidate.ActionName}",
                candidate);
            if (!historyStack.TryMoveUndoToRedo(opposite, out WpfAnnotationHistorySnapshot target))
            {
                transition = null;
                return false;
            }

            transition = new AnnotationHistoryTransition(target, FormatActionName(target.ActionName));
            return true;
        }

        public bool TryRedo(
            Func<string, WpfAnnotationHistorySnapshot, WpfAnnotationHistorySnapshot> captureOpposite,
            out AnnotationHistoryTransition transition)
        {
            WpfAnnotationHistorySnapshot candidate = historyStack.PeekRedo();
            if (candidate == null)
            {
                transition = null;
                return false;
            }

            WpfAnnotationHistorySnapshot opposite = captureOpposite(
                $"Undo {candidate.ActionName}",
                candidate);
            if (!historyStack.TryMoveRedoToUndo(opposite, out WpfAnnotationHistorySnapshot target))
            {
                transition = null;
                return false;
            }

            transition = new AnnotationHistoryTransition(target, FormatActionName(target.ActionName));
            return true;
        }

        private static string NormalizeActionName(string actionName)
        {
            string normalized = actionName ?? string.Empty;
            if (normalized.StartsWith("Undo ", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("Redo ", StringComparison.OrdinalIgnoreCase))
            {
                return normalized.Substring(5);
            }

            return normalized;
        }

        private static string FormatActionName(string actionName)
        {
            string normalized = NormalizeActionName(actionName);
            return string.IsNullOrWhiteSpace(normalized) ? "편집" : normalized;
        }
    }

    public sealed class AnnotationHistoryToolState
    {
        public AnnotationHistoryToolState(bool canUndo, bool canRedo, string undoActionName, string redoActionName)
        {
            CanUndo = canUndo;
            CanRedo = canRedo;
            UndoActionName = undoActionName ?? string.Empty;
            RedoActionName = redoActionName ?? string.Empty;
        }

        public bool CanUndo { get; }

        public bool CanRedo { get; }

        public string UndoActionName { get; }

        public string RedoActionName { get; }
    }

    public sealed class AnnotationHistoryTransition
    {
        public AnnotationHistoryTransition(WpfAnnotationHistorySnapshot target, string displayActionName)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            DisplayActionName = string.IsNullOrWhiteSpace(displayActionName) ? "편집" : displayActionName;
        }

        public WpfAnnotationHistorySnapshot Target { get; }

        public string DisplayActionName { get; }
    }
}
