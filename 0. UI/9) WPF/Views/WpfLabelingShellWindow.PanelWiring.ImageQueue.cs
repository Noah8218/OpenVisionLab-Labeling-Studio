using OpenVisionLab.Mvvm.Behaviors;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;

namespace MvcVisionSystem
{
    public partial class WpfLabelingShellWindow
    {
        // Image queue panel wiring stays beside its filter/selection command bindings.
        private void InitializeImageQueuePanel()
        {
            ConfigureImageQueuePanelCommands();
            ImageQueueFilterBox.ItemsSource = WpfImageQueueFilterOption.CreateDefaults();
            ImageQueueFilterBox.SelectedIndex = 0;
            imageQueueView = CollectionViewSource.GetDefaultView(imageQueueItems);
            imageQueueView.Filter = item => ShouldShowImageQueueItem(item as WpfImageQueueItem);
            ConfigureImageQueueLiveFiltering();
            ImageQueueGrid.ItemsSource = imageQueueView;
            UpdateQueueQuickFilterButtons();
        }

        private void ConfigureImageQueueLiveFiltering()
        {
            if (!(imageQueueView is ICollectionViewLiveShaping liveShaping)
                || !liveShaping.CanChangeLiveFiltering)
            {
                return;
            }

            liveShaping.LiveFilteringProperties.Clear();
            liveShaping.LiveFilteringProperties.Add(nameof(WpfImageQueueItem.FileName));
            liveShaping.LiveFilteringProperties.Add(nameof(WpfImageQueueItem.IsLabeled));
            liveShaping.LiveFilteringProperties.Add(nameof(WpfImageQueueItem.IsSaveRequired));
            liveShaping.LiveFilteringProperties.Add(nameof(WpfImageQueueItem.ReviewState));
            liveShaping.LiveFilteringProperties.Add(nameof(WpfImageQueueItem.QualityReviewState));
            liveShaping.LiveFilteringProperties.Add(nameof(WpfImageQueueItem.AnomalyReviewState));
            liveShaping.IsLiveFiltering = true;
        }

        private void RefreshImageQueueViewAfterItemStateChange()
        {
            if (imageQueueView is ICollectionViewLiveShaping liveShaping
                && liveShaping.IsLiveFiltering == true)
            {
                return;
            }

            imageQueueView?.Refresh();
        }

        private void ConfigureImageQueuePanelCommands()
        {
            ImageQueueViewModel.ConfigureCommands(
                ExecuteLoadImageRootQueueCommand,
                ExecuteBrowseImageFolderCommand,
                ExecuteOpenCurrentImageFolderCommand,
                ExecuteRefreshImageQueueCommand,
                ExecuteNextUnlabeledQueueCommand,
                ExecuteOpenSelectedQueueImageCommand,
                ExecuteDetectSelectedQueueCommand,
                ExecuteBatchDetectQueueCommand,
                TemplateMatchingAutoLabelViewModel.RunBatch,
                ExecuteRetryFailedQueueCommand,
                ExecuteStopBatchQueueCommand,
                ExecuteQueueFilterUnfinishedCommand,
                ExecuteQueueFilterAllCommand,
                ExecuteQueueFilterCandidateCommand,
                ExecuteQueueFilterFailedCommand,
                ExecuteQueueFilterConfirmedCommand,
                ExecuteQueueFilterSkippedCommand,
                ExecuteQueueFilterNoCandidateCommand,
                ExecuteSelectedQueueItemChanged,
                selected => ImageQueueFilterBox_SelectionChanged(ImageQueueFilterBox, selected),
                text => ImageQueueSearchBox_TextChanged(ImageQueueSearchBox, text),
                selected => ImageQueueGrid_SelectionChanged(ImageQueueGrid, selected),
                () => ImageQueueGrid_MouseDoubleClick(ImageQueueGrid),
                ExecuteApplyAnomalyFolderStateSuggestionCommand,
                ExecuteDismissAnomalyFolderStateSuggestionCommand,
                ExecuteMarkActiveAnomalyNormalAndNextCommand,
                ExecuteMarkActiveAnomalyAbnormalAndNextCommand,
                ExecuteClearActiveAnomalyReviewCommand);
            RefreshAttachedCommandBindings(ImageQueueFilterBox, InputCommandBehaviors.SelectedItemChangedCommandProperty);
            RefreshAttachedCommandBindings(ImageQueueSearchBox, InputCommandBehaviors.TextInputCommandProperty);
            RefreshAttachedCommandBindings(
                ImageQueueGrid,
                InputCommandBehaviors.SelectedItemChangedCommandProperty,
                InputCommandBehaviors.MouseDoubleClickInputCommandProperty);
            SeedImageQueueInputCommands();
        }

