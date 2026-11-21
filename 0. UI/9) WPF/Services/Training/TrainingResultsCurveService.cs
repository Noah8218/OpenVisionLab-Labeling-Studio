using MvcVisionSystem._1._Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MvcVisionSystem
{
    // Reads the per-epoch curve of a finished Ultralytics run from results.csv beside its weights
    // (runs/<task>/<name>/weights/best.pt -> runs/<task>/<name>/results.csv), in the same shape the worker
    // reports live: train and validation loss are the sums of their loss columns, and the validation metric is
    // mAP50 of the masks when present (maskMap50), otherwise of the boxes (map50). Read-only; a missing or
    // unreadable file gives an empty curve.
    public static class TrainingResultsCurveService
    {
        public static IReadOnlyList<TrainingEpochMetric> Read(string weightsPath)
        {
            try
            {
                string directory = Path.GetDirectoryName(weightsPath?.Trim() ?? string.Empty);
                if (string.IsNullOrEmpty(directory)) return Array.Empty<TrainingEpochMetric>();
                string runDirectory = string.Equals(Path.GetFileName(directory), "weights", StringComparison.OrdinalIgnoreCase) ? Path.GetDirectoryName(directory) : directory;
                string path = Path.Combine(runDirectory ?? string.Empty, "results.csv");
                return File.Exists(path) ? Parse(File.ReadAllLines(path)) : Array.Empty<TrainingEpochMetric>();
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                return Array.Empty<TrainingEpochMetric>();
            }
        }

        internal static IReadOnlyList<TrainingEpochMetric> Parse(IReadOnlyList<string> lines)
        {
            if (lines == null || lines.Count < 2) return Array.Empty<TrainingEpochMetric>();
            string[] header = lines[0].Split(',').Select(name => name.Trim()).ToArray();
            int epoch = Array.IndexOf(header, "epoch");
            int[] train = header.Select((name, index) => (name, index)).Where(item => item.name.StartsWith("train/", StringComparison.Ordinal) && item.name.EndsWith("_loss", StringComparison.Ordinal)).Select(item => item.index).ToArray();
            int[] validation = header.Select((name, index) => (name, index)).Where(item => item.name.StartsWith("val/", StringComparison.Ordinal) && item.name.EndsWith("_loss", StringComparison.Ordinal)).Select(item => item.index).ToArray();
            int mask = Array.IndexOf(header, "metrics/mAP50(M)"), box = Array.IndexOf(header, "metrics/mAP50(B)");
            int metric = mask >= 0 ? mask : box;
            string key = mask >= 0 ? "maskMap50" : "map50";
            if (epoch < 0) return Array.Empty<TrainingEpochMetric>();
            var result = new List<TrainingEpochMetric>();
            foreach (string line in lines.Skip(1))
            {
                string[] cells = line.Split(',');
                if (cells.Length != header.Length || !int.TryParse(cells[epoch].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)) continue;
                double? Sum(int[] columns) { double total = 0; foreach (int column in columns) { if (!TryRead(cells[column], out double value)) return null; total += value; } return columns.Length > 0 ? total : null; }
                double? metricValue = metric >= 0 && TryRead(cells[metric], out double read) ? read : null;
                result.Add(new TrainingEpochMetric(number, Sum(train), Sum(validation), metricValue.HasValue ? key : string.Empty, metricValue));
            }
            return result;
        }

        private static bool TryRead(string text, out double value)
            => double.TryParse(text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
    }
}
