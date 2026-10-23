namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the canvas layout policy for image-level anomaly review without
    /// depending on WPF types or the dataset-purpose state owner.
    /// </summary>
    public sealed class CanvasAnomalyReviewPresentationService
    {
        public const double StandardAnnotationToolRailWidth = 52D;
        public const double AnomalyAnnotationToolRailWidth = StandardAnnotationToolRailWidth;

        public CanvasAnomalyReviewPresentationSnapshot Build(bool isAnomalyImageReview)
        {
            return new CanvasAnomalyReviewPresentationSnapshot(
                isWorkspaceVisible: true,
                annotationToolRailWidth: isAnomalyImageReview
                    ? AnomalyAnnotationToolRailWidth
                    : StandardAnnotationToolRailWidth);
        }
    }

    public sealed class CanvasAnomalyReviewPresentationSnapshot
    {
        public CanvasAnomalyReviewPresentationSnapshot(bool isWorkspaceVisible, double annotationToolRailWidth)
        {
            IsWorkspaceVisible = isWorkspaceVisible;
            AnnotationToolRailWidth = annotationToolRailWidth;
        }

        public bool IsWorkspaceVisible { get; }

        public double AnnotationToolRailWidth { get; }
    }
}
