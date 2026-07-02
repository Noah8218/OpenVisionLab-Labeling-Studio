using System.Globalization;

namespace MvcVisionSystem
{
    public sealed class WpfCandidateReviewCompletionPresentationService
    {
        public WpfCandidateReviewCompletionPresentation Build(
            bool hasImage,
            bool isDetecting,
            int pendingCandidateCount,
            int savedLabelCount,
            bool hasUnsavedLabels)
        {
            if (!hasImage)
            {
                return new WpfCandidateReviewCompletionPresentation(
                    "\uAC80\uD1A0 \uB300\uAE30",
                    "\uC774\uBBF8\uC9C0\uB97C \uC5F4\uAC70\uB098 \uD604\uC7AC \uC774\uBBF8\uC9C0\uB97C \uAC80\uC0AC\uD558\uBA74 \uD6C4\uBCF4 \uAC80\uD1A0 \uC0C1\uD0DC\uAC00 \uD45C\uC2DC\uB429\uB2C8\uB2E4.",
                    "\uB2E4\uC74C: \uC774\uBBF8\uC9C0 \uC120\uD0DD \uB610\uB294 \uAC80\uC0AC",
                    "\uC774\uBBF8\uC9C0 \uC644\uB8CC",
                    "\uC644\uB8CC\uD560 \uC774\uBBF8\uC9C0\uB97C \uBA3C\uC800 \uC5F4\uC5B4\uC8FC\uC138\uC694.",
                    canComplete: false);
            }

            if (isDetecting)
            {
                return new WpfCandidateReviewCompletionPresentation(
                    "\uAC80\uC0AC \uC9C4\uD589 \uC911",
                    "\uBAA8\uB378 \uC751\uB2F5\uC744 \uAE30\uB2E4\uB9AC\uB294 \uC911\uC785\uB2C8\uB2E4. \uACB0\uACFC\uAC00 \uB3C4\uCC29\uD558\uBA74 \uD6C4\uBCF4 \uAC80\uD1A0 \uC0C1\uD0DC\uAC00 \uAC31\uC2E0\uB429\uB2C8\uB2E4.",
                    "\uB2E4\uC74C: \uAC80\uC0AC \uC644\uB8CC \uB300\uAE30",
                    "\uAC80\uC0AC \uB300\uAE30",
                    "\uAC80\uC0AC\uAC00 \uB05D\uB09C \uB4A4 \uC644\uB8CC\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.",
                    canComplete: false);
            }

            if (pendingCandidateCount > 0)
            {
                string pendingText = pendingCandidateCount.ToString(CultureInfo.CurrentCulture);
                return new WpfCandidateReviewCompletionPresentation(
                    "\uD6C4\uBCF4 \uAC80\uD1A0 \uC911",
                    string.Format(CultureInfo.CurrentCulture, "\uB0A8\uC740 AI \uD6C4\uBCF4 {0}\uAC1C\uAC00 \uC788\uC2B5\uB2C8\uB2E4. \uD655\uC815\uD558\uAC70\uB098 \uD6C4\uBCF4 \uC228\uAE40 \uD6C4 \uC774\uBBF8\uC9C0\uB97C \uC644\uB8CC\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.", pendingText),
                    "\uB2E4\uC74C: \uB77C\uBCA8 \uD655\uC815 \uB610\uB294 \uD6C4\uBCF4 \uC228\uAE40",
                    "\uD6C4\uBCF4 \uAC80\uD1A0 \uD544\uC694",
                    "\uB0A8\uC740 AI \uD6C4\uBCF4\uB97C \uD655\uC815\uD558\uAC70\uB098 \uD6C4\uBCF4 \uC228\uAE40\uD55C \uB4A4 \uC644\uB8CC\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.",
                    canComplete: false);
            }

            if (savedLabelCount > 0)
            {
                string labelText = savedLabelCount.ToString(CultureInfo.CurrentCulture);
                bool shouldSave = hasUnsavedLabels;
                return new WpfCandidateReviewCompletionPresentation(
                    shouldSave ? "\uB77C\uBCA8 \uC800\uC7A5 \uD544\uC694" : "\uB77C\uBCA8 \uC800\uC7A5 \uC644\uB8CC",
                    string.Format(
                        CultureInfo.CurrentCulture,
                        shouldSave
                            ? "AI \uD6C4\uBCF4 \uAC80\uD1A0\uAC00 \uB05D\uB0AC\uACE0 \uC800\uC7A5 \uB77C\uBCA8 {0}\uAC1C\uAC00 \uC788\uC2B5\uB2C8\uB2E4. \uC544\uC9C1 \uD30C\uC77C\uC5D0 \uBC18\uC601\uB418\uC9C0 \uC54A\uC740 \uBCC0\uACBD\uC774 \uC788\uC73C\uB2C8 \uC774\uBBF8\uC9C0 \uC644\uB8CC\uB97C \uB204\uB974\uBA74 \uC800\uC7A5 \uD6C4 \uB2E4\uC74C \uBBF8\uC644\uB8CC \uC774\uBBF8\uC9C0\uB85C \uC774\uB3D9\uD569\uB2C8\uB2E4."
                            : "AI \uD6C4\uBCF4 \uAC80\uD1A0\uAC00 \uB05D\uB0AC\uACE0 \uC800\uC7A5 \uB77C\uBCA8 {0}\uAC1C\uAC00 \uD30C\uC77C\uC5D0 \uBC18\uC601\uB418\uC5B4 \uC788\uC2B5\uB2C8\uB2E4. \uB2E4\uC74C \uBBF8\uC644\uB8CC \uC774\uBBF8\uC9C0\uB85C \uC774\uB3D9\uD560 \uC218 \uC788\uC2B5\uB2C8\uB2E4.",
                        labelText),
                    shouldSave ? "\uB2E4\uC74C: \uB77C\uBCA8 \uC800\uC7A5 \uD6C4 \uB2E4\uC74C \uBBF8\uC644\uB8CC \uC774\uBBF8\uC9C0" : "\uB2E4\uC74C: \uB2E4\uC74C \uBBF8\uC644\uB8CC \uC774\uBBF8\uC9C0",
                    shouldSave ? "\uB77C\uBCA8 \uC800\uC7A5 \uD6C4 \uB2E4\uC74C" : "\uB2E4\uC74C \uBBF8\uC644\uB8CC",
                    shouldSave ? "\uD604\uC7AC \uB77C\uBCA8\uC744 \uC800\uC7A5\uD558\uACE0 \uB2E4\uC74C \uBBF8\uC644\uB8CC \uC774\uBBF8\uC9C0\uB85C \uC774\uB3D9\uD569\uB2C8\uB2E4." : "\uD604\uC7AC \uB77C\uBCA8\uC740 \uC800\uC7A5\uB418\uC5B4 \uC788\uC2B5\uB2C8\uB2E4. \uB2E4\uC74C \uBBF8\uC644\uB8CC \uC774\uBBF8\uC9C0\uB85C \uC774\uB3D9\uD569\uB2C8\uB2E4.",
                    canComplete: true);
            }

            return new WpfCandidateReviewCompletionPresentation(
                "\uAC1D\uCCB4 \uC5C6\uC74C \uC644\uB8CC \uC900\uBE44",
                "\uB0A8\uC740 AI \uD6C4\uBCF4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4. \uC774\uBBF8\uC9C0 \uC644\uB8CC\uB97C \uB204\uB974\uBA74 \uAC1D\uCCB4 \uC5C6\uC74C\uC73C\uB85C \uC800\uC7A5\uD558\uACE0 \uB2E4\uC74C \uBBF8\uC644\uB8CC \uC774\uBBF8\uC9C0\uB85C \uC774\uB3D9\uD569\uB2C8\uB2E4.",
                "\uB2E4\uC74C: \uAC1D\uCCB4 \uC5C6\uC74C \uC800\uC7A5 \uD6C4 \uB2E4\uC74C \uBBF8\uC644\uB8CC \uC774\uBBF8\uC9C0",
                "\uAC1D\uCCB4 \uC5C6\uC74C \uC800\uC7A5",
                "\uAC1D\uCCB4 \uC5C6\uC74C\uC73C\uB85C \uC800\uC7A5\uD558\uACE0 \uB2E4\uC74C \uBBF8\uC644\uB8CC \uC774\uBBF8\uC9C0\uB85C \uC774\uB3D9\uD569\uB2C8\uB2E4.",
                canComplete: true);
        }
    }

    public sealed class WpfCandidateReviewCompletionPresentation
    {
        public WpfCandidateReviewCompletionPresentation(
            string titleText,
            string detailText,
            string nextActionText,
            string buttonText,
            string toolTip,
            bool canComplete)
        {
            TitleText = titleText ?? string.Empty;
            DetailText = detailText ?? string.Empty;
            NextActionText = nextActionText ?? string.Empty;
            ButtonText = buttonText ?? string.Empty;
            ToolTip = toolTip ?? string.Empty;
            CanComplete = canComplete;
        }

        public string TitleText { get; }

        public string DetailText { get; }

        public string NextActionText { get; }

        public string ButtonText { get; }

        public string ToolTip { get; }

        public bool CanComplete { get; }
    }
}
