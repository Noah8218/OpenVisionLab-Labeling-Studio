using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using MvcVisionSystem._1._Core;
using OpenVisionLab.Mvvm;

namespace MvcVisionSystem
{
    public sealed class WpfProjectConfigPanelViewModel : WpfObservableViewModel
    {
        private string recipeName = string.Empty;
        private string selectedRecipeName = string.Empty;
        private string configPath = string.Empty;
        private string manifestPath = string.Empty;
        private string datasetVersionText = "저장 후 생성";
        private string datasetVersionDetailText = "Recipe를 저장하면 이미지·라벨·클래스·분할의 SHA-256 버전을 기록합니다.";
        private string statusText = "현재 recipe 설정 위치를 확인하세요.";
        private string recipeRootPath = string.Empty;
        private bool isApplyRecipeEnabled = true;
        private bool isRefreshRecipeListEnabled = true;
        private bool isSaveProjectConfigEnabled;
        private bool isOpenProjectConfigFolderEnabled = true;
        private bool isProjectArchiveCommandEnabled = true;
        private ICommand applyRecipeCommand = new RelayCommand(NoOpCommand);
        private ICommand refreshRecipeListCommand = new RelayCommand(NoOpCommand);
        private ICommand saveProjectConfigCommand = new RelayCommand(NoOpCommand);
        private ICommand openProjectConfigFolderCommand = new RelayCommand(NoOpCommand);
        private ICommand exportProjectArchiveCommand = new RelayCommand(NoOpCommand);
        private ICommand importProjectArchiveCommand = new RelayCommand(NoOpCommand);
        private ICommand recipeSelectionChangedCommand = new RelayCommand<object>(NoOpSelectionCommand);
        private ProjectRecipeSessionService projectRecipeSessionService;
        private ProjectRecipeApplyWorkflowService projectRecipeApplyWorkflowService;
        private ProjectArchiveWorkflowService projectArchiveWorkflowService;
        private Func<LabelingApplicationState> applicationProvider;
        private Func<bool> closeApprovedProvider;
        private Func<ProjectConfigNavigationCallbacks> navigationCallbacksProvider;
        private Func<ProjectConfigArchiveCallbacks> archiveCallbacksProvider;
        private Func<string, bool> recipeSelectionGuard = _ => true;
        private int recipeApplyInFlight;

        public string ViewName => nameof(WpfProjectConfigPanel);

        public ObservableCollection<string> RecipeNames { get; } = new ObservableCollection<string>();

        public event Action<string> ProjectConfigSaved;

        public event Action<string, ProjectRecipeApplyResult> ProjectRecipeApplied;

        public event Action<string> WorkflowError;

        public ICommand ApplyRecipeCommand
        {
            get => applyRecipeCommand;
            private set => SetProperty(ref applyRecipeCommand, value);
        }

        public ICommand RefreshRecipeListCommand
        {
            get => refreshRecipeListCommand;
            private set => SetProperty(ref refreshRecipeListCommand, value);
        }

        public ICommand SaveProjectConfigCommand
        {
            get => saveProjectConfigCommand;
            private set => SetProperty(ref saveProjectConfigCommand, value);
        }

        public ICommand OpenProjectConfigFolderCommand
        {
            get => openProjectConfigFolderCommand;
            private set => SetProperty(ref openProjectConfigFolderCommand, value);
        }

        public ICommand ExportProjectArchiveCommand
        {
            get => exportProjectArchiveCommand;
            private set => SetProperty(ref exportProjectArchiveCommand, value);
        }

        public ICommand ImportProjectArchiveCommand
        {
            get => importProjectArchiveCommand;
            private set => SetProperty(ref importProjectArchiveCommand, value);
        }

        public ICommand RecipeSelectionChangedCommand
        {
            get => recipeSelectionChangedCommand;
            private set => SetProperty(ref recipeSelectionChangedCommand, value);
        }
        public string RecipeName
        {
            get => recipeName;
            set
            {
                if (SetProperty(ref recipeName, value ?? string.Empty))
                {
                    RefreshConfigPath();
                }
            }
        }

        public string SelectedRecipeName
        {
            get => selectedRecipeName;
            set => SetProperty(ref selectedRecipeName, value ?? string.Empty);
        }

        public string ConfigPath
        {
            get => configPath;
            private set => SetProperty(ref configPath, value ?? string.Empty);
        }

        public string ManifestPath
        {
            get => manifestPath;
            private set => SetProperty(ref manifestPath, value ?? string.Empty);
        }

