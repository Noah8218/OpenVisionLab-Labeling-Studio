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

            currentImageRoot = imageRoot;
            ImageQueueViewModel?.SetCurrentImageFolder(currentImageRoot, canOpenFolder: true);

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
            IReadOnlyList<WpfImageQueueCatalogEntry> catalogEntries = imageQueueSelectionService.CreateCatalogEntries(
                imagePaths,
                request.CancellationToken);

            var reviewStatus = new YoloImageReviewStatusService();
            reviewStatus.SetImages(imagePaths);
            reviewStatus.LoadReviewStatus(request.Data, imagePaths);

            var anomalyReviewStatus = new AnomalyImageReviewStatusService();
            anomalyReviewStatus.SetImages(imagePaths);
            anomalyReviewStatus.LoadReviewStatus(request.Data, imagePaths);
            bool importedAnomalyFolderStates = request.IsAnomalyPurpose
                && anomalyReviewStatus.ImportUnreviewedStatesFromParentFolders().HasChanges;
            request.CancellationToken.ThrowIfCancellationRequested();

            return new ImageQueueCatalogLoadSnapshot(
                imagePaths,
                catalogEntries,
                reviewStatus,
                anomalyReviewStatus,
                importedAnomalyFolderStates);
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
            if (snapshot.ImportedAnomalyFolderStates)
            {
                anomalyImageReviewStatus.SaveReviewStatus(request.Data);
            }

            suppressImageQueueSelection = true;
            try
            {
                IReadOnlyList<WpfImageQueueItem> items = imageQueueSelectionService.CreateShellItemsFromCatalog(snapshot.CatalogEntries);
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
                bool importedAnomalyFolderStates)
            {
                ImagePaths = imagePaths ?? Array.Empty<string>();
                CatalogEntries = catalogEntries ?? Array.Empty<WpfImageQueueCatalogEntry>();
                ReviewStatus = reviewStatus ?? new YoloImageReviewStatusService();
                AnomalyReviewStatus = anomalyReviewStatus ?? new AnomalyImageReviewStatusService();
                ImportedAnomalyFolderStates = importedAnomalyFolderStates;
            }

            public IReadOnlyList<string> ImagePaths { get; }

            public IReadOnlyList<WpfImageQueueCatalogEntry> CatalogEntries { get; }

            public YoloImageReviewStatusService ReviewStatus { get; }

            public AnomalyImageReviewStatusService AnomalyReviewStatus { get; }

            public bool ImportedAnomalyFolderStates { get; }
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
