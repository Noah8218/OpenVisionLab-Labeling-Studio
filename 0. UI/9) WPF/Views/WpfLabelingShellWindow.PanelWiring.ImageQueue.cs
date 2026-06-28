using OpenVisionLab.Mvvm.Behaviors;
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
            ImageQueueGrid.ItemsSource = imageQueueView;
            UpdateQueueQuickFilterButtons();
        }

        private void ConfigureImageQueuePanelCommands()
        {
            ImageQueueViewModel.ConfigureCommands(
                ExecuteLoadImageRootQueueCommand,
                ExecuteBrowseImageFolderCommand,
                ExecuteRefreshImageQueueCommand,
                ExecuteNextUnlabeledQueueCommand,
                ExecuteOpenSelectedQueueImageCommand,
                ExecuteDetectSelectedQueueCommand,
                ExecuteBatchDetectQueueCommand,
                ExecuteRetryFailedQueueCommand,
                ExecuteStopBatchQueueCommand,
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
                () => ImageQueueGrid_MouseDoubleClick(ImageQueueGrid));
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
            RegisterImageQueueName(nameof(OpenSelectedQueueImageButton), OpenSelectedQueueImageButton);
            RegisterImageQueueName(nameof(DetectSelectedQueueButton), DetectSelectedQueueButton);
            RegisterImageQueueName(nameof(BatchDetectQueueButton), BatchDetectQueueButton);
            RegisterImageQueueName(nameof(RetryFailedQueueButton), RetryFailedQueueButton);
            RegisterImageQueueName(nameof(StopBatchQueueButton), StopBatchQueueButton);
            RegisterImageQueueName(nameof(QueueFilterAllButton), QueueFilterAllButton);
            RegisterImageQueueName(nameof(QueueFilterCandidateButton), QueueFilterCandidateButton);
            RegisterImageQueueName(nameof(QueueFilterFailedButton), QueueFilterFailedButton);
            RegisterImageQueueName(nameof(QueueFilterConfirmedButton), QueueFilterConfirmedButton);
            RegisterImageQueueName(nameof(QueueFilterSkippedButton), QueueFilterSkippedButton);
            RegisterImageQueueName(nameof(QueueFilterNoCandidateButton), QueueFilterNoCandidateButton);
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
