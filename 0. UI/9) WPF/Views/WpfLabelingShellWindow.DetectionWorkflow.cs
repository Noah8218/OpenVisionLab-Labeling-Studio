using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MvcVisionSystem._3._Communication.TCP;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingSize = System.Drawing.Size;
using OpenVisionLab.ImageCanvas.ViewModels;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using OpenVisionLab.Wpf.MessageDialogs;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingPoint = System.Drawing.Point;

namespace MvcVisionSystem
{
    // Responsibility group: detection execution, result application, and template matching adapter.
    // These members remain WPF Window adapters; independent policy belongs in services.
    public partial class WpfLabelingShellWindow : IWpfTemplateMatchingAutoLabelHost
    {
        #region DetectionExecution
        // Detection execution is kept apart from panel wiring so worker latency, fallback, and canvas update paths can be audited in one place.
        private async Task RunInteractiveDetectionAsync(string imagePath = "", bool allowSmokeFallback = false)
        {
            if (isApplicationCloseApproved || isDetecting || isBatchDetectionRunning)
            {
                return;
            }

            EnsureProjectSettings();
            isDetecting = true;
            CancellationTokenSource cancellation = new CancellationTokenSource();
            interactiveDetectionCts = cancellation;
            CancellationToken cancellationToken = cancellation.Token;
            UpdateYoloCommandButtons();
            UpdateCandidateActionState();
            SetYoloCommandStatus(InferenceStatusPresentationService.BuildInteractivePreparingCommandStatus(), isBusy: true);
            SetGlobalInferenceStatus(InferenceStatusPresentationService.BuildInteractivePreparingInferenceStatus(), isBusy: true);
            SetPythonStatus("\uCD94\uB860: \uC900\uBE44 \uC911");
            var totalStopwatch = Stopwatch.StartNew();
            try
            {
                string targetImagePath = detectionTargetService.ResolveInteractiveTargetPath(
                    imagePath,
                    activeImagePath,
                    global.Data.ProjectSettings.PythonModel);
                string inferencePath = "worker";
                YoloWorkerSmokeTestResult result = await RunWorkerDetectionForImageAsync(
                        targetImagePath,
                        applyToCanvas: true,
                        cancellationToken,
                        YoloRuntimePresentationService.GetInteractiveWorkerConnectTimeoutMilliseconds(
                            global.Data?.ProjectSettings?.PythonModel?.DetectionTimeoutSeconds ?? 30,
                            global.Data?.ProjectSettings?.PythonModel?.AutoStartClient != false,
                            allowSmokeFallback))
                    .ConfigureAwait(true);
                if (isApplicationCloseApproved)
                {
                    return;
                }

                if (!result.Succeeded && allowSmokeFallback)
                {
                    AppendLog($"\uCD94\uB860 \uC2E4\uD328, \uD14C\uC2A4\uD2B8 \uACBD\uB85C\uB85C \uC804\uD658: {Path.GetFileName(targetImagePath)}");
                    inferencePath = "smoke fallback";
                    result = await RunDetectionForImageAsync(targetImagePath, applyToCanvas: true, cancellationToken)
                        .ConfigureAwait(true);
                    if (isApplicationCloseApproved)
                    {
                        return;
                    }
                }

                string elapsed = YoloRuntimePresentationService.FormatElapsed(totalStopwatch.Elapsed);
                string inferencePathText = YoloRuntimePresentationService.FormatInferencePath(inferencePath);
                SetYoloCommandStatus(
                    InferenceStatusPresentationService.BuildInteractiveCompletionCommandStatus(result, elapsed),
                    isBusy: false);
                SetGlobalInferenceStatus(
                    InferenceStatusPresentationService.BuildInteractiveCompletionInferenceStatus(result, elapsed),
                    isBusy: false,
                    isWarning: !result.Succeeded);
                AppendLog(InferenceStatusPresentationService.BuildInteractiveCompletionLog(result, elapsed, inferencePathText));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || isApplicationCloseApproved)
            {
                if (!isApplicationCloseApproved)
                {
                    AppendLog("추론이 취소되었습니다.");
                }
            }
            finally
            {
                if (ReferenceEquals(interactiveDetectionCts, cancellation))
                {
                    interactiveDetectionCts = null;
                }

                cancellation.Dispose();
                isDetecting = false;
                if (!isApplicationCloseApproved)
                {
                    UpdateYoloCommandButtons();
                    UpdateCandidateActionState();
                }
            }
        }

