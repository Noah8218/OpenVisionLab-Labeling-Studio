using MvcVisionSystem;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace LabelingApplication.Tests;

internal static partial class Program
{
    private static void TestWpfImageQueueOperatorFolderProfile(string[] args)
    {
        if (System.Windows.Application.Current == null)
        {
            _ = new System.Windows.Application
            {
                ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
            };
        }

        string root = GetArgumentValue(args, "--root", string.Empty);
        AssertTrue(!string.IsNullOrWhiteSpace(root), "operator profile requires --root <image-folder>");
        root = Path.GetFullPath(root);
        AssertTrue(Directory.Exists(root), "operator profile root was not found: " + root);

        int timeoutSeconds = Math.Max(30, GetPositiveArgument(args, "--timeout-seconds", 900));
        int minimumImages = GetPositiveArgument(args, "--minimum-images", 10_000);
        string profileOutputPath = GetArgumentValue(args, "--profile-output", string.Empty);
        if (!string.IsNullOrWhiteSpace(profileOutputPath))
        {
            profileOutputPath = Path.GetFullPath(profileOutputPath);
        }

        CData previousData = CGlobal.Inst.Data;
        string outputRoot = CreateTempRoot();
        try
        {
            var data = new CData();
            data.ConfigureOutputRoot(outputRoot);
            data.ProjectSettings.PythonModel.ImageRootPath = root;
            CGlobal.Inst.Data = data;

            var window = new WpfLabelingShellWindow();
            try
            {
                long workingSetBeforeBytes = GetCurrentProcessWorkingSetBytes();
                Stopwatch catalogStopwatch = Stopwatch.StartNew();
                Task<int> catalogTask = window.LoadImageQueueFromRootAsync(root, loadFirstImage: false, refreshDetails: true);
                double catalogReturnMilliseconds = catalogStopwatch.Elapsed.TotalMilliseconds;
                AssertTrue(catalogReturnMilliseconds < 250D,
                    $"operator queue command did not return to the dispatcher promptly: {catalogReturnMilliseconds:0.0}ms");

                bool catalogInputProbeRan = false;
                Stopwatch catalogInputStopwatch = Stopwatch.StartNew();
                window.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Input,
                    new Action(() => catalogInputProbeRan = true));
                AssertTrue(WaitUntilWpf(() => catalogInputProbeRan, TimeSpan.FromSeconds(2)),
                    "dispatcher input did not run while the operator catalog was loading");
                catalogInputStopwatch.Stop();

                AssertTrue(WaitUntilWpf(() => catalogTask.IsCompleted, TimeSpan.FromSeconds(timeoutSeconds)),
                    $"operator image queue catalog did not complete within {timeoutSeconds} seconds");
                int itemCount = catalogTask.GetAwaiter().GetResult();
                catalogStopwatch.Stop();
                AssertTrue(itemCount >= minimumImages,
                    $"operator profile requires at least {minimumImages} supported images; found {itemCount} under {root}");
                AssertEqual(itemCount, window.ImageQueueItems.Count);
                long workingSetAfterCatalogBytes = GetCurrentProcessWorkingSetBytes();
                Stopwatch detailCompletionAfterCatalogStopwatch = Stopwatch.StartNew();

                var grid = (DataGrid)window.FindName("ImageQueueGrid");
                AssertTrue(grid != null, "operator profile could not resolve ImageQueueGrid");
                WpfImageQueueItem middleItem = window.ImageQueueItems[itemCount / 2];
                WpfImageQueueItem finalItem = window.ImageQueueItems[itemCount - 1];

                Stopwatch scrollStopwatch = Stopwatch.StartNew();
                grid.ScrollIntoView(finalItem);
                grid.UpdateLayout();
                PumpWpfDispatcher(TimeSpan.FromMilliseconds(20));
                scrollStopwatch.Stop();

                Stopwatch middleSelectionStopwatch = Stopwatch.StartNew();
                grid.SelectedItem = middleItem;
                AssertTrue(WaitUntilWpf(
                        () => string.Equals(GetPrivateField<string>(window, "activeImagePath"), middleItem.ImagePath, StringComparison.OrdinalIgnoreCase),
                        TimeSpan.FromSeconds(30)),
                    "middle operator queue selection did not load the selected image");
                middleSelectionStopwatch.Stop();

                Stopwatch finalSelectionStopwatch = Stopwatch.StartNew();
                grid.SelectedItem = finalItem;
                AssertTrue(WaitUntilWpf(
                        () => string.Equals(GetPrivateField<string>(window, "activeImagePath"), finalItem.ImagePath, StringComparison.OrdinalIgnoreCase),
                        TimeSpan.FromSeconds(30)),
                    "final operator queue selection did not load the selected image");
                finalSelectionStopwatch.Stop();

                Task detailTask = GetPrivateField<Task>(window, "imageQueueDetailLoadTask");
                AssertTrue(detailTask != null, "operator image queue detail refresh was not started");
                bool detailWasRunningAtProbe = !detailTask.IsCompleted;
                bool detailInputProbeRan = false;
                Stopwatch detailInputStopwatch = Stopwatch.StartNew();
                window.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Input,
                    new Action(() => detailInputProbeRan = true));
                AssertTrue(WaitUntilWpf(() => detailInputProbeRan, TimeSpan.FromSeconds(2)),
                    "dispatcher input did not run while the operator detail refresh was active");
                detailInputStopwatch.Stop();

                AssertTrue(WaitUntilWpf(() => detailTask.IsCompleted, TimeSpan.FromSeconds(timeoutSeconds)),
                    $"operator image queue detail refresh did not complete within {timeoutSeconds} seconds");
                detailCompletionAfterCatalogStopwatch.Stop();
                long workingSetAfterDetailBytes = GetCurrentProcessWorkingSetBytes();
                int emptyDimensionCount = window.ImageQueueItems.Count(item => string.IsNullOrWhiteSpace(item.Dimensions));

                string summary = FormattableString.Invariant(
                    $"OPERATOR_QUEUE_PROFILE_ROOT={root}; MINIMUM_IMAGES={minimumImages}; ITEMS={itemCount}; CATALOG_RETURN_MS={catalogReturnMilliseconds:0.0}; CATALOG_TOTAL_MS={catalogStopwatch.Elapsed.TotalMilliseconds:0.0}; CATALOG_INPUT_MS={catalogInputStopwatch.Elapsed.TotalMilliseconds:0.0}; SCROLL_DISPATCH_MS={scrollStopwatch.Elapsed.TotalMilliseconds:0.0}; SELECT_MIDDLE_MS={middleSelectionStopwatch.Elapsed.TotalMilliseconds:0.0}; SELECT_FINAL_MS={finalSelectionStopwatch.Elapsed.TotalMilliseconds:0.0}; DETAIL_TOTAL_AFTER_CATALOG_MS={detailCompletionAfterCatalogStopwatch.Elapsed.TotalMilliseconds:0.0}; DETAIL_INPUT_MS={detailInputStopwatch.Elapsed.TotalMilliseconds:0.0}; DETAIL_RUNNING_AT_PROBE={detailWasRunningAtProbe}; EMPTY_DIMENSIONS={emptyDimensionCount}; WORKING_SET_BEFORE_MB={ToMegabytes(workingSetBeforeBytes):0.0}; WORKING_SET_AFTER_CATALOG_MB={ToMegabytes(workingSetAfterCatalogBytes):0.0}; WORKING_SET_AFTER_DETAIL_MB={ToMegabytes(workingSetAfterDetailBytes):0.0}");
                if (!string.IsNullOrWhiteSpace(profileOutputPath))
                {
                    string profileOutputDirectory = Path.GetDirectoryName(profileOutputPath);
                    if (!string.IsNullOrWhiteSpace(profileOutputDirectory))
                    {
                        Directory.CreateDirectory(profileOutputDirectory);
                    }

                    File.WriteAllText(profileOutputPath, summary);
                }

                Console.WriteLine(summary);
            }
            finally
            {
                window.Close();
            }
        }
        finally
        {
            CGlobal.Inst.Data = previousData;
            DeleteTempRoot(outputRoot);
        }
    }

    private static long GetCurrentProcessWorkingSetBytes()
    {
        using Process process = Process.GetCurrentProcess();
        process.Refresh();
        return process.WorkingSet64;
    }

    private static double ToMegabytes(long bytes)
    {
        return Math.Max(0L, bytes) / 1024D / 1024D;
    }
}
