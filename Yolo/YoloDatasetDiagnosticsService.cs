using System.Collections.Generic;

namespace MvcVisionSystem.Yolo
{
    public static class YoloDatasetDiagnosticsService
    {
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
                return lines;
            }

            foreach (string error in report.Errors)
            {
                lines.Add($"YOLO dataset issue: {error}");
            }

            return lines;
        }
    }
}
