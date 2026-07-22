using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MvcVisionSystem._1._Core
{
    public static class PythonModelRuntimeBundledWorkerService
    {
        public const string UltralyticsWorkerRelativePath = @"Runtime\Python\openvisionlab_ultralytics_worker.py";
        public const string UnetWorkerRelativePath = @"Runtime\Python\openvisionlab_unet_worker.py";
        public const string SegmentationPredictionExporterRelativePath = @"Runtime\Python\openvisionlab_segmentation_prediction_export.py";
        public const string MobileSamBoxPromptWorkerRelativePath = @"Runtime\Python\openvisionlab_mobile_sam_box_prompt.py";

        public static string ResolveUltralyticsWorkerScriptPath()
        {
            return ResolveWorkerScriptPath(UltralyticsWorkerRelativePath);
        }

        public static string ResolveUnetWorkerScriptPath()
        {
            return ResolveWorkerScriptPath(UnetWorkerRelativePath);
        }

        public static string ResolveSegmentationPredictionExporterScriptPath()
        {
            return ResolveWorkerScriptPath(SegmentationPredictionExporterRelativePath);
        }

        public static string ResolveMobileSamBoxPromptWorkerScriptPath()
        {
            return ResolveWorkerScriptPath(MobileSamBoxPromptWorkerRelativePath);
        }

        public static string ResolveUnetWorkerRootPath()
        {
            string scriptPath = ResolveUnetWorkerScriptPath();
            string directory = Path.GetDirectoryName(scriptPath);
            return string.IsNullOrWhiteSpace(directory) ? AppContext.BaseDirectory : directory;
        }

        public static bool IsUnetWorkerScriptPath(string scriptPath)
        {
            return IsWorkerScriptPath(scriptPath, UnetWorkerRelativePath);
        }

        private static string ResolveWorkerScriptPath(string relativePath)
        {
            foreach (string candidate in EnumerateWorkerCandidates(relativePath))
            {
                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }

            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, relativePath));
        }

        public static string ResolveUltralyticsWorkerRootPath()
        {
            string scriptPath = ResolveUltralyticsWorkerScriptPath();
            string directory = Path.GetDirectoryName(scriptPath);
            return string.IsNullOrWhiteSpace(directory) ? AppContext.BaseDirectory : directory;
        }

        public static bool IsUltralyticsWorkerScriptPath(string scriptPath)
        {
            return IsWorkerScriptPath(scriptPath, UltralyticsWorkerRelativePath);
        }

        private static bool IsWorkerScriptPath(string scriptPath, string relativePath)
        {
            string trimmed = scriptPath?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return false;
            }

            string fileName = Path.GetFileName(trimmed);
            if (!string.Equals(fileName, Path.GetFileName(relativePath), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            try
            {
                string fullPath = Path.GetFullPath(trimmed);
                return EnumerateWorkerCandidates(relativePath)
                    .Any(candidate => string.Equals(Path.GetFullPath(candidate), fullPath, StringComparison.OrdinalIgnoreCase))
                    || File.Exists(fullPath);
            }
            catch
            {
                return false;
            }
        }

        private static IEnumerable<string> EnumerateWorkerCandidates(string relativePath)
        {
            string[] roots =
            {
                AppContext.BaseDirectory,
                Environment.CurrentDirectory,
                FindRepositoryRoot(AppContext.BaseDirectory),
                FindRepositoryRoot(Environment.CurrentDirectory)
            };

            return roots
                .Where(root => !string.IsNullOrWhiteSpace(root))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(root => Path.Combine(root, relativePath));
        }

        private static string FindRepositoryRoot(string startPath)
        {
            string current = string.IsNullOrWhiteSpace(startPath)
                ? string.Empty
                : Path.GetFullPath(startPath);
            if (File.Exists(current))
            {
                current = Path.GetDirectoryName(current) ?? string.Empty;
            }

            while (!string.IsNullOrWhiteSpace(current))
            {
                if (File.Exists(Path.Combine(current, "MvcVisionSystem.csproj")))
                {
                    return current;
                }

                current = Directory.GetParent(current)?.FullName ?? string.Empty;
            }

            return string.Empty;
        }
    }
}
