using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.Yolo;
using OpenVisionLab;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the Shell-facing batch detection workflow orchestration.
    /// BatchDetectionWorkflowService remains the execution and lifetime owner;
    /// this adapter translates its callbacks into existing queue, canvas,
    /// candidate-review, and status owners.
    /// </summary>
    internal sealed class BatchDetectionWorkflowAdapter
    {
        private readonly BatchDetectionWorkflowAdapterContext context;
        private bool isRefreshingCandidateQueueState;

        private LabelingProjectData projectData => context.DataProvider?.Invoke();

        internal BatchDetectionWorkflowAdapter(BatchDetectionWorkflowAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            ArgumentNullException.ThrowIfNull(context.DataProvider);
            ArgumentNullException.ThrowIfNull(context.BatchDetectionWorkflowService);
            ArgumentNullException.ThrowIfNull(context.BatchDetectionProgressService);
            ArgumentNullException.ThrowIfNull(context.DetectionTargetService);
            ArgumentNullException.ThrowIfNull(context.ImageDetectionWorkflowService);
            ArgumentNullException.ThrowIfNull(context.ImageQualityReviewWorkflowService);
            ArgumentNullException.ThrowIfNull(context.AnomalyImageReviewSession);
            ArgumentNullException.ThrowIfNull(context.DetectionResultPresentationService);
            ArgumentNullException.ThrowIfNull(context.CandidateReviewState);
            ArgumentNullException.ThrowIfNull(context.QueueItemsProvider);
            ArgumentNullException.ThrowIfNull(context.VisibleQueueItemsProvider);
            ArgumentNullException.ThrowIfNull(context.RunInteractiveDetectionAsync);
            ArgumentNullException.ThrowIfNull(context.RunWorkerDetectionAsync);
            ArgumentNullException.ThrowIfNull(context.YieldBatchDetectionResultFrameAsync);
            ArgumentNullException.ThrowIfNull(context.EnsurePythonModelClientReadyAsync);
            ArgumentNullException.ThrowIfNull(context.CommunicationStatusProvider);
            ArgumentNullException.ThrowIfNull(context.PythonWorkerLastErrorProvider);
            ArgumentNullException.ThrowIfNull(context.SelectedInferenceModelProvider);
            ArgumentNullException.ThrowIfNull(context.RunTrialModelInteractiveDetectionAsync);
            ArgumentNullException.ThrowIfNull(context.CreateTrialModelSettings);
            ArgumentNullException.ThrowIfNull(context.RunTrialModelDetectionForImageAsync);
        }

        internal void ExecuteDetectSelectedQueueCommand()
        {
            _ = ExecuteDetectSelectedQueueCommandAsync();
        }

        internal async Task ExecuteDetectSelectedQueueCommandAsync()
        {
            if (context.EnsureInferenceModeForDetection?.Invoke() != true)
            {
                return;
            }

            WpfImageQueueItem item = context.SelectedQueueItemProvider?.Invoke();
            if (item == null)
            {
                context.AppendLog?.Invoke(OpenVisionLab.OpenVisionLanguageService.T("WpfBatch.Run.SelectImage"));
                return;
            }

            WpfModelRegistryHistoryItem selectedModel = context.SelectedInferenceModelProvider?.Invoke();
            if (selectedModel != null && !selectedModel.IsCurrentInspectionModel)
            {
                PythonModelSettings trialSettings = context.CreateTrialModelSettings?.Invoke(selectedModel);
                if (trialSettings == null)
                {
                    context.AppendLog?.Invoke(OpenVisionLanguageService.T("WpfInference.Model.TrialUnavailable"));
                    return;
                }

                await context.RunTrialModelInteractiveDetectionAsync(
                    item.ImagePath,
                    trialSettings,
                    selectedModel.CandidateId).ConfigureAwait(true);
                return;
            }

            await context.RunInteractiveDetectionAsync(item.ImagePath, false).ConfigureAwait(true);
        }

        internal void ExecuteBatchDetectQueueCommand()
        {
            _ = ExecuteBatchDetectQueueCommandAsync();
        }

        internal async Task ExecuteBatchCandidateQueueCommandAsync()
        {
            if (!CanGenerateCandidateBatch()) return;
            LabelingProjectData originalData = projectData;
            InferenceResultReviewService cache = context.InferenceResultReviewServiceProvider?.Invoke();
            PythonModelSettings settings = ImageDetectionWorkflowService.CloneSettings(context.CandidateModelSettingsProvider?.Invoke());
            string originalScope = context.CandidateRecipeIdentityProvider?.Invoke();
            string modelIdentity = context.CandidateModelIdentityProvider?.Invoke();
            string trainingRunIdentity = context.CandidateTrainingRunIdentityProvider?.Invoke();
            string artifactIdentity = context.CandidateArtifactIdentityProvider?.Invoke();
            string settingsIdentity = PythonModelSettings.BuildCandidateSettingsIdentity(settings, modelIdentity + "|" + trainingRunIdentity);
            string[] classNames = originalData?.ClassNamedList?.Select(label => label?.Text).ToArray() ?? Array.Empty<string>();
            bool?[] archivedClasses = originalData?.ClassNamedList?.Select(label => label?.IsArchived).ToArray() ?? Array.Empty<bool?>();
            if (cache == null || string.IsNullOrWhiteSpace(originalScope) || string.IsNullOrWhiteSpace(modelIdentity)
                || string.IsNullOrWhiteSpace(artifactIdentity) || !ArtifactMatches(settings, artifactIdentity)) return;
            cache.SetScope(originalScope);
            cache.SelectModel(settings);
            RefreshCandidateQueueState();
            WpfImageQueueItem[] originalCatalog = context.QueueItemsProvider().ToArray();
            bool IsCurrent() => context.IsApplicationCloseApproved?.Invoke() != true && ReferenceEquals(originalData, projectData)
                && string.Equals(originalScope, context.CandidateRecipeIdentityProvider?.Invoke(), StringComparison.Ordinal)
                && string.Equals(trainingRunIdentity, context.CandidateTrainingRunIdentityProvider?.Invoke(), StringComparison.Ordinal)
                && string.Equals(artifactIdentity, context.CandidateArtifactIdentityProvider?.Invoke(), StringComparison.OrdinalIgnoreCase)
                && classNames.SequenceEqual(projectData?.ClassNamedList?.Select(label => label?.Text) ?? Array.Empty<string>())
                && archivedClasses.SequenceEqual(projectData?.ClassNamedList?.Select(label => label?.IsArchived) ?? Array.Empty<bool?>())
                && string.Equals(settingsIdentity, PythonModelSettings.BuildCandidateSettingsIdentity(context.CandidateModelSettingsProvider?.Invoke(), GetCandidateModelIdentity()), StringComparison.Ordinal)
                && originalCatalog.SequenceEqual(context.QueueItemsProvider());
            var imageIdentities = originalCatalog.Where(item => item != null).ToDictionary(item => item.ImagePath, item => GetFileIdentity(item.ImagePath), StringComparer.OrdinalIgnoreCase);
            WpfImageQueueItem[] queue = originalCatalog.Where(item => item != null && !item.HasSavedLabelFile && !item.HasAnomalyVerdict
                && !item.IsSaveRequired && item.QualityReviewState == YoloImageQualityReviewState.Unreviewed
                && item.SessionCandidateCount == 0 && !(IsActiveImagePath(item.ImagePath) && context.CandidateReviewState.HasPendingCandidates)
                && cache.FindRelevant(item.ImagePath, artifactIdentity, imageIdentities[item.ImagePath], settingsIdentity)?.Result == null).ToArray();
            if (queue.Length == 0) return;
            BatchDetectionRun run = context.BatchDetectionWorkflowService.TryBegin(queue.Length, null);
            if (run == null) return;
            int withCandidates = 0, empty = 0, failed = 0;
            string evidenceRoot = context.CandidateEvidenceOutputDirectoryProvider?.Invoke();
            string evidenceDirectory = string.IsNullOrWhiteSpace(evidenceRoot) ? string.Empty : Path.Combine(evidenceRoot, Guid.NewGuid().ToString("N"));
            context.ImageQueueViewModelProvider?.Invoke()?.SetCandidateBatchRunning(true);
            var candidateCallbacks = new ImageDetectionCallbacks
            {
                IsResultOnly = () => true,
                RecordResult = _ => { },
                SetPythonStatus = text => { if (IsCurrent()) context.SetPythonStatus?.Invoke(text); },
                AppendLog = text => { if (IsCurrent()) context.AppendLog?.Invoke(text); }
            };
            try
            {
                await context.BatchDetectionWorkflowService.ExecuteAsync(run, queue.Select(item => item.ImagePath).ToArray(), new BatchDetectionCallbacks
                {
                    PrepareAsync = _ => Task.FromResult<string>(null),
                    ItemStarting = path =>
                    {
                        if (!IsCurrent()) { run.Cancel(); return; }
                        context.BeginInferenceReference?.Invoke(path, settings);
                        UpdateBatchDetectionControls("AI", Path.GetFileName(path));
                    },
                    DetectAsync = async (path, token) =>
                    {
                        if (!IsCurrent() || !ArtifactMatches(settings, artifactIdentity)
                            || !string.Equals(imageIdentities[path], GetFileIdentity(path), StringComparison.Ordinal))
                        { run.Cancel(); token.ThrowIfCancellationRequested(); }
                        // Explicit frozen-model execution bypasses the normal batch's canvas,
                        // review-status and anomaly classification application callbacks.
                        if (!string.IsNullOrWhiteSpace(evidenceDirectory)) Directory.CreateDirectory(evidenceDirectory);
                        return await context.ImageDetectionWorkflowService.RunSmokeAsync(path, false, token, candidateCallbacks, settings,
                            decisionEvaluation: true, decisionOutputDirectory: evidenceDirectory, trainingRunIdentity: trainingRunIdentity).ConfigureAwait(true);
                    },
                    ApplyResult = (path, result, _) =>
                    {
                        WpfImageQueueItem originalItem = queue.First(item => string.Equals(item.ImagePath, path, StringComparison.OrdinalIgnoreCase));
                        if (!IsCurrent() || !ArtifactMatches(settings, artifactIdentity) || !ReferenceEquals(originalItem, context.FindQueueItem?.Invoke(path))
                            || originalItem.HasSavedLabelFile || originalItem.HasAnomalyVerdict || originalItem.IsSaveRequired
                            || originalItem.QualityReviewState != YoloImageQualityReviewState.Unreviewed
                            || !string.Equals(imageIdentities[path], GetFileIdentity(path), StringComparison.Ordinal))
                        { run.Cancel(); return false; }
                        if (result.Succeeded && (!string.Equals(result.CaptureWeightsSha256, artifactIdentity, StringComparison.OrdinalIgnoreCase)
                            || !string.Equals(result.CaptureImageSha256, imageIdentities[path], StringComparison.OrdinalIgnoreCase)))
                        {
                            result.Succeeded = false;
                            result.Candidates = Array.Empty<YoloWorkerSmokeCandidate>();
                            result.CandidateCount = 0;
                            result.Summary = OpenVisionLanguageService.T("WpfCandidate.Batch.UnverifiedResult");
                        }
                        if (!result.Succeeded && string.IsNullOrWhiteSpace(imageIdentities[path]) && cache.ScopeIdentity == originalScope)
                            cache.Record(result);
                        else if (!cache.RecordForScope(originalScope, result, artifactIdentity, imageIdentities[path], settingsIdentity, trainingRunIdentity)) { run.Cancel(); return false; }
                        context.AcceptInferenceReference?.Invoke(result);
                        if (!result.Succeeded) failed++;
                        else if (GetCandidatePresentationCount(result) == 0) empty++;
                        else withCandidates++;
                        if (result.Succeeded)
                        {
                            originalItem.SessionCandidateCount = GetCandidatePresentationCount(result);
                            originalItem.CandidateUncertainty = string.IsNullOrWhiteSpace(trainingRunIdentity) ? null : GetCandidateUncertainty(result, context.CandidateSavedAnomalyThresholdProvider?.Invoke(artifactIdentity));
                            originalItem.ModelDisagreement = LabelSuspicionReviewService.GetDisagreement(context.CandidatePredictionsProvider?.Invoke(path));
                            SetCandidateAnomalyRow(originalItem, result, context.CandidateSavedAnomalyThresholdProvider?.Invoke(artifactIdentity));
                        }
                        context.RefreshQueueView?.Invoke();
                        return false;
                    },
                    ItemCompleted = (path, _) => UpdateBatchDetectionControls("AI", Path.GetFileName(path)),
                    YieldResultFrameAsync = _ => Task.CompletedTask
                }).ConfigureAwait(true);
            }
            finally
            {
                context.FinishInferenceReference?.Invoke(context.ActiveImagePathProvider?.Invoke());
            }
            if (context.IsApplicationCloseApproved?.Invoke() == true) return;
            context.ImageQueueViewModelProvider?.Invoke()?.SetCandidateBatchRunning(false);
            UpdateBatchDetectionControls();
            if (!IsCurrent()) return;
            RefreshCandidateQueueState();
            var summary = new CandidateBatchSummary(run.CompletedCount, withCandidates, empty, failed, run.Token.IsCancellationRequested, run.FailureSummary);
            WpfImageQueuePanelViewModel viewModel = context.ImageQueueViewModelProvider?.Invoke();
            viewModel?.SetCandidateBatchSummary(summary);
            context.SetPythonStatus?.Invoke(context.BatchDetectionProgressService.BuildCandidateCompletionPythonStatus(summary.Canceled, summary.FailedImages, summary.FailureSummary));
            if (run.CompletedCount > 0 && !summary.Canceled && string.IsNullOrWhiteSpace(summary.FailureSummary))
            {
                viewModel?.SetSelectedFilter(WpfImageQueueFilter.Unlabeled);
                viewModel?.SelectCandidateSort(ImageQueueSort.Uncertain);
                context.RefreshQueueView?.Invoke();
                if (context.HasUnsavedCandidateEditorChanges?.Invoke() != true)
                {
                    WpfImageQueueItem first = GetVisibleQueueItems().FirstOrDefault(item => queue.Contains(item) && item.CandidateUncertainty.HasValue);
                    if (first != null) ShowBatchDetectionImage(first);
                }
            }
            context.CandidateBatchCompleted?.Invoke(summary);
        }

        internal bool CanGenerateCandidateBatch() => context.IsApplicationCloseApproved?.Invoke() != true
            && !context.BatchDetectionWorkflowService.IsRunning && !context.ImageDetectionWorkflowService.IsDetecting
            && context.CanGenerateCandidateBatch?.Invoke() == true;

        internal void ConfigureCandidateWorkflow()
        {
            context.ImageQueueViewModelProvider?.Invoke()?.ConfigureCandidateWorkflow(() => _ = ExecuteBatchCandidateQueueCommandAsync(),
                CanGenerateCandidateBatch, _ => context.ImageQueueViewModelProvider?.Invoke()?.ApplyCandidateSort(context.QueueCollectionViewProvider?.Invoke(), refresh: true));
            RefreshCandidateQueueState();
        }

        internal void RefreshCandidateCommandState()
        {
            WpfImageQueuePanelViewModel viewModel = context.ImageQueueViewModelProvider?.Invoke();
            viewModel?.SetCandidateWorkflowVisible(context.IsApplicationCloseApproved?.Invoke() != true && context.CandidateLabelingStageProvider?.Invoke() == true);
            viewModel?.RefreshCandidateCommands();
        }

        // Routine UI projection uses already captured session provenance. Content
        // verification belongs to explicit inference/navigation/catalog actions.
        internal void RefreshCandidateQueueState(bool verifyContent = false, string updatedImagePath = null, bool refreshDisagreement = true)
        {
            if (isRefreshingCandidateQueueState || context.IsApplicationCloseApproved?.Invoke() == true) return;
            isRefreshingCandidateQueueState = true;
            try
            {
                RefreshCandidateCommandState();
                if (context.CandidateLabelingStageProvider?.Invoke() != true) return;
                InferenceResultReviewService cache = context.InferenceResultReviewServiceProvider?.Invoke();
                string scope = context.CandidateRecipeIdentityProvider?.Invoke();
                bool scopeChanged = !string.Equals(cache?.ScopeIdentity, scope, StringComparison.OrdinalIgnoreCase);
                cache?.SetScope(scope);
                PythonModelSettings settings = context.CandidateModelSettingsProvider?.Invoke();
                string modelIdentity = context.CandidateModelIdentityProvider?.Invoke();
                string artifact = context.CandidateArtifactIdentityProvider?.Invoke();
                var configuredArtifactIdentity = new Lazy<string>(() => GetFileIdentity(settings?.WeightsPath));
                string settingsIdentity = PythonModelSettings.BuildCandidateSettingsIdentity(settings, GetCandidateModelIdentity());
                bool relevantModel = cache != null && !string.IsNullOrWhiteSpace(modelIdentity) && !string.IsNullOrWhiteSpace(artifact);
                if (relevantModel) cache.SelectModel(settings);
                if (verifyContent && relevantModel && cache.ResultPaths.Count > 0) relevantModel = ArtifactMatches(settings, artifact);
                bool hasTrainedModel = !string.IsNullOrWhiteSpace(context.CandidateTrainingRunIdentityProvider?.Invoke());
                double? threshold = verifyContent ? context.CandidateSavedAnomalyThresholdProvider?.Invoke(artifact) : null;
                WpfImageQueueItem[] rows = context.QueueItemsProvider().Where(item => item != null).ToArray();
                string activePath = context.ActiveImagePathProvider?.Invoke();
                InferenceResultReviewEntry activeEntry = null;
                InferenceResultReviewEntry activeTransfer = context.CandidateReviewState.LabelingTransferEntry;
                bool transferRelevant = activeTransfer != null && IsCandidateSourceRelevant(activeTransfer, verifyContent
                    && (string.IsNullOrWhiteSpace(updatedImagePath) || string.Equals(updatedImagePath, activePath, StringComparison.OrdinalIgnoreCase)), configuredArtifactIdentity);
                foreach (WpfImageQueueItem item in rows)
                {
                    bool verifyImage = verifyContent && (string.IsNullOrWhiteSpace(updatedImagePath)
                        || string.Equals(updatedImagePath, item.ImagePath, StringComparison.OrdinalIgnoreCase));
                    InferenceResultReviewEntry stored = relevantModel ? cache.Find(item.ImagePath) : null;
                    string imageIdentity = verifyImage && stored?.Result != null ? GetFileIdentity(item.ImagePath) : stored?.ImageIdentity;
                    InferenceResultReviewEntry entry = relevantModel ? cache.FindRelevant(item.ImagePath, artifact, imageIdentity, settingsIdentity) : null;
                    InferenceResultReviewEntry draft = cache?.FindCandidateDraft(item.ImagePath);
                    bool draftRelevant = draft != null && IsCandidateSourceRelevant(draft, verifyImage, configuredArtifactIdentity);
                    item.SessionCandidateCount = item.HasAnomalyVerdict ? 0 : draftRelevant ? draft.PendingDraft.Count
                        : entry?.PendingDraft?.Count ?? GetCandidatePresentationCount(entry?.Result);
                    if (transferRelevant && IsActiveImagePath(item.ImagePath)
                        && string.Equals(activeTransfer.Result.Source.ImagePath, item.ImagePath, StringComparison.OrdinalIgnoreCase))
                        item.SessionCandidateCount = context.CandidateReviewState.PendingCount;
                    if (!hasTrainedModel || entry?.Result == null) item.CandidateUncertainty = null;
                    else if (verifyImage) item.CandidateUncertainty = GetCandidateUncertainty(entry.Result, threshold);
                    if (verifyImage && refreshDisagreement) item.ModelDisagreement = LabelSuspicionReviewService.GetDisagreement(context.CandidatePredictionsProvider?.Invoke(item.ImagePath));
                    else if (scopeChanged || !relevantModel) item.ModelDisagreement = null;
                    if (item.HasAnomalyVerdict || entry == null) SetCandidateAnomalyRow(item, null, null);
                    else if (verifyImage) SetCandidateAnomalyRow(item, entry.Result, threshold);
                    if (IsActiveImagePath(item.ImagePath)) activeEntry = entry;
                }
                if (activeTransfer != null && (!transferRelevant
                    || !string.Equals(activeTransfer.Result?.Source?.ImagePath, activePath, StringComparison.OrdinalIgnoreCase)))
                {
                    context.CandidateReviewState.LoadPendingCandidates(Array.Empty<YoloWorkerSmokeCandidate>(), clearConfirmed: false);
                    context.RefreshCandidateList?.Invoke();
                    context.RedrawReviewRois?.Invoke();
                }
                bool activeVerdictSaved = context.FindQueueItem?.Invoke(activePath)?.HasAnomalyVerdict == true;
                if (activeVerdictSaved || activeEntry == null) context.CandidateAnomalyRestored?.Invoke(null, null);
                else if (verifyContent && (string.IsNullOrWhiteSpace(updatedImagePath) || string.Equals(updatedImagePath, activePath, StringComparison.OrdinalIgnoreCase)))
                    context.CandidateAnomalyRestored?.Invoke(activeEntry.Result, threshold);
                WpfImageQueuePanelViewModel viewModel = context.ImageQueueViewModelProvider?.Invoke();
                viewModel?.SetCandidateFilterCounts(rows.Count(item => item.SessionCandidateCount > 0 || item.ReviewState == YoloImageReviewState.Candidate), rows.Count(item => item.LabelSuspicionCount > 0));
                viewModel?.SetCandidateSortAvailability(rows.Any(item => item.CandidateUncertainty.HasValue), rows.Any(item => item.ModelDisagreement.HasValue),
                    OpenVisionLanguageService.T("WpfCandidate.Batch.SortUnavailable"));
                viewModel?.ApplyCandidateSort(context.QueueCollectionViewProvider?.Invoke());
            }
            finally { isRefreshingCandidateQueueState = false; }
        }

        internal void SaveActiveCandidateDraft()
        {
            InferenceResultReviewEntry entry = context.CandidateReviewState.LabelingTransferEntry;
            string path = context.ActiveImagePathProvider?.Invoke();
            if (entry?.Result?.Source == null || !string.Equals(entry.Result.Source.ImagePath, path, StringComparison.OrdinalIgnoreCase)) return;
            if (!IsCandidateSourceRelevant(entry, verifyContent: true)) return;
            InferenceResultReviewService cache = context.InferenceResultReviewServiceProvider?.Invoke();
            IEnumerable<YoloWorkerSmokeCandidate> confirmed = (entry.ConfirmedDraft ?? Array.Empty<YoloWorkerSmokeCandidate>())
                .Concat(context.CandidateReviewState.ConfirmedCandidates).GroupBy(candidate => candidate.Index).Select(group => group.Last());
            cache?.SaveCandidateDraftForEntry(context.CandidateRecipeIdentityProvider?.Invoke(), entry, context.CandidateReviewState.PendingCandidates, confirmed);
        }

        internal void AttachActiveCandidateResult(YoloWorkerSmokeTestResult result)
        {
            if (context.CandidateLabelingStageProvider?.Invoke() != true || result?.Succeeded != true || result.Source?.ResultOnly != false
                || !IsActiveImagePath(result.Source.ImagePath)) return;
            InferenceResultReviewService cache = context.InferenceResultReviewServiceProvider?.Invoke();
            InferenceResultReviewEntry entry = cache?.Find(result.Source.ImagePath);
            if (ReferenceEquals(entry?.Result, result) && IsCandidateSourceRelevant(entry, verifyContent: false))
                context.CandidateReviewState.AttachInferenceResultForLabeling(entry);
        }

        internal void RestoreCandidateDraft(string imagePath)
        {
            context.CandidateAnomalyRestored?.Invoke(null, null);
            InferenceResultReviewService cache = context.InferenceResultReviewServiceProvider?.Invoke();
            PythonModelSettings settings = context.CandidateModelSettingsProvider?.Invoke();
            string artifact = context.CandidateArtifactIdentityProvider?.Invoke();
            if (cache == null) return;
            InferenceResultReviewEntry entry = cache.FindCandidateDraft(imagePath);
            if (entry != null && !IsCandidateSourceRelevant(entry, verifyContent: true)) return;
            if (entry == null)
            {
                if (!ArtifactMatches(settings, artifact)) return;
                cache.SelectModel(settings);
                entry = cache.FindRelevant(imagePath, artifact, GetFileIdentity(imagePath), PythonModelSettings.BuildCandidateSettingsIdentity(settings, GetCandidateModelIdentity()));
            }
            if (entry == null) return;
            if (entry.Result.Candidates?.Any(candidate => candidate?.ImageLevel == true) == true)
            {
                if (context.FindQueueItem?.Invoke(imagePath)?.HasAnomalyVerdict != true)
                    context.CandidateAnomalyRestored?.Invoke(entry.Result, context.CandidateSavedAnomalyThresholdProvider?.Invoke(artifact));
                return;
            }
            // Confirmed work already passed the save/discard admission and saved GT
            // has just been loaded. Restore review progress without adding it twice.
            bool restored = context.CandidateReviewState.RestoreCandidateDraft(entry, restoreConfirmed: false)
                || context.CandidateReviewState.LoadInferenceResultForLabeling(entry, 0, projectData?.ClassNamedList);
            if (!restored) return;
            context.RefreshCandidateList?.Invoke();
            context.RefreshObjectList?.Invoke();
            context.RedrawReviewRois?.Invoke();
        }

        private string GetCandidateModelIdentity()
            => (context.CandidateModelIdentityProvider?.Invoke() ?? string.Empty) + "|" + (context.CandidateTrainingRunIdentityProvider?.Invoke() ?? string.Empty);

        private bool IsCandidateSourceRelevant(InferenceResultReviewEntry entry, bool verifyContent, Lazy<string> configuredArtifactIdentity = null)
        {
            InferenceResultSource source = entry?.Result?.Source;
            InferenceResultReviewService cache = context.InferenceResultReviewServiceProvider?.Invoke();
            if (source == null || entry.Result.Succeeded != true || context.IsApplicationCloseApproved?.Invoke() == true
                || cache?.IsCurrentEntry(context.CandidateRecipeIdentityProvider?.Invoke(), entry) != true) return false;
            ModelRegistrySettings registry = projectData?.ProjectSettings?.ModelRegistry;
            ModelCandidate[] matches = registry?.Candidates?.Where(candidate => candidate?.CandidateId == source.CandidateId).ToArray() ?? Array.Empty<ModelCandidate>();
            if (matches.Length != 1) return false;
            ModelCandidate candidate = matches[0];
            if (string.Equals(candidate.Decision, ModelRegistryService.CandidateDecisionRejected, StringComparison.Ordinal)
                || !string.Equals(candidate.TrainingRunId, entry.TrainingRunIdentity, StringComparison.Ordinal)
                || !string.Equals(entry.TrainingRunIdentity, entry.Result.CaptureTrainingRunId, StringComparison.Ordinal)
                || (string.IsNullOrWhiteSpace(candidate.WeightsSha256)
                    ? !string.IsNullOrWhiteSpace(candidate.TrainingRunId) || !candidate.IsCurrentInspectionModel
                        || !string.Equals(registry.CurrentInspectionModelId, candidate.CandidateId, StringComparison.Ordinal)
                        || !string.Equals(registry.CurrentProfileId, candidate.ProfileId, StringComparison.Ordinal)
                        || !source.Matches(context.CandidateModelSettingsProvider?.Invoke(), source.ResultOnly)
                    : !string.Equals(candidate.WeightsSha256, entry.ArtifactIdentity, StringComparison.OrdinalIgnoreCase))
                || (!string.Equals(candidate.WeightsPath, source.WeightsPath, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(candidate.ArtifactPath, source.WeightsPath, StringComparison.OrdinalIgnoreCase))) return false;
            ModelProfile[] profiles = registry.Profiles?.Where(profile => profile?.ProfileId == candidate.ProfileId).ToArray() ?? Array.Empty<ModelProfile>();
            if (profiles.Length != 1 || !string.Equals(PythonModelSettings.NormalizeModelEngine(profiles[0].ModelEngine), PythonModelSettings.NormalizeModelEngine(source.ModelEngine), StringComparison.Ordinal)
                || !string.Equals(profiles[0].ProjectRootPath?.TrimEnd('\\', '/'), source.ProjectRootPath?.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)) return false;
            PythonModelSettings sourceSettings = context.CandidateSourceSettingsProvider?.Invoke(entry);
            if (sourceSettings == null || !source.Matches(sourceSettings, source.ResultOnly)
                || !string.Equals(PythonModelSettings.BuildCandidateSettingsIdentity(sourceSettings, source.CandidateId + "|" + entry.TrainingRunIdentity), entry.CandidateSettingsIdentity, StringComparison.Ordinal)
                || !string.Equals(entry.CandidateSettingsIdentity, entry.Result.CaptureCandidateSettingsIdentity, StringComparison.Ordinal)) return false;
            bool configuredArtifactVerified = false;
            if (string.IsNullOrWhiteSpace(candidate.WeightsSha256))
            {
                string actualArtifactIdentity = configuredArtifactIdentity?.Value ?? GetFileIdentity(sourceSettings.WeightsPath);
                if (string.IsNullOrWhiteSpace(entry.ArtifactIdentity) || !string.Equals(actualArtifactIdentity, entry.ArtifactIdentity, StringComparison.OrdinalIgnoreCase)) return false;
                configuredArtifactVerified = true;
            }
            return !verifyContent || ((configuredArtifactVerified || ArtifactMatches(sourceSettings, entry.ArtifactIdentity))
                && string.Equals(GetFileIdentity(source.ImagePath), entry.ImageIdentity, StringComparison.OrdinalIgnoreCase));
        }

        private int GetCandidatePresentationCount(YoloWorkerSmokeTestResult result)
            => result?.Candidates?.Any(candidate => candidate?.ImageLevel == true) == true ? result.CandidateCount
                : CandidateReviewStateService.BuildCandidatePresentation(result, projectData?.ClassNamedList).Count;

        private static bool ArtifactMatches(PythonModelSettings settings, string expected)
            => !string.IsNullOrWhiteSpace(expected) && string.Equals(GetFileIdentity(settings?.WeightsPath), expected, StringComparison.OrdinalIgnoreCase);

        private static void SetCandidateAnomalyRow(WpfImageQueueItem item, YoloWorkerSmokeTestResult result, double? threshold)
        {
            double? score = result?.Succeeded == true ? result.CaptureAnomalyScore ?? result.Candidates?.FirstOrDefault(candidate => candidate?.ImageLevel == true)?.AnomalyScore : null;
            bool valid = score.HasValue && threshold.HasValue && double.IsFinite(score.Value) && double.IsFinite(threshold.Value);
            item.SessionCandidateAnomalyScore = valid ? score : null;
            item.SessionCandidateAnomalyState = valid ? score.Value >= threshold.Value ? AnomalyImageReviewState.Abnormal : AnomalyImageReviewState.Normal : AnomalyImageReviewState.Unreviewed;
        }

        private static double? GetCandidateUncertainty(YoloWorkerSmokeTestResult result, double? threshold)
            => result?.CaptureAnomalyScore is double score ? threshold.HasValue && double.IsFinite(score) && double.IsFinite(threshold.Value)
                ? -Math.Abs(score - threshold.Value) : null : LabelSuspicionReviewService.GetUncertainty(result?.Candidates, threshold);

        private static string GetFileIdentity(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return string.Empty;
                return HashingService.ComputeFileSha256(path, lowerCase: true);
            }
            catch (IOException) { return string.Empty; }
            catch (UnauthorizedAccessException) { return string.Empty; }
        }

        internal async Task ExecuteInferenceWorkspaceBatchCommandAsync(bool onlyUnexecuted)
        {
            if (context.IsApplicationCloseApproved?.Invoke() == true
                || context.BatchDetectionWorkflowService.IsRunning || context.ImageDetectionWorkflowService.IsDetecting) return;
            if (context.IsInferenceWorkspaceStage?.Invoke() != true)
            {
                if (!onlyUnexecuted) await ExecuteBatchDetectQueueCommandAsync().ConfigureAwait(true);
                return;
            }
            if (context.EnsureInferenceModeForDetection?.Invoke() != true) return;
            IReadOnlyList<WpfImageQueueItem> items = context.QueueItemsProvider()
                .Where(item => item != null && (!onlyUnexecuted || context.HasCompletedInferenceResult?.Invoke(item.ImagePath) != true)).ToArray();
            await RunBatchDetectionAsync(items, OpenVisionLanguageService.T(onlyUnexecuted ? "WpfInference.Target.Unexecuted" : "WpfInference.Target.All")).ConfigureAwait(true);
        }

        internal async Task ExecuteBatchDetectQueueCommandAsync()
        {
            if (context.EnsureInferenceModeForDetection?.Invoke() != true)
            {
                return;
            }

            WpfBatchDetectionPlan plan = context.ShowBatchDetectionPreflight?.Invoke(
                GetVisibleQueueItems(),
                OpenVisionLab.OpenVisionLanguageService.T("WpfBatch.Run.Scope.Visible"));
            if (plan != null)
            {
                await RunBatchDetectionAsync(plan.Items, plan.ScopeText).ConfigureAwait(true);
            }
        }

        internal void ExecuteRetryFailedQueueCommand()
        {
            _ = ExecuteRetryFailedQueueCommandAsync();
        }

        internal async Task ExecuteRetryFailedQueueCommandAsync()
        {
            if (context.EnsureInferenceModeForDetection?.Invoke() != true)
            {
                return;
            }

            WpfBatchDetectionPlan plan = context.ShowBatchDetectionPreflight?.Invoke(
                context.QueueItemsProvider()
                    .Where(item => item?.ReviewState == YoloImageReviewState.Failed)
                    .ToList(),
                OpenVisionLab.OpenVisionLanguageService.T("WpfBatch.Run.Scope.Retry"));
            if (plan != null)
            {
                await RunBatchDetectionAsync(plan.Items, plan.ScopeText).ConfigureAwait(true);
            }
        }

        internal void ExecuteStopBatchQueueCommand()
        {
            if (context.IsApplicationCloseApproved?.Invoke() == true)
            {
                return;
            }

            context.BatchDetectionWorkflowService.Cancel();
            context.AppendLog?.Invoke(OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.BatchDetectionWorkflowAdapter.StopRequested"));
        }

        internal void ApplyDetectionResultToQueueItem(
            WpfImageQueueItem item,
            YoloWorkerSmokeTestResult result,
            bool saveReviewStatus = true,
            bool refreshQueueView = true,
            bool updateQueueStatusText = true)
        {
            if (item == null
                || result == null
                || !context.ImageQualityReviewWorkflowService.CanReview(projectData))
            {
                return;
            }

            string imageName = Path.GetFileNameWithoutExtension(item.ImagePath);
            YoloImageReviewStatus status = result.Succeeded
                ? result.CandidateCount > 0
                    ? context.ImageQualityReviewWorkflowService.SetDetectionCandidates(item.ImagePath, imageName, result.CandidateCount)
                    : context.ImageQualityReviewWorkflowService.SetDetectionNoCandidates(item.ImagePath, imageName)
                : context.ImageQualityReviewWorkflowService.SetDetectionFailed(item.ImagePath, imageName, result.Summary);
            context.ApplyReviewStatusToItem?.Invoke(item, status);
            context.ApplyAnomalyClassificationToImage?.Invoke(item.ImagePath, imageName, result.Candidates, saveReviewStatus);
            if (saveReviewStatus)
            {
                context.ImageQualityReviewWorkflowService.SaveReviewStatus(projectData);
            }

            if (refreshQueueView)
            {
                context.RefreshQueueView?.Invoke();
            }

            if (updateQueueStatusText)
            {
                context.UpdateImageQueueStatusText?.Invoke();
            }
        }

        internal IReadOnlyList<WpfImageQueueItem> GetVisibleQueueItems()
            => context.VisibleQueueItemsProvider() ?? Array.Empty<WpfImageQueueItem>();

        internal void UpdateBatchDetectionControls(string scopeText = "", string currentFileName = "")
        {
            BatchDetectionControlState controlState = context.BatchDetectionProgressService.BuildControlState(
                context.BatchDetectionWorkflowService.IsRunning,
                context.BatchDetectionWorkflowService.TotalCount,
                context.BatchDetectionWorkflowService.CompletedCount,
                scopeText,
                currentFileName);
            context.ApplyBatchDetectionControls?.Invoke(controlState);
        }

        internal bool ShowBatchDetectionImage(WpfImageQueueItem item)
        {
            if (item == null
                || string.IsNullOrWhiteSpace(item.ImagePath)
                || !File.Exists(item.ImagePath))
            {
                return false;
            }

            context.SelectImageQueueItem?.Invoke(item.ImagePath);
            bool loaded = context.TryLoadBatchImage?.Invoke(item.ImagePath) == true;
            if (loaded)
            {
                context.UpdateSelectedQueueImageButton?.Invoke(item);
            }

            return loaded;
        }

        internal bool IsActiveImagePath(string imagePath)
        {
            return !string.IsNullOrWhiteSpace(imagePath)
                && string.Equals(context.ActiveImagePathProvider?.Invoke(), imagePath, StringComparison.OrdinalIgnoreCase);
        }

        internal bool ApplyBatchDetectionResultToCanvas(WpfImageQueueItem item, YoloWorkerSmokeTestResult result)
        {
            if (item == null || result == null)
            {
                return false;
            }

            if (!IsActiveImagePath(item.ImagePath) && !ShowBatchDetectionImage(item))
            {
                return false;
            }

            context.SelectImageQueueItem?.Invoke(item.ImagePath);
            if (result.Succeeded) ApplyBatchDetectionCandidates(result.Candidates, true);
            if (!result.Succeeded)
            {
                ShowBatchDetectionFailureResult(item, result);
            }
            else if (context.CandidateReviewState.PendingCandidates.Count == 0)
            {
                ShowBatchNoCandidateResult(item, result);
            }

            return true;
        }

        internal Task YieldBatchDetectionResultFrameAsync(CancellationToken token)
        {
            return context.YieldBatchDetectionResultFrameAsync(token);
        }

        internal void ShowBatchNoCandidateResult(WpfImageQueueItem item, YoloWorkerSmokeTestResult result)
        {
            WpfDetectionOverlayPresentation presentation = context.DetectionResultPresentationService.BuildNoCandidateOverlay(
                item?.ImagePath ?? result?.ImagePath ?? context.ActiveImagePathProvider?.Invoke() ?? string.Empty,
                context.CandidateConfidenceFilterProvider?.Invoke() ?? 0D);
            context.SetDetectionOverlay?.Invoke(presentation);
        }

        internal void ShowBatchDetectionFailureResult(WpfImageQueueItem item, YoloWorkerSmokeTestResult result)
        {
            WpfDetectionOverlayPresentation presentation = context.DetectionResultPresentationService.BuildFailureOverlay(
                item?.ImagePath ?? result?.ImagePath ?? context.ActiveImagePathProvider?.Invoke() ?? string.Empty,
                result?.Summary);
            context.SetDetectionOverlay?.Invoke(presentation);
        }

        internal void ApplyBatchDetectionCandidates(IReadOnlyList<YoloWorkerSmokeCandidate> candidates, bool succeeded)
        {
            int loadedCount = context.CandidateReviewState.LoadPendingCandidates(candidates, clearConfirmed: true);
            context.ClearCandidateReviewHistory?.Invoke();
            context.ApplyCanvasDisplayMode?.Invoke(WpfCanvasDisplayMode.InferenceOnly, false, false);
            context.RefreshCandidateList?.Invoke();
            context.RefreshObjectList?.Invoke();
            context.RedrawReviewRois?.Invoke();
            context.SetActiveImageDetectionStatus?.Invoke(loadedCount, succeeded);
            context.AddCandidateReviewHistory?.Invoke(
                context.DetectionResultPresentationService.BuildCandidateLoadHistory(
                    loadedCount,
                    succeeded,
                    context.CandidateConfidenceFilterProvider?.Invoke() ?? 0D));
            context.ShowCandidateReviewWorkflowView?.Invoke();
        }

        internal async Task RunBatchDetectionAsync(IReadOnlyList<WpfImageQueueItem> items, string scopeText)
        {
            if (!context.AnomalyImageReviewSession.CanReview(projectData)
                || !context.ImageQualityReviewWorkflowService.CanReview(projectData))
            {
                context.AppendLog?.Invoke(OpenVisionLab.OpenVisionLanguageService.T("WpfBatch.Run.LoadImages"));
                return;
            }

            if (context.BatchDetectionWorkflowService.IsRunning
                || context.ImageDetectionWorkflowService.IsDetecting)
            {
                context.AppendLog?.Invoke(OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.BatchDetectionWorkflowAdapter.AlreadyRunning"));
                return;
            }

            IReadOnlyList<WpfImageQueueItem> queue = context.DetectionTargetService.BuildBatchQueue(items);
            if (queue.Count == 0)
            {
                context.AppendLog?.Invoke(context.DetectionTargetService.BuildEmptyBatchMessage(scopeText));
                return;
            }

            if (queue.Any(item => !ReferenceEquals(context.FindQueueItem?.Invoke(item.ImagePath), item)))
            {
                context.AppendLog?.Invoke(OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.BatchDetectionWorkflowAdapter.QueueChanged"));
                return;
            }

            WpfModelRegistryHistoryItem selectedModel = context.SelectedInferenceModelProvider?.Invoke();
            bool isTrialModelRun = selectedModel != null && !selectedModel.IsCurrentInspectionModel;
            PythonModelSettings trialSettings = isTrialModelRun
                ? context.CreateTrialModelSettings?.Invoke(selectedModel)
                : null;
            if (isTrialModelRun && trialSettings == null)
            {
                context.AppendLog?.Invoke(OpenVisionLanguageService.T("WpfInference.Model.TrialUnavailable"));
                return;
            }

            BatchDetectionRun run = context.BatchDetectionWorkflowService.TryBegin(queue.Count, CaptureBatchReviewStatusSave());
            if (run == null)
            {
                return;
            }

            string modelSourceText = InferenceStatusPresentationService.BuildRuntimeModelLabel(
                trialSettings ?? projectData?.ProjectSettings?.PythonModel);
            if (isTrialModelRun)
            {
                modelSourceText = string.Format(
                    System.Globalization.CultureInfo.CurrentCulture,
                    OpenVisionLanguageService.T("WpfInference.Run.TrialStarted"),
                    Path.GetFileName(trialSettings.WeightsPath));
            }
            Dictionary<string, WpfImageQueueItem> queueByPath = queue.ToDictionary(
                item => item.ImagePath,
                StringComparer.OrdinalIgnoreCase);

            await context.BatchDetectionWorkflowService.ExecuteAsync(
                run,
                queue.Select(item => item.ImagePath).ToArray(),
                new BatchDetectionCallbacks
                {
                    PrepareAsync = async token =>
                    {
                        UpdateBatchDetectionControls(scopeText, string.Empty);
                        context.SetYoloCommandStatus?.Invoke(
                            context.BatchDetectionProgressService.BuildStartCommandStatus(queue.Count),
                            true);
                        context.SetGlobalInferenceStatus?.Invoke(
                            context.BatchDetectionProgressService.BuildStartInferenceStatus(queue.Count),
                            true,
                            false);
                        context.AppendLog?.Invoke(
                            context.BatchDetectionProgressService.BuildStartLog(scopeText, queue.Count, modelSourceText));
                        context.SetGlobalInferenceStatus?.Invoke(
                            isTrialModelRun
                                ? OpenVisionLanguageService.T("WpfInference.Run.TrialInProgress")
                                : context.BatchDetectionProgressService.BuildWorkerPreparingInferenceStatus(queue.Count),
                            true,
                            false);
                        context.SetPythonStatus?.Invoke(isTrialModelRun
                            ? OpenVisionLanguageService.T("WpfInference.Run.TrialInProgress")
                            : OpenVisionLab.OpenVisionLanguageService.T("WpfInference.Text.BatchDetectionWorkflowAdapter.BatchConnecting"));
                        if (isTrialModelRun)
                        {
                            return null;
                        }

                        bool ready = await context.EnsurePythonModelClientReadyAsync(
                            YoloRuntimePresentationService.GetWorkerConnectTimeoutMilliseconds(
                                projectData?.ProjectSettings?.PythonModel?.DetectionTimeoutSeconds ?? 30),
                            token).ConfigureAwait(true);
                        if (ready || !run.CanApplyResult)
                        {
                            return null;
                        }

                        string failure = YoloRuntimePresentationService.BuildPythonWorkerFailureText(
                            context.CommunicationStatusProvider(),
                            context.PythonWorkerLastErrorProvider());
                        context.AppendLog?.Invoke(string.Format(System.Globalization.CultureInfo.CurrentCulture, OpenVisionLab.OpenVisionLanguageService.T("WpfBatch.Run.StartFailed"), failure));
                        return failure;
                    },
                    DetectAsync = (path, token) => isTrialModelRun
                        ? context.RunTrialModelDetectionForImageAsync(
                            path,
                            trialSettings,
                            selectedModel.CandidateId,
                            token)
                        : context.RunWorkerDetectionAsync(path, token),
                    ItemStarting = path =>
                    {
                        WpfImageQueueItem item = queueByPath[path];
                        context.ApplyReviewStatusToItem?.Invoke(
                            item,
                            context.ImageQualityReviewWorkflowService.SetDetectionRequested(
                                path,
                                Path.GetFileNameWithoutExtension(path)));
                        ShowBatchDetectionImage(item);
                        context.SetGlobalInferenceStatus?.Invoke(
                            context.BatchDetectionProgressService.BuildItemInferenceStatus(
                                run.CompletedCount,
                                run.TotalCount,
                                path),
                            true,
                            false);
                        UpdateBatchDetectionControls(
                            scopeText,
                            context.BatchDetectionProgressService.ResolveImageFileName(path));
                    },
                    ApplyResult = (path, result, elapsed) =>
                    {
                        WpfImageQueueItem item = queueByPath[path];
                        ApplyDetectionResultToQueueItem(
                            item,
                            result,
                            saveReviewStatus: false,
                            refreshQueueView: false,
                            updateQueueStatusText: false);
                        bool displayed = run.CanApplyResult && ApplyBatchDetectionResultToCanvas(item, result);
                        string elapsedText = YoloRuntimePresentationService.FormatElapsed(elapsed);
                        if (result.Succeeded)
                        {
                            context.AppendLog?.Invoke(
                                context.BatchDetectionProgressService.BuildItemCompletedLog(
                                    run.CompletedCount + 1,
                                    run.TotalCount,
                                    path,
                                    result.CandidateCount,
                                    elapsedText,
                                    modelSourceText));
                        }
                        else if (run.CanApplyResult)
                        {
                            context.AppendLog?.Invoke(
                                context.BatchDetectionProgressService.BuildItemFailedLog(
                                    run.CompletedCount + 1,
                                    run.TotalCount,
                                    path,
                                    elapsedText,
                                    result.Summary,
                                    modelSourceText));
                        }

                        return displayed;
                    },
                    ItemCompleted = (path, elapsed) =>
                    {
                        string elapsedText = YoloRuntimePresentationService.FormatElapsed(elapsed);
                        context.SetPythonStatus?.Invoke(
                            context.BatchDetectionProgressService.BuildItemPythonStatus(
                                run.CompletedCount,
                                run.TotalCount,
                                elapsedText));
                        UpdateBatchDetectionControls(
                            scopeText,
                            context.BatchDetectionProgressService.BuildLatestFileStatus(path, elapsedText));
                    },
                    YieldResultFrameAsync = YieldBatchDetectionResultFrameAsync
                }).ConfigureAwait(true);

            if (context.IsApplicationCloseApproved?.Invoke() == true)
            {
                return;
            }

            PresentBatchDetectionCompletion(run, modelSourceText);
        }

        internal Action CaptureBatchReviewStatusSave()
        {
            LabelingProjectData batchData = projectData;
            Action saveQualityReviewStatus = context.ImageQualityReviewWorkflowService.CaptureReviewStatusSave(batchData);
            Action saveAnomalyReviewStatus = context.IsAnomalyDatasetPurpose?.Invoke() == true
                ? context.AnomalyImageReviewSession.CaptureReviewStatusSave(batchData)
                : null;
            return () =>
            {
                saveQualityReviewStatus();
                saveAnomalyReviewStatus?.Invoke();
            };
        }

        internal void PresentBatchDetectionCompletion(BatchDetectionRun run, string modelSourceText)
        {
            bool canceled = run.Token.IsCancellationRequested;
            if (run.HadException)
            {
                context.AppendLog?.Invoke(OpenVisionLab.OpenVisionLanguageService.T("WpfBatch.Run.ExceptionPrefix") + " " + run.FailureSummary);
            }

            context.RefreshQueueView?.Invoke();
            UpdateBatchDetectionControls(canceled ? OpenVisionLab.OpenVisionLanguageService.T("WpfBatch.Run.Completion.Stopped") : OpenVisionLab.OpenVisionLanguageService.T("WpfBatch.Run.Completion.Done"), string.Empty);
            context.SetPythonStatus?.Invoke(canceled ? OpenVisionLab.OpenVisionLanguageService.T("WpfBatch.Run.Status.Stopped") : OpenVisionLab.OpenVisionLanguageService.T("WpfBatch.Run.Status.Completed"));
            string totalElapsedText = YoloRuntimePresentationService.FormatElapsed(run.Elapsed);
            string averageElapsedText = YoloRuntimePresentationService.FormatAverageElapsed(run.Elapsed, run.CompletedCount);
            context.SetYoloCommandStatus?.Invoke(
                context.BatchDetectionProgressService.BuildCompletionCommandStatus(
                    canceled,
                    run.CompletedCount,
                    run.TotalCount,
                    totalElapsedText),
                false);
            context.SetGlobalInferenceStatus?.Invoke(
                context.BatchDetectionProgressService.BuildCompletionInferenceStatus(
                    canceled,
                    run.CompletedCount,
                    run.TotalCount,
                    totalElapsedText),
                false,
                canceled);
            context.AppendLog?.Invoke(
                context.BatchDetectionProgressService.BuildCompletionLog(
                    canceled,
                    run.CompletedCount,
                    run.TotalCount,
                    totalElapsedText,
                    averageElapsedText,
                    modelSourceText));
            if (!string.IsNullOrEmpty(run.FailureSummary))
            {
                UpdateBatchDetectionControls(OpenVisionLab.OpenVisionLanguageService.T("WpfBatch.Run.Completion.Failed"), string.Empty);
                context.SetPythonStatus?.Invoke(OpenVisionLab.OpenVisionLanguageService.T("WpfBatch.Run.Status.Failed"));
                context.SetYoloCommandStatus?.Invoke(
                    context.BatchDetectionProgressService.BuildFailureCommandStatus(
                        run.CompletedCount,
                        run.TotalCount,
                        run.FailureSummary),
                    false);
                context.SetGlobalInferenceStatus?.Invoke(
                    context.BatchDetectionProgressService.BuildFailureInferenceStatus(
                        run.CompletedCount,
                        run.TotalCount,
                        run.FailureSummary),
                    false,
                    true);
                context.AppendLog?.Invoke(
                    context.BatchDetectionProgressService.BuildFailureLog(
                        run.CompletedCount,
                        run.TotalCount,
                        run.FailureSummary));
            }
        }
    }

    internal sealed class BatchDetectionWorkflowAdapterContext
    {
        internal Func<InferenceResultReviewService> InferenceResultReviewServiceProvider { get; init; }
        internal Func<string> CandidateRecipeIdentityProvider { get; init; }
        internal Func<string> CandidateModelIdentityProvider { get; init; }
        internal Func<string> CandidateTrainingRunIdentityProvider { get; init; }
        internal Func<string> CandidateArtifactIdentityProvider { get; init; }
        internal Func<PythonModelSettings> CandidateModelSettingsProvider { get; init; }
        internal Func<InferenceResultReviewEntry, PythonModelSettings> CandidateSourceSettingsProvider { get; init; }
        internal Func<bool> CanGenerateCandidateBatch { get; init; }
        internal Func<bool> CandidateLabelingStageProvider { get; init; }
        internal Func<string> CandidateEvidenceOutputDirectoryProvider { get; init; }
        internal Func<bool> HasUnsavedCandidateEditorChanges { get; init; }
        internal Func<WpfImageQueuePanelViewModel> ImageQueueViewModelProvider { get; init; }
        internal Func<ICollectionView> QueueCollectionViewProvider { get; init; }
        internal Func<string, double?> CandidateSavedAnomalyThresholdProvider { get; init; }
        internal Func<string, IReadOnlyList<LabelSuspicionPrediction>> CandidatePredictionsProvider { get; init; }
        internal Action<YoloWorkerSmokeTestResult, double?> CandidateAnomalyRestored { get; init; }
        internal Action<CandidateBatchSummary> CandidateBatchCompleted { get; init; }
        internal Func<LabelingProjectData> DataProvider { get; init; }
        internal Func<string, bool> HasCompletedInferenceResult { get; init; }
        internal BatchDetectionWorkflowService BatchDetectionWorkflowService { get; init; }
        internal BatchDetectionProgressService BatchDetectionProgressService { get; init; }
        internal DetectionTargetService DetectionTargetService { get; init; }
        internal ImageDetectionWorkflowService ImageDetectionWorkflowService { get; init; }
        internal ImageQualityReviewWorkflowService ImageQualityReviewWorkflowService { get; init; }
        internal AnomalyImageReviewSession AnomalyImageReviewSession { get; init; }
        internal DetectionResultPresentationService DetectionResultPresentationService { get; init; }
        internal CandidateReviewStateService CandidateReviewState { get; init; }
        internal Func<IReadOnlyList<WpfImageQueueItem>> QueueItemsProvider { get; init; }
        internal Func<IReadOnlyList<WpfImageQueueItem>> VisibleQueueItemsProvider { get; init; }
        internal Func<WpfImageQueueItem> SelectedQueueItemProvider { get; init; }
        internal Func<WpfModelRegistryHistoryItem> SelectedInferenceModelProvider { get; init; }
        internal Func<string, WpfImageQueueItem> FindQueueItem { get; init; }
        internal Func<string> ActiveImagePathProvider { get; init; }
        internal Func<bool> IsApplicationCloseApproved { get; init; }
        internal Func<bool> EnsureInferenceModeForDetection { get; init; }
        internal Func<bool> IsInferenceWorkspaceStage { get; init; }
        internal Func<string, bool, Task> RunInteractiveDetectionAsync { get; init; }
        internal Func<string, PythonModelSettings, string, Task> RunTrialModelInteractiveDetectionAsync { get; init; }
        internal Func<WpfModelRegistryHistoryItem, PythonModelSettings> CreateTrialModelSettings { get; init; }
        internal Func<IReadOnlyList<WpfImageQueueItem>, string, WpfBatchDetectionPlan> ShowBatchDetectionPreflight { get; init; }
        internal Func<string, CancellationToken, Task<YoloWorkerSmokeTestResult>> RunWorkerDetectionAsync { get; init; }
        internal Func<string, PythonModelSettings, string, CancellationToken, Task<YoloWorkerSmokeTestResult>> RunTrialModelDetectionForImageAsync { get; init; }
        internal Func<CancellationToken, Task> YieldBatchDetectionResultFrameAsync { get; init; }
        internal Func<int, CancellationToken, Task<bool>> EnsurePythonModelClientReadyAsync { get; init; }
        internal Func<PythonCommunicationStatus> CommunicationStatusProvider { get; init; }
        internal Func<string> PythonWorkerLastErrorProvider { get; init; }
        internal Func<bool> IsAnomalyDatasetPurpose { get; init; }
        internal Action<string> AppendLog { get; init; }
        internal Action<WpfImageQueueItem, YoloImageReviewStatus> ApplyReviewStatusToItem { get; init; }
        internal Func<string, string, IReadOnlyList<YoloWorkerSmokeCandidate>, bool, bool> ApplyAnomalyClassificationToImage { get; init; }
        internal Action RefreshQueueView { get; init; }
        internal Action UpdateImageQueueStatusText { get; init; }
        internal Action<BatchDetectionControlState> ApplyBatchDetectionControls { get; init; }
        internal Action<string, bool> SetYoloCommandStatus { get; init; }
        internal Action<string, bool, bool> SetGlobalInferenceStatus { get; init; }
        internal Action<string> SetPythonStatus { get; init; }
        internal Action<string> SelectImageQueueItem { get; init; }
        internal Func<string, bool> TryLoadBatchImage { get; init; }
        internal Action<WpfImageQueueItem> UpdateSelectedQueueImageButton { get; init; }
        internal Action RefreshCandidateList { get; init; }
        internal Action RefreshObjectList { get; init; }
        internal Action<string, PythonModelSettings> BeginInferenceReference { get; init; }
        internal Action<string> FinishInferenceReference { get; init; }
        internal Action<YoloWorkerSmokeTestResult> AcceptInferenceReference { get; init; }
        internal Action RedrawReviewRois { get; init; }
        internal Action<int, bool> SetActiveImageDetectionStatus { get; init; }
        internal Action<string> AddCandidateReviewHistory { get; init; }
        internal Action ShowCandidateReviewWorkflowView { get; init; }
        internal Action ClearCandidateReviewHistory { get; init; }
        internal Action<WpfCanvasDisplayMode, bool, bool> ApplyCanvasDisplayMode { get; init; }
        internal Action<WpfDetectionOverlayPresentation> SetDetectionOverlay { get; init; }
        internal Func<double> CandidateConfidenceFilterProvider { get; init; }
    }

    public sealed record CandidateBatchSummary(int ProcessedImages, int ImagesWithCandidates, int EmptyImages, int FailedImages, bool Canceled, string FailureSummary);
}
