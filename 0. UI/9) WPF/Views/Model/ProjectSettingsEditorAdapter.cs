using System;
using System.Windows.Controls;
using MvcVisionSystem._1._Core;

namespace MvcVisionSystem
{
    // Owns the synchronization between persisted project settings and the
    // model/training editor ViewModels. The Shell supplies WPF-only callbacks.
    internal sealed class ProjectSettingsEditorAdapter
    {
        private readonly ProjectSettingsEditorAdapterContext context;

        private LabelingProjectData projectData => context.DataProvider?.Invoke();

        internal ProjectSettingsEditorAdapter(ProjectSettingsEditorAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            ArgumentNullException.ThrowIfNull(context.DataProvider);
        }

        internal void PopulateYoloEditorFields()
        {
            context.EnsureProjectSettings?.Invoke();
            context.YoloModelSettingsViewModel?.LoadFrom(
                projectData.ProjectSettings.PythonModel,
                projectData.ProjectSettings.AnomalyClassification);
        }

        internal void PopulateTrainingEditorFields()
        {
            context.EnsureProjectSettings?.Invoke();
            context.TrainingSettingsViewModel?.LoadFrom(
                projectData.GetTrainingSettings(),
                projectData.ProjectSettings.YoloDataset,
                projectData.ProjectSettings.PythonModel,
                projectData.ProjectSettings.DatasetPurpose,
                projectData.ProjectSettings.ExternalYoloDataset);
            context.RefreshExternalYoloDatasetIntakePresentation?.Invoke();
            context.TrainingSettingsViewModel?.RefreshSegmentationAdapterComparisonContext();
        }

        internal void SaveYoloEditorFields()
        {
            context.EnsureProjectSettings?.Invoke();
            PythonModelSettings settings = projectData.ProjectSettings.PythonModel;
            context.YoloModelSettingsViewModel?.ApplyTo(settings);
            context.YoloModelSettingsViewModel?.ApplyTo(
                projectData.ProjectSettings.AnomalyClassification);
            if (context.CandidateConfidenceSlider != null)
            {
                context.CandidateConfidenceSlider.Value = Math.Clamp(settings.MinimumDetectionConfidence, 0F, 1F);
            }
        }

        internal void SaveTrainingEditorFields()
        {
            context.EnsureProjectSettings?.Invoke();
            TrainingSettings training = projectData.ProjectSettings.Training;
            context.TrainingSettingsViewModel?.ApplyTo(
                training,
                projectData.ProjectSettings.YoloDataset,
                projectData.TrainingParam);
        }

        internal void RefreshCandidateConfidenceFilterFromAppliedSettings()
        {
            if (context.CandidateConfidenceSlider == null)
            {
                return;
            }

            context.CandidateConfidenceSlider.Value = Math.Clamp(
                projectData.ProjectSettings.PythonModel.MinimumDetectionConfidence,
                0F,
                1F);
            context.UpdateCandidateConfidenceText?.Invoke();
        }
    }

    internal sealed class ProjectSettingsEditorAdapterContext
    {
        internal Func<LabelingProjectData> DataProvider { get; init; }
        internal WpfYoloModelSettingsPanelViewModel YoloModelSettingsViewModel { get; init; }
        internal WpfTrainingSettingsPanelViewModel TrainingSettingsViewModel { get; init; }
        internal Slider CandidateConfidenceSlider { get; init; }
        internal Action EnsureProjectSettings { get; init; }
        internal Action RefreshExternalYoloDatasetIntakePresentation { get; init; }
        internal Action UpdateCandidateConfidenceText { get; init; }
    }
}
