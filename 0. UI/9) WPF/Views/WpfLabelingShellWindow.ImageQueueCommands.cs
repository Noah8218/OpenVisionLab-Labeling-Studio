using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            SaveCurrentImageRootToRecipe(selectedPath);
            LoadImageQueueFromRoot(selectedPath, string.Empty, loadFirstImage: true);
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
                global.Data.SaveConfig(recipeName);
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
            string root = Directory.Exists(currentImageRoot)
                ? currentImageRoot
                : global.Data.ProjectSettings?.PythonModel?.ImageRootPath;
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

            WpfImageQueueItem item = WpfImageQueueFilterService.FindSingleItem(imageQueueView
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
            return TryOpenSelectedQueueImage(GetOpenSelectedQueueSelection(), skipIfAlreadyActive);
        }

        private WpfImageQueueOpenSelection GetOpenSelectedQueueSelection()
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
                // the operator's intended target for the Open action.
                imageQueueView.Refresh();
                candidates.Add(WpfImageQueueFilterService.FindSingleItem(imageQueueView
                    .Cast<object>()
                    .OfType<WpfImageQueueItem>()));
            }

            return imageQueueSelectionService.ResolveOpenSelection(candidates, global.Data);
        }

        private WpfImageQueueItem FindSingleSearchMatchedQueueItem()
        {
            // Open is a deliberate user command. When search text uniquely identifies
            // one filtered row, prefer that row even if DataGrid focus/selection is stale.
            return WpfImageQueueFilterService.FindSingleSearchMatch(
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

        private bool TryOpenSelectedQueueImage(WpfImageQueueOpenSelection selection, bool skipIfAlreadyActive = false)
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

            return WpfImageQueueFilterService.CountSearchMatches(
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


        private static bool TryReadImageSize(string imagePath, out DrawingSize imageSize, out string error)
        {
            return WpfImageQueueDetailLoader.TryReadImageSize(imagePath, out imageSize, out error);
        }
    }
}
