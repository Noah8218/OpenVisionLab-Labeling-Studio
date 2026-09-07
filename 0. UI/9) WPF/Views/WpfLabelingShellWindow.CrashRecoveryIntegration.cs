using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using OpenVisionLab.ImageCanvas.CanvasShapes;
using OpenVisionLab.Wpf.MessageDialogs;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MvcVisionSystem
{
    // Responsibility group: crash recovery window integration.
    // These members remain WPF Window adapters; independent policy belongs in services.
    public partial class WpfLabelingShellWindow
    {
        #region CrashRecovery
        private bool TryHandleCrashRecoveryOnStartup()
        {
            WpfCrashRecoveryReadResult result = crashRecoveryJournalService.ReadAvailable(
                GetCurrentRecipeName(),
                global.Data?.OutputRootPath);
            if (result.Status == WpfCrashRecoveryReadStatus.None)
            {
                return false;
            }

            if (result.Status == WpfCrashRecoveryReadStatus.Invalid)
            {
                string quarantineDetail = string.IsNullOrWhiteSpace(result.QuarantinePath)
                    ? "손상된 초안은 제거되었습니다."
                    : $"검토용 격리 위치: {result.QuarantinePath}";
                AppendLog($"비정상 종료 복구 초안을 사용할 수 없습니다: {result.Error} {quarantineDetail}");
                return false;
            }

            WpfMessageDialogResult decision = ShowCrashRecoveryPrompt(result.Draft);
            if (decision != WpfMessageDialogResult.Yes)
            {
                DiscardCrashRecoveryJournal();
                AppendLog("비정상 종료 복구 초안을 폐기했습니다.");
                return false;
            }

            if (!TryRestoreCrashRecoveryDraft(result.Draft))
            {
                WpfMessageDialog.Show(this, new WpfMessageDialogOptions
                {
                    Title = "편집 초안을 복구하지 못했습니다",
                    Message = "원본 이미지 또는 현재 Recipe 상태를 확인한 뒤 다시 시작해 주세요.",
                    Details = "복구 초안은 안전을 위해 격리되거나 유지되지 않습니다. 저장된 라벨 파일은 변경하지 않았습니다.",
                    Kind = WpfMessageDialogKind.Warning,
                    Buttons = WpfMessageDialogButtons.OK,
                    PrimaryButtonText = "확인"
                });
                DiscardCrashRecoveryJournal();
                return false;
            }

            return true;
        }

        private WpfMessageDialogResult ShowCrashRecoveryPrompt(WpfCrashRecoveryDraft draft)
        {
            string imageName = Path.GetFileName(draft?.ImagePath ?? string.Empty);
            string localTime = draft == null
                ? "-"
                : draft.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);
            return WpfMessageDialog.Show(this, new WpfMessageDialogOptions
            {
                Title = "비정상 종료 편집 복구",
                Message = "저장되지 않은 현재 이미지 편집 초안을 발견했습니다.",
                Details =
                    $"Recipe: {draft?.RecipeName}\n" +
                    $"이미지: {imageName}\n" +
                    $"초안 시각: {localTime}\n" +
                    $"편집 사유: {draft?.DirtyReason}\n" +
                    $"객체: 박스 {draft?.Boxes?.Count ?? 0}개, 세그멘테이션 {draft?.Segments?.Count ?? 0}개\n\n" +
                    "복구하면 화면의 미저장 편집 상태로만 돌아옵니다. " +
                    "AI 후보를 승인하거나 라벨 파일을 저장하지 않습니다. 검토 후 `라벨 저장`을 눌러야 합니다.",
                Kind = WpfMessageDialogKind.Warning,
                Buttons = WpfMessageDialogButtons.YesNo,
                DefaultResult = WpfMessageDialogResult.No,
                PrimaryButtonText = "편집 복구",
                SecondaryButtonText = "초안 폐기",
                MaxWidth = 660D
            });
        }

        private bool TryRestoreCrashRecoveryDraft(WpfCrashRecoveryDraft draft)
        {
            if (draft == null || string.IsNullOrWhiteSpace(draft.ImagePath))
            {
                return false;
            }

            suppressCrashRecoveryJournal = true;
            try
            {
                if (!TryLoadImage(draft.ImagePath, populateQueue: true))
                {
                    return false;
                }

                WpfCrashRecoveryRestorePlan restorePlan =
                    crashRecoverySessionService.BuildRestorePlan(draft);
                PrepareCrashRecoveryRestoreState();
                RestoreCrashRecoveryBoxes(restorePlan.Boxes);
                RestoreCrashRecoverySegments(restorePlan.Segments);
                RefreshCrashRecoveryRestorePresentation();
            }
            catch (Exception ex)
            {
                AppendLog($"비정상 종료 편집 복구 실패: {ex.Message}");
                return false;
            }
            finally
            {
                suppressCrashRecoveryJournal = false;
            }

            MarkAnnotationsDirty("비정상 종료 편집 복구");
            SetYoloCommandStatus("편집 초안 복구 완료 · 검토 후 라벨 저장이 필요합니다.", isBusy: false);
            AppendLog("비정상 종료 편집 초안을 미저장 상태로 복구했습니다. AI 후보 승인이나 라벨 저장은 실행하지 않았습니다.");
            return true;
        }

        private void PrepareCrashRecoveryRestoreState()
        {
            WpfAnnotationHistorySnapshot savedState = CaptureAnnotationHistory("비정상 종료 복구");
            ClearAnnotationHistory();
            PushAnnotationHistorySnapshot(savedState, markDirty: false);

            manualRois.Clear();
            manualRoiClassNames.Clear();
            manualRoiShapeKinds.Clear();
            manualRoiOverlayIds.Clear();
            manualSegments.Clear();
            objectMetadataStateService.Clear();
            candidateReviewState.ClearAll();
            smartMaskPromptSession.Reset();
        }

        private void RestoreCrashRecoveryBoxes(IReadOnlyList<WpfCrashRecoveryBox> boxes)
        {
            foreach (WpfCrashRecoveryBox box in boxes)
            {
                manualRois.Add(new Rectangle(box.X, box.Y, box.Width, box.Height));
                manualRoiClassNames.Add(box.ClassName);
                manualRoiShapeKinds.Add(
                    Enum.TryParse(box.ShapeKind, ignoreCase: true, out CanvasRoiShapeKind shapeKind)
                        ? shapeKind
                        : CanvasRoiShapeKind.Rectangle);
                manualRoiOverlayIds.Add(string.Empty);
                objectMetadataStateService.SetManualRoiMetadata(
                    manualRois.Count - 1,
                    CrashRecoverySessionService.ToPersistentMetadata(box.Metadata));
            }
        }

        private void RestoreCrashRecoverySegments(IReadOnlyList<WpfCrashRecoverySegment> segments)
        {
            foreach (WpfCrashRecoverySegment source in segments)
            {
                LabelClass classItem = classCatalogWorkflowService.EnsureClassItem(global.Data, source.ClassName);
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
                manualSegments.Add(segment);
                objectMetadataStateService.SetManualSegmentMetadata(
                    segment,
                    CrashRecoverySessionService.ToPersistentMetadata(source.Metadata));
            }
        }

        private void RefreshCrashRecoveryRestorePresentation()
        {
            objectMetadataStateService.DissolveInvalidGroups(manualRois.Count, manualSegments);
            RedrawReviewRois();
            RefreshPolygonOverlays();
            RefreshObjectList();
            RefreshCandidateList();
            PopulateClassList();
            UpdateDetectionResultOverlay();
            RefreshActiveImageQueueStatus(hasActiveCandidates: false);
            SetPythonStatus("추론: 대기 0 / 확정 0");
        }

        private void ScheduleCrashRecoveryJournalWrite()
        {
            if (isApplicationCloseApproved
                || suppressCrashRecoveryJournal
                || activeImageBitmap == null
                || activeImageSize.IsEmpty
                || string.IsNullOrWhiteSpace(activeImagePath)
                || !annotationDirtyState.IsDirty)
            {
                return;
            }

            int captureVersion = ++crashRecoveryCaptureVersion;
            Dispatcher.BeginInvoke(
                new Action(() => ApplyScheduledCrashRecoveryJournalWrite(captureVersion)),
                System.Windows.Threading.DispatcherPriority.ContextIdle);
        }

        private void ApplyScheduledCrashRecoveryJournalWrite(int captureVersion)
        {
            if (isApplicationCloseApproved
                || captureVersion != crashRecoveryCaptureVersion
                || suppressCrashRecoveryJournal
                || !annotationDirtyState.IsDirty
                || HasPendingMaskStrokeCommitWork())
            {
                return;
            }

            WpfCrashRecoveryDraft draft;
            try
            {
                draft = CaptureCrashRecoveryDraft();
            }
            catch (Exception ex)
            {
                AppendLog($"비정상 종료 복구 초안 캡처 실패: {ex.Message}");
                return;
            }

            crashRecoveryJournalWriteCoordinator.QueueWrite(draft);
        }

        private WpfCrashRecoveryDraft CaptureCrashRecoveryDraft()
        {
            List<WpfCrashRecoveryRoiSnapshot> roiSnapshots = CaptureCrashRecoveryRoiSnapshots();
            List<WpfCrashRecoveryCandidateSnapshot> candidateSnapshots = CaptureCrashRecoveryCandidateSnapshots();
            List<WpfCrashRecoverySegmentSnapshot> segmentSnapshots = CaptureCrashRecoverySegmentSnapshots();

            return crashRecoverySessionService.Capture(new WpfCrashRecoveryCaptureRequest(
                GetType().Assembly.GetName().Version?.ToString() ?? string.Empty,
                GetCurrentRecipeName(),
                global.Data.OutputRootPath,
                activeImagePath,
                activeImageSize,
                annotationDirtyState.Reason,
                roiSnapshots,
                candidateSnapshots,
                segmentSnapshots));
        }

        private List<WpfCrashRecoveryRoiSnapshot> CaptureCrashRecoveryRoiSnapshots()
        {
            var manualRoiSnapshots = new List<WpfCrashRecoveryRoiSnapshot>();
            for (int index = 0; index < manualRois.Count; index++)
            {
                Rectangle bounds = manualRois[index];
                if (bounds.IsEmpty)
                {
                    continue;
                }

                manualRoiSnapshots.Add(new WpfCrashRecoveryRoiSnapshot(
                    bounds,
                    GetManualRoiClassName(index),
                    index < manualRoiShapeKinds.Count
                        ? manualRoiShapeKinds[index]
                        : CanvasRoiShapeKind.Rectangle,
                    objectMetadataStateService.GetManualRoiMetadata(index)));
            }

            return manualRoiSnapshots;
        }

        private List<WpfCrashRecoveryCandidateSnapshot> CaptureCrashRecoveryCandidateSnapshots()
        {
            var confirmedCandidates = new List<WpfCrashRecoveryCandidateSnapshot>();
            foreach (YoloWorkerSmokeCandidate candidate in confirmedDetectionCandidates)
            {
                Rectangle bounds = CandidateReviewPresentationService.ClipCandidateBounds(candidate, activeImageSize);
                confirmedCandidates.Add(new WpfCrashRecoveryCandidateSnapshot(
                    candidate?.ClassName,
                    bounds,
                    candidate?.PolygonPoints?.Count >= 3));
            }

            return confirmedCandidates;
        }

        private List<WpfCrashRecoverySegmentSnapshot> CaptureCrashRecoverySegmentSnapshots()
        {
            Dictionary<string, List<LabelingSegmentationObject>> segmentsByClass = BuildAnnotationSegments();
            var segments = new List<WpfCrashRecoverySegmentSnapshot>();
            foreach (LabelingSegmentationObject segment in segmentsByClass
                .Values
                .Where(items => items != null)
                .SelectMany(items => items)
                .Where(item => item != null))
            {
                WpfPersistentObjectMetadata metadata = manualSegments.Contains(segment)
                    ? objectMetadataStateService.GetManualSegmentMetadata(segment)
                    : WpfPersistentObjectMetadata.Default;
                segments.Add(new WpfCrashRecoverySegmentSnapshot(segment, metadata));
            }

            return segments;
        }

        private void DiscardCrashRecoveryJournal()
        {
            crashRecoveryCaptureVersion++;
            crashRecoveryJournalWriteCoordinator.Discard();
        }
        #endregion

    }
}
