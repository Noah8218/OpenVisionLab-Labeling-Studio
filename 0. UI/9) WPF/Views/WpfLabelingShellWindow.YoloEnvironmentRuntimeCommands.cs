using MvcVisionSystem.Yolo;
using MvcVisionSystem._1._Core;
using OpenVisionLab.Wpf.MessageDialogs;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Runtime environment commands manage Python/worker state and should not mix with settings field browsing.
        private async void ExecuteCheckYoloCommand()
        {
            if (!BeginYoloEnvironmentCommand(WpfYoloEnvironmentCommandPresentationService.BuildEnvironmentCheckStartingStatus()))
            {
                return;
            }

            try
            {
                global.Data.ProjectSettings ??= new LabelingProjectSettings();
                global.Data.ProjectSettings.PythonModel ??= new PythonModelSettings();
                PythonModelRuntimeState runtimeState = GetPythonModelRuntimeState();
                PythonModelValidationResult result = runtimeState.State == PythonModelRuntimeStateKind.NotInstalled
                    ? new PythonModelValidationResult(new[] { runtimeState.NextActionText }, Array.Empty<string>())
                    : PythonModelSettingsValidator.Validate(global.Data.ProjectSettings.PythonModel, requireWeights: true);
                RefreshYoloStatus();
                ShowYoloModelCenterWorkflowView();
                await RefreshYoloSettingsPanelAsync(result).ConfigureAwait(true);

                if (result.IsValid)
                {
                    string readyStatus = WpfYoloEnvironmentCommandPresentationService.BuildEnvironmentReadyStatus();
                    SetYoloCommandStatus(readyStatus, isBusy: false);
                    AppendLog(readyStatus);
                    return;
                }

                SetYoloCommandStatus(WpfYoloEnvironmentCommandPresentationService.BuildEnvironmentNeedsAttentionStatus(), isBusy: false);
                AppendLog(WpfYoloEnvironmentCommandPresentationService.BuildEnvironmentNeedsAttentionLogHeader());
                foreach (string line in result.Errors.Concat(result.Warnings))
                {
                    AppendLog($"- {line}");
                }
            }
            catch (Exception ex)
            {
                string failureStatus = WpfYoloEnvironmentCommandPresentationService.BuildEnvironmentCheckFailureStatus(ex.Message);
                SetYoloCommandStatus(failureStatus, isBusy: false);
                AppendLog(failureStatus);
            }
            finally
            {
                EndYoloEnvironmentCommand();
            }
        }

        private async void ExecuteDetectCurrentImageCommand()
        {
            if (!EnsureModelRuntimeForInference())
            {
                return;
            }

            if (!EnsureInferenceModeForDetection())
            {
                return;
            }

            await RunInteractiveDetectionAsync(allowSmokeFallback: false).ConfigureAwait(true);
        }

        private async void ExecuteInstallRequirementsCommand()
        {
            if (!BeginYoloEnvironmentCommand(WpfYoloEnvironmentCommandPresentationService.BuildRequirementsCheckStartingStatus()))
            {
                return;
            }

            try
            {
                global.Data.ProjectSettings ??= new LabelingProjectSettings();
                global.Data.ProjectSettings.PythonModel ??= new PythonModelSettings();
                PythonModelSettings settings = global.Data.ProjectSettings.PythonModel;
                PythonModelRuntimeState runtimeState = GetPythonModelRuntimeState();
                if (!runtimeState.IsRuntimeInstalled)
                {
                    ShowModelRuntimeUnavailable(runtimeState.NextActionText, runtimeState);
                    return;
                }

                PythonEnvironmentCheckResult check = await PythonEnvironmentService
                    .CheckRequirementsAsync(settings)
                    .ConfigureAwait(true);

                if (check.Errors.Count > 0)
                {
                    SetYoloCommandStatus(WpfYoloEnvironmentCommandPresentationService.BuildRequirementsSkippedStatus(check.Summary), isBusy: false);
                    await RefreshYoloSettingsPanelAsync().ConfigureAwait(true);
                    AppendLog(WpfYoloEnvironmentCommandPresentationService.BuildRequirementsSkippedLog(check.Summary));
                    return;
                }

                if (check.MissingPackages.Count == 0)
                {
                    SetYoloCommandStatus(WpfYoloEnvironmentCommandPresentationService.BuildRequirementsReadyStatus(), isBusy: false);
                    await RefreshYoloSettingsPanelAsync().ConfigureAwait(true);
                    AppendLog(WpfYoloEnvironmentCommandPresentationService.BuildRequirementsNoMissingPackageLog());
                    return;
                }

                SetYoloCommandStatus(WpfYoloEnvironmentCommandPresentationService.BuildRequirementsInstallingStatus(check.MissingPackages.Count), isBusy: true);
                AppendLog(WpfYoloEnvironmentCommandPresentationService.BuildRequirementsInstallingLog(check.MissingPackages));
                PythonPackageInstallResult install = await PythonEnvironmentService
                    .InstallRequirementsAsync(settings)
                    .ConfigureAwait(true);

                await RefreshYoloSettingsPanelAsync().ConfigureAwait(true);
                SetYoloCommandStatus(WpfYoloEnvironmentCommandPresentationService.BuildRequirementsInstallResultStatus(install), isBusy: false);
                AppendLog(WpfYoloEnvironmentCommandPresentationService.BuildRequirementsInstallResultLog(install));
            }
            catch (Exception ex)
            {
                SetYoloCommandStatus(WpfYoloEnvironmentCommandPresentationService.BuildRequirementsInstallFailureStatus(ex.Message), isBusy: false);
                AppendLog(WpfYoloEnvironmentCommandPresentationService.BuildRequirementsInstallFailureLog(ex.Message));
            }
            finally
            {
                EndYoloEnvironmentCommand();
            }
        }

        private async void ExecuteInstallUltralyticsPackageCommand()
        {
            await ExecuteUltralyticsPackageCommandAsync(uninstall: false).ConfigureAwait(true);
        }

        private async void ExecuteUninstallUltralyticsPackageCommand()
        {
            await ExecuteUltralyticsPackageCommandAsync(uninstall: true).ConfigureAwait(true);
        }

        private async Task ExecuteUltralyticsPackageCommandAsync(bool uninstall)
        {
            string operationName = WpfYoloEnvironmentCommandPresentationService.BuildUltralyticsOperationName(uninstall);
            PythonModelSettings settings = CreateYoloModelSettingsSnapshot();
            PythonModelRuntimeInstallPlan plan = PythonModelRuntimeInstallPlanService.BuildPlan(settings);
            bool canRun = uninstall ? plan.CanRunUninstall : plan.CanRunInstall;
            if (!plan.IsVisible || !canRun)
            {
                string status = WpfYoloEnvironmentCommandPresentationService.BuildUltralyticsUnavailableStatus(operationName, plan);
                SetYoloCommandStatus(status, isBusy: false);
                YoloModelSettingsViewModel?.SetRuntimeProfileActionStatus(status);
                AppendLog(WpfYoloEnvironmentCommandPresentationService.BuildUltralyticsSkippedLog(operationName, status));
                return;
            }

            if (!ConfirmUltralyticsPackageOperation(uninstall, plan))
            {
                string canceledText = WpfYoloEnvironmentCommandPresentationService.BuildUltralyticsCanceledStatus(operationName);
                SetYoloCommandStatus(canceledText, isBusy: false);
                YoloModelSettingsViewModel?.SetRuntimeProfileActionStatus(canceledText);
                SetUltralyticsPackageOperationResult(
                    WpfYoloEnvironmentCommandPresentationService.BuildUltralyticsOperationSummary(DateTime.Now, operationName, "\uCDE8\uC18C"),
                    WpfYoloEnvironmentCommandPresentationService.BuildUltralyticsPackageOperationDetail(plan, uninstall, null, canceledText));
                AppendLog(canceledText);
                return;
            }

            if (!BeginYoloEnvironmentCommand(WpfYoloEnvironmentCommandPresentationService.BuildUltralyticsRunningStatus(operationName)))
            {
                return;
            }

            try
            {
                AppendLog(WpfYoloEnvironmentCommandPresentationService.BuildUltralyticsStartLog(operationName, plan));
                PythonPackageInstallResult result = uninstall
                    ? await PythonEnvironmentService.UninstallPackageAsync(settings, "ultralytics").ConfigureAwait(true)
                    : await PythonEnvironmentService.InstallPackageAsync(settings, "ultralytics").ConfigureAwait(true);

                AppendPythonPackageOperationLog(operationName, result);
                YoloModelSettingsViewModel?.LoadFrom(settings);
                RefreshYoloStatus();

                string statusText = WpfYoloEnvironmentCommandPresentationService.BuildUltralyticsResultStatus(uninstall, operationName, result);
                SetYoloCommandStatus(statusText, isBusy: false);
                YoloModelSettingsViewModel?.SetRuntimeProfileActionStatus(statusText);
                SetUltralyticsPackageOperationResult(
                    WpfYoloEnvironmentCommandPresentationService.BuildUltralyticsOperationSummary(DateTime.Now, operationName, result.Succeeded ? "\uC131\uACF5" : "\uC2E4\uD328"),
                    WpfYoloEnvironmentCommandPresentationService.BuildUltralyticsPackageOperationDetail(plan, uninstall, result, statusText));
                AppendLog(statusText);
            }
            catch (Exception ex)
            {
                string statusText = WpfYoloEnvironmentCommandPresentationService.BuildUltralyticsFailureStatus(operationName, ex.Message);
                SetYoloCommandStatus(statusText, isBusy: false);
                YoloModelSettingsViewModel?.SetRuntimeProfileActionStatus(statusText);
                SetUltralyticsPackageOperationResult(
                    WpfYoloEnvironmentCommandPresentationService.BuildUltralyticsOperationSummary(DateTime.Now, operationName, "\uC2E4\uD328"),
                    WpfYoloEnvironmentCommandPresentationService.BuildUltralyticsPackageOperationDetail(plan, uninstall, null, statusText));
                AppendLog(statusText);
            }
            finally
            {
                EndYoloEnvironmentCommand();
            }
        }

        private bool ConfirmUltralyticsPackageOperation(bool uninstall, PythonModelRuntimeInstallPlan plan)
        {
            WpfUltralyticsPackageConfirmationPresentation presentation =
                WpfYoloEnvironmentCommandPresentationService.BuildUltralyticsConfirmation(uninstall, plan);
            WpfMessageDialogResult result = WpfMessageDialog.Confirm(
                this,
                presentation.Title,
                presentation.Detail,
                presentation.PrimaryButtonText,
                presentation.CancelButtonText);
            return result == WpfMessageDialogResult.Yes;
        }

        private void SetUltralyticsPackageOperationResult(string summaryText, string detailText)
        {
            YoloModelSettingsViewModel?.SetRuntimePackageOperationResult(summaryText, detailText);
        }

        private void AppendPythonPackageOperationLog(string operationName, PythonPackageInstallResult result)
        {
            if (result == null)
            {
                AppendLog($"{operationName} \uACB0\uACFC\uB97C \uC77D\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4.");
                return;
            }

            AppendLog($"{operationName} \uBA85\uB839: {result.CommandLine}");
            foreach (string line in SplitPackageCommandLogTail(result.Output, maxLines: 8))
            {
                AppendLog($"[stdout] {line}");
            }

            foreach (string line in SplitPackageCommandLogTail(result.Error, maxLines: 8))
            {
                AppendLog($"[stderr] {line}");
            }
        }

        private static string[] SplitPackageCommandLogTail(string text, int maxLines)
        {
            if (string.IsNullOrWhiteSpace(text) || maxLines <= 0)
            {
                return Array.Empty<string>();
            }

            return text
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .TakeLast(maxLines)
                .ToArray();
        }

        private async void ExecuteRunYoloSmokeCommand()
        {
            if (currentWorkflowMode != WorkflowMode.Inference)
            {
                SetWorkflowMode(WorkflowMode.Inference);
                AppendLog(WpfYoloEnvironmentCommandPresentationService.BuildModelTestModeSwitchLog());
            }

            if (!EnsureModelRuntimeForInference())
            {
                return;
            }

            if (!BeginYoloEnvironmentCommand(WpfYoloEnvironmentCommandPresentationService.BuildModelTestStartingStatus()))
            {
                return;
            }

            try
            {
                await RunInteractiveDetectionAsync(allowSmokeFallback: true).ConfigureAwait(true);
                await RefreshYoloSettingsPanelAsync().ConfigureAwait(true);
                SetYoloCommandStatus(WpfYoloEnvironmentCommandPresentationService.BuildModelTestCompletedStatus(), isBusy: false);
            }
            catch (Exception ex)
            {
                string errorText = WpfYoloEnvironmentCommandPresentationService.BuildModelTestFailureStatus(ex.Message);
                WpfYoloEnvironmentRecoveryPresentation recovery = WpfYoloEnvironmentCommandPresentationService.BuildModelTestFailureRecovery(errorText);
                SetYoloCommandStatus(errorText, isBusy: false);
                SetYoloRecoveryStatus(recovery.Title, recovery.Detail, recovery.Action);
                AppendLog(errorText);
            }
            finally
            {
                EndYoloEnvironmentCommand();
            }
        }

        private async void ExecuteRestartPythonWorkerCommand()
        {
            if (!BeginYoloEnvironmentCommand(WpfYoloEnvironmentCommandPresentationService.BuildWorkerRestartStartingStatus()))
            {
                return;
            }

            try
            {
                PythonModelRuntimeState runtimeState = GetPythonModelRuntimeState();
                if (!runtimeState.IsRuntimeInstalled)
                {
                    ShowModelRuntimeUnavailable(runtimeState.NextActionText, runtimeState);
                    return;
                }

                bool connected = await global
                    .RestartPythonModelClientConnectionAsync(GetWorkerConnectTimeoutMilliseconds())
                    .ConfigureAwait(true);

                if (connected)
                {
                    string requestId = CreateRequestId();
                    global.DeepLearning.SendHealthCheck(requestId);
                    global.DeepLearning.SendModelStatus(requestId, ensureLoaded: false);
                }

                await RefreshYoloSettingsPanelAsync().ConfigureAwait(true);
                string restartText = connected
                    ? WpfYoloEnvironmentCommandPresentationService.BuildWorkerRestartConnectedStatus()
                    : BuildPythonWorkerFailureText();
                SetYoloCommandStatus(restartText, isBusy: false);
                if (!connected)
                {
                    WpfYoloEnvironmentRecoveryPresentation recovery = WpfYoloEnvironmentCommandPresentationService.BuildWorkerRestartConnectionFailureRecovery(restartText);
                    SetYoloRecoveryStatus(recovery.Title, recovery.Detail, recovery.Action);
                }

                AppendLog(restartText);
            }
            catch (Exception ex)
            {
                string errorText = WpfYoloEnvironmentCommandPresentationService.BuildWorkerRestartFailureStatus(ex.Message);
                WpfYoloEnvironmentRecoveryPresentation recovery = WpfYoloEnvironmentCommandPresentationService.BuildWorkerRestartFailureRecovery(errorText);
                SetYoloCommandStatus(errorText, isBusy: false);
                SetYoloRecoveryStatus(recovery.Title, recovery.Detail, recovery.Action);
                AppendLog(errorText);
            }
            finally
            {
                EndYoloEnvironmentCommand();
            }
        }

        private async void ExecuteStopPythonWorkerCommand()
        {
            if (!BeginYoloEnvironmentCommand(WpfYoloEnvironmentCommandPresentationService.BuildWorkerStopStartingStatus()))
            {
                return;
            }

            try
            {
                await global.StopPythonModelClientConnectionAsync().ConfigureAwait(true);
                await RefreshYoloSettingsPanelAsync().ConfigureAwait(true);
                string stopText = WpfYoloEnvironmentCommandPresentationService.BuildWorkerStopCompletedStatus();
                SetYoloCommandStatus(stopText, isBusy: false);
                AppendLog(stopText);
            }
            catch (Exception ex)
            {
                string errorText = WpfYoloEnvironmentCommandPresentationService.BuildWorkerStopFailureStatus(ex.Message);
                SetYoloCommandStatus(errorText, isBusy: false);
                AppendLog(errorText);
            }
            finally
            {
                EndYoloEnvironmentCommand();
            }
        }
    }
}
