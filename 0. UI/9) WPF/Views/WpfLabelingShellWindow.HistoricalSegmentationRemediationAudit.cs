using MvcVisionSystem.Yolo;
using System;
using System.IO;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        private bool isHistoricalSegmentationRemediationAuditRunning;

        // The report is deliberately separate from the later, user-approved migration path.
        private async void ExecuteHistoricalSegmentationRemediationAuditCommand()
        {
            if (isHistoricalSegmentationRemediationAuditRunning)
            {
                return;
            }

            CData data = global.Data;
            string outputPath = YoloSegmentationHistoricalRemediationAuditService.ResolveDefaultOutputPath(data);
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                SetModelStatus("SEG \uBCF4\uC815 \uAC80\uD1A0 \uC2E4\uD328: \uB370\uC774\uD130\uC14B \uC800\uC7A5 \uD3F4\uB354\uB97C \uBA3C\uC800 \uC9C0\uC815\uD558\uC138\uC694.");
                return;
            }

            string sourceImagePath = TemplateMatchingAutoLabelViewModel?.RegisteredTemplateSourceImagePath ?? string.Empty;
            isHistoricalSegmentationRemediationAuditRunning = true;
            SetModelStatus("SEG \uBCF4\uC815 \uAC80\uD1A0: \uAE30\uC874 \uB9C8\uC2A4\uD06C\uC640 YOLO \uB77C\uBCA8\uC744 \uC77D\uAE30 \uC804\uC6A9\uC73C\uB85C \uBE44\uAD50 \uC911");
            try
            {
                (YoloSegmentationHistoricalRemediationAuditReport Report, YoloSegmentationHistoricalRemediationAuditExportResult Export) result =
                    await Task.Run(() =>
                    {
                        YoloSegmentationHistoricalRemediationAuditReport report =
                            YoloSegmentationHistoricalRemediationAuditService.Build(data, sourceImagePath);
                        return (report, YoloSegmentationHistoricalRemediationAuditService.ExportMarkdown(report, outputPath));
                    });

                string sourceSummary = result.Report.ExcludedSourceImageCount > 0
                    ? $"\uAE30\uC900 \uC774\uBBF8\uC9C0 \uC81C\uC678 {result.Report.ExcludedSourceImageCount}\uC7A5"
                    : "\uAE30\uC900 \uC774\uBBF8\uC9C0 \uC81C\uC678 \uC5C6\uC74C";
                string errorSummary = result.Report.HasErrors
                    ? $" / \uD655\uC778 \uD544\uC694 {result.Report.UnresolvedRecordCount}\uAC74"
                    : string.Empty;
                SetModelStatus(
                    $"SEG \uBCF4\uC815 \uAC80\uD1A0 \uBCF4\uACE0\uC11C \uC800\uC7A5: {Path.GetFileName(result.Export.OutputPath)} / \uB300\uC0C1 {result.Report.CandidateImageCount}\uC7A5 / \uB77C\uBCA8 \uCC28\uC774 {result.Report.ChangedYoloLabelImageCount}\uC7A5 / {sourceSummary}{errorSummary}");
                AppendLog(
                    $"SEG remediation dry run saved: {result.Export.OutputPath} / images {result.Report.CandidateImageCount} / records {result.Report.CandidateRecordCount} / changed labels {result.Report.ChangedYoloLabelImageCount} / excluded sources {result.Report.ExcludedSourceImageCount}");
            }
            catch (Exception ex)
            {
                SetModelStatus($"SEG \uBCF4\uC815 \uAC80\uD1A0 \uC2E4\uD328: {ex.Message}");
                AppendLog($"SEG remediation dry run failed: {ex.Message}");
            }
            finally
            {
                isHistoricalSegmentationRemediationAuditRunning = false;
            }
        }
    }
}
