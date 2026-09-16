using System;
using System.Collections.Generic;

namespace MvcVisionSystem
{
    /// <summary>
    /// Keeps in-memory ModelRegistry state aligned with a later Recipe save.
    /// Python model settings remain staged outside this transaction so a failed
    /// save can be retried without losing the selected candidate path.
    /// </summary>
    public sealed class ModelRegistryStateTransaction : IDisposable
    {
        private readonly ModelRegistrySettings registry;
        private readonly ModelRegistrySettings snapshot;
        private bool completed;

        public ModelRegistryStateTransaction(ModelRegistrySettings registry)
        {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            this.registry.EnsureDefaults();
            snapshot = Clone(this.registry);
        }

        public void Commit()
        {
            completed = true;
        }

        public void Rollback()
        {
            if (completed)
            {
                return;
            }

            Restore(registry, snapshot);
            completed = true;
        }

        public void Dispose()
        {
            Rollback();
        }

        private static ModelRegistrySettings Clone(ModelRegistrySettings source)
        {
            source.EnsureDefaults();
            return new ModelRegistrySettings
            {
                SchemaVersion = source.SchemaVersion,
                CurrentProfileId = source.CurrentProfileId,
                LatestTrainingRunId = source.LatestTrainingRunId,
                LatestCandidateId = source.LatestCandidateId,
                CurrentInspectionModelId = source.CurrentInspectionModelId,
                Profiles = CloneList(source.Profiles, Clone),
                TrainingRuns = CloneList(source.TrainingRuns, Clone),
                Candidates = CloneList(source.Candidates, Clone),
                CandidateDecisions = CloneList(source.CandidateDecisions, Clone),
                AdoptionHistory = CloneList(source.AdoptionHistory, Clone)
            };
        }

        private static void Restore(ModelRegistrySettings target, ModelRegistrySettings source)
        {
            target.EnsureDefaults();
            target.SchemaVersion = source.SchemaVersion;
            target.CurrentProfileId = source.CurrentProfileId;
            target.LatestTrainingRunId = source.LatestTrainingRunId;
            target.LatestCandidateId = source.LatestCandidateId;
            target.CurrentInspectionModelId = source.CurrentInspectionModelId;
            ReplaceList(target.Profiles, source.Profiles, Clone);
            ReplaceList(target.TrainingRuns, source.TrainingRuns, Clone);
            ReplaceList(target.Candidates, source.Candidates, Clone);
            ReplaceList(target.CandidateDecisions, source.CandidateDecisions, Clone);
            ReplaceList(target.AdoptionHistory, source.AdoptionHistory, Clone);
        }

        private static List<T> CloneList<T>(IEnumerable<T> source, Func<T, T> clone)
        {
            var result = new List<T>();
            if (source == null)
            {
                return result;
            }

            foreach (T item in source)
            {
                result.Add(clone(item));
            }

            return result;
        }

        private static void ReplaceList<T>(List<T> target, IEnumerable<T> source, Func<T, T> clone)
        {
            target.Clear();
            foreach (T item in CloneList(source, clone))
            {
                target.Add(item);
            }
        }

        private static ModelProfile Clone(ModelProfile source)
        {
            return source == null
                ? null
                : new ModelProfile
                {
                    ProfileId = source.ProfileId,
                    DisplayName = source.DisplayName,
                    AdapterKey = source.AdapterKey,
                    ModelEngine = source.ModelEngine,
                    DatasetPurpose = source.DatasetPurpose,
                    ProjectRootPath = source.ProjectRootPath,
                    CreatedUtc = source.CreatedUtc,
                    LastUsedUtc = source.LastUsedUtc
                };
        }

        private static TrainingRun Clone(TrainingRun source)
        {
            return source == null
                ? null
                : new TrainingRun
                {
                    TrainingRunId = source.TrainingRunId,
                    RunName = source.RunName,
                    ProfileId = source.ProfileId,
                    EventUtc = source.EventUtc,
                    OutputRootPath = source.OutputRootPath,
                    State = source.State,
                    ProgressPercent = source.ProgressPercent,
                    Message = source.Message,
                    CandidateWeightsPath = source.CandidateWeightsPath,
                    WeightsSha256 = source.WeightsSha256,
                    ArtifactPath = source.ArtifactPath,
                    ArtifactStatus = source.ArtifactStatus,
                    BaselineWeightsPath = source.BaselineWeightsPath,
                    MetricsSummary = source.MetricsSummary,
                    DatasetVersionId = source.DatasetVersionId,
                    DatasetContentSha256 = source.DatasetContentSha256
                };
        }

        private static ModelCandidate Clone(ModelCandidate source)
        {
            return source == null
                ? null
                : new ModelCandidate
                {
                    CandidateId = source.CandidateId,
                    ProfileId = source.ProfileId,
                    TrainingRunId = source.TrainingRunId,
                    WeightsPath = source.WeightsPath,
                    WeightsSha256 = source.WeightsSha256,
                    ArtifactPath = source.ArtifactPath,
                    ArtifactStatus = source.ArtifactStatus,
                    BaselineWeightsPath = source.BaselineWeightsPath,
                    MetricsSummary = source.MetricsSummary,
                    CreatedUtc = source.CreatedUtc,
                    LastSeenUtc = source.LastSeenUtc,
                    SavedToRecipe = source.SavedToRecipe,
                    IsCurrentInspectionModel = source.IsCurrentInspectionModel,
                    Decision = source.Decision,
                    DecisionUtc = source.DecisionUtc,
                    DecisionSummary = source.DecisionSummary
                };
        }

        private static ModelCandidateDecision Clone(ModelCandidateDecision source)
        {
            return source == null
                ? null
                : new ModelCandidateDecision
                {
                    DecisionId = source.DecisionId,
                    ProfileId = source.ProfileId,
                    CandidateId = source.CandidateId,
                    WeightsPath = source.WeightsPath,
                    WeightsSha256 = source.WeightsSha256,
                    ArtifactPath = source.ArtifactPath,
                    PreviousWeightsPath = source.PreviousWeightsPath,
                    Decision = source.Decision,
                    DecidedUtc = source.DecidedUtc,
                    SavedToRecipe = source.SavedToRecipe,
                    MetricsSummary = source.MetricsSummary,
                    DecisionSummary = source.DecisionSummary
                };
        }

        private static InspectionModelAdoption Clone(InspectionModelAdoption source)
        {
            return source == null
                ? null
                : new InspectionModelAdoption
                {
                    AdoptionId = source.AdoptionId,
                    ProfileId = source.ProfileId,
                    CandidateId = source.CandidateId,
                    WeightsPath = source.WeightsPath,
                    WeightsSha256 = source.WeightsSha256,
                    ArtifactPath = source.ArtifactPath,
                    PreviousWeightsPath = source.PreviousWeightsPath,
                    AdoptedUtc = source.AdoptedUtc,
                    SavedToRecipe = source.SavedToRecipe,
                    DecisionSummary = source.DecisionSummary
                };
        }
    }
}
