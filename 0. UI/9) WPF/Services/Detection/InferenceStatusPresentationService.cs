using System;
using System.Globalization;
using System.IO;
using System.Linq;
using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using OpenVisionLab;

namespace MvcVisionSystem
{
    public static class InferenceStatusPresentationService
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
                ? OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.InferenceStatusPresentationService.NoModelPath")
                : weightsPath;
            string runtimeText = OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.InferenceStatusPresentationService.RuntimeLabel") + " " + BuildRuntimeLabel(settings);
            string statusLine = string.IsNullOrWhiteSpace(statusText)
                ? OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.InferenceStatusPresentationService.StatusWaiting")
                : OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.InferenceStatusPresentationService.StatusLabel") + " " + statusText.Trim();
            return $"{statusLine}\n{modelText}\n{runtimeText}\n{modelPathText}";
        }

        public static string BuildRuntimePythonStatus(
            PythonModelValidationResult validationResult,
            PythonModelRuntimeState runtimeState)
        {
            return validationResult?.IsValid == true
                ? OpenVisionLanguageService.T("WpfShell.Status.InferenceReady")
                : runtimeState?.SummaryText ?? string.Empty;
        }

        public static string BuildInspectionModelStatusText(
            PythonModelSettings settings,
            bool hasPendingModelCandidate)
        {
            string weightsPath = settings?.WeightsPath ?? string.Empty;
            if (string.IsNullOrWhiteSpace(weightsPath) || !File.Exists(weightsPath))
            {
                return OpenVisionLab.OpenVisionLanguageService.T("WpfShell.Status.ModelNone");
            }

            string displayPath = TrainingWeightsService.FormatWeightsDisplayPath(weightsPath);
            string runtimeLabel = BuildRuntimeLabel(settings);
            return string.Format(
                CultureInfo.InvariantCulture,
                OpenVisionLanguageService.T(hasPendingModelCandidate
                    ? "WpfShell.Status.InspectionCandidate"
                    : "WpfShell.Status.InspectionModel"),
                runtimeLabel,
                displayPath);
        }

        public static string BuildInspectionModelToolTip(
            PythonModelSettings settings,
            bool hasPendingModelCandidate)
        {
            string weightsPath = settings?.WeightsPath ?? string.Empty;
            if (string.IsNullOrWhiteSpace(weightsPath) || !File.Exists(weightsPath))
            {
                return OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.InferenceStatusPresentationService.ChooseModelFile");
            }

            return string.Format(CultureInfo.InvariantCulture,
                OpenVisionLab.OpenVisionLanguageService.T(hasPendingModelCandidate
                    ? "WpfInference.Text.InferenceStatusPresentationService.PendingModelToolTip"
                    : "WpfInference.Text.InferenceStatusPresentationService.InspectionModelToolTip"),
                BuildRuntimeLabel(settings), weightsPath);
        }

        public static string BuildRuntimeModelLabel(PythonModelSettings settings)
        {
            string runtimeLabel = BuildRuntimeLabel(settings);
            string weightsPath = settings?.WeightsPath ?? string.Empty;
            string displayPath = TrainingWeightsService.FormatWeightsDisplayPath(weightsPath);
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
            return string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfInference.ComparisonSource.Models"), runtimeLabel, currentModel, runtimeLabel, candidateModel);
        }

        public static string BuildInteractivePreparingCommandStatus()
        {
            return OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.InferenceStatusPresentationService.PreparingCommand");
        }

        public static string BuildInteractivePreparingInferenceStatus()
        {
            return OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.InferenceStatusPresentationService.PreparingImage");
        }

        public static string BuildInteractiveCompletionCommandStatus(YoloWorkerSmokeTestResult result, string elapsedText)
        {
            string elapsed = NormalizeElapsedText(elapsedText);
            if (result?.Succeeded == true)
            {
                return string.Format(
                    CultureInfo.CurrentCulture,
                    OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.InferenceStatusPresentationService.CompletedCommand"),
                    Math.Max(0, result.CandidateCount),
                    elapsed);
            }

            return string.Format(
                CultureInfo.CurrentCulture,
                OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.InferenceStatusPresentationService.FailedCommand"),
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
                    OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.InferenceStatusPresentationService.CompletedStatus"),
                    Math.Max(0, result.CandidateCount),
                    elapsed);
            }

            return string.Format(
                CultureInfo.CurrentCulture,
                OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.InferenceStatusPresentationService.FailedStatus"),
                BuildInteractiveDetectionFailureSummary(result));
        }

        public static string BuildInteractiveCompletionLog(YoloWorkerSmokeTestResult result, string elapsedText, string inferencePathText)
        {
            string elapsed = NormalizeElapsedText(elapsedText);
            if (result?.Succeeded == true)
            {
                return string.Format(
                    CultureInfo.CurrentCulture,
                    OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.InferenceStatusPresentationService.CompletedLog"),
                    elapsed,
                    NormalizeDisplayText(inferencePathText));
            }

            return string.Format(
                CultureInfo.CurrentCulture,
                OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.InferenceStatusPresentationService.FailedLog"),
                elapsed,
                BuildInteractiveDetectionFailureSummary(result));
        }

        public static string BuildInteractiveDetectionFailureSummary(YoloWorkerSmokeTestResult result, int maxLength = 80)
        {
            string summary = FirstNonEmpty(
                result?.Summary,
                result?.Error,
                result?.Errors?.FirstOrDefault(),
                OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.InferenceStatusPresentationService.FailureHelp"));
            if (string.Equals(summary, BuildWorkerImageMissingSummary(), StringComparison.Ordinal)) summary = OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.InferenceStatusPresentationService.ImageMissing");
            else if (string.Equals(summary, BuildWorkerImageLoadFailureSummary(), StringComparison.Ordinal)) summary = OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.InferenceStatusPresentationService.ImageLoadFailed");
            else if (string.Equals(summary, BuildWorkerTimedOutSummary(), StringComparison.Ordinal)) summary = OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.InferenceStatusPresentationService.TimeoutFailure");
            else if (string.Equals(summary, BuildWorkerCanceledSummary(), StringComparison.Ordinal)) summary = OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.InferenceStatusPresentationService.CanceledFailure");
            else if (string.Equals(summary, BuildWorkerRequestFailureSummary(string.Empty), StringComparison.Ordinal)) summary = OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.InferenceStatusPresentationService.RequestFailure");
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
                ? OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.InferenceStatusPresentationService.PreparingImage")
                : string.Format(CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.InferenceStatusPresentationService.BatchPreparing"), ResolveImageFileName(imagePath));
        }

        public static string BuildWorkerPreparingCommandStatus()
        {
            return OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.InferenceStatusPresentationService.RuntimePreparing");
        }

        public static string BuildWorkerConnectionFailureInferenceStatus()
        {
            return OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.InferenceStatusPresentationService.ConnectionFailed");
        }

        public static string BuildWorkerConnectionFailureLog(string elapsedText)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.InferenceStatusPresentationService.ConnectionFailedLog"),
                NormalizeElapsedText(elapsedText));
        }

        public static string BuildWorkerRunningInferenceStatus(bool applyToCanvas, string imagePath)
        {
            return applyToCanvas
                ? OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.InferenceStatusPresentationService.Running")
                : string.Format(CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.InferenceStatusPresentationService.BatchRunning"), ResolveImageFileName(imagePath));
        }

        public static string BuildWorkerStartLog(string imagePath, string modelSourceText)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.InferenceStatusPresentationService.StartedLog"),
                ResolveImageFileName(imagePath),
                NormalizeDisplayText(modelSourceText));
        }

        public static string BuildWorkerRequestCommandStatus()
        {
            return OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.InferenceStatusPresentationService.Requested");
        }

        public static string BuildWorkerRequestFailureInferenceStatus()
        {
            return OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.InferenceStatusPresentationService.RequestFailed");
        }

        public static string BuildWorkerRequestFailureSummary(string lastError)
        {
            string summary = FirstNonEmpty(lastError, OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Transport.RequestFailed"));
            return OpenVisionLab.OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfInference.Transport.RequestDisconnected").Contains(summary, StringComparer.Ordinal)
                ? OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Transport.RequestDisconnected")
                : summary;
        }

        public static string BuildWorkerTimedOutInferenceStatus()
        {
            return OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.InferenceStatusPresentationService.TimeoutStatus");
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
                OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.InferenceStatusPresentationService.PythonCompleted"),
                NormalizeDisplayText(modelSourceText),
                Math.Max(0, candidateCount));
        }

        public static string BuildWorkerElapsedLog(string elapsedText, string modelSourceText)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.InferenceStatusPresentationService.ElapsedLog"),
                NormalizeElapsedText(elapsedText),
                NormalizeDisplayText(modelSourceText));
        }

        public static string BuildWorkerCanceledInferenceStatus()
        {
            return OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.InferenceStatusPresentationService.Canceled");
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
                return OpenVisionLab.OpenVisionLanguageService.T("WpfShell.Status.ModelNone");
            }

            string displayPath = TrainingWeightsService.FormatWeightsDisplayPath(weightsPath);
            string runtimeLabel = BuildRuntimeLabel(settings);
            return string.Format(CultureInfo.InvariantCulture,
                OpenVisionLab.OpenVisionLanguageService.T(hasPendingModelCandidate
                    ? "WpfShell.Status.InspectionCandidate" : "WpfShell.Status.InspectionModel"),
                runtimeLabel, displayPath);
        }

        private static string FormatComparisonModelPath(string weightsPath)
        {
            string displayPath = TrainingWeightsService.FormatWeightsDisplayPath(weightsPath);
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
                PythonModelSettings.EngineUnet => "U-Net",
                PythonModelSettings.EnginePatchCore => "PatchCore",
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

    [Obsolete("Use InferenceStatusPresentationService.", false)]
    public static class WpfInferenceStatusPresentationService
    {
        public static string BuildStatusText(
            string statusText,
            PythonModelSettings settings,
            bool hasPendingModelCandidate)
            => InferenceStatusPresentationService.BuildStatusText(statusText, settings, hasPendingModelCandidate);

        public static string BuildToolTip(
            string statusText,
            PythonModelSettings settings,
            bool hasPendingModelCandidate)
            => InferenceStatusPresentationService.BuildToolTip(statusText, settings, hasPendingModelCandidate);

        public static string BuildRuntimePythonStatus(
            PythonModelValidationResult validationResult,
            PythonModelRuntimeState runtimeState)
            => InferenceStatusPresentationService.BuildRuntimePythonStatus(validationResult, runtimeState);

        public static string BuildInspectionModelStatusText(
            PythonModelSettings settings,
            bool hasPendingModelCandidate)
            => InferenceStatusPresentationService.BuildInspectionModelStatusText(settings, hasPendingModelCandidate);

        public static string BuildInspectionModelToolTip(
            PythonModelSettings settings,
            bool hasPendingModelCandidate)
            => InferenceStatusPresentationService.BuildInspectionModelToolTip(settings, hasPendingModelCandidate);

        public static string BuildRuntimeModelLabel(PythonModelSettings settings)
            => InferenceStatusPresentationService.BuildRuntimeModelLabel(settings);

        public static string BuildModelComparisonSourceText(
            PythonModelSettings settings,
            string currentWeightsPath,
            string candidateWeightsPath)
            => InferenceStatusPresentationService.BuildModelComparisonSourceText(settings, currentWeightsPath, candidateWeightsPath);

        public static string BuildInteractivePreparingCommandStatus()
            => InferenceStatusPresentationService.BuildInteractivePreparingCommandStatus();

        public static string BuildInteractivePreparingInferenceStatus()
            => InferenceStatusPresentationService.BuildInteractivePreparingInferenceStatus();

        public static string BuildInteractiveCompletionCommandStatus(YoloWorkerSmokeTestResult result, string elapsedText)
            => InferenceStatusPresentationService.BuildInteractiveCompletionCommandStatus(result, elapsedText);

        public static string BuildInteractiveCompletionInferenceStatus(YoloWorkerSmokeTestResult result, string elapsedText)
            => InferenceStatusPresentationService.BuildInteractiveCompletionInferenceStatus(result, elapsedText);

        public static string BuildInteractiveCompletionLog(YoloWorkerSmokeTestResult result, string elapsedText, string inferencePathText)
            => InferenceStatusPresentationService.BuildInteractiveCompletionLog(result, elapsedText, inferencePathText);

        public static string BuildInteractiveDetectionFailureSummary(YoloWorkerSmokeTestResult result, int maxLength = 80)
            => InferenceStatusPresentationService.BuildInteractiveDetectionFailureSummary(result, maxLength);

        public static string BuildWorkerImageMissingSummary()
            => InferenceStatusPresentationService.BuildWorkerImageMissingSummary();

        public static string BuildWorkerImageMissingError(string imagePath)
            => InferenceStatusPresentationService.BuildWorkerImageMissingError(imagePath);

        public static string BuildWorkerImageLoadFailureSummary()
            => InferenceStatusPresentationService.BuildWorkerImageLoadFailureSummary();

        public static string BuildWorkerImageLoadFailureError(string imagePath)
            => InferenceStatusPresentationService.BuildWorkerImageLoadFailureError(imagePath);

        public static string BuildWorkerPreparingInferenceStatus(bool applyToCanvas, string imagePath)
            => InferenceStatusPresentationService.BuildWorkerPreparingInferenceStatus(applyToCanvas, imagePath);

        public static string BuildWorkerPreparingCommandStatus()
            => InferenceStatusPresentationService.BuildWorkerPreparingCommandStatus();

        public static string BuildWorkerConnectionFailureInferenceStatus()
            => InferenceStatusPresentationService.BuildWorkerConnectionFailureInferenceStatus();

        public static string BuildWorkerConnectionFailureLog(string elapsedText)
            => InferenceStatusPresentationService.BuildWorkerConnectionFailureLog(elapsedText);

        public static string BuildWorkerRunningInferenceStatus(bool applyToCanvas, string imagePath)
            => InferenceStatusPresentationService.BuildWorkerRunningInferenceStatus(applyToCanvas, imagePath);

        public static string BuildWorkerStartLog(string imagePath, string modelSourceText)
            => InferenceStatusPresentationService.BuildWorkerStartLog(imagePath, modelSourceText);

        public static string BuildWorkerRequestCommandStatus()
            => InferenceStatusPresentationService.BuildWorkerRequestCommandStatus();

        public static string BuildWorkerRequestFailureInferenceStatus()
            => InferenceStatusPresentationService.BuildWorkerRequestFailureInferenceStatus();

        public static string BuildWorkerRequestFailureSummary(string lastError)
            => InferenceStatusPresentationService.BuildWorkerRequestFailureSummary(lastError);

        public static string BuildWorkerTimedOutInferenceStatus()
            => InferenceStatusPresentationService.BuildWorkerTimedOutInferenceStatus();

        public static string BuildWorkerTimedOutSummary()
            => InferenceStatusPresentationService.BuildWorkerTimedOutSummary();

        public static string BuildWorkerSuccessSummary(string modelSourceText, int candidateCount)
            => InferenceStatusPresentationService.BuildWorkerSuccessSummary(modelSourceText, candidateCount);

        public static string BuildWorkerPythonCompletedStatus(string modelSourceText, int candidateCount)
            => InferenceStatusPresentationService.BuildWorkerPythonCompletedStatus(modelSourceText, candidateCount);

        public static string BuildWorkerElapsedLog(string elapsedText, string modelSourceText)
            => InferenceStatusPresentationService.BuildWorkerElapsedLog(elapsedText, modelSourceText);

        public static string BuildWorkerCanceledInferenceStatus()
            => InferenceStatusPresentationService.BuildWorkerCanceledInferenceStatus();

        public static string BuildWorkerCanceledSummary()
            => InferenceStatusPresentationService.BuildWorkerCanceledSummary();
    }
}
