using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using OpenVisionLab.ImageCanvas.ViewModels;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.IO;
using DrawingRectangle = System.Drawing.Rectangle;

namespace MvcVisionSystem
{
    // Owns Candidate Review action commands and their workflow side effects.
    // The Shell remains the composition root and exposes thin compatibility facades.
    internal sealed class CandidateReviewActionAdapter
    {
        private readonly CandidateReviewActionAdapterContext context;

        private LabelingProjectData projectData => context.DataProvider?.Invoke();
        private ClassCatalogWorkflowService classCatalogWorkflowService => context.ClassCatalogWorkflowService;
        private CandidateReviewStateService candidateReviewState => context.CandidateReviewState;
        private CandidateConfirmationService candidateConfirmationService => context.CandidateConfirmationService;
        private SmartMaskPromptSessionService smartMaskPromptSession => context.SmartMaskPromptSession;
        private WpfCandidateReviewPanelViewModel CandidateReviewViewModel => context.CandidateReviewViewModel;
        private WpfCanvasPanelViewModel CanvasPanelViewModel => context.CanvasPanelViewModel;
        private RoiImageCanvasViewModel MainCanvasViewModel => context.MainCanvasViewModel;
        private IReadOnlyList<YoloWorkerSmokeCandidate> pendingDetectionCandidates => candidateReviewState?.PendingCandidates ?? Array.Empty<YoloWorkerSmokeCandidate>();
        private IReadOnlyList<YoloWorkerSmokeCandidate> confirmedDetectionCandidates => candidateReviewState?.ConfirmedCandidates ?? Array.Empty<YoloWorkerSmokeCandidate>();
        private Bitmap activeImageBitmap => context.ActiveImageBitmapProvider?.Invoke();
        private System.Drawing.Size activeImageSize => context.ActiveImageSizeProvider?.Invoke() ?? System.Drawing.Size.Empty;
        private bool isApplicationCloseApproved => context.IsApplicationCloseApproved?.Invoke() == true;

        internal CandidateReviewActionAdapter(CandidateReviewActionAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            ArgumentNullException.ThrowIfNull(context.DataProvider);
        }

        private void AppendLog(string message) => context.AppendLog?.Invoke(message);
        private void AddCandidateReviewHistory(string message) => context.AddCandidateReviewHistory?.Invoke(message);
        private void SetPythonStatus(string message) => context.SetPythonStatus?.Invoke(message);
        private void SetModelStatus(string message) => context.SetModelStatus?.Invoke(message);
        private void ShowCandidateReviewWorkflowView() => context.ShowCandidateReviewWorkflowView?.Invoke();
        private void ShowSavedLabelsWorkflowView() => context.ShowSavedLabelsWorkflowView?.Invoke();
        private void RedrawReviewRois() => context.RedrawReviewRois?.Invoke();
        private void RefreshCandidateListWithPreferred(YoloWorkerSmokeCandidate candidate) => context.RefreshCandidateListWithPreferred?.Invoke(candidate);
        private void RefreshObjectList() => context.RefreshObjectList?.Invoke();
        private void PopulateClassList() => context.PopulateClassList?.Invoke();
        private void SyncObjectClassEditorToSelection() => context.SyncObjectClassEditorToSelection?.Invoke();
        private void RefreshSmartMaskCommandState() => context.RefreshSmartMaskCommandState?.Invoke();
        private void MarkActiveImageConfirmed() => context.MarkActiveImageConfirmed?.Invoke();
        private void MarkActiveImageNoCandidate() => context.MarkActiveImageNoCandidate?.Invoke();
        private void MarkActiveImageSkippedOrCandidate() => context.MarkActiveImageSkippedOrCandidate?.Invoke();
        private void ContinueAutoSmartMaskAfterResolvedCandidate(string resolution) => context.ContinueAutoSmartMaskAfterResolvedCandidate?.Invoke(resolution);
        private void RefreshYoloTrainingStepCompletion() => context.RefreshYoloTrainingStepCompletion?.Invoke();
        private void RegisterAnnotationHistoryBeforeChange(string actionName, bool markDirty = true) => context.RegisterAnnotationHistoryBeforeChange?.Invoke(actionName, markDirty);
        private void ApplyCanvasDisplayMode(WpfCanvasDisplayMode mode, bool redraw, bool logChange) => context.ApplyCanvasDisplayMode?.Invoke(mode, redraw, logChange);
        private void UpdateCandidateActionState() => context.UpdateCandidateActionState?.Invoke();
        private void ApplyCandidateSelectionReview(YoloWorkerSmokeCandidate candidate) => context.ApplyCandidateSelectionReview?.Invoke(candidate);
        private void UpdateDetectionResultOverlay() => context.UpdateDetectionResultOverlay?.Invoke();
        private void FocusCandidateInViewer(YoloWorkerSmokeCandidate candidate, bool logIfMissing) => context.FocusCandidateInViewer?.Invoke(candidate, logIfMissing);
        private void FocusSelectedCandidateInViewer(bool logIfMissing) => context.FocusSelectedCandidateInViewer?.Invoke(logIfMissing);

