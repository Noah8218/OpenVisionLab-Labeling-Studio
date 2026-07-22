using MvcVisionSystem;
using MvcVisionSystem.Yolo;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Automation;

namespace LabelingApplication.Tests;

internal static partial class Program
{
    private static int RunExeImageQueueWorklistSmoke(string[] args)
    {
        const int imageCount = 125;
        const int initiallyCompletedCount = 5;
        const int initialWorklistCount = imageCount - initiallyCompletedCount;
        string recipeName = "image_queue_worklist_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        Process process = null;
        AutomationElement queueGrid = null;
        StructureChangedEventHandler structureHandler = null;
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
                Path.Combine(root, "artifacts", "image-queue-worklist-exe", "OpenVisionLab.LabelingStudio.exe")));
            string artifactRoot = Path.GetFullPath(GetArgumentValue(
                args,
                "--artifact-root",
                Path.Combine(root, "artifacts", "exe-image-queue-worklist", recipeName)));
            string screenshotDirectory = Path.Combine(artifactRoot, "screenshots");
            string outputRoot = Path.Combine(artifactRoot, "dataset");
            string imageRoot = Path.Combine(outputRoot, "data", "train", "images");
            string labelRoot = Path.Combine(outputRoot, "data", "train", "labels");
            string firstLabelPath = Path.Combine(labelRoot, "queue-local-000.txt");

            AssertTrue(File.Exists(exePath), "image-queue Worklist EXE was not found: " + exePath);

            string exeDirectory = Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;
            string recipeRoot = Path.Combine(exeDirectory, "RECIPE");
            recipeDirectory = Path.Combine(recipeRoot, recipeName);
            lastOpenedRecipePath = Path.Combine(recipeRoot, ".last-opened-recipe");
            hadLastOpenedRecipe = File.Exists(lastOpenedRecipePath);
            previousLastOpenedRecipe = hadLastOpenedRecipe ? File.ReadAllBytes(lastOpenedRecipePath) : null;

            DeleteDirectoryIfExists(recipeDirectory);
            DeleteDirectoryIfExists(artifactRoot);
            Directory.CreateDirectory(screenshotDirectory);
            Directory.CreateDirectory(imageRoot);
            for (int index = 0; index < imageCount; index++)
            {
                CreateVisualSmokeImage(Path.Combine(imageRoot, $"queue-local-{index:000}.jpg"), index + 1);
            }

            WriteExeTemplateBatchAutoLabelRecipe(recipeName, outputRoot, recipeDirectory, imageRoot);
            Directory.CreateDirectory(labelRoot);
            for (int index = imageCount - initiallyCompletedCount; index < imageCount; index++)
            {
                File.WriteAllText(
                    Path.Combine(labelRoot, $"queue-local-{index:000}.txt"),
                    "0 0.500000 0.500000 0.200000 0.200000" + Environment.NewLine,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
            File.WriteAllText(
                Path.Combine(labelRoot, "queue-local-003.txt"),
                "0 0.500000 0.500000 0.200000 0.200000" + Environment.NewLine,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            SeedExeImageQueueWorklistReviewStates(outputRoot, imageRoot);

            Directory.CreateDirectory(recipeRoot);
            File.WriteAllText(lastOpenedRecipePath, recipeName, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            process = StartYoloV8RuntimeSmokeExe(exePath, out IntPtr stableHandle);
            AssertTrue(
                WaitUntil(
                    () =>
                    {
                        string datasetStatus = GetAutomationValueByAutomationId(
                            RefreshAutomationRoot(process, stableHandle, bringToFront: false),
                            "DatasetStatusText");
                        return datasetStatus.Contains($"{imageCount}/{imageCount}", StringComparison.Ordinal)
                            && datasetStatus.Contains($"\uC644\uB8CC {initiallyCompletedCount}", StringComparison.Ordinal)
                            && datasetStatus.Contains("\uC218\uC815 \uD544\uC694 1", StringComparison.Ordinal)
                            && datasetStatus.Contains("AI \uD6C4\uBCF4 1", StringComparison.Ordinal)
                            && datasetStatus.Contains("\uC2E4\uD328 1", StringComparison.Ordinal)
                            && datasetStatus.Contains("queue-local-000", StringComparison.OrdinalIgnoreCase);
                    },
                    TimeSpan.FromSeconds(20)),
                "current EXE did not restore the mixed-state 125-image Recipe");

            AutomationElement rootElement = RefreshAutomationRoot(process, stableHandle);
            _ = TryInvokeAutomationButtonByAutomationId(rootElement, "LabelingWorkbenchStageButton");
            Thread.Sleep(350);
            rootElement = RefreshAutomationRoot(process, stableHandle, bringToFront: false);

            AutomationElement worklistButton = FindAutomationElementByAutomationId(rootElement, "QueueWorklistButton");
            AssertTrue(worklistButton != null && !worklistButton.Current.IsOffscreen,
                "current EXE did not expose the visible Worklist entry");
            AssertTrue(worklistButton.Current.Name.Contains("Worklist", StringComparison.Ordinal)
                    && worklistButton.Current.Name.Contains("99+\uC7A5 \uBCF4\uAE30", StringComparison.Ordinal),
                "current EXE Worklist did not expose the capped actionable count: " + worklistButton.Current.Name);
            CaptureWorkflowStep(rootElement, screenshotDirectory, "01_mixed_recipe_before_worklist");

            AssertTrue(TryInvokeAutomationButtonByAutomationId(rootElement, "QueueWorklistButton"),
                "current EXE Worklist entry was not invokable");
            AssertTrue(
                WaitUntil(
                    () =>
                    {
                        string datasetStatus = GetAutomationValueByAutomationId(
                            RefreshAutomationRoot(process, stableHandle, bringToFront: false),
                            "DatasetStatusText");
                        return datasetStatus.Contains($"{initialWorklistCount}/{imageCount}", StringComparison.Ordinal)
                            && datasetStatus.Contains("\uD544\uD130 \uD655\uC778 \uD544\uC694", StringComparison.Ordinal);
                    },
                    TimeSpan.FromSeconds(5)),
                "current EXE Worklist did not reduce the mixed Recipe to actionable rows");
            rootElement = RefreshAutomationRoot(process, stableHandle, bringToFront: false);
            CaptureWorkflowStep(rootElement, screenshotDirectory, "02_worklist_filtered_120_of_125");
            AssertExeWorklistSearch(process, stableHandle, imageCount, "queue-local-001", 1, "\uD6C4\uBCF4");
            AssertExeWorklistSearch(process, stableHandle, imageCount, "queue-local-002", 1, "\uC2E4\uD328");
            AssertExeWorklistSearch(process, stableHandle, imageCount, "queue-local-003", 1, "\uC218\uC815 \uD544\uC694");
            AssertExeWorklistSearch(process, stableHandle, imageCount, "queue-local-004", 1, "\uAC80\uC0AC\uC911");
            AssertExeWorklistSearch(process, stableHandle, imageCount, "queue-local-120", 0, string.Empty);
            AssertExeWorklistSearch(process, stableHandle, imageCount, "queue-local-000", 1, string.Empty);
            AssertTrue(
                WaitUntil(
                    () => GetAutomationValueByAutomationId(
                            RefreshAutomationRoot(process, stableHandle, bringToFront: false),
                            "DatasetStatusText")
                        .Contains("queue-local-000", StringComparison.OrdinalIgnoreCase),
                    TimeSpan.FromSeconds(3)),
                "Worklist search did not return focus to the first unreviewed image");
            rootElement = RefreshAutomationRoot(process, stableHandle, bringToFront: false);
            AssertTrue(TrySetAutomationValueByAutomationId(rootElement, "ImageQueueSearchBox", string.Empty),
                "current EXE Worklist search was not clearable");
            AssertTrue(
                WaitUntil(
                    () => GetAutomationValueByAutomationId(
                            RefreshAutomationRoot(process, stableHandle, bringToFront: false),
                            "DatasetStatusText")
                        .Contains($"{initialWorklistCount}/{imageCount}", StringComparison.Ordinal),
                    TimeSpan.FromSeconds(3)),
                "current EXE Worklist did not restore all actionable rows after clearing search");
            rootElement = RefreshAutomationRoot(process, stableHandle, bringToFront: false);

            queueGrid = FindAutomationElementByAutomationId(rootElement, "ImageQueueGrid");
            AssertTrue(queueGrid != null, "current EXE image queue was not available for Worklist reset measurement");
            int invalidations = 0;
            int bulkChanges = 0;
            structureHandler = (_, eventArgs) =>
            {
                if (eventArgs.StructureChangeType == StructureChangeType.ChildrenInvalidated)
                {
                    Interlocked.Increment(ref invalidations);
                }
                else if (eventArgs.StructureChangeType == StructureChangeType.ChildrenBulkAdded
                    || eventArgs.StructureChangeType == StructureChangeType.ChildrenBulkRemoved)
                {
                    Interlocked.Increment(ref bulkChanges);
                }
            };
            Automation.AddStructureChangedEventHandler(queueGrid, TreeScope.Subtree, structureHandler);

            AutomationElement boxToolItem = FindAnnotationToolItem(rootElement, "\uBC15\uC2A4")
                ?? FindVisibleAutomationElementByName(rootElement, "\uBC15\uC2A4", maximumWidth: 55D, maximumHeight: 55D)
                ?? FindCanvasAnnotationToolByIndex(rootElement, index: 1);
            AssertTrue(boxToolItem != null, "box tool was not reachable in the current EXE Worklist smoke");
            NativeClick(GetAutomationCenter(boxToolItem));
            AssertTrue(
                WaitUntil(
                    () => IsAutomationSelectionItemSelected(boxToolItem)
                        || IsExeSmokeBoxToolSelected(RefreshAutomationRoot(process, stableHandle, bringToFront: false)),
                    TimeSpan.FromSeconds(2)),
                "box tool was not selected before the Worklist label drag");

            rootElement = RefreshAutomationRoot(process, stableHandle, bringToFront: false);
            AutomationElement canvas = FindAutomationElementByClass(rootElement, "RoiImageCanvasView");
            AssertTrue(canvas != null, "current EXE Worklist canvas was not found");
            _ = TryInvokeAutomationButton(rootElement, "Fit");
            Thread.Sleep(100);
            canvas = FindAutomationElementByClass(
                RefreshAutomationRoot(process, stableHandle, bringToFront: false),
                "RoiImageCanvasView") ?? canvas;
            System.Windows.Rect fittedBounds = BuildExeSmokeFittedImageBounds(
                canvas.Current.BoundingRectangle,
                Path.Combine(imageRoot, "queue-local-000.jpg"));
            ExeSmokeDragPath drag = BuildExeSmokeImagePixelBoxDrag(
                fittedBounds,
                new Size(260, 220),
                new Rectangle(82, 66, 58, 48));
            _ = ExecuteExeSmokeDragBatch(new[] { drag }, moveDelayMilliseconds: 2, postMouseUpMilliseconds: 60);
            AssertTrue(
                WaitUntil(
                    () => GetAutomationValueByAutomationId(
                            RefreshAutomationRoot(process, stableHandle, bringToFront: false),
                            "AnnotationSaveStatusText")
                        .Contains("\uD544\uC694", StringComparison.Ordinal),
                    TimeSpan.FromSeconds(3)),
                "real canvas drag did not create a save-required Worklist label");
            CaptureWorkflowStep(
                RefreshAutomationRoot(process, stableHandle, bringToFront: false),
                screenshotDirectory,
                "03_save_required_remains_in_worklist");

            rootElement = RefreshAutomationRoot(process, stableHandle, bringToFront: false);
            AssertTrue(TryInvokeAutomationButtonByAutomationId(rootElement, "SaveAnnotationsButton"),
                "current EXE label-save button was not invokable from the Worklist");
            string savedDatasetStatus = string.Empty;
            bool savedRowLeftWorklist = WaitUntil(
                    () =>
                    {
                        AutomationElement latestRoot = RefreshAutomationRoot(process, stableHandle, bringToFront: false);
                        savedDatasetStatus = GetAutomationValueByAutomationId(latestRoot, "DatasetStatusText");
                        AutomationElement latestGrid = FindAutomationElementByAutomationId(latestRoot, "ImageQueueGrid");
                        string selectedRow = GetSelectedQueueRowName(latestGrid);
                        return File.Exists(firstLabelPath)
                            && new FileInfo(firstLabelPath).Length > 0
                            && savedDatasetStatus.Contains($"{initialWorklistCount - 1}/{imageCount}", StringComparison.Ordinal)
                            && savedDatasetStatus.Contains($"\uC644\uB8CC {initiallyCompletedCount + 1}", StringComparison.Ordinal)
                            && savedDatasetStatus.Contains("queue-local-001", StringComparison.OrdinalIgnoreCase)
                            && selectedRow.Contains("queue-local-001", StringComparison.OrdinalIgnoreCase);
                    },
                    TimeSpan.FromSeconds(5));
            if (!savedRowLeftWorklist)
            {
                CaptureWorkflowStep(
                    RefreshAutomationRoot(process, stableHandle, bringToFront: false),
                    screenshotDirectory,
                    "failure_saved_worklist_status");
            }

            AssertTrue(savedRowLeftWorklist,
                "saved current image did not leave the Worklist and focus the next actionable row; status="
                + savedDatasetStatus);
            Thread.Sleep(350);
            AssertEqual(0, invalidations);
            AssertEqual(0, bulkChanges);
            CaptureWorkflowStep(
                RefreshAutomationRoot(process, stableHandle, bringToFront: false),
                screenshotDirectory,
                "04_saved_row_removed_and_next_focused");

            string exeHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(exePath)));
            string summaryPath = Path.Combine(artifactRoot, "summary.txt");
            File.WriteAllLines(summaryPath, new[]
            {
                "EXE image-queue Worklist smoke passed.",
                "exePath=" + exePath,
                "exeSha256=" + exeHash,
                $"imageCount={imageCount}",
                $"initiallyCompleted={initiallyCompletedCount}",
                $"initialWorklist={initialWorklistCount}",
                $"worklistAfterSave={initialWorklistCount - 1}",
                $"completedAfterSave={initiallyCompletedCount + 1}",
                "verifiedActionableStates=unreviewed,save-required,candidate,failed,needs-fix,requested",
                "verifiedExcludedState=completed-label",
                $"queueInvalidations={invalidations}",
                $"queueBulkChanges={bulkChanges}",
                "nextActiveImage=queue-local-001.jpg",
                "labelPath=" + firstLabelPath,
                "screenshots=" + screenshotDirectory
            }, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            Console.WriteLine(
                $"EXE_IMAGE_QUEUE_WORKLIST images={imageCount} completed={initiallyCompletedCount}->{initiallyCompletedCount + 1} worklist={initialWorklistCount}->{initialWorklistCount - 1} invalidations={invalidations} bulkChanges={bulkChanges} next=queue-local-001.jpg");
            Console.WriteLine("EXE_IMAGE_QUEUE_WORKLIST exeSha256=" + exeHash);
            Console.WriteLine("EXE_IMAGE_QUEUE_WORKLIST summary=" + summaryPath);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL EXE image-queue Worklist smoke: " + ex.Message);
            Console.Error.WriteLine(ex.ToString());
            return 1;
        }
        finally
        {
            if (queueGrid != null && structureHandler != null)
            {
                try
                {
                    Automation.RemoveStructureChangedEventHandler(queueGrid, structureHandler);
                }
                catch (ElementNotAvailableException)
                {
                }
            }

            CloseExeSmokeProcess(process);
            RestoreLastOpenedRecipe(lastOpenedRecipePath, hadLastOpenedRecipe, previousLastOpenedRecipe);
            DeleteDirectoryIfExists(recipeDirectory);
        }
    }

    private static void SeedExeImageQueueWorklistReviewStates(string outputRoot, string imageRoot)
    {
        string ImagePath(int index) => Path.Combine(imageRoot, $"queue-local-{index:000}.jpg");

        var data = new CData();
        data.ConfigureOutputRoot(outputRoot);
        var service = new YoloImageReviewStatusService();
        service.SetImages(Directory.EnumerateFiles(imageRoot, "*.jpg", SearchOption.TopDirectoryOnly));
        service.SetDetectionCandidates(ImagePath(1), "queue-local-001", 2);
        service.SetDetectionRequested(ImagePath(2), "queue-local-002");
        service.SetDetectionFailed(ImagePath(2), "queue-local-002", "Detection failed.");
        service.MarkQualityNeedsFix(ImagePath(3), "queue-local-003", "Bounding box needs correction.");
        service.SetDetectionRequested(ImagePath(4), "queue-local-004");
        service.SaveReviewStatus(data);
    }

    private static void AssertExeWorklistSearch(
        Process process,
        IntPtr stableHandle,
        int totalCount,
        string searchText,
        int expectedVisibleCount,
        string expectedRowText)
    {
        AutomationElement root = RefreshAutomationRoot(process, stableHandle, bringToFront: false);
        AssertTrue(TrySetAutomationValueByAutomationId(root, "ImageQueueSearchBox", searchText),
            "current EXE Worklist search was not editable: " + searchText);

        string statusText = string.Empty;
        AssertTrue(
            WaitUntil(
                () =>
                {
                    AutomationElement latestRoot = RefreshAutomationRoot(process, stableHandle, bringToFront: false);
                    statusText = GetAutomationValueByAutomationId(latestRoot, "DatasetStatusText");
                    return statusText.Contains($"{expectedVisibleCount}/{totalCount}", StringComparison.Ordinal)
                        && statusText.Contains("\uD544\uD130 \uD655\uC778 \uD544\uC694", StringComparison.Ordinal);
                },
                TimeSpan.FromSeconds(3)),
            $"Worklist search visibility mismatch for {searchText}: expected {expectedVisibleCount}/{totalCount}; status={statusText}");

        if (expectedVisibleCount <= 0)
        {
            return;
        }

        AutomationElement grid = FindAutomationElementByAutomationId(
            RefreshAutomationRoot(process, stableHandle, bringToFront: false),
            "ImageQueueGrid");
        string rowText = BuildAutomationTextSample(grid, 80);
        AssertTrue(rowText.Contains(searchText, StringComparison.OrdinalIgnoreCase),
            "Worklist search did not expose the expected row: " + searchText + " / " + rowText);
        if (!string.IsNullOrWhiteSpace(expectedRowText))
        {
            AssertTrue(rowText.Contains(expectedRowText, StringComparison.Ordinal),
                "Worklist row did not expose its expected state: " + searchText + " / " + rowText);
        }
    }

    private static string GetSelectedQueueRowName(AutomationElement queueGrid)
    {
        if (queueGrid == null)
        {
            return string.Empty;
        }

        try
        {
            if (queueGrid.TryGetCurrentPattern(SelectionPattern.Pattern, out object gridPattern)
                && gridPattern is SelectionPattern selectionPattern)
            {
                AutomationElement[] selectedItems = selectionPattern.Current.GetSelection();
                if (selectedItems.Length > 0)
                {
                    return BuildAutomationTextSample(selectedItems[0], 40);
                }
            }
        }
        catch (ElementNotAvailableException)
        {
        }

        foreach (AutomationElement element in EnumerateAutomationDescendants(queueGrid))
        {
            try
            {
                if (element.Current.ControlType == ControlType.DataItem
                    && element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out object pattern)
                    && pattern is SelectionItemPattern selection
                    && selection.Current.IsSelected)
                {
                    return BuildAutomationTextSample(element, 40);
                }
            }
            catch (ElementNotAvailableException)
            {
            }
        }

        return string.Empty;
    }
}
