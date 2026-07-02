using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MvcVisionSystem._1._Core
{
    public static class PythonModelRuntimeBundledWorkerService
    {
        public const string UltralyticsWorkerRelativePath = @"Runtime\Python\openvisionlab_ultralytics_worker.py";

        public static string ResolveUltralyticsWorkerScriptPath()
        {
            foreach (string candidate in EnumerateUltralyticsWorkerCandidates())
            {
                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }

            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, UltralyticsWorkerRelativePath));
        }

        public static string ResolveUltralyticsWorkerRootPath()
        {
            string scriptPath = ResolveUltralyticsWorkerScriptPath();
            string directory = Path.GetDirectoryName(scriptPath);
            return string.IsNullOrWhiteSpace(directory) ? AppContext.BaseDirectory : directory;
        }

        public static bool IsUltralyticsWorkerScriptPath(string scriptPath)
        {
            string trimmed = scriptPath?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return false;
            }

            string fileName = Path.GetFileName(trimmed);
            if (!string.Equals(fileName, Path.GetFileName(UltralyticsWorkerRelativePath), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            try
            {
                string fullPath = Path.GetFullPath(trimmed);
                return EnumerateUltralyticsWorkerCandidates()
                    .Any(candidate => string.Equals(Path.GetFullPath(candidate), fullPath, StringComparison.OrdinalIgnoreCase))
                    || File.Exists(fullPath);
            }
            catch
            {
                return false;
            }
        }

        private static IEnumerable<string> EnumerateUltralyticsWorkerCandidates()
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
                .Select(root => Path.Combine(root, UltralyticsWorkerRelativePath));
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
