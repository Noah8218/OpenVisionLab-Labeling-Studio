using MvcVisionSystem._1._Core;
using MvcVisionSystem.Yolo;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    internal sealed class ImageQueueReviewAdapter
    {
        private readonly ImageQueueReviewAdapterContext context;

        private LabelingProjectData projectData => context.DataProvider?.Invoke();
        private ImageQualityReviewWorkflowService imageQualityReviewWorkflowService => context.ImageQualityReviewWorkflowService;
        private AnomalyImageReviewSession anomalyImageReviewSession => context.AnomalyImageReviewSession;
        private ImageQueueSelectionService imageQueueSelectionService => context.ImageQueueSelectionService;
        private IList<WpfImageQueueItem> imageQueueItems => context.ImageQueueItems ?? Array.Empty<WpfImageQueueItem>();
        private IReadOnlyDictionary<string, WpfImageQueueItem> imageQueueItemsByPath => context.ImageQueueItemsByPath;
        private ICollectionView imageQueueView => context.ImageQueueViewProvider?.Invoke();
        private WpfImageQueuePanelViewModel ImageQueueViewModel => context.ImageQueueViewModel;
        private WpfObjectReviewPanelViewModel ObjectReviewViewModel => context.ObjectReviewViewModel;
        private ComboBox ImageQueueFilterBox => context.ImageQueueFilterBox;
        private TextBox ImageQueueSearchBox => context.ImageQueueSearchBox;
        private DataGrid ImageQueueGrid => context.ImageQueueGrid;
        private WpfImageQueuePanel ImageQueuePanelControl => context.ImageQueuePanelControl;
        private AnnotationDirtyState annotationDirtyState => context.AnnotationDirtyState;
        private IReadOnlyList<YoloWorkerSmokeCandidate> pendingDetectionCandidates
            => context.PendingDetectionCandidatesProvider?.Invoke() ?? Array.Empty<YoloWorkerSmokeCandidate>();
        private string activeImagePath => context.ActiveImagePathProvider?.Invoke() ?? string.Empty;
        private System.Drawing.Size activeImageSize => context.ActiveImageSizeProvider?.Invoke() ?? System.Drawing.Size.Empty;
        private bool isApplicationCloseApproved => context.IsApplicationCloseApproved?.Invoke() == true;
        private bool suppressImageQueueSelection
        {
            get => context.IsImageQueueSelectionSuppressed?.Invoke() == true;
            set => context.SetImageQueueSelectionSuppressed?.Invoke(value);
        }
        private System.Windows.Threading.Dispatcher Dispatcher => context.Dispatcher;

        internal ImageQueueReviewAdapter(ImageQueueReviewAdapterContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            ArgumentNullException.ThrowIfNull(context.DataProvider);
        }

        private void AppendLog(string message) => context.AppendLog?.Invoke(message);
        private void SetDatasetStatus(string message) => context.SetDatasetStatus?.Invoke(message);
        private void SetModelStatus(string message) => context.SetModelStatus?.Invoke(message);
        private void RefreshImageQueueViewAfterItemStateChange() => context.RefreshImageQueueViewAfterItemStateChange?.Invoke();
        private bool TryOpenNextIncompleteQueueImage() => context.TryOpenNextIncompleteQueueImage?.Invoke() == true;
        internal void ApplyReviewStatusToItem(WpfImageQueueItem item, YoloImageReviewStatus status)
            => ApplyReviewStatusToItemCore(item, status, refreshTrainingStepCompletion: true);

        internal void ApplyReviewStatusToItemCore(
            WpfImageQueueItem item,
            YoloImageReviewStatus status,
            bool refreshTrainingStepCompletion)
        {
            if (item == null || status == null)
            {
                return;
            }

            item.LabelStatus = WpfImageQueuePresenter.FormatLabelStatus(status.LabelText);
            item.DetectStatus = WpfImageQueuePresenter.FormatDetectionStatus(status);
            item.IsLabeled = status.IsLabeled;
            item.IsSaveRequired = false;
            item.ReviewState = status.ReviewState;
            item.QualityReviewState = status.QualityReviewState;
            item.QueueIconKind = WpfImageQueuePresenter.GetIconKind(status);
            item.QueueIconBrush = WpfImageQueuePresenter.GetIconBrush(status);
            item.QueueBadgeBackgroundBrush = WpfImageQueuePresenter.GetBadgeBackgroundBrush(status);
            item.QueueRowAccentBrush = WpfImageQueuePresenter.GetRowAccentBrush(status);
            item.QueueBadgeText = WpfImageQueuePresenter.BuildBadgeText(status);
            item.QueueStatusSummary = WpfImageQueuePresenter.BuildStatusSummary(status);
            item.Detail = WpfImageQueuePresenter.BuildDetailText(status);
            if (IsActiveImageQueueSaveRequired(item))
            {
                WpfImageQueuePresenter.ApplySaveRequiredStatusToItem(item, annotationDirtyState.Reason);
            }

            RefreshActiveImageQualityReviewPresentation(item, status);

            if (refreshTrainingStepCompletion)
            {
                context.RefreshYoloTrainingStepCompletion?.Invoke();
            }
        }

        #region ImageQueuePresentation
        // Queue presentation helpers stay isolated from queue loading so status-text changes do not hide data-flow changes.
        internal void SelectImageQueueItem(string imagePath)
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

        internal WpfImageQueueItem FindImageQueueItem(string imagePath)
        {
            if (!string.IsNullOrWhiteSpace(imagePath)
                && imageQueueItemsByPath.TryGetValue(imagePath, out WpfImageQueueItem indexedItem))
            {
                return indexedItem;
            }

            return imageQueueSelectionService.FindItem(imageQueueItems, imagePath);
        }

        internal void UpdateImageQueueStatusText(int loadedCount = -1, int totalToLoad = -1)
        {
            WpfImageQueueFilter selectedFilter = GetSelectedImageQueueFilter();
            ImageQueueSummary summary = ImageQueueFilterService.Summarize(imageQueueItems);
            bool hasSearch = !string.IsNullOrWhiteSpace(GetImageQueueSearchText());
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

        internal void UpdateQueueQuickFilterButtons()
        {
            UpdateQueueQuickFilterButtons(
                ImageQueueFilterService.Summarize(imageQueueItems),
                GetSelectedImageQueueFilter());
        }

        internal void UpdateQueueQuickFilterButtons(ImageQueueSummary summary, WpfImageQueueFilter filter)
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

        internal WpfImageQueueFilter GetSelectedImageQueueFilter()
        {
            return ImageQueueViewModel?.SelectedFilter
                ?? (ImageQueueFilterBox?.SelectedItem as WpfImageQueueFilterOption)?.Filter
                ?? WpfImageQueueFilter.All;
        }

        internal string GetImageQueueSearchText()
        {
            return ImageQueueViewModel?.SearchText ?? ImageQueueSearchBox?.Text ?? string.Empty;
        }

        internal void ApplyFilterSelectionChanged()
        {
            ImageQueuePanelControl?.RefreshQueueView();
            UpdateImageQueueStatusText();
        }

        internal void SetImageQueueFilter(WpfImageQueueFilter filter)
        {
            if (ImageQueueFilterBox?.ItemsSource is IEnumerable<WpfImageQueueFilterOption> options)
            {
                WpfImageQueueFilterOption selected = options.FirstOrDefault(option => option.Filter == filter);
                if (selected != null)
                {
                    ImageQueueFilterBox.SelectedItem = selected;
                    return;
                }
            }

            ImageQueuePanelControl?.RefreshQueueView();
            UpdateImageQueueStatusText();
        }

        internal void ApplySearchChanged(string searchText)
        {
            ImageQueuePanelControl?.RefreshQueueView();
            SelectSingleVisibleQueueSearchResult();
            UpdateImageQueueStatusText();
        }

        internal void SelectSingleVisibleQueueSearchResult()
        {
            if (imageQueueView == null || ImageQueueGrid == null)
            {
                return;
            }

            WpfImageQueueItem item = ImageQueueFilterService.FindSingleItem(imageQueueView
                .Cast<object>()
                .OfType<WpfImageQueueItem>());
            if (item == null)
            {
                return;
            }

            // Search narrows the queue for reopen/review work. If exactly one row remains,
            // select it so the visible Open action works without a fragile extra row click.
            suppressImageQueueSelection = true;
            try
            {
                ImageQueueGrid.SelectedItem = item;
                if (ImageQueueViewModel != null)
                {
                    ImageQueueViewModel.SelectedQueueItem = item;
                }

                ImageQueueGrid.ScrollIntoView(item);
            }
            finally
            {
                suppressImageQueueSelection = false;
            }

            UpdateSelectedQueueImageButton(item);
        }

        internal void ExecuteSelectedQueueItemChanged(WpfImageQueueItem item)
        {
            WpfImageQueueItem selectedItem = imageQueueSelectionService.ResolveSelectedItem(item, imageQueueItems, activeImagePath);
            if (suppressImageQueueSelection)
            {
                UpdateSelectedQueueImageButton(selectedItem);
                return;
            }

            if (selectedItem == null)
            {
                UpdateSelectedQueueImageButton(null);
                return;
            }

            UpdateSelectedQueueImageButton(selectedItem);
            context.OpenSelectedQueueImage?.Invoke(selectedItem, true);
        }

        internal ImageQueueOpenSelection GetOpenSelectedQueueSelection()
        {
            var candidates = new List<WpfImageQueueItem>
            {
                ImageQueueGrid?.SelectedItem as WpfImageQueueItem,
                ImageQueueViewModel?.SelectedQueueItem,
                FindSingleSearchMatchedQueueItem()
            };

            if (imageQueueView != null)
            {
                // UIAutomation and keyboard focus can leave DataGrid.SelectedItem unset while
                // a filtered single row is plainly visible. In that case the visible row is
                // the operator's intended target for the Open action. Filter/search changes
                // already refresh this view, so opening must not reevaluate the whole queue.
                candidates.Add(ImageQueueFilterService.FindSingleItem(imageQueueView
                    .Cast<object>()
                    .OfType<WpfImageQueueItem>()));
            }

            return imageQueueSelectionService.ResolveOpenSelection(candidates, projectData);
        }

        internal WpfImageQueueItem FindSingleSearchMatchedQueueItem()
        {
            // Open is a deliberate user command. When search text uniquely identifies
            // one filtered row, prefer that row even if DataGrid focus/selection is stale.
            return ImageQueueFilterService.FindSingleSearchMatch(
                imageQueueItems,
                GetImageQueueSearchText(),
                GetSelectedImageQueueFilter());
        }

        internal string BuildOpenQueueSelectionFailureMessage()
        {
            string searchText = GetImageQueueSearchText().Trim();
            string gridSelection = (ImageQueueGrid?.SelectedItem as WpfImageQueueItem)?.FileName ?? "-";
            string viewModelSelection = ImageQueueViewModel?.SelectedQueueItem?.FileName ?? "-";
            int visibleCount = CountVisibleQueueItems(limit: 3);
            int searchMatchCount = CountSearchMatchedQueueItems(searchText, limit: 3);
            return WpfImageQueuePresenter.BuildOpenSelectionFailureMessage(
                searchText,
                visibleCount,
                searchMatchCount,
                gridSelection,
                viewModelSelection);
        }

        internal int CountVisibleQueueItems(int limit)
        {
            if (imageQueueView == null)
            {
                return 0;
            }

            return imageQueueView
                .Cast<object>()
                .OfType<WpfImageQueueItem>()
                .Take(Math.Max(1, limit))
                .Count();
        }

        internal int CountSearchMatchedQueueItems(string searchText, int limit)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return 0;
            }

            return ImageQueueFilterService.CountSearchMatches(
                imageQueueItems,
                searchText,
                GetSelectedImageQueueFilter(),
                limit);
        }

        internal void UpdateSelectedQueueImageButton(WpfImageQueueItem item)
        {
            bool canOpenSelectedImage = context.CanOpenQueueItem?.Invoke(item) == true;

            if (ImageQueueViewModel != null)
            {
                ImageQueueViewModel.SetSelectedImageAvailability(canOpenSelectedImage);
                return;
            }

            context.SetOpenSelectedImageEnabled?.Invoke(canOpenSelectedImage);
        }

        #endregion

        #region ImageQueueReviewStatus
        // Review-state persistence runs outside the immediate delete/selection hot path and marshals only the latest result back to WPF.
        internal void RefreshActiveImageQueueStatus(bool hasActiveCandidates)
        {
            if (!imageQualityReviewWorkflowService.CanReview(projectData)) return;

            if (string.IsNullOrWhiteSpace(activeImagePath) || activeImageSize.IsEmpty)
            {
                return;
            }

            WpfImageQueueItem item = FindImageQueueItem(activeImagePath);
            if (IsAnomalyDatasetPurpose())
            {
                WpfImageQueuePresenter.ApplyAnomalyReviewStatusToItem(
                    item,
                    anomalyImageReviewSession.GetStatus(activeImagePath, isAnomalyPurpose: true));
                RefreshImageQueueViewAfterItemStateChange();
                UpdateImageQueueStatusText();
                return;
            }
            YoloImageReviewStatus status = imageQualityReviewWorkflowService.RefreshLabelStatusAndReviewState(
                activeImagePath,
                activeImageSize,
                projectData,
                hasActiveCandidates);
            imageQualityReviewWorkflowService.SaveReviewStatus(projectData);
            ApplyReviewStatusToItem(item, status);
            RefreshImageQueueViewAfterItemStateChange();
            UpdateImageQueueStatusText();
        }

        internal void QueueActiveImageQueueStatusRefresh(bool hasActiveCandidates)
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
                    anomalyImageReviewSession.GetStatus(activeImagePath, isAnomalyPurpose: true));
                RefreshImageQueueViewAfterItemStateChange();
                UpdateImageQueueStatusText();
                return;
            }

            ImageQueueReviewStatusRefreshOperation operation = imageQualityReviewWorkflowService.QueueRefresh(
                activeImagePath,
                activeImageSize,
                projectData,
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
                || !imageQualityReviewWorkflowService.IsCurrent(operation.Version)
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

        internal bool IsActiveImageQueueSaveRequired(WpfImageQueueItem item)
        {
            return item != null
                && annotationDirtyState.IsDirty
                && !string.IsNullOrWhiteSpace(activeImagePath)
                && string.Equals(item.ImagePath, activeImagePath, StringComparison.OrdinalIgnoreCase);
        }

        internal void ApplyActiveImageQueueSaveRequiredStatus(string reason)
        {
            WpfImageQueuePresenter.ApplySaveRequiredStatusToItem(FindImageQueueItem(activeImagePath), reason);
            // Live filtering observes IsSaveRequired; refreshing here resets and redraws the entire queue per edit.
            UpdateImageQueueStatusText();
        }

        internal void SetActiveImageDetectionStatus(int candidateCount, bool succeeded)
        {
            if (!imageQualityReviewWorkflowService.CanReview(projectData)) return;

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
            imageQualityReviewWorkflowService.SaveReviewStatus(projectData);
            if (IsAnomalyDatasetPurpose())
            {
                WpfImageQueuePresenter.ApplyAnomalyReviewStatusToItem(
                    FindImageQueueItem(activeImagePath),
                    anomalyImageReviewSession.GetStatus(activeImagePath, isAnomalyPurpose: true));
            }
            else
            {
                ApplyReviewStatusToItem(FindImageQueueItem(activeImagePath), status);
            }
            RefreshImageQueueViewAfterItemStateChange();
            UpdateImageQueueStatusText();
        }

        internal bool ApplyActiveAnomalyClassification(IReadOnlyList<YoloWorkerSmokeCandidate> candidates)
        {
            return ApplyAnomalyClassificationToImage(
                activeImagePath,
                Path.GetFileNameWithoutExtension(activeImagePath),
                candidates,
                saveReviewStatus: true);
        }

        internal bool ApplyAnomalyClassificationToImage(
            string imagePath,
            string imageName,
            IReadOnlyList<YoloWorkerSmokeCandidate> candidates,
            bool saveReviewStatus)
        {
            if (!IsAnomalyDatasetPurpose() || string.IsNullOrWhiteSpace(imagePath))
            {
                return false;
            }

            AnomalyClassificationResult result = anomalyImageReviewSession.ApplyClassification(
                new AnomalyClassificationRequest(
                    imagePath,
                    imageName,
                    candidates,
                    AnomalyClassificationOptionsSnapshot.From(
                        projectData.ProjectSettings.AnomalyClassification.ToDecisionOptions()),
                    saveReviewStatus),
                projectData);
            if (!result.IsMapped)
            {
                return false;
            }

            WpfImageQueuePresenter.ApplyAnomalyReviewStatusToItem(FindImageQueueItem(imagePath), result.Status);
            UpdateImageQueueStatusText();
            return true;
        }

        internal void MarkActiveImageConfirmed()
        {
            if (!imageQualityReviewWorkflowService.CanReview(projectData)) return;

            if (string.IsNullOrWhiteSpace(activeImagePath))
            {
                return;
            }

            YoloImageReviewStatus status = imageQualityReviewWorkflowService.MarkConfirmed(activeImagePath, Path.GetFileNameWithoutExtension(activeImagePath));
            if (!activeImageSize.IsEmpty)
            {
                status = imageQualityReviewWorkflowService.RefreshLabelStatusAndReviewState(activeImagePath, activeImageSize, projectData, hasActiveCandidates: false) ?? status;
            }

            ApplyReviewStatusToItem(FindImageQueueItem(activeImagePath), status);
            imageQualityReviewWorkflowService.SaveReviewStatus(projectData);
            MarkActiveAnomalyImageAbnormal();
            // Live filtering observes the row properties above; a full Refresh resets and redraws the entire queue.
            UpdateImageQueueStatusText();
        }

        internal void MarkActiveImageNoCandidate()
        {
            if (!imageQualityReviewWorkflowService.CanReview(projectData)) return;

            if (string.IsNullOrWhiteSpace(activeImagePath))
            {
                return;
            }

            string imageName = Path.GetFileNameWithoutExtension(activeImagePath);
            YoloImageReviewStatus status = imageQualityReviewWorkflowService.SetDetectionNoCandidates(activeImagePath, imageName);
            if (!activeImageSize.IsEmpty)
            {
                status = imageQualityReviewWorkflowService.RefreshLabelStatusAndReviewState(activeImagePath, activeImageSize, projectData, hasActiveCandidates: false) ?? status;
            }

            ApplyReviewStatusToItem(FindImageQueueItem(activeImagePath), status);
            imageQualityReviewWorkflowService.SaveReviewStatus(projectData);
            MarkActiveAnomalyImageNormal();
            RefreshImageQueueViewAfterItemStateChange();
            UpdateImageQueueStatusText();
        }

        internal void MarkActiveImageSkippedOrCandidate()
        {
            if (!imageQualityReviewWorkflowService.CanReview(projectData)) return;

            if (string.IsNullOrWhiteSpace(activeImagePath))
            {
                return;
            }

            string imageName = Path.GetFileNameWithoutExtension(activeImagePath);
            YoloImageReviewStatus status = pendingDetectionCandidates.Count > 0
                ? imageQualityReviewWorkflowService.SetDetectionCandidates(activeImagePath, imageName, pendingDetectionCandidates.Count)
                : imageQualityReviewWorkflowService.MarkSkipped(activeImagePath, imageName);
            ApplyReviewStatusToItem(FindImageQueueItem(activeImagePath), status);
            imageQualityReviewWorkflowService.SaveReviewStatus(projectData);
            RefreshImageQueueViewAfterItemStateChange();
            UpdateImageQueueStatusText();
        }

        internal void ExecuteMarkQualityUnreviewedCommand()
        {
            SetActiveImageQualityReviewState(YoloImageQualityReviewState.Unreviewed);
        }

        internal void ExecuteMarkQualityNeedsFixCommand()
        {
            SetActiveImageQualityReviewState(YoloImageQualityReviewState.NeedsFix);
        }

        internal void ExecuteMarkQualityReviewedCommand()
        {
            SetActiveImageQualityReviewState(YoloImageQualityReviewState.Reviewed);
        }

        internal void ExecuteExportQualityReviewReportCommand()
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
                ImageQualityReviewReportResult report = imageQualityReviewWorkflowService.ExportQualityReviewReport(projectData);
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

        internal void SetActiveImageQualityReviewState(YoloImageQualityReviewState state)
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
                projectData);
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

        internal void InvalidateActiveImageQualityReviewAfterEdit()
        {
            if (!imageQualityReviewWorkflowService.CanReview(projectData)) return;

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
            imageQualityReviewWorkflowService.SaveReviewStatus(projectData);
        }

        internal void RefreshActiveImageQualityReviewPresentation()
        {
            WpfImageQueueItem item = FindImageQueueItem(activeImagePath);
            RefreshActiveImageQualityReviewPresentation(item, imageQualityReviewWorkflowService.GetOrCreate(activeImagePath));
        }

        internal void RefreshActiveImageQualityReviewPresentation(
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

        internal bool IsLabelQualityReviewPurpose()
        {
            // Row projection reads the configured purpose; runtime path discovery belongs to project setup.
            LabelingDatasetPurpose purpose = projectData.ProjectSettings?.DatasetPurpose ?? LabelingDatasetPurpose.ObjectDetection;
            return purpose == LabelingDatasetPurpose.ObjectDetection
                || purpose == LabelingDatasetPurpose.Segmentation;
        }

        internal bool IsAnomalyDatasetPurpose()
        {
            return projectData.ProjectSettings?.DatasetPurpose == LabelingDatasetPurpose.AnomalyDetection;
        }

        internal void MarkActiveAnomalyImageNormal()
        {
            MarkActiveAnomalyImageReviewState(AnomalyImageReviewState.Normal);
        }

        internal void MarkActiveAnomalyImageAbnormal()
        {
            MarkActiveAnomalyImageReviewState(AnomalyImageReviewState.Abnormal);
        }

        internal void MarkActiveAnomalyImageReviewState(AnomalyImageReviewState state)
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            TryMarkActiveAnomalyImageReviewState(state);
        }

        internal bool TryMarkActiveAnomalyImageReviewState(AnomalyImageReviewState state)
        {
            if (isApplicationCloseApproved)
            {
                return false;
            }

            if (!IsAnomalyDatasetPurpose() || string.IsNullOrWhiteSpace(activeImagePath))
            {
                return false;
            }

            string imageName = Path.GetFileNameWithoutExtension(activeImagePath);
            return MarkAnomalyImageReviewState(activeImagePath, imageName, state, saveReviewStatus: true);
        }

        internal void ExecuteMarkActiveAnomalyNormalAndNextCommand()
        {
            MarkActiveAnomalyImageAndOpenNext(AnomalyImageReviewState.Normal);
        }

        internal void ExecuteMarkActiveAnomalyAbnormalAndNextCommand()
        {
            MarkActiveAnomalyImageAndOpenNext(AnomalyImageReviewState.Abnormal);
        }

        internal void ExecuteClearActiveAnomalyReviewCommand()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            if (!IsAnomalyDatasetPurpose() || string.IsNullOrWhiteSpace(activeImagePath))
            {
                return;
            }

            if (!TryMarkActiveAnomalyImageReviewState(AnomalyImageReviewState.Unreviewed))
            {
                return;
            }
            SetDatasetStatus($"OK/NG 이미지 판정: 미판정으로 되돌림 / {Path.GetFileName(activeImagePath)}");
            AppendLog($"Anomaly image review cleared: {activeImagePath}");
        }

        internal void MarkActiveAnomalyImageAndOpenNext(AnomalyImageReviewState state)
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
            if (!TryMarkActiveAnomalyImageReviewState(state))
            {
                return;
            }
            string decisionText = state == AnomalyImageReviewState.Normal ? "정상(OK)" : "이상(NG)";
            SetDatasetStatus($"OK/NG 이미지 판정: {decisionText} 저장 / {Path.GetFileName(reviewedPath)}");
            AppendLog($"Anomaly image reviewed: {reviewedPath} / {state}");
            if (!TryOpenNextIncompleteQueueImage())
            {
                SetDatasetStatus("OK/NG 이미지 판정: 모든 이미지 판정 완료");
            }
        }

        internal bool MarkAnomalyImageReviewState(string imagePath, string imageName, AnomalyImageReviewState state, bool saveReviewStatus)
        {
            AnomalyImageReviewCommandResult result = anomalyImageReviewSession.Apply(
                imagePath,
                imageName,
                state,
                isAnomalyPurpose: IsAnomalyDatasetPurpose(),
                projectData,
                saveReviewStatus);
            if (!result.IsApplicable)
            {
                return false;
            }

            WpfImageQueuePresenter.ApplyAnomalyReviewStatusToItem(FindImageQueueItem(imagePath), result.Status);
            // Live filtering observes AnomalyReviewState/IsLabeled. Refresh() would reset and redraw every row.
            UpdateImageQueueStatusText();
            return true;
        }

        internal void RefreshImageQueuePurposePresentation()
        {
            bool isAnomalyPurpose = IsAnomalyDatasetPurpose();
            AnomalyImageReviewQueueProjection anomalyProjection = isAnomalyPurpose
                ? anomalyImageReviewSession.BuildQueue(
                    imageQueueItems.Select(queueItem => queueItem.ImagePath).ToList(),
                    isAnomalyPurpose: true)
                : null;
            foreach (WpfImageQueueItem item in imageQueueItems)
            {
                if (isAnomalyPurpose)
                {
                    WpfImageQueuePresenter.ApplyAnomalyReviewStatusToItem(item, anomalyProjection.GetStatus(item.ImagePath));
                }
                else
                {
                    ApplyReviewStatusToItemCore(
                        item,
                        imageQualityReviewWorkflowService.GetOrCreate(item.ImagePath),
                        refreshTrainingStepCompletion: false);
                }
            }

            ImageQueuePanelControl?.RefreshQueueView();
            UpdateImageQueueStatusText();
        }

        #endregion

    }

    internal sealed class ImageQueueReviewAdapterContext
    {
        internal Func<LabelingProjectData> DataProvider { get; init; }
        internal ImageQualityReviewWorkflowService ImageQualityReviewWorkflowService { get; init; }
        internal AnomalyImageReviewSession AnomalyImageReviewSession { get; init; }
        internal ImageQueueSelectionService ImageQueueSelectionService { get; init; }
        internal IList<WpfImageQueueItem> ImageQueueItems { get; init; }
        internal IReadOnlyDictionary<string, WpfImageQueueItem> ImageQueueItemsByPath { get; init; }
        internal Func<ICollectionView> ImageQueueViewProvider { get; init; }
        internal WpfImageQueuePanelViewModel ImageQueueViewModel { get; init; }
        internal WpfObjectReviewPanelViewModel ObjectReviewViewModel { get; init; }
        internal ComboBox ImageQueueFilterBox { get; init; }
        internal TextBox ImageQueueSearchBox { get; init; }
        internal DataGrid ImageQueueGrid { get; init; }
        internal WpfImageQueuePanel ImageQueuePanelControl { get; init; }
        internal AnnotationDirtyState AnnotationDirtyState { get; init; }
        internal Func<IReadOnlyList<YoloWorkerSmokeCandidate>> PendingDetectionCandidatesProvider { get; init; }
        internal Func<string> ActiveImagePathProvider { get; init; }
        internal Func<System.Drawing.Size> ActiveImageSizeProvider { get; init; }
        internal Func<bool> IsApplicationCloseApproved { get; init; }
        internal Func<bool> IsImageQueueSelectionSuppressed { get; init; }
        internal Action<bool> SetImageQueueSelectionSuppressed { get; init; }
        internal System.Windows.Threading.Dispatcher Dispatcher { get; init; }
        internal Action<string> AppendLog { get; init; }
        internal Action<string> SetDatasetStatus { get; init; }
        internal Action<string> SetModelStatus { get; init; }
        internal Action RefreshImageQueueViewAfterItemStateChange { get; init; }
        internal Func<bool> TryOpenNextIncompleteQueueImage { get; init; }
        internal Func<WpfImageQueueItem, bool> CanOpenQueueItem { get; init; }
        internal Action<bool> SetOpenSelectedImageEnabled { get; init; }
        internal Action<WpfImageQueueItem, bool> OpenSelectedQueueImage { get; init; }
        internal Action RefreshYoloTrainingStepCompletion { get; init; }
    }

}
