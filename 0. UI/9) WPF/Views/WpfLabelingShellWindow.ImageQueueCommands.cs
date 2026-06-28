using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DrawingSize = System.Drawing.Size;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Queue commands are invoked through WpfImageQueuePanelViewModel; the shell keeps only workflow orchestration here.
        private void ExecuteLoadImageRootQueueCommand()
        {
            EnsureProjectSettings();
            string imageRootPath = global.Data.ProjectSettings.PythonModel.ImageRootPath;
            if (string.IsNullOrWhiteSpace(imageRootPath) || !Directory.Exists(imageRootPath))
            {
                AppendLog($"설정된 이미지 루트가 없습니다: {imageRootPath}");
                return;
            }

            LoadImageQueueFromRoot(imageRootPath, activeImagePath, loadFirstImage: true);
        }

        private void ExecuteBrowseImageFolderCommand()
        {
            string currentRoot = Directory.Exists(currentImageRoot) ? currentImageRoot : string.Empty;
            if (!TryPickFolder("이미지 폴더 선택", currentRoot, out string selectedPath))
            {
                return;
            }

            EnsureProjectSettings();
            global.Data.ProjectSettings.PythonModel.ImageRootPath = selectedPath;
            LoadImageQueueFromRoot(selectedPath, string.Empty, loadFirstImage: true);
        }

        private void ExecuteRefreshImageQueueCommand()
        {
            string root = Directory.Exists(currentImageRoot)
                ? currentImageRoot
                : global.Data.ProjectSettings?.PythonModel?.ImageRootPath;
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                AppendLog($"이미지 루트가 없습니다: {root}");
                return;
            }

            LoadImageQueueFromRoot(root, activeImagePath, loadFirstImage: imageQueueItems.Count == 0);
        }

        private void ExecuteNextUnlabeledQueueCommand()
        {
            if (!TryOpenNextIncompleteQueueImage())
            {
                AppendLog("현재 큐에 남은 미완료 이미지가 없습니다.");
            }
        }

        private bool TryOpenNextIncompleteQueueImage()
        {
            IReadOnlyList<string> orderedPaths = imageQueueItems.Select(item => item.ImagePath).ToList();
            if (imageReviewStatus.TryFindNextUnlabeled(orderedPaths, activeImagePath, out string nextImagePath))
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
            UpdateQueueQuickFilterButtons();
            UpdateImageQueueStatusText();
        }

        private void ExecuteQueueFilterAllCommand()
        {
            SetImageQueueFilter(WpfImageQueueFilter.All);
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
            UpdateQueueQuickFilterButtons();
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

            List<WpfImageQueueItem> visibleItems = imageQueueView
                .Cast<object>()
                .OfType<WpfImageQueueItem>()
                .Take(2)
                .ToList();
            if (visibleItems.Count != 1)
            {
                return;
            }

            // Search narrows the queue for reopen/review work. If exactly one row remains,
            // select it so the visible Open action works without a fragile extra row click.
            WpfImageQueueItem item = visibleItems[0];
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
            ExecuteSelectedQueueItemChanged(selectedItem as WpfImageQueueItem);
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
            if (ReferenceEquals(selectedItem, ImageQueueGrid?.SelectedItem))
            {
                TryOpenSelectedQueueImage(skipIfAlreadyActive: true);
            }
            else
            {
                TryOpenSelectedQueueImage(selectedItem, skipIfAlreadyActive: true);
            }
        }

        private void ImageQueueGrid_MouseDoubleClick(object sender)
        {
            TryOpenSelectedQueueImage(skipIfAlreadyActive: false);
        }

        private void ExecuteOpenSelectedQueueImageCommand()
        {
            TryOpenSelectedQueueImage(skipIfAlreadyActive: false);
        }

        private bool TryOpenSelectedQueueImage(bool skipIfAlreadyActive = false)
        {
            return TryOpenSelectedQueueImage(GetOpenSelectedQueueItem(), skipIfAlreadyActive);
        }

        private WpfImageQueueItem GetOpenSelectedQueueItem()
        {
            WpfImageQueueItem selectedItem = ImageQueueGrid?.SelectedItem as WpfImageQueueItem;
            if (CanOpenQueueItem(selectedItem))
            {
                return selectedItem;
            }

            selectedItem = ImageQueueViewModel?.SelectedQueueItem;
            if (CanOpenQueueItem(selectedItem))
            {
                return selectedItem;
            }

            selectedItem = FindSingleSearchMatchedQueueItem();
            if (CanOpenQueueItem(selectedItem))
            {
                return selectedItem;
            }

            if (imageQueueView == null)
            {
                return null;
            }

            // UIAutomation and keyboard focus can leave DataGrid.SelectedItem unset while
            // a filtered single row is plainly visible. In that case the visible row is
            // the operator's intended target for the Open action.
            imageQueueView.Refresh();
            List<WpfImageQueueItem> visibleItems = imageQueueView
                .Cast<object>()
                .OfType<WpfImageQueueItem>()
                .Take(2)
                .ToList();
            return visibleItems.Count == 1 ? visibleItems[0] : null;
        }

        private WpfImageQueueItem FindSingleSearchMatchedQueueItem()
        {
            string searchText = ImageQueueSearchBox?.Text;
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return null;
            }

            // Open is a deliberate user command. When search text uniquely identifies
            // one filtered row, prefer that row even if DataGrid focus/selection is stale.
            WpfImageQueueFilter filter = GetSelectedImageQueueFilter();
            List<WpfImageQueueItem> matches = imageQueueItems
                .Where(item => WpfImageQueueFilterService.ShouldShow(item, searchText, filter))
                .Take(2)
                .ToList();
            return matches.Count == 1 ? matches[0] : null;
        }

        private bool TryOpenSelectedQueueImage(WpfImageQueueItem item, bool skipIfAlreadyActive = false)
        {
            if (!imageQueueSelectionService.TryResolveOpenImagePath(item, global.Data, out string openImagePath))
            {
                AppendLog(BuildOpenQueueSelectionFailureMessage());
                return false;
            }

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

        private string BuildOpenQueueSelectionFailureMessage()
        {
            string searchText = ImageQueueSearchBox?.Text?.Trim() ?? string.Empty;
            string gridSelection = (ImageQueueGrid?.SelectedItem as WpfImageQueueItem)?.FileName ?? "-";
            string viewModelSelection = ImageQueueViewModel?.SelectedQueueItem?.FileName ?? "-";
            int visibleCount = CountVisibleQueueItems(limit: 3);
            int searchMatchCount = CountSearchMatchedQueueItems(searchText, limit: 3);
            return $"\uC5F4 \uC774\uBBF8\uC9C0\uB97C \uC120\uD0DD\uD558\uC138\uC694. \uAC80\uC0C9='{searchText}' \uD45C\uC2DC={FormatLimitedQueueCount(visibleCount)} \uAC80\uC0C9\uC77C\uCE58={FormatLimitedQueueCount(searchMatchCount)} \uC120\uD0DD={gridSelection} VM={viewModelSelection}";
        }

        private int CountVisibleQueueItems(int limit)
        {
            if (imageQueueView == null)
            {
                return 0;
            }

            imageQueueView.Refresh();
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

            WpfImageQueueFilter filter = GetSelectedImageQueueFilter();
            return imageQueueItems
                .Where(item => WpfImageQueueFilterService.ShouldShow(item, searchText, filter))
                .Take(Math.Max(1, limit))
                .Count();
        }

        private static string FormatLimitedQueueCount(int count)
        {
            return count >= 3 ? "3+" : count.ToString(System.Globalization.CultureInfo.InvariantCulture);
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


        private static bool TryReadImageSize(string imagePath, out DrawingSize imageSize, out string error)
        {
            return WpfImageQueueDetailLoader.TryReadImageSize(imagePath, out imageSize, out error);
        }
    }
}
