using System.Windows.Controls;
using ComboBox = System.Windows.Controls.ComboBox;
using ProgressBar = System.Windows.Controls.ProgressBar;
using TextBox = System.Windows.Controls.TextBox;
using UserControl = System.Windows.Controls.UserControl;
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
        public TextBlock CurrentFolderPathTextBlock => CurrentImageFolderPathText;
        public WpfUiButton OpenCurrentFolderButton => OpenCurrentImageFolderButton;
        public WpfUiButton OpenSelectedButton => OpenSelectedQueueImageButton;
        public WpfUiButton DetectSelectedButton => DetectSelectedQueueButton;
        public WpfUiButton BatchDetectButton => BatchDetectQueueButton;
        public WpfUiButton TemplateBatchButton => TemplateBatchQueueButton;
        public WpfUiButton RetryFailedButton => RetryFailedQueueButton;
        public WpfUiButton StopBatchButton => StopBatchQueueButton;
        public WpfUiButton QueueFilterUnfinished => QueueFilterUnfinishedButton;
        public WpfUiButton QueueFilterAll => QueueFilterAllButton;
        public WpfUiButton QueueFilterCandidate => QueueFilterCandidateButton;
        public WpfUiButton QueueFilterFailed => QueueFilterFailedButton;
        public WpfUiButton QueueFilterConfirmed => QueueFilterConfirmedButton;
        public WpfUiButton QueueFilterSkipped => QueueFilterSkippedButton;
        public WpfUiButton QueueFilterNoCandidate => QueueFilterNoCandidateButton;
        public TextBlock QueueFilterUnfinishedTextBlock => QueueFilterUnfinishedText;
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
