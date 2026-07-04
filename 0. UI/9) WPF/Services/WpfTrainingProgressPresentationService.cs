using MvcVisionSystem._3._Communication.TCP;
using System;
using System.Collections.Generic;
using System.IO;

namespace MvcVisionSystem
{
    public static class WpfTrainingProgressPresentationService
    {
        private const string TrainingWeightDownloadRequiredCode = "TrainingWeightDownloadRequired";

        public static string BuildAcceptedWorkerWaitProgressText()
        {
            return "\uD559\uC2B5 \uBA85\uB839 \uC218\uB77D\uB428 / \uC6CC\uCEE4 \uC0C1\uD0DC \uB300\uAE30";
        }

        public static string BuildBeforeEpochText()
        {
            return "\uC5D0\uD3ED \uC2DC\uC791 \uC804";
        }

        public static string BuildIdleProgressText()
        {
            return "\uD559\uC2B5 \uB300\uAE30";
        }

        public static string BuildStatusNoResponseText()
        {
            return "\uD559\uC2B5 \uC0C1\uD0DC \uC751\uB2F5 \uC5C6\uC74C: \uBA85\uB839 \uC804\uC1A1 \uD6C4 \uC6CC\uCEE4\uC758 \uC5D0\uD3ED/\uC0C1\uD0DC \uB85C\uADF8\uAC00 \uB3C4\uCC29\uD558\uC9C0 \uC54A\uC558\uC2B5\uB2C8\uB2E4.";
        }

        public static WpfTrainingRecoveryStatus BuildStatusNoResponseRecovery(string detail)
        {
            return new WpfTrainingRecoveryStatus
            {
                Title = "\uD559\uC2B5 \uC0C1\uD0DC \uC751\uB2F5 \uC5C6\uC74C",
                Detail = NormalizeDetail(detail),
                Action = "\uB2E4\uC74C: \uC0C1\uC138 \uB85C\uADF8\uC5D0\uC11C Python worker \uCD9C\uB825\uC744 \uD655\uC778\uD558\uACE0, \uD544\uC694\uD558\uBA74 \uBAA8\uB378 \uD14C\uC2A4\uD2B8 \uB610\uB294 \uC7AC\uC2DC\uC791 \uD6C4 \uD559\uC2B5\uC744 \uB2E4\uC2DC \uC2DC\uC791\uD558\uC138\uC694."
            };
        }

        public static WpfTrainingRecoveryStatus BuildFailedRecovery(string detail)
        {
            return new WpfTrainingRecoveryStatus
            {
                Title = "\uD559\uC2B5 \uC2E4\uD328 - \uC870\uCE58 \uD544\uC694",
                Detail = NormalizeDetail(detail),
                Action = "\uB2E4\uC74C: \uC0C1\uC138 \uB85C\uADF8\uC758 \uB9C8\uC9C0\uB9C9 \uC624\uB958 \uD655\uC778 -> \uB370\uC774\uD130\uC14B \uC810\uAC80 -> \uD544\uC694 \uC2DC \uC774\uBBF8\uC9C0 320/\uBC30\uCE58 4\uB85C \uB0AE\uCD98 \uB4A4 \uB2E4\uC2DC \uD559\uC2B5\uD558\uC138\uC694."
            };
        }

        public static string BuildFailureDetail(string message)
        {
            string formattedMessage = FormatTrainingMessage(message);
            if (string.IsNullOrWhiteSpace(formattedMessage))
            {
                formattedMessage = "Python worker\uAC00 \uC2E4\uD328 \uC0C1\uD0DC\uB97C \uBCF4\uB0C8\uC9C0\uB9CC \uC0C1\uC138 \uBA54\uC2DC\uC9C0\uB97C \uC81C\uACF5\uD558\uC9C0 \uC54A\uC558\uC2B5\uB2C8\uB2E4.";
            }

            return "\uC6D0\uC778: " + formattedMessage;
        }

        public static string BuildFailureDetail(PythonCommunicationStatus status)
        {
            return BuildFailureDetail(ResolveTrainingMessage(status));
        }

        public static string BuildProgressSummary(PythonCommunicationStatus status)
        {
            if (!HasTrainingStatus(status))
            {
                return BuildIdleProgressText();
            }

            List<string> parts = new List<string>
            {
                $"\uD559\uC2B5 {FormatTrainingState(status.LastTrainingState)}"
            };
            if (status.LastTrainingProgressPercent.HasValue)
            {
                parts.Add($"{Math.Clamp(status.LastTrainingProgressPercent.Value, 0, 100)}%");
            }

            string message = ResolveTrainingMessage(status);
            if (!string.IsNullOrWhiteSpace(message))
            {
                parts.Add(FormatTrainingMessage(message));
            }

            string trainingWeight = FormatTrainingWeight(status.LastTrainingWeightsPath);
            if (!string.IsNullOrWhiteSpace(trainingWeight))
            {
                parts.Add($"\uD559\uC2B5 weight {trainingWeight}");
            }

            return string.Join(" / ", parts);
        }

