// Candidate Review selection, overlay, and completion state is owned here.
// The Shell supplies state providers and UI callbacks; it no longer spreads
// this workflow across a Window partial.
using MvcVisionSystem._1._Core;
using MvcVisionSystem._3._Communication.TCP;
using OpenVisionLab;
using OpenVisionLab.ImageCanvas.ViewModels;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Controls;
using DrawingRectangle = System.Drawing.Rectangle;

namespace MvcVisionSystem
{
    internal sealed class CandidateReviewStateAdapter
    {
        private const string AllCandidateClassesFilter = "모든 클래스";
        private readonly CandidateReviewStateAdapterContext context;
        private string selectedCandidateClassFilter = AllCandidateClassesFilter;

        internal CandidateReviewStateAdapter(CandidateReviewStateAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            ArgumentNullException.ThrowIfNull(context.DataProvider);
        }

        #region ContextAliases
        private LabelingProjectData projectData => context.DataProvider?.Invoke();
        private WpfCanvasPanelViewModel CanvasPanelViewModel => context.CanvasPanelViewModel;
        private WpfCandidateReviewPanelViewModel CandidateReviewViewModel => context.CandidateReviewViewModel;
        private CandidateReviewStateService candidateReviewState => context.CandidateReviewState;
        private CandidateReviewPresentationService candidateReviewPresentationService => context.CandidateReviewPresentationService;
        private CandidateReviewCompletionPresentationService candidateReviewCompletionPresentationService => context.CandidateReviewCompletionPresentationService;
        private ImageDetectionWorkflowService imageDetectionWorkflowService => context.ImageDetectionWorkflowService;
        private PatchCoreHeatmapReviewService patchCoreHeatmapReviewService => context.PatchCoreHeatmapReviewService;
        private PatchCoreHeatmapWindowHost patchCoreHeatmapWindowHost => context.PatchCoreHeatmapWindowHost;
        private AnnotationDirtyState annotationDirtyState => context.AnnotationDirtyState;
        private IReadOnlyList<YoloWorkerSmokeCandidate> pendingDetectionCandidates => candidateReviewState.PendingCandidates;
        private IReadOnlyList<YoloWorkerSmokeCandidate> confirmedDetectionCandidates => candidateReviewState.ConfirmedCandidates;
        private IList<Rectangle> manualRois => context.ManualRois;
        private IReadOnlyList<string> manualRoiOverlayIds => context.ManualRoiOverlayIds;
        private string activeImagePath => context.ActiveImagePathProvider?.Invoke() ?? string.Empty;
        private Size activeImageSize => context.ActiveImageSizeProvider?.Invoke() ?? Size.Empty;
        private Bitmap activeImageBitmap => context.ActiveImageBitmapProvider?.Invoke();
        private bool isApplicationCloseApproved => context.IsApplicationCloseApproved?.Invoke() == true;
        private bool isCandidateMutationBlocked => isApplicationCloseApproved || imageDetectionWorkflowService.IsDetecting || context.IsCandidateMutationBlocked?.Invoke() == true;
        private bool isInferenceInspectionOnly => context.IsInferenceInspectionOnly?.Invoke() == true;
        private bool Topmost => context.IsTopmostProvider?.Invoke() == true;
        private Slider CandidateConfidenceSlider => context.CandidateConfidenceSlider;
        private ListBox CandidateListBox => context.CandidateListBox;
        private Control FitCanvasButton => context.FitCanvasButton;
        private Control ActualSizeCanvasButton => context.ActualSizeCanvasButton;
        private Control PanCanvasButton => context.PanCanvasButton;
        private Control FocusCandidateCanvasButton => context.FocusCandidateCanvasButton;
        private Control ResetAiOverlayCanvasButton => context.ResetAiOverlayCanvasButton;
        private bool IsModelWorkflowCreated => context.IsModelWorkflowCreated?.Invoke() == true;

        private string GetManualRoiClassName(int index)
            => context.GetManualRoiClassName?.Invoke(index) ?? string.Empty;

        private int GetCanvasLabelObjectCount()
            => context.GetCanvasLabelObjectCount?.Invoke() ?? 0;

        private void ShowCandidateReviewWorkflowView()
            => context.ShowCandidateReviewWorkflowView?.Invoke();

        private void RedrawReviewRois()
            => context.RedrawReviewRois?.Invoke();

