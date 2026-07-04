using System;
using System.Globalization;
using System.IO;
using System.Linq;
using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;

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

        public static string BuildRuntimePythonStatus(
            PythonModelValidationResult validationResult,
            PythonModelRuntimeState runtimeState)
        {
            return validationResult?.IsValid == true
                ? "\uCD94\uB860: \uC900\uBE44 \uC644\uB8CC"
                : runtimeState?.SummaryText ?? string.Empty;
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

        public static string BuildInteractivePreparingCommandStatus()
        {
            return "\uCD94\uB860 \uC900\uBE44 \uC911...";
        }

        public static string BuildInteractivePreparingInferenceStatus()
        {
            return "\uD604\uC7AC \uC774\uBBF8\uC9C0 \uCD94\uB860 \uC900\uBE44 \uC911";
        }

        public static string BuildInteractiveCompletionCommandStatus(YoloWorkerSmokeTestResult result, string elapsedText)
        {
            string elapsed = NormalizeElapsedText(elapsedText);
            if (result?.Succeeded == true)
            {
                return string.Format(
                    CultureInfo.CurrentCulture,
                    "\uCD94\uB860 \uC644\uB8CC: \uD6C4\uBCF4 {0}\uAC1C / {1}",
                    Math.Max(0, result.CandidateCount),
                    elapsed);
            }

            return string.Format(
                CultureInfo.CurrentCulture,
                "\uCD94\uB860 \uC2E4\uD328: {0} / {1}",
                BuildInteractiveDetectionFailureSummary(result),
                elapsed);
        }

        public static string BuildInteractiveCompletionInferenceStatus(YoloWorkerSmokeTestResult result, string elapsedText)
        {
            string elapsed = NormalizeElapsedText(elapsedText);
            if (result?.Succeeded == true)
            {
                return string.Format(
                    CultureInfo.CurrentCulture,
                    "\uC644\uB8CC: \uD6C4\uBCF4 {0}\uAC1C / {1}",
                    Math.Max(0, result.CandidateCount),
                    elapsed);
            }

            return string.Format(
                CultureInfo.CurrentCulture,
                "\uC2E4\uD328: {0}",
                BuildInteractiveDetectionFailureSummary(result));
        }

        public static string BuildInteractiveCompletionLog(YoloWorkerSmokeTestResult result, string elapsedText, string inferencePathText)
        {
            string elapsed = NormalizeElapsedText(elapsedText);
            if (result?.Succeeded == true)
            {
                return string.Format(
                    CultureInfo.CurrentCulture,
                    "\uB2E8\uC77C \uC774\uBBF8\uC9C0 \uCD94\uB860 \uC644\uB8CC: {0} / \uACBD\uB85C {1}",
                    elapsed,
                    NormalizeDisplayText(inferencePathText));
            }

            return string.Format(
                CultureInfo.CurrentCulture,
                "\uB2E8\uC77C \uC774\uBBF8\uC9C0 \uCD94\uB860 \uC2E4\uD328: {0} / {1}",
                elapsed,
                BuildInteractiveDetectionFailureSummary(result));
        }

        public static string BuildInteractiveDetectionFailureSummary(YoloWorkerSmokeTestResult result, int maxLength = 80)
        {
            string summary = FirstNonEmpty(
                result?.Summary,
                result?.Error,
                result?.Errors?.FirstOrDefault(),
                "\uCD94\uB860 \uACB0\uACFC \uB610\uB294 \uC751\uB2F5\uC744 \uD655\uC778\uD558\uC138\uC694.");
            summary = summary.Replace(Environment.NewLine, " ").Trim();
            if (summary.Length <= maxLength)
            {
                return summary;
            }

            return summary.Substring(0, Math.Max(0, maxLength)) + "...";
        }

        public static string BuildWorkerImageMissingSummary()
        {
            return "\uAC80\uC0AC \uC774\uBBF8\uC9C0\uB97C \uCC3E\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.";
        }

        public static string BuildWorkerImageMissingError(string imagePath)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                "\uAC80\uC0AC \uC774\uBBF8\uC9C0\uB97C \uCC3E\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4. {0}",
                NormalizeDisplayText(imagePath));
        }

        public static string BuildWorkerImageLoadFailureSummary()
        {
            return "\uAC80\uC0AC \uC774\uBBF8\uC9C0\uB97C \uB85C\uB4DC\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.";
        }

        public static string BuildWorkerImageLoadFailureError(string imagePath)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                "\uAC80\uC0AC \uC774\uBBF8\uC9C0\uB97C \uB85C\uB4DC\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4. {0}",
                NormalizeDisplayText(imagePath));
        }

        public static string BuildWorkerPreparingInferenceStatus(bool applyToCanvas, string imagePath)
        {
            return applyToCanvas
                ? "\uD604\uC7AC \uC774\uBBF8\uC9C0 \uCD94\uB860 \uC900\uBE44 \uC911"
                : string.Format(CultureInfo.CurrentCulture, "\uC77C\uAD04 \uCD94\uB860 \uC900\uBE44 {0}", ResolveImageFileName(imagePath));
        }

        public static string BuildWorkerPreparingCommandStatus()
        {
            return "\uCD94\uB860 \uC2E4\uD589\uAE30 \uC900\uBE44 \uC911...";
        }

        public static string BuildWorkerConnectionFailureInferenceStatus()
        {
            return "\uC2E4\uD328: \uCD94\uB860 \uC5F0\uACB0 \uC2E4\uD328";
        }

        public static string BuildWorkerConnectionFailureLog(string elapsedText)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                "\uCD94\uB860 \uC5F0\uACB0 \uC2E4\uD328: {0}. \uBAA8\uB378 \uC124\uC815 \uB610\uB294 \uCD94\uB860 \uC2E4\uD589 \uC0C1\uD0DC\uB97C \uD655\uC778\uD558\uC138\uC694.",
                NormalizeElapsedText(elapsedText));
        }

        public static string BuildWorkerRunningInferenceStatus(bool applyToCanvas, string imagePath)
        {
            return applyToCanvas
                ? "AI \uCD94\uB860 \uC911"
                : string.Format(CultureInfo.CurrentCulture, "\uC77C\uAD04 \uCD94\uB860 \uC911 {0}", ResolveImageFileName(imagePath));
        }

        public static string BuildWorkerStartLog(string imagePath, string modelSourceText)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                "\uCD94\uB860 \uC2DC\uC791: {0} / \uBAA8\uB378 {1}",
                ResolveImageFileName(imagePath),
                NormalizeDisplayText(modelSourceText));
        }

        public static string BuildWorkerRequestCommandStatus()
        {
            return "AI \uCD94\uB860 \uC694\uCCAD \uC911...";
        }

        public static string BuildWorkerRequestFailureInferenceStatus()
        {
            return "\uC2E4\uD328: \uCD94\uB860 \uC694\uCCAD \uC2E4\uD328";
        }

        public static string BuildWorkerRequestFailureSummary(string lastError)
        {
            return FirstNonEmpty(lastError, "\uCD94\uB860 \uAC80\uCD9C \uC694\uCCAD\uC744 \uBCF4\uB0B4\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.");
        }

        public static string BuildWorkerTimedOutInferenceStatus()
        {
            return "\uC2E4\uD328: \uCD94\uB860 \uC2DC\uAC04 \uCD08\uACFC";
        }

        public static string BuildWorkerTimedOutSummary()
        {
            return "\uCD94\uB860 \uAC80\uCD9C \uC2DC\uAC04 \uCD08\uACFC.";
        }

        public static string BuildWorkerSuccessSummary(string modelSourceText, int candidateCount)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                "\uCD94\uB860 \uC644\uB8CC. \uBAA8\uB378:{0} / \uD6C4\uBCF4:{1}",
                NormalizeDisplayText(modelSourceText),
                Math.Max(0, candidateCount));
        }

        public static string BuildWorkerPythonCompletedStatus(string modelSourceText, int candidateCount)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                "\uCD94\uB860: \uC644\uB8CC  \uBAA8\uB378 {0} / \uD6C4\uBCF4 {1}",
                NormalizeDisplayText(modelSourceText),
                Math.Max(0, candidateCount));
        }

        public static string BuildWorkerElapsedLog(string elapsedText, string modelSourceText)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                "\uCD94\uB860 \uC2DC\uAC04: {0} / \uBAA8\uB378 {1}",
                NormalizeElapsedText(elapsedText),
                NormalizeDisplayText(modelSourceText));
        }

        public static string BuildWorkerCanceledInferenceStatus()
        {
            return "\uCD94\uB860 \uCDE8\uC18C";
        }

        public static string BuildWorkerCanceledSummary()
        {
            return "\uCD94\uB860 \uAC80\uCD9C \uCDE8\uC18C.";
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

        private static string NormalizeElapsedText(string elapsedText)
        {
            return string.IsNullOrWhiteSpace(elapsedText)
                ? "-"
                : elapsedText.Trim();
        }

        private static string NormalizeDisplayText(string text)
        {
            return string.IsNullOrWhiteSpace(text)
                ? "\uC54C \uC218 \uC5C6\uC74C"
                : text.Trim();
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }

        private static string ResolveImageFileName(string imagePath)
        {
            return string.IsNullOrWhiteSpace(imagePath)
                ? "-"
                : Path.GetFileName(imagePath);
        }
    }
}
