using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // The batch loop owns progress and cancellation; per-item result presentation lives in smaller helpers.
        private async Task RunBatchDetectionAsync(IReadOnlyList<WpfImageQueueItem> items, string scopeText)
        {
            if (isBatchDetectionRunning || isDetecting)
            {
                AppendLog("검출이 이미 실행 중입니다.");
                return;
            }

            IReadOnlyList<WpfImageQueueItem> queue = detectionTargetService.BuildBatchQueue(items);
            if (queue.Count == 0)
            {
                AppendLog(detectionTargetService.BuildEmptyBatchMessage(scopeText));
                return;
            }

            batchDetectionCts?.Cancel();
            batchDetectionCts?.Dispose();
            batchDetectionCts = new CancellationTokenSource();
            CancellationToken token = batchDetectionCts.Token;
            isBatchDetectionRunning = true;
            batchDetectionTotalCount = queue.Count;
            batchDetectionCompletedCount = 0;
            UpdateBatchDetectionControls(scopeText, string.Empty);
            SetYoloCommandStatus(batchDetectionProgressService.BuildStartCommandStatus(queue.Count), isBusy: true);
            SetGlobalInferenceStatus(batchDetectionProgressService.BuildStartInferenceStatus(queue.Count), isBusy: true);
            string modelSourceText = WpfInferenceStatusPresentationService.BuildRuntimeModelLabel(
                global.Data?.ProjectSettings?.PythonModel);

            AppendLog(batchDetectionProgressService.BuildStartLog(scopeText, queue.Count, modelSourceText));
            var batchStopwatch = Stopwatch.StartNew();
            int pendingReviewStatusSaves = 0;
            bool batchFailed = false;
            string batchFailureSummary = string.Empty;
            try
            {
                SetGlobalInferenceStatus(batchDetectionProgressService.BuildWorkerPreparingInferenceStatus(queue.Count), isBusy: true);
                SetPythonStatus("\uCD94\uB860: \uC77C\uAD04 \uC5F0\uACB0 \uD655\uC778 \uC911");
                bool workerReady = await global
                    .EnsurePythonModelClientReadyAsync(GetWorkerConnectTimeoutMilliseconds())
                    .ConfigureAwait(true);
                if (!workerReady)
                {
                    batchFailed = true;
                    batchFailureSummary = BuildPythonWorkerFailureText();
                    AppendLog($"일괄 검사 시작 실패: {batchFailureSummary}");
                    return;
                }

                foreach (WpfImageQueueItem item in queue)
                {
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    string imageName = Path.GetFileNameWithoutExtension(item.ImagePath);
                    ApplyReviewStatusToItem(item, imageReviewStatus.SetDetectionRequested(item.ImagePath, imageName));
                    ShowBatchDetectionImage(item);
                    string currentFileName = batchDetectionProgressService.ResolveImageFileName(item.ImagePath);
                    SetGlobalInferenceStatus(batchDetectionProgressService.BuildItemInferenceStatus(batchDetectionCompletedCount, batchDetectionTotalCount, item.ImagePath), isBusy: true);
                    UpdateBatchDetectionControls(scopeText, currentFileName);

                    var itemStopwatch = Stopwatch.StartNew();
                    YoloWorkerSmokeTestResult result = await RunWorkerDetectionForImageAsync(
                        item.ImagePath,
                        applyToCanvas: false,
                        token,
                        workerReadyAlreadyChecked: true).ConfigureAwait(true);
                    TimeSpan itemElapsed = itemStopwatch.Elapsed;
                    result.ElapsedMilliseconds ??= ClampElapsedMilliseconds(itemElapsed);
                    int nextCompleted = batchDetectionCompletedCount + 1;
                    string elapsedText = FormatElapsed(itemElapsed);
                    ApplyDetectionResultToQueueItem(
                        item,
                        result,
                        saveReviewStatus: false,
                        refreshQueueView: false,
                        updateQueueStatusText: false);

                    bool displayedResult = !token.IsCancellationRequested
                        && ApplyBatchDetectionResultToCanvas(item, result);
                    if (result.Succeeded)
                    {
                        AppendLog(batchDetectionProgressService.BuildItemCompletedLog(nextCompleted, batchDetectionTotalCount, item.ImagePath, result.CandidateCount, elapsedText, modelSourceText));
                    }
                    else if (!token.IsCancellationRequested)
                    {
                        AppendLog(batchDetectionProgressService.BuildItemFailedLog(nextCompleted, batchDetectionTotalCount, item.ImagePath, elapsedText, result.Summary, modelSourceText));
                    }

                    pendingReviewStatusSaves++;
                    if (pendingReviewStatusSaves >= BatchReviewStatusSaveInterval)
                    {
                        imageReviewStatus.SaveReviewStatus(global.Data);
                        pendingReviewStatusSaves = 0;
                    }

                    batchDetectionCompletedCount++;
                    SetPythonStatus(batchDetectionProgressService.BuildItemPythonStatus(batchDetectionCompletedCount, batchDetectionTotalCount, elapsedText));
                    UpdateBatchDetectionControls(scopeText, batchDetectionProgressService.BuildLatestFileStatus(item.ImagePath, elapsedText));
                    if (displayedResult)
                    {
                        await YieldBatchDetectionResultFrameAsync(token).ConfigureAwait(true);
                    }
                }
            }
            finally
            {
                bool canceled = token.IsCancellationRequested;
                isBatchDetectionRunning = false;
                if (pendingReviewStatusSaves > 0 || batchDetectionCompletedCount > 0)
                {
                    imageReviewStatus.SaveReviewStatus(global.Data);
                }

                imageQueueView?.Refresh();
                UpdateBatchDetectionControls(canceled ? "중지됨" : "완료", string.Empty);
                SetPythonStatus(canceled ? "\uCD94\uB860: \uC77C\uAD04 \uAC80\uC0AC \uC911\uC9C0" : "\uCD94\uB860: \uC77C\uAD04 \uAC80\uC0AC \uC644\uB8CC");
                string totalElapsedText = FormatElapsed(batchStopwatch.Elapsed);
                string averageElapsedText = FormatAverageElapsed(batchStopwatch.Elapsed, batchDetectionCompletedCount);
                SetYoloCommandStatus(batchDetectionProgressService.BuildCompletionCommandStatus(canceled, batchDetectionCompletedCount, batchDetectionTotalCount, totalElapsedText), isBusy: false);
                SetGlobalInferenceStatus(
                    batchDetectionProgressService.BuildCompletionInferenceStatus(canceled, batchDetectionCompletedCount, batchDetectionTotalCount, totalElapsedText),
                    isBusy: false,
                    isWarning: canceled);
                AppendLog(batchDetectionProgressService.BuildCompletionLog(canceled, batchDetectionCompletedCount, batchDetectionTotalCount, totalElapsedText, averageElapsedText, modelSourceText));
                if (batchFailed)
                {
                    UpdateBatchDetectionControls("실패", string.Empty);
                    SetPythonStatus("\uCD94\uB860: \uC77C\uAD04 \uAC80\uC0AC \uC2E4\uD328");
                    SetYoloCommandStatus(batchDetectionProgressService.BuildFailureCommandStatus(batchDetectionCompletedCount, batchDetectionTotalCount, batchFailureSummary), isBusy: false);
                    SetGlobalInferenceStatus(batchDetectionProgressService.BuildFailureInferenceStatus(batchDetectionCompletedCount, batchDetectionTotalCount, batchFailureSummary), isBusy: false, isWarning: true);
                    AppendLog(batchDetectionProgressService.BuildFailureLog(batchDetectionCompletedCount, batchDetectionTotalCount, batchFailureSummary));
                }
            }
        }
    }
}
