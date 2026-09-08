using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using MvcVisionSystem.Yolo;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using OpenVisionLab;

namespace MvcVisionSystem
{
    // Responsibility group: image queue commands and detail refresh lifecycle.
    // These members remain WPF Window adapters; independent policy belongs in services.
    public partial class WpfLabelingShellWindow
    {
        #region ImageQueueCommands
        // ponytail: one private seam keeps queue-command tests independent of image decoding and GPU canvas upload.
        private Func<string, bool> imageQueueNavigationLoadOverride = null;

        // Queue commands are invoked through WpfImageQueuePanelViewModel; the shell keeps only workflow orchestration here.
        private void ExecuteLoadImageRootQueueCommand()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            EnsureProjectSettings();
            string imageRootPath = global.Data.ProjectSettings.ResolveImageRootPath();
            if (string.IsNullOrWhiteSpace(imageRootPath) && viewModels.IsModelWorkflowCreated)
            {
                imageRootPath = viewModels.YoloModelSettingsViewModel?.ImageRootPath?.Trim();
            }
            if (string.IsNullOrWhiteSpace(imageRootPath) || !Directory.Exists(imageRootPath))
            {
                AppendLog($"설정된 이미지 루트가 없습니다: {imageRootPath}");
                return;
            }

            if (!string.Equals(global.Data.ProjectSettings.ResolveImageRootPath(), imageRootPath, StringComparison.OrdinalIgnoreCase))
            {
                global.Data.ProjectSettings.ImageRootPath = imageRootPath;
                SaveCurrentImageRootToRecipe(imageRootPath);
            }

            _ = LoadImageQueueFromRootAsync(imageRootPath, activeImagePath, loadFirstImage: true);
        }

        private void ExecuteBrowseImageFolderCommand()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            string currentRoot = Directory.Exists(currentImageRoot) ? currentImageRoot : string.Empty;
            if (!TryPickFolder("이미지 폴더 선택", currentRoot, out string selectedPath))
            {
                return;
            }

            if (isApplicationCloseApproved)
            {
                return;
            }

