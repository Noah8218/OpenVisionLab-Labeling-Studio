using MvcVisionSystem._1._Core;
using OpenVisionLab.ImageCanvas.ViewModels;
using System;
using DrawingRectangle = System.Drawing.Rectangle;
using DrawingRectangleF = System.Drawing.RectangleF;
using DrawingSize = System.Drawing.Size;

namespace MvcVisionSystem
{
    // Candidate review owns the decision; this adapter owns only viewer navigation.
    internal sealed class CandidateReviewViewerNavigator
    {
        private readonly RoiImageCanvasViewModel canvasViewModel;
        private readonly Func<DrawingSize> activeImageSizeProvider;
        private readonly Func<YoloWorkerSmokeCandidate> selectedCandidateProvider;
        private readonly Func<DrawingRectangle, WpfCandidateOverlapInfo> overlapProvider;
        private readonly Action<WpfCanvasDisplayMode, bool, bool> applyDisplayMode;
        private readonly Action<WpfObjectReviewItemRef> refreshObjectListWithSelection;
        private readonly Action showSavedLabelsWorkflowView;
        private readonly Action<string> setModelStatus;
        private readonly Action<string> appendLog;

        public CandidateReviewViewerNavigator(
            RoiImageCanvasViewModel canvasViewModel,
            Func<DrawingSize> activeImageSizeProvider,
            Func<YoloWorkerSmokeCandidate> selectedCandidateProvider,
            Func<DrawingRectangle, WpfCandidateOverlapInfo> overlapProvider,
            Action<WpfCanvasDisplayMode, bool, bool> applyDisplayMode,
            Action<WpfObjectReviewItemRef> refreshObjectListWithSelection,
            Action showSavedLabelsWorkflowView,
            Action<string> setModelStatus,
            Action<string> appendLog)
        {
            this.canvasViewModel = canvasViewModel ?? throw new ArgumentNullException(nameof(canvasViewModel));
            this.activeImageSizeProvider = activeImageSizeProvider ?? throw new ArgumentNullException(nameof(activeImageSizeProvider));
            this.selectedCandidateProvider = selectedCandidateProvider ?? throw new ArgumentNullException(nameof(selectedCandidateProvider));
            this.overlapProvider = overlapProvider ?? throw new ArgumentNullException(nameof(overlapProvider));
            this.applyDisplayMode = applyDisplayMode ?? throw new ArgumentNullException(nameof(applyDisplayMode));
            this.refreshObjectListWithSelection = refreshObjectListWithSelection ?? throw new ArgumentNullException(nameof(refreshObjectListWithSelection));
            this.showSavedLabelsWorkflowView = showSavedLabelsWorkflowView ?? throw new ArgumentNullException(nameof(showSavedLabelsWorkflowView));
            this.setModelStatus = setModelStatus ?? throw new ArgumentNullException(nameof(setModelStatus));
            this.appendLog = appendLog ?? throw new ArgumentNullException(nameof(appendLog));
        }

        public bool FocusSelectedCandidateInViewer(bool logIfMissing)
        {
            YoloWorkerSmokeCandidate candidate = selectedCandidateProvider();
            if (candidate == null)
            {
                if (logIfMissing)
                {
                    appendLog("초점을 맞출 AI 후보를 선택하세요.");
                }

                return false;
            }

            return FocusCandidateInViewer(candidate, logIfMissing);
        }

        public void ExecuteFocusCurrentLabelCommand()
        {
            FocusCurrentLabelForSelectedCandidate(logIfMissing: true);
        }

        public bool FocusCurrentLabelForSelectedCandidate(bool logIfMissing)
        {
            YoloWorkerSmokeCandidate candidate = selectedCandidateProvider();
            if (candidate == null)
            {
                if (logIfMissing)
                {
                    appendLog("현재 라벨을 확인할 AI 후보를 선택하세요.");
                }

                return false;
            }

            DrawingRectangle candidateBounds = CandidateReviewPresentationService.ClipCandidateBounds(candidate, activeImageSizeProvider());
            WpfCandidateOverlapInfo overlap = overlapProvider(candidateBounds);
            if (!overlap.HasCurrentObject)
            {
                if (logIfMissing)
                {
                    appendLog("선택한 AI 후보와 겹치는 현재 라벨이 없습니다.");
                }

                return false;
            }

            // Candidate Review can point at the existing label, but Object Review still owns label editing.
            applyDisplayMode(WpfCanvasDisplayMode.LabelsOnly, true, false);
            refreshObjectListWithSelection(overlap.CurrentObjectRef);
            showSavedLabelsWorkflowView();
            if (overlap.CurrentObjectRef.Source == WpfObjectReviewSource.ManualRoi)
            {
                canvasViewModel.SelectRoiOverlayById(overlap.CurrentObjectRef.SourceId, refreshImmediately: true);
            }

            if (!overlap.Bounds.IsEmpty && canvasViewModel?.ImageViewer != null)
            {
                canvasViewModel.ImageViewer.FitToRect(BuildCandidateFocusRectForShell(overlap.Bounds));
            }

            setModelStatus($"현재 라벨 선택: {overlap.Label}  {CandidateReviewPresenter.FormatBoundsCompact(overlap.Bounds)}");
            return true;
        }

        public bool FocusCandidateInViewer(YoloWorkerSmokeCandidate candidate, bool logIfMissing)
        {
            DrawingSize activeImageSize = activeImageSizeProvider();
            if (candidate == null || activeImageSize.IsEmpty)
            {
                if (logIfMissing)
                {
                    appendLog("후보 초점 이동을 하려면 먼저 이미지를 불러오세요.");
                }

                return false;
            }

            DrawingRectangle bounds = CandidateReviewPresentationService.ClipCandidateBounds(candidate, activeImageSize);
            if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0)
            {
                if (logIfMissing)
                {
                    appendLog("후보 영역이 이미지 범위 밖에 있습니다.");
                }

                return false;
            }

            applyDisplayMode(WpfCanvasDisplayMode.InferenceOnly, true, false);
            canvasViewModel.ImageViewer.FitToRect(BuildCandidateFocusRectForShell(bounds));
            setModelStatus($"후보 초점: {candidate.ClassName} {CandidateReviewPresenter.FormatConfidence(candidate, "P1")}  {CandidateReviewPresenter.FormatBoundsCompact(bounds)}");
            return true;
        }

        public DrawingRectangleF BuildCandidateFocusRectForShell(DrawingRectangle bounds)
        {
            DrawingSize activeImageSize = activeImageSizeProvider();
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
