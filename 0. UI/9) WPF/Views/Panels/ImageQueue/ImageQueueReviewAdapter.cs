using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;
using OpenVisionLab.ImageCanvas.ViewModels;
using System.Drawing;

namespace MvcVisionSystem
{
    // Responsibility group: image queue review status and presentation.
    // These members remain WPF Window adapters; independent policy belongs in services.
    internal sealed class ImageQueueReviewAdapter
    {
        private readonly ImageQueueReviewAdapterContext context;
        private bool isReviewCommandRunning;
        private AnomalyClassificationDecision anomalyPrediction;
        private string anomalyPredictionImagePath;
        private LabelingProjectData anomalyPredictionData;
        private LabelSuspicionReport activeSuspicionReport;
        private string activeSuspicionPath = string.Empty;
        private string suspicionScope = string.Empty;
        private LabelingProjectData suspicionData;
        private string dismissalUndoPath = string.Empty;
        private string dismissalUndoIdentity = string.Empty;
        private string dismissalUndoPreviousIdentity = string.Empty;
        private readonly LabelSuspicionReviewService labelSuspicionReview = new();

        private LabelingProjectData projectData => context.DataProvider?.Invoke();
        private ImageQualityReviewWorkflowService imageQualityReviewWorkflowService => context.ImageQualityReviewWorkflowService;
        private AnomalyImageReviewSession anomalyImageReviewSession => context.AnomalyImageReviewSession;
        private ImageQueueSelectionService imageQueueSelectionService => context.ImageQueueSelectionService;
        private IList<WpfImageQueueItem> imageQueueItems => context.ImageQueueItems ?? Array.Empty<WpfImageQueueItem>();
        private IReadOnlyDictionary<string, WpfImageQueueItem> imageQueueItemsByPath => context.ImageQueueItemsByPath;
        private ICollectionView imageQueueView => context.ImageQueueViewProvider?.Invoke();
        private WpfImageQueuePanelViewModel ImageQueueViewModel => context.ImageQueueViewModel;
        private WpfObjectReviewPanelViewModel ObjectReviewViewModel => context.ObjectReviewViewModel;
        private ComboBox ImageQueueFilterBox => context.ImageQueueFilterBox;
        private TextBox ImageQueueSearchBox => context.ImageQueueSearchBox;
        private DataGrid ImageQueueGrid => context.ImageQueueGrid;
        private WpfImageQueuePanel ImageQueuePanelControl => context.ImageQueuePanelControl;
        private AnnotationDirtyState annotationDirtyState => context.AnnotationDirtyState;
        private IReadOnlyList<YoloWorkerSmokeCandidate> pendingDetectionCandidates
            => context.PendingDetectionCandidatesProvider?.Invoke() ?? Array.Empty<YoloWorkerSmokeCandidate>();
        private string activeImagePath => context.ActiveImagePathProvider?.Invoke() ?? string.Empty;
        private string activeReviewImagePath => FindImageQueueItem(activeImagePath)?.ImagePath ?? activeImagePath;
        private System.Drawing.Size activeImageSize => context.ActiveImageSizeProvider?.Invoke() ?? System.Drawing.Size.Empty;
        private bool isApplicationCloseApproved => context.IsApplicationCloseApproved?.Invoke() == true;
        private bool suppressImageQueueSelection
        {
            get => context.IsImageQueueSelectionSuppressed?.Invoke() == true;
            set => context.SetImageQueueSelectionSuppressed?.Invoke(value);
        }
        private System.Windows.Threading.Dispatcher Dispatcher => context.Dispatcher;

        internal ImageQueueReviewAdapter(ImageQueueReviewAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            ArgumentNullException.ThrowIfNull(context.DataProvider);
        }

        private void AppendLog(string message) => context.AppendLog?.Invoke(message);
        private void SetDatasetStatus(string message) => context.SetDatasetStatus?.Invoke(message);
        private void SetModelStatus(string message) => context.SetModelStatus?.Invoke(message);
        private void RefreshImageQueueViewAfterItemStateChange() => context.RefreshImageQueueViewAfterItemStateChange?.Invoke();
        private bool TryOpenNextIncompleteQueueImage() => context.TryOpenNextIncompleteQueueImage?.Invoke() == true;

        internal void ConfigureLabelSuspicionReview()
        {
            context.CandidateReviewViewModel?.ConfigureLabelSuspicionReview(FixLabelSuspicion, DismissLabelSuspicion, UndoLabelSuspicionDismissal,
                CanEditLabelSuspicion, CanUndoLabelSuspicionDismissal, _ => RefreshLabelSuspicionReview());
        }

        internal void ResetLabelSuspicionReview()
        {
            activeSuspicionReport = null;
            activeSuspicionPath = string.Empty;
            suspicionData = null;
            suspicionScope = string.Empty;
            dismissalUndoPath = dismissalUndoIdentity = dismissalUndoPreviousIdentity = string.Empty;
            context.CandidateReviewViewModel?.SetLabelSuspicionReview(null);
        }

        private bool IsCurrentSuspicionScope()
        {
            string current = context.ReviewScopeIdentityProvider?.Invoke() ?? string.Empty;
            if (isApplicationCloseApproved || string.IsNullOrWhiteSpace(current) || context.ResultCache?.ScopeIdentity != current) return false;
            if (!ReferenceEquals(suspicionData, projectData) || suspicionScope != current)
            {
                suspicionData = projectData;
                suspicionScope = current;
                activeSuspicionReport = null;
                activeSuspicionPath = string.Empty;
                dismissalUndoPath = dismissalUndoIdentity = dismissalUndoPreviousIdentity = string.Empty;
                context.CandidateReviewViewModel?.SetLabelSuspicionReview(null);
            }
            return !isApplicationCloseApproved && imageQualityReviewWorkflowService.CanReview(projectData);
        }

        internal bool CanEditLabelSuspicion() => IsCurrentSuspicionScope() && !isReviewCommandRunning && context.IsLabelingStageActive?.Invoke() == true
            && context.IsOperationRunning?.Invoke() != true && context.HasWorkflowDraft?.Invoke() != true
            && context.IsAnnotationLoadBlocked?.Invoke() != true && !string.IsNullOrEmpty(activeImagePath) && !activeImageSize.IsEmpty;

        internal bool CanOpenLabelSuspicionReview() => IsCurrentSuspicionScope() && !isReviewCommandRunning
            && context.IsOperationRunning?.Invoke() != true && context.HasWorkflowDraft?.Invoke() != true;

        internal IReadOnlyList<LabelSuspicionPrediction> GetCurrentLabelSuspicionPredictions(string imagePath)
            => GetCurrentLabelSuspicionPredictionsCore(imagePath, null, null);

        private IReadOnlyList<LabelSuspicionPrediction> GetCurrentLabelSuspicionPredictionsCore(string imagePath,
            Dictionary<(string Path, string Hash), bool> artifactChecks, IReadOnlyList<RecipeDatasetVersionSnapshot> history)
        {
            if (!IsCurrentSuspicionScope() || string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath)) return Array.Empty<LabelSuspicionPrediction>();
            try
            {
                string imageIdentity = HashingService.ComputeFileSha256(imagePath, lowerCase: true);
                ModelRegistrySettings registry = projectData.ProjectSettings?.ModelRegistry;
                if (registry?.Candidates == null || registry.TrainingRuns == null) return Array.Empty<LabelSuspicionPrediction>();
                ModelCandidate inspection = registry.Candidates.FirstOrDefault(candidate => candidate?.CandidateId == registry.CurrentInspectionModelId);
                history ??= RecipeDatasetVersionService.LoadHistory(context.CurrentRecipeDirectoryProvider?.Invoke());
                var predictions = new List<LabelSuspicionPrediction>();
                bool anomaly = IsAnomalyDatasetPurpose();
                foreach (InferenceResultReviewEntry entry in context.ResultCache.FindModelResults(imagePath, imageIdentity))
                {
                    YoloWorkerSmokeTestResult result = entry.Result;
                    if (entry.Failure != null || result?.Source == null || !result.HasCompletePredictions || string.IsNullOrWhiteSpace(entry.TrainingRunIdentity)
                        || result.CaptureTrainingRunId != entry.TrainingRunIdentity || !string.Equals(result.CaptureImageSha256, imageIdentity, StringComparison.OrdinalIgnoreCase)
                        || !string.Equals(result.CaptureWeightsSha256, entry.ArtifactIdentity, StringComparison.OrdinalIgnoreCase)
                        || !anomaly && (result.CaptureConfidenceFloor is not double floor || !double.IsFinite(floor) || floor > .25)
                        || !VerifyArtifact(result.Source.WeightsPath, entry.ArtifactIdentity)) continue;
                    ModelCandidate candidate = registry.Candidates.Where(item => item != null && item.TrainingRunId == entry.TrainingRunIdentity
                        && string.Equals(item.WeightsSha256, entry.ArtifactIdentity, StringComparison.OrdinalIgnoreCase)
                        && (string.IsNullOrEmpty(result.Source.CandidateId) || item.CandidateId == result.Source.CandidateId))
                        .OrderByDescending(item => item.CandidateId == inspection?.CandidateId).FirstOrDefault();
                    TrainingRun run = registry.TrainingRuns.FirstOrDefault(item => item?.TrainingRunId == entry.TrainingRunIdentity);
                    ModelProfile profile = registry.Profiles?.FirstOrDefault(item => item?.ProfileId == candidate?.ProfileId);
                    if (candidate == null || run == null || profile == null || run.ProfileId != candidate.ProfileId
                        || !string.Equals(run.WeightsSha256, entry.ArtifactIdentity, StringComparison.OrdinalIgnoreCase)
                        || !string.Equals(PythonModelSettings.NormalizeModelEngine(profile.ModelEngine), PythonModelSettings.NormalizeModelEngine(result.Source.ModelEngine), StringComparison.OrdinalIgnoreCase)
                        || profile.DatasetPurpose != projectData.ProjectSettings.DatasetPurpose.ToString()
                        || !VerifyArtifact(string.IsNullOrEmpty(candidate.ArtifactPath) ? candidate.WeightsPath : candidate.ArtifactPath, candidate.WeightsSha256)) continue;
                    if (result.Candidates == null || !anomaly && (projectData.ClassNamedList == null || result.Candidates.Any(item => item == null || !item.ClassId.HasValue || item.ClassId.Value < 0
                        || item.ClassId.Value >= projectData.ClassNamedList.Count || projectData.ClassNamedList[item.ClassId.Value]?.Text != item.ClassName))) continue;
                    RecipeDatasetVersionSnapshot training = history.FirstOrDefault(item => item.DatasetVersionId == run.DatasetVersionId
                        && string.Equals(item.ContentSha256, run.DatasetContentSha256, StringComparison.OrdinalIgnoreCase));
                    RecipeDatasetVersionFile[] samePixels = training?.Files?.Where(file => file?.Kind == "image" && string.Equals(file.Sha256, imageIdentity, StringComparison.OrdinalIgnoreCase)).ToArray();
                    bool fullDatasetInput = run.Parameters?.OnlyReviewed == false && string.Equals(run.Parameters.InputDatasetContentSha256, run.DatasetContentSha256, StringComparison.OrdinalIgnoreCase);
                    PredictionTrainingMembership membership = training == null || !fullDatasetInput ? PredictionTrainingMembership.Unknown
                        : samePixels?.Any(file => file.Split == "train") == true ? PredictionTrainingMembership.Training : PredictionTrainingMembership.NotTraining;
                    string settings = Newtonsoft.Json.JsonConvert.SerializeObject(new { result.Source.ModelEngine, result.Source.ImageSize, result.Source.Confidence,
                        result.Source.MaximumCandidates, result.Source.ProjectRootPath, result.CaptureConfidenceFloor, result.CaptureCandidateLimit });
                    predictions.Add(new(entry.ArtifactIdentity.ToLowerInvariant(), candidate.CandidateId == inspection?.CandidateId, result.Candidates, membership,
                        anomaly ? context.SavedAnomalyThresholdProvider?.Invoke(entry.ArtifactIdentity, entry.TrainingRunIdentity) : null,
                        entry.TrainingRunIdentity, imageIdentity, HashingService.ComputeUtf8TextSha256(settings, lowerCase: true)));
                }
                return predictions;

                bool VerifyArtifact(string path, string hash)
                {
                    if (artifactChecks == null) return ModelArtifactStoreService.Verify(path, hash);
                    var key = (path ?? string.Empty, (hash ?? string.Empty).ToLowerInvariant());
                    if (!artifactChecks.TryGetValue(key, out bool verified)) artifactChecks[key] = verified = ModelArtifactStoreService.Verify(path, hash);
                    return verified;
                }
            }
            catch (Exception error) when (IsReviewStorageException(error) || error is ArgumentException)
            {
                AppendLog("Label suspicion evidence unavailable: " + error.Message);
                return Array.Empty<LabelSuspicionPrediction>();
            }
        }

