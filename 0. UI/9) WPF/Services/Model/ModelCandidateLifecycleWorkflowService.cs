using MvcVisionSystem._1._Core;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

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

            try
            {
                string target = string.IsNullOrWhiteSpace(request.AdoptionPlan.ArtifactPath) ? request.AdoptionPlan.CandidateWeightsPath : request.AdoptionPlan.ArtifactPath;
                using FileStream modelStream = new(target, FileMode.Open, FileAccess.Read, FileShare.Read);
                string expectedHash = request.AdoptionPlan.WeightsSha256;
                string adoptedHash = Convert.ToHexString(SHA256.HashData(modelStream));
                if (!string.IsNullOrWhiteSpace(expectedHash) && !string.Equals(adoptedHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Model artifact changed after adoption was prepared.");
                ModelCandidate current = ModelRegistryService.FindCurrentInspectionModel(request.Data?.ProjectSettings?.ModelRegistry);
                if (SamePath(target, request.Data?.ProjectSettings?.PythonModel?.WeightsPath) && current != null
                    && (SamePath(current.WeightsPath, target) || SamePath(current.ArtifactPath, target)))
                    return ModelCandidateLifecycleResult.AlreadyCurrent(request.AdoptionPlan.CandidateWeightsPath);
                string previousPath = Normalize(request.AdoptionPlan.BaselineWeightsPath);
                if (previousPath.Length == 0) previousPath = Normalize(request.Data?.ProjectSettings?.PythonModel?.WeightsPath);
                TryOpenBaselineForReject(previousPath, out FileStream previousStream, out _);
                using (previousStream)
                {
                    string previousHash = previousStream == null ? string.Empty : Convert.ToHexString(SHA256.HashData(previousStream));
                    return ApplyDecision(request, ModelRegistryService.CandidateDecisionAdopted,
                        string.IsNullOrWhiteSpace(request.DecisionSummary) ? request.AdoptionPlan.DecisionSummary : request.DecisionSummary.Trim(),
                        savedToRecipe: true, restoreBaselineBeforeSave: false, previousWeightsSha256: previousHash,
                        adoptedWeightsSha256: adoptedHash, adoptionPreviousWeightsPath: previousPath);
                }
            }
            catch (Exception ex)
            {
                return ModelCandidateLifecycleResult.Failed(request.CandidateWeightsPath, request.BaselineWeightsPath, true, ex);
            }
        }

        public static InspectionModelAdoption FindUndoableAdoption(LabelingProjectData data)
        {
            ModelRegistrySettings registry = data?.ProjectSettings?.ModelRegistry;
            InspectionModelAdoption adoption = ModelRegistryService.FindLatestInspectionModelAdoption(registry);
            if (adoption == null || !string.IsNullOrEmpty(adoption.UndoneUtc) || registry.CurrentInspectionModelId != adoption.CandidateId
                || !SamePath(data.ProjectSettings.PythonModel?.WeightsPath, adoption.AppliedWeightsPath)
                || !IsSha256(adoption.WeightsSha256) || !IsSha256(adoption.PreviousWeightsSha256)
                || adoption.PreviousCandidateId == adoption.CandidateId) return null;
            ModelProfile profile = registry.Profiles?.FirstOrDefault(item => item.ProfileId == adoption.ProfileId);
            PythonModelSettings settings = data.ProjectSettings.PythonModel;
            if (profile == null || PythonModelSettings.NormalizeModelEngine(profile.ModelEngine) != PythonModelSettings.NormalizeModelEngine(settings.ModelEngine)
                || profile.DatasetPurpose != data.ProjectSettings.DatasetPurpose.ToString()
                || !string.Equals(Normalize(profile.ProjectRootPath).TrimEnd('\\', '/'), Normalize(settings.ProjectRootPath).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)) return null;
            ModelCandidate previous = registry.Candidates.FirstOrDefault(item => item.CandidateId == adoption.PreviousCandidateId && item.ProfileId == adoption.ProfileId);
            return previous != null && (string.IsNullOrWhiteSpace(previous.WeightsSha256) || string.Equals(previous.WeightsSha256, adoption.PreviousWeightsSha256, StringComparison.OrdinalIgnoreCase))
                && (SamePath(previous.WeightsPath, adoption.PreviousWeightsPath) || SamePath(previous.ArtifactPath, adoption.PreviousWeightsPath))
                ? adoption : null;
        }

        public ModelCandidateLifecycleResult UndoAdoption(ModelAdoptionUndoRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            InspectionModelAdoption adoption = FindUndoableAdoption(request.Data);
            if (adoption == null || adoption.AdoptionId != request.AdoptionId)
                return ModelCandidateLifecycleResult.UndoUnavailable(new InvalidOperationException("현재 지정과 일치하는 되돌리기 기록이 없습니다."));
            PythonModelSettings settings = request.Data.ProjectSettings.PythonModel;
            try
            {
                using FileStream currentStream = new(adoption.AppliedWeightsPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using FileStream previousStream = new(adoption.PreviousWeightsPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (!string.Equals(Convert.ToHexString(SHA256.HashData(currentStream)), adoption.WeightsSha256, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(Convert.ToHexString(SHA256.HashData(previousStream)), adoption.PreviousWeightsSha256, StringComparison.OrdinalIgnoreCase))
                    return ModelCandidateLifecycleResult.UndoUnavailable(new InvalidDataException("검사 모델 파일이 바뀌어 되돌리지 않았습니다."));
                using RecipeSettingsStateTransaction recipeTransaction = new(request.Data);
                using ModelRegistryStateTransaction registryTransaction = new(request.Data.ProjectSettings.ModelRegistry);
                if (!ModelRegistryService.RestoreInspectionModelAdoption(request.Data.ProjectSettings.ModelRegistry, adoption, request.Reason))
                    return ModelCandidateLifecycleResult.UndoUnavailable(new InvalidOperationException("되돌리기 대상 모델 기록이 바뀌었습니다."));
                settings.WeightsPath = adoption.PreviousWeightsPath;
                if (request.SaveModelMetadata?.Invoke() != true)
                    return ModelCandidateLifecycleResult.PersistenceFailed(adoption.PreviousWeightsPath, adoption.AppliedWeightsPath, true);
                recipeTransaction.Commit();
                registryTransaction.Commit();
                return ModelCandidateLifecycleResult.Committed(adoption.PreviousWeightsPath, adoption.AppliedWeightsPath, true);
            }
            catch (Exception ex)
            {
                return ModelCandidateLifecycleResult.Failed(adoption.PreviousWeightsPath, adoption.AppliedWeightsPath, true, ex);
            }
        }

        private static bool IsSha256(string value) => value?.Length == 64 && value.All(Uri.IsHexDigit);
        private static bool SamePath(string left, string right)
            => !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

        private static ModelCandidateLifecycleResult ApplyDecision(
            ModelCandidateLifecycleRequest request,
            string decision,
            string decisionSummary,
            bool savedToRecipe,
            bool restoreBaselineBeforeSave,
            FileStream baselineStream = null,
            string previousWeightsSha256 = "",
            string adoptedWeightsSha256 = "",
            string adoptionPreviousWeightsPath = "")
        {
            LabelingProjectData data = request.Data ?? throw new ArgumentNullException(nameof(request.Data));
            data.ProjectSettings ??= new LabelingProjectSettings();
            data.ProjectSettings.EnsureDefaults();
            PythonModelSettings settings = data.ProjectSettings.PythonModel;
            string candidateWeightsPath = Normalize(request.CandidateWeightsPath);
            string baselineWeightsPath = Normalize(request.BaselineWeightsPath);
            if (savedToRecipe && !string.IsNullOrWhiteSpace(adoptionPreviousWeightsPath)) baselineWeightsPath = adoptionPreviousWeightsPath;
            string previousSettingsWeightsPath = settings.WeightsPath;

            try
            {
                using RecipeSettingsStateTransaction recipeSettingsTransaction = new RecipeSettingsStateTransaction(data);
                using ModelRegistryStateTransaction registryTransaction = new ModelRegistryStateTransaction(data.ProjectSettings.ModelRegistry);

                ModelCandidate candidate = ModelRegistryService.RecordCandidateDecision(
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
                    trainingRunId: request.TrainingRunId ?? data.ProjectSettings.TrainingGuide.LastTrainingRunId,
                    trainingRunName: request.TrainingRunName ?? data.ProjectSettings.TrainingGuide.LastTrainingRunName,
                    candidateWeightsSha256: string.IsNullOrWhiteSpace(adoptedWeightsSha256) ? request.AdoptionPlan?.WeightsSha256 : adoptedWeightsSha256,
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

                if (savedToRecipe)
                {
                    InspectionModelAdoption adoption = ModelRegistryService.FindLatestInspectionModelAdoption(data.ProjectSettings.ModelRegistry);
                    if (adoption?.CandidateId == candidate?.CandidateId)
                    {
                        adoption.AppliedWeightsPath = settings.WeightsPath;
                        adoption.PreviousWeightsSha256 = previousWeightsSha256;
                        adoption.PreviousCandidateId = ModelRegistryService.FindInspectionModelByPath(data.ProjectSettings.ModelRegistry, baselineWeightsPath, candidate.ProfileId)?.CandidateId ?? string.Empty;
                        adoption.EvaluationDatasetVersionId = request.EvaluationDatasetVersionId ?? string.Empty;
                        adoption.EvaluationDatasetContentSha256 = request.EvaluationDatasetContentSha256 ?? string.Empty;
                        adoption.EvaluationReportPath = request.EvaluationReportPath ?? string.Empty;
                        adoption.EvaluationReportSha256 = request.EvaluationReportSha256 ?? string.Empty;
                        adoption.EvaluationConditions = request.EvaluationConditions ?? string.Empty;
                        adoption.EvaluationMetricsSummary = request.EvaluationMetricsSummary ?? string.Empty;
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
                settings.WeightsPath = previousSettingsWeightsPath;
                return ModelCandidateLifecycleResult.PersistenceFailed(
                    candidateWeightsPath,
                    baselineWeightsPath,
                    savedToRecipe);
            }
            catch (Exception ex)
            {
                settings.WeightsPath = previousSettingsWeightsPath;
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

        public string TrainingRunId { get; set; }

        public string TrainingRunName { get; set; }

        public string EvaluationDatasetVersionId { get; set; } = string.Empty;
        public string EvaluationDatasetContentSha256 { get; set; } = string.Empty;
        public string EvaluationReportPath { get; set; } = string.Empty;
        public string EvaluationReportSha256 { get; set; } = string.Empty;
        public string EvaluationConditions { get; set; } = string.Empty;
        public string EvaluationMetricsSummary { get; set; } = string.Empty;

        public ModelHistoryAdoptionPlan AdoptionPlan { get; set; }

        public Func<bool> SaveModelMetadata { get; set; }
    }

    public sealed class ModelAdoptionUndoRequest
    {
        public LabelingProjectData Data { get; set; }
        public string AdoptionId { get; set; } = string.Empty;
        public string Reason { get; set; } = "검사 모델 지정을 되돌림";
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
        BaselineUnavailable,
        UndoUnavailable
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

        public static ModelCandidateLifecycleResult UndoUnavailable(Exception error)
            => new ModelCandidateLifecycleResult(ModelCandidateLifecycleStatus.UndoUnavailable, string.Empty, string.Empty, false, error);

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
