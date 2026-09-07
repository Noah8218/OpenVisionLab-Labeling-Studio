using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MvcVisionSystem._1._Core;

namespace MvcVisionSystem
{
    /// <summary>
    /// Performs the non-visual Core transitions for the active recipe. The
    /// shell remains responsible for UI refresh and operator-facing status.
    /// </summary>
    public class ProjectRecipeSessionService
    {
        private readonly SemaphoreSlim applyGate = new SemaphoreSlim(1, 1);
        private long applyRequestVersion;

        public string Save(LabelingProjectData data, string recipeName, bool refreshDatasetVersion = true)
        {
            RecipeConfigurationSaveResult result = SaveConfiguration(
                data,
                recipeName,
                updateYoloDataYaml: false,
                refreshDatasetVersion: refreshDatasetVersion);
            if (!result.IsSuccess)
            {
                throw new IOException($"Recipe configuration could not be saved: {result.ErrorMessage}");
            }

            return ProjectRecipeService.BuildConfigPath(
                ProjectRecipeService.GetRecipeRootDirectory(),
                recipeName);
        }

        public RecipeConfigurationSaveResult SaveConfiguration(
            LabelingProjectData data,
            string recipeName,
            bool updateYoloDataYaml,
            bool refreshDatasetVersion = true)
        {
            ArgumentNullException.ThrowIfNull(data);
            data.ProjectSettings ??= new LabelingProjectSettings();
            PythonModelRuntimePathResolver.ApplyDefaults(data.ProjectSettings);
            Recipe.InitDirectory(recipeName);
            return updateYoloDataYaml
                ? data.SaveConfigAndYoloDataYaml(recipeName, refreshDatasetVersion)
                : data.SaveConfig(recipeName, refreshDatasetVersion);
        }

        public string Apply(LabelingApplicationState application, string recipeName)
        {
            ValidateApplyInputs(application, recipeName);
            long requestVersion = Interlocked.Increment(ref applyRequestVersion);
            applyGate.Wait();
            try
            {
                LabelingProjectData loadedData = LoadRecipeData(application, recipeName);
                return CommitIfCurrent(application, recipeName, loadedData, requestVersion);
            }
            finally
            {
                applyGate.Release();
            }
        }

        public async Task<string> ApplyAsync(
            LabelingApplicationState application,
            string recipeName,
            CancellationToken cancellationToken = default)
        {
            ValidateApplyInputs(application, recipeName);
            long requestVersion = Interlocked.Increment(ref applyRequestVersion);
            await applyGate.WaitAsync(cancellationToken);
            try
            {
                LabelingProjectData sourceData = GetSourceData(application, recipeName);
                LabelingProjectData loadedData = await Task.Run(
                    () => LoadRecipeData(sourceData, recipeName),
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                return CommitIfCurrent(application, recipeName, loadedData, requestVersion);
            }
            finally
            {
                applyGate.Release();
            }
        }

        public string ApplyPrepared(LabelingApplicationState application, string recipeName, LabelingProjectData preparedData)
        {
            ValidateApplyInputs(application, recipeName);
            ArgumentNullException.ThrowIfNull(preparedData);
            long requestVersion = Interlocked.Increment(ref applyRequestVersion);
            applyGate.Wait();
            try
            {
                return CommitIfCurrent(application, recipeName, preparedData, requestVersion);
            }
            finally
            {
                applyGate.Release();
            }
        }

        private static LabelingProjectData LoadRecipeData(LabelingApplicationState application, string recipeName)
        {
            LabelingProjectData sourceData = GetSourceData(application, recipeName);
            return LoadRecipeData(sourceData, recipeName);
        }

        private static LabelingProjectData GetSourceData(LabelingApplicationState application, string recipeName)
        {
            ValidateApplyInputs(application, recipeName);
            return application.Data ?? new LabelingProjectData();
        }

        private static LabelingProjectData LoadRecipeData(LabelingProjectData sourceData, string recipeName)
        {
            string normalizedRecipeName = recipeName.Trim();
            RecipeConfigurationLoadResult result = sourceData.TryLoadConfig(normalizedRecipeName);
            if (result.IsSuccess)
            {
                return result.Data;
            }

            if (result.FailureKind == RecipeConfigurationFailureKind.Missing)
            {
                throw new FileNotFoundException($"Recipe configuration does not exist: {normalizedRecipeName}", result.Path);
            }

            throw new InvalidOperationException($"Recipe configuration could not be loaded: {normalizedRecipeName}. {result.ErrorMessage}");
        }

        private string CommitIfCurrent(
            LabelingApplicationState application,
            string recipeName,
            LabelingProjectData loadedData,
            long requestVersion)
        {
            if (requestVersion != Volatile.Read(ref applyRequestVersion))
            {
                throw new OperationCanceledException("A newer Recipe apply request replaced this request.");
            }

            return Commit(application, recipeName.Trim(), loadedData);
        }

        private static string Commit(LabelingApplicationState application, string recipeName, LabelingProjectData loadedData)
        {
            string previousRecipeName = (application.Recipe.Name ?? string.Empty).Trim();
            loadedData.ProjectSettings ??= new LabelingProjectSettings();
            PythonModelRuntimePathResolver.ApplyDefaults(loadedData.ProjectSettings);
            application.Data = loadedData;
            application.Recipe.CommitLoadedRecipe(recipeName);
            return previousRecipeName;
        }

        private static void ValidateApplyInputs(LabelingApplicationState application, string recipeName)
        {
            ArgumentNullException.ThrowIfNull(application);
            if (string.IsNullOrWhiteSpace(recipeName))
            {
                throw new ArgumentException("A Recipe name is required.", nameof(recipeName));
            }
        }
    }

    [Obsolete("Use ProjectRecipeSessionService.", false)]
    public sealed class WpfProjectRecipeSessionService : ProjectRecipeSessionService
    {
    }
}
