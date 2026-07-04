using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Queue state updates are isolated so batch progress changes do not touch canvas result rendering.
        private void ApplyDetectionResultToQueueItem(
            WpfImageQueueItem item,
            YoloWorkerSmokeTestResult result,
            bool saveReviewStatus = true,
            bool refreshQueueView = true,
            bool updateQueueStatusText = true)
        {
            if (item == null || result == null)
            {
                return;
            }

            string imageName = Path.GetFileNameWithoutExtension(item.ImagePath);
            YoloImageReviewStatus status = result.Succeeded
                ? result.CandidateCount > 0
                    ? imageReviewStatus.SetDetectionCandidates(item.ImagePath, imageName, result.CandidateCount)
                    : imageReviewStatus.SetDetectionNoCandidates(item.ImagePath, imageName)
                : imageReviewStatus.SetDetectionFailed(item.ImagePath, imageName, result.Summary);
            ApplyReviewStatusToItem(item, status);
            ApplyAnomalyClassificationToImage(item.ImagePath, imageName, result.Candidates, saveReviewStatus);
            if (saveReviewStatus)
            {
                imageReviewStatus.SaveReviewStatus(global.Data);
            }

            if (refreshQueueView)
            {
                imageQueueView?.Refresh();
            }

            if (updateQueueStatusText)
            {
                UpdateImageQueueStatusText();
            }
        }

        private IReadOnlyList<WpfImageQueueItem> GetVisibleQueueItems()
        {
            return imageQueueView == null
                ? imageQueueItems.ToList()
                : imageQueueView.Cast<object>().OfType<WpfImageQueueItem>().ToList();
        }

        private void UpdateBatchDetectionControls(string scopeText = "", string currentFileName = "")
        {
            UpdateYoloCommandButtons();
            WpfBatchDetectionControlState controlState = batchDetectionProgressService.BuildControlState(
                isBatchDetectionRunning,
                batchDetectionTotalCount,
                batchDetectionCompletedCount,
                scopeText,
                currentFileName);

            BatchProgressBar.Maximum = controlState.ProgressMaximum;
            BatchProgressBar.Value = controlState.ProgressValue;
            BatchStatusText.Text = controlState.StatusText;

            if (controlState.ShouldRefreshQueueStatus)
            {
                UpdateImageQueueStatusText();
            }
            else
            {
                SetDatasetStatus(controlState.DatasetStatusText);
            }
        }
    }
}