        public string DatasetVersionText
        {
            get => datasetVersionText;
            private set => SetProperty(ref datasetVersionText, value ?? string.Empty);
        }

        public string DatasetVersionDetailText
        {
            get => datasetVersionDetailText;
            private set => SetProperty(ref datasetVersionDetailText, value ?? string.Empty);
        }

        public string StatusText
        {
            get => statusText;
            set => SetProperty(ref statusText, value ?? string.Empty);
        }

        public bool IsApplyRecipeEnabled
        {
            get => isApplyRecipeEnabled;
            private set => SetProperty(ref isApplyRecipeEnabled, value);
        }

        public bool IsRefreshRecipeListEnabled
        {
            get => isRefreshRecipeListEnabled;
            private set => SetProperty(ref isRefreshRecipeListEnabled, value);
        }

        public bool IsSaveProjectConfigEnabled
        {
            get => isSaveProjectConfigEnabled;
            private set => SetProperty(ref isSaveProjectConfigEnabled, value);
        }

        public bool IsOpenProjectConfigFolderEnabled
        {
            get => isOpenProjectConfigFolderEnabled;
            private set => SetProperty(ref isOpenProjectConfigFolderEnabled, value);
        }

        public bool IsProjectArchiveCommandEnabled
        {
            get => isProjectArchiveCommandEnabled;
            private set => SetProperty(ref isProjectArchiveCommandEnabled, value);
        }

        public void ConfigureCommands(
            Action applyRecipe,
            Action refreshRecipeList,
            Action saveProjectConfig,
            Action openProjectConfigFolder,
            Action exportProjectArchive,
            Action importProjectArchive,
            Action<object> recipeSelectionChanged)
        {
            // Recipe selection passes the selected value instead of WPF EventArgs so the ViewModel remains reusable.
            ApplyRecipeCommand = new RelayCommand(applyRecipe ?? NoOpCommand);
            RefreshRecipeListCommand = new RelayCommand(refreshRecipeList ?? NoOpCommand);
            SaveProjectConfigCommand = new RelayCommand(saveProjectConfig ?? NoOpCommand);
            OpenProjectConfigFolderCommand = new RelayCommand(openProjectConfigFolder ?? NoOpCommand);
            ExportProjectArchiveCommand = new RelayCommand(exportProjectArchive ?? NoOpCommand);
            ImportProjectArchiveCommand = new RelayCommand(importProjectArchive ?? NoOpCommand);
            RecipeSelectionChangedCommand = new RelayCommand<object>(recipeSelectionChanged ?? NoOpSelectionCommand);
        }

        public void ConfigureRecipeSelectionWorkflow(Func<string, bool> selectionGuard)
        {
            recipeSelectionGuard = selectionGuard ?? (_ => true);
            RecipeSelectionChangedCommand = new RelayCommand<object>(ExecuteRecipeSelectionChanged);
        }

        private void ExecuteRecipeSelectionChanged(object selectedItem)
        {
            string recipeName = selectedItem as string ?? SelectedRecipeName;
            if (string.IsNullOrWhiteSpace(recipeName))
            {
                return;
            }

            recipeName = recipeName.Trim();
            if (!recipeSelectionGuard(recipeName))
            {
                return;
            }

            SelectRecipeFromList(recipeName);
        }

        public void ConfigurePersistenceWorkflow(
            ProjectRecipeSessionService sessionService,
            ProjectRecipeApplyWorkflowService applyWorkflowService,
            Func<LabelingApplicationState> dataProvider,
            Func<bool> closeApprovedProvider)
        {
            projectRecipeSessionService = sessionService
                ?? throw new ArgumentNullException(nameof(sessionService));
            projectRecipeApplyWorkflowService = applyWorkflowService
                ?? throw new ArgumentNullException(nameof(applyWorkflowService));
            applicationProvider = dataProvider
                ?? throw new ArgumentNullException(nameof(dataProvider));
            this.closeApprovedProvider = closeApprovedProvider
                ?? throw new ArgumentNullException(nameof(closeApprovedProvider));
            ApplyRecipeCommand = new RelayCommand(ExecuteApplyRecipe);
            SaveProjectConfigCommand = new RelayCommand(ExecuteSaveProjectConfig);
        }

