using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfUiButton = Wpf.Ui.Controls.Button;

namespace MvcVisionSystem
{
    public partial class WpfClassCatalogPanel : UserControl
    {
        public WpfClassCatalogPanel()
        {
            InitializeComponent();
            DataContext = ViewModel;
        }

        public WpfClassCatalogPanelViewModel ViewModel { get; } = new WpfClassCatalogPanelViewModel();

        public event KeyEventHandler ClassNameKeyDown;
        public event RoutedEventHandler AddClassRequested;
        public event RoutedEventHandler RemoveClassRequested;
        public event RoutedEventHandler BrowseOutputRootRequested;
        public event RoutedEventHandler SaveOutputRootRequested;
        public event SelectionChangedEventHandler ClassSelectionChanged;

        public TextBox ClassNameTextBox => ClassNameBox;
        public WpfUiButton AddClass => AddClassButton;
        public WpfUiButton RemoveClass => RemoveClassButton;
        public TextBox OutputRootPathTextBox => OutputRootPathBox;
        public WpfUiButton BrowseOutputRoot => BrowseOutputRootButton;
        public WpfUiButton SaveOutputRoot => SaveOutputRootButton;
        public TextBlock StatusTextBlock => ClassEditStatusText;
        public ListBox ClassList => ClassListBox;

        private void ClassNameBox_KeyDown(object sender, KeyEventArgs e) => ClassNameKeyDown?.Invoke(sender, e);
        private void AddClassButton_Click(object sender, RoutedEventArgs e) => AddClassRequested?.Invoke(sender, e);
        private void RemoveClassButton_Click(object sender, RoutedEventArgs e) => RemoveClassRequested?.Invoke(sender, e);
        private void BrowseOutputRootButton_Click(object sender, RoutedEventArgs e) => BrowseOutputRootRequested?.Invoke(sender, e);
        private void SaveOutputRootButton_Click(object sender, RoutedEventArgs e) => SaveOutputRootRequested?.Invoke(sender, e);
        private void ClassListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => ClassSelectionChanged?.Invoke(sender, e);
    }
}
