using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using MvcVisionSystem.Yolo;

namespace MvcVisionSystem
{
    public sealed class WpfImageQueueOpenSelection
    {
        public WpfImageQueueItem Item { get; set; }

        public string OpenImagePath { get; set; } = string.Empty;

        public bool CanOpen => Item != null && !string.IsNullOrWhiteSpace(OpenImagePath);
    }

    public sealed class WpfImageQueueSelectionService
    {
        private static readonly string[] ImageExtensions = { ".bmp", ".gif", ".jpg", ".jpeg", ".png", ".tif", ".tiff" };

        public List<string> EnumerateImageFiles(string imageRoot)
        {
            return EnumerateImageFiles(imageRoot, CancellationToken.None);
        }

        public bool HasImageFiles(string imageRoot)
        {
            if (string.IsNullOrWhiteSpace(imageRoot) || !Directory.Exists(imageRoot))
            {
                return false;
            }

            return Directory.EnumerateFiles(imageRoot, "*", SearchOption.TopDirectoryOnly).Any(HasSupportedExtension)
                || Directory.EnumerateFiles(imageRoot, "*", SearchOption.AllDirectories).Any(HasSupportedExtension);
        }

        public List<string> EnumerateImageFiles(string imageRoot, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(imageRoot) || !Directory.Exists(imageRoot))
            {
                return new List<string>();
            }

            List<string> directImages = EnumerateImageFiles(imageRoot, SearchOption.TopDirectoryOnly, cancellationToken);
            return (directImages.Count > 0
                    ? directImages
                    : EnumerateImageFiles(imageRoot, SearchOption.AllDirectories, cancellationToken));
        }

        public List<string> InterleaveTopLevelFolderImages(
            string imageRoot,
            IEnumerable<string> imagePaths,
            CancellationToken cancellationToken)
        {
            string rootPath = string.IsNullOrWhiteSpace(imageRoot)
                ? string.Empty
                : Path.GetFullPath(imageRoot);
            List<string> orderedPaths = (imagePaths ?? Enumerable.Empty<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (string.IsNullOrWhiteSpace(rootPath) || orderedPaths.Count < 2)
            {
                return orderedPaths;
            }

            // ponytail: only top-level child folders are interleaved; preserve nested source ordering unless explicit grouping is needed.
            List<Queue<string>> folderQueues = orderedPaths
                .GroupBy(path => ResolveTopLevelFolderName(rootPath, path), StringComparer.OrdinalIgnoreCase)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .Select(group => new Queue<string>(group))
                .ToList();
            if (folderQueues.Count < 2 || folderQueues.Any(queue => queue.Count == 0))
            {
                return orderedPaths;
            }

            var interleaved = new List<string>(orderedPaths.Count);
            while (folderQueues.Any(queue => queue.Count > 0))
            {
                foreach (Queue<string> folderQueue in folderQueues)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (folderQueue.Count > 0)
                    {
                        interleaved.Add(folderQueue.Dequeue());
                    }
                }
            }

            return interleaved;
        }

        private List<string> EnumerateImageFiles(
            string imageRoot,
            SearchOption searchOption,
            CancellationToken cancellationToken)
        {
            var imagePaths = new List<string>();
            foreach (string imagePath in Directory.EnumerateFiles(imageRoot, "*", searchOption))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (HasSupportedExtension(imagePath))
                {
                    imagePaths.Add(imagePath);
                }
            }