        private async Task<YoloWorkerSmokeTestResult> RunDetectionForImageAsync(
            string imagePath,
            bool applyToCanvas,
            CancellationToken cancellationToken)
        {
            if (isApplicationCloseApproved)
            {
                return new YoloWorkerSmokeTestResult
                {
                    ImagePath = imagePath ?? string.Empty
                };
            }

            cancellationToken.ThrowIfCancellationRequested();

            var stopwatch = Stopwatch.StartNew();
            EnsureProjectSettings();
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                AppendLog($"검출 이미지 없음: {imagePath}");
                return new YoloWorkerSmokeTestResult
                {
                    Succeeded = false,
                    Summary = "검출 이미지를 찾지 못했습니다.",
                    ImagePath = imagePath ?? string.Empty,
                    Errors = new[] { $"검출 이미지를 찾지 못했습니다: {imagePath}" }
                };
            }

            if (applyToCanvas && !string.Equals(imagePath, activeImagePath, StringComparison.OrdinalIgnoreCase))
            {
                TryLoadImage(imagePath);
            }

            SetPythonStatus("\uCD94\uB860: \uD14C\uC2A4\uD2B8 \uC2E4\uD589 \uC911");
            AppendLog($"\uD14C\uC2A4\uD2B8 \uCD94\uB860 \uC2DC\uC791: {Path.GetFileName(imagePath)}");
            YoloWorkerSmokeTestResult result = await YoloWorkerSmokeTestService
                .RunAsync(global.Data.ProjectSettings.PythonModel, imagePath, cancellationToken)
                .ConfigureAwait(true);
            if (isApplicationCloseApproved)
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
                    && File.Exists(result.ImagePath)
                    && !string.Equals(result.ImagePath, activeImagePath, StringComparison.OrdinalIgnoreCase))
                {
                    TryLoadImage(result.ImagePath);
                }

