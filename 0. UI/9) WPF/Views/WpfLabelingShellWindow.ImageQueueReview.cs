using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;

namespace MvcVisionSystem
{
    // Responsibility group: image queue review status and presentation.
    // These members remain WPF Window adapters; independent policy belongs in services.
    public partial class WpfLabelingShellWindow
    {
        #region ImageQueuePresentation
        // Queue presentation helpers stay isolated from queue loading so status-text changes do not hide data-flow changes.
        private void SelectImageQueueItem(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return;
            }

            WpfImageQueueItem item = FindImageQueueItem(imagePath);
            if (item == null)
            {
                return;
            }

            suppressImageQueueSelection = true;
            try
            {
                ImageQueueGrid.SelectedItem = item;
                if (ImageQueueGrid.Columns.Count > 0)
                {
                    ImageQueueGrid.CurrentCell = new DataGridCellInfo(item, ImageQueueGrid.Columns[0]);
                }

                ImageQueueGrid.ScrollIntoView(item);
            }
            finally
            {
                suppressImageQueueSelection = false;
            }

            UpdateSelectedQueueImageButton(item);
        }

        private WpfImageQueueItem FindImageQueueItem(string imagePath)
        {
            if (!string.IsNullOrWhiteSpace(imagePath)
                && imageQueueItemsByPath.TryGetValue(imagePath, out WpfImageQueueItem indexedItem))
            {
                return indexedItem;
            }

            return imageQueueSelectionService.FindItem(imageQueueItems, imagePath);
        }

        private bool ShouldShowImageQueueItem(WpfImageQueueItem item)
        {
            return ImageQueueFilterService.ShouldShow(item, ImageQueueSearchBox?.Text, GetSelectedImageQueueFilter());
        }

        private void UpdateImageQueueStatusText(int loadedCount = -1, int totalToLoad = -1)
        {
            WpfImageQueueFilter selectedFilter = GetSelectedImageQueueFilter();
            ImageQueueSummary summary = ImageQueueFilterService.Summarize(imageQueueItems);
            bool hasSearch = !string.IsNullOrWhiteSpace(ImageQueueSearchBox?.Text);
            int visibleCount = !hasSearch
                ? ImageQueueFilterService.CountByFilter(summary, selectedFilter)
                : imageQueueView?.Cast<object>().Count() ?? summary.TotalCount;
            UpdateQueueQuickFilterButtons(summary, selectedFilter);
            SetDatasetStatus(ImageQueueFilterService.BuildDatasetStatusTextWithActiveImage(
                summary,
                visibleCount,
                selectedFilter,
                loadedCount,
                totalToLoad,
                activeImagePath));
        }

        private void UpdateQueueQuickFilterButtons()
        {
            UpdateQueueQuickFilterButtons(
                ImageQueueFilterService.Summarize(imageQueueItems),
                GetSelectedImageQueueFilter());
        }

        private void UpdateQueueQuickFilterButtons(ImageQueueSummary summary, WpfImageQueueFilter filter)
        {
            summary ??= new ImageQueueSummary();
            ImageQueueViewModel.SetQuickFilterState(
                filter,
                summary.CandidateCount,
                summary.FailedCount,
                summary.ConfirmedCount,
                summary.SkippedCount,
                summary.NoCandidateCount,
                summary.WorklistCount);
        }

        private WpfImageQueueFilter GetSelectedImageQueueFilter()
        {
            return (ImageQueueFilterBox?.SelectedItem as WpfImageQueueFilterOption)?.Filter
                ?? WpfImageQueueFilter.All;
        }

        #endregion

