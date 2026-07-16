using MvcVisionSystem.Yolo;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        private bool isExternalEvaluationDataAuditRunning;

        // Folder selection remains a view adapter; the SHA-256 audit itself is a side-effect-free service.
        private async void ExecuteExternalEvaluationDataAuditCommand()
        {
            if (isExternalEvaluationDataAuditRunning)
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
            LearningWorkflowViewModel?.SetExternalEvaluationDataAuditResult(
                "\uC678\uBD80 \uD3C9\uAC00 \uB300\uC870: \uD655\uC778 \uC911",
                "SHA-256\uB85C \uD604\uC7AC \uD559\uC2B5/\uAC80\uC99D/\uCD5C\uC885 \uAC80\uC99D \uC774\uBBF8\uC9C0\uC640 \uBE44\uAD50\uD569\uB2C8\uB2E4.",
                selectedDirectory);

            YoloExternalEvaluationDataAuditReport report;
            try
            {
                report = await Task.Run(() =>
                    YoloExternalEvaluationDataAuditService.Build(referenceDirectories, selectedDirectory));
            }
            catch (Exception ex)
            {
                LearningWorkflowViewModel?.SetExternalEvaluationDataAuditResult(
                    "\uC678\uBD80 \uD3C9\uAC00 \uB300\uC870: \uD655\uC778 \uBD88\uAC00",
                    ex.Message,
                    selectedDirectory);
                return;
            }
            finally
            {
                isExternalEvaluationDataAuditRunning = false;
            }

            string statusText;
            string detailText;
            if (report.HasErrors)
            {
                statusText = "\uC678\uBD80 \uD3C9\uAC00 \uB300\uC870: \uD655\uC778 \uBD88\uAC00";
                detailText = string.Join(" ", report.Errors.Take(2));
            }
            else if (!report.HasExternalImages)
            {
                statusText = "\uC678\uBD80 \uD3C9\uAC00 \uB300\uC870: \uC774\uBBF8\uC9C0 \uC5C6\uC74C";
                detailText = "\uC120\uD0DD\uD55C \uD3F4\uB354\uC5D0 \uC9C0\uC6D0 \uC774\uBBF8\uC9C0\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4.";
            }
            else if (report.HasContentOverlap)
            {
                statusText = "\uC678\uBD80 \uD3C9\uAC00 \uB300\uC870: \uC911\uBCF5 \uBC1C\uACAC";
                detailText = $"\uAE30\uC900 {report.ReferenceImageCount}\uC7A5 / \uC678\uBD80 {report.ExternalImageCount}\uC7A5 / \uB3D9\uC77C \uCF58\uD150\uCE20 {report.ContentOverlapCount}\uC7A5. {report.OverlapExample}";
            }
            else
            {
                statusText = "\uC678\uBD80 \uD3C9\uAC00 \uB300\uC870: \uC911\uBCF5 \uC5C6\uC74C";
                detailText = $"\uAE30\uC900 {report.ReferenceImageCount}\uC7A5 / \uC678\uBD80 {report.ExternalImageCount}\uC7A5 / \uB3D9\uC77C \uCF58\uD150\uCE20 0\uC7A5 / \uD30C\uC77C\uBA85 \uC911\uBCF5 {report.NameOverlapCount}\uAC1C. \uB77C\uBCA8 \uD488\uC9C8\uACFC NG \uD3EC\uD568 \uC5EC\uBD80\uB97C \uB2E4\uC74C\uC73C\uB85C \uD655\uC778\uD558\uC138\uC694.";
            }

            LearningWorkflowViewModel?.SetExternalEvaluationDataAuditResult(statusText, detailText, selectedDirectory);
            AppendLog($"\uC678\uBD80 \uD3C9\uAC00 \uB300\uC870: {Path.GetFileName(selectedDirectory)} / \uAE30\uC900 {report.ReferenceImageCount} / \uC678\uBD80 {report.ExternalImageCount} / \uC911\uBCF5 {report.ContentOverlapCount}");
        }
    }
}
