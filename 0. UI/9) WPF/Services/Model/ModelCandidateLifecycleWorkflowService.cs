using MvcVisionSystem._1._Core;
using System;
using System.IO;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the mutable lifecycle of a trained model candidate after the Shell
    /// has collected an explicit user decision. The WPF Shell supplies the
    /// model-metadata Recipe save callback, but this owner keeps Registry and
    /// Recipe state in one rollback boundary.
    /// </summary>
    public sealed class ModelCandidateLifecycleWorkflowService
    {
        public ModelCandidateLifecycleResult Reject(ModelCandidateLifecycleRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (!request.HasPendingCandidate || string.IsNullOrWhiteSpace(request.CandidateWeightsPath))
            {
                return ModelCandidateLifecycleResult.MissingCandidate();
            }

            string candidateWeightsPath = Normalize(request.CandidateWeightsPath);
            string baselineWeightsPath = Normalize(request.BaselineWeightsPath);
            if (!TryOpenBaselineForReject(baselineWeightsPath, out FileStream baselineStream, out Exception baselineError))
            {
                return ModelCandidateLifecycleResult.BaselineUnavailable(
                    candidateWeightsPath,
                    baselineWeightsPath,
                    baselineError);
            }

            using (baselineStream)
            {
                return ApplyDecision(
                    request,
                    ModelRegistryService.CandidateDecisionRejected,
                    request.DecisionSummary,
                    savedToRecipe: false,
                    restoreBaselineBeforeSave: true,
                    baselineStream: baselineStream);
            }
        }

        private static bool TryOpenBaselineForReject(
            string baselineWeightsPath,
            out FileStream baselineStream,
            out Exception error)
        {
            baselineStream = null;
            if (string.IsNullOrWhiteSpace(baselineWeightsPath))
            {
                error = new InvalidOperationException("기존 검사 모델 경로가 없어 후보를 기각하지 않았습니다.");
                return false;
            }

            try
            {
                baselineStream = new FileStream(
                    baselineWeightsPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read);
                error = null;
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
            {
                error = new InvalidOperationException("기존 검사 모델을 복원할 수 없어 후보를 기각하지 않았습니다.", ex);
                return false;
            }
        }

        public ModelCandidateLifecycleResult Adopt(ModelCandidateLifecycleRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (request.AdoptionPlan == null)
            {
                return ModelCandidateLifecycleResult.InvalidAdoptionPlan();
            }

            if (request.AdoptionPlan.IsAlreadyCurrent)
            {
                return ModelCandidateLifecycleResult.AlreadyCurrent(request.AdoptionPlan.CandidateWeightsPath);
            }

            if (!request.AdoptionPlan.IsReady)
            {
                return ModelCandidateLifecycleResult.InvalidAdoptionPlan();
            }

            return ApplyDecision(
                request,
                ModelRegistryService.CandidateDecisionAdopted,
                request.AdoptionPlan.DecisionSummary,
                savedToRecipe: true,
                restoreBaselineBeforeSave: false);
        }

        private static ModelCandidateLifecycleResult ApplyDecision(
            ModelCandidateLifecycleRequest request,
            string decision,
            string decisionSummary,
            bool savedToRecipe,
            bool restoreBaselineBeforeSave,
            FileStream baselineStream = null)
        {
            LabelingProjectData data = request.Data ?? throw new ArgumentNullException(nameof(request.Data));
            data.ProjectSettings ??= new LabelingProjectSettings();
            data.ProjectSettings.EnsureDefaults();
            PythonModelSettings settings = data.ProjectSettings.PythonModel;
            string candidateWeightsPath = Normalize(request.CandidateWeightsPath);
            string baselineWeightsPath = Normalize(request.BaselineWeightsPath);

            try
            {
                using RecipeSettingsStateTransaction recipeSettingsTransaction = new RecipeSettingsStateTransaction(data);
                using ModelRegistryStateTransaction registryTransaction = new ModelRegistryStateTransaction(data.ProjectSettings.ModelRegistry);

                ModelRegistryService.RecordCandidateDecision(
                    data.ProjectSettings.ModelRegistry,
                    settings,
                    data.ProjectSettings.DatasetPurpose,
                    data.OutputRootPath,
                    candidateWeightsPath,
                    baselineWeightsPath,
                    request.MetricsSummary,
                    decision,
                    decisionSummary,
                    savedToRecipe,
                    request.AdoptionPlan?.DatasetVersionId
                        ?? data.ProjectSettings.TrainingGuide.LastTrainingDatasetVersionId,
                    request.AdoptionPlan?.DatasetContentSha256
                        ?? data.ProjectSettings.TrainingGuide.LastTrainingDatasetContentSha256,
                    trainingRunId: data.ProjectSettings.TrainingGuide.LastTrainingRunId,
                    trainingRunName: data.ProjectSettings.TrainingGuide.LastTrainingRunName,
                    candidateWeightsSha256: request.AdoptionPlan?.WeightsSha256,
                    candidateArtifactPath: request.AdoptionPlan?.ArtifactPath);

                if (restoreBaselineBeforeSave)
                {
                    if (baselineStream == null)
                    {
                        throw new InvalidOperationException("기존 검사 모델 복원 확인이 없어 후보를 기각하지 않았습니다.");
                    }

                    settings.WeightsPath = baselineWeightsPath;
                }
                else if (!restoreBaselineBeforeSave)
                {
                    settings.WeightsPath = Normalize(request.AdoptionPlan?.ArtifactPath);
                    if (string.IsNullOrWhiteSpace(settings.WeightsPath))
                    {
                        settings.WeightsPath = candidateWeightsPath;
                    }
                }

                bool configSaved = request.SaveModelMetadata?.Invoke() == true;
                if (configSaved)
                {
                    recipeSettingsTransaction.Commit();
                    registryTransaction.Commit();
                    return ModelCandidateLifecycleResult.Committed(
                        candidateWeightsPath,
                        baselineWeightsPath,
                        savedToRecipe);
                }

                recipeSettingsTransaction.Rollback();
                registryTransaction.Rollback();
                settings.WeightsPath = candidateWeightsPath;
                return ModelCandidateLifecycleResult.PersistenceFailed(
                    candidateWeightsPath,
                    baselineWeightsPath,
                    savedToRecipe);
            }
            catch (Exception ex)
            {
                settings.WeightsPath = candidateWeightsPath;
                return ModelCandidateLifecycleResult.Failed(
                    candidateWeightsPath,
                    baselineWeightsPath,
                    savedToRecipe,
                    ex);
            }
        }

        private static string Normalize(string value)
            => value?.Trim() ?? string.Empty;
    }

    public sealed class ModelCandidateLifecycleRequest
    {
        public LabelingProjectData Data { get; set; }

        public bool HasPendingCandidate { get; set; }

        public string CandidateWeightsPath { get; set; } = string.Empty;

        public string BaselineWeightsPath { get; set; } = string.Empty;

        public string MetricsSummary { get; set; } = string.Empty;

        public string DecisionSummary { get; set; } = string.Empty;

        public ModelHistoryAdoptionPlan AdoptionPlan { get; set; }

        public Func<bool> SaveModelMetadata { get; set; }
    }

    public enum ModelCandidateLifecycleStatus
    {
        MissingCandidate,
        InvalidAdoptionPlan,
        AlreadyCurrent,
        PersistenceFailed,
        Committed,
        Failed,
        BaselineUnavailable
    }

    public sealed class ModelCandidateLifecycleResult
    {
        private ModelCandidateLifecycleResult(
            ModelCandidateLifecycleStatus status,
            string candidateWeightsPath,
            string baselineWeightsPath,
            bool savedToRecipe,
            Exception error = null)
        {
            Status = status;
            CandidateWeightsPath = candidateWeightsPath ?? string.Empty;
            BaselineWeightsPath = baselineWeightsPath ?? string.Empty;
            SavedToRecipe = savedToRecipe;
            Error = error;
        }

        public ModelCandidateLifecycleStatus Status { get; }

        public string CandidateWeightsPath { get; }

        public string BaselineWeightsPath { get; }

        public bool SavedToRecipe { get; }

        public Exception Error { get; }

        public bool IsCommitted => Status == ModelCandidateLifecycleStatus.Committed;

        public bool ShouldRetainPendingCandidate
            => Status == ModelCandidateLifecycleStatus.PersistenceFailed
                || Status == ModelCandidateLifecycleStatus.Failed
                || Status == ModelCandidateLifecycleStatus.BaselineUnavailable;

        public static ModelCandidateLifecycleResult MissingCandidate()
            => new ModelCandidateLifecycleResult(ModelCandidateLifecycleStatus.MissingCandidate, string.Empty, string.Empty, false);

        public static ModelCandidateLifecycleResult InvalidAdoptionPlan()
            => new ModelCandidateLifecycleResult(ModelCandidateLifecycleStatus.InvalidAdoptionPlan, string.Empty, string.Empty, true);

        public static ModelCandidateLifecycleResult AlreadyCurrent(string candidateWeightsPath)
            => new ModelCandidateLifecycleResult(ModelCandidateLifecycleStatus.AlreadyCurrent, candidateWeightsPath, string.Empty, true);

        public static ModelCandidateLifecycleResult PersistenceFailed(
            string candidateWeightsPath,
            string baselineWeightsPath,
            bool savedToRecipe)
            => new ModelCandidateLifecycleResult(
                ModelCandidateLifecycleStatus.PersistenceFailed,
                candidateWeightsPath,
                baselineWeightsPath,
                savedToRecipe);

        public static ModelCandidateLifecycleResult Committed(
            string candidateWeightsPath,
            string baselineWeightsPath,
            bool savedToRecipe)
            => new ModelCandidateLifecycleResult(
                ModelCandidateLifecycleStatus.Committed,
                candidateWeightsPath,
                baselineWeightsPath,
                savedToRecipe);

        public static ModelCandidateLifecycleResult Failed(
            string candidateWeightsPath,
            string baselineWeightsPath,
            bool savedToRecipe,
            Exception error)
            => new ModelCandidateLifecycleResult(
                ModelCandidateLifecycleStatus.Failed,
                candidateWeightsPath,
                baselineWeightsPath,
                savedToRecipe,
                error);

        public static ModelCandidateLifecycleResult BaselineUnavailable(
            string candidateWeightsPath,
            string baselineWeightsPath,
            Exception error)
            => new ModelCandidateLifecycleResult(
                ModelCandidateLifecycleStatus.BaselineUnavailable,
                candidateWeightsPath,
                baselineWeightsPath,
                false,
                error);
    }
}
