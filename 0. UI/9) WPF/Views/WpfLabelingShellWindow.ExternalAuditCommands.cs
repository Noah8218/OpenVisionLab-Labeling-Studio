using MvcVisionSystem.Yolo;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    // Responsibility group: external dataset and historical audit commands.
    // These members remain WPF Window adapters; independent policy belongs in services.
    public partial class WpfLabelingShellWindow
    {
        #region ExternalEvaluationAudit
        private bool isExternalEvaluationDataAuditRunning;

        // Folder selection remains a view adapter; the SHA-256 audit itself is a side-effect-free service.
        private void ExecuteExternalEvaluationDataAuditCommand()
        {
            _ = ExecuteExternalEvaluationDataAuditCommandAsync();
        }

        private async Task ExecuteExternalEvaluationDataAuditCommandAsync()
        {
            if (isApplicationCloseApproved || isExternalEvaluationDataAuditRunning)
            {
                return;
            }

            string initialDirectory = LearningWorkflowViewModel?.ExternalEvaluationDataAuditPathText;
            if (!Directory.Exists(initialDirectory))
            {
                initialDirectory = Directory.Exists(currentImageRoot) ? currentImageRoot : string.Empty;
            }

            if (!TryPickFolder("\uC678\uBD80 \uD3C9\uAC00 \uD3F4\uB354 \uB300\uC870", initialDirectory, out string selectedDirectory))
            {
                LearningWorkflowViewModel?.SetExternalEvaluationDataAuditResult(
                    "\uC678\uBD80 \uD3C9\uAC00 \uB300\uC870: \uCDE8\uC18C",
                    "\uD3F4\uB354\uB97C \uC120\uD0DD\uD558\uBA74 \uD604\uC7AC \uD559\uC2B5/\uAC80\uC99D/\uCD5C\uC885 \uAC80\uC99D \uC774\uBBF8\uC9C0\uC640 \uB3D9\uC77C \uCF58\uD150\uCE20\uC778\uC9C0 \uD655\uC778\uD569\uB2C8\uB2E4.",
                    string.Empty);
                return;
            }

            string[] referenceDirectories = global.Data == null
                ? Array.Empty<string>()
                : new[]
                {
                    global.Data.TrainImagesPath,
                    global.Data.ValidImagesPath,
                    global.Data.TestImagesPath
                };

            isExternalEvaluationDataAuditRunning = true;
            CancellationTokenSource cancellation = new CancellationTokenSource();
            externalEvaluationDataAuditCts = cancellation;
            CancellationToken cancellationToken = cancellation.Token;
            LearningWorkflowViewModel?.SetExternalEvaluationDataAuditResult(
                "\uC678\uBD80 \uD3C9\uAC00 \uB300\uC870: \uD655\uC778 \uC911",
                "SHA-256\uB85C \uD604\uC7AC \uD559\uC2B5/\uAC80\uC99D/\uCD5C\uC885 \uAC80\uC99D \uC774\uBBF8\uC9C0\uC640 \uBE44\uAD50\uD569\uB2C8\uB2E4.",
                selectedDirectory);

            YoloExternalEvaluationDataAuditReport report;
            try
            {
                report = await Task.Run(
                    () => YoloExternalEvaluationDataAuditService.Build(referenceDirectories, selectedDirectory, cancellationToken),
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                if (!isApplicationCloseApproved)
                {
                    LearningWorkflowViewModel?.SetExternalEvaluationDataAuditResult(
                        "\uC678\uBD80 \uD3C9\uAC00 \uB300\uC870: \uD655\uC778 \uBD88\uAC00",
                        ex.Message,
                        selectedDirectory);
                }
                return;
            }
            finally
            {
                if (ReferenceEquals(externalEvaluationDataAuditCts, cancellation))
                {
                    externalEvaluationDataAuditCts = null;
                }

                cancellation.Dispose();
                isExternalEvaluationDataAuditRunning = false;
            }

            if (isApplicationCloseApproved || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            ExternalEvaluationDataAuditPresentation presentation =
                ExternalEvaluationDataAuditPresentationService.Build(report);

            LearningWorkflowViewModel?.SetExternalEvaluationDataAuditResult(
                presentation.StatusText,
                presentation.DetailText,
                selectedDirectory);
            AppendLog($"\uC678\uBD80 \uD3C9\uAC00 \uB300\uC870: {Path.GetFileName(selectedDirectory)} / \uAE30\uC900 {report.ReferenceImageCount} / \uC678\uBD80 {report.ExternalImageCount} / \uC911\uBCF5 {report.ContentOverlapCount}");
        }
        #endregion

        #region HistoricalSegmentationRemediationAudit
        private bool isHistoricalSegmentationRemediationAuditRunning;

        // The report is deliberately separate from the later, user-approved migration path.
        private void ExecuteHistoricalSegmentationRemediationAuditCommand()
        {
            _ = ExecuteHistoricalSegmentationRemediationAuditCommandAsync();
        }

        private async Task ExecuteHistoricalSegmentationRemediationAuditCommandAsync()
        {
            if (isApplicationCloseApproved || isHistoricalSegmentationRemediationAuditRunning)
            {
                return;
            }

            LabelingProjectData data = global.Data;
            string outputPath = YoloSegmentationHistoricalRemediationAuditService.ResolveDefaultOutputPath(data);
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                SetModelStatus("SEG \uBCF4\uC815 \uAC80\uD1A0 \uC2E4\uD328: \uB370\uC774\uD130\uC14B \uC800\uC7A5 \uD3F4\uB354\uB97C \uBA3C\uC800 \uC9C0\uC815\uD558\uC138\uC694.");
                return;
            }

            string sourceImagePath = TemplateMatchingAutoLabelViewModel?.RegisteredTemplateSourceImagePath ?? string.Empty;
            isHistoricalSegmentationRemediationAuditRunning = true;
            CancellationTokenSource cancellation = new CancellationTokenSource();
            historicalSegmentationRemediationAuditCts = cancellation;
            CancellationToken cancellationToken = cancellation.Token;
            SetModelStatus("SEG \uBCF4\uC815 \uAC80\uD1A0: \uAE30\uC874 \uB9C8\uC2A4\uD06C\uC640 YOLO \uB77C\uBCA8\uC744 \uC77D\uAE30 \uC804\uC6A9\uC73C\uB85C \uBE44\uAD50 \uC911");
            try
            {
                (YoloSegmentationHistoricalRemediationAuditReport Report, YoloSegmentationHistoricalRemediationAuditExportResult Export) result =
                    await Task.Run(
                        () =>
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            YoloSegmentationHistoricalRemediationAuditReport report =
                                YoloSegmentationHistoricalRemediationAuditService.Build(data, sourceImagePath, cancellationToken);
                            cancellationToken.ThrowIfCancellationRequested();
                            return (report, YoloSegmentationHistoricalRemediationAuditService.ExportMarkdown(report, outputPath, cancellationToken));
                        },
                        cancellationToken);
                if (isApplicationCloseApproved || cancellationToken.IsCancellationRequested)
                {
                    return;
                }

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
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                if (!isApplicationCloseApproved)
                {
                    SetModelStatus($"SEG \uBCF4\uC815 \uAC80\uD1A0 \uC2E4\uD328: {ex.Message}");
                    AppendLog($"SEG remediation dry run failed: {ex.Message}");
                }
            }
            finally
            {
                if (ReferenceEquals(historicalSegmentationRemediationAuditCts, cancellation))
                {
                    historicalSegmentationRemediationAuditCts = null;
                }

                cancellation.Dispose();
                isHistoricalSegmentationRemediationAuditRunning = false;
            }
        }
        #endregion

    }
}
