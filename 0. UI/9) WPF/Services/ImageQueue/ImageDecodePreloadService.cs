using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    public class ImageDecodePreloadService
    {
        private readonly object syncRoot = new object();
        private readonly int[] adjacentOffsets;
        private CancellationTokenSource activeCancellation;
        private int version;
        private Task preloadTask = Task.CompletedTask;

        public ImageDecodePreloadService()
            : this(new[] { 1, -1, 2, -2 })
        {
        }

        public ImageDecodePreloadService(int[] adjacentOffsets)
        {
            this.adjacentOffsets = adjacentOffsets == null || adjacentOffsets.Length == 0
                ? new[] { 1, -1, 2, -2 }
                : adjacentOffsets.ToArray();
        }

        public Task CurrentTask
        {
            get
            {
                lock (syncRoot)
                {
                    return preloadTask ?? Task.CompletedTask;
                }
            }
        }

        public IReadOnlyList<string> SelectAdjacentPreloadPaths(
            string activeImagePath,
            IEnumerable<string> orderedImagePaths,
            Func<string, bool> fileExists,
            Func<string, bool> isCached)
        {
            if (string.IsNullOrWhiteSpace(activeImagePath) || orderedImagePaths == null)
            {
                return Array.Empty<string>();
            }

            fileExists ??= _ => true;
            isCached ??= _ => false;

            List<string> orderedPaths = orderedImagePaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToList();
            int currentIndex = orderedPaths.FindIndex(path => string.Equals(path, activeImagePath, StringComparison.OrdinalIgnoreCase));
            if (currentIndex < 0)
            {
                return Array.Empty<string>();
            }

            var preloadPaths = new List<string>();
            foreach (int offset in adjacentOffsets)
            {
                int index = currentIndex + offset;
                if (index < 0 || index >= orderedPaths.Count)
                {
                    continue;
                }

                string preloadPath = orderedPaths[index];
                if (string.Equals(preloadPath, activeImagePath, StringComparison.OrdinalIgnoreCase)
                    || !fileExists(preloadPath)
                    || isCached(preloadPath))
                {
                    continue;
                }

                preloadPaths.Add(preloadPath);
            }

            return preloadPaths;
        }

        public void StartAdjacentPreload(
            string activeImagePath,
            IEnumerable<string> orderedImagePaths,
            ImageDecodeCacheService cacheService,
            Func<string, bool> fileExists,
            Func<string, CachedDecodedImage> decodeImage)
        {
            if (cacheService == null || decodeImage == null)
            {
                return;
            }

            int taskVersion = Interlocked.Increment(ref version);
            IReadOnlyList<string> preloadPaths = SelectAdjacentPreloadPaths(activeImagePath, orderedImagePaths, fileExists, cacheService.IsCached);
            if (preloadPaths.Count == 0)
            {
                return;
            }

            // Versioned and cancellable tasks keep rapid queue clicks cheap: stale decodes are discarded instead of updating shared cache state.
            var cancellation = new CancellationTokenSource();
            lock (syncRoot)
            {
                activeCancellation?.Cancel();
                activeCancellation = cancellation;
                preloadTask = Task.Run(() =>
                {
                    try
                    {
                        foreach (string preloadPath in preloadPaths)
                        {
                            if (taskVersion != Volatile.Read(ref version)
                                || cancellation.IsCancellationRequested
                                || cacheService.IsCached(preloadPath))
                            {
                                return;
                            }

                            CachedDecodedImage decoded = decodeImage(preloadPath);
                            if (taskVersion != Volatile.Read(ref version) || cancellation.IsCancellationRequested)
                            {
                                decoded?.Dispose();
                                return;
                            }

                            if (decoded != null)
                            {
                                cacheService.Store(decoded);
                            }
                        }
                    }
                    finally
                    {
                        CompletePreload(cancellation);
                    }
                });
            }
        }

        public void CancelAndWait(TimeSpan timeout)
        {
            Interlocked.Increment(ref version);
            CancellationTokenSource cancellation;
            Task task;
            lock (syncRoot)
            {
                cancellation = activeCancellation;
                task = preloadTask ?? Task.CompletedTask;
                cancellation?.Cancel();
                activeCancellation = null;
                preloadTask = Task.CompletedTask;
            }

            WaitForTask(task, timeout);
        }

        public void WaitForCurrent(TimeSpan timeout)
        {
            Task task = CurrentTask;
            WaitForTask(task, timeout);
        }

        private static void WaitForTask(Task task, TimeSpan timeout)
        {
            if (task.IsCompleted)
            {
                return;
            }

            try
            {
                task.Wait(timeout);
            }
            catch (AggregateException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private void CompletePreload(CancellationTokenSource cancellation)
        {
            lock (syncRoot)
            {
                if (ReferenceEquals(activeCancellation, cancellation))
                {
                    activeCancellation = null;
                    preloadTask = Task.CompletedTask;
                }
            }

            cancellation.Dispose();
        }
    }

    [Obsolete("Use ImageDecodePreloadService.", false)]
    public sealed class WpfImageDecodePreloadService : ImageDecodePreloadService
    {
        public WpfImageDecodePreloadService()
        {
        }

        public WpfImageDecodePreloadService(int[] adjacentOffsets)
            : base(adjacentOffsets)
        {
        }
    }
}
