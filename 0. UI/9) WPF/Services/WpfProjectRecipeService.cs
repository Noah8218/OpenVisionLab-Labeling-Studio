using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MvcVisionSystem
{
    public static class WpfProjectRecipeService
    {
        public static string GetRecipeRootDirectory()
        {
            return Path.Combine(AppContext.BaseDirectory, "RECIPE");
        }

        public static string BuildConfigDirectory(string recipeRootPath, string recipeName)
        {
            string rootPath = string.IsNullOrWhiteSpace(recipeRootPath)
                ? GetRecipeRootDirectory()
                : recipeRootPath;

            return string.IsNullOrWhiteSpace(recipeName)
                ? rootPath
                : Path.Combine(rootPath, recipeName.Trim());
        }

        public static string BuildConfigPath(string recipeRootPath, string recipeName)
        {
            return string.IsNullOrWhiteSpace(recipeName)
                ? string.Empty
                : Path.Combine(BuildConfigDirectory(recipeRootPath, recipeName), "VISION.xml");
        }

        public static string BuildManifestPath(string recipeRootPath, string recipeName)
        {
            return string.IsNullOrWhiteSpace(recipeName)
                ? string.Empty
                : Path.Combine(BuildConfigDirectory(recipeRootPath, recipeName), LabelingDatasetManifestService.FileName);
        }

        public static string BuildConfigPreviewPath(string recipeRootPath, string recipeName)
        {
            return string.IsNullOrWhiteSpace(recipeName)
                ? Path.Combine(BuildConfigDirectory(recipeRootPath, string.Empty), "(recipe 선택 필요)", "VISION.xml")
                : BuildConfigPath(recipeRootPath, recipeName);
        }

        public static string BuildManifestPreviewPath(string recipeRootPath, string recipeName)
        {
            string configPreviewPath = BuildConfigPreviewPath(recipeRootPath, recipeName);
            string directoryPath = Path.GetDirectoryName(configPreviewPath) ?? BuildConfigDirectory(recipeRootPath, recipeName);
            return Path.Combine(directoryPath, LabelingDatasetManifestService.FileName);
        }

        public static IReadOnlyList<string> ListRecipeNames(string recipeRootPath)
        {
            string rootPath = string.IsNullOrWhiteSpace(recipeRootPath)
                ? GetRecipeRootDirectory()
                : recipeRootPath;

            if (!Directory.Exists(rootPath))
            {
                return Array.Empty<string>();
            }

            return Directory.EnumerateDirectories(rootPath)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static bool IsValidRecipeName(string recipeName)
        {
            return !string.IsNullOrWhiteSpace(recipeName)
                && recipeName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
        }
    }
}
