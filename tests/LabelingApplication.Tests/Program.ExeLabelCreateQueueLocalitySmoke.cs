using MvcVisionSystem;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Automation;

namespace LabelingApplication.Tests;

internal static partial class Program
{
    private static int RunExeLabelCreateQueueLocalitySmoke(string[] args)
    {
        const int imageCount = 125;
        string recipeName = "label_create_queue_locality_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
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
                Path.Combine(root, "artifacts", "run", "Debug", "OpenVisionLab.LabelingStudio.exe")));
            string artifactRoot = Path.GetFullPath(GetArgumentValue(
                args,
                "--artifact-root",
                Path.Combine(root, "artifacts", "exe-label-create-queue-locality", recipeName)));
            string screenshotDirectory = Path.Combine(artifactRoot, "screenshots");
            string outputRoot = Path.Combine(artifactRoot, "dataset");
            string imageRoot = Path.Combine(outputRoot, "data", "train", "images");
            string labelPath = Path.Combine(outputRoot, "data", "train", "labels", "queue-local-000.txt");

            AssertTrue(File.Exists(exePath), "label-create queue-locality EXE was not found: " + exePath);

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
            Directory.CreateDirectory(recipeRoot);
            File.WriteAllText(lastOpenedRecipePath, recipeName, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            process = StartYoloV8RuntimeSmokeExe(exePath, out IntPtr stableHandle);
            AssertTrue(
                WaitUntil(
                    () =>
                    {
                        AutomationElement latestRoot = RefreshAutomationRoot(process, stableHandle, bringToFront: false);
                        string datasetStatus = GetAutomationValueByAutomationId(latestRoot, "DatasetStatusText");
                        return datasetStatus.Contains("125/125", StringComparison.Ordinal)
                            && datasetStatus.Contains("queue-local-000", StringComparison.OrdinalIgnoreCase);
                    },
                    TimeSpan.FromSeconds(15)),
                "latest EXE did not restore the isolated 125-image queue");

            AutomationElement rootElement = RefreshAutomationRoot(process, stableHandle);
            _ = TryInvokeAutomationButtonByAutomationId(rootElement, "LabelingWorkbenchStageButton");
            Thread.Sleep(350);
            rootElement = RefreshAutomationRoot(process, stableHandle, bringToFront: false);

            AutomationElement boxToolItem = null;
            bool boxToolFound = WaitUntil(
                    () =>
                    {
                        AutomationElement latestRoot = RefreshAutomationRoot(process, stableHandle, bringToFront: false);
                        boxToolItem = FindAnnotationToolItem(latestRoot, "박스")
                            ?? FindVisibleAutomationElementByName(latestRoot, "박스", maximumWidth: 55D, maximumHeight: 55D)
                            ?? FindCanvasAnnotationToolByIndex(latestRoot, index: 1);
                        return boxToolItem != null;
                    },
                    TimeSpan.FromSeconds(5));
            if (!boxToolFound)
            {
                AutomationElement failureRoot = RefreshAutomationRoot(process, stableHandle, bringToFront: false);
                CaptureWorkflowStep(failureRoot, screenshotDirectory, "failure_box_tool_not_found");
                throw new InvalidOperationException(
                    "box tool was not reachable in the restored labeling workspace; automation="
                    + BuildAutomationTextSample(failureRoot, 160));
            }
            NativeClick(GetAutomationCenter(boxToolItem));
            AssertTrue(
                WaitUntil(
                    () => IsAutomationSelectionItemSelected(boxToolItem)
                        || IsExeSmokeBoxToolSelected(RefreshAutomationRoot(process, stableHandle, bringToFront: false)),
                    TimeSpan.FromSeconds(2)),
                "box tool was not selected before the real canvas drag");

            rootElement = RefreshAutomationRoot(process, stableHandle, bringToFront: false);
            AutomationElement canvas = FindAutomationElementByClass(rootElement, "RoiImageCanvasView");
            AssertTrue(canvas != null, "label-create queue-locality canvas was not found");
            _ = TryInvokeAutomationButton(rootElement, "Fit");
            Thread.Sleep(100);

            rootElement = RefreshAutomationRoot(process, stableHandle, bringToFront: false);
            canvas = FindAutomationElementByClass(rootElement, "RoiImageCanvasView") ?? canvas;
            queueGrid = FindAutomationElementByAutomationId(rootElement, "ImageQueueGrid");
            AssertTrue(queueGrid != null, "image queue grid was not available for reset measurement");

            ScrollPattern queueScroll = GetAutomationScrollPattern(queueGrid);
            AssertTrue(queueScroll != null && queueScroll.Current.VerticallyScrollable,
                "image queue did not expose a vertical scroll pattern for locality verification");
            queueScroll.SetScrollPercent(ScrollPattern.NoScroll, 55D);
            Thread.Sleep(350);
            double scrollBeforeCreate = GetVerticalScrollPercent(queueGrid);
            AssertTrue(scrollBeforeCreate > 10D, "image queue did not move away from the first rows before label creation");
            string[] visibleRowsBeforeCreate = GetVisibleQueueRowNames(queueGrid);
            AssertTrue(visibleRowsBeforeCreate.Length > 0, "no visible image queue rows were available before label creation");
            CaptureWorkflowStep(rootElement, screenshotDirectory, "01_before_label_create_scrolled_queue");

            int createInvalidations = 0;
            int createBulkChanges = 0;
            int saveInvalidations = 0;
            int saveBulkChanges = 0;
            bool measuringSave = false;
            structureHandler = (_, eventArgs) =>
            {
                bool invalidated = eventArgs.StructureChangeType == StructureChangeType.ChildrenInvalidated;
                bool bulkChanged = eventArgs.StructureChangeType == StructureChangeType.ChildrenBulkAdded
                    || eventArgs.StructureChangeType == StructureChangeType.ChildrenBulkRemoved;
                if (!invalidated && !bulkChanged)
                {
                    return;
                }

                if (Volatile.Read(ref measuringSave))
                {
                    if (invalidated)
                    {
                        Interlocked.Increment(ref saveInvalidations);
                    }
                    else
                    {
                        Interlocked.Increment(ref saveBulkChanges);
                    }
                }
                else if (invalidated)
                {
                    Interlocked.Increment(ref createInvalidations);
                }
                else
                {
                    Interlocked.Increment(ref createBulkChanges);
                }
            };
            Automation.AddStructureChangedEventHandler(queueGrid, TreeScope.Subtree, structureHandler);

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
                        .Contains("필요", StringComparison.Ordinal),
                    TimeSpan.FromSeconds(3)),
                "real canvas drag did not create a dirty label");
            Thread.Sleep(350);

            double scrollAfterCreate = GetVerticalScrollPercent(queueGrid);
            string[] visibleRowsAfterCreate = GetVisibleQueueRowNames(queueGrid);
            AssertQueueLocality(
                "create",
                createInvalidations,
                createBulkChanges,
                scrollBeforeCreate,
                scrollAfterCreate,
                visibleRowsBeforeCreate,
                visibleRowsAfterCreate);
            AssertQueueDatasetStillLoaded(process, stableHandle, imageCount);
            CaptureWorkflowStep(
                RefreshAutomationRoot(process, stableHandle, bringToFront: false),
                screenshotDirectory,
                "02_after_label_create_queue_preserved");

            Volatile.Write(ref measuringSave, true);
            rootElement = RefreshAutomationRoot(process, stableHandle, bringToFront: false);
            AssertTrue(
                TryInvokeAutomationButtonByAutomationId(rootElement, "SaveAnnotationsButton")
                    || TryInvokeAutomationButton(rootElement, "라벨 저장"),
                "label save button was not invokable in the real EXE");
            AssertTrue(
                WaitUntil(
                    () => GetAutomationValueByAutomationId(
                            RefreshAutomationRoot(process, stableHandle, bringToFront: false),
                            "AnnotationSaveStatusText")
                        .Contains("저장됨", StringComparison.Ordinal),
                    TimeSpan.FromSeconds(4)),
                "real EXE did not report the label as saved");
            AssertTrue(
                WaitUntil(() => File.Exists(labelPath) && new FileInfo(labelPath).Length > 0, TimeSpan.FromSeconds(3)),
                "real EXE did not write the expected YOLO label file: " + labelPath);
            Thread.Sleep(350);

            double scrollAfterSave = GetVerticalScrollPercent(queueGrid);
            string[] visibleRowsAfterSave = GetVisibleQueueRowNames(queueGrid);
            AssertQueueLocality(
                "save",
                saveInvalidations,
                saveBulkChanges,
                scrollAfterCreate,
                scrollAfterSave,
                visibleRowsAfterCreate,
                visibleRowsAfterSave);
            AssertQueueDatasetStillLoaded(process, stableHandle, imageCount);
            CaptureWorkflowStep(
                RefreshAutomationRoot(process, stableHandle, bringToFront: false),
                screenshotDirectory,
                "03_after_label_save_queue_preserved");

            string exeHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(exePath)));
            string summaryPath = Path.Combine(artifactRoot, "summary.txt");
            File.WriteAllLines(summaryPath, new[]
            {
                "EXE label-create queue-locality smoke passed.",
                "exePath=" + exePath,
                "exeSha256=" + exeHash,
                "imageCount=" + imageCount.ToString(CultureInfo.InvariantCulture),
                $"createInvalidations={createInvalidations}",
                $"createBulkChanges={createBulkChanges}",
                FormattableString.Invariant($"scrollBeforeCreate={scrollBeforeCreate:F2}"),
                FormattableString.Invariant($"scrollAfterCreate={scrollAfterCreate:F2}"),
                $"saveInvalidations={saveInvalidations}",
                $"saveBulkChanges={saveBulkChanges}",
                FormattableString.Invariant($"scrollAfterSave={scrollAfterSave:F2}"),
                "labelPath=" + labelPath,
                "screenshots=" + screenshotDirectory
            }, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            Console.WriteLine(
                FormattableString.Invariant(
                    $"EXE_LABEL_CREATE_QUEUE_LOCALITY images={imageCount} createInvalidations={createInvalidations} createBulkChanges={createBulkChanges} saveInvalidations={saveInvalidations} saveBulkChanges={saveBulkChanges} scroll={scrollBeforeCreate:F2}->{scrollAfterCreate:F2}->{scrollAfterSave:F2}"));
            Console.WriteLine("EXE_LABEL_CREATE_QUEUE_LOCALITY exeSha256=" + exeHash);
            Console.WriteLine("EXE_LABEL_CREATE_QUEUE_LOCALITY summary=" + summaryPath);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL EXE label-create queue-locality smoke: " + ex.Message);
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

    private static ScrollPattern GetAutomationScrollPattern(AutomationElement element)
    {
        if (element != null
            && element.TryGetCurrentPattern(ScrollPattern.Pattern, out object pattern)
            && pattern is ScrollPattern scrollPattern)
        {
            return scrollPattern;
        }

        return null;
    }

    private static AutomationElement FindVisibleAutomationElementByName(
        AutomationElement root,
        string name,
        double maximumWidth,
        double maximumHeight)
    {
        foreach (AutomationElement element in EnumerateAutomationDescendants(root))
        {
            try
            {
                System.Windows.Rect bounds = element.Current.BoundingRectangle;
                if (!element.Current.IsOffscreen
                    && string.Equals(element.Current.Name, name, StringComparison.Ordinal)
                    && bounds.Width > 0D
                    && bounds.Height > 0D
                    && bounds.Width <= maximumWidth
                    && bounds.Height <= maximumHeight)
                {
                    return element;
                }
            }
            catch (ElementNotAvailableException)
            {
            }
        }

        return null;
    }

    private static AutomationElement FindCanvasAnnotationToolByIndex(AutomationElement root, int index)
    {
        AutomationElement toolList = FindAutomationElementByAutomationId(root, "CanvasAnnotationToolListBox");
        if (toolList == null)
        {
            return null;
        }

        AutomationElement[] items = EnumerateAutomationDescendants(toolList)
            .Where(element =>
            {
                try
                {
                    return element.Current.ControlType == ControlType.ListItem
                        && !element.Current.IsOffscreen
                        && element.Current.IsEnabled;
                }
                catch (ElementNotAvailableException)
                {
                    return false;
                }
            })
            .OrderBy(element => element.Current.BoundingRectangle.Top)
            .ToArray();
        return index >= 0 && index < items.Length ? items[index] : null;
    }

    private static double GetVerticalScrollPercent(AutomationElement queueGrid)
    {
        try
        {
            ScrollPattern pattern = GetAutomationScrollPattern(queueGrid);
            return pattern?.Current.VerticalScrollPercent ?? ScrollPattern.NoScroll;
        }
        catch (ElementNotAvailableException)
        {
            return double.NaN;
        }
    }

    private static string[] GetVisibleQueueRowNames(AutomationElement queueGrid)
    {
        var names = new List<string>();
        foreach (AutomationElement element in EnumerateAutomationDescendants(queueGrid))
        {
            try
            {
                if (element.Current.ControlType != ControlType.DataItem
                    || element.Current.IsOffscreen
                    || string.IsNullOrWhiteSpace(element.Current.Name))
                {
                    continue;
                }

                names.Add(element.Current.Name);
            }
            catch (ElementNotAvailableException)
            {
            }
        }

        return names.Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToArray();
    }

    private static void AssertQueueLocality(
        string phase,
        int invalidations,
        int bulkChanges,
        double scrollBefore,
        double scrollAfter,
        IReadOnlyCollection<string> visibleRowsBefore,
        IReadOnlyCollection<string> visibleRowsAfter)
    {
        AssertEqual(0, invalidations);
        AssertEqual(0, bulkChanges);
        AssertTrue(!double.IsNaN(scrollAfter), $"queue element became stale during label {phase}");
        AssertTrue(Math.Abs(scrollAfter - scrollBefore) < 0.5D,
            FormattableString.Invariant($"queue scroll moved during label {phase}: {scrollBefore:F2} -> {scrollAfter:F2}"));
        AssertTrue(visibleRowsBefore.SequenceEqual(visibleRowsAfter, StringComparer.Ordinal),
            $"visible queue rows changed during label {phase}");
    }

    private static void AssertQueueDatasetStillLoaded(Process process, IntPtr stableHandle, int imageCount)
    {
        string datasetStatus = GetAutomationValueByAutomationId(
            RefreshAutomationRoot(process, stableHandle, bringToFront: false),
            "DatasetStatusText");
        AssertTrue(datasetStatus.Contains($"{imageCount}/{imageCount}", StringComparison.Ordinal),
            "image queue count changed after local row update: " + datasetStatus);
        AssertTrue(datasetStatus.Contains("queue-local-000", StringComparison.OrdinalIgnoreCase),
            "active image changed after local row update: " + datasetStatus);
    }
}
