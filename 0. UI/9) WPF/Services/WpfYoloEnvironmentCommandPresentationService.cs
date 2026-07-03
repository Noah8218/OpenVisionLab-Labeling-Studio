using MvcVisionSystem._1._Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem
{
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

        private static string NormalizeMessage(string message)
        {
            return string.IsNullOrWhiteSpace(message)
                ? "상세 원인을 확인할 수 없습니다."
                : message.Trim();
        }
    }
}
