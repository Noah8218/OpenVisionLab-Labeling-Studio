using MvcVisionSystem._1._Core;
using System;
using System.Globalization;

namespace MvcVisionSystem
{
    internal static class ModelRegistryTextPresentationService
    {
        public static string FormatModelPath(string path)
            => TrainingWeightsService.FormatWeightsDisplayPath(path);

        public static string FormatTrainingState(string state)
        {
            return (state ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "completed" or "complete" or "done" => "\uC644\uB8CC",
                "failed" or "error" => "\uC2E4\uD328",
                "running" or "training" => "\uC9C4\uD589 \uC911",
                "started" => "\uC2DC\uC791\uB428",
                "" => "\uBBF8\uD655\uC778",
                _ => state.Trim()
            };
        }

        public static string FormatLocalTime(string utcText)
        {
            if (DateTime.TryParse(
                utcText,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out DateTime utc))
            {
                return utc.ToLocalTime().ToString("HH:mm", CultureInfo.CurrentCulture);
            }

            return string.Empty;
        }

        public static DateTime ParseUtc(string utcText)
        {
            if (DateTime.TryParse(
                utcText,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out DateTime utc))
            {
                return utc.ToUniversalTime();
            }

            return DateTime.MinValue;
        }

        public static string FormatCandidateDecisionText(string decision)
        {
            return (decision ?? string.Empty).Trim() switch
            {
                ModelRegistryService.CandidateDecisionAdopted => "\uCC44\uD0DD",
                ModelRegistryService.CandidateDecisionRejected => "\uAC70\uC808",
                ModelRegistryService.CandidateDecisionPending => "\uB300\uAE30",
                _ => string.Empty
            };
        }
    }
}
