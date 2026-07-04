using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using System;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Settings panel refresh can run package checks, so it remains separate from cheap status-label updates.
        private async Task RefreshYoloSettingsPanelAsync(PythonModelValidationResult validation = null)
        {
            global.Data.ProjectSettings ??= new LabelingProjectSettings();
            global.Data.ProjectSettings.PythonModel ??= new PythonModelSettings();
            PythonModelSettings settings = global.Data.ProjectSettings.PythonModel;
            PythonCommunicationStatus communicationStatus = global.GetPythonCommunicationStatusSnapshot();
            PythonModelRuntimeState runtimeState = PythonModelSettingsValidator.GetRuntimeState(
                settings,
                communicationStatus?.WorkerSupportedModels,
                communicationStatus?.WorkerTrainingModels,
                communicationStatus?.WorkerDetectionModels);
            validation ??= runtimeState.State == PythonModelRuntimeStateKind.NotInstalled
                ? new PythonModelValidationResult(new[] { runtimeState.NextActionText }, Array.Empty<string>())
                : PythonModelSettingsValidator.Validate(settings, requireWeights: true);

            YoloModelSettingsViewModel?.ApplyRuntimeCapabilities(
                communicationStatus?.WorkerSupportedModels,
                communicationStatus?.WorkerTrainingModels,
                communicationStatus?.WorkerDetectionModels);

            PythonEnvironmentCheckResult environment = null;
            string environmentCheckError = string.Empty;
            if (runtimeState.IsRuntimeInstalled)
            {
                try
                {
                    environment = await PythonEnvironmentService
                        .CheckRequirementsAsync(settings)
                        .ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    environmentCheckError = ex.Message;
                }
            }

            string detail = WpfYoloSettingsPanelStatusPresentationService.BuildDetail(
                settings,
                validation,
                runtimeState,
                communicationStatus,
                global.PythonClientProcess?.IsRunning == true,
                environment,
                environmentCheckError);

            if (YoloStatusViewModel != null)
            {
                YoloStatusViewModel.SetSettingsStatus(runtimeState.SummaryText, detail);
                return;
            }

            YoloSettingsSummaryText.Text = runtimeState.SummaryText;
            YoloSettingsDetailText.Text = detail;
        }
    }
}
