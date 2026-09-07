using System;
using System.Windows;
using System.IO;
using System.Linq;

namespace MvcVisionSystem
{
    // Responsibility group: dataset and project transfer windows.
    // These members remain WPF Window adapters; independent policy belongs in services.
    public partial class WpfLabelingShellWindow
    {
        #region DatasetHealth
        private WpfDatasetHealthWindow datasetHealthWindow;

        private void ExecuteOpenDatasetHealthCommand()
        {
            if (datasetHealthWindow == null)
            {
                var viewModel = new WpfDatasetHealthViewModel(global.Data);
                viewModel.ConfigureVisualQaOpen(ExecuteOpenDatasetHealthImageInEditor);
                datasetHealthWindow = new WpfDatasetHealthWindow(viewModel)
                {
                    Owner = this
                };
                datasetHealthWindow.Closed += DatasetHealthWindow_Closed;
                datasetHealthWindow.ApplyThemeFrom(this);
                datasetHealthWindow.Show();
            }
            else
            {
                datasetHealthWindow.ViewModel?.Refresh(global.Data);
                datasetHealthWindow.ApplyThemeFrom(this);
                if (datasetHealthWindow.WindowState == WindowState.Minimized)
                {
                    datasetHealthWindow.WindowState = WindowState.Normal;
                }
            }

            datasetHealthWindow.Activate();
        }

        private void ExecuteOpenDatasetHealthImageInEditor(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return;
            }

            EnterLabelingWorkbenchStartView();
            if (!TryLoadImage(
                imagePath,
                populateQueue: false,
                refreshQueueDetails: false,
                refreshActiveStatus: true,
                appendLoadLog: true))
            {
                return;
            }

            datasetHealthWindow?.Close();
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }

