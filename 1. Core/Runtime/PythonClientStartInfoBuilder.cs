using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace MvcVisionSystem._1._Core
{
    internal static class PythonClientStartInfoBuilder
    {
        internal static bool TryCreateStartInfo(PythonModelSettings settings, out ProcessStartInfo startInfo, out string error)
        {
            startInfo = null;
            error = "";

            settings ??= new PythonModelSettings();
            PythonModelRuntimePathResolver.ApplyDefaults(settings);

            PythonModelValidationResult validation = PythonModelSettingsValidator.Validate(settings, requireWeights: false);
            if (!validation.IsValid)
            {
                error = validation.Errors.FirstOrDefault() ?? "Python model client settings are invalid.";
                return false;
            }

            PythonModelRuntimeLockPreflightResult lockPreflight =
                PythonModelRuntimeLockManifestService.ValidateIfConfigured(settings);
            if (!lockPreflight.IsValid)
            {
                error = lockPreflight.Summary;
                return false;
            }

            string projectRootPath = settings.ProjectRootPath?.Trim() ?? "";
            string clientScriptPath = settings.ClientScriptPath?.Trim() ?? "";
            string pythonExecutablePath = PythonModelSettingsValidator.ResolvePythonExecutable(settings);

            startInfo = new ProcessStartInfo
            {
                FileName = pythonExecutablePath,
                WorkingDirectory = projectRootPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false),
                StandardErrorEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false),
                WindowStyle = ProcessWindowStyle.Hidden
            };
            startInfo.ArgumentList.Add(clientScriptPath);
            startInfo.ArgumentList.Add("--retry");
            startInfo.ArgumentList.Add("--preload");

            string modelRootPath = settings.GetModelRootPath();
            if (Directory.Exists(modelRootPath))
            {
                startInfo.ArgumentList.Add("--model-root");
                startInfo.ArgumentList.Add(modelRootPath);
            }

            string weightsPath = settings.WeightsPath?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(weightsPath))
            {
                startInfo.ArgumentList.Add("--weights");
                startInfo.ArgumentList.Add(weightsPath);
            }

            string imageRootPath = settings.ImageRootPath?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(imageRootPath))
            {
                startInfo.ArgumentList.Add("--image-root");
                startInfo.ArgumentList.Add(imageRootPath);
            }

            startInfo.ArgumentList.Add("--conf");
            startInfo.ArgumentList.Add(settings.MinimumDetectionConfidence.ToString(CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add("--img-size");
            startInfo.ArgumentList.Add(settings.InferenceImageSize.ToString(CultureInfo.InvariantCulture));

            return true;
        }

        internal static bool TryCreateStartSignature(PythonModelSettings settings, out string startSignature, out string error)
        {
            startSignature = "";
            if (!TryCreateStartInfo(settings, out ProcessStartInfo startInfo, out error))
            {
                return false;
            }

            startSignature = CreateStartSignature(startInfo);
            return true;
        }

        internal static string CreateStartSignature(ProcessStartInfo startInfo)
        {
            if (startInfo == null)
            {
                return string.Empty;
            }

            string arguments = string.Join("\u001F", startInfo.ArgumentList.Select(item => item ?? string.Empty));
            return string.Join(
                "\u001E",
                startInfo.FileName ?? string.Empty,
                startInfo.WorkingDirectory ?? string.Empty,
                arguments);
        }
    }
}
