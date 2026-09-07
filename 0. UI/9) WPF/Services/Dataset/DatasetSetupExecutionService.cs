using System;
using System.IO;
using MvcVisionSystem._1._Core;

namespace MvcVisionSystem
{
    public enum WpfDatasetSetupExecutionFailure
    {
        None,
        InvalidRecipeName,
        DuplicateOutputRoot,
        SamplePreset,
        RecipePersistence
    }

    public sealed class WpfDatasetSetupExecutionResult
    {
        public WpfDatasetSetupExecutionFailure Failure { get; set; }

        public bool Succeeded => Failure == WpfDatasetSetupExecutionFailure.None;

        public string RecipeName { get; set; } = string.Empty;

        public string OutputRootPath { get; set; } = string.Empty;

        public string SelectedClassName { get; set; } = string.Empty;

        public string ImageRootPath { get; set; } = string.Empty;

        public string ExistingRecipeName { get; set; } = string.Empty;

        public string SampleError { get; set; } = string.Empty;

        public string PersistenceError { get; set; } = string.Empty;

        public string ManifestPath { get; set; } = string.Empty;

        public DatasetSamplePresetApplyResult SampleResult { get; set; }

        public LabelingProjectData Data { get; set; }
    }

    /// <summary>
    /// Creates the persisted dataset contract from a validated wizard request.
    /// It deliberately has no WPF window, view-model, or LabelingApplicationState dependency so
    /// the shell can remain an adapter for UI state and navigation. Paired Recipe/YAML persistence
    /// is delegated to the concrete project Recipe session owner.
    /// </summary>
    public class DatasetSetupExecutionService
    {
        private readonly DatasetSetupPathService pathService;
        private readonly DatasetSetupDataService dataService;
        private readonly ProjectRecipeSessionService projectRecipeSessionService;

        public DatasetSetupExecutionService()
            : this(
                new DatasetSetupPathService(),
                new DatasetSetupDataService(),
                new ProjectRecipeSessionService())
        {
        }

        public DatasetSetupExecutionService(
            DatasetSetupPathService pathService,
            DatasetSetupDataService dataService)
            : this(pathService, dataService, new ProjectRecipeSessionService())
        {
        }

        public DatasetSetupExecutionService(
            DatasetSetupPathService pathService,
            DatasetSetupDataService dataService,
            ProjectRecipeSessionService projectRecipeSessionService)
        {
            this.pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
            this.dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
            this.projectRecipeSessionService = projectRecipeSessionService ?? throw new ArgumentNullException(nameof(projectRecipeSessionService));
        }

        public WpfDatasetSetupExecutionResult Execute(
            WpfDatasetSetupRequest request,
            string recipeRootPath,
            string defaultOutputRootPath)
        {
            var result = new WpfDatasetSetupExecutionResult();
            if (request == null)
            {
                result.Failure = WpfDatasetSetupExecutionFailure.InvalidRecipeName;
                return result;
            }

            string recipeName = request.RecipeName?.Trim() ?? string.Empty;
            if (!ProjectRecipeService.IsValidRecipeName(recipeName))
            {
                result.Failure = WpfDatasetSetupExecutionFailure.InvalidRecipeName;
                return result;
            }

            string outputRootPath = string.IsNullOrWhiteSpace(request.OutputRootPath)
                ? pathService.ResolveOutputRoot(recipeName, defaultOutputRootPath, recipeRootPath)
                : request.OutputRootPath.Trim();
            if (pathService.TryFindDatasetUsingOutputRoot(recipeRootPath, outputRootPath, recipeName, out string existingRecipeName))
            {
                result.Failure = WpfDatasetSetupExecutionFailure.DuplicateOutputRoot;
                result.ExistingRecipeName = existingRecipeName;
                return result;
            }

            var data = new LabelingProjectData();
            data.ProjectSettings.EnsureDefaults();
            PythonModelRuntimePathResolver.ApplyDefaults(data.ProjectSettings);
            data.ProjectSettings.DatasetPurpose = request.Purpose;
            data.ProjectSettings.PythonModel.ModelEngine = PythonModelSettings.NormalizeModelEngine(request.ModelEngine);
            data.ProjectSettings.PythonModel.WeightsPath = request.WeightsPath?.Trim() ?? string.Empty;
            if (request.Purpose == LabelingDatasetPurpose.AnomalyDetection)
            {
                data.ProjectSettings.AnomalyClassification.NormalClassNames = new System.Collections.Generic.List<string>(request.AnomalyNormalClassNames ?? Array.Empty<string>());
                data.ProjectSettings.AnomalyClassification.AbnormalClassNames = new System.Collections.Generic.List<string>(request.AnomalyAbnormalClassNames ?? Array.Empty<string>());
                data.ProjectSettings.AnomalyClassification.EnsureDefaults();
            }
            string selectedClassName = dataService.ApplyOutputRootAndClasses(data, outputRootPath, request.ClassNames);
            if (!DatasetSamplePresetService.TryApplySample(request, data, out DatasetSamplePresetApplyResult sampleResult, out string sampleError))
            {
                result.Failure = WpfDatasetSetupExecutionFailure.SamplePreset;
                result.SampleError = sampleError;
                return result;
            }

            string imageRootPath = Directory.Exists(request.ImageRootPath)
                ? request.ImageRootPath.Trim()
                : sampleResult?.Applied == true && Directory.Exists(sampleResult.ImageRootPath)
                    ? sampleResult.ImageRootPath
                    : data.TrainImagesPath;
            data.ProjectSettings.ImageRootPath = imageRootPath;
            PythonModelRuntimePathResolver.ApplyDefaults(data.ProjectSettings);
            RecipeConfigurationSaveResult saveResult = projectRecipeSessionService.SaveConfiguration(
                data,
                recipeName,
                updateYoloDataYaml: true,
                refreshDatasetVersion: true);
            if (!saveResult.IsSuccess)
            {
                result.Failure = WpfDatasetSetupExecutionFailure.RecipePersistence;
                result.PersistenceError = saveResult.ErrorMessage;
                return result;
            }

            result.RecipeName = recipeName;
            result.OutputRootPath = data.OutputRootPath;
            result.SelectedClassName = selectedClassName;
            result.ImageRootPath = imageRootPath;
            result.ManifestPath = LabelingDatasetManifestService.GetManifestPath(recipeName);
            result.SampleResult = sampleResult;
            result.Data = data;
            return result;
        }
    }

    [Obsolete("Use DatasetSetupExecutionService.", false)]
    public sealed class WpfDatasetSetupExecutionService : DatasetSetupExecutionService
    {
        public WpfDatasetSetupExecutionService()
        {
        }

        public WpfDatasetSetupExecutionService(
            DatasetSetupPathService pathService,
            DatasetSetupDataService dataService)
            : base(pathService, dataService)
        {
        }

        public WpfDatasetSetupExecutionService(
            DatasetSetupPathService pathService,
            DatasetSetupDataService dataService,
            ProjectRecipeSessionService projectRecipeSessionService)
            : base(pathService, dataService, projectRecipeSessionService)
        {
        }
    }
}
