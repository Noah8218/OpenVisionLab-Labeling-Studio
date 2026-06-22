using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem.Yolo
{
    public static class YoloDatasetDiagnosticsService
    {
        private const int RecommendedMinimumObjectsPerClass = 5;
        private const double ClassBalanceWarningRatio = 5D;

        public static IReadOnlyList<string> BuildOperatorReport(CData data, bool refreshYaml)
        {
            var lines = new List<string>();
            YoloDatasetReadinessReport report = YoloDatasetReadinessService.Build(data, refreshYaml);

            lines.Add(report.IsReady ? "YOLO dataset diagnosis: READY" : "YOLO dataset diagnosis: NOT READY");
            if (data != null)
            {
                lines.Add($"YOLO output root: {data.OutputRootPath}");
                lines.Add($"YOLO data.yaml: {data.DataYamlFilePath}");
            }

            if (report.IsReady)
            {
                lines.AddRange(report.SummaryLines);
                AppendQualityHints(lines, data, report.Statistics);
                return lines;
            }

            foreach (string error in report.Errors)
            {
                lines.Add($"YOLO dataset issue: {error}");
            }

            return lines;
        }

        private static void AppendQualityHints(List<string> lines, CData data, YoloDatasetStatistics statistics)
        {
            if (statistics == null)
            {
                return;
            }

            lines.Add($"YOLO split detail. Train:{statistics.TrainImageCount}, Valid:{statistics.ValidImageCount}, Test:{statistics.TestImageCount}");
            lines.Add("YOLO split guide: Validation checks training while it runs. Test is reserved for final model comparison.");

            if (statistics.TestImageCount == 0)
            {
                lines.Add("YOLO dataset warning: Test split is empty. Set Test % before final model comparison.");
            }

            List<string> classNames = data?.ClassNamedList?
                .Select(item => item?.Text?.Trim() ?? string.Empty)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(System.StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            if (classNames.Count == 0)
            {
                classNames = statistics.ObjectCountByClass.Keys.OrderBy(name => name).ToList();
            }

            if (classNames.Count == 0)
            {
                return;
            }

            var classCounts = new List<KeyValuePair<string, int>>();
            foreach (string className in classNames)
            {
                statistics.ObjectCountByClass.TryGetValue(className, out int count);
                classCounts.Add(new KeyValuePair<string, int>(className, count));
            }

            lines.Add($"YOLO class balance. {string.Join(", ", classCounts.Select(item => $"{item.Key}:{item.Value}"))}");

            foreach (KeyValuePair<string, int> item in classCounts.Where(item => item.Value < RecommendedMinimumObjectsPerClass))
            {
                lines.Add($"YOLO dataset warning: class '{item.Key}' has only {item.Value} object(s). Add more labeled examples before trusting training.");
            }

            List<KeyValuePair<string, int>> positiveClassCounts = classCounts
                .Where(item => item.Value > 0)
                .OrderBy(item => item.Value)
                .ToList();

            if (positiveClassCounts.Count < 2)
            {
                return;
            }

            KeyValuePair<string, int> smallest = positiveClassCounts[0];
            KeyValuePair<string, int> largest = positiveClassCounts[positiveClassCounts.Count - 1];
            if (largest.Value >= smallest.Value * ClassBalanceWarningRatio)
            {
                lines.Add($"YOLO dataset warning: class balance is skewed. Smallest '{smallest.Key}'={smallest.Value}, largest '{largest.Key}'={largest.Value}.");
            }
        }
    }
}