            Activate();
            AppendLog($"Dataset Health 시각 QA에서 편집기로 이동: {imagePath}");
        }

        private void DatasetHealthWindow_Closed(object sender, EventArgs e)
        {
            if (datasetHealthWindow != null)
            {
                datasetHealthWindow.Closed -= DatasetHealthWindow_Closed;
                datasetHealthWindow = null;
            }
        }

        private void CloseDatasetHealthWindow()
        {
            datasetHealthWindow?.Close();
        }

        private void RefreshDatasetHealthWindowTheme()
        {
            datasetHealthWindow?.ApplyThemeFrom(this);
        }
        #endregion

        #region DatasetInterchange
        private readonly WpfFileDialogService datasetInterchangeFileDialogService =
            new WpfFileDialogService();
        private WpfDatasetInterchangeWindow datasetInterchangeWindow;

        private void ExecuteOpenDatasetInterchangeCommand()
        {
            if (datasetInterchangeWindow == null)
            {
                var viewModel = new WpfDatasetInterchangeViewModel(
                    global.Data,
                    recipeName: global.Recipe.Name);
                viewModel.ConfigurePickers(
                    PickDatasetInterchangeSource,
                    PickDatasetInterchangeTarget,
                    PickDatasetInterchangeImageRoot);
                datasetInterchangeWindow = new WpfDatasetInterchangeWindow(viewModel)
                {
                    Owner = this
                };
                datasetInterchangeWindow.Closed += DatasetInterchangeWindow_Closed;
                datasetInterchangeWindow.ApplyThemeFrom(this);
                datasetInterchangeWindow.Show();
            }
            else
            {
                datasetInterchangeWindow.ViewModel?.Refresh(global.Data, global.Recipe.Name);
                datasetInterchangeWindow.ApplyThemeFrom(this);
                if (datasetInterchangeWindow.WindowState == WindowState.Minimized)
                {
                    datasetInterchangeWindow.WindowState = WindowState.Normal;
                }
            }

            datasetInterchangeWindow.Activate();
        }

        private string PickDatasetInterchangeSource(
            WpfDatasetInterchangeOption operation,
            string currentPath)
        {
            if (operation?.SourceIsDirectory == true)
            {
                return datasetInterchangeFileDialogService.TryPickFolder(
                    datasetInterchangeWindow,
                    "\uC678\uBD80 \uC5B4\uB178\uD14C\uC774\uC158 \uD3F4\uB354 \uC120\uD0DD",
                    currentPath,
                    out string folderPath)
                    ? folderPath
                    : currentPath;
            }

            string filter = operation?.Capability.FormatKey.Contains("cvat", StringComparison.Ordinal) == true
                ? "CVAT archive (*.zip)|*.zip|All files (*.*)|*.*"
                : "JSON annotation (*.json)|*.json|All files (*.*)|*.*";
            return datasetInterchangeFileDialogService.TryPickFile(
                datasetInterchangeWindow,
                "\uC678\uBD80 \uC5B4\uB178\uD14C\uC774\uC158 \uC120\uD0DD",
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
                return datasetInterchangeFileDialogService.TryPickFolder(
                    datasetInterchangeWindow,
                    "Pascal VOC \uB0B4\uBCF4\uB0B4\uAE30 \uD3F4\uB354 \uC120\uD0DD",
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
            return datasetInterchangeFileDialogService.TryPickSaveFile(
                datasetInterchangeWindow,
                "\uB0B4\uBCF4\uB0B4\uAE30 \uB300\uC0C1 \uC120\uD0DD",
                filter,
                currentPath,
                extension,
                out string filePath)
                ? filePath
                : currentPath;
        }

        private string PickDatasetInterchangeImageRoot(string currentPath)
            => datasetInterchangeFileDialogService.TryPickFolder(
                datasetInterchangeWindow,
                "\uC6D0\uBCF8 \uC774\uBBF8\uC9C0 \uD3F4\uB354 \uC120\uD0DD",
                currentPath,
                out string folderPath)
                ? folderPath
                : currentPath;

        private void DatasetInterchangeWindow_Closed(object sender, EventArgs e)
        {
            if (datasetInterchangeWindow != null)
            {
                datasetInterchangeWindow.Closed -= DatasetInterchangeWindow_Closed;
                datasetInterchangeWindow = null;
            }
        }

        private void CloseDatasetInterchangeWindow()
        {
            datasetInterchangeWindow?.Close();
        }

        private void RefreshDatasetInterchangeWindowTheme()
        {
            datasetInterchangeWindow?.ApplyThemeFrom(this);
        }
        #endregion

        #region ProjectArchiveCommands
        private readonly PortableProjectArchiveService portableProjectArchiveService =
            new PortableProjectArchiveService();

        private void ExecuteExportProjectArchiveCommand()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            string recipeName = GetCurrentRecipeName();
            string configPath = GetCurrentRecipeConfigPath();
            string datasetRoot = global.Data?.OutputRootPath ?? string.Empty;
            WpfProjectArchivePreflightResult preflight = WpfProjectArchivePreflightService.Check(
                WpfProjectArchiveOperation.Export,
                BuildApplicationCloseState(),
                recipeName,
                configPath,
                datasetRoot);
            if (!preflight.CanProceed)
            {
                SetProjectConfigStatus(preflight.StatusText);
                return;
            }

            string suggestedPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                recipeName + ".ovl-project.zip");
            if (!fileDialogService.TryPickSaveFile(
                this,
                "프로젝트 아카이브 내보내기",
                "OpenVisionLab 프로젝트 (*.ovl-project.zip)|*.ovl-project.zip|ZIP 아카이브 (*.zip)|*.zip",
                suggestedPath,
                ".zip",
                out string archivePath))
            {
                return;
            }

            if (isApplicationCloseApproved)
            {
                return;
            }

            try
            {
                WpfProjectArchiveExportResult result = portableProjectArchiveService.Export(
                    recipeName,
                    GetCurrentRecipeConfigDirectory(),
                    datasetRoot,
                    archivePath);
                string referenceText = result.ExternalReferenceCount > 0
                    ? $" / 외부 참조 {result.ExternalReferenceCount}개는 경로만 기록"
                    : string.Empty;
                SetProjectConfigStatus(
                    $"프로젝트 아카이브 완료: {Path.GetFileName(result.ArchivePath)} / 파일 {result.FileCount}개{referenceText}");
                AppendLog($"프로젝트 아카이브 내보내기: {result.ArchivePath}");
            }
            catch (Exception ex)
            {
                SetProjectConfigStatus("프로젝트 아카이브 내보내기 실패: " + ex.Message);
                AppendLog("프로젝트 아카이브 내보내기 실패: " + ex.Message);
            }
        }

        private void ExecuteImportProjectArchiveCommand()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            WpfProjectArchivePreflightResult preflight = WpfProjectArchivePreflightService.Check(
                WpfProjectArchiveOperation.Import,
                BuildApplicationCloseState());
            if (!preflight.CanProceed)
            {
                SetProjectConfigStatus(preflight.StatusText);
                return;
            }

            if (!TryPickFile(
                "프로젝트 아카이브 가져오기",
                "OpenVisionLab 프로젝트 (*.ovl-project.zip;*.zip)|*.ovl-project.zip;*.zip",
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                out string archivePath))
            {
                return;
            }

            string defaultDatasetParent = ResolveProjectArchiveDatasetParent();
            if (!TryPickFolder(
                "가져온 데이터셋을 만들 상위 폴더 선택",
                defaultDatasetParent,
                out string datasetParent))
            {
                return;
            }

            if (isApplicationCloseApproved)
            {
                return;
            }

            try
            {
                WpfProjectArchiveImportResult result = portableProjectArchiveService.Import(
                    archivePath,
                    ProjectRecipeService.GetRecipeRootDirectory(),
                    datasetParent);
                PopulateProjectRecipeList(result.RecipeName);
                ProjectConfigViewModel?.SelectRecipeFromList(result.RecipeName);
                string referenceText = result.ExternalReferenceCount > 0
                    ? $" 외부 실행기/가중치 참조 {result.ExternalReferenceCount}개는 이 PC에서 다시 확인해야 합니다."
                    : string.Empty;
                SetProjectConfigStatus(
                    $"가져오기 완료: {result.RecipeName}. 자동 적용하지 않았습니다. 목록에서 `적용`을 누르세요.{referenceText}");
                AppendLog(
                    $"프로젝트 아카이브 가져오기: {result.ArchivePath} -> {result.RecipeDirectory} / {result.DatasetRootPath}");
            }
            catch (Exception ex)
            {
                SetProjectConfigStatus("프로젝트 아카이브 가져오기 실패: " + ex.Message);
                AppendLog("프로젝트 아카이브 가져오기 실패: " + ex.Message);
            }
        }

        private string ResolveProjectArchiveDatasetParent()
        {
            string outputRoot = global.Data?.OutputRootPath ?? string.Empty;
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
        #endregion

    }
}