        private LabelSuspicionReport EvaluateSavedLabelSuspicion(string path, Size size, IReadOnlyList<LabelSuspicionPrediction> predictions,
            Dictionary<(string Path, string Hash), bool> artifactChecks = null)
        {
            LabelSuspicionGroundTruth truth = imageQualityReviewWorkflowService.ReadLabelSuspicionGroundTruth(path, size, projectData);
            ModelRegistrySettings registry = projectData.ProjectSettings?.ModelRegistry;
            ModelCandidate inspection = registry?.Candidates?.FirstOrDefault(item => item?.CandidateId == registry.CurrentInspectionModelId);
            TrainingRun inspectionRun = registry?.TrainingRuns?.FirstOrDefault(item => item?.TrainingRunId == inspection?.TrainingRunId);
            LabelSuspicionModelIdentity inspectionIdentity = inspection != null && inspectionRun != null
                && string.Equals(inspectionRun.WeightsSha256, inspection.WeightsSha256, StringComparison.OrdinalIgnoreCase)
                && VerifyInspection()
                ? new(inspection.TrainingRunId, inspection.WeightsSha256.ToLowerInvariant()) : null;
            string imageIdentity = File.Exists(path) ? HashingService.ComputeFileSha256(path, lowerCase: true) : string.Empty;
            return labelSuspicionReview.EvaluateVerified(truth, imageIdentity, inspectionIdentity,
                predictions.Select(model => new LabelSuspicionModelIdentity(model.RunIdentity, model.ArtifactIdentity)).ToArray(), predictions, IsAnomalyDatasetPurpose());

            bool VerifyInspection()
            {
                string artifact = string.IsNullOrEmpty(inspection.ArtifactPath) ? inspection.WeightsPath : inspection.ArtifactPath;
                var key = (artifact ?? string.Empty, (inspection.WeightsSha256 ?? string.Empty).ToLowerInvariant());
                if (artifactChecks != null && artifactChecks.TryGetValue(key, out bool verified)) return verified;
                bool actual = ModelArtifactStoreService.Verify(artifact, inspection.WeightsSha256);
                if (artifactChecks != null) artifactChecks[key] = actual;
                return actual;
            }
        }

        internal void RefreshLabelSuspicionReview(bool redraw = true)
        {
            if (!IsCurrentSuspicionScope() || string.IsNullOrEmpty(activeImagePath) || activeImageSize.IsEmpty)
            {
                context.CandidateReviewViewModel?.SetLabelSuspicionReview(null);
                activeSuspicionReport = null;
                if (redraw) context.RedrawReviewOverlays?.Invoke();
                return;
            }
            try
            {
                IReadOnlyList<LabelSuspicionPrediction> predictions = GetCurrentLabelSuspicionPredictions(activeImagePath);
                LabelSuspicionReport saved = EvaluateSavedLabelSuspicion(activeImagePath, activeImageSize, predictions);
                activeSuspicionReport = saved;
                activeSuspicionPath = activeImagePath;
                bool dismissed = saved.CanEvaluate && imageQualityReviewWorkflowService.GetOrCreate(activeReviewImagePath)?.IsLabelSuspicionDismissed(saved.EvidenceIdentity) == true;
                WpfImageQueueItem row = FindImageQueueItem(activeImagePath);
                if (row != null)
                {
                    row.ModelDisagreement = LabelSuspicionReviewService.GetDisagreement(predictions);
                    row.LabelSuspicionCount = dismissed ? 0 : saved.Items.Count;
                    row.IsLabelSuspicionEvaluated = saved.CanEvaluate;
                    row.LabelSuspicionUnavailableReason = saved.UnavailableReason;
                }
                bool preview = annotationDirtyState?.IsDirty == true && saved.CanEvaluate && !IsAnomalyDatasetPurpose();
                if (preview)
                {
                    activeSuspicionReport = context.CaptureLabelSuspicionPreview == null
                        ? new(Array.Empty<LabelSuspicionItem>(), "EditorPreviewUnavailable", saved.HasTrainingImageWarning, saved.HasUnknownTrainingWarning)
                        : labelSuspicionReview.Evaluate(context.CaptureLabelSuspicionPreview(), true, predictions, anomaly: false) with { EvidenceIdentity = saved.EvidenceIdentity };
                    dismissed = false;
                    if (row != null)
                    {
                        row.LabelSuspicionCount = activeSuspicionReport.Items.Count;
                        row.IsLabelSuspicionEvaluated = activeSuspicionReport.CanEvaluate;
                        row.LabelSuspicionUnavailableReason = activeSuspicionReport.UnavailableReason;
                    }
                }
                context.CandidateReviewViewModel?.SetLabelSuspicionReview(activeSuspicionReport, dismissed, preview);
                RefreshLabelSuspicionQueueCounts();
                if (redraw) context.RedrawReviewOverlays?.Invoke();
            }
            catch (Exception error) when (IsReviewStorageException(error) || error is ArgumentException)
            {
                activeSuspicionReport = new(Array.Empty<LabelSuspicionItem>(), "SavedLabelsMalformed", false, false) { Diagnostic = error.Message };
                context.CandidateReviewViewModel?.SetLabelSuspicionReview(activeSuspicionReport);
                if (redraw) context.RedrawReviewOverlays?.Invoke();
            }
        }

        internal void RefreshAllLabelSuspicions()
        {
            if (!IsCurrentSuspicionScope()) { ResetLabelSuspicionReview(); return; }
            var artifactChecks = new Dictionary<(string Path, string Hash), bool>();
            IReadOnlyList<RecipeDatasetVersionSnapshot> history;
            try { history = RecipeDatasetVersionService.LoadHistory(context.CurrentRecipeDirectoryProvider?.Invoke()); }
            catch (Exception error) when (IsReviewStorageException(error))
            {
                AppendLog("Training image membership unavailable: " + error.Message);
                history = Array.Empty<RecipeDatasetVersionSnapshot>();
            }
            foreach (WpfImageQueueItem row in imageQueueItems)
            {
                try
                {
                    Size size;
                    if (string.Equals(row.ImagePath, activeImagePath, StringComparison.OrdinalIgnoreCase)) size = activeImageSize;
                    else { using System.Drawing.Image image = System.Drawing.Image.FromFile(row.ImagePath); size = image.Size; }
                    IReadOnlyList<LabelSuspicionPrediction> predictions = GetCurrentLabelSuspicionPredictionsCore(row.ImagePath, artifactChecks, history);
                    LabelSuspicionReport report = EvaluateSavedLabelSuspicion(row.ImagePath, size, predictions, artifactChecks);
                    row.ModelDisagreement = LabelSuspicionReviewService.GetDisagreement(predictions);
                    row.LabelSuspicionCount = imageQualityReviewWorkflowService.GetOrCreate(row.ImagePath)?.IsLabelSuspicionDismissed(report.EvidenceIdentity) == true ? 0 : report.Items.Count;
                    row.IsLabelSuspicionEvaluated = report.CanEvaluate;
                    row.LabelSuspicionUnavailableReason = report.UnavailableReason;
                }
                catch (Exception error) when (IsReviewStorageException(error) || error is ArgumentException || error is OutOfMemoryException)
                {
                    row.LabelSuspicionCount = 0;
                    row.IsLabelSuspicionEvaluated = false;
                    row.LabelSuspicionUnavailableReason = "ImageIdentityUnavailable";
                }
            }
            if (artifactChecks.Any(check => check.Value && !ModelArtifactStoreService.Verify(check.Key.Path, check.Key.Hash)))
                foreach (WpfImageQueueItem row in imageQueueItems)
                {
                    row.LabelSuspicionCount = 0; row.IsLabelSuspicionEvaluated = false;
                    row.LabelSuspicionUnavailableReason = "PredictionProvenanceMismatch"; row.ModelDisagreement = null;
                }
            RefreshLabelSuspicionQueueCounts();
            RefreshLabelSuspicionReview();
            RefreshImageQueueViewAfterItemStateChange();
        }

        private void RefreshLabelSuspicionQueueCounts() => ImageQueueViewModel?.SetCandidateFilterCounts(
            imageQueueItems.Count(item => item.SessionCandidateCount > 0 || item.ReviewState == YoloImageReviewState.Candidate),
            imageQueueItems.Count(item => item.LabelSuspicionCount > 0));

        internal void OpenLabelSuspicionReview()
        {
            if (!CanOpenLabelSuspicionReview()) return;
            context.ShowLabelSuspicionPanel?.Invoke();
            RefreshAllLabelSuspicions();
            RefreshReviewModeCommandState();
            ObjectReviewViewModel?.SetReviewMode(true);
            context.SelectReviewTool?.Invoke();
            SetImageQueueFilter(WpfImageQueueFilter.LabelSuspicion);
            WpfImageQueueItem first = imageQueueItems.FirstOrDefault(item => item.LabelSuspicionCount > 0)
                ?? imageQueueItems.FirstOrDefault(ImageQueueFilterService.HasSavedGroundTruth);
            if (first != null && annotationDirtyState?.IsDirty != true && !string.Equals(first.ImagePath, activeImagePath, StringComparison.OrdinalIgnoreCase))
                context.OpenSelectedQueueImage?.Invoke(first, true);
        }

        private void FixLabelSuspicion(LabelSuspicionItem item)
        {
            if (!CanEditLabelSuspicion()) return;
            RefreshLabelSuspicionReview(redraw: false);
            if (activeSuspicionReport?.CanEvaluate != true || !activeSuspicionReport.Items.Contains(item)) return;
            if (context.ApplyLabelSuspicionFix?.Invoke(item) == true) RefreshLabelSuspicionReview();
            else ReportReviewGuidance(T("WpfLabelSuspicion.EditUnavailable"));
        }

