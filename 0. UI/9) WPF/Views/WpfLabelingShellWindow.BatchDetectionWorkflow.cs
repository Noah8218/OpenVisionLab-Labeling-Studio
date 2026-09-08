using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace MvcVisionSystem
{
    // Responsibility group: batch commands and WPF result/progress projection.
    // These members remain WPF Window adapters; independent policy belongs in services.
    public partial class WpfLabelingShellWindow
    {
        #region BatchDetection
        // BatchDetectionWorkflowService owns batch lifetime; these commands adapt the current queue.
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

            batchDetectionWorkflowService.Cancel();
            AppendLog("\uC77C\uAD04 \uAC80\uC0AC \uC911\uC9C0\uB97C \uC694\uCCAD\uD588\uC2B5\uB2C8\uB2E4.");
        }

        // Queue result and progress projection stay beside the batch commands;
        // execution and lifetime belong to Services/Detection/BatchDetectionWorkflowService.cs.
        private void ApplyDetectionResultToQueueItem(
            WpfImageQueueItem item,
            YoloWorkerSmokeTestResult result,
            bool saveReviewStatus = true,
            bool refreshQueueView = true,
            bool updateQueueStatusText = true)
        {
            if (item == null || result == null || !imageQualityReviewWorkflowService.CanReview(global.Data))
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
                batchDetectionWorkflowService.IsRunning,
                batchDetectionWorkflowService.TotalCount,
                batchDetectionWorkflowService.CompletedCount,
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
        // PL-0035: execution is owned by BatchDetectionWorkflowService.
        private async Task RunBatchDetectionAsync(IReadOnlyList<WpfImageQueueItem> items, string scopeText)
        {
            if (!anomalyImageReviewSession.CanReview(global.Data) || !imageQualityReviewWorkflowService.CanReview(global.Data))
            {
                AppendLog("이미지 목록을 불러온 뒤 일괄 검출을 시작하세요.");
                return;
            }

            if (batchDetectionWorkflowService.IsRunning || imageDetectionWorkflowService.IsDetecting)
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

            if (queue.Any(item => !ReferenceEquals(FindImageQueueItem(item.ImagePath), item)))
            {
                AppendLog("이미지 목록이 변경되었습니다. 현재 목록에서 일괄 검출을 다시 시작하세요.");
                return;
            }

            BatchDetectionRun run = batchDetectionWorkflowService.TryBegin(queue.Count, CaptureBatchReviewStatusSave());
            if (run == null) return;
            string modelSourceText = InferenceStatusPresentationService.BuildRuntimeModelLabel(global.Data?.ProjectSettings?.PythonModel);
            var queueByPath = queue.ToDictionary(item => item.ImagePath, StringComparer.OrdinalIgnoreCase);

            // Only UI/runtime adapters live here. The service owns iteration,
            // cancellation, counts, periodic persistence and exception finalization.
            await batchDetectionWorkflowService.ExecuteAsync(run, queue.Select(item => item.ImagePath).ToArray(), new BatchDetectionCallbacks
            {
                PrepareAsync = async token =>
                {
                    UpdateBatchDetectionControls(scopeText, string.Empty);
                    SetYoloCommandStatus(batchDetectionProgressService.BuildStartCommandStatus(queue.Count), isBusy: true);
                    SetGlobalInferenceStatus(batchDetectionProgressService.BuildStartInferenceStatus(queue.Count), isBusy: true);
                    AppendLog(batchDetectionProgressService.BuildStartLog(scopeText, queue.Count, modelSourceText));
                    SetGlobalInferenceStatus(batchDetectionProgressService.BuildWorkerPreparingInferenceStatus(queue.Count), isBusy: true);
                    SetPythonStatus("추론: 일괄 연결 확인 중");
                    bool ready = await global.ModelRuntime.EnsurePythonModelClientReadyAsync(
                        YoloRuntimePresentationService.GetWorkerConnectTimeoutMilliseconds(
                            global.Data?.ProjectSettings?.PythonModel?.DetectionTimeoutSeconds ?? 30), token).ConfigureAwait(true);
                    if (ready || !run.CanApplyResult) return null;
                    string failure = YoloRuntimePresentationService.BuildPythonWorkerFailureText(
                        global.GetPythonCommunicationStatusSnapshot(), global.ModelRuntime.PythonClientProcess?.LastError);
                    AppendLog($"일괄 검사 시작 실패: {failure}");
                    return failure;
                },
                DetectAsync = (path, token) => RunWorkerDetectionForImageAsync(path, applyToCanvas: false, token, workerReadyAlreadyChecked: true),
                ItemStarting = path =>
                {
                    WpfImageQueueItem item = queueByPath[path];
                    ApplyReviewStatusToItem(item, imageQualityReviewWorkflowService.SetDetectionRequested(path, Path.GetFileNameWithoutExtension(path)));
                    ShowBatchDetectionImage(item);
                    SetGlobalInferenceStatus(batchDetectionProgressService.BuildItemInferenceStatus(run.CompletedCount, run.TotalCount, path), isBusy: true);
                    UpdateBatchDetectionControls(scopeText, batchDetectionProgressService.ResolveImageFileName(path));
                },
                ApplyResult = (path, result, elapsed) =>
                {
                    WpfImageQueueItem item = queueByPath[path];
                    ApplyDetectionResultToQueueItem(item, result, saveReviewStatus: false, refreshQueueView: false, updateQueueStatusText: false);
                    bool displayed = run.CanApplyResult && ApplyBatchDetectionResultToCanvas(item, result);
                    string elapsedText = YoloRuntimePresentationService.FormatElapsed(elapsed);
                    if (result.Succeeded)
                    {
                        AppendLog(batchDetectionProgressService.BuildItemCompletedLog(run.CompletedCount + 1, run.TotalCount, path, result.CandidateCount, elapsedText, modelSourceText));
                    }
                    else if (run.CanApplyResult)
                    {
                        AppendLog(batchDetectionProgressService.BuildItemFailedLog(run.CompletedCount + 1, run.TotalCount, path, elapsedText, result.Summary, modelSourceText));
                    }
                    return displayed;
                },
                ItemCompleted = (path, elapsed) =>
                {
                    string elapsedText = YoloRuntimePresentationService.FormatElapsed(elapsed);
                    SetPythonStatus(batchDetectionProgressService.BuildItemPythonStatus(run.CompletedCount, run.TotalCount, elapsedText));
                    UpdateBatchDetectionControls(scopeText, batchDetectionProgressService.BuildLatestFileStatus(path, elapsedText));
                },
                YieldResultFrameAsync = YieldBatchDetectionResultFrameAsync
            }).ConfigureAwait(true);

            if (isApplicationCloseApproved) return;
            PresentBatchDetectionCompletion(run, modelSourceText);
        }

        private Action CaptureBatchReviewStatusSave()
        {
            LabelingProjectData batchData = global.Data;
            Action saveQualityReviewStatus = imageQualityReviewWorkflowService.CaptureReviewStatusSave(batchData);
            Action saveAnomalyReviewStatus = IsAnomalyDatasetPurpose()
                ? anomalyImageReviewSession.CaptureReviewStatusSave(batchData)
                : null;
            return () =>
            {
                saveQualityReviewStatus();
                saveAnomalyReviewStatus?.Invoke();
            };
        }

        private void PresentBatchDetectionCompletion(BatchDetectionRun run, string modelSourceText)
        {
            bool canceled = run.Token.IsCancellationRequested;
            if (run.HadException) AppendLog("일괄 검사 예외: " + run.FailureSummary);
            imageQueueView?.Refresh();
            UpdateBatchDetectionControls(canceled ? "중지됨" : "완료", string.Empty);
            SetPythonStatus(canceled ? "추론: 일괄 검사 중지" : "추론: 일괄 검사 완료");
            string totalElapsedText = YoloRuntimePresentationService.FormatElapsed(run.Elapsed);
            string averageElapsedText = YoloRuntimePresentationService.FormatAverageElapsed(run.Elapsed, run.CompletedCount);
            SetYoloCommandStatus(batchDetectionProgressService.BuildCompletionCommandStatus(canceled, run.CompletedCount, run.TotalCount, totalElapsedText), isBusy: false);
            SetGlobalInferenceStatus(
                batchDetectionProgressService.BuildCompletionInferenceStatus(canceled, run.CompletedCount, run.TotalCount, totalElapsedText),
                isBusy: false,
                isWarning: canceled);
            AppendLog(batchDetectionProgressService.BuildCompletionLog(canceled, run.CompletedCount, run.TotalCount, totalElapsedText, averageElapsedText, modelSourceText));
            if (!string.IsNullOrEmpty(run.FailureSummary))
            {
                UpdateBatchDetectionControls("실패", string.Empty);
                SetPythonStatus("추론: 일괄 검사 실패");
                SetYoloCommandStatus(batchDetectionProgressService.BuildFailureCommandStatus(run.CompletedCount, run.TotalCount, run.FailureSummary), isBusy: false);
                SetGlobalInferenceStatus(batchDetectionProgressService.BuildFailureInferenceStatus(run.CompletedCount, run.TotalCount, run.FailureSummary), isBusy: false, isWarning: true);
                AppendLog(batchDetectionProgressService.BuildFailureLog(run.CompletedCount, run.TotalCount, run.FailureSummary));
            }
        }
        #endregion

    }
}
