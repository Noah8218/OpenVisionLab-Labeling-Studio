using MvcVisionSystem._1._Core;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the anomaly-evaluation run and summary-state workflow. The Shell
    /// supplies data snapshots and projects typed results into WPF state.
    /// </summary>
    public sealed class AnomalyClassificationEvaluationWorkflowService
    {
        private readonly AnomalyClassificationEvaluationRunService runService;
        private readonly AnomalyClassificationEvaluationSummaryService summaryService;
        private CancellationTokenSource evaluationCancellation;
        private string preferredSummaryPath = string.Empty;
        private string activeOutputRootPath = string.Empty;

        public AnomalyClassificationEvaluationWorkflowService()
            : this(
                new AnomalyClassificationEvaluationRunService(),
                new AnomalyClassificationEvaluationSummaryService())
        {
        }

        public AnomalyClassificationEvaluationWorkflowService(
            AnomalyClassificationEvaluationRunService runService,
            AnomalyClassificationEvaluationSummaryService summaryService)
        {
            this.runService = runService ?? throw new ArgumentNullException(nameof(runService));
            this.summaryService = summaryService ?? throw new ArgumentNullException(nameof(summaryService));
        }

        public bool IsRunning { get; private set; }

        public string PreferredSummaryPath => preferredSummaryPath;

        public AnomalyClassificationEvaluationRefreshResult Refresh(LabelingProjectData data)
        {
            if (data?.ProjectSettings?.DatasetPurpose != LabelingDatasetPurpose.AnomalyDetection)
            {
                ClearSummarySelection();
                return AnomalyClassificationEvaluationRefreshResult.Hidden();
            }

            TrackOutputRoot(data.OutputRootPath);
            string summaryPath = summaryService.ResolveSummaryPath(activeOutputRootPath, preferredSummaryPath);
            if (!string.IsNullOrWhiteSpace(preferredSummaryPath)
                && !string.Equals(summaryPath, preferredSummaryPath.Trim(), StringComparison.Ordinal))
            {
                preferredSummaryPath = string.Empty;
            }

            if (string.IsNullOrWhiteSpace(summaryPath)
                || !summaryService.TryReadSummary(summaryPath, out AnomalyClassificationEvaluationSummary summary))
            {
                preferredSummaryPath = string.Empty;
                return AnomalyClassificationEvaluationRefreshResult.VisibleWithoutSummary();
            }

            return AnomalyClassificationEvaluationRefreshResult.WithSummary(
                summaryPath,
                summary,
                AnomalyClassificationEvaluationPresentationService.Build(summary.Report, summary.Options));
        }

        public AnomalyClassificationEvaluationSummaryLoadResult LoadSummary(
            LabelingProjectData data,
            string summaryPath)
        {
            if (data?.ProjectSettings?.DatasetPurpose != LabelingDatasetPurpose.AnomalyDetection)
            {
                ClearSummarySelection();
                return AnomalyClassificationEvaluationSummaryLoadResult.UnsupportedDataset();
            }

            TrackOutputRoot(data.OutputRootPath);
            string normalizedPath = summaryPath?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedPath)
                || !summaryService.TryReadSummary(normalizedPath, out AnomalyClassificationEvaluationSummary summary))
            {
                preferredSummaryPath = string.Empty;
                return AnomalyClassificationEvaluationSummaryLoadResult.Failed();
            }

            preferredSummaryPath = normalizedPath;
            return AnomalyClassificationEvaluationSummaryLoadResult.Success(
                normalizedPath,
                summary,
                AnomalyClassificationEvaluationPresentationService.Build(summary.Report, summary.Options));
        }

        public async Task<AnomalyClassificationEvaluationWorkflowRunResult> RunAsync(
            LabelingProjectData data,
            CancellationToken cancellationToken = default)
        {
            if (IsRunning)
            {
                return AnomalyClassificationEvaluationWorkflowRunResult.Failed(
                    null,
                    AnomalyClassificationEvaluationRunResult.Failed(
                        "Anomaly classification evaluation is already running.",
                        string.Empty,
                        string.Empty));
            }

            if (data?.ProjectSettings?.DatasetPurpose != LabelingDatasetPurpose.AnomalyDetection)
            {
                return AnomalyClassificationEvaluationWorkflowRunResult.Failed(
                    null,
                    AnomalyClassificationEvaluationRunResult.Failed(
                        "Anomaly classification evaluation requires an anomaly dataset.",
                        string.Empty,
                        string.Empty));
            }

            IsRunning = true;
            using CancellationTokenSource linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            evaluationCancellation = linkedCancellation;
            try
            {
                AnomalyClassificationEvaluationRunRequest request = runService.BuildRequest(data);
                IReadOnlyList<string> validationErrors = runService.ValidateRequest(request);
                if (validationErrors.Count > 0)
                {
                    return AnomalyClassificationEvaluationWorkflowRunResult.Failed(
                        request,
                        AnomalyClassificationEvaluationRunResult.Failed(
                            string.Join(Environment.NewLine, validationErrors),
                            string.Empty,
                            string.Empty),
                        validationErrors);
                }

                AnomalyClassificationEvaluationRunResult runResult = await runService
                    .RunAsync(request, linkedCancellation.Token)
                    .ConfigureAwait(false);
                if (!runResult.Succeeded)
                {
                    return AnomalyClassificationEvaluationWorkflowRunResult.Failed(request, runResult);
                }

                if (string.IsNullOrWhiteSpace(runResult.SummaryPath)
                    || !summaryService.TryReadSummary(runResult.SummaryPath, out AnomalyClassificationEvaluationSummary summary))
                {
                    return AnomalyClassificationEvaluationWorkflowRunResult.Failed(
                        request,
                        AnomalyClassificationEvaluationRunResult.Failed(
                            "Anomaly classification evaluation summary could not be read.",
                            runResult.Output,
                            runResult.Error));
                }

                TrackOutputRoot(data.OutputRootPath);
                preferredSummaryPath = runResult.SummaryPath;
                return AnomalyClassificationEvaluationWorkflowRunResult.Success(
                    request,
                    runResult,
                    summary,
                    AnomalyClassificationEvaluationPresentationService.Build(summary.Report, summary.Options));
            }
            finally
            {
                if (ReferenceEquals(evaluationCancellation, linkedCancellation))
                {
                    evaluationCancellation = null;
                }

                IsRunning = false;
            }
        }

        public void Cancel()
        {
            evaluationCancellation?.Cancel();
        }

        public void Reset()
        {
            Cancel();
            evaluationCancellation = null;
            IsRunning = false;
        }

        public void SetPreferredSummaryPath(LabelingProjectData data, string summaryPath)
        {
            if (data?.ProjectSettings?.DatasetPurpose != LabelingDatasetPurpose.AnomalyDetection)
            {
                ClearSummarySelection();
                return;
            }

            TrackOutputRoot(data.OutputRootPath);
            preferredSummaryPath = summaryPath?.Trim() ?? string.Empty;
        }

        private void TrackOutputRoot(string outputRootPath)
        {
            string normalizedRoot = outputRootPath?.Trim() ?? string.Empty;
            if (!string.Equals(activeOutputRootPath, normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                activeOutputRootPath = normalizedRoot;
                preferredSummaryPath = string.Empty;
            }
        }

        private void ClearSummarySelection()
        {
            preferredSummaryPath = string.Empty;
            activeOutputRootPath = string.Empty;
        }
    }

    public sealed class AnomalyClassificationEvaluationRefreshResult
    {
        private AnomalyClassificationEvaluationRefreshResult(
            bool isVisible,
            string summaryPath,
            AnomalyClassificationEvaluationSummary summary,
            WpfAnomalyClassificationEvaluationPresentation presentation)
        {
            IsVisible = isVisible;
            SummaryPath = summaryPath ?? string.Empty;
            Summary = summary;
            Presentation = presentation;
        }

        public bool IsVisible { get; }

        public string SummaryPath { get; }

        public AnomalyClassificationEvaluationSummary Summary { get; }

        public WpfAnomalyClassificationEvaluationPresentation Presentation { get; }

        public bool HasSummary => Summary != null && Presentation != null;

        public static AnomalyClassificationEvaluationRefreshResult Hidden()
            => new AnomalyClassificationEvaluationRefreshResult(false, string.Empty, null, null);

        public static AnomalyClassificationEvaluationRefreshResult VisibleWithoutSummary()
            => new AnomalyClassificationEvaluationRefreshResult(true, string.Empty, null, null);

        public static AnomalyClassificationEvaluationRefreshResult WithSummary(
            string summaryPath,
            AnomalyClassificationEvaluationSummary summary,
            WpfAnomalyClassificationEvaluationPresentation presentation)
            => new AnomalyClassificationEvaluationRefreshResult(true, summaryPath, summary, presentation);
    }

    public sealed class AnomalyClassificationEvaluationSummaryLoadResult
    {
        private AnomalyClassificationEvaluationSummaryLoadResult(
            bool isSupportedDataset,
            bool succeeded,
            string summaryPath,
            AnomalyClassificationEvaluationSummary summary,
            WpfAnomalyClassificationEvaluationPresentation presentation)
        {
            IsSupportedDataset = isSupportedDataset;
            Succeeded = succeeded;
            SummaryPath = summaryPath ?? string.Empty;
            Summary = summary;
            Presentation = presentation;
        }

        public bool IsSupportedDataset { get; }

        public bool Succeeded { get; }

        public string SummaryPath { get; }

        public AnomalyClassificationEvaluationSummary Summary { get; }

        public WpfAnomalyClassificationEvaluationPresentation Presentation { get; }

        public static AnomalyClassificationEvaluationSummaryLoadResult UnsupportedDataset()
            => new AnomalyClassificationEvaluationSummaryLoadResult(false, false, string.Empty, null, null);

        public static AnomalyClassificationEvaluationSummaryLoadResult Failed()
            => new AnomalyClassificationEvaluationSummaryLoadResult(true, false, string.Empty, null, null);

        public static AnomalyClassificationEvaluationSummaryLoadResult Success(
            string summaryPath,
            AnomalyClassificationEvaluationSummary summary,
            WpfAnomalyClassificationEvaluationPresentation presentation)
            => new AnomalyClassificationEvaluationSummaryLoadResult(true, true, summaryPath, summary, presentation);
    }

    public sealed class AnomalyClassificationEvaluationWorkflowRunResult
    {
        private AnomalyClassificationEvaluationWorkflowRunResult(
            bool succeeded,
            AnomalyClassificationEvaluationRunRequest request,
            AnomalyClassificationEvaluationRunResult runResult,
            AnomalyClassificationEvaluationSummary summary,
            WpfAnomalyClassificationEvaluationPresentation presentation,
            IReadOnlyList<string> validationErrors)
        {
            Succeeded = succeeded;
            Request = request;
            RunResult = runResult ?? throw new ArgumentNullException(nameof(runResult));
            Summary = summary;
            Presentation = presentation;
            ValidationErrors = validationErrors ?? Array.Empty<string>();
        }

        public bool Succeeded { get; }

        public AnomalyClassificationEvaluationRunRequest Request { get; }

        public AnomalyClassificationEvaluationRunResult RunResult { get; }

        public AnomalyClassificationEvaluationSummary Summary { get; }

        public WpfAnomalyClassificationEvaluationPresentation Presentation { get; }

        public IReadOnlyList<string> ValidationErrors { get; }

        public static AnomalyClassificationEvaluationWorkflowRunResult Failed(
            AnomalyClassificationEvaluationRunRequest request,
            AnomalyClassificationEvaluationRunResult runResult,
            IReadOnlyList<string> validationErrors = null)
            => new AnomalyClassificationEvaluationWorkflowRunResult(false, request, runResult, null, null, validationErrors);

        public static AnomalyClassificationEvaluationWorkflowRunResult Success(
            AnomalyClassificationEvaluationRunRequest request,
            AnomalyClassificationEvaluationRunResult runResult,
            AnomalyClassificationEvaluationSummary summary,
            WpfAnomalyClassificationEvaluationPresentation presentation)
            => new AnomalyClassificationEvaluationWorkflowRunResult(true, request, runResult, summary, presentation, Array.Empty<string>());
    }
}
