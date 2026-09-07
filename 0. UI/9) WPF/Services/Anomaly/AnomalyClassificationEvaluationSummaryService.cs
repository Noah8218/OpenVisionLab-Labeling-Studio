using MvcVisionSystem.Yolo;
using System;
using System.IO;
using System.Linq;

namespace MvcVisionSystem
{
    public class AnomalyClassificationEvaluationSummary
    {
        public AnomalyClassificationEvaluationSummary(
            string summaryPath,
            AnomalyClassificationEvaluationReport report,
            AnomalyClassificationEvaluationOptions options)
        {
            SummaryPath = summaryPath ?? string.Empty;
            Report = report ?? new AnomalyClassificationEvaluationReport();
            Options = options ?? new AnomalyClassificationEvaluationOptions();
        }

        public string SummaryPath { get; }

        public AnomalyClassificationEvaluationReport Report { get; }

        public AnomalyClassificationEvaluationOptions Options { get; }
    }

    public class AnomalyClassificationEvaluationSummaryService
    {
        private const string SummaryFileName = "classification-evaluation-summary.json";

        public string ResolveSummaryPath(string outputRootPath, string preferredSummaryPath = "")
        {
            string preferredPath = preferredSummaryPath?.Trim() ?? string.Empty;
            if (File.Exists(preferredPath))
            {
                return preferredPath;
            }

            if (string.IsNullOrWhiteSpace(outputRootPath))
            {
                return string.Empty;
            }

            string root = outputRootPath.Trim();
            string directPath = Path.Combine(root, SummaryFileName);
            if (File.Exists(directPath))
            {
                return directPath;
            }

            string evaluationPath = Path.Combine(root, "classification-evaluation", SummaryFileName);
            if (File.Exists(evaluationPath))
            {
                return evaluationPath;
            }

            try
            {
                if (!Directory.Exists(root))
                {
                    return string.Empty;
                }

                return Directory
                    .EnumerateDirectories(root, "classification-evaluation-*", SearchOption.TopDirectoryOnly)
                    .Select(directory => new FileInfo(Path.Combine(directory, SummaryFileName)))
                    .Where(summary => summary.Exists)
                    .OrderByDescending(summary => summary.LastWriteTimeUtc)
                    .Select(summary => summary.FullName)
                    .FirstOrDefault() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public bool TryReadSummary(
            string summaryPath,
            out AnomalyClassificationEvaluationSummary summary)
        {
            summary = null;
            if (string.IsNullOrWhiteSpace(summaryPath) || !File.Exists(summaryPath))
            {
                return false;
            }

            try
            {
                AnomalyClassificationEvaluationReport report =
                    AnomalyClassificationEvaluationService.ReadSummaryFile(summaryPath, out AnomalyClassificationEvaluationOptions options);
                summary = new AnomalyClassificationEvaluationSummary(summaryPath, report, options);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    [Obsolete("Use AnomalyClassificationEvaluationSummaryService.", false)]
    public sealed class WpfAnomalyClassificationEvaluationSummaryService : AnomalyClassificationEvaluationSummaryService
    {
        public bool TryReadSummary(
            string summaryPath,
            out WpfAnomalyClassificationEvaluationSummary summary)
        {
            summary = null;
            if (!base.TryReadSummary(summaryPath, out AnomalyClassificationEvaluationSummary canonical))
            {
                return false;
            }

            summary = WpfAnomalyClassificationEvaluationSummary.FromCanonical(canonical);
            return true;
        }
    }

    [Obsolete("Use AnomalyClassificationEvaluationSummary.", false)]
    public sealed class WpfAnomalyClassificationEvaluationSummary : AnomalyClassificationEvaluationSummary
    {
        public WpfAnomalyClassificationEvaluationSummary(
            string summaryPath,
            AnomalyClassificationEvaluationReport report,
            AnomalyClassificationEvaluationOptions options)
            : base(summaryPath, report, options)
        {
        }

        internal static WpfAnomalyClassificationEvaluationSummary FromCanonical(AnomalyClassificationEvaluationSummary source)
        {
            return source == null
                ? null
                : new WpfAnomalyClassificationEvaluationSummary(source.SummaryPath, source.Report, source.Options);
        }
    }
}
