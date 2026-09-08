using MvcVisionSystem._1._Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    // One owner for AI and template batch admission/lifetime. The Shell projects
    // progress and results; it must not recreate tokens, counters or an AI loop.
    // Start/cancel/close and callbacks run on the caller's UI context. Worker work
    // may run elsewhere, but its continuation returns here before applying results.
    public sealed class BatchDetectionWorkflowService : IDisposable
    {
        private bool disposed;

        public BatchDetectionRun Current { get; private set; }
        public bool IsRunning => Current?.IsRunning == true;
        public int TotalCount => Current?.TotalCount ?? 0;
        public int CompletedCount => Current?.CompletedCount ?? 0;

        public BatchDetectionRun TryBegin(int totalCount, Action saveReviewStatus)
        {
            // Cancellation keeps the slot occupied until the awaiting caller exits.
            // A second batch cannot replace the context of a late completion.
            if (disposed || IsRunning) return null;
            Current = new BatchDetectionRun(totalCount, saveReviewStatus);
            return Current;
        }

        public async Task ExecuteAsync(BatchDetectionRun run, IReadOnlyList<string> imagePaths, BatchDetectionCallbacks callbacks)
        {
            if (!ReferenceEquals(Current, run) || run == null || !run.IsRunning) return;
            try
            {
                string preparationFailure = await callbacks.PrepareAsync(run.Token).ConfigureAwait(true);
                if (!run.CanApplyResult) return;
                if (!string.IsNullOrEmpty(preparationFailure))
                {
                    run.Fail(preparationFailure);
                    return;
                }

                foreach (string imagePath in imagePaths)
                {
                    if (!run.CanApplyResult) break;
                    callbacks.ItemStarting(imagePath);
                    if (!run.CanApplyResult) break;
                    var stopwatch = Stopwatch.StartNew();
                    YoloWorkerSmokeTestResult result = await callbacks.DetectAsync(imagePath, run.Token).ConfigureAwait(true);
                    if (!run.CanApplyResult) break;

                    TimeSpan elapsed = stopwatch.Elapsed;
                    result.ElapsedMilliseconds ??= YoloRuntimePresentationService.ClampElapsedMilliseconds(elapsed);
                    bool displayed = callbacks.ApplyResult(imagePath, result, elapsed);
                    if (!run.CanApplyResult) break;
                    run.RecordResult();
                    callbacks.ItemCompleted(imagePath, elapsed);
                    if (displayed)
                    {
                        await callbacks.YieldResultFrameAsync(run.Token).ConfigureAwait(true);
                    }
                }
            }
            catch (OperationCanceledException) when (run.Token.IsCancellationRequested)
            {
                // Complete retains cancellation and counts for the caller's final projection.
            }
            catch (Exception ex)
            {
                run.Fail(ex);
            }
            finally
            {
                run.Complete();
            }
        }

        public void Cancel() => Current?.Cancel();

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Current?.Close();
        }
    }

    // A run keeps the original save target alive until finalization. It never
    // reads a later global project or catalog. Completed/closed runs are inert.
    public sealed class BatchDetectionRun
    {
        private const int ReviewStatusSaveInterval = 10;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly CancellationTokenRegistration saveOnCancellation;
        private readonly Action saveReviewStatus;
        private readonly Stopwatch stopwatch = Stopwatch.StartNew();
        private int pendingReviewStatusSaves;
        private bool closed;

        internal BatchDetectionRun(int totalCount, Action saveReviewStatus)
        {
            TotalCount = Math.Max(0, totalCount);
            this.saveReviewStatus = saveReviewStatus;
            Token = cancellation.Token;
            // Catalog replacement cancels synchronously before reading its cache.
            // Flush applied results now; an awaiting worker may return much later.
            saveOnCancellation = Token.Register(FlushReviewStatus);
        }

        public CancellationToken Token { get; }
        public int TotalCount { get; }
        public int CompletedCount { get; private set; }
        public bool IsRunning { get; private set; } = true;
        public bool CanApplyResult => IsRunning && !Token.IsCancellationRequested;
        public TimeSpan Elapsed => stopwatch.Elapsed;
        public string FailureSummary { get; private set; } = string.Empty;
        public bool HadException { get; private set; }

        public void RecordResult()
        {
            if (!CanApplyResult) return;
            pendingReviewStatusSaves++;
            if (pendingReviewStatusSaves >= ReviewStatusSaveInterval) FlushReviewStatus();
            CompletedCount++;
        }

        public void FlushReviewStatus()
        {
            if (!IsRunning || closed || pendingReviewStatusSaves == 0) return;
            saveReviewStatus?.Invoke();
            pendingReviewStatusSaves = 0;
        }

        internal void Fail(string message) => FailureSummary = message;

        internal void Fail(Exception exception)
        {
            HadException = true;
            FailureSummary = string.IsNullOrWhiteSpace(exception.Message)
                ? "일괄 검사 중 알 수 없는 오류가 발생했습니다."
                : exception.Message.Trim();
        }

        public void Cancel()
        {
            if (IsRunning) cancellation.Cancel();
        }

        public void Complete()
        {
            if (!IsRunning) return;
            try
            {
                FlushReviewStatus();
            }
            catch (Exception ex)
            {
                Fail(ex);
            }
            finally
            {
                IsRunning = false;
                stopwatch.Stop();
                saveOnCancellation.Dispose();
                cancellation.Dispose();
            }
        }

        internal void Close()
        {
            if (!IsRunning) return;
            closed = true;
            Cancel();
            Complete();
        }
    }

    // Concrete adapter points used by the AI workflow. None carries a Window,
    // queue row or visual tree into the execution owner.
    public sealed class BatchDetectionCallbacks
    {
        public Func<CancellationToken, Task<string>> PrepareAsync { get; init; }
        public Func<string, CancellationToken, Task<YoloWorkerSmokeTestResult>> DetectAsync { get; init; }
        public Action<string> ItemStarting { get; init; }
        public Func<string, YoloWorkerSmokeTestResult, TimeSpan, bool> ApplyResult { get; init; }
        public Action<string, TimeSpan> ItemCompleted { get; init; }
        public Func<CancellationToken, Task> YieldResultFrameAsync { get; init; }
    }
}