        public void ConfigureNavigationWorkflow(
            Func<ProjectConfigNavigationCallbacks> callbacksProvider)
        {
            navigationCallbacksProvider = callbacksProvider
                ?? throw new ArgumentNullException(nameof(callbacksProvider));
            RefreshRecipeListCommand = new RelayCommand(ExecuteRefreshRecipeList);
            OpenProjectConfigFolderCommand = new RelayCommand(ExecuteOpenProjectConfigFolder);
        }

        public void ConfigureArchiveWorkflow(
            ProjectArchiveWorkflowService workflowService,
            Func<ProjectConfigArchiveCallbacks> callbacksProvider)
        {
            projectArchiveWorkflowService = workflowService
                ?? throw new ArgumentNullException(nameof(workflowService));
            archiveCallbacksProvider = callbacksProvider
                ?? throw new ArgumentNullException(nameof(callbacksProvider));
            ExportProjectArchiveCommand = new RelayCommand(ExecuteExportProjectArchive);
            ImportProjectArchiveCommand = new RelayCommand(ExecuteImportProjectArchive);
        }

        private void ExecuteRefreshRecipeList()
        {
            ProjectConfigNavigationCallbacks callbacks = navigationCallbacksProvider?.Invoke();
            if (callbacks?.IsApplicationCloseApproved?.Invoke() == true)
            {
                return;
            }

            string selectedRecipeName = RecipeName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(selectedRecipeName))
            {
                selectedRecipeName = callbacks?.CurrentRecipeName?.Invoke()?.Trim() ?? string.Empty;
            }

            if (callbacks?.RefreshRecipeList?.Invoke(selectedRecipeName) == true)
            {
                StatusText = "Recipe 목록을 다시 읽었습니다. 적용할 항목을 선택하세요.";
            }
        }

        private void ExecuteOpenProjectConfigFolder()
        {
            ProjectConfigNavigationCallbacks callbacks = navigationCallbacksProvider?.Invoke();
            if (callbacks?.IsApplicationCloseApproved?.Invoke() == true)
            {
                return;
            }

            callbacks?.OpenProjectConfigFolder?.Invoke();
        }

        private void ExecuteExportProjectArchive()
        {
            ProjectConfigArchiveCallbacks callbacks = archiveCallbacksProvider?.Invoke();
            if (callbacks?.IsApplicationCloseApproved?.Invoke() == true)
            {
                return;
            }

            string recipeName = callbacks?.CurrentRecipeName?.Invoke()?.Trim() ?? string.Empty;
            string configPath = callbacks?.CurrentRecipeConfigPath?.Invoke() ?? string.Empty;
            string datasetRootPath = applicationProvider?.Invoke()?.Data?.OutputRootPath ?? string.Empty;
            WpfProjectArchivePreflightResult preflight = projectArchiveWorkflowService.CheckExport(
                callbacks?.BuildApplicationCloseState?.Invoke(),
                recipeName,
                configPath,
                datasetRootPath);
            if (preflight?.CanProceed != true)
            {
                StatusText = preflight?.StatusText ?? "프로젝트 아카이브 내보내기를 시작할 수 없습니다.";
                return;
            }

            string suggestedPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                recipeName + ".ovl-project.zip");
            string archivePath = callbacks.SelectSaveFile?.Invoke(
                "프로젝트 아카이브 내보내기",
                "OpenVisionLab 프로젝트 (*.ovl-project.zip)|*.ovl-project.zip|ZIP 아카이브 (*.zip)|*.zip",
                suggestedPath);
            if (string.IsNullOrWhiteSpace(archivePath)
                || callbacks.IsApplicationCloseApproved?.Invoke() == true)
            {
                return;
            }

