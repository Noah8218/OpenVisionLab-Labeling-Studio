using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;

namespace MvcVisionSystem
{
    public sealed class TrainingReadinessWorkflowRequest
    {
        public LabelingProjectData Data { get; init; }

        public bool RefreshYaml { get; init; }
    }

    public sealed class TrainingReadinessWorkflowResult
    {
        public TrainingReadinessWorkflowResult(
            YoloDatasetReadinessReport datasetReport,
            ExternalYoloDatasetSettings externalSettings,
            YoloExternalDatasetIntakeReport externalReport,
            bool usesExternalDataset)
        {
            DatasetReport = datasetReport;
            ExternalSettings = externalSettings;
            ExternalReport = externalReport;
            UsesExternalDataset = usesExternalDataset;
        }

        public YoloDatasetReadinessReport DatasetReport { get; }

        public ExternalYoloDatasetSettings ExternalSettings { get; }

        public YoloExternalDatasetIntakeReport ExternalReport { get; }

        public bool UsesExternalDataset { get; }
    }

    /// <summary>
    /// Owns the training-readiness scan and external-source identity policy.
    /// The Shell only projects the returned report into WPF presentation and
    /// keeps the explicit refresh command boundary.
    /// </summary>
    public sealed class TrainingReadinessWorkflowService
    {
        public TrainingReadinessWorkflowResult Refresh(TrainingReadinessWorkflowRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            LabelingProjectData data = request.Data
                ?? throw new ArgumentNullException(nameof(request.Data));

            EnsureProjectSettings(data);
            ExternalYoloDatasetSettings externalSettings = data.ProjectSettings.ExternalYoloDataset;
            if (externalSettings?.UseForTraining == true)
            {
                return BuildExternalResult(externalSettings, request.RefreshYaml);
            }

            return new TrainingReadinessWorkflowResult(
                YoloDatasetReadinessService.Build(data, request.RefreshYaml),
                externalSettings,
                externalReport: null,
                usesExternalDataset: false);
        }

        private static TrainingReadinessWorkflowResult BuildExternalResult(
            ExternalYoloDatasetSettings settings,
            bool refreshYaml)
        {
            YoloExternalDatasetIntakeReport report = null;
            if (refreshYaml)
            {
                report = YoloExternalDatasetIntakeService.Build(
                    settings.DataYamlFilePath,
                    settings.DatasetPurpose);
                if (report.IsReady
                    && !YoloExternalDatasetIntakeService.HasCurrentSourceIdentity(settings, report, out string identityError))
                {
                    YoloExternalDatasetIntakeService.ApplyValidation(settings, report);
                    YoloExternalDatasetIntakeService.MarkSourceIdentityRequiresReactivation(settings, identityError);
                }
                else
                {
                    YoloExternalDatasetIntakeService.ApplyValidation(settings, report);
                }
            }

            return new TrainingReadinessWorkflowResult(
                datasetReport: null,
                settings,
                report,
                usesExternalDataset: true);
        }

        private static void EnsureProjectSettings(LabelingProjectData data)
        {
            data.ProjectSettings ??= new LabelingProjectSettings();
            PythonModelRuntimePathResolver.ApplyDefaults(data.ProjectSettings);
        }
    }
}
