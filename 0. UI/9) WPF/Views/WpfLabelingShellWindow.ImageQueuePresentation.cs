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
            int visibleCount = imageQueueView?.Cast<object>().Count() ?? imageQueueItems.Count;
            UpdateQueueQuickFilterButtons();
            SetDatasetStatus(WpfImageQueueFilterService.BuildDatasetStatusTextWithActiveImage(
                imageQueueItems,
                visibleCount,
                GetSelectedImageQueueFilter(),
                loadedCount,
                totalToLoad,
                activeImagePath));
        }

        private void UpdateQueueQuickFilterButtons()
        {
            WpfImageQueueFilter filter = GetSelectedImageQueueFilter();
            ImageQueueViewModel.SetQuickFilterState(
                filter,
                WpfImageQueueFilterService.CountByFilter(imageQueueItems, WpfImageQueueFilter.Candidate),
                WpfImageQueueFilterService.CountByFilter(imageQueueItems, WpfImageQueueFilter.Failed),
                WpfImageQueueFilterService.CountByFilter(imageQueueItems, WpfImageQueueFilter.Confirmed),
                WpfImageQueueFilterService.CountByFilter(imageQueueItems, WpfImageQueueFilter.Skipped),
                WpfImageQueueFilterService.CountByFilter(imageQueueItems, WpfImageQueueFilter.NoCandidate),
                WpfImageQueueFilterService.CountByFilter(imageQueueItems, WpfImageQueueFilter.Unlabeled));
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
