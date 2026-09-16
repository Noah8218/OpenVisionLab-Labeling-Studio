using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.Canvas;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using OpenVisionLab.ImageCanvas.ViewModels;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;

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
        private readonly SmartMaskWorkflowAdapterContext context;

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
        }

        internal void StartCandidateGeneration()
            => _ = ExecuteCreateSmartMaskCandidateCommandAsync();

        internal async Task ExecuteCreateSmartMaskCandidateCommandAsync()
        {
            if (IsApplicationCloseApproved || workflowService.IsRunning)
            {
                return;
            }

            bool isStartingSession = !promptSession.HasSession;
            string promptOverlayId = string.Empty;
            Rectangle promptBounds = Rectangle.Empty;
            if (isStartingSession)
            {
                int promptIndex = FindSmartMaskPromptIndex();
                if (promptIndex < 0 || ActiveImageSize.IsEmpty || string.IsNullOrWhiteSpace(ActiveImagePath))
                {
                    RefreshSmartMaskCommandState("결함 둘레에 사각형 박스를 먼저 그린 뒤 다시 누르세요.");
                    AppendLog("스마트 마스크: 결함 둘레에 사각형 박스를 먼저 그리세요.");
                    return;
                }

                promptOverlayId = ObjectReviewSelectionService.GetManualRoiOverlayId(
                    context.ManualRoiOverlayIds,
                    promptIndex);
                promptBounds = context.ManualRois[promptIndex];
                string className = GetManualRoiClassName(promptIndex);
                int classIdValue = Data.ClassNamedList.FindIndex(item =>
                    string.Equals(item?.Text, className, StringComparison.OrdinalIgnoreCase));
                int? classId = classIdValue >= 0 ? classIdValue : null;
                promptSession.Start(
                    ActiveImagePath,
                    GetCurrentSmartMaskRecipeName(),
                    promptBounds,
                    classId,
                    className);
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

            RefreshSmartMaskCommandState("MobileSAM이 박스와 보정점을 사용해 후보 경계를 계산하고 있습니다.");
            SetYoloCommandStatus("스마트 마스크 후보 생성 중...", isBusy: true);
            SmartMaskWorkflowResult workflowResult = await workflowService.RunAsync(
                new SmartMaskWorkflowRequest { Prompt = request });

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
                RefreshSmartMaskCommandState("후보 생성을 취소했습니다. 프롬프트는 유지되며 다시 실행할 수 있습니다.");
                SetYoloCommandStatus("스마트 마스크 후보 생성 취소", isBusy: false);
                AppendLog("스마트 마스크 후보 생성을 취소했습니다.");
                return;
            }

            if (workflowResult.Error != null)
            {
                string workflowError = workflowResult.Error.Message;
                RefreshSmartMaskCommandState(workflowError);
                SetYoloCommandStatus("스마트 마스크 실패: " + workflowError, isBusy: false);
                AppendLog("스마트 마스크 실패: " + workflowError);
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
                    ? "Smart Mask 후보가 반환되지 않았습니다."
                    : result.Error;
                RefreshSmartMaskCommandState(resultError);
                SetYoloCommandStatus("스마트 마스크 실패: " + resultError, isBusy: false);
                AppendLog("스마트 마스크 실패: " + resultError);
                return;
            }

            if (isStartingSession)
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
            else
            {
                RegisterAnnotationHistoryBeforeChange("스마트 마스크 후보 다시 생성", markDirty: false);
            }

            // Candidate Review owns the pending AI candidate. The session keeps only
            // the initial/latest alternatives until the operator confirms or skips it.
            context.ApplyDetectionCandidatesPreservingConfirmed?.Invoke(
                new[] { result.Candidate },
                true);
            promptSession.RecordCandidate(result.Candidate);
            RefreshPolygonOverlays();
            SetYoloCommandStatus(result.Summary + " / 확정 전 후보", isBusy: false);
            AppendLog($"{result.Summary} / {result.RuntimeSummary} / mask area {result.MaskArea}");
            RefreshSmartMaskCommandState("자동 후보가 부족할 때 보정 옵션에서 한 점을 추가하고 다시 생성해 비교하세요. 확정 전에는 저장되지 않습니다.");
        }

        internal void ExecuteSetSmartMaskPointModeCommand(WpfSmartMaskPointInputMode mode)
        {
            if (!promptSession.HasSession || workflowService.IsRunning)
            {
                return;
            }

            WpfSmartMaskPointInputMode nextMode = promptSession.InputMode == mode
                ? WpfSmartMaskPointInputMode.None
                : mode;
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
                RefreshSmartMaskCommandState("현재 후보를 먼저 확정하거나 스킵하세요.");
                return;
            }

            ResetSmartMaskPromptSession();
            SelectAnnotationTool(WpfAnnotationTool.Rectangle);
            string nextDetail = CanvasPanelViewModel?.IsSmartMaskAutoContourEnabled == true
                ? "다음 객체를 사각형으로 감싸면 자동 윤곽 후보가 바로 생성됩니다."
                : "다음 객체 둘레에 사각형 박스를 그린 뒤 자동 윤곽 옵션을 켜세요.";
            RefreshSmartMaskCommandState(nextDetail);
            SetYoloCommandStatus("스마트 마스크: 다음 객체 박스를 기다립니다.", isBusy: false);
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

            AppendLog("자동 윤곽: 새 사각형을 MobileSAM 프롬프트로 사용합니다.");
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
            RefreshSmartMaskCommandState($"{resolution} 완료 · 다음 객체를 사각형으로 감싸세요.");
            SetYoloCommandStatus($"자동 윤곽: {resolution} 완료 · 다음 박스 대기", isBusy: false);
        }

        internal void ExecuteSelectSmartMaskCandidateVersionCommand(WpfSmartMaskCandidateVersion version)
        {
            if (workflowService.IsRunning
                || !CandidateReviewState.HasPendingCandidates
                || !promptSession.TrySelectCandidate(version, out YoloWorkerSmokeCandidate candidate))
            {
                RefreshSmartMaskCommandState("비교할 Smart Mask 후보가 없습니다.");
                return;
            }

            CandidateReviewState.LoadPendingCandidates(new[] { candidate }, clearConfirmed: false);
            ApplyCanvasDisplayMode(WpfCanvasDisplayMode.InferenceOnly, redraw: false, logChange: false);
            context.RefreshCandidateListWithPreferred?.Invoke(candidate);
            context.RefreshObjectList?.Invoke();
            RefreshPolygonOverlays();
            context.RefreshCanvasWorkflowContext?.Invoke();
            string versionText = version == WpfSmartMaskCandidateVersion.Initial ? "이전" : "현재";
            context.AddCandidateReviewHistory?.Invoke($"Smart Mask {versionText} 후보 보기 · 확정 전");
            SetYoloCommandStatus($"Smart Mask {versionText} 후보 선택 / 확정 전", isBusy: false);
            AppendLog($"Smart Mask {versionText} 후보로 전환했습니다. 확정 전에는 저장되지 않습니다.");
            RefreshSmartMaskCommandState($"{versionText} 후보를 보고 있습니다. 확정하면 이 후보만 저장됩니다.");
        }

        internal bool TryApplySmartMaskPointInput(CanvasImagePointEventArgs e)
        {
            if (e == null
                || !promptSession.HasSession
                || promptSession.InputMode == WpfSmartMaskPointInputMode.None)
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

            if (!promptSession.TryAddPoint(e.ImagePoint, ActiveImageSize))
            {
                return true;
            }

            RefreshPolygonOverlays();
            RefreshSmartMaskCommandState("보정점을 추가했습니다. 후보 다시 생성으로 효과를 비교한 뒤 부족할 때 다음 점을 추가하세요.");
            return true;
        }

        internal void ResetSmartMaskPromptSession()
        {
            workflowService.Cancel();
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

            bool isVisible = Data.ProjectSettings?.DatasetPurpose == LabelingDatasetPurpose.Segmentation;
            if (promptSession.HasSession
                && !promptSession.MatchesContext(ActiveImagePath, GetCurrentSmartMaskRecipeName()))
            {
                ResetSmartMaskPromptSession();
            }

            bool hasSession = isVisible && promptSession.HasSession;
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
                    : promptIndex < 0
                        ? "결함 둘레에 사각형 박스를 그리면 MobileSAM 후보 마스크를 만들 수 있습니다."
                        : "마지막 사각형을 시작 박스로 사용합니다. 결과는 확정 전 후보로만 표시됩니다.";
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
                promptSession.SelectedCandidateVersion);
        }

        private string GetManualRoiClassName(int index)
            => context.GetManualRoiClassName?.Invoke(index)
                ?? ObjectReviewPresentationService.GetManualRoiClassName(context.ManualRoiClassNames, index);

        private void RegisterAnnotationHistoryBeforeChange(string actionName, bool markDirty)
            => context.RegisterAnnotationHistoryBeforeChange?.Invoke(actionName, markDirty);

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
        internal List<Rectangle> ManualRois { get; init; }
        internal List<string> ManualRoiClassNames { get; init; }
        internal List<CanvasRoiShapeKind> ManualRoiShapeKinds { get; init; }
        internal List<string> ManualRoiOverlayIds { get; init; }
        internal Func<int, string> GetManualRoiClassName { get; init; }
        internal Action EnsureManualRoiMetadataCount { get; init; }
        internal Action<IReadOnlyList<YoloWorkerSmokeCandidate>, bool> ApplyDetectionCandidatesPreservingConfirmed { get; init; }
        internal Action<string, bool> RegisterAnnotationHistoryBeforeChange { get; init; }
        internal Action<WpfAnnotationTool, bool> SelectAnnotationTool { get; init; }
        internal Action<WpfCanvasDisplayMode, bool, bool> ApplyCanvasDisplayMode { get; init; }
        internal Action<YoloWorkerSmokeCandidate> RefreshCandidateListWithPreferred { get; init; }
        internal Action RefreshObjectList { get; init; }
        internal Action RefreshPolygonOverlays { get; init; }
        internal Action RefreshCanvasWorkflowContext { get; init; }
        internal Action<string> AddCandidateReviewHistory { get; init; }
        internal Action<string, bool> SetYoloCommandStatus { get; init; }
        internal Action<string> AppendLog { get; init; }
    }
}