        #region ImageQueueReviewStatus
        // Review-state persistence runs outside the immediate delete/selection hot path and marshals only the latest result back to WPF.
        private void RefreshActiveImageQueueStatus(bool hasActiveCandidates)
        {
            if (string.IsNullOrWhiteSpace(activeImagePath) || activeImageSize.IsEmpty)
            {
                return;
            }

            WpfImageQueueItem item = FindImageQueueItem(activeImagePath);
            if (IsAnomalyDatasetPurpose())
            {
                WpfImageQueuePresenter.ApplyAnomalyReviewStatusToItem(item, anomalyImageReviewWorkflowService.GetOrCreate(activeImagePath));
                RefreshImageQueueViewAfterItemStateChange();
                UpdateImageQueueStatusText();
                return;
            }
            YoloImageReviewStatus status = imageQualityReviewWorkflowService.RefreshLabelStatusAndReviewState(
                activeImagePath,
                activeImageSize,
                global.Data,
                hasActiveCandidates);
            imageQualityReviewWorkflowService.SaveReviewStatus(global.Data);
            ApplyReviewStatusToItem(item, status);
            RefreshImageQueueViewAfterItemStateChange();
            UpdateImageQueueStatusText();
        }

        private void QueueActiveImageQueueStatusRefresh(bool hasActiveCandidates)
        {
            if (isApplicationCloseApproved
                || string.IsNullOrWhiteSpace(activeImagePath)
                || activeImageSize.IsEmpty)
            {
                return;
            }

            if (IsAnomalyDatasetPurpose())
            {
                WpfImageQueuePresenter.ApplyAnomalyReviewStatusToItem(
                    FindImageQueueItem(activeImagePath),
                    anomalyImageReviewWorkflowService.GetOrCreate(activeImagePath));
                RefreshImageQueueViewAfterItemStateChange();
                UpdateImageQueueStatusText();
                return;
            }

            ImageQueueReviewStatusRefreshOperation operation = imageQueueReviewStatusRefreshCoordinator.Queue(
                activeImagePath,
                activeImageSize,
                imageQualityReviewWorkflowService,
                global.Data,
                hasActiveCandidates);
            if (operation == null)
            {
                return;
            }

            // Delete must feel immediate. Label-file recount and review-state JSON writes are
            // background bookkeeping; only the latest completed result returns to the UI thread.
            operation.Completion.ContinueWith(
                    _ => ApplyQueuedActiveImageQueueStatusRefresh(operation),
                    TaskScheduler.Default);
        }

        private void ApplyQueuedActiveImageQueueStatusRefresh(ImageQueueReviewStatusRefreshOperation operation)
        {
            try
            {
                Dispatcher.BeginInvoke(
                    new Action(() => ApplyQueuedActiveImageQueueStatusRefreshOnUi(operation)),
                    DispatcherPriority.Background);
            }
            catch (InvalidOperationException)
            {
                // The shell can close while a queued delete-status refresh is finishing.
            }
            catch (TaskCanceledException)
            {
            }
        }

        private void ApplyQueuedActiveImageQueueStatusRefreshOnUi(ImageQueueReviewStatusRefreshOperation operation)
        {
            if (isApplicationCloseApproved
                || !imageQueueReviewStatusRefreshCoordinator.IsCurrent(operation.Version)
                || !string.Equals(activeImagePath, operation.ImagePath, StringComparison.OrdinalIgnoreCase)
                || operation.Completion.IsCanceled)
            {
                return;
            }

            if (operation.Completion.IsFaulted)
            {
                AppendLog($"Image queue status refresh failed after delete: {operation.Completion.Exception?.GetBaseException().Message}");
                return;
            }

            if (operation.Completion.Result == null)
            {
                return;
            }

            ApplyReviewStatusToItem(FindImageQueueItem(operation.ImagePath), operation.Completion.Result);
            RefreshImageQueueViewAfterItemStateChange();
            UpdateImageQueueStatusText();
        }

        private bool IsActiveImageQueueSaveRequired(WpfImageQueueItem item)
        {
            return item != null
                && annotationDirtyState.IsDirty
                && !string.IsNullOrWhiteSpace(activeImagePath)
                && string.Equals(item.ImagePath, activeImagePath, StringComparison.OrdinalIgnoreCase);
        }

        private void ApplyActiveImageQueueSaveRequiredStatus(string reason)
        {
            WpfImageQueuePresenter.ApplySaveRequiredStatusToItem(FindImageQueueItem(activeImagePath), reason);
            // Live filtering observes IsSaveRequired; refreshing here resets and redraws the entire queue per edit.
            UpdateImageQueueStatusText();
        }

