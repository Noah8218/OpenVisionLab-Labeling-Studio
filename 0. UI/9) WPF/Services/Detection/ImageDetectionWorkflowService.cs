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
using DrawingSize = System.Drawing.Size;

namespace MvcVisionSystem
{
    // Owns single-image inference and each batch worker request. Runtime access is
    // lazy; constructing a Shell must not start Python or initialize communication.
    // The caller's UI context owns admission and callbacks. Transport events may
    // arrive on a worker thread; the existing waiter resumes the captured context.
    public sealed class ImageDetectionWorkflowService : IDisposable
    {
        private readonly Func<PythonModelSettings> settingsAccessor;
        private readonly Func<DetectionResultApplicationService> resultsAccessor;
        private readonly Func<int, CancellationToken, Task<bool>> ensureReadyAsync;
        private readonly Func<bool, string, DrawingSize, bool> tryStartDetection;
        private readonly Func<string> workerFailureAccessor;
        private readonly Func<string> requestErrorAccessor;
        private readonly Func<PythonModelSettings, string, CancellationToken, Task<YoloWorkerSmokeTestResult>> runSmokeAsync;
        private readonly DetectionTargetService detectionTargetService = new DetectionTargetService();
        private readonly DetectionResultPresentationService detectionResultPresentationService = new DetectionResultPresentationService();
        private CancellationTokenSource interactiveCancellation;
        private bool disposed;

        public ImageDetectionWorkflowService(
            Func<PythonModelSettings> settingsAccessor,
            Func<DetectionResultApplicationService> resultsAccessor,
            Func<int, CancellationToken, Task<bool>> ensureReadyAsync,
            Func<bool, string, DrawingSize, bool> tryStartDetection,
            Func<string> workerFailureAccessor,
            Func<string> requestErrorAccessor,
            Func<PythonModelSettings, string, CancellationToken, Task<YoloWorkerSmokeTestResult>> runSmokeAsync = null)
        {
            this.settingsAccessor = settingsAccessor ?? throw new ArgumentNullException(nameof(settingsAccessor));
            this.resultsAccessor = resultsAccessor ?? throw new ArgumentNullException(nameof(resultsAccessor));
            this.ensureReadyAsync = ensureReadyAsync ?? throw new ArgumentNullException(nameof(ensureReadyAsync));
            this.tryStartDetection = tryStartDetection ?? throw new ArgumentNullException(nameof(tryStartDetection));
            this.workerFailureAccessor = workerFailureAccessor ?? throw new ArgumentNullException(nameof(workerFailureAccessor));
            this.requestErrorAccessor = requestErrorAccessor ?? throw new ArgumentNullException(nameof(requestErrorAccessor));
            this.runSmokeAsync = runSmokeAsync ?? YoloWorkerSmokeTestService.RunAsync;
        }

        public bool IsDetecting { get; private set; }

