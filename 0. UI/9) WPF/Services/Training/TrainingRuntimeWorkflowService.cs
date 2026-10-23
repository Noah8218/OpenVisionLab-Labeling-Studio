using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the model-worker training lifecycle: readiness, start/stop dispatch,
    /// and the timestamped status-poll state. The Shell only projects results
    /// into WPF controls and keeps its timer as a UI scheduler.
    /// </summary>
    public sealed class TrainingRuntimeWorkflowService : IDisposable
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
        private string statusPollingRunId = string.Empty;
        private readonly TrainingQueueWorkflowService queue = new();
        private CancellationTokenSource queuePreparationCancellation;
        private TrainingQueueRuntimeCallbacks queueCallbacks;
        private bool isPreparingQueue;
        private bool disposed;
        private PendingTrainingQueueCandidate pendingCandidate;

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
            queue.Changed += HandleQueueChanged;
        }

        public bool IsTrainingWorkflowRunning { get; private set; }

        public DateTime StatusPollingStartedUtc => statusPollingStartedUtc;
        public event Action<TrainingQueueSnapshot> QueueChanged;
        public TrainingQueueSnapshot QueueSnapshot => queue.Snapshot;
        public bool IsQueueBusy => isPreparingQueue || queue.Snapshot.IsRunning;
        public bool IsQueuePreparing => isPreparingQueue;
        public bool HasPendingQueueCandidate => pendingCandidate != null;
        public string PendingQueueCandidateWeightsPath => pendingCandidate?.WeightsPath ?? string.Empty;

        public void ConfigureQueue(TrainingQueueRuntimeCallbacks callbacks)
        {
            ArgumentNullException.ThrowIfNull(callbacks); ArgumentNullException.ThrowIfNull(callbacks.SaveMetadata);
            queueCallbacks = callbacks;
        }

        public async Task<bool> RunQueueAsync(TrainingQueuePlan plan, IEnumerable<TrainingDatasetImageSelection> images)
        {
            ArgumentNullException.ThrowIfNull(plan);
            if (disposed || IsQueueBusy || pendingCandidate != null || queueCallbacks?.CanStart?.Invoke() != true || queueCallbacks.IsCloseApproved?.Invoke() == true)
                return false;
            LabelingProjectData source = dataAccessor();
            if (!string.Equals(plan.RecipeName, recipeNameAccessor(), StringComparison.Ordinal)
                || plan.Models.Any(model => !YoloTrainingWorkflowService.CanUseQueuedTrainingModel(source?.ProjectSettings?.PythonModel, source?.ProjectSettings?.DatasetPurpose ?? LabelingDatasetPurpose.ObjectDetection, statusAccessor(), model.ModelEngine, model.WeightsName)))
                throw new InvalidOperationException("The recipe or connected model runtime changed before training.");
            TrainingDatasetImageSelection[] selectedImages = (images ?? Array.Empty<TrainingDatasetImageSelection>()).ToArray();
            var preparationCancellation = new CancellationTokenSource();
            queuePreparationCancellation = preparationCancellation;
            isPreparingQueue = true; NotifyQueueChanged();
            try
            {
                var preparation = new YoloTrainingDatasetPreparationService();
                string inputRoot = Path.Combine(source.OutputRootPath, "training-inputs", Guid.NewGuid().ToString("N"));
                TrainingDatasetSnapshot captured = await Task.Run(() => preparation.CaptureQueueInput(source,
                    new RecipeDatasetVersionSnapshot { ContentSha256 = plan.DatasetContentSha256 }, selectedImages, plan.OnlyReviewed, inputRoot, preparationCancellation.Token), preparationCancellation.Token);
                preparationCancellation.Token.ThrowIfCancellationRequested();
                if (disposed || queueCallbacks.IsCloseApproved?.Invoke() == true || !ReferenceEquals(source, dataAccessor()) || plan.RecipeName != recipeNameAccessor())
                    throw new OperationCanceledException(preparationCancellation.Token);
                isPreparingQueue = false;
                return await queue.RunAsync(plan, (capturedPlan, model, token) => RunQueuedModelAsync(source, captured, capturedPlan, model, token));
            }
            finally
            {
                isPreparingQueue = false;
                if (ReferenceEquals(queuePreparationCancellation, preparationCancellation)) queuePreparationCancellation = null;
                preparationCancellation.Dispose();
                NotifyQueueChanged();
            }
        }

        public bool StopQueueCurrent() => queue.StopCurrent();

        public bool CancelQueue()
        {
            if (isPreparingQueue) { queuePreparationCancellation?.Cancel(); return true; }
            return queue.CancelAll();
        }

        public bool TrySavePendingQueueCandidate()
        {
            if (disposed || IsQueueBusy || pendingCandidate == null || queueCallbacks?.IsCloseApproved?.Invoke() == true) return false;
            PendingTrainingQueueCandidate saved = pendingCandidate;
            CommitQueueCandidate(saved);
            queue.ConfirmResultSaved(saved.Model.RunId, saved.WeightsPath, saved.Message);
            pendingCandidate = null; NotifyQueueChanged();
            return true;
        }

        public bool KeepPendingQueueCandidateFile()
        {
            if (disposed || IsQueueBusy || pendingCandidate == null || queueCallbacks?.IsCloseApproved?.Invoke() == true) return false;
            pendingCandidate = null; NotifyQueueChanged();
            return true;
        }

        private async Task<TrainingQueueModelResult> RunQueuedModelAsync(LabelingProjectData source, TrainingDatasetSnapshot captured, TrainingQueuePlan plan, TrainingQueueModel model, CancellationToken cancellationToken)
        {
            bool started = false;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (disposed || queueCallbacks.IsCloseApproved?.Invoke() == true) throw new OperationCanceledException(cancellationToken);
                if (!ReferenceEquals(source, dataAccessor()) || plan.RecipeName != recipeNameAccessor()
                    || !string.Equals(source.ProjectSettings.PythonModel.ModelEngine, captured.Data.ProjectSettings.PythonModel.ModelEngine, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(source.ProjectSettings.PythonModel.ProjectRootPath, captured.Data.ProjectSettings.PythonModel.ProjectRootPath, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The original recipe/runtime changed during the training queue.");
                if (!RecipeDatasetVersionService.CreateSnapshot(captured.Data).ContentSha256.Equals(captured.InputDatasetContentSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Captured training input changed before the next model started.");
                if (!YoloTrainingWorkflowService.CanUseQueuedTrainingModel(source.ProjectSettings.PythonModel, source.ProjectSettings.DatasetPurpose, statusAccessor(), model.ModelEngine, model.WeightsName))
                    throw new InvalidOperationException("The connected worker no longer supports the next selected training model or its local weights.");
                string startWeightsPath = Path.GetFullPath(Path.Combine(source.ProjectSettings.PythonModel.GetModelRootPath(), model.WeightsName));
                string startWeightsHash = File.Exists(startWeightsPath) ? HashingService.ComputeFileSha256(startWeightsPath, lowerCase: true) : string.Empty;
                TrainingRuntimeStartResult result = await StartAsync(new TrainingRuntimeStartRequest
                {
                    RecipeName = plan.RecipeName, RunName = model.ModelId, RunId = model.RunId, DataSnapshot = captured.Data,
                    SettingsOverride = new TrainingSettings { Epoch = plan.Epochs, ImageSize = plan.ImageSize, Batch = plan.BatchSize, Cfg = model.ModelId, Weight = model.ModelId },
                    WeightsOverride = model.WeightsName, ModelEngineOverride = model.ModelEngine, SourceDatasetVersionId = plan.DatasetVersionId, SourceDatasetContentSha256 = plan.DatasetContentSha256
                }, cancellationToken);
                if (!result.Started)
                    return new TrainingQueueModelResult(false, string.Empty, result.WorkerReady ? result.PreparationFailureMessage : result.WorkerError);
                started = true; BeginStatusPolling(model.RunId);
                var elapsed = Stopwatch.StartNew();
                while (true)
                {
                    await Task.Delay(500, cancellationToken);
                    if (disposed || queueCallbacks.IsCloseApproved?.Invoke() == true) throw new OperationCanceledException(cancellationToken);
                    TrainingRuntimeStatusSnapshot snapshot = PollStatus();
                    if (!snapshot.Status.IsClientConnected) throw new IOException("The training worker disconnected; the remaining queue was canceled.");
                    if (snapshot.HasCurrentStatus)
                        queue.ReportProgress(model.RunId, snapshot.Status.LastTrainingEpoch, snapshot.Status.LastTrainingTotalEpochs, snapshot.Status.LastTrainingProgressPercent, snapshot.Status.LastTrainingMessage,
                            snapshot.Status.LastTrainingMetricsRunId == model.RunId ? snapshot.Status.LastTrainingMetrics : null);
                    if (snapshot.TimedOut) throw new TimeoutException("The training worker did not return a status for this Run ID.");
                    if (!snapshot.IsTerminal) continue;
                    started = false;
                    if (!string.Equals(snapshot.Status.LastTrainingState, "completed", StringComparison.OrdinalIgnoreCase))
                        return new TrainingQueueModelResult(false, string.Empty, snapshot.Status.LastTrainingMessage);
                    string weightsPath = snapshot.Status.LastTrainingResultWeightsPath;
                    if (!File.Exists(weightsPath)) throw new InvalidDataException("The completed training weights file is missing.");
                    string weightsHash = HashingService.ComputeFileSha256(weightsPath, lowerCase: true);
                    if (string.Equals(Path.GetFullPath(weightsPath), startWeightsPath, StringComparison.OrdinalIgnoreCase)
                        || startWeightsHash.Length > 0 && string.Equals(weightsHash, startWeightsHash, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("The completed training result is the starting weights, not a trained output.");
                    var parameters = new TrainingRunParameters
                    {
                        ModelId = model.ModelId, WeightsName = model.WeightsName, Epochs = plan.Epochs, ImageSize = plan.ImageSize, BatchSize = plan.BatchSize,
                        OnlyReviewed = plan.OnlyReviewed, InputDatasetContentSha256 = captured.InputDatasetContentSha256, QualityReviewSha256 = captured.QualityReviewSha256,
                        InputRootPath = captured.Data.OutputRootPath, DurationSeconds = elapsed.Elapsed.TotalSeconds
                    };
                    var candidate = new PendingTrainingQueueCandidate(source, plan, model, weightsPath, weightsHash, snapshot.Status.LastTrainingMessage, parameters);
                    try { CommitQueueCandidate(candidate); }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or AggregateException)
                    {
                        pendingCandidate = candidate;
                        return new TrainingQueueModelResult(false, weightsPath, error.Message, parameters.DurationSeconds);
                    }
                    return new TrainingQueueModelResult(true, weightsPath, snapshot.Status.LastTrainingMessage, parameters.DurationSeconds);
                }
            }
            finally
            {
                if (started && !disposed && queueCallbacks.IsCloseApproved?.Invoke() != true)
                {
                    TrainingRuntimeStopResult stopped = await StopAsync(CancellationToken.None, model.RunId);
                    if (!stopped.Stopped) throw new InvalidOperationException("The current worker's stop could not be confirmed; the remaining queue was canceled.");
                }
                StopStatusPolling();
            }
        }

        private void CommitQueueCandidate(PendingTrainingQueueCandidate candidate)
        {
            if (disposed || queueCallbacks.IsCloseApproved?.Invoke() == true || !ReferenceEquals(candidate.Source, dataAccessor()) || candidate.Plan.RecipeName != recipeNameAccessor())
                throw new InvalidOperationException("The original recipe is no longer open; the training result file was retained.");
            if (!File.Exists(candidate.WeightsPath) || !HashingService.ComputeFileSha256(candidate.WeightsPath, lowerCase: true).Equals(candidate.WeightsHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Training result weights changed before metadata save.");
            LabelingProjectSettings settings = candidate.Source.ProjectSettings;
            using var registryTransaction = new ModelRegistryStateTransaction(settings.ModelRegistry);
            using var settingsTransaction = new RecipeSettingsStateTransaction(candidate.Source);
            ModelCandidate recorded = ModelRegistryService.RecordTrainingCandidate(settings.ModelRegistry, settings.PythonModel, settings.DatasetPurpose,
                candidate.Source.OutputRootPath, candidate.WeightsPath, settings.PythonModel.WeightsPath, string.Empty, "completed", 100, candidate.Message, false,
                candidate.Plan.DatasetVersionId, candidate.Plan.DatasetContentSha256, candidate.Model.RunId, candidate.Model.ModelId,
                candidate.WeightsHash, recordAdoption: false, trainingParameters: candidate.Parameters, trainingModelEngine: candidate.Model.ModelEngine);
            if (recorded?.ArtifactStatus != "verified") throw new InvalidDataException("The training result artifact could not be verified.");
            settings.TrainingGuide.LastTrainingDatasetVersionId = candidate.Plan.DatasetVersionId;
            settings.TrainingGuide.LastTrainingDatasetContentSha256 = candidate.Plan.DatasetContentSha256;
            settings.TrainingGuide.LastTrainingRunId = candidate.Model.RunId;
            settings.TrainingGuide.LastTrainingRunName = candidate.Model.ModelId;
            settings.TrainingGuide.LastTrainingState = "completed";
            settings.TrainingGuide.LastTrainingProgressPercent = 100;
            if (!queueCallbacks.SaveMetadata(candidate.Source, candidate.Plan.RecipeName)) throw new IOException("Training completed, but the recipe metadata could not be saved. The result file was retained.");
            registryTransaction.Commit(); settingsTransaction.Commit();
            queueCallbacks.HistoryUpdated?.Invoke();
        }

        private void HandleQueueChanged(TrainingQueueSnapshot snapshot) => NotifyQueueChanged(snapshot);

        private void NotifyQueueChanged(TrainingQueueSnapshot snapshot = null)
        {
            if (disposed) return;
            queueCallbacks?.CommandStateChanged?.Invoke();
            QueueChanged?.Invoke(snapshot ?? queue.Snapshot);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true; queuePreparationCancellation?.Cancel(); queue.Dispose();
            queue.Changed -= HandleQueueChanged; QueueChanged = null;
        }

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

            LabelingProjectData data = request.DataSnapshot ?? dataAccessor();
            PythonModelCommunication communication = communicationAccessor();
            YoloTrainingWorkflowService trainingWorkflow = trainingWorkflowAccessor();
            bool started = trainingWorkflow.TryStartTraining(
                data,
                communication,
                request.RunName,
                string.IsNullOrWhiteSpace(request.RecipeName) ? recipeNameAccessor() : request.RecipeName,
                request.RunId,
                request.SettingsOverride,
                request.WeightsOverride,
                request.SourceDatasetVersionId,
                request.SourceDatasetContentSha256,
                updateRecipeManifest: request.DataSnapshot == null,
                modelEngineOverride: request.ModelEngineOverride);
            return TrainingRuntimeStartResult.Completed(
                started,
                readyStatus,
                trainingWorkflow.LastPreparationFailureMessage);
        }

        public Task<TrainingRuntimeStopResult> StopAsync(CancellationToken cancellationToken, string runId = null)
        {
            return Task.Run(
                () =>
                {
                    bool stopped = trainingWorkflowAccessor().TryStopTraining(
                        communicationAccessor(),
                        processAccessor(),
                        cancellationToken,
                        runId);
                    if (stopped)
                    {
                        IsTrainingWorkflowRunning = false;
                        StopStatusPolling();
                    }

                    return new TrainingRuntimeStopResult(stopped, statusAccessor() ?? new PythonCommunicationStatus());
                },
                cancellationToken);
        }

        public void BeginStatusPolling(string runId = "")
        {
            IsTrainingWorkflowRunning = true;
            statusPollingStartedUtc = DateTime.UtcNow;
            statusPollingRunId = runId ?? string.Empty;
            RequestStatusSnapshot();
        }

        public void StopStatusPolling()
        {
            statusPollingStartedUtc = DateTime.MinValue;
            statusPollingRunId = string.Empty;
        }

        public void Reset()
        {
            CancelQueue();
            IsTrainingWorkflowRunning = false;
            StopStatusPolling();
        }

        public TrainingRuntimeStatusSnapshot PollStatus()
        {
            RequestStatusSnapshot();
            PythonCommunicationStatus status = statusAccessor() ?? new PythonCommunicationStatus();
            bool hasStatus = TrainingProgressPresentationService.HasTrainingStatus(status);
            string expectedRunId = statusPollingRunId.Length > 0 ? statusPollingRunId
                : dataAccessor()?.ProjectSettings?.TrainingGuide?.LastTrainingRunId ?? string.Empty;
            bool hasCurrentStatus = hasStatus && IsCurrent(status)
                && (string.IsNullOrWhiteSpace(status.LastTrainingRunId) ? statusPollingRunId.Length == 0
                    : string.Equals(status.LastTrainingRunId, expectedRunId, StringComparison.Ordinal));
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

    public sealed class TrainingQueueRuntimeCallbacks
    {
        public Func<bool> CanStart { get; init; }
        public Func<bool> IsCloseApproved { get; init; }
        public Func<LabelingProjectData, string, bool> SaveMetadata { get; init; }
        public Action CommandStateChanged { get; init; }
        public Action HistoryUpdated { get; init; }
    }

    internal sealed record PendingTrainingQueueCandidate(LabelingProjectData Source, TrainingQueuePlan Plan, TrainingQueueModel Model, string WeightsPath, string WeightsHash, string Message, TrainingRunParameters Parameters);

    public sealed class TrainingRuntimeStartRequest
    {
        public int WorkerReadyTimeoutMilliseconds { get; init; } = 30_000;

        public string RecipeName { get; init; } = string.Empty;

        public string RunName { get; init; } = string.Empty;

        public string RunId { get; init; } = string.Empty;
        public LabelingProjectData DataSnapshot { get; init; }
        public TrainingSettings SettingsOverride { get; init; }
        public string WeightsOverride { get; init; } = string.Empty;
        public string ModelEngineOverride { get; init; } = string.Empty;
        public string SourceDatasetVersionId { get; init; } = string.Empty;
        public string SourceDatasetContentSha256 { get; init; } = string.Empty;
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
