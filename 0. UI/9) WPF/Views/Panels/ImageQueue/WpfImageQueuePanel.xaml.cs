using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;
using UserControl = System.Windows.Controls.UserControl;
using WpfUiButton = Wpf.Ui.Controls.Button;

namespace MvcVisionSystem
{
    public partial class WpfImageQueuePanel : UserControl
    {
        private DispatcherOperation columnWidthRefresh;

        public WpfImageQueuePanel()
        {
            InitializeComponent();
            ImageQueueGrid.Unloaded += ImageQueueGrid_Unloaded;
        }

        public WpfImageQueuePanelViewModel ViewModel => DataContext as WpfImageQueuePanelViewModel;

        public ComboBox FilterBox => ImageQueueFilterBox;
        public TextBox SearchBox => ImageQueueSearchBox;
        public DataGrid QueueGrid => ImageQueueGrid;
        public TextBlock PanelTitleTextBlock => ImageQueuePanelTitleText;
        public TextBlock CurrentFolderPathTextBlock => CurrentImageFolderPathText;
        public WpfUiButton OpenCurrentFolderButton => OpenCurrentImageFolderButton;
        public WpfUiButton OpenSelectedButton => OpenSelectedQueueImageButton;
        public WpfUiButton DetectSelectedButton => DetectSelectedQueueButton;
        public WpfUiButton BatchDetectButton => BatchDetectQueueButton;
        public WpfUiButton TemplateBatchButton => TemplateBatchQueueButton;
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
        public ICollectionView QueueView { get; private set; }
        public string SearchText => SearchBox?.Text ?? string.Empty;
        public WpfImageQueueItem SelectedItem => QueueGrid?.SelectedItem as WpfImageQueueItem;

        public ICollectionView InitializeQueue(IEnumerable<WpfImageQueueItem> items)
        {
            FilterBox.SelectedIndex = 0;
            QueueView = CollectionViewSource.GetDefaultView(items);
            QueueView.Filter = ShouldShowQueueItem;
            ConfigureLiveFiltering();
            QueueGrid.ItemsSource = QueueView;
            RefreshQueueColumnWidth();
            return QueueView;
        }

        private void ImageQueueGrid_Loaded(object sender, RoutedEventArgs e) => RefreshQueueColumnWidth();

        private void ImageQueueGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.WidthChanged) RefreshQueueColumnWidth();
        }

        private void RefreshQueueColumnWidth(bool deferRefresh = true)
        {
            if (QueueGrid.ActualWidth <= 0 || QueueGrid.Columns.Count == 0 || QueueGrid.Columns.Count(column => column.Visibility == Visibility.Visible) != 1) return;
            DataGridColumn column = QueueGrid.Columns[0];
            if (!column.Width.IsStar) return;
            double starWeight = column.Width.Value;
            column.Width = DataGridLength.Auto;
            column.Width = new DataGridLength(starWeight, DataGridLengthUnitType.Star);
            if (!deferRefresh || !QueueGrid.IsLoaded || columnWidthRefresh?.Status == DispatcherOperationStatus.Pending
                || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
            columnWidthRefresh = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                columnWidthRefresh = null;
                if (QueueGrid.IsLoaded) RefreshQueueColumnWidth(deferRefresh: false);
            }));
        }

        private void ImageQueueGrid_Unloaded(object sender, RoutedEventArgs e)
        {
            columnWidthRefresh?.Abort();
            columnWidthRefresh = null;
        }

        private bool ShouldShowQueueItem(object item)
        {
            return ViewModel?.ShouldShow(item as WpfImageQueueItem) ?? true;
        }

        public void RefreshQueueViewAfterItemStateChange()
        {
            if (QueueView is ICollectionViewLiveShaping liveShaping
                && liveShaping.IsLiveFiltering == true)
            {
                return;
            }

            QueueView?.Refresh();
        }

        public void RefreshQueueView()
        {
            QueueView?.Refresh();
        }

        private void ConfigureLiveFiltering()
        {
            if (!(QueueView is ICollectionViewLiveShaping liveShaping)
                || !liveShaping.CanChangeLiveFiltering)
            {
                return;
            }

            liveShaping.LiveFilteringProperties.Clear();
            liveShaping.LiveFilteringProperties.Add(nameof(WpfImageQueueItem.FileName));
            liveShaping.LiveFilteringProperties.Add(nameof(WpfImageQueueItem.IsLabeled));
            liveShaping.LiveFilteringProperties.Add(nameof(WpfImageQueueItem.HasSavedLabelFile));
            liveShaping.LiveFilteringProperties.Add(nameof(WpfImageQueueItem.IsSaveRequired));
            liveShaping.LiveFilteringProperties.Add(nameof(WpfImageQueueItem.ReviewState));
            liveShaping.LiveFilteringProperties.Add(nameof(WpfImageQueueItem.QualityReviewState));
            liveShaping.LiveFilteringProperties.Add(nameof(WpfImageQueueItem.AnomalyReviewState));
            liveShaping.LiveFilteringProperties.Add(nameof(WpfImageQueueItem.IsAnomalyReviewItem));
            liveShaping.LiveFilteringProperties.Add(nameof(WpfImageQueueItem.SessionCandidateCount));
            liveShaping.LiveFilteringProperties.Add(nameof(WpfImageQueueItem.LabelSuspicionCount));
            liveShaping.IsLiveFiltering = true;
        }
    }
}
