using OpenVisionLab;

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
        private bool isSelected;
        private string activeImageKey = string.Empty;

        public CanvasNoObjectCompletionPresentationSnapshot SetState(
            bool hasImage,
            bool hasLabelObjects,
            bool hasPendingCandidates,
            string imageKey = null,
            string saveStatusKey = null)
        {
            if (imageKey != null && !string.Equals(activeImageKey, imageKey, System.StringComparison.OrdinalIgnoreCase))
            {
                activeImageKey = imageKey;
                isSelected = false;
            }
            if (saveStatusKey is "Waiting" or "Unlabeled" or "Saved" or "LoadError") isSelected = false;
            if (saveStatusKey == "NoObjectSaved") isSelected = true;
            this.hasImage = hasImage;
            this.hasLabelObjects = hasLabelObjects;
            this.hasPendingCandidates = hasPendingCandidates;
            if (!hasImage || hasLabelObjects || hasPendingCandidates) isSelected = false;
            return BuildSnapshot();
        }

        public CanvasNoObjectCompletionPresentationSnapshot SetSelected(bool selected)
        {
            isSelected = selected && hasImage && !hasLabelObjects && !hasPendingCandidates;
            return BuildSnapshot();
        }

        public CanvasNoObjectCompletionPresentationSnapshot GetSnapshot()
            => BuildSnapshot();

        private CanvasNoObjectCompletionPresentationSnapshot BuildSnapshot()
        {
            string actionText = OpenVisionLanguageService.T("WpfCanvas.NoObject.Action");
            if (!hasImage)
            {
                return new CanvasNoObjectCompletionPresentationSnapshot(
                    isEnabled: false,
                    actionText: actionText,
                    toolTip: OpenVisionLanguageService.T("WpfCanvas.NoObject.NoImage.ToolTip"));
            }

            if (hasLabelObjects)
            {
                return new CanvasNoObjectCompletionPresentationSnapshot(
                    isEnabled: false,
                    actionText: actionText,
                    toolTip: OpenVisionLanguageService.T("WpfCanvas.NoObject.HasLabels.ToolTip"));
            }

            if (hasPendingCandidates)
            {
                return new CanvasNoObjectCompletionPresentationSnapshot(
                    isEnabled: false,
                    actionText: actionText,
                    toolTip: OpenVisionLanguageService.T("WpfCanvas.NoObject.HasCandidates.ToolTip"));
            }

            return new CanvasNoObjectCompletionPresentationSnapshot(
                isEnabled: true,
                actionText: actionText,
                toolTip: OpenVisionLanguageService.T("WpfCanvas.NoObject.Ready.ToolTip"),
                isSelected: isSelected);
        }
    }

    public sealed class CanvasNoObjectCompletionPresentationSnapshot
    {
        public CanvasNoObjectCompletionPresentationSnapshot(bool isEnabled, string actionText, string toolTip, bool isSelected = false)
        {
            IsEnabled = isEnabled;
            IsSelected = isSelected;
            ActionText = actionText ?? string.Empty;
            ToolTip = toolTip ?? string.Empty;
        }

        public bool IsEnabled { get; }

        public bool IsSelected { get; }

        public string ActionText { get; }

        public string ToolTip { get; }
    }
}
