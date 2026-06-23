using System.Windows.Controls;
using WpfUiButton = Wpf.Ui.Controls.Button;

namespace MvcVisionSystem
{
    public partial class WpfClassCatalogPanel : UserControl
    {
        public WpfClassCatalogPanel()
        {
            InitializeComponent();
        }

        public WpfClassCatalogPanelViewModel ViewModel => DataContext as WpfClassCatalogPanelViewModel;

        public TextBox ClassNameTextBox => ClassNameBox;
        public WpfUiButton AddClass => AddClassButton;
        public WpfUiButton RemoveClass => RemoveClassButton;
        public TextBox OutputRootPathTextBox => OutputRootPathBox;
        public WpfUiButton BrowseOutputRoot => BrowseOutputRootButton;
        public WpfUiButton SaveOutputRoot => SaveOutputRootButton;
        public TextBlock StatusTextBlock => ClassEditStatusText;
        public ListBox ClassList => ClassListBox;
    }
}