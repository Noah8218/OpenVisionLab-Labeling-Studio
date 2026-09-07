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
using System.IO;

namespace MvcVisionSystem
{
    // Responsibility group: candidate review confirmation and navigation commands.
    // These members remain WPF Window adapters; independent policy belongs in services.
    public partial class WpfLabelingShellWindow
    {
        #region CandidateReviewConfirmationCommands
        // Candidate confirmation mutates labels, history, and review state; keep it out of simple selection code.
        private void ExecuteConfirmSelectedCandidateCommand()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

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
            if (isApplicationCloseApproved)
            {
                return;
            }

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
            if (isApplicationCloseApproved)
            {
                return;
            }

            YoloWorkerSmokeCandidate candidate = GetSelectedCandidate();
            if (candidate == null)
            {
                AppendLog("스킵할 AI 후보를 선택하세요.");
                return;
            }

            YoloWorkerSmokeCandidate nextCandidate = FindNextVisibleCandidateAfter(candidate, new[] { candidate });
            RegisterAnnotationHistoryBeforeChange("Skip AI candidate", markDirty: false);
            bool resolvedSmartMaskCandidate = smartMaskPromptSession.IsSelectedCandidate(candidate);
            candidateReviewState.SkipCandidate(candidate);
            if (resolvedSmartMaskCandidate)
            {
                smartMaskPromptSession.MarkCandidateResolved();
            }
            if (!candidateReviewState.HasPendingCandidates)
            {
                ApplyCanvasDisplayMode(WpfCanvasDisplayMode.LabelsOnly, redraw: false, logChange: false);
            }
            if (resolvedSmartMaskCandidate && !candidateReviewState.HasPendingCandidates)
            {
                ContinueAutoSmartMaskAfterResolvedCandidate("스킵");
            }

