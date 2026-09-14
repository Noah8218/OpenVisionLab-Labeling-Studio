using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the WPF projection of an Image Queue catalog snapshot.
    /// File enumeration and request lifetime remain in the existing catalog
    /// service/coordinator; this adapter owns rows, suggestion presentation,
    /// and the handoff to detail refresh and image loading.
    /// </summary>
    internal sealed class ImageQueueCatalogProjectionAdapter
    {
        private readonly ImageQueueCatalogProjectionAdapterContext context;

        internal ImageQueueCatalogProjectionAdapter(ImageQueueCatalogProjectionAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            ArgumentNullException.ThrowIfNull(context.DataProvider);
        }

        internal int Apply(
            ImageQueueCatalogLoadRequest request,
            ImageQueueCatalogLoadResult snapshot)
        {
            if (request == null
                || snapshot == null
                || context.IsCurrentCatalogRequest?.Invoke(request) != true)
            {
                return 0;
            }

            context.ImageQualityReviewWorkflowService.AdoptCatalog(snapshot.ReviewWorkflow);
            context.AnomalyImageReviewSession.AdoptCatalog(request.ImageRoot, snapshot.AnomalyReviewSession);
            UpdateAnomalyFolderStateSuggestion(request);

            context.SetImageQueueSelectionSuppressed?.Invoke(true);
            try
            {
                IReadOnlyList<WpfImageQueueItem> items = context.ImageQueueSelectionService.CreateShellItemsFromCatalog(snapshot.CatalogEntries);
                if (request.IsAnomalyPurpose)
                {
                    AnomalyImageReviewQueueProjection projection = context.AnomalyImageReviewSession.BuildQueue(
                        items.Select(item => item.ImagePath).ToList(),
                        isAnomalyPurpose: true);
                    foreach (WpfImageQueueItem item in items)
                    {
                        WpfImageQueuePresenter.ApplyAnomalyReviewStatusToItem(item, projection.GetStatus(item.ImagePath));
                    }
                }

                context.ImageQueueItems.ReplaceAll(items);
                RebuildImageQueueItemIndex(items);
                context.RefreshImageQueueView?.Invoke();
                context.SelectImageQueueItem?.Invoke(request.SelectedImagePath);
            }
            finally
            {
                context.SetImageQueueSelectionSuppressed?.Invoke(false);
            }

            context.UpdateImageQueueStatusText?.Invoke();
            if (request.RefreshDetails && snapshot.ImagePaths.Count > 0)
            {
                IReadOnlyDictionary<string, WpfImageQueueItem> itemLookup =
                    new Dictionary<string, WpfImageQueueItem>(context.ImageQueueItemsByPath, StringComparer.OrdinalIgnoreCase);
                context.BeginDetailRefresh?.Invoke(snapshot.ImagePaths, itemLookup, request.Data);
            }

            string targetPath = snapshot.ImagePaths.FirstOrDefault(path =>
                    string.Equals(path, request.SelectedImagePath, StringComparison.OrdinalIgnoreCase))
                ?? snapshot.ImagePaths.FirstOrDefault();
            if (request.LoadFirstImage && !string.IsNullOrWhiteSpace(targetPath))
            {
                context.TryLoadImage?.Invoke(targetPath);
            }
            else if (request.LoadFirstImage)
            {
                context.ClearActiveImageAfterQueueReset?.Invoke();
            }

            return snapshot.ImagePaths.Count;
        }

        internal void UpdateAnomalyFolderStateSuggestion(ImageQueueCatalogLoadRequest request)
        {
            if (context.AnomalyImageReviewSession.ShouldShowFolderSuggestion(request?.IsAnomalyPurpose == true))
            {
                context.ImageQueueViewModel?.SetAnomalyFolderStateSuggestion(context.AnomalyImageReviewSession.FolderStateSuggestion);
                return;
            }

            context.ImageQueueViewModel?.ClearAnomalyFolderStateSuggestion();
        }

        internal void ExecuteApplyAnomalyFolderStateSuggestionCommand()
        {
            if (context.IsAnomalyDatasetPurpose?.Invoke() != true)
            {
                context.ImageQueueViewModel?.ClearAnomalyFolderStateSuggestion();
                return;
            }

            AnomalyFolderStateSuggestionApplyResult applyResult = context.AnomalyImageReviewSession.ApplyFolderSuggestion(
                isAnomalyPurpose: true,
                context.DataProvider());
            context.ImageQueueViewModel?.ClearAnomalyFolderStateSuggestion();
            if (!applyResult.HasChanges)
            {
                return;
            }

            AnomalyImageReviewFolderImportResult result = applyResult.ImportResult;
            foreach (WpfImageQueueItem item in context.ImageQueueItems)
            {
                WpfImageQueuePresenter.ApplyAnomalyReviewStatusToItem(
                    item,
                    context.AnomalyImageReviewSession.GetStatus(item.ImagePath, isAnomalyPurpose: true));
            }

            context.RefreshImageQueueView?.Invoke();
            context.UpdateImageQueueStatusText?.Invoke();
            context.SetDatasetStatus?.Invoke($"OK/NG 이미지 판정: 폴더명 기준 일괄 판정 완료 (정상 {result.NormalImageCount}장 / 이상 {result.AbnormalImageCount}장, 기존 수동 판정 {result.ExistingReviewCount}장 유지)");
            context.AppendLog?.Invoke($"Anomaly folder-state suggestion applied: normal={result.NormalImageCount}, abnormal={result.AbnormalImageCount}, existing={result.ExistingReviewCount}.");
        }

        internal void ExecuteDismissAnomalyFolderStateSuggestionCommand()
        {
            context.AnomalyImageReviewSession.DismissFolderSuggestion();
            context.ImageQueueViewModel?.ClearAnomalyFolderStateSuggestion();
            context.SetDatasetStatus?.Invoke("OK/NG 이미지 판정: 폴더명은 적용하지 않았습니다. 이미지를 하나씩 정상 또는 이상으로 판정하세요.");
            context.AppendLog?.Invoke("Anomaly folder-state suggestion dismissed; images remain unreviewed until an operator reviews them.");
        }

        internal void RebuildImageQueueItemIndex(IEnumerable<WpfImageQueueItem> items)
        {
            context.ImageQueueItemsByPath.Clear();
            foreach (WpfImageQueueItem item in items ?? Enumerable.Empty<WpfImageQueueItem>())
            {
                if (item != null && !string.IsNullOrWhiteSpace(item.ImagePath))
                {
                    context.ImageQueueItemsByPath[item.ImagePath] = item;
                }
            }
        }
    }

    internal sealed class ImageQueueCatalogProjectionAdapterContext
    {
        internal Func<LabelingProjectData> DataProvider { get; init; }
        internal ImageQualityReviewWorkflowService ImageQualityReviewWorkflowService { get; init; }
        internal AnomalyImageReviewSession AnomalyImageReviewSession { get; init; }
        internal ImageQueueSelectionService ImageQueueSelectionService { get; init; }
        internal BulkObservableCollection<WpfImageQueueItem> ImageQueueItems { get; init; }
        internal IDictionary<string, WpfImageQueueItem> ImageQueueItemsByPath { get; init; }
        internal WpfImageQueuePanelViewModel ImageQueueViewModel { get; init; }
        internal Func<ImageQueueCatalogLoadRequest, bool> IsCurrentCatalogRequest { get; init; }
        internal Func<bool> IsAnomalyDatasetPurpose { get; init; }
        internal Action<bool> SetImageQueueSelectionSuppressed { get; init; }
        internal Action<string> SelectImageQueueItem { get; init; }
        internal Action RefreshImageQueueView { get; init; }
        internal Action UpdateImageQueueStatusText { get; init; }
        internal Action<IReadOnlyList<string>, IReadOnlyDictionary<string, WpfImageQueueItem>, LabelingProjectData> BeginDetailRefresh { get; init; }
        internal Action<string> TryLoadImage { get; init; }
        internal Action ClearActiveImageAfterQueueReset { get; init; }
        internal Action<string> SetDatasetStatus { get; init; }
        internal Action<string> AppendLog { get; init; }
    }
}
