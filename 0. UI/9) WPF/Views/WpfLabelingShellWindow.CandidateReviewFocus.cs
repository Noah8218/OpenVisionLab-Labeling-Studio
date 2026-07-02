using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using OpenVisionLab.ImageCanvas.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingRectangleF = System.Drawing.RectangleF;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Candidate focusing is viewer navigation, not candidate list rebuilding.
        private bool FocusSelectedCandidateInViewer(bool logIfMissing)
        {
            YoloWorkerSmokeCandidate candidate = GetSelectedCandidate();
            if (candidate == null)
            {
                if (logIfMissing)
                {
                    AppendLog("초점을 맞출 AI 후보를 선택하세요.");
                }

                return false;
            }

            return FocusCandidateInViewer(candidate, logIfMissing);
        }

        private void ExecuteFocusCurrentLabelCommand()
        {
            FocusCurrentLabelForSelectedCandidate(logIfMissing: true);
        }

        private bool FocusCurrentLabelForSelectedCandidate(bool logIfMissing)
        {
            YoloWorkerSmokeCandidate candidate = GetSelectedCandidate();
            if (candidate == null)
            {
                if (logIfMissing)
                {
                    AppendLog("\uD604\uC7AC \uB77C\uBCA8\uC744 \uD655\uC778\uD560 AI \uD6C4\uBCF4\uB97C \uC120\uD0DD\uD558\uC138\uC694.");
                }

                return false;
            }

            DrawingRectangle candidateBounds = GetClippedCandidateBounds(candidate);
            WpfCandidateOverlapInfo overlap = GetCandidateOverlapInfo(candidateBounds);
            if (!overlap.HasCurrentObject)
            {
                if (logIfMissing)
                {
                    AppendLog("\uC120\uD0DD\uD55C AI \uD6C4\uBCF4\uC640 \uACB9\uCE58\uB294 \uD604\uC7AC \uB77C\uBCA8\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.");
                }

                return false;
            }

            // Candidate Review can point at the existing label, but Object Review still owns label editing.
            ApplyCanvasDisplayMode(WpfCanvasDisplayMode.LabelsOnly, redraw: true, logChange: false);
            RefreshObjectListWithSelection(overlap.CurrentObjectRef);
            ShowSavedLabelsWorkflowView();
            if (overlap.CurrentObjectRef.Source == WpfObjectReviewSource.ManualRoi)
            {
                MainCanvasViewModel.SelectRoiOverlayById(overlap.CurrentObjectRef.SourceId, refreshImmediately: true);
            }

            if (!overlap.Bounds.IsEmpty && MainCanvasViewModel?.ImageViewer != null)
            {
                MainCanvasViewModel.ImageViewer.FitToRect(BuildCandidateFocusRect(overlap.Bounds));
            }

            SetModelStatus($"\uD604\uC7AC \uB77C\uBCA8 \uC120\uD0DD: {overlap.Label}  {FormatBoundsCompact(overlap.Bounds)}");
            return true;
        }

        private bool FocusCandidateInViewer(YoloWorkerSmokeCandidate candidate, bool logIfMissing)
        {
            if (candidate == null || activeImageSize.IsEmpty)
            {
                if (logIfMissing)
                {
                    AppendLog("후보 초점 이동을 하려면 먼저 이미지를 불러오세요.");
                }

                return false;
            }

            DrawingRectangle bounds = GetClippedCandidateBounds(candidate);
            if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
            {
                if (logIfMissing)
                {
                    AppendLog("후보 영역이 이미지 범위 밖에 있습니다.");
                }

                return false;
            }

            ApplyCanvasDisplayMode(WpfCanvasDisplayMode.InferenceOnly, redraw: true, logChange: false);
            MainCanvasViewModel.ImageViewer.FitToRect(BuildCandidateFocusRect(bounds));
            SetModelStatus($"후보 초점: {candidate.ClassName} {candidate.Confidence:P1}  {WpfCandidateReviewPresenter.FormatBoundsCompact(bounds)}");
            return true;
        }

        private DrawingRectangleF BuildCandidateFocusRect(DrawingRectangle bounds)
        {
            float padding = Math.Max(12F, Math.Max(bounds.Width, bounds.Height) * 0.65F);
            float left = Math.Max(0F, bounds.Left - padding);
            float right = Math.Min(activeImageSize.Width, bounds.Right + padding);
            float top = Math.Max(0F, bounds.Top - padding);
            float bottom = Math.Min(activeImageSize.Height, bounds.Bottom + padding);

            if (right <= left)
            {
                right = Math.Min(activeImageSize.Width, left + 1F);
            }

            if (bottom <= top)
            {
                bottom = Math.Min(activeImageSize.Height, top + 1F);
            }

            return DrawingRectangleF.FromLTRB(
                left,
                activeImageSize.Height - bottom,
                right,
                activeImageSize.Height - top);
        }
    }
}
