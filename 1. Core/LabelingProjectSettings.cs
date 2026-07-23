using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.IO;

namespace MvcVisionSystem
{
    public enum LabelingDatasetPurpose
    {
        ObjectDetection,
        Segmentation,
        AnomalyDetection
    }

    public class LabelingProjectSettings
    {
        public LabelingDatasetPurpose DatasetPurpose { get; set; } = LabelingDatasetPurpose.ObjectDetection;

        public YoloDatasetSettings YoloDataset { get; set; } = new YoloDatasetSettings();

        // Native YOLO data.yaml inputs stay separate from the recipe-owned export root.
        // Selecting one never rewrites the current labeling dataset paths or annotations.
        public ExternalYoloDatasetSettings ExternalYoloDataset { get; set; } = new ExternalYoloDatasetSettings();

        public TrainingSettings Training { get; set; } = new TrainingSettings();

        public PythonModelSettings PythonModel { get; set; } = new PythonModelSettings();

        public YoloTrainingGuideHistory TrainingGuide { get; set; } = new YoloTrainingGuideHistory();

        public ModelRegistrySettings ModelRegistry { get; set; } = new ModelRegistrySettings();

        public AnomalyClassificationSettings AnomalyClassification { get; set; } = new AnomalyClassificationSettings();

        public void EnsureDefaults()
        {
            if (!Enum.IsDefined(typeof(LabelingDatasetPurpose), DatasetPurpose))
            {
                DatasetPurpose = LabelingDatasetPurpose.ObjectDetection;
            }

            YoloDataset ??= new YoloDatasetSettings();
            ExternalYoloDataset ??= new ExternalYoloDatasetSettings();
            Training ??= new TrainingSettings();
            PythonModel ??= new PythonModelSettings();
            TrainingGuide ??= new YoloTrainingGuideHistory();
            ModelRegistry ??= new ModelRegistrySettings();
            AnomalyClassification ??= new AnomalyClassificationSettings();
            TrainingGuide.EnsureDefaults();
            ModelRegistry.EnsureDefaults();
            PythonModel.EnsureDefaults();
            AnomalyClassification.EnsureDefaults();
            ExternalYoloDataset.EnsureDefaults();
        }
    }

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
            LastTrainingWeightFile = "";
            LastTrainingResolvedWeightFile = "";
            LastTrainingWeightSha256 = "";
            LastTrainingPythonExecutablePath = "";
            LastTrainingClientScriptPath = "";
            LastTrainingClientScriptSha256 = "";
        }
    }

    public class AnomalyClassificationSettings
    {
        public List<string> NormalClassNames { get; set; } = new List<string>();

        public List<string> AbnormalClassNames { get; set; } = new List<string>();

        public double MinimumConfidence { get; set; }

        public void EnsureDefaults()
        {
            NormalClassNames ??= new List<string>();
            AbnormalClassNames ??= new List<string>();
            MinimumConfidence = double.IsNaN(MinimumConfidence) || double.IsInfinity(MinimumConfidence)
                ? 0D
                : Math.Clamp(MinimumConfidence, 0D, 1D);
        }

        public AnomalyClassificationDecisionOptions ToDecisionOptions()
        {
            EnsureDefaults();
            return new AnomalyClassificationDecisionOptions
            {
                NormalClassNames = NormalClassNames,
                AbnormalClassNames = AbnormalClassNames,
                MinimumConfidence = MinimumConfidence
            };
        }
    }

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

        public string AppliedWeightsPath { get; set; } = "";

        public string AppliedWeightsUtc { get; set; } = "";

        public bool AppliedWeightsSavedToRecipe { get; set; }

        public List<YoloTrainingGuideRunRecord> RunHistory { get; set; } = new List<YoloTrainingGuideRunRecord>();

        public void EnsureDefaults()
        {
            RunHistory ??= new List<YoloTrainingGuideRunRecord>();
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

        public string AppliedWeightsPath { get; set; } = "";

        public bool AppliedWeightsSavedToRecipe { get; set; }
    }

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

        public string ProfileId { get; set; } = "";

        public string EventUtc { get; set; } = "";

        public string OutputRootPath { get; set; } = "";

        public string State { get; set; } = "";

        public int ProgressPercent { get; set; } = -1;

        public string Message { get; set; } = "";

        public string CandidateWeightsPath { get; set; } = "";

        public string BaselineWeightsPath { get; set; } = "";

        public string MetricsSummary { get; set; } = "";
    }

    public class ModelCandidate
    {
        public string CandidateId { get; set; } = "";

        public string ProfileId { get; set; } = "";

        public string TrainingRunId { get; set; } = "";

        public string WeightsPath { get; set; } = "";

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

        public string PreviousWeightsPath { get; set; } = "";

        public string AdoptedUtc { get; set; } = "";

        public bool SavedToRecipe { get; set; }

        public string DecisionSummary { get; set; } = "";
    }

    public class PythonModelSettings
    {
        public const string EngineYoloV5 = "YOLOv5";
        public const string EngineYoloV8 = "YOLOv8";
        public const string EngineYolo11 = "YOLO11";
        public const string EngineUnet = "U-Net";
        public const string EngineOnnx = "ONNX";

        private const string ProjectRootPathDefault = @"C:\Git\yolov5";
        private const string BundledTrainImageRootPathDefault = @"C:\Git\yolov5\data\train\images";
        private const string BundledValidImageRootPathDefault = @"C:\Git\yolov5\data\valid\images";
        private const string LegacyTrainedImageRootPathDefault = @"C:\Git\py\data\train\images";
        private const string LegacyImageRootPathDefault = @"C:\Git\py\KtemData";
        private const string RetiredProjectRootPath = @"C:\Git\새 폴더\yolov5";
        private const string RetiredImageRootPath = @"C:\Git\새 폴더\py\KtemData";

        public string PythonExecutablePath { get; set; } = "";

        public string ModelEngine { get; set; } = EngineYoloV5;

        public string ProjectRootPath { get; set; } = GetDefaultProjectRootPath();

        public string ClientScriptPath { get; set; } = Path.Combine(GetDefaultProjectRootPath(), "labelling_tcp_client.py");

        public string WeightsPath { get; set; } = Path.Combine(GetDefaultProjectRootPath(), "best.pt");

        public string ImageRootPath { get; set; } = GetDefaultImageRootPath();

        public float MinimumDetectionConfidence { get; set; } = 0.25F;

        public int MaximumDetectionCandidates { get; set; } = 20;

        public int InferenceImageSize { get; set; } = 320;

        public int DetectionTimeoutSeconds { get; set; } = 30;

        public bool AutoStartClient { get; set; } = true;

        public void EnsureDefaults()
        {
            MigrateRetiredDefaults();
            RepairPortableYoloPaths();
            ModelEngine = NormalizeModelEngine(ModelEngine);

            if (string.IsNullOrWhiteSpace(ProjectRootPath))
            {
                ProjectRootPath = GetDefaultProjectRootPath();
            }

            if (string.IsNullOrWhiteSpace(ClientScriptPath))
            {
                ClientScriptPath = ModelEngine == EngineUnet
                    ? _1._Core.PythonModelRuntimeBundledWorkerService.ResolveUnetWorkerScriptPath()
                    : Path.Combine(ProjectRootPath, "labelling_tcp_client.py");
            }

            if (string.IsNullOrWhiteSpace(WeightsPath))
            {
                WeightsPath = ModelEngine == EngineUnet
                    ? GetDefaultUnetWeightsPath(ProjectRootPath)
                    : Path.Combine(ProjectRootPath, "best.pt");
            }

            if (string.IsNullOrWhiteSpace(ImageRootPath))
            {
                ImageRootPath = GetDefaultImageRootPath();
            }

            MinimumDetectionConfidence = Math.Clamp(MinimumDetectionConfidence, 0F, 1F);
            MaximumDetectionCandidates = Math.Clamp(MaximumDetectionCandidates, 1, 200);
            InferenceImageSize = Math.Clamp(InferenceImageSize, 64, 2048);
            DetectionTimeoutSeconds = Math.Clamp(DetectionTimeoutSeconds, 1, 600);
        }

        public static IReadOnlyList<string> GetSupportedModelEngines()
            => new[] { EngineYoloV5, EngineYoloV8, EngineYolo11, EngineUnet, EngineOnnx };

        public static string NormalizeModelEngine(string value)
        {
            string normalized = (value ?? string.Empty).Trim();
            if (string.Equals(normalized, "yolov8", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "yolo8", StringComparison.OrdinalIgnoreCase))
            {
                return EngineYoloV8;
            }

            if (string.Equals(normalized, "yolo11", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "yolov11", StringComparison.OrdinalIgnoreCase))
            {
                return EngineYolo11;
            }

            if (string.Equals(normalized, "unet", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "u-net", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "u net", StringComparison.OrdinalIgnoreCase))
            {
                return EngineUnet;
            }

            if (string.Equals(normalized, "onnx", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "onnxruntime", StringComparison.OrdinalIgnoreCase))
            {
                return EngineOnnx;
            }

            return EngineYoloV5;
        }

        public string GetProtocolModelName()
        {
            return NormalizeModelEngine(ModelEngine) switch
            {
                EngineYoloV8 => "yolov8",
                EngineYolo11 => "yolo11",
                EngineUnet => "unet",
                EngineOnnx => "onnx",
                _ => "yolov5"
            };
        }

        public string GetModelRootPath()
        {
            string projectRootPath = ProjectRootPath?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(projectRootPath))
            {
                return string.Empty;
            }

            return NormalizeModelEngine(ModelEngine) switch
            {
                EngineYoloV5 => Path.Combine(projectRootPath, "yolov5Master"),
                _ => projectRootPath
            };
        }

        public static string GetDefaultProjectRootPath()
        {
            string siblingRoot = ResolveSiblingPath("yolov5");
            if (Directory.Exists(siblingRoot))
            {
                return siblingRoot;
            }

            return Directory.Exists(ProjectRootPathDefault) ? ProjectRootPathDefault : siblingRoot;
        }

        public static string GetDefaultUnetProjectRootPath()
        {
            string siblingRoot = ResolveSiblingPath("unet");
            return Directory.Exists(siblingRoot) ? siblingRoot : Path.Combine(@"C:\Git", "unet");
        }

        public static string GetDefaultUnetWeightsPath(string projectRootPath = "")
        {
            string root = string.IsNullOrWhiteSpace(projectRootPath)
                ? GetDefaultUnetProjectRootPath()
                : projectRootPath.Trim();
            return Path.Combine(root, "runs", "segment", "openvisionlab-unet-segmentation", "weights", "best.pt");
        }

        public static string GetDefaultImageRootPath()
        {
            string projectRootPath = GetDefaultProjectRootPath();
            string siblingPyRoot = ResolveSiblingPath("py");
            foreach (string candidate in new[]
            {
                Path.Combine(projectRootPath, "data", "train", "images"),
                Path.Combine(projectRootPath, "data", "valid", "images"),
                Path.Combine(projectRootPath, "data", "images"),
                Path.Combine(projectRootPath, "yolov5Master", "data", "images"),
                BundledTrainImageRootPathDefault,
                BundledValidImageRootPathDefault,
                Path.Combine(siblingPyRoot, "data", "train", "images"),
                Path.Combine(siblingPyRoot, "KtemData"),
                LegacyTrainedImageRootPathDefault,
                LegacyImageRootPathDefault
            })
            {
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            return "";
        }

        public string GetRequirementsPath()
        {
            return string.IsNullOrWhiteSpace(ProjectRootPath)
                ? string.Empty
                : Path.Combine(ProjectRootPath, "requirements.txt");
        }

        private void RepairPortableYoloPaths()
        {
            string defaultProjectRootPath = GetDefaultProjectRootPath();
            bool repairedProjectRoot = false;
            if (string.IsNullOrWhiteSpace(ProjectRootPath) && IsUsableYoloProjectRoot(defaultProjectRootPath))
            {
                ProjectRootPath = defaultProjectRootPath;
                repairedProjectRoot = true;
            }

            string preferredScriptPath = ResolvePreferredClientScriptPath(ProjectRootPath);
            if (!string.IsNullOrWhiteSpace(preferredScriptPath)
                && (repairedProjectRoot || string.IsNullOrWhiteSpace(ClientScriptPath)))
            {
                ClientScriptPath = preferredScriptPath;
            }

            string preferredWeightsPath = string.IsNullOrWhiteSpace(ProjectRootPath)
                ? string.Empty
                : Path.Combine(ProjectRootPath, "best.pt");
            if (!string.IsNullOrWhiteSpace(preferredWeightsPath)
                && File.Exists(preferredWeightsPath)
                && (repairedProjectRoot || string.IsNullOrWhiteSpace(WeightsPath)))
            {
                WeightsPath = preferredWeightsPath;
            }

            string preferredImageRootPath = GetDefaultImageRootPath();
            if (!string.IsNullOrWhiteSpace(preferredImageRootPath)
                && Directory.Exists(preferredImageRootPath)
                && (repairedProjectRoot || string.IsNullOrWhiteSpace(ImageRootPath)))
            {
                ImageRootPath = preferredImageRootPath;
            }
        }

        private static bool IsUsableYoloProjectRoot(string projectRootPath)
        {
            if (string.IsNullOrWhiteSpace(projectRootPath) || !Directory.Exists(projectRootPath))
            {
                return false;
            }

            string clientScriptPath = ResolvePreferredClientScriptPath(projectRootPath);
            return File.Exists(clientScriptPath)
                && (File.Exists(Path.Combine(projectRootPath, "requirements.txt"))
                    || Directory.Exists(Path.Combine(projectRootPath, "yolov5Master")));
        }

        private static string ResolvePreferredClientScriptPath(string projectRootPath)
        {
            if (string.IsNullOrWhiteSpace(projectRootPath))
            {
                return string.Empty;
            }

            foreach (string fileName in new[] { "labelling_tcp_client.py", "labeling_tcp_client.py" })
            {
                string candidate = Path.Combine(projectRootPath, fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return Path.Combine(projectRootPath, "labelling_tcp_client.py");
        }

        private static string ResolveSiblingPath(string directoryName)
        {
            string repositoryRoot = FindLabelingRepositoryRoot();
            string parent = string.IsNullOrWhiteSpace(repositoryRoot)
                ? Directory.GetParent(AppContext.BaseDirectory)?.FullName
                : Directory.GetParent(repositoryRoot)?.FullName;

            return string.IsNullOrWhiteSpace(parent)
                ? Path.Combine(AppContext.BaseDirectory, directoryName)
                : Path.Combine(parent, directoryName);
        }

        private static string FindLabelingRepositoryRoot()
        {
            foreach (string startPath in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
            {
                string current = startPath;
                while (!string.IsNullOrWhiteSpace(current))
                {
                    if (File.Exists(Path.Combine(current, "OpenVisionLab.LabelingStudio.sln"))
                        || File.Exists(Path.Combine(current, "OpenVisionLab.LabelingStudio.csproj"))
                        || File.Exists(Path.Combine(current, "MvcVisionSystem.sln"))
                        || File.Exists(Path.Combine(current, "MvcVisionSystem.csproj")))
                    {
                        return current;
                    }

                    current = Directory.GetParent(current)?.FullName;
                }
            }

            return string.Empty;
        }

        private void MigrateRetiredDefaults()
        {
            string defaultProjectRootPath = GetDefaultProjectRootPath();

            if (string.Equals(ProjectRootPath, RetiredProjectRootPath, StringComparison.OrdinalIgnoreCase))
            {
                ProjectRootPath = defaultProjectRootPath;
            }

            string retiredScriptPath = Path.Combine(RetiredProjectRootPath, "labelling_tcp_client.py");
            if (string.Equals(ClientScriptPath, retiredScriptPath, StringComparison.OrdinalIgnoreCase))
            {
                ClientScriptPath = Path.Combine(defaultProjectRootPath, "labelling_tcp_client.py");
            }

            string retiredWeightsPath = Path.Combine(RetiredProjectRootPath, "best.pt");
            if (string.Equals(WeightsPath, retiredWeightsPath, StringComparison.OrdinalIgnoreCase))
            {
                WeightsPath = Path.Combine(defaultProjectRootPath, "best.pt");
            }

            if (string.Equals(ImageRootPath, RetiredImageRootPath, StringComparison.OrdinalIgnoreCase))
            {
                ImageRootPath = GetDefaultImageRootPath();
            }
        }
    }

    public class YoloDatasetSettings
    {
        public string OutputRootPath { get; set; } = "";

        public string DataYamlFilePath { get; set; } = "";

        public string TrainImagesPath => Path.Combine(OutputRootPath, "data", "train", "images");

        public string TrainLabelsPath => Path.Combine(OutputRootPath, "data", "train", "labels");

        public string ValidImagesPath => Path.Combine(OutputRootPath, "data", "valid", "images");

        public string ValidLabelsPath => Path.Combine(OutputRootPath, "data", "valid", "labels");

        public string TestImagesPath => Path.Combine(OutputRootPath, "data", "test", "images");

        public string TestLabelsPath => Path.Combine(OutputRootPath, "data", "test", "labels");

        public int ValidationPercent { get; set; } = 20;

        public int TestPercent { get; set; } = 0;

        public int SplitSeed { get; set; } = 17;

        public void ConfigureOutputRoot(string outputRootPath)
        {
            if (string.IsNullOrWhiteSpace(outputRootPath))
            {
                return;
            }

            OutputRootPath = outputRootPath;
            DataYamlFilePath = Path.Combine(outputRootPath, "data.yaml");
        }

        public string ResolveOutputRootPath(string fallbackRootPath)
        {
            if (!string.IsNullOrWhiteSpace(OutputRootPath))
            {
                return OutputRootPath;
            }

            if (!string.IsNullOrWhiteSpace(DataYamlFilePath))
            {
                if (IsYamlFilePath(DataYamlFilePath))
                {
                    string directoryName = Path.GetDirectoryName(DataYamlFilePath);
                    if (!string.IsNullOrWhiteSpace(directoryName))
                    {
                        return directoryName;
                    }
                }

                return DataYamlFilePath;
            }

            return fallbackRootPath;
        }

        public static bool IsYamlFilePath(string path)
        {
            string extension = Path.GetExtension(path);
            return string.Equals(extension, ".yaml", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".yml", StringComparison.OrdinalIgnoreCase);
        }
    }

    public class TrainingSettings
    {
        public int ImageSize { get; set; } = 320;

        public int Batch { get; set; } = 16;

        public int Epoch { get; set; } = 50;

        public string Cfg { get; set; } = CYolov5TrainingParam.Cfg.yolov5x.ToString();

        public string Weight { get; set; } = CYolov5TrainingParam.Weight.yolov5x.ToString();

        public void CopyFrom(CYolov5TrainingParam trainingParam)
        {
            if (trainingParam == null)
            {
                return;
            }

            ImageSize = trainingParam.imageSize;
            Batch = trainingParam.batch;
            Epoch = trainingParam.epoch;
            Cfg = trainingParam.cfg.ToString();
            Weight = trainingParam.weight.ToString();
        }

        public void ApplyTo(CYolov5TrainingParam trainingParam)
        {
            if (trainingParam == null)
            {
                return;
            }

            trainingParam.imageSize = ImageSize;
            trainingParam.batch = Batch;
            trainingParam.epoch = Epoch;

            if (Enum.TryParse(Cfg, out CYolov5TrainingParam.Cfg cfg))
            {
                trainingParam.cfg = cfg;
            }

            if (Enum.TryParse(Weight, out CYolov5TrainingParam.Weight weight))
            {
                trainingParam.weight = weight;
            }
        }
    }
}
