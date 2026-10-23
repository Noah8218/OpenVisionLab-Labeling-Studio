using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    /// <summary>
    /// Connects the Shell's existing runtime state to the Window-free single-image
    /// detection service. The service owns worker execution and disposal; this
    /// adapter only supplies callbacks and admission rules.
    /// </summary>
    internal sealed class DetectionWorkflowAdapter
    {
        private readonly DetectionWorkflowAdapterContext context;

        internal DetectionWorkflowAdapter(DetectionWorkflowAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            ArgumentNullException.ThrowIfNull(context.SettingsAccessor);
            ArgumentNullException.ThrowIfNull(context.DetectionResultsAccessor);
            ArgumentNullException.ThrowIfNull(context.EnsureReadyAsync);
            ArgumentNullException.ThrowIfNull(context.TryStartDetection);
            ArgumentNullException.ThrowIfNull(context.WorkerFailureAccessor);
            ArgumentNullException.ThrowIfNull(context.RequestErrorAccessor);
            ArgumentNullException.ThrowIfNull(context.ActiveImagePathProvider);
            ArgumentNullException.ThrowIfNull(context.IsApplicationCloseApproved);
            ArgumentNullException.ThrowIfNull(context.IsBatchDetectionRunning);

            WorkflowService = new ImageDetectionWorkflowService(
                context.SettingsAccessor,
                context.DetectionResultsAccessor,
                context.EnsureReadyAsync,
                context.TryStartDetection,
                context.WorkerFailureAccessor,
                context.RequestErrorAccessor,
                trainingRunIdentityAccessor: context.TrainingRunIdentityAccessor,
                modelIdentityAccessor: context.ModelIdentityAccessor,
                supportedClassesAccessor: context.SupportedClassesAccessor);
        }

        internal ImageDetectionWorkflowService WorkflowService { get; }

        internal ImageDetectionCallbacks CreateImageDetectionCallbacks(PythonModelSettings referenceSettings = null)
        {
            return new ImageDetectionCallbacks
            {
                PrepareCanvasImage = (path, populateQueue) =>
                {
                    // Same-image inference must preserve in-progress manual labels.
                    bool shouldLoadTargetImage = !string.Equals(
                        path,
                        context.ActiveImagePathProvider(),
                        StringComparison.OrdinalIgnoreCase);
                    if (shouldLoadTargetImage && !context.TryLoadImage(path, populateQueue)) return null;
                    if (shouldLoadTargetImage) context.BeginInferenceReference?.Invoke(path, referenceSettings ?? context.SettingsAccessor());
                    return context.ActiveImageSizeProvider();
                },
                IsCurrentImage = path => AreSameImagePath(path, context.ActiveImagePathProvider()),
                ApplyCandidates = context.ApplyCandidates,
                IsResultOnly = context.IsResultOnly,
                RecordResult = context.RecordResult,
                RefreshActions = context.RefreshActions,
                SetPythonStatus = context.SetPythonStatus,
                SetPythonCompletedStatus = context.SetPythonCompletedStatus,
                SetCommandStatus = context.SetCommandStatus,
                SetInferenceStatus = context.SetInferenceStatus,
                AppendLog = context.AppendLog
            };
        }

        internal async Task RunInteractiveDetectionAsync(string imagePath = "", bool allowSmokeFallback = false)
        {
            if (context.IsApplicationCloseApproved() || context.IsBatchDetectionRunning())
            {
                return;
            }

            string referencePath = string.IsNullOrWhiteSpace(imagePath) ? context.ActiveImagePathProvider() : imagePath;
            bool trackReference = !WorkflowService.IsDetecting;
            if (trackReference) context.BeginInferenceReference?.Invoke(referencePath, context.SettingsAccessor());
            try
            {
                await WorkflowService.RunInteractiveAsync(
                    imagePath,
                    context.ActiveImagePathProvider(),
                    allowSmokeFallback,
                    CreateImageDetectionCallbacks(),
                    decisionEvaluation: true,
                    decisionOutputDirectory: CreateCaptureOutputDirectory()).ConfigureAwait(true);
            }
            finally
            {
                if (trackReference) context.FinishInferenceReference?.Invoke(referencePath);
            }
        }

        internal async Task RunTrialModelInteractiveDetectionAsync(
            string imagePath,
            PythonModelSettings trialModelSettings,
            string modelCandidateId)
        {
            if (context.IsApplicationCloseApproved() || context.IsBatchDetectionRunning())
            {
                return;
            }

            string referencePath = string.IsNullOrWhiteSpace(imagePath) ? context.ActiveImagePathProvider() : imagePath;
            bool trackReference = !WorkflowService.IsDetecting;
            if (trackReference) context.BeginInferenceReference?.Invoke(referencePath, trialModelSettings);
            try
            {
                await WorkflowService.RunInteractiveAsync(
                    imagePath,
                    context.ActiveImagePathProvider(),
                    allowSmokeFallback: false,
                    CreateImageDetectionCallbacks(trialModelSettings),
                    trialModelSettings,
                    modelCandidateId,
                    decisionEvaluation: true,
                    decisionOutputDirectory: CreateCaptureOutputDirectory()).ConfigureAwait(true);
            }
            finally
            {
                if (trackReference) context.FinishInferenceReference?.Invoke(referencePath);
            }
        }

        internal async Task<YoloWorkerSmokeTestResult> RunTrialModelDetectionForImageAsync(
            string imagePath,
            PythonModelSettings trialModelSettings,
            string modelCandidateId,
            CancellationToken cancellationToken)
        {
            string referencePath = string.IsNullOrWhiteSpace(imagePath) ? context.ActiveImagePathProvider() : imagePath;
            context.BeginInferenceReference?.Invoke(referencePath, trialModelSettings);
            try
            {
                return await WorkflowService.RunSmokeAsync(
                    imagePath,
                    applyToCanvas: false,
                    cancellationToken,
                    CreateImageDetectionCallbacks(trialModelSettings),
                    trialModelSettings,
                    modelCandidateId,
                    decisionEvaluation: true,
                    decisionOutputDirectory: CreateCaptureOutputDirectory()).ConfigureAwait(true);
            }
            finally
            {
                context.FinishInferenceReference?.Invoke(referencePath);
            }
        }

        internal static PythonModelSettings CreateTrialModelSettings(
            PythonModelSettings recipeSettings,
            WpfModelRegistryHistoryItem candidate,
            bool includeUnavailableForPresentation = false)
        {
            if (candidate == null || (!includeUnavailableForPresentation && !candidate.CanRunInferenceTrial))
            {
                return null;
            }

            bool usesCurrentRuntime = recipeSettings != null
                && string.Equals(PythonModelSettings.NormalizeModelEngine(candidate.ModelEngine), PythonModelSettings.NormalizeModelEngine(recipeSettings.ModelEngine), StringComparison.Ordinal)
                && string.Equals(candidate.ProjectRootPath?.Trim().TrimEnd('\\', '/'), recipeSettings.ProjectRootPath?.Trim().TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
            var settings = new PythonModelSettings
            {
                PythonExecutablePath = recipeSettings?.PythonExecutablePath ?? string.Empty,
                ModelEngine = PythonModelSettings.NormalizeModelEngine(candidate.ModelEngine),
                ProjectRootPath = candidate.ProjectRootPath ?? string.Empty,
                ClientScriptPath = usesCurrentRuntime ? recipeSettings.ClientScriptPath ?? string.Empty : string.Empty,
                WeightsPath = candidate.WeightsPath ?? string.Empty,
                ImageRootPath = recipeSettings?.ImageRootPath ?? string.Empty,
                MinimumDetectionConfidence = recipeSettings?.MinimumDetectionConfidence ?? 0.25F,
                MaximumDetectionCandidates = recipeSettings?.MaximumDetectionCandidates ?? 20,
                InferenceImageSize = recipeSettings?.InferenceImageSize ?? 320,
                DetectionTimeoutSeconds = recipeSettings?.DetectionTimeoutSeconds ?? 30,
                AutoStartClient = recipeSettings?.AutoStartClient ?? true
            };
            PythonModelRuntimePathResolver.ApplyPathDefaults(settings);
            return settings;
        }

        internal static PythonModelSettings CreateCandidateSourceSettings(LabelingProjectData data, InferenceResultReviewEntry entry)
        {
            InferenceResultSource source = entry?.Result?.Source;
            ModelRegistrySettings registry = data?.ProjectSettings?.ModelRegistry;
            if (source == null || registry?.Candidates == null || string.IsNullOrWhiteSpace(source.CandidateId)) return null;
            ModelCandidate candidate = registry.Candidates.FirstOrDefault(item => item?.CandidateId == source.CandidateId);
            if (candidate == null) return null;
            if (registry.CurrentInspectionModelId == candidate.CandidateId)
                return ImageDetectionWorkflowService.CloneSettings(data.ProjectSettings.PythonModel);
            ModelProfile profile = registry.Profiles?.FirstOrDefault(item => item?.ProfileId == candidate.ProfileId);
            if (profile == null) return null;
            return CreateTrialModelSettings(data.ProjectSettings.PythonModel, new WpfModelRegistryHistoryItem
            {
                CandidateId = candidate.CandidateId, ModelEngine = profile.ModelEngine,
                ProjectRootPath = profile.ProjectRootPath, WeightsPath = source.WeightsPath
            }, includeUnavailableForPresentation: true);
        }

        internal async Task<YoloWorkerSmokeTestResult> RunDetectionForImageAsync(
            string imagePath,
            bool applyToCanvas,
            CancellationToken cancellationToken)
        {
            string referencePath = string.IsNullOrWhiteSpace(imagePath) ? context.ActiveImagePathProvider() : imagePath;
            context.BeginInferenceReference?.Invoke(referencePath, context.SettingsAccessor());
            try
            {
                return await WorkflowService.RunSmokeAsync(
                    imagePath,
                    applyToCanvas,
                    cancellationToken,
                    CreateImageDetectionCallbacks(),
                    decisionEvaluation: true,
                    decisionOutputDirectory: CreateCaptureOutputDirectory()).ConfigureAwait(true);
            }
            finally
            {
                context.FinishInferenceReference?.Invoke(referencePath);
            }
        }

        internal async Task<YoloWorkerSmokeTestResult> RunWorkerDetectionForImageAsync(
            string imagePath,
            bool applyToCanvas,
            CancellationToken cancellationToken,
            int connectTimeoutMilliseconds = -1,
            bool workerReadyAlreadyChecked = false)
        {
            string referencePath = string.IsNullOrWhiteSpace(imagePath) ? context.ActiveImagePathProvider() : imagePath;
            context.BeginInferenceReference?.Invoke(referencePath, context.SettingsAccessor());
            try
            {
                return await WorkflowService.RunWorkerAsync(
                    imagePath,
                    applyToCanvas,
                    cancellationToken,
                    connectTimeoutMilliseconds,
                    CreateImageDetectionCallbacks(),
                    workerReadyAlreadyChecked).ConfigureAwait(true);
            }
            finally
            {
                context.FinishInferenceReference?.Invoke(referencePath);
            }
        }

        private static bool AreSameImagePath(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            }

            try
            {
                return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
            }
        }

        private string CreateCaptureOutputDirectory()
        {
            string root = context.EvidenceOutputDirectoryProvider?.Invoke();
            if (string.IsNullOrWhiteSpace(root)) return string.Empty;
            string path = Path.Combine(root, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }
    }

    internal sealed class DetectionWorkflowAdapterContext
    {
        internal Func<PythonModelSettings> SettingsAccessor { get; init; }
        internal Func<PythonModelSettings, string> TrainingRunIdentityAccessor { get; init; }
        internal Func<PythonModelSettings, string> ModelIdentityAccessor { get; init; }
        internal Func<IReadOnlyList<LabelClass>> SupportedClassesAccessor { get; init; }
        internal Func<string> EvidenceOutputDirectoryProvider { get; init; }
        internal Func<DetectionResultApplicationService> DetectionResultsAccessor { get; init; }
        internal Func<int, CancellationToken, Task<bool>> EnsureReadyAsync { get; init; }
        internal Func<bool, string, Size, bool> TryStartDetection { get; init; }
        internal Func<string> WorkerFailureAccessor { get; init; }
        internal Func<string> RequestErrorAccessor { get; init; }
        internal Func<string> ActiveImagePathProvider { get; init; }
        internal Func<Size> ActiveImageSizeProvider { get; init; }
        internal Func<string, bool, bool> TryLoadImage { get; init; }
        internal Action<IReadOnlyList<YoloWorkerSmokeCandidate>, bool> ApplyCandidates { get; init; }
        internal Func<bool> IsResultOnly { get; init; }
        internal Action<string, PythonModelSettings> BeginInferenceReference { get; init; }
        internal Action<string> FinishInferenceReference { get; init; }
        internal Action<YoloWorkerSmokeTestResult> RecordResult { get; init; }
        internal Action RefreshActions { get; init; }
        internal Action<string> SetPythonStatus { get; init; }
        internal Action<string, int> SetPythonCompletedStatus { get; init; }
        internal Action<string, bool> SetCommandStatus { get; init; }
        internal Action<string, bool, bool> SetInferenceStatus { get; init; }
        internal Action<string> AppendLog { get; init; }
        internal Func<bool> IsApplicationCloseApproved { get; init; }
        internal Func<bool> IsBatchDetectionRunning { get; init; }
    }

}
