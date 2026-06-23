using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MvcVisionSystem
{
    public sealed class WpfTrainingWeightsService
    {
        public bool TryFindLatestTrainingWeights(string projectRootPath, string outputRootPath, out string latestWeightsPath)
        {
            latestWeightsPath = EnumerateBestWeightCandidates(projectRootPath)
                .Concat(EnumerateBestWeightCandidates(outputRootPath))
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault() ?? string.Empty;

            return !string.IsNullOrWhiteSpace(latestWeightsPath);
        }

        public IReadOnlyList<string> EnumerateBestWeightCandidates(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            {
                return Array.Empty<string>();
            }

            var candidates = new List<string>
            {
                Path.Combine(rootPath, "best.pt")
            };

            string trainRunsRoot = Path.Combine(rootPath, "runs", "train");
            if (!Directory.Exists(trainRunsRoot))
            {
                return candidates;
            }

            candidates.Add(Path.Combine(trainRunsRoot, "weights", "best.pt"));
            foreach (string runDirectory in Directory.EnumerateDirectories(trainRunsRoot))
            {
                candidates.Add(Path.Combine(runDirectory, "weights", "best.pt"));
            }

            return candidates;
        }

        public static bool ShouldPreferTrainingWeights(string latestWeightsPath, string currentWeightsPath)
        {
            if (string.IsNullOrWhiteSpace(latestWeightsPath) || !File.Exists(latestWeightsPath))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(currentWeightsPath) || !File.Exists(currentWeightsPath))
            {
                return true;
            }

            return File.GetLastWriteTimeUtc(latestWeightsPath) >= File.GetLastWriteTimeUtc(currentWeightsPath);
        }

        public static bool IsCompletedTrainingState(string state)
            => string.Equals(state?.Trim(), "completed", StringComparison.OrdinalIgnoreCase);
    }
}