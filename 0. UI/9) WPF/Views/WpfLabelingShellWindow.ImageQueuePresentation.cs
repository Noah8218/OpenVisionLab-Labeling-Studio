using Lib.Common;
using MahApps.Metro.IconPacks;
using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;
using DrawingSize = System.Drawing.Size;
using MediaBrush = System.Windows.Media.Brush;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Queue presentation helpers stay isolated from queue loading so status-text changes do not hide data-flow changes.
        private void SelectImageQueueItem(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return;
            }

            WpfImageQueueItem item = FindImageQueueItem(imagePath);
            if (item == null)
            {
                return;
            }

            suppressImageQueueSelection = true;
            try
            {
                ImageQueueGrid.SelectedItem = item;
                if (ImageQueueGrid.Columns.Count > 0)
                {
                    ImageQueueGrid.CurrentCell = new DataGridCellInfo(item, ImageQueueGrid.Columns[0]);
                }

                ImageQueueGrid.ScrollIntoView(item);
            }
            finally
            {
                suppressImageQueueSelection = false;
            }

            UpdateSelectedQueueImageButton(item);
        }

        private WpfImageQueueItem FindImageQueueItem(string imagePath)
        {
            if (!string.IsNullOrWhiteSpace(imagePath)
                && imageQueueItemsByPath.TryGetValue(imagePath, out WpfImageQueueItem indexedItem))
            {
                return indexedItem;
            }

            return imageQueueSelectionService.FindItem(imageQueueItems, imagePath);
        }

        private bool ShouldShowImageQueueItem(WpfImageQueueItem item)
        {
            return WpfImageQueueFilterService.ShouldShow(item, ImageQueueSearchBox?.Text, GetSelectedImageQueueFilter());
        }

        private void UpdateImageQueueStatusText(int loadedCount = -1, int totalToLoad = -1)
        {
            WpfImageQueueFilter selectedFilter = GetSelectedImageQueueFilter();
            WpfImageQueueSummary summary = WpfImageQueueFilterService.Summarize(imageQueueItems);
            bool hasSearch = !string.IsNullOrWhiteSpace(ImageQueueSearchBox?.Text);
            int visibleCount = !hasSearch
                ? WpfImageQueueFilterService.CountByFilter(summary, selectedFilter)
                : imageQueueView?.Cast<object>().Count() ?? summary.TotalCount;
            UpdateQueueQuickFilterButtons(summary, selectedFilter);
            SetDatasetStatus(WpfImageQueueFilterService.BuildDatasetStatusTextWithActiveImage(
                summary,
                visibleCount,
                selectedFilter,
                loadedCount,
                totalToLoad,
                activeImagePath));
        }

        private void UpdateQueueQuickFilterButtons()
        {
            UpdateQueueQuickFilterButtons(
                WpfImageQueueFilterService.Summarize(imageQueueItems),
                GetSelectedImageQueueFilter());
        }

        private void UpdateQueueQuickFilterButtons(WpfImageQueueSummary summary, WpfImageQueueFilter filter)
        {
            summary ??= new WpfImageQueueSummary();
            ImageQueueViewModel.SetQuickFilterState(
                filter,
                summary.CandidateCount,
                summary.FailedCount,
                summary.ConfirmedCount,
                summary.SkippedCount,
                summary.NoCandidateCount,
                summary.WorklistCount);
        }

        private WpfImageQueueFilter GetSelectedImageQueueFilter()
        {
            return (ImageQueueFilterBox?.SelectedItem as WpfImageQueueFilterOption)?.Filter
                ?? WpfImageQueueFilter.All;
        }

        private static string FormatQueueQuickFilterText(string label, int count)
        {
            return WpfImageQueuePresenter.FormatQuickFilterText(label, count);
        }

        private static string BuildQueueReviewCountSummary(IEnumerable<WpfImageQueueItem> items)
        {
            return WpfImageQueuePresenter.BuildReviewCountSummary(items);
        }

        private static string FormatLabelStatusForQueue(string labelText)
        {
            return WpfImageQueuePresenter.FormatLabelStatus(labelText);
        }

        private static string FormatDetectionStatusForQueue(YoloImageReviewStatus status)
        {
            return WpfImageQueuePresenter.FormatDetectionStatus(status);
        }

        private static PackIconMaterialKind GetQueueIconKind(YoloImageReviewStatus status)
        {
            return WpfImageQueuePresenter.GetIconKind(status);
        }

        private static MediaBrush GetQueueIconBrush(YoloImageReviewStatus status)
        {
            return WpfImageQueuePresenter.GetIconBrush(status);
        }

        private static MediaBrush GetQueueBadgeBackgroundBrush(YoloImageReviewStatus status)
        {
            return WpfImageQueuePresenter.GetBadgeBackgroundBrush(status);
        }

        private static MediaBrush GetQueueRowAccentBrush(YoloImageReviewStatus status)
        {
            return WpfImageQueuePresenter.GetRowAccentBrush(status);
        }

        private static string BuildQueueBadgeText(YoloImageReviewStatus status)
        {
            return WpfImageQueuePresenter.BuildBadgeText(status);
        }

        private static string BuildQueueStatusSummary(YoloImageReviewStatus status)
        {
            return WpfImageQueuePresenter.BuildStatusSummary(status);
        }

        private static string TranslateDetectionMessageForQueue(string message)
        {
            return WpfImageQueuePresenter.TranslateDetectionMessage(message);
        }

        private static string ShortenQueueMessage(string message, int maxLength)
        {
            return WpfImageQueuePresenter.ShortenMessage(message, maxLength);
        }

        private static string BuildReviewDetailText(YoloImageReviewStatus status)
        {
            return WpfImageQueuePresenter.BuildDetailText(status);
        }
    }
}
