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
            $"className \"{RecipeClassName}\" \u2192 Recipe \"{RecipeClassName}\"";
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
            "\uACB0\uACFC\uB294 Candidate Review \uB300\uAE30 \uD6C4\uBCF4\uB85C\uB9CC \uC804\uB2EC\uB429\uB2C8\uB2E4. \uC790\uB3D9 \uC2B9\uC778\u00B7\uC790\uB3D9 \uC800\uC7A5\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4.";

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
                issues.Add("\uC0AC\uC804\uAC80\uC0AC\uD560 \uC774\uBBF8\uC9C0\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.");
            }

            if (missingImageCount > 0)
            {
                issues.Add($"\uD30C\uC77C\uC744 \uC5F4 \uC218 \uC5C6\uB294 \uC774\uBBF8\uC9C0 {missingImageCount}\uAC1C\uAC00 \uD3EC\uD568\uB418\uC5C8\uC2B5\uB2C8\uB2E4.");
            }

            if (runnable.Count == 0 && requested.Count > 0)
            {
                issues.Add("\uD604\uC7AC \uAE30\uC874 \uB77C\uBCA8 \uCC98\uB9AC \uC815\uCC45\uC73C\uB85C \uC2E4\uD589\uD560 \uC774\uBBF8\uC9C0\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.");
            }

            PythonModelSettings snapshot = CopySettings(settings);
            PythonModelValidationResult validation = PythonModelSettingsValidator.Validate(snapshot, requireWeights: true);
            issues.AddRange(validation.Errors);
            warnings.AddRange(validation.Warnings);
            if (string.IsNullOrWhiteSpace(snapshot.WeightsPath))
            {
                issues.Add("\uAC80\uC0AC \uAC00\uC911\uCE58 \uD30C\uC77C\uC774 \uC120\uD0DD\uB418\uC9C0 \uC54A\uC558\uC2B5\uB2C8\uB2E4.");
            }

            IReadOnlyList<WpfBatchClassMappingItem> mappings = BuildClassMappings(data, issues);
            if (mappings.Count > 0)
            {
                warnings.Add(
                    "\uAC00\uC911\uCE58 \uD074\uB798\uC2A4 \uBAA9\uB85D\uC740 \uC0AC\uC804 \uCD94\uCD9C\uD558\uC9C0 \uC54A\uC2B5\uB2C8\uB2E4. "
                    + "worker className\uC744 Recipe\uC758 \uB3D9\uC77C \uC774\uB984\uC5D0 \uB9E4\uD551\uD558\uBA70 \uBAA8\uB4E0 \uACB0\uACFC\uB294 Candidate Review\uC5D0\uC11C \uD655\uC778\uD569\uB2C8\uB2E4.");
            }

            if (request.ExistingLabelPolicy == WpfBatchExistingLabelPolicy.IncludeAndKeep
                && existingLabelCount > 0)
            {
                warnings.Add($"\uAE30\uC874 \uB77C\uBCA8\uC774 \uC788\uB294 {existingLabelCount}\uAC1C \uC774\uBBF8\uC9C0\uB3C4 \uAC80\uC0AC\uD569\uB2C8\uB2E4. \uAE30\uC874 \uB77C\uBCA8\uC740 \uBCF4\uC874\uB429\uB2C8\uB2E4.");
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
                issues.Add("\uD604\uC7AC Recipe\uC5D0 \uB4F1\uB85D\uB41C \uD074\uB798\uC2A4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.");
                return Array.Empty<WpfBatchClassMappingItem>();
            }

            if (names.Any(string.IsNullOrWhiteSpace))
            {
                issues.Add("\uC774\uB984\uC774 \uBE48 \uD074\uB798\uC2A4\uAC00 \uC788\uC2B5\uB2C8\uB2E4.");
            }

            if (names.Where(name => !string.IsNullOrWhiteSpace(name))
                .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Any(group => group.Count() > 1))
            {
                issues.Add("\uB300\uC18C\uBB38\uC790\uB9CC \uB2E4\uB978 \uC911\uBCF5 \uD074\uB798\uC2A4 \uC774\uB984\uC774 \uC788\uC2B5\uB2C8\uB2E4.");
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
                LabelingDatasetPurpose.Segmentation => "\uC138\uADF8\uBA58\uD14C\uC774\uC158",
                LabelingDatasetPurpose.AnomalyDetection => "\uC774\uC0C1 \uAC80\uCD9C",
                _ => "\uAC1D\uCCB4 \uAC80\uCD9C"
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
