using MvcVisionSystem._1._Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MvcVisionSystem
{
    // Live training view below the curves (v2 prototype liveDetails()): the latest recorded epochs as a table with
    // the best epoch marked, and a summary of the best value, each finished same-version model at the same epoch,
    // the time per epoch, the remaining estimate and the next queued model. Window-free; the workspace ViewModel
    // supplies the received samples, the finished peers and the measured epoch rate, and binds the snapshot.
    internal static class TrainingLivePresentationService
    {
        internal const int TableRows = 8;

        internal static TrainingLiveSnapshot Build(TrainingLiveRequest request)
        {
            TrainingEpochMetric[] samples = (request.Metrics ?? Array.Empty<TrainingEpochMetric>())
                .Where(metric => metric != null).Select(metric => metric.Normalize(Math.Max(1, request.TotalEpochs))).Where(metric => metric != null && metric.ValidationMetricValue.HasValue)
                .GroupBy(metric => metric.Epoch).Select(group => group.Last()).OrderBy(metric => metric.Epoch).ToArray();
            if (samples.Length == 0) return TrainingLiveSnapshot.Empty;

            string key = samples[^1].ValidationMetricKey;
            TrainingEpochMetric best = samples.Where(metric => metric.ValidationMetricKey == key).OrderByDescending(metric => metric.ValidationMetricValue).ThenBy(metric => metric.Epoch).First();
            TrainingEpochMetric latest = samples[^1];
            string Loss(double? value) => value.HasValue ? value.Value.ToString("0.00", CultureInfo.CurrentCulture) : "—";
            string Metric(double value) => value.ToString("0.000", CultureInfo.CurrentCulture);
            TrainingLiveEpochRow[] rows = samples.Reverse().Take(TableRows)
                .Select(metric => new TrainingLiveEpochRow(metric.Epoch.ToString(CultureInfo.CurrentCulture), Loss(metric.TrainLoss), Loss(metric.ValidationLoss), Metric(metric.ValidationMetricValue.Value), ReferenceEquals(metric, best)))
                .ToArray();

            string metricName = TrainingWorkspaceViewModel.T("Metric." + key);
            var facts = new List<TrainingLiveFact>
            {
                new(TrainingWorkspaceViewModel.F("LiveBest", metricName), Metric(best.ValidationMetricValue.Value), TrainingWorkspaceViewModel.F("LiveEpochDetail", best.Epoch), TrainingLiveTone.Muted, true)
            };
            foreach (TrainingLivePeer peer in request.Peers ?? Array.Empty<TrainingLivePeer>())
            {
                // The peer's value at the last sampled epoch not after the running model's latest epoch.
                double? value = peer.Curve?.Where(metric => metric != null && metric.Epoch <= latest.Epoch && metric.ValidationMetricKey == key && metric.ValidationMetricValue is >= 0 and <= 1)
                    .OrderBy(metric => metric.Epoch).LastOrDefault()?.ValidationMetricValue;
                if (!value.HasValue) continue;
                double delta = latest.ValidationMetricValue.Value - value.Value;
                string signed = (delta >= 0 ? "+" : "−") + Math.Abs(delta).ToString("0.000", CultureInfo.CurrentCulture);
                facts.Add(new(TrainingWorkspaceViewModel.F("LivePeerAt", latest.Epoch, peer.Name), Metric(value.Value), TrainingWorkspaceViewModel.F("LivePeerDelta", signed),
                    delta >= 0 ? TrainingLiveTone.Good : TrainingLiveTone.Warning, false));
            }

            bool measured = request.SecondsPerEpoch is > 0 && double.IsFinite(request.SecondsPerEpoch.Value);
            facts.Add(new(TrainingWorkspaceViewModel.T("LiveSecondsPerEpoch"),
                measured ? TrainingWorkspaceViewModel.F("LiveSeconds", request.SecondsPerEpoch.Value) : TrainingWorkspaceViewModel.T("LiveMeasuring"), string.Empty, TrainingLiveTone.Muted, false));
            if (measured)
            {
                // The queued models are assumed to run at the measured rate of the current one.
                int currentEpoch = Math.Clamp(request.CurrentEpoch ?? latest.Epoch, 0, request.TotalEpochs);
                double seconds = ((request.TotalEpochs - currentEpoch) + Math.Max(0, request.WaitingCount) * request.TotalEpochs) * request.SecondsPerEpoch.Value;
                facts.Add(new(TrainingWorkspaceViewModel.T("LiveRemaining"), TrainingWorkspaceViewModel.F("LiveRemainingValue", Math.Max(1, (int)Math.Round(seconds / 60))),
                    request.WaitingCount > 0 ? TrainingWorkspaceViewModel.F("LiveRemainingQueue", request.WaitingCount) : string.Empty, TrainingLiveTone.Muted, false));
            }
            facts.Add(string.IsNullOrWhiteSpace(request.NextModelName)
                ? new(TrainingWorkspaceViewModel.T("LiveNextModel"), string.Empty, TrainingWorkspaceViewModel.T("LiveNoNext"), TrainingLiveTone.Muted, false)
                : new(TrainingWorkspaceViewModel.T("LiveNextModel"), request.NextModelName, TrainingWorkspaceViewModel.F("LiveNextValue", request.TotalEpochs, request.ImageSize), TrainingLiveTone.Muted, false));
            return new TrainingLiveSnapshot(rows, facts, metricName);
        }
    }

    internal sealed record TrainingLivePeer(string Name, IReadOnlyList<TrainingEpochMetric> Curve);

    internal sealed record TrainingLiveRequest(IReadOnlyList<TrainingEpochMetric> Metrics, IReadOnlyList<TrainingLivePeer> Peers, int TotalEpochs, int? CurrentEpoch,
        double? SecondsPerEpoch, int WaitingCount, string NextModelName, int ImageSize);

    public enum TrainingLiveTone { Muted, Good, Warning }

    public sealed record TrainingLiveEpochRow(string Epoch, string TrainLoss, string ValidationLoss, string Metric, bool IsBest);

    // Label, primary value, then a detail shown muted, green (this model ahead) or amber (behind).
    public sealed record TrainingLiveFact(string Label, string Value, string Detail, TrainingLiveTone Tone, bool IsEmphasized)
    {
        public bool IsGood => Tone == TrainingLiveTone.Good;
        public bool IsWarning => Tone == TrainingLiveTone.Warning;
    }

    internal sealed record TrainingLiveSnapshot(IReadOnlyList<TrainingLiveEpochRow> Rows, IReadOnlyList<TrainingLiveFact> Facts, string MetricName)
    {
        internal static readonly TrainingLiveSnapshot Empty = new(Array.Empty<TrainingLiveEpochRow>(), Array.Empty<TrainingLiveFact>(), string.Empty);
        internal bool HasRows => Rows.Count > 0;
    }
}
