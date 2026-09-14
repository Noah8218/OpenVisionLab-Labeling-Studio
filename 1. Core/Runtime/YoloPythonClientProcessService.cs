using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem._1._Core
{
    public sealed class YoloPythonClientProcessService : IDisposable
    {
        private readonly object sync = new object();
        private Process process;
        private ProcessTreeLifetime processTree;
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
            return EnsureStarted(settings, CancellationToken.None);
        }

        public bool EnsureStarted(PythonModelSettings settings, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (sync)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!PythonClientStartInfoBuilder.TryCreateStartInfo(settings, out ProcessStartInfo startInfo, out string error))
                {
                    LastError = error;
                    AppLog.ABNORMAL(error);
                    return false;
                }

                string startSignature = PythonClientStartInfoBuilder.CreateStartSignature(startInfo);
                if (IsRunning && string.Equals(currentStartSignature, startSignature, StringComparison.Ordinal))
                {
                    return true;
                }

                if (IsRunning)
                {
                    AppLog.COMM("YOLO Python client settings changed. Restarting client process.");
                    StopLocked();
                }

                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    process?.Dispose();
                    processTree?.Dispose();
                    processTree = null;
                    process = new Process
                    {
                        StartInfo = startInfo,
                        EnableRaisingEvents = true
                    };
                    process.OutputDataReceived += OnOutputDataReceived;
                    process.ErrorDataReceived += OnErrorDataReceived;
                    process.Exited += OnExited;

                    cancellationToken.ThrowIfCancellationRequested();
                    if (!process.Start())
                    {
                        LastError = "YOLO Python client process did not start.";
                        AppLog.ABNORMAL(LastError);
                        DisposeFailedProcessLocked();
                        return false;
                    }

                    processTree = new ProcessTreeLifetime(process);
                    cancellationToken.ThrowIfCancellationRequested();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    cancellationToken.ThrowIfCancellationRequested();
                    LastStartedAtUtc = DateTime.UtcNow;
                    LastError = "";
                    LastExitCode = null;
                    stopRequested = false;
                    currentStartSignature = startSignature;
                    AppLog.COMM($"YOLO Python client started. PID:{process.Id}");
                    return true;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    DisposeFailedProcessLocked();
                    throw;
                }
                catch (Exception ex)
                {
                    LastError = $"YOLO Python client start failed: {ex.Message}";
                    AppLog.ABNORMAL(LastError);
                    DisposeFailedProcessLocked();
                    return false;
                }
            }
        }

        public void Stop()
        {
            StopAndWait(TimeSpan.FromSeconds(5));
        }

        public bool StopAndWait(TimeSpan timeout)
        {
            Process processToStop;
            ProcessTreeLifetime treeToStop;
            lock (sync)
            {
                processToStop = DetachProcessForStopLocked(out treeToStop);
            }

            return StopDetachedProcess(processToStop, treeToStop, timeout);
        }

        public Task StopAsync()
        {
            Process processToStop;
            ProcessTreeLifetime treeToStop;
            lock (sync)
            {
                processToStop = DetachProcessForStopLocked(out treeToStop);
            }

            return processToStop == null
                ? Task.CompletedTask
                : Task.Run(() => StopDetachedProcess(processToStop, treeToStop, TimeSpan.FromSeconds(5)));
        }

        public void Dispose()
        {
            Stop();
        }

        public static bool TryCreateStartInfo(PythonModelSettings settings, out ProcessStartInfo startInfo, out string error)
        {
            return PythonClientStartInfoBuilder.TryCreateStartInfo(settings, out startInfo, out error);
        }

        public static bool TryCreateStartSignature(PythonModelSettings settings, out string startSignature, out string error)
        {
            return PythonClientStartInfoBuilder.TryCreateStartSignature(settings, out startSignature, out error);
        }

        private void StopLocked()
        {
            Process processToStop = DetachProcessForStopLocked(out ProcessTreeLifetime treeToStop);
            StopDetachedProcess(processToStop, treeToStop, TimeSpan.FromSeconds(5));
        }

        private void DisposeFailedProcessLocked()
        {
            Process failedProcess = process;
            ProcessTreeLifetime failedTree = processTree;
            process = null;
            processTree = null;
            currentStartSignature = "";
            stopRequested = true;
            if (failedProcess == null)
            {
                return;
            }

            StopDetachedProcess(failedProcess, failedTree, TimeSpan.FromSeconds(5));
        }

        private Process DetachProcessForStopLocked(out ProcessTreeLifetime treeToStop)
        {
            treeToStop = processTree;
            processTree = null;
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

        private bool StopDetachedProcess(Process processToStop, ProcessTreeLifetime treeToStop, TimeSpan timeout)
        {
            if (processToStop == null)
            {
                treeToStop?.Dispose();
                return true;
            }

            bool hasExited = false;
            try
            {
                processToStop.OutputDataReceived -= OnOutputDataReceived;
                processToStop.ErrorDataReceived -= OnErrorDataReceived;
                processToStop.Exited -= OnExited;
                treeToStop?.Dispose();

                try
                {
                    hasExited = processToStop.HasExited;
                }
                catch (InvalidOperationException)
                {
                    hasExited = true;
                }

                if (!hasExited)
                {
                    int pid = 0;
                    try
                    {
                        pid = processToStop.Id;
                    }
                    catch (Exception error)
                    {
                        AppLog.COMM($"YOLO Python client PID was unavailable during stop: {error.Message}");
                    }

                    if (treeToStop == null)
                    {
                        processToStop.Kill(entireProcessTree: true);
                    }
                    AppLog.COMM(pid > 0
                        ? $"YOLO Python client stop requested. PID:{pid}"
                        : "YOLO Python client stop requested.");
                    int waitMilliseconds = (int)Math.Max(0, Math.Min(int.MaxValue, timeout.TotalMilliseconds));
                    hasExited = processToStop.WaitForExit(waitMilliseconds);
                }

                if (!hasExited)
                {
                    LastError = "YOLO Python client process did not exit within the stop timeout.";
                    AppLog.ABNORMAL(LastError);
                    return false;
                }

                LastExitedAtUtc = DateTime.UtcNow;
                try
                {
                    LastExitCode = processToStop.ExitCode;
                }
                catch (InvalidOperationException)
                {
                    LastExitCode = null;
                }
                return true;
            }
            catch (Exception ex)
            {
                AppLog.ABNORMAL($"YOLO Python client stop failed: {ex.Message}");
                return false;
            }
            finally
            {
                treeToStop?.Dispose();
                processToStop.Dispose();
            }
        }

        private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!IsCurrentProcess(sender) || string.IsNullOrWhiteSpace(e.Data))
            {
                return;
            }

            AppLog.COMM($"[YOLO] {e.Data}");
        }

        private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!IsCurrentProcess(sender) || string.IsNullOrWhiteSpace(e.Data))
            {
                return;
            }

            if (IsBenignPythonStderrLine(e.Data))
            {
                AppLog.COMM($"[YOLO] {e.Data}");
                return;
            }

            LastError = e.Data;
            AppLog.ABNORMAL($"[YOLO] {e.Data}");
        }

        private bool IsCurrentProcess(object sender)
        {
            lock (sync)
            {
                return !stopRequested && ReferenceEquals(process, sender);
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
            bool isCurrentProcess;
            bool wasStopRequested;
            lock (sync)
            {
                isCurrentProcess = ReferenceEquals(process, exitedProcess);
                wasStopRequested = stopRequested;
            }
            if (!isCurrentProcess)
            {
                return;
            }

            int exitCode = 0;
            try
            {
                exitCode = exitedProcess?.ExitCode ?? 0;
            }
            catch (Exception error)
            {
                AppLog.COMM($"YOLO Python client exit code was unavailable during shutdown: {error.Message}");
            }

            AppLog.COMM($"YOLO Python client exited. ExitCode:{exitCode}");
            lock (sync)
            {
                if (!ReferenceEquals(process, exitedProcess))
                {
                    return;
                }

                processTree?.Dispose();
                processTree = null;

                LastExitedAtUtc = DateTime.UtcNow;
                LastExitCode = exitCode;
                if (wasStopRequested)
                {
                    LastError = "";
                    stopRequested = false;
                    return;
                }

                if (exitCode != 0)
                {
                    LastError = $"YOLO Python client exited with code {exitCode}.";
                }
            }
        }
    }
}
