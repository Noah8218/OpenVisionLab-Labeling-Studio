using System.Windows.Controls;
using ListBox = System.Windows.Controls.ListBox;
using TextBox = System.Windows.Controls.TextBox;
using UserControl = System.Windows.Controls.UserControl;
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

        public Border GuidePanel => ClassCatalogGuidePanel;
        public TextBlock GuideTitleTextBlock => ClassCatalogGuideTitleText;
        public TextBlock GuideDetailTextBlock => ClassCatalogGuideDetailText;
        public TextBlock SummaryTextBlock => ClassCatalogSummaryText;
        public TextBlock CurrentDrawingClassTitleTextBlock => CurrentDrawingClassTitleText;
        public TextBlock CurrentDrawingClassDetailTextBlock => CurrentDrawingClassDetailText;
        public TextBlock ActionTextBlock => ClassCatalogActionText;
        public TextBox ClassNameTextBox => ClassNameBox;
        public WpfUiButton AddClass => AddClassButton;
        public WpfUiButton RenameClass => RenameClassButton;
        public WpfUiButton RemoveClass => RemoveClassButton;
        public ListBox ClassColor => ClassColorBox;
        public Expander ClassColorAdvanced => ClassColorAdvancedPanel;
        public WpfUiButton ApplyClassColor => ApplyClassColorButton;
        public TextBlock StatusTextBlock => ClassEditStatusText;
        public ListBox ClassList => ClassListBox;
    }
}
