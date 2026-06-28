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
        // Queue-driven detection owns batch progress and review-state writes; single-image worker calls stay in the main detection flow.
        private async void ExecuteDetectSelectedQueueCommand()
        {
            if (!EnsureInferenceModeForDetection())
            {
                return;
            }

            if (ImageQueueGrid.SelectedItem is not WpfImageQueueItem item)
            {
                AppendLog("먼저 이미지를 선택하세요.");
                return;
            }

            await RunInteractiveDetectionAsync(item.ImagePath, allowSmokeFallback: false).ConfigureAwait(true);
        }

        private async void ExecuteBatchDetectQueueCommand()
        {
            if (!EnsureInferenceModeForDetection())
            {
                return;
            }

            await RunBatchDetectionAsync(GetVisibleQueueItems(), "표시 행").ConfigureAwait(true);
        }

        private async void ExecuteRetryFailedQueueCommand()
        {
            if (!EnsureInferenceModeForDetection())
            {
                return;
            }

            await RunBatchDetectionAsync(
                imageQueueItems.Where(item => item.ReviewState == YoloImageReviewState.Failed).ToList(),
                "실패 재시도").ConfigureAwait(true);
        }

        private void ExecuteStopBatchQueueCommand()
        {
            batchDetectionCts?.Cancel();
            AppendLog("일괄 검사 중지를 요청했습니다.");
        }



    }
}
