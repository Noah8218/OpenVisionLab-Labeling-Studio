using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MvcVisionSystem
{
    public static class WpfProjectRecipeService
    {
        private const string LastOpenedRecipeFileName = ".last-opened-recipe";

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

        public static string BuildLastOpenedRecipePath(string recipeRootPath)
        {
            string rootPath = string.IsNullOrWhiteSpace(recipeRootPath)
                ? GetRecipeRootDirectory()
                : recipeRootPath;
            return Path.Combine(rootPath, LastOpenedRecipeFileName);
        }

        public static void SaveLastOpenedRecipeName(string recipeRootPath, string recipeName)
        {
            if (!IsValidRecipeName(recipeName))
            {
                return;
            }

            string rootPath = string.IsNullOrWhiteSpace(recipeRootPath)
                ? GetRecipeRootDirectory()
                : recipeRootPath;
            Directory.CreateDirectory(rootPath);
            File.WriteAllText(BuildLastOpenedRecipePath(rootPath), recipeName.Trim());
        }

        public static string LoadLastOpenedRecipeName(string recipeRootPath)
        {
            string statePath = BuildLastOpenedRecipePath(recipeRootPath);
            if (!File.Exists(statePath))
            {
                return string.Empty;
            }

            try
            {
                string recipeName = File.ReadAllText(statePath).Trim();
                if (!IsValidRecipeName(recipeName))
                {
                    return string.Empty;
                }

                string recipeDirectory = BuildConfigDirectory(recipeRootPath, recipeName);
                return Directory.Exists(recipeDirectory) ? recipeName : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public static string ResolveStartupRecipeName(string recipeRootPath, string currentRecipeName = "")
        {
            if (IsValidRecipeName(currentRecipeName)
                && Directory.Exists(BuildConfigDirectory(recipeRootPath, currentRecipeName)))
            {
                return currentRecipeName.Trim();
            }

            string lastOpenedRecipeName = LoadLastOpenedRecipeName(recipeRootPath);
            if (!string.IsNullOrWhiteSpace(lastOpenedRecipeName))
            {
                return lastOpenedRecipeName;
            }

            return string.Empty;
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

            // 최신에 수정(저장)된 레시피를 위로 노출해서 사용자가 직관적으로 “가장 최근 데이터셋”을 바로 선택할 수 있게 한다.
            return Directory.EnumerateDirectories(rootPath)
                .Select(directoryPath => new
                {
                    Name = Path.GetFileName(directoryPath),
                    Path = directoryPath,
                    LastWrite = GetRecipeLastWriteTimeUtc(directoryPath)
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                .OrderByDescending(item => item.LastWrite)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .Select(item => item.Name)
                .ToArray();
        }

        private static DateTime GetRecipeLastWriteTimeUtc(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return DateTime.MinValue;
            }

            try
            {
                string manifestPath = Path.Combine(directoryPath, LabelingDatasetManifestService.FileName);
                if (File.Exists(manifestPath))
                {
                    return File.GetLastWriteTimeUtc(manifestPath);
                }

                return Directory.GetLastWriteTimeUtc(directoryPath);
            }
            catch
            {
                return Directory.Exists(directoryPath)
                    ? Directory.GetLastWriteTimeUtc(directoryPath)
                    : DateTime.MinValue;
            }
        }

        public static bool IsValidRecipeName(string recipeName)
        {
            return !string.IsNullOrWhiteSpace(recipeName)
                && recipeName.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;
        }
    }
}
