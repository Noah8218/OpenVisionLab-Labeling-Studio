using System;
using System.Globalization;
using OpenVisionLab;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the active label-class card presentation without depending on WPF.
    /// ClassCatalogService remains the class collection and mutation owner.
    /// </summary>
    public sealed class CanvasLabelClassPresentationWorkflowService
    {
        private bool hasPresentationState;
        private bool hasSelectedClass;
        private string canonicalDisplayText = string.Empty;

        public CanvasLabelClassPresentationSnapshot GetSnapshot()
        {
            return BuildSnapshot();
        }

        public CanvasLabelClassPresentationSnapshot SetState(
            bool hasSelectedClass,
            string canonicalDisplayText)
        {
            hasPresentationState = true;
            this.hasSelectedClass = hasSelectedClass && !string.IsNullOrWhiteSpace(canonicalDisplayText);
            this.canonicalDisplayText = this.hasSelectedClass
                ? canonicalDisplayText.Trim()
                : string.Empty;
            return BuildSnapshot();
        }

        private CanvasLabelClassPresentationSnapshot BuildSnapshot()
        {
            if (!hasPresentationState)
            {
                return new CanvasLabelClassPresentationSnapshot(
                    isSetupMissing: true,
                    titleText: "다음 라벨 클래스",
                    detailText: "클래스를 선택하면 다음에 그리는 박스/마스크에 적용됩니다.",
                    actionText: "클래스 관리",
                    actionToolTip: "오른쪽 클래스 패널을 열어 새 라벨 이름을 추가하거나 다음 라벨 클래스를 바꿉니다.");
            }

            if (!hasSelectedClass)
            {
                return new CanvasLabelClassPresentationSnapshot(
                    isSetupMissing: true,
                    titleText: T("WpfCanvas.ActiveClass.MissingTitle"),
                    detailText: T("WpfCanvas.ActiveClass.MissingDetail"),
                    actionText: T("WpfCanvas.ActiveClass.MissingAction"),
                    actionToolTip: T("WpfCanvas.ActiveClass.MissingAction.ToolTip"));
            }

            return new CanvasLabelClassPresentationSnapshot(
                isSetupMissing: false,
                titleText: Format("WpfCanvas.ActiveClass.Title", canonicalDisplayText),
                detailText: Format("WpfCanvas.ActiveClass.Detail", canonicalDisplayText),
                actionText: T("WpfCanvas.ActiveClass.Action"),
                actionToolTip: T("WpfCanvas.ActiveClass.Action.ToolTip"));
        }

        private static string T(string key)
        {
            return OpenVisionLanguageService.T(key);
        }

        private static string Format(string key, params object[] arguments)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                T(key),
                arguments ?? Array.Empty<object>());
        }
    }

    public sealed class CanvasLabelClassPresentationSnapshot
    {
        public CanvasLabelClassPresentationSnapshot(
            bool isSetupMissing,
            string titleText,
            string detailText,
            string actionText,
            string actionToolTip)
        {
            IsSetupMissing = isSetupMissing;
            TitleText = titleText ?? string.Empty;
            DetailText = detailText ?? string.Empty;
            ActionText = actionText ?? string.Empty;
            ActionToolTip = actionToolTip ?? string.Empty;
        }

        public bool IsSetupMissing { get; }

        public string TitleText { get; }

        public string DetailText { get; }

        public string ActionText { get; }

        public string ActionToolTip { get; }
    }
}
