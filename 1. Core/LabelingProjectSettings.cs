using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.IO;

namespace MvcVisionSystem
{
    public class LabelingProjectSettings
    {
        public YoloDatasetSettings YoloDataset { get; set; } = new YoloDatasetSettings();

        public TrainingSettings Training { get; set; } = new TrainingSettings();

        public PythonModelSettings PythonModel { get; set; } = new PythonModelSettings();

        public YoloTrainingGuideHistory TrainingGuide { get; set; } = new YoloTrainingGuideHistory();

        public void EnsureDefaults()
        {
            YoloDataset ??= new YoloDatasetSettings();
            Training ??= new TrainingSettings();
            PythonModel ??= new PythonModelSettings();
            TrainingGuide ??= new YoloTrainingGuideHistory();
            TrainingGuide.EnsureDefaults();
            PythonModel.EnsureDefaults();
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

    public class PythonModelSettings
    {
        private const string ProjectRootPathDefault = @"C:\Git\yolov5";
        private const string BundledTrainImageRootPathDefault = @"C:\Git\yolov5\data\train\images";
        private const string BundledValidImageRootPathDefault = @"C:\Git\yolov5\data\valid\images";
        private const string LegacyTrainedImageRootPathDefault = @"C:\Git\py\data\train\images";
        private const string LegacyImageRootPathDefault = @"C:\Git\py\KtemData";
        private const string RetiredProjectRootPath = @"C:\Git\새 폴더\yolov5";
        private const string RetiredImageRootPath = @"C:\Git\새 폴더\py\KtemData";

        public string PythonExecutablePath { get; set; } = "";

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

            if (string.IsNullOrWhiteSpace(ProjectRootPath))
            {
                ProjectRootPath = GetDefaultProjectRootPath();
            }

            if (string.IsNullOrWhiteSpace(ClientScriptPath))
            {
                ClientScriptPath = Path.Combine(ProjectRootPath, "labelling_tcp_client.py");
            }

            if (string.IsNullOrWhiteSpace(WeightsPath))
            {
                WeightsPath = Path.Combine(ProjectRootPath, "best.pt");
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

        public static string GetDefaultProjectRootPath()
        {
            string siblingRoot = ResolveSiblingPath("yolov5");
            if (Directory.Exists(siblingRoot))
            {
                return siblingRoot;
            }

            return Directory.Exists(ProjectRootPathDefault) ? ProjectRootPathDefault : siblingRoot;
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
            if (ShouldRepairProjectRoot(ProjectRootPath, ClientScriptPath) && IsUsableYoloProjectRoot(defaultProjectRootPath))
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

        private static bool ShouldRepairProjectRoot(string projectRootPath, string clientScriptPath)
        {
            if (string.IsNullOrWhiteSpace(projectRootPath) || !Directory.Exists(projectRootPath))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(clientScriptPath) && File.Exists(clientScriptPath))
            {
                return false;
            }

            return !IsUsableYoloProjectRoot(projectRootPath);
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
                    if (File.Exists(Path.Combine(current, "MvcVisionSystem.sln"))
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