        public async Task RunInteractiveAsync(string imagePath, string activeImagePath, bool allowSmokeFallback, ImageDetectionCallbacks view)
        {
            if (disposed || IsDetecting)
            {
                return;
            }

            PythonModelSettings settings = settingsAccessor();
            IsDetecting = true;
            CancellationTokenSource cancellation = new CancellationTokenSource();
            interactiveCancellation = cancellation;
            CancellationToken cancellationToken = cancellation.Token;
            var totalStopwatch = Stopwatch.StartNew();
            try
            {
                view.RefreshActions();
                view.SetCommandStatus(InferenceStatusPresentationService.BuildInteractivePreparingCommandStatus(), true);
                view.SetInferenceStatus(InferenceStatusPresentationService.BuildInteractivePreparingInferenceStatus(), true, false);
                view.SetPythonStatus("\uCD94\uB860: \uC900\uBE44 \uC911");
                string targetImagePath = detectionTargetService.ResolveInteractiveTargetPath(
                    imagePath,
                    activeImagePath,
                    settings);
                string inferencePath = "worker";
                YoloWorkerSmokeTestResult result = await RunWorkerAsync(
                        targetImagePath,
                        applyToCanvas: true,
                        cancellationToken,
                        YoloRuntimePresentationService.GetInteractiveWorkerConnectTimeoutMilliseconds(
                            settings?.DetectionTimeoutSeconds ?? 30,
                            settings?.AutoStartClient != false,
                            allowSmokeFallback), view)
                    .ConfigureAwait(true);
                if (disposed)
                {
                    return;
                }

                if (!result.Succeeded && allowSmokeFallback)
                {
                    view.AppendLog($"\uCD94\uB860 \uC2E4\uD328, \uD14C\uC2A4\uD2B8 \uACBD\uB85C\uB85C \uC804\uD658: {Path.GetFileName(targetImagePath)}");
                    inferencePath = "smoke fallback";
                    result = await RunSmokeAsync(targetImagePath, applyToCanvas: true, cancellationToken, view)
                        .ConfigureAwait(true);
                    if (disposed)
                    {
                        return;
                    }
                }

                string elapsed = YoloRuntimePresentationService.FormatElapsed(totalStopwatch.Elapsed);
                string inferencePathText = YoloRuntimePresentationService.FormatInferencePath(inferencePath);
                view.SetCommandStatus(
                    InferenceStatusPresentationService.BuildInteractiveCompletionCommandStatus(result, elapsed),
                    false);
                view.SetInferenceStatus(
                    InferenceStatusPresentationService.BuildInteractiveCompletionInferenceStatus(result, elapsed),
                    false,
                    !result.Succeeded);
                view.AppendLog(InferenceStatusPresentationService.BuildInteractiveCompletionLog(result, elapsed, inferencePathText));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || disposed)
            {
                if (!disposed)
                {
                    view.AppendLog("추론이 취소되었습니다.");
                }
            }
            finally
            {
                if (ReferenceEquals(interactiveCancellation, cancellation))
                {
                    interactiveCancellation = null;
                }

                cancellation.Dispose();
                IsDetecting = false;
                if (!disposed)
                {
                    view.RefreshActions();
                }
            }
        }

        public async Task<YoloWorkerSmokeTestResult> RunSmokeAsync(
            string imagePath,
            bool applyToCanvas,
            CancellationToken cancellationToken,
            ImageDetectionCallbacks view)
        {
            if (disposed)
            {
                return new YoloWorkerSmokeTestResult
                {
                    ImagePath = imagePath ?? string.Empty
                };
            }

            cancellationToken.ThrowIfCancellationRequested();

            var stopwatch = Stopwatch.StartNew();
            PythonModelSettings settings = settingsAccessor();
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                view.AppendLog($"검출 이미지 없음: {imagePath}");
                return new YoloWorkerSmokeTestResult
                {
                    Succeeded = false,
                    Summary = "검출 이미지를 찾지 못했습니다.",
                    ImagePath = imagePath ?? string.Empty,
                    Errors = new[] { $"검출 이미지를 찾지 못했습니다: {imagePath}" }
                };
            }

            if (applyToCanvas) view.PrepareCanvasImage(imagePath, true);

            view.SetPythonStatus("\uCD94\uB860: \uD14C\uC2A4\uD2B8 \uC2E4\uD589 \uC911");
            view.AppendLog($"\uD14C\uC2A4\uD2B8 \uCD94\uB860 \uC2DC\uC791: {Path.GetFileName(imagePath)}");
            YoloWorkerSmokeTestResult result = await runSmokeAsync(settings, imagePath, cancellationToken)
                .ConfigureAwait(true);
            if (disposed)
            {
                return new YoloWorkerSmokeTestResult
                {
                    ImagePath = imagePath ?? string.Empty
                };
            }

            if (applyToCanvas)
            {
                // Keep existing manual labels when smoke detection returns the already-active image;
                // Candidate Review needs those labels to compute duplicate/current-label focus.
                if (!string.IsNullOrWhiteSpace(result.ImagePath)
                    && File.Exists(result.ImagePath))
                {
                    view.PrepareCanvasImage(result.ImagePath, true);
                }

                view.ApplyCandidates(result.Candidates, result.Succeeded);
                view.SetPythonStatus(detectionResultPresentationService.BuildSmokeStatus(result));
                foreach (string error in result.Errors)
                {
                    view.AppendLog($"- {error}");
                }
            }

