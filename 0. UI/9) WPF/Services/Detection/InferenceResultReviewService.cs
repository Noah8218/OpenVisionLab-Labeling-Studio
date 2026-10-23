using MvcVisionSystem._1._Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace MvcVisionSystem
{
    // Result review is session state. It never writes labels, quality reviews or recipe files.
    public sealed class InferenceResultReviewService
    {
        private readonly Dictionary<(string Engine, string Weights, string Root), (Dictionary<string, InferenceResultReviewEntry> Results, List<string> Order)> modelResults = new();
        private Dictionary<string, InferenceResultReviewEntry> results = new(StringComparer.OrdinalIgnoreCase);
        private List<string> executionOrder = new();
        private (string Engine, string Weights, string Root) selectedModel;
        private string scope = string.Empty;
        private Func<InferenceResultReviewEntry, ModelDecisionResult> decisionEvaluator;
        private Func<(Func<InferenceResultReviewEntry, ModelDecisionResult> Evaluate, Func<bool> Validate)> decisionEvaluationFactory;

        public void ConfigureDecisionEvaluation(Func<InferenceResultReviewEntry, ModelDecisionResult> evaluator, Func<(Func<InferenceResultReviewEntry, ModelDecisionResult> Evaluate, Func<bool> Validate)> factory = null)
        {
            decisionEvaluator = evaluator;
            decisionEvaluationFactory = factory;
        }

        public ModelDecisionResult EvaluateDecision(InferenceResultReviewEntry entry)
            => decisionEvaluator?.Invoke(entry) ?? new("not_configured", null, Array.Empty<int>());

        public ModelDecisionResult GetDecisionProjection(InferenceResultReviewEntry entry)
            => entry == null ? new("not_configured", null, Array.Empty<int>()) : modelResults.Values.Select(model => model.Results.TryGetValue(entry.Result?.Source?.ImagePath ?? string.Empty, out var stored) ? stored : null)
                .FirstOrDefault(stored => stored != null && ReferenceEquals(stored.Result, entry?.Result) && stored.ArtifactIdentity == entry.ArtifactIdentity
                    && stored.ImageIdentity == entry.ImageIdentity && stored.CandidateSettingsIdentity == entry.CandidateSettingsIdentity && stored.TrainingRunIdentity == entry.TrainingRunIdentity)?.DecisionProjection
                ?? entry?.DecisionProjection ?? new("not_configured", null, Array.Empty<int>());

        public void InvalidateDecisionProjections()
        {
            foreach (var model in modelResults.Values)
                foreach (var pair in model.Results.ToArray())
                    model.Results[pair.Key] = pair.Value with { DecisionProjection = new("RuleStale", null, Array.Empty<int>()) };
        }

        public void RefreshDecisionProjections(string imagePath = null)
        {
            string expectedScope = scope;
            var targets = modelResults.Values.SelectMany(model => model.Results
                .Where(pair => imagePath == null || string.Equals(pair.Key, imagePath, StringComparison.OrdinalIgnoreCase))
                .Select(pair => (Store: model.Results, Key: pair.Key, Entry: pair.Value))).ToArray();
            if (targets.Length == 0) return;
            var decisionScope = decisionEvaluationFactory?.Invoke();
            ModelDecisionResult[] decisions = targets.Select(target => decisionScope.HasValue ? decisionScope.Value.Evaluate(target.Entry) : EvaluateDecision(target.Entry)).ToArray();
            bool valid = !decisionScope.HasValue || decisionScope.Value.Validate();
            if (!string.Equals(expectedScope, scope, StringComparison.OrdinalIgnoreCase)) return;
            for (int index = 0; index < targets.Length; index++)
            {
                var target = targets[index];
                if (target.Store.TryGetValue(target.Key, out var current) && ReferenceEquals(current, target.Entry))
                    target.Store[target.Key] = current with { DecisionProjection = valid ? decisions[index] : new("RuleStale", null, Array.Empty<int>()) };
            }
        }

        public void SetScope(string recipeIdentity)
        {
            string next = recipeIdentity ?? string.Empty;
            if (string.Equals(scope, next, StringComparison.OrdinalIgnoreCase)) return;
            modelResults.Clear();
            results = new(StringComparer.OrdinalIgnoreCase);
            executionOrder = new();
            selectedModel = default;
            scope = next;
        }

        public bool SelectModel(PythonModelSettings settings)
            => settings != null && SelectModel(settings.ModelEngine, settings.WeightsPath, settings.ProjectRootPath);

        private bool SelectModel(string engine, string weights, string root)
        {
            var key = (PythonModelSettings.NormalizeModelEngine(engine).ToUpperInvariant(), (weights ?? string.Empty).Trim().ToUpperInvariant(), (root ?? string.Empty).Trim().ToUpperInvariant());
            if (selectedModel == key) return false;
            if (!modelResults.TryGetValue(key, out var model))
            {
                model = (new Dictionary<string, InferenceResultReviewEntry>(StringComparer.OrdinalIgnoreCase), new List<string>());
                modelResults.Add(key, model);
            }
            selectedModel = key;
            results = model.Results;
            executionOrder = model.Order;
            return true;
        }

        public InferenceResultReviewEntry Find(string imagePath)
            => results.TryGetValue(imagePath ?? string.Empty, out var entry) ? entry : null;

        public string ScopeIdentity => scope;

        // Captured provenance is required for candidate batches and label review.
        // A normal result with no provenance remains available to the existing result viewer only.
        public bool RecordForScope(string expectedScope, YoloWorkerSmokeTestResult result, string artifactIdentity, string imageIdentity, string candidateSettingsIdentity = "", string trainingRunIdentity = "")
        {
            if (!string.Equals(scope, expectedScope, StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(expectedScope)
                || result?.Source == null || string.IsNullOrWhiteSpace(artifactIdentity) || string.IsNullOrWhiteSpace(imageIdentity)) return false;
            Record(result);
            string path = result.Source.ImagePath;
            if (!results.TryGetValue(path, out InferenceResultReviewEntry entry)) return false;
            // Failed retry cannot give a previous successful prediction a new identity.
            if (result.Succeeded) results[path] = entry with { ArtifactIdentity = artifactIdentity, ImageIdentity = imageIdentity, CandidateSettingsIdentity = candidateSettingsIdentity ?? string.Empty, TrainingRunIdentity = trainingRunIdentity ?? string.Empty };
            return true;
        }

        public InferenceResultReviewEntry FindRelevant(string imagePath, string artifactIdentity, string imageIdentity, string candidateSettingsIdentity = null)
        {
            InferenceResultReviewEntry entry = Find(imagePath);
            return entry?.Result?.Succeeded == true && !string.IsNullOrWhiteSpace(artifactIdentity) && !string.IsNullOrWhiteSpace(imageIdentity)
                && string.Equals(entry.ArtifactIdentity, artifactIdentity, StringComparison.Ordinal)
                && string.Equals(entry.ImageIdentity, imageIdentity, StringComparison.Ordinal)
                && (candidateSettingsIdentity == null || string.Equals(entry.CandidateSettingsIdentity, candidateSettingsIdentity, StringComparison.Ordinal)) ? entry : null;
        }

        public IReadOnlyList<InferenceResultReviewEntry> FindModelResults(string imagePath, string imageIdentity)
            => string.IsNullOrWhiteSpace(imageIdentity) ? Array.Empty<InferenceResultReviewEntry>() : modelResults.Values
                .Select(model => model.Results.TryGetValue(imagePath ?? string.Empty, out var entry) ? entry : null)
                .Where(entry => entry?.Result?.Succeeded == true && !string.IsNullOrWhiteSpace(entry.ArtifactIdentity)
                    && string.Equals(entry.ImageIdentity, imageIdentity, StringComparison.Ordinal))
                .GroupBy(entry => entry.ArtifactIdentity, StringComparer.Ordinal).Select(group => group.First()).ToArray();

        public bool SaveCandidateDraft(string imagePath, string artifactIdentity, string imageIdentity,
            IEnumerable<YoloWorkerSmokeCandidate> pending, IEnumerable<YoloWorkerSmokeCandidate> confirmed, string candidateSettingsIdentity = null)
        {
            InferenceResultReviewEntry entry = FindRelevant(imagePath, artifactIdentity, imageIdentity, candidateSettingsIdentity);
            if (entry == null) return false;
            results[imagePath] = entry with
            {
                PendingDraft = (pending ?? Array.Empty<YoloWorkerSmokeCandidate>()).Where(candidate => candidate != null).Select(AnnotationHistoryService.CloneCandidate).ToArray(),
                ConfirmedDraft = (confirmed ?? Array.Empty<YoloWorkerSmokeCandidate>()).Where(candidate => candidate != null).Select(AnnotationHistoryService.CloneCandidate).ToArray()
            };
            return true;
        }

        public void Record(YoloWorkerSmokeTestResult result)
        {
            if (result?.Source == null || string.IsNullOrWhiteSpace(result.Source.ImagePath)) return;
            SelectModel(result.Source.ModelEngine, result.Source.WeightsPath, result.Source.ProjectRootPath);
            string imagePath = result.Source.ImagePath;
            InferenceResultReviewEntry previous = Find(imagePath);
            if (previous == null) executionOrder.Add(imagePath);
            results[imagePath] = result.Succeeded
                ? new InferenceResultReviewEntry(result, null, false)
                    { ArtifactIdentity = result.CaptureWeightsSha256 ?? string.Empty, ImageIdentity = result.CaptureImageSha256 ?? string.Empty, TrainingRunIdentity = result.CaptureTrainingRunId ?? string.Empty, CandidateSettingsIdentity = result.CaptureCandidateSettingsIdentity ?? string.Empty }
                : new InferenceResultReviewEntry(previous?.Result, result, previous?.Seen == true)
                    { ArtifactIdentity = previous?.ArtifactIdentity ?? string.Empty, ImageIdentity = previous?.ImageIdentity ?? string.Empty,
                        CandidateSettingsIdentity = previous?.CandidateSettingsIdentity ?? string.Empty, TrainingRunIdentity = previous?.TrainingRunIdentity ?? string.Empty,
                        DecisionProjection = previous?.DecisionProjection,
                        IsLabelingDraft = previous?.IsLabelingDraft == true, PendingDraft = previous?.PendingDraft, ConfirmedDraft = previous?.ConfirmedDraft };
        }

        public bool IsCurrentEntry(string expectedScope, InferenceResultReviewEntry entry)
        {
            if (string.IsNullOrWhiteSpace(expectedScope) || !string.Equals(scope, expectedScope, StringComparison.OrdinalIgnoreCase) || entry?.Result?.Source == null) return false;
            return modelResults.Values.Any(model => model.Results.TryGetValue(entry.Result.Source.ImagePath, out InferenceResultReviewEntry stored)
                && ReferenceEquals(stored.Result, entry.Result) && stored.ArtifactIdentity == entry.ArtifactIdentity && stored.ImageIdentity == entry.ImageIdentity
                && stored.CandidateSettingsIdentity == entry.CandidateSettingsIdentity && stored.TrainingRunIdentity == entry.TrainingRunIdentity);
        }

        public InferenceResultReviewEntry FindCandidateDraft(string imagePath)
            => modelResults.Values.Select(model => model.Results.TryGetValue(imagePath ?? string.Empty, out var entry) ? entry : null)
                .SingleOrDefault(entry => entry?.IsLabelingDraft == true);

        public bool SaveCandidateDraftForEntry(string expectedScope, InferenceResultReviewEntry expectedEntry, IEnumerable<YoloWorkerSmokeCandidate> pending, IEnumerable<YoloWorkerSmokeCandidate> confirmed)
        {
            if (!IsCurrentEntry(expectedScope, expectedEntry)) return false;
            string path = expectedEntry.Result.Source.ImagePath;
            foreach (var model in modelResults.Values)
            {
                if (!model.Results.TryGetValue(path, out InferenceResultReviewEntry stored)) continue;
                model.Results[path] = ReferenceEquals(stored.Result, expectedEntry.Result) ? stored with
                {
                    IsLabelingDraft = true,
                    PendingDraft = (pending ?? Array.Empty<YoloWorkerSmokeCandidate>()).Where(candidate => candidate != null).Select(AnnotationHistoryService.CloneCandidate).ToArray(),
                    ConfirmedDraft = (confirmed ?? Array.Empty<YoloWorkerSmokeCandidate>()).Where(candidate => candidate != null).Select(AnnotationHistoryService.CloneCandidate).ToArray()
                } : stored with { IsLabelingDraft = false };
            }
            return true;
        }

        public bool MarkSeen(string imagePath)
        {
            InferenceResultReviewEntry entry = Find(imagePath);
            if (entry?.Result == null || entry.Seen) return false;
            results[imagePath] = entry with { Seen = true };
            return true;
        }

        public string FindNextUnseen(IReadOnlyList<string> visiblePaths, string currentPath)
        {
            if (visiblePaths == null || visiblePaths.Count == 0) return string.Empty;
            int current = visiblePaths.ToList().FindIndex(path => string.Equals(path, currentPath, StringComparison.OrdinalIgnoreCase));
            for (int offset = 1; offset <= visiblePaths.Count; offset++)
            {
                string path = visiblePaths[(current + offset) % visiblePaths.Count];
                InferenceResultReviewEntry entry = Find(path);
                if (entry?.Result != null && !entry.Seen && !string.Equals(path, currentPath, StringComparison.OrdinalIgnoreCase)) return path;
            }
            return string.Empty;
        }

        public string FindFirstResultNeedingReview()
            => executionOrder.FirstOrDefault(path => Find(path) is { } entry && (entry.Failure != null || !entry.Seen || entry.Result?.CandidateCount == 0)) ?? executionOrder.FirstOrDefault() ?? string.Empty;

        public IReadOnlyList<string> ResultPaths => executionOrder.ToArray();

        public string BuildCsv(IReadOnlyList<string> imagePaths, double displayConfidence)
        {
            if (!double.IsFinite(displayConfidence) || displayConfidence < 0 || displayConfidence > 1) throw new ArgumentOutOfRangeException(nameof(displayConfidence));
            var decisionScope = decisionEvaluationFactory?.Invoke();
            var csv = new StringBuilder("recipe_identity,image_path,selected_model_engine,selected_weights_path,selected_runtime_root,execution_state,raw_count,visible_count,display_confidence,source_model_engine,source_weights_path,source_runtime_root,source_image_size,source_confidence,source_maximum_candidates,source_candidate_id,source_result_only,source_started_at,source_completed_at,latest_failure,result_seen,part_judgment_status,part_judgment,class_id,class_name,confidence,anomaly_score,image_level,candidate_type,prediction_type,segmentation_type,x,y,width,height\r\n");
            foreach (string path in (imagePaths ?? Array.Empty<string>()).Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                InferenceResultReviewEntry entry = Find(path);
                YoloWorkerSmokeTestResult result = entry?.Result;
                ModelDecisionResult decision = decisionScope.HasValue ? decisionScope.Value.Evaluate(entry) : EvaluateDecision(entry);
                InferenceResultSource source = (result ?? entry?.Failure)?.Source;
                YoloWorkerSmokeCandidate[] candidates = (result?.Candidates ?? Array.Empty<YoloWorkerSmokeCandidate>())
                    .Where(candidate => candidate != null && (candidate.ImageLevel || candidate.Confidence >= displayConfidence)).ToArray();
                string state;
                if (result == null) state = entry?.Failure == null ? "not_run" : "failed";
                else if (entry.Failure != null) state = "completed_with_failure";
                else if (result.CandidateCount == 0) state = "completed_zero";
                else state = candidates.Length == 0 ? "filtered_empty" : "completed";
                foreach (YoloWorkerSmokeCandidate candidate in candidates.DefaultIfEmpty())
                {
                    AppendCsvRow(csv, scope, path, selectedModel.Engine, selectedModel.Weights, selectedModel.Root, state, result?.CandidateCount, result != null ? candidates.Length : (int?)null,
                        displayConfidence, source?.ModelEngine, source?.WeightsPath, source?.ProjectRootPath, source?.ImageSize, source?.Confidence, source?.MaximumCandidates, source?.CandidateId, source?.ResultOnly,
                        source?.StartedAt.ToString("O", CultureInfo.InvariantCulture), source?.CompletedAt == default(DateTimeOffset) ? null : source?.CompletedAt.ToString("O", CultureInfo.InvariantCulture),
                        entry?.Failure?.Summary, entry?.Seen, decision.Status, decision.IsNg.HasValue ? decision.IsNg.Value ? "NG" : "OK" : null, candidate?.ClassId, candidate?.ClassName,
                        candidate?.ImageLevel == true ? null : candidate?.Confidence, candidate?.AnomalyScore, candidate?.ImageLevel, candidate?.CandidateType, candidate?.PredictionType, candidate?.SegmentationType,
                        candidate?.ImageLevel == true ? null : candidate?.X, candidate?.ImageLevel == true ? null : candidate?.Y,
                        candidate?.ImageLevel == true ? null : candidate?.Width, candidate?.ImageLevel == true ? null : candidate?.Height);
                }
            }
            if (decisionScope.HasValue && !decisionScope.Value.Validate())
                throw new System.IO.InvalidDataException(OpenVisionLab.OpenVisionLanguageService.T("WpfComparisonWorkspace.RuleStale"));
            return csv.ToString();
        }

        private static void AppendCsvRow(StringBuilder csv, params object[] fields)
        {
            csv.AppendJoin(',', fields.Select(value =>
            {
                string text = value is bool flag ? flag ? "true" : "false" : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
                // Protect text cells when the CSV is opened in a spreadsheet; numeric signs retain their value.
                if (value is string && text.Length > 0 && "=+-@\t\r\n".Contains(text[0])) text = "'" + text;
                return "\"" + text.Replace("\"", "\"\"") + "\"";
            }));
            csv.Append("\r\n");
        }

        public InferenceResultSummary BuildSummary(IReadOnlyList<string> imagePaths, double displayConfidence)
        {
            string[] paths = (imagePaths ?? Array.Empty<string>()).Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            InferenceResultReviewEntry[] entries = paths.Select(Find).ToArray();
            YoloWorkerSmokeTestResult[] completed = entries.Where(entry => entry?.Result != null).Select(entry => entry.Result).ToArray();
            InferenceClassSummary[] classes = completed.SelectMany(result => result.Candidates ?? Array.Empty<YoloWorkerSmokeCandidate>())
                .Where(candidate => candidate != null && (candidate.ImageLevel || candidate.Confidence >= displayConfidence))
                .GroupBy(candidate => string.IsNullOrWhiteSpace(candidate.ClassName) ? "Defect" : candidate.ClassName, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal).Select(group => new InferenceClassSummary(group.Key, group.Count(), group.First().ClassId)).ToArray();
            ModelDecisionResult[] decisions = entries.Where(entry => entry?.Result != null).Select(GetDecisionProjection).ToArray();
            return new InferenceResultSummary(paths.Length, completed.Length, completed.Count(result => result.CandidateCount == 0),
                entries.Count(entry => entry?.Failure != null), classes,
                decisions.Count(decision => decision.IsNg == true), decisions.Count(decision => decision.IsNg == false), decisions.Count(decision => !decision.IsNg.HasValue));
        }
    }

    public sealed record InferenceClassSummary(string ClassName, int Count, int? ClassId = null);
    public sealed record InferenceResultSummary(int TotalImages, int CompletedImages, int EmptyImages, int FailedImages, IReadOnlyList<InferenceClassSummary> Classes, int NgImages = 0, int OkImages = 0, int UnknownImages = 0)
    {
        public static InferenceResultSummary Empty { get; } = new(0, 0, 0, 0, Array.Empty<InferenceClassSummary>());
    }

    public sealed record InferenceResultReviewEntry(YoloWorkerSmokeTestResult Result, YoloWorkerSmokeTestResult Failure, bool Seen)
    {
        public string ArtifactIdentity { get; init; } = string.Empty;
        public string ImageIdentity { get; init; } = string.Empty;
        public string CandidateSettingsIdentity { get; init; } = string.Empty;
        public string TrainingRunIdentity { get; init; } = string.Empty;
        public bool IsLabelingDraft { get; init; }
        public ModelDecisionResult DecisionProjection { get; init; }
        public IReadOnlyList<YoloWorkerSmokeCandidate> PendingDraft { get; init; }
        public IReadOnlyList<YoloWorkerSmokeCandidate> ConfirmedDraft { get; init; }
    }

    public sealed record InferenceResultReviewContext(string RecipeIdentity, string ImagePath, IReadOnlyList<string> VisibleImages, PythonModelSettings NextSettings, bool ResultOnly, bool IsBusy, bool HasUnsavedLabels, bool HasPendingCandidates, int VisiblePredictions, bool IsComparisonEvidence = false, IReadOnlyList<string> AllImages = null, double DisplayConfidence = .25, InferenceResultReviewEntry LabelingTransferEntry = null, bool IsClosing = false)
    {
        public static InferenceResultReviewContext Empty { get; } = new(string.Empty, string.Empty, Array.Empty<string>(), null, true, false, false, false, 0);
    }
}
