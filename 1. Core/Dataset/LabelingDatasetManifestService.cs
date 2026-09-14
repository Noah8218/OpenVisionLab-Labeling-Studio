using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MvcVisionSystem.Yolo;

namespace MvcVisionSystem
{
    public static class LabelingDatasetManifestService
    {
        public const string FileName = "dataset.manifest.json";

        public static string GetManifestPath(string recipeName)
        {
            return Path.Combine(AppContext.BaseDirectory, "RECIPE", recipeName ?? string.Empty, FileName);
        }

        public static void Save(LabelingProjectData data, string recipeName)
            => Save(data, recipeName, snapshot: null);

        public static void Save(LabelingProjectData data, string recipeName, RecipeDatasetVersionSnapshot snapshot)
        {
            if (data == null)
            {
                return;
            }

            string manifestPath = GetManifestPath(recipeName);
            string recipeDirectory = Path.GetDirectoryName(manifestPath);
            Directory.CreateDirectory(recipeDirectory);
            RecipeDatasetVersionSnapshot stored = RecipeDatasetVersionService.RecordSnapshot(
                recipeDirectory,
                snapshot ?? RecipeDatasetVersionService.CreateSnapshot(data));
            AnnotationFilePersistence.WriteAtomically(manifestPath,
                temporaryPath => File.WriteAllText(temporaryPath, JsonConvert.SerializeObject(Build(data, recipeName, stored), Formatting.Indented)));
        }

        public static LabelingDatasetManifest Build(LabelingProjectData data, string recipeName)
            => Build(data, recipeName, RecipeDatasetVersionService.CreateSnapshot(data));

        private static LabelingDatasetManifest Build(
            LabelingProjectData data,
            string recipeName,
            RecipeDatasetVersionSnapshot datasetVersion)
        {
            data ??= new LabelingProjectData();
            LabelingProjectSettings settings = data.ProjectSettings ?? new LabelingProjectSettings();
            settings.EnsureDefaults();
            YoloDatasetStatistics statistics = YoloDatasetValidator.BuildStatistics(data);
            AnomalyImageReviewSummary anomalySummary = settings.DatasetPurpose == LabelingDatasetPurpose.AnomalyDetection
                ? AnomalyImageReviewStatusService.LoadPersistedSummary(data, statistics.TotalImageCount)
                : new AnomalyImageReviewSummary();
            LabelingDatasetManifestArtifactSummary artifactSummary = BuildArtifactSummary(settings.DatasetPurpose, statistics, anomalySummary);

            var manifest = new LabelingDatasetManifest
            {
                RecipeName = recipeName ?? string.Empty,
                GeneratedUtc = DateTime.UtcNow.ToString("O"),
                DatasetPurpose = settings.DatasetPurpose.ToString(),
                AnnotationProfile = ResolveAnnotationProfile(settings.DatasetPurpose),
                VisibleTools = ResolveVisibleTools(settings.DatasetPurpose).ToList(),
                OutputRootPath = data.OutputRootPath,
                ImageRootPath = settings.ResolveImageRootPath(),
                DataYamlFilePath = data.DataYamlFilePath,
                Classes = data.ClassNamedList?
                    .Select(item => item?.Text)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList() ?? new List<string>(),
                Training = new LabelingDatasetManifestTraining
                {
                    ValidationPercent = settings.YoloDataset?.ValidationPercent ?? 0,
                    TestPercent = settings.YoloDataset?.TestPercent ?? 0,
                    SplitSeed = settings.YoloDataset?.SplitSeed ?? 0
                },
                ArtifactSummary = artifactSummary,
                DatasetVersionId = datasetVersion?.DatasetVersionId ?? string.Empty,
                ContentIdentity = new LabelingDatasetManifestContentIdentity
                {
                    IdentitySchemaVersion = datasetVersion?.IdentitySchemaVersion ?? RecipeDatasetVersionService.IdentitySchemaVersion,
                    Algorithm = datasetVersion?.Algorithm ?? RecipeDatasetVersionService.Algorithm,
                    ContentSha256 = datasetVersion?.ContentSha256 ?? string.Empty,
                    ClassContractSha256 = datasetVersion?.ClassContractSha256 ?? string.Empty,
                    SplitContractSha256 = datasetVersion?.SplitContractSha256 ?? string.Empty,
                    FileCount = datasetVersion?.FileCount ?? 0,
                    ImageFileCount = datasetVersion?.ImageFileCount ?? 0,
                    AnnotationFileCount = datasetVersion?.AnnotationFileCount ?? 0,
                    HistoryEntry = Path.Combine(
                            RecipeDatasetVersionService.HistoryDirectoryName,
                            (datasetVersion?.DatasetVersionId ?? string.Empty) + ".json")
                        .Replace(Path.DirectorySeparatorChar, '/')
                }
            };
            return manifest;
        }

        private static string ResolveAnnotationProfile(LabelingDatasetPurpose purpose)
        {
            return purpose switch
            {
                LabelingDatasetPurpose.Segmentation => "mask-and-polygon",
                LabelingDatasetPurpose.AnomalyDetection => "image-level-normal-abnormal",
                _ => "bounding-box"
            };
        }

        private static IEnumerable<string> ResolveVisibleTools(LabelingDatasetPurpose purpose)
        {
            return purpose switch
            {
                LabelingDatasetPurpose.Segmentation => new[] { "select", "polygon", "brush", "eraser", "panZoom" },
                LabelingDatasetPurpose.AnomalyDetection => new[] { "panZoom" },
                _ => new[] { "select", "rectangle", "panZoom" }
            };
        }

        private static LabelingDatasetManifestArtifactSummary BuildArtifactSummary(
            LabelingDatasetPurpose purpose,
            YoloDatasetStatistics statistics,
            AnomalyImageReviewSummary anomalySummary)
        {
            statistics ??= new YoloDatasetStatistics();
            anomalySummary ??= new AnomalyImageReviewSummary();
            string primaryLabelKind = purpose switch
            {
                LabelingDatasetPurpose.Segmentation => statistics.TotalSegmentationObjectCount > 0
                    ? "segments"
                    : "masks",
                LabelingDatasetPurpose.AnomalyDetection => "image-level-normal-abnormal",
                _ => "boxes"
            };

            int primaryLabelCount = purpose == LabelingDatasetPurpose.AnomalyDetection
                ? anomalySummary.ReviewedImageCount
                : primaryLabelKind == "segments"
                ? statistics.TotalSegmentationObjectCount
                : primaryLabelKind == "masks"
                    ? statistics.TotalMaskFileCount
                    : statistics.TotalObjectCount;

            return new LabelingDatasetManifestArtifactSummary
            {
                PrimaryLabelKind = primaryLabelKind,
                PrimaryLabelCount = primaryLabelCount,
                ImageCount = statistics.TotalImageCount,
                AnomalyReviewedImageCount = anomalySummary.ReviewedImageCount,
                AnomalyNormalImageCount = anomalySummary.NormalImageCount,
                AnomalyAbnormalImageCount = anomalySummary.AbnormalImageCount,
                AnomalyUnreviewedImageCount = anomalySummary.UnreviewedImageCount,
                BoxObjectCount = statistics.TotalObjectCount,
                BoxLabelFileCount = statistics.TotalLabelFileCount,
                SegmentObjectCount = statistics.TotalSegmentationObjectCount,
                SegmentFileCount = statistics.TotalSegmentFileCount,
                MaskFileCount = statistics.TotalMaskFileCount
            };
        }

    }
}
