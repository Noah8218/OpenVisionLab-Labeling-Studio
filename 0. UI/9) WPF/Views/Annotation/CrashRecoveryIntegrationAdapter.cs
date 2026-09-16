using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using OpenVisionLab.Wpf.MessageDialogs;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace MvcVisionSystem
{
    /// <summary>
    /// Owns the WPF integration around the crash-recovery journal.
    /// Journal sequencing remains in CrashRecoveryJournalWorkflowService;
    /// this adapter only presents failures and applies an existing restore plan
    /// to the Shell's explicitly supplied mutable state.
    /// </summary>
    internal sealed class CrashRecoveryIntegrationAdapter
    {
        #region Fields

        private readonly CrashRecoveryIntegrationAdapterContext context;

        #endregion

        #region Constructors

        internal CrashRecoveryIntegrationAdapter(CrashRecoveryIntegrationAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            if (context.JournalService == null) throw new ArgumentNullException(nameof(context.JournalService));
            if (context.JournalWorkflowService == null) throw new ArgumentNullException(nameof(context.JournalWorkflowService));
            if (context.SessionService == null) throw new ArgumentNullException(nameof(context.SessionService));
            if (context.SnapshotAdapter == null) throw new ArgumentNullException(nameof(context.SnapshotAdapter));
            if (context.DataProvider == null) throw new ArgumentNullException(nameof(context.DataProvider));
            if (context.CurrentRecipeNameProvider == null) throw new ArgumentNullException(nameof(context.CurrentRecipeNameProvider));
            if (context.IsApplicationCloseApproved == null) throw new ArgumentNullException(nameof(context.IsApplicationCloseApproved));
            if (context.HasActiveImage == null) throw new ArgumentNullException(nameof(context.HasActiveImage));
            if (context.HasDirtyAnnotations == null) throw new ArgumentNullException(nameof(context.HasDirtyAnnotations));
            if (context.HasPendingCommitWork == null) throw new ArgumentNullException(nameof(context.HasPendingCommitWork));
            if (context.TryLoadImage == null) throw new ArgumentNullException(nameof(context.TryLoadImage));
            if (context.ShowCrashRecoveryPrompt == null) throw new ArgumentNullException(nameof(context.ShowCrashRecoveryPrompt));
            if (context.ShowRestoreFailureDialog == null) throw new ArgumentNullException(nameof(context.ShowRestoreFailureDialog));
            if (context.AppendLog == null) throw new ArgumentNullException(nameof(context.AppendLog));
            if (context.RunOnUiThread == null) throw new ArgumentNullException(nameof(context.RunOnUiThread));
            if (context.SetAnnotationSaveStatus == null) throw new ArgumentNullException(nameof(context.SetAnnotationSaveStatus));
            if (context.CaptureAnnotationHistory == null) throw new ArgumentNullException(nameof(context.CaptureAnnotationHistory));
            if (context.ClearAnnotationHistory == null) throw new ArgumentNullException(nameof(context.ClearAnnotationHistory));
            if (context.PushAnnotationHistorySnapshot == null) throw new ArgumentNullException(nameof(context.PushAnnotationHistorySnapshot));
            if (context.MarkAnnotationsDirty == null) throw new ArgumentNullException(nameof(context.MarkAnnotationsDirty));
            if (context.RefreshRestorePresentation == null) throw new ArgumentNullException(nameof(context.RefreshRestorePresentation));
            if (context.SetYoloCommandStatus == null) throw new ArgumentNullException(nameof(context.SetYoloCommandStatus));
            if (context.SetPythonStatus == null) throw new ArgumentNullException(nameof(context.SetPythonStatus));
            if (context.RefreshActiveImageQueueStatus == null) throw new ArgumentNullException(nameof(context.RefreshActiveImageQueueStatus));
            if (context.ClassCatalogWorkflowService == null) throw new ArgumentNullException(nameof(context.ClassCatalogWorkflowService));
            if (context.ObjectMetadataStateService == null) throw new ArgumentNullException(nameof(context.ObjectMetadataStateService));
            if (context.CandidateReviewStateService == null) throw new ArgumentNullException(nameof(context.CandidateReviewStateService));
            if (context.SmartMaskPromptSessionService == null) throw new ArgumentNullException(nameof(context.SmartMaskPromptSessionService));
            if (context.ManualRois == null) throw new ArgumentNullException(nameof(context.ManualRois));
            if (context.ManualRoiClassNames == null) throw new ArgumentNullException(nameof(context.ManualRoiClassNames));
            if (context.ManualRoiShapeKinds == null) throw new ArgumentNullException(nameof(context.ManualRoiShapeKinds));
            if (context.ManualRoiOverlayIds == null) throw new ArgumentNullException(nameof(context.ManualRoiOverlayIds));
            if (context.ManualSegments == null) throw new ArgumentNullException(nameof(context.ManualSegments));
        }

        #endregion

        #region Journal failure and startup

        internal void OnCrashRecoveryJournalWriteFailed(
            object sender,
            CrashRecoveryJournalWriteFailedEventArgs failure)
        {
            context.RunOnUiThread(() =>
            {
                if (context.IsApplicationCloseApproved())
                {
                    return;
                }

                string detail = $"복구 초안 저장 실패 (revision {failure.Revision}): {failure.Message}";
                context.AppendLog(detail);
                context.SetAnnotationSaveStatus(
                    context.HasDirtyAnnotations(),
                    "복구 초안 저장 실패",
                    detail);
            });
        }

        internal void OnCrashRecoveryJournalCaptureFailed(
            object sender,
            CrashRecoveryJournalCaptureFailedEventArgs failure)
        {
            if (context.IsApplicationCloseApproved())
            {
                return;
            }

            context.AppendLog($"비정상 종료 복구 초안 캡처 실패: {failure.Message}");
        }

        internal bool TryHandleCrashRecoveryOnStartup()
        {
            WpfCrashRecoveryReadResult result = context.JournalService.ReadAvailable(
                context.CurrentRecipeNameProvider(),
                context.DataProvider()?.OutputRootPath,
                expectedClassOrderSha256: RecipeDatasetVersionService.ComputeClassContractSha256(
                    context.DataProvider()?.ClassNamedList?
                        .Select(item => item?.Text?.Trim())
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .ToList() ?? new List<string>()));
            if (result.Status == WpfCrashRecoveryReadStatus.None)
            {
                return false;
            }

            if (result.Status == WpfCrashRecoveryReadStatus.Invalid)
            {
                string quarantineDetail = string.IsNullOrWhiteSpace(result.QuarantinePath)
                    ? "손상된 초안은 제거되었습니다."
                    : $"검토용 격리 위치: {result.QuarantinePath}";
                context.AppendLog($"비정상 종료 복구 초안을 사용할 수 없습니다: {result.Error} {quarantineDetail}");
                return false;
            }

            WpfMessageDialogResult decision = ShowCrashRecoveryPrompt(result.Draft);
            if (decision != WpfMessageDialogResult.Yes)
            {
                DiscardCrashRecoveryJournal();
                context.AppendLog("비정상 종료 복구 초안을 폐기했습니다.");
                return false;
            }

            if (!TryRestoreCrashRecoveryDraft(result.Draft))
            {
                context.ShowRestoreFailureDialog();
                DiscardCrashRecoveryJournal();
                return false;
            }

            return true;
        }

        internal WpfMessageDialogResult ShowCrashRecoveryPrompt(WpfCrashRecoveryDraft draft)
            => context.ShowCrashRecoveryPrompt(draft);

        #endregion

        #region Restore

        internal bool TryRestoreCrashRecoveryDraft(WpfCrashRecoveryDraft draft)
        {
            if (draft == null || string.IsNullOrWhiteSpace(draft.ImagePath))
            {
                return false;
            }

            using (context.JournalWorkflowService.SuppressCapture())
            {
                try
                {
                    if (!context.TryLoadImage(draft.ImagePath))
                    {
                        return false;
                    }

                    WpfCrashRecoveryRestorePlan restorePlan =
                        context.SessionService.BuildRestorePlan(draft);
                    PrepareCrashRecoveryRestoreState();
                    RestoreCrashRecoveryBoxes(restorePlan.Boxes);
                    RestoreCrashRecoverySegments(restorePlan.Segments);
                    context.RefreshRestorePresentation();
                }
                catch (Exception ex)
                {
                    context.AppendLog($"비정상 종료 편집 복구 실패: {ex.Message}");
                    return false;
                }
            }

            context.MarkAnnotationsDirty("비정상 종료 편집 복구");
            context.SetYoloCommandStatus("편집 초안 복구 완료 · 검토 후 라벨 저장이 필요합니다.", false);
            context.AppendLog("비정상 종료 편집 초안을 미저장 상태로 복구했습니다. AI 후보 승인이나 라벨 저장은 실행하지 않았습니다.");
            return true;
        }

        private void PrepareCrashRecoveryRestoreState()
        {
            WpfAnnotationHistorySnapshot savedState = context.CaptureAnnotationHistory("비정상 종료 복구");
            context.ClearAnnotationHistory();
            context.PushAnnotationHistorySnapshot(savedState, false);

            context.ManualRois.Clear();
            context.ManualRoiClassNames.Clear();
            context.ManualRoiShapeKinds.Clear();
            context.ManualRoiOverlayIds.Clear();
            context.ManualSegments.Clear();
            context.ObjectMetadataStateService.Clear();
            context.CandidateReviewStateService.ClearAll();
            context.SmartMaskPromptSessionService.Reset();
        }

        private void RestoreCrashRecoveryBoxes(IReadOnlyList<WpfCrashRecoveryBox> boxes)
        {
            foreach (WpfCrashRecoveryBox box in boxes)
            {
                context.ManualRois.Add(new Rectangle(box.X, box.Y, box.Width, box.Height));
                context.ManualRoiClassNames.Add(box.ClassName);
                context.ManualRoiShapeKinds.Add(
                    Enum.TryParse(box.ShapeKind, ignoreCase: true, out CanvasRoiShapeKind shapeKind)
                        ? shapeKind
                        : CanvasRoiShapeKind.Rectangle);
                context.ManualRoiOverlayIds.Add(string.Empty);
                context.ObjectMetadataStateService.SetManualRoiMetadata(
                    context.ManualRois.Count - 1,
                    CrashRecoverySessionService.ToPersistentMetadata(box.Metadata));
            }
        }

        private void RestoreCrashRecoverySegments(IReadOnlyList<WpfCrashRecoverySegment> segments)
        {
            foreach (WpfCrashRecoverySegment source in segments)
            {
                LabelClass classItem = context.ClassCatalogWorkflowService.EnsureClassItem(
                    context.DataProvider(),
                    source.ClassName);
                var segment = new LabelingSegmentationObject
                {
                    ClassName = classItem?.Text ?? source.ClassName,
                    ClassItem = classItem,
                    ObjectId = source.ObjectId ?? string.Empty,
                    ComponentIndex = source.ComponentIndex,
                    ZOrder = source.ZOrder,
                    LastStructuralOperation = source.LastStructuralOperation ?? string.Empty,
                    Points = (source.Points ?? new List<WpfCrashRecoveryPoint>())
                        .Select(point => new Point(point.X, point.Y))
                        .ToList(),
                    CutoutPolygons = (source.CutoutPolygons
                        ?? new List<List<WpfCrashRecoveryPoint>>())
                        .Select(cutout => (cutout ?? new List<WpfCrashRecoveryPoint>())
                            .Select(point => new Point(point.X, point.Y))
                            .ToList())
                        .ToList(),
                    MaskData = source.MaskData?.ToArray(),
                    MaskSize = new Size(source.MaskWidth, source.MaskHeight),
                    MaskBounds = new Rectangle(
                        source.MaskBoundsX,
                        source.MaskBoundsY,
                        source.MaskBoundsWidth,
                        source.MaskBoundsHeight),
                    RenderVersion = source.MaskData?.Length > 0 ? 1 : 0,
                    RenderDirtyBounds = source.MaskData?.Length > 0
                        ? new Rectangle(0, 0, source.MaskWidth, source.MaskHeight)
                        : Rectangle.Empty
                };
                context.ManualSegments.Add(segment);
                context.ObjectMetadataStateService.SetManualSegmentMetadata(
                    segment,
                    CrashRecoverySessionService.ToPersistentMetadata(source.Metadata));
            }
        }

        #endregion

        #region Journal lifecycle

        internal bool ScheduleCrashRecoveryJournalWrite()
            => context.JournalWorkflowService.ScheduleWrite(
                hasActiveImage: context.HasActiveImage(),
                hasDirtyAnnotations: context.HasDirtyAnnotations(),
                hasPendingCommitWork: context.HasPendingCommitWork,
                captureDraft: context.SnapshotAdapter.CaptureDraft);

        internal WpfCrashRecoveryDraft CaptureCrashRecoveryDraft()
            => context.SnapshotAdapter.CaptureDraft();

        internal void DiscardCrashRecoveryJournal()
            => context.JournalWorkflowService.Discard();

        #endregion
    }

    internal sealed class CrashRecoveryIntegrationAdapterContext
    {
        internal CrashRecoveryJournalService JournalService { get; init; }
        internal CrashRecoveryJournalWorkflowService JournalWorkflowService { get; init; }
        internal CrashRecoverySessionService SessionService { get; init; }
        internal CrashRecoverySnapshotAdapter SnapshotAdapter { get; init; }
        internal Func<LabelingProjectData> DataProvider { get; init; }
        internal Func<string> CurrentRecipeNameProvider { get; init; }
        internal Func<bool> IsApplicationCloseApproved { get; init; }
        internal Func<bool> HasActiveImage { get; init; }
        internal Func<bool> HasDirtyAnnotations { get; init; }
        internal Func<bool> HasPendingCommitWork { get; init; }
        internal Func<string, bool> TryLoadImage { get; init; }
        internal Func<WpfCrashRecoveryDraft, WpfMessageDialogResult> ShowCrashRecoveryPrompt { get; init; }
        internal Action ShowRestoreFailureDialog { get; init; }
        internal Action<string> AppendLog { get; init; }
        internal Action<Action> RunOnUiThread { get; init; }
        internal Action<bool, string, string> SetAnnotationSaveStatus { get; init; }
        internal Func<string, WpfAnnotationHistorySnapshot> CaptureAnnotationHistory { get; init; }
        internal Action ClearAnnotationHistory { get; init; }
        internal Action<WpfAnnotationHistorySnapshot, bool> PushAnnotationHistorySnapshot { get; init; }
        internal Action<string> MarkAnnotationsDirty { get; init; }
        internal Action RefreshRestorePresentation { get; init; }
        internal Action<string, bool> SetYoloCommandStatus { get; init; }
        internal Action<string> SetPythonStatus { get; init; }
        internal Action<bool> RefreshActiveImageQueueStatus { get; init; }
        internal ClassCatalogWorkflowService ClassCatalogWorkflowService { get; init; }
        internal ObjectMetadataStateService ObjectMetadataStateService { get; init; }
        internal CandidateReviewStateService CandidateReviewStateService { get; init; }
        internal SmartMaskPromptSessionService SmartMaskPromptSessionService { get; init; }
        internal IList<Rectangle> ManualRois { get; init; }
        internal IList<string> ManualRoiClassNames { get; init; }
        internal IList<CanvasRoiShapeKind> ManualRoiShapeKinds { get; init; }
        internal IList<string> ManualRoiOverlayIds { get; init; }
        internal IList<LabelingSegmentationObject> ManualSegments { get; init; }
    }
}
