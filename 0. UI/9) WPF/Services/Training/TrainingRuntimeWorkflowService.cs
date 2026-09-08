using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the model-worker training lifecycle: readiness, start/stop dispatch,
    /// and the timestamped status-poll state. The Shell only projects results
    /// into WPF controls and keeps its timer as a UI scheduler.
    /// </summary>
    public sealed class TrainingRuntimeWorkflowService
    {
        private const int StatusPollTimeoutSeconds = 600;
        private readonly Func<LabelingProjectData> dataAccessor;
        private readonly Func<YoloTrainingWorkflowService> trainingWorkflowAccessor;
        private readonly Func<YoloPythonClientProcessService> processAccessor;
        private readonly Func<PythonModelCommunication> communicationAccessor;
        private readonly Func<PythonCommunicationStatus> statusAccessor;
        private readonly Func<int, CancellationToken, Task<bool>> ensureReadyAsync;
        private readonly Func<string> recipeNameAccessor;
        private DateTime statusPollingStartedUtc = DateTime.MinValue;

        public TrainingRuntimeWorkflowService(
            Func<LabelingProjectData> dataAccessor,
            Func<YoloTrainingWorkflowService> trainingWorkflowAccessor,
            Func<YoloPythonClientProcessService> processAccessor,
            Func<PythonModelCommunication> communicationAccessor,
            Func<PythonCommunicationStatus> statusAccessor,
            Func<int, CancellationToken, Task<bool>> ensureReadyAsync,
            Func<string> recipeNameAccessor)
        {
            this.dataAccessor = dataAccessor ?? throw new ArgumentNullException(nameof(dataAccessor));
            this.trainingWorkflowAccessor = trainingWorkflowAccessor ?? throw new ArgumentNullException(nameof(trainingWorkflowAccessor));
            this.processAccessor = processAccessor ?? throw new ArgumentNullException(nameof(processAccessor));
            this.communicationAccessor = communicationAccessor ?? throw new ArgumentNullException(nameof(communicationAccessor));
            this.statusAccessor = statusAccessor ?? throw new ArgumentNullException(nameof(statusAccessor));
            this.ensureReadyAsync = ensureReadyAsync ?? throw new ArgumentNullException(nameof(ensureReadyAsync));
            this.recipeNameAccessor = recipeNameAccessor ?? throw new ArgumentNullException(nameof(recipeNameAccessor));
        }

        public bool IsTrainingWorkflowRunning { get; private set; }

        public DateTime StatusPollingStartedUtc => statusPollingStartedUtc;

        public bool IsTrainingStopAvailable()
            => IsTrainingWorkflowRunning
                || TrainingProgressPresentationService.IsTrainingStopAvailable(statusAccessor());

        public async Task<TrainingRuntimeStartResult> StartAsync(
            TrainingRuntimeStartRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            bool ready = await ensureReadyAsync(request.WorkerReadyTimeoutMilliseconds, cancellationToken);
            PythonCommunicationStatus readyStatus = statusAccessor() ?? new PythonCommunicationStatus();
            if (!ready)
            {
                return TrainingRuntimeStartResult.WorkerUnavailable(
                    readyStatus,
                    processAccessor()?.LastError);
            }

            LabelingProjectData data = dataAccessor();
            PythonModelCommunication communication = communicationAccessor();
            YoloTrainingWorkflowService trainingWorkflow = trainingWorkflowAccessor();
            bool started = trainingWorkflow.TryStartTraining(
                data,
                communication,
                request.RunName,
                string.IsNullOrWhiteSpace(request.RecipeName) ? recipeNameAccessor() : request.RecipeName);
            return TrainingRuntimeStartResult.Completed(
                started,
                readyStatus,
                trainingWorkflow.LastPreparationFailureMessage);
        }

        public Task<TrainingRuntimeStopResult> StopAsync(CancellationToken cancellationToken)
        {
            return Task.Run(
                () =>
                {
                    bool stopped = trainingWorkflowAccessor().TryStopTraining(
                        communicationAccessor(),
                        processAccessor(),
                        cancellationToken);
                    if (stopped)
                    {
                        IsTrainingWorkflowRunning = false;
                        StopStatusPolling();
                    }

                    return new TrainingRuntimeStopResult(stopped, statusAccessor() ?? new PythonCommunicationStatus());
                },
                cancellationToken);
        }

        public void BeginStatusPolling()
        {
            IsTrainingWorkflowRunning = true;
            statusPollingStartedUtc = DateTime.UtcNow;
            RequestStatusSnapshot();
        }

        public void StopStatusPolling()
        {
            statusPollingStartedUtc = DateTime.MinValue;
        }

        public void Reset()
        {
            IsTrainingWorkflowRunning = false;
            StopStatusPolling();
        }

        public TrainingRuntimeStatusSnapshot PollStatus()
        {
            RequestStatusSnapshot();
            PythonCommunicationStatus status = statusAccessor() ?? new PythonCommunicationStatus();
            bool hasStatus = TrainingProgressPresentationService.HasTrainingStatus(status);
            bool hasCurrentStatus = hasStatus && IsCurrent(status);
            bool isLiveTraining = hasCurrentStatus && TrainingProgressPresentationService.IsLiveTrainingStatus(status);
            if (hasCurrentStatus)
            {
                IsTrainingWorkflowRunning = isLiveTraining;
            }

            bool isTerminal = hasCurrentStatus
                && TrainingProgressPresentationService.IsTerminalTrainingState(status.LastTrainingState);
            bool timedOut = !hasCurrentStatus
                && statusPollingStartedUtc != DateTime.MinValue
                && DateTime.UtcNow - statusPollingStartedUtc > TimeSpan.FromSeconds(StatusPollTimeoutSeconds);
            if (isTerminal)
            {
                StopStatusPolling();
            }

            return new TrainingRuntimeStatusSnapshot(
                status,
                hasCurrentStatus,
                isLiveTraining,
                isTerminal,
                timedOut,
                IsTrainingWorkflowRunning,
                statusPollingStartedUtc);
        }

        private bool IsCurrent(PythonCommunicationStatus status)
        {
            if (!status.LastTrainingStatusAtUtc.HasValue
                || statusPollingStartedUtc == DateTime.MinValue
                || !IsTrainingWorkflowRunning)
            {
                return TrainingProgressPresentationService.HasTrainingStatus(status);
            }

            return status.LastTrainingStatusAtUtc.Value >= statusPollingStartedUtc.AddSeconds(-1);
        }

        private void RequestStatusSnapshot()
        {
            if (!IsTrainingWorkflowRunning)
            {
                return;
            }

            communicationAccessor()?.SendModelStatus(
                YoloRuntimePresentationService.CreateRequestId(),
                ensureLoaded: false);
        }
    }

    public sealed class TrainingRuntimeStartRequest
    {
        public int WorkerReadyTimeoutMilliseconds { get; init; } = 30_000;

        public string RecipeName { get; init; } = string.Empty;

        public string RunName { get; init; } = string.Empty;
    }

    public sealed class TrainingRuntimeStartResult
    {
        private TrainingRuntimeStartResult(
            bool workerReady,
            bool started,
            PythonCommunicationStatus status,
            string preparationFailureMessage,
            string workerError)
        {
            WorkerReady = workerReady;
            Started = started;
            Status = status ?? new PythonCommunicationStatus();
            PreparationFailureMessage = preparationFailureMessage ?? string.Empty;
            WorkerError = workerError ?? string.Empty;
        }

        public bool WorkerReady { get; }

        public bool Started { get; }

        public PythonCommunicationStatus Status { get; }

        public string PreparationFailureMessage { get; }

        public string WorkerError { get; }

        public static TrainingRuntimeStartResult WorkerUnavailable(
            PythonCommunicationStatus status,
            string workerError)
            => new TrainingRuntimeStartResult(false, false, status, string.Empty, workerError);

        public static TrainingRuntimeStartResult Completed(
            bool started,
            PythonCommunicationStatus status,
            string preparationFailureMessage)
            => new TrainingRuntimeStartResult(true, started, status, preparationFailureMessage, string.Empty);
    }

    public sealed class TrainingRuntimeStopResult
    {
        public TrainingRuntimeStopResult(bool stopped, PythonCommunicationStatus status)
        {
            Stopped = stopped;
            Status = status ?? new PythonCommunicationStatus();
        }

        public bool Stopped { get; }

        public PythonCommunicationStatus Status { get; }
    }

    public sealed class TrainingRuntimeStatusSnapshot
    {
        public TrainingRuntimeStatusSnapshot(
            PythonCommunicationStatus status,
            bool hasCurrentStatus,
            bool isLiveTraining,
            bool isTerminal,
            bool timedOut,
            bool isTrainingWorkflowRunning,
            DateTime statusPollingStartedUtc)
        {
            Status = status ?? new PythonCommunicationStatus();
            HasCurrentStatus = hasCurrentStatus;
            IsLiveTraining = isLiveTraining;
            IsTerminal = isTerminal;
            TimedOut = timedOut;
            IsTrainingWorkflowRunning = isTrainingWorkflowRunning;
            StatusPollingStartedUtc = statusPollingStartedUtc;
        }

        public PythonCommunicationStatus Status { get; }

        public bool HasCurrentStatus { get; }

        public bool IsLiveTraining { get; }

        public bool IsTerminal { get; }

        public bool TimedOut { get; }

        public bool IsTrainingWorkflowRunning { get; }

        public DateTime StatusPollingStartedUtc { get; }
    }
}