                ApplyDetectionCandidates(result.Candidates, result.Succeeded);
                SetPythonStatus(detectionResultPresentationService.BuildSmokeStatus(result));
                foreach (string error in result.Errors)
                {
                    AppendLog($"- {error}");
                }
            }

            AppendLog(result.Summary);
            AppendLog($"\uD14C\uC2A4\uD2B8 \uCD94\uB860 \uC2DC\uAC04: {YoloRuntimePresentationService.FormatElapsed(stopwatch.Elapsed)}");
            return result;
        }
        #endregion

        #region DetectionWorkerExecution
        // Worker detection orchestration is kept away from result application to make UI stalls easier to profile.
        private async Task<YoloWorkerSmokeTestResult> RunWorkerDetectionForImageAsync(
            string imagePath,
            bool applyToCanvas,
            CancellationToken cancellationToken,
            int connectTimeoutMilliseconds = -1,
            bool workerReadyAlreadyChecked = false)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stopwatch = Stopwatch.StartNew();
            EnsureProjectSettings();
            string modelSourceText = InferenceStatusPresentationService.BuildRuntimeModelLabel(
                global.Data?.ProjectSettings?.PythonModel);
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
                    Summary = InferenceStatusPresentationService.BuildWorkerImageLoadFailureSummary(),
                    ImagePath = imagePath,
                    Errors = new[] { InferenceStatusPresentationService.BuildWorkerImageLoadFailureError(imagePath) }
                };
            }

            if (applyToCanvas)
            {
                requestImageSize = activeImageSize;
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
                    global.Data?.ProjectSettings?.PythonModel?.DetectionTimeoutSeconds ?? 30);
            SetGlobalInferenceStatus(InferenceStatusPresentationService.BuildWorkerPreparingInferenceStatus(applyToCanvas, imagePath), isBusy: true);
            SetPythonStatus("\uCD94\uB860: \uC5F0\uACB0 \uD655\uC778 \uC911");
            SetYoloCommandStatus(InferenceStatusPresentationService.BuildWorkerPreparingCommandStatus(), isBusy: true);
            bool ready = workerReadyAlreadyChecked
                ? true
                : await global.ModelRuntime.EnsurePythonModelClientReadyAsync(timeoutMilliseconds, cancellationToken).ConfigureAwait(true);
            if (isApplicationCloseApproved)
            {
                return new YoloWorkerSmokeTestResult
                {
                    ImagePath = imagePath
                };
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (!ready)
            {
                SetGlobalInferenceStatus(InferenceStatusPresentationService.BuildWorkerConnectionFailureInferenceStatus(), isBusy: false, isWarning: true);
                SetPythonStatus("\uCD94\uB860: \uC5F0\uACB0 \uC2E4\uD328");
                AppendLog(InferenceStatusPresentationService.BuildWorkerConnectionFailureLog(
                    YoloRuntimePresentationService.FormatElapsed(stopwatch.Elapsed)));
                string workerFailureText = YoloRuntimePresentationService.BuildPythonWorkerFailureText(
                    global.GetPythonCommunicationStatusSnapshot(),
                    global.ModelRuntime.PythonClientProcess?.LastError);
                return new YoloWorkerSmokeTestResult
                {
                    Succeeded = false,
                    Summary = workerFailureText,
                    ImagePath = imagePath,
                    Errors = new[] { workerFailureText }
                };
            }

            using var completionWaiter = new DetectionWorkerCompletionWaiter(
                global.DetectionResults,
                imagePath,
                cancellationToken);

            try
            {
                SetGlobalInferenceStatus(InferenceStatusPresentationService.BuildWorkerRunningInferenceStatus(applyToCanvas, imagePath), isBusy: true);
                SetPythonStatus("\uCD94\uB860: \uC2E4\uD589 \uC911");
                AppendLog(InferenceStatusPresentationService.BuildWorkerStartLog(imagePath, modelSourceText));
                SetYoloCommandStatus(InferenceStatusPresentationService.BuildWorkerRequestCommandStatus(), isBusy: true);
                bool started = applyToCanvas
                    ? global.ModelRuntime.DetectionWorkflow.TryStartCurrentImageDetection(
                        global.Data,
                        global.ModelRuntime.DeepLearning,
                        global.DetectionTransport,
                        () => true)
                    : global.ModelRuntime.DetectionWorkflow.TryStartImagePathDetection(
                        global.Data,
                        global.ModelRuntime.DeepLearning,
                        global.DetectionTransport,
                        imagePath,
                        requestImageSize,
                        () => true);
                if (!started)
                {
                    SetGlobalInferenceStatus(InferenceStatusPresentationService.BuildWorkerRequestFailureInferenceStatus(), isBusy: false, isWarning: true);
                    SetPythonStatus("\uCD94\uB860: \uC694\uCCAD \uC2E4\uD328");
                    return new YoloWorkerSmokeTestResult
                    {
                        Succeeded = false,
                        Summary = InferenceStatusPresentationService.BuildWorkerRequestFailureSummary(global.GetPythonCommunicationStatusSnapshot().LastError),
                        ImagePath = imagePath
                    };
                }

                DetectionCandidatesUpdatedEventArgs completed = await completionWaiter.Completion.ConfigureAwait(true);
                if (isApplicationCloseApproved)
                {
                    return new YoloWorkerSmokeTestResult
                    {
                        ImagePath = imagePath
                    };
                }

                if (completed.Reason == DetectionCandidateUpdateReason.RequestTimedOut)
                {
                    SetGlobalInferenceStatus(InferenceStatusPresentationService.BuildWorkerTimedOutInferenceStatus(), isBusy: false, isWarning: true);
                    string timeoutSummary = InferenceStatusPresentationService.BuildWorkerTimedOutSummary();
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
                    ApplyDetectionCandidates(result.Candidates, result.Succeeded);
                    SetPythonStatus(InferenceStatusPresentationService.BuildWorkerPythonCompletedStatus(modelSourceText, result.CandidateCount));
                }

                AppendLog(InferenceStatusPresentationService.BuildWorkerElapsedLog(
                    YoloRuntimePresentationService.FormatElapsed(stopwatch.Elapsed),
                    modelSourceText));
                return result;
            }
            catch (OperationCanceledException)
            {
                if (isApplicationCloseApproved)
                {
                    return new YoloWorkerSmokeTestResult
                    {
                        ImagePath = imagePath
                    };
                }

                SetGlobalInferenceStatus(InferenceStatusPresentationService.BuildWorkerCanceledInferenceStatus(), isBusy: false, isWarning: true);
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
        #endregion

        #region DetectionResultApplication
        // Detection result application owns canvas/list updates; execution code should only return worker results.
        private void ApplyDetectionCandidates(IReadOnlyList<YoloWorkerSmokeCandidate> candidates, bool succeeded)
            => ApplyDetectionCandidatesCore(candidates, succeeded, clearConfirmed: true);

        private void ApplyDetectionCandidatesPreservingConfirmed(
            IReadOnlyList<YoloWorkerSmokeCandidate> candidates,
            bool succeeded)
            => ApplyDetectionCandidatesCore(candidates, succeeded, clearConfirmed: false);

        private void ApplyDetectionCandidatesCore(
            IReadOnlyList<YoloWorkerSmokeCandidate> candidates,
            bool succeeded,
            bool clearConfirmed)
        {
            int loadedCount = candidateReviewState.LoadPendingCandidates(candidates, clearConfirmed);
            CandidateReviewViewModel?.ClearReviewHistory();

            ApplyCanvasDisplayMode(WpfCanvasDisplayMode.InferenceOnly, redraw: false, logChange: false);
            RefreshCandidateList();
            RefreshObjectList();
            RedrawReviewRois();
            SetActiveImageDetectionStatus(loadedCount, succeeded);
            ApplyActiveAnomalyClassification(candidates);
            AddCandidateReviewHistory(detectionResultPresentationService.BuildCandidateLoadHistory(loadedCount, succeeded, GetCandidateConfidenceFilter()));
            ShowCandidateReviewWorkflowView();
            RefreshCanvasWorkflowContext();

            if (!candidateReviewState.HasPendingCandidates)
            {
                AppendLog("AI 후보가 없습니다.");
                return;
            }

            AppendLog($"AI 후보 로드: {loadedCount}개");
        }
        private void AddCandidateReviewHistory(string message)
        {
            CandidateReviewViewModel?.AddReviewHistory(message);
        }

        private void RedrawReviewRois()
        {
            EnsureManualRoiMetadataCount();
            RefreshCanvasLayerVisibilityState();
            bool showLabels = ShouldShowLabelOverlays();
            bool showInference = ShouldShowInferenceOverlays();
            using (MainCanvasViewModel.ImageViewer.SuppressRefresh())
            {
                MainCanvasViewModel.ClearRois();
                if (showLabels)
                {
                    for (int i = 0; i < manualRois.Count; i++)
                    {
                        DrawingRectangle roi = manualRois[i];
                        WpfObjectSessionState sessionState = GetManualRoiSessionState(i);
                        if (roi.IsEmpty || sessionState.IsHidden)
                        {
                            manualRoiOverlayIds[i] = string.Empty;
                            continue;
                        }

                        string className = GetManualRoiClassName(i);
                        var overlay = MainCanvasViewModel.AddInitialRoi(
                            roi,
                            ObjectReviewPresentationService.GetManualRoiShapeKind(manualRoiShapeKinds, i),
                            GetClassDrawColor(className),
                            className);
                        manualRoiOverlayIds[i] = overlay?.UniqueId ?? string.Empty;
                        var overlayItem = MainCanvasViewModel.ImageViewer
                            .GetCanvasOverlayManager()
                            .GetOverlayByUniqueId(manualRoiOverlayIds[i]);
                        if (overlayItem != null)
                        {
                            overlayItem.IsControlLock = sessionState.IsLocked;
                            overlayItem.IsMoveLock = sessionState.IsPinned;
                        }
                    }

                    foreach (YoloWorkerSmokeCandidate candidate in confirmedDetectionCandidates)
                    {
                        DrawingRectangle bounds = CandidateReviewPresentationService.ClipCandidateBounds(candidate, activeImageSize);
                        if (!bounds.IsEmpty)
                        {
                            MainCanvasViewModel.AddInitialRoi(bounds, OpenVisionLab.ImageCanvas.CanvasShapes.CanvasRoiShapeKind.Rectangle, GetClassDrawColor(candidate.ClassName), candidate.ClassName);
                        }
                    }
                }

                if (showInference)
                {
                    MainCanvasViewModel.SetDetectionOverlays(
                        CandidateReviewPresentationService.BuildDetectionOverlays(
                            pendingDetectionCandidates,
                            GetSelectedCandidate(),
                            activeImageSize,
                            IsCandidateConfirmable));
                }
                else
                {
                    MainCanvasViewModel.SetDetectionOverlays(Array.Empty<RoiImageCanvasDetectionOverlay>());
                }

                if (showLabels)
                {
                    RefreshPolygonOverlays();
                }
                else
                {
                    ClearSegmentationOverlays();
                }
            }

            MainCanvasViewModel.ImageViewer.RefreshGL();
        }
        #endregion

        #region TemplateMatchingCommands
        bool IWpfTemplateMatchingAutoLabelHost.IsAutoLabelBusy => isBatchDetectionRunning || isDetecting;

        bool IWpfTemplateMatchingAutoLabelHost.IsAutoLabelCloseApproved => isApplicationCloseApproved;

        bool IWpfTemplateMatchingAutoLabelHost.HasActiveAutoLabelImage => activeImageBitmap != null && !activeImageSize.IsEmpty;

        DrawingBitmap IWpfTemplateMatchingAutoLabelHost.ActiveAutoLabelImage => activeImageBitmap;

        string IWpfTemplateMatchingAutoLabelHost.ActiveAutoLabelImagePath => activeImagePath;

        LabelingProjectData IWpfTemplateMatchingAutoLabelHost.AutoLabelData => global.Data;

        int IWpfTemplateMatchingAutoLabelHost.MaximumTemplateMatchingCandidateCount
        {
            get
            {
                int configured = global.Data?.ProjectSettings?.PythonModel?.MaximumDetectionCandidates ?? 20;
                return Math.Clamp(configured, 1, 200);
            }
        }

        bool IWpfTemplateMatchingAutoLabelHost.TryResolveTemplateMatchingSource(out DrawingRectangle templateBounds, out string className)
        {
            return templateMatchingSourceService.TryResolveTemplateMatchingSource(
                CreateTemplateMatchingSourceSnapshot(),
                out templateBounds,
                out className);
        }

        bool IWpfTemplateMatchingAutoLabelHost.TryResolveTemplateMatchingSourceSegment(
            out IReadOnlyList<DrawingPoint> points,
            out IReadOnlyList<IReadOnlyList<DrawingPoint>> cutouts)
        {
            return templateMatchingSourceService.TryResolveTemplateMatchingSourceSegment(
                CreateTemplateMatchingSourceSnapshot(),
                out points,
                out cutouts);
        }

        bool IWpfTemplateMatchingAutoLabelHost.TryResolveTemplateMatchingSourceMask(
            out byte[] maskData,
            out System.Drawing.Size maskSize,
            out DrawingRectangle maskBounds)
        {
            return templateMatchingSourceService.TryResolveTemplateMatchingSourceMask(
                CreateTemplateMatchingSourceSnapshot(),
                out maskData,
                out maskSize,
                out maskBounds);
        }

        private TemplateMatchingSourceSnapshot CreateTemplateMatchingSourceSnapshot()
        {
            TryGetSelectedObjectReviewItem(out WpfObjectReviewItemRef selected);
            return new TemplateMatchingSourceSnapshot(
                selected,
                manualRois,
                manualRoiClassNames,
                manualSegments);
        }

        LabelClass IWpfTemplateMatchingAutoLabelHost.EnsureAutoLabelClassItem(string className)
        {
            return classCatalogWorkflowService.EnsureClassItem(global.Data, className);
        }

        IReadOnlyList<WpfImageQueueItem> IWpfTemplateMatchingAutoLabelHost.GetVisibleAutoLabelQueueItems()
        {
            return GetVisibleQueueItems();
        }

        IReadOnlyList<WpfImageQueueItem> IWpfTemplateMatchingAutoLabelHost.GetAllAutoLabelQueueItems()
        {
            return imageQueueItems.ToList();
        }

        IReadOnlyList<WpfImageQueueItem> IWpfTemplateMatchingAutoLabelHost.BuildAutoLabelBatchQueue(IEnumerable<WpfImageQueueItem> items)
        {
            return detectionTargetService.BuildBatchQueue(items);
        }

        void IWpfTemplateMatchingAutoLabelHost.AppendAutoLabelLog(string message)
        {
            AppendLog(message);
        }

        void IWpfTemplateMatchingAutoLabelHost.ShowAutoLabelGuide(string title, string message)
        {
            SetGlobalInferenceStatus(title ?? string.Empty, isBusy: false, isWarning: true);
            WpfMessageDialog.ShowInfo(
                this,
                string.IsNullOrWhiteSpace(title) ? "\uD15C\uD50C\uB9BF \uC548\uB0B4" : title,
                message ?? string.Empty,
                "\uD655\uC778");
        }

        int IWpfTemplateMatchingAutoLabelHost.ApplyAutoLabelCandidates(
            IReadOnlyList<YoloWorkerSmokeCandidate> candidates,
            bool succeeded,
            DrawingRectangle? sourceSegmentBounds,
            IReadOnlyList<DrawingPoint> sourceSegmentPoints,
            IReadOnlyList<IReadOnlyList<DrawingPoint>> sourceSegmentCutouts,
            byte[] sourceMaskData,
            System.Drawing.Size sourceMaskSize,
            DrawingRectangle sourceMaskBounds)
        {
            IReadOnlyList<YoloWorkerSmokeCandidate> safeCandidates = candidates ?? Array.Empty<YoloWorkerSmokeCandidate>();
            if (!succeeded)
            {
                ApplyDetectionCandidates(safeCandidates, succeeded: false);
                return 0;
            }

            if (succeeded && safeCandidates.Count == 0)
            {
                ApplyTemplateNoCandidateResult();
                return 0;
            }

            return ApplyTemplateLabelCandidates(
                safeCandidates,
                sourceSegmentBounds,
                sourceSegmentPoints,
                sourceSegmentCutouts,
                sourceMaskData,
                sourceMaskSize,
                sourceMaskBounds);
        }

        void IWpfTemplateMatchingAutoLabelHost.SetAutoLabelPythonStatus(string text)
        {
            SetPythonStatus(text);
        }

        void IWpfTemplateMatchingAutoLabelHost.SetAutoLabelCommandStatus(string text, bool isBusy)
        {
            SetYoloCommandStatus(text, isBusy);
        }

        void IWpfTemplateMatchingAutoLabelHost.SetAutoLabelGlobalInferenceStatus(string text, bool isBusy, bool isWarning)
        {
            SetGlobalInferenceStatus(text, isBusy, isWarning);
        }

        CancellationToken IWpfTemplateMatchingAutoLabelHost.StartAutoLabelBatch(int totalCount, string scopeText)
        {
            batchDetectionCts?.Cancel();
            batchDetectionCts?.Dispose();
            batchDetectionCts = new CancellationTokenSource();
            isBatchDetectionRunning = true;
            batchDetectionTotalCount = Math.Max(0, totalCount);
            batchDetectionCompletedCount = 0;
            UpdateBatchDetectionControls(scopeText, string.Empty);
            UpdateYoloCommandButtons();
            return batchDetectionCts.Token;
        }

        void IWpfTemplateMatchingAutoLabelHost.MarkAutoLabelBatchItemRequested(WpfImageQueueItem item)
        {
            if (item == null)
            {
                return;
            }

            string imageName = Path.GetFileNameWithoutExtension(item.ImagePath);
            ApplyReviewStatusToItem(item, imageQualityReviewWorkflowService.SetDetectionRequested(item.ImagePath, imageName));
        }

        void IWpfTemplateMatchingAutoLabelHost.UpdateAutoLabelBatchProgress(
            string scopeText,
            string currentFileName,
            int completedCount,
            int totalCount)
        {
            batchDetectionCompletedCount = Math.Max(0, completedCount);
            batchDetectionTotalCount = Math.Max(0, totalCount);
            UpdateBatchDetectionControls(scopeText, currentFileName);
        }

        void IWpfTemplateMatchingAutoLabelHost.ApplyAutoLabelBatchResult(
            WpfImageQueueItem item,
            TemplateMatchingBatchAutoLabelItemResult result,
            bool saveReviewStatus)
        {
            if (item == null || result == null)
            {
                return;
            }

            YoloImageReviewStatus status;
            string imageName = Path.GetFileNameWithoutExtension(item.ImagePath);
            if (result.Saved)
            {
                status = imageQualityReviewWorkflowService.RefreshLabelStatusAndReviewState(
                    item.ImagePath,
                    result.ImageSize,
                    global.Data,
                    hasActiveCandidates: false)
                    ?? imageQualityReviewWorkflowService.MarkConfirmed(item.ImagePath, imageName);
            }
            else if (result.NoCandidate)
            {
                status = imageQualityReviewWorkflowService.SetDetectionNoCandidates(item.ImagePath, imageName);
            }
            else
            {
                status = imageQualityReviewWorkflowService.SetDetectionFailed(item.ImagePath, imageName, result.Message);
            }

            ApplyReviewStatusToItem(item, status);
            if (saveReviewStatus)
            {
                imageQualityReviewWorkflowService.SaveReviewStatus(global.Data);
            }

            UpdateImageQueueStatusText();
        }

        void IWpfTemplateMatchingAutoLabelHost.SaveAutoLabelReviewStatus()
        {
            imageQualityReviewWorkflowService.SaveReviewStatus(global.Data);
        }

        void IWpfTemplateMatchingAutoLabelHost.CompleteAutoLabelBatch(
            bool canceled,
            int completedCount,
            int totalCount,
            string scopeText)
        {
            isBatchDetectionRunning = false;
            batchDetectionCompletedCount = Math.Max(0, completedCount);
            batchDetectionTotalCount = Math.Max(0, totalCount);
            imageQueueView?.Refresh();
            RefreshActiveImageQueueStatus(hasActiveCandidates: pendingDetectionCandidates.Count > 0);
            UpdateBatchDetectionControls(canceled ? "canceled" : "complete", string.Empty);
            UpdateYoloCommandButtons();
        }

        void IWpfTemplateMatchingAutoLabelHost.NotifyAutoLabelDataChanged()
        {
            global.System?.UpdateData();
        }

        Task IWpfTemplateMatchingAutoLabelHost.YieldAutoLabelBatchFrameAsync(CancellationToken token)
        {
            return YieldBatchDetectionResultFrameAsync(token);
        }

        private void ApplyTemplateNoCandidateResult()
        {
            candidateReviewState.LoadPendingCandidates(Array.Empty<YoloWorkerSmokeCandidate>(), clearConfirmed: true);
            CandidateReviewViewModel?.ClearReviewHistory();
            RefreshCandidateList();
            RefreshObjectList();
            RedrawReviewRois();
            AddCandidateReviewHistory("템플릿 초안 없음: 기준 박스는 결과에서 제외되며, 현재 이미지에서 추가 위치를 찾지 못했습니다.");
            AppendLog("Template matching no candidate: source box excluded, no extra current-image candidate.");

            if (!string.IsNullOrWhiteSpace(activeImagePath) && !activeImageSize.IsEmpty)
            {
                RefreshActiveImageQueueStatus(hasActiveCandidates: false);
            }

            RefreshImageQueueViewAfterItemStateChange();
            UpdateImageQueueStatusText();
        }

        private int ApplyTemplateLabelCandidates(
            IReadOnlyList<YoloWorkerSmokeCandidate> candidates,
            DrawingRectangle? sourceSegmentBounds,
            IReadOnlyList<DrawingPoint> sourceSegmentPoints,
            IReadOnlyList<IReadOnlyList<DrawingPoint>> sourceSegmentCutouts,
            byte[] sourceMaskData,
            System.Drawing.Size sourceMaskSize,
            DrawingRectangle sourceMaskBounds)
        {
            if (activeImageBitmap == null || activeImageSize.IsEmpty)
            {
                return 0;
            }

            var labelsToAdd = new List<(YoloWorkerSmokeCandidate Candidate, DrawingRectangle Bounds)>();
            foreach (YoloWorkerSmokeCandidate candidate in candidates ?? Array.Empty<YoloWorkerSmokeCandidate>())
            {
                DrawingRectangle bounds = CandidateReviewPresentationService.ClipCandidateBounds(candidate, activeImageSize);
                if (bounds.IsEmpty || IsTemplateLabelDuplicate(bounds, CandidateReviewPresenter.GetClassName(candidate), labelsToAdd.Select(item => item.Bounds)))
                {
                    continue;
                }

                labelsToAdd.Add((candidate, bounds));
            }

            if (labelsToAdd.Count == 0)
            {
                ApplyTemplateNoCandidateResult();
                return 0;
            }

            RegisterAnnotationHistoryBeforeChange("Template label");
            candidateReviewState.LoadPendingCandidates(Array.Empty<YoloWorkerSmokeCandidate>(), clearConfirmed: true);
            int addedCount;
            if (IsSegmentationDatasetPurposeActive())
            {
                string className = CandidateReviewPresenter.GetClassName(labelsToAdd[0].Candidate);
                LabelClass classItem = classCatalogWorkflowService.EnsureClassItem(global.Data, className);
                IReadOnlyDictionary<string, List<LabelingSegmentationObject>> segmentsByClass =
                    TemplateMatchingBatchAutoLabelService.BuildSegmentsByClass(
                        classItem,
                        className,
                        labelsToAdd.Select(item => item.Candidate).ToList(),
                        activeImageSize,
                        sourceSegmentBounds,
                        sourceSegmentPoints,
                        sourceSegmentCutouts,
                        sourceMaskData,
                        sourceMaskSize,
                        sourceMaskBounds);
                List<LabelingSegmentationObject> transferredSegments = segmentsByClass
                    .Values
                    .Where(items => items != null)
                    .SelectMany(items => items)
                    .Where(segment => segment != null)
                    .ToList();
                int nextZOrder = SegmentationZOrderService.GetNextZOrder(manualSegments);
                for (int index = 0; index < transferredSegments.Count; index++)
                {
                    transferredSegments[index].ZOrder = nextZOrder + index;
                }

                manualSegments.AddRange(transferredSegments);
                addedCount = transferredSegments.Count;
            }
            else
            {
                foreach ((YoloWorkerSmokeCandidate candidate, DrawingRectangle bounds) in labelsToAdd)
                {
                    string className = CandidateReviewPresenter.GetClassName(candidate);
                    classCatalogWorkflowService.EnsureClassItem(global.Data, className);
                    manualRois.Add(bounds);
                    manualRoiClassNames.Add(className);
                    manualRoiShapeKinds.Add(CanvasRoiShapeKind.Rectangle);
                    manualRoiOverlayIds.Add(string.Empty);
                }

                addedCount = labelsToAdd.Count;
            }

            if (addedCount == 0)
            {
                ApplyTemplateNoCandidateResult();
                return 0;
            }

            ApplyCanvasDisplayMode(WpfCanvasDisplayMode.LabelsOnly, redraw: false, logChange: false);
            RefreshCandidateList();
            RefreshObjectList();
            RedrawReviewRois();
            PopulateClassList();
            ShowSavedLabelsWorkflowView();
            SetModelStatus($"템플릿 라벨 초안 생성: {addedCount}개 / 위치 확인 후 라벨 저장");
            AddCandidateReviewHistory($"템플릿 라벨 초안 생성: {addedCount}개 / 저장 전 초안");
            AppendLog($"Template labels added: {addedCount}");
            RefreshImageQueueViewAfterItemStateChange();
            UpdateImageQueueStatusText();
            return addedCount;
        }

        private bool IsTemplateLabelDuplicate(
            DrawingRectangle bounds,
            string className,
            IEnumerable<DrawingRectangle> pendingBounds)
        {
            string normalizedClassName = ClassCatalogService.NormalizeClassName(className);
            foreach (DrawingRectangle pending in pendingBounds ?? Array.Empty<DrawingRectangle>())
            {
                if (CandidateReviewPresenter.CalculateIntersectionOverUnion(bounds, pending) >= 0.9D)
                {
                    return true;
                }
            }

            for (int i = 0; i < manualRois.Count; i++)
            {
                if (string.Equals(ClassCatalogService.NormalizeClassName(GetManualRoiClassName(i)), normalizedClassName, StringComparison.OrdinalIgnoreCase)
                    && CandidateReviewPresenter.CalculateIntersectionOverUnion(bounds, manualRois[i]) >= 0.9D)
                {
                    return true;
                }
            }

            foreach (LabelingSegmentationObject segment in manualSegments)
            {
                if (segment != null
                    && string.Equals(ClassCatalogService.NormalizeClassName(templateMatchingSourceService.GetManualSegmentClassName(segment)), normalizedClassName, StringComparison.OrdinalIgnoreCase)
                    && CandidateReviewPresenter.CalculateIntersectionOverUnion(bounds, segment.Bounds) >= 0.9D)
                {
                    return true;
                }
            }

            foreach (YoloWorkerSmokeCandidate confirmed in confirmedDetectionCandidates)
            {
                if (string.Equals(ClassCatalogService.NormalizeClassName(CandidateReviewPresenter.GetClassName(confirmed)), normalizedClassName, StringComparison.OrdinalIgnoreCase)
                    && CandidateReviewPresenter.CalculateIntersectionOverUnion(
                        bounds,
                        CandidateReviewPresentationService.ClipCandidateBounds(confirmed, activeImageSize)) >= 0.9D)
                {
                    return true;
                }
            }

            return false;
        }
        #endregion

    }
}
