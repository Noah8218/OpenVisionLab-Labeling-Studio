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
                string message = "\uBAA8\uB378 \uBE44\uAD50 \uC2E4\uD589 \uBD88\uAC00: " + string.Join(" / ", validationErrors.Take(3));
                view.SetComparisonTexts(
                    comparisonText: message,
                    adoptionDecisionText: "\uAD50\uCCB4 \uD310\uB2E8: \uBCF4\uB958 - \uCD5C\uC885 \uAC80\uC99D \uBE44\uAD50 \uBD88\uAC00");
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
                comparisonText: "\uBAA8\uB378 \uBE44\uAD50 \uC2E4\uD589 \uC911: \uCD5C\uC885 \uAC80\uC99D \uC774\uBBF8\uC9C0\uB85C \uAE30\uC874 \uBAA8\uB378\uACFC \uC0C8 \uD559\uC2B5 \uBAA8\uB378\uC744 \uBE44\uAD50\uD569\uB2C8\uB2E4.",
                adoptionDecisionText: "\uAD50\uCCB4 \uD310\uB2E8: \uBE44\uAD50 \uC2E4\uD589 \uC911");
            view.SetCommandStatus("\uBAA8\uB378 \uBE44\uAD50 \uC2E4\uD589 \uC911...", true);
            view.AppendLog($"\uBAA8\uB378 \uBE44\uAD50 \uC2DC\uC791: \uAE30\uC874={Path.GetFileName(request.BaselineWeightsPath)}, \uC0C8 \uBAA8\uB378={Path.GetFileName(request.CandidateWeightsPath)}, \uB300\uC0C1={request.Task}");

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
                        adoptionDecisionText: "\uAD50\uCCB4 \uD310\uB2E8: \uBCF4\uB958 - \uBAA8\uB378 \uBE44\uAD50 \uC2E4\uD328");
                    view.SetCommandStatus(errorText, false);
                    view.AppendLog(errorText);
                    return;
                }

                view.RefreshCandidateComparison();

                string summaryName = string.IsNullOrWhiteSpace(result.SummaryPath)
                    ? "comparison-summary.json"
                    : Path.GetFileName(Path.GetDirectoryName(result.SummaryPath) ?? result.SummaryPath);
                string completeText = $"\uBAA8\uB378 \uBE44\uAD50 \uC644\uB8CC: {summaryName}. Candidate Review\uC758 \uBAA8\uB378 \uCC28\uC774 \uC608\uC2DC\uB97C \uD074\uB9AD\uD574 \uC774\uBBF8\uC9C0 \uC704\uCE58\uB97C \uD655\uC778\uD558\uC138\uC694.";
                view.SetComparisonTexts(comparisonText: completeText);
                if (!view.AdoptionDecisionText().Contains("\uAD50\uCCB4 \uD310\uB2E8:", StringComparison.Ordinal)
                    && !view.AdoptionDecisionText().Contains("Adoption decision:", StringComparison.OrdinalIgnoreCase))
                {
                    view.SetComparisonTexts(
                        adoptionDecisionText: "\uAD50\uCCB4 \uD310\uB2E8: \uCC28\uC774 \uC608\uC2DC \uD655\uC778 \uD544\uC694");
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
                    string errorText = $"\uBAA8\uB378 \uBE44\uAD50 \uC2E4\uD328: {ex.Message}";
                    view.SetComparisonTexts(
                        comparisonText: errorText,
                        adoptionDecisionText: "\uAD50\uCCB4 \uD310\uB2E8: \uBCF4\uB958 - \uBAA8\uB378 \uBE44\uAD50 \uC2E4\uD328");
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
                const string purposeError = "YOLO 엔진 비교는 객체탐지 데이터셋에서만 실행할 수 있습니다.";
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
                ? "학습 검증(val, 교체 판단 아님)"
                : "최종 검증(test)";
            IReadOnlyList<string> validationErrors = validateModelRequest(request);
            if (validationErrors.Count > 0)
            {
                string message = enginePair + " 분석 실행 불가: " + string.Join(" / ", validationErrors.Take(4));
                view.SetComparisonTexts(
                    comparisonText: message,
                    adoptionDecisionText: "엔진 비교: 준비 필요");
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
                summaryText: enginePair + " 객체탐지 분석 중",
                comparisonText: $"동일한 {comparisonBasisText} 이미지에서 정확도와 모델 Takt를 측정하는 중입니다.",
                adoptionDecisionText: "엔진 비교: 실행 중");
            view.SetCommandStatus(enginePair + " 객체탐지 분석 중...", true);
            view.AppendLog($"YOLO 엔진 비교 시작: {enginePair}; baseline={Path.GetFileName(request.BaselineWeightsPath)}, candidate={Path.GetFileName(request.CandidateWeightsPath)}, batch=1, task={request.Task}");

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
                        adoptionDecisionText: "엔진 비교: 실패");
                    view.SetCommandStatus(errorText, false);
                    view.AppendLog(errorText);
                    return;
                }

                WpfModelComparisonReviewReport report = buildEngineReview(result, request);
                if (!report.HasComparison)
                {
                    string errorText = enginePair + " 분석 결과를 읽지 못했습니다.";
                    view.SetComparisonTexts(
                        comparisonText: errorText,
                        adoptionDecisionText: "엔진 비교: 결과 확인 필요");
                    view.SetCommandStatus(errorText, false);
                    view.AppendLog(errorText);
                    return;
                }

                string dataYamlName = Path.GetFileName(request.DataYamlPath);
                string sourceText = $"비교 대상: {enginePair}; baseline={Path.GetFileName(request.BaselineWeightsPath)}, candidate={Path.GetFileName(request.CandidateWeightsPath)} / 데이터: {dataYamlName} / 기준: {comparisonBasisText}";
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
                    summaryText: enginePair + " 객체탐지 분석 완료",
                    comparisonText: string.IsNullOrWhiteSpace(report.BenchmarkText)
                        ? report.DetailText
                        : report.BenchmarkText + Environment.NewLine + report.DetailText,
                    adoptionDecisionText: string.IsNullOrWhiteSpace(report.RecommendationText)
                        ? "엔진 비교: 예시 확인 필요"
                        : report.RecommendationText);

                string completeText = $"{enginePair} 객체탐지 분석 완료 ({comparisonBasisText}): Candidate Review에서 정확도, 모델 Takt, 이미지별 차이를 확인하세요.";
                view.AddReviewHistory(completeText);
                view.ShowReview();
                view.SetCommandStatus(completeText, false);
                view.AppendLog(completeText);
            }
            catch (Exception ex)
            {
                if (!isClosed)
                {
                    string errorText = $"{enginePair} 분석 실패: {ex.Message}";
                    view.SetComparisonTexts(
                        comparisonText: errorText,
                        adoptionDecisionText: "엔진 비교: 실패");
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
                string message = "U-Net vs YOLO-seg 비교 실행 불가: " + string.Join(" / ", validationErrors.Take(3));
                view.SetSegmentationState(
                    isRunning: false,
                    statusText: "준비 필요",
                    detailText: message,
                    actionText: "checkpoint와 각 실행기의 Python 경로를 확인한 뒤 다시 실행하세요.");
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
                statusText: "공통 마스크 비교 실행 중",
                detailText: "recipe 원본은 읽기 전용으로 유지합니다. canonical export와 두 raw prediction artifact를 생성하고 있습니다.",
                actionText: "완료 후 macro Dice/IoU와 결함 component TP/FP/FN을 확인하세요. 검사 모델 자동 교체는 하지 않습니다.");
            view.RefreshCommands();
            view.SetCommandStatus("U-Net vs YOLO-seg 공통 마스크 비교 실행 중...", true);
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
                    string errorText = "U-Net vs YOLO-seg 비교 실패: " + FirstLine(result.Error);
                    view.SetSegmentationState(
                        isRunning: false,
                        statusText: "결과 확인 필요",
                        detailText: errorText,
                        actionText: "원본 레시피와 현재 검사 모델은 변경되지 않았습니다. 실행기, checkpoint, canonical export 조건을 확인하세요.");
                    view.SetCommandStatus(errorText, false);
                    view.AppendLog(errorText);
                    return;
                }

                SegmentationMaskComparisonResult comparison = result.Comparison;
                string resultText = $"test {comparison.Baseline.ImageCount}장 / macro Dice U-Net {comparison.Baseline.MeanDice:0.000} · {request.YoloEngine} {comparison.Candidate.MeanDice:0.000} / macro IoU U-Net {comparison.Baseline.MeanIoU:0.000} · {request.YoloEngine} {comparison.Candidate.MeanIoU:0.000}";
                string reportPath = comparison.ReportPath ?? string.Empty;
                string completeText = "U-Net vs YOLO-seg 비교 완료";
                view.SetSegmentationState(
                    isRunning: false,
                    statusText: completeText,
                    detailText: resultText,
                    actionText: "결과 artifact: " + reportPath + " / 이 결과만으로 검사 모델을 자동 교체하지 않습니다. 클래스별 Dice/IoU와 component FP/FN을 검토한 뒤 별도로 채택하세요.");
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

                string errorText = "U-Net vs YOLO-seg 비교 실패: " + FirstLine(ex.Message);
                view.SetSegmentationState(
                    isRunning: false,
                    statusText: "결과 확인 필요",
                    detailText: errorText,
                    actionText: "원본 레시피와 현재 검사 모델은 변경되지 않았습니다. 실행 로그와 각 checkpoint를 확인하세요.");
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
                return "\uBAA8\uB378 \uBE44\uAD50 \uC2E4\uD328: \uC2E4\uD589 \uACB0\uACFC\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.";
            }

            string firstLine = detail
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault() ?? detail.Trim();
            return $"\uBAA8\uB378 \uBE44\uAD50 \uC2E4\uD328: {firstLine}";
        }

        private static string FirstLine(string value)
        {
            return (value ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()?.Trim() ?? "실행 상세가 없습니다.";
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
