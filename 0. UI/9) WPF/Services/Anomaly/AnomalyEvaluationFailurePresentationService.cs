using System;
using System.Linq;

namespace MvcVisionSystem
{
    /// <summary>
    /// Converts a failed anomaly-evaluation run result into the short message
    /// shown by the WPF shell. Process diagnostics remain owned by the run service.
    /// </summary>
    public static class AnomalyEvaluationFailurePresentationService
    {
        public static string Build(AnomalyClassificationEvaluationRunResult result)
        {
            string detail = result?.Error ?? string.Empty;
            if (string.IsNullOrWhiteSpace(detail))
            {
                detail = result?.Output ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(detail))
            {
                return "\uC774\uC0C1 \uBD84\uB958 \uD3C9\uAC00 \uC2E4\uD328: \uC2E4\uD589 \uACB0\uACFC\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.";
            }

            string firstLine = detail
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault() ?? detail.Trim();
            return $"\uC774\uC0C1 \uBD84\uB958 \uD3C9\uAC00 \uC2E4\uD328: {firstLine}";
        }
    }

    [Obsolete("Use AnomalyEvaluationFailurePresentationService.", false)]
    public static class WpfAnomalyEvaluationFailurePresentationService
    {
        public static string Build(WpfAnomalyClassificationEvaluationRunResult result)
            => AnomalyEvaluationFailurePresentationService.Build(result);
    }
}
