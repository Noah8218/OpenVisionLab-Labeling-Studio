using System.Collections.Generic;

namespace MvcVisionSystem
{
    public class YoloTrainingGuideHistory
    {
        public string LastDatasetCheckUtc { get; set; } = "";

        public bool LastDatasetReady { get; set; }

        public string LastDatasetIssueKind { get; set; } = "";

        public string LastDatasetSummary { get; set; } = "";

        public string LastTrainingUpdateUtc { get; set; } = "";

        public string LastTrainingState { get; set; } = "";

        public int LastTrainingProgressPercent { get; set; } = -1;

        public string LastTrainingMessage { get; set; } = "";

        public string LastTrainingRunId { get; set; } = "";

        public string LastTrainingRunName { get; set; } = "";

        public string LastTrainingDatasetVersionId { get; set; } = "";

        public string LastTrainingDatasetContentSha256 { get; set; } = "";

        public string AppliedWeightsPath { get; set; } = "";

        public string AppliedWeightsUtc { get; set; } = "";

        public bool AppliedWeightsSavedToRecipe { get; set; }

        public List<YoloTrainingGuideRunRecord> RunHistory { get; set; } = new List<YoloTrainingGuideRunRecord>();

        public void EnsureDefaults()
        {
            RunHistory ??= new List<YoloTrainingGuideRunRecord>();
            LastTrainingRunId ??= "";
            LastTrainingRunName ??= "";
            LastTrainingDatasetVersionId ??= "";
            LastTrainingDatasetContentSha256 ??= "";
        }
    }

    public class YoloTrainingGuideRunRecord
    {
        public string EventUtc { get; set; } = "";

        public string EventKind { get; set; } = "";

        public bool DatasetReady { get; set; }

        public string DatasetIssueKind { get; set; } = "";

        public string DatasetSummary { get; set; } = "";

        public string TrainingState { get; set; } = "";

        public int TrainingProgressPercent { get; set; } = -1;

        public string TrainingMessage { get; set; } = "";

        public string TrainingRunId { get; set; } = "";

        public string TrainingRunName { get; set; } = "";

        public string AppliedWeightsPath { get; set; } = "";

        public bool AppliedWeightsSavedToRecipe { get; set; }
    }
}
