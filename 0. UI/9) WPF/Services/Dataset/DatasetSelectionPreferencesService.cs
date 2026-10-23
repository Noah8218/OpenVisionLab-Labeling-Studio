using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MvcVisionSystem
{
    internal sealed class DatasetSelectionPreferencesService
    {
        private readonly string settingsPath;

        public DatasetSelectionPreferencesService()
            : this(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "OpenVisionLab",
                "LabelingStudio",
                "dataset-selection.json"))
        {
        }

        internal DatasetSelectionPreferencesService(string settingsPath)
        {
            this.settingsPath = settingsPath;
        }

        public HashSet<string> LoadFavoriteRecipeNames()
        {
            if (string.IsNullOrWhiteSpace(settingsPath) || !File.Exists(settingsPath))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                return (JsonConvert.DeserializeObject<string[]>(File.ReadAllText(settingsPath)) ?? Array.Empty<string>())
                    .Where(ProjectRecipeService.IsValidRecipeName)
                    .Select(name => name.Trim())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        public bool SaveFavoriteRecipeNames(IEnumerable<string> recipeNames)
        {
            if (string.IsNullOrWhiteSpace(settingsPath))
            {
                return false;
            }

            string temporaryPath = settingsPath + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
                File.WriteAllText(temporaryPath, JsonConvert.SerializeObject(
                    (recipeNames ?? Array.Empty<string>())
                        .Where(ProjectRecipeService.IsValidRecipeName)
                        .Select(name => name.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)));
                File.Move(temporaryPath, settingsPath, overwrite: true);
                return true;
            }
            catch
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch
                {
                }

                return false;
            }
        }
    }
}
