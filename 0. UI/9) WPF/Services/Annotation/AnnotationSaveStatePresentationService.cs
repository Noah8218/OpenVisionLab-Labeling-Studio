using System;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the shared dirty, saved, and waiting annotation save-state presentation contract.
    /// WPF ViewModels remain adapters that apply this contract to their own surfaces.
    /// </summary>
    public static class AnnotationSaveStatePresentationService
    {
        public static AnnotationSaveStatePresentation BuildDirty(string reason)
        {
            string normalizedReason = reason ?? string.Empty;
            return new AnnotationSaveStatePresentation(
                isDirty: true,
                statusBarText: "라벨 저장 필요",
                statusBarToolTip: $"아직 파일에 저장되지 않은 편집: {normalizedReason}",
                canvasActionText: "라벨 저장",
                canvasToolTip: "현재 이미지의 박스와 선택한 클래스를 저장합니다.",
                canvasStatusKey: "Dirty",
                canvasStatusTitleText: "저장 필요",
                canvasStatusDetailText: "현재 이미지의 라벨 편집이 아직 파일에 반영되지 않았습니다.",
                objectReviewStateKey: "Dirty",
                objectReviewBadgeText: "저장 필요",
                objectReviewDetailText: $"파일 미반영: {normalizedReason}");
        }

        public static AnnotationSaveStatePresentation BuildSaved(string reason)
        {
            string normalizedReason = string.IsNullOrWhiteSpace(reason)
                ? "현재 라벨이 파일에 저장되었습니다."
                : reason;
            return new AnnotationSaveStatePresentation(
                isDirty: false,
                statusBarText: "라벨 저장됨",
                statusBarToolTip: normalizedReason,
                canvasActionText: "저장 완료",
                canvasToolTip: "현재 이미지의 라벨이 저장되어 있습니다.",
                canvasStatusKey: "Saved",
                canvasStatusTitleText: "파일 저장됨",
                canvasStatusDetailText: "현재 이미지의 라벨이 저장 폴더에 반영되었습니다.",
                objectReviewStateKey: "Saved",
                objectReviewBadgeText: "저장됨",
                objectReviewDetailText: string.IsNullOrWhiteSpace(reason)
                    ? "현재 이미지의 라벨이 파일에 반영되었습니다."
                    : reason);
        }

        public static AnnotationSaveStatePresentation BuildWaiting()
        {
            const string statusText = "이미지를 열면 라벨 저장 상태를 표시합니다.";
            const string canvasText = "이미지를 불러오면 라벨 저장 상태를 표시합니다.";
            return new AnnotationSaveStatePresentation(
                isDirty: false,
                statusBarText: "라벨 대기",
                statusBarToolTip: statusText,
                canvasActionText: "저장 대기",
                canvasToolTip: canvasText,
                canvasStatusKey: "Waiting",
                canvasStatusTitleText: "이미지 대기",
                canvasStatusDetailText: statusText,
                objectReviewStateKey: "Waiting",
                objectReviewBadgeText: "라벨 대기",
                objectReviewDetailText: statusText);
        }

        public static AnnotationSaveStatePresentation BuildLoadBlocked(string errorSummary)
        {
            string detail = string.IsNullOrWhiteSpace(errorSummary)
                ? "원본 라벨을 보존했으며 명시적 복구 전에는 저장하지 않습니다."
                : $"원본 라벨을 보존했습니다. {errorSummary}";
            return new AnnotationSaveStatePresentation(
                isDirty: false,
                statusBarText: "라벨 읽기 오류",
                statusBarToolTip: detail,
                canvasActionText: "복구 필요",
                canvasToolTip: "손상된 라벨은 부분 로드하지 않으며 명시적 복구 전에는 덮어쓸 수 없습니다.",
                canvasStatusKey: "LoadError",
                canvasStatusTitleText: "라벨 읽기 오류",
                canvasStatusDetailText: detail,
                objectReviewStateKey: "LoadError",
                objectReviewBadgeText: "복구 필요",
                objectReviewDetailText: detail);
        }

        [Obsolete("Use BuildDirty, BuildSaved, or BuildWaiting and pass the immutable presentation.", false)]
        public static AnnotationSaveStatePresentation BuildLegacyCanvasState(
            bool isDirty,
            string actionText,
            string toolTip)
        {
            bool isWaiting = !isDirty
                && (string.IsNullOrWhiteSpace(actionText)
                    || actionText.Contains("대기", StringComparison.Ordinal));
            AnnotationSaveStatePresentation baseline = isDirty
                ? BuildDirty(string.Empty)
                : isWaiting
                    ? BuildWaiting()
                    : BuildSaved(toolTip);
            return baseline.WithCanvasAction(actionText, toolTip);
        }
    }

    public sealed class AnnotationSaveStatePresentation
    {
        public AnnotationSaveStatePresentation(
            bool isDirty,
            string statusBarText,
            string statusBarToolTip,
            string canvasActionText,
            string canvasToolTip,
            string canvasStatusKey,
            string canvasStatusTitleText,
            string canvasStatusDetailText,
            string objectReviewStateKey,
            string objectReviewBadgeText,
            string objectReviewDetailText)
        {
            IsDirty = isDirty;
            StatusBarText = statusBarText ?? string.Empty;
            StatusBarToolTip = statusBarToolTip ?? string.Empty;
            CanvasActionText = canvasActionText ?? string.Empty;
            CanvasToolTip = canvasToolTip ?? string.Empty;
            CanvasStatusKey = canvasStatusKey ?? string.Empty;
            CanvasStatusTitleText = canvasStatusTitleText ?? string.Empty;
            CanvasStatusDetailText = canvasStatusDetailText ?? string.Empty;
            ObjectReviewStateKey = objectReviewStateKey ?? string.Empty;
            ObjectReviewBadgeText = objectReviewBadgeText ?? string.Empty;
            ObjectReviewDetailText = objectReviewDetailText ?? string.Empty;
        }

        public bool IsDirty { get; }

        public string StatusBarText { get; }

        public string StatusBarToolTip { get; }

        public string CanvasActionText { get; }

        public string CanvasToolTip { get; }

        public string CanvasStatusKey { get; }

        public string CanvasStatusTitleText { get; }

        public string CanvasStatusDetailText { get; }

        public string ObjectReviewStateKey { get; }

        public string ObjectReviewBadgeText { get; }

        public string ObjectReviewDetailText { get; }

        internal AnnotationSaveStatePresentation WithCanvasAction(string actionText, string toolTip)
        {
            return new AnnotationSaveStatePresentation(
                IsDirty,
                StatusBarText,
                StatusBarToolTip,
                string.IsNullOrWhiteSpace(actionText) ? CanvasActionText : actionText,
                string.IsNullOrWhiteSpace(toolTip) ? CanvasToolTip : toolTip,
                CanvasStatusKey,
                CanvasStatusTitleText,
                CanvasStatusDetailText,
                ObjectReviewStateKey,
                ObjectReviewBadgeText,
                ObjectReviewDetailText);
        }
    }
}
