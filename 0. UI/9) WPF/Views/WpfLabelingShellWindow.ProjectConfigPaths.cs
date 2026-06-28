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
        // Path and dialog helpers are shared by project, YOLO, and training settings flows.
        private void SetProjectConfigStatus(string message)
        {
            if (ProjectConfigViewModel != null)
            {
                ProjectConfigViewModel.StatusText = message ?? string.Empty;
            }
            else if (ProjectConfigStatusText != null)
            {
                ProjectConfigStatusText.Text = message ?? string.Empty;
            }
        }

        private string GetCurrentRecipeName()
        {
            return global.Recipe?.Name?.Trim() ?? string.Empty;
        }

        private static string GetRecipeRootDirectory()
        {
            return WpfProjectRecipeService.GetRecipeRootDirectory();
        }

        private string GetCurrentRecipeConfigDirectory()
        {
            string recipeName = GetCurrentRecipeName();
            return WpfProjectRecipeService.BuildConfigDirectory(GetRecipeRootDirectory(), recipeName);
        }

        private string GetCurrentRecipeConfigPath()
        {
            string recipeName = GetCurrentRecipeName();
            return WpfProjectRecipeService.BuildConfigPath(GetRecipeRootDirectory(), recipeName);
        }

        private bool TryPickFile(string title, string filter, string currentPath, out string selectedPath)
            => fileDialogService.TryPickFile(this, title, filter, currentPath, out selectedPath);

        private bool TryPickFolder(string title, string currentPath, out string selectedPath)
            => fileDialogService.TryPickFolder(this, title, currentPath, out selectedPath);
    }
}
