using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.Yolo;
using OpenVisionLab;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
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
        private readonly Func<PythonModelSettings, string> trainingRunIdentityAccessor;
        private readonly Func<PythonModelSettings, string> modelIdentityAccessor;
        private readonly Func<IReadOnlyList<LabelClass>> supportedClassesAccessor;
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
            Func<PythonModelSettings, string, CancellationToken, Task<YoloWorkerSmokeTestResult>> runSmokeAsync = null,
            Func<PythonModelSettings, string> trainingRunIdentityAccessor = null,
            Func<PythonModelSettings, string> modelIdentityAccessor = null,
            Func<IReadOnlyList<LabelClass>> supportedClassesAccessor = null)
        {
            this.settingsAccessor = settingsAccessor ?? throw new ArgumentNullException(nameof(settingsAccessor));
            this.resultsAccessor = resultsAccessor ?? throw new ArgumentNullException(nameof(resultsAccessor));
            this.ensureReadyAsync = ensureReadyAsync ?? throw new ArgumentNullException(nameof(ensureReadyAsync));
            this.tryStartDetection = tryStartDetection ?? throw new ArgumentNullException(nameof(tryStartDetection));
            this.workerFailureAccessor = workerFailureAccessor ?? throw new ArgumentNullException(nameof(workerFailureAccessor));
            this.requestErrorAccessor = requestErrorAccessor ?? throw new ArgumentNullException(nameof(requestErrorAccessor));
            this.runSmokeAsync = runSmokeAsync;
            this.trainingRunIdentityAccessor = trainingRunIdentityAccessor;
            this.modelIdentityAccessor = modelIdentityAccessor;
            this.supportedClassesAccessor = supportedClassesAccessor;
        }

        public bool IsDetecting { get; private set; }

        public async Task RunInteractiveAsync(
            string imagePath,
            string activeImagePath,
            bool allowSmokeFallback,
            ImageDetectionCallbacks view,
            PythonModelSettings trialModelSettings = null,
            string trialModelCandidateId = "",
            bool decisionEvaluation = false,
            string decisionOutputDirectory = "")
        {
            if (disposed || IsDetecting)
            {
                return;
            }

            bool isTrialModelRun = trialModelSettings != null;
            PythonModelSettings settings = isTrialModelRun ? CloneSettings(trialModelSettings) : settingsAccessor();
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
                view.SetPythonStatus(OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.ImageDetectionWorkflowService.PreparingPython"));
                string targetImagePath = detectionTargetService.ResolveInteractiveTargetPath(
                    imagePath,
                    activeImagePath,
                    settings);
                string inferencePath = "worker";
                YoloWorkerSmokeTestResult result;
                if (isTrialModelRun)
                {
                    result = await RunSmokeAsync(
                        targetImagePath,
                        applyToCanvas: true,
                        cancellationToken,
                        view,
                        settings,
                        trialModelCandidateId, decisionEvaluation, decisionOutputDirectory).ConfigureAwait(true);
                    inferencePath = "trial model";
                }
                else
                {
                    result = await RunWorkerAsync(
                            targetImagePath,
                            applyToCanvas: true,
                            cancellationToken,
                            YoloRuntimePresentationService.GetInteractiveWorkerConnectTimeoutMilliseconds(
                                settings?.DetectionTimeoutSeconds ?? 30,
                                settings?.AutoStartClient != false,
                                allowSmokeFallback), view)
                        .ConfigureAwait(true);
                }
                if (disposed)
                {
                    return;
                }

                if (!result.Succeeded && allowSmokeFallback)
                {
                    view.AppendLog($"\uCD94\uB860 \uC2E4\uD328, \uD14C\uC2A4\uD2B8 \uACBD\uB85C\uB85C \uC804\uD658: {Path.GetFileName(targetImagePath)}");
                    inferencePath = "smoke fallback";
                    result = await RunSmokeAsync(targetImagePath, applyToCanvas: true, cancellationToken, view,
                            decisionEvaluation: decisionEvaluation, decisionOutputDirectory: decisionOutputDirectory)
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
                    view.AppendLog(OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.ImageDetectionWorkflowService.CanceledLog"));
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
            ImageDetectionCallbacks view,
            PythonModelSettings settingsOverride = null,
            string modelCandidateId = "",
            bool decisionEvaluation = false,
            string decisionOutputDirectory = "",
            string trainingRunIdentity = "")
        {
            if (disposed) return new YoloWorkerSmokeTestResult { ImagePath = imagePath ?? string.Empty };
            cancellationToken.ThrowIfCancellationRequested();
            PythonModelSettings settings = CloneSettings(settingsOverride ?? settingsAccessor());
            string capturedModelIdentity = modelIdentityAccessor?.Invoke(settings) ?? string.Empty;
            string capturedTrainingRun = string.IsNullOrWhiteSpace(trainingRunIdentity) ? trainingRunIdentityAccessor?.Invoke(settings) ?? string.Empty : trainingRunIdentity;
            string capturedSettingsIdentity = string.IsNullOrWhiteSpace(capturedModelIdentity) ? string.Empty
                : PythonModelSettings.BuildCandidateSettingsIdentity(settings, capturedModelIdentity + "|" + capturedTrainingRun);
            string capturedImageIdentity = ReadContentIdentity(imagePath), capturedArtifactIdentity = ReadContentIdentity(settings.WeightsPath);
            InferenceResultSource source = InferenceResultSource.Capture(imagePath, settings, view.IsResultOnly?.Invoke() == true,
                string.IsNullOrWhiteSpace(modelCandidateId) ? capturedModelIdentity : modelCandidateId);
            YoloWorkerSmokeTestResult result = await RunSmokeCoreAsync(imagePath, applyToCanvas, cancellationToken, view, settings, modelCandidateId,
                decisionEvaluation, decisionOutputDirectory, capturedTrainingRun, capturedImageIdentity, capturedArtifactIdentity, capturedModelIdentity, source).ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();
            if (!disposed)
            {
                result.Source = source with { CompletedAt = DateTimeOffset.UtcNow };
                result.CaptureTrainingRunId = capturedTrainingRun;
                result.CaptureCandidateSettingsIdentity = capturedSettingsIdentity;
                view.RecordResult?.Invoke(result);
                if (!result.Succeeded) view.SetPythonStatus(OpenVisionLanguageService.T("WpfInference.Run.FailedStatus"));
            }
            return result;
        }

        private async Task<YoloWorkerSmokeTestResult> RunSmokeCoreAsync(
            string imagePath,
            bool applyToCanvas,
            CancellationToken cancellationToken,
            ImageDetectionCallbacks view,
            PythonModelSettings settingsOverride = null,
            string modelCandidateId = "",
            bool decisionEvaluation = false,
            string decisionOutputDirectory = "",
            string expectedTrainingRunIdentity = "",
            string expectedImageIdentity = "",
            string expectedArtifactIdentity = "",
            string expectedModelIdentity = "",
            InferenceResultSource requestSource = null)
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
            PythonModelSettings settings = settingsOverride == null ? settingsAccessor() : CloneSettings(settingsOverride);
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

            view.SetPythonStatus(string.IsNullOrWhiteSpace(modelCandidateId)
                ? "\uCD94\uB860: \uD14C\uC2A4\uD2B8 \uC2E4\uD589 \uC911"
                : OpenVisionLanguageService.T("WpfInference.Run.TrialInProgress"));
            view.AppendLog(string.IsNullOrWhiteSpace(modelCandidateId)
                ? $"\uD14C\uC2A4\uD2B8 \uCD94\uB860 \uC2DC\uC791: {Path.GetFileName(imagePath)}"
                : string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    OpenVisionLanguageService.T("WpfInference.Run.TrialStarted"),
                    Path.GetFileName(settings.WeightsPath)));
            YoloWorkerSmokeTestResult result = await (runSmokeAsync != null ? runSmokeAsync(settings, imagePath, cancellationToken)
                : YoloWorkerSmokeTestService.RunAsync(settings, imagePath, cancellationToken, decisionEvaluation, decisionOutputDirectory))
                .ConfigureAwait(true);
            cancellationToken.ThrowIfCancellationRequested();
            result.InferenceModelCandidateId = modelCandidateId ?? string.Empty;
            if (disposed)
            {
                return new YoloWorkerSmokeTestResult
                {
                    ImagePath = imagePath ?? string.Empty
                };
            }

            if (!string.Equals(expectedImageIdentity, ReadContentIdentity(imagePath), StringComparison.Ordinal)
                || !string.Equals(expectedArtifactIdentity, ReadContentIdentity(settings.WeightsPath), StringComparison.Ordinal)
                || !string.Equals(expectedModelIdentity, modelIdentityAccessor?.Invoke(settings) ?? string.Empty, StringComparison.Ordinal)
                || (trainingRunIdentityAccessor != null && !string.Equals(expectedTrainingRunIdentity, trainingRunIdentityAccessor(settings), StringComparison.Ordinal)))
                return new YoloWorkerSmokeTestResult { ImagePath = imagePath, Summary = OpenVisionLanguageService.T("WpfInference.Run.SourceChanged") };

            if (applyToCanvas)
            {
                // Keep existing manual labels when smoke detection returns the already-active image;
                // Candidate Review needs those labels to compute duplicate/current-label focus.
                if (view.IsCurrentImage?.Invoke(imagePath) == false)
                {
                    string staleSummary = OpenVisionLanguageService.T("WpfInference.Run.StaleNotApplied");
                    view.AppendLog(staleSummary);
                    return new YoloWorkerSmokeTestResult
                    {
                        Succeeded = false,
                        Summary = staleSummary,
                        ImagePath = imagePath,
                        Errors = new[] { staleSummary }
                    };
                }

                if (!string.IsNullOrWhiteSpace(result.ImagePath))
                {
                    if (view.IsCurrentImage?.Invoke(result.ImagePath) == false)
                    {
                        string staleSummary = OpenVisionLanguageService.T("WpfInference.Run.StaleNotApplied");
                        view.AppendLog(staleSummary);
                        return new YoloWorkerSmokeTestResult
                        {
                            Succeeded = false,
                            Summary = staleSummary,
                            ImagePath = imagePath,
                            Errors = new[] { staleSummary }
                        };
                    }

                    if (File.Exists(result.ImagePath))
                    {
                        view.PrepareCanvasImage(result.ImagePath, true);
                    }
                }

                if (result.Succeeded)
                {
                    IReadOnlyList<YoloWorkerSmokeCandidate> displayCandidates = result.Candidates.Any(candidate => candidate?.ImageLevel == true)
                        ? result.Candidates.Take(Math.Max(1, settings.MaximumDetectionCandidates)).ToArray()
                        : CandidateReviewStateService.BuildCandidatePresentation(result, supportedClassesAccessor?.Invoke(), requestSource);
                    view.ApplyCandidates(displayCandidates, true);
                }
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

        internal static PythonModelSettings CloneSettings(PythonModelSettings source)
        {
            return new PythonModelSettings
            {
                PythonExecutablePath = source?.PythonExecutablePath ?? string.Empty,
                ModelEngine = source?.ModelEngine ?? PythonModelSettings.EngineYoloV5,
                ProjectRootPath = source?.ProjectRootPath ?? string.Empty,
                ClientScriptPath = source?.ClientScriptPath ?? string.Empty,
                WeightsPath = source?.WeightsPath ?? string.Empty,
                ImageRootPath = source?.ImageRootPath ?? string.Empty,
                MinimumDetectionConfidence = source?.MinimumDetectionConfidence ?? 0.25F,
                MaximumDetectionCandidates = source?.MaximumDetectionCandidates ?? 20,
                InferenceImageSize = source?.InferenceImageSize ?? 320,
                DetectionTimeoutSeconds = source?.DetectionTimeoutSeconds ?? 30,
                AutoStartClient = source?.AutoStartClient ?? true
            };
        }

        public async Task<YoloWorkerSmokeTestResult> RunWorkerAsync(
            string imagePath,
            bool applyToCanvas,
            CancellationToken cancellationToken,
            int connectTimeoutMilliseconds,
            ImageDetectionCallbacks view,
            bool workerReadyAlreadyChecked = false)
        {
            if (disposed) return new YoloWorkerSmokeTestResult { ImagePath = imagePath ?? string.Empty };
            cancellationToken.ThrowIfCancellationRequested();
            PythonModelSettings settings = CloneSettings(settingsAccessor());
            string capturedModelIdentity = modelIdentityAccessor?.Invoke(settings) ?? string.Empty;
            string capturedTrainingRun = trainingRunIdentityAccessor?.Invoke(settings) ?? string.Empty;
            string capturedSettingsIdentity = string.IsNullOrWhiteSpace(capturedModelIdentity) ? string.Empty
                : PythonModelSettings.BuildCandidateSettingsIdentity(settings, capturedModelIdentity + "|" + capturedTrainingRun);
            string imageIdentity = ReadContentIdentity(imagePath), artifactIdentity = ReadContentIdentity(settings.WeightsPath);
            InferenceResultSource source = InferenceResultSource.Capture(imagePath, settings, view.IsResultOnly?.Invoke() == true, capturedModelIdentity);
            YoloWorkerSmokeTestResult result = await RunWorkerCoreAsync(imagePath, applyToCanvas, cancellationToken, connectTimeoutMilliseconds, view, workerReadyAlreadyChecked,
                settings, imageIdentity, artifactIdentity, capturedTrainingRun, capturedModelIdentity, source).ConfigureAwait(true);
            if (!disposed && !cancellationToken.IsCancellationRequested)
            {
                bool current = string.Equals(JsonSerializer.Serialize(settings), JsonSerializer.Serialize(settingsAccessor()), StringComparison.Ordinal)
                    && string.Equals(capturedModelIdentity, modelIdentityAccessor?.Invoke(settingsAccessor()) ?? string.Empty, StringComparison.Ordinal)
                    && string.Equals(capturedTrainingRun, trainingRunIdentityAccessor?.Invoke(settingsAccessor()) ?? string.Empty, StringComparison.Ordinal)
                    && string.Equals(imageIdentity, ReadContentIdentity(imagePath), StringComparison.Ordinal)
                    && string.Equals(artifactIdentity, ReadContentIdentity(settings.WeightsPath), StringComparison.Ordinal);
                if (current && result.Succeeded)
                {
                    result.CaptureImageSha256 = imageIdentity;
                    result.CaptureWeightsSha256 = artifactIdentity;
                }
                else if (!current)
                {
                    result.Succeeded = false;
                    result.Candidates = Array.Empty<YoloWorkerSmokeCandidate>();
                    result.CandidateCount = 0;
                    result.Summary = OpenVisionLanguageService.T("WpfInference.Run.SourceChanged");
                }
                result.Source = source with { CompletedAt = DateTimeOffset.UtcNow };
                result.CaptureTrainingRunId = capturedTrainingRun;
                result.CaptureCandidateSettingsIdentity = capturedSettingsIdentity;
                view.RecordResult?.Invoke(result);
                if (!result.Succeeded) view.SetPythonStatus(OpenVisionLanguageService.T("WpfInference.Run.FailedStatus"));
            }
            return result;
        }

        private async Task<YoloWorkerSmokeTestResult> RunWorkerCoreAsync(
            string imagePath,
            bool applyToCanvas,
            CancellationToken cancellationToken,
            int connectTimeoutMilliseconds,
            ImageDetectionCallbacks view,
            bool workerReadyAlreadyChecked = false,
            PythonModelSettings requestSettings = null,
            string requestImageIdentity = "",
            string requestArtifactIdentity = "",
            string requestTrainingRunIdentity = "",
            string requestModelIdentity = "",
            InferenceResultSource requestSource = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stopwatch = Stopwatch.StartNew();
            PythonModelSettings settings = requestSettings ?? CloneSettings(settingsAccessor());
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

            if (!string.Equals(JsonSerializer.Serialize(settings), JsonSerializer.Serialize(settingsAccessor()), StringComparison.Ordinal)
                || !string.Equals(requestModelIdentity, modelIdentityAccessor?.Invoke(settingsAccessor()) ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals(requestTrainingRunIdentity, trainingRunIdentityAccessor?.Invoke(settingsAccessor()) ?? string.Empty, StringComparison.Ordinal))
                return new YoloWorkerSmokeTestResult { ImagePath = imagePath, Summary = OpenVisionLanguageService.T("WpfInference.Run.SourceChanged") };

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
                cancellationToken.ThrowIfCancellationRequested();
                if (disposed)
                {
                    return new YoloWorkerSmokeTestResult
                    {
                        ImagePath = imagePath
                    };
                }

                if (completed.Reason == DetectionCandidateUpdateReason.StaleResultIgnored)
                {
                    string staleSummary = OpenVisionLanguageService.T("WpfInference.Run.StaleNotApplied");
                    view.AppendLog(staleSummary);
                    return new YoloWorkerSmokeTestResult
                    {
                        Succeeded = false,
                        Summary = staleSummary,
                        ImagePath = imagePath,
                        Errors = new[] { staleSummary }
                    };
                }

                if (completed.Reason == DetectionCandidateUpdateReason.RequestFailed)
                {
                    string failureSummary = string.IsNullOrWhiteSpace(completed.ErrorMessage) ? "추론 실행 실패." : completed.ErrorMessage;
                    return new YoloWorkerSmokeTestResult
                    {
                        Succeeded = false,
                        Summary = failureSummary,
                        ImagePath = imagePath,
                        Errors = new[] { failureSummary }
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

                if (!string.Equals(JsonSerializer.Serialize(settings), JsonSerializer.Serialize(settingsAccessor()), StringComparison.Ordinal)
                    || !string.Equals(requestModelIdentity, modelIdentityAccessor?.Invoke(settingsAccessor()) ?? string.Empty, StringComparison.Ordinal)
                    || !string.Equals(requestTrainingRunIdentity, trainingRunIdentityAccessor?.Invoke(settingsAccessor()) ?? string.Empty, StringComparison.Ordinal)
                    || !string.Equals(requestImageIdentity, ReadContentIdentity(imagePath), StringComparison.Ordinal)
                    || !string.Equals(requestArtifactIdentity, ReadContentIdentity(settings.WeightsPath), StringComparison.Ordinal))
                    return new YoloWorkerSmokeTestResult { ImagePath = imagePath, Summary = OpenVisionLanguageService.T("WpfInference.Run.SourceChanged") };
                IReadOnlyList<DefectInfo> defects = detectionResults.GetLastDefects();
                if (view.IsCurrentImage?.Invoke(imagePath) == false)
                {
                    string staleSummary = OpenVisionLanguageService.T("WpfInference.Run.StaleNotApplied");
                    view.AppendLog(staleSummary);
                    return new YoloWorkerSmokeTestResult
                    {
                        Succeeded = false,
                        Summary = staleSummary,
                        ImagePath = imagePath,
                        Errors = new[] { staleSummary }
                    };
                }
                IReadOnlyList<DefectInfo> rawDefects = completed.RawResult?.Defects ?? defects;
                IReadOnlyList<YoloWorkerSmokeCandidate> candidates = rawDefects
                    .Select((defect, index) =>
                    {
                        YoloWorkerSmokeCandidate candidate = CandidateReviewPresentationService.FromDefect(defect, index + 1);
                        candidate.ClassId = defect.ClassId;
                        return candidate;
                    })
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
                    Candidates = candidates,
                    CaptureConfidenceFloor = completed.RawResult?.CaptureConfidenceFloor,
                    CaptureAnomalyScore = completed.RawResult?.CaptureAnomalyScore,
                    HasCompletePredictions = completed.RawResult?.HasCompletePredictions == true,
                    CaptureCandidateLimit = completed.RawResult?.CaptureCandidateLimit
                };

                if (applyToCanvas)
                {
                    IReadOnlyList<YoloWorkerSmokeCandidate> displayCandidates = result.Candidates.Any(candidate => candidate?.ImageLevel == true)
                        ? defects.Select((defect, index) => CandidateReviewPresentationService.FromDefect(defect, index + 1)).ToArray()
                        : CandidateReviewStateService.BuildCandidatePresentation(result, supportedClassesAccessor?.Invoke(), requestSource);
                    view.ApplyCandidates(displayCandidates, result.Succeeded);
                    if (view.SetPythonCompletedStatus != null) view.SetPythonCompletedStatus(modelSourceText, result.CandidateCount);
                    else view.SetPythonStatus(InferenceStatusPresentationService.BuildWorkerPythonCompletedStatus(modelSourceText, result.CandidateCount));
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

        private static string ReadContentIdentity(string path)
        {
            try { return string.IsNullOrWhiteSpace(path) || !File.Exists(path) ? string.Empty : HashingService.ComputeFileSha256(path, lowerCase: true); }
            catch (IOException) { return string.Empty; }
            catch (UnauthorizedAccessException) { return string.Empty; }
        }
    }

    // Data-only UI ports: the execution owner never receives a Window/control.
    // PrepareCanvasImage preserves same-image manual labels in the Shell adapter.
    public sealed class ImageDetectionCallbacks
    {
        public Func<string, bool, DrawingSize?> PrepareCanvasImage { get; init; }
        public Action<IReadOnlyList<YoloWorkerSmokeCandidate>, bool> ApplyCandidates { get; init; }
        public Func<bool> IsResultOnly { get; init; }
        public Action<YoloWorkerSmokeTestResult> RecordResult { get; init; }
        public Func<string, bool> IsCurrentImage { get; init; }
        public Action RefreshActions { get; init; }
        public Action<string> SetPythonStatus { get; init; }
        public Action<string, int> SetPythonCompletedStatus { get; init; }
        public Action<string, bool> SetCommandStatus { get; init; }
        public Action<string, bool, bool> SetInferenceStatus { get; init; }
        public Action<string> AppendLog { get; init; }
    }
}
