using System;

namespace MvcVisionSystem
{
    public sealed class ExternalYoloDatasetSettings
    {
        public string DataYamlFilePath { get; set; } = "";

        public LabelingDatasetPurpose DatasetPurpose { get; set; } = LabelingDatasetPurpose.ObjectDetection;

        public bool UseForTraining { get; set; }

        public bool LastValidationSucceeded { get; set; }

        public bool RequiresExplicitReactivation { get; set; }

        public string LastValidationUtc { get; set; } = "";

        public string LastValidationSummary { get; set; } = "";

        // Snapshot only: it tells the operator which native classes were validated without changing recipe-owned classes.
        public string LastValidationClassNames { get; set; } = "";

        public int TrainImageCount { get; set; }

        public int ValidImageCount { get; set; }

        public int TestImageCount { get; set; }

        public int LabelFileCount { get; set; }

        public int AnnotationCount { get; set; }

        public int ClassCount { get; set; }

        public string SourceFingerprintSha256 { get; set; } = "";

        public int SourceFileCount { get; set; }

        public string LastTrainingUtc { get; set; } = "";

        public string LastTrainingSourceFingerprintSha256 { get; set; } = "";

        public string LastTrainingDataYamlFilePath { get; set; } = "";

        // Source stays selected above; this records the app-owned standard-layout copy actually sent to YOLO.
        public string LastTrainingRuntimeDataYamlFilePath { get; set; } = "";

        public string LastTrainingModel { get; set; } = "";

        public string LastTrainingTask { get; set; } = "";

        public string LastTrainingRunName { get; set; } = "";

        public string LastTrainingRunId { get; set; } = "";

        public string LastTrainingWeightFile { get; set; } = "";

        public string LastTrainingResolvedWeightFile { get; set; } = "";

        public string LastTrainingWeightSha256 { get; set; } = "";

        public string LastTrainingPythonExecutablePath { get; set; } = "";

        public string LastTrainingClientScriptPath { get; set; } = "";

        public string LastTrainingClientScriptSha256 { get; set; } = "";

        public bool HasSelection => !string.IsNullOrWhiteSpace(DataYamlFilePath);

        public void EnsureDefaults()
        {
            if (DatasetPurpose != LabelingDatasetPurpose.ObjectDetection
                && DatasetPurpose != LabelingDatasetPurpose.Segmentation)
            {
                DatasetPurpose = LabelingDatasetPurpose.ObjectDetection;
            }

            DataYamlFilePath ??= "";
            LastValidationUtc ??= "";
            LastValidationSummary ??= "";
            LastValidationClassNames ??= "";
            SourceFingerprintSha256 ??= "";
            LastTrainingUtc ??= "";
            LastTrainingSourceFingerprintSha256 ??= "";
            LastTrainingDataYamlFilePath ??= "";
            LastTrainingRuntimeDataYamlFilePath ??= "";
            LastTrainingModel ??= "";
            LastTrainingTask ??= "";
            LastTrainingRunName ??= "";
            LastTrainingRunId ??= "";
            LastTrainingWeightFile ??= "";
            LastTrainingResolvedWeightFile ??= "";
            LastTrainingWeightSha256 ??= "";
            LastTrainingPythonExecutablePath ??= "";
            LastTrainingClientScriptPath ??= "";
            LastTrainingClientScriptSha256 ??= "";
            TrainImageCount = Math.Max(0, TrainImageCount);
            ValidImageCount = Math.Max(0, ValidImageCount);
            TestImageCount = Math.Max(0, TestImageCount);
            LabelFileCount = Math.Max(0, LabelFileCount);
            AnnotationCount = Math.Max(0, AnnotationCount);
            ClassCount = Math.Max(0, ClassCount);
            SourceFileCount = Math.Max(0, SourceFileCount);
            if (string.IsNullOrWhiteSpace(DataYamlFilePath))
            {
                UseForTraining = false;
            }
        }

        public void Clear()
        {
            DataYamlFilePath = "";
            DatasetPurpose = LabelingDatasetPurpose.ObjectDetection;
            UseForTraining = false;
            LastValidationSucceeded = false;
            RequiresExplicitReactivation = false;
            LastValidationUtc = "";
            LastValidationSummary = "";
            LastValidationClassNames = "";
            TrainImageCount = 0;
            ValidImageCount = 0;
            TestImageCount = 0;
            LabelFileCount = 0;
            AnnotationCount = 0;
            ClassCount = 0;
            SourceFingerprintSha256 = "";
            SourceFileCount = 0;
            LastTrainingUtc = "";
            LastTrainingSourceFingerprintSha256 = "";
            LastTrainingDataYamlFilePath = "";
            LastTrainingRuntimeDataYamlFilePath = "";
            LastTrainingModel = "";
            LastTrainingTask = "";
            LastTrainingRunName = "";
            LastTrainingRunId = "";
            LastTrainingWeightFile = "";
            LastTrainingResolvedWeightFile = "";
            LastTrainingWeightSha256 = "";
            LastTrainingPythonExecutablePath = "";
            LastTrainingClientScriptPath = "";
            LastTrainingClientScriptSha256 = "";
        }
    }
}
