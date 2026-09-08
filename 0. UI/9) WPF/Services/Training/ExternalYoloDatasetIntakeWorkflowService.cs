using MvcVisionSystem.Yolo;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    public sealed class ExternalYoloDatasetIntakeWorkflowRequest
    {
        public string DataYamlFilePath { get; init; } = string.Empty;

        public LabelingDatasetPurpose Purpose { get; init; }
    }

    public sealed class ExternalYoloDatasetIntakeWorkflowResult
    {
        public ExternalYoloDatasetIntakeWorkflowResult(
            bool started,
            bool isCanceled,
            YoloExternalDatasetIntakeReport report,
            Exception error)
        {
            Started = started;
            IsCanceled = isCanceled;
            Report = report;
            Error = error;
        }

        public bool Started { get; }

        public bool IsCanceled { get; }

        public YoloExternalDatasetIntakeReport Report { get; }

        public Exception Error { get; }

        public bool Succeeded => Started && !IsCanceled && Error == null && Report != null;
    }

    /// <summary>
    /// Owns external data.yaml validation execution and its cancellation lifetime.
    /// Settings mutation, Recipe persistence and WPF presentation stay with the Shell adapter.
    /// </summary>
    public sealed class ExternalYoloDatasetIntakeWorkflowService : IDisposable
    {
        private readonly Func<string, LabelingDatasetPurpose, CancellationToken, YoloExternalDatasetIntakeReport> buildReport;
        private CancellationTokenSource cancellation;
        private bool isClosed;
        private bool isDisposed;

        public ExternalYoloDatasetIntakeWorkflowService(
            Func<string, LabelingDatasetPurpose, CancellationToken, YoloExternalDatasetIntakeReport> buildReport = null)
        {
            this.buildReport = buildReport ?? YoloExternalDatasetIntakeService.Build;
        }

        public bool IsRunning { get; private set; }

        public bool IsClosed => isClosed;

        public async Task<ExternalYoloDatasetIntakeWorkflowResult> RunAsync(
            ExternalYoloDatasetIntakeWorkflowRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (isDisposed || isClosed || IsRunning)
            {
                return new ExternalYoloDatasetIntakeWorkflowResult(
                    started: false,
                    isCanceled: false,
                    report: null,
                    error: null);
            }

            IsRunning = true;
            CancellationTokenSource currentCancellation = new CancellationTokenSource();
            cancellation = currentCancellation;
            try
            {
                YoloExternalDatasetIntakeReport report = await Task.Run(
                    () => buildReport(request.DataYamlFilePath, request.Purpose, currentCancellation.Token),
                    currentCancellation.Token).ConfigureAwait(false);
                return new ExternalYoloDatasetIntakeWorkflowResult(
                    started: true,
                    isCanceled: false,
                    report,
                    error: null);
            }
            catch (OperationCanceledException) when (currentCancellation.IsCancellationRequested)
            {
                return new ExternalYoloDatasetIntakeWorkflowResult(
                    started: true,
                    isCanceled: true,
                    report: null,
                    error: null);
            }
            catch (Exception error)
            {
                return new ExternalYoloDatasetIntakeWorkflowResult(
                    started: true,
                    isCanceled: false,
                    report: null,
                    error);
            }
            finally
            {
                if (ReferenceEquals(cancellation, currentCancellation))
                {
                    cancellation = null;
                }

                currentCancellation.Dispose();
                IsRunning = false;
            }
        }

        public void ApproveClose()
        {
            isClosed = true;
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            ApproveClose();
            cancellation?.Cancel();
            IsRunning = false;
        }
    }
}