            EnsureProjectSettings();
            global.Data.ProjectSettings.ImageRootPath = selectedPath;
            SaveCurrentImageRootToRecipe(selectedPath);
            _ = LoadImageQueueFromRootAsync(selectedPath, string.Empty, loadFirstImage: true);
            RefreshShellDatasetContext();
        }

        private void SaveCurrentImageRootToRecipe(string selectedPath)
        {
            string recipeName = GetCurrentRecipeName();
            if (string.IsNullOrWhiteSpace(recipeName))
            {
                AppendLog($"\uC774\uBBF8\uC9C0 \uD3F4\uB354 \uC120\uD0DD: {selectedPath}");
                return;
            }

            try
            {
                // Image folder is part of the dataset context. Persist it immediately
                // so switching away and back reloads the right queue for this recipe.
                projectRecipeSessionService.Save(global.Data, recipeName);

                PopulateYoloEditorFields();
                PopulateProjectConfigPanelFields();
                AppendLog($"\uC774\uBBF8\uC9C0 \uD3F4\uB354 \uC800\uC7A5: {selectedPath}");
            }
            catch (Exception ex)
            {
                AppendLog($"\uC774\uBBF8\uC9C0 \uD3F4\uB354 \uC800\uC7A5 \uC2E4\uD328: {ex.Message}");
            }
        }

        private void ExecuteOpenCurrentImageFolderCommand()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            string root = Directory.Exists(currentImageRoot)
                ? currentImageRoot
                : global.Data.ProjectSettings?.ResolveImageRootPath();
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                AppendLog($"현재 이미지 폴더를 열 수 없습니다: {root}");
                ImageQueueViewModel?.SetCurrentImageFolder(root, canOpenFolder: false);
                return;
            }

            // This is separate from Browse so users can inspect the loaded image folder without changing the queue root.
            Process.Start(new ProcessStartInfo
            {
                FileName = root,
                UseShellExecute = true
            });
        }

        private void ExecuteRefreshImageQueueCommand()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            string root = Directory.Exists(currentImageRoot)
                ? currentImageRoot
                : global.Data.ProjectSettings?.ResolveImageRootPath();
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                AppendLog($"이미지 루트가 없습니다: {root}");
                return;
            }

            _ = LoadImageQueueFromRootAsync(root, activeImagePath, loadFirstImage: imageQueueItems.Count == 0);
        }

        private void ExecuteNextUnlabeledQueueCommand()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            if (!TryOpenNextIncompleteQueueImage())
            {
                AppendLog("현재 큐에 남은 미완료 이미지가 없습니다.");
            }
        }

        private bool TryOpenNextIncompleteQueueImage()
        {
            return TryOpenNextIncompleteQueueImage(activeImagePath);
        }

        private bool TryOpenNextIncompleteQueueImage(string currentImagePath)
        {
            IReadOnlyList<string> orderedPaths = imageQueueItems.Select(item => item.ImagePath).ToList();
            if (IsAnomalyDatasetPurpose())
            {
                AnomalyImageReviewNextResult nextResult = anomalyImageReviewSession.FindNextUnreviewed(
                    orderedPaths,
                    currentImagePath,
                    isAnomalyPurpose: true);
                if (nextResult.HasNextImage)
                {
                    bool loaded = imageQueueNavigationLoadOverride?.Invoke(nextResult.NextImagePath)
                        ?? TryLoadImage(
                            nextResult.NextImagePath,
                            populateQueue: false,
                            refreshQueueDetails: false,
                            refreshActiveStatus: false,
                            appendLoadLog: false);
                    if (loaded)
                    {
                        // The queue already contains this image. Keep its rows intact and move only the active selection.
                        SelectImageQueueItem(nextResult.NextImagePath);
                        return true;
                    }

                    return false;
                }

                return false;
            }

            if (imageQualityReviewWorkflowService.TryFindNextUnlabeled(orderedPaths, currentImagePath, out string nextImagePath))
            {
                SelectImageQueueItem(nextImagePath);
                TryLoadImage(nextImagePath);
                return true;
            }

            return false;
        }

        private void FinishQueueCompletionAndGuideDatasetCheck()
        {
            // Completing the last image should advance the user's mental model from
            // drawing labels to checking whether the dataset is ready for training.
            RefreshTrainingReadinessPanel(refreshYaml: true);
            WpfLearningStepItem saveStep = LearningWorkflowViewModel?.LearningSteps
                .FirstOrDefault(step => step.Step == WpfLearningStep.Save);
            if (saveStep != null)
            {
                LearningWorkflowViewModel.SelectedStep = saveStep;
            }

            SetModelStatus("이미지 완료: 데이터셋 점검 결과를 확인하세요.");
            AppendLog("모든 이미지 완료: 데이터셋 점검을 실행했습니다. 다음 단계로 이동할 수 있습니다.");
            RefreshCanvasWorkflowContext();
        }

        private void ImageQueueFilterBox_SelectionChanged(object sender, object selectedItem)
        {
            imageQueueView?.Refresh();
            UpdateImageQueueStatusText();
        }

        private void ExecuteQueueFilterAllCommand()
        {
            SetImageQueueFilter(WpfImageQueueFilter.All);
        }

        private void ExecuteQueueFilterUnfinishedCommand()
        {
            SetImageQueueFilter(WpfImageQueueFilter.Unlabeled);
        }

        private void ExecuteQueueFilterCandidateCommand()
        {
            SetImageQueueFilter(WpfImageQueueFilter.Candidate);
        }

        private void ExecuteQueueFilterFailedCommand()
        {
            SetImageQueueFilter(WpfImageQueueFilter.Failed);
        }

        private void ExecuteQueueFilterConfirmedCommand()
        {
            SetImageQueueFilter(WpfImageQueueFilter.Confirmed);
        }

        private void ExecuteQueueFilterSkippedCommand()
        {
            SetImageQueueFilter(WpfImageQueueFilter.Skipped);
        }

        private void ExecuteQueueFilterNoCandidateCommand()
        {
            SetImageQueueFilter(WpfImageQueueFilter.NoCandidate);
        }

        private void SetImageQueueFilter(WpfImageQueueFilter filter)
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

            imageQueueView?.Refresh();
            UpdateImageQueueStatusText();
        }

        private void ImageQueueSearchBox_TextChanged(object sender, string searchText)
        {
            imageQueueView?.Refresh();
            SelectSingleVisibleQueueSearchResult();
            UpdateImageQueueStatusText();
        }

        private void SelectSingleVisibleQueueSearchResult()
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

        private void ImageQueueGrid_SelectionChanged(object sender, object selectedItem)
        {
            WpfImageQueueItem item = selectedItem as WpfImageQueueItem;
            if (ImageQueueViewModel == null)
            {
                ExecuteSelectedQueueItemChanged(item);
                return;
            }

            // The SelectedItem binding normally updates the ViewModel first. The attached
            // command is only a fallback for event-order edge cases and must not open twice.
            if (!ReferenceEquals(ImageQueueViewModel.SelectedQueueItem, item))
            {
                ImageQueueViewModel.SelectedQueueItem = item;
            }
        }

        private void ExecuteSelectedQueueItemChanged(WpfImageQueueItem item)
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
            TryOpenSelectedQueueImage(selectedItem, skipIfAlreadyActive: true);
        }

        private void ImageQueueGrid_MouseDoubleClick(object sender)
        {
            TryOpenSelectedQueueImage(skipIfAlreadyActive: false);
        }

        private void ExecuteOpenSelectedQueueImageCommand()
        {
            if (isApplicationCloseApproved)
            {
                return;
            }

            TryOpenSelectedQueueImage(skipIfAlreadyActive: false);
        }

        private bool TryOpenSelectedQueueImage(bool skipIfAlreadyActive = false)
        {
            return TryOpenSelectedQueueImage(GetOpenSelectedQueueSelection(), skipIfAlreadyActive);
        }

        private ImageQueueOpenSelection GetOpenSelectedQueueSelection()
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

            return imageQueueSelectionService.ResolveOpenSelection(candidates, global.Data);
        }

        private WpfImageQueueItem FindSingleSearchMatchedQueueItem()
        {
            // Open is a deliberate user command. When search text uniquely identifies
            // one filtered row, prefer that row even if DataGrid focus/selection is stale.
            return ImageQueueFilterService.FindSingleSearchMatch(
                imageQueueItems,
                ImageQueueSearchBox?.Text,
                GetSelectedImageQueueFilter());
        }

        private bool TryOpenSelectedQueueImage(WpfImageQueueItem item, bool skipIfAlreadyActive = false)
        {
            return TryOpenSelectedQueueImage(
                imageQueueSelectionService.ResolveOpenSelection(new[] { item }, global.Data),
                skipIfAlreadyActive);
        }

        private bool TryOpenSelectedQueueImage(ImageQueueOpenSelection selection, bool skipIfAlreadyActive = false)
        {
            if (selection?.CanOpen != true)
            {
                AppendLog(BuildOpenQueueSelectionFailureMessage());
                return false;
            }

            WpfImageQueueItem item = selection.Item;
            string openImagePath = selection.OpenImagePath;
            UpdateSelectedQueueImageButton(item);

            if (skipIfAlreadyActive
                && string.Equals(openImagePath, activeImagePath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            bool loaded = TryLoadImage(
                openImagePath,
                populateQueue: false,
                refreshQueueDetails: false,
                refreshActiveStatus: false,
                appendLoadLog: false);
            if (loaded)
            {
                UpdateSelectedQueueImageButton(item);
            }

            return loaded;
        }

        private bool TryOpenAdjacentQueueImage(int direction)
        {
            if (direction == 0 || imageQueueItems.Count == 0)
            {
                return false;
            }

            WpfImageQueueItem targetItem = imageQueueSelectionService.FindAdjacentOpenableItem(
                GetVisibleQueueItems(),
                activeImagePath,
                ImageQueueViewModel?.SelectedQueueItem?.ImagePath,
                direction,
                CanOpenQueueItem);
            if (targetItem == null)
            {
                return false;
            }

            SelectImageQueueItem(targetItem.ImagePath);
            return TryOpenSelectedQueueImage(targetItem, skipIfAlreadyActive: true);
        }

        private string BuildOpenQueueSelectionFailureMessage()
        {
            string searchText = ImageQueueSearchBox?.Text?.Trim() ?? string.Empty;
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

        private int CountVisibleQueueItems(int limit)
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

        private int CountSearchMatchedQueueItems(string searchText, int limit)
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

        private void UpdateSelectedQueueImageButton(WpfImageQueueItem item)
        {
            bool canOpenSelectedImage = CanOpenQueueItem(item);

            if (ImageQueueViewModel != null)
            {
                ImageQueueViewModel.SetSelectedImageAvailability(canOpenSelectedImage);
                return;
            }

            SetControlEnabled(OpenSelectedQueueImageButton, canOpenSelectedImage);
        }

        private bool CanOpenQueueItem(WpfImageQueueItem item)
        {
            return imageQueueSelectionService.TryResolveOpenImagePath(item, global.Data, out _);
        }


        #endregion

        #region ImageQueueDetailRefresh
        // Queue detail scanning stays in the concrete service. Only a bounded set of changed rows is applied at background priority.
        private Task CompleteImageQueueDetailRefreshAsync(CancellationToken token)
        {
            if (isApplicationCloseApproved || token.IsCancellationRequested)
            {
                return Task.CompletedTask;
            }

            return Dispatcher.InvokeAsync(
                () => CompleteImageQueueDetailRefresh(token),
                DispatcherPriority.Background,
                token).Task;
        }

        private async Task ApplyImageQueueDetailBatchAsync(
            IReadOnlyList<ImageQueueDetailRefreshResult> results,
            IReadOnlyDictionary<string, WpfImageQueueItem> itemLookup,
            int loadedCount,
            int totalCount,
            CancellationToken token)
        {
            if (results == null || results.Count == 0)
            {
                return;
            }

            if (isApplicationCloseApproved)
            {
                return;
            }

            await Dispatcher.InvokeAsync(
                () => ApplyImageQueueDetailBatch(results, itemLookup, loadedCount, totalCount, token),
                DispatcherPriority.Background,
                token).Task.ConfigureAwait(false);
        }

        private void ApplyImageQueueDetailBatch(
            IReadOnlyList<ImageQueueDetailRefreshResult> results,
            IReadOnlyDictionary<string, WpfImageQueueItem> itemLookup,
            int loadedCount,
            int totalCount,
            CancellationToken token)
        {
            if (isApplicationCloseApproved || token.IsCancellationRequested)
            {
                return;
            }

            foreach (ImageQueueDetailRefreshResult result in results ?? Array.Empty<ImageQueueDetailRefreshResult>())
            {
                if (result == null
                    || itemLookup == null
                    || !itemLookup.TryGetValue(result.ImagePath, out WpfImageQueueItem item)
                    || item == null)
                {
                    continue;
                }

                if (result.Error != null)
                {
                    item.LabelStatus = "\uC0C1\uD0DC \uD655\uC778 \uC2E4\uD328";
                    item.DetectStatus = "\uB300\uAE30";
                    AppendLog($"Image status failed: {Path.GetFileName(item.ImagePath)}  {result.Error.Message}");
                    continue;
                }

                ApplyImageQueueDetail(item, result.Detail);
            }

            UpdateImageQueueDetailProgress(loadedCount, totalCount);
        }

        private void CompleteImageQueueDetailRefresh(CancellationToken token)
        {
            if (isApplicationCloseApproved || token.IsCancellationRequested)
            {
                return;
            }

            // One final full view refresh makes the active filter exact without re-evaluating all rows for every detail batch.
            imageQueueView?.Refresh();
            UpdateImageQueueStatusText();
            RefreshYoloTrainingStepCompletion();
        }

        private void UpdateImageQueueDetailProgress(int loadedCount, int totalCount)
        {
            int total = Math.Max(0, totalCount);
            int loaded = Math.Min(Math.Max(0, loadedCount), total);
            string activeText = string.IsNullOrWhiteSpace(activeImagePath)
                ? string.Empty
                : string.Format(
                    CultureInfo.InvariantCulture,
                    OpenVisionLanguageService.T("WpfShell.Status.DatasetDetailActiveImage"),
                    Path.GetFileName(activeImagePath));
            SetDatasetStatus(string.Format(
                CultureInfo.InvariantCulture,
                OpenVisionLanguageService.T("WpfShell.Status.DatasetDetailProgress"),
                imageQueueItems.Count,
                total,
                loaded,
                total,
                activeText));
        }

        private void ApplyImageQueueDetail(WpfImageQueueItem item, WpfImageQueueDetail detail)
        {
            if (item == null || detail == null)
            {
                return;
            }

            item.Dimensions = ImageQueueDetailLoader.FormatImageSize(detail.ImageSize);
            if (IsAnomalyDatasetPurpose())
            {
                WpfImageQueuePresenter.ApplyAnomalyReviewStatusToItem(
                    item,
                    anomalyImageReviewSession.GetStatus(item.ImagePath, isAnomalyPurpose: true));
                return;
            }
            ApplyReviewStatusToItemCore(item, detail.ReviewStatus, refreshTrainingStepCompletion: false);
        }

        private void ApplyReviewStatusToItem(WpfImageQueueItem item, YoloImageReviewStatus status)
        {
            ApplyReviewStatusToItemCore(item, status, refreshTrainingStepCompletion: true);
        }

        private void ApplyReviewStatusToItemCore(
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
                RefreshYoloTrainingStepCompletion();
            }
        }

        private void CancelImageQueueCatalogLoad(bool waitForCompletion)
        {
            Task catalogTask = imageQueueCatalogLoadCoordinator.Cancel();
            imageQualityReviewWorkflowService.CancelCatalogLoad();
            anomalyImageReviewSession.CancelCatalogLoad();
            if (waitForCompletion)
            {
                WaitForImageQueueDetailRefresh(catalogTask);
            }
        }

        private void CancelImageQueueDetailRefresh(bool waitForCompletion)
        {
            Task detailTask = imageQueueDetailRefreshCoordinator.Cancel();
            if (waitForCompletion)
            {
                WaitForImageQueueDetailRefresh(detailTask);
            }
        }

        private void WaitForImageQueueDetailRefresh(Task detailTask)
        {
            if (detailTask == null || detailTask.IsCompleted)
            {
                return;
            }

            if (!Dispatcher.CheckAccess())
            {
                try
                {
                    detailTask.Wait(TimeSpan.FromSeconds(2));
                }
                catch (AggregateException)
                {
                }

                return;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            while (!detailTask.IsCompleted && stopwatch.Elapsed < TimeSpan.FromSeconds(2))
            {
                // Detail refresh resumes on the UI dispatcher; pump briefly so close can release image file handles.
                var frame = new DispatcherFrame();
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
                Dispatcher.PushFrame(frame);
            }

            if (detailTask.IsFaulted)
            {
                _ = detailTask.Exception;
            }
        }
        #endregion

    }
}