        internal bool PreviewAnomalySuspicionVerdict(YoloWorkerSmokeCandidate candidate)
        {
            if (!CanEditLabelSuspicion() || !IsAnomalyDatasetPurpose() || candidate?.ImageLevel != true) return false;
            LabelSuspicionPrediction prediction = GetCurrentLabelSuspicionPredictions(activeImagePath).FirstOrDefault(model => model.IsInspectionModel
                && model.SavedAnomalyThreshold.HasValue && model.Candidates.Contains(candidate));
            if (prediction == null) return false;
            InferenceResultReviewEntry entry = context.ResultCache.FindModelResults(activeImagePath, prediction.ImageIdentity).FirstOrDefault(item => item.TrainingRunIdentity == prediction.RunIdentity
                && string.Equals(item.ArtifactIdentity, prediction.ArtifactIdentity, StringComparison.OrdinalIgnoreCase) && item.Result.Candidates.Contains(candidate));
            if (entry == null) return false;
            ImageQueueViewModel?.SetCandidateAnomalyPrediction(entry.Result, prediction.SavedAnomalyThreshold);
            context.ShowLabelSuspicionPanel?.Invoke();
            return true;
        }

        internal string LabelSuspicionRejectionReasonKey => activeSuspicionReport?.CanEvaluate == true && activeSuspicionPath == activeImagePath
            ? activeSuspicionReport.Items.FirstOrDefault()?.Kind switch
            {
                LabelSuspicionKind.Position => "Position", LabelSuspicionKind.Class => "Class", LabelSuspicionKind.Unnecessary => "Extra",
                LabelSuspicionKind.AnomalyVerdict => "Verdict", _ => "Missing"
            } : IsAnomalyDatasetPurpose() ? "Verdict" : "Missing";

        private void DismissLabelSuspicion()
        {
            if (!CanEditLabelSuspicion() || annotationDirtyState?.IsDirty == true) return;
            RefreshLabelSuspicionReview(redraw: false);
            if (activeSuspicionReport?.CanEvaluate != true || activeSuspicionReport.Items.Count == 0) return;
            string identity = activeSuspicionReport.EvidenceIdentity;
            string previous = imageQualityReviewWorkflowService.GetOrCreate(activeReviewImagePath)?.LabelSuspicionDismissalIdentity ?? string.Empty;
            try
            {
                imageQualityReviewWorkflowService.SaveLabelSuspicionDismissal(activeReviewImagePath, identity, projectData);
                dismissalUndoPath = activeReviewImagePath; dismissalUndoIdentity = identity; dismissalUndoPreviousIdentity = previous;
                RefreshLabelSuspicionReview();
                ReportReviewGuidance(context.CandidateReviewViewModel?.LabelSuspicionStateText ?? T("WpfLabelSuspicion.Title"));
            }
            catch (Exception error) when (IsReviewStorageException(error)) { ReportReviewGuidance(T("WpfLabelSuspicion.SaveFailed") + " " + error.Message); }
        }

        private bool CanUndoLabelSuspicionDismissal() => activeSuspicionReport?.CanEvaluate == true && annotationDirtyState?.IsDirty != true
            && imageQualityReviewWorkflowService.GetOrCreate(activeReviewImagePath)?.IsLabelSuspicionDismissed(activeSuspicionReport.EvidenceIdentity) == true;

        internal void UndoLabelSuspicionDismissal()
        {
            if (!CanEditLabelSuspicion()) return;
            RefreshLabelSuspicionReview(redraw: false);
            if (!CanUndoLabelSuspicionDismissal()) return;
            string previous = dismissalUndoPath == activeReviewImagePath && dismissalUndoIdentity == activeSuspicionReport.EvidenceIdentity ? dismissalUndoPreviousIdentity : string.Empty;
            try
            {
                imageQualityReviewWorkflowService.SaveLabelSuspicionDismissal(activeReviewImagePath, previous, projectData);
                dismissalUndoPath = dismissalUndoIdentity = dismissalUndoPreviousIdentity = string.Empty;
                RefreshLabelSuspicionReview();
                ReportReviewGuidance(context.CandidateReviewViewModel?.LabelSuspicionStateText ?? T("WpfLabelSuspicion.Title"));
            }
            catch (Exception error) when (IsReviewStorageException(error)) { ReportReviewGuidance(T("WpfLabelSuspicion.SaveFailed") + " " + error.Message); }
        }

        internal IReadOnlyList<RoiImageCanvasDetectionOverlay> GetLabelSuspicionOverlays()
        {
            if (isApplicationCloseApproved || context.IsLabelingStageActive?.Invoke() != true || context.CandidateReviewViewModel?.IsSuspicionOverlayVisible != true
                || !string.Equals(activeSuspicionPath, activeImagePath, StringComparison.OrdinalIgnoreCase) || !ReferenceEquals(suspicionData, projectData)
                || suspicionScope != context.ReviewScopeIdentityProvider?.Invoke() || activeSuspicionReport?.CanEvaluate != true
                || annotationDirtyState?.IsDirty != true && imageQualityReviewWorkflowService.GetOrCreate(activeReviewImagePath)?.IsLabelSuspicionDismissed(activeSuspicionReport.EvidenceIdentity) == true)
                return Array.Empty<RoiImageCanvasDetectionOverlay>();
            return activeSuspicionReport.Items.Select(item =>
            {
                Rectangle bounds = item.Kind == LabelSuspicionKind.AnomalyVerdict ? new(Point.Empty, activeImageSize)
                    : item.Prediction != null ? CandidateReviewPresentationService.ClipCandidateBounds(item.Prediction, activeImageSize)
                    : item.Label != null ? new((int)item.Label.X, (int)item.Label.Y, (int)item.Label.Width, (int)item.Label.Height) : Rectangle.Empty;
                return new RoiImageCanvasDetectionOverlay { Bounds = bounds, Color = Color.FromArgb(255, 210, 77), IsLabelSuspicion = true,
                    // Native GL text is byte-based; localized reasons remain in the review panel.
                    Label = "!",
                    ContourPoints = item.Prediction != null ? CandidateReviewPresentationService.GetCandidateContourPoints(item.Prediction, activeImageSize) : Array.Empty<PointF>() };
            }).Where(overlay => !overlay.Bounds.IsEmpty).ToArray();
        }
        internal void ApplyReviewStatusToItem(WpfImageQueueItem item, YoloImageReviewStatus status)
            => ApplyReviewStatusToItemCore(item, status, refreshTrainingStepCompletion: true);

        internal void ApplyReviewStatusToItemCore(
            WpfImageQueueItem item,
            YoloImageReviewStatus status,
            bool refreshTrainingStepCompletion)
        {
            if (item == null || status == null)
            {
                return;
            }

            if (IsAnomalyDatasetPurpose())
            {
                WpfImageQueuePresenter.ApplyAnomalyReviewStatusToItem(item, anomalyImageReviewSession.GetStatus(item.ImagePath, true), status);
            }
            else
            {
                item.LabelStatus = WpfImageQueuePresenter.FormatLabelStatus(status.LabelText);
                item.IsAnomalyReviewItem = false;
                item.AnomalyReviewState = AnomalyImageReviewState.Unreviewed;
                item.DetectStatus = WpfImageQueuePresenter.FormatDetectionStatus(status);
                item.IsLabeled = status.IsLabeled;
                item.HasSavedLabelFile = status.LabelStatus?.HasLabelFile == true;
                item.IsSaveRequired = false;
                item.ReviewState = status.ReviewState;
                item.QualityReviewState = status.QualityReviewState;
                item.QueueIconKind = WpfImageQueuePresenter.GetIconKind(status);
                item.QueueIconBrush = WpfImageQueuePresenter.GetIconBrush(status);
                item.QueueBadgeBackgroundBrush = WpfImageQueuePresenter.GetBadgeBackgroundBrush(status);
                item.QueueRowAccentBrush = WpfImageQueuePresenter.GetRowAccentBrush(status);
                item.QueueBadgeText = WpfImageQueuePresenter.BuildBadgeText(status);
                item.QueueStatusSummary = WpfImageQueuePresenter.BuildStatusSummary(status);
                item.Detail = WpfImageQueuePresenter.BuildDetailText(status);
            }
            if (IsActiveImageQueueSaveRequired(item))
            {
                WpfImageQueuePresenter.ApplySaveRequiredStatusToItem(item, annotationDirtyState.Reason);
            }

            RefreshActiveImageQualityReviewPresentation(item, status);

            if (refreshTrainingStepCompletion)
            {
                context.RefreshYoloTrainingStepCompletion?.Invoke();
            }
        }

        #region ImageQueuePresentation
        // Queue presentation helpers stay isolated from queue loading so status-text changes do not hide data-flow changes.
        internal void SelectImageQueueItem(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return;
            }

            WpfImageQueueItem item = FindImageQueueItem(imagePath);
            if (item == null)
            {
                return;
            }

            suppressImageQueueSelection = true;
            try
            {
                ImageQueueGrid.SelectedItem = item;
                if (ImageQueueGrid.Columns.Count > 0)
                {
                    ImageQueueGrid.CurrentCell = new DataGridCellInfo(item, ImageQueueGrid.Columns[0]);
                }

                ImageQueueGrid.ScrollIntoView(item);
            }
            finally
            {
                suppressImageQueueSelection = false;
            }

