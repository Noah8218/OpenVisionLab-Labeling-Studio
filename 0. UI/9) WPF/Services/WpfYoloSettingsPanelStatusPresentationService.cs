using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace MvcVisionSystem
{
    public static class WpfYoloSettingsPanelStatusPresentationService
    {
        public static string BuildDetail(
            PythonModelSettings settings,
            PythonModelValidationResult validation,
            PythonModelRuntimeState runtimeState,
            PythonCommunicationStatus communicationStatus,
            bool pythonClientProcessRunning,
            PythonEnvironmentCheckResult environment,
            string environmentCheckError)
        {
            settings ??= new PythonModelSettings();
            runtimeState ??= PythonModelSettingsValidator.GetRuntimeState(settings);
            communicationStatus ??= new PythonCommunicationStatus();

            var detail = new StringBuilder();
            detail.AppendLine($"실행 파일: {PythonModelSettingsValidator.ResolvePythonExecutable(settings)}");
            detail.AppendLine($"프로젝트: {settings.ProjectRootPath}");
            detail.AppendLine($"실행 스크립트: {settings.ClientScriptPath}");
            detail.AppendLine($"모델 파일: {settings.WeightsPath}");
            detail.AppendLine($"이미지: {settings.ImageRootPath}");
            detail.AppendLine($"실행 환경 설정: {settings.GetRequirementsPath()}");
            detail.AppendLine($"신뢰도: {settings.MinimumDetectionConfidence.ToString("0.##", CultureInfo.InvariantCulture)}");
            detail.AppendLine($"시간 제한: {settings.DetectionTimeoutSeconds}s");
            AppendPythonWorkerStatus(detail, communicationStatus, pythonClientProcessRunning);
            AppendValidationMessages(detail, validation);
            AppendPackageStatus(detail, runtimeState, environment, environmentCheckError);
            return detail.ToString();
        }

        private static void AppendValidationMessages(StringBuilder detail, PythonModelValidationResult validation)
        {
            if (validation == null || (validation.Errors.Count == 0 && validation.Warnings.Count == 0))
            {
                return;
            }

            detail.AppendLine();
            foreach (string error in validation.Errors)
            {
                detail.AppendLine($"오류: {NormalizeDisplayText(error)}");
            }

            foreach (string warning in validation.Warnings)
            {
                detail.AppendLine($"주의: {NormalizeDisplayText(warning)}");
            }
        }

        private static void AppendPackageStatus(
            StringBuilder detail,
            PythonModelRuntimeState runtimeState,
            PythonEnvironmentCheckResult environment,
            string environmentCheckError)
        {
            detail.AppendLine();
            if (runtimeState?.IsRuntimeInstalled != true)
            {
                detail.AppendLine("패키지: 모델 실행기 설치 후 확인");
                detail.AppendLine(NormalizeDisplayText(runtimeState?.DetailText));
                return;
            }

            if (environment != null)
            {
                detail.AppendLine($"패키지: {BuildPythonEnvironmentSummary(environment)}");
                detail.AppendLine($"필요 패키지: {Math.Max(0, environment.RequiredPackages?.Count ?? 0)}");
                if ((environment.MissingPackages?.Count ?? 0) > 0)
                {
                    detail.AppendLine($"누락: {string.Join(", ", environment.MissingPackages.Take(12))}");
                }

                return;
            }

            if (!string.IsNullOrWhiteSpace(environmentCheckError))
            {
                detail.AppendLine($"패키지: 점검 실패 - {NormalizeDisplayText(environmentCheckError)}");
            }
        }

        private static void AppendPythonWorkerStatus(
            StringBuilder detail,
            PythonCommunicationStatus status,
            bool pythonClientProcessRunning)
        {
            detail.AppendLine(
                $"추론 실행기: 대기 {FormatBooleanStatus(status.IsListening, "켜짐", "꺼짐")} / 연결 {FormatBooleanStatus(status.IsClientConnected, "연결됨", "미연결")} / 실행 {FormatBooleanStatus(pythonClientProcessRunning, "실행 중", "중지")}");
            if (status.ListenerPort > 0)
            {
                detail.AppendLine($"추론 연결 포트: {status.ListenerPort}");
            }

            if (HasWorkerCapability(status))
            {
                detail.AppendLine(
                    $"worker capability: models={FormatModelList(status.WorkerSupportedModels)} / training={FormatModelList(status.WorkerTrainingModels)} / detection={FormatModelList(status.WorkerDetectionModels)} / segmentation={FormatModelList(status.WorkerSegmentationModels)} / classification={FormatModelList(status.WorkerClassificationModels)}");
            }

            if ((status.WorkerCachedTrainingWeights?.Count ?? 0) > 0
                || (status.WorkerMissingTrainingWeights?.Count ?? 0) > 0)
            {
                detail.AppendLine(
                    $"학습 weight 캐시: 보유={FormatModelList(status.WorkerCachedTrainingWeights)} / 누락={FormatModelList(status.WorkerMissingTrainingWeights)}");
                detail.AppendLine(
                    $"학습 weight 런타임: 실행 가능={FormatModelList(status.WorkerRuntimeReadyTrainingWeights)} / 런타임 차단={FormatModelList(status.WorkerRuntimeBlockedTrainingWeights)}");
                detail.AppendLine(
                    $"학습 weight 준비: 다운로드/캐시 필요={FormatModelList(status.WorkerDownloadRequiredTrainingWeights)} / 런타임 지원 필요={FormatModelList(status.WorkerRuntimeBlockedMissingTrainingWeights)}");
            }

            if (!string.IsNullOrWhiteSpace(status.LastWorkerState)
                || !string.IsNullOrWhiteSpace(status.LastWorkerMessage))
            {
                detail.AppendLine($"추론 상태: {FormatWorkerState(status.LastWorkerState)} {TranslateWorkerMessage(status.LastWorkerMessage)}".TrimEnd());
            }

            if (!string.IsNullOrWhiteSpace(status.LastModelState)
                || !string.IsNullOrWhiteSpace(status.LastModelMessage))
            {
                string modelLoadedText = FormatBooleanStatus(status.LastModelLoaded, "예", "아니요");
                detail.AppendLine($"모델 상태: {FirstNonEmpty(status.LastModelState, "-")} / 로드:{modelLoadedText} {NormalizeOptionalText(status.LastModelMessage)}".TrimEnd());
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
                    : WpfTrainingProgressPresentationService.FormatTrainingMessage(status.LastTrainingMessage);
                detail.AppendLine($"학습: {WpfTrainingProgressPresentationService.FormatTrainingState(status.LastTrainingState)} {progress}{epoch} {trainingMessage}".TrimEnd());
            }

            if (!string.IsNullOrWhiteSpace(status.LastTrainingWeightsPath))
            {
                detail.AppendLine($"학습 weight 선택: {NormalizeOptionalText(status.LastTrainingWeightsPath)}");
            }

            if (!string.IsNullOrWhiteSpace(status.LastError))
            {
                detail.AppendLine($"실행 오류: {FormatWorkerError(status.LastError)}");
            }

            if ((status.WorkerRuntimeWarnings?.Count ?? 0) > 0)
            {
                foreach (string warning in status.WorkerRuntimeWarnings.Take(5))
                {
                    detail.AppendLine($"추론 경고: {NormalizeDisplayText(warning)}");
                }
            }
        }

        private static string BuildPythonEnvironmentSummary(PythonEnvironmentCheckResult environment)
        {
            if ((environment.Errors?.Count ?? 0) > 0)
            {
                return NormalizeDisplayText(environment.Errors[0]);
            }

            if ((environment.MissingPackages?.Count ?? 0) > 0)
            {
                return $"누락 패키지: {string.Join(", ", environment.MissingPackages.Take(6))}";
            }

            if (environment.IsReady)
            {
                return "추론 실행 환경 준비 완료.";
            }

            if ((environment.Warnings?.Count ?? 0) > 0)
            {
                return NormalizeDisplayText(environment.Warnings[0]);
            }

            return NormalizeDisplayText(environment.Summary);
        }

        private static bool HasWorkerCapability(PythonCommunicationStatus status)
            => (status?.WorkerSupportedModels?.Count ?? 0) > 0
                || (status?.WorkerTrainingModels?.Count ?? 0) > 0
                || (status?.WorkerDetectionModels?.Count ?? 0) > 0
                || (status?.WorkerSegmentationModels?.Count ?? 0) > 0
                || (status?.WorkerClassificationModels?.Count ?? 0) > 0;

        private static string FormatModelList(System.Collections.Generic.IEnumerable<string> values)
        {
            string text = string.Join(",", (values ?? Enumerable.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Take(8));
            return string.IsNullOrWhiteSpace(text) ? "-" : text;
        }

        private static string FormatWorkerState(string state)
        {
            string normalized = state?.Trim() ?? string.Empty;
            return normalized.ToLowerInvariant() switch
            {
                "" => "-",
                "listening" => "수신 대기",
                "connected" => "연결됨",
                "running" => "실행 중",
                "stopped" => "중지",
                "error" => "오류",
                _ => normalized
            };
        }

        private static string TranslateWorkerMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return string.Empty;
            }

            return message.Trim() switch
            {
                "Python TCP listener is waiting for a client." => "추론 실행기 연결 대기 중입니다.",
                "Python TCP listener stopped." => "추론 실행기가 중지되었습니다.",
                _ => message.Trim()
            };
        }

        private static string FormatBooleanStatus(bool value, string trueText, string falseText)
        {
            return value ? trueText : falseText;
        }

        private static string NormalizeDisplayText(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? "상세 없음" : text.Trim();
        }

        private static string FormatWorkerError(string text)
        {
            string normalized = NormalizeDisplayText(text);
            return normalized.Contains("TrainingWeightDownloadRequired", StringComparison.OrdinalIgnoreCase)
                ? "학습 weight 다운로드 승인 필요: 모델 weight를 캐시에 추가하거나 다운로드를 명시적으로 승인하세요."
                : normalized;
        }

        private static string NormalizeOptionalText(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
            {
                return string.Empty;
            }

            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }
    }
}
