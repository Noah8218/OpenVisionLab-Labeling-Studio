using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MvcVisionSystem
{
    /// <summary>
    /// Splits the shared annotation-tool catalog into selectable canvas tools and
    /// one-shot edit command items. The ViewModel owns the WPF collections; this
    /// service owns only the grouping projection.
    /// </summary>
    public sealed class CanvasAnnotationToolbarPresentationService
    {
        public CanvasAnnotationToolbarPresentationSnapshot Build(
            IEnumerable<WpfAnnotationToolItem> tools)
        {
            var selectableTools = new List<WpfAnnotationToolItem>();
            WpfAnnotationToolItem undoTool = null;
            WpfAnnotationToolItem redoTool = null;
            WpfAnnotationToolItem deleteTool = null;

            foreach (WpfAnnotationToolItem tool in tools ?? new WpfAnnotationToolItem[0])
            {
                if (tool == null || !AnnotationWorkflowService.IsOneShotCommandTool(tool.Tool))
                {
                    selectableTools.Add(tool);
                    continue;
                }

                switch (tool.Tool)
                {
                    case WpfAnnotationTool.Undo:
                        undoTool = tool;
                        break;

                    case WpfAnnotationTool.Redo:
                        redoTool = tool;
                        break;

                    case WpfAnnotationTool.Delete:
                        deleteTool = tool;
                        break;
                }
            }

            return new CanvasAnnotationToolbarPresentationSnapshot(
                selectableTools.AsReadOnly(),
                undoTool,
                redoTool,
                deleteTool);
        }
    }

    public sealed class CanvasAnnotationToolbarPresentationSnapshot
    {
        public CanvasAnnotationToolbarPresentationSnapshot(
            IReadOnlyList<WpfAnnotationToolItem> selectableTools,
            WpfAnnotationToolItem undoTool,
            WpfAnnotationToolItem redoTool,
            WpfAnnotationToolItem deleteTool)
        {
            SelectableTools = selectableTools ?? new ReadOnlyCollection<WpfAnnotationToolItem>(
                new List<WpfAnnotationToolItem>());
            UndoTool = undoTool;
            RedoTool = redoTool;
            DeleteTool = deleteTool;
        }

        public IReadOnlyList<WpfAnnotationToolItem> SelectableTools { get; }

        public WpfAnnotationToolItem UndoTool { get; }

        public WpfAnnotationToolItem RedoTool { get; }

        public WpfAnnotationToolItem DeleteTool { get; }
    }
}
