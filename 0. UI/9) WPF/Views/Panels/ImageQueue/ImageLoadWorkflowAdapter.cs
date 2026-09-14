using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.Diagnostics;
using System.IO;
using CvMat = OpenCvSharp.Mat;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingSize = System.Drawing.Size;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the decode, canvas handoff, image workspace transfer, and post-load sequencing.
    /// The Shell supplies UI callbacks; existing decode, annotation, queue, and review owners stay intact.
    /// </summary>
    internal sealed class ImageLoadWorkflowAdapter
    {
        private readonly ImageLoadWorkflowAdapterContext context;

        private LabelingProjectData projectData => context.DataProvider?.Invoke();

        internal ImageLoadWorkflowAdapter(ImageLoadWorkflowAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            ArgumentNullException.ThrowIfNull(context.DataProvider);
        }

        internal ImageLoadDiagnostics LastImageLoadDiagnostics
            => context.LastImageLoadDiagnosticsProvider?.Invoke() ?? ImageLoadDiagnostics.Empty;

        internal bool TryLoadStartupSampleImage()
        {
            if (context.ApplicationState.ImageWorkspace.ActiveImage != null
                || !string.IsNullOrWhiteSpace(context.ApplicationState.ImageWorkspace.ActiveImagePath))
            {
                return true;
            }

            context.EnsureProjectSettings?.Invoke();
            string imagePath = YoloWorkerSmokeTestService.ResolveSmokeImagePath(
                projectData.ProjectSettings.PythonModel);
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                context.SetDatasetStatus?.Invoke(
                    context.ImageLoadPresentationService.BuildStartupSampleMissingDatasetStatus());
                context.AppendLog?.Invoke(context.ImageLoadPresentationService.BuildStartupSampleMissingLog());
                return false;
            }

            return TryLoadImage(imagePath, populateQueue: true, refreshQueueDetails: false);
        }

        internal bool TryLoadImage(
            string imagePath,
            bool populateQueue = true,
            bool refreshQueueDetails = true,
            bool refreshActiveStatus = true,
            bool appendLoadLog = true)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                context.AppendLog?.Invoke(context.ImageLoadPresentationService.BuildMissingImageLog(imagePath));
                return false;
            }

            if (!context.ImageViewerLoadOverrideProvider()
                && !EnsureViewerReady(out string graphicsDetail))
            {
                context.SetDatasetStatus?.Invoke("이미지 뷰어 환경 확인 필요");
                context.AppendLog?.Invoke("이미지 열기 차단: " + graphicsDetail);
                if (context.IsViewerVisible?.Invoke() == true)
                {
                    context.ShowViewerUnavailable?.Invoke(graphicsDetail);
                }

                return false;
            }

            if (context.TrySavePendingAnnotationsBeforeImageChange?.Invoke(imagePath) == false)
            {
                return false;
            }

            context.PrepareForImageChange?.Invoke();
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
                if (context.ImageDecodeCacheService.TryTake(imagePath, out CachedDecodedImage cachedImage))
                {
                    cacheHit = true;
                    workspaceBitmap = cachedImage.TakeBitmap();
                    imageMat = cachedImage.TakeMat();
                    cachedImage.Dispose();
                }
                else
                {
                    using CachedDecodedImage decodedImage = context.ImageDecodeService.DecodeForCanvas(imagePath);
                    workspaceBitmap = decodedImage.TakeBitmap();
                    imageMat = decodedImage.TakeMat();
                }
                decodeMilliseconds = ImageLoadDiagnosticsService.TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);

                string imageName = Path.GetFileNameWithoutExtension(imagePath);
                context.LoadImageToCanvas?.Invoke(imageMat, Path.GetFileName(imagePath));
                canvasUploadMilliseconds = ImageLoadDiagnosticsService.TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);
                if (!context.ImageViewerLoadOverrideProvider())
                {
                    context.RefreshCanvas?.Invoke();
                }

                canvasRefreshMilliseconds = ImageLoadDiagnosticsService.TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);
                DrawingBitmap previousBitmap = context.ApplicationState.ImageWorkspace.ActiveImage;
                context.ImageLoadResourceService.ReplaceActiveBitmap(previousBitmap, workspaceBitmap);
                context.ApplicationState.ImageWorkspace.SetActiveImage(imageName, imagePath, workspaceBitmap);
                workspaceBitmap = null;

                context.ImageLoadResourceService.SetDisplayImage(imageMat);
                imageMat = null;
                stateTransferMilliseconds = ImageLoadDiagnosticsService.TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);

                context.ResetForImageChange?.Invoke();
                context.UpdateDetectionResultOverlay?.Invoke();
                int loadedSavedBoxCount = context.LoadSavedBoxAnnotationsForActiveImage?.Invoke(imagePath) ?? 0;
                int loadedSavedSegmentCount = context.LoadSavedSegmentationAnnotationsForActiveImage?.Invoke(imagePath) ?? 0;
                context.LoadObjectMetadataForActiveImage?.Invoke(imagePath);
                int loadedSavedAnnotationCount = loadedSavedBoxCount + loadedSavedSegmentCount;
                annotationResetMilliseconds = ImageLoadDiagnosticsService.TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);
                if (populateQueue)
                {
                    context.PopulateImageQueueAfterLoad?.Invoke(imagePath, refreshQueueDetails);
                }

                queuePopulateMilliseconds = ImageLoadDiagnosticsService.TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);
                DrawingSize activeImageSize = context.ActiveImageSizeProvider?.Invoke() ?? DrawingSize.Empty;
                context.SetDatasetStatus?.Invoke(
                    context.ImageLoadPresentationService.BuildLoadedDatasetStatus(imagePath, activeImageSize));
                context.SetModelStatus?.Invoke(
                    context.ImageLoadPresentationService.BuildModelStatus(
                        projectData.ProjectSettings?.PythonModel?.WeightsPath));
                context.MarkAnnotationsSaved?.Invoke(
                    context.ImageLoadPresentationService.BuildAnnotationLoadedStatus());
                bool deferReviewRefresh = ShouldDeferImageLoadReviewRefresh(
                    populateQueue,
                    refreshQueueDetails,
                    refreshActiveStatus,
                    appendLoadLog);
                if (deferReviewRefresh)
                {
                    context.ScheduleImageLoadReviewRefresh?.Invoke(imagePath, refreshActiveStatus, false);
                }
                else
                {
                    context.RefreshImageLoadReviewState?.Invoke(refreshActiveStatus, true);
                }

                reviewRefreshMilliseconds = ImageLoadDiagnosticsService.TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);
                if (context.IsDisplayAdjustmentActive?.Invoke() == true)
                {
                    context.ScheduleDisplayAdjustmentRefresh?.Invoke();
                }

                if (!appendLoadLog)
                {
                    context.PreloadAdjacentQueueImages?.Invoke(imagePath);
                    preloadScheduleMilliseconds = ImageLoadDiagnosticsService.TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);
                    SetLastImageLoadDiagnostics(
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

                context.AppendLog?.Invoke(loadedSavedAnnotationCount > 0
                    ? $"{context.ImageLoadPresentationService.BuildLoadLog(imagePath)} / saved labels: {loadedSavedAnnotationCount}"
                    : context.ImageLoadPresentationService.BuildLoadLog(imagePath));
                context.PreloadAdjacentQueueImages?.Invoke(imagePath);
                preloadScheduleMilliseconds = ImageLoadDiagnosticsService.TakeElapsedMilliseconds(loadStopwatch, ref stepStartTicks);
                SetLastImageLoadDiagnostics(
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
                context.SetDatasetStatus?.Invoke(context.ImageLoadPresentationService.BuildLoadFailureDatasetStatus());
                context.AppendLog?.Invoke(context.ImageLoadPresentationService.BuildLoadFailureLog(ex.Message));
                return false;
            }
            finally
            {
                imageMat?.Dispose();
            }
        }

        private bool EnsureViewerReady(out string graphicsDetail)
        {
            (bool isReady, string detail) readiness = context.EnsureViewerReadyForImageLoad?.Invoke()
                ?? (true, string.Empty);
            graphicsDetail = readiness.detail ?? string.Empty;
            return readiness.isReady;
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

        private void SetLastImageLoadDiagnostics(
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
            context.SetLastImageLoadDiagnostics?.Invoke(
                ImageLoadDiagnosticsService.Create(
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
                    preloadScheduleMilliseconds));
        }
    }

    internal sealed class ImageLoadWorkflowAdapterContext
    {
        internal LabelingApplicationState ApplicationState { get; init; }
        internal Func<LabelingProjectData> DataProvider { get; init; }
        internal ImageDecodeCacheService ImageDecodeCacheService { get; init; }
        internal ImageDecodeService ImageDecodeService { get; init; }
        internal ImageLoadResourceService ImageLoadResourceService { get; init; }
        internal ImageLoadPresentationService ImageLoadPresentationService { get; init; }
        internal Func<string> ActiveImagePathProvider { get; init; }
        internal Func<DrawingSize> ActiveImageSizeProvider { get; init; }
        internal Func<bool> ImageViewerLoadOverrideProvider { get; init; }
        internal Func<(bool IsReady, string Detail)> EnsureViewerReadyForImageLoad { get; init; }
        internal Func<bool> IsViewerVisible { get; init; }
        internal Action<string> ShowViewerUnavailable { get; init; }
        internal Action<CvMat, string> LoadImageToCanvas { get; init; }
        internal Action RefreshCanvas { get; init; }
        internal Action EnsureProjectSettings { get; init; }
        internal Func<string, bool> TrySavePendingAnnotationsBeforeImageChange { get; init; }
        internal Action PrepareForImageChange { get; init; }
        internal Action ResetForImageChange { get; init; }
        internal Action UpdateDetectionResultOverlay { get; init; }
        internal Func<string, int> LoadSavedBoxAnnotationsForActiveImage { get; init; }
        internal Func<string, int> LoadSavedSegmentationAnnotationsForActiveImage { get; init; }
        internal Action<string> LoadObjectMetadataForActiveImage { get; init; }
        internal Action<string, bool> PopulateImageQueueAfterLoad { get; init; }
        internal Action<string, bool, bool> ScheduleImageLoadReviewRefresh { get; init; }
        internal Action<bool, bool> RefreshImageLoadReviewState { get; init; }
        internal Func<bool> IsDisplayAdjustmentActive { get; init; }
        internal Action ScheduleDisplayAdjustmentRefresh { get; init; }
        internal Action<string> PreloadAdjacentQueueImages { get; init; }
        internal Action<ImageLoadDiagnostics> SetLastImageLoadDiagnostics { get; init; }
        internal Func<ImageLoadDiagnostics> LastImageLoadDiagnosticsProvider { get; init; }
        internal Action<string> SetDatasetStatus { get; init; }
        internal Action<string> SetModelStatus { get; init; }
        internal Action<string> MarkAnnotationsSaved { get; init; }
        internal Action<string> AppendLog { get; init; }
    }
}
