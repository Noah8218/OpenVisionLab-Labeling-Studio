using System.Windows.Controls;
using WpfUiButton = Wpf.Ui.Controls.Button;

namespace MvcVisionSystem
{
    public partial class WpfObjectReviewPanel : UserControl
    {
        public WpfObjectReviewPanel()
        {
            InitializeComponent();
        }

        public WpfObjectReviewPanelViewModel ViewModel => DataContext as WpfObjectReviewPanelViewModel;

        public TextBlock SummaryTextBlock => ObjectReviewSummaryText;
        public WpfUiButton DeleteButton => DeleteObjectButton;
        public ComboBox ClassBox => ObjectClassBox;
        public WpfUiButton ApplyClassButton => ApplyObjectClassButton;
        public ListBox ObjectList => ObjectListBox;
    }
}
