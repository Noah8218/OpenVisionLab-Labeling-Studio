using System;
using System.IO;

namespace MvcVisionSystem
{
    public enum WpfDatasetSetupExecutionFailure
    {
        None,
        InvalidRecipeName,
        DuplicateOutputRoot,
        SamplePreset
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

        public string ManifestPath { get; set; } = string.Empty;

        public WpfDatasetSamplePresetApplyResult SampleResult { get; set; }

        public CData Data { get; set; }
    }

    /// <summary>
    /// Creates the persisted dataset contract from a validated wizard request.
    /// It deliberately has no WPF window, view-model, or CGlobal dependency so
    /// the shell can remain an adapter for UI state and navigation.
    /// </summary>
    public sealed class WpfDatasetSetupExecutionService
    {
        private readonly WpfDatasetSetupPathService pathService;
        private readonly WpfDatasetSetupDataService dataService;

        public WpfDatasetSetupExecutionService()
            : this(new WpfDatasetSetupPathService(), new WpfDatasetSetupDataService())
        {
        }

        public WpfDatasetSetupExecutionService(
            WpfDatasetSetupPathService pathService,
            WpfDatasetSetupDataService dataService)
        {
            this.pathService = pathService ?? throw new ArgumentNullException(nameof(pathService));
            this.dataService = dataService ?? throw new ArgumentNullException(nameof(dataService));
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
            if (!WpfProjectRecipeService.IsValidRecipeName(recipeName))
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

            var data = new CData();
            data.ProjectSettings.EnsureDefaults();
            data.ProjectSettings.DatasetPurpose = request.Purpose;
            string selectedClassName = dataService.ApplyOutputRootAndClasses(data, outputRootPath, request.ClassNames);
            if (!WpfDatasetSamplePresetService.TryApplySample(request, data, out WpfDatasetSamplePresetApplyResult sampleResult, out string sampleError))
            {
                result.Failure = WpfDatasetSetupExecutionFailure.SamplePreset;
                result.SampleError = sampleError;
                return result;
            }

            string imageRootPath = sampleResult?.Applied == true && Directory.Exists(sampleResult.ImageRootPath)
                ? sampleResult.ImageRootPath
                : data.TrainImagesPath;
            data.ProjectSettings.PythonModel.ImageRootPath = imageRootPath;
            data.SaveYoloDataYaml();
            data.SaveConfig(recipeName);

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
}
