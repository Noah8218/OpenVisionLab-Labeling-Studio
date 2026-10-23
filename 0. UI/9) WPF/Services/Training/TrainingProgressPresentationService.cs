using MvcVisionSystem._3._Communication.TCP;
using System;
using System.Collections.Generic;
using System.IO;

namespace MvcVisionSystem
{
    public static class TrainingProgressPresentationService
    {
        private const string TrainingWeightDownloadRequiredCode = "TrainingWeightDownloadRequired";

        public static string BuildAcceptedWorkerWaitProgressText()
        {
            return OpenVisionLab.OpenVisionLanguageService.T("WpfTraining.Remaining.TrainingProgressPresentationService.Accepted");
        }

        public static string BuildBeforeEpochText()
        {
            return OpenVisionLab.OpenVisionLanguageService.T("WpfTraining.Remaining.TrainingProgressPresentationService.BeforeEpoch");
        }

        public static string BuildIdleProgressText()
        {
            return OpenVisionLab.OpenVisionLanguageService.T("WpfTraining.Remaining.TrainingProgressPresentationService.Idle");
        }

        public static string BuildStatusNoResponseText()
        {
            return OpenVisionLab.OpenVisionLanguageService.T("WpfTraining.Remaining.TrainingProgressPresentationService.NoResponse");
        }

        public static TrainingRecoveryStatus BuildStatusNoResponseRecovery(string detail)
        {
            return new TrainingRecoveryStatus
            {
                Title = OpenVisionLab.OpenVisionLanguageService.T("WpfTraining.Remaining.TrainingProgressPresentationService.NoResponseTitle"),
                Detail = NormalizeDetail(detail),
                Action = OpenVisionLab.OpenVisionLanguageService.T("WpfTraining.Remaining.TrainingProgressPresentationService.NoResponseAction")
            };
        }

        public static TrainingRecoveryStatus BuildFailedRecovery(string detail)
        {
            return new TrainingRecoveryStatus
            {
                Title = OpenVisionLab.OpenVisionLanguageService.T("WpfTraining.Remaining.TrainingProgressPresentationService.FailedTitle"),
                Detail = NormalizeDetail(detail),
                Action = OpenVisionLab.OpenVisionLanguageService.T("WpfTraining.Remaining.TrainingProgressPresentationService.FailedAction")
            };
        }

        public static string BuildFailureDetail(string message)
        {
            string formattedMessage = FormatTrainingMessage(message);
            if (string.IsNullOrWhiteSpace(formattedMessage))
            {
                formattedMessage = OpenVisionLab.OpenVisionLanguageService.T("WpfTraining.Remaining.TrainingProgressPresentationService.FailureNoDetail");
            }

            return OpenVisionLab.OpenVisionLanguageService.T("WpfTraining.Remaining.TrainingProgressPresentationService.CausePrefix") + " " + formattedMessage;
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
                string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfTraining.Remaining.TrainingProgressPresentationService.ProgressState"), FormatTrainingState(status.LastTrainingState))
            };
            if (status.LastTrainingProgressPercent.HasValue)
            {
                parts.Add($"{Math.Clamp(status.LastTrainingProgressPercent.Value, 0, 100)}%");
            }

            bool isLiveTrainingStatus = IsLiveTrainingStatus(status);
            if (isLiveTrainingStatus && !status.LastTrainingEpoch.HasValue)
            {
                parts.Add(BuildEpochSummary(status, isLiveTrainingStatus));
            }

            string message = ResolveTrainingMessage(status);
            if (!string.IsNullOrWhiteSpace(message))
            {
                parts.Add(FormatTrainingMessage(message));
            }

            string trainingWeight = FormatTrainingWeight(status.LastTrainingWeightsPath);
            if (!string.IsNullOrWhiteSpace(trainingWeight))
            {
                parts.Add(string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfTraining.Remaining.TrainingProgressPresentationService.ProgressWeight"), trainingWeight));
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
                return string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfTraining.Remaining.TrainingProgressPresentationService.EpochPair"), status.LastTrainingEpoch.Value, status.LastTrainingTotalEpochs.Value);
            }

            if (status?.LastTrainingEpoch.HasValue == true)
            {
                return string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfTraining.Remaining.TrainingProgressPresentationService.EpochSingle"), status.LastTrainingEpoch.Value);
            }

            return string.Empty;
        }

        public static bool HasTrainingStatus(PythonCommunicationStatus status)
        {
            return status != null
                && (!string.IsNullOrWhiteSpace(status.LastTrainingState)
                    || !string.IsNullOrWhiteSpace(status.LastTrainingMessage)
                    || status.LastTrainingProgressPercent.HasValue
                    || status.LastTrainingEpoch.HasValue
                    || status.LastTrainingTotalEpochs.HasValue);
        }