            imagePaths.Sort(StringComparer.OrdinalIgnoreCase);
            return imagePaths;
        }

        private static string ResolveTopLevelFolderName(string rootPath, string imagePath)
        {
            string relativePath = Path.GetRelativePath(rootPath, imagePath ?? string.Empty);
            int separatorIndex = relativePath.IndexOfAny(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar });
            return separatorIndex > 0
                ? relativePath.Substring(0, separatorIndex)
                : string.Empty;
        }

        public IReadOnlyList<WpfImageQueueItem> CreateShellItems(IEnumerable<string> imagePaths)
        {
            return CreateShellItems(imagePaths, CancellationToken.None);
        }

        public IReadOnlyList<WpfImageQueueItem> CreateShellItems(
            IEnumerable<string> imagePaths,
            CancellationToken cancellationToken)
        {
            return CreateCatalogEntries(imagePaths, cancellationToken)
                .Select(WpfImageQueueItem.CreateShell)
                .ToList();
        }

        public IReadOnlyList<WpfImageQueueCatalogEntry> CreateCatalogEntries(
            IEnumerable<string> imagePaths,
            CancellationToken cancellationToken)
        {
            var entries = new List<WpfImageQueueCatalogEntry>();
            foreach (string imagePath in imagePaths ?? Array.Empty<string>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!string.IsNullOrWhiteSpace(imagePath))
                {
                    entries.Add(WpfImageQueueCatalogEntry.Create(imagePath));
                }
            }

            return entries;
        }

        public IReadOnlyList<WpfImageQueueCatalogEntry> CreateCatalogEntries(IEnumerable<string> imagePaths)
        {
            return CreateCatalogEntries(imagePaths, CancellationToken.None);
        }

        public IReadOnlyList<WpfImageQueueItem> CreateShellItemsFromCatalog(
            IEnumerable<WpfImageQueueCatalogEntry> entries)
        {
            var items = new List<WpfImageQueueItem>();
            foreach (WpfImageQueueCatalogEntry entry in entries ?? Array.Empty<WpfImageQueueCatalogEntry>())
            {
                if (entry != null)
                {
                    items.Add(WpfImageQueueItem.CreateShell(entry));
                }
            }

            return items;
        }

        public WpfImageQueueItem FindItem(IEnumerable<WpfImageQueueItem> items, string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return null;
            }

            return (items ?? Array.Empty<WpfImageQueueItem>()).FirstOrDefault(item =>
                item != null && string.Equals(item.ImagePath, imagePath, StringComparison.OrdinalIgnoreCase));
        }

        public WpfImageQueueItem ResolveSelectedItem(WpfImageQueueItem selectedItem, IEnumerable<WpfImageQueueItem> items, string activeImagePath)
        {
            // Virtualized controls can briefly report null while recycling; keep selection anchored to the active image.
            return selectedItem ?? FindItem(items, activeImagePath);
        }

        public bool CanOpen(WpfImageQueueItem item)
        {
            return item != null
                && !string.IsNullOrWhiteSpace(item.ImagePath)
                && File.Exists(item.ImagePath);
        }

        public bool TryResolveOpenImagePath(WpfImageQueueItem item, CData data, out string imagePath)
        {
            imagePath = item?.ImagePath ?? string.Empty;
            if (CanOpen(item))
            {
                return true;
            }

            foreach (string candidatePath in EnumerateSavedDatasetImageCandidates(item, data))
            {
                if (File.Exists(candidatePath))
                {
                    imagePath = candidatePath;
                    return true;
                }
            }

            return false;
        }

        public WpfImageQueueOpenSelection ResolveOpenSelection(IEnumerable<WpfImageQueueItem> candidates, CData data)
        {
            foreach (WpfImageQueueItem candidate in candidates ?? Array.Empty<WpfImageQueueItem>())
            {
                if (TryResolveOpenImagePath(candidate, data, out string imagePath))
                {
                    return new WpfImageQueueOpenSelection
                    {
                        Item = candidate,
                        OpenImagePath = imagePath
                    };
                }
            }

            return new WpfImageQueueOpenSelection();
        }

        private IEnumerable<string> EnumerateSavedDatasetImageCandidates(WpfImageQueueItem item, CData data)
        {
            string fileStem = Path.GetFileNameWithoutExtension(item?.ImagePath);
            if (string.IsNullOrWhiteSpace(fileStem))
            {
                fileStem = Path.GetFileNameWithoutExtension(item?.FileName);
            }

            if (string.IsNullOrWhiteSpace(fileStem) || data == null)
            {
                yield break;
            }

            data.NormalizeOutputPaths();
            if (string.IsNullOrWhiteSpace(data.OutputRootPath))
            {
                yield break;
            }

            // Saving labels writes an image copy into one split. The queue may still
            // hold the original staging path, so reopen has to recover that split copy.
            foreach (string mode in new[] { YoloDatasetSplitService.TrainMode, YoloDatasetSplitService.ValidMode, YoloDatasetSplitService.TestMode })
            {
                string imageDirectory = Path.Combine(data.OutputRootPath, "data", mode, "images");
                foreach (string extension in ImageExtensions)
                {
                    yield return Path.Combine(imageDirectory, $"{fileStem}{extension}");
                }
            }
        }

        public bool ShouldOpen(WpfImageQueueItem item, string activeImagePath, bool skipIfAlreadyActive)
        {
            if (!CanOpen(item))
            {
                return false;
            }

            return !skipIfAlreadyActive
                || !string.Equals(item.ImagePath, activeImagePath, StringComparison.OrdinalIgnoreCase);
        }

        public bool IsSameRoot(string firstRoot, string secondRoot)
        {
            if (string.IsNullOrWhiteSpace(firstRoot) || string.IsNullOrWhiteSpace(secondRoot))
            {
                return false;
            }

            try
            {
                return string.Equals(Path.GetFullPath(firstRoot), Path.GetFullPath(secondRoot), StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception) when (firstRoot.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || secondRoot.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                return false;
            }
        }

        public bool HasSupportedExtension(string imagePath)
        {
            return !string.IsNullOrWhiteSpace(imagePath)
                && ImageExtensions.Contains(Path.GetExtension(imagePath), StringComparer.OrdinalIgnoreCase);
        }
    }
}
