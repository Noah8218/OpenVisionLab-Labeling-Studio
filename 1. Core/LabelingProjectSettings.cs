using MvcVisionSystem.Yolo;
using System;
using System.IO;

namespace MvcVisionSystem
{
    public class LabelingProjectSettings
    {
        public YoloDatasetSettings YoloDataset { get; set; } = new YoloDatasetSettings();

        public TrainingSettings Training { get; set; } = new TrainingSettings();

        public PythonModelSettings PythonModel { get; set; } = new PythonModelSettings();

        public void EnsureDefaults()
        {
            YoloDataset ??= new YoloDatasetSettings();
            Training ??= new TrainingSettings();
            PythonModel ??= new PythonModelSettings();
            PythonModel.EnsureDefaults();
        }
    }

    public class PythonModelSettings
    {
        private const string ProjectRootPathDefault = @"C:\Git\yolov5";
        private const string ImageRootPathDefault = @"C:\Git\py\KtemData";
        private const string RetiredProjectRootPath = @"C:\Git\새 폴더\yolov5";
        private const string RetiredImageRootPath = @"C:\Git\새 폴더\py\KtemData";

        public string PythonExecutablePath { get; set; } = "";

        public string ProjectRootPath { get; set; } = GetDefaultProjectRootPath();

        public string ClientScriptPath { get; set; } = Path.Combine(GetDefaultProjectRootPath(), "labelling_tcp_client.py");

        public string WeightsPath { get; set; } = Path.Combine(GetDefaultProjectRootPath(), "best.pt");

        public string ImageRootPath { get; set; } = GetDefaultImageRootPath();

        public float MinimumDetectionConfidence { get; set; } = 0.25F;

        public int DetectionTimeoutSeconds { get; set; } = 30;

        public bool AutoStartClient { get; set; } = true;

        public void EnsureDefaults()
        {
            MigrateRetiredDefaults();

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
            DetectionTimeoutSeconds = Math.Clamp(DetectionTimeoutSeconds, 1, 600);
        }

        public static string GetDefaultProjectRootPath()
        {
            return ProjectRootPathDefault;
        }

        public static string GetDefaultImageRootPath()
        {
            return Directory.Exists(ImageRootPathDefault) ? ImageRootPathDefault : "";
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

        public int ValidationPercent { get; set; } = 20;

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
