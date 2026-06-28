using Lib.Common;
using MvcVisionSystem._1._Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Recipe list commands only update the project-config ViewModel and filesystem folder view.
        private bool PopulateProjectRecipeList(string selectedRecipeName)
        {
            WpfProjectConfigPanelViewModel viewModel = ProjectConfigViewModel;
            if (viewModel == null)
            {
                return false;
            }

            suppressProjectRecipeSelection = true;
            try
            {
                IReadOnlyList<string> recipeNames = WpfProjectRecipeService.ListRecipeNames(GetRecipeRootDirectory());
                string matchingRecipeName = recipeNames
                    .FirstOrDefault(name => string.Equals(name, selectedRecipeName, StringComparison.OrdinalIgnoreCase))
                    ?? string.Empty;
                viewModel.SetRecipeList(recipeNames, matchingRecipeName);

                return true;
            }
            catch (Exception ex)
            {
                viewModel.SetRecipeList(Array.Empty<string>(), string.Empty);
                SetProjectConfigStatus($"Recipe 목록 읽기 실패: {ex.Message}");
                AppendLog($"Recipe 목록 읽기 실패: {ex.Message}");
                return false;
            }
            finally
            {
                suppressProjectRecipeSelection = false;
            }
        }

        private void ExecuteSaveProjectConfigCommand()
        {
            SaveProjectConfigFromPanel();
        }

        private void ExecuteApplyProjectRecipeCommand()
        {
            ApplyProjectRecipeFromPanel();
        }

        private void ProjectRecipeListBox_SelectionChanged(object sender, object selectedItem)
        {
            if (suppressProjectRecipeSelection)
            {
                return;
            }

            string recipeName = selectedItem as string ?? ProjectConfigViewModel?.SelectedRecipeName ?? ProjectRecipeListBox?.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(recipeName))
            {
                return;
            }

            ProjectConfigViewModel?.SelectRecipeFromList(recipeName);
        }

        private void ExecuteRefreshProjectRecipeListCommand()
        {
            string selectedRecipeName = ProjectConfigViewModel?.RecipeName?.Trim() ?? GetCurrentRecipeName();
            if (PopulateProjectRecipeList(selectedRecipeName))
            {
                SetProjectConfigStatus("Recipe 목록을 다시 읽었습니다. 적용할 항목을 선택하세요.");
            }
        }

        private void ExecuteOpenProjectConfigFolderCommand()
        {
            string directoryPath = string.IsNullOrWhiteSpace(GetCurrentRecipeName())
                ? GetRecipeRootDirectory()
                : GetCurrentRecipeConfigDirectory();

            try
            {
                Directory.CreateDirectory(directoryPath);
                Process.Start(new ProcessStartInfo
                {
                    FileName = directoryPath,
                    UseShellExecute = true
                });
                SetProjectConfigStatus($"폴더 열기: {directoryPath}");
                AppendLog($"Recipe 설정 폴더 열기: {directoryPath}");
            }
            catch (Exception ex)
            {
                SetProjectConfigStatus($"폴더 열기 실패: {ex.Message}");
                AppendLog($"Recipe 설정 폴더 열기 실패: {ex.Message}");
            }
        }
    }
}
