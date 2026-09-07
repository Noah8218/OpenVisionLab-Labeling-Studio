using MvcVisionSystem.Yolo;
using OpenVisionLab;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MvcVisionSystem
{
    /// <summary>
    /// Keeps the visible Training Checklist text as catalog descriptors so the
    /// same readiness result can be rendered again after a language change.
    /// </summary>
    public sealed class TrainingChecklistLocalizationSnapshot
    {
        private readonly TrainingChecklistTextDescriptor statusText;
        private readonly TrainingChecklistTextDescriptor detailText;

        internal TrainingChecklistLocalizationSnapshot(
            TrainingChecklistTextDescriptor statusText,
            TrainingChecklistTextDescriptor detailText)
        {
            this.statusText = statusText ?? throw new ArgumentNullException(nameof(statusText));
            this.detailText = detailText ?? throw new ArgumentNullException(nameof(detailText));
        }

        public string StatusText => statusText.Render();

        public string DetailText => detailText.Render();
    }

    /// <summary>
    /// Keeps Training Checklist action text as catalog descriptors so the same
    /// readiness result can be rendered again after a language change.
    /// </summary>
    public sealed class TrainingChecklistActionLocalizationSnapshot
    {
        private readonly IReadOnlyList<TrainingChecklistTextDescriptor> actionTexts;

        internal TrainingChecklistActionLocalizationSnapshot(
            IEnumerable<TrainingChecklistTextDescriptor> actionTexts)
        {
            this.actionTexts = (actionTexts ?? Enumerable.Empty<TrainingChecklistTextDescriptor>())
                .Where(item => item != null)
                .ToList();
        }

        public string ActionText => string.Join(
            " / ",
            actionTexts
                .Select(item => item.Render())
                .Where(item => !string.IsNullOrWhiteSpace(item)));
    }

    internal sealed class TrainingChecklistTextDescriptor
    {
        private readonly string key;
        private readonly object[] arguments;

        internal TrainingChecklistTextDescriptor(string key, params object[] arguments)
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
            return argument switch
            {
                TrainingChecklistTextDescriptor descriptor => descriptor.Render(),
                TrainingChecklistLocalizedArgument localizedArgument => localizedArgument.Render(),
                _ => argument ?? string.Empty
            };
        }
    }

    internal sealed class TrainingChecklistLocalizedArgument
    {
        private readonly Func<string> render;

        internal TrainingChecklistLocalizedArgument(Func<string> render)
        {
            this.render = render ?? throw new ArgumentNullException(nameof(render));
        }

        internal string Render() => render() ?? string.Empty;
    }

    public static class TrainingChecklistLocalizationService
    {
        public static TrainingChecklistLocalizationSnapshot CreateInitial()
        {
            return new TrainingChecklistLocalizationSnapshot(
                Text("WpfLearningWorkflow.TrainingChecklist.Status.Initial"),
                Text("WpfLearningWorkflow.TrainingChecklist.Detail.Initial"));
        }

        public static TrainingChecklistActionLocalizationSnapshot CreateInitialAction()
        {
            return new TrainingChecklistActionLocalizationSnapshot(
                new[]
                {
                    Text("WpfLearningWorkflow.TrainingChecklist.Action.Initial")
                });
        }

        public static TrainingChecklistActionLocalizationSnapshot BuildReadyAction(
            LabelingDatasetPurpose purpose)
        {
            return new TrainingChecklistActionLocalizationSnapshot(
                new[]
                {
                    Text(purpose switch
                    {
                        LabelingDatasetPurpose.Segmentation => "WpfLearningWorkflow.DatasetDashboard.Action.Ready.Segmentation",
                        LabelingDatasetPurpose.AnomalyDetection => "WpfLearningWorkflow.DatasetDashboard.Action.Ready.Anomaly",
                        _ => "WpfLearningWorkflow.DatasetDashboard.Action.Ready.ObjectDetection"
                    })
                });
        }

        public static TrainingChecklistActionLocalizationSnapshot BuildQualityWarningAction(
            IReadOnlyList<string> warnings)
        {
            if (warnings == null || warnings.Count == 0)
            {
                return BuildReadyAction(LabelingDatasetPurpose.ObjectDetection);
            }

            return new TrainingChecklistActionLocalizationSnapshot(
                warnings
                    .Take(2)
                    .Select(BuildQualityWarningActionText));
        }

        public static TrainingChecklistActionLocalizationSnapshot BuildFailureAction(string issueKind)
        {
            return new TrainingChecklistActionLocalizationSnapshot(
                new[]
                {
                    Text(GetFailureActionKey(issueKind))
                });
        }

        internal static string FormatQualityWarning(string warning)
        {
            string normalized = warning?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return OpenVisionLanguageService.T("WpfLearningWorkflow.DatasetDashboard.Action.Warning.Unknown");
            }

            if (normalized.Contains("Test split is empty", StringComparison.OrdinalIgnoreCase))
            {
                return OpenVisionLanguageService.T("WpfLearningWorkflow.DatasetDashboard.Action.Warning.TestSplitEmpty");
            }

            if (normalized.Contains("YOLO split guide", StringComparison.OrdinalIgnoreCase))
            {
                return OpenVisionLanguageService.T("WpfLearningWorkflow.DatasetDashboard.Action.Warning.SplitGuide");
            }

            if (OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.English)
            {
                return normalized;
            }

            return normalized
                .Replace("train/valid/test", "학습/검증/최종 검증", StringComparison.OrdinalIgnoreCase)
                .Replace("training", "학습", StringComparison.OrdinalIgnoreCase)
                .Replace("validation", "검증", StringComparison.OrdinalIgnoreCase)
                .Replace("test split", "최종 검증", StringComparison.OrdinalIgnoreCase)
                .Replace("train", "학습", StringComparison.OrdinalIgnoreCase)
                .Replace("valid", "검증", StringComparison.OrdinalIgnoreCase)
                .Replace("test", "최종", StringComparison.OrdinalIgnoreCase);
        }

        public static TrainingChecklistLocalizationSnapshot BuildReady(
            YoloDatasetStatistics statistics,
            int classCount,
            LabelingDatasetPurpose purpose,
            bool hasWarnings)
        {
            statistics ??= new YoloDatasetStatistics();
            TrainingChecklistTextDescriptor state = Text(
                hasWarnings
                    ? "WpfLearningWorkflow.TrainingChecklist.State.Warning"
                    : "WpfLearningWorkflow.TrainingChecklist.State.Ready");

            TrainingChecklistTextDescriptor status = purpose switch
            {
                LabelingDatasetPurpose.Segmentation => Text(
                    "WpfLearningWorkflow.TrainingChecklist.Status.Segmentation",
                    state,
                    statistics.TotalImageCount,
                    BuildSegmentationPrimaryLabel(statistics),
                    statistics.TotalObjectCount),
                LabelingDatasetPurpose.AnomalyDetection => Text(
                    "WpfLearningWorkflow.TrainingChecklist.Status.Anomaly",
                    state,
                    statistics.TotalImageCount,
                    statistics.AnomalyNormalImageCount,
                    statistics.AnomalyAbnormalImageCount,
                    statistics.AnomalyUnreviewedImageCount),
                _ => Text(
                    "WpfLearningWorkflow.TrainingChecklist.Status.ObjectDetection",
                    state,
                    statistics.TotalImageCount,
                    statistics.TotalObjectCount,
                    statistics.TotalSegmentationObjectCount)
            };

            TrainingChecklistTextDescriptor detail = purpose == LabelingDatasetPurpose.AnomalyDetection
                ? Text(
                    "WpfLearningWorkflow.TrainingChecklist.Detail.Ready.Anomaly",
                    BuildPurposeText(purpose),
                    statistics.TrainImageCount,
                    statistics.ValidImageCount,
                    statistics.TestImageCount,
                    statistics.AnomalyNormalImageCount,
                    statistics.AnomalyAbnormalImageCount,
                    statistics.AnomalyUnreviewedImageCount)
                : Text(
                    "WpfLearningWorkflow.TrainingChecklist.Detail.Ready.Standard",
                    BuildPurposeText(purpose),
                    statistics.TrainImageCount,
                    statistics.ValidImageCount,
                    statistics.TestImageCount,
                    statistics.TotalObjectCount,
                    statistics.TotalLabelFileCount,
                    statistics.TotalSegmentationObjectCount,
                    statistics.TotalSegmentFileCount,
                    statistics.TotalMaskFileCount,
                    classCount);

            return new TrainingChecklistLocalizationSnapshot(status, detail);
        }

        public static TrainingChecklistLocalizationSnapshot BuildFailure(
            string issueKind,
            string firstError,
            YoloDatasetStatistics statistics,
            LabelingDatasetPurpose purpose)
        {
            statistics ??= new YoloDatasetStatistics();
            object issueArgument = string.IsNullOrWhiteSpace(firstError)
                ? Text("WpfLearningWorkflow.TrainingChecklist.Issue.Unknown")
                : firstError.Trim();
            TrainingChecklistTextDescriptor status = Text(GetFailureStatusKey(issueKind));
            TrainingChecklistTextDescriptor detail = Text(
                "WpfLearningWorkflow.TrainingChecklist.Detail.Failure",
                BuildPurposeText(purpose),
                issueArgument,
                statistics.TrainImageCount,
                statistics.ValidImageCount,
                statistics.TestImageCount,
                statistics.TotalObjectCount,
                statistics.TotalSegmentationObjectCount,
                statistics.TotalMaskFileCount);
            return new TrainingChecklistLocalizationSnapshot(status, detail);
        }

        private static TrainingChecklistTextDescriptor BuildSegmentationPrimaryLabel(YoloDatasetStatistics statistics)
        {
            if (statistics.TotalSegmentationObjectCount > 0)
            {
                return Text(
                    "WpfLearningWorkflow.TrainingChecklist.Label.Segments",
                    statistics.TotalSegmentationObjectCount);
            }

            if (statistics.TotalMaskFileCount > 0)
            {
                return Text(
                    "WpfLearningWorkflow.TrainingChecklist.Label.Masks",
                    statistics.TotalMaskFileCount);
            }

            return Text("WpfLearningWorkflow.TrainingChecklist.Label.Segments", 0);
        }

        private static TrainingChecklistTextDescriptor BuildPurposeText(LabelingDatasetPurpose purpose)
        {
            return Text(purpose switch
            {
                LabelingDatasetPurpose.Segmentation => "WpfLearningWorkflow.TrainingChecklist.Purpose.Segmentation",
                LabelingDatasetPurpose.AnomalyDetection => "WpfLearningWorkflow.TrainingChecklist.Purpose.Anomaly",
                _ => "WpfLearningWorkflow.TrainingChecklist.Purpose.ObjectDetection"
            });
        }

        private static string GetFailureStatusKey(string issueKind)
        {
            return issueKind switch
            {
                "Classes" => "WpfLearningWorkflow.TrainingChecklist.Status.Failure.Classes",
                "Labels" => "WpfLearningWorkflow.TrainingChecklist.Status.Failure.Labels",
                "SegmentationPolicy" => "WpfLearningWorkflow.TrainingChecklist.Status.Failure.SegmentationPolicy",
                "SegmentationLabels" => "WpfLearningWorkflow.TrainingChecklist.Status.Failure.SegmentationLabels",
                "ValidImages" => "WpfLearningWorkflow.TrainingChecklist.Status.Failure.ValidImages",
                "Split" => "WpfLearningWorkflow.TrainingChecklist.Status.Failure.Split",
                "DataYaml" => "WpfLearningWorkflow.TrainingChecklist.Status.Failure.DataYaml",
                "LabelFormat" => "WpfLearningWorkflow.TrainingChecklist.Status.Failure.LabelFormat",
                "OutputRoot" => "WpfLearningWorkflow.TrainingChecklist.Status.Failure.OutputRoot",
                "Images" => "WpfLearningWorkflow.TrainingChecklist.Status.Failure.Images",
                _ => "WpfLearningWorkflow.TrainingChecklist.Status.Failure.Unknown"
            };
        }

        private static string GetFailureActionKey(string issueKind)
        {
            return issueKind switch
            {
                "Classes" => "WpfLearningWorkflow.DatasetDashboard.Action.Failure.Classes",
                "Labels" => "WpfLearningWorkflow.DatasetDashboard.Action.Failure.Labels",
                "SegmentationPolicy" => "WpfLearningWorkflow.DatasetDashboard.Action.Failure.SegmentationPolicy",
                "SegmentationLabels" => "WpfLearningWorkflow.DatasetDashboard.Action.Failure.SegmentationLabels",
                "ValidImages" => "WpfLearningWorkflow.DatasetDashboard.Action.Failure.ValidImages",
                "Split" => "WpfLearningWorkflow.DatasetDashboard.Action.Failure.Split",
                "DataYaml" => "WpfLearningWorkflow.DatasetDashboard.Action.Failure.DataYaml",
                "LabelFormat" => "WpfLearningWorkflow.DatasetDashboard.Action.Failure.LabelFormat",
                "OutputRoot" => "WpfLearningWorkflow.DatasetDashboard.Action.Failure.OutputRoot",
                "Images" => "WpfLearningWorkflow.DatasetDashboard.Action.Failure.Images",
                _ => "WpfLearningWorkflow.DatasetDashboard.Action.Failure.Unknown"
            };
        }

        private static TrainingChecklistTextDescriptor BuildQualityWarningActionText(string warning)
        {
            string normalized = warning?.Trim() ?? string.Empty;
            if (normalized.Contains("Test split is empty", StringComparison.OrdinalIgnoreCase))
            {
                return Text("WpfLearningWorkflow.DatasetDashboard.Action.Warning.TestSplitEmpty");
            }

            if (normalized.Contains("YOLO split guide", StringComparison.OrdinalIgnoreCase))
            {
                return Text("WpfLearningWorkflow.DatasetDashboard.Action.Warning.SplitGuide");
            }

            if (string.IsNullOrWhiteSpace(normalized))
            {
                return Text("WpfLearningWorkflow.DatasetDashboard.Action.Warning.Unknown");
            }

            return Text(
                "WpfLearningWorkflow.TrainingChecklist.Action.Warning.Raw",
                new TrainingChecklistLocalizedArgument(() => FormatQualityWarning(normalized)));
        }

        private static TrainingChecklistTextDescriptor Text(string key, params object[] arguments)
        {
            return new TrainingChecklistTextDescriptor(key, arguments);
        }
    }

    [Obsolete("Use TrainingChecklistLocalizationSnapshot.", false)]
    public sealed class WpfTrainingChecklistLocalizationSnapshot
    {
        private readonly TrainingChecklistLocalizationSnapshot inner;

        private WpfTrainingChecklistLocalizationSnapshot(TrainingChecklistLocalizationSnapshot source)
        {
            inner = source ?? throw new ArgumentNullException(nameof(source));
        }

        internal static WpfTrainingChecklistLocalizationSnapshot FromCanonical(TrainingChecklistLocalizationSnapshot source)
            => source == null ? null : new WpfTrainingChecklistLocalizationSnapshot(source);

        internal TrainingChecklistLocalizationSnapshot ToCanonical() => inner;

        public string StatusText => inner.StatusText;

        public string DetailText => inner.DetailText;
    }

    [Obsolete("Use TrainingChecklistActionLocalizationSnapshot.", false)]
    public sealed class WpfTrainingChecklistActionLocalizationSnapshot
    {
        private readonly TrainingChecklistActionLocalizationSnapshot inner;

        private WpfTrainingChecklistActionLocalizationSnapshot(TrainingChecklistActionLocalizationSnapshot source)
        {
            inner = source ?? throw new ArgumentNullException(nameof(source));
        }

        internal static WpfTrainingChecklistActionLocalizationSnapshot FromCanonical(TrainingChecklistActionLocalizationSnapshot source)
            => source == null ? null : new WpfTrainingChecklistActionLocalizationSnapshot(source);

        internal TrainingChecklistActionLocalizationSnapshot ToCanonical() => inner;

        public string ActionText => inner.ActionText;
    }

    [Obsolete("Use TrainingChecklistLocalizationService.", false)]
    public static class WpfTrainingChecklistLocalizationService
    {
        public static WpfTrainingChecklistLocalizationSnapshot CreateInitial()
            => WpfTrainingChecklistLocalizationSnapshot.FromCanonical(TrainingChecklistLocalizationService.CreateInitial());

        public static WpfTrainingChecklistActionLocalizationSnapshot CreateInitialAction()
            => WpfTrainingChecklistActionLocalizationSnapshot.FromCanonical(TrainingChecklistLocalizationService.CreateInitialAction());

        public static WpfTrainingChecklistActionLocalizationSnapshot BuildReadyAction(LabelingDatasetPurpose purpose)
            => WpfTrainingChecklistActionLocalizationSnapshot.FromCanonical(TrainingChecklistLocalizationService.BuildReadyAction(purpose));

        public static WpfTrainingChecklistActionLocalizationSnapshot BuildQualityWarningAction(IReadOnlyList<string> warnings)
            => WpfTrainingChecklistActionLocalizationSnapshot.FromCanonical(TrainingChecklistLocalizationService.BuildQualityWarningAction(warnings));

        public static WpfTrainingChecklistActionLocalizationSnapshot BuildFailureAction(string issueKind)
            => WpfTrainingChecklistActionLocalizationSnapshot.FromCanonical(TrainingChecklistLocalizationService.BuildFailureAction(issueKind));

        public static WpfTrainingChecklistLocalizationSnapshot BuildReady(
            YoloDatasetStatistics statistics,
            int classCount,
            LabelingDatasetPurpose purpose,
            bool hasWarnings)
            => WpfTrainingChecklistLocalizationSnapshot.FromCanonical(TrainingChecklistLocalizationService.BuildReady(statistics, classCount, purpose, hasWarnings));

        public static WpfTrainingChecklistLocalizationSnapshot BuildFailure(
            string issueKind,
            string firstError,
            YoloDatasetStatistics statistics,
            LabelingDatasetPurpose purpose)
            => WpfTrainingChecklistLocalizationSnapshot.FromCanonical(TrainingChecklistLocalizationService.BuildFailure(issueKind, firstError, statistics, purpose));
    }
}
