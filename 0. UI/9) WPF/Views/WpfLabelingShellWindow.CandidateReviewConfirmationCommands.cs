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
        // Candidate confirmation mutates labels, history, and review state; keep it out of simple selection code.
        private void ExecuteConfirmSelectedCandidateCommand()
        {
            YoloWorkerSmokeCandidate candidate = GetSelectedCandidate();
            if (candidate == null)
            {
                AppendLog("먼저 AI 후보를 선택하세요.");
                return;
            }

            ConfirmCandidates(new[] { candidate }, "선택");
        }

        private void ExecuteConfirmAllCandidatesCommand()
        {
            IReadOnlyList<YoloWorkerSmokeCandidate> candidates = GetVisibleCandidateList();
            if (candidates.Count == 0)
            {
                AppendLog("확정할 표시 AI 후보가 없습니다.");
                return;
            }

            ConfirmCandidates(candidates, "표시 후보 전체");
        }

        private void ExecuteSkipSelectedCandidateCommand()
        {
            YoloWorkerSmokeCandidate candidate = GetSelectedCandidate();
            if (candidate == null)
            {
                AppendLog("스킵할 AI 후보를 선택하세요.");
                return;
            }

            YoloWorkerSmokeCandidate nextCandidate = FindNextVisibleCandidateAfter(candidate, new[] { candidate });
            RegisterAnnotationHistoryBeforeChange("Skip AI candidate", markDirty: false);
            candidateReviewState.SkipCandidate(candidate);
            if (!candidateReviewState.HasPendingCandidates)
            {
                ApplyCanvasDisplayMode(WpfCanvasDisplayMode.LabelsOnly, redraw: false, logChange: false);
            }

            RefreshCandidateListWithPreferred(nextCandidate);
            RedrawReviewRois();
            FocusCandidateInViewer(nextCandidate, logIfMissing: false);
            MarkActiveImageSkippedOrCandidate();
            AddCandidateReviewHistory($"스킵: {FormatCandidate(candidate)}");
            SetPythonStatus($"\uCD94\uB860: \uB300\uAE30 {pendingDetectionCandidates.Count} / \uD655\uC815 {confirmedDetectionCandidates.Count}");
            AppendLog($"후보 스킵: {FormatCandidate(candidate)}");
        }

        private void ExecuteCompleteImageAndNextCommand()
        {
            if (activeImageBitmap == null || activeImageSize.IsEmpty)
            {
                AppendLog("\uC644\uB8CC\uD560 \uC774\uBBF8\uC9C0\uB97C \uBA3C\uC800 \uC5F4\uC5B4\uC8FC\uC138\uC694.");
                return;
            }

            if (candidateReviewState.HasPendingCandidates)
            {
                ShowCandidateReviewWorkflowView();
                FocusSelectedCandidateInViewer(logIfMissing: false);
                AddCandidateReviewHistory($"\uC644\uB8CC \uBCF4\uB958: \uB0A8\uC740 \uD6C4\uBCF4 {candidateReviewState.PendingCount}\uAC1C");
                AppendLog($"\uB0A8\uC740 AI \uD6C4\uBCF4 {candidateReviewState.PendingCount}\uAC1C\uB97C \uBA3C\uC800 \uD655\uC815\uD558\uAC70\uB098 \uC2A4\uD0B5\uD558\uC138\uC694.");
                return;
            }

            bool hasLabels = HasCanvasLabelObjects();
            bool saved = hasLabels
                ? SaveCurrentAnnotations(out _)
                : SaveCurrentEmptyAnnotations();
            if (!saved)
            {
                AppendLog("\uD604\uC7AC \uC774\uBBF8\uC9C0\uB97C \uC644\uB8CC\uD558\uC9C0 \uBABB\uD588\uC2B5\uB2C8\uB2E4. \uC774\uBBF8\uC9C0\uC640 \uC800\uC7A5 \uACBD\uB85C\uB97C \uD655\uC778\uD558\uC138\uC694.");
                return;
            }

            if (hasLabels)
            {
                MarkActiveImageConfirmed();
                AddCandidateReviewHistory("\uC774\uBBF8\uC9C0 \uC644\uB8CC: \uB77C\uBCA8 \uC800\uC7A5");
            }
            else
            {
                MarkActiveImageNoCandidate();
                AddCandidateReviewHistory("\uC774\uBBF8\uC9C0 \uC644\uB8CC: \uAC1D\uCCB4 \uC5C6\uC74C");
            }

            RefreshYoloTrainingStepCompletion();
            if (!TryOpenNextIncompleteQueueImage())
            {
                FinishQueueCompletionAndGuideDatasetCheck();
            }
        }

        private void ExecuteCandidateSelectionChangedCommand(object selectedItem)
        {
            UpdateCandidateActionState();
            YoloWorkerSmokeCandidate candidate = GetSelectedCandidate();
            if (candidate == null)
            {
                ApplyCandidateSelectionReview(null);
                UpdateDetectionResultOverlay();
                RedrawReviewRois();
                return;
            }

            DrawingRectangle bounds = GetClippedCandidateBounds(candidate);
            string confidence = WpfCandidateReviewPresenter.FormatConfidence(candidate, "P1");
            ApplyCandidateSelectionReview(candidate);
            SetModelStatus(bounds.IsEmpty
                ? $"후보: {candidate.ClassName} {confidence} 이미지 밖"
                : $"후보: {candidate.ClassName} {confidence}  {WpfCandidateReviewPresenter.FormatBoundsCompact(bounds)}");
            UpdateDetectionResultOverlay();
            RedrawReviewRois();
        }

        private void ConfirmCandidates(IReadOnlyList<YoloWorkerSmokeCandidate> candidates, string scope)
        {
            if (activeImageBitmap == null || activeImageSize.IsEmpty)
            {
                AppendLog("후보를 확정하려면 이미지를 먼저 불러오세요.");
                return;
            }

            WpfCandidateConfirmationAttempt attempt = candidateConfirmationService.Prepare(
                candidateReviewState,
                candidates,
                IsCandidateConfirmable,
                IsCandidateHighOverlap);
            if (!attempt.CanConfirm)
            {
                AddCandidateReviewHistory(attempt.ReviewHistoryMessage);
                AppendLog(attempt.LogMessage);
                return;
            }

            WpfCandidateConfirmationPlan plan = attempt.Plan;
            YoloWorkerSmokeCandidate selectedBeforeConfirm = GetSelectedCandidate();
            YoloWorkerSmokeCandidate nextCandidate = FindNextVisibleCandidateAfter(selectedBeforeConfirm, plan.ConfirmableCandidates);
            RegisterAnnotationHistoryBeforeChange($"Confirm {scope}");
            EnsureConfirmedCandidateClassItems(plan.ConfirmableCandidates);
            candidateConfirmationService.ApplyConfirmation(candidateReviewState, plan);
            if (!candidateReviewState.HasPendingCandidates)
            {
                ApplyCanvasDisplayMode(WpfCanvasDisplayMode.LabelsOnly, redraw: false, logChange: false);
            }

            bool saved = SaveCurrentAnnotations(out int savedCount);
            WpfCandidateConfirmationResult result = candidateConfirmationService.BuildConfirmedResult(
                scope,
                plan,
                saved,
                savedCount,
                BuildLabelPathSummary());
            AddCandidateReviewHistory(result.ReviewHistoryMessage);
            RefreshCandidateListWithPreferred(nextCandidate);
            RefreshObjectList();
            RedrawReviewRois();
            PopulateClassList();
            SyncObjectClassEditorToSelection();
            if (saved)
            {
                MarkActiveImageConfirmed();
            }
            if (candidateReviewState.HasPendingCandidates)
            {
                ShowCandidateReviewWorkflowView();
                FocusCandidateInViewer(nextCandidate, logIfMissing: false);
            }
            else
            {
                ShowSavedLabelsWorkflowView();
            }
            SetPythonStatus($"\uCD94\uB860: \uB300\uAE30 {candidateReviewState.PendingCount} / \uD655\uC815 {candidateReviewState.ConfirmedCount}");

            AppendLog(result.LogMessage);
            if (!string.IsNullOrWhiteSpace(result.DuplicateLogMessage))
            {
                AppendLog(result.DuplicateLogMessage);
            }
        }

        private void EnsureConfirmedCandidateClassItems(IEnumerable<YoloWorkerSmokeCandidate> candidates)
        {
            foreach (string className in (candidates ?? Array.Empty<YoloWorkerSmokeCandidate>())
                .Select(WpfCandidateReviewPresenter.GetClassName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                EnsureClassItem(className);
            }
        }
    }
}
