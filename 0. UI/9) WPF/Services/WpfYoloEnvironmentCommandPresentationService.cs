using MvcVisionSystem._1._Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MvcVisionSystem
{
    public sealed class WpfUltralyticsPackageConfirmationPresentation
    {
        public string Title { get; set; } = string.Empty;

        public string Detail { get; set; } = string.Empty;

        public string PrimaryButtonText { get; set; } = string.Empty;

        public string CancelButtonText { get; set; } = string.Empty;
    }

    public sealed class WpfYoloEnvironmentRecoveryPresentation
    {
        public string Title { get; set; } = string.Empty;

        public string Detail { get; set; } = string.Empty;

        public string Action { get; set; } = string.Empty;
    }

    public static class WpfYoloEnvironmentCommandPresentationService
    {
        public static string BuildBusyCommandLog()
        {
            return "모델 실행 명령이 이미 실행 중입니다.";
        }

        public static string BuildEnvironmentCheckStartingStatus()
        {
            return "모델 실행 환경 점검 중...";
        }

        public static string BuildEnvironmentReadyStatus()
        {
            return "모델 실행 환경 준비 완료.";
        }

        public static string BuildEnvironmentNeedsAttentionStatus()
        {
            return "모델 실행 환경 확인 필요.";
        }

        public static string BuildEnvironmentNeedsAttentionLogHeader()
        {
            return "모델 실행 환경 확인 필요:";
        }

        public static string BuildEnvironmentCheckFailureStatus(string message)
        {
            return $"모델 실행 환경 점검 실패: {NormalizeMessage(message)}";
        }

        public static string BuildRequirementsCheckStartingStatus()
        {
            return "추론 실행 환경 점검 중...";
        }

        public static string BuildRequirementsSkippedStatus(string summary)
        {
            return $"설치 건너뜀: {NormalizeMessage(summary)}";
        }

        public static string BuildRequirementsSkippedLog(string summary)
        {
            return $"추론 실행 환경 설치 건너뜀: {NormalizeMessage(summary)}";
        }

        public static string BuildRequirementsReadyStatus()
        {
            return "추론 실행 환경 정상.";
        }

        public static string BuildRequirementsNoMissingPackageLog()
        {
            return "추론 실행 환경 설치 건너뜀. 누락 패키지가 없습니다.";
        }

        public static string BuildRequirementsInstallingStatus(int missingPackageCount)
        {
            return $"누락 실행 환경 패키지 {Math.Max(0, missingPackageCount)}개 설치 중...";
        }

        public static string BuildRequirementsInstallingLog(IEnumerable<string> missingPackages)
        {
            string packageText = string.Join(", ", (missingPackages ?? Array.Empty<string>()).Take(8));
            return string.IsNullOrWhiteSpace(packageText)
                ? "추론 실행 환경 패키지 설치 중"
                : $"추론 실행 환경 패키지 설치 중: {packageText}";
        }

        public static string BuildRequirementsInstallResultStatus(PythonPackageInstallResult install)
        {
            if (install?.Succeeded == true)
            {
                return "추론 실행 환경 설치 완료. 다음은 테스트를 실행하세요.";
            }

            return $"설치 실패: {NormalizeMessage(install?.Summary)}";
        }

        public static string BuildRequirementsInstallResultLog(PythonPackageInstallResult install)
        {
            return install?.Succeeded == true
                ? "추론 실행 환경 설치 완료."
                : $"추론 실행 환경 설치 실패: {NormalizeMessage(install?.Summary)}";
        }

        public static string BuildRequirementsInstallFailureStatus(string message)
        {
            return $"설치 실패: {NormalizeMessage(message)}";
        }

        public static string BuildRequirementsInstallFailureLog(string message)
        {
            return $"추론 실행 환경 설치 실패: {NormalizeMessage(message)}";
        }

        public static string BuildUltralyticsOperationName(bool uninstall)
        {
            return uninstall ? "Ultralytics 제거" : "Ultralytics 설치";
        }

        public static string BuildUltralyticsUnavailableStatus(string operationName, PythonModelRuntimeInstallPlan plan)
        {
            return string.IsNullOrWhiteSpace(plan?.DetailText)
                ? $"{NormalizeMessage(operationName)}을 실행할 Python/venv를 먼저 연결하세요."
                : plan.DetailText.Trim();
        }

        public static string BuildUltralyticsSkippedLog(string operationName, string status)
        {
            return $"{NormalizeMessage(operationName)} 건너뜀: {NormalizeMessage(status)}";
        }

        public static string BuildUltralyticsCanceledStatus(string operationName)
        {
            return $"{NormalizeMessage(operationName)} 취소. 실행환경은 변경하지 않았습니다.";
        }

        public static string BuildUltralyticsRunningStatus(string operationName)
        {
            return $"{NormalizeMessage(operationName)} 중...";
        }

        public static string BuildUltralyticsStartLog(string operationName, PythonModelRuntimeInstallPlan plan)
        {
            return $"{NormalizeMessage(operationName)} 시작: {plan?.TargetEnvironmentText ?? string.Empty}";
        }

        public static string BuildUltralyticsOperationSummary(DateTime now, string operationName, string resultText)
        {
            return $"{now.ToString("HH:mm:ss", CultureInfo.CurrentCulture)} {NormalizeMessage(operationName)} {NormalizeMessage(resultText)}";
        }

        public static string BuildUltralyticsResultStatus(bool uninstall, string operationName, PythonPackageInstallResult result)
        {
            if (result?.Succeeded == true)
            {
                return uninstall
                    ? "Ultralytics 제거 완료. Self-test를 다시 확인했습니다. 테스트를 반복하려면 설치 실행을 다시 누르세요."
                    : "Ultralytics 설치 완료. Self-test를 다시 확인했습니다. 모델 파일을 선택하세요.";
            }

            return $"{NormalizeMessage(operationName)} 실패: {NormalizeMessage(result?.Summary)}";
        }

        public static string BuildUltralyticsFailureStatus(string operationName, string message)
        {
            return $"{NormalizeMessage(operationName)} 실패: {NormalizeMessage(message)}";
        }

        public static WpfUltralyticsPackageConfirmationPresentation BuildUltralyticsConfirmation(
            bool uninstall,
            PythonModelRuntimeInstallPlan plan)
        {
            string commandText = uninstall ? plan?.UninstallCommandText : plan?.InstallCommandText;
            string message = uninstall
                ? "테스트를 반복하기 위해 선택한 venv에서 ultralytics 패키지만 제거합니다."
                : "선택한 venv에 Ultralytics 패키지를 설치합니다.";

            return new WpfUltralyticsPackageConfirmationPresentation
            {
                Title = uninstall ? "Ultralytics 제거 확인" : "Ultralytics 설치 확인",
                PrimaryButtonText = uninstall ? "제거" : "설치 실행",
                CancelButtonText = "취소",
                Detail = string.Join(
                    Environment.NewLine,
                    message,
                    string.Empty,
                    $"대상: {plan?.TargetEnvironmentText ?? string.Empty}",
                    $"명령: {commandText ?? string.Empty}",
                    string.Empty,
                    "실행 후 이 패널의 self-test와 설치 상태를 다시 확인합니다.")
            };
        }

        public static string BuildUltralyticsPackageOperationDetail(
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
                ? "실행 안 함"
                : result.ExitCode.ToString(CultureInfo.InvariantCulture);

            return string.Join(
                Environment.NewLine,
                new[]
                {
                    $"결과: {statusText ?? string.Empty}",
                    $"대상: {plan?.TargetEnvironmentText ?? string.Empty}",
                    $"명령: {commandText ?? string.Empty}",
                    $"종료 코드: {exitText}",
                    string.IsNullOrWhiteSpace(logText) ? string.Empty : $"로그 요약: {logText}"
                }.Where(line => !string.IsNullOrWhiteSpace(line)));
        }

        public static string BuildModelTestModeSwitchLog()
        {
            return "\uBAA8\uB378 \uD14C\uC2A4\uD2B8\uB97C \uC704\uD574 \uCD94\uB860 \uAC80\uD1A0 \uBAA8\uB4DC\uB85C \uC804\uD658\uD588\uC2B5\uB2C8\uB2E4.";
        }

        public static string BuildModelTestStartingStatus()
        {
            return "\uBAA8\uB378 \uD14C\uC2A4\uD2B8 \uCD94\uB860 \uC911...";
        }

        public static string BuildModelTestCompletedStatus()
        {
            return "\uBAA8\uB378 \uD14C\uC2A4\uD2B8 \uCD94\uB860 \uC644\uB8CC.";
        }

        public static string BuildModelTestFailureStatus(string message)
        {
            return $"\uBAA8\uB378 \uD14C\uC2A4\uD2B8 \uCD94\uB860 \uC2E4\uD328: {NormalizeMessage(message)}";
        }

        public static WpfYoloEnvironmentRecoveryPresentation BuildModelTestFailureRecovery(string detail)
        {
            return new WpfYoloEnvironmentRecoveryPresentation
            {
                Title = "\uBAA8\uB378 \uD14C\uC2A4\uD2B8 \uC2E4\uD328",
                Detail = NormalizeMessage(detail),
                Action = "\uB2E4\uC74C: \uC2E4\uD589 \uD30C\uC77C, \uD504\uB85C\uC81D\uD2B8, \uC2A4\uD06C\uB9BD\uD2B8, \uAC80\uC0AC \uBAA8\uB378 \uACBD\uB85C\uB97C \uD655\uC778\uD55C \uB4A4 \uD14C\uC2A4\uD2B8\uB97C \uB2E4\uC2DC \uC2E4\uD589\uD558\uC138\uC694."
            };
        }

        public static string BuildWorkerRestartStartingStatus()
        {
            return "\uCD94\uB860 \uC2E4\uD589\uAE30 \uC7AC\uC2DC\uC791 \uC911...";
        }

        public static string BuildWorkerRestartConnectedStatus()
        {
            return "\uCD94\uB860 \uC2E4\uD589\uAE30 \uC7AC\uC2DC\uC791 \uBC0F \uC5F0\uACB0 \uC644\uB8CC.";
        }

        public static WpfYoloEnvironmentRecoveryPresentation BuildWorkerRestartConnectionFailureRecovery(string detail)
        {
            return new WpfYoloEnvironmentRecoveryPresentation
            {
                Title = "\uCD94\uB860 \uC2E4\uD589\uAE30 \uC5F0\uACB0 \uC2E4\uD328",
                Detail = NormalizeMessage(detail),
                Action = "\uB2E4\uC74C: \uBAA8\uB378 \uD14C\uC2A4\uD2B8\uB85C \uD658\uACBD\uC744 \uD655\uC778\uD558\uAC70\uB098 \uC2E4\uD589 \uD30C\uC77C/\uC2A4\uD06C\uB9BD\uD2B8 \uACBD\uB85C\uB97C \uC218\uC815\uD55C \uB4A4 \uC7AC\uC2DC\uC791\uD558\uC138\uC694."
            };
        }

        public static string BuildWorkerRestartFailureStatus(string message)
        {
            return $"\uCD94\uB860 \uC2E4\uD589\uAE30 \uC7AC\uC2DC\uC791 \uC2E4\uD328: {NormalizeMessage(message)}";
        }

        public static WpfYoloEnvironmentRecoveryPresentation BuildWorkerRestartFailureRecovery(string detail)
        {
            return new WpfYoloEnvironmentRecoveryPresentation
            {
                Title = "\uCD94\uB860 \uC2E4\uD589\uAE30 \uC7AC\uC2DC\uC791 \uC2E4\uD328",
                Detail = NormalizeMessage(detail),
                Action = "\uB2E4\uC74C: \uC0C1\uC138 \uB85C\uADF8\uC5D0\uC11C \uC624\uB958\uB97C \uD655\uC778\uD558\uACE0 Python/\uBAA8\uB378 \uC2E4\uD589 \uC124\uC815 \uACBD\uB85C\uB97C \uC218\uC815\uD558\uC138\uC694."
            };
        }

        public static string BuildWorkerStopStartingStatus()
        {
            return "\uCD94\uB860 \uC2E4\uD589\uAE30 \uC911\uC9C0 \uC911...";
        }

        public static string BuildWorkerStopCompletedStatus()
        {
            return "\uCD94\uB860 \uC2E4\uD589\uAE30 \uC911\uC9C0 \uC644\uB8CC.";
        }

        public static string BuildWorkerStopFailureStatus(string message)
        {
            return $"\uCD94\uB860 \uC2E4\uD589\uAE30 \uC911\uC9C0 \uC2E4\uD328: {NormalizeMessage(message)}";
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

        private static string NormalizeMessage(string message)
        {
            return string.IsNullOrWhiteSpace(message)
                ? "상세 원인을 확인할 수 없습니다."
                : message.Trim();
        }
    }
}