        private void SetModelStatus(string text)
            => context.SetModelStatus?.Invoke(text);

        private void UpdateWorkflowProgressStatus()
            => context.UpdateWorkflowProgressStatus?.Invoke();

        private static void SetControlEnabled(Control control, bool isEnabled)
        {
            if (control != null)
            {
                control.IsEnabled = isEnabled;
            }
        }
        #endregion

        #region CandidateReview
        // AI candidate review is grouped here so selection, confirmation, and overlay state stay traceable as one workflow.
        internal YoloWorkerSmokeCandidate GetSelectedCandidate()
            => CandidateReviewSelectionService.GetSelectedCandidate(
                CandidateReviewViewModel?.SelectedCandidate);

        internal void UpdateDetectionResultOverlay()
        {
            if (CanvasPanelViewModel == null)
            {
                return;
            }

            if (CanvasPanelViewModel?.IsInferenceLayerVisible != true)
            {
                CanvasPanelViewModel.ClearDetectionOverlay();
                return;
            }

            WpfDetectionOverlayPresentation presentation = candidateReviewPresentationService.BuildOverlayPresentation(
                activeImagePath,
                pendingDetectionCandidates,
                GetSelectedCandidate(),
                GetCandidateConfidenceFilter(),
                IsCandidateHighOverlap,
                IsCandidateConfirmable,
                candidate => CandidateReviewPresenter.BuildSecondaryText(
                    candidate,
                    CandidateReviewPresentationService.ClipCandidateBounds(candidate, activeImageSize),
                    GetCandidateOverlapInfo(candidate),
                    GetMinimumDetectionConfidence(),
                    context.IsInferenceInspectionOnly?.Invoke() == true),
                context.IsInferenceInspectionOnly?.Invoke() == true);
            if (presentation.IsEmpty)
            {
                CanvasPanelViewModel.ClearDetectionOverlay();
                return;
            }

            CanvasPanelViewModel.SetDetectionOverlay(
                presentation.Title,
                presentation.Summary,
                presentation.SelectedText,
                presentation.Detail,
                presentation.Status);
        }

        internal void MainCanvasViewModel_DetectionOverlayClicked(object sender, int candidateIndex)
        {
            if (isApplicationCloseApproved
                || candidateIndex < 0
                || candidateIndex >= pendingDetectionCandidates.Count)
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
            SetModelStatus($"AI 후보 선택: {CandidateReviewPresenter.FormatCandidate(
                candidate,
                    CandidateReviewPresentationService.ClipCandidateBounds(candidate, activeImageSize))}");
        }

        internal void ExecuteCandidateConfidenceChangedCommand(double confidence)
        {
            UpdateCandidateConfidenceText();
            if (CandidateListBox == null)
            {
                return;
            }

            RefreshCandidateList();
        }

        internal void ExecuteCandidateClassFilterChangedCommand(string classFilter)
        {
            selectedCandidateClassFilter = string.IsNullOrWhiteSpace(classFilter)
                ? AllCandidateClassesFilter
                : classFilter.Trim();
            RefreshCandidateList();
        }
        #endregion

        #region CandidateReviewListState
        // Candidate list state is split from overlay geometry so selection UX changes remain local.
        internal void RefreshCandidateList()
        {
            if (!IsModelWorkflowCreated)
            {
                return;
            }

            RefreshCandidateListViewModel(null);
        }

        internal void RefreshCandidateListWithPreferred(YoloWorkerSmokeCandidate preferredCandidate)
        {
            if (!IsModelWorkflowCreated)
            {
                return;
            }

            RefreshCandidateListViewModel(preferredCandidate);
        }

