using System.IO;
using MvcVisionSystem._1._Core;

namespace MvcVisionSystem
{
    public static class WpfInferenceStatusPresentationService
    {
        public static string BuildStatusText(
            string statusText,
            PythonModelSettings settings,
            bool hasPendingModelCandidate)
        {
            string baseText = string.IsNullOrWhiteSpace(statusText) ? "\uB300\uAE30" : statusText.Trim();
            string modelText = BuildModelText(settings, hasPendingModelCandidate);
            return string.IsNullOrWhiteSpace(modelText)
                ? baseText
                : $"{baseText} / {modelText}";
        }

        public static string BuildToolTip(
            string statusText,
            PythonModelSettings settings,
            bool hasPendingModelCandidate)
        {
            string weightsPath = settings?.WeightsPath ?? string.Empty;
            string modelText = BuildModelText(settings, hasPendingModelCandidate);
            string modelPathText = string.IsNullOrWhiteSpace(weightsPath)
                ? "\uBAA8\uB378 \uACBD\uB85C \uC5C6\uC74C"
                : weightsPath;
            string runtimeText = "\uC2E4\uD589\uAE30: " + BuildRuntimeLabel(settings);
            string statusLine = string.IsNullOrWhiteSpace(statusText)
                ? "\uCD94\uB860 \uC0C1\uD0DC: \uB300\uAE30"
                : "\uCD94\uB860 \uC0C1\uD0DC: " + statusText.Trim();
            return $"{statusLine}\n{modelText}\n{runtimeText}\n{modelPathText}";
        }

        public static string BuildInspectionModelStatusText(
            PythonModelSettings settings,
            bool hasPendingModelCandidate)
        {
            string weightsPath = settings?.WeightsPath ?? string.Empty;
            if (string.IsNullOrWhiteSpace(weightsPath) || !File.Exists(weightsPath))
            {
                return "\uAC80\uC0AC \uBAA8\uB378: \uC5C6\uC74C";
            }

            string displayPath = WpfTrainingWeightsService.FormatWeightsDisplayPath(weightsPath);
            string runtimeLabel = BuildRuntimeLabel(settings);
            return hasPendingModelCandidate
                ? $"\uAC80\uC0AC \uD6C4\uBCF4: {runtimeLabel} / {displayPath}"
                : $"\uAC80\uC0AC \uBAA8\uB378: {runtimeLabel} / {displayPath}";
        }

        public static string BuildInspectionModelToolTip(
            PythonModelSettings settings,
            bool hasPendingModelCandidate)
        {
            string weightsPath = settings?.WeightsPath ?? string.Empty;
            if (string.IsNullOrWhiteSpace(weightsPath) || !File.Exists(weightsPath))
            {
                return "\uAC80\uC0AC\uC5D0 \uC0AC\uC6A9\uD560 \uBAA8\uB378 \uD30C\uC77C\uC744 YOLO \uC124\uC815\uC5D0\uC11C \uC120\uD0DD\uD558\uC138\uC694.";
            }

            string roleText = hasPendingModelCandidate
                ? "\uC120\uD0DD\uB41C \uD559\uC2B5 \uACB0\uACFC \uD6C4\uBCF4\uC785\uB2C8\uB2E4. \uAC80\uC0AC \uBAA8\uB378\uB85C \uD655\uC815\uD558\uB824\uBA74 YOLO \uC124\uC815\uC744 \uC800\uC7A5\uD558\uC138\uC694."
                : "\uD604\uC7AC \uCD94\uB860/\uAC80\uC0AC\uC5D0 \uC0AC\uC6A9\uD558\uB294 \uBAA8\uB378\uC785\uB2C8\uB2E4.";
            return $"{roleText}\n\uC2E4\uD589\uAE30: {BuildRuntimeLabel(settings)}\n\uBAA8\uB378 \uD30C\uC77C: {weightsPath}";
        }

        public static string BuildRuntimeModelLabel(PythonModelSettings settings)
        {
            string runtimeLabel = BuildRuntimeLabel(settings);
            string weightsPath = settings?.WeightsPath ?? string.Empty;
            string displayPath = WpfTrainingWeightsService.FormatWeightsDisplayPath(weightsPath);
            return string.IsNullOrWhiteSpace(displayPath)
                ? runtimeLabel
                : $"{runtimeLabel} / {displayPath}";
        }

        public static string BuildModelComparisonSourceText(
            PythonModelSettings settings,
            string currentWeightsPath,
            string candidateWeightsPath)
        {
            string runtimeLabel = BuildRuntimeLabel(settings);
            string currentModel = FormatComparisonModelPath(currentWeightsPath);
            string candidateModel = FormatComparisonModelPath(candidateWeightsPath);
            return $"\uBE44\uAD50 \uB300\uC0C1: \uD604\uC7AC \uAC80\uC0AC {runtimeLabel} / {currentModel} -> \uD559\uC2B5 \uD6C4\uBCF4 {runtimeLabel} / {candidateModel}";
        }

        private static string BuildModelText(PythonModelSettings settings, bool hasPendingModelCandidate)
        {
            string weightsPath = settings?.WeightsPath ?? string.Empty;
            if (string.IsNullOrWhiteSpace(weightsPath) || !File.Exists(weightsPath))
            {
                return "\uAC80\uC0AC \uBAA8\uB378 \uC5C6\uC74C";
            }

            string displayPath = WpfTrainingWeightsService.FormatWeightsDisplayPath(weightsPath);
            string runtimeLabel = BuildRuntimeLabel(settings);
            return hasPendingModelCandidate
                ? $"\uBAA8\uB378 \uD6C4\uBCF4 {runtimeLabel} / {displayPath}"
                : $"\uAC80\uC0AC \uBAA8\uB378 {runtimeLabel} / {displayPath}";
        }

        private static string FormatComparisonModelPath(string weightsPath)
        {
            string displayPath = WpfTrainingWeightsService.FormatWeightsDisplayPath(weightsPath);
            return string.IsNullOrWhiteSpace(displayPath)
                ? "\uC5C6\uC74C"
                : displayPath;
        }

        private static string BuildRuntimeLabel(PythonModelSettings settings)
        {
            return PythonModelSettings.NormalizeModelEngine(settings?.ModelEngine) switch
            {
                PythonModelSettings.EngineYoloV8 => "YOLOv8",
                PythonModelSettings.EngineYolo11 => "YOLO11",
                PythonModelSettings.EngineOnnx => "ONNX",
                _ => "YOLOv5"
            };
        }
    }
}
