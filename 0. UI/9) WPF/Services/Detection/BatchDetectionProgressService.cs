using System;
using System.Globalization;
using System.IO;

namespace MvcVisionSystem
{
    public class BatchDetectionControlState
    {
        public BatchDetectionControlState(
            int progressMaximum,
            int progressValue,
            string statusText,
            string datasetStatusText,
            bool shouldRefreshQueueStatus)
        {
            ProgressMaximum = progressMaximum;
            ProgressValue = progressValue;
            StatusText = statusText ?? string.Empty;
            DatasetStatusText = datasetStatusText ?? string.Empty;
            ShouldRefreshQueueStatus = shouldRefreshQueueStatus;
        }

        public int ProgressMaximum { get; }

        public int ProgressValue { get; }

        public string StatusText { get; }

        public string DatasetStatusText { get; }

        public bool ShouldRefreshQueueStatus { get; }
    }

    public class BatchDetectionProgressService
    {
        // Batch wording and progress math stay here so the shell only applies values to WPF controls.
        public BatchDetectionControlState BuildControlState(
            bool isBusy,
            int totalCount,
            int completedCount,
            string scopeText,
            string currentFileName)
        {
            int total = Math.Max(0, totalCount);
            int completed = Math.Max(0, completedCount);
            int visibleProgress = Math.Min(completed, total);

            string statusText = isBusy
                ? string.Format(CultureInfo.CurrentCulture, "{0}/{1}", visibleProgress, total)
                : total > 0
                    ? string.Format(CultureInfo.CurrentCulture, "{0}/{1}", completed, total)
                    : OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.BatchDetectionProgressService.Waiting");
            int progressMaximum = total <= 0 ? 1 : total;
            int progressValue = total <= 0 ? 0 : Math.Min(visibleProgress, total);

            if (!isBusy)
            {
                return new BatchDetectionControlState(
                    progressMaximum,
                    progressValue,
                    statusText,
                    string.Empty,
                    shouldRefreshQueueStatus: true);
            }

            string fileText = string.IsNullOrWhiteSpace(currentFileName) ? string.Empty : " " + currentFileName;
            string datasetStatusText = string.Format(
                CultureInfo.CurrentCulture,
                OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.BatchDetectionProgressService.DatasetProgress"),
                completed,
                total,
                scopeText,
                fileText);
            return new BatchDetectionControlState(
                progressMaximum,
                progressValue,
                statusText,
                datasetStatusText,
                shouldRefreshQueueStatus: false);
        }

