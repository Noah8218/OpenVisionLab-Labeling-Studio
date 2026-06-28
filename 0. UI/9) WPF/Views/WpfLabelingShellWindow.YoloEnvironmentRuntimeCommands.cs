using MvcVisionSystem.Yolo;
using MvcVisionSystem._1._Core;
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
            if (!BeginYoloEnvironmentCommand("YOLO 설정 점검 중..."))
            {
                return;
            }

            try
            {
                EnsureProjectSettings();
                PythonModelValidationResult result = PythonModelSettingsValidator.Validate(global.Data.ProjectSettings.PythonModel, requireWeights: true);
                RefreshYoloStatus();
                YoloSettingsReviewTab.IsSelected = true;
                await RefreshYoloSettingsPanelAsync(result).ConfigureAwait(true);

                if (result.IsValid)
                {
                    SetYoloCommandStatus("YOLO 설정 준비 완료.", isBusy: false);
                    AppendLog("YOLO 설정 준비 완료.");
                    return;
                }

                SetYoloCommandStatus("YOLO 설정 확인 필요.", isBusy: false);
                AppendLog("YOLO 설정 확인 필요:");
                foreach (string line in result.Errors.Concat(result.Warnings))
                {
                    AppendLog($"- {line}");
                }
            }
            catch (Exception ex)
            {
                SetYoloCommandStatus($"YOLO 설정 점검 실패: {ex.Message}", isBusy: false);
                AppendLog($"YOLO 설정 점검 실패: {ex.Message}");
            }
            finally
            {
                EndYoloEnvironmentCommand();
            }
        }

        private async void ExecuteDetectCurrentImageCommand()
        {
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
                EnsureProjectSettings();
                PythonModelSettings settings = global.Data.ProjectSettings.PythonModel;
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

        private async void ExecuteRunYoloSmokeCommand()
        {
            if (currentWorkflowMode != WorkflowMode.Inference)
            {
                SetWorkflowMode(WorkflowMode.Inference);
                AppendLog("YOLO 테스트를 위해 추론 검토 모드로 전환했습니다.");
            }

            if (!BeginYoloEnvironmentCommand("YOLO 테스트 추론 중..."))
            {
                return;
            }

            try
            {
                await RunInteractiveDetectionAsync(allowSmokeFallback: true).ConfigureAwait(true);
                await RefreshYoloSettingsPanelAsync().ConfigureAwait(true);
                SetYoloCommandStatus("YOLO 테스트 추론 완료.", isBusy: false);
            }
            catch (Exception ex)
            {
                SetYoloCommandStatus($"YOLO 테스트 추론 실패: {ex.Message}", isBusy: false);
                AppendLog($"YOLO 테스트 추론 실패: {ex.Message}");
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
                SetYoloCommandStatus(connected
                    ? "\uCD94\uB860 \uC2E4\uD589\uAE30 \uC7AC\uC2DC\uC791 \uBC0F \uC5F0\uACB0 \uC644\uB8CC."
                    : BuildPythonWorkerFailureText(), isBusy: false);
                AppendLog(connected
                    ? "\uCD94\uB860 \uC2E4\uD589\uAE30 \uC7AC\uC2DC\uC791 \uBC0F \uC5F0\uACB0 \uC644\uB8CC."
                    : BuildPythonWorkerFailureText());
            }
            catch (Exception ex)
            {
                SetYoloCommandStatus($"\uCD94\uB860 \uC2E4\uD589\uAE30 \uC7AC\uC2DC\uC791 \uC2E4\uD328: {ex.Message}", isBusy: false);
                AppendLog($"\uCD94\uB860 \uC2E4\uD589\uAE30 \uC7AC\uC2DC\uC791 \uC2E4\uD328: {ex.Message}");
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
