using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingSize = System.Drawing.Size;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Detection result application owns canvas/list updates; execution code should only return worker results.
        private void ApplyDetectionCandidates(IReadOnlyList<YoloWorkerSmokeCandidate> candidates, bool succeeded)
        {
            int loadedCount = candidateReviewState.LoadPendingCandidates(candidates, clearConfirmed: true);
            CandidateReviewViewModel?.ClearReviewHistory();

            RefreshCandidateList();
            RefreshObjectList();
            RedrawReviewRois();
            SetActiveImageDetectionStatus(loadedCount, succeeded);
            AddCandidateReviewHistory(detectionResultPresentationService.BuildCandidateLoadHistory(loadedCount, succeeded, GetCandidateConfidenceFilter()));

            if (!candidateReviewState.HasPendingCandidates)
            {
                CenterCanvasAfterInferenceResult();
                AppendLog("검출 후보가 없습니다.");
                return;
            }

            CandidatesReviewTab.IsSelected = true;
            CenterCanvasAfterInferenceResult();
            AppendLog($"검출 후보 로드: {loadedCount}개");
        }
        private void AddCandidateReviewHistory(string message)
        {
            CandidateReviewViewModel?.AddReviewHistory(message);
        }

        private void CenterCanvasAfterInferenceResult()
        {
            if (MainCanvasViewModel?.ImageViewer == null)
            {
                return;
            }

            MainCanvasViewModel.ImageViewer.ZoomToFit();
            Dispatcher.BeginInvoke(new Action(() =>
            {
                MainCanvasViewModel?.ImageViewer?.ZoomToFit();
            }), DispatcherPriority.Render);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                MainCanvasViewModel?.ImageViewer?.ZoomToFit();
            }), DispatcherPriority.ApplicationIdle);
        }

        private void RedrawReviewRois()
        {
            EnsureManualRoiMetadataCount();
            using (MainCanvasViewModel.ImageViewer.SuppressRefresh())
            {
                MainCanvasViewModel.ClearRois();
                for (int i = 0; i < manualRois.Count; i++)
                {
                    DrawingRectangle roi = manualRois[i];
                    if (roi.IsEmpty)
                    {
                        continue;
                    }

                    var overlay = MainCanvasViewModel.AddInitialRoi(roi, GetManualRoiShapeKind(i));
                    manualRoiOverlayIds[i] = overlay?.UniqueId ?? string.Empty;
                }

                foreach (YoloWorkerSmokeCandidate candidate in confirmedDetectionCandidates)
                {
                    DrawingRectangle bounds = GetClippedCandidateBounds(candidate);
                    if (!bounds.IsEmpty)
                    {
                        MainCanvasViewModel.AddInitialRoi(bounds);
                    }
                }

                MainCanvasViewModel.SetDetectionOverlays(BuildDetectionOverlays(pendingDetectionCandidates));
                RefreshPolygonOverlays();
            }

            MainCanvasViewModel.ImageViewer.RefreshGL();
        }
    }
}
