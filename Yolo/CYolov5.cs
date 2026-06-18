using RJCodeUI_M1.RJControls;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using YamlDotNet.Serialization;
using static MvcVisionSystem.DEFINE;

namespace MvcVisionSystem.Yolo
{
    public class YamlData
    {
        public string train { get; set; }
        public string val { get; set; }
        public int nc { get; set; }
        public List<string> names { get; set; }
    }

    public class CYolov5TrainingParam
    {
        public int imageSize { get; set; } = 320;
        public int batch { get; set; } = 16;
        public int epoch { get; set; } = 50;

        public Cfg cfg { get; set; } = Cfg.yolov5x;

        public Weight weight { get; set; } = Weight.yolov5x;

        public enum Cfg
        {
            yolov5s,
            yolov5n,
            yolov5m,
            yolov5l,
            yolov5x
        }

        public enum Weight
        {
            yolov5s,
            yolov5n,
            yolov5m,
            yolov5l,
            yolov5x
        }
    }

    [Obsolete("Use CYolov5TrainingParam. This type is kept for legacy source compatibility.")]
    public class CYolov5TranningParam : CYolov5TrainingParam
    {
    }

    public static class CYolov5
    {
        public static void CreateYaml(string trainImagesPath, string valImagesPath, List<string> classNames, string outputYamlPath)
        {
            if (string.IsNullOrWhiteSpace(outputYamlPath))
            {
                throw new ArgumentException("YAML output path is required.", nameof(outputYamlPath));
            }

            classNames ??= new List<string>();
            string outputDirectory = Path.GetDirectoryName(outputYamlPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            // Create an object with the necessary information for the yaml file
            YamlData data = new YamlData
            {
                train = NormalizeYamlPath(trainImagesPath),
                val = NormalizeYamlPath(valImagesPath),
                nc = classNames.Count,
                names = classNames
            };

            // Create a new SerializerBuilder and serialize the data
            var serializer = new SerializerBuilder().Build();
            var yaml = serializer.Serialize(data);

            if (File.Exists(outputYamlPath)
                && string.Equals(File.ReadAllText(outputYamlPath, Encoding.UTF8), yaml, StringComparison.Ordinal))
            {
                return;
            }

            // Write the yaml data to a file
            File.WriteAllText(outputYamlPath, yaml, new UTF8Encoding(false));
        }

        public static string NormalizeYamlPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? "" : path.Replace("\\", "/");
        }

    }
}
