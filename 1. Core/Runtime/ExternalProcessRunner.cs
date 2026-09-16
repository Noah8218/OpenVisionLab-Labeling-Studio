using System;
using System.Diagnostics;
using System.IO;
using System.Text;
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
        private const int MaxCapturedOutputCharacters = 256 * 1024;
        private const int CapturedOutputTailCharacters = 64 * 1024;
        private const string OutputTruncationMarker = "\n...[external process output truncated; first and last portions retained]...\n";
        private static readonly int CapturedOutputHeadCharacters =
            MaxCapturedOutputCharacters - CapturedOutputTailCharacters - OutputTruncationMarker.Length;
        private static readonly TimeSpan OutputDrainTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan ProcessExitAfterKillTimeout = TimeSpan.FromSeconds(5);
        private static readonly Encoding Utf8OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);

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
            ConfigureDefaultOutputEncoding(startInfo);
            Task<BoundedOutputCapture> outputTask = Task.FromResult(BoundedOutputCapture.Empty);
            Task<BoundedOutputCapture> errorTask = Task.FromResult(BoundedOutputCapture.Empty);
            bool started = false;
            ProcessTreeLifetime processTree = null;

            try
            {
                if (!process.Start())
                {
                    return ExternalProcessRunResult.NotStarted("External process could not be started.");
                }

                started = true;
                processTree = new ProcessTreeLifetime(process);

                outputTask = startInfo.RedirectStandardOutput
                    ? CaptureOutputAsync(process.StandardOutput)
                    : Task.FromResult(BoundedOutputCapture.Empty);
                errorTask = startInfo.RedirectStandardError
                    ? CaptureOutputAsync(process.StandardError)
                    : Task.FromResult(BoundedOutputCapture.Empty);

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
                    processTree.Dispose();
                    WaitForExitAfterKill(process);
                    BoundedOutputCapture output = await ReadOutputAsync(outputTask, "stdout").ConfigureAwait(false);
                    BoundedOutputCapture error = await ReadOutputAsync(errorTask, "stderr").ConfigureAwait(false);
                    return new ExternalProcessRunResult(
                        exitCode: -1,
                        output: output.Text,
                        error: error.Text,
                        started: true,
                        timedOut: timedOut,
                        canceled: canceled,
                        outputTruncated: output.Truncated,
                        errorTruncated: error.Truncated);
                }

                processTree.Dispose();
                int exitCode = process.ExitCode;
                BoundedOutputCapture completedOutput = await ReadOutputAsync(outputTask, "stdout").ConfigureAwait(false);
                BoundedOutputCapture completedError = await ReadOutputAsync(errorTask, "stderr").ConfigureAwait(false);
                string errorText = completedError.Text;
                if (exitCode != 0 && string.IsNullOrWhiteSpace(errorText))
                {
                    errorText = $"External process exited with code {exitCode}.";
                }

                return new ExternalProcessRunResult(
                    exitCode,
                    completedOutput.Text,
                    errorText,
                    started: true,
                    timedOut: false,
                    canceled: false,
                    outputTruncated: completedOutput.Truncated,
                    errorTruncated: completedError.Truncated);
            }
            catch (Exception ex)
            {
                TryKill(process);
                processTree?.Dispose();
                WaitForExitAfterKill(process);
                BoundedOutputCapture output = await ReadOutputAsync(outputTask, "stdout").ConfigureAwait(false);
                BoundedOutputCapture errorOutput = await ReadOutputAsync(errorTask, "stderr").ConfigureAwait(false);
                return new ExternalProcessRunResult(
                    exitCode: -1,
                    output: output.Text,
                    error: string.IsNullOrWhiteSpace(errorOutput.Text)
                        ? ex.Message
                        : $"{ex.Message} / {errorOutput.Text}",
                    started: started,
                    timedOut: false,
                    canceled: false,
                    outputTruncated: output.Truncated,
                    errorTruncated: errorOutput.Truncated);
            }
            finally
            {
                processTree?.Dispose();
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

        private static void ConfigureDefaultOutputEncoding(ProcessStartInfo startInfo)
        {
            if (startInfo.RedirectStandardOutput && startInfo.StandardOutputEncoding == null)
            {
                startInfo.StandardOutputEncoding = Utf8OutputEncoding;
            }

            if (startInfo.RedirectStandardError && startInfo.StandardErrorEncoding == null)
            {
                startInfo.StandardErrorEncoding = Utf8OutputEncoding;
            }
        }

        private static async Task<BoundedOutputCapture> ReadOutputAsync(
            Task<BoundedOutputCapture> outputTask,
            string streamName)
        {
            try
            {
                Task completed = await Task.WhenAny(
                    outputTask,
                    Task.Delay(OutputDrainTimeout)).ConfigureAwait(false);
                return completed == outputTask
                    ? await outputTask.ConfigureAwait(false)
                    : BoundedOutputCapture.Empty;
            }
            catch (Exception error)
            {
                AppLog.ABNORMAL($"External process {streamName} drain failed: {error.Message}");
                return BoundedOutputCapture.Empty;
            }
        }

        private static async Task<BoundedOutputCapture> CaptureOutputAsync(StreamReader reader)
        {
            var head = new StringBuilder(CapturedOutputHeadCharacters);
            var tail = new StringBuilder(CapturedOutputTailCharacters);
            char[] buffer = new char[8192];
            bool truncated = false;

            int read;
            while ((read = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
            {
                if (!truncated)
                {
                    int remainingHead = CapturedOutputHeadCharacters - head.Length;
                    if (read <= remainingHead)
                    {
                        head.Append(buffer, 0, read);
                        continue;
                    }

                    if (remainingHead > 0)
                    {
                        head.Append(buffer, 0, remainingHead);
                    }

                    truncated = true;
                    AppendTail(tail, buffer, remainingHead, read - remainingHead);
                    continue;
                }

                AppendTail(tail, buffer, 0, read);
            }

            if (!truncated)
            {
                return new BoundedOutputCapture(head.ToString(), truncated: false);
            }

            return new BoundedOutputCapture(
                head.ToString() + OutputTruncationMarker + tail,
                truncated: true);
        }

        private static void AppendTail(StringBuilder tail, char[] buffer, int index, int count)
        {
            if (count <= 0)
            {
                return;
            }

            if (count >= CapturedOutputTailCharacters)
            {
                tail.Clear();
                tail.Append(buffer, index + count - CapturedOutputTailCharacters, CapturedOutputTailCharacters);
                return;
            }

            int overflow = tail.Length + count - CapturedOutputTailCharacters;
            if (overflow > 0)
            {
                tail.Remove(0, overflow);
            }

            tail.Append(buffer, index, count);
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
            catch (Exception error)
            {
                AppLog.ABNORMAL($"External process exit wait after kill failed: {error.Message}");
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
            catch (Exception error)
            {
                AppLog.ABNORMAL($"External process tree kill failed: {error.Message}");
            }
        }

        private sealed class BoundedOutputCapture
        {
            public static readonly BoundedOutputCapture Empty = new BoundedOutputCapture(string.Empty, truncated: false);

            public BoundedOutputCapture(string text, bool truncated)
            {
                Text = text ?? string.Empty;
                Truncated = truncated;
            }

            public string Text { get; }

            public bool Truncated { get; }
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
            bool canceled,
            bool outputTruncated = false,
            bool errorTruncated = false)
        {
            ExitCode = exitCode;
            Output = output ?? string.Empty;
            Error = error ?? string.Empty;
            Started = started;
            TimedOut = timedOut;
            Canceled = canceled;
            OutputTruncated = outputTruncated;
            ErrorTruncated = errorTruncated;
        }

        public int ExitCode { get; }

        public string Output { get; }

        public string Error { get; }

        public bool Started { get; }

        public bool TimedOut { get; }

        public bool Canceled { get; }

        public bool OutputTruncated { get; }

        public bool ErrorTruncated { get; }

        public static ExternalProcessRunResult CanceledBeforeStart()
            => new ExternalProcessRunResult(-1, string.Empty, string.Empty, false, false, true);

        public static ExternalProcessRunResult NotStarted(string error)
            => new ExternalProcessRunResult(-1, string.Empty, error, false, false, false);
    }
}
