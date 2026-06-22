using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfUiButton = Wpf.Ui.Controls.Button;

namespace MvcVisionSystem
{
    public partial class WpfObjectReviewPanel : UserControl
    {
        public WpfObjectReviewPanel()
        {
            InitializeComponent();
            DataContext = ViewModel;
        }

        public WpfObjectReviewPanelViewModel ViewModel { get; } = new WpfObjectReviewPanelViewModel();

        public event RoutedEventHandler DeleteObjectRequested;
        public event RoutedEventHandler ApplyObjectClassRequested;
        public event SelectionChangedEventHandler ObjectSelectionChanged;
        public event KeyEventHandler ObjectPreviewKeyDown;

        public TextBlock SummaryTextBlock => ObjectReviewSummaryText;
        public WpfUiButton DeleteButton => DeleteObjectButton;
        public ComboBox ClassBox => ObjectClassBox;
        public WpfUiButton ApplyClassButton => ApplyObjectClassButton;
        public ListBox ObjectList => ObjectListBox;

        private void DeleteObjectButton_Click(object sender, RoutedEventArgs e) => DeleteObjectRequested?.Invoke(sender, e);
        private void ApplyObjectClassButton_Click(object sender, RoutedEventArgs e) => ApplyObjectClassRequested?.Invoke(sender, e);
        private void ObjectListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => ObjectSelectionChanged?.Invoke(sender, e);
        private void ObjectListBox_PreviewKeyDown(object sender, KeyEventArgs e) => ObjectPreviewKeyDown?.Invoke(sender, e);
    }
}
