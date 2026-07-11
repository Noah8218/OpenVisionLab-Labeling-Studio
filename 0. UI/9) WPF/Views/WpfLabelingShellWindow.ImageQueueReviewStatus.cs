using Lib.Common;
using MahApps.Metro.IconPacks;
using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using DrawingSize = System.Drawing.Size;
using MediaBrush = System.Windows.Media.Brush;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Review-state persistence runs outside the immediate delete/selection hot path and marshals only the latest result back to WPF.
        private void RefreshActiveImageQueueStatus(bool hasActiveCandidates)
        {
            if (string.IsNullOrWhiteSpace(activeImagePath) || activeImageSize.IsEmpty)
            {
                return;
            }

            WpfImageQueueItem item = FindImageQueueItem(activeImagePath);
            YoloImageReviewStatus status = RefreshActiveImageQueueStatusCore(
                activeImagePath,
                activeImageSize,
                global.Data,
                hasActiveCandidates);
            ApplyReviewStatusToItem(item, status);
            imageQueueView?.Refresh();
            UpdateImageQueueStatusText();
        }

        private void QueueActiveImageQueueStatusRefresh(bool hasActiveCandidates)
        {
            if (string.IsNullOrWhiteSpace(activeImagePath) || activeImageSize.IsEmpty)
            {
                return;
            }

            string imagePath = activeImagePath;
            DrawingSize imageSize = activeImageSize;
            CData data = global.Data;
            int refreshVersion = Interlocked.Increment(ref queuedActiveImageQueueStatusRefreshVersion);

            // Delete must feel immediate. Label-file recount and review-state JSON writes are
            // background bookkeeping; only the latest completed result returns to the UI thread.
            Task.Run(() => RefreshActiveImageQueueStatusCore(
                    imagePath,
                    imageSize,
                    data,
                    hasActiveCandidates))
                .ContinueWith(
                    task => ApplyQueuedActiveImageQueueStatusRefresh(refreshVersion, imagePath, task),
                    TaskScheduler.Default);
        }

        private YoloImageReviewStatus RefreshActiveImageQueueStatusCore(
            string imagePath,
            DrawingSize imageSize,
            CData data,
            bool hasActiveCandidates)
        {
            YoloImageReviewStatus status = imageReviewStatus.RefreshLabelStatusAndReviewState(
                imagePath,
                imageSize,
                data,
                hasActiveCandidates);
            imageReviewStatus.SaveReviewStatus(data);
            return status;
        }

        private void ApplyQueuedActiveImageQueueStatusRefresh(
            int refreshVersion,
            string imagePath,
            Task<YoloImageReviewStatus> refreshTask)
        {
            try
            {
                Dispatcher.BeginInvoke(
                    new Action(() =>
                    {
                        if (refreshVersion != Volatile.Read(ref queuedActiveImageQueueStatusRefreshVersion)
                            || !string.Equals(activeImagePath, imagePath, StringComparison.OrdinalIgnoreCase)
                            || refreshTask.IsCanceled)
                        {
                            return;
                        }

                        if (refreshTask.IsFaulted)
                        {
                            AppendLog($"Image queue status refresh failed after delete: {refreshTask.Exception?.GetBaseException().Message}");
                            return;
                        }

                        ApplyReviewStatusToItem(FindImageQueueItem(imagePath), refreshTask.Result);
                        imageQueueView?.Refresh();
                        UpdateImageQueueStatusText();
                    }),
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

        private bool IsActiveImageQueueSaveRequired(WpfImageQueueItem item)
        {
            return item != null
                && !string.IsNullOrWhiteSpace(annotationDirtyReason)
                && !string.IsNullOrWhiteSpace(activeImagePath)
                && string.Equals(item.ImagePath, activeImagePath, StringComparison.OrdinalIgnoreCase);
        }

        private void ApplyActiveImageQueueSaveRequiredStatus(string reason)
        {
            ApplySaveRequiredStatusToQueueItem(FindImageQueueItem(activeImagePath), reason);
            imageQueueView?.Refresh();
            UpdateImageQueueStatusText();
        }

        private static void ApplySaveRequiredStatusToQueueItem(WpfImageQueueItem item, string reason)
        {
            if (item == null)
            {
                return;
            }

            string displayReason = string.IsNullOrWhiteSpace(reason) ? "\uB77C\uBCA8 \uD3B8\uC9D1" : reason.Trim();
            item.IsSaveRequired = true;
            item.LabelStatus = "\uC800\uC7A5 \uD544\uC694";
            item.QueueIconKind = PackIconMaterialKind.AlertCircleOutline;
            item.QueueIconBrush = WpfImageQueueItem.WarningBrush;
            item.QueueBadgeBackgroundBrush = WpfImageQueueItem.WarningBadgeBrush;
            item.QueueRowAccentBrush = WpfImageQueueItem.WarningBrush;
            item.QueueBadgeText = "\uC800\uC7A5 \uD544\uC694";
            item.QueueStatusSummary = $"\uB77C\uBCA8 \uC800\uC7A5 \uD544\uC694: {displayReason}";
            item.Detail = $"{item.FileName}{Environment.NewLine}\uD30C\uC77C\uC5D0 \uBC18\uC601\uD558\uB824\uBA74 \uB77C\uBCA8 \uC800\uC7A5\uC744 \uB20C\uB7EC\uC57C \uD569\uB2C8\uB2E4.{Environment.NewLine}{displayReason}";
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
                    ? imageReviewStatus.SetDetectionCandidates(activeImagePath, imageName, candidateCount)
                    : imageReviewStatus.SetDetectionNoCandidates(activeImagePath, imageName)
                : imageReviewStatus.SetDetectionFailed(activeImagePath, imageName, "Detection failed.");
            ApplyReviewStatusToItem(FindImageQueueItem(activeImagePath), status);
            imageReviewStatus.SaveReviewStatus(global.Data);
            imageQueueView?.Refresh();
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

            AnomalyClassificationDecision decision = AnomalyClassificationDecisionService.Build(
                candidates,
                global.Data.ProjectSettings.AnomalyClassification.ToDecisionOptions());
            if (!decision.IsMapped)
            {
                return false;
            }

            MarkAnomalyImageReviewState(imagePath, imageName, decision.ReviewState, saveReviewStatus);
            return true;
        }

        private void MarkActiveImageConfirmed()
        {
            if (string.IsNullOrWhiteSpace(activeImagePath))
            {
                return;
            }

            YoloImageReviewStatus status = imageReviewStatus.MarkConfirmed(activeImagePath, Path.GetFileNameWithoutExtension(activeImagePath));
            if (!activeImageSize.IsEmpty)
            {
                status = imageReviewStatus.RefreshLabelStatusAndReviewState(activeImagePath, activeImageSize, global.Data, hasActiveCandidates: false) ?? status;
            }

            ApplyReviewStatusToItem(FindImageQueueItem(activeImagePath), status);
            imageReviewStatus.SaveReviewStatus(global.Data);
            MarkActiveAnomalyImageAbnormal();
            imageQueueView?.Refresh();
            UpdateImageQueueStatusText();
        }

        private void MarkActiveImageNoCandidate()
        {
            if (string.IsNullOrWhiteSpace(activeImagePath))
            {
                return;
            }

            string imageName = Path.GetFileNameWithoutExtension(activeImagePath);
            YoloImageReviewStatus status = imageReviewStatus.SetDetectionNoCandidates(activeImagePath, imageName);
            if (!activeImageSize.IsEmpty)
            {
                status = imageReviewStatus.RefreshLabelStatusAndReviewState(activeImagePath, activeImageSize, global.Data, hasActiveCandidates: false) ?? status;
            }

            ApplyReviewStatusToItem(FindImageQueueItem(activeImagePath), status);
            imageReviewStatus.SaveReviewStatus(global.Data);
            MarkActiveAnomalyImageNormal();
            imageQueueView?.Refresh();
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
                ? imageReviewStatus.SetDetectionCandidates(activeImagePath, imageName, pendingDetectionCandidates.Count)
                : imageReviewStatus.MarkSkipped(activeImagePath, imageName);
            ApplyReviewStatusToItem(FindImageQueueItem(activeImagePath), status);
            imageReviewStatus.SaveReviewStatus(global.Data);
            imageQueueView?.Refresh();
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
            if (!IsLabelQualityReviewPurpose())
            {
                SetModelStatus("QA 보고서는 Detection/Segmentation 데이터셋에서 내보낼 수 있습니다.");
                return;
            }

            string outputPath = YoloImageQualityReviewReportExportService.ResolveDefaultOutputPath(global.Data);
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                SetModelStatus("QA 보고서 저장 실패: 데이터셋 저장 폴더를 먼저 지정하세요.");
                return;
            }

            try
            {
                YoloImageQualityReviewReportExportResult result = YoloImageQualityReviewReportExportService.ExportMarkdown(
                    imageReviewStatus.GetItems(),
                    outputPath);
                SetModelStatus($"QA 보고서 저장: {Path.GetFileName(result.OutputPath)} / 수정 필요 {result.NeedsFixCount}");
                AppendLog($"QA 보고서 저장: {Path.GetFileName(result.OutputPath)} / 전체 {result.TotalImageCount} / 수정 필요 {result.NeedsFixCount} / 검수 완료 {result.ReviewedCount}");
            }
            catch (Exception exception)
            {
                SetModelStatus($"QA 보고서 저장 실패: {exception.Message}");
                AppendLog($"QA 보고서 저장 실패: {exception.Message}");
            }
        }

        private void SetActiveImageQualityReviewState(YoloImageQualityReviewState state)
        {
            if (!IsLabelQualityReviewPurpose() || string.IsNullOrWhiteSpace(activeImagePath))
            {
                AppendLog("품질 검수 상태를 변경할 Detection/Segmentation 이미지를 먼저 여세요.");
                return;
            }

            WpfImageQueueItem item = FindImageQueueItem(activeImagePath);
            if (state == YoloImageQualityReviewState.Reviewed
                && (item == null
                    || item.IsSaveRequired
                    || !string.IsNullOrWhiteSpace(annotationDirtyReason)
                    || !WpfImageQueueFilterService.HasCompletedLabelWork(item)))
            {
                SetModelStatus("검수 완료 불가: 라벨 저장 또는 객체 없음 완료 후 다시 선택하세요.");
                RefreshActiveImageQualityReviewPresentation(item, imageReviewStatus.GetOrCreate(activeImagePath));
                return;
            }

            string imageName = Path.GetFileNameWithoutExtension(activeImagePath);
            YoloImageReviewStatus status = state switch
            {
                YoloImageQualityReviewState.NeedsFix => imageReviewStatus.MarkQualityNeedsFix(
                    activeImagePath,
                    imageName,
                    ObjectReviewViewModel?.QualityReviewNoteText),
                YoloImageQualityReviewState.Reviewed => imageReviewStatus.MarkQualityReviewed(activeImagePath, imageName),
                _ => imageReviewStatus.ClearQualityReview(activeImagePath, imageName)
            };
            ApplyReviewStatusToItem(item, status);
            imageReviewStatus.SaveReviewStatus(global.Data);
            imageQueueView?.Refresh();
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

            YoloImageReviewStatus before = imageReviewStatus.GetOrCreate(activeImagePath);
            if (before?.QualityReviewState != YoloImageQualityReviewState.Reviewed)
            {
                RefreshActiveImageQualityReviewPresentation(FindImageQueueItem(activeImagePath), before);
                return;
            }

            YoloImageReviewStatus status = imageReviewStatus.InvalidateQualityReviewAfterEdit(
                activeImagePath,
                Path.GetFileNameWithoutExtension(activeImagePath));
            ApplyReviewStatusToItem(FindImageQueueItem(activeImagePath), status);
            imageReviewStatus.SaveReviewStatus(global.Data);
        }

        private void RefreshActiveImageQualityReviewPresentation()
        {
            WpfImageQueueItem item = FindImageQueueItem(activeImagePath);
            RefreshActiveImageQualityReviewPresentation(item, imageReviewStatus.GetOrCreate(activeImagePath));
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
                && string.IsNullOrWhiteSpace(annotationDirtyReason)
                && !item.IsSaveRequired
                && WpfImageQueueFilterService.HasCompletedLabelWork(item);
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
            if (!IsAnomalyDatasetPurpose() || string.IsNullOrWhiteSpace(activeImagePath))
            {
                return;
            }

            string imageName = Path.GetFileNameWithoutExtension(activeImagePath);
            MarkAnomalyImageReviewState(activeImagePath, imageName, state, saveReviewStatus: true);
        }

        private void MarkAnomalyImageReviewState(string imagePath, string imageName, AnomalyImageReviewState state, bool saveReviewStatus)
        {
            if (!IsAnomalyDatasetPurpose() || string.IsNullOrWhiteSpace(imagePath))
            {
                return;
            }

            if (state == AnomalyImageReviewState.Normal)
            {
                anomalyImageReviewStatus.MarkNormal(imagePath, imageName);
            }
            else if (state == AnomalyImageReviewState.Abnormal)
            {
                anomalyImageReviewStatus.MarkAbnormal(imagePath, imageName);
            }
            else
            {
                anomalyImageReviewStatus.ClearReviewState(imagePath, imageName);
            }

            if (saveReviewStatus)
            {
                SaveAnomalyImageReviewStatus();
            }
        }

        private void SaveAnomalyImageReviewStatus()
        {
            anomalyImageReviewStatus.SaveReviewStatus(global.Data);
            string recipeName = GetCurrentRecipeName();
            if (!string.IsNullOrWhiteSpace(recipeName))
            {
                LabelingDatasetManifestService.Save(global.Data, recipeName);
            }
        }
    }
}
