using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.ViewModels;
using OpenVisionLab.Wpf.MessageDialogs;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CvMat = OpenCvSharp.Mat;
using DrawingBitmap = System.Drawing.Bitmap;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    // Responsibility group: image loading and queue catalog lifecycle.
    // These members remain WPF Window adapters; independent policy belongs in services.
    public partial class WpfLabelingShellWindow
    {
        #region ImageLoading
        // ponytail: tests may replace only the GPU upload while preserving the full image-load workflow.
        private Action<CvMat, string> imageViewerLoadOverride = null;

        // Image loading still coordinates shell state, but lives outside the event-heavy code-behind for easier diagnosis.
        public ImageLoadDiagnostics LastImageLoadDiagnostics => lastImageLoadDiagnostics;

        public bool TryLoadStartupSampleImage()
        {
            if (activeImageBitmap != null || !string.IsNullOrWhiteSpace(activeImagePath))
            {
                return true;
            }

            EnsureProjectSettings();
            string imagePath = YoloWorkerSmokeTestService.ResolveSmokeImagePath(global.Data.ProjectSettings.PythonModel);
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                SetDatasetStatus(imageLoadPresentationService.BuildStartupSampleMissingDatasetStatus());
                AppendLog(imageLoadPresentationService.BuildStartupSampleMissingLog());
                return false;
            }

            return TryLoadImage(imagePath, populateQueue: true, refreshQueueDetails: false);
        }

        public bool TryLoadImage(string imagePath, bool populateQueue = true, bool refreshQueueDetails = true, bool refreshActiveStatus = true, bool appendLoadLog = true)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                AppendLog(imageLoadPresentationService.BuildMissingImageLog(imagePath));
                return false;
            }

            if (imageViewerLoadOverride == null
                && !RuntimeDiagnosticsViewModel.EnsureViewerReadyForImageLoad(out string graphicsDetail))
            {
                SetDatasetStatus("이미지 뷰어 환경 확인 필요");
                AppendLog("이미지 열기 차단: " + graphicsDetail);
                if (IsLoaded && IsVisible)
                {
                    WpfMessageDialog.Show(this, new WpfMessageDialogOptions
                    {
                        Title = "이미지 뷰어를 시작할 수 없습니다",
                        Message = "현재 그래픽 환경이 라벨링 뷰어의 필수 기능을 지원하지 않습니다.",
                        Details =
                            graphicsDetail
                            + "\n\n설정/도구 > 진단/지원에서 환경 점검을 실행하거나 지원 자료를 만들어 확인하세요.",
                        Kind = WpfMessageDialogKind.Warning,
                        Buttons = WpfMessageDialogButtons.OK,
                        PrimaryButtonText = "확인",
                        MaxWidth = 680D
                    });
                }

                return false;
            }

            if (!TrySavePendingAnnotationsBeforeImageChange(imagePath))
            {
                return false;
            }

            shellTimers.DisplayAdjustmentRefresh.Stop();
            CancelFourPointBoxDraft(updateStatus: false);
            CancelPendingSegmentationRemoveUnderlying(updateStatus: false);
            CancelPendingPolygonVertexEdit(updateStatus: false);
            CancelPendingIntelligentScissors(updateStatus: false);
            CancelObjectGroupSelection(updateStatus: false);
            objectSessionStateService.Clear();
            objectMetadataStateService.Clear();
            Stopwatch loadStopwatch = Stopwatch.StartNew();
            long stepStartTicks = loadStopwatch.ElapsedTicks;
            bool cacheHit = false;
            double decodeMilliseconds = 0D;
            double canvasUploadMilliseconds = 0D;
            double canvasRefreshMilliseconds = 0D;
            double stateTransferMilliseconds = 0D;
            double annotationResetMilliseconds = 0D;
            double queuePopulateMilliseconds = 0D;
            double reviewRefreshMilliseconds = 0D;
            double preloadScheduleMilliseconds = 0D;
            DrawingBitmap workspaceBitmap = null;
            CvMat imageMat = null;
            try
            {
                if (imageDecodeCacheService.TryTake(imagePath, out CachedDecodedImage cachedImage))
                {
                    cacheHit = true;
                    workspaceBitmap = cachedImage.TakeBitmap();
                    imageMat = cachedImage.TakeMat();
                    cachedImage.Dispose();
                }
                else
                {
                    using CachedDecodedImage decodedImage = imageDecodeService.DecodeForCanvas(imagePath);
                    workspaceBitmap = decodedImage.TakeBitmap();
                    imageMat = decodedImage.TakeMat();
                }
                decodeMilliseconds = ImageLoadDiagnosticsService.TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);

                string imageName = Path.GetFileNameWithoutExtension(imagePath);
                using (MainCanvasViewModel.ImageViewer.SuppressRefresh())
                {
                    if (imageViewerLoadOverride != null)
                    {
                        imageViewerLoadOverride(imageMat, Path.GetFileName(imagePath));
                    }
                    else
                    {
                        MainCanvasViewModel.LoadImage(imageMat, Path.GetFileName(imagePath));
                    }

                    MainCanvasViewModel.ClearRois();
                    MainCanvasViewModel.SetDetectionOverlays(Array.Empty<RoiImageCanvasDetectionOverlay>());
                    MainCanvasViewModel.SetMaskOverlays(Array.Empty<RoiImageCanvasMaskOverlay>());
                    MainCanvasViewModel.SetPolygonOverlays(Array.Empty<RoiImageCanvasPolygonOverlay>());
                    MainCanvasViewModel.ClearMaskStrokePreview(refresh: false, clearTexture: true);
                }
                canvasUploadMilliseconds = ImageLoadDiagnosticsService.TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);
                if (imageViewerLoadOverride == null)
                {
                    MainCanvasViewModel.ImageViewer.RefreshGL();
                }

                canvasRefreshMilliseconds = ImageLoadDiagnosticsService.TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);

                activeImageBitmap?.Dispose();
                global.ImageWorkspace.SetActiveImage(imageName, imagePath, workspaceBitmap);
                LabelingImageSnapshot activeImage = global.ImageWorkspace.CaptureSnapshot();
                activeImageBitmap = activeImage.Image;
                workspaceBitmap = null;
                activeImagePath = activeImage.ImagePath;
                activeImageSize = activeImage.ImageSize;

                DisplayManager.ImageSrc = imageMat;
                imageMat = null;
                stateTransferMilliseconds = ImageLoadDiagnosticsService.TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);

                manualRois.Clear();
                manualRoiClassNames.Clear();
                manualRoiShapeKinds.Clear();
                manualRoiOverlayIds.Clear();
                manualSegments.Clear();
                CancelPendingSegmentationSplit(updateStatus: false);
                CancelPendingSegmentationHoleEdit(updateStatus: false);
                CancelPendingPolygonVertexEdit(updateStatus: false);
                CancelPendingIntelligentScissors(updateStatus: false);
                ClearQueuedMaskStrokeCommits();
                polygonAnnotationService.Reset();
                CancelMaskStrokePreviewCommitSwap();
                lastMaskStrokePoint = null;
                activeMaskStrokeInProgress = false;
                activeMaskStrokeActionName = string.Empty;
                activeMaskStrokeSegmentIndices.Clear();
                ResetMaskStrokeCommitBuffer();
                activeMaskStrokeNeedsFullObjectRefresh = false;
                candidateReviewState.ClearAll();
                smartMaskPromptSession.Reset();
                ClearAnnotationHistory();
                UpdateDetectionResultOverlay();
                int loadedSavedBoxCount = LoadSavedBoxAnnotationsForActiveImage(imagePath);
                int loadedSavedSegmentCount = LoadSavedSegmentationAnnotationsForActiveImage(imagePath);
                LoadObjectMetadataForActiveImage(imagePath);
                int loadedSavedAnnotationCount = loadedSavedBoxCount + loadedSavedSegmentCount;
                annotationResetMilliseconds = ImageLoadDiagnosticsService.TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);
                if (populateQueue)
                {
                    // ponytail: queued image navigation never substitutes its leaf folder for the operator-selected root.
                    string queueRoot = imageQueueItemsByPath.ContainsKey(imagePath)
                        && !string.IsNullOrWhiteSpace(currentImageRoot)
                        && Directory.Exists(currentImageRoot)
                        ? currentImageRoot
                        : Path.GetDirectoryName(imagePath);
                    PopulateImageQueue(queueRoot, imagePath, refreshQueueDetails);
                }
                queuePopulateMilliseconds = ImageLoadDiagnosticsService.TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);
                SetDatasetStatus(imageLoadPresentationService.BuildLoadedDatasetStatus(imagePath, activeImageSize));
                SetModelStatus(imageLoadPresentationService.BuildModelStatus(global.Data.ProjectSettings?.PythonModel?.WeightsPath));
                MarkAnnotationsSaved(imageLoadPresentationService.BuildAnnotationLoadedStatus());
                bool deferReviewRefresh = ShouldDeferImageLoadReviewRefresh(
                    populateQueue,
                    refreshQueueDetails,
                    refreshActiveStatus,
                    appendLoadLog);
                if (deferReviewRefresh)
                {
                    ScheduleImageLoadReviewRefresh(imagePath, refreshActiveStatus, refreshClassCatalog: false);
                }
                else
                {
                    RefreshImageLoadReviewState(refreshActiveStatus, refreshClassCatalog: true);
                }
                reviewRefreshMilliseconds = ImageLoadDiagnosticsService.TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);
                if (CanvasPanelViewModel.IsDisplayAdjustmentActive)
                {
                    ScheduleDisplayAdjustmentRefresh();
                }
                if (!appendLoadLog)
                {
                    PreloadAdjacentQueueImages(imagePath);
                    preloadScheduleMilliseconds = ImageLoadDiagnosticsService.TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);
                    RecordImageLoadDiagnostics(
                        imagePath,
                        cacheHit,
                        loadStopwatch.Elapsed.TotalMilliseconds,
                        decodeMilliseconds,
                        canvasUploadMilliseconds,
                        canvasRefreshMilliseconds,
                        stateTransferMilliseconds,
                        annotationResetMilliseconds,
                        queuePopulateMilliseconds,
                        reviewRefreshMilliseconds,
                        preloadScheduleMilliseconds);
                    return true;
                }
                AppendLog(loadedSavedAnnotationCount > 0
                    ? $"{imageLoadPresentationService.BuildLoadLog(imagePath)} / saved labels: {loadedSavedAnnotationCount}"
                    : imageLoadPresentationService.BuildLoadLog(imagePath));
                PreloadAdjacentQueueImages(imagePath);
                preloadScheduleMilliseconds = ImageLoadDiagnosticsService.TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);
                RecordImageLoadDiagnostics(
                    imagePath,
                    cacheHit,
                    loadStopwatch.Elapsed.TotalMilliseconds,
                    decodeMilliseconds,
                    canvasUploadMilliseconds,
                    canvasRefreshMilliseconds,
                    stateTransferMilliseconds,
                    annotationResetMilliseconds,
                    queuePopulateMilliseconds,
                    reviewRefreshMilliseconds,
                    preloadScheduleMilliseconds);
                return true;
            }
            catch (Exception ex)
            {
                workspaceBitmap?.Dispose();
                SetDatasetStatus(imageLoadPresentationService.BuildLoadFailureDatasetStatus());
                AppendLog(imageLoadPresentationService.BuildLoadFailureLog(ex.Message));
                return false;
            }
            finally
            {
                imageMat?.Dispose();
            }
        }

        private void ClearActiveImageAfterQueueReset()
        {
            shellTimers.DisplayAdjustmentRefresh.Stop();
            CancelPendingSegmentationRemoveUnderlying(updateStatus: false);
            CancelPendingPolygonVertexEdit(updateStatus: false);
            CancelPendingIntelligentScissors(updateStatus: false);
            CancelObjectGroupSelection(updateStatus: false);
            objectSessionStateService.Clear();
            objectMetadataStateService.Clear();
            activeImageBitmap?.Dispose();
            global.ImageWorkspace.SetActiveImage(string.Empty, string.Empty, null);
            LabelingImageSnapshot activeImage = global.ImageWorkspace.CaptureSnapshot();
            activeImageBitmap = activeImage.Image;
            activeImagePath = activeImage.ImagePath;
            activeImageSize = activeImage.ImageSize;
            DisplayManager.ImageSrc = null;

            manualRois.Clear();
            manualRoiClassNames.Clear();
            manualRoiShapeKinds.Clear();
            manualRoiOverlayIds.Clear();
            manualSegments.Clear();
            CancelPendingSegmentationSplit(updateStatus: false);
            CancelPendingSegmentationHoleEdit(updateStatus: false);
            CancelPendingPolygonVertexEdit(updateStatus: false);
            CancelPendingIntelligentScissors(updateStatus: false);
            ClearQueuedMaskStrokeCommits();
            polygonAnnotationService.Reset();
            CancelMaskStrokePreviewCommitSwap();
            lastMaskStrokePoint = null;
            activeMaskStrokeInProgress = false;
            activeMaskStrokeActionName = string.Empty;
            activeMaskStrokeSegmentIndices.Clear();
            ResetMaskStrokeCommitBuffer();
            activeMaskStrokeNeedsFullObjectRefresh = false;
            candidateReviewState.ClearAll();
            smartMaskPromptSession.Reset();
            ClearAnnotationHistory();

            MainCanvasViewModel.ClearImage();
            RefreshCandidateList();
            RefreshObjectList();
            SetAnnotationSaveStatusWaiting();
        }

        private static bool ShouldDeferImageLoadReviewRefresh(
            bool populateQueue,
            bool refreshQueueDetails,
            bool refreshActiveStatus,
            bool appendLoadLog)
            => !populateQueue
                && !refreshQueueDetails
                && !refreshActiveStatus
                && !appendLoadLog;

        private void ScheduleImageLoadReviewRefresh(string imagePath, bool refreshActiveStatus, bool refreshClassCatalog)
        {
            Dispatcher.BeginInvoke(
                new Action(() => ApplyScheduledImageLoadReviewRefresh(
                    imagePath,
                    refreshActiveStatus,
                    refreshClassCatalog)),
                System.Windows.Threading.DispatcherPriority.Background);
        }

        private void ApplyScheduledImageLoadReviewRefresh(
            string imagePath,
            bool refreshActiveStatus,
            bool refreshClassCatalog)
        {
            if (isApplicationCloseApproved
                || !string.Equals(activeImagePath, imagePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            RefreshImageLoadReviewState(refreshActiveStatus, refreshClassCatalog);
        }

        private void RefreshImageLoadReviewState(bool refreshActiveStatus, bool refreshClassCatalog)
        {
            RefreshCandidateList();
            RefreshObjectList();
            if (refreshClassCatalog)
            {
                PopulateClassList();
            }

            if (refreshActiveStatus)
            {
                RefreshActiveImageQueueStatus(hasActiveCandidates: false);
            }
            else
            {
                UpdateImageQueueStatusText();
            }
        }

        public ImageDecodeCacheDiagnostics GetImageDecodeCacheDiagnostics()
            => imageDecodeCacheService.GetDiagnostics();

        private void RecordImageLoadDiagnostics(
            string imagePath,
            bool cacheHit,
            double totalMilliseconds,
            double decodeMilliseconds,
            double canvasUploadMilliseconds,
            double canvasRefreshMilliseconds,
            double stateTransferMilliseconds,
            double annotationResetMilliseconds,
            double queuePopulateMilliseconds,
            double reviewRefreshMilliseconds,
            double preloadScheduleMilliseconds)
        {
            lastImageLoadDiagnostics = ImageLoadDiagnosticsService.Create(
                imagePath,
                cacheHit,
                totalMilliseconds,
                decodeMilliseconds,
                canvasUploadMilliseconds,
                canvasRefreshMilliseconds,
                stateTransferMilliseconds,
                annotationResetMilliseconds,
                queuePopulateMilliseconds,
                reviewRefreshMilliseconds,
                preloadScheduleMilliseconds);
        }

        private bool TrySavePendingAnnotationsBeforeImageChange(string nextImagePath)
        {
            if (activeImageBitmap == null
                || string.IsNullOrWhiteSpace(activeImagePath)
                || string.IsNullOrWhiteSpace(nextImagePath)
                || string.Equals(Path.GetFullPath(activeImagePath), Path.GetFullPath(nextImagePath), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!annotationDirtyState.IsDirty && !HasPendingMaskStrokeCommitWork())
            {
                return true;
            }

            if (SaveCurrentAnnotations(out int savedCount))
            {
                AppendLog($"이미지 전환 전 라벨 자동 저장: {Path.GetFileName(activeImagePath)} / 객체 {savedCount}개");
                return true;
            }

            AppendLog($"이미지 전환 중단: 현재 이미지 라벨을 저장하지 못했습니다. {Path.GetFileName(activeImagePath)}");
            return false;
        }

        private void PreloadAdjacentQueueImages(string imagePath)
        {
            // Adjacent preload is only useful after the interactive shell is loaded; headless construction tests should not open extra image files.
            if (!IsLoaded || string.IsNullOrWhiteSpace(imagePath) || imageQueueItems.Count == 0)
            {
                return;
            }

            imageDecodePreloadService.StartAdjacentPreload(
                imagePath,
                imageQueueItems.Select(item => item.ImagePath),
                imageDecodeCacheService,
                File.Exists,
                imageDecodeService.TryDecodeForCache);
        }
        #endregion

        #region ImageQueue
        // File enumeration and metadata construction can be slow on a large folder. Keep the synchronous entry point for
        // deterministic callers, while UI commands use the cancellable asynchronous path below.
        public int LoadImageQueueFromRoot(string imageRoot, string selectedImagePath = "", bool loadFirstImage = false, bool refreshDetails = true)
        {
            if (!TryBeginImageQueueCatalogLoad(imageRoot, selectedImagePath, loadFirstImage, refreshDetails, out ImageQueueCatalogLoadRequest request))
            {
                return 0;
            }

            try
            {
                ImageQueueCatalogLoadResult snapshot = imageQueueCatalogLoadCoordinator.Load(request);
                return IsCurrentImageQueueCatalogLoad(request)
                    ? ApplyImageQueueCatalogLoad(request, snapshot)
                    : 0;
            }
            catch (OperationCanceledException)
            {
                return 0;
            }
            catch (Exception ex)
            {
                ReportImageQueueCatalogLoadFailure(request, ex);
                return 0;
            }
            finally
            {
                CompleteImageQueueCatalogLoad(request);
            }
        }

        public Task<int> LoadImageQueueFromRootAsync(
            string imageRoot,
            string selectedImagePath = "",
            bool loadFirstImage = false,
            bool refreshDetails = true)
        {
            if (!TryBeginImageQueueCatalogLoad(imageRoot, selectedImagePath, loadFirstImage, refreshDetails, out ImageQueueCatalogLoadRequest request))
            {
                return Task.FromResult(0);
            }

            return LoadImageQueueFromRootAsyncCore(request);
        }

        private async Task<int> LoadImageQueueFromRootAsyncCore(ImageQueueCatalogLoadRequest request)
        {
            try
            {
                ImageQueueCatalogLoadResult snapshot = await imageQueueCatalogLoadCoordinator.LoadAsync(request).ConfigureAwait(true);
                return IsCurrentImageQueueCatalogLoad(request)
                    ? ApplyImageQueueCatalogLoad(request, snapshot)
                    : 0;
            }
            catch (OperationCanceledException)
            {
                return 0;
            }
            catch (Exception ex)
            {
                ReportImageQueueCatalogLoadFailure(request, ex);
                return 0;
            }
            finally
            {
                CompleteImageQueueCatalogLoad(request);
            }
        }

        private bool TryBeginImageQueueCatalogLoad(
            string imageRoot,
            string selectedImagePath,
            bool loadFirstImage,
            bool refreshDetails,
            out ImageQueueCatalogLoadRequest request)
        {
            request = null;
            if (isApplicationCloseApproved)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(imageRoot) || !Directory.Exists(imageRoot))
            {
                SetDatasetStatus("\uB370\uC774\uD130\uC14B: \uC774\uBBF8\uC9C0 \uB8E8\uD2B8 \uC5C6\uC74C");
                AppendLog($"Image root does not exist: {imageRoot}");
                return false;
            }

            CancelImageQueueCatalogLoad(waitForCompletion: false);
            CancelImageQueueDetailRefresh(waitForCompletion: false);

            bool imageRootChanged = !imageQueueSelectionService.IsSameRoot(imageRoot, currentImageRoot);
            currentImageRoot = imageRoot;
            if (imageRootChanged)
            {
                dismissedAnomalyFolderStateSuggestionRoot = string.Empty;
            }
            ImageQueueViewModel?.SetCurrentImageFolder(currentImageRoot, canOpenFolder: true);
            ImageQueueViewModel?.SetAnomalyImageReviewMode(IsAnomalyDatasetPurpose());

            request = imageQueueCatalogLoadCoordinator.Begin(
                imageRoot,
                selectedImagePath,
                loadFirstImage,
                refreshDetails,
                global.Data,
                IsAnomalyDatasetPurpose());
            if (request == null)
            {
                return false;
            }

            SetDatasetStatus("\uB370\uC774\uD130\uC14B: \uD30C\uC77C \uBAA9\uB85D \uC900\uBE44 \uC911...");
            return true;
        }

        private int ApplyImageQueueCatalogLoad(
            ImageQueueCatalogLoadRequest request,
            ImageQueueCatalogLoadResult snapshot)
        {
            if (snapshot == null || !IsCurrentImageQueueCatalogLoad(request))
            {
                return 0;
            }

            imageReviewStatus = snapshot.ReviewStatus;
            anomalyImageReviewStatus = snapshot.AnomalyReviewStatus;
            imageQualityReviewWorkflowService = snapshot.ReviewWorkflow;
            anomalyImageReviewWorkflowService = snapshot.AnomalyReviewWorkflow;
            UpdateAnomalyFolderStateSuggestion(request, snapshot.AnomalyFolderStateSuggestion);

            suppressImageQueueSelection = true;
            try
            {
                IReadOnlyList<WpfImageQueueItem> items = imageQueueSelectionService.CreateShellItemsFromCatalog(snapshot.CatalogEntries);
                if (request.IsAnomalyPurpose)
                {
                    foreach (WpfImageQueueItem item in items)
                    {
                        WpfImageQueuePresenter.ApplyAnomalyReviewStatusToItem(item, anomalyImageReviewWorkflowService.GetOrCreate(item.ImagePath));
                    }
                }
                imageQueueItems.ReplaceAll(items);
                RebuildImageQueueItemIndex(items);
                imageQueueView?.Refresh();
                SelectImageQueueItem(request.SelectedImagePath);
            }
            finally
            {
                suppressImageQueueSelection = false;
            }

            UpdateImageQueueStatusText();
            if (request.RefreshDetails && snapshot.ImagePaths.Count > 0)
            {
                IReadOnlyDictionary<string, WpfImageQueueItem> itemLookup =
                    new Dictionary<string, WpfImageQueueItem>(imageQueueItemsByPath, StringComparer.OrdinalIgnoreCase);
                imageQueueDetailRefreshCoordinator.Begin(
                    snapshot.ImagePaths,
                    imageQualityReviewWorkflowService,
                    request.Data,
                    (results, loadedCount, totalCount, token) => ApplyImageQueueDetailBatchAsync(
                        results,
                        itemLookup,
                        loadedCount,
                        totalCount,
                        token),
                    CompleteImageQueueDetailRefreshAsync);
            }

            string targetPath = snapshot.ImagePaths.FirstOrDefault(path =>
                    string.Equals(path, request.SelectedImagePath, StringComparison.OrdinalIgnoreCase))
                ?? snapshot.ImagePaths.FirstOrDefault();
            if (request.LoadFirstImage && !string.IsNullOrWhiteSpace(targetPath))
            {
                TryLoadImage(targetPath);
            }
            else if (request.LoadFirstImage)
            {
                ClearActiveImageAfterQueueReset();
            }

            return snapshot.ImagePaths.Count;
        }

        private bool IsCurrentImageQueueCatalogLoad(ImageQueueCatalogLoadRequest request)
        {
            return imageQueueCatalogLoadCoordinator.IsCurrent(request);
        }

        private void CompleteImageQueueCatalogLoad(ImageQueueCatalogLoadRequest request)
        {
            imageQueueCatalogLoadCoordinator.Complete(request);
        }

        private void ReportImageQueueCatalogLoadFailure(ImageQueueCatalogLoadRequest request, Exception exception)
        {
            if (!IsCurrentImageQueueCatalogLoad(request))
            {
                return;
            }

            SetDatasetStatus("\uB370\uC774\uD130\uC14B: \uD30C\uC77C \uBAA9\uB85D \uC900\uBE44 \uC2E4\uD328");
            AppendLog($"Image queue catalog load failed: {exception.Message}");
        }

        private void RebuildImageQueueItemIndex(IEnumerable<WpfImageQueueItem> items)
        {
            imageQueueItemsByPath.Clear();
            foreach (WpfImageQueueItem item in items ?? Enumerable.Empty<WpfImageQueueItem>())
            {
                if (item != null && !string.IsNullOrWhiteSpace(item.ImagePath))
                {
                    imageQueueItemsByPath[item.ImagePath] = item;
                }
            }
        }

        private void UpdateAnomalyFolderStateSuggestion(
            ImageQueueCatalogLoadRequest request,
            AnomalyImageReviewFolderImportResult suggestion)
        {
            bool canSuggest = request?.IsAnomalyPurpose == true
                && suggestion?.HasChanges == true
                && !imageQueueSelectionService.IsSameRoot(request.ImageRoot, dismissedAnomalyFolderStateSuggestionRoot);
            if (canSuggest)
            {
                ImageQueueViewModel?.SetAnomalyFolderStateSuggestion(suggestion);
                return;
            }

            ImageQueueViewModel?.ClearAnomalyFolderStateSuggestion();
        }

        private void ExecuteApplyAnomalyFolderStateSuggestionCommand()
        {
            if (!IsAnomalyDatasetPurpose())
            {
                ImageQueueViewModel?.ClearAnomalyFolderStateSuggestion();
                return;
            }

            AnomalyImageReviewFolderImportResult result = anomalyImageReviewWorkflowService.ImportUnreviewedStatesFromParentFolders();
            dismissedAnomalyFolderStateSuggestionRoot = currentImageRoot;
            ImageQueueViewModel?.ClearAnomalyFolderStateSuggestion();
            if (!result.HasChanges)
            {
                return;
            }

            SaveAnomalyImageReviewStatus();
            foreach (WpfImageQueueItem item in imageQueueItems)
            {
                WpfImageQueuePresenter.ApplyAnomalyReviewStatusToItem(item, anomalyImageReviewWorkflowService.GetOrCreate(item.ImagePath));
            }
            imageQueueView?.Refresh();
            UpdateImageQueueStatusText();
            SetDatasetStatus($"OK/NG 이미지 판정: 폴더명 기준 일괄 판정 완료 (정상 {result.NormalImageCount}장 / 이상 {result.AbnormalImageCount}장, 기존 수동 판정 {result.ExistingReviewCount}장 유지)");
            AppendLog($"Anomaly folder-state suggestion applied: normal={result.NormalImageCount}, abnormal={result.AbnormalImageCount}, existing={result.ExistingReviewCount}.");
        }

        private void ExecuteDismissAnomalyFolderStateSuggestionCommand()
        {
            dismissedAnomalyFolderStateSuggestionRoot = currentImageRoot;
            ImageQueueViewModel?.ClearAnomalyFolderStateSuggestion();
            SetDatasetStatus("OK/NG 이미지 판정: 폴더명은 적용하지 않았습니다. 이미지를 하나씩 정상 또는 이상으로 판정하세요.");
            AppendLog("Anomaly folder-state suggestion dismissed; images remain unreviewed until an operator reviews them.");
        }

        private void PopulateImageQueue(string imageRoot, string selectedImagePath, bool refreshDetails = true)
        {
            if (string.IsNullOrWhiteSpace(imageRoot) || !Directory.Exists(imageRoot))
            {
                return;
            }

            if (imageQueueItems.Count == 0
                || !imageQueueSelectionService.IsSameRoot(imageRoot, currentImageRoot))
            {
                LoadImageQueueFromRoot(imageRoot, selectedImagePath, loadFirstImage: false, refreshDetails: refreshDetails);
                return;
            }

            SelectImageQueueItem(selectedImagePath);
            RefreshActiveImageQueueStatus(hasActiveCandidates: pendingDetectionCandidates.Count > 0);
        }
        #endregion

    }
}
