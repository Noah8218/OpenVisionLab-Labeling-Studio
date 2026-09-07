using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MvcVisionSystem
{
    /// <summary>
    /// Reads the persisted recipe artifacts needed by the dataset selector.
    /// It returns immutable selection data and does not depend on WPF controls
    /// or the dataset-selection ViewModel.
    /// </summary>
    public class DatasetSelectionCatalogService
    {
        public IReadOnlyList<DatasetSelectionSnapshot> Load(
            string recipeRootPath,
            string currentRecipeName)
        {
            return ProjectRecipeService.ListRecipeNames(recipeRootPath)
                .Select(recipeName => BuildSnapshot(recipeRootPath, recipeName, currentRecipeName))
                .ToArray();
        }

        private static DatasetSelectionSnapshot BuildSnapshot(
            string recipeRootPath,
            string recipeName,
            string currentRecipeName)
        {
            string manifestPath = ProjectRecipeService.BuildManifestPath(recipeRootPath, recipeName);
            string configPath = ProjectRecipeService.BuildConfigPath(recipeRootPath, recipeName);
            LabelingDatasetManifest manifest = TryReadManifest(manifestPath);
            LabelingProjectData recipeData = TryReadRecipeConfig(configPath);
            IReadOnlyList<string> classes = manifest?.Classes?.Count > 0
                ? manifest.Classes.ToArray()
                : recipeData?.ClassNamedList?
                    .Select(item => item?.Text)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToArray()
                    ?? Array.Empty<string>();

            return new DatasetSelectionSnapshot(
                recipeName,
                manifest?.DatasetPurpose ?? string.Empty,
                FirstNonEmpty(manifest?.OutputRootPath, recipeData?.OutputRootPath),
                FirstNonEmpty(manifest?.ImageRootPath, recipeData?.ProjectSettings?.ResolveImageRootPath()),
                classes,
                manifest?.ArtifactSummary?.ImageCount ?? 0,
                manifest?.ArtifactSummary?.PrimaryLabelCount ?? 0,
                manifestPath,
                File.Exists(manifestPath),
                string.Equals(recipeName, currentRecipeName, StringComparison.OrdinalIgnoreCase));
        }

        private static LabelingDatasetManifest TryReadManifest(string manifestPath)
        {
            if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
            {
                return null;
            }

            try
            {
                return JsonConvert.DeserializeObject<LabelingDatasetManifest>(File.ReadAllText(manifestPath));
            }
            catch
            {
                return null;
            }
        }

        private static LabelingProjectData TryReadRecipeConfig(string configPath)
        {
            if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
            {
                return null;
            }

            try
            {
                RecipeConfigurationLoadResult result = new RecipeConfigurationStore().Load(configPath);
                if (!result.IsSuccess)
                {
                    return null;
                }

                result.Data.NormalizeOutputPaths();
                return result.Data;
            }
            catch
            {
                return null;
            }
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values?.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }
    }

    [Obsolete("Use DatasetSelectionCatalogService.", false)]
    public sealed class WpfDatasetSelectionCatalogService : DatasetSelectionCatalogService
    {
        public new IReadOnlyList<WpfDatasetSelectionSnapshot> Load(
            string recipeRootPath,
            string currentRecipeName)
            => base.Load(recipeRootPath, currentRecipeName)
                .Select(WpfDatasetSelectionSnapshot.FromCanonical)
                .ToArray();
    }

    public class DatasetSelectionSnapshot
    {
        public DatasetSelectionSnapshot(
            string recipeName,
            string datasetPurpose,
            string outputRootPath,
            string imageRootPath,
            IReadOnlyList<string> classes,
            int imageCount,
            int labelCount,
            string manifestPath,
            bool hasManifest,
            bool isCurrent)
        {
            RecipeName = recipeName ?? string.Empty;
            DatasetPurpose = datasetPurpose ?? string.Empty;
            OutputRootPath = outputRootPath ?? string.Empty;
            ImageRootPath = imageRootPath ?? string.Empty;
            Classes = classes?.ToArray() ?? Array.Empty<string>();
            ImageCount = imageCount;
            LabelCount = labelCount;
            ManifestPath = manifestPath ?? string.Empty;
            HasManifest = hasManifest;
            IsCurrent = isCurrent;
        }

        public string RecipeName { get; }

        public string DatasetPurpose { get; }

        public string OutputRootPath { get; }

        public string ImageRootPath { get; }

        public IReadOnlyList<string> Classes { get; }

        public int ImageCount { get; }

        public int LabelCount { get; }

        public string ManifestPath { get; }

        public bool HasManifest { get; }

        public bool IsCurrent { get; }
    }

    [Obsolete("Use DatasetSelectionSnapshot.", false)]
    public sealed class WpfDatasetSelectionSnapshot : DatasetSelectionSnapshot
    {
        public WpfDatasetSelectionSnapshot(
            string recipeName,
            string datasetPurpose,
            string outputRootPath,
            string imageRootPath,
            IReadOnlyList<string> classes,
            int imageCount,
            int labelCount,
            string manifestPath,
            bool hasManifest,
            bool isCurrent)
            : base(
                recipeName,
                datasetPurpose,
                outputRootPath,
                imageRootPath,
                classes,
                imageCount,
                labelCount,
                manifestPath,
                hasManifest,
                isCurrent)
        {
        }

        internal static WpfDatasetSelectionSnapshot FromCanonical(DatasetSelectionSnapshot source)
            => source == null
                ? null
                : new WpfDatasetSelectionSnapshot(
                    source.RecipeName,
                    source.DatasetPurpose,
                    source.OutputRootPath,
                    source.ImageRootPath,
                    source.Classes,
                    source.ImageCount,
                    source.LabelCount,
                    source.ManifestPath,
                    source.HasManifest,
                    source.IsCurrent);
    }
}
