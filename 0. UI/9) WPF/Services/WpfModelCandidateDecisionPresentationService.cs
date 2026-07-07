using System.IO;

namespace MvcVisionSystem
{
    public sealed class WpfModelCandidateDecisionPresentation
    {
        public bool CanSave { get; set; }

        public bool CanReject { get; set; }

        public string StatusText { get; set; } = string.Empty;

        public string DetailText { get; set; } = string.Empty;

        public string SaveToolTip { get; set; } = string.Empty;

        public string RejectToolTip { get; set; } = string.Empty;
    }

    public static class WpfModelCandidateDecisionPresentationService
    {
        public static WpfModelCandidateDecisionPresentation BuildPendingCandidate(
            string candidateWeightsPath,
            string baselineWeightsPath,
            bool canReject)
        {
            string candidateName = FormatModelName(candidateWeightsPath, "\uD6C4\uBCF4 \uBAA8\uB378");
            string baselineName = string.IsNullOrWhiteSpace(baselineWeightsPath)
                ? "\uAE30\uC874 \uBAA8\uB378 \uD655\uC778 \uD544\uC694"
                : FormatModelName(baselineWeightsPath, "\uAE30\uC874 \uBAA8\uB378");

            return new WpfModelCandidateDecisionPresentation
            {
                CanSave = true,
                CanReject = canReject,
                StatusText = $"\uD6C4\uBCF4 \uACB0\uC815: \uC800\uC7A5 \uB610\uB294 \uAC70\uC808 \uD544\uC694 ({candidateName})",
                DetailText = $"\uC800\uC7A5\uD558\uBA74 \uB2E4\uC74C \uCD94\uB860\uBD80\uD130 \uC774 \uD6C4\uBCF4\uB97C \uC0AC\uC6A9\uD569\uB2C8\uB2E4. \uAC70\uC808\uD558\uBA74 \uAE30\uC874 \uAC80\uC0AC \uBAA8\uB378 {baselineName}\uC744 \uC720\uC9C0\uD569\uB2C8\uB2E4.",
                SaveToolTip = "\uD559\uC2B5 \uACB0\uACFC\uB97C recipe\uC758 \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378\uB85C \uC800\uC7A5\uD569\uB2C8\uB2E4.",
                RejectToolTip = canReject
                    ? "\uD559\uC2B5 \uACB0\uACFC\uB97C \uC4F0\uC9C0 \uC54A\uACE0 \uAE30\uC874 \uAC80\uC0AC \uBAA8\uB378\uC744 \uC720\uC9C0\uD569\uB2C8\uB2E4."
                    : "\uB418\uB3CC\uB9B4 \uAE30\uC874 \uAC80\uC0AC \uBAA8\uB378 \uACBD\uB85C\uAC00 \uC5C6\uC5B4 \uAC70\uC808\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4."
            };
        }

        public static WpfModelCandidateDecisionPresentation BuildRejectedCandidate(
            string candidateWeightsPath,
            string decisionSummary)
        {
            return new WpfModelCandidateDecisionPresentation
            {
                CanSave = false,
                CanReject = false,
                StatusText = $"\uD6C4\uBCF4 \uACB0\uC815: \uAC70\uC808\uB428 ({FormatModelName(candidateWeightsPath, "\uD6C4\uBCF4 \uBAA8\uB378")})",
                DetailText = string.IsNullOrWhiteSpace(decisionSummary)
                    ? "\uC774 \uD6C4\uBCF4\uB294 \uD604\uC7AC recipe\uC758 \uAC80\uC0AC \uBAA8\uB378\uB85C \uCC44\uD0DD\uD558\uC9C0 \uC54A\uC558\uC2B5\uB2C8\uB2E4."
                    : decisionSummary.Trim(),
                SaveToolTip = "\uC774\uBBF8 \uAC70\uC808\uB41C \uD6C4\uBCF4\uC785\uB2C8\uB2E4. \uB2E4\uC2DC \uC4F0\uB824\uBA74 \uBAA8\uB378 \uC124\uC815\uC5D0\uC11C \uC9C1\uC811 \uC120\uD0DD\uD558\uC138\uC694.",
                RejectToolTip = "\uC774\uBBF8 \uAC70\uC808\uB41C \uD6C4\uBCF4\uC785\uB2C8\uB2E4."
            };
        }

        public static WpfModelCandidateDecisionPresentation BuildSavedCandidate(string candidateWeightsPath)
        {
            return new WpfModelCandidateDecisionPresentation
            {
                CanSave = false,
                CanReject = false,
                StatusText = $"\uD6C4\uBCF4 \uACB0\uC815: \uAC80\uC0AC \uBAA8\uB378\uB85C \uC800\uC7A5\uB428 ({FormatModelName(candidateWeightsPath, "\uD6C4\uBCF4 \uBAA8\uB378")})",
                DetailText = "\uC774 \uD6C4\uBCF4\uB294 recipe\uC758 \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378 \uC774\uB825\uC5D0 \uAE30\uB85D\uB418\uC5B4 \uC788\uC2B5\uB2C8\uB2E4.",
                SaveToolTip = "\uC774\uBBF8 \uAC80\uC0AC \uBAA8\uB378\uB85C \uC800\uC7A5\uB41C \uD6C4\uBCF4\uC785\uB2C8\uB2E4.",
                RejectToolTip = "\uC774\uBBF8 \uC800\uC7A5\uB41C \uD6C4\uBCF4\uB294 \uC5EC\uAE30\uC11C \uAC70\uC808\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4."
            };
        }

