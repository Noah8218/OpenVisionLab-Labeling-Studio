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
            string modelSourceText = WpfInferenceStatusPresentationService.BuildRuntimeModelLabel(
                global.Data?.ProjectSettings?.PythonModel);
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                return new YoloWorkerSmokeTestResult
                {
                    Succeeded = false,
                    Summary = WpfInferenceStatusPresentationService.BuildWorkerImageMissingSummary(),
                    ImagePath = imagePath ?? string.Empty,
                    Errors = new[] { WpfInferenceStatusPresentationService.BuildWorkerImageMissingError(imagePath) }
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
                    Summary = WpfInferenceStatusPresentationService.BuildWorkerImageLoadFailureSummary(),
                    ImagePath = imagePath,
                    Errors = new[] { WpfInferenceStatusPresentationService.BuildWorkerImageLoadFailureError(imagePath) }
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
            SetGlobalInferenceStatus(WpfInferenceStatusPresentationService.BuildWorkerPreparingInferenceStatus(applyToCanvas, imagePath), isBusy: true);
            SetPythonStatus("\uCD94\uB860: \uC5F0\uACB0 \uD655\uC778 \uC911");
            SetYoloCommandStatus(WpfInferenceStatusPresentationService.BuildWorkerPreparingCommandStatus(), isBusy: true);
            bool ready = workerReadyAlreadyChecked
                ? true
                : await global.EnsurePythonModelClientReadyAsync(timeoutMilliseconds).ConfigureAwait(true);
            if (!ready)
            {
                SetGlobalInferenceStatus(WpfInferenceStatusPresentationService.BuildWorkerConnectionFailureInferenceStatus(), isBusy: false, isWarning: true);
                SetPythonStatus("\uCD94\uB860: \uC5F0\uACB0 \uC2E4\uD328");
                AppendLog(WpfInferenceStatusPresentationService.BuildWorkerConnectionFailureLog(FormatElapsed(stopwatch.Elapsed)));
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
                SetGlobalInferenceStatus(WpfInferenceStatusPresentationService.BuildWorkerRunningInferenceStatus(applyToCanvas, imagePath), isBusy: true);
                SetPythonStatus("\uCD94\uB860: \uC2E4\uD589 \uC911");
                AppendLog(WpfInferenceStatusPresentationService.BuildWorkerStartLog(imagePath, modelSourceText));
                SetYoloCommandStatus(WpfInferenceStatusPresentationService.BuildWorkerRequestCommandStatus(), isBusy: true);
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
                    SetGlobalInferenceStatus(WpfInferenceStatusPresentationService.BuildWorkerRequestFailureInferenceStatus(), isBusy: false, isWarning: true);
                    SetPythonStatus("\uCD94\uB860: \uC694\uCCAD \uC2E4\uD328");
                    return new YoloWorkerSmokeTestResult
                    {
                        Succeeded = false,
                        Summary = WpfInferenceStatusPresentationService.BuildWorkerRequestFailureSummary(global.GetPythonCommunicationStatusSnapshot().LastError),
                        ImagePath = imagePath
                    };
                }

                DetectionCandidatesUpdatedEventArgs completed = await completionWaiter.Completion.ConfigureAwait(true);
                if (completed.Reason == DetectionCandidateUpdateReason.RequestTimedOut)
                {
                    SetGlobalInferenceStatus(WpfInferenceStatusPresentationService.BuildWorkerTimedOutInferenceStatus(), isBusy: false, isWarning: true);
                    string timeoutSummary = WpfInferenceStatusPresentationService.BuildWorkerTimedOutSummary();
                    return new YoloWorkerSmokeTestResult
                    {
                        Succeeded = false,
                        Summary = timeoutSummary,
                        ImagePath = imagePath,
                        Errors = new[] { timeoutSummary }
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
                    Summary = WpfInferenceStatusPresentationService.BuildWorkerSuccessSummary(modelSourceText, candidates.Count),
                    ImagePath = imagePath,
                    CandidateCount = candidates.Count,
                    FirstClassName = first?.ClassName ?? string.Empty,
                    FirstConfidence = first?.Confidence,
                    Candidates = candidates
                };

                if (applyToCanvas)
                {
                    ApplyDetectionCandidates(result.Candidates, result.Succeeded);
                    SetPythonStatus(WpfInferenceStatusPresentationService.BuildWorkerPythonCompletedStatus(modelSourceText, result.CandidateCount));
                }

                AppendLog(WpfInferenceStatusPresentationService.BuildWorkerElapsedLog(FormatElapsed(stopwatch.Elapsed), modelSourceText));
                return result;
            }
            catch (OperationCanceledException)
            {
                SetGlobalInferenceStatus(WpfInferenceStatusPresentationService.BuildWorkerCanceledInferenceStatus(), isBusy: false, isWarning: true);
                string canceledSummary = WpfInferenceStatusPresentationService.BuildWorkerCanceledSummary();
                return new YoloWorkerSmokeTestResult
                {
                    Succeeded = false,
                    Summary = canceledSummary,
                    ImagePath = imagePath,
                    Errors = new[] { canceledSummary }
                };
            }
        }
    }
}
