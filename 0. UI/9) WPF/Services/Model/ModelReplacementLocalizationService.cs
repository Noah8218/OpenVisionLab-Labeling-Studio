using MvcVisionSystem.Yolo;
using OpenVisionLab;
using System;
using System.Globalization;
using System.Linq;

namespace MvcVisionSystem
{
    /// <summary>
    /// Keeps Model Replacement status/detail as catalog descriptors so the same
    /// held-out evidence can be rendered again after a language change.
    /// </summary>
    public sealed class ModelReplacementLocalizationSnapshot
    {
        private readonly ModelReplacementTextDescriptor statusText;
        private readonly ModelReplacementTextDescriptor detailText;

        internal ModelReplacementLocalizationSnapshot(
            ModelReplacementTextDescriptor statusText,
            ModelReplacementTextDescriptor detailText)
        {
            this.statusText = statusText ?? throw new ArgumentNullException(nameof(statusText));
            this.detailText = detailText ?? throw new ArgumentNullException(nameof(detailText));
        }

        public string StatusText => statusText.Render();

        public string DetailText => detailText.Render();
    }

    internal sealed class ModelReplacementTextDescriptor
    {
        private readonly string key;
        private readonly object[] arguments;

        internal ModelReplacementTextDescriptor(string key, params object[] arguments)
        {
            this.key = key ?? string.Empty;
            this.arguments = arguments ?? Array.Empty<object>();
        }

        internal string Render()
        {
            object[] renderedArguments = arguments
                .Select(RenderArgument)
                .ToArray();
            return string.Format(
                CultureInfo.InvariantCulture,
                OpenVisionLanguageService.T(key),
                renderedArguments);
        }

        private static object RenderArgument(object argument)
        {
            return argument is ModelReplacementTextDescriptor descriptor
                ? descriptor.Render()
                : argument ?? string.Empty;
        }
    }

    public static class ModelReplacementLocalizationService
    {
        private const int RecommendedTestImageCount = 10;

        public static ModelReplacementLocalizationSnapshot CreateInitial()
        {
            return new ModelReplacementLocalizationSnapshot(
                Text("WpfLearningWorkflow.ModelReplacement.Status.Initial"),
                Text("WpfLearningWorkflow.ModelReplacement.Detail.Initial"));
        }

        public static ModelReplacementLocalizationSnapshot Build(
            YoloDatasetReadinessReport report,
            YoloDatasetStatistics statistics)
        {
            statistics ??= report?.Statistics ?? new YoloDatasetStatistics();
            if (report?.IsReady != true)
            {
                return new ModelReplacementLocalizationSnapshot(
                    Text("WpfLearningWorkflow.ModelReplacement.Status.Unavailable"),
                    Text("WpfLearningWorkflow.ModelReplacement.Detail.Unavailable"));
            }

            int testImageCount = statistics.TestImageCount;
            int testLabelCount = statistics.TestLabelCount;
            int finalVerificationCount = Math.Min(testImageCount, testLabelCount);
            ModelReplacementTextDescriptor status = testImageCount > 0 && testLabelCount > 0
                ? Text(
                    finalVerificationCount >= RecommendedTestImageCount
                        ? "WpfLearningWorkflow.ModelReplacement.Status.Available"
                        : "WpfLearningWorkflow.ModelReplacement.Status.EvidenceInsufficient")
                : Text("WpfLearningWorkflow.ModelReplacement.Status.Hold");

            ModelReplacementTextDescriptor detail;
            if (testImageCount > 0 && testLabelCount <= 0)
            {
                detail = Text("WpfLearningWorkflow.ModelReplacement.Detail.NoLabels");
            }
            else if (finalVerificationCount > 0 && finalVerificationCount < RecommendedTestImageCount)
            {
                detail = Text(
                    "WpfLearningWorkflow.ModelReplacement.Detail.WeakEvidence",
                    finalVerificationCount,
                    RecommendedTestImageCount,
                    RecommendedTestImageCount - finalVerificationCount);
            }
            else if (finalVerificationCount >= RecommendedTestImageCount)
            {
                detail = Text(
                    "WpfLearningWorkflow.ModelReplacement.Detail.Available",
                    finalVerificationCount);
            }
            else
            {
                detail = Text("WpfLearningWorkflow.ModelReplacement.Detail.NoTest");
            }

            return new ModelReplacementLocalizationSnapshot(status, detail);
        }

        private static ModelReplacementTextDescriptor Text(string key, params object[] arguments)
        {
            return new ModelReplacementTextDescriptor(key, arguments);
        }
    }

    [Obsolete("Use ModelReplacementLocalizationSnapshot.", false)]
    public sealed class WpfModelReplacementLocalizationSnapshot
    {
        private readonly ModelReplacementLocalizationSnapshot inner;

        private WpfModelReplacementLocalizationSnapshot(ModelReplacementLocalizationSnapshot source)
        {
            inner = source ?? throw new ArgumentNullException(nameof(source));
        }

        internal static WpfModelReplacementLocalizationSnapshot FromCanonical(ModelReplacementLocalizationSnapshot source)
            => source == null ? null : new WpfModelReplacementLocalizationSnapshot(source);

        public string StatusText => inner.StatusText;

        public string DetailText => inner.DetailText;
    }

    [Obsolete("Use ModelReplacementLocalizationService.", false)]
    public static class WpfModelReplacementLocalizationService
    {
        public static WpfModelReplacementLocalizationSnapshot CreateInitial()
            => WpfModelReplacementLocalizationSnapshot.FromCanonical(ModelReplacementLocalizationService.CreateInitial());

        public static WpfModelReplacementLocalizationSnapshot Build(
            YoloDatasetReadinessReport report,
            YoloDatasetStatistics statistics)
            => WpfModelReplacementLocalizationSnapshot.FromCanonical(ModelReplacementLocalizationService.Build(report, statistics));
    }
}
