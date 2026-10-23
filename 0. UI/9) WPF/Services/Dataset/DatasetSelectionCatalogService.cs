using Newtonsoft.Json;
using MvcVisionSystem.Yolo;
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
        private static readonly ImageQueueSelectionService ImageQueueSelectionService = new ImageQueueSelectionService();

        public DatasetSelectionWorkHistorySnapshot LoadWorkHistory(
            string recipeRootPath,
            string recipeName,
            string datasetPurpose)
        {
            LabelingProjectData data = TryReadRecipeConfig(ProjectRecipeService.BuildConfigPath(recipeRootPath, recipeName));
            if (data == null)
            {
                return new DatasetSelectionWorkHistorySnapshot();
            }

            LabelingDatasetManifest manifest = TryReadManifest(ProjectRecipeService.BuildManifestPath(recipeRootPath, recipeName));
            string imageRootPath = FirstNonEmpty(manifest?.ImageRootPath, data.ProjectSettings?.ResolveImageRootPath());
            var history = new DatasetSelectionWorkHistorySnapshot { IsConfigurationAvailable = true };
            try
            {
                IReadOnlyList<string> sourceRoots = data.ProjectSettings.ResolveImageRootPaths(imageRootPath);
                if (sourceRoots.Any(Directory.Exists) && (data.ProjectSettings.AdditionalImageRootPaths?.Count == 0 || sourceRoots.All(Directory.Exists)))
                {
                    List<string> imagePaths = ImageQueueSelectionService.EnumerateImageFiles(sourceRoots);
                    history.IsReviewAvailable = true;
                    history.ReviewImageCount = imagePaths.Count;
                    foreach (string imagePath in imagePaths)
                    {
                        string split = YoloDatasetSplitService.SelectModesForImage(Path.GetFileNameWithoutExtension(imagePath), data.ProjectSettings.YoloDataset).Single();
                        if (split == YoloDatasetSplitService.TrainMode) history.TrainImageCount++;
                        else if (split == YoloDatasetSplitService.ValidMode) history.ValidImageCount++;
                        else if (split == YoloDatasetSplitService.TestMode) history.TestImageCount++;
                        else if (split == YoloDatasetSplitService.ExcludedMode) history.ExcludedImageCount++;
                    }
                    var qualityReview = new YoloImageReviewStatusService();
                    qualityReview.LoadReviewStatus(data, imagePaths);
                    IReadOnlyList<YoloImageReviewStatus> statuses = qualityReview.GetItems();
                    history.ReviewedImageCount = statuses.Count(item => item.QualityReviewState == YoloImageQualityReviewState.Reviewed);
                    history.NeedsFixImageCount = statuses.Count(item => item.QualityReviewState == YoloImageQualityReviewState.NeedsFix);
                    var needsFixPaths = new HashSet<string>(statuses.Where(item => item.QualityReviewState == YoloImageQualityReviewState.NeedsFix).Select(item => item.ImagePath), StringComparer.OrdinalIgnoreCase);
                    if (data.ProjectSettings?.DatasetPurpose == LabelingDatasetPurpose.AnomalyDetection
                        || string.Equals(datasetPurpose, nameof(LabelingDatasetPurpose.AnomalyDetection), StringComparison.OrdinalIgnoreCase))
                    {
                        AnomalyImageReviewSummary summary = AnomalyImageReviewStatusService.LoadPersistedSummary(data, imagePaths.Count);
                        history.NormalImageCount = summary.NormalImageCount;
                        history.AbnormalImageCount = summary.AbnormalImageCount;
                        var reviewStatus = new AnomalyImageReviewStatusService();
                        reviewStatus.LoadReviewStatus(data, imagePaths);
                        history.UnworkedImageCount = reviewStatus.GetItems().Count(item => item.ReviewState == AnomalyImageReviewState.Unreviewed && !needsFixPaths.Contains(item.ImagePath));
                    }
                    else
                    {
                        history.UnworkedImageCount = imagePaths.Count(path => !needsFixPaths.Contains(path) && (data.ProjectSettings.DatasetPurpose == LabelingDatasetPurpose.Segmentation
                            ? !YoloSegmentationAnnotationService.GetCandidateSegmentPaths(path, data).Any(File.Exists) && !YoloAnnotationService.GetCandidateLabelPaths(path, data).Any(File.Exists)
                            : !YoloAnnotationService.GetCandidateLabelPaths(path, data).Any(File.Exists)));
                    }
                }
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException || error is NotSupportedException)
            {
                history.IsReviewAvailable = false;
            }

            ModelRegistrySettings registry = data.ProjectSettings?.ModelRegistry;
            registry?.EnsureDefaults();
            YoloTrainingGuideHistory trainingGuide = data.ProjectSettings?.TrainingGuide;
            trainingGuide?.EnsureDefaults();
            var trainingRunIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (TrainingRun run in registry?.TrainingRuns ?? new List<TrainingRun>())
            {
                if (!string.IsNullOrWhiteSpace(run?.TrainingRunId))
                {
                    trainingRunIds.Add(run.TrainingRunId);
                }
            }

            foreach (YoloTrainingGuideRunRecord run in trainingGuide?.RunHistory ?? new List<YoloTrainingGuideRunRecord>())
            {
                if (!string.IsNullOrWhiteSpace(run?.TrainingRunId))
                {
                    trainingRunIds.Add(run.TrainingRunId);
                }
            }

            if (!string.IsNullOrWhiteSpace(trainingGuide?.LastTrainingRunId))
            {
                trainingRunIds.Add(trainingGuide.LastTrainingRunId);
            }

            TrainingRun latestRun = ModelRegistryService.FindLatestTrainingRun(registry);
            ModelProfile latestProfile = latestRun == null
                ? null
                : registry?.Profiles?.FirstOrDefault(item => string.Equals(item?.ProfileId, latestRun.ProfileId, StringComparison.Ordinal));
            history.TrainingRunCount = trainingRunIds.Count;
            history.HasTrainingRecord = trainingRunIds.Count > 0 || !string.IsNullOrWhiteSpace(trainingGuide?.LastTrainingState);
            history.IsLatestTrainingCompleted = TrainingWeightsService.IsCompletedTrainingState(latestRun != null ? latestRun.State : trainingGuide?.LastTrainingState);
            history.LatestTrainingName = FirstNonEmpty(
                latestProfile?.DisplayName,
                latestRun?.RunName,
                trainingGuide?.LastTrainingRunName);
            ModelCandidate inspection = ModelRegistryService.FindCurrentInspectionModel(registry);
            ModelProfile inspectionProfile = inspection == null ? null : registry?.Profiles?.FirstOrDefault(item => item?.ProfileId == inspection.ProfileId);
            history.InspectionModelName = inspection == null ? string.Empty : FirstNonEmpty(inspectionProfile?.DisplayName, Path.GetFileNameWithoutExtension(inspection.WeightsPath));
            history.InspectionMetrics = inspection?.MetricsSummary ?? string.Empty;
            history.HasInspectionModel = inspection != null;
            TrainingRun[] inspectionRuns = registry?.TrainingRuns?.Where(run => run != null && run.TrainingRunId == inspection?.TrainingRunId).ToArray() ?? Array.Empty<TrainingRun>();
            TrainingRun inspectionRun = inspectionRuns.Length == 1 ? inspectionRuns[0] : null;
            ModelDecisionRule rule = inspectionRun?.DecisionRule;
            history.DecisionRuleStatus = ModelRegistryService.ValidateDecisionRule(rule);
            if (rule != null && history.DecisionRuleStatus.Length == 0)
            {
                try
                {
                    RecipeDatasetVersionSnapshot version = RecipeDatasetVersionService.CreateSnapshot(data);
                    string weights = File.Exists(inspection.WeightsPath) ? inspection.WeightsPath : inspection.ArtifactPath;
                    if (rule.DatasetPurpose != data.ProjectSettings.DatasetPurpose || !ModelArtifactStoreService.Verify(weights, rule.WeightsSha256)
                        || !string.Equals(inspection.WeightsSha256, rule.WeightsSha256, StringComparison.OrdinalIgnoreCase)
                        || !ModelRegistryService.DecisionRuleMatches(rule, inspectionRun.TrainingRunId, inspectionRun.WeightsSha256,
                            version.DatasetVersionId, version.ContentSha256, RecipeDatasetVersionService.CreateDecisionReviewEvidenceSha256(data)))
                        history.DecisionRuleStatus = "RuleStale";
                    else history.InspectionDecisionRule = rule.Copy();
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException || error is InvalidDataException)
                { history.DecisionRuleStatus = "RuleStale"; }
            }
            return history;
        }

        public IReadOnlyList<DatasetSelectionSnapshot> Load(
            string recipeRootPath,
            string currentRecipeName)
        {
            return ProjectRecipeService.ListRecipeNames(recipeRootPath)
                .Select(recipeName => BuildSnapshot(recipeRootPath, recipeName, currentRecipeName))
                .ToArray();
        }

        public IReadOnlyList<string> LoadRecipePreviewImagePaths(string imageRootPath)
            => EnumerateRecipePreviewImagePaths(imageRootPath);

        private static IReadOnlyList<string> EnumerateRecipePreviewImagePaths(string imageRootPath)
        {
            try
            {
                return ImageQueueSelectionService.EnumerateImageFiles(imageRootPath);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException || error is NotSupportedException)
            {
                return Array.Empty<string>();
            }
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

            string imageRootPath = FirstNonEmpty(manifest?.ImageRootPath, recipeData?.ProjectSettings?.ResolveImageRootPath());
            LabelingProjectSettings settings = recipeData?.ProjectSettings;
            RecipeImageSelectionMode previewImageMode = settings?.RecipeListPreviewImageMode ?? RecipeImageSelectionMode.Automatic;
            string previewImageRelativePath = settings?.RecipeListPreviewImageRelativePath ?? string.Empty;
            int imageCount = manifest?.ArtifactSummary?.ImageCount ?? 0;
            IReadOnlyList<string> sourceRoots = settings?.ResolveImageRootPaths(imageRootPath) ?? new[] { imageRootPath };
            if (sourceRoots.Any(Directory.Exists) && (settings?.AdditionalImageRootPaths?.Count == 0 || sourceRoots.All(Directory.Exists)))
            {
                try
                {
                    imageCount = ImageQueueSelectionService.EnumerateImageFiles(sourceRoots).Count;
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException || error is NotSupportedException)
                {
                    // An unavailable source retains the last persisted count; work-history availability is checked separately.
                }
            }
            string previewImagePath = ResolveRecipePreviewImagePath(
                imageRootPath,
                previewImageMode,
                previewImageRelativePath,
                out bool hasMissingPreviewImage);
            return new DatasetSelectionSnapshot(
                recipeName,
                manifest?.DatasetPurpose ?? string.Empty,
                FirstNonEmpty(manifest?.OutputRootPath, recipeData?.OutputRootPath),
                imageRootPath,
                classes,
                imageCount,
                manifest?.ArtifactSummary?.PrimaryLabelCount ?? 0,
                manifestPath,
                File.Exists(manifestPath),
                string.Equals(recipeName, currentRecipeName, StringComparison.OrdinalIgnoreCase),
                GetLastModifiedUtc(recipeRootPath, recipeName, manifestPath),
                recipeData?.ProjectSettings?.ProductCode,
                recipeData?.ProjectSettings?.RecipeDescription,
                recipeData != null,
                previewImagePath,
                previewImageMode,
                previewImageRelativePath,
                hasMissingPreviewImage,
                recipeData?.ClassNamedList?.Where(item => item != null).Select(item => new LabelClass { Text = item.Text, DrawColor = item.DrawColor, IsArchived = item.IsArchived }).ToArray());
        }

        private static string ResolveRecipePreviewImagePath(
            string imageRootPath,
            RecipeImageSelectionMode mode,
            string relativePath,
            out bool hasMissingImage)
        {
            hasMissingImage = false;
            if (mode == RecipeImageSelectionMode.Hidden)
            {
                return string.Empty;
            }

            if (mode == RecipeImageSelectionMode.Selected)
            {
                string expectedRelativePath = NormalizeRelativePath(relativePath);
                string selectedPath = string.IsNullOrWhiteSpace(expectedRelativePath)
                    ? string.Empty
                    : EnumerateRecipePreviewImagePaths(imageRootPath)
                        .FirstOrDefault(path => string.Equals(
                            NormalizeRelativePath(Path.GetRelativePath(imageRootPath, path)),
                            expectedRelativePath,
                            StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(selectedPath))
                {
                    return selectedPath;
                }

                hasMissingImage = true;
            }

            return ImageQueueSelectionService.FindFirstImageFile(imageRootPath);
        }

        private static string NormalizeRelativePath(string path)
            => (path ?? string.Empty).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

        private static DateTime GetLastModifiedUtc(string recipeRootPath, string recipeName, string manifestPath)
        {
            try
            {
                return File.Exists(manifestPath)
                    ? File.GetLastWriteTimeUtc(manifestPath)
                    : Directory.GetLastWriteTimeUtc(ProjectRecipeService.BuildConfigDirectory(recipeRootPath, recipeName));
            }
            catch
            {
                return DateTime.MinValue;
            }
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

    public sealed class DatasetSelectionWorkHistorySnapshot
    {
        public bool IsConfigurationAvailable { get; set; }

        public bool IsReviewAvailable { get; set; }

        public int ReviewImageCount { get; set; }

        public int? UnworkedImageCount { get; set; }

        public int ReviewedImageCount { get; set; }

        public int NeedsFixImageCount { get; set; }

        public int NormalImageCount { get; set; }

        public int AbnormalImageCount { get; set; }

        public bool HasTrainingRecord { get; set; }

        public bool IsLatestTrainingCompleted { get; set; }

        public int TrainingRunCount { get; set; }

        public string LatestTrainingName { get; set; } = string.Empty;

        public int TrainImageCount { get; set; }
        public int ValidImageCount { get; set; }
        public int TestImageCount { get; set; }
        public int ExcludedImageCount { get; set; }
        public bool HasInspectionModel { get; set; }
        public string InspectionModelName { get; set; } = string.Empty;
        public string InspectionMetrics { get; set; } = string.Empty;
        public string DecisionRuleStatus { get; set; } = "RuleUnconfigured";
        public ModelDecisionRule InspectionDecisionRule { get; set; }
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
            bool isCurrent,
            DateTime lastModifiedUtc = default,
            string productCode = "",
            string recipeDescription = "",
            bool hasConfiguration = false,
            string previewImagePath = "",
            RecipeImageSelectionMode previewImageMode = RecipeImageSelectionMode.Automatic,
            string previewImageRelativePath = "",
            bool hasMissingPreviewImage = false,
            IReadOnlyList<LabelClass> labelClasses = null)
        {
            RecipeName = recipeName ?? string.Empty;
            DatasetPurpose = datasetPurpose ?? string.Empty;
            OutputRootPath = outputRootPath ?? string.Empty;
            ImageRootPath = imageRootPath ?? string.Empty;
            Classes = classes?.ToArray() ?? Array.Empty<string>();
            LabelClasses = labelClasses?.ToArray() ?? Array.Empty<LabelClass>();
            ImageCount = imageCount;
            LabelCount = labelCount;
            ManifestPath = manifestPath ?? string.Empty;
            HasManifest = hasManifest;
            IsCurrent = isCurrent;
            LastModifiedUtc = lastModifiedUtc;
            ProductCode = productCode ?? string.Empty;
            RecipeDescription = recipeDescription ?? string.Empty;
            HasConfiguration = hasConfiguration;
            PreviewImagePath = previewImagePath ?? string.Empty;
            PreviewImageMode = previewImageMode;
            PreviewImageRelativePath = previewImageRelativePath ?? string.Empty;
            HasMissingPreviewImage = hasMissingPreviewImage;
        }

        public string RecipeName { get; }

        public string DatasetPurpose { get; }

        public string OutputRootPath { get; }

        public string ImageRootPath { get; }

        public IReadOnlyList<string> Classes { get; }

        public IReadOnlyList<LabelClass> LabelClasses { get; }

        public int ImageCount { get; }

        public int LabelCount { get; }

        public string ManifestPath { get; }

        public bool HasManifest { get; }

        public bool IsCurrent { get; }

        public DateTime LastModifiedUtc { get; }

        public string ProductCode { get; }

        public string RecipeDescription { get; }

        public bool HasConfiguration { get; }

        public string PreviewImagePath { get; }

        public RecipeImageSelectionMode PreviewImageMode { get; }

        public string PreviewImageRelativePath { get; }

        public bool HasMissingPreviewImage { get; }
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
            bool isCurrent,
            DateTime lastModifiedUtc = default,
            string productCode = "",
            string recipeDescription = "",
            bool hasConfiguration = false,
            string previewImagePath = "",
            RecipeImageSelectionMode previewImageMode = RecipeImageSelectionMode.Automatic,
            string previewImageRelativePath = "",
            bool hasMissingPreviewImage = false)
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
                isCurrent,
                lastModifiedUtc,
                productCode,
                recipeDescription,
                hasConfiguration,
                previewImagePath,
                previewImageMode,
                previewImageRelativePath,
                hasMissingPreviewImage)
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
                    source.IsCurrent,
                    source.LastModifiedUtc,
                    source.ProductCode,
                    source.RecipeDescription,
                    source.HasConfiguration,
                    source.PreviewImagePath,
                    source.PreviewImageMode,
                    source.PreviewImageRelativePath,
                    source.HasMissingPreviewImage);
    }
}
