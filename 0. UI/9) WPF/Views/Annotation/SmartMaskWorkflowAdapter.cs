using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.Canvas;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using OpenVisionLab.ImageCanvas.ViewModels;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the Smart Mask prompt and candidate workflow.
    /// Python execution and prompt-session state remain in their existing services;
    /// candidate confirmation and overlay rendering remain with their existing owners.
    /// </summary>
    internal sealed class SmartMaskWorkflowAdapter
    {
        private readonly SmartMaskWorkflowService workflowService;
        private readonly SmartMaskPromptSessionService promptSession;
        private readonly MaskCandidateEditBufferService candidateEditBuffer = new MaskCandidateEditBufferService();
        private readonly MaskAnnotationService maskAnnotationService = new MaskAnnotationService();
        private readonly SmartMaskWorkflowAdapterContext context;
        private bool isLatestGenerationLoopActive;
        private bool hasLatestGenerationPending;
        private long latestGenerationVersion;
        private Point? lastCandidateStrokePoint;
        private bool candidateStrokeActive;
        private MaskRegionOperation candidateRegionOperation = MaskRegionOperation.Add;
        private bool candidateApplyToSelectedObject;
        private bool candidateReplaceSelectedObject;
        private string selectedObjectPromptId = string.Empty;

        private static readonly TimeSpan LatestGenerationDebounce = TimeSpan.FromMilliseconds(180);

        private LabelingProjectData Data => context.DataProvider?.Invoke();
        private CandidateReviewStateService CandidateReviewState => context.CandidateReviewState;
        private WpfCanvasPanelViewModel CanvasPanelViewModel => context.CanvasPanelViewModel;
        private RoiImageCanvasViewModel MainCanvasViewModel => context.MainCanvasViewModel;
        private Bitmap ActiveImageBitmap => context.ActiveImageBitmapProvider?.Invoke();
        private Size ActiveImageSize => context.ActiveImageSizeProvider?.Invoke() ?? Size.Empty;
        private string ActiveImagePath => context.ActiveImagePathProvider?.Invoke() ?? string.Empty;
        private WpfAnnotationTool ActiveAnnotationTool => context.ActiveAnnotationToolProvider?.Invoke() ?? WpfAnnotationTool.Select;
        private bool IsApplicationCloseApproved => context.IsApplicationCloseApproved?.Invoke() == true;

        internal SmartMaskWorkflowAdapter(
            SmartMaskWorkflowService workflowService,
            SmartMaskPromptSessionService promptSession,
            SmartMaskWorkflowAdapterContext context)
        {
            this.workflowService = workflowService ?? throw new ArgumentNullException(nameof(workflowService));
            this.promptSession = promptSession ?? throw new ArgumentNullException(nameof(promptSession));
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            ArgumentNullException.ThrowIfNull(context.DataProvider);
            ArgumentNullException.ThrowIfNull(context.RecipeNameProvider);
            ArgumentNullException.ThrowIfNull(context.CandidateReviewState);
            ArgumentNullException.ThrowIfNull(context.ManualSegments);
            ArgumentNullException.ThrowIfNull(context.EnsureClassItem);
        }

        internal void StartCandidateGeneration()
            => _ = RunLatestGenerationLoopAsync(debounceFirstRequest: false);

        internal void ActivateSmartMaskTool()
        {
            if (IsApplicationCloseApproved)
            {
                return;
            }

            RefreshSmartMaskCommandState();
            if (MainCanvasViewModel != null)
            {
                MainCanvasViewModel.IsTeachingMode = false;
                MainCanvasViewModel.IsImagePointInputMode = promptSession.HasSession
                    && promptSession.InputMode != WpfSmartMaskPointInputMode.None;
                MainCanvasViewModel.ImageViewer.SetViewMode(CanvasInteractionMode.None);
            }
        }

        private void RequestLatestCandidateGeneration()
        {
            hasLatestGenerationPending = true;
            latestGenerationVersion++;
            if (workflowService.IsRunning)
            {
                workflowService.Cancel();
                promptSession.InvalidatePendingGeneration();
            }

            if (!isLatestGenerationLoopActive)
            {
                _ = RunLatestGenerationLoopAsync(debounceFirstRequest: true);
            }
        }

        private async Task RunLatestGenerationLoopAsync(bool debounceFirstRequest)
        {
            if (isLatestGenerationLoopActive)
            {
                return;
            }

            isLatestGenerationLoopActive = true;
            bool shouldDebounce = debounceFirstRequest;
            try
            {
                do
                {
                    if (shouldDebounce)
                    {
                        await WaitForLatestGenerationDebounceAsync();
                    }

                    hasLatestGenerationPending = false;
                    await ExecuteCreateSmartMaskCandidateCommandAsync();
                    shouldDebounce = hasLatestGenerationPending;
                }
                while (hasLatestGenerationPending && !IsApplicationCloseApproved && promptSession.HasSession);
            }
            finally
            {
                isLatestGenerationLoopActive = false;
            }
        }

        private async Task WaitForLatestGenerationDebounceAsync()
        {
            long observedVersion;
            do
            {
                observedVersion = latestGenerationVersion;
                await Task.Delay(LatestGenerationDebounce);
            }
            while (observedVersion != latestGenerationVersion && !IsApplicationCloseApproved);
        }

        internal async Task ExecuteCreateSmartMaskCandidateCommandAsync()
        {
            if (IsApplicationCloseApproved || workflowService.IsRunning)
            {
                return;
            }

            bool isStartingSession = !promptSession.HasSession;
            bool isStartingFromSelectedObject = false;
            int selectedObjectPromptIndex = -1;
            string promptOverlayId = string.Empty;
            Rectangle promptBounds = Rectangle.Empty;
            if (isStartingSession)
            {
                if (ActiveImageSize.IsEmpty || string.IsNullOrWhiteSpace(ActiveImagePath))
                {
                    RefreshSmartMaskCommandState(OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.ImageRequired"));
                    AppendLog("스마트 마스크: 활성 이미지가 없습니다.");
                    return;
                }

                int promptIndex = FindSmartMaskPromptIndex();
                if (promptIndex >= 0)
                {
                    promptOverlayId = ObjectReviewSelectionService.GetManualRoiOverlayId(
                        context.ManualRoiOverlayIds,
                        promptIndex);
                    promptBounds = context.ManualRois[promptIndex];
                    string className = GetManualRoiClassName(promptIndex);
                    promptSession.Start(
                        ActiveImagePath,
                        GetCurrentSmartMaskRecipeName(),
                        promptBounds,
                        ResolveClassId(className),
                        className);
                }
                else if (TryGetSmartMaskStartingSegmentIndex(out selectedObjectPromptIndex))
                {
                    LabelingSegmentationObject selectedSegment = context.ManualSegments[selectedObjectPromptIndex];
                    promptBounds = selectedSegment?.Bounds ?? Rectangle.Empty;
                    if (!promptBounds.IsEmpty)
                    {
                        string className = string.IsNullOrWhiteSpace(selectedSegment.ClassName)
                            ? selectedSegment.ClassItem?.Text ?? "Defect"
                            : selectedSegment.ClassName;
                        promptSession.Start(
                            ActiveImagePath,
                            GetCurrentSmartMaskRecipeName(),
                            promptBounds,
                            ResolveClassId(className),
                            className);
                        isStartingFromSelectedObject = true;
                        selectedObjectPromptId = selectedSegment.ObjectId ?? string.Empty;
                        candidateReplaceSelectedObject = true;
                        candidateApplyToSelectedObject = true;
                    }
                }

                if (promptBounds.IsEmpty)
                {
                    RefreshSmartMaskCommandState(OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.RegionRequired"));
                    AppendLog(OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.RegionMissing"));
                    return;
                }
            }

            WpfSmartMaskPromptSnapshot snapshot = promptSession.Capture();
            MobileSamBoxPromptRequest request = workflowService.BuildRequest(
                Data.ProjectSettings?.PythonModel,
                snapshot.ImagePath,
                snapshot.PromptBounds,
                snapshot.ClassId,
                snapshot.ClassName,
                snapshot.Points,
                promptSession.MaximumPolygonPoints);
            if (!request.IsValid)
            {
                string error = string.Join(" ", request.Errors);
                if (isStartingSession)
                {
                    ResetSmartMaskPromptSession();
                }

                RefreshSmartMaskCommandState(error);
                AppendLog("스마트 마스크 준비 실패: " + error);
                return;
            }

            if (MainCanvasViewModel != null)
            {
                MainCanvasViewModel.IsTeachingMode = false;
            }

            RefreshSmartMaskCommandState(OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.Generating"));
            SetYoloCommandStatus("스마트 마스크 후보 생성 중...", isBusy: true);
            Task<SmartMaskWorkflowResult> workflowTask = workflowService.RunAsync(
                new SmartMaskWorkflowRequest { Prompt = request });
            RefreshSmartMaskCommandState(OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.Generating"));
            SmartMaskWorkflowResult workflowResult = await workflowTask;

            if (IsApplicationCloseApproved)
            {
                return;
            }

            if (!workflowResult.Started)
            {
                RefreshSmartMaskCommandState("스마트 마스크 후보 생성이 이미 실행 중이거나 종료된 상태입니다.");
                return;
            }

            if (workflowResult.IsCanceled)
            {
                RefreshSmartMaskCommandState(OpenVisionLab.OpenVisionLanguageService.T("WpfSmartMask.Status.CancelGuide"));
                SetYoloCommandStatus(OpenVisionLab.OpenVisionLanguageService.T("WpfSmartMask.Status.Canceled"), isBusy: false);
                AppendLog(OpenVisionLab.OpenVisionLanguageService.T("WpfSmartMask.Log.Canceled"));
                return;
            }

            if (workflowResult.Error != null)
            {
                string workflowError = workflowResult.Error.Message;
                RefreshSmartMaskCommandState(workflowError);
                SetYoloCommandStatus(OpenVisionLab.OpenVisionLanguageService.T("WpfSmartMask.Status.FailedPrefix") + " " + workflowError, isBusy: false);
                AppendLog(OpenVisionLab.OpenVisionLanguageService.T("WpfSmartMask.Status.FailedPrefix") + " " + workflowError);
                return;
            }

            MobileSamBoxPromptResult result = workflowResult.Result;
            if (!promptSession.Matches(
                    snapshot,
                    ActiveImagePath,
                    GetCurrentSmartMaskRecipeName()))
            {
                RefreshSmartMaskCommandState("이미지, 레시피 또는 프롬프트가 변경되어 이전 결과를 적용하지 않았습니다.");
                AppendLog("스마트 마스크 결과 무시: 실행 중 이미지, 레시피 또는 프롬프트가 변경되었습니다.");
                return;
            }

            if (result == null || !result.Succeeded || result.Candidate == null)
            {
                string resultError = string.IsNullOrWhiteSpace(result?.Error)
                    ? OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.NoResult")
                    : result.Error;
                RefreshSmartMaskCommandState(resultError);
                SetYoloCommandStatus(OpenVisionLab.OpenVisionLanguageService.T("WpfSmartMask.Status.FailedPrefix") + " " + resultError, isBusy: false);
                AppendLog(OpenVisionLab.OpenVisionLanguageService.T("WpfSmartMask.Status.FailedPrefix") + " " + resultError);
                return;
            }

            if (isStartingSession && !isStartingFromSelectedObject)
            {
                int currentPromptIndex = ObjectReviewSelectionService.FindManualRoiIndexByOverlayId(
                    context.ManualRoiOverlayIds,
                    promptOverlayId);
                if (currentPromptIndex < 0 || context.ManualRois[currentPromptIndex] != promptBounds)
                {
                    promptSession.Reset();
                    RefreshSmartMaskCommandState("프롬프트 박스가 변경되어 후보를 적용하지 않았습니다.");
                    AppendLog("스마트 마스크 결과 무시: 프롬프트 박스가 변경되었습니다.");
                    return;
                }

                RegisterAnnotationHistoryBeforeChange("박스를 스마트 마스크 프롬프트로 전환", markDirty: false);
                context.ManualRois.RemoveAt(currentPromptIndex);
                RemoveAtIfPresent(context.ManualRoiClassNames, currentPromptIndex);
                RemoveAtIfPresent(context.ManualRoiShapeKinds, currentPromptIndex);
                RemoveAtIfPresent(context.ManualRoiOverlayIds, currentPromptIndex);
            }
            else if (isStartingSession && isStartingFromSelectedObject)
            {
                if (!TryGetSmartMaskStartingSegmentIndex(out int currentSelectedIndex)
                    || currentSelectedIndex != selectedObjectPromptIndex
                    || !string.Equals(
                        context.ManualSegments[currentSelectedIndex]?.ObjectId ?? string.Empty,
                        selectedObjectPromptId,
                        StringComparison.Ordinal))
                {
                    promptSession.Reset();
                    candidateApplyToSelectedObject = false;
                    candidateReplaceSelectedObject = false;
                    selectedObjectPromptId = string.Empty;
                    RefreshSmartMaskCommandState("선택 객체가 변경되어 후보를 적용하지 않았습니다.");
                    AppendLog("스마트 마스크 결과 무시: 선택 객체가 변경되었습니다.");
                    return;
                }

                RegisterAnnotationHistoryBeforeChange("선택 폴리곤을 스마트 마스크 프롬프트로 전환", markDirty: false);
            }
            else
            {
                RegisterAnnotationHistoryBeforeChange("스마트 마스크 후보 다시 생성", markDirty: false);
            }

            // Keep the origin across CandidateReview refresh callbacks. Those callbacks
            // can observe the pending candidate before the prompt session records it and
            // would otherwise clear the selected-object replacement flags.
            string selectedObjectOriginId = isStartingFromSelectedObject
                ? selectedObjectPromptId
                : string.Empty;

            // Candidate Review owns the pending AI candidate. The session keeps only
            // the initial/latest alternatives until the operator confirms or skips it.
            bool keepSegmentationLabelingWorkflow =
                Data.ProjectSettings?.DatasetPurpose == LabelingDatasetPurpose.Segmentation;
            if (isStartingFromSelectedObject || keepSegmentationLabelingWorkflow)
            {
                context.ApplyDetectionCandidatesPreservingConfirmedInCurrentWorkflow?.Invoke(
                    new[] { result.Candidate },
                    true);
            }
            else
            {
                context.ApplyDetectionCandidatesPreservingConfirmed?.Invoke(
                    new[] { result.Candidate },
                    true);
            }
            promptSession.RecordCandidate(result.Candidate);
            if (isStartingFromSelectedObject)
            {
                candidateApplyToSelectedObject = true;
                candidateReplaceSelectedObject = true;
                selectedObjectPromptId = selectedObjectOriginId;
            }
            promptSession.SetInputMode(WpfSmartMaskPointInputMode.Positive);
            // Candidate point input is a labeling action in the reference workflow.
            // The polygon tool must not consume the next click as another polygon vertex.
            context.SelectAnnotationTool?.Invoke(WpfAnnotationTool.Select, false);
            if (MainCanvasViewModel != null)
            {
                MainCanvasViewModel.IsImagePointInputMode = true;
            }
            BeginCandidateEditSession(result.Candidate);
            RefreshPolygonOverlays();
            SetYoloCommandStatus(result.Summary + OpenVisionLab.OpenVisionLanguageService.T("WpfSmartMask.Status.Unaccepted"), isBusy: false);
            AppendLog($"{result.Summary} / {result.RuntimeSummary} / mask area {result.MaskArea}");
            RefreshSmartMaskCommandState("자동 후보가 부족할 때 보정 옵션에서 한 점을 추가하고 다시 생성해 비교하세요. 확정 전에는 저장되지 않습니다.");
        }

        internal void ExecuteSetSmartMaskPointModeCommand(WpfSmartMaskPointInputMode mode)
        {
            if (!promptSession.HasSession || workflowService.IsRunning)
            {
                return;
            }

            WpfSmartMaskPointInputMode nextMode = mode;
            promptSession.SetInputMode(nextMode);
            if (MainCanvasViewModel != null)
            {
                MainCanvasViewModel.IsTeachingMode = false;
                MainCanvasViewModel.IsImagePointInputMode = nextMode != WpfSmartMaskPointInputMode.None;
                MainCanvasViewModel.ImageViewer.SetViewMode(
                    ActiveAnnotationTool == WpfAnnotationTool.PanZoom
                        ? CanvasInteractionMode.Drag
                        : CanvasInteractionMode.None);
            }

            RefreshSmartMaskCommandState(nextMode == WpfSmartMaskPointInputMode.Positive
                ? "캔버스에서 객체에 포함할 위치를 클릭하세요."
                : nextMode == WpfSmartMaskPointInputMode.Negative
                    ? "캔버스에서 후보에서 제외할 위치를 클릭하세요."
                    : "보정점 입력을 종료했습니다.");
        }

        internal void ExecuteCancelSmartMaskGenerationCommand()
        {
            if (!workflowService.IsRunning)
            {
                return;
            }

            workflowService.Cancel();
            promptSession.InvalidatePendingGeneration();
            RefreshSmartMaskCommandState("후보 생성을 취소하는 중입니다.");
        }

        internal void ExecuteUndoSmartMaskPointCommand()
        {
            if (promptSession.UndoPoint())
            {
                RefreshPolygonOverlays();
                RefreshSmartMaskCommandState("마지막 보정점을 취소했습니다. 후보 다시 생성을 눌러 반영하세요.");
            }
        }

        internal void ExecuteClearSmartMaskPointsCommand()
        {
            if (promptSession.ClearPoints())
            {
                RefreshPolygonOverlays();
                RefreshSmartMaskCommandState("모든 보정점을 지웠습니다. 후보 다시 생성을 눌러 반영하세요.");
            }
        }

        internal void ExecuteSetSmartMaskPolygonDetailCommand(WpfSmartMaskPolygonDetail detail)
        {
            promptSession.SetPolygonDetail(detail);
            RefreshSmartMaskCommandState();
        }

        internal void ExecuteNextSmartMaskInstanceCommand()
        {
            if (!promptSession.HasSession
                || !promptSession.HasProducedCandidate
                || CandidateReviewState.HasPendingCandidates
                || workflowService.IsRunning)
            {
                RefreshSmartMaskCommandState("현재 후보를 먼저 확정하거나 건너뛰세요.");
                return;
            }

            ResetSmartMaskPromptSession();
            SelectAnnotationTool(WpfAnnotationTool.Rectangle);
            string nextDetail = CanvasPanelViewModel?.IsSmartMaskAutoContourEnabled == true
                ? OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.NextAuto")
                : OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.NextManual");
            RefreshSmartMaskCommandState(nextDetail);
            SetYoloCommandStatus("스마트 마스크: 다음 객체 박스를 기다립니다.", isBusy: false);
        }

        internal void ExecuteApplySmartMaskCandidateCommand()
        {
            if (IsApplicationCloseApproved || workflowService.IsRunning || ActiveImageSize.IsEmpty)
            {
                return;
            }

            YoloWorkerSmokeCandidate candidate = CandidateReviewState.PendingCandidates
                .FirstOrDefault(promptSession.IsSelectedCandidate);
            IReadOnlyList<PointF> contour = CandidateReviewPresentationService.GetCandidateContourPoints(candidate, ActiveImageSize);
            if (candidate == null || contour.Count < 3)
            {
                string unavailable = OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.ApplyMissing");
                SetYoloCommandStatus(unavailable, isBusy: false);
                AppendLog(unavailable);
                return;
            }

            LabelingSegmentationObject source = BuildCandidateSource(candidate, contour, context.EnsureClassItem(candidate.ClassName), ActiveImageSize);
            if (!SegmentationMaskGeometryService.TryRasterize(source, ActiveImageSize, out byte[] sourceMask, out _))
            {
                string invalid = OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.ConversionFailed");
                SetYoloCommandStatus(invalid, isBusy: false);
                AppendLog(invalid);
                return;
            }

            CompleteSmartMaskCandidateStroke();
            EnsureCandidateEditSession(candidate, source, sourceMask);
            MaskCandidateEditSessionToken token = candidateEditBuffer.CaptureToken();
            if (token == null
                || !candidateEditBuffer.TryBuildApplyResult(
                    token,
                    ActiveImagePath,
                    GetCurrentSmartMaskRecipeName(),
                    source.ObjectId,
                    out byte[] editedCandidateMask))
            {
                candidateEditBuffer.Cancel();
                string failed = OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.PrepareFailed");
                SetYoloCommandStatus(failed, isBusy: false);
                AppendLog(failed);
                return;
            }

            bool targetSelectedObject = candidateApplyToSelectedObject;
            bool replaceSelectedObject = targetSelectedObject && candidateReplaceSelectedObject;
            int selectedSegmentIndex = targetSelectedObject && TryGetEditableSelectedManualSegmentIndex(out int editableIndex)
                ? editableIndex
                : -1;
            bool changedCanonical = false;
            LabelingSegmentationObject replacement = null;
            bool removeSelectedObject = false;
            if (targetSelectedObject && selectedSegmentIndex < 0)
            {
                string missingTarget = OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.SelectionMissing");
                SetYoloCommandStatus(missingTarget, isBusy: false);
                AppendLog(missingTarget);
                return;
            }

            if (targetSelectedObject)
            {
                changedCanonical = replaceSelectedObject
                    ? TryBuildSelectedObjectMaskReplacement(
                        context.ManualSegments[selectedSegmentIndex],
                        editedCandidateMask,
                        out replacement,
                        out removeSelectedObject)
                    : TryBuildSelectedObjectReplacement(
                        context.ManualSegments[selectedSegmentIndex],
                        editedCandidateMask,
                        out replacement,
                        out removeSelectedObject);
            }
            else
            {
                changedCanonical = candidateEditBuffer.TryBuildCanonicalReplacement(
                    token,
                    ActiveImagePath,
                    GetCurrentSmartMaskRecipeName(),
                    source,
                    out replacement);
            }

            if (targetSelectedObject && !changedCanonical)
            {
                const string unchanged = "선택 객체와 후보 영역이 같아 적용할 변경이 없습니다.";
                SetYoloCommandStatus(unchanged, isBusy: false);
                AppendLog(unchanged);
                CandidateReviewState.SkipCandidate(candidate);
                candidateEditBuffer.Complete();
                candidateApplyToSelectedObject = false;
                RefreshSmartMaskCommandState(unchanged);
                return;
            }

            RegisterAnnotationHistoryBeforeChange(
                targetSelectedObject
                    ? replaceSelectedObject
                        ? OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.CorrectHistory")
                        : OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.SelectedApplyHistory")
                    : OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.ApplyHistory"),
                markDirty: true);
            if (targetSelectedObject)
            {
                if (removeSelectedObject)
                {
                    context.ManualSegments.RemoveAt(selectedSegmentIndex);
                }
                else if (replacement != null)
                {
                    replacement.Selected = true;
                    context.ManualSegments[selectedSegmentIndex] = replacement;
                }
            }
            else if (replacement != null)
            {
                replacement.ZOrder = SegmentationZOrderService.GetNextZOrder(context.ManualSegments);
                replacement.Selected = true;
                context.ManualSegments.Add(replacement);
            }
            CandidateReviewState.SkipCandidate(candidate);
            candidateEditBuffer.Complete();
            candidateApplyToSelectedObject = false;
            // Applying the candidate ends the prompt session. Keep the committed
            // mask visible, but release the upper-left Smart Mask popup so the
            // normal polygon/brush/eraser rail remains reachable for refinement.
            ResetSmartMaskPromptSession();
            ApplyCanvasDisplayMode(WpfCanvasDisplayMode.LabelsOnly, redraw: false, logChange: false);
            context.RefreshCandidateListWithPreferred?.Invoke(null);
            context.RefreshObjectList?.Invoke();
            RefreshPolygonOverlays();
            context.QueueActiveImageQueueStatusRefresh?.Invoke(CandidateReviewState.HasPendingCandidates);
            context.RefreshCanvasWorkflowContext?.Invoke();
            string applySummary = targetSelectedObject
                ? (replaceSelectedObject
                    ? OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.Corrected")
                    : removeSelectedObject
                    ? OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.RemovedEmpty")
                    : candidateRegionOperation == MaskRegionOperation.Add
                        ? OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.Added")
                        : OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.Subtracted"))
                : replacement == null
                    ? OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.Empty")
                    : OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.Applied");
            context.AddCandidateReviewHistory?.Invoke(applySummary + " 실행 취소 가능");
            SetYoloCommandStatus(applySummary, isBusy: false);
            AppendLog(applySummary + " / 실행 취소 1단계 등록 / 파일 저장 전");
            RefreshSmartMaskCommandState(applySummary);
        }

        private bool TryBuildSelectedObjectReplacement(
            LabelingSegmentationObject target,
            byte[] candidateMask,
            out LabelingSegmentationObject replacement,
            out bool removeObject)
        {
            replacement = null;
            removeObject = false;
            if (target == null
                || !SegmentationMaskGeometryService.TryRasterize(target, ActiveImageSize, out byte[] targetMask, out _))
            {
                return false;
            }

            MaskRegionAlgebraResult result = MaskRegionAlgebraService.Apply(
                targetMask,
                candidateMask,
                ActiveImageSize,
                candidateRegionOperation);
            if (!result.Changed)
            {
                return false;
            }

            if (result.IsEmpty)
            {
                removeObject = true;
                return true;
            }

            Rectangle bounds = SegmentationGeometry.GetMaskBounds(result.MaskData, ActiveImageSize);
            replacement = new LabelingSegmentationObject
            {
                ClassName = target.ClassName,
                ClassItem = target.ClassItem,
                ObjectId = target.ObjectId,
                ComponentIndex = target.ComponentIndex,
                ZOrder = target.ZOrder,
                LastStructuralOperation = candidateRegionOperation == MaskRegionOperation.Add
                    ? "SmartMaskCandidateAdd"
                    : "SmartMaskCandidateSubtract",
                MaskData = result.MaskData,
                MaskSize = ActiveImageSize,
                MaskBounds = bounds,
                RenderVersion = target.RenderVersion + 1,
                RenderDirtyBounds = Rectangle.Union(target.Bounds, bounds),
                Selected = target.Selected
            };
            return true;
        }

        private bool TryBuildSelectedObjectMaskReplacement(
            LabelingSegmentationObject target,
            byte[] candidateMask,
            out LabelingSegmentationObject replacement,
            out bool removeObject)
        {
            replacement = null;
            removeObject = false;
            if (target == null
                || candidateMask == null
                || ActiveImageSize.IsEmpty
                || candidateMask.Length != ActiveImageSize.Width * ActiveImageSize.Height
                || !SegmentationMaskGeometryService.TryRasterize(target, ActiveImageSize, out byte[] targetMask, out _))
            {
                return false;
            }

            if (candidateMask.SequenceEqual(targetMask))
            {
                return false;
            }

            Rectangle bounds = SegmentationGeometry.GetMaskBounds(candidateMask, ActiveImageSize);
            if (bounds.IsEmpty)
            {
                removeObject = true;
                return true;
            }

            replacement = new LabelingSegmentationObject
            {
                ClassName = target.ClassName,
                ClassItem = target.ClassItem,
                ObjectId = target.ObjectId,
                ComponentIndex = target.ComponentIndex,
                ZOrder = target.ZOrder,
                LastStructuralOperation = "SmartMaskCandidateReplace",
                MaskData = (byte[])candidateMask.Clone(),
                MaskSize = ActiveImageSize,
                MaskBounds = bounds,
                RenderVersion = target.RenderVersion + 1,
                RenderDirtyBounds = Rectangle.Union(target.Bounds, bounds),
                Selected = target.Selected
            };
            return true;
        }

        internal void TryStartAutoSmartMaskForNewRoi(CanvasRect<float> roiRect)
        {
            if (roiRect == null
                || roiRect.ShapeKind != CanvasRoiShapeKind.Rectangle
                || CanvasPanelViewModel?.IsSmartMaskAutoContourEnabled != true
                || Data.ProjectSettings?.DatasetPurpose != LabelingDatasetPurpose.Segmentation
                || ActiveAnnotationTool != WpfAnnotationTool.Rectangle
                || promptSession.HasSession
                || CandidateReviewState.HasPendingCandidates
                || workflowService.IsRunning)
            {
                return;
            }

            AppendLog(OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.AutoContour"));
            StartCandidateGeneration();
        }

        internal void ContinueAutoSmartMaskAfterResolvedCandidate(string resolution)
        {
            if (CanvasPanelViewModel?.IsSmartMaskAutoContourEnabled != true
                || CandidateReviewState.HasPendingCandidates)
            {
                return;
            }

            ResetSmartMaskPromptSession();
            SelectAnnotationTool(WpfAnnotationTool.Rectangle);
            RefreshSmartMaskCommandState(string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.NextBox"), resolution));
            SetYoloCommandStatus($"자동 윤곽: {resolution} 완료 · 다음 박스 대기", isBusy: false);
        }

        internal void ExecuteSelectSmartMaskCandidateVersionCommand(WpfSmartMaskCandidateVersion version)
        {
            if (workflowService.IsRunning
                || !CandidateReviewState.HasPendingCandidates
                || !promptSession.TrySelectCandidate(version, out YoloWorkerSmokeCandidate candidate))
            {
                RefreshSmartMaskCommandState(OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.NoComparison"));
                return;
            }

            CandidateReviewState.LoadPendingCandidates(new[] { candidate }, clearConfirmed: false);
            BeginCandidateEditSession(candidate);
            ApplyCanvasDisplayMode(WpfCanvasDisplayMode.InferenceOnly, redraw: false, logChange: false);
            context.RefreshCandidateListWithPreferred?.Invoke(candidate);
            context.RefreshObjectList?.Invoke();
            RefreshPolygonOverlays();
            context.RefreshCanvasWorkflowContext?.Invoke();
            string versionText = version == WpfSmartMaskCandidateVersion.Initial ? OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.InitialVersion") : OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.CurrentVersion");
            context.AddCandidateReviewHistory?.Invoke(string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.VersionHistory"), versionText));
            SetYoloCommandStatus(string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.VersionSelected"), versionText), isBusy: false);
            AppendLog(string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.VersionChanged"), versionText));
            RefreshSmartMaskCommandState($"{versionText} 후보를 보고 있습니다. 확정 후 따로 저장하세요.");
        }

        internal bool TryApplySmartMaskPointInput(CanvasImagePointEventArgs e)
        {
            if (e == null
                || !promptSession.HasSession
                || ActiveAnnotationTool == WpfAnnotationTool.Brush
                || ActiveAnnotationTool == WpfAnnotationTool.Eraser)
            {
                return false;
            }

            if (e.Button == CanvasPointerButton.Right)
            {
                ExecuteUndoSmartMaskPointCommand();
                return true;
            }

            if (e.Button != CanvasPointerButton.Left)
            {
                return true;
            }

            WpfSmartMaskPointInputMode previousMode = promptSession.InputMode == WpfSmartMaskPointInputMode.None
                ? WpfSmartMaskPointInputMode.Positive
                : promptSession.InputMode;
            bool isAltExclude = (Keyboard.Modifiers & ModifierKeys.Alt) == ModifierKeys.Alt;
            WpfSmartMaskPointInputMode effectiveMode =
                isAltExclude
                    ? WpfSmartMaskPointInputMode.Negative
                    : previousMode;
            if (effectiveMode != promptSession.InputMode)
            {
                promptSession.SetInputMode(effectiveMode);
            }

            if (!promptSession.TryAddPoint(e.ImagePoint, ActiveImageSize))
            {
                if (isAltExclude && previousMode != promptSession.InputMode)
                {
                    promptSession.SetInputMode(previousMode);
                }

                return true;
            }

            if (isAltExclude && previousMode != promptSession.InputMode)
            {
                promptSession.SetInputMode(previousMode);
            }

            RefreshPolygonOverlays();
            RefreshSmartMaskCommandState("보정점을 추가했습니다. 마지막 입력만 반영해 후보를 다시 계산합니다.");
            RequestLatestCandidateGeneration();
            return true;
        }

        internal bool TryApplySmartMaskCandidatePolygonInput(
            CanvasImagePointEventArgs e,
            PolygonAnnotationService polygonAnnotationService)
        {
            if (e == null
                || polygonAnnotationService == null
                || !IsCandidateEditAvailable()
                || ActiveAnnotationTool != WpfAnnotationTool.Polygon)
            {
                return false;
            }

            if (e.Button == CanvasPointerButton.Right)
            {
                polygonAnnotationService.Reset();
                RefreshPolygonOverlays();
                SetYoloCommandStatus(OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.PolygonCancelled"), false);
                return true;
            }

            if (e.Button != CanvasPointerButton.Left || ActiveImageSize.IsEmpty)
            {
                return true;
            }

            if (e.Clicks > 1 && polygonAnnotationService.Points.Count >= 3)
            {
                ApplySmartMaskCandidatePolygon(polygonAnnotationService);
                return true;
            }

            if (!polygonAnnotationService.TryAddPoint(e.ImagePoint, ActiveImageSize, out bool closed))
            {
                return true;
            }

            RefreshPolygonOverlays();
            if (closed)
            {
                ApplySmartMaskCandidatePolygon(polygonAnnotationService);
            }
            else
            {
                SetYoloCommandStatus(
                    string.Format(OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.PolygonPoints"), polygonAnnotationService.Points.Count),
                    false);
            }

            return true;
        }

        internal bool TryCompleteSmartMaskCandidatePolygonDraft(PolygonAnnotationService polygonAnnotationService)
        {
            if (!IsCandidateEditAvailable()
                || ActiveAnnotationTool != WpfAnnotationTool.Polygon
                || polygonAnnotationService?.Points.Count < 3)
            {
                return false;
            }

            ApplySmartMaskCandidatePolygon(polygonAnnotationService);
            return true;
        }

        internal bool TryCancelSmartMaskCandidatePolygonDraft(PolygonAnnotationService polygonAnnotationService)
        {
            if (!IsCandidateEditAvailable()
                || ActiveAnnotationTool != WpfAnnotationTool.Polygon
                || polygonAnnotationService?.Points.Count == 0)
            {
                return false;
            }

            polygonAnnotationService.Reset();
            RefreshPolygonOverlays();
            SetYoloCommandStatus(OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.PolygonCancelled"), false);
            return true;
        }

        private void ApplySmartMaskCandidatePolygon(PolygonAnnotationService polygonAnnotationService)
        {
            IReadOnlyList<Point> points = polygonAnnotationService.Points.ToList();
            MaskRegionAlgebraResult result = candidateEditBuffer.ApplyPolygon(
                points,
                cutouts: null,
                candidateRegionOperation);
            polygonAnnotationService.Reset();
            RefreshPolygonOverlays();
            UpdateCandidateEditState();
            SetYoloCommandStatus(
                result.Changed
                    ? candidateRegionOperation == MaskRegionOperation.Add
                        ? "후보 마스크에 폴리곤 영역을 더했습니다."
                        : "후보 마스크에서 폴리곤 영역을 뺐습니다."
                    : "폴리곤 영역이 후보 마스크를 변경하지 않았습니다.",
                false);
        }

        internal bool TryApplySmartMaskCandidateStroke(CanvasImagePointEventArgs e, bool resetStroke)
        {
            if (e == null
                || !IsCandidateEditAvailable()
                || (ActiveAnnotationTool != WpfAnnotationTool.Brush && ActiveAnnotationTool != WpfAnnotationTool.Eraser))
            {
                return false;
            }

            if (e.Button == CanvasPointerButton.Right)
            {
                bool canceled = candidateEditBuffer.CancelStroke();
                candidateStrokeActive = false;
                lastCandidateStrokePoint = null;
                RefreshPolygonOverlays();
                SetYoloCommandStatus(
                    canceled
                        ? OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.StrokeCancelled")
                        : OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.StrokeMissing"),
                    isBusy: false);
                return true;
            }

            if (e.Button != CanvasPointerButton.Left)
            {
                return true;
            }

            if (resetStroke && candidateStrokeActive)
            {
                CompleteSmartMaskCandidateStroke();
            }

            if (!candidateStrokeActive)
            {
                if (!candidateEditBuffer.BeginStroke())
                {
                    return false;
                }

                candidateStrokeActive = true;
                lastCandidateStrokePoint = null;
            }

            int radius = GetCandidateBrushRadius();
            IReadOnlyList<Point> centers = maskAnnotationService.BuildStrokeCenters(
                resetStroke ? null : lastCandidateStrokePoint,
                e.ImagePoint,
                radius);
            lastCandidateStrokePoint = e.ImagePoint;
            MaskRegionOperation operation = ActiveAnnotationTool == WpfAnnotationTool.Eraser
                ? MaskRegionOperation.Subtract
                : MaskRegionOperation.Add;
            MaskRegionAlgebraResult result = candidateEditBuffer.ApplyBrushStroke(centers, radius, operation);
            if (result.Changed)
            {
                RefreshPolygonOverlays();
                UpdateCandidateEditState();
            }

            return true;
        }

        internal void CompleteSmartMaskCandidateStroke()
        {
            if (!candidateStrokeActive)
            {
                return;
            }

            bool changed = candidateEditBuffer.CompleteStroke();
            candidateStrokeActive = false;
            lastCandidateStrokePoint = null;
            if (changed)
            {
                RefreshPolygonOverlays();
                RefreshSmartMaskCommandState(OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.CorrectionBuffered"));
            }
            else
            {
                UpdateCandidateEditState();
            }
        }

        internal bool UndoSmartMaskCandidateEdit()
        {
            CompleteSmartMaskCandidateStroke();
            bool changed = candidateEditBuffer.Undo();
            if (changed)
            {
                RefreshPolygonOverlays();
                RefreshSmartMaskCommandState(OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.Undone"));
            }
            else
            {
                SetYoloCommandStatus(OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.UndoMissing"), false);
            }

            return changed;
        }

        internal bool RedoSmartMaskCandidateEdit()
        {
            CompleteSmartMaskCandidateStroke();
            bool changed = candidateEditBuffer.Redo();
            if (changed)
            {
                RefreshPolygonOverlays();
                RefreshSmartMaskCommandState(OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.Redone"));
            }
            else
            {
                SetYoloCommandStatus(OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.RedoMissing"), false);
            }

            return changed;
        }

        internal void SetSmartMaskCandidateRegionOperation(MaskRegionOperation operation)
        {
            candidateRegionOperation = operation == MaskRegionOperation.Subtract
                ? MaskRegionOperation.Subtract
                : MaskRegionOperation.Add;
            if (!IsCandidateEditAvailable() || !HasEditableSelectedManualSegment())
            {
                candidateApplyToSelectedObject = false;
                UpdateCandidateEditState();
                SetYoloCommandStatus(
                    "후보를 더하거나 빼려면 잠금 해제된 현재 선택 세그먼트가 필요합니다.",
                    false);
                return;
            }

            candidateApplyToSelectedObject = true;
            RefreshSmartMaskCommandState(
                candidateRegionOperation == MaskRegionOperation.Add
                    ? "현재 선택 객체에 후보를 더합니다. 적용 전까지 원본 라벨은 변경되지 않습니다."
                    : "현재 선택 객체에서 후보 영역을 뺍니다. 적용 전까지 원본 라벨은 변경되지 않습니다.");
        }

        internal bool TryGetCandidateEditSnapshot(out MaskCandidateEditSnapshot snapshot)
        {
            snapshot = null;
            if (!IsCandidateEditAvailable())
            {
                return false;
            }

            return candidateEditBuffer.TryGetSnapshot(out snapshot);
        }

        internal MaskRegionOperation GetSmartMaskCandidateRegionOperation()
            => candidateRegionOperation;

        internal bool IsCandidateEditAvailableForRendering()
            => IsCandidateEditAvailable();

        internal void ResetSmartMaskCandidateEditBuffer()
            => ResetSmartMaskCandidateEditBuffer(clearSelectedObjectOrigin: true);

        internal void ResetSmartMaskCandidateEditBuffer(bool clearSelectedObjectOrigin)
        {
            candidateStrokeActive = false;
            lastCandidateStrokePoint = null;
            candidateEditBuffer.CancelStroke();
            candidateEditBuffer.Cancel();
            if (clearSelectedObjectOrigin)
            {
                candidateApplyToSelectedObject = false;
                candidateReplaceSelectedObject = false;
                selectedObjectPromptId = string.Empty;
            }
        }

        internal void ResetSmartMaskPromptSession()
        {
            workflowService.Cancel();
            ResetSmartMaskCandidateEditBuffer();
            promptSession.Reset();
            if (MainCanvasViewModel != null)
            {
                MainCanvasViewModel.IsImagePointInputMode = false;
                MainCanvasViewModel.IsTeachingMode =
                    ActiveAnnotationTool == WpfAnnotationTool.Rectangle
                    || ActiveAnnotationTool == WpfAnnotationTool.Ellipse;
                MainCanvasViewModel.ImageViewer.SetViewMode(
                    ActiveAnnotationTool == WpfAnnotationTool.PanZoom
                        ? CanvasInteractionMode.Drag
                        : CanvasInteractionMode.None);
            }

            RefreshPolygonOverlays();
        }

        internal string GetCurrentSmartMaskRecipeName()
            => context.RecipeNameProvider?.Invoke() ?? string.Empty;

        internal int FindSmartMaskPromptIndex()
        {
            context.EnsureManualRoiMetadataCount?.Invoke();
            for (int index = context.ManualRois.Count - 1; index >= 0; index--)
            {
                if (ObjectReviewPresentationService.GetManualRoiShapeKind(context.ManualRoiShapeKinds, index) == CanvasRoiShapeKind.Rectangle
                    && !context.ManualRois[index].IsEmpty)
                {
                    return index;
                }
            }

            return -1;
        }

        internal void RefreshSmartMaskCommandState(string detail = "")
        {
            if (CanvasPanelViewModel == null)
            {
                return;
            }

            bool isVisible = Data.ProjectSettings?.DatasetPurpose == LabelingDatasetPurpose.Segmentation
                && (ActiveAnnotationTool == WpfAnnotationTool.SmartMask || promptSession.HasSession);
            if (promptSession.HasSession
                && !promptSession.MatchesContext(ActiveImagePath, GetCurrentSmartMaskRecipeName()))
            {
                ResetSmartMaskPromptSession();
            }

            bool hasSession = isVisible && promptSession.HasSession;
            if (hasSession
                && CandidateReviewState.PendingCandidates.Count == 1
                && string.Equals(
                    CandidateReviewState.PendingCandidates[0]?.CandidateType,
                    "smart-mask",
                    StringComparison.OrdinalIgnoreCase)
                && !promptSession.IsSelectedCandidate(CandidateReviewState.PendingCandidates[0]))
            {
                promptSession.RestoreSelectedCandidateReference(CandidateReviewState.PendingCandidates[0]);
            }
            YoloWorkerSmokeCandidate selectedCandidate = GetSelectedSmartMaskCandidate();
            CanvasPanelViewModel.SetSmartMaskConfidenceText(
                workflowService.IsRunning
                    ? "계산 중..."
                    : selectedCandidate == null
                        ? "신뢰도 —"
                        : $"신뢰도 {selectedCandidate.Confidence.ToString("P0", CultureInfo.CurrentCulture)}");
            if (selectedCandidate != null)
            {
                BeginCandidateEditSession(selectedCandidate);
            }
            else
            {
                ResetSmartMaskCandidateEditBuffer();
            }
            UpdateCandidateEditState();
            int promptIndex = isVisible && !hasSession ? FindSmartMaskPromptIndex() : -1;
            string effectiveDetail = detail;
            bool isReady = false;
            if (isVisible && !workflowService.IsRunning && !ActiveImageSize.IsEmpty)
            {
                WpfSmartMaskPromptSnapshot snapshot = hasSession
                    ? promptSession.Capture()
                    : promptIndex >= 0
                        ? new WpfSmartMaskPromptSnapshot
                        {
                            ImagePath = ActiveImagePath,
                            PromptBounds = context.ManualRois[promptIndex],
                            ClassName = GetManualRoiClassName(promptIndex)
                        }
                        : TryGetSmartMaskStartingSegmentIndex(out int selectedSegmentIndex)
                            ? BuildSelectedSegmentPromptSnapshot(selectedSegmentIndex)
                            : null;
                if (snapshot != null)
                {
                    MobileSamBoxPromptRequest request = workflowService.BuildRequest(
                        Data.ProjectSettings?.PythonModel,
                        snapshot.ImagePath,
                        snapshot.PromptBounds,
                        snapshot.ClassId,
                        snapshot.ClassName,
                        snapshot.Points,
                        hasSession ? promptSession.MaximumPolygonPoints : 96);
                    isReady = request.IsValid;
                    if (string.IsNullOrWhiteSpace(effectiveDetail) && !isReady)
                    {
                        effectiveDetail = string.Join(" ", request.Errors);
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(effectiveDetail))
            {
                effectiveDetail = hasSession
                    ? "포함점/제외점을 한 점씩 추가하고 다시 생성해 비교하세요. 다시 생성하면 대기 후보 하나를 교체합니다."
                    : promptIndex < 0 && TryGetSmartMaskStartingSegmentIndex(out _)
                        ? OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.PolygonSeed")
                        : promptIndex < 0
                            ? OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.DrawSeed")
                            : OpenVisionLab.OpenVisionLanguageService.T("WpfLabeling.SmartMaskWorkflowAdapter.LastBoxSeed");
            }

            CanvasPanelViewModel.SetSmartMaskState(
                isVisible,
                isReady,
                workflowService.IsRunning,
                effectiveDetail,
                hasSession);
            CanvasPanelViewModel.SetSmartMaskSessionState(
                hasSession,
                workflowService.IsRunning,
                promptSession.PositivePointCount,
                promptSession.NegativePointCount,
                promptSession.InputMode,
                promptSession.HasProducedCandidate,
                promptSession.HasProducedCandidate && !CandidateReviewState.HasPendingCandidates,
                promptSession.HasCandidateComparison
                    && CandidateReviewState.PendingCandidates.Count == 1
                    && promptSession.IsSelectedCandidate(CandidateReviewState.PendingCandidates[0]),
                promptSession.SelectedCandidateVersion,
                CandidateReviewState.PendingCandidates.Count == 1
                    && promptSession.IsSelectedCandidate(CandidateReviewState.PendingCandidates[0]));
        }

        private string GetManualRoiClassName(int index)
            => context.GetManualRoiClassName?.Invoke(index)
                ?? ObjectReviewPresentationService.GetManualRoiClassName(context.ManualRoiClassNames, index);

        private int? ResolveClassId(string className)
        {
            int classId = Data?.ClassNamedList?.FindIndex(item =>
                string.Equals(item?.Text, className, StringComparison.OrdinalIgnoreCase)) ?? -1;
            return classId >= 0 ? classId : null;
        }

        private WpfSmartMaskPromptSnapshot BuildSelectedSegmentPromptSnapshot(int index)
        {
            LabelingSegmentationObject selectedSegment = index >= 0 && index < context.ManualSegments.Count
                ? context.ManualSegments[index]
                : null;
            if (selectedSegment == null || selectedSegment.Bounds.IsEmpty)
            {
                return null;
            }

            string className = string.IsNullOrWhiteSpace(selectedSegment.ClassName)
                ? selectedSegment.ClassItem?.Text ?? "Defect"
                : selectedSegment.ClassName;
            return new WpfSmartMaskPromptSnapshot
            {
                ImagePath = ActiveImagePath,
                PromptBounds = selectedSegment.Bounds,
                ClassId = ResolveClassId(className),
                ClassName = className
            };
        }

        private void RegisterAnnotationHistoryBeforeChange(string actionName, bool markDirty)
            => context.RegisterAnnotationHistoryBeforeChange?.Invoke(actionName, markDirty);

        private YoloWorkerSmokeCandidate GetSelectedSmartMaskCandidate()
            => promptSession.HasSession
                && CandidateReviewState.PendingCandidates.Count == 1
                && string.Equals(
                    CandidateReviewState.PendingCandidates[0]?.CandidateType,
                    "smart-mask",
                    StringComparison.OrdinalIgnoreCase)
                && promptSession.IsSelectedCandidate(CandidateReviewState.PendingCandidates[0])
                ? CandidateReviewState.PendingCandidates[0]
                : null;

        private bool IsCandidateEditAvailable()
            => GetSelectedSmartMaskCandidate() != null
                && candidateEditBuffer.HasSession;

        private bool HasEditableSelectedManualSegment()
            => TryGetEditableSelectedManualSegmentIndex(out _);

        private bool TryGetSmartMaskStartingSegmentIndex(out int index)
        {
            if (TryGetEditableSelectedManualSegmentIndex(out index))
            {
                return true;
            }

            // Polygon completion selects the newly created segment in the object
            // review list, but WPF can briefly publish a null selection while that
            // list is rebuilt. In that narrow polygon-tool state, use the newest
            // editable segment as the same direct Smart Mask starting region the
            // reference workflow exposes after polygon confirmation.
            if (context.ActiveAnnotationToolProvider?.Invoke() != WpfAnnotationTool.Polygon)
            {
                index = -1;
                return false;
            }

            for (int candidate = context.ManualSegments.Count - 1; candidate >= 0; candidate--)
            {
                LabelingSegmentationObject segment = context.ManualSegments[candidate];
                if (segment == null || context.CanEditManualSegment?.Invoke(segment) == false)
                {
                    continue;
                }

                index = candidate;
                return true;
            }

            index = -1;
            return false;
        }

        private bool TryGetEditableSelectedManualSegmentIndex(out int index)
        {
            index = -1;
            WpfObjectReviewItemRef selectedObject = context.SelectedObjectProvider?.Invoke();
            if (selectedObject?.Source != WpfObjectReviewSource.ManualSegment
                || selectedObject.Index < 0
                || selectedObject.Index >= context.ManualSegments.Count)
            {
                return false;
            }

            LabelingSegmentationObject segment = context.ManualSegments[selectedObject.Index];
            if (segment == null || context.CanEditManualSegment?.Invoke(segment) == false)
            {
                return false;
            }

            index = selectedObject.Index;
            return true;
        }

        private void UpdateCandidateEditState()
        {
            bool available = IsCandidateEditAvailable();
            CanvasPanelViewModel?.SetSmartMaskCandidateEditState(
                available,
                candidateEditBuffer.CanUndo,
                candidateEditBuffer.CanRedo,
                candidateRegionOperation);
            CanvasPanelViewModel?.SetSmartMaskCandidateRegionTargetState(
                available && HasEditableSelectedManualSegment());
        }

        private int GetCandidateBrushRadius()
            => Math.Max(1, context.CandidateBrushRadiusProvider?.Invoke() ?? MaskAnnotationService.DefaultBrushRadius);

        private bool BeginCandidateEditSession(YoloWorkerSmokeCandidate candidate)
        {
            IReadOnlyList<PointF> contour = CandidateReviewPresentationService.GetCandidateContourPoints(candidate, ActiveImageSize);
            if (candidate == null || contour.Count < 3)
            {
                ResetSmartMaskCandidateEditBuffer();
                return false;
            }

            LabelingSegmentationObject source = BuildCandidateSource(candidate, contour, context.EnsureClassItem(candidate.ClassName), ActiveImageSize);
            if (!SegmentationMaskGeometryService.TryRasterize(source, ActiveImageSize, out byte[] sourceMask, out _))
            {
                ResetSmartMaskCandidateEditBuffer();
                return false;
            }

            return EnsureCandidateEditSession(candidate, source, sourceMask);
        }

        private bool EnsureCandidateEditSession(
            YoloWorkerSmokeCandidate candidate,
            LabelingSegmentationObject source,
            byte[] sourceMask)
        {
            string recipeName = GetCurrentSmartMaskRecipeName();
            if (candidateEditBuffer.HasSession
                && candidateEditBuffer.Matches(
                    candidateEditBuffer.CaptureToken(),
                    ActiveImagePath,
                    recipeName,
                    source.ObjectId))
            {
                return true;
            }

            ResetSmartMaskCandidateEditBuffer(clearSelectedObjectOrigin: false);
            return candidateEditBuffer.Begin(
                    ActiveImagePath,
                    recipeName,
                    source.ObjectId,
                    sourceMask,
                    ActiveImageSize,
                    targetIsEditable: true)
                != null;
        }

        private static LabelingSegmentationObject BuildCandidateSource(
            YoloWorkerSmokeCandidate candidate,
            IReadOnlyList<PointF> contour,
            LabelClass classItem,
            Size imageSize)
        {
            List<Point> points = SegmentationGeometry.NormalizePolygon(
                contour.Select(point => new Point((int)Math.Round(point.X), (int)Math.Round(point.Y))),
                imageSize,
                minimumDistance: 1,
                simplificationTolerance: 0D);
            return new LabelingSegmentationObject(points, classItem)
            {
                ClassName = classItem?.Text ?? "Defect",
                ObjectId = GetCandidateEditObjectId(candidate, points)
            };
        }

        private static string GetCandidateEditObjectId(YoloWorkerSmokeCandidate candidate, IReadOnlyList<Point> points)
        {
            int hash = 17;
            foreach (Point point in points ?? Array.Empty<Point>())
            {
                hash = unchecked((hash * 31) + point.X);
                hash = unchecked((hash * 31) + point.Y);
            }

            return $"smart-mask:{candidate?.Index ?? -1}:{hash}";
        }

        private void ApplyCanvasDisplayMode(WpfCanvasDisplayMode mode, bool redraw, bool logChange)
            => context.ApplyCanvasDisplayMode?.Invoke(mode, redraw, logChange);

        private void SelectAnnotationTool(WpfAnnotationTool tool)
            => context.SelectAnnotationTool?.Invoke(tool, false);

        private void RefreshPolygonOverlays()
            => context.RefreshPolygonOverlays?.Invoke();

        private void SetYoloCommandStatus(string text, bool isBusy)
            => context.SetYoloCommandStatus?.Invoke(text, isBusy);

        private void AppendLog(string message)
            => context.AppendLog?.Invoke(message);

        private static void RemoveAtIfPresent<T>(IList<T> items, int index)
        {
            if (items != null && index >= 0 && index < items.Count)
            {
                items.RemoveAt(index);
            }
        }
    }

    internal sealed class SmartMaskWorkflowAdapterContext
    {
        internal Func<LabelingProjectData> DataProvider { get; init; }
        internal Func<string> RecipeNameProvider { get; init; }
        internal CandidateReviewStateService CandidateReviewState { get; init; }
        internal WpfCanvasPanelViewModel CanvasPanelViewModel { get; init; }
        internal RoiImageCanvasViewModel MainCanvasViewModel { get; init; }
        internal Func<bool> IsApplicationCloseApproved { get; init; }
        internal Func<Bitmap> ActiveImageBitmapProvider { get; init; }
        internal Func<Size> ActiveImageSizeProvider { get; init; }
        internal Func<string> ActiveImagePathProvider { get; init; }
        internal Func<WpfAnnotationTool> ActiveAnnotationToolProvider { get; init; }
        internal Func<int> CandidateBrushRadiusProvider { get; init; }
        internal Func<WpfObjectReviewItemRef> SelectedObjectProvider { get; init; }
        internal Func<LabelingSegmentationObject, bool> CanEditManualSegment { get; init; }
        internal List<Rectangle> ManualRois { get; init; }
        internal List<string> ManualRoiClassNames { get; init; }
        internal List<CanvasRoiShapeKind> ManualRoiShapeKinds { get; init; }
        internal List<string> ManualRoiOverlayIds { get; init; }
        internal List<LabelingSegmentationObject> ManualSegments { get; init; }
        internal Func<string, LabelClass> EnsureClassItem { get; init; }
        internal Func<int, string> GetManualRoiClassName { get; init; }
        internal Action EnsureManualRoiMetadataCount { get; init; }
        internal Action<IReadOnlyList<YoloWorkerSmokeCandidate>, bool> ApplyDetectionCandidatesPreservingConfirmed { get; init; }
        internal Action<IReadOnlyList<YoloWorkerSmokeCandidate>, bool> ApplyDetectionCandidatesPreservingConfirmedInCurrentWorkflow { get; init; }
        internal Action<string, bool> RegisterAnnotationHistoryBeforeChange { get; init; }
        internal Action<WpfAnnotationTool, bool> SelectAnnotationTool { get; init; }
        internal Action<WpfCanvasDisplayMode, bool, bool> ApplyCanvasDisplayMode { get; init; }
        internal Action<YoloWorkerSmokeCandidate> RefreshCandidateListWithPreferred { get; init; }
        internal Action RefreshObjectList { get; init; }
        internal Action RefreshPolygonOverlays { get; init; }
        internal Action RefreshCanvasWorkflowContext { get; init; }
        internal Action<bool> QueueActiveImageQueueStatusRefresh { get; init; }
        internal Action<string> AddCandidateReviewHistory { get; init; }
        internal Action<string, bool> SetYoloCommandStatus { get; init; }
        internal Action<string> AppendLog { get; init; }
    }
}
