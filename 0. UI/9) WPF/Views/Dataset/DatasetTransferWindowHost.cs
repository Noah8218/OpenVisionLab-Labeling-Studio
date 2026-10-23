using MvcVisionSystem._1._Core;
using System;
using System.IO;
using System.Windows;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns dataset-health and dataset-interchange child windows and the inline interchange session.
    /// The shell supplies current data, recipe, path, and editor callbacks;
    /// this adapter owns WPF window lifetime and file-picker callbacks.
    /// </summary>
    internal sealed class DatasetTransferWindowHost : IDisposable
    {
        private readonly Window owner;
        private readonly FrameworkElement ownerContent;
        private DialogBackdropScope interchangeBackdropScope;
        private readonly WpfFileDialogService fileDialogService;
        private readonly Func<LabelingProjectData> captureData;
        private readonly Func<string> captureRecipeName;
        private readonly Func<string> captureOutputRootPath;
        private readonly Func<string, bool> openDatasetHealthImageInEditor;
        private WpfDatasetHealthWindow datasetHealthWindow;
        private WpfDatasetInterchangeWindow datasetInterchangeWindow;
        private WpfDatasetInterchangeViewModel inlineInterchangeViewModel;
        private bool isDisposed;

        internal DatasetTransferWindowHost(
            Window owner,
            FrameworkElement ownerContent,
            WpfFileDialogService fileDialogService,
            Func<LabelingProjectData> captureData,
            Func<string> captureRecipeName,
            Func<string> captureOutputRootPath,
            Func<string, bool> openDatasetHealthImageInEditor)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.ownerContent = ownerContent ?? throw new ArgumentNullException(nameof(ownerContent));
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

        internal void ShowDatasetInterchange(string direction = null)
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
                SelectInterchangeDirection(viewModel, direction);
                viewModel.ConfigurePickers(
                    (operation, current) => PickDatasetInterchangeSource(datasetInterchangeWindow ?? owner, operation, current),
                    (operation, current) => PickDatasetInterchangeTarget(datasetInterchangeWindow ?? owner, operation, current),
                    current => PickDatasetInterchangeImageRoot(datasetInterchangeWindow ?? owner, current));
                datasetInterchangeWindow = new WpfDatasetInterchangeWindow(viewModel)
                {
                    Owner = owner
                };
                datasetInterchangeWindow.Closed += DatasetInterchangeWindow_Closed;
                datasetInterchangeWindow.ApplyThemeFrom(owner);
                try
                {
                    interchangeBackdropScope = new DialogBackdropScope(datasetInterchangeWindow, ownerContent, managesOwnerInput: true);
                    datasetInterchangeWindow.Show();
                }
                catch
                {
                    WpfDatasetInterchangeWindow failedWindow = datasetInterchangeWindow;
                    DetachDatasetInterchangeWindow();
                    failedWindow?.Close();
                    throw;
                }
            }
            else
            {
                datasetInterchangeWindow.ViewModel?.Refresh(
                    captureData(),
                    captureRecipeName() ?? string.Empty);
                SelectInterchangeDirection(datasetInterchangeWindow.ViewModel, direction);
                datasetInterchangeWindow.ApplyThemeFrom(owner);
                RestoreIfMinimized(datasetInterchangeWindow);
            }

            datasetInterchangeWindow.Activate();
        }

        private static void SelectInterchangeDirection(WpfDatasetInterchangeViewModel viewModel, string direction)
        {
            if (string.Equals(direction, "Import", StringComparison.OrdinalIgnoreCase)) viewModel?.SelectDirection(isImport: true);
            else if (string.Equals(direction, "Export", StringComparison.OrdinalIgnoreCase)) viewModel?.SelectDirection(isImport: false);
        }

        internal WpfDatasetInterchangeViewModel GetInlineInterchangeViewModel()
        {
            if (isDisposed) return null;
            if (inlineInterchangeViewModel == null)
            {
                inlineInterchangeViewModel = new WpfDatasetInterchangeViewModel(captureData(), recipeName: captureRecipeName() ?? string.Empty);
                inlineInterchangeViewModel.ConfigurePickers(
                    (operation, current) => PickDatasetInterchangeSource(owner, operation, current),
                    (operation, current) => PickDatasetInterchangeTarget(owner, operation, current),
                    current => PickDatasetInterchangeImageRoot(owner, current));
            }

            return inlineInterchangeViewModel;
        }

        internal void RefreshInlineInterchange()
            => inlineInterchangeViewModel?.Refresh(captureData(), captureRecipeName() ?? string.Empty);

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
            inlineInterchangeViewModel?.Dispose();
            inlineInterchangeViewModel = null;
        }

        private string PickDatasetInterchangeSource(
            Window pickerOwner,
            WpfDatasetInterchangeOption operation,
            string currentPath)
        {
            if (operation?.SourceIsDirectory == true)
            {
                return fileDialogService.TryPickFolder(
                    pickerOwner,
                    OpenVisionLab.OpenVisionLanguageService.T("WpfDataset.Picker.ExternalFolder"),
                    currentPath,
                    out string folderPath)
                    ? folderPath
                    : currentPath;
            }

            string filter = operation?.Capability.FormatKey.Contains("cvat", StringComparison.Ordinal) == true
                ? "CVAT archive (*.zip)|*.zip|All files (*.*)|*.*"
                : "JSON annotation (*.json)|*.json|All files (*.*)|*.*";
            return fileDialogService.TryPickFile(
                pickerOwner,
                OpenVisionLab.OpenVisionLanguageService.T("WpfDataset.Picker.ExternalLabels"),
                filter,
                currentPath,
                out string filePath)
                ? filePath
                : currentPath;
        }

        private string PickDatasetInterchangeTarget(
            Window pickerOwner,
            WpfDatasetInterchangeOption operation,
            string currentPath)
        {
            if (operation?.TargetIsDirectory == true)
            {
                return fileDialogService.TryPickFolder(
                    pickerOwner,
                    OpenVisionLab.OpenVisionLanguageService.T("WpfDataset.Picker.ExportFolder"),
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
                pickerOwner,
                OpenVisionLab.OpenVisionLanguageService.T("WpfDataset.Picker.ExportTarget"),
                filter,
                currentPath,
                extension,
                out string filePath)
                ? filePath
                : currentPath;
        }

        private string PickDatasetInterchangeImageRoot(Window pickerOwner, string currentPath)
            => fileDialogService.TryPickFolder(
                pickerOwner,
                OpenVisionLab.OpenVisionLanguageService.T("WpfDataset.Picker.ImageFolder"),
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
            interchangeBackdropScope?.Dispose();
            interchangeBackdropScope = null;
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
