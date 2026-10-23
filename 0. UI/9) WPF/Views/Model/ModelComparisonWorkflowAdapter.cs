using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Threading;
using OpenVisionLab;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    // Owns WPF composition for model-comparison callbacks and review history.
    // Execution, cancellation, and process lifetime remain in ModelComparisonWorkflowService.
    internal sealed class ModelComparisonWorkflowAdapter : IDisposable
    {
        private readonly ModelComparisonWorkflowAdapterContext context;
        private bool workspaceConfigured;
        private bool disposed;
        private DispatcherTimer adoptionUndoTimer;

        private LabelingProjectData projectData => context.DataProvider?.Invoke();

        private WpfTrainingSettingsPanelViewModel TrainingSettingsViewModel =>
            context.TrainingSettingsViewModelProvider?.Invoke();

        private WpfLearningWorkflowPanelViewModel LearningWorkflowViewModel =>
            context.LearningWorkflowViewModelProvider?.Invoke();

        private WpfCandidateReviewPanelViewModel CandidateReviewViewModel =>
            context.CandidateReviewViewModelProvider?.Invoke();
        private ModelComparisonWorkspaceViewModel Workspace => context.WorkspaceViewModelProvider?.Invoke();

        internal ModelComparisonWorkflowAdapter(ModelComparisonWorkflowAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            ArgumentNullException.ThrowIfNull(context.DataProvider);
        }

        internal ModelComparisonWorkflowService CreateWorkflow()
        {
            return new ModelComparisonWorkflowService(
                BuildCandidateComparisonRequest,
                compareYoloV8ToYolo11 => compareYoloV8ToYolo11
                    ? context.ModelComparisonRunService.BuildYoloV8Yolo11DetectionRequest(projectData)
                    : context.ModelComparisonRunService.BuildYoloV5YoloV8DetectionRequest(projectData),
                () => context.SegmentationAdapterComparisonRunService.BuildRequest(
                    projectData,
                    projectData.ProjectSettings.PythonModel,
                    TrainingSettingsViewModel.SegmentationUnetWeightsPath,
                    TrainingSettingsViewModel.SegmentationYoloWeightsPath,
                    TrainingSettingsViewModel.SelectedSegmentationYoloEngine),
                context.ModelComparisonRunService.ValidateRequest,
                context.SegmentationAdapterComparisonRunService.ValidateRequest,
                context.ModelComparisonRunService.RunAsync,
                context.SegmentationAdapterComparisonRunService.RunAsync,
                (result, request) => context.ModelComparisonReviewService.BuildFromSummaryFile(
                    result.SummaryPath,
                    projectData.ClassNamedList?.Select(item => item?.Text ?? string.Empty).ToList()
                        ?? new List<string>(),
                    request.UiConfidence,
                    maxExamples: 5));
        }

        internal void ConfigureWorkspace()
        {
            if (disposed || workspaceConfigured || Workspace == null) return;
            workspaceConfigured = true;
            Workspace.Configure(
                () => { if (Workspace.CanRunComparison && context.IsWorkspaceBusyProvider?.Invoke() != true) LearningWorkflowViewModel?.RunModelComparisonCommand.Execute(null); },
                AdoptWorkspaceModel,
                (pair, groundTruth) =>
                {
                    Workspace.SetPreview(ModelComparisonPreviewPresentationService.Build(pair, BuildClassColors()));
                    foreach (ModelComparisonImagePair image in Workspace.Images.Where(image => image.ThumbnailSource == null)) image.ThumbnailSource = ModelComparisonPreviewPresentationService.BuildThumbnail(image.Baseline);
                },
                path => { if (context.IsWorkspaceBusyProvider?.Invoke() != true) context.ReviewWorkspaceLabels?.Invoke(path); }, UndoWorkspaceAdoption);
            Workspace.ConfigureDecisionRules(SaveDecisionRule, context.EvaluateDecisionRule, UndoDecisionRuleSave);
            Workspace.RefreshRequested += RefreshWorkspace;
        }

        internal void RefreshWorkspace()
        {
            if (disposed || Workspace == null || context.IsApplicationCloseApproved?.Invoke() == true) return;
            LabelingProjectData data = projectData;
            if (data?.ProjectSettings == null) return;
            PythonModelSettings settings = data.ProjectSettings.PythonModel;
            IReadOnlyList<WpfModelRegistryHistoryItem> models = ModelRegistryHistoryPresentationService.IncludeConfiguredModel(ModelRegistryHistoryPresentationService.BuildHistoryItems(data.ProjectSettings.ModelRegistry, int.MaxValue), settings, int.MaxValue);
            var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var sizes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            foreach (WpfModelRegistryHistoryItem model in models)
            {
                try
                {
                    string path = File.Exists(model.WeightsPath) ? model.WeightsPath : model.ArtifactPath;
                    using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    hashes[model.WeightsPath] = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
                    sizes[model.WeightsPath] = stream.Length;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException) { }
            }
            IReadOnlyList<WpfModelBenchmarkRun> reports = new ModelBenchmarkCatalogService().Load(context.RepositoryRootProvider?.Invoke() ?? ModelBenchmarkCatalogService.FindRepositoryRoot(), string.Empty, out IReadOnlyList<string> unreadable);
            RecipeDatasetVersionSnapshot version = null;
            string snapshotError = string.Empty;
            try { if (!data.ProjectSettings.ExternalYoloDataset.UseForTraining) version = RecipeDatasetVersionService.CreateSnapshot(data); }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException) { snapshotError = ex.Message; }
            Workspace.SetContext(models, reports, hashes, version?.DatasetVersionId, version?.ContentSha256, settings.ModelEngine, settings.ProjectRootPath,
                settings.MinimumDetectionConfidence, LearningWorkflowViewModel?.IsRunModelComparisonEnabled == true, context.IsWorkspaceBusyProvider?.Invoke() == true, data.OutputRootPath, data.ProjectSettings.DatasetPurpose, sizes);
            try { Workspace.SetDecisionRuleContext(data.ProjectSettings.ModelRegistry.TrainingRuns, data.ClassNamedList?.Select(item => item?.Text ?? string.Empty).ToArray(), RecipeDatasetVersionService.CreateDecisionReviewEvidenceSha256(data)); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidDataException)
            {
                Workspace.SetDecisionRuleContext(data.ProjectSettings.ModelRegistry.TrainingRuns, data.ClassNamedList?.Select(item => item?.Text ?? string.Empty).ToArray(), string.Empty);
                Workspace.SetLocalizedStatus("RuleReviewUnreadable");
            }
            Workspace.ValidateAdoptionUndoNotice(ModelCandidateLifecycleWorkflowService.FindUndoableAdoption(data)?.AdoptionId);
            foreach (ModelComparisonImagePair pair in Workspace.Images) pair.ThumbnailSource = ModelComparisonPreviewPresentationService.BuildThumbnail(pair.Baseline);
            if (unreadable.Count > 0) Workspace.SetLocalizedStatus("UnreadableReports", unreadable.Count);
            if (!string.IsNullOrEmpty(snapshotError)) Workspace.SetLocalizedStatus("SnapshotFailed", snapshotError);
        }

        internal async Task RunDecisionEvaluationAsync(TrainingRun run, ModelComparisonWorkflowService workflow)
        {
            if (disposed || context.IsApplicationCloseApproved?.Invoke() == true || context.IsWorkspaceBusyProvider?.Invoke() == true || run == null) return;
            LabelingProjectData data = projectData;
            if (!data.ProjectSettings.ModelRegistry.TrainingRuns.Contains(run) || data.ProjectSettings.ExternalYoloDataset.UseForTraining)
            { Workspace.SetLocalizedStatus("RuleRawUnavailable"); return; }
            try
            {
                RecipeDatasetVersionSnapshot snapshot = RecipeDatasetVersionService.CreateSnapshot(data);
                string reviewHash = RecipeDatasetVersionService.CreateDecisionReviewEvidenceSha256(data);
                string weights = File.Exists(run.CandidateWeightsPath) ? run.CandidateWeightsPath : run.ArtifactPath;
                string hash = HashingService.ComputeFileSha256(weights, lowerCase: true);
                ModelProfile profile = data.ProjectSettings.ModelRegistry.Profiles.FirstOrDefault(item => item?.ProfileId == run.ProfileId);
                PythonModelSettings settings = JsonConvert.DeserializeObject<PythonModelSettings>(JsonConvert.SerializeObject(data.ProjectSettings.PythonModel));
                settings.WeightsPath = weights; settings.MinimumDetectionConfidence = 0;
                settings.ModelEngine = profile?.ModelEngine ?? settings.ModelEngine;
                if (profile != null && !string.IsNullOrWhiteSpace(profile.ProjectRootPath)) settings.ProjectRootPath = profile.ProjectRootPath;
                settings.ClientScriptPath = settings.ModelEngine switch
                {
                    PythonModelSettings.EngineUnet => PythonModelRuntimeBundledWorkerService.ResolveUnetWorkerScriptPath(),
                    PythonModelSettings.EnginePatchCore => PythonModelRuntimeBundledWorkerService.ResolvePatchCoreWorkerScriptPath(),
                    PythonModelSettings.EngineYoloV5 or PythonModelSettings.EngineYoloV8 or PythonModelSettings.EngineYolo11 => PythonModelRuntimeBundledWorkerService.ResolveUltralyticsWorkerScriptPath(),
                    _ => settings.ClientScriptPath
                };
                ModelDecisionEvaluationSample[] samples = BuildDecisionEvaluationSamples(data, snapshot);
                if (!samples.Any(sample => sample.Split == "val" && sample.IsGroundTruthConfirmed && !sample.IsRejected)
                    || !samples.Any(sample => sample.Split == "test" && sample.IsGroundTruthConfirmed && !sample.IsRejected))
                    throw new InvalidDataException(OpenVisionLanguageService.T("WpfComparisonWorkspace.RuleGroundTruthUnavailable"));
                string output = Path.Combine(data.OutputRootPath, "model-decision-evaluations", Guid.NewGuid().ToString("N"));
                Workspace.SetLocalizedStatus("RuleEvaluating");
                context.SetCommandStatus?.Invoke(OpenVisionLanguageService.T("WpfComparisonWorkspace.RuleEvaluating"), true);
                bool settled = false;
                await workflow.RunDecisionEvaluationAsync(async token =>
                {
                    Directory.CreateDirectory(output);
                    var captured = new List<ModelDecisionEvaluationSample>();
                    foreach (ModelDecisionEvaluationSample sample in samples)
                    {
                        token.ThrowIfCancellationRequested();
                        if (!sample.IsGroundTruthConfirmed || sample.IsRejected) { captured.Add(sample); continue; }
                        if (!ModelArtifactStoreService.Verify(sample.ImagePath, sample.ImageSha256)) throw new InvalidDataException("Evaluation image changed.");
                        YoloWorkerSmokeTestResult result = await YoloWorkerSmokeTestService.RunAsync(settings, sample.ImagePath, token, decisionEvaluation: true, decisionOutputDirectory: output).ConfigureAwait(false);
                        token.ThrowIfCancellationRequested();
                        if (!result.Succeeded) throw new InvalidDataException(result.Summary);
                        result.CaptureTrainingRunId = run.TrainingRunId;
                        bool complete = result.HasCompletePredictions && result.CaptureConfidenceFloor == 0
                            && (!result.CaptureCandidateLimit.HasValue || result.Candidates.Count < result.CaptureCandidateLimit.Value);
                        if (!complete) throw new InvalidDataException(OpenVisionLanguageService.T("WpfComparisonWorkspace.RuleIncompleteCapture"));
                        captured.Add(sample with { RawCandidates = result.Candidates, HasCompletePredictions = true });
                    }
                    var cache = new ModelDecisionEvaluationCache(run.TrainingRunId, hash, snapshot.DatasetVersionId, snapshot.ContentSha256,
                        reviewHash, data.ProjectSettings.DatasetPurpose, 0, true, captured);
                    File.WriteAllText(Path.Combine(output, "raw-evaluation.json"), JsonConvert.SerializeObject(cache, Formatting.Indented));
                    return cache;
                }, () => !disposed && ReferenceEquals(data, projectData) && projectData.ProjectSettings.ModelRegistry.TrainingRuns.Contains(run),
                    cache => { settled = true; ApplyDecisionEvaluationCache(cache); }, error => { settled = true; Workspace.SetLocalizedStatus("RuleEvaluationFailed", error); context.AppendLog?.Invoke(error); },
                    () => { context.RefreshCommands?.Invoke(); Workspace.IsBusy = workflow.IsModelComparisonRunning; });
                if (!disposed && context.IsApplicationCloseApproved?.Invoke() != true && ReferenceEquals(data, projectData))
                {
                    if (!settled) Workspace.SetLocalizedStatus("RuleEvaluationCanceled");
                    context.SetCommandStatus?.Invoke(Workspace.OperationStatus, false);
                }
            }
            catch (Exception ex)
            { Workspace.SetLocalizedStatus("RuleEvaluationFailed", ex.Message); context.SetCommandStatus?.Invoke(Workspace.OperationStatus, false); context.AppendLog?.Invoke(ex.Message); }
        }

        internal static ModelDecisionEvaluationSample[] BuildDecisionEvaluationSamples(LabelingProjectData data, RecipeDatasetVersionSnapshot snapshot)
        {
            RecipeDatasetVersionFile[] images = snapshot.Files.Where(file => file.Kind == "image" && (file.Split == "valid" || file.Split == "test")).ToArray();
            if (images.Length == 0 || images.GroupBy(file => Path.GetFileNameWithoutExtension(file.RelativePath), StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1)
                || images.GroupBy(file => file.Sha256, StringComparer.OrdinalIgnoreCase).Any(group => group.Select(file => file.Split).Distinct().Count() > 1))
                throw new InvalidDataException("Validation/test image identities are missing, duplicated, or overlap.");
            string[] paths = images.Select(file => Path.GetFullPath(Path.Combine(data.OutputRootPath, file.RelativePath))).ToArray();
            var yoloReview = new YoloImageReviewStatusService();
            var anomalyReview = new AnomalyImageReviewStatusService();
            string yoloReviewPath = YoloImageReviewStatusService.ResolveReviewStatusFilePath(data);
            string anomalyReviewPath = AnomalyImageReviewStatusService.ResolveReviewStatusFilePath(data);
            // Existing loaders intentionally recover unreadable data. Evaluation must
            // reject it instead of silently treating all records as unreviewed.
            foreach (string reviewPath in new[] { yoloReviewPath, data.ProjectSettings.DatasetPurpose == LabelingDatasetPurpose.AnomalyDetection ? anomalyReviewPath : string.Empty }.Where(File.Exists))
            {
                JToken document = JToken.Parse(File.ReadAllText(reviewPath));
                JArray records = document as JArray ?? (document is JObject wrapper ? wrapper["Items"] as JArray ?? wrapper["items"] as JArray : null);
                if (records == null) throw new InvalidDataException("Saved review evidence is malformed.");
                if (records.Any(item => item is not JObject)) throw new InvalidDataException("Saved review records are malformed.");
                if (records.OfType<JObject>().Any(item => string.IsNullOrWhiteSpace(item["ImageName"]?.Value<string>()) && string.IsNullOrWhiteSpace(item["ImagePath"]?.Value<string>())))
                    throw new InvalidDataException("Saved review identity is missing.");
                foreach (JObject record in records.OfType<JObject>())
                {
                    bool anomaly = reviewPath == anomalyReviewPath;
                    if (!HasValidReviewEnum(record, "ReviewState", anomaly ? typeof(AnomalyImageReviewState) : typeof(YoloImageReviewState))
                        || !anomaly && !HasValidReviewEnum(record, "QualityReviewState", typeof(YoloImageQualityReviewState)))
                        throw new InvalidDataException("Saved review state is unsupported.");
                }
                if (records.OfType<JObject>().GroupBy(item => item["ImageName"]?.Value<string>() ?? Path.GetFileNameWithoutExtension(item["ImagePath"]?.Value<string>() ?? ""), StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
                    throw new InvalidDataException("Saved review identities are duplicated.");
            }
            yoloReview.LoadReviewStatus(data, paths); anomalyReview.LoadReviewStatus(data, paths);
            var samples = new List<ModelDecisionEvaluationSample>();
            for (int index = 0; index < images.Length; index++)
            {
                RecipeDatasetVersionFile image = images[index]; string path = paths[index];
                string name = Path.GetFileNameWithoutExtension(path);
                YoloImageReviewStatus review = yoloReview.GetItems().Single(item => item.ImagePath == path);
                AnomalyImageReviewStatus anomaly = anomalyReview.GetItems().Single(item => item.ImagePath == path);
                bool isAnomaly = data.ProjectSettings.DatasetPurpose == LabelingDatasetPurpose.AnomalyDetection;
                bool rejected = review.QualityReviewState == YoloImageQualityReviewState.NeedsFix;
                bool confirmed = isAnomaly ? anomaly.IsReviewed : snapshot.Files.Any(file => file.Split == image.Split && (file.Kind == "label" || file.Kind == "segment")
                    && string.Equals(Path.GetFileNameWithoutExtension(file.RelativePath), name, StringComparison.OrdinalIgnoreCase));
                var classIds = new List<int>();
                RecipeDatasetVersionFile[] annotations = snapshot.Files.Where(file => file.Split == image.Split && (file.Kind == "label" || file.Kind == "segment" || file.Kind == "mask")
                    && string.Equals(Path.GetFileNameWithoutExtension(file.RelativePath), name, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (confirmed && !rejected && !isAnomaly)
                {
                    string label = Path.Combine(data.OutputRootPath, "data", image.Split, "labels", name + ".txt");
                    string segment = Path.Combine(data.OutputRootPath, "data", image.Split, "segments", name + ".json");
                    using System.Drawing.Image bitmap = System.Drawing.Image.FromFile(path);
                    IReadOnlyCollection<string> names;
                    if (data.ProjectSettings.DatasetPurpose == LabelingDatasetPurpose.Segmentation && File.Exists(segment))
                        names = YoloSegmentationAnnotationService.LoadSegmentationObjects(segment, data.ClassNamedList, bitmap.Size).Where(pair => pair.Value.Count > 0).Select(pair => pair.Key).ToArray();
                    else
                    {
                        if (!File.Exists(label)) throw new InvalidDataException("Confirmed ground truth has no saved label: " + name);
                        YoloAnnotationLoadResult loaded = YoloAnnotationService.LoadAnnotationRectanglesWithDiagnostics(label, data.ClassNamedList, bitmap.Size);
                        if (loaded.HasErrors) throw new InvalidDataException("Saved ground truth is malformed: " + name);
                        names = loaded.Annotations.Where(pair => pair.Value.Count > 0).Select(pair => pair.Key).ToArray();
                    }
                    foreach (string className in names) classIds.Add(ClassCatalogService.FindClassIndex(data.ClassNamedList, className));
                }
                string labelHash = isAnomaly ? RecipeDatasetVersionService.CreateDecisionReviewEvidenceSha256(data) : HashingService.ComputeUtf8TextSha256(string.Join("|", annotations.OrderBy(file => file.RelativePath).Select(file => file.RelativePath + ":" + file.Sha256)), lowerCase: true);
                samples.Add(new(path, image.Sha256, labelHash, image.Split == "valid" ? "val" : "test", confirmed, rejected, classIds,
                    isAnomaly && anomaly.IsReviewed ? anomaly.ReviewState == AnomalyImageReviewState.Abnormal : null, Array.Empty<YoloWorkerSmokeCandidate>(), false));
            }
            return samples.ToArray();

            static bool HasValidReviewEnum(JObject record, string property, Type enumType)
            {
                string name = record[property + "Name"]?.Value<string>();
                if (!string.IsNullOrWhiteSpace(name)) return Enum.TryParse(enumType, name, ignoreCase: true, out object parsed) && Enum.IsDefined(enumType, parsed);
                JToken value = record[property];
                if (value?.Type == JTokenType.String) return Enum.TryParse(enumType, value.Value<string>(), ignoreCase: true, out object parsedValue) && Enum.IsDefined(enumType, parsedValue);
                return value == null || value.Type == JTokenType.Integer && Enum.IsDefined(enumType, value.Value<int>());
            }
        }

        private void SaveDecisionRule(TrainingRun run, ModelDecisionRule rule)
        {
            if (disposed || context.IsApplicationCloseApproved?.Invoke() == true || context.IsWorkspaceBusyProvider?.Invoke() == true || run == null || rule == null) return;
            bool committed = false;
            try
            {
                LabelingProjectData data = projectData;
                if (!data.ProjectSettings.ModelRegistry.TrainingRuns.Contains(run) || data.ProjectSettings.DatasetPurpose != rule.DatasetPurpose)
                { Workspace.CompleteDecisionRuleSave(false, "RuleStale"); return; }
                RecipeDatasetVersionSnapshot version = RecipeDatasetVersionService.CreateSnapshot(data);
                string weightsPath = File.Exists(run.CandidateWeightsPath) ? run.CandidateWeightsPath : run.ArtifactPath;
                if (!ModelArtifactStoreService.Verify(weightsPath, rule.WeightsSha256)
                    || !ModelRegistryService.DecisionRuleMatches(rule, run.TrainingRunId, run.WeightsSha256, version.DatasetVersionId, version.ContentSha256, RecipeDatasetVersionService.CreateDecisionReviewEvidenceSha256(data)))
                { Workspace.CompleteDecisionRuleSave(false, "RuleStale"); return; }
                string error = ModelRegistryService.ValidateDecisionRule(rule);
                if (error.Length > 0) { Workspace.CompleteDecisionRuleSave(false, error); return; }
                if (Workspace.ValidationDecision.Status != "ready" || Workspace.TestDecision.Status != "ready")
                { Workspace.CompleteDecisionRuleSave(false, "RuleNeedsEvaluation"); return; }
                using ModelRegistryStateTransaction transaction = new(data.ProjectSettings.ModelRegistry);
                ModelDecisionEvaluation fixedTest = ModelComparisonReviewService.EvaluateDecisionRule(rule, Workspace.DecisionEvaluationCache, "test");
                if (fixedTest.Status != "ready") { Workspace.CompleteDecisionRuleSave(false, fixedTest.Status); return; }
                rule.FixedTestSummary = new()
                {
                    NgCount = fixedTest.NgCount, OkCount = fixedTest.OkCount, Misses = fixedTest.Misses, Extras = fixedTest.Extras,
                    ExcludedCount = fixedTest.ExcludedCount, EvaluatedUtc = DateTime.UtcNow.ToString("O"),
                    EvaluationEvidenceSha256 = HashingService.ComputeUtf8TextSha256(JsonConvert.SerializeObject(Workspace.DecisionEvaluationCache), lowerCase: true)
                };
                run.PreviousDecisionRule = run.DecisionRule?.Copy(); run.DecisionRuleUndoAvailable = true;
                run.DecisionRule = rule.Copy();
                if (context.SaveDecisionRuleMetadata?.Invoke() != true) { transaction.Rollback(); Workspace.CompleteDecisionRuleSave(false); return; }
                transaction.Commit(); committed = true;
                Workspace.CompleteDecisionRuleSave(true);
                RefreshWorkspace();
                context.RefreshCommands?.Invoke();
            }
            catch (Exception ex)
            {
                if (committed) Workspace.SetLocalizedStatus("RuleRefreshFailed"); else Workspace.CompleteDecisionRuleSave(false);
                context.AppendLog?.Invoke(ex.Message);
            }
        }

        private void UndoDecisionRuleSave(TrainingRun run)
        {
            if (disposed || context.IsApplicationCloseApproved?.Invoke() == true || context.IsWorkspaceBusyProvider?.Invoke() == true || run?.DecisionRuleUndoAvailable != true) return;
            bool committed = false;
            try
            {
                ModelRegistrySettings registry = projectData.ProjectSettings.ModelRegistry;
                if (!registry.TrainingRuns.Contains(run)) { Workspace.CompleteDecisionRuleSave(false, "RuleStale"); return; }
                using ModelRegistryStateTransaction transaction = new(registry);
                run.DecisionRule = run.PreviousDecisionRule?.Copy(); run.PreviousDecisionRule = null; run.DecisionRuleUndoAvailable = false;
                if (context.SaveDecisionRuleMetadata?.Invoke() != true) { transaction.Rollback(); Workspace.CompleteDecisionRuleSave(false); return; }
                transaction.Commit(); committed = true;
                Workspace.CompleteDecisionRuleUndo(); RefreshWorkspace(); context.RefreshCommands?.Invoke();
            }
            catch (Exception ex)
            {
                if (committed) Workspace.SetLocalizedStatus("RuleRefreshFailed"); else Workspace.CompleteDecisionRuleSave(false);
                context.AppendLog?.Invoke(ex.Message);
            }
        }

        internal void ApplyDecisionEvaluationCache(ModelDecisionEvaluationCache cache)
        {
            if (disposed || context.IsApplicationCloseApproved?.Invoke() == true) return;
            try
            {
                RecipeDatasetVersionSnapshot current = RecipeDatasetVersionService.CreateSnapshot(projectData);
                TrainingRun run = projectData.ProjectSettings.ModelRegistry.TrainingRuns.FirstOrDefault(item => item?.TrainingRunId == cache?.TrainingRunId);
                string weightsPath = run == null ? string.Empty : File.Exists(run.CandidateWeightsPath) ? run.CandidateWeightsPath : run.ArtifactPath;
                bool valid = cache != null && run != null && current.DatasetVersionId == cache.DatasetVersionId && current.ContentSha256 == cache.DatasetContentSha256
                    && RecipeDatasetVersionService.CreateDecisionReviewEvidenceSha256(projectData) == cache.ReviewEvidenceSha256 && ModelArtifactStoreService.Verify(weightsPath, cache.WeightsSha256);
                Workspace?.SetDecisionEvaluationCache(valid ? cache : null);
                Workspace?.SetLocalizedStatus(valid ? "RuleEvaluationCompleted" : "RuleStale");
                context.SetCommandStatus?.Invoke(OpenVisionLanguageService.T("WpfComparisonWorkspace." + (valid ? "RuleEvaluationCompleted" : "RuleStale")), false);
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is UnauthorizedAccessException || ex is ArgumentException)
            { Workspace?.SetDecisionEvaluationCache(null); }
        }

        internal string ResolveDecisionRuleRunId(InferenceResultReviewEntry entry)
        {
            InferenceResultSource source = entry?.Result?.Source;
            LabelingProjectData data = context.DataProvider?.Invoke();
            if (source == null || data?.ProjectSettings == null) return string.Empty;
            ModelRegistrySettings registry = data.ProjectSettings.ModelRegistry;
            ModelCandidate candidate = registry.Candidates.FirstOrDefault(item => item?.CandidateId == source.CandidateId);
            string runId = entry.Result.CaptureTrainingRunId;
            if (string.IsNullOrWhiteSpace(runId) || candidate != null && candidate.TrainingRunId != runId) return string.Empty;
            TrainingRun[] matches = registry.TrainingRuns.Where(run => run != null && run.TrainingRunId == runId).ToArray();
            return matches.Length == 1 ? matches[0].TrainingRunId : string.Empty;
        }

        internal ModelDecisionResult EvaluateSavedDecision(InferenceResultReviewEntry entry)
        {
            var scope = CreateSavedDecisionEvaluator();
            ModelDecisionResult decision = scope.Evaluate(entry);
            return scope.Validate() ? decision : new("RuleStale", null, Array.Empty<int>());
        }

        // One CSV/projection refresh invocation owns these delegates. End validation
        // closes the invocation; never retain this scope in the session or a binding.
        internal (Func<InferenceResultReviewEntry, ModelDecisionResult> Evaluate, Func<bool> Validate) CreateSavedDecisionEvaluator()
        {
            if (disposed || context.IsApplicationCloseApproved?.Invoke() == true)
                return (_ => new("RuleRawUnavailable", null, Array.Empty<int>()), () => false);
            LabelingProjectData data = context.DataProvider?.Invoke();
            bool completed = false;
            if (data?.ProjectSettings == null)
                return (_ => new(completed ? "RuleStale" : "RuleUnconfigured", null, Array.Empty<int>()), () =>
                {
                    if (completed) return false;
                    completed = true;
                    return !disposed && context.IsApplicationCloseApproved?.Invoke() != true && ReferenceEquals(data, context.DataProvider?.Invoke());
                });
            LabelingDatasetPurpose purpose = data.ProjectSettings.DatasetPurpose;
            ModelRegistrySettings registry = data.ProjectSettings.ModelRegistry;
            var runs = registry.TrainingRuns.Where(run => run != null).Select(run =>
                (Id: run.TrainingRunId, Weights: run.WeightsSha256, Rule: run.DecisionRule?.Copy())).ToArray();
            var candidates = registry.Candidates.Where(candidate => candidate != null).Select(candidate =>
                (Id: candidate.CandidateId, Run: candidate.TrainingRunId)).ToArray();
            var usedRuns = new HashSet<string>(StringComparer.Ordinal);
            var usedCandidates = new HashSet<string>(StringComparer.Ordinal);
            var artifacts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var images = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var inputs = new Dictionary<InferenceResultReviewEntry, string>();
            RecipeDatasetVersionSnapshot version = null;
            string reviewHash = string.Empty;
            try
            {
                if (runs.Any(run => run.Rule != null))
                {
                    version = RecipeDatasetVersionService.CreateSnapshot(data);
                    reviewHash = RecipeDatasetVersionService.CreateDecisionReviewEvidenceSha256(data);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is InvalidDataException)
            { return (_ => new("RuleStale", null, Array.Empty<int>()), () => false); }
            return (Evaluate, Validate);

            ModelDecisionResult Evaluate(InferenceResultReviewEntry entry)
            {
                YoloWorkerSmokeTestResult result = entry?.Result;
                if (completed || disposed || context.IsApplicationCloseApproved?.Invoke() == true || !ReferenceEquals(data, context.DataProvider?.Invoke()))
                    return new("RuleStale", null, Array.Empty<int>());
                if (result?.Succeeded != true || result.Source == null) return new("RuleRawUnavailable", null, Array.Empty<int>());
                inputs.TryAdd(entry, InputContract(entry));
                string runId = result.CaptureTrainingRunId;
                usedRuns.Add(runId ?? string.Empty);
                var matches = runs.Where(run => run.Id == runId).ToArray();
                if (string.IsNullOrWhiteSpace(runId) || matches.Length != 1) return new("RuleUnconfigured", null, Array.Empty<int>());
                if (!string.IsNullOrWhiteSpace(result.Source.CandidateId))
                {
                    usedCandidates.Add(result.Source.CandidateId);
                    var candidate = candidates.Where(item => item.Id == result.Source.CandidateId).ToArray();
                    if (candidate.Length > 1 || candidate.Length == 1 && candidate[0].Run != runId) return new("RuleStale", null, Array.Empty<int>());
                }
                ModelDecisionRule rule = matches[0].Rule;
                string ruleError = ModelRegistryService.ValidateDecisionRule(rule);
                if (ruleError.Length > 0) return new(ruleError, null, Array.Empty<int>());
                try
                {
                    string capturedWeights = result.CaptureWeightsSha256?.Length == 64 ? result.CaptureWeightsSha256 : entry.ArtifactIdentity;
                    string capturedImage = result.CaptureImageSha256?.Length == 64 ? result.CaptureImageSha256 : entry.ImageIdentity;
                    string weightPath = Path.GetFullPath(result.Source.WeightsPath), imagePath = Path.GetFullPath(result.Source.ImagePath);
                    if (!artifacts.TryGetValue(weightPath, out string weightHash)) artifacts[weightPath] = weightHash = HashingService.ComputeFileSha256(weightPath, lowerCase: true);
                    if (!images.TryGetValue(imagePath, out string imageHash)) images[imagePath] = imageHash = HashingService.ComputeFileSha256(imagePath, lowerCase: true);
                    if (purpose != rule.DatasetPurpose || !string.Equals(weightHash, rule.WeightsSha256, StringComparison.OrdinalIgnoreCase)
                        || !string.Equals(weightHash, matches[0].Weights, StringComparison.OrdinalIgnoreCase)
                        || !ModelRegistryService.DecisionRuleMatches(rule, runId, capturedWeights, version.DatasetVersionId, version.ContentSha256, reviewHash)
                        || !string.Equals(imageHash, capturedImage, StringComparison.OrdinalIgnoreCase)) return new("RuleStale", null, Array.Empty<int>());
                    if (!result.HasCompletePredictions || !result.CaptureConfidenceFloor.HasValue
                        || !double.IsFinite(result.CaptureConfidenceFloor.Value) || result.CaptureConfidenceFloor.Value < 0 || result.CaptureConfidenceFloor.Value > 1
                        || result.Candidates == null || result.CaptureCandidateLimit.HasValue && result.Candidates.Count >= result.CaptureCandidateLimit.Value
                        || rule.DatasetPurpose != LabelingDatasetPurpose.AnomalyDetection && rule.Classes.Where(item => item.Enabled).Any(item => (item.Confidence ?? rule.Confidence) < result.CaptureConfidenceFloor.Value))
                        return new("RuleIncompleteCapture", null, Array.Empty<int>());
                    if (rule.DatasetPurpose == LabelingDatasetPurpose.AnomalyDetection && result.CaptureAnomalyScore.HasValue)
                    {
                        ModelDecisionResult decision = ModelRegistryService.EvaluateDecision(rule, new[] { new YoloWorkerSmokeCandidate { ImageLevel = true, AnomalyScore = result.CaptureAnomalyScore, Index = 0 } });
                        return decision.IsNg == true ? decision with { EvidenceIndices = result.Candidates.Where(item => item?.ImageLevel == true).Select(item => item.Index).ToArray() } : decision;
                    }
                    return ModelRegistryService.EvaluateDecision(rule, result.Candidates);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is InvalidDataException)
                { return new("RuleStale", null, Array.Empty<int>()); }
            }

            bool Validate()
            {
                if (completed) return false;
                completed = true;
                if (disposed || context.IsApplicationCloseApproved?.Invoke() == true || !ReferenceEquals(data, context.DataProvider?.Invoke())
                    || !ReferenceEquals(registry, data.ProjectSettings?.ModelRegistry) || purpose != data.ProjectSettings.DatasetPurpose) return false;
                try
                {
                    if (version != null)
                    {
                        RecipeDatasetVersionSnapshot current = RecipeDatasetVersionService.CreateSnapshot(data);
                        if (current.DatasetVersionId != version.DatasetVersionId || current.ContentSha256 != version.ContentSha256
                            || RecipeDatasetVersionService.CreateDecisionReviewEvidenceSha256(data) != reviewHash) return false;
                    }
                    var currentRuns = registry.TrainingRuns.Where(run => run != null && usedRuns.Contains(run.TrainingRunId)).Select(run =>
                        (Id: run.TrainingRunId, Weights: run.WeightsSha256, Rule: run.DecisionRule)).ToArray();
                    if (JsonConvert.SerializeObject(currentRuns.OrderBy(run => run.Id, StringComparer.Ordinal))
                        != JsonConvert.SerializeObject(runs.Where(run => usedRuns.Contains(run.Id)).OrderBy(run => run.Id, StringComparer.Ordinal))) return false;
                    var currentCandidates = registry.Candidates.Where(candidate => candidate != null && usedCandidates.Contains(candidate.CandidateId)).Select(candidate =>
                        (Id: candidate.CandidateId, Run: candidate.TrainingRunId)).ToArray();
                    if (JsonConvert.SerializeObject(currentCandidates.OrderBy(candidate => candidate.Id, StringComparer.Ordinal))
                        != JsonConvert.SerializeObject(candidates.Where(candidate => usedCandidates.Contains(candidate.Id)).OrderBy(candidate => candidate.Id, StringComparer.Ordinal))) return false;
                    return artifacts.All(pair => ModelArtifactStoreService.Verify(pair.Key, pair.Value)) && images.All(pair => ModelArtifactStoreService.Verify(pair.Key, pair.Value))
                        && inputs.All(pair => InputContract(pair.Key) == pair.Value);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is InvalidDataException) { return false; }
            }

            static string InputContract(InferenceResultReviewEntry entry) => JsonConvert.SerializeObject(new
            {
                entry.ArtifactIdentity, entry.ImageIdentity, entry.TrainingRunIdentity, entry.CandidateSettingsIdentity,
                entry.Result.Source, entry.Result.Succeeded, entry.Result.CaptureTrainingRunId, entry.Result.CaptureWeightsSha256,
                entry.Result.CaptureImageSha256, entry.Result.HasCompletePredictions, entry.Result.CaptureConfidenceFloor,
                entry.Result.CaptureCandidateLimit, entry.Result.CaptureAnomalyScore, entry.Result.CaptureCandidateSettingsIdentity, entry.Result.CandidateCount,
                Candidates = entry.Result.Candidates?.Select(candidate => candidate == null ? null : new
                {
                    candidate.Index, candidate.ClassId, candidate.ClassName, candidate.Confidence, candidate.AnomalyScore, candidate.ImageLevel,
                    candidate.CandidateType, candidate.PredictionType, candidate.SegmentationType, candidate.X, candidate.Y, candidate.Width, candidate.Height
                }).ToArray()
            });
        }

        internal string GetSavedDecisionSummary(string trainingRunId)
        {
            LabelingProjectData data = context.DataProvider?.Invoke();
            TrainingRun run = data?.ProjectSettings?.ModelRegistry?.TrainingRuns.FirstOrDefault(item => item?.TrainingRunId == trainingRunId);
            ModelDecisionRule rule = run?.DecisionRule;
            if (rule == null) return OpenVisionLanguageService.T("WpfComparisonWorkspace.RuleUnconfigured");
            string ruleError = ModelRegistryService.ValidateDecisionRule(rule);
            if (ruleError.Length > 0) return OpenVisionLanguageService.T("WpfComparisonWorkspace." + ruleError);
            try
            {
                RecipeDatasetVersionSnapshot snapshot = RecipeDatasetVersionService.CreateSnapshot(data);
                string path = File.Exists(run.CandidateWeightsPath) ? run.CandidateWeightsPath : run.ArtifactPath;
                if (!ModelArtifactStoreService.Verify(path, rule.WeightsSha256)
                    || !ModelRegistryService.DecisionRuleMatches(rule, run.TrainingRunId, run.WeightsSha256, snapshot.DatasetVersionId, snapshot.ContentSha256, RecipeDatasetVersionService.CreateDecisionReviewEvidenceSha256(data)))
                    return OpenVisionLanguageService.T("WpfComparisonWorkspace.RuleStale");
                string text = rule.DatasetPurpose == LabelingDatasetPurpose.AnomalyDetection
                    ? string.Format(OpenVisionLanguageService.T("WpfComparisonWorkspace.RuleAnomalySummary"), rule.AnomalyScoreThreshold)
                    : string.Format(OpenVisionLanguageService.T("WpfComparisonWorkspace.RuleDetectionSummary"), rule.Confidence, rule.MinimumShortSidePixels, rule.MinimumDefectCount);
                ModelDecisionTestSummary test = rule.FixedTestSummary;
                return text + Environment.NewLine + (test == null ? OpenVisionLanguageService.T("WpfComparisonWorkspace.RuleNeedsEvaluation")
                    : string.Format(OpenVisionLanguageService.T("WpfComparisonWorkspace.RuleFixedTestSummary"), test.Misses, test.NgCount, test.Extras, test.OkCount));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is InvalidDataException)
            { return OpenVisionLanguageService.T("WpfComparisonWorkspace.RuleStale"); }
        }

        internal double? GetSavedAnomalyThreshold(string artifactHash, string trainingRunId = "")
        {
            LabelingProjectData data = context.DataProvider?.Invoke();
            if (disposed || data?.ProjectSettings?.DatasetPurpose != LabelingDatasetPurpose.AnomalyDetection || artifactHash?.Length != 64) return null;
            ModelRegistrySettings registry = data.ProjectSettings.ModelRegistry;
            ModelCandidate current = registry.Candidates.FirstOrDefault(candidate => candidate?.CandidateId == registry.CurrentInspectionModelId);
            string runId = string.IsNullOrWhiteSpace(trainingRunId) ? current?.TrainingRunId : trainingRunId;
            if (string.IsNullOrWhiteSpace(runId)) return null;
            TrainingRun[] matches = registry.TrainingRuns.Where(run => run != null && string.Equals(run.WeightsSha256, artifactHash, StringComparison.OrdinalIgnoreCase)
                && run.TrainingRunId == runId).ToArray();
            if (matches.Length != 1) return null;
            TrainingRun selected = matches[0]; ModelDecisionRule rule = selected.DecisionRule;
            if (ModelRegistryService.ValidateDecisionRule(rule).Length > 0 || rule.DatasetPurpose != LabelingDatasetPurpose.AnomalyDetection) return null;
            try
            {
                RecipeDatasetVersionSnapshot snapshot = RecipeDatasetVersionService.CreateSnapshot(data);
                string weights = File.Exists(selected.CandidateWeightsPath) ? selected.CandidateWeightsPath : selected.ArtifactPath;
                return ModelArtifactStoreService.Verify(weights, artifactHash)
                    && ModelRegistryService.DecisionRuleMatches(rule, selected.TrainingRunId, artifactHash, snapshot.DatasetVersionId, snapshot.ContentSha256, RecipeDatasetVersionService.CreateDecisionReviewEvidenceSha256(data))
                    ? rule.AnomalyScoreThreshold : null;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is InvalidDataException) { return null; }
        }

        private IReadOnlyList<Color> BuildClassColors()
            => projectData?.ClassNamedList?.Select((item, index) => new WpfClassCatalogListItem(item, index).DrawBrush)
                .OfType<SolidColorBrush>().Select(brush => brush.Color).ToArray() ?? Array.Empty<Color>();

        private void AdoptWorkspaceModel(ModelComparisonWorkspaceRow row, string reason)
        {
            if (context.IsApplicationCloseApproved?.Invoke() == true || context.IsWorkspaceBusyProvider?.Invoke() == true || row?.Run == null) return;
            bool committed = false;
            try
            {
                using FileStream reportStream = new(row.Run.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                string reportSha256 = Convert.ToHexString(SHA256.HashData(reportStream)).ToLowerInvariant();
                if (row.Run.SourceSha256.Length != 64 || reportSha256 != row.Run.SourceSha256)
                {
                    Workspace.CompleteAdoption(false, OpenVisionLanguageService.T("WpfComparisonWorkspace.ReportChanged"));
                    return;
                }
                RecipeDatasetVersionSnapshot current = RecipeDatasetVersionService.CreateSnapshot(projectData);
                if (current.DatasetVersionId != row.Run.DatasetVersionId || current.ContentSha256 != row.Run.DatasetContentSha256)
                {
                    Workspace.CompleteAdoption(false, OpenVisionLanguageService.T("WpfComparisonWorkspace.DatasetChanged"));
                    return;
                }
                ModelDecisionRule decisionRule = projectData.ProjectSettings.ModelRegistry.TrainingRuns.FirstOrDefault(run => run?.TrainingRunId == row.History.TrainingRunId)?.DecisionRule;
                if (decisionRule != null && !ModelRegistryService.DecisionRuleMatches(decisionRule, row.History.TrainingRunId, row.Run.WeightsSha256,
                    current.DatasetVersionId, current.ContentSha256, RecipeDatasetVersionService.CreateDecisionReviewEvidenceSha256(projectData)))
                { Workspace.CompleteAdoption(false, OpenVisionLanguageService.T("WpfComparisonWorkspace.RuleStale")); return; }
                string weightsPath = File.Exists(row.History.WeightsPath) ? row.History.WeightsPath : row.History.ArtifactPath;
                if (!ModelArtifactStoreService.Verify(weightsPath, row.Run.WeightsSha256))
                {
                    Workspace.CompleteAdoption(false, OpenVisionLanguageService.T("WpfComparisonWorkspace.WeightsChanged"));
                    return;
                }
                ModelCandidateLifecycleResult result = context.AdoptWorkspaceModel?.Invoke(row.History, row.Run, reason);
                committed = result?.IsCommitted == true;
                Workspace.CompleteAdoption(result?.IsCommitted == true, result?.IsCommitted == true
                    ? OpenVisionLanguageService.T("WpfComparisonWorkspace.Adopted")
                    : string.Format(OpenVisionLanguageService.T("WpfComparisonWorkspace.AdoptionFailed"), result?.Error?.Message ?? result?.Status.ToString()));
                if (result?.IsCommitted == true)
                {
                    RefreshWorkspace();
                    Workspace.SetLocalizedStatus("Adopted");
                    Workspace.ShowAdoptionUndoNotice(ModelCandidateLifecycleWorkflowService.FindUndoableAdoption(projectData)?.AdoptionId, row.ModelName);
                    StartAdoptionUndoTimer();
                }
            }
            catch (Exception ex)
            {
                if (committed) Workspace.SetLocalizedStatus("AdoptionRefreshFailed", ex.Message);
                else Workspace.CompleteAdoption(false, string.Format(OpenVisionLanguageService.T("WpfComparisonWorkspace.AdoptionFailed"), ex.Message));
            }
        }

        private void UndoWorkspaceAdoption(string adoptionId)
        {
            if (disposed || context.IsApplicationCloseApproved?.Invoke() == true || context.IsWorkspaceBusyProvider?.Invoke() == true) return;
            adoptionUndoTimer?.Stop();
            bool committed = false;
            try
            {
                ModelCandidateLifecycleResult result = context.UndoWorkspaceAdoption?.Invoke(adoptionId);
                committed = result?.IsCommitted == true;
                if (committed)
                {
                    Workspace.HideAdoptionUndoNotice();
                    RefreshWorkspace();
                    Workspace.SetLocalizedStatus("UndoCompleted");
                }
                else
                {
                    Workspace.SetLocalizedStatus("UndoFailed", result?.Error?.Message ?? result?.Status.ToString());
                    Workspace.ValidateAdoptionUndoNotice(ModelCandidateLifecycleWorkflowService.FindUndoableAdoption(projectData)?.AdoptionId);
                    StartAdoptionUndoTimer();
                }
            }
            catch (Exception ex)
            {
                if (committed) Workspace.SetLocalizedStatus("UndoRefreshFailed", ex.Message);
                else { Workspace.SetLocalizedStatus("UndoFailed", ex.Message); StartAdoptionUndoTimer(); }
            }
        }

        private void StartAdoptionUndoTimer()
        {
            if (disposed) return;
            if (adoptionUndoTimer == null)
            {
                adoptionUndoTimer = new DispatcherTimer(DispatcherPriority.Background, context.UiDispatcher ?? Dispatcher.CurrentDispatcher) { Interval = TimeSpan.FromSeconds(6) };
                adoptionUndoTimer.Tick += OnAdoptionUndoExpired;
            }
            adoptionUndoTimer.Stop();
            adoptionUndoTimer.Start();
        }

        private void OnAdoptionUndoExpired(object sender, EventArgs e)
        {
            adoptionUndoTimer?.Stop();
            if (!disposed) Workspace?.HideAdoptionUndoNotice();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (adoptionUndoTimer != null) { adoptionUndoTimer.Stop(); adoptionUndoTimer.Tick -= OnAdoptionUndoExpired; }
            if (workspaceConfigured && Workspace != null) { Workspace.RefreshRequested -= RefreshWorkspace; Workspace.HideAdoptionUndoNotice(); }
        }

        private ModelComparisonRunRequest BuildCandidateComparisonRequest()
        {
            LabelingProjectData data = projectData;
            PythonModelSettings settings = data.ProjectSettings.PythonModel;
            bool comparingSelectedInferenceModel = context.IsInferenceStageActiveProvider?.Invoke() == true;
            WpfModelRegistryHistoryItem selectedModel = context.SelectedInferenceModelProvider?.Invoke();
            if (context.IsComparisonStageActiveProvider?.Invoke() == true)
            {
                return context.ModelComparisonRunService.BuildRequest(data, context.TrainingWeightsService, task: "test",
                    baselineWeightsOverride: Workspace?.SelectedA?.History.WeightsPath,
                    candidateWeightsOverride: Workspace?.SelectedB?.History.WeightsPath);
            }
            return context.ModelComparisonRunService.BuildRequest(
                data,
                context.TrainingWeightsService,
                task: "test",
                baselineWeightsOverride: comparingSelectedInferenceModel
                    ? settings.WeightsPath
                    : ModelCenterDashboardWorkflowService.ResolveComparisonCurrentWeightsPath(
                        settings.WeightsPath,
                        context.PendingTrainingBaselineWeightsPathProvider?.Invoke()),
                candidateWeightsOverride: comparingSelectedInferenceModel ? selectedModel?.WeightsPath : string.Empty);
        }

        internal SegmentationAdapterComparisonContext BuildSegmentationAdapterComparisonContext()
        {
            context.EnsureProjectSettings();
            LabelingProjectData data = projectData;
            return context.SegmentationAdapterComparisonRunService.BuildContext(
                data,
                data?.ProjectSettings?.ModelRegistry,
                data?.ProjectSettings?.PythonModel);
        }

        internal bool TryApplyLatestTrainingWeightsFromProject(bool logIfUnchanged)
        {
            context.EnsureProjectSettings();
            if (!logIfUnchanged && projectData.ProjectSettings.ModelRegistry.TrainingRuns.Any(run => run?.Parameters != null
                && string.Equals(run.TrainingRunId, projectData.ProjectSettings.TrainingGuide.LastTrainingRunId, StringComparison.Ordinal)))
            {
                context.RefreshModelCenterDashboard(null);
                return false;
            }

            PythonModelSettings settings = projectData.ProjectSettings.PythonModel;
            TrainingWeightsApplicationResult application = context.TrainingWeightsApplicationWorkflowService.StageLatestCandidate(
                new TrainingWeightsApplicationRequest
                {
                    Data = projectData,
                    CurrentWeightsPath = settings.WeightsPath
                });
            WpfTrainingWeightsComparison comparison = application.Comparison;
            string comparisonStatusText = TrainingComparisonPresentationService.BuildComparisonStatusText(comparison);
            if (LearningWorkflowViewModel != null)
            {
                UpdateTrainingComparisonViewModel(comparison, comparisonStatusText);
            }

            context.RefreshModelCenterDashboard(comparison);
            if (application.Status == TrainingWeightsApplicationStatus.NoCandidate)
            {
                if (logIfUnchanged)
                {
                    context.SetCommandStatus(string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowAdapter.NoCandidateStatus"), comparisonStatusText), false);
                    context.AppendLog(OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowAdapter.NoCandidate"));
                }

                return false;
            }

            if (application.Status == TrainingWeightsApplicationStatus.AlreadyCurrent)
            {
                if (logIfUnchanged)
                {
                    context.SetCommandStatus(comparisonStatusText, false);
                    context.AppendLog(string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowAdapter.KeptCandidate"), application.CandidateWeightsPath));
                }

                return false;
            }

            if (application.Status == TrainingWeightsApplicationStatus.NotNewer)
            {
                if (logIfUnchanged)
                {
                    context.SetCommandStatus(comparisonStatusText, false);
                    context.AppendLog(string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowAdapter.KeptSettings"), settings.WeightsPath));
                }

                return false;
            }

            string latestWeightsPath = application.CandidateWeightsPath;
            if (logIfUnchanged)
            {
                context.SetPendingTrainingBaselineWeightsPath(application.BaselineWeightsPath);
                context.LoadYoloSettings(settings, latestWeightsPath);
                context.SetHasPendingTrainingWeightsRecipeSave(true);
                context.FocusYoloModelSettingsTab();
                context.FocusSaveYoloSettingsButton();
                context.SetProjectConfigStatus(OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowAdapter.SettingsSelected"));
            }

            string latestDisplayName = TrainingWeightsService.FormatWeightsDisplayPath(latestWeightsPath);
            context.RefreshModelCenterDashboard(comparison);
            context.SetGlobalInferenceStatus(string.Empty, false);
            context.SetModelStatus(string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowAdapter.ModelSelected"), latestDisplayName));
            context.SetCommandStatus(string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowAdapter.Completed"), latestDisplayName, comparison.MetricsStatusText), false);

            if (!string.Equals(context.LastAutoAppliedTrainingWeightsPathProvider?.Invoke(), latestWeightsPath, StringComparison.OrdinalIgnoreCase))
            {
                context.SetLastAutoAppliedTrainingWeightsPath(latestWeightsPath);
                context.AppendLog(string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowAdapter.Registered"), latestWeightsPath, application.BaselineWeightsPath, comparison.MetricsStatusText));
            }

            return true;
        }

        internal WpfTrainingWeightsComparison BuildCurrentTrainingWeightsComparison()
        {
            if (context.BuildCurrentTrainingWeightsComparison != null)
            {
                return context.BuildCurrentTrainingWeightsComparison();
            }

            context.EnsureProjectSettings();
            PythonModelSettings settings = projectData.ProjectSettings.PythonModel;
            return context.ModelCenterDashboardWorkflowService.BuildComparison(
                projectData,
                settings.WeightsPath,
                context.PendingTrainingBaselineWeightsPathProvider?.Invoke());
        }

        internal void UpdateTrainingComparisonViewModel(WpfTrainingWeightsComparison comparison, string comparisonStatusText = null)
        {
            if (LearningWorkflowViewModel == null)
            {
                return;
            }

            TrainingComparisonPresentation presentation = TrainingComparisonPresentationService.Build(comparison);
            comparisonStatusText ??= presentation.StatusText;
            LearningWorkflowViewModel.SetTrainingComparisonResultTexts(
                summaryText: presentation.SummaryText,
                comparisonText: comparisonStatusText,
                adoptionDecisionText: presentation.AdoptionDecisionText);
            LearningWorkflowViewModel.SetTrainingResultReportItems(presentation.ResultReportItems);
            UpdateCandidateModelComparisonReviewPanel(comparison);
        }

        internal void UpdateCandidateModelComparisonReviewPanel(WpfTrainingWeightsComparison comparison = null)
        {
            if (CandidateReviewViewModel == null)
            {
                return;
            }

            comparison ??= BuildCurrentTrainingWeightsComparison();
            IReadOnlyList<string> classNames = projectData?.ClassNamedList == null
                ? Array.Empty<string>()
                : projectData.ClassNamedList
                    .Select(item => item?.Text ?? string.Empty)
                    .ToList();
            double confidence = projectData?.ProjectSettings?.PythonModel?.MinimumDetectionConfidence ?? 0.25D;
            CandidateReviewViewModel.SetModelComparisonSourceText(
                InferenceStatusPresentationService.BuildModelComparisonSourceText(
                    projectData?.ProjectSettings?.PythonModel,
                    comparison?.CurrentWeightsPath,
                    comparison?.LatestWeightsPath));
            WpfModelComparisonHistoryItem historyItem = RefreshHistoryItems(
                comparison?.CurrentWeightsPath,
                comparison?.LatestWeightsPath);
            WpfModelComparisonReviewReport report = historyItem == null
                ? WpfModelComparisonReviewReport.Empty
                : BuildHistoryReport(historyItem);
            CandidateReviewViewModel.SetModelComparisonReview(
                report,
                isHistoricalSelection: historyItem?.IsLatest == false);
            context.UpdateCandidateModelDecisionPanel(comparison);
        }

        internal ModelComparisonCallbacks CreateCallbacks()
        {
            bool isInferenceComparison = context.IsInferenceStageActiveProvider?.Invoke() == true;
            bool isWorkspaceComparison = context.IsComparisonStageActiveProvider?.Invoke() == true;
            bool workspaceResultApplied = false;
            return new ModelComparisonCallbacks
            {
                Prepare = refreshReadiness =>
                {
                    context.EnsureProjectSettings();
                    if (!isInferenceComparison && !isWorkspaceComparison)
                    {
                        context.SaveTrainingEditorFields();
                    }
                    if (refreshReadiness)
                    {
                        context.RefreshTrainingReadinessPanel(true);
                    }
                },
                ModelEngine = () => projectData.ProjectSettings.PythonModel.ModelEngine,
                DatasetPurpose = () => projectData.ProjectSettings.DatasetPurpose,
                HasSegmentationSettings = () => TrainingSettingsViewModel != null,
                RefreshCommands = () =>
                {
                    context.RefreshCommands();
                    Workspace?.SetCommandAvailability(LearningWorkflowViewModel?.IsRunModelComparisonEnabled == true, context.IsWorkspaceBusyProvider?.Invoke() == true);
                },
                SetComparisonResult = (summary, comparison, decision) =>
                    LearningWorkflowViewModel.SetTrainingComparisonResultTexts(summary, comparison, decision),
                AdoptionDecisionText = () => LearningWorkflowViewModel.TrainingModelAdoptionDecisionText,
                RefreshCandidateComparison = () =>
                {
                    WpfTrainingWeightsComparison comparison = BuildCurrentTrainingWeightsComparison();
                    UpdateTrainingComparisonViewModel(
                        comparison,
                        TrainingComparisonPresentationService.BuildComparisonStatusText(comparison));
                },
                ApplyCandidateComparisonResult = (result, request, report) =>
                {
                    ApplyCandidateComparisonResult(result, request, report, isInferenceComparison);
                    workspaceResultApplied = true;
                },
                RefreshHistory = RefreshHistoryItems,
                SetComparisonSource = text => CandidateReviewViewModel.SetModelComparisonSourceText(text),
                ApplyReview = (report, historical) =>
                    CandidateReviewViewModel.SetModelComparisonReview(report, isHistoricalSelection: historical),
                ClearCandidateDecision = () =>
                    CandidateReviewViewModel.SetModelCandidateDecisionState(false, false, null, null, null, null),
                AddReviewHistory = text => CandidateReviewViewModel?.AddReviewHistory(text),
                ShowReview = () => { if (!isWorkspaceComparison) context.ShowCandidateReviewWorkflowView?.Invoke(); },
                SetCommandStatus = (text, busy) =>
                {
                    context.SetCommandStatus(text, busy);
                    if (isWorkspaceComparison)
                    {
                        if (busy) Workspace?.SetLocalizedStatus("Evaluating");
                        else if (workspaceResultApplied) Workspace?.SetLocalizedStatus("Completed");
                        else Workspace?.SetLocalizedStatus("EvaluationFailed", text);
                    }
                },
                AppendLog = context.AppendLog,
                SetSegmentationResult = (running, status, detail, action) =>
                    TrainingSettingsViewModel.SetSegmentationAdapterComparisonExecutionState(
                        running,
                        status,
                        detail,
                        action)
            };
        }

        private void ApplyCandidateComparisonResult(
            ModelComparisonRunResult result,
            ModelComparisonRunRequest request,
            WpfModelComparisonReviewReport report,
            bool isInferenceComparison)
        {
            if (context.IsComparisonStageActiveProvider?.Invoke() == true)
            {
                RefreshWorkspace();
                Workspace?.SetLocalizedStatus("Completed");
                return;
            }
            if (!isInferenceComparison)
            {
                WpfTrainingWeightsComparison comparison = BuildCurrentTrainingWeightsComparison();
                UpdateTrainingComparisonViewModel(
                    comparison,
                    TrainingComparisonPresentationService.BuildComparisonStatusText(comparison));
                return;
            }

            WpfModelComparisonHistoryItem historyItem = RefreshHistoryItems(
                request.BaselineWeightsPath,
                request.CandidateWeightsPath,
                result.SummaryPath);
            CandidateReviewViewModel?.SetModelComparisonSourceText(
                InferenceStatusPresentationService.BuildModelComparisonSourceText(
                    projectData?.ProjectSettings?.PythonModel,
                    request.BaselineWeightsPath,
                    request.CandidateWeightsPath));
            CandidateReviewViewModel?.SetModelComparisonReview(
                report,
                isHistoricalSelection: historyItem?.IsLatest == false);
        }

        internal WpfModelComparisonHistoryItem RefreshHistoryItems(
            string baselineWeightsPath,
            string candidateWeightsPath,
            string preferredSummaryPath = "")
        {
            if (string.IsNullOrWhiteSpace(baselineWeightsPath)
                || string.IsNullOrWhiteSpace(candidateWeightsPath))
            {
                CandidateReviewViewModel?.SetModelComparisonHistory(
                    Array.Empty<WpfModelComparisonHistoryItem>());
                return null;
            }

            IReadOnlyList<WpfModelComparisonHistoryItem> items = context.ModelComparisonReviewService.BuildHistory(
                baselineWeightsPath,
                candidateWeightsPath,
                maxItems: 8);
            CandidateReviewViewModel?.SetModelComparisonHistory(items, preferredSummaryPath);
            return CandidateReviewViewModel?.SelectedModelComparisonHistoryItem;
        }

        internal WpfModelComparisonReviewReport BuildHistoryReport(WpfModelComparisonHistoryItem item)
        {
            if (item == null)
            {
                return WpfModelComparisonReviewReport.Empty;
            }

            IReadOnlyList<string> classNames = projectData?.ClassNamedList == null
                ? Array.Empty<string>()
                : projectData.ClassNamedList
                    .Select(classItem => classItem?.Text ?? string.Empty)
                    .ToList();
            double confidence = projectData?.ProjectSettings?.PythonModel?.MinimumDetectionConfidence ?? 0.25D;
            return context.ModelComparisonReviewService.BuildFromSummaryFile(
                item.SourcePath,
                classNames,
                confidence,
                maxExamples: 5);
        }

        internal void ApplyHistorySelectionEffects(WpfModelComparisonHistoryItem item)
        {
            WpfModelComparisonReviewReport report = BuildHistoryReport(item);
            if (!report.HasComparison)
            {
                context.AppendLog(string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowAdapter.HistoryReadFailed"), item.SourcePath));
                return;
            }

            CandidateReviewViewModel?.SetModelComparisonSourceText(
                $"{item.DisplayText} / {item.DetailText} / {item.SourcePath}");
            CandidateReviewViewModel?.SetModelComparisonReview(
                report,
                isHistoricalSelection: !item.IsLatest);
            try
            {
                context.RefreshModelCenterDashboard(BuildCurrentTrainingWeightsComparison());
            }
            catch (Exception ex)
            {
                context.AppendLog(string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowAdapter.HistoryDecisionFailed"), ex.Message));
            }

            context.AppendLog(string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfComparison.Text.ModelComparisonWorkflowAdapter.HistorySelected"), item.DisplayText));
        }
    }

    internal sealed class ModelComparisonWorkflowAdapterContext
    {
        internal Func<LabelingProjectData> DataProvider { get; init; }
        internal ModelComparisonRunService ModelComparisonRunService { get; init; }
        internal SegmentationAdapterComparisonRunService SegmentationAdapterComparisonRunService { get; init; }
        internal ModelComparisonReviewService ModelComparisonReviewService { get; init; }
        internal ModelCenterDashboardWorkflowService ModelCenterDashboardWorkflowService { get; init; }
        internal TrainingWeightsService TrainingWeightsService { get; init; }
        internal TrainingWeightsApplicationWorkflowService TrainingWeightsApplicationWorkflowService { get; init; }
        internal Func<WpfTrainingSettingsPanelViewModel> TrainingSettingsViewModelProvider { get; init; }
        internal Func<WpfLearningWorkflowPanelViewModel> LearningWorkflowViewModelProvider { get; init; }
        internal Func<WpfCandidateReviewPanelViewModel> CandidateReviewViewModelProvider { get; init; }
        internal Func<WpfModelRegistryHistoryItem> SelectedInferenceModelProvider { get; init; }
        internal Func<bool> IsInferenceStageActiveProvider { get; init; }
        internal Func<bool> IsComparisonStageActiveProvider { get; init; }
        internal Func<ModelComparisonWorkspaceViewModel> WorkspaceViewModelProvider { get; init; }
        internal Func<bool> IsWorkspaceBusyProvider { get; init; }
        internal Func<bool> IsApplicationCloseApproved { get; init; }
        internal Func<string> RepositoryRootProvider { get; init; }
        internal Func<WpfModelRegistryHistoryItem, WpfModelBenchmarkRun, string, ModelCandidateLifecycleResult> AdoptWorkspaceModel { get; init; }
        internal Func<string, ModelCandidateLifecycleResult> UndoWorkspaceAdoption { get; init; }
        internal Dispatcher UiDispatcher { get; init; }
        internal Action<string> ReviewWorkspaceLabels { get; init; }
        internal Func<string> PendingTrainingBaselineWeightsPathProvider { get; init; }
        internal Func<string> LastAutoAppliedTrainingWeightsPathProvider { get; init; }
        internal Func<WpfTrainingWeightsComparison> BuildCurrentTrainingWeightsComparison { get; init; }
        internal Action EnsureProjectSettings { get; init; }
        internal Action SaveTrainingEditorFields { get; init; }
        internal Action<bool> RefreshTrainingReadinessPanel { get; init; }
        internal Action RefreshCommands { get; init; }
        internal Func<bool> SaveDecisionRuleMetadata { get; init; }
        internal Action<TrainingRun> EvaluateDecisionRule { get; init; }
        internal Action<WpfTrainingWeightsComparison> RefreshModelCenterDashboard { get; init; }
        internal Action<WpfTrainingWeightsComparison> UpdateCandidateModelDecisionPanel { get; init; }
        internal Action ShowCandidateReviewWorkflowView { get; init; }
        internal Action<string, bool> SetCommandStatus { get; init; }
        internal Action<string> AppendLog { get; init; }
        internal Action<PythonModelSettings, string> LoadYoloSettings { get; init; }
        internal Action<string> SetPendingTrainingBaselineWeightsPath { get; init; }
        internal Action<bool> SetHasPendingTrainingWeightsRecipeSave { get; init; }
        internal Action<string> SetLastAutoAppliedTrainingWeightsPath { get; init; }
        internal Action<string> SetModelStatus { get; init; }
        internal Action<string> SetProjectConfigStatus { get; init; }
        internal Action<string, bool> SetGlobalInferenceStatus { get; init; }
        internal Action FocusYoloModelSettingsTab { get; init; }
        internal Action FocusSaveYoloSettingsButton { get; init; }
    }
}
