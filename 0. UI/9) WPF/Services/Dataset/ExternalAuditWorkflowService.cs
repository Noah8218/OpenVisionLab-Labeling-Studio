using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    public sealed class ExternalEvaluationDataAuditWorkflowRequest
    {
        public IReadOnlyList<string> ReferenceDirectories { get; init; } = Array.Empty<string>();

        public string SelectedDirectory { get; init; } = string.Empty;
    }

    public sealed class ExternalEvaluationDataAuditWorkflowResult
    {
        public ExternalEvaluationDataAuditWorkflowResult(
            bool started,
            bool isCanceled,
            YoloExternalEvaluationDataAuditReport report,
            Exception error)
        {
            Started = started;
            IsCanceled = isCanceled;
            Report = report;
            Error = error;
        }

        public bool Started { get; }

        public bool IsCanceled { get; }

        public YoloExternalEvaluationDataAuditReport Report { get; }

        public Exception Error { get; }

        public bool Succeeded => Started && !IsCanceled && Error == null && Report != null;
    }

    public sealed class HistoricalSegmentationRemediationAuditWorkflowRequest
    {
        public LabelingProjectData Data { get; init; }

        public string SourceImagePath { get; init; } = string.Empty;

        public string OutputPath { get; init; } = string.Empty;
    }

    public sealed class HistoricalSegmentationRemediationAuditWorkflowResult
    {
        public HistoricalSegmentationRemediationAuditWorkflowResult(
            bool started,
            bool isCanceled,
            YoloSegmentationHistoricalRemediationAuditReport report,
            YoloSegmentationHistoricalRemediationAuditExportResult export,
            Exception error)
        {
            Started = started;
            IsCanceled = isCanceled;
            Report = report;
            Export = export;
            Error = error;
        }

        public bool Started { get; }

        public bool IsCanceled { get; }

        public YoloSegmentationHistoricalRemediationAuditReport Report { get; }

        public YoloSegmentationHistoricalRemediationAuditExportResult Export { get; }

        public Exception Error { get; }

        public bool Succeeded => Started && !IsCanceled && Error == null && Report != null && Export != null;
    }

    /// <summary>
    /// Owns external audit execution admission and cancellation lifetime.
    /// Folder selection, status projection, presentation, and close-policy text stay with the Shell adapter.
    /// </summary>
    public sealed class ExternalAuditWorkflowService : IDisposable
    {
        private readonly object syncRoot = new object();
        private readonly Func<IReadOnlyList<string>, string, CancellationToken, YoloExternalEvaluationDataAuditReport> buildExternalEvaluationAudit;
        private readonly Func<LabelingProjectData, string, CancellationToken, YoloSegmentationHistoricalRemediationAuditReport> buildHistoricalRemediationAudit;
        private readonly Func<YoloSegmentationHistoricalRemediationAuditReport, string, CancellationToken, YoloSegmentationHistoricalRemediationAuditExportResult> exportHistoricalRemediationAudit;
        private CancellationTokenSource externalEvaluationCancellation;
        private CancellationTokenSource historicalRemediationCancellation;
        private bool isClosed;
        private bool isDisposed;

        public ExternalAuditWorkflowService(
            Func<IReadOnlyList<string>, string, CancellationToken, YoloExternalEvaluationDataAuditReport> buildExternalEvaluationAudit = null,
            Func<LabelingProjectData, string, CancellationToken, YoloSegmentationHistoricalRemediationAuditReport> buildHistoricalRemediationAudit = null,
            Func<YoloSegmentationHistoricalRemediationAuditReport, string, CancellationToken, YoloSegmentationHistoricalRemediationAuditExportResult> exportHistoricalRemediationAudit = null)
        {
            this.buildExternalEvaluationAudit = buildExternalEvaluationAudit
                ?? ((directories, selectedDirectory, cancellationToken) =>
                    YoloExternalEvaluationDataAuditService.Build(directories, selectedDirectory, cancellationToken));
            this.buildHistoricalRemediationAudit = buildHistoricalRemediationAudit
                ?? ((data, sourceImagePath, cancellationToken) =>
                    YoloSegmentationHistoricalRemediationAuditService.Build(data, sourceImagePath, cancellationToken));
            this.exportHistoricalRemediationAudit = exportHistoricalRemediationAudit
                ?? ((report, outputPath, cancellationToken) =>
                    YoloSegmentationHistoricalRemediationAuditService.ExportMarkdown(report, outputPath, cancellationToken));
        }

        public bool IsExternalEvaluationDataAuditRunning { get; private set; }

        public bool IsHistoricalSegmentationRemediationAuditRunning { get; private set; }

        public bool IsRunning => IsExternalEvaluationDataAuditRunning || IsHistoricalSegmentationRemediationAuditRunning;

        public bool IsClosed => isClosed;

        public async Task<ExternalEvaluationDataAuditWorkflowResult> RunExternalEvaluationAsync(
            ExternalEvaluationDataAuditWorkflowRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            CancellationTokenSource currentCancellation;
            lock (syncRoot)
            {
                if (isDisposed || isClosed || IsExternalEvaluationDataAuditRunning)
                {
                    return new ExternalEvaluationDataAuditWorkflowResult(false, false, null, null);
                }

                IsExternalEvaluationDataAuditRunning = true;
                currentCancellation = new CancellationTokenSource();
                externalEvaluationCancellation = currentCancellation;
            }

            try
            {
                YoloExternalEvaluationDataAuditReport report = await Task.Run(
                    () => buildExternalEvaluationAudit(
                        request.ReferenceDirectories,
                        request.SelectedDirectory,
                        currentCancellation.Token),
                    currentCancellation.Token).ConfigureAwait(false);
                return new ExternalEvaluationDataAuditWorkflowResult(true, false, report, null);
            }
            catch (OperationCanceledException) when (currentCancellation.IsCancellationRequested)
            {
                return new ExternalEvaluationDataAuditWorkflowResult(true, true, null, null);
            }
            catch (Exception error)
            {
                return new ExternalEvaluationDataAuditWorkflowResult(true, false, null, error);
            }
            finally
            {
                CompleteExternalEvaluation(currentCancellation);
            }
        }

        public async Task<HistoricalSegmentationRemediationAuditWorkflowResult> RunHistoricalRemediationAsync(
            HistoricalSegmentationRemediationAuditWorkflowRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            CancellationTokenSource currentCancellation;
            lock (syncRoot)
            {
                if (isDisposed || isClosed || IsHistoricalSegmentationRemediationAuditRunning)
                {
                    return new HistoricalSegmentationRemediationAuditWorkflowResult(false, false, null, null, null);
                }

                IsHistoricalSegmentationRemediationAuditRunning = true;
                currentCancellation = new CancellationTokenSource();
                historicalRemediationCancellation = currentCancellation;
            }

            try
            {
                (YoloSegmentationHistoricalRemediationAuditReport Report, YoloSegmentationHistoricalRemediationAuditExportResult Export) result =
                    await Task.Run(
                        () =>
                        {
                            currentCancellation.Token.ThrowIfCancellationRequested();
                            YoloSegmentationHistoricalRemediationAuditReport report = buildHistoricalRemediationAudit(
                                request.Data,
                                request.SourceImagePath,
                                currentCancellation.Token);
                            currentCancellation.Token.ThrowIfCancellationRequested();
                            YoloSegmentationHistoricalRemediationAuditExportResult export = exportHistoricalRemediationAudit(
                                report,
                                request.OutputPath,
                                currentCancellation.Token);
                            return (report, export);
                        },
                        currentCancellation.Token).ConfigureAwait(false);
                return new HistoricalSegmentationRemediationAuditWorkflowResult(
                    true,
                    false,
                    result.Report,
                    result.Export,
                    null);
            }
            catch (OperationCanceledException) when (currentCancellation.IsCancellationRequested)
            {
                return new HistoricalSegmentationRemediationAuditWorkflowResult(true, true, null, null, null);
            }
            catch (Exception error)
            {
                return new HistoricalSegmentationRemediationAuditWorkflowResult(true, false, null, null, error);
            }
            finally
            {
                CompleteHistoricalRemediation(currentCancellation);
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
                externalEvaluationCancellation?.Cancel();
                historicalRemediationCancellation?.Cancel();
                IsExternalEvaluationDataAuditRunning = false;
                IsHistoricalSegmentationRemediationAuditRunning = false;
            }
        }

        private void CompleteExternalEvaluation(CancellationTokenSource currentCancellation)
        {
            lock (syncRoot)
            {
                if (ReferenceEquals(externalEvaluationCancellation, currentCancellation))
                {
                    externalEvaluationCancellation = null;
                    IsExternalEvaluationDataAuditRunning = false;
                }
            }

            currentCancellation.Dispose();
        }

        private void CompleteHistoricalRemediation(CancellationTokenSource currentCancellation)
        {
            lock (syncRoot)
            {
                if (ReferenceEquals(historicalRemediationCancellation, currentCancellation))
                {
                    historicalRemediationCancellation = null;
                    IsHistoricalSegmentationRemediationAuditRunning = false;
                }
            }

            currentCancellation.Dispose();
        }
    }
}
