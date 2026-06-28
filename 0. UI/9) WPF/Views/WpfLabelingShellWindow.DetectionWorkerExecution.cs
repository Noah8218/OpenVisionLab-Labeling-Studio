using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingSize = System.Drawing.Size;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Worker detection orchestration is kept away from result application to make UI stalls easier to profile.
        private async Task<YoloWorkerSmokeTestResult> RunWorkerDetectionForImageAsync(
            string imagePath,
            bool applyToCanvas,
            CancellationToken cancellationToken,
            int connectTimeoutMilliseconds = -1,
            bool workerReadyAlreadyChecked = false)
        {
            var stopwatch = Stopwatch.StartNew();
            EnsureProjectSettings();
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                return new YoloWorkerSmokeTestResult
                {
                    Succeeded = false,
                    Summary = "검출 이미지를 찾지 못했습니다.",
                    ImagePath = imagePath ?? string.Empty,
                    Errors = new[] { $"검출 이미지를 찾지 못했습니다: {imagePath}" }
                };
            }

            DrawingSize requestImageSize = activeImageSize;
            // Running inference on the current image must preserve in-progress labels;
            // reloading here would erase the manual ROI/mask state before candidate comparison.
            bool shouldLoadTargetImage = applyToCanvas
                && !string.Equals(imagePath, activeImagePath, StringComparison.OrdinalIgnoreCase);
            if (shouldLoadTargetImage && !TryLoadImage(imagePath, populateQueue: false))
            {
                return new YoloWorkerSmokeTestResult
                {
                    Succeeded = false,
                    Summary = "검출 이미지를 로드하지 못했습니다.",
                    ImagePath = imagePath,
                    Errors = new[] { $"검출 이미지를 로드하지 못했습니다: {imagePath}" }
                };
            }

            if (applyToCanvas)
            {
                requestImageSize = activeImageSize;
            }
            else if (!TryReadImageSize(imagePath, out requestImageSize, out string imageSizeError))
            {
                return new YoloWorkerSmokeTestResult
                {
                    Succeeded = false,
                    Summary = imageSizeError,
                    ImagePath = imagePath,
                    Errors = new[] { imageSizeError }
                };
            }

            int timeoutMilliseconds = connectTimeoutMilliseconds > 0
                ? connectTimeoutMilliseconds
                : GetWorkerConnectTimeoutMilliseconds();
            SetGlobalInferenceStatus(
                applyToCanvas
                    ? "현재 이미지 추론 준비 중"
                    : $"일괄 추론 준비: {Path.GetFileName(imagePath)}",
                isBusy: true);
            SetPythonStatus("\uCD94\uB860: \uC5F0\uACB0 \uD655\uC778 \uC911");
            SetYoloCommandStatus("\uCD94\uB860 \uC2E4\uD589\uAE30 \uC900\uBE44 \uC911...", isBusy: true);
            bool ready = workerReadyAlreadyChecked
                ? true
                : await global.EnsurePythonModelClientReadyAsync(timeoutMilliseconds).ConfigureAwait(true);
            if (!ready)
            {
                SetGlobalInferenceStatus("\uC2E4\uD328: \uCD94\uB860 \uC5F0\uACB0 \uC2E4\uD328", isBusy: false, isWarning: true);
                SetPythonStatus("\uCD94\uB860: \uC5F0\uACB0 \uC2E4\uD328");
                AppendLog($"\uCD94\uB860 \uC5F0\uACB0 \uC2E4\uD328: {FormatElapsed(stopwatch.Elapsed)}. \uBAA8\uB378 \uC124\uC815 \uB610\uB294 \uCD94\uB860 \uC2E4\uD589 \uC0C1\uD0DC\uB97C \uD655\uC778\uD558\uC138\uC694.");
                return new YoloWorkerSmokeTestResult
                {
                    Succeeded = false,
                    Summary = BuildPythonWorkerFailureText(),
                    ImagePath = imagePath,
                    Errors = new[] { BuildPythonWorkerFailureText() }
                };
            }

            using var completionWaiter = new WpfDetectionWorkerCompletionWaiter(
                global.DetectionResults,
                imagePath,
                cancellationToken);

            try
            {
                SetGlobalInferenceStatus(
                    applyToCanvas
                        ? "AI 추론 중"
                        : $"일괄 추론 중: {Path.GetFileName(imagePath)}",
                    isBusy: true);
                SetPythonStatus("\uCD94\uB860: \uC2E4\uD589 \uC911");
                AppendLog($"\uCD94\uB860 \uC2DC\uC791: {Path.GetFileName(imagePath)}");
                SetYoloCommandStatus("AI 추론 요청 중...", isBusy: true);
                bool started = applyToCanvas
                    ? global.DetectionWorkflow.TryStartCurrentImageDetection(
                        global.Data,
                        global.DeepLearning,
                        global.DetectionResults,
                        () => true)
                    : global.DetectionWorkflow.TryStartImagePathDetection(
                        global.Data,
                        global.DeepLearning,
                        global.DetectionResults,
                        imagePath,
                        requestImageSize,
                        () => true);
                if (!started)
                {
                    SetGlobalInferenceStatus("\uC2E4\uD328: \uCD94\uB860 \uC694\uCCAD \uC2E4\uD328", isBusy: false, isWarning: true);
                    SetPythonStatus("\uCD94\uB860: \uC694\uCCAD \uC2E4\uD328");
                    return new YoloWorkerSmokeTestResult
                    {
                        Succeeded = false,
                        Summary = FirstNonEmpty(global.GetPythonCommunicationStatusSnapshot().LastError, "\uCD94\uB860 \uAC80\uCD9C \uC694\uCCAD\uC744 \uBCF4\uB0B4\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4."),
                        ImagePath = imagePath
                    };
                }

                DetectionCandidatesUpdatedEventArgs completed = await completionWaiter.Completion.ConfigureAwait(true);
                if (completed.Reason == DetectionCandidateUpdateReason.RequestTimedOut)
                {
                    SetGlobalInferenceStatus("\uC2E4\uD328: \uCD94\uB860 \uC2DC\uAC04 \uCD08\uACFC", isBusy: false, isWarning: true);
                    return new YoloWorkerSmokeTestResult
                    {
                        Succeeded = false,
                        Summary = "\uCD94\uB860 \uAC80\uCD9C \uC2DC\uAC04 \uCD08\uACFC.",
                        ImagePath = imagePath,
                        Errors = new[] { "\uCD94\uB860 \uAC80\uCD9C \uC2DC\uAC04 \uCD08\uACFC." }
                    };
                }

                IReadOnlyList<DefectInfo> defects = global.DetectionResults.GetLastDefects();
                IReadOnlyList<YoloWorkerSmokeCandidate> candidates = defects
                    .Select((defect, index) => ToSmokeCandidate(defect, index + 1))
                    .ToList();
                YoloWorkerSmokeCandidate first = candidates.FirstOrDefault();
                var result = new YoloWorkerSmokeTestResult
                {
                    Succeeded = true,
                    Summary = $"\uCD94\uB860 \uC644\uB8CC. \uD6C4\uBCF4:{candidates.Count}",
                    ImagePath = imagePath,
                    CandidateCount = candidates.Count,
                    FirstClassName = first?.ClassName ?? string.Empty,
                    FirstConfidence = first?.Confidence,
                    Candidates = candidates
                };

                if (applyToCanvas)
                {
                    ApplyDetectionCandidates(result.Candidates, result.Succeeded);
                    SetPythonStatus($"\uCD94\uB860: \uC644\uB8CC  \uD6C4\uBCF4 {result.CandidateCount}");
                }

                AppendLog($"\uCD94\uB860 \uC2DC\uAC04: {FormatElapsed(stopwatch.Elapsed)}");
                return result;
            }
            catch (OperationCanceledException)
            {
                SetGlobalInferenceStatus("추론 취소", isBusy: false, isWarning: true);
                return new YoloWorkerSmokeTestResult
                {
                    Succeeded = false,
                    Summary = "\uCD94\uB860 \uAC80\uCD9C \uCDE8\uC18C.",
                    ImagePath = imagePath,
                    Errors = new[] { "\uCD94\uB860 \uAC80\uCD9C \uCDE8\uC18C." }
                };
            }
        }
    }
}
