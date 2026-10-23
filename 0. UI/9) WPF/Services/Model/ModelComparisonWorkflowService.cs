using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    // PL-0038: one owner for comparison command/lifetime coordination. Existing run,
    // review and presentation services remain authoritative; do not split them again
    // without the concrete evidence required by STABLE_VERIFIED_AREAS.
    // Call on the UI context. Composition is lazy; constructing this owner starts no work.
    public sealed class ModelComparisonWorkflowService : IDisposable
    {
        private readonly Func<ModelComparisonRunRequest> buildCandidateRequest;
        private readonly Func<bool, ModelComparisonRunRequest> buildEngineRequest;
        private readonly Func<SegmentationAdapterComparisonRunRequest> buildSegmentationRequest;
        private readonly Func<ModelComparisonRunRequest, IReadOnlyList<string>> validateModelRequest;
        private readonly Func<SegmentationAdapterComparisonRunRequest, IReadOnlyList<string>> validateSegmentationRequest;
        private readonly Func<ModelComparisonRunRequest, CancellationToken, Task<ModelComparisonRunResult>> runModelAsync;
        private readonly Func<SegmentationAdapterComparisonRunRequest, CancellationToken, Task<SegmentationAdapterComparisonRunResult>> runSegmentationAsync;
        private readonly Func<ModelComparisonRunResult, ModelComparisonRunRequest, WpfModelComparisonReviewReport> buildEngineReview;
        private CancellationTokenSource modelCancellation;
        private CancellationTokenSource segmentationCancellation;
        private bool isClosed;
        private bool disposed;

        public ModelComparisonWorkflowService(
            Func<ModelComparisonRunRequest> buildCandidateRequest,
            Func<bool, ModelComparisonRunRequest> buildEngineRequest,
            Func<SegmentationAdapterComparisonRunRequest> buildSegmentationRequest,
            Func<ModelComparisonRunRequest, IReadOnlyList<string>> validateModelRequest,
            Func<SegmentationAdapterComparisonRunRequest, IReadOnlyList<string>> validateSegmentationRequest,
            Func<ModelComparisonRunRequest, CancellationToken, Task<ModelComparisonRunResult>> runModelAsync,
            Func<SegmentationAdapterComparisonRunRequest, CancellationToken, Task<SegmentationAdapterComparisonRunResult>> runSegmentationAsync,
            Func<ModelComparisonRunResult, ModelComparisonRunRequest, WpfModelComparisonReviewReport> buildEngineReview)
        {
            this.buildCandidateRequest = buildCandidateRequest ?? throw new ArgumentNullException(nameof(buildCandidateRequest));
            this.buildEngineRequest = buildEngineRequest ?? throw new ArgumentNullException(nameof(buildEngineRequest));
            this.buildSegmentationRequest = buildSegmentationRequest ?? throw new ArgumentNullException(nameof(buildSegmentationRequest));
            this.validateModelRequest = validateModelRequest ?? throw new ArgumentNullException(nameof(validateModelRequest));
            this.validateSegmentationRequest = validateSegmentationRequest ?? throw new ArgumentNullException(nameof(validateSegmentationRequest));
            this.runModelAsync = runModelAsync ?? throw new ArgumentNullException(nameof(runModelAsync));
            this.runSegmentationAsync = runSegmentationAsync ?? throw new ArgumentNullException(nameof(runSegmentationAsync));
            this.buildEngineReview = buildEngineReview ?? throw new ArgumentNullException(nameof(buildEngineReview));
        }

        // Candidate and engine share admission; segmentation keeps its separate contract.
        public bool IsModelComparisonRunning { get; private set; }
        public bool IsSegmentationComparisonRunning { get; private set; }

        public async Task RunDecisionEvaluationAsync(Func<CancellationToken, Task<ModelDecisionEvaluationCache>> evaluate,
            Func<bool> isCurrent, Action<ModelDecisionEvaluationCache> apply, Action<string> failed, Action refreshCommands)
        {
            if (isClosed || IsModelComparisonRunning || IsSegmentationComparisonRunning) return;
            ArgumentNullException.ThrowIfNull(evaluate); ArgumentNullException.ThrowIfNull(isCurrent); ArgumentNullException.ThrowIfNull(apply);
            IsModelComparisonRunning = true;
            var cancellation = new CancellationTokenSource();
            modelCancellation = cancellation;
            refreshCommands?.Invoke();
            try
            {
                ModelDecisionEvaluationCache cache = await evaluate(cancellation.Token).ConfigureAwait(true);
                if (!isClosed && !cancellation.IsCancellationRequested && isCurrent()) apply(cache);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            catch (Exception ex) { if (!isClosed && isCurrent()) failed?.Invoke(ex.Message); }
            finally
            {
                if (ReferenceEquals(modelCancellation, cancellation)) modelCancellation = null;
                cancellation.Dispose(); IsModelComparisonRunning = false;
                if (!isClosed) refreshCommands?.Invoke();
            }
        }

        public async Task RunCandidateAsync(ModelComparisonCallbacks view)
        {
            if (isClosed || IsModelComparisonRunning)
            {
                return;
            }

            view.Prepare(true);

            ModelComparisonRunRequest request = buildCandidateRequest();
            IReadOnlyList<string> validationErrors = validateModelRequest(request);
            if (validationErrors.Count > 0)
            {
                string message = OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.Unavailable") + " " + string.Join(" / ", validationErrors.Take(3));
                view.SetComparisonTexts(
                    comparisonText: message,
                    adoptionDecisionText: OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.BlockedDecision"));
                view.SetCommandStatus(message, false);
                view.AppendLog(message);
                return;
            }

            IsModelComparisonRunning = true;
            CancellationTokenSource cancellation = new CancellationTokenSource();
            modelCancellation = cancellation;
            CancellationToken modelComparisonToken = cancellation.Token;
            view.RefreshCommands();
            view.SetComparisonTexts(
                comparisonText: OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.RunningHelp"),
                adoptionDecisionText: OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.RunningDecision"));
            view.SetCommandStatus(OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.Running"), true);
            view.AppendLog(string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.StartLog"), Path.GetFileName(request.BaselineWeightsPath), Path.GetFileName(request.CandidateWeightsPath), request.Task));

            try
            {
                ModelComparisonRunResult result = await runModelAsync(request, modelComparisonToken)
                    .ConfigureAwait(true);
                if (isClosed)
                {
                    return;
                }

                if (!result.Succeeded)
                {
                    string errorText = BuildModelComparisonFailureText(result);
                    view.SetComparisonTexts(
                        comparisonText: errorText,
                        adoptionDecisionText: OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.FailedDecision"));
                    view.SetCommandStatus(errorText, false);
                    view.AppendLog(errorText);
                    return;
                }

                WpfModelComparisonReviewReport reviewReport = buildEngineReview(result, request);
                if (!reviewReport.HasComparison)
                {
                    string errorText = string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.ReadFailure"), Path.GetFileName(request.BaselineWeightsPath), Path.GetFileName(request.CandidateWeightsPath));
                    view.SetComparisonTexts(
                        comparisonText: errorText,
                        adoptionDecisionText: OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.ReadFailedDecision"));
                    view.SetCommandStatus(errorText, false);
                    view.AppendLog(errorText);
                    return;
                }

                if (view.ApplyCandidateComparisonResult != null)
                {
                    view.ApplyCandidateComparisonResult(result, request, reviewReport);
                }
                else
                {
                    view.RefreshCandidateComparison();
                }

                string summaryName = string.IsNullOrWhiteSpace(result.SummaryPath)
                    ? "comparison-summary.json"
                    : Path.GetFileName(Path.GetDirectoryName(result.SummaryPath) ?? result.SummaryPath);
                string completeText = string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.Completed"), summaryName);
                view.SetComparisonTexts(comparisonText: completeText);
                if (!view.AdoptionDecisionText().Contains(OpenVisionLab.OpenVisionLanguageService.GetPreviousKoreanDefaults("WpfLearningWorkflow.TrainingComparison.Term.InspectionDecisionPrefix")[0], StringComparison.Ordinal)
                    && !view.AdoptionDecisionText().Contains(OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.DecisionPrefix"), StringComparison.Ordinal)
                    && !view.AdoptionDecisionText().Contains("Adoption decision:", StringComparison.OrdinalIgnoreCase))
                {
                    view.SetComparisonTexts(
                        adoptionDecisionText: OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.ExamplesNeeded"));
                }

                view.AddReviewHistory(completeText);
                view.ShowReview();
                view.SetCommandStatus(completeText, false);
                view.AppendLog(completeText);
            }
            catch (Exception ex)
            {
                if (!isClosed)
                {
                    string errorText = string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.Failed"), ex.Message);
                    view.SetComparisonTexts(
                        comparisonText: errorText,
                        adoptionDecisionText: OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.FailedDecision"));
                    view.SetCommandStatus(errorText, false);
                    view.AppendLog(errorText);
                }
            }
            finally
            {
                if (ReferenceEquals(modelCancellation, cancellation)) modelCancellation = null;
                cancellation.Dispose();
                IsModelComparisonRunning = false;
                if (!isClosed)
                {
                    view.RefreshCommands();
                }
            }
        }

        public async Task RunEngineAsync(ModelComparisonCallbacks view)
        {
            if (isClosed || IsModelComparisonRunning)
            {
                return;
            }

            view.Prepare(true);
            if (string.Equals(
                    PythonModelSettings.NormalizeModelEngine(view.ModelEngine()),
                    PythonModelSettings.EngineUnet,
                    StringComparison.Ordinal))
            {
                await RunSegmentationAsync(view).ConfigureAwait(true);
                return;
            }
            if (view.DatasetPurpose() != LabelingDatasetPurpose.ObjectDetection)
            {
                string purposeError = OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.PurposeError");
                view.SetComparisonTexts(comparisonText: purposeError);
                view.SetCommandStatus(purposeError, false);
                view.AppendLog(purposeError);
                return;
            }

            bool compareYoloV8ToYolo11 = string.Equals(
                PythonModelSettings.NormalizeModelEngine(view.ModelEngine()),
                PythonModelSettings.EngineYolo11,
                StringComparison.Ordinal);
            ModelComparisonRunRequest request = buildEngineRequest(compareYoloV8ToYolo11);
            string enginePair = BuildEnginePairLabel(request);
            string comparisonBasisText = string.Equals(request.Task, "val", StringComparison.OrdinalIgnoreCase)
                ? OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.ValidationBasis")
                : OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.TestBasis");
            IReadOnlyList<string> validationErrors = validateModelRequest(request);
            if (validationErrors.Count > 0)
            {
                string message = enginePair + " " + OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.AnalysisUnavailable") + " " + string.Join(" / ", validationErrors.Take(4));
                view.SetComparisonTexts(
                    comparisonText: message,
                    adoptionDecisionText: OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.EngineReady"));
                view.SetCommandStatus(message, false);
                view.AppendLog(message);
                return;
            }

            IsModelComparisonRunning = true;
            CancellationTokenSource cancellation = new CancellationTokenSource();
            modelCancellation = cancellation;
            CancellationToken modelComparisonToken = cancellation.Token;
            view.RefreshCommands();
            view.SetComparisonTexts(
                summaryText: enginePair + " " + OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.EngineAnalyzing"),
                comparisonText: string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.Timing"), comparisonBasisText),
                adoptionDecisionText: OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.EngineRunning"));
            view.SetCommandStatus(enginePair + " " + OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.EngineAnalyzingStatus"), true);
            view.AppendLog(string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.EngineStartLog"), enginePair, Path.GetFileName(request.BaselineWeightsPath), Path.GetFileName(request.CandidateWeightsPath), request.Task));

            try
            {
                ModelComparisonRunResult result = await runModelAsync(request, modelComparisonToken)
                    .ConfigureAwait(true);
                if (isClosed)
                {
                    return;
                }
                if (!result.Succeeded)
                {
                    string errorText = BuildModelComparisonFailureText(result);
                    view.SetComparisonTexts(
                        comparisonText: errorText,
                        adoptionDecisionText: OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.EngineFailed"));
                    view.SetCommandStatus(errorText, false);
                    view.AppendLog(errorText);
                    return;
                }

                WpfModelComparisonReviewReport report = buildEngineReview(result, request);
                if (!report.HasComparison)
                {
                    string errorText = enginePair + " " + OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.EngineReadFailed");
                    view.SetComparisonTexts(
                        comparisonText: errorText,
                        adoptionDecisionText: OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.EngineResult"));
                    view.SetCommandStatus(errorText, false);
                    view.AppendLog(errorText);
                    return;
                }

                string dataYamlName = Path.GetFileName(request.DataYamlPath);
                string sourceText = string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.SourceSummary"), enginePair, Path.GetFileName(request.BaselineWeightsPath), Path.GetFileName(request.CandidateWeightsPath), dataYamlName, comparisonBasisText);
                WpfModelComparisonHistoryItem historyItem = view.RefreshHistory(
                    request.BaselineWeightsPath,
                    request.CandidateWeightsPath,
                    result.SummaryPath);
                view.SetComparisonSource(sourceText);
                view.ApplyReview(
                    report,
                    historyItem?.IsLatest == false);
                view.ClearCandidateDecision();
                view.SetComparisonTexts(
                    summaryText: enginePair + " " + OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.EngineAnalysisCompleted"),
                    comparisonText: string.IsNullOrWhiteSpace(report.BenchmarkText)
                        ? report.DetailText
                        : report.BenchmarkText + Environment.NewLine + report.DetailText,
                    adoptionDecisionText: string.IsNullOrWhiteSpace(report.RecommendationText)
                        ? OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.EngineExamples")
                        : report.RecommendationText);

                string completeText = string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.EngineCompleted"), enginePair, comparisonBasisText);
                view.AddReviewHistory(completeText);
                view.ShowReview();
                view.SetCommandStatus(completeText, false);
                view.AppendLog(completeText);
            }
            catch (Exception ex)
            {
                if (!isClosed)
                {
                    string errorText = string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.EngineFailure"), enginePair, ex.Message);
                    view.SetComparisonTexts(
                        comparisonText: errorText,
                        adoptionDecisionText: OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.EngineFailed"));
                    view.SetCommandStatus(errorText, false);
                    view.AppendLog(errorText);
                }
            }
            finally
            {
                if (ReferenceEquals(modelCancellation, cancellation)) modelCancellation = null;
                cancellation.Dispose();
                IsModelComparisonRunning = false;
                if (!isClosed)
                {
                    view.RefreshCommands();
                }
            }
        }

        public async Task RunSegmentationAsync(ModelComparisonCallbacks view)
        {
            if (isClosed
                || IsSegmentationComparisonRunning
                || !view.HasSegmentationSettings())
            {
                return;
            }

            view.Prepare(false);
            SegmentationAdapterComparisonRunRequest request = buildSegmentationRequest();
            IReadOnlyList<string> validationErrors = validateSegmentationRequest(request);
            if (validationErrors.Count > 0)
            {
                string message = OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.MaskUnavailable") + " " + string.Join(" / ", validationErrors.Take(3));
                view.SetSegmentationState(
                    isRunning: false,
                    statusText: OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.Preparation"),
                    detailText: message,
                    actionText: OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.CheckpointHelp"));
                view.SetCommandStatus(message, false);
                view.AppendLog(message);
                return;
            }

            IsSegmentationComparisonRunning = true;
            CancellationTokenSource cancellation = new CancellationTokenSource();
            segmentationCancellation = cancellation;
            CancellationToken cancellationToken = cancellation.Token;
            view.SetSegmentationState(
                isRunning: true,
                statusText: OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.MaskRunning"),
                detailText: OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.MaskRunningDetail"),
                actionText: OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.MaskNext"));
            view.RefreshCommands();
            view.SetCommandStatus(OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.MaskStatus"), true);
            view.AppendLog($"Segmentation adapter comparison started: unet={Path.GetFileName(request.UnetSettings.WeightsPath)}, yolo={Path.GetFileName(request.YoloSettings.WeightsPath)}, engine={request.YoloEngine}");

            try
            {
                SegmentationAdapterComparisonRunResult result = await runSegmentationAsync(request, cancellationToken)
                    .ConfigureAwait(true);
                if (isClosed || cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (!result.Succeeded)
                {
                    string errorText = OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.MaskFailed") + " " + FirstLine(result.Error);
                    view.SetSegmentationState(
                        isRunning: false,
                        statusText: OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.ResultNeeded"),
                        detailText: errorText,
                        actionText: OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.MaskRecover"));
                    view.SetCommandStatus(errorText, false);
                    view.AppendLog(errorText);
                    return;
                }

                SegmentationMaskComparisonResult comparison = result.Comparison;
                string resultText = string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.MaskMetricSummary"), comparison.Baseline.ImageCount, comparison.Baseline.MeanDice, request.YoloEngine, comparison.Candidate.MeanDice, comparison.Baseline.MeanIoU, comparison.Candidate.MeanIoU);
                string reportPath = comparison.ReportPath ?? string.Empty;
                string completeText = OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.MaskCompleted");
                view.SetSegmentationState(
                    isRunning: false,
                    statusText: completeText,
                    detailText: resultText,
                    actionText: OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.Artifact") + " " + reportPath + " " + OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.MaskResultNext"));
                view.SetCommandStatus(completeText, false);
                view.AppendLog(completeText + ": " + reportPath);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                // Match the close contract for late non-cancellation failures as well.
                if (isClosed) return;

                string errorText = OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.MaskFailed") + " " + FirstLine(ex.Message);
                view.SetSegmentationState(
                    isRunning: false,
                    statusText: OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.ResultNeeded"),
                    detailText: errorText,
                    actionText: OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.MaskLogRecover"));
                view.SetCommandStatus(errorText, false);
                view.AppendLog(errorText);
            }
            finally
            {
                if (ReferenceEquals(segmentationCancellation, cancellation))
                {
                    segmentationCancellation = null;
                }

                cancellation.Dispose();
                IsSegmentationComparisonRunning = false;
                if (!isClosed)
                {
                    view.RefreshCommands();
                }
            }
        }

        private static string BuildEnginePairLabel(ModelComparisonRunRequest request)
        {
            return PythonModelSettings.FormatModelEngineName(request?.BaselineModelEngine)
                + " vs "
                + PythonModelSettings.FormatModelEngineName(request?.CandidateModelEngine);
        }

        private static string BuildModelComparisonFailureText(ModelComparisonRunResult result)
        {
            string detail = result?.Error ?? string.Empty;
            if (string.IsNullOrWhiteSpace(detail))
            {
                detail = result?.Output ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(detail))
            {
                return OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.ResultMissing");
            }

            string firstLine = detail
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault() ?? detail.Trim();
            return string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.FailureLine"), firstLine);
        }

        private static string FirstLine(string value)
        {
            return (value ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()?.Trim() ?? OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowService.NoDetails");
        }

        // Close approval suppresses work/results; process cancellation stays at cleanup.
        public void ApproveClose()
        {
            isClosed = true;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            ApproveClose();
            modelCancellation?.Cancel();
            modelCancellation?.Dispose();
            modelCancellation = null;
            segmentationCancellation?.Cancel();
            segmentationCancellation?.Dispose();
            segmentationCancellation = null;
            IsModelComparisonRunning = false;
            IsSegmentationComparisonRunning = false;
        }
    }

    // Data and display adapters only: no Window or concrete control crosses this boundary.
    public sealed class ModelComparisonCallbacks
    {
        public Action<bool> Prepare { get; init; }
        public Func<string> ModelEngine { get; init; }
        public Func<LabelingDatasetPurpose> DatasetPurpose { get; init; }
        public Func<bool> HasSegmentationSettings { get; init; }
        public Action RefreshCommands { get; init; }
        public Action<string, string, string> SetComparisonResult { get; init; }
        public Func<string> AdoptionDecisionText { get; init; }
        public Action RefreshCandidateComparison { get; init; }
        public Action<ModelComparisonRunResult, ModelComparisonRunRequest, WpfModelComparisonReviewReport> ApplyCandidateComparisonResult { get; init; }
        public Func<string, string, string, WpfModelComparisonHistoryItem> RefreshHistory { get; init; }
        public Action<string> SetComparisonSource { get; init; }
        public Action<WpfModelComparisonReviewReport, bool> ApplyReview { get; init; }
        public Action ClearCandidateDecision { get; init; }
        public Action<string> AddReviewHistory { get; init; }
        public Action ShowReview { get; init; }
        public Action<string, bool> SetCommandStatus { get; init; }
        public Action<string> AppendLog { get; init; }
        public Action<bool, string, string, string> SetSegmentationResult { get; init; }

        public void SetComparisonTexts(string summaryText = null, string comparisonText = null, string adoptionDecisionText = null)
        {
            SetComparisonResult(summaryText, comparisonText, adoptionDecisionText);
        }

        public void SetSegmentationState(bool isRunning, string statusText, string detailText, string actionText)
        {
            SetSegmentationResult(isRunning, statusText, detailText, actionText);
        }
    }
}
