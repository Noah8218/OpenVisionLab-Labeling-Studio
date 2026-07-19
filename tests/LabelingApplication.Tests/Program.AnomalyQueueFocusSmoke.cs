using MvcVisionSystem;
using MvcVisionSystem._1._Core;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using WpfButtonBase = System.Windows.Controls.Primitives.ButtonBase;

namespace LabelingApplication.Tests;

internal static partial class Program
{
    private static void TestWpfAnomalyQueueFocusFollowsActiveImage()
    {
        if (System.Windows.Application.Current == null)
        {
            _ = new System.Windows.Application
            {
                ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown
            };
        }

        CData previousData = CGlobal.Inst.Data;
        string previousRecipeName = CGlobal.Inst.Recipe.Name;
        string root = CreateTempRoot();
        try
        {
            const int decisionCount = 18;
            string requestedImageRoot = Environment.GetEnvironmentVariable("LABELING_ANOMALY_QUEUE_IMAGE_ROOT");
            string imageRoot = Directory.Exists(requestedImageRoot)
                ? Path.GetFullPath(requestedImageRoot)
                : Path.Combine(root, "images");
            string outputRoot = Path.Combine(root, "output");
            if (!Directory.Exists(requestedImageRoot))
            {
                Directory.CreateDirectory(imageRoot);
                for (int index = 0; index < 100; index++)
                {
                    CreateVisualSmokeImage(Path.Combine(imageRoot, $"focus-{index:000}.jpg"), index + 1);
                }
            }
            int imageCount = new WpfImageQueueSelectionService().EnumerateImageFiles(imageRoot).Count;
            AssertTrue(imageCount > decisionCount,
                $"Anomaly queue focus fixture requires more than {decisionCount} images: {imageCount}");

            var data = new CData();
            data.ConfigureOutputRoot(outputRoot);
            data.ProjectSettings.DatasetPurpose = LabelingDatasetPurpose.AnomalyDetection;
            data.ProjectSettings.PythonModel.ImageRootPath = imageRoot;
            data.ProjectSettings.YoloDataset.ValidationPercent = 0;
            data.ProjectSettings.YoloDataset.TestPercent = 0;
            CGlobal.Inst.Data = data;
            SetPrivateField(CGlobal.Inst.Recipe, "m_strName", string.Empty);

            WpfLabelingShellWindow window = new WpfLabelingShellWindow
            {
                Width = 1920,
                Height = 1080,
                Left = 0,
                Top = 0,
                WindowStartupLocation = System.Windows.WindowStartupLocation.Manual
            };
            try
            {
                window.Show();
                AssertEqual(imageCount, window.LoadImageQueueFromRoot(imageRoot, loadFirstImage: true, refreshDetails: false));
                PumpWpfDispatcher(TimeSpan.FromMilliseconds(200));
                window.Activate();
                window.Focus();
                PumpWpfDispatcher(TimeSpan.FromMilliseconds(100));

                string[] requiredApplicationResourceKeys =
                {
                    "GridLineBrush",
                    "PrimaryTextBrush",
                    "DisabledTextBrush",
                    "SecondaryTextBrush",
                    "ControlElevationBorderBrush",
                    "ToolbarButtonBorderBrush",
                    "PanelBrush"
                };
                foreach (string key in requiredApplicationResourceKeys)
                {
                    AssertTrue(System.Windows.Application.Current.TryFindResource(key) != null,
                        $"Application theme resources should expose '{key}' to detached WPF visual states");
                }

                DataGrid queueGrid = GetAnomalyQueueFocusGrid(window);
                WpfImageQueuePanel queuePanel = window.FindName("ImageQueuePanelControl") as WpfImageQueuePanel
                    ?? throw new InvalidOperationException("Image queue panel was not available");
                WpfButtonBase normalButton = queuePanel.FindName("MarkAnomalyNormalButton") as WpfButtonBase
                    ?? throw new InvalidOperationException("Anomaly normal button was not available");
                WpfButtonBase abnormalButton = queuePanel.FindName("MarkAnomalyAbnormalButton") as WpfButtonBase
                    ?? throw new InvalidOperationException("Anomaly abnormal button was not available");
                ICollectionView queueView = GetPrivateField<ICollectionView>(window, "imageQueueView");
                Predicate<object> originalFilter = queueView.Filter;
                int filterEvaluationCount = 0;
                int viewResetCount = 0;
                queueView.Filter = value =>
                {
                    filterEvaluationCount++;
                    return originalFilter?.Invoke(value) ?? true;
                };
                ((INotifyCollectionChanged)queueView).CollectionChanged += (_, args) =>
                {
                    if (args.Action == NotifyCollectionChangedAction.Reset)
                    {
                        viewResetCount++;
                    }
                };
                PumpWpfDispatcher(TimeSpan.FromMilliseconds(40));
                bool allGridSelectionsFollowed = true;
                bool allViewModelSelectionsFollowed = true;
                bool allCurrentRowsFollowed = true;
                bool allSelectedRowsWereRealized = true;
                bool allDecisionsAvoidedViewReset = true;
                bool allFilterUpdatesWereLocal = true;
                bool allQueuePopulateWorkWasSkipped = true;
                var decisionDurations = new List<double>();
                TraceSource resourceTrace = PresentationTraceSources.ResourceDictionarySource;
                SourceLevels previousTraceLevel = resourceTrace.Switch.Level;
                using var resourceWarningWriter = new StringWriter();
                using var resourceWarningListener = new TextWriterTraceListener(resourceWarningWriter);
                resourceTrace.Switch.Level = SourceLevels.Warning;
                resourceTrace.Listeners.Add(resourceWarningListener);
                try
                {
                    for (int decisionIndex = 0; decisionIndex < decisionCount; decisionIndex++)
                    {
                        filterEvaluationCount = 0;
                        viewResetCount = 0;
                        Stopwatch decisionStopwatch = Stopwatch.StartNew();
                        if (decisionIndex % 2 == 0)
                        {
                            InvokeAnomalyDecisionButton(normalButton);
                        }
                        else
                        {
                            InvokeAnomalyDecisionButton(abnormalButton);
                        }

                        PumpWpfDispatcher(TimeSpan.FromMilliseconds(80));
                        decisionStopwatch.Stop();
                        decisionDurations.Add(decisionStopwatch.Elapsed.TotalMilliseconds);

                        string activePath = GetPrivateField<string>(window, "activeImagePath");
                        WpfImageQueueItem activeItem = window.ImageQueueItems.Single(item =>
                            string.Equals(item.ImagePath, activePath, StringComparison.OrdinalIgnoreCase));
                        WpfImageQueueItem gridItem = queueGrid.SelectedItem as WpfImageQueueItem;
                        WpfImageQueueItem currentCellItem = queueGrid.CurrentCell.Item as WpfImageQueueItem;

                        Console.WriteLine(
                            $"ANOMALY_QUEUE_FOCUS_{decisionIndex + 1}: active={Path.GetFileName(activePath)}; "
                            + $"grid={Path.GetFileName(gridItem?.ImagePath)}; "
                            + $"viewModel={Path.GetFileName(window.ImageQueueViewModel.SelectedQueueItem?.ImagePath)}; "
                            + $"currentCell={Path.GetFileName(currentCellItem?.ImagePath)}; "
                            + $"elapsedMs={decisionStopwatch.Elapsed.TotalMilliseconds:0.0}; "
                            + $"viewResets={viewResetCount}; filterEvaluations={filterEvaluationCount}; "
                            + $"loadMs={window.LastImageLoadDiagnostics.TotalMilliseconds:0.0}; "
                            + $"queueMs={window.LastImageLoadDiagnostics.QueuePopulateMilliseconds:0.0}; "
                            + $"reviewMs={window.LastImageLoadDiagnostics.ReviewRefreshMilliseconds:0.0}");

                        allGridSelectionsFollowed &= ReferenceEquals(activeItem, gridItem);
                        allViewModelSelectionsFollowed &= ReferenceEquals(activeItem, window.ImageQueueViewModel.SelectedQueueItem);
                        allCurrentRowsFollowed &= ReferenceEquals(activeItem, currentCellItem);
                        allDecisionsAvoidedViewReset &= viewResetCount == 0;
                        allFilterUpdatesWereLocal &= filterEvaluationCount < imageCount;
                        allQueuePopulateWorkWasSkipped &= window.LastImageLoadDiagnostics.QueuePopulateMilliseconds < 50D;
                        queueGrid.UpdateLayout();
                        DataGridRow row = queueGrid.ItemContainerGenerator.ContainerFromItem(activeItem) as DataGridRow;
                        allSelectedRowsWereRealized &= row?.IsSelected == true;
                    }
                }
                finally
                {
                    resourceWarningListener.Flush();
                    resourceTrace.Listeners.Remove(resourceWarningListener);
                    resourceTrace.Switch.Level = previousTraceLevel;
                }

                string resourceWarnings = resourceWarningWriter.ToString();
                int resourceWarningCount = resourceWarnings.Split('\n')
                    .Count(line => line.Contains("Resource not found", StringComparison.Ordinal));
                double[] orderedDecisionDurations = decisionDurations.OrderBy(value => value).ToArray();
                double medianDecisionMilliseconds = orderedDecisionDurations[orderedDecisionDurations.Length / 2];
                double maximumDecisionMilliseconds = orderedDecisionDurations.Max();
                Console.WriteLine(
                    $"ANOMALY_QUEUE_DECISION_TIMING: medianMs={medianDecisionMilliseconds:0.0}; "
                    + $"maxMs={maximumDecisionMilliseconds:0.0}");
                Console.WriteLine($"ANOMALY_QUEUE_RESOURCE_WARNINGS={resourceWarningCount}");

                string capturePath = Environment.GetEnvironmentVariable("LABELING_ANOMALY_QUEUE_FOCUS_CAPTURE");
                if (!string.IsNullOrWhiteSpace(capturePath))
                {
                    CaptureWindow(window, capturePath);
                    Console.WriteLine($"ANOMALY_QUEUE_FOCUS_CAPTURE={capturePath}");
                }

                AssertTrue(allGridSelectionsFollowed,
                    "OK/NG decision should move the image queue selection to every newly active image");
                AssertTrue(allViewModelSelectionsFollowed,
                    "OK/NG decision should keep the queue ViewModel selection on every newly active image");
                AssertTrue(allCurrentRowsFollowed,
                    "OK/NG decision should move the DataGrid current row to every newly active image");
                AssertTrue(allSelectedRowsWereRealized,
                    "OK/NG decision should realize and visibly select every newly active queue row");
                AssertTrue(allDecisionsAvoidedViewReset,
                    "OK/NG decision must not reset and redraw the whole image queue view");
                AssertTrue(allFilterUpdatesWereLocal,
                    "OK/NG decision must not reevaluate the filter for every image queue row");
                AssertTrue(allQueuePopulateWorkWasSkipped,
                    "OK/NG next-image navigation must not repopulate or refresh the existing queue");
                AssertTrue(medianDecisionMilliseconds < 750D,
                    $"OK/NG decision median latency regressed: {medianDecisionMilliseconds:0.0}ms");
                AssertTrue(maximumDecisionMilliseconds < 1500D,
                    $"OK/NG decision maximum latency regressed: {maximumDecisionMilliseconds:0.0}ms");
                AssertEqual(0, resourceWarningCount);
                var persistedReviewStatus = new AnomalyImageReviewStatusService();
                persistedReviewStatus.LoadReviewStatus(data, window.ImageQueueItems.Select(item => item.ImagePath));
                AnomalyImageReviewSummary summary = persistedReviewStatus.BuildSummary();
                AssertEqual(decisionCount / 2, summary.NormalImageCount);
                AssertEqual(decisionCount / 2, summary.AbnormalImageCount);
            }
            finally
            {
                window.Close();
            }
        }
        finally
        {
            CGlobal.Inst.Data = previousData;
            SetPrivateField(CGlobal.Inst.Recipe, "m_strName", previousRecipeName);
            DeleteTempRoot(root);
        }
    }

    private static DataGrid GetAnomalyQueueFocusGrid(WpfLabelingShellWindow window)
    {
        PropertyInfo property = typeof(WpfLabelingShellWindow).GetProperty(
            "ImageQueueGrid",
            BindingFlags.Instance | BindingFlags.NonPublic);
        return property?.GetValue(window) as DataGrid
            ?? throw new InvalidOperationException("Image queue grid was not available");
    }

    private static void InvokeAnomalyDecisionButton(WpfButtonBase button)
    {
        if (button.Command == null || !button.Command.CanExecute(button.CommandParameter))
        {
            throw new InvalidOperationException("Anomaly decision button command was not executable");
        }

        button.Command.Execute(button.CommandParameter);
    }
}
