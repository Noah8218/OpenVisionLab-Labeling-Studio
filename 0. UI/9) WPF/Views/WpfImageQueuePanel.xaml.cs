using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfUiButton = Wpf.Ui.Controls.Button;

namespace MvcVisionSystem
{
    public partial class WpfImageQueuePanel : UserControl
    {
        public WpfImageQueuePanel()
        {
            InitializeComponent();
            DataContext = ViewModel;
        }

        public WpfImageQueuePanelViewModel ViewModel { get; } = new WpfImageQueuePanelViewModel();

        public event RoutedEventHandler LoadImageRootRequested;
        public event RoutedEventHandler BrowseImageFolderRequested;
        public event RoutedEventHandler RefreshImageQueueRequested;
        public event RoutedEventHandler NextUnlabeledRequested;
        public event RoutedEventHandler OpenSelectedQueueImageRequested;
        public event RoutedEventHandler DetectSelectedQueueRequested;
        public event RoutedEventHandler BatchDetectQueueRequested;
        public event RoutedEventHandler RetryFailedQueueRequested;
        public event RoutedEventHandler StopBatchQueueRequested;
        public event RoutedEventHandler QueueFilterAllRequested;
        public event RoutedEventHandler QueueFilterCandidateRequested;
        public event RoutedEventHandler QueueFilterFailedRequested;
        public event RoutedEventHandler QueueFilterConfirmedRequested;
        public event RoutedEventHandler QueueFilterSkippedRequested;
        public event RoutedEventHandler QueueFilterNoCandidateRequested;
        public event SelectionChangedEventHandler FilterSelectionChanged;
        public event TextChangedEventHandler SearchTextChanged;
        public event SelectionChangedEventHandler QueueSelectionChanged;
        public event MouseButtonEventHandler QueueMouseDoubleClick;

        public ComboBox FilterBox => ImageQueueFilterBox;
        public TextBox SearchBox => ImageQueueSearchBox;
        public DataGrid QueueGrid => ImageQueueGrid;
        public TextBlock BatchStatusTextBlock => BatchStatusText;
        public ProgressBar BatchProgress => BatchProgressBar;
        public WpfUiButton OpenSelectedButton => OpenSelectedQueueImageButton;
        public WpfUiButton DetectSelectedButton => DetectSelectedQueueButton;
        public WpfUiButton BatchDetectButton => BatchDetectQueueButton;
        public WpfUiButton RetryFailedButton => RetryFailedQueueButton;
        public WpfUiButton StopBatchButton => StopBatchQueueButton;
        public WpfUiButton QueueFilterAll => QueueFilterAllButton;
        public WpfUiButton QueueFilterCandidate => QueueFilterCandidateButton;
        public WpfUiButton QueueFilterFailed => QueueFilterFailedButton;
        public WpfUiButton QueueFilterConfirmed => QueueFilterConfirmedButton;
        public WpfUiButton QueueFilterSkipped => QueueFilterSkippedButton;
        public WpfUiButton QueueFilterNoCandidate => QueueFilterNoCandidateButton;
        public TextBlock QueueFilterAllTextBlock => QueueFilterAllText;
        public TextBlock QueueFilterCandidateTextBlock => QueueFilterCandidateText;
        public TextBlock QueueFilterFailedTextBlock => QueueFilterFailedText;
        public TextBlock QueueFilterConfirmedTextBlock => QueueFilterConfirmedText;
        public TextBlock QueueFilterSkippedTextBlock => QueueFilterSkippedText;
        public TextBlock QueueFilterNoCandidateTextBlock => QueueFilterNoCandidateText;
        public string SearchText => SearchBox?.Text ?? string.Empty;
        public WpfImageQueueItem SelectedItem => QueueGrid?.SelectedItem as WpfImageQueueItem;

        private void LoadImageRootButton_Click(object sender, RoutedEventArgs e) => LoadImageRootRequested?.Invoke(sender, e);
        private void BrowseImageFolderButton_Click(object sender, RoutedEventArgs e) => BrowseImageFolderRequested?.Invoke(sender, e);
        private void RefreshImageQueueButton_Click(object sender, RoutedEventArgs e) => RefreshImageQueueRequested?.Invoke(sender, e);
        private void NextUnlabeledButton_Click(object sender, RoutedEventArgs e) => NextUnlabeledRequested?.Invoke(sender, e);
        private void OpenSelectedQueueImageButton_Click(object sender, RoutedEventArgs e) => OpenSelectedQueueImageRequested?.Invoke(sender, e);
        private void DetectSelectedQueueButton_Click(object sender, RoutedEventArgs e) => DetectSelectedQueueRequested?.Invoke(sender, e);
        private void BatchDetectQueueButton_Click(object sender, RoutedEventArgs e) => BatchDetectQueueRequested?.Invoke(sender, e);
        private void RetryFailedQueueButton_Click(object sender, RoutedEventArgs e) => RetryFailedQueueRequested?.Invoke(sender, e);
        private void StopBatchQueueButton_Click(object sender, RoutedEventArgs e) => StopBatchQueueRequested?.Invoke(sender, e);
        private void QueueFilterAllButton_Click(object sender, RoutedEventArgs e) => QueueFilterAllRequested?.Invoke(sender, e);
        private void QueueFilterCandidateButton_Click(object sender, RoutedEventArgs e) => QueueFilterCandidateRequested?.Invoke(sender, e);
        private void QueueFilterFailedButton_Click(object sender, RoutedEventArgs e) => QueueFilterFailedRequested?.Invoke(sender, e);
        private void QueueFilterConfirmedButton_Click(object sender, RoutedEventArgs e) => QueueFilterConfirmedRequested?.Invoke(sender, e);
        private void QueueFilterSkippedButton_Click(object sender, RoutedEventArgs e) => QueueFilterSkippedRequested?.Invoke(sender, e);
        private void QueueFilterNoCandidateButton_Click(object sender, RoutedEventArgs e) => QueueFilterNoCandidateRequested?.Invoke(sender, e);
        private void ImageQueueFilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => FilterSelectionChanged?.Invoke(sender, e);
        private void ImageQueueSearchBox_TextChanged(object sender, TextChangedEventArgs e) => SearchTextChanged?.Invoke(sender, e);
        private void ImageQueueGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => QueueSelectionChanged?.Invoke(sender, e);
        private void ImageQueueGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e) => QueueMouseDoubleClick?.Invoke(sender, e);
    }
}
