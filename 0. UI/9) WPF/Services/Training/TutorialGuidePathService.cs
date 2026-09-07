using System;
using System.IO;

namespace MvcVisionSystem
{
    /// <summary>
    /// Resolves the checked-in tutorial guide without coupling path discovery to the WPF Shell.
    /// </summary>
    public static class TutorialGuidePathService
    {
        private const string TutorialHtmlGuideRelativePath = @"docs\tutorial\labeling-workbench-tutorial.html";

        public static string ResolveTutorialHtmlGuidePath()
        {
            string[] searchRoots =
            {
                Environment.CurrentDirectory,
                AppContext.BaseDirectory
            };

            foreach (string root in searchRoots)
            {
                string path = FindRelativeFileFromAncestor(root, TutorialHtmlGuideRelativePath);
                if (!string.IsNullOrWhiteSpace(path))
                {
                    return path;
                }
            }

            return Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, TutorialHtmlGuideRelativePath));
        }

        public static string FindRelativeFileFromAncestor(string startPath, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(startPath) || string.IsNullOrWhiteSpace(relativePath))
            {
                return string.Empty;
            }

            DirectoryInfo directory;
            try
            {
                directory = new DirectoryInfo(Path.GetFullPath(startPath));
            }
            catch
            {
                return string.Empty;
            }

            if (!directory.Exists && directory.Parent != null)
            {
                directory = directory.Parent;
            }

            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, relativePath);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }

            return string.Empty;
        }
    }

    [Obsolete("Use TutorialGuidePathService.", false)]
    public static class WpfTutorialGuidePathService
    {
        public static string ResolveTutorialHtmlGuidePath() => TutorialGuidePathService.ResolveTutorialHtmlGuidePath();

        public static string FindRelativeFileFromAncestor(string startPath, string relativePath)
            => TutorialGuidePathService.FindRelativeFileFromAncestor(startPath, relativePath);
    }
}
