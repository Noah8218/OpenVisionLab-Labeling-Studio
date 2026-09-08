namespace MvcVisionSystem
{
    public enum WpfDetectionOverlayStatus
    {
        Confirmable,
        Duplicate,
        Review
    }

    /// <summary>
    /// Owns the canvas detection-result card state that is derived from an already
    /// prepared overlay presentation. The ViewModel only projects the immutable
    /// result into WPF binding properties.
    /// </summary>
    public sealed class CanvasDetectionOverlayPresentationService
    {
        public CanvasDetectionOverlayPresentationSnapshot Clear()
        {
            return new CanvasDetectionOverlayPresentationSnapshot(
                isVisible: false,
                showActions: false,
                title: string.Empty,
                summary: string.Empty,
                selectedText: string.Empty,
                detail: string.Empty,
                status: WpfDetectionOverlayStatus.Confirmable);
        }

        public CanvasDetectionOverlayPresentationSnapshot Set(
            string title,
            string summary,
            string selectedText,
            string detail,
            WpfDetectionOverlayStatus status)
        {
            return new CanvasDetectionOverlayPresentationSnapshot(
                isVisible: true,
                showActions: status == WpfDetectionOverlayStatus.Confirmable
                    || status == WpfDetectionOverlayStatus.Duplicate,
                title: string.IsNullOrWhiteSpace(title) ? "검출 결과" : title,
                summary: summary,
                selectedText: selectedText,
                detail: detail,
                status: status);
        }
    }

    public sealed class CanvasDetectionOverlayPresentationSnapshot
    {
        public CanvasDetectionOverlayPresentationSnapshot(
            bool isVisible,
            bool showActions,
            string title,
            string summary,
            string selectedText,
            string detail,
            WpfDetectionOverlayStatus status)
        {
            IsVisible = isVisible;
            ShowActions = showActions;
            Title = title ?? string.Empty;
            Summary = summary ?? string.Empty;
            SelectedText = selectedText ?? string.Empty;
            Detail = detail ?? string.Empty;
            StatusKey = status.ToString();
        }

        public bool IsVisible { get; }

        public bool ShowActions { get; }

        public string Title { get; }

        public string Summary { get; }

        public string SelectedText { get; }

        public string Detail { get; }

        public string StatusKey { get; }
    }
}
