using MvcVisionSystem._1._Core;
using System;
using System.Globalization;
using System.Linq;

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
                "completed" or "complete" or "done" => OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.TrainingState.Completed"),
                "failed" or "error" => OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.TrainingState.Failed"),
                "running" or "training" => OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.TrainingState.Running"),
                "started" => OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.TrainingState.Started"),
                "" => OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.TrainingState.Unknown"),
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

        public static string FormatPendingDecisionReason(string decision, string summary)
        {
            string raw = summary ?? string.Empty;
            const string key = "WpfModelRegistry.Reason.Pending";
            if (!string.Equals(decision, ModelRegistryService.CandidateDecisionPending, StringComparison.Ordinal)) return raw;
            if (!OpenVisionLab.OpenVisionLanguageService.GetEntries().Any(item => string.Equals(item.Key, key, StringComparison.Ordinal))) return raw;
            string currentKorean = OpenVisionLab.OpenVisionLanguageService.GetDefaultEntries().FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.Ordinal))?.Korean;
            if (!string.Equals(raw, currentKorean, StringComparison.Ordinal)
                && !OpenVisionLab.OpenVisionLanguageService.GetPreviousKoreanDefaults(key).Contains(raw, StringComparer.Ordinal)) return raw;
            return OpenVisionLab.OpenVisionLanguageService.T(key);
        }

        public static string FormatCandidateDecisionText(string decision)
        {
            return (decision ?? string.Empty).Trim() switch
            {
                ModelRegistryService.CandidateDecisionAdopted => OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.Decision.Assigned"),
                ModelRegistryService.CandidateDecisionRejected => OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.Decision.Rejected"),
                ModelRegistryService.CandidateDecisionPending => OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.Decision.Pending"),
                _ => string.Empty
            };
        }
    }
}
