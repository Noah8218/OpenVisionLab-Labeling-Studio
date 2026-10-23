using MvcVisionSystem._1._Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns Image Queue next/open/adjacent navigation policy while the Shell supplies
    /// the WPF selection and image-load callbacks.
    /// </summary>
    internal sealed class ImageQueueNavigationAdapter
    {
        private readonly ImageQueueNavigationAdapterContext context;

        internal ImageQueueNavigationAdapter(ImageQueueNavigationAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            ArgumentNullException.ThrowIfNull(context.DataProvider);
        }

        internal void ExecuteNextUnlabeledQueueCommand()
        {
            if (context.IsApplicationCloseApproved?.Invoke() == true)
            {
                return;
            }

            if (!TryOpenNextIncompleteQueueImage())
            {
                context.AppendLog?.Invoke(OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.Text.ImageQueueNavigationAdapter.1"));
            }
        }

        internal bool TryOpenNextIncompleteQueueImage()
            => TryOpenNextIncompleteQueueImage(context.ActiveImagePathProvider?.Invoke() ?? string.Empty);

        internal bool TryOpenNextIncompleteQueueImage(string currentImagePath)
        {
            IReadOnlyList<string> orderedPaths = (context.VisibleQueueItemsProvider?.Invoke() ?? context.QueueItemsProvider?.Invoke() ?? Array.Empty<WpfImageQueueItem>())
                .Select(item => item.ImagePath)
                .ToList();
            if (context.IsAnomalyDatasetPurpose?.Invoke() == true)
            {
                AnomalyImageReviewNextResult nextResult = context.AnomalyImageReviewSession.FindNextUnreviewed(
                    orderedPaths,
                    currentImagePath,
                    isAnomalyPurpose: true);
                if (!nextResult.HasNextImage)
                {
                    return false;
                }

                Func<string, bool> navigationOverride = context.NavigationLoadOverrideProvider?.Invoke();
                bool loaded = navigationOverride?.Invoke(nextResult.NextImagePath)
                    ?? context.LoadAnomalyImage?.Invoke(nextResult.NextImagePath) == true;
                if (loaded)
                {
                    // The queue already contains this image. Keep its rows intact and move only the active selection.
                    context.SelectImageQueueItem?.Invoke(nextResult.NextImagePath);
                    return true;
                }

                return false;
            }

            if (!context.ImageQualityReviewWorkflowService.TryFindNextUnlabeled(orderedPaths, currentImagePath, out string nextImagePath))
            {
                return false;
            }

            bool nextLoaded = context.LoadNextImage?.Invoke(nextImagePath) == true;
            if (nextLoaded) context.SelectImageQueueItem?.Invoke(nextImagePath);
            return nextLoaded;
        }

        internal void ExecuteOpenSelectedQueueImageCommand()
        {
            if (context.IsApplicationCloseApproved?.Invoke() == true)
            {
                return;
            }

            TryOpenSelectedQueueImage(skipIfAlreadyActive: false);
        }

        internal bool TryOpenSelectedQueueImage(bool skipIfAlreadyActive = false)
            => TryOpenSelectedQueueImage(context.OpenSelectionProvider?.Invoke(), skipIfAlreadyActive);

        internal bool TryOpenSelectedQueueImage(WpfImageQueueItem item, bool skipIfAlreadyActive = false)
            => TryOpenSelectedQueueImage(
                context.ImageQueueSelectionService.ResolveOpenSelection(new[] { item }, context.DataProvider()),
                skipIfAlreadyActive);

        internal bool TryOpenSelectedQueueImage(ImageQueueOpenSelection selection, bool skipIfAlreadyActive = false)
        {
            if (selection?.CanOpen != true)
            {
                context.AppendLog?.Invoke(context.BuildOpenQueueSelectionFailureMessage?.Invoke() ?? string.Empty);
                return false;
            }

            WpfImageQueueItem item = selection.Item;
            string openImagePath = selection.OpenImagePath;
            context.UpdateSelectedQueueImageButton?.Invoke(item);

            if (skipIfAlreadyActive
                && string.Equals(openImagePath, context.ActiveImagePathProvider?.Invoke(), StringComparison.OrdinalIgnoreCase))
            {
                context.SelectImageQueueItem?.Invoke(item.ImagePath);
                return true;
            }

            bool loaded = context.LoadSelectedImage?.Invoke(openImagePath) == true;
            if (loaded)
            {
                context.SelectImageQueueItem?.Invoke(item.ImagePath);
                context.UpdateSelectedQueueImageButton?.Invoke(item);
            }

            return loaded;
        }

        internal bool TryOpenAdjacentQueueImage(int direction, bool includeFilteredItems = false)
        {
            IReadOnlyList<WpfImageQueueItem> queueItems = context.QueueItemsProvider?.Invoke();
            if (direction == 0 || queueItems == null || queueItems.Count == 0)
            {
                return false;
            }

            WpfImageQueueItem targetItem = context.ImageQueueSelectionService.FindAdjacentOpenableItem(
                includeFilteredItems ? queueItems : context.VisibleQueueItemsProvider?.Invoke() ?? Array.Empty<WpfImageQueueItem>(),
                context.ActiveImagePathProvider?.Invoke() ?? string.Empty,
                context.SelectedQueueItemProvider?.Invoke()?.ImagePath,
                direction,
                CanOpenQueueItem);
            if (targetItem == null)
            {
                return false;
            }

            context.SelectImageQueueItem?.Invoke(targetItem.ImagePath);
            return TryOpenSelectedQueueImage(targetItem, skipIfAlreadyActive: true);
        }

        internal bool TryOpenNextReviewPendingImage(string currentImagePath, IReadOnlyList<WpfImageQueueItem> orderedItems)
        {
            WpfImageQueueItem target = context.ImageQueueSelectionService.FindNextReviewPendingItem(orderedItems, currentImagePath, CanOpenQueueItem)
                ?? context.ImageQueueSelectionService.FindNextReviewPendingItem(context.QueueItemsProvider?.Invoke(), currentImagePath, CanOpenQueueItem);
            return target != null && TryOpenSelectedQueueImage(target, skipIfAlreadyActive: true);
        }

        internal string GetPreviousImagePath()
        {
            IReadOnlyList<WpfImageQueueItem> items = context.QueueItemsProvider?.Invoke() ?? Array.Empty<WpfImageQueueItem>();
            string activePath = context.ActiveImagePathProvider?.Invoke() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(activePath)) return string.Empty;
            LabelingProjectData data = context.DataProvider();
            for (int index = 0; index < items.Count; index++)
            {
                WpfImageQueueItem item = items[index];
                bool isActive = string.Equals(item?.ImagePath, activePath, StringComparison.OrdinalIgnoreCase)
                    || context.ImageQueueSelectionService.TryResolveOpenImagePath(item, data, out string openPath)
                        && string.Equals(openPath, activePath, StringComparison.OrdinalIgnoreCase);
                if (!isActive) continue;
                return index > 0 && context.ImageQueueSelectionService.TryResolveOpenImagePath(items[index - 1], data, out string previousPath)
                    ? previousPath : string.Empty;
            }
            return string.Empty;
        }

        internal bool CanOpenQueueItem(WpfImageQueueItem item)
            => context.ImageQueueSelectionService.TryResolveOpenImagePath(item, context.DataProvider(), out _);
    }

    internal sealed class ImageQueueNavigationAdapterContext
    {
        internal Func<LabelingProjectData> DataProvider { get; init; }
        internal ImageQualityReviewWorkflowService ImageQualityReviewWorkflowService { get; init; }
        internal AnomalyImageReviewSession AnomalyImageReviewSession { get; init; }
        internal ImageQueueSelectionService ImageQueueSelectionService { get; init; }
        internal Func<bool> IsApplicationCloseApproved { get; init; }
        internal Func<bool> IsAnomalyDatasetPurpose { get; init; }
        internal Func<IReadOnlyList<WpfImageQueueItem>> QueueItemsProvider { get; init; }
        internal Func<string> ActiveImagePathProvider { get; init; }
        internal Func<WpfImageQueueItem> SelectedQueueItemProvider { get; init; }
        internal Func<IReadOnlyList<WpfImageQueueItem>> VisibleQueueItemsProvider { get; init; }
        internal Func<ImageQueueOpenSelection> OpenSelectionProvider { get; init; }
        internal Func<Func<string, bool>> NavigationLoadOverrideProvider { get; init; }
        internal Func<string, bool> LoadAnomalyImage { get; init; }
        internal Func<string, bool> LoadNextImage { get; init; }
        internal Func<string, bool> LoadSelectedImage { get; init; }
        internal Action<string> SelectImageQueueItem { get; init; }
        internal Action<WpfImageQueueItem> UpdateSelectedQueueImageButton { get; init; }
        internal Func<string> BuildOpenQueueSelectionFailureMessage { get; init; }
        internal Action<string> AppendLog { get; init; }
    }
}
