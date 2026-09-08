using MvcVisionSystem.Yolo;
using MvcVisionSystem._1._Core;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    public sealed class SmartMaskWorkflowRequest
    {
        public MobileSamBoxPromptRequest Prompt { get; init; }
    }

    public sealed class SmartMaskWorkflowResult
    {
        public SmartMaskWorkflowResult(
            bool started,
            bool isCanceled,
            MobileSamBoxPromptResult result,
            Exception error)
        {
            Started = started;
            IsCanceled = isCanceled;
            Result = result;
            Error = error;
        }

        public bool Started { get; }

        public bool IsCanceled { get; }

        public MobileSamBoxPromptResult Result { get; }

        public Exception Error { get; }

        public bool Succeeded => Started && !IsCanceled && Error == null && Result != null;
    }

    /// <summary>
    /// Owns Smart Mask candidate execution admission and cancellation lifetime.
    /// Prompt editing, candidate review, annotation mutation, and WPF presentation stay with the Shell adapters.
    /// </summary>
    public sealed class SmartMaskWorkflowService : IDisposable
    {
        private readonly object syncRoot = new object();
        private readonly MobileSamBoxPromptService mobileSamBoxPromptService;
        private readonly Func<MobileSamBoxPromptRequest, CancellationToken, Task<MobileSamBoxPromptResult>> runPrompt;
        private CancellationTokenSource cancellation;
        private bool isClosed;
        private bool isDisposed;

        public SmartMaskWorkflowService(
            MobileSamBoxPromptService mobileSamBoxPromptService = null,
            Func<MobileSamBoxPromptRequest, CancellationToken, Task<MobileSamBoxPromptResult>> runPrompt = null)
        {
            this.mobileSamBoxPromptService = mobileSamBoxPromptService ?? new MobileSamBoxPromptService();
            this.runPrompt = runPrompt ?? ((request, cancellationToken) =>
                this.mobileSamBoxPromptService.RunAsync(request, cancellationToken));
        }

        public bool IsRunning { get; private set; }

        public bool IsClosed => isClosed;

        public MobileSamBoxPromptRequest BuildRequest(
            PythonModelSettings pythonModel,
            string imagePath,
            Rectangle promptBounds,
            int? classId,
            string className,
            IReadOnlyList<WpfSmartMaskPromptPoint> promptPoints,
            int maximumPolygonPoints)
            => mobileSamBoxPromptService.BuildRequest(
                pythonModel,
                imagePath,
                promptBounds,
                classId,
                className,
                promptPoints,
                maximumPolygonPoints);

        public async Task<SmartMaskWorkflowResult> RunAsync(SmartMaskWorkflowRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(request.Prompt);

            CancellationTokenSource currentCancellation;
            lock (syncRoot)
            {
                if (isDisposed || isClosed || IsRunning)
                {
                    return new SmartMaskWorkflowResult(false, false, null, null);
                }

                IsRunning = true;
                currentCancellation = new CancellationTokenSource();
                cancellation = currentCancellation;
            }

            try
            {
                MobileSamBoxPromptResult result = await runPrompt(
                    request.Prompt,
                    currentCancellation.Token).ConfigureAwait(false);
                return new SmartMaskWorkflowResult(true, false, result, null);
            }
            catch (OperationCanceledException) when (currentCancellation.IsCancellationRequested)
            {
                return new SmartMaskWorkflowResult(true, true, null, null);
            }
            catch (Exception error)
            {
                return new SmartMaskWorkflowResult(true, false, null, error);
            }
            finally
            {
                Complete(currentCancellation);
            }
        }

        public void Cancel()
        {
            lock (syncRoot)
            {
                cancellation?.Cancel();
            }
        }

        public void ApproveClose()
        {
            lock (syncRoot)
            {
                isClosed = true;
            }
        }

        public void Dispose()
        {
            lock (syncRoot)
            {
                if (isDisposed)
                {
                    return;
                }

                isDisposed = true;
                isClosed = true;
                cancellation?.Cancel();
                IsRunning = false;
            }
        }

        private void Complete(CancellationTokenSource currentCancellation)
        {
            lock (syncRoot)
            {
                if (ReferenceEquals(cancellation, currentCancellation))
                {
                    cancellation = null;
                    IsRunning = false;
                }
            }

            currentCancellation.Dispose();
        }
    }
}