        private bool HasCanvasLabelObjects() => context.HasCanvasLabelObjects?.Invoke() == true;

        private bool SaveCurrentAnnotations(out int savedCount)
        {
            AnnotationSaveOutcome outcome = context.SaveCurrentAnnotations?.Invoke() ?? AnnotationSaveOutcome.Failed;
            savedCount = outcome.SavedCount;
            return outcome.Succeeded;
        }

        private bool SaveCurrentEmptyAnnotations() => context.SaveCurrentEmptyAnnotations?.Invoke() == true;

        private bool TryOpenNextIncompleteQueueImage() => context.TryOpenNextIncompleteQueueImageWithoutPath?.Invoke() == true;

        private void FinishQueueCompletionAndGuideDatasetCheck() => context.FinishQueueCompletionAndGuideDatasetCheck?.Invoke();

        private string BuildLabelPathSummary() => context.BuildLabelPathSummary?.Invoke() ?? string.Empty;

        private YoloWorkerSmokeCandidate GetSelectedCandidate() => context.GetSelectedCandidate?.Invoke();

        private IReadOnlyList<YoloWorkerSmokeCandidate> GetVisibleCandidateList()
            => context.GetVisibleCandidateList?.Invoke() ?? Array.Empty<YoloWorkerSmokeCandidate>();

        private bool IsCandidateConfirmable(YoloWorkerSmokeCandidate candidate)
            => context.IsCandidateConfirmable?.Invoke(candidate) == true;

        private bool IsCandidateHighOverlap(YoloWorkerSmokeCandidate candidate)
            => context.IsCandidateHighOverlap?.Invoke(candidate) == true;

        private System.Drawing.RectangleF BuildCandidateFocusRect(DrawingRectangle bounds)
            => context.BuildCandidateFocusRect?.Invoke(bounds) ?? System.Drawing.RectangleF.Empty;

        private void FocusCandidateListItem(WpfCandidateReviewListItem item)
            => context.FocusCandidateListItem?.Invoke(item);

        private bool TryLoadModelComparisonImage(string imagePath)
            => context.TryLoadModelComparisonImage?.Invoke(imagePath) == true;

        private bool CanOpenModelComparisonImage(string imagePath)
            => context.CanOpenModelComparisonImage?.Invoke(imagePath) == true;

        #region CandidateReviewConfirmationCommands
        // Candidate confirmation mutates labels, history, and review state; keep it out of simple selection code.
        internal void ExecuteConfirmSelectedCandidateCommand()
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

        internal void ExecuteConfirmAllCandidatesCommand()
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

        internal void ExecuteSkipSelectedCandidateCommand()
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

        internal void ExecuteCompleteImageAndNextCommand()
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

