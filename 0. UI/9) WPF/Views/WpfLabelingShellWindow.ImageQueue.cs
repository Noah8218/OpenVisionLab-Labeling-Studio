using Lib.Common;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
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
                ImageQueueCatalogLoadSnapshot snapshot = BuildImageQueueCatalogLoadSnapshot(request);
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

            Task<int> loadTask = LoadImageQueueFromRootAsyncCore(request);
            imageQueueCatalogLoadTask = loadTask;
            return loadTask;
        }

        private async Task<int> LoadImageQueueFromRootAsyncCore(ImageQueueCatalogLoadRequest request)
        {
            try
            {
                ImageQueueCatalogLoadSnapshot snapshot = await Task.Run(
                    () => BuildImageQueueCatalogLoadSnapshot(request),
                    request.CancellationToken).ConfigureAwait(true);
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

            var cancellation = new CancellationTokenSource();
            imageQueueCatalogLoadCts = cancellation;
            imageQueueCatalogLoadTask = Task.CompletedTask;
            int version = ++imageQueueCatalogLoadVersion;
            request = new ImageQueueCatalogLoadRequest(
                imageRoot,
                selectedImagePath,
                loadFirstImage,
                refreshDetails,
                global.Data,
                IsAnomalyDatasetPurpose(),
                version,
                cancellation);
            SetDatasetStatus("\uB370\uC774\uD130\uC14B: \uD30C\uC77C \uBAA9\uB85D \uC900\uBE44 \uC911...");
            return true;
        }

        private ImageQueueCatalogLoadSnapshot BuildImageQueueCatalogLoadSnapshot(ImageQueueCatalogLoadRequest request)
        {
            request.CancellationToken.ThrowIfCancellationRequested();
            List<string> imagePaths = imageQueueSelectionService.EnumerateImageFiles(
                request.ImageRoot,
                request.CancellationToken);
            if (request.IsAnomalyPurpose)
            {
                imagePaths = imageQueueSelectionService.InterleaveTopLevelFolderImages(
                    request.ImageRoot,
                    imagePaths,
                    request.CancellationToken);
            }
            IReadOnlyList<WpfImageQueueCatalogEntry> catalogEntries = imageQueueSelectionService.CreateCatalogEntries(
                imagePaths,
                request.CancellationToken);

            var reviewStatus = new YoloImageReviewStatusService();
            reviewStatus.SetImages(imagePaths);
            reviewStatus.LoadReviewStatus(request.Data, imagePaths);

            var anomalyReviewStatus = new AnomalyImageReviewStatusService();
            anomalyReviewStatus.SetImages(imagePaths);
            anomalyReviewStatus.LoadReviewStatus(request.Data, imagePaths);
            AnomalyImageReviewFolderImportResult anomalyFolderStateSuggestion = request.IsAnomalyPurpose
                ? anomalyReviewStatus.PreviewUnreviewedStatesFromParentFolders()
                : null;
            request.CancellationToken.ThrowIfCancellationRequested();

            return new ImageQueueCatalogLoadSnapshot(
                imagePaths,
                catalogEntries,
                reviewStatus,
                anomalyReviewStatus,
                anomalyFolderStateSuggestion);
        }

        private int ApplyImageQueueCatalogLoad(
            ImageQueueCatalogLoadRequest request,
            ImageQueueCatalogLoadSnapshot snapshot)
        {
            if (snapshot == null || !IsCurrentImageQueueCatalogLoad(request))
            {
                return 0;
            }

            imageReviewStatus = snapshot.ReviewStatus;
            anomalyImageReviewStatus = snapshot.AnomalyReviewStatus;
            UpdateAnomalyFolderStateSuggestion(request, snapshot.AnomalyFolderStateSuggestion);

            suppressImageQueueSelection = true;
            try
            {
                IReadOnlyList<WpfImageQueueItem> items = imageQueueSelectionService.CreateShellItemsFromCatalog(snapshot.CatalogEntries);
                if (request.IsAnomalyPurpose)
                {
                    foreach (WpfImageQueueItem item in items)
                    {
                        ApplyAnomalyReviewStatusToItem(item, anomalyImageReviewStatus.GetOrCreate(item.ImagePath));
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
                imageQueueDetailLoadCts = new CancellationTokenSource();
                imageQueueDetailLoadTask = StartImageQueueDetailRefreshAsync(
                    snapshot.ImagePaths,
                    new Dictionary<string, WpfImageQueueItem>(imageQueueItemsByPath, StringComparer.OrdinalIgnoreCase),
                    imageReviewStatus,
                    request.Data,
                    imageQueueDetailLoadCts.Token);
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
            return request != null
                && request.Version == imageQueueCatalogLoadVersion
                && ReferenceEquals(request.Cancellation, imageQueueCatalogLoadCts)
                && !request.CancellationToken.IsCancellationRequested;
        }

        private void CompleteImageQueueCatalogLoad(ImageQueueCatalogLoadRequest request)
        {
            if (request == null || !ReferenceEquals(request.Cancellation, imageQueueCatalogLoadCts))
            {
                return;
            }

            imageQueueCatalogLoadCts = null;
            imageQueueCatalogLoadTask = Task.CompletedTask;
            request.Cancellation.Dispose();
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

            AnomalyImageReviewFolderImportResult result = anomalyImageReviewStatus.ImportUnreviewedStatesFromParentFolders();
            dismissedAnomalyFolderStateSuggestionRoot = currentImageRoot;
            ImageQueueViewModel?.ClearAnomalyFolderStateSuggestion();
            if (!result.HasChanges)
            {
                return;
            }

            SaveAnomalyImageReviewStatus();
            foreach (WpfImageQueueItem item in imageQueueItems)
            {
                ApplyAnomalyReviewStatusToItem(item, anomalyImageReviewStatus.GetOrCreate(item.ImagePath));
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

        private sealed class ImageQueueCatalogLoadRequest
        {
            public ImageQueueCatalogLoadRequest(
                string imageRoot,
                string selectedImagePath,
                bool loadFirstImage,
                bool refreshDetails,
                CData data,
                bool isAnomalyPurpose,
                int version,
                CancellationTokenSource cancellation)
            {
                ImageRoot = imageRoot ?? string.Empty;
                SelectedImagePath = selectedImagePath ?? string.Empty;
                LoadFirstImage = loadFirstImage;
                RefreshDetails = refreshDetails;
                Data = data;
                IsAnomalyPurpose = isAnomalyPurpose;
                Version = version;
                Cancellation = cancellation;
            }

            public string ImageRoot { get; }

            public string SelectedImagePath { get; }

            public bool LoadFirstImage { get; }

            public bool RefreshDetails { get; }

            public CData Data { get; }

            public bool IsAnomalyPurpose { get; }

            public int Version { get; }

            public CancellationTokenSource Cancellation { get; }

            public CancellationToken CancellationToken => Cancellation.Token;
        }

        private sealed class ImageQueueCatalogLoadSnapshot
        {
            public ImageQueueCatalogLoadSnapshot(
                IReadOnlyList<string> imagePaths,
                IReadOnlyList<WpfImageQueueCatalogEntry> catalogEntries,
                YoloImageReviewStatusService reviewStatus,
                AnomalyImageReviewStatusService anomalyReviewStatus,
                AnomalyImageReviewFolderImportResult anomalyFolderStateSuggestion)
            {
                ImagePaths = imagePaths ?? Array.Empty<string>();
                CatalogEntries = catalogEntries ?? Array.Empty<WpfImageQueueCatalogEntry>();
                ReviewStatus = reviewStatus ?? new YoloImageReviewStatusService();
                AnomalyReviewStatus = anomalyReviewStatus ?? new AnomalyImageReviewStatusService();
                AnomalyFolderStateSuggestion = anomalyFolderStateSuggestion;
            }

            public IReadOnlyList<string> ImagePaths { get; }

            public IReadOnlyList<WpfImageQueueCatalogEntry> CatalogEntries { get; }

            public YoloImageReviewStatusService ReviewStatus { get; }

            public AnomalyImageReviewStatusService AnomalyReviewStatus { get; }

            public AnomalyImageReviewFolderImportResult AnomalyFolderStateSuggestion { get; }
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
    }
}
