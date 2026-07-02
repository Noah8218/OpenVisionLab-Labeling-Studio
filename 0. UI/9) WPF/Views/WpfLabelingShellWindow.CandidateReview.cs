using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using OpenVisionLab.ImageCanvas.ViewModels;
using OpenVisionLab.Mvvm;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingRectangleF = System.Drawing.RectangleF;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // AI candidate review is grouped here so selection, confirmation, and overlay state stay traceable as one workflow.
        private void MainCanvasViewModel_DetectionOverlayClicked(object sender, int candidateIndex)
        {
            if (candidateIndex < 0 || candidateIndex >= pendingDetectionCandidates.Count)
            {
                return;
            }

            YoloWorkerSmokeCandidate candidate = pendingDetectionCandidates[candidateIndex];
            RefreshCandidateListWithPreferred(candidate);
            ShowCandidateReviewWorkflowView();
            CandidateListBox?.ScrollIntoView(CandidateReviewViewModel?.SelectedCandidate);
            ApplyCandidateSelectionReview(candidate);
            UpdateDetectionResultOverlay();
            RedrawReviewRois();
            SetModelStatus($"AI 후보 선택: {FormatCandidate(candidate)}");
        }

        private void ExecuteCandidateConfidenceChangedCommand(double confidence)
        {
            UpdateCandidateConfidenceText();
            if (CandidateListBox == null)
            {
                return;
            }

            RefreshCandidateList();
        }




    }
}
