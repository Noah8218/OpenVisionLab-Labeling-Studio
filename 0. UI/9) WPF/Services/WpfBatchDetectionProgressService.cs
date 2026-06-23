using System;
using System.Globalization;
using System.IO;

namespace MvcVisionSystem
{
    public sealed class WpfBatchDetectionControlState
    {
        public WpfBatchDetectionControlState(
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

    public sealed class WpfBatchDetectionProgressService
    {
        // Batch wording and progress math stay here so the shell only applies values to WPF controls.
        public WpfBatchDetectionControlState BuildControlState(
            bool isBusy,
            int totalCount,
            int completedCount,
            string scopeText,
            string currentFileName)
        {
            int total = Math.Max(0, totalCount);
            int completed = Math.Max(0, completedCount);
            int visibleProgress = isBusy && total > 0 && !string.IsNullOrWhiteSpace(currentFileName)
                ? Math.Min(completed + 1, total)
                : completed;
            if (isBusy)
            {
                completed = visibleProgress;
            }

            string statusText = isBusy
                ? string.Format(CultureInfo.CurrentCulture, "{0}/{1}", visibleProgress, total)
                : total > 0
                    ? string.Format(CultureInfo.CurrentCulture, "{0}/{1}", completed, total)
                    : "\uBC30\uCE58 \uB300\uAE30";
            int progressMaximum = total <= 0 ? 1 : total;
            int progressValue = total <= 0 ? 0 : Math.Min(visibleProgress, total);

            if (!isBusy)
            {
                return new WpfBatchDetectionControlState(
                    progressMaximum,
                    progressValue,
                    statusText,
                    string.Empty,
                    shouldRefreshQueueStatus: true);
            }

            string fileText = string.IsNullOrWhiteSpace(currentFileName) ? string.Empty : " " + currentFileName;
            string datasetStatusText = string.Format(
                CultureInfo.CurrentCulture,
                "\uB370\uC774\uD130\uC14B: \uC77C\uAD04 {0}/{1} {2}{3}",
                completed,
                total,
                scopeText,
                fileText);
            return new WpfBatchDetectionControlState(
                progressMaximum,
                progressValue,
                statusText,
                datasetStatusText,
                shouldRefreshQueueStatus: false);
        }

        public string BuildStartCommandStatus(int totalCount)
        {
            return string.Format(CultureInfo.CurrentCulture, "\uC77C\uAD04 \uAC80\uC0AC \uC2DC\uC791: {0}\uAC1C", totalCount);
        }

        public string BuildStartInferenceStatus(int totalCount)
        {
            return string.Format(CultureInfo.CurrentCulture, "\uC77C\uAD04 \uCD94\uB860 \uC2DC\uC791: {0}\uAC1C", totalCount);
        }

        public string BuildStartLog(string scopeText, int totalCount)
        {
            return string.Format(CultureInfo.CurrentCulture, "\uC77C\uAD04 \uAC80\uC0AC \uC2DC\uC791. \uBC94\uC704:{0}, \uAC1C\uC218:{1}", scopeText, totalCount);
        }

        public string BuildWorkerPreparingInferenceStatus(int totalCount)
        {
            return string.Format(CultureInfo.CurrentCulture, "\uC77C\uAD04 \uCD94\uB860 worker \uC900\uBE44 \uC911: {0}\uAC1C", totalCount);
        }

        public string BuildItemInferenceStatus(int completedCount, int totalCount, string imagePath)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                "\uC77C\uAD04 \uCD94\uB860 {0}/{1}: {2}",
                completedCount + 1,
                totalCount,
                ResolveImageFileName(imagePath));
        }

        public string BuildItemCompletedLog(int completedCount, int totalCount, string imagePath, int candidateCount, string elapsedText)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                "\uC77C\uAD04 \uAC80\uC0AC \uD56D\uBAA9 \uC644\uB8CC: {0}/{1} {2} \uD6C4\uBCF4:{3} / {4}",
                completedCount,
                totalCount,
                ResolveImageFileName(imagePath),
                candidateCount,
                elapsedText);
        }

        public string BuildItemFailedLog(int completedCount, int totalCount, string imagePath, string elapsedText, string summary)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                "\uC77C\uAD04 \uAC80\uC0AC \uD56D\uBAA9 \uC2E4\uD328: {0}/{1} {2} / {3} / {4}",
                completedCount,
                totalCount,
                ResolveImageFileName(imagePath),
                elapsedText,
                summary);
        }

        public string BuildItemPythonStatus(int completedCount, int totalCount, string elapsedText)
        {
            return string.Format(CultureInfo.CurrentCulture, "Python: \uC77C\uAD04 {0}/{1} / \uCD5C\uADFC {2}", completedCount, totalCount, elapsedText);
        }

        public string BuildLatestFileStatus(string imagePath, string elapsedText)
        {
            return string.Format(CultureInfo.CurrentCulture, "{0} / \uCD5C\uADFC {1}", ResolveImageFileName(imagePath), elapsedText);
        }

        public string BuildCompletionCommandStatus(bool canceled, int completedCount, int totalCount, string totalElapsedText)
        {
            string stateText = canceled ? "\uC911\uC9C0" : "\uC644\uB8CC";
            return string.Format(CultureInfo.CurrentCulture, "\uC77C\uAD04 \uAC80\uC0AC {0}: {1}/{2} / {3}", stateText, completedCount, totalCount, totalElapsedText);
        }

        public string BuildCompletionInferenceStatus(bool canceled, int completedCount, int totalCount, string totalElapsedText)
        {
            if (canceled)
            {
                return string.Format(CultureInfo.CurrentCulture, "\uC77C\uAD04 \uC911\uC9C0: {0}/{1}", completedCount, totalCount);
            }

            return string.Format(CultureInfo.CurrentCulture, "\uC77C\uAD04 \uC644\uB8CC: {0}/{1} / {2}", completedCount, totalCount, totalElapsedText);
        }

        public string BuildCompletionLog(bool canceled, int completedCount, int totalCount, string totalElapsedText, string averageElapsedText)
        {
            string stateText = canceled ? "\uC911\uC9C0" : "\uC644\uB8CC";
            return string.Format(
                CultureInfo.CurrentCulture,
                "\uC77C\uAD04 \uAC80\uC0AC {0}. \uC644\uB8CC:{1}/{2} / \uC804\uCCB4:{3} / {4}",
                stateText,
                completedCount,
                totalCount,
                totalElapsedText,
                averageElapsedText);
        }

        public string BuildFailureCommandStatus(int completedCount, int totalCount, string failureSummary)
        {
            return string.Format(CultureInfo.CurrentCulture, "\uC77C\uAD04 \uAC80\uC0AC \uC2E4\uD328: {0}/{1} / {2}", completedCount, totalCount, failureSummary);
        }

        public string BuildFailureInferenceStatus(int completedCount, int totalCount)
        {
            return string.Format(CultureInfo.CurrentCulture, "\uC77C\uAD04 \uC2E4\uD328: {0}/{1}", completedCount, totalCount);
        }

        public string BuildFailureLog(int completedCount, int totalCount, string failureSummary)
        {
            return string.Format(CultureInfo.CurrentCulture, "\uC77C\uAD04 \uAC80\uC0AC \uC2E4\uD328. \uC644\uB8CC:{0}/{1} / {2}", completedCount, totalCount, failureSummary);
        }

        public string ResolveImageFileName(string imagePath)
        {
            return string.IsNullOrWhiteSpace(imagePath) ? string.Empty : Path.GetFileName(imagePath);
        }
    }
}