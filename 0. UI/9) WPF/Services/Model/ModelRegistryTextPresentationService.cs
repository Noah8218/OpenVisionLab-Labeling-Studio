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

        public static string FormatModelCaption(string engine, string runName, string eventUtc, string weightsPath,
            string modelId = null, bool hasTrainingRun = true, string trainingRunId = null)
        {
            string kind = (modelId ?? string.Empty).ToLowerInvariant() switch
            {
                "yolov5s" => "YOLOv5s", "yolov8n" => "YOLOv8n", "yolov8s" => "YOLOv8s",
                "yolo11n" => "YOLO11n", "yolo11s" => "YOLO11s", "yolo11m" => "YOLO11m", "patchcore" => "PatchCore (WRN50)",
                "yolov8s-seg" => "YOLOv8s-seg", "yolo11s-seg" => "YOLO11s-seg",
                "yolov8s-cls" => "YOLOv8s-cls", "yolo11s-cls" => "YOLO11s-cls",
                _ => string.IsNullOrWhiteSpace(engine) ? string.Empty : PythonModelSettings.FormatModelEngineName(engine)
            };
            string name = (runName ?? string.Empty).Trim();
            if (name.Equals(trainingRunId, StringComparison.Ordinal)) name = string.Empty;
            if (name.Length == 0)
            {
                string directoryName = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(weightsPath ?? string.Empty));
                name = directoryName?.Length == 64 && directoryName.All(Uri.IsHexDigit)
                    ? System.IO.Path.GetFileName(weightsPath) : FormatModelPath(weightsPath);
            }
            string caption = kind.Length == 0 ? name : name.Length == 0 || name.Equals(kind, StringComparison.OrdinalIgnoreCase)
                || name.Equals(modelId, StringComparison.OrdinalIgnoreCase) || name.Equals(engine, StringComparison.OrdinalIgnoreCase)
                ? kind : kind + " · " + name;
            if (!hasTrainingRun) return caption;
            // EventUtc can change when a model record is updated, including adoption fallback.
            string time = DateTime.TryParseExact(eventUtc, new[] { "yyyy-MM-ddTHH:mm:ssK", "yyyy-MM-ddTHH:mm:ss.FFFFFFFK" },
                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime utc)
                ? string.Format(CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.RecordedTime"), utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture))
                : OpenVisionLab.OpenVisionLanguageService.T("WpfModelRegistry.RunTimeUnknown");
            return caption + " · " + time;
        }

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
