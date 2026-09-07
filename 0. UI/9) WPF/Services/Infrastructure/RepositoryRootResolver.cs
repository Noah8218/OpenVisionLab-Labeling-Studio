using System;
using System.IO;

namespace MvcVisionSystem
{
    public static class RepositoryRootResolver
    {
        private static readonly string[] RepositoryMarkers =
        {
            "OpenVisionLab.LabelingStudio.sln",
            "OpenVisionLab.LabelingStudio.csproj",
            "MvcVisionSystem.sln",
            "MvcVisionSystem.csproj"
        };

        public static string FindRepositoryRoot(string startPath = "")
        {
            string[] starts =
            {
                startPath,
                Environment.CurrentDirectory,
                AppContext.BaseDirectory
            };

            foreach (string start in starts)
            {
                if (string.IsNullOrWhiteSpace(start))
                {
                    continue;
                }

                DirectoryInfo current;
                try
                {
                    string fullPath = Path.GetFullPath(start);
                    if (File.Exists(fullPath))
                    {
                        fullPath = Path.GetDirectoryName(fullPath) ?? string.Empty;
                    }

                    current = new DirectoryInfo(fullPath);
                }
                catch
                {
                    continue;
                }

                while (current != null)
                {
                    foreach (string marker in RepositoryMarkers)
                    {
                        if (File.Exists(Path.Combine(current.FullName, marker)))
                        {
                            return current.FullName;
                        }
                    }

                    current = current.Parent;
                }
            }

            return Environment.CurrentDirectory;
        }
    }

    [Obsolete("Use RepositoryRootResolver.", false)]
    public static class WpfRepositoryRootResolver
    {
        public static string FindRepositoryRoot(string startPath = "") => RepositoryRootResolver.FindRepositoryRoot(startPath);
    }
}
