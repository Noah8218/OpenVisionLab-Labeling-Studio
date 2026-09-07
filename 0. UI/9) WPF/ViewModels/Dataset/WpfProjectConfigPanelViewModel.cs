using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
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

        public string ViewName => nameof(WpfProjectConfigPanel);

        public ObservableCollection<string> RecipeNames { get; } = new ObservableCollection<string>();

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
}
