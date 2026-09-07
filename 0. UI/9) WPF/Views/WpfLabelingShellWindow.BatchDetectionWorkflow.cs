using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace MvcVisionSystem
{
    // Responsibility group: batch detection orchestration and canvas result presentation.
    // These members remain WPF Window adapters; independent policy belongs in services.
    public partial class WpfLabelingShellWindow
    {
        #region BatchDetection
        // Queue-driven detection owns batch progress and review-state writes; single-image worker calls stay in the main detection flow.
        private void ExecuteDetectSelectedQueueCommand()
        {
            _ = ExecuteDetectSelectedQueueCommandAsync();
        }

        private async Task ExecuteDetectSelectedQueueCommandAsync()
        {
            if (!EnsureInferenceModeForDetection())
            {
                return;
            }

            if (ImageQueueGrid.SelectedItem is not WpfImageQueueItem item)
            {
                AppendLog("\uBA3C\uC800 \uC774\uBBF8\uC9C0\uB97C \uC120\uD0DD\uD558\uC138\uC694.");
                return;
            }

            await RunInteractiveDetectionAsync(item.ImagePath, allowSmokeFallback: false).ConfigureAwait(true);
        }

        private void ExecuteBatchDetectQueueCommand()
        {
            _ = ExecuteBatchDetectQueueCommandAsync();
        }

        private async Task ExecuteBatchDetectQueueCommandAsync()
        {
            if (!EnsureInferenceModeForDetection())
            {
                return;
            }

            WpfBatchDetectionPlan plan = ShowBatchDetectionPreflight(
                GetVisibleQueueItems(),
                "\uD45C\uC2DC \uD589");
            if (plan != null)
            {
                await RunBatchDetectionAsync(plan.Items, plan.ScopeText).ConfigureAwait(true);
            }
        }

        private void ExecuteRetryFailedQueueCommand()
        {
            _ = ExecuteRetryFailedQueueCommandAsync();
        }

        private async Task ExecuteRetryFailedQueueCommandAsync()
        {
            if (!EnsureInferenceModeForDetection())
            {
                return;
            }

            WpfBatchDetectionPlan plan = ShowBatchDetectionPreflight(
                imageQueueItems.Where(item => item.ReviewState == YoloImageReviewState.Failed).ToList(),
                "\uC2E4\uD328 \uC7AC\uC2DC\uB3C4");
            if (plan != null)
            {
                await RunBatchDetectionAsync(plan.Items, plan.ScopeText).ConfigureAwait(true);
            }
        }

        private WpfBatchDetectionPlan ShowBatchDetectionPreflight(
            IReadOnlyList<WpfImageQueueItem> items,
            string scopeText)
        {
            var viewModel = new WpfBatchDetectionPreflightViewModel(global.Data, items, scopeText);
            var window = new WpfBatchDetectionPreflightWindow(viewModel)
            {
                Owner = this
            };
            window.ApplyThemeFrom(this);
            bool? accepted = window.ShowDialog();
            if (accepted != true || window.SelectedPlan == null)
            {
                AppendLog($"AI \uBC30\uCE58 \uAC80\uC0AC \uCDE8\uC18C: {scopeText}");
                return null;
            }

            AppendLog(
                $"AI \uBC30\uCE58 \uC0AC\uC804\uC810\uAC80 \uD1B5\uACFC: {scopeText} \u00B7 "
                + $"\uC2E4\uD589 {window.SelectedPlan.Items.Count}\uAC1C \u00B7 "
                + "\uACB0\uACFC\uB294 Candidate Review \uB300\uAE30, \uC790\uB3D9 \uC800\uC7A5 \uC5C6\uC74C");
            return window.SelectedPlan;
        }

        private void ExecuteStopBatchQueueCommand()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            batchDetectionCts?.Cancel();
            AppendLog("\uC77C\uAD04 \uAC80\uC0AC \uC911\uC9C0\uB97C \uC694\uCCAD\uD588\uC2B5\uB2C8\uB2E4.");
        }

        // Queue result and progress projection stay beside the batch commands;
        // asynchronous execution/lifetime remains in BatchDetectionExecution.cs.
        private void ApplyDetectionResultToQueueItem(
            WpfImageQueueItem item,
            YoloWorkerSmokeTestResult result,
            bool saveReviewStatus = true,
            bool refreshQueueView = true,
            bool updateQueueStatusText = true)
        {
            if (item == null || result == null)
            {
                return;
            }

            string imageName = Path.GetFileNameWithoutExtension(item.ImagePath);
            YoloImageReviewStatus status = result.Succeeded
                ? result.CandidateCount > 0
                    ? imageQualityReviewWorkflowService.SetDetectionCandidates(item.ImagePath, imageName, result.CandidateCount)
                    : imageQualityReviewWorkflowService.SetDetectionNoCandidates(item.ImagePath, imageName)
                : imageQualityReviewWorkflowService.SetDetectionFailed(item.ImagePath, imageName, result.Summary);
            ApplyReviewStatusToItem(item, status);
            ApplyAnomalyClassificationToImage(item.ImagePath, imageName, result.Candidates, saveReviewStatus);
            if (saveReviewStatus)
            {
                imageQualityReviewWorkflowService.SaveReviewStatus(global.Data);
            }

            if (refreshQueueView)
            {
                imageQueueView?.Refresh();
            }

            if (updateQueueStatusText)
            {
                UpdateImageQueueStatusText();
            }
        }

        private IReadOnlyList<WpfImageQueueItem> GetVisibleQueueItems()
        {
            return imageQueueView == null
                ? imageQueueItems.ToList()
                : imageQueueView.Cast<object>().OfType<WpfImageQueueItem>().ToList();
        }

        private void UpdateBatchDetectionControls(string scopeText = "", string currentFileName = "")
        {
            UpdateYoloCommandButtons();
            BatchDetectionControlState controlState = batchDetectionProgressService.BuildControlState(
                isBatchDetectionRunning,
                batchDetectionTotalCount,
                batchDetectionCompletedCount,
                scopeText,
                currentFileName);

            BatchProgressBar.Maximum = controlState.ProgressMaximum;
            BatchProgressBar.Value = controlState.ProgressValue;
            BatchStatusText.Text = controlState.StatusText;

            if (controlState.ShouldRefreshQueueStatus)
            {
                UpdateImageQueueStatusText();
            }
            else
            {
                SetDatasetStatus(controlState.DatasetStatusText);
            }
        }
        #endregion

        #region BatchDetectionCanvas
        // Canvas preview during batch detection is intentionally throttled through Dispatcher yields.
        private bool ShowBatchDetectionImage(WpfImageQueueItem item)
        {
            if (item == null
                || string.IsNullOrWhiteSpace(item.ImagePath)
                || !File.Exists(item.ImagePath))
            {
                return false;
            }

            SelectImageQueueItem(item.ImagePath);
            bool loaded = TryLoadImage(
                item.ImagePath,
                populateQueue: false,
                refreshQueueDetails: false,
                refreshActiveStatus: false,
                appendLoadLog: false);
            if (loaded)
            {
                UpdateSelectedQueueImageButton(item);
            }

            return loaded;
        }

        private bool IsActiveImagePath(string imagePath)
        {
            return !string.IsNullOrWhiteSpace(imagePath)
                && string.Equals(activeImagePath, imagePath, StringComparison.OrdinalIgnoreCase);
        }

        private bool ApplyBatchDetectionResultToCanvas(WpfImageQueueItem item, YoloWorkerSmokeTestResult result)
        {
            if (item == null || result == null)
            {
                return false;
            }

            if (!IsActiveImagePath(item.ImagePath) && !ShowBatchDetectionImage(item))
            {
                return false;
            }

            SelectImageQueueItem(item.ImagePath);
            ApplyBatchDetectionCandidates(result.Candidates, result.Succeeded);
            if (!result.Succeeded)
            {
                ShowBatchDetectionFailureResult(item, result);
            }
            else if (pendingDetectionCandidates.Count == 0)
            {
                ShowBatchNoCandidateResult(item, result);
            }

            return true;
        }

        private async Task YieldBatchDetectionResultFrameAsync(CancellationToken token)
        {
            if (token.IsCancellationRequested || Dispatcher == null)
            {
                return;
            }

            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            if (token.IsCancellationRequested)
            {
                return;
            }

            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
        }

        private void ShowBatchNoCandidateResult(WpfImageQueueItem item, YoloWorkerSmokeTestResult result)
        {
            if (CanvasPanelViewModel == null)
            {
                return;
            }

            WpfDetectionOverlayPresentation presentation = detectionResultPresentationService.BuildNoCandidateOverlay(
                item?.ImagePath ?? result?.ImagePath ?? activeImagePath ?? string.Empty,
                GetCandidateConfidenceFilter());
            CanvasPanelViewModel.SetDetectionOverlay(
                presentation.Title,
                presentation.Summary,
                presentation.SelectedText,
                presentation.Detail,
                presentation.Status);
        }

        private void ShowBatchDetectionFailureResult(WpfImageQueueItem item, YoloWorkerSmokeTestResult result)
        {
            if (CanvasPanelViewModel == null)
            {
                return;
            }

            WpfDetectionOverlayPresentation presentation = detectionResultPresentationService.BuildFailureOverlay(
                item?.ImagePath ?? result?.ImagePath ?? activeImagePath ?? string.Empty,
                result?.Summary);
            CanvasPanelViewModel.SetDetectionOverlay(
                presentation.Title,
                presentation.Summary,
                presentation.SelectedText,
                presentation.Detail,
                presentation.Status);
        }

        private void ApplyBatchDetectionCandidates(IReadOnlyList<YoloWorkerSmokeCandidate> candidates, bool succeeded)
        {
            int loadedCount = candidateReviewState.LoadPendingCandidates(candidates, clearConfirmed: true);
            CandidateReviewViewModel?.ClearReviewHistory();

            ApplyCanvasDisplayMode(WpfCanvasDisplayMode.InferenceOnly, redraw: false, logChange: false);
            RefreshCandidateList();
            RefreshObjectList();
            RedrawReviewRois();
            SetActiveImageDetectionStatus(loadedCount, succeeded);
            AddCandidateReviewHistory(detectionResultPresentationService.BuildCandidateLoadHistory(loadedCount, succeeded, GetCandidateConfidenceFilter()));
            ShowCandidateReviewWorkflowView();

        }
        #endregion

        #region BatchDetectionExecution
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
            string modelSourceText = InferenceStatusPresentationService.BuildRuntimeModelLabel(
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
                bool workerReady = await global.ModelRuntime
                    .EnsurePythonModelClientReadyAsync(
                        YoloRuntimePresentationService.GetWorkerConnectTimeoutMilliseconds(
                            global.Data?.ProjectSettings?.PythonModel?.DetectionTimeoutSeconds ?? 30),
                        token)
                    .ConfigureAwait(true);
                if (isApplicationCloseApproved)
                {
                    return;
                }

                if (!workerReady)
                {
                    batchFailed = true;
                    batchFailureSummary = YoloRuntimePresentationService.BuildPythonWorkerFailureText(
                        global.GetPythonCommunicationStatusSnapshot(),
                        global.ModelRuntime.PythonClientProcess?.LastError);
                    AppendLog($"일괄 검사 시작 실패: {batchFailureSummary}");
                    return;
                }

                foreach (WpfImageQueueItem item in queue)
                {
                    if (isApplicationCloseApproved || token.IsCancellationRequested)
                    {
                        break;
                    }

                    string imageName = Path.GetFileNameWithoutExtension(item.ImagePath);
                    ApplyReviewStatusToItem(item, imageQualityReviewWorkflowService.SetDetectionRequested(item.ImagePath, imageName));
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
                    if (isApplicationCloseApproved)
                    {
                        break;
                    }

                    TimeSpan itemElapsed = itemStopwatch.Elapsed;
                    result.ElapsedMilliseconds ??= YoloRuntimePresentationService.ClampElapsedMilliseconds(itemElapsed);
                    int nextCompleted = batchDetectionCompletedCount + 1;
                    string elapsedText = YoloRuntimePresentationService.FormatElapsed(itemElapsed);
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
                        imageQualityReviewWorkflowService.SaveReviewStatus(global.Data);
                        if (IsAnomalyDatasetPurpose())
                        {
                            SaveAnomalyImageReviewStatus();
                        }

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
            catch (OperationCanceledException) when (token.IsCancellationRequested || isApplicationCloseApproved)
            {
                // The completion block owns the stopped/closed status projection.
            }
            catch (Exception ex)
            {
                batchFailed = true;
                batchFailureSummary = string.IsNullOrWhiteSpace(ex.Message)
                    ? "일괄 검사 중 알 수 없는 오류가 발생했습니다."
                    : ex.Message.Trim();
                if (!isApplicationCloseApproved)
                {
                    AppendLog("일괄 검사 예외: " + batchFailureSummary);
                }
            }
            finally
            {
                bool canceled = token.IsCancellationRequested;
                isBatchDetectionRunning = false;
                if (!isApplicationCloseApproved)
                {
                    if (pendingReviewStatusSaves > 0 || batchDetectionCompletedCount > 0)
                    {
                        imageQualityReviewWorkflowService.SaveReviewStatus(global.Data);
                        if (IsAnomalyDatasetPurpose())
                        {
                            SaveAnomalyImageReviewStatus();
                        }
                    }

                    imageQueueView?.Refresh();
                    UpdateBatchDetectionControls(canceled ? "중지됨" : "완료", string.Empty);
                    SetPythonStatus(canceled ? "\uCD94\uB860: \uC77C\uAD04 \uAC80\uC0AC \uC911\uC9C0" : "\uCD94\uB860: \uC77C\uAD04 \uAC80\uC0AC \uC644\uB8CC");
                    string totalElapsedText = YoloRuntimePresentationService.FormatElapsed(batchStopwatch.Elapsed);
                    string averageElapsedText = YoloRuntimePresentationService.FormatAverageElapsed(batchStopwatch.Elapsed, batchDetectionCompletedCount);
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
        #endregion

    }
}
