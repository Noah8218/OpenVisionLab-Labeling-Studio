using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    public sealed class WpfImageDecodePreloadService
    {
        private readonly int[] adjacentOffsets;
        private int version;
        private Task preloadTask = Task.CompletedTask;

        public WpfImageDecodePreloadService()
            : this(new[] { 1, -1, 2, -2 })
        {
        }

        public WpfImageDecodePreloadService(int[] adjacentOffsets)
        {
            this.adjacentOffsets = adjacentOffsets == null || adjacentOffsets.Length == 0
                ? new[] { 1, -1, 2, -2 }
                : adjacentOffsets.ToArray();
        }

        public Task CurrentTask => preloadTask ?? Task.CompletedTask;

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
            WpfImageDecodeCacheService cacheService,
            Func<string, bool> fileExists,
            Func<string, WpfCachedDecodedImage> decodeImage)
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

            // Versioned tasks keep rapid queue clicks cheap: stale decodes are discarded instead of updating shared cache state.
            preloadTask = Task.Run(() =>
            {
                foreach (string preloadPath in preloadPaths)
                {
                    if (taskVersion != Volatile.Read(ref version) || cacheService.IsCached(preloadPath))
                    {
                        return;
                    }

                    WpfCachedDecodedImage decoded = decodeImage(preloadPath);
                    if (taskVersion != Volatile.Read(ref version))
                    {
                        decoded?.Dispose();
                        return;
                    }

                    if (decoded != null)
                    {
                        cacheService.Store(decoded);
                    }
                }
            });
        }

        public void CancelAndWait(TimeSpan timeout)
        {
            Interlocked.Increment(ref version);
            WaitForCurrent(timeout);
        }

        public void WaitForCurrent(TimeSpan timeout)
        {
            Task task = CurrentTask;
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
    }
}
