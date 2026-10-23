using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MvcVisionSystem._1._Core
{
    public enum LabelSuspicionKind { Missing, Position, Class, Unnecessary, AnomalyVerdict }
    public enum PredictionTrainingMembership { Unknown, Training, NotTraining }
    public sealed record LabelSuspicionAnnotation(string Identity, string ClassName, double X, double Y, double Width, double Height,
        int OccurrenceIndex = 0, string SegmentObjectId = "");
    public sealed record LabelSuspicionPrediction(string ArtifactIdentity, bool IsInspectionModel, IReadOnlyList<YoloWorkerSmokeCandidate> Candidates,
        PredictionTrainingMembership TrainingMembership = PredictionTrainingMembership.Unknown, double? SavedAnomalyThreshold = null,
        string RunIdentity = "", string ImageIdentity = "", string SettingsIdentity = "");
    public sealed record LabelSuspicionModelIdentity(string RunIdentity, string ArtifactIdentity);
    public sealed record LabelSuspicionGroundTruth(IReadOnlyList<LabelSuspicionAnnotation> Labels, string ContentIdentity, string UnavailableReason = "", string Diagnostic = "", bool? AnomalyVerdict = null)
    {
        public bool IsAvailable => string.IsNullOrEmpty(UnavailableReason) && !string.IsNullOrWhiteSpace(ContentIdentity);
    }
    public sealed record LabelSuspicionItem(LabelSuspicionKind Kind, LabelSuspicionAnnotation Label, YoloWorkerSmokeCandidate Prediction);
    public sealed record LabelSuspicionReport(IReadOnlyList<LabelSuspicionItem> Items, string UnavailableReason, bool HasTrainingImageWarning, bool HasUnknownTrainingWarning)
    {
        public bool CanEvaluate => string.IsNullOrEmpty(UnavailableReason);
        public string EvidenceIdentity { get; init; } = string.Empty;
        public string Diagnostic { get; init; } = string.Empty;
        public bool HasSingleModelWarning { get; init; }
    }

    // Review settings follow the approved initial proposal; they are not calibrated model accuracy claims.
    // The caller supplies saved labels and current explicit model results. This policy never mutates either.
    public sealed class LabelSuspicionReviewService
    {
        // Current registry identities and content hashes are supplied by the I/O owner.
        // A cached prediction is evidence only for the exact trained artifact, image and request settings.
        public LabelSuspicionReport EvaluateVerified(LabelSuspicionGroundTruth groundTruth, string imageIdentity,
            LabelSuspicionModelIdentity inspectionModel, IReadOnlyList<LabelSuspicionModelIdentity> currentModels,
            IReadOnlyList<LabelSuspicionPrediction> predictions, bool anomaly = false)
        {
            LabelSuspicionPrediction[] models = (predictions ?? Array.Empty<LabelSuspicionPrediction>()).ToArray();
            LabelSuspicionReport Unavailable(string reason, string diagnostic = "") => new(Array.Empty<LabelSuspicionItem>(), reason,
                models.Any(model => model?.TrainingMembership == PredictionTrainingMembership.Training),
                models.Any(model => model?.TrainingMembership == PredictionTrainingMembership.Unknown)) { Diagnostic = diagnostic };
            if (groundTruth?.IsAvailable != true) return Unavailable(groundTruth?.UnavailableReason ?? "SavedLabelsUnavailable", groundTruth?.Diagnostic ?? string.Empty);
            if (string.IsNullOrWhiteSpace(imageIdentity)) return Unavailable("ImageIdentityUnavailable");
            if (inspectionModel == null || string.IsNullOrWhiteSpace(inspectionModel.RunIdentity) || string.IsNullOrWhiteSpace(inspectionModel.ArtifactIdentity))
                return Unavailable("TrainedModelUnavailable");
            if (models.Length == 0) return Unavailable("InspectionPredictionUnavailable");
            if (models.Any(model => model == null || model.Candidates == null || string.IsNullOrWhiteSpace(model.RunIdentity) || string.IsNullOrWhiteSpace(model.ArtifactIdentity)
                || string.IsNullOrWhiteSpace(model.SettingsIdentity) || !string.Equals(model.ImageIdentity, imageIdentity, StringComparison.Ordinal)
                || !(currentModels ?? Array.Empty<LabelSuspicionModelIdentity>()).Any(current => current != null
                    && current.RunIdentity == model.RunIdentity && current.ArtifactIdentity == model.ArtifactIdentity)))
                return Unavailable("PredictionProvenanceMismatch");
            if (models.Count(model => model.IsInspectionModel) != 1 || !models.Any(model => model.IsInspectionModel
                && model.RunIdentity == inspectionModel.RunIdentity && model.ArtifactIdentity == inspectionModel.ArtifactIdentity))
                return Unavailable("InspectionPredictionUnavailable");
            LabelSuspicionReport report = Evaluate(groundTruth.Labels, true, models, groundTruth.AnomalyVerdict, anomaly);
            if (!report.CanEvaluate) return report;
            string evidence = JsonSerializer.Serialize(new
            {
                Policy = "label-suspicion-v1:.25,.7,.8,.2,.6,.5,.3,.1", GroundTruth = groundTruth.ContentIdentity, Image = imageIdentity,
                Predictions = models.OrderBy(model => model.ArtifactIdentity, StringComparer.Ordinal).Select(model => new
                {
                    model.RunIdentity, model.ArtifactIdentity, model.SettingsIdentity, model.IsInspectionModel, model.SavedAnomalyThreshold,
                    Candidates = model.Candidates.Select(candidate => new { candidate.ClassName, candidate.X, candidate.Y, candidate.Width, candidate.Height,
                        candidate.Confidence, candidate.ImageLevel, candidate.AnomalyScore, candidate.CandidateType, candidate.PredictionType,
                        candidate.SegmentationType, candidate.PolygonPoints, candidate.NormalizedPolygonPoints })
                })
            });
            return report with { EvidenceIdentity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(evidence))) };
        }

        public LabelSuspicionReport Evaluate(IReadOnlyList<LabelSuspicionAnnotation> savedLabels, bool savedLabelsAvailable,
            IReadOnlyList<LabelSuspicionPrediction> predictions, bool? savedAnomalyVerdict = null, bool anomaly = false)
        {
            LabelSuspicionPrediction[] models = (predictions ?? Array.Empty<LabelSuspicionPrediction>())
                .Where(model => model != null && !string.IsNullOrWhiteSpace(model.ArtifactIdentity) && model.Candidates != null)
                .GroupBy(model => model.ArtifactIdentity, StringComparer.Ordinal).Select(group => group.FirstOrDefault(model => model.IsInspectionModel) ?? group.First()).ToArray();
            LabelSuspicionPrediction inspection = models.FirstOrDefault(model => model.IsInspectionModel);
            bool training = models.Any(model => model.TrainingMembership == PredictionTrainingMembership.Training);
            bool unknown = models.Any(model => model.TrainingMembership == PredictionTrainingMembership.Unknown);
            LabelSuspicionReport Unavailable(string reason) => new(Array.Empty<LabelSuspicionItem>(), reason, training, unknown);
            if (!savedLabelsAvailable) return Unavailable("SavedLabelsUnavailable");
            if (inspection == null) return Unavailable("InspectionPredictionUnavailable");
            var items = new List<LabelSuspicionItem>();
            if (anomaly)
            {
                if (!savedAnomalyVerdict.HasValue) return Unavailable("SavedVerdictUnavailable");
                var scores = models.Select(model => (Model: model, Candidate: model.Candidates.FirstOrDefault(candidate => candidate?.ImageLevel == true))).ToArray();
                if (scores.Any(item => !item.Model.SavedAnomalyThreshold.HasValue || !double.IsFinite(item.Model.SavedAnomalyThreshold.Value)
                    || item.Candidate?.AnomalyScore.HasValue != true || !double.IsFinite(item.Candidate.AnomalyScore.Value)))
                    return Unavailable("SavedAnomalyCriterionUnavailable");
                var current = scores.First(item => item.Model.IsInspectionModel);
                if (scores.All(item => (item.Candidate.AnomalyScore.Value >= item.Model.SavedAnomalyThreshold.Value) != savedAnomalyVerdict.Value)
                    && Math.Abs(current.Candidate.AnomalyScore.Value - current.Model.SavedAnomalyThreshold.Value) >= .1)
                    items.Add(new(LabelSuspicionKind.AnomalyVerdict, null, current.Candidate));
                return new(items, string.Empty, training, unknown);
            }

            LabelSuspicionAnnotation[] labels = (savedLabels ?? Array.Empty<LabelSuspicionAnnotation>()).ToArray();
            if (labels.Any(label => label == null || string.IsNullOrWhiteSpace(label.Identity) || string.IsNullOrWhiteSpace(label.ClassName) || !ValidBounds(label.X, label.Y, label.Width, label.Height)))
                return Unavailable("SavedLabelsMalformed");
            if (models.Any(model => model.Candidates.Any(candidate => candidate == null || candidate.ImageLevel || !IsObject(candidate))))
                return Unavailable("PredictionMalformed");
            foreach (YoloWorkerSmokeCandidate candidate in inspection.Candidates.Where(IsObject))
            {
                bool corroborated = models.Any(model => !model.IsInspectionModel && model.Candidates.Any(other => IsObject(other)
                    && other.Confidence >= .25 && SameClass(candidate.ClassName, other.ClassName) && IoU(candidate, other) >= .5));
                if (candidate.Confidence < .25) continue;
                var overlap = labels.Select(label => (Label: label, IoU: IoU(label, candidate))).OrderByDescending(item => item.IoU).ToArray();
                if (overlap.Length == 0 || overlap[0].IoU < .2)
                {
                    if (candidate.Confidence >= (corroborated ? .7 : .8)) items.Add(new(LabelSuspicionKind.Missing, null, candidate));
                    continue;
                }
                var wrongClass = overlap.FirstOrDefault(item => item.IoU >= .5 && !SameClass(item.Label.ClassName, candidate.ClassName));
                if (wrongClass.Label != null)
                {
                    items.Add(new(LabelSuspicionKind.Class, wrongClass.Label, candidate));
                    continue;
                }
                var sameClass = overlap.FirstOrDefault(item => SameClass(item.Label.ClassName, candidate.ClassName));
                if (sameClass.Label != null && sameClass.IoU >= .2 && sameClass.IoU < .6)
                    items.Add(new(LabelSuspicionKind.Position, sameClass.Label, candidate));
            }
            if (models.Length >= 2)
                foreach (LabelSuspicionAnnotation label in labels)
                    if (!models.Any(model => model.Candidates.Any(candidate => IsObject(candidate) && candidate.Confidence >= .25 && IoU(label, candidate) >= .3)))
                        items.Add(new(LabelSuspicionKind.Unnecessary, label, null));
            return new(items, string.Empty, training, unknown) { HasSingleModelWarning = models.Length < 2 };
        }

        public static double? GetUncertainty(IReadOnlyList<YoloWorkerSmokeCandidate> candidates, double? savedAnomalyThreshold = null)
        {
            if (candidates == null) return null;
            YoloWorkerSmokeCandidate image = candidates.FirstOrDefault(candidate => candidate?.ImageLevel == true);
            if (image != null) return image.AnomalyScore.HasValue && savedAnomalyThreshold.HasValue && double.IsFinite(image.AnomalyScore.Value)
                && double.IsFinite(savedAnomalyThreshold.Value) ? -Math.Abs(image.AnomalyScore.Value - savedAnomalyThreshold.Value) : null;
            double[] values = candidates.Where(IsObject).Select(candidate => 1 - 2 * Math.Abs(candidate.Confidence - .5)).ToArray();
            return values.Length == 0 ? 0 : values.Max();
        }

        public static double? GetDisagreement(IReadOnlyList<LabelSuspicionPrediction> predictions)
        {
            LabelSuspicionPrediction[] models = (predictions ?? Array.Empty<LabelSuspicionPrediction>())
                .Where(model => model != null && !string.IsNullOrWhiteSpace(model.ArtifactIdentity) && model.Candidates != null)
                .GroupBy(model => model.ArtifactIdentity, StringComparer.Ordinal).Select(group => group.First()).ToArray();
            if (models.Length < 2) return null;
            if (models.Any(model => model.Candidates.Any(candidate => candidate?.ImageLevel == true)))
            {
                var verdicts = models.Select(model => (Model: model, Candidate: model.Candidates.FirstOrDefault(candidate => candidate?.ImageLevel == true))).ToArray();
                if (verdicts.Any(item => item.Candidate?.AnomalyScore == null || !item.Model.SavedAnomalyThreshold.HasValue
                    || !double.IsFinite(item.Candidate.AnomalyScore.Value) || !double.IsFinite(item.Model.SavedAnomalyThreshold.Value))) return null;
                return verdicts.Select(item => item.Candidate.AnomalyScore.Value >= item.Model.SavedAnomalyThreshold.Value).Distinct().Count() > 1 ? 1 : 0;
            }
            double disagreement = 0;
            for (int left = 0; left < models.Length; left++)
                for (int right = left + 1; right < models.Length; right++)
                {
                    YoloWorkerSmokeCandidate[] first = models[left].Candidates.Where(IsObject).ToArray();
                    var remaining = models[right].Candidates.Where(IsObject).ToList();
                    int total = first.Length + remaining.Count, matched = 0;
                    foreach (YoloWorkerSmokeCandidate candidate in first)
                    {
                        YoloWorkerSmokeCandidate match = remaining.Where(other => SameClass(candidate.ClassName, other.ClassName) && IoU(candidate, other) >= .5)
                            .OrderByDescending(other => IoU(candidate, other)).FirstOrDefault();
                        if (match == null) continue;
                        matched++; remaining.Remove(match);
                    }
                    disagreement = Math.Max(disagreement, total == 0 ? 0 : 1 - 2D * matched / total);
                }
            return disagreement;
        }

        private static bool SameClass(string left, string right) => string.Equals(left?.Trim(), right?.Trim(), StringComparison.Ordinal);
        private static bool IsObject(YoloWorkerSmokeCandidate candidate) => candidate != null && !candidate.ImageLevel && !string.IsNullOrWhiteSpace(candidate.ClassName) && double.IsFinite(candidate.Confidence)
            && candidate.Confidence >= 0 && candidate.Confidence <= 1 && ValidBounds(candidate.X, candidate.Y, candidate.Width, candidate.Height)
            && !(candidate.PolygonPoints?.Any(point => point == null || !float.IsFinite(point.X) || !float.IsFinite(point.Y)) ?? false)
            && !(candidate.NormalizedPolygonPoints?.Any(point => point == null || !float.IsFinite(point.X) || !float.IsFinite(point.Y)) ?? false);
        private static bool ValidBounds(double x, double y, double width, double height) => double.IsFinite(x) && double.IsFinite(y) && double.IsFinite(width)
            && double.IsFinite(height) && width > 0 && height > 0 && double.IsFinite(x + width) && double.IsFinite(y + height);
        private static double IoU(LabelSuspicionAnnotation label, YoloWorkerSmokeCandidate candidate)
            => IoU(label.X, label.Y, label.Width, label.Height, candidate.X, candidate.Y, candidate.Width, candidate.Height);
        private static double IoU(YoloWorkerSmokeCandidate left, YoloWorkerSmokeCandidate right)
            => IoU(left.X, left.Y, left.Width, left.Height, right.X, right.Y, right.Width, right.Height);
        private static double IoU(double x, double y, double width, double height, double bx, double by, double bw, double bh)
        {
            double intersection = Math.Max(0, Math.Min(x + width, bx + bw) - Math.Max(x, bx)) * Math.Max(0, Math.Min(y + height, by + bh) - Math.Max(y, by));
            double union = width * height + bw * bh - intersection;
            return union > 0 && double.IsFinite(union) ? intersection / union : 0;
        }
    }
}
