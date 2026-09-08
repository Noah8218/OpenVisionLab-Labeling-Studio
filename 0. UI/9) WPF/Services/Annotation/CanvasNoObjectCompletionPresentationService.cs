namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the canvas toolbar decision for completing an image as object-free.
    /// The ViewModel only projects this snapshot into WPF bindings.
    /// </summary>
    public sealed class CanvasNoObjectCompletionPresentationService
    {
        private bool hasImage;
        private bool hasLabelObjects;
        private bool hasPendingCandidates;

        public CanvasNoObjectCompletionPresentationSnapshot SetState(
            bool hasImage,
            bool hasLabelObjects,
            bool hasPendingCandidates)
        {
            this.hasImage = hasImage;
            this.hasLabelObjects = hasLabelObjects;
            this.hasPendingCandidates = hasPendingCandidates;
            return BuildSnapshot();
        }

        public CanvasNoObjectCompletionPresentationSnapshot GetSnapshot()
            => BuildSnapshot();

        private CanvasNoObjectCompletionPresentationSnapshot BuildSnapshot()
        {
            if (!hasImage)
            {
                return new CanvasNoObjectCompletionPresentationSnapshot(
                    isEnabled: false,
                    actionText: "객체 없음",
                    toolTip: "이미지를 먼저 열면 객체 없음으로 완료할 수 있습니다.");
            }

            if (hasLabelObjects)
            {
                return new CanvasNoObjectCompletionPresentationSnapshot(
                    isEnabled: false,
                    actionText: "객체 없음",
                    toolTip: "이미 라벨된 객체가 있습니다. 객체 없음으로 완료하려면 기존 라벨을 먼저 삭제하세요.");
            }

            if (hasPendingCandidates)
            {
                return new CanvasNoObjectCompletionPresentationSnapshot(
                    isEnabled: false,
                    actionText: "객체 없음",
                    toolTip: "남은 AI 후보가 있습니다. 후보를 확정하거나 숨긴 뒤 객체 없음으로 완료하세요.");
            }

            return new CanvasNoObjectCompletionPresentationSnapshot(
                isEnabled: true,
                actionText: "객체 없음",
                toolTip: "라벨을 만들지 않고 빈 YOLO 라벨 파일을 저장한 뒤 다음 미완료 이미지로 이동합니다.");
        }
    }

    public sealed class CanvasNoObjectCompletionPresentationSnapshot
    {
        public CanvasNoObjectCompletionPresentationSnapshot(bool isEnabled, string actionText, string toolTip)
        {
            IsEnabled = isEnabled;
            ActionText = actionText ?? string.Empty;
            ToolTip = toolTip ?? string.Empty;
        }

        public bool IsEnabled { get; }

        public string ActionText { get; }

        public string ToolTip { get; }
    }
}
