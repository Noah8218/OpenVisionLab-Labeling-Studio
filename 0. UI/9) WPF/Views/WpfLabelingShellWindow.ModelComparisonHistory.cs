using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        private WpfModelComparisonHistoryItem RefreshModelComparisonHistoryItems(
            string baselineWeightsPath,
            string candidateWeightsPath,
            string preferredSummaryPath = "")
        {
            if (string.IsNullOrWhiteSpace(baselineWeightsPath)
                || string.IsNullOrWhiteSpace(candidateWeightsPath))
            {
                CandidateReviewViewModel.SetModelComparisonHistory(Array.Empty<WpfModelComparisonHistoryItem>());
                return null;
            }

            IReadOnlyList<WpfModelComparisonHistoryItem> items = modelComparisonReviewService.BuildHistory(
                baselineWeightsPath,
                candidateWeightsPath,
                maxItems: 8);
            CandidateReviewViewModel.SetModelComparisonHistory(items, preferredSummaryPath);
            return CandidateReviewViewModel.SelectedModelComparisonHistoryItem;
        }

        private WpfModelComparisonReviewReport BuildModelComparisonHistoryReport(
            WpfModelComparisonHistoryItem item)
        {
            if (item == null)
            {
                return WpfModelComparisonReviewReport.Empty;
            }

            IReadOnlyList<string> classNames = global.Data?.ClassNamedList == null
                ? Array.Empty<string>()
                : global.Data.ClassNamedList
                    .Select(classItem => classItem?.Text ?? string.Empty)
                    .ToList();
            double confidence = global.Data?.ProjectSettings?.PythonModel?.MinimumDetectionConfidence ?? 0.25D;
            return modelComparisonReviewService.BuildFromSummaryFile(
                item.SourcePath,
                classNames,
                confidence,
                maxExamples: 5);
        }

        private void ExecuteModelComparisonHistorySelectionChangedCommand(object selectedItem)
        {
            if (selectedItem is not WpfModelComparisonHistoryItem item)
            {
                return;
            }

            WpfModelComparisonReviewReport report = BuildModelComparisonHistoryReport(item);
            if (!report.HasComparison)
            {
                AppendLog($"\uBAA8\uB378 \uBE44\uAD50 \uC774\uB825 \uBD88\uB7EC\uC624\uAE30 \uC2E4\uD328: {item.SourcePath}");
                return;
            }

            CandidateReviewViewModel.SetModelComparisonSourceText(
                $"{item.DisplayText} / {item.DetailText} / {item.SourcePath}");
            CandidateReviewViewModel.SetModelComparisonReview(
                report,
                isHistoricalSelection: !item.IsLatest);
            try
            {
                RefreshModelCenterDashboard(BuildCurrentTrainingWeightsComparison());
            }
            catch (Exception ex)
            {
                AppendLog($"\uBAA8\uB378 \uBE44\uAD50 \uC774\uB825 \uD310\uB2E8 \uAC31\uC2E0 \uC2E4\uD328: {ex.Message}");
            }

            AppendLog($"\uBAA8\uB378 \uBE44\uAD50 \uC774\uB825 \uC120\uD0DD: {item.DisplayText}");
        }
    }
}
