using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MvcVisionSystem
{
    public sealed class WpfImageQueueSelectionService
    {
        private static readonly string[] ImageExtensions = { ".bmp", ".gif", ".jpg", ".jpeg", ".png", ".tif", ".tiff" };

        public List<string> EnumerateImageFiles(string imageRoot)
        {
            if (string.IsNullOrWhiteSpace(imageRoot) || !Directory.Exists(imageRoot))
            {
                return new List<string>();
            }

            return Directory
                .EnumerateFiles(imageRoot)
                .Where(HasSupportedExtension)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public IReadOnlyList<WpfImageQueueItem> CreateShellItems(IEnumerable<string> imagePaths)
        {
            return (imagePaths ?? Array.Empty<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(WpfImageQueueItem.CreateShell)
                .ToList();
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