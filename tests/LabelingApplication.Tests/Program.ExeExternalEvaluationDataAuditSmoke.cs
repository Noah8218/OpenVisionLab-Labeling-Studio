using MvcVisionSystem;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace LabelingApplication.Tests;

internal static partial class Program
{
    private static int RunExeExternalEvaluationDataAuditSmoke(string[] args)
    {
        string recipeName = "external_evaluation_audit_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        Process process = null;
        string recipeDirectory = string.Empty;
        string lastOpenedRecipePath = string.Empty;
        byte[] previousLastOpenedRecipe = null;
        bool hadLastOpenedRecipe = false;

        try
        {
            string root = FindRepositoryRoot();
            string exePath = Path.GetFullPath(GetArgumentValue(
                args,
                "--exe",
                Path.Combine(root, "artifacts", "run", "Debug", "OpenVisionLab.LabelingStudio.exe")));
            string artifactRoot = Path.GetFullPath(GetArgumentValue(
                args,
                "--artifact-root",
                Path.Combine(root, "artifacts", "exe-external-evaluation-data-audit", recipeName)));
            string screenshotDirectory = Path.Combine(artifactRoot, "screenshots");
            string outputRoot = Path.Combine(artifactRoot, "dataset");
            string externalDuplicateDirectory = Path.Combine(artifactRoot, "external-duplicate");
            string externalIndependentDirectory = Path.Combine(artifactRoot, "external-independent");

            AssertTrue(File.Exists(exePath), "external evaluation audit EXE was not found: " + exePath);

            string exeDirectory = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;
            string recipeRoot = Path.Combine(exeDirectory, "RECIPE");
            recipeDirectory = Path.Combine(recipeRoot, recipeName);
            lastOpenedRecipePath = Path.Combine(recipeRoot, ".last-opened-recipe");
            hadLastOpenedRecipe = File.Exists(lastOpenedRecipePath);
            previousLastOpenedRecipe = hadLastOpenedRecipe ? File.ReadAllBytes(lastOpenedRecipePath) : null;

            DeleteDirectoryIfExists(recipeDirectory);
            DeleteDirectoryIfExists(artifactRoot);
            Directory.CreateDirectory(screenshotDirectory);
            Directory.CreateDirectory(externalDuplicateDirectory);
            Directory.CreateDirectory(externalIndependentDirectory);

            process = StartYoloV8RuntimeSmokeExe(exePath, out IntPtr stableHandle);
            CaptureWorkflowStep(RefreshAutomationRoot(process, stableHandle), screenshotDirectory, "01_before_recipe_setup");
            CreateDatasetRecipeThroughExe(
                process,
                stableHandle,
                recipeName,
                outputRoot,
                recipeDirectory,
                screenshotDirectory,
                "\uAC1D\uCCB4 \uD0D0\uC9C0",
                LabelingDatasetPurpose.ObjectDetection,
                "OK");

            string visionPath = Path.Combine(recipeDirectory, "VISION.xml");
            AssertTrue(File.Exists(visionPath), "external evaluation audit recipe was not created");
            string referenceImageDirectory = Path.Combine(outputRoot, "data", "train", "images");
            Directory.CreateDirectory(referenceImageDirectory);
            string referenceImagePath = Path.Combine(referenceImageDirectory, "reference.jpeg");
            using (Bitmap referenceImage = CreateSolidBitmap(24, 24, Color.DarkRed))
            {
                referenceImage.Save(referenceImagePath, System.Drawing.Imaging.ImageFormat.Jpeg);
            }

            string copiedImagePath = Path.Combine(externalDuplicateDirectory, "copied-reference.jpeg");
            File.Copy(referenceImagePath, copiedImagePath, overwrite: true);
            string independentImagePath = Path.Combine(externalIndependentDirectory, "independent.jpeg");
            using (Bitmap independentImage = CreateSolidBitmap(24, 24, Color.DarkBlue))
            {
                independentImage.Save(independentImagePath, System.Drawing.Imaging.ImageFormat.Jpeg);
            }

            AssertTrue(OpenYoloModelCenterThroughExe(process, stableHandle), "model center was not selectable for external evaluation audit");
            AssertTrue(OpenExternalEvaluationAuditSectionThroughExe(process, stableHandle), "external evaluation audit section was not reachable");
            CaptureWorkflowStep(RefreshAutomationRoot(process, stableHandle), screenshotDirectory, "02_audit_ready");
            string visionHashBeforeAudit = ComputeFileSha256(visionPath);

            var rootElement = RefreshAutomationRoot(process, stableHandle);
            AssertTrue(
                TryInvokeAutomationButtonByAutomationId(rootElement, "YoloExternalEvaluationAuditButton"),
                "external evaluation audit button was not invokable");
            ChooseFolderForExternalEvaluationAudit(process, externalDuplicateDirectory);

            string statusText = string.Empty;
            string detailText = string.Empty;
            AssertTrue(
                WaitUntil(
                    () =>
                    {
                        var latestRoot = RefreshAutomationRoot(process, stableHandle, bringToFront: false);
                        statusText = GetAutomationValueByAutomationId(latestRoot, "YoloExternalEvaluationAuditStatusText");
                        detailText = GetAutomationValueByAutomationId(latestRoot, "YoloExternalEvaluationAuditDetailText");
                        return statusText.Contains("\uC911\uBCF5 \uBC1C\uACAC", StringComparison.Ordinal)
                            && detailText.Contains("\uB3D9\uC77C \uCF58\uD150\uCE20 1\uC7A5", StringComparison.Ordinal);
                    },
                    TimeSpan.FromSeconds(12)),
                "external evaluation audit did not surface duplicate content: " + statusText + " / " + detailText);
            AssertEqual(visionHashBeforeAudit, ComputeFileSha256(visionPath));
            CaptureWorkflowStep(RefreshAutomationRoot(process, stableHandle), screenshotDirectory, "03_duplicate_content_blocked");

            string duplicateStatusText = statusText;
            string duplicateDetailText = detailText;
            rootElement = RefreshAutomationRoot(process, stableHandle);
            AssertTrue(
                TryInvokeAutomationButtonByAutomationId(rootElement, "YoloExternalEvaluationAuditButton"),
                "external evaluation audit button was not invokable for independent content");
            ChooseFolderForExternalEvaluationAudit(process, externalIndependentDirectory);

            statusText = string.Empty;
            detailText = string.Empty;
            AssertTrue(
                WaitUntil(
                    () =>
                    {
                        var latestRoot = RefreshAutomationRoot(process, stableHandle, bringToFront: false);
                        statusText = GetAutomationValueByAutomationId(latestRoot, "YoloExternalEvaluationAuditStatusText");
                        detailText = GetAutomationValueByAutomationId(latestRoot, "YoloExternalEvaluationAuditDetailText");
                        return statusText.Contains("\uC911\uBCF5 \uC5C6\uC74C", StringComparison.Ordinal)
                            && detailText.Contains("\uB3D9\uC77C \uCF58\uD150\uCE20 0\uC7A5", StringComparison.Ordinal);
                    },
                    TimeSpan.FromSeconds(12)),
                "external evaluation audit did not clear independent content: " + statusText + " / " + detailText);
            AssertEqual(visionHashBeforeAudit, ComputeFileSha256(visionPath));
            CaptureWorkflowStep(RefreshAutomationRoot(process, stableHandle), screenshotDirectory, "04_independent_content_clear");

            string summaryPath = Path.Combine(artifactRoot, "summary.txt");
            File.WriteAllLines(summaryPath, new[]
            {
                "EXE external evaluation audit smoke passed.",
                "referenceImage=" + referenceImagePath,
                "externalDuplicateImage=" + copiedImagePath,
                "duplicateStatus=" + duplicateStatusText,
                "duplicateDetail=" + duplicateDetailText,
                "externalIndependentImage=" + independentImagePath,
                "independentStatus=" + statusText,
                "independentDetail=" + detailText,
                "recipeMutation=none",
                "screenshots=" + screenshotDirectory
            }, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            Console.WriteLine("EXE_EXTERNAL_EVALUATION_AUDIT duplicateStatus=" + duplicateStatusText);
            Console.WriteLine("EXE_EXTERNAL_EVALUATION_AUDIT duplicateDetail=" + duplicateDetailText);
            Console.WriteLine("EXE_EXTERNAL_EVALUATION_AUDIT independentStatus=" + statusText);
            Console.WriteLine("EXE_EXTERNAL_EVALUATION_AUDIT independentDetail=" + detailText);
            Console.WriteLine("EXE_EXTERNAL_EVALUATION_AUDIT summary=" + summaryPath);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL EXE external evaluation audit smoke: " + ex.Message);
            Console.Error.WriteLine(ex.ToString());
            return 1;
        }
        finally
        {
            CloseExeSmokeProcess(process);
            RestoreLastOpenedRecipe(lastOpenedRecipePath, hadLastOpenedRecipe, previousLastOpenedRecipe);
            DeleteDirectoryIfExists(recipeDirectory);
        }
    }

    private static bool OpenExternalEvaluationAuditSectionThroughExe(Process process, IntPtr stableHandle)
    {
        for (int attempt = 0; attempt < 10; attempt++)
        {
            var root = RefreshAutomationRoot(process, stableHandle, bringToFront: false);
            if (FindAutomationElementByAutomationId(root, "YoloExternalEvaluationAuditButton") == null)
            {
                _ = SelectAutomationTabByAutomationId(root, "YoloModelCenterDataTaskTab");
                Thread.Sleep(250);
                root = RefreshAutomationRoot(process, stableHandle, bringToFront: false);
            }

            if (FindAutomationElementByAutomationId(root, "YoloExternalEvaluationAuditButton") != null)
            {
                return true;
            }

            if (TryExpandAutomationElementByAutomationId(root, "YoloDatasetReadinessQuickPanel"))
            {
                Thread.Sleep(250);
            }
        }

        return false;
    }

    private static void ChooseFolderForExternalEvaluationAudit(Process process, string folderPath)
    {
        const string dialogTitle = "\uC678\uBD80 \uD3C9\uAC00 \uD3F4\uB354 \uB300\uC870";
        var dialog = WaitForProcessWindowByName(process, dialogTitle, TimeSpan.FromSeconds(8));
        AssertTrue(dialog != null, "external evaluation folder dialog did not appear");
        BringNativeWindowToFront(new IntPtr(dialog.Current.NativeWindowHandle));
        Thread.Sleep(200);

        Clipboard.SetText(folderPath);
        SendKeys.SendWait("%d");
        Thread.Sleep(120);
        SendKeys.SendWait("^v");
        Thread.Sleep(120);
        SendKeys.SendWait("{ENTER}");
        Thread.Sleep(800);

        dialog = FindProcessWindowByName(process, dialogTitle);
        if (dialog == null)
        {
            return;
        }

        if (!TryInvokeAutomationButton(dialog, "\uD3F4\uB354 \uC120\uD0DD")
            && !TryInvokeAutomationButton(dialog, "\uC120\uD0DD")
            && !TryInvokeAutomationButton(dialog, "Select Folder"))
        {
            SendKeys.SendWait("{ENTER}");
        }
    }
}
