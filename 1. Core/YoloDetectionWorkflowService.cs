using Lib.Common;
using MvcVisionSystem._3._Communication.TCP;
using System;

namespace MvcVisionSystem._1._Core
{
    public sealed class YoloDetectionWorkflowService
    {
        public bool TryStartCurrentImageDetection(
            CData data,
            CCommunicationLearning communication,
            DetectionResultApplicationService detectionResults,
            Func<bool> ensurePythonClientReady)
        {
            if (!ValidatePythonModelSettings(data))
            {
                return false;
            }

            if (communication == null)
            {
                AppLog.ABNORMAL("YOLO detection communication is not initialized.");
                return false;
            }

            if (detectionResults == null)
            {
                AppLog.ABNORMAL("YOLO detection result service is not initialized.");
                return false;
            }

            if (ensurePythonClientReady == null || !ensurePythonClientReady())
            {
                return false;
            }

            int timeoutSeconds = data.ProjectSettings?.PythonModel?.DetectionTimeoutSeconds ?? 30;
            return detectionResults.TrySendCurrentImageForDetection(communication, timeoutSeconds);
        }

        private static bool ValidatePythonModelSettings(CData data)
        {
            if (data == null)
            {
                AppLog.ABNORMAL("YOLO detection data is not initialized.");
                return false;
            }

            data.ProjectSettings ??= new LabelingProjectSettings();
            data.ProjectSettings.EnsureDefaults();

            PythonModelValidationResult validation = PythonModelSettingsValidator.Validate(
                data.ProjectSettings.PythonModel,
                requireWeights: true);

            foreach (string warning in validation.Warnings)
            {
                AppLog.COMM(warning);
            }

            foreach (string error in validation.Errors)
            {
                AppLog.ABNORMAL(error);
            }

            return validation.IsValid;
        }
    }
}
