namespace MvcVisionSystem
{
    internal sealed class WpfMaskEditStateService
    {
        public const double CommitQueueQuietMilliseconds = 120.0;
        public const double CommitQueueDrainIntervalMilliseconds = 16.0;

        public bool IsMaskPaintTool(WpfAnnotationTool tool)
            => tool == WpfAnnotationTool.Brush || tool == WpfAnnotationTool.Eraser;

        // The FBO preview is the visible source while brush/eraser remains selected.
        // Committing CPU MaskData/history during a short pause makes labels pop in and
        // steals the UI thread from the next stroke, which is the stutter users feel.
        public bool CanProcessQueuedStrokeCommit(bool isStrokeActive, WpfAnnotationTool activeTool)
            => !isStrokeActive && !IsMaskPaintTool(activeTool);

        // Brush/eraser switches are common during segmentation. Keep the GPU preview
        // as the visible source while mask tools are active; the old Viewer2D flow
        // edits the OpenGL layer directly and does not swap to labeled overlays
        // between strokes.
        public bool ShouldPreservePreviewDuringToolSwitch(bool hasPendingCommitWork)
            => true;

        public bool ShouldDelayPreviewSwap(bool isStrokeActive, bool hasPendingCommitWork, WpfAnnotationTool activeTool)
            => isStrokeActive || hasPendingCommitWork || IsMaskPaintTool(activeTool);

        // Committed mask rows are not selected during painting; otherwise labels and
        // handles pop over the brush path while the operator is still drawing.
        public bool ShouldSelectCommittedMask(WpfAnnotationTool activeTool)
            => !IsMaskPaintTool(activeTool);
    }
}
