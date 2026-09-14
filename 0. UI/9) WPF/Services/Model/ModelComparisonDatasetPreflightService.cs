using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the run-side dataset preflight needed before model comparison starts.
    /// Process construction and execution remain in ModelComparisonRunService.
    /// </summary>
    public sealed class ModelComparisonDatasetPreflightService
    {
        public string ResolveEngineComparisonTask(string dataYamlPath, string requestedTask)
        {
            if (string.Equals(requestedTask, "val", StringComparison.OrdinalIgnoreCase))
            {
                return "val";
            }

            if (string.Equals(requestedTask, "test", StringComparison.OrdinalIgnoreCase))
            {
                return "test";
            }

            if (string.IsNullOrWhiteSpace(dataYamlPath) || !File.Exists(dataYamlPath))
            {
                return "test";
            }

            Dictionary<string, string> values = ReadDataYamlScalarValues(dataYamlPath);
            if (!values.TryGetValue("test", out string splitPath) || string.IsNullOrWhiteSpace(splitPath))
            {
                return "val";
            }

            string yamlRootPath = values.TryGetValue("path", out string rootPath) ? rootPath : string.Empty;
            string resolved = ResolveDataYamlPath(dataYamlPath, yamlRootPath, splitPath);
            return CountDataYamlImages(resolved) > 0 ? "test" : "val";
        }

        public IReadOnlyList<string> ValidateDataYamlSplitImages(ModelComparisonRunRequest request)
        {
            var errors = new List<string>();
            if (request == null || string.IsNullOrWhiteSpace(request.DataYamlPath) || !File.Exists(request.DataYamlPath))
            {
                return errors;
            }

            string task = string.Equals(request.Task, "val", StringComparison.OrdinalIgnoreCase) ? "val" : "test";
            Dictionary<string, string> values = ReadDataYamlScalarValues(request.DataYamlPath);
            if (!values.TryGetValue(task, out string splitPath) || string.IsNullOrWhiteSpace(splitPath))
            {
                errors.Add($"\uD559\uC2B5 \uC124\uC815\uC5D0 \uCD5C\uC885 \uAC80\uC99D \uC774\uBBF8\uC9C0 \uACBD\uB85C\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4. \uBAA8\uB378 \uBE44\uAD50 \uC804 {task} \uC774\uBBF8\uC9C0\uB97C 1\uC7A5 \uC774\uC0C1 \uD655\uBCF4\uD558\uC138\uC694.");
                return errors;
            }

            string yamlRootPath = values.TryGetValue("path", out string rootPath) ? rootPath : string.Empty;
            string resolved = ResolveDataYamlPath(request.DataYamlPath, yamlRootPath, splitPath);
            int imageCount = CountDataYamlImages(resolved);
            if (imageCount <= 0)
            {
                errors.Add($"\uD559\uC2B5 \uC124\uC815\uC758 {task} \uBD84\uD560\uC5D0 \uC774\uBBF8\uC9C0\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4: {resolved}");
                return errors;
            }

            int labelCount = CountDataYamlLabelFiles(resolved);
            if (labelCount <= 0)
            {
                errors.Add($"\uD559\uC2B5 \uC124\uC815\uC758 {task} \uBD84\uD560\uC5D0 \uC815\uB2F5 \uB77C\uBCA8 \uD30C\uC77C\uC774 \uC5C6\uC2B5\uB2C8\uB2E4: {resolved}");
                return errors;
            }

            if (string.Equals(request.ModelTask, "segment", StringComparison.OrdinalIgnoreCase)
                && CountDataYamlSegmentationLabelLines(resolved) <= 0)
            {
                errors.Add($"Model comparison needs at least one positive segmentation label line in the {task} split before YOLOv8 SEG validation: {resolved}");
            }

            return errors;
        }

        public Dictionary<string, string> ReadDataYamlScalarValues(string dataYamlPath)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string rawLine in File.ReadLines(dataYamlPath))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                int separatorIndex = line.IndexOf(':');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                string key = line.Substring(0, separatorIndex).Trim();
                string value = StripYamlScalarValue(line.Substring(separatorIndex + 1).Trim());
                if (!string.IsNullOrWhiteSpace(key))
                {
                    values[key] = value;
                }
            }

            return values;
        }

        public string ResolveDataYamlPath(string yamlFilePath, string yamlRootPath, string yamlPath)
        {
            string normalizedYamlPath = (yamlPath ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(normalizedYamlPath))
            {
                return Path.GetFullPath(normalizedYamlPath);
            }

            string yamlDirectory = Path.GetDirectoryName(yamlFilePath) ?? Directory.GetCurrentDirectory();
            string root = string.IsNullOrWhiteSpace(yamlRootPath)
                ? yamlDirectory
                : yamlRootPath.Replace('/', Path.DirectorySeparatorChar);
            if (!Path.IsPathRooted(root))
            {
                root = Path.Combine(yamlDirectory, root);
            }

            return Path.GetFullPath(Path.Combine(root, normalizedYamlPath));
        }

        private int CountDataYamlImages(string resolvedPath)
        {
            if (string.IsNullOrWhiteSpace(resolvedPath))
            {
                return 0;
            }

            if (Directory.Exists(resolvedPath))
            {
                return Directory
                    .EnumerateFiles(resolvedPath, "*.*", SearchOption.TopDirectoryOnly)
                    .Count(IsSupportedImagePath);
            }

            if (File.Exists(resolvedPath))
            {
                string directory = Path.GetDirectoryName(resolvedPath) ?? Directory.GetCurrentDirectory();
                return File
                    .ReadLines(resolvedPath)
                    .Select(line => RemoveYamlInlineComment(line).Trim())
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Select(line => ResolveListImagePath(directory, line))
                    .Count(path => File.Exists(path) && IsSupportedImagePath(path));
            }

            return 0;
        }

        private int CountDataYamlLabelFiles(string resolvedPath)
        {
            if (string.IsNullOrWhiteSpace(resolvedPath))
            {
                return 0;
            }

            if (Directory.Exists(resolvedPath))
            {
                return Directory
                    .EnumerateFiles(resolvedPath, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(IsSupportedImagePath)
                    .Select(ResolveLabelPathFromImagePath)
                    .Count(File.Exists);
            }

            if (File.Exists(resolvedPath))
            {
                string directory = Path.GetDirectoryName(resolvedPath) ?? Directory.GetCurrentDirectory();
                return File
                    .ReadLines(resolvedPath)
                    .Select(line => RemoveYamlInlineComment(line).Trim())
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Select(line => ResolveListImagePath(directory, line))
                    .Select(ResolveLabelPathFromImagePath)
                    .Count(File.Exists);
            }

            return 0;
        }

        private int CountDataYamlSegmentationLabelLines(string resolvedPath)
        {
            return EnumerateDataYamlLabelPaths(resolvedPath)
                .Where(File.Exists)
                .SelectMany(File.ReadLines)
                .Select(line => RemoveYamlInlineComment(line).Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Count(IsSegmentationLabelLine);
        }

        private IEnumerable<string> EnumerateDataYamlLabelPaths(string resolvedPath)
        {
            if (string.IsNullOrWhiteSpace(resolvedPath))
            {
                yield break;
            }

            if (Directory.Exists(resolvedPath))
            {
                foreach (string labelPath in Directory
                    .EnumerateFiles(resolvedPath, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(IsSupportedImagePath)
                    .Select(ResolveLabelPathFromImagePath))
                {
                    yield return labelPath;
                }

                yield break;
            }

            if (!File.Exists(resolvedPath))
            {
                yield break;
            }

            string directory = Path.GetDirectoryName(resolvedPath) ?? Directory.GetCurrentDirectory();
            foreach (string labelPath in File
                .ReadLines(resolvedPath)
                .Select(line => RemoveYamlInlineComment(line).Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => ResolveLabelPathFromImagePath(ResolveListImagePath(directory, line))))
            {
                yield return labelPath;
            }
        }

        private static bool IsSegmentationLabelLine(string line)
        {
            string[] tokens = (line ?? string.Empty)
                .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length < 7 || tokens.Length % 2 == 0)
            {
                return false;
            }

            return tokens.All(token => double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out _));
        }

        private static string ResolveLabelsDirectoryFromImagesPath(string imagesPath)
        {
            string normalized = Path.GetFullPath(imagesPath ?? string.Empty)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string directoryName = Path.GetFileName(normalized);
            string parent = Path.GetDirectoryName(normalized) ?? string.Empty;
            if (string.Equals(directoryName, "images", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(parent))
            {
                return Path.Combine(parent, "labels");
            }

            return Path.Combine(normalized, "labels");
        }

        private static string ResolveLabelPathFromImagePath(string imagePath)
        {
            string directory = Path.GetDirectoryName(imagePath) ?? string.Empty;
            string labelsDirectory = ResolveLabelsDirectoryFromImagesPath(directory);
            return Path.Combine(labelsDirectory, Path.GetFileNameWithoutExtension(imagePath) + ".txt");
        }

        private static bool IsSupportedImagePath(string path)
        {
            string extension = Path.GetExtension(path);
            return string.Equals(extension, ".bmp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".tif", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".tiff", StringComparison.OrdinalIgnoreCase);
        }

        private static string StripYamlScalarValue(string value)
        {
            value = RemoveYamlInlineComment(value ?? string.Empty).Trim();
            if (value.Length >= 2
                && ((value[0] == '"' && value[value.Length - 1] == '"')
                    || (value[0] == '\'' && value[value.Length - 1] == '\'')))
            {
                value = value.Substring(1, value.Length - 2);
            }

            return value.Trim();
        }

        private static string RemoveYamlInlineComment(string value)
        {
            int commentIndex = value.IndexOf('#');
            return commentIndex >= 0 ? value.Substring(0, commentIndex) : value;
        }

        private static string ResolveListImagePath(string listDirectory, string imagePath)
        {
            string normalized = (imagePath ?? string.Empty).Replace('/', Path.DirectorySeparatorChar);
            return Path.IsPathRooted(normalized)
                ? Path.GetFullPath(normalized)
                : Path.GetFullPath(Path.Combine(listDirectory, normalized));
        }
    }
}
