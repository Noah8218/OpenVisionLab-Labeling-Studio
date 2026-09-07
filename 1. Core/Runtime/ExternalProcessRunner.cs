using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem._1._Core
{
    /// <summary>
    /// Owns the common external-process lifecycle used by Python and PowerShell workers.
    /// Output is drained concurrently, and cancellation/timeout always attempts to stop
    /// the complete process tree before the result is returned.
    /// </summary>
    public sealed class ExternalProcessRunner
    {
        private static readonly TimeSpan OutputDrainTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan ProcessExitAfterKillTimeout = TimeSpan.FromSeconds(5);

        public ExternalProcessRunResult Run(
            ProcessStartInfo startInfo,
            TimeSpan timeout = default,
            CancellationToken cancellationToken = default)
            => RunAsync(startInfo, timeout, cancellationToken).GetAwaiter().GetResult();

        public async Task<ExternalProcessRunResult> RunAsync(
            ProcessStartInfo startInfo,
            TimeSpan timeout = default,
            CancellationToken cancellationToken = default)
        {
            if (startInfo == null)
            {
                return ExternalProcessRunResult.NotStarted("External process start information is missing.");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return ExternalProcessRunResult.CanceledBeforeStart();
            }

            using var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = false
            };
            Task<string> outputTask = Task.FromResult(string.Empty);
            Task<string> errorTask = Task.FromResult(string.Empty);
            bool started = false;

            try
            {
                if (!process.Start())
                {
                    return ExternalProcessRunResult.NotStarted("External process could not be started.");
                }

                started = true;

                outputTask = startInfo.RedirectStandardOutput
                    ? process.StandardOutput.ReadToEndAsync()
                    : Task.FromResult(string.Empty);
                errorTask = startInfo.RedirectStandardError
                    ? process.StandardError.ReadToEndAsync()
                    : Task.FromResult(string.Empty);

                using CancellationTokenSource timeoutSource = CreateTimeoutSource(timeout);
                using CancellationTokenSource linkedSource = CreateLinkedSource(cancellationToken, timeoutSource);
                CancellationToken waitToken = linkedSource?.Token ?? cancellationToken;
                try
                {
                    await process.WaitForExitAsync(waitToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (waitToken.IsCancellationRequested)
                {
                    bool canceled = cancellationToken.IsCancellationRequested;
                    bool timedOut = !canceled && timeoutSource?.IsCancellationRequested == true;
                    TryKill(process);
                    WaitForExitAfterKill(process);
                    return new ExternalProcessRunResult(
                        exitCode: -1,
                        output: await ReadOutputAsync(outputTask).ConfigureAwait(false),
                        error: await ReadOutputAsync(errorTask).ConfigureAwait(false),
                        started: true,
                        timedOut: timedOut,
                        canceled: canceled);
                }

                return new ExternalProcessRunResult(
                    process.ExitCode,
                    await ReadOutputAsync(outputTask).ConfigureAwait(false),
                    await ReadOutputAsync(errorTask).ConfigureAwait(false),
                    started: true,
                    timedOut: false,
                    canceled: false);
            }
            catch (Exception ex)
            {
                TryKill(process);
                WaitForExitAfterKill(process);
                return new ExternalProcessRunResult(
                    exitCode: -1,
                    output: await ReadOutputAsync(outputTask).ConfigureAwait(false),
                    error: ex.Message,
                    started: started,
                    timedOut: false,
                    canceled: false);
            }
        }

        private static CancellationTokenSource CreateTimeoutSource(TimeSpan timeout)
        {
            return timeout > TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan
                ? new CancellationTokenSource(timeout)
                : null;
        }

        private static CancellationTokenSource CreateLinkedSource(
            CancellationToken cancellationToken,
            CancellationTokenSource timeoutSource)
        {
            if (timeoutSource == null)
            {
                return null;
            }

            return CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
        }

        private static async Task<string> ReadOutputAsync(Task<string> outputTask)
        {
            try
            {
                Task completed = await Task.WhenAny(
                    outputTask,
                    Task.Delay(OutputDrainTimeout)).ConfigureAwait(false);
                return completed == outputTask
                    ? await outputTask.ConfigureAwait(false)
                    : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void WaitForExitAfterKill(Process process)
        {
            try
            {
                if (process != null && !process.HasExited)
                {
                    process.WaitForExit((int)ProcessExitAfterKillTimeout.TotalMilliseconds);
                }
            }
            catch
            {
            }
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (process != null && !process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
            }
        }
    }

    public sealed class ExternalProcessRunResult
    {
        public ExternalProcessRunResult(
            int exitCode,
            string output,
            string error,
            bool started,
            bool timedOut,
            bool canceled)
        {
            ExitCode = exitCode;
            Output = output ?? string.Empty;
            Error = error ?? string.Empty;
            Started = started;
            TimedOut = timedOut;
            Canceled = canceled;
        }

        public int ExitCode { get; }

        public string Output { get; }

        public string Error { get; }

        public bool Started { get; }

        public bool TimedOut { get; }

        public bool Canceled { get; }

        public static ExternalProcessRunResult CanceledBeforeStart()
            => new ExternalProcessRunResult(-1, string.Empty, string.Empty, false, false, true);

        public static ExternalProcessRunResult NotStarted(string error)
            => new ExternalProcessRunResult(-1, string.Empty, error, false, false, false);
    }
}
