using MvcVisionSystem.Yolo;
using MvcVisionSystem._1._Core;
using OpenVisionLab.Wpf.MessageDialogs;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Runtime environment commands manage Python/worker state and should not mix with settings field browsing.
        private async void ExecuteCheckYoloCommand()
        {
            if (!BeginYoloEnvironmentCommand("\uBAA8\uB378 \uC2E4\uD589 \uD658\uACBD \uC810\uAC80 \uC911..."))
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
                    SetYoloCommandStatus("\uBAA8\uB378 \uC2E4\uD589 \uD658\uACBD \uC900\uBE44 \uC644\uB8CC.", isBusy: false);
                    AppendLog("\uBAA8\uB378 \uC2E4\uD589 \uD658\uACBD \uC900\uBE44 \uC644\uB8CC.");
                    return;
                }

                SetYoloCommandStatus("\uBAA8\uB378 \uC2E4\uD589 \uD658\uACBD \uD655\uC778 \uD544\uC694.", isBusy: false);
                AppendLog("\uBAA8\uB378 \uC2E4\uD589 \uD658\uACBD \uD655\uC778 \uD544\uC694:");
                foreach (string line in result.Errors.Concat(result.Warnings))
                {
                    AppendLog($"- {line}");
                }
            }
            catch (Exception ex)
            {
                SetYoloCommandStatus($"\uBAA8\uB378 \uC2E4\uD589 \uD658\uACBD \uC810\uAC80 \uC2E4\uD328: {ex.Message}", isBusy: false);
                AppendLog($"\uBAA8\uB378 \uC2E4\uD589 \uD658\uACBD \uC810\uAC80 \uC2E4\uD328: {ex.Message}");
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
            if (!BeginYoloEnvironmentCommand("\uCD94\uB860 \uC2E4\uD589 \uD658\uACBD \uC810\uAC80 \uC911..."))
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
                    SetYoloCommandStatus($"설치 건너뜀: {check.Summary}", isBusy: false);
                    await RefreshYoloSettingsPanelAsync().ConfigureAwait(true);
                    AppendLog($"\uCD94\uB860 \uC2E4\uD589 \uD658\uACBD \uC124\uCE58 \uAC74\uB108\uB700: {check.Summary}");
                    return;
                }

                if (check.MissingPackages.Count == 0)
                {
                    SetYoloCommandStatus("\uCD94\uB860 \uC2E4\uD589 \uD658\uACBD \uC815\uC0C1.", isBusy: false);
                    await RefreshYoloSettingsPanelAsync().ConfigureAwait(true);
                    AppendLog("\uCD94\uB860 \uC2E4\uD589 \uD658\uACBD \uC124\uCE58 \uAC74\uB108\uB700. \uB204\uB77D \uD328\uD0A4\uC9C0\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.");
                    return;
                }

                SetYoloCommandStatus($"\uB204\uB77D \uC2E4\uD589 \uD658\uACBD \uD328\uD0A4\uC9C0 {check.MissingPackages.Count}\uAC1C \uC124\uCE58 \uC911...", isBusy: true);
                AppendLog($"\uCD94\uB860 \uC2E4\uD589 \uD658\uACBD \uD328\uD0A4\uC9C0 \uC124\uCE58 \uC911: {string.Join(", ", check.MissingPackages.Take(8))}");
                PythonPackageInstallResult install = await PythonEnvironmentService
                    .InstallRequirementsAsync(settings)
                    .ConfigureAwait(true);

                await RefreshYoloSettingsPanelAsync().ConfigureAwait(true);
                SetYoloCommandStatus(install.Succeeded
                    ? "\uCD94\uB860 \uC2E4\uD589 \uD658\uACBD \uC124\uCE58 \uC644\uB8CC. \uB2E4\uC74C\uC740 \uD14C\uC2A4\uD2B8\uB97C \uC2E4\uD589\uD558\uC138\uC694."
                    : $"설치 실패: {install.Summary}", isBusy: false);
                AppendLog(install.Succeeded
                    ? "\uCD94\uB860 \uC2E4\uD589 \uD658\uACBD \uC124\uCE58 \uC644\uB8CC."
                    : $"\uCD94\uB860 \uC2E4\uD589 \uD658\uACBD \uC124\uCE58 \uC2E4\uD328: {install.Summary}");
            }
            catch (Exception ex)
            {
                SetYoloCommandStatus($"설치 실패: {ex.Message}", isBusy: false);
                AppendLog($"\uCD94\uB860 \uC2E4\uD589 \uD658\uACBD \uC124\uCE58 \uC2E4\uD328: {ex.Message}");
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
            string operationName = uninstall ? "Ultralytics \uC81C\uAC70" : "Ultralytics \uC124\uCE58";
            PythonModelSettings settings = CreateYoloModelSettingsSnapshot();
            PythonModelRuntimeInstallPlan plan = PythonModelRuntimeInstallPlanService.BuildPlan(settings);
            bool canRun = uninstall ? plan.CanRunUninstall : plan.CanRunInstall;
            if (!plan.IsVisible || !canRun)
            {
                string status = string.IsNullOrWhiteSpace(plan.DetailText)
                    ? $"{operationName}\uC744 \uC2E4\uD589\uD560 Python/venv\uB97C \uBA3C\uC800 \uC5F0\uACB0\uD558\uC138\uC694."
                    : plan.DetailText;
                SetYoloCommandStatus(status, isBusy: false);
                YoloModelSettingsViewModel?.SetRuntimeProfileActionStatus(status);
                AppendLog($"{operationName} \uAC74\uB108\uB700: {status}");
                return;
            }

            if (!ConfirmUltralyticsPackageOperation(uninstall, plan))
            {
                string canceledText = $"{operationName} \uCDE8\uC18C. \uC2E4\uD589\uD658\uACBD\uC740 \uBCC0\uACBD\uD558\uC9C0 \uC54A\uC558\uC2B5\uB2C8\uB2E4.";
                SetYoloCommandStatus(canceledText, isBusy: false);
                YoloModelSettingsViewModel?.SetRuntimeProfileActionStatus(canceledText);
                SetUltralyticsPackageOperationResult(
                    $"{DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture)} {operationName} \uCDE8\uC18C",
                    BuildUltralyticsPackageOperationDetail(plan, uninstall, null, canceledText));
                AppendLog(canceledText);
                return;
            }

            if (!BeginYoloEnvironmentCommand($"{operationName} \uC911..."))
            {
                return;
            }

            try
            {
                AppendLog($"{operationName} \uC2DC\uC791: {plan.TargetEnvironmentText}");
                PythonPackageInstallResult result = uninstall
                    ? await PythonEnvironmentService.UninstallPackageAsync(settings, "ultralytics").ConfigureAwait(true)
                    : await PythonEnvironmentService.InstallPackageAsync(settings, "ultralytics").ConfigureAwait(true);

                AppendPythonPackageOperationLog(operationName, result);
                YoloModelSettingsViewModel?.LoadFrom(settings);
                RefreshYoloStatus();

                string statusText = result.Succeeded
                    ? uninstall
                        ? "Ultralytics \uC81C\uAC70 \uC644\uB8CC. Self-test\uB97C \uB2E4\uC2DC \uD655\uC778\uD588\uC2B5\uB2C8\uB2E4. \uD14C\uC2A4\uD2B8\uB97C \uBC18\uBCF5\uD558\uB824\uBA74 \uC124\uCE58 \uC2E4\uD589\uC744 \uB2E4\uC2DC \uB204\uB974\uC138\uC694."
                        : "Ultralytics \uC124\uCE58 \uC644\uB8CC. Self-test\uB97C \uB2E4\uC2DC \uD655\uC778\uD588\uC2B5\uB2C8\uB2E4. \uBAA8\uB378 \uD30C\uC77C\uC744 \uC120\uD0DD\uD558\uC138\uC694."
                    : $"{operationName} \uC2E4\uD328: {result.Summary}";
                SetYoloCommandStatus(statusText, isBusy: false);
                YoloModelSettingsViewModel?.SetRuntimeProfileActionStatus(statusText);
                SetUltralyticsPackageOperationResult(
                    $"{DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture)} {operationName} {(result.Succeeded ? "\uC131\uACF5" : "\uC2E4\uD328")}",
                    BuildUltralyticsPackageOperationDetail(plan, uninstall, result, statusText));
                AppendLog(statusText);
            }
            catch (Exception ex)
            {
                string statusText = $"{operationName} \uC2E4\uD328: {ex.Message}";
                SetYoloCommandStatus(statusText, isBusy: false);
                YoloModelSettingsViewModel?.SetRuntimeProfileActionStatus(statusText);
                SetUltralyticsPackageOperationResult(
                    $"{DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture)} {operationName} \uC2E4\uD328",
                    BuildUltralyticsPackageOperationDetail(plan, uninstall, null, statusText));
                AppendLog(statusText);
            }
            finally
            {
                EndYoloEnvironmentCommand();
            }
        }

        private bool ConfirmUltralyticsPackageOperation(bool uninstall, PythonModelRuntimeInstallPlan plan)
        {
            string title = uninstall
                ? "Ultralytics \uC81C\uAC70 \uD655\uC778"
                : "Ultralytics \uC124\uCE58 \uD655\uC778";
            string primaryButtonText = uninstall ? "\uC81C\uAC70" : "\uC124\uCE58 \uC2E4\uD589";
            string commandText = uninstall ? plan?.UninstallCommandText : plan?.InstallCommandText;
            string message = uninstall
                ? "\uD14C\uC2A4\uD2B8\uB97C \uBC18\uBCF5\uD558\uAE30 \uC704\uD574 \uC120\uD0DD\uD55C venv\uC5D0\uC11C ultralytics \uD328\uD0A4\uC9C0\uB9CC \uC81C\uAC70\uD569\uB2C8\uB2E4."
                : "\uC120\uD0DD\uD55C venv\uC5D0 Ultralytics \uD328\uD0A4\uC9C0\uB97C \uC124\uCE58\uD569\uB2C8\uB2E4.";
            string detail = string.Join(
                Environment.NewLine,
                message,
                string.Empty,
                $"\uB300\uC0C1: {plan?.TargetEnvironmentText ?? string.Empty}",
                $"\uBA85\uB839: {commandText ?? string.Empty}",
                string.Empty,
                "\uC2E4\uD589 \uD6C4 \uC774 \uD328\uB110\uC758 self-test\uC640 \uC124\uCE58 \uC0C1\uD0DC\uB97C \uB2E4\uC2DC \uD655\uC778\uD569\uB2C8\uB2E4.");

            WpfMessageDialogResult result = WpfMessageDialog.Confirm(
                this,
                title,
                detail,
                primaryButtonText,
                "\uCDE8\uC18C");
            return result == WpfMessageDialogResult.Yes;
        }

        private void SetUltralyticsPackageOperationResult(string summaryText, string detailText)
        {
            YoloModelSettingsViewModel?.SetRuntimePackageOperationResult(summaryText, detailText);
        }

        private static string BuildUltralyticsPackageOperationDetail(
            PythonModelRuntimeInstallPlan plan,
            bool uninstall,
            PythonPackageInstallResult result,
            string statusText)
        {
            string commandText = result?.CommandLine;
            if (string.IsNullOrWhiteSpace(commandText))
            {
                commandText = uninstall ? plan?.UninstallCommandText : plan?.InstallCommandText;
            }

            string logText = FirstPackageCommandLogLine(result?.Error);
            if (string.IsNullOrWhiteSpace(logText))
            {
                logText = FirstPackageCommandLogLine(result?.Output);
            }

            string exitText = result == null
                ? "\uC2E4\uD589 \uC548 \uD568"
                : result.ExitCode.ToString(CultureInfo.InvariantCulture);

            return string.Join(
                Environment.NewLine,
                new[]
                {
                    $"\uACB0\uACFC: {statusText ?? string.Empty}",
                    $"\uB300\uC0C1: {plan?.TargetEnvironmentText ?? string.Empty}",
                    $"\uBA85\uB839: {commandText ?? string.Empty}",
                    $"\uC885\uB8CC \uCF54\uB4DC: {exitText}",
                    string.IsNullOrWhiteSpace(logText) ? string.Empty : $"\uB85C\uADF8 \uC694\uC57D: {logText}"
                }.Where(line => !string.IsNullOrWhiteSpace(line)));
        }

        private static string FirstPackageCommandLogLine(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return text
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line)) ?? string.Empty;
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
                AppendLog("\uBAA8\uB378 \uD14C\uC2A4\uD2B8\uB97C \uC704\uD574 \uCD94\uB860 \uAC80\uD1A0 \uBAA8\uB4DC\uB85C \uC804\uD658\uD588\uC2B5\uB2C8\uB2E4.");
            }

            if (!EnsureModelRuntimeForInference())
            {
                return;
            }

            if (!BeginYoloEnvironmentCommand("\uBAA8\uB378 \uD14C\uC2A4\uD2B8 \uCD94\uB860 \uC911..."))
            {
                return;
            }

            try
            {
                await RunInteractiveDetectionAsync(allowSmokeFallback: true).ConfigureAwait(true);
                await RefreshYoloSettingsPanelAsync().ConfigureAwait(true);
                SetYoloCommandStatus("\uBAA8\uB378 \uD14C\uC2A4\uD2B8 \uCD94\uB860 \uC644\uB8CC.", isBusy: false);
            }
            catch (Exception ex)
            {
                string errorText = $"\uBAA8\uB378 \uD14C\uC2A4\uD2B8 \uCD94\uB860 \uC2E4\uD328: {ex.Message}";
                SetYoloCommandStatus(errorText, isBusy: false);
                SetYoloRecoveryStatus(
                    "\uBAA8\uB378 \uD14C\uC2A4\uD2B8 \uC2E4\uD328",
                    errorText,
                    "\uB2E4\uC74C: \uC2E4\uD589 \uD30C\uC77C, \uD504\uB85C\uC81D\uD2B8, \uC2A4\uD06C\uB9BD\uD2B8, \uAC80\uC0AC \uBAA8\uB378 \uACBD\uB85C\uB97C \uD655\uC778\uD55C \uB4A4 \uD14C\uC2A4\uD2B8\uB97C \uB2E4\uC2DC \uC2E4\uD589\uD558\uC138\uC694.");
                AppendLog(errorText);
            }
            finally
            {
                EndYoloEnvironmentCommand();
            }
        }

        private async void ExecuteRestartPythonWorkerCommand()
        {
            if (!BeginYoloEnvironmentCommand("\uCD94\uB860 \uC2E4\uD589\uAE30 \uC7AC\uC2DC\uC791 \uC911..."))
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
                    ? "\uCD94\uB860 \uC2E4\uD589\uAE30 \uC7AC\uC2DC\uC791 \uBC0F \uC5F0\uACB0 \uC644\uB8CC."
                    : BuildPythonWorkerFailureText();
                SetYoloCommandStatus(restartText, isBusy: false);
                if (!connected)
                {
                    SetYoloRecoveryStatus(
                        "\uCD94\uB860 \uC2E4\uD589\uAE30 \uC5F0\uACB0 \uC2E4\uD328",
                        restartText,
                        "\uB2E4\uC74C: \uBAA8\uB378 \uD14C\uC2A4\uD2B8\uB85C \uD658\uACBD\uC744 \uD655\uC778\uD558\uAC70\uB098 \uC2E4\uD589 \uD30C\uC77C/\uC2A4\uD06C\uB9BD\uD2B8 \uACBD\uB85C\uB97C \uC218\uC815\uD55C \uB4A4 \uC7AC\uC2DC\uC791\uD558\uC138\uC694.");
                }

                AppendLog(restartText);
            }
            catch (Exception ex)
            {
                string errorText = $"\uCD94\uB860 \uC2E4\uD589\uAE30 \uC7AC\uC2DC\uC791 \uC2E4\uD328: {ex.Message}";
                SetYoloCommandStatus(errorText, isBusy: false);
                SetYoloRecoveryStatus(
                    "\uCD94\uB860 \uC2E4\uD589\uAE30 \uC7AC\uC2DC\uC791 \uC2E4\uD328",
                    errorText,
                    "\uB2E4\uC74C: \uC0C1\uC138 \uB85C\uADF8\uC5D0\uC11C \uC624\uB958\uB97C \uD655\uC778\uD558\uACE0 Python/\uBAA8\uB378 \uC2E4\uD589 \uC124\uC815 \uACBD\uB85C\uB97C \uC218\uC815\uD558\uC138\uC694.");
                AppendLog(errorText);
            }
            finally
            {
                EndYoloEnvironmentCommand();
            }
        }

        private async void ExecuteStopPythonWorkerCommand()
        {
            if (!BeginYoloEnvironmentCommand("\uCD94\uB860 \uC2E4\uD589\uAE30 \uC911\uC9C0 \uC911..."))
            {
                return;
            }

            try
            {
                await global.StopPythonModelClientConnectionAsync().ConfigureAwait(true);
                await RefreshYoloSettingsPanelAsync().ConfigureAwait(true);
                SetYoloCommandStatus("\uCD94\uB860 \uC2E4\uD589\uAE30 \uC911\uC9C0 \uC644\uB8CC.", isBusy: false);
                AppendLog("\uCD94\uB860 \uC2E4\uD589\uAE30 \uC911\uC9C0 \uC644\uB8CC.");
            }
            catch (Exception ex)
            {
                SetYoloCommandStatus($"\uCD94\uB860 \uC2E4\uD589\uAE30 \uC911\uC9C0 \uC2E4\uD328: {ex.Message}", isBusy: false);
                AppendLog($"\uCD94\uB860 \uC2E4\uD589\uAE30 \uC911\uC9C0 \uC2E4\uD328: {ex.Message}");
            }
            finally
            {
                EndYoloEnvironmentCommand();
            }
        }
    }
}
