using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    public sealed record TrainingQueueModel(string ModelId, string ModelEngine, string WeightsName, string RunId);

    public sealed class TrainingQueuePlan
    {
        public TrainingQueuePlan(string recipeName, string datasetVersionId, string datasetContentSha256, IEnumerable<TrainingQueueModel> models, int epochs, int imageSize, int batchSize, bool onlyReviewed)
        {
            TrainingQueueModel[] selected = models?.ToArray() ?? Array.Empty<TrainingQueueModel>();
            if (string.IsNullOrWhiteSpace(recipeName) || string.IsNullOrWhiteSpace(datasetVersionId) || datasetContentSha256?.Length != 64)
                throw new ArgumentException("A recipe and captured dataset identity are required.");
            if (selected.Length == 0 || selected.Any(model => model == null || string.IsNullOrWhiteSpace(model.ModelId) || string.IsNullOrWhiteSpace(model.ModelEngine) || string.IsNullOrWhiteSpace(model.RunId))
                || selected.Select(model => model.ModelId).Distinct(StringComparer.Ordinal).Count() != selected.Length
                || selected.Select(model => model.RunId).Distinct(StringComparer.Ordinal).Count() != selected.Length)
                throw new ArgumentException("Select distinct models with distinct training run IDs.");
            if (epochs < 1 || epochs > 1000 || imageSize < 64 || imageSize > 2048 || imageSize % 32 != 0 || batchSize < 1 || batchSize > 128)
                throw new ArgumentOutOfRangeException(nameof(epochs), "Training settings are outside the supported form ranges.");
            RecipeName = recipeName; DatasetVersionId = datasetVersionId; DatasetContentSha256 = datasetContentSha256;
            Models = Array.AsReadOnly(selected); Epochs = epochs; ImageSize = imageSize; BatchSize = batchSize; OnlyReviewed = onlyReviewed;
        }

        public string RecipeName { get; }
        public string DatasetVersionId { get; }
        public string DatasetContentSha256 { get; }
        public IReadOnlyList<TrainingQueueModel> Models { get; }
        public int Epochs { get; }
        public int ImageSize { get; }
        public int BatchSize { get; }
        public bool OnlyReviewed { get; }
    }

    public enum TrainingQueueItemState { Waiting, Running, Completed, Canceled, Failed }

    public sealed record TrainingEpochMetric(int Epoch, double? TrainLoss, double? ValidationLoss, string ValidationMetricKey, double? ValidationMetricValue)
    {
        public bool HasValue => TrainLoss.HasValue || ValidationLoss.HasValue || ValidationMetricValue.HasValue;
        public TrainingEpochMetric Normalize(int maximumEpoch)
        {
            if (Epoch < 1 || Epoch > Math.Min(1000, maximumEpoch)) return null;
            string key = ValidationMetricKey is "map50" or "maskMap50" or "top1Accuracy" ? ValidationMetricKey : string.Empty;
            return this with { TrainLoss = FiniteLoss(TrainLoss), ValidationLoss = FiniteLoss(ValidationLoss), ValidationMetricKey = key,
                ValidationMetricValue = key.Length > 0 && ValidationMetricValue is >= 0 and <= 1 && double.IsFinite(ValidationMetricValue.Value) ? ValidationMetricValue : null };
        }
        private static double? FiniteLoss(double? value) => value is >= 0 && double.IsFinite(value.Value) ? value : null;
    }

    public sealed record TrainingQueueItemSnapshot(TrainingQueueModel Model, TrainingQueueItemState State, string WeightsPath, string Message, double? DurationSeconds, int? Epoch, int? TotalEpochs, double? ProgressPercent, IReadOnlyList<TrainingEpochMetric> Metrics = null);

    public sealed record TrainingQueueSnapshot(TrainingQueuePlan Plan, bool IsRunning, IReadOnlyList<TrainingQueueItemSnapshot> Items)
    {
        public TrainingQueueItemSnapshot Current => Items.FirstOrDefault(item => item.State == TrainingQueueItemState.Running);
        public int CompletedCount => Items.Count(item => item.State == TrainingQueueItemState.Completed);
        public int WaitingCount => Items.Count(item => item.State == TrainingQueueItemState.Waiting);
    }

    public sealed record TrainingQueueModelResult(bool Succeeded, string WeightsPath, string Message, double? DurationSeconds = null);

    // The queue owns ordering and cancellation. The existing training runtime owns
    // worker commands, process lifetime, dataset preparation and terminal evidence.
    public sealed class TrainingQueueWorkflowService : IDisposable
    {
        private readonly object sync = new();
        private TrainingQueuePlan plan;
        private TrainingQueueItemSnapshot[] items = Array.Empty<TrainingQueueItemSnapshot>();
        private CancellationTokenSource currentCancellation;
        private bool isRunning;
        private bool cancelRemaining;
        private bool disposed;

        public event Action<TrainingQueueSnapshot> Changed;

        public TrainingQueueSnapshot Snapshot
        {
            get { lock (sync) return CaptureSnapshot(); }
        }

        public async Task<bool> RunAsync(TrainingQueuePlan requestedPlan, Func<TrainingQueuePlan, TrainingQueueModel, CancellationToken, Task<TrainingQueueModelResult>> runModelAsync)
        {
            ArgumentNullException.ThrowIfNull(requestedPlan); ArgumentNullException.ThrowIfNull(runModelAsync);
            lock (sync)
            {
                if (disposed || isRunning) return false;
                plan = requestedPlan;
                items = plan.Models.Select(model => new TrainingQueueItemSnapshot(model, TrainingQueueItemState.Waiting, string.Empty, string.Empty, null, null, null, null, Array.Empty<TrainingEpochMetric>())).ToArray();
                isRunning = true; cancelRemaining = false;
            }
            try
            {
                for (int index = 0; index < items.Length; index++)
                {
                    CancellationTokenSource cancellation;
                    TrainingQueueModel model;
                    lock (sync)
                    {
                        if (cancelRemaining || disposed) break;
                        model = items[index].Model;
                        cancellation = new CancellationTokenSource();
                        currentCancellation = cancellation;
                        items[index] = items[index] with { State = TrainingQueueItemState.Running };
                    }
                    try
                    {
                        Publish();
                        TrainingQueueModelResult result = await runModelAsync(plan, model, cancellation.Token);
                        if (result == null) throw new InvalidOperationException("The training runtime returned no terminal result.");
                        lock (sync)
                        {
                            TrainingQueueItemState state = result.Succeeded ? TrainingQueueItemState.Completed
                                : cancellation.IsCancellationRequested ? TrainingQueueItemState.Canceled : TrainingQueueItemState.Failed;
                            items[index] = items[index] with { State = state, WeightsPath = result.WeightsPath ?? string.Empty, Message = result.Message ?? string.Empty,
                                DurationSeconds = result.DurationSeconds is >= 0 && double.IsFinite(result.DurationSeconds.Value) ? result.DurationSeconds : null };
                            if (state == TrainingQueueItemState.Failed) cancelRemaining = true;
                        }
                    }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                    {
                        lock (sync) items[index] = items[index] with { State = TrainingQueueItemState.Canceled };
                    }
                    catch (Exception error)
                    {
                        lock (sync) { items[index] = items[index] with { State = TrainingQueueItemState.Failed, Message = error.Message }; cancelRemaining = true; }
                    }
                    finally
                    {
                        lock (sync) { if (ReferenceEquals(currentCancellation, cancellation)) currentCancellation = null; }
                        cancellation.Dispose();
                    }
                    Publish();
                }
                return true;
            }
            finally
            {
                lock (sync)
                {
                    for (int index = 0; index < items.Length; index++)
                        if (items[index].State == TrainingQueueItemState.Waiting) items[index] = items[index] with { State = TrainingQueueItemState.Canceled };
                    isRunning = false;
                }
                Publish();
            }
        }

        public bool StopCurrent()
        {
            lock (sync)
            {
                if (!isRunning || currentCancellation == null) return false;
                currentCancellation.Cancel();
                return true;
            }
        }

        public bool CancelAll()
        {
            lock (sync)
            {
                if (!isRunning) return false;
                cancelRemaining = true;
                currentCancellation?.Cancel();
                return true;
            }
        }

        public bool ConfirmResultSaved(string runId, string weightsPath, string message)
        {
            lock (sync)
            {
                int index = Array.FindIndex(items, item => item.Model.RunId == runId && item.State == TrainingQueueItemState.Failed
                    && !string.IsNullOrWhiteSpace(item.WeightsPath) && item.WeightsPath == weightsPath);
                if (disposed || isRunning || index < 0) return false;
                items[index] = items[index] with { State = TrainingQueueItemState.Completed, Message = message ?? string.Empty };
            }
            Publish();
            return true;
        }

        public void ReportProgress(string runId, int? epoch, int? totalEpochs, double? percent, string message, IReadOnlyList<TrainingEpochMetric> metrics = null)
        {
            lock (sync)
            {
                int index = Array.FindIndex(items, item => item.State == TrainingQueueItemState.Running && item.Model.RunId == runId);
                if (disposed || !isRunning || index < 0) return;
                IReadOnlyList<TrainingEpochMetric> received = items[index].Metrics ?? Array.Empty<TrainingEpochMetric>();
                if (metrics != null) received = Array.AsReadOnly(received.Concat(metrics).Where(metric => metric != null).Select(metric => metric.Normalize(plan.Epochs))
                    .Where(metric => metric != null).GroupBy(metric => metric.Epoch).Select(group => group.Last()).OrderBy(metric => metric.Epoch).ToArray());
                items[index] = items[index] with { Epoch = epoch is >= 0 ? epoch : null, TotalEpochs = totalEpochs is > 0 ? totalEpochs : null,
                    ProgressPercent = percent.HasValue && double.IsFinite(percent.Value) && percent is >= 0 and <= 100 ? percent : null, Message = message ?? string.Empty, Metrics = received };
            }
            Publish();
        }

        public void Dispose()
        {
            lock (sync) { if (disposed) return; disposed = true; cancelRemaining = true; currentCancellation?.Cancel(); }
        }

        private TrainingQueueSnapshot CaptureSnapshot() => new(plan, isRunning, Array.AsReadOnly(items.ToArray()));

        private void Publish()
        {
            TrainingQueueSnapshot snapshot;
            lock (sync) { if (disposed) return; snapshot = CaptureSnapshot(); }
            Changed?.Invoke(snapshot);
        }
    }
}