        public static WpfModelCandidateDecisionPresentation BuildReviewAvailable()
        {
            return new WpfModelCandidateDecisionPresentation
            {
                CanSave = false,
                CanReject = false,
                StatusText = "\uD6C4\uBCF4 \uACB0\uC815: \uAC80\uD1A0 \uAC00\uB2A5",
                DetailText = "\uD6C4\uBCF4 \uAC80\uC99D\uC744 \uC2E4\uD589\uD574 \uAE30\uC874 \uAC80\uC0AC \uBAA8\uB378\uACFC \uBE44\uAD50\uD55C \uB4A4 \uC800\uC7A5 \uC5EC\uBD80\uB97C \uACB0\uC815\uD558\uC138\uC694.",
                SaveToolTip = "\uBA3C\uC800 \uD6C4\uBCF4 \uAC80\uC99D\uC73C\uB85C \uD559\uC2B5 \uACB0\uACFC\uB97C \uD655\uC778\uD558\uC138\uC694.",
                RejectToolTip = "\uBA3C\uC800 \uD6C4\uBCF4 \uAC80\uC99D\uC73C\uB85C \uD559\uC2B5 \uACB0\uACFC\uB97C \uD655\uC778\uD558\uC138\uC694."
            };
        }

        public static WpfModelCandidateDecisionPresentation BuildNoCandidate()
        {
            return new WpfModelCandidateDecisionPresentation
            {
                CanSave = false,
                CanReject = false,
                StatusText = "\uD6C4\uBCF4 \uACB0\uC815: \uD6C4\uBCF4 \uC5C6\uC74C",
                DetailText = "\uD559\uC2B5\uC774 \uC644\uB8CC\uB418\uC5B4 \uBAA8\uB378 \uD6C4\uBCF4\uAC00 \uC0DD\uAE30\uBA74 \uC5EC\uAE30\uC5D0\uC11C \uC800\uC7A5 \uB610\uB294 \uAC70\uC808 \uACB0\uC815\uC744 \uB0A8\uAE38 \uC218 \uC788\uC2B5\uB2C8\uB2E4.",
                SaveToolTip = "\uC800\uC7A5\uD560 \uD559\uC2B5 \uBAA8\uB378 \uD6C4\uBCF4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.",
                RejectToolTip = "\uAC70\uC808\uD560 \uD559\uC2B5 \uBAA8\uB378 \uD6C4\uBCF4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4."
            };
        }

        public static string BuildNoRejectCandidateStatus()
        {
            return "\uAC70\uC808\uD560 \uD559\uC2B5 \uBAA8\uB378 \uD6C4\uBCF4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.";
        }

        public static string BuildRejectDecisionSummary()
        {
            return "\uD6C4\uBCF4 \uAC70\uC808, \uAE30\uC874 \uAC80\uC0AC \uBAA8\uB378 \uC720\uC9C0";
        }

        public static string BuildRejectCommandStatus(string candidateWeightsPath, bool configSaved)
        {
            string candidateName = FormatModelName(candidateWeightsPath, "\uD6C4\uBCF4 \uBAA8\uB378");
            return configSaved
                ? $"\uD559\uC2B5 \uBAA8\uB378 \uD6C4\uBCF4\uB97C \uAC70\uC808\uD588\uC2B5\uB2C8\uB2E4: {candidateName}. \uD604\uC7AC \uAC80\uC0AC \uBAA8\uB378\uC744 \uC720\uC9C0\uD569\uB2C8\uB2E4."
                : $"\uD559\uC2B5 \uBAA8\uB378 \uD6C4\uBCF4\uB97C \uAC70\uC808\uD588\uC2B5\uB2C8\uB2E4: {candidateName}. Recipe \uC800\uC7A5\uC740 \uBCC4\uB3C4 \uD655\uC778\uC774 \uD544\uC694\uD569\uB2C8\uB2E4.";
        }

        public static string BuildRejectProjectConfigStatus(bool configSaved)
        {
            return configSaved
                ? "\uD559\uC2B5 \uBAA8\uB378 \uD6C4\uBCF4 \uAC70\uC808 \uAE30\uB85D \uC800\uC7A5 \uC644\uB8CC."
                : "\uD559\uC2B5 \uBAA8\uB378 \uD6C4\uBCF4 \uAC70\uC808 \uAE30\uB85D\uC740 \uBA54\uBAA8\uB9AC\uC5D0 \uBC18\uC601\uB418\uC5C8\uC9C0\uB9CC recipe \uC800\uC7A5\uC740 \uC2E4\uD328\uD588\uC2B5\uB2C8\uB2E4.";
        }

        public static string BuildRejectLog(string candidateWeightsPath, string baselineWeightsPath)
        {
            return $"\uD559\uC2B5 \uBAA8\uB378 \uD6C4\uBCF4 \uAC70\uC808: {candidateWeightsPath} / baseline={baselineWeightsPath}";
        }

        public static string BuildRejectFailureStatus(string message)
        {
            return $"\uD559\uC2B5 \uBAA8\uB378 \uD6C4\uBCF4 \uAC70\uC808 \uC2E4\uD328: {NormalizeMessage(message)}";
        }

        private static string FormatModelName(string path, string fallback)
        {
            string name = string.IsNullOrWhiteSpace(path) ? string.Empty : Path.GetFileName(path.Trim());
            return string.IsNullOrWhiteSpace(name) ? fallback : name;
        }

        private static string NormalizeMessage(string message)
        {
            return string.IsNullOrWhiteSpace(message)
                ? "\uC0C1\uC138 \uC6D0\uC778\uC744 \uD655\uC778\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4."
                : message.Trim();
        }
    }
}
