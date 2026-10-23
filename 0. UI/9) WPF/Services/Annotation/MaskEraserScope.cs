namespace MvcVisionSystem
{
    /// <summary>
    /// Limits normal eraser strokes without changing the default visible-editable
    /// behavior. The scope is captured when a stroke is queued.
    /// </summary>
    public enum MaskEraserScope
    {
        VisibleEditable,
        CurrentClass,
        SelectedObject
    }
}