            try
            {
                WpfProjectArchiveExportResult result = projectArchiveWorkflowService.Export(
                    new ProjectArchiveExportRequest
                    {
                        CloseState = callbacks.BuildApplicationCloseState?.Invoke(),
                        RecipeName = recipeName,
                        ConfigPath = configPath,
                        DatasetRootPath = datasetRootPath,
                        RecipeDirectory = callbacks.CurrentRecipeConfigDirectory?.Invoke() ?? string.Empty,
                        ArchivePath = archivePath
                    });
                string referenceText = result.ExternalReferenceCount > 0
                    ? $" / 외부 참조 {result.ExternalReferenceCount}개는 경로만 기록"
                    : string.Empty;
                StatusText = $"프로젝트 아카이브 완료: {Path.GetFileName(result.ArchivePath)} / 파일 {result.FileCount}개{referenceText}";
                callbacks.AppendLog?.Invoke($"프로젝트 아카이브 내보내기: {result.ArchivePath}");
            }
            catch (Exception ex)
            {
                string failure = "프로젝트 아카이브 내보내기 실패: " + ex.Message;
                StatusText = failure;
                WorkflowError?.Invoke(failure);
            }
        }

        private void ExecuteImportProjectArchive()
        {
            ProjectConfigArchiveCallbacks callbacks = archiveCallbacksProvider?.Invoke();
            if (callbacks?.IsApplicationCloseApproved?.Invoke() == true)
            {
                return;
            }

            WpfProjectArchivePreflightResult preflight = projectArchiveWorkflowService.CheckImport(
                callbacks?.BuildApplicationCloseState?.Invoke());
            if (preflight?.CanProceed != true)
            {
                StatusText = preflight?.StatusText ?? "프로젝트 아카이브 가져오기를 시작할 수 없습니다.";
                return;
            }

            string archivePath = callbacks.SelectFile?.Invoke(
                "프로젝트 아카이브 가져오기",
                "OpenVisionLab 프로젝트 (*.ovl-project.zip;*.zip)|*.ovl-project.zip;*.zip",
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            if (string.IsNullOrWhiteSpace(archivePath))
            {
                return;
            }

            string defaultDatasetParent = callbacks.DefaultDatasetParentDirectory?.Invoke()
                ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string datasetParent = callbacks.SelectFolder?.Invoke(
                "가져온 데이터셋을 만들 상위 폴더 선택",
                defaultDatasetParent);
            if (string.IsNullOrWhiteSpace(datasetParent)
                || callbacks.IsApplicationCloseApproved?.Invoke() == true)
            {
                return;
            }

            try
            {
                WpfProjectArchiveImportResult result = projectArchiveWorkflowService.Import(
                    new ProjectArchiveImportRequest
                    {
                        CloseState = callbacks.BuildApplicationCloseState?.Invoke(),
                        ArchivePath = archivePath,
                        RecipeRootDirectory = callbacks.RecipeRootDirectory?.Invoke() ?? string.Empty,
                        DatasetParentDirectory = datasetParent
                    });
                callbacks.ImportedRecipeLoaded?.Invoke(result.RecipeName);
                string referenceText = result.ExternalReferenceCount > 0
                    ? $" 외부 실행기/가중치 참조 {result.ExternalReferenceCount}개는 이 PC에서 다시 확인해야 합니다."
                    : string.Empty;
                StatusText = $"가져오기 완료: {result.RecipeName}. 자동 적용하지 않았습니다. 목록에서 `적용`을 누르세요.{referenceText}";
                callbacks.AppendLog?.Invoke(
                    $"프로젝트 아카이브 가져오기: {result.ArchivePath} -> {result.RecipeDirectory} / {result.DatasetRootPath}");
            }
            catch (Exception ex)
            {
                string failure = "프로젝트 아카이브 가져오기 실패: " + ex.Message;
                StatusText = failure;
                WorkflowError?.Invoke(failure);
            }
        }

        public void LoadFrom(string currentRecipeName, string rootPath)
        {
            recipeRootPath = rootPath ?? string.Empty;
            RecipeName = currentRecipeName?.Trim() ?? string.Empty;
            SelectedRecipeName = RecipeName;
            RefreshConfigPath();
        }

        public void SetRecipeList(IEnumerable<string> recipeNames, string selectedName)
        {
            RecipeNames.Clear();
            foreach (string name in recipeNames ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(name))
                {
                    RecipeNames.Add(name);
                }
            }

            SelectedRecipeName = selectedName ?? string.Empty;
        }

        public void SetDatasetVersionInfo(RecipeDatasetVersionPresentation presentation)
        {
            presentation ??= new RecipeDatasetVersionPresentation();
            DatasetVersionText = presentation.VersionText;
            DatasetVersionDetailText = presentation.DetailText;
        }

        public void SelectRecipeFromList(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            RecipeName = name.Trim();
            SelectedRecipeName = RecipeName;
            StatusText = $"목록에서 선택: {RecipeName}. 적용을 누르세요.";
        }

        private void RefreshConfigPath()
        {
            ConfigPath = ProjectRecipeService.BuildConfigPreviewPath(recipeRootPath, RecipeName);
            ManifestPath = ProjectRecipeService.BuildManifestPreviewPath(recipeRootPath, RecipeName);
        }

        private void ExecuteSaveProjectConfig()
        {
            if (IsPersistenceUnavailable())
            {
                return;
            }

            string currentRecipeName = RecipeName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(currentRecipeName))
            {
                StatusText = "Recipe 이름이 없어 설정을 저장하지 않았습니다.";
                return;
            }

            try
            {
                RecipeConfigurationSaveResult result = projectRecipeSessionService.SaveConfiguration(
                    applicationProvider()?.Data,
                    currentRecipeName,
                    updateYoloDataYaml: false,
                    refreshDatasetVersion: true);
                if (!result.IsSuccess)
                {
                    string failure = $"프로젝트 설정 저장 실패: {result.ErrorMessage}";
                    StatusText = failure;
                    WorkflowError?.Invoke(failure);
                    return;
                }

                StatusText = $"설정 저장 완료: {DateTime.Now:HH:mm:ss}";
                ProjectConfigSaved?.Invoke(result.Path);
            }
            catch (Exception ex)
            {
                string failure = $"프로젝트 설정 저장 실패: {ex.Message}";
                StatusText = failure;
                WorkflowError?.Invoke(failure);
            }
        }

        private void ExecuteApplyRecipe()
        {
            if (IsPersistenceUnavailable() || Interlocked.CompareExchange(ref recipeApplyInFlight, 1, 0) != 0)
            {
                return;
            }

            string recipeName = RecipeName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(recipeName))
            {
                Interlocked.Exchange(ref recipeApplyInFlight, 0);
                StatusText = "적용할 recipe 이름을 입력하세요.";
                return;
            }

            if (!ProjectRecipeService.IsValidRecipeName(recipeName))
            {
                Interlocked.Exchange(ref recipeApplyInFlight, 0);
                StatusText = "Recipe 이름에 사용할 수 없는 문자가 있습니다.";
                return;
            }

            StatusText = $"Recipe 적용 중: {recipeName}";
            _ = ApplyRecipeAsync(recipeName);
        }

        private async Task ApplyRecipeAsync(string recipeName)
        {
            try
            {
                ProjectRecipeApplyResult result = await projectRecipeApplyWorkflowService.ApplyAsync(
                    applicationProvider(),
                    recipeName);
                if (result.IsApplied)
                {
                    ProjectRecipeApplied?.Invoke(recipeName, result);
                }
            }
            catch (Exception ex)
            {
                string failure = $"Recipe 적용 실패: {ex.Message}";
                StatusText = failure;
                WorkflowError?.Invoke(failure);
            }
            finally
            {
                Interlocked.Exchange(ref recipeApplyInFlight, 0);
            }
        }

        private bool IsPersistenceUnavailable()
            => projectRecipeSessionService == null
                || projectRecipeApplyWorkflowService == null
                || applicationProvider == null
                || closeApprovedProvider?.Invoke() == true;

        public void ApplyWorkflowCommandState(WorkflowCommandState state)
        {
            bool canRunGeneralCommands = state?.CanRunGeneralCommands == true;
            IsApplyRecipeEnabled = canRunGeneralCommands;
            IsRefreshRecipeListEnabled = canRunGeneralCommands;
            IsSaveProjectConfigEnabled = state?.CanSaveProjectConfig == true;
            IsOpenProjectConfigFolderEnabled = canRunGeneralCommands;
            IsProjectArchiveCommandEnabled = canRunGeneralCommands;
        }

        private static void NoOpCommand()
        {
        }

        private static void NoOpSelectionCommand(object selectedItem)
        {
        }
    }

    public sealed class ProjectConfigNavigationCallbacks
    {
        public Func<string, bool> RefreshRecipeList { get; init; }

        public Func<string> CurrentRecipeName { get; init; }

        public Action OpenProjectConfigFolder { get; init; }

        public Func<bool> IsApplicationCloseApproved { get; init; }
    }

    public sealed class ProjectConfigArchiveCallbacks
    {
        public Func<ApplicationCloseState> BuildApplicationCloseState { get; init; }

        public Func<string> CurrentRecipeName { get; init; }

        public Func<string> CurrentRecipeConfigPath { get; init; }

        public Func<string> CurrentRecipeConfigDirectory { get; init; }

        public Func<string> RecipeRootDirectory { get; init; }

        public Func<string> DefaultDatasetParentDirectory { get; init; }

        public Func<string, string, string, string> SelectSaveFile { get; init; }

        public Func<string, string, string, string> SelectFile { get; init; }

        public Func<string, string, string> SelectFolder { get; init; }

        public Action<string> ImportedRecipeLoaded { get; init; }

        public Action<string> AppendLog { get; init; }

        public Func<bool> IsApplicationCloseApproved { get; init; }
    }
}