        private void SetActiveImageDetectionStatus(int candidateCount, bool succeeded)
        {
            if (string.IsNullOrWhiteSpace(activeImagePath))
            {
                return;
            }

            string imageName = Path.GetFileNameWithoutExtension(activeImagePath);
            YoloImageReviewStatus status = succeeded
                ? candidateCount > 0
                    ? imageQualityReviewWorkflowService.SetDetectionCandidates(activeImagePath, imageName, candidateCount)
                    : imageQualityReviewWorkflowService.SetDetectionNoCandidates(activeImagePath, imageName)
                : imageQualityReviewWorkflowService.SetDetectionFailed(activeImagePath, imageName, "Detection failed.");
            imageQualityReviewWorkflowService.SaveReviewStatus(global.Data);
            if (IsAnomalyDatasetPurpose())
            {
                WpfImageQueuePresenter.ApplyAnomalyReviewStatusToItem(
                    FindImageQueueItem(activeImagePath),
                    anomalyImageReviewWorkflowService.GetOrCreate(activeImagePath));
            }
            else
            {
                ApplyReviewStatusToItem(FindImageQueueItem(activeImagePath), status);
            }
            RefreshImageQueueViewAfterItemStateChange();
            UpdateImageQueueStatusText();
        }

        private bool ApplyActiveAnomalyClassification(IReadOnlyList<YoloWorkerSmokeCandidate> candidates)
        {
            return ApplyAnomalyClassificationToImage(
                activeImagePath,
                Path.GetFileNameWithoutExtension(activeImagePath),
                candidates,
                saveReviewStatus: true);
        }

        private bool ApplyAnomalyClassificationToImage(
            string imagePath,
            string imageName,
            IReadOnlyList<YoloWorkerSmokeCandidate> candidates,
            bool saveReviewStatus)
        {
            if (!IsAnomalyDatasetPurpose() || string.IsNullOrWhiteSpace(imagePath))
            {
                return false;
            }

            AnomalyClassificationResult result = anomalyImageReviewWorkflowService.ApplyClassification(
                new AnomalyClassificationRequest(
                    imagePath,
                    imageName,
                    candidates,
                    AnomalyClassificationOptionsSnapshot.From(
                        global.Data.ProjectSettings.AnomalyClassification.ToDecisionOptions()),
                    saveReviewStatus),
                global.Data);
            if (!result.IsMapped)
            {
                return false;
            }

            WpfImageQueuePresenter.ApplyAnomalyReviewStatusToItem(FindImageQueueItem(imagePath), result.Status);
            UpdateImageQueueStatusText();
            return true;
        }

        private void MarkActiveImageConfirmed()
        {
            if (string.IsNullOrWhiteSpace(activeImagePath))
            {
                return;
            }

            YoloImageReviewStatus status = imageQualityReviewWorkflowService.MarkConfirmed(activeImagePath, Path.GetFileNameWithoutExtension(activeImagePath));
            if (!activeImageSize.IsEmpty)
            {
                status = imageQualityReviewWorkflowService.RefreshLabelStatusAndReviewState(activeImagePath, activeImageSize, global.Data, hasActiveCandidates: false) ?? status;
            }

            ApplyReviewStatusToItem(FindImageQueueItem(activeImagePath), status);
            imageQualityReviewWorkflowService.SaveReviewStatus(global.Data);
            MarkActiveAnomalyImageAbnormal();
            // Live filtering observes the row properties above; a full Refresh resets and redraws the entire queue.
            UpdateImageQueueStatusText();
        }

