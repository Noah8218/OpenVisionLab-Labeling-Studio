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

            return ApplyDecision(
                request,
                ModelRegistryService.CandidateDecisionRejected,
                request.DecisionSummary,
                savedToRecipe: false,
                restoreBaselineBeforeSave: true);
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
            bool restoreBaselineBeforeSave)
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
                        ?? data.ProjectSettings.TrainingGuide.LastTrainingDatasetContentSha256);

                if (restoreBaselineBeforeSave
                    && !string.IsNullOrWhiteSpace(baselineWeightsPath)
                    && File.Exists(baselineWeightsPath))
                {
                    settings.WeightsPath = baselineWeightsPath;
                }
                else if (!restoreBaselineBeforeSave)
                {
                    settings.WeightsPath = candidateWeightsPath;
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
        Failed
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
                || Status == ModelCandidateLifecycleStatus.Failed;

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
    }
}