            view.AppendLog(result.Summary);
            view.AppendLog($"\uD14C\uC2A4\uD2B8 \uCD94\uB860 \uC2DC\uAC04: {YoloRuntimePresentationService.FormatElapsed(stopwatch.Elapsed)}");
            return result;
        }

        public async Task<YoloWorkerSmokeTestResult> RunWorkerAsync(
            string imagePath,
            bool applyToCanvas,
            CancellationToken cancellationToken,
            int connectTimeoutMilliseconds,
            ImageDetectionCallbacks view,
            bool workerReadyAlreadyChecked = false)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stopwatch = Stopwatch.StartNew();
            PythonModelSettings settings = settingsAccessor();
            string modelSourceText = InferenceStatusPresentationService.BuildRuntimeModelLabel(
                settings);
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                return new YoloWorkerSmokeTestResult
                {
                    Succeeded = false,
                    Summary = InferenceStatusPresentationService.BuildWorkerImageMissingSummary(),
                    ImagePath = imagePath ?? string.Empty,
                    Errors = new[] { InferenceStatusPresentationService.BuildWorkerImageMissingError(imagePath) }
                };
            }

            DrawingSize requestImageSize;
            if (applyToCanvas)
            {
                DrawingSize? preparedSize = view.PrepareCanvasImage(imagePath, false);
                if (!preparedSize.HasValue)
                {
                    return new YoloWorkerSmokeTestResult
                    {
                        Succeeded = false,
                        Summary = InferenceStatusPresentationService.BuildWorkerImageLoadFailureSummary(),
                        ImagePath = imagePath,
                        Errors = new[] { InferenceStatusPresentationService.BuildWorkerImageLoadFailureError(imagePath) }
                    };
                }
                requestImageSize = preparedSize.Value;
            }
            else if (!ImageQueueDetailLoader.TryReadImageSize(imagePath, out requestImageSize, out string imageSizeError))
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
                : YoloRuntimePresentationService.GetWorkerConnectTimeoutMilliseconds(
                    settings?.DetectionTimeoutSeconds ?? 30);
            view.SetInferenceStatus(InferenceStatusPresentationService.BuildWorkerPreparingInferenceStatus(applyToCanvas, imagePath), true, false);
            view.SetPythonStatus("\uCD94\uB860: \uC5F0\uACB0 \uD655\uC778 \uC911");
            view.SetCommandStatus(InferenceStatusPresentationService.BuildWorkerPreparingCommandStatus(), true);
            bool ready = workerReadyAlreadyChecked
                ? true
                : await ensureReadyAsync(timeoutMilliseconds, cancellationToken).ConfigureAwait(true);
            if (disposed)
            {
                return new YoloWorkerSmokeTestResult
                {
                    ImagePath = imagePath
                };
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (!ready)
            {
                view.SetInferenceStatus(InferenceStatusPresentationService.BuildWorkerConnectionFailureInferenceStatus(), false, true);
                view.SetPythonStatus("\uCD94\uB860: \uC5F0\uACB0 \uC2E4\uD328");
                view.AppendLog(InferenceStatusPresentationService.BuildWorkerConnectionFailureLog(
                    YoloRuntimePresentationService.FormatElapsed(stopwatch.Elapsed)));
                string workerFailureText = workerFailureAccessor();
                return new YoloWorkerSmokeTestResult
                {
                    Succeeded = false,
                    Summary = workerFailureText,
                    ImagePath = imagePath,
                    Errors = new[] { workerFailureText }
                };
            }

            DetectionResultApplicationService detectionResults = resultsAccessor();
            using var completionWaiter = new DetectionWorkerCompletionWaiter(
                detectionResults,
                imagePath,
                cancellationToken);

            try
            {
                view.SetInferenceStatus(InferenceStatusPresentationService.BuildWorkerRunningInferenceStatus(applyToCanvas, imagePath), true, false);
                view.SetPythonStatus("\uCD94\uB860: \uC2E4\uD589 \uC911");
                view.AppendLog(InferenceStatusPresentationService.BuildWorkerStartLog(imagePath, modelSourceText));
                view.SetCommandStatus(InferenceStatusPresentationService.BuildWorkerRequestCommandStatus(), true);
                bool started = tryStartDetection(applyToCanvas, imagePath, requestImageSize);
                if (!started)
                {
                    view.SetInferenceStatus(InferenceStatusPresentationService.BuildWorkerRequestFailureInferenceStatus(), false, true);
                    view.SetPythonStatus("\uCD94\uB860: \uC694\uCCAD \uC2E4\uD328");
                    return new YoloWorkerSmokeTestResult
                    {
                        Succeeded = false,
                        Summary = InferenceStatusPresentationService.BuildWorkerRequestFailureSummary(requestErrorAccessor()),
                        ImagePath = imagePath
                    };
                }

                DetectionCandidatesUpdatedEventArgs completed = await completionWaiter.Completion.ConfigureAwait(true);
                if (disposed)
                {
                    return new YoloWorkerSmokeTestResult
                    {
                        ImagePath = imagePath
                    };
                }

                if (completed.Reason == DetectionCandidateUpdateReason.RequestTimedOut)
                {
                    view.SetInferenceStatus(InferenceStatusPresentationService.BuildWorkerTimedOutInferenceStatus(), false, true);
                    string timeoutSummary = InferenceStatusPresentationService.BuildWorkerTimedOutSummary();
                    return new YoloWorkerSmokeTestResult
                    {
                        Succeeded = false,
                        Summary = timeoutSummary,
                        ImagePath = imagePath,
                        Errors = new[] { timeoutSummary }
                    };
                }

                IReadOnlyList<DefectInfo> defects = detectionResults.GetLastDefects();
                IReadOnlyList<YoloWorkerSmokeCandidate> candidates = defects
                    .Select((defect, index) => CandidateReviewPresentationService.FromDefect(defect, index + 1))
                    .ToList();
                YoloWorkerSmokeCandidate first = candidates.FirstOrDefault();
                var result = new YoloWorkerSmokeTestResult
                {
                    Succeeded = true,
                    Summary = InferenceStatusPresentationService.BuildWorkerSuccessSummary(modelSourceText, candidates.Count),
                    ImagePath = imagePath,
                    CandidateCount = candidates.Count,
                    FirstClassName = first?.ClassName ?? string.Empty,
                    FirstConfidence = first?.Confidence,
                    Candidates = candidates
                };

                if (applyToCanvas)
                {
                    view.ApplyCandidates(result.Candidates, result.Succeeded);
                    view.SetPythonStatus(InferenceStatusPresentationService.BuildWorkerPythonCompletedStatus(modelSourceText, result.CandidateCount));
                }

                view.AppendLog(InferenceStatusPresentationService.BuildWorkerElapsedLog(
                    YoloRuntimePresentationService.FormatElapsed(stopwatch.Elapsed),
                    modelSourceText));
                return result;
            }
            catch (OperationCanceledException)
            {
                if (disposed)
                {
                    return new YoloWorkerSmokeTestResult
                    {
                        ImagePath = imagePath
                    };
                }

                view.SetInferenceStatus(InferenceStatusPresentationService.BuildWorkerCanceledInferenceStatus(), false, true);
                string canceledSummary = InferenceStatusPresentationService.BuildWorkerCanceledSummary();
                return new YoloWorkerSmokeTestResult
                {
                    Succeeded = false,
                    Summary = canceledSummary,
                    ImagePath = imagePath,
                    Errors = new[] { canceledSummary }
                };
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            interactiveCancellation?.Cancel();
            interactiveCancellation?.Dispose();
            interactiveCancellation = null;
            IsDetecting = false;
        }
    }

    // Data-only UI ports: the execution owner never receives a Window/control.
    // PrepareCanvasImage preserves same-image manual labels in the Shell adapter.
    public sealed class ImageDetectionCallbacks
    {
        public Func<string, bool, DrawingSize?> PrepareCanvasImage { get; init; }
        public Action<IReadOnlyList<YoloWorkerSmokeCandidate>, bool> ApplyCandidates { get; init; }
        public Action RefreshActions { get; init; }
        public Action<string> SetPythonStatus { get; init; }
        public Action<string, bool> SetCommandStatus { get; init; }
        public Action<string, bool, bool> SetInferenceStatus { get; init; }
        public Action<string> AppendLog { get; init; }
    }
}