        private void MarkActiveImageNoCandidate()
        {
            if (string.IsNullOrWhiteSpace(activeImagePath))
            {
                return;
            }

            string imageName = Path.GetFileNameWithoutExtension(activeImagePath);
            YoloImageReviewStatus status = imageQualityReviewWorkflowService.SetDetectionNoCandidates(activeImagePath, imageName);
            if (!activeImageSize.IsEmpty)
            {
                status = imageQualityReviewWorkflowService.RefreshLabelStatusAndReviewState(activeImagePath, activeImageSize, global.Data, hasActiveCandidates: false) ?? status;
            }

            ApplyReviewStatusToItem(FindImageQueueItem(activeImagePath), status);
            imageQualityReviewWorkflowService.SaveReviewStatus(global.Data);
            MarkActiveAnomalyImageNormal();
            RefreshImageQueueViewAfterItemStateChange();
            UpdateImageQueueStatusText();
        }

        private void MarkActiveImageSkippedOrCandidate()
        {
            if (string.IsNullOrWhiteSpace(activeImagePath))
            {
                return;
            }

            string imageName = Path.GetFileNameWithoutExtension(activeImagePath);
            YoloImageReviewStatus status = pendingDetectionCandidates.Count > 0
                ? imageQualityReviewWorkflowService.SetDetectionCandidates(activeImagePath, imageName, pendingDetectionCandidates.Count)
                : imageQualityReviewWorkflowService.MarkSkipped(activeImagePath, imageName);
            ApplyReviewStatusToItem(FindImageQueueItem(activeImagePath), status);
            imageQualityReviewWorkflowService.SaveReviewStatus(global.Data);
            RefreshImageQueueViewAfterItemStateChange();
            UpdateImageQueueStatusText();
        }

        private void ExecuteMarkQualityUnreviewedCommand()
        {
            SetActiveImageQualityReviewState(YoloImageQualityReviewState.Unreviewed);
        }

        private void ExecuteMarkQualityNeedsFixCommand()
        {
            SetActiveImageQualityReviewState(YoloImageQualityReviewState.NeedsFix);
        }

        private void ExecuteMarkQualityReviewedCommand()
        {
            SetActiveImageQualityReviewState(YoloImageQualityReviewState.Reviewed);
        }

        private void ExecuteExportQualityReviewReportCommand()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            if (!IsLabelQualityReviewPurpose())
            {
                SetModelStatus("QA 보고서는 Detection/Segmentation 데이터셋에서 내보낼 수 있습니다.");
                return;
            }

