using MvcVisionSystem._1._Core;
using System;
using System.IO;
using System.Windows;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns dataset-health and dataset-interchange child windows.
    /// The shell supplies current data, recipe, path, and editor callbacks;
    /// this adapter owns WPF window lifetime and file-picker callbacks.
    /// </summary>
    internal sealed class DatasetTransferWindowHost : IDisposable
    {
        private readonly Window owner;
        private readonly WpfFileDialogService fileDialogService;
        private readonly Func<LabelingProjectData> captureData;
        private readonly Func<string> captureRecipeName;
        private readonly Func<string> captureOutputRootPath;
        private readonly Func<string, bool> openDatasetHealthImageInEditor;
        private WpfDatasetHealthWindow datasetHealthWindow;
        private WpfDatasetInterchangeWindow datasetInterchangeWindow;
        private bool isDisposed;

        internal DatasetTransferWindowHost(
            Window owner,
            WpfFileDialogService fileDialogService,
            Func<LabelingProjectData> captureData,
            Func<string> captureRecipeName,
            Func<string> captureOutputRootPath,
            Func<string, bool> openDatasetHealthImageInEditor)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.fileDialogService = fileDialogService ?? throw new ArgumentNullException(nameof(fileDialogService));
            this.captureData = captureData ?? throw new ArgumentNullException(nameof(captureData));
            this.captureRecipeName = captureRecipeName ?? throw new ArgumentNullException(nameof(captureRecipeName));
            this.captureOutputRootPath = captureOutputRootPath ?? throw new ArgumentNullException(nameof(captureOutputRootPath));
            this.openDatasetHealthImageInEditor = openDatasetHealthImageInEditor ?? throw new ArgumentNullException(nameof(openDatasetHealthImageInEditor));
        }

        internal void ShowDatasetHealth()
        {
            if (isDisposed)
            {
                return;
            }

            if (datasetHealthWindow == null)
            {
                var viewModel = new WpfDatasetHealthViewModel(captureData());
                viewModel.ConfigureVisualQaOpen(imagePath => openDatasetHealthImageInEditor(imagePath));
                datasetHealthWindow = new WpfDatasetHealthWindow(viewModel)
                {
                    Owner = owner
                };
                datasetHealthWindow.Closed += DatasetHealthWindow_Closed;
                datasetHealthWindow.ApplyThemeFrom(owner);
                datasetHealthWindow.Show();
            }
            else
            {
                datasetHealthWindow.ViewModel?.Refresh(captureData());
                datasetHealthWindow.ApplyThemeFrom(owner);
                RestoreIfMinimized(datasetHealthWindow);
            }

            datasetHealthWindow.Activate();
        }

        internal void ShowDatasetInterchange()
        {
            if (isDisposed)
            {
                return;
            }

            if (datasetInterchangeWindow == null)
            {
                var viewModel = new WpfDatasetInterchangeViewModel(
                    captureData(),
                    recipeName: captureRecipeName() ?? string.Empty);
                viewModel.ConfigurePickers(
                    PickDatasetInterchangeSource,
                    PickDatasetInterchangeTarget,
                    PickDatasetInterchangeImageRoot);
                datasetInterchangeWindow = new WpfDatasetInterchangeWindow(viewModel)
                {
                    Owner = owner
                };
                datasetInterchangeWindow.Closed += DatasetInterchangeWindow_Closed;
                datasetInterchangeWindow.ApplyThemeFrom(owner);
                datasetInterchangeWindow.Show();
            }
            else
            {
                datasetInterchangeWindow.ViewModel?.Refresh(
                    captureData(),
                    captureRecipeName() ?? string.Empty);
                datasetInterchangeWindow.ApplyThemeFrom(owner);
                RestoreIfMinimized(datasetInterchangeWindow);
            }

            datasetInterchangeWindow.Activate();
        }

        internal void RefreshTheme()
        {
            if (isDisposed)
            {
                return;
            }

            datasetHealthWindow?.ApplyThemeFrom(owner);
            datasetInterchangeWindow?.ApplyThemeFrom(owner);
        }

        internal void CloseDatasetHealth()
        {
            datasetHealthWindow?.Close();
        }

        internal void CloseDatasetInterchange()
        {
            datasetInterchangeWindow?.Close();
        }

        internal string ResolveProjectArchiveDatasetParent()
        {
            string outputRoot = captureOutputRootPath() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(outputRoot))
            {
                string parent = Path.GetDirectoryName(outputRoot.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar));
                if (!string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent))
                {
                    return parent;
                }
            }

            return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;
            CloseDatasetHealth();
            CloseDatasetInterchange();
            DetachDatasetHealthWindow();
            DetachDatasetInterchangeWindow();
        }

        private string PickDatasetInterchangeSource(
            WpfDatasetInterchangeOption operation,
            string currentPath)
        {
            if (operation?.SourceIsDirectory == true)
            {
                return fileDialogService.TryPickFolder(
                    datasetInterchangeWindow,
                    "외부 어노테이션 폴더 선택",
                    currentPath,
                    out string folderPath)
                    ? folderPath
                    : currentPath;
            }

            string filter = operation?.Capability.FormatKey.Contains("cvat", StringComparison.Ordinal) == true
                ? "CVAT archive (*.zip)|*.zip|All files (*.*)|*.*"
                : "JSON annotation (*.json)|*.json|All files (*.*)|*.*";
            return fileDialogService.TryPickFile(
                datasetInterchangeWindow,
                "외부 어노테이션 선택",
                filter,
                currentPath,
                out string filePath)
                ? filePath
                : currentPath;
        }

        private string PickDatasetInterchangeTarget(
            WpfDatasetInterchangeOption operation,
            string currentPath)
        {
            if (operation?.TargetIsDirectory == true)
            {
                return fileDialogService.TryPickFolder(
                    datasetInterchangeWindow,
                    "Pascal VOC 내보내기 폴더 선택",
                    currentPath,
                    out string folderPath)
                    ? folderPath
                    : currentPath;
            }

            bool isArchive = operation?.Capability.FormatKey.Contains("archive", StringComparison.Ordinal) == true;
            string filter = isArchive
                ? "ZIP archive (*.zip)|*.zip"
                : "JSON file (*.json)|*.json";
            string extension = isArchive ? ".zip" : ".json";
            return fileDialogService.TryPickSaveFile(
                datasetInterchangeWindow,
                "내보내기 대상 선택",
                filter,
                currentPath,
                extension,
                out string filePath)
                ? filePath
                : currentPath;
        }

        private string PickDatasetInterchangeImageRoot(string currentPath)
            => fileDialogService.TryPickFolder(
                datasetInterchangeWindow,
                "원본 이미지 폴더 선택",
                currentPath,
                out string folderPath)
                ? folderPath
                : currentPath;

        private void DatasetHealthWindow_Closed(object sender, EventArgs e)
        {
            if (sender is WpfDatasetHealthWindow closedWindow
                && ReferenceEquals(datasetHealthWindow, closedWindow))
            {
                DetachDatasetHealthWindow();
            }
        }

        private void DatasetInterchangeWindow_Closed(object sender, EventArgs e)
        {
            if (sender is WpfDatasetInterchangeWindow closedWindow
                && ReferenceEquals(datasetInterchangeWindow, closedWindow))
            {
                DetachDatasetInterchangeWindow();
            }
        }

        private void DetachDatasetHealthWindow()
        {
            if (datasetHealthWindow == null)
            {
                return;
            }

            datasetHealthWindow.Closed -= DatasetHealthWindow_Closed;
            datasetHealthWindow = null;
        }

        private void DetachDatasetInterchangeWindow()
        {
            if (datasetInterchangeWindow == null)
            {
                return;
            }

            datasetInterchangeWindow.Closed -= DatasetInterchangeWindow_Closed;
            datasetInterchangeWindow = null;
        }

        private static void RestoreIfMinimized(Window window)
        {
            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }
        }
    }
}
