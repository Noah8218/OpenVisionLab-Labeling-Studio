using Lib.Common;
using MvcVisionSystem._3._Communication.TCP;
using System;
using System.Drawing;
using System.IO;
using System.Linq;

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
            if (!ValidatePythonModelSettings(data, out string validationError))
            {
                communication?.SetLastError(validationError);
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

        public bool TryStartImagePathDetection(
            CData data,
            CCommunicationLearning communication,
            DetectionResultApplicationService detectionResults,
            string imagePath,
            Size imageSize,
            Func<bool> ensurePythonClientReady)
        {
            if (!ValidatePythonModelSettings(data, out string validationError))
            {
                communication?.SetLastError(validationError);
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

            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                communication.SetLastError($"YOLO detection image was not found: {imagePath}");
                return false;
            }

            if (imageSize.IsEmpty)
            {
                communication.SetLastError($"YOLO detection image size is empty: {imagePath}");
                return false;
            }

            if (ensurePythonClientReady == null || !ensurePythonClientReady())
            {
                return false;
            }

            int timeoutSeconds = data.ProjectSettings?.PythonModel?.DetectionTimeoutSeconds ?? 30;
            return detectionResults.TrySendImagePathForDetection(
                communication,
                data,
                imagePath,
                imageSize,
                timeoutSeconds);
        }

        private static bool ValidatePythonModelSettings(CData data, out string validationError)
        {
            validationError = "";
            if (data == null)
            {
                validationError = "YOLO detection data is not initialized.";
                AppLog.ABNORMAL(validationError);
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

            validationError = validation.Errors.FirstOrDefault() ?? "";
            return validation.IsValid;
        }
    }
}
