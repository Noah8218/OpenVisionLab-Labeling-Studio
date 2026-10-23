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
        public TrainingRunParameters Parameters { get; set; }
        public ModelDecisionRule DecisionRule { get; set; }
        public ModelDecisionRule PreviousDecisionRule { get; set; }
        public bool DecisionRuleUndoAvailable { get; set; }
    }

    // Recipe-scoped, explicit rule for one immutable training artifact. Null means unconfigured.
    public sealed class ModelDecisionRule
    {
        public string TrainingRunId { get; set; } = "";
        public string WeightsSha256 { get; set; } = "";
        public string DatasetVersionId { get; set; } = "";
        public string DatasetContentSha256 { get; set; } = "";
        public string ReviewEvidenceSha256 { get; set; } = "";
        public string SavedUtc { get; set; } = "";
        public LabelingDatasetPurpose DatasetPurpose { get; set; }
        public double Confidence { get; set; } = 0.25;
        public double MinimumShortSidePixels { get; set; }
        public int MinimumDefectCount { get; set; } = 1;
        public double? AnomalyScoreThreshold { get; set; }
        public List<ModelDecisionClassRule> Classes { get; set; } = new();
        public ModelDecisionTestSummary FixedTestSummary { get; set; }

        public ModelDecisionRule Copy() => new()
        {
            TrainingRunId = TrainingRunId, WeightsSha256 = WeightsSha256, DatasetVersionId = DatasetVersionId,
            DatasetContentSha256 = DatasetContentSha256, ReviewEvidenceSha256 = ReviewEvidenceSha256, SavedUtc = SavedUtc,
            DatasetPurpose = DatasetPurpose, Confidence = Confidence, MinimumShortSidePixels = MinimumShortSidePixels,
            MinimumDefectCount = MinimumDefectCount, AnomalyScoreThreshold = AnomalyScoreThreshold,
            Classes = Classes?.ConvertAll(item => item?.Copy()) ?? new(), FixedTestSummary = FixedTestSummary?.Copy()
        };
    }

    public sealed class ModelDecisionTestSummary
    {
        public int NgCount { get; set; }
        public int OkCount { get; set; }
        public int Misses { get; set; }
        public int Extras { get; set; }
        public int ExcludedCount { get; set; }
        public string EvaluatedUtc { get; set; } = "";
        public string EvaluationEvidenceSha256 { get; set; } = "";
        public ModelDecisionTestSummary Copy() => (ModelDecisionTestSummary)MemberwiseClone();
    }

    public sealed class ModelDecisionClassRule
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; } = "";
        public bool Enabled { get; set; } = true;
        public double? Confidence { get; set; }
        public ModelDecisionClassRule Copy() => new() { ClassId = ClassId, ClassName = ClassName, Enabled = Enabled, Confidence = Confidence };
    }

    public sealed class TrainingRunParameters
    {
        public string ModelId { get; set; } = "";
        public string WeightsName { get; set; } = "";
        public int Epochs { get; set; }
        public int ImageSize { get; set; }
        public int BatchSize { get; set; }
        public bool OnlyReviewed { get; set; }
        public string InputDatasetContentSha256 { get; set; } = "";
        public string QualityReviewSha256 { get; set; } = "";
        public string InputRootPath { get; set; } = "";
        public double? DurationSeconds { get; set; }

        public TrainingRunParameters Copy() => new()
        {
            ModelId = ModelId, WeightsName = WeightsName, Epochs = Epochs, ImageSize = ImageSize, BatchSize = BatchSize,
            OnlyReviewed = OnlyReviewed, InputDatasetContentSha256 = InputDatasetContentSha256,
            QualityReviewSha256 = QualityReviewSha256, InputRootPath = InputRootPath, DurationSeconds = DurationSeconds
        };
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

        public string AppliedWeightsPath { get; set; } = "";

        public string PreviousWeightsSha256 { get; set; } = "";

        public string PreviousCandidateId { get; set; } = "";

        public string UndoneUtc { get; set; } = "";

        public string UndoReason { get; set; } = "";

        public string EvaluationDatasetVersionId { get; set; } = "";

        public string EvaluationDatasetContentSha256 { get; set; } = "";

        public string EvaluationReportPath { get; set; } = "";

        public string EvaluationReportSha256 { get; set; } = "";

        public string EvaluationConditions { get; set; } = "";

        public string EvaluationMetricsSummary { get; set; } = "";

        public string AdoptedUtc { get; set; } = "";

        public bool SavedToRecipe { get; set; }

        public string DecisionSummary { get; set; } = "";
    }
}
