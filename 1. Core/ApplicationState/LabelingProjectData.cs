using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using MvcVisionSystem.Yolo;

namespace MvcVisionSystem
{
    // Keep the VISION.xml root/type contract stable while the CLR type gets a
    // domain-specific name. Existing Recipes must remain readable.
    [XmlRoot("CData")]
    [XmlType(TypeName = "CData")]
    public class LabelingProjectData
    {
        [XmlArrayItem("CClassItem", typeof(LabelClass))]
        public List<LabelClass> ClassNamedList { get; set; } = new List<LabelClass>();

        public string OutputDataYamlPath { get; set; } = "";

        public string OutputDataImageAndTxtPath { get; set; } = "";

        public YoloV5TrainingParameters TranningParam { get; set; } = new YoloV5TrainingParameters();

        [XmlIgnore]
        public YoloV5TrainingParameters TrainingParam
        {
            get => TranningParam;
            set => TranningParam = value ?? new YoloV5TrainingParameters();
        }

        public LabelingProjectSettings ProjectSettings { get; set; } = new LabelingProjectSettings();

        // Legacy source-compatibility members only. Runtime detection and
        // annotation persistence use LabelingImageWorkspace.CaptureSnapshot().
        [XmlIgnore] public string LastSelectImageName { get; set; } = "";

        [XmlIgnore] public string LastSelectImagePath { get; set; } = "";

        [XmlIgnore] public string OutputRootPath => ResolveOutputRootPath();

        [XmlIgnore] public string DataYamlFilePath => ResolveDataYamlFilePath();

        [XmlIgnore] public string TrainImagesPath => Path.Combine(OutputRootPath, "data", "train", "images");

        [XmlIgnore] public string ValidImagesPath => Path.Combine(OutputRootPath, "data", "valid", "images");

        [XmlIgnore] public string TestImagesPath => Path.Combine(OutputRootPath, "data", "test", "images");

        public IReadOnlyList<string> GetDatasetImageDirectories()
            => new[] { TrainImagesPath, ValidImagesPath, TestImagesPath };

        public LabelingProjectData()
        {
            // Dataset folders are created by the selected setup/save workflow.
            // Construction must remain side-effect free for packaged startup.
        }
        public RecipeConfigurationLoadResult TryLoadConfig(string recipeName)
        {
            RecipeConfigurationLoadResult result = new RecipeConfigurationStore().Load(GetRecipeConfigPath(recipeName));
            if (!result.IsSuccess)
            {
                return result;
            }

            try
            {
                result.Data.NormalizeOutputPaths();
                result.Data.NormalizeTrainingSettings();
                return result;
            }
            catch (Exception error) when (error is ArgumentException
                || error is IOException
                || error is UnauthorizedAccessException)
            {
                AppLog.ABNORMAL($"Recipe configuration validation failed: {result.Path} / {error.Message}");
                return new RecipeConfigurationLoadResult(
                    null,
                    result.Path,
                    RecipeConfigurationFailureKind.ValidationFailed,
                    error.Message);
            }
        }

        public LabelingProjectData LoadConfig(string recipeName)
        {
            RecipeConfigurationLoadResult result = TryLoadConfig(recipeName);
            if (result.IsSuccess)
            {
                return result.Data;
            }

            if (result.FailureKind != RecipeConfigurationFailureKind.Missing)
            {
                return null;
            }

            RecipeConfigurationSaveResult saveResult = SaveConfig(recipeName);
            return saveResult.IsSuccess ? TryLoadConfig(recipeName).Data : null;
        }

        public RecipeConfigurationSaveResult SaveConfig(string recipeName, bool refreshDatasetVersion = true)
        {
            if (!TryPrepareConfigSave(recipeName, out RecipeConfigurationSaveResult preparationFailure))
            {
                return preparationFailure;
            }

            RecipeConfigurationSaveResult result = null;
            AnnotationFilePersistence.ExecuteTransaction(() =>
            {
                result = new RecipeConfigurationStore().Save(GetRecipeConfigPath(recipeName), this);
                if (!result.IsSuccess)
                {
                    return false;
                }

                if (refreshDatasetVersion)
                {
                    LabelingDatasetManifestService.Save(this, recipeName);
                }

                return true;
            });

            return result;
        }

        /// <summary>
        /// Saves the class-dependent YOLO manifest and Recipe configuration as
        /// one in-process outcome. A YAML write is enrolled in the existing
        /// file transaction so it is rolled back if the later XML save fails.
        /// </summary>
        public RecipeConfigurationSaveResult SaveConfigAndYoloDataYaml(
            string recipeName,
            bool refreshDatasetVersion = true)
        {
            if (!TryPrepareConfigSave(recipeName, out RecipeConfigurationSaveResult preparationFailure))
            {
                return preparationFailure;
            }

            string recipeConfigPath = GetRecipeConfigPath(recipeName);
            RecipeConfigurationSaveResult recipeResult = null;
            try
            {
                bool committed = AnnotationFilePersistence.ExecuteTransaction(() =>
                {
                    SaveYoloDataYaml();
                    recipeResult = new RecipeConfigurationStore().Save(recipeConfigPath, this);
                    if (recipeResult.IsSuccess && refreshDatasetVersion)
                    {
                        LabelingDatasetManifestService.Save(this, recipeName);
                    }
                    return recipeResult.IsSuccess;
                });

                if (!committed)
                {
                    string errorMessage = recipeResult?.ErrorMessage ?? "Recipe configuration was not saved.";
                    return new RecipeConfigurationSaveResult(
                        recipeConfigPath,
                        recipeResult?.BackupPath ?? string.Empty,
                        recipeResult?.FailureKind ?? RecipeConfigurationFailureKind.WriteFailed,
                        $"data.yaml was rolled back because Recipe configuration save failed: {errorMessage}");
                }
            }
            catch (Exception error) when (error is IOException
                || error is UnauthorizedAccessException
                || error is InvalidOperationException
                || error is AggregateException)
            {
                string errorMessage = error.GetBaseException().Message;
                AppLog.ABNORMAL($"Recipe/YAML paired save failed: {recipeName} / {errorMessage}");
                return new RecipeConfigurationSaveResult(
                    recipeConfigPath,
                    failureKind: RecipeConfigurationFailureKind.WriteFailed,
                    errorMessage: $"data.yaml was not saved and Recipe configuration was not changed: {errorMessage}");
            }

            return recipeResult;
        }