        internal void ApplyCandidateSelectionChangedEffects(WpfCandidateReviewListItem selectedItem)
        {
            UpdateCandidateActionState();
            YoloWorkerSmokeCandidate candidate = CandidateReviewSelectionService.GetSelectedCandidate(selectedItem);
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

        internal void ConfirmCandidates(IReadOnlyList<YoloWorkerSmokeCandidate> candidates, string scope)
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

        internal void EnsureConfirmedCandidateClassItems(IEnumerable<YoloWorkerSmokeCandidate> candidates)
        {
            foreach (string className in (candidates ?? Array.Empty<YoloWorkerSmokeCandidate>())
                .Select(CandidateReviewPresenter.GetClassName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                classCatalogWorkflowService.EnsureClassItem(projectData, className);
            }
        }
        #endregion

        #region CandidateReviewNavigationCommands
        // Candidate navigation is kept beside confirmation because both change the review workflow state.

        internal void ExecutePreviousCandidateCommand()
        {
            SelectCandidateOffset(-1);
        }

        internal void ExecuteNextCandidateCommand()
        {
            SelectCandidateOffset(1);
        }

        internal void ExecuteOpenModelComparisonExampleCommand(WpfModelComparisonReviewExample example)
        {
            if (example == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(example.ImagePath) || !CanOpenModelComparisonImage(example.ImagePath))
            {
                CandidateReviewViewModel?.AddReviewHistory($"\uBAA8\uB378 \uCC28\uC774 \uC608\uC2DC \uC774\uBBF8\uC9C0\uB97C \uCC3E\uC744 \uC218 \uC5C6\uC74C: {example.ImageKey}");
                AppendLog($"\uBAA8\uB378 \uCC28\uC774 \uC608\uC2DC \uC774\uBBF8\uC9C0\uB97C \uCC3E\uC744 \uC218 \uC5C6\uC74C: {example.ImageKey}");
                return;
            }

            bool loaded = TryLoadModelComparisonImage(example.ImagePath);
            if (!loaded)
            {
                return;
            }

            FocusModelComparisonExampleInViewer(example);
            AppendLog($"\uBAA8\uB378 \uCC28\uC774 \uC608\uC2DC \uC5F4\uB9BC: {example.Title}");
        }

        internal void FocusModelComparisonExampleInViewer(WpfModelComparisonReviewExample example)
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

        internal DrawingRectangle BuildModelComparisonExampleBounds(WpfModelComparisonReviewExample example)
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

        internal static string BuildModelComparisonExampleLabel(WpfModelComparisonReviewExample example)
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

        internal void SelectCandidateOffset(int offset)
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
            FocusCandidateListItem(selection.SelectedItem);
            FocusSelectedCandidateInViewer(logIfMissing: false);
        }

        internal YoloWorkerSmokeCandidate FindNextVisibleCandidateAfter(
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

    internal sealed class CandidateReviewActionAdapterContext
    {
        internal Func<LabelingProjectData> DataProvider { get; init; }
        internal ClassCatalogWorkflowService ClassCatalogWorkflowService { get; init; }
        internal CandidateReviewStateService CandidateReviewState { get; init; }
        internal CandidateConfirmationService CandidateConfirmationService { get; init; }
        internal SmartMaskPromptSessionService SmartMaskPromptSession { get; init; }
        internal WpfCandidateReviewPanelViewModel CandidateReviewViewModel { get; init; }
        internal WpfCanvasPanelViewModel CanvasPanelViewModel { get; init; }
        internal RoiImageCanvasViewModel MainCanvasViewModel { get; init; }
        internal Func<bool> IsApplicationCloseApproved { get; init; }
        internal Func<Bitmap> ActiveImageBitmapProvider { get; init; }
        internal Func<Size> ActiveImageSizeProvider { get; init; }
        internal Func<YoloWorkerSmokeCandidate> GetSelectedCandidate { get; init; }
        internal Func<IReadOnlyList<YoloWorkerSmokeCandidate>> GetVisibleCandidateList { get; init; }
        internal Func<YoloWorkerSmokeCandidate, bool> IsCandidateConfirmable { get; init; }
        internal Func<YoloWorkerSmokeCandidate, bool> IsCandidateHighOverlap { get; init; }
        internal Action<string> AppendLog { get; init; }
        internal Action<string> AddCandidateReviewHistory { get; init; }
        internal Action<string> SetPythonStatus { get; init; }
        internal Action<string> SetModelStatus { get; init; }
        internal Action ShowCandidateReviewWorkflowView { get; init; }
        internal Action ShowSavedLabelsWorkflowView { get; init; }
        internal Action RedrawReviewRois { get; init; }
        internal Action<YoloWorkerSmokeCandidate> RefreshCandidateListWithPreferred { get; init; }
        internal Action RefreshObjectList { get; init; }
        internal Action PopulateClassList { get; init; }
        internal Action SyncObjectClassEditorToSelection { get; init; }
        internal Action RefreshSmartMaskCommandState { get; init; }
        internal Action MarkActiveImageConfirmed { get; init; }
        internal Action MarkActiveImageNoCandidate { get; init; }
        internal Action MarkActiveImageSkippedOrCandidate { get; init; }
        internal Action<string> ContinueAutoSmartMaskAfterResolvedCandidate { get; init; }
        internal Action RefreshYoloTrainingStepCompletion { get; init; }
        internal Action<string, bool> RegisterAnnotationHistoryBeforeChange { get; init; }
        internal Action<WpfCanvasDisplayMode, bool, bool> ApplyCanvasDisplayMode { get; init; }
        internal Action UpdateCandidateActionState { get; init; }
        internal Action<YoloWorkerSmokeCandidate> ApplyCandidateSelectionReview { get; init; }
        internal Action UpdateDetectionResultOverlay { get; init; }
        internal Action<YoloWorkerSmokeCandidate, bool> FocusCandidateInViewer { get; init; }
        internal Action<bool> FocusSelectedCandidateInViewer { get; init; }
        internal Func<bool> HasCanvasLabelObjects { get; init; }
        internal Func<AnnotationSaveOutcome> SaveCurrentAnnotations { get; init; }
        internal Func<bool> SaveCurrentEmptyAnnotations { get; init; }
        internal Func<bool> TryOpenNextIncompleteQueueImageWithoutPath { get; init; }
        internal Action FinishQueueCompletionAndGuideDatasetCheck { get; init; }
        internal Func<string> BuildLabelPathSummary { get; init; }
        internal Func<DrawingRectangle, RectangleF> BuildCandidateFocusRect { get; init; }
        internal Action<WpfCandidateReviewListItem> FocusCandidateListItem { get; init; }
        internal Func<string, bool> CanOpenModelComparisonImage { get; init; }
        internal Func<string, bool> TryLoadModelComparisonImage { get; init; }
    }
}
