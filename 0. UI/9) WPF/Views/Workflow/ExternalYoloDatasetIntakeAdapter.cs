using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.IO;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the WPF callback boundary for external YOLO dataset intake.
    /// The learning workflow ViewModel owns command admission and async
    /// execution; this adapter only reads project settings, applies the
    /// validated result, persists recipe metadata, and forwards presentation.
    /// </summary>
    internal sealed class ExternalYoloDatasetIntakeAdapter
    {
        private readonly ExternalYoloDatasetIntakeAdapterContext context;

        internal ExternalYoloDatasetIntakeAdapter(ExternalYoloDatasetIntakeAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            if (context.DataProvider == null)
            {
                throw new ArgumentNullException(nameof(context.DataProvider));
            }

            if (context.ProjectRecipeSessionService == null)
            {
                throw new ArgumentNullException(nameof(context.ProjectRecipeSessionService));
            }

            if (context.CurrentRecipeNameProvider == null)
            {
                throw new ArgumentNullException(nameof(context.CurrentRecipeNameProvider));
            }

            if (context.SettingsProvider == null)
            {
                throw new ArgumentNullException(nameof(context.SettingsProvider));
            }

            if (context.SelectedPurposeProvider == null)
            {
                throw new ArgumentNullException(nameof(context.SelectedPurposeProvider));
            }

            if (context.SelectDataYamlPath == null)
            {
                throw new ArgumentNullException(nameof(context.SelectDataYamlPath));
            }

            if (context.IsApplicationCloseApproved == null)
            {
                throw new ArgumentNullException(nameof(context.IsApplicationCloseApproved));
            }

            if (context.SetPresentation == null)
            {
                throw new ArgumentNullException(nameof(context.SetPresentation));
            }

            if (context.PopulateTrainingEditorFields == null)
            {
                throw new ArgumentNullException(nameof(context.PopulateTrainingEditorFields));
            }

            if (context.RefreshTrainingReadinessPanel == null)
            {
                throw new ArgumentNullException(nameof(context.RefreshTrainingReadinessPanel));
            }

            if (context.RefreshExternalTrainingReadinessPanel == null)
            {
                throw new ArgumentNullException(nameof(context.RefreshExternalTrainingReadinessPanel));
            }

            if (context.AppendLog == null)
            {
                throw new ArgumentNullException(nameof(context.AppendLog));
            }
        }

        #region ExternalYoloDatasetIntake
        internal ExternalYoloDatasetIntakeCallbacks CreateExternalYoloDatasetIntakeCallbacks()
        {
            return new ExternalYoloDatasetIntakeCallbacks
            {
                SettingsProvider = GetExternalYoloDatasetSettings,
                SelectedPurposeProvider = context.SelectedPurposeProvider,
                SelectDataYamlPath = context.SelectDataYamlPath,
                IsApplicationCloseApproved = context.IsApplicationCloseApproved,
                ClearSelection = ClearExternalYoloDatasetSelection,
                ApplyValidationResult = ApplyExternalYoloDatasetValidation,
                AppendLog = context.AppendLog
            };
        }

        internal void ClearExternalYoloDatasetSelection()
        {
            ExternalYoloDatasetSettings settings = GetExternalYoloDatasetSettings();
            if (settings == null)
            {
                return;
            }

            settings.Clear();
            TrySaveExternalYoloDatasetSettings();
            RefreshExternalYoloDatasetIntakePresentation();
            context.PopulateTrainingEditorFields();
            context.RefreshTrainingReadinessPanel(false);
        }

        internal bool ApplyExternalYoloDatasetValidation(
            YoloExternalDatasetIntakeReport report,
            string dataYamlFilePath,
            LabelingDatasetPurpose purpose,
            bool useForNextTraining)
        {
            ExternalYoloDatasetSettings settings = GetExternalYoloDatasetSettings();
            if (settings == null || report == null)
            {
                return false;
            }

            settings.DataYamlFilePath = string.IsNullOrWhiteSpace(report.DataYamlFilePath)
                ? dataYamlFilePath ?? string.Empty
                : report.DataYamlFilePath;
            settings.DatasetPurpose = purpose;
            settings.UseForTraining = useForNextTraining && report.IsReady;
            YoloExternalDatasetIntakeService.ApplyValidation(settings, report, acceptSourceIdentity: useForNextTraining);
            TrySaveExternalYoloDatasetSettings();
            context.PopulateTrainingEditorFields();
            if (settings.UseForTraining)
            {
                context.RefreshExternalTrainingReadinessPanel(report);
            }
            else
            {
                RefreshExternalYoloDatasetIntakePresentation();
                context.RefreshTrainingReadinessPanel(false);
            }

            return settings.UseForTraining;
        }

        internal ExternalYoloDatasetSettings GetExternalYoloDatasetSettings()
        {
            LabelingProjectData data = context.DataProvider();
            if (data == null)
            {
                return null;
            }

            data.ProjectSettings ??= new LabelingProjectSettings();
            return data.ProjectSettings.ExternalYoloDataset;
        }

        internal void RefreshExternalYoloDatasetIntakePresentation()
        {
            ExternalYoloDatasetSettings settings = GetExternalYoloDatasetSettings();
            if (settings == null)
            {
                return;
            }

            (string statusText, string detailText) = TrainingReadinessPresentationService.BuildExternalDatasetPresentation(settings);
            context.SetPresentation(
                settings.DatasetPurpose,
                statusText,
                detailText,
                settings.DataYamlFilePath);
        }

        internal bool TrySaveExternalYoloDatasetSettings()
        {
            string recipeName = context.CurrentRecipeNameProvider();
            LabelingProjectData data = context.DataProvider();
            if (data == null || string.IsNullOrWhiteSpace(recipeName))
            {
                return false;
            }

            try
            {
                RecipeConfigurationSaveResult saveResult = context.ProjectRecipeSessionService.SaveConfiguration(
                    data,
                    recipeName,
                    updateYoloDataYaml: false,
                    refreshDatasetVersion: true);
                if (!saveResult.IsSuccess)
                {
                    throw new IOException(saveResult.ErrorMessage);
                }

                return true;
            }
            catch (Exception ex)
            {
                context.AppendLog($"외부 YOLO data.yaml 설정 저장 실패: {ex.Message}");
                return false;
            }
        }
        #endregion
    }

    internal sealed class ExternalYoloDatasetIntakeAdapterContext
    {
        internal Func<LabelingProjectData> DataProvider { get; init; }
        internal ProjectRecipeSessionService ProjectRecipeSessionService { get; init; }
        internal Func<string> CurrentRecipeNameProvider { get; init; }
        internal Func<ExternalYoloDatasetSettings> SettingsProvider { get; init; }
        internal Func<LabelingDatasetPurpose> SelectedPurposeProvider { get; init; }
        internal Func<string, string> SelectDataYamlPath { get; init; }
        internal Func<bool> IsApplicationCloseApproved { get; init; }
        internal Action<LabelingDatasetPurpose, string, string, string> SetPresentation { get; init; }
        internal Action PopulateTrainingEditorFields { get; init; }
        internal Action<bool> RefreshTrainingReadinessPanel { get; init; }
        internal Action<YoloExternalDatasetIntakeReport> RefreshExternalTrainingReadinessPanel { get; init; }
        internal Action<string> AppendLog { get; init; }
    }
}