            RefreshCandidateListWithPreferred(nextCandidate);
            RedrawReviewRois();
            FocusCandidateInViewer(nextCandidate, logIfMissing: false);
            MarkActiveImageSkippedOrCandidate();
            AddCandidateReviewHistory($"스킵: {CandidateReviewPresenter.FormatCandidate(
                candidate,
                CandidateReviewPresentationService.ClipCandidateBounds(candidate, activeImageSize))}");
            SetPythonStatus($"\uCD94\uB860: \uB300\uAE30 {pendingDetectionCandidates.Count} / \uD655\uC815 {confirmedDetectionCandidates.Count}");
            AppendLog($"후보 스킵: {CandidateReviewPresenter.FormatCandidate(
                candidate,
                CandidateReviewPresentationService.ClipCandidateBounds(candidate, activeImageSize))}");
            RefreshSmartMaskCommandState();
        }

        private void ExecuteCompleteImageAndNextCommand()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

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

            DrawingRectangle bounds = CandidateReviewPresentationService.ClipCandidateBounds(candidate, activeImageSize);
            string confidence = CandidateReviewPresenter.FormatConfidence(candidate, "P1");
            ApplyCandidateSelectionReview(candidate);
            SetModelStatus(candidate.ImageLevel
                ? $"후보: {candidate.ClassName} {confidence} 이미지 전체 판정"
                : bounds.IsEmpty
                ? $"후보: {candidate.ClassName} {confidence} 이미지 밖"
                : $"후보: {candidate.ClassName} {confidence}  {CandidateReviewPresenter.FormatBoundsCompact(bounds)}");
            UpdateDetectionResultOverlay();
            RedrawReviewRois();
        }

        private void ConfirmCandidates(IReadOnlyList<YoloWorkerSmokeCandidate> candidates, string scope)
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

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
            bool resolvesSmartMaskCandidate = plan.ConfirmableCandidates.Any(smartMaskPromptSession.IsSelectedCandidate);
            YoloWorkerSmokeCandidate selectedBeforeConfirm = GetSelectedCandidate();
            YoloWorkerSmokeCandidate nextCandidate = FindNextVisibleCandidateAfter(selectedBeforeConfirm, plan.ConfirmableCandidates);
            RegisterAnnotationHistoryBeforeChange($"Confirm {scope}");
            EnsureConfirmedCandidateClassItems(plan.ConfirmableCandidates);
            candidateConfirmationService.ApplyConfirmation(candidateReviewState, plan);
            if (resolvesSmartMaskCandidate)
            {
                smartMaskPromptSession.MarkCandidateResolved();
            }
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
                if (resolvesSmartMaskCandidate && !candidateReviewState.HasPendingCandidates)
                {
                    ContinueAutoSmartMaskAfterResolvedCandidate("확정");
                }
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
            RefreshSmartMaskCommandState();
        }

        private void EnsureConfirmedCandidateClassItems(IEnumerable<YoloWorkerSmokeCandidate> candidates)
        {
            foreach (string className in (candidates ?? Array.Empty<YoloWorkerSmokeCandidate>())
                .Select(CandidateReviewPresenter.GetClassName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                classCatalogWorkflowService.EnsureClassItem(global.Data, className);
            }
        }
        #endregion

        #region CandidateReviewNavigationCommands
        // Keyboard shortcuts and candidate navigation stay isolated from confirmation side effects.
        private void ExecuteCandidatePreviewKeyDownCommand(KeyInputCommandArgs e)
        {
            if (e == null)
            {
                return;
            }

            if (e.Key == Key.Enter)
            {
                ExecuteConfirmSelectedCandidateCommand();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Delete || e.Key == Key.Back)
            {
                ExecuteSkipSelectedCandidateCommand();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.A && (e.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                ExecuteConfirmAllCandidatesCommand();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.N)
            {
                ExecuteNextCandidateCommand();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.P)
            {
                ExecutePreviousCandidateCommand();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.F)
            {
                FocusSelectedCandidateInViewer(logIfMissing: true);
                e.Handled = true;
            }
        }

        private void ExecutePreviousCandidateCommand()
        {
            SelectCandidateOffset(-1);
        }

        private void ExecuteNextCandidateCommand()
        {
            SelectCandidateOffset(1);
        }

        private void ExecuteOpenModelComparisonExampleCommand(WpfModelComparisonReviewExample example)
        {
            if (example == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(example.ImagePath) || !File.Exists(example.ImagePath))
            {
                CandidateReviewViewModel?.AddReviewHistory($"\uBAA8\uB378 \uCC28\uC774 \uC608\uC2DC \uC774\uBBF8\uC9C0\uB97C \uCC3E\uC744 \uC218 \uC5C6\uC74C: {example.ImageKey}");
                AppendLog($"\uBAA8\uB378 \uCC28\uC774 \uC608\uC2DC \uC774\uBBF8\uC9C0\uB97C \uCC3E\uC744 \uC218 \uC5C6\uC74C: {example.ImageKey}");
                return;
            }

            bool loaded = TryLoadImage(
                example.ImagePath,
                populateQueue: true,
                refreshQueueDetails: true,
                refreshActiveStatus: true,
                appendLoadLog: false);
            if (!loaded)
            {
                return;
            }

            FocusModelComparisonExampleInViewer(example);
            AppendLog($"\uBAA8\uB378 \uCC28\uC774 \uC608\uC2DC \uC5F4\uB9BC: {example.Title}");
        }

        private void FocusModelComparisonExampleInViewer(WpfModelComparisonReviewExample example)
        {
            DrawingRectangle bounds = BuildModelComparisonExampleBounds(example);
            string boundsText = bounds.IsEmpty
                ? example?.LocationText ?? string.Empty
                : CandidateReviewPresenter.FormatBoundsCompact(bounds);
            CandidateReviewViewModel?.SetModelComparisonFocus(example, boundsText);
            ApplyCanvasDisplayMode(WpfCanvasDisplayMode.InferenceOnly, redraw: true, logChange: false);

            if (bounds.IsEmpty)
            {
                CanvasPanelViewModel?.SetDetectionOverlay(
                    "\uBAA8\uB378 \uCC28\uC774 \uC608\uC2DC",
                    Path.GetFileName(example.ImagePath),
                    example.Title,
                    example.ReviewText,
                    WpfDetectionOverlayStatus.Review);
                CandidateReviewViewModel?.AddReviewHistory($"\uBAA8\uB378 \uCC28\uC774 \uC608\uC2DC \uC5F4\uB9BC: {Path.GetFileName(example.ImagePath)} / \uC704\uCE58 \uD45C\uC2DC \uC5C6\uC74C");
                return;
            }

            // Model comparison examples are not live detection candidates; draw one selected overlay so the clicked difference is unmistakable.
            MainCanvasViewModel.SetDetectionOverlays(new[]
            {
                new RoiImageCanvasDetectionOverlay
                {
                    Index = -1,
                    Bounds = bounds,
                    Label = BuildModelComparisonExampleLabel(example),
                    IsSelected = true,
                    Color = System.Drawing.Color.FromArgb(245, 158, 11)
                }
            });
            MainCanvasViewModel.ImageViewer.FitToRect(BuildCandidateFocusRect(bounds));
            CanvasPanelViewModel?.SetDetectionOverlay(
                "\uBAA8\uB378 \uCC28\uC774 \uC608\uC2DC",
                Path.GetFileName(example.ImagePath),
                $"{example.Title} / {boundsText}",
                example.ReviewText,
                WpfDetectionOverlayStatus.Review);
            CandidateReviewViewModel?.AddReviewHistory($"\uBAA8\uB378 \uCC28\uC774 \uC608\uC2DC \uC5F4\uB9BC: {Path.GetFileName(example.ImagePath)} / {boundsText}");
            SetModelStatus($"\uBAA8\uB378 \uCC28\uC774 \uC704\uCE58: {example.Title}  {boundsText}");
        }

        private DrawingRectangle BuildModelComparisonExampleBounds(WpfModelComparisonReviewExample example)
        {
            if (example?.HasFocusBox != true || activeImageSize.IsEmpty)
            {
                return DrawingRectangle.Empty;
            }

            int left = (int)Math.Floor(example.Left * activeImageSize.Width);
            int top = (int)Math.Floor(example.Top * activeImageSize.Height);
            int right = (int)Math.Ceiling(example.Right * activeImageSize.Width);
            int bottom = (int)Math.Ceiling(example.Bottom * activeImageSize.Height);
            left = Math.Max(0, Math.Min(activeImageSize.Width - 1, left));
            top = Math.Max(0, Math.Min(activeImageSize.Height - 1, top));
            right = Math.Max(left + 1, Math.Min(activeImageSize.Width, right));
            bottom = Math.Max(top + 1, Math.Min(activeImageSize.Height, bottom));
            return new DrawingRectangle(left, top, right - left, bottom - top);
        }

        private static string BuildModelComparisonExampleLabel(WpfModelComparisonReviewExample example)
        {
            switch (example?.Kind)
            {
                case "CandidateOnly":
                    return "NEW";
                case "BaselineOnly":
                    return "BASE";
                case "ClassChanged":
                    return "CLASS";
                default:
                    return "DIFF";
            }
        }

        private void SelectCandidateOffset(int offset)
        {
            if (CandidateReviewViewModel == null)
            {
                return;
            }

            WpfCandidateNavigationSelection selection = CandidateReviewSelectionService.SelectCandidateOffset(
                CandidateReviewViewModel.Candidates,
                CandidateReviewViewModel.SelectedCandidate,
                offset);
            if (selection.Status == WpfCandidateNavigationStatus.NoCandidates)
            {
                AppendLog("이동할 AI 후보가 없습니다.");
                return;
            }

            if (selection.Status == WpfCandidateNavigationStatus.SingleCandidate)
            {
                AppendLog("이동할 다른 AI 후보가 없습니다.");
                return;
            }

            CandidateReviewViewModel.SelectedCandidate = selection.SelectedItem;
            CandidateListBox?.ScrollIntoView(selection.SelectedItem);
            CandidateListBox?.Focus();
            FocusSelectedCandidateInViewer(logIfMissing: false);
        }

        private YoloWorkerSmokeCandidate FindNextVisibleCandidateAfter(
            YoloWorkerSmokeCandidate current,
            IEnumerable<YoloWorkerSmokeCandidate> removingCandidates)
        {
            return CandidateReviewSelectionService.FindNextVisibleCandidateAfter(
                GetVisibleCandidateList(),
                current,
                removingCandidates);
        }
        #endregion

    }
}