            UpdateSelectedQueueImageButton(item);
        }

        internal WpfImageQueueItem FindImageQueueItem(string imagePath)
        {
            if (!string.IsNullOrWhiteSpace(imagePath)
                && imageQueueItemsByPath.TryGetValue(imagePath, out WpfImageQueueItem indexedItem))
            {
                return indexedItem;
            }

            WpfImageQueueItem exactItem = imageQueueSelectionService.FindItem(imageQueueItems, imagePath);
            if (exactItem != null)
            {
                return exactItem;
            }

            // The active image can be reopened through a resolved dataset path
            // while a catalog row still carries the source path. The image stem
            // is the durable queue identity in that case; keep save/review state
            // attached to the visible row instead of leaving it at the previous
            // no-object state.
            string imageStem = Path.GetFileNameWithoutExtension(imagePath);
            return string.IsNullOrWhiteSpace(imageStem)
                ? null
                : imageQueueItems.FirstOrDefault(item =>
                    item != null
                    && string.Equals(
                        Path.GetFileNameWithoutExtension(item.FileName),
                        imageStem,
                        StringComparison.OrdinalIgnoreCase));
        }

        internal void UpdateImageQueueStatusText(int loadedCount = -1, int totalToLoad = -1)
        {
            if (!IsAnomalyDatasetPurpose() || !ReferenceEquals(anomalyPredictionData, projectData) || !string.Equals(anomalyPredictionImagePath, activeImagePath, StringComparison.OrdinalIgnoreCase)) ClearAnomalyPrediction();
            WpfImageQueueFilter selectedFilter = GetSelectedImageQueueFilter();
            ImageQueueSummary summary = ImageQueueFilterService.Summarize(imageQueueItems);
            ImageQueueViewModel.ActiveQueueItem = FindImageQueueItem(activeImagePath);
            ImageQueueViewModel.SetAnomalyDecisionSummary(IsAnomalyDatasetPurpose() ? anomalyImageReviewSession.ReviewStatus.BuildSummary() : null);
            ImageQueueViewModel.SetDatasetContentCounts(
                summary.TotalCount,
                imageQueueItems.Count(item => item?.IsLabeled == true),
                imageQueueItems.Count(ImageQueueFilterService.IsReviewedQueueItem));
            bool hasSearch = !string.IsNullOrWhiteSpace(GetImageQueueSearchText());
            int visibleCount = !hasSearch
                ? ImageQueueFilterService.CountByFilter(summary, selectedFilter)
                : imageQueueView?.Cast<object>().Count() ?? summary.TotalCount;
            UpdateQueueQuickFilterButtons(summary, selectedFilter);
            SetDatasetStatus(ImageQueueFilterService.BuildDatasetStatusTextWithActiveImage(
                summary,
                visibleCount,
                selectedFilter,
                loadedCount,
                totalToLoad,
                activeImagePath,
                GetImageQueueSearchText(),
                annotationDirtyState?.IsDirty == true));
            context.RefreshInferenceResultPresentation?.Invoke();
            RefreshReviewModeCommandState(summary.ReviewPendingCount);
        }

        internal void UpdateQueueQuickFilterButtons()
        {
            UpdateQueueQuickFilterButtons(
                ImageQueueFilterService.Summarize(imageQueueItems),
                GetSelectedImageQueueFilter());
        }

        internal void UpdateQueueQuickFilterButtons(ImageQueueSummary summary, WpfImageQueueFilter filter)
        {
            summary ??= new ImageQueueSummary();
            ImageQueueViewModel.SetLabelingFilterCounts(summary);
            ImageQueueViewModel.SetQuickFilterState(
                filter,
                summary.CandidateCount,
                summary.FailedCount,
                summary.ConfirmedCount,
                summary.SkippedCount,
                summary.NoCandidateCount,
                summary.WorklistCount);
        }

        internal WpfImageQueueFilter GetSelectedImageQueueFilter()
        {
            return ImageQueueViewModel?.SelectedFilter
                ?? (ImageQueueFilterBox?.SelectedItem as WpfImageQueueFilterOption)?.Filter
                ?? WpfImageQueueFilter.All;
        }

        internal string GetImageQueueSearchText()
        {
            return ImageQueueViewModel?.SearchText ?? ImageQueueSearchBox?.Text ?? string.Empty;
        }

        internal void ApplyFilterSelectionChanged()
        {
            ImageQueuePanelControl?.RefreshQueueView();
            UpdateImageQueueStatusText();
        }

        internal void SetImageQueueFilter(WpfImageQueueFilter filter)
        {
            ImageQueueViewModel?.SetSelectedFilter(filter);
            if (ImageQueueFilterBox?.ItemsSource is IEnumerable<WpfImageQueueFilterOption> options)
            {
                WpfImageQueueFilterOption selected = options.FirstOrDefault(option => option.Filter == filter);
                if (selected != null)
                {
                    ImageQueueFilterBox.SelectedItem = selected;
                    return;
                }
            }

            ImageQueuePanelControl?.RefreshQueueView();
            UpdateImageQueueStatusText();
        }

        internal void ApplySearchChanged(string searchText)
        {
            ImageQueuePanelControl?.RefreshQueueView();
            SelectSingleVisibleQueueSearchResult(searchText);
            if (string.IsNullOrWhiteSpace(searchText) && ImageQueueViewModel?.SelectedQueueItem == null)
            {
                WpfImageQueueItem item = imageQueueView?.Cast<object>().OfType<WpfImageQueueItem>().FirstOrDefault(candidate => context.CanOpenQueueItem?.Invoke(candidate) == true);
                if (item != null) ImageQueueGrid.SelectedItem = item;
            }

            UpdateImageQueueStatusText();
        }

        internal void SelectSingleVisibleQueueSearchResult(string searchText = null)
        {
            if (imageQueueView == null || ImageQueueGrid == null)
            {
                return;
            }

            IReadOnlyList<WpfImageQueueItem> visibleItems = imageQueueView
                .Cast<object>()
                .OfType<WpfImageQueueItem>()
                .ToList();
            WpfImageQueueItem item = ImageQueueFilterService.FindSingleItem(visibleItems);
            if (item == null)
            {
                string effectiveSearchText = searchText ?? GetImageQueueSearchText();
                if (!string.IsNullOrWhiteSpace(effectiveSearchText) && visibleItems.Count == 0)
                {
                    ClearSelectionAfterNoSearchMatch();
                }

                return;
            }

            // Search narrows the queue for reopen/review work. If exactly one row remains,
            // select it so the visible Open action works without a fragile extra row click.
            suppressImageQueueSelection = true;
            try
            {
                ImageQueueGrid.SelectedItem = item;
                if (ImageQueueViewModel != null)
                {
                    ImageQueueViewModel.SelectedQueueItem = item;
                }

                ImageQueueGrid.ScrollIntoView(item);
            }
            finally
            {
                suppressImageQueueSelection = false;
            }

            UpdateSelectedQueueImageButton(item);
        }

        private void ClearSelectionAfterNoSearchMatch()
        {
            if (annotationDirtyState?.IsDirty == true)
            {
                return;
            }

            suppressImageQueueSelection = true;
            try
            {
                ImageQueueGrid.UnselectAll();
                ImageQueueGrid.SelectedItem = null;
                ImageQueueGrid.CurrentCell = new DataGridCellInfo();
                if (ImageQueueViewModel != null)
                {
                    ImageQueueViewModel.SelectedQueueItem = null;
                }
            }
            finally
            {
                suppressImageQueueSelection = false;
            }

            UpdateSelectedQueueImageButton(null);
            if (context.HasDisplayedInferenceResult?.Invoke() != true) context.ClearActiveImageAfterSearchNoMatch?.Invoke();
        }

        internal void ExecuteSelectedQueueItemChanged(WpfImageQueueItem item)
        {
            WpfImageQueueItem selectedItem = imageQueueSelectionService.ResolveSelectedItem(item, imageQueueItems, activeImagePath);
            if (suppressImageQueueSelection)
            {
                UpdateSelectedQueueImageButton(selectedItem);
                return;
            }

            if (selectedItem == null)
            {
                UpdateSelectedQueueImageButton(null);
                return;
            }

            UpdateSelectedQueueImageButton(selectedItem);
            context.OpenSelectedQueueImage?.Invoke(selectedItem, true);
        }

        internal ImageQueueOpenSelection GetOpenSelectedQueueSelection()
        {
            var candidates = new List<WpfImageQueueItem>
            {
                ImageQueueGrid?.SelectedItem as WpfImageQueueItem,
                ImageQueueViewModel?.SelectedQueueItem,
                FindSingleSearchMatchedQueueItem()
            };

            if (imageQueueView != null)
            {
                // UIAutomation and keyboard focus can leave DataGrid.SelectedItem unset while
                // a filtered single row is plainly visible. In that case the visible row is
                // the operator's intended target for the Open action. Filter/search changes
                // already refresh this view, so opening must not reevaluate the whole queue.
                candidates.Add(ImageQueueFilterService.FindSingleItem(imageQueueView
                    .Cast<object>()
                    .OfType<WpfImageQueueItem>()));
            }

            return imageQueueSelectionService.ResolveOpenSelection(candidates, projectData);
        }

        internal WpfImageQueueItem FindSingleSearchMatchedQueueItem()
        {
            // Open is a deliberate user command. When search text uniquely identifies
            // one filtered row, prefer that row even if DataGrid focus/selection is stale.
            return ImageQueueFilterService.FindSingleSearchMatch(
                imageQueueItems,
                GetImageQueueSearchText(),
                GetSelectedImageQueueFilter());
        }

        internal string BuildOpenQueueSelectionFailureMessage()
        {
            string searchText = GetImageQueueSearchText().Trim();
            string gridSelection = (ImageQueueGrid?.SelectedItem as WpfImageQueueItem)?.FileName ?? "-";
            string viewModelSelection = ImageQueueViewModel?.SelectedQueueItem?.FileName ?? "-";
            int visibleCount = CountVisibleQueueItems(limit: 3);
            int searchMatchCount = CountSearchMatchedQueueItems(searchText, limit: 3);
            return WpfImageQueuePresenter.BuildOpenSelectionFailureMessage(
                searchText,
                visibleCount,
                searchMatchCount,
                gridSelection,
                viewModelSelection);
        }

        internal int CountVisibleQueueItems(int limit)
        {
            if (imageQueueView == null)
            {
                return 0;
            }

            return imageQueueView
                .Cast<object>()
                .OfType<WpfImageQueueItem>()
                .Take(Math.Max(1, limit))
                .Count();
        }

        internal int CountSearchMatchedQueueItems(string searchText, int limit)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return 0;
            }

            return ImageQueueFilterService.CountSearchMatches(
                imageQueueItems,
                searchText,
                GetSelectedImageQueueFilter(),
                limit);
        }

        internal void UpdateSelectedQueueImageButton(WpfImageQueueItem item)
        {
            bool canOpenSelectedImage = context.CanOpenQueueItem?.Invoke(item) == true;

            if (ImageQueueViewModel != null)
            {
                ImageQueueViewModel.SetSelectedImageAvailability(canOpenSelectedImage);
                return;
            }

            context.SetOpenSelectedImageEnabled?.Invoke(canOpenSelectedImage);
        }

        #endregion

        #region ImageQueueReviewStatus
        // Review-state persistence runs outside the immediate delete/selection hot path and marshals only the latest result back to WPF.
        internal void RefreshActiveImageQueueStatus(bool hasActiveCandidates)
        {
            if (!imageQualityReviewWorkflowService.CanReview(projectData)) return;

            if (string.IsNullOrWhiteSpace(activeImagePath) || activeImageSize.IsEmpty)
            {
                return;
            }

            WpfImageQueueItem item = FindImageQueueItem(activeImagePath);
            if (IsAnomalyDatasetPurpose())
            {
                ApplyReviewStatusToItemCore(item, imageQualityReviewWorkflowService.GetOrCreate(activeReviewImagePath), refreshTrainingStepCompletion: false);
                RefreshImageQueueViewAfterItemStateChange();
                UpdateImageQueueStatusText();
                return;
            }
            YoloImageReviewStatus status = imageQualityReviewWorkflowService.RefreshLabelStatusAndReviewState(
                activeReviewImagePath,
                activeImageSize,
                projectData,
                hasActiveCandidates);
            imageQualityReviewWorkflowService.SaveReviewStatus(projectData);
            ApplyReviewStatusToItem(item, status);
            RefreshImageQueueViewAfterItemStateChange();
            UpdateImageQueueStatusText();
        }

        internal void QueueActiveImageQueueStatusRefresh(bool hasActiveCandidates)
        {
            if (isApplicationCloseApproved
                || string.IsNullOrWhiteSpace(activeImagePath)
                || activeImageSize.IsEmpty)
            {
                return;
            }

            if (IsAnomalyDatasetPurpose())
            {
                ApplyReviewStatusToItemCore(FindImageQueueItem(activeImagePath), imageQualityReviewWorkflowService.GetOrCreate(activeReviewImagePath), refreshTrainingStepCompletion: false);
                RefreshImageQueueViewAfterItemStateChange();
                UpdateImageQueueStatusText();
                return;
            }

            ImageQueueReviewStatusRefreshOperation operation = imageQualityReviewWorkflowService.QueueRefresh(
                activeReviewImagePath,
                activeImageSize,
                projectData,
                hasActiveCandidates);
            if (operation == null)
            {
                return;
            }

            // Delete must feel immediate. Label-file recount and review-state JSON writes are
            // background bookkeeping; only the latest completed result returns to the UI thread.
            operation.Completion.ContinueWith(
                    _ => ApplyQueuedActiveImageQueueStatusRefresh(operation),
                    TaskScheduler.Default);
        }

        private void ApplyQueuedActiveImageQueueStatusRefresh(ImageQueueReviewStatusRefreshOperation operation)
        {
            try
            {
                Dispatcher.BeginInvoke(
                    new Action(() => ApplyQueuedActiveImageQueueStatusRefreshOnUi(operation)),
                    DispatcherPriority.Background);
            }
            catch (InvalidOperationException)
            {
                // The shell can close while a queued delete-status refresh is finishing.
            }
            catch (TaskCanceledException)
            {
            }
        }

        private void ApplyQueuedActiveImageQueueStatusRefreshOnUi(ImageQueueReviewStatusRefreshOperation operation)
        {
            if (isApplicationCloseApproved
                || !imageQualityReviewWorkflowService.IsCurrent(operation.Version)
                || !string.Equals(activeReviewImagePath, operation.ImagePath, StringComparison.OrdinalIgnoreCase)
                || operation.Completion.IsCanceled)
            {
                return;
            }

            if (operation.Completion.IsFaulted)
            {
                AppendLog($"Image queue status refresh failed after delete: {operation.Completion.Exception?.GetBaseException().Message}");
                return;
            }

            if (operation.Completion.Result == null)
            {
                return;
            }

            ApplyReviewStatusToItem(FindImageQueueItem(operation.ImagePath), operation.Completion.Result);
            RefreshImageQueueViewAfterItemStateChange();
            UpdateImageQueueStatusText();
        }

        internal bool IsActiveImageQueueSaveRequired(WpfImageQueueItem item)
        {
            return item != null
                && annotationDirtyState.IsDirty
                && !string.IsNullOrWhiteSpace(activeImagePath)
                && string.Equals(item.ImagePath, activeReviewImagePath, StringComparison.OrdinalIgnoreCase);
        }

        internal void ApplyActiveImageQueueSaveRequiredStatus(string reason)
        {
            WpfImageQueuePresenter.ApplySaveRequiredStatusToItem(FindImageQueueItem(activeImagePath), reason);
            // Live filtering observes IsSaveRequired; refreshing here resets and redraws the entire queue per edit.
            UpdateImageQueueStatusText();
        }

        internal void SetActiveImageDetectionStatus(int candidateCount, bool succeeded)
        {
            if (!imageQualityReviewWorkflowService.CanReview(projectData)) return;

            if (string.IsNullOrWhiteSpace(activeImagePath))
            {
                return;
            }

            string imageName = Path.GetFileNameWithoutExtension(activeImagePath);
            YoloImageReviewStatus status = succeeded
                ? candidateCount > 0
                    ? imageQualityReviewWorkflowService.SetDetectionCandidates(activeReviewImagePath, imageName, candidateCount)
                    : imageQualityReviewWorkflowService.SetDetectionNoCandidates(activeReviewImagePath, imageName)
                : imageQualityReviewWorkflowService.SetDetectionFailed(activeReviewImagePath, imageName, "Detection failed.");
            imageQualityReviewWorkflowService.SaveReviewStatus(projectData);
            if (IsAnomalyDatasetPurpose())
            {
                ApplyReviewStatusToItemCore(FindImageQueueItem(activeImagePath), status, refreshTrainingStepCompletion: false);
            }
            else
            {
                ApplyReviewStatusToItem(FindImageQueueItem(activeImagePath), status);
            }
            RefreshImageQueueViewAfterItemStateChange();
            UpdateImageQueueStatusText();
        }

        internal bool ApplyActiveAnomalyClassification(IReadOnlyList<YoloWorkerSmokeCandidate> candidates)
        {
            ClearAnomalyPrediction();
            if (!IsAnomalyDatasetPurpose() || string.IsNullOrWhiteSpace(activeImagePath)) return false;
            AnomalyClassificationDecision decision = AnomalyClassificationDecisionService.Build(candidates, projectData.ProjectSettings.AnomalyClassification.ToDecisionOptions());
            if (!decision.IsMapped) return false;
            anomalyPrediction = decision; anomalyPredictionImagePath = activeImagePath; anomalyPredictionData = projectData;
            if (!ApplyAnomalyClassificationToImage(activeImagePath, Path.GetFileNameWithoutExtension(activeImagePath), candidates, saveReviewStatus: true))
            {
                ClearAnomalyPrediction();
                return false;
            }
            ImageQueueViewModel?.SetAnomalyPrediction(anomalyPrediction);
            return true;
        }

        internal bool TryAcceptAnomalyPrediction()
        {
            RefreshAnomalyDecisionCommandState();
            if (anomalyPrediction?.IsMapped != true || ImageQueueViewModel?.IsAnomalyDecisionAvailable != true || ImageQueueViewModel.IsAnomalyGridActive) return false;
            AnomalyImageReviewState state = anomalyPrediction.ReviewState;
            ExecuteAnomalySelectionDecision(state);
            return true;
        }

        internal void ClearAnomalyPrediction()
        {
            anomalyPrediction = null; anomalyPredictionImagePath = null; anomalyPredictionData = null;
            ImageQueueViewModel?.SetAnomalyPrediction(null);
        }

        internal bool ApplyAnomalyClassificationToImage(
            string imagePath,
            string imageName,
            IReadOnlyList<YoloWorkerSmokeCandidate> candidates,
            bool saveReviewStatus)
        {
            if (!IsAnomalyDatasetPurpose() || string.IsNullOrWhiteSpace(imagePath))
            {
                return false;
            }

            AnomalyClassificationResult result = anomalyImageReviewSession.ApplyClassification(
                new AnomalyClassificationRequest(
                    imagePath,
                    imageName,
                    candidates,
                    AnomalyClassificationOptionsSnapshot.From(
                        projectData.ProjectSettings.AnomalyClassification.ToDecisionOptions()),
                    saveReviewStatus),
                projectData);
            if (!result.IsMapped)
            {
                return false;
            }

            ApplyReviewStatusToItemCore(FindImageQueueItem(imagePath), imageQualityReviewWorkflowService.GetOrCreate(imagePath), refreshTrainingStepCompletion: false);
            UpdateImageQueueStatusText();
            return true;
        }

        internal void MarkActiveImageConfirmed()
        {
            if (!imageQualityReviewWorkflowService.CanReview(projectData)) return;

            if (string.IsNullOrWhiteSpace(activeImagePath))
            {
                return;
            }

            YoloImageReviewStatus status = imageQualityReviewWorkflowService.MarkConfirmed(activeReviewImagePath, Path.GetFileNameWithoutExtension(activeImagePath));
            if (!activeImageSize.IsEmpty)
            {
                status = imageQualityReviewWorkflowService.RefreshLabelStatusAndReviewState(activeReviewImagePath, activeImageSize, projectData, hasActiveCandidates: false) ?? status;
            }

            ApplyReviewStatusToItem(FindImageQueueItem(activeImagePath), status);
            imageQualityReviewWorkflowService.SaveReviewStatus(projectData);
            MarkActiveAnomalyImageAbnormal();
            // A confirmed save changes both the row's review state and any active
            // worklist filter. Refresh the projection so the row cannot retain a
            // stale "no object" presentation while the canvas already shows the
            // saved labels.
            RefreshImageQueueViewAfterItemStateChange();
            UpdateImageQueueStatusText();
        }

        internal void MarkActiveImageNoCandidate()
        {
            if (!imageQualityReviewWorkflowService.CanReview(projectData)) return;

            if (string.IsNullOrWhiteSpace(activeImagePath))
            {
                return;
            }

            string imageName = Path.GetFileNameWithoutExtension(activeImagePath);
            YoloImageReviewStatus status = imageQualityReviewWorkflowService.SetDetectionNoCandidates(activeReviewImagePath, imageName);
            if (!activeImageSize.IsEmpty)
            {
                status = imageQualityReviewWorkflowService.RefreshLabelStatusAndReviewState(activeReviewImagePath, activeImageSize, projectData, hasActiveCandidates: false) ?? status;
            }

            ApplyReviewStatusToItem(FindImageQueueItem(activeImagePath), status);
            imageQualityReviewWorkflowService.SaveReviewStatus(projectData);
            MarkActiveAnomalyImageNormal();
            RefreshImageQueueViewAfterItemStateChange();
            UpdateImageQueueStatusText();
        }

        internal void MarkActiveImageSkippedOrCandidate()
        {
            if (!imageQualityReviewWorkflowService.CanReview(projectData)) return;

            if (string.IsNullOrWhiteSpace(activeImagePath))
            {
                return;
            }

            string imageName = Path.GetFileNameWithoutExtension(activeImagePath);
            YoloImageReviewStatus status = pendingDetectionCandidates.Count > 0
                ? imageQualityReviewWorkflowService.SetDetectionCandidates(activeReviewImagePath, imageName, pendingDetectionCandidates.Count)
                : imageQualityReviewWorkflowService.MarkSkipped(activeImagePath, imageName);
            ApplyReviewStatusToItem(FindImageQueueItem(activeImagePath), status);
            imageQualityReviewWorkflowService.SaveReviewStatus(projectData);
            RefreshImageQueueViewAfterItemStateChange();
            UpdateImageQueueStatusText();
        }

        internal void ExecuteMarkQualityUnreviewedCommand()
        {
            SetActiveImageQualityReviewState(YoloImageQualityReviewState.Unreviewed);
        }

        internal void ExecuteMarkQualityNeedsFixCommand()
        {
            SetActiveImageQualityReviewState(YoloImageQualityReviewState.NeedsFix);
        }

        internal void ExecuteMarkQualityReviewedCommand()
        {
            SetActiveImageQualityReviewState(YoloImageQualityReviewState.Reviewed);
        }

        internal void ExecuteExportQualityReviewReportCommand()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            if (!IsLabelQualityReviewPurpose())
            {
                SetModelStatus("QA 보고서는 Detection/Segmentation 데이터셋에서 내보낼 수 있습니다.");
                return;
            }

            try
            {
                ImageQualityReviewReportResult report = imageQualityReviewWorkflowService.ExportQualityReviewReport(projectData);
                if (!report.HasOutputPath)
                {
                    SetModelStatus(OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.ImageQueueReviewAdapter.ReportFolder"));
                    return;
                }

                SetModelStatus(string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.ImageQueueReviewAdapter.ReportSaved"), Path.GetFileName(report.OutputPath), report.NeedsFixCount));
                AppendLog(string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.ImageQueueReviewAdapter.ReportLog"), Path.GetFileName(report.OutputPath), report.TotalImageCount, report.NeedsFixCount, report.ReviewedCount));
            }
            catch (Exception exception)
            {
                SetModelStatus($"QA 보고서 저장 실패: {exception.Message}");
                AppendLog($"QA 보고서 저장 실패: {exception.Message}");
            }
        }

        internal void SetActiveImageQualityReviewState(YoloImageQualityReviewState state)
            => TrySetActiveImageQualityReviewState(state);

        private bool TrySetActiveImageQualityReviewState(YoloImageQualityReviewState state, string note = null)
        {
            if (isApplicationCloseApproved) return false;
            if (!IsLabelQualityReviewPurpose() || string.IsNullOrWhiteSpace(activeImagePath))
            {
                ReportReviewGuidance(T("WpfReview.Detail.NoImage"));
                return false;
            }

            WpfImageQueueItem item = FindImageQueueItem(activeImagePath);
            string imageName = Path.GetFileNameWithoutExtension(activeImagePath);
            YoloImageReviewStatus previous = imageQualityReviewWorkflowService.GetOrCreate(activeReviewImagePath);
            YoloImageQualityReviewState previousState = previous?.QualityReviewState ?? YoloImageQualityReviewState.Unreviewed;
            string previousNote = previous?.QualityReviewNote ?? string.Empty;
            ImageQualityReviewResult result = imageQualityReviewWorkflowService.ApplyQualityReview(
                new ImageQualityReviewRequest(
                    activeReviewImagePath,
                    imageName,
                    state,
                    IsLabelQualityReviewPurpose(),
                    item?.IsSaveRequired == true,
                    annotationDirtyState.IsDirty,
                    ImageQueueFilterService.HasCompletedLabelWork(item),
                    note ?? ObjectReviewViewModel?.QualityReviewNoteText),
                projectData);
            if (!result.IsApplicable)
            {
                ReportReviewGuidance(T("WpfReview.Guidance.Unavailable"));
                return false;
            }

            if (!result.IsAccepted)
            {
                ReportReviewGuidance(T("WpfReview.Detail.Unworked"));
                RefreshActiveImageQualityReviewPresentation(item, result.Status);
                return false;
            }

            YoloImageReviewStatus status = result.Status;
            if (status.QualityReviewState != previousState || status.QualityReviewNote != previousNote) context.SavedGroundTruthChanged?.Invoke();
            ApplyReviewStatusToItem(item, status);
            RefreshImageQueueViewAfterItemStateChange();
            UpdateImageQueueStatusText();

            string displayText = WpfImageQueuePresenter.FormatQualityReviewState(state);
            SetModelStatus($"품질 검수: {displayText}");
            AppendLog($"품질 검수 상태 변경: {Path.GetFileName(activeImagePath)} / {displayText}");
            return true;
        }

        internal void SetLabelingReviewMode(bool reviewMode)
        {
            RefreshReviewModeCommandState();
            if (ObjectReviewViewModel?.IsReviewModeAvailable != true) return;
            ObjectReviewViewModel.SetReviewMode(reviewMode);
            if (reviewMode) context.SelectReviewTool?.Invoke();
            SetImageQueueFilter(reviewMode ? WpfImageQueueFilter.ReviewPending : WpfImageQueueFilter.All);
            if (reviewMode && !annotationDirtyState.IsDirty && !ImageQueueFilterService.IsReviewPendingQueueItem(FindImageQueueItem(activeImagePath)))
                context.TryOpenNextReviewPendingImage?.Invoke(activeReviewImagePath, CaptureReviewOrder());
            RefreshReviewModeCommandState();
        }

        internal void RefreshReviewModeCommandState(int? pendingCount = null)
        {
            RefreshAnomalyDecisionCommandState();
            if (ObjectReviewViewModel == null) return;
            bool isLabelQualityReviewPurpose = IsLabelQualityReviewPurpose();
            if (!isLabelQualityReviewPurpose && ObjectReviewViewModel.IsReviewModeActive)
            {
                ObjectReviewViewModel.SetReviewMode(false);
                if (GetSelectedImageQueueFilter() == WpfImageQueueFilter.ReviewPending) SetImageQueueFilter(WpfImageQueueFilter.All);
            }
            bool available = !isApplicationCloseApproved && !isReviewCommandRunning && isLabelQualityReviewPurpose
                && context.IsLabelingStageActive?.Invoke() == true && imageQualityReviewWorkflowService.CanReview(projectData)
                && context.IsOperationRunning?.Invoke() != true && context.HasWorkflowDraft?.Invoke() != true;
            WpfImageQueueItem item = FindImageQueueItem(activeImagePath);
            bool canAct = available && item != null && !activeImageSize.IsEmpty && context.IsAnnotationLoadBlocked?.Invoke() != true;
            bool canApprove = canAct && (!IsAnomalyDatasetPurpose() || ImageQueueFilterService.HasSavedGroundTruth(item)) && (annotationDirtyState.IsDirty
                ? context.HasCanvasLabelObjects?.Invoke() == true || context.IsNoObjectSelected?.Invoke() == true
                : ImageQueueFilterService.HasSavedGroundTruth(item));
            ObjectReviewViewModel.SetReviewModeState(available, canApprove, canAct && ImageQueueFilterService.HasSavedGroundTruth(item),
                pendingCount ?? ObjectReviewViewModel.PendingReviewCount);
        }

        internal void ApproveAndOpenNextReviewImage()
        {
            RefreshReviewModeCommandState();
            if (ObjectReviewViewModel?.IsReviewModeActive != true || !ObjectReviewViewModel.IsApproveAndNextEnabled) return;
            string reviewedPath = activeReviewImagePath;
            IReadOnlyList<WpfImageQueueItem> order = CaptureReviewOrder();
            isReviewCommandRunning = true;
            RefreshReviewModeCommandState();
            try
            {
                ObjectReviewViewModel.SetReviewGuidance(string.Empty);
                if (annotationDirtyState.IsDirty && context.SaveAnnotationsForReview?.Invoke() != true)
                {
                    ReportReviewGuidance(T("WpfReview.Guidance.LabelSaveFailed"));
                    return;
                }
                YoloImageReviewStatus status = IsAnomalyDatasetPurpose()
                    ? imageQualityReviewWorkflowService.GetOrCreate(activeReviewImagePath)
                    : imageQualityReviewWorkflowService.RefreshLabelStatusAndReviewState(activeReviewImagePath, activeImageSize, projectData, pendingDetectionCandidates.Count > 0);
                ApplyReviewStatusToItem(FindImageQueueItem(activeImagePath), status);
                if (!ImageQueueFilterService.HasSavedGroundTruth(FindImageQueueItem(activeImagePath)) || annotationDirtyState.IsDirty)
                {
                    ReportReviewGuidance(T("WpfReview.Detail.Unworked"));
                    return;
                }
                if (!TrySetActiveImageQualityReviewState(YoloImageQualityReviewState.Reviewed, string.Empty)) return;
                OpenNextReviewImage(reviewedPath, order);
            }
            catch (Exception exception) when (IsReviewStorageException(exception))
            {
                ReportReviewGuidance(T("WpfReview.Guidance.ReviewSaveFailed") + " " + exception.Message);
                RefreshActiveImageQualityReviewPresentation();
            }
            finally
            {
                isReviewCommandRunning = false;
                RefreshReviewModeCommandState();
            }
        }

        internal void RejectAndOpenNextReviewImage()
        {
            RefreshReviewModeCommandState();
            if (ObjectReviewViewModel?.IsReviewModeActive != true || !ObjectReviewViewModel.IsRejectImageEnabled) return;
            string reviewedPath = activeReviewImagePath;
            IReadOnlyList<WpfImageQueueItem> order = CaptureReviewOrder();
            isReviewCommandRunning = true;
            RefreshReviewModeCommandState();
            try
            {
                string note = context.ShowRejectionDialog?.Invoke(activeImagePath, annotationDirtyState.IsDirty);
                if (note == null || !string.Equals(reviewedPath, activeReviewImagePath, StringComparison.OrdinalIgnoreCase)) return;
                if (!TrySetActiveImageQualityReviewState(YoloImageQualityReviewState.NeedsFix, note)) return;
                if (annotationDirtyState.IsDirty && context.DiscardUnsavedAnnotations?.Invoke() != true)
                {
                    ReportReviewGuidance(T("WpfReview.Guidance.ReloadFailed"));
                    return;
                }
                OpenNextReviewImage(reviewedPath, order);
            }
            catch (Exception exception) when (IsReviewStorageException(exception))
            {
                ReportReviewGuidance(T("WpfReview.Guidance.ReviewSaveFailed") + " " + exception.Message);
                RefreshActiveImageQualityReviewPresentation();
            }
            finally
            {
                isReviewCommandRunning = false;
                RefreshReviewModeCommandState();
            }
        }

        internal void OnAnnotationsSaved(bool hadChanges)
        {
            if (!IsLabelQualityReviewPurpose() || !imageQualityReviewWorkflowService.CanReview(projectData) || string.IsNullOrWhiteSpace(activeImagePath)) return;
            try
            {
                YoloImageReviewStatus status = IsAnomalyDatasetPurpose()
                    ? imageQualityReviewWorkflowService.GetOrCreate(activeReviewImagePath)
                    : imageQualityReviewWorkflowService.RefreshLabelStatusAndReviewState(activeReviewImagePath, activeImageSize, projectData, pendingDetectionCandidates.Count > 0);
                ApplyReviewStatusToItem(FindImageQueueItem(activeImagePath), status);
                if (hadChanges && status?.QualityReviewState != YoloImageQualityReviewState.Unreviewed)
                    TrySetActiveImageQualityReviewState(YoloImageQualityReviewState.Unreviewed, string.Empty);
                UpdateImageQueueStatusText();
            }
            catch (Exception exception) when (IsReviewStorageException(exception))
            {
                ReportReviewGuidance(T("WpfReview.Guidance.LabelSavedReviewFailed") + " " + exception.Message);
            }
        }

        private IReadOnlyList<WpfImageQueueItem> CaptureReviewOrder()
            => imageQueueView?.Cast<object>().OfType<WpfImageQueueItem>().ToList() ?? imageQueueItems.ToList();

        private void OpenNextReviewImage(string reviewedPath, IReadOnlyList<WpfImageQueueItem> order)
        {
            if (context.TryOpenNextReviewPendingImage?.Invoke(reviewedPath, order) == true)
                ObjectReviewViewModel.SetReviewGuidance(string.Empty);
            else ReportReviewGuidance(T(imageQueueItems.Any(ImageQueueFilterService.IsReviewPendingQueueItem)
                ? "WpfReview.Guidance.NextLoadFailed" : "WpfReview.Guidance.None"));
            UpdateImageQueueStatusText();
        }

        private void ReportReviewGuidance(string message)
        {
            ObjectReviewViewModel?.SetReviewGuidance(message);
            SetModelStatus(message);
            AppendLog(message);
        }

        private static bool IsReviewStorageException(Exception exception)
            => exception is IOException || exception is UnauthorizedAccessException || exception is InvalidDataException
                || exception is ArgumentException || exception is NotSupportedException || exception is System.Security.SecurityException;

        private static string T(string key) => OpenVisionLab.OpenVisionLanguageService.T(key);

        internal void InvalidateActiveImageQualityReviewAfterEdit()
        {
            if (!imageQualityReviewWorkflowService.CanReview(projectData)) return;

            if (!IsLabelQualityReviewPurpose() || string.IsNullOrWhiteSpace(activeImagePath))
            {
                return;
            }

            YoloImageReviewStatus before = imageQualityReviewWorkflowService.GetOrCreate(activeReviewImagePath);
            if (before?.QualityReviewState != YoloImageQualityReviewState.Reviewed)
            {
                RefreshActiveImageQualityReviewPresentation(FindImageQueueItem(activeImagePath), before);
                return;
            }

            try
            {
                WpfImageQueueItem item = FindImageQueueItem(activeImagePath);
                ImageQualityReviewResult result = imageQualityReviewWorkflowService.ApplyQualityReview(new ImageQualityReviewRequest(
                    activeReviewImagePath, Path.GetFileNameWithoutExtension(activeImagePath), YoloImageQualityReviewState.Unreviewed,
                    true, item?.IsSaveRequired == true, annotationDirtyState.IsDirty, ImageQueueFilterService.HasCompletedLabelWork(item), string.Empty), projectData);
                if (!result.IsAccepted) return;
                context.SavedGroundTruthChanged?.Invoke();
                ApplyReviewStatusToItem(item, result.Status);
                UpdateImageQueueStatusText();
            }
            catch (Exception error) when (IsReviewStorageException(error))
            {
                ReportReviewGuidance(T("WpfReview.Guidance.ReviewSaveFailed") + " " + error.Message);
            }
        }

        internal void RefreshActiveImageQualityReviewPresentation()
        {
            WpfImageQueueItem item = FindImageQueueItem(activeImagePath);
            RefreshActiveImageQualityReviewPresentation(item, imageQualityReviewWorkflowService.GetOrCreate(activeReviewImagePath));
        }

        internal void RefreshActiveImageQualityReviewPresentation(
            WpfImageQueueItem item,
            YoloImageReviewStatus status)
        {
            if (item != null && !string.Equals(item.ImagePath, FindImageQueueItem(activeImagePath)?.ImagePath, StringComparison.OrdinalIgnoreCase)) return;
            bool hasActiveImage = IsLabelQualityReviewPurpose()
                && !string.IsNullOrWhiteSpace(activeImagePath)
                && item != null
                && string.Equals(item.ImagePath, activeReviewImagePath, StringComparison.OrdinalIgnoreCase);
            bool canMarkReviewed = hasActiveImage
                && !annotationDirtyState.IsDirty
                && !item.IsSaveRequired
                && ImageQueueFilterService.HasSavedGroundTruth(item);
            ObjectReviewViewModel?.SetQualityReviewState(
                status?.QualityReviewState ?? YoloImageQualityReviewState.Unreviewed,
                hasActiveImage,
                canMarkReviewed,
                status?.QualityReviewNote,
                ImageQueueFilterService.HasSavedGroundTruth(item));
            RefreshReviewModeCommandState();
        }

        internal bool IsLabelQualityReviewPurpose()
        {
            // Row projection reads the configured purpose; runtime path discovery belongs to project setup.
            LabelingDatasetPurpose purpose = projectData.ProjectSettings?.DatasetPurpose ?? LabelingDatasetPurpose.ObjectDetection;
            return purpose == LabelingDatasetPurpose.ObjectDetection
                || purpose == LabelingDatasetPurpose.Segmentation
                || purpose == LabelingDatasetPurpose.AnomalyDetection;
        }

        internal bool IsAnomalyDatasetPurpose()
        {
            return projectData.ProjectSettings?.DatasetPurpose == LabelingDatasetPurpose.AnomalyDetection;
        }

        internal void MarkActiveAnomalyImageNormal()
        {
            MarkActiveAnomalyImageReviewState(AnomalyImageReviewState.Normal);
        }

        internal void MarkActiveAnomalyImageAbnormal()
        {
            MarkActiveAnomalyImageReviewState(AnomalyImageReviewState.Abnormal);
        }

        internal void MarkActiveAnomalyImageReviewState(AnomalyImageReviewState state)
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            TryMarkActiveAnomalyImageReviewState(state);
        }

        internal bool TryMarkActiveAnomalyImageReviewState(AnomalyImageReviewState state)
        {
            if (isApplicationCloseApproved)
            {
                return false;
            }

            if (!IsAnomalyDatasetPurpose() || string.IsNullOrWhiteSpace(activeImagePath))
            {
                return false;
            }

            string imageName = Path.GetFileNameWithoutExtension(activeImagePath);
            return MarkAnomalyImageReviewState(activeImagePath, imageName, state, saveReviewStatus: true);
        }

        internal void ExecuteMarkActiveAnomalyNormalAndNextCommand()
        {
            MarkActiveAnomalyImageAndOpenNext(AnomalyImageReviewState.Normal);
        }

        internal void ExecuteMarkActiveAnomalyAbnormalAndNextCommand()
        {
            MarkActiveAnomalyImageAndOpenNext(AnomalyImageReviewState.Abnormal);
        }

        internal void ExecuteClearActiveAnomalyReviewCommand()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            if (!IsAnomalyDatasetPurpose() || string.IsNullOrWhiteSpace(activeImagePath))
            {
                return;
            }

            if (!TryMarkActiveAnomalyImageReviewState(AnomalyImageReviewState.Unreviewed))
            {
                return;
            }
            SetDatasetStatus($"OK/NG 이미지 판정: 미판정으로 되돌림 / {Path.GetFileName(activeImagePath)}");
            AppendLog($"Anomaly image review cleared: {activeImagePath}");
        }

        internal void MarkActiveAnomalyImageAndOpenNext(AnomalyImageReviewState state)
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            if (!IsAnomalyDatasetPurpose() || string.IsNullOrWhiteSpace(activeImagePath))
            {
                return;
            }

            string reviewedPath = activeImagePath;
            if (!TryMarkActiveAnomalyImageReviewState(state))
            {
                return;
            }
            string decisionText = state == AnomalyImageReviewState.Normal ? "정상(OK)" : "이상(NG)";
            SetDatasetStatus($"OK/NG 이미지 판정: {decisionText} 저장 / {Path.GetFileName(reviewedPath)}");
            AppendLog($"Anomaly image reviewed: {reviewedPath} / {state}");
            if (!TryOpenNextIncompleteQueueImage())
            {
                SetDatasetStatus("OK/NG 이미지 판정: 모든 이미지 판정 완료");
            }
        }

        internal bool MarkAnomalyImageReviewState(string imagePath, string imageName, AnomalyImageReviewState state, bool saveReviewStatus)
        {
            try
            {
                bool stateChanged = anomalyImageReviewSession.GetStatus(imagePath, IsAnomalyDatasetPurpose())?.ReviewState != state;
                AnomalyImageReviewCommandResult result = anomalyImageReviewSession.Apply(
                    imagePath, imageName, state, isAnomalyPurpose: IsAnomalyDatasetPurpose(), projectData, saveReviewStatus, imageQualityReviewWorkflowService);
                if (!result.IsApplicable) return false;
                if (saveReviewStatus && stateChanged) context.SavedGroundTruthChanged?.Invoke();
                ApplyReviewStatusToItemCore(FindImageQueueItem(imagePath), imageQualityReviewWorkflowService.GetOrCreate(imagePath), refreshTrainingStepCompletion: false);
                // Live filtering observes AnomalyReviewState/IsLabeled. Refresh() would reset and redraw every row.
                UpdateImageQueueStatusText();
                if (saveReviewStatus && state != AnomalyImageReviewState.Unreviewed)
                    ImageQueueViewModel?.ShowAnomalyDecisionNotice("Single", Path.GetFileName(imagePath), state, offerUndo: stateChanged && anomalyImageReviewSession.CanUndoReview(projectData));
                RefreshAnomalyDecisionCommandState();
                return true;
            }
            catch (Exception exception) when (IsReviewStorageException(exception))
            {
                ReportAnomalyDecisionFailure(exception);
                return false;
            }
        }

        internal void ConfigureAnomalyReviewCommands()
        {
            ImageQueueViewModel?.ConfigureAnomalyReviewCommands(imageQueueView ?? (System.Collections.IEnumerable)imageQueueItems,
                ExecuteAnomalySelectionDecision, UndoAnomalyDecision, OpenAnomalyGridImage, SetAnomalyAutoAdvance);
            RefreshAnomalyDecisionCommandState();
        }

        private void RefreshAnomalyDecisionCommandState()
        {
            if (!IsAnomalyDatasetPurpose() || !ReferenceEquals(anomalyPredictionData, projectData) || !string.Equals(anomalyPredictionImagePath, activeImagePath, StringComparison.OrdinalIgnoreCase)) ClearAnomalyPrediction();
            if (ImageQueueViewModel == null || anomalyImageReviewSession == null) return;
            context.SetAnomalyReviewSaveState?.Invoke(anomalyImageReviewSession.CanReview(projectData)
                ? anomalyImageReviewSession.GetStatus(activeImagePath, IsAnomalyDatasetPurpose())?.ReviewState : null);
            ImageQueueViewModel.RestoreAnomalyAutoAdvance(projectData?.ProjectSettings?.AnomalyAutoAdvanceAfterDecision ?? true);
            bool hasView = !isApplicationCloseApproved && IsAnomalyDatasetPurpose() && context.IsLabelingStageActive?.Invoke() == true && anomalyImageReviewSession.CanReview(projectData);
            bool available = hasView && !isReviewCommandRunning && context.IsOperationRunning?.Invoke() != true
                && context.HasWorkflowDraft?.Invoke() != true && context.IsAnnotationLoadBlocked?.Invoke() != true && annotationDirtyState?.IsDirty != true;
            ImageQueueViewModel.SetAnomalyDecisionAvailability(available, !string.IsNullOrWhiteSpace(activeImagePath) && !activeImageSize.IsEmpty,
                anomalyImageReviewSession.CanUndoReview(projectData), resetView: !hasView);
        }

        private void ExecuteAnomalySelectionDecision(AnomalyImageReviewState state)
        {
            RefreshAnomalyDecisionCommandState();
            if (ImageQueueViewModel?.IsAnomalyDecisionAvailable != true) return;
            if (!ImageQueueViewModel.IsAnomalyGridActive)
            {
                if (activeImageSize.IsEmpty) return;
                isReviewCommandRunning = true;
                RefreshReviewModeCommandState();
                try
                {
                    if (ImageQueueViewModel.IsAnomalyAutoAdvanceAfterDecision && ObjectReviewViewModel?.IsReviewModeActive != true) MarkActiveAnomalyImageAndOpenNext(state);
                    else TryMarkActiveAnomalyImageReviewState(state);
                }
                finally { isReviewCommandRunning = false; RefreshReviewModeCommandState(); }
                return;
            }
            WpfImageQueueItem[] selected = ImageQueueViewModel.AnomalyGridSelection.ToArray();
            if (selected.Length == 0 || selected.Any(item => !ReferenceEquals(FindImageQueueItem(item.ImagePath), item)
                || !ImageQueueViewModel.ShouldShow(item))) return;
            isReviewCommandRunning = true;
            RefreshReviewModeCommandState();
            try
            {
                IReadOnlyList<AnomalyImageReviewStatus> changed = anomalyImageReviewSession.ApplyMany(selected.Select(item => item.ImagePath), state, true, projectData, imageQualityReviewWorkflowService);
                if (changed.Count > 0) context.SavedGroundTruthChanged?.Invoke();
                foreach (AnomalyImageReviewStatus status in changed)
                    ApplyReviewStatusToItemCore(FindImageQueueItem(status.ImagePath), imageQualityReviewWorkflowService.GetOrCreate(status.ImagePath), refreshTrainingStepCompletion: false);
                UpdateImageQueueStatusText();
                ImageQueueViewModel.ClearAnomalyGridSelection();
                ImageQueueViewModel.ShowAnomalyDecisionNotice("Bulk", state: state, count: selected.Length, offerUndo: changed.Count > 0);
                AppendLog($"Anomaly grid decision saved: {selected.Length} images / {state}.");
            }
            catch (Exception exception) when (IsReviewStorageException(exception)) { ReportAnomalyDecisionFailure(exception); }
            finally { isReviewCommandRunning = false; RefreshReviewModeCommandState(); }
        }

        private void UndoAnomalyDecision()
        {
            RefreshAnomalyDecisionCommandState();
            if (ImageQueueViewModel?.IsUndoAnomalyDecisionEnabled != true) return;
            isReviewCommandRunning = true;
            RefreshReviewModeCommandState();
            try
            {
                IReadOnlyList<AnomalyImageReviewStatus> restored = anomalyImageReviewSession.UndoReview(projectData, imageQualityReviewWorkflowService);
                if (restored.Count > 0) context.SavedGroundTruthChanged?.Invoke();
                foreach (AnomalyImageReviewStatus status in restored)
                    ApplyReviewStatusToItemCore(FindImageQueueItem(status.ImagePath), imageQualityReviewWorkflowService.GetOrCreate(status.ImagePath), refreshTrainingStepCompletion: false);
                UpdateImageQueueStatusText();
                bool imageAvailable = restored.Count == 0 || ImageQueueViewModel.IsAnomalyGridActive
                    || string.Equals(activeReviewImagePath, restored[0].ImagePath, StringComparison.OrdinalIgnoreCase)
                    || context.OpenAnomalyGridImage?.Invoke(FindImageQueueItem(restored[0].ImagePath)) == true;
                ImageQueueViewModel.ShowAnomalyDecisionNotice(imageAvailable ? "Undone" : "UndoneImageUnavailable");
                AppendLog($"Anomaly decision restored: {restored.Count} images.");
            }
            catch (Exception exception) when (IsReviewStorageException(exception)) { ReportAnomalyDecisionFailure(exception); }
            finally { isReviewCommandRunning = false; RefreshReviewModeCommandState(); }
        }

        private void OpenAnomalyGridImage(WpfImageQueueItem item)
        {
            RefreshAnomalyDecisionCommandState();
            if (ImageQueueViewModel?.IsAnomalyDecisionAvailable != true || item == null || !ReferenceEquals(FindImageQueueItem(item.ImagePath), item)) return;
            if (context.OpenAnomalyGridImage?.Invoke(item) == true) ImageQueueViewModel.SetAnomalyGridActive(false);
        }

        private void SetAnomalyAutoAdvance(bool enabled)
        {
            RefreshAnomalyDecisionCommandState();
            if (ImageQueueViewModel?.IsAnomalyDecisionAvailable != true || projectData?.ProjectSettings == null) return;
            bool previous = projectData.ProjectSettings.AnomalyAutoAdvanceAfterDecision;
            projectData.ProjectSettings.AnomalyAutoAdvanceAfterDecision = enabled;
            string recipeName = context.CurrentRecipeNameProvider?.Invoke();
            try
            {
                if (!string.IsNullOrWhiteSpace(recipeName) && context.SaveCurrentRecipeConfiguration != null)
                {
                    RecipeConfigurationSaveResult result = context.SaveCurrentRecipeConfiguration(recipeName);
                    if (result?.IsSuccess != true) throw new IOException(result?.ErrorMessage ?? "Recipe option save failed.");
                }
            }
            catch (Exception exception) when (IsReviewStorageException(exception))
            {
                projectData.ProjectSettings.AnomalyAutoAdvanceAfterDecision = previous;
                ImageQueueViewModel.ShowAnomalyDecisionNotice("PreferenceFailed");
                AppendLog("Anomaly auto-advance option save failed; previous option retained: " + exception.Message);
            }
            finally { ImageQueueViewModel.RestoreAnomalyAutoAdvance(projectData.ProjectSettings.AnomalyAutoAdvanceAfterDecision); }
        }

        private void ReportAnomalyDecisionFailure(Exception exception)
        {
            ImageQueueViewModel?.ShowAnomalyDecisionNotice("Failed");
            SetDatasetStatus(T("WpfAnomalyGrid.Notice.Failed"));
            AppendLog("Anomaly decision save failed; previous state retained: " + exception.Message);
            RefreshAnomalyDecisionCommandState();
        }

        internal void RefreshImageQueuePurposePresentation()
        {
            var d1Total = System.Diagnostics.Stopwatch.StartNew();
            int d1Visited = 0, d1Statuses = 0;
            long d1GetTicks = 0, d1ApplyTicks = 0, d1MaxGetTicks = 0, d1MaxApplyTicks = 0;
            System.Diagnostics.Trace.WriteLine(FormattableString.Invariant($"D1_QUEUE_BEGIN rows={imageQueueItems.Count} anomaly={IsAnomalyDatasetPurpose()}"));
            foreach (WpfImageQueueItem item in imageQueueItems)
            {
                long d1Started = System.Diagnostics.Stopwatch.GetTimestamp();
                YoloImageReviewStatus status = imageQualityReviewWorkflowService.GetOrCreate(item.ImagePath);
                long d1Elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - d1Started;
                d1GetTicks += d1Elapsed; d1MaxGetTicks = Math.Max(d1MaxGetTicks, d1Elapsed);
                d1Started = System.Diagnostics.Stopwatch.GetTimestamp();
                ApplyReviewStatusToItemCore(item, status, refreshTrainingStepCompletion: false);
                d1Elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - d1Started;
                d1ApplyTicks += d1Elapsed; d1MaxApplyTicks = Math.Max(d1MaxApplyTicks, d1Elapsed);
                d1Visited++; if (status != null) d1Statuses++;
            }

            double d1RowsMs = d1Total.Elapsed.TotalMilliseconds;
            ImageQueuePanelControl?.RefreshQueueView();
            double d1ViewMs = d1Total.Elapsed.TotalMilliseconds;
            UpdateImageQueueStatusText();
            double d1TickMs = 1000D / System.Diagnostics.Stopwatch.Frequency;
            System.Diagnostics.Trace.WriteLine(FormattableString.Invariant($"D1_QUEUE_END rows={d1Visited} statuses={d1Statuses} totalMs={d1Total.Elapsed.TotalMilliseconds:F3} rowsMs={d1RowsMs:F3} getMs={d1GetTicks * d1TickMs:F3} applyMs={d1ApplyTicks * d1TickMs:F3} maxGetMs={d1MaxGetTicks * d1TickMs:F3} maxApplyMs={d1MaxApplyTicks * d1TickMs:F3} viewMs={d1ViewMs - d1RowsMs:F3} summaryMs={d1Total.Elapsed.TotalMilliseconds - d1ViewMs:F3}"));
        }

        #endregion

    }

    internal sealed class ImageQueueReviewAdapterContext
    {
        internal Func<LabelingProjectData> DataProvider { get; init; }
        internal ImageQualityReviewWorkflowService ImageQualityReviewWorkflowService { get; init; }
        internal AnomalyImageReviewSession AnomalyImageReviewSession { get; init; }
        internal ImageQueueSelectionService ImageQueueSelectionService { get; init; }
        internal IList<WpfImageQueueItem> ImageQueueItems { get; init; }
        internal IReadOnlyDictionary<string, WpfImageQueueItem> ImageQueueItemsByPath { get; init; }
        internal Func<ICollectionView> ImageQueueViewProvider { get; init; }
        internal WpfImageQueuePanelViewModel ImageQueueViewModel { get; init; }
        internal WpfObjectReviewPanelViewModel ObjectReviewViewModel { get; init; }
        internal ComboBox ImageQueueFilterBox { get; init; }
        internal TextBox ImageQueueSearchBox { get; init; }
        internal DataGrid ImageQueueGrid { get; init; }
        internal WpfImageQueuePanel ImageQueuePanelControl { get; init; }
        internal AnnotationDirtyState AnnotationDirtyState { get; init; }
        internal Func<IReadOnlyList<YoloWorkerSmokeCandidate>> PendingDetectionCandidatesProvider { get; init; }
        internal Func<string> ActiveImagePathProvider { get; init; }
        internal Func<System.Drawing.Size> ActiveImageSizeProvider { get; init; }
        internal Func<bool> IsApplicationCloseApproved { get; init; }
        internal Func<bool> IsImageQueueSelectionSuppressed { get; init; }
        internal Action<bool> SetImageQueueSelectionSuppressed { get; init; }
        internal System.Windows.Threading.Dispatcher Dispatcher { get; init; }
        internal Action<string> AppendLog { get; init; }
        internal Action<string> SetDatasetStatus { get; init; }
        internal Action<string> SetModelStatus { get; init; }
        internal Action<AnomalyImageReviewState?> SetAnomalyReviewSaveState { get; init; }
        internal Action RefreshImageQueueViewAfterItemStateChange { get; init; }
        internal Action ClearActiveImageAfterSearchNoMatch { get; init; }
        internal Func<bool> HasDisplayedInferenceResult { get; init; }
        internal Action RefreshInferenceResultPresentation { get; init; }
        internal Func<bool> TryOpenNextIncompleteQueueImage { get; init; }
        internal Func<WpfImageQueueItem, bool> CanOpenQueueItem { get; init; }
        internal Action<bool> SetOpenSelectedImageEnabled { get; init; }
        internal Action<WpfImageQueueItem, bool> OpenSelectedQueueImage { get; init; }
        internal Func<WpfImageQueueItem, bool> OpenAnomalyGridImage { get; init; }
        internal Func<string> CurrentRecipeNameProvider { get; init; }
        internal Func<string, RecipeConfigurationSaveResult> SaveCurrentRecipeConfiguration { get; init; }
        internal Action RefreshYoloTrainingStepCompletion { get; init; }
        internal Func<bool> IsLabelingStageActive { get; init; }
        internal Func<bool> IsOperationRunning { get; init; }
        internal Func<bool> HasWorkflowDraft { get; init; }
        internal Func<bool> IsAnnotationLoadBlocked { get; init; }
        internal Func<bool> HasCanvasLabelObjects { get; init; }
        internal Func<bool> IsNoObjectSelected { get; init; }
        internal Func<bool> SaveAnnotationsForReview { get; init; }
        internal Func<bool> DiscardUnsavedAnnotations { get; init; }
        internal Action SelectReviewTool { get; init; }
        internal Func<string, bool, string> ShowRejectionDialog { get; init; }
        internal Func<string, IReadOnlyList<WpfImageQueueItem>, bool> TryOpenNextReviewPendingImage { get; init; }
        internal InferenceResultReviewService ResultCache { get; init; }
        internal WpfCandidateReviewPanelViewModel CandidateReviewViewModel { get; init; }
        internal Func<string> ReviewScopeIdentityProvider { get; init; }
        internal Func<string> CurrentRecipeDirectoryProvider { get; init; }
        internal Func<string, string, double?> SavedAnomalyThresholdProvider { get; init; }
        internal Func<LabelSuspicionItem, bool> ApplyLabelSuspicionFix { get; init; }
        internal Func<IReadOnlyList<LabelSuspicionAnnotation>> CaptureLabelSuspicionPreview { get; init; }
        internal Action RedrawReviewOverlays { get; init; }
        internal Action ShowLabelSuspicionPanel { get; init; }
        internal Action SavedGroundTruthChanged { get; init; }
    }

}
