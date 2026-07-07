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
        // Candidate list state is split from overlay geometry so selection UX changes remain local.
        private void RefreshCandidateList()
        {
            RefreshCandidateListViewModel(null);
        }

        private void RefreshCandidateListWithPreferred(YoloWorkerSmokeCandidate preferredCandidate)
        {
            RefreshCandidateListViewModel(preferredCandidate);
        }

        private void RefreshCandidateListViewModel(YoloWorkerSmokeCandidate preferredCandidate)
        {
            WpfCandidateReviewListPresentation presentation = candidateReviewPresentationService.BuildListPresentation(
                pendingDetectionCandidates,
                GetVisibleCandidateList(),
                preferredCandidate,
                GetCandidateConfidenceFilter(),
                GetMinimumDetectionConfidence(),
                GetClippedCandidateBounds,
                GetCandidateOverlapInfo);

            CandidateReviewViewModel.SetCandidates(
                presentation.Rows,
                presentation.Detail,
                presentation.PreferredCandidate);
            YoloWorkerSmokeCandidate selected = GetSelectedCandidate();
            if (selected != null)
            {
                ApplyCandidateSelectionReview(selected);
            }
            else
            {
                ApplyCandidateSelectionReview(null);
            }

            UpdateCandidateActionState();
            UpdateDetectionResultOverlay();
        }

        private void UpdateCandidateActionState()
        {
            IReadOnlyList<YoloWorkerSmokeCandidate> visibleCandidates = GetVisibleCandidateList();
            bool hasVisibleCandidates = visibleCandidates.Count > 0 && !isDetecting;
            YoloWorkerSmokeCandidate selectedCandidate = GetSelectedCandidate();
            bool hasSelectedCandidate = selectedCandidate != null;
            bool selectedConfirmable = hasVisibleCandidates && hasSelectedCandidate && IsCandidateConfirmable(selectedCandidate);
            bool hasConfirmableCandidates = hasVisibleCandidates && visibleCandidates.Any(IsCandidateConfirmable);
            bool canNavigateCandidates = hasVisibleCandidates && hasSelectedCandidate && visibleCandidates.Count > 1;
            bool canFocusCandidate = hasVisibleCandidates && hasSelectedCandidate;
            WpfCandidateOverlapInfo selectedOverlap = hasSelectedCandidate
                ? GetCandidateOverlapInfo(selectedCandidate)
                : default;
            bool canFocusCurrentLabel = hasVisibleCandidates && hasSelectedCandidate && selectedOverlap.HasCurrentObject;
            bool hasImage = activeImageBitmap != null && !activeImageSize.IsEmpty && !isDetecting;
            CandidateReviewViewModel?.SetActionState(
                selectedConfirmable,
                hasConfirmableCandidates,
                hasVisibleCandidates && hasSelectedCandidate,
                selectedConfirmable ? "\uC120\uD0DD AI \uD6C4\uBCF4 \uD655\uC815" : BuildCandidateConfirmDisabledHintText(selectedCandidate),
                hasConfirmableCandidates ? "\uD45C\uC2DC\uB41C \uD655\uC815 \uAC00\uB2A5 \uD6C4\uBCF4 \uC804\uCCB4 \uD655\uC815" : "\uD655\uC815 \uAC00\uB2A5\uD55C \uD45C\uC2DC \uD6C4\uBCF4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4. \uC911\uBCF5 \uAC00\uB2A5 \uD6C4\uBCF4\uB294 \uC81C\uC678\uD569\uB2C8\uB2E4.",
                hasSelectedCandidate ? "\uC120\uD0DD AI \uD6C4\uBCF4 \uC2A4\uD0B5" : "\uC2A4\uD0B5\uD560 AI \uD6C4\uBCF4\uB97C \uC120\uD0DD\uD558\uC138\uC694.");
            CandidateReviewViewModel?.SetNavigationState(canNavigateCandidates, canNavigateCandidates, canFocusCandidate);
            CandidateReviewViewModel?.SetCurrentLabelFocusState(
                canFocusCurrentLabel,
                canFocusCurrentLabel
                    ? "\uACB9\uCE58\uB294 \uD604\uC7AC \uB77C\uBCA8\uC744 \uB77C\uBCA8 \uBAA9\uB85D\uC5D0\uC11C \uC120\uD0DD\uD569\uB2C8\uB2E4."
                    : "\uACB9\uCE58\uB294 \uD604\uC7AC \uB77C\uBCA8\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.");
            CandidateReviewViewModel?.SetCompletionState(candidateReviewCompletionPresentationService.Build(
                hasImage,
                isDetecting,
                pendingDetectionCandidates.Count,
                GetCanvasLabelObjectCount(),
                !string.IsNullOrWhiteSpace(annotationDirtyReason)));
            UpdateCanvasCommandButtons();
            UpdateWorkflowProgressStatus();
        }

        private void UpdateCanvasCommandButtons()
        {
            bool hasImage = activeImageBitmap != null && !activeImageSize.IsEmpty && !isDetecting;
            IReadOnlyList<YoloWorkerSmokeCandidate> visibleCandidates = GetVisibleCandidateList();
            bool hasVisibleCandidates = hasImage && visibleCandidates.Count > 0;
            YoloWorkerSmokeCandidate selectedCandidate = GetSelectedCandidate();
            bool hasSelectedCandidate = hasImage && selectedCandidate != null;
            bool hasPendingCandidates = hasImage && pendingDetectionCandidates.Count > 0 && !isDetecting;
            bool canNavigateCandidates = hasVisibleCandidates && hasSelectedCandidate && visibleCandidates.Count > 1;
            bool selectedConfirmable = hasVisibleCandidates && hasSelectedCandidate && IsCandidateConfirmable(selectedCandidate);
            WpfCandidateOverlapInfo selectedOverlap = hasSelectedCandidate
                ? GetCandidateOverlapInfo(selectedCandidate)
                : default;
            bool canFocusCurrentLabel = hasVisibleCandidates && hasSelectedCandidate && selectedOverlap.HasCurrentObject;

            if (CanvasPanelViewModel != null)
            {
                CanvasPanelViewModel.SetCommandAvailability(hasImage, hasSelectedCandidate, hasPendingCandidates);
                CanvasPanelViewModel.SetCandidateReviewState(
                    canNavigateCandidates,
                    canNavigateCandidates,
                    canFocusCurrentLabel,
                    selectedConfirmable,
                    hasPendingCandidates && hasSelectedCandidate);
                return;
            }

            SetControlEnabled(FitCanvasButton, hasImage);
            SetControlEnabled(ActualSizeCanvasButton, hasImage);
            SetControlEnabled(PanCanvasButton, hasImage);
            SetControlEnabled(FocusCandidateCanvasButton, hasSelectedCandidate);
            SetControlEnabled(ResetAiOverlayCanvasButton, hasPendingCandidates);
        }

        private string BuildCandidateConfirmDisabledHintText(YoloWorkerSmokeCandidate candidate)
        {
            DrawingRectangle bounds = GetClippedCandidateBounds(candidate);
            return WpfCandidateReviewPresenter.BuildConfirmDisabledHint(
                candidate,
                bounds,
                GetCandidateOverlapInfo(bounds));
        }

        private IReadOnlyList<YoloWorkerSmokeCandidate> GetVisibleCandidateList()
        {
            double minimum = GetCandidateConfidenceFilter();
            return candidateReviewState.GetVisibleCandidates(minimum);
        }

        private double GetCandidateConfidenceFilter()
        {
            return CandidateConfidenceSlider == null
                ? 0D
                : Math.Clamp(CandidateConfidenceSlider.Value, 0D, 1D);
        }

        private float GetMinimumDetectionConfidence()
        {
            return global.Data.ProjectSettings?.PythonModel?.MinimumDetectionConfidence ?? 0F;
        }

        private WpfCandidateOverlapInfo GetCandidateOverlapInfo(YoloWorkerSmokeCandidate candidate)
        {
            return GetCandidateOverlapInfo(GetClippedCandidateBounds(candidate));
        }

        private WpfCandidateOverlapInfo GetCandidateOverlapInfo(DrawingRectangle candidateBounds)
        {
            (string label, DrawingRectangle bounds, double iou, WpfObjectReviewItemRef currentObjectRef) = FindBestCurrentObjectOverlapInfo(candidateBounds);
            return new WpfCandidateOverlapInfo(label, bounds, iou, currentObjectRef);
        }

        private bool IsCandidateConfirmable(YoloWorkerSmokeCandidate candidate)
        {
            DrawingRectangle bounds = GetClippedCandidateBounds(candidate);
            return WpfCandidateReviewPresenter.IsConfirmable(
                candidate,
                bounds,
                GetCandidateOverlapInfo(bounds),
                GetMinimumDetectionConfidence());
        }

        private bool IsCandidateHighOverlap(YoloWorkerSmokeCandidate candidate)
        {
            DrawingRectangle bounds = GetClippedCandidateBounds(candidate);
            return WpfCandidateReviewPresenter.IsHighOverlap(GetCandidateOverlapInfo(bounds));
        }

        private void UpdateCandidateConfidenceText()
        {
            string text = GetCandidateConfidenceFilter().ToString("P0", CultureInfo.CurrentCulture);
            if (CandidateReviewViewModel != null)
            {
                CandidateReviewViewModel.ConfidenceText = text;
            }
        }

        private void ApplyCandidateSelectionReview(YoloWorkerSmokeCandidate candidate)
        {
            if (CandidateReviewViewModel != null)
            {
                if (candidate == null)
                {
                    CandidateReviewViewModel.ApplySelectionReview("선택된 AI 후보가 없습니다.", default, showComparison: false);
                    return;
                }

                DrawingRectangle bounds = GetClippedCandidateBounds(candidate);
                WpfCandidateComparisonPresentation comparison = WpfCandidateReviewPresenter.BuildComparison(
                    candidate,
                    bounds,
                    GetCandidateOverlapInfo(bounds));
                CandidateReviewViewModel.ApplySelectionReview(
                    FormatCandidateDetail(candidate),
                    comparison,
                    showComparison: true);
            }
        }
    }
}
