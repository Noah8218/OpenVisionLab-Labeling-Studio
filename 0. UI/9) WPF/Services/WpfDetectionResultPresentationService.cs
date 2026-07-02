using System.Globalization;
using System.IO;

namespace MvcVisionSystem
{
    public sealed class WpfDetectionResultPresentationService
    {
        // Detection result wording is kept out of the shell so workflow refactors do not duplicate operator-facing status text.
        public string BuildCandidateLoadHistory(int candidateCount, bool succeeded, double confidenceFilter)
        {
            if (!succeeded)
            {
                return "\uD6C4\uBCF4 \uB85C\uB4DC \uC2E4\uD328: \uCD94\uB860 \uACB0\uACFC\uB97C \uD655\uC778\uD558\uC138\uC694";
            }

            return candidateCount == 0
                ? "AI \uD6C4\uBCF4 \uB85C\uB4DC: AI \uD6C4\uBCF4 \uC5C6\uC74C"
                : string.Format(
                    CultureInfo.CurrentCulture,
                    "AI \uD6C4\uBCF4 \uB85C\uB4DC: {0}\uAC1C / \uC800\uC7A5 \uC804 / \uAE30\uC900 {1:P0}",
                    candidateCount,
                    confidenceFilter);
        }

        public WpfDetectionOverlayPresentation BuildNoCandidateOverlay(string imagePath, double confidenceFilter)
        {
            string imageName = ResolveImageName(imagePath);
            return new WpfDetectionOverlayPresentation(
                isEmpty: false,
                title: "AI \uD6C4\uBCF4 \uACB0\uACFC",
                summary: string.Format(
                    CultureInfo.CurrentCulture,
                    "{0} / AI \uD6C4\uBCF4 0\uAC1C / \uC800\uC7A5 \uC804 / \uAE30\uC900 {1:P0}",
                    imageName,
                    confidenceFilter),
                selectedText: "\uACB0\uACFC: AI \uD6C4\uBCF4 \uC5C6\uC74C",
                detail: "\uD604\uC7AC \uAE30\uC900 \uC2E0\uB8B0\uB3C4 \uC774\uC0C1\uC73C\uB85C \uD45C\uC2DC\uD560 \uC800\uC7A5 \uC804 AI \uD6C4\uBCF4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.",
                status: WpfDetectionOverlayStatus.Review);
        }

        public WpfDetectionOverlayPresentation BuildFailureOverlay(string imagePath, string summary)
        {
            string imageName = ResolveImageName(imagePath);
            string failureSummary = WpfImageQueuePresenter.TranslateDetectionMessage(summary);
            return new WpfDetectionOverlayPresentation(
                isEmpty: false,
                title: "\uAC80\uC0AC \uC2E4\uD328",
                summary: $"{imageName} / \uAC80\uC0AC \uC2E4\uD328",
                selectedText: $"\uACB0\uACFC: {failureSummary}",
                detail: $"\uC2E4\uD328 \uC6D0\uC778: {failureSummary} / \uBAA8\uB378 \uC124\uC815, \uCD94\uB860 \uC2E4\uD589 \uC0C1\uD0DC, \uC774\uBBF8\uC9C0 \uACBD\uB85C\uB97C \uD655\uC778\uD558\uC138\uC694.",
                status: WpfDetectionOverlayStatus.Review);
        }

        private static string ResolveImageName(string imagePath)
        {
            return string.IsNullOrWhiteSpace(imagePath)
                ? "-"
                : Path.GetFileName(imagePath);
        }
    }
}
