using Microsoft.Win32;
using MvcVisionSystem.Yolo;
using System;
using System.IO;
using System.Text;
using System.Windows;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

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

        public bool TryPickSaveFile(Window owner, string title, string filter, string currentPath, string defaultExtension, out string selectedPath)
            => TryPickSaveFile(owner, title, filter, currentPath, defaultExtension, out selectedPath, suggestedFileName: null);

        public bool TryPickSaveFile(
            Window owner,
            string title,
            string filter,
            string currentPath,
            string defaultExtension,
            out string selectedPath,
            string suggestedFileName)
        {
            selectedPath = string.Empty;
            var dialog = new SaveFileDialog
            {
                Title = title,
                Filter = filter,
                AddExtension = true,
                DefaultExt = defaultExtension ?? string.Empty,
                InitialDirectory = ResolveInitialDirectory(currentPath),
                FileName = File.Exists(currentPath) ? Path.GetFileName(currentPath) : suggestedFileName ?? string.Empty,
                OverwritePrompt = true
            };

            if (dialog.ShowDialog(owner) != true || string.IsNullOrWhiteSpace(dialog.FileName))
            {
                return false;
            }

            selectedPath = dialog.FileName;
            return true;
        }

        public string TrySaveUtf8Text(Window owner, string title, string filter, string content, string suggestedFileName, string defaultExtension)
            => TrySaveUtf8Text(owner, title, filter, () => content, suggestedFileName, defaultExtension);

        public string TrySaveUtf8Text(Window owner, string title, string filter, Func<string> contentFactory, string suggestedFileName, string defaultExtension)
        {
            if (!TryPickSaveFile(owner, title, filter, string.Empty, defaultExtension, out string path, suggestedFileName)) return string.Empty;
            WriteUtf8Text(path, contentFactory());
            return path;
        }

        internal static void WriteUtf8Text(string path, string content)
            => AnnotationFilePersistence.WriteAtomically(path, temporaryPath => File.WriteAllText(temporaryPath, content, new UTF8Encoding(true)));

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
