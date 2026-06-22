using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MvcVisionSystem
{
    public sealed class WpfProjectConfigPanelViewModel : WpfObservableViewModel
    {
        private string recipeName = string.Empty;
        private string selectedRecipeName = string.Empty;
        private string configPath = string.Empty;
        private string statusText = "현재 recipe 설정 위치를 확인하세요.";
        private string recipeRootPath = string.Empty;
        private bool isApplyRecipeEnabled = true;
        private bool isRefreshRecipeListEnabled = true;
        private bool isSaveProjectConfigEnabled;
        private bool isOpenProjectConfigFolderEnabled = true;

        public string ViewName => nameof(WpfProjectConfigPanel);

        public ObservableCollection<string> RecipeNames { get; } = new ObservableCollection<string>();

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
            ConfigPath = WpfProjectRecipeService.BuildConfigPreviewPath(recipeRootPath, RecipeName);
        }

        public void ApplyWorkflowCommandState(WpfWorkflowCommandState state)
        {
            bool canRunGeneralCommands = state?.CanRunGeneralCommands == true;
            IsApplyRecipeEnabled = canRunGeneralCommands;
            IsRefreshRecipeListEnabled = canRunGeneralCommands;
            IsSaveProjectConfigEnabled = state?.CanSaveProjectConfig == true;
            IsOpenProjectConfigFolderEnabled = canRunGeneralCommands;
        }
    }
}
