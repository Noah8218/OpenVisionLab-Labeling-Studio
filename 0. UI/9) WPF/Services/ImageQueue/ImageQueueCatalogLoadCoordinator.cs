using MvcVisionSystem.Yolo;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns one active Image Queue catalog request and its cancellation lifetime.
    /// The Shell keeps root validation, UI-thread application, rows, and current-image transitions.
    /// </summary>
    public class ImageQueueCatalogLoadCoordinator : IDisposable
    {
        private readonly object syncRoot = new object();
        private readonly ImageQueueCatalogLoadService catalogService;
        private CancellationTokenSource activeCancellation;
        private Task activeTask = Task.CompletedTask;
        private int loadVersion;
        private bool disposed;

        public ImageQueueCatalogLoadCoordinator(ImageQueueCatalogLoadService catalogService)
        {
            this.catalogService = catalogService ?? throw new ArgumentNullException(nameof(catalogService));
        }

        public int CurrentVersion
        {
            get
            {
                lock (syncRoot)
                {
                    return loadVersion;
                }
            }
        }

        public ImageQueueCatalogLoadRequest Begin(
            string imageRoot,
            string selectedImagePath,
            bool loadFirstImage,
            bool refreshDetails,
            LabelingProjectData data,
            bool isAnomalyPurpose)
        {
            Cancel();
            lock (syncRoot)
            {
                if (disposed)
                {
                    return null;
                }

                var cancellation = new CancellationTokenSource();
                int version = ++loadVersion;
                var request = new ImageQueueCatalogLoadRequest(
                    imageRoot,
                    selectedImagePath,
                    loadFirstImage,
                    refreshDetails,
                    data,
                    isAnomalyPurpose,
                    version,
                    cancellation);
                activeCancellation = cancellation;
                activeTask = Task.CompletedTask;
                return request;
            }
        }

        public ImageQueueCatalogLoadResult Load(ImageQueueCatalogLoadRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return catalogService.Build(
                request.ImageRoot,
                request.Data,
                request.IsAnomalyPurpose,
                request.CancellationToken);
        }

        public Task<ImageQueueCatalogLoadResult> LoadAsync(ImageQueueCatalogLoadRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            lock (syncRoot)
            {
                if (disposed || !ReferenceEquals(request.Cancellation, activeCancellation))
                {
                    return Task.FromCanceled<ImageQueueCatalogLoadResult>(new CancellationToken(true));
                }

                Task<ImageQueueCatalogLoadResult> loadTask = Task.Run(
                    () => Load(request),
                    request.CancellationToken);
                activeTask = loadTask;
                return loadTask;
            }
        }

        public bool IsCurrent(ImageQueueCatalogLoadRequest request)
        {
            if (request == null)
            {
                return false;
            }

            lock (syncRoot)
            {
                return !disposed
                    && request.Version == loadVersion
                    && ReferenceEquals(request.Cancellation, activeCancellation)
                    && !request.CancellationToken.IsCancellationRequested;
            }
        }

        public void Complete(ImageQueueCatalogLoadRequest request)
        {
            if (request == null)
            {
                return;
            }

            CancellationTokenSource cancellation = null;
            lock (syncRoot)
            {
                if (ReferenceEquals(request.Cancellation, activeCancellation))
                {
                    activeCancellation = null;
                    activeTask = Task.CompletedTask;
                    cancellation = request.Cancellation;
                }
            }

            cancellation?.Dispose();
        }

        public Task Cancel()
        {
            CancellationTokenSource cancellation;
            Task task;
            lock (syncRoot)
            {
                cancellation = activeCancellation;
                task = activeTask;
                if (cancellation == null)
                {
                    return Task.CompletedTask;
                }

                ++loadVersion;
                activeCancellation = null;
                activeTask = Task.CompletedTask;
            }

            cancellation.Cancel();
            DisposeWhenCompleted(cancellation, task);
            return task ?? Task.CompletedTask;
        }

        public void Dispose()
        {
            CancellationTokenSource cancellation;
            Task task;
            lock (syncRoot)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                cancellation = activeCancellation;
                task = activeTask;
                activeCancellation = null;
                activeTask = Task.CompletedTask;
                if (cancellation != null)
                {
                    ++loadVersion;
                }
            }

            if (cancellation == null)
            {
                return;
            }

            cancellation.Cancel();
            DisposeWhenCompleted(cancellation, task);
        }

        private static void DisposeWhenCompleted(CancellationTokenSource cancellation, Task task)
        {
            if (task == null || task.IsCompleted)
            {
                cancellation.Dispose();
                return;
            }

            task.ContinueWith(
                _ => cancellation.Dispose(),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    [Obsolete("Use ImageQueueCatalogLoadCoordinator.", false)]
    public sealed class WpfImageQueueCatalogLoadCoordinator : ImageQueueCatalogLoadCoordinator
    {
        public WpfImageQueueCatalogLoadCoordinator(ImageQueueCatalogLoadService catalogService)
            : base(catalogService)
        {
        }
    }

    public class ImageQueueCatalogLoadRequest
    {
        private readonly string reviewStatusFilePath;

        internal ImageQueueCatalogLoadRequest(
            string imageRoot,
            string selectedImagePath,
            bool loadFirstImage,
            bool refreshDetails,
            LabelingProjectData data,
            bool isAnomalyPurpose,
            int version,
            CancellationTokenSource cancellation)
        {
            ImageRoot = imageRoot ?? string.Empty;
            SelectedImagePath = selectedImagePath ?? string.Empty;
            LoadFirstImage = loadFirstImage;
            RefreshDetails = refreshDetails;
            Data = data;
            reviewStatusFilePath = YoloImageReviewStatusService.ResolveReviewStatusFilePath(data);
            IsAnomalyPurpose = isAnomalyPurpose;
            Version = version;
            Cancellation = cancellation ?? throw new ArgumentNullException(nameof(cancellation));
        }

        public string ImageRoot { get; }

        public string SelectedImagePath { get; }

        public bool LoadFirstImage { get; }

        public bool RefreshDetails { get; }

        public LabelingProjectData Data { get; }

        internal bool MatchesData(LabelingProjectData data)
        {
            return ReferenceEquals(Data, data)
                && string.Equals(reviewStatusFilePath, YoloImageReviewStatusService.ResolveReviewStatusFilePath(data), StringComparison.OrdinalIgnoreCase);
        }

        public bool IsAnomalyPurpose { get; }

        public int Version { get; }

        internal CancellationTokenSource Cancellation { get; }

        public CancellationToken CancellationToken => Cancellation.Token;
    }

    [Obsolete("Use ImageQueueCatalogLoadRequest.", false)]
    public sealed class WpfImageQueueCatalogLoadRequest : ImageQueueCatalogLoadRequest
    {
        internal WpfImageQueueCatalogLoadRequest(
            string imageRoot,
            string selectedImagePath,
            bool loadFirstImage,
            bool refreshDetails,
            LabelingProjectData data,
            bool isAnomalyPurpose,
            int version,
            CancellationTokenSource cancellation)
            : base(
                imageRoot,
                selectedImagePath,
                loadFirstImage,
                refreshDetails,
                data,
                isAnomalyPurpose,
                version,
                cancellation)
        {
        }
    }
}