        internal void RefreshCandidateListViewModel(YoloWorkerSmokeCandidate preferredCandidate)
        {
            CandidateReviewViewModel.SetCandidateClassFilters(
                pendingDetectionCandidates.Select(CandidateReviewPresenter.GetClassName),
                selectedCandidateClassFilter);
            selectedCandidateClassFilter = CandidateReviewViewModel.SelectedCandidateClassFilter;
            WpfCandidateReviewListPresentation presentation = candidateReviewPresentationService.BuildListPresentation(
                pendingDetectionCandidates,
                GetVisibleCandidateList(),
                preferredCandidate,
                GetCandidateConfidenceFilter(),
                GetMinimumDetectionConfidence(),
                candidate => CandidateReviewPresentationService.ClipCandidateBounds(candidate, activeImageSize),
                GetCandidateOverlapInfo,
                context.IsInferenceInspectionOnly?.Invoke() == true);

            IReadOnlyList<WpfClassCatalogListItem> classes = context.ClassCatalogProvider?.Invoke() ?? Array.Empty<WpfClassCatalogListItem>();
            foreach (WpfCandidateReviewListItem row in presentation.Rows)
            {
                if (row.Payload is not YoloWorkerSmokeCandidate candidate) continue;
                WpfClassCatalogListItem classItem = candidate.ClassId.HasValue ? classes.FirstOrDefault(item => item.CanonicalIndex == candidate.ClassId.Value) : null;
                classItem ??= classes.FirstOrDefault(item => string.Equals(item.Text, candidate.ClassName, StringComparison.OrdinalIgnoreCase));
                row.SetInlinePresentation(classItem?.DrawBrush, context.IsSmartMaskCandidate?.Invoke(candidate) == true);
            }
            CandidateReviewViewModel.SetCandidates(
                presentation.Rows,
                presentation.Detail,
                presentation.PreferredCandidate,
                pendingDetectionCandidates.Count);
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

        internal void UpdateCandidateActionState()
        {
            if (!IsModelWorkflowCreated)
            {
                return;
            }

            UpdateCandidateConfidenceText();
            context.RefreshInferenceResultPresentation?.Invoke();
            IReadOnlyList<YoloWorkerSmokeCandidate> visibleCandidates = GetVisibleCandidateList();
            bool hasVisibleCandidates = visibleCandidates.Count > 0 && !isCandidateMutationBlocked;
            YoloWorkerSmokeCandidate selectedCandidate = GetSelectedCandidate();
            bool hasSelectedCandidate = selectedCandidate != null;
            bool canConfirmCandidates = !isInferenceInspectionOnly;
            bool selectedConfirmable = canConfirmCandidates && hasVisibleCandidates && hasSelectedCandidate && IsCandidateConfirmable(selectedCandidate);
            bool hasConfirmableCandidates = canConfirmCandidates && hasVisibleCandidates && visibleCandidates.Any(IsCandidateConfirmable);
            if (CandidateReviewViewModel != null)
            {
                foreach (WpfCandidateReviewListItem row in CandidateReviewViewModel.Candidates)
                {
                    bool isPending = row.Payload is YoloWorkerSmokeCandidate candidate && pendingDetectionCandidates.Contains(candidate);
                    row.SetInlineAvailability(isPending && canConfirmCandidates && hasVisibleCandidates && IsCandidateConfirmable((YoloWorkerSmokeCandidate)row.Payload),
                        isPending && canConfirmCandidates && hasVisibleCandidates);
                }
                CandidateReviewViewModel.RefreshInlineCommands();
            }
            bool canNavigateCandidates = hasVisibleCandidates && hasSelectedCandidate && visibleCandidates.Count > 1;
            bool canFocusCandidate = hasVisibleCandidates && hasSelectedCandidate;
            WpfCandidateOverlapInfo selectedOverlap = hasSelectedCandidate
                ? GetCandidateOverlapInfo(selectedCandidate)
                : default;
            bool canFocusCurrentLabel = hasVisibleCandidates && hasSelectedCandidate && selectedOverlap.HasCurrentObject;
            bool hasImage = activeImageBitmap != null && !activeImageSize.IsEmpty && !isCandidateMutationBlocked;
            CandidateReviewViewModel?.SetActionState(
                selectedConfirmable,
                hasConfirmableCandidates,
                hasVisibleCandidates && hasSelectedCandidate,
                isInferenceInspectionOnly
                    ? OpenVisionLanguageService.T("WpfInference.ResultOnly.ConfirmBlocked")
                    : selectedConfirmable ? OpenVisionLanguageService.T("WpfCandidate.Draft.ConfirmSelectedHint") : BuildCandidateConfirmDisabledHintText(selectedCandidate),
                isInferenceInspectionOnly
                    ? OpenVisionLanguageService.T("WpfInference.ResultOnly.ConfirmBlocked")
                    : hasConfirmableCandidates ? OpenVisionLanguageService.T("WpfCandidate.Draft.ConfirmAllHint") : "\uD655\uC815 \uAC00\uB2A5 \uD6C4\uBCF4\uAC00 \uC5C6\uC2B5\uB2C8\uB2E4. \uC911\uBCF5 \uAC00\uB2A5 \uD6C4\uBCF4\uB294 \uC81C\uC678\uD569\uB2C8\uB2E4.",
                hasSelectedCandidate ? "\uC120\uD0DD AI \uD6C4\uBCF4 \uAC74\uB108\uB6F0\uAE30" : "\uAC74\uB108\uB6F8 AI \uD6C4\uBCF4\uB97C \uC120\uD0DD\uD558\uC138\uC694.");
            CandidateReviewViewModel?.SetNavigationState(canNavigateCandidates, canNavigateCandidates, canFocusCandidate);
            CandidateReviewViewModel?.SetCurrentLabelFocusState(
                canFocusCurrentLabel,
                canFocusCurrentLabel
                    ? "\uACB9\uCE58\uB294 \uD604\uC7AC \uB77C\uBCA8\uC744 \uB77C\uBCA8 \uBAA9\uB85D\uC5D0\uC11C \uC120\uD0DD\uD569\uB2C8\uB2E4."
                    : "\uACB9\uCE58\uB294 \uD604\uC7AC \uB77C\uBCA8\uC774 \uC5C6\uC2B5\uB2C8\uB2E4.");
            CandidateReviewViewModel?.UpdateCandidateAvailabilityPresentation(visibleCandidates.Count, pendingDetectionCandidates.Count, isInferenceInspectionOnly, context.HasDisplayedInferenceResult?.Invoke() == true);
            if (isInferenceInspectionOnly)
            {
                CandidateReviewViewModel?.SetCompletionState(
                    false,
                    OpenVisionLanguageService.T("WpfInference.ResultOnly.CompletionBlocked"));
            }
            else
            {
                CandidateReviewViewModel?.SetCompletionState(candidateReviewCompletionPresentationService.Build(
                    hasImage,
                    isCandidateMutationBlocked,
                    pendingDetectionCandidates.Count,
                    GetCanvasLabelObjectCount(),
                    annotationDirtyState.IsDirty));
            }
            UpdateCanvasCommandButtons();
            UpdateWorkflowProgressStatus();
        }

        internal void UpdateCanvasCommandButtons()
        {
            bool hasImage = activeImageBitmap != null && !activeImageSize.IsEmpty && !isCandidateMutationBlocked;
            IReadOnlyList<YoloWorkerSmokeCandidate> visibleCandidates = GetVisibleCandidateList();
            bool hasVisibleCandidates = hasImage && visibleCandidates.Count > 0;
            YoloWorkerSmokeCandidate selectedCandidate = GetSelectedCandidate();
            bool hasSelectedCandidate = hasImage && selectedCandidate != null;
            bool hasPendingCandidates = hasImage && pendingDetectionCandidates.Count > 0 && !isCandidateMutationBlocked;
            bool canNavigateCandidates = hasVisibleCandidates && hasSelectedCandidate && visibleCandidates.Count > 1;
            bool selectedConfirmable = !isInferenceInspectionOnly && hasVisibleCandidates && hasSelectedCandidate && IsCandidateConfirmable(selectedCandidate);
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

        internal string BuildCandidateConfirmDisabledHintText(YoloWorkerSmokeCandidate candidate)
        {
            DrawingRectangle bounds = CandidateReviewPresentationService.ClipCandidateBounds(candidate, activeImageSize);
            return CandidateReviewPresenter.BuildConfirmDisabledHint(
                candidate,
                bounds,
                GetCandidateOverlapInfo(bounds));
        }

        internal IReadOnlyList<YoloWorkerSmokeCandidate> GetVisibleCandidateList()
        {
            double minimum = GetCandidateConfidenceFilter();
            if (context.IsInferenceWorkspaceStage?.Invoke() == true)
                return pendingDetectionCandidates.Where(candidate => candidate != null && (candidate.ImageLevel || candidate.Confidence >= minimum)).ToArray();
            IReadOnlyList<YoloWorkerSmokeCandidate> candidates = candidateReviewState.GetVisibleCandidates(minimum);
            if (string.Equals(selectedCandidateClassFilter, AllCandidateClassesFilter, StringComparison.OrdinalIgnoreCase))
            {
                return candidates;
            }

            return candidates
                .Where(candidate => string.Equals(
                    CandidateReviewPresenter.GetClassName(candidate),
                    selectedCandidateClassFilter,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        internal double GetCandidateConfidenceFilter()
        {
            return CandidateReviewViewModel != null ? CandidateReviewViewModel.DisplayConfidence : CandidateConfidenceSlider == null
                ? 0D
                : Math.Clamp(CandidateConfidenceSlider.Value, 0D, 1D);
        }

        internal float GetMinimumDetectionConfidence()
        {
            return projectData?.ProjectSettings?.PythonModel?.MinimumDetectionConfidence ?? 0F;
        }

        internal WpfCandidateOverlapInfo GetCandidateOverlapInfo(YoloWorkerSmokeCandidate candidate)
        {
            return GetCandidateOverlapInfo(
                CandidateReviewPresentationService.ClipCandidateBounds(candidate, activeImageSize));
        }

        internal WpfCandidateOverlapInfo GetCandidateOverlapInfo(DrawingRectangle candidateBounds)
        {
            var sources = new List<WpfCandidateOverlapSource>(manualRois.Count + confirmedDetectionCandidates.Count);
            for (int index = 0; index < manualRois.Count; index++)
            {
                sources.Add(new WpfCandidateOverlapSource(
                    string.Format(CultureInfo.CurrentCulture, OpenVisionLanguageService.T("WpfCandidate.Overlap.ManualLabel"), GetManualRoiClassName(index)),
                    manualRois[index],
                    WpfObjectReviewItemRef.Manual(
                        index,
                        ObjectReviewSelectionService.GetManualRoiOverlayId(manualRoiOverlayIds, index))));
            }

            for (int index = 0; index < confirmedDetectionCandidates.Count; index++)
            {
                YoloWorkerSmokeCandidate confirmed = confirmedDetectionCandidates[index];
                sources.Add(new WpfCandidateOverlapSource(
                    $"AI {CandidateReviewPresenter.GetClassName(confirmed)}",
                    CandidateReviewPresentationService.ClipCandidateBounds(confirmed, activeImageSize),
                    WpfObjectReviewItemRef.ConfirmedAi(index)));
            }

            return CandidateReviewPresentationService.FindBestOverlap(candidateBounds, sources);
        }

        internal bool IsCandidateConfirmable(YoloWorkerSmokeCandidate candidate)
        {
            DrawingRectangle bounds = CandidateReviewPresentationService.ClipCandidateBounds(candidate, activeImageSize);
            return CandidateReviewPresenter.IsConfirmable(
                candidate,
                bounds,
                GetCandidateOverlapInfo(bounds),
                GetMinimumDetectionConfidence());
        }

        internal bool IsCandidateHighOverlap(YoloWorkerSmokeCandidate candidate)
        {
            DrawingRectangle bounds = CandidateReviewPresentationService.ClipCandidateBounds(candidate, activeImageSize);
            return CandidateReviewPresenter.IsHighOverlap(GetCandidateOverlapInfo(bounds));
        }

        internal void UpdateCandidateConfidenceText()
        {
            string text = GetCandidateConfidenceFilter().ToString("P0", CultureInfo.CurrentCulture);
            if (CandidateReviewViewModel != null)
            {
                CandidateReviewViewModel.ConfidenceText = text;
            }
        }

        internal void ApplyCandidateSelectionReview(YoloWorkerSmokeCandidate candidate)
        {
            if (CandidateReviewViewModel != null)
            {
                bool preserveOpenEvidence = patchCoreHeatmapWindowHost.IsOpenFor(candidate);
                if (!preserveOpenEvidence)
                {
                    ClosePatchCoreHeatmapWindow();
                    CandidateReviewViewModel.SetPatchCoreHeatmapAvailability(
                        patchCoreHeatmapReviewService.Inspect(candidate));
                }
                if (candidate == null)
                {
                    CandidateReviewViewModel.ApplySelectionReview("선택된 AI 후보가 없습니다.", default, showComparison: false);
                    return;
                }

                DrawingRectangle bounds = CandidateReviewPresentationService.ClipCandidateBounds(candidate, activeImageSize);
                if (isInferenceInspectionOnly)
                {
                    CandidateReviewViewModel.ApplySelectionReview(CandidateReviewPresenter.GetClassName(candidate) + " / " + candidate.Confidence.ToString("P0") + " / " + CandidateReviewPresenter.FormatBoundsCompact(bounds) + Environment.NewLine + OpenVisionLanguageService.T("WpfInference.ResultOnly.Panel.Detail"), default, showComparison: false);
                    return;
                }
                WpfCandidateComparisonPresentation comparison = CandidateReviewPresenter.BuildComparison(
                    candidate,
                    bounds,
                    GetCandidateOverlapInfo(bounds));
                CandidateReviewViewModel.ApplySelectionReview(
                    CandidateReviewPresenter.BuildDetail(
                        candidate,
                        bounds,
                        GetCandidateOverlapInfo(bounds),
                        GetMinimumDetectionConfidence()),
                    comparison,
                    showComparison: true);
            }
        }

        internal void ExecuteTogglePatchCoreHeatmapCommand()
        {
            if (CandidateReviewViewModel == null)
            {
                return;
            }

            if (patchCoreHeatmapWindowHost.IsOpen || CandidateReviewViewModel.IsPatchCoreHeatmapOpen)
            {
                ClosePatchCoreHeatmapWindow();
                return;
            }

            PatchCoreHeatmapLoadResult result = patchCoreHeatmapReviewService.Load(GetSelectedCandidate());
            CandidateReviewViewModel.ShowPatchCoreHeatmap(result);
            if (!CandidateReviewViewModel.IsPatchCoreHeatmapOpen)
            {
                return;
            }

            patchCoreHeatmapWindowHost.Show(CandidateReviewViewModel, GetSelectedCandidate(), Topmost);
        }

        internal void ClosePatchCoreHeatmapWindow()
        {
            patchCoreHeatmapWindowHost.Close();
        }
        #endregion
    }

    internal sealed class CandidateReviewStateAdapterContext
    {
        internal Func<LabelingProjectData> DataProvider { get; init; }
        internal WpfCanvasPanelViewModel CanvasPanelViewModel { get; init; }
        internal WpfCandidateReviewPanelViewModel CandidateReviewViewModel { get; init; }
        internal CandidateReviewStateService CandidateReviewState { get; init; }
        internal Func<IReadOnlyList<WpfClassCatalogListItem>> ClassCatalogProvider { get; init; }
        internal Predicate<YoloWorkerSmokeCandidate> IsSmartMaskCandidate { get; init; }
        internal CandidateReviewPresentationService CandidateReviewPresentationService { get; init; }
        internal CandidateReviewCompletionPresentationService CandidateReviewCompletionPresentationService { get; init; }
        internal ImageDetectionWorkflowService ImageDetectionWorkflowService { get; init; }
        internal PatchCoreHeatmapReviewService PatchCoreHeatmapReviewService { get; init; }
        internal PatchCoreHeatmapWindowHost PatchCoreHeatmapWindowHost { get; init; }
        internal AnnotationDirtyState AnnotationDirtyState { get; init; }
        internal Func<string> ActiveImagePathProvider { get; init; }
        internal Func<Size> ActiveImageSizeProvider { get; init; }
        internal Func<Bitmap> ActiveImageBitmapProvider { get; init; }
        internal IList<Rectangle> ManualRois { get; init; }
        internal IReadOnlyList<string> ManualRoiOverlayIds { get; init; }
        internal Func<int, string> GetManualRoiClassName { get; init; }
        internal Func<int> GetCanvasLabelObjectCount { get; init; }
        internal Func<bool> IsApplicationCloseApproved { get; init; }
        internal Func<bool> IsCandidateMutationBlocked { get; init; }
        internal Func<bool> IsInferenceInspectionOnly { get; init; }
        internal Func<bool> IsInferenceWorkspaceStage { get; init; }
        internal Func<bool> HasDisplayedInferenceResult { get; init; }
        internal Func<bool> IsModelWorkflowCreated { get; init; }
        internal Func<bool> IsTopmostProvider { get; init; }
        internal Action ShowCandidateReviewWorkflowView { get; init; }
        internal Action RedrawReviewRois { get; init; }
        internal Action<string> SetModelStatus { get; init; }
        internal Action UpdateWorkflowProgressStatus { get; init; }
        internal Action RefreshInferenceResultPresentation { get; init; }
        internal Slider CandidateConfidenceSlider { get; init; }
        internal ListBox CandidateListBox { get; init; }
        internal Control FitCanvasButton { get; init; }
        internal Control ActualSizeCanvasButton { get; init; }
        internal Control PanCanvasButton { get; init; }
        internal Control FocusCandidateCanvasButton { get; init; }
        internal Control ResetAiOverlayCanvasButton { get; init; }
    }
}
