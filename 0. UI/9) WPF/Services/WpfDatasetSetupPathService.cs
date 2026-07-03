using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using MvcVisionSystem.Yolo;

namespace MvcVisionSystem
{
    public sealed class WpfDatasetSetupPathService
    {
        public string ResolveRecipeName(
            string preferredRecipeName,
            string currentRecipeName,
            LabelingDatasetPurpose purpose,
            string recipeRootPath)
        {
            if (CanUseRecipeNameForNewDataset(preferredRecipeName, recipeRootPath))
            {
                return preferredRecipeName.Trim();
            }

            if (CanUseRecipeNameForNewDataset(currentRecipeName, recipeRootPath))
            {
                return currentRecipeName.Trim();
            }

            return BuildUniqueRecipeName(purpose, recipeRootPath);
        }

        public string ResolveOutputRoot(string recipeName, string baseOutputRootPath, string recipeRootPath)
        {
            string defaultOutputRoot = string.IsNullOrWhiteSpace(baseOutputRootPath)
                ? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "DATA"))
                : Path.GetFullPath(baseOutputRootPath);
            string normalizedRecipeName = WpfProjectRecipeService.IsValidRecipeName(recipeName)
                ? recipeName.Trim()
                : BuildUniqueRecipeName(LabelingDatasetPurpose.ObjectDetection, recipeRootPath);
            string outputRootPath = Path.Combine(defaultOutputRoot, normalizedRecipeName);
            if (!Directory.Exists(outputRootPath))
            {
                return outputRootPath;
            }

            int suffix = 2;
            while (Directory.Exists($"{outputRootPath}_{suffix}"))
            {
                suffix++;
            }

            return $"{outputRootPath}_{suffix}";
        }

        public bool CanUseRecipeNameForNewDataset(string recipeName, string recipeRootPath)
        {
            if (!WpfProjectRecipeService.IsValidRecipeName(recipeName))
            {
                return false;
            }

            string recipeDirectory = WpfProjectRecipeService.BuildConfigDirectory(recipeRootPath, recipeName.Trim());
            return !Directory.Exists(recipeDirectory);
        }

        public string BuildUniqueRecipeName(LabelingDatasetPurpose purpose, string recipeRootPath)
        {
            string baseName = $"Dataset_{purpose}_{DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)}";
            string recipeName = baseName;
            int suffix = 2;
            while (!CanUseRecipeNameForNewDataset(recipeName, recipeRootPath))
            {
                recipeName = $"{baseName}_{suffix}";
                suffix++;
            }

            return recipeName;
        }

        public bool TryFindDatasetUsingOutputRoot(
            string recipeRootPath,
            string outputRootPath,
            string requestedRecipeName,
            out string existingRecipeName)
        {
            existingRecipeName = string.Empty;
            if (string.IsNullOrWhiteSpace(outputRootPath))
            {
                return false;
            }

            foreach (string recipeName in WpfProjectRecipeService.ListRecipeNames(recipeRootPath))
            {
                if (string.Equals(recipeName, requestedRecipeName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string configPath = WpfProjectRecipeService.BuildConfigPath(recipeRootPath, recipeName);
                string existingOutputRoot = TryReadRecipeOutputRoot(configPath);
                if (PathsEqual(existingOutputRoot, outputRootPath))
                {
                    existingRecipeName = recipeName;
                    return true;
                }
            }

            return false;
        }

        public string TryReadRecipeOutputRoot(string configPath)
        {
            if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
            {
                return string.Empty;
            }

            try
            {
                XDocument document = XDocument.Load(configPath);
                return document.Descendants("OutputRootPath").FirstOrDefault()?.Value
                    ?? document.Descendants("OutputDataImageAndTxtPath").FirstOrDefault()?.Value
                    ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public static bool PathsEqual(string firstPath, string secondPath)
        {
            if (string.IsNullOrWhiteSpace(firstPath) || string.IsNullOrWhiteSpace(secondPath))
            {
                return false;
            }

            try
            {
                return string.Equals(
                    Path.GetFullPath(firstPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    Path.GetFullPath(secondPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}
