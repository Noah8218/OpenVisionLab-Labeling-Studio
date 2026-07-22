using MvcVisionSystem;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;

namespace LabelingApplication.Tests;

internal static partial class Program
{
    private static void TestWpfImageQueueWorklistTenThousand()
    {
        if (System.Windows.Application.Current == null)
        {
            _ = new System.Windows.Application
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown
            };
        }

        const int itemCount = 10_000;
        var items = Enumerable.Range(0, itemCount)
            .Select(index => WpfImageQueueItem.CreateShell(Path.Combine(Path.GetTempPath(), $"worklist-{index:00000}.jpg")))
            .ToArray();

        items[1].IsSaveRequired = true;
        items[2].ReviewState = YoloImageReviewState.Candidate;
        items[3].ReviewState = YoloImageReviewState.Failed;
        items[4].IsLabeled = true;
        items[4].ReviewState = YoloImageReviewState.Confirmed;
        items[4].QualityReviewState = YoloImageQualityReviewState.NeedsFix;
        foreach (WpfImageQueueItem item in items.Skip(5))
        {
            item.IsLabeled = true;
            item.ReviewState = YoloImageReviewState.Confirmed;
        }

        Stopwatch summaryStopwatch = Stopwatch.StartNew();
        WpfImageQueueSummary summary = WpfImageQueueFilterService.Summarize(items);
        summaryStopwatch.Stop();
        AssertEqual(itemCount, summary.TotalCount);
        AssertEqual(9_995, summary.CompletedCount);
        AssertEqual(5, summary.WorklistCount);
        AssertEqual(1, summary.SaveRequiredCount);
        AssertEqual(1, summary.CandidateCount);
        AssertEqual(1, summary.FailedCount);
        AssertEqual(1, summary.NeedsFixCount);
        AssertTrue(summaryStopwatch.Elapsed.TotalMilliseconds < 250D,
            $"10K worklist summary exceeded 250ms: {summaryStopwatch.Elapsed.TotalMilliseconds:0.0}ms");

        WpfImageQueueItem anomalyOk = WpfImageQueueItem.CreateShell("anomaly-ok.jpg");
        anomalyOk.AnomalyReviewState = AnomalyImageReviewState.Normal;
        anomalyOk.IsLabeled = true;
        WpfImageQueueItem anomalyNg = WpfImageQueueItem.CreateShell("anomaly-ng.jpg");
        anomalyNg.AnomalyReviewState = AnomalyImageReviewState.Abnormal;
        anomalyNg.IsLabeled = true;
        AssertTrue(!WpfImageQueueFilterService.MatchesFilter(anomalyOk, WpfImageQueueFilter.Unlabeled),
            "completed anomaly OK must not enter the worklist");
        AssertTrue(!WpfImageQueueFilterService.MatchesFilter(anomalyNg, WpfImageQueueFilter.Unlabeled),
            "completed anomaly NG must not enter the worklist");

        var window = new WpfLabelingShellWindow();
        try
        {
            WpfBulkObservableCollection<WpfImageQueueItem> source =
                GetPrivateField<WpfBulkObservableCollection<WpfImageQueueItem>>(window, "imageQueueItems");
            source.ReplaceAll(items);
            InvokePrivateResult<object>(window, "UpdateImageQueueStatusText", -1, -1);
            window.ImageQueueViewModel.QueueFilterUnfinishedCommand.Execute(null);
            PumpWpfDispatcher(TimeSpan.FromMilliseconds(50));

            ICollectionView queueView = GetPrivateField<ICollectionView>(window, "imageQueueView");
            AssertEqual(5, queueView.Cast<object>().Count());
            AssertEqual("5장 보기", window.ImageQueueViewModel.QueueFilterUnfinishedText);
            AssertTrue(window.ImageQueueViewModel.NextUnlabeledActionText.Contains("미완료", StringComparison.Ordinal),
                "the existing next-incomplete action should remain available beside the worklist");
            AssertTrue(window.FindName("QueueFilterUnfinishedButton") is FrameworkElement worklistButton
                    && worklistButton.Visibility == Visibility.Visible,
                "the 10K worklist entry must be visible in the image queue");

            Predicate<object> originalFilter = queueView.Filter;
            int filterEvaluationCount = 0;
            queueView.Filter = value =>
            {
                filterEvaluationCount++;
                return originalFilter?.Invoke(value) ?? true;
            };
            PumpWpfDispatcher(TimeSpan.FromMilliseconds(20));
            filterEvaluationCount = 0;

            var viewActions = new List<NotifyCollectionChangedAction>();
            ((INotifyCollectionChanged)queueView).CollectionChanged += (_, args) => viewActions.Add(args.Action);
            WpfImageQueueItem[] originalItems = window.ImageQueueItems.ToArray();

            Stopwatch updateStopwatch = Stopwatch.StartNew();
            items[0].IsLabeled = true;
            items[0].ReviewState = YoloImageReviewState.Confirmed;
            InvokePrivateResult<object>(window, "UpdateImageQueueStatusText", -1, -1);
            PumpWpfDispatcher(TimeSpan.FromMilliseconds(50));
            updateStopwatch.Stop();

            AssertEqual(4, queueView.Cast<object>().Count());
            AssertEqual("4장 보기", window.ImageQueueViewModel.QueueFilterUnfinishedText);
            AssertTrue(window.FindName("DatasetStatusText") is System.Windows.Controls.TextBlock datasetStatus
                    && datasetStatus.Text.Contains("4/10000", StringComparison.Ordinal),
                "the Worklist dataset status must use the current summary instead of a stale live-filter view count");
            AssertTrue(!viewActions.Contains(NotifyCollectionChangedAction.Reset),
                "completing one worklist item must not reset the 10K queue view");
            AssertTrue(filterEvaluationCount < 100,
                $"completing one worklist item reevaluated too many rows: {filterEvaluationCount}/{itemCount}");
            AssertTrue(updateStopwatch.Elapsed.TotalMilliseconds < 500D,
                $"10K worklist single-item update exceeded 500ms: {updateStopwatch.Elapsed.TotalMilliseconds:0.0}ms");
            AssertEqual(itemCount, window.ImageQueueItems.Count);
            AssertTrue(originalItems.SequenceEqual(window.ImageQueueItems),
                "worklist filtering must preserve every queue row instance");

            Console.WriteLine(
                $"WORKLIST_10K_SUMMARY_MS={summaryStopwatch.Elapsed.TotalMilliseconds:0.0}; "
                + $"UPDATE_MS={updateStopwatch.Elapsed.TotalMilliseconds:0.0}; "
                + $"FILTER_EVALUATIONS={filterEvaluationCount}; VISIBLE=4; TOTAL={itemCount}");
        }
        finally
        {
            window.Close();
        }
    }
}
