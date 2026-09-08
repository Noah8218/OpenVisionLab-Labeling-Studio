namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the canvas annotation-tool selection state that is independent of WPF.
    /// The ViewModel converts the snapshot into binding properties and keeps the tool items.
    /// </summary>
    public sealed class CanvasAnnotationToolSelectionWorkflowService
    {
        private WpfAnnotationTool? selectedTool;
        private WpfAnnotationTool? lastDrawingTool;
        private string lastLabelClassName = string.Empty;

        public CanvasAnnotationToolSelectionSnapshot GetSnapshot()
            => BuildSnapshot();

        public CanvasAnnotationToolSelectionSnapshot SetSelectedTool(WpfAnnotationTool tool)
        {
            if (AnnotationWorkflowService.IsOneShotCommandTool(tool))
            {
                return BuildSnapshot();
            }

            selectedTool = tool;
            if (AnnotationProductivityService.IsRepeatableDrawingTool(tool))
            {
                lastDrawingTool = tool;
            }

            return BuildSnapshot();
        }

        public CanvasAnnotationToolSelectionSnapshot ClearSelectedTool()
        {
            selectedTool = null;
            return BuildSnapshot();
        }

        public void SetSelectedLabelClass(string className)
        {
            lastLabelClassName = className ?? string.Empty;
        }

        public bool TryGetRepeatSelection(out WpfAnnotationTool tool, out string className)
        {
            tool = lastDrawingTool ?? WpfAnnotationTool.Select;
            className = lastLabelClassName;
            return lastDrawingTool.HasValue && !string.IsNullOrWhiteSpace(className);
        }

        private CanvasAnnotationToolSelectionSnapshot BuildSnapshot()
            => new CanvasAnnotationToolSelectionSnapshot(
                selectedTool,
                selectedTool == WpfAnnotationTool.Brush || selectedTool == WpfAnnotationTool.Eraser,
                lastDrawingTool,
                lastLabelClassName);
    }

    public sealed class CanvasAnnotationToolSelectionSnapshot
    {
        public CanvasAnnotationToolSelectionSnapshot(
            WpfAnnotationTool? selectedTool,
            bool isMaskBrushControlVisible,
            WpfAnnotationTool? lastDrawingTool,
            string lastLabelClassName)
        {
            SelectedTool = selectedTool;
            IsMaskBrushControlVisible = isMaskBrushControlVisible;
            LastDrawingTool = lastDrawingTool;
            LastLabelClassName = lastLabelClassName ?? string.Empty;
        }

        public WpfAnnotationTool? SelectedTool { get; }

        public bool IsMaskBrushControlVisible { get; }

        public WpfAnnotationTool? LastDrawingTool { get; }

        public string LastLabelClassName { get; }
    }
}
