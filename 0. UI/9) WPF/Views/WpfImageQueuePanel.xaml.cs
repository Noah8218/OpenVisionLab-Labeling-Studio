using System.Windows.Controls;
using WpfUiButton = Wpf.Ui.Controls.Button;

namespace MvcVisionSystem
{
    public partial class WpfImageQueuePanel : UserControl
    {
        public WpfImageQueuePanel()
        {
            InitializeComponent();
        }

        public WpfImageQueuePanelViewModel ViewModel => DataContext as WpfImageQueuePanelViewModel;

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
    }
}