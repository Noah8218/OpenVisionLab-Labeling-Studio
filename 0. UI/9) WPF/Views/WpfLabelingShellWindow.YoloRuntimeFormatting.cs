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
        // Runtime formatting helpers are pure string/timeout utilities shared by inference and batch flows.
        private string BuildPythonWorkerFailureText()
        {
            PythonCommunicationStatus status = global.GetPythonCommunicationStatusSnapshot();
            string error = FirstNonEmpty(status.LastError, global.PythonClientProcess.LastError, "상세 없음");
            return $"\uCD94\uB860 \uC2E4\uD589\uAE30 \uC5F0\uACB0 \uC2E4\uD328: {error}";
        }

        private int GetWorkerConnectTimeoutMilliseconds()
        {
            int detectionTimeoutSeconds = global.Data?.ProjectSettings?.PythonModel?.DetectionTimeoutSeconds ?? 30;
            int startupTimeoutSeconds = Math.Clamp(detectionTimeoutSeconds + 90, 120, 300);
            return startupTimeoutSeconds * 1000;
        }

        private int GetInteractiveWorkerConnectTimeoutMilliseconds()
        {
            return GetWorkerConnectTimeoutMilliseconds();
        }

        private static string FormatElapsed(TimeSpan elapsed)
        {
            return elapsed.TotalSeconds >= 1
                ? $"{elapsed.TotalSeconds:0.0}s"
                : $"{elapsed.TotalMilliseconds:0}ms";
        }

        private static int ClampElapsedMilliseconds(TimeSpan elapsed)
        {
            return (int)Math.Clamp(elapsed.TotalMilliseconds, 0D, int.MaxValue);
        }

        private static string FormatAverageElapsed(TimeSpan totalElapsed, int count)
        {
            if (count <= 0)
            {
                return "평균 -";
            }

            return $"평균 {FormatElapsed(TimeSpan.FromMilliseconds(totalElapsed.TotalMilliseconds / count))}";
        }

        private static string FormatInferencePath(string path)
        {
            return path switch
            {
                "worker" => "\uCD94\uB860 \uC2E4\uD589\uAE30",
                "smoke fallback" => "\uD14C\uC2A4\uD2B8 \uACB0\uACFC",
                _ => FirstNonEmpty(path, "알 수 없음")
            };
        }

        private static string TranslatePythonEnvironmentSummary(string summary)
        {
            if (string.IsNullOrWhiteSpace(summary))
            {
                return "상태 미확인";
            }

            return summary.Trim() switch
            {
                "Python environment is ready." => "\uCD94\uB860 \uC2E4\uD589 \uD658\uACBD \uC900\uBE44 \uC644\uB8CC.",
                _ => summary.Trim()
            };
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
                "Python TCP listener is waiting for a client." => "\uCD94\uB860 \uC2E4\uD589\uAE30 \uC5F0\uACB0 \uB300\uAE30 \uC911\uC785\uB2C8\uB2E4.",
                "Python TCP listener stopped." => "\uCD94\uB860 \uC2E4\uD589\uAE30\uAC00 \uC911\uC9C0\uB418\uC5C8\uC2B5\uB2C8\uB2E4.",
                _ => message.Trim()
            };
        }

        private static string CreateRequestId()
        {
            return Guid.NewGuid().ToString("N");
        }
    }
}
