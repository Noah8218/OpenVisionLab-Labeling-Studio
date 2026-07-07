using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.ViewModels;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CvMat = OpenCvSharp.Mat;
using DrawingBitmap = System.Drawing.Bitmap;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Image loading still coordinates shell state, but lives outside the event-heavy code-behind for easier diagnosis.
        public WpfImageLoadDiagnostics LastImageLoadDiagnostics => lastImageLoadDiagnostics;

        public bool TryLoadStartupSampleImage()
        {
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

            if (!TrySavePendingAnnotationsBeforeImageChange(imagePath))
            {
                return false;
            }

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
                if (imageDecodeCacheService.TryTake(imagePath, out WpfCachedDecodedImage cachedImage))
                {
                    cacheHit = true;
                    workspaceBitmap = cachedImage.TakeBitmap();
                    imageMat = cachedImage.TakeMat();
                    cachedImage.Dispose();
                }
                else
                {
                    using WpfCachedDecodedImage decodedImage = imageDecodeService.DecodeForCanvas(imagePath);
                    workspaceBitmap = decodedImage.TakeBitmap();
                    imageMat = decodedImage.TakeMat();
                }
                decodeMilliseconds = WpfImageLoadDiagnosticsService.TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);

                string imageName = Path.GetFileNameWithoutExtension(imagePath);
                using (MainCanvasViewModel.ImageViewer.SuppressRefresh())
                {
                    MainCanvasViewModel.LoadImage(imageMat, Path.GetFileName(imagePath));
                    MainCanvasViewModel.ClearRois();
                    MainCanvasViewModel.SetDetectionOverlays(Array.Empty<RoiImageCanvasDetectionOverlay>());
                    MainCanvasViewModel.SetMaskOverlays(Array.Empty<RoiImageCanvasMaskOverlay>());
                    MainCanvasViewModel.SetPolygonOverlays(Array.Empty<RoiImageCanvasPolygonOverlay>());
                    MainCanvasViewModel.ClearMaskStrokePreview(refresh: false, clearTexture: true);
                }
                canvasUploadMilliseconds = WpfImageLoadDiagnosticsService.TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);
                MainCanvasViewModel.ImageViewer.RefreshGL();
                canvasRefreshMilliseconds = WpfImageLoadDiagnosticsService.TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);

                activeImageBitmap?.Dispose();
                activeImageBitmap = workspaceBitmap;
                workspaceBitmap = null;
                activeImagePath = imagePath;
                activeImageSize = activeImageBitmap.Size;

                global.Data.LastSelectImageName = imageName;
                global.Data.LastSelectImagePath = imagePath;
                global.ImageWorkspace.SetActiveImage(imageName, imagePath, activeImageBitmap);
                CDisplayManager.ImageSrc = imageMat;
                imageMat = null;
                stateTransferMilliseconds = WpfImageLoadDiagnosticsService.TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);

                manualRois.Clear();
                manualRoiClassNames.Clear();
                manualRoiShapeKinds.Clear();
                manualRoiOverlayIds.Clear();
                manualSegments.Clear();
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
                ClearAnnotationHistory();
                UpdateDetectionResultOverlay();
                int loadedSavedBoxCount = LoadSavedBoxAnnotationsForActiveImage(imagePath);
                int loadedSavedSegmentCount = LoadSavedSegmentationAnnotationsForActiveImage(imagePath);
                int loadedSavedAnnotationCount = loadedSavedBoxCount + loadedSavedSegmentCount;
                annotationResetMilliseconds = WpfImageLoadDiagnosticsService.TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);
                if (populateQueue)
                {
                    PopulateImageQueue(Path.GetDirectoryName(imagePath), imagePath, refreshQueueDetails);
                }
                queuePopulateMilliseconds = WpfImageLoadDiagnosticsService.TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);
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
                reviewRefreshMilliseconds = WpfImageLoadDiagnosticsService.TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);
                if (!appendLoadLog)
                {
                    PreloadAdjacentQueueImages(imagePath);
                    preloadScheduleMilliseconds = WpfImageLoadDiagnosticsService.TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);
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
                preloadScheduleMilliseconds = WpfImageLoadDiagnosticsService.TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);
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
                new Action(() =>
                {
                    if (!string.Equals(activeImagePath, imagePath, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    RefreshImageLoadReviewState(refreshActiveStatus, refreshClassCatalog);
                }),
                System.Windows.Threading.DispatcherPriority.Background);
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

        public WpfImageDecodeCacheDiagnostics GetImageDecodeCacheDiagnostics()
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
            lastImageLoadDiagnostics = WpfImageLoadDiagnosticsService.Create(
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

            if (string.IsNullOrWhiteSpace(annotationDirtyReason) && !HasPendingMaskStrokeCommitWork())
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
    }
}
