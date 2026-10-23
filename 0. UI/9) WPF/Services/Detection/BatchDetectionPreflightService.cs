using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MvcVisionSystem
{
    public enum WpfBatchExistingLabelPolicy
    {
        SkipLabeled,
        IncludeAndKeep
    }

    public sealed class WpfBatchDetectionPreflightRequest
    {
        public LabelingProjectData Data { get; set; }

        public IReadOnlyList<WpfImageQueueItem> Items { get; set; } =
            Array.Empty<WpfImageQueueItem>();

        public string ScopeText { get; set; } = string.Empty;

        public WpfBatchExistingLabelPolicy ExistingLabelPolicy { get; set; } =
            WpfBatchExistingLabelPolicy.SkipLabeled;
    }

    public sealed class WpfBatchClassMappingItem
    {
        public WpfBatchClassMappingItem(int index, string recipeClassName)
        {
            Index = index;
            RecipeClassName = recipeClassName ?? string.Empty;
        }

        public int Index { get; }

        public string RecipeClassName { get; }

        public string WorkerMappingText =>
            string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfBatch.ClassMapping.Display"), RecipeClassName, RecipeClassName);
    }

    public sealed class WpfBatchDetectionPreflightReport
    {
        public IReadOnlyList<WpfImageQueueItem> RunnableItems { get; init; } =
            Array.Empty<WpfImageQueueItem>();

        public IReadOnlyList<WpfBatchClassMappingItem> ClassMappings { get; init; } =
            Array.Empty<WpfBatchClassMappingItem>();

        public IReadOnlyList<string> Issues { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

        public int RequestedCount { get; init; }

        public int MissingImageCount { get; init; }

        public int ExistingLabelCount { get; init; }

        public int SkippedExistingLabelCount { get; init; }

        public string ScopeText { get; init; } = string.Empty;

        public string DatasetPurposeText { get; init; } = string.Empty;

        public string ModelEngineText { get; init; } = string.Empty;

        public string WeightsPath { get; init; } = string.Empty;

        public string ConfidenceText { get; init; } = string.Empty;

        public string ExistingLabelPolicyText { get; init; } = string.Empty;

        public string DestinationPolicyText { get; init; } =
            OpenVisionLab.OpenVisionLanguageService.T("WpfBatch.DestinationPolicy");

        public bool CanStart => Issues.Count == 0 && RunnableItems.Count > 0;
    }

    public sealed class WpfBatchDetectionPlan
    {
        public WpfBatchDetectionPlan(
            IReadOnlyList<WpfImageQueueItem> items,
            string scopeText,
            WpfBatchExistingLabelPolicy existingLabelPolicy)
        {
            Items = items ?? Array.Empty<WpfImageQueueItem>();
            ScopeText = scopeText ?? string.Empty;
            ExistingLabelPolicy = existingLabelPolicy;
        }

        public IReadOnlyList<WpfImageQueueItem> Items { get; }

        public string ScopeText { get; }

        public WpfBatchExistingLabelPolicy ExistingLabelPolicy { get; }
    }

    public class BatchDetectionPreflightService
    {
        private readonly DetectionTargetService targetService;

        public BatchDetectionPreflightService(DetectionTargetService targetService = null)
        {
            this.targetService = targetService ?? new DetectionTargetService();
        }

        public WpfBatchDetectionPreflightReport DryRun(WpfBatchDetectionPreflightRequest request)
        {
            request ??= new WpfBatchDetectionPreflightRequest();
            LabelingProjectData data = request.Data;
            PythonModelSettings settings = data?.ProjectSettings?.PythonModel;
            var issues = new List<string>();
            var warnings = new List<string>();
            IReadOnlyList<WpfImageQueueItem> requested = (request.Items ?? Array.Empty<WpfImageQueueItem>())
                .Where(item => item != null)
                .GroupBy(item => item.ImagePath ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
            IReadOnlyList<WpfImageQueueItem> physicalItems = targetService.BuildBatchQueue(requested);
            int missingImageCount = requested.Count - physicalItems.Count;
            int existingLabelCount = physicalItems.Count(item => item.IsLabeled);
            IReadOnlyList<WpfImageQueueItem> runnable = request.ExistingLabelPolicy == WpfBatchExistingLabelPolicy.SkipLabeled
                ? physicalItems.Where(item => !item.IsLabeled).ToList()
                : physicalItems;
            int skippedExistingLabelCount = physicalItems.Count - runnable.Count;

            if (requested.Count == 0)
            {
                issues.Add(OpenVisionLab.OpenVisionLanguageService.T("WpfBatch.Finding.NoImages"));
            }

            if (missingImageCount > 0)
            {
                issues.Add(string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfBatch.Finding.MissingImages"), missingImageCount));
            }

            if (runnable.Count == 0 && requested.Count > 0)
            {
                issues.Add(OpenVisionLab.OpenVisionLanguageService.T("WpfBatch.Finding.NoRunnable"));
            }

            PythonModelSettings snapshot = CopySettings(settings);
            PythonModelValidationResult validation = PythonModelSettingsValidator.Validate(snapshot, requireWeights: true);
            issues.AddRange(validation.Errors);
            warnings.AddRange(validation.Warnings);
            if (string.IsNullOrWhiteSpace(snapshot.WeightsPath))
            {
                issues.Add(OpenVisionLab.OpenVisionLanguageService.T("WpfBatch.Finding.MissingWeights"));
            }

            IReadOnlyList<WpfBatchClassMappingItem> mappings = BuildClassMappings(data, issues);
            if (mappings.Count > 0)
            {
                warnings.Add(
                    OpenVisionLab.OpenVisionLanguageService.T("WpfBatch.Finding.MappingWarning"));
            }

            if (request.ExistingLabelPolicy == WpfBatchExistingLabelPolicy.IncludeAndKeep
                && existingLabelCount > 0)
            {
                warnings.Add(string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfBatch.Finding.IncludeExisting"), existingLabelCount));
            }

            return new WpfBatchDetectionPreflightReport
            {
                RunnableItems = runnable,
                ClassMappings = mappings,
                Issues = issues,
                Warnings = warnings,
                RequestedCount = requested.Count,
                MissingImageCount = missingImageCount,
                ExistingLabelCount = existingLabelCount,
                SkippedExistingLabelCount = skippedExistingLabelCount,
                ScopeText = request.ScopeText ?? string.Empty,
                DatasetPurposeText = FormatPurpose(data?.ProjectSettings?.DatasetPurpose
                    ?? LabelingDatasetPurpose.ObjectDetection),
                ModelEngineText = PythonModelSettings.NormalizeModelEngine(snapshot.ModelEngine),
                WeightsPath = snapshot.WeightsPath ?? string.Empty,
                ConfidenceText = snapshot.MinimumDetectionConfidence.ToString("P0"),
                ExistingLabelPolicyText = request.ExistingLabelPolicy == WpfBatchExistingLabelPolicy.SkipLabeled
                    ? "\uAE30\uC874 \uB77C\uBCA8 \uC774\uBBF8\uC9C0 \uC81C\uC678"
                    : "\uAE30\uC874 \uB77C\uBCA8 \uC774\uBBF8\uC9C0 \uD3EC\uD568 \u00B7 \uB77C\uBCA8 \uBCF4\uC874"
            };
        }

        private static IReadOnlyList<WpfBatchClassMappingItem> BuildClassMappings(
            LabelingProjectData data,
            ICollection<string> issues)
        {
            List<string> names = data?.ClassNamedList?
                .Select(item => item?.Text?.Trim() ?? string.Empty)
                .ToList() ?? new List<string>();
            if (names.Count == 0 || names.All(string.IsNullOrWhiteSpace))
            {
                issues.Add(OpenVisionLab.OpenVisionLanguageService.T("WpfBatch.Finding.NoClasses"));
                return Array.Empty<WpfBatchClassMappingItem>();
            }

            if (names.Any(string.IsNullOrWhiteSpace))
            {
                issues.Add(OpenVisionLab.OpenVisionLanguageService.T("WpfBatch.Finding.EmptyClass"));
            }

            if (names.Where(name => !string.IsNullOrWhiteSpace(name))
                .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Any(group => group.Count() > 1))
            {
                issues.Add(OpenVisionLab.OpenVisionLanguageService.T("WpfBatch.Finding.DuplicateClass"));
            }

            return names
                .Select((name, index) => new WpfBatchClassMappingItem(index, name))
                .ToList();
        }

        private static PythonModelSettings CopySettings(PythonModelSettings source)
        {
            source ??= new PythonModelSettings();
            return new PythonModelSettings
            {
                PythonExecutablePath = source.PythonExecutablePath,
                ModelEngine = source.ModelEngine,
                ProjectRootPath = source.ProjectRootPath,
                ClientScriptPath = source.ClientScriptPath,
                WeightsPath = source.WeightsPath,
                ImageRootPath = source.ImageRootPath,
                MinimumDetectionConfidence = source.MinimumDetectionConfidence,
                MaximumDetectionCandidates = source.MaximumDetectionCandidates,
                InferenceImageSize = source.InferenceImageSize,
                DetectionTimeoutSeconds = source.DetectionTimeoutSeconds,
                AutoStartClient = source.AutoStartClient
            };
        }

        private static string FormatPurpose(LabelingDatasetPurpose purpose)
            => purpose switch
            {
                LabelingDatasetPurpose.Segmentation => OpenVisionLab.OpenVisionLanguageService.T("WpfShell.Dataset.Purpose.Segmentation"),
                LabelingDatasetPurpose.AnomalyDetection => OpenVisionLab.OpenVisionLanguageService.T("WpfShell.Dataset.Purpose.AnomalyDetection"),
                _ => OpenVisionLab.OpenVisionLanguageService.T("WpfShell.Dataset.Purpose.ObjectDetection")
            };
    }

    [Obsolete("Use BatchDetectionPreflightService.", false)]
    public sealed class WpfBatchDetectionPreflightService : BatchDetectionPreflightService
    {
        public WpfBatchDetectionPreflightService(DetectionTargetService targetService = null)
            : base(targetService)
        {
        }
    }
}
