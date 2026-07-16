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
        // Image queue state changes are kept together because detection status, filters, and detail refreshes share the same review model.
        public int LoadImageQueueFromRoot(string imageRoot, string selectedImagePath = "", bool loadFirstImage = false, bool refreshDetails = true)
        {
            if (string.IsNullOrWhiteSpace(imageRoot) || !Directory.Exists(imageRoot))
            {
                SetDatasetStatus("데이터셋: 이미지 루트 없음");
                AppendLog($"Image root does not exist: {imageRoot}");
                return 0;
            }

            currentImageRoot = imageRoot;
            ImageQueueViewModel?.SetCurrentImageFolder(currentImageRoot, canOpenFolder: true);
            CancelImageQueueDetailRefresh(waitForCompletion: false);
            imageQueueDetailLoadCts = new CancellationTokenSource();
            imageQueueDetailLoadTask = Task.CompletedTask;

            List<string> imagePaths = imageQueueSelectionService.EnumerateImageFiles(imageRoot);
            imageReviewStatus.SetImages(imagePaths);
            imageReviewStatus.LoadReviewStatus(global.Data, imagePaths);
            anomalyImageReviewStatus.SetImages(imagePaths);
            anomalyImageReviewStatus.LoadReviewStatus(global.Data, imagePaths);
            if (IsAnomalyDatasetPurpose()
                && anomalyImageReviewStatus.ImportUnreviewedStatesFromParentFolders().HasChanges)
            {
                anomalyImageReviewStatus.SaveReviewStatus(global.Data);
            }

            suppressImageQueueSelection = true;
            try
            {
                imageQueueItems.ReplaceAll(imageQueueSelectionService.CreateShellItems(imagePaths));
                imageQueueView?.Refresh();
                SelectImageQueueItem(selectedImagePath);
            }
            finally
            {
                suppressImageQueueSelection = false;
            }

            UpdateImageQueueStatusText();
            if (refreshDetails)
            {
                imageQueueDetailLoadTask = StartImageQueueDetailRefreshAsync(imagePaths, imageQueueDetailLoadCts.Token);
            }

            string targetPath = imagePaths.FirstOrDefault(path =>
                    string.Equals(path, selectedImagePath, StringComparison.OrdinalIgnoreCase))
                ?? imagePaths.FirstOrDefault();
            if (loadFirstImage && !string.IsNullOrWhiteSpace(targetPath))
            {
                TryLoadImage(targetPath);
            }
            else if (loadFirstImage)
            {
                ClearActiveImageAfterQueueReset();
            }

            return imagePaths.Count;
        }

        private void PopulateImageQueue(string imageRoot, string selectedImagePath, bool refreshDetails = true)
        {
            if (string.IsNullOrWhiteSpace(imageRoot) || !Directory.Exists(imageRoot))
            {
                return;
            }

            if (imageQueueItems.Count == 0
                || !imageQueueSelectionService.IsSameRoot(imageRoot, currentImageRoot))
            {
                LoadImageQueueFromRoot(imageRoot, selectedImagePath, loadFirstImage: false, refreshDetails: refreshDetails);
                return;
            }

            SelectImageQueueItem(selectedImagePath);
            RefreshActiveImageQueueStatus(hasActiveCandidates: pendingDetectionCandidates.Count > 0);
        }



    }
}