        private void RegisterImageQueuePanelNames()
        {
            RegisterImageQueueName(nameof(ImageQueueFilterBox), ImageQueueFilterBox);
            RegisterImageQueueName(nameof(ImageQueueSearchBox), ImageQueueSearchBox);
            RegisterImageQueueName(nameof(ImageQueueGrid), ImageQueueGrid);
            RegisterImageQueueName(nameof(BatchStatusText), BatchStatusText);
            RegisterImageQueueName(nameof(BatchProgressBar), BatchProgressBar);
            RegisterImageQueueName(nameof(CurrentImageFolderPathText), CurrentImageFolderPathText);
            RegisterImageQueueName(nameof(OpenCurrentImageFolderButton), OpenCurrentImageFolderButton);
            RegisterImageQueueName(nameof(OpenSelectedQueueImageButton), OpenSelectedQueueImageButton);
            RegisterImageQueueName(nameof(DetectSelectedQueueButton), DetectSelectedQueueButton);
            RegisterImageQueueName(nameof(BatchDetectQueueButton), BatchDetectQueueButton);
            RegisterImageQueueName(nameof(TemplateBatchQueueButton), TemplateBatchQueueButton);
            RegisterImageQueueName(nameof(RetryFailedQueueButton), RetryFailedQueueButton);
            RegisterImageQueueName(nameof(StopBatchQueueButton), StopBatchQueueButton);
            RegisterImageQueueName(nameof(QueueFilterUnfinishedButton), QueueFilterUnfinishedButton);
            RegisterImageQueueName(nameof(QueueFilterAllButton), QueueFilterAllButton);
            RegisterImageQueueName(nameof(QueueFilterCandidateButton), QueueFilterCandidateButton);
            RegisterImageQueueName(nameof(QueueFilterFailedButton), QueueFilterFailedButton);
            RegisterImageQueueName(nameof(QueueFilterConfirmedButton), QueueFilterConfirmedButton);
            RegisterImageQueueName(nameof(QueueFilterSkippedButton), QueueFilterSkippedButton);
            RegisterImageQueueName(nameof(QueueFilterNoCandidateButton), QueueFilterNoCandidateButton);
            RegisterImageQueueName(nameof(QueueFilterUnfinishedText), QueueFilterUnfinishedText);
            RegisterImageQueueName(nameof(QueueFilterAllText), QueueFilterAllText);
            RegisterImageQueueName(nameof(QueueFilterCandidateText), QueueFilterCandidateText);
            RegisterImageQueueName(nameof(QueueFilterFailedText), QueueFilterFailedText);
            RegisterImageQueueName(nameof(QueueFilterConfirmedText), QueueFilterConfirmedText);
            RegisterImageQueueName(nameof(QueueFilterSkippedText), QueueFilterSkippedText);
            RegisterImageQueueName(nameof(QueueFilterNoCandidateText), QueueFilterNoCandidateText);
        }

        private void RegisterImageQueueName(string name, FrameworkElement element)
        {
            if (!string.IsNullOrWhiteSpace(name) && element != null)
            {
                RegisterName(name, element);
            }
        }

    }
}