        public static string BuildEpochSummary(PythonCommunicationStatus status, bool isLiveTrainingStatus)
        {
            if (isLiveTrainingStatus && status?.LastTrainingEpoch.HasValue != true)
            {
                return BuildBeforeEpochText();
            }

            if (status?.LastTrainingEpoch.HasValue == true && status.LastTrainingTotalEpochs.HasValue)
            {
                return $"\uC5D0\uD3ED {status.LastTrainingEpoch.Value}/{status.LastTrainingTotalEpochs.Value}";
            }

            if (status?.LastTrainingEpoch.HasValue == true)
            {
                return $"\uC5D0\uD3ED {status.LastTrainingEpoch.Value}";
            }

            return string.Empty;
        }

        public static string FormatTrainingState(string state)
        {
            string normalized = state?.Trim() ?? string.Empty;
            return normalized.ToLowerInvariant() switch
            {
                "" => "\uC0C1\uD0DC \uBBF8\uD655\uC778",
                "idle" => "\uB300\uAE30",
                "starting" => "\uC2DC\uC791 \uC911",
                "started" => "\uBA85\uB839 \uC218\uB77D\uB428",
                "running" => "\uC9C4\uD589 \uC911",
                "completed" => "\uC644\uB8CC",
                "stopped" => "\uC911\uC9C0\uB428",
                "failed" => "\uC2E4\uD328",
                "error" => "\uC624\uB958",
                _ => normalized
            };
        }

        public static string FormatTrainingMessage(string message)
        {
            string normalized = message?.Trim() ?? string.Empty;
            if (normalized.Contains(TrainingWeightDownloadRequiredCode, StringComparison.OrdinalIgnoreCase))
            {
                return "\uD559\uC2B5 weight \uC900\uBE44 \uD544\uC694: \uBAA8\uB378 weight \uD30C\uC77C\uC744 \uCE90\uC2DC\uC5D0 \uCD94\uAC00\uD558\uAC70\uB098 \uBA85\uC2DC\uC801\uC73C\uB85C \uB2E4\uC6B4\uB85C\uB4DC\uB97C \uC2B9\uC778\uD558\uC138\uC694.";
            }

            if (normalized.StartsWith("Training failed: process exited at the first training batch", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("Training failed: process exited near the first training batch", StringComparison.OrdinalIgnoreCase))
            {
                return "\uD559\uC2B5 \uC2E4\uD328: CPU\uC5D0\uC11C \uD604\uC7AC \uC124\uC815\uC774 \uB108\uBB34 \uD07D\uB2C8\uB2E4. yolov5s / \uC774\uBBF8\uC9C0 320 / \uBC30\uCE58 4 \uAD8C\uC7A5";
            }

            return normalized.ToLowerInvariant() switch
            {
                "epoch" => "\uC5D0\uD3ED",
                "epoch update" => "\uC5D0\uD3ED \uAC31\uC2E0",
                "yolo training accepted by worker." => "\uC6CC\uCEE4 \uC218\uB77D, \uC5D0\uD3ED \uC2DC\uC791 \uC804",
                "yolov5 training started." => "\uC6CC\uCEE4 \uC2DC\uC791",
                "yolov5 training completed." => "\uD559\uC2B5 \uC644\uB8CC",
                "yolov5 training failed." => "\uD559\uC2B5 \uC2E4\uD328",
                "yolov5 training stopped." => "\uD559\uC2B5 \uC911\uC9C0\uB428",
                _ => normalized
            };
        }

        private static string ResolveTrainingMessage(PythonCommunicationStatus status)
        {
            if (status == null)
            {
                return string.Empty;
            }

            string state = status.LastTrainingState?.Trim() ?? string.Empty;
            bool failed = string.Equals(state, "failed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "error", StringComparison.OrdinalIgnoreCase);
            if (failed && !string.IsNullOrWhiteSpace(status.LastError))
            {
                return status.LastError;
            }

            return status.LastTrainingMessage ?? string.Empty;
        }

        private static string FormatTrainingWeight(string path)
        {
            string normalized = path?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            try
            {
                string fileName = Path.GetFileName(normalized);
                return string.IsNullOrWhiteSpace(fileName) ? normalized : fileName;
            }
            catch (ArgumentException)
            {
                return normalized;
            }
        }

        private static bool HasTrainingStatus(PythonCommunicationStatus status)
        {
            return status != null
                && (!string.IsNullOrWhiteSpace(status.LastTrainingState)
                    || !string.IsNullOrWhiteSpace(status.LastTrainingMessage)
                    || status.LastTrainingProgressPercent.HasValue
                    || status.LastTrainingEpoch.HasValue
                    || status.LastTrainingTotalEpochs.HasValue);
        }

        private static string NormalizeDetail(string detail)
        {
            return string.IsNullOrWhiteSpace(detail)
                ? "\uC0C1\uC138 \uC6D0\uC778\uC744 \uD655\uC778\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4."
                : detail.Trim();
        }
    }
}
