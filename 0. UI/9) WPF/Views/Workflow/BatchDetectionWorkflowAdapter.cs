using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the Shell-facing batch detection workflow orchestration.
    /// BatchDetectionWorkflowService remains the execution and lifetime owner;
    /// this adapter translates its callbacks into existing queue, canvas,
    /// candidate-review, and status owners.
    /// </summary>
    internal sealed class BatchDetectionWorkflowAdapter
    {
        private readonly BatchDetectionWorkflowAdapterContext context;

        private LabelingProjectData projectData => context.DataProvider?.Invoke();

        internal BatchDetectionWorkflowAdapter(BatchDetectionWorkflowAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            ArgumentNullException.ThrowIfNull(context.DataProvider);
            ArgumentNullException.ThrowIfNull(context.BatchDetectionWorkflowService);
            ArgumentNullException.ThrowIfNull(context.BatchDetectionProgressService);
            ArgumentNullException.ThrowIfNull(context.DetectionTargetService);
            ArgumentNullException.ThrowIfNull(context.ImageDetectionWorkflowService);
            ArgumentNullException.ThrowIfNull(context.ImageQualityReviewWorkflowService);
            ArgumentNullException.ThrowIfNull(context.AnomalyImageReviewSession);
            ArgumentNullException.ThrowIfNull(context.DetectionResultPresentationService);
            ArgumentNullException.ThrowIfNull(context.CandidateReviewState);
            ArgumentNullException.ThrowIfNull(context.QueueItemsProvider);
            ArgumentNullException.ThrowIfNull(context.VisibleQueueItemsProvider);
            ArgumentNullException.ThrowIfNull(context.RunInteractiveDetectionAsync);
            ArgumentNullException.ThrowIfNull(context.RunWorkerDetectionAsync);
            ArgumentNullException.ThrowIfNull(context.YieldBatchDetectionResultFrameAsync);
            ArgumentNullException.ThrowIfNull(context.EnsurePythonModelClientReadyAsync);
            ArgumentNullException.ThrowIfNull(context.CommunicationStatusProvider);
            ArgumentNullException.ThrowIfNull(context.PythonWorkerLastErrorProvider);
        }

        internal void ExecuteDetectSelectedQueueCommand()
        {
            _ = ExecuteDetectSelectedQueueCommandAsync();
        }

        internal async Task ExecuteDetectSelectedQueueCommandAsync()
        {
            if (context.EnsureInferenceModeForDetection?.Invoke() != true)
            {
                return;
            }

            WpfImageQueueItem item = context.SelectedQueueItemProvider?.Invoke();
            if (item == null)
            {
                context.AppendLog?.Invoke("\uBA3C\uC800 \uC774\uBBF8\uC9C0\uB97C \uC120\uD0DD\uD558\uC138\uC694.");
                return;
            }

            await context.RunInteractiveDetectionAsync(item.ImagePath, false).ConfigureAwait(true);
        }

        internal void ExecuteBatchDetectQueueCommand()
        {
            _ = ExecuteBatchDetectQueueCommandAsync();
        }

        internal async Task ExecuteBatchDetectQueueCommandAsync()
        {
            if (context.EnsureInferenceModeForDetection?.Invoke() != true)
            {
                return;
            }

            WpfBatchDetectionPlan plan = context.ShowBatchDetectionPreflight?.Invoke(
                GetVisibleQueueItems(),
                "\uD45C\uC2DC \uD589");
            if (plan != null)
            {
                await RunBatchDetectionAsync(plan.Items, plan.ScopeText).ConfigureAwait(true);
            }
        }

        internal void ExecuteRetryFailedQueueCommand()
        {
            _ = ExecuteRetryFailedQueueCommandAsync();
        }

        internal async Task ExecuteRetryFailedQueueCommandAsync()
        {
            if (context.EnsureInferenceModeForDetection?.Invoke() != true)
            {
                return;
            }

            WpfBatchDetectionPlan plan = context.ShowBatchDetectionPreflight?.Invoke(
                context.QueueItemsProvider()
                    .Where(item => item?.ReviewState == YoloImageReviewState.Failed)
                    .ToList(),
                "\uC2E4\uD328 \uC7AC\uC2DC\uB3C4");
            if (plan != null)
            {
                await RunBatchDetectionAsync(plan.Items, plan.ScopeText).ConfigureAwait(true);
            }
        }

        internal void ExecuteStopBatchQueueCommand()
        {
            if (context.IsApplicationCloseApproved?.Invoke() == true)
            {
                return;
            }

            context.BatchDetectionWorkflowService.Cancel();
            context.AppendLog?.Invoke("\uC77C\uAD04 \uAC80\uC0AC \uC911\uC9C0\uB97C \uC694\uCCAD\uD588\uC2B5\uB2C8\uB2E4.");
        }

        internal void ApplyDetectionResultToQueueItem(
            WpfImageQueueItem item,
            YoloWorkerSmokeTestResult result,
            bool saveReviewStatus = true,
            bool refreshQueueView = true,
            bool updateQueueStatusText = true)
        {
            if (item == null
                || result == null
                || !context.ImageQualityReviewWorkflowService.CanReview(projectData))
            {
                return;
            }

            string imageName = Path.GetFileNameWithoutExtension(item.ImagePath);
            YoloImageReviewStatus status = result.Succeeded
                ? result.CandidateCount > 0
                    ? context.ImageQualityReviewWorkflowService.SetDetectionCandidates(item.ImagePath, imageName, result.CandidateCount)
                    : context.ImageQualityReviewWorkflowService.SetDetectionNoCandidates(item.ImagePath, imageName)
                : context.ImageQualityReviewWorkflowService.SetDetectionFailed(item.ImagePath, imageName, result.Summary);
            context.ApplyReviewStatusToItem?.Invoke(item, status);
            context.ApplyAnomalyClassificationToImage?.Invoke(item.ImagePath, imageName, result.Candidates, saveReviewStatus);
            if (saveReviewStatus)
            {
                context.ImageQualityReviewWorkflowService.SaveReviewStatus(projectData);
            }

            if (refreshQueueView)
            {
                context.RefreshQueueView?.Invoke();
            }

            if (updateQueueStatusText)
            {
                context.UpdateImageQueueStatusText?.Invoke();
            }
        }

        internal IReadOnlyList<WpfImageQueueItem> GetVisibleQueueItems()
            => context.VisibleQueueItemsProvider() ?? Array.Empty<WpfImageQueueItem>();

        internal void UpdateBatchDetectionControls(string scopeText = "", string currentFileName = "")
        {
            BatchDetectionControlState controlState = context.BatchDetectionProgressService.BuildControlState(
                context.BatchDetectionWorkflowService.IsRunning,
                context.BatchDetectionWorkflowService.TotalCount,
                context.BatchDetectionWorkflowService.CompletedCount,
                scopeText,
                currentFileName);
            context.ApplyBatchDetectionControls?.Invoke(controlState);
        }

        internal bool ShowBatchDetectionImage(WpfImageQueueItem item)
        {
            if (item == null
                || string.IsNullOrWhiteSpace(item.ImagePath)
                || !File.Exists(item.ImagePath))
            {
                return false;
            }

            context.SelectImageQueueItem?.Invoke(item.ImagePath);
            bool loaded = context.TryLoadBatchImage?.Invoke(item.ImagePath) == true;
            if (loaded)
            {
                context.UpdateSelectedQueueImageButton?.Invoke(item);
            }

            return loaded;
        }

        internal bool IsActiveImagePath(string imagePath)
        {
            return !string.IsNullOrWhiteSpace(imagePath)
                && string.Equals(context.ActiveImagePathProvider?.Invoke(), imagePath, StringComparison.OrdinalIgnoreCase);
        }

        internal bool ApplyBatchDetectionResultToCanvas(WpfImageQueueItem item, YoloWorkerSmokeTestResult result)
        {
            if (item == null || result == null)
            {
                return false;
            }

            if (!IsActiveImagePath(item.ImagePath) && !ShowBatchDetectionImage(item))
            {
                return false;
            }

            context.SelectImageQueueItem?.Invoke(item.ImagePath);
            ApplyBatchDetectionCandidates(result.Candidates, result.Succeeded);
            if (!result.Succeeded)
            {
                ShowBatchDetectionFailureResult(item, result);
            }
            else if (context.CandidateReviewState.PendingCandidates.Count == 0)
            {
                ShowBatchNoCandidateResult(item, result);
            }

            return true;
        }

        internal Task YieldBatchDetectionResultFrameAsync(CancellationToken token)
        {
            return context.YieldBatchDetectionResultFrameAsync(token);
        }

        internal void ShowBatchNoCandidateResult(WpfImageQueueItem item, YoloWorkerSmokeTestResult result)
        {
            WpfDetectionOverlayPresentation presentation = context.DetectionResultPresentationService.BuildNoCandidateOverlay(
                item?.ImagePath ?? result?.ImagePath ?? context.ActiveImagePathProvider?.Invoke() ?? string.Empty,
                context.CandidateConfidenceFilterProvider?.Invoke() ?? 0D);
            context.SetDetectionOverlay?.Invoke(presentation);
        }

        internal void ShowBatchDetectionFailureResult(WpfImageQueueItem item, YoloWorkerSmokeTestResult result)
        {
            WpfDetectionOverlayPresentation presentation = context.DetectionResultPresentationService.BuildFailureOverlay(
                item?.ImagePath ?? result?.ImagePath ?? context.ActiveImagePathProvider?.Invoke() ?? string.Empty,
                result?.Summary);
            context.SetDetectionOverlay?.Invoke(presentation);
        }

        internal void ApplyBatchDetectionCandidates(IReadOnlyList<YoloWorkerSmokeCandidate> candidates, bool succeeded)
        {
            int loadedCount = context.CandidateReviewState.LoadPendingCandidates(candidates, clearConfirmed: true);
            context.ClearCandidateReviewHistory?.Invoke();
            context.ApplyCanvasDisplayMode?.Invoke(WpfCanvasDisplayMode.InferenceOnly, false, false);
            context.RefreshCandidateList?.Invoke();
            context.RefreshObjectList?.Invoke();
            context.RedrawReviewRois?.Invoke();
            context.SetActiveImageDetectionStatus?.Invoke(loadedCount, succeeded);
            context.AddCandidateReviewHistory?.Invoke(
                context.DetectionResultPresentationService.BuildCandidateLoadHistory(
                    loadedCount,
                    succeeded,
                    context.CandidateConfidenceFilterProvider?.Invoke() ?? 0D));
            context.ShowCandidateReviewWorkflowView?.Invoke();
        }

        internal async Task RunBatchDetectionAsync(IReadOnlyList<WpfImageQueueItem> items, string scopeText)
        {
            if (!context.AnomalyImageReviewSession.CanReview(projectData)
                || !context.ImageQualityReviewWorkflowService.CanReview(projectData))
            {
                context.AppendLog?.Invoke("\uC774\uBBF8\uC9C0 \uBAA9\uB85D\uC744 \uBD88\uB7EC\uC628 \uB4A4 \uC77C\uAD04 \uAC80\uCD9C\uC744 \uC2DC\uC791\uD558\uC138\uC694.");
                return;
            }

            if (context.BatchDetectionWorkflowService.IsRunning
                || context.ImageDetectionWorkflowService.IsDetecting)
            {
                context.AppendLog?.Invoke("\uAC80\uCD9C\uC774 \uC774\uBBF8 \uC2E4\uD589 \uC911\uC785\uB2C8\uB2E4.");
                return;
            }

            IReadOnlyList<WpfImageQueueItem> queue = context.DetectionTargetService.BuildBatchQueue(items);
            if (queue.Count == 0)
            {
                context.AppendLog?.Invoke(context.DetectionTargetService.BuildEmptyBatchMessage(scopeText));
                return;
            }

            if (queue.Any(item => !ReferenceEquals(context.FindQueueItem?.Invoke(item.ImagePath), item)))
            {
                context.AppendLog?.Invoke("\uC774\uBBF8\uC9C0 \uBAA9\uB85D\uC774 \uBCC0\uACBD\uB418\uC5C8\uC2B5\uB2C8\uB2E4. \uD604\uC7AC \uBAA9\uB85D\uC5D0\uC11C \uC77C\uAD04 \uAC80\uAC80\uC744 \uB2E4\uC2DC \uC2DC\uC791\uD558\uC138\uC694.");
                return;
            }

            BatchDetectionRun run = context.BatchDetectionWorkflowService.TryBegin(queue.Count, CaptureBatchReviewStatusSave());
            if (run == null)
            {
                return;
            }

            string modelSourceText = InferenceStatusPresentationService.BuildRuntimeModelLabel(
                projectData?.ProjectSettings?.PythonModel);
            Dictionary<string, WpfImageQueueItem> queueByPath = queue.ToDictionary(
                item => item.ImagePath,
                StringComparer.OrdinalIgnoreCase);

            await context.BatchDetectionWorkflowService.ExecuteAsync(
                run,
                queue.Select(item => item.ImagePath).ToArray(),
                new BatchDetectionCallbacks
                {
                    PrepareAsync = async token =>
                    {
                        UpdateBatchDetectionControls(scopeText, string.Empty);
                        context.SetYoloCommandStatus?.Invoke(
                            context.BatchDetectionProgressService.BuildStartCommandStatus(queue.Count),
                            true);
                        context.SetGlobalInferenceStatus?.Invoke(
                            context.BatchDetectionProgressService.BuildStartInferenceStatus(queue.Count),
                            true,
                            false);
                        context.AppendLog?.Invoke(
                            context.BatchDetectionProgressService.BuildStartLog(scopeText, queue.Count, modelSourceText));
                        context.SetGlobalInferenceStatus?.Invoke(
                            context.BatchDetectionProgressService.BuildWorkerPreparingInferenceStatus(queue.Count),
                            true,
                            false);
                        context.SetPythonStatus?.Invoke("\uCD94\uB860: \uC77C\uAD04 \uC5F0\uACB0 \uD655\uC778 \uC911");
                        bool ready = await context.EnsurePythonModelClientReadyAsync(
                            YoloRuntimePresentationService.GetWorkerConnectTimeoutMilliseconds(
                                projectData?.ProjectSettings?.PythonModel?.DetectionTimeoutSeconds ?? 30),
                            token).ConfigureAwait(true);
                        if (ready || !run.CanApplyResult)
                        {
                            return null;
                        }

                        string failure = YoloRuntimePresentationService.BuildPythonWorkerFailureText(
                            context.CommunicationStatusProvider(),
                            context.PythonWorkerLastErrorProvider());
                        context.AppendLog?.Invoke($"\uC77C\uAD04 \uAC80\uC0AC \uC2DC\uC791 \uC2E4\uD328: {failure}");
                        return failure;
                    },
                    DetectAsync = (path, token) => context.RunWorkerDetectionAsync(path, token),
                    ItemStarting = path =>
                    {
                        WpfImageQueueItem item = queueByPath[path];
                        context.ApplyReviewStatusToItem?.Invoke(
                            item,
                            context.ImageQualityReviewWorkflowService.SetDetectionRequested(
                                path,
                                Path.GetFileNameWithoutExtension(path)));
                        ShowBatchDetectionImage(item);
                        context.SetGlobalInferenceStatus?.Invoke(
                            context.BatchDetectionProgressService.BuildItemInferenceStatus(
                                run.CompletedCount,
                                run.TotalCount,
                                path),
                            true,
                            false);
                        UpdateBatchDetectionControls(
                            scopeText,
                            context.BatchDetectionProgressService.ResolveImageFileName(path));
                    },
                    ApplyResult = (path, result, elapsed) =>
                    {
                        WpfImageQueueItem item = queueByPath[path];
                        ApplyDetectionResultToQueueItem(
                            item,
                            result,
                            saveReviewStatus: false,
                            refreshQueueView: false,
                            updateQueueStatusText: false);
                        bool displayed = run.CanApplyResult && ApplyBatchDetectionResultToCanvas(item, result);
                        string elapsedText = YoloRuntimePresentationService.FormatElapsed(elapsed);
                        if (result.Succeeded)
                        {
                            context.AppendLog?.Invoke(
                                context.BatchDetectionProgressService.BuildItemCompletedLog(
                                    run.CompletedCount + 1,
                                    run.TotalCount,
                                    path,
                                    result.CandidateCount,
                                    elapsedText,
                                    modelSourceText));
                        }
                        else if (run.CanApplyResult)
                        {
                            context.AppendLog?.Invoke(
                                context.BatchDetectionProgressService.BuildItemFailedLog(
                                    run.CompletedCount + 1,
                                    run.TotalCount,
                                    path,
                                    elapsedText,
                                    result.Summary,
                                    modelSourceText));
                        }

                        return displayed;
                    },
                    ItemCompleted = (path, elapsed) =>
                    {
                        string elapsedText = YoloRuntimePresentationService.FormatElapsed(elapsed);
                        context.SetPythonStatus?.Invoke(
                            context.BatchDetectionProgressService.BuildItemPythonStatus(
                                run.CompletedCount,
                                run.TotalCount,
                                elapsedText));
                        UpdateBatchDetectionControls(
                            scopeText,
                            context.BatchDetectionProgressService.BuildLatestFileStatus(path, elapsedText));
                    },
                    YieldResultFrameAsync = YieldBatchDetectionResultFrameAsync
                }).ConfigureAwait(true);

            if (context.IsApplicationCloseApproved?.Invoke() == true)
            {
                return;
            }

            PresentBatchDetectionCompletion(run, modelSourceText);
        }

        internal Action CaptureBatchReviewStatusSave()
        {
            LabelingProjectData batchData = projectData;
            Action saveQualityReviewStatus = context.ImageQualityReviewWorkflowService.CaptureReviewStatusSave(batchData);
            Action saveAnomalyReviewStatus = context.IsAnomalyDatasetPurpose?.Invoke() == true
                ? context.AnomalyImageReviewSession.CaptureReviewStatusSave(batchData)
                : null;
            return () =>
            {
                saveQualityReviewStatus();
                saveAnomalyReviewStatus?.Invoke();
            };
        }

        internal void PresentBatchDetectionCompletion(BatchDetectionRun run, string modelSourceText)
        {
            bool canceled = run.Token.IsCancellationRequested;
            if (run.HadException)
            {
                context.AppendLog?.Invoke("\uC77C\uAD04 \uAC80\uC0AC \uC608\uC678: " + run.FailureSummary);
            }

            context.RefreshQueueView?.Invoke();
            UpdateBatchDetectionControls(canceled ? "\uC911\uC9C0\uB428" : "\uC644\uB8CC", string.Empty);
            context.SetPythonStatus?.Invoke(canceled ? "\uCD94\uB860: \uC77C\uAD04 \uAC80\uC0AC \uC911\uC9C0" : "\uCD94\uB860: \uC77C\uAD04 \uAC80\uC0AC \uC644\uB8CC");
            string totalElapsedText = YoloRuntimePresentationService.FormatElapsed(run.Elapsed);
            string averageElapsedText = YoloRuntimePresentationService.FormatAverageElapsed(run.Elapsed, run.CompletedCount);
            context.SetYoloCommandStatus?.Invoke(
                context.BatchDetectionProgressService.BuildCompletionCommandStatus(
                    canceled,
                    run.CompletedCount,
                    run.TotalCount,
                    totalElapsedText),
                false);
            context.SetGlobalInferenceStatus?.Invoke(
                context.BatchDetectionProgressService.BuildCompletionInferenceStatus(
                    canceled,
                    run.CompletedCount,
                    run.TotalCount,
                    totalElapsedText),
                false,
                canceled);
            context.AppendLog?.Invoke(
                context.BatchDetectionProgressService.BuildCompletionLog(
                    canceled,
                    run.CompletedCount,
                    run.TotalCount,
                    totalElapsedText,
                    averageElapsedText,
                    modelSourceText));
            if (!string.IsNullOrEmpty(run.FailureSummary))
            {
                UpdateBatchDetectionControls("\uC2E4\uD328", string.Empty);
                context.SetPythonStatus?.Invoke("\uCD94\uB860: \uC77C\uAD04 \uAC80\uC0AC \uC2E4\uD328");
                context.SetYoloCommandStatus?.Invoke(
                    context.BatchDetectionProgressService.BuildFailureCommandStatus(
                        run.CompletedCount,
                        run.TotalCount,
                        run.FailureSummary),
                    false);
                context.SetGlobalInferenceStatus?.Invoke(
                    context.BatchDetectionProgressService.BuildFailureInferenceStatus(
                        run.CompletedCount,
                        run.TotalCount,
                        run.FailureSummary),
                    false,
                    true);
                context.AppendLog?.Invoke(
                    context.BatchDetectionProgressService.BuildFailureLog(
                        run.CompletedCount,
                        run.TotalCount,
                        run.FailureSummary));
            }
        }
    }

    internal sealed class BatchDetectionWorkflowAdapterContext
    {
        internal Func<LabelingProjectData> DataProvider { get; init; }
        internal BatchDetectionWorkflowService BatchDetectionWorkflowService { get; init; }
        internal BatchDetectionProgressService BatchDetectionProgressService { get; init; }
        internal DetectionTargetService DetectionTargetService { get; init; }
        internal ImageDetectionWorkflowService ImageDetectionWorkflowService { get; init; }
        internal ImageQualityReviewWorkflowService ImageQualityReviewWorkflowService { get; init; }
        internal AnomalyImageReviewSession AnomalyImageReviewSession { get; init; }
        internal DetectionResultPresentationService DetectionResultPresentationService { get; init; }
        internal CandidateReviewStateService CandidateReviewState { get; init; }
        internal Func<IReadOnlyList<WpfImageQueueItem>> QueueItemsProvider { get; init; }
        internal Func<IReadOnlyList<WpfImageQueueItem>> VisibleQueueItemsProvider { get; init; }
        internal Func<WpfImageQueueItem> SelectedQueueItemProvider { get; init; }
        internal Func<string, WpfImageQueueItem> FindQueueItem { get; init; }
        internal Func<string> ActiveImagePathProvider { get; init; }
        internal Func<bool> IsApplicationCloseApproved { get; init; }
        internal Func<bool> EnsureInferenceModeForDetection { get; init; }
        internal Func<string, bool, Task> RunInteractiveDetectionAsync { get; init; }
        internal Func<IReadOnlyList<WpfImageQueueItem>, string, WpfBatchDetectionPlan> ShowBatchDetectionPreflight { get; init; }
        internal Func<string, CancellationToken, Task<YoloWorkerSmokeTestResult>> RunWorkerDetectionAsync { get; init; }
        internal Func<CancellationToken, Task> YieldBatchDetectionResultFrameAsync { get; init; }
        internal Func<int, CancellationToken, Task<bool>> EnsurePythonModelClientReadyAsync { get; init; }
        internal Func<PythonCommunicationStatus> CommunicationStatusProvider { get; init; }
        internal Func<string> PythonWorkerLastErrorProvider { get; init; }
        internal Func<bool> IsAnomalyDatasetPurpose { get; init; }
        internal Action<string> AppendLog { get; init; }
        internal Action<WpfImageQueueItem, YoloImageReviewStatus> ApplyReviewStatusToItem { get; init; }
        internal Func<string, string, IReadOnlyList<YoloWorkerSmokeCandidate>, bool, bool> ApplyAnomalyClassificationToImage { get; init; }
        internal Action RefreshQueueView { get; init; }
        internal Action UpdateImageQueueStatusText { get; init; }
        internal Action<BatchDetectionControlState> ApplyBatchDetectionControls { get; init; }
        internal Action<string, bool> SetYoloCommandStatus { get; init; }
        internal Action<string, bool, bool> SetGlobalInferenceStatus { get; init; }
        internal Action<string> SetPythonStatus { get; init; }
        internal Action<string> SelectImageQueueItem { get; init; }
        internal Func<string, bool> TryLoadBatchImage { get; init; }
        internal Action<WpfImageQueueItem> UpdateSelectedQueueImageButton { get; init; }
        internal Action RefreshCandidateList { get; init; }
        internal Action RefreshObjectList { get; init; }
        internal Action RedrawReviewRois { get; init; }
        internal Action<int, bool> SetActiveImageDetectionStatus { get; init; }
        internal Action<string> AddCandidateReviewHistory { get; init; }
        internal Action ShowCandidateReviewWorkflowView { get; init; }
        internal Action ClearCandidateReviewHistory { get; init; }
        internal Action<WpfCanvasDisplayMode, bool, bool> ApplyCanvasDisplayMode { get; init; }
        internal Action<WpfDetectionOverlayPresentation> SetDetectionOverlay { get; init; }
        internal Func<double> CandidateConfidenceFilterProvider { get; init; }
    }
}
