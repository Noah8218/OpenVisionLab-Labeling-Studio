using System;
using System.Collections.Generic;

namespace MvcVisionSystem
{
    public class ModelRegistrySettings
    {
        public int SchemaVersion { get; set; } = 1;

        public string CurrentProfileId { get; set; } = "";

        public string LatestTrainingRunId { get; set; } = "";

        public string LatestCandidateId { get; set; } = "";

        public string CurrentInspectionModelId { get; set; } = "";

        public List<ModelProfile> Profiles { get; set; } = new List<ModelProfile>();

        public List<TrainingRun> TrainingRuns { get; set; } = new List<TrainingRun>();

        public List<ModelCandidate> Candidates { get; set; } = new List<ModelCandidate>();

        public List<ModelCandidateDecision> CandidateDecisions { get; set; } = new List<ModelCandidateDecision>();

        public List<InspectionModelAdoption> AdoptionHistory { get; set; } = new List<InspectionModelAdoption>();

        public void EnsureDefaults()
        {
            SchemaVersion = Math.Max(1, SchemaVersion);
            Profiles ??= new List<ModelProfile>();
            TrainingRuns ??= new List<TrainingRun>();
            Candidates ??= new List<ModelCandidate>();
            CandidateDecisions ??= new List<ModelCandidateDecision>();
            AdoptionHistory ??= new List<InspectionModelAdoption>();
        }
    }

    public class ModelProfile
    {
        public string ProfileId { get; set; } = "";

        public string DisplayName { get; set; } = "";

        public string AdapterKey { get; set; } = "";

        public string ModelEngine { get; set; } = "";

        public string DatasetPurpose { get; set; } = "";

        public string ProjectRootPath { get; set; } = "";

        public string CreatedUtc { get; set; } = "";

        public string LastUsedUtc { get; set; } = "";
    }

    public class TrainingRun
    {
        public string TrainingRunId { get; set; } = "";

        public string RunName { get; set; } = "";

        public string ProfileId { get; set; } = "";

        public string EventUtc { get; set; } = "";

        public string OutputRootPath { get; set; } = "";

        public string State { get; set; } = "";

        public int ProgressPercent { get; set; } = -1;

        public string Message { get; set; } = "";

        public string CandidateWeightsPath { get; set; } = "";

        public string WeightsSha256 { get; set; } = "";

        public string ArtifactPath { get; set; } = "";

        public string ArtifactStatus { get; set; } = "";

        public string BaselineWeightsPath { get; set; } = "";

        public string MetricsSummary { get; set; } = "";

        public string DatasetVersionId { get; set; } = "";

        public string DatasetContentSha256 { get; set; } = "";
    }

    public class ModelCandidate
    {
        public string CandidateId { get; set; } = "";

        public string ProfileId { get; set; } = "";

        public string TrainingRunId { get; set; } = "";

        public string WeightsPath { get; set; } = "";

        public string WeightsSha256 { get; set; } = "";

        public string ArtifactPath { get; set; } = "";

        public string ArtifactStatus { get; set; } = "";

        public string BaselineWeightsPath { get; set; } = "";

        public string MetricsSummary { get; set; } = "";

        public string CreatedUtc { get; set; } = "";

        public string LastSeenUtc { get; set; } = "";

        public bool SavedToRecipe { get; set; }

        public bool IsCurrentInspectionModel { get; set; }

        public string Decision { get; set; } = "";

        public string DecisionUtc { get; set; } = "";

        public string DecisionSummary { get; set; } = "";
    }

    public class ModelCandidateDecision
    {
        public string DecisionId { get; set; } = "";

        public string ProfileId { get; set; } = "";

        public string CandidateId { get; set; } = "";

        public string WeightsPath { get; set; } = "";

        public string WeightsSha256 { get; set; } = "";

        public string ArtifactPath { get; set; } = "";

        public string PreviousWeightsPath { get; set; } = "";

        public string Decision { get; set; } = "";

        public string DecidedUtc { get; set; } = "";

        public bool SavedToRecipe { get; set; }

        public string MetricsSummary { get; set; } = "";

        public string DecisionSummary { get; set; } = "";
    }

    public class InspectionModelAdoption
    {
        public string AdoptionId { get; set; } = "";

        public string ProfileId { get; set; } = "";

        public string CandidateId { get; set; } = "";

        public string WeightsPath { get; set; } = "";

        public string WeightsSha256 { get; set; } = "";

        public string ArtifactPath { get; set; } = "";

        public string PreviousWeightsPath { get; set; } = "";

        public string AdoptedUtc { get; set; } = "";

        public bool SavedToRecipe { get; set; }

        public string DecisionSummary { get; set; } = "";
    }
}
