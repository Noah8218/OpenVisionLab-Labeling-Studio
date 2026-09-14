using MvcVisionSystem._1._Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the Shell-facing image-load handoff that surrounds the existing
    /// decode and catalog owners. The adapter keeps save-before-change,
    /// deferred review refresh, and adjacent preload sequencing together so
    /// the generated Window remains a composition root.
    /// </summary>
    internal sealed class ImageQueueLoadingAdapter
    {
        private readonly ImageQueueLoadingAdapterContext context;

        internal ImageQueueLoadingAdapter(ImageQueueLoadingAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        internal void ScheduleImageLoadReviewRefresh(string imagePath, bool refreshActiveStatus, bool refreshClassCatalog)
        {
            context.ScheduleBackground?.Invoke(() => ApplyScheduledImageLoadReviewRefresh(
                imagePath,
                refreshActiveStatus,
                refreshClassCatalog));
        }

        internal void RefreshImageLoadReviewState(bool refreshActiveStatus, bool refreshClassCatalog)
        {
            context.RefreshCandidateList?.Invoke();
            context.RefreshObjectList?.Invoke();
            if (refreshClassCatalog)
            {
                context.PopulateClassList?.Invoke();
            }

            if (refreshActiveStatus)
            {
                context.RefreshActiveImageQueueStatus?.Invoke(false);
            }
            else
            {
                context.UpdateImageQueueStatusText?.Invoke();
            }
        }

        internal ImageDecodeCacheDiagnostics GetImageDecodeCacheDiagnostics()
            => context.ImageDecodeCacheService?.GetDiagnostics() ?? new ImageDecodeCacheDiagnostics(0, 0L, 0L, 0L, 0L, 0L, 0, 0L);

        internal bool TrySavePendingAnnotationsBeforeImageChange(string nextImagePath)
        {
            string activeImagePath = context.ActiveImagePathProvider?.Invoke() ?? string.Empty;
            if (context.ActiveImageBitmapProvider?.Invoke() == null
                || string.IsNullOrWhiteSpace(activeImagePath)
                || string.IsNullOrWhiteSpace(nextImagePath)
                || string.Equals(Path.GetFullPath(activeImagePath), Path.GetFullPath(nextImagePath), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (context.AnnotationDirtyState?.IsDirty != true
                && context.HasPendingMaskStrokeCommitWork?.Invoke() != true)
            {
                return true;
            }

            if (context.SaveCurrentAnnotations?.Invoke(out int savedCount) == true)
            {
                context.AppendLog?.Invoke($"이미지 전환 전 라벨 자동 저장: {Path.GetFileName(activeImagePath)} / 객체 {savedCount}개");
                return true;
            }

            context.AppendLog?.Invoke($"이미지 전환 중단: 현재 이미지 라벨을 저장하지 못했습니다. {Path.GetFileName(activeImagePath)}");
            return false;
        }

        internal void PreloadAdjacentQueueImages(string imagePath)
        {
            // Adjacent preload is only useful after the interactive shell is loaded; headless construction tests should not open extra image files.
            if (context.IsShellLoaded?.Invoke() != true
                || string.IsNullOrWhiteSpace(imagePath)
                || (context.QueueItemCountProvider?.Invoke() ?? 0) == 0)
            {
                return;
            }

            context.ImageDecodePreloadService?.StartAdjacentPreload(
                imagePath,
                context.QueueImagePathsProvider?.Invoke() ?? Array.Empty<string>(),
                context.ImageDecodeCacheService,
                File.Exists,
                path => context.ImageDecodeService.TryDecodeForCache(path));
        }

        internal void PopulateImageQueue(string imageRoot, string selectedImagePath, bool refreshDetails = true)
        {
            if (string.IsNullOrWhiteSpace(imageRoot) || !Directory.Exists(imageRoot))
            {
                return;
            }

            string currentImageRoot = context.CurrentImageRootProvider?.Invoke() ?? string.Empty;
            if ((context.QueueItemCountProvider?.Invoke() ?? 0) == 0
                || context.IsSameImageRoot?.Invoke(imageRoot, currentImageRoot) != true)
            {
                context.LoadImageQueueFromRoot?.Invoke(imageRoot, selectedImagePath, false, refreshDetails);
                return;
            }

            context.SelectImageQueueItem?.Invoke(selectedImagePath);
            context.RefreshActiveImageQueueStatus?.Invoke(
                context.PendingCandidateCountProvider?.Invoke() > 0);
        }

        internal void PopulateImageQueueAfterLoad(string imagePath, bool refreshQueueDetails)
        {
            string currentImageRoot = context.CurrentImageRootProvider?.Invoke() ?? string.Empty;
            string queueRoot = context.IsImageQueued?.Invoke(imagePath) == true
                && !string.IsNullOrWhiteSpace(currentImageRoot)
                && Directory.Exists(currentImageRoot)
                ? currentImageRoot
                : Path.GetDirectoryName(imagePath);
            PopulateImageQueue(queueRoot, imagePath, refreshQueueDetails);
        }

        private void ApplyScheduledImageLoadReviewRefresh(
            string imagePath,
            bool refreshActiveStatus,
            bool refreshClassCatalog)
        {
            if (context.IsApplicationCloseApproved?.Invoke() == true
                || !string.Equals(
                    context.ActiveImagePathProvider?.Invoke(),
                    imagePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            RefreshImageLoadReviewState(refreshActiveStatus, refreshClassCatalog);
        }
    }

    internal delegate bool SaveImageAnnotationsCallback(out int savedCount);

    internal sealed class ImageQueueLoadingAdapterContext
    {
        internal ImageDecodeCacheService ImageDecodeCacheService { get; init; }
        internal ImageDecodePreloadService ImageDecodePreloadService { get; init; }
        internal ImageDecodeService ImageDecodeService { get; init; }
        internal AnnotationDirtyState AnnotationDirtyState { get; init; }
        internal Func<bool> IsApplicationCloseApproved { get; init; }
        internal Func<bool> IsShellLoaded { get; init; }
        internal Func<string> ActiveImagePathProvider { get; init; }
        internal Func<System.Drawing.Bitmap> ActiveImageBitmapProvider { get; init; }
        internal Func<bool> HasPendingMaskStrokeCommitWork { get; init; }
        internal SaveImageAnnotationsCallback SaveCurrentAnnotations { get; init; }
        internal Action<Action> ScheduleBackground { get; init; }
        internal Action RefreshCandidateList { get; init; }
        internal Action RefreshObjectList { get; init; }
        internal Action PopulateClassList { get; init; }
        internal Action<bool> RefreshActiveImageQueueStatus { get; init; }
        internal Action UpdateImageQueueStatusText { get; init; }
        internal Action<string> AppendLog { get; init; }
        internal Action<string, string, bool, bool> LoadImageQueueFromRoot { get; init; }
        internal Action<string> SelectImageQueueItem { get; init; }
        internal Func<int> QueueItemCountProvider { get; init; }
        internal Func<int> PendingCandidateCountProvider { get; init; }
        internal Func<IEnumerable<string>> QueueImagePathsProvider { get; init; }
        internal Func<string, bool> IsImageQueued { get; init; }
        internal Func<string, string, bool> IsSameImageRoot { get; init; }
        internal Func<string> CurrentImageRootProvider { get; init; }
    }
}
