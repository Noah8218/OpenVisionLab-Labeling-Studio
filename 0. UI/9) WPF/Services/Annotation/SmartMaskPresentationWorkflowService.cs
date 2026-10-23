using System;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns Smart Mask panel presentation state without depending on WPF.
    /// Execution and prompt/session state remain with their existing owners.
    /// </summary>
    public sealed class SmartMaskPresentationWorkflowService
    {
        private bool isVisible;
        private bool hasSession;
        private bool isEnabled;
        private bool isAutoContourToggleEnabled;
        private bool isSessionVisible;
        private bool isCorrectionOptionsExpanded;
        private bool isCandidateComparisonVisible;
        private bool isPointActionEnabled;
        private bool isPointUndoEnabled;
        private bool isCancelEnabled;
        private bool isApplyCandidateEnabled;
        private bool isNextInstanceEnabled;
        private bool isShowInitialCandidateEnabled;
        private bool isShowLatestCandidateEnabled;
        private bool isPositivePointMode;
        private bool isNegativePointMode;
        private string actionText = "스마트 마스크";
        private string toolTip = "선택한 폴리곤 또는 박스에서 스마트 마스크 후보를 계산합니다.";
        private string promptSummaryText = "박스를 그려 첫 후보를 만드세요.";
        private string candidateComparisonText = string.Empty;

        public bool IsCorrectionOptionsExpanded => isCorrectionOptionsExpanded;

        public SmartMaskPresentationSnapshot GetSnapshot() => BuildSnapshot();

        public SmartMaskPresentationSnapshot SetState(
            bool isVisible,
            bool isEnabled,
            bool isBusy,
            string detail,
            bool hasSession)
        {
            this.isVisible = isVisible;
            this.hasSession = hasSession;
            this.isEnabled = isVisible && isEnabled && !isBusy;
            isAutoContourToggleEnabled = isVisible && !hasSession && !isBusy;
            actionText = isBusy
                ? "마스크 생성 중..."
                : hasSession
                    ? "후보 다시 생성"
                    : "스마트 마스크";
            toolTip = string.IsNullOrWhiteSpace(detail)
                ? "선택한 폴리곤 또는 결함 둘레의 박스에서 스마트 마스크 후보를 만듭니다."
                : detail;
            return BuildSnapshot();
        }

        public SmartMaskPresentationSnapshot SetSessionState(
            bool isVisible,
            bool isBusy,
            int positivePointCount,
            int negativePointCount,
            WpfSmartMaskPointInputMode inputMode,
            bool hasProducedCandidate,
            bool canMoveToNextInstance,
            bool hasCandidateComparison = false,
            WpfSmartMaskCandidateVersion selectedCandidateVersion = WpfSmartMaskCandidateVersion.Latest,
            bool canApplyCandidate = false)
        {
            isSessionVisible = isVisible;
            if (!isVisible)
            {
                isCorrectionOptionsExpanded = false;
            }

            isPointActionEnabled = isVisible && !isBusy;
            isPointUndoEnabled = isVisible && !isBusy && positivePointCount + negativePointCount > 0;
            isCancelEnabled = isVisible && isBusy;
            isApplyCandidateEnabled = isVisible && !isBusy && canApplyCandidate;
            isNextInstanceEnabled = isVisible && !isBusy && canMoveToNextInstance;
            isCandidateComparisonVisible = isVisible && hasCandidateComparison;
            isShowInitialCandidateEnabled = isVisible
                && !isBusy
                && hasCandidateComparison
                && selectedCandidateVersion != WpfSmartMaskCandidateVersion.Initial;
            isShowLatestCandidateEnabled = isVisible
                && !isBusy
                && hasCandidateComparison
                && selectedCandidateVersion != WpfSmartMaskCandidateVersion.Latest;
            candidateComparisonText = !isVisible || !hasCandidateComparison
                ? string.Empty
                : selectedCandidateVersion == WpfSmartMaskCandidateVersion.Initial
                    ? "이전 후보를 보고 있음 · 적용 후 따로 저장하세요"
                    : "현재 후보를 보고 있음 · 적용 후 따로 저장하세요";
            isPositivePointMode = inputMode == WpfSmartMaskPointInputMode.Positive;
            isNegativePointMode = inputMode == WpfSmartMaskPointInputMode.Negative;
            promptSummaryText = !isVisible
                ? "박스를 그려 첫 후보를 만드세요."
                : isBusy
                    ? "자동 후보를 계산하고 있습니다."
                    : positivePointCount + negativePointCount > 0
                        ? $"+ 포함 {positivePointCount} · − 제외 {negativePointCount} · 한 점씩 다시 생성해 비교"
                        : hasProducedCandidate || canApplyCandidate
                            ? "자동 후보 준비 · 그대로 적용하거나 필요할 때만 보정"
                            : "시작 박스로 자동 후보를 준비합니다.";
            return BuildSnapshot();
        }

        public SmartMaskPresentationSnapshot SetCorrectionOptionsExpanded(bool expanded)
        {
            isCorrectionOptionsExpanded = expanded;
            return BuildSnapshot();
        }

        private SmartMaskPresentationSnapshot BuildSnapshot()
        {
            return new SmartMaskPresentationSnapshot(
                isVisible,
                hasSession,
                isEnabled,
                isAutoContourToggleEnabled,
                isSessionVisible,
                isCorrectionOptionsExpanded,
                isCandidateComparisonVisible,
                isPointActionEnabled,
                isPointUndoEnabled,
                isCancelEnabled,
                isApplyCandidateEnabled,
                isNextInstanceEnabled,
                isShowInitialCandidateEnabled,
                isShowLatestCandidateEnabled,
                isPositivePointMode,
                isNegativePointMode,
                actionText,
                toolTip,
                promptSummaryText,
                candidateComparisonText);
        }
    }

    public sealed class SmartMaskPresentationSnapshot
    {
        public SmartMaskPresentationSnapshot(
            bool isVisible,
            bool hasSession,
            bool isEnabled,
            bool isAutoContourToggleEnabled,
            bool isSessionVisible,
            bool isCorrectionOptionsExpanded,
            bool isCandidateComparisonVisible,
            bool isPointActionEnabled,
            bool isPointUndoEnabled,
            bool isCancelEnabled,
            bool isApplyCandidateEnabled,
            bool isNextInstanceEnabled,
            bool isShowInitialCandidateEnabled,
            bool isShowLatestCandidateEnabled,
            bool isPositivePointMode,
            bool isNegativePointMode,
            string actionText,
            string toolTip,
            string promptSummaryText,
            string candidateComparisonText)
        {
            IsVisible = isVisible;
            HasSession = hasSession;
            IsEnabled = isEnabled;
            IsAutoContourToggleEnabled = isAutoContourToggleEnabled;
            IsSessionVisible = isSessionVisible;
            IsCorrectionOptionsExpanded = isCorrectionOptionsExpanded;
            IsCandidateComparisonVisible = isCandidateComparisonVisible;
            IsPointActionEnabled = isPointActionEnabled;
            IsPointUndoEnabled = isPointUndoEnabled;
            IsCancelEnabled = isCancelEnabled;
            IsApplyCandidateEnabled = isApplyCandidateEnabled;
            IsNextInstanceEnabled = isNextInstanceEnabled;
            IsShowInitialCandidateEnabled = isShowInitialCandidateEnabled;
            IsShowLatestCandidateEnabled = isShowLatestCandidateEnabled;
            IsPositivePointMode = isPositivePointMode;
            IsNegativePointMode = isNegativePointMode;
            ActionText = actionText ?? string.Empty;
            ToolTip = toolTip ?? string.Empty;
            PromptSummaryText = promptSummaryText ?? string.Empty;
            CandidateComparisonText = candidateComparisonText ?? string.Empty;
        }

        public bool IsVisible { get; }

        public bool HasSession { get; }

        public bool IsEnabled { get; }

        public bool IsAutoContourToggleEnabled { get; }

        public bool IsSessionVisible { get; }

        public bool IsCorrectionOptionsExpanded { get; }

        public bool IsCandidateComparisonVisible { get; }

        public bool IsPointActionEnabled { get; }

        public bool IsPointUndoEnabled { get; }

        public bool IsCancelEnabled { get; }

        public bool IsApplyCandidateEnabled { get; }

        public bool IsNextInstanceEnabled { get; }

        public bool IsShowInitialCandidateEnabled { get; }

        public bool IsShowLatestCandidateEnabled { get; }

        public bool IsPositivePointMode { get; }

        public bool IsNegativePointMode { get; }

        public string ActionText { get; }

        public string ToolTip { get; }

        public string PromptSummaryText { get; }

        public string CandidateComparisonText { get; }
    }
}
