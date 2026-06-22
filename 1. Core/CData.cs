using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using Lib.Common;
using MvcVisionSystem.Yolo;

namespace MvcVisionSystem
{   
    public class CData
    {
        public List<CClassItem> ClassNamedList { get; set; } = new List<CClassItem>();

        public string OutputDataYamlPath { get; set; } = "";

        public string OutputDataImageAndTxtPath { get; set; } = "";

        public CYolov5TrainingParam TranningParam { get; set; } = new CYolov5TrainingParam();

        [XmlIgnore]
        public CYolov5TrainingParam TrainingParam
        {
            get => TranningParam;
            set => TranningParam = value ?? new CYolov5TrainingParam();
        }

        public LabelingProjectSettings ProjectSettings { get; set; } = new LabelingProjectSettings();

        [XmlIgnore] public string LastSelectImageName { get; set; } = "";

        [XmlIgnore] public string LastSelectImagePath { get; set; } = "";

        [XmlIgnore] public string OutputRootPath => ResolveOutputRootPath();

        [XmlIgnore] public string DataYamlFilePath => ResolveDataYamlFilePath();

        [XmlIgnore] public string TrainImagesPath => Path.Combine(OutputRootPath, "data", "train", "images");

        [XmlIgnore] public string ValidImagesPath => Path.Combine(OutputRootPath, "data", "valid", "images");

        [XmlIgnore] public string TestImagesPath => Path.Combine(OutputRootPath, "data", "test", "images");

        public CData() { CUtil.InitDirectory("DATA"); }
        public CData LoadConfig(string RecipeName)
        {
            string strPath = GetRecipeConfigPath(RecipeName);
            CData newData = null;

            if (File.Exists(strPath))
            {
                newData = SerializeHelper.FromXmlFile<CData>(strPath);
                if (newData != null)
                {
                    newData.NormalizeOutputPaths();
                    newData.NormalizeTrainingSettings();
                    return newData;
                }
                    
            }
            this.SaveConfig(RecipeName);
            return newData = this.LoadConfig(RecipeName);
        }

        public void SaveConfig(string RecipeName)
        {
            NormalizeOutputPaths();
            NormalizeTrainingSettings();
            string strPath = GetRecipeConfigPath(RecipeName);
            Directory.CreateDirectory(Path.GetDirectoryName(strPath));
            SerializeHelper.ToXmlFile(strPath, this);
        }

        public void ConfigureOutputRoot(string outputRootPath)
        {
            if (string.IsNullOrWhiteSpace(outputRootPath))
            {
                return;
            }

            OutputDataImageAndTxtPath = outputRootPath;
            OutputDataYamlPath = Path.Combine(outputRootPath, "data.yaml");
            EnsureProjectSettings();
            ProjectSettings.YoloDataset.ConfigureOutputRoot(outputRootPath);
        }

        public void NormalizeOutputPaths()
        {
            EnsureProjectSettings();
            string outputRootPath = ResolveOutputRootPath();
            ProjectSettings.YoloDataset.ConfigureOutputRoot(outputRootPath);
            OutputDataImageAndTxtPath = ProjectSettings.YoloDataset.OutputRootPath;
            OutputDataYamlPath = ProjectSettings.YoloDataset.DataYamlFilePath;
        }

        public void NormalizeTrainingSettings()
        {
            EnsureProjectSettings();
            ProjectSettings.Training.CopyFrom(TranningParam);
        }

        public TrainingSettings GetTrainingSettings()
        {
            NormalizeTrainingSettings();
            return ProjectSettings.Training;
        }

        public void SaveYoloDataYaml()
        {
            NormalizeOutputPaths();
            EnsureYoloOutputDirectories();
            List<string> classNames = ClassNamedList.Select(item => item.Text).ToList();
            CYolov5.CreateYaml(TrainImagesPath, ValidImagesPath, TestImagesPath, classNames, DataYamlFilePath);
        }

        public void EnsureYoloOutputDirectories()
        {
            Directory.CreateDirectory(OutputRootPath);
            Directory.CreateDirectory(TrainImagesPath);
            Directory.CreateDirectory(Path.Combine(OutputRootPath, "data", "train", "labels"));
            Directory.CreateDirectory(ValidImagesPath);
            Directory.CreateDirectory(Path.Combine(OutputRootPath, "data", "valid", "labels"));
            Directory.CreateDirectory(TestImagesPath);
            Directory.CreateDirectory(Path.Combine(OutputRootPath, "data", "test", "labels"));
        }

        private string ResolveOutputRootPath()
        {
            EnsureProjectSettings();
            string settingsOutputRoot = ProjectSettings.YoloDataset.ResolveOutputRootPath("");
            if (!string.IsNullOrWhiteSpace(settingsOutputRoot))
            {
                return settingsOutputRoot;
            }

            if (!string.IsNullOrWhiteSpace(OutputDataImageAndTxtPath))
            {
                return OutputDataImageAndTxtPath;
            }

            if (!string.IsNullOrWhiteSpace(OutputDataYamlPath))
            {
                if (IsYamlFilePath(OutputDataYamlPath))
                {
                    string directoryName = Path.GetDirectoryName(OutputDataYamlPath);
                    if (!string.IsNullOrWhiteSpace(directoryName))
                    {
                        return directoryName;
                    }
                }

                return OutputDataYamlPath;
            }

            return Path.Combine(AppContext.BaseDirectory, "DATA");
        }

        private string ResolveDataYamlFilePath()
        {
            EnsureProjectSettings();
            if (!string.IsNullOrWhiteSpace(ProjectSettings.YoloDataset.DataYamlFilePath)
                && YoloDatasetSettings.IsYamlFilePath(ProjectSettings.YoloDataset.DataYamlFilePath))
            {
                return ProjectSettings.YoloDataset.DataYamlFilePath;
            }

            if (!string.IsNullOrWhiteSpace(OutputDataYamlPath) && IsYamlFilePath(OutputDataYamlPath))
            {
                return OutputDataYamlPath;
            }

            return Path.Combine(OutputRootPath, "data.yaml");
        }

        private static string GetRecipeConfigPath(string recipeName)
        {
            return Path.Combine(AppContext.BaseDirectory, "RECIPE", recipeName ?? string.Empty, "VISION.xml");
        }

        private static bool IsYamlFilePath(string path)
        {
            return YoloDatasetSettings.IsYamlFilePath(path);
        }

        private void EnsureProjectSettings()
        {
            ProjectSettings ??= new LabelingProjectSettings();
            ProjectSettings.EnsureDefaults();
        }
    }

}
