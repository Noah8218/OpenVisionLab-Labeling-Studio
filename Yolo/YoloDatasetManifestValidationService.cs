using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using YamlDotNet.Serialization;

namespace MvcVisionSystem.Yolo
{
    /// <summary>
    /// Owns the persisted YOLO data.yaml class and split-path contract.
    /// Dataset validation orchestration remains with YoloDatasetValidator.
    /// </summary>
    public static class YoloDatasetManifestValidationService
    {
        public static void Validate(LabelingProjectData data, IList<string> errors)
        {
            if (data == null || errors == null || string.IsNullOrWhiteSpace(data.DataYamlFilePath) || !File.Exists(data.DataYamlFilePath))
            {
                return;
            }

            byte[] yamlBytes;
            try
            {
                yamlBytes = File.ReadAllBytes(data.DataYamlFilePath);
            }
            catch (Exception ex)
            {
                errors.Add($"data.yaml could not be read: {ex.Message}");
                return;
            }

            if (yamlBytes.Length >= 3 && yamlBytes[0] == 0xEF && yamlBytes[1] == 0xBB && yamlBytes[2] == 0xBF)
            {
                errors.Add("data.yaml must be UTF-8 without BOM. YOLOv5 can misread the first key when a BOM is present.");
            }

            YoloDataYamlContract yaml;
            try
            {
                string yamlText = Encoding.UTF8.GetString(yamlBytes);
                yaml = new DeserializerBuilder()
                    .IgnoreUnmatchedProperties()
                    .Build()
                    .Deserialize<YoloDataYamlContract>(yamlText);
            }
            catch (Exception ex)
            {
                errors.Add($"data.yaml is invalid YAML: {ex.Message}");
                return;
            }

            if (yaml == null)
            {
                errors.Add("data.yaml is empty or unreadable.");
                return;
            }

            ValidateClasses(data, yaml, errors);
            ValidatePath(data.DataYamlFilePath, yaml.path, "train", yaml.train, data.TrainImagesPath, required: true, errors);
            ValidatePath(data.DataYamlFilePath, yaml.path, "val", yaml.val, data.ValidImagesPath, required: true, errors);
            ValidatePath(data.DataYamlFilePath, yaml.path, "test", yaml.test, data.TestImagesPath, required: false, errors);
        }

        private static void ValidateClasses(LabelingProjectData data, YoloDataYamlContract yaml, IList<string> errors)
        {
            List<string> classNames = data.ClassNamedList?
                .Select(item => item?.Text?.Trim() ?? string.Empty)
                .ToList()
                ?? new List<string>();

            if (yaml.nc != classNames.Count)
            {
                errors.Add($"data.yaml class count does not match project classes. yaml nc:{yaml.nc}, project classes:{classNames.Count}.");
            }

            if (yaml.names == null || yaml.names.Count != classNames.Count)
            {
                errors.Add($"data.yaml class names do not match project classes. yaml names:{yaml.names?.Count ?? 0}, project classes:{classNames.Count}.");
                return;
            }

            for (int index = 0; index < classNames.Count; index++)
            {
                if (!string.Equals(yaml.names[index]?.Trim() ?? string.Empty, classNames[index], StringComparison.Ordinal))
                {
                    errors.Add($"data.yaml class name mismatch at index {index}. yaml:'{yaml.names[index]}', project:'{classNames[index]}'.");
                }
            }
        }

        private static void ValidatePath(
            string yamlFilePath,
            string yamlRootPath,
            string key,
            string yamlPath,
            string expectedPath,
            bool required,
            IList<string> errors)
        {
            if (string.IsNullOrWhiteSpace(yamlPath))
            {
                if (required)
                {
                    errors.Add($"data.yaml '{key}' path is missing.");
                }

                return;
            }

            string resolved = ResolvePath(yamlFilePath, yamlRootPath, yamlPath);
            string expected = NormalizeFullPath(expectedPath);
            if (!string.Equals(resolved, expected, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"data.yaml '{key}' path does not match the current project dataset. yaml:'{yamlPath}', resolved:'{resolved}', expected:'{expected}'.");
            }
        }

        private static string ResolvePath(string yamlFilePath, string yamlRootPath, string yamlPath)
        {
            if (string.IsNullOrWhiteSpace(yamlPath))
            {
                return string.Empty;
            }

            string normalizedPath = yamlPath.Replace('/', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(normalizedPath))
            {
                return NormalizeFullPath(normalizedPath);
            }

            string root = string.IsNullOrWhiteSpace(yamlRootPath)
                ? Path.GetDirectoryName(yamlFilePath)
                : yamlRootPath.Replace('/', Path.DirectorySeparatorChar);
            if (!Path.IsPathRooted(root))
            {
                string yamlDirectory = Path.GetDirectoryName(yamlFilePath) ?? string.Empty;
                root = Path.Combine(yamlDirectory, root);
            }

            return NormalizeFullPath(Path.Combine(root ?? string.Empty, normalizedPath));
        }

        private static string NormalizeFullPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private sealed class YoloDataYamlContract
        {
            public string path { get; set; } = "";
            public string train { get; set; } = "";
            public string val { get; set; } = "";
            public string test { get; set; } = "";
            public int nc { get; set; }
            public List<string> names { get; set; } = new List<string>();
        }
    }
}
