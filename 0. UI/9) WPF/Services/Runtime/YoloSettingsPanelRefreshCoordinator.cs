using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns one current YOLO settings-panel environment check and its cancellation
    /// lifetime. The Shell keeps WPF bindings and applies the admitted result.
    /// </summary>
    public sealed class YoloSettingsPanelRefreshCoordinator : IDisposable
    {
        private readonly object syncRoot = new object();
        private readonly Func<PythonModelSettings> settingsAccessor;
        private readonly Func<PythonCommunicationStatus> communicationStatusAccessor;
        private readonly Func<bool> pythonClientProcessRunningAccessor;
        private readonly Action<PythonCommunicationStatus> applyRuntimeCapabilities;
        private readonly Action<YoloSettingsPanelRefreshResult> applyResult;
        private readonly Func<PythonModelSettings, CancellationToken, Task<PythonEnvironmentCheckResult>> checkRequirementsAsync;
        private CancellationTokenSource activeCancellation;
        private Task activeTask = Task.CompletedTask;
        private long refreshVersion;
        private bool disposed;

        public YoloSettingsPanelRefreshCoordinator(
            Func<PythonModelSettings> settingsAccessor,
            Func<PythonCommunicationStatus> communicationStatusAccessor,
            Func<bool> pythonClientProcessRunningAccessor,
            Action<PythonCommunicationStatus> applyRuntimeCapabilities,
            Action<YoloSettingsPanelRefreshResult> applyResult,
            Func<PythonModelSettings, CancellationToken, Task<PythonEnvironmentCheckResult>> checkRequirementsAsync = null)
        {
            this.settingsAccessor = settingsAccessor ?? throw new ArgumentNullException(nameof(settingsAccessor));
            this.communicationStatusAccessor = communicationStatusAccessor ?? throw new ArgumentNullException(nameof(communicationStatusAccessor));
            this.pythonClientProcessRunningAccessor = pythonClientProcessRunningAccessor ?? throw new ArgumentNullException(nameof(pythonClientProcessRunningAccessor));
            this.applyRuntimeCapabilities = applyRuntimeCapabilities ?? throw new ArgumentNullException(nameof(applyRuntimeCapabilities));
            this.applyResult = applyResult ?? throw new ArgumentNullException(nameof(applyResult));
            this.checkRequirementsAsync = checkRequirementsAsync ?? PythonEnvironmentService.CheckRequirementsAsync;
        }

        public bool IsDisposed
        {
            get
            {
                lock (syncRoot)
                {
                    return disposed;
                }
            }
        }

        public long CurrentVersion
        {
            get
            {
                lock (syncRoot)
                {
                    return refreshVersion;
                }
            }
        }

        public Task CurrentTask
        {
            get
            {
                lock (syncRoot)
                {
                    return activeTask;
                }
            }
        }

        public Task RefreshAsync(PythonModelValidationResult validation = null)
        {
            YoloSettingsPanelRefreshOperation operation;
            TaskCompletionSource<bool> startGate;
            CancellationTokenSource previousCancellation;
            Task previousTask;
            Task currentTask;

            lock (syncRoot)
            {
                if (disposed)
                {
                    return Task.CompletedTask;
                }

                operation = CreateOperation(validation, ++refreshVersion, new CancellationTokenSource());
                previousCancellation = activeCancellation;
                previousTask = activeTask;
                activeCancellation = operation.Cancellation;
                startGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                currentTask = ExecuteAsync(operation, startGate.Task);
                activeTask = currentTask;
            }

            previousCancellation?.Cancel();
            DisposeWhenCompleted(previousCancellation, previousTask);

            try
            {
                if (IsCurrent(operation))
                {
                    applyRuntimeCapabilities(operation.CommunicationStatus);
                }
            }
            finally
            {
                startGate.TrySetResult(true);
            }
            return currentTask;
        }

        public bool IsCurrent(YoloSettingsPanelRefreshOperation operation)
        {
            if (operation == null)
            {
                return false;
            }

            lock (syncRoot)
            {
                return !disposed
                    && operation.Version == refreshVersion
                    && ReferenceEquals(operation.Cancellation, activeCancellation)
                    && !operation.CancellationToken.IsCancellationRequested;
            }
        }

        public void Dispose()
        {
            CancellationTokenSource cancellation;
            Task task;
            lock (syncRoot)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                cancellation = activeCancellation;
                task = activeTask;
                activeCancellation = null;
                activeTask = Task.CompletedTask;
                ++refreshVersion;
            }

            if (cancellation == null)
            {
                return;
            }

            cancellation.Cancel();
            DisposeWhenCompleted(cancellation, task);
        }

        private YoloSettingsPanelRefreshOperation CreateOperation(
            PythonModelValidationResult validation,
            long version,
            CancellationTokenSource cancellation)
        {
            PythonModelSettings currentSettings = settingsAccessor() ?? new PythonModelSettings();
            PythonModelRuntimePathResolver.ApplyDefaults(currentSettings);
            PythonModelSettings settings = CloneSettings(currentSettings);
            PythonCommunicationStatus communicationStatus = communicationStatusAccessor()?.Clone()
                ?? new PythonCommunicationStatus();
            PythonModelRuntimeState runtimeState = PythonModelSettingsValidator.GetRuntimeState(
                settings,
                communicationStatus.WorkerSupportedModels,
                communicationStatus.WorkerTrainingModels,
                communicationStatus.WorkerDetectionModels);
            PythonModelValidationResult effectiveValidation = validation
                ?? (runtimeState.State == PythonModelRuntimeStateKind.NotInstalled
                    ? new PythonModelValidationResult(new[] { runtimeState.NextActionText }, Array.Empty<string>())
                    : PythonModelSettingsValidator.Validate(settings, requireWeights: true));

            return new YoloSettingsPanelRefreshOperation(
                version,
                settings,
                effectiveValidation,
                runtimeState,
                communicationStatus,
                pythonClientProcessRunningAccessor(),
                cancellation);
        }

        private async Task ExecuteAsync(
            YoloSettingsPanelRefreshOperation operation,
            Task startGate)
        {
            PythonEnvironmentCheckResult environment = null;
            string environmentCheckError = string.Empty;
            try
            {
                await startGate.ConfigureAwait(true);
                if (!IsCurrent(operation))
                {
                    return;
                }

                if (operation.RuntimeState.IsRuntimeInstalled)
                {
                    try
                    {
                        environment = await checkRequirementsAsync(
                                operation.Settings,
                                operation.CancellationToken)
                            .ConfigureAwait(true);
                    }
                    catch (OperationCanceledException) when (operation.CancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        environmentCheckError = ex.Message;
                    }
                }

                if (!IsCurrent(operation))
                {
                    return;
                }

                applyResult(new YoloSettingsPanelRefreshResult(
                    operation.Settings,
                    operation.Validation,
                    operation.RuntimeState,
                    operation.CommunicationStatus,
                    operation.PythonClientProcessRunning,
                    environment,
                    environmentCheckError));
            }
            catch (OperationCanceledException) when (operation.CancellationToken.IsCancellationRequested)
            {
            }
            finally
            {
                Complete(operation);
            }
        }

        private void Complete(YoloSettingsPanelRefreshOperation operation)
        {
            CancellationTokenSource cancellation = null;
            lock (syncRoot)
            {
                if (ReferenceEquals(operation.Cancellation, activeCancellation))
                {
                    activeCancellation = null;
                    activeTask = Task.CompletedTask;
                    cancellation = operation.Cancellation;
                }
            }

            cancellation?.Dispose();
        }

        private static void DisposeWhenCompleted(CancellationTokenSource cancellation, Task task)
        {
            if (cancellation == null)
            {
                return;
            }

            if (task == null || task.IsCompleted)
            {
                cancellation.Dispose();
                return;
            }

            task.ContinueWith(
                _ => cancellation.Dispose(),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private static PythonModelSettings CloneSettings(PythonModelSettings source)
        {
            return new PythonModelSettings
            {
                PythonExecutablePath = source?.PythonExecutablePath ?? string.Empty,
                ModelEngine = source?.ModelEngine ?? PythonModelSettings.EngineYoloV5,
                ProjectRootPath = source?.ProjectRootPath ?? string.Empty,
                ClientScriptPath = source?.ClientScriptPath ?? string.Empty,
                WeightsPath = source?.WeightsPath ?? string.Empty,
                ImageRootPath = source?.ImageRootPath ?? string.Empty,
                MinimumDetectionConfidence = source?.MinimumDetectionConfidence ?? 0.25F,
                MaximumDetectionCandidates = source?.MaximumDetectionCandidates ?? 20,
                InferenceImageSize = source?.InferenceImageSize ?? 320,
                DetectionTimeoutSeconds = source?.DetectionTimeoutSeconds ?? 30,
                AutoStartClient = source?.AutoStartClient ?? true
            };
        }
    }

    public sealed class YoloSettingsPanelRefreshOperation
    {
        internal YoloSettingsPanelRefreshOperation(
            long version,
            PythonModelSettings settings,
            PythonModelValidationResult validation,
            PythonModelRuntimeState runtimeState,
            PythonCommunicationStatus communicationStatus,
            bool pythonClientProcessRunning,
            CancellationTokenSource cancellation)
        {
            Version = version;
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Validation = validation ?? throw new ArgumentNullException(nameof(validation));
            RuntimeState = runtimeState ?? throw new ArgumentNullException(nameof(runtimeState));
            CommunicationStatus = communicationStatus ?? throw new ArgumentNullException(nameof(communicationStatus));
            PythonClientProcessRunning = pythonClientProcessRunning;
            Cancellation = cancellation ?? throw new ArgumentNullException(nameof(cancellation));
        }

        public long Version { get; }

        public PythonModelSettings Settings { get; }

        public PythonModelValidationResult Validation { get; }

        public PythonModelRuntimeState RuntimeState { get; }

        public PythonCommunicationStatus CommunicationStatus { get; }

        public bool PythonClientProcessRunning { get; }

        public CancellationToken CancellationToken => Cancellation.Token;

        internal CancellationTokenSource Cancellation { get; }
    }

    public sealed class YoloSettingsPanelRefreshResult
    {
        internal YoloSettingsPanelRefreshResult(
            PythonModelSettings settings,
            PythonModelValidationResult validation,
            PythonModelRuntimeState runtimeState,
            PythonCommunicationStatus communicationStatus,
            bool pythonClientProcessRunning,
            PythonEnvironmentCheckResult environment,
            string environmentCheckError)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Validation = validation ?? throw new ArgumentNullException(nameof(validation));
            RuntimeState = runtimeState ?? throw new ArgumentNullException(nameof(runtimeState));
            CommunicationStatus = communicationStatus ?? throw new ArgumentNullException(nameof(communicationStatus));
            PythonClientProcessRunning = pythonClientProcessRunning;
            Environment = environment;
            EnvironmentCheckError = environmentCheckError ?? string.Empty;
        }

        public PythonModelSettings Settings { get; }

        public PythonModelValidationResult Validation { get; }

        public PythonModelRuntimeState RuntimeState { get; }

        public PythonCommunicationStatus CommunicationStatus { get; }

        public bool PythonClientProcessRunning { get; }

        public PythonEnvironmentCheckResult Environment { get; }

        public string EnvironmentCheckError { get; }
    }
}
