using MahApps.Metro.IconPacks;
using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

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
            PythonModelRuntimeState runtimeState = PythonModelSettingsValidator.GetRuntimeState(settings);
            validation ??= runtimeState.State == PythonModelRuntimeStateKind.NotInstalled
                ? new PythonModelValidationResult(new[] { runtimeState.NextActionText }, Array.Empty<string>())
                : PythonModelSettingsValidator.Validate(settings, requireWeights: true);

            var detail = new StringBuilder();
            detail.AppendLine($"\uC2E4\uD589 \uD30C\uC77C: {PythonModelSettingsValidator.ResolvePythonExecutable(settings)}");
            detail.AppendLine($"프로젝트: {settings.ProjectRootPath}");
            detail.AppendLine($"\uC2E4\uD589 \uC2A4\uD06C\uB9BD\uD2B8: {settings.ClientScriptPath}");
            detail.AppendLine($"\uBAA8\uB378 \uD30C\uC77C: {settings.WeightsPath}");
            detail.AppendLine($"이미지: {settings.ImageRootPath}");
            detail.AppendLine($"\uC2E4\uD589 \uD658\uACBD \uC124\uC815: {settings.GetRequirementsPath()}");
            detail.AppendLine($"신뢰도: {settings.MinimumDetectionConfidence.ToString("0.##", CultureInfo.InvariantCulture)}");
            detail.AppendLine($"시간 제한: {settings.DetectionTimeoutSeconds}s");
            AppendPythonWorkerStatus(detail);

            if (validation.Errors.Count > 0 || validation.Warnings.Count > 0)
            {
                detail.AppendLine();
                foreach (string error in validation.Errors)
                {
                    detail.AppendLine($"오류: {error}");
                }

                foreach (string warning in validation.Warnings)
                {
                    detail.AppendLine($"주의: {warning}");
                }
            }

            if (!runtimeState.IsRuntimeInstalled)
            {
                detail.AppendLine();
                detail.AppendLine("\uD328\uD0A4\uC9C0: \uBAA8\uB378 \uC2E4\uD589\uAE30 \uC124\uCE58 \uD6C4 \uD655\uC778");
                detail.AppendLine(runtimeState.DetailText);
            }
            else
            {
                try
                {
                    PythonEnvironmentCheckResult environment = await PythonEnvironmentService
                        .CheckRequirementsAsync(settings)
                        .ConfigureAwait(true);
                    detail.AppendLine();
                    detail.AppendLine($"패키지: {TranslatePythonEnvironmentSummary(environment.Summary)}");
                    detail.AppendLine($"\uD544\uC694 \uD328\uD0A4\uC9C0: {environment.RequiredPackages.Count}");
                    if (environment.MissingPackages.Count > 0)
                    {
                        detail.AppendLine($"누락: {string.Join(", ", environment.MissingPackages.Take(12))}");
                    }
                }
                catch (Exception ex)
                {
                    detail.AppendLine();
                    detail.AppendLine($"패키지: 점검 실패 - {ex.Message}");
                }
            }

            if (YoloStatusViewModel != null)
            {
                YoloStatusViewModel.SetSettingsStatus(
                    runtimeState.SummaryText,
                    detail.ToString());
                return;
            }

            YoloSettingsSummaryText.Text = runtimeState.SummaryText;
            YoloSettingsDetailText.Text = detail.ToString();
        }

        private void AppendPythonWorkerStatus(StringBuilder detail)
        {
            PythonCommunicationStatus status = global.GetPythonCommunicationStatusSnapshot();
            detail.AppendLine($"\uCD94\uB860 \uC2E4\uD589\uAE30: \uB300\uAE30 {(status.IsListening ? "\uCF1C\uC9D0" : "\uAEBC\uC9D0")} / \uC5F0\uACB0 {(status.IsClientConnected ? "\uC5F0\uACB0\uB428" : "\uBBF8\uC5F0\uACB0")} / \uC2E4\uD589 {(global.PythonClientProcess.IsRunning ? "\uC2E4\uD589 \uC911" : "\uC911\uC9C0")}");
            if (status.ListenerPort > 0)
            {
                detail.AppendLine($"\uCD94\uB860 \uC5F0\uACB0 \uD3EC\uD2B8: {status.ListenerPort}");
            }

            if (!string.IsNullOrWhiteSpace(status.LastWorkerState)
                || !string.IsNullOrWhiteSpace(status.LastWorkerMessage))
            {
                detail.AppendLine($"\uCD94\uB860 \uC0C1\uD0DC: {FormatWorkerState(status.LastWorkerState)} {TranslateWorkerMessage(status.LastWorkerMessage)}".TrimEnd());
            }

            if (!string.IsNullOrWhiteSpace(status.LastModelState)
                || !string.IsNullOrWhiteSpace(status.LastModelMessage))
            {
                detail.AppendLine($"모델 상태: {FirstNonEmpty(status.LastModelState, "-")} / 로드:{status.LastModelLoaded} {status.LastModelMessage}".TrimEnd());
            }

            if (!string.IsNullOrWhiteSpace(status.LastTrainingState)
                || status.LastTrainingProgressPercent.HasValue)
            {
                string progress = status.LastTrainingProgressPercent.HasValue
                    ? $"{status.LastTrainingProgressPercent.Value}%"
                    : "-";
                string epoch = status.LastTrainingEpoch.HasValue && status.LastTrainingTotalEpochs.HasValue
                    ? $" 에폭 {status.LastTrainingEpoch.Value}/{status.LastTrainingTotalEpochs.Value}"
                    : string.Empty;
                string trainingMessage = string.IsNullOrWhiteSpace(status.LastTrainingMessage)
                    ? string.Empty
                    : FormatTrainingMessage(status.LastTrainingMessage);
                detail.AppendLine($"학습: {FormatTrainingState(status.LastTrainingState)} {progress}{epoch} {trainingMessage}".TrimEnd());
            }

            if (!string.IsNullOrWhiteSpace(status.LastError))
            {
                detail.AppendLine($"\uCD94\uB860 \uC624\uB958: {status.LastError}");
            }
        }
    }
}
