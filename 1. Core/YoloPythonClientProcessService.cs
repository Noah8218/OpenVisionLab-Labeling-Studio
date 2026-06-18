using Lib.Common;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MvcVisionSystem._1._Core
{
    public sealed class YoloPythonClientProcessService : IDisposable
    {
        private readonly object sync = new object();
        private Process process;
        private string currentStartSignature = "";
        private volatile bool stopRequested;

        public string LastError { get; private set; } = "";
        public DateTime? LastStartedAtUtc { get; private set; }
        public DateTime? LastExitedAtUtc { get; private set; }
        public int? LastExitCode { get; private set; }

        public bool IsRunning
        {
            get
            {
                lock (sync)
                {
                    return process != null && !process.HasExited;
                }
            }
        }

        public int? ProcessId
        {
            get
            {
                lock (sync)
                {
                    if (process == null || process.HasExited)
                    {
                        return null;
                    }

                    return process.Id;
                }
            }
        }

        public bool EnsureStarted(PythonModelSettings settings)
        {
            lock (sync)
            {
                if (!TryCreateStartInfo(settings, out ProcessStartInfo startInfo, out string error))
                {
                    LastError = error;
                    AppLog.ABNORMAL(error);
                    return false;
                }

                string startSignature = CreateStartSignature(startInfo);
                if (IsRunning && string.Equals(currentStartSignature, startSignature, StringComparison.Ordinal))
                {
                    return true;
                }

                if (IsRunning)
                {
                    AppLog.COMM("YOLOv5 Python client settings changed. Restarting client process.");
                    StopLocked();
                }

                try
                {
                    process?.Dispose();
                    process = new Process
                    {
                        StartInfo = startInfo,
                        EnableRaisingEvents = true
                    };
                    process.OutputDataReceived += OnOutputDataReceived;
                    process.ErrorDataReceived += OnErrorDataReceived;
                    process.Exited += OnExited;

                    if (!process.Start())
                    {
                        LastError = "YOLOv5 Python client process did not start.";
                        AppLog.ABNORMAL(LastError);
                        process.Dispose();
                        process = null;
                        return false;
                    }

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    LastStartedAtUtc = DateTime.UtcNow;
                    LastError = "";
                    LastExitCode = null;
                    stopRequested = false;
                    currentStartSignature = startSignature;
                    AppLog.COMM($"YOLOv5 Python client started. PID:{process.Id}");
                    return true;
                }
                catch (Exception ex)
                {
                    LastError = $"YOLOv5 Python client start failed: {ex.Message}";
                    AppLog.ABNORMAL(LastError);
                    process?.Dispose();
                    process = null;
                    currentStartSignature = "";
                    return false;
                }
            }
        }

        public void Stop()
        {
            Process processToStop;
            lock (sync)
            {
                processToStop = DetachProcessForStopLocked();
            }

            StopDetachedProcess(processToStop);
        }

        public Task StopAsync()
        {
            Process processToStop;
            lock (sync)
            {
                processToStop = DetachProcessForStopLocked();
            }

            return processToStop == null
                ? Task.CompletedTask
                : Task.Run(() => StopDetachedProcess(processToStop));
        }

        public void Dispose()
        {
            Stop();
        }

        public static bool TryCreateStartInfo(PythonModelSettings settings, out ProcessStartInfo startInfo, out string error)
        {
            startInfo = null;
            error = "";

            settings ??= new PythonModelSettings();
            settings.EnsureDefaults();

            PythonModelValidationResult validation = PythonModelSettingsValidator.Validate(settings, requireWeights: false);
            if (!validation.IsValid)
            {
                error = validation.Errors.FirstOrDefault() ?? "YOLOv5 Python client settings are invalid.";
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
                WindowStyle = ProcessWindowStyle.Hidden
            };
            startInfo.ArgumentList.Add(clientScriptPath);
            startInfo.ArgumentList.Add("--retry");

            string modelRootPath = Path.Combine(projectRootPath, "yolov5Master");
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

            return true;
        }

        public static bool TryCreateStartSignature(PythonModelSettings settings, out string startSignature, out string error)
        {
            startSignature = "";
            if (!TryCreateStartInfo(settings, out ProcessStartInfo startInfo, out error))
            {
                return false;
            }

            startSignature = CreateStartSignature(startInfo);
            return true;
        }

        private static string CreateStartSignature(ProcessStartInfo startInfo)
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

        private void StopLocked()
        {
            StopDetachedProcess(DetachProcessForStopLocked());
        }

        private Process DetachProcessForStopLocked()
        {
            if (process == null)
            {
                currentStartSignature = "";
                LastError = "";
                return null;
            }

            Process processToStop = process;
            process = null;
            currentStartSignature = "";
            LastError = "";
            stopRequested = true;
            return processToStop;
        }

        private void StopDetachedProcess(Process processToStop)
        {
            if (processToStop == null)
            {
                return;
            }

            try
            {
                processToStop.OutputDataReceived -= OnOutputDataReceived;
                processToStop.ErrorDataReceived -= OnErrorDataReceived;
                processToStop.Exited -= OnExited;

                if (!processToStop.HasExited)
                {
                    int pid = 0;
                    try
                    {
                        pid = processToStop.Id;
                    }
                    catch
                    {
                    }

                    processToStop.Kill(entireProcessTree: true);
                    AppLog.COMM(pid > 0
                        ? $"YOLOv5 Python client stop requested. PID:{pid}"
                        : "YOLOv5 Python client stop requested.");
                }

                LastExitedAtUtc = DateTime.UtcNow;
                LastExitCode = null;
            }
            catch (Exception ex)
            {
                AppLog.ABNORMAL($"YOLOv5 Python client stop failed: {ex.Message}");
            }
            finally
            {
                processToStop.Dispose();
            }
        }

        private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                if (stopRequested)
                {
                    return;
                }

                AppLog.COMM($"[YOLOv5] {e.Data}");
            }
        }

        private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
            {
                if (stopRequested)
                {
                    return;
                }

                if (IsBenignPythonStderrLine(e.Data))
                {
                    AppLog.COMM($"[YOLOv5] {e.Data}");
                    return;
                }

                LastError = e.Data;
                AppLog.ABNORMAL($"[YOLOv5] {e.Data}");
            }
        }

        private static bool IsBenignPythonStderrLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return true;
            }

            return line.IndexOf("warning", StringComparison.OrdinalIgnoreCase) >= 0
                || line.IndexOf("deprecated", StringComparison.OrdinalIgnoreCase) >= 0
                || line.IndexOf("pkg_resources", StringComparison.OrdinalIgnoreCase) >= 0
                || line.IndexOf("with amp.autocast", StringComparison.OrdinalIgnoreCase) >= 0
                || line.IndexOf("Fusing layers", StringComparison.OrdinalIgnoreCase) >= 0
                || line.IndexOf("Adding AutoShape", StringComparison.OrdinalIgnoreCase) >= 0
                || line.IndexOf("summary:", StringComparison.OrdinalIgnoreCase) >= 0
                || line.IndexOf("YOLOv5", StringComparison.OrdinalIgnoreCase) >= 0 && line.IndexOf("torch-", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void OnExited(object sender, EventArgs e)
        {
            Process exitedProcess = sender as Process;
            int exitCode = 0;
            try
            {
                exitCode = exitedProcess?.ExitCode ?? 0;
            }
            catch
            {
                // Process exit code can be unavailable during shutdown.
            }

            AppLog.COMM($"YOLOv5 Python client exited. ExitCode:{exitCode}");
            LastExitedAtUtc = DateTime.UtcNow;
            LastExitCode = exitCode;
            if (stopRequested)
            {
                LastError = "";
                stopRequested = false;
                return;
            }

            if (exitCode != 0)
            {
                LastError = $"YOLOv5 Python client exited with code {exitCode}.";
            }
        }
    }
}