        public string BuildStartCommandStatus(int totalCount)
        {
            return string.Format(CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.BatchDetectionProgressService.StartCommand"), totalCount);
        }

        public string BuildStartInferenceStatus(int totalCount)
        {
            return string.Format(CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.BatchDetectionProgressService.StartStatus"), totalCount);
        }

        public string BuildStartLog(string scopeText, int totalCount, string modelSourceText = "")
        {
            string modelText = BuildModelSourceSuffix(modelSourceText);
            return string.Format(CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.BatchDetectionProgressService.StartLog"), scopeText, totalCount, modelText);
        }

        public string BuildWorkerPreparingInferenceStatus(int totalCount)
        {
            return string.Format(CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.BatchDetectionProgressService.Preparing"), totalCount);
        }

        public string BuildItemInferenceStatus(int completedCount, int totalCount, string imagePath)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.BatchDetectionProgressService.ItemProgress"),
                completedCount + 1,
                totalCount,
                ResolveImageFileName(imagePath));
        }

        public string BuildItemCompletedLog(int completedCount, int totalCount, string imagePath, int candidateCount, string elapsedText, string modelSourceText = "")
        {
            string modelText = BuildModelSourceSuffix(modelSourceText);
            return string.Format(
                CultureInfo.CurrentCulture,
                OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.BatchDetectionProgressService.ItemCompleted"),
                completedCount,
                totalCount,
                ResolveImageFileName(imagePath),
                candidateCount,
                elapsedText,
                modelText);
        }

        public string BuildItemFailedLog(int completedCount, int totalCount, string imagePath, string elapsedText, string summary, string modelSourceText = "")
        {
            string modelText = BuildModelSourceSuffix(modelSourceText);
            return string.Format(
                CultureInfo.CurrentCulture,
                OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.BatchDetectionProgressService.ItemFailed"),
                completedCount,
                totalCount,
                ResolveImageFileName(imagePath),
                elapsedText,
                modelText,
                summary);
        }

        public string BuildItemPythonStatus(int completedCount, int totalCount, string elapsedText)
        {
            return string.Format(CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.BatchDetectionProgressService.PythonProgress"), completedCount, totalCount, elapsedText);
        }

        public string BuildLatestFileStatus(string imagePath, string elapsedText)
        {
            return string.Format(CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.BatchDetectionProgressService.LatestFile"), ResolveImageFileName(imagePath), elapsedText);
        }

        public string BuildCandidateCompletionPythonStatus(bool canceled, int failedImages, string failureSummary)
        {
            string state = canceled ? OpenVisionLab.OpenVisionLanguageService.T("WpfCandidate.Batch.Canceled")
                : !string.IsNullOrWhiteSpace(failureSummary) ? OpenVisionLab.OpenVisionLanguageService.T("WpfCandidate.Batch.Stopped")
                : failedImages > 0 ? string.Format(CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfCandidate.Batch.Failed"), failedImages)
                : OpenVisionLab.OpenVisionLanguageService.T("WpfCandidate.Batch.Completed");
            return string.Format(CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfCandidate.Batch.Status"), state);
        }

        public string BuildCompletionCommandStatus(bool canceled, int completedCount, int totalCount, string totalElapsedText)
        {
            string stateText = canceled ? OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.BatchDetectionProgressService.Stopped") : OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.BatchDetectionProgressService.Completed");
            return string.Format(CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.BatchDetectionProgressService.CompletionCommand"), stateText, completedCount, totalCount, totalElapsedText);
        }

        public string BuildCompletionInferenceStatus(bool canceled, int completedCount, int totalCount, string totalElapsedText)
        {
            if (canceled)
            {
                return string.Format(CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.BatchDetectionProgressService.StoppedStatus"), completedCount, totalCount);
            }

            return string.Format(CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.BatchDetectionProgressService.CompletedStatus"), completedCount, totalCount, totalElapsedText);
        }

        public string BuildCompletionLog(bool canceled, int completedCount, int totalCount, string totalElapsedText, string averageElapsedText, string modelSourceText = "")
        {
            string stateText = canceled ? OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.BatchDetectionProgressService.Stopped") : OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.BatchDetectionProgressService.Completed");
            string modelText = BuildModelSourceSuffix(modelSourceText);
            return string.Format(
                CultureInfo.CurrentCulture,
                OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.BatchDetectionProgressService.CompletedLog"),
                stateText,
                completedCount,
                totalCount,
                totalElapsedText,
                averageElapsedText,
                modelText);
        }

        public string BuildFailureCommandStatus(int completedCount, int totalCount, string failureSummary)
        {
            return string.Format(CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.BatchDetectionProgressService.FailedCommand"), completedCount, totalCount, failureSummary);
        }

        public string BuildFailureInferenceStatus(int completedCount, int totalCount, string failureSummary)
        {
            string summary = ShortenMessage(failureSummary, 80);
            return string.IsNullOrWhiteSpace(summary)
                ? string.Format(CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.BatchDetectionProgressService.FailedStatus"), completedCount, totalCount)
                : string.Format(CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.BatchDetectionProgressService.FailedStatusDetail"), completedCount, totalCount, summary);
        }

        public string BuildFailureLog(int completedCount, int totalCount, string failureSummary)
        {
            return string.Format(CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.BatchDetectionProgressService.FailedLog"), completedCount, totalCount, failureSummary);
        }

        private static string BuildModelSourceSuffix(string modelSourceText)
        {
            return string.IsNullOrWhiteSpace(modelSourceText)
                ? string.Empty
                : string.Format(CultureInfo.CurrentCulture, " " + OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.BatchDetectionProgressService.ModelSuffix"), modelSourceText.Trim());
        }

        private static string ShortenMessage(string message, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(message) || message.Length <= maxLength)
            {
                return message ?? string.Empty;
            }

            return message.Substring(0, Math.Max(0, maxLength - 3)) + "...";
        }

        public string ResolveImageFileName(string imagePath)
        {
            return string.IsNullOrWhiteSpace(imagePath) ? string.Empty : Path.GetFileName(imagePath);
        }
    }

    [Obsolete("Use BatchDetectionControlState.", false)]
    public sealed class WpfBatchDetectionControlState : BatchDetectionControlState
    {
        public WpfBatchDetectionControlState(
            int progressMaximum,
            int progressValue,
            string statusText,
            string datasetStatusText,
            bool shouldRefreshQueueStatus)
            : base(
                progressMaximum,
                progressValue,
                statusText,
                datasetStatusText,
                shouldRefreshQueueStatus)
        {
        }
    }

    [Obsolete("Use BatchDetectionProgressService.", false)]
    public sealed class WpfBatchDetectionProgressService : BatchDetectionProgressService
    {
    }
}
