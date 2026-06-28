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
        // Canvas preview during batch detection is intentionally throttled through Dispatcher yields.
        private bool ShowBatchDetectionImage(WpfImageQueueItem item)
        {
            if (item == null
                || string.IsNullOrWhiteSpace(item.ImagePath)
                || !File.Exists(item.ImagePath))
            {
                return false;
            }

            SelectImageQueueItem(item.ImagePath);
            bool loaded = TryLoadImage(
                item.ImagePath,
                populateQueue: false,
                refreshQueueDetails: false,
                refreshActiveStatus: false,
                appendLoadLog: false);
            if (loaded)
            {
                UpdateSelectedQueueImageButton(item);
            }

            return loaded;
        }

        private bool IsActiveImagePath(string imagePath)
        {
            return !string.IsNullOrWhiteSpace(imagePath)
                && string.Equals(activeImagePath, imagePath, StringComparison.OrdinalIgnoreCase);
        }

        private bool ApplyBatchDetectionResultToCanvas(WpfImageQueueItem item, YoloWorkerSmokeTestResult result)
        {
            if (item == null || result == null)
            {
                return false;
            }

            if (!IsActiveImagePath(item.ImagePath) && !ShowBatchDetectionImage(item))
            {
                return false;
            }

            SelectImageQueueItem(item.ImagePath);
            ApplyBatchDetectionCandidates(result.Candidates, result.Succeeded);
            if (!result.Succeeded)
            {
                ShowBatchDetectionFailureResult(item, result);
            }
            else if (pendingDetectionCandidates.Count == 0)
            {
                ShowBatchNoCandidateResult(item, result);
            }

            return true;
        }

        private async Task YieldBatchDetectionResultFrameAsync(CancellationToken token)
        {
            if (token.IsCancellationRequested || Dispatcher == null)
            {
                return;
            }

            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            if (token.IsCancellationRequested)
            {
                return;
            }

            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
        }

        private void ShowBatchNoCandidateResult(WpfImageQueueItem item, YoloWorkerSmokeTestResult result)
        {
            if (CanvasPanelViewModel == null)
            {
                return;
            }

            WpfDetectionOverlayPresentation presentation = detectionResultPresentationService.BuildNoCandidateOverlay(
                item?.ImagePath ?? result?.ImagePath ?? activeImagePath ?? string.Empty,
                GetCandidateConfidenceFilter());
            CanvasPanelViewModel.SetDetectionOverlay(
                presentation.Title,
                presentation.Summary,
                presentation.SelectedText,
                presentation.Detail,
                presentation.Status);
        }

        private void ShowBatchDetectionFailureResult(WpfImageQueueItem item, YoloWorkerSmokeTestResult result)
        {
            if (CanvasPanelViewModel == null)
            {
                return;
            }

            WpfDetectionOverlayPresentation presentation = detectionResultPresentationService.BuildFailureOverlay(
                item?.ImagePath ?? result?.ImagePath ?? activeImagePath ?? string.Empty,
                result?.Summary);
            CanvasPanelViewModel.SetDetectionOverlay(
                presentation.Title,
                presentation.Summary,
                presentation.SelectedText,
                presentation.Detail,
                presentation.Status);
        }

        private void ApplyBatchDetectionCandidates(IReadOnlyList<YoloWorkerSmokeCandidate> candidates, bool succeeded)
        {
            int loadedCount = candidateReviewState.LoadPendingCandidates(candidates, clearConfirmed: true);
            CandidateReviewViewModel?.ClearReviewHistory();

            RefreshCandidateList();
            RefreshObjectList();
            RedrawReviewRois();
            SetActiveImageDetectionStatus(loadedCount, succeeded);
            AddCandidateReviewHistory(detectionResultPresentationService.BuildCandidateLoadHistory(loadedCount, succeeded, GetCandidateConfidenceFilter()));
            if (candidateReviewState.HasPendingCandidates)
            {
                CandidatesReviewTab.IsSelected = true;
            }

            CenterCanvasAfterInferenceResult();
        }
    }
}
