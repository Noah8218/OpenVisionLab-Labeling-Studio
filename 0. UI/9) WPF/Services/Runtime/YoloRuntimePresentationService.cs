using MvcVisionSystem._3._Communication.TCP;
using System;
using System.Globalization;

namespace MvcVisionSystem
{
    /// <summary>
    /// Formats Python runtime status, timeout, and elapsed-time text.
    /// The Window supplies snapshots; display captions use the shared language service.
    /// </summary>
    public static class YoloRuntimePresentationService
    {
        public static string BuildPythonWorkerFailureText(
            PythonCommunicationStatus communicationStatus,
            string processLastError)
        {
            string error = FirstNonEmpty(
                communicationStatus?.LastError,
                processLastError,
                OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloRuntimePresentationService.FailureNoDetail"));
            return string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeFollowOn.YoloRuntimePresentationService.21"), error);
        }

        public static int GetWorkerConnectTimeoutMilliseconds(int detectionTimeoutSeconds)
        {
            int startupTimeoutSeconds = Math.Clamp(detectionTimeoutSeconds + 90, 120, 300);
            return startupTimeoutSeconds * 1000;
        }

        public static int GetInteractiveWorkerConnectTimeoutMilliseconds(
            int detectionTimeoutSeconds,
            bool autoStartClient,
            bool allowSmokeFallback)
        {
            if (allowSmokeFallback && !autoStartClient)
            {
                return Math.Clamp(detectionTimeoutSeconds, 1, 30) * 1000;
            }

            return GetWorkerConnectTimeoutMilliseconds(detectionTimeoutSeconds);
        }

        public static string FormatElapsed(TimeSpan elapsed)
        {
            return elapsed.TotalSeconds >= 1
                ? $"{elapsed.TotalSeconds:0.0}s"
                : $"{elapsed.TotalMilliseconds:0}ms";
        }

        public static int ClampElapsedMilliseconds(TimeSpan elapsed)
        {
            return (int)Math.Clamp(elapsed.TotalMilliseconds, 0D, int.MaxValue);
        }

        public static string FormatAverageElapsed(TimeSpan totalElapsed, int count)
        {
            if (count <= 0)
            {
                return "평균 -";
            }

            return $"평균 {FormatElapsed(TimeSpan.FromMilliseconds(totalElapsed.TotalMilliseconds / count))}";
        }

        public static string FormatInferencePath(string path)
        {
            return path switch
            {
                "worker" => OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeFollowOn.YoloRuntimePresentationService.69"),
                "smoke fallback" => OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloRuntimePresentationService.SmokeFallback"),
                _ => FirstNonEmpty(path, OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloRuntimePresentationService.UnknownPath"))
            };
        }

        public static string TranslatePythonEnvironmentSummary(string summary)
        {
            if (string.IsNullOrWhiteSpace(summary))
            {
                return "상태 미확인";
            }

            return summary.Trim() switch
            {
                "Python environment is ready." => "추론 실행 환경 준비 완료.",
                _ => summary.Trim()
            };
        }

        public static string FormatWorkerState(string state)
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

        public static string TranslateWorkerMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return string.Empty;
            }

            return message.Trim() switch
            {
                "Python TCP listener is waiting for a client." => OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeFollowOn.YoloRuntimePresentationService.113"),
                "Python TCP listener stopped." => OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeFollowOn.YoloRuntimePresentationService.114"),
                _ => message.Trim()
            };
        }

        public static string CreateRequestId()
            => Guid.NewGuid().ToString("N");

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return string.Empty;
        }
    }

    [Obsolete("Use YoloRuntimePresentationService.", false)]
    public static class WpfYoloRuntimePresentationService
    {
        public static string BuildPythonWorkerFailureText(
            PythonCommunicationStatus communicationStatus,
            string processLastError)
            => YoloRuntimePresentationService.BuildPythonWorkerFailureText(communicationStatus, processLastError);

        public static int GetWorkerConnectTimeoutMilliseconds(int detectionTimeoutSeconds)
            => YoloRuntimePresentationService.GetWorkerConnectTimeoutMilliseconds(detectionTimeoutSeconds);

        public static int GetInteractiveWorkerConnectTimeoutMilliseconds(
            int detectionTimeoutSeconds,
            bool autoStartClient,
            bool allowSmokeFallback)
            => YoloRuntimePresentationService.GetInteractiveWorkerConnectTimeoutMilliseconds(detectionTimeoutSeconds, autoStartClient, allowSmokeFallback);

        public static string FormatElapsed(TimeSpan elapsed)
            => YoloRuntimePresentationService.FormatElapsed(elapsed);

        public static int ClampElapsedMilliseconds(TimeSpan elapsed)
            => YoloRuntimePresentationService.ClampElapsedMilliseconds(elapsed);

        public static string FormatAverageElapsed(TimeSpan totalElapsed, int count)
            => YoloRuntimePresentationService.FormatAverageElapsed(totalElapsed, count);

        public static string FormatInferencePath(string path)
            => YoloRuntimePresentationService.FormatInferencePath(path);

        public static string TranslatePythonEnvironmentSummary(string summary)
            => YoloRuntimePresentationService.TranslatePythonEnvironmentSummary(summary);

        public static string FormatWorkerState(string state)
            => YoloRuntimePresentationService.FormatWorkerState(state);

        public static string TranslateWorkerMessage(string message)
            => YoloRuntimePresentationService.TranslateWorkerMessage(message);

        public static string CreateRequestId()
            => YoloRuntimePresentationService.CreateRequestId();
    }
}
