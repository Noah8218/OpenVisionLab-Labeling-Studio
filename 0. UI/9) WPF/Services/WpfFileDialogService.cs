using Microsoft.Win32;
using System.IO;
using System.Windows;

namespace MvcVisionSystem
{
    public sealed class WpfFileDialogService
    {
        public bool TryPickFile(Window owner, string title, string filter, string currentPath, out string selectedPath)
        {
            selectedPath = string.Empty;
            var dialog = new OpenFileDialog
            {
                Title = title,
                Filter = filter,
                CheckFileExists = true,
                Multiselect = false,
                InitialDirectory = ResolveInitialDirectory(currentPath)
            };

            if (dialog.ShowDialog(owner) != true || string.IsNullOrWhiteSpace(dialog.FileName))
            {
                return false;
            }

            selectedPath = dialog.FileName;
            return true;
        }

        public bool TryPickFolder(Window owner, string title, string currentPath, out string selectedPath)
        {
            selectedPath = string.Empty;
            var dialog = new OpenFolderDialog
            {
                Title = title,
                InitialDirectory = ResolveInitialDirectory(currentPath)
            };

            if (dialog.ShowDialog(owner) != true || string.IsNullOrWhiteSpace(dialog.FolderName))
            {
                return false;
            }

            selectedPath = dialog.FolderName;
            return true;
        }

        // Keep dialog path normalization out of the shell so future picker changes stay localized.
        public static string ResolveInitialDirectory(string currentPath)
        {
            if (string.IsNullOrWhiteSpace(currentPath))
            {
                return string.Empty;
            }

            if (Directory.Exists(currentPath))
            {
                return Path.GetFullPath(currentPath);
            }

            string directory = Path.GetDirectoryName(currentPath);
            return !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory)
                ? directory
                : string.Empty;
        }
    }
}