            try
            {
                ImageQualityReviewReportResult report = imageQualityReviewWorkflowService.ExportQualityReviewReport(global.Data);
                if (!report.HasOutputPath)
                {
                    SetModelStatus("QA 보고서 저장 실패: 데이터셋 저장 폴더를 먼저 지정하세요.");
                    return;
                }

                SetModelStatus($"QA 보고서 저장: {Path.GetFileName(report.OutputPath)} / 수정 필요 {report.NeedsFixCount}");
                AppendLog($"QA 보고서 저장: {Path.GetFileName(report.OutputPath)} / 전체 {report.TotalImageCount} / 수정 필요 {report.NeedsFixCount} / 검수 완료 {report.ReviewedCount}");
            }
            catch (Exception exception)
            {
                SetModelStatus($"QA 보고서 저장 실패: {exception.Message}");
                AppendLog($"QA 보고서 저장 실패: {exception.Message}");
            }
        }

        private void SetActiveImageQualityReviewState(YoloImageQualityReviewState state)
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            if (!IsLabelQualityReviewPurpose() || string.IsNullOrWhiteSpace(activeImagePath))
            {
                AppendLog("품질 검수 상태를 변경할 Detection/Segmentation 이미지를 먼저 여세요.");
                return;
            }

            WpfImageQueueItem item = FindImageQueueItem(activeImagePath);
            string imageName = Path.GetFileNameWithoutExtension(activeImagePath);
            ImageQualityReviewResult result = imageQualityReviewWorkflowService.ApplyQualityReview(
                new ImageQualityReviewRequest(
                    activeImagePath,
                    imageName,
                    state,
                    IsLabelQualityReviewPurpose(),
                    item?.IsSaveRequired == true,
                    annotationDirtyState.IsDirty,
                    ImageQueueFilterService.HasCompletedLabelWork(item),
                    ObjectReviewViewModel?.QualityReviewNoteText),
                global.Data);
            if (!result.IsApplicable)
            {
                AppendLog("품질 검수 상태를 변경할 Detection/Segmentation 이미지를 먼저 여세요.");
                return;
            }

            if (!result.IsAccepted)
            {
                SetModelStatus("검수 완료 불가: 라벨 저장 또는 객체 없음 완료 후 다시 선택하세요.");
                RefreshActiveImageQualityReviewPresentation(item, result.Status);
                return;
            }

            YoloImageReviewStatus status = result.Status;
            ApplyReviewStatusToItem(item, status);
            RefreshImageQueueViewAfterItemStateChange();
            UpdateImageQueueStatusText();

            string displayText = WpfImageQueuePresenter.FormatQualityReviewState(state);
            SetModelStatus($"품질 검수: {displayText}");
            AppendLog($"품질 검수 상태 변경: {Path.GetFileName(activeImagePath)} / {displayText}");
        }

        private void InvalidateActiveImageQualityReviewAfterEdit()
        {
            if (!IsLabelQualityReviewPurpose() || string.IsNullOrWhiteSpace(activeImagePath))
            {
                return;
            }

            YoloImageReviewStatus before = imageQualityReviewWorkflowService.GetOrCreate(activeImagePath);
            if (before?.QualityReviewState != YoloImageQualityReviewState.Reviewed)
            {
                RefreshActiveImageQualityReviewPresentation(FindImageQueueItem(activeImagePath), before);
                return;
            }

            YoloImageReviewStatus status = imageQualityReviewWorkflowService.InvalidateQualityReviewAfterEdit(
                activeImagePath,
                Path.GetFileNameWithoutExtension(activeImagePath));
            ApplyReviewStatusToItem(FindImageQueueItem(activeImagePath), status);
            imageQualityReviewWorkflowService.SaveReviewStatus(global.Data);
        }

        private void RefreshActiveImageQualityReviewPresentation()
        {
            WpfImageQueueItem item = FindImageQueueItem(activeImagePath);
            RefreshActiveImageQualityReviewPresentation(item, imageQualityReviewWorkflowService.GetOrCreate(activeImagePath));
        }

        private void RefreshActiveImageQualityReviewPresentation(
            WpfImageQueueItem item,
            YoloImageReviewStatus status)
        {
            bool hasActiveImage = IsLabelQualityReviewPurpose()
                && !string.IsNullOrWhiteSpace(activeImagePath)
                && item != null
                && string.Equals(item.ImagePath, activeImagePath, StringComparison.OrdinalIgnoreCase);
            bool canMarkReviewed = hasActiveImage
                && !annotationDirtyState.IsDirty
                && !item.IsSaveRequired
                && ImageQueueFilterService.HasCompletedLabelWork(item);
            ObjectReviewViewModel?.SetQualityReviewState(
                status?.QualityReviewState ?? YoloImageQualityReviewState.Unreviewed,
                hasActiveImage,
                canMarkReviewed,
                status?.QualityReviewNote);
        }

        private bool IsLabelQualityReviewPurpose()
        {
            EnsureProjectSettings();
            LabelingDatasetPurpose purpose = global.Data.ProjectSettings.DatasetPurpose;
            return purpose == LabelingDatasetPurpose.ObjectDetection
                || purpose == LabelingDatasetPurpose.Segmentation;
        }

        private bool IsAnomalyDatasetPurpose()
        {
            EnsureProjectSettings();
            return global.Data?.ProjectSettings?.DatasetPurpose == LabelingDatasetPurpose.AnomalyDetection;
        }

        private void MarkActiveAnomalyImageNormal()
        {
            MarkActiveAnomalyImageReviewState(AnomalyImageReviewState.Normal);
        }

        private void MarkActiveAnomalyImageAbnormal()
        {
            MarkActiveAnomalyImageReviewState(AnomalyImageReviewState.Abnormal);
        }

        private void MarkActiveAnomalyImageReviewState(AnomalyImageReviewState state)
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            if (!IsAnomalyDatasetPurpose() || string.IsNullOrWhiteSpace(activeImagePath))
            {
                return;
            }

            string imageName = Path.GetFileNameWithoutExtension(activeImagePath);
            MarkAnomalyImageReviewState(activeImagePath, imageName, state, saveReviewStatus: true);
        }

        private void ExecuteMarkActiveAnomalyNormalAndNextCommand()
        {
            MarkActiveAnomalyImageAndOpenNext(AnomalyImageReviewState.Normal);
        }

        private void ExecuteMarkActiveAnomalyAbnormalAndNextCommand()
        {
            MarkActiveAnomalyImageAndOpenNext(AnomalyImageReviewState.Abnormal);
        }

        private void ExecuteClearActiveAnomalyReviewCommand()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            if (!IsAnomalyDatasetPurpose() || string.IsNullOrWhiteSpace(activeImagePath))
            {
                return;
            }

            MarkActiveAnomalyImageReviewState(AnomalyImageReviewState.Unreviewed);
            SetDatasetStatus($"OK/NG 이미지 판정: 미판정으로 되돌림 / {Path.GetFileName(activeImagePath)}");
            AppendLog($"Anomaly image review cleared: {activeImagePath}");
        }

        private void MarkActiveAnomalyImageAndOpenNext(AnomalyImageReviewState state)
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            if (!IsAnomalyDatasetPurpose() || string.IsNullOrWhiteSpace(activeImagePath))
            {
                return;
            }

            string reviewedPath = activeImagePath;
            MarkActiveAnomalyImageReviewState(state);
            string decisionText = state == AnomalyImageReviewState.Normal ? "정상(OK)" : "이상(NG)";
            SetDatasetStatus($"OK/NG 이미지 판정: {decisionText} 저장 / {Path.GetFileName(reviewedPath)}");
            AppendLog($"Anomaly image reviewed: {reviewedPath} / {state}");
            if (!TryOpenNextIncompleteQueueImage())
            {
                SetDatasetStatus("OK/NG 이미지 판정: 모든 이미지 판정 완료");
            }
        }

        private void MarkAnomalyImageReviewState(string imagePath, string imageName, AnomalyImageReviewState state, bool saveReviewStatus)
        {
            if (!IsAnomalyDatasetPurpose() || string.IsNullOrWhiteSpace(imagePath))
            {
                return;
            }

            AnomalyImageReviewResult result = anomalyImageReviewWorkflowService.ApplyReviewState(
                new AnomalyImageReviewRequest(imagePath, imageName, state, saveReviewStatus),
                global.Data);
            if (!result.IsApplicable)
            {
                return;
            }

            WpfImageQueuePresenter.ApplyAnomalyReviewStatusToItem(FindImageQueueItem(imagePath), result.Status);
            // Live filtering observes AnomalyReviewState/IsLabeled. Refresh() would reset and redraw every row.
            UpdateImageQueueStatusText();
        }

        private void RefreshImageQueuePurposePresentation()
        {
            bool isAnomalyPurpose = IsAnomalyDatasetPurpose();
            foreach (WpfImageQueueItem item in imageQueueItems)
            {
                if (isAnomalyPurpose)
                {
                    WpfImageQueuePresenter.ApplyAnomalyReviewStatusToItem(item, anomalyImageReviewWorkflowService.GetOrCreate(item.ImagePath));
                }
                else
                {
                    ApplyReviewStatusToItemCore(
                        item,
                        imageQualityReviewWorkflowService.GetOrCreate(item.ImagePath),
                        refreshTrainingStepCompletion: false);
                }
            }

            imageQueueView?.Refresh();
            UpdateImageQueueStatusText();
        }

        private void SaveAnomalyImageReviewStatus()
        {
            anomalyImageReviewWorkflowService.SaveReviewStatus(global.Data);
            // The manifest is derived and is rebuilt by LabelingProjectData.SaveConfig. Keep the primary review state durable
            // without rescanning the full dataset on every OK/NG decision.
        }
        #endregion

    }
}
