using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingSize = System.Drawing.Size;
using OpenVisionLab.ImageCanvas.ViewModels;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using OpenVisionLab.Wpf.MessageDialogs;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingPoint = System.Drawing.Point;

namespace MvcVisionSystem
{
    // Responsibility group: inference runtime/UI adapters, result application and template matching adapter.
    // These members remain WPF Window adapters; independent policy belongs in services.
    public partial class WpfLabelingShellWindow : IWpfTemplateMatchingAutoLabelHost
    {
        #region DetectionExecution
        // PL-0036: runtime composition and visual projection only. Execution,
        // cancellation, completion subscription and fallback live in the service.
        private ImageDetectionWorkflowService CreateImageDetectionWorkflow()
        {
            return new ImageDetectionWorkflowService(
                () => { EnsureProjectSettings(); return global.Data.ProjectSettings.PythonModel; },
                () => global.DetectionResults,
                (timeout, token) => global.ModelRuntime.EnsurePythonModelClientReadyAsync(timeout, token),
                (currentImage, path, size) => currentImage
                    ? global.ModelRuntime.DetectionWorkflow.TryStartCurrentImageDetection(
                        global.Data, global.ModelRuntime.DeepLearning, global.DetectionTransport, () => true)
                    : global.ModelRuntime.DetectionWorkflow.TryStartImagePathDetection(
                        global.Data, global.ModelRuntime.DeepLearning, global.DetectionTransport, path, size, () => true),
                () => YoloRuntimePresentationService.BuildPythonWorkerFailureText(
                    global.GetPythonCommunicationStatusSnapshot(), global.ModelRuntime.PythonClientProcess?.LastError),
                () => global.GetPythonCommunicationStatusSnapshot().LastError);
        }

        private ImageDetectionCallbacks CreateImageDetectionCallbacks()
        {
            return new ImageDetectionCallbacks
            {
                PrepareCanvasImage = (path, populateQueue) =>
                {
                    // Same-image inference must preserve in-progress manual labels.
                    bool shouldLoadTargetImage = !string.Equals(path, activeImagePath, StringComparison.OrdinalIgnoreCase);
                    return !shouldLoadTargetImage || TryLoadImage(path, populateQueue: populateQueue)
                        ? activeImageSize : null;
                },
                ApplyCandidates = ApplyDetectionCandidates,
                RefreshActions = () => { UpdateYoloCommandButtons(); UpdateCandidateActionState(); },
                SetPythonStatus = SetPythonStatus,
                SetCommandStatus = (text, busy) => SetYoloCommandStatus(text, busy),
                SetInferenceStatus = (text, busy, warning) => SetGlobalInferenceStatus(text, busy, warning),
                AppendLog = AppendLog
            };
        }

        private Task RunInteractiveDetectionAsync(string imagePath = "", bool allowSmokeFallback = false)
        {
            if (isApplicationCloseApproved || batchDetectionWorkflowService.IsRunning) return Task.CompletedTask;
            return imageDetectionWorkflowService.RunInteractiveAsync(imagePath, activeImagePath, allowSmokeFallback, CreateImageDetectionCallbacks());
        }

        private Task<YoloWorkerSmokeTestResult> RunDetectionForImageAsync(string imagePath, bool applyToCanvas, CancellationToken cancellationToken)
        {
            return imageDetectionWorkflowService.RunSmokeAsync(imagePath, applyToCanvas, cancellationToken, CreateImageDetectionCallbacks());
        }

        private Task<YoloWorkerSmokeTestResult> RunWorkerDetectionForImageAsync(
            string imagePath,
            bool applyToCanvas,
            CancellationToken cancellationToken,
            int connectTimeoutMilliseconds = -1,
            bool workerReadyAlreadyChecked = false)
        {
            return imageDetectionWorkflowService.RunWorkerAsync(
                imagePath, applyToCanvas, cancellationToken, connectTimeoutMilliseconds, CreateImageDetectionCallbacks(), workerReadyAlreadyChecked);
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
        bool IWpfTemplateMatchingAutoLabelHost.IsAutoLabelBusy => batchDetectionWorkflowService.IsRunning || imageDetectionWorkflowService.IsDetecting;

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
            if (isApplicationCloseApproved || imageDetectionWorkflowService.IsDetecting) return new CancellationToken(canceled: true);
            BatchDetectionRun run = batchDetectionWorkflowService.TryBegin(totalCount, CaptureBatchReviewStatusSave());
            if (run == null) return new CancellationToken(canceled: true);
            UpdateBatchDetectionControls(scopeText, string.Empty);
            UpdateYoloCommandButtons();
            return run.Token;
        }

        void IWpfTemplateMatchingAutoLabelHost.MarkAutoLabelBatchItemRequested(WpfImageQueueItem item)
        {
            if (!imageQualityReviewWorkflowService.CanReview(global.Data)
                || (item != null && !ReferenceEquals(FindImageQueueItem(item.ImagePath), item))) return;

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
            UpdateBatchDetectionControls(scopeText, currentFileName);
        }

        void IWpfTemplateMatchingAutoLabelHost.ApplyAutoLabelBatchResult(
            WpfImageQueueItem item,
            TemplateMatchingBatchAutoLabelItemResult result,
            bool saveReviewStatus)
        {
            if (batchDetectionWorkflowService.Current?.CanApplyResult != true
                || !imageQualityReviewWorkflowService.CanReview(global.Data)
                || (item != null && !ReferenceEquals(FindImageQueueItem(item.ImagePath), item))) return;

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
            batchDetectionWorkflowService.Current.RecordResult();
            if (saveReviewStatus)
            {
                batchDetectionWorkflowService.Current.FlushReviewStatus();
            }

            UpdateImageQueueStatusText();
        }

        void IWpfTemplateMatchingAutoLabelHost.SaveAutoLabelReviewStatus()
        {
            batchDetectionWorkflowService.Current?.FlushReviewStatus();
        }

        void IWpfTemplateMatchingAutoLabelHost.CompleteAutoLabelBatch(
            bool canceled,
            int completedCount,
            int totalCount,
            string scopeText)
        {
            batchDetectionWorkflowService.Current?.Complete();
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
