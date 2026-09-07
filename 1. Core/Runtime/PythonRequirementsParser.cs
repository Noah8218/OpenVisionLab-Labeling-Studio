using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MvcVisionSystem._1._Core
{
    internal static class PythonRequirementsParser
    {
        internal static IReadOnlyList<string> ReadPackageNames(
            string requirementsPath,
            List<string> warnings,
            List<string> errors)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var packages = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            ReadPackageNames(requirementsPath, packages, visited, warnings, errors);
            return packages.ToList();
        }

        internal static IReadOnlyCollection<string> ParseInstalledPackageNames(string json)
        {
            try
            {
                List<PipPackageInfo> packages = JsonConvert.DeserializeObject<List<PipPackageInfo>>(json) ?? new List<PipPackageInfo>();
                return packages
                    .Select(package => NormalizePackageName(package.Name))
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        internal static string NormalizePackageName(string packageName)
        {
            return (packageName ?? string.Empty).Trim().Replace('_', '-').ToLowerInvariant();
        }

        internal static bool IsSafePackageName(string packageName)
        {
            return !string.IsNullOrWhiteSpace(packageName)
                && Regex.IsMatch(packageName, @"^[A-Za-z0-9_.-]+$");
        }

        private static void ReadPackageNames(
            string requirementsPath,
            ISet<string> packages,
            ISet<string> visited,
            List<string> warnings,
            List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(requirementsPath) || !File.Exists(requirementsPath))
            {
                errors.Add($"requirements.txt 파일을 찾을 수 없습니다: {requirementsPath}");
                return;
            }

            string fullPath = Path.GetFullPath(requirementsPath);
            if (!visited.Add(fullPath))
            {
                return;
            }

            string baseDirectory = Path.GetDirectoryName(fullPath) ?? string.Empty;
            foreach (string rawLine in File.ReadLines(fullPath))
            {
                string line = StripComment(rawLine).Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                if (line.StartsWith("-r ", StringComparison.OrdinalIgnoreCase)
                    || line.StartsWith("--requirement ", StringComparison.OrdinalIgnoreCase))
                {
                    string includePath = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
                    string nestedPath = Path.IsPathRooted(includePath) ? includePath : Path.Combine(baseDirectory, includePath);
                    ReadPackageNames(nestedPath, packages, visited, warnings, errors);
                    continue;
                }

                if (line.StartsWith("-", StringComparison.Ordinal)
                    || line.StartsWith("--", StringComparison.Ordinal))
                {
                    continue;
                }

                string packageName = TryExtractPackageName(line);
                if (!string.IsNullOrWhiteSpace(packageName))
                {
                    packages.Add(packageName);
                }
            }
        }

        private static string StripComment(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return string.Empty;
            }

            int index = line.IndexOf(" #", StringComparison.Ordinal);
            return index >= 0 ? line.Substring(0, index) : line;
        }

        private static string TryExtractPackageName(string requirementLine)
        {
            string line = requirementLine.Split(';')[0].Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                return string.Empty;
            }

            Match eggMatch = Regex.Match(line, @"[#&]egg=([A-Za-z0-9_.-]+)");
            if (eggMatch.Success)
            {
                return eggMatch.Groups[1].Value;
            }

            Match packageMatch = Regex.Match(line, @"^([A-Za-z0-9_.-]+)(?:\[[^\]]+\])?\s*(?:[<>=!~]=?.*)?$");
            return packageMatch.Success ? packageMatch.Groups[1].Value : string.Empty;
        }

        private sealed class PipPackageInfo
        {
            public string Name { get; set; } = string.Empty;
        }
    }
}