        public void ConfigureOutputRoot(string outputRootPath)
        {
            if (string.IsNullOrWhiteSpace(outputRootPath))
            {
                return;
            }

            OutputDataImageAndTxtPath = outputRootPath;
            OutputDataYamlPath = Path.Combine(outputRootPath, "data.yaml");
            EnsureProjectSettings();
            ProjectSettings.YoloDataset.ConfigureOutputRoot(outputRootPath);
        }

        public void NormalizeOutputPaths()
        {
            EnsureProjectSettings();
            string outputRootPath = ResolveOutputRootPath();
            ProjectSettings.YoloDataset.ConfigureOutputRoot(outputRootPath);
            OutputDataImageAndTxtPath = ProjectSettings.YoloDataset.OutputRootPath;
            OutputDataYamlPath = ProjectSettings.YoloDataset.DataYamlFilePath;
        }

        public void NormalizeTrainingSettings()
        {
            EnsureProjectSettings();
            ProjectSettings.Training.CopyFrom(TranningParam);
        }

        public TrainingSettings GetTrainingSettings()
        {
            NormalizeTrainingSettings();
            return ProjectSettings.Training;
        }

        public void SaveYoloDataYaml()
        {
            NormalizeOutputPaths();
            EnsureYoloOutputDirectories();
            List<string> classNames = ClassNamedList.Select(item => item.Text).ToList();
            YoloDatasetWriter.CreateYaml(TrainImagesPath, ValidImagesPath, TestImagesPath, classNames, DataYamlFilePath);
        }

        public void EnsureYoloOutputDirectories()
        {
            Directory.CreateDirectory(OutputRootPath);
            Directory.CreateDirectory(TrainImagesPath);
            Directory.CreateDirectory(Path.Combine(OutputRootPath, "data", "train", "labels"));
            Directory.CreateDirectory(ValidImagesPath);
            Directory.CreateDirectory(Path.Combine(OutputRootPath, "data", "valid", "labels"));
            Directory.CreateDirectory(TestImagesPath);
            Directory.CreateDirectory(Path.Combine(OutputRootPath, "data", "test", "labels"));
        }

        private string ResolveOutputRootPath()
        {
            EnsureProjectSettings();
            string settingsOutputRoot = ProjectSettings.YoloDataset.ResolveOutputRootPath("");
            if (!string.IsNullOrWhiteSpace(settingsOutputRoot))
            {
                return settingsOutputRoot;
            }

            if (!string.IsNullOrWhiteSpace(OutputDataImageAndTxtPath))
            {
                return OutputDataImageAndTxtPath;
            }

            if (!string.IsNullOrWhiteSpace(OutputDataYamlPath))
            {
                if (IsYamlFilePath(OutputDataYamlPath))
                {
                    string directoryName = Path.GetDirectoryName(OutputDataYamlPath);
                    if (!string.IsNullOrWhiteSpace(directoryName))
                    {
                        return directoryName;
                    }
                }

                return OutputDataYamlPath;
            }

            return Path.Combine(AppContext.BaseDirectory, "DATA");
        }

        private string ResolveDataYamlFilePath()
        {
            EnsureProjectSettings();
            if (!string.IsNullOrWhiteSpace(ProjectSettings.YoloDataset.DataYamlFilePath)
                && YoloDatasetSettings.IsYamlFilePath(ProjectSettings.YoloDataset.DataYamlFilePath))
            {
                return ProjectSettings.YoloDataset.DataYamlFilePath;
            }

            if (!string.IsNullOrWhiteSpace(OutputDataYamlPath) && IsYamlFilePath(OutputDataYamlPath))
            {
                return OutputDataYamlPath;
            }

            return Path.Combine(OutputRootPath, "data.yaml");
        }

        internal static string GetRecipeConfigPath(string recipeName)
        {
            return Path.Combine(AppContext.BaseDirectory, "RECIPE", recipeName ?? string.Empty, "VISION.xml");
        }

        private static bool IsYamlFilePath(string path)
        {
            return YoloDatasetSettings.IsYamlFilePath(path);
        }

        private bool TryPrepareConfigSave(
            string recipeName,
            out RecipeConfigurationSaveResult failure)
        {
            failure = null;
            try
            {
                NormalizeOutputPaths();
                NormalizeTrainingSettings();
                return true;
            }
            catch (Exception error) when (error is ArgumentException
                || error is IOException
                || error is UnauthorizedAccessException)
            {
                AppLog.ABNORMAL($"Recipe configuration preparation failed: {recipeName} / {error.Message}");
                failure = new RecipeConfigurationSaveResult(
                    GetRecipeConfigPath(recipeName),
                    failureKind: RecipeConfigurationFailureKind.ValidationFailed,
                    errorMessage: error.Message);
                return false;
            }
        }

        private void EnsureProjectSettings()
        {
            ProjectSettings ??= new LabelingProjectSettings();
            ProjectSettings.EnsureDefaults();
        }
    }

}