        public static TrainingRecoveryStatus ResolveRecoveryStatus(PythonCommunicationStatus status)
        {
            if (!HasTrainingStatus(status))
            {
                return null;
            }

            string state = status.LastTrainingState?.Trim() ?? string.Empty;
            if (string.Equals(state, "failed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "error", StringComparison.OrdinalIgnoreCase))
            {
                return BuildFailedRecovery(BuildFailureDetail(status));
            }

            return null;
        }

        public static bool ShouldClearRecoveryStatus(PythonCommunicationStatus status)
        {
            if (!HasTrainingStatus(status))
            {
                return false;
            }

            string state = status.LastTrainingState?.Trim() ?? string.Empty;
            return TrainingWeightsService.IsCompletedTrainingState(state)
                || IsLiveTrainingStatus(status)
                || string.Equals(state, "started", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "running", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsTerminalTrainingState(string state)
        {
            return string.Equals(state?.Trim(), "idle", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state?.Trim(), "completed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state?.Trim(), "stopped", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state?.Trim(), "failed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state?.Trim(), "error", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsLiveTrainingStatus(PythonCommunicationStatus status)
        {
            if (!HasTrainingStatus(status))
            {
                return false;
            }

            string state = status.LastTrainingState?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(state))
            {
                return !IsTerminalTrainingState(state);
            }

            return status.LastTrainingProgressPercent.HasValue
                && status.LastTrainingProgressPercent.Value > 0
                && status.LastTrainingProgressPercent.Value < 100;
        }

        public static bool IsTrainingStopAvailable(PythonCommunicationStatus status)
        {
            if (status == null)
            {
                return false;
            }

            string state = status.LastTrainingState?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(state))
            {
                return status.LastTrainingProgressPercent.HasValue
                    && status.LastTrainingProgressPercent.Value > 0
                    && status.LastTrainingProgressPercent.Value < 100;
            }

            return !IsTerminalTrainingState(state);
        }

        public static string GetTrainingStateBrushResourceKey(PythonCommunicationStatus status)
        {
            if (!HasTrainingStatus(status))
            {
                return "SecondaryTextBrush";
            }

            string state = status.LastTrainingState?.Trim() ?? string.Empty;
            if (TrainingWeightsService.IsCompletedTrainingState(state))
            {
                return "SuccessBrush";
            }

            if (string.Equals(state, "failed", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state, "error", StringComparison.OrdinalIgnoreCase))
            {
                return "ErrorBrush";
            }

            if (string.Equals(state, "stopped", StringComparison.OrdinalIgnoreCase))
            {
                return "WarningBrush";
            }

            return !IsTerminalTrainingState(state) || status.LastTrainingProgressPercent.HasValue
                ? "InfoBrush"
                : "SecondaryTextBrush";
        }

        public static string FormatTrainingState(string state)
        {
            string normalized = state?.Trim() ?? string.Empty;
            return normalized.ToLowerInvariant() switch
            {
                "" => OpenVisionLab.OpenVisionLanguageService.T("WpfTraining.Remaining.TrainingProgressPresentationService.State.239"),
                "idle" => OpenVisionLab.OpenVisionLanguageService.T("WpfTraining.Remaining.TrainingProgressPresentationService.State.240"),
                "starting" => OpenVisionLab.OpenVisionLanguageService.T("WpfTraining.Remaining.TrainingProgressPresentationService.State.241"),
                "started" => OpenVisionLab.OpenVisionLanguageService.T("WpfTraining.Remaining.TrainingProgressPresentationService.State.242"),
                "running" => OpenVisionLab.OpenVisionLanguageService.T("WpfTraining.Remaining.TrainingProgressPresentationService.State.243"),
                "completed" => OpenVisionLab.OpenVisionLanguageService.T("WpfTraining.Remaining.TrainingProgressPresentationService.State.244"),
                "stopped" => OpenVisionLab.OpenVisionLanguageService.T("WpfTraining.Remaining.TrainingProgressPresentationService.State.245"),
                "failed" => OpenVisionLab.OpenVisionLanguageService.T("WpfTraining.Remaining.TrainingProgressPresentationService.State.246"),
                "error" => OpenVisionLab.OpenVisionLanguageService.T("WpfTraining.Remaining.TrainingProgressPresentationService.State.247"),
                _ => normalized
            };
        }

        public static string FormatTrainingMessage(string message)
        {
            string normalized = message?.Trim() ?? string.Empty;
            if (normalized.Contains(TrainingWeightDownloadRequiredCode, StringComparison.OrdinalIgnoreCase))
            {
                return OpenVisionLab.OpenVisionLanguageService.T("WpfTraining.Remaining.TrainingProgressPresentationService.Message.257");
            }

            if (normalized.StartsWith("Training failed: process exited at the first training batch", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("Training failed: process exited near the first training batch", StringComparison.OrdinalIgnoreCase))
            {
                return OpenVisionLab.OpenVisionLanguageService.T("WpfTraining.Remaining.TrainingProgressPresentationService.Message.263");
            }

            return normalized.ToLowerInvariant() switch
            {
                "epoch" => OpenVisionLab.OpenVisionLanguageService.T("WpfTraining.Remaining.TrainingProgressPresentationService.Message.268"),
                "epoch update" => OpenVisionLab.OpenVisionLanguageService.T("WpfTraining.Remaining.TrainingProgressPresentationService.Message.269"),
                "yolo training accepted by worker." => OpenVisionLab.OpenVisionLanguageService.T("WpfTraining.Remaining.TrainingProgressPresentationService.Message.270"),
                "yolov5 training started." => OpenVisionLab.OpenVisionLanguageService.T("WpfTraining.Remaining.TrainingProgressPresentationService.Message.271"),
                "yolov5 training completed." => OpenVisionLab.OpenVisionLanguageService.T("WpfTraining.Remaining.TrainingProgressPresentationService.Message.272"),
                "yolov5 training failed." => OpenVisionLab.OpenVisionLanguageService.T("WpfTraining.Remaining.TrainingProgressPresentationService.Message.273"),
                "yolov5 training stopped." => OpenVisionLab.OpenVisionLanguageService.T("WpfTraining.Remaining.TrainingProgressPresentationService.Message.274"),
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

        private static string NormalizeDetail(string detail)
        {
            return string.IsNullOrWhiteSpace(detail)
                ? "\uC0C1\uC138 \uC6D0\uC778\uC744 \uD655\uC778\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4."
                : detail.Trim();
        }
    }

    [Obsolete("Use TrainingProgressPresentationService.", false)]
    public static class WpfTrainingProgressPresentationService
    {
        public static string BuildAcceptedWorkerWaitProgressText()
            => TrainingProgressPresentationService.BuildAcceptedWorkerWaitProgressText();

        public static string BuildBeforeEpochText()
            => TrainingProgressPresentationService.BuildBeforeEpochText();

        public static string BuildIdleProgressText()
            => TrainingProgressPresentationService.BuildIdleProgressText();

        public static string BuildStatusNoResponseText()
            => TrainingProgressPresentationService.BuildStatusNoResponseText();

        public static WpfTrainingRecoveryStatus BuildStatusNoResponseRecovery(string detail)
            => ToLegacy(TrainingProgressPresentationService.BuildStatusNoResponseRecovery(detail));

        public static WpfTrainingRecoveryStatus BuildFailedRecovery(string detail)
            => ToLegacy(TrainingProgressPresentationService.BuildFailedRecovery(detail));

        public static string BuildFailureDetail(string message)
            => TrainingProgressPresentationService.BuildFailureDetail(message);

        public static string BuildFailureDetail(PythonCommunicationStatus status)
            => TrainingProgressPresentationService.BuildFailureDetail(status);

        public static string BuildProgressSummary(PythonCommunicationStatus status)
            => TrainingProgressPresentationService.BuildProgressSummary(status);

        public static string BuildEpochSummary(PythonCommunicationStatus status, bool isLiveTrainingStatus)
            => TrainingProgressPresentationService.BuildEpochSummary(status, isLiveTrainingStatus);

        public static bool HasTrainingStatus(PythonCommunicationStatus status)
            => TrainingProgressPresentationService.HasTrainingStatus(status);

        public static WpfTrainingRecoveryStatus ResolveRecoveryStatus(PythonCommunicationStatus status)
            => ToLegacy(TrainingProgressPresentationService.ResolveRecoveryStatus(status));

        public static bool ShouldClearRecoveryStatus(PythonCommunicationStatus status)
            => TrainingProgressPresentationService.ShouldClearRecoveryStatus(status);

        public static bool IsTerminalTrainingState(string state)
            => TrainingProgressPresentationService.IsTerminalTrainingState(state);

        public static bool IsLiveTrainingStatus(PythonCommunicationStatus status)
            => TrainingProgressPresentationService.IsLiveTrainingStatus(status);

        public static bool IsTrainingStopAvailable(PythonCommunicationStatus status)
            => TrainingProgressPresentationService.IsTrainingStopAvailable(status);

        public static string GetTrainingStateBrushResourceKey(PythonCommunicationStatus status)
            => TrainingProgressPresentationService.GetTrainingStateBrushResourceKey(status);

        public static string FormatTrainingState(string state)
            => TrainingProgressPresentationService.FormatTrainingState(state);

        public static string FormatTrainingMessage(string message)
            => TrainingProgressPresentationService.FormatTrainingMessage(message);

        private static WpfTrainingRecoveryStatus ToLegacy(TrainingRecoveryStatus status)
        {
            if (status == null)
            {
                return null;
            }

            return new WpfTrainingRecoveryStatus
            {
                Title = status.Title,
                Detail = status.Detail,
                Action = status.Action
            };
        }
    }
}
