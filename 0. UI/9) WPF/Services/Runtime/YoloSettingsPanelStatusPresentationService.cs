using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace MvcVisionSystem
{
    public static class YoloSettingsPanelStatusPresentationService
    {
        private static readonly OpenVisionLab.OpenVisionLocalizationEntry MissingRuntimeDetailDefault = OpenVisionLab.OpenVisionLanguageService.GetDefaultEntries()
            .FirstOrDefault(entry => string.Equals(entry.Key, "WpfModelDetails.Runtime.203", StringComparison.Ordinal));

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
            detail.AppendLine(string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloSettingsPanelStatusPresentationService.Executable"), PythonModelSettingsValidator.ResolvePythonExecutable(settings)));
            detail.AppendLine(string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloSettingsPanelStatusPresentationService.Project"), settings.ProjectRootPath));
            detail.AppendLine(string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloSettingsPanelStatusPresentationService.Script"), settings.ClientScriptPath));
            detail.AppendLine(string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloSettingsPanelStatusPresentationService.ModelFile"), settings.WeightsPath));
            detail.AppendLine(string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloSettingsPanelStatusPresentationService.Image"), settings.ImageRootPath));
            detail.AppendLine(string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloSettingsPanelStatusPresentationService.Requirements"), settings.GetRequirementsPath()));
            detail.AppendLine(string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloSettingsPanelStatusPresentationService.Confidence"), settings.MinimumDetectionConfidence.ToString("0.##", CultureInfo.InvariantCulture)));
            detail.AppendLine(string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloSettingsPanelStatusPresentationService.Timeout"), settings.DetectionTimeoutSeconds));
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
                detail.AppendLine(string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloSettingsPanelStatusPresentationService.Error"), NormalizeDisplayText(error)));
            }

            foreach (string warning in validation.Warnings)
            {
                detail.AppendLine(string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloSettingsPanelStatusPresentationService.Warning"), NormalizeDisplayText(warning)));
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
                detail.AppendLine(OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeFollowOn.YoloSettingsPanelStatusPresentationService.68"));
                const string detailKey = "WpfModelDetails.Runtime.203";
                string runtimeDetail = NormalizeDisplayText(runtimeState?.DetailText);
                var entries = OpenVisionLab.OpenVisionLanguageService.GetEntries();
                OpenVisionLab.OpenVisionLocalizationEntry entry = entries.FirstOrDefault(item => string.Equals(item.Key, detailKey, StringComparison.Ordinal));
                if (entry != null
                    && (string.Equals(runtimeDetail, entry.Korean, StringComparison.Ordinal)
                        || string.Equals(runtimeDetail, entry.English, StringComparison.Ordinal)
                        || OpenVisionLab.OpenVisionLanguageService.GetPreviousKoreanDefaults(detailKey).Contains(runtimeDetail, StringComparer.Ordinal)
                        || string.Equals(runtimeDetail, MissingRuntimeDetailDefault?.Korean, StringComparison.Ordinal)
                        || string.Equals(runtimeDetail, MissingRuntimeDetailDefault?.English, StringComparison.Ordinal)))
                {
                    string translated = OpenVisionLab.OpenVisionLanguageService.CurrentLanguage == OpenVisionLab.OpenVisionLanguage.Korean ? entry.Korean : entry.English;
                    if (!string.IsNullOrWhiteSpace(translated)) runtimeDetail = translated;
                }
                detail.AppendLine(runtimeDetail);
                return;
            }

            if (environment != null)
            {
                detail.AppendLine(string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloSettingsPanelStatusPresentationService.Package"), BuildPythonEnvironmentSummary(environment)));
                detail.AppendLine(string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloSettingsPanelStatusPresentationService.RequiredPackages"), Math.Max(0, environment.RequiredPackages?.Count ?? 0)));
                if ((environment.MissingPackages?.Count ?? 0) > 0)
                {
                    detail.AppendLine(string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloSettingsPanelStatusPresentationService.Missing"), string.Join(", ", environment.MissingPackages.Take(12))));
                }

                return;
            }

            if (!string.IsNullOrWhiteSpace(environmentCheckError))
            {
                detail.AppendLine(string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloSettingsPanelStatusPresentationService.PackageCheckFailed"), NormalizeDisplayText(environmentCheckError)));
            }
        }

        private static void AppendPythonWorkerStatus(
            StringBuilder detail,
            PythonCommunicationStatus status,
            bool pythonClientProcessRunning)
        {
            detail.AppendLine(
                string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeFollowOn.YoloSettingsPanelStatusPresentationService.97"), FormatBooleanStatus(status.IsListening, OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloSettingsPanelStatusPresentationService.ListeningOn"), OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloSettingsPanelStatusPresentationService.ListeningOff")), FormatBooleanStatus(status.IsClientConnected, OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloSettingsPanelStatusPresentationService.ClientConnected"), OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloSettingsPanelStatusPresentationService.ClientDisconnected")), FormatBooleanStatus(pythonClientProcessRunning, OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloSettingsPanelStatusPresentationService.ProcessRunning"), OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloSettingsPanelStatusPresentationService.ProcessStopped"))));
            if (status.ListenerPort > 0)
            {
                detail.AppendLine(string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloSettingsPanelStatusPresentationService.Port"), status.ListenerPort));
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
                    string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloSettingsPanelStatusPresentationService.CachedWeights"), FormatModelList(status.WorkerCachedTrainingWeights), FormatModelList(status.WorkerMissingTrainingWeights)));
                detail.AppendLine(
                    string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeFollowOn.YoloSettingsPanelStatusPresentationService.115"), FormatModelList(status.WorkerRuntimeReadyTrainingWeights), FormatModelList(status.WorkerRuntimeBlockedTrainingWeights)));
                detail.AppendLine(
                    string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeFollowOn.YoloSettingsPanelStatusPresentationService.117"), FormatModelList(status.WorkerDownloadRequiredTrainingWeights), FormatModelList(status.WorkerRuntimeBlockedMissingTrainingWeights)));
            }

            if (!string.IsNullOrWhiteSpace(status.LastWorkerState)
                || !string.IsNullOrWhiteSpace(status.LastWorkerMessage))
            {
                detail.AppendLine(string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloSettingsPanelStatusPresentationService.InferenceStatus"), FormatWorkerState(status.LastWorkerState), TranslateWorkerMessage(status.LastWorkerMessage)).TrimEnd());
            }

            if (!string.IsNullOrWhiteSpace(status.LastModelState)
                || !string.IsNullOrWhiteSpace(status.LastModelMessage))
            {
                string modelLoadedText = FormatBooleanStatus(status.LastModelLoaded, OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloSettingsPanelStatusPresentationService.ModelLoadedYes"), OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloSettingsPanelStatusPresentationService.ModelLoadedNo"));
                detail.AppendLine(string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloSettingsPanelStatusPresentationService.ModelStatus"), FirstNonEmpty(status.LastModelState, "-"), modelLoadedText, NormalizeOptionalText(status.LastModelMessage)).TrimEnd());
            }

            if (!string.IsNullOrWhiteSpace(status.LastTrainingState)
                || status.LastTrainingProgressPercent.HasValue)
            {
                string progress = status.LastTrainingProgressPercent.HasValue
                    ? $"{status.LastTrainingProgressPercent.Value}%"
                    : "-";
                string epoch = status.LastTrainingEpoch.HasValue && status.LastTrainingTotalEpochs.HasValue
                    ? string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloSettingsPanelStatusPresentationService.Epoch"), status.LastTrainingEpoch.Value, status.LastTrainingTotalEpochs.Value)
                    : string.Empty;
                string trainingMessage = string.IsNullOrWhiteSpace(status.LastTrainingMessage)
                    ? string.Empty
                    : TrainingProgressPresentationService.FormatTrainingMessage(status.LastTrainingMessage);
                detail.AppendLine(string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloSettingsPanelStatusPresentationService.Training"), TrainingProgressPresentationService.FormatTrainingState(status.LastTrainingState), progress, epoch, trainingMessage).TrimEnd());
            }

            if (!string.IsNullOrWhiteSpace(status.LastTrainingWeightsPath))
            {
                detail.AppendLine(string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloSettingsPanelStatusPresentationService.TrainingWeights"), NormalizeOptionalText(status.LastTrainingWeightsPath)));
            }

            if (!string.IsNullOrWhiteSpace(status.LastError))
            {
                detail.AppendLine(string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloSettingsPanelStatusPresentationService.ExecutionError"), FormatWorkerError(status.LastError)));
            }

            if ((status.WorkerRuntimeWarnings?.Count ?? 0) > 0)
            {
                foreach (string warning in status.WorkerRuntimeWarnings.Take(5))
                {
                    detail.AppendLine(string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloSettingsPanelStatusPresentationService.InferenceWarning"), NormalizeDisplayText(warning)));
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
                return string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloSettingsPanelStatusPresentationService.MissingPackages"), string.Join(", ", environment.MissingPackages.Take(6)));
            }

            if (environment.IsReady)
            {
                return OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloSettingsPanelStatusPresentationService.EnvironmentReady");
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
                "listening" => OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloSettingsPanelStatusPresentationService.WorkerListening"),
                "connected" => OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloSettingsPanelStatusPresentationService.WorkerConnected"),
                "running" => OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloSettingsPanelStatusPresentationService.WorkerRunning"),
                "stopped" => OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloSettingsPanelStatusPresentationService.WorkerStopped"),
                "error" => OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloSettingsPanelStatusPresentationService.WorkerError"),
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
                "Python TCP listener is waiting for a client." => OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeFollowOn.YoloSettingsPanelStatusPresentationService.231"),
                "Python TCP listener stopped." => OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeFollowOn.YoloSettingsPanelStatusPresentationService.232"),
                _ => message.Trim()
            };
        }

        private static string FormatBooleanStatus(bool value, string trueText, string falseText)
        {
            return value ? trueText : falseText;
        }

        private static string NormalizeDisplayText(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeCompanion.YoloSettingsPanelStatusPresentationService.NoDetail") : text.Trim();
        }

        private static string FormatWorkerError(string text)
        {
            string normalized = NormalizeDisplayText(text);
            return normalized.Contains("TrainingWeightDownloadRequired", StringComparison.OrdinalIgnoreCase)
                ? OpenVisionLab.OpenVisionLanguageService.T("WpfModelDetails.RuntimeFollowOn.YoloSettingsPanelStatusPresentationService.251")
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

    [Obsolete("Use YoloSettingsPanelStatusPresentationService.", false)]
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
            => YoloSettingsPanelStatusPresentationService.BuildDetail(
                settings,
                validation,
                runtimeState,
                communicationStatus,
                pythonClientProcessRunning,
                environment,
                environmentCheckError);
    }
}
